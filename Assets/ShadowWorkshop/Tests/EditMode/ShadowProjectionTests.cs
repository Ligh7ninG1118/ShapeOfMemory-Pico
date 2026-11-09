using NUnit.Framework;
using UnityEngine;
using CloudGarden.ShadowPuzzle;
using ShadowProjection = CloudGarden.ShadowPuzzle.ShadowProjection;

namespace CloudGarden.Tests
{
    public class ShadowProjectionTests
    {
        ShadowProjection projection;
        [SetUp] public void Setup(){projection=new ShadowProjection();}
        [TearDown] public void Teardown(){projection.Dispose();}
        [Test] public void TargetIsGeneratedFromReachableMeshes()
        {
            foreach(var pose in ShadowProjection.Solution)Assert.IsTrue(ShadowProjection.InWorkspace(pose));
            projection.Render(ShadowProjection.Solution,new[]{true,true,true});var score=projection.Score();Assert.AreEqual(1,score.iou);Assert.IsTrue(score.Complete);
        }
        [Test] public void MissingMastCannotPass()
        {
            projection.Render(ShadowProjection.Solution,new[]{true,true,false});var score=projection.Score();Assert.Less(score.mast,.70f);Assert.IsFalse(score.Complete);
        }
        [Test] public void HullAlonePassesTutorialOnly()
        {
            projection.Render(ShadowProjection.Solution,new[]{true,false,false});Assert.IsTrue(projection.Score().HullReady);Assert.IsFalse(projection.Score().Complete);
        }
        [Test] public void OversizedShadowCannotPass()
        {
            var poses=(ShadowPose[])ShadowProjection.Solution.Clone();for(int i=0;i<3;i++){var p=poses[i];p.position.z=-.16f;poses[i]=p;}
            projection.Render(poses,new[]{true,true,true});Assert.IsFalse(projection.Score().Complete);Assert.Greater(projection.Score().spill,.15f);
        }
        [Test] public void OutsideWorkspaceDoesNotScore()
        {
            var poses=(ShadowPose[])ShadowProjection.Solution.Clone();for(int i=0;i<3;i++)poses[i].position.x=1;
            projection.Render(poses,new[]{true,true,true});Assert.AreEqual(0,projection.Score().iou);
        }
        [Test] public void CloserToLampMakesLargerShadow()
        {
            var far=projection.Project(new Vector3(.05f,.32f,.15f));var near=projection.Project(new Vector3(.05f,.32f,-.1f));Assert.Greater(near.x-64,far.x-64);
        }
        [Test] public void SmallPoseChangesRemainSolvable()
        {
            var poses=(ShadowPose[])ShadowProjection.Solution.Clone();for(int i=0;i<3;i++){poses[i].position.x+=.004f;poses[i].rotation=Quaternion.Euler(0,4f,0);}
            projection.Render(poses,new[]{true,true,true});Assert.IsTrue(projection.Score().Complete);
        }
    }
}
