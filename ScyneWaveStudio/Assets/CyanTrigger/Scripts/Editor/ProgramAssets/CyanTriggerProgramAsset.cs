
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;
using VRC.Udon.Serialization.OdinSerializer;

namespace CyanTrigger
{
    public class CyanTriggerProgramAsset : UdonAssemblyProgramAsset
    {
        public string triggerHash;

        [NonSerialized, OdinSerialize]
        private CyanTriggerDataInstance cyanTriggerDataInstance;
        
        [NonSerialized, OdinSerialize]
        private CyanTriggerDataReferences variableReferences;
        
        [NonSerialized, OdinSerialize]
        private Dictionary<string, (object value, Type type)> heapDefaultValues = 
            new Dictionary<string, (object value, Type type)>();
        
        private bool _showProgramUasm;
        private bool _showVariableReferences;
        private bool _showPublicVariables;
        private bool _showDefaultHeapValues;

        protected override void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            CyanTrigger cyanTrigger = udonBehaviour.GetComponent<CyanTrigger>();
            if (cyanTrigger == null)
            {
                // TODO figure out what to do here.
                // Delete udon behaviour as cyanTrigger component is missing?
                EditorGUILayout.LabelField("Missing CyanTrigger!");
                return;
            }
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("CyanTrigger", cyanTrigger, typeof(CyanTrigger), true);
            EditorGUI.EndDisabledGroup();

            // TODO verify if valid, otherwise break out;

            ShowDebugInformation(udonBehaviour, ref dirty);
        }

        private void ShowDebugInformation(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            _showProgramUasm = EditorGUILayout.Foldout(_showProgramUasm, "Compiled Trigger Assembly");
            if (_showProgramUasm)
            {
                DrawAssemblyTextArea(false, ref dirty);

                if (program != null)
                {
                    DrawProgramDisassembly();
                }
            }
            
#if CYAN_TRIGGER_DEBUG
            _showVariableReferences = EditorGUILayout.Foldout(_showVariableReferences, "Variable References");
            if (_showVariableReferences) 
            {
                if (variableReferences == null)
                {
                    Debug.LogError("Variable references are null for program: "+name);
                    CompileTrigger();
                }
                
                foreach (var reference in variableReferences.ActionDataIndices)
                {
                    GUILayout.Label(reference.ToString());
                }
            }

            _showPublicVariables = EditorGUILayout.Foldout(_showPublicVariables, "Public Variables");
            if (_showPublicVariables)
            {
                EditorGUI.BeginDisabledGroup(true);

                DrawPublicVariables(udonBehaviour, ref dirty);

                EditorGUI.EndDisabledGroup();
            }

            _showDefaultHeapValues = EditorGUILayout.Foldout(_showDefaultHeapValues, "Heap Variables");
            if (_showDefaultHeapValues)
            {
                IUdonSymbolTable symbolTable = program?.SymbolTable;
                IUdonHeap heap = program?.Heap;
                if (symbolTable == null || heap == null)
                {
                    return;
                }

                GUILayout.Label("Heap Values:");
                foreach (var symbol in symbolTable.GetSymbols())
                {
                    uint address = symbolTable.GetAddressFromSymbol(symbol);
                    GUILayout.Label("Symbol: " + symbol + ", type: " + heap.GetHeapVariableType(address) + ", obj: " + heap.GetHeapVariable(address));
                }

                GUILayout.Space(5);
                GUILayout.Label("Default Values:");

                foreach (var defaultValue in heapDefaultValues)
                {
                    GUILayout.Label(defaultValue.Key + " " + defaultValue.Value.Item1);
                }
            }
#endif
        }

        public void ApplyCyanTriggerToUdon(
            CyanTriggerSerializableInstance triggerInstance, 
            UdonBehaviour udonBehaviour,
            ref bool dirty)
        {
            if (variableReferences == null)
            {
                // TODO figure out why serialization is failing here.
                Debug.LogError("Variable references are null for program: "+name);
                CompileTrigger();
            }
            
            UpdatePublicVariables(triggerInstance.triggerDataInstance, udonBehaviour, ref dirty);

            ApplyUdonProperties(triggerInstance, udonBehaviour, ref dirty);
        }

        private void UpdatePublicVariables(
            CyanTriggerDataInstance triggerDataInstance, 
            UdonBehaviour udonBehaviour, 
            ref bool dirty)
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            if (program == null)
            {
                Debug.LogWarning("CyanTrigger program is null: " +name);
                return;
            }
            
