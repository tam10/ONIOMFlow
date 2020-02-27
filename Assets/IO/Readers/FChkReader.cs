using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Mathematics;
public class FChkReader : GeometryReader {
    
    /// <summary>Instantiate a Formatted Checkpoint file reader to parse a Geometry</summary>
    /// <param name="geometry">The Geometry to parse into.</param>
    public FChkReader(Geometry geometry) {

        activeParser = ParseNormal;

        this.geometry = geometry;

		normalParseDict.Clear();
        AddKey(ParseDictKey.ATOMIC_NUMBERS);
        AddKey(ParseDictKey.COORDS);
        AddKey(ParseDictKey.MM_CHARGES);
        AddKey(ParseDictKey.ATOM_LAYERS);
        AddKey(ParseDictKey.BASIS_FUNCTIONS);
        AddKey(ParseDictKey.ELECTRONS);
        
        atomicNumbers = new int[0];
        coordinates = new float[0];
        mmCharges = new float[0];
        atomLayers = new int[0];

        numBasisFunctions = 0;
        numElectrons = 0;
    }

    /// <summary>Instantiate a Formatted Checkpoint file reader to parse Basis Function information for a Cube Reader</summary>
    public FChkReader() {
        activeParser = ParseNormal;
        
		normalParseDict.Clear();
        
        AddKey(ParseDictKey.BASIS_FUNCTIONS);
        AddKey(ParseDictKey.ELECTRONS);

        numBasisFunctions = 0;
        numElectrons = 0;

    }

	public enum ParseDictKey {ATOMIC_NUMBERS, COORDS, MM_CHARGES, ATOM_LAYERS, BASIS_FUNCTIONS, ELECTRONS}
	delegate bool Condition();
	Dictionary<ParseDictKey, Condition> normalParseDict = new Dictionary<ParseDictKey, Condition>();

    int arrayPos;
    int arrayLength;
    int[] atomicNumbers;
    float[] coordinates;
    float[] mmCharges;
    int[] atomLayers;

    public int numBasisFunctions;
    public int numElectrons;
    bool setAtoms;

	public void RemoveKey(ParseDictKey parseDictKey) {
		normalParseDict.Remove(parseDictKey);
	}

    public override IEnumerator CleanUp() {


        if (!setAtoms) {
            yield break;
        }

        int numAtoms = atomicNumbers.Length;


        //Error checking
        if (numAtoms == 0) {
            ThrowError(
                "ParseAtomicNumbers",
                new System.Exception(string.Format(
                    "Geometry has no atoms!"
                ))
            );
            yield break;
        } else if (numAtoms * 3 != coordinates.Length) {
            ThrowError(
                "ParseCoordinates",
                new System.Exception(string.Format(
                    "Number of coordinates ({0}) inconsistent with number of Atoms ({1})!",
                    coordinates.Length,
                    numAtoms
                ))
            );
            yield break;
        }
        
        bool copyCharges = numAtoms == mmCharges.Length;

        int positionIndex = 0;
        
        for (atomIndex = 0; atomIndex < numAtoms; atomIndex++) {
            AtomID atomID;
            if (!geometry.atomMap.TryGetValue(atomIndex, out atomID)) {
                ThrowError(
                    "CleanUp",
                    new System.Exception(string.Format(
                        "Atom Map does not contain Atom Index {0}!",
                        atomIndex
                    ))
                );
                yield break;
            }

            Atom atom;
            if (!geometry.TryGetAtom(atomID, out atom)) {
                ThrowError(
                    "CleanUp",
                    new System.Exception(string.Format(
                        "Geometry does not contain Atom ID {0}!",
                        atomID
                    ))
                );
                yield break;
            }

            atom.position = new float3(
                coordinates[positionIndex++],
                coordinates[positionIndex++],
                coordinates[positionIndex++]
            );

            if (copyCharges) {
                atom.partialCharge = mmCharges[atomIndex];
            }

        }
    }

	public void AddKey(ParseDictKey parseDictKey) {
		switch (parseDictKey) {
			case (ParseDictKey.ATOMIC_NUMBERS):
                if (geometry.atomMap == null || geometry.atomMap.Count == 0) {
                    ThrowError(
                        "Initialise",
                        new System.Exception(string.Format(
                            "Geometry does not have an Atom Map - Try loading on top of a Gaussian input file"
                        ))
                    );
                } else {
				    normalParseDict[ParseDictKey.ATOMIC_NUMBERS] = ExpectAtomicNumbers;
                    setAtoms = true;
                }
				break;
			case (ParseDictKey.COORDS):
				normalParseDict[ParseDictKey.COORDS] = ExpectCoordinates;
				break;
			case (ParseDictKey.MM_CHARGES):
				normalParseDict[ParseDictKey.MM_CHARGES] = ExpectMMCharges;
				break;
			case (ParseDictKey.ATOM_LAYERS):
				normalParseDict[ParseDictKey.ATOM_LAYERS] = ExpectAtomLayers;
				break;
            case (ParseDictKey.BASIS_FUNCTIONS):
                normalParseDict[ParseDictKey.BASIS_FUNCTIONS] = ReadNumBasisFunctions;
                break;
            case (ParseDictKey.ELECTRONS):
                normalParseDict[ParseDictKey.ELECTRONS] = ReadNumElectrons;
                break;
		}
	}

