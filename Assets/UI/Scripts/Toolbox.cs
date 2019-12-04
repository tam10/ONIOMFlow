using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using AMID = Constants.AtomModificationID;
using GIS = Constants.GeometryInterfaceStatus;

public class Toolbox : MonoBehaviour {
    
    public Button hButton;
    public Button atomRealButton;
    public Button atomIntermediateButton;
    public Button atomModelButton;
    public Button residueRealButton;
    public Button residueIntermediateButton;
    public Button residueModelButton;
    private Button lastPressedButton;

    public AMID atomModificationID;

    public Canvas canvas;

    public void Show() {
        atomModificationID = AMID.NULL;
        List<Button> buttons = new List<Button> {
            hButton,
            atomRealButton,
            atomIntermediateButton,
            atomModelButton,
            residueRealButton,
            residueIntermediateButton,
            residueModelButton
        };

        foreach (Button button in buttons) {
            SetButtonColourDeselected(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate {ButtonPressed(button);});
        }

        canvas.enabled = true;
    }
    public void Hide() {
        atomModificationID = AMID.NULL;
        SetButtonColourDeselected(lastPressedButton);
        canvas.enabled = false;
    }

    private void SetButtonColourSelected(Button button) {
        if (button != null) {
            button.colors = ColorScheme.GetColorBlock(GIS.LOADING);
        }
    }

    private void SetButtonColourDeselected(Button button) {
        if (button != null) {
            button.colors = ColorScheme.GetColorBlock(GIS.COMPLETED);
        }
    }

    public void ButtonPressed(Button button) {
        AMID amid = GetAtomModificationID(button);

        if (amid == atomModificationID) {
            SetButtonColourDeselected(button);

            atomModificationID = AMID.NULL;
        } else {
            SetButtonColourDeselected(lastPressedButton);
            SetButtonColourSelected(button);

            lastPressedButton = button;
            atomModificationID = amid;
        }
    }

    public AMID GetAtomModificationID (Button button) {
        
        if (ReferenceEquals(button, hButton)) {
            return AMID.HYDROGENS;
        } else if (ReferenceEquals(button, atomModelButton)) {
            return AMID.MODEL_ATOM;
        } else if (ReferenceEquals(button, atomIntermediateButton)) {
            return AMID.INT_ATOM;
        } else if (ReferenceEquals(button, atomRealButton)) {
            return AMID.REAL_ATOM;
        } else if (ReferenceEquals(button, residueModelButton)) {
            return AMID.MODEL_RESIDUE;
        } else if (ReferenceEquals(button, residueIntermediateButton)) {
            return AMID.INT_RESIDUE;
        } else if (ReferenceEquals(button, residueRealButton)) {
            return AMID.REAL_RESIDUE;
        } else {
            return AMID.NULL;
        }
    }

    public Button GetAtomModificationButton (AMID atomModificationID) {

        switch (atomModificationID) {
            case (AMID.HYDROGENS): 
                return hButton;
            case (AMID.MODEL_ATOM):
                return atomModelButton;
            case (AMID.INT_ATOM):
                return atomIntermediateButton;
            case (AMID.REAL_ATOM):
                return atomRealButton;
            case (AMID.MODEL_RESIDUE):
                return residueModelButton;
            case (AMID.INT_RESIDUE):
                return residueIntermediateButton;
            case (AMID.REAL_RESIDUE):
                return residueRealButton;
            default:
                return null;
        }
    }



}
