using UnityEngine;

namespace ShapeOfMemory
{
    // On the scene's play area (MemoryGame.PlayArea, its origin the middle of the table top, +z away from the player).
    // Draws in the Scene view what the game puts there at runtime and the scene doesn't hold, so the table, the stand and
    // the book can be laid out against it:
    //   the player's eyes (MemoryGame.StageBelow over and StageAhead behind the origin: the game places the play area from
    //   them in a headset), the orb's middle (Torch2Projection.Home) with its rings' reach, the book standing open on the
    //   lamps' walls during a memory (MemoryBook.Walls), and the HINT / HOLD TO RESET / MENU panel.
    // Nothing at runtime.
    public sealed class MemoryPlayArea : MonoBehaviour
    {
        [Tooltip("Scene view: draw the eyes, the orb, the book on the walls and the button panel.")]
        public bool ShowGuides=true;
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if(!ShowGuides)return;
            Gizmos.matrix=transform.localToWorldMatrix;
            // The eyes, and their line to the orb.
            var eyes=MemorySeatedPlayer.Eyes;var orb=CloudGarden.ShadowPuzzle.Torch2Projection.Home;
            Gizmos.color=new Color(1,.35f,.3f,.9f);Gizmos.DrawWireSphere(eyes,.03f);Gizmos.DrawLine(eyes,orb);
            // The orb: its middle, and the rings at their drawn size (about 30 cm from the eyes).
            Gizmos.color=new Color(.35f,.85f,.85f,.9f);Gizmos.DrawSphere(orb,.012f);
            float rings=Vector3.Distance(eyes,orb)-.30f;Gizmos.DrawWireSphere(orb,rings);
            // The book on the walls: its two pages standing open at a right angle, full size.
            Gizmos.color=new Color(.95f,.85f,.55f,.8f);
            var walls=MemoryBook.Walls;var shape=new BookShape(Vector3.zero,Vector3.right,Vector3.up,Vector3.forward,90,0);
            for(int k=0;k<2;k++)
            {
                var page=shape.Page(k==1);Vector3 P(float u,float v)=>walls.position+walls.rotation*((page.position+page.rotation*new Vector3(u,v,0))*walls.scale);
                float w=MemoryBook.Width,h=MemoryBook.Height;
                Gizmos.DrawLine(P(0,0),P(w,0));Gizmos.DrawLine(P(w,0),P(w,h));Gizmos.DrawLine(P(w,h),P(0,h));Gizmos.DrawLine(P(0,h),P(0,0));
            }
            // The play strip where the game would build it without the scene's (MemoryGame.StripAt).
            var game=FindFirstObjectByType<MemoryGame>();
            Gizmos.color=new Color(.9f,.5f,.4f,.8f);var m=Gizmos.matrix;
            if(!game||!game.ButtonPanel){Gizmos.matrix=m*Matrix4x4.TRS(MemoryGame.StripAt,MemoryGame.StripFacing,Vector3.one);Gizmos.DrawWireCube(Vector3.zero,new Vector3(MemoryGame.StripWidth,.014f,MemoryGame.StripDepth));}
            Gizmos.matrix=m;
            Gizmos.matrix=Matrix4x4.identity;
            UnityEditor.Handles.color=Color.white;
            UnityEditor.Handles.Label(transform.TransformPoint(orb+Vector3.up*(rings+.03f)),"Orb (a memory's things, in its rings)");
            UnityEditor.Handles.Label(transform.TransformPoint(walls.position+Vector3.up*(MemoryBook.Height+.05f)),"The book on the lamps' walls (during a memory)");
        }
#endif
    }
}
