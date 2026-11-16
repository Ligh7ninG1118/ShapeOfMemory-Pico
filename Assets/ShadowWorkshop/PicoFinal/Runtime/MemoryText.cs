using System;
using System.Collections.Generic;
using System.Linq;
using CloudGarden.ShadowPuzzle;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;

namespace ShapeOfMemory
{
    // Every word the game prints, in the player's language (Unity Localization; edit them in Window > Asset Management >
    // Localization Tables, or MetaHackthone > Pico Final > Text > Open tables). The language is the headset's own
    // (Localization Settings' startup selectors: command line, then the system language, then English); a language
    // the game doesn't have falls back to English, and so does a word not yet translated.
    // - Ui: the words written in code (keys like "how/turn"). Missing one is an error: MemoryTextTests reads the keys
    //   out of the code.
    // - Book: the words kept in assets, the chapters' (Torch2Level name/riddle/hint/complete, keyed "<level>/<field>")
    //   and the credits' headings. The asset's own text is the fallback; "Copy asset text into tables" refreshes the
    //   English column after a designer edits a level, and Check reports one that drifted.
    // The typefaces are MemoryFonts' (by role and script): static atlases baked in the editor, holding every character
    // the tables use (MemoryTextTests checks), so nothing is made at run time.
    // In table text, <em>words</em> print in the highlight colour the caller gives (MemoryPalette's aqua).
    public static class MemoryText
    {
        public const string Ui="ShapeOfMemory UI",Book="ShapeOfMemory Book";
        // The language changed: print everything again.
        public static event Action Changed;

        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            if(hooked)LocalizationSettings.SelectedLocaleChanged-=OnChanged;
            hooked=false;Changed=null;
        }
        static void Hook(){if(hooked||!Application.isPlaying)return;hooked=true;LocalizationSettings.SelectedLocaleChanged+=OnChanged;}
        static void OnChanged(Locale locale)
        {
            Debug.Log("SHAPE_OF_MEMORY_TEXT language "+(locale?locale.Identifier.Code:"none"));
            Changed?.Invoke();
        }

        // The selected language; in edit mode (no selection yet) the project's, English.
        public static Locale Locale{get{Hook();var locale=LocalizationSettings.SelectedLocale;return locale?locale:LocalizationSettings.ProjectLocale;}}
        public static string Code{get{var locale=Locale;return locale?locale.Identifier.Code:"en";}}
        // Chinese, Japanese and Korean have no italics (a slanted face is a fake one): they print upright.
        public static bool Upright=>IsUpright(Code);
        public static bool IsUpright(string code)=>code.StartsWith("zh")||code.StartsWith("ja")||code.StartsWith("ko");
        // Switch language (tests, the development L key): Changed follows.
        public static void Use(string code){var locale=LocalizationSettings.AvailableLocales.GetLocale(code);if(locale)LocalizationSettings.SelectedLocale=locale;else Debug.LogError("SHAPE_OF_MEMORY_TEXT no locale "+code);}
        public static void Next()
        {
            var locales=LocalizationSettings.AvailableLocales.Locales;if(locales.Count==0)return;
            LocalizationSettings.SelectedLocale=locales[(locales.IndexOf(Locale)+1)%locales.Count];
        }
        public static FontStyles Style(FontStyles style)=>Upright?style&~FontStyles.Italic:style;
        public static string Italic(string text)=>Upright?text:"<i>"+text+"</i>";

        // A word from the Ui table; args fill a smart entry's {0}, {1}... A missing key prints itself and logs an error.
        public static string Get(string key,params object[] args)=>Get(key,null,args);
        public static string Get(string key,Color? emphasis,params object[] args)
        {
            var entry=Entry(Ui,key);
            if(entry==null||string.IsNullOrEmpty(entry.Value)){Debug.LogError($"SHAPE_OF_MEMORY_TEXT missing '{key}' in {Ui}");return key;}
            return Emphasis(args!=null&&args.Length>0?entry.GetLocalizedString(args):entry.GetLocalizedString(),emphasis);
        }
        // A word kept in an asset: the Book table's entry, else the asset's own text.
        public static string Asset(string key,string fallback)
        {
            var entry=Entry(Book,key);
            return entry!=null&&!string.IsNullOrEmpty(entry.Value)?entry.GetLocalizedString():fallback;
        }
        public static string LevelKey(Torch2Level level,string field)=>level.name+"/"+field;
        public static string Name(Torch2Level level)=>Asset(LevelKey(level,"button"),level.button);
        public static string Riddle(Torch2Level level)=>Asset(LevelKey(level,"riddle"),level.riddle);
        public static string Hint(Torch2Level level)=>Asset(LevelKey(level,"hint"),level.hint);
        public static string Complete(Torch2Level level)=>Asset(LevelKey(level,"complete"),level.complete);
        public static readonly string[] LevelFields={"button","riddle","hint","complete"};
        public static string LevelField(Torch2Level level,string field)=>field switch{"button"=>level.button,"riddle"=>level.riddle,"hint"=>level.hint,"complete"=>level.complete,_=>null};
        public static string SectionKey(string heading)=>"credits/section/"+heading;
        public const string MadeByKey="credits/made-by",ThanksToKey="credits/thanks-to";
        // "CHAPTER  IV" / "第四章": the table picks Roman {0}, Chinese {1} or the digit {2}.
        public static string Chapter(int number)=>Get("chapter/number",MemoryPages.Roman(number),Hanzi(number),number);
        public static string Hanzi(int n)=>n>=1&&n<=10?"一二三四五六七八九十"[n-1].ToString():n.ToString();

