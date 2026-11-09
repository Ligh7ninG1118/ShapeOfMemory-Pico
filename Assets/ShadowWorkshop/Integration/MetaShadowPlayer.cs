using System.Linq;
using CloudGarden.ShadowPuzzle;
using Oculus.Interaction.Input;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.XR;
using Hand = Oculus.Interaction.Input.Hand;

namespace CloudGarden.Integration
{
    // The scene that owns the shadow game: ShadowDemo (the level browser) or Shape of Memory (the game).
    public interface IShadowHost
    {
        bool Busy { get; }
        void Recenter();
        void LoadLevel(string level);
    }

    public sealed class MetaShadowPlayer : ShadowPlayerContext
    {
        public MetaPlayerRig Player;
        public IShadowHost Host;
        Hand[] hands;
        OVRHand[] systemHands;
        bool focused = true;
        Oculus.Interaction.RayInteractor[] rays;
        float nextBind;
        bool headWasReady;
        void Update()
        {
            if(!Active)return;
            bool ready=HeadReady;
            if(ready!=headWasReady){headWasReady=ready;Debug.Log("SHAPE_OF_MEMORY_HEAD "+HeadState());}
            if(Time.unscaledTime<nextBind)return;
            nextBind=Time.unscaledTime+1;
            rays ??= Player.XRRig.GetComponentsInChildren<Oculus.Interaction.RayInteractor>(true);
            foreach(var ray in rays)
            {
                if(!ray) continue;
                var selector=MetaPinchSelector.BindToRay(ray);
                if(selector) selector.ArmDistance=RayArmDistance;
                var pointer=ray.GetComponentInChildren<Oculus.Interaction.HandPointerPose>(true);
                // On the UnityXR rig the shoulder aim always takes over: Pico's own aim jitters and follows the wrist.
                if(pointer) ShoulderHandAim.Attach(pointer,Player.ViewCamera.transform,UnityXRRig);
            }
        }
        // After a hand comes back into view its ray's pinch counts once the hand reads this open (normalized thumb-index
        // distance). The selector's default (.045, its release distance) is wider than a resting hand, so a player who
        // lifts a relaxed hand to point could never select until they spread their fingers.
        public const float RayArmDistance=.035f;
        public override bool Active => Player && Player.IsXR;
        // The shadow games are hands-only. The comprehensive rig hands a side to its controller whenever that controller
        // counts as connected, and on PICO an idle controller left switched on does: that side's hand ray turns off
        // ("Hand and No Controller" = hand AND NOT controller) and, while the hand is out of view, the controller's poke
        // and grabs come on ("Controller and No Hand"). Rewire the rig's trackers so a hand never yields: "Hand and No
        // Controller" follows the hand alone, and the controller-only folders stay off. Returns how many trackers changed
        // (0 on a rig without them).
        // No pointing at all: the rig's hand rays go (the controller ones are off through HandsOnly). Returns how many.
        public static int NoHandRays(Transform rig)
        {
            int removed = 0;
            foreach (var ray in rig.GetComponentsInChildren<Oculus.Interaction.RayInteractor>(true))
                if (ray.GetComponentInChildren<Oculus.Interaction.HandPointerPose>(true)) { ray.gameObject.SetActive(false); removed++; }
            return removed;
        }
        // The rig's hand pokes go (a game that presses with its own fingertip test). Returns how many.
        public static int NoHandPokes(Transform rig)
        {
            int removed = 0;
            foreach (var poke in rig.GetComponentsInChildren<Oculus.Interaction.PokeInteractor>(true))
                if (poke.GetComponent<HandRef>()) { poke.gameObject.SetActive(false); removed++; }
            return removed;
        }
        public static int HandsOnly(Transform rig)
        {
            int changed = 0;
            foreach (var tracker in rig.GetComponentsInChildren<Oculus.Interaction.ActiveStateTracker>(true))
            {
                var folder = tracker.transform.parent;
                if (!folder || folder.name != "ActiveStatesTrackers") continue;
                var hand = tracker.GetComponentInParent<HandRef>(true);
                var dependents = folder.parent ? folder.parent.Find(tracker.name) : null;
                switch (tracker.name)
                {
                    case "Hand and No Controller":
                        if (hand) { tracker.InjectActiveState(hand); changed++; }
                        break;
                    case "Controller": case "Controller and No Hand": case "Controller and Hand":
                        tracker.enabled = false; if (dependents) dependents.gameObject.SetActive(false); changed++;
                        break;
                }
            }
            return changed;
        }
        // The Pico build's portable UnityXR rig rather than Meta's OVR rig.
        public bool UnityXRRig => Player && !Player.XRRig.GetComponent<OVRCameraRig>();
        // Hands the rig tracks. The UnityXR rig also carries a static prefab hand (HandSphereMap) that always reads as
        // valid, which must never stand in for a hand that lost tracking.
        Hand[] Hands => hands ??= Player.XRRig.GetComponentsInChildren<Hand>(true).Where(h => !h.GetComponent<FromHandPrefabDataSource>()).ToArray();
        public Hand TrackedHand(bool left) => Active && HeadReady ? Tracked(left) : null;
        // The rig's hand rays (not its controller rays) and the hand each follows.
        public System.Collections.Generic.IEnumerable<(Oculus.Interaction.RayInteractor ray, IHand hand)> HandRays()
        {
            if (!Active) yield break;
            rays ??= Player.XRRig.GetComponentsInChildren<Oculus.Interaction.RayInteractor>(true);
            foreach (var ray in rays)
            {
                var pointer = ray ? ray.GetComponentInChildren<Oculus.Interaction.HandPointerPose>(true) : null;
                if (pointer && pointer.Hand != null) yield return (ray, pointer.Hand);
            }
        }
        Hand Tracked(bool left) => Hands.FirstOrDefault(h => h && h.isActiveAndEnabled && h.IsConnected && h.IsTrackedDataValid
            && h.Handedness == (left ? Handedness.Left : Handedness.Right));
        // The UnityXR rig has no OVRHand. Its runtime (Pico) reports the system gesture (palm up and pinch: the system
        // menu) in the hand-aim flags instead, and that pinch belongs to the system, not the orb.
        readonly bool[] systemGesture = new bool[2];
        bool InSystemGesture(bool left)
        {
            var aim = left ? UnityEngine.XR.Hands.MetaAimHand.left : UnityEngine.XR.Hands.MetaAimHand.right;
            bool now = aim != null && aim.added && ((UnityEngine.XR.Hands.MetaAimFlags)(ulong)(uint)aim.aimFlags.ReadValue() & UnityEngine.XR.Hands.MetaAimFlags.SystemGesture) != 0;
            int i = left ? 0 : 1;
            if (now != systemGesture[i]) { systemGesture[i] = now; Debug.Log($"SHAPE_OF_MEMORY_SYSTEM_GESTURE {(left ? "Left" : "Right")} {(now ? "start" : "end")}"); }
            return now;
        }
        // One line per hand ray for device logs: tracking, ray state, whose aim, which selector.
        public string Report()
        {
            if (rays == null) return "rays=unbound";
            var text = new System.Text.StringBuilder(HeadState()).Append(' ');
            foreach (var ray in rays)
            {
                if (!ray) continue;
                var pointer = ray.GetComponentInChildren<Oculus.Interaction.HandPointerPose>(true);
                if (!pointer || pointer.Hand == null) continue;
                var aim = pointer.GetComponent<ShoulderHandAim>();
                var selector = ray.GetComponentInChildren<MetaPinchSelector>(true);
                text.Append($"{pointer.Hand.Handedness}:tracked={pointer.Hand.IsTrackedDataValid},ray={ray.State},aim={(aim && aim.Overriding ? "shoulder" : "runtime")},select={(selector ? (selector.Armed ? "distance/armed" : "distance/unarmed") : "sdk")} ");
            }
            return text.ToString();
        }
        HeadSignal tracking, presence;
        public override bool HeadReady
        {
            get
            {
                // The desktop has no head to wait for, and an unfocused editor window must not refuse clicks and tests.
                if (!Player) return false;
                if (!Active) return true;
                if (!focused) return false;
                var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                return head.isValid
                    && tracking.Allows(head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked), tracked)
                    && presence.Allows(head.TryGetFeatureValue(CommonUsages.userPresence, out bool present), present);
            }
        }
        // What HeadReady is made of, for device logs.
        public string HeadState()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            string Read(InputFeatureUsage<bool> usage, HeadSignal signal) =>
                head.TryGetFeatureValue(usage, out bool value) ? value + (signal.Seen ? "" : "(unproven)") : "none";
            return $"ready={HeadReady} focus={focused}/{Application.isFocused} head={head.isValid} tracked={Read(CommonUsages.isTracked, tracking)} present={Read(CommonUsages.userPresence, presence)}";
        }
        public override bool ReadHand(bool left, out Pose palm, out Vector3 tip, out float pinchDistance) => ReadHand(left, out palm, out tip, out pinchDistance, false);
        // throughSystemGesture: for a hand already holding something. Lifting a held page turns the palm up toward the
        // face, which the runtime can flag as the system gesture; that must not drop what the hand holds (if the system
        // menu really opens, focus goes and HeadReady lets go instead).
        public bool ReadHand(bool left, out Pose palm, out Vector3 tip, out float pinchDistance, bool throughSystemGesture)
        {
            palm = default; tip = default; pinchDistance = float.PositiveInfinity;
            if (!HeadReady || !Active || !Player.XRRig.gameObject.activeInHierarchy) return false;
            if (SystemGesture(left) && !throughSystemGesture) return false;
            var hand = Tracked(left);
            if (!hand || !hand.GetRootPose(out var wrist) || !hand.GetJointPose(HandJointId.HandMiddle1, out var middle)
                || !hand.GetJointPose(HandJointId.HandIndexTip, out var index) || !hand.GetJointPose(HandJointId.HandThumbTip, out var thumb)) return false;
            // Meta poses are already in world space. Retain the physical 18/20 mm pinch thresholds.
            palm = new Pose(Vector3.Lerp(wrist.position, middle.position, .5f), wrist.rotation);
            tip = index.position; pinchDistance = Vector3.Distance(index.position, thumb.position);
            return true;
        }
        bool SystemGesture(bool left)
        {
            systemHands ??= Player.XRRig.GetComponentsInChildren<OVRHand>(true);
            bool ovr = systemHands.Any(h => h && h.GetHand() == (left ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight) && h.IsSystemGestureInProgress);
            return InSystemGesture(left) || ovr;
        }
        // Why ReadHand gives nothing for this hand, for device logs.
        public string HandProblem(bool left, bool throughSystemGesture = false)
        {
            if (!Active) return "inactive";
            if (!HeadReady) return "head " + HeadState();
            if (!Player.XRRig.gameObject.activeInHierarchy) return "rig off";
            if (!throughSystemGesture && SystemGesture(left)) return "system gesture";
            var hand = Tracked(left);
            return !hand ? "untracked" : "joints missing";
        }
        // Meta's pointer pose is the same ray the system and the menu use, so the beam goes where the hand points.
        public override bool ReadAim(bool left, out Pose aim)
        {
            aim = default;
            if (!HeadReady || !Active || !Player.XRRig.gameObject.activeInHierarchy) return false;
            var hand = Tracked(left);
            return hand && hand.IsPointerPoseValid && hand.GetPointerPose(out aim);
        }
        public override void Recenter() { if (HeadReady && Host != null && !Host.Busy) Host.Recenter(); }
        public override void SwitchLevel(string level) => Host?.LoadLevel(level);
        void OnApplicationFocus(bool value) => focused = value;
        void OnApplicationPause(bool value) => focused = !value;
    }

    // A head signal (tracked, user present) holds the hands back only once the runtime has reported it true. A runtime
    // can offer a signal and never set it (XR_EXT_user_presence, say), which would otherwise draw no hands and drop
    // every press for the whole session.
    public struct HeadSignal
    {
        public bool Seen { get; private set; }
        public bool Allows(bool readable, bool value)
        {
            if (!readable) return true;
            if (value) Seen = true;
            return value || !Seen;
        }
    }
}
