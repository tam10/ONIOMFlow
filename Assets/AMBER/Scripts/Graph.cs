using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Mathematics;
using EL = Constants.ErrorLevel;
using CT = Constants.CoulombType;
using VT = Constants.VanDerWaalsType;
using BT = Constants.BondType;
using RS = Constants.ResidueState;
using TID = Constants.TaskID;

public class Graph {


	public List<PrecomputedNonBonding> nonBondings;
    public List<StretchCalculator> stretches;
    public List<BendCalculator> bends;
    public List<DihedralCalculator> torsions;
    public List<DihedralCalculator> impropers;


    public int numNearbyAtoms;
    public int numMobileAtoms;
    public List<AtomID> mobileAtomIDs;
    public List<AtomID> nearbyAtomIDs;
    public bool[] mobileMask;
    private Dictionary<AtomID, int> atomIDToIndex;
    private float3[] positions;
    private Atom[] atomRefs;
    private float3[] forces;

    public static Parameters parameters;

	public Geometry geometry;


	public IEnumerator SetGeometry(Geometry geometry, List<ResidueID> mobileResidueIDs) {

        /*
        
        Set up the forcefield for an Geometry object
        This calculation has three layers:
            1) Mobile Residues: Forces are computed for atoms of these Residues
            2) Nearby Residues: Forcefield terms are computed with atoms of 
                                these Residues to add to forces of Mobile Residues, 
                                but atoms in this layer are static
            3) Rest of System : Atoms of these Residues are ignored
        
        Positions and forces are stored and updated for layers 1 and 2
        Only non-bonded and link terms are computed for the boundary between layers 1 and 2
        */

        NotificationBar.SetTaskProgress(TID.UPDATE_PARAMETERS, 0f);
        yield return null;
        
        stretches = new List<StretchCalculator>();
        bends = new List<BendCalculator>();
        torsions = new List<DihedralCalculator>();
        impropers = new List<DihedralCalculator>();
        nonBondings = new List<PrecomputedNonBonding>();

		this.geometry = geometry;
        parameters = Settings.defaultParameters.Duplicate();
        parameters.UpdateParameters(geometry.parameters);

        List<ResidueID> nearbyResidueIDs = new List<ResidueID>();
        
        atomIDToIndex = new Dictionary<AtomID, int>();

        numNearbyAtoms = 0;
        numMobileAtoms = 0;
        int numNearbyResidues = 0;

        void AddNearbyResidue(ResidueID nearbyResidueID, Residue nearbyResidue, bool mobile) {
            if (!nearbyResidueIDs.Contains(nearbyResidueID)) {
                return;
            }
            nearbyResidueIDs.Add(nearbyResidueID);
            numNearbyResidues++;
            foreach ((PDBID pdbID, Atom atom) in nearbyResidue.atoms) {
                AtomID atomID = new AtomID(nearbyResidueID, pdbID);
                nearbyAtomIDs.Add(atomID);
                atomIDToIndex[atomID] = numNearbyAtoms;

                mobileMask[numNearbyAtoms] = mobile;

                if (mobile) {
                    mobileAtomIDs.Add(atomID);
                    numMobileAtoms++;
                }
                numNearbyAtoms++;
            }
        }

        foreach ((ResidueID residueID, Residue residue) in geometry.residueDict) {
            bool mobile = mobileResidueIDs.Contains(residueID);

            if (mobile) {
                AddNearbyResidue(residueID, residue, mobile);
                foreach (ResidueID nearbyResidueID in residue.ResiduesWithinDistance(Settings.maxNonBondingCutoff)) {
                    if (!nearbyResidueIDs.Contains(nearbyResidueID)) {
                        AddNearbyResidue(nearbyResidueID, geometry.residueDict[nearbyResidueID], mobile);
                    }
                }
            }
        }

        int currentResidueNum = 0;
        //Copy across partial charges
        foreach (ResidueID residueID in nearbyResidueIDs) {
            Residue forceFieldResidue = geometry.residueDict[residueID];

            Dictionary<RS, Residue> standardFamily;
            if (! Data.standardResidues.TryGetValue(forceFieldResidue.residueName, out standardFamily)) {
                continue;
            }
            
            Residue standardResidue;
            if ( ! (
                    standardFamily.TryGetValue(RS.STANDARD, out standardResidue)
                    || standardFamily.TryGetValue(RS.WATER, out standardResidue)
            ) ) {
                continue;
            }

            foreach ((PDBID pdbID, Atom atom) in forceFieldResidue.atoms) {
                Atom standardAtom;
                if (standardResidue.atoms.TryGetValue(pdbID, out standardAtom)) {
                    atom.partialCharge = standardAtom.partialCharge;
                }
            }

            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.UPDATE_PARAMETERS, 
                    CustomMathematics.Map(currentResidueNum++, 0f, numNearbyResidues, 0f, 0.1f)
                );
                yield return null;
            }
        }

        NotificationBar.SetTaskProgress(TID.UPDATE_PARAMETERS, 0.2f);
        yield return null;

        int3[] improperCycles = new int3[] {
            new int3(0, 1, 2), new int3(0, 2, 1),
            new int3(1, 0, 2), new int3(1, 2, 0),
            new int3(2, 0, 1), new int3(2, 1, 0)
        };

        atomRefs = new Atom[numNearbyAtoms];
        positions = new float3[numNearbyAtoms];
        forces = new float3[numNearbyAtoms];

        for (int i=0; i<numNearbyAtoms; i++) {
            AtomID atomID0 = nearbyAtomIDs[i];
            Atom atom0 = geometry.GetAtom(atomID0);

            atomRefs[i] = atom0;
            positions[i] = atom0.position;

            if (!mobileMask[i]) {
                continue;
            }

            //Non-bonding terms
            foreach (AtomID atomID1 in nearbyAtomIDs) {
                if (atomID1 == atomID0) {continue;}
                Atom atom1 = geometry.GetAtom(atomID1);
                if (CustomMathematics.GetDistance(atom0, atom1) < Settings.maxNonBondingCutoff) {
                    
                    try { 
                        //Check this hasn't been added in reverse
                        (int, int) nonBondKey = (
                            atomIDToIndex[atomID0], 
                            atomIDToIndex[atomID1]
                        );
                        if (!nonBondings.Select(
                            x => x.index0 == nonBondKey.Item2 && 
                                x.index1 == nonBondKey.Item1
                        ).Any(x => x)) {
                            nonBondings.Add(new PrecomputedNonBonding(atom0, atom1, nonBondKey, geometry.GetGraphDistance(atomID0, atomID1, 3)));
                        }
                    } catch {}

                }
            }

            AtomID[] atom0Neighbours = atom0.EnumerateConnections()
                .Select(x => x.Item1)
                .ToArray();

            //Impropers
            if (atom0Neighbours.Length == 3) {

                //Cycle over the 6 possible improper combinations for this central Atom
                foreach (int3 improperCycle in improperCycles) {
                    AtomID atomID1 = atom0Neighbours[improperCycle.x];
                    AtomID atomID2 = atom0Neighbours[improperCycle.y];
                    AtomID atomID3 = atom0Neighbours[improperCycle.z];

                    Atom atom1 = geometry.GetAtom(atomID1);
                    Atom atom2 = geometry.GetAtom(atomID2);
                    Atom atom3 = geometry.GetAtom(atomID3);

                    try { 
                        //Check this hasn't been added in reverse
                        (int, int, int, int) dihedralKey = (
                            atomIDToIndex[atomID1], 
                            atomIDToIndex[atomID0],
                            atomIDToIndex[atomID2],
                            atomIDToIndex[atomID3]
                        );
                        if (!impropers.Select(
                            x => x.index0 == dihedralKey.Item4 && 
                                x.index1 == dihedralKey.Item3 && 
                                x.index2 == dihedralKey.Item2 && 
                                x.index3 == dihedralKey.Item1
                        ).Any(x => x)) {
                            impropers.Add(new DihedralCalculator(atom1, atom0, atom2, atom3, false, dihedralKey));
                        }
                    } catch {}
                }
                
            }
            

            //Stretches
            foreach (AtomID atomID1 in atom0Neighbours) {
                Atom atom1 = geometry.GetAtom(atomID1);
                
                try { 
                    //Check this hasn't been added in reverse
                    (int, int) stretchKey = (
                        atomIDToIndex[atomID0], 
                        atomIDToIndex[atomID1]
                    );
                    if (!stretches.Select(
                        x => x.index0 == stretchKey.Item2 && 
                            x.index1 == stretchKey.Item1
                    ).Any(x => x)) {
                        stretches.Add(new StretchCalculator(atom0, atom1, stretchKey));
                    }
                } catch {
                    CustomLogger.LogFormat(
                        EL.WARNING,
                        "No Stretch Parameter for Atoms: '{0}'-'{1}'. Ambers: '{2}'-'{3}'",
                        () => new object[] {
                            atomID0,
                            atomID1,
                            atom0.amber,
                            atom1.amber
                        }
                    );
                }

                //Bends
                foreach ((AtomID atomID2, BT bondType12) in atom1.EnumerateConnections()) {
                    if (atomID2 == atomID0) {continue;}
                    Atom atom2 = geometry.GetAtom(atomID2);
                    
                    try { 
                        //Check this hasn't been added in reverse
                        (int, int, int) bendKey = (
                            atomIDToIndex[atomID0], 
                            atomIDToIndex[atomID1],
                            atomIDToIndex[atomID2]
                        );
                        if (!bends.Select(
                            x => x.index0 == bendKey.Item3 && 
                                x.index1 == bendKey.Item2 && 
                                x.index2 == bendKey.Item1
                        ).Any(x => x)) {
                            bends.Add(new BendCalculator(atom0, atom1, atom2, bendKey));
                        }
                    } catch {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "No Bend Parameter for Atoms: '{0}'-'{1}'-{2}'. Ambers: '{3}'-'{4}'-'{5}'",
                            () => new object[] {
                                atomID0,
                                atomID1,
                                atomID2,
                                atom0.amber,
                                atom1.amber,
                                atom2.amber
                            }
                        );
                    }
                    
                    
                    //Propers
                    foreach ((AtomID atomID3, BT bondType23) in atom2.EnumerateConnections()) {
                        if (atomID3 == atomID1) {continue;}
                        Atom atom3 = geometry.GetAtom(atomID3);

                        try { 
                            //Check this hasn't been added in reverse
                            (int, int, int, int) dihedralKey = (
                                atomIDToIndex[atomID0], 
                                atomIDToIndex[atomID1],
                                atomIDToIndex[atomID2],
                                atomIDToIndex[atomID3]
                            );
                            if (!torsions.Select(
                                x => x.index0 == dihedralKey.Item4 && 
                                    x.index1 == dihedralKey.Item3 && 
                                    x.index2 == dihedralKey.Item2 && 
                                    x.index3 == dihedralKey.Item1
                            ).Any(x => x)) {
                                torsions.Add(new DihedralCalculator(atom0, atom1, atom2, atom3, true, dihedralKey));
                            }
                        } catch {}
                    }
                }

            }
            
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.UPDATE_PARAMETERS, 
                    CustomMathematics.Map(i, 0f, numMobileAtoms, 0.1f, 1f)
                );
                yield return null;
            }
        }

        NotificationBar.ClearTask(TID.UPDATE_PARAMETERS);
	}

    public IEnumerable<(int, int, float[])> EnumerateBadStretches() {
        foreach (StretchCalculator sc in stretches) {
            float[] energies = new float[3];
            sc.AddEnergies(energies, positions);
            if (energies.Any(x => float.IsNaN(x))) {
                yield return (
                    sc.index0, 
                    sc.index1, 
                    energies
                );
            }
        }
    }

    public IEnumerable<(int, int, float[])> EnumerateBadNonBondings() {
        foreach (PrecomputedNonBonding pcn in nonBondings) {
            float[] energies = new float[3];
            pcn.AddEnergies(energies, positions);
            if (energies.Any(x => float.IsNaN(x))) {
                yield return (
                    pcn.index0, 
                    pcn.index1, 
                    energies
                );
            }
        }
    }

    public IEnumerable<(int, int, int, float[])> EnumerateBadBends() {
        foreach (BendCalculator bc in bends) {
            float[] energies = new float[3];
            bc.AddEnergies(energies, positions);
            if (energies.Any(x => float.IsNaN(x))) {
                yield return (
                    bc.index0, 
                    bc.index1, 
                    bc.index2, 
                    energies
                );
            }
        }
    }

    public IEnumerable<(int, int, int, int, float[])> EnumerateBadTorsions() {
        foreach (DihedralCalculator dc in torsions) {
            float[] energies = new float[3];
            dc.AddEnergies(energies, positions);
            if (energies.Any(x => float.IsNaN(x))) {
                yield return (
                    dc.index0, 
                    dc.index1, 
                    dc.index2, 
                    dc.index3, 
                    energies
                );
            }
        }
    }

    public IEnumerable<(int, int, int, int, float[])> EnumerateBadImpropers() {
        foreach (DihedralCalculator dc in impropers) {
            float[] energies = new float[3];
            dc.AddEnergies(energies, positions);
            if (energies.Any(x => float.IsNaN(x))) {
                yield return (
                    dc.index0, 
                    dc.index1, 
                    dc.index2, 
                    dc.index3, 
                    energies
                );
            }
        }
    }

    public float3[] ComputeForces(
        bool computeStretches=true,
        bool computeBends=true,
        bool computeTorsions=true,
        bool computeImpropers=true,
        bool computeNonBondings=true
    ) {

        //for (int i=0; i<numNearbyAtoms; i++) {
        //    forces[i] = new float3();
        //}

        if (computeStretches)   stretches  .ForEach(x => x.AddForces(forces, positions, mobileMask));
        if (computeBends)       bends      .ForEach(x => x.AddForces(forces, positions, mobileMask));
        if (computeTorsions)    torsions  .ForEach(x => x.AddForces(forces, positions, mobileMask));
        if (computeImpropers)   impropers  .ForEach(x => x.AddForces(forces, positions, mobileMask));
        if (computeNonBondings) nonBondings.ForEach(x => x.AddForces(forces, positions, mobileMask));
        
        //if (computeStretches)   stretches  .AsParallel().ForAll(x => x.AddForces(forces));
        //if (computeBends)       bends      .AsParallel().ForAll(x => x.AddForces(forces));
        //if (computeDihedrals)   dihedrals  .AsParallel().ForAll(x => x.AddForces(forces));
        //if (computeNonBondings) nonBondings.AsParallel().ForAll(x => x.AddForces(forces));

        return forces;
    }

    public float GetStretchesEnergy() {
        float[] energies = new float[3];
        stretches.ForEach(x => x.AddEnergies(energies, positions));
        return energies[0];
    }

    public float GetBendsEnergy() {
        float[] energies = new float[3];
        bends.ForEach(x => x.AddEnergies(energies, positions));
        return energies[0];
    }

    public float GetTorsionsEnergy() {
        float[] energies = new float[3];
        torsions.ForEach(x => x.AddEnergies(energies, positions));
        return energies[0];
    }

    public float GetImpropersEnergy() {
        float[] energies = new float[3];
        impropers.ForEach(x => x.AddEnergies(energies, positions));
        return energies[0];
    }

    public float GetNonBondingEnergy() {
        float[] energies = new float[3];
        nonBondings.ForEach(x => x.AddEnergies(energies, positions));
        return energies[0];
    }

    public void TakeStep(float3[] forces, float stepSize, float maxStep) {

        float maxDis = forces.Select(x => math.length(x) * stepSize).Max();
        if (maxDis > maxStep) {
            stepSize *= maxStep / maxDis;
        }

        for (int i=0; i<numNearbyAtoms; i++) {
            float3 position = positions[i] += forces[i] * stepSize;
            atomRefs[i].position = position;
        }
        
    }

}

