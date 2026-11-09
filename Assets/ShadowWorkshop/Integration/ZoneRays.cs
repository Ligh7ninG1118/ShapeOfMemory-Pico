using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloudGarden.Integration
{
    // Draws the hand rays instead of the SDK's ray and cursor visuals, which appear only while the ray is on a button and
    // vanish between buttons. Here a ray shows while it is on a button (to the SDK's hit, in hover or select colour) or
    // inside an AimZone (to the zone, quieter), and fades in and out rather than popping. Selection is still the SDK's.
    // The cursor shrinks as the pinch closes. Hidden is a host's veto per hand (Shape of Memory: a hand at the orb).
    public sealed class ZoneRays : MonoBehaviour
    {
        public const float Reach=3,Width=.0035f,Cursor=.016f,Fade=.12f;
        static readonly Color Idle=new Color(.80f,.88f,.86f),Hover=new Color(.55f,.95f,.82f),Pressed=Color.white;
        public Func<Handedness,bool> Hidden;
        MetaShadowPlayer player;
        readonly List<View> views=new List<View>();
        Material material;
        bool bound;

        sealed class View
        {
            public RayInteractor ray;
            public IHand hand;
            public Transform beam,cursor;
            public Renderer beamRenderer,cursorRenderer;
            // Fade 0..1, and the last length and colour, kept while it fades out.
            public float shown,length;
            public Color colour;
        }
        public static ZoneRays Add(GameObject host,MetaShadowPlayer player,Material glow,List<UnityEngine.Object> owned)
        {
            var rays=host.AddComponent<ZoneRays>();rays.player=player;
            rays.material=new Material(glow){name="Zone ray"};rays.material.SetColor("_BaseColor",Color.white);owned.Add(rays.material);
            return rays;
        }
        // The rig's hand rays appear once their hands have tracked; bind as they do.
        void Bind()
        {
            foreach(var (ray,hand) in player.HandRays())
            {
                if(views.Exists(v=>v.ray==ray))continue;
                // The SDK's own ray and cursor visuals step aside.
                foreach(var visual in ray.GetComponentsInChildren<RayInteractorRayVisual>(true)){visual.enabled=false;foreach(var r in visual.GetComponentsInChildren<Renderer>(true))r.enabled=false;}
                foreach(var visual in ray.GetComponentsInChildren<RayInteractorCursorVisual>(true)){visual.enabled=false;foreach(var r in visual.GetComponentsInChildren<Renderer>(true))r.enabled=false;}
                var view=new View{ray=ray,hand=hand};
                view.beam=Part(PrimitiveType.Cylinder,"Ray "+hand.Handedness,out view.beamRenderer);
                view.cursor=Part(PrimitiveType.Sphere,"Cursor "+hand.Handedness,out view.cursorRenderer);
                views.Add(view);
            }
            bound=views.Count>=2;
        }
        Transform Part(PrimitiveType type,string name,out Renderer renderer)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;Destroy(go.GetComponent<Collider>());go.transform.SetParent(transform,false);
            renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            go.SetActive(false);return go.transform;
        }
        static MaterialPropertyBlock block;
        // After the rays have processed this frame.
        void LateUpdate()
        {
            if(!player||!player.Active)return;
            if(!bound&&(views.Count==0||Time.frameCount%30==0))Bind();
            foreach(var view in views)
            {
                var ray=view.ray;
                bool live=ray&&ray.isActiveAndEnabled&&ray.State!=InteractorState.Disabled&&view.hand.IsTrackedDataValid&&!(Hidden?.Invoke(view.hand.Handedness)??false);
                if(live&&ray.Interactable!=null){view.length=Vector3.Distance(ray.Origin,ray.End);view.colour=ray.State==InteractorState.Select?Pressed:Hover;}
                else if(live&&AimZone.CastAll(new Ray(ray.Origin,ray.Forward),Reach,out float d)){view.length=d;view.colour=Idle;}
                else live=false;
                view.shown=Mathf.MoveTowards(view.shown,live?1:0,Time.unscaledDeltaTime/Fade);
                bool show=ray&&view.shown>0&&view.length>.02f;view.beam.gameObject.SetActive(show);view.cursor.gameObject.SetActive(show);
                if(!show)continue;
                float length=view.length;var colour=view.colour;
                var start=ray.Origin+ray.Forward*.02f;var end=ray.Origin+ray.Forward*length;var axis=end-start;
                view.beam.position=(start+end)*.5f;view.beam.rotation=Quaternion.FromToRotation(Vector3.up,axis);
                view.beam.localScale=new Vector3(Width*view.shown,axis.magnitude*.5f,Width*view.shown);
                // The cursor tightens as the pinch closes, as the SDK's does.
                float closing=view.hand.GetJointPose(HandJointId.HandIndexTip,out var tip)&&view.hand.GetJointPose(HandJointId.HandThumbTip,out var thumb)
                    ?Mathf.InverseLerp(.065f,.03f,Vector3.Distance(tip.position,thumb.position)):0;
                view.cursor.position=end-ray.Forward*.002f;view.cursor.localScale=Vector3.one*Cursor*view.shown*(1-.45f*closing);
                block??=new MaterialPropertyBlock();block.SetColor("_BaseColor",colour);view.beamRenderer.SetPropertyBlock(block);view.cursorRenderer.SetPropertyBlock(block);
            }
        }
    }
}
