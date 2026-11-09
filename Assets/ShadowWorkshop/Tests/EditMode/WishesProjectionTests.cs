using NUnit.Framework;
using UnityEngine;
using CloudGarden.ShadowPuzzle;

namespace CloudGarden.Tests
{
    public sealed class WishesProjectionTests
    {
        WishesProjection p;
        [SetUp] public void Setup(){p=new WishesProjection();}
        [TearDown] public void Dispose(){p.Dispose();}
        [Test] public void ActualCarvedPiecesReproduceThreeCompleteShadows()
        {
            p.Render(p.Solution);foreach(var score in p.scores){Assert.AreEqual(1,score.iou);Assert.AreEqual(1,score.windowClear);}
            Assert.IsTrue(p.Complete(p.Solution));Assert.AreEqual(12,p.meshes.Length);
        }
        [Test] public void AllThreeSilhouettesAreDistinct()
        {
            for(int a=0;a<3;a++)for(int b=a+1;b<3;b++)
            {int overlap=0,union=0;for(int i=0;i<p.target[a].Length;i++){if(p.target[a][i]&&p.target[b][i])overlap++;if(p.target[a][i]||p.target[b][i])union++;}Assert.Less(overlap/(float)union,.80f,$"Views {a}/{b} too similar");}
        }
        [Test] public void LanternAndPortholeAreRealNegativeSpace()
        {
            int[] holes=new int[3];for(int v=0;v<3;v++)foreach(bool pixel in p.windows[v])if(pixel)holes[v]++;
            Assert.Greater(holes[0],8);Assert.AreEqual(0,holes[1]);Assert.Greater(holes[2],20);
        }
        [Test] public void FrontAloneDoesNotConstrainDepth()
        {
            var poses=(ShadowPose[])p.Solution.Clone();for(int i=0;i<poses.Length;i++)poses[i].position.z+=.06f;
            p.Render(poses);Assert.AreEqual(1,p.scores[0].iou);Assert.IsTrue(p.ReadyThrough(0));Assert.IsFalse(p.ReadyThrough(1));Assert.IsFalse(p.Complete(poses));
        }
        [Test] public void SecondStageRequiresFrontAndLeftTogether()
        {
            var poses=(ShadowPose[])p.Solution.Clone();for(int i=0;i<poses.Length;i++)poses[i].position+=new Vector3(.05f,0,.05f);
            p.Render(poses);Assert.IsTrue(p.scores[1].Ready);Assert.IsFalse(p.scores[0].Ready);Assert.IsFalse(p.ReadyThrough(1));
        }
        [Test] public void GenericGridRoundingRemainsSolvable()
        {
            var poses=(ShadowPose[])p.Solution.Clone();for(int i=0;i<poses.Length;i++){var v=poses[i].position;poses[i].position=new Vector3(Mathf.Round(v.x/.005f)*.005f,Mathf.Round(v.y/.005f)*.005f,Mathf.Round(v.z/.005f)*.005f);}
            p.Render(poses);Assert.IsTrue(p.Complete(poses));
        }
        [Test] public void EveryPieceMustJoinFinalAssembly()
        {
            for(int i=0;i<WishesProjection.Count;i++){var poses=(ShadowPose[])p.Solution.Clone();poses[i]=WishesProjection.Tray[i];p.Render(poses);Assert.IsFalse(p.Complete(poses),"Missing "+i);}
        }
        [Test] public void SideDirectionsAreFortyFiveDegreesFromFront()
        {
            Assert.AreEqual(45,Quaternion.Angle(WishesProjection.Views[0],WishesProjection.Views[1]),.001f);
            Assert.AreEqual(45,Quaternion.Angle(WishesProjection.Views[0],WishesProjection.Views[2]),.001f);
            Assert.Less((WishesProjection.Views[1]*Vector3.back).x,0);Assert.Greater((WishesProjection.Views[2]*Vector3.back).x,0);
        }
        [Test] public void CachedProjectionUpdatesOnMoveRotationAndTrayReturn()
        {
            var poses=(ShadowPose[])p.Solution.Clone();p.Render(poses);Assert.IsTrue(p.Complete(poses));
            poses[0].position+=new Vector3(.08f,0,.03f);poses[0].rotation=Quaternion.Euler(0,35,0);p.Render(poses);Assert.Less(p.scores[0].iou,1);
            poses[0]=p.Solution[0];p.Render(poses);Assert.AreEqual(1,p.scores[0].iou);
            for(int i=0;i<poses.Length;i++)poses[i]=WishesProjection.Tray[i];p.Render(poses);foreach(var score in p.scores)Assert.AreEqual(0,score.iou);
        }
        [Test] public void OpaqueBlockCannotFillLanternWindow()
        {
            var poses=(ShadowPose[])p.Solution.Clone();poses[0]=new ShadowPose(new Vector3(-.025f,.405f,0),Quaternion.identity);p.Render(poses);
            Assert.Less(p.scores[0].windowClear,.80f);Assert.IsFalse(p.scores[0].Ready);
        }
    }
}
