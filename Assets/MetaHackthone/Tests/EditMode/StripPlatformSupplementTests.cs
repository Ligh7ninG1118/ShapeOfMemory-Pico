using System.Xml;
using NUnit.Framework;
using PhysicalAssembly.Editor;

namespace PhysicalAssembly.Tests
{
    public sealed class StripPlatformSupplementTests
    {
        const string Manifest=@"<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" xmlns:horizonos=""http://schemas.horizonos/sdk"">
  <horizonos:uses-horizonos-sdk horizonos:minSdkVersion=""60"" horizonos:targetSdkVersion=""207"" />
  <horizonos:uses-horizonos-supplement name=""horizonos-supplement-other"" minSupplementVersion=""1"" />
  <horizonos:uses-horizonos-supplement name=""horizonos-supplement-hzplatformclientcore"" minSupplementVersion=""1"" targetSupplementVersion=""1"" />
  <horizonos:uses-horizonos-supplement horizonos:name=""horizonos-supplement-hzplatformclientcore"" />
  <uses-horizonos-supplement name=""horizonos-supplement-hzplatformclientcore"" />
  <application><horizonos:uses-horizonos-supplement name=""horizonos-supplement-hzplatformclientcore"" /></application>
</manifest>";
        static XmlDocument Load(string xml) { var doc=new XmlDocument(); doc.LoadXml(xml); return doc; }
        static int Count(XmlDocument doc,string xpath)
        {
            var ns=new XmlNamespaceManager(doc.NameTable); ns.AddNamespace("horizonos","http://schemas.horizonos/sdk");
            return doc.SelectNodes(xpath,ns).Count;
        }
        [Test] public void RemovesOnlyTheTopLevelPlatformSupplement()
        {
            var doc=Load(Manifest);
            Assert.AreEqual(2,StripPlatformSupplement.Strip(doc));
            Assert.AreEqual(0,Count(doc,"/manifest/horizonos:uses-horizonos-supplement[@*[local-name()='name']='horizonos-supplement-hzplatformclientcore']"));
            Assert.AreEqual(1,Count(doc,"/manifest/horizonos:uses-horizonos-supplement[@name='horizonos-supplement-other']"));
            Assert.AreEqual(1,Count(doc,"/manifest/horizonos:uses-horizonos-sdk"));
            Assert.AreEqual(1,Count(doc,"/manifest/uses-horizonos-supplement"),"un-namespaced element is not a Horizon declaration");
            Assert.AreEqual(1,Count(doc,"/manifest/application/horizonos:uses-horizonos-supplement"),"only <manifest> children are declarations");
        }
        [Test] public void IsIdempotent()
        {
            var doc=Load(Manifest); StripPlatformSupplement.Strip(doc);
            Assert.AreEqual(0,StripPlatformSupplement.Strip(doc));
        }
    }
}
