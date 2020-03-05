using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Xml.Linq;
using System.Linq;
using EL = Constants.ErrorLevel;
using RS = Constants.ResidueState;
using OLID = Constants.OniomLayerID;
using GCT = Constants.GaussianConvergenceThreshold;
using GIID = Constants.GeometryInterfaceID;
using TID = Constants.TaskID;
using GIS = Constants.GeometryInterfaceStatus;


public class Macro : MacroGroup {

    public static IEnumerator RunMacro(GIID geometryInterfaceID) {
		Macro macro = Macro.FromGeometryInterface(geometryInterfaceID);
		yield return macro.LoadRootFromXML();
		yield return macro.ProcessRoot();
    }

    public Macro(Geometry geometry, XElement xElement, bool root) : base (geometry, xElement, root) {
        sourceGeometry = geometry;
        rootX = xElement;
        isRoot = root;
    }

    public static Macro FromGeometryInterface(GIID geometryInterfaceID) {

        Macro macro = new Macro(null, null, false);

        geometryDict = new Dictionary<string, Geometry>();
        variableDict = new Dictionary<string, string>();
        arrayDict = new Dictionary<string, List<string>>();
        failed = false;

        if (!macro.LoadSourceFromGIID(geometryInterfaceID)) {
            throw new System.Exception("Failed to load from Geometry Interface!");
        }

        geometryDict["$(GEOMETRY)"] = macro.sourceGeometry;

        return macro;
    }


    bool LoadSourceFromGIID(GIID geometryInterfaceID) {

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        GIS status = geometryInterface.status;
        if (status != GIS.OK && status != GIS.COMPLETED && status != GIS.WARNING) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Geometry Interface '{0}' is not in an eligible state - Cannot proceed.",
                geometryInterfaceID
            );
            return false;
        }

        if (geometryInterface.geometry == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Geometry Interface '{0}' has no Geometry - Cannot proceed.",
                geometryInterfaceID
            );
            return false;
        }

        sourceGeometry = geometryInterface.geometry;
        return true;
    }

}

public class MacroGroup {

    public static Dictionary<string, Geometry> geometryDict;
    public static Dictionary<string, string> variableDict;
    public static Dictionary<string, List<string>> arrayDict;
    public static bool failed;


    public bool isRoot;
    public XElement rootX;
    public Geometry sourceGeometry;

    public MacroGroup(Geometry geometry, XElement xElement, bool root) {
        sourceGeometry = geometry;
        rootX = xElement;
        isRoot = root;
    }

    public IEnumerator LoadRootFromXML() {
        
        FileSelector loadPrompt = FileSelector.main;

		//Set FileSelector to Load mode
		yield return loadPrompt.Initialise(saveMode:false, new List<string>{"xml"});
		//Wait for user response
		while (!loadPrompt.userResponded) {
			yield return null;
		}

		if (loadPrompt.cancelled) {
			GameObject.Destroy(loadPrompt.gameObject);
            NotificationBar.ClearTask(TID.RUN_GAUSSIAN_RECIPE);
            failed = true;
			yield break;
		}

		//Got a non-cancelled response from the user
		string path = loadPrompt.confirmedText;

		//Close the FileSelector
		GameObject.Destroy(loadPrompt.gameObject);

		//Check the file exists
		if (!File.Exists(path)) {
			CustomLogger.LogFormat(EL.ERROR, "File does not exist: {0}", path);
			GameObject.Destroy(loadPrompt.gameObject);
            NotificationBar.ClearTask(TID.RUN_GAUSSIAN_RECIPE);
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

        rootX = xDocument.Element("recipe");
        if (rootX == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Element 'recipe' not found in root of '{0}'.",
                path
            );
            failed = true;
            yield break;
        }

    }

    public IEnumerator ProcessRoot() {
        yield return ProcessElement(rootX);
    }

