// Unity 5.4's UnityEngine.VR API, removed in later Unity versions, re-created for the flat (no headset) build.
// Everything reports "no VR device"; head tracking queries return the flat rig's head pose so code that reads
// InputTracking still sees where the player is looking.
using System;

namespace UnityEngine.VR
{
    public enum VRNode { LeftEye = 0, RightEye = 1, CenterEye = 2, Head = 3, LeftHand = 4, RightHand = 5, GameController = 6, TrackingReference = 7, HardwareTracker = 8 }

    public enum VRDeviceType { None = 0, Stereo = 1, Split = 2, Oculus = 3, PlayStationVR = 4, Morpheus = 4, Unknown = 5 }

    public enum TrackingSpaceType { Stationary = 0, RoomScale = 1 }

    public static class VRSettings
    {
        public static bool enabled { get { return false; } set { } }
        public static bool isDeviceActive { get { return false; } }
        public static bool showDeviceView { get; set; }
        public static float renderScale { get; set; } = 1f;
        public static float renderViewportScale { get; set; } = 1f;
        public static int eyeTextureWidth { get { return Screen.width; } }
        public static int eyeTextureHeight { get { return Screen.height; } }
        public static string loadedDeviceName { get { return ""; } }
        public static VRDeviceType loadedDevice { get { return VRDeviceType.None; } set { } }
        public static void LoadDeviceByName(string deviceName) { }
        public static void LoadDeviceByName(string[] prioritizedDeviceNameList) { }
    }

    public static class VRDevice
    {
        public static bool isPresent { get { return false; } }
        public static string family { get { return ""; } }
        public static string model { get { return ""; } }
        public static float refreshRate { get { return 60f; } }
        public static IntPtr GetNativePtr() { return IntPtr.Zero; }
        public static TrackingSpaceType GetTrackingSpaceType() { return TrackingSpaceType.RoomScale; }
        public static bool SetTrackingSpaceType(TrackingSpaceType t) { return true; }
    }

    public static class VRStats
    {
        public static float gpuTimeLastFrame { get { return 0f; } }
    }

    public static class InputTracking
    {
        // Set every frame by the flat rig (local to the play area, like real tracking data).
        public static Vector3 headPosition = new Vector3(0f, 1.6f, 0f);
        public static Quaternion headRotation = Quaternion.identity;

        public static Vector3 GetLocalPosition(VRNode node)
        {
            const float halfIpd = 0.032f;
            switch (node)
            {
                case VRNode.LeftEye: return headPosition + headRotation * new Vector3(-halfIpd, 0f, 0f);
                case VRNode.RightEye: return headPosition + headRotation * new Vector3(halfIpd, 0f, 0f);
                case VRNode.CenterEye:
                case VRNode.Head: return headPosition;
                default: return Vector3.zero;
            }
        }

        public static Quaternion GetLocalRotation(VRNode node)
        {
            return node <= VRNode.Head ? headRotation : Quaternion.identity;
        }

        public static void Recenter() { }
    }
}
