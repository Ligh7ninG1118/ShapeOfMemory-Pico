using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Smoother look for the pocket compass (Free Low Poly Game Assets' Compass: 16-sided and flat-shaded, 678
    // triangles): the case standing on its edge with the dial facing +z, raised bezel, cream dial in a lilac ring, red/blue
    // diamond needle with a pin, and a knob on top, in the same frame and outline, now 24-sided with smooth normals (about
    // 660 triangles, mobile VR). All light blue: the case and the knob's cap, and a softer light blue for the bezel's inner
    // ring, the knob's stem and the pin. Coloured from the same palette texture (UVs on its swatches), so it keeps its
    // material (Color_1). Drawn only: the original stays the piece's shadow
    // (ShadowPiece.shadowProxy in the gym, Torch2Piece.shadowMesh in levels), so the puzzle is unchanged.
    public static class CompassBuilder
    {
        public const string MeshPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Compass detailed.asset";
        public const string OriginalPath="Assets/ShadowWorkshop/Art/Torch2/Free Low Poly Game Assets/Models/Compass.fbx";
        const int Sides=24;
        // Swatches on Color_1 (cell centres): the case's light blue (#7CC7FF), a softer light blue for the trim (#88BAE0:
        // bezel ring, knob stem, pin), dial cream, the dial's lilac ring, and the needle's red and blue halves.
        static readonly Vector2 CaseBlue=new Vector2(.09375f,.15625f),TrimBlue=new Vector2(.96875f,.71875f),Cream=new Vector2(.350f,.021f),
            Lilac=new Vector2(.290f,.790f),Red=new Vector2(.660f,.707f),Blue=new Vector2(.153f,.900f);
        static readonly Vector3 Centre=new Vector3(0,.0392f,0);   // the case's centre; its axis is z
        const float Dial=.0044f;                                   // the dial's height in front of the centre

        [MenuItem("MetaHackthone/Shadow Workshop/Build compass")]
        public static void Build(){var m=BuildMesh();Debug.Log($"COMPASS_OK {m.triangles.Length/3} tris, {m.bounds.size*1000} mm");}
        public static Mesh Original=>AssetDatabase.LoadAssetAtPath<Mesh>(OriginalPath);

        public static Mesh BuildMesh()
        {
            var b=new PropMesh(1);
            // Case: from the middle of the back out, round the edge, over the bezel and down its inside to the dial.
            Paint(b,CaseBlue,()=>b.SmoothLathe(0,Centre,Vector3.forward,Sides,new[]{V(0,-.0109f),V(.0355f,-.0109f),V(.0392f,-.0050f),V(.0392f,.0050f),
                V(.0355f,.0109f),V(.0300f,.0109f),V(.0288f,.0060f)}));
            Paint(b,TrimBlue,()=>b.SmoothLathe(0,Centre,Vector3.forward,Sides,new[]{V(.0288f,.0060f),V(.0288f,Dial),V(.0264f,Dial)},1));
            Paint(b,Lilac,()=>b.SmoothLathe(0,Centre,Vector3.forward,Sides,new[]{V(.0264f,Dial),V(.0243f,Dial)}));
            Paint(b,Cream,()=>b.SmoothLathe(0,Centre,Vector3.forward,Sides,new[]{V(.0243f,Dial),V(0,Dial)}));
            // Needle: a diamond standing just off the dial with a ridge down its middle, red to the north, blue south.
            Half(b,Red,1);Half(b,Blue,-1);
            Paint(b,TrimBlue,()=>b.SmoothLathe(0,Centre+Vector3.forward*.0050f,Vector3.forward,12,new[]{V(.0045f,-.0008f),V(.0045f,.0022f),V(.0030f,.0031f),V(0,.0033f)}));
            // Knob: a stem out of the case's top and a round cap.
            Paint(b,TrimBlue,()=>b.SmoothLathe(0,Vector3.zero,Vector3.up,16,new[]{V(.0082f,.0760f),V(.0082f,.0823f)}));
            Paint(b,CaseBlue,()=>b.SmoothLathe(0,Vector3.zero,Vector3.up,16,new[]{V(.0082f,.0823f),V(.0122f,.0827f),V(.0122f,.0872f),V(.0108f,.0889f),V(0,.0889f)}));
            return b.Save(MeshPath,"Compass detailed");
        }

        // Runs add, then gives every vertex it made the swatch's UV.
        static void Paint(PropMesh b,Vector2 swatch,System.Action add){int start=b.v.Count;add();for(int i=start;i<b.v.Count;i++)b.uv[i]=swatch;}
        // Half the needle (dir 1 north, -1 south): its two sloping faces meeting on a ridge, and the two edges down
        // towards the dial. Flat-shaded, each face its own corners.
        static void Half(PropMesh b,Vector2 swatch,float dir)
        {
            const float Low=.0052f,High=.0072f,Width=.0100f,Edge=.0047f;float mid=.0388f,tip=mid+dir*.0167f;
            Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);
            var t=P(0,tip,Low);var c=P(0,mid,High);var l=P(-Width,mid,Low);var r=P(Width,mid,Low);
            void Face(Vector3 a,Vector3 bb,Vector3 cc,Vector3 out_)
            {
                var nn=Vector3.Cross(bb-a,cc-a).normalized;if(Vector3.Dot(nn,out_)<0)nn=-nn;
                b.Tri(0,b.Add(a,nn,swatch),b.Add(bb,nn,swatch),b.Add(cc,nn,swatch));
            }
            void Side(Vector3 a,Vector3 bb)
            {
                var o=Vector3.Cross(bb-a,Vector3.forward).normalized;if(Vector3.Dot(o,(a+bb)/2-P(0,mid,Low))<0)o=-o;
                b.Quad(0,b.Add(a,o,swatch),b.Add(bb,o,swatch),b.Add(new Vector3(bb.x,bb.y,Edge),o,swatch),b.Add(new Vector3(a.x,a.y,Edge),o,swatch));
            }
            Face(t,c,l,Vector3.forward);Face(t,r,c,Vector3.forward);Side(t,l);Side(t,r);
        }
        static Vector2 V(float r,float h)=>new Vector2(r,h);
    }
}
