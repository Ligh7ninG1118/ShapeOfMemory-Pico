using System.Collections;
using UnityEngine;
using Unity.XR.OpenXR.Features.PICOSupport;

namespace PhysicalAssembly.MetaXR
{
    // Present only on the Pico build's portable player prefab. Turns video see-through on at start (retrying while the
    // runtime comes up); Show(false) turns it off again (a game that covers the view with its own sky).
    public sealed class PicoPassthrough : MonoBehaviour
    {
        bool wanted=true;
        IEnumerator Start()
        {
            if(Application.isEditor) yield break;
            for(int attempt=0;attempt<50&&wanted;attempt++)
            {
                if(PassthroughFeature.EnableSeeThroughManual(true))
                {
                    Debug.Log("METAHACKTHONE_PICO_PASSTHROUGH_READY");
                    yield break;
                }
                yield return new WaitForSeconds(.1f);
            }
            if(wanted) Debug.LogWarning("Pico passthrough unavailable; XR rendering continues.");
        }
        // Whether the runtime took it (always true in the editor, which has none).
        public bool Show(bool on)
        {
            wanted=on;
            return Application.isEditor || PassthroughFeature.EnableSeeThroughManual(on);
        }
    }

    // The device's passthrough, on whichever rig this build has: the Quest rig's OVRPassthroughLayer, PICO's video
    // see-through (PicoPassthrough). Returns what it switched, for the device log.
    public static class XRPassthrough
    {
        public static string Show(bool on)
        {
            string said="";
            foreach(var layer in Object.FindObjectsByType<OVRPassthroughLayer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            { layer.enabled=on; said+=" ovr-layer"; }
            foreach(var pico in Object.FindObjectsByType<PicoPassthrough>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                said+=" pico="+(pico.Show(on)?"ok":"refused");
            return said.Length==0?" none":said;
        }
    }
}
