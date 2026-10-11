using System.Collections.Generic;
using UnityEngine;

namespace Breathe
{
    /// <summary>
    /// The approved pen-knife, assembled from the six exported parts. The click: Blade scales along the pen axis from the nose
    /// (origin) and the Plunger translates a few millimetres. Parts order: Barrel, Collar, Grip, Clip, Plunger, Blade.
    /// </summary>
    public sealed class KnifeProp
    {
        public readonly GameObject Root;
        readonly Transform bladePivot, plungerPivot;
        readonly Vector3 axis;          // pen axis in the knife's local space (points from the nose toward the cap)
        readonly List<Material> materials = new List<Material>();
        public bool Valid { get; }

        KnifeProp(GameAssets assets, Transform parent)
        {
            Root = new GameObject("Pen-knife"); Root.transform.SetParent(parent, false);
            var parts = assets ? assets.knifeParts : null;
            if (parts == null || parts.Length < 6) { Valid = false; axis = Vector3.up; return; }
            Valid = true;
            var template = Resources.Load<Material>("OfficeLit") ?? Resources.Load<Material>("OfficeSurface");
            Material Make(string hex, float metal, float smooth)
            {
                var m = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                var color = TerminalUI.Hex(hex); m.SetColor("_BaseColor", color); if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                materials.Add(m); return m;
            }
            var navy = Make("1c3f8f", 0, .35f); var chrome = Make("c4c9d2", .9f, .8f); var rubber = Make("191a20", 0, .1f);
            var red = Make("c9262b", 0, .6f); var steel = Make("dfe5ee", .85f, .75f);
            var pivots = new Transform[6];
            for (int i = 0; i < 6; i++)
            {
                var pivot = new GameObject("Pivot " + parts[i].name).transform; pivot.SetParent(Root.transform, false); pivots[i] = pivot;
                var instance = Object.Instantiate(parts[i], pivot, false);
                foreach (var r in instance.GetComponentsInChildren<Renderer>())
                {
                    var slots = r.sharedMaterials; var assigned = new Material[slots.Length];
                    for (int s = 0; s < slots.Length; s++)
                        assigned[s] = i == 0 ? navy : i == 1 || i == 3 ? chrome : i == 2 ? rubber : i == 4 ? (s == 0 ? chrome : red) : steel;
                    r.sharedMaterials = assigned;
                }
            }
            plungerPivot = pivots[4]; bladePivot = pivots[5];
            // Find the pen axis from where the barrel sits relative to the nose-centre origin, snapped to a local cardinal axis.
            var barrelBounds = Bounds(pivots[0]); var direction = Root.transform.InverseTransformPoint(barrelBounds.center) - Vector3.zero;
            axis = Dominant(direction);
            SetDeploy(1f);
        }

        public static KnifeProp Create(GameAssets assets, Transform parent) => new KnifeProp(assets, parent);

        static Bounds Bounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(); var b = new Bounds(t.position, Vector3.zero); bool first = true;
            foreach (var r in renderers) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            return b;
        }
        static Vector3 Dominant(Vector3 v)
        {
            var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x >= a.y && a.x >= a.z ? new Vector3(Mathf.Sign(v.x), 0, 0) : a.y >= a.z ? new Vector3(0, Mathf.Sign(v.y), 0) : new Vector3(0, 0, Mathf.Sign(v.z));
        }

        /// <summary>The unit vector pointing from the cap toward the blade tip (the way the knife "points") in the knife's local space.</summary>
        public Vector3 TipDirection => -axis;

        /// <summary>0 = pen (blade hidden in the bore), 1 = blade fully out. Blade renderer is disabled when retracted.</summary>
        public void SetDeploy(float t)
        {
            if (!Valid) return;
            t = Mathf.Clamp01(t);
            var mask = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
            bladePivot.localScale = Vector3.one + mask * (Mathf.Max(.001f, t) - 1f);
            bladePivot.gameObject.SetActive(t > .02f);
            plungerPivot.localPosition = -axis * (.004f * t);   // the click button sits 4 mm lower when the blade is out
        }

        /// <summary>Hold the knife at a hand, with the tip pointing along <paramref name="tipWorldDirection"/>.</summary>
        public void Hold(Transform hand, Vector3 localPosition, Vector3 tipDirectionInParent, float scale)
        {
            Root.transform.SetParent(hand, false); Root.transform.localPosition = localPosition; Root.transform.localScale = Vector3.one * scale;
            Root.transform.localRotation = Quaternion.FromToRotation(TipDirection, tipDirectionInParent.normalized);
        }

        public void Dispose() { foreach (var m in materials) if (m) Object.Destroy(m); if (Root) Object.Destroy(Root); }
    }
}
