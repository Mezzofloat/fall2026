# Iteration 2 design (current behaviour)
- **Phases**: horror only during Transformation/Unreadable, the three chases, the memory choice and after a catch. ChallengeIntro/TeamIntro/GapIntro, Return and Offer are safe. Safe to horror changes hide behind a quick eyelid slam; horror to safe behind a slow blink. The laugh plays only on the first horror; later rounds go straight into the 96 s horror loop.
- **Building**: 3 x 3 rooms (reception, open plan, break room, archive, atrium, conference, HR, server room, interview suite), 12 doors. Doors open for the player by walking up, close after 3 s. Mr. Vale waits 1 s to open a closed door unless the stress score is elevated (>= `DoorBurstFear`), then he bursts through instantly. Closed doors block sight.
- **Stress score**: `FearModel` blends pulse (60%) and glove fidgeting (40%) when both are live, either alone otherwise, voice loudness adds. It drives space-out blur, slowdown, shake, red pulse and doors.
- **Talking**: V / right-glove click works in every act except menus. Chat never changes the story. At a page station the model also rates the answer (good/weak/offtopic); off-topic does not count, makes noise and angers Vale. Vale's reply carries a tone (calm/sarcastic/menacing/furious) that selects the ElevenLabs delivery (`eleven_v4_turbo` audio tags + stability) and local distortion.

# BREATHE.EXE: game design (current behaviour first)

> **Revision 2026-10-11. Read this section first; it supersedes anything below that contradicts it.**
> Tone: Buckshot Roulette meets Baldi's Basics. There is **no gentle mode** (it was never requested).

## What changed from the offline core

| Area | Now |
|---|---|
| Screens | **Nothing is a flat 2D screen.** The world camera always renders. The title screen is the *Continuity lobby* (reception desk, CRT monitor = BEGIN, clipboard = SETUP, sandwich board = content notice, door = QUIT, frosted window with a figure walking past). Resume selection = four folders on the desk. Interviews = Vale's desk in the interview room. Pause = a sign held in front of the camera over the frozen, dimmed world. Endings = the interview room. All text and buttons are world-space canvases mounted on props. Only the transparent first-person HUD is screen-space. A PlayMode test enforces this for every act. |
| Music | `safe.mp3` plays in calm acts. When horror starts (the pen click, "Transformation"), the **laugh plays instantly**, the screen is **black while it plays**, and `loop` is scheduled on the audio clock to begin the moment the laugh ends. Pausing during the laugh reschedules the loop correctly. The calm epilogue (Offer) returns to `safe.mp3`. |
| Jumpscare | Getting caught fills the screen with Vale's face for ~1.45 s with a harsh stinger, then the retry cards appear. One loud cue, no strobing. |
| Fear (replaces "simulated pressure") | A **game meter** fused from the live webcam pulse (change from the baseline measured during the tour) and, while answering, microphone loudness. Real calm for several seconds makes you **space out** (blur, desaturation, slower look). Too stressed **slows you down (never below 55%), shakes the room and pulses the room red** (<= 2 Hz). With no live pulse the meter is neutral; `[` / `]` are clearly-labelled dev keys. It is never a medical or psychological measurement. |
| Chase | Each chase has **answer stations**. Recover a page (E), then **hold V and answer out loud** (hold SPACE 3 s as the keyboard fallback when voice is unavailable). Gemini only classifies the answer; local rules gate every transition. Speaking loudly or sprinting makes noise. Vale **patrols, investigates noise, chases on sight, and searches** where he last saw you. Alcoves hide you from sight but not from a patient searcher. |
| Vale | Friendly sprite in calm acts, changed sprite in horror. The pen in his hand is a real 3D prop; its blade scales out when he clicks it. |
| Voice | ElevenLabs speech-to-text and text-to-speech, Gemini for short in-character replies (<= 32 words, schema-checked), all through the local bridge. Keys never enter Unity. |

Originals of the sections that follow are kept for story reference; ignore their mentions of a gentle mode, "no camera shake", "no loud spike", "simulated pressure", or the Space-hold rehearsal.

---

# BREATHE.EXE — Continuity

Canonical direction: first-person 2.5D office exploration and pursuit, with flat 2D billboard characters inside simple 3D rooms and close-up interview screens. Unity 6.3 LTS remains the engine. The user clarified that “like Baldi's Basics” means this visual format, superseding their earlier top-down selection. Fictional sample resumes remain approved. Finished models and peripherals are deferred.

## The promise

You arrive for an ordinary interview at Continuity, a company whose promise is “Your next chapter starts here.” The company wants a complete employment history. By the end, it wants every hour of your life. Its interviewer treats your humanity as a formatting problem.

The horror comes from a familiar space becoming hostile, absurd corporate expectations becoming physical obstacles, and the gradual realization that the friendly staff were previous candidates. The office is the same floor plan throughout: learning it in the opening has a practical payoff later. In the updated premise, the people disappear, the office becomes dark red, and a large smiling monster pursues you.

