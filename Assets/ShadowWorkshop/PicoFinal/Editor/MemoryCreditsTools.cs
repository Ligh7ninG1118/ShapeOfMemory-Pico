using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShapeOfMemory.Editor
{
    // The credits asset (MemoryCredits): MetaHackthone > Pico Final > Credits selects it (making it if missing); its
    // inspector shows what still prints as a placeholder; a build of the game logs the same and goes on.
    public static class MemoryCreditsTools
    {
        public const string AssetPath=ShapeOfMemoryBuilder.Root+"/Resources/"+MemoryCredits.ResourcePath+".asset";

        [MenuItem("MetaHackthone/Pico Final/Credits")]
        public static void Select(){var credits=FindOrCreate();Selection.activeObject=credits;EditorGUIUtility.PingObject(credits);}
        public static MemoryCredits FindOrCreate()
        {
            var credits=AssetDatabase.LoadAssetAtPath<MemoryCredits>(AssetPath);if(credits)return credits;
            credits=ScriptableObject.CreateInstance<MemoryCredits>();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));
            AssetDatabase.CreateAsset(credits,AssetPath);AssetDatabase.SaveAssets();
            Debug.Log("SHAPE_OF_MEMORY_CREDITS made "+AssetPath);return credits;
        }
    }

    sealed class MemoryCreditsBuildCheck:IProcessSceneWithReport
    {
        public int callbackOrder=>0;
        public void OnProcessScene(Scene scene,BuildReport report)
        {
            if(report==null||scene.path!=ShapeOfMemoryBuilder.Scene)return;
            var credits=MemoryCredits.Load();
            if(!credits){Debug.LogWarning("SHAPE_OF_MEMORY_CREDITS missing: "+MemoryCreditsTools.AssetPath+" (Pico Final > Credits makes it); the credits pages print the defaults.");return;}
            foreach(var problem in credits.Check())Debug.LogWarning("SHAPE_OF_MEMORY_CREDITS "+problem);
        }
    }

    [CustomEditor(typeof(MemoryCredits))]
    sealed class MemoryCreditsInspector:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("The book's last spread, after THANK YOU. Left page: the made-by line, then the names in this order. "+
                "Right page: the thanks line, then each section's heading and lines. The pages are printed from this asset whenever the spread is shown.",MessageType.None);
            var credits=(MemoryCredits)target;
            DrawDefaultInspector();
            var problems=credits.Check();
            if(problems.Count==0)EditorGUILayout.HelpBox("Ready: nothing prints as a placeholder.",MessageType.Info);
            foreach(var problem in problems)EditorGUILayout.HelpBox(problem,MessageType.Warning);
        }
    }
}
