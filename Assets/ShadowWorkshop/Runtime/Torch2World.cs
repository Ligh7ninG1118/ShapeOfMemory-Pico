using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloudGarden.ShadowPuzzle
{
    // The game around the shared room (Torch2Room): labels, buttons, picture cards and the pile in its orb.
    public sealed class Torch2World : MonoBehaviour
    {
        public Torch2Workshop game;
        public readonly List<ShadowControl> controls=new List<ShadowControl>();
        // What a pinch would grab right now (set each frame by Torch2Input).
        public Torch2Input.Grip Hover;
        const int G=Torch2Projection.Grid;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        Torch2Room room;
        readonly TMP_Text[] wallLabels=new TMP_Text[2];
        readonly List<GameObject> cards=new List<GameObject>();
        bool shadowsDrawn;
        Quaternion shownRotation;
        Vector3 shownPosition;
        MaterialPropertyBlock property;
        Transform pile;
        Renderer[] rings;
        Renderer hub;
        Torch2Input.Grip shownHighlight=(Torch2Input.Grip)(-1);
        bool shownHeld;
        static readonly Color ringColor=new Color(.28f,.44f,.48f,Torch2Room.RingAlpha),ringLit=new Color(.55f,.92f,1f,Torch2Room.RingLitAlpha);
        // The rings' tints at rest, with a hand near enough to roll them, and while rolling them. A host can set its own
        // (RingColors); the gym keeps these.
        Color ringRest=ringColor,ringHover=ringLit,ringGrab=ringLit;
        public void RingColors(Color rest,Color hover,Color grab){ringRest=rest;ringHover=hover;ringGrab=grab;shownHighlight=(Torch2Input.Grip)(-1);Highlight();}
        // The hub's colour at rest (a host can make it see-through, with a transparent material on Hub).
        public Color HubColor=new Color(.16f,.16f,.17f);
        public Renderer Hub=>hub;
        Renderer[] pieceRenderers;
        Color[][] baseColors;
        TMP_Text title,warmth,message,resetLabel;
        AudioSource audioSource;AudioClip chime;
        float resetUntil,revealTime=-1;
        readonly Color mint=new Color(.45f,.88f,.75f),gold=new Color(.94f,.70f,.39f);
        public bool Animating=>revealTime>=0&&Time.unscaledTime-revealTime<1.3f;
        public Transform Pile=>pile;
        // The pile's parts, in level.pieces order, for a host that takes it apart and builds it again (Shape of Memory's
        // intro): each piece, its wire to the hub (null when it sits at the hub), and the orb's three rings and hub.
        public Transform[] Pieces{get;private set;}
        public Transform[] Wires{get;private set;}
        public Transform[] OrbParts{get;private set;}
        // A host that shows the pieces floating free (Shape of Memory): the wires go, and Wires holds nulls.
        public void RemoveWires(){for(int i=0;i<Wires.Length;i++)if(Wires[i]){Torch2Room.Kill(Wires[i].gameObject);Wires[i]=null;}}
        public Torch2Room Room=>room;
        // 0 = lamps off: dark walls, so no shadow shows, and a silent cue. 1 = on.
        public float LampLevel{get=>lampLevel;set{lampLevel=Mathf.Clamp01(value);room.Lamps(lampLevel);Cue.Level=lampLevel;RefreshWalls();}}
        float lampLevel=1;
        public Torch2Cue Cue{get;private set;}

        public void Build()
        {
            property=new MaterialPropertyBlock();var level=game.level;
            room=new Torch2Room(transform,game.surfaceMaterial,game.screenMaterial,game.glowMaterial,level.lamps,owned);game.roomStyle?.Invoke(room);
            var size=Torch2Room.WallSize;bool hud=!game.bare;
            if(hud)for(int k=0;k<2;k++){wallLabels[k]=Label("",room.wallCentres[k]+Vector3.up*(size.y*.5f+.035f)+Torch2Projection.WallNormal[k]*.01f,.015f,new Vector2(.8f,.04f),Color.white);wallLabels[k].transform.localRotation=room.wallFacing[k];}
            if(hud)title=Label(level.title,new Vector3(0,1.20f,.62f),.032f,new Vector2(1.4f,.06f),level.accent);
            if(hud)warmth=Label("",new Vector3(0,1.145f,.62f),.018f,new Vector2(1.2f,.05f),Color.white);
            pile=new GameObject("Posable pile").transform;pile.SetParent(transform,false);
            pieceRenderers=new Renderer[level.pieces.Length];baseColors=new Color[level.pieces.Length][];
            Pieces=new Transform[level.pieces.Length];Wires=new Transform[level.pieces.Length];
            for(int i=0;i<level.pieces.Length;i++)
            {
                var piece=level.pieces[i];var go=new GameObject(piece.label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(pile,false);Pieces[i]=go.transform;
                var at=game.Drawn(piece.position);
                go.transform.localPosition=at;go.transform.localRotation=Quaternion.Euler(piece.euler);go.transform.localScale=piece.Scale;
                go.GetComponent<MeshFilter>().sharedMesh=piece.mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterials=piece.materials;renderer.shadowCastingMode=ShadowCastingMode.Off;
                pieceRenderers[i]=renderer;baseColors[i]=new Color[piece.materials.Length];
                for(int m=0;m<piece.materials.Length;m++)baseColors[i][m]=piece.materials[m]&&piece.materials[m].HasProperty("_BaseColor")?piece.materials[m].GetColor("_BaseColor"):Color.white;
                // Thin wires to a small hub keep the pile reading as one sculpture; they are below a wall pixel.
                float length=at.magnitude;
                if(length>.02f){var wire=room.Cylinder("Wire",at*.5f,new Vector3(.003f,length*.5f,.003f),new Color(.12f,.12f,.13f),pile);wire.transform.localRotation=Quaternion.FromToRotation(Vector3.up,at);Wires[i]=wire.transform;}
            }
            rings=room.Orb(pile,out hub);Highlight();
            OrbParts=new Transform[rings.Length+1];for(int i=0;i<rings.Length;i++)OrbParts[i]=rings[i].transform;OrbParts[rings.Length]=hub.transform;
            if(!game.hostShowsPictures)foreach(int k in level.lamps)
            {
                var card=new GameObject("Picture card "+k).transform;card.SetParent(transform,false);
                // Inside the room, at the front corner on the same side as its wall.
                card.localPosition=new Vector3(k==0?-.58f:.58f,.62f,-.18f);card.localRotation=Quaternion.Euler(0,k==0?-30:30,0);
                room.Quad("Target picture "+k,Vector3.zero,new Vector2(.24f,.24f),CardTexture(k)).transform.SetParent(card,false);
                Label(k==0?"LEFT WALL":"RIGHT WALL",new Vector3(0,.15f,0),.013f,new Vector2(.3f,.04f),gold).transform.SetParent(card,false);cards.Add(card.gameObject);
            }
            if(hud)
            {
                message=Label("",new Vector3(0,.13f,-.20f),.016f,new Vector2(.72f,.15f),new Color(.76f,.84f,.83f));message.transform.localRotation=Quaternion.Euler(55,0,0);
                Button("HINT",new Vector3(.36f,.025f,-.32f),game.Hint,()=>!game.Completed);
                Button("LOWER",new Vector3(-.36f,.025f,-.43f),()=>game.Height(-.025f),()=>!game.Busy);
                Button("RAISE",new Vector3(-.18f,.025f,-.43f),()=>game.Height(.025f),()=>!game.Busy);
                Button("RECENTER",new Vector3(0,.025f,-.43f),()=>game.xr.Recenter(),()=>game.xr&&game.xr.HeadReady);
                resetLabel=Button("RESET",new Vector3(.36f,.025f,-.43f),()=>{if(Time.unscaledTime<resetUntil){game.ResetChallenge();resetUntil=0;}else resetUntil=Time.unscaledTime+4;},()=>!game.Busy).GetComponentInChildren<TMP_Text>();
            }
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.volume=.12f;audioSource.spatialBlend=0;
            var samples=new float[30870];for(int i=0;i<samples.Length;i++){float t=i/44100f;samples[i]=(Mathf.Sin(t*523*Mathf.PI*2)+.6f*Mathf.Sin(t*659*Mathf.PI*2)*Mathf.Min(1,t*5)+.5f*Mathf.Sin(t*784*Mathf.PI*2)*Mathf.Min(1,t*3))*.45f*Mathf.Exp(-t*4)*Mathf.Min(1,t*120);}
            chime=AudioClip.Create("Torch2 pictures found",samples.Length,1,44100,false);chime.SetData(samples,0);owned.Add(chime);
            Cue=gameObject.AddComponent<Torch2Cue>();Cue.Build(game,room,owned);
            Sync();RefreshWalls();Refresh();
        }
        Texture2D CardTexture(int k)
        {
            int slot=Array.IndexOf(game.level.lamps,k);var texture=room.Texture("Torch2 target "+k,G,G);var card=new Color32[G*G];
            for(int i=0;i<card.Length;i++)card[i]=game.Projection.target[slot][i]?Color.Lerp(game.level.accent,Torch2Room.Dark,.55f):new Color(.93f,.88f,.78f);
            texture.SetPixels32(card);texture.Apply(false,false);return texture;
        }
        TMP_Text Label(string text,Vector3 p,float size,Vector2 bounds,Color color)
        {
            var go=new GameObject(text,typeof(TextMeshPro));go.transform.SetParent(transform,false);go.transform.localPosition=p;var label=go.GetComponent<TextMeshPro>();label.text=text;label.fontSize=size*10;label.color=color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=bounds;label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        ShadowControl Button(string text,Vector3 p,Action action,Func<bool> enabled,float width=.16f)
        {
            var go=room.Box(text,p,new Vector3(width,.03f,.065f),new Color(.24f,.36f,.39f),true);go.transform.localRotation=Quaternion.Euler(-20,0,0);
            var control=go.AddComponent<ShadowControl>();control.action=action;control.enabledWhen=enabled;controls.Add(control);
            var label=Label(text,p+new Vector3(0,.017f,-.006f),.012f,new Vector2(width-.01f,.04f),Color.white);label.transform.localRotation=Quaternion.Euler(70,0,0);label.transform.SetParent(go.transform,true);return control;
        }
        // After input has moved the pile this frame, so the pile and its shadows never lag each other.
        void LateUpdate(){Sync();Highlight();}
        void Highlight()
        {
            var grabbed=game.input?game.input.Grabbed:Torch2Input.Grip.None;var active=grabbed!=Torch2Input.Grip.None?grabbed:Hover;bool held=grabbed!=Torch2Input.Grip.None;
            if(active==shownHighlight&&held==shownHeld)return;shownHighlight=active;shownHeld=held;
            foreach(var r in rings)room.Tint(r,active==Torch2Input.Grip.Roll?held?ringGrab:ringHover:ringRest);
            room.Tint(hub,active==Torch2Input.Grip.Carry?gold:HubColor);
        }
        void Sync()
        {
            pile.localPosition=game.Position;pile.localRotation=game.Rotation;
            if(shadowsDrawn&&game.Rotation.Equals(shownRotation)&&game.Position==shownPosition)return;
            shadowsDrawn=true;shownRotation=game.Rotation;shownPosition=game.Position;room.Shadows(game.Projection,game.Rotation,game.Position);
        }
        public void RefreshWalls()
        {
            float glow=revealTime>=0?Mathf.Clamp01((Time.unscaledTime-revealTime)/1.2f):0;
            room.Light(game.Projection,Color.Lerp(room.Base,Color.Lerp(Torch2Room.Warm,Color.Lerp(Torch2Room.Warm,game.level.accent,.45f),glow),lampLevel));
        }
        public void Refresh()
        {
            var p=game.Projection;var s=p.Score;int percent=Mathf.RoundToInt(s.warmth*100);
            if(warmth)
            {
                warmth.text=game.Completed?game.level.button+"  /  FOUND":s.Ready?"HOLD STILL":"WARMTH "+percent+"%";
                warmth.color=game.Completed||s.Ready?mint:Color.Lerp(new Color(.55f,.75f,.95f),new Color(1f,.55f,.30f),s.warmth);
            }
            for(int k=0;k<2;k++)
            {
                int slot=Array.IndexOf(game.level.lamps,k);bool used=slot>=0;var ws=used?p.scores[slot]:default;
                if(wallLabels[k])
                {
                    wallLabels[k].text=!used?"":game.Completed?"":ws.Ready?"THIS WALL  /  READY":"THIS WALL  "+Mathf.RoundToInt(ws.warmth*100)+"%";
                    wallLabels[k].gameObject.SetActive(used&&game.TwoLamps);wallLabels[k].color=ws.Ready?mint:Color.white;
                }
                // Kept in a bare game too: on two-wall levels the frame of a wall that is right turns mint.
                room.Tint(room.frames[k],game.Completed&&used?Color.Lerp(Torch2Room.FrameColor,game.level.accent,.6f):used&&ws.Ready&&game.TwoLamps?Color.Lerp(Torch2Room.FrameColor,mint,.5f):Torch2Room.FrameColor);
            }
            foreach(var card in cards)card.SetActive(game.HintActive||game.Completed||game.CardsShown);
            bool lit=game.Busy;
            for(int i=0;i<pieceRenderers.Length;i++)for(int m=0;m<baseColors[i].Length;m++){property.Clear();property.SetColor("_BaseColor",lit?Color.Lerp(baseColors[i][m],Color.white,.30f):baseColors[i][m]);pieceRenderers[i].SetPropertyBlock(property,m);}
            foreach(var control in controls)
            {
                if(control.name=="HINT")control.gameObject.SetActive(!game.Completed);
                room.Tint(control.GetComponent<Renderer>(),control.Available?new Color(.24f,.39f,.41f):new Color(.11f,.17f,.20f));
            }
            if(message){message.text=game.Message;resetLabel.text=Time.unscaledTime<resetUntil?"PRESS AGAIN":"RESET x2";}
        }
        public void Reveal(){revealTime=Time.unscaledTime;audioSource.PlayOneShot(chime);if(message){message.fontSize=.18f;message.color=Color.white;}Refresh();}
        public void ResetReveal(){revealTime=-1;if(message){message.fontSize=.16f;message.color=new Color(.76f,.84f,.83f);}Cue.ResetTiers();RefreshWalls();Refresh();}
        void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);}
    }
}
