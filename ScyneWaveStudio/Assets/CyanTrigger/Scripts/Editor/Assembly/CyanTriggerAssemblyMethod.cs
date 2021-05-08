using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CyanTrigger
{
    public class CyanTriggerAssemblyMethod
    {
        public uint startAddress;
        public List<CyanTriggerAssemblyInstruction> actions;
        public string name;
        public bool export;

        public CyanTriggerAssemblyInstruction endNop;
        
        public CyanTriggerAssemblyMethod(string name, bool export)
        {
            this.name = name;
            this.export = export;
            actions = new List<CyanTriggerAssemblyInstruction>();

            endNop = CyanTriggerAssemblyInstruction.Nop();
        }
        
        public void AddAction(CyanTriggerAssemblyInstruction action)
        {
            actions.Add(action);
        }

        public void AddActions(List<CyanTriggerAssemblyInstruction> actions)
        {
            this.actions.AddRange(actions);
        }

        public void PushInitialEndVariable(CyanTriggerAssemblyData data)
        {
            CyanTriggerAssemblyDataType endAddress = data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.EndAddress);
            actions.Insert(0, CyanTriggerAssemblyInstruction.PushVariable(endAddress));
        }

        public void PushMethodEndReturnJump(CyanTriggerAssemblyData data)
        {
            CyanTriggerAssemblyDataType returnAddress = data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ReturnAddress);
            AddAction(CyanTriggerAssemblyInstruction.PushVariable(returnAddress));
            AddAction(CyanTriggerAssemblyInstruction.Copy());
            AddAction(CyanTriggerAssemblyInstruction.JumpIndirect(returnAddress));
        }

        public uint ApplyAddressSize(uint address)
        {
            if (actions.Count == 0)
            {
                return address;
            }

            startAddress = address + actions[0].GetInstructionSize();
            foreach (var instruction in actions)
            {
                instruction.SetAddress(address);
                address += instruction.GetInstructionSize();
            }

            return address;
        }

        public void MapLabelsToAddress(Dictionary<string, uint> methodsToStartAddress)
        {
            foreach (var action in actions)
            {
                string jumpLabel = action.GetJumpLabel();
                if (!string.IsNullOrEmpty(jumpLabel))
                {
                    if (!methodsToStartAddress.ContainsKey(jumpLabel))
                    {
                        throw new Exception("JumpLabel missing: " + jumpLabel);
                    }
                    action.UpdateAddress(methodsToStartAddress[jumpLabel]);
                }
            }
        }

        public void Finish()
        {
            actions.Add(endNop);
        }

        public string Export()
        {
            StringBuilder sb = new StringBuilder();
            if (export)
            {
                sb.AppendLine("  .export " + name);
            }

            sb.AppendLine("  " + name + ":");

            foreach (var action in actions)
            {
                if (action.GetInstructionType() == CyanTriggerInstructionType.NOP)
                {
                    continue;
                }

                sb.AppendLine("    " + action.Export());
            }

            return sb.ToString();
        }

        public CyanTriggerAssemblyMethod Clone()
        {
            CyanTriggerAssemblyMethod method = new CyanTriggerAssemblyMethod(name, export);

            foreach (var action in actions)
            {
                method.AddAction(action.Clone());
            }
            
            return method;
        }
    }
}