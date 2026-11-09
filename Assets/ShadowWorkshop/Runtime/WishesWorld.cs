using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    public sealed class WishesWorld : MonoBehaviour
    {
        public WishesWorkshop game;
        public readonly List<ShadowControl> controls=new List<ShadowControl>();
        public int HoverPart=-1;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<GameObject> trays=new List<GameObject>();
        readonly Transform[] boards=new Transform[3];
        readonly Renderer[] frames=new Renderer[3];
        readonly TMP_Text[] scores=new TMP_Text[3],viewButtons=new TMP_Text[3];
        readonly Texture2D[] textures=new Texture2D[3];
        readonly float[] openedAt={-10,-10,-10};
        readonly Color32[] pixels=new Color32[WishesProjection.Size*WishesProjection.Size];
        static readonly string[] titles={"HOME / LIGHTHOUSE","GROWTH / PINE","JOURNEY / ROCKET"};
        static readonly Color[] colors={new Color(.81f,.49f,.27f),new Color(.29f,.65f,.65f),new Color(.87f,.80f,.61f)};
        readonly Color gold=new Color(.94f,.70f,.39f),mint=new Color(.45f,.88f,.75f);
        MaterialPropertyBlock property;
        Transform display,lightMarker;
        TMP_Text phase,message,snapLabel,revealLabel,spinLabel,resetLabel;
        AudioSource audioSource;AudioClip chime;
        float resetUntil;
        public int VisibleViews {get{int count=0;foreach(var b in boards)if(b&&b.gameObject.activeSelf)count++;return count;}}
        public Transform Board(int view)=>boards[view];
        public void Build()
        {
            property=new MaterialPropertyBlock();
            Box("Sculptor table",new Vector3(0,-.035f,.04f),new Vector3(1.4f,.06f,1.0f),new Color(.085f,.14f,.19f));
            Box("Shared sculpture base",new Vector3(0,.035f,0),new Vector3(.49f,.025f,.47f),new Color(.16f,.25f,.29f));
            for(int i=-4;i<=4;i++)
            {
                Box("Grid X",new Vector3(i*.05f,.048f,0),new Vector3(.001f,.001f,.44f),new Color(.23f,.37f,.39f));
                Box("Grid Z",new Vector3(0,.048f,i*.05f),new Vector3(.45f,.001f,.001f),new Color(.23f,.37f,.39f));
            }
            Label("T H R E E   W I S H E S",new Vector3(0,.93f,.66f),.032f,new Vector2(1.2f,.06f),gold);
            phase=Label("01 / HOME",new Vector3(0,.865f,.64f),.024f,new Vector2(1.25f,.06f),Color.white);
            display=new GameObject("Player's shadow-hull sculpture").transform;display.SetParent(transform,false);
            for(int i=0;i<WishesProjection.Count;i++)
            {
                var go=new GameObject(game.Projection.data.pieces[i].name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(display,false);
                go.GetComponent<MeshFilter>().sharedMesh=game.Projection.meshes[i];var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=game.surfaceMaterial;game.renderers[i]=renderer;
                var colliders=new List<Collider>();
                foreach(var mesh in game.Projection.convexMeshes[i])
                {
                    var child=new GameObject("Convex pick surface",typeof(MeshCollider));child.transform.SetParent(go.transform,false);
                    var collider=child.GetComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;colliders.Add(collider);
                }
                game.pickSurfaces[i]=colliders.ToArray();game.pieces[i]=go.transform;game.ApplyPose(i);Tint(renderer,colors[i/4]);
                var p=WishesProjection.Tray[i].position;
                trays.Add(Box("Piece rest",new Vector3(p.x,.055f,p.z),new Vector3(.16f,.012f,.18f),new Color(.14f,.22f,.27f)));
                trays.Add(Label((i+1).ToString("00"),new Vector3(p.x,.065f,p.z-.105f),.014f,new Vector2(.12f,.03f),colors[i/4]).gameObject);
            }
            for(int view=0;view<3;view++)
            {
                var root=new GameObject("Unlocked view "+view).transform;root.SetParent(transform,false);boards[view]=root;
                root.localPosition=view==0?new Vector3(0,.52f,.64f):new Vector3(view==1?-.60f:.60f,.61f,.47f);
                root.localRotation=Quaternion.Euler(0,view==0?0:view==1?-45:45,0);
                float size=view==0?.53f:.44f;
                var frame=Box("View frame",Vector3.zero,new Vector3(size+.022f,size+.022f,.018f),gold);frame.transform.SetParent(root,false);frame.transform.localPosition=new Vector3(0,0,.012f);frames[view]=frame.GetComponent<Renderer>();
                var panel=GameObject.CreatePrimitive(PrimitiveType.Quad);panel.name="Projected silhouette";panel.transform.SetParent(root,false);panel.transform.localScale=new Vector3(size,size,1);Destroy(panel.GetComponent<Collider>());
                textures[view]=new Texture2D(WishesProjection.Size,WishesProjection.Size,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};owned.Add(textures[view]);
                var material=new Material(game.screenMaterial);owned.Add(material);material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",textures[view]);material.SetFloat("_Cull",0);panel.GetComponent<Renderer>().sharedMaterial=material;
                scores[view]=Label("",Vector3.zero,.017f,new Vector2(size+.08f,.06f),Color.white);scores[view].transform.SetParent(root,false);scores[view].transform.localPosition=new Vector3(0,size*.5f+.04f,-.01f);
                // Locked views are inactive from construction, including their text and targets.
                root.gameObject.SetActive(view==0);
            }
            message=Label("",new Vector3(0,.13f,-.17f),.016f,new Vector2(.63f,.15f),new Color(.76f,.84f,.83f));message.transform.localRotation=Quaternion.Euler(55,0,0);
            lightMarker=Box("Selected light direction",Vector3.zero,Vector3.one*.018f,gold).transform;
            for(int i=0;i<3;i++){int view=i;viewButtons[i]=Button(new[]{"FRONT","LEFT 45","RIGHT 45"}[i],new Vector3((i-1)*.18f,.025f,-.22f),()=>game.SetFocus(view),()=>!game.Completed&&game.ViewUnlocked(view)).GetComponentInChildren<TMP_Text>();}
            Button("RESTORE",new Vector3(-.52f,.025f,-.55f),game.Restore,()=>!game.Completed&&!game.Busy&&game.Stage>0);
            Button("MOON LEVEL",new Vector3(.52f,.025f,-.55f),()=>game.xr.SwitchLevel("MoonCourier"),()=>!game.Busy);
            Button("UNDO",new Vector3(-.36f,.025f,-.32f),game.Undo,()=>!game.Completed&&!game.Busy&&game.UndoCount>0);
            revealLabel=Button("REVEAL",new Vector3(0,.025f,-.32f),()=>game.RevealNext(),()=>game.CanReveal,.50f).GetComponentInChildren<TMP_Text>();
            Button("HINT",new Vector3(.36f,.025f,-.32f),game.Hint,()=>!game.Completed);
            Button("LOWER",new Vector3(-.36f,.025f,-.43f),()=>game.Height(-.025f),()=>!game.Busy);
            Button("RAISE",new Vector3(-.18f,.025f,-.43f),()=>game.Height(.025f),()=>!game.Busy);
            Button("RECENTER",new Vector3(0,.025f,-.43f),()=>game.xr.Recenter(),()=>game.xr&&game.xr.HeadReady);
            snapLabel=Button("GRID",new Vector3(.18f,.025f,-.43f),()=>game.Snap=!game.Snap,()=>!game.Completed).GetComponentInChildren<TMP_Text>();
            resetLabel=Button("RESET",new Vector3(.36f,.025f,-.43f),()=>{if(Time.unscaledTime<resetUntil){game.ResetChallenge();resetUntil=0;}else resetUntil=Time.unscaledTime+4;},()=>!game.Busy).GetComponentInChildren<TMP_Text>();
            spinLabel=Button("TURN",new Vector3(-.18f,.025f,-.32f),()=>game.Spinning=!game.Spinning,()=>game.Completed).GetComponentInChildren<TMP_Text>();
            Button("EDIT",new Vector3(.18f,.025f,-.32f),game.EditAssembly,()=>game.Completed);
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.volume=.12f;audioSource.spatialBlend=0;
            var samples=new float[12000];for(int i=0;i<samples.Length;i++){float t=i/44100f;samples[i]=Mathf.Sin(t*660*Mathf.PI*2)*Mathf.Exp(-t*14)*Mathf.Min(1,t*120);}
            chime=AudioClip.Create("A new wish",samples.Length,1,44100,false);chime.SetData(samples,0);owned.Add(chime);
            Refresh();
        }
        void Update()
        {
            if(game.Completed&&game.Spinning)display.RotateAround(transform.TransformPoint(WishesProjection.Center),transform.up,Time.unscaledDeltaTime*12);
            if(!game.Completed)for(int i=0;i<3;i++)if(boards[i].gameObject.activeSelf)
            {float t=Mathf.Clamp01((Time.unscaledTime-openedAt[i])/.75f);boards[i].localScale=Vector3.one*Mathf.Max(.001f,t*t*(3-2*t));}
        }
        GameObject Box(string name,Vector3 p,Vector3 size,Color color,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=game.surfaceMaterial;Tint(go.GetComponent<Renderer>(),color);if(!collider)Destroy(go.GetComponent<Collider>());return go;
        }
        void Tint(Renderer r,Color color){property.SetColor("_BaseColor",color);r.SetPropertyBlock(property);}
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
        public void Refresh()
        {
            for(int v=0;v<3;v++)
            {
                viewButtons[v].text=game.ViewUnlocked(v)?new[]{"FRONT","LEFT 45","RIGHT 45"}[v]:"LOCKED";
                if(!game.ViewUnlocked(v))continue;
                var target=game.Projection.target[v];var current=game.Projection.current[v];var score=game.Projection.scores[v];
                int selected=HoverPart;for(int i=0;i<WishesProjection.Count;i++)if(game.owners[i]>=0){selected=i;break;}
                var selectedMask=selected>=0?game.Projection.PartMask(selected,v):null;
                for(int y=0;y<WishesProjection.Size;y++)for(int x=0;x<WishesProjection.Size;x++)
                {
                    int p=y*WishesProjection.Size+x;Color32 c=new Color32(225,228,213,255);
                    if(target[p])c=new Color32(148,177,164,255);
                    if(target[p]&&!current[p]&&(x+y)%7<2)c=new Color32(194,211,189,255);
                    if(current[p])c=target[p]?new Color32(29,63,66,255):new Color32(211,135,74,255);
                    if(selectedMask!=null&&selectedMask[p]&&target[p])c=new Color32(66,138,151,255);
                    pixels[p]=c;
                }
                textures[v].SetPixels32(pixels);textures[v].Apply(false,false);
                scores[v].text=titles[v]+"\n"+Mathf.RoundToInt(score.iou*100)+"%"+(score.Ready?"  READY":score.windowClear<.80f?"  OPEN THE WINDOW":"");scores[v].color=score.Ready?mint:Color.white;
                Tint(frames[v],score.Ready?mint:game.Focus==v?gold:new Color(.23f,.34f,.36f));
            }
            for(int i=0;i<WishesProjection.Count;i++)Tint(game.renderers[i],HoverPart==i||game.owners[i]>=0?Color.Lerp(colors[i/4],Color.white,.38f):colors[i/4]);
            foreach(var control in controls)
            {
                bool final=control.name=="TURN"||control.name=="EDIT";
                bool puzzle=control.name=="FRONT"||control.name=="LEFT 45"||control.name=="RIGHT 45"||control.name=="REVEAL"||control.name=="HINT"||control.name=="UNDO"||control.name=="GRID"||control.name=="RESTORE";
                if(final)control.gameObject.SetActive(game.Completed);else if(puzzle)control.gameObject.SetActive(!game.Completed);
                Tint(control.GetComponent<Renderer>(),control.Available?(control.name=="REVEAL"?new Color(.31f,.59f,.49f):new Color(.24f,.39f,.41f)):new Color(.11f,.17f,.20f));
            }
            phase.text=game.Completed?"ONE SCULPTURE / THREE WISHES":new[]{"01 / HOME - ONE VIEW","02 / GROWTH - TWO VIEWS TOGETHER","03 / JOURNEY - THREE VIEWS TOGETHER"}[game.Stage];
            message.text=game.Message;snapLabel.text=game.Snap?"GRID ON":"GRID OFF";spinLabel.text=game.Spinning?"PAUSE TURN":"TURN";
            resetLabel.text=Time.unscaledTime<resetUntil?"PRESS AGAIN":"RESET x2";
            revealLabel.text=game.CanReveal?new[]{"REVEAL LEFT 45","REVEAL RIGHT 45","REVEAL SCULPTURE"}[game.Stage]:"MATCH "+(game.Stage+1)+" VIEW"+(game.Stage==0?"":"S")+" TO REVEAL";
            lightMarker.localPosition=WishesProjection.Center+WishesProjection.Views[game.Focus]*Vector3.back*.37f;
        }
        public void Unlock(int view){openedAt[view]=Time.unscaledTime;boards[view].localScale=Vector3.one*.001f;boards[view].gameObject.SetActive(true);}
        public void Chime(){audioSource.PlayOneShot(chime);}
        public void Reveal()
        {
            foreach(var b in boards)b.gameObject.SetActive(false);foreach(var t in trays)t.SetActive(false);lightMarker.gameObject.SetActive(false);
            message.transform.localPosition=new Vector3(0,.66f,.50f);message.transform.localRotation=Quaternion.identity;message.rectTransform.sizeDelta=new Vector2(.85f,.20f);game.Spinning=true;Chime();
        }
        public void ResetReveal()
        {
            display.localPosition=Vector3.zero;display.localRotation=Quaternion.identity;
            for(int i=0;i<3;i++){boards[i].gameObject.SetActive(game.ViewUnlocked(i));boards[i].localScale=Vector3.one;openedAt[i]=-10;}
            foreach(var t in trays)t.SetActive(true);lightMarker.gameObject.SetActive(true);
            message.transform.localPosition=new Vector3(0,.13f,-.17f);message.transform.localRotation=Quaternion.Euler(55,0,0);message.rectTransform.sizeDelta=new Vector2(.63f,.15f);Refresh();
        }
        void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);}
    }
}
