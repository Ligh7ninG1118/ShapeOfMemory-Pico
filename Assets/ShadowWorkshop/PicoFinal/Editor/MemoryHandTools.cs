using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShapeOfMemory.Editor
{
    // MetaHackthone > Pico Final > Hands: our hand mesh (from Tools/ShadowArt/build_hand.py's JSON), the hand looks (Ink,
    // Lantern) and their materials, which look the open scene's game uses, and posed hands in the Scene view to judge a
    // look by (there is no tracking in the editor).
    public static class MemoryHandTools
    {
        const string Menu="MetaHackthone/Pico Final/Hands/";
        public const string Folder=ShapeOfMemoryBuilder.Root+"/Art/Hands";
        public const string SourcePath=Folder+"/Source/MemoryHand.json";
        public const string RightPath=Folder+"/MemoryHand_R.asset",LeftPath=Folder+"/MemoryHand_L.asset";
        public const string LooksFolder=ShapeOfMemoryBuilder.Root+"/Looks/Hands";
        public const string MaterialsFolder=ShapeOfMemoryBuilder.Root+"/Materials/Hands";
        public const string InkPath=LooksFolder+"/Ink.asset",LanternPath=LooksFolder+"/Lantern.asset";

        // ---- The mesh
        [Serializable]
        public class Source
        {
            public string[] joints=Array.Empty<string>();
            public float[] rest=Array.Empty<float>(),vertices=Array.Empty<float>(),normals=Array.Empty<float>(),weights=Array.Empty<float>();
            public int[] triangles=Array.Empty<int>();
        }
        [MenuItem(Menu+"Import hand mesh")]
        public static void Import()
        {
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(SourcePath));
            if(!source.joints.SequenceEqual(MemoryHandSkin.Names))
                throw new InvalidDataException($"{SourcePath}: its joints aren't MemoryHandSkin's 26 in order ({string.Join(",",source.joints)})");
            Directory.CreateDirectory(Folder);
            var right=Save(Build(source,false),RightPath);var left=Save(Build(source,true),LeftPath);
            AssetDatabase.SaveAssets();
            // Rewritten in place with a new vertex count, the editor kept drawing from the old GPU buffers and the hands
            // vanished in edit-mode renders (2026-11-15): load both again from disk. (A player build reads the files.)
            foreach(var path in new[]{RightPath,LeftPath})AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            right=AssetDatabase.LoadAssetAtPath<Mesh>(RightPath);
            Debug.Log($"SHAPE_OF_MEMORY_HANDS imported {right.vertexCount} vertices, {right.triangles.Length/3} triangles a hand -> {RightPath}, {LeftPath}");
        }
        // Mesh space: metres, the wrist at the origin, fingers +y, palm toward -z. The right hand as the JSON has it; the
        // left its mirror across x.
        public static Mesh Build(Source s,bool left)
        {
            int n=s.vertices.Length/3;float sx=left?-1:1;
            var v=new Vector3[n];var nm=new Vector3[n];
            for(int i=0;i<n;i++)
            {
                v[i]=new Vector3(sx*s.vertices[3*i],s.vertices[3*i+1],s.vertices[3*i+2]);
                nm[i]=new Vector3(sx*s.normals[3*i],s.normals[3*i+1],s.normals[3*i+2]);
            }
            var rest=new Vector3[MemoryHandSkin.Count];
            for(int i=0;i<rest.Length;i++)rest[i]=new Vector3(sx*s.rest[3*i],s.rest[3*i+1],s.rest[3*i+2]);
            // The JSON's triangles have outward cross products (b-a)x(c-a), which is what Unity takes as a front face too
            // (clockwise seen from outside in its left-handed space), so the right hand keeps them. The left hand's mirror
            // turns every cross product inward: flip its winding.
            var tris=(int[])s.triangles.Clone();
            if(left)for(int t=0;t<tris.Length;t+=3)(tris[t+1],tris[t+2])=(tris[t+2],tris[t+1]);
            var weights=new BoneWeight[n];
            for(int i=0;i<n;i++)
            {
                int o=8*i;
                weights[i]=new BoneWeight{
                    boneIndex0=(int)s.weights[o],weight0=s.weights[o+1],boneIndex1=(int)s.weights[o+2],weight1=s.weights[o+3],
                    boneIndex2=(int)s.weights[o+4],weight2=s.weights[o+5],boneIndex3=(int)s.weights[o+6],weight3=s.weights[o+7]};
            }
            var mesh=new Mesh{name=left?"MemoryHand_L":"MemoryHand_R"};
            mesh.SetVertices(v);mesh.SetNormals(nm);mesh.SetTriangles(tris,0);
            // UV2: the rest position, so patterns stay on the skin as it bends. UV3: x the index and thumb tips (their aqua),
            // y the wrist's fade (0 at the stub's far end).
            mesh.SetUVs(2,v);mesh.SetUVs(3,Masks(v,rest));
            mesh.boneWeights=weights;mesh.bindposes=MemoryHandSkin.Bindposes(rest);
            mesh.RecalculateBounds();
            return mesh;
        }
        public const float TipNear=.006f,TipFar=.017f,FadeFrom=-.042f,FadeTo=-.014f;
        static List<Vector2> Masks(Vector3[] v,Vector3[] rest)
        {
            // The pads: each tip joint drawn back along its distal bone, to the middle of the fingertip's round.
            Vector3 Pad(int tip)=>rest[tip]-(rest[tip]-rest[tip-1]).normalized*TipNear;
            var index=Pad(MemoryHandSkin.IndexTip);var thumb=Pad(MemoryHandSkin.ThumbTip);
            var forward=(rest[MemoryHandSkin.MiddleProximal]-rest[MemoryHandSkin.Wrist]).normalized;
            var masks=new List<Vector2>(v.Length);
            foreach(var p in v)
            {
                float d=Mathf.Min(Vector3.Distance(p,index),Vector3.Distance(p,thumb));
                float tip=1-Smooth(TipNear,TipFar,d);
                float fade=Smooth(FadeFrom,FadeTo,Vector3.Dot(p-rest[MemoryHandSkin.Wrist],forward));
                masks.Add(new Vector2(tip,fade));
            }
            return masks;
        }
        static float Smooth(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
        static Mesh Save(Mesh mesh,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!existing){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            // CopySerialized changes the asset but not what the GPU already holds (2026-11-14: a winding fix only showed
            // after a reimport): send the new data up.
            EditorUtility.CopySerialized(mesh,existing);existing.name=Path.GetFileNameWithoutExtension(path);
            existing.UploadMeshData(false);EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);return existing;
        }

        // ---- The looks
        [MenuItem(Menu+"Make looks (overwrites the hand materials and looks)")]
        public static void MakeLooks()
        {
            Directory.CreateDirectory(LooksFolder);Directory.CreateDirectory(MaterialsFolder);
            var depth=Mat("Hand depth","ShapeOfMemory/HandDepth",m=>m.renderQueue=2990);
            var light=Mat("Hand ink line (light)","ShapeOfMemory/HandInkLine",m=>
            {
                m.SetColor("_Color",MemoryPalette.Moonlight);m.SetFloat("_Width",.0016f);m.SetFloat("_Boil",.0006f);m.SetFloat("_Seed",0);m.renderQueue=2991;
            });
            var dark=Mat("Hand ink line (dark)","ShapeOfMemory/HandInkLine",m=>
            {
                m.SetColor("_Color",MemoryPalette.Sepia);m.SetFloat("_Width",.0010f);m.SetFloat("_Boil",.0008f);m.SetFloat("_Seed",5.3f);m.renderQueue=2992;
            });
            // Hatched round the shaded side, as a sketch is (judged 2026-11-14 from the eyes in the game's dusk light:
            // higher thresholds hatched the whole hand like an engraving).
            var wash=Mat("Hand ink wash","ShapeOfMemory/HandInkWash",m=>
            {
                m.SetColor("_Wash",MemoryPalette.Paper);m.SetColor("_Ink",MemoryPalette.Sepia);Tips(m);
                m.SetFloat("_WashAlpha",.42f);m.SetFloat("_HatchBelow",.58f);m.SetFloat("_CrossBelow",.34f);m.SetFloat("_EdgeEnd",.32f);
                m.renderQueue=3000;
            });
            var lantern=Mat("Hand lantern","ShapeOfMemory/HandLantern",m=>
            {
                m.SetColor("_Edge",MemoryPalette.Ember*.55f);m.SetColor("_Core",MemoryPalette.GoldBright);Tips(m);m.renderQueue=3000;
            });
            var mote=Mat("Hand mote","ShapeOfMemory/HandMote",m=>m.renderQueue=3001);
            var right=AssetDatabase.LoadAssetAtPath<Mesh>(RightPath);var left=AssetDatabase.LoadAssetAtPath<Mesh>(LeftPath);
            if(!right||!left)throw new InvalidOperationException("Import the hand mesh first (Hands > Import hand mesh).");
            Look(InkPath,l=>{l.Right=right;l.Left=left;l.Materials=new[]{depth,light,dark,wash};l.Motes=false;l.MoteMaterial=null;});
            Look(LanternPath,l=>{l.Right=right;l.Left=left;l.Materials=new[]{depth,lantern};l.Motes=true;l.MoteMaterial=mote;});
            AssetDatabase.SaveAssets();
            Debug.Log("SHAPE_OF_MEMORY_HANDS looks made in "+LooksFolder);
        }
        static void Tips(Material m){m.SetColor("_TipIdle",MemoryPalette.AquaIdle);m.SetColor("_TipGrab",MemoryPalette.AquaGrab);}
        static Material Mat(string name,string shaderName,Action<Material> set)
        {
            var shader=Shader.Find(shaderName);if(!shader)throw new InvalidOperationException("No shader "+shaderName);
            var path=MaterialsFolder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,path);}
            else m.shader=shader;
            set(m);EditorUtility.SetDirty(m);return m;
        }
        static void Look(string path,Action<MemoryHandLook> set)
        {
            var l=AssetDatabase.LoadAssetAtPath<MemoryHandLook>(path);
            if(!l){l=ScriptableObject.CreateInstance<MemoryHandLook>();AssetDatabase.CreateAsset(l,path);}
            set(l);EditorUtility.SetDirty(l);
        }

        // ---- Which hand mesh the looks use: the Unity XR Hands sample hands as they come (Art/Hands/UnityXRHands), or ours
        public const string UnityHands=Folder+"/UnityXRHands";
        [MenuItem(Menu+"Mesh: Unity XR Hands sample")]
        public static void UseUnityHands()=>SetModels(AssetDatabase.LoadAssetAtPath<GameObject>(UnityHands+"/RightHand.fbx"),AssetDatabase.LoadAssetAtPath<GameObject>(UnityHands+"/LeftHand.fbx"));
        [MenuItem(Menu+"Mesh: ours (build_hand.py)")]
        public static void UseOurHands()=>SetModels(null,null);
        static void SetModels(GameObject right,GameObject left)
        {
            // The model's mesh read on the CPU at run time (the rig reads its bind poses): an import setting, not a change
            // to the model.
            foreach(var model in new[]{right,left})
            {
                if(!model)continue;
                var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(model)) as ModelImporter;
                if(importer&&!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            }
            var fade=WristFadeFor(right);
            foreach(var path in new[]{InkPath,LanternPath})
            {
                var look=AssetDatabase.LoadAssetAtPath<MemoryHandLook>(path);if(!look)continue;
                look.RightModel=right;look.LeftModel=left;look.WristFade=fade;EditorUtility.SetDirty(look);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SHAPE_OF_MEMORY_HANDS looks drawn with "+(right?"the Unity XR Hands sample hands":"our hand mesh"));
            if(preview)ShowPreview();
        }

        // The forearm stub fades in from just past its cut end (measured along the hand from the wrist joint) over 1.7 cm;
        // our own mesh keeps its -4.2..-1.4 cm.
        public static Vector2 WristFadeFor(GameObject model)
        {
            var skin=model?model.GetComponentInChildren<SkinnedMeshRenderer>(true):null;
            if(!skin)return new Vector2(-.042f,-.014f);
            var names=System.Array.ConvertAll(skin.bones,b=>b?b.name:"");
            var rig=new MemoryHandSkin.Rig(skin.sharedMesh,names);
            bool left=System.Array.Exists(names,n=>n.StartsWith("L_"));
            var toLocal=MemoryHandSkin.HandToLocal(rig.Rest,left);float end=0;
            foreach(var v in skin.sharedMesh.vertices)end=Mathf.Min(end,toLocal.MultiplyPoint3x4(v*rig.Unit).y);
            return new Vector2(end+.001f,Mathf.Min(end+.018f,-.002f));
        }

        // ---- Which look the game uses
        [MenuItem(Menu+"Use Ink")]
        public static void UseInk()=>Use(AssetDatabase.LoadAssetAtPath<MemoryHandLook>(InkPath));
        [MenuItem(Menu+"Use Lantern")]
        public static void UseLantern()=>Use(AssetDatabase.LoadAssetAtPath<MemoryHandLook>(LanternPath));
        [MenuItem(Menu+"Use the joint bones (no hand look)")]
        public static void UseBones()=>Use(null);
        public static void Use(MemoryHandLook look)
        {
            var game=UnityEngine.Object.FindFirstObjectByType<MemoryGame>();
            if(!game){Debug.LogWarning("SHAPE_OF_MEMORY_HANDS no MemoryGame in the open scene");return;}
            Undo.RecordObject(game,"Hand look");game.HandLook=look;
            EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            Debug.Log($"SHAPE_OF_MEMORY_HANDS {(look?look.name:"joint bones")} in {game.gameObject.scene.name}: save the scene to keep it");
            if(preview)ShowPreview(look);
        }

        // ---- Posed hands in the Scene view (never saved): the right hand pinching toward the book, the left open.
        const string PreviewMenu=Menu+"Show posed hands in the Scene view";
        static GameObject preview;
        static MemoryHandSkin.SkinnedHand previewRight,previewLeft;
        [MenuItem(PreviewMenu)]
        static void TogglePreview(){if(preview)HidePreview();else ShowPreview();}
        [MenuItem(PreviewMenu,true)]
        static bool PreviewState(){UnityEditor.Menu.SetChecked(PreviewMenu,preview);return true;}
        public static GameObject ShowPreview(MemoryHandLook look=null,float rightPinch=1,float leftPinch=0,float rightPoint=0)
        {
            HidePreview();
            var game=UnityEngine.Object.FindFirstObjectByType<MemoryGame>();
            if(!look)look=game&&game.HandLook?game.HandLook:AssetDatabase.LoadAssetAtPath<MemoryHandLook>(InkPath);
            if(!look||!look.Valid){Debug.LogWarning("SHAPE_OF_MEMORY_HANDS no valid hand look to preview");return null;}
            preview=new GameObject("Hand preview ("+look.name+")"){hideFlags=HideFlags.DontSave};
            previewRight=new MemoryHandSkin.SkinnedHand(preview.transform,"Right hand",look.MeshFor(false),look,look.BonesFor(false),false);
            previewLeft=new MemoryHandSkin.SkinnedHand(preview.transform,"Left hand",look.MeshFor(true),look,look.BonesFor(true),true);
            PosePreview(rightPinch,leftPinch,rightPoint);
            SceneView.RepaintAll();
            return preview;
        }
        // Where the hands sit: in the play area's space from the eyes (MemorySeatedPlayer.Eyes), reaching for the book.
        public static void PosePreview(float rightPinch,float leftPinch,float rightPoint=0)
        {
            if(!preview)return;
            var game=UnityEngine.Object.FindFirstObjectByType<MemoryGame>();
            var area=game&&game.PlayArea?game.PlayArea:null;
            var eyes=area?area.TransformPoint(MemorySeatedPlayer.Eyes):new Vector3(0,1.2f,0);
            var turn=area?area.rotation:Quaternion.identity;
            void Place(MemoryHandSkin.SkinnedHand hand,bool left,float pinch,float point)
            {
                float side=left?-1:1;
                var wrist=eyes+turn*new Vector3(side*.10f,-.31f,.25f);
                var rot=turn*Quaternion.Euler(16,-side*14,0)*MemoryHandPose.PalmDown*Quaternion.AngleAxis(-side*38,Vector3.up);
                hand.Pose(MemoryHandPose.Place(MemoryHandPose.Make(hand.Rig.Canonical,pinch,point),wrist,rot));
                hand.Show(true);
            }
            Place(previewRight,false,rightPinch,rightPoint);Place(previewLeft,true,leftPinch,0);
            SceneView.RepaintAll();
        }
        public static void HidePreview()
        {
            if(preview)UnityEngine.Object.DestroyImmediate(preview);
            // A domain reload forgets the reference but not the (never saved) object: clear any left behind.
            foreach(var stray in Resources.FindObjectsOfTypeAll<GameObject>())
                if(stray&&stray.transform.parent==null&&stray.hideFlags==HideFlags.DontSave&&stray.name.StartsWith("Hand preview ("))UnityEngine.Object.DestroyImmediate(stray);
            preview=null;previewRight=previewLeft=null;
        }
        public static IReadOnlyList<Vector3> PreviewJoints(bool left)=>(left?previewLeft:previewRight)?.Joints;
    }
}
