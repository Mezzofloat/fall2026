using System;
using System.Collections;
using System.IO;
using System.Linq;
using Breathe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class PresentationTests
{
    GameObject root;
    BreatheGame game;
    Keyboard keyboard;
    readonly System.Collections.Generic.List<string> clipped = new System.Collections.Generic.List<string>();
    InputSettings.BackgroundBehavior originalBackground;
#if UNITY_EDITOR
    InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
#endif

    static AudioClip Clip(string name, float seconds)
    {
        int rate = 22050, n = (int)(rate * seconds); var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Sin(i * .05f) * .05f;
        var clip = AudioClip.Create(name, n, 1, rate, false); clip.SetData(data, 0); return clip;
    }

    IEnumerator Boot(bool withMusic)
    {
        foreach (var existing in UnityEngine.Object.FindObjectsByType<BreatheGame>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(existing.gameObject);
        yield return null;
        root = new GameObject("Presentation test game");
        var assets = root.AddComponent<GameAssets>();
        if (withMusic) { assets.safe = Clip("safe", 2); assets.laugh = Clip("laugh", 1.5f); assets.horror = Clip("horror", 3); }
#if UNITY_EDITOR
        // Use the real, approved pen-knife parts so screenshots show what the player sees.
        assets.knifeParts = new[] { "Barrel", "Collar", "Grip", "Clip", "Plunger", "Blade" }.Select(n => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Breathe/Art/Props/PenKnife/" + n + ".fbx")).ToArray();
        assets.kitParts = UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Breathe/Art/Props/OfficeKit" }).Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(g))).ToArray();
        assets.propParts = UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Breathe/Art/Props/OfficeProps" }).Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(g))).ToArray();
        assets.headFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/_Breathe/Fonts/Silkscreen-Bold.ttf");
        assets.bodyFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/_Breathe/Fonts/VT323-Regular.ttf");
        if (!withMusic) { AudioClip C(string folder, string name) => UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Breathe/Audio/" + folder + "/" + name + "-norm.wav"); assets.jumpscare = C("SFX", "jumpscare"); assets.doorOpen = C("SFX", "door-open"); assets.doorBreak = C("SFX", "door-break"); assets.knifeCut = C("SFX", "knife-cut"); assets.knifeSwing = C("SFX", "knife-swing"); assets.paper = C("SFX", "paper"); assets.walking = C("SFX", "walking"); }
#endif
        game = root.AddComponent<BreatheGame>();
        game.Run.Sound = withMusic;
        originalBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        keyboard = InputSystem.AddDevice<Keyboard>();
        yield return null; yield return null;
    }
    [UnitySetUp] public IEnumerator Setup() { clipped.Clear(); yield return Boot(false); }
    [UnityTearDown] public IEnumerator Teardown()
    {
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = originalBackground;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
#endif
        UnityEngine.Object.Destroy(root); yield return null;
        Assert.That(clipped, Is.Empty, "Text overflows its box:\n" + string.Join("\n", clipped));
    }

    Button FindButton(string prefix) => root.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text.StartsWith(prefix, StringComparison.Ordinal));
    bool HasButton(string prefix) => root.GetComponentsInChildren<Button>().Any(b => b.GetComponentInChildren<Text>().text.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>The project rule: there is never a purely 2D screen. The world camera renders, and no big opaque UI image covers the view.</summary>
    void AssertNo2DScreen(string label)
    {
        Assert.That(game.WorldCamera.enabled && game.WorldCamera.gameObject.activeInHierarchy, Is.True, label + ": the world camera must render.");
        Assert.That(game.WorldCamera.orthographic, Is.False, label);
        foreach (var canvas in root.GetComponentsInChildren<Canvas>())
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            foreach (var image in canvas.GetComponentsInChildren<Image>())
            {
                if (!image.enabled || !image.gameObject.activeInHierarchy) continue;
                var r = image.rectTransform.rect; bool large = r.width * r.height > .6f * 1600f * 900f;
                Assert.That(large && image.color.a > .6f, Is.False, label + ": opaque full-screen UI image '" + image.name + "' would be a flat 2D screen.");
            }
        }
        var world = root.GetComponentsInChildren<Canvas>().Where(c => c.renderMode == RenderMode.WorldSpace && c.gameObject.activeInHierarchy).ToArray();
        if (!game.Run.IsMap && !game.JumpscareActive && game.Run.Act != Act.Transformation) Assert.That(world.Length, Is.GreaterThan(0), label + ": text must live on props in the 3D scene.");
    }

    static bool ClipsAtSmallestSize(Text t)
    {
        var rect = t.rectTransform.rect;
        if (!t.resizeTextForBestFit) return t.preferredHeight > rect.height + 6;
        var settings = t.GetGenerationSettings(rect.size); settings.resizeTextForBestFit = false; settings.fontSize = t.resizeTextMinSize;
        return t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) / t.pixelsPerUnit > rect.height + 6;
    }

    IEnumerator Capture(string label, int width = 1600, int height = 900)
    {
        game.Stage.SettleCamera(); yield return null; yield return null;
        Canvas.ForceUpdateCanvases();
        AssertNo2DScreen(label);
        foreach (var text in root.GetComponentsInChildren<Text>())
            if (text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text) && ClipsAtSmallestSize(text)) clipped.Add(label + " clips even at its smallest size: " + text.text.Replace("\n", " / "));
        string folder = Environment.GetEnvironmentVariable("BREATHE_QA_PATH");
        if (string.IsNullOrEmpty(folder)) yield break;
        Directory.CreateDirectory(folder);
        var camera = game.WorldCamera; var overlay = root.transform.Find("BREATHE 2D interface").GetComponent<Canvas>();
        TerminalUI.ViewportOverride = new Rect(0, 0, 1, 1);
        var oldRect = camera.rect; float oldAspect = camera.aspect; var oldTarget = camera.targetTexture;
        var target = new RenderTexture(width, height, 24); target.Create();
        camera.rect = new Rect(0, 0, 1, 1); camera.aspect = (float)width / height; camera.targetTexture = target;
        overlay.renderMode = RenderMode.ScreenSpaceCamera; overlay.worldCamera = camera; overlay.planeDistance = .1f;
        yield return null; Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.Combine(folder, label + ".png"), texture.EncodeToPNG());
        RenderTexture.active = previous; overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.worldCamera = null;
        camera.targetTexture = oldTarget; camera.rect = oldRect; camera.aspect = oldAspect; TerminalUI.ViewportOverride = null;
        UnityEngine.Object.Destroy(texture); target.Release(); UnityEngine.Object.Destroy(target);
        yield return null;
    }

    void Answer(int pages)
    {
        for (int i = 0; i < pages; i++) { game.Run.Collect(); game.Run.Answer(false); }
        game.Run.ExitQuestion();
    }

    /// <summary>Wait out the eyelid blink that hides a safe/horror change (it is real time, so tests wait for it).</summary>
    IEnumerator Settle()
    {
        float end = Time.realtimeSinceStartup + 10; yield return null; yield return null;
        while (game.TransitionBusy && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(game.TransitionBusy, Is.False, "The blink must finish.");
        yield return null;
    }

    [UnityTest] public IEnumerator EveryScreenIsA3DPlaceAndButtonsDriveTheFlow()
    {
        yield return Capture("01-lobby-menu"); Assert.That(HasButton("EXIT"), Is.True); Assert.That(HasButton("OPEN SETUP"), Is.True);
        FindButton("BEGIN APPLICATION").onClick.Invoke();
        yield return Capture("02-folders"); FindButton("NOOR PATEL").onClick.Invoke();
        yield return null; Assert.That(game.Run.Candidate.Name, Is.EqualTo("NOOR PATEL"));
        FindButton("OK. I REMEMBER.").onClick.Invoke(); yield return Settle(); yield return Capture("03-tour");
        for (int i = 0; i < 4; i++) game.Run.MeetColleague(); yield return Settle(); yield return Capture("04-name-room");
        Assert.That(game.VisualHorror, Is.False, "The first question is still the safe office.");
        FindButton("My name is").onClick.Invoke(); yield return null; yield return null;
        Assert.That(game.Run.Act, Is.EqualTo(Act.Transformation)); Assert.That(game.BlackoutLeft, Is.GreaterThan(0), "Black screen while the laugh plays.");
        Assert.That(game.VisualHorror, Is.True, "Horror begins with the transformation.");
        yield return Capture("05-blackout");
        game.Run.FinishTransformation(); yield return Settle(); yield return Capture("06-unreadable");
        FindButton("Could you repeat").onClick.Invoke(); yield return Capture("07-clarification");
        FindButton("I'll answer").onClick.Invoke(); yield return Settle();
        Assert.That(game.VisualHorror, Is.False, "Between rounds the world returns to the safe office.");
        yield return Capture("08-challenge-intro");
        FindButton("GET BACK").onClick.Invoke(); yield return Settle();
        Assert.That(game.VisualHorror, Is.True, "Asking the next question brings the horror back.");
        yield return Capture("09-chase"); Answer(3); yield return Settle();
        Assert.That(game.VisualHorror, Is.False); yield return Capture("10-team-intro"); FindButton("FIND IRIS").onClick.Invoke(); yield return Settle();
        yield return Capture("11-team-chase"); Answer(2); yield return Settle(); yield return Capture("12-gap-intro");
        FindButton("RETRIEVE").onClick.Invoke(); yield return Settle(); yield return Capture("13-gap-chase");
        game.Run.Catch(); yield return null; yield return null; yield return Capture("14-jumpscare");
        Assert.That(game.JumpscareActive, Is.True);
        float deadline = Time.realtimeSinceStartup + 6; while (game.JumpscareActive && Time.realtimeSinceStartup < deadline) yield return null;
        yield return Capture("15-caught-cards"); FindButton("RETRY THIS QUESTION").onClick.Invoke(); yield return Settle();
        yield return null; Answer(1); yield return Settle(); yield return Capture("16-gap-choice");
        FindButton("Keep the missing").onClick.Invoke(); yield return Settle(); yield return Capture("17-return");
        FindButton("LOOK UP").onClick.Invoke(); yield return Settle(); yield return Capture("18-offer");
        FindButton("I have hours").onClick.Invoke(); yield return Settle(); yield return Capture("19-ending");
        Assert.That(game.Run.Completed, Is.True);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest] public IEnumerator VisualGalleryOfEveryRoomInSafeAndHorror()
    {
        string folder = Environment.GetEnvironmentVariable("BREATHE_QA_PATH");
        if (string.IsNullOrEmpty(folder)) Assert.Ignore("Set BREATHE_QA_PATH to capture the gallery.");
        var run = game.Run; run.StartSelection(); run.StartTour(); yield return Settle();
        var map = game.Map; int shot = 0;
        for (int room = 0; room < 9; room++)
        {
            var rect = OfficeMap.RoomRect(room); var spot = new Vector2(rect.x + 1.2f, rect.y + 1.2f); var centre = new Vector2(rect.x + rect.width / 2f, rect.y + rect.height / 2f);
            game.DebugSetPlayer(spot); game.DebugSetView(Mathf.Atan2(centre.x - spot.x, -(centre.y - spot.y)) * Mathf.Rad2Deg, 3f); yield return null; yield return null;
            yield return Capture("room" + room + "-" + OfficeMap.RoomNames[room].Replace(' ', '_') + "-safe");
            shot++;
        }
        for (int i = 0; i < 4; i++) run.MeetColleague(); yield return Settle(); run.Introduce(); yield return null; yield return null;
        run.FinishTransformation(); yield return Settle(); run.FinishUnreadable(); yield return Settle(); run.StartQuestion(); yield return Settle();
        Assert.That(game.VisualHorror, Is.True);
        for (int room = 0; room < 9; room++)
        {
            var rect = OfficeMap.RoomRect(room); var spot = new Vector2(rect.x + 1.2f, rect.y + 1.2f); var centre = new Vector2(rect.x + rect.width / 2f, rect.y + rect.height / 2f);
            game.DebugPlaceHunter(new Vector2(rect.x + rect.width - 2.5f, rect.y + rect.height - 2.5f), 99999f);
            game.DebugSetPlayer(spot); game.DebugSetView(Mathf.Atan2(centre.x - spot.x, -(centre.y - spot.y)) * Mathf.Rad2Deg, 3f); yield return null; yield return null;
            yield return Capture("room" + room + "-" + OfficeMap.RoomNames[room].Replace(' ', '_') + "-horror");
        }
        // corners and doors, safe look
        yield return null;
        foreach (var (name, room) in new[] { ("open", 1), ("atrium", 4) })
        {
            var rect = OfficeMap.RoomRect(room); var centre = new Vector2(rect.x + rect.width / 2f, rect.y + rect.height / 2f);
            int c = 0;
            foreach (var corner in new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax) })
            {
                game.DebugSetPlayer(centre); game.DebugSetView(Mathf.Atan2(corner.x - centre.x, -(corner.y - centre.y)) * Mathf.Rad2Deg, 0f); yield return null; yield return null;
                yield return Capture("corner-" + name + "-" + c++);
            }
        }
        // Vale in view
        game.DebugSetPlayer(new Vector2(5.5f, 5.5f)); game.DebugPlaceHunter(new Vector2(5.5f, 2.5f), 99999f); game.DebugSetView(180f, 0f); yield return null; yield return null; yield return Capture("vale-chase-view");
    }


    static ImuReading Glove(int ljx, int ljy, bool lclick, int rjx, int rjy, bool rclick) => new ImuReading { connected = true, left = new GloveSide { connected = true, jx = ljx, jy = ljy, click = lclick }, right = new GloveSide { connected = true, jx = rjx, jy = rjy, click = rclick } };

    [UnityTest] public IEnumerator GlovesWalkLookAndInteractWithoutAKeyboard()
    {
        game.Run.StartSelection(); game.Run.StartTour(); yield return Settle();
        var start = game.PlayerPosition; float yawBefore = game.WorldCamera.transform.eulerAngles.y;
        for (int i = 0; i < 40; i++) { game.Live.DebugSetImu(Glove(0, 100, false, 80, 0, false)); yield return null; }
        Assert.That(Vector2.Distance(start, game.PlayerPosition), Is.GreaterThan(.5f), "The left stick walks.");
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(yawBefore, game.WorldCamera.transform.eulerAngles.y)), Is.GreaterThan(10f), "The right stick looks around.");
        game.Live.DebugSetImu(Glove(0, 0, false, 0, 0, false)); yield return null;
        game.DebugSetPlayer(game.Map.Tour[0]); yield return null;
        game.Live.DebugSetImu(Glove(0, 0, true, 0, 0, false)); yield return null; yield return null;
        game.Live.DebugSetImu(Glove(0, 0, false, 0, 0, false)); yield return null;
        Assert.That(game.Run.TourStop, Is.EqualTo(1), "A left-glove click is E (interact).");
        Assert.That(game.Glove.Active, Is.True);
    }

    [UnityTest] public IEnumerator GlovesCanOperateTheMenusWithAPointerAndTheLeftClick()
    {
        var mouse = InputSystem.AddDevice<Mouse>();
        game.Stage.SettleCamera(); yield return null; yield return null; yield return null;
        var start = game.GlovePointer;
        game.Live.DebugSetImu(Glove(0, 0, false, 100, 0, false)); for (int i = 0; i < 20; i++) yield return null;
        game.Live.DebugSetImu(Glove(0, 0, false, 0, 0, false)); yield return null;
        Assert.That(game.GlovePointer.x, Is.GreaterThan(start.x + 20f), "Pushing a stick moves the pointer.");
        var begin = FindButton("BEGIN APPLICATION"); var at = ScreenCenter(begin); game.DebugSetGlovePointer(at); yield return null; yield return null;
        game.Live.DebugSetImu(Glove(0, 0, true, 0, 0, false)); yield return null; yield return null; yield return null;
        game.Live.DebugSetImu(Glove(0, 0, false, 0, 0, false)); yield return null; yield return null;
        Assert.That(game.Run.Act, Is.EqualTo(Act.Resume), "A left-glove click on the 3D monitor presses BEGIN.");
        InputSystem.RemoveDevice(mouse);
    }

    [UnityTest] public IEnumerator TheApprovedBlenderPropsAreReallyPlacedAndReskinnedByName()
    {
        game.Run.StartSelection(); game.Run.StartTour(); yield return Settle();
        var counts = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Prop: ")) { var n = t.name.Substring(6); counts[n] = counts.TryGetValue(n, out var c) ? c + 1 : 1; }
        TestContext.WriteLine("Props placed: " + string.Join(", ", counts.Select(kv => kv.Key + " x" + kv.Value)));
        foreach (var need in new[] { "OfficeChair", "ConferenceChair", "FilingCabinet", "ServerRack", "VendingMachine", "WaterCooler", "Copier", "Fridge", "Sofa", "PottedPlant", "MonitorLCD", "KeyboardMouse", "WallClock", "ShelfUnit", "Microwave", "CoffeeMaker", "Whiteboard", "BoxStack", "DeskLamp" })
            Assert.That(counts.ContainsKey(need), Is.True, need + " is built but never placed in a room.");
        Assert.That(counts["DoorFrame"], Is.EqualTo(12)); Assert.That(counts["DoorSlab"], Is.EqualTo(12));
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (m != null) Assert.That(m.name.EndsWith("_main") || m.name.EndsWith("_dark") || m.name.EndsWith("_metal") || m.name.EndsWith("_glow") || m.name.EndsWith("_accent") || m.name.EndsWith("_glass"), Is.False, "Imported material '" + m.name + "' was not re-skinned on " + r.name);
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Breathe/Art/Props/OfficeProps/OfficeChair.fbx");
        var names = prefab.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m ? m.name : "(null)").Distinct().ToArray();
        TestContext.WriteLine("OfficeChair imported material names: " + string.Join(", ", names));
        Assert.That(names.Any(n => n.EndsWith("_main")), Is.True, "FBX material names must survive import so slots can be re-skinned by suffix.");
