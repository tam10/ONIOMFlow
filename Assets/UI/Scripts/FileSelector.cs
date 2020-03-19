using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using System.IO;


public class FileSelector : PopupWindow {

    private static FileSelector _main;
    public new static FileSelector main {
        get {
            if (_main == null) {
                GameObject gameObject = new GameObject("FileSelector");
                _main = (FileSelector)gameObject.AddComponent(typeof(FileSelector));
                _main.StartCoroutine(_main.Create());
            };
            return _main;
        }
    }

	private Scrollbar verticalScrollbar;
	private RectTransform contentHolder;

	[Header("Enabled File Color Block")]
	public ColorBlock enabledFileColorBlock;

	[Header("Disabled File Color Block")]
	public ColorBlock disabledFileColorBlock;

	[Header("Directory Color Block")]
	public ColorBlock directoryColorBlock;


	public TextMeshProUGUI fullPathText;
	public TextMeshProUGUI titleText;
	public TMP_InputField fileNameInput;

	public string confirmedText;

	private string[] filetypes;

	public bool saveMode;

	public float listItemMinHeight = 40f;
	private float listItemHeight;


	private List<string> autocompleteList;

	public override IEnumerator Create() {

		activeTasks++;
        
        AddBackgroundCanvas();
        AddBlurBackground();

        GameObject edge = AddBackground();
        SetRect(edge, 0.2f, 0.4f, 0.8f, 0.6f, 0, -200, 0, 200);

        RectTransform topBarRect = AddTopBar("");
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
		tempText.color = new Color(0f, 0f, 0f, 0f);
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

		//Content Holder
        GameObject navigatorScrollView = AddScrollView(navigatorHolder, "Navigator ScrollView");
        SetRect(navigatorScrollView, 0, 0, 1, 1, 2, 2, -2, -2);
		ScrollRect scrollRect = navigatorScrollView.GetComponent<ScrollRect>();
		contentHolder = scrollRect.content;
		contentHolder.GetComponent<VerticalLayoutGroup>().spacing = 4;

		//Scrollbar
		verticalScrollbar = scrollRect.verticalScrollbar;

		//File Name
        GameObject fileNameInputGO = AddInputField(bottomBarRect, "File Name");
		SetRect(fileNameInputGO, 0, 0.2f, 0.5f, 0.8f, 2, 2, -2, -2);
		fileNameInput = fileNameInputGO.GetComponent<TMP_InputField>();
		fileNameInput.onValueChanged.AddListener(delegate {InputTextChanged();});

		//List Item
		listItemHeight = Mathf.Max(listItemMinHeight, contentRect.rect.height / 10);

		//Colours
		enabledFileColorBlock = ColorScheme.main.enabledFileCB;
		disabledFileColorBlock = ColorScheme.main.disabledFileCB;
		directoryColorBlock = ColorScheme.main.directoryCB;

		activeTasks--;
		yield break;
	}

	public IEnumerator Initialise(bool saveMode, List<string> fileTypes=null, string promptText="") {

		if (isBusy) {yield return null;}

		fullPathText.text = Settings.currentDirectory;

		userResponded = false;
		cancelled = false;

		SetFileTypes(fileTypes ?? new List<string>());
		this.saveMode = saveMode;
		if (!saveMode) {
			fileNameInput.gameObject.SetActive(false);
		}

		if (string.IsNullOrEmpty(promptText)) {
			SetPromptText(saveMode ? "Save File" : "Load File");
		} else {
			SetPromptText(promptText);
		}

		enabledFileColorBlock = ColorScheme.main.enabledFileCB;
		disabledFileColorBlock = ColorScheme.main.disabledFileCB;
		directoryColorBlock = ColorScheme.main.directoryCB;

		yield return Populate();

		Show();
	}

