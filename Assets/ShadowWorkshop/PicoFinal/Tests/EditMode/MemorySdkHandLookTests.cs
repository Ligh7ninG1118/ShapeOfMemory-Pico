using NUnit.Framework;
using Oculus.Interaction;
using UnityEditor;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    // The Quest's SDK hands in a hand look (MemorySdkHandLook): Meta's mesh as it comes, with the look's per-hand shader values.
    public class MemorySdkHandLookTests
    {
        const string Meshes="Packages/com.meta.xr.sdk.interaction/Runtime/Meshes/";
        const string UnityRight="Assets/ShadowWorkshop/PicoFinal/Art/Hands/UnityXRHands/RightHand.fbx";
        static readonly int HandToLocalId=Shader.PropertyToID("_HandToLocal"),FadeId=Shader.PropertyToID("_WristFade");
        GameObject root;
        [TearDown] public void Clean(){if(root)Object.DestroyImmediate(root);}

        static int Bone(SkinnedMeshRenderer skin,params string[] ends)
        {
            for(int i=0;i<skin.bones.Length;i++){var n=skin.bones[i].name.ToLowerInvariant();foreach(var e in ends)if(n.EndsWith(e))return i;}
            return -1;
        }
        // The wrist fade must be over before the front of Meta's slanted stub cap, or the cap's edge shows as a line
        // across the back of the hand.
        [TestCase("OpenXRRightHand.fbx"),TestCase("OpenXRLeftHand.fbx"),TestCase("OVRHand_R.fbx"),TestCase("OVRHand_L.fbx")]
        public void TheWristFadeEndsBeforeMetasStubCap(string file)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Meshes+file);Assert.NotNull(model,file);
            var skin=model.GetComponentInChildren<SkinnedMeshRenderer>(true);var mesh=skin.sharedMesh;
            Assume.That(mesh.isReadable,file+" isn't readable");
            int wrist=Bone(skin,"wrist"),middle=Bone(skin,"middleproximal","middle1");
            Assert.That(wrist>=0&&middle>=0,file+": no wrist or middle knuckle bone");
            var w=mesh.bindposes[wrist].inverse.GetPosition();var forward=(mesh.bindposes[middle].inverse.GetPosition()-w).normalized;
            var vertices=mesh.vertices;var normals=mesh.normals;
            float cut=float.MaxValue,capFront=float.MinValue;
            for(int i=0;i<vertices.Length;i++)
            {
                float s=Vector3.Dot(vertices[i]-w,forward);cut=Mathf.Min(cut,s);
                if(s<0&&Vector3.Dot(normals[i],forward)<-.6f)capFront=Mathf.Max(capFront,s);
            }
            Assert.AreEqual(MemorySdkHandLook.StubCut,cut,.0005f,file+": the stub moved; re-measure WristFade");
            Assert.AreEqual(MemorySdkHandLook.CapFront,capFront,.0005f,file+": the stub's cap moved; re-measure WristFade");
            Assert.Greater(MemorySdkHandLook.WristFade.x,capFront,"dark by the cap's front");
            Assert.Greater(MemorySdkHandLook.WristFade.y,MemorySdkHandLook.WristFade.x);
        }
        // The aqua pads are placed by the mesh's fingertip, measured from the distal joint (the tip joint is mid-phalanx).
        [TestCase("OpenXRRightHand.fbx"),TestCase("OpenXRLeftHand.fbx")]
        public void TheReachesEndAtMetasFingertips(string file)
        {
            var skin=AssetDatabase.LoadAssetAtPath<GameObject>(Meshes+file).GetComponentInChildren<SkinnedMeshRenderer>(true);
            var mesh=skin.sharedMesh;Assume.That(mesh.isReadable,file+" isn't readable");
            var vertices=mesh.vertices;var weights=mesh.boneWeights;
            foreach(var (finger,reach) in new[]{("index",MemorySdkHandLook.IndexReach),("thumb",MemorySdkHandLook.ThumbReach)})
            {
                int distal=Bone(skin,finger+"distal"),tip=Bone(skin,finger+"tip");
                Assert.That(distal>=0&&tip>=0,file+": no "+finger+" distal/tip bone");
                var d=mesh.bindposes[distal].inverse.GetPosition();var along=(mesh.bindposes[tip].inverse.GetPosition()-d).normalized;
                float end=float.MinValue;
                for(int i=0;i<vertices.Length;i++)
                {
                    int main=weights[i].boneIndex0;
                    if(main==distal||main==tip)end=Mathf.Max(end,Vector3.Dot(vertices[i]-d,along));
                }
                Assert.AreEqual(reach,end,.0005f,file+": "+finger+" fingertip moved");
            }
        }

        // ---- The ghost hand on Meta's hand: the drawn hand stops on a table top, a ghost copy stays at the tracked pose.
        const string Lantern="Assets/ShadowWorkshop/PicoFinal/Looks/Hands/Lantern.asset";
        Transform model;
        MemorySdkHandLook GhostRig(float top)
        {
            var look=AssetDatabase.LoadAssetAtPath<MemoryHandLook>(Lantern);Assert.NotNull(look,Lantern);
            root=new GameObject("SDK ghost test"){hideFlags=HideFlags.DontSave};
            var visual=new GameObject("Visual").transform;visual.SetParent(root.transform,false);
            model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Meshes+"OpenXRRightHand.fbx"),visual).transform;
            model.localScale*=.01f;   // the FBX comes in at 100x; the SDK rescales its root to the hand
            var skin=model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            skin.gameObject.AddComponent<MaterialPropertyBlockEditor>();   // as on the SDK's hand prefab
            var it=MemorySdkHandLook.AddFor(root,look,skin,model,false);
            // A table top: everything below top, 2 m across.
            it.Ghost=new MemoryGhostHand(list=>{list.Add(new MemoryGhostHand.Solid{Id=1,Centre=new Vector3(0,top-.2f,0),Turn=Quaternion.identity,Half=new Vector3(1,.2f,1)});return list.Count;});
            it.GhostMaterial=new Material(Shader.Find("ShapeOfMemory/HandGhost"));
            return it;
        }
        // What the SDK does each update: the root at the tracked root, then the joints read off the hand.
        Vector3[] TrackAt(Vector3 at)
        {
            model.position=at;
            var all=model.GetComponentsInChildren<Transform>(true);var joints=new Vector3[MemoryHandSkin.Count];
            for(int i=0;i<joints.Length;i++)
            {
                var want="XRHand_"+MemoryHandSkin.Names[i];bool found=false;
                foreach(var t in all)if(t.name==want){joints[i]=t.position;found=true;break;}
                Assert.IsTrue(found,want);
            }
            return joints;
        }
        static float Lowest(Vector3[] joints){float y=float.MaxValue;foreach(var j in joints)y=Mathf.Min(y,j.y);return y;}

        [Test] public void TheDrawnHandStopsOnTheTableAndItsGhostStaysTracked()
        {
            var above=new Vector3(0,1,0);
            // Lay the table 6 cm under the hand's lowest joint.
            root=new GameObject("probe"){hideFlags=HideFlags.DontSave};
            model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Meshes+"OpenXRRightHand.fbx"),root.transform).transform;model.localScale*=.01f;
            float top=Lowest(TrackAt(above))-.06f;Object.DestroyImmediate(root);
            var it=GhostRig(top);
            it.Prewarm();
            Assert.NotNull(it.GhostRoot(0),"the ghost copy is made up front");
            Assert.IsFalse(it.GhostShown(0),"and hidden until a hand is in something");
            Assert.IsEmpty(it.GhostRoot(0).GetComponentsInChildren<MaterialPropertyBlockEditor>(true),"the SDK block editors aren't copied");

            it.Step(0,TrackAt(above));
            Assert.Less(Vector3.Distance(above,model.position),1e-5f,"clear of the table: drawn where tracked");
            Assert.IsFalse(it.Ghosted(0));Assert.IsFalse(it.GhostShown(0));

            // Down through the top: 4 cm of the hand in the table.
            var into=above+Vector3.down*.10f;
            it.Step(0,TrackAt(into));
            float lift=model.position.y-into.y;
            Assert.Greater(lift,.04f,"the drawn hand is held on the top");
            Assert.AreEqual(0,Vector3.ProjectOnPlane(model.position-into,Vector3.up).magnitude,1e-5f,"held straight up, out of the face it came in by");
            Assert.IsTrue(it.Ghosted(0),"deep: only the ghost shows");
            Assert.IsTrue(it.GhostShown(0));
            Assert.Less(Vector3.Distance(it.GhostRoot(0).position,into),1e-5f,"the ghost is at the tracked pose");

            // The SDK puts the root back at the tracked pose every update, so the offset doesn't add up.
            it.Step(0,TrackAt(into));
            Assert.AreEqual(into.y+lift,model.position.y,1e-5f);

            // Out of view: no ghost, and a fresh start when it comes back.
            it.Step(0,null);
            Assert.IsFalse(it.Ghosted(0));Assert.IsFalse(it.GhostShown(0));
            it.Step(0,TrackAt(above));
            Assert.Less(Vector3.Distance(above,model.position),1e-5f);Assert.IsFalse(it.GhostShown(0));
        }

        // The SDK's MaterialPropertyBlockEditor sets its own block on the hand every frame. The look's values go into
        // that block, so they survive it; a block of our own would be wiped.
        [Test] public void TheLooksValuesSurviveTheSdksBlockEditor()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(UnityRight);Assert.NotNull(model,UnityRight);
            var source=model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var names=System.Array.ConvertAll(source.bones,b=>b.name);
            var joints=new MemoryHandSkin.Rig(source.sharedMesh,names).Rest;

            root=new GameObject("SDK hand test"){hideFlags=HideFlags.DontSave};
            var skin=root.AddComponent<SkinnedMeshRenderer>();
            var editor=root.AddComponent<MaterialPropertyBlockEditor>();
            editor.Renderers=new System.Collections.Generic.List<Renderer>{skin};
            var read=new MaterialPropertyBlock();

            // Our own block: gone once the editor updates.
            MemorySdkHandLook.Apply(skin,new MaterialPropertyBlock(),joints,false);
            editor.UpdateMaterialPropertyBlock();
            skin.GetPropertyBlock(read);
            Assert.IsFalse(read.HasMatrix(HandToLocalId),"control: the editor replaces a separate block");

            // The editor's block: kept.
            MemorySdkHandLook.Apply(skin,editor.MaterialPropertyBlock,joints,false);
            editor.UpdateMaterialPropertyBlock();
            skin.GetPropertyBlock(read);
            Assert.AreEqual(MemoryHandSkin.HandToLocal(joints,false),read.GetMatrix(HandToLocalId));
            Assert.AreEqual(MemorySdkHandLook.WristFade.x,read.GetVector(FadeId).x,1e-6f);
        }
    }
}
