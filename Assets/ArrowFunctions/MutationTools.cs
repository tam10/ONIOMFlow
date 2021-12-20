using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RS = Constants.ResidueState;
using EL = Constants.ErrorLevel;
using BT = Constants.BondType;
using TID = Constants.TaskID;
using Amber = Constants.Amber;
using Element = Constants.Element;
using System.Linq;
using Unity.Mathematics;


public class ResidueMutator : MonoBehaviour {

    public Parameters parameters;
    public Geometry geometry;
    Residue oldResidue;

    public bool failed;

    public float clashScore;
    public DihedralScanner dihedralScanner;

    public enum OptimisationMethod {NONE, TREE, BRUTE_FORCE, TREE_BRUTE};
    
    static readonly IList<RS> validStates = new [] {RS.STANDARD, RS.C_TERMINAL, RS.N_TERMINAL};

    public void Initialise(Geometry geometry, ResidueID residueID, Parameters sourceParameters=null) {
        
        if (geometry == null) {
            throw new System.Exception(string.Format(
                "Cannot Mutate '{0}' - Geometry is null!",
                residueID
            ));
        }
        

        if (parameters == null) {
            parameters = PrefabManager.InstantiateParameters(transform);
        }

        if (sourceParameters != null) {

            parameters.UpdateParameters(sourceParameters);

            if (parameters.IsEmpty()) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Source Parameters are empty - cannot initialise Dihedral Scanner"
                );
                failed = true;
                return;
            } else {
                CustomLogger.LogFormat(
                    EL.VERBOSE,
                    "Using defined Parameters for Dihedral Scanner"
                );
            }

        } else if (geometry.parameters.IsEmpty()) {
            parameters.UpdateParameters(Settings.defaultParameters);

            if (parameters.IsEmpty()) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "No Parameters available - cannot initialise Dihedral Scanner"
                );
                failed = true;
                return;
            } else {
                CustomLogger.LogFormat(
                    EL.VERBOSE,
                    "Geometry Parameters empty - using Default Parameters for Dihedral Scanner"
                );
            }
        } else {
            parameters.UpdateParameters(geometry.parameters);
            
            if (parameters.IsEmpty()) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "No Parameters in {0} - cannot initialise Dihedral Scanner",
                    geometry.name
                );
                failed = true;
                return;
            } else {
                CustomLogger.LogFormat(
                    EL.VERBOSE,
                    "Using Geometry Parameters for Dihedral Scanner"
                );
            }

        }

        if (!geometry.TryGetResidue(residueID, out oldResidue)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot Mutate '{0}' - Residue not found in Geometry!",
                residueID
            );
            failed = true;
            return;
        }

        if (! validStates.Any(x => x == oldResidue.state)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot Mutate '{0}' - Residue is not in a valid State for mutation. Must be one of: {0}",
                string.Join(", ", validStates.Select(x => Constants.ResidueStateMap[x]))
            );
            failed = true;
            return;
        }

        this.geometry = geometry;
        failed = false;
        clashScore = 0;
    }
	
    Atom GetValidAtom(Residue residue, PDBID pdbID) {
        Atom atom = residue.GetSingleAtom(pdbID);
        if (atom == null) {
            throw new System.Exception(string.Format(
                "Required Atom {0} is null.",
                new AtomID(residue.residueID, pdbID)
            ));
        }

        if (math.all(math.isnan(atom.position))) {
            throw new System.Exception(string.Format(
                "Position of atom {0} is {1}.",
                new AtomID(residue.residueID, pdbID),
                atom.position
            ));
        }
        return atom;
    }

	///<summary>
	/// Change a Standard Residue for another.
	///</summary>
    public IEnumerator MutateStandard(Residue targetResidue, float deltaTheta, OptimisationMethod optimisationMethod=OptimisationMethod.TREE) {

        if (targetResidue == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "New Residue is null! Cannot mutate."
            );
            failed = true;
            yield break;
        }
        
        yield return NotificationBar.UpdateTaskProgress(TID.MUTATE_RESIDUE, 0f);
        
        CustomLogger.LogFormat(
            EL.INFO,
            "Mutating Residue '{0}' ('{1}'[{3}] -> '{2}'[{4}])",
            oldResidue.residueID,
            oldResidue.residueName,
            targetResidue.residueName,
            oldResidue.state,
            targetResidue.state
        );

        yield return AlignTargetResidue(targetResidue);
        if (failed) {
            yield return NotificationBar.UpdateClearTask(TID.MUTATE_RESIDUE);
            yield break;
        }

        yield return ReplaceSideChain(targetResidue);
        if (failed) {
            yield return NotificationBar.UpdateClearTask(TID.MUTATE_RESIDUE);
            yield break;
        }
        
        oldResidue.residueName = targetResidue.residueName;

        if (optimisationMethod == OptimisationMethod.NONE) {
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "No dihedral optimisation."
            );
        } else {
            yield return Optimise(targetResidue, deltaTheta, optimisationMethod);
            if (failed) {
                yield return NotificationBar.UpdateClearTask(TID.MUTATE_RESIDUE);
                yield break;
            }
        }

        //Replace the clashgroup in the dihedral scanner
        dihedralScanner.geometryClashGroups[targetResidue.residueID] = new ClashGroup(targetResidue, parameters);

        yield return NotificationBar.UpdateClearTask(TID.MUTATE_RESIDUE);
    }

    IEnumerator AlignTargetResidue(Residue targetResidue) {
        
        Atom cAtom;
        Atom caAtom;
        Atom nAtom;

        try {
			cAtom  = GetValidAtom(oldResidue, PDBID.C);
			caAtom = GetValidAtom(oldResidue, PDBID.CA);
			nAtom  = GetValidAtom(oldResidue, PDBID.N);
		} catch (System.Exception e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot Mutate '{0}': {1}{2}{3}",
                oldResidue.residueID,
                e.Message,
                FileIO.newLine,
                e.StackTrace
            );
            NotificationBar.ClearTask(TID.MUTATE_RESIDUE);
            failed = true;
            yield break;
		}

        
        yield return NotificationBar.UpdateTaskProgress(TID.MUTATE_RESIDUE, 0.05f);

        //Move mutated Residue to current position
        targetResidue.TranslateTo(PDBID.C, cAtom.position);

        //Align to new bond direction
        targetResidue.AlignBond(
            PDBID.C, 
            PDBID.N, 
            math.normalize(nAtom.position - cAtom.position)
        );

        //Align by dihedral
        float dihedral = CustomMathematics.GetDihedral(caAtom, cAtom, nAtom, targetResidue.GetAtom(PDBID.CA));
        Vector3 bondVector = CustomMathematics.GetVector(cAtom, nAtom);
        targetResidue.Rotate(
            quaternion.AxisAngle(math.normalize(cAtom.position - nAtom.position), -dihedral), 
            targetResidue.GetAtom(PDBID.C).position
        );

    }

    IEnumerator ReplaceSideChain(Residue targetResidue) {
        
        bool protonated = oldResidue.protonated;

        //Delete all sidechain Atoms

        List<PDBID> deleteList = oldResidue.pdbIDs.Where(pdbID => !Data.backbonePDBs.Contains(pdbID)).ToList();
        foreach (PDBID pdbID in deleteList) {
            oldResidue.RemoveAtom(pdbID);
        }
        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Deleted atoms: '{0}'",
            string.Join("', '", deleteList)
        );
		
        yield return NotificationBar.UpdateTaskProgress(TID.MUTATE_RESIDUE, 0.1f);
        
        List<PDBID> addList;
        if (protonated) {
            addList = targetResidue.pdbIDs.Where(pdbID => !Data.backbonePDBs.Contains(pdbID)).ToList();
        } else {
            addList = targetResidue.pdbIDs.Where(pdbID => pdbID.element != Element.H && !Data.backbonePDBs.Contains(pdbID)).ToList();
        }

        //Add new sidechain Atoms
        foreach (PDBID pdbID in addList) {
            oldResidue.AddAtom(pdbID, targetResidue.GetAtom(pdbID).Copy());
        }

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Added atoms:   '{0}'",
            string.Join("', '", addList)
        );
		
        yield return NotificationBar.UpdateTaskProgress(TID.MUTATE_RESIDUE, 0.15f);

        //Proline exceptions
        PDBID cdPDBID = new PDBID(Element.C, "D");
        PDBID hPDBID = new PDBID(Element.H);

        Atom nAtom;
        try {
			nAtom  = GetValidAtom(oldResidue, PDBID.N);
		} catch (System.Exception e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot Mutate '{0}': {1}{2}{3}",
                oldResidue.residueID,
                e.Message,
                FileIO.newLine,
                e.StackTrace
            );
            failed = true;
            NotificationBar.ClearTask(TID.MUTATE_RESIDUE);
            yield break;
		}
        
        //Add H if Proline
        if (nAtom.EnumerateInternalConnections().Select(x => x.Item1.pdbID).Any(x => x.TypeEquals(cdPDBID))) {
            CustomLogger.Log(
                EL.DEBUG,
                "Old Residue is Proline-like. Adding missing Proton."
            );
            oldResidue.AddProton(PDBID.N);
        }

        if (targetResidue.residueName == "PRO") {
            if (oldResidue.HasAtom(hPDBID)){
                CustomLogger.Log(
                    EL.DEBUG,
                    "New Residue is Proline. Removing Backbone Proton."
                );
                oldResidue.RemoveAtom(hPDBID);
            }

            CustomLogger.Log(
                EL.DEBUG,
                "New Residue is Proline. Connecting N to CD."
            );

            oldResidue.GetAtom(PDBID.N).internalConnections[cdPDBID] = BT.SINGLE;
            oldResidue.GetAtom(cdPDBID).internalConnections[PDBID.N] = BT.SINGLE;
        }

        //Restore CA-CB bond if residue has CB
        if (oldResidue.pdbIDs.Contains(PDBID.CB)) {
            oldResidue.GetAtom(PDBID.CA).internalConnections[PDBID.CB] = BT.SINGLE;
            oldResidue.GetAtom(PDBID.CB).internalConnections[PDBID.CA] = BT.SINGLE;
        }
    }

    IEnumerator Optimise(Residue targetResidue, float deltaTheta, OptimisationMethod optimisationMethod=OptimisationMethod.TREE) {


        // Check for clashes and find best configuration
        //
        // 1. Select the Gamma position
        // 2. If the atom at this position doesn't exist, exit
        // 3. Form the dihedral containing atoms of the previous identifiers
        // 4. Rotate around the dihedral, fixing all the previous identifiers
        // 5. Take the dihedrals with the lowest clash score and keep
        // 6. Select the next position up ('D', 'E', 'Z', 'H')
        // 7. Continue at 2.

        dihedralScanner = GetComponent<DihedralScanner>();

        if (dihedralScanner == null) {
            //Create a new Dihedral scanner on this geometry
            dihedralScanner = gameObject.AddComponent<DihedralScanner>();
        }

        //Initialise Scanner
        dihedralScanner.Initialise(geometry, parameters);

        if (dihedralScanner.failed) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to create Dihedral Scanner"
            );
            failed = true;
            NotificationBar.ClearTask(TID.MUTATE_RESIDUE);
            yield break;
        }
        
        yield return dihedralScanner.SetResidue(targetResidue);

        switch (optimisationMethod) {
            case (OptimisationMethod.TREE):
            case (OptimisationMethod.TREE_BRUTE):
                yield return dihedralScanner.Optimise(deltaTheta, OptimisationMethod.TREE);
                break;
            case (OptimisationMethod.BRUTE_FORCE):
                yield return dihedralScanner.Optimise(deltaTheta, OptimisationMethod.BRUTE_FORCE);
                break;
        }
        
        yield return NotificationBar.UpdateTaskProgress(TID.MUTATE_RESIDUE, 0.95f);
        
        clashScore = dihedralScanner.bestScore;

        // Capture error message
        string message = "";
        try {
            dihedralScanner.UpdatePositions(oldResidue);
        } catch (System.Exception e) {
            failed = true;
            message = " - " + e.Message;
        }

        if (failed) {
            if (optimisationMethod == OptimisationMethod.TREE_BRUTE) {
                // Cancel failure - can also fail on brute force
                failed = false;
                // Resort to brute-force optimise
                CustomLogger.LogFormat(
                    EL.INFO,
                    "Resorting to Brute-Force optimisation"
                );
                yield return dihedralScanner.Optimise(deltaTheta, OptimisationMethod.BRUTE_FORCE);
                clashScore = dihedralScanner.bestScore;
                        
                try {
                    dihedralScanner.UpdatePositions(oldResidue);
                } catch (System.Exception e) {
                    failed = true;
                    message = " - " + e.Message;
                }

            } else {
                // Error message
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Optimisation failed!{0}",
                    message
                );
            }
        }

    }
}

    
public class DihedralScanner : MonoBehaviour {

