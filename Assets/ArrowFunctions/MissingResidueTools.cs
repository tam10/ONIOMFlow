using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using EL = Constants.ErrorLevel;
using BT = Constants.BondType;
using Unity.Mathematics;

public class MissingResidueTools : MonoBehaviour {
    
    private static MissingResidueTools _main;
    public static MissingResidueTools main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<MissingResidueTools>();
            return _main;
        }
    }

    public LineRenderer lineRenderer;
    public List<List<float3>> beziers = new List<List<float3>>();

    public IEnumerable<(Residue, Residue, List<string>, List<ResidueID>)> EnumerateMissingSegments(Geometry geometry) {
        
        List<ResidueID> residueIDs = geometry.EnumerateResidueIDs().ToList();
        List<ResidueID> missingResidueIDs = geometry.missingResidues.Keys.ToList();
        
        List<string> segmentNames = new List<string>();
        List<ResidueID> segmentIDs = new List<ResidueID>();
        ResidueID startResidueID = geometry
            .missingResidues
            .First()
            .Key
            .GetPreviousID();
        for  (int missingResidueIndex=0; missingResidueIndex<missingResidueIDs.Count; missingResidueIndex++) {

            ResidueID missingResidueID = missingResidueIDs[missingResidueIndex];
            string missingResidueName = geometry.missingResidues[missingResidueID];

            CustomLogger.LogFormat(
                EL.VERBOSE,
                "Reading missing Residue: {0}({1})",
                missingResidueID,
                missingResidueName
            );

            //Check if Missing Residue is already accounted for
            if (residueIDs.Contains(missingResidueID)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    "Atoms object already has missing Residue: {0}",
                    missingResidueID
                );

                string existingResidueName = geometry.GetResidue(missingResidueID).residueName;
                if (existingResidueName != missingResidueName) {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Missing Residue {0}({1}) has a different name to existing Residue {2}({3})",
                        missingResidueID,
                        missingResidueName,
                        missingResidueID,
                        existingResidueName
                    );
                }

                //End the segment if it exists
                if (segmentNames.Count > 0) {
                    yield return (
                        residueIDs.Contains(startResidueID) ? geometry.GetResidue(startResidueID) : null,
                        residueIDs.Contains(missingResidueID) ? geometry.GetResidue(missingResidueID) : null,
                        segmentNames,
                        segmentIDs
                    );
                }

                segmentNames = new List<string>();
                segmentIDs = new List<ResidueID>();
                continue;
            }

            ResidueID previousID = missingResidueID.GetPreviousID();
            ResidueID nextID = missingResidueID.GetNextID();

            if (segmentNames.Count == 0) {
                //Start a new segment
                startResidueID = previousID;
            }
            
            //Continue building the segment
            segmentNames.Add(missingResidueName);
            segmentIDs.Add(missingResidueID);

            if (
                residueIDs.Contains(nextID) ||
                missingResidueIndex + 1 >= missingResidueIDs.Count ||
                missingResidueIDs[missingResidueIndex + 1] != nextID
            ) {
                //Terminate the segment
                Residue startResidue = residueIDs.Contains(startResidueID) 
                    ? geometry.GetResidue(startResidueID) 
                    : null;
                Residue endResidue = !residueIDs.Contains(nextID) 
                    ? null 
                    : geometry.GetResidue(nextID).isWater 
                        ? null 
                        : geometry.GetResidue(nextID);

                yield return (
                    startResidue,
                    endResidue,
                    segmentNames,
                    segmentIDs
                );
                segmentNames = new List<string>();
                segmentIDs = new List<ResidueID>();
                continue;
            }

        }
    }

    public float GetMissingResiduesDistance(List<string> missingResidueNames, Residue startResidue) {

        PDBID cPDBID = PDBID.C;
        PDBID caPBDID = PDBID.CA;
        PDBID nPDBID = PDBID.N;

        float distance = 0f;

        foreach (string missingResidueName in missingResidueNames) {
            Residue misingResidue = Residue.FromString(missingResidueName, startResidue.state);
            float3 v_c_n = CustomMathematics.GetVector(misingResidue.atoms[cPDBID], misingResidue.atoms[nPDBID]);
            float3 v_c_ca = CustomMathematics.GetVector(misingResidue.atoms[cPDBID], misingResidue.atoms[caPBDID]);
            distance += math.length(v_c_n + v_c_ca);
        }
        
        return distance;
    }

    /// <summary>Fill in a gap between two existing Residues with new Residues using a list of Residue IDs and a list of Residue Names.</summary>
    /// <param name="startResidue">The Residue to start building from.</param>
    /// <param name="endResidue">The Residue to end at.</param>
    /// <param name="missingResidueIDs">The list of Residue IDs to assign the new Residues.</param>
    /// <param name="missingResidueNames">The list of standard Residue Names to assign the new Residues.</param>
    public void JoinResidues(Residue startResidue, Residue endResidue, List<ResidueID> missingResidueIDs, List<string> missingResidueNames) {

        int divisions = missingResidueNames.Count;

        if (missingResidueIDs.Count != divisions) {
            throw new System.Exception(string.Format(
                "Length of missingResidueIDs ({0}) must match length of missingResidueNames ({1})",
                missingResidueIDs.Count,
                divisions
            ));
        }
        
        Residue terminalResidue = startResidue;
        Residue previousResidue = startResidue;

        //Special PDBIDs used to link Residues
        PDBID cPDBID = PDBID.C;
        PDBID caPDBID = PDBID.CA;
        PDBID nPDBID = PDBID.N;

        //Validation before starting computation
        if (!terminalResidue.pdbIDs.Contains(cPDBID)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot add Neighbour Residue to Host Residue '{0}'. Host Residue does not contain Atom '{1}'",
                terminalResidue.residueID,
                PDBID.C
            );
        }

        //Length to fit Bezier to
        float targetDistance = GetMissingResiduesDistance(missingResidueNames, startResidue);

        //Positions to fit Bezier to
        float3[] bezierHandles = new float3[4];

        //Positions of linker atoms
        float3 p_c0 = startResidue.atoms[cPDBID].position;
        float3 p_n0 = startResidue.atoms[nPDBID].position;
        float3 p_c1 = endResidue.atoms[cPDBID].position;
        float3 p_n1 = endResidue.atoms[nPDBID].position;

        //Use start and end linkers to form start and end of bezier
        bezierHandles[0] = p_c0;
        bezierHandles[3] = p_n1;

        //Get the vectors between these atoms
        float3 v_c0_n0 = p_c0 - p_n0;
        float3 v_n1_c1 = p_n1 - p_c1;
        float3 v_c0_n1 = p_n1 - p_c0;

        //Project out vectors between start and end residue to create a new vector that adds "curve" to the Bezier
        //This gives us a parameter to adjust so the length can be fitted
        float3 v_c0_norm = v_c0_n0 - v_c0_n1 * math.dot(v_c0_n1, v_c0_n0) / math.dot(v_c0_n1, v_c0_n1);
        float3 v_n1_norm = v_n1_c1 - v_c0_n1 * math.dot(v_c0_n1, v_n1_c1) / math.dot(v_c0_n1, v_c0_n1);
        
        float minCurveAmount = 0f;
        float maxCurveAmount = 10f;

        int iterations = 10;
        int steps = 5;

        CustomLogger.LogFormat(
            EL.INFO,
            "Computing Bezier between {0} and {1}. Distance between existing residues: {2}. Target distance: {3}.",
            p_c0,
            p_n1,
            math.distance(p_c0, p_n1),
            targetDistance
        );

        float curveAmount;

        //Solve for the amount of curve the bezier is given to fit the gap
        for (int iteration=0; iteration<iterations; iteration++) {
            float deltaCurve = (maxCurveAmount - minCurveAmount) / steps;

            List<float3>[] bezierList = new List<float3>[steps];
            float[] deltas = new float[steps];

            foreach (int curveStep in Enumerable.Range(0, steps)) {
                curveAmount = minCurveAmount + curveStep * deltaCurve;

                //New handles are a combination of the C-N vectors of start and end Residues and the perpendicular vectors scaled by curveAmount
                bezierHandles[1] = p_c0 + v_c0_n0 * curveAmount + v_c0_norm * curveAmount.Squared();
                bezierHandles[2] = p_n1 + v_n1_c1 * curveAmount + v_n1_norm * curveAmount.Squared();

                //Compute current bezier
                bezierList[curveStep] = CustomMathematics.GetBezier(bezierHandles, divisions).ToList();
                deltas[curveStep] = (
                    targetDistance - 
                    //Enumerate over all points
                    Enumerable.Range(0, divisions)
                        //Sum all the distances between them
                        .Select(x => math.distance(bezierList[curveStep][x], bezierList[curveStep][x+1]))
                        .Sum()
                ).Squared();

            }

            //Group pairs of solutions
            float[] deltaPairs = Enumerable.Range(0, steps - 1)
                .Select(x => deltas[x] + deltas[x + 1])
                .ToArray();

            //Get best pair
            int best = CustomMathematics.IndexOfMin(deltaPairs);

            //This pair is now the new min and max curve amount
            minCurveAmount = minCurveAmount + deltaCurve * best;
            maxCurveAmount = minCurveAmount + deltaCurve * (best + 1);
        }

        //Curve amount to use is the average of the best pair
        curveAmount = 0.5f * (minCurveAmount + maxCurveAmount);
        
        //Create final bezier from this curve amount
        bezierHandles[1] = p_c0 + v_c0_n0 * curveAmount + v_c0_norm * curveAmount.Squared();
        bezierHandles[2] = p_n1 + v_n1_c1 * curveAmount + v_n1_norm * curveAmount.Squared();
        List<float3> bezier = CustomMathematics.GetBezier(bezierHandles, divisions).ToList();
        for (
            int missingResidueIndex = 0; 
            missingResidueIndex < divisions; 
            missingResidueIndex++
        ) {
            ResidueID missingResidueID = missingResidueIDs[missingResidueIndex];
            string missingResidueName = missingResidueNames[missingResidueIndex];

            //Check Residue is valid
            if (!terminalResidue.pdbIDs.Contains(cPDBID)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Cannot add Neighbour Residue to Host Residue '{0}'. Host Residue does not contain Atom '{1}'",
                    terminalResidue.residueID,
                    cPDBID
                );
                return;
            }
            terminalResidue = terminalResidue.AddNeighbourResidue(
                missingResidueName, 
                cPDBID,
                missingResidueID,
                terminalResidue.state
            );
            
            CustomMathematics.AngleRuler angleRuler = new CustomMathematics.AngleRuler(
                previousResidue.atoms[cPDBID], 
                terminalResidue.atoms[nPDBID], 
                terminalResidue.atoms[caPDBID]
            );
            float angle = angleRuler.angle;
            terminalResidue.Rotate(
                Quaternion.AngleAxis(
                    (2f * Mathf.PI / 3f - angle) * Mathf.Rad2Deg, 
                    angleRuler.norm
                ), 
                terminalResidue.atoms[nPDBID].position
            );

            //float[] p0 = terminalResidue.atoms[nPDBID].position;
            float3 p0 = bezier[missingResidueIndex];
            float3 p1 = bezier[missingResidueIndex + 1];

            float3 v01 = p1 - p0;

            terminalResidue.AlignBond(nPDBID, cPDBID, v01);

            //TEMP
            foreach (Atom atom in terminalResidue.atoms.Values) {
                atom.oniomLayer = Constants.OniomLayerID.MODEL;
            }
        }

        terminalResidue.atoms[cPDBID].externalConnections[new AtomID(endResidue.residueID, nPDBID)] = BT.SINGLE;
        endResidue.atoms[nPDBID].externalConnections[new AtomID(terminalResidue.residueID, cPDBID)] = BT.SINGLE;

        bezier = CustomMathematics.GetBezier(bezierHandles, 200).ToList();

        //bezier.Insert(0, bezierHandles[1]);
        bezier.Insert(0, bezierHandles[1]);
        bezier.Add(bezierHandles[2]);

        beziers.Add(bezier);


    }

    public void DrawBezier(Vector3[] points) {
        lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);
    }

    public void ClearBezier() {
        lineRenderer.positionCount = 0;
    }



}
