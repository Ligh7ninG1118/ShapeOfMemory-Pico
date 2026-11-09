using NUnit.Framework;
using PhysicalAssembly.Editor;
using UnityEditor;
using UnityEditor.Build;

namespace PhysicalAssembly.Tests
{
    public sealed class AndroidBuildValidationTests
    {
        [Test]
        public void AndroidBuildRejectsDesktopActiveTarget()
        {
            var error = Assert.Throws<BuildFailedException>(() => AndroidBuildValidation.ValidateTarget(BuildTarget.Android, BuildTarget.StandaloneWindows64));
            StringAssert.Contains("Switch the active build target to Android", error.Message);
        }

        [Test]
        public void AndroidBuildAcceptsAndroidActiveTarget()
            => Assert.DoesNotThrow(() => AndroidBuildValidation.ValidateTarget(BuildTarget.Android, BuildTarget.Android));

        [Test]
        public void DesktopBuildIsNotBlockedByAndroidGuard()
            => Assert.DoesNotThrow(() => AndroidBuildValidation.ValidateTarget(BuildTarget.StandaloneWindows64, BuildTarget.StandaloneWindows64));

        [Test]
        public void GuardRunsBeforeRenderPipelineBuildDataIsCollected()
            => Assert.Less(new AndroidBuildValidation().callbackOrder, int.MinValue + 50);
    }
}
