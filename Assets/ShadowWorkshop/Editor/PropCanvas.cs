using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowWorkshopIntegration.Editor
{
    // 2D drawing for the hand-built props' printed textures (luggage tag card, salt jar, sugar box): anti-aliased fills
    // and strokes into a Color[] laid out as Texture2D.SetPixels (row 0 at the bottom), and a 5 x 7 dot font drawn either
    // as square dots (Text) or as rounded strokes joining the dots (RoundText).
    internal static class PropCanvas
    {
        static readonly Dictionary<char,string> Glyphs=new Dictionary<char,string>{
            {'A',"01110 10001 10001 11111 10001 10001 10001"},{'B',"11110 10001 10001 11110 10001 10001 11110"},
            {'C',"01110 10001 10000 10000 10000 10001 01110"},{'D',"11110 10001 10001 10001 10001 10001 11110"},
            {'E',"11111 10000 10000 11110 10000 10000 11111"},{'G',"01110 10001 10000 10111 10001 10001 01111"},
            {'H',"10001 10001 10001 11111 10001 10001 10001"},{'L',"10000 10000 10000 10000 10000 10000 11111"},
            {'M',"10001 11011 10101 10101 10001 10001 10001"},{'N',"10001 11001 10101 10011 10001 10001 10001"},
            {'O',"01110 10001 10001 10001 10001 10001 01110"},{'P',"11110 10001 10001 11110 10000 10000 10000"},
            {'R',"11110 10001 10001 11110 10100 10010 10001"},{'S',"01111 10000 10000 01110 00001 00001 11110"},
            {'T',"11111 00100 00100 00100 00100 00100 00100"},{'U',"10001 10001 10001 10001 10001 10001 01110"},
            {'V',"10001 10001 10001 10001 10001 01010 00100"},{'Y',"10001 10001 01010 00100 00100 00100 00100"}};
        static bool Dot(string[] rows,int r,int c)=>r>=0&&r<7&&c>=0&&c<5&&rows[r][c]=='1';

        // Square dots, cell px each, (x, y) the bottom left of the first letter.
        public static void Text(Color[] px,int w,string text,int x,int y,int cell,Color color)
        {
            foreach(var ch in text)
            {
                if(Glyphs.TryGetValue(ch,out var glyph))
                {
                    var rows=glyph.Split(' ');
                    for(int r=0;r<7;r++)for(int c=0;c<5;c++)if(rows[r][c]=='1')
                        for(int dy=0;dy<cell;dy++)for(int dx=0;dx<cell;dx++)px[(y+(6-r)*cell+dy)*w+x+c*cell+dx]=color;
                }
                x+=6*cell;
            }
        }
        // Rounded strokes through the dots: neighbours joined across and down, and diagonally where no square neighbour
        // turns the corner, so S, A and V come out smooth. Cells may be wider than tall (to undo a texture's stretch).
        public static void RoundText(Color[] px,int w,int h,string text,float x,float y,float cellW,float cellH,Color color,float weight=1)
        {
            var segments=new List<(Vector2,Vector2)>();
            foreach(var ch in text)
            {
                if(Glyphs.TryGetValue(ch,out var glyph))
                {
                    var rows=glyph.Split(' ');float left=x;
                    Vector2 At(int r,int c)=>new Vector2(left+(c+.5f)*cellW,y+(6-r+.5f)*cellH);
                    for(int r=0;r<7;r++)for(int c=0;c<5;c++)
                    {
                        if(!Dot(rows,r,c))continue;bool joined=false;
                        if(Dot(rows,r,c+1)){segments.Add((At(r,c),At(r,c+1)));joined=true;}
                        if(Dot(rows,r+1,c)){segments.Add((At(r,c),At(r+1,c)));joined=true;}
                        if(Dot(rows,r+1,c+1)&&!Dot(rows,r,c+1)&&!Dot(rows,r+1,c)){segments.Add((At(r,c),At(r+1,c+1)));joined=true;}
                        if(Dot(rows,r+1,c-1)&&!Dot(rows,r,c-1)&&!Dot(rows,r+1,c)){segments.Add((At(r,c),At(r+1,c-1)));joined=true;}
                        if(!joined&&!Dot(rows,r-1,c)&&!Dot(rows,r,c-1))segments.Add((At(r,c),At(r,c)));
                    }
                }
                x+=6*cellW;
            }
            Segments(px,w,h,segments,Mathf.Min(cellW,cellH)*weight,color);
        }
        public static float TextWidth(string text,float cell)=>(text.Length*6-1)*cell;

        // Anti-aliased fill (4 x 4 samples a pixel) of the pixels in [x0, x1) x [y0, y1) where inside holds.
        public static void Fill(Color[] px,int w,Func<Vector2,bool> inside,Color color,int x0,int y0,int x1,int y1)
        {
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
            {
                int hits=0;for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++)if(inside(new Vector2(x+(sx+.5f)/4,y+(sy+.5f)/4)))hits++;
                if(hits>0)px[y*w+x]=Color.Lerp(px[y*w+x],color,hits/16f);
            }
        }
        // Fill only where inside holds, over the bounding box of the given points.
        public static void Fill(Color[] px,int w,Func<Vector2,bool> inside,Color color,params Vector2[] bounds)
        {
            float x0=float.MaxValue,y0=float.MaxValue,x1=float.MinValue,y1=float.MinValue;
            foreach(var p in bounds){x0=Mathf.Min(x0,p.x);y0=Mathf.Min(y0,p.y);x1=Mathf.Max(x1,p.x);y1=Mathf.Max(y1,p.y);}
            int h=px.Length/w;
            Fill(px,w,inside,color,Mathf.Max(0,Mathf.FloorToInt(x0)-1),Mathf.Max(0,Mathf.FloorToInt(y0)-1),Mathf.Min(w,Mathf.CeilToInt(x1)+1),Mathf.Min(h,Mathf.CeilToInt(y1)+1));
        }
        public static void FillRect(Color[] px,int w,Vector2 min,Vector2 max,Color color)=>Fill(px,w,p=>p.x>=min.x&&p.x<max.x&&p.y>=min.y&&p.y<max.y,color,min,max);
        public static void FillPolygon(Color[] px,int w,Color color,params Vector2[] convex)=>Fill(px,w,p=>Convex(p,convex),color,convex);
        // Anti-aliased polyline with round joins.
        public static void Stroke(Color[] px,int w,int h,List<Vector2> points,float width,Color color)
        {
            var segments=new List<(Vector2,Vector2)>();for(int i=0;i+1<points.Count;i++)segments.Add((points[i],points[i+1]));
            Segments(px,w,h,segments,width,color);
        }
        // Anti-aliased round-ended segments, overlaps drawn once.
        public static void Segments(Color[] px,int w,int h,List<(Vector2 a,Vector2 b)> segments,float width,Color color)
        {
            var cover=new float[w*h];float r=width*.5f;
            foreach(var (a,c) in segments)
            {
                int x0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,c.x)-r-1)),x1=Mathf.Min(w-1,Mathf.CeilToInt(Mathf.Max(a.x,c.x)+r+1));
                int y0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,c.y)-r-1)),y1=Mathf.Min(h-1,Mathf.CeilToInt(Mathf.Max(a.y,c.y)+r+1));
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float k=Mathf.Clamp01(r+.5f-SegmentDistance(new Vector2(x+.5f,y+.5f),a,c));
                    if(k>cover[y*w+x])cover[y*w+x]=k;
                }
            }
            for(int i=0;i<cover.Length;i++)if(cover[i]>0)px[i]=Color.Lerp(px[i],color,cover[i]);
        }
        public static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {
            var ab=b-a;if(ab.sqrMagnitude<1e-12f)return Vector2.Distance(p,a);
            float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude);return Vector2.Distance(p,a+ab*t);
        }
        public static float RoundRectDistance(Vector2 p,Vector2 center,Vector2 half,float r)
        {
            var q=new Vector2(Mathf.Abs(p.x-center.x)-half.x+r,Mathf.Abs(p.y-center.y)-half.y+r);
            return Vector2.Max(q,Vector2.zero).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-r;
        }
        public static bool Convex(Vector2 p,params Vector2[] polygon)
        {
            bool positive=false,negative=false;
            for(int i=0;i<polygon.Length;i++)
            {
                Vector2 a=polygon[i],c=polygon[(i+1)%polygon.Length];float cross=(c.x-a.x)*(p.y-a.y)-(c.y-a.y)*(p.x-a.x);
                if(cross>0)positive=true;else if(cross<0)negative=true;
            }
            return !(positive&&negative);
        }
    }
}
