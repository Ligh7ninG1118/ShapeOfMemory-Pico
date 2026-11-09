using System;
using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction;
using UnityEngine;

namespace ShapeOfMemory
{
    // Device-log tracing of the rig's interactors (poke, ray, grab, locomotion, hands and controllers): every state change
    // (Normal, Hover, Select, Disabled) with the button it is on, named by hand and kind ("R-HandPoke", "L-Ray"). An
    // interactor in the same group as the poke or the ray can hold them off while it hovers or selects, so all are traced.
    // Pressable names its pointer events by the same names (Describe). At most PerSecond lines per interactor a second.
    public sealed class MemoryTrace : MonoBehaviour
    {
        public const int PerSecond=12;
        sealed class Watched{public IInteractorView view;public MonoBehaviour behaviour;public string name;public Action<InteractorStateChangeArgs> handler;public float second;public int lines,dropped;}
        static readonly List<Watched> all=new List<Watched>();
        readonly List<Watched> mine=new List<Watched>();

        public static MemoryTrace Add(GameObject host,Transform rig)
        {
            var trace=host.AddComponent<MemoryTrace>();var seen=new Dictionary<string,int>();
            foreach(var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(behaviour is not IInteractor interactor)continue;
                string name=NameOf(behaviour);seen[name]=seen.TryGetValue(name,out int n)?n+1:1;if(seen[name]>1)name+="#"+seen[name];
                var w=new Watched{view=interactor,behaviour=behaviour,name=name};w.handler=args=>trace.Changed(w,args);
                interactor.WhenStateChanged+=w.handler;trace.mine.Add(w);all.Add(w);
            }
            Debug.Log($"SHAPE_OF_MEMORY_INTERACTORS {trace.mine.Count}: "+string.Join(", ",trace.mine.Select(w=>w.name+(w.behaviour.isActiveAndEnabled?"":"(off)"))));
            return trace;
        }
        // A pointer event's interactor by name ("#id" when it is not one of the rig's).
        public static string Describe(int identifier)
        {
            foreach(var w in all)if(w.behaviour&&Id(w.view)==identifier)return w.name;
            return "#"+identifier;
        }
        // The hands' pokes and rays at a glance, for the periodic HANDS line.
        public string Summary()
        {
            var parts=mine.Where(w=>w.behaviour&&(w.behaviour is PokeInteractor||w.behaviour is RayInteractor)).Select(w=>w.name+"="+(w.behaviour.isActiveAndEnabled?w.view.State.ToString():"off")+Target(w.behaviour));
            return "interactors "+string.Join(" ",parts);
        }
        void Changed(Watched w,InteractorStateChangeArgs args)
        {
            float now=Time.unscaledTime;
            if(now-w.second>=1){w.second=now;w.lines=0;}
            if(++w.lines>PerSecond){w.dropped++;return;}
            string dropped=w.dropped>0?$" (+{w.dropped} not logged)":"";w.dropped=0;
            Debug.Log($"SHAPE_OF_MEMORY_INTERACTOR {w.name} {args.PreviousState}>{args.NewState}{Target(w.behaviour)}{dropped}");
        }
        static int Id(IInteractorView view){try{return view.Identifier;}catch(NullReferenceException){return int.MinValue;}}
        static string Target(MonoBehaviour behaviour)
        {
            Component target=behaviour switch{PokeInteractor p=>p.Interactable,RayInteractor r=>r.Interactable,_=>null};
            if(!target)return "";
            var press=target.GetComponentInParent<Pressable>();
            return " on "+(press?press.Name:target.name);
        }
        // Side and kind from the rig hierarchy: "R-hand-Poke", "L-ctrl-Ray", "R-both-HandGrabUse", "?-Gaze". The kind is the
        // folder under the comprehensive rig's Interactors: Hand / Hand and No Controller (hand), Controller / Controller
        // and No Hand (ctrl), Controller and Hand (both).
        static string NameOf(MonoBehaviour behaviour)
        {
            string side="?",kind="";
            for(var t=behaviour.transform;t;t=t.parent)
            {
                var n=t.name;
                if(side=="?")side=n.IndexOf("Left",StringComparison.OrdinalIgnoreCase)>=0?"L":n.IndexOf("Right",StringComparison.OrdinalIgnoreCase)>=0?"R":"?";
                if(kind==""&&t.parent&&t.parent.name=="Interactors")kind=n.StartsWith("Hand")?"hand-":n=="Controller and Hand"?"both-":n.StartsWith("Controller")?"ctrl-":"";
            }
            return side+"-"+kind+behaviour.GetType().Name.Replace("Interactor","");
        }
        // The poke interactor behind a pointer event's identifier, or null.
        public static PokeInteractor Poke(int identifier)
        {
            foreach(var w in all)if(w.behaviour is PokeInteractor poke&&poke&&Id(w.view)==identifier)return poke;
            return null;
        }
        void OnDestroy()
        {
            foreach(var w in mine){if(w.behaviour)w.view.WhenStateChanged-=w.handler;all.Remove(w);}
            mine.Clear();
        }
        // Domain reload is off in the editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>all.Clear();
    }
}