            variableReferences?.ApplyPublicVariableData(
                triggerDataInstance, 
                udonBehaviour,
                program.SymbolTable,
                ref dirty);
        }

        private static void ApplyUdonProperties(
            CyanTriggerSerializableInstance triggerInstance, 
            UdonBehaviour udonBehaviour, 
            ref bool dirty)
        {
            if (!Mathf.Approximately(triggerInstance.proximity, udonBehaviour.proximity))
            {
                udonBehaviour.proximity = triggerInstance.proximity;
                dirty = true;
            }

            if (triggerInstance.interactText != udonBehaviour.interactText)
            {
                udonBehaviour.interactText = triggerInstance.interactText;
                dirty = true;
            }
        }
        
        protected override void RefreshProgramImpl()
        {
            CompileTrigger();
        }
        
        private void ApplyDefaultValuesToHeap()
        {
            IUdonSymbolTable symbolTable = program?.SymbolTable;
            IUdonHeap heap = program?.Heap;
            if (symbolTable == null || heap == null)
            {
                return;
            }

            foreach (var defaultValue in heapDefaultValues)
            {
                if (!symbolTable.HasAddressForSymbol(defaultValue.Key))
                {
                    continue;
                }

                uint symbolAddress = symbolTable.GetAddressFromSymbol(defaultValue.Key);
                (object value, Type declaredType) = defaultValue.Value;
                if (typeof(UnityEngine.Object).IsAssignableFrom(declaredType))
                {
                    if (value != null && !declaredType.IsInstanceOfType(value))
                    {
                        heap.SetHeapVariable(symbolAddress, null, declaredType);
                        continue;
                    }

                    if ((UnityEngine.Object)value == null)
                    {
                        heap.SetHeapVariable(symbolAddress, null, declaredType);
                        continue;
                    }
                }

                if (value != null)
                {
                    if (!declaredType.IsInstanceOfType(value))
                    {
                        value = declaredType.IsValueType ? Activator.CreateInstance(declaredType) : null;
                    }
                }

                heap.SetHeapVariable(symbolAddress, value, declaredType);
            }
        }
        
        private void SetDefaultHeapValues(Dictionary<string, (object value, Type type)> variables)
        {
            heapDefaultValues.Clear();
            if (variables != null)
            {
                foreach (var var in variables)
                {
                    heapDefaultValues.Add(var.Key, var.Value);
                }
            }
        }

        public void SetCyanTriggerData(CyanTriggerDataInstance dataInstance, string hash)
        {
            triggerHash = hash;
            cyanTriggerDataInstance = CyanTriggerUtil.CopyCyanTriggerDataInstance(dataInstance, false);
        }

        public bool CompileTrigger()
        {
            return CyanTriggerCompiler.CompileCyanTrigger(cyanTriggerDataInstance, this, triggerHash);
        }

        public void SetCompiledData(
            string hash, 
            string assembly, 
            Dictionary<string, (object value, Type type)> variables,
            CyanTriggerDataReferences varReferences,
            CyanTriggerDataInstance dataInstance)
        {
            triggerHash = hash;
            udonAssembly = assembly;
            cyanTriggerDataInstance = dataInstance;

            variableReferences = varReferences;
            SetDefaultHeapValues(variables);
            
            base.RefreshProgramImpl();
            ApplyDefaultValuesToHeap();
            
            SerializedProgramAsset.StoreProgram(program);
            EditorUtility.SetDirty(this);
        }

        private void DebugLogHeapAndRefData()
        {
            Debug.Log("---- Default Heap:");
            foreach (var var in heapDefaultValues)
            {
                Debug.Log(var.Key +" " +var.Value.type +" " +var.Value.value);
            }
            Debug.Log("---- References:");
            foreach (var v in variableReferences.ActionDataIndices)
            {
                Debug.Log(v);
            }
        }


        [SerializeField, HideInInspector]
        private SerializationData serializationData;
        
        protected override void OnBeforeSerialize()
        {
            UnitySerializationUtility.SerializeUnityObject(this, ref serializationData);
            base.OnBeforeSerialize();
        }
        
        protected override void OnAfterDeserialize()
        {
            UnitySerializationUtility.DeserializeUnityObject(this, ref serializationData);
            base.OnAfterDeserialize();
        }

        private void OnEnable()
        {
            try
            {
                // Program doesn't always reload correctly, so try to retrieve it here.
                if (program == null && SerializedProgramAsset != null)
                {
                    program = SerializedProgramAsset.RetrieveProgram();
                }

                // TODO verify if this is even needed? 
                // if (CyanTriggerVersionMigrator.MigrateTrigger(cyanTriggerDataInstance))
                // {
                //     triggerHash = CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(cyanTriggerDataInstance);
                //     EditorUtility.SetDirty(this);
                //     AssetDatabase.SaveAssets();
                // }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}
