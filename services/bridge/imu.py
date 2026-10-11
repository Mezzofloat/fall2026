"""Glove input: IMU + joystick lines from the ESP32 hub (hub/esp_hub.py, raw TCP) turned into a small, validated snapshot.

Two gloves are expected: left (hand 0: move, click = E) and right (hand 1: look, click = V). Firmware that does not send a
hand id is treated as IMU_DEFAULT_HAND. Nothing here is trusted: every number is range-checked and a stale glove
reads as disconnected, so a dead radio can never leave a joystick "held".
"""
import math
import os
import re
import socket
import threading
import time

LOOPBACK = {"127.0.0.1", "localhost", "::1"}
STALE_SECONDS = 1.5
IDLE_STICK = 25          # |joystick %| below this counts as "thumb not driving"
READABLE = re.compile(
    r"A\[g\]\s+(?P<ax>\S+)\s+(?P<ay>\S+)\s+(?P<az>\S+)\s*\|\s*G\[dps\]\s+(?P<gx>\S+)\s+(?P<gy>\S+)\s+(?P<gz>\S+)\s*\|\s*"
    r"Act\s+(?P<aa>\S+)/(?P<ag>\S+)\s*\|\s*Joy X:\s*(?P<jxr>-?\d+)\s*\(\s*(?P<jx>-?\d+)%\)\s*Y:\s*(?P<jyr>-?\d+)\s*\(\s*(?P<jy>-?\d+)%\)"
    r"(?:\s*Click:\s*(?P<btn>[01]))?\s*\|\s*(?P<state>CONNECTED|DISCONNECTED)(?:\s+(?P<hand>[LR]))?")


def _finite(value):
    try:
        number = float(value)
    except (TypeError, ValueError):
        return None
    return number if math.isfinite(number) else None


def _clamp(value, low, high):
    return low if value < low else high if value > high else value


class Glove:
    def __init__(self, hand):
        self.hand = hand
        self.last_seen = None
        self.jx = self.jy = 0
        self.click = False
        self.act_a = self.act_g = 0.0
        self.gyro = self.accel = 0.0
        self.fidget = 0.0
        self.packets = 0


