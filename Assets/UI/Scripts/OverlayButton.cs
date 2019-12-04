using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public class OverlayButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
    public Button button;

    public bool draggable;

    public bool pointerDown;
    public bool isBeingDragged;
    Vector3 pointerDownPosition;

    float timeUntilDragged = 0.1f;

    public delegate void BeginDragHandler();
    public BeginDragHandler onBeginDrag;

    public delegate void PointerClickHandler();
    public PointerClickHandler onPointerClick;

    void Awake() {
        draggable = true;
        onBeginDrag = Pass;
        onPointerClick = Pass;
    }

    void Pass() {}

    public void OnPointerDown(PointerEventData pointerEventData) {

        pointerDown = true;
        isBeingDragged = false;

		pointerDownPosition = Input.mousePosition;

		if (pointerEventData.button == PointerEventData.InputButton.Left) {
            StartCoroutine(DraggingTest());
        }
    }

    public void OnPointerUp(PointerEventData pointerEventData) {
        pointerDown = false;
        if (!isBeingDragged) {
            OnPointerClick(pointerEventData);
        }
        isBeingDragged = false;
    }

    private IEnumerator DraggingTest() {
        float timer = 0f;
        while (pointerDown && timer < timeUntilDragged) {
            timer += Time.deltaTime;
            yield return null;
        }

        isBeingDragged = true;
		if (draggable) {
            onBeginDrag();
		}
    }

	public void OnPointerClick(PointerEventData pointerEventData) {
		if (pointerEventData.button == PointerEventData.InputButton.Left) {
            onPointerClick();
		} else if (pointerEventData.button == PointerEventData.InputButton.Right) {
			;
		}
	}

    void Update() {
        if (isBeingDragged && Input.GetMouseButtonUp(0)) {
            isBeingDragged = false;
        }
    }
}
