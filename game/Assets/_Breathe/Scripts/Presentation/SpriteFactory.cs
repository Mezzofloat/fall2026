using System.Collections.Generic;
using UnityEngine;

namespace Breathe
{
    /// <summary>Procedural point-filtered character sprites (flat people for the 2.5D office). Textures are tracked for cleanup.</summary>
    public static class SpriteFactory
    {
        public const int W = 96, H = 192;
        /// <summary>Where the raised hand sits on Vale's sprite, as a fraction of the quad (0..1).</summary>
        public static readonly Vector2 ValeHand = new Vector2(84f / W, 136f / H);

        sealed class Canvas2D
        {
            public readonly Color[] Pixels = new Color[W * H];
            public void Rect(int x, int y, int w, int h, Color c) { for (int a = x; a < x + w; a++) for (int b = y; b < y + h; b++) Set(a, b, c); }
            public void Circle(int x, int y, int r, Color c) { for (int a = x - r; a <= x + r; a++) for (int b = y - r; b <= y + r; b++) if ((a - x) * (a - x) + (b - y) * (b - y) <= r * r) Set(a, b, c); }
            public void Ellipse(int x, int y, int rx, int ry, Color c) { for (int a = x - rx; a <= x + rx; a++) for (int b = y - ry; b <= y + ry; b++) { float u = (a - x) / (float)rx, v = (b - y) / (float)ry; if (u * u + v * v <= 1) Set(a, b, c); } }
            void Set(int a, int b, Color c) { if (a >= 0 && a < W && b >= 0 && b < H) Pixels[b * W + a] = c; }
        }

        static Color Hex(string v) => TerminalUI.Hex(v);

        public static Texture2D Person(bool scary, int index, List<Texture2D> track)
        {
            var c = new Canvas2D();
            Color ink = Hex("11182b"), skin = scary ? Hex("D8D69A") : Hex("E4B38D"), skinShade = scary ? Hex("B3B07A") : Hex("C4906E");
            bool raised = scary || index == 3;
            Color suit = scary ? Hex("261018") : index == 0 ? Hex("B33A4E") : index == 1 ? Hex("34796A") : index == 3 ? Hex("2d3347") : Hex("65559A");
            Color suitDark = scary ? Hex("160a10") : suit * .78f;
            Color shirt = Hex("ECE7D6"), tie = scary ? Hex("E0242C") : index == 3 ? Hex("C42B33") : Hex("2E60B5"), pants = scary ? Hex("0f0b12") : Hex("1b2236");
            // legs + shoes
            c.Rect(30, 8, 16, 58, pants); c.Rect(50, 8, 16, 58, pants); c.Rect(26, 0, 22, 9, ink); c.Rect(48, 0, 24, 9, ink);
            // torso: jacket with lapel shading, shirt wedge, tie
            c.Rect(22, 62, 52, 72, suit); c.Rect(22, 62, 6, 72, suitDark); c.Rect(68, 62, 6, 72, suitDark);
            c.Rect(40, 82, 16, 52, shirt); c.Rect(46, 70, 5, 58, tie); c.Rect(44, 118, 9, 9, tie);
            // arms
            c.Rect(10, 64, 13, 66, suit); c.Circle(16, 60, 7, skin);
            if (raised) { c.Rect(73, 92, 14, 40, suit); c.Rect(78, 118, 12, 14, suit); c.Circle(84, 136, 8, skin); c.Rect(80, 130, 8, 6, skinShade); }
            else { c.Rect(73, 64, 13, 66, suit); c.Circle(80, 60, 7, skin); }
            // neck + head
            c.Rect(41, 130, 14, 14, skinShade);
            int hy = 164, hr = scary ? 27 : 24;
            c.Circle(48, hy, hr, ink); c.Circle(48, hy, hr - 2, skin);
            if (!scary)
            {
                c.Ellipse(48, hy + 17, 25, 9, index == 0 ? Hex("6B3E2A") : index == 1 ? Hex("2a2a33") : index == 3 ? Hex("1a1a22") : Hex("8a5a35"));
                c.Circle(38, hy + 1, 5, Color.white); c.Circle(58, hy + 1, 5, Color.white); c.Circle(39, hy, 2, ink); c.Circle(57, hy, 2, ink);
                c.Rect(42, hy - 10, 14, 3, ink); c.Rect(40, hy - 8, 3, 3, ink); c.Rect(55, hy - 8, 3, 3, ink);
            }
            else
            {
                c.Ellipse(48, hy + 20, 27, 10, Hex("0b0b10"));           // slicked hair
                c.Circle(35, hy + 3, 11, Hex("F2E7A0")); c.Circle(62, hy + 5, 14, Hex("F2E7A0")); // mismatched eyes
                c.Circle(37, hy + 1, 5, ink); c.Circle(66, hy + 8, 6, ink);
                c.Rect(24, hy + 12, 22, 3, ink); c.Rect(54, hy + 17, 26, 4, ink);                   // brows, one higher
                c.Ellipse(48, hy - 12, 20, 8, Hex("1a060b"));                                        // grin
                for (int t = 0; t < 11; t++) c.Rect(30 + t * 3 + 1, hy - 17, 2, 11, Color.white);   // teeth
                c.Rect(29, hy - 12, 38, 1, Hex("1a060b"));
            }
            return Finish(c, track);
        }

        public static Texture2D Icon(string glyph, Color color, List<Texture2D> track)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var data = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                bool on = glyph == "+" ? (x > 12 && x < 19 || y > 12 && y < 19) : x > 4 && x < 27 && y > 2 && y < 29;
                data[y * 32 + x] = on ? color : Color.clear;
                if (glyph != "+" && x > 9 && x < 23 && (y == 11 || y == 16 || y == 21)) data[y * 32 + x] = TerminalUI.Ink;
            }
            texture.SetPixels(data); texture.Apply(); track?.Add(texture); return texture;
        }

        public static Texture2D Dot(List<Texture2D> track)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var data = new Color[256];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) data[y * 16 + x] = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(8, 8)) < 6 ? new Color(1, 1, 1, 1) : Color.clear;
            texture.SetPixels(data); texture.Apply(); track?.Add(texture); return texture;
        }

        static Texture2D Finish(Canvas2D c, List<Texture2D> track)
        {
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(c.Pixels); texture.Apply(); track?.Add(texture); return texture;
        }
    }
}
