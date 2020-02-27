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


public static class MutationTools {


	///<summary>
	/// Change a Standard Residue for another.
	///</summary>
    public static IEnumerator MutateStandard(Geometry geometry, Residue oldResidue, Residue newResidue, bool optimise) {

        if (oldResidue == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Old Residue is null! Cannot mutate."
            );
            yield break;
        }

        if (newResidue == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "New Residue is null! Cannot mutate."
            );
            yield break;
        }

        yield return NotificationBar.UpdateTaskProgress(TID.MUTATE_RESIDUE, 0f);
        
        IList<RS> validStates = new [] {RS.STANDARD, RS.C_TERMINAL, RS.N_TERMINAL};

        if (! validStates.Any(x => x == oldResidue.state)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Residue is not in a valid State for mutation. Must be one of: {0}",
                string.Join(", ", validStates.Select(x => Constants.ResidueStateMap[x]))
            );
            yield break;
        }

        //Get new Residue
        if (newResidue == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot mutate Residue '{0}' to '{1}'",
                oldResidue.residueName,
                newResidue.residueName
            );
            yield break;
        }

        CustomLogger.LogFormat(
            EL.INFO,
            "Mutating Residue '{0}' ('{1}' -> '{2}')",
            oldResidue.residueID,
            oldResidue.residueName,
            newResidue.residueName
        );
		
		bool TryGetAtom(Residue residue, PDBID pdbID, out Atom atom) {
			atom = residue.GetSingleAtom(pdbID);
			if (atom == null) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Cannot mutate Residue '{0}' to '{1}' - Required Atom {2} is null.",
                    oldResidue.residueName,
                    newResidue.residueName,
					new AtomID(residue.residueID, pdbID)
				);
				return false;
			}

			if (math.all(math.isnan(atom.position))) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Cannot mutate Residue '{0}' to '{1}' - Position of atom {2} is {3}.",
                    oldResidue.residueName,
                    newResidue.residueName,
					new AtomID(residue.residueID, pdbID),
					atom.position
				);
				return false;
			}
			return true;
		}

        Atom cAtom;
        Atom caAtom;
        Atom nAtom;

        if (
			!TryGetAtom(oldResidue, PDBID.C, out cAtom) ||
			!TryGetAtom(oldResidue, PDBID.CA, out caAtom) || 
			!TryGetAtom(oldResidue, PDBID.N, out nAtom)
		) {
            yield return NotificationBar.UpdateClearTask(TID.MUTATE_RESIDUE);
            yield break;
		}

        yield return NotificationBar.UpdateTaskProgress(TID.MUTATE_RESIDUE, 0.05f);

        //Move mutated Residue to current position
        newResidue.TranslateTo(PDBID.C, cAtom.position);

        //Align to new bond direction
        newResidue.AlignBond(
            PDBID.C, 
            PDBID.N, 
            math.normalize(nAtom.position - cAtom.position)
        );

        //Align by dihedral
        float dihedral = CustomMathematics.GetDihedral(caAtom, cAtom, nAtom, newResidue.atoms[PDBID.CA]);
        Vector3 bondVector = CustomMathematics.GetVector(cAtom, nAtom);
        newResidue.Rotate(
            Quaternion.AngleAxis(dihedral * Mathf.Rad2Deg, bondVector), 
            newResidue.atoms[PDBID.C].position
        );

        //Keep protonation flag
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
            addList = newResidue.pdbIDs.Where(x => !Data.backbonePDBs.Contains(x)).ToList();
        } else {
            addList = newResidue.pdbIDs.Where(x => x.element != Element.H && !Data.backbonePDBs.Contains(x)).ToList();
        }

        //Add new sidechain Atoms
        foreach (PDBID pdbID in addList) {
            oldResidue.AddAtom(pdbID, newResidue.atoms[pdbID].Copy());
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
        
        //Add H if Proline
        if (nAtom.EnumerateInternalConnections().Select(x => x.Item1.pdbID).Any(x => x.TypeEquals(cdPDBID))) {
            CustomLogger.Log(
                EL.DEBUG,
                "Old Residue is Proline-like. Adding missing Proton."
            );
            oldResidue.AddProton(PDBID.N);
        }

        if (newResidue.residueName == "PRO") {
            if (oldResidue.atoms.ContainsKey(hPDBID)){
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

            oldResidue.atoms[PDBID.N].internalConnections[cdPDBID] = BT.SINGLE;
            oldResidue.atoms[cdPDBID].internalConnections[PDBID.N] = BT.SINGLE;
        }

        if (!optimise) {
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "No dihedral optimisation."
            );
            oldResidue.residueName = newResidue.residueName;
            yield return NotificationBar.UpdateClearTask(TID.MUTATE_RESIDUE);
            yield break;
        }

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


        DihedralScanner dihedralScanner;
        try {
            dihedralScanner = new DihedralScanner(geometry, newResidue, nearbyResidues);
        } catch (System.Exception e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to create Dihedral Scanner: {0}",
                e.Message
            );
            NotificationBar.ClearTask(TID.MUTATE_RESIDUE);
            yield break;
        }

        yield return dihedralScanner.GetBestDihedrals();
        yield return NotificationBar.UpdateTaskProgress(TID.MUTATE_RESIDUE, 0.95f);

        if (dihedralScanner.scores != null && dihedralScanner.scores.Count() > 0) {

            int bestIndex = CustomMathematics.IndexOfMin(dihedralScanner.scores);

            CustomLogger.LogFormat(
                EL.VERBOSE,
                "{0} dihedrals returned from Dihedral Scanner - best score: {1}.",
                dihedralScanner.scores.Count(),
                dihedralScanner.scores[bestIndex]
            );

            float3[] bestPositions = dihedralScanner.bestPositions[bestIndex];

            for (int atomIndex=0; atomIndex<newResidue.size; atomIndex++) {
                PDBID pdbID = dihedralScanner.residuePDBIDs[atomIndex];
                Atom atom;
                if (!oldResidue.atoms.TryGetValue(pdbID, out atom)) {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Mutation failed - couldn't find atom '{0}' in Residue '{1}'.",
                        pdbID,
                        oldResidue.residueID
                    );
                    yield return NotificationBar.UpdateClearTask(TID.MUTATE_RESIDUE);
                    yield break;
                }
                oldResidue.atoms[pdbID].position = bestPositions[atomIndex];
            }

        } else {
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "No dihedrals returned from Dihedral Scanner."
            );
        }
        
        oldResidue.residueName = newResidue.residueName;
        yield return NotificationBar.UpdateClearTask(TID.MUTATE_RESIDUE);
    }
}