The visual direction is inspired by the deliberately crude educational-game appearance of Baldi's Basics: flat colors, chunky repeated floor tiles, beveled interface buttons, mismatched eyes, awkward graphic portraits, and a grin that becomes grotesque. Use original 2D sprites and characters inside a perspective office. Walk at eye level with mouse look, strafe, and turn controls. The rooms have geometric depth; the people remain flat sprites. The current graphics are procedural placeholders that establish the direction before final art.

This core build is a complete offline prototype, not finished art or a validated interview-training product. Target first play: approximately 8–12 minutes, to be tuned through human playtests. It uses text dialogue, flat sprite characters, primitive office geometry, and quiet synthesized placeholder audio. Detailed character models are unnecessary.

## Cast and fiction

- **Mr. Vale, interviewer:** calm, polite, precise. Never admits that anything abnormal is happening. A pen becomes a knife. His silhouette stays recognizably human, but his eyes and smile no longer behave like a face.
- **Mina, reception:** offers a quiet minute before the interview. Her introduction teaches the blue refuge alcoves. Later, she greets another applicant as if nothing happened.
- **Len, operations:** sells the culture of collaboration while describing a broken two-person printer. This becomes the teamwork challenge.
- **Iris, archive:** quietly insists that some things do not belong on a resume. Her original identity has begun to disappear; preserving your own history gives her a moment of recognition.
- **The candidate:** one of four fictional files: Mara Voss, Eli Park, Noor Patel, or Jonah Reed. Each has a distinct challenge, teamwork example, and meaningful period outside conventional employment. Their gap is a piece of a life, not evidence of failure.

## Complete flow

### Before the interview

The title screen feels like a dated corporate recruiting portal: warm paper, corporate blue, a perfectly normal application. A clear horror notice precedes entry. There is no surprise about the experience being a horror game; the timing of the transformation is the surprise. Gentler pursuit and audio controls are available immediately. Escape always opens a calm pause screen and an immediate end-interview action. There are no strobe effects or camera shake.

### Remember this

Four candidate cards open into a resume. The selected file includes work, a challenge, teamwork, and the gap. The instruction is simply **“Remember this.”** This gives players material even when they have no interview experience. Selecting a different candidate changes the relevant story text; the office and question sequence stay learnable.

The samples are explicitly fictional. Replace the content in `CandidateFiles` when the team's four approved resumes are ready. Never silently substitute real people's private information.

### The office tour

Warm colors and deliberately bland music. Move from Mina to Len to Iris, then to the interview room in the back. Their introductions teach places and mechanics without announcing an enemy: refuge alcoves, printer, archive, exit. Press E near each highlighted person to continue. The tour is safe and has no timer.

### Question 1: “What's your name?”

Vale is ordinary. The candidate introduces themselves using their selected name. Vale says, “Here at Continuity, we see the whole person. At least, the parts that fit on one page.”

The eventual hardware build can establish a provisional baseline in this calm section. The present version neither measures nor labels a physiological baseline.

### The pen click

A small, quiet click. The light over Vale cuts out and slowly reveals his changed face and the knife where the pen was. The transition is a single fade and a deliberate pause, without repeated flashes or a loud audio spike. The office music becomes a low, uneven tone.

### Question 2: “Tell me about [unreadable]”

The question cannot be understood. The player can ask, “Could you repeat the question?” Vale responds, “Yes, I can,” and repeats the same unreadable prompt. Further clarification remains possible, but the game does not require guessing a hidden correct word. “I'll answer what I understand” continues.

Asking for clarification is recorded for the debrief. As specified in the finished notes, Vale always declares this answer “Unsatisfactory,” regardless of the response, and question three always escalates into a chase. This is a scripted hostile response, not an actual evaluation of the player. Later speech integration can recognize equivalent requests, but this build uses explicit buttons.

### Question 3: “Tell me about a time you overcame a challenge.”

The chair slides away. The candidate is in the office again, with Vale pursuing them. The selected candidate's example is shown before the chase. Recover three numbered pages in order: situation, action, result. Rehearse the answer while moving, then return to the interview-room exit.

The structure of the answer is embodied in the route. This is an escape challenge, not a keyword quiz. A compact heading and distance cue points toward the next objective. The office is viewed through the player's eyes; there is no overhead map. Simulated pressure never causes arbitrary instant death.

### Question 4: “Tell me about a time you worked with a team.”

Vale asks for “independent team players.” The people are gone. At their abandoned stations, the candidate calls Iris's recorded voice to hold a switch, then Len's recorded voice to complete the print job. Help persists after each interaction: there is no need to control multiple characters or hold two controls simultaneously. Complete the rehearsal and return to the door while evading Vale.

The payoff for the cheerful tour is functional: you already know where these people work and what the printer needs.

### Question 5: “Explain the gap in your resume.”

Retrieve the original file in the archive. Its unedited line is a memory from the selected candidate's life: caring for a parent, rebuilding a home, changing fields, or traveling with family. Reach the door and decide whether to keep the missing months or let the company redact them.

