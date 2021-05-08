using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyanTrigger
{
    public class CyanTriggerCompiler
    {
        private readonly CyanTriggerDataInstance _cyanTriggerDataInstance;
        private readonly string _triggerHash;
        
        private readonly CyanTriggerAssemblyCode _code;
        private readonly CyanTriggerAssemblyData _data;
        private readonly CyanTriggerAssemblyProgram _program;

        private readonly HashSet<CyanTriggerActionGroupDefinition> _processedActionGroupDefinitions =
            new HashSet<CyanTriggerActionGroupDefinition>();
        private readonly Dictionary<CyanTriggerActionDefinition, CyanTriggerEventTranslation> _actionDefinitionTranslations =
            new Dictionary<CyanTriggerActionDefinition, CyanTriggerEventTranslation>();

        private readonly List<CyanTriggerAssemblyMethod> _cyanTriggerMethods = new List<CyanTriggerAssemblyMethod>();

        private readonly CyanTriggerProgramScopeData _programScopeData = new CyanTriggerProgramScopeData();

        private readonly Dictionary<Vector3Int, CyanTriggerAssemblyDataType> _refVariablesDataCache =
            new Dictionary<Vector3Int, CyanTriggerAssemblyDataType>();

        private readonly CyanTriggerDataReferences _variableReferences;

        // TODO add errors and warnings to udon behaviour
        private readonly List<string> _logWarningMessages = new List<string>();
        private readonly List<string> _logErrorMessages = new List<string>();
        
        
        public static bool CompileCyanTrigger(
            CyanTriggerDataInstance trigger,
            CyanTriggerProgramAsset triggerProgramAsset,
            string triggerHash = "")
        {
            try
            {
                if (trigger == null || trigger.variables == null || trigger.events == null)
                {
                    triggerProgramAsset.SetCompiledData("", "", null, null, null);
                    return false;
                }

                if (string.IsNullOrEmpty(triggerHash))
                {
                    triggerHash = CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(trigger);
                }
                new CyanTriggerCompiler(trigger, triggerHash).ApplyProgram(triggerProgramAsset);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message + "\n" + e.StackTrace);
                
                triggerProgramAsset.SetCompiledData("", "", null, null, null);
                
                return false;
            }
        }

        private CyanTriggerCompiler(CyanTriggerDataInstance trigger, string triggerHash)
        {
            _cyanTriggerDataInstance = trigger;
            _triggerHash = string.IsNullOrEmpty(triggerHash) ? 
                CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(_cyanTriggerDataInstance) :
                triggerHash;
            
            _code = new CyanTriggerAssemblyCode();
            _data = new CyanTriggerAssemblyData();
            _program = new CyanTriggerAssemblyProgram(_code, _data);
            _variableReferences = new CyanTriggerDataReferences();
            
            // Always create these first.
            _data.CreateSpecialAddressVariables();
            
            AddUserDefinedVariables();
            
            _data.AddThisVariables();
            
            AddExtraMethods();

            ProcessAllEventsAndActions();
            
            AddCyanTriggerEvents();
            
            Finish();
        }

        private void Finish()
        {
            _program.Finish();
            
            foreach (CyanTriggerAssemblyMethod method in _cyanTriggerMethods)
            {
                method?.PushMethodEndReturnJump(_data);
            }
            
            _program.ApplyAddresses();
        }
 
        public void ApplyProgram(CyanTriggerProgramAsset programAsset)
        {
            if (programAsset == null)
            {
                LogError("Cannot apply program for empty program asset");
                return;
            }
            
            // TODO add errors and warnings to program
            
            programAsset.SetCompiledData(
                _triggerHash, 
                _program.Export(), 
                _data.GetHeapDefaultValues(),
                _variableReferences,
                _cyanTriggerDataInstance);
        }

        private void LogWarning(string warning)
        {
            Debug.LogWarning(warning);
            _logWarningMessages.Add(warning);
        }

        private void LogError(string error)
        {
            Debug.LogError(error);
            _logErrorMessages.Add(error);
        }
        
        private void AddUserDefinedVariables()
        {
            HashSet<string> variablesWithCallbacks =
                CyanTriggerCustomNodeOnVariableChanged.GetVariablesWithOnChangedCallback(_cyanTriggerDataInstance.events);
               
            foreach (var variable in _cyanTriggerDataInstance.variables)
            {
                bool hasCallback = variablesWithCallbacks.Contains(variable.variableID);
                _data.AddUserDefinedVariable(variable.name, variable.variableID, variable.type.type, variable.sync, hasCallback);
                
                _variableReferences.userVariables.Add(variable.name, variable.type.type);
            }

            var method = CyanTriggerCustomNodeOnVariableChanged.HandleVariables(_program, _cyanTriggerDataInstance);
            if (method != null)
            {
                AddMethod(method);
            }
        }

        private void AddExtraMethods()
        {
            if (_cyanTriggerDataInstance.applyAnimatorMove)
            {
                CyanTriggerAssemblyMethod udonMethod = GetOrAddMethod("_onAnimatorMove");
                CyanTriggerAssemblyDataType animatorVar = _program.data.CreateReferenceVariable(typeof(Animator));
                _variableReferences.animatorSymbol = animatorVar.name;
                udonMethod.AddActions(CyanTriggerAssemblyActionsUtils.OnAnimatorMove(_program, animatorVar));
            }

            // Get the local player in start
            {
                CyanTriggerAssemblyMethod udonMethod = GetOrAddMethod("_start");
                udonMethod.AddActions(CyanTriggerAssemblyActionsUtils.GetLocalPlayer(_program));
            }
        }

        // TODO figure out instance version of actions/events work
        private void ProcessAllEventsAndActions()
        {
            // Go through all events and actions and merge unique programs in.
            foreach (var trigEvent in _cyanTriggerDataInstance.events)
            {
                var eventType = trigEvent.eventInstance.actionType;
                if (!string.IsNullOrEmpty(eventType.guid) &&
                    CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(
                    eventType.guid, out var actionGroupDefinition))
                {
                    ProcessActionDefinition(actionGroupDefinition);
                }

                foreach (var actionInstance in trigEvent.actionInstances)
                {
                    var actionType = actionInstance.actionType;
                    if (!string.IsNullOrEmpty(actionType.guid) &&
                        CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(
                        actionType.guid, out actionGroupDefinition))
                    {
                        ProcessActionDefinition(actionGroupDefinition);
                    }
                }
            }
        }

        private CyanTriggerAssemblyMethod GetOrAddMethod(string baseEvent)
        {
            if (_code.GetOrCreateMethod(baseEvent, true, out var method))
            {
                _data.GetEventVariables(baseEvent);
                AddMethod(method);
            }

            return method;
        }
        
        private void AddMethod(CyanTriggerAssemblyMethod method)
        {
            method.PushInitialEndVariable(_data);
            _cyanTriggerMethods.Add(method);
            _code.AddMethod(method);
        }

        private void AddCyanTriggerEvents()
        {
            var events = _cyanTriggerDataInstance.events;
            for (int curEvent = 0; curEvent < events.Length; ++curEvent)
            {
                CyanTriggerEvent trigEvent = events[curEvent];
                CyanTriggerActionInstance eventAction = trigEvent.eventInstance;
                
                // Add event itself to the scope stack. This way local variables can be added properly
                _programScopeData.Clear();
                _programScopeData.ScopeStack.Push(new CyanTriggerScopeFrame(null, eventAction));
                
                // TODO
                // if (!eventAction.active) 
                // {
                //     continue;
                // }

                // Get base action for event
                CyanTriggerAssemblyMethod udonMethod = GetOrCreateMethodForBaseAction(eventAction, trigEvent.name);

                CyanTriggerEventOptions eventOptions = trigEvent.eventOptions;

                // Only add special event gate if it is not anyone gating. 
                CyanTriggerAssemblyMethod gatedMethod = udonMethod;
                if (eventOptions.userGate != CyanTriggerUserGate.Anyone)
                {
                    gatedMethod =
                        new CyanTriggerAssemblyMethod($"__intern_event_{curEvent}_gated", false);
                    AddMethod(gatedMethod);
                
                    // add gate checks
                    udonMethod.AddActions(
                        CyanTriggerAssemblyActionsUtils.EventUserGate(
                            _program, 
                            gatedMethod.name,
                            eventOptions.userGate, 
                            eventOptions.userGateExtraData));
                }
                
                CyanTriggerAssemblyMethod actionsMethod =
                    new CyanTriggerAssemblyMethod($"__intern_event_{curEvent}_actions", false);
                AddMethod(actionsMethod);
                
                // Call to event with call to action method
                CallEventAction(curEvent, eventAction, gatedMethod, actionsMethod);

                // Add network call
                if (eventOptions.broadcast != CyanTriggerBroadcast.Local)
                {
                    CyanTriggerAssemblyMethod networkedActionsMethod =
                        new CyanTriggerAssemblyMethod($"intern_event_{curEvent}_networked_actions", true);
                    AddMethod(networkedActionsMethod);
                    
                    actionsMethod.AddActions(CyanTriggerAssemblyActionsUtils.EventBroadcast(
                        _program,
                        networkedActionsMethod.name,
                        eventOptions.broadcast));
                    
                    actionsMethod = networkedActionsMethod;
                }
                
                // add delay to action method
                if (eventOptions.delay > 0)
                {
                    CyanTriggerAssemblyMethod delayMethod =
                        new CyanTriggerAssemblyMethod($"__intern_event_{curEvent}_delayed_actions", true);
                    AddMethod(delayMethod);
                    
                    actionsMethod.AddActions(CyanTriggerAssemblyActionsUtils.SendToTimerQueue(
                        _program,
                        delayMethod.name,
                        eventOptions.delay));

                    actionsMethod = delayMethod;
                }
                
                // TODO Initialize all temp variables
                
                AddCyanTriggerEventsActionsInList(curEvent, trigEvent.actionInstances, actionsMethod);
            }
        }

        private void AddCyanTriggerEventsActionsInList(
            int eventIndex,
            CyanTriggerActionInstance[] actionInstances, 
            CyanTriggerAssemblyMethod actionMethod)
        {
            for (int curAction = 0; curAction < actionInstances.Length; ++curAction)
            {
                CyanTriggerActionInstance actionInstance = actionInstances[curAction];
                
                // TODO
                // if (!actionInstance.active) 
                // {
                //     continue;
                // }

                var valid = actionInstance.IsValid();
                if (valid != CyanTriggerUtil.InvalidReason.Valid)
                {
                    CyanTriggerActionInfoHolder info = CyanTriggerActionInfoHolder.GetActionInfoHolder(actionInstance.actionType.guid, actionInstance.actionType.directEvent);
                    // TODO give better information
                    LogWarning($"Event[{eventIndex}].Action[{curAction}] {info.GetActionRenderingDisplayName()} is invalid {valid}!");
                    continue;
                }
                
                CallAction(eventIndex, curAction, actionInstance, actionMethod);
            }
        }

        private CyanTriggerAssemblyMethod GetOrCreateMethodForBaseAction(CyanTriggerActionInstance action, string customName)
        {
            var actionType = action.actionType;
            if (CyanTriggerNodeDefinitionManager.TryGetCustomDefinition(actionType.directEvent, out var customDefinition))
            {
                if (customDefinition.GetBaseMethod(_program, action, out var customMethod))
                {
                    AddMethod(customMethod);
                }
                
                return customMethod;
            }
            
            string baseEvent = actionType.directEvent;
            if (!string.IsNullOrEmpty(actionType.guid))
            {
                if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionDefinition(actionType.guid,
                    out CyanTriggerActionDefinition actionDefinition))
                {
                    LogError($"Action Definition GUID is not valid! {actionType.guid}");
                    return null;
                }

                baseEvent = actionDefinition.baseEventName;
                customName = actionDefinition.eventEntry;
            }
            
            CyanTriggerNodeDefinition nodeDefinition = CyanTriggerNodeDefinitionManager.GetDefinition(baseEvent);
            if (nodeDefinition == null)
            {
                LogError("Base event is not a valid event! " + baseEvent);
                return null;
            }

            if (baseEvent == "Event_Custom")
            {
                baseEvent = customName;
            }
            else
            {
                baseEvent = "_" + char.ToLower(baseEvent[6]) + baseEvent.Substring(7);
            }

            return GetOrAddMethod(baseEvent);
        }

        private void CallEventAction(
            int eventIndex,
            CyanTriggerActionInstance actionInstance, 
            CyanTriggerAssemblyMethod eventMethod,
            CyanTriggerAssemblyMethod actionMethod)
        {
            var actionType = actionInstance.actionType;
            if (!string.IsNullOrEmpty(actionType.directEvent))
            {
                HandleDirectActionForEvents(eventIndex, actionInstance, eventMethod, actionMethod);
                return;
            }
            
            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionDefinition(
                actionType.guid, out CyanTriggerActionDefinition actionDefinition))
            {
                LogError("Action Definition GUID is not valid! " + actionType.guid);
                return;
            }

            var actionTranslation = GetActionTranslation(actionType.guid, actionDefinition);
            if (actionTranslation == null)
            {
                LogError("Action translation is missing! " + actionDefinition.FullName());
                return;
            }

            AddEventJumpToActionVariableCopy(eventMethod, actionMethod, actionTranslation);

            CallAction(eventIndex, -1, actionInstance, eventMethod, actionTranslation, actionDefinition.variables);
        }

        private void CallAction(
            int eventIndex,
            int actionIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod)
        {
            var actionType = actionInstance.actionType;
            if (!string.IsNullOrEmpty(actionType.directEvent))
            {
                HandleDirectAction(eventIndex, actionIndex, actionInstance, actionMethod);
                return;
            }
            _programScopeData.PreviousScopeDefinition = null;
            
            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionDefinition(
                actionType.guid, out CyanTriggerActionDefinition actionDefinition))
            {
                LogError("Action Definition GUID is not valid! " + actionType.guid);
                return;
            }

            var actionTranslation = GetActionTranslation(actionType.guid, actionDefinition);
            if (actionTranslation == null)
            {
                LogError("Action translation is missing! " + actionDefinition.FullName());
                return;
            }
            
            CallAction(eventIndex, actionIndex, actionInstance, actionMethod, actionTranslation, actionDefinition.variables);
        }

        private void AddEventJumpToActionVariableCopy(
            CyanTriggerAssemblyMethod eventMethod,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerEventTranslation actionTranslation)
        {
            // Set the action jump method
            var actionJumpLoc = _data.CreateMethodReturnVar(actionMethod.actions[0]);
            eventMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(
                actionJumpLoc,
                _data.GetVariableNamed(actionTranslation.ActionJumpVariableName)));
        }

        private void HandleDirectActionForEvents(
            int eventIndex,
            CyanTriggerActionInstance actionInstance, 
            CyanTriggerAssemblyMethod eventMethod,
            CyanTriggerAssemblyMethod actionMethod)
        {
            var actionType = actionInstance.actionType;
            if (CyanTriggerNodeDefinitionManager.TryGetCustomDefinition(actionType.directEvent, out var customDefinition))
            {
                customDefinition.AddEventToProgram(new CyanTriggerCompileState
                {
                    Program = _program,
                    ScopeData = _programScopeData,
                    ActionInstance = actionInstance,
                    EventMethod = eventMethod,
                    ActionMethod = actionMethod,
                    
                    GetDataFromVariableInstance = (multiVarIndex, varIndex, variableInstance, type, output) => 
                        GetDataFromVariableInstance(eventIndex, -1, multiVarIndex, varIndex, variableInstance, type, output),
                });
                return;
            }
            
            eventMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program, actionMethod.name));
        }
        
        private void HandleDirectAction(
            int eventIndex,
            int actionIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod)
        {
            var actionType = actionInstance.actionType;
            if (CyanTriggerNodeDefinitionManager.TryGetCustomDefinition(actionType.directEvent, out var customDefinition))
            {
                if (customDefinition.CreatesScope())
                {
                    var scopeFrame = new CyanTriggerScopeFrame(customDefinition, actionInstance);
                    _programScopeData.ScopeStack.Push(scopeFrame);
                } 
                
                if (customDefinition is CyanTriggerCustomNodeVariableProvider variableProvider)
                {
                    _programScopeData.AddVariableOptions(_program, actionInstance, variableProvider);
                }

                var compileState = new CyanTriggerCompileState
                {
                    Program = _program,
                    ScopeData = _programScopeData,
                    ActionInstance = actionInstance,
                    ActionMethod = actionMethod,

                    GetDataFromVariableInstance = (multiVarIndex, varIndex, variableInstance, type, output) =>
                        GetDataFromVariableInstance(eventIndex, actionIndex, multiVarIndex, varIndex, variableInstance,
                            type, output),
                };
                
                customDefinition.AddActionToProgram(compileState);

                // End scope, cleanup stack item
                if (customDefinition is CyanTriggerCustomNodeBlockEnd)
                {
                    var lastScope = _programScopeData.ScopeStack.Peek();
                    compileState.ActionInstance = lastScope.ActionInstance;
                    lastScope.Definition.HandleEndScope(compileState);
                    _programScopeData.PopScope(_program);
                    
                    // TODO verify next definition too? Needed for condition to expect condition body
                }
                
                return;
            }
            _programScopeData.PreviousScopeDefinition = null;
            
            CyanTriggerNodeDefinition nodeDef = CyanTriggerNodeDefinitionManager.GetDefinition(actionType.directEvent);
            if (nodeDef == null)
            {
                LogError("No definition found for action name: "+actionType.directEvent);
                return;
            }
            
            if (nodeDef.variableDefinitions.Length > 0 && 
                (nodeDef.variableDefinitions[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
            {
                for (int curInput = 0; curInput < actionInstance.multiInput.Length; ++curInput)
                {
                    HandleDirectActionSingle(eventIndex, actionIndex, curInput, actionInstance, actionMethod, nodeDef.variableDefinitions);
                }
            }
            else
            {
                HandleDirectActionSingle(eventIndex, actionIndex, -1, actionInstance, actionMethod, nodeDef.variableDefinitions);
            }
        }

        private void CallAction(
            int eventIndex,
            int actionIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerEventTranslation actionTranslation,
            CyanTriggerActionVariableDefinition[] variableDefinitions)
        {
            if (string.IsNullOrEmpty(actionTranslation.TranslatedAction.TranslatedName))
            {
                LogError($"Event[{eventIndex}].Action[{actionIndex}] Translation name is null");
                return;
            }
            
            if (variableDefinitions.Length > 0 && 
                (variableDefinitions[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
            {
                for (int curInput = 0; curInput < actionInstance.multiInput.Length; ++curInput)
                {
                    CallActionSingle(eventIndex, actionIndex, curInput, actionInstance, actionMethod, actionTranslation, variableDefinitions);
                }
            }
            else
            {
                CallActionSingle(eventIndex, actionIndex, -1, actionInstance, actionMethod, actionTranslation, variableDefinitions);
            }
        }

        private void HandleDirectActionSingle(
            int eventIndex,
            int actionIndex,
            int multiVarIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerActionVariableDefinition[] variableDefinitions)
        {
            var actionType = actionInstance.actionType;
            for (int curVar = 0; curVar < actionInstance.inputs.Length; ++curVar)
            {
                var def = variableDefinitions[curVar];
                var input = (curVar == 0 && multiVarIndex != -1) ?
                    actionInstance.multiInput[multiVarIndex] :
                    actionInstance.inputs[curVar];

                var variable = GetDataFromVariableInstance(
                    eventIndex, 
                    actionIndex, 
                    multiVarIndex, 
                    curVar, 
                    input,
                    def.type.type,
                    false);
                actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(variable));
            }

            // TODO Remove now that "Set_" has been created?
            if (actionType.directEvent.StartsWith("Const_"))
            {
                actionMethod.AddAction(CyanTriggerAssemblyInstruction.Copy());
            }
            else
            {
                actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(actionType.directEvent));
            }
            
            
            for (int curVar = 0; curVar < actionInstance.inputs.Length; ++curVar)
            {
                var def = variableDefinitions[curVar];
                if ((def.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) == 0)
                {
                    continue;
                }
                
                var input = (curVar == 0 && multiVarIndex != -1) ?
                    actionInstance.multiInput[multiVarIndex] :
                    actionInstance.inputs[curVar];
                
                CyanTriggerAssemblyDataType srcVariable = GetOutputDataFromVariableInstance(_data, input);
                if (srcVariable == null)
                {
                    continue;
                }
                
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.OnVariableChangedCheck(_program, srcVariable));
            }
        }
        
        private void CallActionSingle(
            int eventIndex,
            int actionIndex,
            int multiVarIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerEventTranslation actionTranslation,
            CyanTriggerActionVariableDefinition[] variableDefinitions)
        {
            if (string.IsNullOrEmpty(actionTranslation.TranslatedAction.TranslatedName))
            {
                LogError($"Event[{eventIndex}].Action[{actionIndex}] Translation name is null");
                return;
            }
            
            
            // Copy event specific variable data
            foreach (var variable in actionTranslation.EventTranslatedVariables)
            {
                CyanTriggerAssemblyDataType srcVariable = _data.GetVariableNamed(variable.BaseName);
                CyanTriggerAssemblyDataType destVariable = _data.GetVariableNamed(variable.TranslatedName);
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(srcVariable, destVariable));
            }
            
            // Copy user variable data
            for (int curVar = 0; curVar < actionInstance.inputs.Length; ++curVar)
            {
                var def = variableDefinitions[curVar];
                var input = (curVar == 0 && multiVarIndex != -1) ?
                    actionInstance.multiInput[multiVarIndex] :
                    actionInstance.inputs[curVar];
                
                CyanTriggerAssemblyDataType srcVariable = GetDataFromVariableInstance(
                    eventIndex, 
                    actionIndex, 
                    multiVarIndex, 
                    curVar, 
                    input,
                    def.type.type,
                    false);
                
                CyanTriggerAssemblyDataType destVariable =
                    _data.GetVariableNamed(actionTranslation.TranslatedVariables[curVar].TranslatedName);
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(srcVariable, destVariable));
            }
            
            
            // Call method itself
            actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program,
                actionTranslation.TranslatedAction.TranslatedName));
            

            // Copy saved variables back
            for (int curVar = 0; curVar < actionInstance.inputs.Length; ++curVar)
            {
                var def = variableDefinitions[curVar];
                if ((def.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) == 0)
                {
                    continue;
                }

                var input = (curVar == 0 && multiVarIndex != -1) ?
                    actionInstance.multiInput[multiVarIndex] :
                    actionInstance.inputs[curVar];
                
                CyanTriggerAssemblyDataType destVariable = GetOutputDataFromVariableInstance(_data, input);
                if (destVariable == null)
                {
                    continue;
                }
                CyanTriggerAssemblyDataType srcVariable =
                    _data.GetVariableNamed(actionTranslation.TranslatedVariables[curVar].TranslatedName);
                
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(srcVariable, destVariable));
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.OnVariableChangedCheck(_program, destVariable));
            }
        }

        private CyanTriggerAssemblyDataType GetDataFromVariableInstance(
            int eventIndex,
            int actionIndex,
            int multiVarIndex,
            int varIndex,
            CyanTriggerActionVariableInstance input, 
            Type type, 
            bool outputOnly)
        {
            if (outputOnly)
            {
                var variable = GetOutputDataFromVariableInstance(_program.data, input);
                if (variable == null)
                {
                    variable = _program.data.RequestTempVariable(type);
                    _program.data.ReleaseTempVariable(variable);
                }
                return variable;
            }

            if (input.isVariable)
            {
                return GetInputDataFromVariableInstance(_program.data, input, type);
            }

            bool isMulti = varIndex == 0 && multiVarIndex != -1;
            
            // Is a constant value. Create a reference for this variable so that data
            // is not in the code directly, allowing program reuse.
            
            Vector3Int cacheIndex = new Vector3Int(eventIndex, actionIndex, varIndex);
            // Do not hash multi vars as those will never repeat.
            // Check cache first before creating a new reference
            if (!isMulti && _refVariablesDataCache.TryGetValue(cacheIndex, out var cachedData))
            {
                return cachedData;
            }
            
            // TODO do not pass in the data here. Ensure that public variables are properly updated in the program asset
            var varData = _program.data.CreateReferenceVariable(type);

            // Add variable to the list of exported variables.
            _variableReferences.ActionDataIndices.Add(new CyanTriggerActionDataReferenceIndex
            {
                eventIndex = eventIndex,
                actionIndex = actionIndex,
                multiVariableIndex = isMulti ? multiVarIndex : -1,
                variableIndex = varIndex,
                symbolName = varData.name,
                symbolType = type,
            });
            
            if (!isMulti)
            {
                _refVariablesDataCache.Add(cacheIndex, varData);
            }
                
            return varData;
        }
        
        public static CyanTriggerAssemblyDataType GetInputDataFromVariableInstance(
            CyanTriggerAssemblyData data,
            CyanTriggerActionVariableInstance input, 
            Type type)
        {
            if (!input.isVariable)
            {
                // Try to minimize the usage of this as this is defined in the program itself...
                return data.GetOrCreateVariableConstant(type, input.data.obj, false);
            }

            // TODO remove
            if (input.variableID != null && input.variableID.StartsWith("this_"))
            {
                input.variableID = "_" + input.variableID;
            }
            
            // These methods should automatically verify if the variable exists.
            if (input.variableID != null && CyanTriggerAssemblyData.IsIdThisVariable(input.variableID))
            {
                return data.GetThisConst(type, input.variableID);
            }
            
            if (!string.IsNullOrEmpty(input.variableID))
            {
                return data.GetUserDefinedVariable(input.variableID);
            }

            if (!string.IsNullOrEmpty(input.name))
            {
                return data.GetVariableNamed(input.name);
            }
            
            // Variable is missing. Provide a temporary one to ignore the data.
            var variable = data.RequestTempVariable(type);
            data.ReleaseTempVariable(variable);
            return variable;
        }
        
        private static CyanTriggerAssemblyDataType GetOutputDataFromVariableInstance(
            CyanTriggerAssemblyData data,
            CyanTriggerActionVariableInstance input)
        {
            if (!input.isVariable)
            {
                Debug.LogWarning("Trying to copy from a constant value");
                return null;
            }
            if (string.IsNullOrEmpty(input.variableID))
            {
                Debug.LogWarning("Output Variable is missing");
                return null;
            }
            
            // TODO remove
            if (input.variableID != null && input.variableID.StartsWith("this_"))
            {
                input.variableID = "_" + input.variableID;
            }
            
            if (CyanTriggerAssemblyData.IsIdThisVariable(input.variableID))
            {
                Debug.LogWarning("Cannot use this with output variables");
                return null;
            }
            
            // This should automatically verify if the variable exists.
            return data.GetUserDefinedVariable(input.variableID);
        }

        private CyanTriggerEventTranslation GetActionTranslation(
            string actionGuid,
            CyanTriggerActionDefinition actionDefinition)
        {
            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(
                actionGuid, out var actionGroupDefinition))
            {
                return null; 
            }

            ProcessActionDefinition(actionGroupDefinition);
            if (!_actionDefinitionTranslations.TryGetValue(actionDefinition, out var actionTranslation))
            {
                return null;
            }

            return actionTranslation;
        }
        
        private void ProcessActionDefinition(CyanTriggerActionGroupDefinition actionGroupDefinition)
        {
            if (actionGroupDefinition == null)
            {
                return;
            }

            if (_processedActionGroupDefinitions.Contains(actionGroupDefinition))
            {
                return;
            }
            
            CyanTriggerAssemblyProgram program = actionGroupDefinition.GetCyanTriggerAssemblyProgram();
            if (program == null)
            {
                LogWarning("Program is null for action group! " + actionGroupDefinition.name);
                return;
            }
            
            _processedActionGroupDefinitions.Add(actionGroupDefinition);
            
            CyanTriggerAssemblyProgram actionProgram = program.Clone();
            CyanTriggerAssemblyProgramUtil.ProcessProgramForCyanTriggers(actionProgram);

            CyanTriggerProgramTranslation programTranslation =
                CyanTriggerAssemblyProgramUtil.AddNamespace(
                    actionProgram, 
                    "_action_group_" + _actionDefinitionTranslations.Count);

            Dictionary<string, CyanTriggerItemTranslation> methodMap = 
                new Dictionary<string, CyanTriggerItemTranslation>();
            Dictionary<string, CyanTriggerItemTranslation> variableMap = 
                new Dictionary<string, CyanTriggerItemTranslation>();

            foreach (var method in programTranslation.TranslatedMethods)
            {
                methodMap.Add(method.BaseName, method);
            }
            foreach (var variable in programTranslation.TranslatedVariables)
            {
                variableMap.Add(variable.BaseName, variable);
            }
            
            _program.MergeProgram(actionProgram);
            
            foreach (var action in actionGroupDefinition.exposedActions)
            {
                CyanTriggerEventTranslation eventTranslation = new CyanTriggerEventTranslation();
                _actionDefinitionTranslations.Add(action, eventTranslation);
                
                eventTranslation.TranslatedAction = methodMap[action.eventEntry];
                eventTranslation.TranslatedVariables = new CyanTriggerItemTranslation[action.variables.Length];

                for (int cur = 0; cur < action.variables.Length; ++cur)
                {
                    eventTranslation.TranslatedVariables[cur] = variableMap[action.variables[cur].udonName];
                }
                
                eventTranslation.ActionJumpVariableName = variableMap[
                    CyanTriggerAssemblyData.GetSpecialVariableName(CyanTriggerAssemblyData
                        .CyanTriggerSpecialVariableName.ActionJumpAddress)].TranslatedName;

                List<CyanTriggerItemTranslation> eventInputTranslation = new List<CyanTriggerItemTranslation>();
                var eventVariables = CyanTriggerAssemblyData.GetEventVariableTypes(action.baseEventName);
                if (eventVariables != null)
                {
                    foreach (var variable in eventVariables)
                    {
                        eventInputTranslation.Add(variableMap[variable.Item1]);
                    }
                }
                
                eventTranslation.EventTranslatedVariables = eventInputTranslation.ToArray();
            }
        }
    }

    public class CyanTriggerScopeFrame
    {
        public CyanTriggerAssemblyInstruction StartNop;
        public CyanTriggerAssemblyInstruction EndNop;
        public readonly CyanTriggerCustomUdonNodeDefinition Definition;
        public readonly CyanTriggerActionInstance ActionInstance;
        public readonly bool IsLoop;
        public readonly List<CyanTriggerEditorVariableOption> ScopeVariables = 
            new List<CyanTriggerEditorVariableOption>();

        public CyanTriggerScopeFrame(
            CyanTriggerCustomUdonNodeDefinition definition,
            CyanTriggerActionInstance actionInstance)
        {
            Definition = definition;
            ActionInstance = actionInstance;

            IsLoop = definition is ICyanTriggerCustomNodeLoop;
        }

        public void AddVariables(
            CyanTriggerAssemblyProgram program,
            CyanTriggerEditorVariableOption[] variableOptions)
        {
            foreach (var variable in variableOptions)
            {
                program.data.AddUserDefinedVariable(
                    variable.Name, 
                    variable.ID,
                    variable.Type, 
                    CyanTriggerSyncMode.NotSynced,
                    false);
                program.data.GetUserDefinedVariable(variable.ID).export = false;
                
                ScopeVariables.Add(variable);
            }
        }
    }

    public class CyanTriggerProgramScopeData
    {
        public readonly Stack<CyanTriggerScopeFrame> ScopeStack = new Stack<CyanTriggerScopeFrame>();
        public CyanTriggerCustomUdonNodeDefinition PreviousScopeDefinition;

        public void Clear()
        {
            ScopeStack.Clear();
            PreviousScopeDefinition = null;
        }
        
        public bool VerifyPreviousScope()
        {
            // TODO
            return true;
        }

        public void AddVariableOptions(
            CyanTriggerAssemblyProgram program, 
            CyanTriggerActionInstance actionInstance,
            CyanTriggerCustomNodeVariableProvider variableProvider)
        {
            var scopeFrame = ScopeStack.Peek();
            var variableOptions = variableProvider.GetCustomEditorVariableOptions(program, actionInstance.inputs);
            scopeFrame.AddVariables(program, variableOptions);
        }

        public CyanTriggerScopeFrame PopScope(CyanTriggerAssemblyProgram program)
        {
            var scopeFrame = ScopeStack.Pop();

            foreach (var variable in scopeFrame.ScopeVariables)
            {
                program.data.RemoveUserDefinedVariable(variable.ID);
            }

            PreviousScopeDefinition = scopeFrame.Definition;
            return scopeFrame;
        }
    }
    
    public class CyanTriggerItemTranslation
    {
        public string BaseName;
        public string TranslatedName;
    }

    public class CyanTriggerEventTranslation
    {
        public string ActionJumpVariableName;
        public CyanTriggerItemTranslation TranslatedAction;
        public CyanTriggerItemTranslation[] TranslatedVariables;
        public CyanTriggerItemTranslation[] EventTranslatedVariables;
    }
    
    public class CyanTriggerProgramTranslation
    {
        public CyanTriggerItemTranslation[] TranslatedMethods;
        public CyanTriggerItemTranslation[] TranslatedVariables;
    }
    
    public class CyanTriggerCompileState
    {
        public CyanTriggerAssemblyProgram Program;
        public CyanTriggerProgramScopeData ScopeData;
        public CyanTriggerActionInstance ActionInstance;
        public CyanTriggerAssemblyMethod EventMethod;
        public CyanTriggerAssemblyMethod ActionMethod;

        // multi index, variable index, variable instance, expected type
        public Func<int, int, CyanTriggerActionVariableInstance, Type, bool, CyanTriggerAssemblyDataType> 
            GetDataFromVariableInstance;
    }
}