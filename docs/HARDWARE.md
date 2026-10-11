# Hardware preparation

Confirmed: development Mac is M3 Pro. AirPods are intended for immersive playback. The team is working on the hardware.

Unconfirmed: heart-rate sensor make/model/interface; AirPods model; separate microphone; haptic device and transport.

Hardware remains deferred while the first-person sprite core is implemented. The latest team notes say both movement devices are plugged in concurrently for redundancy; this has not been tested by this game. The remote repository contains ESP-NOW receiver work, but its Mac-facing interface and the actual sensor models remain unverified. Do not assume BLE. No drivers, microphone capture, or haptics are implemented in this milestone.

| Device | Exact make/model | Connection/interface | Vendor documentation | Owner | Ready for integration? |
|---|---|---|---|---|---|
| Development Mac | M3 Pro | macOS / Apple Silicon | | | Environment setup in progress |
| Heart-rate sensor | Unknown | Unknown | | | No |
| Playback headphones | AirPods; model unknown | Bluetooth audio | | | Routing test pending |
| Speech microphone | Pending | Internal or USB planned | | | No |
| Optional haptics | Unknown | Unknown | | | No |

When integration is authorized, verify the exact sensor interface before choosing an adapter. Keep AirPods as output and explicitly select a separate input microphone. Record hardware test results without committing private player recordings or local device identifiers.


## Gloves, iteration 2 (two gloves, joystick click, hub)
Each glove is an ESP32-C3 SuperMini wristband (MPU6500 + joystick) sending ESP-NOW packets to one USB receiver ESP32 plugged into the **Mac**.
- **Left glove (`HAND_ID 0`)**: left stick walks, click = **E**. **Right glove (`HAND_ID 1`)**: right stick looks, click = **V** (hold to talk). Fidgeting (wrist motion while the thumb is not on the stick) raises the stress score.
- **New pin**: joystick push-button `SW` to **GPIO5** and GND (internal pull-up, 30 ms debounce). Everything else is unchanged.
- **Packet** (`ESP Controller Code` / `ESP Reciever Code`, both changed): `DataPkt` gained `uint8_t hand; uint8_t btn;` at the end, so **both sketches must be reflashed together**, and each glove needs its own `HAND_ID`. The receiver keeps one peer per glove.
- **Receiver output** (CSV mode, now the default): `D,time_ms,ax,ay,az,gx,gy,gz,batt,jx,jy,jxRaw,jyRaw,actA,actG,hand,btn` (16 fields after the tag). Readable mode also prints `Click:n` and the hand letter. The hub and bridge parse the 8-field, 14-field and 16-field CSV and the readable lines.
- **Hub** (`hub/esp_hub.py`) is the single owner of the serial port. On the Mac: `scripts/mac/start-hub.command` (venv with pyserial under `~/Documents/Codex/breathe-runtime/venv`). The bridge reads its raw TCP stream (`IMU_HUB`, default `127.0.0.1:8766`, loopback only) and exposes `imu` in `/status`.
- The firmware was edited but **not compiled or flashed** here: build it in the Arduino IDE (board ESP32C3 Dev Module, USB CDC On Boot enabled for the wristband).
- If the receiver is on the Linux laptop instead, forward the hub: `ssh -R 8766:127.0.0.1:8766 mac`.

### Flashing and power-only use (verified)
- `scripts/flash-gloves.sh right|left` builds and flashes a wristband from the Linux laptop; `scripts/flash-gloves.sh receiver` flashes the receiver on the Mac over SSH. (Needs the Arduino IDE's arduino-cli and the esp32 core 3.x: `arduino-cli core install esp32:esp32 --additional-urls https://espressif.github.io/arduino-esp32/package_esp32_index.json`.)
- The right glove (`HAND_ID 1`) and the receiver were flashed on 2026-10-11 and the stream was verified end to end: glove -> receiver (Mac) -> hub -> bridge `/status.imu.right` (`firmware: csv16`, hand 1).
- Standalone: the wristband's serial output is now skipped unless a computer is listening and never blocks (`Serial.setTxTimeoutMs(0)`), so it runs identically on a power-only cable/battery. After power-up keep it still and flat for about a second (gyro calibration), then it connects to the receiver by itself.
- Opening the glove's serial port on the laptop reboots it (USB reset); do not leave a serial monitor open while playing.
