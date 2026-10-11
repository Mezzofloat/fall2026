using UnityEditor;
using UnityEngine;

public static class BreatheDiag
{
    /// <summary>Looks for triangles of the monitor bezel that cover its aperture.</summary>
    public static void BezelCheck()
    {
        var go=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Breathe/Art/Props/OfficeKit/MonitorBezel.fbx"));
        go.transform.position=Vector3.zero; var filter=go.GetComponentInChildren<MeshFilter>(); var mesh=filter.sharedMesh; var tr=filter.transform;
        var v=mesh.vertices; var t=mesh.triangles; int inside=0;
        for(int i=0;i<t.Length;i+=3)
        {
            Vector3 a=tr.TransformPoint(v[t[i]]),b=tr.TransformPoint(v[t[i+1]]),c=tr.TransformPoint(v[t[i+2]]);
            var centroid=(a+b+c)/3f; float area=Vector3.Cross(b-a,c-a).magnitude/2f;
            bool onFront=Mathf.Abs(a.z)<.001f&&Mathf.Abs(b.z)<.001f&&Mathf.Abs(c.z)<.001f; bool inAperture=Mathf.Abs(centroid.x)<.8f&&Mathf.Abs(centroid.y)<.55f;
            if(onFront&&inAperture) {inside++; Debug.Log("BREATHE_BEZEL front-plane triangle in aperture area="+area.ToString("F3")+" a="+a.ToString("F3")+" b="+b.ToString("F3")+" c="+c.ToString("F3"));}
        }
        Debug.Log("BREATHE_BEZEL tris="+(t.Length/3)+" verts="+v.Length+" frontTrisInAperture="+inside+" bounds="+mesh.bounds.size.ToString("F3")+" subMeshes="+mesh.subMeshCount);
        Object.DestroyImmediate(go);
    }

    /// <summary>Logs size/position of the office-kit parts as Unity sees them (world space, after import rules).</summary>
    public static void PrintKit()
    {
        AssetDatabase.ImportAsset("Assets/_Breathe/Art/Props/OfficeKit",ImportAssetOptions.ImportRecursive|ImportAssetOptions.ForceUpdate);
        foreach(var guid in AssetDatabase.FindAssets("t:Model",new[]{"Assets/_Breathe/Art/Props/OfficeKit"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var go=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path)); var renderers=go.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            Debug.Log("BREATHE_KIT "+System.IO.Path.GetFileNameWithoutExtension(path)+" rootPos="+go.transform.position.ToString("F3")+" center="+bounds.center.ToString("F3")+" size="+bounds.size.ToString("F3")+" subMeshes="+renderers[0].sharedMaterials.Length);
            Object.DestroyImmediate(go);
        }
    }

    /// <summary>Logs where each exported pen-knife part sits in Unity space (maps Blender axes to Unity axes).</summary>
    public static void PrintAxes()
    {
        foreach(string part in new[]{"Barrel","Clip","Blade"})
        {
            AssetDatabase.ImportAsset("Assets/_Breathe/Art/Props/PenKnife/"+part+".fbx",ImportAssetOptions.ForceUpdate);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Breathe/Art/Props/PenKnife/"+part+".fbx");
            var go=(GameObject)Object.Instantiate(prefab); var renderers=go.GetComponentsInChildren<Renderer>(); var bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            var imp=(ModelImporter)AssetImporter.GetAtPath("Assets/_Breathe/Art/Props/PenKnife/"+part+".fbx");
            Debug.Log("BREATHE_AXES "+part+" center="+bounds.center.ToString("F6")+" size="+bounds.size.ToString("F6")+" rootRot="+go.transform.eulerAngles+" rootScale="+go.transform.localScale.ToString("F6")+" importer: globalScale="+imp.globalScale+" useFileScale="+imp.useFileScale+" fileScale="+imp.fileScale+" bakeAxis="+imp.bakeAxisConversion);
            AssetDatabase.ImportAsset("Assets/_Breathe/Art/Props/PenKnife/"+part+".fbx",ImportAssetOptions.ForceUpdate);
            Object.DestroyImmediate(go);
        }
    }
}
