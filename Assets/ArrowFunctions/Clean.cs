using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;
using System.IO;
using Element = Constants.Element;
using TID = Constants.TaskID;
using RCID = Constants.ResidueCheckerID;
using ACID = Constants.AtomCheckerID;
using GIID = Constants.GeometryInterfaceID;
using RS = Constants.ResidueState;
using GIS = Constants.GeometryInterfaceStatus;
using BT = Constants.BondType;
using CT = Constants.ConnectionType;
using EL = Constants.ErrorLevel;
using Amber = Constants.Amber;
using System.Diagnostics;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>The Cleaner Static Class</summary>
/// 
/// <remarks>
/// Handles various tasks related to cleaning Geometry objects.
/// Contains static IEnumerators that perform cleaning tasks.
/// </remarks>
public static class Cleaner {

    /// <summary>Copies a Geometry (startID to targetID) then runs a list of Tasks (taskIDs) on the new Geometry</summary>    
	/// <param name="startID">GeometryInterfaceID of Geometry to copy from.</param>
    /// <param name="endID">GeometryInterfaceID of Geometry to copy to and run tasks on.</param>
    /// <param name="taskIDs">List of TaskIDs to run on endID.</param>
    public static IEnumerator GetCleanedGeometry(GIID startID, GIID targetID, List<TID> taskIDs) {

        Flow.GetGeometryInterface(targetID).activeTasks++;
        yield return Flow.CopyGeometry(startID, targetID);
        
        foreach (TID taskID in taskIDs) {
            yield return RunTask(targetID, taskID);
        }
        
        Flow.GetGeometryInterface(targetID).activeTasks--;
        Flow.GetGeometryInterface(startID).status = GIS.COMPLETED;
    }

    /// <summary>Run a Task (taskID) on a GeometryInterface (geometryInterfaceID)</summary>
	/// <param name="geometryInterfaceID">GeometryInterfaceID of Geometry to run the Task on.</param>
    /// <param name="taskID">TaskID of Task to run on geometryInterfaceID.</param>
    private static IEnumerator RunTask(GIID geometryInterfaceID, TID taskID) {
        ArrowFunctions.Task task = ArrowFunctions.GetTask(taskID);
        yield return task(geometryInterfaceID);
    }



    /// <summary>Helper delegate to copy PDBID, Amber and Partial charge information.</summary>    
    /// <remarks>Used by StandardiseWaters - won't work outside of its context.</remarks>
	/// <param name="waterResidue">Residue containing oldPDBID.</param>
    /// <param name="oldPDBID">PDBID of atom to modify.</param>
    /// <param name="newPDBID">New PDBID of atom.</param>
    delegate void AtomProcessor(
        Residue waterResidue, 
        PDBID oldPDBID, 
        PDBID newPDBID
    );
    /// <summary>Standardise the Water Residues of a GeometryInterface</summary>    
    /// <remarks>Standardises PDBIDs, AMBERs and partial charges of the atoms of each residue</remarks>
	/// <param name="geometryInterfaceID">ID of Geometry Interface to Clean.</param>
    public static IEnumerator StandardiseWaters(GIID geometryInterfaceID) {

        NotificationBar.SetTaskProgress(TID.STANDARDISE_WATERS, 0f);
        CustomLogger.LogFormat(
            EL.INFO, 
            "Standardising water residues of Geometry Interface {0}. Using Residue Name: {1}", 
            geometryInterfaceID, 
            Settings.standardWaterResidueName
        );

        Geometry geometry = Flow.GetGeometry(geometryInterfaceID);

        // Look up standard water
        AminoAcid standardWater;
        if (!Data.waterResidues.TryGetValue(Settings.standardWaterResidueName, out standardWater)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Standard Water Residue '{0}' not present in Database!",
                Settings.standardWaterResidueName
            );
            NotificationBar.ClearTask(TID.STANDARDISE_WATERS);
            yield break;
        }
        
        standardWater = Data.waterResidues[Settings.standardWaterResidueName];

        // Get the PDBIDs of standard water
        PDBID[] standardWaterPDBIDs = standardWater.GetPDBIDs(RS.WATER);

        //Create a dictionary mapping PDBID to AMBER for standard water
        Dictionary<PDBID, Amber> ambers = standardWaterPDBIDs
            .Zip(standardWater.GetAmbersFromPDBs(RS.WATER, standardWaterPDBIDs), (k, v) => new {k, v})
            .ToDictionary(x => x.k, x => x.v);

        //Create a dictionary mapping PDBID to Partial Charge for standard water
        Dictionary<PDBID, float> partialCharges = standardWaterPDBIDs
            .Zip(standardWater.GetPartialChargesFromPDBs(RS.WATER, standardWaterPDBIDs), (k, v) => new {k, v})
            .ToDictionary(x => x.k, x => x.v);

        CustomLogger.LogFormat(
            EL.VERBOSE, 
            "PDBIDs: '{0}'{3}AMBERs: '{1}'{3}Partial Charges: '{2}'", 
            string.Join("', '", standardWaterPDBIDs),
            string.Join("', '", standardWaterPDBIDs.Select(x => ambers[x])),
            string.Join("', '", standardWaterPDBIDs.Select(x => partialCharges[x])),
            FileIO.newLine
        );

        //Get the individual PDBIDs
        PDBID oxygenID = standardWaterPDBIDs.Single(x => x.element == Element.O);
        PDBID hydrogenID0 = standardWaterPDBIDs.First(x => x.element == Element.H);
        PDBID hydrogenID1 = standardWaterPDBIDs.Last(x => x.element == Element.H);

        //Create the Atom Processor
        //This will change the PDBID if necessary, as well as giving the Atom standard AMBER and Partial Charge
        //Anonymous function has access to the lookup dictionaries above
        AtomProcessor ProcessAtom = delegate(
            Residue waterResidue, 
            PDBID oldPDBID, 
            PDBID newPDBID
        ) {
            //Log all the changes that are occurring on the Atom level
            //Use a lambda function to save time if not using DEBUG mode
            CustomLogger.LogFormat(
                EL.DEBUG,
                "Residue: {0}: PDBID {1} => {2}. AMBER {3} => {4}. Charge {5} => {6}",
                () => {
                    //Using ContainsKey will make sure the user knows if there is a problematic Atom
                    //Otherwise it would just break here
                    Amber oldAmber = (waterResidue.atoms.ContainsKey(oldPDBID)) 
                        ? waterResidue.atoms[oldPDBID].amber 
                        : Amber.X;
                    string oldPartialCharge = (waterResidue.atoms.ContainsKey(oldPDBID)) 
                        ? waterResidue.atoms[oldPDBID].partialCharge.ToString()
                        : "";
                    return new object[] {
                        waterResidue.residueName,
                        oldPDBID,
                        newPDBID,
                        oldAmber,
                        ambers[newPDBID],
                        oldPartialCharge,
                        partialCharges[newPDBID]
                    };
                }
            );
            //Change the PDBID
            waterResidue.ChangePDBID(oldPDBID, newPDBID);
            //Remember to use the new PDBID to look up Atom
            Atom atom = waterResidue.atoms[newPDBID];
            //Change AMBER
            atom.amber = ambers[newPDBID];
            //Change Partial Charge
            atom.partialCharge = partialCharges[newPDBID];
        };


