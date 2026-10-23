using UnityEngine;

namespace OwlchemyVR
{
    // Replaces SteamVR_TrackedInputController on the hand prefab (same component slot, so every reference to it
    // stays valid). Button states come from FlatControls instead of a Vive wand; the buttons mean what they mean
    // on a Vive: Trigger = grab (GrabCustom), Grip = use/squeeze (InteractCustom), Trackpad = teleport, Menu = menu.
    public class Flat_TrackedInputController : TrackedController
    {
        public enum Btn { Grab, Use, Teleport, Menu, Count }

        readonly bool[] pending = new bool[(int)Btn.Count];
        readonly bool[] now = new bool[(int)Btn.Count];
        readonly bool[] prev = new bool[(int)Btn.Count];
        int tickedFrame = -1;

        public Vector2 trackpad;

        void Start()
        {
            isControllerReady = true;
        }

        public void Set(Btn b, bool down)
        {
            pending[(int)b] = down;
        }

        public bool IsDown(Btn b)
        {
            return pending[(int)b];
        }

        // Called once per frame by FlatControls before gameplay scripts run, so Down/Up last exactly one frame.
        public void Tick()
        {
            if (tickedFrame == Time.frameCount) return;
            tickedFrame = Time.frameCount;
            for (int i = 0; i < now.Length; i++)
            {
                prev[i] = now[i];
                now[i] = pending[i];
            }
        }

        static int Slot(ControllerButton b)
        {
            switch (b)
            {
                case ControllerButton.Trigger:
                case ControllerButton.GrabCustom:
                case ControllerButton.MouseClick:
                    return (int)Btn.Grab;
                case ControllerButton.Grip:
                case ControllerButton.InteractCustom:
                    return (int)Btn.Use;
                case ControllerButton.Trackpad:
                case ControllerButton.Joystick:
                    return (int)Btn.Teleport;
                case ControllerButton.Menu:
                    return (int)Btn.Menu;
                default:
                    return -1;
            }
        }

        protected override bool GetButtonDetailedControllerIsReady(ControllerButton controllerButton, ButtonStates state, bool isTouch)
        {
            int i = Slot(controllerButton);
            if (i < 0) return false;
            switch (state)
            {
                case ButtonStates.Current: return now[i];
                case ButtonStates.Down: return now[i] && !prev[i];
                case ButtonStates.Up: return !now[i] && prev[i];
                default: return false;
            }
        }

        protected override float GetButtonDetailedRawControllerIsReady(ControllerButton controllerButton)
        {
            int i = Slot(controllerButton);
            return i >= 0 && now[i] ? 1f : 0f;
        }

        public override Vector2 GetTrackPadPos()
        {
            return trackpad * 32767f;
        }

        public override void TriggerHapticPulse(float usDurationMicroSec)
        {
        }

        public override void ClearHaptics()
        {
        }
    }
}
