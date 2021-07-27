using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TaskBar : MonoBehaviour {
    
    public TextMeshProUGUI text;
    public ProgressBar progressBar;

    private bool ready = false;

    public int fontSize = 12;
    public float outerHeight = 24f;
    public float innerHeight = 6f;
    public float taskTextWidth = 200f;
    public float progressBarWidth = 200f;

    public void Start() {
//        int _pixelMultiplier = (Settings.isRetina ? 2 : 1);
//
//        //Change text geometry
//        text.fontSize = fontSize * _pixelMultiplier;
//        text.GetComponent<RectTransform>().sizeDelta = new Vector2(
//            taskTextWidth * _pixelMultiplier,
//            outerHeight * _pixelMultiplier
//        );
//
//        //Change progress bar container geometry
//        progressBar.GetComponent<RectTransform>().sizeDelta = new Vector2(
//            progressBarWidth * _pixelMultiplier,
//            outerHeight * _pixelMultiplier
//        );
//
//        //Change progress bar geometry
//        RectTransform barBackground = progressBar.transform.GetChild(0).GetComponent<RectTransform>();
//        barBackground.sizeDelta = new Vector2(
//            progressBarWidth * _pixelMultiplier,
//            innerHeight * _pixelMultiplier
//        );
    }

    public void SetProgress(string newText, float progressRatio) {
        SetText(newText);
        progressBar.SetValue(progressRatio);
        ready = false;
    }

    public void SetText(string newText) {
        text.text = newText;
    }

    public void Clear() {
        if (!ready) {
            text.text = "Ready";
            progressBar.SetValue(0f);
            ready = true;
        }
    }
    
}
