using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CloudGarden.ShadowPuzzle;

namespace CloudGarden.Tests
{
    public sealed class Torch2CueTests
    {
        static readonly string[] Ids={"Birthday","BonVoyage","GoodMorning","Home"};
        static readonly Vector3 Tiers=new Vector3(.35f,.60f,.80f);
        const float Frame=1/72f;
        // A steady reading for this long; returns how many times the tier went up.
        static int Hold(Torch2Tier t,float warmth,float seconds,bool passing=false,bool lookalike=false)
        {
            int ups=0;for(int i=0;i<Mathf.CeilToInt(seconds/Frame);i++)if(t.Update(warmth,passing,lookalike,Tiers,Frame)>0)ups++;return ups;
        }

        [Test] public void TiersRiseAtThresholdsAndFallOnlyPastTheHysteresis()
        {
            var t=new Torch2Tier();
            Hold(t,.34f,1);Assert.AreEqual(0,t.Tier);
            Hold(t,.36f,1);Assert.AreEqual(1,t.Tier);
            Hold(t,.65f,1);Assert.AreEqual(2,t.Tier);
            Hold(t,.55f,2);Assert.AreEqual(2,t.Tier,"inside the hysteresis band");
            Hold(t,.50f,2);Assert.AreEqual(1,t.Tier);
            Hold(t,.97f,1);Assert.AreEqual(3,t.Tier,"not passing tops out at 3");
        }
        [Test] public void ChangesWaitLongerGoingDownThanUp()
        {
            var t=new Torch2Tier();
            Hold(t,.65f,Torch2Tier.UpDwell*.8f);Assert.AreEqual(0,t.Tier);
            Hold(t,.65f,Torch2Tier.UpDwell*.4f);Assert.AreEqual(2,t.Tier,"straight to the tier the warmth reaches");
            Hold(t,.1f,Torch2Tier.UpDwell*1.2f);Assert.AreEqual(2,t.Tier,"a short dip does not drop it");
            Hold(t,.1f,Torch2Tier.DownDwell);Assert.AreEqual(0,t.Tier);
        }
        [Test] public void TremorAcrossAThresholdNeverChangesTheTier()
        {
            // .04 either side of the 0.60 threshold, switching at the 10 Hz scoring rate, from below and from above.
            foreach(float settle in new[]{.40f,.70f})
            {
                var t=new Torch2Tier();Hold(t,settle,1);int before=t.Tier,changes=0;
                for(int i=0;i<60;i++)for(int f=0;f<7;f++)if(t.Update(i%2==0?.56f:.64f,false,false,Tiers,Frame)!=0)changes++;
                Assert.AreEqual(0,changes,"settled at "+settle);Assert.AreEqual(before,t.Tier);
            }
        }
        [Test] public void LookalikesStopAtTierOneAndPassingIsImmediate()
        {
            var t=new Torch2Tier();Hold(t,.95f,1,lookalike:true);Assert.AreEqual(1,t.Tier);
            Assert.AreEqual(1,t.Update(.5f,true,true,Tiers,Frame));Assert.AreEqual(4,t.Tier,"passing wins, even far from the designed answer");
        }
        [Test] public void ShownEasesUpWithoutOvershoot()
        {
            var t=new Torch2Tier();float last=0;
            for(int i=0;i<144;i++){t.Update(1,true,false,Tiers,Frame);Assert.GreaterOrEqual(t.Shown,last);Assert.LessOrEqual(t.Shown,4);last=t.Shown;}
            Assert.AreEqual(4,t.Shown,"settled within two seconds");
            t.Reset();Assert.AreEqual(0,t.Tier);Assert.AreEqual(0,t.Shown);
        }
        [Test] public void EveryTierLooksAndSoundsClearer()
        {
            Torch2Cue.Look(0,out float blur,out float saturation,out float mix);float cutoff=Torch2Cue.CutoffAt(0);
            for(float tier=.25f;tier<=4;tier+=.25f)
            {
                Torch2Cue.Look(tier,out float b,out float s,out float m);float c=Torch2Cue.CutoffAt(tier);
                Assert.Less(b,blur+1e-6f);Assert.Greater(s,saturation-1e-6f);Assert.Greater(m,mix-1e-6f);Assert.Greater(c,cutoff);
                blur=b;saturation=s;mix=m;cutoff=c;
            }
            Torch2Cue.Look(4,out blur,out saturation,out mix);Assert.AreEqual(0,blur);Assert.AreEqual(1,saturation);Assert.AreEqual(1,mix);
            Assert.AreEqual(22000,Torch2Cue.CutoffAt(4),1);
        }
        [Test] public void LayersJoinOnePerTier()
        {
            Assert.Greater(Torch2Cue.LayerVolume(0,4,0),0,"the first layer always plays");
            for(int layer=1;layer<4;layer++){Assert.AreEqual(0,Torch2Cue.LayerVolume(layer,4,layer-1));Assert.AreEqual(1,Torch2Cue.LayerVolume(layer,4,layer));}
            Assert.AreEqual(0,Torch2Cue.LayerVolume(1,2,1.9f));Assert.AreEqual(1,Torch2Cue.LayerVolume(1,2,3),"with two layers the second joins at tier 3");
        }
        [Test] public void PlaceholderLayersAreSeamlessLoopsOfOneLength()
        {
            var layers=Torch2Placeholders.Layers();Assert.AreEqual(4,layers.Length);
            foreach(var clip in layers)
            {
                Assert.AreEqual(layers[0].samples,clip.samples,clip.name);var data=new float[clip.samples];clip.GetData(data,0);
                float peak=0,step=0;for(int i=0;i<data.Length;i++){peak=Mathf.Max(peak,Mathf.Abs(data[i]));if(i>0)step=Mathf.Max(step,Mathf.Abs(data[i]-data[i-1]));}
                Assert.LessOrEqual(peak,.5f,clip.name);Assert.LessOrEqual(Mathf.Abs(data[0]-data[data.Length-1]),step+1e-4f,clip.name+": the loop point is no bigger than an ordinary step");
            }
            Assert.AreSame(layers,Torch2Placeholders.Layers(),"generated once");
        }
        [Test] public void PlaceholderSlidesCanBlurAndDifferPerWall()
        {
            var a=Torch2Placeholders.Slide("Level/0",Color.red);var again=Torch2Placeholders.Slide("Level/0",Color.red);var b=Torch2Placeholders.Slide("Level/1",Color.red);
            try
            {
                Assert.Greater(a.mipmapCount,5,"coarse mips to blur with");
                Assert.IsTrue(a.GetPixels32().SequenceEqual(again.GetPixels32()),"same level and wall, same slide");
                Assert.IsFalse(a.GetPixels32().SequenceEqual(b.GetPixels32()));
            }
            finally{Object.DestroyImmediate(a);Object.DestroyImmediate(again);Object.DestroyImmediate(b);}
        }
        [TestCaseSource(nameof(Ids))] public void ShippedLevelsStartColdAndSnapFromATypicalNearMiss(string id)
        {
            var level=Torch2Level.Load(id);var p=new Torch2Projection(level);
            p.Render(p.Start,Torch2Projection.Home);p.Evaluate();bool far=p.AngleToAnswer(p.Start)>level.lookalikeAngle;
            Assert.LessOrEqual(Torch2Tier.Target(p.Score.warmth,p.Score.Ready,far,level.tiers,0),1,"the start reads cold or stirring");
            p.Render(p.Solution,Torch2Projection.Home);p.Evaluate();Assert.AreEqual(4,Torch2Tier.Target(p.Score.warmth,p.Score.Ready,false,level.tiers,0));
            // Released 10 degrees off about a random axis, the median pose is warm enough to snap.
            var random=new System.Random(5);var warmth=new float[25];
            for(int i=0;i<warmth.Length;i++)
            {
                var axis=new Vector3((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1).normalized;
                p.Render(Quaternion.AngleAxis(10,axis)*p.Solution,Torch2Projection.Home);p.Evaluate();warmth[i]=p.Score.Ready?1:p.Score.warmth;
            }
            System.Array.Sort(warmth);Assert.GreaterOrEqual(warmth[warmth.Length/2],level.snapWarmth);
        }
    }
}