public class DihedralScanner {

    public PDBID[] residuePDBIDs;
    public int[] identifers;
    float3[] residuePositions;
    float[] residueWellDepths;
    float[] residueRadii;
    int numResidueAtoms;

    float3[] nearbyPositions;
    float[] nearbyWellDepths;
    float[] nearbyRadii;
    int numNearbyAtoms;

    PDBID[][] dihedralGroups;
    int numDihedralGroups;

    Parameters parameters;
    List<Amber> missingAmbers;

    public List<float3[]> bestPositions;
    public List<float> scores;

     
    static readonly string[] allIdentifiers = new string[] {"", "A", "B", "G", "D", "E", "Z", "H"};
    // These are the identifiers that remain fixed during the scans
    // They form the mask to stop atoms from rotating
    // They start from the backbone and grow down the chain
    
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
        
        missingAmbers = new List<Amber>();

        // Array of PDBIDs of all the mutated/new residue
        residuePDBIDs = newResidue.pdbIDs.ToArray();
        numResidueAtoms = residuePDBIDs.Length;
        identifers = residuePDBIDs
            .Select(x => System.Array.IndexOf(allIdentifiers, x.identifier == "" ? "" : x.identifier.Substring(0, 1)))
            .ToArray();
        
        // Store the positions, well depths and radii of the new residue
        residuePositions = new float3[numResidueAtoms];
        residueWellDepths = new float[numResidueAtoms];
        residueRadii = new float[numResidueAtoms];
        
        int startIndex = 0;
        // Get the positions and parameters of the mutated Residue
        GetResidueDetails(
            parameters, 
            newResidue, 
            residuePositions,
            residueWellDepths,
            residueRadii,
            missingAmbers,
            ref startIndex
        );

        CustomLogger.LogFormat(
            EL.DEBUG,
            "Parameters for Mutated Residue:{1}Index: PDBID: WellDepth: Radius:{1}{0}",
            () => new object[] {
                string.Join(
                    FileIO.newLine,
                    residuePDBIDs.Select(
                        (pdbID,index) => 
                        string.Format(
                            "{0,5} {1,6} {2,8:#.##E+00} {3,8:#.##E+00}",
                            index,
                            pdbID,
                            residueWellDepths[index],
                            residueRadii[index]
                        )
                    )
                ),
                FileIO.newLine
            }
        );
        

        numNearbyAtoms = nearbyResidues.SelectMany(x => x.atoms).Count();
        
        // Store the positions, well depths and radii of the nearby residues
        nearbyPositions = new float3[numNearbyAtoms];
        nearbyWellDepths = new float[numNearbyAtoms];
        nearbyRadii = new float[numNearbyAtoms];

