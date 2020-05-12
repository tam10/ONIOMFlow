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
using Amber = Constants.Amber;

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

    public Parameters parameters;

	public Geometry geometry;


	public IEnumerator SetGeometry(Geometry geometry, List<ResidueID> mobileResidueIDs=null) {

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
        parameters.UpdateParameters(geometry.parameters, true);

        List<ResidueID> nearbyResidueIDs = new List<ResidueID>();
        
        nearbyAtomIDs = new List<AtomID>();
        mobileAtomIDs = new List<AtomID>();
        atomIDToIndex = new Dictionary<AtomID, int>();

        List<bool> mobileMaskList = new List<bool>();

        numNearbyAtoms = 0;
        numMobileAtoms = 0;
        int numNearbyResidues = 0;

        void AddNearbyResidue(ResidueID nearbyResidueID, Residue nearbyResidue, bool mobile) {
            // if (!nearbyResidueIDs.Contains(nearbyResidueID)) {
            //     return;
            // }
            nearbyResidueIDs.Add(nearbyResidueID);
            numNearbyResidues++;
            foreach ((PDBID pdbID, Atom atom) in nearbyResidue.EnumerateAtoms()) {
                AtomID atomID = new AtomID(nearbyResidueID, pdbID);
                nearbyAtomIDs.Add(atomID);
                atomIDToIndex[atomID] = numNearbyAtoms;

                mobileMaskList.Add(mobile);

                if (mobile) {
                    mobileAtomIDs.Add(atomID);
                    numMobileAtoms++;
                }
                numNearbyAtoms++;
            }
        }

        int GetGraphDistance(int[][] graph, int index0, int index1) {
            foreach (int connection0 in graph[index0]) {
                if (connection0 == index1) {return 1;}
                foreach (int connection1 in graph[connection0]) {
                    if (connection1 == index1) {return 2;}
                    foreach (int connection2 in graph[connection1]) {
                        if (connection2 == index1) {return 3;}
                    }
                }
            }
            return -1;
        }

        foreach ((ResidueID residueID, Residue residue) in geometry.EnumerateResidues()) {
            bool mobile = mobileResidueIDs == null ? true : mobileResidueIDs.Contains(residueID);

            if (mobile) {
                foreach (ResidueID nearbyResidueID in residue.ResiduesWithinDistance(Settings.maxNonBondingCutoff)) {
                    if (!nearbyResidueIDs.Contains(nearbyResidueID)) {
                        Residue nearbyResidue;
                        if (!geometry.TryGetResidue(nearbyResidueID, out nearbyResidue)) {
                            CustomLogger.LogFormat(
                                EL.ERROR,
                                "Could not add Nearby Residue '{0}' - Residue not present in Geometry!",
                                nearbyResidueID
                            );
                        }
                        AddNearbyResidue(nearbyResidueID, nearbyResidue, mobile);
                    }
                }
            }
        }

        mobileMask = mobileMaskList.ToArray();

        int currentResidueNum = 0;
        //Copy across partial charges
        foreach (ResidueID residueID in nearbyResidueIDs) {
            Residue forceFieldResidue = geometry.GetResidue(residueID);

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

            foreach ((PDBID pdbID, Atom atom) in forceFieldResidue.EnumerateAtoms()) {
                Atom standardAtom;
                if (standardResidue.TryGetAtom(pdbID, out standardAtom)) {
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
        
        AtomicParameter[] atomicParameters = new AtomicParameter[numNearbyAtoms];
        Amber[] ambers = new Amber[numNearbyAtoms];
        float[] charges = new float[numNearbyAtoms];
        int[][] connections = new int[numNearbyAtoms][];

        float nonBonSq = CustomMathematics.Squared(Settings.maxNonBondingCutoff);

        //First pass to populate arrays
        for (int i=0; i<numNearbyAtoms; i++) {
            AtomID atomID0 = nearbyAtomIDs[i];
            Atom atom0 = geometry.GetAtom(atomID0);
            
            atomRefs[i] = atom0;
            positions[i] = atom0.position;
            ambers[i] = atom0.amber;
            charges[i] = atom0.partialCharge;
            atomicParameters[i] = parameters.GetAtomicParameter(atom0.amber);

            List<int> neighbours = new List<int>();
            foreach ((AtomID atomID, BT bondType) in atom0.EnumerateConnections()) {
                int neighbour;
                if (!atomIDToIndex.TryGetValue(atomID, out neighbour)) {
                    CustomLogger.LogFormat(
                        EL.WARNING,
                        "Skipping connected atom {0} in Graph generation - not in Nearby Atoms List",
                        atomID
                    );
                } else {
                    neighbours.Add(neighbour);
                }
            }
            connections[i] = neighbours.ToArray();

        }

        float nonBonTimeTot = 0f;
        float impropersTimeTot = 0f;
        float stretchTimeTot = 0f;
        float bendTimeTot = 0f;
        float torsionTimeTot = 0f;

        int2 ij = new int2();
        int4 ijkl = new int4();

        Amber2 stretchTypes = Amber2.empty;
        Amber3 bendTypes = Amber3.empty;
        Amber4 dihedralTypes = Amber4.empty;

        NonBonding nonBonding = parameters.nonbonding;
        
        CustomLogger.LogOutput(
            "Start          AtN    NonBon     Stretch    Bend       Improper   Torsion"
        );

        for (int i=0; i<numNearbyAtoms; i++) {

            if (!mobileMask[i]) {
                continue;
            }

            ij.x = ijkl.x = i;

            //
            float startTime = Time.realtimeSinceStartup;

            float3 position0 = positions[i];
            float charge0 = charges[i];

            AtomicParameter atomicParameter0 = atomicParameters[i];
                
            //Non-bonding terms
            if (atomicParameter0 == null) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "No Atomic Parameter for Amber Type: {0}",
                    ambers[i]
                );
            } else {
                for (int j=i+1; j<numNearbyAtoms; j++) {
                    if (math.distancesq(position0, positions[j]) < nonBonSq) {
                        
                        ij.y = j;
                        AtomicParameter atomicParameter1 = atomicParameters[j];

                        float vdwR = (atomicParameter0.radius + atomicParameter1.radius);

                        if (vdwR == 0f) {
                            continue;
                        }

                        nonBondings.Add(new PrecomputedNonBonding(
                            atomicParameter0,
                            atomicParameter1,
                            nonBonding,
                            ij, 
                            GetGraphDistance(connections, i, j),
                            charge0 * charges[j],
                            parameters.dielectricConstant,
                            vdwR
                        ));

                    }
                }
            }

            
            float nonBonTime = Time.realtimeSinceStartup - startTime;
            nonBonTimeTot += nonBonTime;

            int[] atom0Neighbours = connections[i];

            //
            startTime = Time.realtimeSinceStartup;

            //Impropers
            if (atom0Neighbours.Length == 3) {

                dihedralTypes.amber1 = ambers[i];

                //Cycle over the 6 possible improper combinations for this central Atom
                foreach (int3 improperCycle in improperCycles) {
                    int j = atom0Neighbours[improperCycle.x];
                    int k = atom0Neighbours[improperCycle.y];
                    int l = atom0Neighbours[improperCycle.z];

                    //Centre atom is outer loop so it's impossible to be added in reverse
                    dihedralTypes.amber0 = ambers[j];
                    dihedralTypes.amber2 = ambers[k];
                    dihedralTypes.amber3 = ambers[l];

                    
                    ImproperTorsion improper;
                    if (parameters.TryGetImproperTorsion(dihedralTypes, out improper, true)) {
                        impropers.Add(new DihedralCalculator(
                            improper, 
                            new int4(j,i,k,l)
                        ));
                    }

                }
                
            }
            
            float impropersTime = Time.realtimeSinceStartup - startTime;
            impropersTimeTot += impropersTime;
            
            
            float stretchTime = 0f;
            float bendTime = 0f;
            float torsionTime = 0f;

            stretchTypes.amber0 = bendTypes.amber0 = dihedralTypes.amber0 = ambers[i];

            //Stretches
            foreach (int j in atom0Neighbours) {

                //
                startTime = Time.realtimeSinceStartup;
                bendTypes.amber1 = dihedralTypes.amber1 = stretchTypes.amber1 = ambers[j];
                
                ijkl.y = j;
                if (j > i) {
                    
                    Stretch stretch;
                    if (parameters.TryGetStretch(stretchTypes, out stretch, true)) {
                        stretches.Add(new StretchCalculator(
                            parameters, 
                            stretch, 
                            ijkl.xy
                        ));
                    } else {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "No Stretch Parameter for Atoms: '{0}'-'{1}'. Ambers: {2}-{3}",
                            () => new object[] {
                                nearbyAtomIDs[i],
                                nearbyAtomIDs[j],
                                stretchTypes.amber0,
                                stretchTypes.amber1
                            }
                        );
                    }
                }

                stretchTime += Time.realtimeSinceStartup - startTime;
                stretchTimeTot += stretchTime;
                
                int[] atom1Neighbours = connections[j];

                //Bends
                foreach (int k in atom1Neighbours) {

                    if (k == i) {continue;}
                    //
                    startTime = Time.realtimeSinceStartup;
                    bendTypes.amber2 = dihedralTypes.amber2 = ambers[k];

                    ijkl.z = k;
                    if (k > i) {
                        Bend bend;
                        if (parameters.TryGetBend(bendTypes, out bend, true)) {
                            bends.Add(new BendCalculator(
                                parameters,
                                bend, 
                                ijkl.xyz
                            ));
                        } else {
                            CustomLogger.LogFormat(
                                EL.WARNING,
                                "No Bend Parameter for Atoms: '{0}'-'{1}'-'{2}'. Ambers: {3}-{4}-{5}",
                                () => new object[] {
                                    nearbyAtomIDs[i],
                                    nearbyAtomIDs[j],
                                    nearbyAtomIDs[k],
                                    bendTypes.amber0,
                                    bendTypes.amber1,
                                    bendTypes.amber2
                                }
                            );
                        }
                    }
                    
                    

                    bendTime += Time.realtimeSinceStartup - startTime;
                    bendTimeTot += bendTime;
                    
                    int[] atom2Neighbours = connections[k];
                    
                    //
                    startTime = Time.realtimeSinceStartup;
                    
                    //Propers
                    foreach (int l in atom2Neighbours) {
                        if (l <= i || l == j) {continue;}
                        dihedralTypes.amber3 = ambers[l];

                        Torsion torsion;
                        if (parameters.TryGetTorsion(dihedralTypes, out torsion, true)) {
                            torsions.Add(new DihedralCalculator(
                                torsion, 
                                ijkl
                            ));
                            break;
                        }
                    }
                    
                    torsionTime += Time.realtimeSinceStartup - startTime;
                    torsionTimeTot += torsionTime;
                }

            }
            
            if (Timer.yieldNow) {
                NotificationBar.SetTaskProgress(
                    TID.UPDATE_PARAMETERS, 
                    CustomMathematics.Map(i, 0f, numMobileAtoms, 0.1f, 1f)
                );
                yield return null;
            }

            //CustomLogger.LogOutput(
            //    "Processed Atom {0,4} {1,10:0.000000} {2,10:0.000000} {3,10:0.000000} {4,10:0.000000} {5,10:0.000000}",
            //    i+1,
            //    nonBonTime,
            //    stretchTime,
            //    bendTime,
            //    impropersTime,
            //    torsionTime
            //);
        }
        
        CustomLogger.LogOutput(
            "Finished            {0,10:0.000000} {1,10:0.000000} {2,10:0.000000} {3,10:0.000000} {4,10:0.000000}",
            nonBonTimeTot,
            stretchTimeTot,
            bendTimeTot,
            impropersTimeTot,
            torsionTimeTot
        );
        
        CustomLogger.LogOutput(
            "Count               {0,10} {1,10} {2,10} {3,10} {4,10}",
            nonBondings.Count,
            stretches.Count,
            bends.Count,
            impropers.Count,
            torsions.Count
        );

        NotificationBar.ClearTask(TID.UPDATE_PARAMETERS);
	}

    public IEnumerable<(int2, float[])> EnumerateBadStretches() {
        foreach (StretchCalculator sc in stretches) {
            float[] energies = new float[3];
            sc.AddEnergies(energies, positions);
            if (energies.Any(x => float.IsNaN(x))) {
                yield return (
                    sc.key, 
                    energies
                );
            }
        }
    }

    public IEnumerable<(int2, float[])> EnumerateBadNonBondings() {
        foreach (PrecomputedNonBonding pcn in nonBondings) {
            float[] energies = new float[3];
            pcn.AddEnergies(energies, positions);
            if (energies.Any(x => float.IsNaN(x))) {
                yield return (
                    pcn.key, 
                    energies
                );
            }
        }
    }

    public IEnumerable<(int3, float[])> EnumerateBadBends() {
        foreach (BendCalculator bc in bends) {
            float[] energies = new float[3];
            bc.AddEnergies(energies, positions);
            if (energies.Any(x => float.IsNaN(x))) {
                yield return (
                    bc.key, 
                    energies
                );
            }
        }
    }

    public IEnumerable<(int4, float[])> EnumerateBadTorsions() {
        foreach (DihedralCalculator dc in torsions) {
            float[] energies = new float[3];
            dc.AddEnergies(energies, positions);
            if (energies.Any(x => float.IsNaN(x))) {
                yield return (
                    dc.key, 
                    energies
                );
            }
        }
    }

    public IEnumerable<(int4, float[])> EnumerateBadImpropers() {
        foreach (DihedralCalculator dc in impropers) {
            float[] energies = new float[3];
            dc.AddEnergies(energies, positions);
            if (energies.Any(x => float.IsNaN(x))) {
                yield return (
                    dc.key, 
                    energies
                );
            }
        }
    }

    public IEnumerator ReportBadTerms() {
        IEnumerable<(int, AtomID)> badIDs = forces
            .Select((force,index)=>(force,index))
            .Where(fi => math.any(math.isnan(fi.force)))
            .Select(fi => (fi.index, mobileAtomIDs[fi.index]));

        yield return null;

        foreach ((int index, AtomID atomID) in badIDs) {
            Atom badAtom = atomRefs[index];
            CustomLogger.LogOutput(
                string.Format(
                    "Bad force on Atom ID: {0,10} (AMBER: {1,3}) Force: {2}",
                    atomID,
                    badAtom.amber,
                    forces[index]
                )
            );
        }
        
        yield return null;

        foreach ((int2 key, float[] energies) in EnumerateBadStretches()) {
            CustomLogger.LogOutput(
                string.Format(
                    "Bad Stretch ('{0}'-'{1}'). Energy: {2}. 1st Derivative: {3}",
                    mobileAtomIDs[key.x],
                    mobileAtomIDs[key.y],
                    energies[0],
                    energies[1]
                )
            );
        }
        
        yield return null;

        foreach ((int2 key, float[] energies) in EnumerateBadNonBondings()) {
            CustomLogger.LogOutput(
                string.Format(
                    "Bad Non-Bonding ('{0}'-'{1}'). Energy: {2}. 1st Derivative: {3}",
                    mobileAtomIDs[key.x],
                    mobileAtomIDs[key.y],
                    energies[0],
                    energies[1]
                )
            );
        }
        
        yield return null;

        foreach ((int3 key, float[] energies) in EnumerateBadBends()) {
            CustomLogger.LogOutput(
                string.Format(
                    "Bad Bend ('{0}'-'{1}'-'{2}'). Energy: {3}. 1st Derivative: {4}",
                    mobileAtomIDs[key.x],
                    mobileAtomIDs[key.y],
                    mobileAtomIDs[key.z],
                    energies[0],
                    energies[1]
                )
            );
        }
        
        yield return null;

        foreach ((int4 key, float[] energies) in EnumerateBadTorsions()) {
            CustomLogger.LogOutput(
                string.Format(
                    "Bad Torsion ('{0}'-'{1}'-'{2}'-'{3}'). Energy: {4}. 1st Derivative: {5}",
                    mobileAtomIDs[key.x],
                    mobileAtomIDs[key.y],
                    mobileAtomIDs[key.z],
                    mobileAtomIDs[key.w],
                    energies[0],
                    energies[1]
                )
            );
        }
        
        yield return null;

        foreach ((int4 key, float[] energies) in EnumerateBadImpropers()) {
            CustomLogger.LogOutput(
                string.Format(
                    "Bad Improper ('{0}'-'{1}'-'{2}'-'{3}'). Energy: {4}. 1st Derivative: {5}",
                    mobileAtomIDs[key.x],
                    mobileAtomIDs[key.y],
                    mobileAtomIDs[key.z],
                    mobileAtomIDs[key.w],
                    energies[0],
                    energies[1]
                )
            );
        }
        
        yield return null;
    }

    public float3[] ComputeForces(
        bool computeStretches=true,
        bool computeBends=true,
        bool computeTorsions=true,
        bool computeImpropers=true,
        bool computeNonBondings=true
    ) {

        for (int i=0; i<numNearbyAtoms; i++) {
            forces[i] = new float3();
        }

        //float st = Time.realtimeSinceStartup;
        //float s = Time.realtimeSinceStartup;
        if (computeStretches)   stretches  .AsParallel().ForAll(x => x.AddForces(forces, positions, mobileMask));
        //if (computeStretches)   stretches  .ForEach(x => x.AddForces(forces, positions, mobileMask));
        //float d = Time.realtimeSinceStartup - s;
        //CustomLogger.LogOutput("Stretches: {0,8:0.0000}", d);

        //s = Time.realtimeSinceStartup;
        if (computeBends)       bends      .AsParallel().ForAll(x => x.AddForces(forces, positions, mobileMask));
        //if (computeBends)       bends      .ForEach(x => x.AddForces(forces, positions, mobileMask));
        //d = Time.realtimeSinceStartup - s;
        //CustomLogger.LogOutput("Bends    : {0,8:0.0000}", d);

        //s = Time.realtimeSinceStartup;
        if (computeTorsions)    torsions   .AsParallel().ForAll(x => x.AddForces(forces, positions, mobileMask));
        //if (computeTorsions)    torsions   .ForEach(x => x.AddForces(forces, positions, mobileMask));
        //d = Time.realtimeSinceStartup - s;
        //CustomLogger.LogOutput("Torsions : {0,8:0.0000}", d);

        //s = Time.realtimeSinceStartup;
        if (computeImpropers)   impropers  .AsParallel().ForAll(x => x.AddForces(forces, positions, mobileMask));
        //if (computeImpropers)   impropers  .ForEach(x => x.AddForces(forces, positions, mobileMask));
        //d = Time.realtimeSinceStartup - s;
        //CustomLogger.LogOutput("Improper : {0,8:0.0000}", d);

        //s = Time.realtimeSinceStartup;
        if (computeNonBondings) nonBondings.AsParallel().ForAll(x => x.AddForces(forces, positions, mobileMask));
        //if (computeNonBondings) nonBondings.ForEach(x => x.AddForces(forces, positions, mobileMask));
        //d = Time.realtimeSinceStartup - s;
        //CustomLogger.LogOutput("NonBond  : {0,8:0.0000}", d);
        //CustomLogger.LogOutput("Tot      : {0,8:0.0000}", Time.realtimeSinceStartup - st);

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
            atomRefs[i].position = positions[i] += forces[i] * stepSize;
        }
        
    }

}

