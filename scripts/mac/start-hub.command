#!/bin/bash
# Start the ESP32 serial hub (glove IMU + joystick) on the Mac. The receiver ESP32 must be plugged into the Mac. Leave this window open.
# The hub is the single owner of the serial port; the BREATHE bridge reads it on 127.0.0.1:8766 (raw lines).
cd "$(dirname "$0")/../../hub" || exit 1
PY="$HOME/Documents/Codex/breathe-runtime/venv/bin/python"
[ -x "$PY" ] || { echo "Missing venv: run  /opt/homebrew/bin/python3 -m venv ~/Documents/Codex/breathe-runtime/venv && ~/Documents/Codex/breathe-runtime/venv/bin/pip install pyserial"; read -r; exit 1; }
PORT=${ESP_PORT:-auto}
echo "ESP hub starting on port: $PORT   (HTTP 127.0.0.1:8765 is used by the bridge, so the hub uses 8775/8766)"
exec "$PY" esp_hub.py --port "$PORT" --http-port 8775 --tcp-port 8766 --allow-fmt
