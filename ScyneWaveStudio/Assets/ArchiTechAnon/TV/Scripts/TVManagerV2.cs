
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using UnityEngine.UI;

namespace ArchiTech
{
    [DefaultExecutionOrder(-9999)] // needs to initialize before anything else if possible
    public class TVManagerV2 : UdonSharpBehaviour
    {
        private float EPSILON = Mathf.Epsilon;

        // list of all events that the TVManager may produce.
        private const string EVENT_READY = "_OnReady";
        private const string EVENT_PLAY = "_OnPlay";
        private const string EVENT_PAUSE = "_OnPause";
        private const string EVENT_STOP = "_OnStop";
        private const string EVENT_MEDIASTART = "_OnMediaStart";
        private const string EVENT_MEDIAEND = "_OnMediaEnd";
        private const string EVENT_MEDIALOOP = "_OnMediaLoop";
        private const string EVENT_MEDIACHANGE = "_OnMediaChange";
        private const string EVENT_OWNERCHANGE = "_OnOwnerChange";
        private const string EVENT_VIDEOPLAYERCHANGE = "_OnVideoPlayerChange";
        private const string EVENT_VIDEOPLAYERERROR = "_OnVideoPlayerError";
        private const string EVENT_MUTE = "_OnMute";
        private const string EVENT_UNMUTE = "_OnUnMute";
        private const string EVENT_VOLUMECHANGE = "_OnVolumeChange";
        private const string EVENT_AUDIOMODE3D = "_OnAudioMode3d";
        private const string EVENT_AUDIOMODE2D = "_OnAudioMode2d";
        private const string EVENT_ENABLELOOP = "_OnEnableLoop";
        private const string EVENT_DISABLELOOP = "_OnDisableLoop";
        private const string EVENT_SYNC = "_OnSync";
        private const string EVENT_DESYNC = "_OnDeSync";
        private const string EVENT_LOCK = "_OnLock";
        private const string EVENT_UNLOCK = "_OnUnLock";
        private const string EVENT_LOADING = "_OnLoading";
        private const string EVENT_LOADINGEND = "_OnLoadingEnd";
        private const string EVENT_LOADINGSTOP = "_OnLoadingStop";
        // These variable names are used to pass information back to any event listeners that have been registered.
        // They follow the pattern of the word OUT, the expected type of the target variable and a meaningful name on what that variable is related to.
        // EG: OUT_float_Volume means the data is outgoing, will target a variable of type 'float', and represents the TV's volume value.
        private const string VAR_ERROR = "OUT_OnVideoPlayerError_VideoError_Error";
        private const string VAR_VOLUME = "OUT_OnVolumeChange_float_Percent";
        private const string VAR_VIDEOPLAYER = "OUT_OnVideoPlayerChange_int_Index";
        private const string VAR_OWNER = "OUT_OnOwnerChange_int_Id";

        // enum values for ownerState/currentState/localState
        private const int STOPPED = 0;
        private const int PLAYING = 1;
        private const int PAUSED = 2;

        [Tooltip("This is the URL to set as automatically playing when the first user joins a new instance. This has no bearing on an existing instance as the TV has already been syncing data after the initial point.")]
        public VRCUrl autoplayURL = VRCUrl.Empty;
        [Tooltip("This is used to offset the delay of the initial attempt for a TV to fetch it's URL when a user joins a world. Primarily used if there are multiple TVs in the world to avoid excessive rate limiting issues. Make sure each TV has a different value (recommend intervals of 3).")]
        [Range(0f, 60f)] public float autoplayStartOffset = 0f;
        [Tooltip("The volume that the TV starts off at.")]
        [Range(0f, 1f)] public float initialVolume = 0.3f;
        [Tooltip("Time difference allowed between owner's synced seek time and the local seek time while the video is playing locally. Determines how 'tight' the sync is. If the value is too low it can cause temporary stuttering on non-owner clients. Increase this value by 0.1 until a stable threshold is found.")]
        public float playingThreshold = 0.65f;
        [Tooltip("Time difference allowed between owner's synced seek time and the local seek time while the video is paused locally. Can be thought of as a 'frame preview' of what's currently playing. It's good to have this at a higher value, NOT recommended to have this value less than 1.0")]
        public float pausedThreshold = 5.0f;
        [Tooltip("The player (based on the VideoManagers list below) for the TV to start off on.")]
        public int initialPlayer = 0;
        // There is serious problems with the TV (specifically AVPro rendering to materials) when it's game object is manually disabled in the editor.
        // So instead of disabling it in the editor, the world creator should activate this flag to have the TV automatically hide itself after the initilization completes.
        // This allows AVPro to correctly init the materials references and avoids whitescreen issues.
        [Tooltip("Set this flag to have the TV auto-hide itself after initialization. DO NOT HAVE THE TV DISABLED MANUALLY! THIS WILL CAUSE PROBLEMS! Please use this flag instead.")]
        public bool startHidden = false;
        // this option is explicitly used for the edge case where world owners want to have anyone use the TV regardless.
        // It prevents the instance master from being able to lock the TV down by any means, when the world creator doesn't want them to.
        // Helps against malicious users in the edge case. 
        [Tooltip("This option enables the instance master to lock and unlock the TV. Leaving enabled should be perfectly acceptable in most cases.")]
        public bool allowMasterLockToggle = true;
        [Tooltip("Determines if the video player starts off as locked down to master only. Good for worlds that do public events and similar.")]
        public bool lockedToMasterByDefault = false;
        [Space]

