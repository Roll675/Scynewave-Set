
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.Udon;
namespace ArchiTech
{
    [RequireComponent(typeof(BaseVRCVideoPlayer))]
    public class VideoManager : UdonSharpBehaviour
    {

        public string managerName;
        public Transform rootTVNode;
        [HideInInspector] public BaseVRCVideoPlayer player;
        [HideInInspector] public bool isVisible;
        private GameObject[] screens;
        private TVManager tv;
        [System.NonSerialized] public AudioSource[] speakers;
        
        // UI Components
        private Slider volumeModifier;
        private Toggle muteModifier;
        private Toggle audioModeModifier;
        // End UI Components

        private VideoError lastError;
        private bool skipLog = false;
        private bool init = false;

        private void log(string value)
        {
            if (!skipLog) Debug.Log("[<color=#00ffcc>VideoManager</color>] " + value);
        }

        private void err(string value)
        {
            if (!skipLog) Debug.LogError("[<color=#00ffcc>VideoManager</color>] " + value);
        }

        private void assignRefs()
        {
            log("Starting " + rootTVNode.name + " - " + managerName);
            var ui = rootTVNode.Find("VideoPlayer").Find("UI").Find("Offset");
            volumeModifier = ui.Find("VolumeModifier").GetComponent<Slider>();
            audioModeModifier = ui.Find("AudioModeModifier").GetComponent<Toggle>();
            muteModifier = ui.Find("MuteModifier").GetComponent<Toggle>();
            player = (BaseVRCVideoPlayer)GetComponent(typeof(VRCAVProVideoPlayer));
            // hunt for the screens
            skipLog = true;
            var _screens = traverseFor(typeof(VRCAVProVideoScreen), rootTVNode);
            var count = 0;
            for (int i = 0; i < _screens.Length; i++)
            {
                if (_screens[i] == null) continue;
                if (_screens[i].gameObject.name == managerName) count++;
            }
            screens = new GameObject[count];
            count = 0;
            for (int i = 0; i < _screens.Length; i++)
            {
                if (_screens[i] == null) continue;
                if (_screens[i].gameObject.name == managerName)
                {
                    screens[count] = _screens[i].gameObject;
                    count++;
                }
            }
            skipLog = false;
            if (count == 0) err("Unable to find any screen objects for manager: " + managerName);
            else log("Found " + count + " matching screens");
            // hunt for all speakers attached to this player
            // WORKAROUND NOTE: find speakers who's gameObject.name is the same as the managerName
            // since the VideoPlayer attribute of VRCAVproVideoSpeaker is not available
            // in udon to check against (... yet)
            skipLog = true;
            var _speakers = traverseFor(typeof(VRCAVProVideoSpeaker), rootTVNode);
            count = 0;
            for (int i = 0; i < _speakers.Length; i++)
            {
                if (_speakers[i] == null) continue;
                if (_speakers[i].gameObject.name == managerName) count++;
            }
            // convert speakers to their respective AudioSources
            speakers = new AudioSource[count];
            count = 0;
            for (int i = 0; i < _speakers.Length; i++)
            {
                if (_speakers[i] == null) continue;
                if (_speakers[i].gameObject.name == managerName)
                {
                    speakers[count] = _speakers[i].gameObject.GetComponent<AudioSource>();
                    count++;
                }
            }
            skipLog = false;
            if (count == 0) err("Unable to find any speaker objects for manager: " + managerName);
            else log("Found " + count + " matching speakers");
        }

        void Start()
        {
            assignRefs(); // grab ALL the things
            UpdateVolume(); // ensure the speakers volume matches the volume slider value
            Mute(true); // mute speakers by default
            foreach (var screen in screens)
                screen.SetActive(false);
            init = true;
        }


        // ================= UI Events ========================


        public void UpdateVolume()
        {
            foreach (var speaker in speakers)
            {
                if (speaker == null) continue;
                speaker.volume = volumeModifier.value;
            }
        }

        public void UpdateAudioMode()
        {
            var blend = audioModeModifier.isOn ? 1.0f : 0.0f;
            foreach (var speaker in speakers)
            {
                if (speaker == null) continue;
                speaker.spatialBlend = blend;
            }
        }

