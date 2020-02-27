using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using RP = Constants.ResidueProperty;
using PDT = Constants.PropertyDisplayType;
using GIID = Constants.GeometryInterfaceID;
using TMPro;

public class ResidueTable : MonoBehaviour {

    private static ResidueTable _main;
    public static ResidueTable main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<ResidueTable>();
            return _main;
        }
    }

    
    //The interface from which these residues come
    public GIID geometryInterfaceID;

    //Prefabs
    public ResidueTableItem residueTableItemPrefab;
    public ResidueHeader columnHeaderPrefab;

    private List<ResidueTableItem> residueTableItems = new List<ResidueTableItem>();


    public RectTransform contentTransform;
    public Canvas canvas;
    public Toolbox toolbox;
    public GameObject toolboxGameObject;
    public RectTransform titleGroupRect;

    //This is the displayed scrollbar showing the whole range
    public ResidueScrollbar residueScrollbar;

    //This is the hidden scrollbar that we use to provide feedback
    //when the top or bottom of the buffered region is reached
    
    public Scrollbar bufferedScrollbar;

    //This is the scrollable area. Added a listener to OnScroll
    public ScrollRect bufferedScrollRect;

    //Geometry
    public RectTransform backgroundRect;
    public RectTransform tableTitleRect;
    public TextMeshProUGUI tableTitleText;


    Geometry geometry;
    List<ResidueID> residueIDs = new List<ResidueID>();
    public ResidueID primaryResidueID;
    Dictionary<RP, float> columnWidths = new Dictionary<RP, float>();
    public Dictionary<RP, ResidueHeader> headerDict = new Dictionary<RP, ResidueHeader>();

    //This is needed to stop both listener functions being called
    public bool scrollRectValueChanged = false;
    public bool scrollbarValueChanged = false;


    //Number of Residues to be loaded
    public int numBuffered;
    public int numResidues;

    //Indices of first and last atom in buffered region
    public int bufferedResidueStartIndex;
    public int bufferedResidueEndIndex;

    public Vector2 contentShiftVector;

    public float tableTitleFontSize = 18f;
    public float tableTitleHeight = 30f;

    //Representations
    private ResidueRepresentation residueRepresentation;
    public DraggableInterface draggableInterface;


    public void Awake() {
        
        backgroundRect.sizeDelta = new Vector2(backgroundRect.sizeDelta.x, Screen.height / 3);
        tableTitleText.fontSize = Settings.isRetina ? tableTitleFontSize * 2 : tableTitleFontSize;
        tableTitleRect.sizeDelta = new Vector2(tableTitleRect.sizeDelta.x, Settings.isRetina ? tableTitleHeight * 2 : tableTitleHeight);
        Hide();
    }

    public IEnumerator Populate(GIID geometryInterfaceID) {

        Geometry geometry = Flow.main.geometryDict[geometryInterfaceID].geometry;
        if (geometry == null) {
            yield break;
        }
        this.geometry = geometry;

        ClearTitles();
        Clear();

        yield return null;

        this.geometryInterfaceID = geometryInterfaceID;
        residueIDs = geometry.EnumerateResidueIDs().ToList();
        residueIDs.Sort();

        contentShiftVector = new Vector2(
            0f,
            residueTableItemPrefab.GetComponent<RectTransform>().rect.height +
            contentTransform.GetComponent<VerticalLayoutGroup>().spacing
        );

        //The number of "apparent" residues in the table
        numResidues = residueIDs.Count;

        //How many ResidueTableItems should be loaded?
        //Use twice the number that can fit in tableScrollableContent or numResidues if there aren't enough items
        float contentHeight = bufferedScrollRect.GetComponent<RectTransform>().rect.height;
        int fitNumber = (int)(contentHeight / contentShiftVector.y);
        numBuffered = Mathf.Min(2 * fitNumber, numResidues);
        
        //Start with the lowest ResidueID being the primary
        primaryResidueID = (numBuffered > 0) ? residueIDs[0] : ResidueID.Empty;

        //Keep track of first and last buffered residue
        bufferedResidueStartIndex = 0;
        bufferedResidueEndIndex = numBuffered - 1;


        //Add listeners so scrollrect and scrollbar talk to each other
        bufferedScrollRect.onValueChanged.AddListener(delta => ScrollRectValueChanged(delta));
        residueScrollbar.onValueChanged.AddListener(value => ScrollBarValueChanged(value));

        //Don't use small scrollbar when fewer atoms than rows
        residueScrollbar.size = (numBuffered < numResidues) ? 0.2f : 1f;

        //Get Column widths and set titles
        foreach (RP residueProperty in Settings.residueTableProperties) {
            float width = Settings.GetResiduePropertyWidth(residueProperty);
            columnWidths[residueProperty] = Settings.isRetina ? width * 2 : width;

            ResidueHeader header = GameObject.Instantiate<ResidueHeader>(columnHeaderPrefab, titleGroupRect.transform);
            header.rectTransform.sizeDelta = new Vector2(columnWidths[residueProperty], header.rectTransform.rect.height);

            header.headerText.text = Settings.GetResiduePropertyTitle(residueProperty);
            headerDict[residueProperty] = header;

        }

        yield return null;

        Display();
        yield return null;

        //Instantiate buffered rows
        residueTableItems = new List<ResidueTableItem>();
        for (int i = 0; i < numBuffered; i++) {
            ResidueTableItem residueTableItem = Instantiate<ResidueTableItem>(residueTableItemPrefab, contentTransform);

            // i == 0 refers to the primary residue (using the first Residue as the primary)
            residueTableItem.Initialise(this, geometry.GetResidue(residueIDs[i]), i == 0);
            SetRowValues(residueTableItem, i);
            residueTableItems.Add(residueTableItem);
        }
        
        yield return null;

        residueRepresentation = PrefabManager.InstantiateResidueRepresentation(transform);
        residueRepresentation.transform.position = new Vector3(0f, 2f, 15f);
        residueRepresentation.SetGeometry(geometryInterfaceID);
        residueRepresentation.MakeInteractive(draggableInterface);
        residueRepresentation.toolbox = toolbox;
        
        ///TEMP
        if (! primaryResidueID.IsEmpty()) {
            residueRepresentation.SetRepresentationResidue(primaryResidueID);
        }
    }

    public void SetRepresentationResidue(ResidueID residueID) {
        
        primaryResidueID = residueID;
        
        if (!(residueRepresentation is null)) {
            residueRepresentation.SetRepresentationResidue(residueID);
        }
    }

    public void UpdateResidueTableItems() {
        foreach (ResidueTableItem residueTableItem in residueTableItems) {
            ResidueID residueID = residueTableItem.residue.residueID;
            residueTableItem.SetPrimary(residueID == primaryResidueID);
            SetRowValues(residueTableItem, residueID);
        }
    }

    void Clear() {
        foreach (Transform child in contentTransform) {
            GameObject.Destroy(child.gameObject);
        }
        residueTableItems.Clear();
    }

    void ClearTitles() {
        foreach (Transform child in titleGroupRect.transform) {
            GameObject.Destroy(child.gameObject);
        }
    }

    object GetValue(GIID geometryInterfaceID, ResidueID residueID, RP residueProperty) {
        Residue residue = Flow.GetGeometry(geometryInterfaceID).GetResidue(residueID);
        switch(residueProperty) {
            case (RP.CHAINID): return residue.chainID;
            case (RP.RESIDUE_NUMBER): return residueID.residueNumber;
		    case (RP.RESIDUE_NAME): return residue.residueName;
		    case (RP.STATE): return residue.state;
		    case (RP.STANDARD): return residue.standard;
		    case (RP.PROTONATED): return residue.protonated;
            case (RP.CHARGE): return residue.GetCharge();
		    case (RP.SIZE): return residue.size;
            default: throw new ErrorHandler.InvalidResiduePropertyID(
                string.Format("Invalid Residue Property: {0}", residueProperty),
                residueProperty
            );
        }
    }

    private void ScrollRectValueChanged(Vector2 delta) {
        //Invoked when the content window (table) are scrolled
        if (scrollbarValueChanged) return;
        scrollRectValueChanged = true;

        //CustomLogger.LogOutput(
        //    "residueScrollbar.value {0} -> {1}",
        //    residueScrollbar.value,
        //    (numResidues - numBuffered > 0) ? (float)bufferedResidueEndIndex / (numResidues - numBuffered) : 1
        //);
        //Change position of displayed scrollbar
        residueScrollbar.value = (numResidues - numBuffered > 0) ? (float)bufferedResidueEndIndex / (numResidues - numBuffered) : 1;
        
        //Swap top and bottom rows and change values 
        // if reached top or bottom of buffered region
        if (bufferedScrollbar.value <= 0) {
            //Debug.Log("ScrollRectValueChanged.TryScrollDownStep");
            TryScrollDownStep();
        } else if (bufferedScrollbar.value >= 1) {
            //Debug.Log("ScrollRectValueChanged.TryScrollUpStep");
            TryScrollUpStep();
        }

        scrollRectValueChanged = false;
    }

    private void ScrollBarValueChanged(float value) {
        //Invoked when the scrollbar area is clicked
        if (scrollRectValueChanged) return;
        scrollbarValueChanged = true;

        //Scrollbar has a value between 0 and 1.
        //Translate this to atom index using numAtoms
        //Need to subtract numBuffered so bufferedAtomEndIndex doesn't exceed numAtoms
        bufferedResidueStartIndex = (int)((value) * (numResidues - numBuffered));
        bufferedResidueEndIndex = bufferedResidueStartIndex + numBuffered - 1;
        
        //All the entries must be updated this time - 10x slower
        for (int i = 0; i < numBuffered; i++) {
            ResidueTableItem atomInfo = contentTransform.GetChild(i).GetComponent<ResidueTableItem>();
            SetRowValues(atomInfo, bufferedResidueStartIndex + i);
        }

        scrollbarValueChanged = false;
    }

    public void TryScrollDownStep() {
        //Shouldn't be a case where bufferedAtomEndIndex + 1 ever exceeds numAtoms
        // but can't be too cautious
        if (bufferedResidueEndIndex + 1 < numResidues) {
            ScrollDownStep();
        }
    }

    public void TryScrollUpStep() {
        if (bufferedResidueStartIndex > 0) {
            ScrollUpStep();
        }
    }

    void ScrollDownStep() {
        //Invoked when bottom of scrollrect is reached
        //Recycle AtomInfo from top by moving it to the bottom
        // and changing values
        MoveTopToBottom();
        contentTransform.anchoredPosition = contentTransform.anchoredPosition - contentShiftVector;
    }

    void ScrollUpStep() {
        MoveBottomToTop();
        contentTransform.anchoredPosition = contentTransform.anchoredPosition + contentShiftVector;
    }

    void MoveTopToBottom() {
        if (contentTransform.childCount == 0) return;
        //Move top row to bottom in the table.
        //Change values of this row to new bottom

        Transform rowTransform = contentTransform.GetChild(0);
        rowTransform.SetAsLastSibling();

        bufferedResidueStartIndex++;
        bufferedResidueEndIndex++;

        SetRowValues(rowTransform.GetComponent<ResidueTableItem>(), bufferedResidueEndIndex);
    }

    void MoveBottomToTop() {
        if (contentTransform.childCount == 0) return;
        Transform rowTransform = contentTransform.GetChild(numBuffered - 1);
        rowTransform.SetAsFirstSibling();

        bufferedResidueStartIndex--;
        bufferedResidueEndIndex--;

        SetRowValues(rowTransform.GetComponent<ResidueTableItem>(), bufferedResidueStartIndex);
    }

    void SetRowValues(ResidueTableItem residueTableItem, int index) {
        //Use arrays to set values of a row
        ResidueID residueID = residueIDs[index];
        SetRowValues(residueTableItem, residueID);
    }

    void SetRowValues(ResidueTableItem residueTableItem, ResidueID residueID) {
        //Use arrays to set values of a row
        Residue residue = geometry.GetResidue(residueID);

        foreach(RP residueProperty in Settings.residueTableProperties) {
            residueTableItem.SetResidue(geometry.GetResidue(residueID));
        }
        residueTableItem.SetPrimary(residueID == primaryResidueID);
    }
    
    public void Display() {
        canvas.enabled = true;
        toolbox.Show();
        Flow.main.GetComponent<Canvas>().enabled = false;
    }

    public void Hide() {
        //Take the dialogueGameObject away from the scene
        canvas.enabled = false;
        toolbox.Hide();

        //Remove child objects so if the dialogueGameObject is invoked they don't reappear
        Clear();

        if (residueRepresentation != null) {
            GameObject.Destroy(residueRepresentation.gameObject);
        }

        Flow.main.GetComponent<Canvas>().enabled = true;
    }

    public void Update() {
        //Title group must keep track of scrollrect so they're aligned
        titleGroupRect.offsetMin = new Vector2(contentTransform.offsetMin.x, 0f);
    }

    
}
