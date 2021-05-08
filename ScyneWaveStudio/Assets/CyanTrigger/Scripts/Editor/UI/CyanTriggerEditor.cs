using UnityEditor;
using UnityEngine;

namespace CyanTrigger
{
    [CustomEditor(typeof(CyanTrigger))]
    public class CyanTriggerEditor : Editor
    {
        private CyanTrigger _cyanTrigger;
        private CyanTriggerSerializableInstanceEditor _editor;
        
        private void OnEnable()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }
            
            _cyanTrigger = (CyanTrigger)target;
            CreateEditor();

            //EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        
        private void OnDisable()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }
            
            _editor?.Dispose();
            
            //EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void CreateEditor()
        {
            _editor?.Dispose();

            var triggerInstance = _cyanTrigger.triggerInstance;
            var instanceProperty = serializedObject.FindProperty(nameof(CyanTriggerScriptableObject.triggerInstance));

            _editor = new CyanTriggerSerializableInstanceEditor(instanceProperty, triggerInstance, this);
        }
        
        public override void OnInspectorGUI()
        {
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox), GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 30));
                
                EditorGUILayout.LabelField("Exit Playmode to Edit CyanTrigger.");
                EditorGUILayout.LabelField("This will be improved later.");
                
                EditorGUILayout.EndVertical();
                return;
            }
            
            _editor.OnInspectorGUI();
            
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 30));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);
            
            if (GUILayout.Button("Compile Triggers"))
            {
                CyanTriggerSerializerManager.RecompileAllTriggers(true);
            }
            
            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
        
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                CreateEditor();
            }
        }
    }
}