using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicalAssembly.Editor
{
    // What Build and Run builds and where, and the actions themselves. The choice is per user and machine (EditorPrefs),
    // shared by the window, the toolbar and the menu.
    public static class BuildAndRun
    {
        const string Prefix="MetaHackthone.BuildAndRun.";
        public const string AnyHeadset="",NoHeadset="-";
        public static event Action Changed;

        public static DeployConfig Config
        {
            get
            {
                var all=DeployConfig.All(); string guid=EditorPrefs.GetString(Prefix+"Config","");
                return all.FirstOrDefault(c=>Guid(c)==guid)??all.FirstOrDefault();
            }
            set { EditorPrefs.SetString(Prefix+"Config",value?Guid(value):""); Changed?.Invoke(); }
        }
        static string Guid(DeployConfig config)=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
        // A serial, AnyHeadset (the first one the game can run on) or NoHeadset (build only).
        public static string Serial { get=>EditorPrefs.GetString(Prefix+"Serial",AnyHeadset); set { EditorPrefs.SetString(Prefix+"Serial",value); Changed?.Invoke(); } }
        // Only used without a headset; otherwise the headset decides.
        public static DevicePlatform Platform { get=>(DevicePlatform)EditorPrefs.GetInt(Prefix+"Platform",0); set { EditorPrefs.SetInt(Prefix+"Platform",(int)value); Changed?.Invoke(); } }
        public static bool Development { get=>EditorPrefs.GetBool(Prefix+"Development",true); set { EditorPrefs.SetBool(Prefix+"Development",value); Changed?.Invoke(); } }
        public static bool CaptureLogs { get=>EditorPrefs.GetBool(Prefix+"CaptureLogs",true); set { EditorPrefs.SetBool(Prefix+"CaptureLogs",value); Changed?.Invoke(); } }
        // The level a game with a LevelFolder starts at, per game; "" starts it normally.
        public static string LevelOf(DeployConfig config)=>config?EditorPrefs.GetString(Prefix+"Level."+Guid(config),""):"";
        public static void SetLevel(DeployConfig config,string level) { EditorPrefs.SetString(Prefix+"Level."+Guid(config),level??""); Changed?.Invoke(); }
        public static bool Clean;
        public static string Status { get; private set; }="";
        public static MessageType StatusType { get; private set; }

        public static Headsets.Headset Target(DeployConfig config)
        {
            if(Serial==NoHeadset || !config) return null;
            var ready=Headsets.Connected.Where(h=>h.Status==Headsets.Status.Ready && h.Platform!=null);
            return Serial!=AnyHeadset?ready.FirstOrDefault(h=>h.Serial==Serial):ready.FirstOrDefault(h=>config.Supports(h.Platform.Value));
        }
        public static DevicePlatform PlatformFor(DeployConfig config,Headsets.Headset headset)
        {
            if(headset!=null) return headset.Platform.Value;
            var other=Platform==DevicePlatform.Quest?DevicePlatform.Pico:DevicePlatform.Quest;
            return config && !config.Supports(Platform) && config.Supports(other)?other:Platform;
        }
        // Why there's no headset to run on, as something to do about it.
        public static string HeadsetAdvice(DeployConfig config)
        {
            if(Headsets.AdbError!=null) return "adb isn't working: "+Headsets.AdbError+"\nIn Unity Hub, add Android Build Support (with the SDK and NDK tools) to this Unity version.";
            var picked=Serial==AnyHeadset?null:Headsets.Connected.FirstOrDefault(h=>h.Serial==Serial);
            var all=picked!=null?new[] { picked }:Headsets.Connected;
            if(all.Length==0) return Serial==AnyHeadset?"No headset found. Plug it into this computer with a USB-C data cable; it needs developer mode (Docs/BuildAndRun.md).":"That headset isn't connected. Plug it in, or pick another.";
            if(all.Any(h=>h.Status==Headsets.Status.Unauthorized)) return "Put the headset on and accept \"Allow USB debugging\" (tick Always allow).";
            if(all.Any(h=>h.Status==Headsets.Status.Offline)) return "The headset is offline. Unplug it and plug it back in.";
            var other=all.FirstOrDefault(h=>h.Platform!=null);
            return other!=null?$"{config.name} has no {other.Platform} build, and {other.Name} is the headset here.":"This device isn't a Quest or PICO headset.";
        }

        // With "No headset (build only)" picked, the toolbar button and the menu item build without installing.
        public static void BuildAndRunNow()=>Do(true,Serial!=NoHeadset);
        public static void BuildOnly()=>Do(true,false);
        public static void InstallAndRun()=>Do(false,true);
        public static void RunOnly()=>Run(null);
        // The gym's Run on headset: start the game at a level, building only when the APK lacks the level as it is now,
        // and installing only when the headset lacks that APK. Other code changes since the build go unnoticed (Stale).
        public static void RunLevel(DeployConfig config,string level)
        {
            Config=config; SetLevel(config,level); Headsets.Refresh();
            var headset=Target(config);
            if(headset==null) { Fail(Serial==NoHeadset?"Pick a headset in Build and Run first (it's set to build only).":HeadsetAdvice(config)); return; }
            var platform=headset.Platform.Value;
            string stale=BuildRunner.Stale(config,platform,level);
            if(stale!=null) { Report($"Building, since {stale}.",MessageType.Info); Do(true,true); return; }
            string apk=config.ApkPath(platform),package=BuildRunner.LoadRecord(apk)?.packageId??config.Package;
            if(Headsets.HasInstalled(headset.Serial,package,apk)) Run(headset,config,platform,package); else Do(false,true);
        }

        static void Do(bool build,bool install)
        {
            var config=Config; if(!config) { Fail("No build config. Create one with Assets > Create > MetaHackthone > Build and Run config."); return; }
            if(install) Headsets.Refresh();
            var headset=Target(config);
            if(install && headset==null) { Fail(Serial==NoHeadset?"Pick a headset to install on, or use Build only.":HeadsetAdvice(config)); return; }
            var platform=PlatformFor(config,headset);
            if(!config.Supports(platform)) { Fail($"{config.name} has no {platform} build."); return; }
            string apk=config.ApkPath(platform);
            if(build)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode) { Fail("Stop Play mode first."); return; }
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)
                {
                    if(EditorUtility.DisplayDialog("Switch to Android?","Headset builds need the Android build target. Switching reimports assets for Android, which can take a while. Press Build and Run again once it's done.","Switch to Android","Cancel"))
                        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android);
                    Fail("Switch the build target to Android first."); return;
                }
                // Only open scenes the build takes need saving first; the gym's, say, doesn't.
                var scenes=config.ScenePaths;
                if(Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isDirty && scenes.Contains(s.path)) &&
                    !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                Report($"Building {config.name} for {platform}...",MessageType.Info);
                BuildReport report;
                try { report=BuildRunner.Build(config,platform,Development,Clean); }
                catch(Exception e) { Fail("Build failed: "+e.Message); return; }
                Clean=false; config=Config; // The build unloaded the old object.
                if(report.summary.result!=BuildResult.Succeeded) { Fail($"Build {report.summary.result.ToString().ToLowerInvariant()}: {report.summary.totalErrors} errors. See the Console."); return; }
                if(!install) { Report($"Built {Path.GetFileName(apk)} ({new FileInfo(apk).Length/1e6f:0} MB).",MessageType.Info); return; }
            }
            if(!File.Exists(apk)) { Fail($"No {Path.GetFileName(apk)} yet. Build it first."); return; }
            string package=BuildRunner.LoadRecord(apk)?.packageId??config.Package;
            string error=Headsets.Install(headset.Serial,apk);
            if(error!=null && error.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE") && EditorUtility.DisplayDialog("Replace the app?",
                $"The {config.Product} on {headset.Name} was built on another computer, so this build can't update it. Uninstall it first? Its saved progress on the headset goes with it.","Uninstall and install","Cancel"))
            {
                Headsets.Adb($"-s {headset.Serial} uninstall {package}",out _);
                error=Headsets.Install(headset.Serial,apk);
            }
            if(error!=null) { Fail("Install failed: "+(Headsets.Advice(error)??error)); return; }
            Headsets.MarkInstalled(headset.Serial,package,apk);
            Run(headset,config,platform,package);
        }
        static void Run(Headsets.Headset headset,DeployConfig config=null,DevicePlatform? platform=null,string package=null)
        {
            if(!config) config=Config;
            if(!config) { Fail("No build config."); return; }
            if(headset==null) { Headsets.Refresh(); headset=Target(config); }
            if(headset==null) { Fail(HeadsetAdvice(config)); return; }
            string apk=config.ApkPath(platform??headset.Platform.Value);
            package??=BuildRunner.LoadRecord(apk)?.packageId??config.Package;
            string level=LevelOf(config); if(!config.Levels.Contains(level)) level="";
            string log=CaptureLogs?Headsets.StartLog(headset.Serial,(Path.GetFileNameWithoutExtension(apk)+(level!=""?"-"+level:"")+"-"+headset.Name).Replace(' ','_')):null;
            string error=level!=""?Headsets.Launch(headset.Serial,package,("level",level)):Headsets.Launch(headset.Serial,package);
            if(error!=null) { Headsets.StopLog(); Fail("Couldn't start the app: "+error); return; }
            Report($"Running {config.Product}{(level!=""?" at "+level:"")} on {headset.Name}."+(Headsets.Asleep(headset.Serial)?" The headset is asleep: put it on.":"")+(log!=null?"\nLog: "+log:""),MessageType.Info);
        }

        public static void Stop()=>OnHeadset(true,(headset,package)=>{ Headsets.Adb($"-s {headset.Serial} shell am force-stop {package}",out _); Headsets.StopLog(); return $"Stopped {package} on {headset.Name}."; });
        public static void ClearData()=>OnHeadset(EditorUtility.DisplayDialog("Clear app data?",$"Deletes {Config?.Product}'s saved progress and settings on the headset.","Clear","Cancel"),
            (headset,package)=>{ string output=Headsets.Adb($"-s {headset.Serial} shell pm clear {package}",out int exit); return exit==0?$"Cleared {package}'s data on {headset.Name}.":"Clear failed: "+output.Trim(); });
        public static void Uninstall()=>OnHeadset(EditorUtility.DisplayDialog("Uninstall?",$"Removes {Config?.Product} and its saved progress from the headset.","Uninstall","Cancel"),
            (headset,package)=>{ string output=Headsets.Adb($"-s {headset.Serial} uninstall {package}",out int exit); return exit==0?$"Uninstalled {package} from {headset.Name}.":"Uninstall failed: "+output.Trim(); });
        static void OnHeadset(bool confirmed,Func<Headsets.Headset,string,string> action)
        {
            if(!confirmed) return;
            var config=Config; Headsets.Refresh(); var headset=config?Target(config):null;
            if(headset==null) { Fail(config?HeadsetAdvice(config):"No build config."); return; }
            string package=BuildRunner.LoadRecord(config.ApkPath(headset.Platform.Value))?.packageId??config.Package;
            Report(action(headset,package),MessageType.Info);
        }

        static void Report(string message,MessageType type)
        {
            Status=message; StatusType=type; Changed?.Invoke();
            if(type==MessageType.Error) Debug.LogWarning("BUILD_AND_RUN "+message); else Debug.Log("BUILD_AND_RUN "+message);
        }
        static void Fail(string message)=>Report(message,MessageType.Error);
    }
}
