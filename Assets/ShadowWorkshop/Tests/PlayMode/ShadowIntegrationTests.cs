using System.Collections;
using System.Linq;
using CloudGarden.Integration;
using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using Oculus.Interaction;
using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowWorkshopIntegration.Tests
{
    public class ShadowIntegrationTests
    {
        [UnityTest] public IEnumerator LevelChangesKeepOnePlayerAndWaitForHeldPieces()
        {
            yield return SceneManager.LoadSceneAsync("EverydayObjects"); yield return null; yield return null;
            var demo=Object.FindFirstObjectByType<ShadowDemo>(); Assert.AreEqual("ALife",demo.Level); Assert.NotNull(demo.Silhouette);
            var player=demo.Player;
            Assert.IsTrue(demo.Silhouette.BeginGrab(0,99));
            demo.LoadLevel("MoonCourier");
            Assert.AreEqual("ALife",demo.Level,"A held piece blocks a level change");
            demo.Silhouette.EndGrab(0,99,true);
            PressLevel(demo,"PREV LEVEL"); yield return null; Assert.NotNull(demo.Boat);
            PressLevel(demo,"MOON LEVEL"); yield return null; Assert.NotNull(demo.Moon);
            Capture(demo.Player.ViewCamera,"Logs/Shadow-Moon.png");
            PressLevel(demo,"BOAT LEVEL"); yield return null; Assert.NotNull(demo.Boat);
            Capture(demo.Player.ViewCamera,"Logs/Shadow-Boat.png");
            PressLevel(demo,"MOON LEVEL"); yield return null; Assert.NotNull(demo.Moon);
            PressLevel(demo,"THREE WISHES"); yield return null;
            Assert.AreSame(player,demo.Player);
            Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator ThreeWishesUsesCurrentCumulativeConstraintsAndSharedButtons()
        {
            yield return SceneManager.LoadSceneAsync("ThreeWishes"); yield return null; yield return null;
            var demo=Object.FindFirstObjectByType<ShadowDemo>(); var game=demo.Wishes;
            yield return null;
            Capture(demo.Player.ViewCamera,"Logs/Shadow-ThreeWishes.png");
            game.enabled=false;
            for(int i=0;i<game.poses.Length;i++) { game.poses[i]=game.Projection.Solution[i]; game.ApplyPose(i); }
            void Ready(){game.Projection.Render(game.poses);game.Evaluate(1.1f);}
            Ready(); Assert.IsTrue(game.CanReveal);
            var reveal=game.world.controls.Single(b=>b.name=="REVEAL").GetComponent<MetaMenuButton>();
            reveal.Poke.PublishPointerEvent(new PointerEvent(1001,PointerEventType.Select,default));
            Assert.AreEqual(1,game.Stage); Assert.IsFalse(game.ViewUnlocked(2));
            Ready(); Assert.IsTrue(game.CanReveal);
            var saved=(ShadowPose[])game.poses.Clone();
            for(int i=0;i<game.poses.Length;i++){game.poses[i]=WishesProjection.Tray[i];game.ApplyPose(i);}
            Assert.IsFalse(game.RevealNext(),"A stale green score must not reveal the next view");
            for(int i=0;i<game.poses.Length;i++){game.poses[i]=saved[i];game.ApplyPose(i);}
            Ready();Assert.IsTrue(game.RevealNext());
            Assert.AreEqual(2,game.Stage); Ready();
            yield return new WaitForSeconds(.8f);
            Capture(demo.Player.ViewCamera,"Logs/Shadow-ThreeViews.png");
            Assert.IsTrue(game.BeginGrab(0,99)); Assert.IsFalse(game.RevealNext());
            game.EndGrab(0,99); Ready(); Assert.IsTrue(game.RevealNext()); Assert.IsTrue(game.Completed);
            Capture(demo.Player.ViewCamera,"Logs/Shadow-Sculpture.png");
            game.EditAssembly();Assert.IsFalse(game.Completed);game.ResetChallenge();Assert.AreEqual(0,game.Stage);
            Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator TrackingLossBypassesSnapAndUndoRestoresGrabStart()
        {
            yield return SceneManager.LoadSceneAsync("ThreeWishes"); yield return null; yield return null;
            var game=Object.FindFirstObjectByType<WishesWorkshop>();var start=game.poses[0];
            var moved=new ShadowPose(new Vector3(.021f,.199f,.032f),Quaternion.Euler(14,31,46));
            Assert.IsTrue(game.BeginGrab(0,77));game.MoveGrab(0,77,moved);game.EndGrab(0,77,true);
            Assert.AreEqual(moved.position,game.poses[0].position);Assert.AreEqual(moved.rotation,game.poses[0].rotation);
            game.Undo();Assert.AreEqual(start.position,game.poses[0].position);
            Assert.IsTrue(game.BeginGrab(0,77));game.MoveGrab(0,77,moved);game.EndGrab(0,77);
            Assert.Less(Vector3.Distance(new Vector3(.02f,.20f,.03f),game.poses[0].position),.00001f);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(15,30,45),game.poses[0].rotation),.06f);
        }
        [UnityTest] public IEnumerator NextAndPrevCycleAllLevelsAndEverydayLevelsComplete()
        {
            yield return SceneManager.LoadSceneAsync("ThreeWishes"); yield return null; yield return null;
            var demo=Object.FindFirstObjectByType<ShadowDemo>();
            foreach(var expected in new[]{"MoonCourier","ShadowWorkshop","ALife","Italia","Nippon","Encore","BrightIdea","GoodBoy","Birthday","BonVoyage","GoodMorning","Home","ThreeWishes"})
            {
                PressLevel(demo,"NEXT LEVEL"); yield return null;
                Assert.AreEqual(expected,demo.Level);
                if(TorchLevel.Load(expected)!=null){yield return null;SolveTorch(demo,expected);continue;}
                if(Torch2Level.Load(expected)!=null){yield return null;SolveTorch2(demo,expected);continue;}
                Assert.IsNull(demo.Torch2);
                Assert.IsNull(demo.Torch);
                var game=demo.Silhouette;
                if(SilhouetteLevel.Load(expected)==null){Assert.IsNull(game);continue;}
                Assert.NotNull(game);yield return null;
                Capture(demo.Player.ViewCamera,"Logs/Shadow-"+expected+".png");
                game.enabled=false;
                for(int i=0;i<game.Count;i++){game.poses[i]=game.level.pieces[i].Solution;game.ApplyPose(i);}
                Assert.IsTrue(game.BeginGrab(0,99));Assert.IsTrue(demo.Busy);game.Projection.Render(game.poses);game.Evaluate(1);
                Assert.IsFalse(game.Completed,"A held object must block completion");
                game.EndGrab(0,99);game.Projection.Render(game.poses);game.Evaluate(1);Assert.IsTrue(game.Completed);
                game.EditAssembly();Assert.IsFalse(game.Completed);game.ResetChallenge();
                Assert.IsTrue(game.poses.Select((p,i)=>p.position==game.level.pieces[i].Tray.position).All(x=>x));
            }
            PressLevel(demo,"PREV LEVEL"); yield return null; Assert.AreEqual("Home",demo.Level);
            // A scene playlist (TorchLevels2) keeps PREV / NEXT inside its own set.
            demo.Playlist=new[]{"Birthday","BonVoyage","GoodMorning","Home"};demo.LoadLevel("BonVoyage");yield return null;
            PressLevel(demo,"NEXT LEVEL");yield return null;PressLevel(demo,"NEXT LEVEL");yield return null;PressLevel(demo,"NEXT LEVEL");yield return null;Assert.AreEqual("Birthday",demo.Level);
            PressLevel(demo,"PREV LEVEL");yield return null;Assert.AreEqual("Home",demo.Level);demo.Playlist=null;
            Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        }
        static void SolveTorch2(ShadowDemo demo,string id)
        {
            var game=demo.Torch2;Assert.NotNull(game);Assert.IsNull(demo.Torch);
            Capture(demo.Player.ViewCamera,"Logs/Shadow-"+id+".png");
            game.enabled=false;var input=game.input;var root=game.transform;
            // Recorded-hand playback at 50 Hz, wrist 8 cm behind and 3 cm below the pinch. Roll: pinch the front of the orb
            // and drag 30 degrees right over its surface, re-grab and drag 20 degrees up, re-grab on top and drag 25 degrees
            // sideways (a roll the old two-axis pile could not do); each time the pinched point stays under the fingers.
            // Carry: pinch the hub and move 5 cm without turning. A one-sample tracking jump is skipped, and so is the jump back.
            var centre=game.Position;var wrist=new Vector3(0,-.03f,-.08f);const float r=.28f;
            Pose Hand(Vector3 pinch)=>new Pose(root.TransformPoint(pinch)+root.rotation*wrist,root.rotation);
            void Sample(Vector3 pinch,float distance=.01f)=>input.SampleHand(0,true,Hand(pinch),root.TransformPoint(pinch),distance,.02f);
            void Grab(Vector3 pinch){for(int i=0;i<6;i++)Sample(pinch,.05f);Sample(pinch);Assert.IsTrue(game.Busy);}
            void Drag(Vector3 from,Vector3 axis,int degrees)
            {
                for(int i=1;i<=Mathf.Abs(degrees);i++)Sample(centre+Quaternion.AngleAxis(Mathf.Sign(degrees)*i,axis)*from);
                Sample(centre+Quaternion.AngleAxis(degrees,axis)*from,.05f);Assert.IsFalse(game.Busy);
            }
            var expected=game.Rotation;var front=new Vector3(0,0,-r);var top=new Vector3(0,r,0);
            Grab(centre+front);Assert.IsTrue(demo.Busy);Assert.AreEqual(Torch2Input.Grip.Roll,input.Grabbed);
            Drag(front,Vector3.up,-30);expected=Quaternion.AngleAxis(-30,Vector3.up)*expected;Assert.Less(Quaternion.Angle(expected,game.Rotation),.2f,"rolled right");
            Grab(centre+front);Drag(front,Vector3.right,20);expected=Quaternion.AngleAxis(20,Vector3.right)*expected;Assert.Less(Quaternion.Angle(expected,game.Rotation),.2f,"rolled up");
            Grab(centre+top);Drag(top,Vector3.forward,-25);expected=Quaternion.AngleAxis(-25,Vector3.forward)*expected;Assert.Less(Quaternion.Angle(expected,game.Rotation),.2f,"rolled sideways on top");
            Assert.Less(Vector3.Distance(centre,game.Position),1e-4f,"rolling never moves the orb");
            var hub=centre+new Vector3(.02f,0,0);var carried=new Vector3(.05f,0,0);
            Grab(hub);Assert.AreEqual(Torch2Input.Grip.Carry,input.Grabbed);
            for(int i=1;i<=20;i++)Sample(hub+carried*i/20);
            Sample(hub+carried+new Vector3(0,.2f,0));Sample(hub+carried);
            Assert.Less(Vector3.Distance(centre+carried,game.Position),1e-3f);Assert.Less(Quaternion.Angle(expected,game.Rotation),.01f,"carrying never turns the orb");
            Sample(hub+carried,.05f);Assert.IsFalse(game.Busy);
            // Solve: a held pile never completes; releasing it at the answer does.
            Assert.IsTrue(game.BeginHold(9));game.Hold(9,Torch2Projection.Home,game.Projection.Solution);
            game.Projection.Render(game.Rotation,game.Position);game.Projection.Evaluate();game.Evaluate(1);Assert.IsFalse(game.Completed);
            game.EndHold(9);game.Evaluate(1);Assert.IsTrue(game.Completed,id);
            game.ResetChallenge();Assert.IsFalse(game.Completed);Assert.Less(Quaternion.Angle(game.Projection.Start,game.Rotation),.01f);
        }
        static void SolveTorch(ShadowDemo demo,string id)
        {
            var torch=demo.Torch;Assert.NotNull(torch);Assert.IsNull(demo.Silhouette);
            Capture(demo.Player.ViewCamera,"Logs/Shadow-"+id+".png");
            torch.enabled=false;var input=torch.input;int hand=torch.TurnHand;float start=torch.Yaw;
            // Recorded-hand playback: open, pinch on the rim, carry the pinch 30 degrees round the axis, open.
            Vector3 Rim(float degrees)=>torch.transform.TransformPoint(TorchProjection.Axis+new Vector3(Mathf.Sin(degrees*Mathf.Deg2Rad)*.2f,.2f,Mathf.Cos(degrees*Mathf.Deg2Rad)*.2f));
            for(int i=0;i<6;i++)input.SampleTurn(hand,true,Rim(0),.05f,.02f);
            input.SampleTurn(hand,true,Rim(0),.01f,.02f);Assert.IsTrue(torch.Busy);Assert.IsTrue(demo.Busy);
            input.SampleTurn(hand,true,Rim(30),.01f,.02f);Assert.AreEqual(Mathf.Repeat(start+30,360),torch.Yaw,.01f);
            input.SampleTurn(hand,true,Rim(30),.05f,.02f);Assert.IsFalse(torch.Busy);
            // Desktop orbit keeps the beam on the pile; the lens can never sit inside it.
            torch.Orbit(30,10,.5f);Assert.AreEqual(.5f,(torch.Light-TorchProjection.Center).magnitude,1e-4f);
            Assert.Less(Vector3.Angle(torch.Aim,TorchProjection.Center-torch.Light),.01f);
            torch.SetLight(TorchProjection.Center,Vector3.forward);Assert.AreEqual(TorchWorkshop.MinLightDistance,(torch.Light-TorchProjection.Center).magnitude,1e-4f);
            Assert.IsTrue(torch.BeginTurn(9));torch.Turn(9,torch.level.solutionYaw);torch.EndTurn(9);
            var light=torch.level.solutionLight;torch.SetLight(light,TorchProjection.Center-light);
            Assert.IsTrue(torch.BeginTurn(9));torch.Projection.Render(torch.Yaw,torch.Light,torch.Aim);torch.Projection.Evaluate();torch.Evaluate(1);
            Assert.IsFalse(torch.Completed,"Turning the table must block completion");torch.EndTurn(9);
            torch.Evaluate(1);Assert.IsTrue(torch.Completed,id);
            torch.ResetChallenge();Assert.IsFalse(torch.Completed);Assert.AreEqual(torch.level.startYaw,torch.Yaw,.001f);
        }
        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1920,1080,24); var previous=camera.targetTexture; var active=RenderTexture.active;
            var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();
                System.IO.Directory.CreateDirectory("Logs");System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally { camera.targetTexture=previous;RenderTexture.active=active;Object.Destroy(pixels);Object.Destroy(target); }
        }
        static void PressLevel(ShadowDemo demo,string name)
        {
            Physics.SyncTransforms();
            var control=demo.Content.GetComponentsInChildren<ShadowControl>().Single(b=>b.name==name);
            var target=control.transform.TransformPoint(new Vector3(0,.51f,0));
            var ray=new Ray(demo.Player.ViewCamera.transform.position,target-demo.Player.ViewCamera.transform.position);
            Assert.IsTrue(Physics.Raycast(ray,out var hit,10));
            Assert.AreSame(control,hit.collider.GetComponent<ShadowControl>(),"Level button must not be hidden behind a piece or table");
            control.GetComponent<MetaMenuButton>().Ray.PublishPointerEvent(new PointerEvent(1122,PointerEventType.Select,new Pose(target,Quaternion.identity)));
        }
    }
}
