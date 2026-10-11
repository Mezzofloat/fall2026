# BREATHE.EXE — first-person sprite core

Verified locally on October 10, 2026. This milestone supersedes the blank setup scene and the top-down prototype.

## What is playable

The Unity game now uses a perspective first-person camera inside a simple geometric office, with flat billboard sprite characters and point-filtered textures. It includes four fictional resume choices, a safe office tour, interview close-ups, the pen-to-knife transformation, the impossible second question, three escape questions in the red empty office, pathfinding pursuit, refuge, retries, final availability choices, three endings, and game-action feedback.

WASD walks and strafes. Mouse looks; left/right arrows turn. E interacts, Shift sprints, Space rehearses an answer, and Escape pauses or ends the interview. The cursor releases in menus and on pause. Losing focus pauses the simulation. Pressure is explicitly simulated.

## Verified

- Pinned Unity 6000.3.26f1 imported and compiled the project.
- 9 EditMode tests passed: endings, guarded transitions, retry state, objective gates, pressure bounds, collision, candidate data, and reachability.
- 3 PlayMode tests passed: keyboard movement and turning, perspective camera height, pause and stop, pursuer navigation and capture, retry, story buttons, and text/window framing.
- 22 rendered screen captures cover the story flow and narrow/wide menus. Representative office, red office, and sprite pursuer captures were visually inspected.
- Native Mac development build succeeded. The executable contains both arm64 and x86_64 slices; arm64 supports the target M3 Pro.
- The new native app launched and its full start screen was visually confirmed. The user began interacting with it, so the app was left under user control. A complete human playthrough of this corrected native build is still pending.
- Asset metadata audit found no missing metadata or duplicate GUIDs. Existing scene identity is preserved.
- No Git commit or push was made. The local branch remains one existing remote hardware commit behind origin/main; that work was not overwritten or merged.

## Deliverables

- `BREATHE-FirstPerson.app`: corrected native app. Use this rather than the earlier `BREATHE.EXE.app`, which is retained as the top-down prototype.
- `BREATHE-EXE-unity-project-source.zip`: complete current Unity source and metadata, excluding regenerated caches/builds.
- `BREATHE-EXE-game-design.md`: canonical story and systems.
- `BREATHE-EXE-play-guide.md`: controls and human playtest checklist.
- `BREATHE-EXE-codex-prompt.md`: continuation instructions for coding assistants.
- Office and pursuer PNGs: previews from Unity's rendering checks.

The working repository is `/Users/awsomebroshanks/Documents/Codex/fall2026`. Open `Assets/_Breathe/Scenes/Main.unity` in the pinned editor and press Play. The BREATHE editor menu can regenerate the baseline scene or build the Mac app; regeneration replaces Main, so review before using it after future hand-authored scene work.

## Deferred

This is a playable core with procedural placeholder graphics and synthesized audio, not finished art or a shipped game. Real speech, biometric calibration, joystick/SNES support, haptics, Gemini, ElevenLabs, and cloud services are unimplemented. No microphone or sensor stream is read. No speech or physiological score is claimed. Final assets and peripheral interfaces remain the team's later milestones. The actual device models and Mac-facing protocol are still unknown; do not assume BLE.

Pacing, pursuit fairness, comfort, distinctiveness of each chase, and final audio need human playtesting. Keyboard answer rehearsal is a temporary substitute for the later input system. The first-person map is intentionally simple; detailed props, animations, and final sprite work are still deferred.