        static StringTableEntry Entry(string table,string key)
        {
            var result=LocalizationSettings.StringDatabase.GetTableEntry(table,key,Locale);
            return result.Entry;
        }
        static string Emphasis(string text,Color? color)
        {
            if(text.IndexOf("<em>",StringComparison.Ordinal)<0)return text;
            return color.HasValue?text.Replace("<em>",MemoryPalette.Tag(color.Value)).Replace("</em>","</color>"):text.Replace("<em>","").Replace("</em>","");
        }

        // What a label is, which picks its face (MemoryFonts): Text (sentences, and anything pressed, in Bold), Caps (small
        // capitals: kickers, chapter numbers, banners, headings) or Title (the cover's title, chapter names, THANK YOU).
        // Auto: Caps if the label is Bold, else Text.
        public enum Role{Auto,Text,Caps,Title}
        // The face for a role in the selected language (null if the fonts asset is missing).
        public static TMP_FontAsset Face(Role role)=>Face(role,Upright);
        public static TMP_FontAsset Face(Role role,bool upright)
        {
            var fonts=MemoryFonts.Load();
            if(!fonts){Debug.LogError("SHAPE_OF_MEMORY_TEXT missing Resources/"+MemoryFonts.ResourcePath+".asset (Pico Final > Fonts > Bake atlases makes it)");return null;}
            return fonts.For(role==Role.Auto?Role.Text:role,upright);
        }
        // A language's own name, from its own table ("English", "简体中文": the Settings page lists each in its own words
        // and face, whatever language is selected).
        public static string NameOf(string code)
        {
            var locale=LocalizationSettings.AvailableLocales.GetLocale(code);if(!locale)return code;
            var entry=LocalizationSettings.StringDatabase.GetTableEntry(Ui,"language/name",locale).Entry;
            return entry!=null&&!string.IsNullOrEmpty(entry.Value)?entry.Value:code;
        }
        // Give a label its face; call it after setting the label's style. Labels made by MemoryUi.Label get Text; held ones
        // get theirs again on Changed. Caps and Title are drawn heavy already, so a Bold flag on them is dropped rather
        // than thickening them further.
        public static void Apply(TMP_Text label,Role role=Role.Text)
        {
            if(!label)return;
            if(role==Role.Auto)role=(label.fontStyle&FontStyles.Bold)!=0?Role.Caps:Role.Text;
            var f=Face(role);if(!f)f=TMP_Settings.defaultFontAsset;
            if(f&&label.font!=f)label.font=f;
            if(role!=Role.Text)label.fontStyle&=~FontStyles.Bold;
        }

        // What the tables are missing for the book: a key with no text in some language, an asset's English that moved on
        // from the table's, a language with no font for its script. Nothing here breaks the game (English stands in).
        public static List<string> Check(IEnumerable<Torch2Level> levels,MemoryCredits credits)
        {
            var problems=new List<string>();
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            var locales=LocalizationSettings.AvailableLocales.Locales;
            var english=locales.FirstOrDefault(l=>l.Identifier.Code=="en");
            if(!english){problems.Add("No English locale.");return problems;}
            foreach(var locale in locales)
            {
                var ui=LocalizationSettings.StringDatabase.GetTable(Ui,locale);
                if(!ui){problems.Add($"{locale.Identifier.Code}: no {Ui} table.");continue;}
                foreach(var shared in ui.SharedData.Entries)
                {
                    var entry=ui.GetEntry(shared.Id);
                    if(entry==null||string.IsNullOrWhiteSpace(entry.Value))problems.Add($"{locale.Identifier.Code}: {Ui} '{shared.Key}' has no text.");
                }
                var book=LocalizationSettings.StringDatabase.GetTable(Book,locale);
                if(!book){problems.Add($"{locale.Identifier.Code}: no {Book} table.");continue;}
                void Field(string key,string own)
                {
                    var entry=book.GetEntry(key);
                    if(entry==null||string.IsNullOrWhiteSpace(entry.Value)){problems.Add($"{locale.Identifier.Code}: {Book} '{key}' has no text"+(locale==english?" (run Copy asset text into tables)":"")+".");return;}
                    if(locale==english&&own!=null&&Normal(entry.Value)!=Normal(own))problems.Add($"en: {Book} '{key}' is not the asset's text (run Copy asset text into tables): \"{Short(entry.Value)}\" vs \"{Short(own)}\".");
                }
                foreach(var level in levels)if(level)foreach(var field in LevelFields)Field(LevelKey(level,field),LevelField(level,field));
                if(credits)
                {
                    Field(MadeByKey,credits.madeBy);Field(ThanksToKey,credits.thanksTo);
                    foreach(var section in credits.Sections)if(!string.IsNullOrWhiteSpace(section.heading))Field(SectionKey(section.heading),section.heading);
                }
            }
            return problems;
        }
        static string Normal(string s)=>(s??"").Replace("\r\n","\n").Trim();
        static string Short(string s){s=Normal(s).Replace("\n"," / ");return s.Length>40?s.Substring(0,40)+"...":s;}
    }
}
