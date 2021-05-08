
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.Udon;
using VRC.Udon.Common.Enums;

namespace ArchiTech
{
    public class BasicUI : UdonSharpBehaviour
    {
        public Pallete pallete;
        public TVManagerV2 tv;
        [Space]


        [Header("UI Components")]
        public Slider seek;
        public Button seekForward;
        public Button seekBackward;
        public Text info;
        public Text currentTime;
        public Text endTime;
        public Slider volume;
        public VRCUrlInputField inputURL;
        public Button play;
        public Button pause;
        public Button stop;
        public Button mute;
        public Button loop;
        public Button sync;
        public Dropdown resolution;
        public Button audioMode;
        public Button locked;
        public GameObject lockedNotice;
        public GameObject loadingOverlay;
        private Transform loadingSpinner;
        private Image playIndicator;
        private Image pauseIndicator;
        private Image stopIndicator;
        private Image muteIndicator;
        private Image loopIndicator;
        // private Image syncIndicator;
        private Image audioModeIndicator;
        private Image lockedIndicator;

        private Color infoOriginal;

        private VideoError OUT_OnVideoPlayerError_VideoError_Error;
        private float OUT_OnVolumeChange_float_Percent;
        private int OUT_OnVideoPlayerChange_int_Index;

        private bool hasPallete = true;
        private bool unsafeSeek;
        private float duration;
        private float nextCheck;
        private bool init = false;
        private bool tvInit = false;
        private bool skipLog = false;

        private void initialize()
        {
            if (init) return;
            if (pallete == null) pallete = GetComponent<Pallete>();
            if (pallete == null) hasPallete = false;
            if (hasPallete)
            {
                infoOriginal = info.color;
            }
            playIndicator = play.transform.parent.Find("PlayIndicator").GetComponent<Image>();
            pauseIndicator = pause.transform.parent.Find("PauseIndicator").GetComponent<Image>();
            stopIndicator = stop.transform.parent.Find("StopIndicator").GetComponent<Image>();
            muteIndicator = mute.transform.parent.Find("MuteIndicator").GetComponent<Image>();
            loopIndicator = loop.transform.parent.Find("LoopIndicator").GetComponent<Image>();
            audioModeIndicator = audioMode.transform.parent.Find("AudioModeIndicator").GetComponent<Image>();
            // syncIndicator = sync.transform.parent.Find("SyncIndicator").GetComponent<Image>();
            lockedIndicator = locked.transform.parent.Find("LockIndicator").GetComponent<Image>();
            loadingSpinner = loadingOverlay.transform.Find("Spinner").transform;
            lockedNotice.SetActive(false);
            if (pallete == null) pallete = GetComponent<Pallete>();
            if (pallete == null) hasPallete = false;
            if (hasPallete)
            {
                playIndicator.color = pallete.uiInactive;
                pauseIndicator.color = pallete.uiInactive;
                stopIndicator.color = pallete.uiActive;
                muteIndicator.color = pallete.uiInactive;
                audioModeIndicator.color = pallete.uiActive;
                loopIndicator.color = Networking.IsMaster ? pallete.uiInactive : pallete.uiDisabled;
                // syncIndicator.color = Networking.IsMaster ? pallete.uiDisabled : pallete.uiActive;
                if (!tv.allowMasterLockToggle) lockedIndicator.gameObject.SetActive(false);
                else if (Networking.IsMaster) lockedIndicator.color = tv.lockedToMasterByDefault ? pallete.uiActive : pallete.uiInactive;
                else lockedIndicator.color = pallete.uiDisabled;
            }

            tv._RegisterUdonSharpEventReceiver(this);
            init = true;
        }

        void Start()
        {
            initialize();
        }

        void Update()
        {
            if (!tvInit) return;
            // rotate the spinner while loading a video
            if (loadingOverlay.activeInHierarchy)
                loadingSpinner.Rotate(0f, 0f, (-200f * Time.deltaTime) % 360f);
            if (Time.realtimeSinceStartup > nextCheck)
            {
                nextCheck = Time.realtimeSinceStartup + 1f;
                float timestamp = tv.currentTime;
                // to prevent recursion, don't update the seek value, just update the handle's visual position
                if (duration != Mathf.Infinity)
                {
                    unsafeSeek = true;
                    seek.value = timestamp / duration;
                    unsafeSeek = false;
                }
                currentTime.text = getReadableTime(timestamp);
            }
        }

        new void OnPlayerLeft(VRCPlayerApi player)
        {
            var owner = isOwner();
            if (owner)
            {
                sync.enabled = false;
                inputURL.gameObject.SetActive(true);
            }
            if (!hasPallete) return;
            if (Networking.IsMaster) lockedIndicator.color = tv.localLocked ? pallete.uiActive : pallete.uiInactive;
            else lockedIndicator.color = pallete.uiDisabled;
            if (owner)
            {
                // syncIndicator.color = pallete.uiDisabled;
                loopIndicator.color = pallete.uiInactive;
            }
        }


        // === Events from the UI elements to forward to the TV ===

