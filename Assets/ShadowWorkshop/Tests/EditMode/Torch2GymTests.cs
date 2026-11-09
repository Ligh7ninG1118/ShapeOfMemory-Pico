using System.Collections.Generic;
using System.Linq;
using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using ShadowWorkshopIntegration.Editor;
using UnityEditor;
using UnityEngine;

namespace CloudGarden.Tests
{
    public sealed class Torch2GymTests
    {
        const string Id="GymTestLevel";
        readonly List<Object> made=new List<Object>();
        // The test gym is made and destroyed through Undo, like Open in gym's own changes. The test runner reverts the
        // undo history after a run; objects destroyed outside it would come back from that revert at the scene root.
        [TearDown] public void Clean()
        {
            foreach(var o in made)if(o){if(o is GameObject go)Undo.DestroyObjectImmediate(go);else Object.DestroyImmediate(o);}made.Clear();
            AssetDatabase.DeleteAsset($"{Torch2GymTools.LevelFolder}/{Id}.asset");AssetDatabase.DeleteAsset($"{Torch2GymTools.ShadowFolder}/{Id}");
        }
        static Mesh Builtin(PrimitiveType type){var go=GameObject.CreatePrimitive(type);var mesh=go.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(go);return mesh;}
        Torch2Level Level(bool bothFaces,params Torch2Piece[] pieces){var l=ScriptableObject.CreateInstance<Torch2Level>();made.Add(l);l.lamps=new[]{0};l.pieces=pieces;l.bothFaces=bothFaces;return l;}
        static int Area(Torch2Level level){var p=new Torch2Projection(level,false);p.Render(Quaternion.identity,Torch2Projection.Home);return p.shadow[0].Count(b=>b);}

        [Test] public void PieceScaleCountsAndZeroMeansOne()
        {
            var cube=Builtin(PrimitiveType.Cube);
            int small=Area(Level(false,new Torch2Piece{mesh=cube,scale=Vector3.one*.1f})),tall=Area(Level(false,new Torch2Piece{mesh=cube,scale=new Vector3(.1f,.2f,.1f)}));
            Assert.Greater(small,30);Assert.AreEqual(2,tall/(float)small,.25f,"twice as tall, twice the shadow");
            Assert.AreEqual(Vector3.one,new Torch2Piece().Scale);
        }
        [Test] public void VisualOnlyPiecesCastNothing()
        {
            var cube=Builtin(PrimitiveType.Cube);var piece=new Torch2Piece{mesh=cube,scale=Vector3.one*.1f};var visual=new Torch2Piece{mesh=cube,scale=Vector3.one*.15f,position=new Vector3(.1f,0,0),visualOnly=true};
            Assert.AreEqual(0,Area(Level(false,visual)));Assert.AreEqual(Area(Level(false,piece)),Area(Level(false,piece,visual)));
        }
        [Test] public void SingleSidedMeshesCastFromBehindWithBothFaces()
        {
            // A quad facing the lamp casts; turned away it casts only when both faces count.
            var quad=Builtin(PrimitiveType.Quad);var n=Torch2Projection.WallNormal[0];
            Torch2Piece Facing(Vector3 d)=>new Torch2Piece{mesh=quad,scale=Vector3.one*.2f,euler=Quaternion.LookRotation(d).eulerAngles};
            int a=Area(Level(false,Facing(n))),b=Area(Level(false,Facing(-n)));
            Assert.IsTrue((a>0)!=(b>0),$"one side only: {a} / {b}");
            int both=Area(Level(true,Facing(n))),bothBack=Area(Level(true,Facing(-n)));
            Assert.Greater(both,30);Assert.AreEqual(both,bothBack,both/20+2);
        }
        [Test] public void ReadableCopyKeepsTheShape()
        {
            var sphere=Builtin(PrimitiveType.Sphere);var copy=Torch2Gym.ReadableCopy(sphere);made.Add(copy);
            Assert.IsTrue(copy.isReadable);Assert.AreEqual(sphere.vertexCount,copy.vertexCount);Assert.AreEqual(sphere.triangles.Length,copy.triangles.Length);
        }

