#!/usr/bin/env python3
"""
esp_hub.py - share one ESP32 serial port with many applications.

Why this exists
---------------
A serial port has exactly one byte stream. If two programs open /dev/ttyUSB0
at the same time, every read() in one program steals bytes from the other, so
both see half-missing, corrupted lines. This hub is the *single* owner of the
port and re-publishes every line the ESP32 prints to any number of clients:

  raw TCP  (default 127.0.0.1:8766)
      Every line exactly as the ESP32 sent it, byte for byte. Anything a
      client sends is written to the ESP32, one line at a time. Works with
      `nc`, or a plain socket in any language.

  HTTP     (default 127.0.0.1:8765)
      GET  /status    hub + serial state, counters, connected clients
      GET  /latest    recent lines from the in-memory history (JSON)
      GET  /stream    live Server-Sent Events stream (JSON per line)
      POST /command   write line(s) to the ESP32
      POST /reset     hardware-reset the board (EN pin via RTS)
      POST /pause     release the serial port, e.g. to upload a sketch
      POST /resume    re-acquire it

See README.md next to this file for the full API with examples.

Requires Python 3.8+ and pyserial (python-pyserial on Arch).
"""

import argparse
import collections
import fcntl
import json
import logging
import math
import os
import queue
import re
import signal
import socket
import socketserver
import sys
import termios
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlsplit

import serial
import serial.tools.list_ports

log = logging.getLogger("esp_hub")

# USB vendor IDs of the USB-serial chips found on ESP32 dev boards. Used by
# --port auto to find the board without hard-coding /dev/ttyUSB0.
KNOWN_USB_VIDS = {
    0x10C4: "Silicon Labs CP210x",
    0x1A86: "WCH CH340/CH9102",
    0x0403: "FTDI",
    0x303A: "Espressif native USB (S2/S3/C3/C6)",
}

RETRY_SECONDS = 1.5        # back-off between (re)connect attempts
MAX_LINE_BYTES = 4096      # flush a "line" this long even without a newline
SETTLE_WINDOW = 0.25       # see Hub._discard_flood
MAX_POST_BYTES = 64 * 1024
SSE_PING_SECONDS = 15      # keep-alive comment so dead SSE clients get noticed

# A line "tag" is the text before the first comma when it looks like an
# upper-case identifier, e.g. "D", "STAT", "EVT", "RECEIVER_MAC".
TAG_RE = re.compile(r"^[A-Z][A-Z0-9_]*$")

# First line of an HTTP request. If one arrives on the raw TCP port it is a
# browser being tricked into talking to us (cross-protocol request), not a
# real client, so we hang up instead of forwarding its body to the ESP32.
HTTP_REQUEST_RE = re.compile(rb"^[A-Z]+ \S+ HTTP/\d")

# CSV layouts printed by reciever-esp32/reciever-esp32.ino. The hub works with
# any line-based firmware; these only add a convenience "data" object to JSON
# output. Raw TCP clients always get the untouched bytes.
def _num(text):
    # printf prints a dead sensor as "nan"; JSON has no NaN (browsers'
    # JSON.parse rejects it), so map non-finite values to null.
    value = float(text)
    return value if math.isfinite(value) else None


_D8 = (("t_ms", int), ("ax", _num), ("ay", _num), ("az", _num),
       ("gx", _num), ("gy", _num), ("gz", _num), ("batt", _num))
_D14 = _D8 + (("jx", int), ("jy", int), ("jx_raw", int), ("jy_raw", int), ("act_a", _num), ("act_g", _num))
_D16 = _D14 + (("hand", int), ("btn", int))   # hand 0 = left glove, 1 = right glove; btn = joystick click

CSV_LAYOUTS = {
    # Fields after the tag: older firmware sends 8, joystick firmware 14, two-glove click firmware 16.
    "D": (_D8, _D14, _D16),
    "STAT": (("packets", int), ("bad_len", int), ("dropped", int)),
    "ACK": (("cmd", int), ("value", _num)),
    "EVT": (("event", str),),
    "ERR": (("message", str),),
    "RECEIVER_MAC": (("mac", str),),
}

# One line received from the ESP32. seq increases by 1 per line for the life
# of the hub process, so clients can detect gaps; ts is the hub's receive
# time (Unix seconds); raw is the exact bytes including the line ending.
Line = collections.namedtuple("Line", "seq ts raw")


def line_text(line):
    return line.raw.rstrip(b"\r\n").decode("utf-8", "replace")