class ImuState:
    def __init__(self, clock=time.monotonic, default_hand=1):
        self.clock = clock
        self.default_hand = 1 if default_hand else 0
        self.gloves = (Glove(0), Glove(1))
        self.baseline = 0.04
        self.lock = threading.Lock()
        self.bad_lines = 0
        self.firmware = "none"        # "csv8", "csv14", "csv16", "readable"
        self.calibrating_until = 0.0
        self.calibration = []

    # ------------------------------------------------------------------ parsing
    def ingest_line(self, line):
        """Returns True when the line carried a glove sample. Unknown or malformed lines are counted and ignored."""
        if not isinstance(line, str) or len(line) > 400:
            return False
        line = line.strip()
        if line.startswith("D,"):
            return self._ingest_csv(line)
        match = READABLE.search(line)
        if match:
            return self._ingest_readable(match)
        return False

    def _bad(self):
        with self.lock:
            self.bad_lines += 1
        return False

    def _ingest_csv(self, line):
        parts = line.split(",")[1:]
        if len(parts) not in (8, 14, 16):
            return self._bad()
        values = [_finite(p) for p in parts]
        if any(v is None for v in values):
            return self._bad()
        ax, ay, az, gx, gy, gz = values[1:7]
        jx = jy = 0
        act_a = act_g = None
        hand, click = self.default_hand, False
        if len(values) >= 14:
            jx, jy = values[8], values[9]
            act_a, act_g = values[12], values[13]
        if len(values) == 16:
            if values[14] not in (0, 1) or values[15] not in (0, 1):
                return self._bad()
            hand, click = int(values[14]), values[15] == 1
        self._store(hand, ax, ay, az, gx, gy, gz, jx, jy, click, act_a, act_g, {8: "csv8", 14: "csv14", 16: "csv16"}[len(values)])
        return True

    def _ingest_readable(self, m):
        if m.group("state") != "CONNECTED":
            return False
        values = {k: _finite(v) for k, v in m.groupdict().items() if k not in ("state", "hand", "btn")}
        if any(v is None for v in values.values()):
            return self._bad()
        hand = {"L": 0, "R": 1}.get(m.group("hand"), self.default_hand)
        self._store(hand, values["ax"], values["ay"], values["az"], values["gx"], values["gy"], values["gz"], values["jx"], values["jy"],
                    m.group("btn") == "1", values["aa"], values["ag"], "readable")
        return True

    def _store(self, hand, ax, ay, az, gx, gy, gz, jx, jy, click, act_a, act_g, firmware):
        now = self.clock()
        glove = self.gloves[hand]
        accel = math.sqrt(ax * ax + ay * ay + az * az)
        gyro = math.sqrt(gx * gx + gy * gy + gz * gz)
        if act_a is None:   # old firmware: derive the same 0..1 activity the band computes
            act_a = _clamp((abs(accel - 1.0) - .03) / 1.0, 0, 1)
            act_g = _clamp((gyro - 5.0) / 250.0, 0, 1)
        with self.lock:
            dt = _clamp(now - glove.last_seen, 0, 1) if glove.last_seen is not None else 0
            glove.last_seen = now
            glove.jx, glove.jy = int(_clamp(jx, -100, 100)), int(_clamp(jy, -100, 100))
            glove.click = bool(click)
            glove.act_a, glove.act_g = _clamp(act_a, 0, 1), _clamp(act_g, 0, 1)
            glove.accel, glove.gyro = accel, gyro
            glove.packets += 1
            self.firmware = firmware
            # Fidgeting = wrist movement while the thumb is NOT driving the stick. Slow average so one gesture is not "stress".
            idle = abs(glove.jx) < IDLE_STICK and abs(glove.jy) < IDLE_STICK and not glove.click
            raw = max(glove.act_g, glove.act_a * .8)
            if idle and dt > 0:
                glove.fidget += (raw - glove.fidget) * (1 - math.exp(-dt / 4.0))
            if self.calibrating_until:
                if idle:
                    self.calibration.append(raw)
                if now >= self.calibrating_until:
                    if self.calibration:
                        ordered = sorted(self.calibration)
                        self.baseline = _clamp(ordered[len(ordered) // 2], 0, .5)
                    self.calibrating_until, self.calibration = 0.0, []

    # ------------------------------------------------------------------ calibration and output
    def calibrate(self, seconds=15.0):
        """Rest baseline: measured while the player holds still at the start of the tour."""
        with self.lock:
            self.calibrating_until = self.clock() + _clamp(seconds, 1, 60)
            self.calibration = []

    def snapshot(self):
        now = self.clock()
        with self.lock:
            gloves = {}
            for name, glove in (("left", self.gloves[0]), ("right", self.gloves[1])):
                age = now - glove.last_seen if glove.last_seen is not None else -1
                live = 0 <= age <= STALE_SECONDS
                gloves[name] = {"connected": live, "age_seconds": round(age, 2), "jx": glove.jx if live else 0, "jy": glove.jy if live else 0,
                                "click": bool(glove.click and live), "activity": round(max(glove.act_g, glove.act_a), 3) if live else 0,
                                "fidget": round(glove.fidget, 3) if live else 0, "packets": glove.packets}
            live_fidgets = [g["fidget"] for g in gloves.values() if g["connected"]]
            excess = _clamp((max(live_fidgets) - self.baseline) / .35, 0, 1) if live_fidgets else 0
            return {"source": "esp32-gloves", "connected": bool(live_fidgets) or any(g["connected"] for g in gloves.values()),
                    "firmware": self.firmware, "baseline": round(self.baseline, 3), "fidget": round(excess, 3),
                    "calibrating": bool(self.calibrating_until), "bad_lines": self.bad_lines, "left": gloves["left"], "right": gloves["right"]}


class HubReader(threading.Thread):
    """Reads the hub's raw TCP stream (loopback only) and feeds ImuState. Reconnects forever; the hub may start after the game."""

    def __init__(self, state, address="127.0.0.1:8766"):
        super().__init__(daemon=True)
        host, _, port = address.rpartition(":")
        if host not in LOOPBACK or not port.isdigit() or not 0 < int(port) < 65536:
            raise ValueError("IMU hub must be a loopback host:port")
        self.state, self.host, self.port = state, host, int(port)
        self.stop_event = threading.Event()
        self.connected = False

    def run(self):
        while not self.stop_event.is_set():
            try:
                with socket.create_connection((self.host, self.port), timeout=2) as sock:
                    sock.settimeout(2)
                    self.connected = True
                    buffer = b""
                    while not self.stop_event.is_set():
                        try:
                            chunk = sock.recv(4096)
                        except socket.timeout:
                            continue
                        if not chunk:
                            break
                        buffer = (buffer + chunk)[-8192:]
                        while b"\n" in buffer:
                            raw, buffer = buffer.split(b"\n", 1)
                            self.state.ingest_line(raw.decode("utf-8", "replace"))
            except OSError:
                pass
            self.connected = False
            self.stop_event.wait(2.0)

    def stop(self):
        self.stop_event.set()


def from_env():
    address = os.getenv("IMU_HUB", "127.0.0.1:8766")
    state = ImuState(default_hand=int(os.getenv("IMU_DEFAULT_HAND", "1") or 1))
    reader = None
    if address.lower() not in {"off", "none", ""}:
        reader = HubReader(state, address)
    return state, reader