public class MMCalculator {
    public virtual void AddEnergies(float[] energies, float3[] positions) {}
    public virtual void AddForces(float3[] forces, float3[] positions, bool[] mobileMask) {}
    public virtual void AddForces(float3[] forces, float3[] positions, float[] forceMultiplier) {}
}


public class StretchCalculator : MMCalculator{

    public int2 key;

    public float req;
    public float keq;
    
	public StretchCalculator(
        Parameters parameters,
        Stretch stretch, 
        int2 stretchKey
    ) {

        key = stretchKey;

        keq = stretch.keq * Data.kcalToHartree;
        req = stretch.req;

    }
    
	public override void AddEnergies(float[] energies, float3[] positions) {
		CustomMathematics.EStretch(CustomMathematics.GetDistance(positions, key.x, key.y) - req, keq, energies);
	}

    public override void AddForces(float3[] forces, float3[] positions, bool[] mobileMask) {
        float3 v10 = positions[key.y] - positions[key.x];
        float r10 = math.length(v10);
        
        float de = CustomMathematics.EStretch(r10 - req, keq, 1);
        v10 *= de / r10;

        if (mobileMask[key.x]) {
            forces[key.x] += v10;
        }
        if (mobileMask[key.y]) {
            forces[key.y] -= v10;
        }
    }

