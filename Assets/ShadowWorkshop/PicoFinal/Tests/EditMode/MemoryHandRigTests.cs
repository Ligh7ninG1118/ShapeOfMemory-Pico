using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    // A rigged hand model used as it comes (2026-11-15: the Unity XR Hands sample hand, Art/Hands/UnityXRHands), driven by
    // MemoryHandSkin from joint positions: its bones found by name, each keeping its own axes, its units found from its size.
    public class MemoryHandRigTests
    {
        const string RightModel="Assets/ShadowWorkshop/PicoFinal/Art/Hands/UnityXRHands/RightHand.fbx",LeftModel="Assets/ShadowWorkshop/PicoFinal/Art/Hands/UnityXRHands/LeftHand.fbx";
        GameObject root;
        [TearDown] public void Clean(){if(root)Object.DestroyImmediate(root);}

        MemoryHandSkin.SkinnedHand Hand(bool left,out MemoryHandLook look)
        {
            look=ScriptableObject.CreateInstance<MemoryHandLook>();
            look.RightModel=AssetDatabase.LoadAssetAtPath<GameObject>(RightModel);look.LeftModel=AssetDatabase.LoadAssetAtPath<GameObject>(LeftModel);
            Assert.NotNull(look.RightModel,RightModel);Assert.NotNull(look.LeftModel,LeftModel);
            look.Materials=new[]{new Material(Shader.Find("ShapeOfMemory/HandDepth"))};
            root=new GameObject("Hand test"){hideFlags=HideFlags.DontSave};
            return new MemoryHandSkin.SkinnedHand(root.transform,"Hand",look.MeshFor(left),look,look.BonesFor(left),left);
        }
        static Vector3[] Bake(MemoryHandSkin.SkinnedHand hand)
        {
            var baked=new Mesh();hand.Skin.BakeMesh(baked,true);
            var t=hand.Skin.transform;var v=baked.vertices.Select(p=>t.TransformPoint(p)).ToArray();
            Object.DestroyImmediate(baked);return v;
        }

        [Test] public void BoneNamesFindTheirJoints()
        {
            Assert.AreEqual(MemoryHandSkin.IndexProximal,MemoryHandSkin.Rig.JointOf("R_IndexProximal"));
            Assert.AreEqual(MemoryHandSkin.Wrist,MemoryHandSkin.Rig.JointOf("L_Wrist"));
            Assert.AreEqual(MemoryHandSkin.Palm,MemoryHandSkin.Rig.JointOf("Palm"));
            Assert.AreEqual(-1,MemoryHandSkin.Rig.JointOf("Armature"));
        }

        // Every joint has a bone; the model's units come out as metres (a hand about 8.5 cm wrist to middle knuckle).
        [Test] public void TheModelsAreRiggedToAllTwentySixJoints()
        {
            foreach(bool left in new[]{false,true})
            {
                var hand=Hand(left,out var look);var rest=hand.Rig.Rest;
                Assert.IsTrue(hand.Rig.External);Assert.AreEqual(look.MeshFor(left).bindposes.Length,hand.Bones.Length);
                float span=(rest[MemoryHandSkin.MiddleProximal]-rest[MemoryHandSkin.Wrist]).magnitude;
                Assert.That(span,Is.InRange(.06f,.12f),"wrist to middle knuckle, metres");
                // The thumb is on the hand's own side: +x for the right hand (fingers +y, back +z in the hand's frame).
                var local=MemoryHandSkin.HandToLocal(rest,left).MultiplyPoint3x4(rest[MemoryHandSkin.ThumbTip]);
                Assert.Greater(local.x,.02f,(left?"left":"right")+" thumb toward +x in the hand's frame");
                Assert.Greater(MemoryHandSkin.HandToLocal(rest,left).MultiplyPoint3x4(rest[15]).y,.1f,"fingers toward +y");
                // Its rest in our mesh's layout (what made-up poses start from): wrist at 0, fingers +y, thumb +x right / -x left.
                var c=hand.Rig.Canonical;
                Assert.Less(c[MemoryHandSkin.Wrist].magnitude,1e-5f);Assert.Greater(c[15].y,.1f);
                Assert.Greater(left?-c[MemoryHandSkin.ThumbTip].x:c[MemoryHandSkin.ThumbTip].x,.02f,"thumb side");
                Object.DestroyImmediate(root);
            }
        }

        // At its rest joints the model's skin is its own mesh, as authored (in metres).
        [Test] public void AtRestTheSkinIsTheModel()
        {
            var hand=Hand(false,out var look);var mesh=look.MeshFor(false);
            hand.Pose(hand.Rig.Rest);
            var baked=Bake(hand);var v=mesh.vertices;float unit=hand.Rig.Unit;
            float worst=0;for(int i=0;i<v.Length;i++)worst=Mathf.Max(worst,Vector3.Distance(v[i]*unit,baked[i]));
            Assert.Less(worst,5e-4f,"worst vertex off by "+worst+" m");
        }

        // A tracked hand somewhere else, turned and 10% bigger: every bone sits on its joint and the fingertips follow.
        [Test] public void ItFollowsATrackedHand()
        {
            var hand=Hand(false,out var look);var rest=hand.Rig.Rest;
            var turn=Quaternion.Euler(-30,40,15);var at=new Vector3(-.2f,1.05f,.35f);
            var joints=rest.Select(p=>at+turn*(p*1.1f)).ToArray();
            hand.Pose(joints);
            var names=look.BonesFor(false);
            for(int k=0;k<hand.Bones.Length;k++)Assert.Less(Vector3.Distance(hand.Bones[k].position,joints[MemoryHandSkin.Rig.JointOf(names[k])]),1e-5f,names[k]);
            var baked=Bake(hand);var v=look.MeshFor(false).vertices;float unit=hand.Rig.Unit;
            foreach(int tip in new[]{MemoryHandSkin.ThumbTip,MemoryHandSkin.IndexTip,15,20,25})
            {
                int nearest=Enumerable.Range(0,v.Length).OrderBy(i=>(v[i]*unit-rest[tip]).sqrMagnitude).First();
                var expected=at+turn*((v[nearest]*unit)*1.1f);
                Assert.Less(Vector3.Distance(baked[nearest],expected),.003f,MemoryHandSkin.Names[tip]);
            }
        }
    }
}
