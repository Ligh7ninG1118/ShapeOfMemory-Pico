using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShapeOfMemory.Editor
{
    // The soundtrack asset (MemorySoundtrack). MetaHackthone > Pico Final > Soundtrack selects it, and makes it if it is
    // missing. Its inspector lists what won't play as listed, and songs not imported as streamed music, with a button that
    // fixes their import. A build of the game logs the same and goes on.
    public static class MemorySoundtrackTools
    {
        public const string AssetPath=ShapeOfMemoryBuilder.Root+"/Resources/"+MemorySoundtrack.ResourcePath+".asset";
        // Where songs go.
        public const string MusicFolder=ShapeOfMemoryBuilder.Root+"/Audio/Music";

        [MenuItem("MetaHackthone/Pico Final/Soundtrack")]
        public static void Select(){var soundtrack=FindOrCreate();Selection.activeObject=soundtrack;EditorGUIUtility.PingObject(soundtrack);}
        public static MemorySoundtrack FindOrCreate()
        {
            var soundtrack=AssetDatabase.LoadAssetAtPath<MemorySoundtrack>(AssetPath);if(soundtrack)return soundtrack;
            soundtrack=ScriptableObject.CreateInstance<MemorySoundtrack>();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));
            AssetDatabase.CreateAsset(soundtrack,AssetPath);AssetDatabase.SaveAssets();
            Debug.Log("SHAPE_OF_MEMORY_MUSIC made "+AssetPath);return soundtrack;
        }

        // A song as music on the headset: streamed from storage, Vorbis at quality .6 (about 190 kbps stereo), stereo
        // kept, for every platform (no overrides). Decompressed on load, 137 s of stereo would take 24 MB of memory.
        const float Quality=.6f;
        public static bool IsMusic(AudioClip clip)
        {
            if(!(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) is AudioImporter importer))return true;
            var s=importer.defaultSampleSettings;
            return s.loadType==AudioClipLoadType.Streaming&&s.compressionFormat==AudioCompressionFormat.Vorbis&&!importer.forceToMono&&!importer.ContainsSampleSettingsOverride("Android");
        }
        public static void ImportAsMusic(AudioClip clip)
        {
            if(!(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) is AudioImporter importer))return;
            var s=importer.defaultSampleSettings;
            s.loadType=AudioClipLoadType.Streaming;s.compressionFormat=AudioCompressionFormat.Vorbis;s.quality=Quality;s.preloadAudioData=false;
            s.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings=s;importer.forceToMono=false;importer.loadInBackground=false;
            foreach(var platform in new[]{"Android","Standalone"})if(importer.ContainsSampleSettingsOverride(platform))importer.ClearSampleSettingOverride(platform);
            importer.SaveAndReimport();Debug.Log("SHAPE_OF_MEMORY_MUSIC imported as streamed music: "+importer.assetPath);
        }
        // Check() and the songs not imported as music.
        public static List<string> Problems(MemorySoundtrack soundtrack)
        {
            var problems=soundtrack.Check();
            foreach(var clip in Clips(soundtrack).Where(c=>!IsMusic(c)))problems.Add($"{clip.name} is not imported as streamed music (Import songs as music fixes it).");
            return problems;
        }
        public static IEnumerable<AudioClip> Clips(MemorySoundtrack soundtrack)=>(soundtrack.songs??new MemorySoundtrack.Song[0]).Where(s=>s!=null&&s.clip).Select(s=>s.clip).Distinct();
    }

    sealed class MemorySoundtrackBuildCheck:IProcessSceneWithReport
    {
        public int callbackOrder=>0;
        public void OnProcessScene(Scene scene,BuildReport report)
        {
            if(report==null||scene.path!=ShapeOfMemoryBuilder.Scene)return;
            var soundtrack=MemorySoundtrack.Load();
            if(!soundtrack){Debug.LogWarning("SHAPE_OF_MEMORY_MUSIC missing: "+MemorySoundtrackTools.AssetPath+" (Pico Final > Soundtrack makes it); the game has no music.");return;}
            foreach(var problem in MemorySoundtrackTools.Problems(soundtrack))Debug.LogWarning("SHAPE_OF_MEMORY_MUSIC "+problem);
        }
    }

    [CustomEditor(typeof(MemorySoundtrack))]
    sealed class MemorySoundtrackInspector:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("The book's music. It starts once the cover is first opened, and the songs play in this order. "+
                "A looping song plays on and on. Any other song is followed by the next one, and after the last the list starts over. "+
                "It fades out while a memory is up on the walls, where the closeness cue is the music, and goes on where it was when the book comes down. "+
                "Songs go in "+MemorySoundtrackTools.MusicFolder+".",MessageType.None);
            var soundtrack=(MemorySoundtrack)target;
            DrawDefaultInspector();
            var problems=MemorySoundtrackTools.Problems(soundtrack);
            if(problems.Count==0)EditorGUILayout.HelpBox("Ready.",MessageType.Info);
            foreach(var problem in problems)EditorGUILayout.HelpBox(problem,MessageType.Warning);
            var raw=MemorySoundtrackTools.Clips(soundtrack).Where(c=>!MemorySoundtrackTools.IsMusic(c)).ToArray();
            if(raw.Length>0&&GUILayout.Button("Import songs as music (streamed, Vorbis)"))foreach(var clip in raw)MemorySoundtrackTools.ImportAsMusic(clip);
        }
    }
}
