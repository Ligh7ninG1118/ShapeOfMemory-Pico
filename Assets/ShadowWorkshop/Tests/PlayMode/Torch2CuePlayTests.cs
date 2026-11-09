using System.Collections;
using System.Linq;
using CloudGarden.Integration;
using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowWorkshopIntegration.Tests
{
    public class Torch2CuePlayTests
    {
        static IEnumerator Open(string id,System.Action<Torch2Workshop> found)
        {
            yield return SceneManager.LoadSceneAsync("ThreeWishes");yield return null;yield return null;
            var demo=Object.FindFirstObjectByType<ShadowDemo>();demo.LoadLevel(id);yield return null;
            Assert.NotNull(demo.Torch2,id);found(demo.Torch2);
        }
        // Held at the pose, then let go.
        static void Release(Torch2Workshop game,Quaternion rotation,int hand=9)
        {
            Assert.IsTrue(game.BeginHold(hand));game.Hold(hand,Torch2Projection.Home,rotation);game.EndHold(hand);
        }

        [UnityTest] public IEnumerator CloseReleaseSnapsIntoTheAnswerAndCompletes()
        {
            Torch2Workshop game=null;yield return Open("Birthday",g=>game=g);var p=game.Projection;
            var near=Quaternion.AngleAxis(8,Vector3.up)*p.Solution;
            p.Render(near,Torch2Projection.Home);p.Evaluate();Assert.IsTrue(p.Score.Ready||p.Score.warmth>=game.level.snapWarmth,"precondition: 8 degrees off is close");
            Release(game,near);Assert.IsTrue(game.Snapping);Assert.IsFalse(game.Busy);
            float deadline=Time.realtimeSinceStartup+3;
            while(!game.Completed&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(game.Completed);Assert.Less(Quaternion.Angle(game.Rotation,p.Solution),.01f,"turned exactly into the answer");
            yield return new WaitForSeconds(1);
            var cue=game.world.Cue;Assert.AreEqual(4,cue.Overall.Tier);Assert.AreEqual(4,cue.Overall.Shown,.01f);
            Assert.IsTrue(cue.Layers.All(l=>l.isPlaying));Assert.Greater(cue.Layers.Last().volume,0,"every layer has joined");
        }
        [UnityTest] public IEnumerator FarReleaseStaysAndAGrabStopsASnap()
        {
            Torch2Workshop game=null;yield return Open("BonVoyage",g=>game=g);var p=game.Projection;
            Release(game,Quaternion.AngleAxis(35,Vector3.right)*p.Solution);Assert.IsFalse(game.Snapping,"35 degrees is past the snap reach");
            yield return new WaitForSeconds(.6f);Assert.AreEqual(35,Quaternion.Angle(game.Rotation,p.Solution),.05f);
            Release(game,Quaternion.AngleAxis(8,Vector3.up)*p.Solution);Assert.IsTrue(game.Snapping);
            yield return null;Assert.IsTrue(game.BeginHold(8),"another hand grabs mid-snap");Assert.IsFalse(game.Snapping);
            var held=game.Rotation;yield return new WaitForSeconds(.6f);
            Assert.AreEqual(held,game.Rotation,"the hand wins");Assert.IsFalse(game.Completed);game.EndHold(8);
        }
        [UnityTest] public IEnumerator SlidesAndLayersFollowEachWallsTier()
        {
            Torch2Workshop game=null;yield return Open("Home",g=>game=g);var p=game.Projection;var cue=game.world.Cue;
            yield return new WaitForSeconds(1.5f);
            Assert.LessOrEqual(cue.Overall.Tier,1,"the start reads cold");Assert.AreEqual(2,cue.Walls.Length);
            var wall=game.GetComponentsInChildren<Renderer>().First(r=>r.name=="Shadow wall 0").sharedMaterial;
            Assert.AreEqual("ShadowWorkshop/Projected Slide",wall.shader.name);Assert.NotNull(wall.GetTexture("_Slide"));
            Torch2Cue.Look(cue.Walls[0].Shown,out _,out _,out float mix);Assert.AreEqual(mix,wall.GetFloat("_SlideMix"),1e-4f);
            Assert.AreEqual(4,cue.Layers.Length);Assert.AreEqual(0,cue.Layers[3].volume,"the last layer waits for tier 3");
            // Held at the answer: both walls pass, the slides come sharp and every layer joins, before letting go.
            Assert.IsTrue(game.BeginHold(9));game.Hold(9,Torch2Projection.Home,p.Solution);yield return new WaitForSeconds(2);
            Assert.AreEqual(4,cue.Walls[0].Tier);Assert.AreEqual(4,cue.Walls[1].Tier);Assert.AreEqual(4,cue.Overall.Shown,.01f);
            Assert.AreEqual(1,wall.GetFloat("_SlideMix"),1e-3f);Assert.AreEqual(0,wall.GetFloat("_SlideBlur"),1e-4f);Assert.Greater(cue.Layers[3].volume,0);
            game.EndHold(9);yield return new WaitForSeconds(1);Assert.IsTrue(game.Completed);
            game.ResetChallenge();Assert.AreEqual(0,cue.Overall.Tier,"RESET starts cold again");Assert.AreEqual(0,cue.Walls[0].Shown);
        }
    }
}
