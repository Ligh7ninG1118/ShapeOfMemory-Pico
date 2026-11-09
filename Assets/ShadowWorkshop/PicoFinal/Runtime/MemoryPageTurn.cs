using System;
using UnityEngine;

namespace ShapeOfMemory
{
    // How a turning page moves, in turn progress: 0 lying on the page it leaves, 1 landed on the other. Two points: the
    // root (the page at the spine) and the tip (its free edge). Held, the tip follows the hand and drags the root along
    // once the page bends more than MaxBend, and at once past BendLimit. Let go, both fall toward the side the page was
    // going (Finish, looked ahead by its speed); the tip has more air drag and a stiff bend spring to the root, so a flicked
    // corner keeps going, the edge trails a little once the page is under way and lands last with a small flap. The walls
    // at 0 and 1 stop them. A tap, a click or TurnTo is a short virtual lift of the corner, then let go toward the far side.
    // Fixed steps, so it replays exactly. Tuned as paper, not cloth: it bends gently near the spine and swings as a whole.
    public sealed class PageTurn
    {
        // Held: the tip's spring to the hand; the root's spring to (tip - MaxBend); the most it bends however fast the
        // hand goes (a hand sweeping fast swings the page rather than whipping it into a curl).
        // 2026-11-08: 5% softer than the tuned paper (bends .2/.32, spring 80/12), on the user's word "a bit too rigid".
        public const float TipOmega=20,RootOmega=18,RootDamping=.9f,MaxBend=.21f,BendLimit=.336f;
        // Free: pull toward the goal (progress/s²), drag, the tip's bend spring to the root (damping kept at the same
        // ratio), how much each bounces off a wall.
        public const float Fall=8,TipFall=8.5f,RootDrag=.5f,TipDrag=4,BendSpring=76,BendDamping=11.7f,RootBounce=.08f,TipBounce=.15f;
        // A tap: the corner lifted this much further by a virtual hand over this long, accelerating, then let go.
        public const float AutoLift=.3f,AutoLiftSeconds=.2f;
        // Let go: it turns if the corner-weighted progress, looked ahead by its speed, reaches Finish. A page standing on the
        // walls turns past a third; a page lying flat must be lifted past upright (gravity takes it from there).
        public const float StandingFinish=.35f,FlatFinish=.5f,TipWeight=.7f,LookAhead=.25f;
        public float Finish=StandingFinish;
        // Settled: within Near of the goal and slower than Still. Free for longer than Timeout: put down where it is going.
        const float Dt=1/240f,Near=.003f,Still=.05f,Timeout=3;

        public float Root,Tip,RootSpeed,TipSpeed;
        public bool Held{get;private set;}=true;
        // -1 while held; then 0 (falls back) or 1 (turns).
        public int Goal{get;private set;}=-1;
        public bool Landed{get;private set;}
        // The hardest the edge hit a page during the last Advance (progress/s), for the flap's sound.
        public float Impact{get;private set;}
        float hand,carry,free,autoTime=-1,autoFrom;

