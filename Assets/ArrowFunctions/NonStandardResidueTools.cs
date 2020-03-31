using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Element = Constants.Element;
using TID = Constants.TaskID;
using GIID = Constants.GeometryInterfaceID;
using GIS = Constants.GeometryInterfaceStatus;
using RS = Constants.ResidueState;
using BT = Constants.BondType;
using EL = Constants.ErrorLevel;

/// <summary>The Non-Standard Residue Tools Static Class</summary>
/// 
/// <remarks>
/// Handles various tasks related to Non-Standard and Standard Residues.
/// Contains static IEnumerators that perform tasks.
/// </remarks>

public static class NonStandardResidueTools {

    /// <summary>Copies a Geometry (startID to targetID) then runs a list of Tasks (taskIDs) on the new Atoms</summary>    
	/// <param name="startID">GeometryInterfaceID of Geometry to copy from.</param>
    /// <param name="endID">GeometryInterfaceID of Geometry to copy to and run tasks on.</param>
    /// <param name="taskIDs">List of TaskIDs to run on endID.</param>
    public static IEnumerator GetStandardResidues(GIID startID, GIID targetID, List<TID> taskIDs) {
        
        Flow.GetGeometryInterface(targetID).activeTasks++;
        yield return Flow.CopyGeometry(startID, targetID);
        
        foreach (TID taskID in taskIDs) {
            yield return RunTask(targetID, taskID);
        }

        Flow.GetGeometryInterface(targetID).activeTasks--;
        Flow.GetGeometryInterface(startID).status = GIS.COMPLETED;

    }

    /// <summary>Copies a Geometry (startID to targetID) then runs a list of Tasks (taskIDs) on the new Atoms</summary>    
	/// <param name="startID">GeometryInterfaceID of Geometry to copy from.</param>
    /// <param name="endID">GeometryInterfaceID of Geometry to copy to and run tasks on.</param>
    /// <param name="taskIDs">List of TaskIDs to run on endID.</param>
    public static IEnumerator GetNonStandardResidues(GIID startID, GIID targetID, List<TID> taskIDs) {
        
        Flow.GetGeometryInterface(targetID).activeTasks++;

        yield return Flow.CopyGeometry(startID, targetID);
        
        foreach (TID taskID in taskIDs) {
            yield return RunTask(targetID, taskID);
        }

        Flow.GetGeometryInterface(targetID).activeTasks--;
        Flow.GetGeometryInterface(startID).status = GIS.COMPLETED;

    }

    /// <summary>Copies a Geometry (startID to targetID) then runs a list of Tasks (taskIDs) on the new Atoms</summary>    
	/// <param name="startID">GeometryInterfaceID of Geometry to copy from.</param>
    /// <param name="endID">GeometryInterfaceID of Geometry to copy to and run tasks on.</param>
    /// <param name="taskIDs">List of TaskIDs to run on endID.</param>
    public static IEnumerator MergeGeometries(GIID startID, GIID targetID, List<TID> taskIDs) {

        Flow.main.geometryDict[targetID].activeTasks++;
        
        foreach (TID taskID in taskIDs) {
            yield return RunTask(targetID, taskID);
        }
        
        Flow.main.geometryDict[targetID].activeTasks--;
    }

    /// <summary>Run a Task (taskID) on a GeometryInterface (geometryInterfaceID)</summary>
	/// <param name="geometryInterfaceID">GeometryInterfaceID of Geometry to run the Task on.</param>
    /// <param name="taskID">TaskID of Task to run on geometryInterfaceID.</param>
    private static IEnumerator RunTask(GIID geometryInterfaceID, TID taskID) {
        ArrowFunctions.Task task = ArrowFunctions.GetTask(taskID);
        yield return task(geometryInterfaceID);
    }

    /// <summary>Isolates the Non-Standard Residues from an Atoms object and caps the dangling bonds.</summary>
    /// <param name="geometryInterfaceID">The ID of the Geometry Interface containing the Atoms object</param>
    public static IEnumerator GetNonStandardResidues(GIID geometryInterfaceID) {


        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;

        NotificationBar.SetTaskProgress(TID.GET_NSRS, 0f);
        yield return null;

        //Get all ResidueIDs whose Residues are marked as Non-Standard
        List<ResidueID> nsrIDs = geometryInterface.geometry
            .EnumerateResidues(x => !x.standard && !x.isWater)
            .Select(x => x.Item1)
            .ToList();

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Separating out NSR Residue IDs: {0}",
            string.Join(", ", nsrIDs)
        );
        
        NotificationBar.SetTaskProgress(TID.GET_NSRS, 0.1f);
        yield return null;

        //Extract and Cap Residues
        yield return GetCappedAtoms(nsrIDs, geometryInterfaceID, TID.GET_NSRS);

