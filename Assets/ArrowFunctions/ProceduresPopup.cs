using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TID = Constants.TaskID;
using EL = Constants.ErrorLevel;
using COL = Constants.Colour;
using CS = Constants.ColourScheme;
using TMPro;

/// <summary>The Procedures Popup Singleton Class</summary>
/// 
/// <remarks>
/// Derives from PopupWindow
/// Lazy instantiation
/// Activated when an active arrow is clicked
/// Displays the current and available tasks to the user
/// </remarks>
public class ProceduresPopup : PopupWindow {

	/// <summary>The singleton instance of the Procedures Popup.</summary>
    private static ProceduresPopup _main;
	/// <summary>Getter for the singleton instance of the Procedures Popup.</summary>
    /// <remarks>
    /// Instantiates a singleton instance and runs Create() if _main is null
    /// </remarks>
    public new static ProceduresPopup main {
        get {
            if (_main == null) {
                GameObject gameObject = new GameObject("ProceduresPopup");
                _main = (ProceduresPopup)gameObject.AddComponent(typeof(ProceduresPopup));
                _main.StartCoroutine(_main.Create());
            };
            return _main;
        }
    }

	/// <summary>The description text in the content panel.</summary>
    private TextMeshProUGUI description;
	/// <summary>The original description text string.</summary>
    private string originalDescription = "Add, remove or reorder tasks.";

	/// <summary>The Transform that is used while a ListItem is dragged.</summary>
	/// <remarks>Prevents the item from being hidden or affected by the listItemParent.</remarks>
    private Transform unmaskedTransform;
	/// <summary>The ScrollRect to place a ListItem into when it is dropped.</summary>
    private ScrollRect listItemParent;

	/// <summary>The Transform that holds the Current Task ListItems.</summary>
    private Transform currentTasksTransform;
	/// <summary>The Transform that holds the Available Task ListItems.</summary>
    private Transform availableTasksTransform;

	/// <summary>How many ListItems should fill the entire window.</summary>
    public int listItemHeightToWindowHeightRatio = 20;

	/// <summary>The List of TaskIDs when this window is closed.</summary>
    public List<TID> finalTaskIDs;

	/// <summary>Creates the Procedure Popup Window.</summary>
    public override IEnumerator Create() {
        
		activeTasks++;

        AddBackgroundCanvas();
        AddBlurBackground();

        GameObject edge = AddBackground();
        SetRect(edge, 0.2f, 0.2f, 0.8f, 0.8f, 0, 0, 0, 0);

        AddTopBar("Select Tasks...");
        AddBottomBar(confirm:true);

        //CONTENT
        AddContentRect();

        //Description
        RectTransform descriptionHolder = AddRect(contentRect, "Description");
        SetRect(descriptionHolder, 0, 0.9f, 1, 1, 8, 8, -8, -8);

        GameObject descriptionText = AddText(descriptionHolder, "DescriptionText", originalDescription, textAlignmentOptions:TMPro.TextAlignmentOptions.MidlineLeft);
        description = descriptionText.GetComponent<TextMeshProUGUI>();
        SetRect(descriptionText, 0, 0.5f, 1f, 0.5f, 8, -15, -8, 15);

        //TASKS
        RectTransform tasksHolder = AddRect(contentRect, "Tasks");
        SetRect(tasksHolder, 0, 0, 1, 0.9f, 8, 8, -8, -8);
        
        //Current Tasks
        RectTransform currentTasksHolder = AddRect(tasksHolder, "Current Tasks");
        SetRect(currentTasksHolder, 0, 0, 0.5f, 1, 2, 2, -2, -2);

        RectTransform currentTasksTitleHolder = AddRect(currentTasksHolder, "Current Tasks Title", COL.DARK_50);
        SetRect(currentTasksTitleHolder, 0, 0.9f, 1, 1, 2, 2, -2, -2);

        GameObject currentTasksTitleText = AddText(
            currentTasksTitleHolder, 
            "Current Tasks Text", 
            "Current Tasks", 
            fontStyles:TMPro.FontStyles.Italic, 
            textAlignmentOptions:TMPro.TextAlignmentOptions.MidlineLeft
        );
        SetRect(currentTasksTitleText, 0, 0.5f, 1f, 0.5f, 8, -15, -8, 15);

        GameObject currentTasks = AddScrollView(currentTasksHolder, "Current Tasks ScrollView");
        SetRect(currentTasks, 0, 0, 1, 0.9f, 2, 2, -2, -2);
        listItemParent = currentTasks.GetComponent<ScrollRect>();
        currentTasksTransform = listItemParent.content;
        currentTasksTransform.GetComponent<VerticalLayoutGroup>().spacing = 4;
        
        //Available Tasks
        RectTransform availableTasksHolder = AddRect(tasksHolder, "Available Tasks");
        SetRect(availableTasksHolder, 0.5f, 0, 1, 1, 2, 2, -2, -2);

        RectTransform availableTasksTitleHolder = AddRect(availableTasksHolder, "Available Tasks Title", COL.DARK_50);
        SetRect(availableTasksTitleHolder, 0, 0.9f, 1, 1, 2, 2, -2, -2);

        GameObject availableTasksTitleText = AddText(
            availableTasksTitleHolder, 
            "Available Tasks Text", 
            "Available Tasks", 
            fontStyles:TMPro.FontStyles.Italic, 
            textAlignmentOptions:TMPro.TextAlignmentOptions.MidlineLeft
        );
        SetRect(availableTasksTitleText, 0, 0.5f, 1f, 0.5f, 8, -15, -8, 15);

        GameObject availableTasks = AddScrollView(availableTasksHolder, "Available Tasks ScrollView");
        SetRect(availableTasks, 0, 0, 1, 0.9f, 2, 2, -2, -2);
        ScrollRect availableTasksScrollRect = availableTasks.GetComponent<ScrollRect>();
        availableTasksTransform = availableTasksScrollRect.content;
        availableTasksTransform.GetComponent<VerticalLayoutGroup>().spacing = 4;

        //Unmasked transform
        RectTransform unmaskedRect = AddRect(transform, "Unmasked Transform");
        GameObject.Destroy(unmaskedRect.GetComponent<Image>()); 
        unmaskedTransform = unmaskedRect;
        SetRect(unmaskedRect, 0, 0, 1, 1, 0, 0, 0, 0);

        finalTaskIDs = new List<TID>();

        activeTasks--;

        yield break;
    }
    