def text_tag(text):
    tag = text.split(",", 1)[0]
    return tag if TAG_RE.match(tag) else None


def parse_fields(text):
    """Parse a known receiver CSV line into a dict, or return None."""
    tag, _, rest = text.partition(",")
    layout = CSV_LAYOUTS.get(tag)
    if layout is None:
        return None
    if layout and isinstance(layout[0][0], tuple):   # several accepted layouts: pick the one with this many fields
        parts = rest.split(",")
        layout = next((l for l in layout if len(l) == len(parts)), None)
        if layout is None:
            return None
    elif len(layout) == 1 and layout[0][1] is str:
        return {layout[0][0]: rest}  # free text, may itself contain commas
    parts = rest.split(",")
    if len(parts) != len(layout):
        return None
    try:
        return {name: conv(part) for (name, conv), part in zip(layout, parts)}
    except ValueError:
        return None


def line_to_dict(line):
    """JSON shape of a line, shared by /latest, /stream and /command."""
    text = line_text(line)
    obj = {"seq": line.seq, "ts": round(line.ts, 6), "line": text}
    tag = text_tag(text)
    if tag:
        obj["tag"] = tag
        data = parse_fields(text)
        if data is not None:
            obj["data"] = data
    return obj


def resolve_port(port_arg):
    """Return the device path to open, or (None, reason) if there isn't one."""
    if port_arg != "auto":
        if os.path.exists(port_arg):
            return port_arg, None
        return None, "%s does not exist (unplugged?)" % port_arg
    found = sorted((p for p in serial.tools.list_ports.comports()
                    if p.vid in KNOWN_USB_VIDS), key=lambda p: p.device)
    if not found:
        return None, "no USB serial device from a known ESP32 USB chip found"
    if len(found) > 1:
        # Guessing wrong here would silently read the wrong board (e.g. the
        # sender, if it is also on USB), so make the user choose.
        return None, "several USB serial devices found (%s); pass --port" % (
            ", ".join(p.device for p in found))
    return found[0].device, None


class HubError(Exception):
    """A request the hub refuses; status is the HTTP status code to use."""

    def __init__(self, status, message):
        super().__init__(message)
        self.status = status
        self.message = message


class Subscriber:
    """One connected client's bounded queue of ("line"|"status", item)."""

    def __init__(self, kind, peer, maxsize):
        self.kind = kind
        self.peer = peer
        self.since = time.time()
        self.queue = queue.Queue(maxsize)
        self.dropped = 0

    def offer(self, item):
        # Only ever called with Hub._sub_lock held, so there is a single
        # producer and drop-oldest cannot race. A slow client loses its own
        # oldest lines; it never slows down the serial reader or other clients.
        try:
            self.queue.put_nowait(item)
        except queue.Full:
            try:
                self.queue.get_nowait()
            except queue.Empty:
                pass
            self.dropped += 1
            self.queue.put_nowait(item)

    def info(self):
        return {"kind": self.kind, "peer": self.peer, "since": round(self.since, 3),
                "queued": self.queue.qsize(), "dropped": self.dropped}


