using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEngine;

namespace ShadowWorkshopIntegration.Editor
{
    // Turns Tools/ShadowArt/build_everyday.py output into Mesh, Material and SilhouetteLevel assets.
    // Existing assets are updated in place, so references and GUIDs survive a re-import.
    public static class EverydayImporter
    {
        public const string Source="Assets/ShadowWorkshop/Art/Everyday/EverydayObjects.json";
        const string Meshes="Assets/ShadowWorkshop/Art/Everyday/Meshes",Materials="Assets/ShadowWorkshop/Art/Everyday/Materials",Levels="Assets/ShadowWorkshop/Resources/ShadowArt/Levels";
        [Serializable] class Pose { public float[] position,euler; }
        [Serializable] class Piece { public string @object,label; public Pose solution,tray; }
        [Serializable] class Outline { public float[] points; }
        [Serializable] class Level { public string id,title,button,subtitle,intro,hint,complete; public float[] accent; public Outline[] outline; public Piece[] pieces; }
        [Serializable] class Submesh { public string material; public int[] triangles; }
        [Serializable] class Item { public string name; public float[] vertices; public Submesh[] submeshes; }
        [Serializable] class Swatch { public string name; public float[] color; public float smoothness; }
        [Serializable] class Tolerance { public int pixels; public float region,cover,spill,piece; }
        [Serializable] class Data { public int version; public Tolerance tolerance; public Swatch[] materials; public Item[] objects; public Level[] levels; }

        [MenuItem("MetaHackthone/Shadow Workshop/Import everyday objects")]
        public static void Import()
        {
            var data=JsonUtility.FromJson<Data>(File.ReadAllText(Source));
            if(data==null||data.version!=1)throw new InvalidOperationException("Unexpected "+Source+"; rerun Tools/ShadowArt/build_everyday.py");
            var meshes=ImportArt(data.materials,data.objects,Meshes,Materials,Levels);
            foreach(var source in data.levels)
            {
                string path=Levels+"/"+source.id+".asset";var level=AssetDatabase.LoadAssetAtPath<SilhouetteLevel>(path);
                if(!level){level=ScriptableObject.CreateInstance<SilhouetteLevel>();AssetDatabase.CreateAsset(level,path);}
                level.title=source.title;level.button=source.button;level.subtitle=source.subtitle;level.intro=source.intro;level.hint=source.hint;level.complete=source.complete;
                level.accent=new Color(source.accent[0],source.accent[1],source.accent[2]);
                level.tolerancePixels=data.tolerance.pixels;level.coverMin=data.tolerance.cover;level.regionMin=data.tolerance.region;level.spillMax=data.tolerance.spill;level.pieceSpillMax=data.tolerance.piece;
                level.outline=(source.outline??new Outline[0]).Select(o=>new SilhouetteOutline{points=Enumerable.Range(0,o.points.Length/2).Select(i=>new Vector2(o.points[i*2],o.points[i*2+1])).ToArray()}).ToArray();
                level.pieces=source.pieces.Select(p=>new SilhouettePiece{label=p.label,mesh=meshes[p.@object].mesh,materials=meshes[p.@object].materials,
                    solutionPosition=V(p.solution.position),solutionEuler=V(p.solution.euler),trayPosition=V(p.tray.position),trayEuler=V(p.tray.euler)}).ToArray();
                EditorUtility.SetDirty(level);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"EVERYDAY_IMPORT_OK objects={data.objects.Length} materials={data.materials.Length} levels={string.Join(",",data.levels.Select(l=>l.id))}");
        }
        // Shared by both generators: materials and meshes are updated in place so references survive.
        static Dictionary<string,(Mesh mesh,Material[] materials)> ImportArt(Swatch[] swatches,Item[] objects,string meshFolder,string materialFolder,string levelFolder)
        {
            foreach(var folder in new[]{meshFolder,materialFolder,levelFolder})Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var shader=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Graybox surfaces.mat").shader;
            var materials=new Dictionary<string,Material>();
            foreach(var swatch in swatches)
            {
                string path=materialFolder+"/"+swatch.name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
                var color=new Color(swatch.color[0],swatch.color[1],swatch.color[2]);
                material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",swatch.smoothness);EditorUtility.SetDirty(material);materials[swatch.name]=material;
            }
            var meshes=new Dictionary<string,(Mesh mesh,Material[] materials)>();
            foreach(var item in objects)
            {
                string path=meshFolder+"/"+item.name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
                mesh.name=item.name;var v=new Vector3[item.vertices.Length/3];for(int i=0;i<v.Length;i++)v[i]=new Vector3(item.vertices[i*3],item.vertices[i*3+1],item.vertices[i*3+2]);
                mesh.vertices=v;mesh.subMeshCount=item.submeshes.Length;for(int s=0;s<item.submeshes.Length;s++)mesh.SetTriangles(item.submeshes[s].triangles,s);
                mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                meshes[item.name]=(mesh,item.submeshes.Select(s=>materials[s.material]).ToArray());
            }
            return meshes;
        }

        public const string TorchSource="Assets/ShadowWorkshop/Art/Torch/TorchObjects.json";
        [Serializable] class TorchPieceData { public string @object,label; public float[] position,euler; }
        [Serializable] class TorchLevelData { public string id,title,button,riddle,hint,complete; public float[] accent,light; public float solution_yaw,start_yaw; public TorchPieceData[] pieces; }
        [Serializable] class Compare { public int tolerance; public float cover,spill,iou; public float[] tilts; }
        [Serializable] class TorchData { public int version; public Compare compare; public Swatch[] materials; public Item[] objects; public TorchLevelData[] levels; }

