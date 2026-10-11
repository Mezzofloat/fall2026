# Live integrations: Presage, ElevenLabs, Gemini

Everything runs on the Mac. Provider keys live in `~/Documents/Codex/breathe-runtime/.env` (mode 600, **outside** the repo; see `.env.example`). Keys never enter Unity.

```
Unity game  <--HTTP loopback + token-->  services/bridge (python)  --->  ElevenLabs STT/TTS, Gemini
                                              |
                                              +--> services/presage (signed C++ helper) --> webcam --> Presage
```

## One-time setup
1. **Xcode** installed and an Apple ID signed in (a free account works for local use). Create an *Apple Development* certificate (Xcode > Settings > Accounts > Manage Certificates).
2. If `security find-identity -v -p codesigning` says 0 valid identities, install Apple's **WWDR G3** intermediate certificate (https://www.apple.com/certificateauthority/AppleWWDRCAG3.cer) into your login keychain. Many Macs only have the expired 2023 one.
3. Provisioning profile for the helper (bundle id `com.breatheexe.presage`): run `services/presage/make-profile.command` from Terminal.app on the Mac.
4. Build and sign the helper: `services/presage/sign_presage.command` (Terminal.app, GUI session). It prints the camera list as a check.
5. Keys: copy `.env.example` to `~/Documents/Codex/breathe-runtime/.env`, fill them in, `chmod 600`.

## Every session
- Start the bridge in Terminal.app: `scripts/mac/start-bridge.command`. The signing key and camera permission only exist in the GUI session, so do not start it over SSH.
- Optional `~/Documents/Codex/breathe-runtime/camera.env` sets `PRESAGE_WIDTH/HEIGHT/FPS` (default 1280x720@30).

## Debug windows
`http://127.0.0.1:8765/presage` (camera list, live bpm/confidence/baseline/fps/validation hint) and `/voice` (mic list, record + transcribe, Gemini reply tester with a hostile-prompt button, ElevenLabs voice test). Opened from the clipboard's **Test windows**.

## Model and voice defaults
`GEMINI_MODEL=gemini-3.5-flash-lite` (~0.75 s), ElevenLabs `scribe_v2` and `eleven_flash_v2_5`. `gemini-2.5-flash` returned 404 on the key used so far.

## Tests
- `services/bridge/tests`: `python3 -m unittest discover -s tests -t .` (66 adversarial tests, no network).
- Unity: `-runTests` EditMode (26) and PlayMode (7); `-testCategory Live` needs the bridge running.
- Live provider smoke test: `services/bridge/live_smoke.py --env-file <.env>`.

## Known limits
- Heart rate from a webcam is noisy; a reading arrives every 2-6 s. The baseline uses an interquartile-range steadiness check.
- Fear is a game meter, not a measurement of stress.


## Iteration 2 additions
- **Bridge `/dialogue`** takes `mood` (`safe|horror`) and returns `intent, reply, tone, verdict, anger`. Intents now include `chat` in every talkable act. Replies are validated strictly (closed tone/verdict enums; no brackets so audio tags cannot be smuggled in). Provider failures and SAFETY blocks fall back to scripted sarcastic lines.
- **Bridge `/tts`** takes `tone`; the model defaults to `eleven_v4_turbo` (`ELEVENLABS_TTS_MODEL` to change). Tags like `[shouting angrily]` are added server-side from the closed tone list, and only for v3/v4 models.
- **Bridge `/imu/calibrate`**, `/status.imu` (per-glove sticks, click, fidget) from `IMU_HUB` (loopback only; `off` disables), `IMU_DEFAULT_HAND` for firmware without a glove id.
- **Hub** `scripts/mac/start-hub.command`; see [HARDWARE.md](HARDWARE.md).
- **Audio** `scripts/process_audio.py` (ffmpeg + numpy) normalizes the repo-root clips into `game/Assets/_Breathe/Audio` (music -25/-26 LUFS, jumpscare -12, SFX -17..-26) and cross-fades loop seams.
