using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShapeOfMemory.Tests
{
    // The environment artist's scene in the deliverable (MemoryEnvironment): loaded but hidden through the title and over
    // the room, dissolving in with the sky (the fade keyword only during the fade), turning with the stage, the far plane
    // and the sky dome beyond it. Ignored when the scene's backdrop has no environment scene.
    public class MemoryEnvironmentPlayTests
    {
        const string Scene="Assets/ShadowWorkshop/PicoFinal/Scenes/PicoFinal.unity";
        [TearDown] public void Clear()=>MemoryGame.StoreOverride=null;

        [UnityTest] public IEnumerator EnvironmentComesUpWithTheSkyAndTurnsWithTheStage()
        {
            MemoryGame.StoreOverride=new MemoryProgress.InMemory();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Scene,new LoadSceneParameters(LoadSceneMode.Single));
#endif
            MemoryGame game=null;float deadline=Time.realtimeSinceStartup+10;
            while((!game||!game.Ready)&&Time.realtimeSinceStartup<deadline){game=Object.FindFirstObjectByType<MemoryGame>();yield return null;}
            Assert.IsTrue(game&&game.Ready,"the game starts");
            var look=game.Look;
            if(!look||!look.HasEnvironment)Assert.Ignore("this look has no environment scene");
            var env=game.Environment;Assert.NotNull(env);
            deadline=Time.realtimeSinceStartup+15;while(!env.Root&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(env.Root,"the scene loads and has its root");
            Assert.IsFalse(env.Shown,"hidden through the title");
            var scene=env.Root.gameObject.scene;
            Assert.IsTrue(scene.GetRootGameObjects().Where(r=>r.CompareTag("EditorOnly")).All(r=>!r.activeSelf),"the guides stay off in Play mode");
            Assert.GreaterOrEqual(game.Player.ViewCamera.farClipPlane,MemoryEnvironment.FarPlane,"far plane");
            // Groups left off draw nothing, or only their giants when the look keeps those for the horizon.
            int giants=0;
            foreach(var name in look.EnvironmentHide)
            {
                var group=env.Root.Find(MemoryEnvironment.ArtName+"/"+name);
                if(!group.gameObject.activeSelf)continue;
                foreach(var r in group.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.AreEqual(look.EnvironmentGiants&&MemoryEnvironment.IsGiant(r),r.enabled,$"{name}/{r.name} {(r.enabled?"drawn":"left off")}");
                    if(r.enabled)giants++;
                }
            }
            if(look.EnvironmentGiants)Assert.Greater(giants,10,"the giants stay for the horizon");

            game.FinishTitle();deadline=Time.realtimeSinceStartup+3;while(game.Book.Sealed&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;
            Assert.IsFalse(env.Shown,"still hidden over the room");
            Assert.IsFalse(Shader.IsKeywordEnabled(MemoryEnvironment.FadeKeyword),"no clip before the sky");

            game.Sky.Show();
            bool fading=false;deadline=Time.realtimeSinceStartup+MemorySky.FadeSeconds+2;
            while(game.Sky.Alpha<1&&Time.realtimeSinceStartup<deadline)
            {
                yield return null;
                float alpha=game.Sky.Alpha;
                if(alpha>.05f&&alpha<.95f&&env.Shown&&Shader.IsKeywordEnabled(MemoryEnvironment.FadeKeyword)&&Mathf.Abs(Shader.GetGlobalFloat("_PicoFinalEnvVisible")-alpha)<.2f)fading=true;
            }
            Assert.IsTrue(fading,"it dissolves in with the sky");
            yield return null;yield return null;
            Assert.IsTrue(env.Shown,"up once the sky is");
            Assert.IsFalse(Shader.IsKeywordEnabled(MemoryEnvironment.FadeKeyword),"the clip is off once it is up");

            // The sky dome draws after it with a depth test, so it must lie behind every piece (the giants are 450-650 m out).
            var dome=game.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="Sky dome");
            float radius=dome.transform.lossyScale.x*.5f;var eye=game.Player.ViewCamera.transform.position;
            foreach(var r in env.Root.GetComponentsInChildren<Renderer>())Assert.Less(Vector3.Distance(eye,r.bounds.center),radius,r.name+" inside the sky dome");

            // Follows the stage (recentring on a headset): on the floor under it, turned with it, the haze and rim told.
            var rest=game.Stage.rotation;
            game.Stage.rotation=Quaternion.Euler(0,90,0);yield return null;
            Assert.AreEqual(90,env.Root.eulerAngles.y,.01f,"turned with the stage");
            Assert.AreEqual(90,Shader.GetGlobalFloat("_PicoFinalEnvYaw"),.01f,"the haze and rim turn too");
            Assert.AreEqual(game.Player.FloorHeight,env.Root.position.y,1e-4f,"on the floor");
            Assert.AreEqual(game.Stage.position.x,env.Root.position.x,1e-4f);Assert.AreEqual(game.Stage.position.z,env.Root.position.z,1e-4f);
            // The glow shows 20 degrees left of forward (the environment's bake, rim and haze assume it), and the light the
            // clouds and ruins get (_MemorySunDir) comes from where the dome puts the glow.
            Assert.AreEqual(-20,look.GlowFacing,1e-4f,"the environment's glow 20 degrees left");
            var sun=Shader.GetGlobalVector("_MemorySunDir");float lit=Mathf.Atan2(sun.x,sun.z)*Mathf.Rad2Deg;
            Assert.AreEqual(0,Mathf.DeltaAngle(lit,90+look.GlowFacing),.01f,"the clouds' light from the glow");
            Assert.AreEqual(0,Mathf.DeltaAngle(dome.transform.eulerAngles.y+look.GlowAzimuth,90+look.GlowFacing),.01f,"the dome's glow there too");
            Assert.AreEqual(1,Shader.GetGlobalFloat("_PicoFinalHazeFromSky"),"the environment's haze reads the game's sky");
            // The look's one fog for its clouds and ruins, its heights from the floor under the stage.
            Assert.IsTrue(look.Fog.On,"the environment's look takes the one fog");
            Assert.AreEqual(1,Shader.GetGlobalFloat("_PicoFinalFog"),"fog published");
            Assert.AreEqual(env.Root.position.y+look.Fog.MistTop,Shader.GetGlobalVector("_PicoFinalFogMist").x,1e-3f,"the mist lies under the cloud tops, from the floor");
            Assert.IsTrue(env.SeaShading,"the sea's material found (the buildings fade into its tops' colour)");
            Assert.IsNotNull(env.FogRing,"the fog's colour round the horizon");
            Assert.AreEqual(1,env.FogRing.Draws,"drawn once from the sky, not every frame");
            Assert.IsTrue(Shader.GetGlobalTexture("_PicoFinalFogRing"),"and given to the shaders");
            game.Stage.rotation=rest;
        }
    }
}
