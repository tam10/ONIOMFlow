using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using System.Linq;
using CS = Constants.ColourScheme;
using EL = Constants.ErrorLevel;
using COL = Constants.Colour;
using OLID = Constants.OniomLayerID;
using GIID = Constants.GeometryInterfaceID;
using GIS = Constants.GeometryInterfaceStatus;
using TID = Constants.TaskID;
using ChainID = Constants.ChainID;
using TMPro;


/// <summary>The Output Updater Singleton Class</summary>
/// 
/// <remarks>
/// Derives from PopupWindow
/// Lazy instantiation
/// Activated when a Geometry is updated from the Interface
/// </remarks>

public class VibrationalAnalysisWindow : PopupWindow {
    
	/// <summary>The singleton instance of the Vibrational Analysis Window.</summary>
    private static VibrationalAnalysisWindow _main;
	/// <summary>Getter for the singleton instance of the Vibrational Analysis Window.</summary>
    /// <remarks>
    /// Instantiates a singleton instance and runs Create() if _main is null
    /// </remarks>
    public new static VibrationalAnalysisWindow main {
        get {
            if (_main == null) {
                GameObject gameObject = new GameObject("VibrationalAnalysisWindow");
                _main = (VibrationalAnalysisWindow)gameObject.AddComponent(typeof(VibrationalAnalysisWindow));
                _main.StartCoroutine(_main.Create());
            };
            return _main;
        }
    }

    Geometry geometry;
    LineDrawer lineDrawer;

    ScrollTable scrollTable;

    int numCols = 7;

    public override IEnumerator Create() {

        activeTasks++;
        
        AddBackgroundCanvas();

        GameObject edge = AddBackground();
        SetRect(edge, 0, 0, 1, 1, 250, 150, -150, -150);
        
        AddTopBar("Update Geometry");
        RectTransform bottomBarRect = AddBottomBar(confirm:true, cancel:true);

        AddContentRect();

        if (Timer.yieldNow) {yield return null;}

        activeTasks--;
    
    }


    public IEnumerator Initialise(Geometry geometry, LineDrawer lineDrawer) {

        this.geometry = geometry;
        this.lineDrawer = lineDrawer;

        GameObject scrollTableGO = AddScrollTable(
            contentRect, 
            "NormalModes", 
            geometry.gaussianResults.numModes, 
            numCols,
            COL.DARK_75
        );
        SetRect(scrollTableGO);

        scrollTable = scrollTableGO.GetComponent<ScrollTable>();
        yield return null;

        scrollTable.InitialiseScrollTable(RowSetter, RowUpdater);
    }

    private void RowSetter(int modeIndex, RectTransform row) {

        // Mode Index
        Transform indexCell = row.GetChild(0);
        RectTransform indexRect = AddTextBox(indexCell, "Index", $"{modeIndex+1}");
        SetRect(indexRect);

        // Frequency
        Transform freqCell = row.GetChild(1);
        RectTransform freqRect = AddTextBox(freqCell, "Frequency", $"{geometry.gaussianResults.frequencies[modeIndex]}");
        SetRect(freqRect);

        // Intensity
        Transform intensityCell = row.GetChild(2);
        RectTransform intensityRect = AddTextBox(intensityCell, "Intensity", $"{geometry.gaussianResults.intensities[modeIndex]}");
        SetRect(intensityRect);

        // Reduced Mass
        Transform reducedMassCell = row.GetChild(3);
        RectTransform reducedMassRect = AddTextBox(reducedMassCell, "ReducedMass", $"{geometry.gaussianResults.reducedMasses[modeIndex]}");
        SetRect(reducedMassRect);

        // Model Percent
        Transform modelPercentCell = row.GetChild(4);
        RectTransform modelPercentRect = AddTextBox(modelPercentCell, "ModelPercent", $"{geometry.gaussianResults.modelPercents[modeIndex]}");
        SetRect(modelPercentRect);

        // Real Percent
        Transform realPercentCell = row.GetChild(5);
        RectTransform realPercentRect = AddTextBox(realPercentCell, "ModelPercent", $"{geometry.gaussianResults.realPercents[modeIndex]}");
        SetRect(realPercentRect);

        // Select Button
        Transform selectCell = row.GetChild(6);
        GameObject selectGO = AddButton(
            selectCell, 
            "Select",
            "Select",
            COL.CLEAR, 
            CS.BRIGHT,
            () => StartCoroutine(SelectMode(modeIndex))
        );
        SetRect(selectGO);
    }

    public void RowUpdater(int modeIndex, RectTransform row) {

        // Mode Index
        TextMeshProUGUI indexText = row.GetChild(0).GetComponentInChildren<TextMeshProUGUI>();
        indexText.text = $"{modeIndex+1}";

        // Frequency
        TextMeshProUGUI freqText = row.GetChild(1).GetComponentInChildren<TextMeshProUGUI>();
        indexText.text = $"{geometry.gaussianResults.frequencies[modeIndex]}";

        // Intensity
        TextMeshProUGUI intensityText = row.GetChild(2).GetComponentInChildren<TextMeshProUGUI>();
        intensityText.text = $"{geometry.gaussianResults.intensities[modeIndex]}";

        // Reduced Mass
        TextMeshProUGUI reducedMassText = row.GetChild(3).GetComponentInChildren<TextMeshProUGUI>();
        reducedMassText.text = $"{geometry.gaussianResults.reducedMasses[modeIndex]}";

        // Model Percent
        TextMeshProUGUI modelPercentText = row.GetChild(4).GetComponentInChildren<TextMeshProUGUI>();
        modelPercentText.text = $"{geometry.gaussianResults.modelPercents[modeIndex]}";

        // Real Percent
        TextMeshProUGUI realPercentText = row.GetChild(5).GetComponentInChildren<TextMeshProUGUI>();
        realPercentText.text = $"{geometry.gaussianResults.realPercents[modeIndex]}";

        // Select Button
        Button selectButton = row.GetChild(6).GetComponent<Button>();
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => SelectMode(modeIndex));
    }

    public IEnumerator SelectMode(int modeIndex) {

        if (isBusy) {
            yield break;
        }
        
        activeTasks++;
        
        lineDrawer.animateVibrations = false;

        NotificationBar.SetTaskProgress(TID.ANIMATE_NORMAL_MODE, 0);

        yield return geometry.gaussianResults.GetVibrationalMode(modeIndex);
        float3[] normalModes = geometry.gaussianResults.normalMode;

        int progress = 0;
        int size = geometry.size;
        foreach ((AtomID atomID, int atomIndex) in geometry.atomMap) {
            lineDrawer.UpdateVibrationVector(atomID, normalModes[atomIndex]);
            if (Timer.yieldNow) {
                yield return null;
            }
            NotificationBar.SetTaskProgress(TID.ANIMATE_NORMAL_MODE, CustomMathematics.Map((float)progress, 0, size, 0.2f, 1f));
            progress++;
        }

        NotificationBar.ClearTask(TID.ANIMATE_NORMAL_MODE);
        lineDrawer.animateVibrations = true;


        activeTasks--;
    }
}