using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static ShadowWorkshopIntegration.Editor.PropMesh;

namespace ShadowWorkshopIntegration.Editor
{
    // Low-poly dog biscuits for mobile VR: a bone 34 mm long and 7 mm thick, 68 triangles (a flat outline whose edge
    // normals lean out of the faces, so the rim shades round). "Fill selected jar with dog biscuits" drops a pile of them
    // into the selected jar: one mesh under the jar (one renderer; brown, red and cream biscuits as three submeshes),
    // marked visual only, so the pile costs no shadow triangles (the jar's own outline already makes the picture).
    public static class DogBiscuitBuilder
    {
        const string Folder="Assets/ShadowWorkshop/Art/Torch2/";
        public const string BiscuitPath=Folder+"Meshes/Dog biscuit.asset";
        const float Scale=.85f;      // the outline below is drawn 40 mm long; biscuits are 34 mm
        const float Half=.0036f;     // half the thickness
        const float BallR=.0042f;    // the spheres standing in for a biscuit when piling (a little over Half)
        static readonly Look[] Looks={
            new Look("dog biscuit brown",new Color(.70f,.45f,.24f),0,.12f),
            new Look("dog biscuit red",new Color(.62f,.24f,.18f),0,.12f),
            new Look("dog biscuit cream",new Color(.90f,.78f,.58f),0,.12f)};

        [MenuItem("MetaHackthone/Shadow Workshop/Build dog biscuit")]
        public static void Build()
        {
            var b=new PropMesh(1);AddBiscuit(b,0,Matrix4x4.identity);var mesh=b.Save(BiscuitPath,"Dog biscuit");
            PropMesh.Materials(Folder+"Materials/",Looks);
            Debug.Log($"DOG_BISCUIT_OK {mesh.triangles.Length/3} tris, {mesh.bounds.size*100} cm");
        }

        [MenuItem("MetaHackthone/Shadow Workshop/Fill selected jar with dog biscuits")]
        static void FillSelected()=>Fill(Pick());
        [MenuItem("MetaHackthone/Shadow Workshop/Fill selected jar with dog biscuits",true)]
        static bool CanFill()=>Pick()!=null;
        // The jar's body when both its body and lid are selected.
        static Transform Pick()
        {
            Transform first=null;
            foreach(var t in Selection.transforms){if(!t.GetComponent<MeshFilter>())continue;if(t.name.Contains("Body"))return t;if(!first)first=t;}
            return first;
        }

        // Piles count biscuits into jar (pivot anywhere, axis along its local y) up to fill of its height, replacing an
        // earlier pile. The inside is read off the jar's mesh: its nearest vertex to the axis between just above the base
        // and a little over the fill line is taken as the wall (a glass jar is often one surface, and its straight wall
        // may have vertices only at its ends); with none there, the mesh's narrower half width. Biscuits keep their
        // 34 mm whatever the jar's scale.
        public static GameObject Fill(Transform jar,int count=11,int seed=1,float fill=.6f)
        {
            var source=jar.GetComponent<MeshFilter>().sharedMesh;var bounds=source.bounds;float height=bounds.size.y;
            var axis=new Vector2(bounds.center.x,bounds.center.z);float wall=Mathf.Min(bounds.extents.x,bounds.extents.z);
            foreach(var v in source.vertices){float t=(v.y-bounds.min.y)/height;if(t>.04f&&t<fill+.15f)wall=Mathf.Min(wall,(new Vector2(v.x,v.z)-axis).magnitude);}
            float k=1/Mathf.Abs(jar.lossyScale.y),r=BallR*k,reach=wall*.92f-r,floor=bounds.min.y+.03f*height+r,top=bounds.min.y+fill*height;

            // Each biscuit (mostly flat, any heading) is dropped at many spots: from above until a sphere of it rests on
            // the floor or on a biscuit already in. Of the drops that stay inside the wall, the one resting lowest is
            // kept, so the bottom fills first and the pile spreads to the glass instead of stacking up the middle.
            var rng=new System.Random(seed);float R(float a,float c)=>a+(float)rng.NextDouble()*(c-a);
            var balls=Balls();var resting=new List<Vector3>();var poses=new List<Matrix4x4>();var colours=new List<int>();
            for(int n=0;n<count;n++)
            {
                Vector3[] bestAt=null;Matrix4x4 bestPose=default;float bestTop=float.MaxValue,bestLift=0;
                for(int tries=0;tries<120;tries++)
                {
                    var rot=Quaternion.Euler(R(-14,14),R(0,360),R(-10,10));float ang=R(0,2*Mathf.PI),dist=Mathf.Sqrt(R(0,1))*reach;
                    var centre=axis+new Vector2(Mathf.Cos(ang),Mathf.Sin(ang))*dist;var at=new Vector3[balls.Length];bool inside=true;
                    for(int i=0;i<balls.Length;i++)
                    {
                        var p=rot*(balls[i]*k);at[i]=new Vector3(centre.x+p.x,p.y,centre.y+p.z);
                        if((new Vector2(at[i].x,at[i].z)-axis).magnitude>reach)inside=false;
                    }
                    if(!inside)continue;
                    float lift=float.MinValue,highest=float.MinValue;
                    foreach(var a in at)
                    {
                        lift=Mathf.Max(lift,floor-a.y);
                        foreach(var o in resting){float d=new Vector2(a.x-o.x,a.z-o.z).magnitude;if(d<2*r)lift=Mathf.Max(lift,o.y+Mathf.Sqrt(4*r*r-d*d)-a.y);}
                    }
                    foreach(var a in at)highest=Mathf.Max(highest,a.y+lift+r);
                    if(highest<bestTop){bestTop=highest;bestAt=at;bestLift=lift;bestPose=Matrix4x4.TRS(new Vector3(centre.x,lift,centre.y),rot,Vector3.one*k);}
                }
                if(bestAt==null||bestTop>top)break;
                foreach(var a in bestAt)resting.Add(a+Vector3.up*bestLift);
                poses.Add(bestPose);float pick=R(0,1);colours.Add(pick<.5f?0:pick<.75f?1:2);
            }

            var b=new PropMesh(Looks.Length);for(int i=0;i<poses.Count;i++)AddBiscuit(b,colours[i],poses[i]);
            var pile=b.Save(Folder+"Meshes/Dog biscuits in "+jar.name+".asset","Dog biscuits in "+jar.name);
            var old=jar.Find("Dog biscuits");if(old)Undo.DestroyObjectImmediate(old.gameObject);
            var go=new GameObject("Dog biscuits");Undo.RegisterCreatedObjectUndo(go,"Fill jar with dog biscuits");
            go.transform.SetParent(jar,false);
            go.AddComponent<MeshFilter>().sharedMesh=pile;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=PropMesh.Materials(Folder+"Materials/",Looks);renderer.shadowCastingMode=ShadowCastingMode.Off;
            go.AddComponent<ShadowPiece>().role=ShadowPiece.Role.VisualOnly;
            Debug.Log($"DOG_BISCUITS_OK {poses.Count} biscuits in {jar.name}, {pile.triangles.Length/3} tris");
            return go;
        }