        // Make sure there are parameters to use

        startIndex = 0;
        // Get the positions and parameters of the nearby Residues
        foreach (Residue nearbyResidue in nearbyResidues) {
            GetResidueDetails(
                parameters, 
                nearbyResidue, 
                nearbyPositions,
                nearbyWellDepths,
                nearbyRadii,
                missingAmbers,
                ref startIndex
            );
        }
        
        CustomLogger.LogFormat(
            EL.DEBUG,
            "Parameters for Nearby Residues:{1}Index:        WellDepth: Radius:{1}{0}",
            () => new object[] {
                string.Join(
                    FileIO.newLine,
                    nearbyWellDepths.Select(
                        (depth,index) => 
                        string.Format(
                            "{0,5}        {1,8:#.##E+00} {2,8:#.##E+00}",
                            index,
                            depth,
                            nearbyRadii[index]
                        )
                    )
                ),
                FileIO.newLine
            }
        );
    }

    public IEnumerator GetBestDihedrals() {

        bestPositions = new List<float3[]>();
        scores = new List<float>();

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
        groupPositions[0].Add(residuePositions);

        // The increments around the dihedral
        float deltaTheta = 20;
        int steps = (int)(360 / deltaTheta) + 1;
        
        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Checking {0} dihedral groups. Scanning {1} degrees per group over {2} steps.",
            numDihedralGroups,
            deltaTheta,
            steps
        );

        CustomLogger.LogFormat(
            EL.DEBUG,
            "Residue has {0} PDB IDs:{4}Index:       {3}{4}PDB ID:      {1}{4}Identifier:  {2}",
            () => new object[] {
                residuePDBIDs.Count(),
                string.Join(" ", residuePDBIDs),
                string.Join(" ", residuePDBIDs.Select(x => 
                    x.identifier == "" 
                        ? "    " 
                        : x.identifier.Substring(0, 1).PadRight(4))
                ),
                string.Join(" ", residuePDBIDs.Select((x,i) => i.ToString().PadRight(4))),
                FileIO.newLine
            }
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
            int i1 = System.Array.IndexOf(residuePDBIDs, pdbID1);

            // Index of Atom 2
            PDBID pdbID2 = dihedralGroup[2];
            int i2 = System.Array.IndexOf(residuePDBIDs, pdbID2);

            // Get the indices of the atoms allowed to rotate
            bool[] mask = identifers
                .Select((x, i) => residuePDBIDs[i].element == Element.H ? x >= fixedIdentifier : x > fixedIdentifier)
                .ToArray();

            
            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Group {0} Axis: {1}-{2} ({3}-{4}). Freezing Identifiers: '{5}'. Mask:{7}             {6}",
                () => new object[] {
                    dihedralGroupIndex,
                    i1,
                    i2,
                    pdbID1,
                    pdbID2,
                    string.Join("' '", allIdentifiers.Where((x,i) => i <= fixedIdentifier)),
                    string.Join(" ", mask.Select(x => x ? "1   " : "0   ")),
                    FileIO.newLine
                }
            );


            foreach (float3[] positions in groupPositions[dihedralGroupIndex]) {

                foreach (float3[] nextGroupPositions in EnumerateBestPositions(positions, deltaTheta, steps, i1, i2, mask, fixedIdentifier)) {

                    float3[] positionsClone = new float3[numResidueAtoms];
                    System.Array.Copy(nextGroupPositions, positionsClone, numResidueAtoms);

                    //Last iteration - these are the best positions
                    if (dihedralGroupIndex == numDihedralGroups - 1) {
                        bestPositions.Add(positionsClone);
                        scores.Add(GetClashScore(positionsClone, mask, fixedIdentifier));
                    } else {
                        groupPositions[dihedralGroupIndex + 1].Add(positionsClone);
                    }

                    if (Timer.yieldNow) {
                        yield return null;
                    }
                }
            }
            
            yield return NotificationBar.UpdateTaskProgress(
                TID.MUTATE_RESIDUE, 
                CustomMathematics.Map(dihedralGroupIndex, 0, numDihedralGroups, 0.2f, 0.95f)
            );
        }
    }
    

    // This is essentially the Van der Waals interaction energy, which is huge when there is a clash
    // N_(atoms in residue) * M_(atoms in nearby residues) scaling
    float GetClashScore(float3[] positions, bool[] mask, int identifier) {
        float score = 0;
        
        for (int n0=0; n0<numResidueAtoms; n0++) {

            int identifier0 = identifers[n0];

            float3 position = positions[n0];
            float radius = residueRadii[n0];
            float wellDepth = residueWellDepths[n0];

            //Include interactions insidue residue
            for (int n1=n0+1; n1<numResidueAtoms; n1++) {

                //Skip atoms with same identifier
                //Stops the score from going too wild from connected atoms
                int identifier1 = identifers[n1];
                if (identifier0 == identifier1) {
                    continue;
                }

                float vdwR = (radius + residueRadii[n1]) * 0.5f;
                float vdwV = Mathf.Sqrt ((wellDepth + residueWellDepths[n1]) * Data.kcalToHartree);
                score += CustomMathematics.EVdWAmber(
                    math.distance(positions[n1], position),
                    vdwV,
                    vdwR,
                    0
                );
            }

            //Skip interactions with nearby atoms except for the identifier being optimised
            if (identifier > identifier0) {
                continue;
            }


            //Include interactions between residue and nearby residues
            for (int m=0; m<numNearbyAtoms; m++) {
                float vdwR = (radius + nearbyRadii[m]) * 0.5f;
                float vdwV = Mathf.Sqrt ((wellDepth + nearbyWellDepths[m]) * Data.kcalToHartree);
                score += CustomMathematics.EVdWAmber(
                    math.distance(nearbyPositions[m], position),
                    vdwV,
                    vdwR,
                    0
                );
            }
        }
        return score;
    }


    // Get parameters for an atom
    void GetDepthAndRadius(Parameters parameters, List<Amber> missingAmbers, Amber amber, PDBID pdbID, out float wellDepth, out float radius) {
        
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
    

    void GetResidueDetails(
        Parameters parameters, 
        Residue residue, 
        float3[] positions, 
        float[] wellDepths,
        float[] radii,
        List<Amber> missingAmbers,
        ref int startIndex
    ) {
        
        foreach ((PDBID mutatedPDBID, Atom mutatedAtom) in residue.atoms) {
            positions[startIndex] = mutatedAtom.position;
            Amber mutatedAmber = mutatedAtom.amber;

            float wellDepth;
            float radius;
            GetDepthAndRadius(parameters, missingAmbers, mutatedAmber, mutatedPDBID, out wellDepth, out radius);
            wellDepths[startIndex] = wellDepth;
            radii[startIndex] = radius;
            startIndex++;
        }
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

        float3[] positions = new float3[numResidueAtoms];
        float3[] previousPositions = new float3[numResidueAtoms];

        System.Array.Copy(initialPositions, positions, numResidueAtoms);

        // Positions of the central atoms in the dihedral
        float3 p1 = positions[index1];
        float3 p2 = positions[index2];

        // Axis to rotate around
        float3 axis = math.normalize(p2 - p1);

        // Quaternion to rotate by
        quaternion rotation = quaternion.AxisAngle(
            axis,
            math.radians(deltaTheta)
        );


        // Flag to see if score went down
        // If it goes down then back up, keep the previous step
        bool scoreDecreased = false;


        float[] scores = new float[steps];
        bool[] keptPositions = new bool[steps];
        for (int step=0; step<steps; step++) {

            for (int atomIndex=0; atomIndex<numResidueAtoms; atomIndex++) {
                // Ignore masked atoms
                if (! mask[atomIndex]) {continue;}
                if (residuePDBIDs[atomIndex] == PDBID.O) {
                    Debug.LogErrorFormat("O not masked");
                }
                positions[atomIndex] = (float3) math.rotate(rotation, positions[atomIndex] - p2) + p2;

            }

            //Get score for this rotation
            scores[step] = GetClashScore(positions, mask, fixedIdentifier);

            if (step > 0) {
                if (scores[step] < scores[step-1]) {
                    scoreDecreased = true;    
                } else {

                    if (scoreDecreased) {
                        //Score went down then up - keep
                        keptPositions[step-1] = true;
                        yield return previousPositions;
                    }

                    scoreDecreased = false;    
                }
            }


            System.Array.Copy(positions, previousPositions, numResidueAtoms);
        }
        
        CustomLogger.LogFormat(
            EL.DEBUG,
            "{1}Step: Theta:         Score:{1}{0}",
            () => new object[] {
                string.Join(
                    FileIO.newLine,
                    scores.Select(
                        (score,step) => 
                        string.Format(
                            "{0,6} {1,6:###.0} {2,8:#.##E+00} {3} {4}",
                            step,
                            deltaTheta * (step + 1),
                            score,
                            step == 0 || score == scores[step-1] ? " " : score > scores[step-1] ? "+" : "-",
                            keptPositions[step] ? "<-" : "  "
                        )
                    )
                ),
                FileIO.newLine
            }
        );
    }
}
