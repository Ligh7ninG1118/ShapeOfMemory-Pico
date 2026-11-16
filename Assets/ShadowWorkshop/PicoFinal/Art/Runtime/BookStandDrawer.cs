using System;
using UnityEngine;

namespace ShapeOfMemory.ArtDressing
{
    // The book stand's drawer: it slides straight out along PullAxis when the knob is pulled, from shut (0) to MaxPull,
    // never further, so a quarter of it (KeepInside) always stays in the stand. Only the mechanism: what grabs the
    // knob, when the drawer may open and what's inside are the game's to decide.
    //
    // For the game:
    //   BeginDrag(handWorld) / Drag(handWorld) / EndDrag()  move the drawer with a hand (or pointer) holding the knob:
    //                                                       only the pull along the axis counts, clamped to 0..MaxPull
    //   SetPull(metres) / SetOpen(0..1)                     put it somewhere directly (e.g. animate it)
    //   Pull, Open, IsDragging, KnobPoint                   read it; Changed fires with Open (0..1) when it moves
    //   Knob                                                the handle; it has a trigger BoxCollider on Ignore Raycast,
    //                                                       so it marks the handle without blocking the game's rays
    // Everything is in this object's local space (the prefab's), so it keeps working when the stand is scaled, as
    // PicoFinalArtDressing does in game. The drawer and the knob are separate children (the FBX's), so both move together.
    [DisallowMultipleComponent]
    public sealed class BookStandDrawer : MonoBehaviour
    {
        [Tooltip("SM_BookStandDrawer_01")]
        public Transform Drawer;
        [Tooltip("SM_BookStandDrawerKnob_01: the handle")]
        public Transform Knob;
        [Tooltip("The way the drawer comes out, in this object's local space")]
        public Vector3 PullAxis = Vector3.forward;
        [Tooltip("How far it can come out, in this object's local units (metres at the prefab's scale)")]
        public float MaxPull = 0.34f;
        [Tooltip("The share of the drawer's depth that always stays inside the stand (MaxPull is set from it on Reset)")]
        [Range(0f, 0.9f)] public float KeepInside = 0.25f;

        public float Pull { get; private set; }
        public float Open => MaxPull > 0 ? Pull / MaxPull : 0;
        public bool IsDragging { get; private set; }
        public Vector3 KnobPoint => Knob ? Knob.position : transform.position;
        public event Action<float> Changed;

        Vector3 drawerRest, knobRest;
        bool rested;
        float dragStartPull, dragStartAlong;

        void Awake() => Rest();

        void Rest()
        {
            if (rested) return;
            if (Drawer) drawerRest = Drawer.localPosition;
            if (Knob) knobRest = Knob.localPosition;
            rested = true;
        }

        public void SetOpen(float open) => SetPull(Mathf.Clamp01(open) * MaxPull);

        public void SetPull(float metres)
        {
            Rest();
            float pull = Mathf.Clamp(metres, 0, MaxPull);
            if (Mathf.Approximately(pull, Pull)) return;
            Pull = pull;
            var offset = PullAxis.normalized * pull;
            if (Drawer) Drawer.localPosition = drawerRest + offset;
            if (Knob) Knob.localPosition = knobRest + offset;
            Changed?.Invoke(Open);
        }

        // A hand (or pointer ray's hit) at handWorld has taken the knob.
        public void BeginDrag(Vector3 handWorld)
        {
            IsDragging = true;
            dragStartPull = Pull;
            dragStartAlong = Along(handWorld);
        }

        // The hand moved: the drawer follows its motion along the axis.
        public void Drag(Vector3 handWorld)
        {
            if (!IsDragging) BeginDrag(handWorld);
            SetPull(dragStartPull + Along(handWorld) - dragStartAlong);
        }

        public void EndDrag() => IsDragging = false;

        float Along(Vector3 world) => Vector3.Dot(transform.InverseTransformPoint(world), PullAxis.normalized);

        // Fills the slots and sets MaxPull from the drawer's depth and the stand's front, in the editor.
        void Reset()
        {
            Drawer = transform.Find("SM_BookStandDrawer_01");
            Knob = transform.Find("SM_BookStandDrawerKnob_01");
            FitMaxPull();
        }

        [ContextMenu("Fit Max Pull to the drawer")]
        void FitMaxPull()
        {
            var body = transform.Find("SM_BookStand_01");
            if (!Drawer || !body) return;
            var drawerMesh = Drawer.GetComponent<MeshFilter>();
            var bodyMesh = body.GetComponent<MeshFilter>();
            if (!drawerMesh || !bodyMesh) return;
            var axis = PullAxis.normalized;
            // The drawer's back and depth along the axis, and its box (this object's space).
            float back = float.MaxValue, front = float.MinValue;
            var box = new Bounds();
            bool first = true;
            foreach (var v in drawerMesh.sharedMesh.vertices)
            {
                var p = transform.InverseTransformPoint(Drawer.TransformPoint(v));
                float a = Vector3.Dot(p, axis);
                back = Mathf.Min(back, a); front = Mathf.Max(front, a);
                if (first) { box = new Bounds(p, Vector3.zero); first = false; } else box.Encapsulate(p);
            }
            // The stand's front where the drawer comes out: only the body within the drawer's own outline counts, which
            // is the slot's frame (the base's moulding, which sticks out 10 cm further, starts just outside it; a
            // smaller outline finds only the opening's back wall). The box's length along the axis is ignored.
            var across = Vector3.Scale(box.size, Vector3.one - new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)));
            var slot = new Bounds(box.center, across + new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)) * 100f);
            float standFront = float.MinValue;
            foreach (var v in bodyMesh.sharedMesh.vertices)
            {
                var p = transform.InverseTransformPoint(body.TransformPoint(v));
                if (slot.Contains(p)) standFront = Mathf.Max(standFront, Vector3.Dot(p, axis));
            }
            if (standFront == float.MinValue) standFront = front;   // no body round the slot: measure from the drawer's face
            float depth = front - back;
            MaxPull = Mathf.Max(0, standFront - back - KeepInside * depth);
        }

        [ContextMenu("Test: pull all the way")] void TestOpen() => SetOpen(1);
        [ContextMenu("Test: push shut")] void TestShut() => SetOpen(0);
    }
}
