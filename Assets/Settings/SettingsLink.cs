using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EL = Constants.ErrorLevel;

public class SettingsLink : MonoBehaviour {

    private static SettingsLink _main;
    public static SettingsLink main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<SettingsLink>();
            return _main;
        }
    }
    public float fogStartDistance = 1f;
	public float fogEndDistance = 50f;
	public float fogRatio = 0.5f;
    public float lineThickness = 0.2f;
    public EL errorLevel = EL.INFO;

    public void PushChanges() {
        Settings.fogStartDistance = fogStartDistance;
        Settings.fogEndDistance = fogEndDistance;
        Settings.fogRatio = fogRatio;
        Settings.lineThickness = lineThickness;
        CustomLogger.logErrorLevel = errorLevel;
    }

}