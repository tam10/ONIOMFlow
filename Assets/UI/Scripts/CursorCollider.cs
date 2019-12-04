using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CursorCollider : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

	private CustomCursorManager ccm;
	public FileSelector parent;
	public Button button;

	// Use this for initialization
	void Start () {
		ccm = GameObject.FindObjectOfType<CustomCursorManager>();
	}

	public void OnPointerEnter(PointerEventData pointerEventData) {
		ccm.UseTextCursor();
	}

	public void OnPointerExit(PointerEventData pointerEventData) {
		ccm.UseArrowCursor();
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
