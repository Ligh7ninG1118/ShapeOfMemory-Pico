using System;
using System.Collections.Generic;
using CloudGarden.Integration;
using CloudGarden.ShadowPuzzle;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // A lamp's sphere that a hand can push (2026-11-15, the user: "pushable, exists at all times, pushing doesn't affect the
    // lighting at all"). The sphere gives up to MaxPush and springs back with a little wobble, which says "fixed, not
    // yours to move" without words. Only the lamp's models move (the art add-on's SM_Spotlight_01, or the flashlight
    // without it): the lamp head, its Light, the shadow it casts and the solve check all stay where they are, so a push can
    // never change a memory. The lamps sit in both rooms (between memories and in one), so every lamp head gets one.
    // With rays (a lit lamp in a memory) a few soft cards of light leave the lens toward the pile, as bright as the lamp
    // (Torch2World.LampLevel), so they come and go with the memory.
    // The orb lives beside its lamp head, not under it: the art add-on hides every renderer under a dressed lamp that is
    // not its own (that is how the flashlight goes), which would hide the rays too.
    public sealed class MemoryLampOrb : MonoBehaviour
    {
        // Metres, seconds. Radius: SM_Spotlight_01 at the add-on's .4 (a .151 x .128 x .139 m sphere with its ring).
        // 2026-11-15, the user: "a bit more pushable, extend the distance it can be pushed, and the spring back": 4 cm at
        // 3 Hz -> 10 cm, a softer 1.5 Hz spring that drifts back over about a second with one easy wobble (was a snap).
        // Static so they can be tuned in Play mode.
        public static float MaxPush=.10f,Frequency=1.5f,Damping=.5f;
        public const float Radius=.066f,FingerRadius=.009f,PalmRadius=.022f,MaxSpeed=1.5f;
        // The rays: from the lens (RayStart in front of the lamp's centre) RayLength toward the pile, RayLens wide there and
        // opening at RaySpread of the spot's own half angle.
        public const float RayStart=.055f,RayLength=.24f,RayLens=.03f,RaySpread=.6f;
        public const int RayCards=4;
        const string RaysMaterial="ShapeOfMemory/Lamp rays";

        Transform head;
        MetaShadowPlayer player;
        Func<float> level;
        Transform rays;
        Renderer raysRenderer;
        MaterialPropertyBlock block;
        AudioSource voice;
        readonly Dictionary<Transform,Vector3> rest=new Dictionary<Transform,Vector3>();
        readonly List<Vector4> joints=new List<Vector4>();
        readonly Vector3[] read=new Vector3[MemoryHandSkin.Count];
        Vector3 velocity;
        bool touching;
        float lastTink=-10;
        // World offset of the models from the lamp head.
        public Vector3 Offset{get;private set;}
        public bool Touching=>touching;
        public Transform Head=>head;
        public Vector3 Centre=>head?head.position+Offset:transform.position;
        public bool HasRays=>rays;
        // Every live orb (the ghost hand treats a sphere at its limit as solid).
        public static readonly List<MemoryLampOrb> All=new List<MemoryLampOrb>();
        // Tests: points (xyz) with radii (w) pressing instead of the tracked hands.
        public Func<IReadOnlyList<Vector4>> Probe;

        // level: the lamp's brightness 0..1 for its rays (null: no rays, as for a lamp left dark or between memories).
        public static MemoryLampOrb Add(Transform head,MetaShadowPlayer player,Func<float> level)
        {
            if(!head)return null;
            var go=new GameObject(head.name+" orb");go.transform.SetParent(head.parent,false);
            var it=go.AddComponent<MemoryLampOrb>();it.head=head;it.player=player;it.level=level;
            if(level!=null)it.BuildRays();
            it.voice=go.AddComponent<AudioSource>();it.voice.playOnAwake=false;it.voice.spatialBlend=1;it.voice.minDistance=.3f;it.voice.maxDistance=4;
            it.voice.dopplerLevel=0;it.voice.clip=Tink();
            return it;
        }
        void OnEnable()=>All.Add(this);
        void OnDisable()=>All.Remove(this);

        // ---- The push: kinematic while a hand is in the sphere, a damped spring after.
        // Moves offset so no point (xyz, radius w) is inside the sphere of Radius about origin + offset, at most MaxPush from
        // origin; returns whether any point was inside.
        public static bool Resolve(Vector3 origin,ref Vector3 offset,IReadOnlyList<Vector4> points)
        {
            bool inside=false;
            for(int pass=0;pass<2;pass++)
                for(int i=0;i<points.Count;i++)
                {
                    var p=(Vector3)points[i];float min=Radius+points[i].w;
                    var d=p-(origin+offset);float dist=d.magnitude;
                    if(dist>=min)continue;
                    inside=true;
                    var away=dist>1e-5f?d/dist:(offset.sqrMagnitude>1e-10f?-offset.normalized:Vector3.back);
                    offset-=away*(min-dist);
                    offset=Vector3.ClampMagnitude(offset,MaxPush);
                }
            return inside;
        }
        // The spring back toward 0 (semi-implicit Euler in steps of at most 1/240 s).
        public static void Spring(ref Vector3 offset,ref Vector3 velocity,float dt)
        {
            float w=2*Mathf.PI*Frequency;
            int steps=Mathf.Max(1,Mathf.CeilToInt(dt*240));float h=dt/steps;
            for(int s=0;s<steps;s++)
            {
                velocity+=(-w*w*offset-2*Damping*w*velocity)*h;
                offset+=velocity*h;
            }
            offset=Vector3.ClampMagnitude(offset,MaxPush);
        }
        // One frame of it: pushed by the points, or springing back. Returns whether a point was inside.
        public static bool Step(Vector3 origin,ref Vector3 offset,ref Vector3 velocity,IReadOnlyList<Vector4> points,float dt)
        {
            var before=offset;
            if(points!=null&&points.Count>0&&Resolve(origin,ref offset,points))
            {
                velocity=dt>0?Vector3.ClampMagnitude((offset-before)/dt,MaxSpeed):Vector3.zero;
                return true;
            }
            Spring(ref offset,ref velocity,dt);
            return false;
        }

        void LateUpdate()
        {
            if(!head){Destroy(gameObject);return;}
            Tick(Mathf.Min(Time.deltaTime,.05f));
        }
        // One frame (tests call it directly).
        public void Tick(float dt)
        {
            var offset=Offset;
            bool was=touching;
            touching=Step(head.position,ref offset,ref velocity,Points(),dt);
            Offset=offset;
            if(touching&&!was&&Time.unscaledTime-lastTink>.35f&&voice&&voice.clip)
            {
                lastTink=Time.unscaledTime;voice.transform.position=Centre;
                voice.pitch=UnityEngine.Random.Range(.94f,1.06f);voice.PlayOneShot(voice.clip,Mathf.Lerp(.25f,.6f,velocity.magnitude/.6f));
            }
            Place();
        }
        IReadOnlyList<Vector4> Points()
        {
            if(Probe!=null)return Probe();
            joints.Clear();
            if(!player)return joints;
            for(int h=0;h<2;h++)
            {
                if(!MemoryHandSkin.ReadJoints(player,h==0,read))continue;
                for(int i=0;i<read.Length;i++)joints.Add(new Vector4(read[i].x,read[i].y,read[i].z,i<=MemoryHandSkin.Wrist?PalmRadius:FingerRadius));
            }
            return joints;
        }
        // The lamp head's models follow the offset (each from where it was put); the Light stays.
        void Place()
        {
            var local=head.InverseTransformVector(Offset);
            for(int i=0;i<head.childCount;i++)
            {
                var child=head.GetChild(i);
                if(child.GetComponent<Light>())continue;
                if(!rest.TryGetValue(child,out var at)){at=child.localPosition;rest[child]=at;}
                child.localPosition=at+local;
            }
            if(rays)
            {
                rays.SetPositionAndRotation(head.position+Offset+head.forward*RayStart,head.rotation);
                float on=level!=null?Mathf.Clamp01(level()):0;
                raysRenderer.enabled=on>.01f;
                if(raysRenderer.enabled){block.SetFloat("_Intensity",on);raysRenderer.SetPropertyBlock(block);}
            }
        }

        // ---- The rays
        void BuildRays()
        {
            var material=Resources.Load<Material>(RaysMaterial);
            if(!material){Debug.LogWarning("SHAPE_OF_MEMORY_LAMP no Resources/"+RaysMaterial+".mat: no rays");return;}
            rays=new GameObject("Rays",typeof(MeshFilter),typeof(MeshRenderer)).transform;rays.SetParent(transform,false);
            rays.GetComponent<MeshFilter>().sharedMesh=RayMesh();
            raysRenderer=rays.GetComponent<MeshRenderer>();raysRenderer.sharedMaterial=material;
            raysRenderer.shadowCastingMode=ShadowCastingMode.Off;raysRenderer.receiveShadows=false;
            raysRenderer.lightProbeUsage=LightProbeUsage.Off;raysRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            block=new MaterialPropertyBlock();
        }
        static Mesh rayMesh;
        // RayCards cards through the beam's line (+z), turned evenly about it; each widens from RayLens at the lens.
        public static Mesh RayMesh()
        {
            if(rayMesh)return rayMesh;
            float r1=RayLens+RayLength*Mathf.Tan(Torch2Projection.HalfAngle*Mathf.Deg2Rad)*RaySpread;
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int c=0;c<RayCards;c++)
            {
                var turn=Quaternion.AngleAxis(c*180f/RayCards+11,Vector3.forward);int b=v.Count;
                v.Add(turn*new Vector3(-RayLens,0,0));v.Add(turn*new Vector3(RayLens,0,0));
                v.Add(turn*new Vector3(r1,0,RayLength));v.Add(turn*new Vector3(-r1,0,RayLength));
                var normal=turn*Vector3.up;for(int k=0;k<4;k++)n.Add(normal);
                uv.Add(new Vector2(0,0));uv.Add(new Vector2(1,0));uv.Add(new Vector2(1,1));uv.Add(new Vector2(0,1));
                t.Add(b);t.Add(b+2);t.Add(b+1);t.Add(b);t.Add(b+3);t.Add(b+2);
            }
            rayMesh=new Mesh{name="Lamp rays",hideFlags=HideFlags.DontSave};rayMesh.SetVertices(v);rayMesh.SetNormals(n);rayMesh.SetUVs(0,uv);rayMesh.SetTriangles(t,0);
            rayMesh.RecalculateBounds();return rayMesh;
        }

        // ---- A soft glassy tink when a hand meets the sphere
        static AudioClip tink;
        public static AudioClip Tink()
        {
            if(tink)return tink;
            const int rate=44100;int n=(int)(rate*.6f);var s=new float[n];
            for(int i=0;i<n;i++)
            {
                float t=i/(float)rate;float attack=Mathf.Clamp01(t/.004f);
                s[i]=attack*(.55f*Mathf.Sin(2*Mathf.PI*1318.5f*t)*Mathf.Exp(-t*7f)+.25f*Mathf.Sin(2*Mathf.PI*1975.5f*t)*Mathf.Exp(-t*11f)
                    +.12f*Mathf.Sin(2*Mathf.PI*2637f*t)*Mathf.Exp(-t*16f))*.5f;
            }
            tink=AudioClip.Create("Lamp tink",n,1,rate,false);tink.SetData(s,0);tink.hideFlags=HideFlags.DontSave;return tink;
        }
    }
}
