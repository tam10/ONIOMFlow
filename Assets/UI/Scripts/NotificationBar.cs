using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Linq;
using TID = Constants.TaskID;
using EL = Constants.ErrorLevel;

public class NotificationBar : MonoBehaviour {

    // This is a Singleton class. There must only be one instance during runtime
    // Methods are static to improve clarity throughout the project
    // The singleton instance is referenced statically using the getter of "main"
    // Methods in this class must always point to "main" before "_main" to prevent null exceptions
    
    private static NotificationBar _main;
    public static NotificationBar main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<NotificationBar>();
            return _main;
        }
    }

    void Awake() {
		//Prevent more than one instantiation
		if (_main != null && _main != this) {
			Destroy(gameObject);
		}
    }

    
    public int errorCount {
        get {return errorMessages.Count;}
    }
    public int warningCount {
        get {return warningMessages.Count;}
    }
    public int infoCount {
        get {return infoMessages.Count;}
    }

    private List<string> errorMessages = new List<string>();
    private List<string> warningMessages = new List<string>();
    private List<string> infoMessages = new List<string>();

    public TextMeshProUGUI errorNumberText;
    public TextMeshProUGUI warningNumberText;
    public TextMeshProUGUI infoNumberText;

    public PulseIcon errorIcon;
    public PulseIcon warningIcon;
    public PulseIcon infoIcon;
    public Button settingsButton;

    public RectTransform iconBackgroundRect;
    public RectTransform iconBarRect;
    public int fontSize = 12;
    public float innerHeight = 16f;
    public float outerHeight = 24f;

    public TaskBar taskBar;
    public List<TID> activeTasks = new List<TID>();
    public Dictionary<TID, float> tasksProgress = new Dictionary<TID, float>();

    public SingleMessageBar lastMessageBar;


    void Start() {

        //Retina geometry
        int _pixelMultiplier = (Settings.isRetina ? 2 : 1);

        try {
            iconBackgroundRect.localScale = new Vector3(1, _pixelMultiplier, 1);
            taskBar.GetComponent<RectTransform>().localScale = new Vector3(_pixelMultiplier, 1, 1);
            iconBarRect.localScale = new Vector3(_pixelMultiplier, 1, 1);

            settingsButton.onClick.AddListener(SettingsButtonClicked);
        } catch {
            return;
        }

        errorIcon.onClickHandler = ShowLastError;
        warningIcon.onClickHandler = ShowLastWarning;
        infoIcon.onClickHandler = ShowLastInfo;

    }

    void SettingsButtonClicked() {
        StartCoroutine(OpenSettingsUI());
    }

    static void ShowLastError() {
        if (main == null) {return;}
        if (_main.errorMessages.Count == 0) {return;}
        _main.lastMessageBar.ShowErrorMessage(_main.errorMessages.Last());
    }

    static void ShowLastWarning() {
        if (main == null) {return;}
        if (_main.warningMessages.Count == 0) {return;}
        _main.lastMessageBar.ShowErrorMessage(_main.warningMessages.Last());
    }

    static void ShowLastInfo() {
        if (main == null) {return;}
        if (_main.infoMessages.Count == 0) {return;}
        _main.lastMessageBar.ShowErrorMessage(_main.infoMessages.Last());
    }

    IEnumerator OpenSettingsUI() {
        SettingsUI settingsUI = SettingsUI.main;
        settingsUI.Initialise();
        while (!settingsUI.userResponded) {
            yield return null;
        }
    }

    public static void AddError(string message) {
        if (main == null) {return;}
        _main.errorMessages.Add(message);
        _main.errorNumberText.text = _main.errorCount.ToString();
        _main.errorIcon.Pulse();
        _main.lastMessageBar.ShowErrorMessage(message);
    }

    public static void AddWarning(string message) {
        if (main == null) {return;}
        _main.warningMessages.Add(message);
        _main.warningNumberText.text = _main.warningCount.ToString();
        _main.warningIcon.Pulse();
        _main.lastMessageBar.ShowWarningMessage(message);
    }

    public static void AddInfo(string message) {
        if (main == null) {return;}
        _main.infoMessages.Add(message);
        _main.infoNumberText.text = _main.infoCount.ToString();
        _main.infoIcon.Pulse();
        _main.lastMessageBar.ShowInfoMessage(message);
    }

    public static void ClearTask(TID taskID) {
        if (main == null) {return;}
        if (main.activeTasks.Contains(taskID)) {
            _main.tasksProgress.Remove(taskID);
            _main.activeTasks.Remove(taskID);
        }
        UpdateTaskBar();
    }

    public static IEnumerator UpdateClearTask(TID taskID) {
        ClearTask(taskID);
        if (Timer.yieldNow) {yield return null;}
    }

    public static void UpdateTaskBar() {
        if (main == null) {return;}
        int count = _main.activeTasks.Count;
        if (count == 0) {
            _main.taskBar.Clear();
        } else {
            TID currentTaskID = _main.activeTasks[count - 1];
            _main.taskBar.SetProgress(Settings.GetTaskFullName(currentTaskID), _main.tasksProgress[currentTaskID]);
        }

    }

    public static void SetTaskProgress(TID taskID, float progressRatio) {

        if (main.activeTasks.Contains(taskID)) {
            if (progressRatio >= 1f) {
                //Task completed
                ClearTask(taskID);
            } else {
                _main.tasksProgress[taskID] = progressRatio;
            }
        } else {
            _main.tasksProgress[taskID] = progressRatio;
            _main.activeTasks.Add(taskID);
        }

        UpdateTaskBar();
        
    }

    public static IEnumerator UpdateTaskProgress(TID taskID, float progressRatio) {

        SetTaskProgress(taskID, progressRatio);
        if (Timer.yieldNow) {yield return null;}
        
    }

    public static void SetTaskText(string text) {
        main.taskBar.SetText(text);
    }
}