        // Held: where the hand puts the corner (progress).
        public void Hold(float target){Held=true;Goal=-1;autoTime=-1;hand=Mathf.Clamp01(target);}
        // A tap: lifts the corner from where it is, then lets it go toward the far side.
        public void Auto(){Held=true;Goal=-1;autoTime=0;autoFrom=Tip;}
        public void Release()=>Release(Decide());
        public void Release(int goal){Held=false;Goal=goal;autoTime=-1;free=0;}
        public int Decide()
        {
            float at=TipWeight*Tip+(1-TipWeight)*Root,speed=TipWeight*TipSpeed+(1-TipWeight)*RootSpeed;
            return at+speed*LookAhead>=Finish?1:0;
        }
        public void Advance(float dt)
        {
            Impact=0;if(Landed)return;
            carry+=Mathf.Max(0,dt);
            while(carry>=Dt&&!Landed){carry-=Dt;Step();}
        }
        void Step()
        {
            if(autoTime>=0)
            {
                autoTime+=Dt;float u=Mathf.Min(1,autoTime/AutoLiftSeconds);hand=Mathf.Min(1,autoFrom+AutoLift*u*u);
                if(autoTime>=AutoLiftSeconds){Release(1);}
            }
            float rootAccel,tipAccel;
            if(Held)
            {
                tipAccel=TipOmega*TipOmega*(hand-Tip)-2*TipOmega*TipSpeed;
                rootAccel=RootOmega*RootOmega*(Mathf.Max(0,Tip-MaxBend)-Root)-2*RootDamping*RootOmega*RootSpeed;
            }
            else
            {
                float toward=Goal==1?1:-1;free+=Dt;
                rootAccel=toward*Fall-RootDrag*RootSpeed;
                tipAccel=toward*TipFall-TipDrag*TipSpeed+BendSpring*(Root-Tip)+BendDamping*(RootSpeed-TipSpeed);
            }
            RootSpeed+=rootAccel*Dt;TipSpeed+=tipAccel*Dt;Root+=RootSpeed*Dt;Tip+=TipSpeed*Dt;
            if(Held){float limited=Mathf.Clamp(Root,Tip-BendLimit,Tip+BendLimit);if(limited!=Root){Root=limited;RootSpeed=TipSpeed;}}
            Wall(ref Root,ref RootSpeed,RootBounce,false);Wall(ref Tip,ref TipSpeed,TipBounce,true);
            if(Held)return;
            float goal=Goal;
            if(Mathf.Abs(Root-goal)<Near&&Mathf.Abs(Tip-goal)<Near&&Mathf.Abs(RootSpeed)<Still&&Mathf.Abs(TipSpeed)<Still||free>Timeout)
            {Root=Tip=goal;RootSpeed=TipSpeed=0;Landed=true;}
        }
        void Wall(ref float x,ref float speed,float bounce,bool edge)
        {
            if(x>=0&&x<=1)return;
            x=Mathf.Clamp01(x);if(edge)Impact=Mathf.Max(Impact,Mathf.Abs(speed));speed=Mathf.Abs(speed)>.15f?-speed*bounce:0;
        }

        // The tapped turn from rest, sampled at every step (tests, captures and Seek).
        static float[] autoRoot,autoTip;
        public static float AutoSeconds{get{Record();return (autoRoot.Length-1)*Dt;}}
        public static void Sample(float p,out float root,out float tip)
        {
            Record();float at=Mathf.Clamp01(p)*(autoRoot.Length-1);int i=Mathf.Min((int)at,autoRoot.Length-2);float w=at-i;
            root=Mathf.Lerp(autoRoot[i],autoRoot[i+1],w);tip=Mathf.Lerp(autoTip[i],autoTip[i+1],w);
        }
        static void Record()
        {
            if(autoRoot!=null)return;
            var turn=new PageTurn();turn.Auto();var roots=new System.Collections.Generic.List<float>{0};var tips=new System.Collections.Generic.List<float>{0};
            while(!turn.Landed&&roots.Count<10000){turn.Step();roots.Add(turn.Root);tips.Add(turn.Tip);}
            autoRoot=roots.ToArray();autoTip=tips.ToArray();
        }
    }

    // The turning page's shape. Each row of the page is a curve from the spine whose direction turns from the page it
    // leaves (along) toward that page's facing (normal), on round to the page it lands on, by an angle that runs from the
    // root's progress to the tip's (times the angle the book opens to: 90° standing on its walls, 180° lying flat), most
    // of the bend near the spine (B). The row it is held by bends a little more than the rest (Lead): drawn by a corner, it
    // folds on a slight slant (more reads as cloth: paper barely twists while it bends). Building the curve from its
    // direction keeps every row exactly the page's width, and keeping the angle within the opening keeps it inside the
    // open book: it can never pass through either page. Rows run along up. The spine sits Rest in front of whichever page
    // the root is nearer. Points rise along up by as much as lift as they swing (mid-turn), so the standing book's bottom
    // edge clears the console on the table.
    public static class PageBend
    {
        public const int Columns=40,Rows=12;
        // CreaseScale: crease per radian of turn per metre of page (1 at 1/CreaseScale).
        public const float Lead=.2f,Rest=.0095f,CreaseScale=.6f;
        public static int Vertices=>(Columns+1)*(Rows+1);
        static float B(float x)=>1-(1-x)*(1-x);
        static float Slope(float x)=>2*(1-x);
        // How far a row runs ahead of the root: the held row (grab: 0 bottom .. 1 top) by Lead more, the far rows less.
        static float RowTip(float root,float tip,float v,float grab)=>Mathf.Clamp01(tip+Lead*(tip-root)*(1-2*Mathf.Abs(v-grab)));

