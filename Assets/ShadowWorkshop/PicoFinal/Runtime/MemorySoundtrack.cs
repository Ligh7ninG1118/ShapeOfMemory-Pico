using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShapeOfMemory
{
    // The soundtrack: Resources/ShapeOfMemory/Soundtrack.asset (MetaHackthone > Pico Final > Soundtrack). MemoryMusic plays
    // it from the moment the book first lies open. The songs play in list order. A song that loops plays again and again,
    // so the songs after it never come. A song that doesn't loop hands over to the next with a clip, and after the last the
    // list starts over.
    // The music belongs to the book. It fades out as the book rises onto the walls for a memory, because there the closeness
    // cue (Torch2Cue, its own layered music, the only feedback) has to be heard. It comes back as the book comes down,
    // picking up where it left off. duringPlay keeps some of it under the cue instead.
    // Songs go in Audio/Music. Import them with Load Type Streaming: a few minutes decoded into memory would be tens of MB.
    [CreateAssetMenu(menuName="Shape of Memory/Soundtrack")]
    public sealed class MemorySoundtrack : ScriptableObject
    {
        public const string ResourcePath="ShapeOfMemory/Soundtrack";

        [Serializable]
        public sealed class Song
        {
            public AudioClip clip;
            [Tooltip("Play it again and again. The songs after it in the list never play.")]
            public bool loop;
            [Range(0,1)] public float volume=.4f;
            [Tooltip("Seconds cut from the start (silence before the music). Every play, a loop's included, starts here.")]
            [Min(0)] public float trimStart;
            [Tooltip("Seconds cut from the end (a tail faded past hearing, or silence). The next play comes that much sooner.")]
            [Min(0)] public float trimEnd;
            // The part that plays, in seconds of the clip.
            public float From=>clip?Mathf.Clamp(trimStart,0,clip.length):0;
            public float To=>clip?Mathf.Clamp(clip.length-trimEnd,From,clip.length):0;
        }

        public Song[] songs=Array.Empty<Song>();
        [Tooltip("How much of the music stays while a memory is out on the walls, as a share of each song's volume. 0: none, and the closeness cue is the level's music.")]
        [Range(0,1)] public float duringPlay=0;
        [Tooltip("Seconds for the music to come up: the first time (with the sky fading in) and as the book comes down.")]
        [Min(.05f)] public float fadeIn=2.5f;
        [Tooltip("The fastest it goes down, in seconds from full to none, as the book rises.")]
        [Min(.05f)] public float fadeOut=1.2f;

        public static MemorySoundtrack Load()=>Resources.Load<MemorySoundtrack>(ResourcePath);
        bool Playable(int index)=>songs!=null&&index>=0&&index<songs.Length&&songs[index]!=null&&songs[index].clip;
        // The song after index (-1: the first): the same one while it loops, else the next one with a clip, wrapping round
        // the list. -1 if no song has a clip.
        public int Next(int index)
        {
            if(Playable(index)&&songs[index].loop)return index;
            int count=songs?.Length??0;
            for(int step=1;step<=count;step++){int i=((index+step)%count+count)%count;if(Playable(i))return i;}
            return -1;
        }

        // What will not play as the list suggests. None of it stops a build; an empty soundtrack is silence.
        public List<string> Check()
        {
            var problems=new List<string>();
            if(Next(-1)<0){problems.Add("No song has a clip: there is no music.");return problems;}
            int looping=-1;
            for(int i=0;i<songs.Length;i++)
            {
                var song=songs[i];string at=$"Song {i+1}"+(song?.clip?$" ({song.clip.name})":"");
                if(!Playable(i)){problems.Add(at+" has no clip: it is skipped.");continue;}
                if(looping>=0){problems.Add($"{at} never plays: song {looping+1} before it loops.");continue;}
                if(song.To-song.From<1)problems.Add($"{at} is trimmed to under a second.");
                if(song.volume<=0)problems.Add(at+" has volume 0.");
                if(song.loop)looping=i;
            }
            return problems;
        }
    }
}
