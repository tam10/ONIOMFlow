using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Element = Constants.Element;
using TID = Constants.TaskID;
using GIID = Constants.GeometryInterfaceID;
using GIS = Constants.GeometryInterfaceStatus;
using RS = Constants.ResidueState;
using EL = Constants.ErrorLevel;
using OLID = Constants.OniomLayerID;
using GCT = Constants.GaussianConvergenceThreshold;
using System.IO;
using System.Linq;
using System.Text;
using System;
using System.Diagnostics;

public static class PartialChargeCalculator {

    static Bash.ExternalCommand _gaussian;
    static Bash.ExternalCommand gaussian {
        get {
            if (_gaussian == null) {
                _gaussian = GaussianCalculator.GetGaussian();
            }
            return _gaussian;
        }
    }

    static IEnumerator SetTaskProgress(TID taskID, float progress){
        NotificationBar.SetTaskProgress(taskID, progress);
        yield return null;
    }
    
    static bool cancelled;

    public static IEnumerator GetPartialCharges(GIID startID, GIID targetID, List<TID> taskIDs) {

        Flow.GetGeometryInterface(targetID).activeTasks++;
        yield return Flow.CopyGeometry(startID, targetID);
        
        foreach (TID taskID in taskIDs) {
            yield return RunTask(targetID, taskID);
        }
        
        Flow.GetGeometryInterface(targetID).activeTasks--;
        Flow.GetGeometryInterface(startID).status = GIS.COMPLETED;
    }

    private static IEnumerator RunTask(GIID geometryInterfaceID, TID taskID) {
        ArrowFunctions.Task task = ArrowFunctions.GetTask(taskID);
        yield return task(geometryInterfaceID);
    }

    public static IEnumerator GetPartialChargesFromMol2(GIID geometryInterfaceID) {
        return GetPartialChargesRED(geometryInterfaceID, false, TID.GET_PARTIAL_CHARGES_FROM_MOL2);
    }
    
    public static IEnumerator CalculatePartialChargesRED(GIID geometryInterfaceID) {
        return GetPartialChargesRED(geometryInterfaceID, true, TID.CALCULATE_PARTIAL_CHARGES_RED);
    }
    
    public static IEnumerator CalculatePartialChargesGaussian(GIID geometryInterfaceID) {
        return GetPartialChargesGaussian(geometryInterfaceID, true, TID.CALCULATE_PARTIAL_CHARGES_GAUSSIAN);
    }

    static IEnumerator GetPartialChargesRED(GIID geometryInterfaceID, bool runRed, TID taskID) {

        // Based on code by Clyde Fare
        // https://github.com/Clyde-fare/cc_utils

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;

        //Modify R.E.D. program
        string redCommandFilename = Path.GetFileName(Settings.redCommandPath);
        string modifiedRedCommandFilename = "mod_" + redCommandFilename;
        string modifiedRedCommandPath = Path.Combine(Settings.projectPath, modifiedRedCommandFilename);
        
        if (runRed) {

            Bash.ExternalCommand perl = Settings.GetExternalCommand("perl");

            //Check Perl
            try {
                perl.CheckCommand();
            } catch (SystemException e) {
                CustomLogger.LogFormat(EL.ERROR, "perl not installed. Cannot run R.E.D.");
                CustomLogger.LogOutput("Traceback: {0}", e.StackTrace);
                geometryInterface.activeTasks--;
                yield break;
            }

            yield return SetTaskProgress(taskID, 0f);

            Dictionary<string, string> results = new Dictionary<string, string>();
            yield return InitialiseRed(results);

            yield return SetTaskProgress(taskID, 0.1f);
            
            try {
                gaussian.CheckCommand();
            } catch (SystemException e) {
                CustomLogger.LogFormat(EL.ERROR, "Gaussian not available - cannot run Partial Charges Calculation");
                CustomLogger.LogOutput("Traceback: {0}", e.StackTrace);
                geometryInterface.activeTasks--;
                yield break;
            }

            if (!CheckScratch()) {
                NotificationBar.ClearTask(taskID);
                geometryInterface.activeTasks--;
                yield break;
            }

            yield return ModifyRED(Settings.redCommandPath, modifiedRedCommandPath, results);

        }

        yield return SetTaskProgress(taskID, 0.2f);

        foreach (List<ResidueID> residueGroup in geometryInterface.geometry.GetGroupedResidues()) {
            ProcessResidueGroupRED(residueGroup, geometryInterface, runRed, modifiedRedCommandPath);
        }

        geometryInterface.activeTasks--;
        NotificationBar.ClearTask(taskID);
    }

