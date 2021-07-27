using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using COL = Constants.Colour;
using CS = Constants.ColourScheme;
using EL = Constants.ErrorLevel;
using SIZE = Constants.Size;
using TMPro;

/// <summary>The Popup Window Singleton Class</summary>
/// 
/// <remarks>
/// This class should be derived from to make a new Popup
/// Lazy instantiation
/// </remarks>
public class PopupWindow : MonoBehaviour {

	/// <summary>The singleton instance of the Popup Window or derived class.</summary>
    private static PopupWindow _main;
	/// <summary>Getter for the singleton instance of the Popup Window or derived class.</summary>
    /// <remarks>
    /// Instantiates a singleton instance and runs Create() if _main is null
    /// </remarks>
    public static PopupWindow main {
        get {
            if (_main == null) {
                GameObject gameObject = new GameObject("PopupWindow");
                _main = (PopupWindow)gameObject.AddComponent(typeof(PopupWindow));
                _main.StartCoroutine(_main.Create());
            };
            return _main;
        }
    }

    /// <summary>The number of currently running jobs in this window.</summary>
    public int activeTasks;
    /// <summary>Returns true if the current number of running jobs is greater than 0.</summary>
    public bool isBusy => activeTasks > 0;

    /// <summary>The Canvas that displays this Popup Window.</summary>
    public Canvas canvas;
    /// <summary>The RectTransform of the Background (everything visible) of this Popup Window.</summary>
    public RectTransform backgroundRect;
    /// <summary>The RectTransform of the Content (everything minus the Top and Bottom bars) of this Popup Window.</summary>
    public RectTransform contentRect;
    /// <summary>The RectTransform of the Top Bar of this Popup Window.</summary>
    private RectTransform topBarRect;
    /// <summary>The RectTransform of the Bottom Bar of this Popup Window.</summary>
    private RectTransform bottomBarRect;
    /// <summary>False until the user clicks an OK or Cancel Button.</summary>
    /// <remarks>Use this to break out of loops in external functions.</remarks>
    public bool userResponded;
    /// <summary>True if the user clicked a Cancel Button.</summary>
    public bool cancelled;
    
    
	/// <summary>The function that is called when this Popup is Instantiated.</summary>
    public virtual IEnumerator Create() {
        yield break;
    }

	/// <summary>This overridable method is called when a Confirm Button is clicked.</summary>
    public virtual void Confirm() {
        userResponded = true;
        cancelled = false;
        Hide();
    }
    
	/// <summary>This overridable method is called when a Cancel Button is clicked.</summary>
    public virtual void Cancel() {
        userResponded = true;
        cancelled = true;
        Hide();
    }
    
	/// <summary>This overridable method is called when a Confirm or Cancel Button is clicked.</summary>
    /// <remarks>If overriding, set canvas.enabled to false.</remarks>
    public virtual void Hide() {
        canvas.enabled = false;
    }
    
	/// <summary>This overridable method is used to display the Popup.</summary>
    /// <remarks>If overriding, set canvas.enabled to true.</remarks>
    public virtual void Show() {
        userResponded = false;
        cancelled = false;
        canvas.enabled = true;
    }

