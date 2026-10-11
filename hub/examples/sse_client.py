#!/usr/bin/env python3
"""
Server-Sent Events client for esp_hub, standard library only.

    python3 sse_client.py               # live lines as JSON
    python3 sse_client.py --replay 50   # start with the last 50 lines

Each event's data is a JSON object, for example:

    {"seq": 812, "ts": 1791670243.48, "line": "D,1234,0.0100,...",
     "tag": "D", "data": {"t_ms": 1234, "ax": 0.01, ...}}

"event: status" messages report the serial connection going up or down.
"""

import argparse
import json
import urllib.request


def events(url):
    """Yield (event_name, data) pairs from an SSE stream."""
    with urllib.request.urlopen(url) as resp:
        name, data = "message", []
        for raw in resp:
            line = raw.decode().rstrip("\n")
            if not line:                      # a blank line ends one event
                if data:
                    yield name, "\n".join(data)
                name, data = "message", []
            elif line.startswith("event:"):
                name = line[6:].strip()
            elif line.startswith("data:"):
                data.append(line[5:].lstrip())
            # "id:", "retry:" and ": comments" are not needed here


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    p.add_argument("--url", default="http://127.0.0.1:8765/stream")
    p.add_argument("--replay", type=int, default=0)
    args = p.parse_args()

    url = args.url + ("?replay=%d" % args.replay if args.replay else "")
    try:
        for name, data in events(url):
            obj = json.loads(data)
            if name == "status":
                print("[serial %s] %s" % (obj["state"], obj.get("error") or obj.get("device")),
                      flush=True)
            elif obj.get("tag") == "D" and "data" in obj:
                d = obj["data"]
                print("#%d accel=(%.3f, %.3f, %.3f) g" % (obj["seq"], d["ax"], d["ay"], d["az"]),
                      flush=True)
            else:
                print("#%d %s" % (obj["seq"], obj["line"]), flush=True)
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
