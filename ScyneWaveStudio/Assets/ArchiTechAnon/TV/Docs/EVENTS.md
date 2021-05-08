# TV Events

## List of Available Incoming Events
The following is the list of events that any behavior may call to modify the TV's state (they ALL start with an underscore `_`):
- _RefreshMedia
    - The main workhorse event that handles loading whatever video is needed to be loaded (either video swap or refreshing the current one)
    - This event DOES NOT take ownership of the TV.
- _ChangeMedia
    - Wrapper event of _RefreshVideo that specifically expects a new video URL to be provided
    - Variable used for input data: `IN_ChangeVideo_VRCUrl_Url`
    - This event DOES attempt to take ownership of the TV. Whether this event does anything is dependant on the master lock settings.
- _ChangeMediaTo(VRCUrl)
    - UdonSharp only equivalent of the combination of `IN_ChangeVideo_VRCUrl_Url` and `_ChangeVideo`
- _ChangeVideoPlayer
    - This event handles the swapping of the video player configurations defined by the TV.
    - Variable used for input data: `IN_ChangeVideoPlayer_int_Index`
    - The variable is a 0-index array value (first entry is the value `0`) representing a particular item in the TV's Video Managers list.
- _ChangeVideoPlayerTo(int)
    - UdonSharp only equivalent of the combination of `IN_ChangeVideoPlayer_int_Index` and `_ChangeVideoPlayer`
- _Play
    - This event causes the current media to either start or resume, depending on the TV's current state.
- _Pause
    - This event causes the current media to suspend if there is one playing.
- _Stop
    - This event halts any currently playing media.
- _Mute
    - This event disables any audio output from the TV.
- _UnMute
    - This event enables any audio output from the TV.
- _ToggleMute
    - This event swaps the state of the audio outputs from the TV.
- _ChangeMuteTo(bool)
    - UdonSharp only method that updates the state of the audio outputs from the TV, based on the provided bool parameter.
- _ChangeVolume
    - This event handles updating the volume of the TV.
    - Variable used for input data: `IN_ChangeVolume_float_Percent`
    - The variable is a float that expects a value between 0.0f and 1.0f (representing a percent of the max volume)
- _ChangeVolumeTo(float)
    - UdonSharp only equivalent of the combination of `IN_ChangeVolume_float_Percent` and `_ChangeVolume`
- _AudioMode3d
    - This event switches any audio outputs from a stereo mode, to a positional audio mode.
- _AudioMode2d
    - This event switches any audio outputs from a positional audio mode, to a stereo mode.
- _ToggleAudioMode
    - This event swaps the audio output mode between positional and stereo, based on it's current value.
- _ChangeAudioModeTo(bool)
    - UdonSharp only method that updates the mode of the audio outputs, based on the provided bool parameter.
    - If the bool is true, the audio is positional (3d). If false, the audio is stereo (2d).
- _Loop
    - This event tells the TV to enable media looping
    - Looping only matters for the TV owner OR if a non-owner has manually de-synced.
- _NoLoop
    - This event tells the TV to disable media looping
- _ToggleLoop
    - This event swaps the looping state to the opposite of its current value.
- _ChangeLoopTo(bool)
    - UdonSharp only method that updates the looping state based on the provided bool parameter.
- _Sync
    - This event forces the TV to conform to any sychnronized data changes.
    - When in this state, the TV will always attempt to adjust itself so that it stays in sync with the current TV owner.
    - If unintentional de-sync of any kind happens, the TV should eventually self-correct to match whatever sync state the owner has, so long as the user is receiving data from the owner.
- _DeSync
    - This event forces the TV to ignore any synchronized data changes
    - This only applies to non-owners. It allows viewers to watch the video at their own pace. For example if someone has to step away for a moment, they could de-sync and then pause the media. Then when they come back, they can choose to re-sync with the owner, or continue watching from where the left off.
- _ToggleSync
    - This event swaps the sync flag to the opposite of its current state.
- _ChangeSyncTo(bool)
    - UdonSharp only method that updates the sync flag based on the provided bool parameter.
- _ChangeSeekTime
    - This event updates the current time of the actively playing media.
    - Commonly updated via some sort of slider.
    - Variable used for input data: `IN_ChangeSeekTime_float_Percent`
    - The variable is a float that expects a value between 0.0f and 1.0f (representing a percent of the total video duration)
- _ChangeSeekTimeTo(float)
    - UdonSharp only equivalent of the combination of `IN_ChangeSeekTime_float_Percent` and `_ChangeSeekTime`
- _SeekForward
    - This event implicitly fastforwards the seek time by 10 seconds.
