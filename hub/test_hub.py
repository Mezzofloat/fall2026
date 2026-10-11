#!/usr/bin/env python3
"""
Tests for esp_hub.py. No hardware needed: a pseudo-terminal (pty) stands in
for the ESP32. The test writes "ESP32 output" into the pty master and reads
back whatever the hub sends to the "ESP32".

    python3 test_hub.py -v
"""

import json
import os
import select
import socket
import threading
import time
import unittest
import urllib.error
import urllib.request

import esp_hub


def wait_for(predicate, timeout=3.0, what="condition"):
    deadline = time.time() + timeout
    while time.time() < deadline:
        if predicate():
            return
        time.sleep(0.02)
    raise AssertionError("timed out waiting for " + what)


class FakeEsp32:
    """The far end of a pty. The hub opens slave_path as its 'serial port'."""

    def __init__(self):
        self.master, slave = os.openpty()
        self.slave_path = os.ttyname(slave)
        os.close(slave)

    def emit(self, data):
        os.write(self.master, data)

    def read_written(self, expected, timeout=3.0):
        """Read what the hub wrote to the ESP32 until `expected` shows up."""
        got = b""
        deadline = time.time() + timeout
        while expected not in got and time.time() < deadline:
            r, _, _ = select.select([self.master], [], [], 0.05)
            if r:
                got += os.read(self.master, 4096)
        return got

    def nothing_written(self, wait=0.4):
        r, _, _ = select.select([self.master], [], [], wait)
        return not r

    def close(self):
        os.close(self.master)


class HubTestCase(unittest.TestCase):
    hub_kwargs = {}

    def setUp(self):
        self.esp = FakeEsp32()
        self.hub = esp_hub.Hub(port=self.esp.slave_path, **self.hub_kwargs)
        self.http, self.tcp = esp_hub.start_servers(self.hub, "127.0.0.1", 0, 0,
                                                    queue_size=100)
        self.base = "http://127.0.0.1:%d" % self.http.server_address[1]
        self.tcp_addr = ("127.0.0.1", self.tcp.server_address[1])
        wait_for(lambda: self.hub.state == "connected", what="hub to open the pty")

    def tearDown(self):
        self.hub.stop()
        self.http.shutdown()
        self.tcp.shutdown()
        self.http.server_close()
        self.tcp.server_close()
        wait_for(lambda: self.hub.state == "stopped", what="hub to stop")
        self.esp.close()

    # --- helpers

    def get(self, path):
        with urllib.request.urlopen(self.base + path, timeout=3) as r:
            return json.load(r)

    def post(self, path, body=b"", headers=None):
        req = urllib.request.Request(self.base + path, data=body, method="POST",
                                     headers=headers or {})
        try:
            with urllib.request.urlopen(req, timeout=5) as r:
                return r.status, json.load(r)
        except urllib.error.HTTPError as e:
            with e:
                return e.code, json.load(e)

    def tcp_client(self):
        s = socket.create_connection(self.tcp_addr, timeout=3)
        self.addCleanup(s.close)
        return s

    def wait_clients(self, n):
        wait_for(lambda: len(self.hub.status()["clients"]) >= n, what="%d clients" % n)

    @staticmethod
    def recv_until(sock, expected):
        got = b""
        while expected not in got:
            chunk = sock.recv(4096)
            if not chunk:
                break
            got += chunk
        return got


class FanOutTests(HubTestCase):

    def test_every_tcp_client_gets_exact_bytes(self):
        a, b = self.tcp_client(), self.tcp_client()
        self.wait_clients(2)
        data = b"D,1234,0.0100,-0.0200,0.9900,1.000,2.000,3.000,3.91\r\nSTAT,7,0,0\n"
        self.esp.emit(data)
        self.assertEqual(self.recv_until(a, b"STAT,7,0,0\n"), data)
        self.assertEqual(self.recv_until(b, b"STAT,7,0,0\n"), data)

    def test_sse_stream_parses_known_lines(self):
        resp = urllib.request.urlopen(self.base + "/stream", timeout=3)
        self.addCleanup(resp.close)
        self.wait_clients(1)
        self.esp.emit(b"D,500,0.1000,0.2000,0.3000,4.000,5.000,6.000,nan\n")
        events = []
        while len(events) < 2:  # initial status event, then the line
            line = resp.readline().decode()
            if line.startswith("data: "):
                events.append(json.loads(line[6:]))
        status, line = events
        self.assertEqual(status["state"], "connected")
        self.assertEqual(line["tag"], "D")
        self.assertEqual(line["data"]["t_ms"], 500)
        self.assertAlmostEqual(line["data"]["gz"], 6.0)
        self.assertIsNone(line["data"]["batt"])  # nan becomes null, not invalid JSON

    def test_latest_and_since(self):
        self.esp.emit(b"STAT,1,0,0\nSTAT,2,0,0\nhello\n")
        wait_for(lambda: self.hub.seq >= 3, what="3 lines")
        lines = self.get("/latest?n=10")["lines"]
        self.assertEqual([l["line"] for l in lines], ["STAT,1,0,0", "STAT,2,0,0", "hello"])
        self.assertNotIn("tag", lines[2])
        after = self.get("/latest?since=%d" % lines[0]["seq"])["lines"]
        self.assertEqual(len(after), 2)

    def test_status_tracks_last_line_per_tag(self):
        self.esp.emit(b"STAT,9,1,2\nEVT,CONNECTED\n")
        wait_for(lambda: self.hub.seq >= 2, what="2 lines")
        status = self.get("/status")
        self.assertEqual(status["last_by_tag"], {"STAT": "STAT,9,1,2", "EVT": "EVT,CONNECTED"})

    def test_slow_client_drops_only_its_own_lines(self):
        slow = self.hub.subscribe("test", "slow", maxsize=5)
        fast = self.tcp_client()
        self.wait_clients(2)
        payload = b"".join(b"STAT,%d,0,0\n" % i for i in range(50))
        self.esp.emit(payload)
        self.assertEqual(self.recv_until(fast, b"STAT,49,0,0\n"), payload)
        self.assertEqual(slow.dropped, 45)
        kept = [slow.queue.get_nowait()[1].raw for _ in range(5)]
        self.assertEqual(kept[-1], b"STAT,49,0,0\n")