    public ClashGroup residueClashGroup;
    public Dictionary<ResidueID, ClashGroup> geometryClashGroups;

    int[] residueIdentifiers;
    bool[] residueHydrogens;


    float[,] residueResidueScaledCharges;
    float[,] residueResidueRadiiSq;
    float[,] residueResidueWellDepths;
    float[,] residueResidueClashDistancesSq;

    int numNearbyAtoms;
    float3[] nearbyPositions;
    float[,] residueNearbyScaledCharges;
    float[,] residueNearbyRadii;
    float[,] residueNearbyWellDepths;
    float[,] residueNearbyClashDistancesSq;

    PDBID[][] dihedralGroupPDBIDs;
    int[][] dihedralGroups;
    Torsion[] torsions;
    public int numDihedralGroups;

    public Parameters parameters;

    public float bestScore;

    public bool stepAccepted;

    public float[] bestDihedrals;

    public ResidueMutator.OptimisationMethod optimisationMethod;
    public int scanSize;
    public int scanned;
    public int clashes;

    public bool failed;
    
    public void Initialise(Geometry geometry, Parameters sourceParameters=null) {

        failed = false;

        if (parameters == null) {
            parameters = PrefabManager.InstantiateParameters(transform);
        }

        if (sourceParameters != null) {

            parameters.UpdateParameters(sourceParameters);

            if (parameters.IsEmpty()) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Source Parameters are empty - cannot initialise Dihedral Scanner"
                );
                failed = true;
                return;
            } else {
                CustomLogger.LogFormat(
                    EL.VERBOSE,
                    "Using defined Parameters for Dihedral Scanner"
                );
            }

        } else if (geometry.parameters.IsEmpty()) {
            parameters.UpdateParameters(Settings.defaultParameters);

            if (parameters.IsEmpty()) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "No Parameters available - cannot initialise Dihedral Scanner"
                );
                failed = true;
                return;
            } else {
                CustomLogger.LogFormat(
                    EL.VERBOSE,
                    "Geometry Parameters empty - using Default Parameters for Dihedral Scanner"
                );
            }
        } else {
            parameters.UpdateParameters(geometry.parameters);
            
            if (parameters.IsEmpty()) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "No Parameters in {0} - cannot initialise Dihedral Scanner",
                    geometry.name
                );
                failed = true;
                return;
            } else {
                CustomLogger.LogFormat(
                    EL.VERBOSE,
                    "Using Geometry Parameters for Dihedral Scanner"
                );
            }

        }

        geometryClashGroups = geometry.EnumerateResidues()
            .ToDictionary(
                x => x.residueID, 
                x => new ClashGroup(x.residue, parameters)
            );

    }

    public IEnumerator SetResidue(Residue residue, bool prune=true) {

        // Get the Amino Acid group for this residue
        AminoAcid aminoAcid;
        if (!Data.aminoAcids.TryGetValue(residue.residueName, out aminoAcid)) {
            CustomLogger.LogOutput(
                "Amino Acid '{0}' not present in Database - cannot initialise Dihedral Scanner!",
                residue.residueName
            );
            failed = true;
            yield break;
        }

        // Get the PDB IDs for each rotateable dihedral in the residue's sidechain
        dihedralGroupPDBIDs = aminoAcid.GetDihedralPDBIDs(residue.state);
        numDihedralGroups = dihedralGroupPDBIDs.Count();
        
        torsions = new Torsion[numDihedralGroups];

        for (int dihedralGroupIndex=0; dihedralGroupIndex<numDihedralGroups; dihedralGroupIndex++) {
            yield return SetTorsionParameter(residue, dihedralGroupIndex);
        }

        residueClashGroup = new ClashGroup(residue, parameters);

        //Get indices
        dihedralGroups = new int[dihedralGroupPDBIDs.Length][];
        for (int dihedralGroupIndex=0; dihedralGroupIndex<dihedralGroupPDBIDs.Length; dihedralGroupIndex++) {
            dihedralGroups[dihedralGroupIndex] = dihedralGroupPDBIDs[dihedralGroupIndex]
                .Select(dihedralGroupPDBID => System.Array.IndexOf(residueClashGroup.pdbIDs, dihedralGroupPDBID))
                .ToArray();
        }

        CustomLogger.LogFormat(
            EL.INFO,
            "Dihedral Scanner initialised with Residue '{0}' ({1})",
            residue.residueID,
            residue.residueName
        );
        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Using Reference Amino Acid '{0}' with Residue State '{1}' ({2} Dihedral Groups)",
            aminoAcid.family,
            residue.state,
            numDihedralGroups
        );

        if (prune && numDihedralGroups > 0) {
            float cutoffDistanceSq = CustomMathematics.Squared(Settings.maxNonBondingCutoff + 10f);

            Atom rootAtom = residue.GetAtom(dihedralGroupPDBIDs[0][0]);
            float3 rootPosition = rootAtom.position;

            List<ResidueID> pruneList = new List<ResidueID>();
            
            foreach ((ResidueID nearbyResidueID, ClashGroup nearbyClashGroup) in geometryClashGroups) {
                if (nearbyClashGroup.positions.All(x => math.distancesq(x, rootPosition) > cutoffDistanceSq )) {
                    pruneList.Add(nearbyResidueID);
                }
            }

            foreach (ResidueID pruneID in pruneList) {
                geometryClashGroups.Remove(pruneID);
            }

            CustomLogger.LogFormat(
                EL.INFO,
                "Pruned {0} Residues",
                pruneList.Count()
            );
        }

        SetArrays();

    }

    void SetArrays() {

        int residueSize = residueClashGroup.size;
        numNearbyAtoms = geometryClashGroups
            .Where(x => x.Value.residueID != residueClashGroup.residueID)
            .Select(x => x.Value.size)
            .Sum();

        residueIdentifiers = new int[residueSize];
        residueHydrogens = new bool[residueSize];
        residueResidueScaledCharges = new float[residueSize, residueSize];
        residueResidueRadiiSq = new float[residueSize, residueSize];
        residueResidueWellDepths = new float[residueSize, residueSize];
        residueResidueClashDistancesSq = new float[residueSize, residueSize];
        nearbyPositions = new float3[numNearbyAtoms];
        residueNearbyScaledCharges = new float[residueSize, numNearbyAtoms];
        residueNearbyRadii = new float[residueSize, numNearbyAtoms];
        residueNearbyWellDepths = new float[residueSize, numNearbyAtoms];
        residueNearbyClashDistancesSq = new float[residueSize, numNearbyAtoms];

        for (int m0=0; m0<residueClashGroup.size; m0++) {
            ClashGroupAtom residueAtom0 = residueClashGroup.groupAtoms[m0];

            residueIdentifiers[m0] = residueAtom0.identifer;
            residueHydrogens[m0] = residueAtom0.pdbID.element == Element.H;

            float scaledCharge = residueAtom0.charge / parameters.dielectricConstant;

            for (int m1=m0+1; m1<residueClashGroup.size; m1++) {

                ClashGroupAtom residueAtom1 = residueClashGroup.groupAtoms[m1];

                residueResidueScaledCharges[m0,m1] = residueResidueScaledCharges[m1,m0] = scaledCharge * residueAtom1.charge;
                residueResidueRadiiSq[m0,m1] = residueResidueRadiiSq[m1,m0] = CustomMathematics.Squared(residueAtom0.radius + residueAtom1.radius);
                residueResidueWellDepths[m0,m1] = residueResidueWellDepths[m1,m0] = math.sqrt(residueAtom0.wellDepth + residueAtom1.wellDepth);

                float[] clashDisSq;
                if (Data.TryGetBondDistancesSquared(residueAtom0.pdbID.element, residueAtom1.pdbID.element, out clashDisSq)) {
                    residueResidueClashDistancesSq[m0,m1] = residueResidueClashDistancesSq[m1,m0] = clashDisSq[0];
                };
            }

            foreach (KeyValuePair<ResidueID, ClashGroup> nearbyClashGroup in geometryClashGroups) {
                if (residueClashGroup.residueID == nearbyClashGroup.Key) {
                    continue;
                }

                int m1 = 0;
                foreach (ClashGroupAtom nearbyAtom in nearbyClashGroup.Value.groupAtoms) {

                    residueNearbyScaledCharges[m0,m1] = scaledCharge * nearbyAtom.charge;
                    residueNearbyRadii[m0,m1] = CustomMathematics.Squared(residueAtom0.radius + nearbyAtom.radius);
                    residueNearbyWellDepths[m0,m1] = math.sqrt(residueAtom0.wellDepth + nearbyAtom.wellDepth);

                    float[] clashDisSq;
                    if (Data.TryGetBondDistancesSquared(residueAtom0.pdbID.element, nearbyAtom.pdbID.element, out clashDisSq)) {
                        residueNearbyClashDistancesSq[m0,m1] = clashDisSq[0];
                    };
                    
                    m1++;
                }
            }
        }
    }

    IEnumerator SetTorsionParameter(Residue residue, int dihedralGroupIndex) {
        
        Atom[] atoms = dihedralGroupPDBIDs[dihedralGroupIndex].Select(x => residue.GetAtom(x)).ToArray();
        Amber[] ambers = atoms.Select(x => x.amber).ToArray();

        //Get torsion parameters for this dihedral
        torsions[dihedralGroupIndex] = parameters.GetTorsion(ambers[0], ambers[1], ambers[2], ambers[3]);

        if (!torsions[dihedralGroupIndex].IsDefault()) {
            // Parameter was in set

            yield break;
        }

        yield return RecomputeTorsionParameter(residue, dihedralGroupIndex, ambers);
        
        // Attempt 1:
        // Get normal parameter match (X-X-X-X)
        // X: Exact match

        yield return null;

        if (!torsions[dihedralGroupIndex].IsDefault()) {
            // Successfully updated parameter
            CustomLogger.LogFormat(
                EL.INFO,
                "Successfully recomputed Torsion Parameter '{0}-{1}-{2}-{3}'.",
                ambers[0], ambers[1], ambers[2], ambers[3]
            );
            yield break;
        }
        
        // Attempt 2:
        // Try to use similar torsion (?-X-X-?)
        // ?: Element match

        GetSimilarTorsionParameter(dihedralGroupIndex, ambers);
            
        if (!torsions[dihedralGroupIndex].IsDefault()) {
            // Successfully found similar torsion
            CustomLogger.LogFormat(
                EL.WARNING,
                "Using Torsion Parameter '{0}-{1}-{2}-{3}' instead.",
                torsions[dihedralGroupIndex].types.amber0, 
                ambers[1], 
                ambers[2], 
                torsions[dihedralGroupIndex].types.amber3
            );
            yield break;
        }

        // Attempt 3:
        // Last resort - use any outer amber (*-X-X-*)
        // *: Any Amber
        
        GetAnyTorsionParameter(dihedralGroupIndex, ambers);

        if (!torsions[dihedralGroupIndex].IsDefault()) {
            // Successfully found similar torsion
            CustomLogger.LogFormat(
                EL.WARNING,
                "Using Torsion Parameter '{0}-{1}-{2}-{3}' instead.",
                torsions[dihedralGroupIndex].types.amber0, 
                ambers[1], 
                ambers[2], 
                torsions[dihedralGroupIndex].types.amber3
            );
            yield break;
        }

        // Failed - cannot use torsions in mutation

        CustomLogger.LogFormat(
            EL.ERROR,
            "Unable to recalculate Torsion Parameter '{0}-{1}-{2}-{3}'!",
            ambers[0], ambers[1], ambers[2], ambers[3]
        );
    }

    IEnumerator RecomputeTorsionParameter(Residue residue, int dihedralGroupIndex, Amber[] ambers) {
        
        CustomLogger.LogFormat(
            EL.INFO,
            "Torsion Parameter '{0}-{1}-{2}-{3}' missing - recomputing...",
            ambers[0], ambers[1], ambers[2], ambers[3]
        );

        Geometry tempGeo = PrefabManager.InstantiateGeometry(transform);
        tempGeo.parameters.UpdateParameters(parameters);
        Residue clonedResidue = residue.Take(tempGeo);
        tempGeo.AddResidue(residue.residueID, clonedResidue);

        yield return tempGeo.parameters.Calculate2();

        parameters.UpdateParameters(tempGeo.parameters);

        torsions[dihedralGroupIndex] = parameters.GetTorsion(ambers[0], ambers[1], ambers[2], ambers[3], true);

        GameObject.Destroy(tempGeo.gameObject);

        yield return null;
    }

    void GetSimilarTorsionParameter(int dihedralGroupIndex, Amber[] ambers) {

        Element element0 = dihedralGroupPDBIDs[dihedralGroupIndex][0].element;
        Element element3 = dihedralGroupPDBIDs[dihedralGroupIndex][3].element;

        string element0Str = element0.ToString();
        string element3Str = element3.ToString();

        List<Amber> element0Ambers = ((Amber[]) System.Enum.GetValues(typeof(Amber)))
            .Where(x => System.Enum.GetName(typeof(Amber), x).Substring(0, 1) == element0Str)
            .ToList();

        List<Amber> element3Ambers = ((Amber[]) System.Enum.GetValues(typeof(Amber)))
            .Where(x => System.Enum.GetName(typeof(Amber), x).Substring(0, 1) == element3Str)
            .ToList();

        torsions[dihedralGroupIndex] = parameters.torsions
            .Where(kvp => 
                (
                    kvp.Key.amber1 == ambers[1] &&
                    kvp.Key.amber2 == ambers[2] &&
                    element0Ambers.Contains(kvp.Key.amber0) &&
                    element3Ambers.Contains(kvp.Key.amber3)
                ) || (
                    kvp.Key.amber2 == ambers[1] &&
                    kvp.Key.amber1 == ambers[2] &&
                    element0Ambers.Contains(kvp.Key.amber3) &&
                    element3Ambers.Contains(kvp.Key.amber0)
                )
            )
            .Select(x => x.Value)
            .FirstOrDefault();
    }

    void GetAnyTorsionParameter(int dihedralGroupIndex, Amber[] ambers) {
        torsions[dihedralGroupIndex] = parameters.GetTorsion(Amber._, ambers[1], ambers[2], Amber._, true);
    }

    public void SetDihedralsToZero(float3[] positions) {
        SetDihedrals(positions, new float[numDihedralGroups]);
    }

    public void SetDihedrals(float3[] positions, float[] newValues) {

        if (newValues.Length != numDihedralGroups) {
            throw new System.IndexOutOfRangeException($"Length of newValues '{newValues.Length}' must equal numDihedralGroups '{numDihedralGroups}'");
        }
        
        for (int dihedralGroupIndex=0; dihedralGroupIndex<numDihedralGroups; dihedralGroupIndex++) {

            int[] indices = dihedralGroups[dihedralGroupIndex];

            CustomMathematics.DihedralRuler dihedralRuler = new CustomMathematics.DihedralRuler(
                positions[indices[0]],
                positions[indices[1]],
                positions[indices[2]],
                positions[indices[3]]
            );

            dihedralRuler.RotateIP(
                positions,
                newValues[dihedralGroupIndex] - dihedralRuler.dihedral,
                residueClashGroup.GetMask(dihedralGroupIndex + 2)
            );

        }
    }

    public (float, bool) GetClashScore(Residue residue) {

        int size = residueClashGroup.size;
        if (residue.size != residueClashGroup.size) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Inconsistent size between Residue ({0}) and Dihedral Scanner ({1})! Cannot compute Clash Score!",
                residue.size,
                size
            );
            failed = true;
            return (0, true);
        }

        float3[] positions = new float3[size];

        for (int i=0; i<size; i++) {

            PDBID pdbID = residueClashGroup.pdbIDs[i];

            Atom atom;

            if (!residue.TryGetAtom(pdbID, out atom)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "PDBID '{0}' not present in Residue {1} ({2})! Cannot compute Clash Score!",
                    pdbID,
                    residue.residueID,
                    residue.residueName
                );
                failed = true;
                return (0, true);
            }

            positions[i] = atom.position;
        }

        float score = 0f;
        bool clash = false;
        
        // Get all previous dihedrals
        for (int dihedralGroupIndex=0; dihedralGroupIndex<numDihedralGroups; dihedralGroupIndex++) {

            int[] previousIndices = dihedralGroups[dihedralGroupIndex];

            CustomMathematics.DihedralRuler dihedralRuler = new CustomMathematics.DihedralRuler(
                positions[previousIndices[0]],
                positions[previousIndices[1]],
                positions[previousIndices[2]],
                positions[previousIndices[3]]
            );

            int identifier = dihedralGroupIndex + 2;
            (float groupScore, bool groupClash) = GetClashScore(positions, identifier, dihedralRuler.dihedral);

            score += groupScore;
            clash |= groupClash;
        }

        return (score, clash);
    }

    public (float, bool) GetClashScore(float3[] positions, int identifier, float dihedral) {

        float score = 0;

        float maxNonbonCutoff2 = Settings.maxNonBondingCutoff * Settings.maxNonBondingCutoff;

        bool lastGroup = identifier > numDihedralGroups;

        Torsion torsion = torsions[identifier - 2];

        if (!torsion.IsDefault()) {
            score += CustomMathematics.ETorsion(
                dihedral, 
                torsion.barrierHeights, 
                torsion.phaseOffsets, 
                0
            );
        }

        /*
        Dihedral Group Indices:           0  1  2
        Identifiers              0  0  1  2  3  4  5
        Heavy atoms              N -C -CA-CB-CG-CD-CE
        Hydrogens                      HA HB HG HD HE
        */

        ResidueID residueID = residueClashGroup.residueID;

        /*
        Be careful to only use parameter info from residueClashGroup - positional info is passed in as arg
        */

        for (int m0=0; m0<residueClashGroup.size; m0++) {


            bool isH = residueHydrogens[m0];
            int identifier0 = residueIdentifiers[m0];

            //Get atoms that contribute to score
            if (lastGroup) {
                //Include all atoms with same or greater identifier in last group
                //Also include previous H

                if (isH) {
                    if (identifier0 - 1 < identifier) {
                        continue;
                    }
                } else {
                    if (identifier0 < identifier) {
                        continue;
                    }
                }
            } else {
                //Include all atoms with same identifier in last group
                //Also include previous H

                if (isH) {
                    if (identifier0 + 1 != identifier) {
                        continue;
                    }
                } else {
                    if (identifier0 != identifier) {
                        continue;
                    }
                }
            }

            // Skip atoms that have an identifier greater than the currently tested identifier
            if (!lastGroup && identifier0 - identifier > 1) {
                continue;
            }

            float3 position = positions[m0];

            for (int m1=0; m1<residueClashGroup.size; m1++) {

                int identifier1 = residueIdentifiers[m1];

                // Skip atoms that have an identifier greater than the currently tested identifier
                if (!lastGroup && identifier1 - identifier > 1) {
                    continue;
                }

                //Skip atoms with neighbouring or greater identifier
                //Also skips same atom
                //Stops the score from going too wild from connected atoms
                if (identifier0 - identifier1 < 2) {
                    continue;
                }

                float r2 = math.distancesq(
                    position,
                    positions[m1]
                );

                if (r2 < residueResidueClashDistancesSq[m0, m1]) {
                    return (0f, true);
                }

                float vdwR2 = residueResidueRadiiSq[m0, m1];
                float vdwV = residueResidueWellDepths[m0, m1];
                float charge = residueResidueScaledCharges[m0, m1];
                score += CustomMathematics.EVdWAmberSquared(r2, vdwR2, vdwR2)
                    +  CustomMathematics.EElectrostaticR1Squared(r2, charge);

            }

            if (identifier0 < 2) {
                continue;
            }

            for (int m1=0; m1<numNearbyAtoms; m1++) {

                float r2 = math.distancesq(
                    position,
                    nearbyPositions[m1]
                );

                // Skip interactions that are too far away
                if (r2 > maxNonbonCutoff2) {
                    continue;
                }
                
                //Check that atoms don't clash (would be considered bonded by distance)
                if (r2 < residueNearbyClashDistancesSq[m0, m1]) {
                    return (0f, true);
                }

                float vdwR2 = residueNearbyRadii[m0, m1];
                float vdwV = residueNearbyWellDepths[m0, m1];
                float charge = residueNearbyScaledCharges[m0, m1];
                score += CustomMathematics.EVdWAmberSquared(r2, vdwV, vdwR2)
                    +  CustomMathematics.EElectrostaticR1Squared(r2, charge);
            }

        }

        return (score, false);
    }

    public IEnumerator Optimise(float deltaTheta, ResidueMutator.OptimisationMethod optimisationMethod) {
        
        // Check method
        switch (optimisationMethod) {
            case (ResidueMutator.OptimisationMethod.NONE):
                break;
            case (ResidueMutator.OptimisationMethod.TREE):
            case (ResidueMutator.OptimisationMethod.BRUTE_FORCE):
                break;
            default:
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Method '{0}' not available",
                    optimisationMethod
                );
                yield break;
        }

        this.optimisationMethod = optimisationMethod;

        if (numDihedralGroups == 0) {
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Mutant group has no dihedral groups - no optimisation will occur."
            );
            yield break;
        }

        int numAtoms = residueClashGroup.size;
        float3[] positions = residueClashGroup.positions.ToArray();

        SetDihedralsToZero(positions);

        int steps = (int)(360 / deltaTheta);

        float deltaRad = math.radians(deltaTheta);

        scanSize = CustomMathematics.IntPow(steps, numDihedralGroups);

        int[] index1s = dihedralGroups.Select(x => x[1]).ToArray();
        int[] index2s = dihedralGroups.Select(x => x[2]).ToArray();

        // Store the current steps
        int[] stepArray = new int[numDihedralGroups];

        // Masks of mobile atoms for each dihedral group
        bool[][] masks = new bool[numDihedralGroups][];
        for (int groupIndex=0; groupIndex<numDihedralGroups; groupIndex++) {
            masks[groupIndex] = residueClashGroup.GetMask(groupIndex+2);
        }

        /// <summary>
        /// Scan the current depth recursively
        /// </summary>
        /// <param name="currentDepth">How deep in the tree the scan is.</param>
        /// <param name="accumulatedScore">Current clash score up to this point</param>
        /// <returns>The score up to this point and whether any atoms clash</returns>
        IEnumerable<(float accumulatedScore, bool clash)> Scan(int currentDepth, float accumulatedScore) {
            
            // Exceeded depth of tree - return
            if (currentDepth >= numDihedralGroups) {
                yield return (accumulatedScore, false);
            }

            // All identifiers below this are immobilised
            int fixedIdentifier = currentDepth + 2;

            // Get the indices of the atoms allowed to rotate
            bool[] mask = masks[currentDepth];

            // Index of Atom 1
            int index1 = index1s[currentDepth];

            // Index of Atom 2
            int index2 = index2s[currentDepth];

            // Positions of the central atoms in the dihedral
            float3 p1 = positions[index1];
            float3 p2 = positions[index2];;

            // Axis to rotate around
            float3 axis = math.normalize(p1 - p2);

            // Quaternion to rotate by
            quaternion forwardRotation = quaternion.AxisAngle(
                axis,
                deltaRad
            );
            
            quaternion backwardRotation = quaternion.AxisAngle(
                axis,
                -deltaRad
            );

            void StepForward() {
                for (int atomIndex=0; atomIndex<numAtoms; atomIndex++) {
                    // Ignore masked atoms
                    if (! mask[atomIndex]) {continue;}
                    positions[atomIndex] = math.rotate(forwardRotation, positions[atomIndex] - p2) + p2;
                }
            }

            void StepBackward() {
                for (int atomIndex=0; atomIndex<numAtoms; atomIndex++) {
                    // Ignore masked atoms
                    if (! mask[atomIndex]) {continue;}
                    positions[atomIndex] = math.rotate(backwardRotation, positions[atomIndex] - p2) + p2;
                }
            }


            int initialStep;
            if (optimisationMethod == ResidueMutator.OptimisationMethod.BRUTE_FORCE) {
                // Brute force: start from 0 as normal
                initialStep = 0;
            } else {
                // Tree method: needs one more step to determine if first dihedral is a minimum
                initialStep = -1;

                // Rotate atoms backwards one step
                StepBackward();

            }
            
            bool firstStep = true;
            bool scoreDecreased = false;
            float previousScore = 0f;

            for (int step=initialStep; step<steps; step++) {

                // Keep track of where we are in the tree
                stepArray[currentDepth] = step;

                // Get score and clash
                (float score, bool clash) = GetClashScore(positions, fixedIdentifier, step * deltaRad);
                
                // Branch algorithms here:
                // Tree - recursive search of numerical minima
                // Brute Force - recursive search of all non-clash dihedrals

                if (optimisationMethod == ResidueMutator.OptimisationMethod.BRUTE_FORCE) {
                    // Brute force - return all branches
                    if (clash) {
                        // Clashed - kill branch
                        yield return (accumulatedScore + score, true);
                    } else if (currentDepth + 1 == numDihedralGroups) {
                        // Reached the end of the tree - break out of recursion
                        yield return (accumulatedScore + score, false);
                    } else {

                        // Recursion
                        foreach ((float nextScore, bool nextClash) in Scan(currentDepth + 1, accumulatedScore + score)) {
                            yield return (nextScore, nextClash);
                        }
                    }
                } else {
                    // Tree method - return minima of each tree depth

                    if (firstStep) {
                        //Skip first step to get previous score
                        firstStep = false;
                    } else {
                        if (clash) {
                            // Count clash as score increase so next step isn't a minimum
                            scoreDecreased = false;
                            yield return (accumulatedScore + score, true);
                        } else if (score <= previousScore) {
                            // Score went down
                            scoreDecreased = true;
                        }  else {
                            if (scoreDecreased) {
                                // Score went up, but previously decreased - minimum

                                if (currentDepth + 1 == numDihedralGroups) {
                                    // Reached the end of the tree - return previous step
                                    stepArray[currentDepth]--;
                                    yield return (accumulatedScore + previousScore, false);
                                } else {
                                    // Step backwards to get to previous dihedral with better score
                                    StepBackward();
                                    stepArray[currentDepth]--;

                                    // Scan next tree depth
                                    foreach ((float nextScore, bool nextClash) in Scan(currentDepth+1, accumulatedScore + previousScore)) {
                                        yield return (nextScore, nextClash);
                                    }

                                    // Step forward to bring positions back to where they were
                                    StepForward();
                                    stepArray[currentDepth]++;

                                    // Reset step array for next tree depth
                                    stepArray[currentDepth + 1] = 0;
                                }
                            }
                            scoreDecreased = false;
                        }
                    }
                }
                
                previousScore = score;

                // Perform rotation
                StepForward();
            }
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        stepAccepted=false;
        scanned = 0;
        clashes = 0;

        int[] bestSteps = new int[numDihedralGroups];
        foreach ((float accumulatedScore, bool clash) in Scan(0, 0f)) {
            scanned++;
            if (!clash) {
                if (!stepAccepted) {
                    // Make sure first non-clash is registered
                    stepAccepted = true;

                    // Keep track of best dihedrals and score so far
                    System.Array.Copy(stepArray, bestSteps, numDihedralGroups);
                    bestScore = accumulatedScore;
                } else if (accumulatedScore < bestScore) {
                    // Keep track of best dihedrals and score so far
                    System.Array.Copy(stepArray, bestSteps, numDihedralGroups);
                    bestScore = accumulatedScore;
                }
            } else {
                clashes++;
            }
            
            // Progress update
            if (Timer.yieldNow) {

                int progress = 0;
                for (int step=0; step<numDihedralGroups; step++) {
                    progress += stepArray[step] * CustomMathematics.IntPow(steps, numDihedralGroups-step-1);
                }

                NotificationBar.SetTaskProgress(
                    TID.MUTATE_RESIDUE, 
                    CustomMathematics.Map(progress, 0, scanSize, 0.2f, 0.95f)
                );
                sb.Clear();
                sb.Append("[ ");
                foreach (int step in stepArray) {
                    sb.Append($"{step:00} ");
                }
                sb.Append($"]: {bestScore:0.0000}");
                NotificationBar.SetTaskText(sb.ToString());
                yield return null;
            }

        }

        // Convert steps to dihedrals
        bestDihedrals = bestSteps.Select(x => x * deltaRad).ToArray();

        // Print results in log
        PrintResults();

    }

    public void PrintResults() {
        
        System.Text.StringBuilder resultsSB = new System.Text.StringBuilder();
        resultsSB.AppendLine("Optimisation results:");
        switch (optimisationMethod) {
            case (ResidueMutator.OptimisationMethod.TREE):
                resultsSB.AppendLine("Type: Tree");
                resultsSB.AppendLine($"Total: {scanSize}. Scanned: {scanned}. Clashes: {clashes}");
                break;
            case (ResidueMutator.OptimisationMethod.BRUTE_FORCE):
                resultsSB.AppendLine("Type: Brute-Force");
                resultsSB.AppendLine($"Total: {scanSize}. Scanned: {scanned}. Clashes: {clashes + scanSize - scanned}");
                break;
        } 
        if (stepAccepted) {
            resultsSB.AppendLine($"New Residue '{residueClashGroup.residueID}' dihedrals (degrees):");
            for (int dihedralGroupIndex=0; dihedralGroupIndex<numDihedralGroups; dihedralGroupIndex++) {

            }
            foreach (int dihedralGroupIndex in Enumerable.Range(0, numDihedralGroups)) {
                resultsSB.AppendLine(string.Format(
                    "'{0}': {1}",
                    string.Join("'-'", dihedralGroupPDBIDs[dihedralGroupIndex]),
                    math.degrees(bestDihedrals[dihedralGroupIndex])
                ));
            }
        } else {
            resultsSB.AppendLine($"Optimisation of Residue '{residueClashGroup.residueID}' failed!");
        }
        CustomLogger.LogFormat(
            EL.INFO,
            resultsSB.ToString()
        );
    }

    public void UpdatePositions(Residue residue) {

        // Check there is actually a set of dihedrals that doesn't clash
        if (stepAccepted) {

            for (int dihedralGroupIndex=0; dihedralGroupIndex<numDihedralGroups; dihedralGroupIndex++) {
                float bestDihedral = bestDihedrals[dihedralGroupIndex];

                PDBID[] dihedralPDBIDs = dihedralGroupPDBIDs[dihedralGroupIndex];

                CustomMathematics.DihedralRuler dihedralRuler = new CustomMathematics.DihedralRuler(
                    residue.GetAtom(dihedralPDBIDs[0]),
                    residue.GetAtom(dihedralPDBIDs[1]),
                    residue.GetAtom(dihedralPDBIDs[2]),
                    residue.GetAtom(dihedralPDBIDs[3])
                );

                bool[] mask = residueClashGroup.GetMask(dihedralGroupIndex + 2);

                for (int index=0; index<residueClashGroup.size; index++) {
                    if (!mask[index]) {continue;}
                    PDBID pdbID = residueClashGroup.pdbIDs[index];
                    float3 position = residue.GetAtom(pdbID).position;
                    residue.GetAtom(pdbID).position = dihedralRuler.Rotate(position, bestDihedral - dihedralRuler.dihedral);
                }

            }

        } else if (numDihedralGroups == 0) {
            CustomLogger.LogFormat(
                EL.INFO,
                "Residue has no dihedral groups to optimise."
            );
        } else {
           throw new System.Exception(
                "Optimisation failed! No configurations found without clashes."
            );
        }
    }

}

