using System;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeSetVariable : CyanTriggerCustomUdonActionNodeDefinition
    {
        private readonly Type _type;
        private readonly UdonNodeDefinition _definition;

        public CyanTriggerCustomNodeSetVariable(Type type)
        {
            _type = type;
            string friendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(_type);
            string fullName = CyanTriggerNameHelpers.SanitizeName(_type.FullName);
            if (type.IsArray)
            {
                fullName += "Array";
            }
            
            _definition = new UdonNodeDefinition(
                "Set " + friendlyName,
                fullName+"__.Set__"+fullName +"__" +fullName,
                _type,
                new []
                {
                    new UdonNodeParameter
                    {
                        name = "input",
                        parameterType = UdonNodeParameter.ParameterType.IN,
                        type = _type
                    },
                    new UdonNodeParameter
                    {
                        name = "output",
                        parameterType = UdonNodeParameter.ParameterType.OUT,
                        type = _type
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
        
        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;

            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(
                compileState.GetDataFromVariableInstance(-1, 0, actionInstance.inputs[0], _type, false)));
            var outputVar =
                compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], _type, true);
                //CyanTriggerCompiler.GetDataFromVariableInstance(program.data, actionInstance.inputs[1], _type, true);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(outputVar));
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.Copy());
            
            actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.OnVariableChangedCheck(program, outputVar));
        }
    }
}