        // positions and normals (Vertices each), crease (0..1, how sharply it bends there) and each row's free-edge point.
        // opening in radians.
        public static void Shape(float root,float tip,Vector3 corner,Vector3 along,Vector3 normal,Vector3 up,float opening,float width,float height,
            float lift,float grab,Vector3[] positions,Vector3[] normals,Color32[] crease,Vector3[] edge)
        {
            root=Mathf.Clamp01(root);tip=Mathf.Clamp01(tip);float ds=width/Columns;
            // Facing the page it leaves at first, the page it lands on at the end.
            float facing=Mathf.PI*.5f+root*(opening-Mathf.PI);
            var hinge=corner+Rest*(Mathf.Cos(facing)*along+Mathf.Sin(facing)*normal);
            for(int j=0;j<=Rows;j++)
            {
                float v=j/(float)Rows,bend=RowTip(root,tip,v,grab)-root;
                var point=hinge;
                for(int i=0;i<=Columns;i++)
                {
                    float x=i/(float)Columns;
                    if(i>0){float mid=(i-.5f)/Columns,a=(root+bend*B(mid))*opening;point+=ds*(Mathf.Cos(a)*along+Mathf.Sin(a)*normal);}
                    float progress=root+bend*B(x);int k=j*(Columns+1)+i;
                    positions[k]=point+up*(v*height+lift*x*Mathf.Sin(progress*Mathf.PI));
                    if(crease!=null)crease[k]=new Color32((byte)(255*Mathf.Clamp01(Mathf.Abs(bend)*Slope(x)*opening/width*CreaseScale)),0,0,255);
                }
                if(edge!=null)edge[j]=positions[j*(Columns+1)+Columns];
            }
            if(normals==null)return;
            for(int j=0;j<=Rows;j++)for(int i=0;i<=Columns;i++)
            {
                var ds2=positions[j*(Columns+1)+Mathf.Min(i+1,Columns)]-positions[j*(Columns+1)+Mathf.Max(i-1,0)];
                var dv=positions[Mathf.Min(j+1,Rows)*(Columns+1)+i]-positions[Mathf.Max(j-1,0)*(Columns+1)+i];
                // The front's normal (the side the right page's print is on): toward the reader while it lies on the right page.
                normals[j*(Columns+1)+i]=Vector3.Cross(dv,ds2).normalized;
            }
        }
        // Where the held row's free edge is, across the page it leaves (x) and out along its facing (y), in page widths.
        public static Vector2 Edge(float root,float tip,float opening,float grab)
        {
            float bend=RowTip(root,tip,grab,grab)-root;var point=Vector2.zero;
            for(int i=1;i<=Columns;i++){float a=(root+bend*B((i-.5f)/Columns))*opening;point+=new Vector2(Mathf.Cos(a),Mathf.Sin(a))/Columns;}
            return point;
        }
        // The tip that puts the held row's edge at angle (radians round the spine from the page it leaves), the root where
        // it is. Further tip, further round, so halving finds it.
        public static float TipFor(float angle,float root,float opening,float grab)
        {
            float low=0,high=1;
            for(int k=0;k<14;k++){float mid=(low+high)*.5f;var e=Edge(root,mid,opening,grab);if(Mathf.Atan2(e.y,e.x)<angle)low=mid;else high=mid;}
            return (low+high)*.5f;
        }
        // Triangles wound so the front faces the reader while the page lies on the leaving page of a forward turn.
        public static int[] Triangles()
        {
            var t=new int[Columns*Rows*6];int n=0;
            for(int j=0;j<Rows;j++)for(int i=0;i<Columns;i++)
            {
                int a=j*(Columns+1)+i,up=a+Columns+1;
                t[n++]=a;t[n++]=up;t[n++]=a+1;t[n++]=a+1;t[n++]=up;t[n++]=up+1;
            }
            return t;
        }
        public static Vector2[] Uvs()
        {
            var uv=new Vector2[Vertices];
            for(int j=0;j<=Rows;j++)for(int i=0;i<=Columns;i++)uv[j*(Columns+1)+i]=new Vector2(i/(float)Columns,j/(float)Rows);
            return uv;
        }
    }
}
