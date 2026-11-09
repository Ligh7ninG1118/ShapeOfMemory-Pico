using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CloudGarden.ShadowPuzzle;
using Object = UnityEngine.Object;

namespace CloudGarden.Tests
{
    [TestFixture(typeof(WishesWorkshop), typeof(WishesInput), typeof(WishesWorld))]
    [TestFixture(typeof(MoonWorkshop), typeof(MoonInput), typeof(MoonWorld))]
    [TestFixture(typeof(ShadowWorkshop), typeof(ShadowInput), typeof(ShadowWorld))]
    public sealed class ShadowGrabTests
    {
        readonly Type gameType, inputType, worldType;
        GameObject root, geometry;
        Component game, input;
        int[] owners;
        ShadowPose[] poses;
        Transform[] pieces;
        Action<int, bool, Pose, Vector3, float, float> sample;
        Func<int, int, bool> begin;
        Action<int, int, ShadowPose> move;
        Action<int, int, bool> end;
        readonly Vector3 first = new Vector3(-.06f, .20f, 0), second = new Vector3(.06f, .20f, 0);

        public ShadowGrabTests(Type gameType, Type inputType, Type worldType)
        { this.gameType = gameType; this.inputType = inputType; this.worldType = worldType; }

        [SetUp] public void Setup()
        {
            // Keep scene builders dormant; use real colliders and the production hand input path.
            root = new GameObject("Grab regression"); root.SetActive(false);
            geometry = new GameObject("Grab regression colliders");
            game = root.AddComponent(gameType);
            input = root.AddComponent(inputType);
            var world = root.AddComponent(worldType);
            gameType.GetField("world").SetValue(game, world);
            gameType.GetField("input").SetValue(game, input);
            inputType.GetField("game").SetValue(input, game);
            worldType.GetField("game").SetValue(world, game);
            gameType.GetField("Snap")?.SetValue(game, false);
            if (game is ShadowWorkshop boat) for (int i = 0; i < 3; i++) boat.available[i] = true;
            owners = (int[])gameType.GetField("owners").GetValue(game);
            poses = (ShadowPose[])gameType.GetField("poses").GetValue(game);
            pieces = (Transform[])gameType.GetField("pieces").GetValue(game);
            for (int i = 0; i < pieces.Length; i++)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.transform.SetParent(geometry.transform);
                block.transform.localScale = Vector3.one * .04f;
                block.transform.position = i == 0 ? first : i == 1 ? second : new Vector3(2 + i, .2f, 0);
                pieces[i] = block.transform;
                poses[i] = new ShadowPose(block.transform.position, Quaternion.identity);
                owners[i] = -1;
                if (game is WishesWorkshop wishes)
                {
                    wishes.pickSurfaces[i] = new[] { block.GetComponent<Collider>() };
                    wishes.renderers[i] = block.GetComponent<Renderer>();
                }
            }
            sample = (Action<int, bool, Pose, Vector3, float, float>)Delegate.CreateDelegate(typeof(Action<int, bool, Pose, Vector3, float, float>), input, "SampleHand");
            begin = (Func<int, int, bool>)Delegate.CreateDelegate(typeof(Func<int, int, bool>), game, "BeginGrab");
            move = (Action<int, int, ShadowPose>)Delegate.CreateDelegate(typeof(Action<int, int, ShadowPose>), game, "MoveGrab");
            end = (Action<int, int, bool>)Delegate.CreateDelegate(typeof(Action<int, int, bool>), game, "EndGrab");
            Physics.SyncTransforms();
        }

        [TearDown] public void Cleanup()
        {
            if (input) inputType.GetMethod("ReleaseAll").Invoke(input, null);
            Object.DestroyImmediate(root); Object.DestroyImmediate(geometry);
        }

        void Frame(Vector3 tip, float distance, int hand = 0, bool tracked = true)
        { sample(hand, tracked, new Pose(tip, Quaternion.identity), tip, distance, .02f); Physics.SyncTransforms(); }
        void Open(Vector3 tip, int hand = 0) { for (int i = 0; i < 6; i++) Frame(tip, .025f, hand); }
        void GrabFirst() { Open(first); Frame(first, .012f); Assert.AreEqual(0, owners[0]); }

