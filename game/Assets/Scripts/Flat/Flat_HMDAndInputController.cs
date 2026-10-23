using UnityEngine;

namespace OwlchemyVR
{
    // Replaces SteamVR_HMDAndInputController on the player rig prefab. The rig, head, hands and everything the
    // game hangs off them stay the game's own; only the tracking source changes: FlatControls poses the head and
    // hands from touch input instead of a headset and wands.
    public class Flat_HMDAndInputController : MasterHMDAndInputController
    {
        // Kept so the SteamVR rig prefab's serialized data still matches this component.
        [SerializeField]
        protected GameObject controllerPrefabOld;

        public override void Awake()
        {
            base.Awake();
            for (int i = 0; i < hands.Count; i++)
            {
                hands[i].gameObject.SetActive(true);
                hands[i].transform.localPosition = Vector3.zero;
                hands[i].transform.localRotation = Quaternion.identity;
            }
            HMDCamera.stereoTargetEye = StereoTargetEyeMask.None;
            gameObject.AddComponent<FlatControls>();
            Debug.Log("[RMVR] flat rig ready: " + hands.Count + " hands");
        }

        protected override void EnableTracking()
        {
            HMDCamera.stereoTargetEye = StereoTargetEyeMask.None;
        }

        protected override void DisableTracking()
        {
            HMDCamera.stereoTargetEye = StereoTargetEyeMask.None;
        }
    }
}
