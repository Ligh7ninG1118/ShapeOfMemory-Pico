using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    [Serializable] public class WishesCell { public float[] vertices; public int[] triangles; }
    [Serializable] public class WishesPart { public string name; public int band; public float[] position,vertices; public WishesCell[] colliders; }
    [Serializable] public class WishesData { public int version; public WishesPart[] pieces; }
    public struct WishesScore
    {
        public float iou,coverage,spill,windowClear;
        public bool Ready=>iou>=.84f&&coverage>=.90f&&spill<=.08f&&windowClear>=.80f;
    }
    public sealed class WishesProjection : IDisposable
    {
        public const int Count=12,Size=128;
        public const float Span=.56f;
        public static readonly Vector3 Center=new Vector3(0,.29f,0);
        // Camera/light at left-front has negative x; panels have their own inward-facing transforms.
        public static readonly Quaternion[] Views={Quaternion.identity,Quaternion.Euler(0,45,0),Quaternion.Euler(0,-45,0)};
        public static readonly ShadowPose[] Tray=MakeTray();
        public readonly ShadowPose[] Solution=new ShadowPose[Count];
        public readonly Mesh[] meshes=new Mesh[Count];
        public readonly List<Mesh>[] convexMeshes=new List<Mesh>[Count];
        public readonly Bounds[] localBounds=new Bounds[Count];
        public readonly WishesData data;
        public readonly bool[][] target=Masks(),current=Masks(),tolerance=Masks(),windows=Masks();
        public readonly WishesScore[] scores=new WishesScore[3];
        readonly Vector3[][] vertices=new Vector3[Count][];
        readonly Vector2[] projected=new Vector2[3];
        readonly Bounds[] assemblyBounds=new Bounds[Count];
        readonly bool[] reached=new bool[Count];
        readonly bool[][][] pieceMasks=new bool[Count][][];
        readonly ShadowPose[] cachedPoses=new ShadowPose[Count];
        readonly bool[] cached=new bool[Count];
        static bool[][] Masks()=>new[]{new bool[Size*Size],new bool[Size*Size],new bool[Size*Size]};
        static ShadowPose[] MakeTray()
        {
            var result=new ShadowPose[Count];
            for(int i=0;i<Count;i++){int slot=i%6;result[i]=new ShadowPose(new Vector3((i<6?-1:1)*(.36f+(slot%2)*.18f),.14f,-.17f+(slot/2)*.20f),Quaternion.Euler(0,(i%4)*90,0));}
            return result;
        }
        public WishesProjection()
        {
            var json=Resources.Load<TextAsset>("ShadowArt/ThreeWishes");if(!json)throw new InvalidOperationException("Missing ThreeWishes mesh data");
            data=JsonUtility.FromJson<WishesData>(json.text);if(data.version!=1||data.pieces.Length!=Count)throw new InvalidOperationException("Invalid ThreeWishes mesh data");
            for(int i=0;i<Count;i++)
            {
                var part=data.pieces[i];Solution[i]=new ShadowPose(new Vector3(part.position[0],part.position[1],part.position[2]),Quaternion.identity);
                vertices[i]=Vectors(part.vertices);var indices=new int[vertices[i].Length];for(int t=0;t<indices.Length;t++)indices[t]=t;
                meshes[i]=Mesh(part.name,vertices[i],indices);localBounds[i]=meshes[i].bounds;convexMeshes[i]=new List<Mesh>();
                pieceMasks[i]=Masks();
                foreach(var cell in part.colliders)convexMeshes[i].Add(Mesh("Pick surface",Vectors(cell.vertices),cell.triangles));
            }
            for(int v=0;v<3;v++)
            {
                for(int i=0;i<Count;i++)Raster(i,Solution[i],v,target[v]);
                for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)if(target[v][y*Size+x])
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(x+dx>=0&&x+dx<Size&&y+dy>=0&&y+dy<Size)tolerance[v][(y+dy)*Size+x+dx]=true;
                FindWindows(target[v],windows[v]);
            }
        }
        static Vector3[] Vectors(float[] values){var result=new Vector3[values.Length/3];for(int i=0;i<result.Length;i++)result[i]=new Vector3(values[i*3],values[i*3+1],values[i*3+2]);return result;}
        static Mesh Mesh(string name,Vector3[] v,int[] triangles){var mesh=new Mesh{name=name};mesh.vertices=v;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        static void FindWindows(bool[] solid,bool[] windows)
        {
            var outside=new bool[solid.Length];var queue=new Queue<int>();
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)if((x==0||y==0||x==Size-1||y==Size-1)&&!solid[y*Size+x]){outside[y*Size+x]=true;queue.Enqueue(y*Size+x);}
            while(queue.Count>0)
            {
                int p=queue.Dequeue(),x=p%Size,y=p/Size;
                foreach(int offset in new[]{-1,1,-Size,Size})
                {
                    int n=p+offset;if(n<0||n>=solid.Length||(offset==-1&&x==0)||(offset==1&&x==Size-1))continue;
                    if(!outside[n]&&!solid[n]){outside[n]=true;queue.Enqueue(n);}
                }
            }
            // Exclude a one-pixel hole boundary to tolerate normal hand placement error.
            for(int y=1;y<Size-1;y++)for(int x=1;x<Size-1;x++)
            {int p=y*Size+x;windows[p]=!solid[p]&&!outside[p]&&!solid[p-1]&&!solid[p+1]&&!solid[p-Size]&&!solid[p+Size];}
        }
        public static bool InWorkspace(ShadowPose p)=>Mathf.Abs(p.position.x)<=.24f&&Mathf.Abs(p.position.z)<=.24f&&p.position.y>=.055f&&p.position.y<=.56f;
        public void Render(ShadowPose[] poses)
        {
            bool changed=false;
            for(int i=0;i<Count;i++)
            {
                if(cached[i]&&cachedPoses[i].position.Equals(poses[i].position)&&cachedPoses[i].rotation.Equals(poses[i].rotation))continue;
                changed=true;cached[i]=true;cachedPoses[i]=poses[i];
                for(int v=0;v<3;v++){Array.Clear(pieceMasks[i][v],0,Size*Size);if(InWorkspace(poses[i]))Raster(i,poses[i],v,pieceMasks[i][v]);}
            }
            if(!changed)return;
            for(int v=0;v<3;v++)
            {
                Array.Clear(current[v],0,current[v].Length);
                for(int i=0;i<Count;i++)if(InWorkspace(poses[i]))for(int p=0;p<Size*Size;p++)current[v][p]|=pieceMasks[i][v][p];
                int intersection=0,union=0,area=0,outside=0,windowArea=0,windowFilled=0;
                for(int p=0;p<Size*Size;p++)
                {
                    bool a=target[v][p],b=current[v][p];if(a)area++;if(a&&b)intersection++;if(a||b)union++;if(b&&!tolerance[v][p])outside++;
                    if(windows[v][p]){windowArea++;if(b)windowFilled++;}
                }
                scores[v]=new WishesScore{iou=intersection/(float)Mathf.Max(1,union),coverage=intersection/(float)Mathf.Max(1,area),spill=outside/(float)Mathf.Max(1,area),windowClear=1-windowFilled/(float)Mathf.Max(1,windowArea)};
            }
        }
        public bool ReadyThrough(int stage)
        {if(stage<0||stage>2)return false;for(int i=0;i<=stage;i++)if(!scores[i].Ready)return false;return true;}
        public bool[] PartMask(int part,int view)=>pieceMasks[part][view];
        public bool Complete(ShadowPose[] poses)=>ReadyThrough(2)&&Connected(poses);
        public bool Connected(ShadowPose[] poses)
        {
            Array.Clear(reached,0,Count);reached[0]=true;
            for(int i=0;i<Count;i++)
            {
                if(!InWorkspace(poses[i]))return false;var b=localBounds[i];assemblyBounds[i]=new Bounds(poses[i].position+poses[i].rotation*b.center,Vector3.zero);
                for(int c=0;c<8;c++)assemblyBounds[i].Encapsulate(poses[i].position+poses[i].rotation*(b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1))));
                assemblyBounds[i].Expand(.010f);
            }
            for(int pass=0;pass<Count;pass++)for(int i=0;i<Count;i++)if(reached[i])for(int j=0;j<Count;j++)if(assemblyBounds[i].Intersects(assemblyBounds[j]))reached[j]=true;
            foreach(bool r in reached)if(!r)return false;return true;
        }
        void Raster(int part,ShadowPose pose,int view,bool[] mask)
        {
            var v=vertices[part];var inverse=Quaternion.Inverse(Views[view]);var matrix=Matrix4x4.TRS(inverse*(pose.position-Center),inverse*pose.rotation,Vector3.one);
            for(int t=0;t<v.Length;t+=3)
            {
                for(int k=0;k<3;k++){var p=matrix.MultiplyPoint3x4(v[t+k]);projected[k]=new Vector2(p.x/Span+.5f,p.y/Span+.5f)*Size;}
                var a=projected[0];var b=projected[1];var c=projected[2];float area=Cross(b-a,c-a);if(Mathf.Abs(area)<.001f)continue;
                int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,Size-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,Size-1);
                int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,Size-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,Size-1);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    var p=new Vector2(x+.5f,y+.5f);float e0=Cross(b-a,p-a),e1=Cross(c-b,p-b),e2=Cross(a-c,p-c);
                    if(area>0?e0>=0&&e1>=0&&e2>=0:e0<=0&&e1<=0&&e2<=0)mask[y*Size+x]=true;
                }
            }
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        public void Dispose(){foreach(var mesh in meshes)Delete(mesh);foreach(var list in convexMeshes)if(list!=null)foreach(var mesh in list)Delete(mesh);}
        static void Delete(UnityEngine.Object obj){if(!obj)return;if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
    }
}
