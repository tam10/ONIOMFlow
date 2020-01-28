using System.Collections;
using System.Collections.Generic;
using EL = Constants.ErrorLevel;
using UnityEngine;

public class GeometryHistory : MonoBehaviour {

    public int historyStep;
    public int maxHistory;
    public Geometry geometry;

    public void Initialise(Geometry geometry, int maxHistory) {
        ClearHistory();
        this.maxHistory = maxHistory;
        this.geometry = geometry;
    }
    
    public void ClearHistory() {
        foreach (Transform savedTransform in transform) {
            GameObject.Destroy(savedTransform.gameObject);
        }
        historyStep = 0;
    }

    public void ResetHistory() {
        ClearHistory();
        if (geometry == null) {
            CustomLogger.LogFormat(EL.ERROR, "Failed to reset geometry history - geometry is null.");
            return;
        }
        SaveState("Reset");
    }

    public void SaveState(string operationName="") {
        if (geometry == null) {
            CustomLogger.LogFormat(EL.ERROR, "Failed to save geometry history - geometry is null.");
            return;
        }

        //Do not exceed the maximum number of history steps
        if (transform.childCount == maxHistory) {
            GameObject.Destroy(transform.GetChild(0).gameObject);
        }

        //Delete all redo states past the most recent undo operation
        for (int deleteIndex = historyStep + 1; deleteIndex < transform.childCount; deleteIndex++) {
            GameObject.Destroy(transform.GetChild(deleteIndex).gameObject);
        }

        //Clone the current geometry
        Geometry clonedGeometry = PrefabManager.InstantiateGeometry(transform);
        clonedGeometry.transform.SetAsLastSibling();
        geometry.CopyTo(clonedGeometry);
        
        historyStep = transform.childCount - 1;
        clonedGeometry.gameObject.name = string.Format(
            "Save {0} ({1})", 
            historyStep, 
            operationName
        );
        
    }

    public void LoadState(int historyStep) {
        if (historyStep < 0 || historyStep >= transform.childCount) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "History step ({0}) out of bounds for history count ({1})",
                historyStep,
                transform.childCount
            );
            return;
        }

        Geometry historyGeometry = transform.GetChild(historyStep).GetComponent<Geometry>();
        if (historyGeometry == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Could not load state - geometry was null!"
            );
            return;
        }
        historyGeometry.CopyTo(geometry);
    }

    public void Undo() {
        if (historyStep < 1) {
            return;
        }
        LoadState(--historyStep);
    }

    public void Redo() {
        if (historyStep + 2 > transform.childCount) {
            return;
        }
        LoadState(++historyStep);
    }
}

/// ADD COLOURS BY AMBER?
// Green - OK
// Yellow - DU
// Red - Missing