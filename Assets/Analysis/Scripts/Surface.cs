using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Mathematics;
using Element = Constants.Element;
using Amber = Constants.Amber;
using EL = Constants.ErrorLevel;
using BT = Constants.BondType;

public class SurfaceAnalysis : MonoBehaviour {

    ////////////
    // pwSASA //
    ////////////
    
    // pwSASA Based on:
    // J Chem Theory Comput. 2018 Nov 13; 14(11): 5797–5814.
    // He Huang and Carlos Simmerling
    // A Fast Pairwise Approximation of Solvent Accessible Surface Area for Implicit Solvent Simulations of Proteins on CPUs and GPUs

    // This is an empirical approximation to the SASA using parameters defined in SASA.txt

    public int param_m = 10;
    public int param_n = 4;

    public float maxSASA = 0.00001f;

    public List<(uint[], SASAType)> sasaTypes;
    public Dictionary<AtomID, SASAType> atomTypes;
    public Dictionary<AtomID, Amber> ambers;


    /// <summary>
    /// Initialise this Surface Calculator for SASA Analysis
    /// </summary>
    /// <param name="geometry">The geometry to analyse</param>
    public IEnumerator InitialisePWSASA(Geometry geometry) {

        string sasaPath;
		if (!Settings.TryGetPath(Settings.dataPath, Settings.sasaFileName, out sasaPath)) {
			CustomLogger.LogFormat(
                EL.ERROR,
                "Could not find Projects Settings File: {0}", 
                Settings.sasaFileName
            );
            yield break;
		}


        yield return GetSASATypes(sasaPath);

        ambers = geometry.EnumerateAtomIDPairs()
            .ToDictionary(x => x.atomID, x => x.atom.amber);

        yield return GetAtomTypes(geometry);
    }

    ////////////////////
    // Numerical SASA //
    ////////////////////
    
    // Modified Shrake-Rupley algorithm
    // Journal of Molecular Biology 1973, 79 (2), 351–371.
    // 57. Shrake A; Rupley JA
    // Environment and Exposure to Solvent of Protein Atoms-Lysozyme and Insulin. 

    // 1) Generate a sphere of radius 1 on the origin
    // 2) For each Atom a0 and nearby Atoms a1, get their radii and positions (r0, r1, p0 and p1).
    // 3) Define a cutoff as r0 + r1 + (2*r(H2O) = 2.8A)
    // 4) If ||p0-p1|| < cutoff, keep p1
    // 5) Translate the system so p0 is on the origin
    // 6) Scale the system by 1 / (r0 + (r(H2O) = 1.4A)) so the solvent accessible surface of a0 is equal to the sphere
    // 7) For each point on the sphere, if it is within (r1 + r(H2O)) / (r0 + r(H2O)) of p1, it is not solvent accessible
    // 8) The score for an atom is its solvent surface area multiplied by the fraction of solvent accessible points on its sphere

    public float solventRadius = 1.4f;
    public int gridResolution = 3;

    Dictionary<AtomID, (float3 position, float radius)> positionsRadii;

    float3[] vertices;
    int3[] tris;
    int numSpherePoints;

    public IEnumerator InitialiseNumSASA(Geometry geometry) {

        if (geometry.parameters == null) {
			CustomLogger.LogFormat(
                EL.ERROR,
                "Geometry has no parameters! Cannot initialise Surface calculation"
            );
            yield break;
        }

		Sphere.main.GetSphere(out vertices, out tris, gridResolution);
        numSpherePoints = vertices.Length;

        int size = geometry.size;
        //positions = new float3[size];
        //radii = new float[size];

        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Initialising Numerical Solvent-Accessible Surface Area Calculation on {0} atoms",
            size
        );

        CustomLogger.LogFormat(
            EL.DEBUG,
            "Atom ID  Radius     x      y      z   ",
            size
        );

    //    //TEMP
    //    surfacePoints = new List<(float3, float3, Color)>();
    //    //TEMP

