"""End to end without hardware: a pty plays the receiver ESP32, the real hub reads it, the bridge's IMU reader consumes the hub's TCP stream."""
import os
import socket
import subprocess
import sys
import time
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import imu  # noqa: E402

HUB = Path(__file__).resolve().parents[3] / "hub" / "esp_hub.py"


def free_port():
    s = socket.socket(); s.bind(("127.0.0.1", 0)); p = s.getsockname()[1]; s.close(); return p


class HubToBridge(unittest.TestCase):
    @unittest.skipUnless(HUB.exists(), "hub not present")
    def test_pty_receiver_to_hub_to_bridge_for_all_three_firmware_formats(self):
        try:
            import serial  # noqa: F401
        except ImportError:
            self.skipTest("pyserial not installed")
        master, slave = os.openpty()
        tcp, http = free_port(), free_port()
        proc = subprocess.Popen([sys.executable, str(HUB), "--port", os.ttyname(slave), "--tcp-port", str(tcp), "--http-port", str(http)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        state = imu.ImuState()
        reader = imu.HubReader(state, f"127.0.0.1:{tcp}")
        try:
            reader.start()
            deadline = time.time() + 10
            while time.time() < deadline and not reader.connected:
                time.sleep(.1)
            self.assertTrue(reader.connected, "the bridge reader must connect to the hub")
            lines = [
                "D,100,0.01,-0.02,0.99,1.0,2.0,3.0,0.00,10,20,2048,2048,0.050,0.040,0,1",                                                         # click firmware, left glove, clicked
                "D,110,0.01,-0.02,0.99,1.0,2.0,3.0,0.00,-30,-40,2048,2048,0.050,0.040,1,0",                                                       # right glove
                "A[g]   0.010  -0.020   0.990 | G[dps]    1.00     2.00     3.00 | Act 0.10/0.20 | Joy X:2048 (  12%) Y:2000 ( -30%) | CONNECTED",   # readable, no hand -> default hand (right)
                "D,120,0.01,-0.02,0.99,1.0,2.0,3.0,0.00",                                                                                         # oldest firmware
                "garbage ,,, line",
            ]
            for _ in range(3):
                for line in lines:
                    os.write(master, (line + "\r\n").encode())
                time.sleep(.2)
            deadline = time.time() + 6
            while time.time() < deadline and not (state.gloves[0].packets and state.gloves[1].packets):
                time.sleep(.1)
            snap = state.snapshot()
            self.assertTrue(snap["left"]["connected"] and snap["right"]["connected"], snap)
            self.assertEqual(snap["left"]["jx"], 10)
            self.assertTrue(snap["left"]["click"])
        finally:
            reader.stop(); proc.terminate(); proc.wait(5)
            os.close(master); os.close(slave)


if __name__ == "__main__":
    unittest.main()
