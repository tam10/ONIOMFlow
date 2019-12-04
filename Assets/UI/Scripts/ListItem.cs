using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ListItem : 
	MonoBehaviour, 
	IPointerDownHandler, 
	IPointerUpHandler,
	IDragHandler,
	IPointerEnterHandler,
	IPointerExitHandler
{

	public Image background;
	public Image edge;
	public TextMeshProUGUI text;
    public object value;
    public ScrollRect parent;
    public Transform unmaskedTransform;

    public bool draggable;
    public static float timeUntilDragged = 0.1f;
	public static float scrollRegionSize = 20f;
    public bool isBeingDragged;
	Vector3 initialPointerPosition;
	public static float deleteZoneSize = 10f;
	public bool inDeleteZone;

    public bool pointerDown;

	public int siblingIndex;

	private float alpha;
	private Color colour;

	public delegate void PointerHandler(PointerEventData pointerEventData);
	public PointerHandler OnPointerClickHandler;
	public PointerHandler OnPointerEnterHandler;
	public PointerHandler OnPointerExitHandler;

	void Awake() {
		if (OnPointerClickHandler == null) OnPointerClickHandler = Pass;
        if (OnPointerEnterHandler == null) OnPointerEnterHandler = Pass;
        if (OnPointerExitHandler == null) OnPointerExitHandler = Pass;

		colour = background.color;
		alpha = background.color.a;
	} 

    private bool PointerInScrollRect(out Vector2 inRectPosition) {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent.content, 
            Input.mousePosition, 
            null, 
            out inRectPosition
        );
    }

	private void CheckIfInDeleteZone(Vector2 inRectPosition) {
		bool newInDeleteZone = 
			(inRectPosition.x < - deleteZoneSize) ||
			(inRectPosition.x > parent.content.rect.width + deleteZoneSize) || 
			(-inRectPosition.y < - deleteZoneSize) ||
			(-inRectPosition.y > parent.content.rect.height + deleteZoneSize);

		//Only trigger colour change when moving inside or outside of delete zone
		if (inDeleteZone ^ newInDeleteZone) {
			colour.a = newInDeleteZone ? 0f : alpha;
			background.color = colour;
		}

		inDeleteZone = newInDeleteZone;
	}

	private void Pass(PointerEventData pointerEventData) {}

    public void OnPointerClick(PointerEventData pointerEventData) {
		OnPointerClickHandler(pointerEventData);
	}

	public void OnPointerEnter(PointerEventData pointerEventData) {
		OnPointerEnterHandler(pointerEventData);
	}

	public void OnPointerExit(PointerEventData pointerEventData) {
		OnPointerExitHandler(pointerEventData);
	}

    public void OnPointerDown(PointerEventData pointerEventData) {
        pointerDown = true;
        isBeingDragged = false;

		initialPointerPosition = Input.mousePosition;

        //Check whether to invoke drag or click callback
        StartCoroutine(DraggingTest());
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
        while (timer < timeUntilDragged && pointerDown) {
            timer += Time.deltaTime;
            yield return null;
        }
        isBeingDragged = true;
		if (draggable) {
			yield return OnBeginDrag();
		}

    }

	//Override the native OnDrag so the EventSystems handler doesn't call ScrollRect.OnDrag()
    public void OnDrag(PointerEventData pointerEventData) {}

    private IEnumerator OnBeginDrag() {

		siblingIndex = transform.GetSiblingIndex();

		if (transform.parent != parent.content.transform) {
			ListItem clone = Instantiate<ListItem>(this, transform.parent);
			clone.transform.SetSiblingIndex(siblingIndex);
		}

		transform.SetParent(unmaskedTransform);

		Vector3 initialPosition = transform.position;

        while (pointerDown) {
			//Check if item is being dragged to the top or bottom of the rect -> scroll
			Vector2 draggedPosition;
			if (PointerInScrollRect(out draggedPosition)) {

				float positionFromTop = draggedPosition.y;
				float positionFromBottom = parent.content.sizeDelta.y + draggedPosition.y;

				//Scroll up
				if (Mathf.Abs(positionFromTop) < scrollRegionSize) {
					parent.verticalNormalizedPosition = Mathf.Clamp(
						parent.verticalNormalizedPosition + 0.01f * (positionFromTop + scrollRegionSize), 
						0f, 
						1f
					);
				//Scroll down
				} else if (Mathf.Abs(positionFromBottom) < scrollRegionSize) {
					parent.verticalNormalizedPosition = Mathf.Clamp(
						parent.verticalNormalizedPosition - 0.01f * (positionFromBottom + scrollRegionSize), 
						0f, 
						1f
					);
				}
			}

			CheckIfInDeleteZone(draggedPosition);

			//Make the dragged ListItem follow the cursor
			transform.position = initialPosition + Input.mousePosition - initialPointerPosition;
            yield return null;
        }

		yield return OnDrop();
    }

	public IEnumerator ChangeSizeOverTime(Vector2 newSize, float time) {
		RectTransform rectTransform = GetComponent<RectTransform>();
		if (rectTransform == null) {
			yield break;
		}
		Vector2 initialSize = rectTransform.sizeDelta;

		Vector2 dXY = (newSize - initialSize) / time;
		float timer = 0f;

		while (timer < time) {
			if (rectTransform == null) {
				yield break;
			}
			rectTransform.sizeDelta += dXY * Time.deltaTime;
			timer += Time.deltaTime;
			yield return null;
		}
		rectTransform.sizeDelta = newSize;
		yield break;

	}

	private IEnumerator OnDrop() {
		//See if we need to change the sibling index or delete the ListItem

		//Dragged far from ScrollRect - delete
		if (inDeleteZone) {
			GameObject.Destroy(this.gameObject);
			yield break;
		}

		//Get new position in parent
		Vector2 droppedPosition;
		if (PointerInScrollRect(out droppedPosition)) {
			for (siblingIndex=0; siblingIndex < parent.content.childCount; siblingIndex++) {
				Transform child = parent.content.GetChild(siblingIndex);
				RectTransform childRectTransform = child.GetComponent<RectTransform>();

				if (droppedPosition.y > childRectTransform.anchoredPosition.y - childRectTransform.rect.height / 2f) {
					//Place above this ListItem
					break;
				}
			}
		}


		//No change - ListItem goes back where it was 
		transform.SetParent(parent.content);
		transform.SetSiblingIndex(siblingIndex);

		yield break;
	}


}
