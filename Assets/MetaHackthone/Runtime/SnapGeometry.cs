using System;
using UnityEngine;

namespace PhysicalAssembly
{
    // The workshop's nearest-twist search, shared by socket and free-placement snapping.
    public static class SnapGeometry
    {
        public static Quaternion AroundAxis(Quaternion current, Quaternion reference, Vector3 axis, float degrees)
        {
            if (degrees <= 0 || !float.IsFinite(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees));
            Quaternion best = reference;
            float smallest = float.MaxValue;
            int count = Mathf.CeilToInt(360 / degrees);
            for (int i = 0; i < count; i++)
            {
                var candidate = Quaternion.AngleAxis(i * degrees, axis) * reference;
                float angle = Quaternion.Angle(current, candidate);
                if (angle < smallest) { smallest = angle; best = candidate; }
            }
            return best;
        }

        // Pose is relative to the workbench, so recentering does not change the grid.
        public static Pose Grid(Pose pose, float metres, float degrees)
        {
            if (metres <= 0 || !float.IsFinite(metres)) throw new ArgumentOutOfRangeException(nameof(metres));
            var p = pose.position;
            var e = pose.rotation.eulerAngles;
            var x = AroundAxis(Quaternion.AngleAxis(e.x, Vector3.right), Quaternion.identity, Vector3.right, degrees);
            var y = AroundAxis(Quaternion.AngleAxis(e.y, Vector3.up), Quaternion.identity, Vector3.up, degrees);
            var z = AroundAxis(Quaternion.AngleAxis(e.z, Vector3.forward), Quaternion.identity, Vector3.forward, degrees);
            return new Pose(new Vector3(Mathf.Round(p.x / metres) * metres, Mathf.Round(p.y / metres) * metres,
                Mathf.Round(p.z / metres) * metres), y * x * z);
        }
    }
}
