using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloudGarden.ShadowPuzzle
{
    public sealed class ShadowControl : MonoBehaviour
    {
        public Action action;
        public Func<bool> enabledWhen;
        public bool Available => isActiveAndEnabled && (enabledWhen==null||enabledWhen());
        public void Press(){if(Available)action?.Invoke();}
    }

    public sealed class ShadowWorld : MonoBehaviour
    {
        public ShadowWorkshop game;
        public readonly List<ShadowControl> controls=new List<ShadowControl>();
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Renderer[] renderers=new Renderer[3];
        readonly Color[] colors={new Color(.81f,.52f,.25f),new Color(.42f,.73f,.76f),new Color(.79f,.73f,.59f)};
        readonly TMP_Text[] regions=new TMP_Text[3];
        static readonly string[] regionNames={"HULL","SAIL","MAST"};
        TMP_Text message,phase,lockLabel;
        Texture2D texture;Color32[] pixels;
        Material screen;
        AudioSource audioSource;
        AudioClip tone,sea;
        Transform demo;
        float demoUntil,revealTime=-1;
        MaterialPropertyBlock block;
        public int HoverPart=-1;

        public void Build()
        {
            block=new MaterialPropertyBlock();
            Box("Workbench",new Vector3(0,0,.02f),new Vector3(1.08f,.055f,.89f),new Color(.13f,.21f,.24f));
            Box("Front brass trim",new Vector3(0,.008f,-.43f),new Vector3(1.08f,.018f,.014f),new Color(.72f,.48f,.24f));
            for(int s=-1;s<=1;s+=2){Box("Table leg",new Vector3(s*.45f,-.38f,.22f),new Vector3(.055f,.75f,.055f),new Color(.10f,.15f,.18f));}
            Box("Screen frame",new Vector3(0,.32f,.526f),new Vector3(.77f,.69f,.035f),new Color(.62f,.44f,.26f));
            var panel=GameObject.CreatePrimitive(PrimitiveType.Quad);panel.name="Shared projection and score screen";panel.transform.SetParent(transform,false);panel.transform.localPosition=new Vector3(0,.32f,.5f);panel.transform.localScale=new Vector3(ShadowProjection.Width,ShadowProjection.Height,1);Destroy(panel.GetComponent<Collider>());
            texture=new Texture2D(ShadowProjection.Size,ShadowProjection.Size,TextureFormat.RGBA32,false){name="Shadow union mask",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};owned.Add(texture);pixels=new Color32[ShadowProjection.Size*ShadowProjection.Size];
            screen=new Material(game.screenMaterial);owned.Add(screen);screen.SetTexture("_BaseMap",texture);screen.SetColor("_BaseColor",Color.white);screen.SetFloat("_Cull",0);panel.GetComponent<Renderer>().sharedMaterial=screen;
            Label("S H A D O W   /   W O R K S H O P",new Vector3(0,.78f,.49f),.037f,new Vector2(1,.06f),new Color(.89f,.75f,.52f));
            phase=Label("01   /   FIND THE HULL",new Vector3(0,.705f,.48f),.027f,new Vector2(.95f,.06f),Color.white);
            message=Label(game.Message,new Vector3(-.48f,.46f,.36f),.021f,new Vector2(.25f,.35f),new Color(.68f,.80f,.80f));
            message.alignment=TextAlignmentOptions.TopLeft;
            Label("THE LIGHT IS FIXED\nYOU MOVE THE OBJECTS",new Vector3(-.48f,.66f,.36f),.019f,new Vector2(.25f,.1f),new Color(.91f,.71f,.43f)).alignment=TextAlignmentOptions.Left;
            for(int i=0;i<3;i++)regions[i]=Label("",new Vector3(.50f,.45f-i*.065f,.37f),.022f,new Vector2(.24f,.05f),Color.white);
            // Fixed source marker. The projected silhouette is computed from this exact point.
            var lamp=GameObject.CreatePrimitive(PrimitiveType.Sphere);lamp.name="Fixed projection lamp";lamp.transform.SetParent(transform,false);lamp.transform.localPosition=ShadowProjection.Lamp;lamp.transform.localScale=Vector3.one*.02f;lamp.GetComponent<Renderer>().sharedMaterial=game.glowMaterial;Destroy(lamp.GetComponent<Collider>());
            Box("Lamp pedestal",ShadowProjection.Lamp-Vector3.up*.15f,new Vector3(.006f,.29f,.006f),new Color(.34f,.39f,.39f));
            for(int i=0;i<3;i++)
            {
                var go=new GameObject(new[]{"01 Hull block","02 Sail wedge","03 L bracket"}[i],typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(transform,false);go.GetComponent<MeshFilter>().sharedMesh=game.Projection.meshes[i];go.GetComponent<MeshCollider>().sharedMesh=game.Projection.meshes[i];go.GetComponent<MeshCollider>().convex=true;renderers[i]=go.GetComponent<Renderer>();renderers[i].sharedMaterial=game.surfaceMaterial;renderers[i].shadowCastingMode=ShadowCastingMode.Off;game.pieces[i]=go.transform;game.ApplyPose(i);
                var p=ShadowProjection.Tray[i].position;Box("Piece tray",new Vector3(p.x,.04f,p.z),new Vector3(i==0?.17f:.13f,.012f,.17f),new Color(.22f,.30f,.31f));
            }
            Button("KEEP HULL",new Vector3(-.34f,.05f,-.25f),game.KeepHull,()=>game.CanKeep);
            Button("UNDO",new Vector3(-.17f,.05f,-.25f),game.Undo,()=>game.UndoCount>0&&!game.Busy);
            Button("RESTORE",new Vector3(0,.05f,-.25f),game.RestoreHull,()=>game.HullConfirmed&&!game.Busy);
            Button("HINT",new Vector3(.17f,.05f,-.25f),game.Help,()=>true);
            var lockButton=Button("UNLOCK HULL",new Vector3(.34f,.05f,-.25f),game.ToggleHullLock,()=>game.HullConfirmed&&!game.Busy);lockLabel=lockButton.GetComponentInChildren<TMP_Text>();
            Button("LOWER",new Vector3(-.34f,.05f,-.365f),()=>game.Height(-.025f),()=>!game.Busy);
            Button("RAISE",new Vector3(-.17f,.05f,-.365f),()=>game.Height(.025f),()=>!game.Busy);
            Button("RECENTER",new Vector3(0,.05f,-.365f),()=>game.xr.Recenter(),()=>game.xr&&game.xr.HeadReady);
            Button("SAVED HULL",new Vector3(.17f,.05f,-.365f),game.ResumeCheckpoint,()=>!game.Busy&&System.IO.File.Exists(game.SavePath));
            bool confirm=false;float confirmUntil=0;
            Button("RESTART x2",new Vector3(.34f,.05f,-.365f),()=>{if(confirm&&Time.unscaledTime<confirmUntil){game.ResetChallenge();confirm=false;}else{confirm=true;confirmUntil=Time.unscaledTime+4;}},()=>!game.Busy);
            Button("MOON LEVEL",new Vector3(.52f,.025f,-.55f),()=>game.xr.SwitchLevel("MoonCourier"),()=>!game.Busy);
            Label("PINCH + MOVE / TURN     |     RELEASE TO FLOAT",new Vector3(0,.035f,-.155f),.014f,new Vector2(.82f,.04f),new Color(.65f,.78f,.78f)).transform.localRotation=Quaternion.Euler(90,0,0);
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=0;audioSource.volume=.12f;
            tone=MakeAudio(false);sea=MakeAudio(true);owned.Add(tone);owned.Add(sea);
            demo=Box("Rotation and depth example",new Vector3(.49f,.42f,.25f),new Vector3(.08f,.05f,.025f),new Color(.42f,.73f,.76f)).transform;demo.gameObject.SetActive(false);
            Label(Application.platform==RuntimePlatform.Android?"PINCH TO PICK UP\nOPEN FINGERS TO RELEASE\nTURN YOUR PALM TO ROTATE":"MOUSE: DRAG / RIGHT-DRAG TURN\nWHEEL: DEPTH / Q E: ROLL\n1 2 3: SELECT / ENTER: KEEP / Z: UNDO",new Vector3(.50f,.61f,.37f),.014f,new Vector2(.24f,.14f),new Color(.52f,.66f,.67f)).alignment=TextAlignmentOptions.Left;
        }
        GameObject Box(string name,Vector3 position,Vector3 size,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=game.surfaceMaterial;block.SetColor("_BaseColor",color);go.GetComponent<Renderer>().SetPropertyBlock(block);Destroy(go.GetComponent<Collider>());return go;
        }
        TMP_Text Label(string text,Vector3 position,float size,Vector2 bounds,Color color)
        {
            var go=new GameObject(text,typeof(TextMeshPro));go.transform.SetParent(transform,false);go.transform.localPosition=position;var label=go.GetComponent<TextMeshPro>();label.text=text;label.fontSize=size*10;label.color=color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=bounds;label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        ShadowControl Button(string caption,Vector3 position,Action action,Func<bool> enabled)
        {
            var go=Box(caption,position,new Vector3(.152f,.035f,.075f),new Color(.24f,.37f,.39f));go.transform.localRotation=Quaternion.Euler(-20,0,0);var col=go.AddComponent<BoxCollider>();col.size=Vector3.one;
            var control=go.AddComponent<ShadowControl>();control.action=action;control.enabledWhen=enabled;controls.Add(control);
            var label=Label(caption,position+new Vector3(0,.019f,-.006f),.014f,new Vector2(.145f,.04f),new Color(.95f,.87f,.68f));label.transform.localRotation=Quaternion.Euler(70,0,0);label.transform.SetParent(go.transform,true);return control;
        }
        public void Refresh()
        {
            var p=game.Projection;float reveal=revealTime<0?0:Mathf.Clamp01((Time.time-revealTime)/3);
            Color32 paper=Color.Lerp(new Color(.86f,.87f,.77f),new Color(.19f,.37f,.44f),reveal);
            for(int y=0;y<ShadowProjection.Size;y++)for(int x=0;x<ShadowProjection.Size;x++)
            {
                int i=y*ShadowProjection.Size+x;Color32 c=paper;
                if(!game.Completed&&p.target[i])c=new Color32(177,192,170,255);
                if(!game.Completed&&!game.HullConfirmed&&p.regions[0][i])c=new Color32(239,214,155,255);
                if(p.current[i])c=new Color32(29,49,55,255);
                if(!game.Completed&&p.target[i]&&!p.current[i]&&(x+y)%7==0)c=new Color32(214,220,190,255);
                if(!game.Completed&&p.current[i]&&!p.tolerant[i]&&Edge(p.current,x,y))c=new Color32(218,147,88,255);
                if(reveal>0&&y<28&&Mathf.Sin(x*.12f+y*.8f+Time.time*1.2f)>.91f)c=Color.Lerp(paper,new Color(.40f,.66f,.70f),reveal);
                pixels[i]=c;
            }
            texture.SetPixels32(pixels);texture.Apply(false,false);
            for(int i=0;i<3;i++)
            {
                Color color=!game.available[i]?new Color(.17f,.22f,.23f):HoverPart==i||game.owners[i]>=0?Color.Lerp(colors[i],Color.white,.38f):colors[i];
                block.SetColor("_BaseColor",color);renderers[i].SetPropertyBlock(block);
                regions[i].text=regionNames[i]+(game.RegionLit(i)?"  /  READY":game.available[i]?"  /  FIND":"  /  LOCKED");regions[i].color=game.RegionLit(i)?new Color(.54f,.91f,.72f):new Color(.72f,.76f,.70f);
            }
            foreach(var control in controls){block.SetColor("_BaseColor",control.Available?new Color(.27f,.43f,.43f):new Color(.13f,.20f,.22f));control.GetComponent<Renderer>().SetPropertyBlock(block);}
            phase.text=game.Completed?"03   /   A LITTLE BOAT, A NEW HORIZON":game.HullConfirmed?"02   /   BRING THE SAIL AND MAST TOGETHER":game.CanKeep?"HULL FOUND   /   PRESS KEEP HULL":"01   /   FIND THE HULL";
            message.text=game.Message;lockLabel.text=game.HullLocked?"UNLOCK HULL":"LOCK HULL";
            demo.gameObject.SetActive(!game.Completed&&Time.unscaledTime<demoUntil);if(demo.gameObject.activeSelf){demo.localRotation=Quaternion.Euler(15,Time.time*75,0);demo.localPosition=new Vector3(.49f,.19f,.25f+.10f*Mathf.Sin(Time.time*2));}
        }
        static bool Edge(bool[] mask,int x,int y)=>x==0||y==0||x==127||y==127||!mask[y*128+x-1]||!mask[y*128+x+1]||!mask[(y-1)*128+x]||!mask[(y+1)*128+x];
        public void Chime(float pitch){audioSource.pitch=pitch/440f;audioSource.PlayOneShot(tone,.5f);}
        public void ShowDemo(){demoUntil=Time.unscaledTime+10;}
        public void Reveal(){revealTime=Time.time;audioSource.pitch=1;audioSource.clip=sea;audioSource.loop=true;audioSource.Play();}
        public void ResetReveal(){revealTime=-1;audioSource.Stop();audioSource.loop=false;}
        static AudioClip MakeAudio(bool ocean)
        {
            int count=ocean?44100*4:11025;var samples=new float[count];var random=new System.Random(17);float last=0;
            for(int i=0;i<count;i++){float t=i/44100f;if(ocean){last=Mathf.Lerp(last,(float)random.NextDouble()*2-1,.045f);samples[i]=last*(.2f+.15f*Mathf.Sin(t*Mathf.PI*.5f));}else samples[i]=Mathf.Sin(t*440*2*Mathf.PI)*Mathf.Exp(-t*18)*Mathf.Min(1,t*100);}
            var clip=AudioClip.Create(ocean?"Procedural shore":"Local success",count,1,44100,false);clip.SetData(samples,0);return clip;
        }
        void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);}
    }
}
