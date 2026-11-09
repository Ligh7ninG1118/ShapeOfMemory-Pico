using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    // Gameplay consumes the host player's hands; it never starts a loader or moves the rig.
    public abstract class ShadowPlayerContext : MonoBehaviour
    {
        public abstract bool Active { get; }
        public abstract bool HeadReady { get; }
        public virtual bool UsesNativeMenu => true;
        public abstract bool ReadHand(bool left, out Pose palm, out Vector3 tip, out float pinchDistance);
        // World-space aiming ray of a hand (forward = +Z), used to point the torch. False when unavailable.
        public virtual bool ReadAim(bool left, out Pose aim) { aim = default; return false; }
        public abstract void Recenter();
        public abstract void SwitchLevel(string level);
    }
}
