using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    // Release is immediate. Only rearming a NEW grab needs a short, valid open-hand interval.
    // A one-frame threshold crossing or a missing joint must not grab a neighbouring block.
    public sealed class ShadowPinchGesture
    {
        public const float OpenToRearmSeconds = .08f;
        public bool Closed { get; private set; }
        public bool Pressed { get; private set; }
        bool armed;
        float openTime;

        public void Sample(bool tracked, float distance, float deltaTime)
        {
            if (!tracked || float.IsNaN(distance) || float.IsInfinity(distance)) { Reset(); return; }
            bool wasClosed = Closed;
            Closed = ShadowPinch.IsClosed(distance, wasClosed);
            Pressed = Closed && !wasClosed && armed;
            if (Closed)
            {
                armed = false;
                openTime = 0;
            }
            else if (distance >= ShadowPinch.ReleaseDistance)
            {
                // Cap each observation so a single slow frame cannot count as stable opening.
                openTime += Mathf.Clamp(deltaTime, 0, .025f);
                if (openTime >= OpenToRearmSeconds) armed = true;
            }
            else openTime = 0;
        }

        public void Reset() { Closed = Pressed = armed = false; openTime = 0; }
    }
}
