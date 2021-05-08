
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using UnityEngine.UI;
namespace ArchiTech
{
    public class TVManager : UdonSharpBehaviour
    {
        public Pallete pallete;
        private Transform ctx;
        private VideoManager[] videoManagers;
        private VideoManager activeManager;
        private VideoManager nextManager;
        private VideoManager prevManager;
        private VRCUrlInputField URLInput;
        [Tooltip("This is the URL to set as automatically playing when the first user joins a new instance. This has no bearing on an existing instance as the TV has already been syncing data after the initial point.")]
        public VRCUrl autoplayURL = VRCUrl.Empty;
        [Range(0f, 60f)]
        [Tooltip("This is used to offset the delay of the initial attempt for a TV to fetch it's URL when a user joins a world. Primarily used if there are multiple TVs in the world to avoid excessive rate limiting issues. Make sure each TV has a different value (recommend intervals of 3).")]
        public float autoplayStartOffset = 0f;
        public bool isMasterOnly = false;
        [UdonSynced] private VRCUrl syncURL;
        private VRCUrl currentURL;
        private VRCUrl inputURL;

        // UI Components
        [HideInInspector]
        private Dropdown resolutionModifier;
        private Toggle syncModifier;
        private Slider seekModifier;
        private Toggle muteModifier;
        private Toggle audioModeModifier;
        private GameObject loadingOverlay;
        private Text currentOwner;
        private Image loadingSpinner;
        private Image syncIndicator;
        private Image audioModeIndicator;
        private Image playIndicator;
        private Image pauseIndicator;
        private Image stopIndicator;
        private Image muteIndicator;
        private Image loopIndicator;
        private Text currentTimeIndicator;
        private Text endTimeIndicator;
        // End UI Components

        private float waitUntil = 0f;
        private bool loading = false;
        private bool refreshUI = false;
        private float jumpToTime = 0f;
        [UdonSynced] private float syncTime = 0f;
        [UdonSynced] private int ownerState = 0;
        [UdonSynced] private bool ownerLoop = false;
        // ownerState/localState values: 0 = stopped, 1 = playing, 2 = paused
        private int localState = 0;
        private bool locallyPaused = false;
        private bool localLoop = false;
        private int lastRes = -1;
        private double playingThreshold = 0.5;
        private double pausedThreshold = 5.0;
        private bool errorOccurred = false;
        private bool init = false;

        private void log(string value)
        {
            Debug.Log("[<color=#00ff00>TVManager</color>] " + value);
        }

        private void err(string value)
        {
            Debug.LogError("[<color=#00ff00>TVManager</color>] " + value);
        }

        private void assignRefs()
        {
            ctx = transform.childCount > 0 ? transform : transform.parent;
            log("Starting TVManager - " + ctx.name);
            if (pallete == null) pallete = ctx.GetComponent<Pallete>();
            var m = ctx.Find("VideoPlayer");
            videoManagers = m.GetComponentsInChildren<VideoManager>();
            foreach (var manager in videoManagers) manager.SetTV(this);
            var panelUI = m.Find("UI").Find("Offset");
            var overlay = panelUI.Find("LoadingOverlay");
            loadingOverlay = overlay.gameObject;
            loadingSpinner = overlay.Find("SpinnerBG").Find("Spinner").GetComponent<Image>();
            resolutionModifier = panelUI.Find("ResolutionModifier").GetComponent<Dropdown>();
            syncModifier = panelUI.Find("SyncModifier").GetComponent<Toggle>();
            syncIndicator = syncModifier.GetComponent<Image>();
            audioModeModifier = panelUI.Find("AudioModeModifier").GetComponent<Toggle>();
            audioModeIndicator = audioModeModifier.GetComponent<Image>();
            playIndicator = panelUI.Find("PlayModifier").GetComponent<Image>();
            pauseIndicator = panelUI.Find("PauseModifier").GetComponent<Image>();
            muteModifier = panelUI.Find("MuteModifier").GetComponent<Toggle>();
            muteIndicator = muteModifier.GetComponent<Image>();
            stopIndicator = panelUI.Find("StopModifier").GetComponent<Image>();
            loopIndicator = panelUI.Find("LoopModifier").GetComponent<Image>();
            URLInput = (VRCUrlInputField)panelUI.Find("VideoInput").GetComponent(typeof(VRCUrlInputField));
            var underTV = ctx.Find("TVScreen").Find("UI").Find("Offset");
            currentOwner = underTV.Find("Owner").GetComponent<Text>();
            currentTimeIndicator = underTV.Find("CurrentTime").GetComponent<Text>();
            endTimeIndicator = underTV.Find("EndTime").GetComponent<Text>();
            seekModifier = underTV.Find("SeekModifier").GetComponent<Slider>();
            // assign pallete colors to static Graphics
            var active = pallete.uiActive;
            var inactive = pallete.uiInactive;
            var vol = panelUI.Find("VolumeModifier");
            vol.Find("Background").GetComponent<Image>().color = inactive;
            vol.Find("Fill Area").Find("Fill").GetComponent<Image>().color = active;
            seekModifier.transform.Find("Background").GetComponent<Image>().color = inactive;
            seekModifier.transform.Find("Fill Area").Find("Fill").GetComponent<Image>().color = active;
            if (isMasterOnly && !Networking.LocalPlayer.isMaster)
            {
                // disable URL imput and subsequent ownership of player to master only.
                panelUI.Find("ChangeVideo").gameObject.SetActive(false);
                panelUI.Find("VideoInput").gameObject.SetActive(false);
            }
        }