        // Storage for all event subscriber behaviors. 
        // Due to some odd type issues, it requires being stored as a list of components instead of UdonBehaviors.
        // All possible events that this TV triggers will be sent to ALL targets in order of addition
        private Component[] eventTargets;

        // === Video Manager control ===
        public VideoManagerV2[] videoManagers;
        // assigned when the active manager switches to the next one.
        private VideoManagerV2 prevManager;
        // main manager reference that everything operates off of.
        private VideoManagerV2 activeManager;
        // assigned when the user selects a manager to switch to.
        private VideoManagerV2 nextManager;

        // === Synchronized variables and their local counterparts ===

        [System.NonSerialized] [UdonSynced] public float syncTime = 0f;
        [System.NonSerialized] public float currentTime;
        // ownerState/localState values: 0 = stopped, 1 = playing, 2 = paused
        // ownerState is the value that is synced
        // localState is the sync tracking counterpart (used to detect state change from the owner)
        // currentState is the ACTUAL state that the local video player is in.
        // localState and currentState are separated to allow for local to not be forced into the owner's state completely
        // The primary reason for this deleniation is to allow for the local to pause without having to desync.
        // For eg: Someone isn't interested in most videos, but still wants to know what is playing, so they pause it and let it do the pausedThreshold resync (every 5 seconds)
        //      One could simply mute the video, yes, but some people might not want the distraction of an active video playing if they happen to be in front of a mirror
        //      where the TV is reflected. This allows a much more comfortable "keep track of" mode for those users.
        [UdonSynced] private int ownerState = STOPPED;
        private int localState = STOPPED;
        [System.NonSerialized] public int currentState = STOPPED;
        [UdonSynced] private VRCUrl syncUrl = VRCUrl.Empty;
        [System.NonSerialized] public VRCUrl localUrl = VRCUrl.Empty;
        [UdonSynced] private bool locked = false;
        [System.NonSerialized] public bool localLocked = false;
        // To counter the weirdness of udon sync data being cached in unusual ways, for any piece of synchronized data that changes except for syncTime which is continuous,
        //      update this revision counter. This is used in the OnDeserialization event to check if the data is ACTUALLY new or if it is from the cache.
        //      The cache is usually cleared after about 2-3 network frames, but still can cause odd video reloading behavior when we don't actually want it.
        //      If the revision is less than or equal to the locally stored revision value, the data is not new and has not been changed by the owner.
        //      This should be unecessary after the new networking update for udon by using manual sync.
        [UdonSynced] private int revision;
        private int localRevision;


        // === Fields for tracking internal state ===

        // This flag is to track whether or not the local player is able to operate independently of the owner
        // Setting to false gives the local player full control of their local player. 
        // Once they value is set to true, it will automatically resync with the owner, even if the video URL has changed since desyncing.
        [System.NonSerialized] public bool syncToOwner = true;
        [System.NonSerialized] public float startTime;
        [System.NonSerialized] public float endTime;
        [System.NonSerialized] public float videoDuration;
        // track if the player is in the middle of loading. 
        // This state occurs between the ChangeVideo call and when the active video player sends the OnVideoStart event.
        private bool loading = false;
        private bool loop = false;
        private bool mute = false;
        private bool audio3d = true;
        private float volume = 0.5f;

        // Time delay before allowing the TV to update it's active video
        // This value is always assigned as: Time.realtimeSinceStartup + someOffsetValue;
        // It is checked using this structure: if (Time.realtimeSinceStartup < waitUntil) { waitIsOver(); }
        private float waitUntil = 0f;
        // Time to seek to at time sync check
        // This value is set for a couple different reasons.
        // If the video player is switching locally to a different player, it will use Mathf.Epsilon to signal seemless seek time for the player being swapped to.
        // If the video URL contains a t=, startat=, starttime=, or start= params, it will assign that value so to start the video at that point once it's loaded.
        private float jumpToTime = 0f;
        // This flag simply enables the local player to be paused without forcing hard-sync to the owner's state.
        // This results in a pause that, when the owner pauses then plays, it won't foroce the local player to unpause unintentionally.
        // This flag cooperates with the pausedThreshold constant to enable resyncing every 5 seconds without actually having the video playing.
        private bool locallyPaused = false;
        // Cached value to track state change for video player swapping.
        private int lastVideoPlayer = -1;
        // Flag to check if an error occured. When true, it will prevent auto-reloading of the video.
        // Player will need to be forced to refresh by intentionally pressing (calling the method) Play.
        // The exception to this rule is when the error is of RateLimited type. 
        // This error will trigger a auto-reload after a 3 second delay to prevent excess requests from spamming the network causing more rate limiting triggers.
        private bool errorOccurred = false;