        positionsRadii = new Dictionary<AtomID, (float3, float)>();
        int atomIndex = 0;
        foreach ((AtomID atomID, Atom atom) in geometry.EnumerateAtomIDPairs()) {

            AtomicParameter atomicParameter;
            float radius;
            if (geometry.parameters.TryGetAtomicParameter(atom.amber, out atomicParameter)) {
                radius = atomicParameter.radius;
            } else {
                radius = 0;
            }

            positionsRadii[atomID] = (atom.position, radius + solventRadius);

            CustomLogger.LogFormat(
                EL.DEBUG,
                "{0} {1,7:F4} {1,7:F4} {2,7:F4} {3,7:F4}",
                atomID, 
                radius,
                atom.position.x,
                atom.position.y,
                atom.position.z
            );

            atomIndex++;

        }

    }

    public float GetNumSASAScore(AtomID atomID0, Atom atom0, List<(ResidueID, Residue)> nearbyResidues) {

        float radius0 = positionsRadii[atomID0].radius;

        if (radius0 == 0f) {
            return 0;
        }

        float3 position0 = atom0.position;

        //Get factor that scales system to the unit sphere
        float scale = 1 / radius0;

        (ResidueID residueID0, PDBID pdbID0) = atomID0;

        // Cache AtomID
        AtomID atomID1 = AtomID.Empty;

        // For each nearby atom, if their sphere (radius + solvent r_VdW) intersects this atom's sphere, yield its offset to this atom and its scaled radius
        IEnumerable<(float3, float)> GetNearbyAtomPosRadiusSq(ResidueID residueID1, Residue residue1) {
            atomID1.residueID = residueID1;
            bool sameResidue = residueID1 == residueID0;
            foreach ((PDBID pdbID1, Atom atom1) in residue1.EnumerateAtoms()) {
                atomID1.pdbID = pdbID1;

                // Ignore same atom
                if (sameResidue && pdbID1 == pdbID0) {continue;}

                (float3 position1, float radius1) = positionsRadii[atomID1];

                float3 v01 = position1 - position0;

                // Discard if atom pair's combined radius + solvent diameter is less than the distance between them
                float cutoff = CustomMathematics.Squared(radius0 + radius1);
                if (math.lengthsq(v01) > cutoff) {
                    //Spheres are not touching
                    continue;
                }

                // Return vector from this atom to atom0 along with its scaled sphere radius
                yield return (
                    v01 * scale,
                    CustomMathematics.Squared(radius1 * scale)
                );
            }
        }

        // Get all spheres that intersect this atom as a list
        List<(float3, float)> scaledPositionsRadiiSq = nearbyResidues
            //.AsParallel() // Don't parallelise
            .SelectMany(
                ((ResidueID residueID1, Residue residue1) x) => 
                GetNearbyAtomPosRadiusSq(x.residueID1, x.residue1)
            )
            .ToList();

        // Number of sphere vertices that are within another atom's sphere
        int shieldedVerts = vertices
            // For each vertex...
            .AsParallel()
            // ...count... 
            .Count(
                // ...if the distance squared between any vertex and a nearby atom is less than the scaled sphere radius squared
                vert => scaledPositionsRadiiSq.Any(((float3 pos, float r2) x) => math.distancesq(vert, x.pos) < x.r2)
            );

// Visualise surface
////TEMP
//        // Number of sphere vertices that are within another atom's sphere
//        int shieldedVerts = vertices
//            // For each vertex...
//            // .AsParallel()
//            // ...count... 
//            .Count(
//                // ...if the distance squared between any vertex and a nearby atom is less than the scaled sphere radius squared
//                vert => {
//                    if (scaledPositionsRadiiSq.Any(((float3 pos, float r2) x) => math.distancesq(vert, x.pos) < x.r2)) {
//                        return true;
//                    } else {
//                        surfacePoints.Add((0.99f * radius0 * vert + position0, radius0 * vert + position0, Settings.GetAtomColourFromElement(pdbID0.element)));
//                        return false;
//                    }
//                    
//                }
//            );
////TEMP

        float maxSA = 4 * math.PI * radius0 * radius0;


        // Keep track of largest SASA for colour scaling
        if (maxSA > this.maxSASA) {
            this.maxSASA = maxSA;
        }

        // The SASA is the fraction of unshielded sphere verts times the maximum surface area
        return maxSA * ((float)(numSpherePoints - shieldedVerts) / numSpherePoints);
    }

