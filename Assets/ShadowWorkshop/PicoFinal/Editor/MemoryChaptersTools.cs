using System.Linq;
using CloudGarden.ShadowPuzzle;
using PhysicalAssembly.Editor;
using ShadowWorkshopIntegration.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShapeOfMemory.Editor
{
    // The chapter list (MemoryChapters): its menu items, its inspector and the build's check.
    // MetaHackthone > Pico Final > Chapters selects it; Check chapters logs SHAPE_OF_MEMORY_CHAPTERS (ok, or each problem).
    public static class MemoryChaptersTools
    {
        public const string AssetPath=ShapeOfMemoryBuilder.Root+"/Resources/"+MemoryChapters.ResourcePath+".asset";
        // What a missing list is made with: the first book.
        static readonly string[] FirstBook={"Birthday","BonVoyage","GoodMorning","Home"};

        [MenuItem("MetaHackthone/Pico Final/Chapters")]
        public static void Select(){var book=FindOrCreate();Selection.activeObject=book;EditorGUIUtility.PingObject(book);}
        // True when nothing stops a build (warnings are logged too).
        [MenuItem("MetaHackthone/Pico Final/Check chapters")]
        public static bool Check()
        {
            var book=AssetDatabase.LoadAssetAtPath<MemoryChapters>(AssetPath);
            if(!book){Debug.LogError("SHAPE_OF_MEMORY_CHAPTERS missing: "+AssetPath+" (Pico Final > Chapters makes it)");return false;}
            var problems=book.Check();int errors=problems.Count(p=>p.Error);
            string order=string.Join(", ",book.chapters.Select((c,i)=>MemoryChapters.Numeral(i)+" "+(c?c.name:"(empty)")));
            if(problems.Count==0){Debug.Log("SHAPE_OF_MEMORY_CHAPTERS ok: "+order);return true;}
            string text=$"SHAPE_OF_MEMORY_CHAPTERS {errors} error(s), {problems.Count-errors} warning(s) in {order}:\n"+string.Join("\n",problems);
            if(errors>0)Debug.LogError(text);else Debug.LogWarning(text);
            return errors==0;
        }
        public static MemoryChapters FindOrCreate()
        {
            var book=AssetDatabase.LoadAssetAtPath<MemoryChapters>(AssetPath);if(book)return book;
            book=ScriptableObject.CreateInstance<MemoryChapters>();book.chapters=FirstBook.Select(Torch2Level.Load).ToArray();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));
            AssetDatabase.CreateAsset(book,AssetPath);AssetDatabase.SaveAssets();
            Debug.Log("SHAPE_OF_MEMORY_CHAPTERS made "+AssetPath);return book;
        }
        public static void OpenInGym(Torch2Level level)
        {
            if(!level)return;Torch2GymTools.Open();
            var gym=Object.FindFirstObjectByType<Torch2Gym>();if(gym)Torch2GymTools.Load(gym,level);
        }
        // Build and Run's headset, the game opened at this level's chapter (builds first when the level changed).
        public static void RunOnHeadset(Torch2Level level)
        {
            if(!level)return;
            var config=DeployConfig.Find(ShapeOfMemoryBuilder.ConfigName);
            if(!config){Debug.LogError($"The {ShapeOfMemoryBuilder.ConfigName} build config is missing ({ShapeOfMemoryBuilder.ConfigPath}).");return;}
            BuildAndRunWindow.Open();BuildAndRun.RunLevel(config,level.name);
        }
    }

    // A build of the game stops on a chapter error (an empty or repeated slot, not four, a level without pieces);
    // warnings (placeholder or missing words) are logged and the build goes on.
    sealed class MemoryChaptersBuildCheck:IProcessSceneWithReport
    {
        public int callbackOrder=>0;
        public void OnProcessScene(Scene scene,BuildReport report)
        {
            if(report==null||scene.path!=ShapeOfMemoryBuilder.Scene)return;
            var book=MemoryChapters.Load();
            if(!book)throw new BuildFailedException("Shape of Memory has no chapter list: "+MemoryChaptersTools.AssetPath+" (MetaHackthone > Pico Final > Chapters makes it).");
            var problems=book.Check();
            foreach(var warning in problems.Where(p=>!p.Error))Debug.LogWarning("SHAPE_OF_MEMORY_CHAPTERS "+warning);
            var errors=problems.Where(p=>p.Error).ToArray();
            if(errors.Length>0)throw new BuildFailedException("Shape of Memory's chapters (MetaHackthone > Pico Final > Chapters):\n"+string.Join("\n",errors));
        }
    }

    // One row per chapter: its colour, number and level, the level's name and walls, Gym and Headset; then the problems.
    [CustomEditor(typeof(MemoryChapters))]
    sealed class MemoryChaptersInspector:UnityEditor.Editor
    {
        ReorderableList list;
        static float Line=>EditorGUIUtility.singleLineHeight;

        void OnEnable()
        {
            var chapters=serializedObject.FindProperty(nameof(MemoryChapters.chapters));
            list=new ReorderableList(serializedObject,chapters,true,true,true,true)
            {
                elementHeight=Line*2+8,
                drawHeaderCallback=r=>EditorGUI.LabelField(r,"Chapters in play order (drag to reorder)"),
                drawElementCallback=(r,i,active,focused)=>Row(r,i,chapters.GetArrayElementAtIndex(i)),
            };
        }
        static void Row(Rect r,int index,SerializedProperty slot)
        {
            r.y+=3;
            EditorGUI.DrawRect(new Rect(r.x,r.y,5,Line*2+2),MemoryPalette.Light(index));
            var top=new Rect(r.x+10,r.y,r.width-10,Line);
            EditorGUI.LabelField(new Rect(top.x,top.y,32,Line),MemoryChapters.Numeral(index),EditorStyles.boldLabel);
            EditorGUI.PropertyField(new Rect(top.x+34,top.y,top.width-34,Line),slot,GUIContent.none);
            var level=slot.objectReferenceValue as Torch2Level;if(!level)return;
            var bottom=new Rect(top.x+34,r.y+Line+2,top.width-34,Line);const float button=64;
            string walls=level.lamps==null||level.lamps.Length==0?"no lamp":level.lamps.Length>1?"both walls":level.lamps[0]==0?"left wall":"right wall";
            string about=(string.IsNullOrWhiteSpace(level.button)?"(no name)":level.button)+"  ·  "+walls+(index==0?"  ·  teaches the roll":"");
            EditorGUI.LabelField(new Rect(bottom.x,bottom.y,bottom.width-button*2-8,Line),about,EditorStyles.miniLabel);
            if(GUI.Button(new Rect(bottom.xMax-button*2-4,bottom.y,button,Line),new GUIContent("Gym","Open this level in the Shadow Gym"),EditorStyles.miniButton))
                EditorApplication.delayCall+=()=>MemoryChaptersTools.OpenInGym(level);
            if(GUI.Button(new Rect(bottom.xMax-button,bottom.y,button,Line),new GUIContent("Headset","Build and Run: start the game at this chapter on the selected headset"),EditorStyles.miniButton))
                EditorApplication.delayCall+=()=>MemoryChaptersTools.RunOnHeadset(level);
        }
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Drag a Torch2Level into a slot (Shadow Gym > Export makes them in Resources/ShadowArt/Torch2). "+
                "The chapter's number, colour and teaching go with the slot. Progress is saved per level id, so a level swapped in starts unfound.",MessageType.None);
            serializedObject.Update();list.DoLayoutList();serializedObject.ApplyModifiedProperties();
            var problems=((MemoryChapters)target).Check();
            if(problems.Count==0)EditorGUILayout.HelpBox("Ready: "+MemoryChapters.Count+" chapters, nothing missing.",MessageType.Info);
            foreach(var problem in problems)EditorGUILayout.HelpBox(problem.Text,problem.Error?MessageType.Error:MessageType.Warning);
        }
    }
}
