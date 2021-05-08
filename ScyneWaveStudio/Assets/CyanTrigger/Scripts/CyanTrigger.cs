using System;
using UnityEngine;
using VRC.Udon;

namespace CyanTrigger
{
    [DisallowMultipleComponent]
    public class CyanTrigger : MonoBehaviour
    {
        public CyanTriggerSerializableInstance triggerInstance;

#if UNITY_EDITOR
        public void Reset()
        {
            if (triggerInstance == null)
            {
                triggerInstance = CyanTriggerSerializableInstance.CreateInstance();
            }
            
            if (triggerInstance.udonBehaviour == null)
            {
                triggerInstance.udonBehaviour = gameObject.AddComponent<UdonBehaviour>();
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnDrawGizmosSelected()
        {
            var data = triggerInstance.triggerDataInstance;
            foreach (var variable in data.variables)
            {
                DrawLineToObject(variable);
            }
            
            foreach (var trigEvent in data.events)
            {
                DrawLineToObjects(trigEvent.eventInstance);

                foreach (var action in trigEvent.actionInstances)
                {
                    DrawLineToObjects(action);
                }
            }
        }

        private void DrawLineToObjects(CyanTriggerActionInstance actionInstance)
        {
            foreach (var input in actionInstance.inputs)
            {
                DrawLineToObject(input);
            }

            if (actionInstance.multiInput != null)
            {
                foreach (var input in actionInstance.multiInput)
                {
                    DrawLineToObject(input);
                }
            }
        }

        private void DrawLineToObject(CyanTriggerActionVariableInstance variableInstance)
        {
            if (variableInstance.isVariable || variableInstance.data?.obj == null)
            {
                return;
            }

            if (variableInstance.data.obj.GetType().IsSubclassOf(typeof(Component)))
            {
                Component component = variableInstance.data.obj as Component;
                if (component != null && component.gameObject != gameObject)
                {
                    Gizmos.DrawLine(transform.position, component.transform.position);
                }
            }
            
            if (variableInstance.data.obj is GameObject otherGameObject && 
                otherGameObject != null &&
                otherGameObject != gameObject)
            {
                Gizmos.DrawLine(transform.position, otherGameObject.transform.position);
            }
        }
    }

    // TODO delete as inline in Udon Behaviour did not work as expected...
    public class CyanTriggerScriptableObject : ScriptableObject
    {
        public CyanTriggerSerializableInstance triggerInstance;
        
#if UNITY_EDITOR  
        public void Reset()
        {
            if (triggerInstance == null)
            {
                triggerInstance = CyanTriggerSerializableInstance.CreateInstance();
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
    
    [Serializable]
    public class CyanTriggerSerializableInstance
    {
        public float proximity = 2f;
        public string interactText = "Use";
        public CyanTriggerDataInstance triggerDataInstance; // TODO encode this directly instead of encoding each children individually?
        
        [HideInInspector]
        public UdonBehaviour udonBehaviour;

        public static CyanTriggerSerializableInstance CreateInstance()
        {
            var instance = new CyanTriggerSerializableInstance
            {
                triggerDataInstance = new CyanTriggerDataInstance
                {
                    events =  new CyanTriggerEvent[0],
                    variables = new CyanTriggerVariable[0],
                }
            };
            return instance;
        }

        private CyanTriggerSerializableInstance() { }
    }
}