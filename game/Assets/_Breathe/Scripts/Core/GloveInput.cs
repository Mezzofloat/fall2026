using UnityEngine;

namespace Breathe
{
    /// <summary>
    /// The two gloves as a game controller. LEFT joystick walks (up = forward, sideways = strafe) and its click is E (interact).
    /// RIGHT joystick looks (sideways = turn, up/down = pitch) and its click is V (hold to talk). Pure logic: the bridge status feeds it.
    /// A glove that stops talking is treated as released, so a dead radio can never leave the player walking.
    /// </summary>
    public sealed class GloveInput
    {
        public const float Deadzone = .14f;
        public bool InvertLeftY, InvertRightY, SwapSticks;
        public Vector2 Move { get; private set; }       // x strafe, y forward, -1..1
        public Vector2 Look { get; private set; }       // x yaw, y pitch (up = look up), -1..1
        public bool Sprint { get; private set; }         // left stick pushed to the end
        public bool LeftConnected { get; private set; }
        public bool RightConnected { get; private set; }
        public bool Active => LeftConnected || RightConnected;
        public bool InteractHeld { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool TalkHeld { get; private set; }
        public bool TalkPressed { get; private set; }
        public bool TalkReleased { get; private set; }
        public float Fidget { get; private set; }
        public bool FidgetValid { get; private set; }

        /// <summary>Stick percent (-100..100) to -1..1 with a deadzone and a gentle curve for fine control near the centre.</summary>
        public static float Shape(float percent)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent)) return 0f;
            float v = Mathf.Clamp(percent / 100f, -1f, 1f), a = Mathf.Abs(v);
            if (a < Deadzone) return 0f;
            float scaled = (a - Deadzone) / (1f - Deadzone);
            return Mathf.Sign(v) * scaled * (.35f + .65f * scaled);
        }

        public void Update(ImuReading imu)
        {
            InteractPressed = TalkPressed = TalkReleased = false;
            var left = imu?.left; var right = imu?.right;
            if (SwapSticks) { var t = left; left = right; right = t; }
            LeftConnected = left != null && left.connected; RightConnected = right != null && right.connected;
            Move = LeftConnected ? new Vector2(Shape(left.jx), Shape(InvertLeftY ? -left.jy : left.jy)) : Vector2.zero;
            Sprint = LeftConnected && Move.magnitude > .97f;
            Look = RightConnected ? new Vector2(Shape(right.jx), Shape(InvertRightY ? -right.jy : right.jy)) : Vector2.zero;
            bool interact = LeftConnected && left.click, talk = RightConnected && right.click;
            InteractPressed = interact && !InteractHeld; InteractHeld = interact;
            TalkPressed = talk && !TalkHeld; TalkReleased = !talk && TalkHeld; TalkHeld = talk;
            FidgetValid = imu != null && imu.connected && !imu.calibrating; Fidget = FidgetValid ? Mathf.Clamp01(imu.fidget) : 0f;
        }
    }
}
