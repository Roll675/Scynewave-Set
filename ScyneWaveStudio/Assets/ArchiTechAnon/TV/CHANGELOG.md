# ArchiTechAnon TV Asset

## 2.0 Beta 5 (Current Version)
- Modify how time sync works. It now only enforces sync time from owner for the first few seconds, and then any time a state change of the TV affects the current sync time. Basically, the enforcement is a bit more lax to help support Quest playback better.
- Update the UIs to make use of the modified sync time activity. Sync button is now an actual "Resync" action, that will do a one-time activation of the sync time enforcement which will jump the video to the current time sync from the owner.

NOTE: If you unpacked the TV prefabs, I recommend deleting the control UIs (Basic/Slim/SlimReduced) and dropping in a new copy of that UI. The Playlist should not need updated for this beta release.