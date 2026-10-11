using System.Collections.Generic;
using UnityEngine;

namespace Breathe
{
    /// <summary>
    /// The whole mix. Music is always quieter than speech and ducks while Vale talks. Safe acts play the safe office music (which slowly tape-warps
    /// during the interview before horror); horror plays the long horror loop, introduced the first time by the laugh. Also owns every sound effect.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        public enum Mode { Silent, Safe, Horror }
        public const float SafeVolume = .34f, HorrorVolume = .36f, LaughVolume = .7f, CueVolume = .9f;

        AudioSource safeSource, laughSource, loopSource, cues, stepsPlayer, stepsVale;
        AudioLowPassFilter safeLow;
        AudioClip safeClip, laughClip, loopClip, click, stinger, fallbackWarm, fallbackDark;
        GameAssets assets;
        readonly List<AudioSource> pool = new List<AudioSource>();
        Mode mode = Mode.Silent;
        bool paused, loopPending, heardHorror;
        double loopStart;
        float duck = 1f, duckGoal = 1f, fade = 1f, tape;
        Transform valeTransform;
        public Mode Current => mode;
        public float LaughLength => laughClip ? laughClip.length : 0f;
        public bool HasAuthoredMusic => safeClip && laughClip && loopClip;
        /// <summary>True after the first horror start (later starts skip the laugh and go straight into the loop).</summary>
        public bool HeardHorror => heardHorror;
        public float Tape => tape;
        public float SafePitch => safeSource ? safeSource.pitch : 1f;
        public float Duck => duck;
        /// <summary>Seconds of the laugh still to play (used for the black screen).</summary>
        public float LaughRemaining => mode == Mode.Horror && laughClip ? Mathf.Max(0f, laughClip.length - laughSource.time) * (laughSource.isPlaying || paused ? 1f : 0f) : 0f;

        public void Configure(GameAssets a)
        {
            assets = a; if (a) { safeClip = a.safe; laughClip = a.laugh; loopClip = a.horror; }
        }

        void Awake()
        {
            safeSource = Make("Safe music", SafeVolume, true); laughSource = Make("Laugh", LaughVolume, false); loopSource = Make("Horror music", HorrorVolume, true);
            safeLow = safeSource.gameObject.AddComponent<AudioLowPassFilter>(); safeLow.cutoffFrequency = 22000f;
            cues = Make("Cues", CueVolume, false);
            stepsPlayer = Make("Footsteps", .0f, true); stepsVale = Make("Vale footsteps", 0f, true); stepsVale.spatialBlend = 1f; ConfigureSpatial(stepsVale);
            for (int i = 0; i < 8; i++) { var s = Make("SFX " + i, 1f, false); s.spatialBlend = 1f; ConfigureSpatial(s); pool.Add(s); }
            click = Tone("Pen click", 1050f, .1f, 180f); stinger = Stinger(); fallbackWarm = Synth(false); fallbackDark = Synth(true);
        }

        void Start() { if (assets && assets.walking) { stepsPlayer.clip = assets.walking; stepsVale.clip = assets.walking; } }

