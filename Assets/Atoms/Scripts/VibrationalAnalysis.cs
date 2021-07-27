using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

public class GaussianResults {

    public int size;
    public int numModes;
    public float[] frequencies;
    public float[] reducedMasses;
    public float[] forceConstants;
    public float[] intensities;
    public float[] modelPercents;
    public float[] realPercents;

    //Where the normal mode begins in the file

    public float3[] normalMode;
    public int2[] modeLineStarts;
    public string[] symmetries;

    public string path;

    public GaussianResults(string path, int size) {
        this.size = size;
        this.path = path;
        numModes = 3 * size - 6;

        if (numModes <= 0) {
            throw new System.Exception("Cannot have 0 or fewer Normal Modes!");
        }

        frequencies = new float[numModes];
        reducedMasses = new float[numModes];
        forceConstants = new float[numModes];
        intensities = new float[numModes];
        modelPercents = new float[numModes];
        realPercents = new float[numModes];
        symmetries = new string[numModes];
        modeLineStarts = new int2[numModes];
        normalMode = new float3[size];
    }

    public IEnumerator GetVibrationalMode(int modeNum) {
        if (modeNum >= numModes) {
            throw new System.IndexOutOfRangeException($"modeNum ({modeNum}) must be less than numModes ({numModes})");
        }

        int2 modeLineStart = modeLineStarts[modeNum];
        return GaussianOutputReader.ParseNormalMode(normalMode, path, size, modeLineStart.x, modeLineStart.y);
    }

} 