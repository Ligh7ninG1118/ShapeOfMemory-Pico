using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;
using Debug=UnityEngine.Debug;

namespace PhysicalAssembly.Editor
{
    // The one way the project builds an APK. A build temporarily changes tracked settings (package id and name, the
    // default icon when the config has one, and for Pico the OpenXR features, the manifest and the PICO config), so the
    // originals are written to Library first. The finally block puts them back; if the editor dies mid-build, the next
    // editor load does instead.
    public static class BuildRunner
    {
        // icon: the default icon's asset path before the build ("" = none), only meaningful when iconSwapped.
        [Serializable] public sealed class Session { public string config,platform,packageId,productName,icon; public bool iconSwapped; }
        // Saved next to the APK as <apk>.json: Run reads the package id from it, the window the build's age.
        [Serializable] public sealed class Record { public string config,platform,packageId,productName,built,unity; public bool development; public long bytes; public float seconds; }
        const string SessionPath="Library/MetaHackthoneBuild/session.json";
        public static Session Current { get; private set; }

        public static string ApkPath(string stem,DevicePlatform platform)=>"Builds/"+stem+(platform==DevicePlatform.Pico?"-Pico":"")+".apk";
        // Builds started elsewhere (Unity's own Build window) only have the file name to go by.
        public static bool IsPico(string outputPath)=>Current!=null?Current.platform==nameof(DevicePlatform.Pico):outputPath.EndsWith("-Pico.apk",StringComparison.OrdinalIgnoreCase);
        // Why the platform's APK lacks what a launch needs, or null when it's current: no APK yet, game code recompiled
        // since (the editor's copy of every runtime assembly keeps its file time until its code changes), or the level or
        // anything it uses (meshes, materials) changed after the build. Other scene and asset changes go unnoticed.
        public static string Stale(DeployConfig config,DevicePlatform platform,string level)
        {
            string apk=config.ApkPath(platform);
            if(!File.Exists(apk)) return $"there's no {Path.GetFileName(apk)} yet";
            var built=File.GetLastWriteTime(apk);
            var code=CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).Select(a=>$"Library/ScriptAssemblies/{a.name}.dll")
                .Where(p=>File.Exists(p) && File.GetLastWriteTime(p)>built).ToArray();
            if(code.Length>0) return $"the game's code changed after the last build ({Listed(code)})";
            if(string.IsNullOrEmpty(level)) return null;
            string path=config.LevelPath(level);
            if(!File.Exists(path)) return $"{level} doesn't exist";
            var changed=AssetDatabase.GetDependencies(path,true).Where(p=>File.Exists(p) && File.GetLastWriteTime(p)>built).ToArray();
            return changed.Length==0?null:$"{level} changed after the last build ({Listed(changed)})";
        }
        static string Listed(string[] paths)=>Path.GetFileName(paths[0])+(paths.Length>1?$" and {paths.Length-1} more":"");
        public static Record LoadRecord(string apk)
        {
            try { return File.Exists(apk+".json")?JsonUtility.FromJson<Record>(File.ReadAllText(apk+".json")):null; }
            catch(Exception) { return null; }
        }