	/// <summary>Adds a Top Bar to the Popup Window.</summary>
	/// <remarks>Returns the RectTransform that contains the Top Bar.</remarks>
	/// <param name="title">The string to set the title to.</param>
    protected RectTransform AddTopBar(string title) {

        //Add a background image
        GameObject topBarGO = PrefabManager.InstantiateRectImage(backgroundRect, COL.DARK_50);
        //Set the GameObject name
        topBarGO.name = "Top Bar";
        //Set the position of the Top Bar
        topBarRect = topBarGO.GetComponent<RectTransform>();
        SetRect(topBarRect, 
            // Set Anchor Min to top left (0, 1)
            0, 1,    // x: Left,       y: Top
            // Set Anchor Max to top right (1, 1)
            1, 1,    // x: Right,      y: Top
            // Bottom Left of Rect relative to Anchor Min (8, -68)
            8, -68,  // x: 8px Right,  y: 68px Down
            // Top Right of Rect relative to Anchor Max (-8, -8)
            -8, -8   // x: 8px Left,   y: 8px Down
        );

        // Add the Title
        GameObject titleTextGO = PrefabManager.InstantiateText(
            title, 
            //Add to Rect
            topBarRect, 
            COL.LIGHT_75,
            // Bold and Italic
            (TMPro.FontStyles.Bold | TMPro.FontStyles.Italic),
            // Set to middle left
            TextAlignmentOptions.MidlineLeft
        );

        //Set the GameObject name
        titleTextGO.name = "Title";
        SetRect(titleTextGO, 
            // Set Anchor Min to middle left (0, 0.5f)
            0, 0.5f, // x: Left,       y: Mid
            // Set Anchor Max to middle right (1, 0.5f)
            1, 0.5f, // x: Right,      y: Mid
            // Bottom Left of Rect relative to Anchor Min (6, -15)
            6, -15,  // x: 6px Right,  y: 15px Down
            // Top Right of Rect relative to Anchor Max (-8, 15)
            -6, 15   // x: 6px Left,   y: 15px Up
        );

        //Add a close button
        GameObject closeButtonGO = PrefabManager.InstantiateButton(
            "",
            topBarRect, 
            SIZE.VSMALL, 
            CS.CLOSE_BUTTON
        );

        //Set the GameObject name
        closeButtonGO.name = "Close";

        //Set the image
        Image closeButtonImage = closeButtonGO.GetComponent<Image>();
        closeButtonImage.color = Color.white;
        closeButtonImage.sprite = PrefabManager.main.circleSprite;
        
        SetRect(closeButtonGO, 
            // Set Anchor Min to top right (0, 1)
            1, 1,    // x: Right,      y: Top
            // Set Anchor Max to middle right (1, 1)
            1, 1,    // x: Right,      y: Top
            // Bottom Left of Rect relative to Anchor Min (6, -15)
            -24, -24,// x: 24px Left,  y: 24px Down
            // Top Right of Rect relative to Anchor Max (-8, 15)
            -6, -6   // x: 6px Left,   y: 6px Down
        );

        //Call Cancel() if clicked
        closeButtonGO.GetComponent<Button>().onClick.AddListener(Cancel);

        return topBarRect;
    }

	/// <summary>Adds a Bottom Bar to the Popup Window.</summary>
	/// <remarks>Returns the RectTransform that contains the Bottom Bar.</remarks>
	/// <param name="confirm">Add a confirm button?</param>
	/// <param name="cancel">Add a cancel button?</param>
    protected RectTransform AddBottomBar(bool confirm=true, bool cancel=false) {

        //Add a background image
        GameObject bottomBarGO = PrefabManager.InstantiateRectImage(backgroundRect, COL.DARK_50);
        //Set the GameObject name
        bottomBarGO.name = "Bottom Bar";
        //Set the position of the Bottom Bar
        bottomBarRect = bottomBarGO.GetComponent<RectTransform>();
        SetRect(bottomBarGO, 
            // Set Anchor Min to bottom left (0, 1)
            0, 0,    // x: Left,       y: Bottom
            // Set Anchor Max to bottom right (1, 1)
            1, 0,    // x: Right,      y: Bottom
            // Bottom Left of Rect relative to Anchor Min (8, 8)
            8, 8,  // x: 8px Right,    y: 8px Up
            // Top Right of Rect relative to Anchor Max (-8, 88)
            -8, 88   // x: 8px Left,   y: 88px Up
        );

        //Add a rect to contain the Horizontal Layout Group that groups the buttons
        RectTransform horizontalLayoutGroupRect = AddRect(bottomBarGO.transform, "Horizontal Layout Group");
        
        //Stretch to fill bottomBarGO
        SetRect(horizontalLayoutGroupRect);

        //Add and set up the Horizontal Layout Group
        HorizontalLayoutGroup horizontalLayoutGroup = horizontalLayoutGroupRect.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontalLayoutGroup.childControlHeight = false;
        horizontalLayoutGroup.childControlWidth = false;
        horizontalLayoutGroup.childForceExpandWidth = false;
        horizontalLayoutGroup.padding = new RectOffset(0, 6, 0, 0);
        horizontalLayoutGroup.childAlignment = TextAnchor.MiddleRight;

        //Adding a cancel button?
        if (cancel) {
            //Add a cancel button
            GameObject buttonGO = PrefabManager.InstantiateButton(
                "Cancel",
                horizontalLayoutGroupRect, 
                SIZE.MLARGE, 
                CS.PROMPT_BUTTON,
                TMPro.FontStyles.Bold
            );
            buttonGO.name = "Cancel";
            //This is just to set the size of the button to 120x60
            //It will be repositioned by the Horizontal Layout Group
            SetRect(buttonGO, 
                // Set Anchor Min to top right (1, 1)
                1, 1,    // x: Right,      y: Top
                // Set Anchor Max to middle right (1, 1)
                1, 1,    // x: Right,      y: Top
                // Bottom Left of Rect relative to Anchor Min (6, -15)
                -60, -30,// x: 60px Left,  y: 30px Down
                // Top Right of Rect relative to Anchor Max (-8, 15)
                60, 30   // x: 60px Up,   y: 30px Up
            );
            //Call Cancel() if clicked
            buttonGO.GetComponent<Button>().onClick.AddListener(Cancel);
        }

        //Adding a confirm button?
        if (confirm) {
            //Add a confirm button
            GameObject buttonGO = PrefabManager.InstantiateButton(
                "Confirm",
                horizontalLayoutGroupRect, 
                SIZE.MLARGE, 
                CS.PROMPT_BUTTON,
                TMPro.FontStyles.Bold
            );
            buttonGO.name = "Confirm";
            //This is just to set the size of the button to 120x60
            //It will be repositioned by the Horizontal Layout Group
            SetRect(buttonGO, 
                // Set Anchor Min to top right (1, 1)
                1, 1,    // x: Right,      y: Top
                // Set Anchor Max to middle right (1, 1)
                1, 1,    // x: Right,      y: Top
                // Bottom Left of Rect relative to Anchor Min (6, -15)
                -60, -30,// x: 60px Left,  y: 30px Down
                // Top Right of Rect relative to Anchor Max (-8, 15)
                60, 30   // x: 60px Up,   y: 30px Up
            );
            
            //Call Confirm() if clicked
            buttonGO.GetComponent<Button>().onClick.AddListener(Confirm);
        }

        return bottomBarRect;
    }

