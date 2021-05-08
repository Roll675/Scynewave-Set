using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.SDKBase;

namespace CyanTrigger
{
    public class CyanTriggerSerializedProgramManager : UnityEditor.AssetModificationProcessor
    {
        public const string SerializedUdonAssetNamePrefix = "CyanTrigger_";
        public const string SerializedUdonPath = "CyanTriggerSerialized";
        public const string DefaultProgramAssetGuid = "Empty";
        
        private static CyanTriggerSerializedProgramManager _instance;
        public static CyanTriggerSerializedProgramManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CyanTriggerSerializedProgramManager();
                }

                return _instance;
            }
        }

        private readonly Dictionary<string, CyanTriggerProgramAsset> _programAssets =
            new Dictionary<string, CyanTriggerProgramAsset>();

        public CyanTriggerProgramAsset DefaultProgramAsset { get; }


        public static string GetExpectedProgramName(string guid)
        {
            return SerializedUdonAssetNamePrefix + guid + ".asset";
        }

        public static CyanTriggerProgramAsset CreateTriggerProgramAsset(string guid)
        {
            DirectoryInfo directory = new DirectoryInfo(Application.dataPath + "/" + SerializedUdonPath);
            if (!directory.Exists)
            {
                directory.Create();
            }
            
            string assetPath = SerializedUdonPath + "/" + GetExpectedProgramName(guid);
            var programAsset = ScriptableObject.CreateInstance<CyanTriggerProgramAsset>();
            AssetDatabase.CreateAsset(programAsset, "Assets/" + assetPath);
            AssetDatabase.ImportAsset("Assets/" + assetPath);
            return AssetDatabase.LoadAssetAtPath<CyanTriggerProgramAsset>("Assets/" + assetPath);
        }
        
        public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            // Skip, since there is nothing to update
            if (_instance == null)
            {
                return AssetDeleteResult.DidNotDelete;
            }
            
            string path = "Assets/" + SerializedUdonPath + "/" + SerializedUdonAssetNamePrefix;
            if (assetPath.Contains(path) && assetPath.EndsWith(".asset"))
            {
                int startIndex = assetPath.IndexOf(path, StringComparison.Ordinal) + path.Length;
                int len = assetPath.Length - 6 - startIndex;
                string guid = assetPath.Substring(startIndex, len);
                if (_instance._programAssets.ContainsKey(guid))
                {
                    _instance._programAssets.Remove(guid);
                }
            }
            
            return AssetDeleteResult.DidNotDelete;
        }
        
        private CyanTriggerSerializedProgramManager()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            DirectoryInfo directory = new DirectoryInfo(Application.dataPath + "/" + SerializedUdonPath);
            if (!directory.Exists)
            {
                directory.Create();
            }
            
            string filePath = "Assets/" + SerializedUdonPath + "/";
            string defaultAsset = GetExpectedProgramName(DefaultProgramAssetGuid);
            foreach (var item in directory.EnumerateFiles())
            {
                if (!item.Extension.Equals(".asset"))
                {
                    continue;
                }
                
                string fileName = filePath + item.Name;
                var serializedTrigger = AssetDatabase.LoadAssetAtPath<CyanTriggerProgramAsset>(fileName);
                if (serializedTrigger == null)
                {
                    Debug.LogWarning("File was not a proper CyanTriggerProgramAsset: " + fileName);
                    continue;
                }

                if (item.Name == defaultAsset)
                {
                    DefaultProgramAsset = serializedTrigger;
                    continue;
                }
                
                _programAssets.Add(serializedTrigger.triggerHash, serializedTrigger);
            }

            if (DefaultProgramAsset == null)
            {
                DefaultProgramAsset = CreateTriggerProgramAsset(DefaultProgramAssetGuid);
            }
        }

        public void ApplyTriggerPrograms(List<CyanTrigger> triggers, bool force = false)
        {
            Profiler.BeginSample("CyanTriggerSerializedProgramManager.ApplyTriggerPrograms");
            
            Dictionary<string, List<CyanTrigger>> hashToTriggers = new Dictionary<string, List<CyanTrigger>>();
            foreach (var trigger in triggers)
            {
                try
                {
                    string hash =
                        CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(trigger.triggerInstance
                            .triggerDataInstance);

                    // Debug.Log("Trigger hash "+ hash +" " +VRC.Tools.GetGameObjectPath(trigger.gameObject));
                    if (!hashToTriggers.TryGetValue(hash, out var triggerList))
                    {
                        triggerList = new List<CyanTrigger>();
                        hashToTriggers.Add(hash, triggerList);
                    }

                    triggerList.Add(trigger);
                }
                catch (Exception e)
                {
                    Debug.LogError("Failed to hash CyanTrigger on object: " + VRC.Tools.GetGameObjectPath(trigger.gameObject) +"\n" + e);
                }
            }

            foreach (string key in new List<string>(_programAssets.Keys))
            {
                var programAsset = _programAssets[key];
                if (programAsset == null)
                {
                    _programAssets.Remove(key);
                }

                // Ensure each asset is stored at the proper key
                if (programAsset.triggerHash != key)
                {
                    _programAssets.Remove(key);
                    _programAssets.Add(programAsset.triggerHash, programAsset);
                }
            }
            
            Queue<CyanTriggerProgramAsset> unusedAssets = new Queue<CyanTriggerProgramAsset>();
            foreach (var programAssetPair in _programAssets)
            {
                // Never work with default program
                if (programAssetPair.Value == DefaultProgramAsset)
                {
                    continue;
                }
                
                string hash = programAssetPair.Value.triggerHash;
                if (!hashToTriggers.ContainsKey(hash))
                {
                    unusedAssets.Enqueue(programAssetPair.Value);
                }
            }
            
            foreach (var triggerHash in hashToTriggers.Keys)
            {
                List<CyanTrigger> curTriggers = hashToTriggers[triggerHash];
                if (curTriggers.Count == 0)
                {
                    continue;
                }

                var firstTrigger = curTriggers[0];

                bool recompile = force;
                
                if (!_programAssets.TryGetValue(triggerHash, out var programAsset))
                {
                    // Pull an asset from Unused, or create a new one.
                    if (unusedAssets.Count > 0)
                    {
                        programAsset = unusedAssets.Dequeue();
                        _programAssets.Remove(programAsset.triggerHash);
                    }
                    else
                    {
                        // TODO figure out a better method here since guid is pretty arbitrary
                        programAsset = CreateTriggerProgramAsset(Guid.NewGuid().ToString());
                    }
                    
                    _programAssets.Add(triggerHash, programAsset);
                    programAsset.SetCyanTriggerData(firstTrigger.triggerInstance.triggerDataInstance, triggerHash);
                    recompile = true;
                }

                if (recompile)
                {
                    bool success = programAsset.CompileTrigger();
                    if (!success)
                    {
                        PrintError("Failed to compile CyanTrigger on objects: ", curTriggers);
                        
                        _programAssets.Remove(programAsset.triggerHash);
                        programAsset.SetCyanTriggerData(null, programAsset.name);
                        _programAssets.Add(programAsset.triggerHash, programAsset);
                        
                        continue;
                    }
                }

                if (programAsset == DefaultProgramAsset)
                {
                    Debug.LogError("Trying to use default program asset for CyanTrigger!");
                    continue;
                }

                if (triggerHash != programAsset.triggerHash)
                {
                    PrintError("CyanTrigger hash was not the expected hash after compiling! \"" + triggerHash +"\" vs \"" + programAsset.triggerHash +"\" for objects: ", curTriggers);
                    continue;
                }
                
                foreach (var trigger in curTriggers)
                {
                    PairTriggerToProgram(trigger, programAsset);
                }
            }
            
            Profiler.EndSample();
        }

        private static void PairTriggerToProgram(CyanTrigger trigger, CyanTriggerProgramAsset programAsset)
        {
            try
            {
                var data = trigger.triggerInstance;
                var udon = trigger.triggerInstance.udonBehaviour;

                if (data == null || udon == null || data.triggerDataInstance == null)
                {
                    Debug.LogError("Could not apply program for CyanTrigger: " +
                              VRC.Tools.GetGameObjectPath(trigger.gameObject));
                    return;
                }

                bool dirty = false;
                if (udon.programSource != programAsset)
                {
                    udon.programSource = programAsset;
                    dirty = true;
                }

                programAsset.ApplyCyanTriggerToUdon(data, udon, ref dirty);

                if (dirty)
                {
                    // TODO check for prefab applying?
                    EditorUtility.SetDirty(udon);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to apply prefab for CyanTrigger: " +
                               VRC.Tools.GetGameObjectPath(trigger.gameObject));
                Debug.LogError(e);
            }
        }

        private static void PrintError(string message, List<CyanTrigger> triggers)
        {
            string objectPaths = "";
            foreach (var trigger in triggers)
            {
                objectPaths += VRC.Tools.GetGameObjectPath(trigger.gameObject) + "\n";
            }
            
            Debug.LogError(message  + objectPaths);
        }
    }
}