        private int lastOwnerId = 0;
        // 


        // === Event input variables (update these from external udon graphs. U# should use the parameterized method instead) ===
        // These fields represent storage for incoming data from other scripts. 
        // The format is as follows: the word IN, the event that the data is related to, and a meaningful name on what that variable is related to.
        // Eg: IN_ChangeVolume_Percent means the data is incoming, will be used by the event named 'ChangeVideo', and represents the TV's volume value as a percent (between 0.0 and 1.0).

        // parameter for ChangeVideo event
        [System.NonSerialized] public VRCUrl IN_ChangeMedia_VRCUrl_Url = VRCUrl.Empty;
        // parameter for ChangeVolume event
        [System.NonSerialized] public float IN_ChangeVolume_float_Percent = 0f;
        // parameter for ChangeSeekTime event
        [System.NonSerialized] public float IN_ChangeSeekTime_float_Percent = 0f;
        // paramter for ChangeVideoPlayer event
        [System.NonSerialized] public int IN_ChangeVideoPlayer_int_Index = 2;
        // parameter for RegisterUdonEventReceiver event
        [System.NonSerialized] public UdonBehaviour IN_RegisterUdonEventReceiver_UdonBehavior_Subscriber;


        // === Flags used to prevent infinite event loops ==
        // (like volume change -> _ChangeVolume -> OnVolumeChange -> volume change -> _ChangeVolume -> OnVolumeChange -> etc)
        private string[] haltedEvents = new string[23];

        // === Misc variables ===
        private bool refreshAfterWait = false;
        private bool sendEvents = false;
        private bool isLive = false;
        [System.NonSerialized] public bool init = false;
        private bool activeInitialized = false;
        private bool enforceSyncTime = true;
        private float syncTimeOut;
        private float syncEnforcementTimeLimit = 3f;
        private bool skipLog = false;

        private void initialize()
        {
            if (init) return;
            log($"Starting TVManagerV2 - {name}");

            foreach (VideoManagerV2 m in videoManagers)
            {
                if (m != null) m._SetTV(this);
            }

            // init interal URLS to empty instead of null
            IN_ChangeMedia_VRCUrl_Url = localUrl = syncUrl = VRCUrl.Empty;
            // assign inital video if owner
            if (isOwner()) IN_ChangeMedia_VRCUrl_Url = autoplayURL;
            // determine initial locked state
            if (lockedToMasterByDefault)
            {
                if (Networking.IsMaster) locked = true;
                localLocked = true;
                forwardEvent(EVENT_LOCK);
            }
            // make the script wait a few seconds before trying to fetch the video data for the first time.
            waitUntil = Time.realtimeSinceStartup + 3f + autoplayStartOffset;
            syncTimeOut = waitUntil + syncEnforcementTimeLimit;
            // load initial video player (sets the nextManager value)
            IN_ChangeVideoPlayer_int_Index = initialPlayer;
            _ChangeVideoPlayer();
            nextManager._ChangeVolume(initialVolume);
            forwardVariable(VAR_VOLUME, initialVolume);
            forwardEvent(EVENT_VOLUMECHANGE);
            forwardEvent(EVENT_READY);
            if (startHidden) gameObject.SetActive(false);
            init = true;
        }

        // === Subscription Methods ===

        void Start()
        {
            log("Start");
            initialize();
        }

