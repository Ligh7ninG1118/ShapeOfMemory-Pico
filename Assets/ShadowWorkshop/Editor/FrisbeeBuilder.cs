using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // A dog's frisbee for mobile VR: 20 cm across and 2.7 cm tall at scale 1, pivot at the bottom centre like the
    // CozyShops plate it was made to replace (same footprint, so it drops into the plate's place). A gently domed top
    // with a white flight ring, a rolled rim and a hollow underside, 24-sided with smooth normals: about 480 triangles,
    // two materials. A closed 12-sided twin (72 triangles) casts its shadow (ShadowPiece.shadowProxy in the gym,
    // Torch2Piece.shadowMesh in levels).
    public static class FrisbeeBuilder
    {
        const string Folder="Assets/ShadowWorkshop/Art/Torch2/";
        public const string MeshPath=Folder+"Meshes/Frisbee detailed.asset",ShadowPath=Folder+"Meshes/Frisbee.asset";
        const int Plastic=0,Ring=1;
        static readonly Look[] Looks={new Look("frisbee orange",new Color(.98f,.42f,.10f),0,.55f),new Look("frisbee white",new Color(.96f,.95f,.92f),0,.55f)};

        [MenuItem("MetaHackthone/Shadow Workshop/Build frisbee")]
        public static void Build()
        {
            var (mesh,shadow)=BuildMeshes();PropMesh.Materials(Folder+"Materials/",Looks);
            Debug.Log($"FRISBEE_OK {mesh.triangles.Length/3} tris (shadow {shadow.triangles.Length/3}), {mesh.bounds.size*100} cm");
        }
        // Turns the selected object into the frisbee in place (undoable): its mesh, materials and shadow.
        [MenuItem("MetaHackthone/Shadow Workshop/Make selected a frisbee")]
        static void MakeSelected()=>Apply(Selection.activeTransform);
        [MenuItem("MetaHackthone/Shadow Workshop/Make selected a frisbee",true)]
        static bool CanMake()=>Selection.activeTransform&&Selection.activeTransform.GetComponent<MeshFilter>();

        public static void Apply(Transform target)
        {
            var (mesh,shadow)=BuildMeshes();var materials=PropMesh.Materials(Folder+"Materials/",Looks);
            var filter=target.GetComponent<MeshFilter>();var renderer=target.GetComponent<MeshRenderer>();
            Undo.RecordObjects(new Object[]{target.gameObject,filter,renderer},"Make frisbee");
            target.name="Frisbee";filter.sharedMesh=mesh;renderer.sharedMaterials=materials;
            var tag=target.GetComponent<ShadowPiece>();if(!tag)tag=Undo.AddComponent<ShadowPiece>(target.gameObject);else Undo.RecordObject(tag,"Make frisbee");
            tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=shadow;
            EditorUtility.SetDirty(target.gameObject);
        }

        static (Mesh mesh,Mesh shadow) BuildMeshes()
        {
            var b=new PropMesh(2);
            // Out under the top from the centre, down the inside of the rim, round the lip (its lowest point at y 0), up
            // the outside and in over the top to the white ring; then the ring, then the top's middle to the centre.
            b.Revolve(Plastic,24,new[]{V(0,.0248f),V(.0860f,.0215f),V(.0900f,.0050f),V(.0925f,0),V(.0968f,.0012f),
                V(.1000f,.0110f),V(.0995f,.0168f),V(.0945f,.0213f),V(.0860f,.0240f),V(.0730f,.0255f)});
            b.Revolve(Ring,24,new[]{V(.0730f,.0255f),V(.0640f,.0260f)});
            b.Revolve(Plastic,24,new[]{V(.0640f,.0260f),V(0,.0270f)});
            var s=new PropMesh(1);
            s.Revolve(0,12,new[]{V(0,0),V(.0925f,0),V(.1000f,.0110f),V(.0945f,.0213f),V(0,.0270f)});
            return (b.Save(MeshPath,"Frisbee detailed"),s.Save(ShadowPath,"Frisbee"));
        }

        static Vector2 V(float r,float h)=>new Vector2(r,h);
    }
}
