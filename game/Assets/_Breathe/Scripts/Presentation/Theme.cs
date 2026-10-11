using System.Collections.Generic;
using UnityEngine;

namespace Breathe
{
    /// <summary>
    /// Materials that exist in two states: SAFE (sleek, clean office) and HORROR (weathered, moldy, red-lit). Swapping the state swaps
    /// textures, tints and smoothness on every themed material at once, so a hidden moment (blink, blackout) can change the whole building.
    /// </summary>
    public sealed class Theme
    {
        sealed class Entry { public Material M; public Texture2D Safe, Horror; public Color SafeTint, HorrorTint; public float SafeSmooth, HorrorSmooth; public Color SafeEmit, HorrorEmit; }
        readonly List<Entry> entries = new List<Entry>();
        public readonly List<Material> Materials = new List<Material>();
        public readonly List<Texture2D> Textures = new List<Texture2D>();
        public bool Horror { get; private set; }
        static Shader fallback;

        public Material Lit(string name, string safeHex, string horrorHex, float safeSmooth = .25f, float horrorSmooth = .08f, Texture2D safe = null, Texture2D horror = null, Vector2? tiling = null, float metal = 0f)
        {
            var template = Resources.Load<Material>("OfficeLit") ?? Resources.Load<Material>("OfficeSurface");
            var m = template ? new Material(template) : new Material(fallback ??= Shader.Find("Universal Render Pipeline/Unlit")); m.name = name;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (tiling.HasValue) m.mainTextureScale = tiling.Value;
            var e = new Entry { M = m, Safe = safe, Horror = horror ?? safe, SafeTint = TerminalUI.Hex(safeHex), HorrorTint = TerminalUI.Hex(horrorHex), SafeSmooth = safeSmooth, HorrorSmooth = horrorSmooth };
            entries.Add(e); Materials.Add(m); Apply(e); return m;
        }

        /// <summary>Unlit glowing material (screens, lamp tubes, LEDs): bright in both states but recoloured.</summary>
        public Material Glow(string name, string safeHex, string horrorHex)
        {
            var template = Resources.Load<Material>("OfficeSurface");
            var m = template ? new Material(template) : new Material(fallback ??= Shader.Find("Universal Render Pipeline/Unlit")); m.name = name;
            var e = new Entry { M = m, SafeTint = TerminalUI.Hex(safeHex), HorrorTint = TerminalUI.Hex(horrorHex) };
            entries.Add(e); Materials.Add(m); Apply(e); return m;
        }

        /// <summary>Transparent tinted glass (window panes, bottles).</summary>
        public Material Glass(string name, string safeHex, string horrorHex, float safeAlpha = .22f, float horrorAlpha = .35f)
        {
            var template = Resources.Load<Material>("OfficeGlass") ?? Resources.Load<Material>("OfficeSurface");
            var m = template ? new Material(template) : new Material(fallback ??= Shader.Find("Universal Render Pipeline/Unlit")); m.name = name;
            var a = TerminalUI.Hex(safeHex); a.a = safeAlpha; var b = TerminalUI.Hex(horrorHex); b.a = horrorAlpha;
            var e = new Entry { M = m, SafeTint = a, HorrorTint = b }; entries.Add(e); Materials.Add(m); Apply(e); return m;
        }

        void Apply(Entry e)
        {
            var tex = Horror ? e.Horror : e.Safe; var tint = Horror ? e.HorrorTint : e.SafeTint;
            e.M.SetColor("_BaseColor", tint);
            if (tex) { e.M.SetTexture("_BaseMap", tex); e.M.mainTexture = tex; }
            if (e.M.HasProperty("_Smoothness")) e.M.SetFloat("_Smoothness", Horror ? e.HorrorSmooth : e.SafeSmooth);
        }

        public void SetHorror(bool horror) { Horror = horror; foreach (var e in entries) Apply(e); }

        /// <summary>One-off texture helper for wrapper classes that hold their own reference.</summary>
        public Texture2D Track(Texture2D t) { Textures.Add(t); return t; }

        public void Dispose()
        {
            foreach (var m in Materials) if (m) Object.Destroy(m);
            foreach (var t in Textures) if (t) Object.Destroy(t);
            Materials.Clear(); Textures.Clear(); entries.Clear();
        }
    }
}
