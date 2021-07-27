using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ResidueScrollbar : Scrollbar {
    
    RectTransform rectTransform;

    //Inherit Awake from Scrollbar and add to it
    protected override void Awake() {
        base.Awake();
        rectTransform = GetComponent<RectTransform>();
    }

    public override void OnPointerDown(PointerEventData pointerEventData) {
        
        Vector2 localPosition;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            pointerEventData.position,
            pointerEventData.pressEventCamera,
            out localPosition
        )) {
            return;
        }

        //This is the position, between 0 and 1, that the cursor clicked the bar.
        //The region is stretched so clicking anywhere between the top and the 
        // centre of the bar (were it at the top) results in a value of 0 
        float relativeY = 1f - Mathf.Clamp(
            (localPosition.y / rectTransform.rect.height) * (1f + size) - (size / 2f),
            0f,
            1f
        );

        value = relativeY;
        onValueChanged.Invoke(value);
    }
}
