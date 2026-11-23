using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace PhysicalAssembly.Editor
{
    // A build config's RemovePermissions, kept out of the APK. Unity declares RECORD_AUDIO in unityLibrary's manifest
    // whenever any player code touches Microphone (the Meta XR Core and Voice SDKs do), and asks for it at first launch.
    // Gradle merges the launcher manifest last and with the highest priority, so a <uses-permission tools:node="remove"/>
    // there drops every copy, the AARs' included; unityLibrary's own declarations are deleted as well. Only BuildRunner
    // builds carry a list: Unity's Build window builds keep everything.
    public sealed class RemovedPermissions : IPostGenerateGradleAndroidProject
    {
        public const string AndroidNs="http://schemas.android.com/apk/res/android",ToolsNs="http://schemas.android.com/tools";
        public int callbackOrder=>200; // After StripPlatformSupplement; PicoGradle edits other nodes of the same file.
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var names=BuildRunner.Current?.removePermissions;
            if(names==null || names.Length==0) return;
            string library=Path.Combine(path,"src/main/AndroidManifest.xml"),launcher=Path.GetFullPath(Path.Combine(path,"../launcher/src/main/AndroidManifest.xml"));
            if(!File.Exists(launcher)) throw new BuildFailedException($"[RemovedPermissions] No launcher manifest at {launcher}: can't keep {string.Join(", ",names)} out of the APK.");
            int deleted=0;
            if(File.Exists(library)) { var doc=Load(library); deleted=Strip(doc,names); if(deleted>0) doc.Save(library); }
            var top=Load(launcher); MarkRemoved(top,names); top.Save(launcher);
            Debug.Log($"[RemovedPermissions] {string.Join(", ",names)}: {deleted} declaration(s) deleted from unityLibrary, tools:node=\"remove\" in {launcher}");
        }
        static XmlDocument Load(string file) { var doc=new XmlDocument { PreserveWhitespace=true }; doc.Load(file); return doc; }

        static readonly string[] Elements={ "uses-permission","uses-permission-sdk-23" };
        static string Name(XmlElement e)=>e.GetAttribute("name",AndroidNs);
        static IEnumerable<XmlElement> Declarations(XmlDocument doc)=>doc.DocumentElement==null?Enumerable.Empty<XmlElement>()
            :doc.DocumentElement.ChildNodes.OfType<XmlElement>().Where(e=>e.NamespaceURI=="" && Elements.Contains(e.LocalName));

        // Deletes the <manifest>-level declarations of these permissions; returns how many.
        public static int Strip(XmlDocument doc,IEnumerable<string> names)
        {
            var set=new HashSet<string>(names);
            var hits=Declarations(doc).Where(e=>set.Contains(Name(e))).ToArray();
            foreach(var e in hits) e.ParentNode.RemoveChild(e);
            return hits.Length;
        }

        // Replaces any declaration of these permissions with one <uses-permission android:name tools:node="remove"/>
        // each, declaring the tools namespace on <manifest> when it's missing. Running it twice changes nothing.
        public static void MarkRemoved(XmlDocument doc,IEnumerable<string> names)
        {
            var root=doc.DocumentElement??throw new ArgumentException("The manifest has no root element.");
            var list=names.Distinct().ToArray();
            bool Marked(XmlElement e)=>e.LocalName=="uses-permission" && e.GetAttribute("node",ToolsNs)=="remove";
            foreach(var e in Declarations(doc).Where(e=>list.Contains(Name(e)) && !Marked(e)).ToArray()) root.RemoveChild(e);
            var marked=new HashSet<string>(Declarations(doc).Where(Marked).Select(Name));
            if(root.GetPrefixOfNamespace(ToolsNs)=="") root.SetAttribute("xmlns:tools",ToolsNs);
            string prefix=root.GetPrefixOfNamespace(ToolsNs),android=root.GetPrefixOfNamespace(AndroidNs);
            XmlNode anchor=root.FirstChild;
            foreach(var permission in list.Where(p=>!marked.Contains(p)))
            {
                var e=doc.CreateElement("uses-permission");
                var a=doc.CreateAttribute(android==""?"android":android,"name",AndroidNs); a.Value=permission; e.Attributes.Append(a);
                var t=doc.CreateAttribute(prefix==""?"tools":prefix,"node",ToolsNs); t.Value="remove"; e.Attributes.Append(t);
                root.InsertBefore(doc.CreateWhitespace("\n  "),anchor);
                root.InsertBefore(e,anchor);
            }
        }
    }
}