public class MMCalculator {
    public virtual void AddEnergies(float[] energies, float3[] positions) {}
    public virtual void AddForces(float3[] forces, float3[] positions, bool[] mobileMask) {}
    public virtual void AddForces(float3[] forces, float3[] positions, float[] forceMultiplier) {}
}


public class StretchCalculator : MMCalculator{

    public int index0;
    public int index1;

    public float req;
    public float keq;
    
	public StretchCalculator(
        Atom atom0, 
        Atom atom1, 
        (int, int) stretchKey
    ) {

        (index0, index1) = stretchKey;
        
        string[] types = new string[] {atom0.amber, atom1.amber};
        Stretch stretch = Graph.parameters.stretches
            .Where(x => x.types.SequenceEqual(types) || x.types.Reverse().SequenceEqual(types))
            .FirstOrDefault();

        if (stretch.IsDefault()) {
            throw new System.Exception();
        }

        keq = stretch.keq * Data.kcalToHartree;
        req = stretch.req;

    }
    
	public override void AddEnergies(float[] energies, float3[] positions) {
		CustomMathematics.EStretch(CustomMathematics.GetDistance(positions, index0, index1) - req, keq, energies);
	}

    public override void AddForces(float3[] forces, float3[] positions, bool[] mobileMask) {
        float3 v10 = positions[index1] - positions[index0];
        float r10 = math.length(v10);
        
        float de = CustomMathematics.EStretch(r10 - req, keq, 1);
        v10 *= de / r10;

        if (mobileMask[index0]) {
            forces[index0] += v10;
        }
        if (mobileMask[index1]) {
            forces[index1] -= v10;
        }
    }

