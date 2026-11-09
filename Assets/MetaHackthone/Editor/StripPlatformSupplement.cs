using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace PhysicalAssembly.Editor
{
    // The Meta XR Platform SDK (com.meta.xr.sdk.platform, Editor/ModifyAndroidManifest.cs, callbackOrder 1) always adds
    // <horizonos:uses-horizonos-supplement name="horizonos-supplement-hzplatformclientcore"/>. Horizon OS builds without that
    // supplement (Quest 2 on v74) reject the APK with INSTALL_FAILED_MISSING_SHARED_LIBRARY. Nothing here calls Oculus.Platform,
    // so the requirement is dropped. Delete this hook before using Platform SDK features: AndroidClient.Initialize needs it.
    public sealed class StripPlatformSupplement : IPostGenerateGradleAndroidProject
    {
        public const string Supplement="horizonos-supplement-hzplatformclientcore";
        const string HorizonNs="http://schemas.horizonos/sdk";
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifest=Path.Combine(path,"src/main/AndroidManifest.xml");
            if(!File.Exists(manifest)) { Debug.LogWarning($"[StripPlatformSupplement] No manifest at {manifest}"); return; }
            var doc=new XmlDocument(); doc.Load(manifest);
            var removed=Strip(doc); if(removed>0) doc.Save(manifest);
            Debug.Log($"[StripPlatformSupplement] Removed {removed} {Supplement} requirement(s) from {manifest}");
        }
        public static int Strip(XmlDocument doc)
        {
            var root=doc.DocumentElement; if(root==null) return 0;
            var hits=new List<XmlElement>();
            foreach(XmlNode node in root.ChildNodes) if(node is XmlElement e&&e.LocalName=="uses-horizonos-supplement"&&e.NamespaceURI==HorizonNs&&Names(e)) hits.Add(e);
            foreach(var e in hits) root.RemoveChild(e);
            return hits.Count;
        }
        static bool Names(XmlElement e) { foreach(XmlAttribute a in e.Attributes) if(a.LocalName=="name"&&a.Value==Supplement) return true; return false; }
    }
}
