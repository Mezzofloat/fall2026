using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static Breathe.PropFactory;

namespace Breathe
{
    /// <summary>One room's look: sleek in SAFE mode, weathered in HORROR (the same room, decayed).</summary>
    public sealed class RoomStyle
    {
        public string Name; public WallKind Wall; public string Paint, Wain, Trim; public FloorKind Floor; public string FloorA, FloorB; public string Tone; public string DoorPaint;
        public string Graffiti;
        public static readonly RoomStyle[] All = {
            new RoomStyle { Name = "RECEPTION", Wall = WallKind.Panels, Paint = "F1EFE6", Wain = "2B4A7A", Trim = "D0B070", Floor = FloorKind.Marble, FloorA = "E8E4DA", FloorB = "B8B2A4", Tone = "5A2A22", DoorPaint = "2B4A7A", Graffiti = "WELCOME" },
            new RoomStyle { Name = "OPEN PLAN", Wall = WallKind.Plain, Paint = "D8DEE6", Wain = "9AA7B8", Trim = "6B7686", Floor = FloorKind.Carpet, FloorA = "5E6C84", FloorB = "6E7C94", Tone = "4A2E2A", DoorPaint = "6B7686", Graffiti = "SMILE :)" },
            new RoomStyle { Name = "BREAK ROOM", Wall = WallKind.Tiles, Paint = "BFE0D0", Wain = "E8F0EC", Trim = "8FB8A8", Floor = FloorKind.Checker, FloorA = "E8E6DC", FloorB = "7DA8A0", Tone = "3E2A24", DoorPaint = "7DA8A0", Graffiti = "EAT" },
            new RoomStyle { Name = "ARCHIVE", Wall = WallKind.Blocks, Paint = "C9C2AE", Wain = "A8A190", Trim = "8E8776", Floor = FloorKind.Concrete, FloorA = "8E8B84", FloorB = "A29E94", Tone = "3A2D22", DoorPaint = "8E8776", Graffiti = "YOU WERE HERE" },
            new RoomStyle { Name = "ATRIUM", Wall = WallKind.Plain, Paint = "F5F2E8", Wain = "E0DCCB", Trim = "C8A860", Floor = FloorKind.Marble, FloorA = "D9DDE0", FloorB = "A8B0B8", Tone = "50302A", DoorPaint = "C8A860", Graffiti = "LISTEN" },
            new RoomStyle { Name = "CONFERENCE", Wall = WallKind.Wood, Paint = "7A5238", Wain = "5A3A28", Trim = "C8A860", Floor = FloorKind.Carpet, FloorA = "3C4658", FloorB = "4A5468", Tone = "40221E", DoorPaint = "5A3A28", Graffiti = "NO ONE LEAVES" },
            new RoomStyle { Name = "HR OFFICE", Wall = WallKind.Stripes, Paint = "EBD9A0", Wain = "8A5A3A", Trim = "C09A5A", Floor = FloorKind.Wood, FloorA = "A9794F", FloorB = "8F6540", Tone = "4C2A1C", DoorPaint = "8A5A3A", Graffiti = "AFRAID?" },
            new RoomStyle { Name = "SERVER ROOM", Wall = WallKind.Plain, Paint = "4A5668", Wain = "2A323F", Trim = "7A8AA0", Floor = FloorKind.Grate, FloorA = "4A5666", FloorB = "7A8AA0", Tone = "301820", DoorPaint = "2A323F", Graffiti = "HE SEES" },
            new RoomStyle { Name = "INTERVIEW SUITE", Wall = WallKind.Panels, Paint = "2D5F66", Wain = "20323A", Trim = "B08D57", Floor = FloorKind.Carpet, FloorA = "2A2F3F", FloorB = "343A4C", Tone = "38181E", DoorPaint = "20323A", Graffiti = "RUN" },
        };
    }

    /// <summary>
    /// The first-person office: a 3 x 3 building of nine distinct rooms joined by animated doors. SAFE mode is a sleek, clean, dim office;
    /// HORROR mode swaps in weathered, moldy textures, flickering red light and tighter fog. Billboard actors keep their flat look.
    /// </summary>
    public sealed class OfficeWorld
    {
        public const float WallHeight = 2.8f, EyeHeight = 1.55f;
        readonly GameObject rooms;
        readonly OfficeMap map;
        readonly Theme theme = new Theme();
        readonly PropFactory kit;
        readonly Furnisher furnisher;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> loose = new List<Material>();
        readonly List<GameObject> horrorOnly = new List<GameObject>();
        readonly List<GameObject> safeOnly = new List<GameObject>();
        readonly List<ActorSprite> colleagues = new List<ActorSprite>();
        readonly List<GameObject> targets = new List<GameObject>();
        readonly List<Transform> beacons = new List<Transform>();
        readonly List<Transform> shelterMarks = new List<Transform>();
        readonly TerminalUI text = TerminalUI.Kit();
        readonly List<RoomLight> lights = new List<RoomLight>();
        readonly List<DoorVisual> doors = new List<DoorVisual>();
        readonly Material beamMaterial, pageMaterial;
        readonly Texture2D beamTexture;
        GameObject monster;
        int playerRoom = -1; bool horror;
        float lastHunterSpeed;
        public Camera Camera { get; }
        public ActorSprite Vale { get; private set; }
        public KnifeProp Knife { get; private set; }
        public Theme Look => theme;
        public static Vector3 Point(Vector2 cell, float height = 0) => new Vector3(cell.x, height, -cell.y);
        public Vector3 ValeWorldPosition => monster ? monster.transform.position : Vector3.zero;

