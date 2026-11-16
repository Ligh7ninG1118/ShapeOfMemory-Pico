using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Detailed look for the Torch2 'Strawberry' prop: a smooth, slightly lobed berry whose seeds sit in shallow pits
    // painted into its texture (so they cost no triangles), seven pointed sepals over the shoulders and a short stem.
    // Drawn only: the original 10-sided 'Strawberry' mesh stays the piece's shadow, so the puzzle's picture and scoring
    // are unchanged. Same frame and size as the original: tip at y = -23 mm, 19 mm round at y = 1 mm, the calyx reaching
    // about 14 mm out over the top, the stem up to about y = 22 mm.
    public static class StrawberryBuilder
    {
        public const string MeshPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Strawberry detailed.asset";
        public const string ShadowPath="Assets/ShadowWorkshop/Art/Torch2/Meshes/Strawberry.asset";
        const string MaterialFolder="Assets/ShadowWorkshop/Art/Torch2/Materials/";
        const string SkinPath="Assets/ShadowWorkshop/Art/Torch2/Textures/strawberry skin.png";
        const int Skin=0,Leaf=1;   // submeshes, in the order of Materials()
        // VR mobile budget (about 650 triangles): smooth normals do the rounding and the seeds are texture. Outline rows
        // go where it turns (one per TurnStep degrees), none along its straight runs.
        const int Sides=18,Sepals=7,SepalSteps=4,StemSides=6;const float TurnStep=18;
        const float Lobes=5,LobeDepth=.025f;   // gentle unevenness round the berry, fading out at the tip and the top
        // Outline from the tip up to the dimple under the stem (radius, height), smoothed by Catmull-Rom.
        static readonly Vector2[] Outline={new Vector2(0,-.0230f),new Vector2(.0100f,-.0170f),new Vector2(.0190f,.0010f),new Vector2(.0170f,.0130f),new Vector2(.0090f,.0166f),new Vector2(0,.0160f)};
        const int Dense=32;   // samples a span of the outline, for lengths and lookups
        // Seeds: rows SeedPitch apart along the outline, from SeedTipGap above the tip up to height SeedTop (under the calyx).
        const float SeedPitch=.0031f,SeedLength=.00055f,SeedWidth=.00036f,PitRadius=.00105f,SeedTop=.0143f,SeedTipGap=.0022f;
        const int TexW=1024,TexH=512;

        // The skin texture is rewritten in place (same GUID) on every build, so editing SkinTexture takes effect.
        [MenuItem("MetaHackthone/Shadow Workshop/Build strawberry")]
        public static void Build()
        {
            SkinTexture();var mesh=BuildMesh();var materials=Materials();
            Debug.Log($"STRAWBERRY_OK verts={mesh.vertexCount} tris={mesh.triangles.Length/3} submeshes={mesh.subMeshCount} materials={materials.Length}");
        }
        // The new textured skin, then the original leaf green for the calyx and stem.
        public static Material[] Materials()
        {
            var skin=PropMesh.Materials(MaterialFolder,new[]{new Look("strawberry skin",Color.white,0f,.25f){baseMap=()=>AssetDatabase.LoadAssetAtPath<Texture2D>(SkinPath)??SkinTexture()}})[0];
            return new[]{skin,AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+"leaf.mat")};
        }

        public static Mesh BuildMesh()
        {
            var b=new PropMesh(2);var (dense,arc)=Sampled();
            Berry(b,arc);Calyx(b,dense);
            b.Lathe(new Vector3(0,.0158f,0),new Vector3(.10f,1,.06f),StemSides,P(.0017f,0,Leaf),P(.0013f,.0046f,Leaf),P(.0011f,.0062f,Leaf),P(0,.0065f,Leaf));
            return b.Save(MeshPath,"Strawberry detailed");
        }

        // --- Outline ---------------------------------------------------------------------------------------------------
        // Span i (Outline[i] to Outline[i+1]) at t. The ends sit on the axis, so their neighbours are mirrored across it
        // and the outline meets the axis square: a round tip and a flat-bottomed dimple.
        static Vector2 At(int i,float t)
        {
            int last=Outline.Length-1;static Vector2 Mirror(Vector2 p)=>new Vector2(-p.x,p.y);
            var p0=i>0?Outline[i-1]:Mirror(Outline[1]);var p3=i+2<=last?Outline[i+2]:Mirror(Outline[last-1]);
            return CatmullRom(p0,Outline[i],Outline[i+1],p3,t);
        }
        // The outline densely sampled, with the arc length from the tip at each sample.
        static (Vector2[] p,float[] s) Sampled()
        {
            int spans=Outline.Length-1;var p=new Vector2[spans*Dense+1];var s=new float[p.Length];
            for(int i=0;i<spans;i++)for(int k=0;k<Dense;k++)p[i*Dense+k]=At(i,(float)k/Dense);
            p[p.Length-1]=Outline[spans];
            for(int k=1;k<p.Length;k++)s[k]=s[k-1]+Vector2.Distance(p[k-1],p[k]);
            return (p,s);
        }
        static Vector2 CatmullRom(Vector2 p0,Vector2 p1,Vector2 p2,Vector2 p3,float t)   // centripetal, between p1 and p2
        {
            float t0=0,t1=t0+Mathf.Sqrt(Mathf.Max(Vector2.Distance(p0,p1),1e-7f)),t2=t1+Mathf.Sqrt(Mathf.Max(Vector2.Distance(p1,p2),1e-7f)),t3=t2+Mathf.Sqrt(Mathf.Max(Vector2.Distance(p2,p3),1e-7f));
            float u=Mathf.Lerp(t1,t2,t);
            var a1=(t1-u)/(t1-t0)*p0+(u-t0)/(t1-t0)*p1;var a2=(t2-u)/(t2-t1)*p1+(u-t1)/(t2-t1)*p2;var a3=(t3-u)/(t3-t2)*p2+(u-t2)/(t3-t2)*p3;
            var b1=(t2-u)/(t2-t0)*a1+(u-t0)/(t2-t0)*a2;var b2=(t3-u)/(t3-t1)*a2+(u-t1)/(t3-t1)*a3;
            return (t2-u)/(t2-t1)*b1+(u-t1)/(t2-t1)*b2;
        }
        static float Smooth(float from,float to,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,x));

        // --- Berry -----------------------------------------------------------------------------------------------------
        // The outline swept round y, Lobes bumps round it; normals from the surface itself (smooth both ways). u runs
        // round (seam at angle 0), v up the outline by arc length, matching SkinTexture.
        static void Berry(PropMesh b,float[] arc)
        {
            float length=arc[arc.Length-1];var rows=new List<(Vector2 p,float s)>{(Outline[0],0)};
            for(int i=0;i+1<Outline.Length;i++)
            {
                float turn=Vector2.Angle(At(i,.01f)-At(i,0),At(i,1)-At(i,.99f));int steps=Mathf.Clamp(Mathf.CeilToInt(turn/TurnStep),1,6);
                for(int k=1;k<=steps;k++){float t=(float)k/steps,x=(i+t)*Dense;int a=Mathf.Min((int)x,arc.Length-2);rows.Add((At(i,t),Mathf.Lerp(arc[a],arc[a+1],x-a)));}
            }
            int count=rows.Count,cols=Sides+1;var pos=new Vector3[count,cols];
            for(int r=0;r<count;r++)for(int k=0;k<cols;k++)
            {
                float angle=k*2*Mathf.PI/Sides,vn=rows[r].s/length;
                float radius=rows[r].p.x*(1+LobeDepth*Mathf.Sin(Mathf.PI*vn)*Mathf.Sin(Lobes*angle+1.3f*vn));
                pos[r,k]=new Vector3(radius*Mathf.Cos(angle),rows[r].p.y,radius*Mathf.Sin(angle));
            }
            int start=b.v.Count;
            for(int r=0;r<count;r++)for(int k=0;k<cols;k++)
            {
                bool pole=rows[r].p.x<1e-6f;Vector3 n;
                if(pole)n=r==0?Vector3.down:Vector3.up;
                else
                {
                    var along=pos[r+1,k]-pos[r-1,k];var round=pos[r,(k+1)%Sides]-pos[r,(k+Sides-1)%Sides];n=Vector3.Cross(along,round).normalized;
                    // Outside is where the outline's own normal (dh, -dr) points.
                    var d=rows[r+1].p-rows[r-1].p;var radial=new Vector3(pos[r,k].x,0,pos[r,k].z).normalized;
                    if(Vector3.Dot(n,radial*d.y-Vector3.up*d.x)<0)n=-n;
                }
                b.Add(pos[r,k],n,new Vector2((k+(pole?.5f:0))/Sides,rows[r].s/length));
            }
            for(int r=0;r+1<count;r++)for(int k=0;k<Sides;k++){int q=start+r*cols+k;b.Quad(Skin,q,q+cols,q+cols+1,q+1);}
        }

        // --- Calyx -----------------------------------------------------------------------------------------------------
        // Sepals: pointed leaves from the stem out over the shoulders, just above the skin, their tips curling up. Each is
        // a strip three points across (the midrib a little higher than the edges), drawn on both sides.
        static void Calyx(PropMesh b,Vector2[] dense)
        {
            float SurfaceY(float rho)   // height of the berry's top at radius rho, searched from the dimple outwards
            {
                for(int k=dense.Length-1;k>0;k--){Vector2 a=dense[k],c=dense[k-1];if((a.x-rho)*(c.x-rho)<=0&&a.x!=c.x)return Mathf.Lerp(a.y,c.y,(rho-a.x)/(c.x-a.x));}
                return dense[dense.Length-1].y;
            }
            var rnd=new System.Random(7);const int across=3;const float lift=.0005f,thick=.00015f;
            for(int k=0;k<Sepals;k++)
            {
                float angle=(k+(float)rnd.NextDouble()*.3f)*2*Mathf.PI/Sepals,reach=Mathf.Lerp(.0125f,.0142f,(float)rnd.NextDouble()),curl=Mathf.Lerp(.0012f,.0024f,(float)rnd.NextDouble());
                var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=new Vector3(-radial.z,0,radial.x);
                var grid=new Vector3[SepalSteps+1,across];
                for(int i=0;i<=SepalSteps;i++)
                {
                    float s=(float)i/SepalSteps,rho=Mathf.Lerp(.0012f,reach,s),half=SepalHalfWidth(s),y=SurfaceY(rho)+lift+curl*Mathf.Pow(s,2.2f);
                    for(int c=0;c<across;c++){float o=c-1;grid[i,c]=radial*rho+Vector3.up*(y-.00045f*Mathf.Abs(o)*half/.0019f)+side*(o*half);}
                }
                int top=b.v.Count;
                foreach(var face in new[]{1f,-1f})
                    for(int i=0;i<=SepalSteps;i++)for(int c=0;c<across;c++)
                    {
                        var along=grid[Mathf.Min(i+1,SepalSteps),c]-grid[Mathf.Max(i-1,0),c];var wide=grid[i,Mathf.Min(c+1,across-1)]-grid[i,Mathf.Max(c-1,0)];
                        if(wide.sqrMagnitude<1e-14f)wide=side;
                        var n=Vector3.Cross(wide,along).normalized;if(n.y<0)n=-n;n*=face;
                        b.Add(grid[i,c]+n*thick,n,new Vector2((float)c/(across-1),(float)i/SepalSteps));
                    }
                int bottom=top+(SepalSteps+1)*across;
                foreach(var start in new[]{top,bottom})
                    for(int i=0;i<SepalSteps;i++)for(int c=0;c+1<across;c++){int q=start+i*across+c;b.Quad(Leaf,q,q+across,q+across+1,q+1);}
            }
        }
        static float SepalHalfWidth(float s)=>s<.4f?Mathf.Lerp(.0008f,.0019f,s/.4f):.0019f*Mathf.Pow(Mathf.Max(0,1-(s-.4f)/.6f),.9f);

        // --- Skin texture ----------------------------------------------------------------------------------------------
        // TexW x TexH over the berry as Berry maps it: u round, v from the tip (0) up to the dimple (1). Rows of seeds
        // SeedPitch apart along the outline, each with as many as fit round it and every other row half a step on, so they
        // spread evenly over the skin; each seed sits in a darker pit. Deep red at the tip, paler under the calyx.
        static Texture2D SkinTexture()
        {
            var (dense,arc)=Sampled();float length=arc[arc.Length-1];
            float Along(float s,bool height)
            {
                int k=System.Array.BinarySearch(arc,s);if(k>=0)return height?dense[k].y:dense[k].x;k=~k;
                if(k<=0)return height?dense[0].y:dense[0].x;if(k>=arc.Length)return height?dense[arc.Length-1].y:dense[arc.Length-1].x;
                var p=Vector2.Lerp(dense[k-1],dense[k],(s-arc[k-1])/(arc[k]-arc[k-1]));return height?p.y:p.x;
            }
            int rowCount=Mathf.CeilToInt(length/SeedPitch)+2;var perRow=new int[rowCount];
            for(int r=0;r<rowCount;r++){float s=(r+.5f)*SeedPitch;perRow[r]=s<SeedTipGap||s>length||Along(s,true)>SeedTop?0:Mathf.Max(3,Mathf.RoundToInt(2*Mathf.PI*Along(s,false)/SeedPitch));}
            static float Hash(int a,int b){unchecked{int h=a*374761393+b*668265263+1013;h=(h^(h>>13))*1274126177;return ((h^(h>>16))&0xffff)/65535f;}}
            Color tip=new Color(.55f,.03f,.06f),body=new Color(.78f,.06f,.09f),shoulder=new Color(.86f,.13f,.11f),pale=new Color(.93f,.47f,.32f);
            Color Pixel(float u,float s)
            {
                float vn=s/length,circ=2*Mathf.PI*Along(s,false);
                var skin=vn<.35f?Color.Lerp(tip,body,vn/.35f):vn<.8f?Color.Lerp(body,shoulder,(vn-.35f)/.45f):Color.Lerp(shoulder,pale,(vn-.8f)/.2f);
                skin*=.9f+.2f*Fbm(u,vn,8,4,3,41);
                float seed=99,pit=99;int r0=Mathf.FloorToInt(s/SeedPitch-.5f);
                for(int r=Mathf.Max(0,r0-1);r<=Mathf.Min(rowCount-1,r0+2);r++)
                {
                    int n=perRow[r];if(n==0)continue;float sr=(r+.5f)*SeedPitch,offset=(r&1)*.5f;int j0=Mathf.RoundToInt(u*n-offset);
                    for(int j=j0-1;j<=j0+1;j++)
                    {
                        int jw=(j%n+n)%n;float su=(jw+offset+(Hash(r,jw)-.5f)*.45f)/n,ss=sr+(Hash(jw,r+77)-.5f)*.35f*SeedPitch;
                        float du=u-su;du-=Mathf.Round(du);float dx=du*circ,dy=s-ss;
                        seed=Mathf.Min(seed,Mathf.Sqrt(dx*dx/(SeedWidth*SeedWidth)+dy*dy/(SeedLength*SeedLength)));
                        pit=Mathf.Min(pit,Mathf.Sqrt(dx*dx+dy*dy)/PitRadius);
                    }
                }
                skin*=Mathf.Lerp(.62f,1,Smooth(.3f,1,pit));
                var seedColor=Color.Lerp(new Color(.98f,.86f,.42f),new Color(.72f,.52f,.16f),Smooth(.2f,1,seed));
                seedColor=Color.Lerp(seedColor,new Color(.76f,.80f,.36f),Smooth(.75f,.95f,vn));
                var c=Color.Lerp(skin,seedColor,Smooth(1,.82f,seed));c.a=1;return c;
            }
            var px=new Color[TexW*TexH];
            for(int y=0;y<TexH;y++)for(int x=0;x<TexW;x++)
            {
                var c=Color.clear;
                for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++)c+=Pixel((x+.25f+.5f*sx)/TexW,(y+.25f+.5f*sy)/TexH*length);
                c*=.25f;c.a=1;px[y*TexW+x]=c;
            }
            var tex=new Texture2D(TexW,TexH,TextureFormat.RGB24,false,false);tex.SetPixels(px);tex.Apply();
            SaveTexture(SkinPath,tex,false);
            // Round the berry the texture wraps; along it, it stops at the tip and the dimple.
            var importer=(TextureImporter)AssetImporter.GetAtPath(SkinPath);
            if(importer.wrapModeV!=TextureWrapMode.Clamp){importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SkinPath);
        }
    }
}
