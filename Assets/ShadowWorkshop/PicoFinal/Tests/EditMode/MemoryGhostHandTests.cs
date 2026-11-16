using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    public class MemoryGhostHandTests
    {
        // A table top 4 cm deep with its top at y 0.
        static readonly MemoryGhostHand.Solid Top=new MemoryGhostHand.Solid{Id=1,Centre=new Vector3(0,-.02f,0),Turn=Quaternion.identity,Half=new Vector3(.5f,.02f,.5f)};
        static Vector3[] HandAt(float y)
        {
            var j=new Vector3[MemoryHandSkin.Count];
            for(int i=0;i<j.Length;i++)j[i]=new Vector3(i*.004f-.05f,y,.1f);
            return j;
        }
        static MemoryGhostHand Ghost(params MemoryGhostHand.Solid[] solids)=>new MemoryGhostHand(list=>{list.AddRange(solids);return list.Count;});
        // The lowest joint's skin (the palm's radius) on the top.
        static float Lift(float y)=>MemoryGhostHand.PalmRadius-y;

        [Test] public void AHandPressedIntoTheTableStaysOnItsTop()
        {
            var g=Ghost(Top);
            Assert.AreEqual(Vector3.zero,g.Solve(1,HandAt(.10f)),"above it: drawn where it is");
            var off=g.Solve(1,HandAt(-.01f));
            Assert.AreEqual(Lift(-.01f),off.y,1e-4f,"pressed 1 cm in: held up on the top");
            Assert.AreEqual(0,off.x,1e-5f);Assert.AreEqual(0,off.z,1e-5f);
        }

        // Deeper than the top is thick, it is still held on the side it came in from (not pushed out underneath).
        [Test] public void PushedThroughTheThinTopItIsStillHeldOnTop()
        {
            var g=Ghost(Top);
            g.Solve(1,HandAt(.10f));g.Solve(1,HandAt(-.01f));g.Solve(1,HandAt(-.03f));
            var off=g.Solve(1,HandAt(-.07f));
            Assert.AreEqual(Lift(-.07f),off.y,1e-4f);
            Assert.Greater(MemoryGhostHand.Shown(off),.99f,"and the ghost shows where the real hand is");
        }

        // A seated player's hands resting under the table are no business of the top's.
        [Test] public void AHandUnderTheTableIsLeftAlone()
        {
            var g=Ghost(Top);
            Assert.AreEqual(Vector3.zero,g.Solve(0,HandAt(-.15f)));
            Assert.AreEqual(Vector3.zero,g.Solve(0,HandAt(-.10f)));
        }

        // Far past the surface (the player has moved on, or tracking jumped), the drawn hand goes back to the real one.
        [Test] public void FarPastTheSurfaceItLetsGo()
        {
            var g=Ghost(Top);
            g.Solve(1,HandAt(.10f));g.Solve(1,HandAt(-.01f));
            Assert.AreEqual(Vector3.zero,g.Solve(1,HandAt(-.20f)),"past GiveUp");
            Assert.AreEqual(Vector3.zero,g.Solve(1,HandAt(-.15f)),"and it doesn't grab again from underneath");
        }

        // A lamp at its limit is a sphere: the hand is held round it.
        [Test] public void ALampAtItsLimitHoldsTheHandRoundIt()
        {
            var lamp=new MemoryGhostHand.Solid{Id=10,Centre=new Vector3(0,1,0),Turn=Quaternion.identity,Radius=MemoryLampOrb.Radius};
            var joints=new Vector3[MemoryHandSkin.Count];
            for(int i=0;i<joints.Length;i++)joints[i]=new Vector3(0,1,-(MemoryLampOrb.Radius+MemoryGhostHand.FingerRadius-.01f));
            joints[0]=joints[1]=new Vector3(0,1,-.2f);   // the palm well clear
            var off=Ghost(lamp).Solve(1,joints);
            Assert.AreEqual(-.01f,off.z,1e-4f,"pushed back out the way it came");
        }

        // A tracked hand that drops out starts afresh: no remembered face.
        [Test] public void LosingTheHandForgetsWhereItCameIn()
        {
            var g=Ghost(Top);
            g.Solve(1,HandAt(.10f));g.Solve(1,HandAt(-.01f));
            g.Lost(1);
            Assert.AreEqual(Vector3.zero,g.Solve(1,HandAt(-.07f)),"back under the top after a drop-out: left alone");
        }
    }
}
