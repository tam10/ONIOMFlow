using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using CS = Constants.ColourScheme;
using EL = Constants.ErrorLevel;
using COL = Constants.Colour;
using Size = Constants.Size;
using OLID = Constants.OniomLayerID;
using TMPro;


public class SettingsUI : PopupWindow {

    private static SettingsUI _main;
    public new static SettingsUI main {
        get {
            if (_main == null) {
                GameObject gameObject = new GameObject("SettingsUI");
                _main = (SettingsUI)gameObject.AddComponent(typeof(SettingsUI));
                _main.StartCoroutine(_main.Create());
            };
            return _main;
        }
    }
    
    public override IEnumerator Create() {
        
        AddBackgroundCanvas();
        AddBlurBackground();

        GameObject edge = AddBackground();
        SetRect(edge, 0, 0, 1, 1, 250, 150, -150, -150);

        AddTopBar("Settings");
        AddBottomBar(confirm:true);

        AddContentRect();

        List<string> titles = new List<string> {
            "Logging",
            "Graphics"
        };
        List<RectTransform> pages = new List<RectTransform> ();

        AddNotebook(
            contentRect,
            "Notebook",
            CS.BRIGHT,
            titles,
            out pages
        );

        if (Timer.yieldNow) {yield return null;}

        // LOGGING PAGE
        RectTransform loggingPage = pages[titles.IndexOf("Logging")];
        RectTransform loggingRect = AddScrollView(loggingPage, "Logging ScrollView").GetComponent<RectTransform>();
        SetRect(loggingRect);
        AddLoggingPage(loggingRect);
        
        if (Timer.yieldNow) {yield return null;}

        // GRAPHICS PAGE
        RectTransform graphicsPage = pages[titles.IndexOf("Graphics")];
        RectTransform graphicsRect = AddScrollView(graphicsPage, "Graphics ScrollView").GetComponent<RectTransform>();
        SetRect(graphicsRect);
        AddGraphicsPage(graphicsRect);

        yield break;

    } 

    private void AddLoggingPage(RectTransform loggingRect) {

        // LOGGING MODE

        RectTransform loggingModeRect = AddRect(loggingRect, "LoggingMode", COL.DARK_25);
        SetRect(
            loggingModeRect,
            0, 1,
            1, 1,
            2, -86,
            -2, -24
        );

        GameObject loggingModeText = AddText(
            loggingModeRect, 
            "LoggingModeText", 
            "Logging Level", 
            colour:COL.LIGHT_75,
            textAlignmentOptions:TextAlignmentOptions.MidlineLeft
        );
        SetRect(
            loggingModeText,
            0, 0.5f,
            0, 0.5f,
            14, -30,
            254, 30
        );

        GameObject loggingModeDropdownGO = AddDropdown(loggingModeRect, "LoggingModeDropdown", CS.MEDIUM);
        SetRect(
            loggingModeDropdownGO,
            1, 0.5f,
            1, 0.5f,
            -254, -30,
            -14, 30
        );
        
        TMP_Dropdown loggingModeDropdown = loggingModeDropdownGO.GetComponent<TMP_Dropdown>();

        List<EL> errolLevels = Constants.ErrorLevelMap.Values.ToList();

        loggingModeDropdown.AddOptions(
            errolLevels
                .Select(x => new TMP_Dropdown.OptionData(Constants.ErrorLevelMap[x]))
                .ToList()
        );

        loggingModeDropdown.onValueChanged.AddListener(
            (int index) => {
                EL loggingLevel = Constants.ErrorLevelMap[loggingModeDropdown.options[index].text];
                CustomLogger.logErrorLevel = loggingLevel;
            }
        );

        loggingModeDropdown.value = errolLevels.IndexOf(CustomLogger.logErrorLevel);
    }