        public void _Play() => tv._Play();
        public void _Pause() => tv._Pause();
        public void _Stop() => tv._Stop();
        public void _ToggleMute() => tv._ToggleMute();
        public void _ToggleAudioMode() => tv._ToggleAudioMode();
        public void _ToggleLoop() => tv._ToggleLoop();
        public void _ToggleLock() => tv._ToggleLock();
        public void _ToggleSync()
        {
            if (sync.enabled) tv._ReSync();
        }
        public void _ReSync() => tv._ReSync();
        public void _ChangeVolume()
        {
            if (volume.value != OUT_OnVolumeChange_float_Percent)
                tv._ChangeVolumeTo(volume.value);
        }
        public void _ChangeVideo()
        {
            if (inputURL.GetUrl().Get() != string.Empty)
            {
                log("New URL");
                tv.IN_ChangeMedia_VRCUrl_Url = inputURL.GetUrl();
                tv._ChangeMedia();
                // tv._ChangeVideoTo(inputURL.GetUrl());
                inputURL.SetUrl(VRCUrl.Empty);
            }
            else
            {
                log("Old URL");
                tv._ChangeMedia();
            }
        }
        public void _ChangeResolution()
        {
            if (resolution.value != OUT_OnVideoPlayerChange_int_Index)
            {
                tv._ChangeVideoPlayerTo(resolution.value);
            }
        }

        public void _Seek()
        {
            if (!unsafeSeek)
                tv._ChangeSeekTimeTo(seek.value);
        }
        public void _SeekForward() => tv._SeekForward();
        
        public void _SeekBackward() => tv._SeekBackward();
        


        // === Events received from the TV event forwarding ===

        public void _OnMediaStart()
        {
            float startTimestamp = tv.startTime;
            float endTimestamp = tv.endTime;
            duration = tv.videoDuration;
            if (endTimestamp == Mathf.Infinity)
            {
                seek.minValue = 0f;
                seek.maxValue = 1f;
                seek.value = seek.maxValue;
            }
            else
            {
                seek.minValue = startTimestamp / duration;
                seek.maxValue = endTimestamp / duration;
            }
            endTime.text = getReadableTime(endTimestamp);
        }

        public void _OnOwnerChange()
        {
            _OnPlay(); // force redraw of the UI
        }

        public void _OnPlay()
        {
            bool isTVOwner = Networking.IsOwner(tv.gameObject);
            sync.enabled = !isTVOwner;
            if (!hasPallete) return;
            info.color = infoOriginal;
            // Check if loop logic was previously disabled, set inactive if so, otherwise just leave it as is.
            if (loopIndicator.color == pallete.uiDisabled)
            {
                loopIndicator.color = pallete.uiInactive;
            }
            Color syncColor = pallete.uiInactive;
            if (isTVOwner) syncColor = pallete.uiDisabled;
            else if (tv.syncToOwner)
            {
                syncColor = pallete.uiActive;
                // loop logic is disabled when non-owner is synced to owner, reflect in UI by setting disabled
                loopIndicator.color = pallete.uiDisabled;
            }
            // syncIndicator.color = syncColor;
            playIndicator.color = pallete.uiActive;
            pauseIndicator.color = pallete.uiInactive;
            stopIndicator.color = pallete.uiInactive;
            updateInfo();
        }
        public void _OnPause()
        {
            if (!hasPallete) return;
            playIndicator.color = pallete.uiInactive;
            pauseIndicator.color = pallete.uiActive;
            stopIndicator.color = pallete.uiInactive;
        }
        public void _OnStop()
        {
            if (!hasPallete) return;
            playIndicator.color = pallete.uiInactive;
            pauseIndicator.color = pallete.uiInactive;
            stopIndicator.color = pallete.uiActive;
        }
        public void _OnLoading()
        {
            loadingOverlay.SetActive(true);
        }
        public void _OnLoadingEnd()
        {
            loadingOverlay.SetActive(false);
        }
        public void _OnMute()
        {
            if (!hasPallete) return;
            muteIndicator.color = pallete.uiActive;
        }
        public void _OnUnMute()
        {
            if (!hasPallete) return;
            muteIndicator.color = pallete.uiInactive;
        }
        public void _OnAudioMode2d()
        {
            if (!hasPallete) return;
            audioModeIndicator.color = pallete.uiInactive;
        }
        public void _OnAudioMode3d()
        {
            if (!hasPallete) return;
            audioModeIndicator.color = pallete.uiActive;
        }
        public void _OnEnableLoop()
        {
            if (!hasPallete) return;
            loopIndicator.color = pallete.uiActive;
        }
        public void _OnDisableLoop()
        {
            if (!hasPallete) return;
            loopIndicator.color = pallete.uiInactive;
        }
        public void _OnSync()
        {
            if (!hasPallete) return;
            if (sync.enabled)
            {
                // syncIndicator.color = pallete.uiActive;
                loopIndicator.color = pallete.uiDisabled;
            }
        }
        public void _OnDeSync()
        {
            if (!hasPallete) return;
            if (sync.enabled)
            {
                // syncIndicator.color = pallete.uiInactive;
                loopIndicator.color = pallete.uiInactive;
            }
        }
        public void _OnVolumeChange()
        {
            if (volume.value != OUT_OnVolumeChange_float_Percent)
                volume.value = OUT_OnVolumeChange_float_Percent;
        }
        public void _OnVideoPlayerChange()
        {
            if (resolution.value != OUT_OnVideoPlayerChange_int_Index)
                resolution.value = OUT_OnVideoPlayerChange_int_Index;
        }
        public void _OnError()
        {
            if (!hasPallete) return;
            playIndicator.color = pallete.uiInactive;
            pauseIndicator.color = pallete.uiInactive;
            stopIndicator.color = pallete.uiError;
        }

