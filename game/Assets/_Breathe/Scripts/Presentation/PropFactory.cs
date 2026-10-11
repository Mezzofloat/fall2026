using System.Collections.Generic;
using UnityEngine;

namespace Breathe
{
    /// <summary>Materials for a part's named slots: Blender names them &lt;Part&gt;_main, _dark, _metal, _glow, _accent and _glass.</summary>
    public sealed class PropSkin
    {
        public Material Main, Dark, Metal, Glow, Accent, Glass;
        public Material For(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return Main;
            int i = materialName.LastIndexOf('_'); string suffix = i >= 0 ? materialName.Substring(i + 1) : materialName; int space = suffix.IndexOf(' '); if (space > 0) suffix = suffix.Substring(0, space);
            switch (suffix) { case "dark": return Dark ?? Main; case "metal": return Metal ?? Main; case "glow": return Glow ?? Main; case "accent": return Accent ?? Main; case "glass": return Glass ?? Main; default: return Main; }
        }
    }

    /// <summary>Places approved Blender parts (kit FBXs) and builds simple primitives. Everything is created un-collidered and parented for cheap cleanup.</summary>
    public sealed class PropFactory
    {
        readonly Dictionary<string, GameObject> parts = new Dictionary<string, GameObject>();
        public PropFactory(GameAssets assets)
        {
            if (assets == null) return;
            foreach (var list in new[] { assets.kitParts, assets.propParts }) if (list != null) foreach (var part in list) if (part) parts[part.name] = part;
        }
        public bool Has(string name) => parts.ContainsKey(name);
        public int Count => parts.Count;

        /// <summary>Instantiate a kit part at a pivot. The FBX root keeps its layout offset, so it is zeroed; materials are assigned per renderer slot.</summary>
        public Transform Place(Transform parent, string part, Vector3 position, float yaw, Vector3 scale, params Material[] mats)
        {
            var pivot = new GameObject("Prop: " + part).transform; pivot.SetParent(parent, false); pivot.localPosition = position; pivot.localRotation = Quaternion.Euler(0, yaw, 0); pivot.localScale = scale;
            if (!parts.TryGetValue(part, out var prefab)) return pivot;
            var instance = Object.Instantiate(prefab, pivot, false); instance.transform.localPosition = Vector3.zero;
            if (mats != null && mats.Length > 0)
                foreach (var r in instance.GetComponentsInChildren<Renderer>())
                {
                    var slots = r.sharedMaterials; var assigned = new Material[slots.Length];
                    for (int i = 0; i < slots.Length; i++) assigned[i] = mats[Mathf.Min(i, mats.Length - 1)];
                    r.sharedMaterials = assigned;
                }
            return pivot;
        }

        /// <summary>Instantiate a part and re-skin every renderer slot from its material name suffix. Returns the pivot.</summary>
        public Transform PlaceSkinned(Transform parent, string part, Vector3 position, float yaw, Vector3 scale, PropSkin skin)
        {
            var pivot = new GameObject("Prop: " + part).transform; pivot.SetParent(parent, false); pivot.localPosition = position; pivot.localRotation = Quaternion.Euler(0, yaw, 0); pivot.localScale = scale;
            if (!parts.TryGetValue(part, out var prefab)) return pivot;
            var instance = Object.Instantiate(prefab, pivot, false); instance.transform.localPosition = Vector3.zero;
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                var slots = r.sharedMaterials; var assigned = new Material[slots.Length];
                for (int i = 0; i < slots.Length; i++) assigned[i] = skin.For(slots[i] ? slots[i].name : null);
                r.sharedMaterials = assigned;
                if (skin.Glass != null) foreach (var m in assigned) if (m == skin.Glass) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return pivot;
        }

        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localScale = size;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return go;
        }
        public static GameObject Cyl(Transform parent, string name, Vector3 center, float radius, float height, Material m, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localScale = new Vector3(radius * 2, height / 2f, radius * 2);
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value); go.GetComponent<Renderer>().sharedMaterial = m; return go;
        }
        public static GameObject Ball(Transform parent, string name, Vector3 center, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = m; return go;
        }
        public static GameObject Quad(Transform parent, string name, Vector3 center, Vector2 size, Quaternion rotation, Material m, bool shadows = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name; Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localRotation = rotation; go.transform.localScale = new Vector3(size.x, size.y, 1);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return go;
        }
    }
}
