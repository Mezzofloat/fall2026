using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Breathe
{
    /// <summary>
    /// One runtime global Volume (grain, vignette, bloom, chromatic aberration, colour, blur) plus the Buckshot-style CRT/dither pass
    /// (shader globals read by Breathe/CrtDither). Safe mode is clean and sleek; horror is crunchy, desaturated and red-brown.
    /// </summary>
    public sealed class PostFx
    {
        readonly GameObject host;
        readonly VolumeProfile profile;
        readonly Vignette vignette; readonly FilmGrain grain; readonly ChromaticAberration aberration;
        readonly Bloom bloom; readonly ColorAdjustments color; readonly DepthOfField depth;
        static readonly int BrParams = Shader.PropertyToID("_BrParams"), BrTint = Shader.PropertyToID("_BrTint"), BrFx = Shader.PropertyToID("_BrFx");

        bool paused; float modeBlend; float eyeOpen = 1f; bool horrorTarget;
        /// <summary>0 (eye shut) to 1 (open): opening eyes are blurry and tunnel-visioned.</summary>
        public float EyeOpen { get => eyeOpen; set => eyeOpen = Mathf.Clamp01(value); }
        /// <summary>0 = safe look, 1 = horror look (eased).</summary>
        public float ModeBlend => modeBlend;
        /// <summary>Set false to disable the dither pass (screenshots of raw geometry, tests).</summary>
        public static bool CrtEnabled = true;
        public void SetPaused(bool value) { paused = value; }
        public void SnapMode(bool horror) { horrorTarget = horror; modeBlend = horror ? 1f : 0f; }

        public PostFx(Camera camera, Transform parent)
        {
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            host = new GameObject("Post FX", typeof(Volume)); host.transform.SetParent(parent, false);
            var volume = host.GetComponent<Volume>(); volume.isGlobal = true; volume.priority = 10;
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); volume.sharedProfile = profile;
            vignette = profile.Add<Vignette>(false); grain = profile.Add<FilmGrain>(false); aberration = profile.Add<ChromaticAberration>(false);
            bloom = profile.Add<Bloom>(false); color = profile.Add<ColorAdjustments>(false); depth = profile.Add<DepthOfField>(false);
            foreach (VolumeComponent component in new VolumeComponent[] { vignette, grain, aberration, bloom, color }) component.active = true;
            depth.active = false;
            grain.type.Override(FilmGrainLookup.Thin1); grain.response.Override(.8f);
            aberration.intensity.Override(.04f);
            bloom.threshold.Override(.92f); bloom.intensity.Override(.7f); bloom.scatter.Override(.65f);
            depth.mode.Override(DepthOfFieldMode.Gaussian); depth.gaussianStart.Override(0f); depth.gaussianEnd.Override(.1f);
            vignette.smoothness.Override(.55f);
            color.postExposure.Override(.1f);
        }

        /// <summary>Calm band blurs and desaturates; stress band pulses red. The red pulse is capped at 2 Hz (photosensitivity safety) and smooth.</summary>
        public void Update(FearModel fear, bool horror, float time, float dt = .016f)
        {
            horrorTarget = horror; modeBlend = Mathf.MoveTowards(modeBlend, horror ? 1f : 0f, dt * 3f);
            float m = modeBlend;
            float wave = .5f + .5f * Mathf.Sin(time * 2f * Mathf.PI * fear.RedPulseHz);
            float red = Mathf.Clamp01(fear.RedPulseDepth * wave * Mathf.Lerp(.3f, 1f, m));
            grain.intensity.Override(Mathf.Lerp(.12f, .25f, m));
            float eyeShade = 1f - eyeOpen;
            vignette.intensity.Override(Mathf.Clamp01(Mathf.Lerp(.16f, .26f, m) + fear.SpaceOut * .3f + fear.Panic * .1f + eyeShade * .5f));
            vignette.color.Override(Color.Lerp(Color.black, new Color(.6f, 0f, .03f), Mathf.Clamp01(red * 1.5f)));
            color.colorFilter.Override(Color.Lerp(Color.white, new Color(1f, .6f, .6f), red * .85f));
            color.saturation.Override(Mathf.Lerp(8f, -10f, m) - fear.SpaceOut * 50f);
            color.contrast.Override(Mathf.Lerp(4f, 8f, m));
            if (paused) { color.saturation.Override(-70f); color.postExposure.Override(-.9f); } else color.postExposure.Override(Mathf.Lerp(.15f, .55f, m));
            float blur = Mathf.Max(fear.SpaceOut, eyeShade * 1.1f);
            depth.active = blur > .02f;
            if (depth.active) depth.gaussianMaxRadius.Override(Mathf.Lerp(.5f, 1.5f, Mathf.Clamp01(blur)));
            // CRT/dither pass: crisper and richer in the safe office, grittier and redder in horror.
            float lines = Mathf.Lerp(900f, 720f, m), levels = Mathf.Lerp(40f, 28f, m);
            Shader.SetGlobalVector(BrParams, CrtEnabled ? new Vector4(lines, levels, Mathf.Lerp(.25f, .35f, m), Mathf.Lerp(.015f, .04f, m)) : Vector4.zero);
            var tint = Color.Lerp(new Color(1.02f, 1f, .97f), new Color(1.12f, .92f, .88f), m);
            Shader.SetGlobalVector(BrTint, new Vector4(tint.r, tint.g, tint.b, Mathf.Lerp(1.05f, .78f, m)));
            Shader.SetGlobalVector(BrFx, new Vector4(0f, Mathf.Lerp(.1f, .2f, m), Mathf.Lerp(.006f, .014f, m), time));
        }

        public void Dispose()
        {
            Shader.SetGlobalVector(BrParams, Vector4.zero);
            if (profile) Object.Destroy(profile); if (host) Object.Destroy(host);
        }
    }
}
