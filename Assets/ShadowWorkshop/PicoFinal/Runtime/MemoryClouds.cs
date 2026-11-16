using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace ShapeOfMemory
{
    // Stylised cumulus round the player once the sky is up (the user's reference, 2026-11-12: a table floating over a sea
    // of dusk clouds, banks and towers either side, the glow beyond). Meshes from Tools/ShadowArt/build_cloud_kit.py
    // (Art/Clouds/CloudKit.fbx: Sea_*, Puff_*, Bank_*, each with a _LOD1 and a _LOD2; Tower_* would count as mid), all on one opaque
    // material (Shaders/Cloud.shader), so each costs about one shaded pixel where it shows and hides the sky behind it.
    // Space: cloud space is the stage's, moved down onto the floor every frame (LOWER / RAISE and recentring keep the sea
    // where it was under the feet). The eyes are over Eye.
    // Bands, out from the eyes (Layout):
    //   Sea: flat banks whose tops make the sea, SeaTop under the floor (Drop: the reference's floating table; or
    //        UnderFeet: the player stands on it), out to SeaFar, bigger with distance, gaps showing the haze below.
    //   Puffs: cumulus rising out of the sea PuffNear..PuffFar away. Nothing rises above Ceiling(its near edge's
    //        distance): under the floor within KeepOut, then ClimbPerMetre higher per metre further out.
    //   Mid: big cumulus MidNear..MidFar away, framing the view: puffs scaled up (MidPuff of them) and banks (towers
    //        only past MidBank, and the kit has none: they read as stone pillars); none within GlowGap of the glow (MemorySky.Facing), so the sky's glow stays
    //        open, and none whose drift passes nearer than MidNear. A Mid spot's piece may be a puff's mesh.
    //   Ruins: classical stone from Tools/ShadowArt/build_ruin_kit.py (Art/Ruins/RuinKit.fbx: Ruin_*, on their own
    //        material, Materials/Memory ruin.mat: the cloud shader with a stone palette), RuinNear to RuinFar away, spread
    //        evenly in distance (a Halton sequence) and all the way round (a golden-angle spiral, behind the player
    //        too); every piece once, then pillars, columns and arches twice as often as the rest (Piece.Weight). They stand in the sea
    //        with their bases sunk (the haze swallows them), turned up to RuinTurn off facing the eyes; up to RuinFloaters
    //        broken pieces float above the sea, tilted, bobbing. Clear of the glow by only RuinGlowGap: the reference has
    //        ruins by the sun. None rises more than RuinTopElevation over the horizon (from
    //        eyes EyeHeight over the floor): a standing one that would sinks deeper, up to RuinDeepSink of its height, so
    //        they frame the horizon rather than tower over the book.
    // Motion: sea and puffs sway in place, each on its own period (a shared drift under the feet reads as the player
    // moving); banks and towers drift with the wind (Wind m/s, under a degree a second at 50 m). One that drifts past
    // MidFar sinks into the sea over SinkSeconds, moves to its mirror image upwind (same distance) and rises there.
    // Reveal: hidden until the sky starts to show, then they fade in with it over passthrough (_CloudHide = 1 - the
    // sky's alpha, then 0). One material and one shader variant throughout: no swap, no dissolve.
    public sealed class MemoryClouds : MonoBehaviour
    {
        public enum Kind{Sea,Puff,Mid,Ruin}
        public const int Seed=5,SeaCount=80,PuffCount=12,MidCount=14,Tries=8000;
        // Where the sea's tops sit under the floor.
        public const float Drop=8,UnderFeet=.3f;
        // Mid band mix: this share are puffs (scaled MidPuffScale), then banks up to MidBank, the rest towers.
        public const float MidPuff=.6f,MidBank=1;
        public static readonly Vector2 MidPuffScale=new Vector2(2.6f,4.4f),MidBankScale=new Vector2(1,1.6f),MidTowerScale=new Vector2(.7f,1.1f);
        public const float SeaFar=170,PuffNear=10,PuffFar=70,MidNear=45,MidFar=175,KeepOut=8,ClimbPerMetre=.8f,GlowGap=16;
        // Detail tiers (Lod). The sea's by distance: seen flat from above and against more sea, they hide coarse outlines.
        // Puffs and big clouds by how wide they look (degrees): they stand against the sky, often against the glow, where
        // a coarse outline shows, and the big ones are wide even far away.
        public static readonly Vector2 SeaLods=new Vector2(30,150),WideLods=new Vector2(8,3);
        public const float Wind=.7f,SinkSeconds=25,SwaySea=.4f,SwayPuff=.9f,SwayPeriodMin=40,SwayPeriodMax=80,Wobble=1.5f;
        // The haze takes the clouds over from SeaClear under the sea's top down to BaseFade below that (Cloud.shader's
        // _CloudBase), so the sea's lumps read and its underside melts into the sky.
        public const float SeaClear=1.5f,BaseFade=4.5f;
        public static readonly Vector3 WindDirection=new Vector3(1,0,.3f).normalized;
        // Ruins: how many, where, how big, how far off facing the eyes, how deep their bases sink (share of their height);
        // floaters: how many at most, how high over the sea's tops, tilt, bob (metres) and its period.
        public const int RuinCount=24,RuinFloaters=5;
        public const float RuinNear=45,RuinFar=280,RuinGlowGap=6,RuinTurn=70,FloatTilt=15,FloatBob=.6f,RuinTopElevation=14,RuinDeepSink=.6f,EyeHeight=1.5f;
        public static readonly Vector2 RuinScale=new Vector2(1,1.8f),RuinSink=new Vector2(.2f,.4f),FloatAbove=new Vector2(4,18),FloatPeriod=new Vector2(30,50);
        // The eyes over the floor (cloud space), where the stage is placed from (MemoryGame.Recenter).
        public static Vector2 Eye=>new Vector2(0,-MemoryGame.StageAhead);
        // The highest a cloud may reach (cloud space y) whose nearest edge is distance metres from the eyes.
        public static float Ceiling(float distance)=>-1+Mathf.Max(0,distance-KeepOut)*ClimbPerMetre;
        public static float SeaTop(bool underFeet)=>underFeet?-UnderFeet:-Drop;
        // The sea's top at p (cloud space x, z) under a swell: seaTop up or down by up to shape.SeaSwell along slow Perlin
        // noise (SwellLength from crest to crest), but never above the level just under the feet.
        public static float SwellTop(float seaTop,Shape shape,Vector3 p)
        {
            if(shape.SeaSwell<=0)return seaTop;
            float length=Mathf.Max(shape.SwellLength,1);
            float wave=Mathf.Clamp(Mathf.PerlinNoise(p.x/length+31.7f,p.z/length+17.3f)*2-1,-1,1);
            return Mathf.Min(seaTop+shape.SeaSwell*wave,Mathf.Max(seaTop,-UnderFeet));
        }

        public struct Piece
        {
            // Floats: a ruin that may hang above the sea (the broken ones). Weight: how often it comes up among its kind.
            public Kind Kind;public Vector3 Size;public bool Floats;public float Weight;
            public Piece(Kind kind,Vector3 size,bool floats=false,float weight=1){Kind=kind;Size=size;Floats=floats;Weight=weight;}
        }
        public struct Spot
        {
            // Lod: 0 the near mesh, 1 its _LOD1, 2 its _LOD2.
            public Kind Kind;public int Piece,Lod;
            // Middle of its base (cloud space), turn about the vertical, scale; Top is its highest point (cloud space y),
            // Reach its footprint's radius (metres), Distance from the eyes to its middle (horizontal).
            public Vector3 Position;public float Yaw,Scale,Top,Reach,Distance;
            public float Period,Phase,Phase2;
            // A ruin hanging above the sea, and its lean (degrees about x and z).
            public bool Floating;public Vector3 Tilt;
        }

        // How the clouds lie (MemoryLook.CloudShape). Flat: the sea's tops level (within half a metre), as at cs:158.
        [Serializable]
        public struct Shape
        {
            [Tooltip("Metres the sea's tops rise and fall in long, gentle swells (Perlin noise), and what stands in the sea with them. 0: level. Under the feet, it only falls.")]
            public float SeaSwell;
            [Tooltip("Metres from one swell to the next.")]
            public float SwellLength;
            [Tooltip("How many puffs float clear of the sea, LiftedHeight over its tops (MidNear to MidFar away, out of the glow).")]
            public int Lifted;
            public Vector2 LiftedHeight;
            public static Shape Flat=>new Shape{SwellLength=60,LiftedHeight=new Vector2(5,14)};
        }
        // How the ruins stand (MemoryLook.Ruins). Standing: upright in the sea, a few broken ones afloat 4-18 m up (cs:158).
        [Serializable]
        public struct RuinLayout
        {
            [Tooltip("How many ruins.")]
            public int Count;
            [Tooltip("How many at most float over the sea (tilted, bobbing).")]
            public int Floaters;
            [Tooltip("Metres over the sea's tops a floating one hangs.")]
            public Vector2 FloatAbove;
            [Tooltip("Degrees a standing one may lean (our kit: a random lean; the environment artist's models: their instance's own, at most this). 0: upright.")]
            public float Lean;
            [Tooltip("Any of our kit's pieces may float, not only the broken ones (the environment artist's models always may).")]
            public bool AnyFloats;
            public static RuinLayout Standing=>new RuinLayout{Count=RuinCount,Floaters=RuinFloaters,FloatAbove=MemoryClouds.FloatAbove};
        }

        sealed class Cloud
        {
            public Transform holder,model;public Renderer renderer;public Spot spot;public bool isModel;
            public Vector3 drift;public int sink;public float sinkTime,depth;
        }

        public bool UnderFeetMode{get;private set;}
        public int Count=>clouds.Length;
        public bool Revealed=>revealed;
        // The shader's fade: 0 shown, 1 hidden (global, read by every cloud).
        public static float Hidden=>Shader.GetGlobalFloat(HideId);
        public IEnumerable<Renderer> Renderers=>clouds.Select(c=>c.renderer);
        Cloud[] clouds=new Cloud[0];
        MemorySky sky;
        Transform stage;
        Func<float> floor;
        Material material;
        float seaTop;
        bool shown,revealed,borrowing;
        Shape shape=Shape.Flat;
        RuinLayout layout=RuinLayout.Standing;
        Func<Transform> environment;
        static readonly int HideId=Shader.PropertyToID("_CloudHide"),BaseId=Shader.PropertyToID("_CloudBase"),BaseFadeId=Shader.PropertyToID("_CloudBaseFade");

        // The look's clouds and ruins (MemoryLook). environment: the environment scene's root once loaded (null until then,
        // or always without one): ruin models borrow their bake from it.
        public static MemoryClouds Add(Transform stage,MemoryLook look,MemorySky sky,Func<float> floor,Func<Transform> environment,List<Object> owned)=>
            Add(stage,look.CloudPieces,look.CloudMaterial,look.RuinPieces,look.RuinMaterial,look.RuinModels,look.CloudShape,look.Ruins,sky,floor,look.CloudsUnderFeet,environment,owned);
        // pieces: the cloud kit's meshes (names Sea_*, Puff_*, Tower_*, Bank_*, and each one's *_LOD1, *_LOD2), on material.
        // ruins: the ruin kit's (Ruin_*), on ruinMaterial (none without it). models: the environment artist's ruin prefabs,
        // each on its own material, sized to ModelHeight (ModelWidth at most across). floor: the floor's world height.
        public static MemoryClouds Add(Transform stage,IEnumerable<Mesh> pieces,Material material,IEnumerable<Mesh> ruins,Material ruinMaterial,IEnumerable<GameObject> models,
            Shape shape,RuinLayout layout,MemorySky sky,Func<float> floor,bool underFeet,Func<Transform> environment,List<Object> owned)
        {
            var root=new GameObject("Clouds").transform;root.SetParent(stage,false);
            var it=root.gameObject.AddComponent<MemoryClouds>();it.sky=sky;it.stage=stage;it.floor=floor;it.UnderFeetMode=underFeet;it.seaTop=SeaTop(underFeet);
            it.shape=shape;it.layout=layout;it.environment=environment;
            var meshes=(material?(pieces??Enumerable.Empty<Mesh>()).Where(m=>m&&KindOf(m.name)!=Kind.Ruin):Enumerable.Empty<Mesh>())
                .Concat(ruinMaterial?(ruins??Enumerable.Empty<Mesh>()).Where(m=>m&&KindOf(m.name)==Kind.Ruin):Enumerable.Empty<Mesh>())
                .Where(m=>m.bounds.size.sqrMagnitude>0).ToArray();
            var kit=meshes.Where(m=>!m.name.Contains("_LOD")&&KindOf(m.name)!=null).OrderBy(m=>m.name).ToArray();
            var art=(models??Enumerable.Empty<GameObject>()).Select(Model).Where(m=>m.mesh&&m.material).ToArray();
            if(kit.Length+art.Length==0||!sky){Debug.Log("SHAPE_OF_MEMORY_CLOUDS none ("+(!sky?"no sky":"no pieces")+")");return it;}
            Mesh Tier(Mesh m,int lod){for(int t=lod;t>0;t--){var found=meshes.FirstOrDefault(f=>f.name==m.name+"_LOD"+t);if(found)return found;}return m;}
            it.material=material;
            var list=kit.Select(m=>new Piece(KindOf(m.name).Value,m.bounds.size,Floats(m.name)||layout.AnyFloats,Weight(m.name))).ToList();
            // A model's size: its mesh at the scale that makes it ModelHeight tall, or ModelWidth across if that is less.
            var fit=art.Select(m=>{var s=m.mesh.bounds.size;return Mathf.Min(ModelHeight/Mathf.Max(s.y,1e-3f),ModelWidth/Mathf.Max(Mathf.Max(s.x,s.z),1e-3f));}).ToArray();
            for(int i=0;i<art.Length;i++){var s=art[i].mesh.bounds.size*fit[i];list.Add(new Piece(Kind.Ruin,s,true,s.y>1.2f*Mathf.Max(s.x,s.z)?2:1));}
            var spots=Layout(list,Eye,it.seaTop,sky.Facing,Seed,shape,layout);
            var built=new List<Cloud>();
            foreach(var spot in spots)
            {
                bool isModel=spot.Piece>=kit.Length;int m=spot.Piece-kit.Length;
                var mesh=isModel?art[m].mesh:Tier(kit[spot.Piece],spot.Lod);float scale=spot.Scale*(isModel?fit[m]:1);
                var holder=new GameObject(mesh.name).transform;holder.SetParent(root,false);holder.localPosition=spot.Position;
                var model=new GameObject(spot.Kind==Kind.Ruin?"Ruin":"Cloud").transform;model.SetParent(holder,false);
                model.localScale=Vector3.one*scale;Anchor(model,mesh,Quaternion.Euler(spot.Tilt.x,spot.Yaw,spot.Tilt.z),scale);
                model.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var r=model.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=isModel?art[m].material:spot.Kind==Kind.Ruin?ruinMaterial:material;
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;
                r.reflectionProbeUsage=ReflectionProbeUsage.Off;r.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;r.enabled=false;
                built.Add(new Cloud{holder=holder,model=model,renderer=r,spot=spot,isModel=isModel});
            }
            it.clouds=built.ToArray();it.borrowing=it.clouds.Any(c=>c.isModel);
            if(it.borrowing&&environment==null)Debug.LogWarning("SHAPE_OF_MEMORY_CLOUDS ruin models without an environment scene to take their bake from: they show unlit");
            long tris=it.clouds.Sum(c=>{var mm=c.model.GetComponent<MeshFilter>().sharedMesh;long n=0;for(int k=0;k<mm.subMeshCount;k++)n+=mm.GetIndexCount(k)/3;return n;});
            Debug.Log($"SHAPE_OF_MEMORY_CLOUDS {it.clouds.Length} ({spots.Count(s=>s.Kind==Kind.Sea)} sea, {spots.Count(s=>s.Kind==Kind.Puff)} puffs of which {spots.Count(s=>s.Kind==Kind.Puff&&s.Floating)} lifted, {spots.Count(s=>s.Kind==Kind.Mid)} big, {spots.Count(s=>s.Kind==Kind.Ruin)} ruins ({it.clouds.Count(c=>c.isModel)} models) of which {spots.Count(s=>s.Kind==Kind.Ruin&&s.Floating)} afloat), {tris} triangles, sea top {it.seaTop:0.0} m from the floor ({(underFeet?"under the feet":"a drop")}) swell {shape.SeaSwell:0.0} m");
            it.Place();
            return it;
        }
        // A ruin model's mesh and material (its first renderer's).
        static (Mesh mesh,Material material) Model(GameObject prefab)
        {
            var filter=prefab?prefab.GetComponentInChildren<MeshFilter>(true):null;var renderer=filter?filter.GetComponent<MeshRenderer>():null;
            return (filter?filter.sharedMesh:null,renderer?renderer.sharedMaterial:null);
        }
        // Turned by rotation about the middle of its base, which stays on the holder.
        static void Anchor(Transform model,Mesh mesh,Quaternion rotation,float scale)
        {
            var b=mesh.bounds;model.localRotation=rotation;model.localPosition=-(rotation*new Vector3(b.center.x,b.min.y,b.center.z))*scale;
        }
        public static Kind? KindOf(string name)=>name.StartsWith("Sea_")?Kind.Sea:name.StartsWith("Puff_")?Kind.Puff:name.StartsWith("Tower_")||name.StartsWith("Bank_")?Kind.Mid:name.StartsWith("Ruin_")?Kind.Ruin:(Kind?)null;
        public static bool Floats(string name)=>name.StartsWith("Ruin_")&&name.Contains("Broken");
        // Pillars, columns and arches twice as often as the arcade, the wall and the temple (the user likes them best).
        public static float Weight(string name)=>name.StartsWith("Ruin_Pillar")||name.StartsWith("Ruin_Column")||name.StartsWith("Ruin_Arch")?2:1;

        // ---- Ruin models: their light. They are the environment scene's models on its Baked Lit materials, so they take
        // the bake of an instance of the same model there: its lightmap texels, and its turn and lean (the light was
        // baked with the model so turned). A standing one takes an instance leaning at most layout.Lean (then upright if
        // none does, the least leaning one's light); one afloat at most FloatTilt. Instances in turn, so copies differ.
        // Giants (on their own far-haze materials) only lend for a model that has no other instance (the gate). Without
        // the scene they keep the ambient alone.
        public const float ModelHeight=14,ModelWidth=20;
        void Borrow(Transform environment)
        {
            borrowing=false;
            var sources=environment.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r=>r.lightmapIndex>=0&&r.sharedMaterial&&r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh)
                .GroupBy(r=>r.GetComponent<MeshFilter>().sharedMesh).ToDictionary(g=>g.Key,g=>
                {
                    // Instances near enough to share a scale with ours first; the giants (far haze) only for a model that has no other.
                    var all=g.OrderBy(r=>Vector3.Angle(r.transform.up,environment.up)).ToArray();
                    var near=all.Where(r=>!r.sharedMaterial.name.Contains("Giant")).ToArray();
                    return near.Length>0?near:all;
                });
            int lent=0,upright=0;var turn=new Dictionary<Mesh,int>();
            foreach(var c in clouds.Where(c=>c.isModel))
            {
                var mesh=c.model.GetComponent<MeshFilter>().sharedMesh;
                if(!sources.TryGetValue(mesh,out var from)||from.Length==0){Debug.LogWarning("SHAPE_OF_MEMORY_CLOUDS no baked "+mesh.name+" in the environment scene");continue;}
                float limit=c.spot.Floating?FloatTilt:layout.Lean;
                var fit=from.Where(r=>Vector3.Angle(r.transform.up,environment.up)<=limit+1e-3f).ToArray();
                turn.TryGetValue(mesh,out int n);turn[mesh]=n+1;
                var source=fit.Length>0?fit[n%fit.Length]:from[0];
                var rotation=Quaternion.Inverse(environment.rotation)*source.transform.rotation;
                if(fit.Length==0){rotation=Quaternion.Euler(0,rotation.eulerAngles.y,0);upright++;}
                Anchor(c.model,mesh,rotation,c.model.localScale.x);
                c.renderer.lightmapIndex=source.lightmapIndex;c.renderer.lightmapScaleOffset=source.lightmapScaleOffset;lent++;
            }
            Debug.Log($"SHAPE_OF_MEMORY_CLOUDS {lent} ruin models lit from the environment scene's bake ({upright} stood upright on a leaning one's light)");
        }

        void LateUpdate()
        {
            Place();
            if(borrowing&&environment!=null){var env=environment();if(env)Borrow(env);}
            if(clouds.Length==0||!sky)return;
            if(!shown)
            {
                if(!sky.Shown||sky.Alpha<=0)return;
                shown=true;foreach(var c in clouds)c.renderer.enabled=true;
                Debug.Log("SHAPE_OF_MEMORY_CLOUDS fading in with the sky");
            }
            if(!revealed)
            {
                Shader.SetGlobalFloat(HideId,1-sky.Alpha);
                if(sky.Alpha>=1){revealed=true;Shader.SetGlobalFloat(HideId,0);Debug.Log("SHAPE_OF_MEMORY_CLOUDS up");}
            }
            Move(Time.deltaTime,Time.unscaledTime);
        }
        // Onto the floor, under the stage; and the haze's base for the shader.
        void Place()
        {
            if(!stage)return;
            float y=floor!=null?stage.InverseTransformPoint(new Vector3(stage.position.x,floor(),stage.position.z)).y:0;
            transform.localPosition=new Vector3(0,y,0);transform.localRotation=Quaternion.identity;
            Shader.SetGlobalFloat(BaseId,transform.position.y+seaTop-Mathf.Max(shape.SeaSwell,0)-SeaClear-BaseFade);Shader.SetGlobalFloat(BaseFadeId,BaseFade);
        }

        void Move(float dt,float now)
        {
            var eye=new Vector3(Eye.x,0,Eye.y);
            foreach(var c in clouds)
            {
                var s=c.spot;
                if(s.Kind==Kind.Ruin)
                {
                    if(!s.Floating)continue;
                    float f=2*Mathf.PI*now/s.Period;
                    c.holder.localPosition=s.Position+Vector3.up*(FloatBob*Mathf.Sin(f+s.Phase*6.283f));
                    c.holder.localRotation=Quaternion.Euler(0,2*Mathf.Sin(f*.6f+s.Phase2*6.283f),0);
                    continue;
                }
                if(s.Kind==Kind.Mid)
                {
                    c.drift+=WindDirection*(Wind*dt);
                    var at=s.Position+c.drift;var rel=at-eye;rel.y=0;
                    float lift=0;
                    switch(c.sink)
                    {
                        case 0:
                            if(rel.magnitude>MidFar&&Vector3.Dot(rel,WindDirection)>0){c.sink=1;c.sinkTime=0;c.depth=s.Top-(seaTop-SeaClear-BaseFade)+2;}
                            break;
                        case 1:
                            c.sinkTime+=dt;lift=-c.depth*MemoryIntro.Smooth(c.sinkTime/SinkSeconds);
                            if(c.sinkTime>=SinkSeconds){c.drift+=Mirror(rel,WindDirection)-rel;c.sink=2;c.sinkTime=0;lift=-c.depth;}
                            break;
                        case 2:
                            c.sinkTime+=dt;lift=-c.depth*(1-MemoryIntro.Smooth(c.sinkTime/SinkSeconds));
                            if(c.sinkTime>=SinkSeconds){c.sink=0;lift=0;}
                            break;
                    }
                    c.holder.localPosition=s.Position+c.drift+Vector3.up*lift;
                    continue;
                }
                float a=s.Kind==Kind.Sea?SwaySea:SwayPuff,w=2*Mathf.PI*now/s.Period;
                c.holder.localPosition=s.Position+new Vector3(a*Mathf.Sin(w+s.Phase*6.283f),0,a*Mathf.Cos(w/1.3f+s.Phase2*6.283f));
                c.holder.localRotation=Quaternion.Euler(0,Wobble*Mathf.Sin(w*.7f+s.Phase2*6.283f),0);
            }
        }
        // Where a cloud that left downwind comes back: reflected across the line through the eyes square to the wind
        // (same distance from the eyes, same closest pass).
        public static Vector3 Mirror(Vector3 rel,Vector3 wind)=>rel-2*Vector3.Dot(rel,wind)*wind;

        // ---- Layout
        // pieces: each near mesh's kind and size (metres, y up, base at 0). eye: the eyes over the floor (cloud space x, z).
        // seaTop: the sea's tops (cloud space y, the floor 0). glowYaw: where the glow is (degrees from +Z toward +X).
        public static Spot[] Layout(IReadOnlyList<Piece> pieces,Vector2 eye,float seaTop,float glowYaw=0,int seed=Seed)=>Layout(pieces,eye,seaTop,glowYaw,seed,Shape.Flat,RuinLayout.Standing);
        // shape: the sea's swell and the lifted puffs; ruins: how many ruins, how they stand and float. The flat sea and
        // standing ruins draw the same random numbers as before either was added, so those looks keep their layout.
        public static Spot[] Layout(IReadOnlyList<Piece> pieces,Vector2 eye,float seaTop,float glowYaw,int seed,Shape shape,RuinLayout ruinLayout)
        {
            var random=new System.Random(seed);float Next(float a,float b)=>a+(float)random.NextDouble()*(b-a);
            int[] Of(Kind k)=>Enumerable.Range(0,pieces.Count).Where(i=>pieces[i].Kind==k).ToArray();
            var spots=new List<Spot>();
            var eye3=new Vector3(eye.x,0,eye.y);
            // The sea's top here (cloud space y): seaTop moved by the swell, never above the feet's level.
            float Top(Vector3 p)=>SwellTop(seaTop,shape,p);
            Spot Make(Kind kind,int piece,float distance,float azimuth,float scale)
            {
                var size=pieces[piece].Size;var dir=Quaternion.Euler(0,azimuth,0)*Vector3.forward;
                return new Spot{Kind=kind,Piece=piece,Scale=scale,Yaw=Next(0,360),Distance=distance,Position=eye3+dir*distance,
                    Reach=.5f*Mathf.Max(size.x,size.z)*scale,Period=Next(SwayPeriodMin,SwayPeriodMax),Phase=Next(0,1),Phase2=Next(0,1)};
            }
            bool Clear(Spot s,float share,Func<Spot,bool> among)=>spots.Where(among).All(o=>Flat(o.Position-s.Position).magnitude>=share*(o.Reach+s.Reach));
            // Sea: one under the eyes, then spread out, denser near.
            var sea=Of(Kind.Sea);
            if(sea.Length>0)
            {
                for(int attempt=0;attempt<Tries&&spots.Count(s=>s.Kind==Kind.Sea)<SeaCount;attempt++)
                {
                    bool first=attempt==0;
                    float distance=first?0:SeaFar*Mathf.Pow(Next(0,1),.7f);
                    int piece=sea[random.Next(sea.Length)];
                    float scale=first?1.4f:Mathf.Lerp(1.2f,3.4f,distance/SeaFar)*Next(.85f,1.15f);
                    var s=Make(Kind.Sea,piece,distance,Next(0,360),scale);
                    if(!first&&!Clear(s,.42f,o=>o.Kind==Kind.Sea))continue;
                    s.Top=Top(s.Position)-Next(0,.5f);s.Position.y=s.Top-pieces[piece].Size.y*scale;
                    spots.Add(s);
                }
            }
            // Puffs: out of the sea, kept under the ceiling for their distance.
            var puffs=Of(Kind.Puff);
            for(int attempt=0;puffs.Length>0&&attempt<Tries&&spots.Count(s=>s.Kind==Kind.Puff)<PuffCount;attempt++)
            {
                float distance=PuffNear+(PuffFar-PuffNear)*Mathf.Pow(Next(0,1),1.3f);
                int piece=puffs[random.Next(puffs.Length)];var size=pieces[piece].Size;
                float scale=Next(.9f,1.6f)*(1+.6f*distance/PuffFar);
                var s=Make(Kind.Puff,piece,distance,Next(0,360),scale);
                if(!Clear(s,.8f,o=>o.Kind!=Kind.Sea))continue;
                float top=Top(s.Position),height=size.y*scale,baseY=top-.25f*height,ceiling=Ceiling(distance-s.Reach);
                if(baseY+height>ceiling)baseY=ceiling-height;
                if(baseY+height<top+1)continue;
                s.Position.y=baseY;s.Top=baseY+height;spots.Add(s);
            }
            // Banks and towers: mid distance, clear of the glow and of the eyes along their drift.
            var mids=Of(Kind.Mid);
            var banks=mids.Where(i=>pieces[i].Size.y<pieces[i].Size.x).ToArray();var towers=mids.Except(banks).ToArray();
            for(int attempt=0;(mids.Length>0||puffs.Length>0)&&attempt<Tries&&spots.Count(s=>s.Kind==Kind.Mid)<MidCount;attempt++)
            {
                float distance=Next(MidNear,MidFar),azimuth=Next(-180,180),pick=Next(0,1);
                var from=pick<MidPuff&&puffs.Length>0?puffs:pick<MidBank&&banks.Length>0?banks:towers.Length>0?towers:mids;
                if(from.Length==0)continue;
                int piece=from[random.Next(from.Length)];var size=pieces[piece].Size;
                var range=from==puffs?MidPuffScale:from==banks?MidBankScale:MidTowerScale;
                float scale=Next(range.x,range.y);
                var s=Make(Kind.Mid,piece,distance,azimuth,scale);
                float half=Mathf.Atan(s.Reach/distance)*Mathf.Rad2Deg;
                if(Mathf.Abs(Mathf.DeltaAngle(azimuth,glowYaw))<GlowGap+half)continue;
                if(Pass(Flat(s.Position-eye3),WindDirection)<MidNear)continue;
                if(!Clear(s,.8f,o=>o.Kind!=Kind.Sea))continue;
                float height=size.y*scale;s.Position.y=Top(s.Position)-Next(.15f,.3f)*height;s.Top=s.Position.y+height;
                if(s.Top>Ceiling(distance-s.Reach))continue;
                spots.Add(s);
            }
            // Ruins: standing in the sea with their bases sunk, or (a few broken ones) afloat; facing the eyes, roughly.
            var ruins=Of(Kind.Ruin);int floaters=0;float spiral=Next(0,360),weights=ruins.Sum(i=>Mathf.Max(pieces[i].Weight,0));
            // Every piece once first (while the count allows), then by weight.
            var unplaced=new List<int>(ruins);
            int Pick(){if(unplaced.Count>0)return unplaced[random.Next(unplaced.Count)];float at=Next(0,weights);foreach(int i in ruins){at-=Mathf.Max(pieces[i].Weight,0);if(at<=0)return i;}return ruins[ruins.Length-1];}
            for(int attempt=0;ruins.Length>0&&attempt<Tries&&spots.Count(s=>s.Kind==Kind.Ruin)<ruinLayout.Count;attempt++)
            {
                // Distance from a Halton sequence over the ruins placed so far (jittered within its share), so near, mid
                // and far fill evenly whatever the seed; direction from the golden-angle spiral over the tries.
                int placed=spots.Count(o=>o.Kind==Kind.Ruin);
                float band=Mathf.Clamp01(Halton(placed+1,2)+Next(-.5f,.5f)/Mathf.Max(ruinLayout.Count,1));
                float distance=Mathf.Lerp(RuinNear,RuinFar,band),azimuth=Mathf.DeltaAngle(0,spiral+attempt*137.50776f);
                int piece=Pick();var size=pieces[piece].Size;
                bool afloat=pieces[piece].Floats&&floaters<ruinLayout.Floaters&&Next(0,1)<.5f;
                float scale=Next(RuinScale.x,RuinScale.y)*(1+.5f*distance/RuinFar);
                var s=Make(Kind.Ruin,piece,distance,azimuth,scale);
                float half=Mathf.Atan(s.Reach/distance)*Mathf.Rad2Deg;
                if(Mathf.Abs(Mathf.DeltaAngle(azimuth,glowYaw))<RuinGlowGap+half)continue;
                if(!Clear(s,1,o=>o.Kind==Kind.Ruin)||!Clear(s,.5f,o=>o.Kind==Kind.Mid||o.Kind==Kind.Puff))continue;
                float height=size.y*scale,top=Top(s.Position);
                s.Position.y=afloat?top+Next(ruinLayout.FloatAbove.x,ruinLayout.FloatAbove.y):top-Next(RuinSink.x,RuinSink.y)*height;
                float highest=EyeHeight+distance*Mathf.Tan(RuinTopElevation*Mathf.Deg2Rad);
                if(s.Position.y+height>highest)
                {
                    if(afloat||highest-height<top-RuinDeepSink*height)continue;
                    s.Position.y=highest-height;
                }
                s.Top=s.Position.y+height;
                if(s.Top>Ceiling(distance-s.Reach))continue;
                var toEyes=Flat(eye3-s.Position);s.Yaw=Mathf.Atan2(toEyes.x,toEyes.z)*Mathf.Rad2Deg+Next(-RuinTurn,RuinTurn);
                if(afloat){s.Floating=true;s.Tilt=new Vector3(Next(-FloatTilt,FloatTilt),0,Next(-FloatTilt,FloatTilt));floaters++;}
                else if(ruinLayout.Lean>0)s.Tilt=new Vector3(Next(-ruinLayout.Lean,ruinLayout.Lean),0,Next(-ruinLayout.Lean,ruinLayout.Lean));
                s.Period=Next(FloatPeriod.x,FloatPeriod.y);
                spots.Add(s);unplaced.Remove(piece);
            }
            // Lifted puffs: a few clear of the sea at mid distance, out of the glow, under the ceiling.
            for(int attempt=0;shape.Lifted>0&&puffs.Length>0&&attempt<Tries&&spots.Count(s=>s.Kind==Kind.Puff&&s.Floating)<shape.Lifted;attempt++)
            {
                float distance=Next(MidNear*.6f,MidFar*.8f),azimuth=Next(-180,180);
                int piece=puffs[random.Next(puffs.Length)];var size=pieces[piece].Size;
                float scale=Next(.9f,1.6f)*(1+.6f*distance/PuffFar);
                var s=Make(Kind.Puff,piece,distance,azimuth,scale);
                float half=Mathf.Atan(s.Reach/distance)*Mathf.Rad2Deg;
                if(Mathf.Abs(Mathf.DeltaAngle(azimuth,glowYaw))<GlowGap+half)continue;
                if(!Clear(s,.8f,o=>o.Kind!=Kind.Sea))continue;
                float height=size.y*scale;s.Position.y=Top(s.Position)+Next(shape.LiftedHeight.x,shape.LiftedHeight.y);s.Top=s.Position.y+height;
                if(s.Top>Ceiling(distance-s.Reach))continue;
                s.Floating=true;spots.Add(s);
            }
            for(int i=0;i<spots.Count;i++){var s=spots[i];s.Lod=LodOf(s.Kind,s.Distance,s.Reach);spots[i]=s;}
            return spots.ToArray();
        }
        public static int LodOf(Kind kind,float distance,float reach)
        {
            if(kind==Kind.Sea)return distance>SeaLods.y?2:distance>SeaLods.x?1:0;
            float wide=Wide(distance,reach);return wide>WideLods.x?0:wide>WideLods.y?1:2;
        }
        // How many degrees across a cloud of footprint radius reach looks from distance away.
        public static float Wide(float distance,float reach)=>2*Mathf.Atan2(reach,Mathf.Max(distance,1e-3f))*Mathf.Rad2Deg;
        // The radical inverse of i in base b: 1/2, 1/4, 3/4, 1/8, ... for base 2.
        public static float Halton(int i,int b){float f=1,r=0;while(i>0){f/=b;r+=f*(i%b);i/=b;}return r;}
        // How near the eyes a drift along wind passes, from rel (horizontal, eyes to the cloud).
        public static float Pass(Vector3 rel,Vector3 wind)=>Vector3.Cross(Flat(wind).normalized,Flat(rel)).magnitude;
        static Vector3 Flat(Vector3 v)=>new Vector3(v.x,0,v.z);
    }
}