        void Start()
        {
            assignRefs(); // grab ALL the things
            refreshUI = true; // flag the UI as needing an update upon first Update call
            inputURL = currentURL = syncURL = VRCUrl.Empty; // init interal URLS to empty instead of null
            if (isOwner() && autoplayURL != null && autoplayURL.Get() != "") syncURL = autoplayURL; // initialize the autoplay URL if there is one.
            syncModifier.isOn = true; // always by default global sync should be on. Player should manually toggle off.
            seekModifier.value = 0f; // enforce seek slider to initalize at 0
            seekModifier.enabled = false; // seeking is only for owner, so disable by default.
            UpdateResolution(); // set initial video manager
            UpdateSync(); // set enabled status for sync and seekbar
            waitUntil = Time.realtimeSinceStartup + 3f + autoplayStartOffset; // make the script wait a few seconds before trying to fetch the video data for the first time.
            init = true;
        }


        // ================= Main Logic ====================


        void Update()
        {
            if (!init) return; // has not yet been initialized
                               // manager was swapped, get active info and update to next
                               // checks for ratelimiting
            if (activeManager != nextManager)
            {
                // wait until the timeout has cleard
                if (Time.realtimeSinceStartup < waitUntil) return;
                if (activeManager != null)
                {
                    activeManager.player.Pause();
                    jumpToTime = activeManager.player.GetTime();
                }
                prevManager = activeManager;
                activeManager = nextManager;
                refreshVideo(false);
                return; // video has been refreshed, hold till next Update call
            }
            // activeManager has not been fully init'd yet, skip current cycle.
            if (activeManager.player == null) return;
            // make sure activeManager is not empty
            // if (activeManager == null) return;
            float curTime = activeManager.player.GetTime();
            float endTime = activeManager.player.GetDuration();
            bool isLive = endTime == Mathf.Infinity;
            // video looping check
            if (localLoop && curTime >= endTime)
            {
                activeManager.player.SetTime(0f);
                return; // to make looping more efficient, skip the sync enforcement for this cycle
            }
            // update current timestamp
            if (activeManager.player.IsPlaying)
            {
                seekModifier.value = curTime / endTime;
                currentTimeIndicator.text = getReadableTime(curTime);
            }

            // Anything after this point requires: sync enabled, not in loading state
            if (!syncModifier.isOn || loading || errorOccurred) return;

            // detects when the syncURL has changed and forces a video refresh if sync is enabled
            // this is triggered when another player changes the video source
            if (currentURL.Get() != syncURL.Get()) refreshVideo(false);

            var owner = isOwner();

            // synchronize video loop with video owner
            if (!owner)
            {
                localLoop = ownerLoop;
                refreshUI = true;
            }

            // synchronize video state with video owner only if local is not stopped
            // checks for ratelimiting
            if (!owner && localState != ownerState && Time.realtimeSinceStartup > waitUntil)
            {
                switch (ownerState)
                {
                    // always enforce stopping
                    case 0: Stop(); break;
                    // allow the local player to be paused if owner is playing
                    case 1: if (!locallyPaused) Play(); break;
                    // if owner pauses, unset the local pause flag
                    case 2: Pause(); locallyPaused = false; break;
                    default: break;
                }
            }
            // This handles updating the sync time from owner to others.
            // owner must be playing and local must not be stopped
            // skips syncing time if video is a livestream
            if (ownerState == 1 && localState > 0)
            {
                // pull syncTime from owner
                if (owner) syncTime = curTime;
                else if (!isLive && (
                    // when player is playing, ensure sync time is tighter (eg. 0.5 seconds)
                    (activeManager.player.IsPlaying && Mathf.Abs(curTime - syncTime) > playingThreshold)
                    ||
                    // when player is paused, a looser sync time is acceptable (eg. 5 seconds)
                    (!activeManager.player.IsPlaying && Mathf.Abs(curTime - syncTime) > pausedThreshold)
                )) activeManager.player.SetTime(syncTime);
            }

        }

