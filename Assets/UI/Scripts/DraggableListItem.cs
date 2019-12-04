using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class DraggableListItem : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {

    public TextMeshProUGUI text;
    public object value;
    public ScrollRect parent;
    public Transform unmaskedTransform;

    public bool draggable;
    public float timeUntilDragged = 0.1f;
    public bool isBeingDragged;

    public bool pointerDown;

    private bool PointerInScrollRect(Vector2 pointerPosition, out Vector2 inRectPosition) {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent.content, 
            pointerPosition, 
            Camera.main, 
            out inRectPosition
        );
    }

    public void OnPointerClick(PointerEventData pointerEventData) {}

    public void OnPointerDown(PointerEventData pointerEventData) {
        pointerDown = true;

        //Check whether to invoke drag or click callback
        StartCoroutine(DraggingTest());
    }

    public void OnPointerUp(PointerEventData pointerEventData) {
        pointerDown = false;
        if (!isBeingDragged) {
            OnPointerClick(pointerEventData);
        }
    }
    
    private IEnumerator DraggingTest() {
        isBeingDragged = false;
        float timer = 0f;
        while (timer < timeUntilDragged && pointerDown) {
            timer += Time.deltaTime;
            yield return null;
        }
        isBeingDragged = true;

    }

    private IEnumerator OnDrag() {
        while (pointerDown) {
            Vector2 inRectPosition;
            if (PointerInScrollRect(Input.mousePosition, out inRectPosition)) {
                Debug.Log(inRectPosition);
            }
            yield return null;
        }
    }
    
}