/// <summary>
/// Special Residue-like class that stores Atom info in arrays
/// </summary>
public class ClashGroup {
    
    public ClashGroupAtom[] groupAtoms;
    public ResidueID residueID;
    public PDBID[] pdbIDs;
    public int size;
    public int[] identifers;

    public float3[] positions;

    public ClashGroup(Residue residue, Parameters parameters) {

        residueID = residue.residueID;
        size = residue.size;
        pdbIDs = new PDBID[size];
        groupAtoms = new ClashGroupAtom[size];
        identifers = new int[size];
        positions = new float3[size];

        int index = 0;
        foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
            pdbIDs[index] = pdbID;
            ClashGroupAtom cga = groupAtoms[index] = new ClashGroupAtom(pdbID, atom, parameters);
            positions[index] = cga.position;
            identifers[index] = cga.identifer;
            index++;
        }

    }

    public int IndexOf(PDBID pdbID) {
        return System.Array.IndexOf(pdbIDs, pdbID);
    }

    public bool[] GetMask(int fixedIdentifier) {
        bool[] mask = new bool[size];
        for (int i=0; i<size; i++) {
            int identifer = identifers[i];
            mask[i] = (pdbIDs[i].element == Element.H) 
                ? identifer >= fixedIdentifier 
                : identifer > fixedIdentifier;
        }
        return mask;
    }

}

