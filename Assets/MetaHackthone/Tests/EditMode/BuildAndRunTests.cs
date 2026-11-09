using System.IO;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly.Editor;
using UnityEditor;
using UnityEngine;

namespace PhysicalAssembly.Tests
{
    public sealed class BuildAndRunTests
    {
        [Test] public void ParsesTheAdbDeviceList()
        {
            var devices=Headsets.ParseDevices("* daemon not running; starting now at tcp:5037\r\n* daemon started successfully\r\nList of devices attached\r\n"+
                "PA94Y0MGL6240169G\tdevice\r\n1WMHHA66GN2046\tunauthorized\r\n2G0YC5ZG7M00PB\toffline\r\n\r\n");
            CollectionAssert.AreEqual(new[] { ("PA94Y0MGL6240169G","device"),("1WMHHA66GN2046","unauthorized"),("2G0YC5ZG7M00PB","offline") },devices);
            Assert.IsEmpty(Headsets.ParseDevices("List of devices attached\n\n"));
        }
        [Test] public void TellsQuestFromPico()
        {
            Assert.AreEqual((DevicePlatform?)DevicePlatform.Quest,Headsets.Classify("Oculus\r","Quest 2\r").platform);
            Assert.AreEqual("Quest 2",Headsets.Classify("Oculus","Quest 2").name);
            Assert.AreEqual("Quest 3",Headsets.Classify("Meta","Quest 3").name);
            Assert.AreEqual(((DevicePlatform?)DevicePlatform.Pico,"PICO 4 Ultra"),Headsets.Classify("Pico","A9210"));
            Assert.IsNull(Headsets.Classify("samsung","SM-S918B").platform);
        }
        [Test] public void NamesApksPerPlatform()
        {
            Assert.AreEqual("Builds/ShapeOfMemory.apk",BuildRunner.ApkPath("ShapeOfMemory",DevicePlatform.Quest));
            Assert.AreEqual("Builds/ShapeOfMemory-Pico.apk",BuildRunner.ApkPath("ShapeOfMemory",DevicePlatform.Pico));
            // Outside a BuildRunner build, PicoBuild's hooks only have the file name to go by.
            Assert.IsTrue(BuildRunner.IsPico("D:/x/Builds/MetaHackthone-Pico.apk"));
            Assert.IsFalse(BuildRunner.IsPico("D:/x/Builds/MetaHackthone.apk"));
        }
        [Test] public void InstallErrorsComeWithAdvice()
        {
            StringAssert.Contains("another computer",Headsets.Advice("Failure [INSTALL_FAILED_UPDATE_INCOMPATIBLE: Existing package signatures do not match]"));
            StringAssert.Contains("system library",Headsets.Advice("Failure [INSTALL_FAILED_MISSING_SHARED_LIBRARY: Supplement horizonos-supplement-hzplatformclientcore does not exist]"));
            Assert.IsNull(Headsets.Advice("Failure [INSTALL_FAILED_SOMETHING_NEW]"));
        }
        [Test] public void ConfigsBuildRealScenesToDistinctApks()
        {
            var configs=DeployConfig.All();
            foreach(var config in configs)
            {
                Assert.IsTrue(config.Quest || config.Pico,config.name+" builds for no platform");
                Assert.IsNotEmpty(config.ScenePaths,config.name+" has no scenes");
                foreach(var scene in config.ScenePaths) FileAssert.Exists(scene,config.name);
            }
            var apks=configs.SelectMany(c=>new[] { DevicePlatform.Quest,DevicePlatform.Pico }.Where(c.Supports).Select(c.ApkPath)).ToArray();
            CollectionAssert.AllItemsAreUnique(apks);
            var memory=DeployConfig.Find("Shape of Memory");
            Assert.IsNotNull(memory,"Shape of Memory config");
            CollectionAssert.AreEqual(new[] { "Assets/ShadowWorkshop/PicoFinal/Scenes/PicoFinal.unity" },memory.ScenePaths);
            Assert.AreEqual("com.metahackathon2026.shadow",memory.Package);
            Assert.AreEqual("Builds/ShapeOfMemory-Pico.apk",memory.ApkPath(DevicePlatform.Pico));
            // Its chapters are checked to be in this folder (ShapeOfMemory.Tests MemoryChaptersTests).
            Assert.AreEqual("Assets/ShadowWorkshop/Resources/ShadowArt/Torch2",memory.LevelFolder,"Shape of Memory starts at any Torch2 level");
            Assert.IsNotEmpty(memory.Levels);
            Assert.IsNotNull(memory.Icon,"Shape of Memory icon");
            Assert.AreEqual(memory.Icon.width,memory.Icon.height,"square icon");
            Assert.GreaterOrEqual(memory.Icon.width,512,"icon big enough for the store's 512");
        }
        [Test] public void BuildIconSwapsInAndBackOut()
        {
            string before=BuildRunner.DefaultIconPath();
            var icon=DeployConfig.Find("Shape of Memory").Icon;
            try
            {
                BuildRunner.SetDefaultIcon(icon);
                Assert.AreEqual(AssetDatabase.GetAssetPath(icon),BuildRunner.DefaultIconPath());
            }
            finally { BuildRunner.SetDefaultIcon(before==""?null:AssetDatabase.LoadAssetAtPath<Texture2D>(before)); }
            Assert.AreEqual(before,BuildRunner.DefaultIconPath());
        }
        [Test] public void LaunchArgumentsComeFromOverridesInTheEditor()
        {
            try
            {
                Assert.IsNull(LaunchArguments.Get("level"));
                LaunchArguments.Overrides["level"]="BonVoyage"; Assert.AreEqual("BonVoyage",LaunchArguments.Get("level"));
            }
            finally { LaunchArguments.Overrides.Clear(); }
        }
        // An APK is stale when it's missing, when game code recompiled after it, or when the level or what it uses changed.
        [Test] public void StaleBuildsAreNoticed()
        {
            var config=ScriptableObject.CreateInstance<DeployConfig>();
            config.ApkName="StaleTest"; config.LevelFolder="Assets/ShadowWorkshop/Resources/ShadowArt/Torch2";
            string apk=config.ApkPath(DevicePlatform.Quest);
            try
            {
                StringAssert.Contains("no StaleTest.apk",BuildRunner.Stale(config,DevicePlatform.Quest,""));
                Directory.CreateDirectory("Builds"); File.WriteAllText(apk,"");
                Assert.IsNull(BuildRunner.Stale(config,DevicePlatform.Quest,"Birthday"),"newer than everything");
                StringAssert.Contains("doesn't exist",BuildRunner.Stale(config,DevicePlatform.Quest,"NoSuchLevel"));
                File.SetLastWriteTime(apk,new System.DateTime(2001,1,1));
                StringAssert.Contains("code changed",BuildRunner.Stale(config,DevicePlatform.Quest,"Birthday"));
            }
            finally { File.Delete(apk); Object.DestroyImmediate(config); }
        }
    }
}
