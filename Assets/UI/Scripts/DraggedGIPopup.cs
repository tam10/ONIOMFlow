using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TID = Constants.TaskID;
using EL = Constants.ErrorLevel;
using GIID = Constants.GeometryInterfaceID;

/// <summary>The Dragged Geometry Interface Popup Singleton Class</summary>
/// 
/// <remarks>
/// Derives from PopupWindow
/// Lazy instantiation
/// Activated when a Geometry Interface is dragged and dropped onto another Geometry Interface
/// Displays the available tasks to the user
/// </remarks>
public class DraggedGIPopup : PopupWindow {

	/// <summary>The singleton instance of the Dragged Geometry Interface Popup.</summary>
    private static DraggedGIPopup _main;
	/// <summary>Getter for the singleton instance of the Dragged Geometry Interface Popup.</summary>
    /// <remarks>
    /// Instantiates a singleton instance and runs Create() if _main is null
    /// </remarks>
    public new static DraggedGIPopup main {
        get {
            if (_main == null) {
                GameObject gameObject = new GameObject("DraggedGIPopup");
                _main = (DraggedGIPopup)gameObject.AddComponent(typeof(DraggedGIPopup));
                _main.StartCoroutine(_main.Create());
            };
            return _main;
        }
    }

    /// <summary>The list of available tasks.</summary>
    public List<ListItem> listItems;

	/// <summary>The Transform holding the ListItem GameObjects (available tasks).</summary>
    public Transform tasksTransform;

	/// <summary>How many ListItems should fill the entire window.</summary>
    public int listItemHeightToWindowHeightRatio = 20;

	/// <summary>The ID currently selected Task. This is the Task ID that will be used if the confirm button is pressed.</summary>
    public TID selectedTask;
    
	/// <summary>Creates the Dragged Geometry Interface Popup Window.</summary>
    public override IEnumerator Create() {

		activeTasks++;
        
        AddBackgroundCanvas();
        AddBlurBackground();

        GameObject edge = AddBackground();
        SetRect(edge, 0.4f, 0.4f, 0.7f, 0.7f, -100, -200, 100, 200);

        RectTransform topBarRect = AddTopBar("Copy Geometry");
        AddBottomBar(confirm:true);

        //CONTENT
        AddContentRect();

        //Description
        RectTransform descriptionHolder = AddRect(contentRect, "Description");
        SetRect(descriptionHolder, 0, 0.9f, 1, 1, 8, 8, -8, -8);

        GameObject descriptionText = AddText(descriptionHolder,"DescriptionText", "Select Operation", textAlignmentOptions:TMPro.TextAlignmentOptions.MidlineLeft);
        SetRect(descriptionText, 0, 0.5f, 1f, 0.5f, 8, -15, -8, 15);

        //TASKS
        RectTransform tasksHolder = AddRect(contentRect, "Tasks");
        SetRect(tasksHolder, 0, 0, 1, 0.9f, 8, 8, -8, -8);

        GameObject tasksGO = AddScrollView(tasksHolder, "Current Tasks ScrollView");
        SetRect(tasksGO, 0, 0, 1, 0.9f, 2, 2, -2, -2);
        tasksTransform = tasksGO.GetComponent<ScrollRect>().content;
        tasksTransform.GetComponent<VerticalLayoutGroup>().spacing = 4;
        
        listItems = new List<ListItem>();

        yield break;
    }
      
