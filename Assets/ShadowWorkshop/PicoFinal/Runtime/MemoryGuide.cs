using CloudGarden.ShadowPuzzle;
using TMPro;
using UnityEngine;

namespace ShapeOfMemory
{
    // Teaching by showing, on the first level's own orb, in steps the player's own actions end (never a timer alone):
    // 1. The goal is printed on the book's dark page (MemoryGame.StagePage).
    // 2. Until the player has rolled the orb about TaughtAngle once: a glowing ghost pinch reaches the ring, closes, drags
    //    it round and lets go, with the gesture beside the orb in the player's own input (hands, look-and-pinch, mouse).
    // (A third step, "STUCK? Press HINT" after a minute without a hint, was removed 2026-11-15 at the user's request.)
    // The orb never moves (Torch2Input.FixedInPlace), so there is nothing to say about bringing it back into the light.
    public sealed class MemoryGuide : MonoBehaviour
    {
        public enum Input{Hands,Gaze,Desktop}
        public const float TaughtAngle=30,Loop=3.4f;
        // Beside the orb, on the side of the page the lamps leave dark (never between the eye and the shadow), turned
        // toward the player.
        static Vector3 Beside(Torch2Workshop game,float side)=>new Vector3(side*(game.input.Radius+.14f),.04f,-.06f);
        static Quaternion Facing(float side)=>Quaternion.Euler(0,35*side,0);
        static float DarkSide(Torch2Level level)=>System.Array.IndexOf(level.lamps,0)>=0&&System.Array.IndexOf(level.lamps,1)<0?1:-1;
        public bool Teaching{get;private set;}
        Torch2Workshop game;
        System.Action taught;
        Transform ghost;
        Transform[] fingers=new Transform[2],tips=new Transform[2];
        TMP_Text teachLabel;
        float clock;

        // teach: the first level, not yet taught.
        public static MemoryGuide Add(Torch2Workshop game,bool teach,Input input,Material glow,System.Action taught)
        {
            var guide=game.gameObject.AddComponent<MemoryGuide>();guide.game=game;guide.taught=taught;guide.Teaching=teach;
            if(!teach)return guide;
            // Said with what the player can see: the objects, and the rings around them (no "orb" yet).
            // Main line Moonlight, its smaller second line Haze.
            guide.input=input;
            guide.teachLabel=Label(game,Teach(input),MemoryPalette.Moonlight);
            // The ghost hand Haze, its pinching tips aqua (the pinch is what to do).
            guide.ghost=new GameObject("Ghost pinch").transform;guide.ghost.SetParent(game.transform,false);
            for(int i=0;i<2;i++)
            {
                guide.fingers[i]=MemoryUi.Shape(PrimitiveType.Capsule,"Ghost finger",guide.ghost,Vector3.zero,Quaternion.identity,Vector3.one,glow,MemoryPalette.Haze).transform;
                guide.tips[i]=MemoryUi.Shape(PrimitiveType.Sphere,"Ghost tip",guide.ghost,Vector3.zero,Quaternion.identity,Vector3.one*.024f,glow,MemoryPalette.AquaHover).transform;
            }
            return guide;
        }
        static string Second(string words)=>"<size=70%>"+MemoryPalette.Colored(words,MemoryPalette.Haze)+"</size>";
        static string Teach(Input input)=>input==Input.Gaze?MemoryText.Get("guide/teach/gaze")+"\n"+Second(MemoryText.Get("guide/teach/gaze/more")):
            (input==Input.Hands?MemoryText.Get("guide/teach/hands"):MemoryText.Get("guide/teach/desktop"))+"\n"+Second(MemoryText.Get("guide/teach/more"));
        Input input;
        // The language changed: the same words in the new one, in its font.
        public void Relabel()
        {
            if(teachLabel){teachLabel.text=Teach(input);MemoryText.Apply(teachLabel);}
        }
        static TMP_Text Label(Torch2Workshop game,string text,Color color)
        {
            float side=DarkSide(game.level);
            var label=MemoryUi.Label(text,game.transform,Torch2Projection.Home+Beside(game,side),Facing(side),.022f,new Vector2(.42f,.12f),color);
            label.gameObject.SetActive(false);return label;
        }
        void Update()
        {
            if(!teachLabel)return;
            var centre=game.Position;bool live=game.input.enabled&&!game.Completed&&!game.Locking;
            if(Teaching&&Quaternion.Angle(game.Projection.Start,game.Rotation)>=TaughtAngle){Stop();taught?.Invoke();}
            if(!Teaching)return;
            bool show=live&&!game.Busy;
            ghost.gameObject.SetActive(show);teachLabel.gameObject.SetActive(show);if(!show){clock=0;return;}
            clock=(clock+Time.unscaledDeltaTime)%Loop;Animate(centre,clock);
        }
        // Reach in (0-.7 s), close (.7-1 s), drag 70 degrees round the orb (1-2.3 s), open (2.3-2.6 s), fade away.
        void Animate(Vector3 centre,float t)
        {
            float reach=Smooth(t/.7f),close=Smooth((t-.7f)/.3f)*(1-Smooth((t-2.3f)/.3f)),drag=Smooth((t-1f)/1.3f),fade=1-Smooth((t-2.7f)/.5f);
            // The pinch point on the ring's front right, where a right hand reaches, dragged across the front to the left.
            var start=new Vector3(.45f,.05f,-1f).normalized*game.input.Radius;
            var on=Quaternion.AngleAxis(70*drag,Vector3.up)*start;
            var away=on.normalized*.14f+new Vector3(0,.02f,-.06f);
            var pinch=centre+on+away*(1-reach);
            float gap=Mathf.Lerp(.06f,.008f,close);var up=Vector3.up;var outward=on.normalized;
            var knuckle=pinch+outward*.095f+up*.03f;
            Finger(0,knuckle,pinch+up*gap*.5f,fade);Finger(1,knuckle-up*.045f,pinch-up*gap*.5f,fade);
        }
        void Finger(int i,Vector3 from,Vector3 to,float size)
        {
            var axis=to-from;float length=axis.magnitude;
            fingers[i].localPosition=(from+to)*.5f;fingers[i].localRotation=Quaternion.FromToRotation(Vector3.up,axis);
            fingers[i].localScale=new Vector3(.02f,length*.5f,.02f)*size;
            tips[i].localPosition=to;tips[i].localScale=Vector3.one*.024f*size;
        }
        static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        void Stop(){Teaching=false;if(ghost)Destroy(ghost.gameObject);if(teachLabel)teachLabel.gameObject.SetActive(false);}
    }
}
