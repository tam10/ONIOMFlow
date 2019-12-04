using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>The Tooltip Singleton Class</summary>
/// 
/// <remarks>
/// Controls the tooltip that shows when a Geometry Interface is hovered over
/// </remarks>
public class Tooltip : MonoBehaviour {

	private static Tooltip _main;
	/// <value>Gets the reference to the Tooltip Singleton</value>
	public static Tooltip main {
		get {
			if (_main == null) _main = GameObject.FindObjectOfType<Tooltip>();
			return _main;
		}
	}

    /// <value>The RectTransform of the geometry of the Tooltip.</value>
    public RectTransform backgroundRect;
    /// <value>The RectTransform of the mask that hides the text when growing and shrinking.</value>
    public RectTransform viewportRect;
    
    /// <value>The RectTransform of the Text Content of the Tooltip.</value>
    public RectTransform textContentRect;
    /// <value>The Title of this Tooltip.</value>
    public TextMeshProUGUI titleText;
    /// <value>The Info Text of this Tooltip.</value>
    public TextMeshProUGUI infoText;

    /// <value>How long it takes to expand the Tooltip.</value>
    public float expandTimeSeconds;
    /// <value>How long it takes to shrink the Tooltip.</value>
    public float shrinkTimeSeconds; 
    /// <value>How long to wait before expanding the Tooltip.</value>
    public float delayTime;
    /// <value>Is the cursor hovering over the target?</value>
    private bool hovering;

    /// <value>The direction the Tooltip should expand in.</value>
    private Vector2 expansionVector;
    /// <value>The current size of the Tooltip.</value>
    private Vector2 currentSize;
    /// <value>The smallest size tooltip can be.</value>
    private Vector2 minSize; 
    /// <value>The largest size tooltip can be.</value>
    private Vector2 maxSize;
    
    /// <value>The target size. Stop animating when currentSize is this.</value>
    private Vector2 targetSize;
    
    /// <value>Is the Tooltip expanding?</value>
    private bool expanding;
    /// <value>Is the Tooltip shrinking?</value>
    private bool shrinking;

    private float currentAlpha;

	/// <summary>Called when the application starts.</summary>
	/// <remarks>
	/// Called by Unity
	/// </remarks>
    void Awake() {
        //Ensure the Tooltip starts at the minimum size
        backgroundRect.sizeDelta = minSize = new Vector2 (
            Mathf.Max(5f, backgroundRect.rect.width - viewportRect.rect.width),
            Mathf.Max(5f, backgroundRect.rect.height - viewportRect.rect.height)
        );
    }

	/// <summary>Shows the tooltip after delayTime seconds near the cursor position.</summary>
    public void Show(string title, string info) {
        hovering = true;
        StartCoroutine(WarmUp(title, info));
    }

    /// <summary>
    /// Triggers the timer (in delayTime seconds) when hovered over.
    /// </summary>
    /// <remarks>
    /// Starts the AnimateFadeIn Coroutine if the cursor is still hovering.
    /// </remarks>
    private IEnumerator WarmUp(string title, string info) {
        // Start the timer. Keep ticking unless the cursor stops hovering over the target
        float timer = 0f;
        while (timer < delayTime && hovering) {
            timer += Time.deltaTime;
            yield return null;
        }
        // Set the text now (so it doesn't appear prematurely over other targets)
        titleText.text = title;
        infoText.text = info;
        // One extra yield to make sure text content is correctly set.
        yield return null;

        // Call the AnimateFadeIn Coroutine
        if (hovering && backgroundRect != null) {
            StartCoroutine(AnimateFadeIn());
        }
    }

    /// <summary>Hides the tooltip using the AnimateFadeOut Coroutine.</summary>
    public void Hide(bool useExpandAnimation=true, bool useFadeAnimation=false) {
        hovering = false;
        StartCoroutine(AnimateFadeOut());
    }

