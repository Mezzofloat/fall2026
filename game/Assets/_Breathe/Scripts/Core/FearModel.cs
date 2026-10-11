using System;

namespace Breathe
{
    /// <summary>
    /// Game-only overall stress score ("fear") fused from live signals: pulse change from baseline, glove IMU fidgeting and voice loudness.
    /// No single sensor decides: with several signals they are blended, with one it stands alone.
    /// This is a gameplay variable, not a psychological or clinical measurement. Pure C#, no Unity types, unit-tested.
    /// </summary>
    public sealed class FearModel
    {
        public struct Input
        {
            public bool PulseValid;        // fresh, stable, baseline-ready pulse reading
            public float PulseDeltaBpm;    // bpm above the calibration baseline (can be negative)
            public bool FidgetValid;       // at least one glove is connected and calibrated
            public float Fidget;           // 0..1 wrist restlessness above the player's own rest level
            public bool VoiceActive;       // microphone is currently capturing the player's answer
            public float VoiceLoudness;    // 0..1 smoothed mic level while VoiceActive
            public bool DevOverride;       // keyboard dev control when there is no live pulse (clearly labelled in the HUD)
            public float DevFear;          // 0..1
        }

        // Tunables (play-test these).
        public float NeutralFear = .35f;       // used when there is no signal at all
        public float PulseFloorBpm = -4f;      // delta that maps to fear 0
        public float PulseSpanBpm = 28f;       // bpm range above the floor that maps to 0..1
        public float CalmFear = .10f;          // below this the player is "very calm"
        public float PanicFear = .70f;         // above this the player is "too stressed"
        public float SpaceOutDelay = 6f;       // seconds of real calm before "spacing out" begins
        public float SpaceOutRamp = 4f;        // seconds for the blur to reach full strength
        public float MinSpeedScale = .55f;     // movement never drops below this
        public float Smoothing = 2.5f;         // seconds (exponential)
        public float MaxPulseHz = 2f;          // red pulse never faster than this (photosensitivity safety)
        public float PulseWeight = .6f;        // blend when both pulse and glove are live
        public float DoorBurstFear = .6f;      // above this stress Mr. Vale bursts through doors instead of waiting

        public float Fear { get; private set; } = .35f;
        public float CalmSeconds { get; private set; }
        public bool Live { get; private set; }   // fear currently comes from a real body signal (pulse and/or glove)
        public enum SourceKind { Neutral, Dev, Pulse, Glove, PulseAndGlove }
        public SourceKind Source { get; private set; } = SourceKind.Neutral;
        public float PulseComponent { get; private set; }
        public float GloveComponent { get; private set; }
        public float VoiceComponent { get; private set; }
        /// <summary>The overall stress score (same value as Fear).</summary>
        public float Stress => Fear;
        /// <summary>Stressed enough that doors do not slow Mr. Vale down.</summary>
        public bool Elevated => Fear >= DoorBurstFear;

        /// <summary>Menus show no stress effects at all: while muted every effect property is zero.</summary>
        public bool Muted;
        public float Panic => Muted ? 0f : Clamp01((Fear - PanicFear) / Math.Max(.01f, 1f - PanicFear));
        public float SpaceOut => Muted ? 0f : Live ? Clamp01((CalmSeconds - SpaceOutDelay) / Math.Max(.01f, SpaceOutRamp)) : 0f;
        public float SpeedScale => 1f - Panic * (1f - MinSpeedScale);
        public float LookScale => 1f - SpaceOut * .45f;
        public float ShakeAmplitude => Muted ? 0f : Panic * .035f;                       // metres of camera offset
        public float RedPulseHz => Math.Min(MaxPulseHz, .7f + Fear * 1.3f);
        public float RedPulseDepth => Muted ? 0f : Clamp01(.05f + Fear * .25f + Panic * .35f);

        public static float PulseToFear(float deltaBpm, float floor, float span) => Clamp01((deltaBpm - floor) / Math.Max(1f, span));

        public void Reset() { Fear = NeutralFear; CalmSeconds = 0; Live = false; Source = SourceKind.Neutral; PulseComponent = GloveComponent = VoiceComponent = 0f; }

        public void Update(float dt, Input input)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
            dt = Math.Min(dt, .25f);
            bool pulseOk = input.PulseValid && IsFinite(input.PulseDeltaBpm), gloveOk = input.FidgetValid && IsFinite(input.Fidget);
            PulseComponent = pulseOk ? PulseToFear(input.PulseDeltaBpm, PulseFloorBpm, PulseSpanBpm) : 0f;
            GloveComponent = gloveOk ? Clamp01(input.Fidget) * .9f + .05f : 0f;
            float target;
            if (pulseOk && gloveOk) { target = PulseComponent * PulseWeight + GloveComponent * (1f - PulseWeight); Source = SourceKind.PulseAndGlove; Live = true; }
            else if (pulseOk) { target = PulseComponent; Source = SourceKind.Pulse; Live = true; }
            else if (gloveOk) { target = GloveComponent; Source = SourceKind.Glove; Live = true; }
            else
            {
                Live = false;
                bool dev = input.DevOverride && IsFinite(input.DevFear);
                target = dev ? Clamp01(input.DevFear) : NeutralFear; Source = dev ? SourceKind.Dev : SourceKind.Neutral;
            }
            VoiceComponent = 0f;
            if (input.VoiceActive && IsFinite(input.VoiceLoudness))
            {
                VoiceComponent = Clamp01(input.VoiceLoudness);
                target = Clamp01(target * .75f + VoiceComponent * .25f + (input.VoiceLoudness > .6f ? .05f : 0f));
            }
            Fear += (target - Fear) * (1f - (float)Math.Exp(-dt / Math.Max(.1f, Smoothing)));
            Fear = Clamp01(Fear);
            CalmSeconds = Live && Fear < CalmFear ? CalmSeconds + dt : 0f;
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
