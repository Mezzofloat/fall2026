using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Breathe
{
    /// <summary>A world-space canvas mounted on a prop. All menu text and buttons live on these, never on a flat full-screen layer.</summary>
    public sealed class Surface
    {
        public readonly GameObject Holder;
        public readonly RectTransform Root;
        public readonly float PixelWidth, PixelHeight;
        public Surface(GameObject holder, RectTransform root, float w, float h) { Holder = holder; Root = root; PixelWidth = w; PixelHeight = h; }
        public void Clear() { for (int i = Root.childCount - 1; i >= 0; i--) { var child = Root.GetChild(i).gameObject; child.SetActive(false); Object.Destroy(child); } }
        public void SetActive(bool value) { Holder.SetActive(value); }
    }

    public enum Place { Lobby, Interview }
    public enum LobbyView { Front, Folders, Setup }

    /// <summary>The Continuity lobby (the main menu) and the interview room. Both are lit, animated 3D spaces with prop-mounted UI.</summary>
    public sealed class StageWorld
    {
        public readonly Camera Camera;
        public readonly GameObject Root;
        public Surface Monitor, Clipboard, Notice, Sign, DoorPlate, FolderTray;
        public Surface Wall, Blotter, Cards;
        public GameObject ValeActor { get; private set; }
        public KnifeProp Knife { get; private set; }
        public Place Current { get; private set; } = Place.Lobby;
        public LobbyView View { get; private set; } = LobbyView.Front;
        public float LobbyClock { get; private set; }

        static readonly Vector3 LobbyOrigin = new Vector3(80, 0, 0), RoomOrigin = new Vector3(120, 0, 0);
        readonly GameObject lobby, room;
        readonly List<Material> materials = new List<Material>();
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly List<Material> lamps = new List<Material>();
        readonly List<Material> roomPaint = new List<Material>();
        readonly List<Transform> dust = new List<Transform>();
        readonly List<Vector3> dustVelocity = new List<Vector3>();
        readonly List<Transform> billboards = new List<Transform>();
        Material roomWall, roomFloor, roomCeiling, roomDesk, roomLamp, glassMaterial, shadeMaterial;
        Transform silhouette; ActorSprite valeActor;
        public ActorSprite Vale => valeActor;
        readonly List<Light> stageLights = new List<Light>();
        readonly Theme theme = new Theme();
        Furnisher furnisher;
        Renderer shade;
        Vector3 targetPosition; float targetYaw, targetPitch; Vector3 position; float yaw, pitch;
        bool horror; float shadeAlpha; bool snap = true;
        readonly TerminalUI kit = TerminalUI.Kit();
        readonly Dictionary<string, GameObject> props = new Dictionary<string, GameObject>();
        readonly List<(Material material, string normal, string horror)> roomLit = new List<(Material, string, string)>();
        bool useKit;

        public StageWorld(Transform parent, Camera camera, GameAssets assets)
        {
            Camera = camera; Root = new GameObject("Stage - lobby and interview room"); Root.transform.SetParent(parent, false);
            lobby = new GameObject("Lobby"); lobby.transform.SetParent(Root.transform, false);
            room = new GameObject("Interview room"); room.transform.SetParent(Root.transform, false);
            if (assets && assets.kitParts != null) foreach (var part in assets.kitParts) if (part) props[part.name] = part;
            useKit = props.Count >= 16; furnisher = new Furnisher(theme, new PropFactory(assets));
            BuildLobby(); BuildRoom(assets);
            // Black fade quad parented to the camera (a 3D object, used for the blackout while the laugh plays).
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "Blackout"; Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(camera.transform, false); quad.transform.localPosition = new Vector3(0, 0, .12f); quad.transform.localScale = new Vector3(1.4f, .9f, 1);
            shadeMaterial = Glass(new Color(0, 0, 0, 0)); quad.GetComponent<Renderer>().sharedMaterial = shadeMaterial; shade = quad.GetComponent<Renderer>(); shade.enabled = false;
            Show(Place.Lobby, false);
        }

        // ------------------------------------------------------------------ materials and primitives
        Material Mat(string hex, Texture2D texture = null, Vector2? tiling = null)
        {
            var template = Resources.Load<Material>("OfficeSurface");
            var m = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", TerminalUI.Hex(hex));
            if (texture) { m.SetTexture("_BaseMap", texture); m.mainTexture = texture; if (tiling.HasValue) m.mainTextureScale = tiling.Value; }
            materials.Add(m); return m;
        }
        Material Glass(Color color)
        {
            var template = Resources.Load<Material>("OfficeGlass") ?? Resources.Load<Material>("OfficeSurface");
            var m = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", color); materials.Add(m); return m;
        }
        Texture2D Checker(string a, string b)
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var px = new Color[64 * 64]; Color ca = TerminalUI.Hex(a), cb = TerminalUI.Hex(b);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) px[y * 64 + x] = ((x / 32 + y / 32) % 2 == 0 ? ca : cb) * (.9f + ((x * 7 + y * 13) % 9) * .011f);
            t.SetPixels(px); t.Apply(); textures.Add(t); return t;
        }
        Texture2D Grime(string baseColor)
        {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var px = new Color[32 * 32]; var c = TerminalUI.Hex(baseColor); var rng = new System.Random(3);
            for (int i = 0; i < px.Length; i++) { float n = .86f + (float)rng.NextDouble() * .14f; if (rng.Next(40) == 0) n *= .7f; px[i] = c * n; px[i].a = 1; }
            t.SetPixels(px); t.Apply(); textures.Add(t); return t;
        }
        GameObject Cube(Transform parent, string name, Vector3 center, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = m; return go;
        }
        GameObject Billboard(Transform parent, string name, Vector3 center, Vector2 size, Texture2D texture)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name; Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localScale = new Vector3(size.x, size.y, 1);
            var template = Resources.Load<Material>("OfficeSprite") ?? Resources.Load<Material>("OfficeSurface");
            var m = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", texture); m.mainTexture = texture; m.SetFloat("_AlphaClip", 1); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0); m.renderQueue = (int)RenderQueue.AlphaTest;
            materials.Add(m); go.GetComponent<Renderer>().sharedMaterial = m; return go;
        }
        Material Lit(string hex, float metal = 0f, float smooth = .2f)
        {
            var template = Resources.Load<Material>("OfficeLit") ?? Resources.Load<Material>("OfficeSurface");
            var m = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", TerminalUI.Hex(hex)); if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            materials.Add(m); return m;
        }

        /// <summary>Instantiate an approved kit part under a placement pivot. The FBX root keeps the layout offset, so it is zeroed here; rotation/scale carry the axis and unit fix.</summary>
        Transform PlaceProp(Transform parent, string part, Vector3 position, float yaw, params Material[] mats)
        {
            var pivot = new GameObject("Prop: " + part).transform; pivot.SetParent(parent, false); pivot.localPosition = position; pivot.localRotation = Quaternion.Euler(0, yaw, 0);
            var instance = Object.Instantiate(props[part], pivot, false); instance.transform.localPosition = Vector3.zero;
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                var slots = r.sharedMaterials; var assigned = new Material[slots.Length];
                for (int i = 0; i < slots.Length; i++) assigned[i] = mats[Mathf.Min(i, mats.Length - 1)];
                r.sharedMaterials = assigned;
            }
            return pivot;
        }

        public Surface MakeSurface(Transform parent, string name, Vector3 center, Vector3 euler, float widthMeters, float heightMeters, int pixelWidth, string frameHex = "1b1d24", float frameThickness = .05f, bool backing = true)
        {
            float pixelHeight = pixelWidth * heightMeters / widthMeters;
            var holder = new GameObject("Surface: " + name); holder.transform.SetParent(parent, false);
            holder.transform.localPosition = center; holder.transform.localRotation = Quaternion.Euler(euler);
            // Backing panel just behind the canvas so it reads as a physical object.
            if (backing) Cube(holder.transform, "Frame", new Vector3(0, 0, frameThickness / 2 + .003f), new Vector3(widthMeters + .08f, heightMeters + .08f, frameThickness), Mat(frameHex));
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(holder.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera;
            var rt = canvasObject.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(pixelWidth, pixelHeight); rt.localScale = Vector3.one * (widthMeters / pixelWidth);
            rt.localPosition = Vector3.zero;
            return new Surface(holder, rt, pixelWidth, pixelHeight);
        }
        public TerminalUI Kit => kit;

        // ------------------------------------------------------------------ lobby (the main menu)
        void BuildLobby()
        {
            var t = lobby.transform; t.position = LobbyOrigin;
            // Sleek corporate lobby in SAFE (white and navy panelling, marble floor); the same room weathered and moldy in HORROR.
            var tone = TerminalUI.Hex("5A2A22");
            Texture2D WallTex(bool h) => ProcTex.Wall(WallKind.Panels, TerminalUI.Hex("EDEBE2"), TerminalUI.Hex("2B4A7A"), TerminalUI.Hex("D0B070"), h, tone, 501, theme.Textures);
            var wallS = WallTex(false); var wallH = WallTex(true);
            var wallM = theme.Lit("Lobby wall", "FFFFFF", "FFFFFF", .12f, .04f, wallS, wallH, new Vector2(6, 1)); var wallSide = theme.Lit("Lobby side wall", "FFFFFF", "FFFFFF", .12f, .04f, wallS, wallH, new Vector2(7, 1));
            var floorM = theme.Lit("Lobby floor", "FFFFFF", "FFFFFF", .65f, .08f, ProcTex.Floor(FloorKind.Marble, TerminalUI.Hex("E8E4DA"), TerminalUI.Hex("A9A394"), false, tone, 502, theme.Textures), ProcTex.Floor(FloorKind.Marble, TerminalUI.Hex("E8E4DA"), TerminalUI.Hex("A9A394"), true, tone, 502, theme.Textures), new Vector2(6, 7));
            var ceilM = theme.Lit("Lobby ceiling", "FFFFFF", "FFFFFF", .1f, .02f, ProcTex.Ceiling(false, 503, theme.Textures), ProcTex.Ceiling(true, 503, theme.Textures), new Vector2(6, 7));
            var woodM = Mat("7A5334", Grime("7A5334"), new Vector2(3, 1)); var trimM = Mat("4B4E58");
            Cube(t, "Floor", new Vector3(0, -.05f, 7), new Vector3(12, .1f, 14), floorM);
            Cube(t, "Ceiling", new Vector3(0, 3.45f, 7), new Vector3(12, .1f, 14), ceilM);
            Cube(t, "Back wall", new Vector3(0, 1.7f, 14.05f), new Vector3(12, 3.5f, .1f), wallM);
            Cube(t, "Front wall", new Vector3(0, 1.7f, -.55f), new Vector3(12, 3.5f, .1f), wallM);
            Cube(t, "Left wall", new Vector3(-6.05f, 1.7f, 7), new Vector3(.1f, 3.5f, 14), wallSide);
            Cube(t, "Right wall", new Vector3(6.05f, 1.7f, 7), new Vector3(.1f, 3.5f, 14), wallSide);
            Cube(t, "Back baseboard", new Vector3(0, .1f, 13.97f), new Vector3(12, .2f, .05f), trimM);
            for (int x = -4; x <= 4; x += 4) for (int z = 3; z <= 12; z += 4)
            {
                var lamp = Mat("FFF9D6"); lamps.Add(lamp);
                if (useKit) { PlaceProp(t, "LightHousing", new Vector3(x, 3.365f, z), 0, Lit("C9C9C0")); PlaceProp(t, "LightTube", new Vector3(x, 3.365f, z), 0, lamp); }
                else Cube(t, "Fluorescent tube", new Vector3(x, 3.38f, z), new Vector3(1.8f, .04f, .3f), lamp);
            }
            // Soft light shafts (alpha fades toward the floor) away from the screens.
            var fade = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var fp = new Color[4 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 4; x++) fp[y * 4 + x] = new Color(1f, .96f, .75f, Mathf.Pow(y / 63f, 1.6f) * (1f - Mathf.Abs(x - 1.5f) / 2.2f));
            fade.SetPixels(fp); fade.Apply(); textures.Add(fade);
            var shaftMat = Glass(new Color(1f, .96f, .75f, .16f)); shaftMat.SetTexture("_BaseMap", fade); shaftMat.mainTexture = fade;
            foreach (var sx in new[] { -4.4f, 4.4f, -2.6f, 2.6f })
            {
                var shaft = GameObject.CreatePrimitive(PrimitiveType.Quad); shaft.name = "Light shaft"; Object.Destroy(shaft.GetComponent<Collider>());
                shaft.transform.SetParent(t, false); shaft.transform.localPosition = new Vector3(sx, 1.7f, 10.8f); shaft.transform.localRotation = Quaternion.Euler(0, 180, 0);
                shaft.transform.localScale = new Vector3(1.3f, 3.4f, 1); shaft.GetComponent<Renderer>().sharedMaterial = shaftMat;
            }
            // Reception desk with a huge beige CRT: the BEGIN button lives on its screen.
            if (useKit)
            {
                PlaceProp(t, "DeskBody", new Vector3(0, 0, 9.6f), 0, Lit("7A5334", 0, .15f)); PlaceProp(t, "DeskTop", new Vector3(0, 0, 9.6f), 0, Lit("2E3140", 0, .4f));
                var beige = Lit("DAD3BC", 0, .25f); var pivot = new Vector3(0, 1.86f, 9.5f);
                var bezel = PlaceProp(t, "MonitorBezel", pivot, 0, beige); PlaceProp(t, "MonitorBody", pivot, 0, beige); PlaceProp(t, "MonitorNeck", pivot, 0, Lit("1D1F26"));
                PlaceProp(t, "MonitorDetail", pivot, 0, Lit("1D1F26"), Mat("2FDC7A"));
                Cube(t, "Keyboard", new Vector3(0, 1.1f, 9.3f), new Vector3(.9f, .04f, .28f), Mat("BDB7A0"));
                // The canvas sits 30 mm behind the bezel face and slightly larger than the aperture, so the bezel overlaps its edges.
                Monitor = MakeSurface(bezel, "Monitor screen", new Vector3(0, 0, .03f), Vector3.zero, 1.72f, 1.22f, 1160, "0b1218", .02f, false);
            }
            else
            {
                Cube(t, "Reception desk front", new Vector3(0, .5f, 9.6f), new Vector3(5.8f, 1f, .9f), woodM);
                Cube(t, "Reception desk top", new Vector3(0, 1.04f, 9.45f), new Vector3(6f, .08f, 1.3f), Mat("2E3140"));
                Cube(t, "Monitor body", new Vector3(0, 1.84f, 9.95f), new Vector3(1.95f, 1.45f, .9f), Mat("DAD3BC"));
                Cube(t, "Monitor stand", new Vector3(0, 1.2f, 9.9f), new Vector3(.7f, .22f, .6f), Mat("CFC8B0"));
                Cube(t, "Keyboard", new Vector3(0, 1.1f, 9.3f), new Vector3(.9f, .04f, .28f), Mat("BDB7A0"));
                Monitor = MakeSurface(t, "Monitor screen", new Vector3(0, 1.88f, 9.48f), Vector3.zero, 1.65f, 1.14f, 1160, "0b1218", .02f);
            }
            foreach (var lx in new[] { -3.5f, 0f, 3.5f }) foreach (var lz in new[] { 3.5f, 9.5f }) AddLight(t, new Vector3(lx, 3.0f, lz), 8f, 2.1f);
            LobbyDecor(t);
            var trimL = theme.Lit("Lobby trim", "C9C2AE", "5A4A44", .3f, .06f);
            foreach (var cx in new[] { -5.96f, 5.96f }) foreach (var cz in new[] { -.46f, 13.96f }) Cube(t, "Corner post", new Vector3(cx, 1.75f, cz), new Vector3(.14f, 3.5f, .14f), trimL);
            Sign = MakeSurface(t, "Company sign", new Vector3(0, 3.0f, 13.9f), Vector3.zero, 5.2f, 1.0f, 1560, "1d2c54", .05f);
            // Sandwich-board sign with the content notice, left of the desk.
            if (useKit) { var board = PlaceProp(t, "BoardFrame", new Vector3(-2.2f, 1.6f, 8.05f), 6, Lit("7D5B34", 0, .1f)); Notice = MakeSurface(board, "Content notice", Vector3.zero, Vector3.zero, 1.35f, 1.55f, 810, "e1e0d2", .04f, false); }
            else
            {
                Cube(t, "Sign leg left", new Vector3(-3.0f, .55f, 8.1f), new Vector3(.08f, 1.1f, .08f), trimM); Cube(t, "Sign leg right", new Vector3(-1.75f, .55f, 8.1f), new Vector3(.08f, 1.1f, .08f), trimM);
                Notice = MakeSurface(t, "Content notice", new Vector3(-2.4f, 1.6f, 8.05f), new Vector3(0, 6, 0), 1.35f, 1.55f, 810, "e1e0d2", .04f);
            }
            // Clipboard on a stand to the right.
            if (useKit)
            {
                var clip = PlaceProp(t, "ClipboardBoard", new Vector3(2.25f, 1.55f, 8.12f), -6, Lit("8A6A3C", 0, .1f), Lit("B9BEC8", .85f, .6f)); PlaceProp(t, "ClipboardPole", new Vector3(2.25f, 1.55f, 8.12f), -6, Lit("1D1F26"));
                Clipboard = MakeSurface(clip, "Clipboard", new Vector3(0, 0, -.017f), Vector3.zero, 1.1f, 1.45f, 770, "8a6a3c", .05f, false);
            }
            else
            {
                Cube(t, "Clipboard stand", new Vector3(2.5f, .55f, 8.2f), new Vector3(.08f, 1.1f, .08f), trimM);
                Clipboard = MakeSurface(t, "Clipboard", new Vector3(2.5f, 1.55f, 8.12f), new Vector3(0, -6, 0), 1.1f, 1.45f, 770, "8a6a3c", .05f);
            }
            // Exit door on the right wall.
            if (useKit)
            {
                PlaceProp(t, "ExitDoor", new Vector3(5.95f, 0, 11.5f), 90, Lit("2C2F3A", 0, .2f)); PlaceProp(t, "ExitBar", new Vector3(5.95f, 0, 11.5f), 90, Lit("C4C9D2", .9f, .8f)); PlaceProp(t, "ExitSign", new Vector3(5.95f, 0, 11.5f), 90, Mat("2FDC7A"));
                DoorPlate = MakeSurface(t, "Door plate", new Vector3(5.855f, 1.75f, 11.5f), new Vector3(0, 90, 0), .85f, .4f, 340, "d9d3b9", .005f);
            }
            else
            {
                Cube(t, "Exit door", new Vector3(5.93f, 1.1f, 11.5f), new Vector3(.1f, 2.2f, 1.2f), Mat("2c2f3a"));
                DoorPlate = MakeSurface(t, "Door plate", new Vector3(5.85f, 1.75f, 11.5f), new Vector3(0, 90, 0), .85f, .4f, 340, "d9d3b9", .02f);
            }
            // Four folders on the desk (resume selection).
            FolderTray = MakeSurface(t, "Folders", new Vector3(0, 1.1f, 8.8f), new Vector3(68, 0, 0), 3.2f, 0.7f, 1280, "2a2d38", .02f);
            // Cheesy motivational posters and the frosted window with a passer-by.
            var poster = MakeSurface(t, "Poster", new Vector3(-5.95f, 1.9f, 11f), new Vector3(0, -90, 0), 1.2f, .85f, 480, "27324f", .03f); PosterText(poster, "TEAMWORK!!!", "(it only works\nwhen someone else\nholds the switch)", "FFDE57");
            var poster2 = MakeSurface(t, "Poster 2", new Vector3(5.95f, 2.1f, 6.8f), new Vector3(0, 90, 0), 1.2f, .85f, 480, "4f2733", .03f); PosterText(poster2, "SMILE :)", "Your next chapter\nstarts here.", "FF8A8A");
            glassMaterial = Glass(new Color(.7f, .8f, .92f, .38f));
            Cube(t, "Window frame", new Vector3(-5.98f, 1.9f, 5f), new Vector3(.06f, 1.5f, 2.9f), trimM);
            var glass = GameObject.CreatePrimitive(PrimitiveType.Cube); glass.name = "Frosted glass"; Object.Destroy(glass.GetComponent<Collider>());
            glass.transform.SetParent(t, false); glass.transform.localPosition = new Vector3(-5.9f, 1.9f, 5f); glass.transform.localScale = new Vector3(.03f, 1.3f, 2.7f); glass.GetComponent<Renderer>().sharedMaterial = glassMaterial;
            var outside = Mat("5e86c4"); Cube(t, "Outside glow", new Vector3(-7.4f, 1.9f, 5f), new Vector3(.05f, 1.5f, 2.9f), outside);
            var valeTexture = SpriteFactory.Person(true, 0, textures);
            var shadow = Billboard(t, "Silhouette behind glass", new Vector3(-7.2f, 1.2f, 8f), new Vector2(1.1f, 2.2f), valeTexture);
            var shadowMat = shadow.GetComponent<Renderer>().sharedMaterial; shadowMat.SetColor("_BaseColor", new Color(.05f, .05f, .08f, 1));
            silhouette = shadow.transform; billboards.Add(silhouette);
            // Drifting dust catching the fluorescents.
            var dot = SpriteFactory.Dot(textures); var rng = new System.Random(11);
            for (int i = 0; i < 70; i++)
            {
                var d = Billboard(t, "Dust", new Vector3((float)rng.NextDouble() * 10 - 5, (float)rng.NextDouble() * 3, (float)rng.NextDouble() * 12 + 1), new Vector2(.03f, .03f), dot);
                d.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", new Color(1, .98f, .85f, 1)); dust.Add(d.transform); billboards.Add(d.transform);
                dustVelocity.Add(new Vector3((float)rng.NextDouble() * .06f - .03f, (float)rng.NextDouble() * .03f - .012f, (float)rng.NextDouble() * .06f - .03f));
            }
        }

        /// <summary>A waiting area, plants and a rug so the lobby reads as a real place (all themed: sleek in safe, decayed in horror).</summary>
        void LobbyDecor(Transform t)
        {
            furnisher.Place("waiting chairs", t, new Vector3(-4.6f, 0, 3.2f), 90, 3, 1);
            furnisher.Place("plant", t, new Vector3(-5.3f, 0, 7.4f), 0, 1, 1); furnisher.Place("plant", t, new Vector3(5.3f, 0, 7.4f), 0, 1, 1); furnisher.Place("plant", t, new Vector3(4.9f, 0, 1.2f), 0, 1, 1);
            furnisher.Place("sofa", t, new Vector3(4.2f, 0, 4.2f), -90, 2.6f, 1f);
            var rug = theme.Lit("Lobby rug", "2B4A7A", "3A1818", .05f, .02f, ProcTex.Fabric(TerminalUI.Hex("2B4A7A"), false, 71, theme.Textures), ProcTex.Fabric(TerminalUI.Hex("2B4A7A"), true, 71, theme.Textures), new Vector2(8, 5));
            Cube(t, "Rug", new Vector3(0, .01f, 6.2f), new Vector3(6.2f, .02f, 3.2f), rug);
        }

        void PosterText(Surface s, string title, string body, string hex)
        {
            kit.Box(0, 0, s.PixelWidth, s.PixelHeight, TerminalUI.Hex("1a2036"), s.Root, "Poster bg");
            kit.Label(title, 10, 40, s.PixelWidth - 20, 110, 52, TerminalUI.Hex(hex), true, s.Root, TextAnchor.MiddleCenter);
            kit.Label(body, 20, 190, s.PixelWidth - 40, 220, 34, TerminalUI.Pale, false, s.Root, TextAnchor.UpperCenter);
        }

        // ------------------------------------------------------------------ interview room
        void BuildRoom(GameAssets assets)
        {
            var t = room.transform; t.position = RoomOrigin;
            var rtone = TerminalUI.Hex("38181E");
            Texture2D RoomWall(bool h) => ProcTex.Wall(WallKind.Panels, TerminalUI.Hex("2D5F66"), TerminalUI.Hex("20323A"), TerminalUI.Hex("B08D57"), h, rtone, 511, theme.Textures);
            var rwS = RoomWall(false); var rwH = RoomWall(true);
            roomWall = theme.Lit("Interview wall", "FFFFFF", "FFFFFF", .12f, .04f, rwS, rwH, new Vector2(3.5f, 1)); var roomWallSide = theme.Lit("Interview side wall", "FFFFFF", "FFFFFF", .12f, .04f, rwS, rwH, new Vector2(3.4f, 1));
            roomFloor = theme.Lit("Interview floor", "FFFFFF", "FFFFFF", .05f, .05f, ProcTex.Floor(FloorKind.Carpet, TerminalUI.Hex("2A2F3F"), TerminalUI.Hex("343A4C"), false, rtone, 512, theme.Textures), ProcTex.Floor(FloorKind.Carpet, TerminalUI.Hex("2A2F3F"), TerminalUI.Hex("343A4C"), true, rtone, 512, theme.Textures), new Vector2(3.5f, 3.4f));
            roomCeiling = theme.Lit("Interview ceiling", "FFFFFF", "FFFFFF", .1f, .02f, ProcTex.Ceiling(false, 513, theme.Textures), ProcTex.Ceiling(true, 513, theme.Textures), new Vector2(3.5f, 3.4f));
            roomDesk = Mat("546A9A", Grime("546A9A"), new Vector2(2, 1)); roomLamp = Mat("FFFBD5");
            roomPaint.Add(roomLamp); roomPaint.Add(roomDesk);
            Cube(t, "Floor", new Vector3(0, -.05f, 3.2f), new Vector3(7, .1f, 6.8f), roomFloor);
            Cube(t, "Ceiling", new Vector3(0, 3.65f, 3.2f), new Vector3(7, .1f, 6.8f), roomCeiling);
            Cube(t, "Back wall", new Vector3(0, 1.8f, 6.45f), new Vector3(7, 3.7f, .1f), roomWall);
            Cube(t, "Front wall", new Vector3(0, 1.8f, -.55f), new Vector3(7, 3.7f, .1f), roomWall);
            Cube(t, "Left wall", new Vector3(-3.55f, 1.8f, 3.2f), new Vector3(.1f, 3.7f, 6.8f), roomWallSide);
            Cube(t, "Right wall", new Vector3(3.55f, 1.8f, 3.2f), new Vector3(.1f, 3.7f, 6.8f), roomWallSide);
            Cube(t, "Lamp", new Vector3(0, 3.58f, 3.4f), new Vector3(2.4f, .04f, .35f), roomLamp);
            AddLight(t, new Vector3(0, 3.2f, 3.2f), 9f, 2.4f); AddLight(t, new Vector3(0, 2.2f, 1.2f), 6f, .8f);
            AddLight(t, new Vector3(-2.7f, 2.6f, 4.6f), 6f, 2.0f); AddLight(t, new Vector3(2.7f, 2.6f, 4.6f), 6f, 2.0f);
            // trim posts hide the seams where the walls meet, and wall sconces light the corners
            var trimR = theme.Lit("Room trim", "C9C2AE", "5A4A44", .3f, .06f);
            foreach (var cx in new[] { -3.47f, 3.47f }) foreach (var cz in new[] { -.47f, 6.37f }) Cube(t, "Corner post", new Vector3(cx, 1.85f, cz), new Vector3(.12f, 3.7f, .12f), trimR);
            foreach (var cx in new[] { -3.44f, 3.44f }) Cube(t, "Sconce", new Vector3(cx, 2.4f, 4.6f), new Vector3(.08f, .4f, .2f), roomLamp);
            float deskTop = .815f, blotterZ = 3.28f, cardsZ = 2.72f;
            if (useKit)
            {
                var topMat = Lit("546A9A", 0, .3f); var bodyMat = Lit("7A5334", 0, .15f); roomLit.Add((topMat, "546A9A", "3a1f33")); roomLit.Add((bodyMat, "7A5334", "4a2430"));
                PlaceProp(t, "InterviewDeskTop", new Vector3(0, 0, 3.45f), 0, topMat); PlaceProp(t, "InterviewDeskBody", new Vector3(0, 0, 3.45f), 0, bodyMat);
                deskTop = .795f; blotterZ = 3.4f; cardsZ = 2.93f;   // new desk top is 0.78 m, and its front edge is at z=2.9
            }
            else
            {
                Cube(t, "Desk top", new Vector3(0, .76f, 3.45f), new Vector3(3.6f, .08f, 1.1f), roomDesk);
                Cube(t, "Desk front", new Vector3(0, .38f, 2.95f), new Vector3(3.5f, .76f, .06f), roomDesk);
                Cube(t, "Desk back", new Vector3(0, .38f, 3.95f), new Vector3(3.5f, .76f, .06f), roomDesk);
            }
            Wall = MakeSurface(t, "Wall panel", new Vector3(0, 2.3f, 6.33f), Vector3.zero, 6.4f, 1.95f, 1500, "14161c", .05f);
            Blotter = MakeSurface(t, "Blotter", new Vector3(0, deskTop, blotterZ), new Vector3(64, 0, 0), 2.7f, 0.42f, 1400, "efe9cf", .02f);
            Cards = MakeSurface(t, "Answer cards", new Vector3(0, deskTop, cardsZ), new Vector3(64, 0, 0), 2.7f, 0.5f, 1400, "23262f", .02f);
            // Mr. Vale: a flat sprite with the real 3D pen-knife in his raised hand.
            ValeActor = new GameObject("Mr. Vale"); ValeActor.transform.SetParent(t, false); ValeActor.transform.localPosition = new Vector3(0, 1.2f, 4.75f);
            valeActor = new ActorSprite("Vale", ValeActor.transform, Outfit.Vale(false), false, 2.6f, materials, textures, 11);
            billboards.Add(ValeActor.transform);
            Knife = KnifeProp.Create(assets, ValeActor.transform);
            if (Knife.Valid)
            {
                Knife.Hold(valeActor.Root.transform, valeActor.HandLocal + new Vector3(.02f, .12f, -.06f), new Vector3(-.15f, 1f, -.35f), 4.6f);
                Knife.SetDeploy(0f);
            }
        }

        void AddLight(Transform parent, Vector3 local, float range, float intensity)
        {
            var go = new GameObject("Stage light"); go.transform.SetParent(parent, false); go.transform.localPosition = local;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = new Color(1f, .95f, .85f); l.shadows = LightShadows.None; stageLights.Add(l);
        }

        /// <summary>Dim, foggy rooms: fewer lights, short sight lines, redder and tighter in horror.</summary>
        public void ApplyAtmosphere(bool isHorror)
        {
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = isHorror ? new Color(.13f, .04f, .05f) : new Color(.07f, .075f, .095f);
            RenderSettings.fogStartDistance = isHorror ? 5f : 6f; RenderSettings.fogEndDistance = isHorror ? 18f : 22f;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = isHorror ? new Color(.34f, .17f, .17f) : new Color(.30f, .31f, .36f);
            Camera.backgroundColor = RenderSettings.fogColor;
            foreach (var l in stageLights) { l.color = isHorror ? new Color(1f, .42f, .32f) : new Color(1f, .95f, .85f); l.intensity = isHorror ? 3.0f : 2.1f; }
        }

        // ------------------------------------------------------------------ control
        bool shownOnce;
        public void SnapCamera() { snap = true; }
        /// <summary>Jump to the final camera pose with the intro dolly finished (used for screenshots and tests).</summary>
        public void SettleCamera() { snap = true; LobbyClock = 99f; }
        public void Show(Place place, bool isHorror)
        {
            bool changed = place != Current || !shownOnce; shownOnce = true;
            Current = place; horror = isHorror; lobby.SetActive(place == Place.Lobby); room.SetActive(place == Place.Interview);
            theme.SetHorror(isHorror); roomLamp.SetColor("_BaseColor", TerminalUI.Hex(isHorror ? "EE454A" : "FFFBD5")); roomDesk.SetColor("_BaseColor", TerminalUI.Hex(isHorror ? "452235" : "546A9A"));
            foreach (var (m, normal, horror) in roomLit) m.SetColor("_BaseColor", TerminalUI.Hex(isHorror ? horror : normal));
            if (valeActor != null) valeActor.Scary = isHorror;
            if (!changed) return;
            if (place == Place.Lobby) { LobbyClock = 0; SetLobbyView(LobbyView.Front); }
            else { targetPosition = RoomOrigin + new Vector3(0, 1.55f, 1.1f); targetYaw = 0; targetPitch = 13; }
            snap = true;
        }
        public void SetHorrorLook(bool value) { horror = value; Show(Current, value); }

        public void SetLobbyView(LobbyView view)
        {
            View = view;
            switch (view)
            {
                case LobbyView.Front: targetPosition = LobbyOrigin + new Vector3(0, 1.55f, 5.9f); targetYaw = 0; targetPitch = 2; break;
                case LobbyView.Folders: targetPosition = LobbyOrigin + new Vector3(0, 1.65f, 7.2f); targetYaw = 0; targetPitch = 14; break;
                case LobbyView.Setup: targetPosition = LobbyOrigin + new Vector3(1.2f, 1.5f, 6.4f); targetYaw = 22; targetPitch = 0; break;
            }
        }
        public void SetValeVisible(bool visible) { if (ValeActor) ValeActor.SetActive(visible); }
        public void SetBlackout(float alpha) { shadeAlpha = Mathf.Clamp01(alpha); shade.enabled = shadeAlpha > .001f; shadeMaterial.SetColor("_BaseColor", new Color(0, 0, 0, shadeAlpha)); }

        public void Tick(float dt, float time, Vector2 mouse01, bool frozen = false, float talk = 0f, float anger = 0f)
        {
            if (!frozen) LobbyClock += dt;
            // Camera pose: slow dolly in on arrival, then ease toward the target pose with a mouse parallax and a drifting sway.
            Vector3 goal = targetPosition; float goalYaw = targetYaw, goalPitch = targetPitch;
            if (Current == Place.Lobby && View == LobbyView.Front)
            {
                float arrive = Mathf.SmoothStep(0, 1, Mathf.Clamp01(LobbyClock / 7f));
                goal = Vector3.Lerp(LobbyOrigin + new Vector3(0, 1.62f, .9f), targetPosition, arrive);
            }
            goal += new Vector3(Mathf.Sin(time * .31f) * .05f, Mathf.Sin(time * .47f) * .02f, 0);
            goalYaw += (mouse01.x - .5f) * 16f; goalPitch += -(mouse01.y - .5f) * 8f;
            if (snap) { position = goal; yaw = goalYaw; pitch = goalPitch; snap = false; }
            float k = 1f - Mathf.Exp(-dt * 3f);
            position = Vector3.Lerp(position, goal, k); yaw = Mathf.LerpAngle(yaw, goalYaw, k); pitch = Mathf.Lerp(pitch, goalPitch, k);
            Camera.transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0));
            Camera.rect = TerminalUI.Viewport(); Camera.aspect = 16f / 9f;
            if (frozen) return;
            if (valeActor != null && ValeActor.activeInHierarchy) valeActor.Tick(dt, 0f, talk, anger, Camera.transform.position);
            // Fluorescents: steady hum with a stuttering failure now and then.
            for (int i = 0; i < lamps.Count; i++)
            {
                float flicker = Mathf.PerlinNoise(time * 6f, i * 13.7f) > .93f ? .35f : 1f;
                lamps[i].SetColor("_BaseColor", Color.Lerp(TerminalUI.Hex("8f8a6e"), TerminalUI.Hex("FFF9D6"), flicker * (.92f + .08f * Mathf.Sin(time * 50f + i))));
            }
            if (Current == Place.Lobby)
            {
                for (int i = 0; i < dust.Count; i++)
                {
                    dust[i].localPosition += dustVelocity[i] * dt;
                    var p = dust[i].localPosition; if (p.y > 3.3f) p.y = .2f; if (p.y < 0) p.y = 3.2f; if (Mathf.Abs(p.x) > 5.5f) p.x *= -.95f; if (p.z < 1 || p.z > 13.5f) p.z = Mathf.Clamp(p.z, 1.5f, 13f); dust[i].localPosition = p;
                }
                // A figure walks past the frosted window every ~30 s.
                float phase = (time % 30f) / 30f; float z = Mathf.Lerp(9.5f, 1.5f, Mathf.Clamp01((phase - .1f) / .35f));
                silhouette.localPosition = new Vector3(-7.2f, 1.2f, z);
            }
            foreach (var b in billboards)
            {
                if (!b) continue; Vector3 direction = Camera.transform.position - b.position; direction.y = 0;
                if (direction.sqrMagnitude > .0001f) b.rotation = Quaternion.LookRotation(-direction, Vector3.up);
            }
        }

        public void Dispose()
        {
            Knife?.Dispose(); kit.Dispose(); theme.Dispose();
            foreach (var m in materials) if (m) Object.Destroy(m);
            foreach (var t in textures) if (t) Object.Destroy(t);
            if (Root) Object.Destroy(Root);
        }
    }
}
