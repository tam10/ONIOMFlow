using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.Mathematics;
using System.Xml.Linq;
using System.Linq;
using EL = Constants.ErrorLevel;
using RS = Constants.ResidueState;
using OLID = Constants.OniomLayerID;
using GCT = Constants.GaussianConvergenceThreshold;
using GIID = Constants.GeometryInterfaceID;
using TID = Constants.TaskID;
using GIS = Constants.GeometryInterfaceStatus;
using BT = Constants.BondType;

/// <summary>
/// Root Macro Object. 
/// Implements MacroGroup
/// </summary>
public class Macro : MacroGroup {

    /// <summary>Load a Macro and run it on a Geometry Interface</summary>
    /// <param name="geometryInterfaceID">The ID of the Geometry Interface to run the Macro on.</param>
    public static IEnumerator RunMacro(GIID geometryInterfaceID) {

		Macro macro;
        try {
            macro = Macro.FromGeometryInterface(geometryInterfaceID);
        } catch (System.Exception e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't run Macro: {0}",
                e.Message
            );
            failed = true;
            yield break;
        }

        //Get the file and load the root
		yield return macro.UserLoadMacroFile(); 
        if (failed) {yield break;};

        //Process root of the file
		yield return macro.ProcessRoot();
    }

    /// <summary>Constructs a Macro object from a geometry and an XElement</summary>
    /// <param name="geometry">The root Geometry of the Macro.</param>
    /// <param name="xElement">The root XElement of the Macro.</param>
    public Macro(Geometry geometry, XElement xElement) : base (geometry, xElement) {
        rootGeometry = geometry;
        rootX = xElement;
    }

    /// <summary>Constructs a Macro object from a Geometry Interface</summary>
    /// <param name="geometryInterfaceID">The ID of the Geometry Interface to run the Macro on.</param>
    static Macro FromGeometryInterface(GIID geometryInterfaceID) {

        Macro macro = new Macro(null, null);

        //Clear arrays and 'failed' flag
        geometryDict = new Dictionary<string, Geometry>();
        variableDict = new Dictionary<string, string>();
        arrayDict = new Dictionary<string, List<string>>();
        failed = false;

        //Load from Geometry Interface and get root Geometry
        macro.LoadRootGeometryFromGIID(geometryInterfaceID);
        if (failed) {
            throw new System.Exception("Failed to load from Geometry Interface!");
        }

        //Set root Geometry as the special Geometry accessible from the Macro
        geometryDict["$(GEOMETRY)"] = macro.rootGeometry;

        return macro;
    }


    /// <summary>Checks that a Geometry Interface is eligible and set Root Geometry</summary>
    /// <param name="geometryInterfaceID">The ID of the Geometry Interface to run the Macro on.</param>
    void LoadRootGeometryFromGIID(GIID geometryInterfaceID) {

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        GIS status = geometryInterface.status;
        if (
            status != GIS.OK && 
            status != GIS.COMPLETED && 
            status != GIS.WARNING && 
            status != GIS.DISABLED
        ) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Geometry Interface '{0}' is not in an eligible state ({1}) - Cannot proceed.",
                geometryInterfaceID,
                status
            );
            failed = true;
            return;
        }

        //Set root Geometry
        rootGeometry = geometryInterface.geometry;
    }

    /// <summary>Prompt the user to load an XML Macro file</summary>
    public IEnumerator UserLoadMacroFile() {
        
        FileSelector loadPrompt = FileSelector.main;

		//Set FileSelector to Load mode
		yield return loadPrompt.Initialise(saveMode:false, new List<string>{"xml"});
		//Wait for user response
		while (!loadPrompt.userResponded) {
			yield return null;
		}

		if (loadPrompt.cancelled) {
			GameObject.Destroy(loadPrompt.gameObject);
            NotificationBar.ClearTask(TID.RUN_MACRO);
            failed = true;
			yield break;
		}

		//Got a non-cancelled response from the user
		string path = loadPrompt.confirmedText;

		//Close the FileSelector
		GameObject.Destroy(loadPrompt.gameObject);
        yield return null;

		//Check the file exists
		if (!File.Exists(path)) {
			CustomLogger.LogFormat(EL.ERROR, "File does not exist: {0}", path);
			GameObject.Destroy(loadPrompt.gameObject);
            NotificationBar.ClearTask(TID.RUN_MACRO);
            failed = true;
			yield break;
		}

        XDocument xDocument;

        try {
            xDocument = FileIO.ReadXML(path);
        } catch (System.Exception e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Unable to parse '{0}' - not an XML file{2}{1}",
                path,
                e.ToString(),
                FileIO.newLine
            );
            failed = true;
            yield break;
        }

        rootX = xDocument.Element("macro");
        if (rootX == null) {
            Fail(rootX, "Element 'macro' not found in root of '{0}'.", path);
            yield break;
        }

    }

}

/// <summary>
/// Macro Group Object. 
/// Performs tasks parsed in from a Root XElement
/// </summary>
public class MacroGroup {

    /// <summary>Dictionary of Geometries available to the Macro Group.</summary>
    public static Dictionary<string, Geometry> geometryDict = new Dictionary<string, Geometry>();
    /// <summary>Dictionary of string variables available to the Macro Group.</summary>
    public static Dictionary<string, string> variableDict = new Dictionary<string, string>();
    /// <summary>Dictionary of array variables available to the Macro Group.</summary>
    public static Dictionary<string, List<string>> arrayDict = new Dictionary<string, List<string>>();
    /// <summary>True when a step fails in such a way that processing can't continue.</summary>
    public static bool failed;

    /// <summary>Root XElement that is parsed when this Macro Group is processed.</summary>
    public XElement rootX;
    /// <summary>Root Geometry that is referenced when this Macro Group is processed.</summary>
    public Geometry rootGeometry;


    /// <summary>Contructs a Macro Group Object.</summary>
    /// <param name="geometry">Root Geometry that is referenced when this Macro Group is processed.</param>
    /// <param name="xElement">Root XElement that is parsed when this Macro Group is processed.</param>
    public MacroGroup(Geometry geometry, XElement xElement) {
        rootGeometry = geometry;
        rootX = xElement;
    }


    ////////////////////
    // XML Processing //
    ////////////////////


    /// <summary>Parse and process this Macro Group's root XElement.</summary>
    public IEnumerator ProcessRoot() {
        if (rootX == null) {
            Fail(rootX, "Root element is empty!");
            yield break;
        }
        yield return ProcessElement(rootX);
    }

    /// <summary>Constructs a new MacroGroup, parses and processes it.</summary>
    IEnumerator ProcessGroup(Geometry groupGeometry, XElement groupX, string name) {

        //Make geometry accessible
        geometryDict[name] = groupGeometry;

        groupGeometry.name = name;

        //Create new Macro Group
        MacroGroup groupMacro = new MacroGroup(groupGeometry, groupX);
        
        //Process Macro Group using groupX
        yield return groupMacro.ProcessRoot();
        if (failed) {yield break;}

    }

    /// <summary>Parse and process the children of an XElement.</summary>
    /// <param name="parentX">The parent XElement.</param>
    public IEnumerator ProcessElement(XElement parentX) {

        if (parentX == null) {
            Fail(parentX, "Root element is empty!");
            failed = true;
            yield break;
        }

        foreach (XElement elementX in parentX.Elements()) {
            string elementName = elementX.Name.ToString().ToLower();
            switch (elementName) {
                case "var":         yield return ParseVariable(elementX);        break;
                case "uservar":     yield return ParseUserVariable(elementX);    break;
                case "userbool":    yield return ParseUserBool(elementX);        break;
                case "residuevar":  yield return ParseResidueVariable(elementX); break;
                case "array":       yield return ParseArray(elementX);           break;
                case "split":       yield return FormArray(elementX);            break;
                case "action":      yield return RunAction(elementX);            break;
                case "run":         yield return RunCalculation(elementX);       break;
                case "group":       yield return CreateGroup(elementX);          break;
                case "for":         yield return ForLoop(elementX);              break;
                case "foreach":     yield return ForEachLoop(elementX);          break;
                case "while":       yield return WhileLoop(elementX);            break;
                case "getitem":     yield return ElementAt(elementX);            break;
                default:
                    Fail(elementX, "Unrecognised Element '{0}'", elementName);
                    break;
            }
            if (failed) {yield break;}
        };
    }

    /// <summary>Create a new Macro Group.</summary>
    /// <param name="groupX">The new root XElement.</param>
    IEnumerator CreateGroup(XElement groupX) {

        string sourceName = ParseXMLAttrString(groupX, "source", "", rootGeometry);
        Geometry source = GetGeometry(groupX, "source", true);
        if (source == null) {
            Fail(groupX, "Source geometry '{0}' not found!", sourceName);
            yield break;
        }

        string type = ParseXMLAttrString(groupX, "type", "", source).ToLower();
        string groupName = ParseXMLAttrString(groupX, "name", "", source);

        CustomLogger.LogFormat(
            EL.INFO,
            "Created new Group with type '{0}', name '{1}' and source '{2}'.",
            type,
            groupName,
            sourceName
        );

        Geometry group;
        switch (type) {
            case "connected": // Perform actions on groups of residues that are connected
                foreach (List<ResidueID> residueGroup in source.GetGroupedResidues()) {
                    group = source.TakeResidues(residueGroup, null);
                    yield return ProcessGroup(group, groupX, groupName);
                }
                break;
            case "perResidue": // Perform actions on every residue individually
                foreach (ResidueID residueID in source.EnumerateResidueIDs()) {
                    group = source.TakeResidue(residueID, null);
                    yield return ProcessGroup(group, groupX, groupName);
                }
                break;
            case "geometry": // Perform actions on entire Geometry
                group = source.Take(null);
                yield return ProcessGroup(group, groupX, groupName);
                break;
            default:
                Fail(groupX, "Group type '{0}' not recognised.", type);
                yield break;
        }
    }

