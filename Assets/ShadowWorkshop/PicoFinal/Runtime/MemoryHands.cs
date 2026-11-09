using System.Collections.Generic;
using CloudGarden.Integration;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // The player's hands as the runtime tracks them: a slim bone between every pair of joints, a small ball on each
    // knuckle and glowing (aqua, dark-rimmed) index and thumb tips that brighten as a pinch closes. Drawn straight from the joint positions,
    // nothing retargeted, so it sits wherever the tracking does. Used on the Pico rig instead of the SDK's skinned hand,
    // whose mesh is fitted to Meta's skeleton and sat off the real hands there; passthrough shows the real hand around
    // the slim bones.
    public sealed class MemoryHands : MonoBehaviour
    {
        public const float Bone=.008f,Knuckle=.012f,Tip=.013f;
        static readonly HandJointId[][] Chains=
        {
            new[]{HandJointId.HandWristRoot,HandJointId.HandThumb1,HandJointId.HandThumb2,HandJointId.HandThumb3,HandJointId.HandThumbTip},
            new[]{HandJointId.HandWristRoot,HandJointId.HandIndex0,HandJointId.HandIndex1,HandJointId.HandIndex2,HandJointId.HandIndex3,HandJointId.HandIndexTip},
            new[]{HandJointId.HandWristRoot,HandJointId.HandMiddle0,HandJointId.HandMiddle1,HandJointId.HandMiddle2,HandJointId.HandMiddle3,HandJointId.HandMiddleTip},
            new[]{HandJointId.HandWristRoot,HandJointId.HandRing0,HandJointId.HandRing1,HandJointId.HandRing2,HandJointId.HandRing3,HandJointId.HandRingTip},
            new[]{HandJointId.HandWristRoot,HandJointId.HandPinky0,HandJointId.HandPinky1,HandJointId.HandPinky2,HandJointId.HandPinky3,HandJointId.HandPinkyTip},
            // Across the knuckles, so the bones read as a palm.
            new[]{HandJointId.HandIndex1,HandJointId.HandMiddle1,HandJointId.HandRing1,HandJointId.HandPinky1},
        };
        MetaShadowPlayer player;
        readonly HandView[] views=new HandView[2];
        public bool Visible(bool left)=>views[left?0:1].root.gameObject.activeSelf;

        sealed class HandView
        {
            public Transform root;
            public readonly List<(HandJointId from,HandJointId to,Transform bone)> bones=new List<(HandJointId,HandJointId,Transform)>();
            public readonly Dictionary<HandJointId,Transform> joints=new Dictionary<HandJointId,Transform>();
            public Renderer index,thumb;
        }

        public static MemoryHands Add(GameObject host,MetaShadowPlayer player,Material bone,Material glow,List<Object> owned)
        {
            var hands=host.AddComponent<MemoryHands>();hands.player=player;
            var boneMaterial=new Material(bone){name="Memory hand bone"};boneMaterial.SetColor("_BaseColor",MemoryPalette.Moonlight);owned.Add(boneMaterial);
            var tipMaterial=new Material(glow){name="Memory hand tip"};tipMaterial.SetColor("_BaseColor",MemoryPalette.AquaIdle);owned.Add(tipMaterial);
            var rim=MemoryUi.Rim(glow,owned);
            for(int h=0;h<2;h++)hands.views[h]=Build(host.transform,h==0?"Left hand":"Right hand",boneMaterial,tipMaterial,rim);
            return hands;
        }
        static HandView Build(Transform parent,string name,Material bone,Material tip,Material rim)
        {
            var view=new HandView{root=new GameObject(name).transform};view.root.SetParent(parent,false);
            foreach(var chain in Chains)for(int i=0;i<chain.Length;i++)
            {
                var id=chain[i];
                if(!view.joints.ContainsKey(id)&&id!=HandJointId.HandWristRoot)
                {
                    bool fingertip=id==HandJointId.HandIndexTip||id==HandJointId.HandThumbTip;
                    var ball=Part(PrimitiveType.Sphere,id.ToString(),view.root,fingertip?tip:bone);ball.localScale=Vector3.one*(fingertip?Tip:Knuckle);view.joints[id]=ball;
                    // A glowing tip's dark rim (an inverted hull RimWidth larger all round).
                    if(fingertip)Part(PrimitiveType.Sphere,"Rim",ball,rim).localScale=Vector3.one*(1+2*MemoryPalette.RimWidth/Tip);
                    if(id==HandJointId.HandIndexTip)view.index=ball.GetComponent<Renderer>();
                    if(id==HandJointId.HandThumbTip)view.thumb=ball.GetComponent<Renderer>();
                }
                if(i>0)view.bones.Add((chain[i-1],id,Part(PrimitiveType.Capsule,"Bone "+id,view.root,bone)));
            }
            view.root.gameObject.SetActive(false);return view;
        }
        static Transform Part(PrimitiveType type,string name,Transform parent,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;MemoryUi.Kill(go.GetComponent<Collider>());go.transform.SetParent(parent,false);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return go.transform;
        }
        readonly Dictionary<HandJointId,Vector3> at=new Dictionary<HandJointId,Vector3>();
        static MaterialPropertyBlock block;
        // After the hand data has updated for this frame.
        void LateUpdate()
        {
            for(int h=0;h<2;h++)
            {
                var view=views[h];var hand=player?player.TrackedHand(h==0):null;
                bool shown=hand&&Read(hand);view.root.gameObject.SetActive(shown);if(!shown)continue;
                foreach(var pair in view.joints)pair.Value.position=at[pair.Key];
                foreach(var (from,to,bone) in view.bones)
                {
                    var a=at[from];var b=at[to];var axis=b-a;float length=axis.magnitude;
                    bone.position=(a+b)*.5f;bone.rotation=length>1e-5f?Quaternion.FromToRotation(Vector3.up,axis):Quaternion.identity;
                    bone.localScale=new Vector3(Bone,Mathf.Max(length*.5f,Bone*.5f),Bone);
                }
                // The pinching tips brighten from aqua Idle to Grab as they close (2.8 cm is where a pinch registers).
                float closing=Mathf.InverseLerp(.06f,.028f,Vector3.Distance(at[HandJointId.HandIndexTip],at[HandJointId.HandThumbTip]));
                block??=new MaterialPropertyBlock();block.SetColor("_BaseColor",Color.Lerp(MemoryPalette.AquaIdle,MemoryPalette.AquaGrab,closing));
                view.index.SetPropertyBlock(block);view.thumb.SetPropertyBlock(block);
            }
        }
        static readonly HandJointId[] Joints=Distinct();
        static HandJointId[] Distinct(){var ids=new List<HandJointId>();foreach(var chain in Chains)foreach(var id in chain)if(!ids.Contains(id))ids.Add(id);return ids.ToArray();}
        bool Read(Hand hand)
        {
            foreach(var id in Joints){if(!hand.GetJointPose(id,out var pose))return false;at[id]=pose.position;}
            return true;
        }
    }
}
