using CloudGarden.ShadowPuzzle;
using UnityEngine;

namespace ShapeOfMemory
{
    // Where each hand has hold of the orb (playtest 2026-11-11: "where am I pinching?"). A dot on the orb's shell, on the
    // line from its middle through the pinch: the point that stays under the fingers as the orb rolls. A light stroke
    // like the rings (see-through aqua, Ink800 rim): small and faint where a pinch would take hold, full while holding.
    // A pinch well inside or outside the shell (the reach runs Torch2Input.ReachMargin past the rings) gets a thin
    // tether from the fingers to its dot. A hold from afar (look and pinch) shows only the dot.
    public sealed class MemoryGrip : MonoBehaviour
    {
        public const float Dot=.018f,HoverDot=.010f,Tether=.0025f,TetherFrom=.025f,Ease=18;
        Torch2Workshop game;
        readonly Transform[] dots=new Transform[2],rims=new Transform[2],tethers=new Transform[2];
        readonly Renderer[] dotRenderers=new Renderer[2],tetherRenderers=new Renderer[2];
        readonly float[] sizes=new float[2];
        readonly int[] shown={-1,-1};

        public static MemoryGrip Add(Torch2Workshop game,MemoryKit kit)
        {
            var grip=game.gameObject.AddComponent<MemoryGrip>();grip.game=game;
            var root=new GameObject("Grip dots").transform;root.SetParent(game.transform,false);
            for(int h=0;h<2;h++)
            {
                var dot=MemoryUi.Shape(PrimitiveType.Sphere,h==0?"Left grip":"Right grip",root,Vector3.zero,Quaternion.identity,Vector3.one*HoverDot,kit.Stroke,MemoryPalette.AquaHover);
                grip.dots[h]=dot.transform;grip.dotRenderers[h]=dot.GetComponent<Renderer>();
                grip.rims[h]=MemoryUi.Shape(PrimitiveType.Sphere,"Rim",dot.transform,Vector3.zero,Quaternion.identity,Vector3.one,kit.Hull,MemoryPalette.Alpha(MemoryPalette.Ink800,MemoryPalette.RimAlpha)).transform;
                var tether=MemoryUi.Shape(PrimitiveType.Cylinder,h==0?"Left grip tether":"Right grip tether",root,Vector3.zero,Quaternion.identity,Vector3.one,kit.Stroke,MemoryPalette.Alpha(MemoryPalette.AquaHover,MemoryPalette.StrokeLit));
                grip.tethers[h]=tether.transform;grip.tetherRenderers[h]=tether.GetComponent<Renderer>();
                dot.SetActive(false);tether.SetActive(false);
            }
            return grip;
        }
        // 0 nothing, 1 a pinch would take hold here, 2 holding (for tests and the log).
        public int State(int h)=>shown[h]<0?0:shown[h];
        public Vector3 DotPosition(int h)=>dots[h].position;
        public bool DotShown(int h)=>dots[h].gameObject.activeSelf;
        public bool TetherShown(int h)=>tethers[h].gameObject.activeSelf;

        void LateUpdate()
        {
            var input=game.input;
            for(int h=0;h<2;h++)
            {
                var at=Vector3.zero;bool afar=false;
                int state=input&&input.enabled&&!game.Completed?input.Holding(h,out at,out afar)?2:input.Hovering(h,out at)?1:0:0;
                var spoke=at-game.Position;
                if(spoke.sqrMagnitude<1e-6f)state=0;
                if(state==0){Hide(h);continue;}
                if(state!=shown[h])
                {
                    if(shown[h]<=0)sizes[h]=state==2?Dot*.6f:HoverDot*.6f;
                    MemoryUi.Tint(dotRenderers[h],state==2?MemoryPalette.Alpha(MemoryPalette.AquaGrab,MemoryPalette.StrokeHeld):MemoryPalette.Alpha(MemoryPalette.AquaHover,.6f));
                    shown[h]=state;dots[h].gameObject.SetActive(true);
                }
                float target=state==2?Dot:HoverDot;sizes[h]=Mathf.Lerp(sizes[h],target,1-Mathf.Exp(-Ease*Time.unscaledDeltaTime));
                var onShell=game.Position+spoke.normalized*input.Radius;
                dots[h].localPosition=onShell;dots[h].localScale=Vector3.one*sizes[h];rims[h].localScale=Vector3.one*(1+2*MemoryPalette.RimWidth/sizes[h]);
                // The tether runs along the spoke, from the fingers to the shell.
                float gap=spoke.magnitude-input.Radius;bool tie=state==2&&!afar&&Mathf.Abs(gap)>TetherFrom;
                tethers[h].gameObject.SetActive(tie);
                if(tie)
                {
                    var from=at;var to=onShell+spoke.normalized*(Mathf.Sign(gap)*sizes[h]*.5f);
                    var line=to-from;tethers[h].localPosition=(from+to)*.5f;tethers[h].localRotation=Quaternion.FromToRotation(Vector3.up,line);
                    tethers[h].localScale=new Vector3(Tether,line.magnitude*.5f,Tether);
                }
            }
        }
        void Hide(int h)
        {
            if(shown[h]==0)return;shown[h]=0;
            dots[h].gameObject.SetActive(false);tethers[h].gameObject.SetActive(false);
        }
    }
}