    public override void AddForces(float3[] forces, float3[] positions, float[] forceMultiplier) {
        float3 v10 = positions[key.y] - positions[key.x];
        float r10 = math.length(v10);
        
        float de = CustomMathematics.EStretch(r10 - req, keq, 1);
        v10 *= de / r10;

        forces[key.x] += v10 * forceMultiplier[key.x];
        forces[key.y] -= v10 * forceMultiplier[key.y];

    }
}

public class BendCalculator : MMCalculator {
    
    public int3 key;

    public float aeq;
    public float keq;

	public BendCalculator (
        Parameters parameters,
        Bend bend,
        int3 bendKey
    ) {

        key = bendKey;

        keq = bend.keq * Data.kcalToHartree;
        aeq = bend.aeq * Mathf.Deg2Rad;
	}
    
	public override void AddEnergies(float[] energies, float3[] positions) {
		CustomMathematics.EBend(CustomMathematics.GetAngle(positions, key.x, key.y, key.z) - aeq, keq, energies);
	}

    public override void AddForces(float3[] forces, float3[] positions, bool[] mobileMask) {
        float3 v10 = positions[key.y] - positions[key.x];
        float3 v12 = positions[key.y] - positions[key.z];

        float r10 = math.length(v10);
        float r12 = math.length(v12);

        v10 /= r10;
        v12 /= r12;

        float angle = CustomMathematics.UnsignedAngleRad(v10, v12);

        float de = CustomMathematics.EStretch(angle - aeq, keq, 1);

        float3 perp = math.cross(v10, v12);
        float3 force10 = math.cross(v10, perp) * de;
        float3 force12 = math.cross(v12, perp) * - de;

        if (mobileMask[key.x]) {
            forces[key.x] += force10;
        }
        if (mobileMask[key.y]) {
            forces[key.y] -= force10 + force12;
        }
        if (mobileMask[key.z]) {
            forces[key.z] += force12;
        }
    }

