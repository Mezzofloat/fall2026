using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using U = Breathe.TerminalUI;

namespace Breathe
{
    /// <summary>
    /// Game flow. Every screen is a 3D place: the lobby (menu), the interview room, the office, and the jumpscare. UI is drawn
    /// on world-space canvases mounted on props; the only screen-space UI is the transparent first-person HUD.
    /// Horror happens only while a question is being asked or chased; between rounds the world returns to the sleek safe office
    /// behind a slow eyelid blink, as though the horror had been in the player's head.
    /// </summary>
    public sealed class BreatheGame : MonoBehaviour
    {
        public const float ScareLength = 2.4f;
        public InterviewRun Run { get; private set; }
        public FearModel Fear { get; } = new FearModel();
        public GloveInput Glove { get; } = new GloveInput();
        public LiveServices Live { get; private set; }
        public MusicDirector Music { get; private set; }
        public StageWorld Stage { get; private set; }
        public Hud HudView { get; private set; }
        public EyelidFx Eyelids { get; private set; }
        public OfficeMap Map => map;
        public OfficeWorld Office => office;
        public HunterAI Hunter => hunter;
        public Vector2 PlayerPosition => player;
        public Vector2 PursuerPosition => hunter != null ? hunter.Position : Vector2.zero;
        public bool IsPaused => paused;
        public Camera WorldCamera => office.Camera;
        public bool JumpscareActive => scareT >= 0f && scareT < ScareLength;
        public float BlackoutLeft => blackoutLeft;
        /// <summary>True whenever a page is held and waiting for its answer: answer from anywhere, in any room.</summary>
        public bool Answering => Run != null && Run.StationReady;
        /// <summary>What the player currently sees: true while the world wears its weathered horror look.</summary>
        public bool VisualHorror => visualHorror;
        public bool TransitionBusy => trans != Trans.None;
        public float EyeOpen => Eyelids != null ? Eyelids.Open : 1f;
        public string HudPrompt => hudData.Prompt;
        public HudData HudInfo => hudData;
        public string HudHint => hudData.Prompt;

        GameAssets assets;
        TerminalUI ui;
        OfficeWorld office;
        PostFx fx;
        OfficeMap map;
        HunterAI hunter;
        Act drawn;
        bool paused, lostFocus, debug, visualHorror;
        float yaw, pitch, age, messageTime, scareT = -1f, blackoutLeft, fallbackHold, devFear = .35f, setupRefresh, dwell, noiseBoost, hunterSpeed, bobPhase, bobAmount, subtitleUntil, knifeCooldown, tape;
        bool answering, devKeysUsed, talkHeldByGlove;
        string message = "";
        Vector2 player, station, lastHunter;
        Surface pauseSurface;
        GameObject scareQuad;
        Material scareMaterial;
        ActorSprite scareActor;
        Text hudDebug, setupInfo, replyLabel;
        string lastReplyShown = "";
        readonly HudData hudData = new HudData();
        enum Trans { None, Closing, Hold, Opening }
        Trans trans; float transT, closeDur, holdDur, openDur; bool transToHorror;
        readonly string[] colleagueNames = { "MINA / RECEPTION", "LEN / OPERATIONS", "IRIS / HR OFFICE", "MR. VALE / INTERVIEW SUITE" };
        readonly string[] tourLines = {
            "Welcome to Continuity. I'm Mina. If you need a quiet minute, the blue alcoves are yours. No one should have to earn a breath.",
            "Len, operations. The printer only works when someone else holds the switch. We are very proud of our culture of collaboration.",
            "I'm Iris. I keep the records. Read your own file before they read it to you. Some things do not belong on a resume.",
            "Right on time. Please take a seat. This will be a perfectly ordinary conversation."
        };

