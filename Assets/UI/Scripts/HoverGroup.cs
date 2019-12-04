using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class HoverGroup : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

    public Image edge;
    public TextMeshProUGUI text;

    public Color hoveredColour;
    public Color notHoveredColour;
    public float animationTime = 0.1f;

    private Coroutine ChangeColourCoroutine = null;
    private Color currentColour;

    void Awake() {
        currentColour = notHoveredColour;
        edge.color = notHoveredColour;
        text.color = notHoveredColour;
    }

    public void OnPointerEnter(PointerEventData pointerEventData) {
        
        if (ChangeColourCoroutine != null) StopCoroutine(ChangeColourCoroutine);
        StartCoroutine(ChangeColour(currentColour, hoveredColour));
    }

    public void OnPointerExit(PointerEventData pointerEventData) {
        if (ChangeColourCoroutine != null) StopCoroutine(ChangeColourCoroutine);
        StartCoroutine(ChangeColour(currentColour, notHoveredColour));
    }

    private IEnumerator ChangeColour(Color fromColour, Color toColour) {
        float timer = 0f;
        while (timer < animationTime) {
            ChangeCurrentColour(Color.Lerp(fromColour, toColour, timer / animationTime));
            timer += Time.deltaTime;
            yield return null;
        }
        ChangeCurrentColour(toColour);
        yield break;
    }

    private void ChangeCurrentColour(Color newColour) {
        currentColour = newColour;
        edge.color = currentColour;
        text.color = currentColour;
    }
}