	/// <summary>Determines which Tasks are available for the combination of Geometry Interfaces. Makes the window visible.</summary>
	/// <param name="draggedGIID">The ID of the Geometry Interface that was dragged.</param>
	/// <param name="enteredGIID">The ID of the Geometry Interface that the Dragged Geometry Interface was dropped onto.</param>
    public void Initialise(GIID draggedGIID, GIID enteredGIID) {
        
        selectedTask = TID.NONE;
        
        //Only CopyGeometry is available if enteredGIID has no atoms
        List<TID> taskIDs = new List<TID> {TID.COPY_GEOMETRY};
        
        //Add all the other tasks if enteredGIID has a Geometry
        if (Flow.main.geometryDict[enteredGIID].geometry != null) {
            taskIDs.AddRange(new List<TID> {
                TID.COPY_POSITIONS,
                TID.UPDATE_PARAMETERS,
                TID.REPLACE_PARAMETERS,
                TID.COPY_PARTIAL_CHARGES,
                TID.COPY_AMBERS,
                TID.ALIGN_GEOMETRIES
            });
        }

        PopulateTasks(taskIDs);

        userResponded = false;
        cancelled = false;

        Show();
    }

	/// <summary>Populates the Task Transform with a list of Task IDs.</summary>
	/// <param name="tasks">The list of Task IDs.</param>
    void PopulateTasks(List<TID> taskIDs) {
        listItems = new List<ListItem>();

        //Make sure the Task Transform is cleared first
        foreach (Transform child in tasksTransform) {
            GameObject.Destroy(child.gameObject);
        }

        foreach (TID taskID in taskIDs) {
            AddTask(taskID);
        }
    }

	/// <summary>Adds a ListItem to a Task Transform using a Task ID.</summary>
	/// <param name="taskID">The Task ID to create the ListItem (Task GameObject) from.</param>
	/// <param name="tasksTransform">The Transform to add the ListItem to.</param>
    ListItem AddListItemToContentTransform(TID taskID, Transform currentTasksTransform) {
        
        float height = contentRect.rect.height / listItemHeightToWindowHeightRatio;

        ListItem listItem = PrefabManager.InstantiateListItem(tasksTransform);
        RectTransform listItemRectTransform = listItem.GetComponent<RectTransform>();
        listItemRectTransform.sizeDelta = new Vector2(listItemRectTransform.sizeDelta.x, height);
        
		//Remove edge
		listItem.edge.enabled = false;
        
        listItem.name = taskID.ToString();
        
        Button button = listItem.GetComponent<Button>();
        
        button.onClick.AddListener(delegate {ButtonSelected(listItem);});

        listItem.draggable = false;
        listItem.value = taskID;
        
        SetTaskText(listItem, taskID);

        return listItem;
    }

	/// <summary>Callback when a ListItem is pressed (selected).</summary>
	/// <param name="listItem">The ListItem (Task GameObject) that was selected.</param>
    public void ButtonSelected(ListItem listItem) {

        selectedTask = (TID)listItem.value;
        //description.text = Settings.GetTaskDescription((TID)listItem.value);
        EventSystem.current.SetSelectedGameObject(listItem.gameObject);
    }

	/// <summary>Sets the visible text of a ListItem.</summary>
	/// <param name="listItem">The ListItem (Task GameObject) to set the text of.</param>
	/// <param name="taskID">The Task ID to derive the text from.</param>
    void SetTaskText(ListItem listItem, TID taskID) {
        try {
            listItem.text.text = Settings.GetTaskFullName(taskID); 
        } catch (ErrorHandler.InvalidTask e) {
            CustomLogger.LogFormat(EL.ERROR, "Could not add task: {0}", Settings.GetTaskFullName(e.TaskID));
            return;
        }
    }
    
	/// <summary>Adds a ListItem to a Task Transform using a Task ID.</summary>
	/// <param name="taskID">The Task ID to create the ListItem (Task GameObject) from.</param>
	/// <param name="tasksTransform">The Transform to add the ListItem to.</param>
    void AddTask(TID taskID) {
        ListItem listItem = AddListItemToContentTransform(taskID, tasksTransform);

        listItems.Add(listItem);
        listItem.gameObject.transform.SetAsLastSibling();
    }

    public override void Hide() {
        canvas.enabled = false;

        listItems.Clear();
        foreach (Transform child in tasksTransform) {
            GameObject.Destroy(child.gameObject);
        }
    }

}