        void LateUpdate()
        {
            if (!init) return;
            if (loading) // rotate the spinner while loading a video
                loadingSpinner.gameObject.transform.Rotate(0f, 0f, (-200f * Time.deltaTime) % 360f);
            if (localLoop && activeManager != null && activeManager.player.GetTime() >= activeManager.player.GetDuration())
            {
                activeManager.player.SetTime(0f);
                return; // to make looping more efficient, skip the UI update this cycle
            }
            if (refreshUI) updateUI();
        }

        private void updateUI()
        {
            updateColor(syncIndicator, isOwner() ? -1 : syncModifier.isOn ? 1 : 0);
            updateColor(stopIndicator, localState == 0 ? 1 : 0);
            updateColor(playIndicator, localState == 1 ? 1 : 0);
            updateColor(pauseIndicator, localState == 2 ? 1 : 0);
            updateColor(loopIndicator, localLoop ? 1 : 0);
            updateColor(audioModeIndicator, audioModeModifier.isOn ? 1 : 0);
            updateColor(muteIndicator, muteModifier.isOn ? 1 : 0);
            updateColor(currentOwner, 0);
            refreshUI = false;
        }

        private void refreshVideo(bool newUrl)
        {
            // if both url sources are empty, skip
            if (inputURL.Get() == "" && syncURL.Get() == "") return;
            // change sync url if newUrl is specified and the input is not empty
            if (newUrl && inputURL.Get() != "") syncURL = inputURL;
            inputURL = VRCUrl.Empty;
            // only activate the video player if the syncURL isn't empty
            if (syncURL.Get() != "")
            {
                log(ctx.name + " [" + activeManager.managerName + "]" + " loading URL: " + syncURL);
                isLoading(true);
                // mute player if new video source is used, this helps prevent audio studdering when switching.
                if (newUrl) activeManager.Mute(true);
                activeManager.player.PlayURL(syncURL);
                currentURL = syncURL;
            }
        }


        // ================== Video Manager Events ==================


        // Only gets called on LoadUrl, not on PlayUrl apparently
        public void VideoReady()
        {

        }

        public void VideoStart()
        {
            if (!activeManager.isVisible) activeManager.Show();
            if (loading) // only execute this section when the player is refreshing the video
            {
                if (prevManager != activeManager)
                {
                    if (prevManager != null) prevManager.Hide();
                    prevManager = activeManager;
                }
                // clear the url input field for whoever triggered the video change
                if (isOwner()) URLInput.SetUrl(VRCUrl.Empty);
                UpdateSync();
                log(ctx.name + " [" + activeManager.managerName + "] Now Playing: " + syncURL);
                isLoading(false);
            }
            endTimeIndicator.text = getReadableTime(activeManager.player.GetDuration());
            currentOwner.text = Networking.GetOwner(gameObject).displayName + " @ " + getUrlDomain(syncURL);
            if (!muteModifier.isOn) activeManager.Mute(false);
            if (jumpToTime > 0f)
            {
                log("Jumping " + ctx.name + " [" + activeManager.managerName + "] to timestamp: " + jumpToTime);
                activeManager.player.SetTime(jumpToTime);
                jumpToTime = 0f;
            }
            if (isOwner()) ownerState = 1;
            localState = 1;
            errorOccurred = false;
            refreshUI = true;
        }

