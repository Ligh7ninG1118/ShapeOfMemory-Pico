using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // The found moment's own part on the level (MemoryFound is the shared plaque over the orb): while the orb glides into
    // the answer (Torch2Workshop's lock, or a snap) a harp run swells so its top lands with the orb; once the memory is found, the
    // chapter's MemorySolveCue plays: its sounds, and its paper cut-outs on the lit pages round the shadow. The cut-outs are
    // one mesh from one atlas (PageCue shader, Resources/ShapeOfMemory/Page cue.mat), rebuilt each frame (a few dozen quads),
    // dimmed off the lamp's spot and with the lamps (the outro puts them out). It lives on the level, so leaving the level
    // ends it.
    public sealed class MemorySolveMoment : MonoBehaviour
    {
        public const string MaterialPath="ShapeOfMemory/Page cue";
        // How far in front of the wall the cut-outs sit: behind the shadow (drawn .003 out) or over it.
        public const float BehindShadow=.0018f,OverShadow=.0055f;
        // The moment's end: everything fades over EndFade before cue.seconds.
        public const float EndFade=.8f,SwellVolume=.35f;
        const int W=Torch2Projection.Width,H=Torch2Projection.Height;

        public MemorySolveCue Cue{get;private set;}
        public bool Playing{get;private set;}
        // Tests and captures: Frozen holds the cut-outs where they are; Seek shows them that many seconds into the moment
        // (replayed from its start in 1/60 s steps with the same seed, so the same every time; no sounds).
        public bool Frozen;
        public float Time=>pageTime;
        // Cut-outs showing now (tests, logs).
        public int Showing{get;private set;}
        Torch2Workshop game;
        AudioClip swell;
        AudioSource voice,swellVoice;
        bool swelling,wasLocking,found,paging;
        // Seconds since the memory was found (sounds), and since the cut-outs began (pages).
        float time,pageTime;
        int nextSound;
        MemorySolveCue.Sound[] sounds;
        // Per lit page (slot): the spot's middle (wall metres) and radius, from the lamp's light.
        Vector2[] spotMiddle;float[] spotRadius;
        readonly List<Piece> pieces=new List<Piece>();
        Mesh mesh;MeshRenderer view;
        Vector3[] vertices;Color[] colours;Vector2[] uvs,pageUvs;
        const int Seed=7;
        System.Random random=new System.Random(Seed);

        sealed class Piece
        {
            public MemorySolveCue.Layer layer;public int slot,index;
            public float born,life,size,spin,turn,sway;public bool alive,flip,started;
            public Vector2 at,start,velocity;public Color tint;
        }

        public static MemorySolveMoment Add(Torch2Workshop game,MemorySolveCue cue,AudioClip swell)
        {
            var moment=game.gameObject.AddComponent<MemorySolveMoment>();moment.game=game;moment.Cue=cue;moment.swell=swell;
            moment.voice=game.gameObject.AddComponent<AudioSource>();moment.voice.playOnAwake=false;moment.voice.spatialBlend=0;
            moment.swellVoice=game.gameObject.AddComponent<AudioSource>();moment.swellVoice.playOnAwake=false;moment.swellVoice.spatialBlend=0;
            Debug.Log("SHAPE_OF_MEMORY_CUE "+game.level.name+" "+(cue?$"{cue.name}: {cue.layers.Length} layers, {cue.sounds.Length} sounds":"none (shared moment only)"));
            return moment;
        }

        void Update()
        {
            if(!game)return;
            Swell();
            if(!found&&game.Completed){found=true;Begin();}
            if(!Playing||Frozen)return;
            float dt=UnityEngine.Time.unscaledDeltaTime;time+=dt;
            while(nextSound<sounds.Length&&time>=sounds[nextSound].delay)
            {
                var s=sounds[nextSound++];if(s.clip)voice.PlayOneShot(s.clip,s.volume);
            }
            // The cut-outs start once the picture stands the right way up: a memory found flipped is turned upright
            // first (Torch2Workshop.Righting), and a cut-out set on the candles would float off the turning cake.
            if(!paging&&Cue&&Cue.layers.Length>0&&!game.Righting){paging=true;pageTime=0;Spots();Spawn();Build();Debug.Log($"SHAPE_OF_MEMORY_CUE {game.level.name} playing {pieces.Count} cut-outs over {Cue.seconds:0.0} s, {time:0.00} s after the find");}
            if(paging&&pageTime<Cue.seconds){pageTime+=dt;Animate(dt);}
            else if(view&&view.enabled){view.enabled=false;Showing=0;}
            bool pagesDone=!Cue||Cue.layers.Length==0||paging&&pageTime>=Cue.seconds;
            if(nextSound>=sounds.Length&&pagesDone)Playing=false;
        }
        // The harp run while the orb glides in (a lock, Torch2Workshop.HoldToSolve, or a snap), so its top comes as the orb
        // lands: it starts once the landing is SwellPeak away, part way through when the whole glide is shorter. A hand
        // taking the orb back (a release snap can be caught) or a cancelled lock (a pause) cuts it.
        void Swell()
        {
            bool coming=(game.Locking||game.Snapping)&&!game.Completed;
            float left=game.Locking?game.LockRemaining:(1-game.SnapProgress)*game.SnapLength;
            if(coming&&!swelling&&swell&&left<=MemorySounds.SwellPeak)
            {
                swelling=true;swellVoice.clip=swell;swellVoice.volume=SwellVolume;
                swellVoice.time=Mathf.Clamp(MemorySounds.SwellPeak-left,0,swell.length-.05f);swellVoice.Play();
            }
            if(!coming&&swelling&&!game.Completed&&(game.Busy||wasLocking))swellVoice.Stop();
            if(!coming)swelling=false;
            wasLocking=game.Locking;
        }
        void Begin()
        {
            sounds=Cue?Cue.sounds:System.Array.Empty<MemorySolveCue.Sound>();
            System.Array.Sort(sounds=(MemorySolveCue.Sound[])sounds.Clone(),(a,b)=>a.delay.CompareTo(b.delay));
            Playing=true;time=0;nextSound=0;
        }
        public void Seek(float seconds)
        {
            if(!paging||!view)return;
            random=new System.Random(Seed);pieces.Clear();Spawn();pageTime=0;
            while(pageTime<seconds-1e-4f){float dt=Mathf.Min(1/60f,seconds-pageTime);pageTime+=dt;Animate(dt);}
            if(seconds<=0)Animate(0);
        }
        // The lamp's spot on each lit page: the middle and radius of where its light is half or more.
        void Spots()
        {
            var p=game.Projection;int n=p.walls.Length;spotMiddle=new Vector2[n];spotRadius=new float[n];
            for(int j=0;j<n;j++)
            {
                var bright=p.brightness[j];int count=0;double sx=0,sy=0;
                for(int y=0;y<H;y++)for(int x=0;x<W;x++)if(bright[y*W+x]>=.5f){count++;sx+=x+.5;sy+=y+.5;}
                if(count==0){spotMiddle[j]=new Vector2(W,H)*(.5f*Torch2Projection.Pixel);spotRadius[j]=.4f;continue;}
                spotMiddle[j]=new Vector2((float)(sx/count),(float)(sy/count))*Torch2Projection.Pixel;spotRadius[j]=Mathf.Sqrt(count/Mathf.PI)*Torch2Projection.Pixel;
            }
        }
        float Range(Vector2 r)=>Mathf.Lerp(r.x,r.y,(float)random.NextDouble());
        float Random01()=>(float)random.NextDouble();
        void Spawn()
        {
            int slots=game.Projection.walls.Length;
            foreach(var layer in Cue.layers)
            {
                if(layer==null||layer.count<1)continue;
                for(int slot=0;slot<slots;slot++)
                {
                    if(layer.wall>=0&&layer.wall!=slot)continue;
                    int count=layer.spawn==MemorySolveCue.Spawn.Points?Mathf.Min(layer.count,layer.points.Length):layer.count;
                    for(int i=0;i<count;i++)
                    {
                        var piece=new Piece{layer=layer,slot=slot,index=i};
                        float order=count>1?i/(float)(count-1):0;
                        piece.born=layer.delay+layer.stagger*(layer.spawn==MemorySolveCue.Spawn.Random?Random01():order);
                        piece.tint=layer.tints!=null&&layer.tints.Length>0?layer.tints[i%layer.tints.Length]:Color.white;
                        Restart(piece);pieces.Add(piece);
                    }
                }
            }
        }
        // Where and how a cut-out (re)starts: its place, its size, its motion.
        void Restart(Piece piece)
        {
            var l=piece.layer;int count=l.spawn==MemorySolveCue.Spawn.Points?Mathf.Min(l.count,l.points.Length):l.count;float order=count>1?piece.index/(float)(count-1):0;
            switch(l.spawn)
            {
                case MemorySolveCue.Spawn.Line:piece.start=Vector2.Lerp(l.from,l.to,order);break;
                case MemorySolveCue.Spawn.Points:piece.start=l.points[piece.index];break;
                default:piece.start=new Vector2(Mathf.Lerp(l.from.x,l.to.x,Random01()),Mathf.Lerp(l.from.y,l.to.y,Random01()));break;
            }
            piece.at=piece.start;piece.life=Mathf.Max(.1f,Range(l.life));piece.size=Range(l.size);
            if(l.until>0&&!l.loop)piece.life=Mathf.Max(.05f,l.until-piece.born);
            piece.velocity=l.velocity*(1+l.jitter*(Random01()*2-1));
            piece.spin=Range(l.spin)*(Random01()<.5f?-1:1);
            float line=l.alongLine?Mathf.Atan2(l.to.y-l.from.y,l.to.x-l.from.x)*Mathf.Rad2Deg:0;
            piece.turn=line+l.angle+l.tilt*(Random01()*2-1);piece.sway=Random01()*Mathf.PI*2;
            piece.flip=l.flipRandom&&Random01()<.5f;piece.started=false;piece.alive=true;
        }
        void Build()
        {
            var go=new GameObject("Solve cue",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(game.transform,false);
            mesh=new Mesh{name="Solve cue"};mesh.MarkDynamic();int n=pieces.Count;
            vertices=new Vector3[n*4];colours=new Color[n*4];uvs=new Vector2[n*4];pageUvs=new Vector2[n*4];var triangles=new int[n*6];
            for(int i=0;i<n;i++){int v=i*4,t=i*6;triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;triangles[t+3]=v;triangles[t+4]=v+3;triangles[t+5]=v+2;}
            mesh.vertices=vertices;mesh.colors=colours;mesh.uv=uvs;mesh.uv2=pageUvs;mesh.triangles=triangles;
            var size=Torch2Room.WallSize;mesh.bounds=new Bounds(Torch2Projection.Corner+new Vector3(0,size.y*.5f,0),new Vector3(size.x*2.2f,size.y*1.2f,size.x*2.2f));
            go.GetComponent<MeshFilter>().sharedMesh=mesh;view=go.GetComponent<MeshRenderer>();
            var material=Resources.Load<Material>(MaterialPath);if(!material)Debug.LogError("SHAPE_OF_MEMORY_CUE missing Resources/"+MaterialPath+".mat");
            view.sharedMaterial=material;view.shadowCastingMode=ShadowCastingMode.Off;view.receiveShadows=false;view.lightProbeUsage=LightProbeUsage.Off;view.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
        void Animate(float dt)
        {
            var p=game.Projection;var size=Torch2Room.WallSize;bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            float lamps=game.world?game.world.LampLevel:1,end=1-Mathf.Clamp01((pageTime-(Cue.seconds-EndFade))/EndFade);int showing=0;
            for(int i=0;i<pieces.Count;i++)
            {
                var piece=pieces[i];var l=piece.layer;float age=pageTime-piece.born;
                if(age>=piece.life&&piece.alive){if(l.loop){piece.born=pageTime;Restart(piece);age=0;}else piece.alive=false;}
                if(!piece.alive||age<0){Hide(i);continue;}
                // Motion (spot radii): gravity down, drag, and a wrap across the box for drifting things.
                if(piece.started)
                {
                    piece.velocity+=Vector2.down*(l.gravity*dt);if(l.drag>0)piece.velocity*=Mathf.Exp(-l.drag*dt);piece.at+=piece.velocity*dt;
                    if(l.wrap){float lo=Mathf.Min(l.from.x,l.to.x),hi=Mathf.Max(l.from.x,l.to.x),span=hi-lo;if(span>1e-3f){if(piece.at.x<lo)piece.at.x+=span;else if(piece.at.x>hi)piece.at.x-=span;}}
                }
                piece.started=true;
                var at=piece.at;if(l.sway!=0)at.x+=l.sway*Mathf.Sin(piece.sway+age*l.swayRate*Mathf.PI*2);
                float grow=l.pop>0?Mathf.Clamp01(age/l.pop):1;grow=1+2.2f*Mathf.Pow(grow-1,3)+1.2f*Mathf.Pow(grow-1,2);
                float fade=l.fadeOut>0?Mathf.Clamp01((piece.life-age)/l.fadeOut):1;if(l.loop&&!l.wrap)fade=Mathf.Min(fade,Mathf.Clamp01(age/Mathf.Max(.01f,l.pop)));
                float flicker=l.flicker>0?1+l.flicker*(Mathf.PerlinNoise(piece.sway*3,age*7)-.5f)*2:1;
                int slot=piece.slot,k=p.walls[slot];float r=spotRadius[slot];
                var wall=spotMiddle[slot]+at*r;
                int px=Mathf.Clamp((int)(wall.x/Torch2Projection.Pixel),0,W-1),py=Mathf.Clamp((int)(wall.y/Torch2Projection.Pixel),0,H-1);
                // Off the spot a cut-out fades into the page (paper in the dark is as dark as the page), and dims a little
                // through the spot's soft edge. Tints are sRGB; vertex colours reach the shader as they are, so linearise.
                float bright=p.brightness[slot][py*W+px],light=Mathf.Lerp(l.inDark,1,bright),shade=Mathf.Lerp(.7f,1,bright);
                var c=linear?piece.tint.linear:piece.tint;c.a=piece.tint.a*fade*end*lamps*light;c.r*=shade;c.g*=shade;c.b*=shade;
                if(c.a<=.002f){Hide(i);continue;}
                showing++;
                float half=piece.size*r*.5f*Mathf.Max(0,grow)*flicker,angle=(piece.turn+piece.spin*age)*Mathf.Deg2Rad;
                Vector3 right=Torch2Projection.WallRight[k],up=Vector3.up,normal=Torch2Projection.WallNormal[k];
                var across=(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*half;var upward=(-right*Mathf.Sin(angle)+up*Mathf.Cos(angle))*half;
                if(piece.flip)across=-across;
                var middle=Torch2Projection.WallOrigin[k]+right*wall.x+up*wall.y+normal*(l.depth==MemorySolveCue.Depth.Behind?BehindShadow:OverShadow);
                var cell=MemorySolveCue.Cell(l.art);int v=i*4;
                vertices[v]=middle-across-upward;vertices[v+1]=middle+across-upward;vertices[v+2]=middle+across+upward;vertices[v+3]=middle-across+upward;
                uvs[v]=new Vector2(cell.xMin,cell.yMin);uvs[v+1]=new Vector2(cell.xMax,cell.yMin);uvs[v+2]=new Vector2(cell.xMax,cell.yMax);uvs[v+3]=new Vector2(cell.xMin,cell.yMax);
                for(int q=0;q<4;q++)
                {
                    colours[v+q]=c;
                    var o=vertices[v+q]-Torch2Projection.WallOrigin[k];pageUvs[v+q]=new Vector2(Vector3.Dot(o,right)/size.x,o.y/size.y);
                }
            }
            Showing=showing;
            view.enabled=showing>0;
            mesh.vertices=vertices;mesh.colors=colours;mesh.uv=uvs;mesh.uv2=pageUvs;
        }
        void Hide(int i){int v=i*4;for(int q=0;q<4;q++){vertices[v+q]=Vector3.zero;colours[v+q]=Color.clear;}}
    }
}
