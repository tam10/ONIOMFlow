using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PulseIcon : MonoBehaviour
{
    
    public Color normalColor;
    public Color pulseColor;
    private Image icon;
    private Button button;

    private bool animating = false;

    private float pulseTime = 0.4f;

    public delegate void PulseIconHandler();
    public PulseIconHandler onClickHandler;

    public void Awake() {
        icon = GetComponent<Image>();
        button = GetComponent<Button>();
        button.onClick.AddListener(ButtonClicked);
        
    }

    public void Pulse() {
        //Don't allow multiple animations
        if (animating) return;

        StartCoroutine(PulseAnimation());
    }

    private void ButtonClicked() {
        onClickHandler();
    }

    public IEnumerator PulseAnimation() {
        animating = true;
        float currentPulseTime = 0f;
        while (currentPulseTime < pulseTime) {
            icon.color = Color.Lerp(normalColor, pulseColor, currentPulseTime / pulseTime);
            currentPulseTime += Time.deltaTime;
            yield return null;
        }
        while (currentPulseTime > 0f) {
            icon.color = Color.Lerp(normalColor, pulseColor, currentPulseTime / pulseTime);
            currentPulseTime -= Time.deltaTime;
            yield return null;
        }
        icon.color = normalColor;
        animating = false;
    }
}
