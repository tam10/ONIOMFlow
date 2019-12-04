using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableInterface :  
	MonoBehaviour, 
	IPointerDownHandler,
    IScrollHandler,
    IPointerUpHandler {
   
    public delegate void PointerHandler(PointerEventData eventData);

    public PointerHandler OnPointerDownHandler;
    public PointerHandler OnPointerUpHandler;
    public PointerHandler OnScrollHandler;

    void Awake() {
        //Handlers can be set before Awake() is called, so check this before setting to Pass
        if (OnPointerDownHandler == null) OnPointerDownHandler = Pass;
        if (OnPointerUpHandler == null) OnPointerUpHandler = Pass;
        if (OnScrollHandler == null) OnScrollHandler = Pass;
    }
    private void Pass(PointerEventData pointerEventData) {}
	public void OnPointerDown(PointerEventData pointerEventData) {OnPointerDownHandler(pointerEventData);}
	public void OnPointerUp(PointerEventData pointerEventData) {OnPointerUpHandler(pointerEventData);}
    public void OnScroll(PointerEventData pointerEventData) {OnScrollHandler(pointerEventData);}
}