	/// <summary>Adds the Content Rect to the Popup Window.</summary>
	/// <remarks>Returns the Content RectTransform.</remarks>
    protected RectTransform AddContentRect() {
        //Add a background image
        GameObject contentGO = PrefabManager.InstantiateRectImage(backgroundRect, COL.DARK_25);
        //Set the GameObject name
        contentGO.name = "Content";
        //Set the position of the Content Rect
        contentRect = contentGO.GetComponent<RectTransform>();

        float offsetMinY = 12 + (bottomBarRect == null ? 0 : bottomBarRect.sizeDelta.y);
        float offsetMaxY = - 12 - (topBarRect == null ? 0 : topBarRect.sizeDelta.y);
        SetRect(contentGO, 
            // Set Anchor Min to bottom left (0, 1)
            0, 0,           // x: Left,       y: Bottom
            // Set Anchor Max to top right (1, 1)
            1, 1,           // x: Right,      y: Top
            // Bottom Left of Rect relative to Anchor Min (8, 8)
            8, offsetMinY,  // x: 8px Right,  y: Height of the Bottom Bar + 12px Up
            // Top Right of Rect relative to Anchor Max (-8, 88)
            -8, offsetMaxY  // x: 8px Left,   y: Height of the Top Bar + 12px Down
        );

        return contentRect;

    }

	/// <summary>Adds a Background Canvas to the Popup Window.</summary>
    protected void AddBackgroundCanvas() {
        canvas = (Canvas)gameObject.AddComponent(typeof(Canvas));
        gameObject.AddComponent(typeof(CanvasScaler));
        gameObject.AddComponent(typeof(GraphicRaycaster));
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }

	/// <summary>Adds a Background to Blur objects behind the Popup Window.</summary>
    protected void AddBlurBackground() {
        GameObject background = PrefabManager.InstantiateRectImage(canvas.transform, COL.LIGHT_100);
        background.name = "Blur Background";
        SetRect(background);

        Image backgroundImage = background.GetComponent<Image>();
        //Set material to blur material
        backgroundImage.material = ColorScheme.main.blurMaterial;

        //Make this object selectable so it intercepts mouse events
        background.AddComponent<Selectable>();
    }

