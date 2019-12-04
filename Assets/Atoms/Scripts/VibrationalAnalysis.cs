using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class VibrationalAnalysis {

    public int size;
    public int numModes;
    public float[] frequencies;
    public float[] reducedMasses;
    public float[] forceConstants;
    public float[] intensities;
    public float[,,] normalModes;
    public string[] symmetries;

    public VibrationalAnalysis(int size) {
        this.size = size;
        numModes = 3 * size - 6;

        if (numModes <= 0) {
            throw new System.Exception("Cannot have 0 or fewer Normal Modes!");
        }

        frequencies = new float[numModes];
        reducedMasses = new float[numModes];
        forceConstants = new float[numModes];
        intensities = new float[numModes];
        symmetries = new string[numModes];
        normalModes = new float[numModes, size, 3];
    }

}