class CommandTests(HubTestCase):

    def test_http_command_is_written_with_newline(self):
        code, body = self.post("/command", b"cal")
        self.assertEqual(code, 200)
        self.assertEqual(body["sent"], ["cal"])
        self.assertIn(b"cal\n", self.esp.read_written(b"cal\n"))

    def test_json_command_and_expect_reply(self):
        def reply():
            self.esp.read_written(b"SCALE,0.5\n")
            self.esp.emit(b"STAT,0,0,0\nACK,3,0.50\n")
        threading.Thread(target=reply, daemon=True).start()
        code, body = self.post("/command?expect=ACK,ERR&timeout=3",
                               json.dumps({"line": "SCALE,0.5"}).encode(),
                               {"Content-Type": "application/json"})
        self.assertEqual(code, 200)
        self.assertEqual(body["reply"]["line"], "ACK,3,0.50")
        self.assertEqual(body["reply"]["data"], {"cmd": 3, "value": 0.5})

    def test_tcp_client_lines_are_forwarded(self):
        s = self.tcp_client()
        self.wait_clients(1)
        s.sendall(b"CONN\r\nDISC\n")
        got = self.esp.read_written(b"DISC\n")
        self.assertIn(b"CONN\n", got)
        self.assertIn(b"DISC\n", got)

    def test_fmt_is_refused_by_default(self):
        code, body = self.post("/command", b"fmt,human")
        self.assertEqual(code, 403)
        self.assertTrue(self.esp.nothing_written())

    def test_cross_origin_post_is_refused(self):
        code, _ = self.post("/command", b"CAL", {"Origin": "http://evil.example"})
        self.assertEqual(code, 403)
        self.assertTrue(self.esp.nothing_written())

    def test_dns_rebinding_post_is_refused(self):
        # A rebound page's Origin matches the Host header it sends; still no.
        host = self.base.split("//", 1)[1]
        code, _ = self.post("/command", b"CAL", {"Origin": self.base, "Host": host})
        self.assertEqual(code, 403)
        self.assertTrue(self.esp.nothing_written())

    def test_http_request_on_raw_port_is_not_forwarded(self):
        s = self.tcp_client()
        self.wait_clients(1)
        s.sendall(b"POST / HTTP/1.1\r\nHost: x\r\nContent-Length: 4\r\n\r\nCAL\n")
        self.assertTrue(self.esp.nothing_written())
        self.assertEqual(s.recv(100), b"")  # hub hung up

    def test_pause_releases_port_and_resume_reacquires(self):
        code, body = self.post("/pause")
        self.assertEqual(code, 200)
        self.assertEqual(body["status"]["state"], "paused")
        # Port is really free now: another program can open it.
        fd = os.open(self.esp.slave_path, os.O_RDWR | os.O_NOCTTY)
        os.close(fd)
        self.assertEqual(self.post("/command", b"CAL")[0], 503)
        self.post("/resume")
        wait_for(lambda: self.hub.state == "connected", what="reconnect")
        self.assertEqual(self.post("/command", b"CAL")[0], 200)

    def test_port_is_exclusive_while_connected(self):
        with self.assertRaises(OSError):
            os.close(os.open(self.esp.slave_path, os.O_RDWR | os.O_NOCTTY))


class ReadOnlyTests(HubTestCase):
    hub_kwargs = {"read_only": True}

    def test_writes_refused(self):
        self.assertEqual(self.post("/command", b"CAL")[0], 403)
        self.assertEqual(self.post("/reset")[0], 403)
        self.assertTrue(self.esp.nothing_written())