	/// <summary>Adds a Background and Edge to the Popup Window.</summary>
    protected GameObject AddBackground() {
        GameObject edge = PrefabManager.InstantiateRectImage(canvas.transform, COL.BRIGHT_50, false);
        edge.name = "Edge";

        GameObject backgroundGO = PrefabManager.InstantiateRectImage(edge.transform, COL.BRIGHT_25, true);
        backgroundGO.name = "Background";

        backgroundRect = backgroundGO.GetComponent<RectTransform>();
        SetRect(backgroundRect);

        return edge;

    }

	/// <summary>Adds a coloured Rect to a Transform.</summary>
	/// <param name="parent">The Transform to hold the Rect.</param>
	/// <param name="name">The name of the Rect's GameObject.</param>
	/// <param name="color">The colour of the Rect.</param>
	/// <param name="fillCentre">Whether the centre of the image should be filled.</param>
    protected RectTransform AddRect(
        Transform parent, 
        string name, 
        COL color=COL.CLEAR, 
        bool fillCentre=true
    ) {
        GameObject rect = PrefabManager.InstantiateRectImage(parent, color, fillCentre);
        rect.name = name;
        return rect.GetComponent<RectTransform>();
    }

	/// <summary>Adds a Dropdown Menu to a Transform.</summary>
	/// <param name="parent">The Transform to hold the Dropdown.</param>
	/// <param name="name">The name of the Dropdown's GameObject.</param>
	/// <param name="colourScheme">The colour scheme of the Dropdown.</param>
	/// <param name="fontStyles">The style of the Font of each item in the Dropdown.</param>
    protected GameObject AddDropdown(
        Transform parent, 
        string name,
        CS colourScheme=CS.BRIGHT,
		TMPro.FontStyles fontStyles=TMPro.FontStyles.Normal
    ) {
        GameObject dropdownGO = PrefabManager.InstantiateDropdown(parent, colourScheme, fontStyles);
        dropdownGO.name = name;
        return dropdownGO;
    }

	/// <summary>Adds an Input Field to a Transform.</summary>
	/// <param name="parent">The Transform to hold the Input Field.</param>
	/// <param name="name">The name of the Input Field's GameObject.</param>
	/// <param name="colourScheme">The colour scheme of the Input Field.</param>
	/// <param name="fontStyles">The style of the Font of the Input Field.</param>
	/// <param name="contentType">The Content Type of the Input Field - used for validation.</param>
    protected GameObject AddInputField(
        Transform parent, 
        string name,
        CS colourScheme=CS.BRIGHT,
		TMPro.FontStyles fontStyles=TMPro.FontStyles.Normal,
		TMP_InputField.ContentType contentType=TMP_InputField.ContentType.Standard

    ) {
        GameObject inputFieldGO = PrefabManager.InstantiateInputField(parent, colourScheme, fontStyles, contentType);
        inputFieldGO.name = name;
        return inputFieldGO;
    }

	/// <summary>Adds a Scroll View to a Transform.</summary>
	/// <param name="parent">The Transform to hold the Scroll View.</param>
	/// <param name="name">The name of the Scroll View's GameObject.</param>
	/// <param name="colourScheme">The colour scheme of the Scroll View.</param>
    protected GameObject AddScrollView(
        Transform parent,
        string name,
        CS colourScheme=CS.DARK
    ) {
        GameObject scrollViewGO = PrefabManager.InstantiateScrollView(parent, colourScheme);
        scrollViewGO.name = name;
        return scrollViewGO;
    }

    protected Scrollbar AddHorizontalScrollBar(
        Transform parent,
        string name,
        float height,
        COL troughColour=COL.DARK_50,
        COL barColour=COL.LIGHT_75,
        CS colourScheme=CS.DARK
    ) {
        RectTransform scrollbarRect = AddRect(parent, name, troughColour);
        SetRect(scrollbarRect, 0, 0, 1, 0, 0, 0, 0, height);

        RectTransform troughArea = AddRect(scrollbarRect, "Trough");
        SetRect(troughArea, 0, 0, 1, 1, 0, 0, 0, 0);

        RectTransform handleRect = AddRect(troughArea, "Handle", troughColour);
        SetRect(handleRect, 0, 0, 1, 1, 2, 2, -2, -2);

        Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.targetGraphic = handleRect.gameObject.GetComponent<Image>();
        scrollbar.SetDirection(Scrollbar.Direction.LeftToRight, true);

        return scrollbar;
    }