        //Get all the water residues in the Geometry
        IEnumerable<Residue> waterResidues = geometry.residueDict.Where(x => x.Value.isWater).Select(x => x.Value);

        //Keep track of progress
        int numProcessedResidues = 0;
        int totalResidues = waterResidues.Count();
        foreach (Residue waterResidue in waterResidues) {

            //Make sure hydrogens get different PDBIDs
            bool firstH = true;

            // Give this residue the standard name
            waterResidue.residueName = Settings.standardWaterResidueName;

            //Make PDBIDs enumerable
            List<PDBID> pdbIDs = waterResidue.pdbIDs.ToList();

            if (pdbIDs.Count() == 1) {
                //One atom in water - should be just oxygen
                PDBID pdbID = pdbIDs.First();
                if (pdbID.element != Element.O) {
                    //Throw an error if this isn't oxygen. Must be badly named or flagged
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Residue {0} is invalid for a water residue! Has one non-oxygen PDBID: {1}",
                        waterResidue.residueID,
                        pdbID
                    );
                    Flow.GetGeometryInterface(geometryInterfaceID).status = GIS.ERROR;
                    yield break;
                }
                //Process oxygen
                ProcessAtom(waterResidue, pdbID, oxygenID);
            } else if (pdbIDs.Count() == 3) {
                //3 atoms - full water residue
                foreach (PDBID pdbID in pdbIDs) {
                    PDBID newPDBID;
                    if (pdbID.element == Element.H) {
                        //Found a hydrogen 
                        if (firstH) {
                            //Use the first hydrogen PDBID
                            newPDBID = pdbIDs.Contains(hydrogenID0) ? pdbID : hydrogenID0;
                            firstH = false;
                        } else {
                            //Use the second hydrogen PDBID
                            newPDBID = pdbIDs.Contains(hydrogenID1) ? pdbID : hydrogenID1;
                        }
                    } else if (pdbID.element == Element.O) {
                        //Found an oxygen
                        newPDBID = oxygenID;
                    } else {
                        //Unrecognised PDBID - not O or H
                        CustomLogger.LogFormat(
                            EL.ERROR,
                            "Residue {0} is invalid for a water residue! Do not recognise PDBID {1} as water",
                            waterResidue.residueID,
                            pdbID
                        );
                        Flow.GetGeometryInterface(geometryInterfaceID).status = GIS.ERROR;
                        yield break;
                    }
                    //Process this water atom
                    ProcessAtom(waterResidue, pdbID, newPDBID);
                }
            } else {
                //Wrong size for water
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Wrong size for water Residue {0}! Must be 1 or 3, is {1}",
                    waterResidue.residueID,
                    waterResidue.size
                );
                Flow.GetGeometryInterface(geometryInterfaceID).status = GIS.ERROR;
                yield break;
            }

            //Feedback progress
            numProcessedResidues++;
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(TID.STANDARDISE_WATERS, (float)numProcessedResidues / totalResidues);
                yield return null;
            }
        }

        CustomLogger.LogFormat(
            EL.INFO, 
            "Standardised {0} Residue{1}", 
            totalResidues, 
            totalResidues == 1 ? "" : "s"
        );

        NotificationBar.ClearTask(TID.STANDARDISE_WATERS);
    }

    /// <summary>Ensure there are no special characters in the PDBIDs of each Atom of a Geometry.</summary>    
    /// <remarks>Also makes sure no duplicates are created by incrementing the PDBID of the new duplicate.</remarks>
	/// <param name="geometryInterfaceID">ID of Geometry Interface to Clean.</param>
    public static IEnumerator RemovePDBSpecialCharacters(GIID geometryInterfaceID) {

        NotificationBar.SetTaskProgress(TID.REMOVE_PDB_SPECIAL_CHARACTERS, 0f);
        CustomLogger.LogFormat(
            EL.INFO, 
            "Removing special characters from PDB IDs of Geometry Interface: {0}", 
            geometryInterfaceID
        );

        Geometry geometry = Flow.GetGeometry(geometryInterfaceID);

        //Track progress
        int changeCount = 0;
        int numProcessedResidues = 0;
        int totalResidues = geometry.residueDict.Count;
        foreach (KeyValuePair<ResidueID, Residue> keyValuePair in geometry.residueDict) {
            ResidueID residueID = keyValuePair.Key;

            //Create a new list - PDBIDs are being modified
            List<PDBID> pdbIDs = geometry.residueDict[residueID].pdbIDs.ToList();
            
            foreach (PDBID pdbID in pdbIDs) {
                StringBuilder newIdentifier = new StringBuilder();

                //Flag to tell whether the PDBID should be updated
                bool swap = false;

                //Loop through the characters of the original PDBID identifier
                foreach (char c in pdbID.identifier) {

                    if (char.IsLetterOrDigit(c)) {
                        //Acceptable character - keep
                        newIdentifier.Append(c);
                    } else {
                        //Not a number or letter - remove and raise flag to swap
                        swap = true;
                    }
                }

                if (swap) {

                    //PDBID has changed, so change it in the residue
                    PDBID newPDBID = new PDBID(pdbID.element, newIdentifier.ToString(), pdbID.number);

                    CustomLogger.LogFormat(
                        EL.INFO,
                        "Changing PDBID in residue {0}: {1} => {2}",
                        () => new object[] { 
                            keyValuePair.Key,
                            pdbID,
                            newPDBID
                        }
                    );
                    
                    //ChangePDBID function will take care of any duplicates created
                    geometry.residueDict[residueID].ChangePDBID(pdbID, newPDBID);
                    changeCount++;
                }
            }

            numProcessedResidues++;
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(TID.REMOVE_PDB_SPECIAL_CHARACTERS, (float)numProcessedResidues / totalResidues);
                yield return null;
            }
        }

        if (changeCount == 0) {
            CustomLogger.Log(
                EL.INFO, 
                "No PDBIDs with special characters"
            );
        }

        NotificationBar.ClearTask(TID.REMOVE_PDB_SPECIAL_CHARACTERS);
    }

    /// <summary>Opens a Popup Window prompting the user to select an individual Chain from a Geometry.</summary>    
	/// <param name="geometryInterfaceID">ID of Geometry Interface to Clean.</param>
    public static IEnumerator GetChain(GIID geometryInterfaceID) {

        NotificationBar.SetTaskProgress(TID.GET_CHAIN, 0f);
        Geometry geometry = Flow.GetGeometry(geometryInterfaceID);

        //Get all the current chainIDs
        List<string> chainIDs = geometry.GetChainIDs().ToList();

        if (chainIDs.Count == 0) {
            //Skip the routine if there are no chainIDs
            CustomLogger.Log(
                EL.INFO, 
                "No chainIDs to select - cancelling GetChain"
            );
            NotificationBar.ClearTask(TID.GET_CHAIN); 
            yield break;
        } else if (chainIDs.Count == 1) {
            //Skip the routine if there's only one chain
            CustomLogger.Log(
                EL.INFO, 
                "Already just one chain - cancelling GetChain"
            );
            NotificationBar.ClearTask(TID.GET_CHAIN); 
            yield break;
        }

        CustomLogger.LogFormat(
            EL.INFO, 
            "Extracting Chain from Geometry Interface: {0}. Available chainIDs: {1}", 
            geometryInterfaceID,
            string.Join(", ", chainIDs)
        );

        //Open Chain Selection window
        ChainSelection chainSelection = ChainSelection.main;
        chainSelection.Initialise(chainIDs);
        while (!chainSelection.userResponded) {
            yield return null;
        }

        if (chainSelection.cancelled) {
            CustomLogger.LogFormat(
                EL.INFO, 
                "Cancelled GetChain."
            );
            NotificationBar.ClearTask(TID.GET_CHAIN);
            yield break;
        }

        //Keep track of which chain was chosen - might be redundant now
        string chainID = chainIDs[chainSelection.selectedToggle];
        CustomLogger.LogFormat(
            EL.INFO, 
            "Taking Chain: {0}", 
            chainID
        );

        //Remove Residues not in chain
        int numProcessedResidues = 0;
        int totalResidues = geometry.residueDict.Count;
        List<ResidueID> residueIDs = geometry.residueDict.Keys.ToList();
        foreach (ResidueID residueID in residueIDs) {
            if (residueID.chainID != chainID) {
                geometry.residueDict.Remove(residueID);
            }

            numProcessedResidues++;
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.GET_CHAIN, 
                    CustomMathematics.Map((float)numProcessedResidues/totalResidues, 0f, 1f, 0.2f, 0.9f)
                );
                yield return null;
            }
        }

        //Remove unnecessary Missing Residue entries
        List<ResidueID> missingResidueIDs = geometry.missingResidues.Keys.ToList();
        foreach (ResidueID missingResidueID in missingResidueIDs) {
            if (missingResidueID.chainID != chainID) {
                geometry.missingResidues.Remove(missingResidueID);
            }
        }
        geometry.SetResidueDict(geometry.residueDict);
        
        NotificationBar.ClearTask(TID.GET_CHAIN);
        yield return null;
    }

    /// <summary>Run a Geometry Interface's AtomsChecker on its Geometry.</summary>
	/// <param name="geometryInterfaceID">ID of Geometry Interface to Clean.</param>
    public static IEnumerator CheckGeometry(GIID geometryInterfaceID) {
        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);

        yield return geometryInterface.CheckAll();
    }

    /// <summary>Experimental parallel connectivity calculator - unused.</summary>
	/// <param name="geometryInterfaceID">ID of Geometry Interface to compute connectivity of.</param>
    public static IEnumerator CalculateConnectivityParallel(GIID geometryInterfaceID) {
        return ParallelConnectivityCalculator.CalculateConnectivity(geometryInterfaceID);
    }

    /// <summary>Compute the connectivity of the Atoms of a Geometry Interface.</summary>
	/// <param name="geometryInterfaceID">ID of Geometry Interface to compute connectivity of.</param>
	public static IEnumerator CalculateConnectivity(GIID geometryInterfaceID) {
		//Calculate all the bonds in the system

		//First connect all standard residues internally
		//Standard residues have external connection points at N, C and SG (for CYX)
		//If they were connected anywhere else, they wouldn't be standard
		//Non-standard residues can be connected at any point

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;
		yield return DisconnectAll(geometryInterfaceID);

        NotificationBar.SetTaskProgress(TID.CALCULATE_CONNECTIVITY, 0f);
        yield return null;
        Dictionary<ResidueID, Residue> residueDict = geometryInterface.geometry.residueDict;

		List<AtomID> connectionPoints = new List<AtomID>(); 

        int numProcessedResidues = 0;
        int totalResidues = residueDict.Count;
		foreach (KeyValuePair<ResidueID, Residue> residueItem in residueDict) {

			ResidueID residueID = residueItem.Key;
			Residue residue = residueItem.Value;

			if (residue.standard) { 
				//Standard residue. Connect internally first
				//External connection points are N, C and SG
                ConnectStandard(residue, residueID, geometryInterface.geometry, connectionPoints);

			} else if (residue.state == RS.WATER) {
                //Water residue. No external connections but connect H to O if H present
				ConnectWaterResidue(residue, residueID, geometryInterface.geometry);

            } else {
                //Non-standard residue
                ConnectNonStandard(residue, residueID, geometryInterface.geometry, connectionPoints);

			}

            numProcessedResidues++;
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.CALCULATE_CONNECTIVITY, 
                    CustomMathematics.Map((float)numProcessedResidues/totalResidues, 0f, 1f, 0f, 0.3f)
                );
                yield return null;
            }
        }

        //Connections between residues
		int numConnectionPoints = connectionPoints.Count;
        
        float3[] positions = new float3[numConnectionPoints];
        Element[] elements = new Element[numConnectionPoints];
        int index = 0;
        foreach (AtomID atomID in connectionPoints) {
            positions[index] = geometryInterface.geometry.GetAtom(atomID).position;
            elements[index++] = atomID.pdbID.element;
        }

		for (int i0=0; i0<numConnectionPoints - 1; i0++) {
            Element atomicNumber0 = elements[i0];
			for (int i1=i0+1; i1<numConnectionPoints; i1++) {

				float distanceSquared = math.distancesq(positions[i0], positions[i1]);
                
				BT bondType = Data.GetBondOrderDistanceSquared(atomicNumber0, elements[i1], distanceSquared);

                if (bondType != BT.NONE) {
                    geometryInterface.geometry.Connect(connectionPoints[i0], connectionPoints[i1], bondType);
                }
			}


            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.CALCULATE_CONNECTIVITY, 
                    CustomMathematics.Map((float)i0/numConnectionPoints, 0f, 1f, 0.3f, 1f)
                );
                yield return null;
            }
		}

        geometryInterface.activeTasks--;
        NotificationBar.ClearTask(TID.CALCULATE_CONNECTIVITY);
        yield return null;
	}

    /// <summary>Compute the connectivity of the Atoms of a Geometry Interface.</summary>
	/// <param name="geometryInterfaceID">ID of Geometry Interface to compute connectivity of.</param>
	public static IEnumerator CalculateConnectivity(Geometry geometry) {
		//Calculate all the bonds in the system

		//First connect all standard residues internally
		//Standard residues have external connection points at N, C and SG (for CYX)
		//If they were connected anywhere else, they wouldn't be standard
		//Non-standard residues can be connected at any point

		yield return DisconnectAll(geometry);

        NotificationBar.SetTaskProgress(TID.CALCULATE_CONNECTIVITY, 0f);
        yield return null;
        Dictionary<ResidueID, Residue> residueDict = geometry.residueDict;

		List<AtomID> connectionPoints = new List<AtomID>(); 

        int numProcessedResidues = 0;
        int totalResidues = residueDict.Count;
		foreach (KeyValuePair<ResidueID, Residue> residueItem in residueDict) {

			ResidueID residueID = residueItem.Key;
			Residue residue = residueItem.Value;

			if (residue.standard) { 
				//Standard residue. Connect internally first
				//External connection points are N, C and SG
                ConnectStandard(residue, residueID, geometry, connectionPoints);

			} else if (residue.state == RS.WATER) {
                //Water residue. No external connections but connect H to O if H present
				ConnectWaterResidue(residue, residueID, geometry);

            } else {
                //Non-standard residue
                ConnectNonStandard(residue, residueID, geometry, connectionPoints);

			}

            numProcessedResidues++;
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.CALCULATE_CONNECTIVITY, 
                    CustomMathematics.Map((float)numProcessedResidues/totalResidues, 0f, 1f, 0f, 0.3f)
                );
                yield return null;
            }
        }

        //Connections between residues
		int numConnectionPoints = connectionPoints.Count;
        
        float3[] positions = new float3[numConnectionPoints];
        Element[] elements = new Element[numConnectionPoints];
        int index = 0;
        foreach (AtomID atomID in connectionPoints) {
            positions[index] = geometry.GetAtom(atomID).position;
            elements[index++] = atomID.pdbID.element;
        }

		for (int i0=0; i0<numConnectionPoints - 1; i0++) {
            Element atomicNumber0 = elements[i0];
			for (int i1=i0+1; i1<numConnectionPoints; i1++) {

				float distanceSquared = math.distancesq(positions[i0], positions[i1]);
                
				BT bondType = Data.GetBondOrderDistanceSquared(atomicNumber0, elements[i1], distanceSquared);

                if (bondType != BT.NONE) {
                    geometry.Connect(connectionPoints[i0], connectionPoints[i1], bondType);
                }
			}


            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.CALCULATE_CONNECTIVITY, 
                    CustomMathematics.Map((float)i0/numConnectionPoints, 0f, 1f, 0.3f, 1f)
                );
                yield return null;
            }
		}
	}

    private static void ConnectWaterResidue(Residue residue, ResidueID residueID, Geometry geometry) {
        List<PDBID> pdbIDs = residue.pdbIDs.ToList();
        if (pdbIDs.Count > 1) { //Only calculate waters with hydrogens

            //Get the AtomID of the water Oxygen
            PDBID oPDBID = pdbIDs.Single(x => x.element == Element.O);
            AtomID oAtomID = new AtomID(residueID, oPDBID);

            //Loop through all PDBIDs
            foreach (PDBID pdbID1 in pdbIDs) {
                //Check if Hydrogen
                if (pdbID1.element == Element.H) {
                    //Connect all Hydrogens to Oxygens
                    AtomID hAtomID = new AtomID(residueID, pdbID1);
                    geometry.Connect(oAtomID, hAtomID, BT.SINGLE);
                }
            }
        }
    }

    private static void ConnectStandard(Residue residue, ResidueID residueID, Geometry geometry, List<AtomID> connectionPoints) {
        List<PDBID> pdbIDs = residue.pdbIDs.ToList();

        foreach (PDBID pdbID0 in pdbIDs) {
            Atom atom0 = residue.atoms[pdbID0];
            AtomID atomID0 = new AtomID(residueID, pdbID0);
            float3 p0 = atom0.position;

            //Check if this PDBID is a connection point
            CT connectionType = Settings.standardConnectionDict
                .Where(x => pdbID0.TypeEquals(x.Key))
                .FirstOrDefault()
                .Value;

            if (connectionType != CT.NULL) {
                //Connection point - add to list and flag Atom as a connection point
                connectionPoints.Add(new AtomID(residueID, pdbID0));
                atom0.connectionType = connectionType;
            }

            //Use distances to connect within the residue
            foreach (PDBID pdbID1 in pdbIDs) {
                if (pdbID0 == pdbID1) {
                    continue;
                }
                Atom atom1 = residue.atoms[pdbID1];
                float3 p1 = atom1.position;

                //Calculate the distance squared
                float distanceSquared = math.distancesq(p0, p1);
                BT bondType = Data.GetBondOrderDistanceSquared(
                    pdbID0.element, 
                    pdbID1.element, 
                    distanceSquared
                );
                if (bondType != BT.NONE) {
                    //Connect atoms
                    AtomID atomID1 = new AtomID(residueID, pdbID1);
                    geometry.Connect(atomID0, atomID1, bondType);
                }
            }

            //If this is a hydrogen and it has no connections, join to nearest non-H atom.
            if (pdbID0.element == Element.H) {
                PDBID pdbID1 = residue.atoms
                    .Where(kvp => kvp.Key.element != Element.H)
                    .ToList()
                    .OrderBy(x => CustomMathematics.GetDistance(atom0, x.Value))
                    .First()
                    .Key;
                
                atom0.internalConnections[pdbID1] = BT.SINGLE;
                residue.atoms[pdbID1].internalConnections[pdbID0] = BT.SINGLE;
            }
        }
    }

    private static void ConnectNonStandard(Residue residue, ResidueID residueID, Geometry geometry, List<AtomID> connectionPoints) {
        List<PDBID> pdbIDs = residue.pdbIDs.ToList();
        List<PDBID> nonStandardConnectionPoints = residue.pdbIDs
            .Where(x => Settings.nonStandardConnections.Contains(x.element))
            .ToList();
        foreach (PDBID pdbID0 in nonStandardConnectionPoints) {

            AtomID atomID0 = new AtomID(residueID, pdbID0);
            //Atom atom0 = residue.atoms[pdbID0];
            float3 p0 = residue.atoms[pdbID0].position;

            residue.atoms[pdbID0].connectionType = CT.OTHER_VALENT;
            connectionPoints.Add(atomID0);

            foreach (PDBID pdbID1 in pdbIDs) {
                AtomID atomID1 = new AtomID(residueID, pdbID1);
                if (pdbID0 == pdbID1) {
                    continue;
                }
                Atom atom1 = residue.atoms[pdbID1];
                float3 p1 = atom1.position;

                float distanceSquared = math.distancesq(p0, p1);
                BT bondType = Data.GetBondOrderDistanceSquared(
                    pdbID0.element, 
                    pdbID1.element, 
                    distanceSquared
                );
                geometry.Connect(atomID0, atomID1, bondType);
            }
        }
    }

	public static IEnumerator DisconnectAll(GIID geometryInterfaceID) {
        
        NotificationBar.SetTaskProgress(TID.CLEAR_CONNECTIVITY, 0f);

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;

        int numProcessedResidues = 0;
        int totalResidues = geometryInterface.geometry.residueDict.Count;
		foreach (KeyValuePair<ResidueID, Residue> residueItem in geometryInterface.geometry.residueDict) {

			List<PDBID> pdbIDs = residueItem.Value.pdbIDs.ToList();
			foreach (PDBID pdbID0 in pdbIDs) {
                Atom atom = geometryInterface.geometry.residueDict[residueItem.Key].atoms[pdbID0];
				atom.internalConnections.Clear();
				atom.externalConnections.Clear();
			}

            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.CLEAR_CONNECTIVITY, 
                    (float)numProcessedResidues/totalResidues
                );
                yield return null;
            }
		}

        geometryInterface.activeTasks--;
        NotificationBar.ClearTask(TID.CLEAR_CONNECTIVITY);
	}

	public static IEnumerator DisconnectAll(Geometry geometry) {
        
        int numProcessedResidues = 0;
        int totalResidues = geometry.residueDict.Count;
		foreach (KeyValuePair<ResidueID, Residue> residueItem in geometry.residueDict) {

			List<PDBID> pdbIDs = residueItem.Value.pdbIDs.ToList();
			foreach (PDBID pdbID0 in pdbIDs) {
                Atom atom = geometry.residueDict[residueItem.Key].atoms[pdbID0];
				atom.internalConnections.Clear();
				atom.externalConnections.Clear();
			}

            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.CLEAR_CONNECTIVITY, 
                    (float)numProcessedResidues/totalResidues
                );
                yield return null;
            }
		}
	}

    public static IEnumerator FillMissingResidues(GIID geometryInterfaceID) {

        yield return(AmberCalculator.CalculateAMBERTypes(geometryInterfaceID));

        NotificationBar.SetTaskProgress(TID.FILL_MISSING_RESIDUES, 0f);

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;

        Geometry geometry = geometryInterface.geometry;

        //Task only needed if there are Missing Residues
        if (geometry.missingResidues != null && geometry.missingResidues.Count > 0) {
            //Use MissingResidueTools to enumerate segments of missing Residues
            MissingResidueTools mrt = GameObject.FindObjectOfType<MissingResidueTools>();

            CustomLogger.LogFormat(
                EL.INFO,
                "Missing Residues found in Geometry Interface: {0}",
                geometryInterfaceID
            );

            //Loop through each segment
            foreach (
                (
                    Residue startResidue, 
                    Residue endResidue, 
                    List<string> missingResidueNames,
                    List<ResidueID> missingResidueIDs
                ) in mrt.EnumerateMissingSegments(geometry)
            ) {

                //Log the segment for the user
                CustomLogger.LogFormat(
                    EL.INFO,
                    "{0} - ({1}) - {2}",
                    startResidue != null ? string.Format("{0}({1})", startResidue.residueID, startResidue.residueName) : ">",
                    string.Join(", ", missingResidueNames),
                    endResidue != null ? string.Format("{0}({1})", endResidue.residueID, endResidue.residueName) : "<"
                );

                if (startResidue == null) {
                    //This segment has no start Residue so it hangs off the start of a chain

                    if (endResidue == null) {
                        //No end Residue either means this segment can't be placed anywhere
                        CustomLogger.LogFormat(
                            EL.ERROR,
                            "No start or end Residue to attach Missing Residues to."
                        );
                    }

                    //UnityEngine.Debug.LogFormat("End {0} {1}", endResidue.GetResidueID(), endResidue.GetCentre());

                    //This is the residue to attach to
                    //It will be updated in the case of segments longer than 1
                    Residue terminalResidue = endResidue;

                    //Loop through segment backwards
                    for (
                        int missingResidueIndex = missingResidueNames.Count - 1; 
                        missingResidueIndex > -1; 
                        missingResidueIndex--
                    ) {
                        //If the terminalResidue is null, the segment has finished
                        if (terminalResidue == null) {
                            break;
                        }

                        //Get the Residue ID of the Missing Residue
                        ResidueID missingResidueID = missingResidueIDs[missingResidueIndex];

                        //Append a Residue and make it the new Terminal Residue
                        terminalResidue = terminalResidue.AddNeighbourResidue(
                            missingResidueNames[missingResidueIndex], 
                            new PDBID(Element.N), 
                            missingResidueID
                        );

                        UnityEngine.Debug.LogFormat("{0} {1}", missingResidueID, terminalResidue.GetCentre());

                    }
                } else if (endResidue == null) {
                    //This segment has no end Residue so it hangs off the end of a chain

                    //UnityEngine.Debug.LogFormat("Start {0} {1}", startResidue.GetResidueID(), startResidue.GetCentre());

                    //This is the residue to attach to
                    //It will be updated in the case of segments longer than 1
                    Residue terminalResidue = startResidue;

                    //Loop through segment forwards
                    for (
                        int missingResidueIndex = 0; 
                        missingResidueIndex < missingResidueNames.Count; 
                        missingResidueIndex++
                    ) {
                        //If the terminalResidue is null, the segment has finished
                        if (terminalResidue == null) {
                            break;
                        }

                        //Get the Residue ID of the Missing Residue
                        ResidueID missingResidueID = missingResidueIDs[missingResidueIndex];

                        //Append a Residue and make it the new Terminal Residue
                        terminalResidue = terminalResidue.AddNeighbourResidue(
                            missingResidueNames[missingResidueIndex], 
                            new PDBID(Element.C), 
                            missingResidueID
                        );

                        //UnityEngine.Debug.LogFormat("{0} {1}", missingResidueID, terminalResidue.GetCentre());

                    }
                } else {
                    //This segment sits between two Residues, so it must connect them
                    CustomLogger.LogFormat(
                        EL.INFO,
                        "Filling Missing Residues between '{0}' and '{1}'",
                        startResidue.residueID,
                        endResidue.residueID
                    );
                    mrt.JoinResidues(startResidue, endResidue, missingResidueIDs, missingResidueNames);

                }

                if (Timer.yieldNow) {
                    yield return null;
                }
            }

        } else {
            CustomLogger.LogFormat(
                EL.INFO,
                "No Missing Residues for Geometry Interface: {0}",
                geometryInterfaceID
            );
        }

        geometryInterface.activeTasks--;

        NotificationBar.ClearTask(TID.FILL_MISSING_RESIDUES);
        yield return null;
    }

    public static IEnumerator OptimiseMissingResidues(GIID geometryInterfaceID) {

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;

        Geometry geometry = geometryInterface.geometry;

        float originalMaxNonBondingCutoff = Settings.maxNonBondingCutoff;
        Settings.maxNonBondingCutoff = 10f;

        List<ResidueID> missingResidueIDs = geometry.missingResidues.Keys.ToList();
        //UnityEngine.Debug.Log(string.Join(" ", missingResidueIDs));

        Graph graph = new Graph();

        yield return graph.SetGeometry(geometry, missingResidueIDs);

        //UnityEngine.Debug.Log(graph.stretches.Count);
        //UnityEngine.Debug.Log(graph.bends.Count);
        //UnityEngine.Debug.Log(graph.torsions.Count);
        //UnityEngine.Debug.Log(graph.impropers.Count);
        //UnityEngine.Debug.Log(graph.nonBondings.Count);

        
        float3[] forces = new float3[graph.numNearbyAtoms];

        if (forces.Length == 0) {
            geometryInterface.activeTasks--;
            yield break;
        }

        NotificationBar.SetTaskProgress(TID.OPTIMISE_MISSING_RESIDUES, 0f);

        Plotter plotter = Plotter.main;


        float targetRMS = 0.01f;
        float targetMax = 0.015f;

        plotter.AddHorizontalLine(
            new Color(0.1f, 0.1f, 0.6f), 
            new Color(0.1f, 0.1f, 0.6f), 
            targetRMS
        );
        plotter.AddHorizontalLine(
            new Color(0.6f, 0.1f, 0.1f), 
            new Color(0.6f, 0.1f, 0.1f), 
            targetMax
        );

        int maxPlotNum = plotter.AddAxis(Color.red, Color.red);
        int rmsPlotNum = plotter.AddAxis(Color.blue, Color.blue);

        int steps = 5000;

        plotter.Show();

        for (int i=0; i<steps; i++) {
            forces = graph.ComputeForces();

            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.OPTIMISE_MISSING_RESIDUES, 
                    CustomMathematics.Map(i, 0, steps, 0, 1)
                );
                yield return null;
            }

            float maxForce = forces.Max(x => math.length(x));
            float rmsForce = CustomMathematics.RMS(forces);

            if (float.IsNaN(maxForce) || float.IsNaN(rmsForce)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Optimisation failed on forces! See Project Log for details."
                );

                graph.ReportBadTerms();

				break;
            }

            plotter.AddPoint(maxPlotNum, maxForce);
            plotter.AddPoint(rmsPlotNum, rmsForce);

            graph.TakeStep(forces, 0.1f, 0.2f);            

            if (maxForce < targetMax && rmsForce < targetRMS) {
                break;
            }
            
        }

        Settings.maxNonBondingCutoff = originalMaxNonBondingCutoff;

        plotter.Hide();

        geometryInterface.activeTasks--;
        NotificationBar.ClearTask(TID.OPTIMISE_MISSING_RESIDUES);

    }



    //Tools

    //PDB TOOLS

    //PDB string is composed of 4 sections to make a length 4 string:
    //
    //Left pad (optional): spaces (length 0-1)
    //Identifier (required): number (length 0-1), letters (1-3)
    //Number (optional): number (length 0-1)
    //Right pad (optional): spaces (length 0-2)
    //
    // e.g. Aspartic acid with invalid Oxygen_D names:
    // " C  ", " CA ", " CB ", " CG ", " N  ", " O  ", " OD1", " OD1"
    
    // StripPDBNumber(string pdb)
    // Remove the Number part of pdb
    // Aspartic acid would become:
    // " C  ", " CA ", " CB ", " CG ", " N  ", " O  ", " OD ", " OD "
    // This allows the correct numbers to be assigned with MakePDBsUnique
    
    // UniquatePDBs(string[] pdbs, List<int> atomNums)
    // Increment PDB number if duplicates found
    // Aspartic acid would become:
    // " C  ", " CA ", " CB ", " CG ", " N  ", " O  ", " OD1", " OD2"
    
    // StripPDBSpecialCharacters
    // Remove symbols from PDBs that would otherwise break external programs
    // " HD'" -> " HD "
    // Automatically checks for new duplicates and flags them
    // Advisable to call MakePDBsUnique on a residue after this


    public static IEnumerator CalculateInternalConnectivity(Geometry geometry, ResidueID residueID) {

        Residue residue = geometry.residueDict[residueID];
        List<PDBID> pdbIDs = residue.pdbIDs.ToList();

        foreach (PDBID pdbID0 in pdbIDs) {
            Atom atom0 = residue.atoms[pdbID0];
            AtomID atomID0 = new AtomID(residueID, pdbID0);
            float3 p0 = atom0.position;

            foreach ((PDBID pdbID1, CT connectionType) in Settings.standardConnectionDict) {
                if (
                    pdbID0.identifier == pdbID1.identifier &&
                    pdbID0.element == pdbID1.element
                ) {
                    atom0.connectionType = connectionType;
                }
            }

            //Use distances to connect within the residue
            foreach (PDBID pdbID1 in pdbIDs) {

                if (pdbID0 == pdbID1) {
                    continue;
                }
                Atom atom1 = residue.atoms[pdbID1];
                float3 p1 = atom1.position;

                float distanceSquared = math.distancesq(p0, p1);
                BT bondType = Data.GetBondOrderDistanceSquared(
                    pdbID0.element, 
                    pdbID1.element, 
                    distanceSquared
                );
                atom0.internalConnections[pdbID1] = bondType;
                residue.atoms[pdbID1].internalConnections[pdbID0] = bondType;
            }
            yield return null;

            //If this is a hydrogen and it has no connections, join to nearest non-H atom.
            if (pdbID0.element == Element.H) {
                PDBID pdbID1 = residue.atoms
                    .Where(kvp => kvp.Key.element != Element.H)
                    .ToList()
                    .OrderBy(x => CustomMathematics.GetDistance(atom0, x.Value))
                    .First()
                    .Key;
                
                atom0.internalConnections[pdbID1] = BT.SINGLE;
                residue.atoms[pdbID1].internalConnections[pdbID0] = BT.SINGLE;
            }
        }

    }

	private static void DisconnectInternal(Geometry geometry, ResidueID residueID) {

        foreach (PDBID pdbID0 in geometry.residueDict[residueID].pdbIDs) {
            geometry.residueDict[residueID].atoms[pdbID0].internalConnections.Clear();
        }
	}
    private static void UniquatePDBs(string[] pdbs, List<int> atomNums) {
        Dictionary<string, int> pdbNameCountDict = new Dictionary<string, int>();
        List<string> uniquePDBs = new List<string>();
        List<string> strippedPDBs = new List<string>();

        for (int i = 0; i < atomNums.Count; i++) {
            int atomNum = atomNums[i];

            string strippedPDB = StripPDBNumber(pdbs[atomNum]);
            strippedPDBs.Add(strippedPDB);

            if (uniquePDBs.Contains(strippedPDB)) {
                pdbNameCountDict[strippedPDB] += 1;
            } else {
                pdbNameCountDict[strippedPDB] = 1;
                uniquePDBs.Add(strippedPDB);
            }

            int count = pdbNameCountDict[strippedPDB];
            if (count > 1) {

                if (count == 2) {
                    for (int j = 0; j < i; j++) {
                        if (strippedPDBs[j] == strippedPDB) {
                            pdbs[atomNums[j]] = GetNumberedPDB(strippedPDBs[j], 1);
                            break;
                        }
                    }
                }
                pdbs[atomNum] = GetNumberedPDB(strippedPDB, count);
            }
        }
    }

    private static void RemovePDBSpecialCharacters(string[] pdbs, List<int> atomNums, out bool duplicates) {
        duplicates = false;
        List<string> uniquePDBs = new List<string>();

        for (int i = 0; i < atomNums.Count; i++) {
            int atomNum = atomNums[i];
            pdbs[atomNum] = StripPDBSpecialCharacters(pdbs[atomNum]);
            if (uniquePDBs.Contains(pdbs[atomNum])) {
                duplicates = true;
            } else {
                uniquePDBs.Add(pdbs[atomNum]);
            }
        }
    }

    private static string StripPDBNumber(string pdb) {

        StringBuilder sb = new StringBuilder();
        bool atIdentifier = false;
        
        for (int i = 0; i < 4; i++) {
            char c = pdb[i];
            if (atIdentifier) {
                if (char.IsDigit(c)) {
                    sb.Append(' ');
                } else {
                    sb.Append(c);
                }
            } else {
                if (char.IsLetter(c)) {
                    atIdentifier = true;
                }
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string StripPDBSpecialCharacters(string pdb) {

        StringBuilder sb = new StringBuilder();
        bool atIdentifier = false;
        
        for (int i = 0; i < 4; i++) {
            char c = pdb[i];

            if (atIdentifier) {
                if (char.IsLetterOrDigit(c)) {
                    sb.Append(c);
                }
            } else {
                if (char.IsLetter(c)) {
                    atIdentifier = true;
                }
                sb.Append(c);
            }
        }

        return sb.ToString().PadRight(4);
    }

    private static string GetNumberedPDB(string pdb, int number) {

        StringBuilder sb = new StringBuilder();
        bool atIdentifier = false;

        //Loop through string. Comments give a guide as to where the 
        // function is in the string with two examples " OD " and "1HH "
        for (int i = 0; i < 4; i++) {
            char c = pdb[i];
            if (atIdentifier) {
                if (c == ' ') { // " OD[ ]", "1HH[ ]"
                    sb.Append(number.ToString());
                    break;
                } else { // " [OD] ", "1[HH] "
                    sb.Append(c);
                }
            } else {
                if (char.IsLetter(c)) { // "1[HH] "
                    sb.Append(c);
                    atIdentifier = true;
                } else { // "[ ]OD ", "[1]HH "
                    sb.Append(c);
                }
            }
        }

        return sb.ToString().PadRight(4);
        
    }

}





// Experimental parallel connectivity calculator
// Unity doesn't allow secondary threads to survive more than 4 frames so this throws warnings
// The bottle neck is computing n^2 links across all connection points, so putting locks on connection points slows this down too much
static class ParallelConnectivityCalculator {

	static List<AtomID> connectionPoints; 
    static List<ResidueID> residueIDs;
    static GIID geometryInterfaceID;
    static int numProcessedResidues;
    static int numProcessedConnectionPoints;
    static int numConnectionPoints;

    static GeometryInterface geometryInterface;

    public static IEnumerator CalculateConnectivity(GIID geometryInterfaceID) {

        geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;

        ParallelConnectivityCalculator.geometryInterfaceID = geometryInterfaceID;

        connectionPoints = new List<AtomID>();

        residueIDs = geometryInterface.geometry.residueDict.Keys.ToList();
        int totalResidues = residueIDs.Count();

        //Process internal connections
        numProcessedResidues = 0;
        IntraConnectivityJob intra = new IntraConnectivityJob();
        JobHandle intraJobHandle = intra.Schedule(totalResidues, 4);
        while (!intraJobHandle.IsCompleted) {
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.CALCULATE_CONNECTIVITY, 
                    CustomMathematics.Map((float)numProcessedResidues/totalResidues, 0f, 1f, 0f, 0.3f)
                );
                yield return null;
            }
        }
        JobHandle.ScheduleBatchedJobs();
        intraJobHandle.Complete();

        numProcessedConnectionPoints = 0;
        numConnectionPoints = connectionPoints.Count;

        //Process external connections
        InterConnectivityJob inter = new InterConnectivityJob();
        JobHandle interJobHandle = inter.Schedule(numConnectionPoints, 4);
        while (!interJobHandle.IsCompleted) {
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.CALCULATE_CONNECTIVITY, 
                    CustomMathematics.Map((float)numProcessedConnectionPoints/numConnectionPoints, 0f, 1f, 0.3f, 1f)
                );
                yield return null;
            }
        }
        JobHandle.ScheduleBatchedJobs();
        interJobHandle.Complete();
        NotificationBar.ClearTask(TID.CALCULATE_CONNECTIVITY);
        geometryInterface.activeTasks--;
    }


    private struct IntraConnectivityJob : IJobParallelFor {

        public void Execute(int residueNumber) {
            ResidueID residueID = residueIDs[residueNumber];
            Residue residue = Flow.GetGeometry(geometryInterfaceID).residueDict[residueID];

            if (residue.standard) {
                ProcessStandard(residueID, residue);
            } else if (residue.state == RS.WATER) {
                ProcessWater(residueID, residue);
            } else {
                ProcessNonStandard(residueID, residue);
            }
            numProcessedResidues++;
        }

        private void ProcessStandard(ResidueID residueID, Residue residue) {
            //Standard residue. Connect internally first.
            //External connection points are N, C and SG
            List<PDBID> pdbIDs = residue.pdbIDs.ToList();

            foreach (PDBID pdbID0 in pdbIDs) {
                Atom atom0 = residue.atoms[pdbID0];
                AtomID atomID0 = new AtomID(residueID, pdbID0);
                float3 p0 = atom0.position;

                foreach (KeyValuePair<PDBID, CT> keyValuePair in Settings.standardConnectionDict) {
                    if (
                        pdbID0.identifier == keyValuePair.Key.identifier &&
                        pdbID0.element == keyValuePair.Key.element
                    ) {
                        lock (connectionPoints) {
                            connectionPoints.Add(new AtomID(residueID, pdbID0));
                        }
                        
                        atom0.connectionType = keyValuePair.Value;
                    }
                }

                //Use distances to connect within the residue
                foreach (PDBID pdbID1 in pdbIDs) {
                    AtomID atomID1 = new AtomID(residueID, pdbID1);
                    if (pdbID0 == pdbID1) {
                        continue;
                    }
                    Atom atom1 = residue.atoms[pdbID1];
                    float3 p1 = atom1.position;

                    float distanceSquared = math.distancesq(p0, p1);
                    BT bondType = Data.GetBondOrderDistanceSquared(
                        pdbID0.element, 
                        pdbID1.element, 
                        distanceSquared
                    );
                    geometryInterface.geometry.Connect(atomID0, atomID1, bondType);
                }
            }
        }

        private void ProcessWater(ResidueID residueID, Residue residue) {
            //Water residue. No external connections but connect H to O if H present
            List<PDBID> pdbIDs = residue.pdbIDs.ToList();
            if (pdbIDs.Count == 1) return; //Only calculate waters with hydrogens

            PDBID pdbID0 = pdbIDs.Single(x => x.element == Element.O);
            AtomID atomID0 = new AtomID(residueID, pdbID0);
            Atom atom0 = residue.atoms[pdbID0];

            foreach (PDBID pdbID1 in pdbIDs) {
                if (pdbID1.element == Element.H) {
                    AtomID atomID1 = new AtomID(residueID, pdbID1);
                    Atom atom1 = residue.atoms[pdbID1];

                    geometryInterface.geometry.Connect(atomID0, atomID1, BT.SINGLE);
                }
            }

        }

        private void ProcessNonStandard(ResidueID residueID, Residue residue) {
            //Non-standard residue
            List<PDBID> pdbIDs = residue.pdbIDs.ToList();
            foreach (PDBID pdbID0 in pdbIDs) {
                foreach (Element connectionElement in Settings.nonStandardConnections) {
                    if (pdbID0.element == connectionElement) {

                        Atom atom0 = residue.atoms[pdbID0];
                        AtomID atomID0 = new AtomID(residueID, pdbID0);
                        float3 p0 = atom0.position;

                        atom0.connectionType = CT.OTHER_VALENT;
                        lock (connectionPoints) {
                            connectionPoints.Add(new AtomID(residueID, pdbID0));
                        }

                        foreach (PDBID pdbID1 in pdbIDs) {
                            AtomID atomID1 = new AtomID(residueID, pdbID1);
                            if (pdbID0 == pdbID1) {
                                continue;
                            }
                            Atom atom1 = residue.atoms[pdbID1];
                            float3 p1 = atom1.position;

                            float distanceSquared = math.distancesq(p0, p1);
                            BT bondType = Data.GetBondOrderDistanceSquared(
                                pdbID0.element, 
                                pdbID1.element, 
                                distanceSquared
                            );
                            geometryInterface.geometry.Connect(atomID0, atomID1, bondType);
                        }
                    }
                }
                
            }

        }
    }

    private struct InterConnectivityJob : IJobParallelFor {

        public void Execute(int atomNum) {

            AtomID atomID0 = connectionPoints[atomNum];

            float3 p0 = geometryInterface.geometry.GetAtom(atomID0).position;

            for (int i1=atomNum+1; i1<numConnectionPoints; i1++) {
                AtomID atomID1 = connectionPoints[i1];
                if (atomID0.residueID == atomID1.residueID) continue;

                Atom atom1;
                if (geometryInterface.geometry.TryGetAtom(atomID1, out atom1)) {
                    float3 p1 = atom1.position;
                    float distanceSquared = math.distancesq(p0, p1);
                    BT bondType = Data.GetBondOrderDistanceSquared(atomID0.pdbID.element, atomID1.pdbID.element, distanceSquared);
                    geometryInterface.geometry.Connect(atomID0, atomID1, bondType);
                }
            }
            numProcessedConnectionPoints++;
        
        }

    }

    private static Residue TakeResidueAndDisconnectCaps(Residue residue, Geometry newGeometry, Geometry oldGeometry) {
        Residue newResidue = residue.Take(newGeometry);
        newResidue.atoms = newResidue.atoms
            .ToDictionary(
                x => x.Key, 
                x => DisconnectCaps(x.Value, oldGeometry)
            );

        return newResidue;
    }
    
    private static Atom DisconnectCaps(Atom atom, Geometry geometry) {
        atom.externalConnections = atom.externalConnections
            .Where(x => geometry.residueDict[x.Key.residueID].state != RS.CAP)
            .ToDictionary(x => x.Key, x => x.Value);
        return atom;
    }

}