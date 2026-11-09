using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    // A pile of everyday objects on a turntable and a flashlight. One hand turns the table, the other holds the light.
    public sealed class TorchWorkshop : MonoBehaviour
    {
        public const float SettleSeconds=.5f,MinLightDistance=.16f;
        public Camera viewCamera;
        public ShadowPlayerContext xr;
        public Material surfaceMaterial,screenMaterial,glowMaterial;
        public TorchLevel level;
        public TorchWorld world;
        public TorchInput input;
        public TorchProjection Projection {get;private set;}
        public float Yaw {get;private set;}
        // Flashlight lens position and beam direction, workbench space.
        public Vector3 Light {get;private set;}
        public Vector3 Aim {get;private set;}
        public int TurnOwner {get;private set;}=-1;
        public bool Busy=>TurnOwner>=0;
        public bool Completed {get;private set;}
        public bool LeftHanded;
        public int TorchHand=>LeftHanded?0:1;
        public int TurnHand=>LeftHanded?1:0;
        public float StableTime {get;private set;}
        public string Message {get;private set;}
        public bool HintActive=>Time.unscaledTime<hintUntil;
        public float Azimuth {get;private set;}
        public float Elevation {get;private set;}
        public float Distance {get;private set;}
        float nextWall,nextScore,hintUntil;
        void Awake()
        {
            Projection=new TorchProjection(level);Yaw=level.startYaw;Orbit(25,-12,.46f);
            world=gameObject.AddComponent<TorchWorld>();world.game=this;world.Build();
            input=gameObject.AddComponent<TorchInput>();input.game=this;Refresh(true);
        }
        void Update()
        {
            // Light follows a hand, so the wall redraws at 30 Hz; scoring stays at 10 Hz like the other levels.
            if(Time.unscaledTime>=nextWall){nextWall=Time.unscaledTime+1/30f;if(Projection.Render(Yaw,Light,Aim)||world.Animating)world.RefreshWall();}
            if(Time.unscaledTime>=nextScore){nextScore=Time.unscaledTime+.1f;Refresh(false);}
        }
        void Refresh(bool render){if(render)Projection.Render(Yaw,Light,Aim);Projection.Evaluate();Evaluate(.1f);world.Refresh();}
        public void Evaluate(float dt)
        {
            if(Completed)return;
            StableTime=Projection.Score.Ready&&!Busy?StableTime+dt:0;
            if(StableTime>=SettleSeconds){Completed=true;StableTime=0;hintUntil=0;Message=level.complete;world.Reveal();Debug.Log($"TORCH_COMPLETE: {level.name} yaw={Yaw:0} iou={Projection.Score.iou:0.00}");}
            else UpdateMessage();
        }
        void UpdateMessage()
        {
            if(HintActive){Message=level.hint;return;}
            var s=Projection.Score;
            if(!s.visible)Message=level.riddle+"\n"+(xr&&xr.Active?"Point the flashlight in your "+(LeftHanded?"left":"right")+" hand at the pile.":"Right-drag moves the flashlight around the pile.");
            else if(s.Ready)Message="There it is. Hold the light still...";
            else if(s.warmth>.75f)Message="Very warm. Small turns now.";
            else if(s.warmth>.45f)Message="Warmer. Keep turning the table, or raise and lower the light.";
            else Message=level.riddle+"\n"+(xr&&xr.Active?"Pinch the turntable with your "+(LeftHanded?"right":"left")+" hand to turn it.":"Left-drag turns the table. Wheel: light distance.");
        }
        public void SetLight(Vector3 position,Vector3 aim)
        {
            // The lens cannot sit inside the pile; that would also make every triangle cover the whole wall.
            var offset=position-TorchProjection.Center;if(offset.magnitude<MinLightDistance)position=TorchProjection.Center+(offset.sqrMagnitude>1e-6f?offset.normalized:Vector3.back)*MinLightDistance;
            Light=position;Aim=aim.sqrMagnitude>1e-8f?aim.normalized:TorchProjection.Center-position;
        }
        // Desktop model: the light orbits the pile centre and always aims at it.
        public void Orbit(float azimuth,float elevation,float distance)
        {
            Azimuth=Mathf.Repeat(azimuth+180,360)-180;Elevation=Mathf.Clamp(elevation,-35,45);Distance=Mathf.Clamp(distance,.28f,.75f);
            float a=Azimuth*Mathf.Deg2Rad,e=Elevation*Mathf.Deg2Rad;
            var position=TorchProjection.Center+Distance*new Vector3(Mathf.Sin(a)*Mathf.Cos(e),Mathf.Sin(e),-Mathf.Cos(a)*Mathf.Cos(e));
            SetLight(position,TorchProjection.Center-position);
        }
        public bool BeginTurn(int owner){if(Completed||owner<0||(TurnOwner>=0&&TurnOwner!=owner))return false;TurnOwner=owner;StableTime=0;return true;}
        public void Turn(int owner,float yaw){if(TurnOwner==owner)Yaw=Mathf.Repeat(yaw,360);}
        public void EndTurn(int owner){if(TurnOwner==owner)TurnOwner=-1;}
        public void Hint(){if(Completed)return;hintUntil=Time.unscaledTime+12;UpdateMessage();world.Refresh();}
        public void ResetChallenge()
        {
            if(Busy)return;Completed=false;StableTime=0;hintUntil=0;Yaw=level.startYaw;Orbit(25,-12,.46f);world.ResetReveal();Refresh(true);
        }
        public void SwapHands(){if(Busy)return;LeftHanded=!LeftHanded;input.ReleaseAll();UpdateMessage();world.Refresh();}
        public void Height(float amount){if(!Busy)transform.position+=Vector3.up*amount;}
    }
}
