using System;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace PhysicalAssembly.MetaXR
{
    public sealed class MetaPinchSelector : MonoBehaviour,ISelector
    {
        public IHand Hand { get; private set; }
        public bool IsPinching => latch.IsPinching;
        public bool Armed => latch.Armed;
        public float ArmDistance { get => latch.ArmDistance; set => latch.ArmDistance=value; }
        public Func<bool> SuppressSelection;
        public event Action WhenSelected=delegate { };
        public event Action WhenUnselected=delegate { };
        readonly PinchLatch latch=new();
        public static MetaPinchSelector BindToRay(RayInteractor ray)
        {
            var native=ray.GetComponentInChildren<IndexPinchSelector>(true);
            if(!native || !native.isActiveAndEnabled || native.Hand==null) return null;
            var selector=native.GetComponent<MetaPinchSelector>();
            // SDK Selector's setter subscribes even for the same instance. Rebinding
            // on every tracking recovery would duplicate select/unselect events.
            if(selector) return selector;
            if(!native.Hand.IsConnected || !native.Hand.IsTrackedDataValid) return null;
            // Swapping selectors mid-select drops the old selector's unselect: the ray would stay selecting (and its
            // group's poke held off) until a full pinch cycle. Bind once it lets go.
            if(ray.State==InteractorState.Select) return null;
            selector=native.gameObject.AddComponent<MetaPinchSelector>(); selector.Initialize(native.Hand);
            ray.InjectSelector(selector); return selector;
        }
        public void Initialize(IHand hand)
        {
            if(Hand!=null) Hand.WhenHandUpdated-=SampleHand;
            Hand=hand;
            if(isActiveAndEnabled && Hand!=null) Hand.WhenHandUpdated+=SampleHand;
        }
        void OnEnable() { if(Hand!=null) Hand.WhenHandUpdated+=SampleHand; }
        void OnDisable()
        {
            if(Hand!=null) Hand.WhenHandUpdated-=SampleHand;
            ApplySample(false,0,false);
        }
        void SampleHand()
        {
            if(Hand==null || !Hand.IsConnected || !Hand.IsTrackedDataValid
                || !Hand.GetJointPose(HandJointId.HandIndexTip,out var index)
                || !Hand.GetJointPose(HandJointId.HandThumbTip,out var thumb))
            { ApplySample(false,0,false); return; }
            float distance=Vector3.Distance(index.position,thumb.position)/Mathf.Max(.1f,Hand.Scale);
            ApplySample(true,distance,Hand.GetIndexFingerIsPinching());
        }
        // The SDK event path and tests both enter through this sample boundary.
        public void ApplySample(bool valid,float distance,bool systemPinch)
        {
            bool before=latch.IsPinching,armedBefore=latch.Armed;
            if(SuppressSelection?.Invoke()==true)
            {
                latch.Reset(); if(before) WhenUnselected(); return;
            }
            bool after=latch.Update(valid,distance,systemPinch);
            if(latch.Armed!=armedBefore)
                Debug.Log($"METAHACKTHONE_PINCH {(Hand==null?name:Hand.Handedness.ToString())} {(latch.Armed?"armed":"disarmed")} distance={distance:F3} valid={valid}");
            if(before==after) return;
            Debug.Log($"METAHACKTHONE_PINCH {(Hand==null?name:Hand.Handedness.ToString())} {(after?"down":"up")} distance={distance:F3} system={systemPinch}");
            if(after) WhenSelected(); else WhenUnselected();
        }
    }
}
