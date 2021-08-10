using System;
using UnityEngine;

namespace CyanTrigger
{
    public static class CyanTriggerUtil
    {
        public enum InvalidReason
        {
            Valid,
            IsNull,
            InvalidDefinition,
            InvalidInput,
            MissingVariable,
            DataIsNull,
            InputTypeMismatch,
            InputLengthMismatch,
        }

        public static bool ValidateVariables(this CyanTriggerActionInstance actionInstance)
        {
            if (actionInstance == null)
            {
                return false;
            }
            var actionType = actionInstance.actionType;
            var actionInfoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(
                actionType.guid,
                actionType.directEvent);
            if (!actionInfoHolder.IsValid())
            {
                return false;
            }

            bool changed = false;
            var variables = actionInfoHolder.GetVariables();

            if (actionInstance.inputs == null)
            {
                actionInstance.inputs = new CyanTriggerActionVariableInstance[variables.Length];
                changed = true;
            }
            else if (variables.Length != actionInstance.inputs.Length)
            {
                changed = true;
                Array.Resize(ref actionInstance.inputs, variables.Length); 
            }

            return changed;
        }
        
        // TODO return reason for being invalid instead of simply true/false
        public static InvalidReason IsValid(this CyanTriggerActionInstance actionInstance)
        {
            if (actionInstance == null)
            {
                return InvalidReason.IsNull;
            }
            
            var actionType = actionInstance.actionType;
            var actionInfoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(
                actionType.guid,
                actionType.directEvent);
            if (!actionInfoHolder.IsValid())
            {
                return InvalidReason.InvalidDefinition;
            }

            var variables = actionInfoHolder.GetVariables();

            if (variables.Length != actionInstance.inputs.Length)
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogWarning("Input length did not equal variable def length. This shouldn't happen.");
#endif
                return InvalidReason.InputLengthMismatch;
            }
            
