using System.Linq;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.UI;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace ShapeOfMemory.Editor
{
    // The game's words are in Localization tables (MemoryText): edit them in the Localization Tables window, one column
    // per language. The chapters' words (and the credits' headings) start in assets, where designers write them in
    // English (the gym, the Credits inspector): Copy asset text into tables brings that English across after an edit.
    // The translation of a line whose English changed is not touched: Check text and the log say which to look at.
    public static class MemoryTextTools
    {
        const string Menu="MetaHackthone/Pico Final/Text/";

        [MenuItem(Menu+"Open tables")]
        public static void Open()=>LocalizationTablesWindow.ShowWindow(LocalizationEditorSettings.GetStringTableCollection(MemoryText.Ui));

        [MenuItem(Menu+"Copy asset text into tables")]
        public static void CopyAssetText()
        {
            int changed=CopyAssetText(MemoryChapters.Load(),MemoryCredits.Load());
            Debug.Log(changed==0?"SHAPE_OF_MEMORY_TEXT the Book table's English already matches the assets":$"SHAPE_OF_MEMORY_TEXT {changed} English line(s) copied into {MemoryText.Book}; check their translations");
        }
        // Writes the assets' English into the Book table's English column; returns how many lines changed.
        public static int CopyAssetText(MemoryChapters chapters,MemoryCredits credits)
        {
            var collection=LocalizationEditorSettings.GetStringTableCollection(MemoryText.Book);
            if(!collection){Debug.LogError("SHAPE_OF_MEMORY_TEXT no table collection "+MemoryText.Book);return 0;}
            var english=collection.GetTable("en") as StringTable;
            if(!english){Debug.LogError("SHAPE_OF_MEMORY_TEXT "+MemoryText.Book+" has no English table");return 0;}
            int changed=0;
            void Put(string key,string text)
            {
                if(string.IsNullOrWhiteSpace(text))return;
                text=text.Replace("\r\n","\n");var entry=english.GetEntry(key);if(entry!=null&&entry.Value==text)return;
                Debug.Log($"SHAPE_OF_MEMORY_TEXT {key}: \"{entry?.Value}\" -> \"{text}\"");
                english.AddEntry(key,text);changed++;
            }
            if(chapters)foreach(var level in chapters.chapters.Where(l=>l))foreach(var field in MemoryText.LevelFields)Put(MemoryText.LevelKey(level,field),MemoryText.LevelField(level,field));
            if(credits)
            {
                Put(MemoryText.MadeByKey,credits.madeBy);Put(MemoryText.ThanksToKey,credits.thanksTo);
                foreach(var section in credits.Sections)if(!string.IsNullOrWhiteSpace(section.heading))Put(MemoryText.SectionKey(section.heading),section.heading);
            }
            if(changed>0){EditorUtility.SetDirty(english);EditorUtility.SetDirty(english.SharedData);AssetDatabase.SaveAssets();}
            return changed;
        }

        [MenuItem(Menu+"Check text")]
        public static void Check()
        {
            var book=MemoryChapters.Load();
            var problems=MemoryText.Check(book?book.chapters:new Torch2Level[0],MemoryCredits.Load());
            foreach(var problem in problems)Debug.LogWarning("SHAPE_OF_MEMORY_TEXT "+problem);
            if(problems.Count==0)Debug.Log("SHAPE_OF_MEMORY_TEXT every line has English and Chinese, and the Book table matches the assets");
        }
    }
}