    protected Scrollbar AddVerticalScrollBar(
        Transform parent,
        string name,
        float width,
        COL troughColour=COL.DARK_50,
        COL barColour=COL.LIGHT_75,
        CS colourScheme=CS.DARK
    ) {
        RectTransform scrollbarRect = AddRect(parent, name, troughColour);
        SetRect(scrollbarRect, 1, 1, 0, 1, 0, 0, -width, 0);

        RectTransform troughArea = AddRect(scrollbarRect, "Trough");
        SetRect(troughArea, 0, 0, 1, 1, 0, 0, 0, 0);

        RectTransform handleRect = AddRect(troughArea, "Handle", troughColour);
        SetRect(handleRect, 0, 0, 1, 1, 2, 2, -2, -2);

        Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.targetGraphic = handleRect.gameObject.GetComponent<Image>();
        scrollbar.SetDirection(Scrollbar.Direction.TopToBottom, true);

        return scrollbar;
    }

	/// <summary>Adds a Scroll Text to a Transform.</summary>
	/// <param name="parent">The Transform to hold the Scroll Text.</param>
	/// <param name="name">The name of the Scroll Text's GameObject.</param>
	/// <param name="textString">The initial string value of the Scroll Text.</param>
	/// <param name="colourScheme">The colour scheme of the Scroll Text.</param>
	/// <param name="fontStyles">The style of the Font of the Scroll Text.</param>
    protected GameObject AddScrollText(
        Transform parent,
        string name,
        string textString,
        CS colourScheme=CS.DARK,
		TMPro.FontStyles fontStyles=TMPro.FontStyles.Normal
    ) {
        GameObject scrollViewGO = PrefabManager.InstantiateScrollText(
            textString, 
            parent, 
            colourScheme,
            fontStyles
        );
        scrollViewGO.name = name;
        return scrollViewGO;
    }

	/// <summary>Adds a TMPro.TextMeshProUGUI Text object to a Transform.</summary>
	/// <param name="parent">The Transform to hold the Text.</param>
	/// <param name="name">The name of the Text's GameObject.</param>
	/// <param name="textString">The initial string value of the Text.</param>
	/// <param name="size">The size the Text.</param>
	/// <param name="colour">The colour of the Text.</param>
	/// <param name="fontStyles">The style of the Font of the Text.</param>
	/// <param name="textAlignmentOptions">The Alignment of the Text.</param>
    protected GameObject AddText(
        Transform parent, 
        string name, 
        string text, 
        COL colour=COL.LIGHT_75, 
        TMPro.FontStyles fontStyles=TMPro.FontStyles.Normal,
		TMPro.TextAlignmentOptions textAlignmentOptions=TMPro.TextAlignmentOptions.Center,
		bool autosize=true
    ) {
        GameObject textGO = PrefabManager.InstantiateText(text, parent, colour, fontStyles, textAlignmentOptions, autosize);
        textGO.name = name;
        return textGO;
    }

    protected RectTransform AddTextBox(
        Transform parent, 
        string name, 
        string text, 
        COL textColour=COL.LIGHT_75,
        COL rectColour=COL.DARK_25, 
        bool fillCentre=true,
        TMPro.FontStyles fontStyles=TMPro.FontStyles.Normal,
		TMPro.TextAlignmentOptions textAlignmentOptions=TMPro.TextAlignmentOptions.Center,
		bool autosize=true
    ) {
        RectTransform rect = AddRect(parent, name, rectColour, fillCentre);
        GameObject textGO = PrefabManager.InstantiateText(text, rect, textColour, fontStyles, textAlignmentOptions, autosize);
        textGO.name = name;
        return rect;
    }


