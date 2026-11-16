using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    // The Soundtrack asset is the music MemoryMusic plays for the book.
    public class MemorySoundtrackTests
    {
        [Test] public void TheShippingSoundtrackLoopsChildhoodTrimmedOfItsSilences()
        {
            var soundtrack=MemorySoundtrack.Load();Assert.NotNull(soundtrack,"Resources/"+MemorySoundtrack.ResourcePath);
            Assert.IsEmpty(soundtrack.Check());
            Assert.AreEqual(0,soundtrack.Next(-1));var song=soundtrack.songs[0];
            Assert.AreEqual("sb_childhood",song.clip.name);Assert.IsTrue(song.loop,"it loops");Assert.AreEqual(0,soundtrack.Next(0),"and stays");
            Assert.Greater(song.From,1,"the lead-in silence is cut");Assert.Less(song.To,song.clip.length-2,"the faded tail is cut");
            Assert.AreEqual(0,soundtrack.duringPlay,"silent on the walls: the closeness cue is the music there");
        }
        [Test] public void SongsPlayInOrderALoopStaysAndTheListStartsOver()
        {
            var soundtrack=ScriptableObject.CreateInstance<MemorySoundtrack>();
            var clips=Enumerable.Range(0,4).Select(i=>AudioClip.Create("song "+i,44100*3,1,44100,false)).ToArray();
            try
            {
                Assert.AreEqual(-1,soundtrack.Next(-1),"no songs");Assert.IsNotEmpty(soundtrack.Check());
                soundtrack.songs=new[]{Song(clips[0]),Song(null),Song(clips[1]),Song(clips[2],true),Song(clips[3])};
                Assert.AreEqual(0,soundtrack.Next(-1));Assert.AreEqual(2,soundtrack.Next(0),"a song without a clip is skipped");
                Assert.AreEqual(3,soundtrack.Next(2));Assert.AreEqual(3,soundtrack.Next(3),"a loop stays");
                var problems=soundtrack.Check();
                Assert.IsTrue(problems.Any(p=>p.StartsWith("Song 2")&&p.Contains("no clip")));
                Assert.IsTrue(problems.Any(p=>p.StartsWith("Song 5")&&p.Contains("never plays")),"nothing after a loop plays");
                soundtrack.songs=new[]{Song(clips[0]),Song(clips[1])};
                Assert.AreEqual(1,soundtrack.Next(0));Assert.AreEqual(0,soundtrack.Next(1),"after the last, the first");Assert.IsEmpty(soundtrack.Check());
                soundtrack.songs=new[]{Song(clips[0])};Assert.AreEqual(0,soundtrack.Next(0),"one song plays again, looped or not");
            }
            finally{Object.DestroyImmediate(soundtrack);foreach(var clip in clips)Object.DestroyImmediate(clip);}
        }
        [Test] public void TrimsStayInsideTheClip()
        {
            var clip=AudioClip.Create("three seconds",44100*3,1,44100,false);
            try
            {
                var song=Song(clip);song.trimStart=.5f;song.trimEnd=1;Assert.AreEqual(.5f,song.From,1e-4f);Assert.AreEqual(2,song.To,1e-4f);
                song.trimStart=5;song.trimEnd=5;Assert.AreEqual(3,song.From,1e-4f);Assert.AreEqual(3,song.To,1e-4f,"never before From");
            }
            finally{Object.DestroyImmediate(clip);}
        }
        static MemorySoundtrack.Song Song(AudioClip clip,bool loop=false)=>new MemorySoundtrack.Song{clip=clip,loop=loop};
    }
}
