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

public static class GaussianRecipe {

    static Dictionary<string, Geometry> geometryDict;
    static Dictionary<string, string> variableDict;
    static Dictionary<string, bool> boolDict;
    static Dictionary<ResidueID, string> mutationNames;
    static bool failed;

    public static IEnumerator RunGaussianRecipe(GIID geometryInterfaceID) {
        NotificationBar.SetTaskProgress(TID.RUN_GAUSSIAN_RECIPE, 0f);

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        GIS status = geometryInterface.status;
        if (status != GIS.OK && status != GIS.COMPLETED && status != GIS.WARNING) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Geometry Interface '{0}' is not in an eligible state - Cannot proceed.",
                geometryInterfaceID
            );
            NotificationBar.ClearTask(TID.RUN_GAUSSIAN_RECIPE);
            yield break;
        }

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
			yield break;
		}

        
        yield return FromXML(geometryInterface, path);



        NotificationBar.ClearTask(TID.RUN_GAUSSIAN_RECIPE);
    }

    public static IEnumerator FromXML(
        GeometryInterface geometryInterface,
        string path
    ) {


        geometryDict = new Dictionary<string, Geometry>();
        variableDict = new Dictionary<string, string>();
        boolDict = new Dictionary<string, bool>();
        mutationNames = new Dictionary<ResidueID, string>();
        failed = false;

        if (!File.Exists(path)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "File '{0}' does not exist.",
                path
            );
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
            yield break;
        }

        XElement recipeX = xDocument.Element("recipe");
        if (recipeX == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Element 'recipe' not found in root of '{0}'.",
                path
            );
            yield break;
        }

        Geometry geometry = geometryDict["$(GEOMETRY)"] = geometryInterface.geometry;

        //Add variables
        yield return ParseVariables(recipeX);
        yield return ParseUserVariables(recipeX);
        yield return ParseUserBools(recipeX);

        foreach (XElement groupX in recipeX.Elements("group")) {
            string type = ParseXMLAttrString(groupX, "type", "", geometry).ToLower();
            string groupName = ParseXMLAttrString(groupX, "name", "", geometry);
            string source = ParseXMLAttrString(groupX, "source", "", geometry);

            switch (type) {
                case "connected":
                    yield return ParseConnectedGroup(groupX, source, groupName);
                    break;
                case "perResidue":
                    yield return ParseResidueGroup(groupX, source, groupName);
                    break;
                case "geometry":
                    break;
                default:
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Group type '{0}' not recognised.",
                        type
                    );
                    yield break;
            }

            if (failed) {
                yield break;
            }
        }
    }

    static IEnumerator ParseConnectedGroup(
        XElement groupX,
        string sourceName,
        string groupName
    ) {
        Geometry source = GetGeometry(sourceName);
        if (source == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Source geometry '{0}' not found!",
                sourceName
            );
            yield break;
        }
        foreach (List<ResidueID> residueGroup in source.GetGroupedResidues()) {
            Geometry groupGeometry = source.TakeResidues(residueGroup, null);
            geometryDict[groupName] = groupGeometry;
            
            yield return ProcessGroup(groupX, groupGeometry);
        }
    }

    static IEnumerator ParseResidueGroup(
        XElement groupX,
        string sourceName,
        string groupName
    ) {
        Geometry source = GetGeometry(sourceName);
        if (source == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Source geometry '{0}' not found!",
                sourceName
            );
            yield break;
        }
        foreach (ResidueID residueID in source.EnumerateResidueIDs()) {
            Geometry groupGeometry = source.TakeResidue(residueID, null);
            geometryDict[groupName] = groupGeometry;
            
            yield return ProcessGroup(groupX, groupGeometry);
        }
    }

    static IEnumerator ParseGeometry(
        XElement groupX,
        string sourceName,
        string groupName
    ) {
        Geometry source = GetGeometry(sourceName);
        if (source == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Source geometry '{0}' not found!",
                sourceName
            );
            yield break;
        }
        Geometry geometryGroup = source.Take(null);
        yield return ProcessGroup(groupX, geometryGroup);
    }

    static IEnumerator ProcessGroup(XElement groupX, Geometry groupGeometry) {
        foreach (XElement elementX in groupX.Elements()) {
            switch (elementX.Name.ToString().ToLower()) {
                case "action":
                    yield return RunAction(elementX, groupGeometry);
                    break;
                case "run":
                    yield return RunCalculation(elementX, groupGeometry);
                    break;
            }
        }
        yield return null;
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

    static IEnumerator RunAction(XElement actionX, Geometry groupGeometry) {
        string actionID = ParseXMLAttrString(actionX, "id", "", groupGeometry).ToLower();
        switch (actionID) {
            case "movetolayer":
                MoveToLayer(actionX, groupGeometry);
                break;
            case "estimatechargemultiplicity":
                bool confirm = false;
                XAttribute confirmX = actionX.Attribute("confirm");
                if (confirmX != null && confirmX.Value == "true") {
                    confirm = true;
                }
                yield return groupGeometry.gaussianCalculator.EstimateChargeMultiplicity(confirm);
                break;
            case "generateatommap":
                groupGeometry.GenerateAtomMap();
                break;
            case "computeconnectivity":
                yield return Cleaner.CalculateConnectivity(groupGeometry);
                break;
            case "redistributecharge":
                yield return RedistributeCharge(actionX, groupGeometry);
                break;
            case "roundcharge":
                yield return PartialChargeCalculator.RoundCharge(groupGeometry);
                break;
            case "mutateresidue":
                yield return MutateResidue(actionX, groupGeometry);
                break;
        }
    }

    static IEnumerator RunCalculation(XElement calculationX, Geometry groupGeometry) {

        string sourceName = ParseXMLAttrString(calculationX, "source", "");
        Geometry source;
        if (string.IsNullOrWhiteSpace(sourceName)) {
            source = groupGeometry;
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
                    EL.WARNING,
                    "Ignoring invalid 'nproc' attribute: {0}",
                    nProcX.Value
                );
            }
        }

        XElement memX;
        if ((memX = calculationX.Element("mem")) != null) {
            int mem;
            if (int.TryParse(ExpandVariables(memX.Value), out mem)) {
                gc.jobMemoryMB = mem;
            } else {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Ignoring invalid 'mem' attribute: {0}",
                    memX.Value
                );
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

        int electrons = groupGeometry.EnumerateAtomIDs().Select(x => x.pdbID.atomicNumber).Sum();
        
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
                foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
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
                        EL.WARNING,
                        "Ignoring Residue ID '{0}' - cannot parse to Residue ID.",
                        residueIDStr
                    );
                    continue;
                }

                Residue residue;
                if (!geometry.TryGetResidue(residueID, out residue)) {
                    CustomLogger.LogFormat(
                        EL.WARNING,
                        "Ignoring Residue ID '{0}' - not present in Geometry.",
                        residueID
                    );
                    continue;
                }

                foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
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
                yield break;
            }
        } else {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Residue ID variable missing in MutateResidue in Group '{0}'",
                sourceGeometry.name
            );
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
            yield break;
        }

        Data.SetResidueProperties(oldResidue);

        //Get the Residue to mutate to
        XAttribute targetX;
        Residue newResidue;
        if ((targetX = actionX.Attribute("target")) != null) {
            try {
                newResidue = Residue.FromString(ExpandVariable(targetX.Value), oldResidue.state);
            } catch (System.Exception e) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Failed to parse Target Residue in MutateResidue in Group '{0}': {1}",
                    sourceGeometry.name,
                    e.Message
                );
                yield break;
            }
        } else {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Target Residue variable missing in MutateResidue in Group '{0}'",
                sourceGeometry.name
            );
            yield break;
        }

        newResidue.residueID = oldResidue.residueID;

        bool optimise = false;
        XAttribute optimiseX = actionX.Attribute("optimise");
        if (optimiseX != null && optimiseX.Value == "true") {
            optimise = true;
        }

        

        ResidueMutator residueMutator = sourceGeometry.GetComponent<ResidueMutator>();

        if (residueMutator == null) {
            //Create a new Dihedral scanner on this geometry
            residueMutator = sourceGeometry.gameObject.AddComponent<ResidueMutator>();

            //Initialise ResidueMutator
            residueMutator.Initialise(sourceGeometry, residueID);

            if (residueMutator.failed) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Failed to create Dihedral Scanner"
                );
                failed = true;
                yield break;
            }
        }

        yield return residueMutator.MutateStandard(
            newResidue,
            10f,
            (optimise) 
                ? ResidueMutator.OptisationMethod.TREE 
                : ResidueMutator.OptisationMethod.NONE
        );
    }

    static void Set1LetterMutationName(XElement nameX, Geometry sourceGeometry) {

        //Get the Residue to mutate
        XElement residueIDX;
        ResidueID residueID;
        if ((residueIDX = nameX.Element("residueID")) != null) {
            try {
                residueID = ResidueID.FromString(ExpandVariables(residueIDX.Value));
            } catch (System.Exception e) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Failed to parse Residue ID in Set1LetterMutationName in Group '{0}': {1}",
                    sourceGeometry.name,
                    e.Message
                );
                failed = true;
                return;
            }
        } else {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Residue ID variable missing in Set1LetterMutationName in Group '{0}'",
                sourceGeometry.name
            );
            failed = true;
            return;
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
            return;
        }

        //Get the Residue to mutate to
        XAttribute targetX;
        Residue newResidue;
        if ((targetX = nameX.Attribute("target")) != null) {
            try {
                newResidue = Residue.FromString(ExpandVariable(targetX.Value));
            } catch (System.Exception e) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Failed to parse Target Residue in MutateResidue in Group '{0}': {1}",
                    sourceGeometry.name,
                    e.Message
                );
                failed = true;
                return;
            }
        } else {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Target Residue variable missing in MutateResidue in Group '{0}'",
                sourceGeometry.name
            );
            failed = true;
            return;
        }

        string oldCode;
        if (!Data.residueName3To1.TryGetValue(oldResidue.residueName, out oldCode)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to convert Old Residue Name '{0}' in Residue '{1}'",
                oldResidue.residueName,
                residueID
            );
            failed = true;
            return;
        }

        string targetCode;
        if (!Data.residueName3To1.TryGetValue(newResidue.residueName, out targetCode)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to convert New Residue Name '{0}' in Target Residue",
                oldResidue.residueName
            );
            failed = true;
            return;
        }

        mutationNames[residueID] = oldCode + residueID.residueNumber.ToString() + targetCode;

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

    static string ParseXMLAttrString(
        XElement xElement, 
        string name, 
        string defaultValue="", 
        Geometry geometry=null
    ) {
        return ExpandVariables(FileIO.ParseXMLAttrString(xElement, name, defaultValue), geometry);
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

    static IEnumerator ParseVariables(XElement recipeX) {

        foreach (XElement varX in recipeX.Elements("var")) {
            string idStr = FileIO.ParseXMLAttrString(varX, "id", "").ToLower();
            if (string.IsNullOrWhiteSpace(idStr)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Empty Variable ID is being ignored."
                );
                continue;
            }
            string valueStr = varX.Value;
            if (string.IsNullOrWhiteSpace(valueStr)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Empty Variable value is being ignored."
                );
                continue;
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
    }

    static IEnumerator ParseUserVariables(XElement recipeX) {

        foreach (XElement varX in recipeX.Elements("userVar")) {
            string idStr = FileIO.ParseXMLAttrString(varX, "id", "").ToLower();
            if (string.IsNullOrWhiteSpace(idStr)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Empty Variable ID is being ignored."
                );
                continue;
            }
            string initValue = varX.Value;
            
            string titleStr = FileIO.ParseXMLAttrString(varX, "title", "");
            if (string.IsNullOrWhiteSpace(titleStr)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Empty title attribute is being ignored for userVar '{0}'.",
                    idStr
                );
                continue;
            }
            
            string promptStr = FileIO.ParseXMLAttrString(varX, "prompt", "");
            if (string.IsNullOrWhiteSpace(promptStr)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Empty prompt attribute is being ignored for userVar '{0}'.",
                    idStr
                );
                continue;
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
    }

    static IEnumerator ParseUserBools(XElement recipeX) {

        foreach (XElement varX in recipeX.Elements("userBool")) {
            string idStr = FileIO.ParseXMLAttrString(varX, "id", "").ToLower();
            if (string.IsNullOrWhiteSpace(idStr)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Empty Variable ID is being ignored."
                );
                continue;
            }
            string initValue = varX.Value;
            
            string titleStr = FileIO.ParseXMLAttrString(varX, "title", "").ToLower();
            if (string.IsNullOrWhiteSpace(titleStr)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Empty title attribute is being ignored for userVar '{0}'.",
                    idStr
                );
                continue;
            }
            
            string promptStr = FileIO.ParseXMLAttrString(varX, "prompt", "").ToLower();
            if (string.IsNullOrWhiteSpace(promptStr)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Empty prompt attribute is being ignored for userVar '{0}'.",
                    idStr
                );
                continue;
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

            bool finalBool = (userBool && ! multiPrompt.cancelled);



            CustomLogger.LogFormat(
                EL.INFO,
                "Adding User Boolean '{0}' with value '{1}'.",
                idStr,
                finalBool
            );
            boolDict[idStr] = finalBool;

            if (Timer.yieldNow) {yield return null;}
        }
    }
}