using System.Collections.Generic;
using System.Linq;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowWorkshopIntegration.Editor
{
    // Summer (2026-11-17, the user: "帮我设计一个新的关卡吧，然后主题是夏天 最后的完成图是海豚 ... 做好了放在那个场景里面 我自己摆放";
    // later that day the picture became a seal, as the user's arrangement read as one):
    // summer things whose shadow on the left wall the user arranges into a seal, in the gym. The props are built
    // by several builders (Summer*Builder.cs), each calling Stage to set its pieces out in ShadowGym under "Summer props":
    // beside the orb, outside the gym's cluster, so the pile already in the gym is left alone. To arrange them, run Build
    // Summer level, then Open in Shadow Gym on Summer.asset (the gym's id becomes Summer, so its export can't overwrite
    // another level), or drag them into the cluster by hand.
    // Props as before (WildWest): procedural, each at its real size, 60-600 triangles drawn, a light closed hull for the
    // shadow where the drawn mesh is heavy or open (the level's shadow budget is Torch2Gym.ShadowTriangleBudget).
    public static class SummerBuilder
    {
        public const string Art="Assets/ShadowWorkshop/Art/Torch2/",Meshes=Art+"Meshes/",Textures=Art+"Textures/",MaterialFolder=Art+"Materials/";
        public const string LevelPath="Assets/ShadowWorkshop/Resources/ShadowArt/Torch2/Summer.asset",GymScene="Assets/ShadowWorkshop/Scenes/ShadowGym.unity",Root="Summer props";
        public const string SlidePath="Assets/ShadowWorkshop/PicoFinal/Art/Slides/Slide_Summer.png";
        // Every piece's label, in its slot order: two rows of seven in front of the orb, left to right.
        public static readonly string[] Slots={"SWIM RING","FLAMINGO FLOAT","FLIP FLOP L","FLIP FLOP R","SWIM FIN L","SWIM FIN R","MARTINI GLASS",
            "ICED DRINK","BEACH BALL","POPSICLE","SUN HAT","SEASHELL","DIVING MASK","SAND SHOVEL"};
        // Set out at most this big (largest side, metres), about what the gym's piles use; built at full size.
        const float StagedSize=.20f;
        static Vector3 Spot(int slot)=>Torch2Projection.Home+new Vector3(-.78f+.26f*(slot%7),.10f-.28f*(slot/7),-.55f);

        // Puts one piece in ShadowGym under Summer props, replacing an earlier one of the same label: the drawn mesh, its
        // materials (submesh order) and, if it differs, the mesh that casts its shadow (a ShadowPiece proxy).
        public static GameObject Stage(string label,Mesh mesh,Mesh shadow,Material[] materials)
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=GymScene)throw new System.Exception($"Summer: open {GymScene} first (the active scene is {scene.path})");
            int slot=System.Array.IndexOf(Slots,label);if(slot<0)throw new System.Exception($"Summer: {label} has no slot in SummerBuilder.Slots");
            var root=GameObject.Find(Root);
            if(!root)
            {
                root=new GameObject(Root);Undo.RegisterCreatedObjectUndo(root,"Stage summer prop");
                var gym=Object.FindFirstObjectByType<Torch2Gym>();if(gym)root.transform.SetPositionAndRotation(gym.transform.position,gym.transform.rotation);
            }
            var old=root.transform.Find(label);if(old)Undo.DestroyObjectImmediate(old.gameObject);
            var go=new GameObject(label);Undo.RegisterCreatedObjectUndo(go,"Stage summer prop");go.transform.SetParent(root.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;
            var tag=go.AddComponent<ShadowPiece>();tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=shadow&&shadow!=mesh?shadow:null;
            var size=mesh.bounds.size;float largest=Mathf.Max(size.x,size.y,size.z),scale=largest>StagedSize?StagedSize/largest:1;
            go.transform.localScale=Vector3.one*scale;go.transform.localPosition=Spot(slot)-scale*mesh.bounds.center;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log($"SUMMER_STAGED {label} slot {slot}, {mesh.triangles.Length/3} triangles drawn, shadow {(tag.shadowProxy?tag.shadowProxy.triangles.Length/3:mesh.triangles.Length/3)}, staged at {scale:0.00} of {largest*1000:0} mm");
            return go;
        }

        // For props made after the level was opened in the gym (the sand rake, 2026-11-17): straight into the gym's pile, in
        // front of the hub at the orb's edge, at most StagedSize big, marked like Stage's pieces. Built again, an existing
        // piece only gets the new mesh, materials and shadow, and stays where the user put it. The scene is left unsaved
        // (the user is arranging it).
        public static GameObject AddToGym(string label,Mesh mesh,Mesh shadow,Material[] materials)
        {
            var gym=Object.FindFirstObjectByType<Torch2Gym>();
            if(!gym||gym.id!="Summer")throw new System.Exception($"Summer: open Summer in the gym first (the gym is at {(gym?gym.id:"none")})");
            var t=gym.cluster.Find(label);var go=t?t.gameObject:null;bool fresh=!go;
            if(fresh){go=new GameObject(label);Undo.RegisterCreatedObjectUndo(go,"Add summer prop");go.transform.SetParent(gym.cluster,false);}
            else Undo.RecordObjects(new Object[]{go.GetComponent<MeshFilter>(),go.GetComponent<MeshRenderer>()},"Rebuild summer prop");
            // Not ??: in the editor a missing component comes back as a fake null that ?? takes for a component.
            var filter=go.GetComponent<MeshFilter>();if(!filter)filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();if(!renderer)renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;
            var tag=go.GetComponent<ShadowPiece>();if(!tag)tag=go.AddComponent<ShadowPiece>();
            Undo.RecordObject(tag,"Rebuild summer prop");tag.role=ShadowPiece.Role.Picture;tag.shadowProxy=shadow&&shadow!=mesh?shadow:null;
            if(fresh)
            {
                var size=mesh.bounds.size;float largest=Mathf.Max(size.x,size.y,size.z),scale=largest>StagedSize?StagedSize/largest:1;
                go.transform.localScale=Vector3.one*scale;go.transform.localPosition=new Vector3(0,-.20f,-.20f)-scale*mesh.bounds.center;
            }
            EditorSceneManager.MarkSceneDirty(go.scene);gym.Refresh(true);
            Debug.Log($"SUMMER_IN_GYM {label} {(fresh?"added":"rebuilt in place")}, {mesh.triangles.Length/3} triangles drawn, shadow {(tag.shadowProxy?tag.shadowProxy.triangles.Length/3:mesh.triangles.Length/3)}");
            return go;
        }

        // In the level, before the user arranges it: slot order on a 3 x 3 grid, two layers deep, inside the orb (the
        // furthest corner 26 cm from the hub, the orb's reach 36).
        static Vector3 PileSpot(int slot)=>new Vector3(-.17f+.17f*(slot%3),.17f-.17f*(slot/3%3),slot<9?-.09f:.09f);

        // The level: the staged pieces (as staged: size, turn, materials, shadow proxy) set out round the hub, Wild West's
        // one-lamp settings, the summer text and slide. The user arranges it in the gym and exports.
        [MenuItem("MetaHackthone/Shadow Workshop/Build Summer level")]
        public static void BuildLevel()
        {
            var root=GameObject.Find(Root);if(!root)throw new System.Exception($"Summer: open {GymScene} with its {Root} first");
            var pieces=new List<Torch2Piece>();
            foreach(var label in Slots)
            {
                var t=root.transform.Find(label);var filter=t?t.GetComponent<MeshFilter>():null;if(!filter||!filter.sharedMesh){Debug.LogWarning($"Summer: {label} isn't staged yet");continue;}
                var tag=t.GetComponent<ShadowPiece>();var centre=t.localRotation*Vector3.Scale(t.localScale,filter.sharedMesh.bounds.center);
                pieces.Add(new Torch2Piece{label=label,mesh=filter.sharedMesh,shadowMesh=tag&&tag.shadowProxy?tag.shadowProxy:filter.sharedMesh,
                    materials=t.GetComponent<MeshRenderer>().sharedMaterials,position=PileSpot(pieces.Count)-centre,euler=t.localEulerAngles,scale=t.localScale});
            }
            var made=Object.Instantiate(Torch2Level.Load("WildWest"));made.name="Summer";
            // The chapter's name (button: the book and the found banner print it) is SUMMERTIME, one word: two, "Summer
            // Time", reads as daylight saving in British English. The level's id stays Summer (its table keys, its cue).
            made.title="S U M M E R T I M E";made.button="SUMMERTIME";
            // The words (2026-11-17, a seal); the Chinese is in the ShapeOfMemory Book table under Summer/<field>. The hint
            // leaves the pose loose (no facing, no setting) until the user has settled it in the gym.
            made.riddle="On land I'm a lump that goes flop in the sun,\nbut in water I'm swift, and the little fish run.\nI've whiskers and flippers; I clap and I bark,\nand I'll balance your ball on my nose till it's dark.";
            made.hint="A seal seen side-on: a round, whiskered head and a plump body tapering to its flippers.";
            made.complete="SEAL OF APPROVAL\nJust five more minutes in the sun!";
            // The left lamp's slide: a summer shore (ShapeOfMemory.Editor.MemorySummerSlide draws it).
            made.accent=new Color(.25f,.66f,.86f);made.lamps=new[]{0};made.leftSlide=AssetDatabase.LoadAssetAtPath<Texture2D>(SlidePath);made.rightSlide=null;made.layers=new AudioClip[0];
            made.solution=Quaternion.identity;made.start=Quaternion.Euler(20,140,0);made.iouMin=.62f;made.drawGather=1;made.pieces=pieces.ToArray();
            var level=AssetDatabase.LoadAssetAtPath<Torch2Level>(LevelPath);
            if(level){EditorUtility.CopySerialized(made,level);level.name="Summer";EditorUtility.SetDirty(level);AssetDatabase.SaveAssetIfDirty(level);Object.DestroyImmediate(made);}
            else{AssetDatabase.CreateAsset(made,LevelPath);AssetDatabase.SaveAssetIfDirty(made);}
            Debug.Log($"SUMMER_LEVEL_OK {LevelPath}: {pieces.Count} pieces ({string.Join(", ",pieces.Select(p=>p.label))})");
        }
    }
}
