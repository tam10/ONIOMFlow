using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;
using TMPro;
using System.IO;

public class ProjectSelector : PopupWindow {

    private static ProjectSelector _main;
    public new static ProjectSelector main {
        get {
            if (_main == null) {
                GameObject gameObject = new GameObject("ProjectSelector");
                _main = (ProjectSelector)gameObject.AddComponent(typeof(ProjectSelector));
                _main.StartCoroutine(_main.Create());
            };
            return _main;
        }
    }


	private RectTransform contentHolder;

	[Header("File Color Block")]
	public ColorBlock fileColorBlock;

	[Header("Directory Color Block")]
	public ColorBlock directoryColorBlock;


	public TextMeshProUGUI fullPathText;
	public TextMeshProUGUI titleText;
	public TMP_InputField projectNameInput;

	public string confirmedText;

	public float listItemMinHeight = 40f;
	private float listItemHeight;

    bool doubleClickEligible;
    float doubleClickThreshold = 0.2f;

    private string currentDirectory;
    public string projectPath;


	private List<string> autocompleteList;

	public override IEnumerator Create() {

		activeTasks++;

        PlatformID platformID = Environment.OSVersion.Platform;
        bool isWindows = (platformID != PlatformID.Unix && platformID != PlatformID.MacOSX);
    	projectPath = isWindows ? Environment.SpecialFolder.Personal.ToString() : Environment.GetEnvironmentVariable("HOME");
        
        AddBackgroundCanvas();

        GameObject edge = AddBackground();
        SetRect(edge, 0.4f, 0.4f, 0.6f, 0.6f, -100, -200, 100, 200);

        RectTransform topBarRect = AddTopBar("Load Project");
		titleText = topBarRect.GetComponentInChildren<TextMeshProUGUI>();
        RectTransform bottomBarRect = AddBottomBar(confirm:true, cancel:true);

        //CONTENT
        AddContentRect();

        //Full Path 
        RectTransform fullPathHolder = AddRect(contentRect, "Full Path");
        SetRect(fullPathHolder, 0, 0.85f, 1, 1, 2, 0, -2, 0);

		//Calculate the font size to use for the full path
		//Doing this because we can't use a Scroll Rect with autoSize
		GameObject tempTextGO = AddText(fullPathHolder, "Temp", "Temp");
        SetRect(tempTextGO, 0, 0.25f, 1, 0.75f, 2, 0, -2, 0);
		TextMeshProUGUI tempText = tempTextGO.GetComponent<TextMeshProUGUI>();
		tempText.enableAutoSizing = true;
		yield return null;
		float fontSize = tempText.fontSize;
		GameObject.Destroy(tempTextGO);

        GameObject fullPathTextRect = AddScrollText(fullPathHolder, "Full Path", "");
        fullPathText = fullPathTextRect.GetComponent<ScrollText>().text;
		fullPathText.fontSize = fontSize;
        SetRect(fullPathTextRect, 0, 0.1f, 1, 0.9f, 2, 0, -2, -0);

        
        //Navigator
        RectTransform navigatorHolder = AddRect(contentRect, "Navigator");
        SetRect(navigatorHolder, 0, 0, 1, 0.85f, 2, 2, -2, -2);

        GameObject navigatorScrollView = AddScrollView(navigatorHolder, "Navigator ScrollView");
        SetRect(navigatorScrollView, 0, 0, 1, 1, 2, 2, -2, -2);
		contentHolder = navigatorScrollView.GetComponent<ScrollRect>().content;
		contentHolder.GetComponent<VerticalLayoutGroup>().spacing = 4;

		//Directory Name
        GameObject directoryNameInputGO = AddInputField(bottomBarRect, "Directory Name");
		SetRect(directoryNameInputGO, 0, 0.2f, 0.5f, 0.8f, 2, 2, -2, -2);
		projectNameInput = directoryNameInputGO.GetComponent<TMP_InputField>();
		//projectNameInput.onValueChanged.AddListener(delegate {InputTextChanged();});

		//List Item
		listItemHeight = Mathf.Max(listItemMinHeight, contentRect.rect.height / 10);

		activeTasks--;
		yield break;
	}