	/// <summary>Populates the Available and Current Tasks. Makes the window visible.</summary>
	/// <param name="availableTasks">The list of Available Task IDs.</param>
	/// <param name="defaultTasks">The list of Default Task IDs. These will become the Current Task IDs.</param>
    public IEnumerator Initialise(List<TID> availableTasks, List<TID> defaultTasks) {

        yield return PopulateTasks(defaultTasks, currentTasksTransform);
        yield return PopulateAvailableTasks(availableTasks, availableTasksTransform);

        userResponded = false;
        cancelled = false;

        Show();
    }
    
	/// <summary>Populates a Task Transform with a list of Task IDs.</summary>
	/// <param name="tasks">The list of Task IDs.</param>
	/// <param name="tasksTransform">The Transform to populate with ListItems (Task GameObjects).</param>
    IEnumerator PopulateTasks(List<TID> tasks, Transform tasksTransform) {

        //Make sure the Task Transform is cleared first
        foreach (Transform child in tasksTransform) {
            GameObject.Destroy(child.gameObject);
        }

        foreach (TID taskID in tasks) {
            AddTask(taskID, tasksTransform);
            if (Timer.yieldNow) {yield return null;}
        }
    }
    
	/// <summary>Populates the Available Task Transform with a list of Task IDs.</summary>
	/// <param name="tasks">The list of Task IDs.</param>
	/// <param name="tasksTransform">The Transform to populate with ListItems (Task GameObjects).</param>
    IEnumerator PopulateAvailableTasks(List<TID> tasks, Transform tasksTransform) {

        //Make sure the Task Transform is cleared first
        foreach (Transform child in tasksTransform) {
            GameObject.Destroy(child.gameObject);
        }

        List<string> parentClasses = new List<string>();
        Dictionary<string, List<TID>> taskGroups = new Dictionary<string, List<TID>>();

        foreach (TID taskID in tasks) {
            string taskParentClass;
            if (!Settings.taskParentClasses.TryGetValue(taskID, out taskParentClass)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Cannot add Task '{0}'! Task does not have a parent class",
                    taskID
                );
            } else if (!parentClasses.Contains(taskParentClass)) {
                parentClasses.Add(taskParentClass);
                taskGroups[taskParentClass] = new List<TID>();
            } else {
                taskGroups[taskParentClass].Add(taskID);
            }

        }

