
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
    public class SlimUI : UdonSharpBehaviour
    {
        public TVManagerV2 tv;
        public VRCUrlInputField inputURL;
        public Button go;
        public Button play;
        public Button pause;
        public Button stop;
        public Slider seek;
        public Slider loading;
        public Slider volume;
        public Sprite volumeMute;
        public Sprite volumeLow;
        public Sprite volumeMed;
        public Sprite volumeHigh;
        public Button locked;
        public Button unlocked;
        public Text info;
        public Text currentTime;
        public Text endTime;

        private Transform loadingSpinner;
        private Image volumeIndicator;

        private VideoError OUT_OnVideoPlayerError_VideoError_Error;
        private float OUT_OnVolumeChange_float_Percent;

        private float loadingBarDamp = 0f;
        private float duration;
        private bool unsafeSeek = false;
        private bool hasLoading = false;
        private bool hasVolume = false;
        private bool hasLocked = false;
        private bool hasSeek = false;
        private bool hasGo = false;
        private bool hasInput = false;
        private bool hasUnlocked = false;
        private bool hasPause = false;
        private bool hasInfo = false;
        private bool init = false;
        private bool tvInit = false;
        private bool isLoading = false;
        private bool skipVol = false;
        private bool skipLog = false;

        private void initialize()
        {
            if (init) return;

            hasInput = inputURL != null;
            hasLoading = loading != null;
            hasVolume = volume != null;
            hasLocked = locked != null;
            hasSeek = seek != null;
            hasGo = go != null;
            hasUnlocked = unlocked != null;
            hasPause = pause != null;
            hasInfo = info != null;
            if (hasLoading)
                loadingSpinner = loading.transform.Find("Spinner");
            if (hasVolume)
                volumeIndicator = volume.handleRect.transform.Find("Fill").GetComponent<Image>();

            if (!tv.allowMasterLockToggle && hasLocked) locked.gameObject.SetActive(false);
            tv._RegisterUdonSharpEventReceiver(this);
            init = true;
        }

        void Start()
        {
            initialize();
        }

        void LateUpdate()
        {
            if (!tvInit) return;
            if (isLoading && hasLoading)
            {
                // rotate the spinner while loading a video
                loadingSpinner.Rotate(0f, 0f, (-200f * Time.deltaTime) % 360f);
                // Loading bar "animation"
                if (loading.value > 0.95f) return;
                if (loading.value > 0.8f)
                {
                    loading.value = Mathf.SmoothDamp(loading.value, 1f, ref loadingBarDamp, 0.4f);
                }
                else
                {
                    loading.value = Mathf.SmoothDamp(loading.value, 1f, ref loadingBarDamp, 0.3f);
                }
            }
            if (hasSeek)
            {
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
            if (hasInput && isOwner())
            {
                inputURL.gameObject.SetActive(true);
            }

        }
        // === UI EVENTS ===
        public void _UpdateUrlInput()
        {
            if (hasInput && hasGo)
            {
                if (inputURL.GetUrl().Get() == string.Empty)
                    go.gameObject.SetActive(false);
                else go.gameObject.SetActive(true);
            }
        }

        public void _Play() => tv._Play();
        public void _Pause() => tv._Pause();
        public void _Stop() => tv._Stop();
        public void _ReSync() => tv._ReSync();
        public void _ToggleLock() => tv._ToggleLock();
        public void _Seek() {
            if (!unsafeSeek) tv._ChangeSeekTimeTo(seek.value);
        }
        public void _ChangeVolume()
        {
            if (skipVol)
            {
                // prevent the recursive loop
                skipVol = false;
                return;
            }
            if (volume.value != OUT_OnVolumeChange_float_Percent)
            {
                tv._ChangeVolumeTo(volume.value);
                if (volume.value == 0f) volumeIndicator.sprite = volumeMute;
                else if (volume.value == 1f) volumeIndicator.sprite = volumeHigh;
                else if (volume.value > 0.5f) volumeIndicator.sprite = volumeMed;
                else volumeIndicator.sprite = volumeLow;
            }
        }
        public void _ChangeMedia()
        {
            if (inputURL.GetUrl().Get() != string.Empty)
            {
                tv._ChangeMediaTo(inputURL.GetUrl());
                inputURL.SetUrl(VRCUrl.Empty);
            }
        }

        // === TV EVENTS ===
        public void _OnMediaStart()
        {
            float endTimestamp = tv.endTime;
            duration = tv.videoDuration;
            endTime.text = getReadableTime(endTimestamp);
            if (!hasSeek) return;
            if (endTimestamp == Mathf.Infinity)
            {
                seek.minValue = 0f;
                seek.maxValue = 1f;
                unsafeSeek = true;
                seek.value = 1f;
                unsafeSeek = false;
            }
            else
            {
                seek.minValue = tv.startTime / duration;
                seek.maxValue = endTimestamp / duration;
            }
        }

        public void _OnOwnerChange()
        {
            updateInfo();
        }


        public void _OnReady()
        {
            tvInit = true;
        }

        public void _OnPlay()
        {
            if (hasPause) pause.gameObject.SetActive(true);
            updateInfo();
        }


        public void _OnPause()
        {
            if (hasPause) pause.gameObject.SetActive(false);
        }

        public void _OnStop()
        {
            if (hasPause) pause.gameObject.SetActive(false);
        }

        public void _OnLoading()
        {
            if (hasLoading)
            {
                loading.gameObject.SetActive(true);
                loading.value = 0f;
            }
            isLoading = true;
        }
        public void _OnLoadingEnd()
        {
            if (hasLoading) loading.gameObject.SetActive(false);
            isLoading = false;
        }
        public void _OnLock()
        {
            if (hasInput && !Networking.IsMaster)
                inputURL.gameObject.SetActive(false);
            if (hasUnlocked)
                unlocked.gameObject.SetActive(false);
        }
        public void _OnUnLock()
        {
            if (hasInput)
                inputURL.gameObject.SetActive(true);
            if (hasUnlocked)
                unlocked.gameObject.SetActive(true);
        }
        public void _OnVolumeChange()
        {
            if (!hasVolume) return;
            if (volume.value != OUT_OnVolumeChange_float_Percent)
            {
                skipVol = true;
                volume.value = OUT_OnVolumeChange_float_Percent;
                if (volume.value == 0f) volumeIndicator.sprite = volumeMute;
                else if (volume.value == 1f) volumeIndicator.sprite = volumeHigh;
                else if (volume.value > 0.5f) volumeIndicator.sprite = volumeMed;
                else volumeIndicator.sprite = volumeLow;
            }
        }
        public void _OnVideoPlayerError()
        {
            if (!hasInfo) return;
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
        }

        // === helpers ===
        private bool isOwner() => Networking.GetOwner(tv.gameObject) == Networking.LocalPlayer;
        private void updateInfo()
        {
            var player = Networking.GetOwner(tv.gameObject);
            if (hasInfo)
            {
                var source = getUrlDomain(tv.localUrl);
                info.text = $"[{player.displayName} {player.playerId}] {source}";
            }
            if (hasSeek)
            {
                if (player != Networking.LocalPlayer) seek.enabled = false;
                else seek.enabled = true;
            }
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
            if (!skipLog) Debug.Log($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#ffffff>SlimUI</color>] {value}");
        }
        private void warn(string value)
        {
            if (!skipLog) Debug.LogWarning($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#ffffff>SlimUI</color>] {value}");
        }
        private void err(string value)
        {
            if (!skipLog) Debug.LogError($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#ffffff>SlimUI</color>] {value}");
        }
    }
}
