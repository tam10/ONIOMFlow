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


public class ResidueMutator {

    Geometry geometry;
    Residue oldResidue;

    public bool failed;

    public float clashScore;
    public DihedralScanner dihedralScanner;

    public enum OptisationMethod {NONE, TREE, BRUTE_FORCE};
    
    static readonly IList<RS> validStates = new [] {RS.STANDARD, RS.C_TERMINAL, RS.N_TERMINAL};

    public ResidueMutator(Geometry geometry, ResidueID residueID) {
        
        if (geometry == null) {
            throw new System.Exception(string.Format(
                "Cannot Mutate '{0}' - Geometry is null!",
                residueID
            ));
        }

        if (!geometry.TryGetResidue(residueID, out oldResidue)) {
            throw new System.Exception(string.Format(
                "Cannot Mutate '{0}' - Residue not found in Geometry!",
                residueID
            ));
        }
        

        if (! validStates.Any(x => x == oldResidue.state)) {
            throw new System.Exception(string.Format(
                "Cannot Mutate '{0}' - Residue is not in a valid State for mutation. Must be one of: {0}",
                string.Join(", ", validStates.Select(x => Constants.ResidueStateMap[x]))
            ));
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
            "Mutating Residue '{0}' ('{1}' -> '{2}')",
            oldResidue.residueID,
            oldResidue.residueName,
            targetResidue.residueName
        );

        yield return AlignTargetResidue(targetResidue);
        if (failed) {yield break;}

        yield return ReplaceSideChain(targetResidue);
        if (failed) {yield break;}
        

        if (optisationMethod == OptisationMethod.NONE) {
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "No dihedral optimisation."
            );
        } else {
            yield return Optimise(targetResidue, deltaTheta, optisationMethod);
            if (failed) {yield break;}
        }

        oldResidue.residueName = targetResidue.residueName;
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
        List<PDBID> deleteList = oldResidue.pdbIDs.Where(x => !Data.backbonePDBs.Contains(x)).ToList();
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
            addList = targetResidue.pdbIDs.Where(x => !Data.backbonePDBs.Contains(x)).ToList();
        } else {
            addList = targetResidue.pdbIDs.Where(x => x.element != Element.H && !Data.backbonePDBs.Contains(x)).ToList();
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

        // Get all the nearby residues that might clash with new residue
        List<Residue> nearbyResidues = oldResidue.ResiduesWithinDistance(8)
            .Select(x => geometry.GetResidue(x))
            .ToList();

        try {
            dihedralScanner = new DihedralScanner(geometry, targetResidue, nearbyResidues);
        } catch (System.Exception e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to create Dihedral Scanner: {0}{1}{2}",
                e.Message,
                FileIO.newLine,
                e.StackTrace
            );
            failed = true;
            NotificationBar.ClearTask(TID.MUTATE_RESIDUE);
            yield break;
        }

        if (optisationMethod == OptisationMethod.TREE) {
            yield return dihedralScanner.GetBestDihedrals(deltaTheta);
        } else if (optisationMethod == OptisationMethod.BRUTE_FORCE) {
            yield return dihedralScanner.BruteForceOptimise(deltaTheta);
        }
        yield return NotificationBar.UpdateTaskProgress(TID.MUTATE_RESIDUE, 0.95f);
        
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

    
public class DihedralScanner {

    public SingleClashGroup residueClashGroup;
    public MultiClashGroup nearbyClashGroup;

    PDBID[][] dihedralGroups;
    Torsion[] torsions;
    float[] currentDihedrals;
    public int numDihedralGroups;

    Parameters parameters;

    public List<float3[]> acceptedPositions;
    public List<float> scores;
    

    public int bestIndex;
    public float bestScore;
    
    public DihedralScanner(Geometry geometry, Residue newResidue, List<Residue> nearbyResidues) {

        AminoAcid aminoAcid;
        if (!Data.aminoAcids.TryGetValue(newResidue.residueName, out aminoAcid)) {
            throw new System.Exception(string.Format(
                "Amino Acid '{0}' not present in Database - cannot check for clashes!",
                newResidue.residueName
            ));
        }

        dihedralGroups = aminoAcid.GetDihedralPDBIDs(newResidue.state);
        numDihedralGroups = dihedralGroups.Count();

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Using Reference Amino Acid '{0}' with Residue State '{1}' ({2} Dihedral Groups)",
            aminoAcid.family,
            newResidue.state,
            numDihedralGroups
        );
        
