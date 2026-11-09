using CloudGarden.Integration;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    public class AimZoneTests
    {
        // A zone on a turned, scaled object: aiming in lands on its face (world distance), a hand inside reaches its far
        // side, and aiming past it misses.
        [Test] public void CastMeetsTheBoxInWorldUnits()
        {
            var go=new GameObject("Zone");
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(1,1,0),Quaternion.Euler(0,90,0));go.transform.localScale=new Vector3(2,1,1);
                var zone=go.AddComponent<AimZone>();zone.Box=new Bounds(new Vector3(0,.05f,0),new Vector3(.5f,.1f,.5f));
                // Top face at y = 1.1; from 0.5 m above, straight down.
                Assert.IsTrue(zone.Cast(new Ray(new Vector3(1,1.6f,0),Vector3.down),out float down));Assert.AreEqual(.5f,down,1e-4f);
                // Local x is scaled 2 and turned onto world -z: the box spans z -0.5..0.5 there.
                Assert.IsTrue(zone.Cast(new Ray(new Vector3(1,1.05f,-2),Vector3.forward),out float along));Assert.AreEqual(1.5f,along,1e-4f);
                Assert.IsTrue(zone.Cast(new Ray(new Vector3(1,1.05f,0),Vector3.up),out float inside));Assert.AreEqual(.05f,inside,1e-4f,"from inside: to where it leaves");
                Assert.IsFalse(zone.Cast(new Ray(new Vector3(1,1.6f,0),Vector3.up),out _),"pointing away");
                Assert.IsFalse(zone.Cast(new Ray(new Vector3(2,1.6f,0),Vector3.down),out _),"beside it");
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