class StaleBacklogTests(unittest.TestCase):
    """Input arriving faster than any serial wire right after connecting is
    an old backlog (seen on the real board: ~1 MB of stale STAT lines after
    the port sat closed for a few minutes) and must not reach clients."""

    def test_backlog_flood_on_connect_is_discarded(self):
        esp = FakeEsp32()
        self.addCleanup(esp.close)
        os.set_blocking(esp.master, False)
        flooding = threading.Event()
        stop_flood = threading.Event()

        def flood():
            while not stop_flood.is_set():
                try:
                    os.write(esp.master, b"OLD,0,0,0\n" * 100)
                    flooding.set()
                except BlockingIOError:
                    time.sleep(0.001)
        threading.Thread(target=flood, daemon=True).start()
        flooding.wait(2)

        hub = esp_hub.Hub(port=esp.slave_path)
        t = threading.Thread(target=hub.run_serial, daemon=True)
        t.start()
        self.addCleanup(lambda: (hub.stop(), t.join(3)))
        time.sleep(1.0)
        self.assertNotEqual(hub.state, "connected")  # still discarding
        stop_flood.set()
        wait_for(lambda: hub.state == "connected", what="connect after flood")
        for i in range(3):
            os.write(esp.master, b"NEW,%d\n" % i)
            time.sleep(0.1)
        wait_for(lambda: hub.seq >= 3, what="live lines")
        published = [line_.raw for line_ in hub.latest(100)]
        self.assertEqual(published, [b"NEW,0\n", b"NEW,1\n", b"NEW,2\n"])
        self.assertGreater(hub.status()["stale_bytes_discarded"], 10000)


class FloodGuardTests(HubTestCase):
    """The same flood can start while connected; the guard must notice it."""

    def flood(self, seconds):
        os.set_blocking(self.esp.master, False)
        deadline = time.time() + seconds
        while time.time() < deadline:
            try:
                os.write(self.esp.master, b"OLD,0,0,0\n" * 100)
            except BlockingIOError:
                time.sleep(0.001)
        os.set_blocking(self.esp.master, True)

    def test_flood_while_connected_is_cut_off(self):
        self.esp.emit(b"LIVE,1\n")
        wait_for(lambda: self.hub.seq >= 1, what="live line")
        self.flood(1.0)
        wait_for(lambda: self.hub.state == "connected", what="recovery")
        self.esp.emit(b"NEW,1\n")
        wait_for(lambda: self.hub.last_by_tag.get("NEW"), what="line after flood")
        status = self.hub.status()
        published = [l.raw for l in self.hub.latest(5000)]
        leaked = published.count(b"OLD,0,0,0\n")
        self.assertEqual(status["flood_events"], 1)
        self.assertEqual(published[0], b"LIVE,1\n")
        self.assertEqual(published[-1], b"NEW,1\n")
        # Only what fit in the token bucket before detection (1 s of wire
        # data, ~1150 lines of this size) may leak; the rest is discarded.
        self.assertLess(leaked, 1300)
        self.assertGreater(status["stale_bytes_discarded"], 100 * leaked * 10)

    def test_normal_burst_is_not_mistaken_for_a_flood(self):
        # 8 KB at once, like a backlog after the hub was descheduled briefly.
        burst = b"".join(b"D,%d,0.1,0.2,0.3,1.0,2.0,3.0,3.9\n" % i for i in range(250))
        self.esp.emit(burst)
        wait_for(lambda: self.hub.seq >= 250, what="burst")
        self.assertEqual(self.hub.status()["flood_events"], 0)


class MissingPortTests(unittest.TestCase):

    def test_waits_and_reports_missing_device(self):
        hub = esp_hub.Hub(port="/dev/does-not-exist")
        t = threading.Thread(target=hub.run_serial, daemon=True)
        t.start()
        try:
            wait_for(lambda: hub.state == "waiting", what="waiting state")
            self.assertIn("does not exist", hub.status()["error"])
            with self.assertRaises(esp_hub.HubError) as cm:
                hub.write_lines([b"CAL"])
            self.assertEqual(cm.exception.status, 503)
        finally:
            hub.stop()
            t.join(3)


if __name__ == "__main__":
    unittest.main()


class ParseLayoutTests(unittest.TestCase):
    """The receiver firmware has printed 8, 15 and 17 field D lines over time; all must parse."""

    def test_old_eight_fields(self):
        d = esp_hub.parse_fields("D,5,0.1,0.2,0.9,1,2,3,3.9")
        self.assertEqual(d["t_ms"], 5)
        self.assertNotIn("jx", d)

    def test_joystick_fourteen_fields(self):
        d = esp_hub.parse_fields("D,5,0.1,0.2,0.9,1,2,3,0,10,-20,2048,2000,0.1,0.2")
        self.assertEqual((d["jx"], d["jy"]), (10, -20))
        self.assertNotIn("hand", d)

    def test_click_and_hand_sixteen_fields(self):
        d = esp_hub.parse_fields("D,5,0.1,0.2,0.9,1,2,3,0,10,-20,2048,2000,0.1,0.2,0,1")
        self.assertEqual((d["hand"], d["btn"]), (0, 1))

    def test_other_counts_rejected(self):
        self.assertIsNone(esp_hub.parse_fields("D,5,0.1"))
        self.assertIsNone(esp_hub.parse_fields("D," + ",".join(["1"] * 12)))