	/// <summary>Adds a Notebook to a Transform.</summary>
	/// <param name="parent">The Transform to hold the Notebook.</param>
	/// <param name="name">The name of the Notebook's GameObject.</param>
	/// <param name="colourScheme">The colour scheme of the Notebook.</param>
	/// <param name="tabTitles">The titles of the tabs. There will be a Tab and a Page for each Title</param>
	/// <param name="pages">The pages corresponsing to the tabs.</param>
    protected GameObject AddNotebook(
        Transform parent,
        string name,
        CS colourScheme,
        List<string> tabTitles,
        out List<RectTransform> pages 
    ) {
        //Add a content rect to hold the Notebook
        COL[] colours = ColorScheme.GetColorScheme(colourScheme);
        RectTransform notebookRect = AddRect(parent, name + "Rect", colours[4]);
        SetRect(notebookRect);

        //Add a Rect to hold the Tabs
        GameObject tabsGo = AddScrollView(notebookRect, name + "Tabs", CS.DARK);
        RectTransform tabsRect = tabsGo.GetComponent<RectTransform>();
        SetRect(tabsRect,
            0, 0,
            0.2f, 1,
            6, 6,
            -6, -6
        );

        //Configure the Scroll View
		Transform tabsTransform = tabsGo.GetComponent<ScrollRect>().content;
		tabsTransform.GetComponent<VerticalLayoutGroup>().spacing = 4;

        //Add a Rect to hold the Pages
        RectTransform pagesRect = AddRect(notebookRect, name + "Pages", colours[3], false);
        SetRect(pagesRect,
            0.2f, 0,
            1, 1,
            6, 6,
            -6, -6
        );

        //Clear pages
        pages = new List<RectTransform>();

        //Add a Page and Tab for each title
        foreach (string title in tabTitles) {

            //Add the Page Rect
            RectTransform page = AddRect(pagesRect, title, colours[3], false);
            SetRect(page);

            //Add the Tab (a Button)
            GameObject buttonGO = PrefabManager.InstantiateButton(
                title,
                tabsTransform, 
                SIZE.SMALL, 
                CS.PROMPT_BUTTON,
                TMPro.FontStyles.Bold
            );
            buttonGO.name = title;
            
            //Add the listener delegate for the Button to change the page
            buttonGO.GetComponent<Button>().onClick.AddListener(
                () => {
                    foreach (Transform pageTransform in pagesRect) {
                        pageTransform.gameObject.SetActive(buttonGO.name == pageTransform.name);
                    }
                }
            );
            //Add the Page to the pages list
            pages.Add(page);
            page.gameObject.SetActive(false);
        }

        GameObject notebookGO = notebookRect.gameObject;
        notebookGO.name = name;
        return notebookGO;
    }

    protected GameObject AddToggle(
        Transform parent,
        string name,
        COL colour,
        CS colourScheme
    ) {
        GameObject toggleGo = PrefabManager.InstantiateToggle(parent, colour, colourScheme);
        toggleGo.name = name;
        return toggleGo;
    }

    protected GameObject AddButton(
        Transform parent,
        string name,
        string text,
        COL colour,
        CS colourScheme,
        UnityEngine.Events.UnityAction onClick,
        TextAlignmentOptions textAlignmentOptions=TextAlignmentOptions.Center,
        bool autosize=true
    ) {
        GameObject buttonGo = PrefabManager.InstantiateButton(text, parent, colourScheme:colourScheme, textAlignmentOptions:textAlignmentOptions, autosize:autosize);
        Button button = buttonGo.GetComponent<Button>();
        if (button != null) {
            button.onClick.AddListener(onClick);
        }
        buttonGo.name = name;
        return buttonGo;
    }

    protected GameObject AddTable(
        Transform parent,
        string name,
        int numRows,
        int numCols,
        COL colour,
        CS colourScheme = CS.DARK,
        bool fillCentre=true
    ) {
        RectTransform tableRect = AddRect(parent, name, colour, fillCentre);

        Table table = tableRect.gameObject.AddComponent<Table>();
        table.Initialise(this, tableRect, colourScheme, numRows, numCols);

        return tableRect.gameObject;
    }

    protected GameObject AddScrollTable(
        Transform parent,
        string name,
        int numRows,
        int numCols,
        COL colour,
        CS colourScheme = CS.DARK,
        bool fillCentre=true
    ) {
        RectTransform tableRect = AddRect(parent, name, colour, fillCentre);

        ScrollTable scrollTable = tableRect.gameObject.AddComponent<ScrollTable>();
        scrollTable.Initialise(this, tableRect, colourScheme, numRows, numCols);

        return tableRect.gameObject;
    }

