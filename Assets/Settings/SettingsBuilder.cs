using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsBuilder : PopupWindow {

    private static SettingsBuilder _main;
    public new static SettingsBuilder main {
        get {
            if (_main == null) {
                GameObject gameObject = new GameObject("SettingsBuilder");
                _main = (SettingsBuilder)gameObject.AddComponent(typeof(SettingsBuilder));
                _main.StartCoroutine(_main.Create());
            };
            return _main;
        }
    }

    private static TextMeshProUGUI progressText;

    public override IEnumerator Create() {
		activeTasks++;
        
        AddBackgroundCanvas();

        GameObject edge = AddBackground();
        SetRect(edge, 0, 0, 1, 1, 10, 10, -10, -10);
        
        //CONTENT
        AddContentRect();

        edge.GetComponent<Image>().enabled = false;
        backgroundRect.GetComponent<Image>().enabled = false;
        
		GameObject tempTextGO = AddText(edge.GetComponent<RectTransform>(), "Temp", "Temp");
        SetRect(tempTextGO, 0, 1f, 0, 0.05f, 0, 0, 0, 0);
		TextMeshProUGUI tempText = tempTextGO.GetComponent<TextMeshProUGUI>();
		tempText.enableAutoSizing = true;
        tempText.color = new Color(0f, 0f, 0f, 0f);
		yield return null;
		float fontSize = tempText.fontSize;
		GameObject.Destroy(tempTextGO);

        GameObject progressTextGO = AddText(
            contentRect, 
            "ProgressText", 
            "", 
            fontStyles:FontStyles.Bold,
            textAlignmentOptions:TextAlignmentOptions.TopLeft
        );
        SetRect(progressTextGO);
        progressText = progressTextGO.GetComponent<TextMeshProUGUI>();
        progressText.enableAutoSizing = false;
        progressText.fontSize = fontSize;
        
		activeTasks--;
        yield break;

    }

    public static void AddProgressText(string newText) => progressText.text = progressText.text + newText;

	public IEnumerator Initialise() {

		if (isBusy) {yield return null;}

		userResponded = false;
		cancelled = false;

	}
}
