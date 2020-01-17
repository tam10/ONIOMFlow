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
            "Performing Standard Protonation on {0}.",
            geometryInterfaceID
        );

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;
        
		Bash.ExternalCommand externalCommand;
		try {
			externalCommand = Settings.GetExternalCommand("protonate_sr");
		} catch (KeyNotFoundException e) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Could not find external command 'protonate_sr'!"
			);
			CustomLogger.LogOutput(e.StackTrace);
			yield break;
		}
        
        Geometry geometry = geometryInterface.geometry;

        Geometry tempGeometry = geometry.Take(
            null, 
            x => x.pdbID.element != Element.H
        );
        tempGeometry.GenerateAtomMap();
        
        externalCommand.SetSuffix("");

        yield return externalCommand.WriteInputAndExecute(
            tempGeometry, 
            TID.PROTONATE_PDB2PQR,
            false,
            true,
            true,
            (float)tempGeometry.size / 5000
        );

        if (externalCommand.succeeded) {
            tempGeometry.residueDict = new Dictionary<ResidueID, Residue>();
            yield return FileReader.LoadGeometry(
                tempGeometry,
                externalCommand.GetOutputPath(),
                "ProtonateStandard"
            );
            
            SetProtonatedResidueGroup(tempGeometry, geometryInterface, "");
        } else {
            CustomLogger.LogFormat(
                EL.ERROR,
                "SR Protonation failed"
            );
        }
        GameObject.Destroy(tempGeometry.gameObject);

        yield return geometryInterface.geometry.SetAllResidueProperties();
        
        geometryInterface.activeTasks--;
        NotificationBar.ClearTask(TID.PROTONATE_PDB2PQR);

    }

    /// <summary>Protonates the Standard Residues of a Geometry Interface's Geometry.</summary>
    /// <param name="geometryInterfaceID">The Geometry Interface ID of the Geometry to protonate.</param>
    public static IEnumerator ProtonateGroupsWithPDB2PQR(GIID geometryInterfaceID) {
        
        NotificationBar.SetTaskProgress(TID.PROTONATE_PDB2PQR, 0f);

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Performing Standard Protonation on {0}.",
            geometryInterfaceID
        );

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;


        
		Bash.ExternalCommand externalCommand;
		try {
			externalCommand = Settings.GetExternalCommand("protonate_sr");
		} catch (KeyNotFoundException e) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Could not find external command 'protonate_sr'!"
			);
			CustomLogger.LogOutput(e.StackTrace);
			yield break;
		}
        
        Geometry geometry = geometryInterface.geometry;

        List<List<ResidueID>> residueGroups = geometry.GetGroupedResidues().ToList();
        
        foreach (List<ResidueID> residueGroup in residueGroups) {
            
            Geometry groupGeometry = geometry.TakeResidues(
                residueGroup, 
                null, 
                x => x.pdbID.element != Element.H
            );
            groupGeometry.GenerateAtomMap();

            List<ResidueID> sortedResidueIDs = groupGeometry.residueDict.Keys.OrderBy(x => x).ToList();
            string suffix;
            if (sortedResidueIDs.Count == 1) {
                suffix = string.Format(
                    "{0}", 
                    sortedResidueIDs.First()
                );
            } else {
                suffix = string.Format(
                    "{0}-{1}", 
                    sortedResidueIDs.First(),
                    sortedResidueIDs.Last()
                );
            }

            externalCommand.SetSuffix("_" + suffix);

            yield return externalCommand.WriteInputAndExecute(
                groupGeometry, 
                TID.PROTONATE_PDB2PQR,
                false,
                true,
                true,
                (float)groupGeometry.size / 5000
            );

            if (externalCommand.succeeded) {
                groupGeometry.residueDict = new Dictionary<ResidueID, Residue>();
                yield return FileReader.LoadGeometry(
                    groupGeometry,
                    externalCommand.GetOutputPath(),
                    "ProtonateStandard"
                );
                
                SetProtonatedResidueGroup(groupGeometry, geometryInterface, suffix);
            } else {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "SR Protonation failed on '{0}'",
                    suffix
                );
            }
            GameObject.Destroy(groupGeometry.gameObject);
        }

        yield return geometryInterface.geometry.SetAllResidueProperties();
        
        geometryInterface.activeTasks--;
        NotificationBar.ClearTask(TID.PROTONATE_PDB2PQR);

    }

    /// <summary>Protonates the Non-Standard Residues of a Geometry Interface's Atoms.</summary>
    /// <param name="geometryInterfaceID">The Geometry Interface ID of the Atoms to protonate.</param>
    public static IEnumerator ProtonateNonStandard(GIID geometryInterfaceID) {
        
        NotificationBar.SetTaskProgress(TID.PROTONATE_REDUCE, 0f);
        yield return null;
        
        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;
        
		Bash.ExternalCommand externalCommand;
		try {
			externalCommand = Settings.GetExternalCommand("protonate_nsr");
		} catch (KeyNotFoundException e) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Could not find external command 'protonate_nsr'!"
			);
			CustomLogger.LogOutput(e.StackTrace);
			yield break;
		}
        
        Geometry geometry = geometryInterface.geometry;

        List<List<ResidueID>> residueGroups = geometry.GetGroupedResidues().ToList();
        
        foreach (List<ResidueID> residueGroup in residueGroups) {
            
            Geometry groupGeometry = geometry.TakeResidues(
                residueGroup, 
                null, 
                x => x.pdbID.element != Element.H
            );
            groupGeometry.GenerateAtomMap();

            List<ResidueID> sortedResidueIDs = groupGeometry.residueDict.Keys.OrderBy(x => x).ToList();
            string suffix;
            if (sortedResidueIDs.Count == 1) {
                suffix = string.Format(
                    "{0}", 
                    sortedResidueIDs.First()
                );
            } else {
                suffix = string.Format(
                    "{0}-{1}", 
                    sortedResidueIDs.First(),
                    sortedResidueIDs.Last()
                );
            }

            externalCommand.SetSuffix("_" + suffix);

            yield return externalCommand.WriteInputAndExecute(
                groupGeometry, 
                TID.PROTONATE_REDUCE,
                true,
                true,
                true,
                (float)groupGeometry.size / 5000
            );

            if (externalCommand.succeeded) {
                groupGeometry.residueDict = new Dictionary<ResidueID, Residue>();
                yield return FileReader.LoadGeometry(
                    groupGeometry,
                    externalCommand.GetOutputPath(),
                    "ProtonateNonStandard"
                );
                
                SetProtonatedResidueGroup(groupGeometry, geometryInterface, suffix);
            } else {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "NSR Protonation failed on '{0}'",
                    suffix
                );
            }
            CustomLogger.LogOutput(groupGeometry.gameObject);
            GameObject.Destroy(groupGeometry.gameObject);
        }
        
        yield return geometryInterface.geometry.SetAllResidueProperties();

        geometryInterface.activeTasks--;
        NotificationBar.ClearTask(TID.PROTONATE_REDUCE);

    }

    
    /// <summary>Check the output of PDB2PQR against its input, then replace the input.</summary>
    /// <param name="residueGroup">The Residue Group Geometry processed.</param>
    /// <param name="geometryInterfaceID">The Geometry Interface ID of the Residue Group.</param>
    /// <param name="groupNum">The index of the sequence of Residue Groups being processed.</param>
    private static void SetProtonatedResidueGroup(
        Geometry residueGroup, 
        GeometryInterface geometryInterface,
        string groupName
    ) {

        string groupString = string.IsNullOrWhiteSpace(groupName) 
            ? "" 
            : string.Format("(Group {0})", groupName);
        
        foreach ((ResidueID protonatedResidueID, Residue protonatedResidue) in residueGroup.residueDict) {

            //Check Residue hasn't changed its ID and cannot be mapped
            Residue originalResidue;
            if (!geometryInterface.geometry.TryGetResidue(protonatedResidueID, out originalResidue)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Couldn't find Residue ID '{0}' in Geometry Interface {1} {2}",
                    protonatedResidueID,
                    geometryInterface.id,
                    groupString
                );
                continue;
            }

            //Check Residue hasn't changed name - this would indicate something went seriously wrong
            if (originalResidue.residueName != protonatedResidue.residueName && !originalResidue.isWater) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Residue Name for ID {0} in Original Atoms ({1}) does not match Protonated Atoms ({2}) {3}",
                    protonatedResidueID,
                    originalResidue.residueName,
                    protonatedResidue.residueName,
                    groupString
                );
            }

            //Make sure all the heavy atoms haven't changed - this would also indicate something went seriously wrong
            ResidueSignature originalResidueSignature = new ResidueSignature(originalResidue.pdbIDs);
            ResidueSignature protonatedResidueSignature = new ResidueSignature(protonatedResidue.pdbIDs);
            if (!originalResidueSignature.HeavyAtomsMatch(protonatedResidueSignature)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Heavy atoms did not remain the same during protonation for Residue ID {0}! {1}",
                    protonatedResidueID,
                    groupString
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
