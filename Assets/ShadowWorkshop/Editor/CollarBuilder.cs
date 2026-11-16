using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // A dog collar lying flat, for mobile VR: a red strap 13 cm across, 2 cm wide and 3 mm thick standing round in a
    // ring, a steel buckle frame on it, and a steel D-ring by the buckle with a round brass name tag hanging from it
    // (down as the collar lies in Home_NEW's pile: see Down). Pivot at the bottom centre, like the plate and frisbee it
    // can stand in for. About 400 triangles, three materials (collar red, and the shared kitchen steel and brass). A
    // closed 12-sided ring with the D-ring and tag as drawn (about 190 triangles) casts its shadow, so the hanging tag
    // shows on the wall (ShadowPiece.shadowProxy in the gym, Torch2Piece.shadowMesh in levels).
    public static class CollarBuilder
    {
        const string Folder="Assets/ShadowWorkshop/Art/Torch2/";
        public const string MeshPath=Folder+"Meshes/Dog collar detailed.asset",ShadowPath=Folder+"Meshes/Dog collar.asset";
        const int Strap=0,Steel=1,Tag=2;
        const float Ro=.065f,Ri=.062f,H=.020f;   // strap: outer and inner radius, width (its height lying flat)
        const float Wire=.0013f;                 // half thickness of the D-ring's wire (the buckle's is 1.5 mm)
        const float BuckleAt=0,TailTo=-70,RingAt=20;    // where round the strap the buckle, the tail's end and the D-ring sit (degrees)
        const float TagR=.011f;                          // the name tag's radius
        // The collar's pose in Home_NEW's pile (gym, at the answer): world down, and a face turned half way to both lamps
        // (their lines of sight are about square to each other and to down), in the collar's space. The band's lowest
        // point is by the buckle, so the D-ring sits just past it and the tag hangs clear below the band, square to
        // both lamps' sight: it shows on both walls. Moving the collar in the pile means updating these.
        static readonly Vector3 Down=new Vector3(.511f,-.859f,.035f).normalized,TagFace=new Vector3(.816f,.477f,-.326f).normalized;
        static readonly Look[] Looks={
            new Look("collar red",new Color(.80f,.15f,.13f),0,.30f),
            new Look("kitchen steel",new Color(.86f,.87f,.89f),.75f,.86f),
            new Look("brass",new Color(.85f,.68f,.30f),0,.80f)};

        [MenuItem("MetaHackthone/Shadow Workshop/Build dog collar")]
        public static void Build()
        {
            var (mesh,shadow)=BuildMeshes();PropMesh.Materials(Folder+"Materials/",Looks);
            Debug.Log($"DOG_COLLAR_OK {mesh.triangles.Length/3} tris (shadow {shadow.triangles.Length/3}), {mesh.bounds.size*100} cm");
        }

        // Puts a collar where target is (same place and turn, real size) and switches target off, so the two can be
        // compared; undoable.
        public static GameObject PlaceAt(Transform target)
        {
            var (mesh,shadow)=BuildMeshes();var materials=PropMesh.Materials(Folder+"Materials/",Looks);
            var go=new GameObject("Collar");Undo.RegisterCreatedObjectUndo(go,"Place collar");
            go.transform.SetParent(target.parent,false);go.transform.localPosition=target.localPosition;go.transform.localRotation=target.localRotation;
            var parent=target.parent?target.parent.lossyScale:Vector3.one;
            go.transform.localScale=new Vector3(1/Mathf.Abs(parent.x),1/Mathf.Abs(parent.y),1/Mathf.Abs(parent.z));
            go.transform.SetSiblingIndex(target.GetSiblingIndex()+1);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;
            var tag=go.AddComponent<ShadowPiece>();tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=shadow;
            Undo.RecordObject(target.gameObject,"Place collar");target.gameObject.SetActive(false);
            return go;
        }

        static (Mesh mesh,Mesh shadow) BuildMeshes()
        {
            var b=new PropMesh(3);
            // Strap: a flat band round the axis; its cross-section walked outside face up, over the top, down the inside
            // and back under (each corner a crease).
            b.Revolve(Strap,24,new[]{V(Ro,0),V(Ro,H),V(Ro,H),V(Ri,H),V(Ri,H),V(Ri,0),V(Ri,0),V(Ro,0)});
            // Tail: the strap's free end, from under the buckle round over the band to a square end.
            Tail(b,Strap,BuckleAt+6,TailTo,Ro+.0002f,Ro+.0032f,.0015f,H-.0015f,12);
            // Buckle: a rounded rectangular frame over the tail, a little taller than the strap, and its prong lying
            // across the tail.
            var bt=Frame(BuckleAt);var buckle=new List<Vector3>();const float BuckleWire=.0015f,Out=Ro+.0035f+BuckleWire;
            foreach(var p in RoundCorners(new[]{(V(-.013f,BuckleWire),.003f,1),(V(.013f,BuckleWire),.003f,1),(V(.013f,H+.002f),.003f,1),(V(-.013f,H+.002f),.003f,1)},true))
                buckle.Add(bt.radial*Out+bt.along*p.x+Vector3.up*p.y);
            Loop(b,Steel,buckle,bt.radial,BuckleWire);
            b.Bar(Steel,new[]{bt.radial*Out+bt.along*.013f+Vector3.up*(H/2+.001f),bt.radial*(Out-.0005f)+bt.along*-.006f+Vector3.up*(H/2+.001f)},Vector3.up,
                t=>Square(.0009f),0,.0009f,1);
            // D-ring hanging from the strap's lower edge: its straight side along the strap, its round side swung as far
            // down as the strap lets it (so down and out). The name tag, a brass disc, hangs straight down from its far side.
            var (ring,ringNormal,tagAt)=Hanging();
            Loop(b,Steel,ring,ringNormal,Wire);
            HangingTag(b,Tag,tagAt,12);

            // The shadow: the band, and the D-ring and tag as drawn, so the tag shows hanging on the wall too.
            var s=new PropMesh(1);s.Revolve(0,12,new[]{V(Ro,0),V(Ro,H),V(Ro,H),V(Ri,H),V(Ri,H),V(Ri,0),V(Ri,0),V(Ro,0)});
            Loop(s,0,ring,ringNormal,Wire);HangingTag(s,0,tagAt,8);
            return (b.Save(MeshPath,"Dog collar detailed"),s.Save(ShadowPath,"Dog collar"));
        }

        // The hanging D-ring's wire path and plane normal, and where the tag's centre hangs below it.
        static (List<Vector3> ring,Vector3 normal,Vector3 tagAt) Hanging()
        {
            var rt=Frame(RingAt);var swing=(Down-Vector3.Dot(Down,rt.along)*rt.along).normalized;
            var hinge=rt.radial*(Ro+Wire)+Vector3.up*Wire;var ring=new List<Vector3>();
            var d=new List<Vector2>{V(0,-.008f),V(0,.008f)};
            foreach(var deg in new[]{70f,35f,0f,-35f,-70f}){float a=deg*Mathf.Deg2Rad;d.Add(V(.0018f+.0082f*Mathf.Cos(a),.0082f*Mathf.Sin(a)));}
            foreach(var p in d)ring.Add(hinge+swing*p.x+rt.along*p.y);
            var far=hinge+swing*.0100f;
            return (ring,Vector3.Cross(swing,rt.along).normalized,far+Down*(TagR+Wire));
        }
        // The name tag: a brass disc 1.5 mm thick hanging with its face turned to TagFace (round the vertical).
        static void HangingTag(PropMesh b,int m,Vector3 centre,int sides)
        {
            int start=b.v.Count;
            b.Revolve(m,sides,new[]{V(0,-.00075f),V(TagR,-.00075f),V(TagR,-.00075f),V(TagR,.00075f),V(TagR,.00075f),V(0,.00075f)});
            var face=(TagFace-Vector3.Dot(TagFace,Down)*Down).normalized;var turn=Quaternion.FromToRotation(Vector3.up,face);
            for(int i=start;i<b.v.Count;i++){b.v[i]=centre+turn*b.v[i];b.n[i]=turn*b.n[i];}
        }

        // Outward and along-the-strap directions at deg round the axis.
        static (Vector3 radial,Vector3 along) Frame(float deg)
        {
            float a=deg*Mathf.Deg2Rad;return (new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a)));
        }
        // A closed wire through path (lying in the plane with normal planeNormal), half thickness w.
        static void Loop(PropMesh b,int m,List<Vector3> path,Vector3 planeNormal,float w)
        {
            int n=path.Count;var centre=Vector3.zero;foreach(var p in path)centre+=p/n;
            var side=new Vector3[n];var up=new Vector3[n];
            for(int i=0;i<n;i++)
            {
                var t=path[(i+1)%n]-path[(i+n-1)%n];var o=Vector3.Cross(planeNormal,t).normalized;if(Vector3.Dot(o,path[i]-centre)<0)o=-o;
                side[i]=planeNormal;up[i]=o;
            }
            var section=Square(w);b.Sweep(m,path.ToArray(),side,up,i=>section,true,1);
        }
        // A square wire section whose normals lean to its corners, so four sides shade round.
        static (Vector2[] at,Vector2[] normal) Square(float w)=>
            (new[]{V(w,w),V(-w,w),V(-w,-w),V(w,-w)},new[]{V(1,1).normalized,V(-1,1).normalized,V(-1,-1).normalized,V(1,-1).normalized});
        // A strap segment round the axis from angle a0 to a1 (degrees) between radii r0 and r1 and heights h0 and h1: its
        // outside, top and bottom (its inside lies on the band) and a square end at a1.
        static void Tail(PropMesh b,int m,float a0,float a1,float r0,float r1,float h0,float h1,int steps)
        {
            int start=b.v.Count;
            for(int k=0;k<=steps;k++)
            {
                var (radial,_)=Frame(Mathf.Lerp(a0,a1,(float)k/steps));
                b.Add(radial*r1+Vector3.up*h0,radial,Vector2.zero);b.Add(radial*r1+Vector3.up*h1,radial,Vector2.zero);
                b.Add(radial*r1+Vector3.up*h1,Vector3.up,Vector2.zero);b.Add(radial*r0+Vector3.up*h1,Vector3.up,Vector2.zero);
                b.Add(radial*r0+Vector3.up*h0,Vector3.down,Vector2.zero);b.Add(radial*r1+Vector3.up*h0,Vector3.down,Vector2.zero);
            }
            for(int k=0;k<steps;k++)for(int f=0;f<3;f++){int q=start+k*6+f*2,s=q+6;b.Quad(m,q,s,s+1,q+1);}
            var (er,end)=Frame(a1);var cap=a1<a0?-end:end;
            b.Quad(m,b.Add(er*r0+Vector3.up*h0,cap,Vector2.zero),b.Add(er*r1+Vector3.up*h0,cap,Vector2.zero),b.Add(er*r1+Vector3.up*h1,cap,Vector2.zero),b.Add(er*r0+Vector3.up*h1,cap,Vector2.zero));
        }

        static Vector2 V(float x,float y)=>new Vector2(x,y);
    }
}
