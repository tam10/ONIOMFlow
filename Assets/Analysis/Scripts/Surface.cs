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

    public static int param_m = 10;
    public static int param_n = 4;

    public List<(uint[], SASAType)> sasaTypes;
    public Dictionary<AtomID, SASAType> atomTypes;
    public Dictionary<AtomID, Amber> ambers;

    public IEnumerator Initialise(Geometry geometry) {

        string sasaPath;
		if (!Settings.TryGetPath(Settings.dataPath, Settings.sasaFileName, out sasaPath)) {
			CustomLogger.LogFormat(
                EL.ERROR,
                "Could not find Projects Settings File: {0}", 
                Settings.sasaFileName
            );
		}


        yield return GetSASATypes(sasaPath);

        ambers = geometry.EnumerateAtomIDPairs()
            .ToDictionary(x => x.atomID, x => x.atom.amber);

        yield return GetAtomTypes(geometry);
    }

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

    public float GetSASAScore(AtomID atomID0, Atom atom0, List<(ResidueID, Residue)> nearbyResidues) {
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