        [Test] public void HeldBlockStaysSelectedWhenPassingAnotherBlockAndButton()
        {
            int presses = 0;
            var buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonObject.transform.SetParent(geometry.transform);
            buttonObject.transform.position = second; buttonObject.transform.localScale = Vector3.one * .02f;
            var button = buttonObject.AddComponent<ShadowControl>(); button.action = () => presses++;
            var controls = (List<ShadowControl>)worldType.GetField("controls").GetValue(gameType.GetField("world").GetValue(game));
            controls.Add(button);
            GrabFirst();
            var neighbour = poses[1];
            for (int i = 1; i <= 20; i++) Frame(Vector3.Lerp(first, second, i / 20f), .019f);
            Assert.AreEqual(0, owners[0]); Assert.AreEqual(-1, owners[1]);
            Assert.That(Vector3.Distance(poses[0].position, second), Is.LessThan(.0001f));
            Assert.AreEqual(neighbour.position, poses[1].position); Assert.AreEqual(0, presses);
        }

        [Test] public void OneFrameReleaseIsImmediateButCannotRegrabNeighbour()
        {
            GrabFirst(); Frame(second, .020f);
            Assert.AreEqual(-1, owners[0], "20 mm must release in the same frame.");
            for (int i = 0; i < 30; i++) Frame(second, i % 2 == 0 ? .012f : .019f);
            Assert.That(owners, Is.All.EqualTo(-1), "Threshold chatter must not arm another grab.");
        }

        [Test] public void StableOpeningThenNewPinchCanGrabAnotherBlock()
        {
            GrabFirst(); Frame(first, .020f);
            Open(second); Frame(second, .012f);
            Assert.AreEqual(-1, owners[0]); Assert.AreEqual(0, owners[1]);
        }

        [Test] public void TrackingLossFreezesBlockAndRequiresOpeningAfterRecovery()
        {
            GrabFirst(); Frame(first + Vector3.up * .03f, .012f);
            var before = poses[0]; Frame(second, 0, tracked: false);
            Assert.AreEqual(-1, owners[0]); Assert.AreEqual(before.position, poses[0].position);
            for (int i = 0; i < 10; i++) Frame(second, .012f);
            Assert.That(owners, Is.All.EqualTo(-1));
            Open(second); Frame(second, .012f); Assert.AreEqual(0, owners[1]);
        }

        [Test] public void OneHandCannotAcquireASecondBlockOrStealItWhileBusy()
        {
            Assert.IsTrue(begin(0, 0)); Assert.IsFalse(begin(1, 0));
            Assert.IsTrue(begin(1, 1)); Assert.IsFalse(begin(1, 0));
            Assert.AreEqual(0, owners[0]); Assert.AreEqual(1, owners[1]);
            end(0, 0, true); Assert.IsTrue(begin(1, 0), "A free hand can intentionally take over.");
        }

        [Test] public void IntentionalHandoffKeepsUndoAndOldHandCannotGrabNeighbour()
        {
            GrabFirst(); var moved = first + Vector3.up * .03f; Frame(moved, .012f);
            Open(moved, 1); Frame(moved, .012f, 1); Assert.AreEqual(1, owners[0]);
            for (int i = 0; i < 10; i++) Frame(second, .012f);
            Assert.AreEqual(-1, owners[1]); Assert.AreEqual(moved, poses[0].position);
            move(0, 0, new ShadowPose(second, Quaternion.identity));
            Assert.AreEqual(moved, poses[0].position, "Old owner cannot move the transferred block.");
            Frame(moved, .020f, 1);
            Assert.AreEqual(1, gameType.GetProperty("UndoCount").GetValue(game));
            gameType.GetMethod("Undo").Invoke(game, null);
            Assert.AreEqual(first, poses[0].position, "One undo restores the whole handoff transaction.");
        }
    }
}
