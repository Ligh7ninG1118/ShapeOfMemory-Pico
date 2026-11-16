using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShapeOfMemory
{
    // The environment artist's sky scene (MemoryLook.EnvironmentScene: Scenes/PicoFinal_Environment.unity, see
    // Docs/EnvironmentIntegrationHandoff.md) round the player: ruins and clouds on PicoFinal/Floating Chips Baked Lit, with
    // their own lightmaps, authored round the desktop anchor (origin = the floor under the stage, +z forward).
    // Load: additively, as the game starts (the title's dark, so activation's mesh and lightmap upload falls in it). Its
    //   Environment root is switched off the moment the scene activates, and EditorOnly roots (_Guides) with it, since
    //   Play mode in the editor doesn't strip them. (Not allowSceneActivation = false: a scene held back from activation
    //   stalls the other async loads behind it, and the localization tables load through Addressables in the title.)
    // Show: with the game's sky (MemorySky.Alpha): off while the room or the title's dark shows, on once the sky starts to
    //   show; during the sky's fade MemorySky's PICOFINAL_ENV_FADE keyword dissolves it in with the same noise as the sky.
    // Place: follows the stage (the handoff's option A, as MemoryClouds and the sky dome do): the root on the floor under
    //   the stage, turned with it (MemorySky's _PicoFinalEnvYaw turns the haze's cubemap lookup and the rim light too).
    // Far plane: the scene reaches about 900 m, so the view camera's far plane goes to at least FarPlane (the sky dome sits
    //   at 0.8 of it, MemorySky.Round, behind the farthest cloud).
    // Hide: names of groups under Environment/Art to leave off (MemoryLook.EnvironmentHide), for mixing with our own
    //   clouds or ruins; with all of them off, the scene only lends its bake to the ruin models MemoryClouds lays out.
    //   Giants (MemoryLook.EnvironmentGiants): the far pieces on the *_Giant materials stay on even in those groups (the
    //   group stays active with only its other renderers off), for a horizon past our own layout.
    // Fog: the look's one fog for the scene's clouds and ruins (MemoryLook.Fog, MemoryFog), published every frame with
    //   the floor's height; off, each material keeps its own haze.
    public sealed class MemoryEnvironment : MonoBehaviour
    {
        public static bool IsGiant(Renderer r)=>r&&r.sharedMaterial&&r.sharedMaterial.name.Contains("Giant");
        public const string RootName="Environment",ArtName="Art",FadeKeyword=MemorySky.FadeKeyword;
        public const float FarPlane=1200;

        public string ScenePath{get;private set;}
        // The scene's Environment root once loaded (inactive until the sky shows).
        public Transform Root{get;private set;}
        public bool Shown=>Root&&Root.gameObject.activeSelf;
        MemorySky sky;
        Transform stage;
        Func<float> floor;
        string[] hide;
        bool giants;

        // The cloud sea's group (MemoryLook.EnvironmentSeaMaterial goes on its renderers that use SeaMaterialName).
        public const string SeaName="Cloud_Sea",SeaMaterialName="MT_CloudSea";
        Material seaMaterial;
        // Fog: the look's (MemoryLook.Fog), published every frame while the scene is loaded, so it can be tuned in Play
        // mode; SeaShading: the material the sea is drawn with, whose tops' colour the buildings fade into.
        public MemoryFog Fog{get;private set;}
        public Material SeaShading{get;private set;}
        // The fog's colour round the horizon, drawn from the sky when it changes.
        public MemoryFogRing FogRing{get;private set;}
        public static MemoryEnvironment Add(GameObject host,string scenePath,string[] hide,MemorySky sky,Transform stage,Func<float> floor,Camera view,bool giants=false,Material seaMaterial=null,MemoryFog fog=null)
        {
            var it=host.AddComponent<MemoryEnvironment>();
            it.ScenePath=scenePath;it.hide=hide??Array.Empty<string>();it.sky=sky;it.stage=stage;it.floor=floor;it.giants=giants;it.seaMaterial=seaMaterial;it.Fog=fog;
            MemoryFog.Reset();
            if(string.IsNullOrEmpty(scenePath))return it;
            if(view&&view.farClipPlane<FarPlane){Debug.Log($"SHAPE_OF_MEMORY_ENV far plane {view.farClipPlane} -> {FarPlane}");view.farClipPlane=FarPlane;}
            it.StartCoroutine(it.Load());
            return it;
        }

        IEnumerator Load()
        {
#if !UNITY_EDITOR
            if(SceneUtility.GetBuildIndexByScenePath(ScenePath)<0){Debug.LogError("SHAPE_OF_MEMORY_ENV "+ScenePath+" is not in the build (DeployConfig.Scenes): no environment");yield break;}
#endif
            var clock=System.Diagnostics.Stopwatch.StartNew();
            // Already there: the editor had it open beside the game's scene (MetaHackthone > Pico Final > Show the
            // environment in the editor), so Play brought it along. Taken as it is, before the first frame renders.
            var open=SceneManager.GetSceneByPath(ScenePath);
            if(open.IsValid()&&open.isLoaded){OnSceneLoaded(open,LoadSceneMode.Additive);Debug.Log($"SHAPE_OF_MEMORY_ENV {ScenePath} was open already: taken, root={(Root?Root.name:"MISSING")}");yield break;}
            SceneManager.sceneLoaded+=OnSceneLoaded;
            var parameters=new LoadSceneParameters(LoadSceneMode.Additive);
#if UNITY_EDITOR
            var loading=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,parameters);
#else
            var loading=SceneManager.LoadSceneAsync(ScenePath,parameters);
#endif
            if(loading==null){SceneManager.sceneLoaded-=OnSceneLoaded;Debug.LogError("SHAPE_OF_MEMORY_ENV could not load "+ScenePath);yield break;}
            yield return loading;
            Debug.Log($"SHAPE_OF_MEMORY_ENV loaded {ScenePath} in {clock.ElapsedMilliseconds} ms, root={(Root?Root.name:"MISSING")}, sky alpha {(sky?sky.Alpha:0):F2}");
        }

        // Runs in the activation itself, before the frame renders: nothing of the scene shows before the sky does.
        void OnSceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.path!=ScenePath)return;
            SceneManager.sceneLoaded-=OnSceneLoaded;
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root.CompareTag("EditorOnly")){root.SetActive(false);continue;}
                if(root.name==RootName)Root=root.transform;
            }
            if(!Root){Debug.LogError("SHAPE_OF_MEMORY_ENV no "+RootName+" root in "+scene.name);return;}
            Root.gameObject.SetActive(false);
            var art=Root.Find(ArtName);
            int kept=0;
            foreach(var name in hide)
            {
                var group=art?art.Find(name):null;
                if(!group){Debug.LogWarning("SHAPE_OF_MEMORY_ENV no group "+name+" under "+RootName+"/"+ArtName);continue;}
                var renderers=group.GetComponentsInChildren<Renderer>(true);
                if(giants&&renderers.Any(IsGiant)){foreach(var r in renderers){bool keep=IsGiant(r);r.enabled=keep;if(keep)kept++;}}
                else group.gameObject.SetActive(false);
            }
            if(giants)Debug.Log($"SHAPE_OF_MEMORY_ENV kept {kept} giants on the horizon");
            var sea=art?art.Find(SeaName):null;
            if(seaMaterial&&sea)
            {
                int swapped=0;
                foreach(var r in sea.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial&&r.sharedMaterial.name==SeaMaterialName){r.sharedMaterial=seaMaterial;swapped++;}
                Debug.Log($"SHAPE_OF_MEMORY_ENV sea on {seaMaterial.name} ({swapped} renderers)");
            }
            if(sea)foreach(var r in sea.GetComponentsInChildren<Renderer>(true))
            {
                var m=r.sharedMaterial;
                if(m&&(m.name==SeaMaterialName||m==seaMaterial)){SeaShading=m;break;}
            }
            if(Fog!=null&&Fog.On)Debug.Log($"SHAPE_OF_MEMORY_ENV fog: {Fog}; cloud tops #{ColorUtility.ToHtmlStringRGB(SeaShading?MemoryFog.CloudTop(SeaShading):Fog.MistColour)} from {(SeaShading?SeaShading.name:"none")}");
            Place();
        }

        void LateUpdate()
        {
            if(!Root)return;
            Place();
            if(Fog!=null){if(Fog.On)FogRing??=new MemoryFogRing();Fog.Publish(Root.position.y,SeaShading,FogRing);}
            float alpha=sky?sky.Alpha:1;
            bool show=alpha>0;
            if(Root.gameObject.activeSelf!=show){Root.gameObject.SetActive(show);if(show)Debug.Log("SHAPE_OF_MEMORY_ENV showing");}
        }

        void Place()
        {
            float yaw=stage?stage.eulerAngles.y:0;var at=stage?stage.position:Vector3.zero;
            Root.SetPositionAndRotation(new Vector3(at.x,floor!=null?floor():0,at.z),Quaternion.Euler(0,yaw,0));
        }

        void OnDestroy(){SceneManager.sceneLoaded-=OnSceneLoaded;MemoryFog.Reset();FogRing?.Dispose();FogRing=null;}
    }
}