class Hub:
    """Owns the serial port and fans its lines out to subscribers."""

    def __init__(self, port="auto", baud=115200, history=2000,
                 read_only=False, allow_fmt=False):
        self.port_arg = port
        self.baud = baud
        self.read_only = read_only
        self.allow_fmt = allow_fmt
        self.history = collections.deque(maxlen=history)
        self.started = time.time()

        self._subs = set()
        self._sub_lock = threading.Lock()   # subscribers, history, counters
        self._ser = None
        self._ser_lock = threading.Lock()   # self._ser, writes, DTR/RTS changes
        self._stop = threading.Event()
        self._wake = threading.Event()      # cuts the reconnect back-off short
        self._paused_until = None           # None, a Unix time, or float("inf")

        self.state = "starting"             # connected | settling | waiting | paused | stopped
        self.error = None
        self.device = None
        self.connected_since = None
        self.seq = 0
        self.bytes_total = 0
        self.stale_bytes_discarded = 0
        self.flood_events = 0
        self._skip_partial_line = False
        self.writes_total = 0
        self.last_line_ts = None
        self.last_by_tag = {}

    # ---------------------------------------------------------------- serial

    def run_serial(self):
        """Serial thread: connect, read until the port goes away, repeat."""
        while not self._stop.is_set():
            if self._is_paused():
                self._set_state("paused")
                self._wait(0.5)
                continue
            device, reason = resolve_port(self.port_arg)
            if device is None:
                self._set_state("waiting", reason)
                self._wait(RETRY_SECONDS)
                continue
            try:
                ser = self._open(device)
            except (serial.SerialException, OSError) as e:
                self._set_state("waiting", "cannot open %s: %s" % (device, e))
                self._wait(RETRY_SECONDS)
                continue

            with self._ser_lock:
                self._ser = ser
            self.device = device
            self.connected_since = time.time()
            error = None
            try:
                self._discard_flood(ser)
                if not self._stop.is_set() and not self._is_paused():
                    self._set_state("connected")
                    self._read_loop(ser)
            except (serial.SerialException, OSError) as e:
                error = "lost %s: %s" % (device, e)
            finally:
                with self._ser_lock:
                    self._ser = None
                    self._close(ser)
                self.connected_since = None
            if error and not self._stop.is_set():
                self._set_state("waiting", error)
                self._wait(RETRY_SECONDS)
        self._set_state("stopped")

    def _open(self, device):
        # Opening with pyserial's default DTR/RTS levels does NOT reset the
        # ESP32 (verified on the CP2102 board); only /reset toggles them.
        ser = serial.Serial(device, self.baud, timeout=0.25, write_timeout=2,
                            exclusive=True)
        try:
            # exclusive=True is only an advisory flock(). TIOCEXCL makes the
            # kernel refuse every other open() of this tty, so the Arduino IDE
            # or a second reader fails loudly with "port busy" instead of
            # silently stealing half the bytes.
            fcntl.ioctl(ser.fileno(), termios.TIOCEXCL)
        except OSError as e:
            log.warning("could not set TIOCEXCL on %s: %s", device, e)
        try:
            # Throw away whatever piled up before we opened, so clients never
            # get stale lines stamped with "now". (Not always enough on its
            # own; see _discard_flood.)
            ser.reset_input_buffer()
        except (serial.SerialException, OSError):
            self._close(ser)
            raise
        return ser

    def _discard_flood(self, ser):
        """Discard input for as long as it arrives faster than the wire allows.

        Observed on this machine (CP2102 board, kernel 6.18): after the port
        has been closed for a few minutes, opening it can produce the last
        line the ESP32 printed ("STAT,0,0,0") repeated at ~350 KB/s for up
        to ~17 s, about 6 MB, even after tcflush(). It even survives closing
        and reopening the port. Live data can never arrive faster than the
        baud rate allows (115200 baud 8N1 = 11.5 KB/s), so the flood is easy
        to recognise: read in short windows and throw everything away until
        one window's rate is plausible for a real wire (with 2x headroom for
        USB packet batching). The bytes of that last window are dropped too,
        and so is the rest of any line it cut into (via _skip_partial_line),
        so publishing restarts on a line boundary. Runs on every connect,
        where it costs ~0.25 s of data, and whenever _read_loop sees a flood.
        There is no time limit; while it lasts, state is "settling".
        """
        wire_bytes_per_s = self.baud / 10.0
        discarded = 0
        last_byte = b"\n"
        announced = False
        while not self._stop.is_set() and not self._is_paused():
            start, n = time.monotonic(), 0
            while time.monotonic() - start < SETTLE_WINDOW:
                chunk = ser.read(ser.in_waiting or 1)
                if chunk:
                    n += len(chunk)
                    last_byte = chunk[-1:]
            discarded += n
            if n <= 2 * wire_bytes_per_s * (time.monotonic() - start):
                break
            if not announced:
                announced = True
                self._set_state("settling", "discarding serial input that arrives faster "
                                            "than %d baud can carry (stale backlog)" % self.baud)
        self.stale_bytes_discarded += discarded
        if announced:
            log.warning("discarded %d bytes of stale serial input", discarded)
        self._skip_partial_line = last_byte != b"\n"

    @staticmethod
    def _close(ser):
        try:
            fcntl.ioctl(ser.fileno(), termios.TIOCNXCL)
        except (serial.SerialException, OSError, ValueError):
            pass
        try:
            ser.close()
        except (serial.SerialException, OSError):
            pass

    def _read_loop(self, ser):
        buf = bytearray()
        skipping = self._skip_partial_line
        # Token bucket that refills at twice the wire's maximum rate and holds
        # 1 s of wire data, so normal bursts (USB batching, the hub being
        # briefly descheduled) pass, but a flood like the one described in
        # _discard_flood empties it within ~40 ms.
        rate = 2 * self.baud / 10.0
        capacity = self.baud / 10.0
        tokens, last = capacity, time.monotonic()
        while not self._stop.is_set() and not self._is_paused():
            chunk = ser.read(ser.in_waiting or 1)
            if not chunk:
                continue
            now = time.monotonic()
            tokens = min(capacity, tokens + (now - last) * rate) - len(chunk)
            last = now
            if tokens < 0:
                self.flood_events += 1
                log.warning("serial input is arriving faster than %d baud can carry; "
                            "discarding it", self.baud)
                buf.clear()
                self._discard_flood(ser)
                if self._stop.is_set() or self._is_paused():
                    return
                self._set_state("connected")
                skipping = self._skip_partial_line
                tokens, last = capacity, time.monotonic()
                continue
            self.bytes_total += len(chunk)
            buf += chunk
            if skipping:
                nl = buf.find(b"\n")
                if nl < 0:
                    buf.clear()
                    continue
                del buf[:nl + 1]
                skipping = False
            while True:
                nl = buf.find(b"\n")
                if nl < 0:
                    break
                self._publish(bytes(buf[:nl + 1]))
                del buf[:nl + 1]
            if len(buf) >= MAX_LINE_BYTES:
                self._publish(bytes(buf))
                buf.clear()

    def _publish(self, raw):
        text = raw.rstrip(b"\r\n").decode("utf-8", "replace")
        tag = text_tag(text)
        with self._sub_lock:
            self.seq += 1
            line = Line(self.seq, time.time(), raw)
            self.history.append(line)
            self.last_line_ts = line.ts
            if tag:
                self.last_by_tag[tag] = text
            for sub in self._subs:
                sub.offer(("line", line))

    def _set_state(self, state, error=None):
        if (state, error) == (self.state, self.error):
            return
        self.state, self.error = state, error
        if error:
            log.warning("serial %s: %s", state, error)
        else:
            log.info("serial %s%s", state, " (%s)" % self.device if state == "connected" else "")
        brief = self.brief_status()
        with self._sub_lock:
            for sub in self._subs:
                sub.offer(("status", brief))

    def _is_paused(self):
        until = self._paused_until
        if until is None:
            return False
        if time.time() >= until:
            self._paused_until = None
            log.info("pause expired, re-acquiring the serial port")
            return False
        return True

    def _wait(self, seconds):
        self._wake.wait(seconds)
        self._wake.clear()

    # ------------------------------------------------------------- clients

    def subscribe(self, kind, peer, maxsize=1000, replay=0, after_seq=None):
        """Register a client. Optionally pre-fill its queue from history:
        the last `replay` lines, or every line with seq > after_seq."""
        sub = Subscriber(kind, peer, maxsize)
        with self._sub_lock:
            if after_seq is not None:
                backlog = [l for l in self.history if l.seq > after_seq]
            elif replay > 0:
                backlog = list(self.history)[-replay:]
            else:
                backlog = []
            for line in backlog[-maxsize:]:
                sub.offer(("line", line))
            self._subs.add(sub)
        log.info("client connected: %s %s", kind, peer)
        return sub

    def unsubscribe(self, sub):
        with self._sub_lock:
            if sub not in self._subs:
                return
            self._subs.discard(sub)
        log.info("client disconnected: %s %s (dropped %d)", sub.kind, sub.peer, sub.dropped)

    def latest(self, n=100, after_seq=None):
        with self._sub_lock:
            lines = list(self.history)
        if after_seq is not None:
            lines = [l for l in lines if l.seq > after_seq]
        return lines[-n:] if n > 0 else []

    def write_lines(self, lines):
        """Write each line (bytes, no newline) to the ESP32 as one atomic
        group, so commands from different clients never interleave."""
        cleaned = []
        for line in lines:
            line = line.rstrip(b"\r\n")
            if b"\n" in line or b"\r" in line:
                raise HubError(400, "a command line may not contain a line break")
            if not line.strip():
                continue
            if self.read_only:
                raise HubError(403, "hub is running with --read-only")
            # The firmware trims and upper-cases input, so match the same way.
            if not self.allow_fmt and line.strip().upper().startswith(b"FMT,"):
                raise HubError(403, "FMT,* switches the output format for every "
                                    "client at once; restart the hub with --allow-fmt "
                                    "if you really want that")
            cleaned.append(line)
        if not cleaned:
            raise HubError(400, "nothing to send")
        with self._ser_lock:
            if self._ser is None:
                raise HubError(503, "ESP32 not connected (state: %s)" % self.state)
            try:
                for line in cleaned:
                    self._ser.write(line + b"\n")
            except (serial.SerialException, OSError) as e:
                raise HubError(503, "write failed: %s" % e)
            self.writes_total += len(cleaned)
        return cleaned

    def reset_board(self):
        """Hardware reset, the same sequence esptool uses. On ESP32 dev boards
        RTS drives EN (reset) and DTR drives GPIO0 (boot mode) through two
        transistors: hold EN low with GPIO0 high, then release EN, and the
        board reboots into the sketch."""
        if self.read_only:
            raise HubError(403, "hub is running with --read-only")
        with self._ser_lock:
            if self._ser is None:
                raise HubError(503, "ESP32 not connected (state: %s)" % self.state)
            try:
                self._ser.dtr = False
                self._ser.rts = True
                time.sleep(0.1)
                self._ser.rts = False
            except (serial.SerialException, OSError) as e:
                raise HubError(503, "reset failed: %s" % e)

    def pause(self, seconds=None):
        """Release the port (until resume(), or for `seconds`) and wait until
        it is actually closed so the caller can use it right away."""
        self._paused_until = time.time() + seconds if seconds else float("inf")
        self._wake.set()  # don't sit out a reconnect back-off first
        deadline = time.time() + 3
        while time.time() < deadline:
            if self.state == "paused":  # only set once the port is closed
                return
            time.sleep(0.02)
        raise HubError(500, "serial port did not close within 3 s")

    def resume(self):
        self._paused_until = None
        self._wake.set()

    def stop(self):
        self._stop.set()
        self._wake.set()

    @property
    def stopping(self):
        return self._stop.is_set()

    def wait_stopped(self, timeout):
        return self._stop.wait(timeout)

    # -------------------------------------------------------------- status

    def brief_status(self):
        until = self._paused_until
        return {
            "state": self.state,
            "device": self.device,
            "error": self.error,
            "paused_until": None if until in (None, float("inf")) else round(until, 3),
        }

    def status(self):
        now = time.time()
        with self._sub_lock:
            clients = [s.info() for s in self._subs]
            last_by_tag = dict(self.last_by_tag)
            lines_total = self.seq
        status = self.brief_status()
        status.update({
            "port_arg": self.port_arg,
            "baud": self.baud,
            "connected_since": self.connected_since and round(self.connected_since, 3),
            "uptime_s": round(now - self.started, 1),
            "lines_total": lines_total,
            "bytes_total": self.bytes_total,
            "stale_bytes_discarded": self.stale_bytes_discarded,
            "flood_events": self.flood_events,
            "writes_total": self.writes_total,
            "last_line_ts": self.last_line_ts and round(self.last_line_ts, 3),
            "last_line_age_s": self.last_line_ts and round(now - self.last_line_ts, 2),
            "last_by_tag": last_by_tag,
            "read_only": self.read_only,
            "allow_fmt": self.allow_fmt,
            "clients": clients,
        })
        return status