    /// <summary>Set the geometry of a GameObject with a RectTransform</summary>
    /// <param name="anchorMin">The position of the Lower Left Anchor in the parent Rect</param>
    /// <param name="anchorMax">The position of the Upper Right Anchor in the parent Rect</param>
    /// <param name="OffsetMin">The position of the Lower Left of RectTransform from anchorMin</param>
    /// <param name="OffsetMax">The position of the Upper Right of RectTransform from anchorMax</param>
    /// <param name="pivot">The normalised position in this RectTransform that the gameObject rotates around</param>
    protected static void SetRect(
        GameObject gameObject, 
        float anchorMinX = 0,
        float anchorMinY = 0,
        float anchorMaxX = 1,
        float anchorMaxY = 1,
        float offsetMinX = 0f,
        float offsetMinY = 0f,
        float offsetMaxX = 0f,
        float offsetMaxY = 0f,
        float pivotX = 0.5f,
        float pivotY = 0.5f
    ) {
        SetRect(
            gameObject.GetComponent<RectTransform>(),
            anchorMinX, anchorMinY,
            anchorMaxX, anchorMaxY,
            offsetMinX, offsetMinY,
            offsetMaxX, offsetMaxY,
            pivotX, pivotY
        );
    }

    protected static void SetRect(
        RectTransform rectTransform, 
        float anchorMinX = 0,
        float anchorMinY = 0,
        float anchorMaxX = 1,
        float anchorMaxY = 1,
        float offsetMinX = 0f,
        float offsetMinY = 0f,
        float offsetMaxX = 0f,
        float offsetMaxY = 0f,
        float pivotX = 0.5f,
        float pivotY = 0.5f
    ) {
        rectTransform.anchorMin = new Vector2(anchorMinX, anchorMinY);
        rectTransform.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
        rectTransform.pivot = new Vector2(pivotX, pivotY);
        rectTransform.offsetMin = new Vector2(offsetMinX, offsetMinY);
        rectTransform.offsetMax = new Vector2(offsetMaxX, offsetMaxY);

        //Check if this is a Dropdown - need to change its template as well
        TMP_Dropdown dropdown = rectTransform.GetComponent<TMP_Dropdown>();
        if (dropdown != null) {
            GameObject itemGO = dropdown.itemText.transform.parent.gameObject;
            float height = (offsetMaxY - offsetMinY) * 0.7f;
            SetRect(
                itemGO, 
                0, 0.5f,
                1, 0.5f,
                0, -height / 2,
                0, height / 2,
                0.5f, 0.5f
            );
            SetRect(
                itemGO.transform.parent.gameObject, 
                0, 1,
                1, 1,
                0, 0,
                0, height + 8,
                0.5f, 1
            );
        }
    }

    public class Table : MonoBehaviour {
        public int numRows;
        public int numCols;
        public delegate bool ButtonUpdater();
        public delegate string TextUpdater();

        public RectTransform rectTransform;

        public delegate void EventHandler();

        public event EventHandler onUpdate;

        public PopupWindow parent;

        public virtual void Initialise(
            PopupWindow parent, 
            RectTransform tableRect, 
            CS colourScheme,
            int numRows, 
            int numCols
        ) {
            this.parent = parent;
            rectTransform = tableRect;
            this.numRows = numRows;
            this.numCols = numCols;
        }

        public void Refresh() {
            onUpdate?.Invoke();
        }

        public void SetCell(
            RectTransform cell, 
            int rowNum, 
            int colNum,
            int rowSpan=1,
            int colSpan=1,
            int padding=12
        ) {
            if (rowNum >= numRows) {
                CustomLogger.Log(
                    EL.ERROR,
                    $"Cannot create a cell with rowNum ({rowNum}) >= numRows ({numRows})"
                );
            }
            if (colNum >= numCols) {
                CustomLogger.Log(
                    EL.ERROR,
                    $"Cannot create a cell with rowNum ({colNum}) >= numRows ({numCols})"
                );
            } 

            float anchorMinX = (float)colNum/numCols;
            float anchorMaxX = (float)(colNum+colSpan)/numCols;
            float anchorMinY = 1-(float)(rowNum+rowSpan)/numRows;
            float anchorMaxY = 1-(float)(rowNum)/numRows;

            SetRect(
                cell, 
                anchorMinX, anchorMinY,
                anchorMaxX, anchorMaxY,
                padding*2, padding,
                -padding*2, -padding  
            );
        }

        public GameObject CreateBorder( 
            int rowNum,     
            int colNum,
            int rowSpan,
            int colSpan,
            COL colour=COL.CLEAR
        ) {
            RectTransform border = parent.AddRect(
                rectTransform,
                $"[{rowNum}->{rowNum+rowSpan}]_[{colNum}->{colNum+colSpan}]",
                colour,
                false
            );

            border.GetComponent<Image>().pixelsPerUnitMultiplier *= 0.5f;

            SetCell(border, rowNum, colNum, rowSpan, colSpan, 2);

            return border.gameObject;

        }
        