    public IEnumerator ProcessElement(XElement parentX) {

        foreach (XElement elementX in parentX.Elements()) {
            string elementName = elementX.Name.ToString().ToLower();
            switch (elementName) {
                case "var":
                    yield return ParseVariable(elementX);
                    break;
                case "uservar":
                    yield return ParseUserVariable(elementX);
                    break;
                case "userbool":
                    yield return ParseUserBool(elementX);
                    break;
                case "residuevar":
                    yield return ParseResidueVariable(elementX);
                    break;
                case "array":
                    yield return ParseArray(elementX);
                    break;
                case "action":
                    yield return RunAction(elementX);
                    break;
                case "run":
                    yield return RunCalculation(elementX);
                    break;
                case "group":
                    yield return CreateGroup(elementX);
                    break;
                case "foreach":
                    yield return ProcessArray(elementX);
                    break;
                default:
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Unrecognised Element '{0}'",
                        elementName
                    );
                    failed = true;
                    break;
            }
            if (failed) {yield break;}
        };
    }

    public IEnumerator ProcessArray(XElement arrayX) {

        string arrayID = FileIO.ParseXMLAttrString(arrayX, "array", "").ToLower();
        if (string.IsNullOrWhiteSpace(arrayID)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot process Array - Empty Array ID."
            );
            failed = true;
            yield break;
        }
        
        string varStr = FileIO.ParseXMLAttrString(arrayX, "var", "").ToLower();
        if (string.IsNullOrWhiteSpace(varStr)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot process Array - Empty Variable ID."
            );
            failed = true;
            yield break;
        }

        List<string> array;
        if (!arrayDict.TryGetValue(arrayID, out array)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot process Array - Array '{0}' not found!",
                arrayID
            );
            failed = true;
            yield break;
        }

        foreach (string variable in array) {
            variableDict[varStr] = variable;
            yield return ProcessElement(arrayX);
        }

    }

    static Geometry GetGeometry(string name) {
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

    IEnumerator CreateGroup(XElement groupX) {
        string type = ParseXMLAttrString(groupX, "type", "", sourceGeometry).ToLower();
        string groupName = ParseXMLAttrString(groupX, "name", "", sourceGeometry);
        string sourceName = ParseXMLAttrString(groupX, "source", "", sourceGeometry);

        Geometry source = GetGeometry(sourceName);
        if (source == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Source geometry '{0}' not found!",
                sourceName
            );
            failed = true;
            yield break;
        }

        IEnumerator ProcessGroup(Geometry groupGeometry, string name) {
            geometryDict[name] = groupGeometry;
            groupGeometry.name = name;
            MacroGroup groupMacro = new MacroGroup(groupGeometry, groupX, false);
            
            yield return groupMacro.ProcessRoot();
            if (failed) {yield break;}
            GameObject.Destroy(groupGeometry);

        }

        CustomLogger.LogFormat(
            EL.INFO,
            "Created new Group with type '{0}', name '{1}' and source '{2}'.",
            type,
            groupName,
            sourceName
        );

        Geometry group;
        switch (type) {
            case "connected":
                foreach (List<ResidueID> residueGroup in source.GetGroupedResidues()) {
                    group = source.TakeResidues(residueGroup, null);
                    yield return ProcessGroup(group, groupName);
                }
                break;
            case "perResidue":
                foreach (ResidueID residueID in source.EnumerateResidueIDs()) {
                    group = source.TakeResidue(residueID, null);
                    yield return ProcessGroup(group, groupName);
                }
                break;
            case "geometry":
                group = source.Take(null);
                yield return ProcessGroup(group, groupName);
                break;
            default:
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Group type '{0}' not recognised.",
                    type
                );
                yield break;
        }
    }

    IEnumerator RunAction(XElement actionX) {
        string actionID = ParseXMLAttrString(actionX, "id", "", sourceGeometry).ToLower();
        switch (actionID) {
            case "movetolayer":
                MoveToLayer(actionX, sourceGeometry);
                break;
            case "estimatechargemultiplicity":
                bool confirm = false;
                XAttribute confirmX = actionX.Attribute("confirm");
                if (confirmX != null && confirmX.Value == "true") {
                    confirm = true;
                }
                yield return sourceGeometry.gaussianCalculator.EstimateChargeMultiplicity(confirm);
                break;
            case "generateatommap":
                sourceGeometry.GenerateAtomMap();
                break;
            case "computeconnectivity":
                yield return Cleaner.CalculateConnectivity(sourceGeometry);
                break;
            case "redistributecharge":
                yield return RedistributeCharge(actionX, sourceGeometry);
                break;
            case "roundcharge":
                yield return PartialChargeCalculator.RoundCharge(sourceGeometry);
                break;
            case "save":
                yield return Save(actionX);
                break;
            case "mutateresidue":
                yield return MutateResidue(actionX, sourceGeometry);
                break;
        }
    }

    IEnumerator RunCalculation(XElement calculationX) {

        string sourceName = ParseXMLAttrString(calculationX, "source", "");
        Geometry source;
        if (string.IsNullOrWhiteSpace(sourceName)) {
            source = sourceGeometry;
        } else {
            source = GetGeometry(sourceName);
        }
        if (source == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "No source Geometry to run calculation on!"
            );
            failed = true;
            yield break;
        }

        CustomLogger.LogFormat(
            EL.INFO,
            "Get base path"
        );
        string basePath = ParseXMLAttrString(calculationX, "path", "", source);
        basePath = ExpandVariables(basePath, source);

        string atomMapSourceName = ParseXMLAttrString(calculationX, "atomMap", "", source);
        if (atomMapSourceName != "") {
            Geometry atomMapSource = GetGeometry(atomMapSourceName);
            if (atomMapSource != null) {
                source.atomMap = atomMapSource.atomMap;
            }
        }

        GaussianCalculator gc = source.gaussianCalculator;

        XElement nProcX;
        if ((nProcX = calculationX.Element("nproc")) != null) {
            int nProc;
            if (int.TryParse(ExpandVariables(nProcX.Value), out nProc)) {
                gc.numProcessors = nProc;
            } else {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Invalid 'nproc' attribute: {0}",
                    nProcX.Value
                );
                failed = true;
                yield break;
            }
        }

        XElement memX;
        if ((memX = calculationX.Element("mem")) != null) {
            int mem;
            if (int.TryParse(ExpandVariables(memX.Value), out mem)) {
                gc.jobMemoryMB = mem;
            } else {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Invalid 'mem' attribute: {0}",
                    memX.Value
                );
                failed = true;
                yield break;
            }
        }

        

        XElement optX;
        if ((optX = calculationX.Element("opt")) != null) {
            string thresholdStr = FileIO.ParseXMLAttrString(optX, "threshold", "");
            gc.convergenceThreshold = ExpandEnum<GCT>(thresholdStr);
            gc.doOptimisation = true;
        } else {
            gc.doOptimisation = false;
        }

        XElement titleX = calculationX.Element("title");
        if (titleX == null || string.IsNullOrWhiteSpace(titleX.Value)) {
            CustomLogger.LogFormat(
                EL.WARNING,
                "Calculation does not have a 'title' element.",
                nProcX.Value
            );
            gc.title = "Title";
        } else {
            gc.title = ExpandVariables(titleX.Value);
        }

        foreach (XElement layerX in calculationX.Elements("layer")) {

            //Parse Layer ID
            string oniomLayerIDStr = FileIO.ParseXMLAttrString(layerX, "id", "");
            if (string.IsNullOrWhiteSpace(oniomLayerIDStr)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "No 'id' tag in 'layer' element!"
                );
                failed = true;
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

        int electrons = sourceGeometry.EnumerateAtomIDs().Select(x => x.pdbID.atomicNumber).Sum();
        
        gaussian.WriteInputAndExecute(
            source,
            TID.RUN_GAUSSIAN_RECIPE,
            true,
            false,
            true,
            (float)(CustomMathematics.IntPow(electrons, 4) / 1000000000),
            basePath + ".gjf",
            basePath + ".log"
        );

        //Update Positions
        XElement updatePositionsX = calculationX.Element("updatePositions");
        if (updatePositionsX != null) {
            yield return UpdateGeometryFromOutput(source, updatePositionsX, basePath, updatePositions:true);
        }

        //Update Charges
        XElement updateChargesX = calculationX.Element("updateCharges");
        if (updateChargesX != null) {
            yield return UpdateGeometryFromOutput(source, updateChargesX, basePath, updateCharges:true);
        }

        //Update Positions
        XElement updateAmbersX = calculationX.Element("updateAmbers");
        if (updateAmbersX != null) {
            yield return UpdateGeometryFromOutput(source, updateAmbersX, basePath, updateAmbers:true);
        }

    }

    static IEnumerator UpdateGeometryFromOutput(
        Geometry source,
        XElement updateX, 
        string basePath, 
        bool updatePositions=false, 
        bool updateCharges=false, 
        bool updateAmbers=false
    ) {
        if (updateX != null) {
            string destinationStr = ParseXMLAttrString(updateX, "destination", "");

            if (string.IsNullOrWhiteSpace(destinationStr)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "'destination' flag missing in 'update' element."
                );
                failed = true;
                yield break;
            } 

            Geometry destination;
            if (!geometryDict.TryGetValue(destinationStr, out destination)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "'destination' Geometry {0} not found.",
                    destinationStr
                );
                failed = true;
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
    }


    static void MoveToLayer(XElement layerMoveX, Geometry geometry) {

        OLID oniomLayerID = ExpandEnum<OLID>(layerMoveX.Value);

        XAttribute residueStateX = layerMoveX.Attribute("residueState");
        XAttribute residueIDsX = layerMoveX.Attribute("residueIDs");
        if (residueStateX != null) {
            RS residueState = ExpandEnum<RS>(residueStateX.Value);
            foreach ((ResidueID residueID, Residue residue) in geometry.EnumerateResidues(x => x.state == residueState)) {
                foreach (Atom atom in residue.atoms.Values) {
                    atom.oniomLayer = oniomLayerID;
                }
            }
            if (residueIDsX != null) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Ignoring 'residueIDs' parameter in MoveToLayer - 'residueState' already defined."
                );
            }
        } else if (residueIDsX != null) {

            List<ResidueID> residueIDs = residueIDsX
                .Value
                .Split(new[] {' '}, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(x => ResidueID.FromString(x))
                .ToList();

            foreach (string residueIDStr in residueIDsX.Value.Split(new[] {' '}, System.StringSplitOptions.RemoveEmptyEntries)) {
                ResidueID residueID;
                try {
                    residueID = ResidueID.FromString(residueIDStr);
                } catch {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Invalid Residue ID '{0}' in MoveToLayer - cannot parse to Residue ID.",
                        residueIDStr
                    );
                    failed = true;
                    return;
                }

                Residue residue;
                if (!geometry.TryGetResidue(residueID, out residue)) {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Residue ID '{0}' not present in Geometry in MoveToLayer",
                        residueID
                    );
                    failed = true;
                    return;
                }

                foreach (Atom atom in residue.atoms.Values) {
                    atom.oniomLayer = oniomLayerID;
                }
            }

            if (residueIDsX != null) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Ignoring 'residueIDs' parameter in MoveToLayer - 'residueState' already defined."
                );
            }
        } else {
            foreach (Atom atom in geometry.EnumerateAtoms()) {
                atom.oniomLayer = oniomLayerID;
            }
        }
    }

    static IEnumerator RedistributeCharge(XElement actionX, Geometry groupGeometry) {
        List<float> distribution;
        try {
            distribution = GetChargeDistribution(actionX);
        } catch {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't parse charge distribution."
            );
            failed = true;
            yield break;
        }
        yield return PartialChargeCalculator.RedistributeCharge(groupGeometry, distribution);
    }
    
    static IEnumerator MutateResidue(XElement actionX, Geometry sourceGeometry) {

        //Get the Residue to mutate
        XElement residueIDX;
        ResidueID residueID;
        if ((residueIDX = actionX.Element("residueID")) != null) {
            try {
                residueID = ResidueID.FromString(ExpandVariables(residueIDX.Value));
            } catch (System.Exception e) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Failed to parse Residue ID in MutateResidue in Group '{0}': {1}",
                    sourceGeometry.name,
                    e.Message
                );
                failed = true;
                yield break;
            }
        } else {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Residue ID variable missing in MutateResidue in Group '{0}'",
                sourceGeometry.name
            );
            failed = true;
            yield break;
        }

        Residue oldResidue;
        if (! sourceGeometry.TryGetResidue(residueID, out oldResidue)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't find Residue ID '{0}' in Group '{1}'",
                residueID,
                sourceGeometry.name
            );
            failed = true;
            yield break;
        }
        string oldResidueName = oldResidue.residueName;

        //Get the Residue to mutate to
        Residue newResidue;
        string targetStr = ParseXMLString(actionX, "target", "", sourceGeometry).ToUpper();
        if (string.IsNullOrWhiteSpace(targetStr)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Empty Target Variable ID in MutateResidue!"
            );
            failed = true;
            yield break;
        }

        try {
            newResidue = Residue.FromString(targetStr);
        } catch (System.Exception e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to parse Target Residue in MutateResidue in Group '{0}': {1}",
                sourceGeometry.name,
                e.Message
            );
            failed = true;
            yield break;
        }

        bool optimise = (ParseXMLString(actionX, "optimise", "false", sourceGeometry).ToLower() == "true");

        ResidueMutator residueMutator;
        try {
            residueMutator = new ResidueMutator(sourceGeometry, residueID);
        } catch (System.Exception e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to Mutate Residue in Group '{0}': {1}",
                sourceGeometry.name,
                e.Message
            );
            failed = true;
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
            sourceGeometry.name,
            residueMutator.clashScore,
            oldResidueName,
            targetStr
            
        );
    }

    IEnumerator Save(XElement saveX) {

        string sourceName = ParseXMLAttrString(saveX, "source", "", sourceGeometry);

        Geometry source;
        if (string.IsNullOrEmpty(sourceName)) {
            source = sourceGeometry.Take(null);
        } else {
            source = GetGeometry(sourceName).Take(null);
            if (source == null) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Source geometry '{0}' not found!",
                    sourceName
                );
                failed = true;
                yield break;
            }
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
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot save with empty Name!"
            );
            failed = true;
            yield break;
        }

        string path = Path.Combine(directory, nameStr);

        if (!Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        FileWriter fileWriter;
        try {
            fileWriter = new FileWriter(
                source, 
                path, 
                saveX.Element("connectivity") != null
            );
        } catch (System.Exception e) {
            
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to save Geometry to '{0}': {1}",
                path,
                e.Message
            );
            failed = true;
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

    static List<float> GetChargeDistribution(XElement actionX) {
        List<float> distribution = new List<float>();
        XElement distributionX = actionX.Element("distribution");
        if (distributionX != null) {
            string distributionString = distributionX.Value;
            foreach (string depthString in distributionString.Split(new[] {' '}, System.StringSplitOptions.RemoveEmptyEntries)) {
                distribution.Add(float.Parse(depthString));
            }
        }
        return distribution;
    }

    public IEnumerator ParseVariable(XElement varX) {
        string idStr = FileIO.ParseXMLAttrString(varX, "id", "").ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot parse Variable - Empty Variable ID."
            );
            failed = true;
            yield break;
        }
        string valueStr = varX.Value;
        if (string.IsNullOrWhiteSpace(valueStr)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot parse Variable - Empty Value."
            );
            failed = true;
            yield break;
        }
        CustomLogger.LogFormat(
            EL.INFO,
            "Adding Variable '{0}' with value '{1}'.",
            idStr,
            valueStr
        );
        variableDict[idStr] = valueStr;

        if (Timer.yieldNow) {yield return null;}

    }

    public IEnumerator ParseArray(XElement arrayX) {
        string idStr = FileIO.ParseXMLAttrString(arrayX, "id", "").ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot parse Array - Empty Array ID."
            );
            failed = true;
            yield break;
        }

        List<string> stringArray = new List<string>();

        foreach (XElement elementX in arrayX.Elements()) {
            string elementName = elementX.Name.ToString().ToLower();
            switch (elementName) {
                case ("item"):

                    string valueStr = ExpandVariables(elementX.Value, sourceGeometry);
                    stringArray.Add(valueStr);
                    break;
                default:
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Cannot parse Array - unrecognised XElement '{0}'",
                        elementName
                    );
                    break;
            }
            if (Timer.yieldNow) {yield return null;}
        }

        CustomLogger.LogFormat(
            EL.INFO,
            "Adding Array '{0}' with ({1}) values: '{2}'",
            idStr,
            stringArray.Count,
            string.Join("', '", stringArray)
        );
        arrayDict[idStr] = stringArray;


    }

    static IEnumerator ParseUserVariable(XElement varX) {

        string idStr = FileIO.ParseXMLAttrString(varX, "id", "").ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Variable ID is empty in ParseUserVariable!"
            );
            failed = true;
            yield break;
        }
        string initValue = varX.Value;
        
        string titleStr = FileIO.ParseXMLAttrString(varX, "title", "");
        if (string.IsNullOrWhiteSpace(titleStr)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Empty Title attribute for userVar '{0}'.",
                idStr
            );
            failed = true;
            yield break;
        }
        
        string promptStr = FileIO.ParseXMLAttrString(varX, "prompt", "");
        if (string.IsNullOrWhiteSpace(promptStr)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Empty prompt attribute for userVar '{0}'.",
                idStr
            );
            failed = true;
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
            valueStr = initValue;
        } else {
            valueStr = multiPrompt.inputField.text;
        }


        CustomLogger.LogFormat(
            EL.INFO,
            "Adding User Variable '{0}' with value '{1}'.",
            idStr,
            valueStr
        );
        variableDict[idStr] = valueStr;

        if (Timer.yieldNow) {yield return null;}
    }

    static IEnumerator ParseUserBool(XElement varX) {

        string idStr = FileIO.ParseXMLAttrString(varX, "id", "").ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Empty Variable ID in ParseUserBool!"
            );
                failed = true;
                yield break;
        }
        string initValue = varX.Value;
        
        string titleStr = FileIO.ParseXMLAttrString(varX, "title", "");
        if (string.IsNullOrWhiteSpace(titleStr)) {
            CustomLogger.LogFormat(
                EL.WARNING,
                "Empty title attribute userBool '{0}'.",
                idStr
            );
        }
        
        string promptStr = FileIO.ParseXMLAttrString(varX, "prompt", "");
        if (string.IsNullOrWhiteSpace(promptStr)) {
            CustomLogger.LogFormat(
                EL.WARNING,
                "Empty prompt attribute for userVar '{0}'.",
                idStr
            );
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

        string finalBool = (userBool && ! multiPrompt.cancelled) ? "true" : "false";

        CustomLogger.LogFormat(
            EL.INFO,
            "Adding User Boolean '{0}' with value '{1}'.",
            idStr,
            finalBool
        );
        variableDict[idStr] = finalBool;

        if (Timer.yieldNow) {yield return null;}
    }

    IEnumerator ParseResidueVariable(XElement varX) {

        string idStr = FileIO.ParseXMLAttrString(varX, "id", "").ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Empty Variable ID in ParseResidueVariable!"
            );
            failed = true;
            yield break;
        }

        //Get the Residue
        XElement residueIDX;
        ResidueID residueID;
        if ((residueIDX = varX.Element("residueID")) != null) {
            try {
                residueID = ResidueID.FromString(ExpandVariables(residueIDX.Value));
            } catch (System.Exception e) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Failed to parse Residue ID in ParseResidueVariable in Group '{0}': {1}",
                    sourceGeometry.name,
                    e.Message
                );
                failed = true;
                yield break;
            }
        } else {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Residue ID variable missing in ParseResidueVariable in Group '{0}'",
                sourceGeometry.name
            );
            failed = true;
            yield break;
        }

        Residue residue;
        if (! sourceGeometry.TryGetResidue(residueID, out residue)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't find Residue ID '{0}' in Group '{1}'",
                residueID,
                sourceGeometry.name
            );
            failed = true;
            yield break;
        }

        string typeString = ParseXMLAttrString(varX, "type", "", sourceGeometry).ToLower();
        if (string.IsNullOrWhiteSpace(idStr)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Empty Type in ParseResidueVariable!"
            );
            failed = true;
            yield break;
        }

        switch (typeString) {
            case "mutant":
                string targetStr = ParseXMLString(varX, "target", "", sourceGeometry).ToUpper();
                if (string.IsNullOrWhiteSpace(targetStr)) {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Empty Target Variable ID in ParseResidueVariable!"
                    );
                    failed = true;
                    yield break;
                }

                string mutationCode = GetMutationCode(residueID, residue, targetStr);
                if (failed) {yield break;}

                CustomLogger.LogFormat(
                    EL.INFO,
                    "Adding Residue Variable '{0}' with value '{1}'.",
                    idStr,
                    mutationCode
                );

                variableDict[idStr] = mutationCode;
                break;
            default:
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Unrecognised Type '{0}' in ParseResidueVariable!",
                    typeString
                );
                failed = true;
                yield break;
        }
    } 

    static string GetMutationCode(ResidueID residueID, Residue oldResidue, string target) {

        string oldCode;
        if (!Data.residueName3To1.TryGetValue(oldResidue.residueName, out oldCode)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to convert Old Residue Name '{0}' in Residue '{1}'",
                oldResidue.residueName,
                residueID
            );
            failed = true;
            return "";
        }

        string targetCode;
        if (!Data.residueName3To1.TryGetValue(target, out targetCode)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to convert New Residue Name '{0}' in Target Residue",
                target
            );
            failed = true;
            return "";
        }

        return oldCode + residueID.residueNumber.ToString() + targetCode;

    }

    static string ParseXMLAttrString(
        XElement xElement, 
        string name, 
        string defaultValue="", 
        Geometry geometry=null
    ) {
        return ExpandVariables(FileIO.ParseXMLAttrString(xElement, name, defaultValue), geometry);
    }

    static string ParseXMLString(
        XElement xElement, 
        string name, 
        string defaultValue="", 
        Geometry geometry=null
    ) {
        return ExpandVariables(FileIO.ParseXMLString(xElement, name, defaultValue), geometry);
    }

    static string ExpandVariables(string input, Geometry geometry=null) {
        // Variables look like: $(VAR)

        string output = "";
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
                        EL.ERROR,
                        "Expected '(' after '$' in input (char {0}): {1}",
                        charNum.ToString(),
                        input
                    );
                    failed = true;
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

}