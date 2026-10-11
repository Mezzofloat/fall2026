using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using U = Breathe.TerminalUI;

namespace Breathe
{
    /// <summary>Everything the first-person HUD shows, gathered by the game each frame.</summary>
    public sealed class HudData
    {
        public bool Tour;
        public int Question, Required, Collected, Answered;
        public PageState[] Pages = new PageState[0];
        public string Title = "", Room = "", Prompt = "", Subtitle = "", SubtitleSpeaker = "MR. VALE", Source = "", Chips = "", Mode = "";
        public float DistanceMeters, BearingDegrees, Stress, MicLevel, SpeechAnger, FallbackProgress, InteractProgress;
        public bool Recording, VoiceReady, AtStation, Gloves, ExitReady, Busy;
        public Vector2 Player; public float Yaw;
        public Vector2[] Objectives = new Vector2[0]; public Vector2 Exit;
        public bool ValeVisible; public Vector2 Vale; public bool ValeHeard;
    }

    /// <summary>
    /// Transparent first-person overlay (never an opaque screen): quest tracker with page status, mini map (top right), recording indicator,
    /// stress meter, clear answer instructions with key caps, and Vale's subtitles. Pixel fonts, hard edges, no gradients.
    /// </summary>
    public sealed class Hud
    {
        readonly TerminalUI ui; readonly OfficeMap map;
        Text title, subtitle, roomLine, prompt, promptKeys, source, chips, stressLabel, recLabel, subtitleName, mapRoom;
        readonly List<Image> pageBox = new List<Image>(); readonly List<Text> pageText = new List<Text>(); readonly List<Text> pageState = new List<Text>();
        Image stressBar, recDot, arrow, promptProgress; readonly List<Image> recBars = new List<Image>();
        RawImage miniStatic, miniDynamic; Texture2D miniStaticTex, miniDynamicTex; Color32[] dynamicPixels; int mw, mh; float miniClock;
        GameObject recGroup, subtitleGroup, promptGroup; float stressWidth;
        static Sprite triangle;

        public Hud(TerminalUI ui, OfficeMap map) { this.ui = ui; this.map = map; }

