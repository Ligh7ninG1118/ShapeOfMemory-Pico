using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine.XR.OpenXR;
using Unity.XR.OpenXR.Features.PICOSupport;

namespace PhysicalAssembly.Editor
{
    // Meta's manifest processor runs even when its OpenXR feature is disabled.
    public sealed class PicoGradle : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => int.MaxValue;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var pico = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<PICOFeature>();
            if (pico == null || !pico.enabled) return;
            var manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            var xml = new XmlDocument(); xml.Load(manifest);
            foreach (var node in xml.SelectNodes("//*").Cast<XmlNode>().Reverse().ToArray())
            {
                var name = node.Attributes?["name", "http://schemas.android.com/apk/res/android"]?.Value ?? "";
                if (name.StartsWith("com.oculus.") || name.StartsWith("oculus.") || node.Prefix == "horizonos")
                    node.ParentNode.RemoveChild(node);
            }
            xml.Save(manifest);
            // Keep the PICO loader; OVRPlugin embeds a competing loader in its AAR.
            var gradle = Path.Combine(path, "build.gradle");
            File.WriteAllLines(gradle, File.ReadAllLines(gradle).Where(line => !line.Contains("implementation(name: 'OVRPlugin',")));
        }
    }
}