        public static BuildReport Build(DeployConfig config,DevicePlatform platform,bool development,bool clean=false)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Wait until the editor is idle (stop Play mode first).");
            if(!config.Supports(platform)) throw new InvalidOperationException($"{config.name} has no {platform} build.");
            AndroidBuildValidation.ValidateTarget(BuildTarget.Android,EditorUserBuildSettings.activeBuildTarget);
            var scenes=config.ScenePaths;
            if(scenes.Length==0) throw new InvalidOperationException($"{config.name} has no scenes.");
            Recover(); PicoBuild.Restore(); // A Pico switch left on by hand would fail the build's guard.
            // BuildPlayer unloads loaded assets, config included: read everything from it now.
            var target=NamedBuildTarget.Android; string apk=config.ApkPath(platform),package=config.Package,product=config.Product,name=config.name;bool eyeGaze=config.PicoEyeGaze;var icon=config.Icon;
            var session=new Session { config=name,platform=platform.ToString(),packageId=PlayerSettings.GetApplicationIdentifier(target),productName=PlayerSettings.productName,
                iconSwapped=icon,icon=DefaultIconPath() };
            Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)); File.WriteAllText(SessionPath,JsonUtility.ToJson(session,true)); Current=session;
            var clock=Stopwatch.StartNew();
            try
            {
                if(platform==DevicePlatform.Pico) PicoBuild.Prepare(eyeGaze);
                PlayerSettings.SetApplicationIdentifier(target,package); PlayerSettings.productName=product;
                if(icon) SetDefaultIcon(icon);
                // An up-to-date incremental build leaves an identical APK untouched, old timestamp and all; start from none.
                Directory.CreateDirectory(Path.GetDirectoryName(apk)); File.Delete(apk); File.Delete(apk+".json");
                var options=development?BuildOptions.Development|BuildOptions.CompressWithLz4:BuildOptions.CompressWithLz4HC;
                if(clean) options|=BuildOptions.CleanBuildCache;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=scenes,target=BuildTarget.Android,targetGroup=BuildTargetGroup.Android,locationPathName=apk,options=options });
                var file=new FileInfo(apk); float seconds=(float)clock.Elapsed.TotalSeconds;
                if(report.summary.result==BuildResult.Succeeded && file.Exists)
                    File.WriteAllText(apk+".json",JsonUtility.ToJson(new Record { config=name,platform=platform.ToString(),packageId=package,productName=product,
                        built=file.LastWriteTime.ToString("o"),unity=Application.unityVersion,development=development,bytes=file.Length,seconds=seconds },true));
                Debug.Log($"BUILD_AND_RUN_BUILD result={report.summary.result} config={name} platform={platform} package={package} development={development} "+
                    $"errors={report.summary.totalErrors} seconds={seconds:0} "+(file.Exists?$"apk={file.FullName} bytes={file.Length}":"apk=missing"));
                return report;
            }
            finally { Restore(session); }
        }

        // Undoes a build that never reached its finally (editor crash or kill). Statics don't survive the domain reload
        // that follows, so a session file without a live Current is always a leftover.
        public static bool Recover()
        {
            if(Current!=null || BuildPipeline.isBuildingPlayer || !File.Exists(SessionPath)) return false;
            var session=JsonUtility.FromJson<Session>(File.ReadAllText(SessionPath));
            Debug.LogWarning($"BUILD_AND_RUN_RECOVERED an interrupted {session.config} {session.platform} build: package id {session.packageId}, name {session.productName}"+
                (session.iconSwapped?", the default icon":"")+" and the Quest configuration are restored.");
            Restore(session); return true;
        }
        static void Restore(Session session)
        {
            try
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,session.packageId); PlayerSettings.productName=session.productName;
                if(session.iconSwapped) SetDefaultIcon(session.icon==""?null:AssetDatabase.LoadAssetAtPath<Texture2D>(session.icon));
                PicoBuild.Restore(); AssetDatabase.SaveAssets();
            }
            finally { File.Delete(SessionPath); Current=null; }
        }

        // The default icon (Player Settings > Icon, every platform). Android has no icons of its own set, so the launcher
        // uses this one. Setting it and then putting back none leaves ProjectSettings.asset byte-identical.
        public static string DefaultIconPath()
        {
            var icons=PlayerSettings.GetIcons(NamedBuildTarget.Unknown,IconKind.Any);
            return icons!=null && icons.Length>0 && icons[0]?AssetDatabase.GetAssetPath(icons[0]):"";
        }
        public static void SetDefaultIcon(Texture2D icon)=>PlayerSettings.SetIcons(NamedBuildTarget.Unknown,icon?new[] { icon }:new Texture2D[0],IconKind.Any);
        [InitializeOnLoadMethod] static void RecoverOnLoad()=>EditorApplication.delayCall+=()=>Recover();

        // Batch mode: -executeMethod PhysicalAssembly.Editor.BuildRunner.BuildFromCommandLine -buildTarget Android
        //   -deployConfig "Shape of Memory" -devicePlatform Quest|Pico [-release] [-clean]
        public static void BuildFromCommandLine()
        {
            var args=Environment.GetCommandLineArgs();
            string Arg(string key) { int i=Array.IndexOf(args,key); return i>=0 && i+1<args.Length?args[i+1]:null; }
            string name=Arg("-deployConfig")??"Shape of Memory";
            var config=DeployConfig.Find(name)??throw new ArgumentException($"No build config named {name}. Configs: {string.Join(", ",DeployConfig.All().Select(c=>c.name))}");
            var platform=(DevicePlatform)Enum.Parse(typeof(DevicePlatform),Arg("-devicePlatform")??"Quest",true);
            var report=Build(config,platform,!args.Contains("-release"),args.Contains("-clean"));
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception($"{config.name} {platform} build failed: {report.summary.result}");
        }
    }
}
