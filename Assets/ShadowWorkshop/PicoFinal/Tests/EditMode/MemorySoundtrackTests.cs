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
            Assert.Greater(soundtrack.duringPlay,0,"some music stays on the walls (playtest 2026-11-16)");
            Assert.Less(soundtrack.duringPlay,.6f,"well under the closeness cue");Assert.Less(soundtrack.playCutoff,2000,"and muffled below it");
            Assert.Greater(soundtrack.warmDuck,0,"giving way as the cue warms");
        }
        [Test] public void OnTheWallsTheMusicStaysUnderTheCueAndGivesWayAsItWarms()
        {
            var s=ScriptableObject.CreateInstance<MemorySoundtrack>();
            try
            {
                s.duringPlay=.4f;s.warmDuck=.7f;s.playCutoff=900;
                Assert.AreEqual(1,s.Share(0,1),1e-5f,"on the stand: all of it, however warm");
                Assert.AreEqual(.4f,s.Share(1,0),1e-5f,"on the walls and cold: duringPlay");
                Assert.AreEqual(.12f,s.Share(1,1),1e-5f,"passing: warmDuck of that gone");
                Assert.Less(s.Share(1,.5f),s.Share(1,.25f),"warmer, quieter");
                Assert.AreEqual(MemorySoundtrack.Open,s.Cutoff(0),.01f,"open on the stand");
                Assert.AreEqual(900,s.Cutoff(1),.01f,"muffled on the walls");
                Assert.AreEqual(Mathf.Sqrt(900*MemorySoundtrack.Open),s.Cutoff(.5f),1,"half way up, half way in octaves");
                s.duringPlay=0;Assert.AreEqual(0,s.Share(1,0),"0: the old silence");
            }
            finally{Object.DestroyImmediate(s);}
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
