using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace PhysicalAssembly.Editor
{
    // Headsets on USB, through the Unity editor's own adb. Another adb version (the one on PATH, say) kills the editor's
    // adb server and devices drop out, so PATH is only a fallback for a missing Android module.
    public static class Headsets
    {
        public enum Status { Ready, Unauthorized, Offline }
        public sealed class Headset
        {
            public string Serial,Name; public DevicePlatform? Platform; public Status Status;
            public string Label=>Status==Status.Ready?$"{Name} ({Short(Serial)})":$"{Status} headset ({Short(Serial)})";
        }
        static string Short(string serial)=>serial.Length>9?serial.Substring(0,4)+"..."+serial.Substring(serial.Length-4):serial;

        static readonly Dictionary<string,(DevicePlatform? platform,string name)> identities=new Dictionary<string,(DevicePlatform?,string)>();
        static Task refreshing; static int version;
        public static Headset[] Connected { get; private set; }=new Headset[0];
        public static int Version=>version;
        public static string AdbError { get; private set; }

        public static string AdbPath
        {
            get
            {
                string adb=Path.Combine(AndroidExternalToolsSettings.sdkRootPath??"","platform-tools","adb.exe");
                return File.Exists(adb)?adb:"adb";
            }
        }
        public static string Adb(string arguments,out int exit,int timeoutMs=15000)=>Adb(AdbPath,arguments,out exit,timeoutMs);
        static string Adb(string adb,string arguments,out int exit,int timeoutMs)
        {
            exit=-1;
            try
            {
                using var process=Process.Start(Info(adb,arguments));
                var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(timeoutMs)) { try { process.Kill(); } catch(Exception) { } return "adb timed out: "+arguments; }
                exit=process.ExitCode; return output.Result+error.Result;
            }
            catch(Exception e) { return "adb could not start ("+adb+"): "+e.Message; }
        }
        static ProcessStartInfo Info(string file,string arguments)=>new ProcessStartInfo(file,arguments) { UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true };

        // Long adb calls (installs take 10-30 s over USB) run behind a cancelable progress bar, on the main thread, so the
        // dialogs that may follow need no thread hand-off.
        public static string AdbWithProgress(string title,string arguments,out int exit,float expectedSeconds)
        {
            exit=-1; var clock=Stopwatch.StartNew();
            try
            {
                using var process=Process.Start(Info(AdbPath,arguments));
                var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
                while(!process.WaitForExit(100))
                    if(EditorUtility.DisplayCancelableProgressBar(title,$"{clock.Elapsed.TotalSeconds:0} s",Mathf.Min(.95f,(float)clock.Elapsed.TotalSeconds/expectedSeconds)))
                    { try { process.Kill(); } catch(Exception) { } return "Cancelled."; }
                exit=process.ExitCode; return output.Result+error.Result;
            }
            catch(Exception e) { return "adb could not start: "+e.Message; }
            finally { EditorUtility.ClearProgressBar(); }
        }

        public static List<(string serial,string state)> ParseDevices(string output)
        {
            var devices=new List<(string,string)>();
            foreach(var raw in output.Split('\n'))
            {
                string line=raw.Trim(); if(line=="" || line.StartsWith("*") || line.StartsWith("List of devices")) continue;
                var parts=line.Split((char[])null,StringSplitOptions.RemoveEmptyEntries); if(parts.Length>=2) devices.Add((parts[0],parts[1]));
            }
            return devices;
        }
        // ro.product.manufacturer / ro.product.model: Quest reports Oculus / "Quest 2", the PICO 4 Ultra Pico / A9210.
        public static (DevicePlatform? platform,string name) Classify(string manufacturer,string model)
        {
            manufacturer=manufacturer.Trim(); model=model.Trim();
            bool Has(string text,string part)=>text.IndexOf(part,StringComparison.OrdinalIgnoreCase)>=0;
            if(Has(manufacturer,"oculus") || Has(manufacturer,"meta") || model.StartsWith("Quest",StringComparison.OrdinalIgnoreCase))
                return (DevicePlatform.Quest,model.StartsWith("Quest",StringComparison.OrdinalIgnoreCase)?model:"Meta "+model);
            if(Has(manufacturer,"pico")) return (DevicePlatform.Pico,model=="A9210"?"PICO 4 Ultra":"PICO "+model);
            return (null,(manufacturer+" "+model).Trim());
        }

        public static void Refresh()=>Connected=Query(AdbPath);
        public static void RefreshInBackground()
        {
            if(refreshing!=null && !refreshing.IsCompleted) return;
            string adb=AdbPath; // EditorPrefs-backed, so read it here, on the main thread.
            refreshing=Task.Run(()=>Connected=Query(adb));
        }
        static Headset[] Query(string adb)
        {
            string output=Adb(adb,"devices",out int exit,10000);
            AdbError=exit==0?null:output.Trim();
            var headsets=ParseDevices(exit==0?output:"").Select(d=>new Headset { Serial=d.serial,Name=d.serial,
                Status=d.state=="device"?Status.Ready:d.state=="unauthorized"?Status.Unauthorized:Status.Offline }).ToArray();
            foreach(var headset in headsets.Where(h=>h.Status==Status.Ready))
            {
                (DevicePlatform? platform,string name) identity;
                lock(identities)
                    if(!identities.TryGetValue(headset.Serial,out identity))
                    {
                        var props=Adb(adb,$"-s {headset.Serial} shell \"getprop ro.product.manufacturer; getprop ro.product.model\"",out int ok,10000).Split('\n');
                        identity=ok==0 && props.Length>=2?Classify(props[0],props[1]):(null,headset.Serial);
                        if(ok==0) identities[headset.Serial]=identity;
                    }
                headset.Platform=identity.platform; headset.Name=identity.name;
            }
            if(!Same(headsets,Connected)) Interlocked.Increment(ref version);
            return headsets;
        }
        static bool Same(Headset[] a,Headset[] b)=>a.Length==b.Length && a.Zip(b,(x,y)=>x.Serial==y.Serial && x.Status==y.Status && x.Name==y.Name).All(s=>s);

        // Install output to plain advice; null when there's nothing better to say than adb's own words.
        public static string Advice(string adbOutput)
        {
            if(adbOutput.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE")) return "The copy on the headset was built on another computer (a different signing key), so it can't be updated in place. Uninstall it first.";
            if(adbOutput.Contains("INSTALL_FAILED_MISSING_SHARED_LIBRARY")) return "The headset lacks a system library the APK asks for. See Docs/BuildAndRun.md, \"Install fails\".";
            if(adbOutput.Contains("INSTALL_FAILED_INSUFFICIENT_STORAGE")) return "The headset is out of storage. Uninstall something from it.";
            if(adbOutput.Contains("unauthorized")) return "Put the headset on and accept \"Allow USB debugging\".";
            if(adbOutput.Contains("device offline") || adbOutput.Contains("not found")) return "The headset disconnected. Plug it back in.";
            return null;
        }
        public static string Install(string serial,string apk)
        {
            float megabytes=new FileInfo(apk).Length/1e6f;
            string output=AdbWithProgress("Installing "+Path.GetFileName(apk),$"-s {serial} install -r \"{Path.GetFullPath(apk)}\"",out int exit,5+megabytes/12);
            return exit==0 && output.Contains("Success")?null:output.Trim();
        }
        // Restarts the app: Build and Run should never resume a stale session. Each extra becomes a string extra of the
        // start intent (LaunchArguments reads them).
        public static string Launch(string serial,string package,params (string key,string value)[] extras)
        {
            Adb($"-s {serial} shell am force-stop {package}",out _);
            string resolved=Adb($"-s {serial} shell cmd package resolve-activity --brief -c android.intent.category.LAUNCHER {package}",out _);
            string activity=resolved.Split('\n').Select(l=>l.Trim()).LastOrDefault(l=>l.StartsWith(package+"/"));
            if(activity==null) return $"{package} is not installed on the headset.";
            if(extras.Any(e=>(e.key+e.value).IndexOfAny(new[] { '\'','"','\\' })>=0)) return "Launch arguments can't contain quotes or backslashes.";
            // One argument for the device's shell, values in single quotes, so names with spaces arrive whole.
            string command=$"am start -n {activity}"+string.Concat(extras.Select(e=>$" --es {e.key} '{e.value}'"));
            string output=Adb($"-s {serial} shell \"{command}\"",out int exit);
            return exit==0 && !output.Contains("Error")?null:output.Trim();
        }
        // Whether the headset has this very APK installed (Build and Run recorded it, and the package is still there).
        public static bool HasInstalled(string serial,string package,string apk)=>File.Exists(apk) &&
            EditorPrefs.GetString(InstalledKey(serial,package),"")==File.GetLastWriteTimeUtc(apk).Ticks.ToString() &&
            Adb($"-s {serial} shell pm path {package}",out int exit).Contains("package:") && exit==0;
        public static void MarkInstalled(string serial,string package,string apk)=>EditorPrefs.SetString(InstalledKey(serial,package),File.GetLastWriteTimeUtc(apk).Ticks.ToString());
        static string InstalledKey(string serial,string package)=>$"MetaHackthone.Headsets.Installed.{serial}.{package}";
        public static bool Asleep(string serial)=>Adb($"-s {serial} shell dumpsys power",out _).Contains("mWakefulness=Asleep");

        // The app's log, streamed to Logs/Headsets. Pico keeps only 256 KB of log, which rolls over within minutes, so it
        // starts before the app does. cmd.exe owns the redirect so capture survives script reloads; its id is in SessionState.
        const string LogPid="MetaHackthone.Headsets.LogPid",LogFile="MetaHackthone.Headsets.LogFile";
        public static string LogPath=>SessionState.GetString(LogFile,"");
        public static bool Logging
        {
            get
            {
                int pid=SessionState.GetInt(LogPid,0); if(pid==0) return false;
                try { return !Process.GetProcessById(pid).HasExited; } catch(Exception) { return false; }
            }
        }
        public static string StartLog(string serial,string name)
        {
            StopLog(); Directory.CreateDirectory("Logs/Headsets");
            string file=Path.GetFullPath($"Logs/Headsets/{name}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var process=Process.Start(new ProcessStartInfo("cmd.exe",$"/s /c \"\"{AdbPath}\" -s {serial} logcat -T 1 -v time Unity:V CRASH:V AndroidRuntime:E DEBUG:I libc:F *:S > \"{file}\" 2>&1\"") { UseShellExecute=false,CreateNoWindow=true });
            SessionState.SetInt(LogPid,process.Id); SessionState.SetString(LogFile,file);
            return file;
        }
        public static void StopLog()
        {
            int pid=SessionState.GetInt(LogPid,0); if(pid==0) return;
            SessionState.EraseInt(LogPid);
            try { Process.Start(new ProcessStartInfo("taskkill",$"/PID {pid} /T /F") { UseShellExecute=false,CreateNoWindow=true })?.WaitForExit(3000); } catch(Exception) { }
        }
        [InitializeOnLoadMethod] static void StopLogOnQuit()=>EditorApplication.quitting+=StopLog;
    }
}
