using System;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeVariable : CyanTriggerCustomNodeVariableProvider
    {
        public readonly Type Type;
        private readonly UdonNodeDefinition _definition;

        public CyanTriggerCustomNodeVariable(Type type)
        {
            Type = type;
            string friendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(Type);
            string fullName = CyanTriggerNameHelpers.SanitizeName(Type.FullName);
            if (type.IsArray)
            {
                fullName += "Array";
            }
            
            _definition = new UdonNodeDefinition(
                "Variable " + friendlyName,
                "CyanTriggerVariable_" + fullName,
                Type,
                new []
                {
                    new UdonNodeParameter
                    {
                        name = "Value",
                        type = Type,
                        parameterType = UdonNodeParameter.ParameterType.IN,
                    }
                },
                new string[0],
                new string[0],
                new object[0],
                true
            );
        }
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return _definition;
        }

        protected override (string, Type)[] GetVariables()
        {
            return new[]
            {
                ("Variable", type: Type)
            };
        }

        protected override bool ShowDefinedVariablesAtBeginning()
        {
            return false;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;

            string variableGuid = GetVariableGuid(actionInstance, 0);

            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(
                compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], Type, false)));
            var userVariable = program.data.GetUserDefinedVariable(variableGuid);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(userVariable));
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.Copy());
        }

        protected override string GetVariableName(CyanTriggerAssemblyProgram program, Type type)
        {
            return program.data.CreateVariableName("var", type);
        }
    }
}
