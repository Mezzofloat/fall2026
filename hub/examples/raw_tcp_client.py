#!/usr/bin/env python3
"""
Minimal raw-TCP client for esp_hub: prints IMU samples and optionally
records them to CSV. Uses only the standard library.

    python3 raw_tcp_client.py                  # print samples
    python3 raw_tcp_client.py --csv run1.csv   # also record them
    python3 raw_tcp_client.py --send CAL       # send a command first

The raw port carries exactly what the ESP32 prints, one line per line, so
this is also the template for a client in any other language: open a TCP
socket, read lines, split on commas.
"""

import argparse
import csv
import socket
import sys

FIELDS = ["t_ms", "ax", "ay", "az", "gx", "gy", "gz", "batt"]


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    p.add_argument("--host", default="127.0.0.1")
    p.add_argument("--port", type=int, default=8766)
    p.add_argument("--csv", help="append D samples to this CSV file")
    p.add_argument("--send", action="append", default=[], metavar="CMD",
                   help="command to send on connect (CAL, CONN, DISC, SCALE,0.5); repeatable")
    args = p.parse_args()

    sock = socket.create_connection((args.host, args.port))
    for cmd in args.send:
        sock.sendall(cmd.encode() + b"\n")

    writer = None
    if args.csv:
        f = open(args.csv, "a", newline="")
        writer = csv.writer(f)
        if f.tell() == 0:
            writer.writerow(FIELDS)

    try:
        for raw in sock.makefile("rb"):
            line = raw.decode("utf-8", "replace").strip()
            tag, _, rest = line.partition(",")
            if tag == "D":
                values = rest.split(",")
                if len(values) != len(FIELDS):
                    continue  # corrupted line; skip it
                sample = dict(zip(FIELDS, values))
                print("t={t_ms:>8}ms  accel=({ax},{ay},{az}) g  gyro=({gx},{gy},{gz}) dps  "
                      "batt={batt}V".format(**sample), flush=True)
                if writer:
                    writer.writerow(values)
            else:
                print(line, file=sys.stderr, flush=True)  # STAT / EVT / ACK / ERR / boot messages
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
