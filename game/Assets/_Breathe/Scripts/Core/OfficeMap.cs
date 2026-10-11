using System.Collections.Generic;
using UnityEngine;

namespace Breathe
{
    public enum DoorEvent { None, Opened, Burst }

    /// <summary>A door in a dividing wall. Closed doors block walking, pathing speed and line of sight; they auto-close behind people.</summary>
    public sealed class Door
    {
        public const float PlayerOpenSeconds = .35f, VigilSeconds = 3f, CloseSeconds = .6f;
        public int Id;
        public Vector2Int Cell;
        /// <summary>True when the wall runs north-south, so people pass through the door travelling along x.</summary>
        public bool PassesAlongX;
        public float Open;                 // 0 = closed, 1 = fully open
        public float Idle;                 // seconds nobody has been near
        public bool Opening;
        public Vector2 Center => new Vector2(Cell.x + .5f, Cell.y + .5f);
        public bool Blocks => Open < .8f;
        public bool BlocksSight => Open < .5f;
    }

    /// <summary>A solid prop footprint (desks, shelves, racks...). The world renderer builds a themed model for each kind.</summary>
    public struct FurnitureBlock
    {
        public string Kind; public RectInt Rect; public int Room;
        public FurnitureBlock(string kind, RectInt rect, int room) { Kind = kind; Rect = rect; Room = room; }
    }

    /// <summary>
    /// The office: a 3 x 3 building of nine distinct rooms joined by doors. Pure C# (no scene dependency) so doors, pathing and sight are unit-tested.
    /// Rows of rooms run south to north in y; room index = row * 3 + column.
    /// </summary>
    public sealed class OfficeMap
    {
        public const int Width = 31, Height = 28, RoomW = 10, RoomH = 9;
        public static readonly string[] RoomNames = { "RECEPTION", "OPEN PLAN", "BREAK ROOM", "ARCHIVE", "ATRIUM", "CONFERENCE", "HR OFFICE", "SERVER ROOM", "INTERVIEW SUITE" };
        readonly bool[,] solid = new bool[Width, Height];
        readonly bool[,] furniture = new bool[Width, Height];
        readonly Door[,] doorAt = new Door[Width, Height];
        public readonly List<Door> Doors = new List<Door>();
        public readonly List<FurnitureBlock> Blocks = new List<FurnitureBlock>();
        public readonly Queue<(DoorEvent kind, Vector2 where)> Events = new Queue<(DoorEvent, Vector2)>();

        public readonly Vector2 Spawn = new Vector2(2.5f, 5.5f);
        public readonly Vector2 Exit = new Vector2(28.5f, 25.5f);
        public Vector2 HunterSpawn => Exit;
        public readonly Vector2[] Tour = { new Vector2(5.5f, 4.5f), new Vector2(15.5f, 4.5f), new Vector2(5.5f, 22.5f), new Vector2(28.5f, 25.5f) };
        public readonly Vector2[] Challenge = { new Vector2(28.5f, 4.5f), new Vector2(22.5f, 16.5f), new Vector2(15.5f, 25.5f) };
        public readonly Vector2[] Team = { new Vector2(5.5f, 22.5f), new Vector2(15.5f, 4.5f) };
        public readonly Vector2[] Gap = { new Vector2(3.5f, 16.5f) };
        public readonly Vector2[] Shelters = { new Vector2(1.5f, 7.5f), new Vector2(28.5f, 7.5f), new Vector2(8.5f, 10.5f), new Vector2(18.5f, 19.5f) };
        /// <summary>Places the hunter walks between while searching for you: the middle of every room.</summary>
        public readonly Vector2[] Patrol = { new Vector2(5.5f, 5.5f), new Vector2(15.5f, 5.5f), new Vector2(25.5f, 6.5f), new Vector2(5.5f, 12.5f), new Vector2(15.5f, 11.5f), new Vector2(25.5f, 14.5f),
                                             new Vector2(5.5f, 24.5f), new Vector2(15.5f, 19.5f), new Vector2(25.5f, 24.5f) };