        void OnEnable()
        {
            log("Enable");
            initialize();
            // if the TV was disabled before Update check ran, the TV was set off by default from some external method, like a Touch Control or likewise.
            // If that's the case, the startHidden flag is redundant so simply unset the flag.
            if (startHidden) startHidden = false;
            if (activeManager != null)
            {
                if (isOwner()) SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ALL_Play));
                else _Play();
            }
        }

        void OnDisable()
        {
            if (activeManager != null)
            {
                // In order to prevent a loop glitch due to owner not updating syncTime when the object is disabled
                // send a command as owner to everyone to pause the video. 
                // There are other solutions that might work, but this is the most elegant that could be found so far.
                if (isOwner()) SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ALL_Pause));
                else _Pause();
            }
        }

        void Update()
        {
            if (!init) return; // has not yet been initialized
            var time = Time.realtimeSinceStartup;
            // wait until the timeout has cleard
            if (time < waitUntil) return;
            if (activeManager != nextManager)
            {
                log($"Manager swap: Next {nextManager == null} -> Active {activeManager == null} -> Prev {prevManager == null}");
                prevManager = activeManager;
                activeManager = nextManager;
                activeInitialized = true;
                _RefreshMedia();
                return; // video has been refreshed, hold till next Update call
            }
            else if (refreshAfterWait)
            {
                log("Refresh video via update local");
                refreshAfterWait = false;
                _RefreshMedia();
                return;
            }
            // activeManager has not been fully init'd yet, skip current cycle.
            if (!activeInitialized) return;
            // when video player is switching (as denoted by the epsilon jump time), use the prevManager reference.
            var player = jumpToTime == EPSILON ? prevManager.player : activeManager.player;
            currentTime = player.GetTime();

            // To run sync logic, the following conditions must be met.
            // syncToOwner enabled, no blocking error occurred, video is not a livestream
            if (!syncToOwner || errorOccurred || isLive) return;

            // This handles updating the sync time from owner to others.
            // owner must be playing and local must not be stopped
            if (isOwner())
            {
                if (ownerState == PLAYING) syncTime = currentTime;
            }
            else if (!loading && currentState != STOPPED)
            {
                float syncDelta = Mathf.Abs(currentTime - syncTime);
                if (player.IsPlaying)
                {
                    // sync time enforcement check should ONLY be for when the video is playing
                    if (enforceSyncTime && syncDelta > playingThreshold)
                    {
                        log("Playing sync threshold exceeded. Updating");
                        currentTime = syncTime;
                        player.SetTime(currentTime);
                        if (time > syncTimeOut) enforceSyncTime = false;
                    }
                }
                // video sync enforcement will always occur for paused mode as the user expects the video to not be active, so we can skip forward as needed.
                else if (syncDelta > pausedThreshold)
                {
                    log("Paused sync threshold exceeded. Updating");
                    currentTime = syncTime;
                    player.SetTime(currentTime);
                }
            }
        }

        void LateUpdate()
        {
            if (!activeInitialized) return;
            // loop/media end check
            if (currentTime >= endTime)
            {
                var owner = isOwner();
                if (owner && loop)
                {
                    // owner when loop is active
                    activeManager.player.SetTime(startTime);
                    syncTime = currentTime = startTime;
                    forwardEvent(EVENT_MEDIALOOP);
                }
                else if (currentState == PLAYING && endTime > 0f)
                {
                    if (syncToOwner && syncTime < currentTime || !syncToOwner)
                    {
                        // non-owner when owner has loop (causing the sync time to start over)
                        activeManager.player.SetTime(startTime);
                        // update current time to start time so this only executes once, prevents accidental spam
                        currentTime = startTime;
                        enforceSyncTime = true;
                        // loop is cheap so make the sync timeout minimal
                        syncTimeOut = Time.realtimeSinceStartup + 0.1f;
                        forwardEvent(EVENT_MEDIALOOP);
                    }
                    else
                    {
                        // in any other condition, pause the video, specifying the media has finished
                        _Pause();
                        activeManager.player.SetTime(endTime);
                        currentTime = endTime;
                        forwardEvent(EVENT_MEDIAEND);
                    }
                }



                // if (loop && (isOwner() || !syncToOwner))
                // {
                //     activeManager.player.SetTime(startTime);
                //     // update current time to start time so this only executes once, anti-spam
                //     currentTime = startTime;
                //     if (syncToOwner) enforceSyncTime = true;
                //     forwardEvent(EVENT_MEDIALOOP);
                // }
                // else if (currentState == PLAYING && endTime > 0f)
                // {
                //     // extra check to ensure non-owners are able to properly loop when time sync drift occurs.
                //     if (syncToOwner && syncTime < currentTime)
                //     {
                //         activeManager.player.SetTime(startTime);
                //         // update current time to start time so this only executes once, anti-spam
                //         currentTime = startTime;
                //         enforceSyncTime = true;
                //         forwardEvent(EVENT_MEDIALOOP);
                //     }
                //     else
                //     {
                //         _Pause();
                //         activeManager.player.SetTime(endTime);
                //         currentTime = endTime;
                //         forwardEvent(EVENT_MEDIAEND);
                //     }
                // }
            }
        }

        new void OnDeserialization()
        {
            if (Time.realtimeSinceStartup < waitUntil) return;
            if (localRevision >= revision) return;
            else localRevision = revision;
            var id = Networking.GetOwner(gameObject).playerId;
            // TODO: Switch to OnOwherTransfer in UnU
            if (lastOwnerId != id)
            {
                log("Owner change via deserialization");
                lastOwnerId = id;
                forwardVariable(VAR_OWNER, id);
                forwardEvent(EVENT_OWNERCHANGE);
            }
            if (localUrl.Get() != syncUrl.Get())
            {
                log("URL change via deserialization");
                localUrl = syncUrl;
                IN_ChangeMedia_VRCUrl_Url = syncUrl;
                refreshAfterWait = true;
            }
            if (localState != ownerState)
            {
                log($"State change via deserialization {localState} -> {ownerState}");
                localState = ownerState;
                switch (ownerState)
                {
                    // always enforce stopping
                    case STOPPED: log("Stop via state change"); _Stop(); break;
                    // allow the local player to be paused if owner is playing
                    case PLAYING: if (!locallyPaused) play(); break;
                    // if owner pauses, unset the local pause flag
                    case PAUSED: pause(); locallyPaused = false; break;
                    default: break;
                }
            }
            if (localLocked != locked)
            {
                localLocked = locked;
                forwardEvent(localLocked ? EVENT_LOCK : EVENT_UNLOCK);
            }
        }


        // === VideoManager events ===

        // Once the active manager detects the player has finished loading, get video information and log
        public void _OnVideoPlayerStart()
        {
            if (!activeManager.isVisible) activeManager._Show();
            if (loading) // only execute this section when the player is refreshing the video
            {
                if (prevManager != activeManager)
                {
                    if (prevManager != null)
                    {
                        prevManager._ApplyStateTo(activeManager);
                        // this epsilon check is explicitly for when the video players switch, in order to make the timejump much more seamless for non-owners.
                        if (jumpToTime == Mathf.Epsilon)
                        {
                            log("Epsilon time jump set to previous manager");
                            jumpToTime = prevManager.player.GetTime();
                        }
                        log($"Hiding previous manager {prevManager.gameObject.name}");
                        prevManager._Hide();
                    }
                    // video manager wasn't changed, skip epsilon time jump
                    else if (jumpToTime == Mathf.Epsilon)
                    {
                        log("Epsilon time jump set to start time due to not switching video players.");
                        jumpToTime = startTime;
                    }
                    prevManager = activeManager;
                }
                log($"{name} [{activeManager.gameObject.name}] Now Playing: {syncUrl}");
                videoDuration = activeManager.player.GetDuration();
                enforceSyncTime = true;
                syncTimeOut = Time.realtimeSinceStartup + syncEnforcementTimeLimit;
                extractParams();
                isLoading(false);
                forwardEvent(EVENT_MEDIASTART);
            }
            if (!mute) activeManager._UnMute();
            if (jumpToTime < startTime)
            {
                log("jumpToTime preceeds startTime. Updating.");
                jumpToTime = startTime;
            }
            if (jumpToTime > 0f)
            {
                log($"Jumping {name} [{activeManager.gameObject.name}] to timestamp: {jumpToTime}");
                activeManager.player.SetTime(jumpToTime);
                jumpToTime = 0f;
            }
            videoDuration = activeManager.player.GetDuration();
            isLive = videoDuration == Mathf.Infinity;
            activeManager.player.Play();
            if (isOwner())
            {
                ownerState = PLAYING;
                revision++;
            }
            currentState = localState = PLAYING;
            errorOccurred = false;
            forwardEvent(EVENT_PLAY);
        }

        public void _OnVideoPlayerError(VideoError error)
        {
            err($"Video Error: {error}");
            log("Stop via video error"); _Stop();
            if (error == VideoError.RateLimited)
            {
                log("Refresh via rate limit error");
                errorOccurred = false; // skip error shortcircut and use a time check instead
                waitUntil = Time.realtimeSinceStartup + 3f;
                refreshAfterWait = true;
            }
            else errorOccurred = true;
            isLoading(false);
            forwardVariable(VAR_ERROR, error);
            forwardEvent(EVENT_VIDEOPLAYERERROR);
        }



        // === Public events to control the TV from user interfaces ===

        public void _RefreshMedia()
        {
            bool hasNewUrl = IN_ChangeMedia_VRCUrl_Url.Get() != VRCUrl.Empty.Get();
            bool hasSyncUrl = syncUrl.Get() != VRCUrl.Empty.Get();
            // if both url sources are empty, skip
            log($"Has Sync {hasSyncUrl} : {syncUrl} | Has New {hasNewUrl} : {IN_ChangeMedia_VRCUrl_Url}");
            if (!hasSyncUrl && !hasNewUrl) return;
            // change sync url if newUrl is specified and the input is not empty
            if (hasNewUrl)
            {
                syncUrl = IN_ChangeMedia_VRCUrl_Url;
                revision++;
                IN_ChangeMedia_VRCUrl_Url = VRCUrl.Empty;
                hasSyncUrl = true;
            }
            // only activate the video player if the syncURL isn't empty (which should always be true at this point, but just in case...)
            if (hasSyncUrl)
            {
                log($"{name} [{nextManager.gameObject.name}] loading URL: {syncUrl}");
                // if (!nextManager.isVisible) nextManager._Show();
                localUrl = syncUrl;
                nextManager.player.PlayURL(syncUrl);
                isLoading(true);
            }
            IN_ChangeMedia_VRCUrl_Url = VRCUrl.Empty;

        }
        public void _ChangeMedia()
        {
            log("Changing media");
            if (loading)
            {
                warn("Cannot change to another media while loading.");
                return;
            }
            if (localLocked && !Networking.LocalPlayer.isMaster)
            {
                warn("Video player is locked to master. Cannot change media for non-masters.");
                return;
            }
            // uses inputUrl
            if (takeOwnership())
            {
                _RefreshMedia();
                forwardEvent(EVENT_MEDIACHANGE);
            }
        }
        // equivalent to: udonBehavior.SetProgramVariable("IN_ChangeMedia_VRCUrl_Url", (VRCUrl) url); udonBehavior.SendCustomEvent("_ChangeMedia");
        public void _ChangeMediaTo(VRCUrl url)
        {
            log("Changing media with param");
            if (loading)
            {
                warn("Cannot change to another media while loading.");
                return;
            }
            if (localLocked && !Networking.LocalPlayer.isMaster)
            {
                warn("Video player is locked to master. Cannot change media for non-masters.");
                return;
            }
            IN_ChangeMedia_VRCUrl_Url = url;
            _ChangeMedia();
        }
        public void _QueueMedia(VRCUrl url)
        {
            log("Changing media with param");
            if (loading)
            {
                warn("Cannot change to another media while loading.");
                return;
            }
            if (localLocked && !Networking.LocalPlayer.isMaster)
            {
                warn("Video player is locked to master. Cannot change media for non-masters.");
                return;
            }
            refreshAfterWait = true;
            IN_ChangeMedia_VRCUrl_Url = url;
        }
        // equivalent to: udonBehavior.SetProgramVariable("IN_ChangeVideoPlayer_int_Index", (VRCUrl) url); udonBehavior.SendCustomEvent("_ChangeVideoPlayer");
        public void _ChangeVideoPlayer()
        {
            // no need to change if same is picked
            if (IN_ChangeVideoPlayer_int_Index == lastVideoPlayer) return;
            // do not allow changing resolution while a video is loading.
            if (loading)
            {
                IN_ChangeVideoPlayer_int_Index = lastVideoPlayer;
                return;
            }
            if (IN_ChangeVideoPlayer_int_Index >= videoManagers.Length)
            {
                err($"Video Player swap value too large: Expected value between 0 and {videoManagers.Length - 1} - Actual {IN_ChangeVideoPlayer_int_Index}");
                return;
            }
            // special condition for time jump between switching video players
            if (activeManager != null) jumpToTime = Mathf.Epsilon;
            nextManager = videoManagers[IN_ChangeVideoPlayer_int_Index];
            lastVideoPlayer = IN_ChangeVideoPlayer_int_Index;
            forwardVariable(VAR_VIDEOPLAYER, lastVideoPlayer);
            forwardEvent(EVENT_VIDEOPLAYERCHANGE);
            log($"Switching to: {gameObject.name} [{nextManager.gameObject.name}]");
            IN_ChangeVideoPlayer_int_Index = -1;
        }

        public void _ChangeVideoPlayerTo(int videoPlayer)
        {
            IN_ChangeVideoPlayer_int_Index = videoPlayer;
            _ChangeVideoPlayer();
        }

        public void ALL_Play() => _Play();
        public void _Play()
        {
            if (ownerState != STOPPED)
            {
                if (currentState == STOPPED)
                {
                    log("Refresh video via Play (owner playing/local stopped)");
                    _RefreshMedia();
                }
                else
                {
                    play();
                }
            }
        }
        private void play()
        {
            log("Normal Play");
            if (syncToOwner && ownerState == PAUSED && !isOwner()) return;
            BaseVRCVideoPlayer player;
            if (activeManager != null) player = activeManager.player;
            else player = nextManager.player;
            if (isOwner())
            {
                ownerState = localState = PLAYING;
                revision++;
            }
            // if video is at end and user forces play, force loop the video one time.
            log($"Current {currentTime} End {endTime}");
            if (currentTime + playingThreshold >= endTime)
            {
                log("Single loop");
                currentTime = startTime;
                player.SetTime(startTime);
                forwardEvent(EVENT_MEDIALOOP);
            }
            player.Play();
            currentState = PLAYING;
            if (syncToOwner) enforceSyncTime = true;
            forwardEvent(EVENT_PLAY);
        }

        public void ALL_Pause() => _Pause();
        public void _Pause()
        {
            if (ownerState != STOPPED)
            {
                pause();
            }
        }
        private void pause()
        {
            BaseVRCVideoPlayer player = activeManager != null ? activeManager.player : nextManager.player;
            player.Pause();
            if (isOwner())
            {
                ownerState = localState = PAUSED;
                revision++;
            }
            currentState = PAUSED;
            locallyPaused = true; // flag to determine if pause was locally triggered
            forwardEvent(EVENT_PAUSE);
        }

        public void _Stop()
        {
            log("Stopping, hiding active");
            if (loading)
            {
                // if stop is called while loading a video, the video loading will be halted instead of the active player
                nextManager._Hide();
                loading = false;
                errorOccurred = false;
                forwardEvent(EVENT_LOADINGSTOP);
                return;
            }
            activeManager._Hide();
            if (isOwner())
            {
                ownerState = localState = STOPPED;
                revision++;
                activeManager.player.Stop();
                activeManager.player.SetTime(0f);
            }
            currentState = STOPPED;
            isLoading(false);
            locallyPaused = false;
            errorOccurred = false;
            forwardEvent(EVENT_STOP);
        }

        public void _Mute()
        {
            mute = true;
            activeManager._Mute();
            forwardEvent(EVENT_MUTE);
        }
        public void _UnMute()
        {
            mute = false;
            activeManager._UnMute();
            forwardEvent(EVENT_UNMUTE);
        }
        public void _ToggleMute()
        {
            mute = !mute;
            activeManager._ChangeMute(mute);
            forwardEvent(mute ? EVENT_MUTE : EVENT_UNMUTE);
        }
        public void _ChangeMuteTo(bool mute)
        {
            this.mute = mute;
            activeManager._ChangeMute(mute);
            forwardEvent(mute ? EVENT_MUTE : EVENT_UNMUTE);
        }
        public void _ChangeVolume()
        {
            activeManager._ChangeVolume(IN_ChangeVolume_float_Percent);
            forwardVariable(VAR_VOLUME, IN_ChangeVolume_float_Percent);
            forwardEvent(EVENT_VOLUMECHANGE);
            IN_ChangeVolume_float_Percent = 0f;
        }
        // equivalent to: udonBehavior.SetProgramVariable("IN_ChangeVolume_float_Percent", (float) volumePercent); udonBehavior.SendCustomEvent("_ChangeVolume");
        public void _ChangeVolumeTo(float volume)
        {
            this.IN_ChangeVolume_float_Percent = volume;
            _ChangeVolume();
        }
        public void _AudioMode3d()
        {
            audio3d = true;
            activeManager._Use3dAudio();
            forwardEvent(EVENT_AUDIOMODE3D);
        }
        public void _AudioMode2d()
        {
            audio3d = false;
            activeManager._Use2dAudio();
            forwardEvent(EVENT_AUDIOMODE2D);
        }
        public void _ChangeAudioModeTo(bool audio3d)
        {
            this.audio3d = audio3d;
            activeManager._ChangeAudioMode(audio3d);
            forwardEvent(audio3d ? EVENT_AUDIOMODE3D : EVENT_AUDIOMODE2D);
        }
        public void _ToggleAudioMode()
        {
            _ChangeAudioModeTo(!audio3d);
        }
        public void _Loop()
        {
            if (syncToOwner && !isOwner()) return;
            loop = true;
            forwardEvent(EVENT_ENABLELOOP);
        }
        public void _NoLoop()
        {
            if (syncToOwner && !isOwner()) return;
            loop = false;
            forwardEvent(EVENT_DISABLELOOP);
        }
        public void _ChangeLoopTo(bool loop)
        {
            if (syncToOwner && !isOwner()) return;
            this.loop = loop;
            forwardEvent(loop ? EVENT_ENABLELOOP : EVENT_DISABLELOOP);
        }
        public void _ToggleLoop()
        {
            if (syncToOwner && !isOwner()) return;
            _ChangeLoopTo(!loop);
        }
        public void _Sync()
        {
            syncToOwner = true;
            loop = false;
            syncTimeOut = Mathf.Infinity;
            enforceSyncTime = true;
            forwardEvent(EVENT_SYNC);
        }
        public void _DeSync()
        {
            syncToOwner = false;
            syncTimeOut = 0f;
            enforceSyncTime = false;
            forwardEvent(EVENT_DESYNC);
        }
        public void _ChangeSyncTo(bool sync)
        {
            if (sync) _Sync();
            else _DeSync();
        }
        public void _ToggleSync()
        {
            _ChangeSyncTo(!syncToOwner);
        }

        public void _ReSync()
        {
            enforceSyncTime = true;
        }

        // equivalent to: udonBehavior.SetProgramVariable("IN_ChangeSeekTime_float_Percent", (float) seekPercent); udonBehavior.SendCustomEvent("_ChangeSeekTime");
        public void _ChangeSeekTime()
        {
            BaseVRCVideoPlayer player = activeManager != null ? activeManager.player : nextManager.player;
            float dur = player.GetDuration();
            // inifinty is livestreams, they cannot adjust seek time.
            if (dur == Mathf.Infinity) return;
            float time = dur * IN_ChangeSeekTime_float_Percent;
            player.SetTime(time);
            IN_ChangeSeekTime_float_Percent = 0f;
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ManualSeek));
        }
        public void _ChangeSeekTimeTo(float seekPercent)
        {
            this.IN_ChangeSeekTime_float_Percent = seekPercent;
            _ChangeSeekTime();
        }
        public void _SeekForward()
        {
            if (isLive) activeManager.player.SetTime(Mathf.Infinity);
            else activeManager.player.SetTime(activeManager.player.GetTime() + 10f);
        }
        public void _SeekBackward()
        {
            if (isLive) activeManager.player.SetTime(Mathf.Infinity);
            else activeManager.player.SetTime(activeManager.player.GetTime() - 10f);
        }

        public void _Lock()
        {
            if (allowMasterLockToggle && Networking.LocalPlayer.isMaster)
            {
                takeOwnership();
                localLocked = locked = true;
                revision++;
                forwardEvent(EVENT_LOCK);
            }
        }
        public void _UnLock()
        {
            if (allowMasterLockToggle && Networking.LocalPlayer.isMaster)
            {
                takeOwnership();
                localLocked = locked = false;
                revision++;
                forwardEvent(EVENT_UNLOCK);
            }
        }
        public void _ChangeLockTo(bool lockActive)
        {
            if (lockActive) _Lock();
            else _UnLock();
        }
        public void _ToggleLock()
        {
            if (localLocked) _UnLock();
            else _Lock();
        }

        // Use this method to subscribe to the TV's event forwarding.
        // Useful for attaching multiple control panels or behaviors for various side effects to happen.
        public void _RegisterUdonEventReceiver()
        {
            UdonBehaviour target = IN_RegisterUdonEventReceiver_UdonBehavior_Subscriber;
            sendEvents = true;
            if (eventTargets == null) eventTargets = new Component[1];
            int i;
            for (i = 0; i < eventTargets.Length; i++)
            {
                if (eventTargets[i] == null)
                {
                    eventTargets[i] = target;
                    return;
                }
            }
            log($"Expanding event register to {eventTargets.Length + 1}");
            var _targets = eventTargets;
            eventTargets = new Component[_targets.Length + 1];
            for (i = 0; i < _targets.Length; i++)
            {
                eventTargets[i] = _targets[i];
            }
            eventTargets[i] = target;
            IN_RegisterUdonEventReceiver_UdonBehavior_Subscriber = null;
        }

        public void _RegisterUdonSharpEventReceiver(UdonSharpBehaviour target)
        {
            IN_RegisterUdonEventReceiver_UdonBehavior_Subscriber = (UdonBehaviour)(Component)target;
            _RegisterUdonEventReceiver();
        }


        // === Helper methods ===

        public void ManualSeek()
        {
            syncTimeOut = Time.realtimeSinceStartup + 1f;
            enforceSyncTime = true;
        }

        private void extractParams()
        {
            // grab parameters
            float value;
            string param;
            // check for t or start params, only update jumpToTime if start or t succeeds
            // only parse if another jumpToTime value has not been set.
            if (jumpToTime == 0f)
            {
                param = getUrlParam(syncUrl, "t");
                if (float.TryParse(param, out value)) jumpToTime = value;
            }
            // check for start param
            param = getUrlParam(syncUrl, "start");
            if (float.TryParse(param, out value)) startTime = value;
            else startTime = 0f;
            // check for end param
            param = getUrlParam(syncUrl, "end");
            if (float.TryParse(param, out value)) endTime = value;
            else endTime = videoDuration;
            // check for loop param
            int toggle;
            param = getUrlParam(syncUrl, "loop");
            if (int.TryParse(param, out toggle)) _ChangeLoopTo(toggle != 0);
            log("Params set after video is ready");
        }

        private bool isOwner() => Networking.GetOwner(gameObject) == Networking.LocalPlayer;

        private bool takeOwnership()
        {
            if (Networking.IsOwner(gameObject)) return true;
            // TODO: implement authentication behavior callback logic here
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            return true;
        }

        private void forwardEvent(string eventName)
        {
            if (sendEvents && haltEvent(eventName))
            {
                log($"Forwarding event {eventName} to {eventTargets.Length} listeners");
                foreach (var target in eventTargets)
                    if (target != null)
                        ((UdonBehaviour)target).SendCustomEvent(eventName);
            }
            releaseEvent(eventName);
        }

        private void forwardVariable(string variableName, object value)
        {
            if (sendEvents && variableName != null)
            {
                log($"Forwarding variable {variableName} to {eventTargets.Length} listeners");
                foreach (var target in eventTargets)
                    if (target != null)
                        ((UdonBehaviour)target).SetProgramVariable(variableName, value);
            }
        }

        // These two methods are used to prevent recursive event propogation between the TV and subscribed behaviors.
        // Only allows for 1 depth of calling an event before releasing from it's own context.
        private bool haltEvent(string eventName)
        {
            int insert = -1;
            for (int i = 0; i < haltedEvents.Length; i++)
            {
                if (haltedEvents[i] == eventName) return false;
                if (insert == -1 && haltedEvents[i] == null) insert = i;
            }
            haltedEvents[insert] = eventName;
            return true;
        }

        private void releaseEvent(string eventName)
        {
            for (int i = 0; i < haltedEvents.Length; i++)
            {
                if (haltedEvents[i] == eventName) haltedEvents[i] = null;
            }
        }

        private void isLoading(bool yes)
        {
            loading = yes;
            if (loading) forwardEvent(EVENT_LOADING);
            else forwardEvent(EVENT_LOADINGEND);
        }

        private int getUrlParamAsInt(VRCUrl url, string name, int _default)
        {
            string param = getUrlParam(url, name);
            int value;
            return System.Int32.TryParse(param, out value) ? value : _default;
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
                if (p[0] == name) return p[1];
            }
            // if one can't be found, return an empty string
            return string.Empty;
        }


        private void log(string value)
        {
            if (!skipLog) Debug.Log($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#00ff00>TVManagerV2</color>] {value}");
        }
        private void warn(string value)
        {
            if (!skipLog) Debug.LogWarning($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#00ff00>TVManagerV2</color>] {value}");
        }
        private void err(string value)
        {
            if (!skipLog) Debug.LogError($"[<color=#1F84A9>A</color><color=#A3A3A3>T</color><color=#2861B4>A</color> | <color=#00ff00>TVManagerV2</color>] {value}");
        }
    }
}