public readonly struct ClashGroupAtom {
    
    static List<Amber> missingAmbers = new List<Amber>();

    // These are the identifiers that remain fixed during the scans
    // They form the mask to stop atoms from rotating
    // They start from the backbone and grow down the chain
    public static readonly string[] allIdentifiers = new string[] {"", "A", "B", "G", "D", "E", "Z", "H"};


    public readonly float3 position; 
    public readonly float charge;
    public readonly Amber amber;
    public readonly PDBID pdbID;
    public readonly float wellDepth;
    public readonly float radius;
    public readonly int identifer;

    public ClashGroupAtom(PDBID pdbID, Atom atom, Parameters parameters) {
        position = atom.position;
        charge = atom.partialCharge;
        amber = atom.amber;

        this.pdbID = pdbID;
        identifer = GetIdentifier(pdbID);
        
        AtomicParameter atomicParameter;
        if (!parameters.TryGetAtomicParameter(amber, out atomicParameter)) {
            //Parameters doesn't have Amber

            //Try making a secondary Amber from the element
            Amber elementAmber;
            if (AmberCalculator.TryGetAmber(pdbID.element.ToString(), out elementAmber)) {
                if (
                    ! parameters.TryGetAtomicParameter(elementAmber, out atomicParameter)
                ) {
                    if (! missingAmbers.Contains(amber)) {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "Parameters doesn't have Amber type '{0}' or backup type '{1}'. Using generic values to check for clashes",
                            amber,
                            elementAmber
                        );
                        missingAmbers.Add(amber);
                    }
                    wellDepth = 0.1f * Data.kcalToHartree;
                    radius = 2f;
                    return;
                }
            } else {
                //Can't make a secondary Amber
                if (! missingAmbers.Contains(amber)) {
                    CustomLogger.LogFormat(
                        EL.WARNING,
                        "Parameters doesn't have Amber type '{0}'. Using generic values to check for clashes",
                        amber
                    );
                    missingAmbers.Add(amber);
                }
                wellDepth = 0.1f * Data.kcalToHartree;
                radius = 2f;
                return;
            }
        }
        wellDepth = atomicParameter.wellDepth * Data.kcalToHartree;
        radius = atomicParameter.radius;
    }

    public static int GetIdentifier(PDBID pdbID) {
        return System.Array.IndexOf(
            allIdentifiers, 
            (pdbID.identifier == "")
                ? "" 
                : pdbID.identifier.Substring(0, 1)
            );
    }

}

