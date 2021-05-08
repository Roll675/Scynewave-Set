using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CyanTrigger
{
    public enum CyanTriggerSyncMode
    {
        NotSynced = 0,
        Synced = 1,
        SyncedLinear = 2,
        SyncedSmooth = 3,
    }
    
    public enum CyanTriggerUserGate
    {
        Anyone = 0,
        Owner = 1,
        Master = 2,
        UserAllowList = 3,
        UserDenyList = 4,
    }

    public enum CyanTriggerBroadcast
    {
        Local = 0,
        Owner = 1,
        All = 2,
        
        // TODO research buffering using the networking patch.
        // AllBufferOne,
        // AllBuffered
    }
}
