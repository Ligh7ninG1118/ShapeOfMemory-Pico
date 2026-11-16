using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Kind=ShapeOfMemory.MemoryClouds.Kind;

namespace ShapeOfMemory.Tests
{
    public class MemoryCloudsTests
    {
        // The kit's near meshes (Unity axes, y up; build_cloud_kit.py prints them as width x depth x height).
        static readonly MemoryClouds.Piece[] Kit=
        {
            new MemoryClouds.Piece(Kind.Sea,new Vector3(17.9f,4.6f,15.9f)),new MemoryClouds.Piece(Kind.Sea,new Vector3(14.4f,4.0f,19.1f)),
            new MemoryClouds.Piece(Kind.Sea,new Vector3(20.0f,5.5f,11.9f)),new MemoryClouds.Piece(Kind.Sea,new Vector3(15.1f,5.0f,13.9f)),
            new MemoryClouds.Piece(Kind.Puff,new Vector3(10.4f,8.0f,6.7f)),new MemoryClouds.Piece(Kind.Puff,new Vector3(11.4f,9.0f,7.2f)),
            new MemoryClouds.Piece(Kind.Puff,new Vector3(6.2f,6.0f,5.4f)),new MemoryClouds.Piece(Kind.Puff,new Vector3(13.5f,10.0f,8.1f)),
            new MemoryClouds.Piece(Kind.Mid,new Vector3(54.4f,12.0f,15.3f)),new MemoryClouds.Piece(Kind.Mid,new Vector3(47.3f,10.0f,14.8f)),
            // The ruin kit (build_ruin_kit.py): pillar, broken pillar, column, broken column, arch, broken arch (these six at
            // weight 2), arcade, wall, temple; the broken ones may float.
            new MemoryClouds.Piece(Kind.Ruin,new Vector3(3.2f,16.0f,3.2f),false,2),new MemoryClouds.Piece(Kind.Ruin,new Vector3(3.0f,11.4f,3.0f),true,2),
            new MemoryClouds.Piece(Kind.Ruin,new Vector3(2.7f,15.0f,2.7f),false,2),new MemoryClouds.Piece(Kind.Ruin,new Vector3(2.7f,8.9f,2.7f),true,2),
            new MemoryClouds.Piece(Kind.Ruin,new Vector3(10.2f,14.8f,2.8f),false,2),new MemoryClouds.Piece(Kind.Ruin,new Vector3(9.0f,13.5f,2.2f),true,2),
            new MemoryClouds.Piece(Kind.Ruin,new Vector3(19.4f,11.7f,2.6f)),new MemoryClouds.Piece(Kind.Ruin,new Vector3(12.0f,13.9f,1.6f)),
            new MemoryClouds.Piece(Kind.Ruin,new Vector3(12.5f,12.4f,2.6f)),
        };
        static readonly Vector2 Eye=new Vector2(0,-.6f);

        // glow: where the sky's glow shows (MemorySky.Facing), degrees right of forward: 0, or -20 for the Flow sky.
        static MemoryClouds.Spot[] Layout(bool underFeet,int seed=MemoryClouds.Seed,float glow=0)=>MemoryClouds.Layout(Kit,Eye,MemoryClouds.SeaTop(underFeet),glow,seed);

        [Test] public void LayoutIsTheSameEveryTime()
        {
            var a=Layout(false);var b=Layout(false);
            Assert.AreEqual(a.Length,b.Length);
            for(int i=0;i<a.Length;i++){Assert.AreEqual(a[i].Position,b[i].Position);Assert.AreEqual(a[i].Piece,b[i].Piece);Assert.AreEqual(a[i].Yaw,b[i].Yaw);}
        }