        sealed class RoomLight { public Light Light; public Material Lamp; public Transform[] Fixtures; public int Room; public float Seed; }
        sealed class DoorVisual
        {
            public Door Door; public Transform Hinge; public float Smoothed; public float Direction; public Renderer[] Slab;
        }

        public OfficeWorld(Transform parent, OfficeMap map, GameAssets assets = null)
        {
            this.map = map;
            foreach (var old in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) old.enabled = false;
            foreach (var old in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) old.enabled = false;
            var cameraObject = new GameObject("First-person camera", typeof(Camera), typeof(AudioListener)); cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera"; Camera = cameraObject.GetComponent<Camera>(); Camera.fieldOfView = 72;
            Camera.nearClipPlane = .05f; Camera.farClipPlane = 40; Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = TerminalUI.Hex("08090d"); Camera.allowHDR = false;
            rooms = new GameObject("Office - nine rooms"); rooms.transform.SetParent(parent, false);
            kit = new PropFactory(assets); furnisher = new Furnisher(theme, kit);
            beamTexture = MakeBeamTexture(); beamMaterial = Glass("Page beam", beamTexture); pageMaterial = theme.Glow("Page", "FFE96A", "FFB347");
            BuildShell(); BuildFurniture(); BuildDoors(); BuildLights(); BuildDecor(); BuildActors(assets);
            theme.SetHorror(false); rooms.SetActive(false);
        }

        // ------------------------------------------------------------------ atmosphere
        /// <summary>Dim, foggy, limited vision: farther in safe mode, claustrophobic in horror.</summary>
        public void ApplyAtmosphere(bool isHorror)
        {
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = isHorror ? new Color(.13f, .04f, .05f) : new Color(.06f, .07f, .09f);
            RenderSettings.fogStartDistance = isHorror ? 4f : 3f; RenderSettings.fogEndDistance = isHorror ? 17f : 15f;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = isHorror ? new Color(.34f, .17f, .17f) : new Color(.20f, .21f, .25f);
            Camera.backgroundColor = RenderSettings.fogColor;
        }

        // ------------------------------------------------------------------ shell: floors, ceilings, walls
        sealed class RoomMats { public Material Wall, Floor, Ceiling; public RoomStyle Style; }
        readonly RoomMats[] roomMats = new RoomMats[9];