	//////////////////
	// SUB-PARSERS  //
	//////////////////

	void ParseNormal() {
		foreach ((ParseDictKey key, Condition condition) in normalParseDict) {
			if (condition()) {
				break;
			}
		}
	}

    void ParseAtomicNumbers() {
        FillIntArray(atomicNumbers, ref arrayPos);
        if (arrayPos >= atomicNumbers.Length) {
            normalParseDict.Remove(ParseDictKey.ATOMIC_NUMBERS);
            if (normalParseDict.Count() == 0) {
                stopReading = true;
            }
            activeParser = ParseNormal;
        }
    }

    void ParseCoords() {
        FillFloatArray(coordinates, ref arrayPos);
        if (arrayPos >= coordinates.Length) {
            normalParseDict.Remove(ParseDictKey.COORDS);
            if (normalParseDict.Count() == 0) {
                stopReading = true;
            }
            activeParser = ParseNormal;
        }
    }

    void ParseMMCharges() {
        FillFloatArray(mmCharges, ref arrayPos);
        if (arrayPos >= mmCharges.Length) {
            normalParseDict.Remove(ParseDictKey.MM_CHARGES);
            if (normalParseDict.Count() == 0) {
                stopReading = true;
            }
            activeParser = ParseNormal;
        }
    }

    void ParseAtomLayers() {
        FillIntArray(atomLayers, ref arrayPos);
        if (arrayPos >= atomLayers.Length) {
            normalParseDict.Remove(ParseDictKey.ATOM_LAYERS);
            if (normalParseDict.Count() == 0) {
                stopReading = true;
            }
            activeParser = ParseNormal;
        }
    }
    

	////////////////////
	// EXPECT METHODS //
	////////////////////

    bool ExpectAtomicNumbers() {
        if (!line.StartsWith("Atomic numbers")) {return false;}

        arrayPos = 0;
        arrayLength = GetArrayLength();
        atomicNumbers = new int[arrayLength];

        activeParser = ParseAtomicNumbers;
        return true;
    }

    bool ExpectCoordinates() {
        if (!line.StartsWith("Current cartesian coordinates")) {return false;}


        arrayPos = 0;
        arrayLength = GetArrayLength();
        coordinates = new float[arrayLength];

        activeParser = ParseCoords;
        return true;
    }

    bool ExpectMMCharges() {
        if (!line.StartsWith("MM Charges")) {return false;}

        

        arrayPos = 0;
        arrayLength = GetArrayLength();
        mmCharges = new float[arrayLength];

        activeParser = ParseMMCharges;
        return true;
    }

    bool ExpectAtomLayers() {
        if (!line.StartsWith("Atom Layers")) {return false;}

        

        arrayPos = 0;
        arrayLength = GetArrayLength();
        atomLayers = new int[arrayLength];

        activeParser = ParseAtomLayers;
        return true;
    }

    /////////////////////////
    // SINGLE LINE READERS //
    /////////////////////////  

    bool ReadNumBasisFunctions() {
        if (!line.StartsWith("Number of basis functions")) {
            return false;
        }


        RemoveKey(ParseDictKey.BASIS_FUNCTIONS);
        if (int.TryParse(line.Split(new char[] {' '}).Last(), out numBasisFunctions)) {
            return true;
        }


        ThrowError(
            "ReadNumBasisFunctions",
            new System.Exception(string.Format(
                "Error reading number of basis functions in {0} - cannot generate Cube file!",
                path
            ))
        );
    
        return true;
    }

    bool ReadNumElectrons() {
        if (!line.StartsWith("Number of electrons")) {
            return false;
        }


        RemoveKey(ParseDictKey.ELECTRONS);
        if (int.TryParse(line.Split(new char[] {' '}).Last(), out numElectrons)) {
            return true;
        }


        ThrowError(
            "ReadNumBasisFunctions",
            new System.Exception(string.Format(
                "Error reading number of electrons in {0} - cannot generate Cube file!",
                path
            ))
        );
        
        return true;
    }

	///////////
	// TOOLS //
	///////////

    int GetArrayLength() {
        return int.Parse(line.Split(new char[] {' '}, System.StringSplitOptions.RemoveEmptyEntries).Last());
    }

    void FillIntArray(int[] array, ref int arrayPos) {
        string[] splitLine = line.Split(new char[] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i=0; i<splitLine.Length; i++, arrayPos++) {
            array[arrayPos] = int.Parse(splitLine[i]);
        }
    }

    void FillFloatArray(float[] array, ref int arrayPos) {
        string[] splitLine = line.Split(new char[] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i=0; i<splitLine.Length; i++, arrayPos++) {
            array[arrayPos] = float.Parse(splitLine[i]);
        }
    }
}