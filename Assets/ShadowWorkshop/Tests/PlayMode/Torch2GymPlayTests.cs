using System.Collections;
using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowWorkshopIntegration.Tests
{
    // The gym's Play mode: the things fly in from the table (scattered here: no table layout), and only once they have
    // landed do the rings close round them and the lamps come on; then the orb is the player's to turn and save views of.
    public class Torch2GymPlayTests
    {
        GameObject root;
        [TearDown] public void Clean(){if(root)Object.Destroy(root);}

        Torch2Gym Gym()
        {
            root=new GameObject("Play test gym");root.SetActive(false);var gym=root.AddComponent<Torch2Gym>();
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));gym.surfaceMaterial=gym.screenMaterial=gym.glowMaterial=material;
            gym.cluster=new GameObject("Cluster").transform;gym.cluster.SetParent(root.transform,false);gym.id="GymPlayTest";
            gym.autoStart=false;gym.start=Quaternion.Euler(30,120,10);
            Piece(gym.cluster,PrimitiveType.Cube,new Vector3(0,-.04f,0),new Vector3(.12f,.05f,.08f));
            Piece(gym.cluster,PrimitiveType.Sphere,new Vector3(.02f,.05f,0),Vector3.one*.07f);
            Piece(gym.cluster,PrimitiveType.Cylinder,new Vector3(-.07f,.02f,.03f),new Vector3(.03f,.04f,.03f));
            return gym;
        }
        static void Piece(Transform parent,PrimitiveType type,Vector3 position,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(type);Object.Destroy(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        }
        static Transform Orb(Torch2Gym gym)=>gym.transform.Find("Gym room (generated, not saved)/Orb");

        [UnityTest] public IEnumerator WithoutATableLayoutThingsScatterFlyInThenTheRingsClose()
        {
            var gym=Gym();var pieces=new Vector3[3];for(int i=0;i<3;i++)pieces[i]=gym.cluster.GetChild(i).localPosition;
            root.SetActive(true);yield return null;
            Assert.IsTrue(gym.Transiting,"Play mode opens with the table to orb, table layout or not");
            var orb=Orb(gym);Assert.NotNull(orb);Assert.IsFalse(orb.gameObject.activeSelf,"no rings while the things fly");
            Assert.Greater(Vector3.Distance(gym.cluster.GetChild(0).localPosition,pieces[0]),.05f,"on the table, not in the orb");
            Assert.Less(Quaternion.Angle(gym.cluster.localRotation,gym.start),.01f,"they come in at the start rotation");
            float length=gym.TransitLength,deadline=Time.realtimeSinceStartup+length+5;bool ringsBeforeLanding=false;
            while(gym.Transiting&&Time.realtimeSinceStartup<deadline)
            {
                // Rings showing while any piece is still away from its place would be too early.
                if(orb.gameObject.activeSelf)for(int i=0;i<3;i++)if(Vector3.Distance(gym.cluster.GetChild(i).localPosition,pieces[i])>.002f)ringsBeforeLanding=true;
                yield return null;
            }
            Assert.IsFalse(gym.Transiting,"finished");Assert.IsFalse(ringsBeforeLanding,"the rings close only after everything has landed");
            Assert.IsTrue(orb.gameObject.activeSelf);Assert.AreEqual(Vector3.one,orb.localScale);
            for(int i=0;i<3;i++)Assert.AreEqual(pieces[i],gym.cluster.GetChild(i).localPosition,"every piece back in its place");
            // The orb is the player's now; V's save keeps whatever it is turned to.
            gym.cluster.localRotation=Quaternion.Euler(5,60,0);yield return null;
            int k=gym.SaveView();Assert.Less(Quaternion.Angle(gym.views[k].rotation,Quaternion.Euler(5,60,0)),.01f);
            gym.ShowView(k);Assert.IsFalse(gym.showingView,"Play mode needs no way back: it ends where edit mode left the orb");
        }
        [UnityTest] public IEnumerator SkipIntroLandsEverythingAtOnceAndPlayIntroOffNeverShowsIt()
        {
            var gym=Gym();var pieces=new Vector3[3];for(int i=0;i<3;i++)pieces[i]=gym.cluster.GetChild(i).localPosition;
            root.SetActive(true);yield return null;Assert.IsTrue(gym.Transiting);
            gym.SkipTransit();
            Assert.IsFalse(gym.Transiting);Assert.IsTrue(Orb(gym).gameObject.activeSelf);Assert.AreEqual(Vector3.one,Orb(gym).localScale);
            for(int i=0;i<3;i++)Assert.AreEqual(pieces[i],gym.cluster.GetChild(i).localPosition,"every piece in its place");
            Assert.Less(Quaternion.Angle(gym.cluster.localRotation,gym.start),.01f,"at the start rotation, as after the whole intro");
            Object.Destroy(root);yield return null;
            gym=Gym();gym.playIntro=false;root.SetActive(true);yield return null;
            Assert.IsFalse(gym.Transiting,"Play Intro off: the orb is up from the first frame");Assert.IsTrue(Orb(gym).gameObject.activeSelf);
            Assert.Less(Quaternion.Angle(gym.cluster.localRotation,gym.start),.01f);
        }
    }
}