    public override void AddForces(float3[] forces, float3[] positions, float[] forceMultiplier) {
        float3 v10 = positions[index1] - positions[index0];
        float r10 = math.length(v10);
        
        float de = CustomMathematics.EStretch(r10 - req, keq, 1);
        v10 *= de / r10;

        forces[index0] += v10 * forceMultiplier[index0];
        forces[index1] -= v10 * forceMultiplier[index1];

    }
}

public class BendCalculator : MMCalculator {
    
    public int index0;
    public int index1;
    public int index2;

    public float aeq;
    public float keq;

	public BendCalculator (
        Atom atom0, 
        Atom atom1, 
        Atom atom2, 
        (int, int, int) bendKey
    ) {

        (index0, index1, index2) = bendKey;

        string[] types = new string[] {atom0.amber, atom1.amber, atom2.amber};
        Bend bend = Graph.parameters.bends
            .Where(x => x.types.SequenceEqual(types) || x.types.Reverse().SequenceEqual(types))
            .FirstOrDefault();

        if (bend.IsDefault()) {
            throw new System.Exception();
        }

        keq = bend.keq * Data.kcalToHartree;
        aeq = bend.aeq * Mathf.Deg2Rad;
	}
    
	public override void AddEnergies(float[] energies, float3[] positions) {
		CustomMathematics.EBend(CustomMathematics.GetAngle(positions, index0, index1, index2) - aeq, keq, energies);
	}

