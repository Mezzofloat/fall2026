# Continue BREATHE.EXE

Work in the existing repository at `/Users/awsomebroshanks/Documents/Codex/fall2026`. Read AGENTS.md, docs/GAME-DESIGN.md, docs/CORE-STATUS.md, and docs/PLAYTEST.md first. The earlier setup-only phase has been superseded by the user's authorization to build the core game. Their latest correction requires first-person 2.5D: a simple 3D office with flat sprite characters, like the visual format of Baldi's Basics.

Use the pinned Unity 6.3 LTS editor, 6000.3.26f1. Preserve generated .meta GUIDs, package versions, existing repository assets, and uncommitted user work. The scene is Assets/_Breathe/Scenes/Main.unity; the runtime entry is BreatheGame. Keep Unity and first-person navigation; do not revert to top-down or introduce detailed 3D character models.

The current offline core includes resume choice, a safe office tour, five interview questions, the pen/knife transformation, an impossible second question with an inevitable negative response, three first-person escape challenges, collision, pathfinding pursuit, simulated pressure, answer rehearsal, checkpoint retries, final availability choices, three endings, and game-only feedback.

The requested visual direction resembles deliberately crude educational-game graphics: primitive 3D rooms, chunky office tiles, flat sprite people, awkward original portraits, a dark red empty office, and a grinning monster. Do not copy another game's characters or branding. Models and final art come after the core; peripherals come last.

First prioritize human feedback on movement, pursuit, pacing, readability, and story. The four resumes are fictional placeholders, explicitly approved by the user. Keep real speech, sensor interpretation, joystick/SNES hardware, haptics, Gemini, and ElevenLabs deferred until that milestone is requested. Do not infer BLE: the hardware team has ESP-NOW receiver work and says its movement devices are redundant, but the Mac interface and signal protocol remain unverified.

Keep Escape pause/stop working in every state. All story progress must work offline. Simulated pressure is a game variable, never a claimed physiological measurement. Do not create interview-quality or hiring assessments from unmeasured speech or biometrics.

Use Unity's EditMode and PlayMode tests before changing story transitions or movement. The PlayMode checks exercise camera-relative keyboard movement, first-person turning, pause/stop, actual pursuer navigation and capture, checkpoint retry, UI buttons, screen text, and narrow/wide rendering. BreatheBuild generates the baseline Main scene through Unity APIs; its Prepare command intentionally replaces that scene, so review it before running after future hand-edited scene changes.

Do not commit or push until requested. Do not overwrite or migrate the team's remote hardware work. Report which checks actually ran and distinguish a playable core from finished art, hardware integration, and a human playtest.
