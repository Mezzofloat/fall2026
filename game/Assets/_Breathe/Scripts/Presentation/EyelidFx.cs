using UnityEngine;

namespace Breathe
{
    /// <summary>
    /// Two curved eyelids parented to the camera (real 3D quads, never a flat screen layer). Used between rounds: the player's eyes close in
    /// horror and open on the calm office, as though it was all in their head. Open: 0 = shut, 1 = wide open.
    /// </summary>
    public sealed class EyelidFx
    {
        readonly GameObject root; readonly Transform upper, lower; readonly Material material; readonly Texture2D texture;
        const float Distance = .12f, HalfH = .0875f, HalfW = .1555f;
        float open = 1f;
        public float Open { get => open; set { open = Mathf.Clamp01(value); Apply(); } }
        public bool Visible => open < .995f;

        public EyelidFx(Camera camera)
        {
            root = new GameObject("Eyelids"); root.transform.SetParent(camera.transform, false); root.transform.localPosition = new Vector3(0, 0, Distance);
            texture = MakeLid();
            var template = Resources.Load<Material>("OfficeGlass") ?? Resources.Load<Material>("OfficeSurface");
            material = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", texture); material.mainTexture = texture; material.renderQueue = 3500;
            upper = Lid("Upper lid", false); lower = Lid("Lower lid", true); Apply();
        }

        Transform Lid(string name, bool bottom)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name; Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false); go.GetComponent<Renderer>().sharedMaterial = material; go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = new Vector3(HalfW * 2.6f, HalfH * 2.2f, 1) ; if (bottom) go.transform.localRotation = Quaternion.Euler(0, 0, 180);
            return go.transform;
        }

        void Apply()
        {
            root.SetActive(Visible);
            // closed: the lids overlap a little; open: they rest fully outside the view
            float gap = Mathf.SmoothStep(0, 1, open) * (HalfH * 1.15f) - .006f;
            float half = HalfH * 1.1f;      // lid quad half height
            upper.localPosition = new Vector3(0, gap + half, 0); lower.localPosition = new Vector3(0, -gap - half, 0);
        }

        /// <summary>Soft-edged lid with an arched lower edge and lashes (drawn for the upper lid; the lower lid is the same texture turned over).</summary>
        static Texture2D MakeLid()
        {
            int w = 256, h = 96; var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float u = (x / (float)(w - 1)) * 2f - 1f;                   // -1..1
                float edge = 14f + 26f * u * u;                            // the free edge dips in the middle (the lid is the part above it)
                var skin = Color.Lerp(new Color(.03f, 0f, .01f), new Color(.22f, .06f, .05f), Mathf.Clamp01((y - edge) / 50f));
                float a = y >= edge ? 1f : Mathf.Clamp01(1f + (y - edge) / 9f);
                if (y < edge && y > edge - 9f && (x % 6 < 2)) { a = .95f; skin = Color.black; }   // lashes
                px[y * w + x] = new Color(skin.r, skin.g, skin.b, a);
            }
            t.SetPixels(px); t.Apply(); return t;
        }

        public void Dispose() { if (material) Object.Destroy(material); if (texture) Object.Destroy(texture); if (root) Object.Destroy(root); }
    }
}