        NotificationBar.ClearTask(TID.GET_NSRS);
        geometryInterface.activeTasks--;
    }

    public static IEnumerator GetStandardResidues(GIID geometryInterfaceID) {

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        geometryInterface.activeTasks++;

        NotificationBar.SetTaskProgress(TID.GET_SRS, 0f);
        yield return null;

        //Get all ResidueIDs whose Residues are marked as Standard
        List<ResidueID> srIDs = geometryInterface.geometry
            .EnumerateResidues(x => x.standard || x.isWater)
            .Select(x => x.Item1)
            .ToList();

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Separating out SR Residue IDs: {0}",
            string.Join(", ", srIDs)
        );
        
        NotificationBar.SetTaskProgress(TID.GET_SRS, 0.1f);
        yield return null;

        //Extract and Cap Residues
        yield return GetCappedAtoms(srIDs, geometryInterfaceID, TID.GET_SRS);

        NotificationBar.ClearTask(TID.GET_SRS);
        geometryInterface.activeTasks--;
    }

    /// <summary>Opens a Popup Window allowing manual selection of Non-Standard Residues.</summary>    
    /// <remarks>Also allows flagging of special residues such as Caps.</remarks>
	/// <param name="geometryInterfaceID">ID of Geometry Interface to Clean.</param>
    public static IEnumerator SelectNonStandardResidues(GIID geometryInterfaceID) {

        NotificationBar.SetTaskProgress(TID.SELECT_NONSTANDARD_RESIDUES, 0f);
        CustomLogger.LogFormat(
            EL.INFO, 
            "Manual setting of Residues of Geometry Interface: {0}.", 
            geometryInterfaceID
        );

        Geometry geometry = Flow.GetGeometry(geometryInterfaceID);

        //Open the Non-Standard Residue Selection window
        NonStandardResidueSelection nsrSelection = NonStandardResidueSelection.main;
        yield return nsrSelection.Initialise(geometry);

        //Wait for response
        while (!nsrSelection.userResponded) {
            yield return null;
        }

        if (!nsrSelection.cancelled) {
            //User didn't cancel
            //Loop through all the residues that have been changed
            foreach ((ResidueID residueID , RS newState) in nsrSelection.changesDict) {

                Residue residue;
                if (!geometry.TryGetResidue(residueID, out residue)) {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Could not change Residue State of {0} - Residue not found!",
                        residueID
                    );
                    continue;
                }

                CustomLogger.LogFormat(
                    EL.INFO,
                    "Changing Residue State of {0}: {1} => {2}",
                    () => new object[] { 
                        residueID,
                        Constants.ResidueStateMap[residue.state],
                        Constants.ResidueStateMap[newState]
                    }
                );

                //Set the States of the Geometry's Residues based on the user's choice
                residue.state = newState;
            }

            if (nsrSelection.changesDict.Count == 0) {
                CustomLogger.Log(
                    EL.INFO, 
                    "No Residue States to change"
                );
            }
        } else {
            CustomLogger.LogFormat(
                EL.INFO, 
                "Cancelled."
            );
        }
        NotificationBar.ClearTask(TID.SELECT_NONSTANDARD_RESIDUES);
    }

    /// <summary>Sets the Residue Dict of a Geomety using a list of ResidueIDs and caps the dangling bonds.</summary>    
	/// <param name="residueIDs">List of ResidueIDs to extract and cap.</param>
    /// <param name="geometryInterfaceID">Geometry Interface ID of the Geometry to extract from.</param>
    /// <param name="taskID">The ID of the current Task.</param>
    private static IEnumerator GetCappedAtoms(List<ResidueID> residueIDs, GIID geometryInterfaceID, TID taskID) {

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);

        //Convert the List of ResidueIDs to a Residue Dictionary
        Dictionary<ResidueID, Residue> newResidues = new Dictionary<ResidueID, Residue>();
        
        //Keep track of progress 
        int totalResidues = 0;

        foreach (ResidueID residueID in residueIDs) {
            Residue residue;
            if (!geometryInterface.geometry.TryGetResidue(residueID, out residue)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Could not cap Residue '{0}' - Residue not found in Geometry!",
                    residueID
                );
            } else {
                newResidues[residueID] = residue.Take(geometryInterface.geometry);
                totalResidues++;
            }
        }

        int numProcessedResidues = 0;

        foreach (ResidueID newResidueID in residueIDs) {
            Residue newResidue = newResidues[newResidueID];
            //Loop through each Atom
            foreach ((PDBID pdbID, Atom atom) in newResidue.EnumerateAtoms()) {
                //Get each Atom's External Connections
                List<AtomID> neighbourIDs = atom
                    .EnumerateExternalConnections()
                    .Select(x => x.Item1)
                    .ToList();

                foreach (AtomID neighbourID in neighbourIDs) {

                    //If the external connection is not in our original list, it is now a dangling bond
                    if (!residueIDs.Contains(neighbourID.residueID)) {

                        //Cap the dangling bond here
                        if (!TryCapSite(
                            geometryInterfaceID, 
                            new AtomID(newResidueID, pdbID), 
                            neighbourID,
                            newResidues 
                        )) {
                            yield break;
                        }
                    }
                }
            }

            //Feed back progress
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    taskID, 
                    CustomMathematics.Map( (float)numProcessedResidues / totalResidues, 0f, 1f, 0.1f, 1f)
                );
                yield return null;
            }
            numProcessedResidues++;
        }
        
        geometryInterface.geometry.SetResidues(newResidues);

    }

    /// <summary>Caps dangling bonds of a Geometry.</summary>    
    /// <param name="geometry">The Geometry to cap.</param>
    /// <param name="parent">The Parent Geometry to get caps from.</param>
    /// <param name="taskID">The ID of the current Task.</param>
    public static IEnumerator CapAtoms(Geometry geometry, Geometry parent, TID taskID) {

        List<ResidueID> residueIDs = geometry.EnumerateResidueIDs().ToList();
        
        Dictionary<ResidueID, Residue> newResidues = new Dictionary<ResidueID, Residue>();
        
        //Keep track of progress 
        int totalResidues = 0;
        
        foreach (ResidueID residueID in residueIDs) {
            Residue residue;
            if (!geometry.TryGetResidue(residueID, out residue)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Could not cap Residue '{0}' - Residue not found in Geometry!",
                    residueID
                );
            } else {
                newResidues[residueID] = residue.Take(geometry);
                totalResidues++;
            }
        }

        int numProcessedResidues = 0;

        foreach ((ResidueID residueID, Residue residue) in geometry.EnumerateResidues()) {
            
            //Loop through each Atom
            foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
                //Get each Atom's External Connections
                List<AtomID> neighbourIDs = atom
                    .EnumerateExternalConnections()
                    .Select(x => x.Item1)
                    .ToList();

                foreach (AtomID neighbourID in neighbourIDs) {

                    //If the external connection is not in our original list, it is now a dangling bond
                    if (!residueIDs.Contains(neighbourID.residueID)) {

                        //Cap the dangling bond here
                        if (!TryCapSite(
                            parent, 
                            new AtomID(residueID, pdbID), 
                            neighbourID,
                            newResidues 
                        )) {
                            yield break;
                        }
                    }
                }
            }

            //Feed back progress
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    taskID, 
                    CustomMathematics.Map( (float)numProcessedResidues / totalResidues, 0f, 1f, 0.1f, 1f)
                );
                yield return null;
            }
            numProcessedResidues++;
        }
        
        geometry.SetResidues(newResidues);

    }

    /// <summary>Attempt to Cap a dangling bond</summary>
	/// <param name="geometryInterfaceID">The Geometry Interface ID of the Geometry to cap.</param>
	/// <param name="siteID">The Atom ID of the Atom to Cap.</param>
	/// <param name="neighbourID">The Atom ID of the external Neighbour of the Site to cap.</param>
	/// <param name="siteResidueDict">The Residue Dict of the Site to cap.</param>
    private static bool TryCapSite(
        GIID geometryInterfaceID, 
        AtomID siteID, 
        AtomID neighbourID,
        Dictionary<ResidueID, Residue> siteResidueDict=null
    ) {

        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        if (siteResidueDict == null) {
            siteResidueDict = geometryInterface.
                geometry
                .EnumerateResidues()
                .ToDictionary(x => x.residueID, x => x.residue);
        }
        Residue siteResidue = siteResidueDict[siteID.residueID];
        //Disconnect - will be reconnected to Cap
        Atom siteAtom = siteResidue.GetAtom(siteID.pdbID);

        (ResidueID siteResidueID, PDBID sitePDBID) = siteID;

        siteAtom.TryDisconnect(neighbourID);

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Found a dangling bond from Non-Standard Atom {0} to Standard Atom {1}",
            () => new object[] {
                siteID,
                neighbourID
            }
        );

        //Cap site
        if (neighbourID.pdbID.element == Element.N) {
            return CapWithNME(geometryInterfaceID, siteID, neighbourID, siteResidueDict);
        } else if (neighbourID.pdbID.element == Element.C) {
            return CapWithACE(geometryInterfaceID, siteID, neighbourID, siteResidueDict);
        } else {

            CustomLogger.LogFormat(
                EL.WARNING,
                "Dangling bond '{0}'-'{1}' cannot be capped with ACE or NME! Using a Custom Cap (needs attention!).",
                () => new object[] {
                    new AtomID(siteResidueID, sitePDBID),
                    neighbourID
                }
            );

            Residue customCap;
            if (!siteResidueDict.TryGetValue(neighbourID.residueID, out customCap)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Couldn't find Custom Cap Residue!",
                    neighbourID.residueID
                );
                return false;
            } 

            customCap.state = RS.CAP;
            customCap  .GetAtom(neighbourID.pdbID).Connect(new AtomID(siteResidueID, sitePDBID), BT.SINGLE);
            siteResidue.GetAtom(sitePDBID        ).Connect(neighbourID                         , BT.SINGLE);

        }

        return true;
    }

    /// <summary>Attempt to Cap a dangling bond</summary>
	/// <param name="geometry">The Geometry to cap.</param>
	/// <param name="siteID">The Atom ID of the Atom to Cap.</param>
	/// <param name="neighbourID">The Atom ID of the external Neighbour of the Site to cap.</param>
	/// <param name="siteResidueDict">The Residue Dict of the Site to cap.</param>
    private static bool TryCapSite(
        Geometry geometry, 
        AtomID siteID, 
        AtomID neighbourID,
        Dictionary<ResidueID, Residue> siteResidueDict=null
    ) {

        if (siteResidueDict == null) {
            siteResidueDict = geometry
                .EnumerateResidues()
                .ToDictionary(x => x.residueID, x => x.residue);
        }
        Residue siteResidue = siteResidueDict[siteID.residueID];
        //Disconnect - will be reconnected to Cap
        Atom siteAtom = siteResidue.GetAtom(siteID.pdbID);

        (ResidueID siteResidueID, PDBID sitePDBID) = siteID;

        siteAtom.TryDisconnect(neighbourID);

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Found a dangling bond from Non-Standard Atom {0} to Standard Atom {1}",
            () => new object[] {
                siteID,
                neighbourID
            }
        );

        //Cap site
        if (neighbourID.pdbID.element == Element.N) {
            return CapWithNME(geometry, siteID, neighbourID, siteResidueDict);
        } else if (neighbourID.pdbID.element == Element.C) {
            return CapWithACE(geometry, siteID, neighbourID, siteResidueDict);
        } else {

            CustomLogger.LogFormat(
                EL.WARNING,
                "Dangling bond '{0}'-'{1}' cannot be capped with ACE or NME! Using a Custom Cap (needs attention!).",
                () => new object[] {
                    new AtomID(siteResidueID, sitePDBID),
                    neighbourID
                }
            );

            Residue customCap;
            if (!siteResidueDict.TryGetValue(neighbourID.residueID, out customCap)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Couldn't find Custom Cap Residue!",
                    neighbourID.residueID
                );
                return false;
            } 

            customCap.state = RS.CAP;
            customCap  .GetAtom(neighbourID.pdbID).Connect(new AtomID(siteResidueID, sitePDBID), BT.SINGLE);
            siteResidue.GetAtom(sitePDBID        ).Connect(neighbourID                         , BT.SINGLE);

        }

        return true;
    }

    /// <summary>Attempt to Cap a dangling bond with NME</summary>
	/// <param name="geometryInterfaceID">The Geometry Interface ID of the Geometry to cap.</param>
	/// <param name="siteID">The Atom ID of the Atom to Cap.</param>
	/// <param name="neighbourID">The Atom ID of the external Neighbour of the Site to cap.</param>
	/// <param name="siteResidueDict">The Residue Dict of the Site to cap.</param>
    private static bool CapWithNME(
        GIID geometryInterfaceID, 
        AtomID siteID, 
        AtomID neighbourID,
        Dictionary<ResidueID, Residue> siteResidueDict
    ) {
        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        Residue siteResidue = siteResidueDict[siteID.residueID];
        (ResidueID siteResidueID, PDBID sitePDBID) = siteID;

        ResidueID newCapID = siteResidueDict.ContainsKey(neighbourID.residueID)
            ?siteID.residueID.GetNextID()
            :neighbourID.residueID;

        Residue nme;
        if (! geometryInterface.geometry.TryGetResidue(neighbourID.residueID, out nme)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't find neighbouring residue: {0}",
                neighbourID.residueID
            );
            return false;
        }

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Capping Residue {0} with NME Cap at {1}.",
            () => new object[] {
                siteResidueID,
                sitePDBID
            }
        );

        nme = nme.ConvertToNME(neighbourID.pdbID);
        PDBID nmeNID = PDBID.N;
        nme        .GetAtom(nmeNID)   .Connect(new AtomID(siteResidueID, sitePDBID), BT.SINGLE);
        siteResidue.GetAtom(sitePDBID).Connect(new AtomID(newCapID     , nmeNID   ), BT.SINGLE);

        nme.SetResidueID(newCapID);
        siteResidueDict[newCapID] = nme;

        return true;

    }

    /// <summary>Attempt to Cap a dangling bond with NME</summary>
	/// <param name="geometry">The Geometry to cap.</param>
	/// <param name="siteID">The Atom ID of the Atom to Cap.</param>
	/// <param name="neighbourID">The Atom ID of the external Neighbour of the Site to cap.</param>
	/// <param name="siteResidueDict">The Residue Dict of the Site to cap.</param>
    public static bool CapWithNME(
        Geometry geometry, 
        AtomID siteID, 
        AtomID neighbourID,
        Dictionary<ResidueID, Residue> siteResidueDict
    ) {
        Residue siteResidue = siteResidueDict[siteID.residueID];
        (ResidueID siteResidueID, PDBID sitePDBID) = siteID;

        ResidueID newCapID = siteResidueDict.ContainsKey(neighbourID.residueID)
            ?siteID.residueID.GetNextID()
            :neighbourID.residueID;

        Residue nme;
        if (! geometry.TryGetResidue(neighbourID.residueID, out nme)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't find neighbouring residue: {0}",
                neighbourID.residueID
            );
            return false;
        }

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Capping Residue {0} with NME Cap at {1}.",
            () => new object[] {
                siteResidueID,
                sitePDBID
            }
        );

        nme = nme.ConvertToNME(neighbourID.pdbID);
        PDBID nmeNID = PDBID.N;
        nme        .GetAtom(nmeNID   ).Connect(new AtomID(siteResidueID, sitePDBID), BT.SINGLE);
        siteResidue.GetAtom(sitePDBID).Connect(new AtomID(newCapID     , nmeNID   ), BT.SINGLE);

        nme.SetResidueID(newCapID);
        siteResidueDict[newCapID] = nme;

        return true;

    }

    /// <summary>Attempt to Cap a dangling bond with ACE</summary>
	/// <param name="geometryInterfaceID">The Geometry Interface ID of the Geometry to cap.</param>
	/// <param name="siteID">The Atom ID of the Atom to Cap.</param>
	/// <param name="neighbourID">The Atom ID of the external Neighbour of the Site to cap.</param>
	/// <param name="siteResidueDict">The Residue Dict of the Site to cap.</param>
    private static bool CapWithACE(
        GIID geometryInterfaceID, 
        AtomID siteID, 
        AtomID neighbourID,
        Dictionary<ResidueID, Residue> siteResidueDict
    ) {
        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        Residue siteResidue = siteResidueDict[siteID.residueID];
        (ResidueID siteResidueID, PDBID sitePDBID) = siteID;
        ResidueID newCapID = siteResidueDict.ContainsKey(neighbourID.residueID)
            ?siteResidueID.GetPreviousID()
            :neighbourID.residueID;

        Residue ace;
        if (! geometryInterface.geometry.TryGetResidue(neighbourID.residueID, out ace)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't find neighbouring residue: {0}",
                neighbourID.residueID
            );
            return false;
        }

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Capping Residue {0} with ACE Cap at {1}.",
            () => new object[] {
                siteResidueID,
                sitePDBID
            }
        );

        ace = ace.ConvertToACE(neighbourID.pdbID);
        PDBID aceCID = PDBID.C;
        ace        .GetAtom(aceCID   ).Connect(new AtomID(siteResidueID, sitePDBID), BT.SINGLE);
        siteResidue.GetAtom(sitePDBID).Connect(new AtomID(newCapID     , aceCID   ), BT.SINGLE);
            
        ace.SetResidueID(newCapID);
        siteResidueDict[newCapID] = ace;
        return true;
    }

    /// <summary>Attempt to Cap a dangling bond with ACE</summary>
	/// <param name="geometry">The Geometry to cap.</param>
	/// <param name="siteID">The Atom ID of the Atom to Cap.</param>
	/// <param name="neighbourID">The Atom ID of the external Neighbour of the Site to cap.</param>
	/// <param name="siteResidueDict">The Residue Dict of the Site to cap.</param>
    public static bool CapWithACE(
        Geometry geometry, 
        AtomID siteID, 
        AtomID neighbourID,
        Dictionary<ResidueID, Residue> siteResidueDict
    ) {
        Residue siteResidue = siteResidueDict[siteID.residueID];
        (ResidueID siteResidueID, PDBID sitePDBID) = siteID;
        ResidueID newCapID = siteResidueDict.ContainsKey(neighbourID.residueID)
            ?siteResidueID.GetPreviousID()
            :neighbourID.residueID;

        Residue ace;
        if (!geometry.TryGetResidue(neighbourID.residueID, out ace)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't find neighbouring residue: {0}",
                neighbourID.residueID
            );
            return false;
        }

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Capping Residue {0} with ACE Cap at {1}.",
            () => new object[] {
                siteResidueID,
                sitePDBID
            }
        );

        ace = ace.ConvertToACE(neighbourID.pdbID);
        PDBID aceCID = PDBID.C;
        ace        .GetAtom(aceCID   ).Connect(new AtomID(siteResidueID, sitePDBID), BT.SINGLE);
        siteResidue.GetAtom(sitePDBID).Connect(new AtomID(newCapID     , aceCID   ), BT.SINGLE);
            
        ace.SetResidueID(newCapID);
        siteResidueDict[newCapID] = ace;
        return true;
    }

    /// <summary>Joins all neighbouring Non-Standard Residues into groups to simplify later calculations.</summary>    
	/// <param name="geometryInterfaceID">ID of Geometry Interface to Clean.</param>
    public static IEnumerator MergeNSRsByProximity(GIID geometryInterfaceID) {

        NotificationBar.SetTaskProgress(TID.MERGE_NSRS_BY_CONNECTIVITY, 0f);
        CustomLogger.LogFormat(
            EL.INFO, 
            "Merging Non-Standard Residues of Geometry Interface: {0}", 
            geometryInterfaceID
        );
        Geometry geometry = Flow.GetGeometry(geometryInterfaceID);

        //Groups of connected non-standard ResidueID
        List<List<ResidueID>> groups = new List<List<ResidueID>>();

        //Get list of NSRs
        IEnumerable<(ResidueID, Residue)> residues = geometry.EnumerateResidues(x => x.state == RS.NONSTANDARD);

        //Group NSRs by proximity
        int numProcessedResidues = 0;
        int totalResidues = residues.Count();
        foreach ((ResidueID residueID, Residue residue) in residues) {
            // Check if residueID is grouped already
            if (groups.Any(x => x.Any(y => y == residueID))) {continue;}

            //Add new NSR group
            HashSet<ResidueID> group = new HashSet<ResidueID>{residueID};

            groups.Add(geometry.GetConnectedResidueIDs(group, RS.NONSTANDARD).Select(x => x.Item1).ToList());

            numProcessedResidues++;
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.MERGE_NSRS_BY_CONNECTIVITY, 
                    CustomMathematics.Map( (float)numProcessedResidues / totalResidues, 0f, 1f, 0f, 0.3f)
                );
                yield return null;
            }
        }

        //New NSRs will be called e.g. CRA, CRB, CRC etc
        IEnumerator<char> alphabet = Enumerable.Range(65, 26).Select(x => (char)x).GetEnumerator();

        //Merge grouped residues
        int numProcessedGroups = 0;
        int totalGroups = groups.Count();
        foreach (IEnumerable<ResidueID> group in groups) {
            //Only need to merge non-singular groups
            if (group.Count() < 2) {
                CustomLogger.LogFormat(
                    EL.INFO, 
                    "Skipping merge of NSR group: '{0}'", 
                    string.Join(" ", group)
                );
                continue;
            }

            //Get new NSR name
            alphabet.MoveNext();
            string residueName = Settings.baseNSRName + alphabet.Current;

            CustomLogger.LogFormat(
                EL.INFO, 
                "Merging NSR group '{0}' into Residue with Name '{1}'", 
                string.Join(" ", group),
                residueName
            );
            
            //Get enumerator for the group so we can take advantage of Current and MoveNext()
            IEnumerator<ResidueID> groupEnumerator = group.GetEnumerator();
            groupEnumerator.MoveNext();

            //New Residue is from the first residue
            ResidueID newResidueID = groupEnumerator.Current;
            Residue newResidue = geometry.GetResidue(newResidueID);
            CustomLogger.LogFormat(
                EL.DEBUG,
                "Residue {0} contains PDBIDs: '{1}'",
                () => new object[] {
                    newResidueID,
                    string.Join("', '", newResidue.pdbIDs)
                }
            );
            newResidue.residueName = residueName;

            //Keep track of connections to the outside
            List<(AtomID, AtomID, AtomID, BT)> mappedConnections = new List<(AtomID, AtomID, AtomID, BT)>();


            //Add neighbouring atoms to this residue
            while (groupEnumerator.MoveNext()) {
                ResidueID neighbourResidueID = groupEnumerator.Current;
                Residue neighbourResidue = geometry.GetResidue(neighbourResidueID);
                CustomLogger.LogFormat(
                    EL.DEBUG,
                    "Neighbour Residue {0} contains PDBIDs: '{1}'",
                    () => new object[] {
                        neighbourResidueID,
                        string.Join("', '", neighbourResidue.pdbIDs)
                    }
                );
                foreach ((PDBID oldPDBID, Atom atom) in neighbourResidue.EnumerateAtoms()) {

                    //Add atom - PDBID can and will change to prevent clashes!
                    PDBID newPDBID;
                    newResidue.AddAtom(oldPDBID, atom, out newPDBID);

                    AtomID oldAtomID = new AtomID(neighbourResidueID, oldPDBID);
                    AtomID newAtomID = new AtomID(newResidueID, newPDBID);

                    CustomLogger.LogFormat(
                        EL.DEBUG,
                        "Adding PDBID '{0}' from Neighbour Residue {1} to Residue {2}. New PDBID is '{3}'",
                        () => new object[] {
                            oldPDBID,
                            neighbourResidueID,
                            newResidueID,
                            newPDBID
                        }
                    );

                    List<(AtomID, BT)> externalConnections = atom.EnumerateExternalConnections().ToList();
                    CustomLogger.LogFormat(
                        EL.DEBUG,
                        "Atom {0} has external connections: '{1}'",
                        () => new object[] {
                            newAtomID,
                            string.Join("', '", externalConnections.Select(x => x.Item1))
                        }
                    );

                    List<(AtomID, BT)> internalConnections = atom.EnumerateInternalConnections().ToList();
                    CustomLogger.LogFormat(
                        EL.DEBUG,
                        "Atom {0} has internal connections: '{1}'",
                        () => new object[] {
                            newAtomID,
                            string.Join("', '", internalConnections.Select(x => x.Item1))
                        }
                    );

                    //Loop through External Connections
                    foreach ((AtomID externalID, BT bondType) in externalConnections) {
                        if (externalID.residueID == atom.residueID) {
                            //This should now be an internal connection

                            atom.externalConnections.Remove(externalID);
                            atom.internalConnections.Add(externalID.pdbID, bondType);

                            Atom neighbourAtom;
                            if (!geometry.TryGetAtom(externalID, out neighbourAtom)) {
                                CustomLogger.LogFormat(
                                    EL.ERROR,
                                    "Couldn't find AtomID {1} while converting External connection '{0}'-'{1}' to Internal Connection '{2}'-'{3}'",
                                    () => new object[] {
                                        oldAtomID,
                                        externalID,
                                        newAtomID,
                                        externalID
                                    }
                                );
                                NotificationBar.ClearTask(TID.MERGE_NSRS_BY_CONNECTIVITY);
                                yield break;
                            }

                            neighbourAtom.externalConnections.Remove(oldAtomID);
                            neighbourAtom.internalConnections.Add(newPDBID, bondType);

                            CustomLogger.LogFormat(
                                EL.DEBUG,
                                "External connection '{0}'-'{1}' is now Internal Connection '{2}'-'{3}'",
                                () => new object[] {
                                    oldAtomID,
                                    externalID,
                                    newAtomID,
                                    externalID
                                }
                            );
                            
                        } else if (newPDBID != oldPDBID) {
                            //Neighbour needs to know that PDBID of this Atom has changed
                            
                            Atom neighbourAtom;
                            if (!geometry.TryGetAtom(externalID, out neighbourAtom)) {
                                CustomLogger.LogFormat(
                                    EL.ERROR,
                                    "Couldn't find AtomID {1} while converting External connection '{0}'-'{1}' to External Connection '{2}'-'{3}'",
                                    () => new object[] {
                                        oldAtomID,
                                        externalID,
                                        newAtomID,
                                        externalID
                                    }
                                );
                                NotificationBar.ClearTask(TID.MERGE_NSRS_BY_CONNECTIVITY);
                                yield break;
                            }

                            neighbourAtom.externalConnections.Remove(oldAtomID);
                            neighbourAtom.externalConnections.Add(newAtomID, bondType);

                            CustomLogger.LogFormat(
                                EL.DEBUG,
                                "External connection '{0}'-'{1}' is now External Connection '{2}'-'{3}'",
                                () => new object[] {
                                    oldAtomID,
                                    externalID,
                                    newAtomID,
                                    externalID
                                }
                            );

                        }
                    }

                    foreach ((AtomID internalID, BT bondType) in internalConnections) {
                        AtomID oldInternalID = new AtomID(groupEnumerator.Current, internalID.pdbID);
                        if (oldInternalID.residueID != atom.residueID) {
                            //This should now be an external connection
                            atom.internalConnections.Remove(oldInternalID.pdbID);
                            atom.externalConnections.Add(internalID, bondType);

                            Atom neighbourAtom;
                            if (!geometry.TryGetAtom(oldInternalID, out neighbourAtom)) {
                                CustomLogger.LogFormat(
                                    EL.ERROR,
                                    "Couldn't find AtomID {1} while converting Internal connection '{0}'-'{1}' to External Connection '{2}'-'{3}'",
                                    () => new object[] {
                                        oldAtomID,
                                        oldInternalID,
                                        newAtomID,
                                        internalID
                                    }
                                );
                                NotificationBar.ClearTask(TID.MERGE_NSRS_BY_CONNECTIVITY);
                                yield break;
                            }

                            neighbourAtom.internalConnections.Remove(oldAtomID.pdbID);
                            neighbourAtom.externalConnections.Add(newAtomID, bondType);

                            CustomLogger.LogFormat(
                                EL.DEBUG,
                                "Internal connection '{0}'-'{1}' is now External Connection '{2}'-'{3}'",
                                () => new object[] {
                                    oldAtomID,
                                    oldInternalID,
                                    newAtomID,
                                    internalID
                                }
                            );
                        } else if (newPDBID != oldPDBID) {
                            //Neighbour needs to know that PDBID of this Atom has changed

                            Atom neighbourAtom;
                            if (!geometry.TryGetAtom(oldInternalID, out neighbourAtom)) {
                                CustomLogger.LogFormat(
                                    EL.ERROR,
                                    "Couldn't find AtomID {1} while converting Internal connection '{0}'-'{1}' to Interal Connection '{2}'-'{3}'",
                                    () => new object[] {
                                        oldAtomID,
                                        oldInternalID,
                                        newAtomID,
                                        internalID
                                    }
                                );
                                NotificationBar.ClearTask(TID.MERGE_NSRS_BY_CONNECTIVITY);
                                yield break;
                            }

                            neighbourAtom.internalConnections.Remove(oldPDBID);
                            neighbourAtom.internalConnections.Add(newPDBID, bondType);

                            CustomLogger.LogFormat(
                                EL.DEBUG,
                                "Internal connection '{0}'-'{1}' is now Internal Connection '{2}'-'{3}'",
                                () => new object[] {
                                    oldAtomID,
                                    oldInternalID,
                                    newAtomID,
                                    internalID
                                }
                            );
                        }
                    }
                }

                //Delete old residue
                geometry.RemoveResidue(groupEnumerator.Current);
            }

            numProcessedResidues++;
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.MERGE_NSRS_BY_CONNECTIVITY, 
                    CustomMathematics.Map( (float)numProcessedGroups / totalGroups, 0f, 1f, 0f, 0.3f)
                );
                yield return null;
            }

        }
        
        NotificationBar.ClearTask(TID.MERGE_NSRS_BY_CONNECTIVITY);
        yield return null;
    }
        public static IEnumerator MergeGeometries(GIID geometryInterfaceID) {
        //Join two geometry interfaces (Left and Right -> Merged)
        //Merge Left and Right Geometry while removing capping residues
        //Merge Left and Right Parameters
        //Process is NOT symmetric -> Parameters of Right will replace similar Parameters of Left

        //Get the startIDs of the arrows
        List<GIID> geometriesToMerge = new List<GIID>();
        foreach (GeometryInterface gi in Flow.main.geometryDict.Values) {
            foreach (Arrow arrow in gi.arrows) {
                if (arrow.endGIID == geometryInterfaceID && !geometriesToMerge.Contains(arrow.startGIID)) {
                    geometriesToMerge.Add(arrow.startGIID);
                }
            }
        }

        GeometryInterface geometryInterfaceMerged = Flow.GetGeometryInterface(geometryInterfaceID);

        //Check this GIID is valid
        if (geometriesToMerge.Count != 2) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Incorrect number of geometries - {0}. Should be 2",
                geometriesToMerge.Count
            );
            yield break;
        }

        GeometryInterface geometryInterfaceLeft = Flow.GetGeometryInterface(geometriesToMerge[0]);
        if (!(geometryInterfaceLeft.status == GIS.OK || geometryInterfaceLeft.status == GIS.COMPLETED)) {
            yield break;
        }
        GeometryInterface geometryInterfaceRight = Flow.GetGeometryInterface(geometriesToMerge[1]);
        if (!(geometryInterfaceRight.status == GIS.OK || geometryInterfaceRight.status == GIS.COMPLETED)) {
            yield break;
        }


        geometryInterfaceLeft.activeTasks++;
        geometryInterfaceRight.activeTasks++;

        //Create new Geometry
        NotificationBar.SetTaskProgress(TID.MERGE_GEOMETRIES, 0f);

        geometryInterfaceMerged.geometry = PrefabManager.InstantiateGeometry(Flow.GetGeometryInterface(geometryInterfaceID).transform);
        
        NotificationBar.SetTaskProgress(TID.MERGE_GEOMETRIES, 0.1f);

        //Keep track of which atoms were connected to caps
        // - these will form bonds with geometryInterfaceRight
        List<(AtomID, AtomID)> oldConnections = new List<(AtomID, AtomID)>();
        
        Dictionary<ResidueID, Residue> mergedResidueDict = new Dictionary<ResidueID, Residue>();
        foreach ((ResidueID residueID, Residue residue) in geometryInterfaceLeft.geometry.EnumerateResidues()) {
            if (residue.state == RS.CAP) {
                //Left Residue is a capping residue
                //Get the atom that the cap was connected to
                oldConnections.AddRange(residue.NeighbouringAtomIDs());
            } else {
                //Add residue otherwise
                //Will need to remove link to cap later
                mergedResidueDict[residueID] = residue.Take(geometryInterfaceMerged.geometry);
            }
        }


        int numProcessedResidues = 0;
        int numResidues = geometryInterfaceRight.geometry.residueCount;
        foreach ((ResidueID residueID, Residue residue) in geometryInterfaceRight.geometry.EnumerateResidues()) {
            if (residue.state == RS.CAP) {
                // Right Residue is a capping residue
                //Get the atom that the cap was connected to
                oldConnections.AddRange(residue.NeighbouringAtomIDs());
            } else if (!mergedResidueDict.ContainsKey(residueID)) {
                // New residue
                mergedResidueDict[residueID] = residue.Take(geometryInterfaceMerged.geometry);
            } else {
                //Clash
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Clash when merging residues - neither {0} ({1}) nor {2} ({3}) are flagged as CAP", 
                    () => {
                        return new object[4] {
                            residue.residueName, 
                            residue.state,
                            mergedResidueDict[residueID].residueName,
                            mergedResidueDict[residueID].state
                        };
                    }
                );
                geometryInterfaceLeft.activeTasks--;
                geometryInterfaceRight.activeTasks--;
                NotificationBar.ClearTask(TID.MERGE_GEOMETRIES);
                yield break;
            }

            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.MERGE_GEOMETRIES, 
                    CustomMathematics.Map((float)numProcessedResidues/numResidues, 0f, 1f, 0.1f, 0.9f)
                );
                yield return null;
            }
        }

        //Disconnect all references to caps
        foreach ((AtomID oldCapAtomID, AtomID hostAtomID) in oldConnections) {
            Atom hostAtom = mergedResidueDict[hostAtomID.residueID].GetAtom(hostAtomID.pdbID);
            hostAtom.TryDisconnect(oldCapAtomID);
        }

        //Make new connections at connection points
        foreach ((AtomID oldCapAtomID0, AtomID hostAtomID0) in oldConnections) {
            Atom hostAtom0 = mergedResidueDict[hostAtomID0.residueID].GetAtom(hostAtomID0.pdbID);
            float3 p0 = hostAtom0.position;
            foreach ((AtomID oldCapAtomID1, AtomID hostAtomID1) in oldConnections) {
                if (hostAtomID0 == hostAtomID1) {
                    continue;
                }
                Atom hostAtom1 = mergedResidueDict[hostAtomID1.residueID].GetAtom(hostAtomID1.pdbID);
                float3 p1 = hostAtom1.position;

                float distanceSquared = math.distancesq(p0, p1);
                BT bondType = Data.GetBondOrderDistanceSquared(
                    hostAtomID0.pdbID.element, 
                    hostAtomID1.pdbID.element, 
                    distanceSquared
                );

                if (bondType != BT.NONE) {
                    hostAtom0.Connect(hostAtomID1, bondType);
                    hostAtom1.Connect(hostAtomID0, bondType);
                }
            }
        }

        NotificationBar.SetTaskProgress(TID.MERGE_GEOMETRIES, 0.9f);
        geometryInterfaceMerged.geometry.SetResidues(mergedResidueDict);
        yield return geometryInterfaceMerged.SetGeometry();

        Parameters.Copy(geometryInterfaceLeft.geometry, geometryInterfaceMerged.geometry);
        Parameters.UpdateParameters(geometryInterfaceRight.geometry, geometryInterfaceMerged.geometry, true);
        

        yield return null;

        geometryInterfaceLeft.status = GIS.COMPLETED;
        geometryInterfaceRight.status = GIS.COMPLETED;

        geometryInterfaceLeft.activeTasks--;
        geometryInterfaceRight.activeTasks--;
        NotificationBar.ClearTask(TID.MERGE_GEOMETRIES);
    }

}
