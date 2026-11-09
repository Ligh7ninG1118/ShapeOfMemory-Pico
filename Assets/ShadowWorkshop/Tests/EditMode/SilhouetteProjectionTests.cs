using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CloudGarden.ShadowPuzzle;

namespace CloudGarden.Tests
{
    public sealed class SilhouetteProjectionTests
    {
        static readonly string[] Ids={"ALife","Italia","Nippon"};
        static SilhouetteProjection Load(string id){var level=SilhouetteLevel.Load(id);Assert.NotNull(level,"Missing level "+id+"; run Import everyday objects");return new SilhouetteProjection(level);}
        static ShadowPose[] Solution(SilhouetteProjection p)=>p.level.pieces.Select(x=>x.Solution).ToArray();
        static SilhouetteScore Score(SilhouetteProjection p,ShadowPose[] poses){p.Render(poses);return p.Score;}

        [TestCaseSource(nameof(Ids))] public void SolutionMatchesExactly(string id)
        {
            var p=Load(id);var s=Score(p,Solution(p));
            Assert.IsTrue(s.Ready);Assert.AreEqual(1,s.cover);Assert.AreEqual(0,s.spill);Assert.AreEqual(1,s.region);Assert.AreEqual(1,s.Match(p.level));
        }
        [TestCaseSource(nameof(Ids))] public void SolutionsSitOnTheWorkbenchGrid(string id)
        {
            var p=Load(id);var poses=Solution(p);
            for(int i=0;i<poses.Length;i++)
            {
                var snapped=PhysicalAssembly.SnapGeometry.Grid(new Pose(poses[i].position,poses[i].rotation),.005f,15);
                Assert.Less(Vector3.Distance(snapped.position,poses[i].position),.0001f,p.level.pieces[i].label);
                Assert.Less(Quaternion.Angle(snapped.rotation,poses[i].rotation),.05f,p.level.pieces[i].label);
                Assert.IsTrue(SilhouetteProjection.InWorkspace(poses[i]),p.level.pieces[i].label);
            }
        }
        [TestCaseSource(nameof(Ids))] public void TraysStartOutsideTheLight(string id)
        {
            var p=Load(id);var s=Score(p,p.level.pieces.Select(x=>x.Tray).ToArray());
            Assert.IsFalse(s.Ready);Assert.IsFalse(s.placed);Assert.AreEqual(0,s.cover);
        }
        [TestCaseSource(nameof(Ids))] public void EveryObjectIsNeeded(string id)
        {
            var p=Load(id);
            for(int i=0;i<p.Count;i++){var poses=Solution(p);poses[i]=p.level.pieces[i].Tray;Assert.IsFalse(Score(p,poses).Ready,"Passed without "+p.level.pieces[i].label);}
        }
        [TestCaseSource(nameof(Ids))] public void HandPlacementErrorsAreForgiven(string id)
        {
            // Off-grid by up to ~8 mm and 6 degrees: the eased tolerance (3 px = 13 mm) absorbs it.
            var p=Load(id);var poses=Solution(p);var random=new System.Random(11);
            for(int i=0;i<poses.Length;i++)
            {
                float R(float a)=>(float)(random.NextDouble()*2-1)*a;
                poses[i].position+=new Vector3(R(.006f),R(.006f),R(.03f));poses[i].rotation=Quaternion.Euler(R(6),R(6),R(6))*poses[i].rotation;
            }
            var s=Score(p,poses);Assert.IsTrue(s.Ready,$"cover={s.cover:0.00} region={s.region:0.00} spill={s.spill:0.00}");
        }
        [TestCaseSource(nameof(Ids))] public void DepthDoesNotMatterForParallelLight(string id)
        {
            var p=Load(id);var poses=Solution(p);for(int i=0;i<poses.Length;i++)poses[i].position.z=(i%2==0?-1:1)*.2f;
            Assert.IsTrue(Score(p,poses).Ready);
        }
        [TestCaseSource(nameof(Ids))] public void OneGridStepMistakesArePardoned(string id)
        {
            var p=Load(id);var poses=Solution(p);for(int i=0;i<poses.Length;i++)poses[i].position+=new Vector3(.005f,-.005f,0);
            Assert.IsTrue(Score(p,poses).Ready,"Every object one 5 mm step off");
            for(int i=0;i<p.Count;i++)
            {
                poses=Solution(p);poses[i].rotation=Quaternion.AngleAxis(15,Vector3.forward)*poses[i].rotation;
                var s=Score(p,poses);Assert.IsTrue(s.Ready,$"{p.level.pieces[i].label} one 15 degree step off: region={s.region:0.00} own spill={s.pieceSpill:0.00}");
            }
        }
        [TestCase("ALife","UMBRELLA"),TestCase("ALife","SKATEBOARD"),TestCase("ALife","LAPTOP"),TestCase("Italia","TOWER"),TestCase("Italia","PARMIGIANO"),TestCase("Italia","CHILI"),TestCase("Nippon","SHINKANSEN"),TestCase("Nippon","KOKESHI")]
        public void TurningADistinctiveObjectBreaksTheMatch(string id,string label)
        {
            var p=Load(id);var poses=Solution(p);int i=System.Array.FindIndex(p.level.pieces,x=>x.label==label);Assert.GreaterOrEqual(i,0);
            poses[i].rotation=Quaternion.AngleAxis(90,Vector3.forward)*poses[i].rotation;
            Assert.IsFalse(Score(p,poses).Ready,label);
        }
        [TestCaseSource(nameof(Ids))] public void ShiftedAssemblyFails(string id)
        {
            var p=Load(id);var poses=Solution(p);for(int i=0;i<poses.Length;i++)poses[i].position+=new Vector3(.035f,0,0);
            var s=Score(p,poses);Assert.IsFalse(s.Ready);Assert.Greater(s.spill,p.level.spillMax);
        }
        [Test] public void MapLevelsDrawTheirCoastline()
        {
            Assert.Greater(Load("Italia").outline.Count(x=>x),150);Assert.Greater(Load("Nippon").outline.Count(x=>x),150);Assert.AreEqual(0,Load("ALife").outline.Count(x=>x));
        }
    }
}
