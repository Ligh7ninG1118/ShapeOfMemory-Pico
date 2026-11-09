using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    public sealed class MoonWorld : MonoBehaviour
    {
        public MoonWorkshop game;
        public readonly List<ShadowControl> controls=new List<ShadowControl>();
        public int HoverPart=-1;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<GameObject> puzzleOnly=new List<GameObject>();
        readonly Renderer[] blocks=new Renderer[MoonProjection.Count],frames=new Renderer[3];
        readonly TMP_Text[] scoreLabels=new TMP_Text[3];
        readonly Texture2D[] textures=new Texture2D[3];
        readonly Color32[] pixels=new Color32[MoonProjection.Size*MoonProjection.Size];
        MaterialPropertyBlock property;
        readonly Color mint=new Color(.42f,.85f,.76f),gold=new Color(.94f,.67f,.34f);
        static readonly Color[] colors={new Color(.31f,.64f,.65f),new Color(.90f,.79f,.58f),new Color(.91f,.61f,.34f),new Color(.91f,.61f,.34f),new Color(.31f,.43f,.51f),new Color(.31f,.43f,.51f),new Color(.63f,.76f,.72f),new Color(.63f,.76f,.72f),new Color(.88f,.45f,.30f)};
        TMP_Text message,phase,snapLabel,spinLabel;
        Transform display,lightMarker,face;
        AudioSource audioSource;
        AudioClip chime;
        float restartUntil;
        public void Build()
        {
            property=new MaterialPropertyBlock();
            Box("Moon workbench",new Vector3(0,-.035f,.04f),new Vector3(1.26f,.06f,.94f),new Color(.095f,.15f,.20f));
            Box("Build plinth",new Vector3(0,.035f,0),new Vector3(.47f,.025f,.40f),new Color(.16f,.25f,.29f));
            for(int i=-4;i<=4;i++)
            {
                Box("Grid X",new Vector3(i*.05f,.048f,0),new Vector3(.001f,.001f,.38f),new Color(.23f,.37f,.39f));
                if(Mathf.Abs(i)<4)Box("Grid Z",new Vector3(0,.048f,i*.05f),new Vector3(.45f,.001f,.001f),new Color(.23f,.37f,.39f));
            }
            Label("S H A D O W   /   V O L U M E",new Vector3(0,.90f,.62f),.032f,new Vector2(1.2f,.06f),gold);
            phase=Label("01 / FRONT",new Vector3(0,.83f,.60f),.024f,new Vector2(1.3f,.06f),Color.white);
            display=new GameObject("Your nine-block sculpture").transform;display.SetParent(transform,false);
            for(int i=0;i<MoonProjection.Count;i++)
            {
                var go=Box((i+1)+" "+MoonProjection.Names[i],Vector3.zero,MoonProjection.Dimensions[i],colors[i],true);
                go.transform.SetParent(display,false);game.pieces[i]=go.transform;blocks[i]=go.GetComponent<Renderer>();game.ApplyPose(i);
                var tray=MoonProjection.Tray[i].position;
                puzzleOnly.Add(Box("Tray "+i,new Vector3(tray.x,tray.y-MoonProjection.Dimensions[i].y*.5f-.008f,tray.z),new Vector3(.16f,.008f,.16f),new Color(.16f,.23f,.28f)));
                puzzleOnly.Add(Label((i+1)+"  "+MoonProjection.Names[i],tray+new Vector3(0,-MoonProjection.Dimensions[i].y*.5f-.026f,-.09f),.012f,new Vector2(.17f,.025f),new Color(.60f,.73f,.74f)).gameObject);
            }
            for(int view=0;view<3;view++)
            {
                float size=view==0?.48f:.32f;var position=view==0?new Vector3(0,.49f,.62f):new Vector3(view==1?-.565f:.565f,.57f,.50f);
                var frame=Box("View "+view+" frame",position+Vector3.forward*.012f,new Vector3(size+.022f,size+.022f,.018f),gold);frames[view]=frame.GetComponent<Renderer>();puzzleOnly.Add(frame);
                var panel=GameObject.CreatePrimitive(PrimitiveType.Quad);panel.name="Live silhouette "+view;panel.transform.SetParent(transform,false);panel.transform.localPosition=position;panel.transform.localScale=new Vector3(size,size,1);Destroy(panel.GetComponent<Collider>());puzzleOnly.Add(panel);
                textures[view]=new Texture2D(MoonProjection.Size,MoonProjection.Size,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};owned.Add(textures[view]);
                var material=new Material(game.screenMaterial);owned.Add(material);material.SetTexture("_BaseMap",textures[view]);material.SetColor("_BaseColor",Color.white);material.SetFloat("_Cull",0);panel.GetComponent<Renderer>().sharedMaterial=material;
                scoreLabels[view]=Label("",position+new Vector3(0,size*.5f+.04f,-.01f),.018f,new Vector2(size+.07f,.055f),mint);puzzleOnly.Add(scoreLabels[view].gameObject);
            }
            message=Label("",new Vector3(0,.11f,-.22f),.016f,new Vector2(.57f,.14f),new Color(.75f,.84f,.83f));message.transform.localRotation=Quaternion.Euler(55,0,0);
            lightMarker=Box("Active parallel light",Vector3.zero,Vector3.one*.022f,gold).transform;puzzleOnly.Add(lightMarker.gameObject);
            for(int i=0;i<3;i++){int view=i;Button(new[]{"FRONT","LEFT 45","RIGHT 45"}[i],new Vector3((i-1)*.18f,.025f,-.30f),()=>game.SetFocus(view),()=>!game.Completed);}
            Button("UNDO",new Vector3(-.36f,.025f,-.30f),game.Undo,()=>!game.Busy&&!game.Completed&&game.UndoCount>0);
            Button("HINT",new Vector3(.36f,.025f,-.30f),game.Hint,()=>!game.Completed);
            Button("LOWER",new Vector3(-.36f,.025f,-.40f),()=>game.Height(-.025f),()=>!game.Busy);
            Button("RAISE",new Vector3(-.18f,.025f,-.40f),()=>game.Height(.025f),()=>!game.Busy);
            Button("RECENTER",new Vector3(0,.025f,-.40f),()=>game.xr.Recenter(),()=>game.xr&&game.xr.HeadReady);
            snapLabel=Button("GRID ON",new Vector3(.18f,.025f,-.40f),()=>game.Snap=!game.Snap,()=>!game.Completed).GetComponentInChildren<TMP_Text>();
            Button("RESET x2",new Vector3(.36f,.025f,-.40f),()=>{if(Time.unscaledTime<restartUntil){game.ResetChallenge();restartUntil=0;}else{restartUntil=Time.unscaledTime+4;}},()=>!game.Busy);
            spinLabel=Button("PAUSE TURN",new Vector3(-.18f,.025f,-.20f),()=>game.Spinning=!game.Spinning,()=>game.Completed).GetComponentInChildren<TMP_Text>();
            Button("EDIT",new Vector3(.18f,.025f,-.20f),game.EditAssembly,()=>game.Completed);
            Button("BOAT LEVEL",new Vector3(.52f,.025f,-.55f),()=>game.xr.SwitchLevel("ShadowWorkshop"),()=>!game.Busy);
            Button("THREE WISHES",new Vector3(-.52f,.025f,-.55f),()=>game.xr.SwitchLevel("ThreeWishes"),()=>!game.Busy);
            face=new GameObject("Revealed face details").transform;face.SetParent(game.pieces[1],false);
            // Compensate for the head's primitive scale so the reveal details retain metre units.
            face.localScale=new Vector3(1/MoonProjection.Dimensions[1].x,1/MoonProjection.Dimensions[1].y,1/MoonProjection.Dimensions[1].z);
            for(int side=-1;side<=1;side+=2){var eye=Box("Explorer eye",Vector3.zero,new Vector3(.017f,.017f,.004f),mint);eye.transform.SetParent(face,false);eye.transform.localPosition=new Vector3(side*.032f,.004f,-.062f);}
            face.gameObject.SetActive(false);
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=0;audioSource.volume=.12f;
            var samples=new float[10000];for(int i=0;i<samples.Length;i++){float t=i/44100f;samples[i]=Mathf.Sin(t*660*Mathf.PI*2)*Mathf.Exp(-t*20)*Mathf.Min(1,t*120);}
            chime=AudioClip.Create("View matched",samples.Length,1,44100,false);chime.SetData(samples,0);owned.Add(chime);
        }
        void Update()
        {
            if(game.Completed&&game.Spinning){float angle=Time.unscaledDeltaTime*14;display.RotateAround(transform.TransformPoint(MoonProjection.Center),transform.up,angle);}
        }
        GameObject Box(string name,Vector3 p,Vector3 size,Color color,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=game.surfaceMaterial;Tint(go.GetComponent<Renderer>(),color);if(!collider)Destroy(go.GetComponent<Collider>());return go;
        }
        void Tint(Renderer renderer,Color color){property.SetColor("_BaseColor",color);renderer.SetPropertyBlock(property);}
        TMP_Text Label(string text,Vector3 p,float size,Vector2 bounds,Color color)
        {
            var go=new GameObject(text,typeof(TextMeshPro));go.transform.SetParent(transform,false);go.transform.localPosition=p;var label=go.GetComponent<TextMeshPro>();label.text=text;label.fontSize=size*10;label.color=color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=bounds;label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        ShadowControl Button(string text,Vector3 p,Action action,Func<bool> enabled)
        {
            var go=Box(text,p,new Vector3(.16f,.03f,.065f),new Color(.24f,.36f,.39f),true);go.transform.localRotation=Quaternion.Euler(-20,0,0);
            var control=go.AddComponent<ShadowControl>();control.action=action;control.enabledWhen=enabled;controls.Add(control);
            var label=Label(text,p+new Vector3(0,.017f,-.006f),.012f,new Vector2(.15f,.04f),Color.white);label.transform.localRotation=Quaternion.Euler(70,0,0);label.transform.SetParent(go.transform,true);return control;
        }
        public void Refresh()
        {
            for(int v=0;v<3;v++)
            {
                var target=game.Projection.target[v];var current=game.Projection.current[v];var score=game.Projection.scores[v];
                for(int y=0;y<MoonProjection.Size;y++)for(int x=0;x<MoonProjection.Size;x++)
                {
                    int i=y*MoonProjection.Size+x;Color32 c=new Color32(221,225,210,255);
                    if(target[i])c=new Color32(155,181,167,255);
                    if(target[i]&&!current[i]&&(x+y)%6<2)c=new Color32(190,210,191,255);
                    if(current[i])c=target[i]?new Color32(34,72,73,255):new Color32(211,135,74,255);
                    pixels[i]=c;
                }
                textures[v].SetPixels32(pixels);textures[v].Apply(false,false);
                scoreLabels[v].text=new[]{"FRONT","LEFT 45","RIGHT 45"}[v]+"   "+Mathf.RoundToInt(score.iou*100)+"%"+(score.Ready?"  READY":"");scoreLabels[v].color=score.Ready?mint:Color.white;
                Tint(frames[v],score.Ready?mint:game.Focus==v?gold:new Color(.23f,.34f,.36f));
            }
            for(int i=0;i<blocks.Length;i++)Tint(blocks[i],HoverPart==i||game.owners[i]>=0?Color.Lerp(colors[i],Color.white,.35f):colors[i]);
            foreach(var control in controls)
            {
                if(control.name=="PAUSE TURN"||control.name=="EDIT")control.gameObject.SetActive(game.Completed);
                else if(control.name=="FRONT"||control.name=="LEFT 45"||control.name=="RIGHT 45"||control.name=="HINT"||control.name=="UNDO"||control.name=="GRID ON")control.gameObject.SetActive(!game.Completed);
                Tint(control.GetComponent<Renderer>(),control.Available?new Color(.24f,.39f,.41f):new Color(.10f,.17f,.20f));
            }
            phase.text=game.Completed?"M O O N   C O U R I E R   /   YOUR SCULPTURE":new[]{"01 / FRONT OUTLINE","02 / LEFT 45 - FIND THE DEPTH","03 / RIGHT 45 - ALL VIEWS TOGETHER"}[game.Stage];
            message.text=game.Message;snapLabel.text=game.Snap?"GRID ON":"GRID OFF";spinLabel.text=game.Spinning?"PAUSE TURN":"TURN";
            lightMarker.localPosition=MoonProjection.Center+MoonProjection.Views[game.Focus]*Vector3.back*.36f;
        }
        public void Chime(){audioSource.PlayOneShot(chime);}
        public void Reveal(){foreach(var go in puzzleOnly)go.SetActive(false);message.transform.localPosition=new Vector3(0,.63f,.45f);message.transform.localRotation=Quaternion.identity;message.rectTransform.sizeDelta=new Vector2(.8f,.18f);face.gameObject.SetActive(true);game.Spinning=true;Chime();}
        public void ResetReveal(){display.localPosition=Vector3.zero;display.localRotation=Quaternion.identity;foreach(var go in puzzleOnly)go.SetActive(true);face.gameObject.SetActive(false);message.transform.localPosition=new Vector3(0,.11f,-.22f);message.transform.localRotation=Quaternion.Euler(55,0,0);message.rectTransform.sizeDelta=new Vector2(.57f,.14f);}
        void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);}
    }
}