        public OfficeMap()
        {
            for (int x = 0; x < Width; x++) { solid[x, 0] = true; solid[x, Height - 1] = true; }
            for (int y = 0; y < Height; y++) { solid[0, y] = true; solid[Width - 1, y] = true; }
            for (int y = 1; y < Height - 1; y++) { solid[10, y] = true; solid[20, y] = true; }
            for (int x = 1; x < Width - 1; x++) { solid[x, 9] = true; solid[x, 18] = true; }
            // Doors in the dividing walls (west-east on x=10/20, south-north on y=9/18).
            foreach (var p in new[] { (10, 4), (20, 6), (10, 13), (20, 12), (10, 22), (20, 23) }) AddDoor(p.Item1, p.Item2, true);
            foreach (var p in new[] { (4, 9), (16, 9), (26, 9), (7, 18), (14, 18), (23, 18) }) AddDoor(p.Item1, p.Item2, false);
            // Reception
            Fill("reception desk", 3, 2, 4, 1); Fill("sofa", 7, 6, 2, 1);
            // Open plan: four cubicle blocks
            Fill("cubicle", 12, 2, 3, 1); Fill("cubicle", 16, 2, 3, 1); Fill("cubicle", 12, 6, 3, 1); Fill("cubicle", 16, 6, 3, 1);
            // Break room
            Fill("counter", 22, 1, 5, 1); Fill("fridge", 28, 1, 1, 2); Fill("table", 24, 4, 2, 2);
            // Archive: shelving rows with aisles
            Fill("shelves", 2, 11, 5, 1); Fill("shelves", 2, 14, 5, 1); Fill("shelves", 8, 12, 1, 4);
            // Atrium
            Fill("planter", 14, 13, 3, 2);
            // Conference
            Fill("conference table", 23, 12, 5, 2);
            // HR
            Fill("hr desk", 2, 21, 3, 1); Fill("hr desk", 6, 24, 3, 1); Fill("filing", 1, 19, 1, 2);
            // Server room
            Fill("rack", 13, 21, 1, 5); Fill("rack", 17, 21, 1, 5);
            // Interview suite
            Fill("waiting chairs", 22, 22, 3, 1); Fill("plant", 27, 19, 1, 1);
            // Dressing: every room gets the things a real office has, as solid blocks so collision matches what you see.
            Fill("plant", 1, 1, 1, 1); Fill("cooler", 9, 1, 1, 1); Fill("plant", 9, 8, 1, 1);                       // reception
            Fill("copier", 19, 3, 1, 1); Fill("plant", 11, 1, 1, 1);                                                 // open plan
            Fill("vending", 21, 2, 1, 1); Fill("cooler", 21, 4, 1, 1);                                               // break room
            Fill("boxes", 1, 17, 2, 1);                                                                              // archive
            Fill("sofa", 11, 10, 2, 1); Fill("plant", 19, 10, 1, 1); Fill("plant", 19, 17, 1, 1);                    // atrium
            Fill("filing", 21, 10, 2, 1); Fill("plant", 29, 10, 1, 1);                                               // conference
            Fill("plant", 9, 19, 1, 1);                                                                              // HR
            Fill("boxes", 11, 26, 2, 1);                                                                             // server room
            Fill("cooler", 21, 19, 1, 1);                                                                            // interview suite
        }

        void AddDoor(int x, int y, bool alongX)
        {
            var door = new Door { Id = Doors.Count, Cell = new Vector2Int(x, y), PassesAlongX = alongX };
            Doors.Add(door); doorAt[x, y] = door; solid[x, y] = false;
        }

        void Fill(string kind, int x, int y, int w, int h)
        {
            for (int i = x; i < x + w; i++) for (int j = y; j < y + h; j++) { solid[i, j] = true; furniture[i, j] = true; }
            Blocks.Add(new FurnitureBlock(kind, new RectInt(x, y, w, h), RoomIndex(new Vector2(x + .5f, y + .5f))));
        }

        public static int RoomIndex(Vector2 p)
        {
            int c = Mathf.Clamp(Mathf.FloorToInt(p.x / RoomW), 0, 2), r = Mathf.Clamp(Mathf.FloorToInt(p.y / RoomH), 0, 2);
            return r * 3 + c;
        }
        public static string RoomName(Vector2 p) => RoomNames[RoomIndex(p)];
        public static Rect RoomRect(int index) => new Rect((index % 3) * RoomW + 1, (index / 3) * RoomH + 1, RoomW - 1, RoomH - 1);

