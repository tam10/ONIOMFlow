using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EL = Constants.ErrorLevel;


[CustomEditor(typeof(SettingsLink))]
public class SettingsGUI : Editor {

    
    private SerializedProperty fogStartDistance;
	private SerializedProperty fogEndDistance;
	private SerializedProperty fogRatio;
    private SerializedProperty lineThickness;
    public EL errorLevel;



    public void OnEnable() {
        fogStartDistance = serializedObject.FindProperty("fogStartDistance");
        fogEndDistance = serializedObject.FindProperty("fogEndDistance");
        fogRatio = serializedObject.FindProperty("fogRatio");
        lineThickness = serializedObject.FindProperty("lineThickness");
    }

    public override void OnInspectorGUI() {
        serializedObject.UpdateIfRequiredOrScript();

        EditorGUILayout.Slider(fogStartDistance, 0f, 100f, "Fog Start Distance");
        EditorGUILayout.Slider(fogEndDistance, 0f, 100f, "Fog End Distance");
        EditorGUILayout.Slider(fogRatio, 0f, 1f, "Fog Multiplier");

        EditorGUILayout.Space();
        EditorGUILayout.Slider(lineThickness, 0.1f, 1f, "Wireframe Thickness");

        EditorGUILayout.Space();
        errorLevel = (EL)EditorGUILayout.EnumPopup("Set Error Level", errorLevel);
        if (GUILayout.Button("Set")) {
            setErrorLevel(errorLevel);
        }
        serializedObject.ApplyModifiedProperties();

        if (GUI.changed) {SettingsLink.main.PushChanges();}
    }

    void setErrorLevel(EL errorLevel) {
        CustomLogger.logErrorLevel = errorLevel;
    }

}
