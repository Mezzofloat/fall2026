# ESP32 serial hub (`esp_hub.py`)

A serial port has exactly one byte stream. If two programs read
`/dev/ttyUSB0` at once, each `read()` steals bytes from the other, and both
get corrupted, half-missing lines. `esp_hub.py` solves this. It is the
**single owner** of the ESP32's serial port, and it re-publishes every line
to as many programs as you like:

```
                                  ┌─► raw TCP  127.0.0.1:8766   nc, any language's sockets
ESP32 ──USB serial──► esp_hub ────┤
       ◄── commands ──            └─► HTTP     127.0.0.1:8765   /status /latest /stream (SSE) /command …
```

- **Raw TCP.** Each client gets every line exactly as the ESP32 sent it,
  byte for byte. Lines a client sends are written to the ESP32.
- **HTTP.** A JSON status endpoint, recent history, and a live
  Server-Sent-Events stream (works in a browser with `EventSource`). Also
  endpoints to send commands, reset the board, and pause the hub so the
  Arduino IDE can upload.

It needs only Python 3.8+ and **pyserial**, which is already installed here
(`python-pyserial` on Arch). There's nothing to `pip install`.

---

## Contents

1. [Quick start](#quick-start)
2. [Uploading sketches while the hub is running](#uploading-sketches-while-the-hub-is-running)
3. [Raw TCP API](#raw-tcp-api-port-8766)
4. [HTTP API](#http-api-port-8765)
5. [The receiver's line protocol](#the-receivers-line-protocol)
6. [Command-line options](#command-line-options)
7. [Running in the background (systemd)](#running-in-the-background-systemd)
8. [How it works](#how-it-works)
9. [Security](#security)
10. [Tests](#tests)
11. [Troubleshooting](#troubleshooting)

---

## Quick start

```sh
cd ~/fall2026/hub
python3 esp_hub.py
```

```
18:10:25 INFO    HTTP API on http://127.0.0.1:8765   raw TCP on 127.0.0.1:8766
18:10:25 INFO    serial connected (/dev/ttyUSB0)
```

Then, from any number of other terminals or programs, all at the same time:

```sh
nc 127.0.0.1 8766                                   # raw lines; type CAL + Enter to send a command
curl -N http://127.0.0.1:8765/stream                # live JSON (SSE)
curl -s http://127.0.0.1:8765/status | python3 -m json.tool
curl -s 'http://127.0.0.1:8765/latest?n=20' | python3 -m json.tool
curl -d CAL 'http://127.0.0.1:8765/command?expect=ACK,ERR'
python3 examples/raw_tcp_client.py --csv run1.csv   # print and record IMU samples
python3 examples/sse_client.py --replay 20
```

Stop the hub with Ctrl-C. It finds the board on its own (`--port auto`). If
the USB cable is unplugged, it waits and reconnects when the board comes back.

---

## Uploading sketches while the hub is running

While the hub is connected, it holds the port **exclusively**. Any other
program that opens the port, including the Arduino IDE's upload and Serial
Monitor, gets an immediate *"port busy / Device or resource busy"* error.
This is on purpose: without the lock, the upload and the hub would steal each
other's bytes, and you'd get confusing timeouts instead.

To upload, release the port:

```sh
curl -X POST http://127.0.0.1:8765/pause              # releases the port (returns once it's really closed)
#   … upload from the Arduino IDE …
curl -X POST http://127.0.0.1:8765/resume             # takes it back
```

Or pause with a timer, so you can't forget to resume:

```sh
curl -X POST 'http://127.0.0.1:8765/pause?seconds=90'
```

Connected clients **stay connected** while the hub is paused; they simply get
no lines. SSE clients get an `event: status` message saying `"paused"`, and
another when the hub reconnects. You can also stop the hub entirely (Ctrl-C,
or `systemctl --user stop esp-hub`).

**Don't use the IDE's Serial Monitor** while the hub runs. Watch the data
with `nc 127.0.0.1 8766` or `curl -N http://127.0.0.1:8765/stream`. You can
type commands straight into `nc`.

---

## Raw TCP API (port 8766)

The simplest possible interface: a plain TCP socket.

- **ESP32 → you:** every line the ESP32 prints, byte-for-byte, including its
  original line ending (`\n` from `printf`, `\r\n` from `println`). The hub
  never injects anything of its own: no greeting, no errors, no status. You
  can parse the stream blindly.
- **You → ESP32:** send text lines ending in `\n`. Each line goes to the
  ESP32 with a `\n` added. Blank lines are ignored. Refused lines (for
  example while `--read-only` is on, or `FMT,…`) are only logged by the hub,
  because reporting them would pollute the raw stream. Use `POST /command`
  if you need to know whether a command was accepted.
- New clients start with the **next** line. They get no history; use
  `GET /latest` or `/stream?replay=N` for that.
- A client that stops reading loses its own oldest lines once its buffer
  (`--queue-size` lines, default 1000) is full. It never slows down the hub
  or the other clients.

Examples:

```sh
nc 127.0.0.1 8766                       # watch + type commands interactively
nc 127.0.0.1 8766 | grep --line-buffered '^D,' > imu.csv
```

```python
import socket
sock = socket.create_connection(("127.0.0.1", 8766))
sock.sendall(b"CAL\n")                  # optional: send a command
for raw in sock.makefile("rb"):
    tag, *fields = raw.decode().strip().split(",")
    if tag == "D":
        t_ms, ax, ay, az, gx, gy, gz, batt = fields
```

```js
// Node.js
const net = require("net"), readline = require("readline");
const sock = net.connect(8766, "127.0.0.1");
readline.createInterface({ input: sock }).on("line", (line) => console.log(line));
```

---

## HTTP API (port 8765)

All responses are JSON unless noted. Errors look like `{"error": "message"}`
and come with an HTTP status:

| Status | When |
|---|---|
| 400 | Bad parameter or body, or nothing to send |
| 403 | Refused: `--read-only`, `FMT,…` without `--allow-fmt`, or a cross-origin browser request |
| 404 | No such endpoint |
| 413 | Body over 64 KiB |
| 503 | ESP32 not connected (unplugged, paused, or reconnecting) |

| Method & path | Purpose |
|---|---|
| `GET /` | Index of endpoints |
| `GET /status` | Hub and serial state, counters, connected clients |
| `GET /latest?n=100&since=SEQ` | Recent lines from memory |
| `GET /stream?replay=N` | Live Server-Sent Events, one event per line |
| `POST /command[?expect=TAGS&timeout=S]` | Write line(s) to the ESP32 |
| `POST /reset` | Hardware-reset the ESP32 |
| `POST /pause[?seconds=N]` | Release the serial port |
| `POST /resume` | Re-acquire the serial port |

### The line object

`/latest`, `/stream` and `/command?expect=` describe each ESP32 line like this:

```json
{
  "seq": 812,
  "ts": 1791670243.486739,
  "line": "D,1234,0.0100,-0.0200,0.9900,1.000,2.000,3.000,3.91",
  "tag": "D",
  "data": {"t_ms": 1234, "ax": 0.01, "ay": -0.02, "az": 0.99,
           "gx": 1.0, "gy": 2.0, "gz": 3.0, "batt": 3.91}
}
```

| Field | Meaning |
|---|---|
| `seq` | Increases by 1 for every line the hub receives, for the life of the hub process. A jump in `seq` means your client missed lines. |
| `ts` | When the hub received the line (Unix time, seconds). This is the PC's clock; `data.t_ms` is the band's own clock. |
| `line` | The line as text, without its line ending. Invalid UTF-8 shows up as `�`. Use raw TCP if you need the exact bytes. |
| `tag` | The text before the first comma, when it is an upper-case word (`D`, `STAT`, `EVT`, …). Missing for other lines, such as boot messages. |
| `data` | Parsed fields, only for the receiver formats listed in [the line protocol](#the-receivers-line-protocol). `nan` values become `null`. Missing if the line doesn't parse. |

### `GET /status`

```sh
curl -s http://127.0.0.1:8765/status
```
```json
{
  "state": "connected",
  "device": "/dev/ttyUSB0",
  "error": null,
  "paused_until": null,
  "port_arg": "auto",
  "baud": 115200,
  "connected_since": 1791670225.749,
  "uptime_s": 41.0,
  "lines_total": 9,
  "bytes_total": 103,
  "stale_bytes_discarded": 0,
  "flood_events": 0,
  "writes_total": 1,
  "last_line_ts": 1791670263.49,
  "last_line_age_s": 1.21,
  "last_by_tag": {"STAT": "STAT,0,0,0", "ERR": "ERR,band not seen yet"},
  "read_only": false,
  "allow_fmt": false,
  "clients": [
    {"kind": "tcp", "peer": "127.0.0.1:50122", "since": 1791670236.5, "queued": 0, "dropped": 0},
    {"kind": "sse", "peer": "127.0.0.1", "since": 1791670236.5, "queued": 0, "dropped": 0}
  ]
}
```

`state` is one of the following:

| `state` | Meaning |
|---|---|
| `connected` | The port is open and being read. |
| `settling` | The port is open, but input is arriving faster than the baud rate allows. The hub is discarding it as stale; see [How it works](#how-it-works). This is usually over within a few seconds. |
| `waiting` | There is no port, or it can't be opened. `error` says why, for example *does not exist (unplugged?)*, *several USB serial devices found*, or *cannot open … busy*. The hub retries every 1.5 s. |
| `paused` | The port was released by `POST /pause`. `paused_until` is a Unix time, or `null` if the pause lasts until `/resume`. |
| `starting` / `stopped` | Briefly, at startup and at shutdown. |

`last_by_tag` holds the most recent line of each tag. `last_by_tag.STAT` is
a cheap way to poll the receiver's packet counters. `last_line_age_s`
growing past ~5 s means the ESP32 has gone quiet; the sketch prints `STAT`
every 5 s.

### `GET /latest`

The hub keeps the last `--history` lines in memory (default 2000).

| Parameter | Default | Meaning |
|---|---|---|
| `n` | 100 | At most this many lines (the newest ones) |
| `since` | none | Only lines with `seq` greater than this |

```sh
curl -s 'http://127.0.0.1:8765/latest?n=5'
```
```json
{"lines": [{"seq": 7, "ts": 1791670248.48, "line": "STAT,0,0,0", "tag": "STAT",
            "data": {"packets": 0, "bad_len": 0, "dropped": 0}}, "…"],
 "last_seq": 9}
```

**Polling without SSE:** keep the last `seq` you saw, and call
`/latest?since=<it>&n=2000` periodically. If a response's first `seq` is
greater than yours + 1, lines fell out of history between polls.

### `GET /stream`: Server-Sent Events

```sh
curl -N http://127.0.0.1:8765/stream
curl -N 'http://127.0.0.1:8765/stream?replay=50'     # start with the last 50 lines
```
```
retry: 2000

event: status
data: {"state": "connected", "device": "/dev/ttyUSB0", "error": null, "paused_until": null}

id: 812
data: {"seq": 812, "ts": 1791670243.48, "line": "STAT,0,0,0", "tag": "STAT", "data": {...}}

: ping
```

- **Line events** are *unnamed* events (so a browser's `onmessage` fires).
  Their `id` is the line's `seq`, and their `data` is the [line object](#the-line-object).
- **`event: status`** is sent on connect and whenever the serial state
  changes (unplugged, paused, reconnected). Its fields are the first four
  of `/status`.
- **`: ping`** comments arrive every 15 s when nothing else is happening.
  Ignore them.
- **Resuming:** when a browser's `EventSource` reconnects, it sends
  `Last-Event-ID`. The hub then replays every line after that `seq` that is
  still in history, so short blips lose nothing.

Browser:

```html
<script>
  const es = new EventSource("http://127.0.0.1:8765/stream");
  es.onmessage = (e) => {
    const msg = JSON.parse(e.data);
    if (msg.tag === "D") console.log(msg.data.ax, msg.data.ay, msg.data.az);
  };
  es.addEventListener("status", (e) => console.log("serial:", JSON.parse(e.data).state));
</script>
```

Python: see `examples/sse_client.py`. It uses the standard library only.

### `POST /command`

The body is one command per line, as plain text:

```sh
curl -d CAL http://127.0.0.1:8765/command
curl -d 'SCALE,0.5' http://127.0.0.1:8765/command
printf 'CONN\nCAL\n' | curl --data-binary @- http://127.0.0.1:8765/command   # several, in order
```

Or as JSON:

```sh
curl -H 'Content-Type: application/json' -d '{"line": "CAL"}' http://127.0.0.1:8765/command
curl -H 'Content-Type: application/json' -d '{"lines": ["CONN", "CAL"]}' http://127.0.0.1:8765/command
```

The response lists what was written: `{"ok": true, "sent": ["CAL"]}`. The
lines in one request are written back to back, so another client's command
can't land in the middle of them.

**Waiting for the reply:** with `expect`, the hub waits for the first line
with one of those tags and returns it. Use `timeout` to set the wait in
seconds (default 2, max 30).

```sh
curl -d CAL 'http://127.0.0.1:8765/command?expect=ACK,ERR'
```
```json
{"ok": true, "sent": ["CAL"],
 "reply": {"seq": 87453, "ts": 1791670237.58, "line": "ERR,band not seen yet", "tag": "ERR",
           "data": {"message": "band not seen yet"}}}
```

`reply` is `null` if nothing matching arrived in time. The receiver sketch
answers band commands with `ACK,<cmd>,<value>`, or with
`ERR,band not seen yet` if it hasn't heard from the band.

`FMT,HUMAN` and `FMT,CSV` are **refused (403) by default**. They switch the
receiver's output format for *every* client at once, and would break every
program parsing CSV. Start the hub with `--allow-fmt` if you really want
them.

### `POST /reset`

```sh
curl -X POST http://127.0.0.1:8765/reset
```

Hardware-resets the ESP32, the same way esptool and the Arduino IDE do: EN is
pulsed low through RTS, with GPIO0 high through DTR. The board reboots into
its sketch. You'll see the ROM boot messages, then `=== ESP-NOW Receiver ===`,
`RECEIVER_MAC,…` and `READY` on the stream. While EN settles, the ROM banner
`ets Jul 29 2019 12:21:46` / `rst:` can repeat a few dozen times; that's
normal for this board.

### `POST /pause` and `POST /resume`

See [Uploading sketches](#uploading-sketches-while-the-hub-is-running).
`/pause` only returns once the port is really closed, so you can upload
immediately afterwards. `?seconds=N` resumes on its own after N seconds.

---

## The receiver's line protocol

This is what `reciever-esp32/reciever-esp32.ino` prints at 115200 baud in its
default CSV mode. The hub passes everything through untouched. The `data`
column shows the fields the HTTP API parses out.

| Line | Example | Meaning | `data` fields |
|---|---|---|---|
| `D,…` | `D,1234,0.0100,-0.0200,0.9900,1.000,2.000,3.000,3.91` | One IMU sample from the band. Three layouts parse, counted after the `D` tag: 8 fields (older firmware), 14 (+ joystick and activity) and 16 (+ glove id and joystick click) | `t_ms` (band clock, ms), `ax ay az` (g), `gx gy gz` (°/s), `batt` (V); with 14+: `jx jy` (-100..100), `jx_raw jy_raw`, `act_a act_g` (0..1); with 16: `hand` (0 left, 1 right), `btn` (1 = joystick pressed) |
| `STAT,…` | `STAT,120,0,0` | Every 5 s: totals since boot | `packets` (D samples received), `bad_len` (ESP-NOW packets of the wrong size), `dropped` (receive queue overflows) |
| `EVT,…` | `EVT,CONNECTED` | The band said hello (`CONNECTED`) or bye (`DISCONNECTED`) | `event` |
| `ACK,…` | `ACK,1,0.00` | A command was sent to the band | `cmd` (1=calibrate, 2=disconnect, 3=motor scale, 4=connect), `value` |
| `ERR,…` | `ERR,band not seen yet` | A command couldn't be sent, or ESP-NOW init failed (`ERR,esp_now_init`) | `message` |
| `RECEIVER_MAC,…` | `RECEIVER_MAC,00:00:00:00:00:00` | At boot. **Prints zeros on this board; the real MAC is `a0:b7:65:0f:4d:1c`** (see the top-level README) | `mac` |
| `READY` | | At boot: ESP-NOW is up | |
| other | `=== ESP-NOW Receiver ===`, ROM boot log | Human-readable; no tag or no fields | |

Commands the receiver accepts (case-insensitive, one per line):

| Command | Effect |
|---|---|
| `CAL` | Tell the band to calibrate (cmd 1) |
| `DISC` | Tell the band to disconnect (cmd 2) |
| `CONN` | Tell the band to connect (cmd 4) |
| `SCALE,<x>` | Set the band's motor scale to `<x>` (cmd 3) |
| `FMT,HUMAN` / `FMT,CSV` | Switch the receiver's output format. **Blocked by the hub unless `--allow-fmt`.** If someone switched it from the Arduino Serial Monitor, `POST /reset` restores CSV, the default at boot. |

Band commands only work after the receiver has heard from the band at least
once. Before that, it replies `ERR,band not seen yet`.

---

## Command-line options

```
python3 esp_hub.py [options]
```

| Option | Default | Meaning |
|---|---|---|
| `--port PATH` | `auto` | Serial device. `auto` picks the single connected USB-serial chip of a kind used on ESP32 boards (CP210x, CH34x, FTDI, Espressif native USB). If there are several, it refuses to guess, so pass one explicitly. Prefer the stable `/dev/serial/by-id/...` path over `/dev/ttyUSB0`, which can change numbers. |
| `--baud N` | `115200` | Must match `Serial.begin()` in the sketch. |
| `--host ADDR` | `127.0.0.1` | Listen address. `0.0.0.0` exposes the hub to your network; see [Security](#security). |
| `--http-port N` | `8765` | HTTP API port. |
| `--tcp-port N` | `8766` | Raw TCP port. |
| `--history N` | `2000` | Lines kept in memory for `/latest`, `replay` and SSE resume. |
| `--queue-size N` | `1000` | Per-client buffer. A client further behind than this loses its oldest lines. |
| `--read-only` | off | Refuse every write: commands, `/reset`. |
| `--allow-fmt` | off | Allow `FMT,HUMAN` / `FMT,CSV`. |
| `--allow-origin ORIGIN` | none | Let a web page served from `ORIGIN` (for example `http://localhost:5173`) make POST requests. Repeatable. GET endpoints are open to every origin anyway. |
| `-v`, `--verbose` | off | Log every HTTP request. |

Example for this machine, pinned to the board:

```sh
python3 esp_hub.py --port /dev/serial/by-id/usb-Silicon_Labs_CP2102_USB_to_UART_Bridge_Controller_0001-if00-port0
```

---

## Running in the background (systemd)

`esp-hub.service` is a systemd **user** unit. It runs as you, not root:

```sh
mkdir -p ~/.config/systemd/user
cp ~/fall2026/hub/esp-hub.service ~/.config/systemd/user/
systemctl --user daemon-reload
systemctl --user enable --now esp-hub        # start now and at every login

systemctl --user status esp-hub
journalctl --user -u esp-hub -f              # live log
systemctl --user stop esp-hub                # free the port completely
```

The unit assumes the repo is at `~/fall2026`. Edit `ExecStart` if it isn't,
or to add options.

---

## How it works

Everything is in `esp_hub.py`, about 950 lines including comments, with
three kinds of threads:

- **Serial thread** (`Hub.run_serial`). It finds the port, opens it, and reads
  in a loop. It splits the input into lines on `\n`; a line longer than
  4096 bytes without a newline is passed on as-is. Each line gets a `seq`
  and a timestamp, goes into the history ring buffer, and is offered to
  every subscriber. If the port disappears (unplugged, or a USB error), the
  thread closes it, reports `waiting`, and retries every 1.5 s. On every
  attempt it looks the device up again, so a board that comes back as
  `/dev/ttyUSB1` is still found.
- **Raw TCP server and HTTP server** (`socketserver` / `http.server`, one
  thread per client). Each client is a `Subscriber` with its own bounded
  queue. When a queue is full, the *oldest* line in it is dropped and
  counted in `/status`. So one stuck client can never block the serial
  reader or anyone else.

Details that matter:

- **Exclusive access.** The port is opened with pyserial's `exclusive=True`
  (an advisory `flock`), *and* `ioctl(TIOCEXCL)`. That makes the kernel
  refuse every other `open()` of the tty. Other programs get a clear "busy"
  error instead of silently splitting the byte stream with the hub. The
  lock is released on close and on `/pause`.
- **No reset on connect.** Opening the port with pyserial's default DTR/RTS
  levels doesn't reset this board, so starting or restarting the hub doesn't
  reboot the ESP32. Only `POST /reset` toggles those lines.
- **A flood of repeated old data is discarded, not published.** This is a
  quirk of this machine (CP2102 board, Linux 6.18), measured on 2026-10-10.
  After the port has been closed for a few minutes, opening it can produce
  the last line the ESP32 printed, `STAT,0,0,0`, **repeated at ~350 KB/s
  for up to ~17 s (about 6 MB)**.
  - The rate is ~30× what 115200 baud can carry, and the volume equals
    15 days of real `STAT` output, so this isn't real data.
  - A kernel input flush (`tcflush`) doesn't remove it, and it even
    survives closing and reopening the port.
  - It happens with or without pyserial, and not every time.

  Without protection, every client would get hundreds of thousands of
  duplicate lines stamped with the current time. If the last real line had
  been a `D` sample, that would look like the band frozen on one reading.

  Live data can never arrive faster than the wire allows (115200 baud 8N1 ≈
  11.5 KB/s), so the hub defends in two places:
  1. **On every connect,** it reads in 0.25 s windows and discards everything
     until a window's rate is plausible (≤ 2× wire speed). It then drops the
     rest of the line it cut into, and only then reports `connected`. A
     normal connect costs ~0.25 s of data. There is no time limit; while a
     flood lasts, `state` is `settling`.
  2. **While connected,** a token bucket refills at 2× wire speed and holds
     1 s of wire data. Ordinary bursts pass, for example lines that queued
     up while the hub was briefly descheduled. If a flood starts mid-session,
     the bucket empties within ~40 ms. The hub then logs a warning, counts a
     `flood_events`, and goes back to `settling` until the input is
     plausible again. At most about one bucket's worth (~11 KB) of
     duplicates can leak before detection.

  Discarded bytes are counted in `stale_bytes_discarded` in `/status`.
- **Writes are atomic.** All writes and DTR/RTS changes hold one lock. The
  lines from one `/command` request, and each line from a TCP client, reach
  the ESP32 whole and in order.
- **No dependencies beyond pyserial.** Arch's Python is "externally managed"
  (`pip install` is blocked outside a venv), so the hub uses only the
  standard library and the pyserial already installed.

---

## Security

The command path can make the band calibrate, disconnect, or change its
motor scale, so:

- The hub listens on **127.0.0.1 only** by default. `--host 0.0.0.0` lets
  anyone on your network read the data and send commands, and the hub logs a
  warning when you use it.
- **Web pages can read but not write.** GET endpoints send
  `Access-Control-Allow-Origin: *`, so a local dashboard can use
  `/stream`. Any POST that carries a browser `Origin` header is refused,
  unless that exact origin was allowed with `--allow-origin`. So a random
  website you visit can't fire commands at `127.0.0.1:8765`. That includes a
  DNS-rebinding page, which would otherwise look "same-origin". curl and
  scripts send no `Origin`, so they are unaffected.
- **The raw TCP port won't take HTTP.** A web page could otherwise make the
  browser POST to port 8766 and smuggle a `CAL` line in the request body.
  If a TCP client's first line looks like an HTTP request line, the hub
  hangs up without forwarding anything.
- `--read-only` disables every write path.

---

## Tests

```sh
cd ~/fall2026/hub
python3 test_hub.py -v
```

The 19 tests need **no hardware**. A pseudo-terminal stands in for the
ESP32. They cover:

- fan-out to several TCP and SSE clients (byte-exact), and JSON parsing
  (including `nan` → `null`)
- history and `since`, `last_by_tag`
- a slow client dropping only its own lines
- commands over HTTP (text, JSON, `expect`) and over TCP
- `FMT` blocking, cross-origin and DNS-rebinding refusal, and HTTP on the raw port
- pause/resume actually releasing the port, and exclusivity while connected
- `--read-only`, and a missing device
- a flood arriving while connecting being discarded, with only the
  later live lines published
- a flood starting mid-session being cut off (only a bounded amount leaks),
  and an ordinary 8 KB burst *not* being mistaken for a flood

Checked by hand against the real board (2026-10-10):

- Two raw TCP clients and one SSE client received identical lines at the
  same time.
- `POST /command?expect=ACK,ERR` with `CAL` returned `ERR,band not seen yet`
  from the receiver.
- While the hub was connected, `stty -F /dev/ttyUSB0` failed with
  "Device or resource busy".
- `POST /reset` delivered the full boot log (`=== ESP-NOW Receiver ===` …
  `READY`) through the stream.
- `POST /pause` freed the port: `stty` worked, and commands got a 503. After
  `POST /resume`, the hub was back to `connected`.
- Both example clients worked.
- Ctrl-C/SIGINT shut down cleanly.

Not verified on hardware: the flood guard. The flood itself was measured on
this machine; that's where the 350 KB/s and ~6 MB figures come from. But
the one post-idle run with the guard in place happened to have no flood.
The guard's behavior is covered by the pty tests above.

---

## Troubleshooting

**`Permission denied` opening `/dev/ttyUSB0`** (in the hub's `error` field,
or in the Arduino IDE). Serial devices belong to the group `uucp` on Arch
(`dialout` on Debian/Ubuntu/Fedora). Check with `ls -l /dev/ttyUSB0` and
`id`. To fix: `sudo usermod -aG uucp $USER`, then **log out and back in**.
Programs started before the change, including an already-open Arduino IDE,
keep the old groups. On this machine `anon` is already in `uucp`, and the
port opens fine.

**`Device or resource busy` / "port is busy"**. Another program holds the
port, and while the hub is connected, that's the hub, by design. Use
`POST /pause` (see above) or stop the hub. If the *hub* reports busy, find
the other program with `fuser -v /dev/ttyUSB0`: an IDE serial monitor,
`screen`, `minicom`, or a second hub.

**`state: waiting`, "several USB serial devices found"**. More than one
ESP32-type USB chip is plugged in, for example the sender is on USB too.
Pass `--port /dev/serial/by-id/...` for the receiver.
`ls -l /dev/serial/by-id/` shows which is which.

**`state: waiting`, "no USB serial device … found"**. Check the cable (some
are power-only), then `lsusb`, then `journalctl -k | grep -i tty`.

**Only `STAT,0,0,0` lines, never `D,…`**. The receiver works but hears
nothing from the band. See the checklist in the
[top-level README](../README.md#known-issues-as-of-2026-10-10): the
sender's target MAC (`a0:b7:65:0f:4d:1c` or broadcast), channel 1, packet
size 33 bytes, and whether it sends first.

**`state: settling` for more than ~20 s**. The hub is still receiving data
far faster than 115200 baud allows, and is throwing it away; see
[How it works](#how-it-works). If it doesn't end on its own, unplug and
replug the board. Also check that `--baud` matches `Serial.begin()` in the
sketch.

**Lines look like `Output: readable` or `[  12.34s] Accel(g): …`**. Someone
sent `FMT,HUMAN`. Run `curl -X POST http://127.0.0.1:8765/reset`; the
receiver boots in CSV mode.

**`brltty` (mostly Ubuntu)**. The braille-display daemon claims some
CH340/CP210x adapters, and the port vanishes right after you plug the board
in. Uninstall it or mask its udev rule. It's installed on this machine but
not active and not claiming the CP2102, so this doesn't apply here.

**The Arduino IDE can't compile or upload for ESP32**. On this machine the
ESP32 board package is half-installed: `~/.arduino15/packages/esp32/` has
`tools/` but no `hardware/`. Reinstall **esp32 by Espressif Systems** in
Boards Manager. Remember to `/pause` the hub before uploading.
