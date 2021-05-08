# ArchiTechAnon TV Asset
## Requirements
- Ensure latest VRCSDK3 (Udon) is imported (last tested with v2021.1.4)
- Ensure latest UdonSharp version is imported (last tested with [v0.19.8](https://github.com/MerlinVR/UdonSharp/releases/download/v0.19.8/UdonSharp_v0.19.8.unitypackage))
- Import this package
- Done.

## Basic Usage
- Drag a TV prefab (located at `Assets->ArchiTechAnon->TV->Prefabs->OffTheShelfTVs`) into your scene wherever you like, rotate in-scene and customize as needed.
OR 
- Drag the base prefab for the TV (located at `Assets->ArchiTechAnon->TV->TV2.0 Base`), unpack and customize as desired, adding whatever UIs or external modules you want.

You can find more about the ready-made OffTheShelfTVs in the [`Off-The-Shelf Document`](./Docs/OFFTHESHELF.md).

## Features
- Full media synchronization (play/pause/stop/seek/loop)
- Resiliant sync correction
- Automatic ownership management
- Local De-Sync (eg: for players who want to watch a video at their own pace)
- 3D/2D audio toggle
- Near frame-perfect video looping (audio looping isn't always frame-perfect, depends on the video's codec)
- Video autoplay URL support.
- Video autoplay delay offsets which help prevent ratelimit issues with multiple TVs.
- Video url params support (t/start/end/loop)
- Video player swap management for multiple video player configurations (eg:  you could have two for PC, HQ and LQ, and one for Quest)
- Pub/Sub event system for modular extension
- Instance master locking support (optional)

## Core Architecture
In addition to the standard proxy controls for video players (play/pause/stop/volume/seek/etc), the two main unique driving factors that the core architecture accomplishes is event driven modularity as well as the multi-configuration management/swap mechanism.

TV 2.0 has been re-architected to be more modular and extensible. This is done through a pseudo pub/sub system. In essence, a behavior will pass its own reference to the TV (supports both ugraph and usharp) and then will receive custom events (see the [`Events Document`](./Docs/EVENTS.md)) based on the TV's activity and state. The types of events directly reflect the various supported core features of the TV, such as the standard video and audio controls, as well as the video player swap mechanism for managing multiple configurations.

More details about the core architecture can be found in the [`Architecture Document`](./Docs/ARCHITECTURE.md).  
Details for ready-made extension modules for the TV can be found in the [`Modules Document`](./Docs/MODULES.md).  

## Core Settings
- *Autoplay URL*  
Pretty straight forward. This field is a place to put a video that you wish to automatically start upon first load into the world. This only is true for the instance owner (aka master) upon first visit of a new instance (which is then synced to any late-joiners). Leave empty to not have any autoplay.

- *Autoplay Start Offset*  
With the Autoplay Start Offset, which is for when there are more than 1 TV in the world, you can tell the TV to wait for X + 3 seconds before loading the sync'd (or autoplay'd) URL after joining the instance.  
The 3 seconds is required as a buffer for the world to load in, and the field's value is added to that. It's recommended to offset each TV in the world by a 4 or 5 seconds, any less than that has risk of triggering the rate limiting, especially for those that have slower internet.   
If you only have 1 TV in the world, you don't need to worry about this.

- *Initial Volume/Initial Player*  
Again, pretty straight forward. The initial volume determines at what volume the TV will start at upon joining the world.  
Like-wise, the initial player determines which video player configuration to load upon joining the world. The value is a number that represents the index of the Video Players array.

- *Playing/Paused Sync Thresholds*  
The threshold values are used to determine how much of an offset is allowed from the sync time (video player becoming out-of-sync from owner) before forcing non-owners to jump to the current timestamp that the owner is at.  
The paused threshold should be treated like a live slideshow of what is currently playing. This is intended to allow people to see what is visible on the TV without actually having the media actively running.  
The playing threshold is used to keep owner and non-owners closely in sync. One should be careful not to shrink this value too much. Making the value too small will cause excessive jittering and jumping in the video for non-owners. Through various testing the default value of this (0.65 seconds) is a solid middle ground between too far out of sync for a shared experience and so close as to cause intolerable sync jittering.  
The playing threshold value reasonably should never be less than 0.5 seconds. You can increase the value if some users are experiencing too much jitter (such as those with potato internet).

- *Hidden by Default*  
This toggle makes it so the TV disables itself (via SetActive) after it has completed all initialization.  
**IMPORTANT**: DO NOT DISABLE THE TV VIA INSPECTOR OR SEPARATE UDON BEHAVIOR UPON JOINING A WORLD. This will cause issues with the video players. Only use this setting to have the TV be hidden when people join the world. After that any enable/disabling by the user or other scripts is fine. Just not on world join.

- *Master Lock Settings*  
`Allow Master Lock Toggle` is a setting that specifies whether or not the instance master is allowed to lock down the TV to master use only. This will prevent all other users from being able to tell the TV to play something else.  
`Locked To Master By Default` is a setting that specifies whether or not the TV is master locked from the start. This only affects when the first user joins the instance as master. The locked state is synced for non-owners/late-joiners after that point.


## Caveats
- General reminder: Not all websites are supported, especially those that implement custom video players. Sometimes the player is able to resolve those to a raw video url. Feel free to see what works (most should).
- Due to certain limitations with AVPro, if you play a video that has an aspect ratio NOT of 16:9, the video will look stretched. A work around for this issue is on the todo list.
- Due to a temporary limitation in Udon, I cannot completely remove the directionality of the default speakers when switching from 3D audio to 2D audio. Once that limitation is lifted, I will fix that.