    public override void AddForces(float3[] forces, float3[] positions, bool[] mobileMask) {
        float3 v10 = positions[index1] - positions[index0];
        float3 v12 = positions[index1] - positions[index2];

        float r10 = math.length(v10);
        float r12 = math.length(v12);

        v10 /= r10;
        v12 /= r12;

        float angle = CustomMathematics.UnsignedAngleRad(v10, v12);

        float de = CustomMathematics.EStretch(angle - aeq, keq, 1);

        float3 perp = math.cross(v10, v12);
        float3 force10 = math.cross(v10, perp) * de;
        float3 force12 = math.cross(v12, perp) * - de;

        if (mobileMask[index0]) {
            forces[index0] += force10;
        }
        if (mobileMask[index1]) {
            forces[index1] -= force10 + force12;
        }
        if (mobileMask[index2]) {
            forces[index2] += force12;
        }
    }

    public override void AddForces(float3[] forces, float3[] positions, float[] forceMultiplier) {
        float3 v10 = positions[index1] - positions[index0];
        float3 v12 = positions[index1] - positions[index2];

        float r10 = math.length(v10);
        float r12 = math.length(v12);

        v10 /= r10;
        v12 /= r12;

        float angle = CustomMathematics.UnsignedAngleRad(v10, v12);

        float de = CustomMathematics.EStretch(angle - aeq, keq, 1);

        float3 perp = math.cross(v10, v12);
        float3 force10 = math.cross(v10, perp) * de;
        float3 force12 = math.cross(v12, perp) * - de;

        forces[index0] += force10 * forceMultiplier[index0];
        forces[index1] -= (force10 + force12) * forceMultiplier[index1];
        forces[index2] += force12 * forceMultiplier[index2];
    }
}

