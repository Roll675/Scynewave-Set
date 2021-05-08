using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace CyanTrigger
{
    [Serializable]
    public class CyanTriggerActionDataReferenceIndex
    {
        public string symbolName;
        public Type symbolType;
        public int eventIndex;
        public int actionIndex;
        public int variableIndex;
        public int multiVariableIndex;

        public override string ToString()
        {
            return $"Symbol: {symbolType} {symbolName} event[{eventIndex}].action[{actionIndex}].var[{multiVariableIndex}, {variableIndex}]";
        }
    }
    
    [Serializable]
    public class CyanTriggerDataReferences
    {
        public readonly List<CyanTriggerActionDataReferenceIndex> ActionDataIndices =
            new List<CyanTriggerActionDataReferenceIndex>();

        public string animatorSymbol;
        public readonly Dictionary<string, Type> userVariables = new Dictionary<string, Type>();
        
        public void ApplyPublicVariableData(
            CyanTriggerDataInstance cyanTriggerDataInstance,
            UdonBehaviour udonBehaviour,
            IUdonSymbolTable symbolTable,
            ref bool dirty) 
        {
            IUdonVariableTable publicVariables = udonBehaviour.publicVariables;
            if (publicVariables == null)
            {
                Debug.LogError("Cannot set public variables when VariableTable is null");
                return;
            }
            
            // Remove non-exported public variables
            foreach(string publicVariableSymbol in new List<string>(publicVariables.VariableSymbols))
            {
                if(!symbolTable.HasExportedSymbol(publicVariableSymbol))
                {
                    //Debug.Log("Removing Reference: " + publicVariableSymbol);
                    publicVariables.RemoveVariable(publicVariableSymbol);
                }
            }
            

            HashSet<string> usedVariables = new HashSet<string>();
            usedVariables.Add(CyanTriggerAssemblyData.ThisGameObjectGUID);
            usedVariables.Add(CyanTriggerAssemblyData.ThisTransformGUID);
            usedVariables.Add(CyanTriggerAssemblyData.ThisUdonBehaviourGUID);

            SetUdonVariable(
                udonBehaviour, 
                publicVariables, 
                CyanTriggerAssemblyData.ThisGameObjectGUID, 
                typeof(GameObject), 
                udonBehaviour.gameObject,
                ref dirty);
            
            SetUdonVariable(
                udonBehaviour, 
                publicVariables, 
                CyanTriggerAssemblyData.ThisTransformGUID, 
                typeof(Transform), 
                udonBehaviour.transform,
                ref dirty);
            
            SetUdonVariable(
                udonBehaviour, 
                publicVariables, 
                CyanTriggerAssemblyData.ThisUdonBehaviourGUID, 
                typeof(IUdonEventReceiver), 
                udonBehaviour,
                ref dirty);

            // TODO figure out a more generic way to handle data like animator move and timerqueue
            
            // Add TimerQueue if in the code
            string timerQueueSymbolName =
                CyanTriggerAssemblyData.GetSpecialVariableName(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName
                    .TimerQueue);
            if (symbolTable.HasExportedSymbol(timerQueueSymbolName))
            {
                usedVariables.Add(timerQueueSymbolName);
                SetUdonVariable(
                    udonBehaviour, 
                    publicVariables, 
                    timerQueueSymbolName, 
                    typeof(IUdonEventReceiver), 
                    CyanTriggerResourceManager.CyanTriggerResources.timerQueueUdonBehaviour,
                    ref dirty);
            }
            
            if (!string.IsNullOrEmpty(animatorSymbol))
            {
                usedVariables.Add(animatorSymbol);
                
                SetUdonVariable(
                    udonBehaviour, 
                    publicVariables, 
                    animatorSymbol, 
                    typeof(Animator), 
                    udonBehaviour.GetComponent<Animator>(),
                    ref dirty);
            }
            
            foreach (var variable in cyanTriggerDataInstance.variables)
            {
                // Check if variable was valid based on last compile
                if (!userVariables.TryGetValue(variable.name, out var type) || type != variable.type.type)
                {
                    continue;   
                }
                
                usedVariables.Add(variable.name);

                object value = variable.data.obj;
                
                SetUdonVariable(
                    udonBehaviour, 
                    publicVariables, 
                    variable.name,
                    type, 
                    value,
                    ref dirty);

                // Variable had a callback. Ensure that previous value is equal to default value.
                string prevVarName = CyanTriggerAssemblyData.GetPreviousUserVarName(variable.name);
                if (symbolTable.HasExportedSymbol(prevVarName))
                {
                    usedVariables.Add(prevVarName);
                    SetUdonVariable(
                        udonBehaviour, 
                        publicVariables, 
                        prevVarName,
                        type, 
                        value,
                        ref dirty);
                }
            }
            
            foreach (var publicVar in ActionDataIndices)
            {
                try
                {
                    object data = null;
                    Type type = publicVar.symbolType;

                    var eventInstance = cyanTriggerDataInstance
                        .events[publicVar.eventIndex];
                    CyanTriggerActionInstance actionInstance;
                    
                    if (publicVar.actionIndex < 0)
                    {
                        // TODO figure out event organization here
                        actionInstance = eventInstance.eventInstance;
                    }
                    else
                    {
                        actionInstance = eventInstance.actionInstances[publicVar.actionIndex];
                    }

                    // TODO figure out a way to get modified data from custom udon node definitions.
                    if (actionInstance != null)
                    {
                        CyanTriggerActionVariableInstance variableInstance;
                        if (publicVar.multiVariableIndex != -1)
                        {
                            variableInstance = actionInstance.multiInput[publicVar.multiVariableIndex];
                        }
                        else
                        {
                            variableInstance = actionInstance.inputs[publicVar.variableIndex];
                        }

                        data = variableInstance.data.obj;
                    }
                    
                    // TODO fix this. This is too hacky.
                    if (type == typeof(CyanTrigger))
                    {
                        type = typeof(IUdonEventReceiver);
                        if (data is CyanTrigger trigger)
                        {
                            data = trigger.triggerInstance.udonBehaviour;
                        }
                    }

                    // TODO find a better way here...
                    if (publicVar.variableIndex == 1 && 
                        actionInstance.actionType.directEvent == CyanTriggerCustomNodeSetComponentActive.FullName )
                    {
                        string varType = data as string;
                        if (CyanTriggerNodeDefinitionManager.TryGetComponentType(varType, out var componentType))
                        {
                            data = componentType.AssemblyQualifiedName;
                        }
                    }
                    
#if CYAN_TRIGGER_DEBUG
                    Type expectedType = symbolTable.GetSymbolType(publicVar.symbolName);
                    if (expectedType != type)
                    {
                        Debug.LogWarning("Type for symbol does not match public variable type. " + expectedType +", " + type);
                    }
#endif
                    
                    usedVariables.Add(publicVar.symbolName);
                    
                    SetUdonVariable(
                        udonBehaviour, 
                        publicVariables, 
                        publicVar.symbolName, 
                        type, 
                        data,
                        ref dirty);
                }
                catch (Exception ex)
                {
                    Debug.LogError("Could not set default variable for trigger: " +publicVar);
                    Debug.LogError(ex);
                }
            }
            
#if CYAN_TRIGGER_DEBUG
            // Used for debug purposes to see if a public variable was missed.
            foreach (string publicVariableSymbol in new List<string>(publicVariables.VariableSymbols))
            {
                if(!usedVariables.Contains(publicVariableSymbol))
                {
                    Debug.LogWarning("[Internal] Variable was unused: " + publicVariableSymbol);
                }
            }
#endif
        }

        private static void SetUdonVariable(
            UdonBehaviour udonBehaviour, 
            IUdonVariableTable publicVariables, 
            string exportedSymbol, 
            Type symbolType, 
            object value, 
            ref bool dirty)
        {
            bool hasVariable = publicVariables.TryGetVariableValue(exportedSymbol, out object variableValue);

            if (value == null || (value is UnityEngine.Object unityValue && unityValue == null))
            {
                if (hasVariable)
                {
                    dirty = true;
                    //Debug.Log(exportedSymbol +" was changed! " + variableValue +" to " +value);

                    EditorUtility.SetDirty(udonBehaviour);
 
                    Undo.RecordObject(udonBehaviour, "Modify Public Variable");

                    publicVariables.RemoveVariable(exportedSymbol);

                    EditorSceneManager.MarkSceneDirty(udonBehaviour.gameObject.scene);

                    if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(udonBehaviour);
                    }
                }
                
                return;
            }
            
            if (!hasVariable || !value.Equals(variableValue))
            {
                dirty = true;
                //Debug.Log(exportedSymbol +" was changed! " + variableValue +" to " +value);

                EditorUtility.SetDirty(udonBehaviour);
 
                Undo.RecordObject(udonBehaviour, "Modify Public Variable");

                if (!publicVariables.TrySetVariableValue(exportedSymbol, value))
                {
                    if (!publicVariables.TryAddVariable(CreateUdonVariable(exportedSymbol, value, symbolType)))
                    {
                        Debug.LogError($"Failed to set public variable '{exportedSymbol}' value.");
                    }
                }

                EditorSceneManager.MarkSceneDirty(udonBehaviour.gameObject.scene);

                if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(udonBehaviour);
                }
            }
        }
        
        public static IUdonVariable CreateUdonVariable(string symbolName, object value, Type declaredType)
        {
            try
            {
                Type udonVariableType = typeof(UdonVariable<>).MakeGenericType(declaredType);
                return (IUdonVariable) Activator.CreateInstance(udonVariableType, symbolName, value);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to create UdonVariable for symbol: " + symbolName +", type: " +declaredType +", object: " +value);
                Debug.LogError(e);
                throw e;
            }
        }
    }
}
