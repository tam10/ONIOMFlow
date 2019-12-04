using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RS = Constants.ResidueState;
using System.Linq;

public class NonStandardResidueSelection : MonoBehaviour {

    private static NonStandardResidueSelection _main;
    public  static NonStandardResidueSelection main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<NonStandardResidueSelection>();
            return _main;
        }
    }
    
    public ResidueStateDropdown dropdownPrefab;
    public Transform contentTransform;
    public Canvas canvas;

    public bool userResponded;
    public bool cancelled;

    //Keep track of which residues were modified
    public Dictionary<ResidueID, RS> changesDict;
    private Dictionary<ResidueID, RS> residueStateDict;


    private List<string> residueStateStrings;
    private Dictionary<RS, int> residueStateMap;
    private List<RS> residueStates;

    private List<ResidueID> residueIDs;

    void Awake() {
        residueStateStrings = Constants.ResidueStateMap.Keys.ToList();
        residueStates = residueStateStrings.Select(x => Constants.ResidueStateMap[x]).ToList();
        residueStateMap = residueStates.ToDictionary(x => x, x => residueStates.IndexOf(x));
    }

    public IEnumerator Initialise(Geometry geometry) {
        userResponded = false;
        cancelled = false;
        
        residueStateDict = geometry.residueDict.ToDictionary(x => x.Key, x => x.Value.state);

        changesDict = new Dictionary<ResidueID, RS>();
        residueIDs = residueStateDict.Keys.OrderBy(x => x).ToList();
        
        Show();
        yield return Populate(geometry, residueIDs);

    }

    IEnumerator Populate(Geometry geometry, List<ResidueID> residueIDs) {
        
        foreach (Transform child in contentTransform) {
            GameObject.Destroy(child.gameObject);
        }

        foreach (ResidueID residueID in residueIDs) {
            AddDropdown(geometry, residueID);
            if (Timer.yieldNow) {yield return null;}
        }
    }

    void AddDropdown(Geometry geometry, ResidueID residueID) {
        ResidueStateDropdown residueStateDropdown = Instantiate<ResidueStateDropdown>(dropdownPrefab, contentTransform);
        residueStateDropdown.text.text = GetResidueString(geometry, residueID);
        residueStateDropdown.residueID = residueID;

        residueStateDropdown.dropdown.AddOptions(residueStateStrings);
        residueStateDropdown.dropdown.value = residueStateMap[residueStateDict[residueID]];

        residueStateDropdown.dropdown.onValueChanged.AddListener(delegate {DropdownValueChanged(residueStateDropdown);});

    }

    string GetResidueString(Geometry geometry, ResidueID residueID) {
        return string.Format("{0}({1})", residueID, geometry.residueDict[residueID].residueName);
    }

    void DropdownValueChanged(ResidueStateDropdown residueStateDropdown) {
        RS newState = residueStates[residueStateDropdown.dropdown.value];
        changesDict[residueStateDropdown.residueID] = newState;
        residueStateDict[residueStateDropdown.residueID] = newState;
    }

    public void Confirm() {
        userResponded = true;
        cancelled = false;
        Hide();
    }

    public void Cancel() {
        userResponded = true;
        cancelled = true;
        Hide();
    }

    public void Hide() {
        canvas.enabled = false;
        foreach (Transform child in contentTransform) {
            GameObject.Destroy(child.gameObject);
        }
    }

    public void Show() {
        canvas.enabled = true;
    }

}