    private void AddGraphicsPage(RectTransform graphicsRect) {

        // LINE THICKNESSES
        RectTransform lineThicknessRect = AddRect(graphicsRect, "LineThickness", COL.DARK_25);

        SetRect(
            lineThicknessRect,
            0, 1,
            1, 1,
            2, -254,
            -2, -24
        );

        GameObject lineThicknessText = AddText(
            lineThicknessRect, 
            "LineThicknessText", 
            "ONIOM Layer Line Thickness", 
            colour:COL.LIGHT_75,
            textAlignmentOptions:TextAlignmentOptions.MidlineLeft
        );
        SetRect(
            lineThicknessText,
            0, 1,
            1, 1,
            24, -52,
            -24, -12
        );

        //Model Line Thickness
        AddThicknessSetting(lineThicknessRect, OLID.MODEL, -68);
        AddThicknessSetting(lineThicknessRect, OLID.INTERMEDIATE, -120);
        AddThicknessSetting(lineThicknessRect, OLID.REAL, -172);

        // ADVANCED
        RectTransform advancedRect = AddRect(graphicsRect, "Advanced", COL.DARK_25);
        SetRect(
            advancedRect,
            0, 1,
            1, 1,
            2, -378,
            -2, -270
        );

        GameObject advancedText = AddText(
            advancedRect, 
            "AdvancedText", 
            "Advanced Options", 
            colour:COL.LIGHT_75,
            textAlignmentOptions:TextAlignmentOptions.MidlineLeft
        );
        SetRect(
            advancedText,
            0, 1,
            1, 1,
            24, -52,
            -24, -12
        );

        RectTransform parallelRect = AddRect(advancedRect, "ParallelLineDrawer", COL.CLEAR);
        SetRect(
            parallelRect,
            0, 1,
            1, 1,
            2, -126,
            -2, -68
        );
        GameObject parallelText = AddText(
            parallelRect, 
            "ParallelLineDrawerText", 
            "Use Multi-core LineDrawer", 
            colour:COL.LIGHT_75,
            textAlignmentOptions:TextAlignmentOptions.MidlineLeft
        );
        SetRect(
            parallelText,
            0, 0.5f,
            0, 0.5f,
            18, -15,
            418, 15
        );

        GameObject parallelToggleGO = AddToggle(
            parallelRect,
            "ParallelLineDrawerToggle",
            COL.LIGHT_100,
            CS.MEDIUM
        );
        SetRect(
            parallelToggleGO,
            1, 0.5f,
            1, 0.5f,
            -58, -20,
            -18, 20
        );
        Toggle parallelToggle = parallelToggleGO.GetComponent<Toggle>();
        parallelToggle.onValueChanged.AddListener(
            (bool result) => {
                Settings.useParallelLineDrawer = result;
            }
        );
        parallelToggle.isOn = Settings.useParallelLineDrawer;

    }

    private void AddThicknessSetting(RectTransform lineThicknessRect, OLID layerID, float startY) {

        string layerName = Constants.OniomLayerIDStringMap[layerID];

        RectTransform thicknessRect = AddRect(lineThicknessRect, $"{layerName}Thickness", COL.CLEAR);
        SetRect(
            thicknessRect,
            0, 1,
            1, 1,
            2, startY - 40,
            -2, startY
        );
        GameObject thicknessText = AddText(
            thicknessRect, 
            $"{layerName}ThicknessText", 
            $"{layerName} Layer", 
            colour:COL.LIGHT_75,
            textAlignmentOptions:TextAlignmentOptions.MidlineLeft
        );
        SetRect(
            thicknessText,
            0, 0.5f,
            0, 0.5f,
            18, -15,
            418, 15
        );

        GameObject thicknessInputGO = AddInputField(
            thicknessRect,
            $"{layerName}ThicknessInput",
            CS.MEDIUM,
            contentType: TMP_InputField.ContentType.DecimalNumber
        );
        SetRect(
            thicknessInputGO,
            1, 0.5f,
            1, 0.5f,
            -218, -15,
            -18, 15
        );
        TMP_InputField thicknessInput = thicknessInputGO.GetComponent<TMP_InputField>();
        thicknessInput.onEndEdit.AddListener(
            (string inputString) => {
                Settings.layerLineThicknesses[layerID] = ValidateInputFloat(
                    thicknessInput, 
                    inputString, 
                    0.1f, 
                    5f,
                    Settings.layerLineThicknesses[layerID]
                );
            }
        );
        thicknessInput.text = $"{Settings.layerLineThicknesses[layerID]}";
    }

    public void Initialise() {
        Show();
    }

    private IEnumerator ShowError(string titleString, string errorMessage) {
        MultiPrompt multiPrompt = MultiPrompt.main;
        multiPrompt.Initialise(
            titleString, 
            errorMessage, 
            new ButtonSetup("OK", () => {})
        );

        while (!multiPrompt.userResponded) {
            yield return null;
        }

		multiPrompt.Hide();
    }

    private float ValidateInputFloat(TMP_InputField inputField, string inputString, float min, float max, float valueOnError) {
        float value = 0f;
        if (!float.TryParse(inputString, out value) || value < min || value > max) {
            StartCoroutine(ShowError("Invalid value", $"Value must be a number between {min} and {max}"));
            inputField.text = $"{valueOnError}";
            return valueOnError;
        }

        return value;
    }
}
