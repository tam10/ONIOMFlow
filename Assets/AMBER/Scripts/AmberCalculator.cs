using System.Collections;
using System.Collections.Generic;
using Amber = Constants.Amber;
using EL = Constants.ErrorLevel;
using TID = Constants.TaskID;
using GIID = Constants.GeometryInterfaceID;
using RCID = Constants.ResidueCheckerID;
using ACID = Constants.AtomCheckerID;
using GIS = Constants.GeometryInterfaceStatus;
using ChainID = Constants.ChainID;
using System.Linq;
using UnityEngine;

public static class AmberCalculator {

    /// <summary>Use the standardResidues.xml library to set Residue Ambers for Standard and Cap Residues.</summary>
	/// <param name="geometryInterfaceID">ID of Geometry Interface to Clean.</param>
    public static IEnumerator CalculateAMBERTypes(GIID geometryInterfaceID) {

        NotificationBar.SetTaskProgress(TID.CALCULATE_AMBER_TYPES, 0f);
        CustomLogger.LogFormat(
            EL.INFO, 
            "Calculating AMBER Types internally for Geometry Interface: {0}.", 
            geometryInterfaceID
        );

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;
        yield return null;

        Geometry geometry = geometryInterface.geometry;

        //Loop through all residues
        int numProcessedResidues = 0;
        int totalResidues = geometry.residueCount;
        foreach (ResidueID residueID in geometry.EnumerateResidueIDs().ToList()) {
            Residue residue = geometry.GetResidue(residueID);
            //Set their Amber types
            Data.SetResidueAmbers(residue);

            numProcessedResidues++;
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(TID.CALCULATE_AMBER_TYPES, (float)numProcessedResidues / totalResidues);
                yield return null;
            }
        }

        geometryInterface.activeTasks--;

