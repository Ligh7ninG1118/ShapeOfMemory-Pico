using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // Abstract Mediterranean architecture drifting in the dusk beyond the book (MemoryLook.DreamPieces): plaster walls
    // with arched openings, stairs, a portal, an arcade, a bridge, a domed chapel. The pieces and their light come from
    // Tools/ShadowArt/build_dream_kit.py (Art/Dream/DreamKit.fbx + DreamKit.png): lit once in Blender (a warm key from the
    // player's front left, the glow behind as a rim, warm light inside every opening) and baked into one atlas, so here
    // they are unlit, one material, no lights or shadows. They surface one by one as the sky fades in (never over
    // passthrough, where they would float through the real room), then bob and sway slowly.
    // Size: the pieces are abstract, so nothing tells their real size; each copy is ScaleMin..ScaleMax of its piece and
    // sits as far from the eyes as makes it Across degrees wide (6 to 40 m; mostly small). Nearest is Near, four times as far as the
    // walls' corner: the book flies up and grows into the walls well in front of them, with nothing near it to measure
    // the growth against, and none is as big to the eye as one wall (about 45 degrees).
    // Placement, seen from the eyes (stage space, looking along +Z): within Azimuth either side of ahead and Low..High in
    // elevation, Gap degrees apart; each the best of Candidates random spots. Each stands upright, turned to the eyes and
    // then TurnMin..TurnMax away, so the arches' lit sides show (the bake expects that). Seeded: the same every launch.
    public sealed class MemoryDream : MonoBehaviour
    {
        public const int Count=20,Seed=7,Candidates=8,Tries=600;
        // Across is drawn skewed toward AcrossMin (its square): many small and far, a few big.
        public const float ScaleMin=.6f,ScaleMax=1.7f,AcrossMin=4,AcrossMax=22,Near=6,Far=40,Azimuth=110,Low=-18,High=34,Gap=4;
        public const float TurnMin=15,TurnMax=35,Tilt=4;
        // Surfacing: the first First seconds after the sky starts fading in, then one every Stagger, each growing over Bloom
        // while it rises Rise of its own size into place. Drift: up and down by Bob degrees of view (far ones move as much
        // to the eye as near ones) over Period seconds; swaying Sway degrees about its upright.
        public const float First=.5f,Stagger=.18f,Bloom=2.2f,Rise=.35f,Bob=.8f,PeriodMin=8,PeriodMax=14,Sway=3;
        // Dusk haze: the farthest take this much of MemoryPalette.Haze over the baked colour (the nearest none).
        public const float Mist=.12f;

        public struct Spot
        {
            // Middle (stage space), how far from the eyes, how many degrees across, its longest side (metres), facing.
            public Vector3 Position;public float Distance,Across,Size;public Quaternion Facing;
            // Which piece at what scale; order to surface in; drift period and phases.
            public int Piece;public float Scale;public int Rank;public float Period,Phase,SwayPhase;
        }

        sealed class Drifter
        {
            public Transform holder,spin;public Renderer renderer;public Spot spot;public float amplitude;public bool on;
        }

        // The eyes in stage space, where the stage is placed from (MemoryGame.Recenter).
        public static Vector3 Eye=>new Vector3(0,MemoryGame.StageBelow,-MemoryGame.StageAhead);
        public bool Surfaced=>drifters.Length>0&&drifters.All(d=>d.on)&&Elapsed>=First+Stagger*(drifters.Length-1)+Bloom;
        public int Drifting=>drifters.Length;
        // The kit's fronts (the side the bake lights) face this way in mesh space; turned onto +Z, toward the eyes.
        static readonly Quaternion Front=Quaternion.identity;
        float Elapsed=>shownAt<0?0:Time.unscaledTime-shownAt;
        Drifter[] drifters=new Drifter[0];
        MemorySky sky;
        float shownAt=-1;

        public static MemoryDream Add(Transform stage,IEnumerable<Mesh> pieces,Material material,MemorySky sky,List<Object> owned)
        {
            var root=new GameObject("Dream").transform;root.SetParent(stage,false);
            var it=root.gameObject.AddComponent<MemoryDream>();it.sky=sky;
            var meshes=(pieces??Enumerable.Empty<Mesh>()).Where(m=>m&&m.bounds.size.sqrMagnitude>0).ToArray();
            if(meshes.Length==0||!material){Debug.Log("SHAPE_OF_MEMORY_DREAM none ("+(material?"no pieces":"no material")+")");return it;}
            var spots=Layout(meshes.Select(m=>Longest(m.bounds.size)).ToArray(),Eye,Count);
            var haze=MemoryPalette.Haze;var drifters=new List<Drifter>();
            foreach(var spot in spots)
            {
                if(spot.Distance<=0)continue;
                var mesh=meshes[spot.Piece];
                var holder=new GameObject(mesh.name).transform;holder.SetParent(root,false);holder.localPosition=spot.Position;holder.localRotation=spot.Facing;
                var spin=new GameObject("Drift").transform;spin.SetParent(holder,false);
                var model=new GameObject("Piece").transform;model.SetParent(spin,false);
                model.localRotation=Front;model.localScale=Vector3.one*spot.Scale;model.localPosition=-(Front*mesh.bounds.center)*spot.Scale;
                model.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var r=model.gameObject.AddComponent<MeshRenderer>();
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;
                r.reflectionProbeUsage=ReflectionProbeUsage.Off;r.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;r.enabled=false;
                float far=Mathf.InverseLerp(Near,Far,spot.Distance)*Mist;
                var tinted=new Material(material){name=material.name+" "+mesh.name};owned?.Add(tinted);
                if(tinted.HasProperty(BaseColor)){var c=tinted.GetColor(BaseColor);tinted.SetColor(BaseColor,new Color(c.r*Mathf.Lerp(1,haze.r,far),c.g*Mathf.Lerp(1,haze.g,far),c.b*Mathf.Lerp(1,haze.b,far),c.a));}
                r.sharedMaterial=tinted;
                holder.localScale=Vector3.zero;
                drifters.Add(new Drifter{holder=holder,spin=spin,renderer=r,spot=spot,amplitude=spot.Distance*Mathf.Tan(Bob*Mathf.Deg2Rad)});
            }
            it.drifters=drifters.ToArray();
            Debug.Log($"SHAPE_OF_MEMORY_DREAM {it.drifters.Length} pieces: "+string.Join(", ",it.drifters.Select(d=>$"{d.holder.name} x{d.spot.Scale:0.0} {d.spot.Distance:0} m {d.spot.Across:0} deg")));
            return it;
        }

        void Update()
        {
            if(shownAt<0){if(!sky||!sky.Shown)return;shownAt=Time.unscaledTime;}
            float elapsed=Elapsed,now=Time.unscaledTime;
            foreach(var d in drifters)
            {
                if(!d.holder)continue;
                float k=MemoryIntro.Smooth((elapsed-First-Stagger*d.spot.Rank)/Bloom);
                bool on=k>0;if(on!=d.on){d.on=on;if(d.renderer)d.renderer.enabled=on;}
                if(!on)continue;
                var s=d.spot;float wave=2*Mathf.PI*(now/s.Period+s.Phase),sway=2*Mathf.PI*(now/(s.Period*1.6f)+s.SwayPhase);
                d.holder.localPosition=s.Position+Vector3.up*(d.amplitude*Mathf.Sin(wave)-Rise*s.Size*(1-k));
                d.holder.localScale=Vector3.one*k;
                d.spin.localRotation=Quaternion.Euler(.4f*Sway*Mathf.Sin(wave+1.1f),Sway*Mathf.Sin(sway),.5f*Sway*Mathf.Sin(sway*.7f+.3f));
            }
        }

        // ---- Layout
        // count copies of pieces whose longest sides (metres) are sizes, seen from eye (stage space): every piece at least
        // once while count allows, each at its own scale. Biggest first: they are the hardest to fit. A copy that finds no
        // spot Gap clear of the others gets Distance 0 (left out).
        public static Spot[] Layout(IReadOnlyList<float> sizes,Vector3 eye,int count,int seed=Seed)
        {
            var random=new System.Random(seed);float Next(float a,float b)=>a+(float)random.NextDouble()*(b-a);
            var spots=new Spot[count];
            var deck=new List<int>();
            while(deck.Count<count){var round=Enumerable.Range(0,sizes.Count).OrderBy(_=>random.Next()).ToList();deck.AddRange(round);}
            for(int i=0;i<count;i++){spots[i].Piece=deck[i];spots[i].Scale=Next(ScaleMin,ScaleMax);spots[i].Size=sizes[deck[i]]*spots[i].Scale;}
            var placed=new List<(Vector3 direction,float half)>();
            foreach(int i in Enumerable.Range(0,count).OrderByDescending(i=>spots[i].Size).ThenBy(i=>i))
            {
                float size=spots[i].Size,best=float.NegativeInfinity;Vector3 bestDirection=default;float bestHalf=0,bestDistance=0;
                for(int attempt=0,good=0;attempt<Tries&&good<Candidates;attempt++)
                {
                    float pick=Next(0,1),across=Mathf.Lerp(AcrossMin,AcrossMax,pick*pick);
                    float distance=Mathf.Clamp(size*.5f/Mathf.Tan(across*.5f*Mathf.Deg2Rad),Near,Far);
                    float half=Mathf.Atan(size*.5f/distance)*Mathf.Rad2Deg;
                    float margin=half+Bob;if(Low+margin>High-margin)continue;
                    var direction=Quaternion.Euler(-Next(Low+margin,High-margin),Next(-Azimuth+margin,Azimuth-margin),0)*Vector3.forward;
                    float clearance=float.PositiveInfinity;
                    foreach(var (other,otherHalf) in placed)clearance=Mathf.Min(clearance,Vector3.Angle(direction,other)-half-otherHalf);
                    if(clearance<Gap)continue;
                    good++;
                    if(clearance<=best)continue;
                    best=clearance;bestDirection=direction;bestHalf=half;bestDistance=distance;
                }
                if(best==float.NegativeInfinity){Debug.LogWarning($"SHAPE_OF_MEMORY_DREAM no room for copy {i} (piece {spots[i].Piece}, {size:0.0} m)");continue;}
                placed.Add((bestDirection,bestHalf));
                var s=spots[i];
                s.Position=eye+bestDirection*bestDistance;s.Distance=bestDistance;s.Across=2*bestHalf;
                // Upright, turned to the eyes, then away either way so the lit sides of its arches show.
                var toEyes=Vector3.ProjectOnPlane(-bestDirection,Vector3.up);if(toEyes.sqrMagnitude<1e-6f)toEyes=Vector3.back;
                float turn=Next(TurnMin,TurnMax)*(random.Next(2)==0?-1:1);
                s.Facing=Quaternion.LookRotation(toEyes.normalized,Vector3.up)*Quaternion.Euler(Next(-Tilt,Tilt),turn,Next(-Tilt,Tilt));
                s.Period=Next(PeriodMin,PeriodMax);s.Phase=Next(0,1);s.SwayPhase=Next(0,1);
                spots[i]=s;
            }
            // Surfacing order: shuffled, so near and far, left and right come up mixed.
            var ranks=Enumerable.Range(0,count).Where(i=>spots[i].Distance>0).OrderBy(_=>random.Next()).ToArray();
            for(int r=0;r<ranks.Length;r++)spots[ranks[r]].Rank=r;
            return spots;
        }

        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        static float Longest(Vector3 size)=>Mathf.Max(size.x,Mathf.Max(size.y,size.z));
    }
}
