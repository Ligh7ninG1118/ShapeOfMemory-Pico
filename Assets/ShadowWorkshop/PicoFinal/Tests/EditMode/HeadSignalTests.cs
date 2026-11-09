using CloudGarden.Integration;
using NUnit.Framework;

namespace ShapeOfMemory.Tests
{
    public class HeadSignalTests
    {
        // A runtime that offers user presence and never sets it must not hold the hands back.
        [Test] public void ASignalThatNeverReadsTrueAllowsHands()
        {
            var signal=new HeadSignal();
            for(int i=0;i<10;i++)Assert.IsTrue(signal.Allows(true,false));
            Assert.IsFalse(signal.Seen);
        }
        // Once the runtime has shown it works, false means false: the headset came off or tracking was lost.
        [Test] public void ASignalThatHasReadTrueGatesHands()
        {
            var signal=new HeadSignal();
            Assert.IsTrue(signal.Allows(true,true));
            Assert.IsFalse(signal.Allows(true,false));
            Assert.IsTrue(signal.Allows(true,true));
        }
        [Test] public void AnUnreadableSignalAllowsHands()
        {
            var signal=new HeadSignal();
            Assert.IsTrue(signal.Allows(false,false));
            signal.Allows(true,true);
            Assert.IsTrue(signal.Allows(false,false));
        }
    }
}