        foreach (string parentClass in parentClasses) {
            AddGroupHeader(parentClass, tasksTransform);
            foreach (TID taskID in taskGroups[parentClass]) {
                AddTask(taskID, tasksTransform);
                if (Timer.yieldNow) {yield return null;}
            }
        }
    }

	/// <summary>Callback when a ListItem is hovered (selected).</summary>
	/// <param name="listItem">The ListItem (Task GameObject) that was hovered.</param>
    public void ItemHovered(ListItem listItem) {
        //Change the description text
        description.text = Settings.GetTaskDescription((TID)listItem.value);
    }

	/// <summary>Sets the visible text of a ListItem.</summary>
	/// <param name="listItem">The ListItem (Task GameObject) to set the text of.</param>
	/// <param name="taskID">The Task ID to derive the text from.</param>
    void SetTaskText(ListItem listItem, TID taskID) {
        try {
            listItem.text.text = Settings.GetTaskFullName(taskID); 
        } catch {
            CustomLogger.LogFormat(EL.ERROR, "Could not add task: {0}", taskID);
            return;
        }
    }

	/// <summary>Adds a ListItem to a Task Transform using a Task ID.</summary>
	/// <param name="taskID">The Task ID to create the ListItem (Task GameObject) from.</param>
	/// <param name="tasksTransform">The Transform to add the ListItem to.</param>
    ListItem AddListItemToContentTransform(TID taskID, Transform tasksTransform, string text, bool enabled=true) {
        
        //Calculate the height of the ListItem
        float height = contentRect.rect.height / listItemHeightToWindowHeightRatio;

        ListItem listItem = PrefabManager.InstantiateListItem(tasksTransform);
        RectTransform listItemRectTransform = listItem.GetComponent<RectTransform>();
        listItemRectTransform.sizeDelta = new Vector2(listItemRectTransform.sizeDelta.x, height);

        //This is the Transform that will hold the ListItem while it is dragged
        listItem.unmaskedTransform = unmaskedTransform;

        //This is the Scroll Rect to add the ListItem to if it is dropped
        listItem.parent = listItemParent;
        
		//Remove edge
		listItem.edge.enabled = false;
        
        //Add a name for the Editor window (debug)
        listItem.name = taskID.ToString();
        
        if (enabled) {
            //Set the callback for the button
            listItem.OnPointerEnterHandler = (pointerEventData) => ItemHovered(listItem);
        } else {
            Button listItemButton = listItem.GetComponent<Button>();
            listItemButton.interactable = enabled;
            listItemButton.colors = ColorScheme.GetColorSchemeBlock(CS.MEDIUM);

        }
        listItem.draggable = enabled;
        listItem.value = taskID;
        
        listItem.text.text = text;

        return listItem;
    }
    
	/// <summary>Adds a ListItem to a Task Transform using a Task ID.</summary>
	/// <param name="taskID">The Task ID to create the ListItem (Task GameObject) from.</param>
	/// <param name="tasksTransform">The Transform to add the ListItem to.</param>
    void AddTask(TID taskID, Transform tasksTransform) {
        string text;
        try {
            text = Settings.GetTaskFullName(taskID); 
        } catch {
            CustomLogger.LogFormat(EL.ERROR, "Could not add task: {0}", taskID);
            return;
        }
        ListItem listItem = AddListItemToContentTransform(taskID, tasksTransform, text, true);

        listItem.gameObject.transform.SetAsLastSibling();
    }
    
	/// <summary>Adds a ListItem to a Task Transform using a Task ID.</summary>
	/// <param name="taskID">The Task ID to create the ListItem (Task GameObject) from.</param>
	/// <param name="tasksTransform">The Transform to add the ListItem to.</param>
    void AddGroupHeader(string text, Transform tasksTransform) {
        ListItem listItem = AddListItemToContentTransform(TID.NONE, tasksTransform, text, false);

        listItem.gameObject.transform.SetAsLastSibling();
    }

	/// <summary>Get the list of Task IDs in the Current Tasks Transform.</summary>
    IEnumerable<TID> GetFinalTasks() {
        foreach (Transform child in currentTasksTransform) {
            ListItem listItem = child.GetComponent<ListItem>();
            if (listItem == null || listItem.value == null) {
                continue;
            }
            TID taskID = (TID)listItem.value;
            if (taskID != TID.NONE) {
                yield return taskID;
            }
        }
    }

    public override void Confirm() {
        //Get the list of Final Tasks when the confirm button is pressed
        finalTaskIDs = GetFinalTasks().ToList();
        userResponded = true;
        cancelled = false;
        Hide();
    }

}