    public override void AddForces(float3[] forces, float3[] positions, float[] forceMultiplier) {
        float3 v10 = positions[key.y] - positions[key.x];
        float3 v12 = positions[key.y] - positions[key.z];

        float r10 = math.length(v10);
        float r12 = math.length(v12);

        v10 /= r10;
        v12 /= r12;

        float angle = CustomMathematics.UnsignedAngleRad(v10, v12);

        float de = CustomMathematics.EStretch(angle - aeq, keq, 1);

        float3 perp = math.cross(v10, v12);
        float3 force10 = math.cross(v10, perp) * de;
        float3 force12 = math.cross(v12, perp) * - de;

        forces[key.x] += force10 * forceMultiplier[key.x];
        forces[key.y] -= (force10 + force12) * forceMultiplier[key.y];
        forces[key.z] += force12 * forceMultiplier[key.z];
    }
}

public class DihedralCalculator : MMCalculator {
    
    public int4 key;

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
    
	public List<TorsionTerm> torsionTerms = new List<TorsionTerm>();

	public DihedralCalculator (
        Torsion torsion, 
        int4 dihedralKey
    ) {

        
        key = dihedralKey;

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
	}

    public DihedralCalculator (
        ImproperTorsion improperTorsion, 
        int4 dihedralKey
    ) {
        
        key = dihedralKey;
        torsionTerms.Add(
            new TorsionTerm(
                improperTorsion.barrierHeight * Data.kcalToHartree, 
                improperTorsion.phaseOffset * Mathf.Deg2Rad, 
                improperTorsion.periodicity
            )
        );
    }
    
