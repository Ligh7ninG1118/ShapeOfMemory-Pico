using System.Xml;
using NUnit.Framework;
using PhysicalAssembly.Editor;

namespace PhysicalAssembly.Tests
{
    public sealed class RemovedPermissionsTests
    {
        const string Mic="android.permission.RECORD_AUDIO";
        const string Library=@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"">
  <uses-permission android:name=""android.permission.INTERNET"" />
  <uses-permission android:name=""android.permission.RECORD_AUDIO"" />
  <uses-permission-sdk-23 android:name=""android.permission.RECORD_AUDIO"" />
  <application><meta-data android:name=""android.permission.RECORD_AUDIO"" /></application>
</manifest>";
        // A launcher manifest without the tools namespace.
        const string Launcher=@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:installLocation=""preferExternal"">
  <application android:label=""@string/app_name"" android:icon=""@mipmap/app_icon"" />
</manifest>";
        static XmlDocument Load(string xml) { var doc=new XmlDocument { PreserveWhitespace=true }; doc.LoadXml(xml); return doc; }
        static XmlNamespaceManager Ns(XmlDocument doc)
        {
            var ns=new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("a",RemovedPermissions.AndroidNs); ns.AddNamespace("t",RemovedPermissions.ToolsNs);
            return ns;
        }
        static int Count(XmlDocument doc,string xpath)=>doc.SelectNodes(xpath,Ns(doc)).Count;

        [Test] public void StripDeletesOnlyManifestLevelDeclarationsOfTheNamedPermissions()
        {
            var doc=Load(Library);
            Assert.AreEqual(2,RemovedPermissions.Strip(doc,new[] { Mic }));
            Assert.AreEqual(0,Count(doc,$"/manifest/*[@a:name='{Mic}']"));
            Assert.AreEqual(1,Count(doc,"/manifest/uses-permission[@a:name='android.permission.INTERNET']"));
            Assert.AreEqual(1,Count(doc,$"/manifest/application/meta-data[@a:name='{Mic}']"),"only <manifest> children are declarations");
            Assert.AreEqual(0,RemovedPermissions.Strip(doc,new[] { Mic }));
        }

        [Test] public void MarkRemovedAddsOneRemoveMarkerAndTheToolsNamespace()
        {
            var doc=Load(Launcher);
            RemovedPermissions.MarkRemoved(doc,new[] { Mic,Mic });
            Assert.AreEqual(1,Count(doc,$"/manifest/uses-permission[@a:name='{Mic}' and @t:node='remove']"));
            Assert.AreEqual(1,Count(doc,"/manifest/application"));
            // Saved and parsed again, the marker still carries the tools namespace Gradle's merger reads.
            var round=Load(doc.OuterXml);
            Assert.AreEqual(1,Count(round,$"/manifest/uses-permission[@a:name='{Mic}' and @t:node='remove']"));
            StringAssert.Contains("xmlns:tools=\""+RemovedPermissions.ToolsNs+"\"",doc.DocumentElement.OuterXml.Substring(0,doc.DocumentElement.OuterXml.IndexOf('>')));
        }

        [Test] public void MarkRemovedIsIdempotentAndReplacesPlainDeclarations()
        {
            var doc=Load(Library.Replace("<manifest ","<manifest xmlns:tools=\""+RemovedPermissions.ToolsNs+"\" "));
            RemovedPermissions.MarkRemoved(doc,new[] { Mic });
            string once=doc.OuterXml;
            RemovedPermissions.MarkRemoved(doc,new[] { Mic });
            Assert.AreEqual(once,doc.OuterXml);
            Assert.AreEqual(1,Count(doc,$"/manifest/*[@a:name='{Mic}']"),"just the marker");
            Assert.AreEqual(1,Count(doc,$"/manifest/uses-permission[@a:name='{Mic}' and @t:node='remove']"));
        }
    }
}
