#!/bin/bash
# Start the BREATHE local bridge (Presage + ElevenLabs + Gemini) in your desktop session. Leave this window open.
cd "$(dirname "$0")/../../services/bridge" || exit 1
ENV="$HOME/Documents/Codex/breathe-runtime/.env"
[ -f "$ENV" ] || { echo "Missing $ENV (copy .env.example and fill in keys)"; read -r; exit 1; }
echo "BREATHE bridge starting. Diagnostics windows: http://127.0.0.1:8765/presage  and  /voice"
[ -f "$HOME/Documents/Codex/breathe-runtime/camera.env" ] && { set -a; . "$HOME/Documents/Codex/breathe-runtime/camera.env"; set +a; }
exec /opt/homebrew/bin/python3 server.py --env-file "$ENV"
