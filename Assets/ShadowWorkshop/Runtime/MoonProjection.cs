using System;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    public struct MoonScore
    {
        public float iou, coverage, spill;
        public bool Ready => iou >= .84f && coverage >= .90f && spill <= .08f;
    }

    // Parallel rays: three fixed lights, independent of the player's head or selected panel.
    // Both the target and the live image are rasterized from the real, full-depth blocks.
    public sealed class MoonProjection
    {
        public const int Count = 9, Size = 96;
        public const float Span = .56f;
        public static readonly Vector3 Center = new Vector3(0,.285f,0);
        public static readonly Vector3[] Dimensions = {
            new Vector3(.12f,.14f,.10f), new Vector3(.14f,.10f,.12f),
            new Vector3(.035f,.11f,.045f), new Vector3(.035f,.11f,.045f),
            new Vector3(.065f,.055f,.13f), new Vector3(.065f,.055f,.13f),
            new Vector3(.045f,.10f,.07f), new Vector3(.045f,.10f,.07f), new Vector3(.10f,.10f,.06f)
        };
        public static readonly string[] Names = {"BODY","HEAD","EAR L","EAR R","BOOT L","BOOT R","ARM L","ARM R","PACK"};
        public static readonly ShadowPose[] Solution = {
            P(0,.22f,0),P(0,.34f,-.015f),P(-.045f,.445f,-.015f),P(.045f,.445f,-.015f),
            P(-.05f,.1225f,-.025f),P(.05f,.1225f,-.025f),P(-.0825f,.235f,-.015f),P(.0825f,.235f,.015f),P(0,.24f,.08f)
        };
        public static readonly ShadowPose[] Tray = MakeTray();
        public static readonly Quaternion[] Views = {Quaternion.identity,Quaternion.Euler(0,45,0),Quaternion.Euler(0,-45,0)};
        public readonly bool[][] target = Masks(), current = Masks(), tolerance = Masks();
        public readonly MoonScore[] scores = new MoonScore[3];
        static readonly Vector3[] corners = {new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
        static readonly int[] triangles = {0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,3,7,6,3,6,2,0,1,5,0,5,4};
        readonly Vector2[] projected = new Vector2[8];
        static ShadowPose P(float x,float y,float z)=>new ShadowPose(new Vector3(x,y,z),Quaternion.identity);
        static bool[][] Masks()=>new[]{new bool[Size*Size],new bool[Size*Size],new bool[Size*Size]};
        static ShadowPose[] MakeTray()
        {
            var result=new ShadowPose[Count];
            for(int i=0;i<Count;i++){int slot=i<5?i:i-5;result[i]=P((i<5?-1:1)*(.36f+(slot%2)*.17f),.12f+(slot/2)*.16f,.04f);}
            return result;
        }
        public MoonProjection()
        {
            for(int v=0;v<3;v++)
            {
                for(int i=0;i<Count;i++)Raster(i,Solution[i],Views[v],target[v]);
                for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)if(target[v][y*Size+x])
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(x+dx>=0&&x+dx<Size&&y+dy>=0&&y+dy<Size)tolerance[v][(y+dy)*Size+x+dx]=true;
            }
        }
        public static bool InWorkspace(ShadowPose pose)=>Mathf.Abs(pose.position.x)<=.23f&&Mathf.Abs(pose.position.z)<=.19f&&pose.position.y>=.06f&&pose.position.y<=.53f;
        public void Render(ShadowPose[] poses)
        {
            for(int v=0;v<3;v++)
            {
                Array.Clear(current[v],0,current[v].Length);
                for(int i=0;i<Count;i++)if(InWorkspace(poses[i]))Raster(i,poses[i],Views[v],current[v]);
                int intersection=0,union=0,area=0,outside=0;
                for(int p=0;p<Size*Size;p++){bool a=target[v][p],b=current[v][p];if(a)area++;if(a&&b)intersection++;if(a||b)union++;if(b&&!tolerance[v][p])outside++;}
                scores[v]=new MoonScore{iou=intersection/(float)Mathf.Max(1,union),coverage=intersection/(float)Mathf.Max(1,area),spill=outside/(float)Mathf.Max(1,area)};
            }
        }
        public bool AllReady(ShadowPose[] poses)=>scores[0].Ready&&scores[1].Ready&&scores[2].Ready&&Connected(poses);
        // Use all nine blocks and require a connected assembly. Silhouettes alone can hide a floating block.
        // AABB contact includes a small hand-placement tolerance; it is not a hidden answer-pose test.
        public static bool Connected(ShadowPose[] poses)
        {
            var bounds=new Bounds[Count];var reached=new bool[Count];reached[0]=true;
            for(int i=0;i<Count;i++)
            {
                if(!InWorkspace(poses[i]))return false;
                bounds[i]=new Bounds(poses[i].position,Vector3.zero);
                foreach(var c in corners)bounds[i].Encapsulate(poses[i].position+poses[i].rotation*Vector3.Scale(c,Dimensions[i]*.5f));
                bounds[i].Expand(.012f);
            }
            for(int pass=0;pass<Count;pass++)for(int i=0;i<Count;i++)if(reached[i])for(int j=0;j<Count;j++)if(bounds[i].Intersects(bounds[j]))reached[j]=true;
            foreach(bool r in reached)if(!r)return false;return true;
        }
        void Raster(int part,ShadowPose pose,Quaternion view,bool[] mask)
        {
            var inverse=Quaternion.Inverse(view);
            for(int i=0;i<8;i++)
            {
                var p=inverse*(pose.position+pose.rotation*Vector3.Scale(corners[i],Dimensions[part]*.5f)-Center);
                projected[i]=new Vector2(p.x/Span+.5f,p.y/Span+.5f)*Size;
            }
            for(int t=0;t<triangles.Length;t+=3)
            {
                var a=projected[triangles[t]];var b=projected[triangles[t+1]];var c=projected[triangles[t+2]];
                float area=Cross(b-a,c-a);if(Mathf.Abs(area)<.001f)continue;
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
    }
}