public class DihedralCalculator : MMCalculator {
    
    public int index0;
    public int index1;
    public int index2;
    public int index3;

    public struct TorsionTerm {
        public float barrierHeight;
        public float phaseOffset;
        public float periodicity;

        public TorsionTerm(float barrierHeight, float phaseOffset, float periodicity) {
            this.barrierHeight = barrierHeight;
            this.phaseOffset = phaseOffset;
            this.periodicity = periodicity;

        }
    }
    
	public List<TorsionTerm> torsionTerms;

	public DihedralCalculator (
        Atom atom0, 
        Atom atom1, 
        Atom atom2, 
        Atom atom3, 
        bool proper,
        (int, int, int, int) dihedralKey
    ) {
        
        (index0, index1, index2, index3) = dihedralKey;

        string[] types = new string[] {atom0.amber, atom1.amber, atom2.amber, atom3.amber};
        torsionTerms = new List<TorsionTerm>();
        
        if (proper) {
            Torsion torsion = Graph.parameters.torsions
                .Where(x => x.TypeEquivalent(types))
                .FirstOrDefault();

            if (torsion.IsDefault()) {
                throw new System.Exception();
            } 

            for (int i=0; i<4; i++) {
                float barrierHeight = torsion.barrierHeights[i];
                if (barrierHeight == 0f) {continue;}
                torsionTerms.Add(
                    new TorsionTerm(
                        barrierHeight * Data.kcalToHartree / torsion.npaths, 
                        torsion.phaseOffsets[i] * Mathf.Deg2Rad, 
                        i + 1
                    )
                );
            }
        } else {
            ImproperTorsion improperTorsion = Graph.parameters.improperTorsions
                .Where(x => x.TypeEquivalent(types))
                .FirstOrDefault();
            if (improperTorsion.IsDefault()) {
                throw new System.Exception();
            }
            torsionTerms.Add(
                new TorsionTerm(
                    improperTorsion.barrierHeight * Data.kcalToHartree, 
                    improperTorsion.phaseOffset * Mathf.Deg2Rad, 
                    improperTorsion.periodicity
                )
            );
            
        }
	}
    