//    //TEMP
//    public List<(float3, float3, Color)> surfacePoints;
//    //TEMP

    IEnumerator GetSASATypes(string path) {
        sasaTypes = new List<(uint[], SASAType)>();

        foreach (string line in FileIO.EnumerateLines(path, "#")) {
            string[] splitLine = line.Split(new[] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

            if (splitLine.Length < 9) {continue;}

            uint[] hashes = new uint[5];
            hashes[0] = SASAType.GetTypeHash(splitLine[0]);
            // {
            //    SASAType.GetTypeHash(splitLine[0]),
            //    SASAType.GetTypeHash(splitLine[1]),
            //    SASAType.GetTypeHash(splitLine[2]),
            //    SASAType.GetTypeHash(splitLine[3]),
            //    SASAType.GetTypeHash(splitLine[4])
            //};

            uint[] neighbourHashes = new uint[4] {
                SASAType.GetTypeHash(splitLine[1]),
                SASAType.GetTypeHash(splitLine[2]),
                SASAType.GetTypeHash(splitLine[3]),
                SASAType.GetTypeHash(splitLine[4])
            };

            // Add sorted neighbour hashes
            System.Array.Sort(neighbourHashes);
            for (int i=1, j=3; i<5; i++, j--) {
                hashes[i] = neighbourHashes[j];
            }

            CustomLogger.LogOutput(string.Join(" ", hashes.Select(x => x.ToString())));

            float cutoff;
            if (!float.TryParse(splitLine[5], out cutoff)) {
                CustomLogger.LogFormat(EL.ERROR,"Unable to parse Cutoff: {0}",splitLine[5]);
                continue;
            }

            float sigma;
            if (!float.TryParse(splitLine[6], out sigma)) {
                CustomLogger.LogFormat(EL.ERROR,"Unable to parse Sigma: {0}",splitLine[6]);
                continue;
            }

            float epsilon;
            if (!float.TryParse(splitLine[7], out epsilon)) {
                CustomLogger.LogFormat(EL.ERROR,"Unable to parse Epsilon: {0}",splitLine[7]);
                continue;
            }

            float maxSASA;
            if (!float.TryParse(splitLine[8], out maxSASA)) {
                CustomLogger.LogFormat(EL.ERROR,"Unable to parse Max SASA: {0}",splitLine[8]);
                continue;
            }
        
            if (maxSASA > this.maxSASA) {
                this.maxSASA = maxSASA;
            }

            SASAType sasaType = new SASAType(cutoff, sigma, epsilon, maxSASA);

            sasaTypes.Add((hashes, sasaType));

            if (Timer.yieldNow) {yield return null;}
        }
    }

    IEnumerator GetAtomTypes(Geometry geometry) {

        atomTypes = new Dictionary<AtomID, SASAType>();

        uint[] hashes = new uint[5];
        uint[] neighbourHashes = new uint[4];
        foreach ((AtomID atomID, Amber amber) in ambers) {

            atomTypes[atomID] = SASAType.Empty;

            Atom atom;
            if (!geometry.TryGetAtom(atomID, out atom)) {
                continue;
            }

            // This atom's hash:
            hashes[0] = SASAType.GetTypeHash(atomID, atom);

            int neighbourIndex = 0;
            foreach ((AtomID neighbourID, BT bondType) in atom.EnumerateConnections()) {

                if (neighbourIndex > 3) {
                    break;
                }

                Atom neighbour;
                if (geometry.TryGetAtom(neighbourID, out neighbour)) {
                    neighbourHashes[neighbourIndex] = SASAType.GetTypeHash(neighbourID, neighbour);
                } else {
                    neighbourHashes[neighbourIndex] = 0;
                }
                neighbourIndex++;
            }

            //Set remaining positions to 0 
            for (int j=neighbourIndex; j<4; j++) {
                neighbourHashes[j] = 0;
            }

            // Add sorted neighbour hashes
            System.Array.Sort(neighbourHashes);
            for (int i=1, j=3; i<5; i++, j--) {
                hashes[i] = neighbourHashes[j];
            }


            bool match = false;

            //Get a matching SASA Type if any
            foreach ((uint[] sasaHashes, SASAType sasaType) in sasaTypes) {
                match = true;
                for (int i=0; i<5; i++) {

                    uint sasaHash = sasaHashes[i];
                    uint atomHash = hashes[i];

                    if (sasaHash == 5 && atomHash >= 1 && atomHash <= 5) {
                        //General Carbon match
                        continue;
                    } else if (sasaHash == 8 && atomHash >= 6 && atomHash <= 8) {
                        //General Nitrogen match
                        continue;
                    } else if (sasaHash == atomHash) {
                        //Specific match
                        continue;
                    } else {
                        //No match
                        match = false;
                        break;
                    }
                }   

                if (match) {
                    CustomLogger.LogFormat(
                        EL.DEBUG,
                        "{0} {1} -- {2}",
                        atomID,
                        string.Join(" ", hashes.Select(x => x.ToString().PadRight(2))),
                        string.Join(" ", sasaHashes.Select(x => x.ToString().PadRight(2)))
                    );
                    atomTypes[atomID] = sasaType;
                    break;
                }
            }

            if (Timer.yieldNow) {yield return null;}

            if (!match) {
                CustomLogger.LogFormat(
                    EL.DEBUG,
                    "{0} {1} -- XXXXXXXXXXXXXX",
                    atomID,
                    string.Join(" ", hashes.Select(x => x.ToString().PadRight(2)))
                );
            }

        }
    }

    public float GetCutoffIJ(SASAType sasaType0, SASAType sasaType1) {
        return sasaType0.cutoff + sasaType1.cutoff;
    }

    public float GetSigmaIJ(SASAType sasaType0, SASAType sasaType1) {
        return sasaType0.sigma + sasaType1.sigma;
    }

    public float GetEpsilonIJ(SASAType sasaType0, SASAType sasaType1) {
        return math.sqrt(sasaType0.epsilon * sasaType1.epsilon);
    }

    public float GetPWSASAScore(AtomID atomID0, Atom atom0, List<(ResidueID, Residue)> nearbyResidues) {
        SASAType sasaType0 = atomTypes[atomID0]; 
        float3 position0 = atom0.position;

        float score = sasaType0.maxSASA;
        //CustomLogger.LogOutput(
        //    "{0}  Cut: {1}, E: {2}, S: {3}, Max: {4}, ScoreInit: {5}",
        //    atomID0,
        //    sasaType0.cutoff,
        //    sasaType0.epsilon,
        //    sasaType0.sigma,
        //    sasaType0.maxSASA,
        //    score
        //);

        foreach ((ResidueID residueID1, Residue residue1) in nearbyResidues) {
            foreach ((PDBID pdbID1, Atom atom1) in residue1.EnumerateAtoms()) {
                AtomID atomID1 = new AtomID(residueID1, pdbID1);

                // Ignore same atom
                if (atomID0 == atomID1) {continue;}

                SASAType sasaType1 = atomTypes[atomID1]; 
                float3 position1 = atom1.position;

                score -= GetSASAShieldScoreIJ(sasaType0, sasaType1, position0, position1);

            }
        }

        return score;
    }

    public float GetSASAShieldScoreIJ(
        SASAType sasaType0, 
        SASAType sasaType1, 
        float3 position0, 
        float3 position1
    ) {

        float rIJ = math.distance(position0, position1);

        float x_0 = GetCutoffIJ(sasaType0, sasaType1) - rIJ;

        if (x_0 <= 0) {
            return 0f;
        } else {
            float alpha = (1 + x_0 / GetSigmaIJ(sasaType0, sasaType1));

            float epsilonIJ = GetEpsilonIJ(sasaType0, sasaType1);

            return epsilonIJ + epsilonIJ * (
                (param_n / (param_m - param_n)) / (math.pow(alpha, param_m)) - 
                (param_m / (param_m - param_n)) / (math.pow(alpha, param_n))
            );
        }



    }
}