        NotificationBar.ClearTask(TID.CALCULATE_AMBER_TYPES);
        yield return null;

    }

    /// <summary>Use Antechamber to set Residue Ambers for all Residues.</summary>
	/// <param name="geometryInterfaceID">ID of Geometry Interface to Clean.</param>
    public static IEnumerator CalculateAMBERTypesAntechamber(GIID geometryInterfaceID) {

        NotificationBar.SetTaskProgress(TID.CALCULATE_AMBER_TYPES_ANTECHAMBER, 0f);
        CustomLogger.LogFormat(
            EL.INFO, 
            "Calculating AMBER Types using Antechamber for Geometry Interface: {0}.", 
            geometryInterfaceID
        );
        yield return null;

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);

        //Remove water residues from the calculation
        Geometry geometry = geometryInterface.geometry.TakeDry(geometryInterface.transform);

        //Get the chainIDs
        IEnumerable<ChainID> chainIDs = geometry.GetChainIDs();

        NotificationBar.SetTaskProgress(TID.CALCULATE_AMBER_TYPES_ANTECHAMBER, 0.1f);
        yield return null;
           
        geometryInterface.activeTasks++;
        
        
		Bash.ExternalCommand externalCommand;
		try {
			externalCommand = Settings.GetExternalCommand("amber");
		} catch (KeyNotFoundException e) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Could not find external command 'amber'!"
			);
			CustomLogger.LogOutput(e.StackTrace);
			yield break;
		}

        //Loop through each chain
        foreach (ChainID chainID in chainIDs) {

            CustomLogger.LogFormat(
                EL.INFO, 
                "Calculating AMBER Types for chainID: {0}.", 
                Constants.ChainIDMap[chainID]
            );
            
            //Isolate Chain
            Geometry chainGeometry = geometry.TakeChain(chainID, null);

            IEnumerator<char> alphabet = Enumerable.Range(65, 26).Select(x => (char)x).GetEnumerator();
            foreach (List<ResidueID> residueGroup in chainGeometry.GetGroupedResidues()) {

                alphabet.MoveNext();

                externalCommand.SetSuffix(string.Format("_{0}_{1}", chainID, alphabet.Current));
                //Write PDB file for Antechamber

                Geometry groupGeometry = chainGeometry.TakeResidues(residueGroup, null);
                groupGeometry.GenerateAtomMap();

                yield return externalCommand.WriteInputAndExecute(
                    groupGeometry, 
                    TID.CALCULATE_AMBER_TYPES_ANTECHAMBER,
                    true,
                    true,
                    true,
                    (float)groupGeometry.size / 5000
                );

                if (externalCommand.succeeded) {
                    yield return new Mol2Reader(geometryInterface.geometry, chainID).SetAtomAmbersFromMol2File(
                        externalCommand.GetOutputPath(), 
                        geometryInterface.geometry, 
                        chainID,
                        groupGeometry.atomMap
                    );
                }

                GameObject.Destroy(groupGeometry.gameObject);
            }

            GameObject.Destroy(chainGeometry.gameObject);

            
        }
        
        NotificationBar.ClearTask(TID.CALCULATE_AMBER_TYPES_ANTECHAMBER);
        geometryInterface.activeTasks--;

        GameObject.Destroy(geometry.gameObject);
    }

    public static IEnumerator CalculateAMBERParameters(GIID geometryInterfaceID) {
        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);

        //Check AMBER types are ok
        try {
            geometryInterface.checker.SetGeometry(geometryInterface.geometry);
        } catch (System.NullReferenceException e) {
            CustomLogger.LogFormat(
                EL.ERROR, 
                "Trying to calculate AMBER Parameters with null Geometry on Geometry Interface ID: {0}. Error: {1}",
                geometryInterfaceID,
                e.StackTrace
            );
        }

        //Get the original state of the AtomsChecker of this GeometryInterface
        List<RCID> oldResidueCheckIDs = geometryInterface.checker.residueCheckerOrder.ToList();
        List<ACID> oldAtomCheckIDs = geometryInterface.checker.atomCheckerOrder.ToList();
        Dictionary<RCID, GIS> oldResidueChecks = geometryInterface.checker.residueErrorLevels.ToDictionary(x => x.Key, x => x.Value); 
        Dictionary<ACID, GIS> oldAtomChecks = geometryInterface.checker.atomErrorLevels.ToDictionary(x => x.Key, x => x.Value); 

        //Set a temporary AtomsChecker state to check for Ambers
        List<RCID> tempResidueCheckIDs = new List<RCID>();
        List<ACID> tempAtomCheckIDs = new List<ACID> {ACID.HAS_AMBER}; 
        Dictionary<RCID, GIS> tempResidueChecks = new Dictionary<RCID, GIS>(); 
        Dictionary<ACID, GIS> tempAtomChecks = new Dictionary<ACID, GIS>{{ACID.HAS_AMBER, GIS.ERROR}}; 
        geometryInterface.checker.SetChecks(
            tempResidueCheckIDs,
            tempAtomCheckIDs,
            tempResidueChecks,
            tempAtomChecks
        );

        //Run the checker
        yield return geometryInterface.checker.Check();

        //Get the results
        AtomChecker amberChecker;
        geometryInterface.checker.atomCheckers.TryGetValue(ACID.HAS_AMBER, out amberChecker);
        GIS amberCheckResult = geometryInterface.checker.errorLevel;

        //Set the checker back to how it was
        geometryInterface.checker.SetChecks(
            oldResidueCheckIDs,
            oldAtomCheckIDs,
            oldResidueChecks,
            oldAtomChecks
        );

        if (amberChecker == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot compute AMBER parameters - Geometry Interface {0} is not checking for AMBER types",
                geometryInterfaceID
            );
            yield break;
        }

        if (amberCheckResult != GIS.OK) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot compute AMBER parameters - Geometry Interface {0} failed AMBER type check (error level: {1})",
                geometryInterfaceID,
                geometryInterface.checker.errorLevel
            );
            yield break;
        }
        
        geometryInterface.activeTasks++;
        //Geometry Interface is now properly conditioned to calculate Parameters
        yield return geometryInterface.geometry.parameters.Calculate2();
        geometryInterface.activeTasks--;
        yield return null;
    }
    

    public static bool TryGetAmber(string amberString, out Amber amber) {
		amber = Amber.X;
        if (!Constants.AmberMap.TryGetValue(amberString, out amber)) {
            return false;
        }
        return true;
    }
    
    public static Amber GetAmber(string amberString) {
		Amber type = Amber.X;
		if (!Constants.AmberMap.TryGetValue(amberString, out type)) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Couldn't parse AMBER: {0}",
				amberString
			);
		}
        if (type == Amber.DU) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Parsed Dummy AMBER: {0}",
				amberString
			);
        }
		return type;
	}

    public static string GetAmberString(Amber amber) {
        return Constants.AmberMap[amber];
    }

    public static Amber[] GetAmbers(string typesString) {
		return typesString
			.Split(new char[1] {'-'}, System.StringSplitOptions.None)
			.Select(x => GetAmber(x))
			.ToArray();
	}
}