	public IEnumerator Initialise() {

		if (isBusy) {yield return null;}

        currentDirectory = projectPath;
		fullPathText.text = projectPath;

		userResponded = false;
		cancelled = false;


		fileColorBlock = ColorScheme.main.disabledFileCB;
		directoryColorBlock = ColorScheme.main.directoryCB;

		yield return Populate();
	}

	IEnumerator Populate() {

		activeTasks++;

		Clear();

		DirectoryInfo directory = new DirectoryInfo(currentDirectory);

		string[] allFiles = directory
			.GetFiles()
			.Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
			.Select(f => f.Name)
			.ToArray();
		string[] allDirectories = directory
			.GetDirectories()
			.Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
			.Select(f => f.Name)
			.ToArray();

		AddItem("..", false, true);

		for (int i = 0; i < allDirectories.Length; i++) {
			
			string directoryName = allDirectories[i];
			AddItem(
				Path.GetFileName(directoryName) + Path.DirectorySeparatorChar, 
				false, 
				true
			);

			if (Timer.yieldNow) {yield return null;}
		}

		for (int i = 0; i < allFiles.Length; i++) {
			
			string filename = allFiles[i];
			AddItem(
				Path.GetFileName(filename), 
				true,
                false
			);

			if (Timer.yieldNow) {yield return null;}
		}

		activeTasks--;
	}

	public void AddItem(string textValue, bool isFile, bool isEnabled) {
		
		ListItem item = PrefabManager.InstantiateListItem(contentHolder);
		RectTransform itemRect = item.GetComponent<RectTransform>();
		itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, listItemHeight);

		//Remove edge
		item.edge.enabled = false;

		GameObject itemGo = item.gameObject;
		itemGo.name = textValue;
		item.text.text = textValue;

		Button button = item.GetComponent<Button>();
		if (isEnabled) {
			button.interactable = true;
			
			if (!isFile) {
				button.onClick.AddListener(delegate {DirectoryClicked(textValue);});
				button.colors = directoryColorBlock;
			}
			
		} else {
			button.interactable = false;
			button.colors = fileColorBlock;
		}
	}

	public void Clear() {
		foreach (Transform child in contentHolder) {
			GameObject.Destroy(child.gameObject);
		}
	}

    private void DirectoryClicked(string newDirectory) {
        if (doubleClickEligible) {
            ChangeDirectory(newDirectory);
            doubleClickEligible = false;
        } else {
            StartCoroutine(CheckClick(newDirectory));
        }
    }

    private IEnumerator CheckClick(string newDirectory) {
        doubleClickEligible = true;
        float timer = 0f;
        while (timer < doubleClickThreshold) {
            if (!doubleClickEligible) {
                yield break;
            }
            timer += Time.deltaTime;
            yield return null;
        }
        SelectDirectory(newDirectory);
        doubleClickEligible = false;
    }

	public void SelectDirectory(string directoryName) {
		if (isBusy) {return;}
		projectNameInput.text = directoryName;
	}

	public void ChangeDirectory(string newDirectory) {
		if (isBusy) {return;}
		currentDirectory = Path.GetFullPath(Path.Combine(currentDirectory, newDirectory));
		fullPathText.text = currentDirectory;
		projectNameInput.text = string.Empty;
		StartCoroutine(Populate());
	}

	//public void InputTextChanged() {
	//	fullPathText.text = Path.GetFullPath(Path.Combine(projectPath, projectNameInput.text));
	//}

	public override void Cancel() {
		confirmedText = "";
		userResponded = true;
		cancelled = true;
	}
	
	public override void Confirm() {
		string finalPath = Path.GetFullPath(Path.Combine(currentDirectory, projectNameInput.text));
		if (CheckText(finalPath)) {
			userResponded = true;
			cancelled = false;
		}
        projectPath = finalPath;
	}

	bool CheckText(string text) {

		confirmedText = "";

		if (text == "") {
			Debug.LogFormat("Text empty");
			return false;
		}

		confirmedText = text;
		return true;
	}

}