            bool firstAllowsMulti = 
                variables.Length > 0 &&
                (variables[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0;
            if (firstAllowsMulti && actionInstance.multiInput.Length == 0)
            {
                return InvalidReason.InvalidInput;
            }
            
            // TODO verify inputs match definition
            for (int input = 0; input < variables.Length; ++input)
            {
                if (input == 0 && firstAllowsMulti)
                {
                    if (actionInstance.multiInput.Length == 0)
                    {
                        return InvalidReason.InvalidInput;
                    }
                    
                    foreach (var variable in actionInstance.multiInput)
                    {
                        var reason = variable.IsValid(variables[input]);
                        if (reason != InvalidReason.Valid)
                        {
                            Debug.LogWarning("Invalid: " + reason);
                            return InvalidReason.InvalidInput;
                        }
                    }
                    continue;
                }
                
                if ((variables[input].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    return InvalidReason.InvalidDefinition;
                }

                if (actionInstance.inputs[input].IsValid(variables[input]) != InvalidReason.Valid)
                {
                    return InvalidReason.InvalidInput;
                }
            }

            return InvalidReason.Valid;
        }

        public static InvalidReason IsValid(
            this CyanTriggerActionVariableInstance variableInstance, 
            CyanTriggerActionVariableDefinition variableDef = null)
        {
            if (variableInstance == null)
            {
                return InvalidReason.IsNull;
            }
            
            // TODO do we even need this check? If there is no variable, it's not an error, it just ignores setting the output.
            // if (variableInstance.isVariable)
            // {
            //     if (string.IsNullOrEmpty(variableInstance.name) &&
            //         string.IsNullOrEmpty(variableInstance.variableID))
            //     {
            //         return InvalidReason.MissingVariable;
            //     }
            //     
            //     // TODO verify variable options are valid given available variables
            // }
            //else // Constant object
            {
                // This doesn't appear to work on reload...
                
                // Object is null or deleted
                // if (variableInstance.data.obj != null && ( 
                //     (variableInstance.data.obj.GetType().IsSubclassOf(typeof(Component)) && 
                //     (variableInstance.data.obj as Component) == null) ||
                //     variableInstance.data.obj is GameObject otherGameObject && otherGameObject == null))
                // {
                //     Debug.Log(variableInstance.data.obj);
                //     return InvalidReason.DataIsNull;
                // }
                
                
                // Object stored does not match type in definition
                // if (variableDef != null && 
                //     variableInstance.data.obj != null && 
                //     !variableDef.type.type.IsInstanceOfType(variableInstance.data.obj))
                // {
                //     Debug.Log("Type is wrong? " + variableInstance.data.obj.GetType() +" -> " + variableDef.type.type);
                //     return InvalidReason.InputTypeMismatch;
                // }
            }
            
            // TODO Check other cases
            
            return InvalidReason.Valid;
        }

        /*
        public static bool IsValidActionInstance(SerializedProperty actionProperty)
        {
            if (!CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty).IsValid())
            {
                return false;
            }
            
            SerializedProperty inputProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty multiInputProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
            
            // TODO verify inputs match definition
            for (int input = 0; input < inputProperty.arraySize; ++input)
            {
                if (input == 0 && multiInputProperty.arraySize > 0)
                {
                    for (int multiInput = 0; multiInput < inputProperty.arraySize; ++multiInput)
                    {
                        SerializedProperty multiVarProperty = inputProperty.GetArrayElementAtIndex(multiInput);
                        if (!IsValidActionVariableInstance(multiVarProperty))
                        {
                            return false;
                        }
                    }
                    continue;
                }

                SerializedProperty varProperty = inputProperty.GetArrayElementAtIndex(input);
                if (!IsValidActionVariableInstance(varProperty))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        public static bool IsValidActionVariableInstance(SerializedProperty variableProp)
        {
            SerializedProperty isVariableProperty =
                variableProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            SerializedProperty idProperty =
                variableProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
            SerializedProperty nameProperty =
                variableProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));

            if (isVariableProperty.boolValue && 
                string.IsNullOrEmpty(idProperty.stringValue) &&
                string.IsNullOrEmpty(nameProperty.stringValue))
            {
                return false;
            }
            
            // TODO other?
            
            return true;
        }
        */

        public static CyanTriggerDataInstance CopyCyanTriggerDataInstance(CyanTriggerDataInstance data, bool copyData)
        {
            if (data == null)
            {
                return null;
            }
            
            CyanTriggerDataInstance ret = new CyanTriggerDataInstance()
            {
                version = data.version,
                events = new CyanTriggerEvent[data.events.Length],
                variables = new CyanTriggerVariable[data.variables.Length],
                programSyncMode = data.programSyncMode,
                updateOrder = data.updateOrder
            };

            for (int cur = 0; cur < ret.events.Length; ++cur)
            {
                ret.events[cur] = CopyEvent(data.events[cur], copyData);
            }
            
            for (int cur = 0; cur < ret.variables.Length; ++cur)
            {
                ret.variables[cur] = CopyVariable(data.variables[cur], copyData);
            }            
            
            return ret;
        }
        
        public static CyanTriggerVariable CopyVariable(CyanTriggerVariable variable, bool copyData)
        {
            CyanTriggerVariable ret = new CyanTriggerVariable
            {
                name = variable.name,
                sync = variable.sync,
                isVariable = variable.isVariable,
                variableID = variable.variableID,
                type = new CyanTriggerSerializableType(variable.type.type),
            };

            if (copyData)
            {
                ret.data = new CyanTriggerSerializableObject(variable.data.obj);
            }

            return ret;
        }
                
        public static CyanTriggerActionVariableInstance CopyVariableInst(
            CyanTriggerActionVariableInstance variable, bool copyData)
        {
            CyanTriggerActionVariableInstance ret = new CyanTriggerActionVariableInstance
            {
                name = variable.name,
                isVariable = variable.isVariable,
                variableID = variable.variableID,
            };

            // Some values are used in the program and are needed in compilation...
            // CyanTrigger.ActivateCustomTrigger requires string data
            // TODO eventually move those to another field
            object data = variable.data.obj;
            if (copyData || (data != null && !(data is UnityEngine.Object)))
            {
                ret.data = new CyanTriggerSerializableObject(data);
            }
            
            return ret;
        }

        public static CyanTriggerActionType CopyActionType(CyanTriggerActionType actionType)
        {
            return new CyanTriggerActionType
            {
                guid = actionType.guid,
                directEvent = actionType.directEvent,
            };
        }
        
        public static CyanTriggerActionInstance CopyActionInst(CyanTriggerActionInstance action, bool copyData)
        {
            var ret = new CyanTriggerActionInstance
            {
                actionType = CopyActionType(action.actionType),
                inputs = new CyanTriggerActionVariableInstance[action.inputs.Length],
                multiInput = new CyanTriggerActionVariableInstance[action.multiInput.Length],
            };

            for (int cur = 0; cur < ret.inputs.Length; ++cur)
            {
                ret.inputs[cur] = CopyVariableInst(action.inputs[cur], copyData);
            }
            for (int cur = 0; cur < ret.multiInput.Length; ++cur)
            {
                ret.multiInput[cur] = CopyVariableInst(action.multiInput[cur], copyData);
            }

            return ret;
        }

        public static CyanTriggerEventOptions CopyEventOptions(CyanTriggerEventOptions eventOptions)
        {
            var ret = new CyanTriggerEventOptions
            {
                broadcast = eventOptions.broadcast,
                delay = eventOptions.delay,
                userGate = eventOptions.userGate,
                userGateExtraData =
                    new CyanTriggerActionVariableInstance[eventOptions.userGateExtraData.Length],
            };
            
            for (int cur = 0; cur < ret.userGateExtraData.Length; ++cur)
            {
                // TODO Update copy data once it becomes a reference instead of const in the program.
                ret.userGateExtraData[cur] = CopyVariableInst(eventOptions.userGateExtraData[cur], true);
            }
            
            return ret;
        }

        public static CyanTriggerEvent CopyEvent(CyanTriggerEvent oldEvent, bool copyData)
        {
            var ret = new CyanTriggerEvent
            {
                name = oldEvent.name,
                actionInstances = new CyanTriggerActionInstance[oldEvent.actionInstances.Length],
                eventInstance = CopyActionInst(oldEvent.eventInstance, copyData),
                eventOptions = CopyEventOptions(oldEvent.eventOptions),
            };

            for (int cur = 0; cur < ret.actionInstances.Length; ++cur)
            {
                ret.actionInstances[cur] = CopyActionInst(oldEvent.actionInstances[cur], copyData);
            }
            
            return ret;
        }
    }
}
