using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.Linq;
using Element = Constants.Element;
using TID = Constants.TaskID;
using GIID = Constants.GeometryInterfaceID;
using GIS = Constants.GeometryInterfaceStatus;
using RS = Constants.ResidueState;
using BT = Constants.BondType;
using EL = Constants.ErrorLevel;
public static class Protonator {

    public static IEnumerator GetProtonatedStandardResidues(GIID startID, GIID targetID, List<TID> taskIDs) {

        Flow.GetGeometryInterface(targetID).activeTasks++;
        yield return Flow.CopyGeometry(startID, targetID);
        
        foreach (TID taskID in taskIDs) {
            yield return RunTask(targetID, taskID);
        }
        
        Flow.GetGeometryInterface(targetID).activeTasks--;
        Flow.GetGeometryInterface(startID).status = GIS.COMPLETED;
    }

    public static IEnumerator GetProtonatedNonStandardResidues(GIID startID, GIID targetID, List<TID> taskIDs) {

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

    /// <summary>Protonates the Standard Residues of a Geometry Interface's Geometry.</summary>
    /// <param name="geometryInterfaceID">The Geometry Interface ID of the Geometry to protonate.</param>
    public static IEnumerator ProtonateWithPDB2PQR(GIID geometryInterfaceID) {
        
        NotificationBar.SetTaskProgress(TID.PROTONATE_PDB2PQR, 0f);

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Performing Standard Protonation with {0} on {1}.",
            Settings.pdb2pqrCommand,
            geometryInterfaceID
        );

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;

        //Check Command
        if (!Bash.CommandExists(Settings.pdb2pqrCommand)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Command not found: {0}. Check the settings for the Standard Protonation command in {1}.",
                Settings.pdb2pqrCommand,
                Settings.projectSettingsFilename
            );
            NotificationBar.ClearTask(TID.PROTONATE_REDUCE);
            yield break;
        }
        
        NotificationBar.SetTaskProgress(TID.PROTONATE_PDB2PQR, 0.1f);
        yield return null;

        //These are all the Residues to protonate
        List<ResidueID> residueIDs = geometryInterface.geometry.residueDict.Keys.ToList();
        //Use this list to keep track of which Residues have already been protonated
        //They will be grouped together so prevent dangling bonds being protonated
        List<ResidueID> seenResidueIDs = new List<ResidueID>();

        //This is used for file naming
        int groupNum = 1;

        foreach (ResidueID residueID in residueIDs) {
            
            //Skip this Residue if it's been grouped with other Residues and protonated
            if (seenResidueIDs.Contains(residueID)) {
                continue;
            }

            //Expand the selection of ResidueIDs based on connectivity
            IEnumerable<ResidueID> connectedResidues = geometryInterface
                .geometry
                .GetConnectedResidues(residueID);

            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Processing Residues (Group {0}): '{1}'.",
                groupNum,
                string.Join("', '", connectedResidues)
            );

            //Add them to the seen Residue IDs list, so they won't be processed again
            seenResidueIDs.AddRange(connectedResidues);

            //Get the group - connectivity shouldn't be an issue
            //Remove hydrogens
            Geometry residueGroup = geometryInterface.geometry.TakeResidues(
                connectedResidues, 
                null,
                x => x.pdbID.element != Element.H
            );

            //Write PDB file for Reduce
            yield return FileWriter.WriteFile(
                residueGroup, 
                string.Format("{0}_{1}.pdb", Settings.srProtonationPath, groupNum), 
                false
            );

