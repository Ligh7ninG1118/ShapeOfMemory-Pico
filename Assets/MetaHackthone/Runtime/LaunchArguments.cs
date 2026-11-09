using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    // Options a launch can carry: on a headset the start intent's string extras (Build and Run starts the app with
    // `am start ... --es level <id>`), in the editor whatever Overrides holds. Read them once, at startup: the app is
    // force-stopped before each Build and Run launch, so the intent is always the one that started it.
    public static class LaunchArguments
    {
        public static readonly Dictionary<string,string> Overrides=new Dictionary<string,string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>Overrides.Clear();

        public static string Get(string key)
        {
            if(Overrides.TryGetValue(key,out var value)) return value;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intent=UnityEngine.Android.AndroidApplication.currentActivity.Call<AndroidJavaObject>("getIntent");
                return intent?.Call<string>("getStringExtra",key);
            }
            catch(Exception e) { Debug.LogWarning("LaunchArguments: could not read the start intent: "+e.Message); }
#endif
            return null;
        }
    }
}
