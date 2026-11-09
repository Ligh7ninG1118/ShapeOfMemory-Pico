using System;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    [Serializable]
    public struct ShadowPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public ShadowPose(Vector3 p, Quaternion r) { position = p; rotation = r; }
        public Matrix4x4 Matrix => Matrix4x4.TRS(position, rotation, Vector3.one);
    }

    public struct ShadowScore
    {
        public float iou, spill, hullSpill;
        public float hull, sail, mast;
        public bool HullReady => hull >= .82f && hullSpill <= .20f;
        public bool Complete => iou >= .78f && hull >= .82f && sail >= .80f && mast >= .70f && spill <= .15f;
    }

    // The exact same projected triangles feed the display and scoring. No head-camera dependency.
    public sealed class ShadowProjection
    {
        public const int Size = 128;
        public const float Width = .72f, Height = .64f;
        public static readonly Vector3 Lamp = new Vector3(0, .32f, -.5f);
        public const float ScreenZ = .5f;
        public readonly Mesh[] meshes = new Mesh[3];
        public readonly bool[][] regions = { new bool[Size * Size], new bool[Size * Size], new bool[Size * Size] };
        public readonly bool[] target = new bool[Size * Size], tolerant = new bool[Size * Size], current = new bool[Size * Size];
        readonly Vector3[][] vertices = new Vector3[3][];
        readonly int[][] triangles = new int[3][];
        readonly Vector2[] projected = new Vector2[32];
        readonly bool[] hullTolerance = new bool[Size * Size];
        public static readonly ShadowPose[] Solution = {
            new ShadowPose(new Vector3(0, .26f, -.06f), Quaternion.identity),
            new ShadowPose(new Vector3(.01f, .335f, .07f), Quaternion.identity),
            new ShadowPose(new Vector3(-.0395f, .35f, 0), Quaternion.identity)
        };
        public static readonly ShadowPose[] Tray = {
            new ShadowPose(new Vector3(-.12f, .23f, .02f), Quaternion.Euler(24, 38, -18)),
            new ShadowPose(new Vector3(.18f, .15f, .04f), Quaternion.Euler(-32, 48, 34)),
            new ShadowPose(new Vector3(.29f, .16f, .02f), Quaternion.Euler(20, -35, 65))
        };

        public ShadowProjection()
        {
            meshes[0] = Prism("Beveled hull block", new[] { new Vector2(-.07f,.024f), new Vector2(-.043f,-.025f), new Vector2(.043f,-.025f), new Vector2(.07f,.024f) }, new[] {0,1,2,0,2,3}, .028f);
            meshes[1] = Prism("Triangle wedge", new[] { new Vector2(-.055f,-.06f), new Vector2(.055f,-.06f), new Vector2(-.055f,.06f) }, new[] {0,1,2}, .035f);
            meshes[2] = Prism("L bracket", new[] { new Vector2(-.008f,-.07f),new Vector2(.008f,-.07f),new Vector2(.008f,.054f),new Vector2(.045f,.054f),new Vector2(.045f,.07f),new Vector2(-.008f,.07f) }, new[] {0,1,2,0,2,5,2,3,4,2,4,5}, .022f);
            for (int i=0;i<3;i++)
            {
                vertices[i]=meshes[i].vertices; triangles[i]=meshes[i].triangles;
                Rasterize(i,Solution[i],regions[i]);
                for(int p=0;p<target.Length;p++) target[p]|=regions[i][p];
            }
            Dilate(target,tolerant,2); Dilate(regions[0],hullTolerance,2);
        }

        static Mesh Prism(string name, Vector2[] polygon, int[] face, float depth)
        {
            int n=polygon.Length;var v=new Vector3[n*2];
            for(int i=0;i<n;i++){v[i]=new Vector3(polygon[i].x,polygon[i].y,-depth/2);v[i+n]=v[i]+Vector3.forward*depth;}
            var t=new int[face.Length*2+n*6];int k=0;
            for(int i=0;i<face.Length;i+=3){t[k++]=face[i+2];t[k++]=face[i+1];t[k++]=face[i];t[k++]=face[i]+n;t[k++]=face[i+1]+n;t[k++]=face[i+2]+n;}
            for(int i=0;i<n;i++){int j=(i+1)%n;t[k++]=i;t[k++]=j;t[k++]=j+n;t[k++]=i;t[k++]=j+n;t[k++]=i+n;}
            // Duplicate vertices for crisp flat-shaded graybox edges.
            var flat=new Vector3[t.Length];var indices=new int[t.Length];
            for(int i=0;i<t.Length;i++){flat[i]=v[t[i]];indices[i]=i;}
            var mesh=new Mesh{name=name};mesh.vertices=flat;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        public static bool InWorkspace(ShadowPose pose) => Mathf.Abs(pose.position.x)<=.32f && pose.position.y>=.08f && pose.position.y<=.60f && pose.position.z>=-.16f && pose.position.z<=.20f;
        public void Render(ShadowPose[] poses, bool[] enabled)
        {
            Array.Clear(current,0,current.Length);
            for(int i=0;i<3;i++) if(enabled[i]&&InWorkspace(poses[i])) Rasterize(i,poses[i],current);
        }
        public Vector2 Project(Vector3 p)
        {
            float scale=(ScreenZ-Lamp.z)/(p.z-Lamp.z);
            return new Vector2((p.x-Lamp.x)*scale/Width+.5f,(p.y-Lamp.y)*scale/Height+.5f)*Size;
        }
        void Rasterize(int part, ShadowPose pose, bool[] output)
        {
            var v=vertices[part];var t=triangles[part];var matrix=pose.Matrix;
            // Process per triangle; small meshes and persistent buffers avoid readback and GC.
            for(int j=0;j<t.Length;j+=3)
            {
                for(int c=0;c<3;c++)projected[c]=Project(matrix.MultiplyPoint3x4(v[t[j+c]]));
                var a=projected[0];var b=projected[1];var c0=projected[2];float area=Cross(b-a,c0-a);if(Mathf.Abs(area)<.001f)continue;
                int minX=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c0.x))),0,Size-1),maxX=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c0.x))),0,Size-1);
                int minY=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c0.y))),0,Size-1),maxY=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c0.y))),0,Size-1);
                for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
                {
                    var p=new Vector2(x+.5f,y+.5f);float e0=Cross(b-a,p-a),e1=Cross(c0-b,p-b),e2=Cross(a-c0,p-c0);
                    if(area>0?e0>=0&&e1>=0&&e2>=0:e0<=0&&e1<=0&&e2<=0)output[y*Size+x]=true;
                }
            }
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static void Dilate(bool[] source,bool[] output,int radius)
        {
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)if(source[y*Size+x])
                for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)
                    if(x+dx>=0&&x+dx<Size&&y+dy>=0&&y+dy<Size)output[(y+dy)*Size+x+dx]=true;
        }
        public ShadowScore Score()
        {
            int total=0,intersection=0,union=0,spill=0,hullSpill=0;
            int h=0,s=0,m=0,hc=0,sc=0,mc=0;
            for(int p=0;p<target.Length;p++)
            {
                bool a=target[p],b=current[p];if(a)total++;if(a&&b)intersection++;if(a||b)union++;if(b&&!tolerant[p])spill++;if(b&&!hullTolerance[p])hullSpill++;
                if(regions[0][p]){h++;if(b)hc++;}if(regions[1][p]){s++;if(b)sc++;}if(regions[2][p]){m++;if(b)mc++;}
            }
            return new ShadowScore{iou=intersection/(float)Mathf.Max(1,union),spill=spill/(float)Mathf.Max(1,total),hullSpill=hullSpill/(float)Mathf.Max(1,h),hull=hc/(float)Mathf.Max(1,h),sail=sc/(float)Mathf.Max(1,s),mast=mc/(float)Mathf.Max(1,m)};
        }
        public void Dispose(){foreach(var mesh in meshes)if(mesh){if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}}
    }
}
