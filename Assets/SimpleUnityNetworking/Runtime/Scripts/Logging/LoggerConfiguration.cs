using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Logging
{
    [CreateAssetMenu(fileName = "LoggerConfiguration", menuName = "SimpleUnityNetworking/LoggerConfiguration")]
    public class LoggerConfiguration : ScriptableObject
    {
        public Logger Logger => _logger;
        private Logger _logger;
        public LoggerSettings Settings => _settings;
        [SerializeField] private LoggerSettings _settings;
        
        public LoggerConfiguration()
            : this(new()) {}

        public LoggerConfiguration(LoggerSettings settings)
        {
            _settings = settings;
            _logger = new(Settings);
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(LoggerConfiguration), true)]
    public class LoggerConfigurationEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_settings"), new GUIContent("Settings:"));
            EditorGUI.indentLevel--;
            
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomPropertyDrawer(typeof(LoggerSettings), true)]
    public class LoggerSettingsDrawer : PropertyDrawer
    {
        private bool _areSettingsVisible;
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            _areSettingsVisible = EditorGUILayout.Foldout(_areSettingsVisible, "Settings:");

            if (_areSettingsVisible)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(property.FindPropertyRelative("PrintDebugToConsole"), new GUIContent("Print Debug To Console:", "Whether debug messages should also be printed to the console."));
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) { return 0; }
    }
#endif
}