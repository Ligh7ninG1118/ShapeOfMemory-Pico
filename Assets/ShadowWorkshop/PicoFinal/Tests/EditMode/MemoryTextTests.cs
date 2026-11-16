using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;

namespace ShapeOfMemory.Tests
{
    // The game's words (MemoryText): both languages complete, the code's keys in the table, the Chinese font able to print
    // every character, and a headset's language reaching the right locale.
    public class MemoryTextTests
    {
        const string Runtime="Assets/ShadowWorkshop/PicoFinal/Runtime";
        static StringTable Table(string name,string code)
        {
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            return LocalizationSettings.StringDatabase.GetTable(name,LocalizationSettings.AvailableLocales.GetLocale(code));
        }

        [Test] public void EveryLineHasEnglishAndChineseAndTheBookMatchesItsAssets()
        {
            var book=MemoryChapters.Load();Assert.NotNull(book,"the chapters");
            var problems=MemoryText.Check(book.chapters,MemoryCredits.Load());
            Assert.IsEmpty(problems,string.Join("\n",problems));
        }

        // Keys are written out in the code (T("key"), MemoryText.Get("key", ...)), never built, so they can be read here.
        [Test] public void EveryKeyTheCodeUsesIsInTheTable()
        {
            var shared=Table(MemoryText.Ui,"en").SharedData;
            var used=new HashSet<string>();
            foreach(var file in Directory.GetFiles(Runtime,"*.cs"))
            {
                // MemoryText itself calls its own Get unqualified.
                string call=Path.GetFileName(file)=="MemoryText.cs"?@"\bGet":@"(?:\bT|MemoryText\.Get)";
                string text=File.ReadAllText(file);
                foreach(Match match in Regex.Matches(text,call+@"\(""([^""]+)"""))used.Add(match.Groups[1].Value);
                // The buttons by the hand: new Spec(id, caption key, ...).
                foreach(Match match in Regex.Matches(text,@"new Spec\(""[^""]+"",""([^""]+)"""))used.Add(match.Groups[1].Value);
            }
            Assert.Greater(used.Count,40,"the scan finds the keys");
            var missing=used.Where(key=>!shared.Contains(key)).ToArray();
            Assert.IsEmpty(missing,"not in "+MemoryText.Ui+": "+string.Join(", ",missing));
            var unused=shared.Entries.Select(e=>e.Key).Where(key=>!used.Contains(key)).ToArray();
            if(unused.Length>0)Debug.LogWarning("SHAPE_OF_MEMORY_TEXT in "+MemoryText.Ui+" but not used by the code: "+string.Join(", ",unused));
        }

        [Test] public void SmartLinesTakeTheirNumbers()
        {
            var en=LocalizationSettings.AvailableLocales.GetLocale("en");var zh=LocalizationSettings.AvailableLocales.GetLocale("zh-Hans");
            string Line(string key,UnityEngine.Localization.Locale locale,params object[] args)=>LocalizationSettings.StringDatabase.GetLocalizedString(MemoryText.Ui,key,locale,FallbackBehavior.UseProjectSettings,args);
            Assert.AreEqual("CHAPTER IV",Line("chapter/number",en,"IV","四",4));
            Assert.AreEqual("第四章",Line("chapter/number",zh,"IV","四",4));
            StringAssert.Contains("2",Line("progress/some",zh,2,4));StringAssert.Contains("4",Line("progress/some",zh,2,4));
            Assert.AreEqual("2 of 4 memories found.",Line("progress/some",en,2,4));
            Assert.AreEqual("四",MemoryText.Hanzi(4));Assert.AreEqual("十",MemoryText.Hanzi(10));
        }

