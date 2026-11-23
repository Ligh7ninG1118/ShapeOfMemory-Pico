using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropCanvas;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // A Wild West hotel room key for the WildWest level (2026-11-16, the user: "wildwest这一关做一个钥匙放在关卡里面",
    // with a photo of a motel key): a brass pin-tumbler key and a long black fob, threaded on one steel split ring. The fob
    // is a 44 x 88 mm hexagon with pointed ends, 5 mm thick, with a hole at its top end. Its front (-z) is printed MOTEL
    // (the user: "直接说motel就行 加一个room number"), with a lucky horseshoe, ROOM and a big 7; its back says PLEASE RETURN
    // TO MOTEL, ROOM 7. The ring
    // stands across the fob's hole, and the key hangs off the ring's top, lying flat beside the fob and pointing down and
    // to the right. Everything is in metres, in the key's own frame (x right, y up). The drawn mesh is about 600
    // triangles in three materials. A coarse closed twin, about 100 triangles, casts the shadow.
    public static class WildWestKeyBuilder
    {
        const string Art="Assets/ShadowWorkshop/Art/Torch2/";
        public const string MeshPath=Art+"Meshes/Room key.asset",ShadowPath=Art+"Meshes/Room key shadow.asset";
        const string FobPath=Art+"Textures/room key fob.png",MaterialFolder=Art+"Materials/";
        const int Fob=0,Brass=1,Steel=2;

        // Fob: corners counter-clockwise from the bottom tip, and how round each is.
        static readonly Vector2[] FobCorners={new Vector2(0,-.044f),new Vector2(.022f,-.022f),new Vector2(.022f,.022f),new Vector2(0,.044f),new Vector2(-.022f,.022f),new Vector2(-.022f,-.022f)};
        static readonly float[] FobRadius={.007f,.005f,.005f,.007f,.005f,.005f};
        const float FobHalf=.0025f,FobEdge=.001f,Border=.0025f;
        static readonly Vector2 Hole=new Vector2(0,.033f);const float HoleR=.0036f;const int HoleSides=10;
        // Ring: a circle across the hole (in the y-z plane, through the hole's centre), wire round.
        const float RingR=.0125f,Wire=.0009f;const int RingSegments=14;
        static Vector3 RingCentre=>new Vector3(0,Hole.y+RingR,0);
        // Key: its bow's hole on the ring's top, the blade KeyAngle degrees below +x.
        const float BowIn=.004f,BowOut=.0105f,BowHalf=.0011f,BladeHalf=.0009f,KeyAngle=-40;const int BowSides=14;
        // Blade outline (mm, x along the blade from the bow's centre): the cuts along the bottom, the tip, the straight top.
        static readonly float[] Blade={7,-3.6f, 13,-3.6f, 13,-4.6f, 15,-4.6f, 16.5f,-2.9f, 18.5f,-2.9f, 20,-4.6f, 22,-4.6f, 23.5f,-3.3f, 25.5f,-3.3f,
            27,-4.3f, 29,-4.3f, 30.5f,-3.0f, 32.5f,-3.0f, 34,-4.6f, 37,-4.6f, 44,-1.0f, 45,1.2f, 43,3.4f, 13,3.4f, 13,4.4f, 7,4.4f};

        // Fob print: 512 x 512 px, the front's box at FrontX and the back's at BackX (mirrored, so it reads from behind),
        // both from FaceY up, PxPerMetre.
        const int Size=512,FrontX=16,BackX=264,FaceY=8;const float PxPerMetre=5500;

        // For WildWestBuilder.Props (as WesternPostcardBuilder.Prop): the level places it from there.
        public static readonly WildWestBuilder.Prop Prop=new WildWestBuilder.Prop{label="ROOM KEY",file="Room key",shadowFile="Room key shadow",build=BuildMesh,buildShadow=BuildShadow,materials=Materials};

        [MenuItem("MetaHackthone/Shadow Workshop/Build Wild West room key")]
        public static void Build()
        {
            FobTexture();var mesh=BuildMesh();var shadow=BuildShadow();var materials=Materials();
            var counts=new List<string>();for(int i=0;i<mesh.subMeshCount;i++)counts.Add((mesh.GetIndexCount(i)/3).ToString());
            Debug.Log($"WILD_WEST_KEY_OK tris={string.Join("+",counts)} shadow={shadow.triangles.Length/3} size={mesh.bounds.size*1000} mm materials={materials.Length}");
        }
        // The fob's print; the key reuses the Wild West gold, the ring the belt buckle's silver (made the same way if missing).
        public static Material[] Materials()=>PropMesh.Materials(MaterialFolder,new[]{
            new Look("room key fob",Color.white,0f,.60f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(FobPath)??FobTexture()},
            new Look("western gold",new Color(.93f,.72f,.30f),.85f,.70f),new Look("buckle silver",new Color(.80f,.80f,.83f),.90f,.75f)});

        public static Mesh BuildMesh()
        {
            var b=new PropMesh(3);
            // Fob: rounded edge all round, the two faces round the hole, and the hole's wall.
            var outline=Outline(FobCorners,FobRadius,0,45,.015f);
            var plain=FaceUv(new Vector2(-.016f,0),FrontX,false);   // plain black, clear of the print
            Rim(b,outline,plain);
            var face=new List<Vector2>();foreach(var (p,n) in outline)face.Add(p-n*FobEdge);
            var hole=new List<Vector2>();for(int k=0;k<HoleSides;k++){float a=k*2*Mathf.PI/HoleSides;hole.Add(Hole+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*HoleR);}
            Ring(b,Fob,Hole,face,hole,-FobHalf,Vector3.back,p=>FaceUv(p,FrontX,false));
            Ring(b,Fob,Hole,face,hole,FobHalf,Vector3.forward,p=>FaceUv(p,BackX,true));
            int start=b.v.Count;
            for(int k=0;k<=HoleSides;k++)
            {
                var p=hole[k%HoleSides];var inward=(Vector3)(Hole-p).normalized;
                b.Add(new Vector3(p.x,p.y,-FobHalf),inward,plain);b.Add(new Vector3(p.x,p.y,FobHalf),inward,plain);
            }
            for(int k=0;k<HoleSides;k++){int q=start+k*2;b.Quad(Fob,q,q+1,q+3,q+2);}

            // Ring: a round wire on a circle in the y-z plane.
            var path=new Vector3[RingSegments];var side=new Vector3[RingSegments];var up=new Vector3[RingSegments];
            for(int k=0;k<RingSegments;k++)
            {
                float a=k*2*Mathf.PI/RingSegments;var radial=new Vector3(0,-Mathf.Cos(a),Mathf.Sin(a));
                path[k]=RingCentre+radial*RingR;up[k]=radial;side[k]=Vector3.right;
            }
            var wire=Ellipse(Wire,Wire,4);
            b.Sweep(Steel,path,side,up,j=>wire,true,1);

            // Key: the bow (a washer round the ring's top) and the blade, thinner, running out of it.
            var bow=RingCentre+Vector3.up*RingR;var turn=Quaternion.Euler(0,0,KeyAngle);
            b.Lathe(bow,Vector3.forward,BowSides,P(BowOut,BowHalf,Brass),P(BowIn,BowHalf,Brass),P(BowIn,-BowHalf,Brass),P(BowOut,-BowHalf,Brass),P(BowOut,BowHalf,Brass));
            var blade=new Vector2[Blade.Length/2];for(int k=0;k<blade.Length;k++)blade[k]=new Vector2(Blade[2*k],Blade[2*k+1])*.001f;
            start=b.v.Count;
            b.Slab(Brass,i=>Brass,blade,.0003f,p=>BladeHalf,p=>0,1,UvPerMetre);
            for(int k=start;k<b.v.Count;k++){b.v[k]=bow+turn*b.v[k];b.n[k]=turn*b.n[k];}
            return b.Save(MeshPath,"Room key");
        }

        // The shadow: the same things as plain closed solids (fob and bow as prisms, the blade a box, the ring an
        // eight-sided loop), about 100 triangles.
        public static Mesh BuildShadow()
        {
            var b=new PropMesh(1);
            Prism(b,Array.ConvertAll(FobCorners,c=>c),-FobHalf,FobHalf);
            var bow=RingCentre+Vector3.up*RingR;var turn=Quaternion.Euler(0,0,KeyAngle);
            var octagon=new Vector2[8];for(int k=0;k<8;k++){float a=(k+.5f)*Mathf.PI/4;octagon[k]=(Vector2)bow+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*BowOut;}
            Prism(b,octagon,-BowHalf,BowHalf);
            var box=new[]{new Vector2(.007f,-.0046f),new Vector2(.044f,-.0046f),new Vector2(.045f,.0034f),new Vector2(.007f,.0034f)};
            for(int k=0;k<box.Length;k++){var p=turn*box[k];box[k]=(Vector2)bow+new Vector2(p.x,p.y);}
            Prism(b,box,-BladeHalf,BladeHalf);
            var path=new Vector3[8];var side=new Vector3[8];var up=new Vector3[8];
            for(int k=0;k<8;k++){float a=k*Mathf.PI/4;var radial=new Vector3(0,-Mathf.Cos(a),Mathf.Sin(a));path[k]=RingCentre+radial*RingR;up[k]=radial;side[k]=Vector3.right;}
            var wire=Ellipse(Wire,Wire,3);
            b.Sweep(0,path,side,up,j=>wire,true,1);
            return b.Save(ShadowPath,"Room key shadow");
        }
        // A convex polygon (counter-clockwise, x, y) between z0 and z1: fans for the caps, a flat quad per side.
        static void Prism(PropMesh b,Vector2[] poly,float z0,float z1)
        {
            foreach(var (z,normal) in new[]{(z0,Vector3.back),(z1,Vector3.forward)})
            {
                int start=b.v.Count;foreach(var p in poly)b.Add(new Vector3(p.x,p.y,z),normal,Vector2.zero);
                for(int k=1;k+1<poly.Length;k++)b.Tri(0,start,start+k,start+k+1);
            }
            for(int k=0;k<poly.Length;k++)
            {
                var a=poly[k];var c=poly[(k+1)%poly.Length];var d=(c-a).normalized;var n=new Vector3(d.y,-d.x,0);int q=b.v.Count;
                b.Add(new Vector3(a.x,a.y,z0),n,Vector2.zero);b.Add(new Vector3(c.x,c.y,z0),n,Vector2.zero);b.Add(new Vector3(c.x,c.y,z1),n,Vector2.zero);b.Add(new Vector3(a.x,a.y,z1),n,Vector2.zero);
                b.Quad(0,q,q+1,q+2,q+3);
            }
        }

        // --- Fob shape ---------------------------------------------------------------------------------------------------
        // A polygon's outline pulled in by inset, its corners rounded (concentric, at least 0.4 mm), counter-clockwise, as
        // points with outward normals: corner arcs every arcStep degrees, straight edges every step.
        static List<(Vector2 p,Vector2 n)> Outline(Vector2[] corners,float[] radius,float inset,float arcStep,float step)
        {
            int count=corners.Length;var arcs=new (Vector2 c,float r,float a0,float a1)[count];
            for(int i=0;i<count;i++)
            {
                Vector2 at=corners[i],dIn=(at-corners[(i+count-1)%count]).normalized,dOut=(corners[(i+1)%count]-at).normalized;
                Vector2 nIn=new Vector2(dIn.y,-dIn.x),nOut=new Vector2(dOut.y,-dOut.x);
                float halfInterior=Vector2.Angle(-dIn,dOut)*.5f*Mathf.Deg2Rad,r=Mathf.Max(radius[i]-inset,.0004f);
                var c=at-(nIn+nOut).normalized*((inset+r)/Mathf.Sin(halfInterior));
                float a0=Mathf.Atan2(nIn.y,nIn.x),a1=Mathf.Atan2(nOut.y,nOut.x);if(a1<a0)a1+=2*Mathf.PI;
                arcs[i]=(c,r,a0,a1);
            }
            var list=new List<(Vector2,Vector2)>();
            for(int i=0;i<count;i++)
            {
                var (c,r,a0,a1)=arcs[i];int steps=Mathf.Max(1,Mathf.CeilToInt((a1-a0)/(arcStep*Mathf.Deg2Rad)-.001f));
                for(int k=0;k<=steps;k++){float a=Mathf.Lerp(a0,a1,(float)k/steps);var n=new Vector2(Mathf.Cos(a),Mathf.Sin(a));list.Add((c+n*r,n));}
                var next=arcs[(i+1)%count];var normal=new Vector2(Mathf.Cos(a1),Mathf.Sin(a1));
                var from=c+normal*r;var to=next.c+new Vector2(Mathf.Cos(next.a0),Mathf.Sin(next.a0))*next.r;
                int m=Mathf.CeilToInt(Vector2.Distance(from,to)/step-.001f);for(int k=1;k<m;k++)list.Add((Vector2.Lerp(from,to,(float)k/m),normal));
            }
            return list;
        }
        // Rounded edge all round: from each face (FobEdge in from the outline) a bevel out to a flat band. Normals turn from
        // the face to the side, so it shades round. All of it takes the print's plain black.
        static void Rim(PropMesh b,List<(Vector2 p,Vector2 n)> outline,Vector2 uv)
        {
            var profile=new[]{(-FobEdge,-FobHalf,0f,-1f),(0f,-(FobHalf-FobEdge),1f,0f),(0f,FobHalf-FobEdge,1f,0f),(-FobEdge,FobHalf,0f,1f)};
            int count=outline.Count,rows=profile.Length,start=b.v.Count;
            for(int i=0;i<=count;i++)
            {
                var (p,n)=outline[i%count];
                foreach(var (o,z,c,s) in profile)b.Add(new Vector3(p.x+n.x*o,p.y+n.y*o,z),new Vector3(n.x*c,n.y*c,s),uv);
            }
            for(int i=0;i<count;i++)for(int j=0;j+1<rows;j++){int q=start+i*rows+j;b.Quad(Fob,q,q+rows,q+rows+1,q+1);}
        }
        // A flat face at z between an outer and an inner loop, both counter-clockwise and star-shaped round centre, zipped
        // together in order of angle, except that a step whose triangle would turn the wrong way (over the hole) takes the
        // other loop's next point instead (as in LuggageTagBuilder).
        static void Ring(PropMesh b,int m,Vector2 centre,List<Vector2> outer,List<Vector2> inner,float z,Vector3 normal,Func<Vector2,Vector2> uv)
        {
            float Angle(Vector2 p){var d=p-centre;float a=Mathf.Atan2(d.y,d.x);return a<0?a+2*Mathf.PI:a;}
            (int[] index,Vector2[] at,float[] angle) Loop(List<Vector2> loop)
            {
                int first=0;for(int i=1;i<loop.Count;i++)if(Angle(loop[i])<Angle(loop[first]))first=i;
                var index=new int[loop.Count+1];var at=new Vector2[loop.Count+1];var angle=new float[loop.Count+1];
                for(int k=0;k<=loop.Count;k++)
                {
                    var p=loop[(first+k)%loop.Count];at[k]=p;angle[k]=Angle(p)+(k==loop.Count?2*Mathf.PI:0);
                    index[k]=k==loop.Count?index[0]:b.Add(new Vector3(p.x,p.y,z),normal,uv(p));
                }
                return (index,at,angle);
            }
            float Turn(Vector2 a,Vector2 c,Vector2 d)=>(c.x-a.x)*(d.y-a.y)-(c.y-a.y)*(d.x-a.x);
            var (outerIndex,oa,outerAngle)=Loop(outer);var (innerIndex,ia,innerAngle)=Loop(inner);
            int no=outer.Count,ni=inner.Count,po=0,pi=0;
            while(po<no||pi<ni)
            {
                bool next;
                if(pi>=ni)next=true;else if(po>=no)next=false;
                else
                {
                    bool outerOk=Turn(oa[po],oa[po+1],ia[pi])>0,innerOk=Turn(oa[po],ia[pi+1],ia[pi])>0;
                    next=outerAngle[po+1]<=innerAngle[pi+1]?outerOk||!innerOk:outerOk&&!innerOk;
                }
                if(next){b.Tri(m,outerIndex[po],outerIndex[po+1],innerIndex[pi]);po++;}
                else{b.Tri(m,outerIndex[po],innerIndex[pi+1],innerIndex[pi]);pi++;}
            }
        }

        // --- Fob print ---------------------------------------------------------------------------------------------------
        static Vector2 FacePixel(Vector2 p,int x0,bool mirror)=>new Vector2(x0+((mirror?-p.x:p.x)+.022f)*PxPerMetre,FaceY+(p.y+.044f)*PxPerMetre);
        static Vector2 FaceUv(Vector2 p,int x0,bool mirror){var q=FacePixel(p,x0,mirror);return new Vector2(q.x/Size,q.y/Size);}

        static Texture2D FobTexture()
        {
            const int w=Size,h=Size;var px=new Color[w*h];
            Color black=new Color(.06f,.06f,.07f),print=new Color(.94f,.90f,.78f),ridge=new Color(.16f,.16f,.17f),groove=new Color(.02f,.02f,.02f);
            for(int i=0;i<px.Length;i++)px[i]=black;
            float cell=.00062f*PxPerMetre;   // the small print's dot pitch, px
            foreach(var mirror in new[]{false,true})
            {
                int x0=mirror?BackX:FrontX;Vector2 At(float x,float y)=>FacePixel(new Vector2(x,y),x0,mirror);
                // A printed line just inside the edge, and a ring round the hole.
                var border=Outline(FobCorners,FobRadius,Border,10,.002f);var loop=new List<Vector2>();foreach(var (p,_) in border)loop.Add(At(p.x,p.y));loop.Add(loop[0]);
                Stroke(px,w,h,loop,.0006f*PxPerMetre,print);
                var circle=new List<Vector2>();for(int k=0;k<=32;k++){float a=k*2*Mathf.PI/32;circle.Add(At(Hole.x+Mathf.Cos(a)*.0058f,Hole.y+Mathf.Sin(a)*.0058f));}
                Stroke(px,w,h,circle,.0005f*PxPerMetre,print);
                // Ridges across the bottom end, as moulded into the plastic.
                foreach(var y in new[]{-.031f,-.0335f,-.036f,-.0385f})
                {
                    float half=Mathf.Min(.022f,.044f+y)-.0045f;if(half<=.001f)continue;
                    Stroke(px,w,h,new List<Vector2>{At(-half,y-.0004f),At(half,y-.0004f)},.0006f*PxPerMetre,groove);
                    Stroke(px,w,h,new List<Vector2>{At(-half,y+.0003f),At(half,y+.0003f)},.0005f*PxPerMetre,ridge);
                }
                void Line(string text,float y,float pitch)
                {
                    float width=TextWidth(text,pitch);var origin=At(0,y);
                    RoundText(px,w,h,text,origin.x-width*.5f,origin.y,pitch,pitch,print,.9f);
                }
                if(!mirror)
                {
                    Line("MOTEL",.0145f,cell*1.45f);
                    // A horseshoe, open end up (for luck), with its nail holes.
                    var shoe=new List<Vector2>();for(int k=0;k<=24;k++){float a=Mathf.Lerp(-35,215,k/24f)*Mathf.Deg2Rad;shoe.Add(At(Mathf.Cos(a)*.0055f,.002f-Mathf.Sin(a)*.0055f));}
                    Stroke(px,w,h,shoe,.0019f*PxPerMetre,print);
                    foreach(var deg in new[]{-10f,25f,60f,120f,155f,190f}){float a=deg*Mathf.Deg2Rad;var hole=At(Mathf.Cos(a)*.0055f,.002f-Mathf.Sin(a)*.0055f);Stroke(px,w,h,new List<Vector2>{hole,hole},.0005f*PxPerMetre,black);}
                    Line("ROOM",-.0115f,cell);
                    float big=.0021f*PxPerMetre;var seven=At(0,-.0285f);
                    Glyph(px,w,h,Seven,seven.x-2.5f*big,seven.y,big,print,.85f);
                }
                else
                {
                    Line("PLEASE",.014f,cell);Line("RETURN",.007f,cell);Line("TO",0,cell);Line("MOTEL",-.0075f,cell);
                    // ROOM 7: the word in the print font, the 7 in the dot glyph beside it, centred together.
                    float room=TextWidth("ROOM",cell),gap=cell*5,total=room+gap+5*cell;var at=At(0,-.0175f);
                    RoundText(px,w,h,"ROOM",at.x-total*.5f,at.y,cell,cell,print,.9f);Glyph(px,w,h,Seven,at.x-total*.5f+room+gap,at.y,cell,print,.9f);
                }
            }
            var tex=new Texture2D(w,h,TextureFormat.RGB24,true,false);tex.SetPixels(px);tex.Apply();
            SaveTexture(FobPath,tex,false,TextureWrapMode.Clamp);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(FobPath);
        }
        // The room number, in the 5 x 7 dots of PropCanvas's font (which has no digits), joined the same way: across, down,
        // and diagonally where no square neighbour turns the corner. (x, y) the bottom left, cell px per dot.
        static readonly string[] Seven={"11111","00001","00010","00100","01000","01000","01000"};
        static void Glyph(Color[] px,int w,int h,string[] rows,float x,float y,float cell,Color color,float weight)
        {
            bool Dot(int r,int c)=>r>=0&&r<7&&c>=0&&c<5&&rows[r][c]=='1';
            Vector2 At(int r,int c)=>new Vector2(x+(c+.5f)*cell,y+(6-r+.5f)*cell);
            var segments=new List<(Vector2,Vector2)>();
            for(int r=0;r<7;r++)for(int c=0;c<5;c++)
            {
                if(!Dot(r,c))continue;
                if(Dot(r,c+1))segments.Add((At(r,c),At(r,c+1)));
                if(Dot(r+1,c))segments.Add((At(r,c),At(r+1,c)));
                if(Dot(r+1,c+1)&&!Dot(r,c+1)&&!Dot(r+1,c))segments.Add((At(r,c),At(r+1,c+1)));
                if(Dot(r+1,c-1)&&!Dot(r,c-1)&&!Dot(r+1,c))segments.Add((At(r,c),At(r+1,c-1)));
            }
            Segments(px,w,h,segments,cell*weight,color);
        }
    }
}
