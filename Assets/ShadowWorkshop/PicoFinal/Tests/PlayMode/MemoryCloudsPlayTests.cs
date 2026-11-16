using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShapeOfMemory.Tests
{
    // The deliverable scene's clouds: hidden through the title and over the room, fading in with the sky, then fully
    // shown; one material throughout; none in the play space; the sky drawn after them.
    public class MemoryCloudsPlayTests
    {
        const string Scene="Assets/ShadowWorkshop/PicoFinal/Scenes/PicoFinal.unity";
        [TearDown] public void Clear()=>MemoryGame.StoreOverride=null;

        [UnityTest] public IEnumerator CloudsComeUpWithTheSky()
        {
            MemoryGame.StoreOverride=new MemoryProgress.InMemory();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Scene,new LoadSceneParameters(LoadSceneMode.Single));
#endif
            MemoryGame game=null;float deadline=Time.realtimeSinceStartup+10;
            while((!game||!game.Ready)&&Time.realtimeSinceStartup<deadline){game=Object.FindFirstObjectByType<MemoryGame>();yield return null;}
            Assert.IsTrue(game&&game.Ready,"the game starts");
            // The look is being chosen (ShapeOfMemoryBuilder.Preset): the environment artist's scene alone has none of ours.
            var look=game.Look;
            if(!look||!look.HasClouds)Assert.Ignore("this look ("+(look?look.name:"none")+") has none of our clouds: MemoryEnvironmentPlayTests covers it");
            var clouds=game.Clouds;Assert.NotNull(clouds);Assert.Greater(clouds.Count,40,"the scene has its clouds");
            Assert.IsTrue(clouds.Renderers.All(r=>!r.enabled),"hidden before the sky");
            game.FinishTitle();deadline=Time.realtimeSinceStartup+3;while(game.Book.Sealed&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;
            Assert.IsTrue(clouds.Renderers.All(r=>!r.enabled),"still hidden over the room");
            game.Sky.Show();
            bool fading=false;deadline=Time.realtimeSinceStartup+MemorySky.FadeSeconds+2;
            while(!clouds.Revealed&&Time.realtimeSinceStartup<deadline)
            {
                yield return null;
                float alpha=game.Sky.Alpha;
                if(alpha>.05f&&alpha<.95f&&clouds.Renderers.All(r=>r.enabled)&&Mathf.Abs(MemoryClouds.Hidden-(1-alpha))<.2f)fading=true;
            }
            Assert.IsTrue(fading,"they fade in with the sky");
            Assert.IsTrue(clouds.Revealed,"up once the sky is");
            yield return null;
            Assert.AreEqual(0,MemoryClouds.Hidden,"fully shown");
            var models=look.RuinModels.Where(m=>m).Select(m=>m.GetComponentInChildren<MeshRenderer>(true).sharedMaterial).ToArray();
            Assert.IsTrue(clouds.Renderers.All(r=>r.enabled&&(r.sharedMaterial==look.CloudMaterial||r.sharedMaterial==look.RuinMaterial||models.Contains(r.sharedMaterial))),"the look's materials throughout");
            var ruins=clouds.Renderers.Where(r=>r.name=="Ruin").ToArray();
            if(look.HasOurRuins||look.HasRuinModels)Assert.That(ruins.Length,Is.GreaterThan(8),"ruins among the clouds");
            // The environment artist's models take the environment scene's bake, at most Lean off upright when standing.
            if(look.HasRuinModels&&look.HasEnvironment)
            {
                var lit=ruins.Where(r=>models.Contains(r.sharedMaterial)).ToArray();
                Assert.IsNotEmpty(lit,"ruin models in the layout");
                foreach(var r in lit)
                {
                    Assert.GreaterOrEqual(r.lightmapIndex,0,r.transform.parent.name+" lit by the environment's bake");
                    Assert.That(Vector3.Angle(r.transform.up,game.Stage.up),Is.LessThanOrEqualTo(Mathf.Max(look.Ruins.Lean,MemoryClouds.FloatTilt)+1),r.transform.parent.name+" leans little");
                }
            }
            // A swelling sea: its tops rise and fall.
            if(look.CloudShape.SeaSwell>=2)
            {
                var tops=clouds.Renderers.Where(r=>r.transform.parent.name.StartsWith("Sea_")).Select(r=>r.bounds.max.y).ToArray();
                Assert.Greater(tops.Max()-tops.Min(),1.5f,"the sea swells");
            }
            // Nothing in the play space: whatever lies within KeepOut of the eyes stays under the floor.
            var eye=game.Stage.TransformPoint(new Vector3(MemoryClouds.Eye.x,0,MemoryClouds.Eye.y));float floor=game.Player.FloorHeight;
            foreach(var r in clouds.Renderers)
            {
                var b=r.bounds;var flat=new Vector2(Mathf.Max(0,Mathf.Abs(eye.x-b.center.x)-b.extents.x),Mathf.Max(0,Mathf.Abs(eye.z-b.center.z)-b.extents.z));
                if(flat.magnitude<MemoryClouds.KeepOut)Assert.That(b.max.y,Is.LessThan(floor+.05f),$"{r.transform.parent.name} {flat.magnitude:0.0} m from the eyes");
            }
            var dome=game.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="Sky dome");
            Assert.AreEqual(MemorySky.SkyQueue,dome.sharedMaterial.renderQueue,"the sky draws after the clouds");

            // The table-top's key light is the look's, and turns with the stage (a recentre) as the sky does.
            var lighting=game.Lighting;Assert.NotNull(lighting,"the look's lighting");Assert.NotNull(lighting.Key,"a key light");
            Assert.AreEqual(look.Lighting.KeyColor,lighting.Key.color,"its colour");
            var rest=game.Stage.rotation;
            game.Stage.rotation=Quaternion.Euler(0,90,0);yield return null;
            var want=look.Lighting.Rotation(90,look.GlowFacing)*Vector3.forward;
            Assert.Less(Vector3.Angle(want,lighting.Key.transform.forward),.1f,"the key turns with the stage");
            game.Stage.rotation=rest;
        }
    }
}