            //Run pdb2pqr
            string phStr = string.Format("--ph-calc-method=propka --with-ph={0}", Settings.pH);
            string command = string.Format(
                 "{0} -v {1} {2} {3}_{4}.pdb {3}_{4}.pqr",
                Settings.pdb2pqrCommand,
                string.Join(" ", Settings.pdb2pqrOptions.ToArray()),
                phStr,
                Settings.srProtonationPath,
                groupNum
            );
            
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Running command: {0}.",
                command
            );

            NotificationBar.SetTaskProgress(TID.PROTONATE_PDB2PQR, 0.1f);
            Process process = Bash.StartBashProcess(command, logOutput:true, logError:true);
            float progress = 0.1f;
            while (!process.HasExited) {
                process.Refresh();
                NotificationBar.SetTaskProgress(TID.PROTONATE_PDB2PQR, progress);
                //Show that external command is running
                progress = progress < 0.9f? progress + 0.01f : progress;
                yield return new WaitForSeconds(0.25f);
            }

            if (process.ExitCode != 0) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "{0} failed!", 
                    Settings.pdb2pqrCommand
                );
                continue;
            }

            process.Close();

            //Read new atoms
            residueGroup.residueDict = new Dictionary<ResidueID, Residue>();
            yield return FileReader.LoadGeometry(
                residueGroup, 
                string.Format("{0}_{1}.pqr", Settings.srProtonationPath, groupNum)
            );

            SetProtonatedResidueGroup(residueGroup, residueID, geometryInterface, groupNum++);

            GameObject.Destroy(residueGroup.gameObject);
            
        }

        yield return geometryInterface.geometry.SetAllResidueProperties();
        
        geometryInterface.activeTasks--;
        NotificationBar.ClearTask(TID.PROTONATE_PDB2PQR);

    }

    /// <summary>Protonates the Non-Standard Residues of a Geometry Interface's Atoms.</summary>
    /// <param name="geometryInterfaceID">The Geometry Interface ID of the Atoms to protonate.</param>
    public static IEnumerator ProtonateNonStandard(GIID geometryInterfaceID) {
        
        NotificationBar.SetTaskProgress(TID.PROTONATE_REDUCE, 0f);

        CustomLogger.LogFormat(
            EL.INFO,
            "Performing Non-Standard Protonation with {0} on {1}.",
            Settings.reduceCommand,
            geometryInterfaceID
        );

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;

        //Check Command
        if (!Bash.CommandExists(Settings.reduceCommand)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Command not found: {0}. Check the settings for the Non-Standard Protonation command in {1}.",
                Settings.reduceCommand,
                Settings.projectSettingsFilename
            );
            NotificationBar.ClearTask(TID.PROTONATE_REDUCE);
            yield break;
        }
        
        NotificationBar.SetTaskProgress(TID.PROTONATE_REDUCE, 0.1f);
        yield return null;

        //These are all the Residues to protonate
        List<ResidueID> residueIDs = geometryInterface.geometry.residueDict.Keys.ToList();
        //Use this list to keep track of which Residues have already been protonated
        //They will be grouped together so prevent dangling bonds being protonated
        List<ResidueID> seenResidueIDs = new List<ResidueID>();

        //This is used for file naming
        int groupNum = 1;

        foreach (ResidueID residueID in residueIDs) {
            
            //Skip this Residue if it's been grouped with other Residues and protonated
            if (seenResidueIDs.Contains(residueID)) {
                continue;
            }

            //Expand the selection of ResidueIDs based on connectivity
            IEnumerable<ResidueID> connectedResidues = geometryInterface
                .geometry
                .GetConnectedResidues(residueID);
            
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Processing Residues (Group {0}): '{1}'.",
                groupNum,
                string.Join("', '", connectedResidues)
            );

            //Add them to the seen Residue IDs list, so they won't be processed again
            seenResidueIDs.AddRange(connectedResidues);

            //Get the group - connectivity shouldn't be an issue
            //Remove hydrogens
            Geometry residueGroup = geometryInterface.geometry.TakeResidues(
                connectedResidues, 
                null,
                x => x.pdbID.element != Element.H
            );

            //Write PDB file for Reduce
            yield return FileWriter.WriteFile(
                residueGroup, 
                string.Format("{0}_{1}.pdb", Settings.nsrProtonationPath, groupNum), 
                true
            );

            //Run Reduce
            string command = string.Format(
                "{0} {1} {2}_{3}.pdb > {2}_{3}_out.pdb", 
                Settings.reduceCommand,
                string.Join(" ", Settings.reduceOptions.ToArray()),
                Settings.nsrProtonationPath, 
                groupNum
            );
            
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Running command: {0}.",
                command
            );

            Process process = Bash.StartBashProcess(command, logOutput:true, logError:true);
            while (!process.HasExited) {
                yield return new WaitForSeconds(0.25f);
            }

            if (process.ExitCode != 0) {
                CustomLogger.LogFormat(
                    EL.INFO,
                    "{0} failed!", 
                    Settings.reduceCommand
                );
            }

            //Read new atoms
            residueGroup.residueDict = new Dictionary<ResidueID, Residue>();
            yield return FileReader.LoadGeometry(
                residueGroup, 
                string.Format("{0}_{1}_out.pdb", Settings.nsrProtonationPath, groupNum)
            );

            SetProtonatedResidueGroup(residueGroup, residueID, geometryInterface, groupNum++);

            GameObject.Destroy(residueGroup.gameObject);

            groupNum++;
        }
        yield return geometryInterface.geometry.SetAllResidueProperties();

        geometryInterface.activeTasks--;
        NotificationBar.ClearTask(TID.PROTONATE_REDUCE);

    }

    private static void SetProtonatedResidueGroup(
        Geometry residueGroup, 
        ResidueID residueID, 
        GeometryInterface geometryInterface,
        int groupNum
    ) {
        foreach ((ResidueID protonatedResidueID, Residue protonatedResidue) in residueGroup.residueDict) {

            //Check Residue hasn't changed its ID and cannot be mapped
            Residue originalResidue;
            if (!geometryInterface.geometry.TryGetResidue(protonatedResidueID, out originalResidue)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Couldn't find Residue ID '{0}' in Geometry Interface {1} (Group {2})",
                    protonatedResidueID,
                    geometryInterface.id,
                    groupNum
                );
                continue;
            }

            //Check Residue hasn't changed name - this would indicate something went seriously wrong
            if (originalResidue.residueName != protonatedResidue.residueName) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Residue Name for ID {0} in Original Atoms ({1}) does not match Protonated Atoms ({2}) (Group {3})",
                    protonatedResidueID,
                    originalResidue.residueName,
                    protonatedResidue.residueName,
                    groupNum
                );
            }

            //Make sure all the heavy atoms haven't changed - this would also indicate something went seriously wrong
            ResidueSignature originalResidueSignature = new ResidueSignature(originalResidue.pdbIDs);
            ResidueSignature protonatedResidueSignature = new ResidueSignature(protonatedResidue.pdbIDs);
            if (!originalResidueSignature.HeavyAtomsMatch(protonatedResidueSignature)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Heavy atoms did not remain the same during protonation for Residue ID {0}! (Group 1)",
                    protonatedResidueID,
                    groupNum
                );
            }

            //Don't replace Caps
            if (originalResidue.state == RS.CAP) {
                continue;
            }

            //Set the Residue
            geometryInterface.geometry.SetResidue(protonatedResidueID, protonatedResidue);
        }
    }

    


}