# ------------------------------------------------------------------- HTTP

class HttpHandler(BaseHTTPRequestHandler):
    server_version = "esp-hub/1.0"

    @property
    def hub(self):
        return self.server.hub

    def log_message(self, fmt, *args):
        log.debug("http %s %s", self.address_string(), fmt % args)

    # --- routing

    def do_GET(self):
        url = urlsplit(self.path)
        q = parse_qs(url.query)
        routes = {"/": self.get_index, "/status": self.get_status,
                  "/latest": self.get_latest, "/stream": self.get_stream}
        handler = routes.get(url.path.rstrip("/") or "/")
        if handler is None:
            return self.send_json(404, {"error": "no such endpoint", "see": "GET /"})
        try:
            handler(q)
        except HubError as e:
            self.send_json(e.status, {"error": e.message})
        except (BrokenPipeError, ConnectionResetError):
            pass

    def do_POST(self):
        url = urlsplit(self.path)
        q = parse_qs(url.query)
        routes = {"/command": self.post_command, "/reset": self.post_reset,
                  "/pause": self.post_pause, "/resume": self.post_resume}
        handler = routes.get(url.path.rstrip("/"))
        if handler is None:
            return self.send_json(404, {"error": "no such endpoint", "see": "GET /"})
        if not self.post_origin_allowed():
            return self.send_json(403, {"error": "cross-origin POST refused; start the "
                                                 "hub with --allow-origin to permit it"})
        try:
            handler(q, self.read_body())
        except HubError as e:
            self.send_json(e.status, {"error": e.message})
        except (BrokenPipeError, ConnectionResetError):
            pass

    def do_OPTIONS(self):
        # CORS preflight. Reads are open to any origin; writes only to
        # origins listed with --allow-origin.
        self.send_response(204)
        self.send_cors_headers(post=True)
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Last-Event-ID")
        self.send_header("Access-Control-Max-Age", "600")
        self.send_header("Content-Length", "0")
        self.end_headers()

    # --- helpers

    def post_origin_allowed(self):
        """Browsers always send Origin on POSTs; curl and scripts do not.
        Refuse POSTs from web pages unless explicitly allowed, so a random
        website can't fire calibrate/motor commands at your band. There is
        deliberately no "same origin" exception: the hub serves no pages, and
        a DNS-rebinding page (evil.com resolved to 127.0.0.1) would look
        same-origin because its Origin and Host headers match."""
        origin = self.headers.get("Origin")
        return origin is None or origin in self.server.allowed_origins

    def send_cors_headers(self, post=False):
        origin = self.headers.get("Origin")
        if post and origin and origin in self.server.allowed_origins:
            self.send_header("Access-Control-Allow-Origin", origin)
            self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
            self.send_header("Vary", "Origin")
        else:
            self.send_header("Access-Control-Allow-Origin", "*")
            self.send_header("Access-Control-Allow-Methods", "GET, OPTIONS")

    def send_json(self, code, obj):
        body = (json.dumps(obj) + "\n").encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_cors_headers(post=self.command == "POST")
        self.end_headers()
        self.wfile.write(body)

    def read_body(self):
        length = int(self.headers.get("Content-Length") or 0)
        if length > MAX_POST_BYTES:
            raise HubError(413, "body too large")
        return self.rfile.read(length) if length else b""

    @staticmethod
    def int_param(q, name, default):
        try:
            return int(q[name][0]) if name in q else default
        except ValueError:
            raise HubError(400, "%s must be an integer" % name)

    # --- GET endpoints

    def get_index(self, q):
        self.send_json(200, {
            "service": "esp-hub",
            "raw_tcp": "%s:%d" % self.server.tcp_address,
            "endpoints": {
                "GET /status": "hub and serial state, counters, clients",
                "GET /latest?n=100&since=SEQ": "recent lines as JSON",
                "GET /stream?replay=N": "Server-Sent Events, one JSON event per line",
                "POST /command[?expect=ACK,ERR&timeout=2]": "write line(s) to the ESP32",
                "POST /reset": "hardware-reset the ESP32",
                "POST /pause[?seconds=N]": "release the serial port (e.g. to upload)",
                "POST /resume": "re-acquire the serial port",
            },
        })

    def get_status(self, q):
        self.send_json(200, self.hub.status())

    def get_latest(self, q):
        n = self.int_param(q, "n", 100)
        since = self.int_param(q, "since", None)
        lines = self.hub.latest(n, since)
        self.send_json(200, {"lines": [line_to_dict(l) for l in lines],
                             "last_seq": self.hub.seq})

    def get_stream(self, q):
        replay = self.int_param(q, "replay", 0)
        # EventSource sends Last-Event-ID when it reconnects; resume from there
        # so a browser that blinked off for a moment doesn't miss lines.
        last_id = self.headers.get("Last-Event-ID")
        after_seq = int(last_id) if last_id and last_id.isdigit() else None
        sub = self.hub.subscribe("sse", self.address_string(), self.server.queue_size,
                                 replay=replay, after_seq=after_seq)
        try:
            self.send_response(200)
            self.send_header("Content-Type", "text/event-stream")
            self.send_header("Cache-Control", "no-cache")
            self.send_header("X-Accel-Buffering", "no")
            self.send_cors_headers()
            self.end_headers()
            self.write_sse("retry: 2000\n\n")
            self.write_sse("event: status\ndata: %s\n\n" % json.dumps(self.hub.brief_status()))
            while not self.hub.stopping:
                try:
                    kind, item = sub.queue.get(timeout=SSE_PING_SECONDS)
                except queue.Empty:
                    self.write_sse(": ping\n\n")
                    continue
                if kind == "line":
                    # Unnamed event, so a browser's EventSource.onmessage fires.
                    self.write_sse("id: %d\ndata: %s\n\n" % (item.seq, json.dumps(line_to_dict(item))))
                else:
                    self.write_sse("event: status\ndata: %s\n\n" % json.dumps(item))
        except (BrokenPipeError, ConnectionResetError, OSError):
            pass
        finally:
            self.hub.unsubscribe(sub)
            self.close_connection = True

    def write_sse(self, text):
        self.wfile.write(text.encode())
        self.wfile.flush()

    # --- POST endpoints

    def post_command(self, q, body):
        ctype = (self.headers.get("Content-Type") or "").split(";")[0].strip()
        if ctype == "application/json":
            try:
                obj = json.loads(body or b"{}")
            except ValueError:
                raise HubError(400, "invalid JSON")
            if not isinstance(obj, dict):
                raise HubError(400, 'expected {"line": "..."} or {"lines": [...]}')
            lines = obj.get("lines") or ([obj["line"]] if "line" in obj else [])
            if not all(isinstance(l, str) for l in lines):
                raise HubError(400, "lines must be strings")
            lines = [l.encode() for l in lines]
        else:
            lines = body.splitlines()

        expect = {t.strip().upper() for t in q.get("expect", [""])[0].split(",") if t.strip()}
        try:
            timeout = min(float(q.get("timeout", ["2"])[0]), 30.0)
        except ValueError:
            raise HubError(400, "timeout must be a number")

        # Subscribe *before* writing so a fast reply can't slip past us.
        sub = self.hub.subscribe("http-command", self.address_string(), 500) if expect else None
        try:
            sent = self.hub.write_lines(lines)
            result = {"ok": True, "sent": [l.decode("utf-8", "replace") for l in sent]}
            if sub:
                result["reply"] = self.wait_for_tag(sub, expect, timeout)
        finally:
            if sub:
                self.hub.unsubscribe(sub)
        self.send_json(200, result)

    @staticmethod
    def wait_for_tag(sub, tags, timeout):
        deadline = time.time() + timeout
        while True:
            remaining = deadline - time.time()
            if remaining <= 0:
                return None
            try:
                kind, item = sub.queue.get(timeout=remaining)
            except queue.Empty:
                return None
            if kind == "line" and text_tag(line_text(item)) in tags:
                return line_to_dict(item)

    def post_reset(self, q, body):
        self.hub.reset_board()
        self.send_json(200, {"ok": True, "note": "board is rebooting; watch the stream"})

    def post_pause(self, q, body):
        seconds = self.int_param(q, "seconds", None)
        self.hub.pause(seconds)
        self.send_json(200, {"ok": True, "status": self.hub.brief_status()})

    def post_resume(self, q, body):
        self.hub.resume()
        self.send_json(200, {"ok": True})


