using System.Linq;
using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using ShadowWorkshopIntegration.Editor;
using UnityEngine;

namespace CloudGarden.Tests
{
    public sealed class Torch2TransitTests
    {
        static readonly string[] Levels={"Birthday","BonVoyage","GoodMorning","Home"};
        static Torch2Transit Plan(Torch2Level level,float overlap=-1)=>new Torch2Transit(level,level.start,Torch2Projection.Home,level.transitFly,overlap<0?level.transitOverlap:overlap);

        // Before its turn each piece lies where the table says, after it sits in the orb at the start pose.
        [Test] public void EachPieceGoesFromTheTableToItsPlace([ValueSource(nameof(Levels))] string id)
        {
            var level=Torch2Level.Load(id);var transit=Plan(level);var table=Torch2Transit.Scatter(level);
            for(int i=0;i<level.pieces.Length;i++)
            {
                var first=transit.Evaluate(i,-1);var last=transit.Evaluate(i,transit.Length+1);var piece=level.pieces[i];
                Assert.Less(Vector3.Distance(first.position,table[i].position),1e-4f,piece.label);Assert.Less(Quaternion.Angle(first.rotation,table[i].rotation),.05f,piece.label);
                Assert.Less(Vector3.Distance(last.position,Torch2Projection.Home+level.start*piece.position),1e-4f,piece.label);
                Assert.Less(Quaternion.Angle(last.rotation,level.start*Quaternion.Euler(piece.euler)),.05f,piece.label);
            }
        }
        [Test] public void WithoutOverlapOnlyOneThingFliesAtATime([ValueSource(nameof(Levels))] string id)
        {
            var level=Torch2Level.Load(id);var transit=Plan(level,0);
            for(float t=0;t<transit.Length;t+=.05f)Assert.LessOrEqual(transit.movers.Count(m=>t>m.start&&t<m.end),1,$"{id} at {t:0.00} s");
        }
        // The real shapes (convex) never cut into each other deeper than they rest, on the way from the table to the orb.
        [Test] public void NothingCutsIntoAnythingOnTheWay([ValueSource(nameof(Levels))] string id)
        {
            var level=Torch2Level.Load(id);var transit=Plan(level);
            Assert.IsFalse(transit.movers.Any(m=>m.blocked),$"{id}: every piece has a clear line in");
            var clashes=Torch2GymTools.Clashes(level,transit);Assert.IsEmpty(clashes,string.Join("\n",clashes));
        }
        // Pieces that share a table group keep their places relative to each other all the way.
        [Test] public void AGroupTravelsAsOneThing()
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);var mesh=cube.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(cube);
            var level=ScriptableObject.CreateInstance<Torch2Level>();
            try
            {
                Torch2Piece Piece(Vector3 inOrb,Vector3 onTable,int group)=>new Torch2Piece{mesh=mesh,scale=Vector3.one*.05f,position=inOrb,onTable=true,tablePosition=onTable,tableEuler=new Vector3(0,30,0),tableGroup=group};
                // Two pieces 6 cm apart on the table and in the orb (the table turns them 30 degrees about up), and one alone.
                var offset=Quaternion.Euler(0,-30,0)*new Vector3(.06f,0,0);
                level.pieces=new[]{Piece(Vector3.zero,new Vector3(-.2f,.02f,-.3f),4),Piece(offset,new Vector3(-.2f,.02f,-.3f)+new Vector3(.06f,0,0),4),Piece(new Vector3(0,.12f,0),new Vector3(.25f,.02f,-.3f),9)};
                var transit=new Torch2Transit(level,Quaternion.identity,Torch2Projection.Home,.8f,0);
                Assert.AreEqual(2,transit.movers.Length);
                for(float t=0;t<=transit.Length;t+=.1f)
                {
                    var a=transit.Evaluate(0,t);var b=transit.Evaluate(1,t);
                    Assert.AreEqual(.06f,Vector3.Distance(a.position,b.position),1e-3f,$"at {t:0.0} s");Assert.Less(Quaternion.Angle(a.rotation,b.rotation),.05f);
                }
            }
            finally{Object.DestroyImmediate(level);}
        }
    }
}