    static IEnumerator GetPartialChargesGaussian(GIID geometryInterfaceID, bool internalRESP, TID taskID) {

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;
        yield return SetTaskProgress(taskID, 0f);
            
        try {
            gaussian.CheckCommand();
        } catch (SystemException e) {
            CustomLogger.LogFormat(EL.ERROR, "Gaussian not available - cannot run Partial Charges Calculation");
            CustomLogger.LogOutput("Traceback: {0}", e.StackTrace);
            geometryInterface.activeTasks--;
            yield break;
        }

        foreach (List<ResidueID> residueGroup in geometryInterface.geometry.GetGroupedResidues()) {
            yield return ProcessResidueGroupGaussian(residueGroup, geometryInterface, internalRESP, taskID);

            Geometry groupGeometry = geometryInterface.geometry.TakeResidues(residueGroup, null);
            yield return RedistributeCharge(groupGeometry, Settings.chargeDistributions);
            yield return geometryInterface.geometry.UpdateFrom(groupGeometry, updateCharges:true);

            if (groupGeometry != null) {
                GameObject.Destroy(groupGeometry.gameObject);
            }

        }


        geometryInterface.activeTasks--;
        NotificationBar.ClearTask(taskID);

    }




    static IEnumerator InitialiseRed(Dictionary<string, string> results) {

        //Get options for R.E.D. command line tool
        RedOptions redOptions = RedOptions.main;
        redOptions.Initialise();
        while (!redOptions.userResponded) {
            yield return null;
        }

        results = redOptions.GetResults();
    }

    static IEnumerator ModifyRED(string originalREDFile, string newREDFile, Dictionary<string, string> redOptions) {


        using (FileStream fileStream = File.OpenWrite(newREDFile)) {
            using (StreamWriter streamWriter = new StreamWriter(fileStream, Encoding.UTF8)) {

                bool mainProgram = false;
                foreach (string redLine in FileIO.EnumerateLines(originalREDFile)) {
                    string line = redLine;

                    //Only modify flags in MAIN PROGRAM
                    if (line.Contains("MAIN PROGRAM")) {mainProgram = true;}

                    if (mainProgram && line.StartsWith("$")) {
                        string key = line.Split(new char[] {' '}, System.StringSplitOptions.RemoveEmptyEntries)[0].Replace("$", "");
                        
                        string value;
                        if (redOptions.TryGetValue(key, out value)) {
                            string[] splitLine = line.Split(new char[] {';'}, 2, System.StringSplitOptions.RemoveEmptyEntries);
                            string keyString = splitLine[0].Split(new char[] {'"'}, System.StringSplitOptions.RemoveEmptyEntries)[0];
                            line = keyString + string.Format("\"{0}\";", value) + splitLine[1];
                        }
                    }

                    streamWriter.WriteLine(line);
                    if (Timer.yieldNow) {yield return null;}
                }
            }
        }

        Process process = Bash.StartBashProcess(string.Format("chmod 755 {0}", newREDFile));
        while (!process.HasExited) {
            yield return null;
        }
    }