        if (geometry.parameters.IsEmpty()) {
            parameters = Settings.defaultParameters;
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Geometry Parameters empty - using Default Parameters",
                newResidue.residueName
            );
        } else {
            parameters = geometry.parameters;
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Using Geometry Parameters",
                newResidue.residueName
            );
        }
        
        torsions = new Torsion[numDihedralGroups];
        currentDihedrals = new float[numDihedralGroups];

        for (int dihedralGroupIndex=0; dihedralGroupIndex<numDihedralGroups; dihedralGroupIndex++) {
            Atom[] atoms = dihedralGroups[dihedralGroupIndex].Select(x => newResidue.GetAtom(x)).ToArray();
            float initialDihedral = CustomMathematics.GetDihedral(atoms[0], atoms[1], atoms[2], atoms[3]);
            if (initialDihedral < 0) {
                initialDihedral += math.PI * 2;
            }
            currentDihedrals[dihedralGroupIndex] = initialDihedral;

            Amber[] ambers = atoms.Select(x => x.amber).ToArray();
            torsions[dihedralGroupIndex] = parameters.GetTorsion(ambers[0], ambers[1], ambers[2], ambers[3], true);

        }

        residueClashGroup = new SingleClashGroup(newResidue, parameters);

        CustomLogger.LogFormat(
            EL.DEBUG,
            "Parameters for Mutated Residue:{1}Index: PDBID: WellDepth: Radius:  Charge:{1}{0}",
            () => new object[] {
                string.Join(
                    FileIO.newLine,
                    residueClashGroup.groupAtoms.Select(
                        (cga,index) => 
                        string.Format(
                            "{0,5} {1,6}  {2,8:#.##E+00}   {3,8:#.##E+00}  {4,8:#.##E+00}",
                            index,
                            cga.pdbID,
                            cga.wellDepth,
                            cga.radius,
                            cga.charge
                        )
                    )
                ),
                FileIO.newLine
            }
        );
        
        nearbyClashGroup = new MultiClashGroup(nearbyResidues, parameters);
        
        CustomLogger.LogFormat(
            EL.DEBUG,
            "Parameters for Nearby Residues:{1}Index: PDBID: WellDepth: Radius:  Charge:{1}{0}",
            () => new object[] {
                string.Join(
                    FileIO.newLine,
                    nearbyClashGroup.groupAtoms.Select(
                        (cga,index) => 
                        string.Format(
                            "{0,5} {1,6}  {2,8:#.##E+00}   {3,8:#.##E+00}  {4,8:#.##E+00}",
                            index,
                            cga.pdbID,
                            cga.wellDepth,
                            cga.radius,
                            cga.charge
                        )
                    )
                ),
                FileIO.newLine
            }
        );
        
        acceptedPositions = new List<float3[]>();
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

        int[] index1s = dihedralGroups.Select(x => residueClashGroup.IndexOf(x[1])).ToArray();
        int[] index2s = dihedralGroups.Select(x => residueClashGroup.IndexOf(x[2])).ToArray();

        IEnumerable<(float3[] positions, int groupIndex)> ScanNext(int groupIndex) {
            
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

                    float currentDihedral = currentDihedrals[groupIndex];
                    currentDihedral += angleRad;
                    if (currentDihedral > pi2) {
                        currentDihedral -= pi2;
                    }

                    currentDihedrals[groupIndex] = currentDihedral;

                    for (int atomIndex=0; atomIndex<size; atomIndex++) {
                        // Ignore masked atoms
                        if (! mask[atomIndex]) {continue;}
                        positions[atomIndex] = (float3) math.rotate(rotation, positions[atomIndex] - p2) + p2;
                    }

                    foreach ((float3[] ps, int index) in ScanNext(groupIndex+1)) {
                        yield return (positions, groupIndex+1);
                    }
                }
            } else {
                yield return (positions, groupIndex+1);
            }
        }

        float3[] bestPositions = new float3[size];
        foreach ((float3[] currentPositions, int groupIndex) in ScanNext(0)) {

            (float score, bool clash) = GetClashScore(currentPositions, groupIndex+2);
            
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
        acceptedPositions = new List<float3[]>{bestPositions};
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

        // Store all positions
        // Outer dim - dihedral group to rotate (fixed to numDihedralGroups)
        // Middle dim - each minimum found by rotating around that dihedral (flexible)
        // Inner dim - position of each atom in that dihedral group
        List<float3[]>[] groupPositions = new List<float3[]>[numDihedralGroups];
        
        for (int dihedralGroupIndex=0; dihedralGroupIndex<numDihedralGroups; dihedralGroupIndex++) {
            groupPositions[dihedralGroupIndex] = new List<float3[]> {};
        }
        // Initialise positions with the original positions
        groupPositions[0].Add(residueClashGroup.positions);

        // The increments around the dihedral
        int steps = (int)(360 / deltaTheta) + 2;
        
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
            PDBID[] dihedralGroup = dihedralGroups[dihedralGroupIndex];
            
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Scanning group {0} ({1}-{2}-{3}-{4}) over {5} positions from previous group",
                () => new object[] {
                    dihedralGroupIndex,
                    dihedralGroup[0],
                    dihedralGroup[1],
                    dihedralGroup[2],
                    dihedralGroup[3],
                    groupPositions[dihedralGroupIndex].Count()
                }
            );

            // Index of Atom 1
            PDBID pdbID1 = dihedralGroup[1];
            int i1 = residueClashGroup.IndexOf(pdbID1);

            // Index of Atom 2
            PDBID pdbID2 = dihedralGroup[2];
            int i2 =  residueClashGroup.IndexOf(pdbID2);

            // Get the indices of the atoms allowed to rotate
            bool[] mask =  residueClashGroup.GetMask(fixedIdentifier);

            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Group {0} Axis: {1}-{2} ({3}-{4}). Freezing Identifiers: '{5}'. Mask:{7}             {6}",
                () => new object[] {
                    dihedralGroupIndex,
                    i1,
                    i2,
                    pdbID1,
                    pdbID2,
                    string.Join("' '", ClashGroupAtom.allIdentifiers.Where((x,i) => i <= fixedIdentifier)),
                    string.Join(" ", mask.Select(x => x ? "1   " : "0   ")),
                    FileIO.newLine
                }
            );

            foreach (float3[] positions in groupPositions[dihedralGroupIndex]) {

                foreach (float3[] nextGroupPositions in EnumerateBestPositions(positions, deltaTheta, steps, i1, i2, mask, fixedIdentifier)) {

                    float3[] positionsClone = new float3[residueClashGroup.size];
                    System.Array.Copy(nextGroupPositions, positionsClone, residueClashGroup.size);

                    //Last iteration - these are the best positions
                    if (dihedralGroupIndex == numDihedralGroups - 1) {
                        (float score, bool clash) = GetClashScore(positionsClone, fixedIdentifier);
                        if (!clash) {
                            acceptedPositions.Add(positionsClone);

                            scores.Add(score);

                            if (acceptedStepIndex == 0) {
                                bestIndex = 0;
                                bestScore = score;
                            } else if (score < bestScore) {
                                bestIndex = acceptedStepIndex;
                                bestScore = score;
                            }
                            acceptedStepIndex++;

                        }
                    } else {
                        groupPositions[dihedralGroupIndex + 1].Add(positionsClone);
                    }

                    if (Timer.yieldNow) {
                        yield return null;
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

    (float, bool) GetClashScore(float3[] positions, int identifier) {
        
        ClashGroupAtom[] nearbyAtoms = nearbyClashGroup.groupAtoms;

        float score = 0;

        for (int dihedralGroupIndex=identifier-2; dihedralGroupIndex<identifier-2; dihedralGroupIndex++) {
            Torsion torsion = torsions[dihedralGroupIndex];
            float[] energies = new float[3]; 
            score += CustomMathematics.ETorsion(currentDihedrals[dihedralGroupIndex], torsion.barrierHeights, torsion.phaseOffsets, 0);
        }


        for (int n0=0; n0<residueClashGroup.size; n0++) {

            ClashGroupAtom residueAtom0 = residueClashGroup.groupAtoms[n0];

            //Compare currently tested atoms
            int diff = residueAtom0.identifer - identifier;
            if (
                ! (residueAtom0.element == Element.H && diff == 0) &&
                diff != 1
            ) {continue;}

            float3 position0 = positions[n0];
            Element element0 = residueAtom0.element;
            float scaledCharge0 = residueAtom0.charge / parameters.dielectricConstant;

            //Include interactions insidue residue
            for (int n1=n0+1; n1<residueClashGroup.size; n1++) {

                ClashGroupAtom residueAtom1 = residueClashGroup.groupAtoms[n1];

                //Skip atoms with same or neighbouring identifier
                //Stops the score from going too wild from connected atoms
                if (math.abs(residueAtom0.identifer - residueAtom1.identifer) < 2) {
                    continue;
                }

                float r2 = math.distancesq(
                    position0,
                    positions[n1]
                );

                //Check that atoms don't clash (would be considered bonded by distance)
                if (
                    Data.GetBondOrderDistanceSquared(
                        element0, 
                        residueAtom1.element,
                        r2
                    ) != BT.NONE
                ) {
                    return (0, true);
                }
                
                float vdwR = (residueAtom0.radius + residueAtom1.radius) * 0.5f;
                float vdwV = Mathf.Sqrt ((residueAtom0.wellDepth + residueAtom1.wellDepth) * Data.kcalToHartree);
                score += CustomMathematics.EVdWAmberSquared(r2, vdwV, vdwR)
                      +  CustomMathematics.EElectrostaticR2Squared(r2, scaledCharge0*residueAtom1.charge);
                
            }

            //Include interactions between residue and nearby residues
            for (int m=0; m<nearbyClashGroup.size; m++) {
                
                ClashGroupAtom nearbyAtom = nearbyAtoms[m];

                float r2 = math.distancesq(
                    position0,
                    nearbyAtom.position
                );
                
                //Check that atoms don't clash (would be considered bonded by distance)
                if (Data.GetBondOrderDistanceSquared(
                        element0, 
                        nearbyAtom.element,
                        r2
                    ) != BT.NONE
                ) {
                    return (0, true);
                }

                float vdwR = (residueAtom0.radius + nearbyAtom.radius) * 0.5f;
                float vdwV = Mathf.Sqrt ((residueAtom0.wellDepth + nearbyAtom.wellDepth) * Data.kcalToHartree);
                score += CustomMathematics.EVdWAmberSquared(r2, vdwV, vdwR)
                      +  CustomMathematics.EElectrostaticR2Squared(r2, scaledCharge0 * nearbyAtom.charge);
            }
        }
        return (score, false);
    }

    IEnumerable<float3[]> EnumerateBestPositions(
        float3[] initialPositions,
        float deltaTheta,
        int steps,
        int index1,
        int index2,
        bool[] mask,
        int fixedIdentifier
    ) {

        int size = residueClashGroup.size;

        float3[] positions = new float3[size];
        float3[] previousPositions = new float3[size];

        System.Array.Copy(initialPositions, positions, size);

        // Positions of the central atoms in the dihedral
        float3 p1 = positions[index1];
        float3 p2 = positions[index2];

        // Axis to rotate around
        float3 axis = math.normalize(p2 - p1);

        float pi2 = math.PI * 2;
        float angleRad = math.radians(deltaTheta);

        // Quaternion to rotate by
        quaternion rotation = quaternion.AxisAngle(
            axis,
            angleRad
        );
        
        bool debug = CustomLogger.logErrorLevel >= EL.DEBUG;
        CustomLogger.LogFormat(
            EL.DEBUG,
            "{0}Step:   Theta:   Score:",
            FileIO.newLine
        );

        // Flag to see if score went down
        // If it goes down then back up, keep the previous step
        bool scoreDecreased = false;

        float[] scores = new float[steps];
        bool[] keptPositions = new bool[steps];
        for (int step=0; step<steps; step++) {

            float currentDihedral = currentDihedrals[fixedIdentifier - 2];
            currentDihedral += angleRad;
            if (currentDihedral > pi2) {
                currentDihedral -= pi2;
            }
            currentDihedrals[fixedIdentifier - 2] = currentDihedral;

            for (int atomIndex=0; atomIndex<size; atomIndex++) {
                // Ignore masked atoms
                if (! mask[atomIndex]) {continue;}
                positions[atomIndex] = (float3) math.rotate(rotation, positions[atomIndex] - p2) + p2;

            }

            //Get score for this rotation
            (float score, bool clash) = GetClashScore(positions, fixedIdentifier);
            scores[step] = score;

            if (step > 0) {
                if (clash) {
                    
                    if (scoreDecreased) {
                        //Score went down then up - keep
                        keptPositions[step-1] = true;
                        yield return previousPositions;
        
                        if (debug)
                        CustomLogger.LogOutput(" <-",  false);
                    }

                    if (debug)
                    CustomLogger.LogOutput(
                        string.Format(
                            "{0}{1,6} {2,6:###.0} ********",
                            FileIO.newLine,
                            step,
                            deltaTheta * (step + 1)
                        ),
                        false
                    );

                    scoreDecreased = false;
                } else if (score < scores[step-1]) {
                    scoreDecreased = true;   
                    
                    if (debug)
                    CustomLogger.LogOutput(
                        string.Format(
                            "{0}{1,6} {2,6:###.0} {3,8:#.##E+00} -",
                            FileIO.newLine,
                            step,
                            deltaTheta * (step + 1),
                            score
                        ),
                        false
                    );
                } else if (score == scores[step-1]) {
                    scoreDecreased = true;   
                    
                    if (debug)
                    CustomLogger.LogOutput(
                        string.Format(
                            "{0}{1,6} {2,6:###.0} {3,8:#.##E+00}",
                            FileIO.newLine,
                            step,
                            deltaTheta * (step + 1),
                            score
                        ),
                        false
                    );
                } else {

                    if (scoreDecreased) {
                        //Score went down then up - keep
                        keptPositions[step-1] = true;
                        yield return previousPositions;
        
                        if (debug)
                        CustomLogger.LogOutput(" <-", false);
                    }

                    scoreDecreased = false;

                    if (debug)
                    CustomLogger.LogOutput(
                        string.Format(
                            "{0}{1,6} {2,6:###.0} {3,8:#.##E+00} +",
                            FileIO.newLine,
                            step,
                            deltaTheta * (step + 1),
                            score
                        ),
                        false
                    );
                }
            }

            System.Array.Copy(positions, previousPositions, size);
        }
        
        if (debug)
        CustomLogger.LogOutput(FileIO.newLine);
    }

    public void UpdatePositions(Residue residue) {
        
        if (scores != null && scores.Count() > 0) {

            int bestIndex = CustomMathematics.IndexOfMin(scores);
            float3[] bestPositions = acceptedPositions[bestIndex];

            if (residue.size != bestPositions.Length) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Inconsistent size between Residue '{0}' and Dihedral scanner '{1}'!",
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

public struct SingleClashGroup {
    
    public ClashGroupAtom[] groupAtoms;
    public PDBID[] pdbIDs;
    public int size;
    public int[] identifers;

    public float3[] positions;

    public SingleClashGroup(Residue residue, Parameters parameters) {

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


public struct MultiClashGroup {

    public ClashGroupAtom[] groupAtoms;
    public PDBID[] pdbIDs;
    public int size;

    public float3[] GetPositions() {
        float3[] positions = new float3[size];
        for (int i=0; i<size; i++) {
            positions[i] = groupAtoms[i].position;
        }
        return positions;
    }

    public MultiClashGroup(List<Residue> residues, Parameters parameters) {

        size = residues.Sum(x => x.size);
        pdbIDs = new PDBID[size];
        groupAtoms = new ClashGroupAtom[size];

        int index = 0;
        foreach (Residue residue in residues) {
            foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
                pdbIDs[index] = pdbID;
                groupAtoms[index++] = new ClashGroupAtom(pdbID, atom, parameters);
            }
        }

    }
}

public struct ClashGroupAtom {
    
    static List<Amber> missingAmbers = new List<Amber>();

    // These are the identifiers that remain fixed during the scans
    // They form the mask to stop atoms from rotating
    // They start from the backbone and grow down the chain
    public static readonly string[] allIdentifiers = new string[] {"", "A", "B", "G", "D", "E", "Z", "H"};


    public float3 position; 
    public float charge;
    public Amber amber;
    public PDBID pdbID;
    public Element element;
    public float wellDepth;
    public float radius;
    public int identifer;

    public ClashGroupAtom(PDBID pdbID, Atom atom, Parameters parameters) {
        position = atom.position;
        charge = atom.partialCharge;
        amber = atom.amber;

        this.pdbID = pdbID;
        element = pdbID.element;
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