	public override void AddEnergies(float[] energies, float3[] positions) {
        float dihedral = CustomMathematics.GetDihedral(positions, index0, index1, index2, index3);
        foreach (TorsionTerm torsionTerm in torsionTerms) {
            CustomMathematics.EImproperTorsion(
                dihedral, 
                torsionTerm.barrierHeight,
                torsionTerm.phaseOffset,
                torsionTerm.periodicity,
                energies
            );
        }
	}

    public override void AddForces(float3[] forces, float3[] positions, bool[] mobileMask) {
        //https://hal-mines-paristech.archives-ouvertes.fr/hal-00924263/document
        //Bernard Monasse, Frédéric Boussinot. Determination of Forces from a Potential in Molecular Dynamics. 2014. ffhal-00924263

        float3 v01 = positions[index0] - positions[index1];
        float3 v12n = math.normalizesafe(positions[index1] - positions[index2]);
        float3 v23 = positions[index2] - positions[index3];

        float r01 = math.length(v01);
        float r23 = math.length(v23);

        float3 w1 = math.cross(v01 / r01, v12n);
        float3 w2 = math.cross(v23 / r23, v12n);

        float dihedral = CustomMathematics.SignedAngleRad(w1, w2, v12n);

        float de = 0f;
        foreach (TorsionTerm torsionTerm in torsionTerms) {
            de += CustomMathematics.EImproperTorsion(dihedral, torsionTerm.barrierHeight, torsionTerm.phaseOffset, torsionTerm.periodicity, 1);
        }
        
		float da_dr01 = 1f / (r01 * math.length(w1));
		float da_dr23 = 1f / (r23 * math.length(w2));

        float3 force0 = w1 * da_dr01 * de * 0.5f;
        float3 force3 = w2 * da_dr23 * de * 0.5f;

        float3 centre = (positions[index0] + positions[index1]) * 0.5f;

        float3 vc2 = positions[index2] - centre;
        float rc2 = math.length(vc2);
        
        float3 force2 = math.cross((
                    math.cross(vc2, force3) +
                    math.cross(v23, force3) * 0.5f +
                    math.cross(v12n, force0) * - 0.5f
                ) / (- rc2 * rc2),
                vc2
        );

        if (mobileMask[index0]) {
            forces[index0] += force0;
        }
        if (mobileMask[index1]) {
            forces[index1] -= (force0 + force2 + force3);
        }
        if (mobileMask[index2]) {
            forces[index2] += force2;
        }
        if (mobileMask[index3]) {
            forces[index3] += force3;
        }

    }

