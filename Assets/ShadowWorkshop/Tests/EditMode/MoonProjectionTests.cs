using NUnit.Framework;
using UnityEngine;
using CloudGarden.ShadowPuzzle;

namespace CloudGarden.Tests
{
    public class MoonProjectionTests
    {
        [Test] public void SideLabelsMatchPhysicalLightDirections()
        {
            Assert.Less((MoonProjection.Views[1]*Vector3.back).x,0);
            Assert.Greater((MoonProjection.Views[2]*Vector3.back).x,0);
        }
        [Test] public void CanonicalSculptureMatchesAllViewsAndConnects()
        {
            var projection=new MoonProjection();projection.Render(MoonProjection.Solution);
            foreach(var score in projection.scores)Assert.AreEqual(1,score.iou);Assert.IsTrue(projection.AllReady(MoonProjection.Solution));
        }
        [Test] public void SameFrontWrongDepthCannotComplete()
        {
            var p=(ShadowPose[])MoonProjection.Solution.Clone();p[8].position.z=-.14f;var projection=new MoonProjection();projection.Render(p);
            Assert.AreEqual(1,projection.scores[0].iou);Assert.IsFalse(projection.AllReady(p));
        }
        [Test] public void EveryPieceMustBeUsed()
        {
            var projection=new MoonProjection();
            for(int i=0;i<MoonProjection.Count;i++){var p=(ShadowPose[])MoonProjection.Solution.Clone();p[i]=MoonProjection.Tray[i];projection.Render(p);Assert.IsFalse(projection.AllReady(p),"Missing "+i);}
        }
        [Test] public void SmallPhysicalPlacementErrorsAreAccepted()
        {
            var p=(ShadowPose[])MoonProjection.Solution.Clone();for(int i=0;i<p.Length;i++)p[i].position+=new Vector3(.003f,.002f,-.002f);
            var projection=new MoonProjection();projection.Render(p);Assert.IsTrue(projection.AllReady(p));
        }
        [Test] public void TrayCannotAccidentallyPass()
        {
            var projection=new MoonProjection();projection.Render(MoonProjection.Tray);foreach(var s in projection.scores)Assert.AreEqual(0,s.iou);Assert.IsFalse(projection.AllReady(MoonProjection.Tray));
        }
        [Test] public void OpenFingersReleaseImmediately()
        {
            Assert.IsTrue(ShadowPinch.IsClosed(.017f,false));Assert.IsTrue(ShadowPinch.IsClosed(.019f,true));
            Assert.IsFalse(ShadowPinch.IsClosed(.020f,true));Assert.IsFalse(ShadowPinch.IsClosed(.025f,true));Assert.IsFalse(ShadowPinch.IsClosed(.019f,false));
        }
    }
}