        AudioSource Make(string name, float volume, bool loop)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var s = child.AddComponent<AudioSource>(); s.playOnAwake = false; s.loop = loop; s.volume = volume; s.spatialBlend = 0; s.dopplerLevel = 0f; return s;
        }
        static void ConfigureSpatial(AudioSource s) { s.dopplerLevel = 0f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 1.2f; s.maxDistance = 16f; s.spread = 30f; }

        // ------------------------------------------------------------------ music
        public void Apply(bool horror, bool enabled, bool isPaused)
        {
            var wanted = !enabled ? Mode.Silent : horror ? Mode.Horror : Mode.Safe;
            if (isPaused != paused) { paused = isPaused; if (paused) Pause(); else Resume(); }
            if (wanted != mode)
            {
                StopMusic(); mode = wanted;
                if (mode == Mode.Safe) { Load(safeSource, safeClip ? safeClip : fallbackWarm); safeSource.pitch = 1f; if (!paused) safeSource.Play(); else safeSource.Pause(); }
                else if (mode == Mode.Horror) BeginHorror();
            }
            // smooth ducking and tape warp every frame
            if (!paused) { duck = Mathf.MoveTowards(duck, duckGoal, Time.unscaledDeltaTime * (duckGoal < duck ? 6f : 1.4f)); }
            safeSource.volume = SafeVolume * duck; loopSource.volume = HorrorVolume * duck; laughSource.volume = LaughVolume * Mathf.Lerp(duck, 1f, .6f);
            ApplyTape();
            if (!enabled) { stepsPlayer.volume = 0; stepsVale.volume = 0; }
        }

        void Load(AudioSource source, AudioClip clip) { source.clip = clip; source.time = 0; }

        /// <summary>Safe music goes slowly out of tune and slows down (tape warp) as the interview approaches its turn. 0 = normal.</summary>
        public void SetTape(float amount) { tape = Mathf.Clamp01(amount); }
        void ApplyTape()
        {
            if (mode != Mode.Safe) { if (safeSource.pitch != 1f) safeSource.pitch = 1f; safeLow.cutoffFrequency = 22000f; return; }
            float wobble = Mathf.Sin(Time.unscaledTime * 1.7f) * .025f * tape + Mathf.Sin(Time.unscaledTime * .43f) * .03f * tape;
            safeSource.pitch = Mathf.Clamp(1f - tape * .3f + wobble, .55f, 1.05f);
            safeLow.cutoffFrequency = Mathf.Lerp(22000f, 1800f, tape * tape);
        }

        public void SetDuck(bool speaking) { duckGoal = speaking ? .38f : 1f; }

        void BeginHorror()
        {
            if (!HasAuthoredMusic)
            {   // Offline test builds without the authored clips: a synthesised drone stands in.
                Load(loopSource, fallbackDark); if (!paused) loopSource.Play(); heardHorror = true; return;
            }
            Load(loopSource, loopClip);
            if (!heardHorror)
            {   // first horror: the laugh plays instantly and the loop begins the exact moment it ends
                heardHorror = true; Load(laughSource, laughClip);
                if (paused) { laughSource.Pause(); loopPending = true; return; }
                laughSource.Play(); ScheduleLoop(laughClip.length);
            }
            else if (!paused) loopSource.Play();   // later rounds: straight into the loop
        }

        void ScheduleLoop(float delay) { loopStart = AudioSettings.dspTime + delay; loopSource.PlayScheduled(loopStart); loopPending = false; }

        void Pause()
        {
            safeSource.Pause(); laughSource.Pause(); cues.Pause(); stepsPlayer.Pause(); stepsVale.Pause(); foreach (var s in pool) s.Pause();
            if (mode == Mode.Horror && HasAuthoredMusic)
            {
                bool started = AudioSettings.dspTime >= loopStart && !loopPending;
                if (started) loopSource.Pause(); else { loopSource.Stop(); loopPending = true; }
            }
            else loopSource.Pause();
        }

        void Resume()
        {
            if (mode == Mode.Safe) safeSource.UnPause();
            else if (mode == Mode.Horror)
            {
                if (HasAuthoredMusic && loopPending)
                {
                    laughSource.UnPause(); loopSource.clip = loopClip; loopSource.time = 0;
                    ScheduleLoop(Mathf.Max(0f, laughClip.length - laughSource.time));
                }
                else if (loopSource.clip) { if (loopSource.time > 0f || loopSource.isPlaying) loopSource.UnPause(); else loopSource.Play(); }
            }
            cues.UnPause(); stepsPlayer.UnPause(); stepsVale.UnPause(); foreach (var s in pool) s.UnPause();
        }

        void StopMusic() { safeSource.Stop(); laughSource.Stop(); loopSource.Stop(); loopPending = false; }

        // ------------------------------------------------------------------ footsteps
        /// <summary>speed01: 0 still .. 1 sprint. The player's own steps are flat; Vale's are 3D and follow him.</summary>
        public void SetFootsteps(bool enabled, float playerSpeed01, float valeSpeed01, Transform vale)
        {
            if (!assets || !assets.walking) return;
            valeTransform = vale;
            SetStep(stepsPlayer, enabled ? playerSpeed01 : 0f, .55f, 1f);
            if (vale) { stepsVale.transform.position = vale.position; }
            SetStep(stepsVale, enabled && vale ? valeSpeed01 : 0f, 1f, .92f);
        }
        void SetStep(AudioSource s, float speed01, float volume, float pitchBase)
        {
            if (speed01 > .05f && !paused) { if (!s.isPlaying) s.Play(); s.volume = volume * Mathf.Clamp01(speed01 * 1.3f) * duck; s.pitch = pitchBase * Mathf.Lerp(.85f, 1.3f, Mathf.Clamp01(speed01)); }
            else if (s.isPlaying) { s.volume = Mathf.MoveTowards(s.volume, 0f, Time.unscaledDeltaTime * 4f); if (s.volume <= .01f) s.Stop(); }
        }

        // ------------------------------------------------------------------ effects
        public void Click(bool enabled) { if (enabled) cues.PlayOneShot(click, .6f); }
        public void Jumpscare(bool enabled)
        {
            if (!enabled) return;
            cues.Stop(); cues.PlayOneShot(assets && assets.jumpscare ? assets.jumpscare : stinger, 1f);
        }
        public void SilenceCues() { cues.Stop(); }
        /// <summary>One-shot at a world position (doors, knife) or flat when position is null.</summary>
        public void Play(AudioClip clip, bool enabled, float volume = 1f, Vector3? position = null)
        {
            if (!enabled || !clip || paused) return;
            if (!position.HasValue) { cues.PlayOneShot(clip, volume); return; }
            foreach (var s in pool) if (!s.isPlaying) { s.transform.position = position.Value; s.clip = clip; s.volume = volume; s.pitch = 1f; s.Play(); return; }
        }
        public void DoorOpen(bool on, Vector3 at) => Play(assets ? assets.doorOpen : null, on, 1f, at);
        public void DoorBurst(bool on, Vector3 at) => Play(assets ? assets.doorBreak : null, on, 1f, at);
        public void Paper(bool on) => Play(assets ? assets.paper : null, on, .8f);
        public void KnifeSwing(bool on, Vector3 at) => Play(assets ? assets.knifeSwing : null, on, 1f, at);
        public void KnifeCut(bool on) => Play(assets ? assets.knifeCut : null, on, 1f);

        AudioClip Tone(string label, float hz, float seconds, float decay)
        {
            const int rate = 22050; var samples = new float[(int)(rate * seconds)];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * 2 * Mathf.PI * hz / rate) * Mathf.Exp(-i / decay) * .3f;
            var clip = AudioClip.Create(label, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }

        /// <summary>Stand-in for the authored jumpscare when the clip is missing: one harsh descending burst.</summary>
        AudioClip Stinger()
        {
            const int rate = 44100; var data = new float[(int)(rate * 1.1f)]; var rng = new System.Random(7); float phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate, env = Mathf.Clamp01(t * 80f) * Mathf.Exp(-t * 2.6f);
                float hz = Mathf.Lerp(1100f, 140f, Mathf.Clamp01(t / .9f)); phase += 2 * Mathf.PI * hz / rate;
                float saw = (phase / Mathf.PI % 2f) - 1f, noise = (float)rng.NextDouble() * 2f - 1f;
                data[i] = Mathf.Clamp((saw * .55f + noise * .45f) * env * 1.3f, -.95f, .95f);
            }
            var clip = AudioClip.Create("Jumpscare stinger", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        AudioClip Synth(bool tense)
        {
            const int rate = 22050; var data = new float[rate * 8];
            float[] notes = tense ? new[] { 55f, 58.27f, 82.41f, 55f } : new[] { 261.63f, 329.63f, 392f, 329.63f };
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate, note = notes[(int)(t * 2) % notes.Length];
                float envelope = tense ? .55f : Mathf.Exp(-(t * 2 % 1) * 5), fadeEnv = Mathf.Min(1, Mathf.Min(t * 4, (8 - t) * 4));
                data[i] = Mathf.Sin(t * 2 * Mathf.PI * note) * envelope * .18f * fadeEnv;
            }
            var clip = AudioClip.Create(tense ? "After hours (fallback)" : "Office hold music (fallback)", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        void OnDestroy() { foreach (var c in new[] { click, stinger, fallbackWarm, fallbackDark }) if (c) Destroy(c); }
    }
}