    public override void AddForces(float3[] forces, float3[] positions, float[] forceMultiplier) {
        //https://hal-mines-paristech.archives-ouvertes.fr/hal-00924263/document
        //Bernard Monasse, Frédéric Boussinot. Determination of Forces from a Potential in Molecular Dynamics. 2014. ffhal-00924263

        float3 v01 = positions[index0] - positions[index1];
        float3 v12n = math.normalizesafe(positions[index1] - positions[index2]);
        float3 v23 = positions[index2] - positions[index3];

        float r01 = math.length(v01);
        float r23 = math.length(v23);

        float3 w1 = math.cross(v01 / r01, v12n);
        float3 w2 = math.cross(v23 / r23, v12n);

        float dihedral = CustomMathematics.SignedAngleRad(w1, w2, v12n);

        float de = 0f;
        foreach (TorsionTerm torsionTerm in torsionTerms) {
            de += CustomMathematics.EImproperTorsion(dihedral, torsionTerm.barrierHeight, torsionTerm.phaseOffset, torsionTerm.periodicity, 1);
        }
        
		float da_dr01 = 1f / (r01 * math.length(w1));
		float da_dr23 = 1f / (r23 * math.length(w2));

        float3 force0 = w1 * da_dr01 * de * 0.5f;
        float3 force3 = w2 * da_dr23 * de * 0.5f;

        float3 centre = (positions[index0] + positions[index1]) * 0.5f;

        float3 vc2 = positions[index2] - centre;
        float rc2 = math.length(vc2);
        
        float3 force2 = math.cross((
                    math.cross(vc2, force3) +
                    math.cross(v23, force3) * 0.5f +
                    math.cross(v12n, force0) * - 0.5f
                ) / (- rc2 * rc2),
                vc2
        );

        forces[index0] += force0 * forceMultiplier[index0];
        forces[index1] -= (force0 + force2 + force3) * forceMultiplier[index1];
        forces[index2] += force2 * forceMultiplier[index2];
        forces[index3] += force3 * forceMultiplier[index3];

    }
	
}

public class PrecomputedNonBonding : MMCalculator {
	public float vdwR;
	public float vdwV;

	public float coulombFactor;
    
    public int index0;
    public int index1;

    public CT coulombType;
    public VT vdwType;
    float maxVdWGradient;

