using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CS = Constants.ColourScheme;
using COL = Constants.Colour;

/// <summary>The Context Menu Singleton Class</summary>
/// 
/// <remarks>
/// Provides access to the right click menu.
/// Used to add buttons with callbacks.
/// </remarks>
public class ContextMenu : MonoBehaviour, IPointerExitHandler, IPointerEnterHandler {

    /// <summary>The Context Menu Singleton instance</summary>
    private static ContextMenu _main;
    /// <summary>Gets a reference to the Context Menu Singleton instance</summary>
    public static ContextMenu main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<ContextMenu>();
            return _main;
        }
    }

    void Awake() {
		//Prevent instantiation
		if (_main != null && _main != this) {
			Destroy(gameObject);
            return;
		}
        hideable = true;
    }

    /// <summary>A reference to the canvas this menu is drawn on.</summary>
    public Canvas canvas;
    /// <summary>A reference to the Canvas Group of this menu.</summary>
    public CanvasGroup canvasGroup;
    /// <summary>A reference to the RectTransform to place this menu.</summary>
    public RectTransform positionRect;

    /// <summary>A reference to the Transform to add buttons and spacers to.</summary>
    public Transform contentTransform;

    public bool hideable;


    /// <summary>Show the Menu.</summary>
    public void Show() {
        StartCoroutine(_Show());
    }

    /// <summary>Show the Menu.</summary>
    private IEnumerator _Show() {
        yield return CalculatePosition(Input.mousePosition);
        canvas.enabled = true;
    }

    /// <summary>Hide the Menu.</summary>
    public void Hide() {
        canvas.enabled = false;
        canvasGroup.alpha = 1f;
        Clear();
    }

    /// <summary>Remove all the buttons and spacers from the Menu.</summary>
    public void Clear() {
        foreach (Transform child in contentTransform) {
            Destroy(child.gameObject);
        }
    }

    /// <summary>Calculates the position that this menu will be drawn.</summary>
    /// <param name="mousePosition">The position of the cursor.</param>
    IEnumerator CalculatePosition(Vector2 mousePosition) {
        Vector2 anchoredPosition = new Vector2(mousePosition.x, mousePosition.y);

        //Ensure the menu is built before calculating position
        yield return null;
        
        //Prevent dialogue from opening off screen
        //Prevent right side overflow
        if (anchoredPosition.x + positionRect.rect.width > Screen.width) {
            anchoredPosition.x -= positionRect.rect.width;
        }

        //Prevent bottom side overflow
        if (anchoredPosition.y - positionRect.rect.height < 0) {
            anchoredPosition.y += positionRect.rect.height;
        }

        positionRect.anchoredPosition = anchoredPosition;
    }

    /// <summary>
    /// Adds a button to the Menu.
    /// Buttons and spacers are added in order from top to bottom.
    /// </summary>
    /// <param name="callback">The function to execute when this button is clicked.</param>
    /// <param name="buttonText">The text to diplay for this button.</param>
    /// <param name="enabled">Whether this button is interactable or not.</param>
    public ContextButton AddButton(UnityEngine.Events.UnityAction callback, string buttonText, bool enabled) {
        return AddButton(callback, buttonText, enabled, contentTransform);
    }

    /// <summary>
    /// Adds a button to the Menu.
    /// Buttons and spacers are added in order from top to bottom.
    /// </summary>
    /// <param name="callback">The function to execute when this button is clicked.</param>
    /// <param name="buttonText">The text to diplay for this button.</param>
    /// <param name="enabled">Whether this button is interactable or not.</param>
    /// <param name="contextButtonGroup">The Context Button Group to add buttons and spacers to.</param>
    public ContextButton AddButton(UnityEngine.Events.UnityAction callback, string buttonText, bool enabled, ContextButtonGroup contextButtonGroup) {
        return AddButton(callback, buttonText, enabled, contextButtonGroup.contentTransform);
    }

    /// <summary>
    /// Adds a button to the Menu.
    /// Buttons, Button Groups  and spacers are added in order from top to bottom.
    /// </summary>
    /// <param name="callback">The function to execute when this button is clicked.</param>
    /// <param name="buttonText">The text to diplay for this button.</param>
    /// <param name="enabled">Whether this button is interactable or not.</param>
    /// <param name="contentTransform">The Transform to add buttons and spacers to.</param>
    public ContextButton AddButton(UnityEngine.Events.UnityAction callback, string buttonText, bool enabled, Transform contentTransform) {
        ContextButton contextButton = PrefabManager.InstantiateContextButton(contentTransform);

        contextButton.Text.text = buttonText;
        contextButton.button.onClick.AddListener(
            delegate {
                callback(); 
                Hide();
            }
        );

        contextButton.button.interactable = enabled;
        if (enabled) {
            contextButton.Text.color = ColorScheme.GetColor(COL.LIGHT_100);
        } else {
            contextButton.Text.color = ColorScheme.GetColor(COL.LIGHT_25);
        }

        return contextButton;
    }

    /// <summary>
    /// Adds a spacer to the Menu.
    /// Buttons, Button Groups  and spacers are added in order from top to bottom.
    /// </summary>
    public void AddSpacer() {
        PrefabManager.InstantiateSpacerPrefab(contentTransform);
    }

    /// <summary>
    /// Adds a spacer to the Menu.
    /// Buttons, Button Groups  and spacers are added in order from top to bottom.
    /// </summary>
    /// <param name="contentTransform">The Transform to add buttons and spacers to.</param>
    public void AddSpacer(Transform contentTransform) {
        PrefabManager.InstantiateSpacerPrefab(contentTransform);
    }

    /// <summary>
    /// Adds a Button Group to the Menu.
    /// Buttons, Button Groups and spacers are added in order from top to bottom.
    /// </summary>
    /// <param name="buttonText">The text to diplay for this button.</param>
    /// <param name="enabled">Whether this button is interactable or not.</param>
    public ContextButtonGroup AddButtonGroup(string buttonText, bool enabled) {
        ContextButton buttonGroupButton = AddButton(() => {}, buttonText, enabled, contentTransform);
        return buttonGroupButton.AddButtonGroup();
    }

    /// <summary>Called by Unity when the cursor leaves the menu.</summary>
    public void OnPointerExit(PointerEventData pointerEventData) {
        hideable = true;
        StopCoroutine("FadeIn");
        StartCoroutine("FadeOut");
    }

    public void OnPointerEnter(PointerEventData pointerEventData) {
        hideable = false;
        StopCoroutine("FadeOut");
        StartCoroutine("FadeIn");
    }

    IEnumerator FadeOut() {

        if (!hideable) {
            yield break;
        }

        float timer = 0.5f;
        while ((timer -= Time.deltaTime) > 0f) {
            
            if (Input.GetMouseButtonDown(0)) {
                Hide();
                yield break;
            }

            yield return null;
        }

        float alphaValue = canvasGroup.alpha;
        timer = 0.5f;
        while ((timer -= Time.deltaTime) > 0f) {
            canvasGroup.alpha = CustomMathematics.Map(timer, 0.5f, 0f, alphaValue, 0.5f);

            if (Input.GetMouseButtonDown(0)) {
                Hide();
                yield break;
            }

            yield return null;
        }

        timer = 1f;
        while ((timer -= Time.deltaTime) > 0f) {

            if (Input.GetMouseButtonDown(0)) {
                Hide();
                yield break;
            }

            yield return null;
        }
        Hide();
    }

    IEnumerator FadeIn() {

        float alphaValue = canvasGroup.alpha;
        float timer = 0.25f;
        while ((timer -= Time.deltaTime) > 0) {
            canvasGroup.alpha = CustomMathematics.Map(timer, 0.25f, 0f, alphaValue, 1f);
            yield return null;
        }

    }
    
}

public class ButtonGroup : MonoBehaviour {
    
}
