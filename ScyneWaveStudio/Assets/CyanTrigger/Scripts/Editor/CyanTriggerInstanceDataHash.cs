using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CyanTrigger
{
    public static class CyanTriggerInstanceDataHash
    {
/*
Program unique string format:

Version: <version>
Apply animators: <true/false>
variables:
<User Variables>
events:
<Events>



User Variable
<name>, <type>, <synced>, <hasCallback>

Event
Event name: <name>
<EventOption>
<Event Action>
Actions:
<actions>
EndActions
EndEvent

EventOption
<broadcast>, <userGate>, <delay>
UserList
<user list inputs>

Action
<Direct: <name>/CustomAction: <guid>>
MultiInputs
<inputs>
Inputs
<inputs>

Input
<type> (const/var "<name>"/var[<id>] <hasCallback>)
*/

        
        // TODO change to odin version
        // This is basically a string encoding of a cyan trigger that does not depend on any variable data.
        public static string GetProgramUniqueStringForCyanTrigger(CyanTriggerDataInstance instanceData)
        {
            StringBuilder triggerInfo = new StringBuilder();

            HashSet<string> variablesWithCallbacks =
                CyanTriggerCustomNodeOnVariableChanged.GetVariablesWithOnChangedCallback(instanceData.events);
            Dictionary<string, int> variableMap = new Dictionary<string, int>();
            int varCount = 0;
            
            // TODO automate this...
            variableMap.Add(CyanTriggerAssemblyData.ThisGameObjectGUID, varCount++);
            variableMap.Add(CyanTriggerAssemblyData.ThisTransformGUID, varCount++);
            variableMap.Add(CyanTriggerAssemblyData.ThisUdonBehaviourGUID, varCount++);
            variableMap.Add(CyanTriggerAssemblyData.ThisCyanTriggerGUID, varCount++);
            variableMap.Add(CyanTriggerAssemblyData.LocalPlayerGUID, varCount++);

            triggerInfo.AppendLine("Version: " + instanceData.version);
            triggerInfo.AppendLine("Apply animators: " + instanceData.applyAnimatorMove);
            // Variables
            {
                triggerInfo.AppendLine("variables:");

                List<CyanTriggerVariable> variables = new List<CyanTriggerVariable>(instanceData.variables);
                
                // Sort by name first to ensure that variable id is set properly to the correct index per variable.
                variables.Sort((var1, var2) => 
                    String.Compare(var1.name, var2.name, StringComparison.Ordinal));
                
                foreach (var variable in variables)
                {
                    bool hasCallback = variablesWithCallbacks.Contains(variable.variableID);
                    
                    // The name is defined in the code and is needed in the hash.
                    triggerInfo.AppendLine(variable.name +", " + variable.type.type +", " +variable.sync +", " + hasCallback);
                    variableMap.Add(variable.variableID, varCount++);
                }
            }
            // Events
            {
                triggerInfo.AppendLine("events:");

                List<CyanTriggerEvent> events = new List<CyanTriggerEvent>(instanceData.events);
                // TODO support sorting in compilation time
                // TODO make better sorting?
                // events.Sort((e1, e2) =>
                // {
                //     string e1t = string.IsNullOrEmpty(e1.eventInstance.actionType.guid)
                //         ? e1.eventInstance.actionType.directEvent
                //         : e1.eventInstance.actionType.guid;
                //     
                //     string e2t = string.IsNullOrEmpty(e2.eventInstance.actionType.guid)
                //         ? e2.eventInstance.actionType.directEvent
                //         : e2.eventInstance.actionType.guid;
                //
                //     int ret = String.Compare(e1t, e2t, StringComparison.Ordinal);
                //     if (ret == 0)
                //     {
                //         ret = String.Compare(e1.name, e2.name, StringComparison.Ordinal);
                //     }
                //
                //     return ret;
                // });
                
                for (int cur = 0; cur < events.Count; ++cur)
                {
                    triggerInfo.Append(
                        GetProgramUniqueStringForEvent(events[cur], variablesWithCallbacks, variableMap, ref varCount));
                }
            }

            return triggerInfo.ToString();
        }

        private static string GetProgramUniqueStringForEvent(
            CyanTriggerEvent triggerEvent,
            HashSet<string> variablesWithCallbacks,
            Dictionary<string, int> variableMap,
            ref int varCount)
        {
            StringBuilder eventString = new StringBuilder();

            eventString.AppendLine("Event name: " +triggerEvent.name);
            eventString.Append(GetProgramUniqueStringForEventOption(triggerEvent.eventOptions, variablesWithCallbacks, variableMap));
            eventString.Append(GetProgramUniqueStringForAction(triggerEvent.eventInstance, variablesWithCallbacks, variableMap, ref varCount));
            eventString.AppendLine("Actions:");
            for (int cur = 0; cur < triggerEvent.actionInstances.Length; ++cur)
            {
                eventString.Append(GetProgramUniqueStringForAction(triggerEvent.actionInstances[cur], variablesWithCallbacks, variableMap, ref varCount));
            }
            
            eventString.AppendLine("EndActions");
            eventString.AppendLine("EndEvent");
            return eventString.ToString();
        }

        private static string GetProgramUniqueStringForEventOption(
            CyanTriggerEventOptions eventOptions,
            HashSet<string> variablesWithCallbacks,
            Dictionary<string, int> variableMap)
        {
            StringBuilder eventString = new StringBuilder();
            eventString.AppendLine(eventOptions.broadcast + ", " + eventOptions.userGate + ", " + eventOptions.delay);
            
            if (eventOptions.userGate == CyanTriggerUserGate.UserAllowList || 
                eventOptions.userGate == CyanTriggerUserGate.UserDenyList)
            {
                eventString.AppendLine("UserList");
                // TODO sort user gate? 
                foreach (var userGate in eventOptions.userGateExtraData)
                {
                    eventString.AppendLine(GetProgramUniqueStringForVariable(
                        userGate, 
                        CyanTriggerSerializableInstanceEditor.AllowedUserGateVariableDefinition,
                        variablesWithCallbacks, 
                        variableMap,
                        false));
                }
            }

            return eventString.ToString();
        }
        
        private static string GetProgramUniqueStringForAction(
            CyanTriggerActionInstance actionInstance,
            HashSet<string> variablesWithCallbacks,
            Dictionary<string, int> variableMap,
            ref int varCount)
        {
            var infoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(
                actionInstance.actionType.guid, actionInstance.actionType.directEvent);
            
            var varDefs = infoHolder.GetVariables();
            bool allowsMulti = varDefs.Length > 0 &&
                               (varDefs[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0;
            
            StringBuilder actionString = new StringBuilder();
            if (!string.IsNullOrEmpty(actionInstance.actionType.directEvent))
            {
                actionString.AppendLine("Direct: " + actionInstance.actionType.directEvent);
                
                if (CyanTriggerNodeDefinitionManager.TryGetCustomDefinition(actionInstance.actionType.directEvent,
                    out var customDefinition)) 
                {
                    // variable providers
                    if (customDefinition is CyanTriggerCustomNodeVariableProvider variableProvider)
                    {
                        var userVariables = variableProvider.GetCustomEditorVariableOptions(null, actionInstance.inputs);
                        foreach (var var in userVariables)
                        {
                            if (variableMap.ContainsKey(var.ID))
                            {
                                Debug.Log("Map already contains id: "+var.ID);
                                variableMap.Remove(var.ID);
                            }
                            variableMap.Add(var.ID, varCount++);
                        }
                    }

                    // TODO Find a better solution here since this is hacky...
                    // Search through the list of cyan trigger variables and see if any target the local behaviour.
                    // If so, append the custom name so that this differentiates the program from other customs.
                    if (customDefinition is CyanTriggerCustomNodeSendCustomEvent)
                    {
                        bool local = false;
                        foreach (var var in actionInstance.multiInput)
                        {
                            if (var.isVariable && var.variableID == CyanTriggerAssemblyData.ThisCyanTriggerGUID)
                            {
                                local = true;
                                break;
                            }
                        }

                        if (local)
                        {
                            actionString.AppendLine("CustomNamed: " + actionInstance.inputs[1].data.obj);
                        }
                    }
                    
                    if (customDefinition is CyanTriggerCustomNodeSetComponentActive)
                    {
                        actionString.AppendLine("Component Type: " + actionInstance.inputs[1].data.obj);
                    }
                }
            }
            else
            {
                // custom node
                actionString.AppendLine("CustomAction: " + actionInstance.actionType.guid);
            }

            
            
            if (!allowsMulti && varDefs.Length > 0)
            {
                actionString.AppendLine("Inputs");
            }
            
            for (int cur = 0; cur < varDefs.Length; ++cur)
            {
                if (cur == 0 && allowsMulti)
                {
                    actionString.AppendLine("MultiInputs");
                    foreach (var var in actionInstance.multiInput)
                    {
                        actionString.AppendLine(GetProgramUniqueStringForVariable(
                            var,
                            varDefs[cur],
                            variablesWithCallbacks, 
                            variableMap));
                    }
                    continue;
                }

                if (cur == 1 && allowsMulti)
                {
                    actionString.AppendLine("Inputs");
                }

                actionString.AppendLine(GetProgramUniqueStringForVariable(
                    actionInstance.inputs[cur], 
                    varDefs[cur],
                    variablesWithCallbacks, 
                    variableMap));
            }
            
            return actionString.ToString();
        }
        
        private static string GetProgramUniqueStringForVariable(
            CyanTriggerActionVariableInstance variable,
            CyanTriggerActionVariableDefinition def,
            HashSet<string> variablesWithCallbacks,
            Dictionary<string, int> variableMap,
            bool reference = true)
        {
            if (variable.isVariable)
            {
                if (string.IsNullOrEmpty(variable.variableID))
                {
                    return $"{def.type.type} var \"{variable.name}\"";
                }
                
                bool hasCallback = variablesWithCallbacks.Contains(variable.variableID);
                if (!variableMap.TryGetValue(variable.variableID, out int id))
                {
                    throw new Exception("Variable id could not be found: " + variable.variableID);
                }
                return $"{def.type.type} var[{id}] {hasCallback}";
            }

            string data = "";
            if (!reference)
            {
                data = " " + variable.data.obj;
            }
            
            return $"{def.type.type} const" + data;
        }



        public static string HashCyanTriggerInstanceData(CyanTriggerDataInstance instanceData)
        {
            var programString = GetProgramUniqueStringForCyanTrigger(instanceData);
            var bytes = Encoding.ASCII.GetBytes(programString);
            MD5 md5 = new MD5CryptoServiceProvider();
            try
            {
                byte[] result = md5.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < result.Length; i++)
                {
                    sb.Append(result[i].ToString("X2"));
                }

                return sb.ToString();
            }
            catch (ArgumentNullException e)
            {
                Debug.LogError("Could not hash CyanTrigger!");
                Debug.LogError(e);
            }

            return null;
        }
    }
}