The player is choosing what this fictional character yields to the company. A career gap is never presented by the debrief as a moral fault or a hiring recommendation.

### “What's your availability?”

The office returns to warm colors. The knife is a pen. Vale thanks the candidate. Mina welcomes the next applicant outside. Then the final question arrives with a calendar that has no weekends.

Two responses combine with the earlier file decision to create three endings:

| Ending | Choices | Final meaning |
|---|---|---|
| **Out of office.** | “I have hours. I also have a life.” | You set a boundary and walk away. A pen clicks behind you; you keep walking. |
| **You kept something.** | Keep the memory, then offer availability | You enter with a part of yourself intact. The contract has a time to go home. Iris remembers her name for a moment. |
| **Welcome to the team.** | Redact the memory, then offer unlimited availability | Your calendar consumes your life. Tomorrow you stand by the printer, rehearsing the line for the next applicant. |

The debrief reports observable game actions: clarification, completed questions, asking for help, and retries. It explicitly states that speech and biometrics were not evaluated. No invented “calmness score,” diagnostic claim, or interview-quality grade is displayed.

## Playable systems

**Movement:** WASD walks and strafes relative to the view. Mouse looks; left/right arrows turn and up/down arrows walk. Shift sprints. E interacts with a nearby highlighted objective. The office has wall collision, open doorways, furniture, and a pathfinding pursuer. Keyboard is the temporary input method while peripheral work proceeds independently.

**Response substitute:** hold Space to rehearse and settle while moving more slowly. Nine seconds of cumulative rehearsal completes the current answer. It does not read speech, monitor breathing, or instruct the player to change their actual breathing. All objective interactions plus the rehearsal are required to leave a question.

**Simulated pressure:** proximity and sprinting increase a clearly labeled game variable; rehearsal and refuge reduce it. It changes the pursuer's speed within a bounded range. It never independently ends the run. A future physiological adapter must handle missing/stale data and must not present this synthetic variable as a body reading.

**Refuge:** the two blue alcoves conceal the player for four seconds per visit. The countdown is visible. Remaining indefinitely does not stop pursuit forever. This offers a short tactical pause without replacing the real pause menu.

**Failure:** physical contact with the pursuer opens a quiet retry screen. Retry restarts only the current question, resets its objectives and rehearsal, and grants a five-second head start. The selected resume, earlier choices, and retry count persist. The whole run restarts only when the player chooses to end it or apply again.

**Pause and stop:** Escape suspends simulation, movement, pursuit, and ambience. Losing application focus also pauses. The pause overlay blocks underlying buttons. End Interview immediately returns to the ordinary title. No microphone, sensor, personal-data storage, or network service is active.

## Architecture and handoff

- `InterviewRun`: guarded narrative state, choices, progress, simulated pressure, retries, endings.
- `CandidateFiles`: four fictional content records, independent of the floor plan.
- `OfficeMap`: collision grid, objectives, shelters, and breadth-first navigation.
- `BreatheGame`: camera-relative movement, mouse look, interaction, chase, and screen flow.
- `OfficeWorld`: primitive room geometry, point-filtered textures, billboard sprites, first-person camera, and objective beacons.
- `TerminalUI`: responsive 1600x900 reference canvas, menus, interview close-ups, and a transparent first-person HUD.
- `OfficeSound`: low-volume synthesized placeholder ambience and cues.
- `BreatheBuild`: Unity Editor menu and command-line scene/build entry points. Unity generates the actual scene and metadata.
- `CoreFlowTests`: complete endings, gated exits, retries, pressure bounds, invalid transitions, and reachable objectives.

The existing URP project renders a perspective camera at eye level. Collision and pathfinding share one logical floor grid with the room geometry. Sprite materials use alpha clipping, and both opaque and sprite shader variants are retained as Resources materials for native builds. The UI stays readable in a centered 16:9 frame. No dependency upgrades are needed.

## Later milestones, in order

1. Human playtest: pacing, pursuit fairness, legibility, and whether the three escapes feel distinct.
2. Replace graphic placeholders with the team's 2D portraits, billboard animation, office textures, licensed music, and designed sound. Keep simple room geometry and flat characters; add detailed props only if they support the intended crude look.
3. Integrate actual movement devices after identifying their interfaces. Retain keyboard fallback.
4. Integrate the actual sensor stream and define units, baseline window, stale-data timeout, and signal-quality behavior. ESP-NOW receiver work in the remote repository is a clue about the team's hardware, not a verified connection to this Mac. Do not assume BLE.
5. Add speech input with the Mac/internal/USB microphone and AirPods for playback. Map clarification intents and answer activity first; retain buttons if transcription fails.
6. Add ElevenLabs performance and optional Gemini dialogue/debrief behind local fallbacks. The current script and ending rules remain authoritative so a service outage cannot block the story. Keep secrets outside the Unity client and keep any answer-quality assessment separate from physiology.

No runtime service, hardware integration, external voice generation, remote publishing, or asset license clearance is implied by the core implementation.
