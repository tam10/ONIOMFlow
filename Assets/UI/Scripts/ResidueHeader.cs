using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ResidueHeader : MonoBehaviour {
    public RectTransform rectTransform;
    public TextMeshProUGUI headerText;
    public float textFontSize = 14f;

    public void Awake() {
        headerText.fontSize = Settings.isRetina ? textFontSize * 2 : textFontSize;
    }
}
