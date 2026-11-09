using System;
using System.Collections.Generic;
using System.Linq;
using CloudGarden.ShadowPuzzle;
using UnityEngine;

namespace ShapeOfMemory
{
    // The book's chapters in play order: Resources/ShapeOfMemory/Chapters.asset (MetaHackthone > Pico Final > Chapters).
    // Swap a level or reorder them there; nothing else names them. Check() says what is wrong before a build does
    // (Check chapters; a build with errors stops). What goes with the slot, not the level: the chapter's number and
    // colour (MemoryPalette, by order), the teaching (chapter I only) and the ending's 2x2 plates (so four).
    // Progress is saved per level id (the asset's name): a level swapped in starts unfound, and renaming one loses its saves.
    [CreateAssetMenu(menuName="Shape of Memory/Chapters")]
    public sealed class MemoryChapters : ScriptableObject
    {
        public const int Count=4;
        public const string ResourcePath="ShapeOfMemory/Chapters";
        [Tooltip("Torch2Level assets in play order (Resources/ShadowArt/Torch2, made by the Shadow Gym's Export). Exactly four.")]
        public Torch2Level[] chapters=Array.Empty<Torch2Level>();

        public readonly struct Problem
        {
            public readonly bool Error;public readonly string Text;
            public Problem(bool error,string text){Error=error;Text=text;}
            public override string ToString()=>(Error?"error: ":"warning: ")+Text;
        }

        public static MemoryChapters Load()=>Resources.Load<MemoryChapters>(ResourcePath);
        // The shipping book's level ids in order (empty, with an error logged, if the asset is missing).
        public static string[] ShippingIds()
        {
            var book=Load();if(book)return book.Ids;
            Debug.LogError("SHAPE_OF_MEMORY_CHAPTERS missing: Resources/"+ResourcePath+".asset (MetaHackthone > Pico Final > Chapters makes it)");
            return Array.Empty<string>();
        }
        public string[] Ids=>chapters.Select(c=>c?c.name:"").ToArray();
        public static string Numeral(int index)=>MemoryPages.Roman(index+1);

        // Errors break the book (a build stops on them); warnings show in it (a placeholder or empty text).
        public List<Problem> Check()
        {
            var problems=new List<Problem>();
            void Error(string text)=>problems.Add(new Problem(true,text));
            void Warn(string text)=>problems.Add(new Problem(false,text));
            if(chapters.Length!=Count)Error($"The book has {Count} chapters (four chapter colours, the ending's 2x2 plates); this list has {chapters.Length}.");
            for(int i=0;i<chapters.Length;i++)
            {
                var level=chapters[i];string at="Chapter "+Numeral(i);
                if(!level){Error(at+" is empty.");continue;}
                at+=$" ({level.name})";
                int first=Array.IndexOf(chapters,level);
                if(first<i){Error($"{at} is chapter {Numeral(first)} too: progress is saved per level, so they would share it.");continue;}
                if(level.pieces==null||level.pieces.Length==0||level.pieces.Any(p=>!p.mesh))Error(at+" has no pieces, or a piece without a mesh.");
                var lamps=level.lamps??Array.Empty<int>();
                if(lamps.Length<1||lamps.Length>2||lamps.Any(w=>w<0||w>1)||lamps.Distinct().Count()!=lamps.Length)Error($"{at} lamps must be 0 (left wall), 1 (right wall) or both; it has [{string.Join(", ",lamps)}].");
                else if(i==0&&lamps.Length>1)Warn(at+" lights both walls: chapter I teaches on the dark page, so its goal line would not show. Put a one-wall level first.");
                // A start near an answer (an accepted flip counts) is all but solved before the first turn.
                if(level.pieces!=null&&level.pieces.Length>0)
                {
                    float nearest=Torch2Projection.AnswersFor(level).Min(a=>Quaternion.Angle(level.start,a));
                    if(nearest<30)Error($"{at} starts {nearest:0} degrees from an answer (accepted flips count): it is solved at once. The gym's Check level with Auto Start picks a start.");
                    else if(nearest<70)Warn($"{at} starts {nearest:0} degrees from an answer (accepted flips count); 70 or more keeps it cold.");
                }
                if(Torch2Level.Load(level.name)!=level)Warn($"{at} is not in Resources/{Torch2Level.ResourceFolder}: it plays, but Build and Run cannot start at it.");
                if(string.IsNullOrWhiteSpace(level.button)||level.button==Torch2Gym.PlaceholderButton)Warn(at+" has no name yet (button): the pages and the found plaque show it.");
                if(string.IsNullOrWhiteSpace(level.riddle)||level.riddle==Torch2Gym.PlaceholderRiddle)Warn(at+" still has the gym's placeholder riddle, or none.");
                if(string.IsNullOrWhiteSpace(level.hint))Warn(at+" has no hint: HINT 1 would show nothing.");
                if(string.IsNullOrWhiteSpace(level.complete))Warn(at+" has no complete text: the FOUND page would have no words.");
            }
            return problems;
        }
    }
}
