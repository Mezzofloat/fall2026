using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Adds the Buckshot-style full-screen pass (low virtual resolution, dither, palette, scanlines) to the URP renderers, and imports audio sensibly.</summary>
public static class BreatheRenderer
{
    const string MaterialPath = "Assets/_Breathe/Resources/BreatheCrt.mat";
    static readonly string[] Renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };

    public static void Configure()
    {
        var shader = Shader.Find("Breathe/CrtDither");
        if (shader == null) { Debug.LogError("BREATHE_ASSETS_MISSING: shader Breathe/CrtDither not found."); return; }
        if (!AssetDatabase.IsValidFolder("Assets/_Breathe/Resources")) AssetDatabase.CreateFolder("Assets/_Breathe", "Resources");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
        else if (material.shader != shader) material.shader = shader;
        EditorUtility.SetDirty(material);
        foreach (var path in Renderers)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;
            var existing = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f != null && f.name == "Breathe CRT");
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>(); existing.name = "Breathe CRT";
                AssetDatabase.AddObjectToAsset(existing, data);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures"); list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = existing;
                var map = so.FindProperty("m_RendererFeatureMap"); AssetDatabase.TryGetGUIDAndLocalFileIdentifier(existing, out _, out long local);
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = local;
                so.ApplyModifiedProperties();
            }
            existing.passMaterial = material; existing.passIndex = 0;
            existing.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            existing.fetchColorBuffer = true; existing.requirements = ScriptableRenderPassInput.Color; existing.bindDepthStencilAttachment = false;
            existing.SetActive(true);
            EditorUtility.SetDirty(existing); EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
    }
}

/// <summary>Music is compressed in memory (gapless loops); short effects are decompressed on load and mono so 3D positioning works.</summary>
public sealed class BreatheAudioImporter : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/_Breathe/Audio/") || !assetPath.EndsWith("-norm.wav")) return;
        var importer = (AudioImporter)assetImporter; bool music = assetPath.Contains("/Music/");
        var settings = importer.defaultSampleSettings;
        settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = music ? .75f : .85f;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate; settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings; importer.forceToMono = !music && !assetPath.Contains("jumpscare"); importer.loadInBackground = false;
    }
}