        // public void VideoPlay()
        // {

        // }

        // public void VideoPause()
        // {

        // }

        // public void VideoLoop()
        // {

        // }

        // public void VideoEnd()
        // {

        // }

        public void VideoError(VideoError error)
        {
            err("Video Error: " + error);
            Stop();
            errorOccurred = true;
            switch (error)
            {
                case VRC.SDK3.Components.Video.VideoError.PlayerError:
                    currentOwner.text = "[Player Error] Unable to load video. If livestream, it has stopped/ended.";
                    break;
                case VRC.SDK3.Components.Video.VideoError.AccessDenied:
                    currentOwner.text = "[Access Denied] Try enabling Untrusted URLs in Settings";
                    break;
                case VRC.SDK3.Components.Video.VideoError.InvalidURL:
                    currentOwner.text = "[Invalid URL] Parsing issue? Wait a moment then click Play to retry.";
                    break;
                case VRC.SDK3.Components.Video.VideoError.RateLimited:
                    currentOwner.text = "[Rate Limited] Waiting 3 seconds to retry.";
                    errorOccurred = false; // skip error shortcircut and use a time check instead
                    waitUntil = Time.realtimeSinceStartup + 3f;
                    break;
                default:
                    currentOwner.text = "[ERROR] " + error;
                    break;
            }
            updateColor(stopIndicator, -2);
            updateColor(currentOwner, -2);
        }


        // ================ UI EVENTS ======================


        public void UpdateVolume() => activeManager.UpdateVolume();

        public void UpdateAudioMode()
        {
            activeManager.UpdateAudioMode();
            refreshUI = true;
        }

        public void ToggleLooping()
        {
            if (isOwner())
                localLoop = ownerLoop = !ownerLoop;
            else if (!syncModifier.isOn)
                localLoop = !localLoop;
            refreshUI = true;
        }

        public void ToggleMute()
        {
            activeManager.UpdateMute();
            refreshUI = true;
        }

        public void UpdateResolution()
        {
            // no need to change if same is picked
            if (resolutionModifier.value == lastRes) return;
            // do not allow changing resolution while a video is loading.
            if (loading)
            {
                resolutionModifier.value = lastRes;
                return;
            }
            if (resolutionModifier.value >= videoManagers.Length)
            {
                err("Resolution Modifier value too large: Expected " + videoManagers.Length + " - Actual " + resolutionModifier.value);
                return;
            }
            if (activeManager != null)
            {
                if (activeManager.player.GetTime() > 0f) jumpToTime = activeManager.player.GetTime();
                if (activeManager.player.IsPlaying) activeManager.player.Pause();
            }
            nextManager = videoManagers[resolutionModifier.value];
            lastRes = resolutionModifier.value;
            log("Switching to: " + ctx.name + " [" + nextManager.managerName + "]");
        }

        // Updates the current video based on the UI's VRCUrlInput element's contents.
        public void UpdateVideoSource()
        {
            if (loading || (isMasterOnly && !Networking.LocalPlayer.isMaster)) return;
            inputURL = URLInput.GetUrl();
            takeOwnership();
            refreshVideo(true);
        }

        // Updates the current video based on whatever VRCUrl is in inputURL variable, which is generally set prior to calling this method. Ideal to use from a UGraph script.
        public void SwitchVideo()
        {
            if (loading) return;
            takeOwnership();
            refreshVideo(true);
        }

        // Updates the current video based on the VRCUrl that is passed in. Can only be used from another U# script.
        public void SwitchVideoTo(VRCUrl newURL)
        {
            if (loading) return;
            inputURL = newURL;
            takeOwnership();
            refreshVideo(true);
        }

