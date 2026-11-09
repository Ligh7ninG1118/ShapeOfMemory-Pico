using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.Integration
{
    // Somewhere a hand ray may point: a box in this object's space. While a hand's ray falls into it, or the hand reaches
    // inside it, ZoneRays keeps that ray drawn, ending where it meets the box; a button inside then takes over as usual.
    // Put one on a console, a row of cards, a panel: anything people aim at between its buttons. Enable and disable it
    // with the thing it covers.
    public sealed class AimZone : MonoBehaviour
    {
        public Bounds Box=new Bounds(Vector3.zero,Vector3.one*.2f);
        static readonly List<AimZone> zones=new List<AimZone>();
        public static IReadOnlyList<AimZone> Active=>zones;
        void OnEnable()=>zones.Add(this);
        void OnDisable()=>zones.Remove(this);

        // How far along the ray it meets the box (world units). From inside the box (a hand reaching in): how far until
        // it leaves, so the drawn ray still ends on the box.
        public bool Cast(Ray ray,out float distance)
        {
            distance=0;
            // Local space keeps world units along the ray: InverseTransformVector scales the direction with the box.
            var o=transform.InverseTransformPoint(ray.origin);var d=transform.InverseTransformVector(ray.direction);
            Vector3 min=Box.min,max=Box.max;float near=float.NegativeInfinity,far=float.PositiveInfinity;
            for(int axis=0;axis<3;axis++)
            {
                if(Mathf.Abs(d[axis])<1e-7f){if(o[axis]<min[axis]||o[axis]>max[axis])return false;continue;}
                float a=(min[axis]-o[axis])/d[axis],b=(max[axis]-o[axis])/d[axis];
                near=Mathf.Max(near,Mathf.Min(a,b));far=Mathf.Min(far,Mathf.Max(a,b));
            }
            if(far<Mathf.Max(near,0))return false;
            distance=near>0?near:far;return true;
        }
        // The nearest enabled zone along the ray, within reach.
        public static bool CastAll(Ray ray,float reach,out float distance)
        {
            distance=float.PositiveInfinity;
            foreach(var zone in zones)if(zone.Cast(ray,out float d)&&d<distance&&d<=reach)distance=d;
            return distance<float.PositiveInfinity;
        }
    }
}
