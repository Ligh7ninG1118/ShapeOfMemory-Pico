using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Summer drinks (2026-11-17), two pieces for the Summer level (see SummerBuilder), built like the other props
    // (procedural, real size, smooth normals doing the rounding, detail in textures generated here, mobile VR budget).
    // MARTINI GLASS: a cocktail glass 170 mm tall: a cone 115 mm across the rim on a thin stem with a small knob under it
    // and a round foot, all of the shared CozyShops glass; a red drink filling the cone to 80 percent of its depth, two ice
    // cubes floating in it.
    // ICED DRINK: a tumbler 160 mm tall flaring from 60 mm at its thick base to 80 mm at the rim, of the shared glass; cola
    // to 10 mm under the rim, two ice cubes floating at the top and nine more pressed to the glass (only their faces, just
    // outside the cola, are drawn), and a red-and-white bendy straw leaning out past the rim's right side and bending over
    // to the right (it casts in the shadow).
    // Ice: a transparent copy of the shared glass, whiter and less clear, its texture giving each face a cloudy core; drawn
    // after the drink and before the glass round it (submesh order).
    // Shadows: light closed hulls: the martini's foot, stem and cone 80 triangles; the tumbler with its straw 92.
    // Each in its own frame: metres, +y up, -z the front, standing on y = 0 round the y axis.
    public static class SummerDrinksBuilder
    {
        const string Meshes=SummerBuilder.Meshes,Textures=SummerBuilder.Textures,MaterialFolder=SummerBuilder.MaterialFolder;
        const string GlassPath="Assets/Scenes/Store/Sari3D/CozyShops_Lite/Materials/M_CozyShops_Glass.mat",IcePath=MaterialFolder+"summer drinks ice.mat";
        const string IceTexturePath=Textures+"summer drinks ice.png",StrawTexturePath=Textures+"summer drinks straw.png";
        public const string MartiniPath=Meshes+"Summer martini glass.asset",MartiniShadowPath=Meshes+"Summer martini glass shadow.asset";
        public const string IcedPath=Meshes+"Summer iced drink.asset",IcedShadowPath=Meshes+"Summer iced drink shadow.asset";

        static Texture2D Load(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        static Vector2 V(float r,float h)=>new Vector2(r,h);
        // Behind the pale teal glass the drinks read darker and cooler, so they are saturated, with a faint glow for the
        // light coming through them.
        static readonly Look MartiniRed=new Look("summer martini drink",new Color(.58f,.035f,.06f),0f,.90f);
        static readonly Color MartiniGlow=new Color(.24f,.01f,.02f,1f);
        static readonly Look Cola=new Look("summer drinks cola",new Color(.12f,.05f,.022f),0f,.92f);
        static readonly Color ColaGlow=new Color(.05f,.016f,.004f,1f);
        static readonly Look Straw=new Look("summer drinks straw",Color.white,0f,.45f){baseMap=()=>Load(StrawTexturePath)??StrawTexture()};

        // Martini submeshes: drink, ice, glass. Iced drink: cola, straw, ice, glass.
        public static Material[] MartiniMaterials()
        {
            var m=PropMesh.Materials(MaterialFolder,new[]{MartiniRed});
            return new[]{Glow(m[0],MartiniGlow),IceMaterial(),AssetDatabase.LoadAssetAtPath<Material>(GlassPath)};
        }
        public static Material[] IcedMaterials()
        {
            var m=PropMesh.Materials(MaterialFolder,new[]{Cola,Straw});
            return new[]{Glow(m[0],ColaGlow),m[1],IceMaterial(),AssetDatabase.LoadAssetAtPath<Material>(GlassPath)};
        }
        // Emission, set when the material is new (tweaks made in Unity survive). In this order: URP's material check keeps
        // _EMISSION only with an emissive GI flag.
        static Material Glow(Material m,Color emission)
        {
            if(m.IsKeywordEnabled("_EMISSION"))return m;
            m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.SetColor("_EmissionColor",emission);
            m.EnableKeyword("_EMISSION");EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
        }
        // The shared glass's settings (URP Lit, transparent, specular kept), whiter, and its opacity from the texture.
        // Created only when missing, like PropMesh.Materials.
        static Material IceMaterial()
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(IcePath);if(m)return m;
            m=new Material(AssetDatabase.LoadAssetAtPath<Material>(GlassPath));var tint=new Color(.60f,.68f,.72f,1f);
            m.SetColor("_BaseColor",tint);m.SetColor("_Color",tint);m.SetFloat("_Smoothness",.90f);
            var tex=Load(IceTexturePath)??IceTexture();m.SetTexture("_BaseMap",tex);m.SetTexture("_MainTex",tex);
            AssetDatabase.CreateAsset(m,IcePath);AssetDatabase.SaveAssets();return m;
        }

        [MenuItem("MetaHackthone/Shadow Workshop/Summer/Build drinks")]
        public static void Build()
        {
            IceTexture();StrawTexture();
            var martini=Martini();var martiniShadow=MartiniShadow();var iced=IcedDrink();var icedShadow=IcedShadow();
            SummerBuilder.Stage("MARTINI GLASS",martini,martiniShadow,MartiniMaterials());
            SummerBuilder.Stage("ICED DRINK",iced,icedShadow,IcedMaterials());
            Debug.Log($"SUMMER_DRINKS_OK {martini.name} {martini.triangles.Length/3} (shadow {martiniShadow.triangles.Length/3}), {iced.name} {iced.triangles.Length/3} (shadow {icedShadow.triangles.Length/3})");
        }

        // An ice cube (half size half, its edges bevelled and shaded round) turned by euler about its centre; UVs 0..1
        // across each face.
        static void Cube(PropMesh b,int m,Vector3 center,float half,Vector3 euler)
        {
            int from=b.v.Count;b.BevelBox(m,-Vector3.one*half,Vector3.one*half,half*.28f);var q=Quaternion.Euler(euler);
            for(int i=from;i<b.v.Count;i++){b.v[i]=center+q*b.v[i];b.n[i]=q*b.n[i];b.uv[i]=b.uv[i]/(2*half*UvPerMetre)+new Vector2(.5f,.5f);}
        }

        // --- Martini glass ------------------------------------------------------------------------------------------------
        // The cone: outside r = RimR - Slope * (RimY - y) (its tip at 107.5 mm), inside parallel to it 1.4 mm in, to the flat
        // top of the rim; the drink 0.4 mm inside that up to Fill.
        const float RimY=.1700f,RimR=.0575f,Slope=.92f,InnerY=.1704f,InnerR=.0560f,Fill=.1582f;
        static Mesh Martini()
        {
            var b=new PropMesh(3);const int Drink=0,Ice=1,Glass=2;
            // Foot, 67 mm across: flat underneath, a rounded edge, rising to the stem.
            b.Revolve(Glass,14,new[]{V(0,0),V(.0330f,0),V(.0330f,0),V(.0334f,.0030f),V(.0160f,.0068f),V(0,.0090f)});
            // Stem, 6 mm thick, flaring into the foot and into a knob under the cone; both ends buried.
            b.Revolve(Glass,8,new[]{V(.0062f,.0068f),V(.0036f,.0160f),V(.0031f,.0930f),V(.0045f,.1030f),V(.0066f,.1150f)});
            // Cone: outside up to the rim, its flat top, the inside down to the inner tip.
            b.Revolve(Glass,24,new[]{V(0,RimY-RimR/Slope),V(RimR,RimY),V(RimR,RimY),V(InnerR,InnerY),V(InnerR,InnerY),V(0,InnerY-InnerR/Slope)});
            const float Gap=.0004f;float fillR=InnerR-Slope*(InnerY-Fill)-Gap;
            b.Revolve(Drink,24,new[]{V(0,InnerY-(InnerR-Gap)/Slope),V(fillR,Fill),V(fillR,Fill),V(0,Fill)});
            Cube(b,Ice,new Vector3(-.013f,Fill+.0015f,-.009f),.0070f,new Vector3(18,30,12));
            Cube(b,Ice,new Vector3(.014f,Fill+.0008f,.010f),.0070f,new Vector3(-14,65,-20));
            return b.Save(MartiniPath,"Summer martini glass");
        }
        // Its shadow: an 8-sided foot, a cone up to the stem, a square stem, the knob, and a 12-sided cone to the rim
        // (within 1.3 mm of the glass; the ice stays under the rim). 80 triangles.
        static Mesh MartiniShadow()
        {
            var b=new PropMesh(1);
            Hull(b,new[]{Round(.0334f,0,8,0),Round(.0334f,.0030f,8,0),Round(.0045f,.0120f,4,Mathf.PI/4),Round(.0033f,.0960f,4,Mathf.PI/4),
                Round(.0060f,.1110f,6,0),Round(RimR,InnerY,12,0)});
            return b.Save(MartiniShadowPath,"Summer martini glass shadow");
        }

        // --- Iced drink ---------------------------------------------------------------------------------------------------
        // The cola's side (r, y): 2.2 mm inside the glass's outside; the glass below the cola is the solid base.
        static readonly Vector2[] ColaSide={V(.0284f,.0100f),V(.0330f,.0750f),V(.0372f,.1500f)};
        static float ColaR(float y)=>y<=ColaSide[1].y?Mathf.Lerp(ColaSide[0].x,ColaSide[1].x,Mathf.InverseLerp(ColaSide[0].y,ColaSide[1].y,y))
            :Mathf.Lerp(ColaSide[1].x,ColaSide[2].x,Mathf.InverseLerp(ColaSide[1].y,ColaSide[2].y,y));
        // Ice pressed to the glass: (degrees round from +x, height, half size, turn in degrees); six on the front half (-z,
        // 180 to 360 degrees), three behind.
        static readonly (float deg,float y,float half,float turn)[] Pressed={(300,.128f,.0068f,12),(228,.112f,.0072f,-18),(268,.090f,.0060f,30),
            (318,.070f,.0064f,-8),(240,.054f,.0066f,22),(286,.032f,.0068f,-30),(60,.118f,.0068f,15),(125,.080f,.0066f,-20),(95,.038f,.0064f,35)};

        static Mesh IcedDrink()
        {
            var b=new PropMesh(4);const int ColaM=0,StrawM=1,Ice=2,Glass=3;
            // Glass: the outside from the flat bottom round the heel up the flaring side, the flat top of the rim, and the
            // inside down to just under the cola's top (below it the cola fills the glass).
            b.Revolve(Glass,16,new[]{V(0,0),V(.0285f,0),V(.0300f,.0020f),V(.0352f,.0750f),V(.0400f,.1600f),V(.0400f,.1600f),V(.0384f,.1603f),V(.0384f,.1603f),V(.0366f,.1440f)});
            b.Revolve(ColaM,16,new[]{V(0,ColaSide[0].y),ColaSide[0],ColaSide[0],ColaSide[1],ColaSide[2],ColaSide[2],V(0,ColaSide[2].y)});
            Cube(b,Ice,new Vector3(-.014f,.1490f,-.010f),.0075f,new Vector3(15,25,10));
            Cube(b,Ice,new Vector3(.004f,.1490f,.016f),.0075f,new Vector3(-12,60,18));
            foreach(var (deg,y,half,turn) in Pressed)Pressed1(b,Ice,deg,y,half,turn);
            b.Bar(StrawM,StrawLine(4),Vector3.forward,t=>Ellipse(StrawR,StrawR,6),0,.0008f,1);
            return b.Save(IcedPath,"Summer iced drink");
        }
        // The face of an ice cube pressed to the glass: a square with its corners cut, laid round the cola's side 1 mm out
        // (clear of the cola's facets), turned by turn degrees, its normals tipped out toward its edges so it shades like a
        // cube's rounded face.
        static void Pressed1(PropMesh b,int m,float deg,float y0,float half,float turn)
        {
            float a0=deg*Mathf.Deg2Rad,r0=ColaR(y0),c=Mathf.Cos(turn*Mathf.Deg2Rad),s=Mathf.Sin(turn*Mathf.Deg2Rad),cut=half*.55f;
            int At(Vector2 local)
            {
                float along=local.x*c-local.y*s,up=local.y*c+local.x*s,a=a0+along/r0,y=y0+up,r=ColaR(y)+.0010f;
                var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var tangent=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a));
                var normal=(radial+(tangent*along+Vector3.up*up)*(.45f/half)).normalized;
                return b.Add(radial*r+Vector3.up*y,normal,local/(2*half)+new Vector2(.5f,.5f));
            }
            int hub=At(Vector2.zero);
            var outline=new[]{V(half,-cut),V(half,cut),V(cut,half),V(-cut,half),V(-half,cut),V(-half,-cut),V(-cut,-half),V(cut,-half)}.Select(At).ToArray();
            for(int k=0;k<outline.Length;k++)b.Tri(m,hub,outline[k],outline[(k+1)%outline.Length]);
        }

        // The straw's centre line in the z = 0 plane: from inside the cola, leaning 22 degrees right, past the rim 22 mm
        // right of the axis, round a 7 mm bend 32 mm over the rim, then a 34 mm arm pointing 35 degrees down to the right.
        const float StrawR=.003f,Lean=22f,BendR=.007f,ArmLength=.034f,ArmDown=35f;
        static List<Vector3> StrawLine(int arcSteps)
        {
            float lean=Lean*Mathf.Deg2Rad;var d=new Vector3(Mathf.Sin(lean),Mathf.Cos(lean),0);var atRim=new Vector3(.022f,.160f,0);
            var line=new List<Vector3>{atRim-d*(.035f/d.y)};var bend=atRim+d*(.032f/d.y);var center=bend+new Vector3(d.y,-d.x,0)*BendR;
            for(int k=0;k<=arcSteps;k++){float a=Mathf.Lerp(180-Lean,90-ArmDown,(float)k/arcSteps)*Mathf.Deg2Rad;line.Add(center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*BendR);}
            float down=-ArmDown*Mathf.Deg2Rad;line.Add(line[line.Count-1]+new Vector3(Mathf.Cos(down),Mathf.Sin(down),0)*ArmLength);
            return line;
        }
        // Where the bend runs along the drawn straw (t 0..1 along its centre line, as Bar lays u), and the straw's length.
        static void StrawBend(out float t0,out float t1,out float length)
        {
            var line=StrawLine(4);float along=0;t0=t1=0;
            for(int i=1;i<line.Count;i++){along+=Vector3.Distance(line[i-1],line[i]);if(i==1)t0=along;if(i==line.Count-2)t1=along;}
            length=along;t0/=along;t1/=along;
        }
        // Its shadow: a 10-sided hull for the tumbler and a square tube 6 mm across along the straw (the bend in two
        // chords). 92 triangles.
        static Mesh IcedShadow()
        {
            var b=new PropMesh(1);
            Hull(b,new[]{Round(.0300f,0,10,0),Round(.0352f,.0750f,10,0),Round(.0400f,.1603f,10,0)});
            Tube(b,StrawLine(2),StrawR);
            return b.Save(IcedShadowPath,"Summer iced drink shadow");
        }

        // --- Shadow hulls (as WesternGlassPropsBuilder's) -------------------------------------------------------------------
        // Rings of points round the y axis (each a convex loop round it), stitched band to band by angle round the axis and
        // closed flat at both ends: closed and wound outward.
        static void Hull(PropMesh b,IList<Vector3[]> rings)
        {
            float Angle(Vector3 p){float a=Mathf.Atan2(p.z,p.x);return a<0?a+2*Mathf.PI:a;}
            var sorted=rings.Select(r=>r.OrderBy(Angle).ToArray()).ToList();
            var ids=sorted.Select(r=>r.Select(p=>b.Add(p,new Vector3(p.x,0,p.z).normalized,Vector2.zero)).ToArray()).ToList();
            for(int j=0;j+1<sorted.Count;j++)
            {
                var A=sorted[j];var B=sorted[j+1];int n=A.Length,m=B.Length,i=0,k=0;
                float At(Vector3[] r,int t)=>Angle(r[t%r.Length])+2*Mathf.PI*(t/r.Length);
                while(i<n||k<m)
                {
                    bool stepA=k>=m||(i<n&&At(A,i+1)<=At(B,k+1));
                    if(stepA){b.Tri(0,ids[j][i%n],ids[j+1][k%m],ids[j][(i+1)%n]);i++;}
                    else{b.Tri(0,ids[j][i%n],ids[j+1][k%m],ids[j+1][(k+1)%m]);k++;}
                }
            }
            foreach(var (ring,dir) in new[]{(sorted[0],Vector3.down),(sorted[sorted.Count-1],Vector3.up)})
            {
                int start=b.v.Count;foreach(var p in ring)b.Add(p,dir,Vector2.zero);
                for(int k=1;k+1<ring.Length;k++)b.Tri(0,start,start+k,start+k+1);
            }
        }
        // A ring of n points round a circle of radius r at height y, pushed out so the polygon straddles the circle.
        static Vector3[] Round(float r,float y,int n,float phase)
        {
            float grow=2/(1+Mathf.Cos(Mathf.PI/n));
            return Enumerable.Range(0,n).Select(k=>{float a=phase+k*2*Mathf.PI/n;return new Vector3(Mathf.Cos(a)*r*grow,y,Mathf.Sin(a)*r*grow);}).ToArray();
        }
        // A square tube (half width half) along a polyline in the z = 0 plane, its faces square to z, mitred at the joints
        // and closed at both ends.
        static void Tube(PropMesh b,IList<Vector3> line,float half)
        {
            int count=line.Count;var rings=new int[count,4];
            for(int i=0;i<count;i++)
            {
                var tIn=(line[Mathf.Max(i,1)]-line[Mathf.Max(i,1)-1]).normalized;var tOut=(line[Mathf.Min(i+1,count-1)]-line[Mathf.Min(i+1,count-1)-1]).normalized;
                var t=(tIn+tOut).normalized;var up=Vector3.Cross(Vector3.forward,t);float miter=1/Mathf.Max(.5f,Vector3.Dot(t,tIn));
                for(int k=0;k<4;k++){var off=Vector3.forward*(k==1||k==2?half:-half)+up*(k>=2?half:-half)*miter;rings[i,k]=b.Add(line[i]+off,off.normalized,Vector2.zero);}
            }
            for(int i=0;i+1<count;i++)for(int k=0;k<4;k++)b.Quad(0,rings[i,k],rings[i+1,k],rings[i+1,(k+1)%4],rings[i,(k+1)%4]);
            foreach(var (i,sign) in new[]{(0,-1f),(count-1,1f)})
            {
                var t=(line[i==0?1:i]-line[i==0?0:i-1]).normalized*sign;var cap=new int[4];
                for(int k=0;k<4;k++)cap[k]=b.Add(b.v[rings[i,k]],t,Vector2.zero);
                b.Quad(0,cap[0],cap[1],cap[2],cap[3]);
            }
        }

        // --- Textures (detail colours kept at about 0.6 or below: the gym's light lifts lit faces about 1.5x) ------------------
        // An ice cube's face (u, v 0..1 across it): clear toward the edges, a cloudy core, a brighter rim where the edges
        // catch the light, and a crack. Alpha is the opacity (the ice material is transparent; its tint keeps it under 0.72).
        static Texture2D IceTexture()
        {
            const int S=64;var tex=new Texture2D(S,S,TextureFormat.RGBA32,false,false);
            float Segment(Vector2 p,Vector2 a,Vector2 c){var d=c-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return Vector2.Distance(p,a+d*t);}
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=(x+.5f)/S,v=(y+.5f)/S;var p=new Vector2(u,v);
                float edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v)),core=Mathf.Clamp01(1-Vector2.Distance(p,new Vector2(.5f,.5f))/.34f);
                float cloud=Mathf.Clamp01(core*(.55f+.9f*Fbm(u,v,4,4,3,131))),rim=Mathf.Clamp01(1-edge/.08f);
                if(Segment(p,new Vector2(.22f,.70f),new Vector2(.58f,.46f))<.012f||Segment(p,new Vector2(.58f,.46f),new Vector2(.70f,.24f))<.010f)cloud=Mathf.Max(cloud,.85f);
                var c=Color.Lerp(new Color(.84f,.92f,.98f),Color.white,cloud);c.a=Mathf.Lerp(.40f,.85f,Mathf.Max(cloud,rim*.75f));
                tex.SetPixel(x,y,c);
            }
            return SaveTexture(IceTexturePath,tex,false,TextureWrapMode.Clamp);
        }
        // Red and white stripes winding round the straw (u along it, v round it; a turn every 9 mm), the bend ridged with
        // seven grooves.
        static Texture2D StrawTexture()
        {
            StrawBend(out var t0,out var t1,out var length);const int W=512,H=32;var tex=new Texture2D(W,H,TextureFormat.RGB24,false,false);
            var red=new Color(.56f,.06f,.08f);var white=new Color(.60f,.59f,.57f);float turns=length/.009f;
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H,phase=u*turns+v;
                var c=phase-Mathf.Floor(phase)<.5f?red:white;
                if(u>t0&&u<t1)c*=Mathf.Lerp(.70f,1f,Mathf.Abs(Mathf.Cos((u-t0)/(t1-t0)*7*Mathf.PI)));
                c.a=1;tex.SetPixel(x,y,c);
            }
            return SaveTexture(StrawTexturePath,tex,false,TextureWrapMode.Clamp);
        }
    }
}
