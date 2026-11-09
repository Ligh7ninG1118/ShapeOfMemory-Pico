using System.Linq;
using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    // The Chapters asset is the one place the game's levels are named.
    public class MemoryChaptersTests
    {
        // What ships: four levels, nothing missing or still a placeholder (same as Pico Final > Check chapters).
        [Test] public void TheShippingBookIsComplete()
        {
            var book=MemoryChapters.Load();Assert.NotNull(book,"Resources/"+MemoryChapters.ResourcePath);
            var problems=book.Check();Assert.IsEmpty(problems,string.Join("\n",problems));
        }
        [Test] public void CheckCatchesTheUsualMistakes()
        {
            var shipped=MemoryChapters.Load().chapters;var book=ScriptableObject.CreateInstance<MemoryChapters>();
            var fresh=ScriptableObject.CreateInstance<Torch2Level>();fresh.name="Fresh";fresh.button=Torch2Gym.PlaceholderButton;fresh.riddle=Torch2Gym.PlaceholderRiddle;
            try
            {
                book.chapters=new[]{shipped[0],null,shipped[0]};
                var problems=book.Check();
                Assert.IsTrue(problems.Any(p=>p.Error&&p.Text.Contains("has 3")),"not four");
                Assert.IsTrue(problems.Any(p=>p.Error&&p.Text.Contains("Chapter II is empty")),"an empty slot");
                Assert.IsTrue(problems.Any(p=>p.Error&&p.Text.Contains("Chapter III")&&p.Text.Contains("chapter I too")),"a level twice");

                book.chapters=new[]{shipped[0],shipped[1],shipped[2],fresh};
                problems=book.Check();string at="Chapter IV (Fresh)";
                Assert.IsTrue(problems.Any(p=>p.Error&&p.Text.StartsWith(at+" has no pieces")),"no pieces");
                foreach(var part in new[]{"no name yet","placeholder riddle","no hint","no complete text","not in Resources"})
                    Assert.IsTrue(problems.Any(p=>!p.Error&&p.Text.StartsWith(at)&&p.Text.Contains(part)),part);
                Assert.IsFalse(problems.Any(p=>!p.Text.StartsWith(at)),"the shipped three are fine:\n"+string.Join("\n",problems));

                var twoWalls=shipped.FirstOrDefault(l=>l.lamps.Length>1);
                if(twoWalls)
                {
                    book.chapters=new[]{twoWalls}.Concat(shipped.Where(l=>l!=twoWalls)).ToArray();
                    Assert.IsTrue(book.Check().Any(p=>!p.Error&&p.Text.Contains("chapter I teaches")),"a two-wall first chapter");
                }
            }
            finally{Object.DestroyImmediate(book);Object.DestroyImmediate(fresh);}
        }
    }
}
