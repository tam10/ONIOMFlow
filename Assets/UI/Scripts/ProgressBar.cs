using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProgressBar : MonoBehaviour {
    
    private RectTransform rectTransform;
    public RectTransform foregroundRect;

    void Awake() {
        rectTransform = GetComponent<RectTransform>();
    }
    public void SetValue(float value) {
        foregroundRect.sizeDelta = new Vector2 (
            rectTransform.sizeDelta.x * Mathf.Clamp(value, 0f, 1f),
            foregroundRect.sizeDelta.y
        );
    }
}