	public override void AddEnergies(float[] energies, float3[] positions) {
        float dihedral = CustomMathematics.GetDihedral(positions, key.x, key.y, key.z, key.w);
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

        float3 v01 = positions[key.x] - positions[key.y];
        float3 v12n = math.normalizesafe(positions[key.y] - positions[key.z]);
        float3 v23 = positions[key.z] - positions[key.w];

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

        float3 centre = (positions[key.x] + positions[key.y]) * 0.5f;

        float3 vc2 = positions[key.z] - centre;
        float rc2 = math.length(vc2);
        
        float3 force2 = math.cross(
            (
                math.cross(vc2, force3) +
                math.cross(v23, force3) * 0.5f +
                math.cross(v12n, force0) * - 0.5f
            ) / (- rc2 * rc2),
            vc2
        );

        if (mobileMask[key.x]) {
            forces[key.x] += force0;
        }
        if (mobileMask[key.y]) {
            forces[key.y] -= (force0 + force2 + force3);
        }
        if (mobileMask[key.z]) {
            forces[key.z] += force2;
        }
        if (mobileMask[key.w]) {
            forces[key.w] += force3;
        }

    }

    public override void AddForces(float3[] forces, float3[] positions, float[] forceMultiplier) {
        //https://hal-mines-paristech.archives-ouvertes.fr/hal-00924263/document
        //Bernard Monasse, Frédéric Boussinot. Determination of Forces from a Potential in Molecular Dynamics. 2014. ffhal-00924263

        float3 v01 = positions[key.x] - positions[key.y];
        float3 v12n = math.normalizesafe(positions[key.y] - positions[key.z]);
        float3 v23 = positions[key.z] - positions[key.w];

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

        float3 centre = (positions[key.x] + positions[key.y]) * 0.5f;

        float3 vc2 = positions[key.z] - centre;
        float rc2 = math.length(vc2);
        
        float3 force2 = math.cross((
                    math.cross(vc2, force3) +
                    math.cross(v23, force3) * 0.5f +
                    math.cross(v12n, force0) * - 0.5f
                ) / (- rc2 * rc2),
                vc2
        );

        forces[key.x] += force0 * forceMultiplier[key.x];
        forces[key.y] -= (force0 + force2 + force3) * forceMultiplier[key.y];
        forces[key.z] += force2 * forceMultiplier[key.z];
        forces[key.w] += force3 * forceMultiplier[key.w];

    }
	
}

