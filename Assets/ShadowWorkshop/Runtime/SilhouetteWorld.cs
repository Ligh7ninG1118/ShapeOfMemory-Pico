using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloudGarden.ShadowPuzzle
{
    public sealed class SilhouetteWorld : MonoBehaviour
    {
        public SilhouetteWorkshop game;
        public readonly List<ShadowControl> controls=new List<ShadowControl>();
        public int HoverPart=-1;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<GameObject> puzzleOnly=new List<GameObject>();
        const int N=SilhouetteProjection.Size;
        readonly Color32[] pixels=new Color32[N*N];
        readonly float[] soft=new float[N*N],softSpill=new float[N*N],scratch=new float[N*N];
        readonly bool[] spill=new bool[N*N];
        Color[][] baseColors;
        MaterialPropertyBlock property;
        Texture2D texture;
        Renderer frame;
        Transform display;
        TMP_Text title,score,message,snapLabel,resetLabel;
        AudioSource audioSource;AudioClip chime;
        float resetUntil,revealTime=-1;
        readonly Color gold=new Color(.94f,.70f,.39f),mint=new Color(.45f,.88f,.75f);
        static readonly Color paper=new Color(.886f,.894f,.839f),targetTint=new Color(.60f,.70f,.65f),stripe=new Color(.77f,.83f,.75f),ink=new Color(.22f,.27f,.27f),
            shade=new Color(.11f,.23f,.25f),amber=new Color(.83f,.53f,.29f);
        public const float ScreenSize=SilhouetteProjection.Span;
        public static readonly Vector3 ScreenPosition=new Vector3(0,.52f,.66f);

        public void Build()
        {
            property=new MaterialPropertyBlock();var level=game.level;
            Box("Sculptor table",new Vector3(0,-.035f,.04f),new Vector3(1.4f,.06f,1.0f),new Color(.085f,.14f,.19f));
            Box("Light base",new Vector3(0,.035f,0),new Vector3(.49f,.025f,.49f),new Color(.16f,.25f,.29f));
            for(int i=-4;i<=4;i++)
            {
                Box("Grid X",new Vector3(i*.05f,.048f,0),new Vector3(.001f,.001f,.46f),new Color(.23f,.37f,.39f));
                Box("Grid Z",new Vector3(0,.048f,i*.05f),new Vector3(.46f,.001f,.001f),new Color(.23f,.37f,.39f));
            }
            title=Label(level.title,new Vector3(0,.93f,.66f),.032f,new Vector2(1.2f,.06f),level.accent);
            Label(level.subtitle,new Vector3(0,.885f,.65f),.013f,new Vector2(1.2f,.04f),new Color(.70f,.78f,.77f));
            var screen=new GameObject("Silhouette screen").transform;screen.SetParent(transform,false);screen.localPosition=ScreenPosition;
            frame=Box("Screen frame",Vector3.zero,new Vector3(ScreenSize+.024f,ScreenSize+.024f,.018f),gold).GetComponent<Renderer>();frame.transform.SetParent(screen,false);frame.transform.localPosition=new Vector3(0,0,.012f);
            var panel=GameObject.CreatePrimitive(PrimitiveType.Quad);panel.name="Projected silhouette";panel.transform.SetParent(screen,false);panel.transform.localScale=new Vector3(ScreenSize,ScreenSize,1);Destroy(panel.GetComponent<Collider>());
            texture=new Texture2D(N,N,TextureFormat.RGBA32,false){name="Everyday silhouette",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};owned.Add(texture);
            var material=new Material(game.screenMaterial);owned.Add(material);material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",texture);material.SetFloat("_Cull",0);panel.GetComponent<Renderer>().sharedMaterial=material;
            score=Label("",new Vector3(0,ScreenSize*.5f+.035f,-.01f),.018f,new Vector2(ScreenSize+.1f,.05f),Color.white);score.transform.SetParent(screen,false);
            // The light sits behind the player's hands; its rays are parallel and run toward the screen.
            var lamp=GameObject.CreatePrimitive(PrimitiveType.Sphere);lamp.name="Front light";lamp.transform.SetParent(transform,false);lamp.transform.localPosition=SilhouetteProjection.Center+Vector3.back*.40f;lamp.transform.localScale=Vector3.one*.024f;
            lamp.GetComponent<Renderer>().sharedMaterial=game.glowMaterial;Destroy(lamp.GetComponent<Collider>());puzzleOnly.Add(lamp);
            display=new GameObject("Everyday objects").transform;display.SetParent(transform,false);baseColors=new Color[game.Count][];
            for(int i=0;i<game.Count;i++)
            {
                var piece=level.pieces[i];var go=new GameObject((i+1).ToString("00")+" "+piece.label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(display,false);
                go.GetComponent<MeshFilter>().sharedMesh=piece.mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterials=piece.materials;renderer.shadowCastingMode=ShadowCastingMode.Off;
                baseColors[i]=new Color[piece.materials.Length];for(int m=0;m<piece.materials.Length;m++)baseColors[i][m]=piece.materials[m]&&piece.materials[m].HasProperty("_BaseColor")?piece.materials[m].GetColor("_BaseColor"):Color.white;
                var pick=new GameObject("Pick surface",typeof(BoxCollider));pick.transform.SetParent(go.transform,false);var box=pick.GetComponent<BoxCollider>();
                box.center=piece.mesh.bounds.center;box.size=piece.mesh.bounds.size+Vector3.one*.008f;
                game.pieces[i]=go.transform;game.renderers[i]=renderer;game.pickSurfaces[i]=box;game.ApplyPose(i);
                var tray=piece.Tray;var extent=Footprint(piece.mesh.bounds,tray.rotation);
                puzzleOnly.Add(Box("Object rest",new Vector3(tray.position.x,.055f,tray.position.z),new Vector3(extent.x+.03f,.012f,extent.y+.03f),new Color(.14f,.22f,.27f)));
                puzzleOnly.Add(Label((i+1).ToString("00")+"  "+piece.label,new Vector3(tray.position.x,.072f,tray.position.z-extent.y*.5f-.03f),.012f,new Vector2(.20f,.03f),new Color(.62f,.74f,.75f)).gameObject);
            }
            message=Label("",new Vector3(0,.13f,-.17f),.016f,new Vector2(.66f,.15f),new Color(.76f,.84f,.83f));message.transform.localRotation=Quaternion.Euler(55,0,0);
            Button("UNDO",new Vector3(-.36f,.025f,-.32f),game.Undo,()=>!game.Completed&&!game.Busy&&game.UndoCount>0);
            Button("HINT",new Vector3(.36f,.025f,-.32f),game.Hint,()=>!game.Completed);
            Button("EDIT",new Vector3(0,.025f,-.32f),game.EditAssembly,()=>game.Completed&&!game.Busy);
            Button("LOWER",new Vector3(-.36f,.025f,-.43f),()=>game.Height(-.025f),()=>!game.Busy);
            Button("RAISE",new Vector3(-.18f,.025f,-.43f),()=>game.Height(.025f),()=>!game.Busy);
            Button("RECENTER",new Vector3(0,.025f,-.43f),()=>game.xr.Recenter(),()=>game.xr&&game.xr.HeadReady);
            snapLabel=Button("GRID",new Vector3(.18f,.025f,-.43f),()=>game.Snap=!game.Snap,()=>!game.Completed).GetComponentInChildren<TMP_Text>();
            resetLabel=Button("RESET",new Vector3(.36f,.025f,-.43f),()=>{if(Time.unscaledTime<resetUntil){game.ResetChallenge();resetUntil=0;}else resetUntil=Time.unscaledTime+4;},()=>!game.Busy).GetComponentInChildren<TMP_Text>();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.volume=.12f;audioSource.spatialBlend=0;
            var samples=new float[22050];for(int i=0;i<samples.Length;i++){float t=i/44100f;samples[i]=(Mathf.Sin(t*523*Mathf.PI*2)+.6f*Mathf.Sin(t*784*Mathf.PI*2)*Mathf.Min(1,t*8))*.6f*Mathf.Exp(-t*6)*Mathf.Min(1,t*120);}
            chime=AudioClip.Create("Silhouette complete",samples.Length,1,44100,false);chime.SetData(samples,0);owned.Add(chime);
            Refresh();
        }
        static Vector2 Footprint(Bounds b,Quaternion rotation)
        {
            var size=Vector2.zero;
            for(int c=0;c<8;c++){var p=rotation*Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));size=Vector2.Max(size,new Vector2(Mathf.Abs(p.x),Mathf.Abs(p.z)));}
            return size*2;
        }
        GameObject Box(string name,Vector3 p,Vector3 size,Color color,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=game.surfaceMaterial;Tint(go.GetComponent<Renderer>(),color);if(!collider)Destroy(go.GetComponent<Collider>());return go;
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
        int Selected(){for(int i=0;i<game.Count;i++)if(game.owners[i]>=0)return i;return HoverPart;}
        public void Refresh()
        {
            var p=game.Projection;var s=p.Score;var level=game.level;
            for(int i=0;i<spill.Length;i++)spill[i]=p.current[i]&&!p.halo[i];
            // Soft penumbra: the display blurs the live shadow by about the scoring tolerance.
            Blur(p.current,soft);Blur(spill,softSpill);
            float reveal=revealTime<0?0:Mathf.Clamp01((Time.unscaledTime-revealTime)/1.2f);reveal=reveal*reveal*(3-2*reveal);
            int selected=game.HintActive?Selected():-1;float pulse=.35f+.2f*Mathf.Sin(Time.unscaledTime*6);
            var fill=Color.Lerp(level.accent,ink,.15f);
            for(int y=0;y<N;y++)for(int x=0;x<N;x++)
            {
                int i=y*N+x;Color c=paper;
                if(p.target[i]){c=targetTint;if(!p.near[i]&&(x+y)%7<2)c=stripe;}
                if(selected>=0&&p.regions[selected][i])c=Color.Lerp(c,level.accent,pulse);
                c=Color.Lerp(c,shade,soft[i]*.92f);c=Color.Lerp(c,amber,softSpill[i]*.95f);
                if(reveal>0)c=Color.Lerp(c,p.target[i]?fill:paper,reveal);
                if(p.outline[i])c=Color.Lerp(c,ink,.75f);
                pixels[i]=c;
            }
            texture.SetPixels32(pixels);texture.Apply(false,false);
            int percent=Mathf.FloorToInt(s.Match(level)*100);
            score.text=game.Completed?level.button+"  /  COMPLETE":"MATCH "+percent+"%"+(s.Ready?"  /  HOLD STILL":s.spill>level.spillMax||s.pieceSpill>level.pieceSpillMax?"  /  TRIM THE AMBER":"");
            score.color=game.Completed||s.Ready?mint:Color.white;
            Tint(frame,game.Completed||s.Ready?mint:Color.Lerp(gold,mint,percent/100f*.35f));
            for(int i=0;i<game.Count;i++)
            {
                bool lit=HoverPart==i||game.owners[i]>=0;var r=game.renderers[i];
                for(int m=0;m<baseColors[i].Length;m++){property.Clear();property.SetColor("_BaseColor",lit?Color.Lerp(baseColors[i][m],Color.white,.35f):baseColors[i][m]);r.SetPropertyBlock(property,m);}
            }
            foreach(var control in controls)
            {
                if(control.name=="EDIT")control.gameObject.SetActive(game.Completed);
                else if(control.name=="UNDO"||control.name=="HINT"||control.name=="GRID")control.gameObject.SetActive(!game.Completed);
                Tint(control.GetComponent<Renderer>(),control.Available?new Color(.24f,.39f,.41f):new Color(.11f,.17f,.20f));
            }
            title.text=level.title;message.text=game.Message;snapLabel.text=game.Snap?"GRID ON":"GRID OFF";resetLabel.text=Time.unscaledTime<resetUntil?"PRESS AGAIN":"RESET x2";
        }
        void Update(){if(revealTime>=0&&Time.unscaledTime-revealTime<1.3f)Refresh();}
        void Blur(bool[] source,float[] output)
        {
            for(int i=0;i<output.Length;i++)output[i]=source[i]?1:0;
            for(int pass=0;pass<3;pass++){BoxPass(output,scratch,0,1);BoxPass(scratch,output,1,1);}
        }
        static void BoxPass(float[] src,float[] dst,int axis,int radius)
        {
            float scale=1f/(radius*2+1);
            for(int y=0;y<N;y++)for(int x=0;x<N;x++)
            {
                int along=axis==0?x:y;float sum=0;
                for(int k=-radius;k<=radius;k++){int a=Mathf.Clamp(along+k,0,N-1);sum+=src[axis==0?y*N+a:a*N+x];}
                dst[y*N+x]=sum*scale;
            }
        }
        public void Reveal()
        {
            revealTime=Time.unscaledTime;foreach(var go in puzzleOnly)go.SetActive(false);
            message.transform.localPosition=new Vector3(0,.15f,-.20f);message.fontSize=.18f;message.color=Color.white;audioSource.PlayOneShot(chime);Refresh();
        }
        public void ResetReveal()
        {
            revealTime=-1;foreach(var go in puzzleOnly)go.SetActive(true);
            message.transform.localPosition=new Vector3(0,.13f,-.17f);message.fontSize=.16f;message.color=new Color(.76f,.84f,.83f);Refresh();
        }
        void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);}
    }
}
