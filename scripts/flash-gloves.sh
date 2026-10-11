#!/usr/bin/env bash
# Build and flash the glove firmware from this Linux laptop (arduino-cli from the Arduino IDE, esp32 core 3.x).
#   scripts/flash-gloves.sh right [/dev/ttyACM0]   wristband for the RIGHT glove (HAND_ID 1), plugged into this laptop
#   scripts/flash-gloves.sh left  [/dev/ttyACM0]   wristband for the LEFT glove (HAND_ID 0)
#   scripts/flash-gloves.sh receiver               receiver ESP32, flashed on the Mac over SSH (stops the hub first, restart it after)
# Once flashed, a glove only needs power: it boots, calibrates (keep it still and flat for ~1 s), connects to the receiver and streams over ESP-NOW.
set -euo pipefail
CLI=${ARDUINO_CLI:-/opt/arduino-ide/resources/app/lib/backend/resources/arduino-cli}
ROOT=$(cd "$(dirname "$0")/.." && pwd); W=$(mktemp -d)
what=${1:?right|left|receiver}; port=${2:-/dev/ttyACM0}
case "$what" in
  right|left)
    hand=1; [ "$what" = left ] && hand=0
    mkdir -p "$W/wristband"; cp "$ROOT/ESP Controller Code" "$W/wristband/wristband.ino"
    $CLI compile --fqbn esp32:esp32:esp32c3:CDCOnBoot=cdc --build-property "compiler.cpp.extra_flags=-DHAND_ID=$hand" "$W/wristband"
    $CLI upload -p "$port" --fqbn esp32:esp32:esp32c3:CDCOnBoot=cdc "$W/wristband" ;;
  receiver)
    mkdir -p "$W/receiver"; cp "$ROOT/ESP Reciever Code" "$W/receiver/receiver.ino"
    $CLI compile --fqbn esp32:esp32:esp32 --output-dir "$W/out" "$W/receiver"
    scp -i "$HOME/.ssh/id_ed25519_amarel" -o IdentitiesOnly=yes "$W/out/receiver.ino.merged.bin" "${BREATHE_MAC:-awsomebroshanks@10.75.97.73}:/tmp/receiver.merged.bin"
    "$ROOT/scripts/mac-ssh.sh" 'pkill -f esp_hub.py; sleep 1; ~/Documents/Codex/breathe-runtime/venv/bin/pip install -q esptool; ~/Documents/Codex/breathe-runtime/venv/bin/esptool.py --chip esp32 --port /dev/cu.usbserial-0001 --baud 460800 write_flash 0x0 /tmp/receiver.merged.bin' ;;
esac
