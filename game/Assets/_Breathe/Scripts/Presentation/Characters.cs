using System.Collections.Generic;
using UnityEngine;

namespace Breathe
{
    /// <summary>Small pixel painter used for the character and prop sprites (origin bottom-left, point filtered).</summary>
    public sealed class PixelCanvas
    {
        public readonly int W, H; public readonly Color32[] Px;
        public PixelCanvas(int w, int h) { W = w; H = h; Px = new Color32[w * h]; }
        public void Clear() { System.Array.Clear(Px, 0, Px.Length); }
        public void Set(int x, int y, Color32 c) { if (x >= 0 && x < W && y >= 0 && y < H) Px[y * W + x] = c; }
        public Color32 Get(int x, int y) => x >= 0 && x < W && y >= 0 && y < H ? Px[y * W + x] : default;
        public void Rect(int x, int y, int w, int h, Color32 c) { for (int a = x; a < x + w; a++) for (int b = y; b < y + h; b++) Set(a, b, c); }
        public void Circle(int cx, int cy, int r, Color32 c) { for (int a = cx - r; a <= cx + r; a++) for (int b = cy - r; b <= cy + r; b++) if ((a - cx) * (a - cx) + (b - cy) * (b - cy) <= r * r) Set(a, b, c); }
        public void Ellipse(int cx, int cy, int rx, int ry, Color32 c) { for (int a = cx - rx; a <= cx + rx; a++) for (int b = cy - ry; b <= cy + ry; b++) { float u = (a - cx) / (float)rx, v = (b - cy) / (float)ry; if (u * u + v * v <= 1) Set(a, b, c); } }
        public void Line(int x0, int y0, int x1, int y1, Color32 c, int thick = 1)
        {
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx - dy;
            while (true)
            {
                for (int a = 0; a < thick; a++) for (int b = 0; b < thick; b++) Set(x0 + a - thick / 2, y0 + b - thick / 2, c);
                if (x0 == x1 && y0 == y1) break; int e2 = 2 * err; if (e2 > -dy) { err -= dy; x0 += sx; } if (e2 < dx) { err += dx; y0 += sy; }
            }
        }
        /// <summary>Fill a convex or simple polygon (scanline).</summary>
        public void Poly(Vector2[] p, Color32 c)
        {
            float minY = float.MaxValue, maxY = float.MinValue; foreach (var v in p) { minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y); }
            for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
            {
                var xs = new List<float>();
                for (int i = 0; i < p.Length; i++) { var a = p[i]; var b = p[(i + 1) % p.Length]; if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y)) xs.Add(a.x + (y - a.y) / (b.y - a.y) * (b.x - a.x)); }
                xs.Sort(); for (int i = 0; i + 1 < xs.Count; i += 2) for (int x = Mathf.RoundToInt(xs[i]); x <= Mathf.RoundToInt(xs[i + 1]); x++) Set(x, y, c);
            }
        }
        /// <summary>Dark outline around everything painted (1 px).</summary>
        public void Outline(Color32 c)
        {
            var copy = (Color32[])Px.Clone();
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                if (copy[y * W + x].a > 0) continue;
                bool edge = (x > 0 && copy[y * W + x - 1].a > 0) || (x < W - 1 && copy[y * W + x + 1].a > 0) || (y > 0 && copy[(y - 1) * W + x].a > 0) || (y < H - 1 && copy[(y + 1) * W + x].a > 0);
                if (edge) Px[y * W + x] = c;
            }
        }
        /// <summary>Shade the left side lighter and the right side darker using a checker dither (cheap cel shading).</summary>
        public void Shade(int bandLeft, int bandRight)
        {
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                var c = Px[y * W + x]; if (c.a == 0 || (c.r < 30 && c.g < 30 && c.b < 40)) continue;
                int d = x < bandLeft ? 1 : x > W - bandRight ? -1 : 0; if (d == 0 || ((x + y) & 1) == 0) continue;
                float k = d > 0 ? 1.12f : .8f; Px[y * W + x] = new Color32((byte)Mathf.Min(255, c.r * k), (byte)Mathf.Min(255, c.g * k), (byte)Mathf.Min(255, c.b * k), c.a);
            }
        }
        public void CopyFrom(PixelCanvas other) { System.Array.Copy(other.Px, Px, Mathf.Min(Px.Length, other.Px.Length)); }
        public Texture2D ToTexture(List<Texture2D> track, string name = "Sprite")
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.SetPixelData(Px, 0); t.Apply(); track?.Add(t); return t;
        }
        public void Upload(Texture2D t) { t.SetPixelData(Px, 0); t.Apply(false); }
    }

    /// <summary>A character's look: clothing colours (the head is drawn separately and animated).</summary>
    public sealed class Outfit
    {
        public Color32 Suit, SuitDark, Shirt, Tie, Pants, Shoes, Skin, Hair;
        public bool RaisedRight;     // arm raised (Vale holds the pen-knife)
        public static Color32 H(string hex) => TerminalUI.Hex(hex);
        public static Outfit Colleague(int index)
        {
            var o = new Outfit { Shirt = H("ECE7D6"), Pants = H("1b2236"), Shoes = H("0c0f1a"), Skin = H("E4B38D") };
            switch (index)
            {
                case 0: o.Suit = H("B33A4E"); o.Tie = H("2E60B5"); o.Hair = H("6B3E2A"); break;       // Mina
                case 1: o.Suit = H("34796A"); o.Tie = H("E5C14A"); o.Hair = H("2a2a33"); break;       // Len
                default: o.Suit = H("65559A"); o.Tie = H("DDE2EA"); o.Hair = H("8a5a35"); break;      // Iris
            }
            o.SuitDark = Dim(o.Suit, .74f); return o;
        }
        public static Outfit Vale(bool scary)
        {
            var o = new Outfit { RaisedRight = true, Shirt = H("ECE7D6"), Shoes = H("0a0a0e") };
            if (scary) { o.Suit = H("2a1620"); o.SuitDark = H("170b12"); o.Tie = H("E0242C"); o.Pants = H("100b14"); o.Skin = H("D8D69A"); o.Hair = H("0b0b10"); }
            else { o.Suit = H("2d3347"); o.SuitDark = H("1d2233"); o.Tie = H("C42B33"); o.Pants = H("1b2236"); o.Skin = H("E4B38D"); o.Hair = H("1a1a22"); }
            return o;
        }
        public static Color32 Dim(Color32 c, float k) => new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
    }

    /// <summary>Procedural character body frames (a real walk cycle) and animated heads.</summary>
    public static class CharacterArt
    {
        public const int W = 128, H = 192, Frames = 8, HeadW = 96, HeadH = 80, HeadBase = 172, TotalPx = HeadBase + HeadH;
        public static readonly Color32 Ink = new Color32(14, 16, 28, 255);
        /// <summary>Where the raised hand sits on the body frame, as a fraction of the body quad (0..1).</summary>
        public static readonly Vector2 HandUV = new Vector2(104f / W, 150f / H);

        /// <summary>Draw the body (no head) for walk phase 0..1. Feet alternate lifting, arms swing, the torso leans a little: it walks, it does not bob.</summary>
        public static void DrawBody(PixelCanvas c, Outfit o, float phase)
        {
            c.Clear();
            float s = Mathf.Sin(phase * Mathf.PI * 2f), k = Mathf.Cos(phase * Mathf.PI * 2f);
            int liftL = Mathf.RoundToInt(Mathf.Max(0, s) * 14f), liftR = Mathf.RoundToInt(Mathf.Max(0, -s) * 14f);
            int stepL = Mathf.RoundToInt(-s * 3f), stepR = Mathf.RoundToInt(s * 3f), lean = Mathf.RoundToInt(s * 2.5f);
            // legs and shoes
            DrawLeg(c, o, 44 + stepL, liftL, true); DrawLeg(c, o, 84 + stepR, liftR, false);
            int hipY = 78 + Mathf.RoundToInt((liftL + liftR) * .15f);
            // torso (jacket) with shoulders, lapels, shirt, tie
            c.Poly(new[] { new Vector2(30 + lean, hipY), new Vector2(98 + lean, hipY), new Vector2(104 + lean, 150), new Vector2(98 + lean, 166), new Vector2(30 + lean, 166), new Vector2(24 + lean, 150) }, o.Suit);
            c.Rect(24 + lean, hipY, 6, 72, o.SuitDark); c.Rect(98 + lean, hipY, 6, 72, o.SuitDark);
            c.Poly(new[] { new Vector2(52 + lean, 166), new Vector2(76 + lean, 166), new Vector2(64 + lean, 112) }, o.Shirt);
            c.Poly(new[] { new Vector2(48 + lean, 166), new Vector2(58 + lean, 166), new Vector2(60 + lean, 120), new Vector2(40 + lean, 130) }, o.SuitDark);
            c.Poly(new[] { new Vector2(80 + lean, 166), new Vector2(70 + lean, 166), new Vector2(68 + lean, 120), new Vector2(88 + lean, 130) }, o.SuitDark);
            c.Poly(new[] { new Vector2(60 + lean, 160), new Vector2(68 + lean, 160), new Vector2(70 + lean, 118), new Vector2(64 + lean, 100), new Vector2(58 + lean, 118) }, o.Tie);
            c.Rect(60 + lean, 154, 8, 8, o.Tie); c.Rect(30 + lean, hipY, 68, 5, Outfit.Dim(o.Pants, .8f));
            for (int i = 0; i < 3; i++) c.Circle(64 + lean, 96 - i * 8 + 20, 1, new Color32(220, 214, 190, 255));          // buttons
            c.Rect(36 + lean, 140, 9, 3, o.Shirt);                                                                       // pocket square
            // neck
            c.Rect(54 + lean, 164, 20, 14, Outfit.Dim(o.Skin, .86f));
            // arms: left swings opposite the left foot, right is raised (Vale) or swings the other way
            int swingL = Mathf.RoundToInt(k * 7f), swingR = Mathf.RoundToInt(-k * 7f);
            DrawArm(c, o, 14 + lean, 160, 8 + swingL, true);
            if (o.RaisedRight)
            {   // forearm up, hand holding the pen-knife near head height
                c.Poly(new[] { new Vector2(98 + lean, 164), new Vector2(112 + lean, 160), new Vector2(116 + lean, 130), new Vector2(104 + lean, 128) }, o.Suit);
                c.Poly(new[] { new Vector2(102 + lean, 138), new Vector2(116 + lean, 134), new Vector2(112 + lean, 154), new Vector2(104 + lean, 152) }, o.Suit);
                c.Rect(103 + lean, 146, 10, 4, Outfit.Dim(o.Suit, .8f));
                c.Circle(108 + lean, 150, 7, o.Skin); c.Rect(104 + lean, 143, 9, 5, Outfit.Dim(o.Skin, .8f));
            }
            else DrawArm(c, o, 114 + lean, 160, 8 + swingR, false);
            c.Shade(40, 36); c.Outline(Ink);
        }

        static void DrawLeg(PixelCanvas c, Outfit o, int cx, int lift, bool left)
        {
            int top = 84, foot = 4 + lift;
            c.Rect(cx - 12, foot + 8, 24, top - foot - 8, o.Pants); c.Rect(cx - 12, foot + 8, 5, top - foot - 8, Outfit.Dim(o.Pants, .8f)); c.Rect(cx + 8, foot + 8, 4, top - foot - 8, Outfit.Dim(o.Pants, 1.2f));
            c.Rect(cx - 2, foot + 12, 4, top - foot - 14, Outfit.Dim(o.Pants, .88f));
            c.Ellipse(cx + (left ? -3 : 3), foot + 5, 17, 7, o.Shoes); c.Rect(cx - 12, foot + 1, 26, 3, new Color32(20, 20, 26, 255));
            c.Rect(cx + (left ? -12 : -4), foot + 8, 12, 2, new Color32(90, 90, 100, 255));
        }

        static void DrawArm(PixelCanvas c, Outfit o, int x, int shoulderY, int swing, bool left)
        {
            int handY = 76 + swing;
            c.Poly(new[] { new Vector2(x - 6, shoulderY), new Vector2(x + 8, shoulderY), new Vector2(x + 7, handY + 8), new Vector2(x - 7, handY + 8) }, o.Suit);
            c.Rect(x - 6, shoulderY - 56, 4, 56, o.SuitDark);
            c.Rect(x - 6, handY + 6, 14, 3, o.Shirt); c.Circle(x, handY, 7, o.Skin);
        }

        // ------------------------------------------------------------------ heads
        public sealed class FaceState
        {
            public float Talk;        // 0..1 jaw
            public float Anger;       // 0..1
            public float Glitch;      // 0..1 how torn the picture is
            public int Variant;       // alternate face (0 = normal)
            public Vector2 Look;      // eye offset, -1..1
            public bool Blink;
            public float Time;
            public bool Scary;
        }

        /// <summary>Safe Vale/colleagues: a warm human face. Scary Vale: a CRT television head whose screen shows a grinning, glitching face (Omega Flowey).</summary>
        public static void DrawHead(PixelCanvas c, Outfit o, FaceState f, System.Random rng)
        {
            c.Clear();
            if (f.Scary) DrawTv(c, f, rng); else DrawHuman(c, o, f);
        }

        static void DrawHuman(PixelCanvas c, Outfit o, FaceState f)
        {
            int cx = HeadW / 2, cy = 34;
            c.Ellipse(cx, cy, 25, 28, o.Skin); c.Rect(cx - 24, cy - 4, 4, 14, Outfit.Dim(o.Skin, .86f));
            c.Ellipse(cx, cy + 22, 27, 12, o.Hair); c.Rect(cx - 27, cy + 8, 7, 18, o.Hair); c.Rect(cx + 20, cy + 8, 7, 18, o.Hair);
            int ex = Mathf.RoundToInt(f.Look.x * 2), ey = Mathf.RoundToInt(f.Look.y * 1);
            int browLift = Mathf.RoundToInt(Mathf.Sin(f.Time * 1.3f) * 1f - f.Anger * 4);
            for (int side = -1; side <= 1; side += 2)
            {
                int bx = cx + side * 11;
                if (f.Blink) c.Rect(bx - 5, cy + 3, 10, 2, Ink);
                else { c.Ellipse(bx, cy + 3, 6, 5, Color.white); c.Circle(bx + ex, cy + 3 + ey, 2, Ink); }
                c.Line(bx - 7, cy + 12 + browLift * side * 0 + (f.Anger > .3f ? (side < 0 ? -1 : 1) * 0 : 0), bx + 7, cy + 12 - (f.Anger > .3f ? side * 3 : 0) + browLift, Ink, 2);
            }
            c.Rect(cx - 1, cy - 4, 3, 8, Outfit.Dim(o.Skin, .8f));
            int mouth = Mathf.RoundToInt(f.Talk * 9);
            if (mouth < 2) { c.Line(cx - 8, cy - 14, cx + 8, cy - 14 + (f.Anger > .3f ? -1 : 1), Ink, 2); if (f.Anger < .3f) { c.Rect(cx - 8, cy - 13, 2, 1, Ink); c.Rect(cx + 7, cy - 13, 2, 1, Ink); } }
            else { c.Ellipse(cx, cy - 14, 9, mouth / 2 + 1, new Color32(40, 8, 14, 255)); c.Rect(cx - 7, cy - 14 + mouth / 2 - 1, 14, 2, Color.white); }
            c.Outline(Ink);
        }

        static readonly Color32 TvBrown = new Color32(98, 66, 48, 255), TvDark = new Color32(60, 40, 30, 255), TvLight = new Color32(140, 98, 70, 255), Screen = new Color32(236, 236, 228, 255), Black = new Color32(10, 8, 14, 255), Red = new Color32(214, 30, 40, 255), Green = new Color32(120, 220, 90, 255);

        static void DrawTv(PixelCanvas c, FaceState f, System.Random rng)
        {
            int cx = HeadW / 2;
            // the box
            c.Rect(cx - 40, 2, 80, 70, TvBrown); c.Rect(cx - 40, 62, 80, 10, TvLight); c.Rect(cx - 40, 2, 80, 6, TvDark); c.Rect(cx + 32, 2, 8, 70, TvDark);
            c.Poly(new[] { new Vector2(cx - 40, 72), new Vector2(cx - 30, 78), new Vector2(cx + 30, 78), new Vector2(cx + 40, 72) }, TvLight);
            for (int i = 0; i < 4; i++) c.Rect(cx + 20 + i * 4 - 4, 6, 2, 8, Black);                                 // vents
            c.Circle(cx - 30, 8, 2, Red);
            // the screen
            int sx = cx - 32, sy = 14, sw = 64, sh = 52;
            Color32 screen = f.Anger > .6f ? Color32.Lerp(Screen, new Color32(255, 150, 140, 255), (f.Anger - .6f) * 2f) : Screen;
            c.Rect(sx - 2, sy - 2, sw + 4, sh + 4, Black); c.Rect(sx, sy, sw, sh, screen);
            DrawFace(c, f, sx, sy, sw, sh, rng);
            // glitch: slices of the picture tear sideways and RGB-split
            if (f.Glitch > .05f)
            {
                int slices = 1 + Mathf.RoundToInt(f.Glitch * 4);
                for (int i = 0; i < slices; i++)
                {
                    int y0 = sy + rng.Next(sh - 6), h = 2 + rng.Next(5), shift = rng.Next(-8, 9);
                    for (int y = y0; y < y0 + h; y++) { var row = new Color32[sw]; for (int x = 0; x < sw; x++) row[x] = c.Get(sx + ((x - shift) % sw + sw) % sw, y); for (int x = 0; x < sw; x++) c.Set(sx + x, y, row[x]); }
                }
                for (int i = 0; i < 20 + (int)(f.Glitch * 60); i++) c.Set(sx + rng.Next(sw), sy + rng.Next(sh), rng.Next(3) == 0 ? Red : Black);
            }
            // static scanlines on the glass
            for (int y = sy; y < sy + sh; y += 3) for (int x = sx; x < sx + sw; x++) { var p = c.Get(x, y); c.Set(x, y, new Color32((byte)(p.r * .86f), (byte)(p.g * .86f), (byte)(p.b * .86f), 255)); }
            c.Outline(Ink);
        }

        static void DrawFace(PixelCanvas c, FaceState f, int sx, int sy, int sw, int sh, System.Random rng)
        {
            int mx = sx + sw / 2, my = sy + sh / 2; int ex = Mathf.RoundToInt(f.Look.x * 3), ey = Mathf.RoundToInt(f.Look.y * 2);
            int eyeY = my + 12, spread = 16, size = 7 + Mathf.RoundToInt(f.Anger * 3);
            switch (f.Variant)
            {
                case 1: // X eyes, flat mouth
                    for (int side = -1; side <= 1; side += 2) { int bx = mx + side * spread; c.Line(bx - 5, eyeY - 5, bx + 5, eyeY + 5, Black, 2); c.Line(bx - 5, eyeY + 5, bx + 5, eyeY - 5, Black, 2); }
                    c.Line(mx - 18, my - 12, mx + 18, my - 12, Black, 2); return;
                case 2: // three eyes
                    for (int i = -1; i <= 1; i++) { int bx = mx + i * 20; c.Circle(bx, eyeY, 7, Black); c.Circle(bx, eyeY, 5, Red); c.Circle(bx + ex, eyeY + ey, 2, Green); }
                    c.Rect(mx - 22, my - 14, 44, 4, Black); for (int i = 0; i < 9; i++) c.Rect(mx - 22 + i * 5, my - 14, 2, 4, Screen); return;
                case 3: // one huge eye
                    c.Circle(mx, my + 2, 17, Black); c.Circle(mx, my + 2, 14, Red); c.Circle(mx + ex * 2, my + 2 + ey * 2, 8, Green); c.Circle(mx + ex * 2, my + 2 + ey * 2, 3, Black); return;
            }
            // the signature look: ringed red eyes with green centres and a crooked glow
            for (int side = -1; side <= 1; side += 2)
            {
                int bx = mx + side * spread, by = eyeY + (side > 0 ? 2 : 0);
                if (f.Blink) { c.Rect(bx - size, by, size * 2, 2, Black); continue; }
                c.Circle(bx, by, size, Black); c.Circle(bx, by, size - 2, Red); c.Circle(bx + ex, by + ey, Mathf.Max(2, size / 2), Green); c.Circle(bx + ex, by + ey, 1, Black);
                for (int a = 0; a < 6; a++) { float ang = a * 1.047f + f.Time; c.Line(bx, by, bx + Mathf.RoundToInt(Mathf.Cos(ang) * (size + 3)), by + Mathf.RoundToInt(Mathf.Sin(ang) * (size + 3)), Black, 1); }
                // brows slam down when angry
                int browY = by + size + 4 - Mathf.RoundToInt(f.Anger * 5);
                c.Line(bx - size - 2, browY + (side < 0 ? 0 : 0) + Mathf.RoundToInt(f.Anger * side * 4), bx + size + 2, browY - Mathf.RoundToInt(f.Anger * side * 4), Black, 2);
            }
            // the grin: wide, toothy, opening with speech
            int open = Mathf.RoundToInt(f.Talk * 14) + Mathf.RoundToInt(f.Anger * 4);
            int mw = 24 + Mathf.RoundToInt(f.Anger * 6);
            c.Ellipse(mx, my - 10, mw, 6 + open, Black);
            c.Rect(mx - mw, my - 10 + 1, mw * 2 + 1, 2, Black);
            for (int t = -mw + 3; t < mw - 2; t += 5) { c.Rect(mx + t, my - 10 + 1 + (open > 3 ? 0 : 0), 3, Mathf.Min(7, 4 + open / 2), Screen); if (open > 5) c.Rect(mx + t, my - 10 - 6 - open, 3, 4, Screen); }
            if (open > 6) c.Ellipse(mx, my - 10 - open, mw / 2, 3, Red);                                       // tongue when screaming
            c.Line(mx - mw - 2, my - 8, mx - mw - 6, my - 2, Black, 2); c.Line(mx + mw + 2, my - 8, mx + mw + 6, my - 2, Black, 2);
        }
    }

    /// <summary>
    /// A walking, talking, glitching Mr. Vale. A body quad (8-frame walk cycle atlas) plus a head quad whose texture is redrawn ~16 times a second.
    /// Colleagues reuse the same rig with static faces.
    /// </summary>
    public sealed class ActorSprite
    {
        public readonly GameObject Root;
        readonly Transform bodyQuad, headQuad;
        readonly Material bodyMaterial, headMaterial;
        readonly Texture2D atlas, headTexture;
        readonly PixelCanvas headCanvas = new PixelCanvas(CharacterArt.HeadW, CharacterArt.HeadH);
        readonly Outfit outfit;
        readonly CharacterArt.FaceState face = new CharacterArt.FaceState();
        readonly System.Random rng;
        readonly float height, width, px;
        float walkPhase, faceClock, blinkTimer, lookTimer, glitchTimer, variantTimer, glitchLevel, lookGoal;
        int frameShown = -1;
        public bool Scary { get => face.Scary; set { if (face.Scary != value) { face.Scary = value; glitchLevel = 1f; } } }
        /// <summary>Where the raised hand is, in the actor's local space (the pen-knife attaches here).</summary>
        public Vector3 HandLocal => new Vector3((CharacterArt.HandUV.x - .5f) * width, BodyBottom + CharacterArt.HandUV.y * BodyHeight, -.05f);
        public float BodyHeight => CharacterArt.H * px;
        public float BodyBottom => -height / 2f;
        public Renderer BodyRenderer => bodyQuad.GetComponent<Renderer>();

        public ActorSprite(string name, Transform parent, Outfit outfit, bool scary, float height, List<Material> materialsTrack, List<Texture2D> texturesTrack, int seed = 5)
        {
            this.outfit = outfit; this.height = height; px = height / CharacterArt.TotalPx; width = CharacterArt.W * px; rng = new System.Random(seed); face.Scary = scary;
            Root = new GameObject(name); Root.transform.SetParent(parent, false);
            // atlas of the walk cycle: 4 x 2 frames
            var body = new PixelCanvas(CharacterArt.W, CharacterArt.H); var sheet = new PixelCanvas(CharacterArt.W * 4, CharacterArt.H * 2);
            for (int i = 0; i < CharacterArt.Frames; i++)
            {
                CharacterArt.DrawBody(body, outfit, i / (float)CharacterArt.Frames);
                int ox = (i % 4) * CharacterArt.W, oy = (i / 4) * CharacterArt.H;
                for (int y = 0; y < CharacterArt.H; y++) for (int x = 0; x < CharacterArt.W; x++) sheet.Px[(oy + y) * sheet.W + ox + x] = body.Px[y * body.W + x];
            }
            atlas = sheet.ToTexture(texturesTrack, name + " walk atlas");
            headTexture = headCanvas.ToTexture(texturesTrack, name + " face");
            float bodyH = BodyHeight;
            bodyQuad = Quad("Body", new Vector3(0, BodyBottom + bodyH / 2f, 0), new Vector2(width, bodyH), atlas, out bodyMaterial, materialsTrack);
            bodyMaterial.mainTextureScale = new Vector2(.25f, .5f); SetFrame(0);
            float headH = CharacterArt.HeadH * px, headW = CharacterArt.HeadW * px;
            headQuad = Quad("Head", new Vector3(0, BodyBottom + CharacterArt.HeadBase * px + headH / 2f, -.01f), new Vector2(headW, headH), headTexture, out headMaterial, materialsTrack);
            Redraw();
        }

        Transform Quad(string name, Vector3 pos, Vector2 size, Texture2D tex, out Material material, List<Material> track)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name; Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(Root.transform, false); go.transform.localPosition = pos; go.transform.localScale = new Vector3(size.x, size.y, 1);
            var template = Resources.Load<Material>("OfficeSprite") ?? Resources.Load<Material>("OfficeSurface");
            material = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", tex); material.mainTexture = tex; material.SetFloat("_AlphaClip", 1); material.EnableKeyword("_ALPHATEST_ON"); material.SetFloat("_Cull", 0); material.renderQueue = 2450;
            go.GetComponent<Renderer>().sharedMaterial = material; track?.Add(material); return go.transform;
        }

        void SetFrame(int i)
        {
            if (i == frameShown) return; frameShown = i;
            bodyMaterial.mainTextureOffset = new Vector2((i % 4) * .25f, (i / 4) * .5f);
        }

        void Redraw() { CharacterArt.DrawHead(headCanvas, outfit, face, rng); headCanvas.Upload(headTexture); }

        /// <summary>Advance animation. speed is metres per second (0 = standing). talk is the voice loudness 0..1, anger 0..1.</summary>
        public void Tick(float dt, float speed, float talk, float anger, Vector3 cameraPosition)
        {
            if (dt <= 0) return;
            if (speed > .05f) walkPhase = (walkPhase + dt * Mathf.Clamp(speed, .5f, 4f) * .62f) % 1f; else walkPhase = Mathf.MoveTowards(walkPhase, 0f, dt * 2f);
            SetFrame(speed > .05f || walkPhase > .02f ? Mathf.FloorToInt(walkPhase * CharacterArt.Frames) % CharacterArt.Frames : 0);
            // sideways sway while walking (no vertical bobbing)
            float sway = speed > .05f ? Mathf.Sin(walkPhase * Mathf.PI * 2f) * .03f : 0f;
            bodyQuad.localPosition = new Vector3(sway, bodyQuad.localPosition.y, 0); headQuad.localPosition = new Vector3(sway * 1.4f, headQuad.localPosition.y, headQuad.localPosition.z);
            faceClock += dt; if (faceClock < 1f / 16f) return; float step = faceClock; faceClock = 0;
            face.Time += step; face.Talk = Mathf.Lerp(face.Talk, Mathf.Clamp01(talk * 1.4f), .6f); face.Anger = Mathf.Lerp(face.Anger, Mathf.Clamp01(anger), .25f);
            blinkTimer -= step; if (blinkTimer <= 0) { face.Blink = !face.Blink; blinkTimer = face.Blink ? .12f : 1.2f + (float)rng.NextDouble() * 2.5f; }
            lookTimer -= step; if (lookTimer <= 0) { lookGoal = ((float)rng.NextDouble() - .5f) * 2f; lookTimer = .25f + (float)rng.NextDouble() * 1.2f; }
            Vector3 toCam = cameraPosition - Root.transform.position; float side = Vector3.Dot(toCam.normalized, Root.transform.right);
            face.Look = new Vector2(Mathf.Lerp(face.Look.x, Mathf.Clamp(lookGoal * .6f + side, -1, 1), .5f), Mathf.Lerp(face.Look.y, 0f, .2f));
            glitchLevel = Mathf.MoveTowards(glitchLevel, face.Scary ? .12f + face.Anger * .45f : 0f, step * 1.4f);
            glitchTimer -= step; if (glitchTimer <= 0 && face.Scary) { glitchLevel = Mathf.Max(glitchLevel, .5f + face.Anger * .4f); glitchTimer = Mathf.Lerp(1.6f, .45f, face.Anger) + (float)rng.NextDouble(); }
            variantTimer -= step;
            if (variantTimer <= 0 && face.Scary) { face.Variant = face.Variant == 0 && rng.Next(3) == 0 ? 1 + rng.Next(3) : 0; variantTimer = face.Variant == 0 ? 1.2f + (float)rng.NextDouble() * 3.5f : .18f; }
            else if (!face.Scary) face.Variant = 0;
            face.Glitch = glitchLevel;
            Redraw();
        }

        public void Dispose() { if (Root) Object.Destroy(Root); }
    }
}
