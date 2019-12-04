using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;
using TMPro;

public class ContextButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public TextMeshProUGUI Text;
    public Button button;
    public Image arrow;

    /// <summary>A reference to the RectTransform to place this button.</summary>
    private RectTransform positionRect;
    private ContextButtonGroup buttonGroup;
    /// <summary>A reference to the RectTransform to place this button.</summary>

    void Awake() {
        positionRect = GetComponent<RectTransform>();
    }

    public ContextButtonGroup AddButtonGroup() {
        buttonGroup = PrefabManager.InstantiateContextButtonGroup(transform);

        arrow.enabled = true;

        //Transform contentTransform = buttonGroup.transform;
        //Transform childTransform = contentTransform.GetChild(0);
        //while (childTransform != null) {
        //    contentTransform = childTransform;
        //    try {
        //        childTransform = contentTransform.GetChild(0);
        //    } catch {
        //        break;
        //    }
        //    
        //}

        return buttonGroup;
    }

    /// <summary>Calculates the position that the button group will be drawn.</summary>
    IEnumerator CalculateButtonGroupPosition() {

        Vector2 anchor = new Vector2(1, 1);
        Vector2 offsetMin = Vector2.zero;

        yield return null;

        //Prevent dialogue from opening off screen

        //Prevent right side overflow
        if (
            ContextMenu.main.positionRect.anchoredPosition.x + 
            2 * buttonGroup.contentRectTransform.rect.width
            > Screen.width
        ) {

            anchor.x = 0;
            offsetMin.x -= buttonGroup.contentRectTransform.rect.width;

            //anchoredPosition.x -= buttonGroupRect.rect.width;
        }

        //Prevent bottom side overflow
        if (ContextMenu.main.positionRect.anchoredPosition.y - buttonGroup.contentRectTransform.rect.height < 0) {

            anchor.y = 0;
            offsetMin.y += buttonGroup.contentRectTransform.rect.width;

            //anchoredPosition.y += buttonGroupRect.rect.height;
        }
        
        buttonGroup.rectTransform.anchorMin = anchor;
        buttonGroup.rectTransform.anchorMax = anchor;
        buttonGroup.rectTransform.offsetMin = offsetMin;

    }
    
    
    public void OnPointerExit(PointerEventData pointerEventData) {
        if (buttonGroup == null) {
            return;
        }
        buttonGroup.StopCoroutine("FadeIn");
        buttonGroup.StartCoroutine("FadeOut");
    }

    public void OnPointerEnter(PointerEventData pointerEventData) {
        if (buttonGroup == null) {
            return;
        }
        StartCoroutine(CalculateButtonGroupPosition());
        buttonGroup.StopCoroutine("FadeOut");
        buttonGroup.StartCoroutine("FadeIn");
    }

}