        // Each band nearly full, the sea's first piece right under the eyes, and the kinds' pieces only.
        [Test] public void EveryBandIsFilled([Values(false,true)] bool underFeet,[Values(1,5,42)] int seed)
        {
            var spots=Layout(underFeet,seed);
            Assert.That(spots.Count(s=>s.Kind==Kind.Sea),Is.GreaterThanOrEqualTo(MemoryClouds.SeaCount*.8f),"sea");
            Assert.That(spots.Count(s=>s.Kind==Kind.Puff),Is.GreaterThanOrEqualTo(MemoryClouds.PuffCount/2),"puffs");
            Assert.That(spots.Count(s=>s.Kind==Kind.Mid),Is.GreaterThanOrEqualTo(MemoryClouds.MidCount*.8f),"banks and towers");
            Assert.AreEqual(Kind.Sea,spots[0].Kind);Assert.AreEqual(0,spots[0].Distance,1e-4f,"a sea piece under the eyes");
            foreach(var s in spots)
                if(s.Kind==Kind.Mid)Assert.That(Kit[s.Piece].Kind,Is.EqualTo(Kind.Mid).Or.EqualTo(Kind.Puff),"a bank or a big puff");
                else Assert.AreEqual(s.Kind,Kit[s.Piece].Kind,"a piece of its own kind");
        }

        // Nothing comes up into the play space: the sea stays at its top, everything under the ceiling for its nearest edge
        // (under the floor within KeepOut), and the base and top agree with the piece's height.
        [Test] public void NothingRisesIntoThePlaySpace([Values(false,true)] bool underFeet,[Values(1,5,42)] int seed)
        {
            float seaTop=MemoryClouds.SeaTop(underFeet);
            foreach(var s in Layout(underFeet,seed))
            {
                Assert.AreEqual(s.Position.y+Kit[s.Piece].Size.y*s.Scale,s.Top,1e-3f,$"{s.Kind} top from its base");
                float near=s.Distance-s.Reach;
                if(near<=MemoryClouds.KeepOut)Assert.That(s.Top,Is.LessThan(0),$"{s.Kind} {near:0.0} m from the eyes under the floor");
                if(s.Kind!=Kind.Sea)Assert.That(s.Top,Is.LessThanOrEqualTo(MemoryClouds.Ceiling(near)+1e-3f),$"{s.Kind} {near:0.0} m from the eyes under its ceiling");
                if(s.Kind==Kind.Sea)Assert.That(s.Top,Is.InRange(seaTop-.5f-1e-3f,seaTop+1e-3f),"a sea top");
                else Assert.That(s.Top,Is.GreaterThanOrEqualTo(seaTop+1-1e-3f),$"{s.Kind} shows over the sea");
            }
        }

