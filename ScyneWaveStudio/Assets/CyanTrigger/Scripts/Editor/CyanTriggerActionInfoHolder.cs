using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerActionInfoHolder
    {
        private const string InvalidString = "<Invalid>";
        
        private static readonly CyanTriggerActionInfoHolder InvalidAction;
        private static readonly Dictionary<string, CyanTriggerActionInfoHolder> CustomActions =
            new Dictionary<string, CyanTriggerActionInfoHolder>();
        private static readonly Dictionary<string, CyanTriggerActionInfoHolder> DefinitionActions =
            new Dictionary<string, CyanTriggerActionInfoHolder>();
        
        public readonly CyanTriggerNodeDefinition definition;
        public readonly CyanTriggerCustomUdonNodeDefinition customDefinition;
        public readonly CyanTriggerActionDefinition action;
        public readonly CyanTriggerActionGroupDefinition actionGroup;

        
        static CyanTriggerActionInfoHolder()
        {
            InvalidAction = new CyanTriggerActionInfoHolder();
        }

        public static CyanTriggerActionInfoHolder GetActionInfoHolder(
            string guid,
            string directEvent)
        {
            if (!string.IsNullOrEmpty(guid))
            {
                return GetActionInfoHolderFromGuid(guid);
            }
            if (!string.IsNullOrEmpty(directEvent))
            {
                return GetActionInfoHolderFromDefinition(directEvent);
            }

            return InvalidAction;
        }

        public static CyanTriggerActionInfoHolder GetActionInfoHolder(CyanTriggerSettingsFavoriteItem favoriteItem)
        {
            var actionType = favoriteItem.data;
            return GetActionInfoHolder(actionType.guid, actionType.directEvent);
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolderFromGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return InvalidAction;
            }

            if (CustomActions.TryGetValue(guid, out var actionInfo))
            {
                if (actionInfo.IsValid())
                {
                    return actionInfo;
                }
                CustomActions.Remove(guid);
            }

            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionDefinition(guid,
                out CyanTriggerActionDefinition actionDef))
            {
                return InvalidAction;
            }
            
            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(actionDef, out var actionGroup))
            {
                return InvalidAction;
            }
            
            actionInfo = new CyanTriggerActionInfoHolder(actionDef, actionGroup);
            CustomActions.Add(guid, actionInfo);
            
            return actionInfo;
        }

        public static CyanTriggerActionInfoHolder GetActionInfoHolder(CyanTriggerActionDefinition actionDef)
        {
            if (actionDef == null || string.IsNullOrEmpty(actionDef.guid))
            {
                return InvalidAction;
            }
            
            if (CustomActions.TryGetValue(actionDef.guid, out var actionInfo))
            {
                if (actionInfo.IsValid())
                {
                    return actionInfo;
                }
                CustomActions.Remove(actionDef.guid);
            }
            
            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(actionDef, out var actionGroup))
            {
                return InvalidAction;
            }
            
            actionInfo = new CyanTriggerActionInfoHolder(actionDef, actionGroup);
            CustomActions.Add(actionDef.guid, actionInfo);
            
            return actionInfo;
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolder(
            CyanTriggerActionDefinition actionDef, 
            CyanTriggerActionGroupDefinition actionGroup)
        {
            if (actionDef == null || string.IsNullOrEmpty(actionDef.guid))
            {
                return InvalidAction;
            }
            
            if (CustomActions.TryGetValue(actionDef.guid, out var actionInfo))
            {
                if (actionInfo.IsValid())
                {
                    return actionInfo;
                }
                CustomActions.Remove(actionDef.guid);
            }
            
            if (actionGroup == null && 
                !CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(actionDef, out actionGroup))
            {
                return InvalidAction;
            }
            
            actionInfo = new CyanTriggerActionInfoHolder(actionDef, actionGroup);
            CustomActions.Add(actionDef.guid, actionInfo);
            
            return actionInfo;
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolder(UdonNodeDefinition definition)
        {
            if (definition == null)
            {
                return InvalidAction;
            }
            
            return GetActionInfoHolderFromDefinition(definition.fullName);
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolder(CyanTriggerNodeDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.fullName))
            {
                return InvalidAction;
            }

            string key = definition.fullName;
            if (DefinitionActions.TryGetValue(key, out var actionInfo))
            {
                if (actionInfo.IsValid())
                {
                    return actionInfo;
                }
                DefinitionActions.Remove(key);
            }

            actionInfo = new CyanTriggerActionInfoHolder(definition);
            DefinitionActions.Add(key, actionInfo);
            
            return actionInfo;
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolderFromDefinition(string definition)
        {
            if (string.IsNullOrEmpty(definition))
            {
                return InvalidAction;
            }

            if (DefinitionActions.TryGetValue(definition, out var actionInfo))
            {
                if (actionInfo.IsValid())
                {
                    return actionInfo;
                }
                DefinitionActions.Remove(definition);
            }

            var def = CyanTriggerNodeDefinitionManager.GetDefinition(definition);
            if (def == null)
            {
                return InvalidAction;
            }
            
            actionInfo = new CyanTriggerActionInfoHolder();
            DefinitionActions.Add(definition, actionInfo);
            
            return actionInfo;
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolderFromProperties(SerializedProperty actionProperty)
        {
            SerializedProperty actionTypeProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.actionType));
            SerializedProperty directEvent =
                actionTypeProperty.FindPropertyRelative(nameof(CyanTriggerActionType.directEvent));
            SerializedProperty guidProperty =
                actionTypeProperty.FindPropertyRelative(nameof(CyanTriggerActionType.guid));

            return GetActionInfoHolder(
                guidProperty.stringValue,
                directEvent.stringValue);
        }
        
        private CyanTriggerActionInfoHolder() { }

        private CyanTriggerActionInfoHolder(CyanTriggerNodeDefinition definition)
        {
            this.definition = definition;
            CyanTriggerNodeDefinitionManager.TryGetCustomDefinition(definition.fullName, out customDefinition);
        }

        private CyanTriggerActionInfoHolder(
            CyanTriggerActionDefinition action,
            CyanTriggerActionGroupDefinition actionGroup)
        {
            this.action = action;
            this.actionGroup = actionGroup;
            
            if (this.actionGroup == null)
            {
                CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(action, out this.actionGroup);
            }
        }
        
        public string GetDisplayName()
        {
            if (definition != null)
            {
                return definition.GetActionDisplayName();
            }

            if (action != null)
            {
                return action.actionName;
            }

            return InvalidString;
        }

        public string GetVariantName()
        {
            if (definition != null)
            {
                return "VRC_Direct";
            }

            if (action != null)
            {
                return action.actionVariantName;
            }

            return InvalidString;
        }

        public string GetActionRenderingDisplayName()
        {
            string displayName = GetDisplayName();

            if (definition != null)
            {
                return displayName;
            }

            return displayName + "." + GetVariantName();
        }

        public string GetMethodSignature()
        {
            if (definition != null)
            {
                return definition.GetMethodDisplayName();
            }
            
            if (action != null)
            {
                return action.GetMethodSignature();
            }
            
            return InvalidString;
        }

        public bool IsValid()
        {
            return IsDefinition() || IsAction();
        }

        public bool IsAction()
        {
            return action != null && actionGroup != null;
        }

        public bool IsDefinition()
        {
            return definition != null;
        }

        public bool Equals(CyanTriggerActionInfoHolder o)
        {
            return 
                o != null 
                && definition == o.definition 
                && action == o.action 
                && actionGroup == o.actionGroup;
        }

        public CyanTriggerActionVariableDefinition[] GetVariables()
        {
            if (action != null)
            {
                return action.variables;
            }
            
            if (definition == null || definition.fullName == "Event_Custom")
            {
                return new CyanTriggerActionVariableDefinition[0];
            }

            return definition.variableDefinitions;

            /*
            // This would only be used for custom to provide the name field, but it isn't labeled.
            // Keeping the code, but ignoring it here. 
            List<CyanTriggerActionVariableDefinition> variables = new List<CyanTriggerActionVariableDefinition>();
            foreach (var inputs in definition.GetEventVariables(1 /* inputs only UdonNodeParameter.ParameterType * /))
            {
                variables.Add(new CyanTriggerActionVariableDefinition
                {
                    type = new SerializableType(inputs.Item2), 
                    udonName = inputs.Item1, 
                    displayName = inputs.Item1, 
                    variableType = inputs.Item2 == typeof(CyanTriggerVariable) ? 
                        CyanTriggerActionVariableTypeDefinition.VariableInput :
                        CyanTriggerActionVariableTypeDefinition.Constant
                });
            }
            
            return variables.ToArray();
            */
        }

        public CyanTriggerEditorVariableOption[] GetVariableOptions()
        {
            var def = definition;
            if (action != null)
            {
                def = CyanTriggerNodeDefinitionManager.GetDefinition(action.baseEventName);
            }
            
            if (def == null)
            {
                return new CyanTriggerEditorVariableOption[0];
            }
            
            List<CyanTriggerEditorVariableOption> variables = new List<CyanTriggerEditorVariableOption>();
            foreach (var (varName, varType) in def.GetEventVariables(/* output only UdonNodeParameter.ParameterType */))
            {
                variables.Add(new CyanTriggerEditorVariableOption 
                    {Type = varType, Name = varName, IsReadOnly = true});
            }
            
            return variables.ToArray();
        }

        public CyanTriggerEditorVariableOption[] GetCustomEditorVariableOptions(SerializedProperty actionProperty)
        {
            if (definition != null &&
                customDefinition != null &&
                customDefinition.DefinesCustomEditorVariableOptions())
            {
                SerializedProperty inputsProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
                
                return customDefinition.GetCustomEditorVariableOptions(inputsProperty);
            }
            return null;
        }

        public int GetScopeDelta()
        {
            // Check if item has scope (block, blockend, while, for, if, else if, else)
            if (definition != null && CyanTriggerNodeDefinitionManager.DefinitionHasScope(definition.fullName))
            {
                return 1;
            }

            if (definition != null && 
                definition.definition == CyanTriggerCustomNodeBlockEnd.NodeDefinition)
            {
                return -1;
            }
            return 0;
        }

        public List<SerializedProperty> AddActionToEndOfPropertyList(
            SerializedProperty actionListProperty, 
            bool includeDependencies)
        {
            List<SerializedProperty> properties = new List<SerializedProperty>();
            actionListProperty.arraySize++;
            SerializedProperty newActionProperty =
                actionListProperty.GetArrayElementAtIndex(actionListProperty.arraySize - 1);
            properties.Add(newActionProperty);
            
            SetActionData(newActionProperty);
            
            // If scope, add appropriate end point
            if (includeDependencies &&
                definition != null && 
                customDefinition != null &&
                customDefinition.HasDependencyNodes())
            {
                foreach (var dependency in customDefinition.GetDependentNodes())
                {
                    properties.AddRange(
                        GetActionInfoHolder(dependency).AddActionToEndOfPropertyList(actionListProperty, true));
                }
            }

            return properties;
        }
        
        public void SetActionData(SerializedProperty actionProperty)
        {
            SerializedProperty actionTypeProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.actionType));
            SerializedProperty directEvent =
                actionTypeProperty.FindPropertyRelative(nameof(CyanTriggerActionType.directEvent));
            SerializedProperty guidProperty =
                actionTypeProperty.FindPropertyRelative(nameof(CyanTriggerActionType.guid));
            SerializedProperty inputsProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty multiInputsProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
            multiInputsProperty.ClearArray();

            if (action != null)
            {
                guidProperty.stringValue = action.guid;
                directEvent.stringValue = null;
                SetPropertyInputDefaults(inputsProperty, multiInputsProperty, action.variables);
            }

            if (definition != null)
            {
                guidProperty.stringValue = null;
                directEvent.stringValue = definition.fullName;

                SetPropertyInputDefaults(inputsProperty, multiInputsProperty, definition.variableDefinitions);
                
                if (customDefinition != null && customDefinition.HasCustomVariableInitialization())
                {
                    customDefinition.InitializeVariableProperties(inputsProperty, multiInputsProperty);
                }
            }
            
            actionProperty.serializedObject.ApplyModifiedProperties();
        }

        private static void SetPropertyInputDefaults(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty,
            CyanTriggerActionVariableDefinition[] variableDefinitions)
        {
            inputsProperty.ClearArray();
            inputsProperty.arraySize = variableDefinitions.Length;
            
            for (int cur = 0; cur < variableDefinitions.Length; ++cur)
            {
                var variableDef = variableDefinitions[cur];
                Type type = variableDef.type.type;
                
                SerializedProperty inputProperty;
                if (cur == 0 && (variableDef.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    if (type == typeof(VRCPlayerApi))
                    {
                        multiInputsProperty.arraySize = 1;
                        inputProperty = multiInputsProperty.GetArrayElementAtIndex(cur);
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    inputProperty = inputsProperty.GetArrayElementAtIndex(cur);
                }
                    
                SerializedProperty dataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                SerializedProperty isVariableProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                    
                var data = variableDef.defaultValue?.obj;
                if (data == null)
                {
                    if(type.IsValueType)
                    {
                        data = Activator.CreateInstance(type);
                    }
                    else if (type.IsArray)
                    {
                        data = Array.CreateInstance(type, 0);
                    } 
                    else if (type == typeof(string))
                    {
                        data = "";
                    }
                }

                if ((variableDef.variableType & CyanTriggerActionVariableTypeDefinition.Constant) == 0)
                {
                    isVariableProperty.boolValue = true;
                }

                if (type == typeof(VRCPlayerApi))
                {
                    isVariableProperty.boolValue = true;
                    SerializedProperty nameProperty =
                        inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                    SerializedProperty idProperty =
                        inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));

                    nameProperty.stringValue = CyanTriggerAssemblyData.LocalPlayerName;
                    idProperty.stringValue = CyanTriggerAssemblyData.LocalPlayerGUID;
                }
                
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, data);
            }
        }

        public void CopyDataAndRemapVariables(
            SerializedProperty srcProperty,
            SerializedProperty dstProperty, 
            Dictionary<string, string> variableGuidMap)
        {
            SerializedProperty srcInputsProperty =
                srcProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty dstInputsProperty =
                dstProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty srcMultiInputsProperty =
                srcProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
            SerializedProperty dstMultiInputsProperty =
                dstProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));

            int startIndex = 0;
            int endIndex = srcInputsProperty.arraySize;
            
            // If defines custom variables, get old guid and add mapping
            if (customDefinition != null && customDefinition.DefinesCustomEditorVariableOptions())
            {
                var srcOptions = customDefinition.GetCustomEditorVariableOptions(srcInputsProperty);
                var dstOptions = customDefinition.GetCustomEditorVariableOptions(dstInputsProperty);

                Debug.Assert(srcOptions.Length == dstOptions.Length,
                    "Duplicated property has different custom variable option sizes! src: " + srcOptions.Length +
                    ", dst: " + dstOptions.Length);

                for (int cur = 0; cur < srcOptions.Length; ++cur)
                {
                    string srcGuid = srcOptions[cur].ID;
                    string dstGuid = dstOptions[cur].ID;
                    variableGuidMap.Add(srcGuid, dstGuid);
                }

                // Ensure that variable data is not overwritten when copied
                if (customDefinition is CyanTriggerCustomNodeVariableProvider variableProvider)
                {
                    (startIndex, endIndex) = variableProvider.GetDefinitionVariableRange();
                }
            }

            // Copy all data from the source property to the destination.
            // Ensure that variable guids are remapped properly.
            SetPropertyInputDefaults(
                srcInputsProperty, 
                dstInputsProperty, 
                srcMultiInputsProperty,
                dstMultiInputsProperty,
                variableGuidMap, 
                startIndex, 
                endIndex);
        }

        private static void SetPropertyInputDefaults(
            SerializedProperty srcInputProperties,
            SerializedProperty dstInputProperties,
            SerializedProperty srcMultiInputProperties,
            SerializedProperty dstMultiInputProperties,
            Dictionary<string, string> variableGuidMap,
            int startIndex,
            int endIndex)
        {
            void CopyInputProperty(SerializedProperty srcInputProperty, SerializedProperty dstInputProperty)
            {
                SerializedProperty srcDataProperty =
                    srcInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                SerializedProperty srcIsVariableProperty =
                    srcInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                SerializedProperty srcGuidProperty =
                    srcInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                SerializedProperty srcNameProperty =
                    srcInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                
                SerializedProperty dstDataProperty =
                    dstInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                SerializedProperty dstIsVariableProperty =
                    dstInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                SerializedProperty dstGuidProperty =
                    dstInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                SerializedProperty dstNameProperty =
                    dstInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));

                dstIsVariableProperty.boolValue = srcIsVariableProperty.boolValue;
                dstNameProperty.stringValue = srcNameProperty.stringValue;
                
                CyanTriggerSerializableObject.CopySerializedProperty(srcDataProperty, dstDataProperty);
                
                // Remap the variable if it was duplicated
                string srcGuid = srcGuidProperty.stringValue;
                if (!variableGuidMap.TryGetValue(srcGuid, out var dstGuid))
                {
                    dstGuid = srcGuid;
                }
                dstGuidProperty.stringValue = dstGuid;
            }
            
            dstInputProperties.arraySize = srcInputProperties.arraySize;
            for (int cur = startIndex; cur < endIndex; ++cur)
            {
                SerializedProperty srcInputProperty = srcInputProperties.GetArrayElementAtIndex(cur);
                SerializedProperty dstInputProperty = dstInputProperties.GetArrayElementAtIndex(cur);
                CopyInputProperty(srcInputProperty, dstInputProperty);
            }
            
            dstMultiInputProperties.arraySize = srcMultiInputProperties.arraySize;
            for (int cur = 0; cur < srcMultiInputProperties.arraySize; ++cur)
            {
                SerializedProperty srcInputProperty = srcMultiInputProperties.GetArrayElementAtIndex(cur);
                SerializedProperty dstInputProperty = dstMultiInputProperties.GetArrayElementAtIndex(cur);
                CopyInputProperty(srcInputProperty, dstInputProperty);
            }
        }

        public string GetActionRenderingDisplayName(SerializedProperty actionProperty)
        {
            if (!CyanTriggerSettings.Instance.actionDetailedView)
            {
                return GetActionRenderingDisplayName();
            }
            
            string signature = GetActionRenderingDisplayName();
            if (customDefinition is CyanTriggerCustomNodeVariable)
            {
                var variableDefinitions = GetVariables();
                SerializedProperty inputsProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
                SerializedProperty inputNameProperty = inputsProperty.GetArrayElementAtIndex(0);
                SerializedProperty inputDataProperty = inputsProperty.GetArrayElementAtIndex(1);
                
                string name = GetTextForProperty(inputNameProperty, variableDefinitions[0]);
                name = name.Substring(1, name.Length - 2);
            
                return "var " + name +" " + signature + 
                       ".Set(" + GetTextForProperty(inputDataProperty, variableDefinitions[1]) + ")";
            }
            
            return signature + GetMethodArgumentDisplay(actionProperty);
        }
        
        public string GetMethodArgumentDisplay(SerializedProperty actionProperty)
        {
            SerializedProperty inputsProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            
            StringBuilder sb = new StringBuilder();

            var variableDefinitions = GetVariables();
            int displayCount = 0;
            for (int input = 0; input < inputsProperty.arraySize && input < variableDefinitions.Length; ++input)
            {
                var variableDef = variableDefinitions[input];
                if ((variableDef.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
                {
                    continue;
                }

                if (displayCount > 0)
                {
                    sb.Append(", ");
                }
                ++displayCount;
                
                Type varType = variableDef.type.type;
                
                if (input == 0 &&
                    (variableDef.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    SerializedProperty multiInputsProperty =
                        actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                    
                    if (multiInputsProperty.arraySize == 0)
                    {
                        sb.Append("null");
                    }
                    else if (multiInputsProperty.arraySize == 1)
                    {
                        SerializedProperty multiInputProperty = multiInputsProperty.GetArrayElementAtIndex(0);
                        sb.Append(GetTextForProperty(multiInputProperty, variableDef));
                    }
                    else
                    {
                        sb.Append(CyanTriggerNameHelpers.GetTypeFriendlyName(varType) +"Array");
                    }
                    
                    continue;
                }
                
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(input);
                sb.Append(GetTextForProperty(inputProperty, variableDef));
            }

            if (sb.Length > 0)
            {
                return "(" + sb + ")";
            }
            
            return sb.ToString();
        }
        
        public static string GetTextForProperty(
            SerializedProperty inputProperty, 
            CyanTriggerActionVariableDefinition variableDef)
            {
                SerializedProperty isVariableProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                
                if (isVariableProperty.boolValue)
                {
                    SerializedProperty nameProperty =
                        inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));

                    bool isOutput =
                        (variableDef.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0;
                   
                    string displayName = nameProperty.stringValue;
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = "null";
                    }
                    
                    // TODO verify that name is always filled :eyes:
                    return (isOutput ? "out " : "") + "var " + displayName;
                }
                
                SerializedProperty dataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                var data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);

                if (data == null)
                {
                    return "null";
                }

                Type varType = variableDef.type.type;

                
                if (varType == typeof(string))
                {
                    return "\"" + data +"\"";
                }

                if (data is GameObject gameObject)
                {
                    return VRC.Tools.GetGameObjectPath(gameObject);
                }
                if (data is Component component)
                {
                    return VRC.Tools.GetGameObjectPath(component.gameObject);
                }

                if (data is UnityEngine.Object obj)
                {
                    return obj.name;
                }
                
                string ret = data.ToString();
                if (ret == varType.FullName ||
                    (varType.IsValueType 
                     && !varType.IsPrimitive && 
                     varType.GetMethod("ToString", new Type[0]).DeclaringType == typeof(ValueType)))
                {
                    return "const " + CyanTriggerNameHelpers.GetTypeFriendlyName(varType);
                }

                return ret;
            }
    }
  
}