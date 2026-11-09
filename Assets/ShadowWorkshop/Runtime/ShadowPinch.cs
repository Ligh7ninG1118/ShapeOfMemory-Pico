using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    // Tight physical pinch, with only 2 mm of hysteresis. Release has no timer or smoothing.
    public static class ShadowPinch
    {
        public const float CloseDistance = .018f, ReleaseDistance = .020f;
        public static bool IsClosed(float distance, bool wasClosed) => distance < (wasClosed ? ReleaseDistance : CloseDistance);
    }
}