        public Door DoorAt(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? doorAt[x, y] : null;
        public bool Furniture(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && furniture[x, y];
        /// <summary>Walls, furniture and CLOSED doors.</summary>
        public bool Solid(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return true;
            var door = doorAt[x, y];
            return door != null ? door.Blocks : solid[x, y];
        }
        /// <summary>Walls and furniture only: doors count as passable. Used for route planning, because someone can open a door.</summary>
        public bool PathSolid(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height || (doorAt[x, y] == null && solid[x, y]);

        public bool Walkable(Vector2 p, float radius = .24f) => !Solid(Mathf.FloorToInt(p.x - radius), Mathf.FloorToInt(p.y - radius)) && !Solid(Mathf.FloorToInt(p.x + radius), Mathf.FloorToInt(p.y - radius)) && !Solid(Mathf.FloorToInt(p.x - radius), Mathf.FloorToInt(p.y + radius)) && !Solid(Mathf.FloorToInt(p.x + radius), Mathf.FloorToInt(p.y + radius));
        public Vector2 Move(Vector2 p, Vector2 delta)
        {
            // Axis separation allows sliding along walls without passing through corners.
            Vector2 next = p + new Vector2(delta.x, 0); if (Walkable(next)) p = next;
            next = p + new Vector2(0, delta.y); if (Walkable(next)) p = next;
            return p;
        }

        /// <summary>True when no wall, desk or closed door blocks the straight line between two points.</summary>
        public bool HasLineOfSight(Vector2 a, Vector2 b)
        {
            float length = Vector2.Distance(a, b); int steps = Mathf.CeilToInt(length / .25f);
            for (int i = 1; i < steps; i++)
            {
                var p = Vector2.Lerp(a, b, (float)i / steps); int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
                var door = DoorAt(x, y);
                if (door != null ? door.BlocksSight : PathSolid(x, y)) return false;
            }
            return true;
        }

        public bool Sheltered(Vector2 p)
        { foreach (var point in Shelters) if (Vector2.Distance(point, p) < .9f) return true; return false; }
        public Vector2[] Objectives(Act act) => act == Act.Team ? Team : act == Act.Gap ? Gap : Challenge;

        // ------------------------------------------------------------------ doors
        /// <summary>People near a door keep it open; the player opens doors just by walking up to them. Advance every frame.</summary>
        public void UpdateDoors(float dt, Vector2 player, Vector2 hunter)
        {
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            dt = Mathf.Min(dt, .1f);
            foreach (var door in Doors)
            {
                float dp = Vector2.Distance(player, door.Center), dh = Vector2.Distance(hunter, door.Center);
                if (dp < 1.25f)
                {
                    if (door.Open <= 0f) Events.Enqueue((DoorEvent.Opened, door.Center));
                    door.Open = Mathf.Min(1f, door.Open + dt / Door.PlayerOpenSeconds); door.Idle = 0; door.Opening = true;
                }
                else if (dp < 1.9f || dh < 1.9f) door.Idle = 0;   // someone is still close: leave it as it is
                else
                {
                    door.Idle += dt;
                    if (door.Idle >= Door.VigilSeconds && door.Open > 0f) { door.Open = Mathf.Max(0f, door.Open - dt / Door.CloseSeconds); door.Opening = false; }
                }
            }
        }

        /// <summary>Open a door for the hunter. An instant (burst) open is for stressed players: he smashes through.</summary>
        public void HunterOpen(Door door, bool burst)
        {
            if (door == null || door.Open >= 1f) return;
            door.Open = 1f; door.Idle = 0; door.Opening = true;
            Events.Enqueue((burst ? DoorEvent.Burst : DoorEvent.Opened, door.Center));
        }

        public void CloseAllDoors() { foreach (var door in Doors) { door.Open = 0; door.Idle = 0; door.Opening = false; } Events.Clear(); }

        // ------------------------------------------------------------------ pathing
        public List<Vector2> FindPath(Vector2 from, Vector2 to)
        {
            var start = new Vector2Int(Mathf.FloorToInt(from.x), Mathf.FloorToInt(from.y));
            var end = new Vector2Int(Mathf.FloorToInt(to.x), Mathf.FloorToInt(to.y));
            var queue = new Queue<Vector2Int>(); var previous = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(start); previous[start] = start;
            var offsets = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue(); if (cell == end) break;
                foreach (var offset in offsets) { var next = cell + offset; if (PathSolid(next.x, next.y) || previous.ContainsKey(next)) continue; previous[next] = cell; queue.Enqueue(next); }
            }
            var path = new List<Vector2>(); if (!previous.ContainsKey(end)) return path;
            for (var cell = end; cell != start; cell = previous[cell]) path.Add(new Vector2(cell.x + .5f, cell.y + .5f));
            path.Reverse(); return path;
        }

        // ------------------------------------------------------------------ mini map
        public const int MiniScale = 4;
        /// <summary>Pixels (row-major, bottom row first) for the top-right mini map: walls, furniture, doors, objectives and shelters. Dynamic markers are drawn by the HUD.</summary>
        public Color32[] MiniMapStatic(out int pixelWidth, out int pixelHeight, Vector2[] objectives = null)
        {
            int pw = Width * MiniScale, ph = Height * MiniScale; pixelWidth = pw; pixelHeight = ph;
            var pixels = new Color32[pw * ph];
            var floor = new Color32(26, 30, 38, 235); var wall = new Color32(150, 160, 175, 255); var prop = new Color32(70, 80, 98, 255); var door = new Color32(240, 190, 70, 255); var shelter = new Color32(90, 150, 255, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = floor;
            void Cell(int cx, int cy, Color32 color, int inset = 0)
            {
                for (int a = inset; a < MiniScale - inset; a++) for (int b = inset; b < MiniScale - inset; b++) pixels[(cy * MiniScale + b) * pw + cx * MiniScale + a] = color;
            }
            for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++)
            {
                if (doorAt[x, y] != null) Cell(x, y, door, 0);
                else if (furniture[x, y]) Cell(x, y, prop, 1);
                else if (solid[x, y]) Cell(x, y, wall);
            }
            foreach (var s in Shelters) Cell(Mathf.FloorToInt(s.x), Mathf.FloorToInt(s.y), shelter, 1);
            return pixels;
        }
    }
}
