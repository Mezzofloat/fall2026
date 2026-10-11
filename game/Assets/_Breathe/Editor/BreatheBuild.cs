using System;
using System.Collections.Generic;
using System.IO;
using Breathe;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BreatheBuild
{
    const string ScenePath="Assets/_Breathe/Scenes/Main.unity";

    /// <summary>The bridge speaks plain http on 127.0.0.1. Apply and persist the needed player settings through the API whenever the editor loads.</summary>
    [InitializeOnLoadMethod]
    static void EnsureProjectSettings()
    {
        bool changed=false;
        if(PlayerSettings.insecureHttpOption!=InsecureHttpOption.AlwaysAllowed) { PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed; changed=true; }
        if(string.IsNullOrEmpty(PlayerSettings.macOS.microphoneUsageDescription)) { PlayerSettings.macOS.microphoneUsageDescription="BREATHE uses the microphone you select so you can answer interview questions out loud. Audio goes to ElevenLabs for transcription only while you hold V."; changed=true; }
        if(changed) { AssetDatabase.SaveAssets(); EditorApplication.delayCall+=()=>EditorApplication.ExecuteMenuItem("File/Save Project"); }
    }

    [MenuItem("BREATHE/Prepare first-person scene")]
    public static void Prepare()
    {
        // Scene assets are generated through Unity, never hand-authored YAML.
        AssetDatabase.ImportAsset("Assets/_Breathe/Art/Props",ImportAssetOptions.ImportRecursive|ImportAssetOptions.ForceUpdate);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var host=new GameObject("BREATHE - first-person sprite game",typeof(GameAssets),typeof(BreatheGame));
        var assets=host.GetComponent<GameAssets>();
        // Explicit authored-asset references (the .m4a originals are inert and deliberately not referenced).
        // Normalized copies produced by scripts/process_audio.py (originals stay untouched in the repo root).
        AudioClip Clip(string folder,string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Breathe/Audio/"+folder+"/"+name+"-norm.wav");
        assets.safe=Clip("Music","safe"); assets.horror=Clip("Music","horror"); assets.laugh=Clip("Music","laugh");
        assets.jumpscare=Clip("SFX","jumpscare"); assets.doorOpen=Clip("SFX","door-open"); assets.doorBreak=Clip("SFX","door-break");
        assets.knifeCut=Clip("SFX","knife-cut"); assets.knifeSwing=Clip("SFX","knife-swing"); assets.paper=Clip("SFX","paper"); assets.walking=Clip("SFX","walking");
        assets.headFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Breathe/Fonts/Silkscreen-Bold.ttf");
        assets.bodyFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Breathe/Fonts/VT323-Regular.ttf");
        var props=new List<GameObject>();
        foreach(string guid in AssetDatabase.FindAssets("t:Model",new[]{"Assets/_Breathe/Art/Props/OfficeProps"})) props.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
        assets.propParts=props.ToArray();
        BreatheRenderer.Configure();
        var parts=new List<GameObject>();
        foreach(string part in new[]{"Barrel","Collar","Grip","Clip","Plunger","Blade"})
            parts.Add(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Breathe/Art/Props/PenKnife/"+part+".fbx"));
        assets.knifeParts=parts.ToArray();
        var kit=new List<GameObject>();
        foreach(string guid in AssetDatabase.FindAssets("t:Model",new[]{"Assets/_Breathe/Art/Props/OfficeKit"})) kit.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
        assets.kitParts=kit.ToArray();
        if(kit.Count<16) Debug.LogError("BREATHE_ASSETS_MISSING: office kit has "+kit.Count+" of 16 parts.");
        if(assets.safe==null||assets.laugh==null||assets.horror==null||assets.jumpscare==null||assets.doorOpen==null||assets.walking==null||assets.paper==null||parts.Exists(x=>x==null)) Debug.LogError("BREATHE_ASSETS_MISSING: music, sound effects or pen-knife parts did not import (run scripts/process_audio.py).");
        if(assets.headFont==null||assets.bodyFont==null) Debug.LogError("BREATHE_ASSETS_MISSING: pixel fonts did not import.");
        // Keep both runtime shader variants in the player, including sprite alpha clipping.
        if(!AssetDatabase.IsValidFolder("Assets/_Breathe/Resources")) AssetDatabase.CreateFolder("Assets/_Breathe","Resources");
        foreach(string name in new[]{"OfficeSurface","OfficeSprite","OfficeGlass","OfficeLit"})
        {
            string path="Assets/_Breathe/Resources/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find(name=="OfficeLit"?"Universal Render Pipeline/Lit":"Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material,path); }
            if(name=="OfficeGlass")
            {   // transparent unlit surface (frosted glass, blackout fade)
                material.SetFloat("_Surface",1); material.SetFloat("_Blend",0); material.SetFloat("_SrcBlend",5); material.SetFloat("_DstBlend",10); material.SetFloat("_ZWrite",0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000; material.SetOverrideTag("RenderType","Transparent");
            }
            if(name=="OfficeSprite")
            {
                material.SetFloat("_AlphaClip",1); material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cull",0); material.renderQueue=2450;
            }
            EditorUtility.SetDirty(material);
        }
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        PlayerSettings.productName="BREATHE.EXE";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"com.breatheexe.firstperson");
        PlayerSettings.defaultScreenWidth=1440; PlayerSettings.defaultScreenHeight=810;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.resizableWindow=true;
        PlayerSettings.runInBackground=false;
        // The local bridge is plain http on 127.0.0.1; the player blocks that by default. The microphone prompt needs its usage text.
        PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed;
        PlayerSettings.macOS.microphoneUsageDescription="BREATHE uses the microphone you select so you can answer interview questions out loud. Audio goes to ElevenLabs for transcription only while you hold V.";
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("BREATHE_SCENE_READY: First-person Main scene generated successfully.");
    }

    [MenuItem("BREATHE/Build Mac core")]
    public static void BuildMac()
    {
        Prepare();
        string output=Environment.GetEnvironmentVariable("BREATHE_BUILD_PATH");
        if(string.IsNullOrEmpty(output)) output="Builds/Mac/BREATHE.EXE.app";
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.StandaloneOSX,
            options=BuildOptions.None
        });
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("BREATHE Mac build failed: "+report.summary.result);
        Debug.Log("BREATHE_BUILD_SUCCESS: "+Path.GetFullPath(output));
    }
}