        void Awake()
        {
            Application.targetFrameRate = 60;
            assets = GetComponent<GameAssets>();
            if (assets) { U.HeadFont = assets.headFont; U.BodyFont = assets.bodyFont; }
            Run = new InterviewRun(); map = new OfficeMap();
            office = new OfficeWorld(transform, map, assets);
            Stage = new StageWorld(transform, office.Camera, assets);
            ui = new TerminalUI(transform);
            Music = gameObject.AddComponent<MusicDirector>(); Music.Configure(assets);
            fx = new PostFx(office.Camera, transform);
            Eyelids = new EyelidFx(office.Camera);
            Live = gameObject.AddComponent<LiveServices>(); Live.Answer += OnSpeech;
            hunter = new HunterAI(map, map.HunterSpawn);
            var key = new GameObject("Key light", typeof(Light)); key.transform.SetParent(transform, false); key.transform.rotation = Quaternion.Euler(60, 20, 0);
            var light = key.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = .35f; light.color = new Color(.8f, .85f, 1f); light.shadows = LightShadows.None;
            if (EventSystem.current == null) { var events = new GameObject("UI input", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.transform.SetParent(transform); }
            Live.RefreshCameras();
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BREATHE_AUTOPLAY"))) gameObject.AddComponent<AutoPlay>();
            office.ApplyAtmosphere(false); fx.SnapMode(false);
            Draw();
        }

        void OnDestroy()
        {
            if (Live) Live.Answer -= OnSpeech;
            ui?.Dispose(); office?.Dispose(); Stage?.Dispose(); fx?.Dispose(); Eyelids?.Dispose(); HudView?.Dispose();
            if (scareMaterial) Destroy(scareMaterial); EndScare();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void OnApplicationFocus(bool focused)
        {
            lostFocus = !focused;
            if (!focused && Run != null && Run.Act != Act.Welcome) { paused = true; Live?.Suspend(true); Draw(); }
        }

        /// <summary>Test hook: place the pursuer so chase tests do not depend on a long random patrol.</summary>
        public void DebugPlaceHunter(Vector2 position, float headStart) { hunter.Teleport(position, headStart); lastHunter = position; }
        public void DebugSetPlayer(Vector2 position) { player = position; }
        public void DebugSetView(float yawDegrees, float pitchDegrees) { yaw = yawDegrees; pitch = pitchDegrees; }
        public void DebugAdvanceAge(float seconds) { age += seconds; }
        public void DebugSpeech(SpeechReply reply) { OnSpeech(reply); }
        public void DebugInteract() { Interact(); }
        public float Yaw => yaw;
        public bool Tour => Run.Act == Act.Tour;
        public string Message => message;
        public void DebugSetStress(float value) { devFear = Mathf.Clamp01(value); devKeysUsed = true; }

        bool DrawnIsMap => drawn == Act.Tour || drawn == Act.Challenge || drawn == Act.Team || drawn == Act.Gap;

        // ------------------------------------------------------------------ frame loop
        void Update()
        {
            if (Run == null) return;
            var keys = Keyboard.current; var mouse = Mouse.current; float dt0 = Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (keys != null && keys.escapeKey.wasPressedThisFrame) { paused = !paused; Live.Suspend(paused); Draw(); }
            if (keys != null && keys.f1Key.wasPressedThisFrame) { debug = !debug; Draw(); }
            UpdateCursor();
            ui.SetWorldVisible(true);
            bool frozen = paused || lostFocus;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            Glove.Update(Live.Imu);
            TickGlovePointer(dt0);
            UpdateAudio(frozen);
            fx.SetPaused(frozen);
            var mouse01 = mouse != null ? new Vector2(Mathf.Clamp01(mouse.position.x.ReadValue() / Mathf.Max(1, Screen.width)), Mathf.Clamp01(mouse.position.y.ReadValue() / Mathf.Max(1, Screen.height))) : new Vector2(.5f, .5f);
            Fear.Muted = Run.Act == Act.Welcome || Run.Act == Act.Resume || Run.Act == Act.Ending;
            if (!frozen) UpdateFear(dt, keys);
            fx.Update(Fear, visualHorror, Time.unscaledTime, dt);
            if (!DrawnIsMap) Stage.Tick(dt, Time.unscaledTime, mouse01, frozen, Live.SpeechLevel, Live.SpeechAnger);
            if (frozen) return;
            if (drawn != Run.Act && !TransitionBusy) BeginOrEnter();
            TickTransition(dt);
            age += dt; messageTime -= dt;
            if (TransitionBusy && trans != Trans.Opening) { PresentIdle(dt); return; }
            Live.Page = Run.StationReady ? Run.Collected - 1 : -1;
            Run.Tick(dt); Run.RecordFear(Fear.Fear);
            if (DrawnIsMap) TickMap(dt, keys); else { PresentIdle(dt); TickInterview(dt, keys); }
            TickScare(dt);
            HandleVoice(keys);
            RefreshDynamicText(dt);
            RefreshInterviewHud(dt);
        }

        // ------------------------------------------------------------------ glove cursor for menus and the interview room
        Vector2 glovePointer; bool pointerInit, pointerOwned; GameObject cursorObject; RectTransform cursorRect;
        public Vector2 GlovePointer => glovePointer;
        public void DebugSetGlovePointer(Vector2 pixels) { glovePointer = pixels; pointerInit = true; }

        /// <summary>With gloves on, either stick moves a pointer over the 3D menus and the LEFT-glove click presses what is under it (no mouse needed).</summary>
        void TickGlovePointer(float dt)
        {
            bool active = !DrawnIsMap && Glove.Active && !JumpscareActive && Run.Act != Act.Transformation;
            if (cursorObject == null)
            {
                cursorObject = new GameObject("Glove cursor", typeof(Canvas)); cursorObject.transform.SetParent(transform, false);
                var canvas = cursorObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
                var ring = new GameObject("Ring", typeof(RectTransform), typeof(Image)); ring.transform.SetParent(cursorObject.transform, false); cursorRect = ring.GetComponent<RectTransform>(); cursorRect.sizeDelta = new Vector2(34, 34);
                var img = ring.GetComponent<Image>(); img.color = new Color(1f, .85f, .2f, .9f); img.raycastTarget = false; img.sprite = null;
                var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image)); dot.transform.SetParent(ring.transform, false); dot.GetComponent<RectTransform>().sizeDelta = new Vector2(12, 12); dot.GetComponent<Image>().color = new Color(.05f, .05f, .08f, 1f); dot.GetComponent<Image>().raycastTarget = false;
            }
            var mouse = Mouse.current;
            // The gloves only take over the pointer while you use them; moving the real mouse hands it straight back.
            var stick = Glove.Move + Glove.Look;
            if (active && (stick.sqrMagnitude > .04f || Glove.InteractHeld)) pointerOwned = true;
            if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 4f && !(stick.sqrMagnitude > .04f)) pointerOwned = false;
            cursorObject.SetActive(active && pointerOwned);
            if (!active || mouse == null || !pointerOwned) return;
            if (!pointerInit) { glovePointer = new Vector2(Screen.width / 2f, Screen.height / 2f); pointerInit = true; }
            glovePointer += new Vector2(stick.x, stick.y) * Screen.height * .9f * dt;
            glovePointer = new Vector2(Mathf.Clamp(glovePointer.x, 0, Screen.width), Mathf.Clamp(glovePointer.y, 0, Screen.height));
            mouse.WarpCursorPosition(glovePointer);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = glovePointer }.WithButton(MouseButton.Left, Glove.InteractHeld));
            cursorRect.position = glovePointer;
        }

        void UpdateCursor()
        {
            bool capture = DrawnIsMap && !paused && !lostFocus && !JumpscareActive && !TransitionBusy;
            var mode = capture ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            Cursor.visible = !capture;
        }

        void UpdateAudio(bool frozen)
        {
            Music.Apply(visualHorror, Run.Sound, frozen);
            // The interview before horror: the safe music slowly warps out of tune and slows down (tape slowdown), and resets everywhere else.
            float goal = Run.Act == Act.Name || (Run.Act == Act.Transformation && !visualHorror) ? Mathf.Clamp01((age - 1f) / 10f) : 0f;
            tape = Mathf.MoveTowards(tape, goal, Time.unscaledDeltaTime * (goal > tape ? .12f : 1.5f)); Music.SetTape(tape);
            Music.SetDuck(Live.Speaking || Live.Recording);
        }

        void UpdateFear(float dt, Keyboard keys)
        {
            var p = Live.Pulse;
            if (keys != null && !Fear.Live)
            {
                if (keys.leftBracketKey.wasPressedThisFrame) { devFear = Mathf.Clamp01(devFear - .1f); devKeysUsed = true; }
                if (keys.rightBracketKey.wasPressedThisFrame) { devFear = Mathf.Clamp01(devFear + .1f); devKeysUsed = true; }
            }
            Fear.Update(dt, new FearModel.Input
            {
                PulseValid = Live.BiometricsEnabled && p != null && p.valid && p.baseline_ready, PulseDeltaBpm = p != null ? p.delta_bpm : 0,
                FidgetValid = Glove.FidgetValid, Fidget = Glove.Fidget,
                VoiceActive = Live.Recording, VoiceLoudness = Live.MicLevel, DevOverride = devKeysUsed, DevFear = devFear
            });
        }

        // ------------------------------------------------------------------ act entry and eyelid transitions
        void BeginOrEnter()
        {
            bool want = Run.IsHorror;
            if (want == visualHorror || Run.Act == Act.Transformation || Run.Act == Act.Welcome || drawn == Act.Welcome && !want) { EnterAct(); return; }
            // horror <-> safe changes hide behind a blink: slow and soft back to reality, a quick slam into horror
            transToHorror = want; trans = Trans.Closing; transT = 0;
            closeDur = want ? .18f : .55f; holdDur = want ? .22f : .55f; openDur = want ? .6f : 2.2f;
            Live.EndRecording(false);
        }

        void TickTransition(float dt)
        {
            if (trans == Trans.None) return;
            transT += dt;
            switch (trans)
            {
                case Trans.Closing:
                    SetEye(1f - Mathf.Clamp01(transT / closeDur));
                    if (transT >= closeDur) { SetEye(0); EnterAct(); trans = Trans.Hold; transT = 0; }
                    break;
                case Trans.Hold: SetEye(0); if (transT >= holdDur) { trans = Trans.Opening; transT = 0; } break;
                case Trans.Opening:
                    SetEye(Mathf.Clamp01(transT / openDur));
                    if (transT >= openDur) { SetEye(1f); trans = Trans.None; }
                    break;
            }
        }

        void SetEye(float open) { Eyelids.Open = open; fx.EyeOpen = open; }

        void EnterAct()
        {
            age = 0; message = ""; messageTime = 0; answering = false; fallbackHold = 0; dwell = 0; noiseBoost = 0; EndScare(); drawn = Run.Act;
            bool wasHorror = visualHorror; visualHorror = Run.IsHorror;
            Stage.SetHorrorLook(visualHorror); office.SetHorror(visualHorror);
            if (Run.IsMap)
            {
                yaw = Run.Act == Act.Tour ? 45 : 0; pitch = 0; player = map.Spawn; map.CloseAllDoors();
                hunter.Teleport(map.HunterSpawn, Run.Act == Act.Tour ? 99999f : 12f); pageRejects = 0; lastHunter = hunter.Position;
            }
            blackoutLeft = 0; Stage.SetBlackout(0);
            if (Run.Act == Act.Transformation)
            {
                blackoutLeft = Music.LaughLength > 0 ? Music.LaughLength : 3f;   // black screen while the laugh plays
                Stage.SetBlackout(1f);
            }
            if (Run.Act == Act.Caught) BeginScare();
            if (visualHorror != wasHorror) fx.SnapMode(visualHorror);
            Live.SetContext(Run.Act, Mathf.Max(0, Array.IndexOf(CandidateFiles.All, Run.Candidate)), visualHorror ? "horror" : "safe");
            Draw();
            SpeakQuestion();
        }

        static readonly Dictionary<Act, string> Spoken = new Dictionary<Act, string>
        {
            { Act.Name, "What's your name?" }, { Act.Unreadable, "Tell me about... [unintelligible]." },
            { Act.ChallengeIntro, "Tell me about a time you overcame a challenge." }, { Act.TeamIntro, "Tell me about a time you worked with a team." },
            { Act.GapIntro, "Explain the gap in your resume." }, { Act.Offer, "What's your availability?" }
        };
        void SpeakQuestion() { if (Spoken.TryGetValue(Run.Act, out var line)) Live.Say(line, visualHorror ? "menacing" : "calm", visualHorror ? .55f : .05f); }

        void ActOn(Action action)
        {
            var before = Run.Act; Music.Click(Run.Sound); Music.Paper(Run.Sound); action();
            if (before != Run.Act) { /* Update notices the change and runs the transition */ } else Draw();
        }

        void Restart()
        {
            bool audio = Run.Sound;
            Run = new InterviewRun { Sound = audio }; paused = false; Live.Suspend(false); Fear.Reset(); devKeysUsed = false; trans = Trans.None; SetEye(1f); EnterAct();
        }

        // ------------------------------------------------------------------ drawing
        void ClearSurfaces()
        {
            foreach (var s in new[] { Stage.Monitor, Stage.Clipboard, Stage.Notice, Stage.Sign, Stage.DoorPlate, Stage.FolderTray, Stage.Wall, Stage.Blotter, Stage.Cards }) s.Clear();
            hudDebug = setupInfo = replyLabel = null; lastReplyShown = "";
        }

        void Draw()
        {
            if (pauseSurface != null) { Destroy(pauseSurface.Holder); pauseSurface = null; }
            // A blink is about to change the scene (the act changed but has not been entered yet): only the pause overlay may be rebuilt.
            if (drawn != Run.Act) { if (paused) DrawPause(); return; }
            ui.Clear(); ClearSurfaces(); HudView?.Dispose(); HudView = null;
            office.SetAct(Run);
            bool lobby = Run.Act == Act.Welcome || Run.Act == Act.Resume;
            bool wasHidden = !Stage.Root.activeSelf; bool isMap = DrawnIsMap;
            Stage.Root.SetActive(!isMap);
            if (wasHidden && !isMap) Stage.SnapCamera();
            if (!isMap)
            {
                bool horrorLook = visualHorror || Run.Act == Act.Caught;
                Stage.Show(lobby ? Place.Lobby : Place.Interview, horrorLook);
                Stage.ApplyAtmosphere(horrorLook);
                if (lobby) Stage.SetLobbyView(Run.Act == Act.Resume ? LobbyView.Folders : lobbyView);
                Stage.SetValeVisible(Run.Act != Act.Ending);
                if (Stage.Knife != null && Stage.Knife.Valid) Stage.Knife.SetDeploy(Run.Act == Act.Name || Run.Act == Act.Return || Run.Act == Act.Offer ? 0f : Run.Act == Act.Transformation ? 0f : 1f);
            }
            else { office.ApplyAtmosphere(visualHorror); office.UpdateView(player, hunter.Position, yaw, pitch); }
            if (Run.Act == Act.Welcome) DrawLobby();
            else if (Run.Act == Act.Resume) DrawResume();
            else if (isMap) DrawMap();
            else if (Run.Act == Act.Ending) DrawEnding();
            else if (Run.Act == Act.Caught) DrawCaught();
            else DrawInterview();
            if (!isMap) BuildInterviewHud();
            DrawStatusChips();
            if (paused) DrawPause();
        }

        // Convenience wrappers for drawing on prop surfaces.
        Text L(Surface s, string t, float x, float y, float w, float h, int size, Color c, bool bold = false, TextAnchor a = TextAnchor.UpperLeft) => Stage.Kit.Label(t, x, y, w, h, size, c, bold, s.Root, a);
        Image B(Surface s, float x, float y, float w, float h, Color c) => Stage.Kit.Box(x, y, w, h, c, s.Root);
        Button Btn(Surface s, string t, float x, float y, float w, float h, Action a, bool primary = false, bool dark = false, int size = 26) => Stage.Kit.Button(t, x, y, w, h, () => ActOn(a), primary, dark, s.Root, size);

        LobbyView lobbyView = LobbyView.Front;

        void DrawLobby()
        {
            var sign = Stage.Sign;
            B(sign, 0, 0, sign.PixelWidth, sign.PixelHeight, U.Hex("1d2c54"));
            L(sign, "CONTINUITY", 0, 8, sign.PixelWidth, 170, 140, U.Paper, true, TextAnchor.MiddleCenter);
            L(sign, "PEOPLE OPERATIONS   /   YOUR NEXT CHAPTER STARTS HERE.", 0, 172, sign.PixelWidth, 60, 40, U.Hex("91B7F1"), true, TextAnchor.MiddleCenter);

            var m = Stage.Monitor;
            B(m, 0, 0, m.PixelWidth, m.PixelHeight, U.Hex("0a1612"));
            L(m, "BREATHE.EXE  v0.9", 40, 24, 900, 60, 44, U.Hex("5CE0A0"), true);
            L(m, "A perfectly\nnormal\ninterview.", 40, 90, 1080, 400, 135, U.Hex("D7F8E4"), true);
            L(m, "Five questions. One open position.\nTry not to lose yourself in the process.", 44, 480, 1080, 140, 40, U.Hex("A8E0C0"));
            Btn(m, "BEGIN APPLICATION   >", 44, 600, 1070, 100, Run.StartSelection, true, false, 52);
            L(m, LiveSummary(), 44, 708, 1080, 40, 28, U.Hex("7CC39F"));
            Stage.Kit.Button("QUIT GAME", 44, 754, 330, 60, QuitGame, false, true, m.Root, 28);

            var n = Stage.Notice;
            B(n, 0, 0, n.PixelWidth, n.PixelHeight, U.Hex("E1E0D2"));
            L(n, "BEFORE YOU\nSIT DOWN", 32, 24, 750, 160, 84, U.Red, true);
            L(n, "This is a horror game: darkness, a pursuer with a knife, a loud jump scare, red pulsing light and shaking when you are stressed.\n\nPress ESC at any time to pause or end the interview.\n\nAbout 10 minutes. Your webcam pulse, glove sensors and voice are used as game inputs only if you switch them on.", 32, 200, 750, 760, 38, U.Ink);

            var d = Stage.DoorPlate;
            B(d, 0, 0, d.PixelWidth, d.PixelHeight, U.Hex("D9D3B9"));
            Stage.Kit.Button("EXIT  (QUIT)", 10, 10, 320, 140, QuitGame, false, false, d.Root, 34);

            DrawClipboard();
        }

        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        string LiveSummary()
        {
            string pulse = Live.BiometricsEnabled ? (Live.Pulse != null && Live.Pulse.valid ? "PULSE " + Mathf.RoundToInt(Live.Pulse.bpm) + " bpm" : "PULSE waiting") : "PULSE off";
            string gloves = Glove.Active ? "GLOVES " + (Glove.LeftConnected ? "L" : "-") + (Glove.RightConnected ? "R" : "-") : "GLOVES off";
            return pulse + "  /  " + gloves + "  /  VOICE " + (Live.VoiceEnabled ? "on" : "off") + "  /  clipboard > SETUP";
        }

        void DrawClipboard()
        {
            var c = Stage.Clipboard;
            B(c, 0, 0, c.PixelWidth, c.PixelHeight, U.Hex("F1EDDA"));
            if (lobbyView != LobbyView.Setup)
            {
                L(c, "SETUP", 20, 14, 730, 100, 84, U.Ink, true, TextAnchor.MiddleCenter);
                L(c, "Webcam pulse\nGlove sensors\nMicrophone voice\nAudio", 36, 140, 700, 400, 60, U.Ink);
                Btn(c, "OPEN SETUP  >", 30, 780, 710, 120, () => { lobbyView = LobbyView.Setup; Live.RefreshCameras(); }, true, false, 40);
                return;
            }
            L(c, "SETUP", 20, 6, 730, 64, 52, U.Ink, true, TextAnchor.MiddleCenter);
            Btn(c, "Audio: " + (Run.Sound ? "ON" : "OFF"), 20, 76, 730, 74, () => Run.Sound = !Run.Sound, false, false, 28);
            Btn(c, "Voice (mic + ElevenLabs): " + (Live.VoiceEnabled ? "ON" : "OFF"), 20, 160, 730, 74, () => Live.Configure(!Live.VoiceEnabled, Live.BiometricsEnabled), false, false, 25);
            Btn(c, "Mic: " + ShortName(Live.MicrophoneName) + "  (next)", 20, 244, 730, 74, Live.CycleMicrophone, false, false, 24);
            Btn(c, "Pulse (webcam + Presage): " + (Live.BiometricsEnabled ? "ON" : "OFF"), 20, 328, 730, 74, () => { if (Live.BiometricsEnabled) Live.StopPulse(); else Live.StartPulse(); }, false, false, 25);
            Btn(c, "Camera: " + ShortName(Live.SelectedCameraName) + "  (next)", 20, 412, 730, 74, Live.CycleCamera, false, false, 24);
            setupInfo = L(c, "", 24, 494, 722, 330, 24, U.Ink);
            Btn(c, "Test windows", 20, 836, 350, 70, () => { Live.OpenDiagnostics("presage"); Live.OpenDiagnostics("voice"); }, false, true, 24);
            Btn(c, "DONE  >", 400, 836, 350, 70, () => lobbyView = LobbyView.Front, true, false, 28);
            L(c, "Consent: switching Pulse ON starts the selected camera. Gloves need no consent step: they only send motion. Stress is a game meter, not a medical reading.", 24, 912, 722, 110, 20, U.Muted);
            RefreshSetupInfo();
        }

        static string ShortName(string s) => string.IsNullOrEmpty(s) ? "(none)" : s.Length > 24 ? s.Substring(0, 24) + "…" : s;

        void RefreshSetupInfo()
        {
            if (setupInfo == null) return;
            var p = Live.Pulse; var cam = p != null ? p.camera : null; string warn = "";
            if (cam != null && cam.fps > 0 && cam.fps < 25) warn = "\nLOW FPS: add light on your face or pick another camera.";
            var imu = Live.Imu;
            string glove = imu != null && imu.connected ? "Gloves: " + (imu.left.connected ? "LEFT " : "") + (imu.right.connected ? "RIGHT " : "") + "connected" : "Gloves: not connected (start the hub on the Mac)";
            setupInfo.text = (Live.Connected ? "Bridge: connected" : "Bridge: NOT running (start it on the Mac)") + "\n" +
                (Live.BiometricsEnabled ? (cam != null && cam.fps > 0 ? "Camera: " + cam.width + "x" + cam.height + " @ " + cam.fps.ToString("0") + " fps" : "Camera: starting…") : "Camera: off") + warn + "\n" +
                (p != null && p.valid ? "Pulse: " + Mathf.RoundToInt(p.bpm) + " bpm (conf " + Mathf.RoundToInt(p.confidence) + "%)" : "Pulse: " + (p != null && !string.IsNullOrEmpty(p.validation) ? p.validation : "no reading")) + "\n" + glove + "\n" + Live.Status;
        }

        void DrawResume()
        {
            var tray = Stage.FolderTray;
            B(tray, 0, 0, tray.PixelWidth, tray.PixelHeight, U.Hex("23262f"));
            for (int i = 0; i < CandidateFiles.All.Length; i++)
            {
                int index = i; var c = CandidateFiles.All[i];
                Btn(tray, c.Name + "\n" + c.Role, 12 + i * 316, 14, 300, tray.PixelHeight - 28, () => Run.Select(index), Run.Candidate == c, false, 26);
            }
            var m = Stage.Monitor; var cand = Run.Candidate;
            B(m, 0, 0, m.PixelWidth, m.PixelHeight, U.Hex("F6F2E2"));
            L(m, "REMEMBER THIS.", 36, 14, 1080, 80, 64, U.Teal, true);
            L(m, cand.Initials, 36, 100, 140, 100, 80, U.Teal, true);
            L(m, cand.Name + "\n" + cand.Role.ToUpperInvariant(), 190, 108, 920, 100, 36, U.Ink, true);
            L(m, cand.Experience, 36, 214, 1090, 90, 28, U.Ink);
            L(m, "A CHALLENGE", 36, 312, 520, 30, 22, U.Teal, true); L(m, cand.Challenge, 36, 344, 520, 150, 25, U.Ink);
            L(m, "A TEAM", 600, 312, 520, 30, 22, U.Teal, true); L(m, cand.Team, 600, 344, 520, 150, 25, U.Ink);
            L(m, cand.Gap, 36, 504, 1090, 80, 25, U.Teal, true);
            Btn(m, "OK. I REMEMBER.   >", 36, 618, 1090, 120, Run.StartTour, true, false, 44);
        }

        // ---- interview room screens
        void Interview(string eyebrow, string title, string body, params (string text, Action act, bool primary)[] choices)
        {
            bool scary = visualHorror;
            Stage.Blotter.SetActive(true);
            var w = Stage.Wall;
            B(w, 0, 0, w.PixelWidth, w.PixelHeight, scary ? U.Hex("1a0d12") : U.Hex("e8e2c8"));
            // Left of Mr. Vale: the question. Right of him: what he says and your file.
            L(w, eyebrow, 40, 16, 640, 40, 26, scary ? U.Red : U.Teal, true);
            L(w, title, 40, 56, 640, 200, 52, scary ? U.Pale : U.Ink, true);
            L(w, body, 935, 20, 535, w.PixelHeight - 40, body.Length > 230 ? 21 : 25, scary ? U.Pale : U.Ink);
            var b = Stage.Blotter;
            B(b, 0, 0, b.PixelWidth, b.PixelHeight, scary ? U.Hex("2a2a30") : U.Hex("F4F0DC"));
            replyLabel = L(b, "", 40, 30, 1320, 170, 36, scary ? U.Hex("FF8F8F") : U.Red, true, TextAnchor.MiddleLeft);
            var cards = Stage.Cards;
            B(cards, 0, 0, cards.PixelWidth, cards.PixelHeight, U.Hex("23262f"));
            int count = choices.Length; float h = Mathf.Min(116, (cards.PixelHeight - 20 - (count - 1) * 10) / Mathf.Max(1, count));
            for (int i = 0; i < count; i++) { var c = choices[i]; Btn(cards, c.text, 14, 10 + i * (h + 10), cards.PixelWidth - 28, h, c.act, c.primary, !c.primary, 32); }
        }

        void DrawInterview()
        {
            var c = Run.Candidate;
            switch (Run.Act)
            {
                case Act.Name:
                    Interview("QUESTION 01 / INTRODUCTIONS", "What's your name?", "MR. VALE\nMake yourself comfortable. Here at Continuity, we see the whole person.\n\nAt least, the parts that fit on one page.",
                        ("My name is " + c.Name + ".", Run.Introduce, true)); break;
                case Act.Transformation:
                    Interview("QUESTION 02 / PLEASE REMAIN SEATED", "Just one moment.", "The office music slows.\nThe light over your chair stays on.\nAcross the desk, something catches the light.");
                    break;
                case Act.Unreadable:
                    Interview("QUESTION 02 / COMMUNICATION", "Tell me about\n[ // YOUR __ // ].", Run.Clarifications == 0 ? "MR. VALE\nYou heard me.\n\nThe words on the page refuse to stay words." : "MR. VALE\nYes, I can.\nTell me about [ // YOUR __ // ].\n\nThe question is no clearer. Asking was still reasonable.",
                        ("Could you repeat the question?", Run.Clarify, false), ("I'll answer what I understand.", Run.FinishUnreadable, true)); break;
                case Act.ChallengeIntro:
                    Interview("QUESTION 03 / RESILIENCE", "Tell me about a time\nyou overcame a challenge.", "MR. VALE\nUnsatisfactory. Let's try a practical example.\n\nYOUR FILE\n" + c.Challenge + "\n\nThe people are gone. Recover your situation, action, and result, and answer each aloud, while something grinning searches for you.",
                        ("GET BACK TO THE DOOR   >", Run.StartQuestion, true)); break;
                case Act.TeamIntro:
                    Interview("QUESTION 04 / COLLABORATION", "Tell me about a time\nyou worked with a team.", "MR. VALE\nWe expect independent team players.\n\nYOUR FILE\n" + c.Team + "\n\nThe chairs are empty. Call Iris and Len on the office intercoms. Their recorded voices are still here.",
                        ("FIND IRIS AND LEN   >", Run.StartQuestion, true)); break;
                case Act.GapIntro:
                    Interview("QUESTION 05 / CONTINUITY", "Explain the gap\nin your resume.", "MR. VALE\nWe cannot account for these months.\n\n" + c.Gap + "\n\nYour original file is in the archive. Get it before he does.",
                        ("RETRIEVE YOUR FILE   >", Run.StartQuestion, true)); break;
                case Act.GapChoice:
                    Interview("QUESTION 05 / YOUR ORIGINAL RECORD", "These months\nwere not empty.", "IN YOUR OWN HANDWRITING\n" + c.Memory + "\n\nMR. VALE\nWe can make your history continuous. Just let us remove the irrelevant parts.",
                        ("Keep the missing months.", () => Run.ChooseMemory(true), true), ("Let them redact the gap.", () => Run.ChooseMemory(false), false)); break;
                case Act.Return:
                    Interview("INTERVIEW COMPLETE", "A perfectly\nordinary office.", "The knife is a pen. The clock ticks once.\n\nMR. VALE\nThank you. That was very useful.\n\nFrom the hall, you hear Mina greet the next candidate.",
                        ("LOOK UP   >", Run.ReturnToInterview, true)); break;
                case Act.Offer:
                    Interview("ONE LAST QUESTION", "What's your\navailability?", "MR. VALE\nWe think you'll fit right in.\n\nHis smile is normal again. The calendar on his desk has no weekends.",
                        ("I have hours. I also have a life.", () => Run.Finish(true), true), ("Whenever you need me.", () => Run.Finish(false), false)); break;
            }
        }

        void DrawCaught()
        {
            if (JumpscareActive) return;   // the jumpscare owns the screen; the retry cards appear afterwards
            Interview("PLEASE STAY ON TOPIC.", "Let's try that\nquestion again.", "You were caught. Your interview is not over.\nRestart this question with a brief head start.\n\nMR. VALE\nI'll wait.",
                ("RETRY THIS QUESTION   >", Run.Retry, true), ("END INTERVIEW", Restart, false));
        }

        void DrawEnding()
        {
            var w = Stage.Wall;
            Stage.Blotter.SetActive(false);
            B(w, 0, 0, w.PixelWidth, w.PixelHeight, U.Hex("e8e2c8"));
            L(w, "APPLICATION CLOSED   /   " + Run.Candidate.Name, 40, 14, 760, 34, 24, U.Teal, true);
            L(w, Run.EndingTitle, 40, 46, 760, 90, 62, U.Ink, true);
            L(w, Run.EndingText, 40, 140, 760, 300, 27, U.Ink);
            string report = "YOUR SESSION\n" + (Run.Clarifications > 0 ? "You asked for clarification.\n" : "Try asking for clarification when a prompt is unclear.\n") +
                "You completed all three escape questions.\nAnswers spoken aloud: " + Run.VoiceAnswers + "   by keyboard: " + Run.KeyboardAnswers + "\nQuestion retries: " + Run.Catches +
                "\nPeak stress (game meter): " + Mathf.RoundToInt(Run.PeakFear * 100) + "%\n\nThis is game feedback, not a hiring assessment or a health reading.";
            B(w, 830, 20, 650, w.PixelHeight - 40, U.Hex("F4F0DC"));
            L(w, report, 850, 32, 610, w.PixelHeight - 60, 26, U.Ink);
            var cards = Stage.Cards;
            B(cards, 0, 0, cards.PixelWidth, cards.PixelHeight, U.Hex("23262f"));
            Btn(cards, "ANOTHER APPLICATION   >", 14, 12, 880, 110, Restart, true, false, 36);
            Btn(cards, "QUIT", 910, 12, 476, 110, QuitGame, false, true, 36);
            L(cards, "THIS WAS AN INTERVIEW HORROR GAME. Your worth is not a productivity metric.", 14, 142, 1370, 100, 30, U.Hex("91B7F1"), true);
        }

        void DrawPause()
        {
            pauseSurface = Stage.MakeSurface(office.Camera.transform, "Pause sign", new Vector3(0, 0, 1.25f), Vector3.zero, 1.5f, 1.4f, 900, "1d2c54", .03f);
            var s = pauseSurface;
            B(s, 0, 0, s.PixelWidth, s.PixelHeight, U.Hex("14222f"));
            L(s, "TAKE YOUR TIME.", 30, 20, 840, 110, 70, U.Paper, true, TextAnchor.MiddleCenter);
            L(s, "The interview and pursuit are paused.\nNothing is recording you while paused.", 30, 135, 840, 110, 28, U.Pale, false, TextAnchor.UpperCenter);
            Stage.Kit.Button("RESUME", 60, 260, 780, 100, () => { paused = false; Live.Suspend(false); Draw(); }, true, false, s.Root, 38);
            Stage.Kit.Button("Audio: " + (Run.Sound ? "ON" : "OFF"), 60, 380, 780, 90, () => { Run.Sound = !Run.Sound; Draw(); }, false, true, s.Root, 32);
            Stage.Kit.Button("END INTERVIEW NOW", 60, 490, 780, 100, Restart, false, true, s.Root, 34);
            Stage.Kit.Button("QUIT GAME", 60, 610, 780, 100, QuitGame, false, true, s.Root, 34);
            L(s, "F1: debug overlay     [ and ]: dev stress when no live signal", 30, 740, 840, 40, 20, U.Hex("8FA0AE"), false, TextAnchor.UpperCenter);
        }

        void DrawStatusChips()
        {
            if (DrawnIsMap || !debug) return;
            hudDebug = ui.Label("", 24, 130, 700, 400, 18, U.Hex("5CE0A0"));
        }

        // ------------------------------------------------------------------ first-person HUD (transparent overlays only)
        void DrawMap()
        {
            HudView = new Hud(ui, map); HudView.Build(Run.Act == Act.Tour, Run.Required);
            if (debug) hudDebug = ui.Label("", 24, 240, 700, 420, 16, U.Hex("5CE0A0"));
            RefreshHud(0f);
        }

        void BuildInterviewHud()
        {
            HudView = new Hud(ui, map); HudView.Build(false, 0, true);
            RefreshInterviewHud(0f);
        }

        string QuestionTitle()
        {
            switch (Run.Act)
            {
                case Act.Name: return "INTRODUCTIONS"; case Act.Transformation: case Act.Unreadable: return "COMMUNICATION";
                case Act.ChallengeIntro: case Act.Challenge: return "OVERCOME A CHALLENGE"; case Act.TeamIntro: case Act.Team: return "WORK WITH A TEAM";
                case Act.GapIntro: case Act.Gap: case Act.GapChoice: return "EXPLAIN THE GAP"; case Act.Offer: case Act.Return: return "LAST QUESTION"; case Act.Caught: return "TRY THAT AGAIN";
                default: return "";
            }
        }

        string InputModeLine()
        {
            string talk = Glove.RightConnected ? "RIGHT GLOVE CLICK (hold)" : "[V] (hold)";
            string use = Glove.LeftConnected ? "LEFT GLOVE CLICK" : "[E]";
            return "TALK TO MR. VALE ANYTIME: hold " + talk + "      INTERACT: " + use + "      PAUSE: [ESC]";
        }

        /// <summary>Interview-room overlay: round tracker, REC indicator, subtitles and plain instructions for how to answer.</summary>
        void RefreshInterviewHud(float dt)
        {
            if (HudView == null || DrawnIsMap) return;
            var d = hudData; d.Tour = false; d.Question = Run.QuestionNumber; d.Title = QuestionTitle();
            d.Recording = Live.Recording; d.MicLevel = Live.MicLevel; d.SpeechAnger = Live.SpeechAnger;
            d.Subtitle = Time.unscaledTime < subtitleUntil ? Live.LastReply : ""; d.SubtitleSpeaker = "MR. VALE";
            bool canAnswer = LiveRules.CanAnswer(Run.Act);
            if (Live.Recording) d.Prompt = "RECORDING...  RELEASE " + (Glove.RightConnected ? "THE RIGHT GLOVE CLICK" : "[V]") + " WHEN YOU ARE DONE.";
            else if (Live.Busy) d.Prompt = "MR. VALE IS LISTENING...";
            else if (Run.Act == Act.Welcome || Run.Act == Act.Resume || Run.Act == Act.Ending) d.Prompt = "";
            else if (canAnswer && Live.VoiceEnabled && Live.Connected) d.Prompt = "HOW TO ANSWER:  HOLD " + (Glove.RightConnected ? "THE RIGHT GLOVE CLICK" : "[V]") + ", SPEAK YOUR ANSWER OUT LOUD, THEN RELEASE.  Or click a card on the desk.";
            else if (canAnswer) d.Prompt = "HOW TO ANSWER:  CLICK ONE OF THE CARDS ON THE DESK.  (Turn on VOICE in the lobby setup to answer out loud.)";
            else d.Prompt = "CLICK THE CARD ON THE DESK TO CONTINUE.";
            d.Mode = Run.Act == Act.Welcome || Run.Act == Act.Resume || Run.Act == Act.Ending ? "" : InputModeLine();
            d.FallbackProgress = d.InteractProgress = 0; d.Chips = Chips();
            HudView.Update(d, dt);
        }

        string Chips()
        {
            var p = Live.Pulse;
            string imu = Live.Imu != null && Live.Imu.connected ? "GLOVES " + (Live.Imu.left.connected ? "L" : "-") + (Live.Imu.right.connected ? "R" : "-") : "GLOVES off";
            return "CAM " + (Live.BiometricsEnabled ? (p != null && p.camera != null ? p.camera.fps.ToString("0") + "fps" : "…") : "off") + "   " + imu + "   MIC " + (Live.VoiceEnabled ? (Live.Recording ? "REC" : "ready") : "off") + "   BRIDGE " + (Live.Connected ? "ok" : "down") + "   hunter: " + hunter.Mode;
        }

        Vector2 Destination()
        {
            var targets = map.Objectives(Run.Act); bool tour = Run.Act == Act.Tour;
            return tour ? map.Tour[Mathf.Min(3, Run.TourStop)] : Run.StationReady ? station : Run.ExitReady ? map.Exit : Run.Collected < targets.Length ? targets[Run.Collected] : map.Exit;
        }

        bool NearInteract(out string label)
        {
            label = null;
            if (Run.Act == Act.Tour) { if (Vector2.Distance(player, map.Tour[Mathf.Min(3, Run.TourStop)]) < 1.6f) { label = Run.TourStop == 3 ? "TAKE YOUR SEAT" : "CONTINUE THE TOUR"; return true; } return false; }
            var targets = map.Objectives(Run.Act);
            if (!Run.StationReady && Run.Collected < targets.Length && Vector2.Distance(player, targets[Run.Collected]) < 1.6f) { label = Run.Act == Act.Team ? "ASK FOR HELP" : Run.Act == Act.Gap ? "READ YOUR ORIGINAL FILE" : "PICK UP THE PAGE"; return true; }
            if (Run.ExitReady && Vector2.Distance(player, map.Exit) < 1.6f) { label = "LEAVE THROUGH THE INTERVIEW DOOR"; return true; }
            return false;
        }

        void RefreshHud(float dt)
        {
            if (!DrawnIsMap || HudView == null) return;
            var d = hudData; bool tour = Run.Act == Act.Tour; var p = Live.Pulse;
            var targets = map.Objectives(Run.Act); var dest = Destination();
            Vector2 offset = dest - player; float bearing = Mathf.DeltaAngle(yaw, Mathf.Atan2(offset.x, -offset.y) * Mathf.Rad2Deg);
            d.Tour = tour; d.Question = Run.QuestionNumber; d.Required = Run.Required; d.Collected = Run.Collected; d.Answered = Run.Answered;
            d.Pages = new PageState[Run.Required]; for (int i = 0; i < d.Pages.Length; i++) d.Pages[i] = Run.PageStateOf(i);
            d.Title = tour ? "" : QuestionTitle(); d.Room = OfficeMap.RoomName(player); d.DistanceMeters = offset.magnitude; d.BearingDegrees = bearing; d.ExitReady = Run.ExitReady;
            d.Stress = Fear.Fear; d.Source = StressSource(); d.Player = player; d.Yaw = yaw; d.Exit = map.Exit; d.Recording = Live.Recording; d.MicLevel = Live.MicLevel; d.SpeechAnger = Live.SpeechAnger;
            var list = new List<Vector2>(); if (tour) list.Add(dest); else if (!Run.ExitReady) for (int i = Run.StationReady ? Run.Collected - 1 : Run.Collected; i < targets.Length; i++) if (i >= 0) list.Add(targets[i]);
            d.Objectives = list.ToArray();
            d.ValeVisible = Run.IsChase && hunter.Sees(player, map.Sheltered(player)) && Vector2.Distance(hunter.Position, player) < 9f; d.Vale = hunter.Position; d.ValeHeard = Run.IsChase && !d.ValeVisible && hunter.Mode != HunterAI.State.Patrol;
            d.Subtitle = Time.unscaledTime < subtitleUntil ? Live.LastReply : Time.unscaledTime < spokenUntil ? spokenLine : "";
            d.Chips = Chips(); d.FallbackProgress = Mathf.Clamp01(fallbackHold / 3f); d.InteractProgress = Mathf.Clamp01(dwell / 1f);
            BuildPrompt(d, tour);
            HudView.Update(d, dt);
            if (hudDebug != null) hudDebug.text = "DEBUG (F1)\nstress " + Fear.Fear.ToString("0.00") + "  source " + Fear.Source + "  pulse " + Fear.PulseComponent.ToString("0.00") + " glove " + Fear.GloveComponent.ToString("0.00") + " voice " + Fear.VoiceComponent.ToString("0.00") +
                "\nspaceOut " + Fear.SpaceOut.ToString("0.00") + "  panic " + Fear.Panic.ToString("0.00") + "  speed x" + Fear.SpeedScale.ToString("0.00") + "  elevated " + Fear.Elevated +
                "\nhunter " + hunter.Mode + " noise " + lastNoise.ToString("0.0") + " door wait " + hunter.DoorWaitLeft.ToString("0.0") + " opened " + hunter.DoorsOpened + " burst " + hunter.DoorsBurst +
                "\nmusic " + Music.Current + " pitch " + Music.SafePitch.ToString("0.00") + " tape " + Music.Tape.ToString("0.00") + " duck " + Music.Duck.ToString("0.00") + "\n" + Live.Status + "\npulse: " + (p != null ? p.status + " | " + p.validation : "-");
        }

        string StressSource()
        {
            var p = Live.Pulse;
            switch (Fear.Source)
            {
                case FearModel.SourceKind.PulseAndGlove: return "PULSE " + Mathf.RoundToInt(p.bpm) + " bpm + GLOVE FIDGET";
                case FearModel.SourceKind.Pulse: return "LIVE PULSE " + Mathf.RoundToInt(p.bpm) + " bpm (base " + Mathf.RoundToInt(p.baseline_bpm) + ")";
                case FearModel.SourceKind.Glove: return "GLOVE FIDGET ONLY";
                case FearModel.SourceKind.Dev: return "DEV KEYS [ ] (not a body reading)";
                default:
                    if (Run.Act == Act.Tour && Live.BiometricsEnabled) return p != null && p.baseline_ready ? "BASELINE READY " + Mathf.RoundToInt(p.baseline_bpm) + " bpm" : "CALIBRATING... hold still (" + (p != null ? p.calibration_samples : 0) + ")";
                    return "no body signal: neutral";
            }
        }

        string StationPrompt()
        {
            int i = Mathf.Max(0, Run.Collected - 1);
            if (Run.Act == Act.Team) return i == 0 ? "IRIS: \"Tell me why you asked for help.\"" : "LEN: \"The printer jammed. What did you do together?\"";
            if (Run.Act == Act.Gap) return "Say what happened in those months, in your own words.";
            return i == 0 ? "SITUATION: say what actually went wrong." : i == 1 ? "ACTION: say what you did about it." : "RESULT: say what changed because of you.";
        }

        void BuildPrompt(HudData d, bool tour)
        {
            string talk = Glove.RightConnected ? "RIGHT GLOVE CLICK" : "[V]", use = Glove.LeftConnected ? "LEFT GLOVE CLICK" : "[E]";
            if (Live.Recording) d.Prompt = "RECORDING...  RELEASE " + talk + " TO SEND YOUR ANSWER.";
            else if (Live.Busy) d.Prompt = "MR. VALE IS LISTENING...";
            else if (messageTime > 0) d.Prompt = message;
            else if (map.Sheltered(player)) d.Prompt = "QUIET ALCOVE: HE CANNOT SEE YOU HERE, BUT HE WILL SEARCH IT.";
            else if (tour) d.Prompt = NearInteract(out var l0) ? use + "  " + l0 + (Glove.LeftConnected ? "   (or wait 1 s)" : "") : "FOLLOW THE YELLOW LIGHT TO " + colleagueNames[Mathf.Min(3, Run.TourStop)].Split('/')[0].Trim() + ".  The map (top right) shows the way.";
            else if (Run.ExitReady) d.Prompt = NearInteract(out var l1) ? use + "  " + l1 : "ALL PAGES ANSWERED.  RUN TO THE GREEN MARK ON THE MAP: THE INTERVIEW DOOR.";
            else if (Run.StationReady) d.Prompt = "PAGE " + Run.Collected + " ASKS:  " + Run.CurrentPageQuestion.Substring(Run.CurrentPageQuestion.IndexOf(':') + 1).Trim() + "\nANSWER OUT LOUD FROM ANYWHERE: hold " + talk + ", speak, release.";
            else d.Prompt = NearInteract(out var l3) ? use + "  " + l3 : "FIND PAGE " + (Run.Collected + 1) + " OF " + Run.Required + ": THE YELLOW LIGHT.  CHECK THE MAP, TOP RIGHT.";
            string fallbackLine = Run.StationReady ? "STUCK? HOLD [SPACE] 3 SECONDS TO ANSWER BY KEYBOARD.     " : "";
            d.Mode = fallbackLine + InputModeLine();
        }

        float lastNoise; int pageRejects; string spokenLine = ""; float spokenUntil;

        // ------------------------------------------------------------------ map gameplay
        void TickMap(float dt, Keyboard keys)
        {
            Vector2 local = Vector2.zero; float look = Fear.LookScale;
            if (keys != null)
            {
                if (keys.aKey.isPressed) local.x--; if (keys.dKey.isPressed) local.x++;
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) local.y++; if (keys.sKey.isPressed || keys.downArrowKey.isPressed) local.y--;
                if (keys.leftArrowKey.isPressed) yaw -= 115 * dt * look; if (keys.rightArrowKey.isPressed) yaw += 115 * dt * look;
            }
            local += Glove.Move; if (local.sqrMagnitude > 1) local.Normalize();
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            { var delta = Mouse.current.delta.ReadValue(); yaw += delta.x * .09f * look; pitch = Mathf.Clamp(pitch - delta.y * .09f * look, -65, 65); }
            yaw += Glove.Look.x * 130f * dt * look; pitch = Mathf.Clamp(pitch + Glove.Look.y * 80f * dt * look, -65, 65);
            float radians = yaw * Mathf.Deg2Rad;
            Vector2 move = new Vector2(local.x * Mathf.Cos(radians) + local.y * Mathf.Sin(radians), local.x * Mathf.Sin(radians) - local.y * Mathf.Cos(radians));
            bool sprint = ((keys != null && keys.leftShiftKey.isPressed) || Glove.Sprint) && move.sqrMagnitude > 0;
            float speed = (sprint ? 5.1f : 3.6f) * Fear.SpeedScale * Mathf.Min(1f, move.magnitude + (keys != null && (keys.wKey.isPressed || keys.aKey.isPressed || keys.sKey.isPressed || keys.dKey.isPressed) ? 1f : 0f));
            Vector2 before = player;
            player = map.Move(player, move.normalized * speed * dt);
            float moved = (player - before).magnitude / Mathf.Max(.0001f, dt);
            map.UpdateDoors(dt, player, hunter.Position);
            bool hidden = map.Sheltered(player);
            bool caught = false;
            if (Run.IsChase)
            {
                noiseBoost = Mathf.Max(0, noiseBoost - dt);
                float noise = move.sqrMagnitude > 0 ? (sprint ? 6f : 2.6f) : 1f;
                if (Live.Recording) noise += Live.MicLevel * 4f;
                if (noiseBoost > 0) noise += 4f;
                if (hidden) noise *= .45f; lastNoise = noise;
                caught = hunter.Tick(dt, player, hidden, noise, 1f, Fear.Elevated);
                hunterSpeed = Mathf.Lerp(hunterSpeed, (hunter.Position - lastHunter).magnitude / Mathf.Max(.0001f, dt), .25f); lastHunter = hunter.Position;
                // he swings the knife when he is close and coming for you
                knifeCooldown -= dt;
                if (hunter.Mode == HunterAI.State.Chase && Vector2.Distance(hunter.Position, player) < 3.2f && knifeCooldown <= 0) { Music.KnifeSwing(Run.Sound, OfficeWorld.Point(hunter.Position, 1.3f)); knifeCooldown = 2.4f; }
            }
            DrainDoorEvents();
            // walking bob and roll for the player, plus fear shake
            bool walking = moved > .2f; bobAmount = Mathf.MoveTowards(bobAmount, walking ? 1f : 0f, dt * 6f);
            if (walking) bobPhase += dt * moved * 1.9f;
            Vector3 offset = new Vector3(Mathf.Cos(bobPhase) * .018f * bobAmount, Mathf.Abs(Mathf.Sin(bobPhase)) * .05f * bobAmount - .02f * bobAmount, 0);
            if (Fear.ShakeAmplitude > 0)
            {
                float t = Time.unscaledTime * 22f, a = Fear.ShakeAmplitude;
                offset += new Vector3((Mathf.PerlinNoise(t, 0) - .5f) * 2 * a, (Mathf.PerlinNoise(0, t) - .5f) * 2 * a, (Mathf.PerlinNoise(t, t) - .5f) * 2 * a);
            }
            float talk = hunter.Mode == HunterAI.State.Chase || Live.Speaking ? Live.SpeechLevel : 0f;
            office.UpdateView(player, hunter.Position, yaw, pitch, dt, Time.unscaledTime, Run.IsChase ? hunterSpeed : 0f, talk, Mathf.Max(Live.SpeechAnger, hunter.Mode == HunterAI.State.Chase ? .55f : .15f), offset, Mathf.Sin(bobPhase) * .5f * bobAmount);
            Music.SetFootsteps(Run.Sound, Mathf.Clamp01(moved / 5.1f), Run.IsChase ? Mathf.Clamp01(hunterSpeed / 3.3f) : 0f, office.Vale?.Root.transform);
            if (caught) { Music.KnifeCut(Run.Sound); Run.Catch(); return; }
            // interact: key, glove click, or standing at it with gloves
            bool interact = keys != null && keys.eKey.wasPressedThisFrame || Glove.InteractPressed;
            if (Glove.Active && NearInteract(out _)) { dwell += dt; if (dwell >= 1f) { interact = true; dwell = 0; } } else dwell = 0;
            if (interact) { dwell = 0; Interact(); }
            TickAnswer(dt, keys);
            RefreshHud(dt);
        }

        void DrainDoorEvents()
        {
            while (map.Events.Count > 0)
            {
                var (kind, where) = map.Events.Dequeue(); var world = OfficeWorld.Point(where, 1f);
                if (kind == DoorEvent.Burst) { Music.DoorBurst(Run.Sound, world); noiseBoost = Mathf.Max(noiseBoost, .5f); }
                else Music.DoorOpen(Run.Sound, world);
            }
        }

        void Interact()
        {
            if (Run.Act == Act.Tour)
            {
                if (Vector2.Distance(player, map.Tour[Mathf.Min(3, Run.TourStop)]) < 1.6f) ActOn(Run.MeetColleague);
                return;
            }
            var targets = map.Objectives(Run.Act);
            if (!Run.StationReady && Run.Collected < targets.Length && Vector2.Distance(player, targets[Run.Collected]) < 1.6f)
            {
                station = targets[Run.Collected]; Music.Click(Run.Sound); Music.Paper(Run.Sound);
                if (Run.Collect()) { message = ""; messageTime = 0; office.SetAct(Run); AskPageQuestion(); }
                return;
            }
            if (Vector2.Distance(player, map.Exit) < 1.6f)
            {
                if (Run.ExitReady) ActOn(Run.ExitQuestion);
                else { message = "RECOVER AND ANSWER EVERY PAGE FIRST."; messageTime = 4; }
            }
        }

        void TickAnswer(float dt, Keyboard keys)
        {
            // Hold SPACE for 3 s to answer by keyboard (always available while a page waits).
            if (!Run.StationReady || keys == null) { fallbackHold = 0; return; }
            if (keys.spaceKey.isPressed && !Live.Recording) { fallbackHold += dt; if (fallbackHold >= 3f) CompleteAnswer(false, "ANSWER ACCEPTED (KEYBOARD FALLBACK)."); }
            else fallbackHold = 0;
        }

        /// <summary>Taking a page asks its own follow-up question: Vale says it and it stays on the HUD until you answer.</summary>
        void AskPageQuestion()
        {
            string q = Run.CurrentPageQuestion; if (string.IsNullOrEmpty(q)) return;
            spokenLine = q.Substring(q.IndexOf(':') + 1).Trim(); spokenUntil = Time.unscaledTime + 12f; subtitleUntil = 0;
            Live.Say(spokenLine, "menacing", .5f);
        }

        void CompleteAnswer(bool spoken, string note)
        {
            if (!Run.Answer(spoken)) return;
            pageRejects = 0; fallbackHold = 0; spokenUntil = 0; message = note; messageTime = 4; Music.Click(Run.Sound); Music.Paper(Run.Sound); office.SetAct(Run);
        }

        // ------------------------------------------------------------------ interview-room tick (transformation, voice)
        void PresentIdle(float dt)
        {
            if (DrawnIsMap)
            {   // keep the office alive (Vale walks in place of the freeze) while a blink hides a change
                office.UpdateView(player, hunter.Position, yaw, pitch, dt, Time.unscaledTime, 0f, 0f, 0f);
                RefreshHud(dt);
            }
        }

        void TickInterview(float dt, Keyboard keys)
        {
            Music.SetFootsteps(false, 0, 0, null);
            if (Run.Act == Act.Transformation)
            {
                float laugh = Mathf.Max(.1f, Music.LaughLength > 0 ? Music.LaughLength : 3f);
                blackoutLeft = Mathf.Max(0, laugh - age);
                Stage.SetBlackout(age < laugh ? 1f : Mathf.Clamp01(1f - (age - laugh) / 1.2f));
                float click = laugh + 1.8f;
                if (age >= click && Stage.Knife != null && Stage.Knife.Valid) Stage.Knife.SetDeploy(Mathf.Clamp01((age - click) / .5f));
                if (age >= click && age - dt < click) { Music.Click(Run.Sound); Music.KnifeSwing(Run.Sound, Camera.main ? Camera.main.transform.position + Vector3.forward * 2f : Vector3.zero); }
                var wallText = Stage.Wall.Root.childCount > 2 ? Stage.Wall.Root.GetChild(2).GetComponent<Text>() : null;
                if (wallText != null) wallText.text = age < click ? "Just one moment." : age < click + 1.5f ? "[ A PEN CLICKS. ]" : "Let's get to know you.";
                if (age > click + 3.5f) { Stage.SetBlackout(0); ActOn(Run.FinishTransformation); }
            }
        }

        // ------------------------------------------------------------------ jumpscare
        void BeginScare()
        {
            scareT = 0; Music.Jumpscare(Run.Sound);
            if (scareMaterial == null)
            {
                var template = Resources.Load<Material>("OfficeSurface");
                scareMaterial = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                scareMaterial.SetColor("_BaseColor", new Color(.25f, 0, .02f));
            }
            scareQuad = new GameObject("Jumpscare"); scareQuad.transform.SetParent(office.Camera.transform, false); scareQuad.transform.localPosition = new Vector3(0, 0, .5f);
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad); back.name = "Jumpscare backdrop"; Destroy(back.GetComponent<Collider>()); back.transform.SetParent(scareQuad.transform, false);
            back.transform.localPosition = new Vector3(0, 0, .3f); back.transform.localScale = new Vector3(2.4f, 1.6f, 1); back.GetComponent<Renderer>().sharedMaterial = scareMaterial;
            scareActor = new ActorSprite("Jumpscare Vale", scareQuad.transform, Outfit.Vale(true), true, 1.15f, null, null, 9);
            scareActor.Root.transform.localPosition = new Vector3(0, -.05f, 0); scareActor.Root.transform.localRotation = Quaternion.identity;
            // the actor's quads face -z in local space; the camera looks down +z, so they already face the player
            scareActor.Tick(.2f, 0, 1f, 1f, office.Camera.transform.position);
        }

        void TickScare(float dt)
        {
            if (scareQuad == null) return;
            scareT += dt;
            float t = Mathf.Clamp01(scareT / ScareLength), punch = Mathf.Lerp(.9f, 1.55f, 1f - Mathf.Pow(1f - t, 3f));
            scareActor?.Tick(dt, 0f, .7f + .3f * Mathf.Sin(Time.unscaledTime * 30f), 1f, office.Camera.transform.position);
            scareActor.Root.transform.localScale = Vector3.one * punch;
            scareQuad.transform.localPosition = new Vector3((Mathf.PerlinNoise(Time.unscaledTime * 40f, 0) - .5f) * .05f, (Mathf.PerlinNoise(0, Time.unscaledTime * 40f) - .5f) * .05f, .5f - t * .12f);
            if (scareT >= ScareLength) { EndScare(); Draw(); }
        }

        void EndScare() { if (scareActor != null) { scareActor.Dispose(); scareActor = null; } if (scareQuad) { Destroy(scareQuad); scareQuad = null; } scareT = -1f; }

        // ------------------------------------------------------------------ voice
        void HandleVoice(Keyboard keys)
        {
            if (!Live.VoiceEnabled) return;
            bool allowed = LiveRules.CanTalk(Run.Act) && !JumpscareActive && !TransitionBusy;
            bool press = keys != null && keys.vKey.wasPressedThisFrame || Glove.TalkPressed, release = keys != null && keys.vKey.wasReleasedThisFrame || Glove.TalkReleased;
            if (press && allowed) Live.BeginRecording();
            if (release) Live.EndRecording(true);
        }

        void OnSpeech(SpeechReply r)
        {
            if (r == null || paused) return;
            switch (r.intent)
            {
                case "stop": Restart(); return;
                case "pause": paused = true; Live.Suspend(true); Draw(); return;
            }
            bool offtopic = r.verdict == "offtopic";
            switch (Run.Act)
            {
                case Act.Name: if (r.intent == "answer" && !offtopic) ActOn(Run.Introduce); break;
                case Act.Unreadable: if (r.intent == "clarify") ActOn(Run.Clarify); else if (r.intent == "answer" && !offtopic) ActOn(Run.FinishUnreadable); break;
                case Act.GapChoice: if (r.intent == "keep_memory") ActOn(() => Run.ChooseMemory(true)); else if (r.intent == "redact_memory") ActOn(() => Run.ChooseMemory(false)); break;
                case Act.Offer: if (r.intent == "set_boundary") ActOn(() => Run.Finish(true)); else if (r.intent == "unlimited") ActOn(() => Run.Finish(false)); break;
                case Act.Challenge: case Act.Team: case Act.Gap:
                    if (r.intent == "answer" && Run.StationReady)
                    {
                        if (offtopic && pageRejects < 2) { pageRejects++; Run.RejectAnswer(); noiseBoost = 2f; message = "THAT WAS NOT AN ANSWER (" + pageRejects + "/2). SAY WHAT THE PAGE ASKS."; messageTime = 5; }
                        else CompleteAnswer(true, "ANSWER ACCEPTED. KEEP MOVING.");
                    }
                    break;
            }
            // Free chat: Vale answers anything, in character. It never changes the story (only the answers above do).
            if (!string.IsNullOrWhiteSpace(r.reply)) { Live.Say(r.reply, string.IsNullOrEmpty(r.tone) ? "calm" : r.tone, r.anger); subtitleUntil = Time.unscaledTime + 7f; }
        }

        void RefreshDynamicText(float dt)
        {
            if (replyLabel != null && Live.LastReply != lastReplyShown) { lastReplyShown = Live.LastReply; replyLabel.text = string.IsNullOrEmpty(lastReplyShown) ? "" : "MR. VALE: " + lastReplyShown; }
            setupRefresh -= dt;
            if (setupRefresh <= 0) { setupRefresh = .5f; RefreshSetupInfo(); }
            if (hudDebug != null && !DrawnIsMap) hudDebug.text = "DEBUG (F1)\n" + Live.Status + "\nstress " + Fear.Fear.ToString("0.00") + " " + Fear.Source + "\n" + (Live.Pulse != null ? Live.Pulse.status : "");
        }
    }
}
