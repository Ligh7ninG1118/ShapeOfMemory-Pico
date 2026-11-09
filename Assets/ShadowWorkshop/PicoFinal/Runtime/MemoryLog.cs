using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ShapeOfMemory
{
    // Each session keeps its own log on the headset, so a playtest can be read afterwards: every line the game, the hand
    // input and the levels log (SHAPE_OF_MEMORY_, METAHACKTHONE_, TORCH2_), plus errors and exceptions, written to
    // persistentDataPath/logs/session-<start>.log with seconds since the start. The newest Keep sessions stay, and a
    // session stops writing at MaxBytes. Pull them with MetaHackthone > Pico Final > Pull session logs. Clearing the
    // app's data (Clear saved progress) deletes them too.
    public static class MemoryLog
    {
        public const int Keep=20;
        public const long MaxBytes=8L<<20;
        public const string Folder="logs";
        static readonly string[] Prefixes={"SHAPE_OF_MEMORY","METAHACKTHONE","TORCH2"};
        static readonly object gate=new object();
        static StreamWriter writer;
        static Stopwatch clock;
        static long written;
        static bool full;
        static float nextFlush;
        public static string FilePath{get;private set;}
        public static string Directory=>Path.Combine(Application.persistentDataPath,Folder);
        public static bool Writing{get{lock(gate)return writer!=null&&!full;}}

        // Starts this run's file (once per run; a second call does nothing).
        public static void Begin()
        {
            lock(gate)if(writer!=null)return;
            try
            {
                System.IO.Directory.CreateDirectory(Directory);Prune();
                var path=Path.Combine(Directory,$"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                lock(gate){writer=new StreamWriter(path,false,new UTF8Encoding(false));clock=Stopwatch.StartNew();written=0;full=false;FilePath=path;}
                Application.logMessageReceivedThreaded+=Receive;Application.quitting+=End;
                Write($"session {DateTime.Now:yyyy-MM-dd HH:mm:ss} app={Application.identifier} {Application.version} build={Application.buildGUID} unity={Application.unityVersion} device={SystemInfo.deviceModel} os={SystemInfo.operatingSystem} xr={UnityEngine.XR.XRSettings.loadedDeviceName}");
                Flush();Debug.Log("SHAPE_OF_MEMORY_LOG "+path);
            }
            catch(Exception e){lock(gate){writer?.Dispose();writer=null;}Debug.LogWarning("SHAPE_OF_MEMORY_LOG unavailable: "+e.Message);}
        }
        // Main thread, every frame: flush about once a second, so a crash or a pulled battery loses little.
        public static void Tick(){if(Time.unscaledTime<nextFlush)return;nextFlush=Time.unscaledTime+1;Flush();}
        public static void Flush(){lock(gate){try{writer?.Flush();}catch(IOException){}}}
        public static void End()
        {
            Application.logMessageReceivedThreaded-=Receive;Application.quitting-=End;
            lock(gate){try{writer?.Flush();writer?.Dispose();}catch(IOException){}writer=null;}
        }
        // Whether a log line belongs in the session log (errors always do).
        public static bool Interesting(string message)
        {
            if(string.IsNullOrEmpty(message))return false;
            foreach(var prefix in Prefixes)if(message.StartsWith(prefix,StringComparison.Ordinal))return true;
            return false;
        }
        static void Receive(string message,string stack,LogType type)
        {
            bool error=type==LogType.Error||type==LogType.Exception||type==LogType.Assert;
            if(!error&&!Interesting(message))return;
            Write(error?type.ToString().ToUpperInvariant()+" "+message+Stack(type,stack):message);
        }
        static string Stack(LogType type,string stack)
        {
            if(type!=LogType.Exception||string.IsNullOrEmpty(stack))return "";
            return "\n    "+string.Join("\n    ",stack.Split('\n').Select(l=>l.Trim()).Where(l=>l.Length>0).Take(6));
        }
        static void Write(string line)
        {
            lock(gate)
            {
                if(writer==null||full)return;
                try
                {
                    var text=clock.Elapsed.TotalSeconds.ToString("0.000").PadLeft(9)+" "+line;
                    writer.WriteLine(text);written+=text.Length+1;
                    if(written>=MaxBytes){writer.WriteLine("... log full, stopped");writer.Flush();full=true;}
                }
                catch(IOException){full=true;}
            }
        }
        static void Prune()
        {
            var old=new DirectoryInfo(Directory).GetFiles("session-*.log").OrderByDescending(f=>f.Name).Skip(Keep-1);
            foreach(var file in old)try{file.Delete();}catch(IOException){}
        }
        // Domain reload is off in the editor: close a file left open by the last Play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>End();
    }
}
