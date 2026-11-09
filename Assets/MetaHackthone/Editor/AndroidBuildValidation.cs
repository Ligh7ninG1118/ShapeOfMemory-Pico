using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PhysicalAssembly.Editor
{
    // Run before SRP CoreBuildData (int.MinValue + 50) caches the active platform.
    public sealed class AndroidBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MinValue;

        public static void ValidateTarget(BuildTarget requested, BuildTarget active)
        {
            if(requested == BuildTarget.Android && active != BuildTarget.Android)
                throw new BuildFailedException("Switch the active build target to Android and wait for importing/compilation before building. URP otherwise prepares shaders using the desktop rendering profile. For batch builds pass -buildTarget Android when launching Unity.");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateTarget(report.summary.platform, EditorUserBuildSettings.activeBuildTarget);
            if(report.summary.platform != BuildTarget.Android) return;
            QualitySettings.GetRenderPipelineAssetsForPlatform<UniversalRenderPipelineAsset>("Android", out var pipelines, out _);
            const string mobilePath = "Assets/Settings/Mobile_RPAsset.asset";
            if(!pipelines.Any(p => AssetDatabase.GetAssetPath(p) == mobilePath))
                throw new BuildFailedException("Android quality settings must include " + mobilePath);
            Debug.Log("ANDROID_RENDERING_PREFLIGHT_OK: " + string.Join(", ", pipelines.Select(AssetDatabase.GetAssetPath)));
        }
    }
}
