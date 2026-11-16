using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    public class MemoryFogTests
    {
        const string Looks="Assets/ShadowWorkshop/PicoFinal/Looks/";
        const string Sea="Assets/ShadowWorkshop/PicoFinal/Art/Art Assets/Materials/MT_CloudSea.mat";

        [TearDown] public void Off()=>MemoryFog.Reset();

        // Off, the environment's materials keep their own haze; on, its heights go up from the floor under the stage.
        [Test] public void PublishesItsHeightsFromTheFloor()
        {
            var fog=new MemoryFog();
            fog.Publish(3,null);
            Assert.AreEqual(0,Shader.GetGlobalFloat("_PicoFinalFog"),"off: each material's own haze");
            fog.On=true;fog.Publish(3,null);
            Assert.AreEqual(1,Shader.GetGlobalFloat("_PicoFinalFog"));
            var mist=Shader.GetGlobalVector("_PicoFinalFogMist");var wrap=Shader.GetGlobalVector("_PicoFinalFogWrap");var far=Shader.GetGlobalVector("_PicoFinalFogFar");
            Assert.AreEqual(3+fog.MistTop,mist.x,1e-4f);Assert.AreEqual(fog.MistDepth,mist.y,1e-4f);
            Assert.AreEqual(3+fog.WrapTop,wrap.x,1e-4f);Assert.AreEqual(3+fog.WrapBottom,wrap.y,1e-4f);
            Assert.AreEqual(new Vector4(fog.Start,fog.Full,fog.Amount,fog.EaseOut),far);
            var tint=Shader.GetGlobalColor("_PicoFinalFogTint");
            Assert.AreEqual(fog.ColourShare,tint.a,1e-4f,"the fog's own colour's share rides in alpha");
            // Global colours reach the shaders untouched, and they work in linear: the picked colour goes over linear.
            Assert.AreEqual(fog.Colour.linear.r,tint.r,1e-3f);Assert.AreEqual(fog.Colour.linear.b,tint.b,1e-3f);
            MemoryFog.Reset();
            Assert.AreEqual(0,Shader.GetGlobalFloat("_PicoFinalFog"));
        }

        // The defaults are the game's own fog of 2026-11-12 (Cloud.shader: 25 to 400 m, 0.9, ease-out; the sea's mist from
        // 1.5 m under its tops, full 4.5 m lower), with the wrap round the tops (about 8 m under the floor).
        [Test] public void DefaultsAreTheElevenTwelveFog()
        {
            var fog=new MemoryFog();
            Assert.AreEqual(25,fog.Start);Assert.AreEqual(400,fog.Full);Assert.AreEqual(.9f,fog.Amount,1e-4f);Assert.AreEqual(1,fog.EaseOut);
            Assert.AreEqual(-9.5f,fog.MistTop,1e-4f);Assert.AreEqual(4.5f,fog.MistDepth,1e-4f);
            Assert.Greater(fog.WrapTop,-8);Assert.Less(fog.WrapBottom,-8,"the wrap reaches round the cloud tops");
            // Its colour: the sky right above (the band just over the horizon; below it the skies go a flat dark purple),
            // the user's pick over one colour for all the fog.
            Assert.AreEqual(0,fog.ColourShare,"the sky's own colour");
            Assert.IsTrue(fog.ColourToSkyFar,"a colour of its own, if given, still melts into the sky far out");
            Assert.Greater(fog.SkyAbove,0,"reads the sky over the horizon");
            Assert.AreEqual(1,fog.MistSky);Assert.AreEqual(1,fog.WrapSky);
            fog.On=true;fog.Publish(0,null);
            Assert.AreEqual(Mathf.Sin(fog.SkyAbove*Mathf.Deg2Rad),Shader.GetGlobalVector("_PicoFinalFogMist").w,1e-5f,"as a sine");
        }

        // The buildings fade into the colour the sea's shader gives its tops: between its body and lit colours (the
        // low sun lights an upward face about half way up the ramp).
        [Test] public void CloudTopsColourComesFromTheSeasRamp()
        {
            var sea=AssetDatabase.LoadAssetAtPath<Material>(Sea);
            Assert.IsTrue(sea,Sea);
            var top=MemoryFog.CloudTop(sea);
            Color body=sea.GetColor("_Body"),lit=sea.GetColor("_Lit");
            for(int i=0;i<3;i++)Assert.That(top[i],Is.InRange(Mathf.Min(body[i],lit[i])-.02f,Mathf.Max(body[i],lit[i])+.02f),"channel "+i);
            sea=new Material(sea);sea.SetColor("_Lit",Color.green);
            Assert.Greater(MemoryFog.CloudTop(sea).g,top.g,"follows the material's colours");
            Object.DestroyImmediate(sea);
        }

        // The environment artist's looks take the one fog; ours keep their own (our cloud kit has its own haze).
        [Test] public void OnlyTheEnvironmentLooksTakeTheFog()
        {
            foreach(var (name,on) in new[]{("Env art",true),("Env art, our fog",true),("Ours",false),("Blend",false),("Mixed",false)})
            {
                var look=AssetDatabase.LoadAssetAtPath<MemoryLook>(Looks+name+".asset");
                Assert.IsTrue(look,name);
                Assert.AreEqual(on,look.Fog!=null&&look.Fog.On,name);
            }
        }
    }
}
