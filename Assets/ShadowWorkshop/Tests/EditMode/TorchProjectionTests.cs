using NUnit.Framework;
using UnityEngine;
using CloudGarden.ShadowPuzzle;

namespace CloudGarden.Tests
{
    public sealed class TorchProjectionTests
    {
        static readonly string[] Ids={"Encore","BrightIdea","GoodBoy"};
        static TorchProjection Load(string id){var level=TorchLevel.Load(id);Assert.NotNull(level,"Missing torch level "+id+"; run Import torch levels");return new TorchProjection(level);}
        static TorchScore At(TorchProjection p,float yaw,Vector3 light,Vector3? aim=null)
        {p.Render(yaw,light,aim??TorchProjection.Center-light);p.Evaluate();return p.Score;}
        static Vector3 Around(Vector3 light,float azimuth,float elevation,float scale=1)
        {
            // Same orbit model as the desktop controls, relative to the solution light.
            var v=light-TorchProjection.Center;float r=v.magnitude*scale,a=Mathf.Atan2(v.x,-v.z)+azimuth*Mathf.Deg2Rad,e=Mathf.Asin(v.y/v.magnitude)+elevation*Mathf.Deg2Rad;
            return TorchProjection.Center+r*new Vector3(Mathf.Sin(a)*Mathf.Cos(e),Mathf.Sin(e),-Mathf.Cos(a)*Mathf.Cos(e));
        }

        [TestCaseSource(nameof(Ids))] public void SolutionMatchesExactly(string id)
        {
            var p=Load(id);var s=At(p,p.level.solutionYaw,p.level.solutionLight);
            Assert.IsTrue(s.Ready);Assert.AreEqual(1,s.iou,1e-6);Assert.AreEqual(1,s.warmth);
        }
        [TestCaseSource(nameof(Ids))] public void StartingPoseDoesNotMatch(string id)
        {
            var p=Load(id);var light=TorchProjection.Center+.46f*new Vector3(Mathf.Sin(25*Mathf.Deg2Rad)*Mathf.Cos(-12*Mathf.Deg2Rad),Mathf.Sin(-12*Mathf.Deg2Rad),-Mathf.Cos(25*Mathf.Deg2Rad)*Mathf.Cos(-12*Mathf.Deg2Rad));
            var s=At(p,p.level.startYaw,light);Assert.IsTrue(s.visible);Assert.IsFalse(s.Ready);Assert.Less(s.warmth,.5f);
        }
        [TestCaseSource(nameof(Ids))] public void HandHeldErrorsAreForgiven(string id)
        {
            var p=Load(id);var L=p.level.solutionLight;float y=p.level.solutionYaw;
            Assert.IsTrue(At(p,y+5,L).Ready,"turntable +5");Assert.IsTrue(At(p,y-5,L).Ready,"turntable -5");
            Assert.IsTrue(At(p,y,Around(L,0,6)).Ready,"light 6 degrees higher");Assert.IsTrue(At(p,y,Around(L,0,-6)).Ready,"light 6 degrees lower");
            Assert.IsTrue(At(p,y,L,TorchProjection.Center-L+new Vector3(.04f,.03f,0)).Ready,"beam not centred on the pile");
        }
        [TestCaseSource(nameof(Ids))] public void DistanceAndWallPositionDoNotMatter(string id)
        {
            var p=Load(id);float y=p.level.solutionYaw;var L=p.level.solutionLight;
            Assert.IsTrue(At(p,y,Around(L,0,0,.8f)).Ready,"closer");Assert.IsTrue(At(p,y,Around(L,0,0,1.25f)).Ready,"further");
        }
        [TestCaseSource(nameof(Ids))] public void OtherAnglesNeverMatch(string id)
        {
            var p=Load(id);var L=p.level.solutionLight;
            for(int yaw=45;yaw<=315;yaw+=15)foreach(float e in new[]{-15f,0,20})
                Assert.IsFalse(At(p,p.level.solutionYaw+yaw,Around(L,0,e)).Ready,$"yaw +{yaw} elevation {e}");
        }
        [TestCaseSource(nameof(Ids))] public void WarmthGrowsTowardTheAnswer(string id)
        {
            // Some pictures already pass at +12 degrees, so compare two misses on either side of the window's reach.
            var p=Load(id);var L=p.level.solutionLight;float far=At(p,p.level.solutionYaw+120,L).warmth,close=At(p,p.level.solutionYaw+20,L).warmth;
            Assert.Less(far,close);Assert.Less(far,.3f);
        }
        [TestCaseSource(nameof(Ids))] public void BeamAwayFromThePileShowsNothing(string id)
        {
            var p=Load(id);var L=p.level.solutionLight;var s=At(p,p.level.solutionYaw,L,L-TorchProjection.Center);
            Assert.IsFalse(s.visible);Assert.IsFalse(s.Ready);
        }
        [TestCaseSource(nameof(Ids))] public void PileFitsTheTurntable(string id)
        {
            foreach(var piece in TorchLevel.Load(id).pieces)
            {
                var r=new Vector2(piece.position.x-TorchProjection.Axis.x,piece.position.z-TorchProjection.Axis.z).magnitude;
                Assert.LessOrEqual(r,.19f,piece.label);Assert.That(piece.position.y,Is.InRange(.12f,.52f),piece.label);
            }
        }
        [Test] public void UnchangedPoseSkipsTheRedraw()
        {
            var p=Load("Encore");var L=p.level.solutionLight;
            Assert.IsTrue(p.Render(10,L,TorchProjection.Center-L));Assert.IsFalse(p.Render(10,L,TorchProjection.Center-L));Assert.IsTrue(p.Render(11,L,TorchProjection.Center-L));
        }
    }
}
