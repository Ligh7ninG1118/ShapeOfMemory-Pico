using System;
using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // The light the shadow comes from, shown as an old flashlight floating where the lamp is (Torch2Room's lamp head: at
    // the light, its forward aimed at the objects). The 2026-11-08 playtest asked for the light source back, as a
    // flashlight; the earlier lamp on a stand read as a microphone. A dark enamel body with brass bezel, grip rings and
    // tail cap; the lens glows with the lamp (Torch2World.LampLevel) and a short, faint beam leaves it toward the objects.
    public sealed class MemoryFlashlight : MonoBehaviour
    {
        // Metres. The lens sits at the light; everything else is behind it (-z). The beam spreads at the spot's half
        // angle and fades out over BeamLength, well short of the orb and of the button post by the right hand (at .30 the
        // right lamp's beam ran through it).
        public const float Lens=.024f,Body=.0165f,BeamLength=.20f,BeamAlpha=.16f;
        public const int Sides=28;
        static readonly Color Enamel=MemoryPalette.Hex(0x26323C),Brass=MemoryPalette.Hex(0xC2964A),BrassDark=MemoryPalette.Hex(0x8C6A33),LensOff=MemoryPalette.Hex(0x3B352B),LensOn=MemoryPalette.Hex(0xFFF0CF);
        static readonly Color BeamColor=MemoryPalette.Hex(0xFFE7B8);
        Func<float> level;
        Renderer lens,beam;
        float shown=-1;
        MaterialPropertyBlock block;
        public Renderer LensRenderer=>lens;
        public Renderer Beam=>beam;

        // level: 0 lamp off .. 1 on. owned collects the meshes, materials and texture made here.
        public static MemoryFlashlight Add(Transform head,MemoryKit kit,Material glow,Func<float> level)
        {
            var root=new GameObject("Flashlight").transform;root.SetParent(head,false);
            var light=root.gameObject.AddComponent<MemoryFlashlight>();light.level=level;light.Build(kit,glow,kit.Owned);light.Update();return light;
        }
        void Build(MemoryKit kit,Material glow,List<UnityEngine.Object> owned)
        {
            block=new MaterialPropertyBlock();
            // Back to front: tail cap, body, the head flaring out to the bezel.
            Part("Tail cap",Band(owned,.0185f,-.19f,.0185f,-.172f,true,true),kit.Surface,Brass);
            Part("Body",Band(owned,Body,-.172f,Body,-.062f,false,false),kit.Surface,Enamel);
            for(int i=0;i<3;i++){float z=-.152f+i*.016f;Part("Grip ring",Band(owned,Body+.0012f,z,Body+.0012f,z+.0045f,true,true),kit.Surface,BrassDark);}
            Part("Head",Band(owned,Body,-.062f,.026f,-.018f,false,false),kit.Surface,Enamel);
            Part("Bezel",Band(owned,.0285f,-.018f,.0285f,0,true,false),kit.Surface,Brass);
            Part("Bezel face",Ring(owned,Lens,.0285f,0),kit.Surface,Brass);
            var button=GameObject.CreatePrimitive(PrimitiveType.Cube);button.name="Switch";MemoryUi.Kill(button.GetComponent<Collider>());button.transform.SetParent(transform,false);
            button.transform.localPosition=new Vector3(0,Body+.0018f,-.092f);button.transform.localScale=new Vector3(.009f,.006f,.016f);Style(button.GetComponent<Renderer>(),kit.Surface,Brass);
            lens=Part("Lens",Ring(owned,0,Lens,.0004f),glow,LensOff);
            if(!kit.SeeThrough)return;
            // A cone of light leaving the lens, see-through and fading along its length (alpha from a small ramp texture).
            float far=Lens+BeamLength*Mathf.Tan(Torch2Projection.HalfAngle*Mathf.Deg2Rad);
            var material=new Material(kit.SeeThrough){name="Flashlight beam"};owned.Add(material);
            material.SetTexture("_BaseMap",Ramp(owned));material.SetFloat("_Cull",(float)CullMode.Off);material.SetFloat("_ZWrite",0);material.renderQueue=3050;
            beam=Part("Beam",Cone(owned,Lens*.9f,.002f,far,BeamLength),material,MemoryPalette.Alpha(BeamColor,0));
        }
        Renderer Part(string name,Mesh mesh,Material material,Color color)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.GetComponent<MeshRenderer>();Style(r,material,color);return r;
        }
        void Style(Renderer r,Material material,Color color){r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;MemoryUi.Tint(r,color);}
        void Update()
        {
            float on=level!=null?Mathf.Clamp01(level()):0;if(Mathf.Approximately(on,shown))return;shown=on;
            if(lens){block.Clear();block.SetColor("_BaseColor",Color.Lerp(LensOff,LensOn,on));lens.SetPropertyBlock(block);}
            if(beam){block.Clear();block.SetColor("_BaseColor",MemoryPalette.Alpha(BeamColor,BeamAlpha*on));beam.SetPropertyBlock(block);beam.enabled=on>.01f;}
        }

        // ---- Meshes, turned about +z. A front face is wound so Cross(b - a, c - a) points out of the solid.
        // The side of a frustum from radius r0 at z0 to r1 at z1, with flat caps where asked.
        static Mesh Band(List<UnityEngine.Object> owned,float r0,float z0,float r1,float z1,bool backCap,bool frontCap)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();
            float dz=z1-z0,dr=r1-r0,len=Mathf.Sqrt(dz*dz+dr*dr);float radial=dz/len,axial=-dr/len;
            for(int i=0;i<=Sides;i++)
            {
                float a=i*Mathf.PI*2/Sides;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);var normal=(d*radial+Vector3.forward*axial).normalized;
                v.Add(d*r0+Vector3.forward*z0);n.Add(normal);v.Add(d*r1+Vector3.forward*z1);n.Add(normal);
            }
            for(int i=0;i<Sides;i++){int a=2*i;Quad(v,t,a,a+2,a+3,a+1,n[a]);}
            if(backCap)Disc(v,n,t,r0,z0,-1);
            if(frontCap)Disc(v,n,t,r1,z1,1);
            return Make(owned,"Flashlight band",v,n,t);
        }
        // A flat annulus facing +z at z (inner 0: a disc).
        static Mesh Ring(List<UnityEngine.Object> owned,float inner,float outer,float z)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=Sides;i++){float a=i*Mathf.PI*2/Sides;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);v.Add(d*inner+Vector3.forward*z);v.Add(d*outer+Vector3.forward*z);n.Add(Vector3.forward);n.Add(Vector3.forward);}
            for(int i=0;i<Sides;i++){int a=2*i;Quad(v,t,a,a+1,a+3,a+2,Vector3.forward);}
            return Make(owned,"Flashlight ring",v,n,t);
        }
        // The beam: an open cone from r0 at z0 to r1 at length, v along it (0 at the lens).
        static Mesh Cone(List<UnityEngine.Object> owned,float r0,float z0,float r1,float length)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int i=0;i<=Sides;i++)
            {
                float a=i*Mathf.PI*2/Sides;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                v.Add(d*r0+Vector3.forward*z0);v.Add(d*r1+Vector3.forward*length);n.Add(d);n.Add(d);uv.Add(new Vector2(i/(float)Sides,0));uv.Add(new Vector2(i/(float)Sides,1));
            }
            for(int i=0;i<Sides;i++){int a=2*i;Quad(v,t,a,a+2,a+3,a+1,n[a]);}
            var mesh=Make(owned,"Flashlight beam",v,n,t);mesh.SetUVs(0,uv);return mesh;
        }
        static void Disc(List<Vector3> v,List<Vector3> n,List<int> t,float r,float z,float facing)
        {
            int centre=v.Count;var normal=Vector3.forward*facing;v.Add(Vector3.forward*z);n.Add(normal);
            for(int i=0;i<=Sides;i++){float a=i*Mathf.PI*2/Sides;v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,z));n.Add(normal);}
            for(int i=0;i<Sides;i++)Tri(v,t,centre,centre+1+i,centre+2+i,normal);
        }
        static void Quad(List<Vector3> v,List<int> t,int a,int b,int c,int d,Vector3 normal){Tri(v,t,a,b,c,normal);Tri(v,t,a,c,d,normal);}
        static void Tri(List<Vector3> v,List<int> t,int a,int b,int c,Vector3 normal)
        {
            if(Vector3.Dot(Vector3.Cross(v[b]-v[a],v[c]-v[a]),normal)<0){int s=b;b=c;c=s;}
            t.Add(a);t.Add(b);t.Add(c);
        }
        static Mesh Make(List<UnityEngine.Object> owned,string name,List<Vector3> v,List<Vector3> n,List<int> t)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetTriangles(t,0);mesh.RecalculateBounds();owned.Add(mesh);return mesh;
        }
        // Alpha along the beam: full at the lens, gone by the end.
        static Texture2D Ramp(List<UnityEngine.Object> owned)
        {
            const int h=64;var texture=new Texture2D(2,h,TextureFormat.RGBA32,false){name="Flashlight beam ramp",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[2*h];
            for(int y=0;y<h;y++){float f=y/(h-1f);byte a=(byte)Mathf.RoundToInt(255*Mathf.Pow(1-f,1.7f)*Mathf.SmoothStep(0,1,f/.06f));pixels[2*y]=pixels[2*y+1]=new Color32(255,255,255,a);}
            texture.SetPixels32(pixels);texture.Apply(false,false);owned.Add(texture);return texture;
        }
    }
}