        public void _OnLock()
        {
            if (!Networking.IsMaster)
            {
                inputURL.gameObject.SetActive(false);
                lockedNotice.SetActive(true);
            }
            if (!hasPallete) return;
            lockedIndicator.color = pallete.uiActive;
        }

        public void _OnUnLock()
        {
            inputURL.gameObject.SetActive(true);
            lockedNotice.SetActive(false);
            if (!hasPallete) return;
            if (isOwner()) lockedIndicator.color = pallete.uiInactive;
            else lockedIndicator.color = pallete.uiDisabled;
        }

        public void _OnVideoPlayerError()
        {
            string t;
            switch (OUT_OnVideoPlayerError_VideoError_Error)
            {
                case VideoError.PlayerError:
                    t = "[Player Error] Unable to load video. If livestream, it has stopped/ended.";
                    break;
                case VideoError.AccessDenied:
                    t = "[Access Denied] Try enabling Untrusted URLs in Settings";
                    break;
                case VideoError.InvalidURL:
                    string param = getUrlParam(tv.localUrl, "list");
                    if (param != string.Empty)
                        t = "[Timeout Error] Failed fetching video. Please remove any playlist parameter and try again.";
                    else
                        t = "[Invalid URL] Parsing issue? Wait a moment then try again.";
                    break;
                case VideoError.RateLimited:
                    t = "[Rate Limited] Waiting 3 seconds to retry.";
                    break;
                default:
                    t = $"[ERROR] {OUT_OnVideoPlayerError_VideoError_Error}";
                    break;
            }
            info.text = t;
            if (!hasPallete) return;
            info.color = Color.red;
        }

        public void _OnReady()
        {
            tvInit = true;
        }


        // === Helper Methods ===       

        private bool isOwner() => Networking.GetOwner(tv.gameObject) == Networking.LocalPlayer;
        private void updateInfo()
        {
            var player = Networking.GetOwner(tv.gameObject);
            var source = getUrlDomain(tv.localUrl);
            info.text = $"[{player.displayName} {player.playerId}] {source}";
            if (player != Networking.LocalPlayer) seek.enabled = false;
            else seek.enabled = true;
        }

        private string getReadableTime(float duration)
        {
            if (duration == Mathf.Infinity) return "Live";
            if (float.IsNaN(duration)) duration = 0f;
            int seconds = (int)duration % 60;
            int minutes = (int)(duration / 60) % 60;
            int hours = (int)(duration / 60 / 60) % 60;
            if (hours > 0)
                return string.Format("{0}:{1:D2}:{2:D2}", hours, minutes, seconds);
            return string.Format("{0:D2}:{1:D2}", minutes, seconds);
        }

        private string getUrlDomain(VRCUrl url)
        {
            // strip the protocol
            var s = url.Get().Split(new string[] { "://" }, 2, System.StringSplitOptions.None);
            if (s.Length == 1) return string.Empty;
            // strip everything after the first slash
            s = s[1].Split(new char[] { '/' }, 2, System.StringSplitOptions.None);
            // just to be sure, strip everything after the question mark if one is present
            s = s[0].Split(new char[] { '?' }, 2, System.StringSplitOptions.None);
            // return the url's domain value
            return s[0];
        }

        private string getUrlParam(VRCUrl url, string name)
        {
            // strip everything before the query parameters
            string[] s = url.Get().Split(new char[] { '?' }, 2, System.StringSplitOptions.None);
            if (s.Length == 1) return string.Empty;
            // just to be sure, strip everything after the url bang if one is present
            s = s[1].Split(new char[] { '#' }, 2, System.StringSplitOptions.None);
            // attempt to find parameter name match
            s = s[0].Split('&');
            foreach (string param in s)
            {
                string[] p = param.Split(new char[] { '=' }, 2, System.StringSplitOptions.None);
                if (p[0] == name)
                {
                    return p[1];
                }
            }
            // if one can't be found, return an empty string
            return string.Empty;
        }


        private void log(string value)
        {
            if (!skipLog) Debug.Log($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#aabb11>BasicUI</color>] {value}");
        }
        private void warn(string value)
        {
            if (!skipLog) Debug.LogWarning($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#aabb11>BasicUI</color>] {value}");
        }
        private void err(string value)
        {
            if (!skipLog) Debug.LogError($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#aabb11>BasicUI</color>] {value}");
        }
    }
}