    /// <summary>Places the tooltip near the cursor and fades it in by increasing its alpha value.</summary>
    private IEnumerator AnimateFadeIn() {
        // Make sure the cavas is visible
        GetComponent<Canvas>().enabled = true;

        // Set these flags to stop AnimateFadeOut
        expanding = true;
        shrinking = false;
        yield return null;

        //Get mouse position and centre of screen
        Vector2 pM = Input.mousePosition;
        Vector2 pS = new Vector2(Screen.width / 2, Screen.height / 2);

        //Stretch offset by maxSize to place unit vector along ellipse
        //Then normalise to place back onto unit circle
        Vector2 offset = ((pS-pM) / textContentRect.sizeDelta).normalized;

        //Scale to ellipse. Sqrt(2) brings position to outer ellipse
        offset *= Mathf.Sqrt(2f) * textContentRect.sizeDelta / 2f;
        backgroundRect.anchoredPosition = pM + offset * (Settings.isRetina ? 2f : 1f);

        //Set the size of the tooltip to the text size
        backgroundRect.sizeDelta = textContentRect.sizeDelta;

        //Set up the Fade In animation
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        float minAlpha = 0f;
        float maxAlpha = 1f;
        float deltaAlpha = (maxAlpha - minAlpha) / expandTimeSeconds;
        float timer = 0f;
        while (timer < (maxAlpha - currentAlpha) / deltaAlpha) {
            currentAlpha += deltaAlpha * Time.deltaTime;
            canvasGroup.alpha = currentAlpha;
            timer += Time.deltaTime;
            //Break if AnimateFadeOut is triggered
            if (!expanding) {yield break;}
            yield return null;
        }
        //Make sure alpha is at its maximum value if this Coroutine finishes
        currentAlpha = canvasGroup.alpha = maxAlpha;
        yield break;
    }

    /// <summary>Fades out the tooltip by decreasing its alpha value.</summary>
    private IEnumerator AnimateFadeOut() {
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();

        // Set these flags to stop AnimateFadeIn
        expanding = false;
        shrinking = true;
        yield return null;

        //Set up the Fade Out animation
        float minAlpha = 0f;
        float maxAlpha = 1f;
        float deltaAlpha = (minAlpha - maxAlpha) / shrinkTimeSeconds;
        float timer = 0f;
        while (timer < (minAlpha - currentAlpha) / deltaAlpha) {
            currentAlpha += deltaAlpha * Time.deltaTime;
            canvasGroup.alpha = currentAlpha;
            timer += Time.deltaTime;
            //Break if AnimateFadeIn is triggered
            if (!shrinking) {yield break;}
            yield return null;
        }
        //Make sure alpha is at its minimum value if this Coroutine finishes
        currentAlpha = canvasGroup.alpha = minAlpha;
        yield break;
    }

    /// <summary>Expand the tooltip to its full size (deprecated).</summary>
    private IEnumerator AnimateExpand() {
        //Compute expansion vector
        //Use (targetSize - [minSize OR maxSize]) not (targetSize - currentSize)
        //This makes the velocity constant (not the time) in case tooltip does
        // multiple animations in quick succession
        
        currentSize = backgroundRect.sizeDelta;

        GetComponent<Canvas>().enabled = true; //Make visible

        maxSize = textContentRect.sizeDelta;

        //Calculate the position on an overlay
        //This is along a line between the cursor and centre of screen
        //Offset vector from cursor position (which becomes the deploy position) 
        // is the point along an ellipse circumscribing the tooltip rect

        //Get mouse position and centre of screen
        Vector2 pM = Input.mousePosition;
        Vector2 pS = new Vector2(Screen.width / 2, Screen.height / 2);

        //Stretch offset by maxSize to place unit vector along ellipse
        //Then normalise to place back onto unit circle
        Vector2 offset = ((pS-pM) / maxSize).normalized;

        //Scale to ellipse. Sqrt(2) brings position to outer ellipse
        offset *= Mathf.Sqrt(2f) * maxSize / 2f;
        backgroundRect.anchoredPosition = pM + offset * (Settings.isRetina ? 2f : 1f);

        targetSize = maxSize;
        expansionVector = (targetSize - minSize) / expandTimeSeconds;

        expanding = true;
        shrinking = false;
        while (currentSize.x < maxSize.x && currentSize.y < maxSize.y) {
            currentSize += expansionVector * Time.deltaTime;
            backgroundRect.sizeDelta = currentSize;
            //Break if AnimateShrink is triggered
            if (!expanding) {yield break;}
            yield return null;
        }
        //Make sure the size of the tooltip is at its minimum value if this Coroutine finishes
        backgroundRect.sizeDelta = currentSize = maxSize;
    }

    /// <summary>Shrink the tooltip then hide it (deprecated).</summary>
    private IEnumerator AnimateShrink() {
        targetSize = minSize;
        expansionVector = (targetSize - maxSize) / shrinkTimeSeconds;
        
        shrinking = true;
        expanding = false;
        while (currentSize.x > minSize.x && currentSize.y > minSize.y) {
            currentSize += expansionVector * Time.deltaTime;
            backgroundRect.sizeDelta = currentSize;

            if (!shrinking) {yield break;}
            yield return null;
        }
        GetComponent<Canvas>().enabled = false;//Hide
        currentSize = minSize;
        backgroundRect.sizeDelta = currentSize;
    }



}