        // Updates the current video with the same VRCUrl. Basically a force refresh of the video.
        public void SyncVideoSource()
        {
            refreshVideo(false);
        }

        public void UpdateSeekTime()
        {
            var dur = activeManager.player.GetDuration();
            if (dur == Mathf.Infinity) return;
            var time = dur * seekModifier.value;
            activeManager.player.SetTime(time);
            currentTimeIndicator.text = getReadableTime(time);
        }

        public void UpdateSeekForward() => activeManager.player.SetTime(activeManager.player.GetTime() + 10f);
        public void UpdateSeekBackward() => activeManager.player.SetTime(activeManager.player.GetTime() - 10f);

        public void UpdateSync()
        {
            if (isOwner())
            {
                // owner cannot disable sync, but should be able to seek the video
                syncModifier.enabled = false;
                seekModifier.enabled = true;
            }
            else if (syncModifier.isOn)
            {
                // non-owner can disable sync, but should not be able to seek the video
                syncModifier.enabled = true;
                seekModifier.enabled = false;
            }
            else
            {
                // non-owner can enable sync, and while sync is disabled, can seek the video
                syncModifier.enabled = true;
                seekModifier.enabled = true;
            }

            if (activeManager == null || activeManager.player.GetDuration() == 0)
            {
                // enforce disabling of the seekbar when a video is not loaded
                seekModifier.enabled = false;
            }
            refreshUI = true;
        }

        public void Play()
        {
            if (activeManager.player.GetDuration() > 0)
            {
                activeManager.player.Play();
                if (isOwner()) ownerState = 1;
                localState = 1;
                // if video is at end and user forces play, force loop the video one time.
                if (activeManager.player.GetTime() == activeManager.player.GetDuration())
                    activeManager.player.SetTime(0);
            }
            else refreshVideo(false);
            refreshUI = true;
        }

        public void Pause()
        {
            if (activeManager.player.GetDuration() > 0)
            {
                activeManager.player.Pause();
                if (isOwner()) ownerState = 2;
                localState = 2;
                locallyPaused = true; // flag to determine if pause was locally triggered
            }
            refreshUI = true;
        }

        public void Stop()
        {
            activeManager.Hide();
            if (isOwner())
            {
                ownerState = 0;
                activeManager.player.SetTime(0);
            }
            localState = 0;
            isLoading(false);
            errorOccurred = false;
            refreshUI = true;
        }


        // ============== Helper Methods =================


        private string getUrlDomain(VRCUrl url)
        {
            // strip the protocol
            var s = url.Get().Split(new string[] { "://" }, 2, System.StringSplitOptions.None);
            // strip everything after the first slash
            s = s[1].Split(new string[] { "/" }, 2, System.StringSplitOptions.None);
            // just to be sure, strip everything after the question mark if one is present
            s = s[0].Split(new string[] { "?" }, 2, System.StringSplitOptions.None);
            // return the url's domain value
            return s[0];
        }

        private void takeOwnership() => Networking.SetOwner(Networking.LocalPlayer, gameObject);

        private bool isOwner() => Networking.GetOwner(gameObject) == Networking.LocalPlayer;

        private void isLoading(bool yes)
        {
            loading = yes;
            loadingOverlay.SetActive(yes);
            log("Loading state: " + yes);
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

        private void updateColor(Graphic target, int mode)
        {
            switch (mode)
            {
                case -2: // error
                    target.color = new Color(pallete.uiError.r, pallete.uiError.g, pallete.uiError.b, target.color.a);
                    break;
                case -1: // disabled
                    target.color = new Color(pallete.uiDisabled.r, pallete.uiDisabled.g, pallete.uiDisabled.b, target.color.a);
                    break;
                case 0: // inactive
                    target.color = new Color(pallete.uiInactive.r, pallete.uiInactive.g, pallete.uiInactive.b, target.color.a);
                    break;
                case 1: // active
                    target.color = new Color(pallete.uiActive.r, pallete.uiActive.g, pallete.uiActive.b, target.color.a);
                    break;

                default: break; // invalid value passed
            }
        }
    }
}