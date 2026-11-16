using System.Collections.Generic;
using UnityEngine;

namespace ShapeOfMemory
{
    // Made-up hand poses round a hand mesh's rest pose, for the editor preview and the tests (there is no tracking there).
    // The fingers curl toward the palm, each about its own bend axis; the thumb bends toward the index and swings onto a
    // target: the index pad (pinch), over the curled middle finger (point), or where it rests. Joints are in the mesh's
    // space, in MemoryHandSkin's order.
    public static class MemoryHandPose
    {
        static readonly int[][] Fingers={new[]{6,7,8,9,10},new[]{11,12,13,14,15},new[]{16,17,18,19,20},new[]{21,22,23,24,25}};
        // Extra curl at the knuckle, middle and last joints (degrees) per finger, at a full pinch and when pointing.
        static readonly float[][] PinchCurl={new[]{28f,36,16},new[]{40f,52,26},new[]{48f,60,30},new[]{56f,66,34}};
        static readonly float[][] PointCurl={new[]{-4f,-6,-4},new[]{70f,95,45},new[]{74f,98,46},new[]{78f,100,48}};
        // How far apart the joints of a closed pinch sit: the pads touch, the tip joints (on the finger ends) don't.
        public const float PadGap=.0145f;

        public static Vector3[] Make(IReadOnlyList<Vector3> rest,float pinch,float point=0)
        {
            pinch=Mathf.Clamp01(pinch);point=Mathf.Clamp01(Mathf.Min(point,1-pinch));
            var j=new Vector3[MemoryHandSkin.Count];for(int i=0;i<j.Length;i++)j[i]=rest[i];
            var palm=PalmSide(rest);var indexAxis=Vector3.zero;
            for(int f=0;f<4;f++)
            {
                var c=Fingers[f];var axis=BendAxis(rest[c[2]]-rest[c[1]],palm);float phi=0;
                if(f==0)indexAxis=axis;
                for(int s=0;s<3;s++)
                {
                    phi+=PinchCurl[f][s]*pinch+PointCurl[f][s]*point;
                    j[c[s+2]]=j[c[s+1]]+Quaternion.AngleAxis(phi,axis)*(rest[c[s+2]]-rest[c[s+1]]);
                }
            }
            // The index pad: off the tip on the palm side of its last bone (that bone turned on a right angle more about
            // the finger's own bend axis; the curled bone itself may point nearly at the palm).
            var pad=j[10]+Quaternion.AngleAxis(90,indexAxis)*(j[10]-j[9]).normalized*PadGap;
            var over=(j[13]+j[14])*.5f+palm*.02f;
            var target=rest[5]*(1-pinch-point)+pad*pinch+over*point;
            Thumb(rest,j,target);
            return j;
        }
        // The side the palm faces: the thumb rests on it.
        public static Vector3 PalmSide(IReadOnlyList<Vector3> rest)
        {
            var up=MemoryHandSkin.HandFrame(rest)*Vector3.up;
            return Vector3.Dot(up,rest[5]-rest[MemoryHandSkin.Wrist])<0?-up:up;
        }
        // The axis a joint turns about to bend a bone along `along` toward `toward` (a positive angle bends that way).
        static Vector3 BendAxis(Vector3 along,Vector3 toward)
        {
            var a=Vector3.Cross(along,toward).normalized;
            return Vector3.Dot(Quaternion.AngleAxis(10,a)*along-along,toward)<0?-a:a;
        }
        static void Thumb(IReadOnlyList<Vector3> rest,Vector3[] j,Vector3 target)
        {
            var m=rest[2];var axis=BendAxis(rest[5]-m,rest[10]-m);
            float reach=(target-m).magnitude;var c=new Vector3[4];
            Chain(rest,axis,0,c);
            if((c[3]-m).magnitude>reach)
            {
                float lo=0,hi=115;
                for(int i=0;i<20;i++){float mid=(lo+hi)*.5f;Chain(rest,axis,mid,c);if((c[3]-m).magnitude>reach)lo=mid;else hi=mid;}
                Chain(rest,axis,hi,c);
            }
            var turn=Quaternion.FromToRotation(c[3]-m,target-m);
            for(int k=1;k<4;k++)j[2+k]=m+turn*(c[k]-m);
        }
        static void Chain(IReadOnlyList<Vector3> rest,Vector3 axis,float beta,Vector3[] c)
        {
            c[0]=rest[2];c[1]=c[0]+(rest[3]-rest[2]);
            c[2]=c[1]+Quaternion.AngleAxis(beta,axis)*(rest[4]-rest[3]);
            c[3]=c[2]+Quaternion.AngleAxis(beta*2.15f,axis)*(rest[5]-rest[4]);
        }
        // Joints in the world: a pose's joints carried by a wrist pose.
        public static Vector3[] Place(IReadOnlyList<Vector3> joints,Vector3 wrist,Quaternion turn)
        {
            var w=new Vector3[joints.Count];var origin=joints[MemoryHandSkin.Wrist];
            for(int i=0;i<w.Length;i++)w[i]=wrist+turn*(joints[i]-origin);
            return w;
        }
        // The turn that lays a mesh-space hand palm down with its fingers along +z (mesh space: fingers +y, palm -z for
        // the right hand and the left, so the thumb lands inward for either).
        public static readonly Quaternion PalmDown=Quaternion.LookRotation(Vector3.up,Vector3.forward);
    }
}