        void BuildShell()
        {
            var ceilS = ProcTex.Ceiling(false, 21, theme.Textures); var ceilH = ProcTex.Ceiling(true, 21, theme.Textures);
            for (int i = 0; i < 9; i++)
            {
                var s = RoomStyle.All[i]; var tone = TerminalUI.Hex(s.Tone);
                var wallS = ProcTex.Wall(s.Wall, TerminalUI.Hex(s.Paint), TerminalUI.Hex(s.Wain), TerminalUI.Hex(s.Trim), false, tone, 100 + i, theme.Textures);
                var wallH = ProcTex.Wall(s.Wall, TerminalUI.Hex(s.Paint), TerminalUI.Hex(s.Wain), TerminalUI.Hex(s.Trim), true, tone, 100 + i, theme.Textures);
                var floorS = ProcTex.Floor(s.Floor, TerminalUI.Hex(s.FloorA), TerminalUI.Hex(s.FloorB), false, tone, 200 + i, theme.Textures);
                var floorH = ProcTex.Floor(s.Floor, TerminalUI.Hex(s.FloorA), TerminalUI.Hex(s.FloorB), true, tone, 200 + i, theme.Textures);
                var mats = new RoomMats { Style = s };
                mats.Wall = theme.Lit("Wall " + s.Name, "FFFFFF", "FFFFFF", .12f, .04f, wallS, wallH);
                mats.Floor = theme.Lit("Floor " + s.Name, "FFFFFF", "FFFFFF", s.Floor == FloorKind.Marble ? .65f : s.Floor == FloorKind.Carpet ? .05f : .35f, .08f, floorS, floorH);
                mats.Ceiling = theme.Lit("Ceiling " + s.Name, "FFFFFF", "FFFFFF", .1f, .02f, ceilS, ceilH);
                roomMats[i] = mats;
                // floor and ceiling slabs for the room block (walls are 1 m thick cells, so the slab covers them too)
                float x0 = (i % 3) * OfficeMap.RoomW, y0 = (i / 3) * OfficeMap.RoomH, x1 = (i % 3 == 2) ? OfficeMap.Width : x0 + OfficeMap.RoomW, y1 = (i / 3 == 2) ? OfficeMap.Height : y0 + OfficeMap.RoomH;
                Slab("Floor " + s.Name, x0, y0, x1, y1, 0f, false, mats.Floor, 2f); Slab("Ceiling " + s.Name, x0, y0, x1, y1, WallHeight, true, mats.Ceiling, 2f);
            }
            BuildWalls();
            // trim posts in every room corner and crown trim hide seams where wall runs meet
            var trim = theme.Lit("Corner trim", "C9C2AE", "5A4A44", .3f, .06f);
            for (int i = 0; i < 9; i++)
            {
                var r = OfficeMap.RoomRect(i);
                foreach (var (cx, cy) in new[] { (r.xMin, r.yMin), (r.xMax, r.yMin), (r.xMin, r.yMax), (r.xMax, r.yMax) })
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube); post.name = "Corner post"; Object.Destroy(post.GetComponent<Collider>());
                    post.transform.SetParent(rooms.transform, false); post.transform.localPosition = new Vector3(cx + (cx > r.center.x ? -.04f : .04f), WallHeight / 2f, -(cy + (cy > r.center.y ? -.04f : .04f)));
                    post.transform.localScale = new Vector3(.09f, WallHeight, .09f); post.GetComponent<Renderer>().sharedMaterial = trim;
                }
            }
        }

        /// <summary>A horizontal rectangle with UVs in metres / tile, facing up (floor) or down (ceiling).</summary>
        void Slab(string name, float x0, float y0, float x1, float y1, float height, bool facingDown, Material m, float tile)
        {
            var mesh = new Mesh { name = name }; meshes.Add(mesh);
            var a = new Vector3(x0, height, -y0); var b = new Vector3(x1, height, -y0); var c = new Vector3(x1, height, -y1); var d = new Vector3(x0, height, -y1);
            mesh.vertices = new[] { a, b, c, d }; mesh.uv = new[] { new Vector2(x0 / tile, y0 / tile), new Vector2(x1 / tile, y0 / tile), new Vector2(x1 / tile, y1 / tile), new Vector2(x0 / tile, y1 / tile) };
            mesh.triangles = facingDown ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };   // clockwise as seen from the visible side
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(rooms.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// <summary>A vertical wall face along a run of cells, facing a given direction, textured at 2 m x wall height per tile.</summary>
        void WallFace(string name, Vector3 centerBottom, Vector3 along, float length, Vector3 normal, float height, Material m, float yOffsetV = 0f)
        {
            var mesh = new Mesh { name = name }; meshes.Add(mesh);
            var half = along.normalized * length / 2f; var up = Vector3.up * height;
            var p0 = centerBottom - half; var p1 = centerBottom + half;
            mesh.vertices = new[] { p0, p1, p1 + up, p0 + up };
            mesh.uv = new[] { new Vector2(0, yOffsetV), new Vector2(length / 2f, yOffsetV), new Vector2(length / 2f, yOffsetV + height / WallHeight), new Vector2(0, yOffsetV + height / WallHeight) };
            // winding so the visible side faces `normal`
            bool aligned = Vector3.Dot(Vector3.Cross(Vector3.up, -normal), p1 - p0) > 0;   // is p1 to the viewer's right?
            mesh.triangles = aligned ? new[] { 0, 3, 2, 0, 2, 1 } : new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(rooms.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        void BuildWalls()
        {
            // Collect exposed faces: (room, direction, line) -> list of cell indices along the run, then merge contiguous runs.
            var runs = new Dictionary<(int room, int dir, int line), List<int>>();
            for (int x = 0; x < OfficeMap.Width; x++) for (int y = 0; y < OfficeMap.Height; y++)
            {
                if (!map.PathSolid(x, y) || map.Furniture(x, y)) continue;
                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = x + (dir == 0 ? 1 : dir == 1 ? -1 : 0), ny = y + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                    if (map.PathSolid(nx, ny)) continue;
                    int room = OfficeMap.RoomIndex(new Vector2(nx + .5f, ny + .5f));
                    if (dir < 2) { var key = (room, dir, x); if (!runs.TryGetValue(key, out var list)) runs[key] = list = new List<int>(); list.Add(y); }
                    else { var key = (room, dir, y); if (!runs.TryGetValue(key, out var list)) runs[key] = list = new List<int>(); list.Add(x); }
                }
            }
            foreach (var kv in runs)
            {
                var (room, dir, line) = kv.Key; var list = kv.Value; list.Sort();
                int start = list[0], prev = list[0];
                for (int i = 1; i <= list.Count; i++)
                {
                    if (i < list.Count && list[i] == prev + 1) { prev = list[i]; continue; }
                    EmitRun(room, dir, line, start, prev - start + 1);
                    if (i < list.Count) { start = list[i]; prev = list[i]; }
                }
            }
        }

        void EmitRun(int room, int dir, int line, int start, int count)
        {
            var m = roomMats[room].Wall; float length = count;
            if (dir == 0) WallFace("Wall E", new Vector3(line + 1f, 0, -(start + count / 2f)), Vector3.back, length, Vector3.right, WallHeight, m);
            else if (dir == 1) WallFace("Wall W", new Vector3(line, 0, -(start + count / 2f)), Vector3.forward, length, Vector3.left, WallHeight, m);
            else if (dir == 2) WallFace("Wall N", new Vector3(start + count / 2f, 0, -(line + 1f)), Vector3.right, length, Vector3.back, WallHeight, m);
            else WallFace("Wall S", new Vector3(start + count / 2f, 0, -line), Vector3.left, length, Vector3.forward, WallHeight, m);
        }

        // ------------------------------------------------------------------ furniture and decor
        void BuildFurniture()
        {
            foreach (var block in map.Blocks)
            {
                var rect = OfficeMap.RoomRect(block.Room); var center = new Vector2(rect.x + rect.width / 2f, rect.y + rect.height / 2f);
                furnisher.Build(block, rooms.transform, center);
            }
        }

        Material Glass(string name, Texture2D texture)
        {
            var template = Resources.Load<Material>("OfficeGlass") ?? Resources.Load<Material>("OfficeSurface");
            var m = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.name = name;
            m.SetColor("_BaseColor", Color.white); if (texture) { m.SetTexture("_BaseMap", texture); m.mainTexture = texture; }
            loose.Add(m); return m;
        }
        static Texture2D MakeBeamTexture()
        {
            var t = new Texture2D(8, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[8 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 8; x++) { float edge = 1f - Mathf.Abs(x - 3.5f) / 4f; px[y * 8 + x] = new Color(1f, .93f, .45f, Mathf.Pow(y / 63f, 1.4f) * edge * .55f); }
            t.SetPixels(px); t.Apply(); return t;
        }

        /// <summary>A world-space canvas (depth tested, so it can never be seen through walls) with a pixel-font label.</summary>
        public Transform Plate(Transform parent, string name, Vector3 position, Quaternion rotation, float widthM, float heightM, int pixels, System.Action<RectTransform, float, float> fill)
        {
            var holder = new GameObject("Plate: " + name); holder.transform.SetParent(parent, false); holder.transform.localPosition = position; holder.transform.localRotation = rotation;
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); canvasObject.transform.SetParent(holder.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            float ph = pixels * heightM / widthM; var rt = canvasObject.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(pixels, ph); rt.localScale = Vector3.one * (widthM / pixels);
            fill(rt, pixels, ph); return holder.transform;
        }

        void BuildDecor()
        {
            for (int i = 0; i < 9; i++)
            {
                var s = RoomStyle.All[i]; var rect = OfficeMap.RoomRect(i); float cx = rect.x + rect.width / 2f, cy = rect.y + rect.height / 2f;
                // Graffiti: only visible in horror, scrawled in red high on a wall.
                var g = Plate(rooms.transform, "Graffiti " + s.Name, new Vector3(rect.x + 3.2f + (i % 3) * 1.5f, 1.55f + (i % 2) * .2f, -(rect.y + rect.height - .03f)), Quaternion.Euler(0, 180, 0), 1.9f, .6f, 480, (rt, w, h) =>
                {
                    var l = text.Label(s.Graffiti, 0, 0, w, h, 120, new Color(.72f, .05f, .06f), true, rt, TextAnchor.MiddleCenter); l.horizontalOverflow = HorizontalWrapMode.Overflow;
                });
                g.rotation = Quaternion.Euler(0, 180, (i % 3 - 1) * 2f); horrorOnly.Add(g.gameObject);
                // A clean wall plaque (safe) with the room name; the weathered version replaces it in horror.
                var sign = Plate(rooms.transform, "Name plate " + s.Name, new Vector3(rect.x + rect.width - 2.6f, 1.75f, -(rect.y + .03f)), Quaternion.identity, 1.6f, .32f, 480, (rt, w, h) =>
                {
                    text.Box(0, 0, w, h, TerminalUI.Hex("1B1F2B"), rt, "plate"); text.Box(6, 6, w - 12, h - 12, TerminalUI.Hex("2A3040"), rt, "plate inner");
                    var l = text.Label(s.Name, 0, 0, w, h, 66, TerminalUI.Hex("E8D9A0"), true, rt, TextAnchor.MiddleCenter); l.horizontalOverflow = HorizontalWrapMode.Overflow;
                });
                // Wall clock
                Clock(rooms.transform, new Vector3(rect.x + 1.6f, 2.15f, -(rect.y + rect.height - .05f)), 180f + 180f, i);
                if (i == 5 && kit.Has("Whiteboard"))
                    kit.PlaceSkinned(rooms.transform, "Whiteboard", new Vector3(rect.x + 4.5f, 1.55f, -(rect.y + rect.height - .02f)), 180, Vector3.one, new PropSkin { Main = theme.Lit("Board", "F4F5F2", "A8A294", .5f, .1f), Dark = theme.Lit("Board dark", "20232a", "0c0a0a", .3f, .1f), Metal = theme.Lit("Board frame", "B9BEC8", "7A706A", .7f, .2f, null, null, null, .8f), Glow = theme.Lit("Board", "F4F5F2", "A8A294", .5f, .1f), Accent = theme.Lit("Marker", "D94A4A", "6E2020", .3f, .1f), Glass = theme.Lit("Board", "F4F5F2", "A8A294", .5f, .1f) });
            }
        }

        void Clock(Transform parent, Vector3 pos, float yaw, int seed)
        {
            var holder = new GameObject("Clock").transform; holder.SetParent(parent, false); holder.localPosition = pos; holder.localRotation = Quaternion.Euler(0, 180, 0);
            if (kit.Has("WallClock")) { kit.PlaceSkinned(holder, "WallClock", Vector3.zero, 180, Vector3.one * .62f, new PropSkin { Main = theme.Lit("Clock face", "F4F4EE", "B8B09A", .4f, .1f), Dark = theme.Lit("Clock dark", "15161B", "0A0809", .3f, .1f), Metal = theme.Lit("Clock dark", "15161B", "0A0809", .3f, .1f), Glow = theme.Lit("Clock face", "F4F4EE", "B8B09A", .4f, .1f), Accent = theme.Lit("Clock dark", "15161B", "0A0809", .3f, .1f), Glass = theme.Lit("Clock face", "F4F4EE", "B8B09A", .4f, .1f) }); return; }
            var face = theme.Lit("Clock", "F4F4EE", "B8B09A", .4f, .1f); var dark = theme.Lit("Clock dark", "15161B", "0A0809", .3f, .1f);
            Cyl(holder, "Face", Vector3.zero, .2f, .04f, face, new Vector3(90, 0, 0)); Cyl(holder, "Rim", new Vector3(0, 0, .01f), .22f, .03f, dark, new Vector3(90, 0, 0));
            var hour = Box(holder, "Hour", new Vector3(0, .05f, -.03f), new Vector3(.015f, .1f, .01f), dark); hour.transform.localRotation = Quaternion.Euler(0, 0, 30 * (seed + 3));
            var minute = Box(holder, "Minute", new Vector3(0, .075f, -.035f), new Vector3(.01f, .15f, .01f), dark); minute.transform.localRotation = Quaternion.Euler(0, 0, -50 * seed);
            // A clock that is always at the same hour. In horror it freezes at 3:07.
            holder.GetChild(0).localScale = new Vector3(.4f, .02f, .4f);
        }

        // ------------------------------------------------------------------ doors
        void BuildDoors()
        {
            var frameMat = theme.Lit("Door frame", "E8DCC0", "B8C8D8", .35f, .08f); var darkMat = theme.Lit("Door inset", "F0E6CE", "9AAAC0", .2f, .04f); var lampMat = theme.Glow("Door lamp", "5CFF9A", "FF3A2A");
            foreach (var d in map.Doors)
            {
                var visual = new DoorVisual { Door = d };
                int roomA = OfficeMap.RoomIndex(d.Center + (d.PassesAlongX ? Vector2.left : Vector2.down) * 1f), roomB = OfficeMap.RoomIndex(d.Center + (d.PassesAlongX ? Vector2.right : Vector2.up) * 1f);
                var slabS = ProcTex.Door(TerminalUI.Hex(RoomStyle.All[roomB].DoorPaint), TerminalUI.Hex("D0B070"), false, d.Id, 300 + d.Id, theme.Textures);
                var slabH = ProcTex.Door(TerminalUI.Hex(RoomStyle.All[roomB].DoorPaint), TerminalUI.Hex("D0B070"), true, d.Id, 300 + d.Id, theme.Textures);
                var mat = theme.Lit("Door " + d.Id, "FFFFFF", "9FB4D4", .25f, .06f, slabS, slabH);
                // door sits in the middle of the 1 m thick wall cell
                Vector3 centre = Point(d.Center); var root = new GameObject("Door " + d.Id).transform; root.SetParent(rooms.transform, false); root.position = centre;
                Quaternion face = d.PassesAlongX ? Quaternion.identity : Quaternion.Euler(0, 90, 0);   // local +x = along the passage
                root.rotation = face;
                // frame and slab: approved Blender parts when present (local z is across the doorway, local x along the passage)
                var knobMat = theme.Lit("Knob", "D6D9E0", "8E807A", .85f, .2f, null, null, null, 1f);
                var hinge = new GameObject("Hinge").transform; hinge.SetParent(root, false); hinge.localPosition = new Vector3(0, 0, -.43f); visual.Hinge = hinge;
                if (kit.Has("DoorSlab") && kit.Has("DoorFrame"))
                {
                    var skin = new PropSkin { Main = frameMat, Dark = darkMat, Metal = knobMat, Glow = frameMat, Accent = frameMat, Glass = frameMat };
                    kit.PlaceSkinned(root, "DoorFrame", Vector3.zero, -90, new Vector3(.93f, 1f, 1f), skin);
                    kit.PlaceSkinned(hinge, "DoorSlab", Vector3.zero, -90, new Vector3(.92f, 1f, 1f), new PropSkin { Main = mat, Dark = darkMat, Metal = knobMat, Glow = mat, Accent = mat, Glass = mat });
                }
                else
                {
                    Box(root, "Jamb L", new Vector3(0, 1.05f, -.5f + .035f), new Vector3(.14f, 2.1f, .07f), frameMat); Box(root, "Jamb R", new Vector3(0, 1.05f, .5f - .035f), new Vector3(.14f, 2.1f, .07f), frameMat);
                    Box(root, "Lintel", new Vector3(0, 2.13f, 0), new Vector3(.14f, .07f, 1f), frameMat);
                    var slab = GameObject.CreatePrimitive(PrimitiveType.Cube); slab.name = "Slab"; Object.Destroy(slab.GetComponent<Collider>()); slab.transform.SetParent(hinge, false);
                    slab.transform.localPosition = new Vector3(0, 1.0f, .465f); slab.transform.localScale = new Vector3(.06f, 2.0f, .93f); slab.GetComponent<Renderer>().sharedMaterial = mat;
                }
                Box(root, "Transom", new Vector3(0, 2.46f, 0), new Vector3(.12f, .68f, 1f), frameMat);
                // a bright lamp strip above every door (green when safe, red in horror) so doors never read as wall
                Box(root, "Door lamp", new Vector3(0, 2.2f, 0), new Vector3(.2f, .06f, .6f), lampMat, false);
                visual.Direction = (d.Id % 2 == 0) ? 1f : -1f;
                // room name above the door on both sides
                foreach (int side in new[] { -1, 1 })
                {
                    int room = side < 0 ? roomA : roomB; var style = RoomStyle.All[room];
                    var plate = Plate(root, "Door sign", new Vector3(side * (.5f - .02f), 2.62f, 0), Quaternion.Euler(0, side < 0 ? 90 : 270, 0), .9f, .22f, 360, (rt, w, h) =>
                    {
                        text.Box(0, 0, w, h, TerminalUI.Hex("171A24"), rt, "sign"); var l = text.Label(style.Name, 0, 0, w, h, 54, TerminalUI.Hex("E8D9A0"), true, rt, TextAnchor.MiddleCenter); l.horizontalOverflow = HorizontalWrapMode.Overflow;
                    });
                }
                doors.Add(visual);
            }
        }

        // ------------------------------------------------------------------ lights
        void BuildLights()
        {
            for (int i = 0; i < 9; i++)
            {
                var rect = OfficeMap.RoomRect(i); var lampMat = theme.Glow("Lamp " + i, "FFF8DD", "FF6A4A");
                var go = new GameObject("Room light " + i); go.transform.SetParent(rooms.transform, false); go.transform.position = new Vector3(rect.x + rect.width / 2f, 2.55f, -(rect.y + rect.height / 2f));
                var light = go.AddComponent<Light>(); light.type = LightType.Point; light.range = 8.5f; light.intensity = 2.3f; light.color = new Color(1f, .95f, .85f); light.shadows = LightShadows.None; light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low; light.shadowStrength = .9f;
                var fixtures = new List<Transform>();
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = new Vector3(rect.x + rect.width * (k == 0 ? .3f : .7f), WallHeight - .035f, -(rect.y + rect.height / 2f));
                    if (kit.Has("LightHousing") && kit.Has("LightTube")) { fixtures.Add(kit.Place(rooms.transform, "LightHousing", p, 90, Vector3.one, theme.Lit("Housing", "C9C9C0", "5E5048", .4f, .1f))); kit.Place(rooms.transform, "LightTube", p, 90, Vector3.one, lampMat); }
                    else { Box(rooms.transform, "Fixture", p, new Vector3(.3f, .06f, 1.6f), theme.Lit("Housing", "C9C9C0", "5E5048", .4f, .1f)); Box(rooms.transform, "Tube", p + new Vector3(0, -.04f, 0), new Vector3(.12f, .03f, 1.5f), lampMat, false); }
                }
                lights.Add(new RoomLight { Light = go.GetComponent<Light>(), Lamp = lampMat, Room = i, Seed = i * 7.31f });
            }
        }

        // ------------------------------------------------------------------ actors and markers
        void BuildActors(GameAssets assets)
        {
            for (int i = 0; i < 3; i++)
            {
                var actor = new ActorSprite("Colleague " + i, rooms.transform, Outfit.Colleague(i), false, 1.9f, loose, theme.Textures, 40 + i);
                actor.Root.transform.position = Point(map.Tour[i], .95f); actor.Root.SetActive(false); colleagues.Add(actor);
            }
            monster = new GameObject("Mr. Vale (pursuer)"); monster.transform.SetParent(rooms.transform, false); monster.transform.position = Point(map.HunterSpawn, 1.3f);
            Vale = new ActorSprite("Vale", monster.transform, Outfit.Vale(true), true, 2.6f, loose, theme.Textures, 7);
            Knife = KnifeProp.Create(assets, Vale.Root.transform);
            if (Knife.Valid) { Knife.Hold(Vale.Root.transform, Vale.HandLocal + new Vector3(.02f, .12f, -.06f), new Vector3(-.15f, 1f, -.45f), 4.6f); Knife.SetDeploy(1f); }
            monster.SetActive(false);
            foreach (var spot in map.Shelters)
            {
                var tile = Box(rooms.transform, "Quiet alcove", Point(spot, .015f), new Vector3(.95f, .03f, .95f), theme.Glow("Alcove", "5A8FFF", "2A4A9A")); shelterMarks.Add(tile.transform);
                var mark = Plate(rooms.transform, "Alcove mark", Point(spot, 1.75f), Quaternion.identity, .6f, .3f, 180, (rt, w, h) => { var l = text.Label("QUIET", 0, 0, w, h, 54, TerminalUI.Hex("8BB1FF"), true, rt, TextAnchor.MiddleCenter); });
                shelterMarks.Add(mark);
            }
        }

        // ------------------------------------------------------------------ mode, act
        /// <summary>Swap the whole building between its sleek and its decayed state. Call while the screen is hidden (blink, blackout).</summary>
        public void SetHorror(bool value)
        {
            horror = value; theme.SetHorror(value); ApplyAtmosphere(value);
            foreach (var g in horrorOnly) if (g) g.SetActive(value);
            foreach (var g in safeOnly) if (g) g.SetActive(!value);
            foreach (var l in lights) { l.Light.color = value ? new Color(1f, .40f, .30f) : new Color(1f, .95f, .85f); l.Light.intensity = value ? 3.6f : 2.3f; l.Light.range = value ? 10f : 8.5f; }
        }

        public void SetAct(InterviewRun run)
        {
            rooms.SetActive(run.IsMap);
            foreach (int i in new[] { 0, 1, 2 }) colleagues[i].Root.SetActive(run.Act == Act.Tour);
            monster.SetActive(run.IsChase);
            foreach (var target in targets) if (target) Object.Destroy(target);
            targets.Clear(); beacons.Clear();
            if (!run.IsMap) return;
            Vector2[] points = run.Act == Act.Tour ? new[] { map.Tour[Mathf.Min(3, run.TourStop)] } : map.Objectives(run.Act);
            int first = run.Act == Act.Tour ? 0 : run.Collected;
            // While a page is waiting for its answer the marker stays on that spot (so you know where to stand).
            for (int i = first; i < points.Length; i++)
            {
                bool current = i == first;
                targets.Add(BuildBeacon(points[i], current, run.Act == Act.Tour)); 
            }
        }

        GameObject BuildBeacon(Vector2 cell, bool current, bool tour)
        {
            var root = new GameObject(current ? "Objective (current)" : "Objective (later)"); root.transform.SetParent(rooms.transform, false); root.transform.position = Point(cell);
            // a floating page with a soft light; the current one also has a tall beam of light to the ceiling
            var page = Box(root.transform, "Page", new Vector3(0, 1.25f, 0), new Vector3(.3f, .4f, .01f), current ? pageMaterial : theme.Glow("Page later", "8A9A70", "5A6A50"));
            beacons.Add(page.transform);
            if (current)
            {
                var beam = Quad(root.transform, "Beam", new Vector3(0, 1.4f, 0), new Vector2(.7f, 2.8f), Quaternion.identity, beamMaterial); beam.AddComponent<Billboard>();
                var l = new GameObject("Page light").AddComponent<Light>(); l.transform.SetParent(root.transform, false); l.transform.localPosition = new Vector3(0, 1.3f, 0);
                l.type = LightType.Point; l.range = 4.5f; l.intensity = 2.2f; l.color = tour ? new Color(1f, .9f, .5f) : new Color(1f, .85f, .35f);
            }
            return root;
        }

        public sealed class Billboard : MonoBehaviour
        {
            void LateUpdate() { var c = Camera.main; if (!c) return; var d = transform.position - c.transform.position; d.y = 0; if (d.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(d, Vector3.up); }
        }

        // ------------------------------------------------------------------ per-frame
        /// <summary>Move the camera to the player (with walking bob/roll) and the pursuer to his cell; advance every animation.</summary>
        public void UpdateView(Vector2 player, Vector2 hunter, float yaw, float pitch, float dt = 0f, float time = 0f, float hunterSpeed = 0f, float talk = 0f, float anger = 0f, Vector3? cameraOffset = null, float roll = 0f)
        {
            Camera.transform.position = Point(player, EyeHeight) + (cameraOffset ?? Vector3.zero); Camera.transform.rotation = Quaternion.Euler(pitch, yaw, roll);
            Camera.rect = TerminalUI.Viewport(); Camera.aspect = 16f / 9f;
            monster.transform.position = Point(hunter, 1.3f);
            // Vale always faces the camera (flat actor)
            foreach (var actor in new[] { Vale.Root.transform }) { var d = Camera.transform.position - actor.position; d.y = 0; if (d.sqrMagnitude > .001f) actor.rotation = Quaternion.LookRotation(-d, Vector3.up); }
            foreach (var c in colleagues) if (c.Root.activeSelf) { var d = Camera.transform.position - c.Root.transform.position; d.y = 0; if (d.sqrMagnitude > .001f) c.Root.transform.rotation = Quaternion.LookRotation(-d, Vector3.up); }
            if (dt <= 0) return;
            lastHunterSpeed = hunterSpeed;
            Vale.Scary = true; Vale.Tick(dt, hunterSpeed, talk, anger, Camera.transform.position);
            foreach (var c in colleagues) if (c.Root.activeSelf) c.Tick(dt, 0f, 0f, 0f, Camera.transform.position);
            // doors swing toward their state; smoothing makes them feel heavy
            foreach (var v in doors)
            {
                v.Smoothed = Mathf.MoveTowards(v.Smoothed, v.Door.Open, dt * 4.5f);
                v.Hinge.localRotation = Quaternion.Euler(0, v.Direction * Mathf.SmoothStep(0, 1, v.Smoothed) * 105f, 0);
            }
            // lights: only the player's room (and nothing else) casts shadows; horror flickers
            int room = OfficeMap.RoomIndex(player);
            if (room != playerRoom) { playerRoom = room; foreach (var l in lights) l.Light.shadows = l.Room == room ? LightShadows.Hard : LightShadows.None; }
            foreach (var l in lights)
            {
                float n = Mathf.PerlinNoise(time * (horror ? 9f : 3f), l.Seed), dip = horror ? (n > .78f ? .45f : n > .66f ? .75f : 1f) : (n > .93f ? .85f : 1f);
                float baseIntensity = horror ? 3.6f : 2.3f; l.Light.intensity = baseIntensity * dip;
                l.Lamp.SetColor("_BaseColor", Color.Lerp(horror ? new Color(.25f, .05f, .03f) : new Color(.4f, .4f, .36f), horror ? new Color(1f, .35f, .25f) : new Color(1f, .97f, .85f), dip));
            }
            foreach (var (renderer, red) in furnisher.Leds) if (renderer) renderer.enabled = Mathf.PerlinNoise(time * 4f, renderer.GetInstanceID() * .01f) > (red ? .35f : .12f);
            for (int i = 0; i < beacons.Count; i++) if (beacons[i]) { var p = beacons[i].localPosition; p.y = 1.25f + Mathf.Sin(time * 2f + i) * .08f; beacons[i].localPosition = p; beacons[i].Rotate(0, 60f * dt, 0, Space.Self); }
        }

        public void Dispose()
        {
            Knife?.Dispose(); text.Dispose(); theme.Dispose();
            foreach (var c in colleagues) c.Dispose(); Vale?.Dispose();
            foreach (var m in loose) if (m) Object.Destroy(m);
            foreach (var m in meshes) if (m) Object.Destroy(m);
            if (beamTexture) Object.Destroy(beamTexture);
            if (rooms) Object.Destroy(rooms);
        }
    }
}
