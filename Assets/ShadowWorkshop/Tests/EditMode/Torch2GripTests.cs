using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using UnityEngine;

namespace CloudGarden.Tests
{
    // Torch2Input.TwoHandTurn: the orb turned by two grips at once (playtest 2026-11-11).
    public sealed class Torch2GripTests
    {
        // Within about 0.16 degrees (Quaternion.Angle reads 0 below that).
        static void Same(Quaternion want,Quaternion got,string what)=>Assert.Greater(Mathf.Abs(Quaternion.Dot(want,got)),1-1e-6f,$"{what}: {want.eulerAngles} vs {got.eulerAngles}");
        static Vector3 Direction(System.Random random)
        {
            while(true){var v=new Vector3((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1);if(v.sqrMagnitude>.01f&&v.sqrMagnitude<=1)return v.normalized;}
        }

        [Test] public void NothingMovedNothingTurns()
        {
            var a=new Vector3(-.2f,.03f,.05f);var b=new Vector3(.1f,.15f,-.1f);
            Same(Quaternion.identity,Torch2Input.TwoHandTurn(a,b,a,b),"still");
        }
        // Both grips carried by one turn give that turn back, wherever they hold (short of straight across or side by side).
        [Test] public void ARigidTurnOfBothGripsComesBackExactly()
        {
            var random=new System.Random(11);int cases=0;
            for(int n=0;n<1000&&cases<300;n++)
            {
                var turn=Quaternion.AngleAxis((float)random.NextDouble()*25,Direction(random));
                var a=Direction(random)*.2f;var b=Direction(random)*.26f;float apart=Vector3.Angle(a,b);
                if(apart<25||apart>150)continue;cases++;
                Same(turn,Torch2Input.TwoHandTurn(a,b,turn*a,turn*b),$"case {n}, {apart:0} degrees apart");
            }
            Assert.AreEqual(300,cases);
        }
        // One hand held still: the orb turns about that hand's spoke, so the still grip stays where it is.
        [Test] public void AGripHeldStillIsThePivot()
        {
            var still=new Vector3(.1f,.15f,-.05f);var moving=new Vector3(-.18f,.02f,.06f);
            foreach(float degrees in new[]{5f,-12f,20f})
            {
                var about=Quaternion.AngleAxis(degrees,still);var got=Torch2Input.TwoHandTurn(moving,still,about*moving,still);
                Same(about,got,$"{degrees} degrees");Assert.Less((got*still-still).magnitude,1e-5f,"the still grip stays");
            }
        }
        // Grips on opposite sides turn it like a wheel, and can't spin it about the line between them.
        [Test] public void GripsAcrossTheOrbTurnItLikeAWheel()
        {
            var left=new Vector3(-.2f,0,0);var right=new Vector3(.2f,0,0);
            var wheel=Quaternion.AngleAxis(10,Vector3.forward);Same(wheel,Torch2Input.TwoHandTurn(left,right,wheel*left,wheel*right),"wheel");
            var tip=Quaternion.AngleAxis(-8,Vector3.up);Same(tip,Torch2Input.TwoHandTurn(left,right,tip*left,tip*right),"swing");
            var spin=Quaternion.AngleAxis(20,Vector3.right);Same(Quaternion.identity,Torch2Input.TwoHandTurn(left,right,spin*left,spin*right),"no spin about the hands' line");
        }
        // Grips side by side, both moved the same way: one hand at their middle.
        [Test] public void GripsSideBySideActAsOneHand()
        {
            var a=new Vector3(0,0,-.2f);var b=Quaternion.AngleAxis(1.5f,Vector3.up)*a;var lift=new Vector3(.01f,.05f,0);
            Same(Quaternion.FromToRotation(a+b,a+b+2*lift),Torch2Input.TwoHandTurn(a,b,a+lift,b+lift),"as one hand");
        }
    }
}