    static bool CheckScratch() {
        string scratchDirectory = Environment.GetEnvironmentVariable("SCRDIR");
        if (scratchDirectory == "" || !Directory.Exists(scratchDirectory)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Scratch Directory ({0}) does not exist. Cannot run R.E.D.", 
                scratchDirectory
            );
            return false;
        }

        //Check if Scratch directory is empty - R.E.D. refuses to run if it's not empty
        bool scratchHasFiles = Directory.EnumerateFileSystemEntries(scratchDirectory)
            .Where(
                //Exclude hidden files in search
                x => ! new FileInfo(x).Attributes.HasFlag(FileAttributes.Hidden)
            ).Any();

        if (scratchHasFiles) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Scratch Directory ({0}) is not empty. Cannot run R.E.D.", 
                scratchDirectory
            );
            return false;
        }
        return true;
    }

    static IEnumerator ProcessResidueGroupRED(
        List<ResidueID> residueGroup, 
        GeometryInterface geometryInterface, 
        bool runRed,
        string modifiedRedCommandPath=""
    ) {
        
        string groupName = string.Join("', '", residueGroup);
                
        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Processing Residues: '{0}'.",
            groupName
        );
        Geometry groupGeometry = geometryInterface.geometry.TakeResidues(residueGroup, null);
        Geometry newNSR = PrefabManager.InstantiateGeometry(null);

        //R.E.D. wants to have atom names that look like:
        // N1 C1 C2 C3 H1 H2 etc
        Map<AtomID, int> p2nMap = GetP2NMap(groupGeometry, newNSR);

        string chargeFilePath;

        if (runRed) {

            RunRED(groupGeometry, newNSR, modifiedRedCommandPath);

            chargeFilePath = Settings.redChargesPath;
        
        } else {

            FileSelector loadPrompt = FileSelector.main;

            if (groupName.Length > 10 ) {
                groupName = groupName.Substring(0, 7) + "...";
            }

            //Set FileSelector to Load mode
            yield return loadPrompt.Initialise(
                saveMode:false, 
                new List<string> {"mol2"}, 
                string.Format("Load {0}", groupName)
            );
            
            //Wait for user response
            while (!loadPrompt.userResponded) {
                yield return null;
            }

            if (loadPrompt.cancelled) {
                GameObject.Destroy(loadPrompt.gameObject);
                GameObject.Destroy(groupGeometry.gameObject);
                GameObject.Destroy(newNSR.gameObject);
                yield break;
            }

            chargeFilePath = loadPrompt.confirmedText;
            GameObject.Destroy(loadPrompt.gameObject);

        }
        
            
        if (File.Exists(chargeFilePath)) {
            yield return new Mol2Reader(geometryInterface.geometry).SetAtomChargesFromMol2File(
                chargeFilePath, 
                geometryInterface.geometry, 
                atomMap:p2nMap
            );
        } else {
            CustomLogger.LogFormat(
                EL.ERROR,
                "File ({0}) doesn't exist (PartialChargeCalculator)",
                chargeFilePath
            );
        }

        GameObject.Destroy(groupGeometry.gameObject);
        GameObject.Destroy(newNSR.gameObject);

    }

    static Map<AtomID, int> GetP2NMap(Geometry groupGeometry, Geometry newNSR) {
        
        Map<AtomID, int> p2nMap = new Map<AtomID, int>();
        Dictionary<Element, int> elementCountDict = new Dictionary<Element, int>();
        Dictionary<ResidueID, Residue> newResidueDict = new Dictionary<ResidueID, Residue>();

        List<ResidueID> newResidueIDs = groupGeometry.EnumerateResidueIDs().ToList();
        newResidueIDs.Sort();

        int atomNum = 0;
        foreach (ResidueID nsrResidueID in newResidueIDs) {
            Residue residue = groupGeometry.GetResidue(nsrResidueID);
            Residue newResidue = new Residue(nsrResidueID, residue.residueName, newNSR);
            List<PDBID> pdbIDs = residue.pdbIDs.ToList();
            pdbIDs.Sort();
            foreach (PDBID pdbID in residue.pdbIDs.ToList()) {
                Element element = pdbID.element;
                int elementCount;
                if (elementCountDict.ContainsKey(element)) {
                    elementCount = elementCountDict[element] += 1;
                } else {
                    elementCount = elementCountDict[element] = 1;
                }

                PDBID newPDBID = new PDBID(pdbID.element, "", elementCount);
                newResidue.AddAtom(newPDBID, residue.GetAtom(pdbID).Copy());

                if (residue.state != RS.CAP) {
                    p2nMap[atomNum++] = new AtomID(nsrResidueID, pdbID);
                }

            }
            newResidueDict[nsrResidueID] = newResidue;
            newResidueDict[nsrResidueID].state = residue.state;
        }
        newNSR.SetResidues(newResidueDict);

        return p2nMap;
    }

    static IEnumerator RunRED(
        Geometry groupGeometry, 
        Geometry newNSR, 
        string modifiedRedCommandPath=""
    ) {
        //Write P2N file for R.E.D.
        string p2nFile = string.Format("{0}.p2n", Settings.redCalcPath);

        FileWriter fileWriter;
        try {
            fileWriter = new FileWriter(newNSR, p2nFile, true);
        } catch (System.ArgumentException e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to run RED! {0}",
                e.Message
            );
            yield break;
        }
        yield return fileWriter.WriteFile();


        
        if (!File.Exists(p2nFile)) {
            CustomLogger.LogFormat(
                EL.INFO,
                "Charge calculation cancelled"
            );
            GameObject.Destroy(groupGeometry.gameObject);
            GameObject.Destroy(newNSR.gameObject);
            yield break;
        }

        //Run R.E.D.
        string command = string.Format("perl {0}", modifiedRedCommandPath);
        CustomLogger.LogFormat(
            EL.INFO,
            "Starting R.E.D. command: '{0}'. Using p2n file: '{1}'", 
            command, 
            p2nFile
        );

        Bash.ProcessResult result = new Bash.ProcessResult();
        IEnumerator processEnumerator = Bash.ExecuteShellCommand(
            command, 
            result, 
            logOutput:true, 
            logError:true,
            directory:Settings.projectPath
        );


        while (processEnumerator.MoveNext()) {
            yield return null;
        }

        if (result.ExitCode != 0) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "{0} failed!", 
                command
            );
        }
    }



    static IEnumerator ProcessResidueGroupGaussian(
        List<ResidueID> residueGroup, 
        GeometryInterface geometryInterface, 
        bool internalRESP,
        TID taskID
    ) {

        if (residueGroup.Count() == 0) {
            yield break;
        }
                
        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Calculating Charges on Residues: '{0}'.",
            string.Join("', '", residueGroup)
        );
        Geometry groupGeometry = geometryInterface.geometry.TakeResidues(residueGroup, null);
        Constants.ChainID chainID = residueGroup.First().chainID;

        foreach (Atom atom in groupGeometry.EnumerateAtoms()) {
            atom.oniomLayer = OLID.REAL;
        }

		yield return groupGeometry.gaussianCalculator.EstimateChargeMultiplicity(true);

        if (groupGeometry.gaussianCalculator.cancelled) {

            yield break;
        }

        GaussianCalculator gc = groupGeometry.gaussianCalculator;
        Layer layer = gc.layerDict[OLID.REAL];
        string groupBaseName = string.Join("-", residueGroup);

        string chargesDirectory = Path.Combine(Settings.projectPath, Settings.chargesDirectory);

        if (!Directory.Exists(chargesDirectory)) {
            Directory.CreateDirectory(chargesDirectory);
        }

        string optInput = Path.Combine(Settings.projectPath, Settings.chargesDirectory, groupBaseName + "_opt1.gjf");
        string optOutput = Path.Combine(Settings.projectPath, Settings.chargesDirectory, groupBaseName + "_opt1.log");
        string optCheck = Path.Combine(Settings.projectPath, Settings.chargesDirectory, groupBaseName + "_opt1.chk");

        gc.numProcessors = 4;

        gc.doOptimisation = true;
        gc.convergenceThreshold = GCT.LOOSE;

        gc.additionalKeywords = new List<string> {};

        gc.title = "Initial Optimisation";

        gc.checkpointPath = optCheck;

        layer.method = "PM6";
        layer.basis = "";

        groupGeometry.GenerateAtomMap();

        yield return CalculationSetup.SetupCalculation(groupGeometry);

        int electrons = groupGeometry.EnumerateAtomIDs().Select(x => x.pdbID.atomicNumber).Sum();

        yield return gaussian.WriteInputAndExecute(
            groupGeometry,
            taskID,
            true,
            false,
            true,
            //Estimation of the amount of time PM6 opt takes for 1% completion
            (float)(CustomMathematics.IntPow(electrons, 3) / 1000000),
            optInput,
            optOutput
        );
        
        yield return FileReader.UpdateGeometry(groupGeometry, optOutput, updatePositions:true, chainID:chainID);

        gc.additionalKeywords = new List<string> {"SCF=Conver=8"};

        optInput = Path.Combine(Settings.projectPath, Settings.chargesDirectory, groupBaseName + "_opt2.gjf");
        optOutput = Path.Combine(Settings.projectPath, Settings.chargesDirectory, groupBaseName + "_opt2.log");
        optCheck = Path.Combine(Settings.projectPath, Settings.chargesDirectory, groupBaseName + "_opt2.chk");

        gc.convergenceThreshold = GCT.TIGHT;
        
        gc.title = "Second Optimisation";

        gc.checkpointPath = optCheck;

        layer.method = "HF";
        layer.basis = "6-31g(d)";

        yield return CalculationSetup.SetupCalculation(groupGeometry);

        yield return gaussian.WriteInputAndExecute(
            groupGeometry,
            taskID,
            true,
            false,
            true,
            //Estimation of the amount of time HF opt takes for 1% completion
            (float)(CustomMathematics.IntPow(electrons, 4) / 10000000),
            optInput,
            optOutput
        );

        yield return FileReader.UpdateGeometry(groupGeometry, optOutput, updatePositions:true, chainID:chainID);

        //Charges

        string chargeInput = Path.Combine(Settings.projectPath, Settings.chargesDirectory, groupBaseName + "_charge.gjf");
        string chargeOutput = Path.Combine(Settings.projectPath, Settings.chargesDirectory, groupBaseName + "_charge.log");
        string chargeCheck = Path.Combine(Settings.projectPath, Settings.chargesDirectory, groupBaseName + "_charge.chkSettings.");

        gc.doOptimisation = false;
        gc.convergenceThreshold = GCT.TIGHT;

        gc.additionalKeywords = new List<string> {"SCF=Conver=6", "Pop=MK", "IOp(6/33=2,6/41=10,6/42=17,6/77=50)"};

        gc.title = "Charge Derivation";

        layer.method = "HF";
        layer.basis = "6-31g(d)";

        yield return CalculationSetup.SetupCalculation(groupGeometry);

        yield return gaussian.WriteInputAndExecute(
            groupGeometry,
            taskID,
            true,
            false,
            true,
            //Estimation of the amount of time HF opt takes for 1% completion
            (float)(CustomMathematics.IntPow(electrons, 4) / 10000000000),
            chargeInput,
            chargeOutput
        );

        yield return FileReader.UpdateGeometry(geometryInterface.geometry, chargeOutput, updateCharges:true, chainID:chainID);

    }

    
    ///<summary>Redistributes the charge from Capping Residues into Capped Residues.</summary>
    ///<param name="residueGroup">The group of connected residues that might contain one or more Caps.</param>
    ///<param name="geometryInterface">The Geometry Interface containing residueGroup.</param>
    public static IEnumerator RedistributeCharge(
        Geometry groupGeometry,
        List<float> chargeDistributions
    ) {

        int maxDepth = chargeDistributions.Count;

        foreach ((ResidueID residueID, Residue capResidue) in groupGeometry.EnumerateResidues()) {
            
            //Ignore residues that aren't caps
            if (capResidue.state != RS.CAP) {
                continue;
            }

            //This is the charge that will be spread over the Capped Residue/s
            float capCharge = capResidue.GetCharge();

            //Get sites neighbouring the caps
            List<(AtomID, AtomID, Atom, Atom)> neighbourSites = capResidue.NeighbouringAtoms().ToList();

            //Get number of sites, which the total charge will be divided by
            int numSites = neighbourSites.Count;

            float movedCharge = 0f;

            foreach ((AtomID capID, AtomID neighbourID, Atom capAtom, Atom neighbourAtom) in neighbourSites) {

                //Get the atoms near to this site
                List<(Atom, AtomID, int)> closeAtoms = groupGeometry
                    .GetConnectedAtoms(neighbourID, new HashSet<AtomID>{capID}, maxDepth)
                    .ToList();

                //These are the lists of atoms for each graph distance from the residue interface
                List<AtomID>[] chargeRedistributionAtoms = new List<AtomID>[maxDepth];
                for (int depth=0; depth<maxDepth; depth++) {
                    chargeRedistributionAtoms[depth] = new List<AtomID>();
                }

                //Use the closeAtoms list to fill chargeRedistributionAtoms
                foreach ((Atom closeAtom, AtomID closeAtomID, int depth) in closeAtoms) {
                    chargeRedistributionAtoms[depth].Add(closeAtomID);
                }

                //Iterate over each depth so the charge can be spread over each of these 
                for (int depth=0; depth<maxDepth; depth++) {

                    List<AtomID> depthAtoms = chargeRedistributionAtoms[depth];

                    int numAtoms = depthAtoms.Count;
                    if (numAtoms == 0) {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "No atoms in Charge Layer {0} - there might be errors in the total charge of Residue - {1}",
                            depth,
                            neighbourID.residueID
                        );
                        continue;
                    }

                    float chargePartition = chargeDistributions[depth] * capCharge / (numSites * numAtoms);

                    foreach (AtomID atomID in depthAtoms) {
                        Atom atom;
                        if (!groupGeometry.TryGetAtom(atomID, out atom)) {
                            CustomLogger.LogFormat(
                                EL.WARNING,
                                "Unable to redistribute charge to Atom '{0}' - cannot find Atom! Recalculate connectivity and retry.",
                                atomID
                            );
                        }

                        atom.partialCharge += chargePartition;
                        movedCharge += chargePartition;
                    }

                }
            }

            foreach ((PDBID pdbID, Atom capAtom) in groupGeometry.GetResidue(residueID).EnumerateAtoms()) {
                capAtom.partialCharge = 0f;
            }

            if (Timer.yieldNow) {
                yield return null;
            }
        }
    }

    ///<summary>Redistributes the charge on a Residue to create an overall integer charge.</summary>
    ///<param name="residueGroup">The Geometry whose Atoms' charges will be rounded.</param>
    public static IEnumerator RoundCharge(
        Geometry groupGeometry
    ) {
        
        foreach ((ResidueID residueID, Residue residue) in groupGeometry.EnumerateResidues()) {
            float residueCharge = residue.GetCharge();
            float chargeDifference = Mathf.RoundToInt(residueCharge) - residueCharge;
            int numAtoms = residue.size;
            if (numAtoms == 0) {
                continue;
            }
            float chargeToAdd = chargeDifference / numAtoms;

            foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
                atom.partialCharge += chargeToAdd;
            }

            if (Timer.yieldNow) {
                yield return null;
            }
        }
    }
    
}
