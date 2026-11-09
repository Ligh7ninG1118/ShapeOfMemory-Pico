using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    public struct TorchScore
    {
        public float cover,spill,iou,warmth;
        public bool visible,Ready;
    }

    // Point light (the flashlight) through a turntable pile onto a fixed wall. The wall image is rasterised on the
    // CPU from the real triangles, then the visible shadow is normalised (centroid and RMS radius) before it is
    // compared, so only the picture's shape matters, not where on the wall it lands or how large it is.
    public sealed class TorchProjection
    {
        public const int Width=160,Height=120,Grid=64;
        public const float WallZ=.60f,WallX0=-.72f,WallY0=0,Pixel=.009f,HalfAngle=32,RmsCells=12;
        public static readonly Vector3 Axis=new Vector3(0,0,.05f),Center=new Vector3(0,.30f,.05f);
        public readonly TorchLevel level;
        public readonly bool[] shadow=new bool[Width*Height],visible=new bool[Width*Height];
        public readonly float[] brightness=new float[Width*Height];
        public readonly bool[] target=new bool[Grid*Grid],current=new bool[Grid*Grid];
        public TorchScore Score {get;private set;}
        public float Baseline {get;private set;}
        readonly bool[] near=new bool[Grid*Grid],halo=new bool[Grid*Grid];
        readonly Vector3[][] local;
        readonly int[][] triangles;
        readonly Vector3[] world;
        readonly Vector2[] projected;
        readonly bool[] valid;
        readonly Vector2Int[] disc;
        int targetArea;
        bool drawn;
        float drawnYaw;
        Vector3 drawnLight,drawnAim;

        public TorchProjection(TorchLevel level)
        {
            this.level=level;int count=level.pieces.Length,most=0;local=new Vector3[count][];triangles=new int[count][];
            for(int i=0;i<count;i++)
            {
                var piece=level.pieces[i];
                if(!piece.mesh||!piece.mesh.isReadable)throw new InvalidOperationException($"{level.name}: piece {i} needs a readable mesh");
                var matrix=Matrix4x4.TRS(piece.position,Quaternion.Euler(piece.euler),Vector3.one);var v=piece.mesh.vertices;
                for(int k=0;k<v.Length;k++)v[k]=matrix.MultiplyPoint3x4(v[k]);
                local[i]=v;triangles[i]=piece.mesh.triangles;most=Mathf.Max(most,v.Length);
            }
            world=new Vector3[most];projected=new Vector2[most];valid=new bool[most];
            var offsets=new List<Vector2Int>();int r=Mathf.Max(0,level.tolerance);
            for(int dy=-r;dy<=r;dy++)for(int dx=-r;dx<=r;dx++)if(dx*dx+dy*dy<=r*r+r)offsets.Add(new Vector2Int(dx,dy));
            disc=offsets.ToArray();
            Render(level.solutionYaw,level.solutionLight,Center-level.solutionLight);Normalise(visible,target);
            foreach(bool t in target)if(t)targetArea++;
            Dilate(target,halo);
            // Typical match from a wrong angle, so the warmth meter starts near zero on every level.
            var samples=new List<float>();
            for(int k=1;k<12;k++){Render(level.solutionYaw+k*30,level.solutionLight,Center-level.solutionLight);Evaluate();samples.Add(Score.iou);}
            samples.Sort();Baseline=Mathf.Min(samples[samples.Count/2],level.iouMin-.15f);Score=default;
        }
        public static Vector3 Turn(Vector3 p,float yaw)=>Quaternion.Euler(0,yaw,0)*(p-Axis)+Axis;
        public static Vector3 WallPoint(int x,int y)=>new Vector3(WallX0+(x+.5f)*Pixel,WallY0+(y+.5f)*Pixel,WallZ);

        // Light and aim are in workbench space; yaw turns the pile about Axis. Returns false when nothing moved.
        public bool Render(float yaw,Vector3 light,Vector3 aim)
        {
            aim=aim.sqrMagnitude>1e-8f?aim.normalized:Vector3.forward;
            if(drawn&&yaw==drawnYaw&&light==drawnLight&&aim==drawnAim)return false;
            bool lightMoved=!drawn||light!=drawnLight||aim!=drawnAim;drawn=true;drawnYaw=yaw;drawnLight=light;drawnAim=aim;
            Array.Clear(shadow,0,shadow.Length);
            if(lightMoved)
            {
                float edge=Mathf.Cos(HalfAngle*Mathf.Deg2Rad),inner=Mathf.Cos(HalfAngle*.72f*Mathf.Deg2Rad),dz=WallZ-light.z,zz=dz*dz,az=aim.z*dz;
                for(int y=0;y<Height;y++)
                {
                    float dy=WallY0+(y+.5f)*Pixel-light.y,yy=dy*dy+zz,ay=aim.y*dy+az;
                    for(int x=0;x<Width;x++)
                    {
                        float dx=WallX0+(x+.5f)*Pixel-light.x,c=(aim.x*dx+ay)/Mathf.Sqrt(dx*dx+yy);
                        brightness[y*Width+x]=c<=edge?0:Mathf.SmoothStep(0,1,(c-edge)/(inner-edge));
                    }
                }
            }
            var turn=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<local.Length;i++)
            {
                var v=local[i];var t=triangles[i];
                for(int k=0;k<v.Length;k++)
                {
                    var w=turn*(v[k]-Axis)+Axis;world[k]=w;float dz=w.z-light.z;valid[k]=dz>.001f;
                    if(!valid[k])continue;var q=light+(w-light)*((WallZ-light.z)/dz);
                    projected[k]=new Vector2((q.x-WallX0)/Pixel,(q.y-WallY0)/Pixel);
                }
                for(int j=0;j<t.Length;j+=3)
                {
                    int a=t[j],b=t[j+1],c=t[j+2];if(!valid[a]||!valid[b]||!valid[c])continue;
                    // Only triangles facing the light: same silhouette for closed meshes, half the work.
                    if(Vector3.Dot(Vector3.Cross(world[b]-world[a],world[c]-world[a]),light-world[a])<=0)continue;
                    Raster(projected[a],projected[b],projected[c]);
                }
            }
            for(int p=0;p<shadow.Length;p++)visible[p]=shadow[p]&&brightness[p]>0;
            return true;
        }
        public void Evaluate()
        {
            int count=Normalise(visible,current);Dilate(current,near);
            int hit=0,spill=0,both=0,either=0;
            for(int p=0;p<Grid*Grid;p++){bool t=target[p],c=current[p];if(t&&near[p])hit++;if(c&&!halo[p])spill++;if(t&&c)both++;if(t||c)either++;}
            var s=new TorchScore{visible=count>=12,cover=hit/(float)Mathf.Max(1,targetArea),spill=spill/(float)Mathf.Max(1,targetArea),iou=both/(float)Mathf.Max(1,either)};
            s.Ready=s.visible&&s.cover>=level.coverMin&&s.spill<=level.spillMax&&s.iou>=level.iouMin;
            s.warmth=s.Ready?1:Mathf.Clamp(Mathf.Min((s.iou-Baseline)/Mathf.Max(.05f,level.iouMin-Baseline),s.cover/level.coverMin),0,.97f);
            Score=s;
        }
        int Normalise(bool[] source,bool[] output)
        {
            Array.Clear(output,0,output.Length);
            int count=0;double sx=0,sy=0;
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)if(source[y*Width+x]){count++;sx+=x+.5;sy+=y+.5;}
            if(count<12)return count;
            float cx=(float)(sx/count),cy=(float)(sy/count);double ss=0;
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)if(source[y*Width+x]){float dx=x+.5f-cx,dy=y+.5f-cy;ss+=dx*dx+dy*dy;}
            float scale=Mathf.Sqrt((float)(ss/count))/RmsCells;
            for(int gy=0;gy<Grid;gy++)for(int gx=0;gx<Grid;gx++)
            {
                int hits=0;
                for(int s=0;s<4;s++)
                {
                    int x=Mathf.FloorToInt(cx+(gx+.5f+((s&1)==0?-.25f:.25f)-Grid*.5f)*scale),y=Mathf.FloorToInt(cy+(gy+.5f+((s&2)==0?-.25f:.25f)-Grid*.5f)*scale);
                    if(x>=0&&x<Width&&y>=0&&y<Height&&source[y*Width+x])hits++;
                }
                output[gy*Grid+gx]=hits>=2;
            }
            return count;
        }
        void Dilate(bool[] source,bool[] output)
        {
            Array.Clear(output,0,output.Length);
            for(int y=0;y<Grid;y++)for(int x=0;x<Grid;x++)if(source[y*Grid+x])
                foreach(var d in disc){int nx=x+d.x,ny=y+d.y;if(nx>=0&&nx<Grid&&ny>=0&&ny<Grid)output[ny*Grid+nx]=true;}
        }
        // Same inside test as the other levels' rasterisers, with the edge functions stepped incrementally.
        void Raster(Vector2 a,Vector2 b,Vector2 c)
        {
            float area=(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);if(area>-.001f&&area<.001f)return;
            if(area<0){var t=b;b=c;c=t;}
            int x0=Mathf.Clamp((int)Mathf.Floor(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,Width-1),x1=Mathf.Clamp((int)Mathf.Ceil(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,Width-1);
            int y0=Mathf.Clamp((int)Mathf.Floor(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,Height-1),y1=Mathf.Clamp((int)Mathf.Ceil(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,Height-1);
            float px=x0+.5f;
            for(int y=y0;y<=y1;y++)
            {
                float py=y+.5f;
                float e0=(b.x-a.x)*(py-a.y)-(b.y-a.y)*(px-a.x),e1=(c.x-b.x)*(py-b.y)-(c.y-b.y)*(px-b.x),e2=(a.x-c.x)*(py-c.y)-(a.y-c.y)*(px-c.x);
                float s0=-(b.y-a.y),s1=-(c.y-b.y),s2=-(a.y-c.y);int row=y*Width;
                for(int x=x0;x<=x1;x++){if(e0>=0&&e1>=0&&e2>=0)shadow[row+x]=true;e0+=s0;e1+=s1;e2+=s2;}
            }
        }
    }
}