        // Every face a language prints in is a baked (static) atlas holding every character its tables and the credits hold
        // (else TMP prints a box): after new text, MetaHackthone > Pico Final > Fonts > Bake atlases.
        [Test] public void EachLanguagesFacesHoldEveryCharacterItPrints()
        {
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            var fonts=MemoryFonts.Load();Assert.NotNull(fonts,"Resources/"+MemoryFonts.ResourcePath+".asset");
            var credits=MemoryCredits.Load();
            foreach(var locale in LocalizationSettings.AvailableLocales.Locales)
            {
                string code=locale.Identifier.Code;bool upright=MemoryText.IsUpright(code);
                var text=new System.Text.StringBuilder();
                foreach(var name in new[]{MemoryText.Ui,MemoryText.Book})foreach(var entry in Table(name,code).Values)text.Append(entry.Value);
                if(credits)text.Append(credits.Printed());
                var faces=upright?new[]{fonts.cjkText,fonts.cjkBold}:new[]{fonts.text,fonts.italic,fonts.bold,fonts.caps,fonts.title};
                foreach(var face in faces)
                {
                    Assert.NotNull(face,code+": a face is missing from the fonts asset");
                    Assert.AreEqual(TMPro.AtlasPopulationMode.Static,face.atlasPopulationMode,face.name+" is baked, not made at run time");
                    var missing=new HashSet<char>();
                    foreach(char c in text.ToString())if(!char.IsControl(c)&&!face.HasCharacter(c))missing.Add(c);
                    Assert.IsEmpty(missing,$"{code}: {face.name} lacks {new string(missing.ToArray())} (rebake the atlases)");
                }
            }
            Assert.AreEqual(fonts.italic,fonts.text.fontWeightTable[4].italicTypeface,"real italics");
            Assert.AreEqual(fonts.bold,fonts.text.fontWeightTable[7].regularTypeface,"real bold");
            Assert.AreEqual(fonts.cjkBold,fonts.cjkText.fontWeightTable[7].regularTypeface,"real Chinese bold");
        }

        // What Android's Locale.toLanguageTag gives (and the system language Unity reads) to the locale the game picks; a
        // language it doesn't have finds none, and the startup selectors then fall to English.
        [TestCase("zh-CN","zh-Hans")]
        [TestCase("zh-Hans-CN","zh-Hans")]
        [TestCase("zh-SG","zh-Hans")]
        [TestCase("en-US","en")]
        [TestCase("en-GB","en")]
        [TestCase("zh-TW",null)]
        [TestCase("fr-FR",null)]
        public void AHeadsetLanguageFindsItsLocale(string tag,string expected)
        {
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            var locale=LocalizationSettings.AvailableLocales.GetLocale(new CultureInfo(tag));
            Assert.AreEqual(expected,locale?locale.Identifier.Code:null);
        }
        [Test] public void TheSystemLanguagesFindTheirLocales()
        {
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            Assert.AreEqual("zh-Hans",LocalizationSettings.AvailableLocales.GetLocale(SystemLanguage.ChineseSimplified)?.Identifier.Code);
            Assert.AreEqual("en",LocalizationSettings.AvailableLocales.GetLocale(SystemLanguage.English)?.Identifier.Code);
            var selectors=LocalizationSettings.Instance.GetStartupLocaleSelectors();
            Assert.IsTrue(selectors.Any(s=>s is SystemLocaleSelector),"the system language decides");
            Assert.IsFalse(selectors.Any(s=>s is PlayerPrefLocaleSelector),"not a language saved from an earlier run (it would pin the first one)");
            var last=selectors.Last() as SpecificLocaleSelector;Assert.NotNull(last,"then a fixed one");Assert.AreEqual("en",last.LocaleId.Code);
            Assert.IsTrue(LocalizationSettings.InitializeSynchronously,"ready on the first frame");
        }

        [Test] public void ChineseAndJapanesePrintUpright()
        {
            var was=LocalizationSettings.SelectedLocale;
            try
            {
                MemoryText.Use("zh-Hans");Assert.IsTrue(MemoryText.Upright);Assert.AreEqual(TMPro.FontStyles.Bold,MemoryText.Style(TMPro.FontStyles.Bold|TMPro.FontStyles.Italic));
                Assert.AreEqual("待补充",MemoryText.Get("credits/to-come"));Assert.AreEqual("待补充",MemoryText.Italic("待补充"));
                MemoryText.Use("en");Assert.IsFalse(MemoryText.Upright);Assert.AreEqual("<i>x</i>",MemoryText.Italic("x"));
                Assert.AreEqual("Pinch its <color=#FF0000>glowing edge</color> and carry it over.",MemoryText.Get("how/turn/hands",Color.red));
                Assert.AreEqual("Pinch its glowing edge and carry it over.",MemoryText.Get("how/turn/hands"),"no colour given: the marks go");
            }
            finally{LocalizationSettings.SelectedLocale=was;}
        }
    }
}
