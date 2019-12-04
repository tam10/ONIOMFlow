using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AtomsTableInfo : MonoBehaviour
{

//    public AtomsTableTitles atomsTableTitles;
//
//    public TextMeshProUGUI indexText;
//    public Toggle selectedToggle;
//    public TMP_InputField elementText;
//    public TMP_InputField positionXText;
//    public TMP_InputField positionYText;
//    public TMP_InputField positionZText;
//    public TMP_InputField pdbText;
//    public TMP_InputField amberText;
//    public TMP_InputField residueNameText;
//    public TMP_InputField residueNumberText;
//    public TMP_InputField chainText;
//
//    public RectTransform indexRect;
//    public RectTransform selectedRect;
//    public RectTransform elementRect;
//    public RectTransform positionXRect;
//    public RectTransform positionYRect;
//    public RectTransform positionZRect;
//    public RectTransform pdbRect;
//    public RectTransform amberRect;
//    public RectTransform residueNameRect;
//    public RectTransform residueNumberRect;
//    public RectTransform chainRect;
//
//    public AtomsTableBuffered parent;
//    public int index;
//
//    public void SetValues(
//        int index,
//        bool selected,
//        string element,
//        float xCoord,
//        float yCoord,
//        float zCoord,
//        string pdb,
//        string amber,
//        string residueName,
//        int residueNumber,
//        string chain
//    ) { 
//        indexText.text = index.ToString();
//        selectedToggle.isOn = selected;
//        elementText.text = element;
//        positionXText.text = xCoord.ToString();
//        positionYText.text = yCoord.ToString();
//        positionZText.text = zCoord.ToString();
//        pdbText.text = pdb;
//        amberText.text = amber;
//        residueNameText.text = residueName;
//        residueNumberText.text = residueNumber.ToString();
//        chainText.text = chain;
//
//    }
//
//    public void SetWidths (AtomsTableTitles atomsTableTitles) {
//        this.atomsTableTitles = atomsTableTitles;
//        RectTransform.Axis hor = RectTransform.Axis.Horizontal;
//        indexRect.SetSizeWithCurrentAnchors(hor, atomsTableTitles.indexRect.rect.width);
//        selectedRect.SetSizeWithCurrentAnchors(hor, atomsTableTitles.selectedRect.rect.width);
//        elementRect.SetSizeWithCurrentAnchors(hor, atomsTableTitles.elementRect.rect.width);
//        positionXRect.SetSizeWithCurrentAnchors(hor, atomsTableTitles.positionXRect.rect.width);
//        positionYRect.SetSizeWithCurrentAnchors(hor, atomsTableTitles.positionYRect.rect.width);
//        positionZRect.SetSizeWithCurrentAnchors(hor, atomsTableTitles.positionZRect.rect.width);
//        pdbRect.SetSizeWithCurrentAnchors(hor, atomsTableTitles.pdbRect.rect.width);
//        amberRect.SetSizeWithCurrentAnchors(hor, atomsTableTitles.amberRect.rect.width);
//        residueNameRect.SetSizeWithCurrentAnchors(hor, atomsTableTitles.residueNameRect.rect.width);
//        residueNumberRect.SetSizeWithCurrentAnchors(hor, atomsTableTitles.residueNumberRect.rect.width);
//        chainRect.SetSizeWithCurrentAnchors(hor, atomsTableTitles.chainRect.rect.width);        
//    }
//
//    public void GetIndex() {
//        int _index;
//        if (int.TryParse(indexText.text, out _index)) index = _index;
//    }
//
//    public void ChangeSelected() {
//        GetIndex();
//        parent.ChangeSelected(index, selectedToggle.isOn);
//    }
//
//    public void ChangeElement() {
//        GetIndex();
//        parent.ChangeElement(index, elementText.text);
//    }
//
//    public void ChangePositionX() {
//        GetIndex();
//        float pos;
//        if (float.TryParse(elementText.text, out pos)) {
//            parent.ChangePositionX(index, pos);
//        }
//    }
//
//    public void ChangePositionY() {
//        GetIndex();
//        float pos;
//        if (float.TryParse(elementText.text, out pos)) {
//            parent.ChangePositionY(index, pos);
//        }
//    }
//
//    public void ChangePositionZ() {
//        GetIndex();
//        float pos;
//        if (float.TryParse(elementText.text, out pos)) {
//            parent.ChangePositionZ(index, pos);
//        }
//    }
//
//    public void ChangePDB() {
//        GetIndex();
//        parent.ChangePDB(index, pdbText.text);
//    }
//
//    public void ChangeAmber() {
//        GetIndex();
//        parent.ChangeAmber(index, amberText.text);
//    }
//
//    public void ChangeResidueName() {
//        GetIndex();
//        parent.ChangeResidueName(index, residueNameText.text);
//    }
//
//    public void ChangeResidueNumber() {
//        GetIndex();
//        int resNum;
//        if (int.TryParse(elementText.text, out resNum)) {
//            parent.ChangeResidueNumber(index, resNum);
//        }
//    }
//
//    public void ChangeChain() {
//        GetIndex();
//        parent.ChangeChain(index, residueNameText.text);
//    }
//
//    public void LinkArrays(AtomsTableBuffered parent) {
//        this.parent = parent;
//
//        selectedToggle.onValueChanged.AddListener(delegate {ChangeSelected();});
//        elementText.onValueChanged.AddListener(delegate {ChangeElement();});
//        positionXText.onValueChanged.AddListener(delegate {ChangePositionX();});
//        positionYText.onValueChanged.AddListener(delegate {ChangePositionY();});
//        positionZText.onValueChanged.AddListener(delegate {ChangePositionZ();});
//        pdbText.onValueChanged.AddListener(delegate {ChangePDB();});
//        amberText.onValueChanged.AddListener(delegate {ChangeAmber();});
//        residueNameText.onValueChanged.AddListener(delegate {ChangeResidueName();});
//        residueNumberText.onValueChanged.AddListener(delegate {ChangeResidueNumber();});
//        chainText.onValueChanged.AddListener(delegate {ChangeChain();});
//    }


}
