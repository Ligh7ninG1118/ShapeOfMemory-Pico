using UnityEngine;
using XRSettings = UnityEngine.XR.XRSettings;

namespace PhysicalAssembly.MetaXR
{
    // Shared player setup; gameplay adapters must not own camera tracking.
    public sealed class MetaPlayerRig : MonoBehaviour
    {
        public static MetaPlayerRig Shared { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShared() => Shared=null;
        public Transform XRRig;
        public Camera DesktopCamera;
        public bool IsXR => !Application.isEditor && (XRSettings.isDeviceActive || Application.platform == RuntimePlatform.Android);
        public Camera ViewCamera => !IsXR ? DesktopCamera : XRRig.GetComponent<OVRCameraRig>() is OVRCameraRig ovr ? ovr.centerEyeAnchor.GetComponent<Camera>() : XRRig.GetComponentInChildren<Camera>(true);
        public float FloorHeight => XRRig.GetComponent<OVRCameraRig>() is OVRCameraRig ovr ? ovr.trackingSpace.position.y : XRRig.position.y;
        public void Initialize(Transform rig, Camera desktop)
        {
            Shared=this;
            XRRig = rig; DesktopCamera = desktop;
            if (!desktop.GetComponent<AudioListener>()) desktop.gameObject.AddComponent<AudioListener>();
            ConfigureRoomScaleRig(rig);
            var manager = rig.GetComponent<OVRManager>();
            if(manager)
            {
                manager.usePositionTracking = true; manager.useRotationTracking = true;
                manager.AllowRecenter = false; manager.trackingOriginType = OVRManager.TrackingOrigin.Stage;
            }
            desktop.gameObject.SetActive(!IsXR); rig.gameObject.SetActive(IsXR);
        }
        public static void ConfigureRoomScaleRig(Transform rig)
        {
            // ComprehensiveInteractionRig includes a VR locomotor with gravity,
            // grounding and a player capsule. It moves the tracking origin as the
            // user's head moves. MR uses physical room-scale movement, so disable
            // the whole locomotor (including its colliders) and its
            // gesture/controller input groups.
            foreach(var child in rig.GetComponentsInChildren<Transform>(true))
                if(child.name=="Locomotor" || child.name.Contains("Locomotion"))
                    child.gameObject.SetActive(false);
            foreach(var locomotor in rig.GetComponentsInChildren<Oculus.Interaction.Locomotion.FirstPersonLocomotor>(true))
                locomotor.gameObject.SetActive(false);
        }
        void OnDestroy() { if(Shared==this) Shared=null; }
    }
}
