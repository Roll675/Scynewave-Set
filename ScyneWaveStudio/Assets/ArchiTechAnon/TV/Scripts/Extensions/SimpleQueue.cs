
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace ArchiTech
{
    public class SimpleQueue : UdonSharpBehaviour
    {
        public TVManagerV2 tV;


        private bool init = false;
        private bool skipLog = false;

        private void initialize() {
            if (init) return;



            init = true;
        }

        void Start()
        {
            initialize();
        }


        
        
        private void log(string value) {
            if (!skipLog) Debug.Log($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#dccbba>SimpleQueue</color>] {value}");
        }
        private void warn(string value) {
            if (!skipLog) Debug.LogWarning($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#dccbba>SimpleQueue</color>] {value}");
        }
        private void err(string value)
        {
            if (!skipLog) Debug.LogError($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#dccbba>SimpleQueue</color>] {value}");
        }
    }
}
