using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;
using COL = Constants.Colour;

public class ContextButtonGroup : MonoBehaviour {
    public Canvas canvas;
    public CanvasGroup canvasGroup;
    public RectTransform rectTransform;
    public RectTransform contentRectTransform;
    public Transform contentTransform;

    private void Hide() {
        canvas.enabled = false;
        ContextMenu contextMenu = ContextMenu.main;

        //Check if cursor is over the ContextMenu
        Vector2 cursorPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(cursorPosition, Vector2.zero);
        if (hit.collider == null || hit.collider.gameObject != contextMenu.gameObject) {
            contextMenu.StopCoroutine("FadeIn");
            contextMenu.StartCoroutine("FadeOut");
        } else {
            contextMenu.StartCoroutine("FadeOut");
            contextMenu.StopCoroutine("FadeIn");
        }
    }

    private void Show() {
        canvas.enabled = true;
    }

    /// <summary>
    /// Adds a button to the Menu.
    /// Buttons, Button Groups  and spacers are added in order from top to bottom.
    /// </summary>
    /// <param name="callback">The function to execute when this button is clicked.</param>
    /// <param name="buttonText">The text to diplay for this button.</param>
    /// <param name="enabled">Whether this button is interactable or not.</param>
    /// <param name="contentTransform">The Transform to add buttons and spacers to.</param>
    public ContextButton AddButton(UnityEngine.Events.UnityAction callback, string buttonText, bool enabled) {
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

    IEnumerator FadeOut() {

        float alphaValue = canvasGroup.alpha;
        float timer = 0.25f;
        while ((timer -= Time.deltaTime) > 0f) {
            canvasGroup.alpha = CustomMathematics.Map(timer, 0.25f, 0f, alphaValue, 0.5f);

            if (Input.GetMouseButtonDown(0)) {
                Hide();
                yield break;
            }

            yield return null;
        }
        Hide();
    }


    IEnumerator FadeIn() {

        Show();

        float alphaValue = canvasGroup.alpha;
        float timer = 0.25f;
        while ((timer -= Time.deltaTime) > 0) {
            canvasGroup.alpha = CustomMathematics.Map(timer, 0.25f, 0f, alphaValue, 1f);
            yield return null;
        }


    }

}