        public GameObject CreateTextCell(
            int rowNum,     
            int colNum, 
            string text, 
            TextUpdater textUpdater=null,
            COL rectColour=COL.BRIGHT_50, 
            COL textColour=COL.BRIGHT_75, 
            bool fillCentre=false,
            FontStyles fontStyles=FontStyles.Normal,
            TextAlignmentOptions textAlignmentOptions=TextAlignmentOptions.MidlineLeft
        ) {

            RectTransform cell = parent.AddTextBox(
                rectTransform,
                $"{rowNum}_{colNum}",
                text,
                rectColour:rectColour,
                textColour:textColour,
                fillCentre:fillCentre,
                fontStyles:fontStyles,
                textAlignmentOptions:textAlignmentOptions,
                autosize:false
            );

            SetCell(cell, rowNum, colNum);

            TextMeshProUGUI tmp = cell.GetComponentInChildren<TextMeshProUGUI>();

            if (tmp != null && textUpdater != null) {
                onUpdate += () => {tmp.text = textUpdater();};
            }


            return cell.gameObject;

        }

        public GameObject CreateButtonCell(
            int rowNum,     
            int colNum, 
            string text, 
            UnityEngine.Events.UnityAction onClick,
            ButtonUpdater buttonEnabled=null,
            COL colour=COL.CLEAR, 
            TextAlignmentOptions textAlignmentOptions=TextAlignmentOptions.MidlineLeft
        ) {

            GameObject cell = parent.AddButton(
                rectTransform,
                $"{rowNum}_{colNum}",
                text,
                colour,
                CS.BRIGHT,
                onClick,
                autosize:false
            );

            SetCell(cell.GetComponent<RectTransform>(), rowNum, colNum);

            Button button = cell.GetComponent<Button>();

            if (button != null && buttonEnabled != null) {
                onUpdate += () => {
                    if (buttonEnabled()) {
                        button.enabled = true;
                        button.GetComponentInChildren<TextMeshProUGUI>().text = "Update";    
                    } else {
                        button.enabled = false;
                        button.GetComponentInChildren<TextMeshProUGUI>().text = "";    

                    }
                };
            }

            return cell;

        }
    }

    public class ScrollTable : Table {

        public delegate void RowUpdateHandler(int index, RectTransform row);

        /// <summary>
        /// The action to take when a row is created
        /// </summary>
        public RowUpdateHandler RowSetter;
        /// <summary>
        /// The action to take when a row is updated
        /// </summary>
        public RowUpdateHandler RowUpdater;
        public RectTransform scrollRect;

        public int numBufferedRows;

        public override void Initialise(
            PopupWindow parent, 
            RectTransform tableRect, 
            CS colourScheme,
            int numRows, 
            int numCols
        ) {
            this.parent = parent;
            rectTransform = tableRect;
            this.numBufferedRows = numRows;
            this.numRows = Mathf.Min(numRows, 25);
            this.numCols = numCols;
            
            GameObject scrollViewGO = parent.AddScrollView(tableRect, $"{name}_ScrollView", colourScheme);
            SetRect(scrollViewGO, 0, 0, 1, 1, 0, 0, -16, 0);
            scrollRect = scrollViewGO.GetComponent<ScrollRect>().content;

        }

        public void InitialiseScrollTable(RowUpdateHandler rowSetter, RowUpdateHandler rowUpdater) {

            this.RowSetter = rowSetter;
            this.RowUpdater = rowUpdater;

            float cellWidth = (scrollRect.rect.width - 4) / numCols;

            for (int rowNum=0; rowNum<this.numRows; rowNum++) {
                RectTransform row = parent.AddRect(scrollRect, $"Row {rowNum}");
                SetRect(row, 0, 0, 1, 0, 2, 0, -2, 60);
                for (int colNum=0; colNum<this.numCols; colNum++) {
                    RectTransform cell = parent.AddRect(row, $"Cell {colNum}");
                    SetRect(cell, 0, 0, 0, 1, cellWidth * colNum, 0, cellWidth * (colNum+1), 0);
                }
                RowSetter(rowNum, row);
            }
        }


    }

}

