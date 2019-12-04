using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Element = Constants.Element;
using TID = Constants.TaskID;
using GIID = Constants.GeometryInterfaceID;
using GIS = Constants.GeometryInterfaceStatus;
using RS = Constants.ResidueState;
using EL = Constants.ErrorLevel;
using System.IO;
using System.Linq;
using System.Text;
using System;
using System.Diagnostics;

public static class PartialChargeCalculator {


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
        return GetPartialCharges(geometryInterfaceID, false, TID.GET_PARTIAL_CHARGES_FROM_MOL2);
    }
    
    public static IEnumerator CalculatePartialCharges(GIID geometryInterfaceID) {
        return GetPartialCharges(geometryInterfaceID, true, TID.CALCULATE_PARTIAL_CHARGES);
    }

    static IEnumerator GetPartialCharges(GIID geometryInterfaceID, bool runRed, TID taskID) {

        // Based on code by Clyde Fare
        // https://github.com/Clyde-fare/cc_utils

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;

        //Modify R.E.D. program
        string redCommandFilename = Path.GetFileName(Settings.redCommandPath);
        string modifiedRedCommandFilename = "mod_" + redCommandFilename;
        string modifiedRedCommandPath = Path.Combine(Settings.projectPath, modifiedRedCommandFilename);
        
        if (runRed) {
            //Check Perl
            if (!Bash.CommandExists("perl")) {
                throw new ErrorHandler.CommandNotFoundException(
                    string.Format("perl not installed. Cannot run R.E.D."),
                    "perl"
                );
            }

            NotificationBar.SetTaskProgress(taskID, 0f);
            yield return null;

            //Get options for R.E.D. command line tool
            RedOptions redOptions = RedOptions.main;
            redOptions.Initialise();
            while (!redOptions.userResponded) {
                yield return null;
            }
            Dictionary<string, string> results = redOptions.GetResults();

            NotificationBar.SetTaskProgress(taskID, 0.1f);
            yield return null;
            
            //Check Gaussian
            GaussianCalculator.MakeGaussianAvailable();
            if (!Bash.CommandExists(Settings.gaussianVersion)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Gaussian (version {0}) not installed. Cannot run R.E.D.", 
                    Settings.gaussianVersion
                );
                NotificationBar.ClearTask(taskID);
                geometryInterface.activeTasks--;
                yield break;
            }

            if (Settings.gaussianSCRDIR == "" || !Directory.Exists(Settings.gaussianSCRDIR)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Scratch Directory ({0}) does not exist. Cannot run R.E.D.", 
                    Settings.gaussianSCRDIR
                );
                NotificationBar.ClearTask(taskID);
                geometryInterface.activeTasks--;
                yield break;
            }

            //Check if Scratch directory is empty - R.E.D. refuses to run if it's not empty
            bool scratchHasFiles = Directory.EnumerateFileSystemEntries(Settings.gaussianSCRDIR)
                .Where(
                    //Exclude hidden files in search
                    x => ! new FileInfo(x).Attributes.HasFlag(FileAttributes.Hidden)
                ).Any();

            if (scratchHasFiles) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Scratch Directory ({0}) is not empty. Cannot run R.E.D.", 
                    Settings.gaussianSCRDIR
                );
                NotificationBar.ClearTask(taskID);
                geometryInterface.activeTasks--;
                yield break;
            }

            yield return ModifyRED(Settings.redCommandPath, modifiedRedCommandPath, results);

            NotificationBar.SetTaskProgress(taskID, 0.2f);
            yield return null;
        }


        //These are all the Residues to have charges computed
        List<ResidueID> residueIDs = geometryInterface.geometry.residueDict.Keys.ToList();
        //Use this list to keep track of which Residues have already been processed
        List<ResidueID> seenResidueIDs = new List<ResidueID>();

        foreach(ResidueID residueID in residueIDs) {

            //Skip this Residue if it's been grouped with other Residues and processed
            if (seenResidueIDs.Contains(residueID)) {
                continue;
            }

            IEnumerable<ResidueID> connectedResidues = geometryInterface
                .geometry
                .GetConnectedResidues(residueID);
                
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Processing Residues: '{0}'.",
                string.Join("', '", connectedResidues)
            );

            //Add them to the seen Residue IDs list, so they won't be processed again
            seenResidueIDs.AddRange(connectedResidues);

            Geometry residueGroup = geometryInterface.geometry.TakeResidues(connectedResidues, null);
            Geometry newNSR = PrefabManager.InstantiateGeometry(null);

            //R.E.D. wants to have atom names that look like:
            // N1 C1 C2 C3 H1 H2 etc
            Dictionary<int, AtomID> p2nMap = new Dictionary<int, AtomID>();
            Dictionary<Element, int> elementCountDict = new Dictionary<Element, int>();
            Dictionary<ResidueID, Residue> newResidueDict = new Dictionary<ResidueID, Residue>();

            List<ResidueID> newResidueIDs = residueGroup.residueDict.Keys.ToList();
            newResidueIDs.Sort();

            int atomNum = 0;
            foreach (ResidueID nsrResidueID in newResidueIDs) {
                Residue residue = residueGroup.residueDict[nsrResidueID];
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
                    newResidue.AddAtom(newPDBID, residue.atoms[pdbID].Copy());

                    if (residue.state != RS.CAP) {
                        p2nMap[atomNum++] = new AtomID(nsrResidueID, pdbID);
                    }

                }
                newResidueDict[nsrResidueID] = newResidue;
                newResidueDict[nsrResidueID].state = residue.state;
            }
            newNSR.SetResidueDict(newResidueDict);

            if (runRed) {

                //Write P2N file for R.E.D.
                string p2nFile = string.Format("{0}.p2n", Settings.redCalcPath);
                yield return FileWriter.WriteFile(newNSR, p2nFile, true);

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

            yield return new Mol2Reader(geometryInterface.geometry).SetAtomChargesFromMol2File(
                Settings.redChargesPath, 
                geometryInterface.geometry, 
                atomNumToAtomIDDict:p2nMap
            );

            GameObject.Destroy(residueGroup.gameObject);
            GameObject.Destroy(newNSR.gameObject);
        }



        geometryInterface.activeTasks--;
        NotificationBar.ClearTask(taskID);
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
    
}