        [Test] public void TheGlowStaysOpen([Values(1,5,42)] int seed,[Values(0f,-20f)] float glow)
        {
            foreach(var s in Layout(false,seed,glow).Where(s=>s.Kind==Kind.Mid))
            {
                var rel=s.Position-new Vector3(Eye.x,0,Eye.y);
                float azimuth=Mathf.Atan2(rel.x,rel.z)*Mathf.Rad2Deg,half=Mathf.Atan(s.Reach/s.Distance)*Mathf.Rad2Deg;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(azimuth,glow)),Is.GreaterThanOrEqualTo(MemoryClouds.GlowGap+half-1e-2f),$"bank at {azimuth:0} deg");
                Assert.That(s.Distance,Is.InRange(MemoryClouds.MidNear,MemoryClouds.MidFar));
            }
        }

        // Banks and towers drift; none ever passes nearer than MidNear, and the wrap upwind keeps distance and pass.
        [Test] public void DriftKeepsItsDistance([Values(1,5,42)] int seed)
        {
            var wind=MemoryClouds.WindDirection;
            foreach(var s in Layout(false,seed).Where(s=>s.Kind==Kind.Mid))
            {
                var rel=s.Position-new Vector3(Eye.x,s.Position.y,Eye.y);
                Assert.That(MemoryClouds.Pass(rel,wind),Is.GreaterThanOrEqualTo(MemoryClouds.MidNear-1e-3f),"closest pass");
                var back=MemoryClouds.Mirror(rel,wind);
                Assert.AreEqual(rel.magnitude,back.magnitude,1e-3f,"same distance");
                Assert.AreEqual(MemoryClouds.Pass(rel,wind),MemoryClouds.Pass(back,wind),1e-3f,"same pass");
                Assert.AreEqual(-Vector3.Dot(rel,wind),Vector3.Dot(back,wind),1e-3f,"upwind as far as it was downwind");
            }
        }

        [Test] public void FarCloudsUseTheMidMeshes()
        {
            var spots=Layout(false);
            foreach(var s in spots)Assert.AreEqual(MemoryClouds.LodOf(s.Kind,s.Distance,s.Reach),s.Lod,$"{s.Kind} at {s.Distance:0} m");
            // The sea by distance, whatever its size.
            Assert.AreEqual(0,MemoryClouds.LodOf(Kind.Sea,29,40));Assert.AreEqual(1,MemoryClouds.LodOf(Kind.Sea,31,40));Assert.AreEqual(2,MemoryClouds.LodOf(Kind.Sea,151,40));
            // The rest by how wide they look: a 40 m cloud is near-detail even at 170 m, a small one far off is not.
            Assert.AreEqual(0,MemoryClouds.LodOf(Kind.Mid,170,20));Assert.AreEqual(1,MemoryClouds.LodOf(Kind.Puff,100,5));Assert.AreEqual(2,MemoryClouds.LodOf(Kind.Puff,200,4));
            Assert.AreEqual(14.25f,MemoryClouds.Wide(80,10),.01f);
            Assert.That(spots.Count(s=>s.Lod==2),Is.GreaterThan(spots.Length/10),"the farthest sea on the lightest meshes");
            Assert.That(spots.Count(s=>(s.Kind==Kind.Puff||s.Kind==Kind.Mid)&&s.Lod==0),Is.GreaterThan(spots.Count(s=>s.Kind==Kind.Puff||s.Kind==Kind.Mid)*3/4),"puffs and big clouds mostly on their near meshes");
        }

        // Ruins: in their band, clear of the glow's middle; standing ones sunk into the sea by part of their height and
        // showing over it; a few broken ones afloat over the sea, tilted; all turned to the eyes within RuinTurn.
        [Test] public void RuinsStandInTheSea([Values(false,true)] bool underFeet,[Values(1,5,42)] int seed,[Values(0f,-20f)] float glow)
        {
            float seaTop=MemoryClouds.SeaTop(underFeet);var spots=Layout(underFeet,seed,glow).Where(s=>s.Kind==Kind.Ruin).ToArray();
            Assert.That(spots.Length,Is.GreaterThanOrEqualTo(MemoryClouds.RuinCount*.8f),"ruins");
            Assert.That(spots.Count(s=>s.Floating),Is.InRange(1,MemoryClouds.RuinFloaters),"a few afloat");
            // Near, mid and far: some in every third of the band, whatever the seed.
            float third=(MemoryClouds.RuinFar-MemoryClouds.RuinNear)/3;
            for(int t=0;t<3;t++)Assert.That(spots.Count(s=>(int)Mathf.Min(2,(s.Distance-MemoryClouds.RuinNear)/third)==t),Is.GreaterThanOrEqualTo(MemoryClouds.RuinCount/6),$"ruins in distance third {t}");
            Assert.AreEqual(.5f,MemoryClouds.Halton(1,2),1e-6f);Assert.AreEqual(.75f,MemoryClouds.Halton(3,2),1e-6f);
            // All the way round, behind the player too: some in every quarter.
            for(int quarter=0;quarter<4;quarter++)
                Assert.IsTrue(spots.Any(s=>{var r=s.Position-new Vector3(Eye.x,s.Position.y,Eye.y);float a=Mathf.Repeat(Mathf.Atan2(r.x,r.z)*Mathf.Rad2Deg+45,360);return (int)(a/90)==quarter;}),$"a ruin in quarter {quarter} (0 ahead, 2 behind)");
            // Every piece at least once; pillars, columns and arches (weight 2) outnumber the arcade, wall and temple.
            foreach(int i in Enumerable.Range(0,Kit.Length).Where(i=>Kit[i].Kind==Kind.Ruin))Assert.IsTrue(spots.Any(s=>s.Piece==i),$"ruin piece {i} placed");
            Assert.That(spots.Count(s=>Kit[s.Piece].Weight>1),Is.GreaterThan(spots.Count(s=>Kit[s.Piece].Weight<=1)),"mostly pillars, columns and arches");
            foreach(var s in spots)
            {
                var piece=Kit[s.Piece];float height=piece.Size.y*s.Scale;var rel=s.Position-new Vector3(Eye.x,s.Position.y,Eye.y);
                Assert.That(s.Distance,Is.InRange(MemoryClouds.RuinNear,MemoryClouds.RuinFar),"ruin distance");
                float azimuth=Mathf.Atan2(rel.x,rel.z)*Mathf.Rad2Deg,toEyes=Mathf.Atan2(-rel.x,-rel.z)*Mathf.Rad2Deg;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(azimuth,glow)),Is.GreaterThanOrEqualTo(MemoryClouds.RuinGlowGap),"clear of the glow");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(s.Yaw,toEyes)),Is.LessThanOrEqualTo(MemoryClouds.RuinTurn+1e-3f),"facing the eyes");
                Assert.That(Mathf.Atan2(s.Top-MemoryClouds.EyeHeight,s.Distance)*Mathf.Rad2Deg,Is.LessThanOrEqualTo(MemoryClouds.RuinTopElevation+1e-2f),"low on the horizon");
                if(s.Floating)
                {
                    Assert.IsTrue(piece.Floats,"only a broken piece floats");
                    Assert.That(s.Position.y-seaTop,Is.InRange(MemoryClouds.FloatAbove.x,MemoryClouds.FloatAbove.y),"afloat over the sea");
                    Assert.That(Mathf.Abs(s.Tilt.x),Is.LessThanOrEqualTo(MemoryClouds.FloatTilt));Assert.That(Mathf.Abs(s.Tilt.z),Is.LessThanOrEqualTo(MemoryClouds.FloatTilt));
                }
                else
                {
                    Assert.That((seaTop-s.Position.y)/height,Is.InRange(MemoryClouds.RuinSink.x-1e-3f,MemoryClouds.RuinDeepSink+1e-3f),"base sunk into the sea");
                    Assert.AreEqual(Vector3.zero,s.Tilt,"standing upright");
                }
            }
        }

        // A swelling sea (MemoryClouds.Shape): its tops rise and fall by up to SeaSwell, never into the play space, and the
        // puffs, banks and ruins sit on the sea where they stand; a few puffs float clear of it, still under the ceiling.
        [Test] public void TheSeaSwellsAndAFewPuffsLift([Values(false,true)] bool underFeet,[Values(1,5,42)] int seed)
        {
            float seaTop=MemoryClouds.SeaTop(underFeet);
            var shape=MemoryClouds.Shape.Flat;shape.SeaSwell=3;shape.Lifted=6;
            var spots=MemoryClouds.Layout(Kit,Eye,seaTop,0,seed,shape,MemoryClouds.RuinLayout.Standing);
            var sea=spots.Where(s=>s.Kind==Kind.Sea).Select(s=>s.Top).ToArray();
            Assert.That(sea.Min(),Is.GreaterThanOrEqualTo(seaTop-shape.SeaSwell-.5f-1e-3f),"no deeper than the swell");
            Assert.That(sea.Max(),Is.LessThanOrEqualTo(underFeet?seaTop+1e-3f:seaTop+shape.SeaSwell+1e-3f),underFeet?"under the feet it only falls":"no higher than the swell");
            Assert.Greater(sea.Max()-sea.Min(),1.5f,"it swells");
            foreach(var s in spots)
            {
                float near=s.Distance-s.Reach;
                if(near<=MemoryClouds.KeepOut)Assert.That(s.Top,Is.LessThan(0),$"{s.Kind} {near:0.0} m from the eyes under the floor");
                if(s.Kind!=Kind.Sea)Assert.That(s.Top,Is.LessThanOrEqualTo(MemoryClouds.Ceiling(near)+1e-3f),$"{s.Kind} under its ceiling");
            }
            var lifted=spots.Where(s=>s.Kind==Kind.Puff&&s.Floating).ToArray();
            Assert.That(lifted.Length,Is.InRange(3,shape.Lifted),"a few lifted puffs");
            foreach(var s in lifted)Assert.That(s.Position.y-MemoryClouds.SwellTop(seaTop,shape,s.Position),Is.InRange(shape.LiftedHeight.x-1e-3f,shape.LiftedHeight.y+1e-3f),"clear of the sea");
        }

        // Ruins that lean a little (RuinLayout.Lean) and float low (FloatAbove), any piece afloat (AnyFloats).
        [Test] public void RuinsLeanAndFloatLow([Values(1,5,42)] int seed)
        {
            float seaTop=MemoryClouds.SeaTop(false);
            var layout=MemoryClouds.RuinLayout.Standing;layout.Lean=12;layout.FloatAbove=new Vector2(2,8);layout.AnyFloats=true;
            var spots=MemoryClouds.Layout(Kit.Select(p=>p.Kind==Kind.Ruin?new MemoryClouds.Piece(p.Kind,p.Size,true,p.Weight):p).ToArray(),Eye,seaTop,0,seed,MemoryClouds.Shape.Flat,layout).Where(s=>s.Kind==Kind.Ruin).ToArray();
            Assert.That(spots.Length,Is.GreaterThanOrEqualTo(layout.Count*.8f),"ruins");
            Assert.That(spots.Count(s=>s.Floating),Is.InRange(1,layout.Floaters),"a few afloat");
            Assert.IsTrue(spots.Any(s=>!s.Floating&&s.Tilt!=Vector3.zero),"standing ones lean");
            foreach(var s in spots)
            {
                if(s.Floating)Assert.That(s.Position.y-seaTop,Is.InRange(layout.FloatAbove.x-1e-3f,layout.FloatAbove.y+1e-3f),"afloat, low");
                else{Assert.That(Mathf.Abs(s.Tilt.x),Is.LessThanOrEqualTo(layout.Lean));Assert.That(Mathf.Abs(s.Tilt.z),Is.LessThanOrEqualTo(layout.Lean));}
            }
        }

        // The flat sea and standing ruins are what they were before Shape and RuinLayout existed.
        [Test] public void TheDefaultsKeepTheLayout()
        {
            var a=MemoryClouds.Layout(Kit,Eye,MemoryClouds.SeaTop(false),0,MemoryClouds.Seed);
            var b=MemoryClouds.Layout(Kit,Eye,MemoryClouds.SeaTop(false),0,MemoryClouds.Seed,MemoryClouds.Shape.Flat,MemoryClouds.RuinLayout.Standing);
            Assert.AreEqual(a.Length,b.Length);
            for(int i=0;i<a.Length;i++){Assert.AreEqual(a[i].Position,b[i].Position);Assert.AreEqual(a[i].Tilt,b[i].Tilt);}
            Assert.IsFalse(a.Any(s=>s.Kind==Kind.Puff&&s.Floating),"no lifted puffs");
        }

        [Test] public void KindsComeFromNames()
        {
            Assert.AreEqual(Kind.Ruin,MemoryClouds.KindOf("Ruin_Arch"));Assert.IsTrue(MemoryClouds.Floats("Ruin_ColumnBroken"));
            Assert.IsFalse(MemoryClouds.Floats("Ruin_Temple"));Assert.IsFalse(MemoryClouds.Floats("Puff_A"));
            Assert.AreEqual(2,MemoryClouds.Weight("Ruin_ArchBroken"));Assert.AreEqual(2,MemoryClouds.Weight("Ruin_Column"));Assert.AreEqual(1,MemoryClouds.Weight("Ruin_Arcade"));
            Assert.AreEqual(Kind.Sea,MemoryClouds.KindOf("Sea_A"));Assert.AreEqual(Kind.Puff,MemoryClouds.KindOf("Puff_C"));
            Assert.AreEqual(Kind.Mid,MemoryClouds.KindOf("Tower_B"));Assert.AreEqual(Kind.Mid,MemoryClouds.KindOf("Bank_A"));
            Assert.IsNull(MemoryClouds.KindOf("Cube"));
        }

        // The sky and the dark draw after every opaque thing (so the depth test skips what clouds and the room cover),
        // the dark over the sky, both before anything see-through.
        [Test] public void SkyDrawsAfterOpaqueGeometry()
        {
            Assert.That(MemorySky.SkyQueue,Is.GreaterThan((int)RenderQueue.AlphaTest));
            Assert.That(MemorySky.DarkQueue,Is.GreaterThan(MemorySky.SkyQueue));
            Assert.That(MemorySky.DarkQueue,Is.LessThanOrEqualTo((int)RenderQueue.GeometryLast));
        }
    }
}
