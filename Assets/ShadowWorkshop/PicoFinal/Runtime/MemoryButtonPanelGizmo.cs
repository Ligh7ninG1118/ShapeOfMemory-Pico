using UnityEngine;

namespace ShapeOfMemory
{
    // On the scene's play strip (MemoryGame.ButtonPanel): its outline in the Scene view, where the game builds HINT / HOLD TO
    // RESET / PAUSE / HOLD TO QUIT in a row while a memory is played (+y out of the panel toward the eyes, +z up the panel).
    public sealed class MemoryButtonPanelGizmo : MonoBehaviour
    {
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.matrix=transform.localToWorldMatrix;Gizmos.color=new Color(.9f,.5f,.4f,.9f);float cap=MemoryButton.Cap+.012f;
            Gizmos.DrawWireCube(new Vector3(0,.007f,0),new Vector3(MemoryGame.StripWidth,.014f,MemoryGame.StripDepth));
            for(int i=0;i<4;i++)Gizmos.DrawWireCube(new Vector3((i-1.5f)*MemoryGame.ButtonPitch,.016f,.015f),new Vector3(cap,.004f,cap));
            Gizmos.matrix=Matrix4x4.identity;
            UnityEditor.Handles.Label(transform.TransformPoint(new Vector3(0,.02f,MemoryGame.StripDepth*.5f+.03f)),"Play strip: HINT / RESET / PAUSE / QUIT");
        }
#endif
    }
}
