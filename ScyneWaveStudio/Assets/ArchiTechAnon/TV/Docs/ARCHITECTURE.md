# Core Architecture Details

The way that the architecture is setup is based on the pub/sub concept, but implemented using custom udon events. These events allow for the modular extension of the TV without having to get into the nitty-gritty of the TV itself.

The other main piece of the architecture is how the video players (VPs) are setup. The main TV prefab that is available in this asset comes with 5 VP configurations: AVPro player with varying resolution caps (480p/720p/1080p/4k/8k). In tne example scene within this asset, the TV has been augmented with an additional a AVPro config with 3 screens as well as an implementation of the Unity video player.

## The Udon

There are two scripts that compose the core of the TV: `TVManagerV2` and `VideoManagerV2` (the 'V2' is to separate it from the original version so as to avoid import conflicts)  
The `TVManagerV2` script is the main workhorse. This is the behavior that manages the TV's state, triggers events for external behaviors, and proxies actions to the respective video manager scripts.  
Conversly, the `VideoManagerV2` script is a lightweight proxy that captures events from the backing video player system (AVPro/Unity) and forwards that back to the tv manager script. It also manages the internal state of any speakers (audio sources) or screens (renderers) that are associated with it.

To make a TV from scratch is not difficult. Just make sure to add any desired `VideoManagerV2` scripts to the Video Managers list on the `TVManagerV2`. The same goes for any screens or speakers associated with a video player that has a `VideoManagerV2` script associated; make sure to fill out the Speakers and Screens list in the inspector with any of the respective audio sources or renderers that relate to that video player.

## Dynamically Controlling the TV

_There are more details in the [`Events Document`](./EVENTS.md), but will briefly touch on a particular bit of architecture related to it._  

With the new pub/sub style event system, there is a bit of a unique implementation for interacting with it. This was made in such a way to easily support Udon not written in U# (like uGraph).  
There are a few events that have to take some kind of input or output data to act upon. For example, trying to change the TV's active video from custom script. This is accomplished through some predefined variables. These variables ALWYAS come in the form of:  
(`IN` or `OUT`) + `_` + correspondingEventName + `_` + variableType + `_` + meaningfulTerm

Coninuing the example, To change the active URL from a udon graph script, one would need to do the following in order:
- Have a VRCUrl reference/instance ready (eg: `myVRCUrlInstance`)
- Use the node and inputs UdonBehavior -> SetProgramVariable [ `tvReference`, "IN_ChangeVideo_VRCUrl_Url", `myVRCUrlInstance` ]
- Use the node and inputs UdonBehavior -> SendCustomEvent [ `tvReference`, "_ChangeVideo" ]

And that's it. The specifics of this are that there is a handful of events that need some input data.  
The input data variables need to be set before calling the corresponding event. In the case of the url change, it's the `IN_ChangeVideo_VRCUrl_Url`.

Conversely, there are some events that the TV produces that intends to deliver some data to the subscribed behaviors. For example, if the TV's volume is modified at any point, it will attempt to do the following for each of the subscribed behaviors:
- UdonBehavior -> SetProgramVariable [ `tvReference`, "OUT_OnVolumeChange_float_Percent", `percentValue` ]
- UdonBehavior -> SendCustomEvent [ `tvReference`, "OnVolumeChange" ]

To receive the volume data, you would need a variable on your subscribed behavior with the name `OUT_OnVolumeChange_float_Percent` as well as a custom event (public void method for U#) that has the name `OnVolumeChange`. You can then use the variable to do whatever you need to do.

There are only two pieces of data that do not follow this nomenclature. That is the `currentTime` and `localUrl` variables on the TV. Since these two variables are connected with the main pieces of synced data, it is better to get their values as needed, rather than rely on the event system to pass the data, especially for `currentTime` as that's updated almost every frame. Simply do `UdonBehavior -> GetProgramVariable [ tvReference, "currentTime" ]` or the `localUrl` equivalent.

You can find the remainder of the details listed, with examples, in the Events document.

## One Final Thing

In order for any custom behavior to be able to receive events from the TV, they must be registered with the TV. This should be done during the `Start` event phase of the behavior.

For UdonGraph scripts, this is done with the following:
- Create variable of type UdonBehavior and assign the TVManagerV2 instance to it.
    Let's call it `tvReference`.
- Create another variable of type UdonBehavior and DO NOT ASSIGN anything to it in the inspector. 
    This makes it automatically fill with the custom behavior's own reference, which is exactly what we need. Let's call it `selfRef`.
- Drag the `selfRef` variable into the graph to produce a GET node.
- Create node with parameters: UdonBehavior -> SetProgramVariable [ `tvReference`, "IN_RegisterUdonEventReceiver_UdonBehavior_Subscriber", `selfRef` ]
- Create node with parameters: UdonBehavior -> SendCustomEvent [ `tvReference`, "_RegisterUdonEventReceiver" ]
- Create node: Events -> Event Start
- Connect the Start event to the SetProgramVariable node and then connect SetProgramVariable to the SendCustomEvent node.
- Done. Here's what that looks like in the graph:  
![Udon Graph showing how to register a behavior with the TV](./Images/UGraph-TVSubscriberRegister.png)

For UdonSharp scripts, do the following:
- Create a TVManagerV2 field in your custom behavior class, let's call it `tv`.
- Inside the Start method, add the line `tv._RegisterUdonSharpEventReceiver(this);`
- Done