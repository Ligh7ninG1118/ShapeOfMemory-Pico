using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed looks for Birthday's candles, party hat and balloon (build_torch2.py's birthday_candle, party_hat and
    // balloon, 8 to 14 sided). Each keeps its original's pivot, size and outline and is only drawn: the original mesh
    // stays the piece's shadow (ShadowPiece.shadowProxy in the gym, Torch2Piece.shadowMesh in levels), so the puzzle's
    // picture and scoring and the shadow redrawn every frame on Quest are unchanged. Mobile VR budget: a few hundred
    // triangles each (candle ~330, hat ~390, balloon ~1k), smooth normals doing the rounding. Same materials in the
    // same submesh order as the originals.
    public static class PartyPropsBuilder
    {
        const string Meshes="Assets/ShadowWorkshop/Art/Torch2/Meshes/";
        public sealed class Prop{public string mesh,shadow;}
        public static readonly Prop[] Props={
            new Prop{mesh=Meshes+"Birthday candle detailed.asset",shadow=Meshes+"Birthday candle.asset"},
            new Prop{mesh=Meshes+"Party hat detailed.asset",shadow=Meshes+"Party hat.asset"},
            new Prop{mesh=Meshes+"Balloon detailed.asset",shadow=Meshes+"Balloon.asset"}};

        [MenuItem("MetaHackthone/Shadow Workshop/Build party props")]
        public static void Build()
        {
            var meshes=new[]{Candle(),PartyHat(),Balloon()};
            Debug.Log("PARTY_PROPS_OK "+string.Join(", ",System.Array.ConvertAll(meshes,m=>$"{m.name} {m.triangles.Length/3} tris")));
        }

        // --- Birthday candle: body r 6.5 mm from y -52 to +18 mm, flame from +19 to +52 mm (9 mm across at most) ---------
        static Mesh Candle()
        {
            var b=new PropMesh(2);const int Wax=0,Flame=1;
            // Flat ends with a 1 mm chamfer each, shaded round; the rings 1 mm further in keep the long side's normals level.
            b.Revolve(Wax,16,new[]{V(0,-.052f),V(.0055f,-.052f),V(.0065f,-.051f),V(.0065f,-.050f),V(.0065f,.016f),V(.0065f,.017f),V(.0055f,.018f),V(0,.018f)});
            // A teardrop: round at the bottom just over the wax, widest low down, drawn out to a point.
            b.Revolve(Flame,10,new[]{V(0,.0192f),V(.0048f,.0203f),V(.0078f,.0232f),V(.0090f,.0275f),V(.0084f,.0325f),V(.0066f,.0380f),V(.0040f,.0440f),V(.0016f,.0490f),V(0,.0520f)});
            return b.Save(Props[0].mesh,"Birthday candle detailed");
        }

        // --- Party hat: cone 90 mm across at y -62 mm, tip at +48 mm under a 12 mm pompom round (0, 50) mm ---------------
        // Hollow, as a paper hat is: a 1.2 mm wall and a crisp rim. The outside stops inside the pompom.
        static Mesh PartyHat()
        {
            var b=new PropMesh(2);const int Paper=0,Pompom=1;
            const float baseY=-.062f,tipY=.048f,radius=.045f,wall=.0012f,stop=.040f;
            float slope=radius/(tipY-baseY),innerTip=tipY-wall*Mathf.Sqrt(1+slope*slope)/slope,innerRadius=slope*(innerTip-baseY);
            // Down the inside from its tip, across the rim (a repeated point is a crease) and up the outside.
            b.Revolve(Paper,28,new[]{V(0,innerTip),V(innerRadius,baseY),V(innerRadius,baseY),V(radius,baseY),V(radius,baseY),V(slope*(tipY-stop),stop)});
            var (at,normal)=Spheroid(.012f,.012f,.050f,10);b.Revolve(Pompom,14,at,normal);
            return b.Save(Props[1].mesh,"Party hat detailed");
        }

        // --- Balloon: a spheroid 90 mm across and 106 mm tall round (0, 48.6) mm, knot at its foot, string hanging ~90 mm --
        static Mesh Balloon()
        {
            var b=new PropMesh(2);const int Rubber=0,Cord=1;
            var (at,normal)=Spheroid(.045f,.0531f,.04862f,16);b.Revolve(Rubber,24,at,normal);
            // Knot: from under the rolled lip, round it, in to the neck and up into the body (the open end hidden inside).
            b.Revolve(Rubber,10,new[]{V(0,-.0138f),V(.0040f,-.0136f),V(.0064f,-.0124f),V(.0066f,-.0106f),V(.0048f,-.0094f),V(.0030f,-.0088f),V(.0036f,-.0066f),V(.0050f,-.0035f)});
            // String: the original's four points smoothed, its top buried in the knot, a pointed end at the bottom.
            var path=Curve(new[]{new Vector3(0,-.01138f,0),new Vector3(.006f,-.04138f,0),new Vector3(-.004f,-.07138f,0),new Vector3(.003f,-.10138f,0)},4);
            b.Bar(Cord,path,Vector3.forward,t=>Ellipse(.0011f,.0011f,6),0,.0011f,1);
            return b.Save(Props[2].mesh,"Balloon detailed");
        }

        static Vector2 V(float r,float h)=>new Vector2(r,h);
    }
}
