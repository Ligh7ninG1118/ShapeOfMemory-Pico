using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;

namespace ShadowWorkshopIntegration.Tests
{
    public class SharedSnapTests
    {
        [Test] public void PuzzleGridRetainsFiveMillimetresAndFifteenDegrees()
        {
            var input=new Pose(new Vector3(.021f,.099f,-.032f),Quaternion.Euler(14,31,46));
            var actual=SnapGeometry.Grid(input,.005f,15);
            Assert.Less(Vector3.Distance(new Vector3(.02f,.10f,-.03f),actual.position),.00001f);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(15,30,45),actual.rotation),.06f);
        }
    }
}