	public IEnumerator Initialise(string promptText, List<string> fileTypes=null) {

		if (isBusy) {yield return null;}

		fullPathText.text = Settings.currentDirectory;

		userResponded = false;
		cancelled = false;

		SetFileTypes(fileTypes ?? new List<string>());
		saveMode = false;
		SetPromptText(promptText);

		enabledFileColorBlock = ColorScheme.main.enabledFileCB;
		disabledFileColorBlock = ColorScheme.main.disabledFileCB;
		directoryColorBlock = ColorScheme.main.directoryCB;

		yield return Populate();

		Show();
	}

	IEnumerator Populate() {

		activeTasks++;

		Clear();

		DirectoryInfo directory = new DirectoryInfo(Settings.currentDirectory);

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
				CheckExtension(filename)
			);

			if (Timer.yieldNow) {yield return null;}
		}

		verticalScrollbar.value = 1f;

		activeTasks--;
	}

	public void AddItem(string textValue, bool isFile, bool isEnabled, string visibleText="") {
		
		ListItem item = PrefabManager.InstantiateListItem(contentHolder);
		RectTransform itemRect = item.GetComponent<RectTransform>();
		itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, listItemHeight);

		//Remove edge
		item.edge.enabled = false;

		GameObject itemGo = item.gameObject;
		itemGo.name = textValue;
		item.text.text = string.IsNullOrWhiteSpace(visibleText) ? textValue : visibleText;

		Button button = item.GetComponent<Button>();
		if (isEnabled) {
			button.interactable = true;
			
			if (isFile) {
				button.onClick.AddListener(delegate {SelectFile(textValue);});
				button.colors = enabledFileColorBlock;

			} else {
				button.onClick.AddListener(delegate {ChangeDirectory(textValue);});
				button.colors = directoryColorBlock;
			}
			
		} else {
			button.interactable = false;
			button.colors = disabledFileColorBlock;
		}
	}

	public void Clear() {
		foreach (Transform child in contentHolder) {
			GameObject.Destroy(child.gameObject);
		}
	}

	public void ChangeDirectory(string newDirectory) {
		if (isBusy) {return;}
		Settings.currentDirectory = Path.GetFullPath(Path.Combine(Settings.currentDirectory, newDirectory));
		fullPathText.text = Settings.currentDirectory;
		fileNameInput.text = string.Empty;
		StartCoroutine(Populate());
	}

	public void SelectFile(string filename) {
		if (isBusy) {return;}
		fullPathText.text = Path.GetFullPath(Path.Combine(Settings.currentDirectory, filename));
		fileNameInput.text = filename;
	}

	bool CheckExtension(string filename) {
		if (filetypes.Length == 0) {return true;}
		string extension = Path.GetExtension(filename).TrimStart('.');
		return filetypes.Any(x => x == extension);
	}

	public void InputTextChanged() {
		fullPathText.text = Path.GetFullPath(Path.Combine(Settings.currentDirectory, fileNameInput.text));
	}

	public void SetFileTypes(List<string> filetypes) {
		this.filetypes = new string[filetypes.Count];
		for (int i = 0; i < filetypes.Count; i++) {
			this.filetypes[i] = filetypes[i].TrimStart('.');
		}
	}

	public void SetPromptText(string text) {
		titleText.SetText(text);
	}

	public override void Cancel() {
		confirmedText = "";
		userResponded = true;
		cancelled = true;
	}
	
	public override void Confirm() {
		if (CheckText(fullPathText.text)) {
			userResponded = true;
			cancelled = false;
		}
	}

	bool CheckText(string text) {

		confirmedText = "";

		if (text == "") {
			Debug.LogFormat("Text empty");
			return false;
		}

		if (!saveMode) {
			if (! File.Exists(text)) {
				Debug.LogFormat("File doesn't exist: '{0}'", text);
				return false;
			}
		}

		if (filetypes.Length > 0) {
			if (! CheckExtension(text)) {
				Debug.LogFormat("File has wrong extension: '{0}'", text);
				return false;
			}
		}

		confirmedText = text;
		return true;
	}

}