# ---------------------------------------------------------------- raw TCP

class RawTcpHandler(socketserver.BaseRequestHandler):
    """Sends every ESP32 line verbatim; forwards received lines to the ESP32.

    Nothing but ESP32 output is ever written to the socket (no greetings,
    no error messages), so the stream is safe to parse blindly. Rejected
    commands are logged by the hub instead.
    """

    def handle(self):
        hub = self.server.hub
        sock = self.request
        peer = "%s:%d" % self.client_address[:2]
        sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        sub = hub.subscribe("tcp", peer, self.server.queue_size)
        hang_up = threading.Event()
        threading.Thread(target=self.forward_commands, args=(sock, hub, peer, hang_up),
                         daemon=True, name="tcp-in-" + peer).start()
        try:
            while not hang_up.is_set() and not hub.stopping:
                try:
                    kind, item = sub.queue.get(timeout=0.5)
                except queue.Empty:
                    continue
                if kind == "line":
                    sock.sendall(item.raw)
        except OSError:
            pass  # client went away
        finally:
            hub.unsubscribe(sub)

    @staticmethod
    def forward_commands(sock, hub, peer, hang_up):
        # A client closing its sending side (e.g. `nc` with stdin at EOF) only
        # ends this thread; it keeps receiving lines until it disconnects.
        f = sock.makefile("rb")
        first = True
        try:
            while True:
                raw = f.readline(MAX_LINE_BYTES)
                if not raw:
                    return
                if first and HTTP_REQUEST_RE.match(raw):
                    log.warning("tcp %s sent an HTTP request to the raw port; "
                                "hanging up without forwarding it", peer)
                    hang_up.set()
                    sock.shutdown(socket.SHUT_RDWR)
                    return
                first = False
                if not raw.strip():
                    continue
                try:
                    hub.write_lines([raw])
                except HubError as e:
                    log.warning("tcp %s: %r not sent: %s", peer, raw.rstrip(), e.message)
        except OSError:
            pass