        Torch2Gym Gym()
        {
            var root=new GameObject("Test gym");Undo.RegisterCreatedObjectUndo(root,"Test gym");made.Add(root);root.SetActive(false);var gym=root.AddComponent<Torch2Gym>();
            gym.surfaceMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Graybox surfaces.mat");
            gym.screenMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Projection screen.mat");
            gym.glowMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Tracked hands and lamp.mat");
            gym.cluster=new GameObject("Cluster").transform;gym.cluster.SetParent(root.transform,false);gym.id=Id;root.SetActive(true);return gym;
        }
        static GameObject Piece(Transform parent,PrimitiveType type,Vector3 position,Vector3 euler,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(type);Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localEulerAngles=euler;go.transform.localScale=scale;return go;
        }
        [Test] public void GymPinsTheOrbAndGathersTaggedPieces()
        {
            var gym=Gym();gym.cluster.localPosition=Vector3.one;
            var body=Piece(gym.cluster,PrimitiveType.Cube,Vector3.zero,new Vector3(0,30,0),new Vector3(.16f,.10f,.06f));
            var arm=new GameObject("Arm").transform;arm.SetParent(body.transform,false);
            var mirrored=Piece(gym.cluster,PrimitiveType.Sphere,new Vector3(.12f,.05f,0),Vector3.zero,new Vector3(-.06f,.06f,.06f));
            var decor=Piece(gym.cluster,PrimitiveType.Cylinder,new Vector3(-.1f,-.05f,0),Vector3.zero,Vector3.one*.04f);decor.AddComponent<ShadowPiece>().role=ShadowPiece.Role.VisualOnly;
            var proxied=Piece(gym.cluster,PrimitiveType.Sphere,new Vector3(0,.1f,.05f),Vector3.zero,Vector3.one*.05f);proxied.AddComponent<ShadowPiece>().shadowProxy=Builtin(PrimitiveType.Cube);
            gym.Refresh(true);
            Assert.AreEqual(Torch2Projection.Home,gym.cluster.localPosition,"the orb stays at the rest position");
            Assert.AreEqual(4,gym.Pieces.Count);Assert.AreEqual(1,gym.Pieces.Count(p=>p.visualOnly));Assert.NotNull(gym.Projection);
            Assert.AreEqual(Builtin(PrimitiveType.Cube),gym.Pieces.First(p=>p.source==proxied).shadow,"proxy casts instead");
            Assert.AreEqual(-.06f,gym.Pieces.First(p=>p.source==mirrored).scale.x,1e-5f,"mirror kept");
            // The preview answer is its own target.
            var p2=gym.Projection;p2.Render(Quaternion.identity,Torch2Projection.Home);p2.Evaluate();Assert.IsTrue(p2.Score.Ready);
        }
        [Test] public void CheckRejectsBadIdsScriptLevelsAndEmptyClusters()
        {
            var gym=Gym();gym.id="2 bad";Assert.IsFalse(Torch2GymTools.Check(gym,20).Ok);
            Piece(gym.cluster,PrimitiveType.Cube,Vector3.zero,Vector3.zero,Vector3.one*.1f);
            gym.id="Birthday";Assert.IsFalse(Torch2GymTools.Check(gym,20).Ok,"owned by build_torch2.py");
            gym.id=Id;Assert.IsTrue(Torch2GymTools.Check(gym,20).Ok,gym.report);
        }
        [Test] public void ExportThenOpenGivesTheSamePile()
        {
            var gym=Gym();
            Piece(gym.cluster,PrimitiveType.Cube,new Vector3(-.04f,-.02f,0),new Vector3(0,20,10),new Vector3(.18f,.08f,.06f));
            Piece(gym.cluster,PrimitiveType.Sphere,new Vector3(.09f,.07f,0),Vector3.zero,new Vector3(.08f,.08f,.08f));
            Piece(gym.cluster,PrimitiveType.Cylinder,new Vector3(-.10f,.08f,.02f),new Vector3(0,0,35),new Vector3(.03f,.07f,.03f));
            Piece(gym.cluster,PrimitiveType.Capsule,new Vector3(.02f,-.10f,0),new Vector3(0,0,90),new Vector3(.04f,.08f,.04f));
            var level=Torch2GymTools.Export(gym,200,false);Assert.NotNull(level,gym.report);
            Assert.AreEqual(Torch2Level.Load(Id),level,"exported into Resources");Assert.AreEqual(4,level.pieces.Length);Assert.IsTrue(level.bothFaces);
            Assert.AreEqual(Quaternion.identity,level.solution);Assert.That(level.iouMin,Is.InRange(.62f,.95f));
            var p=new Torch2Projection(level);p.Render(level.solution,Torch2Projection.Home);p.Evaluate();Assert.IsTrue(p.Score.Ready);
            p.Render(level.start,Torch2Projection.Home);p.Evaluate();Assert.IsFalse(p.Score.Ready,"the start is not solved");
            var before=gym.Pieces.Select(x=>(x.position,x.rotation,x.scale)).ToList();
            Torch2GymTools.Load(gym,level,false);gym.Refresh(true);
            Assert.AreEqual(4,gym.Pieces.Count);
            for(int i=0;i<4;i++)
            {
                Assert.Less(Vector3.Distance(before[i].position,gym.Pieces[i].position),1e-4f);Assert.Less(Quaternion.Angle(before[i].rotation,gym.Pieces[i].rotation),.01f);
                Assert.Less(Vector3.Distance(before[i].scale,gym.Pieces[i].scale),1e-4f);
            }
            Assert.IsFalse(gym.autoStart);Assert.Less(Quaternion.Angle(level.start,gym.start),.01f);
            Assert.IsTrue(gym.autoThreshold,"the threshold is set again for the pile it becomes");
        }
        // A flat card with a tab, facing the left lamp. Turned round about the room's up, the lamp's line or the card's own
        // width, its shadow is the card with the tab misplaced: close enough to pass a 0.62 threshold.
        static readonly Quaternion Face=Quaternion.LookRotation(Torch2Projection.WallNormal[0]);
        static readonly Vector3[] TurnedRound={Vector3.up,Torch2Projection.WallNormal[0],Face*Vector3.right};
        static void Card(Transform parent)
        {
            Piece(parent,PrimitiveType.Cube,Vector3.zero,Face.eulerAngles,new Vector3(.22f,.14f,.012f));
            Piece(parent,PrimitiveType.Cube,Face*new Vector3(.07f,.095f,0),Face.eulerAngles,new Vector3(.06f,.05f,.012f));
        }
        [Test] public void ClimbFindsTheTurnedRoundLookalike()
        {
            var gym=Gym();Card(gym.cluster);gym.Refresh(true);var level=Object.Instantiate(gym.Preview);made.Add(level);level.iouMin=.62f;level.flips=Torch2Flips.None;var p=new Torch2Projection(level);
            var mirror=Quaternion.AngleAxis(180,Vector3.up);var seed=Quaternion.AngleAxis(12,new Vector3(1,1,0).normalized)*mirror;
            p.Render(seed,Torch2Projection.Home);p.Evaluate();float start=p.Score.iou;
            var found=Torch2GymTools.Climb(p,level,seed);
            Assert.Greater(found.iou,start+.01f,"it climbs");Assert.Less(Quaternion.Angle(found.pose,mirror),10,"to the turned-round pose");
            Assert.AreEqual(0,Torch2GymTools.Climb(p,level,Quaternion.AngleAxis(level.lookalikeAngle+5,Vector3.up)).iou,"heading for the answer itself counts nothing");
        }
        [Test] public void AutoThresholdKeepsTurnedRoundLookalikesFromPassing()
        {
            var gym=Gym();Card(gym.cluster);gym.flips=Torch2Flips.None;Assert.IsTrue(Torch2GymTools.Check(gym,300).Ok,gym.report);
            var level=Object.Instantiate(gym.Preview);made.Add(level);level.iouMin=gym.iouMin;var p=new Torch2Projection(level);
            foreach(var axis in TurnedRound){p.Render(Quaternion.AngleAxis(180,axis),Torch2Projection.Home);p.Evaluate();Assert.IsFalse(p.Score.Ready,$"turned round about {axis}, threshold {gym.iouMin:0.00}\n{gym.report}");}
            p.Render(Quaternion.identity,Torch2Projection.Home);p.Evaluate();Assert.IsTrue(p.Score.Ready,"the answer still passes");
            // Random poses can miss a lookalike that a player turns onto (Good Morning: 0.77 at random, 0.82 turned round).
            Assert.AreEqual(.85f,Torch2GymTools.Threshold(.70f,.82f),1e-4f,"the lookalike sets it");
            Assert.AreEqual(.85f,Torch2GymTools.Threshold(.80f,.50f),1e-4f,"random poses set it");
            Assert.AreEqual(.62f,Torch2GymTools.Threshold(0,0),1e-4f);Assert.AreEqual(.95f,Torch2GymTools.Threshold(.5f,.99f),1e-4f);
        }
        [Test] public void CheckWarnsWhenThePictureIsCut()
        {
            var gym=Gym();Piece(gym.cluster,PrimitiveType.Cube,Vector3.zero,Vector3.zero,Vector3.one*.1f);
            Assert.IsFalse(Torch2GymTools.Check(gym,20).warnings.Any(w=>w.Contains("outside its light")),gym.report);
            Piece(gym.cluster,PrimitiveType.Cube,new Vector3(0,-.25f,-.2f),Vector3.zero,Vector3.one*.1f);
            Assert.IsTrue(Torch2GymTools.Check(gym,20).warnings.Any(w=>w.Contains("outside its light")),gym.report);
        }
        // Twins pair by name through parents under the cluster (a prop of two meshes is one thing), the export keeps where
        // each lies on the table, and opening the level builds the same table layout again.
        [Test] public void TableLayoutPairsByNameExportsAndOpensAgain()
        {
            var gym=Gym();var group=new GameObject("Props").transform;group.SetParent(gym.cluster,false);
            var mug=new GameObject("mug").transform;mug.SetParent(group,false);mug.localPosition=new Vector3(.05f,0,0);
            Piece(mug,PrimitiveType.Cylinder,Vector3.zero,Vector3.zero,new Vector3(.06f,.05f,.06f));Piece(mug,PrimitiveType.Cube,new Vector3(.04f,0,0),Vector3.zero,new Vector3(.02f,.04f,.01f));
            Piece(group,PrimitiveType.Sphere,new Vector3(-.08f,.06f,0),Vector3.zero,Vector3.one*.07f).name="ball";
            var layout=new GameObject("Table").transform;layout.SetParent(gym.transform,false);layout.localPosition=new Vector3(0,.03f,-.3f);gym.tableLayout=layout;
            var mugTwin=new GameObject("mug").transform;mugTwin.SetParent(layout,false);mugTwin.localPosition=new Vector3(.2f,0,0);mugTwin.localEulerAngles=new Vector3(0,90,0);
            Piece(layout,PrimitiveType.Sphere,new Vector3(-.2f,.005f,0),Vector3.zero,Vector3.one*.07f).name="ball";
            gym.Refresh(true);
            Assert.IsTrue(gym.PairTable(null,out var poses,out var groups),gym.Notes);
            Assert.AreEqual(3,poses.Length);Assert.AreEqual(groups[0],groups[1],"the mug's two meshes are one thing");Assert.AreNotEqual(groups[0],groups[2]);
            // The handle sits 4 cm along the mug's x, which the table turns to -z.
            Assert.Less(Vector3.Distance(poses[1].position,new Vector3(.2f,.03f,-.3f-.04f)),1e-4f);
            var level=Torch2GymTools.Export(gym,100,false);Assert.NotNull(level,gym.report);
            Assert.IsTrue(Torch2Transit.HasTable(level));Assert.Less(Vector3.Distance(level.pieces[2].tablePosition,new Vector3(-.2f,.035f,-.3f)),1e-4f);
            Torch2GymTools.Load(gym,level,false);gym.Refresh(true);
            Assert.IsTrue(gym.PairTable(null,out var again,out var groupsAgain),gym.Notes);
            for(int i=0;i<3;i++)Assert.Less(Vector3.Distance(poses[i].position,again[i].position),1e-4f,$"piece {i} lies where it did");
            Assert.AreEqual(groupsAgain[0],groupsAgain[1]);
        }
        [Test] public void ScriptLevelsOpenWithTheirAnswerAsNoRotation()
        {
            // A Python-made level (answer yaw 30, pitch -20) opens turned so the cluster as placed is the answer.
            var gym=Gym();var birthday=Torch2Level.Load("Birthday");Torch2GymTools.Load(gym,birthday,false);gym.Refresh(true);
            Assert.AreEqual("BirthdayGym",gym.id);
            var p=gym.Projection;p.Render(Quaternion.identity,Torch2Projection.Home);p.Evaluate();Assert.IsTrue(p.Score.Ready);
            var original=new Torch2Projection(birthday);var target=original.target[0];var mine=p.target[0];
            int same=0,either=0;for(int i=0;i<target.Length;i++){if(target[i]&&mine[i])same++;if(target[i]||mine[i])either++;}
            Assert.Greater(same/(float)either,.97f,"same picture as the game's Birthday");
        }
    }
}
