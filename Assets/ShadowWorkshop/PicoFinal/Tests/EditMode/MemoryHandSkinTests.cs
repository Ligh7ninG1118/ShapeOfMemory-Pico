using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    // The skinned hand (MemoryHandSkin) on our mesh (Art/Hands/MemoryHand_R/_L): rigged to the 26 joints, its bones placed
    // from joint positions alone, so the skin follows the tracked hand's size and pose and its fingertips land on the
    // tracked tips. Skinned positions are baked on the CPU (SkinnedMeshRenderer.BakeMesh), in world space.
    public class MemoryHandSkinTests
    {
        const string Right="Assets/ShadowWorkshop/PicoFinal/Art/Hands/MemoryHand_R.asset",Left="Assets/ShadowWorkshop/PicoFinal/Art/Hands/MemoryHand_L.asset";
        const string Ink="Assets/ShadowWorkshop/PicoFinal/Looks/Hands/Ink.asset",Lantern="Assets/ShadowWorkshop/PicoFinal/Looks/Hands/Lantern.asset";
        GameObject root;
        [TearDown] public void Clean(){if(root)Object.DestroyImmediate(root);}

        static Mesh Load(string path){var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);Assert.IsNotNull(m,path+" missing: Hands > Import hand mesh");return m;}
        MemoryHandSkin.SkinnedHand Hand(Mesh mesh)
        {
            var look=ScriptableObject.CreateInstance<MemoryHandLook>();
            look.Right=look.Left=mesh;look.Materials=new[]{new Material(Shader.Find("ShapeOfMemory/HandDepth"))};
            root=new GameObject("Hand test"){hideFlags=HideFlags.DontSave};
            return new MemoryHandSkin.SkinnedHand(root.transform,"Hand",mesh,look);
        }
        static Vector3[] Bake(MemoryHandSkin.SkinnedHand hand)
        {
            var baked=new Mesh();hand.Skin.BakeMesh(baked,true);
            var t=hand.Skin.transform;var v=baked.vertices.Select(p=>t.TransformPoint(p)).ToArray();
            Object.DestroyImmediate(baked);return v;
        }

        [Test] public void TheHandMeshesAreRiggedToTheJoints()
        {
            foreach(var mesh in new[]{Load(Right),Load(Left)})
            {
                Assert.AreEqual(MemoryHandSkin.Count,mesh.bindposes.Length,mesh.name);
                var weights=mesh.boneWeights;Assert.AreEqual(mesh.vertexCount,weights.Length,mesh.name);
                foreach(var w in weights)Assert.AreEqual(1f,w.weight0+w.weight1+w.weight2+w.weight3,.002f,mesh.name);
                // The palm and the tips own no skin.
                Assert.IsFalse(weights.Any(w=>w.weight0>.5f&&(w.boneIndex0==MemoryHandSkin.Palm||MemoryHandSkin.Child[w.boneIndex0]<0)),mesh.name);
                var masks=new System.Collections.Generic.List<Vector2>();mesh.GetUVs(3,masks);
                Assert.AreEqual(mesh.vertexCount,masks.Count,mesh.name+" UV3 (tip, fade)");
                Assert.IsTrue(masks.Any(m=>m.x>.9f)&&masks.Any(m=>m.y<.05f),mesh.name+": tips and a faded wrist");
                Assert.Greater(mesh.vertexCount,1500,mesh.name);
                // Front faces outward (2026-11-14: the right hand was flipped and showed its inside): a closed mesh wound
                // so has a positive signed volume, sum of a.(b x c)/6 over its triangles.
                var v=mesh.vertices;var t=mesh.triangles;double volume=0;
                for(int i=0;i<t.Length;i+=3)volume+=Vector3.Dot(v[t[i]],Vector3.Cross(v[t[i+1]],v[t[i+2]]))/6.0;
                Assert.Greater(volume,0,mesh.name+" winding");
                // A hand is a few hundred cm3.
                Assert.That(volume,Is.InRange(1.5e-4,8e-4),mesh.name+" volume");
            }
        }
        [Test] public void TheLeftHandMirrorsTheRight()
        {
            var r=new MemoryHandSkin.Rig(Load(Right));var l=new MemoryHandSkin.Rig(Load(Left));
            for(int i=0;i<MemoryHandSkin.Count;i++)
            {
                var m=r.Rest[i];m.x=-m.x;
                Assert.Less(Vector3.Distance(m,l.Rest[i]),1e-5f,MemoryHandSkin.Names[i]);
            }
            // The right thumb is on +x (fingers +y, palm -z): a right hand in Unity's left-handed space.
            Assert.Greater(r.Rest[MemoryHandSkin.ThumbTip].x,.03f);
        }
        [Test] public void AtRestTheSkinIsTheMesh()
        {
            var mesh=Load(Right);var hand=Hand(mesh);
            hand.Pose(hand.Rig.Rest);
            var baked=Bake(hand);var v=mesh.vertices;
            float worst=0;for(int i=0;i<v.Length;i++)worst=Mathf.Max(worst,Vector3.Distance(v[i],baked[i]));
            Assert.Less(worst,1e-4f,"worst vertex off by "+worst);
        }
        [Test] public void ABiggerHandStretchesTheSkinOntoItsJoints()
        {
            var mesh=Load(Right);var hand=Hand(mesh);var rest=hand.Rig.Rest;
            // 15% longer bones, the hand turned and moved: the fingertips must follow their joints.
            var turn=Quaternion.Euler(20,-35,60);var at=new Vector3(.3f,1.1f,.4f);
            var joints=rest.Select(p=>at+turn*(p*1.15f)).ToArray();
            hand.Pose(joints);
            var baked=Bake(hand);var v=mesh.vertices;
            foreach(int tip in new[]{MemoryHandSkin.ThumbTip,MemoryHandSkin.IndexTip,15,20,25})
            {
                // The vertex nearest the rest tip lands within 2 mm of where that point maps on the bigger hand.
                int nearest=Enumerable.Range(0,v.Length).OrderBy(i=>(v[i]-rest[tip]).sqrMagnitude).First();
                var expected=at+turn*(rest[tip]*1.15f+(v[nearest]-rest[tip]));
                Assert.Less(Vector3.Distance(baked[nearest],expected),.002f,MemoryHandSkin.Names[tip]);
            }
            for(int i=0;i<MemoryHandSkin.Count;i++)Assert.Less(Vector3.Distance(hand.Bones[i].position,joints[i]),1e-5f,MemoryHandSkin.Names[i]);
        }
        [Test] public void APinchBringsThePadsTogether()
        {
            var mesh=Load(Right);var hand=Hand(mesh);var rest=hand.Rig.Rest;
            var open=MemoryHandPose.Make(rest,0);
            for(int i=0;i<MemoryHandSkin.Count;i++)Assert.Less(Vector3.Distance(open[i],rest[i]),1e-5f,"open = rest at "+MemoryHandSkin.Names[i]);
            var pinch=MemoryHandPose.Make(rest,1);
            Assert.AreEqual(MemoryHandPose.PadGap,Vector3.Distance(pinch[MemoryHandSkin.IndexTip],pinch[MemoryHandSkin.ThumbTip]),.002f);
            // Bones keep their lengths.
            for(int i=0;i<MemoryHandSkin.Count;i++)if(MemoryHandSkin.Child[i]>=0&&i!=MemoryHandSkin.Palm&&i!=MemoryHandSkin.Wrist)
                Assert.AreEqual(MemoryHandSkin.Along(rest,i).magnitude,MemoryHandSkin.Along(pinch,i).magnitude,1e-4f,MemoryHandSkin.Names[i]);
            hand.Pose(pinch);
            // The aqua tip skin of the index and the thumb meet: their closest vertices within a fingertip's thickness
            // (6.9 mm on 2026-11-14; in the game the gap follows the tracked tips, this pose is made up).
            var masks=new System.Collections.Generic.List<Vector2>();mesh.GetUVs(3,masks);
            var baked=Bake(hand);
            var index=Enumerable.Range(0,baked.Length).Where(i=>masks[i].x>.5f&&Vector3.Distance(rest[MemoryHandSkin.IndexTip],mesh.vertices[i])<.02f).ToArray();
            var thumb=Enumerable.Range(0,baked.Length).Where(i=>masks[i].x>.5f&&Vector3.Distance(rest[MemoryHandSkin.ThumbTip],mesh.vertices[i])<.02f).ToArray();
            Assert.IsNotEmpty(index);Assert.IsNotEmpty(thumb);
            float closest=index.Min(a=>thumb.Min(b=>Vector3.Distance(baked[a],baked[b])));
            Assert.Less(closest,.009f,"pads "+closest*1000+" mm apart");
        }
        [Test] public void TheLooksAreComplete()
        {
            foreach(var path in new[]{Ink,Lantern})
            {
                var look=AssetDatabase.LoadAssetAtPath<MemoryHandLook>(path);
                Assert.IsNotNull(look,path+" missing: Hands > Make looks");Assert.IsTrue(look.Valid,path);
                Assert.AreEqual("ShapeOfMemory/HandDepth",look.Materials[0].shader.name,path+": depth first");
                foreach(var m in look.Materials)Assert.IsFalse(ShaderUtil.ShaderHasError(m.shader),m.shader.name);
                for(int i=1;i<look.Materials.Length;i++)Assert.Greater(look.Materials[i].renderQueue,look.Materials[i-1].renderQueue,path+" draw order");
                // After the sky (the end of the opaque range), or the sky would be cut out behind the hand.
                Assert.Greater(look.Materials[0].renderQueue,MemorySky.DarkQueue,path);
            }
            var lantern=AssetDatabase.LoadAssetAtPath<MemoryHandLook>(Lantern);
            Assert.IsTrue(lantern.Motes&&lantern.MoteMaterial,"lantern motes");
        }
    }
}
