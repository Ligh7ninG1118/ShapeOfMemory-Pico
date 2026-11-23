using UnityEngine;

namespace ShapeOfMemory
{
    // Plays the soundtrack (MemorySoundtrack). It is silent until Begin, which MemoryGame calls the first time the book lies
    // open on its stand. Then the songs play in order. Up is how far the book has risen toward the walls (MemoryBook.Up):
    // the music is heard fully on the stand and on the walls at the soundtrack's Share (duringPlay, less as the cue warms),
    // muffled toward its playCutoff. The fade follows Up and Warm, but never faster than the soundtrack's fades. Faded right
    // out, the song pauses where it is and goes on from there when the book comes down.
    public sealed class MemoryMusic : MonoBehaviour
    {
        public MemorySoundtrack Soundtrack{get;private set;}
        public AudioSource Source{get;private set;}
        public AudioLowPassFilter Muffle{get;private set;}
        public bool Started{get;private set;}
        // The playing song's index in the soundtrack (-1: none).
        public int Song{get;private set;}=-1;
        // 0 the book on its stand, 1 up on the walls.
        public float Up;
        // How warm the closeness cue is, 0 cold to 1 passing (Torch2Cue.Overall.Shown / 4); the music gives way to it.
        public float Warm;
        // Switched off in Settings: it fades out and holds where it is, and goes on from there when switched back on.
        public bool Muted;
        // How far the music is faded in now, 0 to 1 (its volume is the song's times Fade squared).
        public float Fade{get;private set;}
        // Faded right out and paused, waiting for the book to come down.
        public bool Held{get;private set;}
        // Seen playing since the last Play or UnPause: until then a source that is not playing has not finished.
        bool sounding;

        public static MemoryMusic Add(Transform parent,MemorySoundtrack soundtrack)
        {
            var go=new GameObject("Soundtrack");go.transform.SetParent(parent,false);
            var music=go.AddComponent<MemoryMusic>();music.Soundtrack=soundtrack;
            var source=music.Source=go.AddComponent<AudioSource>();
            source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.dopplerLevel=0;source.priority=0;source.bypassReverbZones=true;source.volume=0;
            music.Muffle=go.AddComponent<AudioLowPassFilter>();music.Muffle.cutoffFrequency=MemorySoundtrack.Open;music.Muffle.lowpassResonanceQ=1;music.Muffle.enabled=false;
            if(!soundtrack)Debug.LogWarning("SHAPE_OF_MEMORY_MUSIC missing: Resources/"+MemorySoundtrack.ResourcePath+".asset (MetaHackthone > Pico Final > Soundtrack makes it). No music.");
            else foreach(var problem in soundtrack.Check())Debug.LogWarning("SHAPE_OF_MEMORY_MUSIC "+problem);
            return music;
        }
        public void Begin()
        {
            if(Started)return;Started=true;
            int first=Soundtrack?Soundtrack.Next(-1):-1;
            if(first>=0)Play(first);else Debug.Log("SHAPE_OF_MEMORY_MUSIC begins: no song");
        }
        void Play(int index)
        {
            Song=index;var song=Soundtrack.songs[index];
            Source.clip=song.clip;Source.Play();Source.time=song.From;sounding=false;Held=false;
            Debug.Log($"SHAPE_OF_MEMORY_MUSIC song {index+1} {song.clip.name} {song.From:0.0}-{song.To:0.0} s{(song.loop?" loops":"")}");
        }
        void Update()
        {
            if(!Started||Song<0)return;
            var song=Soundtrack.songs[Song];
            float target=Muted?0:Soundtrack.Share(Up,Warm),dt=Time.unscaledDeltaTime;
            // Off while open (no filter in the chain on the stand); a cutoff this high passes everything anyway.
            float cutoff=Soundtrack.Cutoff(Up);Muffle.enabled=cutoff<MemorySoundtrack.Open*.95f;if(Muffle.enabled)Muffle.cutoffFrequency=cutoff;
            Fade=Mathf.MoveTowards(Fade,target,dt/(target>Fade?Soundtrack.fadeIn:Soundtrack.fadeOut));
            if(Fade<=0&&target<=0){if(!Held){Source.Pause();Held=true;Debug.Log($"SHAPE_OF_MEMORY_MUSIC held at {Source.time:0.0} s");}return;}
            if(Held){Source.UnPause();Held=false;sounding=false;Debug.Log($"SHAPE_OF_MEMORY_MUSIC goes on at {Source.time:0.0} s");}
            Source.volume=song.volume*Fade*Fade;
            if(Source.isPlaying)sounding=true;
            // Finished: played to the clip's end, or to its trimmed end.
            if(sounding&&(!Source.isPlaying||Source.time>=song.To)){int next=Soundtrack.Next(Song);if(next>=0)Play(next);else Song=-1;}
        }
    }
}