public class PrecomputedNonBonding : MMCalculator {
	public float vdwR;
	public float vdwV;

	public float coulombFactor;
    
    public int2 key;

    public CT coulombType;
    public VT vdwType;
    float maxVdWGradient;

	public PrecomputedNonBonding(
        AtomicParameter atomicParameter0,
        AtomicParameter atomicParameter1,
        NonBonding nonBonding,
        int2 nonBondKey,
        int graphDistance,
        float partialChargeProduct,
        float dielectricConstant,
        float averageRadius
    ) {
        
        key = nonBondKey;

        float cScale = graphDistance == -1 
            ? nonBonding.cScales[0]
            : nonBonding.cScales[graphDistance];
            
        float vScale = graphDistance == -1 
            ? nonBonding.vScales[0]
            : nonBonding.vScales[graphDistance];

        if (cScale < 0) {
            coulombFactor = ( partialChargeProduct * Data.kcalToHartree) /  ( -cScale * dielectricConstant);
        } else {
		    coulombFactor = ( partialChargeProduct * cScale * Data.kcalToHartree) / dielectricConstant;
        }
        
        vdwR = averageRadius;
 
		vdwV = Mathf.Sqrt ((atomicParameter0.wellDepth + atomicParameter1.wellDepth) * Data.kcalToHartree) * vScale;

        coulombType = nonBonding.coulombType;
        vdwType = nonBonding.vdwType;

        //Max gradient is the maximum gradient of the VdW
        //This function is the value of the 1st derivative where the 2nd derivative = 0
        maxVdWGradient = 12f * vdwV * Mathf.Pow(7f / 13f, 7f / 6f) * (1f - (7f / 13f)) / vdwR;

	}


