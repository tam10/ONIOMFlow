using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
public class AtomsTableBuffered : MonoBehaviour {
//    private Atoms atoms;
//
//    public RectTransform scrollRectTransform;
//    public AtomsTableTitles atomsTableTitles;
//    public AtomsTableInfo atomsTableInfoPrefab;
//    public RectTransform dialogueTransform;
//    public RectTransform tableTransform;
//    public RectTransform titleGroupRect;
//
//    public TextMeshProUGUI titleText;
//
//
//    //This is the displayed scrollbar showing the whole range
//    public AtomsScrollbar atomsScrollbar;
//
//    //This is the hidden scrollbar that we use to provide feedback
//    //when the top or bottom of the buffered region is reached
//    
//    public Scrollbar bufferedScrollbar;
//
//    //This is the scrollable area. Added a listener to OnScroll
//    public ScrollRect bufferedScrollRect;
//
//    public AtomsTableInfo[] bufferedRows;
//
//    private int numAtoms;
//    string[] elements;
//    float[,] positions;
//    string[] pdbs;
//    string[] ambers;
//    string[] residueNames;
//    int[] residueNumbers;
//    string[] chains;
//
//    bool[] selectedArray;
//
//    //Number of AtomInfo objects to be loaded
//    private int numBuffered;
//
//    //Indices of first and last atom in buffered region
//    private int bufferedAtomStartIndex;
//    private int bufferedAtomEndIndex;
//    private Vector2 contentShiftVector;
//
//    //This is needed to stop both listener functions being called
//    private bool scrollRectValueChanged = false;
//    private bool scrollbarValueChanged = false;
//
//    public void Populate(Atoms atoms) {
//    //    this.atoms = atoms;
//    //    numAtoms = atoms.size;
//    //    elements = atoms.elements;
//    //    positions = atoms.positions;
//    //    pdbs = atoms.pdbNames;
//    //    ambers = atoms.amberNames;
//    //    residueNames = atoms.residueNames;
//    //    residueNumbers = atoms.residueNumbers;
//    //    chains = atoms.chains;
//    //    selectedArray = new bool[numAtoms];
////
//    //    //Use this for scrolling - shifts all child objects of table
//    //    contentShiftVector = new Vector2(
//    //        0f,
//    //        atomsTableInfoPrefab.GetComponent<RectTransform>().rect.height +
//    //        tableTransform.GetComponent<VerticalLayoutGroup>().spacing
//    //    );
////
//    //    //Number of buffered objects in table
//    //    //This is much more efficient than loading a row for every atom
//    //    numBuffered = Mathf.Min(10, numAtoms);
//    //    //Keep track of the indices for internal communication
//    //    bufferedAtomStartIndex = 0;
//    //    bufferedAtomEndIndex = numBuffered - 1;
////
//    //    //Not too necessary yet. Could be used for index swapping
//    //    bufferedRows = new AtomsTableInfo[numBuffered];
//    //    
//    //    //Make sure there are no child objects in table
//    //    foreach (Transform child in tableTransform) {
//    //        GameObject.Destroy(child.gameObject);
//    //    }
////
//    //    //Add listeners so scrollrect and scrollbar talk to each other
//    //    bufferedScrollRect.onValueChanged.AddListener(delegate {ScrollRectValueChanged();});
//    //    atomsScrollbar.onValueChanged.AddListener(delegate {ScrollBarValueChanged();});
////
//    //    //Don't use small scrollbar when fewer atoms than rows
//    //    if (numBuffered < numAtoms) {
//    //        atomsScrollbar.size = 0.2f;
//    //    } else {
//    //        atomsScrollbar.size = 1f;
//    //    }
////
//    //    //Create the rows from AtomsTableInfo class
//    //    for (int i = 0; i < numBuffered; i++) {
//    //        AtomsTableInfo atomInfo = Instantiate<AtomsTableInfo> (atomsTableInfoPrefab, tableTransform);
//    //        atomInfo.gameObject.SetActive(true);
////
//    //        //Populate using atoms arrays
//    //        SetRowValues(atomInfo, i);
////
//    //        //Link widths to title - this allows consistency
//    //        atomInfo.SetWidths(atomsTableTitles);
////
//    //        //Make sure modifications to fields in the table are 
//    //        // fed back to the arrays
//    //        atomInfo.LinkArrays(this);
//    //        bufferedRows[i] = atomInfo;
//    //    }
////
//    }
//
//    private void ScrollRectValueChanged() {
//        //Invoked when the content window (table) are scrolled
//        if (scrollbarValueChanged) return;
//        scrollRectValueChanged = true;
//
//        //Change position of displayed scrollbar
//        atomsScrollbar.value = (float)bufferedAtomEndIndex / (numAtoms - numBuffered);
//        
//        //Swap top and bottom rows and change values 
//        // if reached top or bottom of buffered region
//        if (bufferedScrollbar.value == 0) {
//            TryScrollDownStep();
//        } else if (bufferedScrollbar.value == 1) {
//            TryScrollUpStep();
//        }
//
//        scrollRectValueChanged = false;
//    }
//
//    private void ScrollBarValueChanged() {
//        //Invoked when the scrollbar area is clicked
//        if (scrollRectValueChanged) return;
//        scrollbarValueChanged = true;
//
//        //Scrollbar has a value between 0 and 1.
//        //Translate this to atom index using numAtoms
//        //Need to subtract numBuffered so bufferedAtomEndIndex doesn't exceed numAtoms
//        bufferedAtomStartIndex = (int)((atomsScrollbar.value) * (numAtoms - numBuffered));
//        bufferedAtomEndIndex = bufferedAtomStartIndex + numBuffered - 1;
//        
//        //All the entries must be updated this time - 10x slower
//        for (int i = 0; i < numBuffered; i++) {
//            AtomsTableInfo atomInfo = tableTransform.GetChild(i).GetComponent<AtomsTableInfo>();
//            SetRowValues(atomInfo, bufferedAtomStartIndex + i);
//        }
//
//        scrollbarValueChanged = false;
//    }
//
//    public void TryScrollDownStep() {
//        //Shouldn't be a case where bufferedAtomEndIndex + 1 ever exceeds numAtoms
//        // but can't be too cautious
//        if (bufferedAtomEndIndex + 1 < numAtoms) {
//            ScrollDownStep();
//        }
//    }
//
//    public void TryScrollUpStep() {
//        if (bufferedAtomStartIndex > 0) {
//            ScrollUpStep();
//        }
//    }
//
//    void ScrollDownStep() {
//        //Invoked when bottom of scrollrect is reached
//        //Recycle AtomInfo from top by moving it to the bottom
//        // and changing values
//        MoveTopToBottom();
//        tableTransform.anchoredPosition = tableTransform.anchoredPosition - contentShiftVector;
//    }
//
//    void ScrollUpStep() {
//        MoveBottomToTop();
//        tableTransform.anchoredPosition = tableTransform.anchoredPosition + contentShiftVector;
//    }
//
//    void MoveBufferedRow(int from, int to) {
//        bufferedRows[from].gameObject.transform.SetSiblingIndex(to);
//    }
//
//    void MoveTopToBottom() {
//        //Move top row to bottom in the table.
//        //Change values of this row to new bottom
//        Transform rowTransform = tableTransform.GetChild(0);
//        rowTransform.SetAsLastSibling();
//
//        bufferedAtomStartIndex++;
//        bufferedAtomEndIndex++;
//        SetRowValues(rowTransform.GetComponent<AtomsTableInfo>(), bufferedAtomEndIndex);
//
//    }
//
//    void MoveBottomToTop() {
//        Transform rowTransform = tableTransform.GetChild(numBuffered - 1);
//        rowTransform.SetAsFirstSibling();
//
//        bufferedAtomStartIndex--;
//        bufferedAtomEndIndex--;
//        SetRowValues(rowTransform.GetComponent<AtomsTableInfo>(), bufferedAtomStartIndex);
//    }
//
//    void SetRowValues(AtomsTableInfo row, int index) {
//        //Use arrays to set values of a row
//        SetRowValues(
//            row, index, selectedArray[index], elements[index], 
//            positions[index, 0], positions[index, 1], positions[index, 2],
//            pdbs[index], ambers[index], residueNames[index], 
//            residueNumbers[index], chains[index]
//        );
//    }
//
//    void SetRowValues(
//        //Set the values of a row
//        AtomsTableInfo atomInfo, int index, bool selected, 
//        string element, float xCoord, float yCoord, float zCoord,
//        string pdb, string amber, string residueName, 
//        int residueNumber, string chain
//    ) {
//        atomInfo.SetValues(
//            index, selected, element, xCoord, yCoord, zCoord, 
//            pdb, amber, residueName, residueNumber, chain
//        );
//
//    }
//
//    public void SetTitle(string title) {
//        //Change the title in the title bar
//        titleText.text = title;
//    }
//
//    public void Display() {
//        dialogueTransform.gameObject.SetActive(true);
//    }
//
//    public void Hide() {
//        //Take the dialogue away from the scene
//        dialogueTransform.gameObject.SetActive(false);
//
//        //Save the changes made
//        SaveAtoms();
//        
//        //Clear the AtomsInfo objects - might not be necessary but safe
//        for (int i = 0; i < numBuffered; i++) {
//            bufferedRows[i] = null;
//        }
//
//        //Remove child objects so if the dialogue is invoked they don't reappear
//        foreach (Transform child in tableTransform) {
//            GameObject.Destroy(child.gameObject);
//        }
//    }
//
//    public void Update() {
//        //Title group must keep track of scrollrect so they're aligned
//        titleGroupRect.offsetMin = new Vector2(tableTransform.offsetMin.x, 0f);
//    }
//
//
//    // Update arrays when value is changed in InputFields/Toggles
//
//    public void ChangeSelected(int index, bool value) {selectedArray[index] = value;}
//    public void ChangeElement(int index, string value) {elements[index] = value;}
//    public void ChangePositionX(int index, float value) {positions[index, 0] = value;}
//    public void ChangePositionY(int index, float value) {positions[index, 1] = value;}
//    public void ChangePositionZ(int index, float value) {positions[index, 2] = value;}
//    public void ChangePDB(int index, string value) {pdbs[index] = value;}
//    public void ChangeAmber(int index, string value) {ambers[index] = value;}
//    public void ChangeResidueName(int index, string value) {residueNames[index] = value;}
//    public void ChangeResidueNumber(int index, int value) {residueNumbers[index] = value;}
//    public void ChangeChain(int index, string value) {chains[index] = value;}
//    
//    private void SaveAtoms() {
//    //    atoms.elements = elements;
//    //    atoms.positions = positions;
//    //    atoms.pdbNames = pdbs;
//    //    atoms.amberNames = ambers;
//    //    atoms.residueNames = residueNames;
//    //    atoms.residueNumbers = residueNumbers;
//    //    atoms.chains = chains;
//    }
}
