using System;
using System.Collections.Generic;
using UnityEngine;

namespace Breathe
{
    public enum FloorKind { Carpet, Tile, Checker, Wood, Concrete, Marble, Grate, Vinyl }
    public enum WallKind { Plain, Panels, Tiles, Blocks, Stripes, Wood }

    /// <summary>
    /// Procedural, point-filtered surface textures. Every surface has a SAFE version (clean, sleek office) and a HORROR version
    /// (the same surface weathered: stains, mold, peeling paint, cracks and drips). The two share a layout so swapping them reads as decay.
    /// </summary>
    public static class ProcTex
    {
        public const int W = 96, H = 128;

        // ------------------------------------------------------------------ noise
        static uint Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u; return h ^ (h >> 16);
        }
        static float Rand(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFF) / (float)0x1000000;
        static float Smooth(float t) => t * t * (3f - 2f * t);
        public static float Value(float x, float y, int seed, int period = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = Smooth(x - xi), fy = Smooth(y - yi);
            int Wrap(int v) => period > 0 ? ((v % period) + period) % period : v;
            float a = Rand(Wrap(xi), Wrap(yi), seed), b = Rand(Wrap(xi + 1), Wrap(yi), seed), c = Rand(Wrap(xi), Wrap(yi + 1), seed), d = Rand(Wrap(xi + 1), Wrap(yi + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        /// <summary>Tiling fractal noise: u and v in 0..1 over the texture, scale = lattice cells across it.</summary>
        public static float Fbm(float u, float v, int scaleX, int scaleY, int seed, int octaves = 4)
        {
            float sum = 0, amp = .5f, norm = 0; int sx = scaleX, sy = scaleY;
            for (int o = 0; o < octaves; o++)
            {
                float n = ValueTile(u * sx, v * sy, seed + o * 31, sx, sy); sum += n * amp; norm += amp; amp *= .5f; sx *= 2; sy *= 2;
            }
            return sum / norm;
        }
        static float ValueTile(float x, float y, int seed, int px, int py)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = Smooth(x - xi), fy = Smooth(y - yi);
            float a = Rand(((xi % px) + px) % px, ((yi % py) + py) % py, seed), b = Rand((((xi + 1) % px) + px) % px, ((yi % py) + py) % py, seed);
            float c = Rand(((xi % px) + px) % px, (((yi + 1) % py) + py) % py, seed), d = Rand((((xi + 1) % px) + px) % px, (((yi + 1) % py) + py) % py, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // ------------------------------------------------------------------ plumbing
        static Texture2D Make(int w, int h, Color[] pixels, List<Texture2D> track, string name)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            t.SetPixels(pixels); t.Apply(true); track?.Add(t); return t;
        }
        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        static Color Mix(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(Mathf.Lerp(a.r, b.r, t), Mathf.Lerp(a.g, b.g, t), Mathf.Lerp(a.b, b.b, t), 1f); }
        static Color Hex(string h) => TerminalUI.Hex(h);

        // ------------------------------------------------------------------ decay (what makes horror look weathered and moldy)
        static readonly Color Mold = new Color(.10f, .17f, .09f), MoldLight = new Color(.30f, .36f, .18f), Stain = new Color(.25f, .17f, .08f), Rust = new Color(.36f, .15f, .07f), Blood = new Color(.30f, .02f, .03f);

        /// <summary>Weather the pixel buffer in place. strength 0..1.</summary>
        static void Decay(Color[] px, int w, int h, int seed, Color tone, float strength, bool wallLike, float moldAmount = 1f)
        {
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h; // 0 = bottom
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w; var c = px[y * w + x];
                    c = Mix(c, Mul(tone, .55f + .7f * (c.r + c.g + c.b) / 3f), .62f * strength);              // sicken the colour toward the room's horror tone
                    float dirt = Fbm(u, v, 4, 6, seed, 4);
                    c = Mul(c, 1f - .38f * strength * (dirt - .3f) - (wallLike ? .45f * Mathf.Pow(1f - v, 3f) + .30f * Mathf.Pow(v, 6f) : .12f) * strength);   // grime gathers at edges
                    float streak = Fbm(u, v, 14, 2, seed + 5, 3);                                                   // water streaks run vertically
                    if (wallLike && streak > .56f) c = Mix(c, Stain, (streak - .56f) * 2.6f * strength * Mathf.Clamp01(v * 1.3f));
                    float mold = Fbm(u, v, 3, 4, seed + 11, 5) + (wallLike ? .22f * Mathf.Pow(1f - v, 2f) : 0f);   // mold blooms low and in corners
                    if (mold > .66f) c = Mix(c, Mix(Mold, MoldLight, Rand(x, y, seed + 2) * .5f), Mathf.Clamp01((mold - .66f) * 5.5f) * strength * moldAmount);
                    float peel = Fbm(u, v, 5, 6, seed + 23, 3);
                    if (wallLike && peel > .77f) c = peel > .79f ? Mix(c, new Color(.55f, .48f, .40f), .7f * strength) : Mul(c, .5f);   // curled paint + dark rim
                    float rust = Fbm(u, v, 6, 6, seed + 41, 3);
                    if (rust > .74f) c = Mix(c, Rust, (rust - .74f) * 5f * strength);
                    px[y * w + x] = c;
                }
            }
            var rng = new System.Random(seed * 97 + 3);
            for (int k = 0; k < 4; k++) Crack(px, w, h, rng, strength);
            if (wallLike) for (int k = 0; k < 3; k++) Drip(px, w, h, rng, k == 0 && strength > .5f);
        }
        static void Crack(Color[] px, int w, int h, System.Random rng, float strength)
        {
            float x = rng.Next(w), y = rng.Next(h), angle = (float)rng.NextDouble() * 6.28f; int length = 12 + rng.Next(30);
            for (int i = 0; i < length; i++)
            {
                angle += ((float)rng.NextDouble() - .5f) * .9f; x += Mathf.Cos(angle); y += Mathf.Sin(angle);
                int ix = ((int)x % w + w) % w, iy = ((int)y % h + h) % h; px[iy * w + ix] = Mul(px[iy * w + ix], 1f - .72f * strength);
            }
        }
        static void Drip(Color[] px, int w, int h, System.Random rng, bool blood)
        {
            int x = rng.Next(w), y0 = h - 1 - rng.Next(h / 3), len = 10 + rng.Next(40);
            for (int i = 0; i < len; i++)
            {
                int y = y0 - i; if (y < 0) break; var existing = px[y * w + x];
                px[y * w + x] = Mix(existing, blood ? Blood : Stain, .8f - i / (float)len * .3f);
                if (i % 7 == 0 && x + 1 < w) px[y * w + x + 1] = Mix(px[y * w + x + 1], blood ? Blood : Stain, .5f);
            }
        }

        // ------------------------------------------------------------------ walls
        /// <summary>96 x 128 covers 2.0 m wide by 2.8 m tall (one full wall height).</summary>
        public static Texture2D Wall(WallKind kind, Color paint, Color wainscot, Color trim, bool horror, Color horrorTone, int seed, List<Texture2D> track)
        {
            var px = new Color[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float v = (float)y / H, u = (float)x / W; Color c = paint;
                float speck = Rand(x, y, seed) * .06f - .03f;
                bool lower = v < .34f;
                switch (kind)
                {
                    case WallKind.Plain: c = lower ? wainscot : paint; if (y == (int)(.34f * H)) c = trim; break;
                    case WallKind.Panels:
                        c = paint; if (lower) { c = wainscot; bool frame = (x % 48 < 3 || x % 48 > 44) || y < 3 || y == (int)(.34f * H) - 2; if (frame) c = Mul(wainscot, .72f); } if (y == (int)(.34f * H)) c = trim; break;
                    case WallKind.Tiles: { bool grout = x % 12 == 0 || y % 12 == 0; c = v < .75f ? (grout ? Mul(paint, .62f) : Mul(paint, 1f + ((x / 12 + y / 12) % 2) * .06f)) : wainscot; if (y == (int)(.75f * H)) c = trim; break; }
                    case WallKind.Blocks: { int row = y / 10; int off = row % 2 * 8; bool mortar = y % 10 == 0 || (x + off) % 16 == 0; c = mortar ? Mul(paint, .7f) : Mul(paint, .96f + Rand(x / 16, row, seed) * .1f); break; }
                    case WallKind.Stripes: c = (x % 12 < 6) ? paint : Mul(paint, .9f); if (lower) c = wainscot; if (y == (int)(.34f * H)) c = trim; break;
                    case WallKind.Wood: { bool seam = x % 16 == 0; c = Mul(paint, seam ? .65f : 1f - .12f * Fbm(u * 6, v, 12, 2, seed, 2) + Rand(x / 16, 0, seed) * .08f); break; }
                }
                if (y < 4) c = Mul(Mix(c, trim, .8f), 1f);                                   // baseboard
                px[y * W + x] = Mul(c, 1f + speck);
            }
            if (horror) Decay(px, W, H, seed, horrorTone, 1f, true);
            return Make(W, H, px, track, (horror ? "Wall horror " : "Wall safe ") + kind);
        }

        // ------------------------------------------------------------------ floors (96 x 96 = 2 m x 2 m)
        public static Texture2D Floor(FloorKind kind, Color a, Color b, bool horror, Color horrorTone, int seed, List<Texture2D> track)
        {
            int n = 96; var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = (float)x / n, v = (float)y / n; Color c = a; float speck = Rand(x, y, seed) * .08f - .04f;
                switch (kind)
                {
                    case FloorKind.Carpet: c = Mix(a, b, Fbm(u, v, 24, 24, seed, 2)); if ((x / 24 + y / 24) % 2 == 0) c = Mul(c, .94f); if (x % 24 == 0 || y % 24 == 0) c = Mul(c, .82f); speck *= 2.2f; break;
                    case FloorKind.Tile: { bool grout = x % 24 == 0 || y % 24 == 0; c = grout ? Mul(a, .55f) : Mix(a, b, Rand(x / 24, y / 24, seed) * .5f); if (!grout && (x % 24 < 2 || y % 24 < 2)) c = Mul(c, 1.12f); break; }
                    case FloorKind.Checker: c = ((x / 12 + y / 12) % 2 == 0) ? a : b; break;
                    case FloorKind.Wood: { int plank = y / 12; c = Mix(a, b, Rand(plank, 0, seed)); float grain = Fbm(u * 3 + plank * .13f, v, 3, 24, seed, 3); c = Mul(c, .86f + .28f * grain); if (y % 12 == 0 || ((x + plank * 31) % 64 == 0)) c = Mul(c, .55f); break; }
                    case FloorKind.Concrete: c = Mix(a, b, Fbm(u, v, 6, 6, seed, 5)); if (x % 48 == 0 || y % 48 == 0) c = Mul(c, .6f); speck *= 1.5f; break;
                    case FloorKind.Marble: { float vein = Mathf.Abs(Mathf.Sin((u * 3f + Fbm(u, v, 4, 4, seed, 4) * 4f) * 6.28f)); c = Mix(a, b, 1f - Mathf.Pow(vein, .35f) * .8f); if (x % 48 == 0 || y % 48 == 0) c = Mul(c, .72f); break; }
                    case FloorKind.Grate: { bool bar = x % 8 < 2 || y % 8 < 2; c = bar ? Mix(a, b, .5f) : Mul(a, .35f); if (x % 48 == 0 || y % 48 == 0) c = Mul(c, 1.25f); break; }
                    case FloorKind.Vinyl: c = Mix(a, b, Fbm(u, v, 8, 8, seed, 3) * .6f); if ((x + y) % 32 < 2) c = Mul(c, 1.1f); break;
                }
                px[y * n + x] = Mul(c, 1f + speck);
            }
            if (horror) Decay(px, n, n, seed + 7, horrorTone, 1f, false);
            return Make(n, n, px, track, (horror ? "Floor horror " : "Floor safe ") + kind);
        }

