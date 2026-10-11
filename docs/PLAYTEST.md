# Playtesting BREATHE.EXE

Run everything **on the Mac** (Linux is only the editor). See [INTEGRATIONS.md](INTEGRATIONS.md) for the one-time setup.

## Start
1. Terminal on the Mac: `~/Documents/Codex/breathe/scripts/mac/start-bridge.command` (leave it open; allow camera/mic prompts once).
2. Gloves: plug the **receiver** ESP32 into the Mac and run `~/Documents/Codex/breathe/scripts/mac/start-hub.command` in a second Terminal. The bridge reads the hub on `127.0.0.1:8766`; the lobby clipboard shows "Gloves: LEFT RIGHT connected". Reflash both sketches first (see [HARDWARE.md](HARDWARE.md)); the packet layout changed.
3. Open the app `game/Builds/Mac/BREATHE.app` (or press Play in Unity on `Assets/_Breathe/Scenes/Main.unity`).
4. In the lobby, click the clipboard **OPEN SETUP**: pick the camera, switch **Pulse** on (this is your camera consent), switch **Voice** on, pick the mic. **Test windows** opens the Presage and Voice diagnostic pages.

## Controls
| Keyboard + mouse | Gloves | Action |
|---|---|---|
| WASD / arrows | **Left stick** (push to the end to sprint) | Walk and strafe |
| Mouse / arrows | **Right stick** | Look around |
| E | **Left-stick click** | Interact: pick up a page, answer a page, take the exit, continue the tour. With gloves, standing at the target for 1 s also interacts. |
| **V (hold)** | **Right-stick click (hold)** | Talk to Mr. Vale. Release to send. Works anywhere he exists (not only at answers). |
| Shift | | Sprint |
| Space (hold 3 s) | | Keyboard answer when voice is off |
| Esc | | Pause or end the interview, always available |
| F1 | | Debug overlay (stress components, hunter door waits, music pitch/duck) |
| [ ] | | Dev stress when there is no live signal (labelled as dev) |

## What is new in iteration 2
- Talk to Mr. Vale about anything: he answers sarcastically or threateningly in his own angry, distorted voice. Chat never moves the story forward. A page answer counts only if it is a real answer; an off-topic one is rejected, makes noise and draws him in.
- Horror only while a question is asked or chased; between rounds the office returns to safe behind a slow eyelid blink. Safe music (the first interview slowly tape-warps) / horror music; the laugh plays only the first time.
- 3x3 rooms with doors. Mr. Vale waits 1 s at a closed door unless your stress score is elevated: then he bursts through.
- Overall stress score = pulse + glove fidgeting + voice loudness (no single sensor decides).
- HUD: question and page tracker (FIND / ANSWER / DONE), mini map top right, recording indicator, plain "how to answer" instructions, Vale's subtitles.

## Things to watch during a playtest
- Does the tour's calm walk give a pulse baseline and a glove rest baseline? (HUD shows "BASELINE READY".) Webcam pulse needs steady light on your face: a C920 in dim light drops to ~8 fps and Presage refuses it.
- Is speaking an answer at a station tense but fair while he searches? Is the 1-2 s chat/TTS latency tolerable?
- Stick directions: if forward walks backward or look is inverted, flip `GloveInput.InvertLeftY/InvertRightY/SwapSticks` (left/right glove IDs are `HAND_ID` in each sketch).
- Do calm/stress effects feel earned? Thresholds live in `FearModel` (`CalmFear`, `PanicFear`, `SpaceOutDelay`, `DoorBurstFear`).
- Jumpscare volume and length (`BreatheGame.ScareLength`; audio `jumpscare.mp3` normalized to -12 LUFS).
- Audio levels: `python3 scripts/process_audio.py` re-normalizes every clip from the repo-root originals (music about 10 dB under speech).