        // Tools/ShadowArt/build_torch.py output -> Art/Torch meshes and materials, Resources/ShadowArt/Torch levels.
        [MenuItem("MetaHackthone/Shadow Workshop/Import torch levels")]
        public static void ImportTorch()
        {
            var data=JsonUtility.FromJson<TorchData>(File.ReadAllText(TorchSource));
            if(data==null||data.version!=1)throw new InvalidOperationException("Unexpected "+TorchSource+"; rerun Tools/ShadowArt/build_torch.py");
            const string levels="Assets/ShadowWorkshop/Resources/ShadowArt/Torch";
            var meshes=ImportArt(data.materials,data.objects,"Assets/ShadowWorkshop/Art/Torch/Meshes","Assets/ShadowWorkshop/Art/Torch/Materials",levels);
            foreach(var source in data.levels)
            {
                string path=levels+"/"+source.id+".asset";var level=AssetDatabase.LoadAssetAtPath<TorchLevel>(path);
                if(!level){level=ScriptableObject.CreateInstance<TorchLevel>();AssetDatabase.CreateAsset(level,path);}
                level.title=source.title;level.button=source.button;level.riddle=source.riddle;level.hint=source.hint;level.complete=source.complete;
                level.accent=new Color(source.accent[0],source.accent[1],source.accent[2]);level.solutionLight=V(source.light);level.solutionYaw=source.solution_yaw;level.startYaw=source.start_yaw;
                level.tolerance=data.compare.tolerance;level.coverMin=data.compare.cover;level.spillMax=data.compare.spill;level.iouMin=data.compare.iou;
                level.pieces=source.pieces.Select(p=>new TorchPiece{label=p.label,mesh=meshes[p.@object].mesh,materials=meshes[p.@object].materials,position=V(p.position),euler=V(p.euler)}).ToArray();
                EditorUtility.SetDirty(level);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"TORCH_IMPORT_OK objects={data.objects.Length} levels={string.Join(",",data.levels.Select(l=>l.id))}");
        }
        public const string Torch2Source="Assets/ShadowWorkshop/Art/Torch2/Torch2Objects.json";
        [Serializable] class Torch2LevelData { public string id,title,button,riddle,hint,complete; public float[] accent,solution,start; public int[] lamps; public float iou_min; public TorchPieceData[] pieces; }
        [Serializable] class Room { public float[] home; public float pixel,halfAngle; public int width,height; }
        [Serializable] class Torch2Data { public int version; public Room room; public Compare compare; public Swatch[] materials; public Item[] objects; public Torch2LevelData[] levels; }

        // Tools/ShadowArt/build_torch2.py output -> Art/Torch2 meshes and materials, Resources/ShadowArt/Torch2 levels.
        [MenuItem("MetaHackthone/Shadow Workshop/Import torch levels 2")]
        public static void ImportTorch2()
        {
            var data=JsonUtility.FromJson<Torch2Data>(File.ReadAllText(Torch2Source));
            if(data==null||data.version!=1)throw new InvalidOperationException("Unexpected "+Torch2Source+"; rerun Tools/ShadowArt/build_torch2.py");
            // The room is hard-coded on both sides; refuse to import a design made for a different room.
            if(Vector3.Distance(V(data.room.home),Torch2Projection.Home)>1e-4f||Mathf.Abs(data.room.pixel-Torch2Projection.Pixel)>1e-6f||data.room.width!=Torch2Projection.Width||data.room.height!=Torch2Projection.Height||Mathf.Abs(data.room.halfAngle-Torch2Projection.HalfAngle)>1e-4f)
                throw new InvalidOperationException("build_torch2.py room constants differ from Torch2Projection");
            if(data.compare.tilts==null||!data.compare.tilts.SequenceEqual(Torch2Projection.Tilts))
                throw new InvalidOperationException("build_torch2.py TILTS differ from Torch2Projection.Tilts");
            const string levels="Assets/ShadowWorkshop/Resources/ShadowArt/Torch2";
            var meshes=ImportArt(data.materials,data.objects,"Assets/ShadowWorkshop/Art/Torch2/Meshes","Assets/ShadowWorkshop/Art/Torch2/Materials",levels);
            foreach(var source in data.levels)
            {
                string path=levels+"/"+source.id+".asset";var level=AssetDatabase.LoadAssetAtPath<Torch2Level>(path);
                if(!level){level=ScriptableObject.CreateInstance<Torch2Level>();AssetDatabase.CreateAsset(level,path);}
                level.title=source.title;level.button=source.button;level.riddle=source.riddle;level.hint=source.hint;level.complete=source.complete;
                level.accent=new Color(source.accent[0],source.accent[1],source.accent[2]);level.lamps=source.lamps;
                level.solution=Torch2Projection.Pose(source.solution[0],source.solution[1]);level.start=Torch2Projection.Pose(source.start[0],source.start[1]);level.bothFaces=false;
                // build_torch2.py checks its starts and thresholds against the solution alone, so no flips count here.
                level.flips=Torch2Flips.None;
                level.tolerance=data.compare.tolerance;level.coverMin=data.compare.cover;level.spillMax=data.compare.spill;level.iouMin=source.iou_min>0?source.iou_min:data.compare.iou;
                level.pieces=source.pieces.Select(p=>new Torch2Piece{label=p.label,mesh=meshes[p.@object].mesh,materials=meshes[p.@object].materials,position=V(p.position),euler=V(p.euler),scale=Vector3.one}).ToArray();
                EditorUtility.SetDirty(level);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"TORCH2_IMPORT_OK objects={data.objects.Length} levels={string.Join(",",data.levels.Select(l=>l.id))}");
        }
        static Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);
    }
}
