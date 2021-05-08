using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace CyanTrigger
{
    public class CyanTriggerSerializableInstanceEditor
    {
        private static readonly GUIContent SelectToEditContent = new GUIContent("Select to Edit");
        private static readonly GUIContent AnimatorMoveContent = new GUIContent("Apply Animator Move",
            "When an UdonBehaviour is on the same object as an animator, root motion breaks. With this option enabled, the transform updates can be auto applied through implementing OnAnimatorMove in Udon.");
        private static readonly GUIContent InteractTextContent = new GUIContent("Interaction Text",
            "Text that will be shown to the user when they highlight an object to interact.");
        private static readonly GUIContent ProximityContent = new GUIContent("Proximity",
            "How close the user needs to be before the object can be interacted with. Note that this is not in unity units and the distance depends on the avatar scale.");

        private const string UnnamedCustomName = "Unnamed";
        
        public static readonly CyanTriggerActionVariableDefinition AllowedUserGateVariableDefinition =
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(string)),
                variableType = CyanTriggerActionVariableTypeDefinition.Constant |
                               CyanTriggerActionVariableTypeDefinition.VariableInput,
                displayName = "Allowed Users",
                description = "If the local user's name is in this list, they will be allowed to initiate this event."
            };
        public static readonly CyanTriggerActionVariableDefinition DeniedUserGateVariableDefinition =
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(string)),
                variableType = CyanTriggerActionVariableTypeDefinition.Constant |
                               CyanTriggerActionVariableTypeDefinition.VariableInput,
                displayName = "Denied Users",
                description = "If the local user's name is in this list, they will be denied from initiating this event."
            };

        private static readonly List<CyanTriggerEditorVariableOption> EmptyVariableOptionsList = new List<CyanTriggerEditorVariableOption>();

        private static readonly HashSet<CyanTriggerSerializableInstanceEditor> OpenSerializers =
            new HashSet<CyanTriggerSerializableInstanceEditor>();
        
        private readonly HashSet<string> _optionExpands = new HashSet<string>();

        
        private readonly SerializedObject _serializedObject;
        private readonly SerializedProperty _serializedProperty;
        private readonly SerializedProperty _dataInstanceProperty;
        private readonly CyanTriggerSerializableInstance _cyanTriggerSerializableInstance;
        private readonly CyanTriggerDataInstance _cyanTriggerDataInstance;
        

        private GUIStyle _style;

        private readonly CyanTriggerVariableTreeView _variableTreeView;
        private readonly SerializedProperty _variableDataProperty;

        private readonly Dictionary<Type, CyanTriggerEditorVariableOptionList> _userVariableOptions =
            new Dictionary<Type, CyanTriggerEditorVariableOptionList>();

        private CyanTriggerActionTreeView.ActionInstanceRenderData[] _eventInstanceRenderData = 
            new CyanTriggerActionTreeView.ActionInstanceRenderData[0];
        private CyanTriggerActionTreeView.ActionInstanceRenderData[] _eventOptionRenderData = 
            new CyanTriggerActionTreeView.ActionInstanceRenderData[0];

        private int _eventListSize;
        private readonly SerializedProperty _eventsProperty;
        private bool[] _hiddenEvents = new bool[0];
        private ReorderableList[] _eventActionUserGateLists = new ReorderableList[0];
        private Dictionary<int, ReorderableList>[] _eventActionInputLists = new Dictionary<int, ReorderableList>[0];
        private Dictionary<int, ReorderableList>[] _eventInputLists = new Dictionary<int, ReorderableList>[0];

        private CyanTriggerActionTreeView[] _eventActionTrees = new CyanTriggerActionTreeView[0];
        
        private bool _resetVariableInputs = false;

        private Editor _baseEditor;
        private bool _showActionDetails;

        private CyanTriggerEditorScopeTree[] _scopeTreeRoot = new CyanTriggerEditorScopeTree[0];
        
        
        public CyanTriggerSerializableInstanceEditor( 
            SerializedProperty serializedProperty, 
            CyanTriggerSerializableInstance cyanTriggerSerializableInstance,
            Editor baseEditor)
        {
            OpenSerializers.Add(this);
            
            _serializedProperty = serializedProperty;
            _serializedObject = serializedProperty.serializedObject;
            _cyanTriggerSerializableInstance = cyanTriggerSerializableInstance;
            _baseEditor = baseEditor;
            
            _cyanTriggerDataInstance = cyanTriggerSerializableInstance.triggerDataInstance;
            _dataInstanceProperty =
                serializedProperty.FindPropertyRelative(nameof(CyanTriggerSerializableInstance.triggerDataInstance));

            _variableDataProperty = _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.variables));
            _eventsProperty = _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.events));

            _variableTreeView = new CyanTriggerVariableTreeView(
                _variableDataProperty,
                OnVariableAddedOrRemoved,
                (varName, varGuid) =>
                {
                    varName = CyanTriggerNameHelpers.SanitizeName(varName);
                    return GetUniqueVariableName(varName, varGuid, _cyanTriggerDataInstance.variables);
                });

            // TODO find a better location for this?
            SerializedProperty version =
                _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.version));
            if (version.intValue != CyanTriggerDataInstance.DataVersion)
            {
                version.intValue = CyanTriggerDataInstance.DataVersion;
            }
            
            UpdateUserVariableOptions();
        }
        
        public void Dispose()
        {
            _baseEditor = null;
            OpenSerializers.Remove(this);
        }

        public static void UpdateAllOpenSerializers()
        {
            foreach (var serializer in OpenSerializers)
            {
                serializer._showActionDetails = CyanTriggerSettings.Instance.actionDetailedView;
                serializer.UpdateActionTreeDisplayNames();
                serializer._baseEditor.Repaint();
            }
        }

        public void OnInspectorGUI()
        {
            Profiler.BeginSample("CyanTriggerEditor");
            _style = new GUIStyle(EditorStyles.helpBox);

            _serializedObject.Update();

            if (Event.current.type == EventType.ValidateCommand &&
                Event.current.commandName == "UndoRedoPerformed")
            {
                ResetValues();
            }

            UpdateVariableScope();

            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 30));
            
            EditorGUILayout.Space();
            
            RenderHeader();
            
            RenderExtraOptions();
            
            RenderVariables();

            RenderEvents();

            EditorGUILayout.EndVertical();

            // if ((GUI.changed && _serializedObject.hasModifiedProperties) ||
            //     (Event.current.type == EventType.ValidateCommand &&
            //      Event.current.commandName == "UndoRedoPerformed"))
            // {
            //     MarkDirty();
            // }

            _serializedObject.ApplyModifiedProperties();


            if (_resetVariableInputs)
            {
                _resetVariableInputs = false;
                UpdateUserVariableOptions();
            }
            Profiler.EndSample();
        }

        private void ResetValues()
        {
            UpdateUserVariableOptions();

            _eventListSize = _eventsProperty.arraySize;

            _eventActionUserGateLists = new ReorderableList[_eventListSize];

            Array.Resize(ref _hiddenEvents, _eventListSize);
            Array.Resize(ref _eventInputLists, _eventListSize);
            Array.Resize(ref _eventActionInputLists, _eventListSize);

            for (int i = 0; i < _eventListSize; ++i)
            {
                if (_eventInputLists[i] == null)
                {
                    _eventInputLists[i] = new Dictionary<int, ReorderableList>();
                }
                else
                {
                    _eventInputLists[i].Clear();
                }

                if (_eventActionInputLists[i] == null)
                {
                    _eventActionInputLists[i] = new Dictionary<int, ReorderableList>();
                }
                else
                {
                    _eventActionInputLists[i].Clear();
                }

                var eventData = _eventInstanceRenderData[i];
                if (eventData != null)
                {
                    eventData.ClearInputLists();
                    eventData.Property = _eventsProperty.GetArrayElementAtIndex(i)
                        .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                }
                
                _eventOptionRenderData[i]?.ClearInputLists();
            }
        }

        private void UpdateVariableScope()
        {
            if (_scopeTreeRoot.Length != _eventsProperty.arraySize)
            {
                _scopeTreeRoot = new CyanTriggerEditorScopeTree[_eventsProperty.arraySize];
            }

            for (int eventIndex = 0; eventIndex < _eventsProperty.arraySize; ++eventIndex)
            {
                if (_scopeTreeRoot[eventIndex] != null)
                {
                    continue;
                }
                
                _scopeTreeRoot[eventIndex] = new CyanTriggerEditorScopeTree();
                var actionListProperty = _eventsProperty.GetArrayElementAtIndex(eventIndex)
                    .FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
                _scopeTreeRoot[eventIndex].CreateStructure(actionListProperty);
            }
        }

        private void UpdateActionTreeDisplayNames()
        {
            for (int index = 0; index < _eventActionTrees.Length; ++index)
            {
                if (_eventActionTrees[index] != null)
                {
                    _eventActionTrees[index].UpdateAllItemDisplayNames();
                }
            }
        }

        private void RemoveEvents(List<int> toRemove)
        {
            int eventLength = _eventsProperty.arraySize;
            int newCount = eventLength - toRemove.Count;
            toRemove.Sort();
            
            // TODO update all other arrays here too :eyes:
            CyanTriggerActionTreeView.ActionInstanceRenderData[] tempRenderData =
                new CyanTriggerActionTreeView.ActionInstanceRenderData[newCount];
            CyanTriggerActionTreeView.ActionInstanceRenderData[] tempOptionData =
                new CyanTriggerActionTreeView.ActionInstanceRenderData[newCount];

            CyanTriggerActionTreeView[] tempActionTrees = new CyanTriggerActionTreeView[newCount];
            
            bool[] tempHiddenEvents = new bool[newCount];
            Dictionary<int, ReorderableList>[] tempEventActionInputLists = 
                new Dictionary<int, ReorderableList>[newCount];
            Dictionary<int, ReorderableList>[] tempEventInputLists = 
                new Dictionary<int, ReorderableList>[newCount];

            ReorderableList[] tempEventActionUserGateLists = new ReorderableList[newCount];
            var tempScopeTrees = new CyanTriggerEditorScopeTree[newCount];
            
            int itr = 0;
            for (int index = 0; index < eventLength; ++index)
            {
                if (itr < toRemove.Count && toRemove[itr] == index)
                {
                    _eventsProperty.DeleteArrayElementAtIndex(toRemove[itr]);
                    ++itr;
                    continue;
                }

                int nIndex = index - itr;
                tempRenderData[nIndex] = _eventInstanceRenderData[index];
                tempRenderData[nIndex].Property = _eventsProperty.GetArrayElementAtIndex(nIndex)
                    .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                tempOptionData[nIndex] = _eventOptionRenderData[index];
                
                tempActionTrees[nIndex] = _eventActionTrees[index];
                
                tempHiddenEvents[nIndex] = _hiddenEvents[index];
                tempEventActionInputLists[nIndex] = _eventActionInputLists[index];
                tempEventInputLists[nIndex] = _eventInputLists[index];

                tempEventActionUserGateLists[nIndex] = _eventActionUserGateLists[index];
                
                tempScopeTrees[nIndex] = _scopeTreeRoot[index];

                if (itr > 0)
                {
                    tempRenderData[nIndex]?.ClearInputLists();
                    tempOptionData[nIndex]?.ClearInputLists();
                    tempEventActionUserGateLists[nIndex] = null;
                    tempEventInputLists[nIndex]?.Clear();
                    tempEventActionInputLists[nIndex]?.Clear();
                }
            }
            
            _eventInstanceRenderData = tempRenderData;
            _eventOptionRenderData = tempOptionData;
            _eventActionTrees = tempActionTrees;
            _hiddenEvents = tempHiddenEvents;
            _eventActionInputLists = tempEventActionInputLists;
            _eventInputLists = tempEventInputLists;
            _eventActionUserGateLists = tempEventActionUserGateLists;
            _scopeTreeRoot = tempScopeTrees;

            _eventListSize = newCount;
            
            UpdateActionTreeViewProperties();
        }

        private void UpdateActionTreeViewProperties()
        {
            int eventLength = _eventsProperty.arraySize;
            for (int index = 0; index < eventLength; ++index)
            {
                UpdateOrCreateActionTreeForEvent(index);
            }

            UpdateAllTreeIndexCounts();
        }

        private void UpdateAllTreeIndexCounts()
        {
            int eventLength = _eventsProperty.arraySize;
            _variableTreeView.IdStartIndex = 0;
            int treeIndexCount = _variableTreeView.Size;
            for (int index = 0; index < eventLength; ++index)
            {
                if (_eventActionTrees[index] == null)
                {
                    continue;
                }
                _eventActionTrees[index].IdStartIndex = treeIndexCount;
                treeIndexCount += _eventActionTrees[index].Size;
            }
        }

        private void ResizeEventArrays(int newSize)
        {
            _eventListSize = newSize;
            Array.Resize(ref _eventInstanceRenderData, newSize);
            Array.Resize(ref _eventOptionRenderData, newSize);
            Array.Resize(ref _eventActionTrees, newSize);
            Array.Resize(ref _hiddenEvents, newSize);
            Array.Resize(ref _eventActionInputLists, newSize);
            Array.Resize(ref _eventInputLists, newSize);
            Array.Resize(ref _eventActionUserGateLists, newSize);
            Array.Resize(ref _scopeTreeRoot, newSize);

            UpdateActionTreeViewProperties();
        }

        private void SwapEventElements(List<int> toMoveUp)
        {
            foreach (int index in toMoveUp)
            {
                int prev = index - 1;
                _eventsProperty.MoveArrayElement(index, prev);

                SwapElements(_eventInstanceRenderData, index, prev);
                SwapElements(_eventOptionRenderData, index, prev);
                SwapElements(_eventActionTrees, index, prev);
                SwapElements(_hiddenEvents, index, prev);
                SwapElements(_eventActionInputLists, index, prev);
                SwapElements(_eventInputLists, index, prev);
                SwapElements(_scopeTreeRoot, index, prev);

                _eventInstanceRenderData[index].Property = _eventsProperty.GetArrayElementAtIndex(index)
                    .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                _eventInstanceRenderData[prev].Property = _eventsProperty.GetArrayElementAtIndex(prev)
                    .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                
                _eventActionUserGateLists[index] = null;
                _eventActionUserGateLists[prev] = null;
                
                _eventInstanceRenderData[index]?.ClearInputLists();
                _eventInstanceRenderData[prev]?.ClearInputLists();
                
                _eventOptionRenderData[index]?.ClearInputLists();
                _eventOptionRenderData[prev]?.ClearInputLists();
                
                _eventActionInputLists[index]?.Clear();
                _eventActionInputLists[prev]?.Clear();
                
                _eventInputLists[index]?.Clear();
                _eventInputLists[prev]?.Clear();
            }

            UpdateActionTreeViewProperties();
        }

        private static void SwapElements<T>(IList<T> array, int index1, int index2)
        {
            var temp = array[index1];
            array[index1] = array[index2];
            array[index2] = temp;
        }

        private void UpdateOrCreateActionTreeForEvent(int index)
        {
            var actionListProperty = _eventsProperty.GetArrayElementAtIndex(index)
                .FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
                
            List<CyanTriggerEditorVariableOption> GetVariableOptionsForEvent(Type type, int actionIndex)
            {
                return GetVariableOptions(type, index, actionIndex);
            }

            void OnEventActionsChanged()
            {
                OnActionsChanged(index);
            }
            
            if (_eventActionTrees[index] == null)
            {
                _eventActionTrees[index] = new CyanTriggerActionTreeView(
                    actionListProperty, 
                    OnEventActionsChanged, 
                    GetVariableOptionsForEvent);
                _eventActionTrees[index].ExpandAll();
            }
            else
            {
                var actionTree = _eventActionTrees[index];
                actionTree.Elements = actionListProperty;
                actionTree.GetVariableOptions = GetVariableOptionsForEvent;
                actionTree.OnActionChanged = OnEventActionsChanged;
            }
        }

        private void OnActionsChanged(int eventIndex)
        {
            if (eventIndex >= _scopeTreeRoot.Length || _scopeTreeRoot[eventIndex] == null)
            {
                return;
            }
            
            // Recalculate action variable inds
            var actionListProperty = _eventsProperty.GetArrayElementAtIndex(eventIndex)
                .FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
            _scopeTreeRoot[eventIndex].CreateStructure(actionListProperty);
        }

        private void OnVariableAddedOrRemoved()
        {
            _serializedObject.ApplyModifiedProperties();
            
            _resetVariableInputs = true;
        }

        private void AddEvent(CyanTriggerActionInfoHolder infoHolder)
        {
            _eventsProperty.arraySize++;
            SerializedProperty newEvent = _eventsProperty.GetArrayElementAtIndex(_eventsProperty.arraySize - 1);
            SerializedProperty eventInstance = newEvent.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            SetActionData(infoHolder, eventInstance);
            
            SerializedProperty actionInstances = newEvent.FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
            actionInstances.ClearArray();
            SerializedProperty eventOptions = newEvent.FindPropertyRelative(nameof(CyanTriggerEvent.eventOptions));
            SerializedProperty userGate = eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGate));
            userGate.intValue = 0;
            SerializedProperty userGateExtraData = 
                eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGateExtraData));
            userGateExtraData.ClearArray();
            SerializedProperty broadcast = eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.broadcast));
            broadcast.intValue = 0;
            SerializedProperty delay = eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.delay));
            delay.floatValue = 0;
            
            // TODO clear custom name as well

            ResizeEventArrays(_eventsProperty.arraySize);
            
            // TODO figure out duplicating events
        }

        private void SetActionData(CyanTriggerActionInfoHolder infoHolder, SerializedProperty actionProperty)
        {
            infoHolder.SetActionData(actionProperty);
        }

        private void UpdateUserVariableOptions()
        {
            _userVariableOptions.Clear();

            CyanTriggerEditorVariableOptionList allVariables = new CyanTriggerEditorVariableOptionList(typeof(CyanTriggerVariable));
            _userVariableOptions.Add(allVariables.Type, allVariables);

            void AddOptionToAllTypes(CyanTriggerEditorVariableOption option)
            {
                // Todo, figure if this breaks anything
                if (!option.IsReadOnly)
                {
                    allVariables.VariableOptions.Add(option);
                }

                Type t = option.Type;
                do
                {
                    if (!_userVariableOptions.TryGetValue(t, out CyanTriggerEditorVariableOptionList options))
                    {
                        options = new CyanTriggerEditorVariableOptionList(t);
                        _userVariableOptions.Add(t, options);
                    }

                    options.VariableOptions.Add(option);

                    if (t != typeof(object) && t.BaseType == null && option.Type.IsInterface)
                    {
                        t = typeof(object);
                    }
                    else
                    {
                        t = t.BaseType;
                    }
                } while (t != null && t != option.Type);
            }
            
            foreach (var variable in _cyanTriggerDataInstance.variables)
            {
                Type varType = variable.type.type;

                CyanTriggerEditorVariableOption option = new CyanTriggerEditorVariableOption
                    {ID = variable.variableID, Name = variable.name, Type = varType};
                AddOptionToAllTypes(option);
            }
            
            CyanTriggerEditorVariableOption thisGameObject = new CyanTriggerEditorVariableOption
            {
                ID = CyanTriggerAssemblyData.ThisGameObjectGUID, 
                Name = CyanTriggerAssemblyData.ThisGameObjectName, 
                Type = typeof(GameObject), 
                IsReadOnly = true
            };
            AddOptionToAllTypes(thisGameObject);
            
            CyanTriggerEditorVariableOption thisTransform = new CyanTriggerEditorVariableOption
            {
                ID = CyanTriggerAssemblyData.ThisTransformGUID, 
                Name = CyanTriggerAssemblyData.ThisTransformName, 
                Type = typeof(Transform), 
                IsReadOnly = true
            };
            AddOptionToAllTypes(thisTransform);
            
            CyanTriggerEditorVariableOption thisUdonBehaviour = new CyanTriggerEditorVariableOption
            {
                ID = CyanTriggerAssemblyData.ThisUdonBehaviourGUID, 
                Name = CyanTriggerAssemblyData.ThisUdonBehaviourName,
                Type = typeof(IUdonEventReceiver), 
                IsReadOnly = true
            };
            AddOptionToAllTypes(thisUdonBehaviour);
            
            CyanTriggerEditorVariableOption thisCyanTrigger = new CyanTriggerEditorVariableOption
            {
                ID = CyanTriggerAssemblyData.ThisCyanTriggerGUID, 
                Name = CyanTriggerAssemblyData.ThisCyanTriggerName, 
                Type = typeof(CyanTrigger), 
                IsReadOnly = true
            };
            AddOptionToAllTypes(thisCyanTrigger);
            
            CyanTriggerEditorVariableOption localPlayer = new CyanTriggerEditorVariableOption
            {
                ID = CyanTriggerAssemblyData.LocalPlayerGUID, 
                Name = CyanTriggerAssemblyData.LocalPlayerName, 
                Type = typeof(VRCPlayerApi), 
                IsReadOnly = true
            };
            AddOptionToAllTypes(localPlayer);
        }

        public List<CyanTriggerEditorVariableOption> GetVariableOptions(Type varType, int eventIndex, int actionIndex)
        {
            // TODO cache this better
            List<CyanTriggerEditorVariableOption> options = new List<CyanTriggerEditorVariableOption>();

            // Get event variables
            CyanTriggerActionInfoHolder curActionInfo = _eventInstanceRenderData[eventIndex].ActionInfo;
            foreach (var def in curActionInfo.GetVariableOptions())
            {
                if (def.Type.IsSubclassOf(varType) || def.Type == varType)
                {
                    options.Add(def);
                }
            }
            
            // Get user variables of this type
            if (_userVariableOptions.TryGetValue(varType, out var list))
            {
                options.AddRange(list.VariableOptions);
            }

            options.AddRange(_scopeTreeRoot[eventIndex].GetVariableOptions(varType, actionIndex).Reverse());
            
            // TODO add items that can be casted or tostring'ed

            return options;
        }

        public static string GetUniqueVariableName(string varName, string id, CyanTriggerVariable[] variables)
        {
            bool match;
            int count = 0;
            string varMatchName = varName;
            do
            {
                match = false;
                foreach (var variable in variables)
                {
                    if (variable.name == varMatchName && id != variable.variableID)
                    {
                        match = true;
                        ++count;
                        varMatchName = varName + count;
                        break;
                    }
                }
            } while (match);

            return varMatchName;
        }

        private void RenderHeader()
        {
            // EditorGUILayout.Space();
        }

        private void RenderExtraOptions()
        {
            bool renderAnimatorSettings = false;
            bool renderInteractSettings = false;
            
            // Add animation options
            if (_cyanTriggerSerializableInstance.udonBehaviour != null &&
                _cyanTriggerSerializableInstance.udonBehaviour.GetComponent<Animator>() != null)
            {
                renderAnimatorSettings = true;
            }

            foreach (var eventType in _cyanTriggerDataInstance.events)
            {
                string directEvent = eventType.eventInstance.actionType.directEvent;
                if (!string.IsNullOrEmpty(directEvent) && directEvent.Equals("Event_Interact"))
                {
                    renderInteractSettings = true;
                    break;
                }
            }

            if (renderAnimatorSettings || renderInteractSettings)
            {
                EditorGUILayout.BeginVertical(_style);

                // TODO come up with a better name here...
                EditorGUILayout.LabelField(new GUIContent("Other Settings", ""));
                
                if (renderAnimatorSettings)
                {
                    SerializedProperty applyAnimatorMoveProperty =
                        _dataInstanceProperty.FindPropertyRelative(
                            nameof(CyanTriggerDataInstance.applyAnimatorMove));
                    EditorGUILayout.PropertyField(applyAnimatorMoveProperty,AnimatorMoveContent);
                }
                
                
                if (renderInteractSettings)
                {
                    SerializedProperty interactTextProperty =
                        _serializedProperty.FindPropertyRelative(
                            nameof(CyanTriggerSerializableInstance.interactText));
                    SerializedProperty interactProximityProperty =
                        _serializedProperty.FindPropertyRelative(
                            nameof(CyanTriggerSerializableInstance.proximity));
                    
                    EditorGUILayout.PropertyField(interactTextProperty, InteractTextContent);

                    interactProximityProperty.floatValue = EditorGUILayout.Slider(ProximityContent,
                        interactProximityProperty.floatValue, 0f, 100f);
                }
                
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space();
            }
        }
        
        private void RenderVariables()
        {
            EditorGUILayout.BeginVertical(_style);
            
            _variableTreeView.DoLayoutTree();
            
            if (_variableDataProperty.arraySize != _variableTreeView.Size)
            {
                _resetVariableInputs = true;
            }

            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
        }

        private void RenderEvents()
        {
            int eventLength = _eventsProperty.arraySize;

            if (_eventListSize != eventLength)
            {
                if (_eventListSize < eventLength)
                {
                    ResizeEventArrays(eventLength);
                }
                else
                {
                    Debug.LogWarning("Event size does not match!" + _eventListSize +" " +eventLength);
                    ResetValues();
                }
            }

            UpdateAllTreeIndexCounts();

            List<int> toRemove = new List<int>();
            List<int> toMoveUp = new List<int>();

            for (int curEvent = 0; curEvent < eventLength; ++curEvent)
            {
                EditorGUILayout.BeginVertical(_style);

                SerializedProperty eventProperty = _eventsProperty.GetArrayElementAtIndex(curEvent);
                SerializedProperty eventInfo = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                
                if (_eventInstanceRenderData[curEvent] == null)
                {
                    _eventInstanceRenderData[curEvent] = new CyanTriggerActionTreeView.ActionInstanceRenderData
                    {
                        Property = eventInfo,
                        ActionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(eventInfo),
                    };
                    
                    // TODO, do this better?
                    _eventOptionRenderData[curEvent] = new CyanTriggerActionTreeView.ActionInstanceRenderData()
                    {
                        // Currently only for user gate
                        InputLists = new ReorderableList[1],
                        ExpandedInputs = new [] {true},
                    };
                }

                CyanTriggerActionInfoHolder curActionInfo = _eventInstanceRenderData[curEvent].ActionInfo;
                TriggerModifyAction modifyAction = RenderEventHeader(curEvent, eventProperty, curActionInfo);

                if (!_hiddenEvents[curEvent])
                {
                    RenderEventOptions(curEvent, eventProperty, curActionInfo);

                    EditorGUILayout.Space();

                    RenderEventActions(curEvent);
                }

                if (modifyAction == TriggerModifyAction.Delete)
                {
                    toRemove.Add(curEvent);
                }
                else if (modifyAction == TriggerModifyAction.MoveUp)
                {
                    toMoveUp.Add(curEvent);
                }
                else if (modifyAction == TriggerModifyAction.MoveDown)
                {
                    toMoveUp.Add(curEvent + 1);
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
            }

            if (toRemove.Count > 0)
            {
                RemoveEvents(toRemove);
            }

            if (toMoveUp.Count > 0)
            {
                SwapEventElements(toMoveUp);
            }
            
            RenderAddEventButton();
        }

        private TriggerModifyAction RenderEventHeader(
            int index, 
            SerializedProperty eventProperty,
            CyanTriggerActionInfoHolder actionInfo)
        {
            SerializedProperty eventInfo = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            
            Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(16f));

            Rect foldoutRect = new Rect(rect.x + 10, rect.y, 10, rect.height);
            _hiddenEvents[index] = !EditorGUI.Foldout(foldoutRect, !_hiddenEvents[index], GUIContent.none);
            
            float spaceBetween = 5;
            float initialSpace = foldoutRect.width + 10;
            float initialOffset = foldoutRect.xMax;

            float baseWidth = (rect.width - initialSpace - spaceBetween * 2) / 3.0f;
            float opButtonWidth = (baseWidth - 2 * spaceBetween) / 3.0f;

            // Draw modify buttons (move up, down, delete)
            TriggerModifyAction modifyAction = TriggerModifyAction.None;
            {
                Rect removeRect = new Rect(rect.xMax - opButtonWidth, rect.y, opButtonWidth, rect.height);
                Rect downRect = new Rect(removeRect.x - spaceBetween - opButtonWidth, rect.y, opButtonWidth,
                    rect.height);
                Rect upRect = new Rect(downRect.x - spaceBetween - opButtonWidth, rect.y, opButtonWidth, rect.height);

                EditorGUI.BeginDisabledGroup(index == 0);
                if (GUI.Button(upRect, "▲"))
                {
                    modifyAction = TriggerModifyAction.MoveUp;
                }

                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(index == _eventsProperty.arraySize - 1);
                if (GUI.Button(downRect, "▼"))
                {
                    modifyAction = TriggerModifyAction.MoveDown;
                }

                EditorGUI.EndDisabledGroup();

                if (GUI.Button(removeRect, "✖"))
                {
                    modifyAction = TriggerModifyAction.Delete;
                }
            }

            // Draw hidden event header
            if (_hiddenEvents[index])
            {
                GUILayout.Space(EditorGUIUtility.singleLineHeight);

                // TODO get custom name here
                
                float eventWidth = rect.width - initialOffset - opButtonWidth * 3 - spaceBetween * 2;
                Rect eventLabelRect = new Rect(initialOffset, rect.y, eventWidth, rect.height);

                string actionDisplayName = actionInfo.GetDisplayName();
                if (actionInfo.definition != null && actionInfo.definition.fullName.Equals("Event_Custom"))
                {
                    SerializedProperty nameProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.name));
                    string customName = string.IsNullOrEmpty(nameProperty.stringValue)
                        ? UnnamedCustomName
                        : nameProperty.stringValue;
                    actionDisplayName += $" \"{customName}\"";
                }
                
                EditorGUI.LabelField(eventLabelRect, actionDisplayName);
                
                EditorGUILayout.EndHorizontal();
                return modifyAction;
            }
            GUILayout.Space(EditorGUIUtility.singleLineHeight * 2);
            
            
            Rect typeRect = new Rect(initialOffset, rect.y, baseWidth, rect.height);
            Rect typeVariantRect = new Rect(typeRect.xMax + spaceBetween, rect.y, baseWidth, rect.height);

            bool valid = actionInfo.IsValid();
            if (GUI.Button(typeRect, actionInfo.GetDisplayName(), new GUIStyle(EditorStyles.popup)))
            {
                void UpdateEvent(CyanTriggerSettingsFavoriteItem newEventInfo)
                {
                    var data = newEventInfo.data;
                    var newActionInfo =
                        CyanTriggerActionInfoHolder.GetActionInfoHolder(data.guid, data.directEvent);
                    if (actionInfo.Equals(newActionInfo))
                    {
                        return;
                    }

                    SetActionData(newActionInfo, eventInfo);
                    _eventInstanceRenderData[index].ActionInfo = newActionInfo;
                }

                CyanTriggerSearchWindowManager.Instance.DisplayEventsFavoritesSearchWindow(UpdateEvent, true);
            }

            int variantCount = CyanTriggerActionGroupDefinitionUtil.GetEventVariantCount(actionInfo);
            EditorGUI.BeginDisabledGroup(!valid || variantCount <= 1);
            if (GUI.Button(typeVariantRect, actionInfo.GetVariantName(), new GUIStyle(EditorStyles.popup)))
            {
                GenericMenu menu = new GenericMenu();
                
                foreach (var actionVariant in CyanTriggerActionGroupDefinitionUtil.GetEventVariantInfoHolders(actionInfo))
                {
                    menu.AddItem(new GUIContent(actionVariant.GetVariantName()), false, (t) =>
                    {
                        var newActionInfo = (CyanTriggerActionInfoHolder) t;
                        SetActionData(newActionInfo, eventInfo);
                        _eventInstanceRenderData[index].ActionInfo = newActionInfo;
                    }, actionVariant);
                }
                
                menu.ShowAsContext();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            
            // Render gate, networking
            SerializedProperty eventOptions = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventOptions));
            SerializedProperty userGateProperty =
                eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGate));
            SerializedProperty broadcastProperty =
                eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.broadcast));
           

            Rect subHeaderRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20f));
            GUILayout.Space(20f);

            subHeaderRect.x += initialSpace;
            subHeaderRect.width -= initialSpace;

            float width = (subHeaderRect.width - spaceBetween * 2) / 3f;
            Rect gateRect = new Rect(subHeaderRect.x, subHeaderRect.y, width, subHeaderRect.height);
            Rect broadcastRect = new Rect(gateRect.xMax + spaceBetween, subHeaderRect.y, width, subHeaderRect.height);

            EditorGUI.PropertyField(gateRect, userGateProperty, GUIContent.none);
            
            string[] broadcastOptions = {"Local", "Send To Owner", "Send To All"};
            broadcastProperty.intValue = EditorGUI.Popup(broadcastRect, broadcastProperty.intValue, broadcastOptions);
            
            // TODO find something to put in this space. 
            
            EditorGUILayout.EndHorizontal();

            
            return modifyAction;
        }

        private void RenderEventOptions(
            int eventIndex, 
            SerializedProperty eventProperty, 
            CyanTriggerActionInfoHolder actionInfo)
        {
            SerializedProperty eventOptions = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventOptions));
            SerializedProperty delayProperty = eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.delay));
            SerializedProperty nameProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.name));
            SerializedProperty userGateProperty =
                eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGate));

            List<CyanTriggerEditorVariableOption> GetThisEventVariables(Type type)
            {
                return GetVariableOptions(type, eventIndex, -1);
            }
            
            if (userGateProperty.intValue == (int) CyanTriggerUserGate.UserAllowList ||
                userGateProperty.intValue == (int) CyanTriggerUserGate.UserDenyList)
            {
                SerializedProperty specificUserGateProperty =
                    eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGateExtraData));

                var definition = userGateProperty.intValue == (int) CyanTriggerUserGate.UserAllowList ?
                    AllowedUserGateVariableDefinition :
                    DeniedUserGateVariableDefinition;

                Rect rectRef = Rect.zero;
                CyanTriggerPropertyEditor.DrawActionVariableInstanceMultiInputEditor(
                    _eventOptionRenderData[eventIndex],
                    0,
                    specificUserGateProperty,
                    definition,
                    GetThisEventVariables,
                    ref rectRef,
                    true
                );
            }

            // TODO variable or const delay value
            EditorGUILayout.PropertyField(delayProperty,
                new GUIContent("Delay in Seconds",
                    "This event will be delayed for the given seconds before performing any actions."));

            // TODO align label width compared to propertyEditor width
            if (actionInfo.definition != null && actionInfo.definition.fullName.Equals("Event_Custom"))
            {
                EditorGUILayout.PropertyField(nameProperty, new GUIContent("Name", "The name of this event."));
                nameProperty.stringValue = CyanTriggerNameHelpers.SanitizeName(nameProperty.stringValue);
                if (string.IsNullOrEmpty(nameProperty.stringValue))
                {
                    nameProperty.stringValue = UnnamedCustomName;
                }
            }

            // TODO event inputs? using ActionInfo (Custom?)
            
            // TODO surround inputs in box to separate from actions and everything else
            // TODO allow collapsing event inputs?
            CyanTriggerPropertyEditor.DrawActionInstanceInputEditors(
                _eventInstanceRenderData[eventIndex],
                GetThisEventVariables, 
                Rect.zero, 
                true);


            // TODO clean up visuals here. This is kind of ugly
            CyanTriggerEditorVariableOption[] eventVariableOptions = actionInfo.GetVariableOptions();
            if (eventVariableOptions.Length > 0)
            {
                EditorGUILayout.BeginVertical(_style);
                // TODO add foldout
                EditorGUILayout.LabelField("Event Variables");

                foreach (var variable in eventVariableOptions)
                {
                    Rect variableRect = EditorGUILayout.BeginHorizontal();
                    GUIContent variableLabel = new GUIContent(
                        CyanTriggerNameHelpers.GetTypeFriendlyName(variable.Type) + " " + variable.Name);
                    Vector2 dim = GUI.skin.label.CalcSize(variableLabel);
                    variableRect.height = 16;
                    variableRect.x = variableRect.xMax - dim.x;
                    variableRect.width = dim.x;
                    EditorGUI.LabelField(variableRect, variableLabel);
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(variableRect.height);
                }

                EditorGUILayout.EndVertical();
            }

            // TODO Allow users to define custom event variables?
            // (Inline variables should be defined in the code rather than at the top...)
        }

        private void RenderEventActions(int eventIndex)
        {
            if (_eventActionTrees[eventIndex] == null)
            {
                Debug.LogWarning("Event action tree is null for event "+eventIndex);
                UpdateOrCreateActionTreeForEvent(eventIndex);
                _eventActionTrees[eventIndex].ExpandAll();
            }
            _eventActionTrees[eventIndex].DoLayoutTree();
        }
        
        private void RenderAddEventButton()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Event"))
            {
                void AddFavoriteEvent(CyanTriggerSettingsFavoriteItem newEventInfo)
                {
                    var data = newEventInfo.data;
                    AddEvent(CyanTriggerActionInfoHolder.GetActionInfoHolder(data.guid, data.directEvent));
                    _serializedObject.ApplyModifiedProperties();
                }

                CyanTriggerSearchWindowManager.Instance.DisplayEventsFavoritesSearchWindow(AddFavoriteEvent, true);
            }
            // TODO duplicate event option?
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }

        private enum TriggerModifyAction
        {
            None,
            Delete,
            MoveUp,
            MoveDown,
        }
    }
    
    public class CyanTriggerEditorVariableOption
    {
        public Type Type;
        public string Name;
        public string ID;
        public bool IsReadOnly;
    }

    public class CyanTriggerEditorVariableOptionList
    {
        public readonly Type Type;
        public readonly List<CyanTriggerEditorVariableOption> 
            VariableOptions = new List<CyanTriggerEditorVariableOption>();

        public CyanTriggerEditorVariableOptionList(Type t)
        {
            Type = t;
        }
    }

    public class CyanTriggerEditorScopeTree
    {
        private readonly List<CyanTriggerEditorVariableOption> _variableOptions =
            new List<CyanTriggerEditorVariableOption>();
        private readonly List<int> _prevIndex = new List<int>();
        private readonly List<int> _startIndex = new List<int>();
        public IEnumerable<CyanTriggerEditorVariableOption> GetVariableOptions(Type varType, int index)
        {
            if (index < 0 || index >= _startIndex.Count)
            {
                yield break;
            }
            int ind = _startIndex[index];

            while (ind != -1)
            {
                var variable = _variableOptions[ind];
                if (variable.Type.IsSubclassOf(varType) || variable.Type == varType)
                {
                    yield return variable;
                }
                
                ind = _prevIndex[ind];
            }
        }


        public void CreateStructure(SerializedProperty actionList)
        {
            _variableOptions.Clear();
            _prevIndex.Clear();
            _startIndex.Clear();

            Stack<int> lastScopes = new Stack<int>();
            int lastScopeIndex = -1;
            for (int i = 0; i < actionList.arraySize; ++i)
            {
                SerializedProperty actionProperty = actionList.GetArrayElementAtIndex(i);
                CyanTriggerActionInfoHolder actionInfo = 
                    CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);
                int scopeDelta = actionInfo.GetScopeDelta();

                if (scopeDelta > 0)
                {
                    lastScopes.Push(lastScopeIndex);
                }
                else if (scopeDelta < 0)
                {
                    lastScopeIndex = lastScopes.Pop();
                }
                
                _startIndex.Add(lastScopeIndex);
                
                var variables = actionInfo.GetCustomEditorVariableOptions(actionProperty);
                if (variables != null)
                {
                    foreach (var variable in variables)
                    {
                        _prevIndex.Add(lastScopeIndex);
                        lastScopeIndex = _variableOptions.Count;
                        
                        _variableOptions.Add(variable);
                    }
                }
            }
        }
    }
}