- _SeekBackward
    - This event implicitly rewinds the seek time by 10 seconds.
- _Lock
    - This event tells the TV to only allow the instance master to manage the sync data (video swapping/synced seeking)
    - This event implicitly assigns the instance master as the TV owner.
- _UnLock
    - This event tells the TV to allow anyone to manage the sync data (video swapping/synced seeking)
- _ToggleLock
    - This event swaps the locked state of the TV to the opposite of its current state.
    - This event will ignore any calls to it that aren't from the instance master.
- _ChangeLockTo(bool)
    - UdonSharp only method that updates the locked state based on the bool parameter.
    - This method will ignore any calls to it that aren't from the instance master.
- _RegisterUdonEventReceiver
    - This event assigns a given behavior as a subscriber of the TV.
    - Variable used for input data: `IN_RegisterUdonEventReceiver_UdonBehavior_Subscriber`
    - This event should only be called during the `Start` event phase of a behavior and should simply assign the behavior's own reference to the variable.
- _RegisterUdonSharpEventReceiver(UdonSharpBehaviour)
    - UdonSharp only equivalent of the combination of `IN_RegisterUdonEventReceiver_UdonBehavior_Subscriber` and `_RegisterUdonEventReceiver`
    - This is generally done like this: `tv._RegisterUdonSharpEventReceiver(this);`
    - The method itself will convert it to a standard UdonBehavior, so don't worry about that.

---

## List of Available Outgoing Events
The following is the list of events that any subscribed behavior may receive:
- _OnReady 
    - Occurs when the TV has completed all initialization and is ready to be controlled
- _OnPlay 
    - Occurs when a video has started or resumed playing
- _OnPause 
    - Occurs when a video has been paused locally
- _OnStop 
    - Occurs when a video has been stopped locally
- _OnMediaStart 
    - Occurs immediately after a video has been loaded 
    - Can be the same URL, in which case it means the user simply refreshed the video
- _OnMediaEnd 
    - Occurs immediately after a video has finished playing 
    - Can be used to trigger a new video, does NOT occur if video is looped
- _OnMediaLoop 
    - Occurs when a video starts over after finishing 
    - Triggered only if video is looped OR if video is at the end and the owner pressed the play button for a one-time loop
- _OnMediaChange
    - Occurs when a user has claimed ownership of the TV and declared a new video to play
    - This event happens before the video is actually loaded. To take action when a video is ready after it has been loaded, use the OnVideoPlayerStart event.
- _OnOwnerChange
    - Occurs when a different player takes control of the TV.
- _OnVideoPlayerChange 
    - Occurs when the TV has swapped the video player configuration to a different one
    - This event will attempt to set the variable `OUT_OnVideoPlayerChange_int_Index` with the current index value of the video player configuration that has been swapped to.
- _OnVideoPlayerError 
    - Occurs when the video failed to resolve and play for some reason
    - This event will attempt to set the variable `OUT_OnVideoPlayerError_VideoError_Error` with the VideoError value that caused the event to trigger.
- _OnMute
    - Occurs when the local user mutes the current video
- _OnUnMute
    - Occurs when the local user unmutes the current video
- _OnVolumeChange
    - Occurs when the local user updates the volume percent value
    - This event will attempt to set the variable `OUT_OnVolumeChange_float_Percent` with the updated volume percent. This event might be called many times in a short period, especially if it is affected by a slider element modifying the TV's volume. 
- _OnAudioMode3d
    - Occurs when the local user switches from positional to stereo audio
- _OnAudioMode2d
    - Occurs when the local user switches from stereo to positional audio
- _OnEnableLoop
    - Occurs when the local user enables looping for the current video.
- _OnDisableLoop
    - Occurs when the local user disables looping for the current video.
- _OnSync
    - Occurs when the local user enables video synchronization (disabled for owner as one cannot desync with oneself)
- _OnDeSync
    - Occurs when the local user disables video synchronization (disabled for owner as one cannot desync with oneself)
- _OnLock
    - Occurs when an authorized user (usually instance master) locks the TV for authorized use only.
- _OnUnLock
    - Occurs when an authorized user (usually instance master) unlocks the TV for anyone to use.
- _OnLoading
    - Occurs when the TV's loading state is enabled 
    - It can happen at various points, so this event is mostly used for UIs to reflect the loading state of the TV.
- _OnLoadingEnd
    - Occurs when the TV's loading state is disabled 
    - It can happen at various points, so this event is mostly used for UIs to reflect the loading state of the TV.
- _OnLoadingStop
    - Occurs when the TV's loading state is interrupted
    - This is caused when the _Stop event is triggered while a video is loading.