	public PrecomputedNonBonding(
        Atom atom0, 
        Atom atom1, 
        (int, int) nonBondKey,
        int graphDistance
    ) {
        
        (index0, index1) = nonBondKey;

        if (CustomMathematics.GetDistance(atom0, atom1) > Settings.maxNonBondingCutoff) {
            throw new System.Exception();
        }

        AtomicParameter atomicParameter0 = Graph.parameters.atomicParameters
            .Where(x => x.type == atom0.amber)
            .FirstOrDefault();
            
        if (atomicParameter0 == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "No Atomic Parameter for Amber Type: {0}",
                atom0.amber
            );
            throw new System.Exception();
        }
            
        AtomicParameter atomicParameter1 = Graph.parameters.atomicParameters
            .Where(x => x.type == atom1.amber)
            .FirstOrDefault();
            
        if (atomicParameter1 == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "No Atomic Parameter for Amber Type: {0}",
                atom1.amber
            );
            throw new System.Exception();
        }

        NonBonding nonBonding = Graph.parameters.nonbonding;

        float cScale = graphDistance == -1 
            ? nonBonding.cScales[0]
            : nonBonding.cScales[graphDistance];
            
        float vScale = graphDistance == -1 
            ? nonBonding.vScales[0]
            : nonBonding.vScales[graphDistance];

        if (cScale < 0) {
            coulombFactor = ( atom0.partialCharge * atom0.partialCharge * Data.kcalToHartree) /  ( -cScale * Graph.parameters.dielectricConstant);
        } else {
		    coulombFactor = ( atom0.partialCharge * atom0.partialCharge * cScale * Data.kcalToHartree) / Graph.parameters.dielectricConstant;
        }

        
        //vdwR = (
        //    (atomID0.pdbID.element == "C" ? 1.5f : 1) * atomicParameter0.radius +
        //    (atomID1.pdbID.element == "C" ? 1.5f : 1) * atomicParameter1.radius
        //) * 0.5f;
        
        vdwR = (atomicParameter0.radius + atomicParameter1.radius) * 0.5f;

		vdwV = Mathf.Sqrt ((atomicParameter0.wellDepth + atomicParameter1.wellDepth) * Data.kcalToHartree) * vScale;

        coulombType = nonBonding.coulombType;
        vdwType = nonBonding.vdwType;

        //Max gradient is the maximum gradient of the VdW
        //This function is the value of the 1st derivative where the 2nd derivative = 0
        maxVdWGradient = 12f * vdwV * Mathf.Pow(7f / 13f, 7f / 6f) * (1f - (7f / 13f)) / vdwR;

	}


	public override void AddEnergies(float[] energies, float3[] positions) {
        float r = CustomMathematics.GetDistance(positions, index0, index1);
		CustomMathematics.EVdWAmber(r, vdwV, vdwR, energies);
		CustomMathematics.EElectrostaticR1(r, coulombFactor, energies);
	}

    public override void AddForces(float3[] forces, float3[] positions, bool[] mobileMask) {
        float3 v10 = positions[index1] - positions[index0];
        float r10 = math.length(v10);

        float de =  Mathf.Max(
            CustomMathematics.EVdWAmber(r10, vdwV, vdwR, 1) + CustomMathematics.EElectrostatic(r10, coulombFactor, coulombType, 1),
            - maxVdWGradient
        );

        v10 *= de / r10;
        if (mobileMask[index0]) {
            forces[index0] += v10;
        }
        if (mobileMask[index1]) {
            forces[index1] -= v10;
        }
    }

    public override void AddForces(float3[] forces, float3[] positions, float[] forceMultiplier) {
        float3 v10 = positions[index1] - positions[index0];
        float r10 = math.length(v10);

        float de =  Mathf.Max(
            CustomMathematics.EVdWAmber(r10, vdwV, vdwR, 1) + CustomMathematics.EElectrostatic(r10, coulombFactor, coulombType, 1),
            - maxVdWGradient
        );

        v10 *= de / r10;
        forces[index0] += v10 * forceMultiplier[index0];
        forces[index1] -= v10 * forceMultiplier[index1];

    }

}
