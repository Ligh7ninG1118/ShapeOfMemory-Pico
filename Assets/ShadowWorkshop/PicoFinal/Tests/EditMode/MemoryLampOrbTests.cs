using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    public class MemoryLampOrbTests
    {
        static List<Vector4> Tip(Vector3 at)=>new List<Vector4>{new Vector4(at.x,at.y,at.z,MemoryLampOrb.FingerRadius)};

        // A fingertip pressed 2 cm into the sphere pushes it 2 cm straight away from the finger.
        [Test] public void AFingertipPushesTheSphereAwayAlongItsLine()
        {
            var offset=Vector3.zero;float touch=MemoryLampOrb.Radius+MemoryLampOrb.FingerRadius;
            Assert.IsTrue(MemoryLampOrb.Resolve(Vector3.zero,ref offset,Tip(new Vector3(0,0,-(touch-.02f)))));
            Assert.AreEqual(.02f,offset.z,1e-4f,"pushed along +z, away from the finger");
            Assert.AreEqual(0,offset.x,1e-5f);Assert.AreEqual(0,offset.y,1e-5f);
            Assert.IsFalse(MemoryLampOrb.Resolve(Vector3.zero,ref offset,Tip(new Vector3(0,0,-(touch+.001f)))),"a finger just outside it doesn't");
        }

        // However deep the hand goes, the sphere gives no more than MaxPush.
        [Test] public void ItGivesNoMoreThanMaxPush()
        {
            var offset=Vector3.zero;var velocity=Vector3.zero;
            // A fingertip pushing 5 mm a frame until it has gone 1 cm further than the lamp gives (still short of its middle).
            float touch=MemoryLampOrb.Radius+MemoryLampOrb.FingerRadius;
            for(int f=0;f<60;f++)MemoryLampOrb.Step(Vector3.zero,ref offset,ref velocity,Tip(new Vector3(.003f,0,-touch+Mathf.Min(f*.005f,MemoryLampOrb.MaxPush+.01f))),1/72f);
            Assert.LessOrEqual(offset.magnitude,MemoryLampOrb.MaxPush+1e-5f);
            Assert.Greater(offset.magnitude,MemoryLampOrb.MaxPush*.95f,"at the limit");
        }

        // Let go, it drifts back with a wobble (past the middle once) and settles within two seconds.
        [Test] public void LetGoItSpringsBackWithAWobble()
        {
            var offset=new Vector3(0,0,MemoryLampOrb.MaxPush);var velocity=Vector3.zero;bool overshot=false;
            for(float t=0;t<2f;t+=1/72f)
            {
                MemoryLampOrb.Step(Vector3.zero,ref offset,ref velocity,null,1/72f);
                if(offset.z<-.002f)overshot=true;
            }
            Assert.IsTrue(overshot,"it swings past where it rests");
            Assert.Less(offset.magnitude,.001f,"back within a millimetre after 2 s");
        }

        // Only the lamp's models move: its Light (and the head the shadow is cast from) stay put.
        [Test] public void APushMovesTheModelsNotTheLight()
        {
            var room=new GameObject("Room");
            try
            {
                var head=new GameObject("Lamp 0").transform;head.SetParent(room.transform,false);head.localPosition=new Vector3(.5f,.3f,-.4f);
                var light=new GameObject("Lamp beam").AddComponent<Light>();light.transform.SetParent(head,false);
                var model=new GameObject("SM_Spotlight_01").transform;model.SetParent(head,false);model.localPosition=new Vector3(.003f,0,.04f);
                var orb=MemoryLampOrb.Add(head,null,null);
                var at=head.position+head.forward*-(MemoryLampOrb.Radius+MemoryLampOrb.FingerRadius-.015f);
                orb.Probe=()=>Tip(at);
                orb.Tick(1/72f);
                Assert.IsTrue(orb.Touching);
                Assert.AreEqual(.015f,orb.Offset.magnitude,1e-4f);
                Assert.AreEqual(Vector3.zero,light.transform.localPosition,"the light stays");
                Assert.AreEqual(new Vector3(.5f,.3f,-.4f),head.localPosition,"the head stays");
                Assert.AreEqual(.015f,Vector3.Distance(model.localPosition,new Vector3(.003f,0,.04f)),1e-4f,"the model moved with it");
                Assert.AreNotEqual(head,orb.transform.parent,"the orb sits beside the head (the art add-on hides what isn't its own under a lamp)");
                orb.Probe=()=>new List<Vector4>();
                for(int f=0;f<120;f++)orb.Tick(1/72f);
                Assert.Less(Vector3.Distance(model.localPosition,new Vector3(.003f,0,.04f)),.001f,"and went back");
            }
            finally{Object.DestroyImmediate(room);}
        }

        // The rays: a material the build carries (Resources), on the cards through the beam's line.
        [Test] public void TheRaysMaterialShipsAndTheCardsFaceAcrossTheBeam()
        {
            var material=Resources.Load<Material>("ShapeOfMemory/Lamp rays");
            Assert.NotNull(material,"Resources/ShapeOfMemory/Lamp rays.mat");
            Assert.AreEqual("ShapeOfMemory/LampRays",material.shader.name);
            var mesh=MemoryLampOrb.RayMesh();
            Assert.AreEqual(MemoryLampOrb.RayCards*4,mesh.vertexCount);
            foreach(var n in mesh.normals)Assert.AreEqual(0,n.z,1e-5f,"each card holds the beam's line");
            Assert.AreEqual(MemoryLampOrb.RayLength,mesh.bounds.max.z,1e-5f);
        }
    }
}
