using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed looks for Birthday's two presents ('Big gift box', 'Small gift box'): a box with softened edges under a
    // lid that overhangs it a little, two ribbons crossed over the lid and round the box (stepping in under the lid's
    // edge), and a bow where they cross (a knot, two loops pinched into it, two tails with notched ends). Drawn only: the
    // original boxes stay the pieces' shadows (ShadowPiece.shadowProxy in the gym, Torch2Piece.shadowMesh in levels), so
    // the cake picture and scoring are unchanged; the bow, about 15-18 mm proud of the lid, casts no shadow. Same frame and
    // outer size as the originals and the same two materials (the paper colour, then the ribbon). About 260 triangles
    // each (mobile VR): bevelled edges shaded round, the box's top (inside the lid) left out, ribbons drawn as their
    // outer face only, the bow's loops flat 4-sided strips and its tails flat cut-outs lying on the lid.
    public static class GiftBuilder
    {
        const string Meshes="Assets/ShadowWorkshop/Art/Torch2/Meshes/";
        public sealed class Prop{public string name,mesh,shadow;public Vector3 half;public float bow;}
        public static readonly Prop[] Props={
            new Prop{name="Big gift box detailed",mesh=Meshes+"Big gift box detailed.asset",shadow=Meshes+"Big gift box.asset",half=new Vector3(.100f,.040f,.070f),bow=1.35f},
            new Prop{name="Small gift box detailed",mesh=Meshes+"Small gift box detailed.asset",shadow=Meshes+"Small gift box.asset",half=new Vector3(.075f,.035f,.050f),bow=1.1f}};
        const int Paper=0,Ribbon=1;
        // Edge round of box and lid; lid depth and overhang; ribbon half width and thickness; how far the second ribbon
        // lies over the first where they cross.
        const float Edge=.003f,LidDepth=.018f,Overhang=.0015f,RibbonHalf=.011f,RibbonThick=.0009f,Over=.0006f;

        [MenuItem("MetaHackthone/Shadow Workshop/Build gifts")]
        public static void Build()
        {
            foreach(var p in Props){var m=BuildMesh(p);Debug.Log($"GIFT_OK {m.name} {m.triangles.Length/3} tris");}
        }

        public static Mesh BuildMesh(Prop p)
        {
            var b=new PropMesh(2);var h=p.half;
            // The box (its top hidden in the lid, so left out), then the lid at the original's full size.
            b.BevelBox(Paper,new Vector3(-h.x+Overhang,-h.y,-h.z+Overhang),new Vector3(h.x-Overhang,h.y-.002f,h.z-Overhang),Edge,false);
            b.BevelBox(Paper,new Vector3(-h.x,h.y-LidDepth,-h.z),new Vector3(h.x,h.y,h.z),Edge);
            Band(b,h,Vector3.right,Vector3.forward,0);     // width along x, running round over the top along z
            Band(b,h,Vector3.forward,Vector3.right,Over);  // width along z, running along x, lying on the first
            Bow(b,new Vector3(0,h.y+RibbonThick+Over,0),p.bow);
            return b.Save(p.mesh,p.name);
        }

        // A ribbon round the box: width along w, running round in the plane of a and y. Its outline on the +a side, from
        // the top: over the lid's rounded edge, down its side, in under its edge, down the box and round under it; then
        // the same mirrored back up the -a side. Only its outer face is drawn: the inside lies on the box and its edges are
        // under a millimetre. The bevels are single steps, as the box's are.
        static void Band(PropMesh b,Vector3 h,Vector3 w,Vector3 a,float lift)
        {
            float lid=Vector3.Dot(h,a),box=lid-Overhang,t=RibbonThick+lift,r=Edge;
            var side=new List<Vector2>{new Vector2(lid-r,h.y+t),new Vector2(lid+t,h.y-r),new Vector2(lid+t,h.y-LidDepth+r),
                new Vector2(box+t,h.y-LidDepth-.0005f),new Vector2(box+t,-h.y+r),new Vector2(box-r,-h.y-t)};
            var loop=new List<Vector2>(side);for(int k=side.Count-1;k>=0;k--)loop.Add(new Vector2(-side[k].x,side[k].y));
            int count=loop.Count,start=b.v.Count;float along=0;
            for(int i=0;i<count;i++)
            {
                Vector2 p=loop[i],tangent=loop[(i+1)%count]-loop[(i+count-1)%count],n=new Vector2(tangent.y,-tangent.x).normalized;
                if(Vector2.Dot(n,p)<0)n=-n;
                if(i>0)along+=Vector2.Distance(loop[i-1],p);
                Vector3 at=a*p.x+Vector3.up*p.y,normal=a*n.x+Vector3.up*n.y;var uv=new Vector2(along*UvPerMetre,0);
                b.Add(at-w*RibbonHalf,normal,uv);b.Add(at+w*RibbonHalf,normal,uv);
            }
            for(int i=0;i<count;i++){int q=start+i*2,s=start+(i+1)%count*2;b.Quad(Ribbon,q,s,s+1,q+1);}
        }

        // Bow at c (on the crossed ribbons), s its size: a knot, two loops along x pinched into it, two tails lying on
        // the lid across the other diagonal.
        static void Bow(PropMesh b,Vector3 c,float s)
        {
            b.SmoothLathe(Ribbon,c,Vector3.up,6,new[]{new Vector2(0,0),new Vector2(.0065f*s,.0005f),new Vector2(.0070f*s,.0040f*s),new Vector2(.0050f*s,.0075f*s),new Vector2(0,.0085f*s)});
            foreach(var d in new[]{Vector3.right,Vector3.left})
            {
                // Loop outline in mm (out along d, up), from inside the knot round and back into it.
                var path=new List<Vector3>();
                foreach(var (o,u) in new[]{(2f,3f),(11f,10.5f),(23f,13f),(31f,8.5f),(28f,2.5f),(15f,1.2f),(3f,2.2f)})
                    path.Add(c+d*(o*.001f*s)+Vector3.up*(u*.001f*s));
                b.Bar(Ribbon,path,Vector3.Cross(d,Vector3.up),t=>Strip(.0070f*s*Mathf.Lerp(.45f,1f,Mathf.Sin(t*Mathf.PI)),.0007f),0,0);
            }
            foreach(var deg in new[]{-35f,145f})Tail(b,c,Quaternion.Euler(0,deg,0)*Vector3.right,s);
        }
        // A flat ribbon's section for Bar: width 2w across the side axis, thickness 2t, its two broad faces shaded nearly
        // flat. The normals lean out a little so the thin edges still wind outward.
        static readonly Vector2[] StripNormals={new Vector2(-.2f,1).normalized,new Vector2(.2f,1).normalized,new Vector2(.2f,-1).normalized,new Vector2(-.2f,-1).normalized};
        static (Vector2[] at,Vector2[] normal) Strip(float w,float t)=>(new[]{new Vector2(-w,t),new Vector2(w,t),new Vector2(w,-t),new Vector2(-w,-t)},StripNormals);
        // A tail from under the knot out along dir, sloping down onto the lid, ending in a V notch. Only its top is drawn
        // (it lies on the lid): three triangles fanned from the notch.
        static void Tail(PropMesh b,Vector3 c,Vector3 dir,float s)
        {
            float len=.026f*s,w0=.0045f*s,w1=.0062f*s,notch=.005f*s;
            var outline=new[]{new Vector2(0,-w0),new Vector2(len,-w1),new Vector2(len-notch,0),new Vector2(len,w1),new Vector2(0,w0)};
            // Built flat in (x along, y across); placed just over the ribbon, dropping RibbonThick + Over to the lid.
            var across=Vector3.Cross(Vector3.up,dir);int start=b.v.Count;
            foreach(var p in outline)
            {
                float drop=(RibbonThick+Over)*Mathf.Clamp01(p.x/len);
                b.Add(c+dir*p.x+across*p.y+Vector3.up*(.00075f-drop),Vector3.up,p*UvPerMetre);
            }
            b.Tri(Ribbon,start+2,start+3,start+4);b.Tri(Ribbon,start+2,start+4,start);b.Tri(Ribbon,start+2,start,start+1);
        }
    }
}