	public override void AddEnergies(float[] energies, float3[] positions) {
        float r = CustomMathematics.GetDistance(positions, key.x, key.y);
		CustomMathematics.EVdWAmber(r, vdwV, vdwR, energies);
		CustomMathematics.EElectrostaticR1(r, coulombFactor, energies);
	}

    public override void AddForces(float3[] forces, float3[] positions, bool[] mobileMask) {
        float3 v10 = positions[key.y] - positions[key.x];
        float r10 = math.length(v10);

        float de =  Mathf.Max(
            CustomMathematics.EVdWAmber(r10, vdwV, vdwR, 1) + CustomMathematics.EElectrostatic(r10, coulombFactor, coulombType, 1),
            - maxVdWGradient
        );

        v10 *= de / r10;
        if (mobileMask[key.x]) {
            forces[key.x] += v10;
        }
        if (mobileMask[key.y]) {
            forces[key.y] -= v10;
        }
    }

    public override void AddForces(float3[] forces, float3[] positions, float[] forceMultiplier) {
        float3 v10 = positions[key.y] - positions[key.x];
        float r10 = math.length(v10);

        float de =  Mathf.Max(
            CustomMathematics.EVdWAmber(r10, vdwV, vdwR, 1) + CustomMathematics.EElectrostatic(r10, coulombFactor, coulombType, 1),
            - maxVdWGradient
        );

        v10 *= de / r10;
        forces[key.x] += v10 * forceMultiplier[key.x];
        forces[key.y] -= v10 * forceMultiplier[key.y];

    }

}
