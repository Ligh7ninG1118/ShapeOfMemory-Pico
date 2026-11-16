using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    // The Credits asset is what the book's last spread prints.
    public class MemoryCreditsTests
    {
        [Test] public void TheShippingCreditsNameTheTeam()
        {
            var credits=MemoryCredits.Load();Assert.NotNull(credits,"Resources/"+MemoryCredits.ResourcePath);
            Assert.IsNotEmpty(credits.Names.ToArray());Assert.IsFalse(string.IsNullOrWhiteSpace(credits.madeBy));
        }
        [Test] public void CheckNamesWhatStillPrintsAsAPlaceholder()
        {
            var credits=ScriptableObject.CreateInstance<MemoryCredits>();
            try
            {
                credits.sections=new[]{new MemoryCredits.Section{heading="Music",lines=new[]{" "}}};
                Assert.IsTrue(credits.Check().Any(p=>p.StartsWith("Music")),"a section with only blank lines");
                credits.sections[0].lines=new[]{"A song, by someone"};
                Assert.IsEmpty(credits.Check());
                credits.names=new[]{"",null};Assert.IsTrue(credits.Check().Any(p=>p.StartsWith("No names")));
            }
            finally{Object.DestroyImmediate(credits);}
        }
    }
}
