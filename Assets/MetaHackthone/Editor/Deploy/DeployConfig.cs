using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace PhysicalAssembly.Editor
{
    public enum DevicePlatform { Quest, Pico }

    // One game that Build and Run can put on a headset: its scenes, the package id and name it installs under, and the
    // platforms it builds for. Any asset of this type shows up in the window, wherever it lives.
    [CreateAssetMenu(menuName="MetaHackthone/Build and Run config",fileName="Build config")]
    public sealed class DeployConfig : ScriptableObject
    {
        [Tooltip("Empty: the enabled scenes of the project's build list, in order.")] public SceneAsset[] Scenes=new SceneAsset[0];
        [Tooltip("Empty: the project's package id. A separate id installs next to the other games.")] public string PackageId="";
        [Tooltip("Empty: the project's product name. Shown under the app icon on the headset.")] public string ProductName="";
        [Tooltip("APK file name in Builds/, without .apk. Pico builds add -Pico. Empty: the asset's name.")] public string ApkName="";
        [Tooltip("App icon for this game's builds only: square, 1024 px, opaque. Unity scales it for the launcher. Empty: the project's default icon.")]
        public Texture2D Icon;
        public bool Quest=true,Pico=true;
        [Tooltip("Pico: also turn on look-and-pinch input (OpenXR Eye Gaze Interaction and PICO eye tracking, which adds the eye tracking permission). For a game that uses it. The declaration makes the PICO system show an eye tracking prompt at launch, and a headset without eye tracking (PICO 4 Ultra) never gives a gaze, so leave it off unless the target headset has it.")]
        public bool PicoEyeGaze;
        [Tooltip("Folder of level assets the game can start at. Build and Run passes the picked one's file name as the launch argument \"level\" (LaunchArguments). Empty: no level picker.")]
        public string LevelFolder="";
        [Tooltip("Position in the window's list.")] public int Order;

        public string[] Levels=>LevelFolder!="" && Directory.Exists(LevelFolder)?Directory.GetFiles(LevelFolder,"*.asset").Select(Path.GetFileNameWithoutExtension).OrderBy(n=>n).ToArray():new string[0];
        public string LevelPath(string level)=>LevelFolder+"/"+level+".asset";

        public bool Supports(DevicePlatform platform)=>platform==DevicePlatform.Quest?Quest:Pico;
        public string[] ScenePaths=>Scenes.Length==0?EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray():Scenes.Where(s=>s).Select(AssetDatabase.GetAssetPath).ToArray();
        public string Package=>PackageId!=""?PackageId:PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        public string Product=>ProductName!=""?ProductName:PlayerSettings.productName;
        public string ApkPath(DevicePlatform platform)=>BuildRunner.ApkPath(ApkName!=""?ApkName:name,platform);

        public static DeployConfig[] All()=>AssetDatabase.FindAssets("t:"+nameof(DeployConfig)).Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<DeployConfig>).Where(c=>c).OrderBy(c=>c.Order).ThenBy(c=>c.name).ToArray();
        public static DeployConfig Find(string name)=>All().FirstOrDefault(c=>string.Equals(c.name,name,StringComparison.OrdinalIgnoreCase));
    }
}
