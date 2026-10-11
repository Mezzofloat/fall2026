# BREATHE.EXE

Read AGENTS.md and docs/INTEGRATIONS.md before working here. Layout: `game/` (Unity), `services/` (bridge + Presage helper), `art/` (Blender sources), `docs/`, `scripts/`.

Current state (iteration 2): first-person 2.5D horror interview game, **3D-only screens** (lobby, interview room, 3x3-room office with doors; text on depth-tested world-space canvases; Buckshot-style dither/CRT pass; sleek safe look vs weathered horror look), live webcam pulse (Presage) + two ESP32 gloves (joystick moves/looks, clicks = E/V, IMU fidgeting) fused into one stress score, ElevenLabs speech with angry tones, free chat with Mr. Vale through bounded Gemini, eyelid blinks between safe and horror rounds, jumpscare, normalized music/SFX. Run the game, camera, mic and hub on the Mac only; edit on Linux and `scripts/sync-mac.sh`. Art: Blender kits `art/office-kit`, `art/office-props` (approved; never rebuild an approved .blend, export with `scripts/export_unity.py`). Audio: `scripts/process_audio.py`.

Verify with: `python3 -m unittest discover -s services/bridge/tests -t services/bridge`, Unity EditMode + PlayMode tests (`-testCategory "!Live"`), and `-testCategory Live` with the bridge running.
