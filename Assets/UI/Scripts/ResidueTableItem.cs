using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RP = Constants.ResidueProperty;
using PDT = Constants.PropertyDisplayType;
using COL = Constants.Colour;
using CS = Constants.ColourScheme;
using UnityEngine.UI;
using TMPro;

public class ResidueTableItem : MonoBehaviour
{
    ResidueTable parent;
    public Residue residue;
    public TableToggle togglePrefab;
    public TableInputField inputFieldPrefab;
    public TableButton buttonPrefab;
    public Image background;
    delegate void ElementMaker(RP residueProperty);
    private Dictionary<RP, object> tableFieldDict = new Dictionary<RP, object>();
    private delegate void ToggleCallback(ResidueTable parent, Residue residue, TableToggle toggle);
    

    public void Initialise(ResidueTable parent, Residue residue, bool primary) {
        this.parent = parent;
        this.residue = residue;

        foreach (RP residueProperty in Settings.residueTableProperties) {
            ElementMaker elementMaker = GetElementMaker(residueProperty);
            elementMaker(residueProperty);
        }

        Populate();
        SetPrimary(primary);
    }

    private void SelectResidue() {
        parent.SetRepresentationResidue(residue.residueID);
    }

    public void SetPrimary(bool primary) {
        COL col = primary ? ColorScheme.GetColorScheme(CS.BRIGHT)[3] : ColorScheme.GetColorScheme(CS.DARK)[3] ;
        background.color = ColorScheme.GetColor(col);
    }

    public void SetResidue(Residue residue) {
        this.residue = residue;
        Populate();
    }

    private void Populate() {
        foreach (RP residueProperty in Settings.residueTableProperties) {
            object obj = tableFieldDict[residueProperty];
            if (obj is TableField) {
                TableField tableField = (TableField)obj;
                tableField.residue = residue;
                tableField.GetValue();
            }
        }
    }

    private void SetItemGeometry(GameObject item, RP residueProperty) {

        RectTransform itemRect = item.GetComponent<RectTransform>();
        RectTransform parentRect = parent.headerDict[residueProperty].GetComponent<RectTransform>();
        itemRect.anchoredPosition = new Vector2(
            parentRect.anchoredPosition.x, 
            itemRect.anchoredPosition.y
        );
        itemRect.sizeDelta = new Vector2(
            parentRect.sizeDelta.x,
            itemRect.sizeDelta.y
        );
        item.SetActive(true);

    }

    private ElementMaker GetElementMaker(RP residueProperty) {
        PDT propertyDisplayType = Settings.GetResiduePropertyDisplayType(residueProperty);

        switch (propertyDisplayType) {
            case (PDT.BOOL_EDITABLE): return MakeEditableToggle;
            case (PDT.BOOL_NONEDITABLE): return MakeNonEditableToggle;
            case (PDT.FLOAT_EDITABLE): return MakeEditableFloatInput;
            case (PDT.FLOAT_NONEDITABLE): return MakeNonEditableFloatInput;
            case (PDT.INT_EDITABLE): return MakeEditableIntInput;
            case (PDT.INT_NONEDITABLE): return MakeNonEditableIntInput;
            case (PDT.STRING_EDITABLE): return MakeEditableStringInput;
            case (PDT.STRING_NONEDITABLE): return MakeNonEditableStringInput;
            case (PDT.BUTTON): return MakeButton;
        }
        throw new ErrorHandler.InvalidResiduePropertyID(
            string.Format("Couldn't get Element Maker for Residue Property: {0}", residueProperty), 
            residueProperty
        );
    }

    private void MakeEditableToggle(RP residueProperty) {
        TableToggle toggle = Instantiate<TableToggle>(togglePrefab, transform);
        toggle.residueProperty = residueProperty;

        SetItemGeometry(toggle.gameObject, residueProperty);
        tableFieldDict[residueProperty] = toggle;
    }

    private void MakeNonEditableToggle(RP residueProperty) {
        TableToggle toggle = Instantiate<TableToggle>(togglePrefab, transform);
        toggle.toggle.interactable = false;
        toggle.residueProperty = residueProperty;
        
        SetItemGeometry(toggle.gameObject, residueProperty);
        tableFieldDict[residueProperty] = toggle;
    }

    private void MakeEditableFloatInput(RP residueProperty) {
        TableInputField inputField = Instantiate<TableInputField>(inputFieldPrefab, transform);
        inputField.tmpInputField.contentType = TMP_InputField.ContentType.DecimalNumber;
        inputField.residueProperty = residueProperty;

        SetItemGeometry(inputField.gameObject, residueProperty);
        tableFieldDict[residueProperty] = inputField;
    }

    private void MakeNonEditableFloatInput(RP residueProperty) {
        TableInputField inputField = Instantiate<TableInputField>(inputFieldPrefab, transform);
        inputField.tmpInputField.contentType = TMP_InputField.ContentType.DecimalNumber;
        inputField.tmpInputField.readOnly = true;
        inputField.residueProperty = residueProperty;

        SetItemGeometry(inputField.gameObject, residueProperty);
        tableFieldDict[residueProperty] = inputField;
    }

    private void MakeEditableIntInput(RP residueProperty) {
        TableInputField inputField = Instantiate<TableInputField>(inputFieldPrefab, transform);
        inputField.tmpInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.residueProperty = residueProperty;

        SetItemGeometry(inputField.gameObject, residueProperty);
        tableFieldDict[residueProperty] = inputField;
    }

    private void MakeNonEditableIntInput(RP residueProperty) {
        TableInputField inputField = Instantiate<TableInputField>(inputFieldPrefab, transform);
        inputField.tmpInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.tmpInputField.readOnly = true;
        inputField.residueProperty = residueProperty;

        SetItemGeometry(inputField.gameObject, residueProperty);
        tableFieldDict[residueProperty] = inputField;
    }

    private void MakeEditableStringInput(RP residueProperty) {
        TableInputField inputField = Instantiate<TableInputField>(inputFieldPrefab, transform);
        inputField.tmpInputField.contentType = TMP_InputField.ContentType.Alphanumeric;
        inputField.residueProperty = residueProperty;

        SetItemGeometry(inputField.gameObject, residueProperty);
        tableFieldDict[residueProperty] = inputField;
    }

    private void MakeNonEditableStringInput(RP residueProperty) {
        TableInputField inputField = Instantiate<TableInputField>(inputFieldPrefab, transform);
        inputField.tmpInputField.contentType = TMP_InputField.ContentType.Alphanumeric;
        inputField.residueProperty = residueProperty;
        inputField.tmpInputField.readOnly = true;

        SetItemGeometry(inputField.gameObject, residueProperty);
        tableFieldDict[residueProperty] = inputField;
    }

    private void MakeButton(RP residueProperty) {
        TableButton button = Instantiate<TableButton>(buttonPrefab, transform);
        button.residueProperty = residueProperty;

        SetItemGeometry(button.gameObject, residueProperty);
        tableFieldDict[residueProperty] = button;

        switch (residueProperty) {
            case (RP.SELECTED):
                button.button.onClick.AddListener(SelectResidue);
                break;
        }
    }

    
}
