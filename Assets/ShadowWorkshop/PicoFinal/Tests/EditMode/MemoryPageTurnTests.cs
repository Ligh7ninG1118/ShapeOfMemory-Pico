using System;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    // The book's turning page: how it moves (PageTurn) and the shape it bends into (PageBend).
    public class MemoryPageTurnTests
    {
        const float Frame=1/72f;
        // Runs a turn at frame rate; while held, the hand puts the corner at hand(seconds). Returns the seconds it took.
        static float Run(PageTurn turn,float seconds,Func<float,float> hand=null,Action<PageTurn> each=null)
        {
            float time=0;
            while(time<seconds&&!turn.Landed)
            {
                if(hand!=null&&turn.Held)turn.Hold(hand(time));
                turn.Advance(Frame);time+=Frame;
                Assert.That(turn.Root,Is.InRange(0f,1f),"the root stays in the book");Assert.That(turn.Tip,Is.InRange(0f,1f),"the edge stays in the book");
                each?.Invoke(turn);
            }
            return time;
        }
        // Draws the corner to `to` (progress) over `seconds`, then lets go.
        static PageTurn Drag(float to,float seconds,float hold=0)
        {
            var turn=new PageTurn();turn.Hold(0);Run(turn,seconds+hold,t=>to*Mathf.Clamp01(t/seconds));
            turn.Release();Run(turn,4);Assert.IsTrue(turn.Landed,"it settles");return turn;
        }

        [Test] public void ATapLiftsTheCornerThenTheEdgeTrailsAndLandsLast()
        {
            var turn=new PageTurn();turn.Auto();float lead=0,trail=0,rootLanded=-1,time=0;
            float seconds=Run(turn,3,null,t=>{time+=Frame;lead=Mathf.Max(lead,t.Tip-t.Root);trail=Mathf.Min(trail,t.Tip-t.Root);if(rootLanded<0&&t.Root>=1)rootLanded=time;});
            Assert.IsTrue(turn.Landed);Assert.AreEqual(1,turn.Goal);Assert.AreEqual(1f,turn.Root);Assert.AreEqual(1f,turn.Tip);
            Assert.That(seconds,Is.InRange(.6f,1.3f),"about as long as the old turn");Assert.AreEqual(PageTurn.AutoSeconds,seconds,.05f);
            Assert.Greater(lead,.1f,"the corner goes first");Assert.Less(trail,-.05f,"then the edge trails a little");
            Assert.That(rootLanded,Is.GreaterThan(0).And.LessThan(seconds-.05f),"the spine side lands before the edge settles");
        }
        [Test] public void LetGoTurnsByHowFarAndHowFastItWasGoing()
        {
            Assert.AreEqual(0,Drag(.25f,.5f,.3f).Goal,"a short slow draw falls back");
            Assert.AreEqual(0,Drag(.4f,.5f,.3f).Goal,"held still under halfway falls back");
            Assert.AreEqual(1,Drag(.55f,.5f,.3f).Goal,"held still past halfway turns");
            Assert.AreEqual(1,Drag(1,.8f,.5f).Goal,"drawn all the way turns");
            Assert.AreEqual(1,Drag(.3f,.1f).Goal,"a quick flick turns");
            var back=Drag(.25f,.5f,.3f);Assert.AreEqual(0f,back.Root);Assert.AreEqual(0f,back.Tip);
        }
        [Test] public void HeldTheCornerLeadsAndDragsThePageAfterIt()
        {
            var turn=new PageTurn();turn.Hold(0);Run(turn,.6f,t=>.15f);
            Assert.AreEqual(.15f,turn.Tip,.01f,"the corner is where the hand is");Assert.AreEqual(0f,turn.Root,.01f,"a little bend leaves the spine side down");
            Run(turn,1,t=>1);Assert.AreEqual(1f,turn.Tip,.01f);Assert.AreEqual(1-PageTurn.MaxBend,turn.Root,.02f,"drawn across, the page follows the corner");
            Assert.IsFalse(turn.Landed,"held, it never lands by itself");
        }
        [Test] public void AHandSweepingFastSwingsThePageRatherThanCurlingIt()
        {
            var turn=new PageTurn();turn.Hold(0);float most=0;
            Run(turn,.5f,t=>Mathf.PingPong(t*8,1),t=>most=Mathf.Max(most,Mathf.Abs(t.Tip-t.Root)));
            Assert.LessOrEqual(most,PageTurn.BendLimit+1e-4f,"it never bends past the limit, either way");Assert.Greater(most,PageTurn.MaxBend,"but it does bend more on the move");
        }
        [Test] public void SamplesRunFromThePageItLeavesToLanded()
        {
            PageTurn.Sample(0,out float root,out float tip);Assert.AreEqual(0f,root);Assert.AreEqual(0f,tip);
            PageTurn.Sample(1,out root,out tip);Assert.AreEqual(1f,root);Assert.AreEqual(1f,tip);
            PageTurn.Sample(.5f,out root,out tip);Assert.That(root,Is.InRange(.05f,.95f));
        }

        static readonly Vector3 Corner=new Vector3(0,0,.8f),Along=new Vector3(1,0,-1).normalized,Across=new Vector3(-1,0,-1).normalized;
        const float W=1.1264f,H=1.056f,Lift=.07f;
        // The standing book: the page it leaves along Along, facing Across, opening 90°.
        static Vector3[] Bend(float root,float tip,out Vector3[] normals)
        {
            var positions=new Vector3[PageBend.Vertices];normals=new Vector3[PageBend.Vertices];
            PageBend.Shape(root,tip,Corner,Along,Across,Vector3.up,Mathf.PI*.5f,W,H,Lift,0,positions,normals,new Color32[PageBend.Vertices],new Vector3[PageBend.Rows+1]);
            return positions;
        }
        // The flat book: pages along x, facing up, opening 180°, held by row grab.
        static Vector3[] BendFlat(float root,float tip,float grab,out Vector3[] edge)
        {
            var positions=new Vector3[PageBend.Vertices];edge=new Vector3[PageBend.Rows+1];
            PageBend.Shape(root,tip,Vector3.zero,Vector3.right,Vector3.up,Vector3.forward,Mathf.PI,W,H,0,grab,positions,new Vector3[PageBend.Vertices],null,edge);
            return positions;
        }
        [Test] public void LyingFlatTheSheetNeverDipsUnderThePagesAndLandsOnTheOther()
        {
            foreach(var (root,tip) in new[]{(0f,0f),(0f,.4f),(.1f,.7f),(.4f,.6f),(.5f,.5f),(.6f,.3f),(.7f,1f),(1f,1f)})
                foreach(var p in BendFlat(root,tip,.5f,out _))Assert.GreaterOrEqual(p.y,-1e-4f,$"({root},{tip}) stays above the book");
            var end=BendFlat(1,1,0,out var edge);Assert.AreEqual(-W,edge[0].x,1e-3f,"landed on the left page");
            foreach(var p in end)Assert.AreEqual(PageBend.Rest,p.y,1e-4f);
        }
        [Test] public void TheHeldEdgeGoesWhereTheHandIsRoundTheSpine()
        {
            foreach(float root in new[]{0f,.2f,.5f})foreach(float degrees in new[]{30f,90f,150f})
            {
                float angle=degrees*Mathf.Deg2Rad;if(angle<root*Mathf.PI)continue;
                float tip=PageBend.TipFor(angle,root,Mathf.PI,.3f);var e=PageBend.Edge(root,tip,Mathf.PI,.3f);
                if(tip<.999f)Assert.AreEqual(degrees,Mathf.Atan2(e.y,e.x)*Mathf.Rad2Deg,.5f,$"root {root}: the held edge at {degrees}°");
                BendFlat(root,tip,.3f,out var edge);var held=edge[Mathf.RoundToInt(.3f*PageBend.Rows)];
                Assert.AreEqual(degrees,Mathf.Atan2(held.y,held.x)*Mathf.Rad2Deg,tip<.999f?2.5f:180,"the sheet's own edge row agrees");
            }
        }
        [Test] public void EveryRowKeepsThePagesWidthAndTheSheetStaysInsideTheBook()
        {
            foreach(var (root,tip) in new[]{(0f,0f),(0f,.3f),(0f,1f),(.2f,.6f),(.5f,.4f),(.6f,1f),(.9f,.7f),(1f,1f)})
            {
                var positions=Bend(root,tip,out var normals);
                for(int j=0;j<=PageBend.Rows;j++)
                {
                    float length=0;
                    for(int i=1;i<=PageBend.Columns;i++){var d=positions[j*(PageBend.Columns+1)+i]-positions[j*(PageBend.Columns+1)+i-1];d.y=0;length+=d.magnitude;}
                    Assert.AreEqual(W,length,.002f,$"row {j} at ({root},{tip}) keeps its width");
                }
                foreach(var p in positions)
                {
                    Assert.GreaterOrEqual(Vector3.Dot(p-Corner,Along),-1e-4f,$"({root},{tip}) stays in front of the far page");
                    Assert.GreaterOrEqual(Vector3.Dot(p-Corner,Across),-1e-4f,$"({root},{tip}) stays in front of the near page");
                }
                foreach(var n in normals)Assert.AreEqual(1f,n.magnitude,1e-3f);
            }
        }
        [Test] public void AtRestTheSheetLiesOnThePageItLeavesAndLandedOnTheOther()
        {
            var start=Bend(0,0,out var normals);
            for(int i=0;i<start.Length;i++){Assert.AreEqual(PageBend.Rest,Vector3.Dot(start[i]-Corner,Across),1e-4f);Assert.Greater(Vector3.Dot(normals[i],Across),.999f,"its front faces the reader");}
            Assert.AreEqual(W,Vector3.Dot(start[PageBend.Columns]-Corner,Along),1e-3f);
            var end=Bend(1,1,out normals);
            for(int i=0;i<end.Length;i++){Assert.AreEqual(PageBend.Rest,Vector3.Dot(end[i]-Corner,Along),1e-4f);Assert.Less(Vector3.Dot(normals[i],Along),-.999f,"its back faces the reader");}
            Assert.AreEqual(H,start[PageBend.Vertices-1].y,1e-4f,"no lift at rest");
        }
    }
}