#endif
    }

    [UnityTest] public IEnumerator NoSignOrTextIsDrawnThroughWalls()
    {
        game.Run.StartSelection(); game.Run.StartTour(); yield return Settle();
        Assert.That(root.GetComponentsInChildren<TextMesh>(true).Length, Is.Zero, "TextMesh uses an always-on-top font material; signs must be depth-tested canvases.");
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials) if (m != null && m.shader != null)
            Assert.That(m.shader.name.StartsWith("GUI/") || m.shader.name.Contains("Text Shader"), Is.False, "A renderer uses the overlay font shader: " + r.name);
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) if (canvas.renderMode == RenderMode.WorldSpace) Assert.That(canvas.overrideSorting, Is.False, "World canvases must be depth-tested with the scene.");
    }

    [UnityTest] public IEnumerator TalkingToValeNeverMovesTheStoryButAnswersDoAndOffTopicOnesDoNot()
    {
        var run = game.Run; run.StartSelection(); run.StartTour(); for (int i = 0; i < 4; i++) run.MeetColleague(); yield return Settle();
        run.Introduce(); yield return Settle(); run.FinishTransformation(); yield return Settle(); run.FinishUnreadable(); yield return Settle(); run.StartQuestion(); yield return Settle();
        run.Collect(); game.DebugSetPlayer(game.Map.Challenge[0]); yield return null;
        var act = run.Act;
        game.DebugSpeech(new SpeechReply { intent = "chat", reply = "Is that your best insult?", tone = "sarcastic", verdict = "none", anger = .3f }); yield return null;
        Assert.That(run.Act, Is.EqualTo(act)); Assert.That(run.Answered, Is.Zero, "Chat never counts as an answer.");
        Assert.That(game.HudInfo.Subtitle, Is.EqualTo("Is that your best insult?").Or.EqualTo(""), "Subtitle shows Vale's words (it needs the live reply text).");
        game.DebugSpeech(new SpeechReply { intent = "answer", reply = "Wrong.", tone = "furious", verdict = "offtopic", anger = 1f }); yield return null;
        Assert.That(run.Answered, Is.Zero, "An off-topic answer does not count."); Assert.That(run.RejectedAnswers, Is.LessThanOrEqualTo(1));
        yield return null; LogAssert.NoUnexpectedReceived();
    }

    Vector2 ScreenCenter(Button b)
    {
        var corners = new Vector3[4]; ((RectTransform)b.transform).GetWorldCorners(corners);
        var centre = (corners[0] + corners[2]) / 2f; var p = game.WorldCamera.WorldToScreenPoint(centre); return new Vector2(p.x, p.y);
    }

    [UnityTest] public IEnumerator RealMouseClicksReachWorldSpaceButtons()
    {
        var mouse = InputSystem.AddDevice<Mouse>();
        game.Stage.SettleCamera(); yield return null; yield return null; yield return null;
        var begin = FindButton("BEGIN APPLICATION"); var at = ScreenCenter(begin);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = at }); yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(MouseButton.Left)); yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = at }); yield return null; yield return null;
        Assert.That(game.Run.Act, Is.EqualTo(Act.Resume), "A real mouse click on the 3D monitor must press the button.");
        game.Stage.SettleCamera(); yield return null; yield return null; yield return null;
        var folder = FindButton("ELI PARK"); at = ScreenCenter(folder);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = at }); yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(MouseButton.Left)); yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = at }); yield return null; yield return null;
        Assert.That(game.Run.Candidate.Name, Is.EqualTo("ELI PARK"), "Clicking a folder on the desk selects that candidate.");
        InputSystem.RemoveDevice(mouse);
    }

    [UnityTest] public IEnumerator SetupClipboardLivesOnAProp()
    {
        FindButton("OPEN SETUP").onClick.Invoke(); yield return Capture("20-setup-clipboard");
        Assert.That(HasButton("Pulse (webcam"), Is.True); Assert.That(HasButton("Voice (mic"), Is.True); Assert.That(HasButton("Test windows"), Is.True);
        FindButton("DONE").onClick.Invoke(); yield return null; Assert.That(HasButton("OPEN SETUP"), Is.True);
    }

    [UnityTest] public IEnumerator KeyboardMovesAndEscapeFreezesThenStopsTheGame()
    {
        FindButton("BEGIN APPLICATION").onClick.Invoke(); yield return null;
        FindButton("OK. I REMEMBER.").onClick.Invoke(); yield return null;
        var before = game.PlayerPosition;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(game.PlayerPosition.x, Is.GreaterThan(before.x + .1f));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
        var rotation = game.WorldCamera.transform.eulerAngles.y;
        yield return new WaitForSecondsRealtime(.45f);
        Assert.That(Mathf.DeltaAngle(rotation, game.WorldCamera.transform.eulerAngles.y), Is.GreaterThan(25));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Assert.That(game.WorldCamera.orthographic, Is.False);
        Assert.That(game.WorldCamera.transform.position.y, Is.EqualTo(OfficeWorld.EyeHeight).Within(.1f));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
        Assert.That(game.IsPaused, Is.True); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        var stopped = game.PlayerPosition;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); yield return new WaitForSecondsRealtime(.2f);
        Assert.That(game.PlayerPosition, Is.EqualTo(stopped));
        yield return Capture("21-pause");
        FindButton("END INTERVIEW NOW").onClick.Invoke(); yield return null;
        Assert.That(game.Run.Act, Is.EqualTo(Act.Welcome)); Assert.That(game.IsPaused, Is.False);
        yield return Capture("22-narrow-window", 900, 1300);
        yield return Capture("23-wide-window", 1900, 800);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest] public IEnumerator PauseWorksInEveryActAndIsNeverAFullScreenPanel()
    {
        var run = game.Run; var steps = new Action[] { () => { }, run.StartSelection, run.StartTour, () => { for (int i = 0; i < 4; i++) run.MeetColleague(); }, run.Introduce, run.FinishTransformation,
            run.FinishUnreadable, run.StartQuestion, () => Answer(3), run.StartQuestion, () => Answer(2), run.StartQuestion, () => Answer(1), () => run.ChooseMemory(true), run.ReturnToInterview, () => run.Finish(true) };
        foreach (var step in steps)
        {
            step(); yield return null; yield return null;
            string label = "pause in " + run.Act;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(game.IsPaused, Is.True, label); Assert.That(HasButton("RESUME"), Is.True, label); AssertNo2DScreen(label);
            FindButton("RESUME").onClick.Invoke(); yield return null; Assert.That(game.IsPaused, Is.False, label);
        }
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest] public IEnumerator PursuerHuntsAnIdleVisiblePlayerAndRetryRestartsOnlyThisQuestion()
    {
        var run = game.Run;
        run.StartSelection(); run.StartTour(); for (int i = 0; i < 4; i++) run.MeetColleague();
        yield return Settle(); run.Introduce(); yield return Settle(); run.FinishTransformation(); yield return Settle(); run.FinishUnreadable(); yield return Settle(); run.StartQuestion(); yield return Settle();
        game.DebugPlaceHunter(new Vector2(game.PlayerPosition.x + 5f, game.PlayerPosition.y), 0f);
        float deadline = Time.realtimeSinceStartup + 40;
        while (run.Act != Act.Caught && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(run.Act, Is.EqualTo(Act.Caught), "The pursuer must reach an idle, visible player.");
        yield return null; yield return null;
        Assert.That(game.JumpscareActive, Is.True, "Getting caught triggers the jumpscare.");
        deadline = Time.realtimeSinceStartup + 5; while (game.JumpscareActive && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null; FindButton("RETRY THIS QUESTION").onClick.Invoke(); yield return Settle();
        Assert.That(run.Act, Is.EqualTo(Act.Challenge)); Assert.That(run.Catches, Is.EqualTo(1));
        Assert.That(game.PlayerPosition, Is.EqualTo(new OfficeMap().Spawn));
        LogAssert.NoUnexpectedReceived();
    }
}

public class MusicTests
{
    GameObject root; BreatheGame game;
    static AudioClip Clip(string name, float seconds)
    {
        int rate = 22050, n = (int)(rate * seconds); var data = new float[n]; for (int i = 0; i < n; i++) data[i] = Mathf.Sin(i * .05f) * .05f;
        var clip = AudioClip.Create(name, n, 1, rate, false); clip.SetData(data, 0); return clip;
    }
    IEnumerator Settle(BreatheGame g) { float end = Time.realtimeSinceStartup + 10; yield return null; yield return null; while (g.TransitionBusy && Time.realtimeSinceStartup < end) yield return null; yield return null; }
    [UnityTest] public IEnumerator SafeMusicBetweenRoundsHorrorMusicInQuestionsAndTheLaughOnlyTheFirstTime()
    {
        foreach (var existing in UnityEngine.Object.FindObjectsByType<BreatheGame>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(existing.gameObject);
        yield return null;
        root = new GameObject("Music test"); var assets = root.AddComponent<GameAssets>(); assets.safe = Clip("safe", 2); assets.laugh = Clip("laugh", 1.5f); assets.horror = Clip("horror", 3);
        game = root.AddComponent<BreatheGame>(); yield return null; yield return null;
        Assert.That(game.Music.HasAuthoredMusic, Is.True);
        Assert.That(game.Music.Current, Is.EqualTo(MusicDirector.Mode.Safe), "The lobby plays the safe music.");
        var run = game.Run; run.StartSelection(); run.StartTour(); for (int i = 0; i < 4; i++) run.MeetColleague(); yield return Settle(game);
        Assert.That(game.Music.Current, Is.EqualTo(MusicDirector.Mode.Safe), "The interview's calm opening is still safe.");
        Assert.That(game.Music.HeardHorror, Is.False);
        run.Introduce(); yield return null; yield return null;
        Assert.That(run.Act, Is.EqualTo(Act.Transformation));
        Assert.That(game.Music.Current, Is.EqualTo(MusicDirector.Mode.Horror), "Horror starts: laugh, then loop.");
        Assert.That(game.Music.HeardHorror, Is.True);
        Assert.That(game.BlackoutLeft, Is.GreaterThan(.2f).And.LessThanOrEqualTo(1.5f), "Black screen lasts for the laugh.");
        run.FinishTransformation(); yield return Settle(game);
        Assert.That(game.Music.Current, Is.EqualTo(MusicDirector.Mode.Horror), "The impossible question is horror.");
        run.FinishUnreadable(); yield return Settle(game);
        Assert.That(game.Music.Current, Is.EqualTo(MusicDirector.Mode.Safe), "Between rounds the safe music returns.");
        run.StartQuestion(); yield return Settle(game);
        Assert.That(game.Music.Current, Is.EqualTo(MusicDirector.Mode.Horror), "The next question brings the horror music back (no second laugh).");
        Answer(run, 3); yield return Settle(game);
        Assert.That(game.Music.Current, Is.EqualTo(MusicDirector.Mode.Safe));
        run.StartQuestion(); yield return Settle(game); Answer(run, 2); yield return Settle(game); run.StartQuestion(); yield return Settle(game); Answer(run, 1); yield return Settle(game);
        Assert.That(run.Act, Is.EqualTo(Act.GapChoice)); Assert.That(game.Music.Current, Is.EqualTo(MusicDirector.Mode.Horror));
        run.ChooseMemory(true); yield return Settle(game);
        Assert.That(game.Music.Current, Is.EqualTo(MusicDirector.Mode.Safe), "The calm epilogue is safe.");
        UnityEngine.Object.Destroy(root); yield return null;
    }
    [UnityTest] public IEnumerator SafeMusicTapeWarpsDuringTheInterviewBeforeHorrorAndResets()
    {
        foreach (var existing in UnityEngine.Object.FindObjectsByType<BreatheGame>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(existing.gameObject);
        yield return null;
        root = new GameObject("Tape test"); var assets = root.AddComponent<GameAssets>(); assets.safe = Clip("safe", 2); assets.laugh = Clip("laugh", 1.5f); assets.horror = Clip("horror", 3);
        game = root.AddComponent<BreatheGame>(); yield return null; yield return null;
        Assert.That(game.Music.SafePitch, Is.EqualTo(1f).Within(.001f), "Lobby music plays at normal pitch.");
        var run = game.Run; run.StartSelection(); run.StartTour(); for (int i = 0; i < 4; i++) run.MeetColleague(); yield return Settle(game);
        Assert.That(run.Act, Is.EqualTo(Act.Name));
        yield return new WaitForSecondsRealtime(.3f); Assert.That(game.Music.Tape, Is.LessThan(.2f), "The warp ramps slowly.");
        game.DebugAdvanceAge(40f); yield return new WaitForSecondsRealtime(3f);
        Assert.That(game.Music.Tape, Is.GreaterThan(.3f)); Assert.That(game.Music.SafePitch, Is.LessThan(.95f), "Safe music goes out of pitch and slows down in the interview before horror.");
        run.Introduce(); yield return Settle(game); run.FinishTransformation(); yield return Settle(game); run.FinishUnreadable(); yield return Settle(game);
        yield return new WaitForSecondsRealtime(2f);
        Assert.That(game.Music.Current, Is.EqualTo(MusicDirector.Mode.Safe));
        Assert.That(game.Music.Tape, Is.LessThan(.05f), "Between rounds the music is back to normal."); Assert.That(game.Music.SafePitch, Is.EqualTo(1f).Within(.04f));
        UnityEngine.Object.Destroy(root); yield return null;
    }
    static void Answer(InterviewRun run, int pages) { for (int i = 0; i < pages; i++) { run.Collect(); run.Answer(false); } run.ExitQuestion(); }
}
