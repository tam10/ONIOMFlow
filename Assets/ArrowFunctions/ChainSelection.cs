using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChainSelection : MonoBehaviour {

    private static ChainSelection _main;
    public  static ChainSelection main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<ChainSelection>();
            return _main;
        }
    }
    
    public GameObject togglePrefab;
    public Transform contentTransform;
    public Canvas canvas;

    public bool userResponded;
    public bool cancelled;

    int _selectedToggle;
    public int selectedToggle {
        get {
            return _selectedToggle;
        }
        set {
            _selectedToggle = value;
            SetToggleGroupOnIndex(value);
        }
    }

    public void Initialise(List<string> chainStrings) {
        Populate(chainStrings);
        userResponded = false;
        cancelled = false;
        Show();
    }

    void Populate(List<string> chainStrings) {


        foreach (Transform child in contentTransform) {
            GameObject.Destroy(child);
        }

        foreach (string chainString in chainStrings) {
            AddToggle(chainString);
        }

        selectedToggle = 0;


    }

    void AddToggle(string chainString) {
        GameObject toggleGO = Instantiate<GameObject>(togglePrefab, contentTransform);
        toggleGO.GetComponentInChildren<TextMeshProUGUI>().text = chainString;
        
        Toggle toggle = toggleGO.GetComponent<Toggle>();
        toggle.isOn = (toggleGO.transform.GetSiblingIndex() == selectedToggle);
        toggle.onValueChanged.AddListener(delegate {Toggled(toggleGO);});


    }

    public void Toggled(GameObject toggleGO) {
        if (toggleGO.GetComponent<Toggle>().isOn) {
            selectedToggle = toggleGO.transform.GetSiblingIndex();
        }
    }

    void SetToggleGroupOnIndex(int index) {
        foreach (Transform child in contentTransform) {
            child.GetComponent<Toggle>().isOn = (child.transform.GetSiblingIndex() == selectedToggle);
        }
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
