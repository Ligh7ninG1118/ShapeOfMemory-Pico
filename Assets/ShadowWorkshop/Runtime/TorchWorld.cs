using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloudGarden.ShadowPuzzle
{
    public sealed class TorchWorld : MonoBehaviour
    {
        public TorchWorkshop game;
        public readonly List<ShadowControl> controls=new List<ShadowControl>();
        public bool HoverTurntable;
        const int W=TorchProjection.Width,H=TorchProjection.Height,G=TorchProjection.Grid;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Color32[] wallPixels=new Color32[W*H],cardPixels=new Color32[G*G];
        readonly float[] soft=new float[W*H],scratch=new float[W*H];
        MaterialPropertyBlock property;
        Texture2D wall,card;
        Transform turntable,flashlight,cardRoot;
        Renderer disc,frame;
        TMP_Text title,warmth,message,swapLabel,resetLabel;
        AudioSource audioSource;AudioClip chime;
        float resetUntil,revealTime=-1;
        readonly Color mint=new Color(.45f,.88f,.75f),gold=new Color(.94f,.70f,.39f);
        static readonly Color dark=new Color(.065f,.07f,.08f),warm=new Color(1f,.90f,.72f);
        public bool Animating=>revealTime>=0&&Time.unscaledTime-revealTime<1.3f;
        public Transform Turntable=>turntable;
        public Transform Flashlight=>flashlight;

        public void Build()
        {
            property=new MaterialPropertyBlock();var level=game.level;
            Box("Sculptor table",new Vector3(0,-.035f,.04f),new Vector3(1.4f,.06f,1.0f),new Color(.085f,.14f,.19f));
            // The wall: a CPU-drawn texture of the flashlight spot and the pile's shadow.
            var wallCenter=new Vector3(TorchProjection.WallX0+W*TorchProjection.Pixel*.5f,TorchProjection.WallY0+H*TorchProjection.Pixel*.5f,TorchProjection.WallZ);
            var wallSize=new Vector2(W*TorchProjection.Pixel,H*TorchProjection.Pixel);
            frame=Box("Wall frame",wallCenter+new Vector3(0,0,.02f),new Vector3(wallSize.x+.03f,wallSize.y+.03f,.02f),new Color(.16f,.17f,.18f)).GetComponent<Renderer>();
            wall=Texture("Torch wall",W,H);var panel=Quad("Shadow wall",wallCenter,wallSize,wall);
            title=Label(level.title,new Vector3(0,wallCenter.y+wallSize.y*.5f+.09f,TorchProjection.WallZ-.01f),.032f,new Vector2(1.3f,.06f),level.accent);
            warmth=Label("",new Vector3(0,wallCenter.y+wallSize.y*.5f+.04f,TorchProjection.WallZ-.01f),.018f,new Vector2(1.2f,.05f),Color.white);
            Cylinder("Turntable pedestal",new Vector3(TorchProjection.Axis.x,.05f,TorchProjection.Axis.z),new Vector3(.14f,.05f,.14f),new Color(.20f,.24f,.27f));
            turntable=new GameObject("Turntable").transform;turntable.SetParent(transform,false);turntable.localPosition=new Vector3(TorchProjection.Axis.x,0,TorchProjection.Axis.z);
            disc=Cylinder("Turntable disc",new Vector3(0,.108f,0),new Vector3(.38f,.008f,.38f),new Color(.45f,.32f,.20f),turntable).GetComponent<Renderer>();
            Box("Turntable mark",new Vector3(0,.118f,.17f),new Vector3(.012f,.004f,.03f),gold).transform.SetParent(turntable,false);
            foreach(var piece in level.pieces)
            {
                var go=new GameObject(piece.label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(turntable,false);
                go.transform.localPosition=piece.position-TorchProjection.Axis;go.transform.localRotation=Quaternion.Euler(piece.euler);
                go.GetComponent<MeshFilter>().sharedMesh=piece.mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterials=piece.materials;renderer.shadowCastingMode=ShadowCastingMode.Off;
                // Sculpture pins: 3 mm rods are below one wall pixel, so they are left out of the shadow.
                float length=Mathf.Max(.01f,piece.position.y-.116f);
                Cylinder("Pin",new Vector3(piece.position.x-TorchProjection.Axis.x,.116f+length*.5f,piece.position.z-TorchProjection.Axis.z),new Vector3(.003f,length*.5f,.003f),new Color(.12f,.12f,.13f),turntable);
            }
            flashlight=new GameObject("Flashlight").transform;flashlight.SetParent(transform,false);
            Cylinder("Flashlight body",new Vector3(0,0,-.085f),new Vector3(.032f,.065f,.032f),new Color(.12f,.12f,.13f),flashlight).transform.localRotation=Quaternion.Euler(90,0,0);
            Cylinder("Flashlight head",new Vector3(0,0,-.014f),new Vector3(.046f,.014f,.046f),new Color(.30f,.30f,.32f),flashlight).transform.localRotation=Quaternion.Euler(90,0,0);
            var lens=Cylinder("Flashlight lens",Vector3.zero,new Vector3(.040f,.002f,.040f),Color.white,flashlight);lens.transform.localRotation=Quaternion.Euler(90,0,0);lens.GetComponent<Renderer>().sharedMaterial=game.glowMaterial;
            var spot=new GameObject("Flashlight beam").AddComponent<Light>();spot.transform.SetParent(flashlight,false);spot.type=LightType.Spot;
            spot.spotAngle=TorchProjection.HalfAngle*2+6;spot.range=2.5f;spot.intensity=2.2f;spot.color=warm;spot.shadows=LightShadows.None;
            cardRoot=new GameObject("Picture card").transform;cardRoot.SetParent(transform,false);cardRoot.localPosition=new Vector3(.88f,.70f,.50f);cardRoot.localRotation=Quaternion.Euler(0,25,0);
            card=Texture("Torch target",G,G);Quad("Target picture",Vector3.zero,new Vector2(.26f,.26f),card).transform.SetParent(cardRoot,false);
            Label("THE PICTURE",new Vector3(0,.16f,0),.014f,new Vector2(.3f,.04f),gold).transform.SetParent(cardRoot,false);
            for(int i=0;i<G*G;i++){var c=game.Projection.target[i]?Color.Lerp(level.accent,dark,.55f):new Color(.93f,.88f,.78f);cardPixels[i]=c;}
            card.SetPixels32(cardPixels);card.Apply(false,false);
            message=Label("",new Vector3(0,.13f,-.17f),.016f,new Vector2(.70f,.15f),new Color(.76f,.84f,.83f));message.transform.localRotation=Quaternion.Euler(55,0,0);
            swapLabel=Button("SWAP HANDS",new Vector3(-.36f,.025f,-.32f),game.SwapHands,()=>!game.Busy).GetComponentInChildren<TMP_Text>();
            Button("HINT",new Vector3(.36f,.025f,-.32f),game.Hint,()=>!game.Completed);
            Button("LOWER",new Vector3(-.36f,.025f,-.43f),()=>game.Height(-.025f),()=>!game.Busy);
            Button("RAISE",new Vector3(-.18f,.025f,-.43f),()=>game.Height(.025f),()=>!game.Busy);
            Button("RECENTER",new Vector3(0,.025f,-.43f),()=>game.xr.Recenter(),()=>game.xr&&game.xr.HeadReady);
            resetLabel=Button("RESET",new Vector3(.36f,.025f,-.43f),()=>{if(Time.unscaledTime<resetUntil){game.ResetChallenge();resetUntil=0;}else resetUntil=Time.unscaledTime+4;},()=>!game.Busy).GetComponentInChildren<TMP_Text>();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.volume=.12f;audioSource.spatialBlend=0;
            var samples=new float[26460];for(int i=0;i<samples.Length;i++){float t=i/44100f;samples[i]=(Mathf.Sin(t*587*Mathf.PI*2)+.7f*Mathf.Sin(t*880*Mathf.PI*2)*Mathf.Min(1,t*6))*.55f*Mathf.Exp(-t*5)*Mathf.Min(1,t*120);}
            chime=AudioClip.Create("Torch picture found",samples.Length,1,44100,false);chime.SetData(samples,0);owned.Add(chime);
            Sync();RefreshWall();Refresh();
        }
        Texture2D Texture(string name,int w,int h){var t=new Texture2D(w,h,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};owned.Add(t);return t;}
        GameObject Quad(string name,Vector3 position,Vector2 size,Texture2D texture)
        {
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name=name;quad.transform.SetParent(transform,false);quad.transform.localPosition=position;quad.transform.localScale=new Vector3(size.x,size.y,1);Destroy(quad.GetComponent<Collider>());
            var material=new Material(game.screenMaterial);owned.Add(material);material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",texture);material.SetFloat("_Cull",0);quad.GetComponent<Renderer>().sharedMaterial=material;return quad;
        }
        GameObject Box(string name,Vector3 p,Vector3 size,Color color,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=game.surfaceMaterial;Tint(go.GetComponent<Renderer>(),color);if(!collider)Destroy(go.GetComponent<Collider>());return go;
        }
        GameObject Cylinder(string name,Vector3 p,Vector3 scale,Color color,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent?parent:transform,false);go.transform.localPosition=p;go.transform.localScale=scale;
            var r=go.GetComponent<Renderer>();r.sharedMaterial=game.surfaceMaterial;r.shadowCastingMode=ShadowCastingMode.Off;Tint(r,color);Destroy(go.GetComponent<Collider>());return go;
        }
        void Tint(Renderer r,Color color){property.Clear();property.SetColor("_BaseColor",color);r.SetPropertyBlock(property);}
        TMP_Text Label(string text,Vector3 p,float size,Vector2 bounds,Color color)
        {
            var go=new GameObject(text,typeof(TextMeshPro));go.transform.SetParent(transform,false);go.transform.localPosition=p;var label=go.GetComponent<TextMeshPro>();label.text=text;label.fontSize=size*10;label.color=color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=bounds;label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        ShadowControl Button(string text,Vector3 p,Action action,Func<bool> enabled,float width=.16f)
        {
            var go=Box(text,p,new Vector3(width,.03f,.065f),new Color(.24f,.36f,.39f),true);go.transform.localRotation=Quaternion.Euler(-20,0,0);
            var control=go.AddComponent<ShadowControl>();control.action=action;control.enabledWhen=enabled;controls.Add(control);
            var label=Label(text,p+new Vector3(0,.017f,-.006f),.012f,new Vector2(width-.01f,.04f),Color.white);label.transform.localRotation=Quaternion.Euler(70,0,0);label.transform.SetParent(go.transform,true);return control;
        }
        void Update()=>Sync();
        void Sync()
        {
            turntable.localRotation=Quaternion.Euler(0,game.Yaw,0);
            flashlight.localPosition=game.Light;flashlight.localRotation=Quaternion.LookRotation(game.Aim,Vector3.up);
        }
        public void RefreshWall()
        {
            var p=game.Projection;
            for(int i=0;i<soft.Length;i++)soft[i]=p.shadow[i]?1:0;
            bool crisp=revealTime>=0;
            for(int pass=crisp?1:0;pass<2;pass++){Pass(soft,scratch,true);Pass(scratch,soft,false);}
            float glow=crisp?Mathf.Clamp01((Time.unscaledTime-revealTime)/1.2f):0;var light=Color.Lerp(warm,Color.Lerp(warm,game.level.accent,.45f),glow);
            for(int i=0;i<wallPixels.Length;i++){float b=p.brightness[i];wallPixels[i]=dark+(light-dark)*(b*(1-.9f*soft[i]));}
            wall.SetPixels32(wallPixels);wall.Apply(false,false);
        }
        // Soft penumbra: a flashlight is a small area light, not a point.
        static void Pass(float[] src,float[] dst,bool horizontal)
        {
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float sum=0;
                for(int k=-1;k<=1;k++){int a=horizontal?Mathf.Clamp(x+k,0,W-1):x,b=horizontal?y:Mathf.Clamp(y+k,0,H-1);sum+=src[b*W+a];}
                dst[y*W+x]=sum/3;
            }
        }
        public void Refresh()
        {
            var s=game.Projection.Score;int percent=Mathf.RoundToInt(s.warmth*100);
            warmth.text=game.Completed?game.level.button+"  /  FOUND":!s.visible?"NO SHADOW ON THE WALL":s.Ready?"HOLD STILL":"WARMTH "+percent+"%";
            warmth.color=game.Completed||s.Ready?mint:Color.Lerp(new Color(.55f,.75f,.95f),new Color(1f,.55f,.30f),s.warmth);
            Tint(frame,game.Completed?Color.Lerp(new Color(.16f,.17f,.18f),game.level.accent,.6f):new Color(.16f,.17f,.18f));
            Tint(disc,HoverTurntable||game.Busy?new Color(.62f,.46f,.30f):new Color(.45f,.32f,.20f));
            cardRoot.gameObject.SetActive(game.HintActive||game.Completed);
            foreach(var control in controls)
            {
                if(control.name=="HINT")control.gameObject.SetActive(!game.Completed);
                Tint(control.GetComponent<Renderer>(),control.Available?new Color(.24f,.39f,.41f):new Color(.11f,.17f,.20f));
            }
            message.text=game.Message;swapLabel.text=game.LeftHanded?"LIGHT IN LEFT":"LIGHT IN RIGHT";resetLabel.text=Time.unscaledTime<resetUntil?"PRESS AGAIN":"RESET x2";
        }
        public void Reveal(){revealTime=Time.unscaledTime;audioSource.PlayOneShot(chime);message.fontSize=.18f;message.color=Color.white;Refresh();}
        public void ResetReveal(){revealTime=-1;message.fontSize=.16f;message.color=new Color(.76f,.84f,.83f);Refresh();}
        void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);}
    }
}
