using UnityEngine;

namespace Breathe
{
    /// <summary>Scene-serialised references to authored assets. Filled by BreatheBuild.Prepare (never by path at runtime).</summary>
    public sealed class GameAssets : MonoBehaviour
    {
        /// <summary>Safe office music (calm acts) and the long horror loop; the laugh plays once when horror first begins.</summary>
        public AudioClip safe, horror, laugh;
        public AudioClip jumpscare, doorOpen, doorBreak, knifeCut, knifeSwing, paper, walking;
        /// <summary>Pixel fonts (OFL): Silkscreen Bold for headings and buttons, VT323 for body text.</summary>
        public Font headFont, bodyFont;
        /// <summary>Pen-knife parts in the order Barrel, Collar, Grip, Clip, Plunger, Blade.</summary>
        public GameObject[] knifeParts;
        /// <summary>The approved reception/interview kit parts (DeskBody, MonitorBezel, ...). Looked up by name.</summary>
        public GameObject[] kitParts;
        /// <summary>The approved office prop set (chairs, cabinets, racks, doors...). Looked up by name.</summary>
        public GameObject[] propParts;
        /// <summary>Back-compat for tests that only set a single loop clip.</summary>
        public AudioClip loop { get => horror; set => horror = value; }
    }
}
