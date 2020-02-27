using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using EL = Constants.ErrorLevel;
using Amber = Constants.Amber;

public class CubeReader : GeometryReader {
    
    public float3 gridOffset;
    public float3 gridScale;
    public int3 dimensions;
    public int gridLength;
    int gridIndex;
    public float[] grid;
    public int numAtoms;

    public CubeReader(Geometry geometry) {
        this.geometry = geometry;
		atomIndex = 0;
        skipLines = 2;
        activeParser = ParseOffset;
    }

    void ParseOffset() {
        string[] offset_spec = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        numAtoms = Mathf.Abs(int.Parse(offset_spec[0]));
        gridOffset[0] = float.Parse(offset_spec[1]);
        gridOffset[1] = float.Parse(offset_spec[2]);
        gridOffset[2] = float.Parse(offset_spec[3]);
        activeParser = ParseDimZ;
    }

    void ParseDimZ() {
        string[] z_spec = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        dimensions[2] = int.Parse(z_spec[0]);
        gridScale [2] = float.Parse (z_spec [1]);
        activeParser = ParseDimY;
    }

    void ParseDimY() {
        string[] y_spec = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        dimensions[1] = int.Parse(y_spec[0]);
        gridScale [1] = float.Parse (y_spec [2]);
        activeParser = ParseDimX;

    }

    void ParseDimX() {
        string[] x_spec = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        dimensions[0] = int.Parse(x_spec[0]);
        gridScale [0] = float.Parse (x_spec [3]);

        atomIndex = 0;
        activeParser = ParseAtoms;
    }

    void ParseAtoms() {

        AtomID atomID = geometry.atomMap[atomIndex];


        string[] splitLine = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        
        int atomicNumber = int.Parse(splitLine[0]);
        if (atomicNumber != atomID.pdbID.atomicNumber) {
            throw new System.Exception(string.Format(
                "Atomic Number {0} doesn't align with Element {1} in Atom ID {2} from Atom Map.",
                atomicNumber,
                atomID.pdbID.element,
                atomID
            ));
        }

        //Position
        float3 position = new float3 (
            float.Parse(splitLine[2]),
            float.Parse(splitLine[3]),
            float.Parse(splitLine[4])
        );
        
        Atom atom = new Atom(position, atomID.residueID, Amber.X);
        geometry.GetResidue(atomID.residueID).AddAtom(atomID.pdbID, atom, false);
        CustomLogger.LogFormat(EL.VERBOSE, "Adding Atom. ID: {0}. Position: {1}", atomID.pdbID, position);
        
        atomIndex++;
        if (atomIndex == numAtoms) {
            skipLines = 1;
            gridLength = dimensions[0] * dimensions[1] * dimensions[2];
            grid = new float[gridLength];
            gridIndex = 0;
            activeParser = ParseGrid;
        }
        
    }

    void ParseGrid() {

        string[] vs = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        if (vs [0].Contains (".")) {
            for (int i = 0; i < vs.Length; i++) {
                float value = float.Parse (vs [i]);
                grid [gridIndex++] = value;
                //grid[gridIndex++] = CustomMathematics.Map(i, 0, vs.Length, -1, 1);
            }
        }

    }

    public override IEnumerator CleanUp() {
        

        Debug.LogFormat(
            "Grid: {0} Dimensions: {1} {2} {3} ({4})",
            gridIndex,
            dimensions[0],
            dimensions[1],
            dimensions[2],
            dimensions[0] * dimensions[1] * dimensions[2]
        );

        yield return null;
    }
}