        static Sprite Triangle()
        {
            if (triangle) return triangle;
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color[1024]; for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) px[y * 32 + x] = (Mathf.Abs(x - 15.5f) < (31 - y) * .5f + .5f && y > 1) ? Color.white : Color.clear;
            t.SetPixels(px); t.Apply(); triangle = Sprite.Create(t, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f)); return triangle;
        }

        static readonly Color Panel = new Color(.04f, .045f, .07f, .78f), Edge = U.Hex("5C6A8C"), Gold = U.Hex("FFD24A"), Teal = U.Hex("5CE0A0"), RedC = U.Hex("FF4A4A"), Dim = U.Hex("8FA0B8"), Ice = U.Hex("DDE8FF");

        void Frame(float x, float y, float w, float h, Color? edge = null)
        {
            var c = edge ?? Edge; ui.Box(x + 5, y + 5, w, h, new Color(0, 0, 0, .5f), null, "Shadow"); ui.Box(x, y, w, h, Panel, null, "Panel");
            ui.Box(x, y, w, 3, c, null, "Edge"); ui.Box(x, y + h - 3, w, 3, c * .6f, null, "Edge"); ui.Box(x, y, 3, h, c, null, "Edge"); ui.Box(x + w - 3, y, 3, h, c * .6f, null, "Edge");
        }

        bool minimal; readonly List<Image> roundBox = new List<Image>(); readonly List<Text> roundText = new List<Text>();
        public void Build(bool tour, int required, bool minimal = false)
        {
            this.minimal = minimal;
            pageBox.Clear(); pageText.Clear(); pageState.Clear(); recBars.Clear(); roundBox.Clear(); roundText.Clear();
            if (minimal) { BuildMinimal(); return; }
            // ---- quest tracker (top left)
            float w = 560, h = tour ? 150 : 212;
            Frame(24, 22, w, h);
            title = ui.Label("", 44, 32, w - 40, 28, 15, Gold, true);
            roomLine = ui.Label("", 44, 62, w - 40, 54, 22, Ice, false);
            if (!tour)
            {
                for (int i = 0; i < required; i++)
                {
                    float x = 44 + i * 170;
                    pageBox.Add(ui.Box(x, 118, 160, 68, Panel * 1.2f, null, "Page box"));
                    pageText.Add(ui.Label("PAGE " + (i + 1), x + 8, 122, 144, 20, 12, Dim, true));
                    pageState.Add(ui.Label("", x + 8, 144, 148, 36, 20, Ice, true));
                }
            }
            arrow = ui.Box(w - 70, 70, 1, 1, Color.clear, null, "Arrow");
            // ---- mini map (top right)
            const int scale = 3; var staticPixels = map.MiniMapStatic(out mw, out mh);
            float mapW = mw * 2.7f, mapH = mh * 2.7f, mx = 1600 - 24 - mapW, my = 22;
            Frame(mx - 6, my - 6, mapW + 12, mapH + 40);
            miniStaticTex = new Texture2D(mw, mh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp }; miniStaticTex.SetPixelData(staticPixels, 0); miniStaticTex.Apply();
            miniDynamicTex = new Texture2D(mw, mh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp }; dynamicPixels = new Color32[mw * mh];
            miniStatic = RawImg("Map", miniStaticTex, mx, my, mapW, mapH); miniDynamic = RawImg("Map markers", miniDynamicTex, mx, my, mapW, mapH);
            mapRoom = ui.Label("", mx, my + mapH + 6, mapW, 26, 14, Gold, true, null, TextAnchor.UpperCenter);
            _ = scale;
            // ---- stress
            float sy = my + mapH + 50; stressWidth = mapW - 16;
            Frame(mx - 6, sy, mapW + 12, 78);
            stressLabel = ui.Label("STRESS", mx + 8, sy + 8, 200, 20, 12, Dim, true);
            ui.Box(mx + 8, sy + 32, mapW - 16, 12, new Color(.15f, .18f, .26f, .95f), null, "Stress track"); stressBar = ui.Box(mx + 8, sy + 32, 0, 12, RedC, null, "Stress bar");
            source = ui.Label("", mx + 8, sy + 48, mapW - 16, 26, 17, Dim, false);
            // ---- recording indicator (top centre)
            recGroup = new GameObject("REC group", typeof(RectTransform)); var rg = recGroup.GetComponent<RectTransform>(); rg.SetParent(ui.Root, false); rg.anchorMin = rg.anchorMax = rg.pivot = new Vector2(0, 1); rg.anchoredPosition = Vector2.zero; rg.sizeDelta = new Vector2(1600, 900);
            RecFrame(rg);
            // ---- subtitles
            subtitleGroup = new GameObject("Subtitle group", typeof(RectTransform)); var sg = subtitleGroup.GetComponent<RectTransform>(); sg.SetParent(ui.Root, false); sg.anchorMin = sg.anchorMax = sg.pivot = new Vector2(0, 1); sg.anchoredPosition = Vector2.zero; sg.sizeDelta = new Vector2(1600, 900);
            Box(sg, 280, 650, 1040, 82, Panel); Box(sg, 280, 650, 1040, 3, RedC);
            subtitleName = ui.Label("MR. VALE", 296, 658, 300, 22, 13, RedC, true, sg); subtitle = ui.Label("", 296, 682, 1010, 48, 22, Ice, false, sg);
            // ---- prompt with key caps
            promptGroup = new GameObject("Prompt group", typeof(RectTransform)); var pg = promptGroup.GetComponent<RectTransform>(); pg.SetParent(ui.Root, false); pg.anchorMin = pg.anchorMax = pg.pivot = new Vector2(0, 1); pg.anchoredPosition = Vector2.zero; pg.sizeDelta = new Vector2(1600, 900);
            Box(pg, 280, 748, 1040, 118, Panel); Box(pg, 280, 748, 1040, 3, Gold);
            prompt = ui.Label("", 300, 760, 1000, 62, 24, Ice, false, pg); promptKeys = ui.Label("", 300, 826, 1000, 30, 16, Dim, false, pg);
            promptProgress = ui.Box(280, 862, 0, 4, Teal, pg, "Prompt progress");
            chips = ui.Label("", 24, 872, 1552, 22, 13, Dim, false, null, TextAnchor.UpperRight);
            ui.Box(797, 444, 6, 12, U.Paper); ui.Box(794, 447, 12, 6, U.Paper);
        }

        /// <summary>Interview-room overlay: where you are in the five questions, the recording indicator, Vale's subtitles and the answer instructions.</summary>
        void BuildMinimal()
        {
            Frame(24, 22, 330, 78);
            title = ui.Label("", 38, 28, 300, 20, 11, Gold, true);
            for (int i = 0; i < 5; i++)
            {
                roundBox.Add(ui.Box(38 + i * 60, 56, 52, 34, Panel * 1.2f, null, "Round box")); roundText.Add(ui.Label((i + 1).ToString(), 38 + i * 60, 56, 52, 34, 20, Dim, true, null, TextAnchor.MiddleCenter));
            }
            recGroup = new GameObject("REC group", typeof(RectTransform)); var rg = recGroup.GetComponent<RectTransform>(); rg.SetParent(ui.Root, false); rg.anchorMin = rg.anchorMax = rg.pivot = new Vector2(0, 1); rg.anchoredPosition = Vector2.zero; rg.sizeDelta = new Vector2(1600, 900);
            RecFrame(rg);
            subtitleGroup = new GameObject("Subtitle group", typeof(RectTransform)); var sg = subtitleGroup.GetComponent<RectTransform>(); sg.SetParent(ui.Root, false); sg.anchorMin = sg.anchorMax = sg.pivot = new Vector2(0, 1); sg.anchoredPosition = Vector2.zero; sg.sizeDelta = new Vector2(1600, 900);
            Box(sg, 280, 650, 1040, 82, Panel); Box(sg, 280, 650, 1040, 3, RedC);
            subtitleName = ui.Label("MR. VALE", 296, 658, 300, 22, 13, RedC, true, sg); subtitle = ui.Label("", 296, 682, 1010, 48, 22, Ice, false, sg);
            promptGroup = new GameObject("Prompt group", typeof(RectTransform)); var pg = promptGroup.GetComponent<RectTransform>(); pg.SetParent(ui.Root, false); pg.anchorMin = pg.anchorMax = pg.pivot = new Vector2(0, 1); pg.anchoredPosition = Vector2.zero; pg.sizeDelta = new Vector2(1600, 900);
            Box(pg, 280, 748, 1040, 118, Panel); Box(pg, 280, 748, 1040, 3, Gold);
            prompt = ui.Label("", 300, 760, 1000, 62, 24, Ice, false, pg); promptKeys = ui.Label("", 300, 826, 1000, 30, 16, Dim, false, pg);
            promptProgress = ui.Box(280, 862, 0, 4, Teal, pg, "Prompt progress");
            chips = ui.Label("", 24, 872, 1552, 22, 13, Dim, false, null, TextAnchor.UpperRight);
        }

        void Box(Transform parent, float x, float y, float w, float h, Color c) { ui.Box(x, y, w, h, c, parent, "Panel"); }

        void RecFrame(RectTransform parent)
        {
            Box(parent, 650, 22, 300, 52, Panel); Box(parent, 650, 22, 300, 3, RedC);
            recDot = ui.Circle(662, 34, 28, RedC, parent); recLabel = ui.Label("RECORDING", 698, 28, 170, 24, 20, RedC, true, parent);
            ui.Label("release V to send", 698, 52, 200, 18, 12, Dim, false, parent);
            for (int i = 0; i < 8; i++) recBars.Add(ui.Box(880 + i * 8, 62, 5, 4, RedC, parent, "Level"));
        }

        RawImage RawImg(string name, Texture2D texture, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage)); var rt = go.GetComponent<RectTransform>(); U.Position(go, ui.Root, x, y, w, h);
            var raw = go.GetComponent<RawImage>(); raw.texture = texture; raw.raycastTarget = false; return raw;
        }

        // ------------------------------------------------------------------ frame update
        static string PageWord(PageState s) => s == PageState.Done ? "DONE" : s == PageState.Answer ? "ANSWER" : s == PageState.Find ? "FIND IT" : "LOCKED";
        static Color PageColor(PageState s) => s == PageState.Done ? Teal : s == PageState.Answer ? RedC : s == PageState.Find ? Gold : Dim;

        public void Update(HudData d, float dt)
        {
            if (title == null) return;
            if (minimal)
            {
                title.text = d.Question > 0 && d.Question <= 5 ? "QUESTION " + d.Question + " OF 5   " + d.Title : d.Title;
                for (int i = 0; i < roundBox.Count; i++)
                {
                    bool done = i + 1 < d.Question, now = i + 1 == d.Question; var col = done ? Teal : now ? Gold : Dim;
                    roundBox[i].color = now ? new Color(.35f, .27f, .05f, .95f) : done ? new Color(.05f, .25f, .18f, .95f) : Panel * 1.2f; roundText[i].color = col; roundText[i].text = done ? "x" : (i + 1).ToString();
                }
                RecAndText(d); return;
            }
            title.text = d.Tour ? "ORIENTATION   /   FLOOR 01" : "QUESTION " + d.Question.ToString("00") + " / 05   " + d.Title;
            roomLine.text = (d.Room.Length > 0 ? "NOW IN:  " + d.Room + "\n" : "") + (d.DistanceMeters > 0 ? Mathf.CeilToInt(d.DistanceMeters) + " m  " + Direction(d.BearingDegrees) : "");
            
            for (int i = 0; i < pageBox.Count; i++)
            {
                var state = i < d.Pages.Length ? d.Pages[i] : PageState.Locked; var col = PageColor(state);
                pageBox[i].color = state == PageState.Locked ? Panel * 1.2f : col * new Color(.25f, .25f, .25f, .95f);
                pageText[i].color = col; pageState[i].color = col;
                pageState[i].text = (state == PageState.Done ? "[x] " : state == PageState.Answer ? "[!] " : state == PageState.Find ? "[>] " : "[ ] ") + PageWord(state);
            }
            mapRoom.text = d.Room;
            stressBar.rectTransform.sizeDelta = new Vector2(stressWidth * Mathf.Clamp01(d.Stress), 12);
            stressBar.color = Color.Lerp(Teal, RedC, Mathf.Clamp01(d.Stress * 1.4f - .2f));
            source.text = d.Source;
            RecAndText(d);
            miniClock -= dt; if (miniClock <= 0) { miniClock = .08f; PaintMap(d); }
        }

        void RecAndText(HudData d)
        {
            // recording indicator: a slow pulse (1 Hz), never a strobe
            recGroup.SetActive(d.Recording);
            if (d.Recording)
            {
                recDot.color = Color.Lerp(new Color(.4f, 0, 0), RedC, .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f));
                for (int i = 0; i < recBars.Count; i++) { float on = Mathf.Clamp01(d.MicLevel * 1.6f * 8f - i); recBars[i].rectTransform.sizeDelta = new Vector2(5, 4 + on * 24); recBars[i].rectTransform.anchoredPosition = new Vector2(recBars[i].rectTransform.anchoredPosition.x, -(76 - 4 - on * 24)); }
            }
            subtitleGroup.SetActive(!string.IsNullOrWhiteSpace(d.Subtitle));
            if (subtitleGroup.activeSelf) { subtitle.text = d.Subtitle; subtitleName.text = d.SubtitleSpeaker; subtitle.color = d.SpeechAnger > .6f ? U.Hex("FFB0A8") : Ice; }
            prompt.text = d.Prompt; promptKeys.text = d.Mode;
            promptProgress.rectTransform.sizeDelta = new Vector2(1040 * Mathf.Clamp01(Mathf.Max(d.FallbackProgress, d.InteractProgress)), 4);
            chips.text = d.Chips;
        }

        static string Direction(float bearing) { float a = Mathf.Abs(bearing); return a < 25 ? "AHEAD" : a > 145 ? "BEHIND YOU" : bearing > 0 ? "TO YOUR RIGHT" : "TO YOUR LEFT"; }

        void Cell(Vector2 p, Color32 c, int radius)
        {
            int cx = Mathf.FloorToInt(p.x * OfficeMap.MiniScale), cy = Mathf.FloorToInt(p.y * OfficeMap.MiniScale);
            for (int a = -radius; a <= radius; a++) for (int b = -radius; b <= radius; b++) { int x = cx + a, y = cy + b; if (x >= 0 && y >= 0 && x < mw && y < mh) dynamicPixels[y * mw + x] = c; }
        }

        void PaintMap(HudData d)
        {
            System.Array.Clear(dynamicPixels, 0, dynamicPixels.Length);
            foreach (var door in map.Doors)
            {
                var c = door.Open > .5f ? new Color32(90, 255, 140, 255) : new Color32(255, 170, 40, 255);
                int x0 = door.Cell.x * OfficeMap.MiniScale, y0 = door.Cell.y * OfficeMap.MiniScale;
                for (int a = 0; a < OfficeMap.MiniScale; a++) for (int b = 0; b < OfficeMap.MiniScale; b++) dynamicPixels[(y0 + b) * mw + x0 + a] = c;
            }
            // the room you are in is outlined
            var room = OfficeMap.RoomRect(OfficeMap.RoomIndex(d.Player)); var line = new Color32(255, 255, 255, 70);
            for (int x = (int)room.xMin * OfficeMap.MiniScale; x < (int)room.xMax * OfficeMap.MiniScale; x++) { dynamicPixels[((int)room.yMin * OfficeMap.MiniScale) * mw + x] = line; dynamicPixels[Mathf.Min(mh - 1, (int)room.yMax * OfficeMap.MiniScale) * mw + x] = line; }
            bool blink = Mathf.Repeat(Time.unscaledTime, 1f) < .6f;
            for (int i = 0; i < d.Objectives.Length; i++) if (i > 0 || blink) Cell(d.Objectives[i], i == 0 ? new Color32(255, 220, 60, 255) : new Color32(130, 120, 80, 255), i == 0 ? 2 : 1);
            if (d.ExitReady) Cell(d.Exit, new Color32(90, 255, 160, 255), 2);
            if (d.ValeVisible) Cell(d.Vale, new Color32(255, 40, 40, 255), 2); else if (d.ValeHeard && blink) Cell(d.Vale, new Color32(255, 80, 80, 120), 3);
            // you: a bright dot with a short view line
            float yaw = d.Yaw * Mathf.Deg2Rad; Vector2 dir = new Vector2(Mathf.Sin(yaw), -Mathf.Cos(yaw));   // yaw 0 looks toward decreasing map y
            for (int s = 1; s <= 7; s++) { var side = new Vector2(-dir.y, dir.x); float spread = s * .09f; Cell(d.Player + dir * s * .5f, new Color32(255, 255, 255, 230), 0); Cell(d.Player + dir * s * .5f + side * spread, new Color32(120, 220, 255, 140), 0); Cell(d.Player + dir * s * .5f - side * spread, new Color32(120, 220, 255, 140), 0); }
            Cell(d.Player, new Color32(80, 200, 255, 255), 2);
            miniDynamicTex.SetPixelData(dynamicPixels, 0); miniDynamicTex.Apply(false);
        }

        public void Dispose() { if (miniStaticTex) Object.Destroy(miniStaticTex); if (miniDynamicTex) Object.Destroy(miniDynamicTex); }
    }
}