public class SASAType {
    public float cutoff;
    public float sigma;
    public float epsilon;
    public float maxSASA;

    public SASAType(
        float cutoff,
        float sigma,
        float epsilon,
        float maxSASA
    ) {

        this.cutoff = cutoff;
        this.sigma = sigma;
        this.epsilon = epsilon;
        this.maxSASA = maxSASA;
    }

    public static SASAType Empty => new SASAType(3.0f, 1.0f, 0.0f, 0.0f);

    public static uint GetTypeHash(string sasaStr) {
        switch (sasaStr) {
            case "C":  return 1;
            case "CT": return 2;
            case "CC": return 3;
            case "CN": return 4;
            case "C_": return 5;
            case "NA": return 6;
            case "NB": return 7;
            case "N_": return 8;
            case "O_": return 9;
            case "S_": return 10;
            case "H_": return 11;
            default  : return 0;
        }
    }

    public static uint GetTypeHash(AtomID atomID, Atom atom) {

        Amber amber = atom.amber;
        // Specific AMBER Matches
        switch (amber) {
            case Amber.C : return 1;
            case Amber.CT: return 2;
            case Amber.CC: return 3;
            case Amber.CN: return 4;
            case Amber.NA: return 6;
            case Amber.NB: return 7;
        }
        // General Element Matches
        switch (atomID.pdbID.element) {
            case Element.C: return 5;
            case Element.N: return 8;
            case Element.O: return 9;
            case Element.S: return 10;
            case Element.H: return 11;
            default: return 12; // 12 not in SASA types so any unrecognised neighbours are undefined
        }
    }
}