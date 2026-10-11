> Historical environment milestone. Core game programming has since been authorized and implemented. See CORE-STATUS.md and GAME-DESIGN.md for the current build.

# Local setup status — October 10, 2026

## Completed and verified

- Repository: https://github.com/Mezzofloat/fall2026.
- Local project: /Users/awsomebroshanks/Documents/Codex/fall2026.
- Mac: M3 Pro / Apple Silicon; macOS 26.6.2.
- Unity Hub 3.22.2 installed. Unity Personal activation verified in Hub.
- Local project registered in Unity Hub as fall2026, with the correct path and editor version 6000.3.26f1 verified in its project list.
- Unity 6.3 LTS, 6000.3.26f1 (de8167e7440c), Apple Silicon editor installed.
- Genuine Universal 3D template used; URP 17.3.0, Input System 1.20.0, Visual Studio Editor package 2.0.26 pinned in generated package files.
- VS Code retained, Microsoft Unity extension 1.3.2 and C# dependencies installed. Unity recognizes VS Code 1.138.0 as its external script editor; local solution/project files generated.
- Shared folders under Assets/_Breathe, with Unity-generated .meta files. Main.unity is the template scene moved with its original GUID and included in the build scene list.
- Product name: BREATHE.EXE. Rendering: Metal. Force Text and Visible Meta Files verified.
- Starter scene entered/exited Play mode; Console showed zero errors and warnings.
- Blank Mac build succeeded at Builds/Mac/BREATHE.EXE.app. It is a universal binary containing native ARM64 and Intel x86_64 executables; Apple Silicon-only architecture was not selected.
- Unity ignores configured; Git LFS 3.7.1 enabled locally. Existing repository music retained without history migration. New imported binary media uses LFS.
- AGENTS.md and CLAUDE.md enforce setup-only scope. Setup, asset intake, hardware inventory, and assistant handoff documentation prepared.
- Original repository files retained. No custom game, service, sensor, or API code added. The official template includes its own Readme helper scripts.

## Final verification status

- Source-only clean import check completed successfully with the pinned editor and no reused project cache.
- Mac player launched successfully; its log confirms the Apple M3 Pro Metal device initialized and assemblies loaded. Its window was visually checked after the Mac was unlocked and rendered the ordinary template skybox correctly. The test app was then closed.
- Unity Hub registration completed and the local project opened from its project list.

Local environment verification is complete. No hardware, microphone, Bluetooth, Gemini, ElevenLabs, haptic, or external-service test has been performed. The sensor model is still unknown.

## Git handoff

Changes are local and uncommitted. Nothing has been pushed to GitHub. A remote fresh-clone/LFS round trip is pending until the setup is committed and published. A source-only clean import is a separate check and must not be described as a remote fresh-clone test.
