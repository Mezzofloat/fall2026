"""Glove input: every firmware layout the receiver has printed, stale gloves, hostile lines, and a fake hub over TCP."""
import socket
import sys
import threading
import time
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import imu  # noqa: E402


class Clock:
    def __init__(self):
        self.t = 100.0

    def __call__(self):
        return self.t


def csv17(hand=0, btn=0, jx=0, jy=0, act_a=.01, act_g=.01, extra=""):
    return f"D,1000,0.0100,-0.0200,0.9900,1.000,2.000,3.000,0.00,{jx},{jy},2048,2048,{act_a},{act_g},{hand},{btn}{extra}"


class Parsing(unittest.TestCase):
    def setUp(self):
        self.clock = Clock()
        self.s = imu.ImuState(self.clock)

    def test_two_glove_csv_with_click(self):
        self.assertTrue(self.s.ingest_line(csv17(hand=0, btn=1, jx=-40, jy=90)))
        self.assertTrue(self.s.ingest_line(csv17(hand=1, btn=0, jx=10, jy=-5)))
        snap = self.s.snapshot()
        self.assertEqual((snap["left"]["jx"], snap["left"]["jy"], snap["left"]["click"]), (-40, 90, True))
        self.assertEqual((snap["right"]["click"], snap["firmware"]), (False, "csv16"))

    def test_fourteen_field_csv_uses_default_hand(self):
        s = imu.ImuState(self.clock, default_hand=0)
        self.assertTrue(s.ingest_line("D,1,0,0,1,0,0,0,0,20,30,2048,2048,.1,.2"))
        self.assertTrue(s.snapshot()["left"]["connected"])
        self.assertFalse(s.snapshot()["right"]["connected"])

    def test_old_eight_field_csv_derives_activity(self):
        self.assertTrue(self.s.ingest_line("D,1,0.6,0,1.0,200,0,0,3.9"))
        self.assertGreater(self.s.snapshot()["right"]["activity"], .5)

    def test_readable_line_with_hand_and_click(self):
        line = "A[g]   0.010  -0.020   0.990 | G[dps]    1.00     2.00     3.00 | Act 0.10/0.20 | Joy X:2048 (  12%) Y:2000 ( -30%) Click:1 | CONNECTED L"
        self.assertTrue(self.s.ingest_line(line))
        left = self.s.snapshot()["left"]
        self.assertEqual((left["jx"], left["jy"], left["click"]), (12, -30, True))

    def test_readable_without_click_field(self):
        line = "A[g] 0.010 -0.020 0.990 | G[dps] 1.00 2.00 3.00 | Act 0.10/0.20 | Joy X:2048 ( 0%) Y:2048 ( 0%) | CONNECTED"
        self.assertTrue(self.s.ingest_line(line))

    def test_disconnected_readable_line_is_ignored(self):
        line = "A[g] 0.0 0.0 1.0 | G[dps] 0.0 0.0 0.0 | Act 0.00/0.00 | Joy X:2048 ( 0%) Y:2048 ( 0%) | DISCONNECTED"
        self.assertFalse(self.s.ingest_line(line))

    def test_hostile_lines_never_crash_or_register(self):
        bad = ["", "D,", "D,1,2,3", "D," + ",".join(["nan"] * 17), "D," + ",".join(["inf"] * 15), csv17(hand=2), csv17(btn=7), csv17(hand="x"),
               "D,1,0,0,1,0,0,0,0,10,10,1,1,.1,.2,0,1,9", "x" * 1000, None, 5, "A[g] garbage", "\x00D,1", csv17(extra="\x00")]
        for line in bad:
            self.assertFalse(self.s.ingest_line(line), repr(line)[:60])
        self.assertFalse(self.s.snapshot()["connected"])

    def test_values_are_clamped(self):
        self.s.ingest_line(csv17(jx=900, jy=-900, act_a=50, act_g=-3))
        right = self.s.snapshot()["left"]
        self.assertEqual((right["jx"], right["jy"]), (100, -100))
        self.assertLessEqual(right["activity"], 1)


class Staleness(unittest.TestCase):
    def test_glove_that_goes_quiet_releases_everything(self):
        clock = Clock()
        s = imu.ImuState(clock)
        s.ingest_line(csv17(hand=1, btn=1, jx=100, jy=100))
        self.assertTrue(s.snapshot()["right"]["click"])
        clock.t += 2.0
        snap = s.snapshot()
        self.assertEqual((snap["right"]["connected"], snap["right"]["click"], snap["right"]["jx"], snap["right"]["jy"]), (False, False, 0, 0))
        self.assertFalse(snap["connected"])


class Fidget(unittest.TestCase):
    def feed(self, s, clock, seconds, hand=1, act=.0, jx=0, jy=0):
        for _ in range(int(seconds * 10)):
            clock.t += .1
            s.ingest_line(csv17(hand=hand, act_a=act, act_g=act, jx=jx, jy=jy))

    def test_fidgeting_raises_score_and_calm_lowers_it(self):
        clock = Clock()
        s = imu.ImuState(clock)
        self.feed(s, clock, 10, act=.0)
        calm = s.snapshot()["fidget"]
        self.feed(s, clock, 20, act=.8)
        busy = s.snapshot()["fidget"]
        self.feed(s, clock, 40, act=.0)
        calm_again = s.snapshot()["fidget"]
        self.assertLess(calm, .1)
        self.assertGreater(busy, .7)
        self.assertLess(calm_again, .1)

    def test_driving_the_stick_does_not_count_as_fidgeting(self):
        clock = Clock()
        s = imu.ImuState(clock)
        self.feed(s, clock, 30, act=.9, jx=80, jy=80)
        self.assertLess(s.snapshot()["fidget"], .05)

    def test_calibration_sets_rest_baseline(self):
        clock = Clock()
        s = imu.ImuState(clock)
        s.calibrate(5)
        self.feed(s, clock, 12, act=.3)
        self.assertGreater(s.snapshot()["baseline"], .1)
        self.feed(s, clock, 30, act=.3)
        self.assertLess(s.snapshot()["fidget"], .15)   # the player's normal restlessness is not "stress"


class FakeHub(unittest.TestCase):
    def test_reader_gets_lines_from_hub_and_reconnects(self):
        server = socket.socket()
        server.bind(("127.0.0.1", 0))
        server.listen(2)
        port = server.getsockname()[1]
        state = imu.ImuState()
        reader = imu.HubReader(state, f"127.0.0.1:{port}")

        def serve():
            for n in range(2):
                conn, _ = server.accept()
                conn.sendall((csv17(hand=n, btn=1) + "\n").encode())
                time.sleep(.3)
                conn.close()
        threading.Thread(target=serve, daemon=True).start()
        reader.start()
        deadline = time.time() + 8
        while time.time() < deadline and state.gloves[0].packets + state.gloves[1].packets < 2:
            time.sleep(.1)
        reader.stop()
        server.close()
        self.assertGreaterEqual(state.gloves[0].packets, 1)
        self.assertGreaterEqual(state.gloves[1].packets, 1)

    def test_non_loopback_hub_is_refused(self):
        for address in ("10.0.0.5:8766", "example.com:8766", "127.0.0.1:0", "127.0.0.1:99999", "127.0.0.1:abc", ":8766"):
            with self.assertRaises(ValueError, msg=address):
                imu.HubReader(imu.ImuState(), address)

    def test_hub_off_means_no_reader(self):
        import os
        os.environ["IMU_HUB"] = "off"
        try:
            self.assertIsNone(imu.from_env()[1])
        finally:
            del os.environ["IMU_HUB"]


if __name__ == "__main__":
    unittest.main()
