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

    public enum OptisationMethod {NONE, TREE, BRUTE_FORCE};
    
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
    public IEnumerator MutateStandard(Residue targetResidue, float deltaTheta, OptisationMethod optisationMethod=OptisationMethod.TREE) {

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
        

        if (optisationMethod == OptisationMethod.NONE) {
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "No dihedral optimisation."
            );
        } else {
            yield return Optimise(targetResidue, deltaTheta, optisationMethod);
            if (failed) {
                yield return NotificationBar.UpdateClearTask(TID.MUTATE_RESIDUE);
                yield break;
            }
        }


        oldResidue.residueName = targetResidue.residueName;

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
            Quaternion.AngleAxis(dihedral * Mathf.Rad2Deg, bondVector), 
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

    IEnumerator Optimise(Residue targetResidue, float deltaTheta, OptisationMethod optisationMethod=OptisationMethod.TREE) {


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

        if (optisationMethod == OptisationMethod.TREE) {
            yield return dihedralScanner.GetBestDihedrals(deltaTheta);
        } else if (optisationMethod == OptisationMethod.BRUTE_FORCE) {
            yield return dihedralScanner.BruteForceOptimise(deltaTheta);
        }
        yield return NotificationBar.UpdateTaskProgress(TID.MUTATE_RESIDUE, 0.95f);
        
        clashScore = dihedralScanner.bestScore;

        try {
            dihedralScanner.UpdatePositions(oldResidue);
        } catch (System.Exception e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Optimisation failed! - {0}",
                e.Message
            );
        }

    }
}

    
public class DihedralScanner : MonoBehaviour {

    public ClashGroup residueClashGroup;
    public Dictionary<ResidueID, ClashGroup> geometryClashGroups;

    PDBID[][] dihedralGroupPDBIDs;
    int[][] dihedralGroups;
    Torsion[] torsions;
    float[] currentDihedrals;
    public int numDihedralGroups;

    public Parameters parameters;

    public List<float> scores;
    
    public int bestIndex;
    public float bestScore;

    public List<float[]> acceptedDihedrals;

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

