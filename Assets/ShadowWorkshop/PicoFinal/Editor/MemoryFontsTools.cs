using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ShapeOfMemory.Editor
{
    // Bakes the game's static TMP atlases (MemoryFonts) from the TTFs in PicoFinal/Fonts, which
    // Tools/ShadowArt/build_fonts.py makes from Google Fonts' originals. Run MetaHackthone > Pico Final > Fonts > Bake
    // atlases after new text (a table edit, a credit) brings a character the atlases don't hold: MemoryTextTests fails until
    // then. Latin faces hold ASCII, Latin-1 and typographic punctuation whatever the text; Chinese ones hold what the
    // Chinese tables use, plus ASCII and Latin-1 for names and digits. The assets keep their GUIDs across bakes.
    public static class MemoryFontsTools
    {
        public const string Folder=ShapeOfMemoryBuilder.Root+"/Fonts";
        public const string AssetPath=ShapeOfMemoryBuilder.Root+"/Resources/"+MemoryFonts.ResourcePath+".asset";
        // SDF sampling size and padding (an eighth of it, room for an outline or underlay).
        public const int Sampling=64,Padding=8;
        const string Punctuation="‘’“”–—…•·′″€";
        const string CjkPunctuation="，。、：；！？“”‘’（）《》〈〉【】「」『』—…·～";
        const string Kerned="‘’“”–—…";

        [MenuItem("MetaHackthone/Pico Final/Fonts/Bake atlases")]
        public static void BakeMenu()=>Debug.Log(Bake());
        [MenuItem("MetaHackthone/Pico Final/Fonts/Select fonts asset")]
        public static void Select(){var fonts=FindOrCreate();Selection.activeObject=fonts;EditorGUIUtility.PingObject(fonts);}

        public static string Bake()
        {
            var (latin,cjk)=Characters();
            // The font engine keeps faces it has loaded: after build_fonts.py rewrites a TTF it would read the old one
            // (2026-11-14: the Chinese Bold baked empty until this).
            FontEngine.UnloadAllFontFaces();
            var fonts=FindOrCreate();
            var log=new StringBuilder($"SHAPE_OF_MEMORY_FONTS baked ({latin.Length} Latin, {cjk.Length} Chinese characters):\n");
            fonts.text=Bake("MemoryText-Regular",latin,1024,1024,log);
            fonts.italic=Bake("MemoryText-Italic",latin,1024,1024,log);
            fonts.bold=Bake("MemoryText-Bold",latin,1024,1024,log);
            fonts.caps=Bake("MemoryCaps-SemiBold",latin,1024,1024,log);
            fonts.title=Bake("CinzelDecorative-Bold",latin,1024,1024,log);
            fonts.cjkText=Bake("NotoSerifSC-Regular",cjk,2048,2048,log);
            fonts.cjkBold=Bake("NotoSerifSC-Bold",cjk,2048,2048,log);
            Weights(fonts.text,fonts.bold,fonts.italic);
            Weights(fonts.cjkText,fonts.cjkBold,null);
            EditorUtility.SetDirty(fonts);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        public static MemoryFonts FindOrCreate()
        {
            var fonts=AssetDatabase.LoadAssetAtPath<MemoryFonts>(AssetPath);if(fonts)return fonts;
            fonts=ScriptableObject.CreateInstance<MemoryFonts>();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));
            AssetDatabase.CreateAsset(fonts,AssetPath);AssetDatabase.SaveAssets();
            return fonts;
        }

        // What each script's faces must hold.
        public static (string latin,string cjk) Characters()
        {
            var latin=new SortedSet<char>();var cjk=new SortedSet<char>();
            void Both(char c){latin.Add(c);cjk.Add(c);}
            for(int c=0x20;c<0x7F;c++)Both((char)c);
            for(int c=0xA0;c<=0xFF;c++)Both((char)c);
            foreach(char c in Punctuation)Both(c);
            foreach(char c in CjkPunctuation)cjk.Add(c);
            foreach(var name in new[]{MemoryText.Ui,MemoryText.Book})
            {
                var collection=LocalizationEditorSettings.GetStringTableCollection(name);if(collection==null)continue;
                foreach(var table in collection.StringTables)
                {
                    var into=MemoryText.IsUpright(table.LocaleIdentifier.Code)?cjk:latin;
                    foreach(var entry in table.Values)foreach(char c in entry?.Value??"")if(!char.IsControl(c))into.Add(c);
                }
            }
            var credits=MemoryCredits.Load();
            if(credits)foreach(char c in credits.Printed())if(!char.IsControl(c))Both(c);
            return (new string(latin.ToArray()),new string(cjk.ToArray()));
        }

        static TMP_FontAsset Bake(string name,string characters,int width,int height,StringBuilder log)
        {
            string fontPath=$"{Folder}/{name}.ttf",assetPath=$"{Folder}/{name} SDF.asset";
            var font=AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if(!font){log.AppendLine($"  MISSING {fontPath} (run Tools/ShadowArt/build_fonts.py)");return null;}
            var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if(asset&&(asset.atlasWidth!=width||asset.atlasHeight!=height||asset.atlasPadding!=Padding||(int)asset.faceInfo.pointSize!=Sampling))
            {AssetDatabase.DeleteAsset(assetPath);asset=null;}
            string missing=characters;
            // Refill the existing asset (its GUID stays); one that still takes nothing is made again from scratch.
            if(asset)
            {
                asset.atlasPopulationMode=AtlasPopulationMode.Dynamic;asset.ClearFontAssetData(false);
                asset.TryAddCharacters(characters,out missing,true);
                if(asset.characterTable.Count==0){AssetDatabase.DeleteAsset(assetPath);asset=null;missing=characters;}
            }
            if(!asset)
            {
                asset=TMP_FontAsset.CreateFontAsset(font,Sampling,Padding,GlyphRenderMode.SDFAA,width,height,AtlasPopulationMode.Dynamic,false);
                asset.name=name+" SDF";
                AssetDatabase.CreateAsset(asset,assetPath);
                asset.atlasTexture.name=name+" SDF Atlas";AssetDatabase.AddObjectToAsset(asset.atlasTexture,asset);
                asset.material.name=name+" SDF Material";AssetDatabase.AddObjectToAsset(asset.material,asset);
                asset.TryAddCharacters(characters,out missing,true);
            }
            // Kerning pairs come in with junk lookup flags (uninitialised; most carry IgnoreSpacingAdjustments), which spaced
            // tracked capitals unevenly ("HOW T H I S BOOK"): no flags, so a pair's kerning and the label's spacing both apply.
            // Kerning only between ASCII letters, digits and punctuation (and curly quotes, dashes, ellipsis): the import
            // brings the whole font's pairs, ~30 MB of YAML a face, nearly all for glyphs we never set side by side.
            var pairs=asset.fontFeatureTable.glyphPairAdjustmentRecords;
            var kerned=new HashSet<uint>(asset.characterTable.Where(c=>c.unicode<0x7F||Kerned.IndexOf((char)c.unicode)>=0).Select(c=>c.glyphIndex));
            pairs.RemoveAll(p=>!kerned.Contains(p.firstAdjustmentRecord.glyphIndex)||!kerned.Contains(p.secondAdjustmentRecord.glyphIndex));
            // Mark positioning is for combining accents, which no text uses (Latin-1 letters come precomposed).
            var feature=new SerializedObject(asset);
            foreach(var marks in new[]{"m_FontFeatureTable.m_MarkToBaseAdjustmentRecords","m_FontFeatureTable.m_MarkToMarkAdjustmentRecords"})
                feature.FindProperty(marks)?.ClearArray();
            feature.ApplyModifiedPropertiesWithoutUndo();
            for(int i=0;i<pairs.Count;i++){var pair=pairs[i];pair.featureLookupFlags=UnityEngine.TextCore.LowLevel.FontFeatureLookupFlags.None;pairs[i]=pair;}
            asset.ReadFontAssetDefinition();
            // The default font's SDF shader, so every label shares the shader the build already has.
            var shader=TMP_Settings.defaultFontAsset?TMP_Settings.defaultFontAsset.material.shader:null;
            if(shader&&asset.material.shader!=shader)asset.material.shader=shader;
            asset.atlasPopulationMode=AtlasPopulationMode.Static;
            EditorUtility.SetDirty(asset);EditorUtility.SetDirty(asset.material);
            foreach(var atlas in asset.atlasTextures)if(atlas)EditorUtility.SetDirty(atlas);
            log.AppendLine($"  {asset.name}: {asset.characterTable.Count} characters in {asset.atlasWidth}x{asset.atlasHeight}"+
                (string.IsNullOrEmpty(missing)?"":$"; not in the font or no room: {missing.Length} ({Short(missing)})"));
            return asset;
        }
        static string Short(string s)=>s.Length>40?s.Substring(0,40)+"...":s;

        // The regular face's weight table: Italic (weight 400) and Bold (700), so FontStyles and <i>/<b> print real faces.
        static void Weights(TMP_FontAsset regular,TMP_FontAsset bold,TMP_FontAsset italic)
        {
            if(!regular)return;
            var table=regular.fontWeightTable;
            table[4].italicTypeface=italic;
            table[7].regularTypeface=bold;
            EditorUtility.SetDirty(regular);
        }
    }
}
