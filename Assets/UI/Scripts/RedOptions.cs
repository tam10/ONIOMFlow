using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Xml.Linq;


public class RedOptions : MonoBehaviour {
    private static RedOptions _main;
    public  static RedOptions main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<RedOptions>();
            return _main;
        }
    }
    
    public Transform contentTransform;
    public Canvas canvas;
    public TMP_InputField numProcInput;
    public TMP_Dropdown qmSoftwareDropdown;
    public TMP_Dropdown chargeTypeDropdown;
    public TMP_Dropdown chargeCorrectionDropdown;
    public Toggle optimiseToggle;
    public Toggle mepToggle;

    string numProcRedName;
    string optimiseRedName;
    string mepRedName;
    string chrTypeRedName;
    string chrCorRedName;
    string qmSoftRedName;



    Dictionary<bool, string> boolDict = new Dictionary<bool, string> {{true, "On"}, {false, "Off"}};
    Dictionary<string, string> chrTypeDict = new Dictionary<string, string> ();
    Dictionary<string, string> chrCorDict = new Dictionary<string, string> ();
    Dictionary<string, string> qmSoftwareDict = new Dictionary<string, string> ();


    public bool userResponded;
    public bool cancelled;

    public void Initialise() {
        SetDefaults();
        userResponded = false;
        cancelled = false;
        Show();
    }

    public Dictionary<string, string> GetResults() {

        return new Dictionary<string, string> {
            {numProcRedName, numProcInput.text},
            {optimiseRedName, boolDict[optimiseToggle.isOn]},
            {mepRedName, boolDict[mepToggle.isOn]},
            {qmSoftRedName, qmSoftwareDict[qmSoftwareDropdown.options[qmSoftwareDropdown.value].text]},
            {chrCorRedName, chrCorDict[chargeCorrectionDropdown.options[chargeCorrectionDropdown.value].text]},
            {chrTypeRedName, chrTypeDict[chargeTypeDropdown.options[chargeTypeDropdown.value].text]}
        };
    }

    public void SetDefaults() {

		XDocument sX = FileIO.ReadXML (Settings.projectSettingsPath);
		XElement pX = sX.Element ("projectSettings");
		XElement partialChargesX = pX.Element("partialCharges");
        XElement partialChargesOptionsX = partialChargesX.Element("options");

        XElement numProcX = partialChargesOptionsX.Element("numProc");
		numProcInput.text = FileIO.ParseXMLInt(numProcX, "value").ToString();
        numProcRedName = FileIO.ParseXMLString(numProcX, "redName");

        XElement optimiseX = partialChargesOptionsX.Element("opt");
        optimiseToggle.isOn = FileIO.ParseXMLInt(optimiseX, "value") == 1;
        optimiseRedName = FileIO.ParseXMLString(optimiseX, "redName");

        XElement mepX = partialChargesOptionsX.Element("mep");
        mepToggle.isOn = FileIO.ParseXMLInt(mepX, "value") == 1;
        mepRedName = FileIO.ParseXMLString(mepX, "redName");


        XElement chrTypeX = partialChargesOptionsX.Element("chrType");
        SetREDDropdownOptions(chrTypeX.Element("options"), chargeTypeDropdown, chrTypeDict, FileIO.ParseXMLString(chrTypeX, "value"));
        chrTypeRedName = FileIO.ParseXMLString(chrTypeX, "redName");


        XElement chrCorX = partialChargesOptionsX.Element("chrCor");
        SetREDDropdownOptions(chrCorX.Element("options"), chargeCorrectionDropdown, chrCorDict, FileIO.ParseXMLString(chrCorX, "value"));
        chrCorRedName = FileIO.ParseXMLString(chrCorX, "redName");


        XElement qmSoftwareX = partialChargesOptionsX.Element("qm");
        SetREDDropdownOptions(qmSoftwareX.Element("options"), qmSoftwareDropdown, qmSoftwareDict, FileIO.ParseXMLString(qmSoftwareX, "value"));
        qmSoftRedName = FileIO.ParseXMLString(qmSoftwareX, "redName");
	
    }

    void SetREDDropdownOptions(XElement optionsX, TMP_Dropdown dropdown, Dictionary<string, string> optionsDict, string defaultValue) {
        int index = 0;
        int counter = 0;
        List<string> options = new List<string>();
        foreach (XElement optionX in optionsX.Elements("option")) {
            string redName = optionX.Attribute("redName").Value;
            string displayName = optionX.Value;

            options.Add(displayName);
            optionsDict[displayName] = redName;

            if (displayName == defaultValue) index = counter;
            counter++;


        }
        dropdown.AddOptions(options);
        dropdown.value = index;
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
    }

    public void Show() {
        canvas.enabled = true;
    }

}