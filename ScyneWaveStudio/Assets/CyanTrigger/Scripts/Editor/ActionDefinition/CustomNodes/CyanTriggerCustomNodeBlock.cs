﻿using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeBlock : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Block",
            "CyanTriggerSpecial_Block",
            typeof(CyanTrigger),
            new UdonNodeParameter[0],
            new string[0],
            new string[0],
            new object[0],
            true
        );
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return NodeDefinition;
        }
        
        public override bool CreatesScope()
        {
            return true;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            // Do nothing here
        }
        
        public override void HandleEndScope(CyanTriggerCompileState compileState)
        {
            // Do nothing here
        }
        
        public override bool HasDependencyNodes()
        {
            return true;
        }

        public override UdonNodeDefinition[] GetDependentNodes()
        {
            return new[] {CyanTriggerCustomNodeBlockEnd.NodeDefinition};
        }
    }
}
