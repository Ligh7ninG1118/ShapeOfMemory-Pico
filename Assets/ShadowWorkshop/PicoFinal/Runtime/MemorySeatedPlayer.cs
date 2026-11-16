using UnityEngine;

namespace ShapeOfMemory
{
    // A stand-in player for laying out the play area in the editor: a seated adult on a chair (Art/Dev/SeatedPlayer.fbx,
    // made by Tools/ShadowArt/build_seated_player.py), its eyes where the game puts a player's eyes over the play area
    // (MemoryGame.StageAhead behind and StageBelow over the play area's origin, the table top's middle: in a headset the game places the play area
    // from the eyes, so this is true for anyone), its feet on its own floor under it. Stature changes the player: the model
    // scales about its eyes, so a shorter player's floor comes up and the table top is that much higher over it.
    // Editor only: its object is tagged EditorOnly (builds leave it out) and it hides itself in Play mode.
    [ExecuteAlways]
    public sealed class MemorySeatedPlayer : MonoBehaviour
    {
        // The model: a 1.70 m adult, eyes 1.215 m over its floor and .105 m ahead of its origin (it faces +z).
        public const float ModelStature=1.70f,ModelEyeHeight=1.215f,ModelEyeAhead=.105f;
        [Tooltip("The player's standing height (metres). The model is 1.70 m on a 45 cm chair.")]
        [Range(1.2f,2.1f)] public float Stature=ModelStature;
        [Tooltip("The play area it sits at (MemoryGame.PlayArea). Empty: its parent.")]
        public Transform PlayArea;
        [Tooltip("Keep the eyes where the game puts them over the play area. Off: move it by hand.")]
        public bool KeepAtTheEyes=true;
        public float Scale=>Stature/ModelStature;
        public float EyesOverFloor=>ModelEyeHeight*Scale;
        // Where the game puts the eyes, in the play area's space.
        public static Vector3 Eyes=>new Vector3(0,MemoryGame.StageBelow,-MemoryGame.StageAhead);
        // The table top (the play area's origin) over this player's floor.
        public float TableOverFloor=>EyesOverFloor-MemoryGame.StageBelow;

        void OnEnable(){if(!Application.isPlaying)Place();}
        void Start(){if(Application.isPlaying)gameObject.SetActive(false);}
        void OnValidate(){if(!Application.isPlaying)Place();}
        void Update(){if(!Application.isPlaying)Place();}
        public void Place()
        {
            if(!KeepAtTheEyes)return;
            var area=PlayArea?PlayArea:transform.parent;
            float k=Scale;var local=Eyes-new Vector3(0,ModelEyeHeight*k,ModelEyeAhead*k);
            var at=area?area.TransformPoint(local):local;var turn=area?Quaternion.Euler(0,area.eulerAngles.y,0):Quaternion.identity;
            if((transform.position-at).sqrMagnitude>1e-10f)transform.position=at;
            if(Quaternion.Angle(transform.rotation,turn)>.01f)transform.rotation=turn;
            var scale=Vector3.one*k;if(transform.parent)scale=Vector3.Scale(scale,new Vector3(1/transform.parent.lossyScale.x,1/transform.parent.lossyScale.y,1/transform.parent.lossyScale.z));
            if((transform.localScale-scale).sqrMagnitude>1e-10f)transform.localScale=scale;
        }
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var eyes=transform.TransformPoint(new Vector3(0,ModelEyeHeight,ModelEyeAhead));
            Gizmos.color=new Color(1,.35f,.3f);Gizmos.DrawSphere(eyes,.012f);
            UnityEditor.Handles.Label(eyes+Vector3.up*.12f,$"Eyes {EyesOverFloor:0.00} m over the floor ({Stature:0.00} m player)\nTable top {TableOverFloor:0.00} m over the floor");
        }
#endif
    }
}
