using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShapeOfMemory
{
    // The credits, the book's last spread (after THANK YOU): Resources/ShapeOfMemory/Credits.asset (MetaHackthone > Pico
    // Final > Credits). Edit the words and names there; the pages are printed from it each time the spread is shown.
    // - Left page: madeBy over a gilt rule, the names in two columns (down the left one first, in list order, by family
    //   name).
    // - Right page: thanksTo over a gilt rule, then each section: its heading in gilt capitals, its lines under it.
    // What is missing keeps its space: a section with no lines prints "to come". Check() lists that and an empty team
    // (the inspector shows them, a build logs them); neither stops a build.
    [CreateAssetMenu(menuName="Shape of Memory/Credits")]
    public sealed class MemoryCredits : ScriptableObject
    {
        public const string ResourcePath="ShapeOfMemory/Credits";
        // Printed under a section with no lines yet.
        public const string LinesPlaceholder="to come";

        [Serializable]
        public sealed class Section
        {
            [Tooltip("Printed in gilt capitals, e.g. MUSIC & SOUND.")]
            public string heading="";
            [Tooltip("One line each: what it is and who made it (and its licence, where the licence asks to be named). Empty lines are skipped.")]
            public string[] lines=Array.Empty<string>();
            public IEnumerable<string> Lines=>(lines??Array.Empty<string>()).Where(l=>!string.IsNullOrWhiteSpace(l));
        }

        [Tooltip("Left page: the line over the names.")]
        public string madeBy="Made with love by";
        [Tooltip("The team, one name per entry, printed in this order (alphabetical by family name) in two columns: down the left one first, which takes the odd one out.")]
        public string[] names={"Anran Gong","Chenghao Li","Yiyang Li","Lei Qian","Dan Qiao","Ruizhe Shen"};
        [Tooltip("Right page: the line over the sections.")]
        public string thanksTo="With thanks to";
        [Tooltip("Right page, top to bottom: what others made that the game uses. The text shrinks to fit if the list grows long.")]
        public Section[] sections={new Section{heading="Music & Sound"},new Section{heading="Art & Assets"},new Section{heading="Tools"}};

        public static MemoryCredits Load()=>Resources.Load<MemoryCredits>(ResourcePath);
        public IEnumerable<string> Names=>(names??Array.Empty<string>()).Where(n=>!string.IsNullOrWhiteSpace(n));
        public IEnumerable<Section> Sections=>(sections??Array.Empty<Section>()).Where(s=>s!=null);
        // Every character the credits can print (headings in capitals too): the font bake and its test read it.
        public string Printed()=>string.Join("\n",new[]{madeBy,thanksTo}.Concat(Names)
            .Concat(Sections.SelectMany(s=>new[]{s.heading??"",(s.heading??"").ToUpperInvariant()}.Concat(s.Lines))));

        // What still shows as a placeholder or prints nothing. None of it breaks the book.
        public List<string> Check()
        {
            var problems=new List<string>();
            if(!Names.Any())problems.Add($"No names: the left page only says \"{madeBy}\".");
            if(!Sections.Any())problems.Add($"No sections: the right page only says \"{thanksTo}\".");
            foreach(var section in Sections)
            {
                string name=string.IsNullOrWhiteSpace(section.heading)?"A section with no heading":section.heading;
                if(!section.Lines.Any())problems.Add($"{name} has no lines yet: it prints \"{LinesPlaceholder}\".");
            }
            return problems;
        }
    }
}