        // Outline in (x, z), counter-clockwise: the right end (two round lobes with a notch between) from the bar's lower
        // edge up, then the same end turned half round for the left.
        static Vector2[] Outline()
        {
            var end=new[]{V(.0097f,-.0042f),V(.0122f,-.0095f),V(.0174f,-.0095f),V(.0200f,-.0050f),V(.0162f,0),
                V(.0200f,.0050f),V(.0174f,.0095f),V(.0122f,.0095f),V(.0097f,.0042f)};
            var list=new List<Vector2>();foreach(var p in end)list.Add(p*Scale);foreach(var p in end)list.Add(-p*Scale);
            return list.ToArray();
        }
        // Spheres along the bar and in the four lobes, in the biscuit's frame.
        static Vector3[] Balls()
        {
            var list=new List<Vector3>();
            foreach(var x in new[]{-.012f,-.006f,0f,.006f,.012f})list.Add(new Vector3(x,0,0)*Scale);
            foreach(var sx in new[]{-1f,1f})foreach(var sz in new[]{-1f,1f})list.Add(new Vector3(sx*.0148f,0,sz*.0050f)*Scale);
            return list.ToArray();
        }

        // One biscuit into b's submesh m, placed by to (rotation, position, uniform scale): two flat faces and one band
        // round the edge whose normals lean out of the faces.
        static void AddBiscuit(PropMesh b,int m,Matrix4x4 to)
        {
            var outline=Outline();int n=outline.Length;var normal=new Vector2[n];
            for(int i=0;i<n;i++){var t=(outline[(i+1)%n]-outline[(i+n-1)%n]).normalized;normal[i]=new Vector2(t.y,-t.x);}
            Vector3 P(Vector2 p,float y)=>to.MultiplyPoint3x4(new Vector3(p.x,y,p.y));
            Vector3 N(Vector3 nn)=>to.MultiplyVector(nn).normalized;
            var caps=Triangulate(outline);
            foreach(var s in new[]{1f,-1f})
            {
                int start=b.v.Count;foreach(var p in outline)b.Add(P(p,s*Half),N(Vector3.up*s),p*UvPerMetre);
                for(int i=0;i<caps.Count;i+=3)b.Tri(m,start+caps[i],start+caps[i+1],start+caps[i+2]);
            }
            int edge=b.v.Count;
            for(int i=0;i<n;i++)
            {
                var o=new Vector3(normal[i].x,0,normal[i].y);
                b.Add(P(outline[i],Half),N(o+Vector3.up*.8f),outline[i]*UvPerMetre);b.Add(P(outline[i],-Half),N(o-Vector3.up*.8f),outline[i]*UvPerMetre);
            }
            for(int i=0;i<n;i++){int a=edge+i*2,c=edge+(i+1)%n*2;b.Quad(m,a,c,c+1,a+1);}
        }

        static Vector2 V(float x,float z)=>new Vector2(x,z);
    }
}