    /// <summary>Perform a Geometry-related action.</summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator RunAction(XElement actionX) {

        Geometry source = GetGeometry(actionX, "source", true);

        string actionID = ParseXMLAttrString(actionX, "id", "", source).ToLower();
        switch (actionID) {
            case "movetolayer":                yield return MoveToLayer(actionX);                break;
            case "estimatechargemultiplicity": yield return EstimateChargeMultiplicity(actionX); break;
            case "generateatommap":            yield return GenerateAtomMap(actionX);            break;
            case "computeconnectivity":        yield return CalculateConnectivity(actionX);      break;
            case "redistributecharge":         yield return RedistributeCharge(actionX);         break;
            case "roundcharge":                yield return RoundCharge(actionX);                break;
            case "save":                       yield return Save(actionX);                       break;
            case "load":                       yield return Load(actionX);                       break;
            case "userload":                   yield return UserLoad(actionX);                   break;
            case "report":                     yield return GenerateReport(actionX);             break;
            case "align":                      yield return AlignGeometry(actionX);              break;
            case "mutateresidue":              yield return MutateResidue(actionX);              break;
            default:
                Fail(actionX, "Action type '{0}' not recognised.", actionID);
                yield break;
        }
    }

    /////////////////////
    // Loop Processing //
    /////////////////////

    /// <summary>
    ///  Create an integer For Loop. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>var:    (required) Variable to set integer to.</para> <br />
    /// <para>start:  (optional) Integer to start count from (defaults to 0).</para> <br />
    /// <para>stop:   (required) Integer to stop count at, non-inclusively.</para> <br />
    /// <para>step:   (optional) Integer to step by (defaults to 1).</para> <br />
    /// <para>source: (optional) Geometry to use to expand attribute variables (defaults to parent's source).</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>any.</para> <br />
    /// <remark><br />
    /// Processes contents of loop for until var exceeds stop.
    /// </remark>
    /// </summary>
    /// <param name="forX">The XElement with details about the action.</param>
    public IEnumerator ForLoop(XElement forX) {
        // Loop from {start} to {stop} by {step} and processes contents

        Geometry source = GetGeometry(forX, "source", true);
        
        //Parse 
        string varStr = ParseXMLAttrString(forX, "var", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(varStr)) {
            Fail(forX, "Cannot process For Loop - Empty Variable ID!");
            yield break;
        }
        
        string startStr = ParseXMLAttrString(forX, "start", "", source);
        int start = 0;
        if (! string.IsNullOrWhiteSpace(startStr)) {
            if (!int.TryParse(startStr, out start)) {
                Fail(forX, "Cannot process For Loop - Failed to parse Start '{0}' as Integer!", startStr);
                yield break;
            }
        }
        
        string stepStr = ParseXMLAttrString(forX, "step", "", source);
        int step = 1;
        if (! string.IsNullOrWhiteSpace(stepStr)) {
            if (!int.TryParse(startStr, out step)) {
                Fail(forX, "Cannot process For Loop - Failed to parse Step '{0}' as Integer!", stepStr);
                yield break;
            }
        }
        
        string stopStr = ParseXMLAttrString(forX, "stop", "", source);
        int stop = 0;
        if (string.IsNullOrWhiteSpace(stopStr)) {
            Fail(forX, "Cannot process For Loop - Empty Stop variable!", stopStr);
            yield break;
        }
        if (!int.TryParse(stopStr, out stop)) {
            Fail(forX, "Cannot process For Loop - Failed to parse Stop '{0}' as Integer!", stopStr);
            yield break;
        }

        if (stop > start && step < 1) {
            Fail(forX, "Cannot process For Loop - Step is negative while Stop > Start!", stopStr);
            yield break;
        }

        if (start > stop && step > -1) {
            Fail(forX, "Cannot process For Loop - Step is position while Stop < Start!", stopStr);
            yield break;
        }

        for (int i=start; i<stop; i+=step) {
            
            variableDict[varStr] = i.ToString();
            yield return ProcessElement(forX);
        }

    }

    /// <summary>
    ///  Create an integer For Each Loop. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>array:  (required) Array to loop through.</para> <br />
    /// <para>var:    (required) Variable to set Array element to.</para> <br />
    /// <para>source: (optional) Geometry to use to expand attribute variables (defaults to parent's source).</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>any.</para> <br />
    /// </summary>
    /// <remark><br />
    /// Processes contents of loop for each item in the array.
    /// </remark>
    /// <param name="forEachX">The XElement with details about the action.</param>
    public IEnumerator ForEachLoop(XElement forEachX) {

        Geometry source = GetGeometry(forEachX, "source", true);

        string arrayID = ParseXMLAttrString(forEachX, "array", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(arrayID)) {
            Fail(forEachX, "Cannot process Array - Empty Array ID!");
            yield break;
        }
        
        string varStr = ParseXMLAttrString(forEachX, "var", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(varStr)) {
            Fail(forEachX, "Cannot process Array - Empty Variable ID!");
            yield break;
        }

        List<string> array;
        if (!arrayDict.TryGetValue(arrayID, out array)) {
            Fail(forEachX, "Cannot process Array - Array '{0}' not found!", arrayID);
            yield break;
        }

        foreach (string variable in array) {
            variableDict[varStr] = variable;
            yield return ProcessElement(forEachX);
        }
    }

    /// <summary>
    ///  Create a While Loop. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>id:     (required) ID of boolean variable to check.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>any.</para> <br />
    /// <para>source: (optional) Geometry to use to expand attribute variables (defaults to parent's source).</para> <br />
    /// </summary>
    /// <remark><br />
    /// Continues to process contents of loop until target boolean is false.
    /// </remark>
    /// <param name="whileX">The XElement with details about the action.</param>
    public IEnumerator WhileLoop(XElement whileX) {

        Geometry source = GetGeometry(whileX, "source", true);

        string boolID = ParseXMLAttrString(whileX, "id", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(boolID)) {
            Fail(whileX, "Cannot process While Loop - Empty Bool ID!");
            yield break;
        }
        
        if (!variableDict.ContainsKey(ExpandVariables(boolID, source))) {
            Fail(whileX, "Cannot process While Loop - Uninitialised Bool Variable '{0}'", boolID);
            yield break;
        }

        if (variableDict[ExpandVariables(boolID, source)] != "true") {
            Warn(whileX, "Bool Variable '{0}' was false before entering loop");
        }

        while (variableDict[ExpandVariables(boolID, source)] == "true") {
            yield return ProcessElement(whileX);
        }

    }

    //////////////////////
    // Array Processing //
    //////////////////////


    /// <summary>
    /// Retrieves the value of an Array with an index. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>id:     (required) ID of Array to access.</para> <br />
    /// <para>var:    (required) Variable ID to set using the Array item.</para> <br />
    /// <para>index:  (required) Index of Array item.</para> <br />
    /// <para>source: (optional) Geometry to use to expand attribute variables (defaults to parent's source).</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none.</para> <br />
    /// </summary>
    /// <param name="arrayX">XElement containing Array access details</param>
    IEnumerator ElementAt(XElement arrayX) {

        Geometry source = GetGeometry(arrayX, "source", true);

        string arrayID = ParseXMLAttrString(arrayX, "id", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(arrayID)) {
            Fail(arrayX, "Cannot get Array Element - Empty Array ID!");
            yield break;
        }
        List<string> array;
        if (!arrayDict.TryGetValue(arrayID, out array)) {
            Fail(arrayX, "Cannot get Array Element - Array '{0}' not found!", arrayID);
            yield break;
        }
        
        string varStr = ParseXMLAttrString(arrayX, "var", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(varStr)) {
            Fail(arrayX, "Cannot get Array Element - Empty Variable ID!");
            yield break;
        }
        
        string indexStr = ParseXMLAttrString(arrayX, "index", "", source);
        int index = 0;
        if (string.IsNullOrWhiteSpace(indexStr)) {
            Fail(arrayX, "Cannot get Array Element - Empty Index!");
            yield break;
        }
        if (!int.TryParse(indexStr, out index)) {
            Fail(arrayX, "Cannot get Array Element - Failed to parse Index '{0}' as Integer!", indexStr);
            yield break;
        }

        if (array.Count <= index || index < 0) {
            Fail(arrayX, "Cannot get Array Element - Index '{0}' out of bounds of array (length '{1}'!", index, array.Count);
            yield break;
        }

        variableDict[varStr] = array[index];

    }

    /////////////////////
    // Geometry Access //
    /////////////////////

    /// <summary>
    /// Retrieves a Geometry from geometryDict.
    /// </summary>
    /// <param name="name">Name of the Geometry.</param>
    Geometry GetGeometry(string name) {
        Geometry geometry;
        if (!geometryDict.TryGetValue(ExpandVariables(name), out geometry)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't get Geometry '{0}'.",
                name
            );
            failed = true;
            return null;
        }
        return geometry;
    }



    /// <summary>
    /// Retrieves a Geometry from geometryDict.
    /// </summary>
    /// <param name="xElement">Element containing Geometry name attribute.</param>
    /// <param name="attributeName">Name of the attribute that corresponds to the Geometry Name.</param>
    /// <param name="allowEmpty">Make the attribute optional, allowing this method to default to the Root Geometry.</param>
    Geometry GetGeometry(XElement xElement, string attributeName, bool allowEmpty=true) {

        string sourceName = ParseXMLAttrString(xElement, attributeName, "", rootGeometry);

        Geometry source = null;
        if (string.IsNullOrEmpty(sourceName) && allowEmpty) {
            source = GetGeometry("$(GEOMETRY)");
            if (source == null) {
                Fail(xElement, "Geometry: '{0}' not found!");
            }
        } else {
            source = GetGeometry(sourceName);
            if (source == null) {
                Fail(xElement, "Geometry: '{0}' not found!");
            }
        }
        return source;
    }

    /////////////
    // Actions //
    /////////////

    /// <summary>
    /// Move a Geometry or a subset of a Geometry to an ONIOM Layer. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>source:       (required) Geometry to change Layers.</para> <br />
    /// <para>residueState: (optional) Move only Residues of a particular Residue State.</para> <br />
    /// <para>residueIDs:   (optional) Move only these Residues.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none.</para> <br />
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator MoveToLayer(XElement actionX) {

        Geometry source = GetGeometry(actionX, "source", true);
        if (source == null) {
            Fail(actionX, "Cannot Move Layer - No source Geometry!");
            yield break;
        }

        OLID oniomLayerID = ExpandEnum<OLID>(actionX.Value);

        XAttribute residueStateX = actionX.Attribute("residueState");
        XAttribute residueIDsX = actionX.Attribute("residueIDs");
        if (residueStateX != null) {
            RS residueState = ExpandEnum<RS>(residueStateX.Value);
            foreach ((ResidueID residueID, Residue residue) in source.EnumerateResidues(x => x.state == residueState)) {
                foreach (Atom atom in residue.atoms.Values) {
                    atom.oniomLayer = oniomLayerID;
                }
            }
            if (residueIDsX != null) {
                Warn(actionX, "Ignoring 'residueIDs' parameter in MoveToLayer - 'residueState' already defined.");
            }
        } else if (residueIDsX != null) {

            foreach ((ResidueID residueID, Residue residue) in GetResiduesFromString(residueIDsX.Value, source, false)) {
                foreach (Atom atom in residue.atoms.Values) {
                    atom.oniomLayer = oniomLayerID;
                }
            }


        } else {
            foreach (Atom atom in source.EnumerateAtoms()) {
                atom.oniomLayer = oniomLayerID;
            }
        }

    }


    /// <summary>
    /// Sets the Calculator of a Geometry to have estimated charge and multiplicity. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>source:  (required) Geometry to estimate charge and multiplicity.</para> <br />
    /// <para>confirm: (optional) Require the user to confirm estimation result if 'true'.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none.</para> <br />
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator EstimateChargeMultiplicity(XElement actionX) {

        Geometry source = GetGeometry(actionX, "source", true);
        if (source == null) {
            Fail(actionX, "Cannot Estimate Charges/Multiplicity - No source Geometry!");
            yield break;
        }

        bool confirm = false;
        XAttribute confirmX = actionX.Attribute("confirm");
        if (confirmX != null && confirmX.Value == "true") {
            confirm = true;
        }

        yield return source.gaussianCalculator.EstimateChargeMultiplicity(confirm);

    }

    /// <summary>
    /// Generates a map between Atom Index and Atom ID, allowing Atoms to be retrieved after an external command is run. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>source:  (required) Geometry to compute Atom Map for.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none.</para> <br />
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator GenerateAtomMap(XElement actionX) {

        Geometry source = GetGeometry(actionX, "source", true);
        if (source == null) {
            Fail(actionX, "Cannot Generate Atom Map - No source Geometry!");
            yield break;
        }

        source.GenerateAtomMap();
        yield return null;

    }

    /// <summary>
    /// Computes the connectivity of a Geometry. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>source:  (required) Geometry to compute connectivity.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none.</para> <br />
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator CalculateConnectivity(XElement actionX) {

        Geometry source = GetGeometry(actionX, "source", true);
        if (source == null) {
            Fail(actionX, "Cannot Calculate Connectivity - No source Geometry!");
            yield break;
        }

        yield return Cleaner.CalculateConnectivity(source);

    }

    /// <summary>
    /// Smears partial charges from Link Atoms. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>source:       (required) Geometry to smear charges.</para> <br />
    /// <para>distribution: (optional) Space delimited redistribution of charges.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none.</para> <br />
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator RedistributeCharge(XElement actionX) {

        Geometry source = GetGeometry(actionX, "source", true);
        if (source == null) {
            Fail(actionX, "Cannot Redistribute Charges - No source Geometry!");
            yield break;
        }

        List<float> distribution = new List<float>();
        XElement distributionX = actionX.Element("distribution");
        if (distributionX != null) {
            try {
                string distributionString = distributionX.Value;
                foreach (string depthString in distributionString.Split(new[] {' '}, System.StringSplitOptions.RemoveEmptyEntries)) {
                    distribution.Add(float.Parse(depthString));
                }
            } catch {
                Fail(actionX,"Couldn't parse charge distribution.");
                yield break;
            }
        }

        yield return PartialChargeCalculator.RedistributeCharge(source, distribution);
    }


    /// <summary>
    /// Rounds the total charge of a Geometry to the nearest integer. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>source:       (required) Geometry to round charges.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none.</para> <br />
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator RoundCharge(XElement actionX) {

        Geometry source = GetGeometry(actionX, "source", true);
        if (source == null) {
            Fail(actionX, "Cannot Round Charges - No source Geometry!");
            yield break;
        }

        yield return PartialChargeCalculator.RoundCharge(source);
    }


    /// <summary>
    /// Saves the Geometry to a file. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>source:       (required) Geometry to save.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>name:         (required) Name (including extension) of file.</para> <br />
    /// <para>directory:    (optional) Directory to save to (defaults to Project Path).</para> <br />
    /// <para>connectivity: (optional) Whether to compute connectivity.</para> <br />
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator Save(XElement saveX) {

        Geometry source = GetGeometry(saveX, "source", true);
        if (source == null) {
            Fail(saveX, "No source Geometry to save!");
            yield break;
        }

        string directory;
        string directoryStr = ParseXMLString(saveX, "directory", "", source);
        if (string.IsNullOrWhiteSpace(directoryStr)) {
            directory = Settings.projectPath;
        } else {
            directory = string.Join(
                Path.DirectorySeparatorChar.ToString(), 
                directoryStr.Split(new char[] {'/', Path.DirectorySeparatorChar})
            );
        }

        string nameStr = ParseXMLString(saveX, "name", "", source);
        if (string.IsNullOrWhiteSpace(nameStr)) {
            Fail(saveX, "Cannot save with empty Name!");
            yield break;
        }

        string path = Path.Combine(directory, nameStr);

        if (!Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        bool computeconnectivity = (ParseXMLString(saveX, "connectivity", "false", source).ToLower() == "true");;

        FileWriter fileWriter;
        try {
            fileWriter = new FileWriter(
                source, 
                path, 
                computeconnectivity
            );
        } catch (System.Exception e) {
            Fail(saveX, "Failed to save Geometry to '{0}': {1}", path, e.Message);
            yield break;
        }


        yield return fileWriter.WriteFile();

        GameObject.Destroy(source);

        CustomLogger.LogFormat(
            EL.INFO,
            "Saved geometry '{0}' to '{1}'",
            source.name,
            path
        );
    }

    /// <summary>
    /// Loads the Geometry from a file. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>destination:  (required) Name of Geometry to use in Macro.</para> <br />
    /// <para>source: (optional) Geometry to use to expand attribute variables (defaults to parent's source).</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>name:         (required) Name (including extension) of file to load.</para> <br />
    /// <para>directory:    (optional) Directory to save to (defaults to Project Path).</para> <br />
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator Load(XElement loadX) {

        Geometry source = GetGeometry(loadX, "source", true);

        string destinationName = ParseXMLAttrString(loadX, "destination", "", source);

        if (string.IsNullOrEmpty(destinationName)) {
            Fail(loadX, "Must have a destination variable to load file to!");
            yield break;
        }

        string directory;
        string directoryStr = ParseXMLString(loadX, "directory", "", source);
        if (string.IsNullOrWhiteSpace(directoryStr)) {
            directory = Settings.projectPath;
        } else {
            directory = string.Join(
                Path.DirectorySeparatorChar.ToString(), 
                directoryStr.Split(new char[] {'/', Path.DirectorySeparatorChar})
            );
        }
        if (!Directory.Exists(directory)) {
            Fail(loadX, "Directory '{0}' does not exist!", directory);
            yield break;
        }

        string nameStr = ParseXMLString(loadX, "name", "", source);
        if (string.IsNullOrWhiteSpace(nameStr)) {
            Fail(loadX, "Cannot load file with empty Name!", directory);
            yield break;
        }

        string path = Path.Combine(directory, nameStr);

        if (!File.Exists(path)) {
            Fail(loadX, "File '{0}' does not exist!", path);
            yield break;
        }

        Geometry newGeometry = PrefabManager.InstantiateGeometry(null);

        yield return FileReader.LoadGeometry(newGeometry, path, "Macro");

        if (newGeometry == null) {
            Fail(loadX, "Failed to load Geometry from '{0}'", path);
            yield break;
        }

        geometryDict[destinationName] = newGeometry;
    }


    /// <summary>
    /// Prompt the User to load a Geometry from a user-selected file. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>destination:  (required) Name of Geometry to use in Macro.</para> <br />
    /// <para>source: (optional) Geometry to use to expand attribute variables (defaults to parent's source).</para> <br />
    /// <para>Elements: </para> <br />
    /// none.
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator UserLoad(XElement loadX) {

        Geometry source = GetGeometry(loadX, "source", true);

        string destinationName = ParseXMLAttrString(loadX, "destination", "", source);

        if (string.IsNullOrEmpty(destinationName)) {
            Fail(loadX, "Must have a destination variable to load file to!");
            yield break;
        }

		FileSelector loadPrompt = FileSelector.main;

		//Set FileSelector to Load mode
		yield return loadPrompt.Initialise(
            saveMode:false, 
            Flow.loadTypes,
            string.Format("Load Geometry '{0}'", destinationName)
        );
		//Wait for user response
		while (!loadPrompt.userResponded) {
			yield return null;
		}

		if (loadPrompt.cancelled) {
			GameObject.Destroy(loadPrompt.gameObject);
            failed = true;
			yield break;
		}

		//Got a non-cancelled response from the user
		string path = loadPrompt.confirmedText;
		//Close the FileSelector
		GameObject.Destroy(loadPrompt.gameObject);

        if (!File.Exists(path)) {
            Fail(loadX, "File '{0}' does not exist!", path);
            yield break;
        }

        Geometry newGeometry = PrefabManager.InstantiateGeometry(null);

        yield return FileReader.LoadGeometry(newGeometry, path, "Macro");

        if (newGeometry == null) {
            Fail(loadX, "Failed to load Geometry from '{0}'", path);
            yield break;
        }

        geometryDict[destinationName] = newGeometry;
    }


    /// <summary>
    /// Alings two Geometies. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>source: (optional) Geometry to align (defaults to parent's source).</para> <br />
    /// <para>target: (required) Target Geometry to align to.</para> <br />
    /// <para>Elements: </para> <br />
    /// none
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator AlignGeometry(XElement alignX) {

        Geometry source = GetGeometry(alignX, "source", true);
        if (source == null) {
            Fail(alignX, "Source Geometry not found in Alignment!");
            yield break;
        }
        Geometry target = GetGeometry(alignX, "alignto", false);
        if (target == null) {
            Fail(alignX, "Target Geometry not found in Alignment!");
            yield break;
        }

        yield return source.AlignTo(target);
        
    }

    /// <summary>
    /// Append information to a log file. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>source:   (optional) Geometry to use to expand attribute variables (defaults to parent's source).</para> <br />
    /// <para>ovewrite: (optional) Whether to overwrite an existing file or append to it.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>name:      (required) Name (including extension) of file to load.</para> <br />
    /// <para>directory: (optional) Directory to save to (defaults to Project Path).</para> <br />
    /// <para>items:     (optional) See 'Items' below.</para> <br />
    /// <para>Items: </para> <br />
    /// <para>text:            Write text contents with variables expanded.</para> <br />
    /// <para>geometry:        Write information about the Geometry (see GetGeometryReportStrings).</para> <br />
    /// <para>residues:        Write information about the Geometry's Residues (see GetResidueReportStrings).</para> <br />
    /// <para>atoms:           Write information about the Geometry's Atoms (see GetAtomsReportStrings).</para> <br />
    /// <para>compareGeometry: Unused.</para> <br />
    /// <para>compareResidues: Unused.</para> <br />
    /// <para>compareAtoms:    Unused.</para> <br />
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator GenerateReport(XElement reportX) {

        Geometry source = GetGeometry(reportX, "source", true);
        if (source == null) {
            Fail(reportX, "Source Geometry not found in Report Generation!");
            yield break;
        }

        string directory;
        string directoryStr = ParseXMLString(reportX, "directory", "", source);
        if (string.IsNullOrWhiteSpace(directoryStr)) {
            directory = Settings.projectPath;
        } else {
            directory = string.Join(
                Path.DirectorySeparatorChar.ToString(), 
                directoryStr.Split(new char[] {'/', Path.DirectorySeparatorChar})
            );
        }

        string nameStr = ParseXMLString(reportX, "name", "", source);
        if (string.IsNullOrWhiteSpace(nameStr)) {
            Fail(reportX, "Cannot generate report with empty Name!");
            failed = true;
            yield break;
        }

        string path = Path.Combine(directory, nameStr);

        if (!Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        bool overwrite = (ParseXMLAttrString(reportX, "overwrite", "false", source).ToLower() == "true");

        if (overwrite) {
            File.Create(path).Close();
        }

        XElement itemsX = reportX.Element("items");
        if (itemsX == null) {
            yield break;
        }

        using (StreamWriter streamWriter = File.AppendText(path)) {

            foreach (XElement itemX in itemsX.Elements()) {
                string itemName = itemX.Name.ToString().ToLower();
                switch (itemName) {
                    case ("text"):
                        streamWriter.WriteLine(ExpandVariables(itemX.Value, source));
                        break;
                    case ("geometry"):
                        foreach (string reportStr in GetGeometryReportStrings(itemX, source)) {
                            streamWriter.WriteLine(reportStr);
                            if (Timer.yieldNow) {yield return null;}
                        }
                        break;
                    case ("residues"):
                        foreach (string reportStr in GetResidueReportStrings(itemX, source)) {
                            streamWriter.WriteLine(reportStr);
                            if (Timer.yieldNow) {yield return null;}
                        }
                        break;
                    case ("atoms"):
                        foreach (string reportStr in GetAtomsReportStrings(itemX, source)) {
                            streamWriter.WriteLine(reportStr);
                            if (Timer.yieldNow) {yield return null;}
                        }
                        break;
                    case ("compareGeometry"):

                        break;
                    case ("compareResidues"):

                        break;
                    case ("compareAtoms"):

                        break;
                    default:
                        Fail(itemX, "Unrecognised Report Item '{0}'!", itemName);
                        break;
                }
                if (Timer.yieldNow) {yield return null;}
            }
        }
    }

    /// <summary>
    /// Mutate a Geometry's Residue to a Target. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>source:   (optional) Geometry to use to expand attribute variables (defaults to parent's source).</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>residueID: (required) The ID of the Residue to mutate.</para> <br />
    /// <para>target:    (required) 3-Letter Amino Acid Name to mutate to.</para> <br />
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator MutateResidue(XElement actionX) {

        Geometry source = GetGeometry(actionX, "source", true);
        if (source == null) {
            Fail(actionX, "Cannot mutate Residue - No source Geometry!");
            yield break;
        }

        //Get the Residue to mutate
        XElement residueIDX;
        ResidueID residueID;
        if ((residueIDX = actionX.Element("residueID")) == null) {
            Fail(actionX, "Residue ID variable missing in MutateResidue in Group '{0}'", source.name);
            yield break;
        }

        try {
            residueID = ResidueID.FromString(ExpandVariables(residueIDX.Value));
        } catch (System.Exception e) {
            Fail(residueIDX, "Failed to parse Residue ID in MutateResidue in Group '{0}': {1}", source.name, e.Message);
            yield break;
        }

        Residue oldResidue;
        if (! source.TryGetResidue(residueID, out oldResidue)) {
            Fail(residueIDX, "Couldn't find Residue ID '{0}' in Group '{1}'", residueID,source.name);
            yield break;
        }
        string oldResidueName = oldResidue.residueName;

        //Get the Residue to mutate to
        Residue newResidue;
        string targetStr = ParseXMLString(actionX, "target", "", source).ToUpper();
        if (string.IsNullOrWhiteSpace(targetStr)) {
            Fail(actionX, "Empty Target Variable ID in MutateResidue!");
            yield break;
        }
        
        try {
            newResidue = Residue.FromString(targetStr);
        } catch (System.Exception e) {
            Fail(actionX, "Failed to parse Target Residue in MutateResidue in Group '{0}': {1}", source.name, e.Message);
            yield break;
        }

        bool optimise = (ParseXMLString(actionX, "optimise", "false", source).ToLower() == "true");

        ResidueMutator residueMutator;
        try {
            residueMutator = new ResidueMutator(source, residueID);
        } catch (System.Exception e) {
            Fail(actionX, "Failed to Mutate Residue in Group '{0}': {1}", source.name, e.Message);
            yield break;
        }

        yield return residueMutator.MutateStandard(
            newResidue,
            optimise
        );

        CustomLogger.LogFormat(
            EL.INFO,
            "Mutated Residue '{0}' in '{1}' with clash score: {2,8:#.##E+00} ('{3}' -> '{4}')",
            residueID,
            source.name,
            residueMutator.clashScore,
            oldResidueName,
            targetStr
            
        );
    }

    //////////////////
    // Calculations //
    //////////////////


    /// <summary>
    /// Run a Gaussian Calculation. <br /> 
    /// <para>Attributes: </para> <br />
    /// <para>path:    (required) Base path (excluding extension) of the calculation.</para> <br />
    /// <para>source:  (optional) Geometry to use to expand attribute variables (defaults to parent's source).</para> <br />
    /// <para>atomMap: (optional) Source geometry to take atomMap to (defaults to source).</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>nproc: (optional) Number of processors to use.</para> <br />
    /// <para>mem:   (optional) Amount of memory in MB to use.</para> <br />
    /// <para>opt:   (optional) Whether to optimise. Optional 'threshold' attribute: $(NORMAL), $(TIGHT), $(VERY_TIGHT), $(LOOSE), $(EXPERT).</para> <br />
    /// <para>keywords:        (optional) Additional keywords to use (space delimited).</para> <br />
    /// <para>layer:           (optional, multiple) Details about each ONIOM Layer (see Layer Options below). Required 'id' attribute: $(REAL), $(INTERMEDIATE), $(MODEL).</para> <br />
    /// <para>version:         (optional) The version of Gaussian to use (defaults to default Gaussian version).</para> <br />
    /// <para>prompt:          (optional) Whether to ask user to confirm details and calculation.</para> <br />
    /// <para>updatePositions: (optional) Whether to update a Geometry's position. Optional 'destination' attribute (defaults to source).</para> <br />
    /// <para>updateCharges:   (optional) Whether to update a Geometry's partial charges. Optional 'destination' attribute (defaults to source).</para> <br />
    /// <para>updateAmbers:    (optional) Whether to update a Geometry's AMBER types. Optional 'destination' attribute (defaults to source).</para> <br />
    /// <para>Layer Options: </para> <br />
    /// <para>method:  (optional) Electronic structure/Energy method to use.</para> <br />
    /// <para>basis:   (optional) Basis set to use.</para> <br />
    /// <para>options: (optional) Additional options for layer.</para> <br />
    /// </summary>
    /// <param name="actionX">The XElement with details about the action.</param>
    IEnumerator RunCalculation(XElement calculationX) {

        Geometry source = GetGeometry(calculationX, "source", true);
        if (source == null) {
            Fail(calculationX, "Cannot run Calculation - No source Geometry!");
            yield break;
        }

        string basePath = ParseXMLAttrString(calculationX, "path", "", source);
        basePath = ExpandVariables(basePath, source);

        Geometry atomMapSource = GetGeometry(calculationX, "atommap", false);
        if (atomMapSource != null) {
            source.atomMap = atomMapSource.atomMap;
        }
        GaussianCalculator gc = source.gaussianCalculator;

        XElement nProcX;
        if ((nProcX = calculationX.Element("nproc")) != null) {
            if (!int.TryParse(ExpandVariables(nProcX.Value), out gc.numProcessors)) {
                Fail(nProcX, "Invalid 'nproc' attribute: {0}", nProcX.Value);
                yield break;
            }
        }

        XElement memX;
        if ((memX = calculationX.Element("mem")) != null) {
            if (!int.TryParse(ExpandVariables(memX.Value), out gc.jobMemoryMB)) {
                Fail(memX, "Invalid 'mem' attribute: {0}", memX.Value);
                yield break;
            }
        }

        XElement optX;
        if ((optX = calculationX.Element("opt")) != null) {
            string thresholdStr = ParseXMLAttrString(optX, "threshold", "", source);
            gc.convergenceThreshold = ExpandEnum<GCT>(thresholdStr);
            gc.doOptimisation = true;
        } else {
            gc.doOptimisation = false;
        }

        XElement titleX = calculationX.Element("title");
        if (titleX == null || string.IsNullOrWhiteSpace(titleX.Value)) {
            Warn(titleX, "Calculation does not have a 'title' element.", nProcX.Value);
            gc.title = "Title";
        } else {
            gc.title = ExpandVariables(titleX.Value);
        }

        foreach (XElement layerX in calculationX.Elements("layer")) {

            //Parse Layer ID
            string oniomLayerIDStr = FileIO.ParseXMLAttrString(layerX, "id", "");
            if (string.IsNullOrWhiteSpace(oniomLayerIDStr)) {
                Fail(layerX, "No 'id' tag in 'layer' element!");
                yield break;
            }
            OLID oniomLayerID = ExpandEnum<OLID>(oniomLayerIDStr);
            Layer layer = gc.layerDict[oniomLayerID];

            //Parse Layer Method
            string methodStr = ParseXMLAttrString(layerX, "method", "");
            if (!string.IsNullOrWhiteSpace(methodStr)) {
                layer.method = methodStr;
            }

            //Parse Layer Basis
            string basisStr = ParseXMLAttrString(layerX, "basis", "");
            if (!string.IsNullOrWhiteSpace(basisStr)) {
                layer.basis = basisStr;
            }

            //Parse Layer Options
            string optionsStr = ParseXMLAttrString(layerX, "options", "");
            if (!string.IsNullOrWhiteSpace(optionsStr)) {
                layer.options = optionsStr.Split(new[] {','}, System.StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        //Parse Calculation Keywords
        XElement keywordsX = calculationX.Element("keywords");
        if (keywordsX != null) {
            string keywordsStr = keywordsX.Value;
            gc.additionalKeywords = keywordsStr.Split(new[] {' '}, System.StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        //Parse Version
        Bash.ExternalCommand gaussian = GaussianCalculator.GetGaussian();
        XElement gaussianVersionX = calculationX.Element("version");
        if (gaussianVersionX != null) {
            try {
                gaussian = Settings.GetExternalCommand(gaussianVersionX.Value);
            } catch (KeyNotFoundException) {}
        }

        if (calculationX.Element("prompt") != null) {
            yield return CalculationSetup.SetupCalculation(source);
        }

        int electrons = source.EnumerateAtomIDs().Select(x => x.pdbID.atomicNumber).Sum();
        
        gaussian.WriteInputAndExecute(
            source,
            TID.RUN_MACRO,
            true,
            false,
            true,
            (float)(CustomMathematics.IntPow(electrons, 4) / 1000000000),
            basePath + ".gjf",
            basePath + ".log"
        );

        //Update Positions
        XElement updateGeometryX = calculationX.Element("updateGeometry");
        if (updateGeometryX != null) {
            yield return UpdateGeometryFromOutput(source, updateGeometryX, basePath);
        }
    }

    /// <summary>
    /// Update information about a Geometry.
    /// <para>Attributes: </para> <br />
    /// <para>destination: (optional) Geometry to update (defaults to parent's source).</para> <br />
    /// <para>positions:   (optional, true/false) Whether to update positions (defaults to false).</para> <br />
    /// <para>charges:     (optional, true/false) Whether to update charges (defaults to false).</para> <br />
    /// <para>ambers:      (optional, true/false) Whether to update ambers (defaults to false).</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none.</para> <br />
    /// </summary>
    /// <param name="source">Default Geometry to update if desination isn't given</param>
    /// <param name="updateX">XElement containing information about Geometry update</param>
    /// <param name="basePath">Path of file to get information from</param>
    IEnumerator UpdateGeometryFromOutput(
        Geometry source,
        XElement updateX, 
        string basePath
    ) {
        if (updateX == null) {
            yield break;
        }

        Geometry destination;
        if (updateX.Attribute("destination") != null) {
            destination = GetGeometry(updateX, "destination", true);
        } else {
            destination = source;
        }

        bool updatePositions = (ParseXMLAttrString(updateX, "positions", "false", source).ToLower() == "true");
        bool updateCharges = (ParseXMLAttrString(updateX, "charges", "false", source).ToLower() == "true");
        bool updateAmbers = (ParseXMLAttrString(updateX, "ambers", "false", source).ToLower() == "true");

        if (destination == null) {
            Fail(updateX, "'destination' Geometry not found!");
            yield break;
        } 

        yield return FileReader.UpdateGeometry(
            destination, 
            basePath + ".log", 
            updatePositions:updatePositions, 
            updateCharges:updateCharges, 
            updateAmbers:updateAmbers, 
            atomMap:source.atomMap
        );
    
    }

    ///////////////
    // Reporting //
    ///////////////

    /// <summary>
    /// Enumerate strings for a Geometry Report.
    /// <para>Attributes: </para> <br />
    /// <para>source: (optional) Geometry to report (defaults to defaultSource).</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>items:     (optional) See 'Items' below.</para> <br />
    /// <para>Items: </para> <br />
    /// <para>numAtoms:    Report the number of Atoms.</para> <br />
    /// <para>numresidues: Report the number of Residues.</para> <br />
    /// <para>sequence:    Report the Amino Acid sequence.</para> <br />
    /// <para>mass:        Report the total mass.</para> <br />
    /// <para>charge:      Report the total charge.</para> <br />
    /// </summary>
    /// <param name="reportX">XElement containing information about Geometry Report.</param>
    /// <param name="defaultSource">Default Geometry to report if source isn't given</param>
    IEnumerable<string> GetGeometryReportStrings(XElement reportX, Geometry defaultSource) {

        //Allow optional source
        Geometry source = (reportX.Attribute("source") == null)
            ? defaultSource
            : GetGeometry(reportX, "source");

        foreach (XElement itemX in reportX.Elements()) {
            string itemName = itemX.Name.ToString().ToLower();
            switch (itemName) {
                case ("numatoms"):
                    yield return string.Format("Number of Atoms: {0}", source.size);
                    break;
                case ("numresidues"):
                    yield return string.Format("Number of Residue: {0}", source.residueCount);
                    break;
                case ("sequence"):
                    foreach (string chainID in source.GetChainIDs()) {
                        yield return string.Format("Chain '{0}' Sequence: {1}", chainID, source.GetSequence(chainID));
                    }
                    break;
                case ("mass"):
                    yield return string.Format("Mass: {0,6:###.00} Da", source.GetMass());
                    break;
                case "charge":
                     yield return string.Format("Charge: {0,6:###.00} au", source.GetCharge()); 
                    break;
                default:
                    Fail(itemX, "Unrecognised Geometry Report Item '{0}'!", itemName);
                    break;
            }
        }
    }

    /// <summary>
    /// Enumerate strings for a Residue Report.
    /// <para>Attributes: </para> <br />
    /// <para>source:     (optional) Geometry to report (defaults to defaultSource).</para> <br />
    /// <para>residueIDs: (optional) List of Residue IDs to report (defaults to every Residue).</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>items:     (optional) See 'Items' below.</para> <br />
    /// <para>Items: </para> <br />
    /// <para>positions:     Report the postions of each Residue's Atoms.</para> <br />
    /// <para>torsionangles: Report the side-chain torsion angles of each Residue.</para> <br />
    /// <para>contact:       Report any close distances with neighbouring Reisdues.</para> <br />
    /// </summary>
    /// <param name="reportX">XElement containing information about Geometry Report.</param>
    /// <param name="defaultSource">Default Geometry to report if source isn't given</param>
    IEnumerable<string> GetResidueReportStrings(XElement reportX, Geometry defaultSource) {

        //Allow optional source
        Geometry source = (reportX.Attribute("source") == null)
            ? defaultSource
            : GetGeometry(reportX, "source");

        string residueIDsStr = ParseXMLAttrString(reportX, "residueIDs", "", source);
        List<(ResidueID, Residue)> residues = GetResiduesFromString(residueIDsStr, source, true).ToList();

        if (failed) {yield break;}

        foreach (XElement itemX in reportX.Elements()) {
            string itemName = itemX.Name.ToString().ToLower();
            switch (itemName) {
                case "positions":
                    foreach ((ResidueID residueID, Residue residue) in residues) {
                        yield return string.Format(
                            "Positions of {0} ({1})",
                            residueID,
                            residue.residueName
                        );
                        yield return "PDBID        x        y        z";
                        foreach ((PDBID pdbID, Atom atom) in residue.atoms) {
                            float3 position = atom.position;
                            yield return string.Format(
                                "{0,5} {1,8:.000} {2,8:.000} {3,8:.000}",
                                pdbID,
                                position.x,
                                position.y,
                                position.z
                            );
                        }
                        yield return "";
                    }
                    yield return "";
                    break;
                case "torsionangles":
                    foreach ((ResidueID residueID, Residue residue) in residues) {

                        AminoAcid aminoAcid;
                        if (!Data.aminoAcids.TryGetValue(residue.residueName, out aminoAcid)) {
                            yield return string.Format(
                                "Residue '{0}' not present in Amino Acid Database.",
                                residue.residueName
                            );
                        }

                        PDBID[][] dihedralGroups = aminoAcid.GetDihedralPDBIDs(residue.state);

                        yield return string.Format(
                            "Torsion Angles of {0} ({1})",
                            residueID,
                            residue.residueName
                        );

                        yield return "ID1   ID2   ID3   ID4   Angle";

                        foreach (PDBID[] dihedralGroup in dihedralGroups) {
                            Atom atom0 = residue.atoms[dihedralGroup[0]];
                            Atom atom1 = residue.atoms[dihedralGroup[1]];
                            Atom atom2 = residue.atoms[dihedralGroup[2]];
                            Atom atom3 = residue.atoms[dihedralGroup[3]];
                            float dihedral = CustomMathematics.GetDihedral(atom0, atom1, atom2, atom3);
                            yield return string.Format(
                                "{0,5} {1,5} {2,5} {3,5} {4,7:.00}",
                                dihedralGroup[0],
                                dihedralGroup[1],
                                dihedralGroup[2],
                                dihedralGroup[3],
                                dihedral
                            );
                        }
                        yield return "";
                    }
                    yield return "";
                    break;
                case "contact":
                    string searchDistanceStr = ParseXMLAttrString(itemX, "searchDistance", "8", source);
                    float searchDistance;
                    if (!float.TryParse(searchDistanceStr, out searchDistance)) {
                        Fail(itemX, "Failed to parse Search Distance '{0}' as a Float!", searchDistanceStr);
                        yield break;
                    }
                    float searchDistanceSq = searchDistance * searchDistance;

                    string thresholdScaleStr = ParseXMLAttrString(itemX, "scale", "8", source);
                    float thresholdScale;
                    if (!float.TryParse(thresholdScaleStr, out thresholdScale)) {
                        Fail(itemX, "Failed to parse Threshold Scale '{0}' as a Float!", thresholdScaleStr);
                        yield break;
                    }
                    float thresholdScaleSq = thresholdScale * thresholdScale;

                    Dictionary<ResidueID, float3> centroids = source.EnumerateResidues()
                        .ToDictionary(x => x.residueID, x => x.residue.GetCentre());

                    foreach ((ResidueID residueID, Residue residue) in residues) {
                        yield return string.Format(
                            "Close Contacts of {0} ({1})",
                            residueID,
                            residue.residueName
                        );
                        yield return "Atom ID   Atom ID   Distance";

                        foreach ((ResidueID closeResidueID, float3 centroid) in centroids) {

                            if (math.distancesq(centroids[residue.residueID], centroid) > searchDistanceSq) {
                                //Residue is far enought to ignore 
                                continue;
                            }

                            //Residues nearby

                            foreach ((PDBID closePDBID, Atom closeAtom) in source.GetResidue(closeResidueID).atoms) {

                                AtomID closeAtomID = new AtomID(closeResidueID, closePDBID);

                                foreach ((PDBID pdbID, Atom atom) in residue.atoms) {

                                    AtomID atomID = new AtomID(residue.residueID, pdbID);

                                    if (atomID == closeAtomID) {continue;}

                                    float distanceSq = math.distancesq(atom.position, closeAtom.position);
                                    if (Data.GetBondOrderDistanceSquared(closePDBID.element, pdbID.element, distanceSq * thresholdScaleSq) == BT.NONE) {
                                        //Atoms are far enough
                                        continue;
                                    }

                                    if (atom.externalConnections.ContainsKey(closeAtomID)) {
                                        //Atoms are connected anyway
                                        continue;
                                    }

                                    yield return string.Format(
                                        "{0,8} {1,8} {2,8:.000}",
                                        atomID,
                                        closeAtomID,
                                        math.sqrt(distanceSq)
                                    );
                                    
                                }
                            }
                            
                        }
                        yield return "";

                    }
                    yield return "";
                    break;
                default:
                    Fail(itemX, "Unrecognised Residue Report Item '{0}'!", itemName);
                    break;
            }
        }
    }

    /// <summary>
    /// Enumerate strings for an Atoms Report.
    /// <para>Attributes: </para> <br />
    /// <para>source:  (optional) Geometry to report (defaults to defaultSource).</para> <br />
    /// <para>atomIDs: (optional) List of Atom IDs to report (defaults to every Atom).</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>items:     (optional) See 'Items' below.</para> <br />
    /// <para>Items: </para> <br />
    /// <para>positions:     Report the postions of each Atoms.</para> <br />
    /// </summary>
    /// <param name="reportX">XElement containing information about Geometry Report.</param>
    /// <param name="defaultSource">Default Geometry to report if source isn't given</param>
    IEnumerable<string> GetAtomsReportStrings(XElement reportX, Geometry defaultSource) {

        //Allow optional source
        Geometry source = (reportX.Attribute("source") == null)
            ? defaultSource
            : GetGeometry(reportX, "source");

        string atomIDsStr = ParseXMLAttrString(reportX, "atomids", "", source);
        List<(AtomID, Atom)> atoms = GetAtomsFromString(atomIDsStr, source, true).ToList();

        if (failed) {yield break;}

        foreach (XElement itemX in reportX.Elements()) {
            string itemName = itemX.Name.ToString().ToLower();
            switch (itemName) {
                case "positions":
                    yield return "Atomic Positions";
                    yield return "Atom ID          x        y        z";
                    foreach ((AtomID atomID, Atom atom) in atoms) {
                        float3 position = atom.position;
                        yield return string.Format(
                            "{0,8} {1,8:.000} {2,8:.000} {3,8:.000}",
                            atomID,
                            position.x,
                            position.y,
                            position.z
                        );
                        yield return "";
                    }
                    yield return "";
                    break;
                default:
                    Fail(itemX, "Unrecognised Residue Report Item '{0}'!", itemName);
                    break;
            }
        }
    }

    /////////////
    // Parsing //
    /////////////


    /// <summary>
    /// Reads a variable string and adds it to variableDict.
    /// <para>Attributes: </para> <br />
    /// <para>source: (optional) Geometry to expand variables from (defaults to rootGeometry).</para> <br />
    /// <para>id:     (required) Key for the variable.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none. </para> <br />
    /// </summary>
    /// <remark> <br />
    /// Text is set to the contents of the element
    /// </remark>
    /// <param name="varX">XElement containing variable.</param>
    public IEnumerator ParseVariable(XElement varX) {

        Geometry source = GetGeometry(varX, "source", true);

        string idStr = ParseXMLAttrString(varX, "id", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            Fail(varX, "Cannot parse Variable - Empty Variable ID!");
            yield break;
        }
        string valueStr = ExpandVariables(varX.Value, source);

        CustomLogger.LogFormat(
            EL.INFO,
            "Setting Variable '{0}' to '{1}'.",
            idStr,
            valueStr
        );
        variableDict[idStr] = valueStr;

        if (Timer.yieldNow) {yield return null;}

    }

    /// <summary>
    /// Forms an array by splitting text value.
    /// <para>Attributes: </para> <br />
    /// <para>source:    (optional) Geometry to expand variables from (defaults to rootGeometry).</para> <br />
    /// <para>id:        (required) Key for the variable.</para> <br />
    /// <para>delimiter: (required, 1 char) Delimiter to split the array.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none. </para> <br />
    /// </summary>
    /// <param name="varX">XElement containing variable.</param>
    public IEnumerator FormArray(XElement xElement) {

        Geometry source = GetGeometry(xElement, "source", true);

        string idStr = ParseXMLAttrString(xElement, "id", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            Fail(xElement, "Cannot Form Array - Empty Array ID!");
            yield break;
        }

        string delimiterStr = ParseXMLAttrString(xElement, "delimiter", "", source);
        if (string.IsNullOrWhiteSpace(delimiterStr)) {
            Fail(xElement, "Cannot Form Array - Empty Delimiter!");
            yield break;
        }

        if (delimiterStr.Length != 1) {
            Fail(xElement, "Invalid length of delimiter ({0}) - must be 1!", delimiterStr.Length);
            yield break;
        }
        char delimiter = delimiterStr[0];

        List<string> stringArray = new List<string>();

        string valueStr = ExpandVariables(xElement.Value, source);

        foreach (string rawValue in valueStr.Split(new char[] {delimiter})) {

            string itemStr = ExpandVariables(rawValue, source);
            stringArray.Add(itemStr);
            
            if (Timer.yieldNow) {yield return null;}

        }

        CustomLogger.LogFormat(
            EL.INFO,
            "Setting Array '{0}': {1} values: '{2}'",
            idStr,
            stringArray.Count,
            string.Join("', '", stringArray)
        );
        arrayDict[idStr] = stringArray;

    }

    /// <summary>
    /// Forms an array by reading individual elements.
    /// <para>Attributes: </para> <br />
    /// <para>source:    (optional) Geometry to expand variables from (defaults to rootGeometry).</para> <br />
    /// <para>id:        (required) Key for the variable.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>items: (See 'Items' below)</para> <br />
    /// <para>Items: </para> <br />
    /// <para>item: An item (in order) of the array. </para> <br />
    /// </summary>
    /// <param name="varX">XElement containing variable.</param>
    public IEnumerator ParseArray(XElement arrayX) {

        Geometry source = GetGeometry(arrayX, "source", true);

        string idStr = ParseXMLAttrString(arrayX, "id", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            Fail(arrayX, "Cannot parse Array - Empty Array ID!");
            yield break;
        }

        List<string> stringArray = new List<string>();

        foreach (XElement elementX in arrayX.Elements()) {
            string elementName = elementX.Name.ToString().ToLower();
            if (elementName != "item") {
                Fail(arrayX, "Cannot parse Array - unrecognised XElement '{0}'", elementName);
                yield break;
            }

            string valueStr = ExpandVariables(elementX.Value, source);
            stringArray.Add(valueStr);

            if (Timer.yieldNow) {yield return null;}
        }

        CustomLogger.LogFormat(
            EL.INFO,
            "Setting Array '{0}': {1} values: '{2}'",
            idStr,
            stringArray.Count,
            string.Join("', '", stringArray)
        );
        arrayDict[idStr] = stringArray;
    }

    /// <summary>
    /// Read a variable definied by the user.
    /// <para>Attributes: </para> <br />
    /// <para>source:      (optional) Geometry to expand variables from (defaults to rootGeometry).</para> <br />
    /// <para>id:          (required) Key for the variable.</para> <br />
    /// <para>title:       (required) Prompt title.</para> <br />
    /// <para>description: (required) Prompt description.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none. </para> <br />
    /// </summary>
    /// <param name="varX">XElement containing variable.</param>
    IEnumerator ParseUserVariable(XElement varX) {

        Geometry source = GetGeometry(varX, "source", true);

        string idStr = ParseXMLAttrString(varX, "id", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            Fail(varX, "Cannot parse User Variable - Empty Variable ID!");
            yield break;
        }
        string initValue = varX.Value;
        
        string titleStr = ParseXMLAttrString(varX, "title", "", source);
        if (string.IsNullOrWhiteSpace(titleStr)) {
            Fail(varX, "Cannot parse User Variable - Empty Title attribute!");
            yield break;
        }
        
        string promptStr = ParseXMLAttrString(varX, "prompt", "", source);
        if (string.IsNullOrWhiteSpace(promptStr)) {
            Warn(varX, "Empty Prompt attribute for {0}", idStr);
            yield break;
        }

        bool cancelled = false;

        MultiPrompt multiPrompt = MultiPrompt.main;
        multiPrompt.Initialise(
            titleStr,
            promptStr,
            new ButtonSetup("Confirm", () => {}),
            new ButtonSetup("Cancel", () => {cancelled = true;}),
            input: true
        );

        multiPrompt.inputField.text = initValue;

        while (!multiPrompt.userResponded) {
            yield return null;
        }


        string valueStr;

        multiPrompt.Hide();

        if (cancelled || multiPrompt.cancelled) {
            failed = true;
            yield break;
        } else {
            valueStr = multiPrompt.inputField.text;
        }


        CustomLogger.LogFormat(
            EL.INFO,
            "Setting User Variable '{0}' to '{1}'.",
            idStr,
            valueStr
        );
        variableDict[idStr] = valueStr;

        if (Timer.yieldNow) {yield return null;}
    }

    /// <summary>
    /// Read a boolean definied by the user.
    /// <para>Attributes: </para> <br />
    /// <para>source:      (optional) Geometry to expand variables from (defaults to rootGeometry).</para> <br />
    /// <para>id:          (required) Key for the variable.</para> <br />
    /// <para>title:       (required) Prompt title.</para> <br />
    /// <para>description: (required) Prompt description.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>none. </para> <br />
    /// </summary>
    /// <param name="varX">XElement containing variable.</param>
    IEnumerator ParseUserBool(XElement varX) {

        Geometry source = GetGeometry(varX, "source", true);

        string idStr = ParseXMLAttrString(varX, "id", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            Fail(varX, "Cannot parse User Boolean - Empty Variable ID!");
            yield break;
        }
        string initValue = varX.Value;
        
        string titleStr = ParseXMLAttrString(varX, "title", "", source);
        if (string.IsNullOrWhiteSpace(titleStr)) {
            Fail(varX, "Cannot parse User Boolean - Empty Title attribute!");
        }
        
        string promptStr = ParseXMLAttrString(varX, "prompt", "", source);
        if (string.IsNullOrWhiteSpace(promptStr)) {
            Warn(varX, "Empty Prompt attribute for {0}", idStr);
        }

        bool userBool = false;

        MultiPrompt multiPrompt = MultiPrompt.main;
        multiPrompt.Initialise(
            titleStr,
            promptStr,
            new ButtonSetup("Yes", () => {userBool = true; }),
            new ButtonSetup("No",  () => {userBool = false;}),
            input:false
        );

        while (!multiPrompt.userResponded) {
            yield return null;
        }

        multiPrompt.Hide();

        if (multiPrompt.cancelled) {
            failed = true;
            yield break;
        }

        string finalBool = userBool ? "true" : "false";

        CustomLogger.LogFormat(
            EL.INFO,
            "Setting User Boolean '{0}' to '{1}'.",
            idStr,
            finalBool
        );
        variableDict[idStr] = finalBool;

        if (Timer.yieldNow) {yield return null;}
    }

    /// <summary>
    /// Reads a variable string formed by a Residue and adds it to variableDict.
    /// <para>Attributes: </para> <br />
    /// <para>source:    (optional) Geometry to expand variables from (defaults to rootGeometry).</para> <br />
    /// <para>id:        (required) Key for the variable.</para> <br />
    /// <para>residueID: (required) ID of the Residue to create variable.</para> <br />
    /// <para>type:      (required) Type of residue. Currently only 'mutant' is valid.</para> <br />
    /// <para>Elements: </para> <br />
    /// <para>target: (required if type="mutant") 3-Letter Amino Acid mutation target. </para> <br />
    /// </summary>
    /// <param name="varX">XElement containing variable.</param>
    IEnumerator ParseResidueVariable(XElement varX) {

        Geometry source = GetGeometry(varX, "source", true);

        string idStr = ParseXMLAttrString(varX, "id", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            Fail(varX, "Cannot parse Residue Variable - Empty Variable ID!");
            yield break;
        }

        //Get the Residue
        XElement residueIDX;
        ResidueID residueID;
        if ((residueIDX = varX.Element("residueID")) != null) {
            try {
                residueID = ResidueID.FromString(ExpandVariables(residueIDX.Value));
            } catch (System.Exception e) {
                Fail(varX, "Cannot parse Residue Variable - Failed to parse Residue ID: {0}", e.Message);
                yield break;
            }
        } else {
            Fail(varX, "Cannot parse Residue Variable - Residue ID element missing!");
            yield break;
        }

        Residue residue;
        if (! source.TryGetResidue(residueID, out residue)) {
            Fail(varX, "Cannot parse Residue Variable - Couldn't find Residue '{0}' in '{1}'!", residueID, source.name);
            yield break;
        }

        string typeString = ParseXMLAttrString(varX, "type", "", source).ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            Fail(varX, "Cannot parse Residue Variable - Type attribute missing!");
            yield break;
        }

        switch (typeString) {
            case "mutant":
                string targetStr = ParseXMLString(varX, "target", "", source).ToUpper();
                if (string.IsNullOrWhiteSpace(targetStr)) {
                    Fail(varX, "Cannot parse Residue Variable - Empty Target Variable ID!");
                    yield break;
                }

                string mutationCode;
                try {
                    mutationCode  = GetMutationCode(residueID, residue, targetStr);
                } catch (System.Exception e) {
                    Fail(varX, "Cannot parse Residue Variable - {0}", e.Message);
                    yield break;
                }
                if (failed) {yield break;}

                CustomLogger.LogFormat(
                    EL.INFO,
                    "Setting Residue Variable '{0}' to '{1}'.",
                    idStr,
                    mutationCode
                );

                variableDict[idStr] = mutationCode;
                break;
            default:
                Fail(varX, "Cannot parse Residue Variable - Unrecognised Type '{0}'!", typeString);
                yield break;
        }
    } 
    
    /// <summary>
    /// Parse variables from an Attribute of an XElement.
    /// </summary>
    /// <param name="xElement">XElement containing attribute</param>
    /// <param name="name">Name of attribute</param>
    /// <param name="defaultValue">Return Value if attribute doesn't exist</param>
    /// <param name="geometry">Geometry to expand variables with</param>
    string ParseXMLAttrString(
        XElement xElement, 
        string name, 
        string defaultValue="", 
        Geometry geometry=null
    ) {
        return ExpandVariables(FileIO.ParseXMLAttrString(xElement, name, defaultValue), geometry);
    }

    /// <summary>
    /// Parse variables from a child of an XElement.
    /// </summary>
    /// <param name="xElement">XElement containing child XElement..</param>
    /// <param name="name">Name of child XElement</param>
    /// <param name="defaultValue">Return Value if child XElement doesn't exist</param>
    /// <param name="geometry">Geometry to expand variables with</param>
    string ParseXMLString(
        XElement xElement, 
        string name, 
        string defaultValue="", 
        Geometry geometry=null
    ) {
        return ExpandVariables(FileIO.ParseXMLString(xElement, name, defaultValue), geometry);
    }


    ////////////////////////
    // Variable Expansion //
    ////////////////////////

    string ExpandVariables(string input, Geometry geometry=null) {
        // Variables look like: $(VAR)

        string output = "";
        bool expectOpen  = false;
        bool parseVar = false;
        bool parseLen = false;
        int charNum = -1;
        string varStr = "";

        //Loop through string
        foreach (char chr in input) {
            charNum++;

            if (chr == '$') {
                //Start of variable token
                expectOpen = true;
            } else if (expectOpen) {
                //Open parenthesis
                if (chr == '(') {
                    varStr = "";
                    parseVar = true;
                    expectOpen = false;
                } else if (chr == '{') {
                    varStr = "";
                    parseLen = true;
                    expectOpen = false;
                } else {
                    //No open parenthesis
                    Fail(null, "Expected '(' or '{' after '$' in input (char {0}): {1}", charNum.ToString(), input);
                    return "";
                }
            } else if (parseVar) {
                //Reading a variable
                if (chr == ')') {
                    //End of reading - convert to variable
                    output += ExpandVariable(varStr, geometry);
                    parseVar = false;
                } else {
                    varStr += chr;
                }
            } else if (parseLen) {
                if (chr == '}') {
                    //End of reading - get array

                    List<string> array;
                    if (!arrayDict.TryGetValue(varStr.ToLower(), out array)) {
                        Fail(null, "Cannot get Array Length - Array '{0}' not found!", varStr);
                        return "";
                    }

                    output += array.Count.ToString();
                    
                    parseLen = false;
                } else {
                    varStr += chr;
                }
            } else {
                output += chr;
            }
        }
        if (parseVar) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Expected ')' after '$(' in input (char {0}): {1}",
                charNum.ToString(),
                input
            );
            failed = true;
            return "";
        }
        return output;
    }

    static TEnum ExpandEnum<TEnum>(string input) where TEnum : struct {
        TEnum output = (TEnum)System.Enum.GetValues(typeof(TEnum)).GetValue(0);
        bool expectOpen  = false;
        bool parseVar = false;
        int charNum = -1;
        string varStr = "";

        //Loop through string
        foreach (char chr in input) {
            charNum++;

            if (chr == '$') {
                //Start of variable token
                expectOpen = true;
            } else if (expectOpen) {
                //Open parenthesis
                if (chr == '(') {
                    varStr = "";
                    parseVar = true;
                    expectOpen = false;
                } else {
                    //No open parenthesis
                    CustomLogger.LogFormat(
                        EL.WARNING,
                        "Expected '(' after '$' in input (char {0}): {1} - using '{2}' instead",
                        charNum.ToString(),
                        input,
                        output
                    );
                    break;
                }
            } else if (parseVar) {
                //Reading a variable
                if (chr == ')') {
                    //End of reading - convert to variable
                    if (!System.Enum.TryParse(varStr, true, out output)) {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "Could not expand special variable '{0}' - using '{1}' instead",
                            varStr,
                            output.ToString()
                        );
                    }
                    break;
                } else {
                    varStr += chr;
                }
            }
        }
        return output;
        
    }

    static string ExpandVariable(string input, Geometry geometry=null) {
        string output;
        if (variableDict.TryGetValue(input.ToLower(), out output)) {
            return output;
        }
        switch (input) {
            case "PROJECT":
                return Settings.projectPath.EndsWith("/") ? Settings.projectPath : Settings.projectPath + "/";
            case "CHARGES_DIR":
                return Settings.chargesDirectory.EndsWith("/") ? Settings.chargesDirectory : Settings.projectPath + "/";
            case "SETTINGS_PATH":
                return Settings.settingsPath.EndsWith("/") ? Settings.settingsPath : Settings.projectPath + "/";
            case "DATA_PATH":
                return Settings.dataPath.EndsWith("/") ? Settings.dataPath : Settings.projectPath + "/";
            case "GEOMETRY":
                return "$(GEOMETRY)";
            case "CHAINS":
                if (geometry == null) {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Cannot use reserved variable {0} without a Geometry.",
                        input
                    );
                    return "";
                } else {
                    return string.Join("-", geometry.GetChainIDs());
                }
            case "RESIDUES":
                if (geometry == null) {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Cannot use reserved variable {0} without a Geometry.",
                        input
                    );
                    failed = true;
                    return "";
                } else {
                    return string.Join("-", geometry.EnumerateResidueIDs().Select(x => x.ToString()));
                }
            default:
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Unrecognised variable {0}",
                    input
                );
                failed = true;
                return "";
        }
    }

    ////////////////////
    // Help Functions //
    ////////////////////

    string GetMutationCode(ResidueID residueID, Residue oldResidue, string target) {

        string oldCode;
        if (!Data.residueName3To1.TryGetValue(oldResidue.residueName, out oldCode)) {
            throw new System.Exception(string.Format(
                "Failed to convert Old Residue Name '{0}' in Residue '{1}'",
                oldResidue.residueName,
                residueID
            ));
        }

        string targetCode;
        if (!Data.residueName3To1.TryGetValue(target, out targetCode)) {
            throw new System.Exception(string.Format(
                "Failed to convert New Residue Name '{0}' in Target Residue",
                target
            ));
        }

        return oldCode + residueID.residueNumber.ToString() + targetCode;

    }

    IEnumerable<ResidueID> GetResidueIDsFromString(string residueIDsStr, Geometry source, bool allowEmpty=true) {
        if (string.IsNullOrEmpty(residueIDsStr)) {
            if (allowEmpty) {
                foreach (ResidueID residueID in source.EnumerateResidueIDs()) {
                    yield return residueID;
                }
            } else {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "'residueIDs' Attribute missing"
                );
                failed = true;
                yield break;
            }
        } else {
            foreach (string residueString in residueIDsStr.Split(new[] {','}, System.StringSplitOptions.RemoveEmptyEntries)) {
                string residueIDStr = ExpandVariables(residueString, source);
                ResidueID residueID;
                try {
                    residueID = ResidueID.FromString(residueIDStr);
                } catch (System.Exception e) {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Failed to parse Residue ID '{0}': '{1}",
                        residueIDStr,
                        e.Message
                    );
                    failed = true;
                    yield break;
                }
                yield return residueID;
            }
        }
    }

    IEnumerable<(ResidueID, Residue)> GetResiduesFromString(string residueIDsStr, Geometry source, bool allowEmpty=true) {
        foreach (ResidueID residueID in GetResidueIDsFromString(residueIDsStr, source, allowEmpty)) {
            Residue residue;
            if (!source.TryGetResidue(residueID, out residue)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Couldn't find Residue ID '{0}' in Geometry '{1}'",
                    residueID,
                    source.name
                );
                failed = true;
                yield break;
            }
            yield return (residueID, residue);
        }
    }

    IEnumerable<AtomID> GetAtomIDsFromString(string atomIDsStr, Geometry source, bool allowEmpty=true) {
        if (string.IsNullOrEmpty(atomIDsStr)) {
            if (allowEmpty) {
                foreach (AtomID atomID in source.EnumerateAtomIDs()) {
                    yield return atomID;
                }
            } else {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "'atomIDs' Attribute missing"
                );
                failed = true;
                yield break;
            }
        } else {
            foreach (string atomStr in atomIDsStr.Split(new[] {','}, System.StringSplitOptions.RemoveEmptyEntries)) {
                string atomIDStr = ExpandVariables(atomStr, source);
                AtomID atomID;
                try {
                    atomID = AtomID.FromString(atomIDStr);
                } catch (System.Exception e) {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Failed to parse Atom ID '{0}': '{1}",
                        atomStr,
                        e.Message
                    );
                    failed = true;
                    yield break;
                }
                yield return atomID;
            }
        }
    }

    IEnumerable<(AtomID, Atom)> GetAtomsFromString(string atomIDsStr, Geometry source, bool allowEmpty=true) {
        foreach (AtomID atomID in GetAtomIDsFromString(atomIDsStr, source, allowEmpty)) {
            Atom atom;
            if (!source.TryGetAtom(atomID, out atom)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Couldn't find Atom ID '{0}' in Geometry '{1}'",
                    atomID,
                    source.name
                );
                failed = true;
                yield break;
            }
            yield return (atomID, atom);
        }
    }

    ////////////////////
    // Error Handling //
    ////////////////////

    string GetFilePosition(XElement xElement) {

        if (xElement == null ) {
            return "";
        }

        System.Xml.IXmlLineInfo lineInfo = xElement;
        int lineNumber = lineInfo == null ? 0 : lineInfo.LineNumber;
        int charNum = lineInfo == null ? 0 : lineInfo.LinePosition;

        return string.Format(
            "[{0}:{1},{2}]",
            xElement.BaseUri,
            lineNumber,
            charNum
        );
    }

    public void Fail(XElement xElement, string format, params object[] args) {
        string message = string.Format(format, args);
        Fail(xElement, message);
    }

    public void Fail(XElement xElement, string message) {
        failed = true;

        CustomLogger.LogFormat(
            EL.ERROR,
            "{0} {1}",
            message,
            GetFilePosition(xElement)
        );
    }

    public void Warn(XElement xElement, string format, params object[] args) {
        string message = string.Format(format, args);
        Warn(xElement, message);
    }

    public void Warn(XElement xElement, string message) {
        CustomLogger.LogFormat(
            EL.WARNING,
            "{0} {1}",
            message,
            GetFilePosition(xElement)
        );
    }

}