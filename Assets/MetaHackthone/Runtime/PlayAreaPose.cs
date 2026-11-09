using UnityEngine;

namespace PhysicalAssembly
{
    // One optional floor anchor for this app session. Never written into construction saves.
    public static class PlayAreaPose
    {
        public static bool IsSet { get; private set; }
        public static Pose Pose { get; private set; }
        public static int Version { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { IsSet=false; Pose=UnityEngine.Pose.identity; Version=0; }

        public static void Set(Vector3 position, Quaternion rotation, float floorHeight)
        {
            position.y=floorHeight;
            var forward=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up);
            Pose=new Pose(position,Quaternion.LookRotation(forward.sqrMagnitude>.001f?forward:Vector3.forward));
            IsSet=true; Version++;
        }

        public static Pose AtHeight(float height) => new(Pose.position+Vector3.up*height,Pose.rotation);
    }
}
