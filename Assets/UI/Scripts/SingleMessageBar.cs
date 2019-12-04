using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>The Single Message Bar Class</summary>
/// <remarks>Provides the animations for UI Notifications.</remarks>
public class SingleMessageBar : MonoBehaviour {

    /// <summary>The message to display.</summary>
    public TextMeshProUGUI messageText;
    /// <summary>Reference to the Colour Scheme.</summary>
    private ColorScheme colorScheme;

    /// <summary>The image of the icon to display.</summary>
    public Image iconImage;
    /// <summary>The icon's material.</summary>
    private Material iconMaterial;

    /// <summary>The background Rect object containing the message text.</summary>
    public RectTransform messageBackground;
    /// <summary>The Rect object containing the message text.</summary>
    public RectTransform messageRect;
    /// <summary>The amount in pixels to pad the right side of the message text when animating.</summary>
    private float rightPad = 9f;


    /// <summary>The time to spend expaning the text.</summary>
    private float expandTime = 0.5f;
    /// <summary>The time to spend shrinking the text.</summary>
    private float shrinkTime = 0.5f;
    /// <summary>The time to spend displaying the text.</summary>
    private float holdOpenTime = 4f;

    /// <summary>The dimensions of the message background.</summary>
    private Vector2 sizeDelta;
    /// <summary>The minimum width to set the background.</summary>
    private float minWidth;

    /// <summary>Set true once Awake is called.</summary>
    bool awake = false;

    /// <summary>Is this currently animating?</summary>
    bool animating;
    

    /// <summary>Called by Unity when the Monobehaviour is activated on the scene.</summary>
    void Awake() {
        iconMaterial = iconImage.material;
        minWidth = messageRect.anchoredPosition.x + rightPad;
        messageBackground.anchoredPosition = new Vector2(
            messageBackground.anchoredPosition.x,
            messageBackground.anchoredPosition.y * (Settings.isRetina ? 2f : 1f)
        );
        sizeDelta = new Vector2(0f, messageBackground.sizeDelta.y);

        colorScheme = GameObject.FindObjectOfType<ColorScheme>();
        awake = true;
    }

    /// <summary>Animate an error message.</summary>
    /// <param name="message">The message text to display.</param>
    public void ShowErrorMessage(string message) {
        if (!awake || animating) {return;}
        
        iconImage.color = colorScheme.errorForegroundColor;
        AnimateMessage(message);
    }
    
    /// <summary>Animate a warning message.</summary>
    /// <param name="message">The message text to display.</param>
    public void ShowWarningMessage(string message) {
        if (!awake || animating) {return;}
        
        iconImage.color = colorScheme.warningBackgroundColor;
        AnimateMessage(message);
    }
    
    /// <summary>Animate an info message.</summary>
    /// <param name="message">The message text to display.</param>
    public void ShowInfoMessage(string message) {
        if (!awake || animating) {return;}
        
        iconImage.color = colorScheme.completedForegroundColor;
        AnimateMessage(message);
    }

    /// <summary>Animate a message using the current settings.</summary>
    /// <param name="message">The message text to display.</param>
    private void AnimateMessage(string message) {
        if (animating) {return;}
        string[] splitMessage = message.Split(new[] {'\n'}, System.StringSplitOptions.RemoveEmptyEntries);
        messageText.text = (splitMessage.Length == 0) ? "" : splitMessage[0];

        StartCoroutine(Animate());
    }

    /// <summary>Animation IEnumerator for Coroutines.</summary>
    private IEnumerator Animate() {
        animating = true;
        
        //Expand
        float targetWidth = messageText.textBounds.size.x + minWidth;
        float expansionSpeed = targetWidth / expandTime;
        while (sizeDelta.x < targetWidth) {
            sizeDelta.x += expansionSpeed * Time.deltaTime;
            messageBackground.sizeDelta = sizeDelta;
            yield return null;
        }
        
        //Hold
        float currentAnimationTime = 0f;
        while (currentAnimationTime < holdOpenTime) {
            currentAnimationTime += Time.deltaTime;
            yield return null;
        }
        
        //Shrink
        expansionSpeed = - sizeDelta.x / shrinkTime;
        while (sizeDelta.x > 0f) {
            sizeDelta.x += expansionSpeed * Time.deltaTime;
            messageBackground.sizeDelta = sizeDelta;
            yield return null;
        }

        animating = false;
    }
}