        // ------------------------------------------------------------------ ceilings, doors, cloth, metal
        public static Texture2D Ceiling(bool horror, int seed, List<Texture2D> track)
        {
            int n = 96; var px = new Color[n * n]; var tile = Hex("D9DBD6");
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                bool seam = x % 48 < 2 || y % 48 < 2; float hole = Rand(x / 2, y / 2, seed) * .1f;
                px[y * n + x] = seam ? Mul(tile, .55f) : Mul(tile, .95f + hole * .5f - (Rand(x, y, seed + 1) < .02f ? .1f : 0));
            }
            if (horror) Decay(px, n, n, seed, new Color(.32f, .18f, .13f), .95f, false, 1.4f);
            return Make(n, n, px, track, horror ? "Ceiling horror" : "Ceiling safe");
        }

        /// <summary>A door slab, 1 m x 2.1 m mapped to 64 x 128 pixels.</summary>
        public static Texture2D Door(Color paint, Color plate, bool horror, int number, int seed, List<Texture2D> track)
        {
            int w = 64, h = 128; var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                Color c = Mul(paint, .96f + Rand(x, y, seed) * .06f);
                bool panel = (x > 8 && x < 56) && ((y > 10 && y < 60) || (y > 68 && y < 120)); bool edge = panel && (x == 9 || x == 55 || y == 11 || y == 59 || y == 69 || y == 119);
                if (panel) c = Mul(c, edge ? .7f : 1.06f);
                if (x < 3 || x > 60) c = Mul(c, .8f);
                if (x > 48 && x < 54 && y > 56 && y < 66) c = Hex("C9CED6");                 // handle
                if (x > 22 && x < 42 && y > 90 && y < 108) c = Mix(c, plate, .95f);            // number plate
                px[y * w + x] = c;
            }
            if (horror) Decay(px, w, h, seed + number, new Color(.34f, .12f, .10f), .95f, true, .6f);
            return Make(w, h, px, track, "Door " + number + (horror ? " horror" : " safe"));
        }

        public static Texture2D Fabric(Color baseColor, bool horror, int seed, List<Texture2D> track)
        {
            int n = 48; var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float weave = ((x + y) % 4 < 2 ? 1f : .88f) * (.94f + Rand(x, y, seed) * .1f); px[y * n + x] = Mul(baseColor, weave); }
            if (horror) Decay(px, n, n, seed, new Color(.30f, .15f, .12f), .9f, false, 1.2f);
            return Make(n, n, px, track, horror ? "Fabric horror" : "Fabric safe");
        }

        public static Texture2D Metal(Color baseColor, bool horror, int seed, List<Texture2D> track)
        {
            int n = 48; var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float brushed = .92f + Fbm((float)x / n, (float)y / n, 2, 24, seed, 3) * .14f; px[y * n + x] = Mul(baseColor, brushed); }
            if (horror) Decay(px, n, n, seed, new Color(.30f, .14f, .09f), 1f, false, .3f);
            return Make(n, n, px, track, horror ? "Metal horror" : "Metal safe");
        }

        public static Texture2D Wood(Color a, Color b, bool horror, int seed, List<Texture2D> track)
        {
            int n = 64; var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float grain = Fbm((float)x / n, (float)y / n, 2, 16, seed, 4); px[y * n + x] = Mul(Mix(a, b, grain), .94f + Rand(x, y, seed) * .08f); }
            if (horror) Decay(px, n, n, seed, new Color(.25f, .14f, .1f), .9f, false, .8f);
            return Make(n, n, px, track, horror ? "Wood horror" : "Wood safe");
        }

        // ------------------------------------------------------------------ small things
        public static Texture2D Solid(Color c, List<Texture2D> track)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            t.SetPixels(new[] { c, c, c, c }); t.Apply(); track?.Add(t); return t;
        }
    }
}
