using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    public struct SilhouetteScore
    {
        public float cover,spill,region,regionMean,pieceSpill;
        public bool placed,Ready;
        // Progress shown to the player: grows with each object that lands, reaches 1 when coverage passes.
        public float Match(SilhouetteLevel level)=>Mathf.Clamp01(Mathf.Min(cover/Mathf.Max(.01f,level.coverMin),regionMean));
    }

    // Single parallel front light along +z, same raster as the Three Wishes front view.
    // Eased scoring: a target pixel is covered when any shadow lies within the tolerance radius,
    // and shadow only counts as spill beyond that radius. Every piece's own footprint must be covered,
    // and no single piece may throw much stray shadow relative to its own footprint (catches a turned object).
    public sealed class SilhouetteProjection
    {
        public const int Size=128;
        public const float Span=.56f;
        public static readonly Vector3 Center=new Vector3(0,.29f,0);
        public readonly SilhouetteLevel level;
        public readonly int Count;
        public readonly bool[] target=new bool[Size*Size],halo=new bool[Size*Size],current=new bool[Size*Size],near=new bool[Size*Size],outline=new bool[Size*Size];
        public readonly bool[][] regions;
        public SilhouetteScore Score {get;private set;}
        readonly int[] regionArea;
        readonly Vector3[][] vertices;
        readonly int[][] triangles;
        readonly bool[][] pieceMasks;
        readonly ShadowPose[] cachedPoses;
        readonly bool[] cached,inside;
        readonly Vector2Int[] disc;
        readonly Vector2[] projected=new Vector2[3];
        int targetArea;

        public SilhouetteProjection(SilhouetteLevel level)
        {
            this.level=level;Count=level.pieces.Length;
            regions=new bool[Count][];pieceMasks=new bool[Count][];regionArea=new int[Count];vertices=new Vector3[Count][];triangles=new int[Count][];
            cachedPoses=new ShadowPose[Count];cached=new bool[Count];inside=new bool[Count];
            var offsets=new List<Vector2Int>();int r=Mathf.Max(0,level.tolerancePixels);
            for(int dy=-r;dy<=r;dy++)for(int dx=-r;dx<=r;dx++)if(dx*dx+dy*dy<=r*r+r)offsets.Add(new Vector2Int(dx,dy));
            disc=offsets.ToArray();
            for(int i=0;i<Count;i++)
            {
                var mesh=level.pieces[i].mesh;
                if(!mesh||!mesh.isReadable)throw new InvalidOperationException($"{level.name}: piece {i} needs a readable mesh");
                vertices[i]=mesh.vertices;triangles[i]=mesh.triangles;regions[i]=new bool[Size*Size];pieceMasks[i]=new bool[Size*Size];
                Raster(i,level.pieces[i].Solution,regions[i]);
                for(int p=0;p<Size*Size;p++)if(regions[i][p]){regionArea[i]++;target[p]=true;}
            }
            foreach(bool t in target)if(t)targetArea++;
            Dilate(target,halo);
            foreach(var line in level.outline)DrawOutline(line.points);
        }
        public static bool InWorkspace(ShadowPose p)=>Mathf.Abs(p.position.x)<=.24f&&Mathf.Abs(p.position.z)<=.24f&&p.position.y>=.055f&&p.position.y<=.56f;
        public static Vector2 ToPixel(Vector2 workbench)=>new Vector2((workbench.x-Center.x)/Span+.5f,(workbench.y-Center.y)/Span+.5f)*Size;
        public bool[] PieceMask(int part)=>pieceMasks[part];

        public void Render(ShadowPose[] poses)
        {
            bool changed=false;
            for(int i=0;i<Count;i++)
            {
                if(cached[i]&&cachedPoses[i].position.Equals(poses[i].position)&&cachedPoses[i].rotation.Equals(poses[i].rotation))continue;
                changed=true;cached[i]=true;cachedPoses[i]=poses[i];inside[i]=InWorkspace(poses[i]);
                Array.Clear(pieceMasks[i],0,Size*Size);if(inside[i])Raster(i,poses[i],pieceMasks[i]);
            }
            if(!changed)return;
            Array.Clear(current,0,current.Length);
            for(int i=0;i<Count;i++)if(inside[i]){var m=pieceMasks[i];for(int p=0;p<m.Length;p++)current[p]|=m[p];}
            Dilate(current,near);
            int covered=0,spill=0;float region=1,regionMean=0,pieceSpill=0;bool placed=true;
            for(int p=0;p<Size*Size;p++){if(target[p]&&near[p])covered++;if(current[p]&&!halo[p])spill++;}
            for(int i=0;i<Count;i++)
            {
                placed&=inside[i];int hit=0,stray=0;var m=regions[i];var own=pieceMasks[i];
                for(int p=0;p<m.Length;p++){if(m[p]&&near[p])hit++;if(own[p]&&!halo[p])stray++;}
                float part=hit/(float)Mathf.Max(1,regionArea[i]);region=Mathf.Min(region,part);regionMean+=Mathf.Min(1,part/Mathf.Max(.01f,level.regionMin))/Count;pieceSpill=Mathf.Max(pieceSpill,stray/(float)Mathf.Max(1,regionArea[i]));
            }
            var score=new SilhouetteScore{cover=covered/(float)Mathf.Max(1,targetArea),spill=spill/(float)Mathf.Max(1,targetArea),region=region,regionMean=regionMean,pieceSpill=pieceSpill,placed=placed};
            // All pieces must be used: a small object hidden in the tray cannot ride on the tolerance.
            score.Ready=placed&&score.cover>=level.coverMin&&score.region>=level.regionMin&&score.spill<=level.spillMax&&score.pieceSpill<=level.pieceSpillMax;
            Score=score;
        }
        void Dilate(bool[] source,bool[] output)
        {
            Array.Clear(output,0,output.Length);
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)if(source[y*Size+x])
                foreach(var d in disc){int nx=x+d.x,ny=y+d.y;if(nx>=0&&nx<Size&&ny>=0&&ny<Size)output[ny*Size+nx]=true;}
        }
        void DrawOutline(Vector2[] points)
        {
            if(points==null||points.Length<2)return;
            for(int i=0;i<points.Length;i++)
            {
                var a=ToPixel(points[i]);var b=ToPixel(points[(i+1)%points.Length]);int steps=Mathf.CeilToInt(Vector2.Distance(a,b)*2)+1;
                for(int s=0;s<=steps;s++){var p=Vector2.Lerp(a,b,s/(float)steps);int x=(int)p.x,y=(int)p.y;if(x>=0&&x<Size&&y>=0&&y<Size)outline[y*Size+x]=true;}
            }
        }
        void Raster(int part,ShadowPose pose,bool[] mask)
        {
            var v=vertices[part];var t=triangles[part];var matrix=Matrix4x4.TRS(pose.position-Center,pose.rotation,Vector3.one);
            for(int j=0;j<t.Length;j+=3)
            {
                for(int k=0;k<3;k++){var p=matrix.MultiplyPoint3x4(v[t[j+k]]);projected[k]=new Vector2(p.x/Span+.5f,p.y/Span+.5f)*Size;}
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
    }
}