        public void UpdateMute() => Mute(muteModifier.isOn);


        // ================= Helper Methods =================


        public void SetTV(TVManager manager) => tv = manager;

        public void Mute(bool muted)
        {
            foreach (var speaker in speakers)
            {
                if (speaker == null) continue;
                speaker.mute = muted;
            }
        }


        public void Show()
        {
            foreach (var screen in screens)
                screen.SetActive(true);
            Mute(false);
            UpdateVolume();
            UpdateAudioMode();
            isVisible = true;
            log(rootTVNode.name + " [" + managerName + "] activated");
        }

        public void Hide()
        {
            Mute(true);
            player.Stop();
            foreach (var screen in screens)
                screen.SetActive(false);
            isVisible = false;
            log(rootTVNode.name + " [" + managerName + "] deactivated");
        }


        // ============== Player Proxy Methods =================

        new void OnVideoStart() => tv.VideoStart();
        // new void OnVideoEnd() => tv.VideoEnd();
        new void OnVideoError(VideoError error) => tv.VideoError(error);
        // new void OnVideoLoop() => tv.VideoLoop();
        // new void OnVideoPause() => tv.VideoPause();
        // new void OnVideoPlay() => tv.VideoPlay();
        // new void OnVideoReady() => tv.VideoReady();


        // ============== Traversal Utilities ===================


        private Component[] traverseAllFor(System.Type T, GameObject[] objects)
        {
            var elements = new Component[objects.Length];
            var index = 0;
            foreach (GameObject obj in objects)
            {
                if (obj == null) continue;
                Component[] found = traverseFor(T, obj.transform);
                foreach (Component element in found)
                {
                    if (element == null) continue;
                    // check if next index exceeds bounds, expand if so
                    if (index >= elements.Length)
                        elements = expand(elements, 16);
                    elements[index] = element;
                    index++;
                }
            }
            // remove excess nulls
            var clean = new Component[index];
            for (int i = 0; i < clean.Length; i++)
            {
                clean[i] = elements[i];
            }
            elements = clean;
            log("Total found: " + elements.Length);
            return elements;
        }

        private Component[] traverseFor(System.Type T, Transform root)
        {
            log("Traversing node " + root.name + " for " + T);
            Component[] elements = new Component[16];
            // stack is actually always Transform, but use Component for generic pop/push use
            Component[] stack = new Component[16];
            stack[0] = root;
            var found = 0;
            while (stack[0] != null)
            {
                var cur = (Transform)pop(stack);
                // log("Gathering elements within node " + cur.name);
                if (cur != null)
                {
                    var t = cur.GetComponent(T);
                    if (t != null)
                    {
                        found++;
                        elements = push(elements, t);
                    }

                    // add all children to stack in reverse order
                    for (int i = cur.childCount - 1; i >= 0; i--)
                        stack = push(stack, cur.GetChild(i));
                }
            }
            if (found > 0) log("Found " + found + " matching component(s)");
            return elements;
        }

        private Component[] push(Component[] stack, Component n)
        {
            var added = false;
            for (int i = 0; i < stack.Length; i++)
            {
                if (stack[i] == null)
                {
                    stack[i] = n;
                    added = true;
                    break;
                }
            }
            // unable to add due to stackoverflow, expand stack and retry
            if (!added)
            {
                stack = expand(stack, 16);
                for (int i = 0; i < stack.Length; i++)
                {
                    if (stack[i] == null)
                    {
                        stack[i] = n;
                        added = true;
                        break;
                    }
                }
            }
            return stack;
        }

        private Component pop(Component[] stack)
        {
            // find first non-null entry in the stack, remove and return.
            var last = -1;
            for (int i = 0; i < stack.Length; i++)
            {
                var item = stack[i];
                if (item != null) last = i;
                if (item == null) break;
            }
            if (last == -1) return null;
            var t = stack[last];
            stack[last] = null;
            return t;
        }

        private Component[] expand(Component[] arr, int add)
        {
            // expand beyond size of self if children are found
            var newArray = new Component[arr.Length + add];
            var index = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == null) continue;
                newArray[index] = arr[i];
                index++;
            }
            return newArray;
        }
    }
}