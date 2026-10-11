# BREATHE.EXE project instructions

## Current scope

(2026-10-11) The game is **3D-only**: never add a flat full-screen 2D screen. No gentle mode. Fear, jumpscare, loud stinger and red pulsing are intentional. Project layout is `game/`, `services/`, `art/`, `docs/`; see README.md.


The human has authorized Presage C++ SDK webcam pulse readings, ElevenLabs speech recognition and speech output, Gemini dialogue, separate diagnostic windows, and adversarial testing. This supersedes earlier deferrals for these integrations. Preserve the first-person 2.5D Unity game, fictional resumes, local story rules, and keyboard fallback. Finished art and other peripherals remain deferred.

## Project foundation

- Unity 6 LTS, C#, Universal Render Pipeline, Metal. Target demo computer: M3 Pro Mac.
- Read ProjectSettings/ProjectVersion.txt and Packages/manifest.json before changing dependencies. Reuse the pinned editor and package versions; do not upgrade automatically.
- Assets, Packages, ProjectSettings, and their required .meta files are source. Library, Temp, Logs, UserSettings, and Builds are generated locally.
- First-party assets live under Assets/_Breathe. Preserve imported third-party packages and their license information under Assets/ThirdParty or their vendor's required location.
- Preserve .meta GUIDs. Prefer Unity's asset tools for moving/renaming assets. Do not regenerate GUIDs, hand-author scene/prefab YAML, or fabricate project settings.
- Use docs/SETUP.md as the startup checklist and docs/ASSETS.md for intake conventions.
- Do not copy source downloads from AssetInbox into Assets wholesale. Import only reviewed assets, with known licensing and pipeline compatibility.
- Git LFS is for binary media, not .meta files or Unity YAML. Preserve existing repository rules. Do not rewrite history.
- Never commit API keys, recordings, transcripts, local hardware identifiers, or private player data. Provider keys stay in a private local configuration outside Unity. The user has authorized live service integration tests.
- The requested pulse source is Presage SmartSpectra C++ with a selected USB webcam. Discover devices explicitly. Follow the installed SDK's Mac signing requirements; never represent a mocked reading as live.
- AirPods are playback only. Select the Mac or USB microphone separately and show recording state and consent.

## Setup validation

The game must open with the pinned Unity version, render its first-person office and interview screens, pass meaningful progression and control tests, and create a Mac build containing native Apple Silicon support. Keep any remote fresh-clone/LFS round trip distinct from local import tests. Do not claim editor, build, permissions, or hardware checks passed unless they were performed. Keep remaining user actions explicit.

## Implementation boundary

Keep story progression and immediate-stop local. Pulse change from a baseline is a game control, not a psychological or clinical stress measurement. Reject stale/unstable readings and show unavailable states. Model responses must be bounded, schema-validated, and unable to skip local state guards. Test provider failures, prompt injection attempts, malformed payloads, progression, navigation, retries, and the Mac build. Document live checks separately from mocks.
