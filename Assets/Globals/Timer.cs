using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Timer : MonoBehaviour {
    //All this Monobehaviour does is calculate the real time when an IEnumerator in a Coroutine should yield
    //Use this as Settings has no Update() function (it is static).

    public TextMeshProUGUI fpsCount;

    private static float _targetFPS = 50;
    public static float targetFPS {
        set {
            _targetFPS = Mathf.Max(0.001f, value);
            yieldStepTime = 1f / _targetFPS;
        }
    }
	public static float yieldStepTime = 0.04f;
	public static float yieldTime = 0f;
    public static float updateFPSTime = 0.25f;
    private static float timeToUpdateFPS = 0f;
	public static bool yieldNow {
		get {return Time.realtimeSinceStartup > yieldTime;}
	}

    public static float frameTime = 0f;

    void Update() {
        if (timeToUpdateFPS < 0f && fpsCount != null) {
            fpsCount.text = Mathf.RoundToInt(1f / Time.deltaTime).ToString();
            timeToUpdateFPS = updateFPSTime;
        }
        timeToUpdateFPS -= Time.deltaTime;
        frameTime = Time.realtimeSinceStartup;
        yieldTime = frameTime + yieldStepTime;
    }
}