class RawTcpServer(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True


class HubHttpServer(ThreadingHTTPServer):
    allow_reuse_address = True
    daemon_threads = True


# ------------------------------------------------------------------- main

def start_servers(hub, host="127.0.0.1", http_port=8765, tcp_port=8766,
                  queue_size=1000, allowed_origins=()):
    """Start the serial thread and both servers; returns (http, tcp).
    Port 0 picks a free port (used by the tests)."""
    tcp = RawTcpServer((host, tcp_port), RawTcpHandler)
    tcp.hub, tcp.queue_size = hub, queue_size
    http = HubHttpServer((host, http_port), HttpHandler)
    http.hub, http.queue_size = hub, queue_size
    http.allowed_origins = set(allowed_origins)
    http.tcp_address = tcp.server_address[:2]
    for name, target in (("serial", hub.run_serial), ("http", http.serve_forever),
                         ("tcp", tcp.serve_forever)):
        threading.Thread(target=target, name=name, daemon=True).start()
    return http, tcp


def main(argv=None):
    p = argparse.ArgumentParser(
        description="Share one ESP32 serial port with many apps over TCP and HTTP.",
        epilog="Full documentation: README.md next to this script.")
    p.add_argument("--port", default="auto",
                   help="serial device, e.g. /dev/serial/by-id/usb-Silicon_Labs_..."
                        " (default: auto-detect a single ESP32 USB-serial chip)")
    p.add_argument("--baud", type=int, default=115200, help="default: %(default)s")
    p.add_argument("--host", default="127.0.0.1",
                   help="address to listen on; 0.0.0.0 exposes the hub (and its "
                        "command endpoints) to your network (default: %(default)s)")
    p.add_argument("--http-port", type=int, default=8765, help="default: %(default)s")
    p.add_argument("--tcp-port", type=int, default=8766, help="default: %(default)s")
    p.add_argument("--history", type=int, default=2000,
                   help="lines kept in memory for /latest and replay (default: %(default)s)")
    p.add_argument("--queue-size", type=int, default=1000,
                   help="per-client buffer; a client that falls further behind "
                        "loses its oldest lines (default: %(default)s)")
    p.add_argument("--read-only", action="store_true",
                   help="refuse all writes to the ESP32 (commands, reset)")
    p.add_argument("--allow-fmt", action="store_true",
                   help="allow FMT,HUMAN / FMT,CSV, which change the output for every client")
    p.add_argument("--allow-origin", action="append", default=[], metavar="ORIGIN",
                   help="let a web page at ORIGIN (e.g. http://localhost:5173) send "
                        "POST requests; repeatable")
    p.add_argument("-v", "--verbose", action="store_true", help="log every HTTP request")
    args = p.parse_args(argv)

    logging.basicConfig(level=logging.DEBUG if args.verbose else logging.INFO,
                        format="%(asctime)s %(levelname)-7s %(message)s",
                        datefmt="%H:%M:%S")

    hub = Hub(port=args.port, baud=args.baud, history=args.history,
              read_only=args.read_only, allow_fmt=args.allow_fmt)
    try:
        http, tcp = start_servers(hub, args.host, args.http_port, args.tcp_port,
                                  args.queue_size, args.allow_origin)
    except OSError as e:
        log.error("cannot listen on %s: %s (is another hub already running?)", args.host, e)
        return 1

    log.info("HTTP API on http://%s:%d   raw TCP on %s:%d",
             args.host, http.server_address[1], args.host, tcp.server_address[1])
    if args.host not in ("127.0.0.1", "localhost", "::1"):
        log.warning("listening on %s: anyone who can reach this machine can read "
                    "the data and send commands to the ESP32", args.host)

    for sig in (signal.SIGINT, signal.SIGTERM):
        signal.signal(sig, lambda *_: hub.stop())
    while not hub.wait_stopped(1):
        pass
    log.info("shutting down")
    http.shutdown()
    tcp.shutdown()
    deadline = time.time() + 3
    while hub.state != "stopped" and time.time() < deadline:
        time.sleep(0.05)
    return 0


if __name__ == "__main__":
    sys.exit(main())
