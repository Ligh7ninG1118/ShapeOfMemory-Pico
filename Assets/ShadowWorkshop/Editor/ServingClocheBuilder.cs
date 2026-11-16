using System.Collections.Generic;
using System.IO;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // A serving cloche in two parts: a round steel platter with a sloping rim, and a lid (a domed cover with a ball knob)
    // that lifts off it. Together 26 cm across and 13 cm tall. The prefab's root sits at the pair's centre with Platter
    // and Lid as children, each pivoted at its own bounding box's centre like the other pieces. One material
    // (kitchen steel), smooth normals doing the rounding (mobile VR): platter ~620 triangles, lid ~1.1k (it has an
    // inside, seen once it is lifted). Each part has a simpler closed twin with the same outline that casts its shadow
    // (ShadowPiece.shadowProxy in the gym, Torch2Piece.shadowMesh in levels), about 210 triangles for the pair.
    public static class ServingClocheBuilder
    {
        const string Folder="Assets/ShadowWorkshop/Art/Torch2/";
        public const string PlatterPath=Folder+"Meshes/Serving platter detailed.asset",PlatterShadowPath=Folder+"Meshes/Serving platter.asset";
        public const string LidPath=Folder+"Meshes/Serving cloche lid detailed.asset",LidShadowPath=Folder+"Meshes/Serving cloche lid.asset";
        public const string PrefabPath=Folder+"Prefabs/Serving cloche.prefab";
        // Heights are from the platter's underside, the lid standing on it.
        const float Top=.1295f;
        const float Floor=.0045f;                               // the platter's top face, where the lid stands
        const float Foot=Floor+.0002f;                          // the lid's underside, just clear of the floor
        const float DomeA=.100f,DomeH0=.0100f,DomeB=.0880f;     // lid: half ellipse of radius DomeA rising DomeB from DomeH0
        const float Wall=.0015f;                                // the lid's thickness
        const float KnobR=.012f,KnobY=Top-KnobR,StemR=.0058f;   // ball knob on a short stem

        [MenuItem("MetaHackthone/Shadow Workshop/Build serving cloche")]
        public static void Build()
        {
            // Same steel as the detailed fork (created only if missing, so tweaks made in Unity survive).
            var steel=PropMesh.Materials(Folder+"Materials/",new[]{new Look("kitchen steel",new Color(.86f,.87f,.89f),.75f,.86f)})[0];
            var platter=Part(Platter(),PlatterShadow(),PlatterPath,PlatterShadowPath,"Serving platter");
            var lid=Part(Lid(),LidShadow(),LidPath,LidShadowPath,"Serving cloche lid");
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var root=new GameObject("Serving cloche");
            try
            {
                foreach(var (name,part) in new[]{("Platter",platter),("Lid",lid)})
                {
                    var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=Vector3.up*(part.height-Top/2);
                    go.AddComponent<MeshFilter>().sharedMesh=part.mesh;go.AddComponent<MeshRenderer>().sharedMaterial=steel;
                    var tag=go.AddComponent<ShadowPiece>();tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=part.shadow;
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{Object.DestroyImmediate(root);}
            string Line(string name,(Mesh mesh,Mesh shadow,float height) p){var s=p.mesh.bounds.size*100;return $"{name} {p.mesh.triangles.Length/3} tris (shadow {p.shadow.triangles.Length/3}), {s.x:0.0} x {s.y:0.0} x {s.z:0.0} cm";}
            Debug.Log("SERVING_CLOCHE_OK "+Line("platter",platter)+"; "+Line("lid",lid));
        }

        // Lowers a part and its shadow by the part's half height so both pivot at its centre; returns where that centre
        // was (height above the platter's underside), for placing it in the prefab.
        static (Mesh mesh,Mesh shadow,float height) Part(PropMesh look,PropMesh shadow,string path,string shadowPath,string name)
        {
            float low=float.MaxValue,high=float.MinValue;foreach(var p in look.v){low=Mathf.Min(low,p.y);high=Mathf.Max(high,p.y);}
            float centre=(low+high)/2;
            foreach(var b in new[]{look,shadow})for(int i=0;i<b.v.Count;i++)b.v[i]-=Vector3.up*centre;
            return (look.Save(path,name+" detailed"),shadow.Save(shadowPath,name),centre);
        }

        // --- Platter: out under the flat foot, up the underside to a rounded 3.4 mm edge, in down the sloping rim and the
        // well's wall, and across the floor (a ring 6 mm in keeps the floor's normals level) to the centre.
        static PropMesh Platter()
        {
            var b=new PropMesh(1);
            var profile=RoundCorners(new[]{(V(0,0),0f,0),(V(.100f,0),0f,0),(V(.1295f,.0072f),.0016f,1),(V(.1295f,.0106f),.0016f,2),
                (V(.111f,.0080f),.004f,1),(V(.106f,Floor),.002f,1),(V(.0995f,Floor),0f,0),(V(0,Floor),0f,0)},false);
            b.Revolve(0,28,profile);
            return b;
        }
        static PropMesh PlatterShadow()
        {
            var b=new PropMesh(1);b.Revolve(0,12,new[]{V(0,0),V(.1295f,0),V(.1295f,.0106f),V(0,.0106f)});return b;
        }

        // --- Lid: down the inside from under the knob, round the beaded foot and up the outside to the knob's collar.
        static PropMesh Lid()
        {
            var b=new PropMesh(1);const int Steel=0;
            float innerA=DomeA-Wall,innerB=DomeB-Wall;
            var dome=new List<Vector2>();
            foreach(var deg in new[]{90f,65f,40f,18f,0f})dome.Add(Ellipse(innerA,innerB,deg));
            // Inside wall down to the underside (a crease), out under the foot, round the bead, a crease where it meets the dome.
            dome.Add(V(innerA,Foot));dome.Add(V(innerA,Foot));
            dome.AddRange(new[]{V(.1020f,Foot),V(.1034f,.0058f),V(.1030f,.0084f),V(DomeA,DomeH0)});
            foreach(var deg in new[]{0f,15f,30f,45f,60f,72f,82.5f})dome.Add(Ellipse(DomeA,DomeB,deg));
            b.Revolve(Steel,28,dome);
            // Knob: a collar seated in the lid's wall, a short stem, creased into a ball.
            var knob=new List<Vector2>{V(.0145f,.0960f),V(.0170f,.0980f),V(.0150f,.1002f),V(StemR,.1012f),V(StemR,.1012f)};
            float from=Mathf.Asin(StemR/KnobR)*Mathf.Rad2Deg;
            knob.Add(Ball(from));
            foreach(var deg in new[]{from,50f,75f,100f,125f,152f,180f})knob.Add(Ball(deg));
            b.Revolve(Steel,14,knob);
            return b;
        }
        // Closed (a flat underside), so it still casts with its open side to a lamp. 12 and 8 sided: the wall picture's
        // pixels are about 9 mm.
        static PropMesh LidShadow()
        {
            var b=new PropMesh(1);
            var dome=new List<Vector2>{V(0,Foot),V(.1034f,Foot)};
            foreach(var deg in new[]{0f,25f,50f,70f,90f})dome.Add(Ellipse(DomeA,DomeB,deg));
            b.Revolve(0,12,dome);
            b.Revolve(0,8,new[]{V(StemR,.0965f),Ball(Mathf.Asin(StemR/KnobR)*Mathf.Rad2Deg),Ball(60),Ball(120),Ball(180)});
            return b;
        }

        // Point on the lid's half ellipse (radius a, rise c), deg up from its foot.
        static Vector2 Ellipse(float a,float c,float deg){float p=deg*Mathf.Deg2Rad;return V(a*Mathf.Max(0,Mathf.Cos(p)),DomeH0+c*Mathf.Sin(p));}
        // Point on the knob's ball, deg up from its bottom.
        static Vector2 Ball(float deg){float p=deg*Mathf.Deg2Rad;return V(KnobR*Mathf.Max(0,Mathf.Sin(p)),KnobY-KnobR*Mathf.Cos(p));}
        static Vector2 V(float r,float h)=>new Vector2(r,h);
    }
}
