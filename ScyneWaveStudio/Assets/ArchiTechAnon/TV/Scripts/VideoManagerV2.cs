
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDK3.Video.Components.Base;

namespace ArchiTech
{
    [RequireComponent(typeof(BaseVRCVideoPlayer))]
    public class VideoManagerV2 : UdonSharpBehaviour
    {
        [HideInInspector] public BaseVRCVideoPlayer player;
        [HideInInspector] public bool isVisible;
        private TVManagerV2 tv;
        public GameObject[] screens;
        public AudioSource[] speakers;
        private VideoError lastError;
        private bool muted = true;
        private float volume = 0.5f;
        private bool audio3d = true;

        private bool init = false;
        private bool skipLog = false;

        private void initialize()
        {
            if (init) return;
            player = (BaseVRCVideoPlayer)GetComponent(typeof(BaseVRCVideoPlayer));
            init = true;
        }
        void Start()
        {
            if (!init)
            {
                initialize();
                log($"Hiding self {gameObject.name}");
                _Hide();
            }
        }


        // === Player Proxy Methods ===

        new void OnVideoStart() => tv._OnVideoPlayerStart();
        // new void OnVideoEnd() => tv.OnVideoPlayerEnd();
        new void OnVideoError(VideoError error) => tv._OnVideoPlayerError(error);
        // new void OnVideoLoop() => tv.OnVideoPlayerLoop();
        // new void OnVideoPause() => tv.OnVideoPlayerPause();
        // new void OnVideoPlay() => tv.OnVideoPlayerPlay();
        // new void OnVideoReady() => tv.OnVideoPlayerReady();


        // === Public events to control the video player parts ===

        public void _Show()
        {
            if (!init) initialize();
            foreach (var screen in screens)
            {
                if (screen == null) continue;
                screen.SetActive(true);
            }
            _UnMute();
            isVisible = true;
            if (tv != null)
                log($"{tv.gameObject.name} [{gameObject.name}] activated");
        }

        public void _Hide()
        {
            if (!init) initialize();
            _Mute();
            player.Stop();
            foreach (var screen in screens)
                screen.SetActive(false);
            isVisible = false;
            if (tv != null)
                log($"{tv.gameObject.name} [{gameObject.name}] deactivated");
        }

        public void _ApplyStateTo(VideoManagerV2 other)
        {
            other._ChangeMute(muted);
            other._ChangeVolume(volume);
            other._ChangeAudioMode(audio3d);
        }

        public void _ChangeMute(bool muted)
        {
            if (!init) initialize();
            this.muted = muted;
            foreach (AudioSource speaker in speakers)
            {
                if (speaker == null) continue;
                speaker.mute = muted;
            }
        }
        public void _Mute() => _ChangeMute(true);
        public void _UnMute() => _ChangeMute(false);


        public void _ChangeAudioMode(bool use3dAudio)
        {
            if (!init) initialize();
            this.audio3d = use3dAudio;
            float blend = use3dAudio ? 1.0f : 0.0f;
            foreach (AudioSource speaker in speakers)
            {
                if (speaker == null) continue;
                speaker.spatialBlend = blend;
                speaker.spread = blend * 360f;
            }
        }
        public void _Use3dAudio() => _ChangeAudioMode(true);
        public void _Use2dAudio() => _ChangeAudioMode(false);


        public void _ChangeVolume(float volume)
        {
            if (!init) initialize();
            this.volume = volume;
            foreach (AudioSource speaker in speakers)
            {
                if (speaker == null) continue;
                speaker.volume = volume;
            }
        }


        // ================= Helper Methods =================

        public void _SetTV(TVManagerV2 manager) => tv = manager;

        private void log(string value)
        {
            if (!skipLog) Debug.Log($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#00ccaa>VideoManagerV2</color>] {value}");
        }
        private void warn(string value)
        {
            if (!skipLog) Debug.LogWarning($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#00ccaa>VideoManagerV2</color>] {value}");
        }
        private void err(string value)
        {
            if (!skipLog) Debug.LogError($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#00ccaa>VideoManagerV2</color>] {value}");
        }
    }

}