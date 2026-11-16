using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed looks for GoodMorning's cookware: the frying pan, the spatula (a turner) and the ladle. Each keeps its
    // original's pivot, size and outline and is only drawn: the original mesh stays the piece's shadow
    // (ShadowPiece.shadowProxy in the gym, Torch2Piece.shadowMesh in levels), so the puzzle's picture and scoring are
    // unchanged. Mobile VR budget: a few hundred triangles each, smooth normals doing the rounding. They keep their
    // originals' materials, in the same submesh order.
    public static class CookwareBuilder
    {
        const string Meshes="Assets/ShadowWorkshop/Art/Torch2/Meshes/",MaterialFolder="Assets/ShadowWorkshop/Art/Torch2/Materials/";
        // The spatula is the exception to "the original stays the shadow": its blade is slotted, and the slots show in the
        // shadow too (the picture's chef's hat gets its pleats), so its shadow is 'Spatula slotted', the original turner
        // with the same slots cut through its blade.
        public sealed class Prop{public string mesh,shadow;public string[] materials;}
        public static readonly Prop[] Props={
            new Prop{mesh=Meshes+"Frying pan detailed.asset",shadow=Meshes+"Frying pan.asset",materials=new[]{"pan","pan handle"}},
            new Prop{mesh=Meshes+"Spatula detailed.asset",shadow=Meshes+"Spatula slotted.asset",materials=new[]{"utensil","utensil black"}},
            new Prop{mesh=Meshes+"Ladle detailed.asset",shadow=Meshes+"Ladle.asset",materials=new[]{"utensil"}}};
        const string SpatulaOriginal=Meshes+"Spatula.asset";
        public static Material[] Materials(Prop p)=>System.Array.ConvertAll(p.materials,name=>AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+name+".mat"));

        [MenuItem("MetaHackthone/Shadow Workshop/Build cookware")]
        public static void Build()
        {
            var meshes=new[]{FryingPan(),Spatula(),SpatulaShadow(),Ladle(),SpatulaSlots()};
            Debug.Log("COOKWARE_OK "+string.Join(", ",System.Array.ConvertAll(meshes,m=>$"{m.name} {m.triangles.Length/3} tris")));
        }

        // --- Frying pan: bowl round (-72.5, 0) mm, 210 mm across the rim, 35 mm tall; handle along +x to 177.5 mm -------
        static Mesh FryingPan()
        {
            var b=new PropMesh(2);const int Pan=0,Handle=1;
            // Out from the bottom centre, up the flared wall, over the rounded lip and down the inside to the floor's centre.
            var profile=RoundCorners(new[]{(new Vector2(0,-.0195f),0f,0),(new Vector2(.0950f,-.0195f),.0090f,2),(new Vector2(.1050f,.0155f),.0028f,1),
                (new Vector2(.0990f,.0155f),.0028f,1),(new Vector2(.0900f,-.0135f),.0080f,2),(new Vector2(0,-.0135f),0f,0)},false);
            b.SmoothLathe(Pan,new Vector3(-.0725f,0,0),Vector3.up,32,profile);
            // Steel collar where the handle meets the wall (its back buried in the wall); the grip comes out of its end face.
            var axis=new Vector3(0,.0105f,0);
            b.SmoothLathe(Pan,axis,Vector3.right,10,new[]{new Vector2(0,.0275f),new Vector2(.0098f,.0275f),new Vector2(.0106f,.0292f),
                new Vector2(.0106f,.0425f),new Vector2(.0096f,.0452f),new Vector2(.0060f,.0456f)});
            // Wooden grip: oval, a little waisted, swelling to a domed end at the original's 177.5 mm. (Caps about as long as
            // the section is wide come out round; longer ones come out pointed.)
            var grip=new[]{new Vector3(.040f,.0105f,0),new Vector3(.075f,.0105f,0),new Vector3(.120f,.0105f,0),new Vector3(.169f,.0105f,0)};
            b.Bar(Handle,grip,Vector3.forward,t=>{float s=Mathf.Lerp(.92f,1.1f,Mathf.SmoothStep(0,1,t))-.06f*Mathf.Sin(t*Mathf.PI);return Ellipse(.0085f*s,.0076f*s,10);},0,.0085f,3);
            return b.Save(Props[0].mesh,"Frying pan detailed");
        }

        // --- Spatula: a turner, blade at -y (65 x 75 mm), black handle up to +106 mm --------------------------------------
        // Four thin slots through the blade, along its length (the pleats of the chef's hat in the shadow); shared by the
        // look and its shadow so they line up.
        static readonly float[] SlotX={-.0180f,-.0060f,.0060f,.0180f};
        const float SlotBottom=-.0985f,SlotTop=-.0465f,SlotHalf=.0014f,Front=-.1063f;
        static List<Vector2[]> Slots(int steps)=>new List<Vector2[]>(System.Array.ConvertAll(SlotX,x=>Stadium(x,SlotBottom,SlotTop,SlotHalf,steps)));
        static Mesh Spatula()
        {
            var b=new PropMesh(2);const int Steel=0,Grip=1;
            // Blade: a plate, wider at the front, rounded corners and edges, slotted. The front edge has a
            // point under each slot, where the caps' cut to it runs.
            var corners=new List<(Vector2,float,int)>{(new Vector2(-.0325f,Front),.006f,3)};
            foreach(var x in SlotX)corners.Add((new Vector2(x,Front),0f,0));
            corners.Add((new Vector2(.0325f,Front),.006f,3));corners.Add((new Vector2(.0300f,-.0390f),.012f,3));corners.Add((new Vector2(-.0300f,-.0390f),.012f,3));
            Plate(b,Steel,RoundCorners(corners,true),Slots(4),.0009f,.0007f,1);
            // Shank: a flat bar from the blade's top edge (standing just proud of it), narrowing and thickening into the handle.
            b.Bar(Steel,new[]{new Vector3(0,-.0440f,0),new Vector3(0,-.0335f,0),new Vector3(0,-.0230f,0)},Vector3.right,
                t=>{float w=Mathf.Lerp(.0080f,.0036f,t),h=Mathf.Lerp(.0013f,.0024f,t);return RoundedRect(-w,w,-h,h,h*.8f,1);},0,0);
            // Handle: oval, swelling a little towards the end, rounded at both ends.
            b.Bar(Grip,new[]{new Vector3(0,-.0255f,0),new Vector3(0,.0050f,0),new Vector3(0,.0450f,0),new Vector3(0,.0830f,0),new Vector3(0,.0975f,0)},Vector3.right,
                t=>{float s=1+.10f*Mathf.Sin(t*Mathf.PI*.9f);return Ellipse(.0060f*s,.0046f*s,10);},.0045f,.0070f,3);
            return b.Save(Props[1].mesh,"Spatula detailed");
        }
        // The spatula's shadow: the original turner (its square 65 x 75 x 4 mm blade, its handle copied as it is) with the
        // look's slots cut through the blade.
        static Mesh SpatulaShadow()
        {
            var b=new PropMesh(1);
            var corners=new List<Vector2>{new Vector2(-.0325f,Front)};foreach(var x in SlotX)corners.Add(new Vector2(x,Front));
            corners.Add(new Vector2(.0325f,Front));corners.Add(new Vector2(.0325f,-.0313f));corners.Add(new Vector2(-.0325f,-.0313f));
            Plate(b,0,corners,Slots(4),.002f,0,0);
            var original=AssetDatabase.LoadAssetAtPath<Mesh>(SpatulaOriginal);var v=original.vertices;var n=original.normals;var handle=original.GetTriangles(1);
            var index=new Dictionary<int,int>();
            for(int k=0;k<handle.Length;k+=3)
            {
                for(int j=0;j<3;j++)if(!index.ContainsKey(handle[k+j]))index[handle[k+j]]=b.Add(v[handle[k+j]],n[handle[k+j]],Vector2.zero);
                b.Tri(0,index[handle[k]],index[handle[k+1]],index[handle[k+2]]);
            }
            return b.Save(Props[1].shadow,"Spatula slotted");
        }

        // --- Three-slot spatula: the original turner's handle (copied as it is) on a 4 mm blade 75 mm long, 69 mm across
        // its front edge (cut at a slant, the right corner 7 mm shorter) and 61 mm across its back, where two concave
        // shoulders run into the handle. Three slots fan along it, each tapering from 7 mm at the front end to 3 mm at the
        // handle end. Two submeshes like the original (steel blade, black handle); readable, so it casts its own
        // shadow, slots and all.
        public const string SpatulaSlotsPath=Meshes+"Spatula three slots.asset";
        const float BladeBack=-.0313f,FrontSlant=.0070f,SlotFrontR=.0035f,SlotBackR=.0015f,SlotBackY=-.0420f;
        static readonly Vector2 TipLeft=new Vector2(-.0330f,Front),TipRight=new Vector2(.0360f,Front+FrontSlant);
        // The front edge's height at x, and each slot's centre at its front and back ends.
        static float FrontAt(float x)=>Mathf.Lerp(TipLeft.y,TipRight.y,(x-TipLeft.x)/(TipRight.x-TipLeft.x));
        static float SlotFrontX(int i)=>(i-1)*.0195f+.0015f;
        static float SlotBackX(int i)=>(i-1)*.0165f;
        [MenuItem("MetaHackthone/Shadow Workshop/Build three-slot spatula")]
        static void BuildSpatulaSlots(){var m=SpatulaSlots();Debug.Log($"SPATULA_SLOTS_OK {m.triangles.Length/3} tris");}
        public static Mesh SpatulaSlots()
        {
            var b=new PropMesh(2);const int Steel=0,Grip=1;
            // Round the front corners, along the slanted front edge (a point under each slot's lowest point, for Plate's
            // cuts), up the right side, along the back to a concave shoulder into the neck (hidden in the handle) and back.
            var corners=new List<(Vector2,float,int)>{(TipLeft,.004f,2)};
            for(int i=0;i<3;i++)corners.Add((new Vector2(SlotFrontX(i),FrontAt(SlotFrontX(i))),0f,0));
            corners.Add((TipRight,.004f,2));corners.Add((new Vector2(.0300f,BladeBack),.006f,2));
            // The shoulder: a 9 mm quarter circle from the back edge up into the 12 mm neck; mirrored on the left.
            var shoulder=new List<Vector2>();
            for(int k=0;k<=4;k++){float a=(-90-k*22.5f)*Mathf.Deg2Rad;shoulder.Add(new Vector2(.0150f+.0090f*Mathf.Cos(a),-.0223f+.0090f*Mathf.Sin(a)));}
            shoulder.Add(new Vector2(.0060f,-.0180f));
            foreach(var p in shoulder)corners.Add((p,0f,0));
            for(int k=shoulder.Count-1;k>=0;k--)corners.Add((new Vector2(-shoulder[k].x,shoulder[k].y),0f,0));
            corners.Add((new Vector2(-.0310f,BladeBack),.006f,2));
            var outline=RoundCorners(corners,true);
            var slots=new List<Vector2[]>();
            for(int i=0;i<3;i++)
            {
                var slot=new List<Vector2>();float yFront=FrontAt(SlotFrontX(i))+.0160f;
                var cf=new Vector2(SlotFrontX(i),yFront+SlotFrontR);var cb=new Vector2(SlotBackX(i),SlotBackY-SlotBackR);
                for(int k=0;k<=4;k++){float a=-k*Mathf.PI/4;slot.Add(cf+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*SlotFrontR);}
                for(int k=0;k<=4;k++){float a=Mathf.PI-k*Mathf.PI/4;slot.Add(cb+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*SlotBackR);}
                slots.Add(slot.ToArray());
            }
            Plate(b,Steel,outline,slots,.002f,0,0);
            var original=AssetDatabase.LoadAssetAtPath<Mesh>(SpatulaOriginal);var v=original.vertices;var n=original.normals;var uv=original.uv;var handle=original.GetTriangles(1);
            var index=new Dictionary<int,int>();
            foreach(int i in handle)if(!index.ContainsKey(i))index[i]=b.Add(v[i],n[i],uv.Length>0?uv[i]:Vector2.zero);
            for(int k=0;k<handle.Length;k+=3)b.Tri(Grip,index[handle[k]],index[handle[k+1]],index[handle[k+2]]);
            return b.Save(SpatulaSlotsPath,"Spatula three slots");
        }

        // --- Plates with holes -----------------------------------------------------------------------------------------
        // A flat plate along z: an outer outline and holes (any windings), half thickness half, every edge rounded by r
        // (arcSteps per quarter; r = 0 leaves square edges). Each hole joins the outline by a cut from its lowest point to
        // the outline point most nearly below it (put one straight below each hole), and the caps are ear-clipped.
        static void Plate(PropMesh b,int m,IList<Vector2> outer,IList<Vector2[]> holes,float half,float r,int arcSteps)
        {
            var cap=Edge(b,m,outer,false,half,r,arcSteps);if(Area(cap)<0)cap.Reverse();
            foreach(var hole in holes)
            {
                var h=Edge(b,m,hole,true,half,r,arcSteps);if(Area(h)>0)h.Reverse();
                int iH=0;for(int i=1;i<h.Count;i++)if(h[i].y<h[iH].y)iH=i;var at=h[iH];
                float Cost(Vector2 p)=>Mathf.Abs(p.x-at.x)+.01f*(at.y-p.y);
                int iO=-1;for(int i=0;i<cap.Count;i++)if(cap[i].y<at.y&&(iO<0||Cost(cap[i])<Cost(cap[iO])))iO=i;
                var merged=new List<Vector2>();
                for(int i=0;i<=iO;i++)merged.Add(cap[i]);
                for(int k=0;k<=h.Count;k++)merged.Add(h[(iH+k)%h.Count]);
                for(int i=iO;i<cap.Count;i++)merged.Add(cap[i]);
                cap=merged;
            }
            var tris=Triangulate(cap.ToArray());
            foreach(var sign in new[]{1f,-1f})
            {
                int start=b.v.Count;foreach(var p in cap)b.Add(new Vector3(p.x,p.y,sign*half),Vector3.forward*sign,p*25);
                for(int k=0;k<tris.Count;k+=3)b.Tri(m,start+tris[k],start+tris[k+1],start+tris[k+2]);
            }
        }
        // The edge round one loop of a plate (for a hole, facing into it); returns the loop pulled in by r, where the caps end.
        static List<Vector2> Edge(PropMesh b,int m,IList<Vector2> loop,bool hole,float half,float r,int arcSteps)
        {
            int count=loop.Count;float sign=(Area(loop)>0?1:-1)*(hole?-1:1);var inner=new List<Vector2>();var normal=new Vector3[count];
            for(int i=0;i<count;i++){var t=(loop[(i+1)%count]-loop[(i+count-1)%count]).normalized;normal[i]=new Vector3(t.y,-t.x,0)*sign;inner.Add(loop[i]-(Vector2)normal[i]*r);}
            int start=b.v.Count,rows=r>0?2*(arcSteps+1):2;
            for(int i=0;i<count;i++)
            {
                if(r<=0){b.Add(new Vector3(loop[i].x,loop[i].y,half),normal[i],Vector2.zero);b.Add(new Vector3(loop[i].x,loop[i].y,-half),normal[i],Vector2.zero);continue;}
                for(int s=0;s<rows;s++)
                {
                    bool top=s<=arcSteps;float phi=(top?90f*(1-(float)s/arcSteps):-90f*(s-arcSteps-1)/arcSteps)*Mathf.Deg2Rad;
                    var nn=normal[i]*Mathf.Cos(phi)+Vector3.forward*Mathf.Sin(phi);
                    b.Add(new Vector3(inner[i].x,inner[i].y,top?half-r:-half+r)+nn*r,nn,Vector2.zero);
                }
            }
            for(int i=0;i<count;i++){int j=(i+1)%count;for(int s=0;s+1<rows;s++){int q=start+i*rows+s,w=start+j*rows+s;b.Quad(m,q,w,w+1,q+1);}}
            return inner;
        }
        static float Area(IList<Vector2> p){float a=0;for(int i=0;i<p.Count;i++){var u=p[i];var w=p[(i+1)%p.Count];a+=u.x*w.y-w.x*u.y;}return a;}
        // A slot with round ends between heights y0 and y1, centred on x; steps (even) per end, so its lowest point is on x.
        static Vector2[] Stadium(float x,float y0,float y1,float half,int steps)
        {
            var list=new List<Vector2>();
            for(int k=0;k<=steps;k++){float a=Mathf.PI*k/steps;list.Add(new Vector2(x+half*Mathf.Cos(a),y1-half+half*Mathf.Sin(a)));}
            for(int k=0;k<=steps;k++){float a=Mathf.PI*(1+(float)k/steps);list.Add(new Vector2(x+half*Mathf.Cos(a),y0+half+half*Mathf.Sin(a)));}
            return list.ToArray();
        }

        // --- Ladle: bowl round (-24.5, -81.9) mm, 70 mm across and 21 mm deep, opening up; handle from its +x rim up to
        // y 102 mm. The original's bowl is a closed lens (its shadow); this one is open, as a ladle is.
        static Mesh Ladle()
        {
            var b=new PropMesh(1);const int Steel=0;
            const float a=.0350f,depth=.0210f,wall=.0014f;var center=new Vector3(-.0245f,-.0819f,0);
            // Up the outside from the bottom, over the rolled lip, down the inside.
            var profile=new List<Vector2>();
            foreach(var deg in new[]{-90f,-64f,-40f,-19f,0f})profile.Add(new Vector2(a*Mathf.Cos(deg*Mathf.Deg2Rad),depth*Mathf.Sin(deg*Mathf.Deg2Rad)));
            profile.Add(new Vector2(a-wall/2,wall/2));
            foreach(var deg in new[]{0f,-24f,-48f,-70f,-90f})profile.Add(new Vector2((a-wall)*Mathf.Cos(deg*Mathf.Deg2Rad),(depth-wall)*Mathf.Sin(deg*Mathf.Deg2Rad)));
            b.SmoothLathe(Steel,center,Vector3.up,20,profile);
            // Handle: a flat strip (its width across z) from just under the rim up, widening towards its rounded end.
            var path=Curve(new[]{new Vector3(.0112f,-.0835f,0),new Vector3(.0130f,-.0770f,0),new Vector3(.0175f,-.0620f,0),
                new Vector3(.0290f,-.0100f,0),new Vector3(.0440f,.0550f,0),new Vector3(.0532f,.0942f,0)},2);
            b.Bar(Steel,path,Vector3.forward,t=>{float w=t<.7f?Mathf.Lerp(.0040f,.0046f,t/.7f):Mathf.Lerp(.0046f,.0070f,Mathf.SmoothStep(0,1,(t-.7f)/.3f));
                return RoundedRect(-w,w,-.0017f,.0017f,.0013f,1);},.0025f,.0070f,3);
            return b.Save(Props[2].mesh,"Ladle detailed");
        }
    }
}
