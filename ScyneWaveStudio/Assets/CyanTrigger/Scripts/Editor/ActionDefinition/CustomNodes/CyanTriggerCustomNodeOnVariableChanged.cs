using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeOnVariableChanged : CyanTriggerCustomUdonEventNodeDefinition
    {
        public static string OnVariableChangedEventName = "Event_OnVariableChanged";

        // TODO don't hardcode
        private static string OnDeserializationEventMethodName = "_onDeserialization";

        public static UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "OnVariableChanged",
            OnVariableChangedEventName,
            typeof(void),
            new[]
            {
                new UdonNodeParameter()
                {
                    name = "variable",
                    type = typeof(CyanTriggerVariable),
                    parameterType = UdonNodeParameter.ParameterType.IN
                }
            },
            new string[0],
            new string[0],
            new object[0],
            true
        );

        public override UdonNodeDefinition GetNodeDefinition()
        {
            return NodeDefinition;
        }

        public override bool GetBaseMethod(
            CyanTriggerAssemblyProgram program,
            CyanTriggerActionInstance actionInstance,
            out CyanTriggerAssemblyMethod method)
        {
            var variable = program.data.GetUserDefinedVariable(actionInstance.inputs[0].variableID);
            string methodName = "OnVariableChanged_" + variable.name;
            return program.code.GetOrCreateMethod(methodName, true, out method);
        }

        public override void AddEventToProgram(CyanTriggerCompileState compileState)
        {
            AddDefaultEventToProgram(compileState.Program, compileState.EventMethod, compileState.ActionMethod);
        }

        public static HashSet<string> GetVariablesWithOnChangedCallback(CyanTriggerEvent[] events)
        {
            HashSet<string> variablesWithCallbacks = new HashSet<string>();
            foreach (var trigEvent in events)
            {
                var eventInstance = trigEvent.eventInstance;
                if (eventInstance.actionType.directEvent == OnVariableChangedEventName)
                {
                    variablesWithCallbacks.Add(eventInstance.inputs[0].variableID);
                }
            }

            return variablesWithCallbacks;
        }

        public static CyanTriggerAssemblyMethod HandleVariables(CyanTriggerAssemblyProgram program,
            CyanTriggerDataInstance cyanTriggerData)
        {
            bool hasSyncedVariableWithCallback = false;
            for (int curVar = 0; curVar < cyanTriggerData.variables.Length; ++curVar)
            {
                CyanTriggerAssemblyDataType variableData =
                    program.data.GetUserDefinedVariable(cyanTriggerData.variables[curVar].variableID);
                if (variableData.hasCallback && variableData.sync != CyanTriggerSyncMode.NotSynced)
                {
                    hasSyncedVariableWithCallback = true;
                    break;
                }
            }

            if (!hasSyncedVariableWithCallback)
            {
                return null;
            }

            program.code.GetOrCreateMethod(OnDeserializationEventMethodName, true, out var method);

            foreach (var variable in cyanTriggerData.variables)
            {
                CyanTriggerAssemblyDataType variableData = program.data.GetUserDefinedVariable(variable.variableID);
                if (variableData.hasCallback && variableData.sync != CyanTriggerSyncMode.NotSynced)
                {
                    method.AddActions(CyanTriggerAssemblyActionsUtils.OnVariableChangedCheck(program, variableData));
                }
            }

            return method;
        }
    }
}