        scores = new List<float>();
    }

    public IEnumerator SetResidue(Residue residue) {

        // Get the Amino Acid group for this residue
        AminoAcid aminoAcid;
        if (!Data.aminoAcids.TryGetValue(residue.residueName, out aminoAcid)) {
            CustomLogger.LogOutput(
                "Amino Acid '{0}' not present in Database - cannot check for clashes!",
                residue.residueName
            );
            failed = true;
            yield break;
        }

        // Get the PDB IDs for each rotateable dihedral in the residue's sidechain
        dihedralGroupPDBIDs = aminoAcid.GetDihedralPDBIDs(residue.state);
        numDihedralGroups = dihedralGroupPDBIDs.Count();

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Using Reference Amino Acid '{0}' with Residue State '{1}' ({2} Dihedral Groups)",
            aminoAcid.family,
            residue.state,
            numDihedralGroups
        );
        
        torsions = new Torsion[numDihedralGroups];
        currentDihedrals = new float[numDihedralGroups];

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

        acceptedDihedrals = new List<float[]>();
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

    public IEnumerator BruteForceOptimise(float deltaTheta) {

        scores = new List<float>();
        int acceptedStepIndex = 0;

        if (numDihedralGroups == 0) {
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Mutant group has no dihedral groups - no optimisation will occur."
            );
            yield break;
        }
        
        int size = residueClashGroup.size;
        float3[] positions = new float3[size];
        System.Array.Copy(residueClashGroup.positions, positions, size);

        // The increments around the dihedral
        int steps = (int)(360 / deltaTheta);

        float pi2 = math.PI * 2;
        float angleRad = math.radians(deltaTheta);

        int scanSize = CustomMathematics.IntPow(steps, numDihedralGroups);
        
        bool[][] masks = new bool[numDihedralGroups][];
        for (int groupIndex=0; groupIndex<numDihedralGroups; groupIndex++) {
            masks[groupIndex] = residueClashGroup.GetMask(groupIndex+2);
        }

        int[] index1s = dihedralGroups.Select(x => x[1]).ToArray();
        int[] index2s = dihedralGroups.Select(x => x[2]).ToArray();

        IEnumerable<(float3[] positions, int groupIndex, float dihedral)> ScanNext(int groupIndex) {
            
            float currentDihedral = currentDihedrals[groupIndex];
            
            if (groupIndex < numDihedralGroups) {

                int fixedIdentifier = groupIndex + 2;

                // Get the indices of the atoms allowed to rotate
                bool[] mask = masks[groupIndex];

                // Index of Atom 1
                int index1 = index1s[groupIndex];

                // Index of Atom 2
                int index2 = index2s[groupIndex];

                // Positions of the central atoms in the dihedral
                float3 p1 = positions[index1];
                float3 p2 = positions[index2];;

                // Axis to rotate around
                float3 axis = math.normalize(p2 - p1);

                // Quaternion to rotate by
                quaternion rotation = quaternion.AxisAngle(
                    axis,
                    angleRad
                );

                for (int step=0; step<steps; step++) {

                    currentDihedral = currentDihedrals[groupIndex];
                    currentDihedral += angleRad;
                    currentDihedral = CustomMathematics.CircleWrap(currentDihedral, 0, pi2);

                    currentDihedrals[groupIndex] = currentDihedral;

                    for (int atomIndex=0; atomIndex<size; atomIndex++) {
                        // Ignore masked atoms
                        if (! mask[atomIndex]) {continue;}
                        positions[atomIndex] = (float3) math.rotate(rotation, positions[atomIndex] - p2) + p2;
                    }

                    foreach ((float3[] ps, int index, float nextDihedral) in ScanNext(groupIndex+1)) {
                        yield return (positions, groupIndex+1, nextDihedral);
                    }
                }
            } else {
                yield return (positions, groupIndex+1, currentDihedral);
            }
        }

        float3[] bestPositions = new float3[size];
        foreach ((float3[] currentPositions, int groupIndex, float dihedral) in ScanNext(0)) {

            (float score, bool clash) = GetClashScore(currentPositions, groupIndex+2, dihedral);
            
            if (!clash) {
                if (acceptedStepIndex == 0) {
                    bestScore = score;
                } else if (score < bestScore) {
                    bestScore = score;
                    System.Array.Copy(currentPositions, bestPositions, size);
                }
            }
            
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.MUTATE_RESIDUE, 
                    CustomMathematics.Map(acceptedStepIndex, 0, scanSize, 0.2f, 0.95f)
                );
                yield return null;
            }

            acceptedStepIndex++;
        }

        scores = new List<float>{bestScore};
    }

    public IEnumerator GetBestDihedrals(float deltaTheta) {

        scores = new List<float>();
        int acceptedStepIndex = 0;

        if (numDihedralGroups == 0) {
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Mutant group has no dihedral groups - no optimisation will occur."
            );
            yield break;
        }

        // Store all dihedrals
        // Outer dim - dihedral group to rotate (fixed to numDihedralGroups)
        // Middle dim - each minimum found by rotating around that dihedral (flexible)
        // Inner dim - angle of each dihedral in that dihedral group
        List<float[]>[] groupAngles = new List<float[]>[numDihedralGroups];
        List<float>[] groupScores = new List<float>[numDihedralGroups];
        
        for (int dihedralGroupIndex=0; dihedralGroupIndex<numDihedralGroups; dihedralGroupIndex++) {
            groupAngles[dihedralGroupIndex] = new List<float[]> {};
            groupScores[dihedralGroupIndex] = new List<float> {};
        }
        // Initialise positions with the original angles
        groupAngles[0].Add(new float[numDihedralGroups]);
        groupScores[0].Add(0f);

        // The increments around the dihedral
        int steps = (int)(360 / deltaTheta) + 2;
        
        float deltaRad = math.radians(deltaTheta);
        
        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Checking {0} dihedral groups. Scanning {1} degrees per group over {2} steps.",
            numDihedralGroups,
            deltaTheta,
            steps
        );

        // Outer loop for each dihedral group
        for (int dihedralGroupIndex=0, fixedIdentifier=2; dihedralGroupIndex<numDihedralGroups; dihedralGroupIndex++, fixedIdentifier++) {


            //Dihedral group is the group of PDBIDs consisting of 4 atoms (starting from Atom 0)
            int[] atomIndices = dihedralGroups[dihedralGroupIndex];
            
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Scanning group {0} ({1}-{2}-{3}-{4}) over {5} positions from previous group",
                () => new object[] {
                    dihedralGroupIndex,
                    dihedralGroupPDBIDs[dihedralGroupIndex][0],
                    dihedralGroupPDBIDs[dihedralGroupIndex][1],
                    dihedralGroupPDBIDs[dihedralGroupIndex][2],
                    dihedralGroupPDBIDs[dihedralGroupIndex][3],
                    groupAngles[dihedralGroupIndex].Count()
                }
            );

            // Get the indices of the atoms allowed to rotate
            bool[] mask = residueClashGroup.GetMask(fixedIdentifier);

            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Group {0} Axis: {1}-{2} ({3}-{4}). Freezing Identifiers: '{5}'. Mask:{7}             {6}",
                () => new object[] {
                    dihedralGroupIndex,
                    atomIndices[1],
                    atomIndices[2],
                    dihedralGroupPDBIDs[dihedralGroupIndex][1],
                    dihedralGroupPDBIDs[dihedralGroupIndex][2],
                    string.Join("' '", ClashGroupAtom.allIdentifiers.Where((x,i) => i <= fixedIdentifier)),
                    string.Join(" ", mask.Select(x => x ? "1   " : "0   ")),
                    FileIO.newLine
                }
            );

            foreach ((float[] angles, int subgroupIndex) in groupAngles[dihedralGroupIndex].Select((x,i)=>(x,i))) {
                
                float accumulatedScore = groupScores[dihedralGroupIndex][subgroupIndex];

                float3[] positions = residueClashGroup.positions.ToArray();

                // Perform all previous dihedral rotations
                for (int previousDGI=0; previousDGI<dihedralGroupIndex; previousDGI++) {
                    float previousDihedral = angles[previousDGI];

                    int[] previousIndices = dihedralGroups[previousDGI];

                    CustomMathematics.DihedralRuler dihedralRuler = new CustomMathematics.DihedralRuler(
                        positions[previousIndices[0]],
                        positions[previousIndices[1]],
                        positions[previousIndices[2]],
                        positions[previousIndices[3]]
                    );

                    dihedralRuler.RotateIP(
                        positions,
                        previousDihedral - dihedralRuler.dihedral,
                        residueClashGroup.GetMask(previousDGI + 2)
                    );
                }
                
                bool firstStep = true;
                bool scoreDecreased = false;
                float previousScore = 0f;

                foreach (
                    (int step, float dihedral, float score, bool clash) in 
                    DihedralScan(positions, deltaRad, steps, atomIndices, mask, fixedIdentifier)
                        .OrderBy(x => x.step)
                ) {

                    if (firstStep) {
                        //Skip first step to get previous score
                        firstStep = false;
                    } else {
                        if (clash) {
                            scoreDecreased = false;
                        } else if (score <= previousScore) {
                            scoreDecreased = true;
                        } else {

                            if (scoreDecreased) {
                                
                                //Score went down then up - keep

                                angles[dihedralGroupIndex] = dihedral - deltaRad;

                                //Last iteration - these are the best positions
                                if (dihedralGroupIndex == numDihedralGroups - 1) {

                                    scores.Add(previousScore + accumulatedScore);
                                    acceptedDihedrals.Add(angles.ToArray());

                                    if (acceptedStepIndex == 0) {
                                        bestIndex = 0;
                                        bestScore = previousScore;
                                    } else if (previousScore < bestScore) {
                                        bestIndex = acceptedStepIndex;
                                        bestScore = previousScore;
                                    }
                                    acceptedStepIndex++;

                                } else {
                                    groupAngles[dihedralGroupIndex + 1].Add(angles.ToArray());
                                    groupScores[dihedralGroupIndex + 1].Add(accumulatedScore + previousScore);
                                }
                
                            }

                            scoreDecreased = false;

                        }
                    }

                    previousScore = score;

                    if (Timer.yieldNow) {
                        yield return null;
                    } else {
                    }

                }
            }
            
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.MUTATE_RESIDUE, 
                    CustomMathematics.Map(dihedralGroupIndex, 0, numDihedralGroups, 0.2f, 0.95f)
                );
                yield return null;
            }
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

        for (int m0=0; m0<residueClashGroup.size; m0++) {

            ClashGroupAtom residueAtom0 = residueClashGroup.groupAtoms[m0];
            PDBID pdbID0 = residueAtom0.pdbID;
            Element element = pdbID0.element;
            bool isH = element == Element.H;

            //Get atoms that contribute to score
            if (lastGroup) {
                //Include all atoms with same or greater identifier in last group
                //Also include previous H

                if (isH) {
                    if (residueAtom0.identifer - 1 < identifier) {
                        continue;
                    }
                } else {
                    if (residueAtom0.identifer < identifier) {
                        continue;
                    }
                }
            } else {
                //Include all atoms with same identifier in last group
                //Also include previous H

                if (isH) {
                    if (residueAtom0.identifer + 1 != identifier) {
                        continue;
                    }
                } else {
                    if (residueAtom0.identifer != identifier) {
                        continue;
                    }
                }
            }

            // Skip atoms that have an identifier greater than the currently tested identifier
            if (!lastGroup && residueAtom0.identifer - identifier > 1) {
                continue;
            }

            float3 position = positions[m0];
            float scaledCharge = residueAtom0.charge / parameters.dielectricConstant;
            
            for (int m1=0; m1<residueClashGroup.size; m1++) {

                ClashGroupAtom residueAtom1 = residueClashGroup.groupAtoms[m1];
                
                // Skip atoms that have an identifier greater than the currently tested identifier
                if (!lastGroup && residueAtom1.identifer - identifier > 1) {
                    continue;
                }

                //Skip atoms with neighbouring or greater identifier
                //Also skips same atom
                //Stops the score from going too wild from connected atoms
                if (residueAtom0.identifer - residueAtom1.identifer < 2) {
                    continue;
                }

                float r2 = math.distancesq(
                    position,
                    positions[m1]
                );
                
                //Check that atoms don't clash (would be considered bonded by distance)
                if (Data.GetBondOrderDistanceSquared(
                        element, 
                        residueAtom1.pdbID.element,
                        r2
                    ) != BT.NONE
                ) {
                    return (0f, true);
                }

                float vdwR2 = CustomMathematics.Squared(residueAtom0.radius + residueAtom1.radius);
                float vdwV = Mathf.Sqrt (residueAtom0.wellDepth + residueAtom1.wellDepth);
                score += CustomMathematics.EVdWAmberSquared(r2, vdwV, vdwR2)
                    +  CustomMathematics.EElectrostaticR1Squared(r2, scaledCharge * residueAtom1.charge);

            }

            if (residueAtom0.identifer < 2) {
                continue;
            }

            //Include interactions between residue and nearby residues
            //foreach (ClashGroupAtom nearbyAtom in nearbyClashGroup.groupAtoms) {
            foreach ((ResidueID nearbyResidueID, ClashGroup nearbyClashGroup) in geometryClashGroups) {

                if (residueID == nearbyResidueID) {
                    continue;
                }

                for (int n=0; n<nearbyClashGroup.size; n++) {

                    ClashGroupAtom nearbyAtom = nearbyClashGroup.groupAtoms[n];

                    float r2 = math.distancesq(
                        position,
                        nearbyAtom.position
                    );

                    // Skip interactions that are too far away
                    if (r2 > maxNonbonCutoff2) {
                        continue;
                    }
                    
                    //Check that atoms don't clash (would be considered bonded by distance)
                    if (Data.GetBondOrderDistanceSquared(
                            element, 
                            nearbyAtom.pdbID.element,
                            r2
                        ) != BT.NONE
                    ) {
                        return (0f, true);
                    }

                    float vdwR2 = CustomMathematics.Squared(residueAtom0.radius + nearbyAtom.radius);
                    float vdwV = Mathf.Sqrt (residueAtom0.wellDepth + nearbyAtom.wellDepth);
                    score += CustomMathematics.EVdWAmberSquared(r2, vdwV, vdwR2)
                        +  CustomMathematics.EElectrostaticR1Squared(r2, scaledCharge * nearbyAtom.charge);

                }

            }

        }

        return (score, false);
    }

    IEnumerable<(int step, float dihedral, float score, bool clash)> DihedralScan(
        float3[] positions,
        float deltaRad,
        int steps,
        int[] indices,
        bool[] mask,
        int fixedIdentifier
    ) {

        int size = residueClashGroup.size;

        CustomMathematics.DihedralRuler dihedralRuler = new CustomMathematics.DihedralRuler(
            positions[indices[0]],
            positions[indices[1]],
            positions[indices[2]],
            positions[indices[3]]
        );

        float pi2 = math.PI * 2;

        //Set angle to 0
        dihedralRuler.RotateIP(positions, -dihedralRuler.dihedral, mask);
        //float currentDihedral = 0;

        return Enumerable.Range(0, steps)
            .AsParallel()
            .Select(step => {
                float currentDihedral = CustomMathematics.CircleWrap(step * deltaRad, 0, pi2);
                float3[] rotatedPositions = dihedralRuler.Rotate(
                    positions, 
                    currentDihedral,
                    mask
                );
                
                (float score, bool clash) = GetClashScore(rotatedPositions, fixedIdentifier, currentDihedral);
                return (step, currentDihedral, score, clash);
            });
        
        // Quaternion to rotate by
//        quaternion rotation = dihedralRuler.GetRotation(deltaRad);
//
//        for (int step=0; step<steps; step++) {
//
//            currentDihedral += deltaRad;
//            currentDihedral = CustomMathematics.CircleWrap(currentDihedral, 0, pi2);
//
//            // Perform rotation
//            dihedralRuler.Rotate(positions, rotation, mask);
//
//            iTime = Time.realtimeSinceStartup;
//
//            //Get score for this rotation
//            (float score, bool clash) = GetClashScore(positions, fixedIdentifier);
//            flow.t2 += Time.realtimeSinceStartup - iTime;
//
//            yield return (currentDihedral, score, clash);
//            
//        }
        
    }

    public void UpdatePositions(Residue residue) {

        if (scores != null && scores.Count() > 0) {

            float[] bestAngles = acceptedDihedrals[bestIndex];

            float3[] bestPositions = residueClashGroup.positions.ToArray();
            
            for (int dihedralGroupIndex=0; dihedralGroupIndex<numDihedralGroups; dihedralGroupIndex++) {
                float previousDihedral = bestAngles[dihedralGroupIndex];

                int[] indices = dihedralGroups[dihedralGroupIndex];

                CustomMathematics.DihedralRuler dihedralRuler = new CustomMathematics.DihedralRuler(
                    bestPositions[indices[0]],
                    bestPositions[indices[1]],
                    bestPositions[indices[2]],
                    bestPositions[indices[3]]
                );

                dihedralRuler.RotateIP(
                    bestPositions,
                    previousDihedral - dihedralRuler.dihedral,
                    residueClashGroup.GetMask(dihedralGroupIndex + 2)
                );

            }

            if (residue.size != bestPositions.Length) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Inconsistent size between Residue ({0}) and Dihedral scanner ({1})!",
                    residue.size,
                    bestPositions.Length
                );
            }

            CustomLogger.LogFormat(
                EL.VERBOSE,
                "{0} dihedrals returned from Dihedral Scanner - best score: {1}.",
                scores.Count(),
                bestScore
            );


            for (int atomIndex=0; atomIndex<residue.size; atomIndex++) {
                PDBID pdbID = residueClashGroup.pdbIDs[atomIndex];
                if (Data.backbonePDBs.Contains(pdbID)) {
                    continue;
                }
                Atom atom;
                if (!residue.TryGetAtom(pdbID, out atom)) {
                    throw new System.Exception(string.Format(
                        "Couldn't find atom '{0}' in Residue '{1}'!",
                        pdbID,
                        residue.residueID
                    ));
                }

                residue.GetAtom(pdbID).position = bestPositions[atomIndex];
            }

        } else if (numDihedralGroups == 0) {
            CustomLogger.LogFormat(
                EL.INFO,
                "Residue has no dihedral groups to optimise."
            );
        } else {
            CustomLogger.LogFormat(
                EL.WARNING,
                "Optimisation failed! No configurations found without clashes."
            );
        }
    }

}

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

