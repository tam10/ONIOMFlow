using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.IO;
using Unity.Mathematics;
using System.Text;
using BT = Constants.BondType;
using OLID = Constants.OniomLayerID;
using EL = Constants.ErrorLevel;
using ChainID = Constants.ChainID;

public class GaussianOutputReader : GeometryReader {

	public GaussianOutputReader(Geometry geometry, ChainID chainID=ChainID._) {
		
		this.geometry = geometry;
		this.chainID = chainID;
		
		layerDict = new Dictionary<OLID, Layer>();

		NormalParseDict.Clear();
		AddKey(ParseDictKey.KEYWORDS);

		energies = new List<float>();
		standardPositions = new List<float3[]>();
		allForces = new List<float3[]>();
		
		size = geometry.size;
		if (size != 0) {
		
			//Set arrays to fit size
			InitialiseAtomsInfo();

		}

		activeParser = ParseNormal;
	}

	public override IEnumerator CleanUp() {

		if (standardPositions.Count == 0) {
			CustomLogger.LogFormat(
				EL.WARNING,
				"No positional information found in '{0}'",
				path
			);
			yield break;
		}

		float3[] positions = standardPositions.Last();

		foreach ((AtomID atomID, int atomNum) in geometry.atomMap) {

			Atom atom;
			
			if (geometry.TryGetAtom(atomID, out atom)) {
				atom.position = positions[atomNum];
				atom.partialCharge = currentESPs[atomNum];
			}
			if (Timer.yieldNow) {
				yield return null;
			}
		}

	}

	Dictionary<OLID, Layer> layerDict;

	
	public enum ParseDictKey {KEYWORDS, ONIOM, STANDARD_ORIENTATION, ENERGY, FORCES, E_DIP_MOM, EXCITED_STATE, ESP, FREQ}
	delegate bool Condition();
	Dictionary<ParseDictKey, Condition> NormalParseDict = new Dictionary<ParseDictKey, Condition>();


	int size;

	List<string> keywordLines;
	List<string> keywords;

	bool atomInfoSet = false;
	bool isONIOM;
	int[] atomNumbers;
	ChainID chainID;

	public List<float> energies;
	float3[] currentPositions;
	public List<float3[]> standardPositions;

	float3[] currentForces;
	List<float3[]> allForces;

	int currentStateIndex;
	List<ExcitedState> currentExcitedStates;
	List<float[]> currentTransitionDipoleMoments;

	float[] currentESPs;

	bool readForce;
	bool readFreq;
	bool readTD;
	bool readESP;

	int currentMode;
	int modesPerLine;

	public void RemoveKey(ParseDictKey parseDictKey) {
		//Debug.LogFormat("Removing Keyword: {0}. Line: {1}", parseDictKey, lineNumber);
		NormalParseDict.Remove(parseDictKey);
	}

	public void AddKey(ParseDictKey parseDictKey) {
		//Debug.LogFormat("Adding Keyword: {0}. Line: {1}", parseDictKey, lineNumber);
		switch (parseDictKey) {
			case (ParseDictKey.KEYWORDS):
				NormalParseDict[ParseDictKey.KEYWORDS] = ExpectKeywords;
				break;
			case (ParseDictKey.ONIOM):
				NormalParseDict[ParseDictKey.ONIOM] = ExpectLayerInfo;
				break;
			case (ParseDictKey.STANDARD_ORIENTATION):
				NormalParseDict[ParseDictKey.STANDARD_ORIENTATION] = ExpectStandardOrientation;
				break;
			case (ParseDictKey.ENERGY):
				NormalParseDict[ParseDictKey.ENERGY] = ExpectEnergy;
				break;
			case (ParseDictKey.FORCES):
				NormalParseDict[ParseDictKey.FORCES] = ExpectForces;
				break;
			case (ParseDictKey.E_DIP_MOM):
				NormalParseDict[ParseDictKey.E_DIP_MOM] = ExpectElectricTDMs;
				break;
			case (ParseDictKey.EXCITED_STATE):
				NormalParseDict[ParseDictKey.EXCITED_STATE] = ExpectExcitedStates;
				break;
			case (ParseDictKey.ESP):
				NormalParseDict[ParseDictKey.ESP] = ExpectESPs;
				break;
			case (ParseDictKey.FREQ):
				NormalParseDict[ParseDictKey.FREQ] = ExpectFrequencies;
				break;
		}
	}



	//////////////////
	// SUB-PARSERS  //
	//////////////////

	void ParseNormal() {
		foreach ((ParseDictKey key, Condition condition) in NormalParseDict) {
			if (condition()) {
				break;
			}
		}
	}

	void ParseKeywords() {
		if (line.StartsWith(" --")) {
			keywords = keywordLines.SelectMany(x => x.Split(new [] {' '})).ToList();
			RemoveKey(ParseDictKey.KEYWORDS);

			isONIOM = false;
			readFreq = false;
			readForce = false;
			readTD = false;
			readESP = false;

			foreach (string keyword in keywords) {
				if (keyword.StartsWith("ONIOM", StringComparison.OrdinalIgnoreCase)) {
					isONIOM = true;
					if (keyword.Contains("TD", StringComparison.OrdinalIgnoreCase)) {
						readTD = true;
					}
					string[] splitKeyword = keyword.Split(new char[] {'='}, System.StringSplitOptions.RemoveEmptyEntries);

					if (
						splitKeyword.Length > 1 && 
						splitKeyword.Last().StartsWith("EMBED", StringComparison.OrdinalIgnoreCase)
					) {
						readESP = true;
					}
				}
				if (keyword.StartsWith("FREQ", StringComparison.OrdinalIgnoreCase)) {
					readFreq = true;
					readForce = true;
				}
				if (keyword.StartsWith("OPT", StringComparison.OrdinalIgnoreCase) || keyword.StartsWith("FORCE", StringComparison.OrdinalIgnoreCase)) {
					readForce = true;
				}
				if (keyword.StartsWith("TD", StringComparison.OrdinalIgnoreCase)) {
					readTD = true;
				}
				if (
					keyword.Contains("POP", StringComparison.OrdinalIgnoreCase) &&
					keyword.Contains("MK", StringComparison.OrdinalIgnoreCase)
				) {
					readESP = true;
				}
			}

			CustomLogger.LogFormat(
				EL.VERBOSE,
				"Reading {4} File (readFreq: {0}, readTD: {1}, readESP: {2}, readForce: {3})",
				readFreq,
				readTD,
				readESP,
				readForce,
				isONIOM ? "ONIOM" : "Single Layer"
			);


			AddKey(ParseDictKey.ONIOM);
			activeParser = ParseNormal;
		} else {
			keywordLines.Add(line);
		}
	}

	///<summary>Defines ONIOM layers</summary>
	void ParseONIOMLayers() {
		if (line.StartsWith(" Charge = ") ) {
			AddONIOMLayerFromLine();
		} else if (line.StartsWith(" Redundant internal") || line.StartsWith(" Symbolic Z-Matrix:")) {
			;
		} else {
			RemoveKey(ParseDictKey.ONIOM);
			ParseAtomInfo();
			activeParser = ParseAtomInfo;
		} 
	}

	void ParseAtomInfo() {
		if (string.IsNullOrWhiteSpace(line)) {
			size = atomIndex;
			atomInfoSet = false;
			InitialiseAtomsInfo();

			if (geometry.atomMap == null) {
				throw new System.Exception(
					"Atoms do not have an Atom Map! Try loading on top of the input file that generated this log file."
				);
			}

			if (geometry.atomMap.Count != size) {
				throw new System.Exception(string.Format(
					"Atom Map ({0}) not the same size as Atoms ({1}).",
					geometry.atomMap.Count,
					size
				));
			}

			AddKey(ParseDictKey.STANDARD_ORIENTATION);
			activeParser = ParseNormal;
		} else {
			//Read the atom
			AtomID atomID;
			try {
				atomID = GaussianPDBLineReader.ParseLine(geometry, line, chainID);
			} catch {
				charNum = GaussianPDBLineReader.charNum;
				throw;
			}
			
			if (GaussianPDBLineReader.failed) {

				if (!atomMapSet) {
					throw new System.Exception(
						"Atoms do not have an Atom Map! Try loading on top of the input file that generated this log file."
					);
				}

				CustomLogger.LogFormat(
					EL.WARNING,
					"Using old style Gaussian output (problematic if geometry do not map properly) - don't use 'geom=allcheck' to avoid this."
				);

				activeParser = ParseNormal;
				return;
			} else {
				if (!geometry.HasAtom(atomID)) {
					throw new System.Exception(string.Format(
						"Geometries do not align by Atom ID! Atoms '{0}' does not contain '{1}'.",
						geometry.name,
						atomID
					));
				}
			}

			//Build new map
			if (!atomMapSet) {
				geometry.atomMap[atomIndex] = atomID;
			}
			atomIndex++;
		}
	}

	void ParseStandardOrientation() {

		if (line.StartsWith(" --")) {
			
			standardPositions.Add((float3[])currentPositions.Clone());

			
			//RemoveKey(ParseDictKey.STANDARD_ORIENTATION);
			if (readTD) {
				AddKey(ParseDictKey.E_DIP_MOM);
				AddKey(ParseDictKey.EXCITED_STATE);
			} else if (readESP) {
				AddKey(ParseDictKey.ESP);
			} else {
				AddKey(ParseDictKey.ENERGY);
			}
			activeParser = ParseNormal;
			atomInfoSet = true;
			
			return;
		}
		
		string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

		if (!atomInfoSet) {
			int atomicNumber;
			if (!int.TryParse(splitLine[1], out atomicNumber)) {
				charNum = GetSplitStringIndex(line, 1);
				throw new System.Exception(string.Format(
					"Unrecognised Atomic Number: {0}",
					splitLine[1]
				));
			}
			atomNumbers[atomIndex] = atomicNumber;

			if (geometry.atomMap[atomIndex].pdbID.atomicNumber != atomicNumber) {
				throw new System.Exception(string.Format(
					"Atoms do not align! Atomic number of index {0} ({1}) does not equal Atomic number of AtomID {2} ({3}) from Atom Map",
					atomIndex,
					atomicNumber,
					geometry.atomMap[atomIndex],
					geometry.atomMap[atomIndex].pdbID.atomicNumber
				));
			}
			
		}

		for (int j=0; j<3; j++) {
			string coordStr = splitLine[3+j];
			float coord;
			if (!float.TryParse(coordStr, out coord)) {
				charNum = GetSplitStringIndex(line, 3+j);
				throw new System.Exception(string.Format(
					"Unrecognised Coordinate: {0}",
					splitLine[3+j]
				));
			}
			currentPositions[atomIndex][j] = coord;
		}

		atomIndex++;
	}

	void ParseForces() {

		if (line.StartsWith(" --")) {
			allForces.Add((float3[])currentForces.Clone());
			RemoveKey(ParseDictKey.FORCES);
			AddKey(ParseDictKey.STANDARD_ORIENTATION);
			activeParser = ParseNormal;
			
			return;
		}
		
		string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

		for (int j=0; j<3; j++) {
			string coordStr = splitLine[2+j];
			float coord;
			if (!float.TryParse(coordStr, out coord)) {
				charNum = GetSplitStringIndex(line, 2+j);
				throw new System.Exception(string.Format(
					"Unrecognised Force on line: {0}",
					splitLine[2+j]
				));
			}

			currentForces[atomIndex][j] = coord;
		}
		atomIndex++;
	}

	void ParseElectricDipoleMoments() {
		string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);
		int stateIndex;
		if (!int.TryParse(splitLine[0], out stateIndex)) {
			RemoveKey(ParseDictKey.STANDARD_ORIENTATION);
			AddKey(ParseDictKey.EXCITED_STATE);
			activeParser = ParseNormal;
			return;
		}
		
		float[] dipoleMoment = new float[3];
		for (int j=0; j<3; j++) {
			string coordStr = splitLine[1+j];
			float coord;
			if (!float.TryParse(coordStr, out coord)) {
				charNum = GetSplitStringIndex(line, 1+j);
				throw new System.Exception(string.Format(
					"Unrecognised Electric Dipole Moment: {0}",
					splitLine[1+j]
				));
			}
			dipoleMoment[j] = coord;
		}
		currentTransitionDipoleMoments.Add((float[])dipoleMoment.Clone());
	}

	void ParseExcitedState() {
		string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

		if (splitLine.Length == 0) {
			RemoveKey(ParseDictKey.EXCITED_STATE);
			if (readESP) {
				AddKey(ParseDictKey.ESP);
			} else {
				AddKey(ParseDictKey.ENERGY);
			}
			activeParser = ParseNormal;
			return;
		}

		int fromOrbital;
		if (!int.TryParse(splitLine[0], out fromOrbital)) {
			activeParser = ParseNormal;
			return;
		}

		int toOrbital;
		if (!int.TryParse(splitLine[2], out toOrbital)) {
			charNum = GetSplitStringIndex(line, 2);
			throw new System.Exception(string.Format(
				"Unrecognised Excited State Orbital: {0}",
				splitLine[2]
			));
		}

		float coefficient;
		if (!float.TryParse(splitLine[3], out coefficient)) {
			charNum = GetSplitStringIndex(line, 3);
			throw new System.Exception(string.Format(
				"Unrecognised Excited State Coefficient: {0}",
				splitLine[3]
			));
		}

		ExcitedStateComposition composition = new ExcitedStateComposition();
		composition.fromOrbital = fromOrbital;
		composition.toOrbital = toOrbital;
		composition.coefficient = coefficient;

		ExcitedState currentExcitedState = currentExcitedStates[currentStateIndex];
		currentExcitedState.composition.Add(composition);
	}

	void ParseESPs() {
		float esp;
		string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);
		if (splitLine.Length != 3) {
			if (atomIndex != size) {
				throw new System.Exception(string.Format(
					"Failed to read ESPs: invalid number of ESPs ({0}) - should be {1}",
					atomIndex,
					size
				));
			}
			RemoveKey(ParseDictKey.ESP);
			AddKey(ParseDictKey.STANDARD_ORIENTATION);
			activeParser = ParseNormal;
			return;
		}
		string espString = splitLine[2];
		if (!float.TryParse(espString, out esp)) {
			throw new System.Exception(string.Format(
				"Failed to read ESPs: couldn't cast {0} to a float",
				espString
			));
		}
		currentESPs[atomIndex++] = esp;
	}

	void ParseFrequencyInfo() {
		string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);
		
		if (splitLine.Length == 0) {
			RemoveKey(ParseDictKey.FREQ);
			AddKey(ParseDictKey.FORCES);
			activeParser = ParseNormal;
			return;
		}
		
		switch (splitLine[0]) {
			case "Frequencies":
				if (splitLine[1] == "--") {
					//Regular Frequency output
				} else if (splitLine[1] == "---") {
					//High precision Frequency output
				}
				modesPerLine = splitLine.Length - 2;
				
				AddFrequencyInfo(geometry.gaussianResults.frequencies, splitLine, 2);
				break;
			case "Red.":
				AddFrequencyInfo(geometry.gaussianResults.reducedMasses, splitLine, 3);
				break;
			case "Frc":
				AddFrequencyInfo(geometry.gaussianResults.forceConstants, splitLine, 3);
				break;
			case "IR":
				AddFrequencyInfo(geometry.gaussianResults.intensities, splitLine, 3);
				break;
			case "%ModelSys":
				AddFrequencyInfo(geometry.gaussianResults.modelPercents, splitLine, 2);
				break;
			case "%RealSys":
				AddFrequencyInfo(geometry.gaussianResults.realPercents, splitLine, 2);
				break;
			case "Atom":
				skipLines = size;
				for (int modeIndex=0; modeIndex<3; modeIndex++) {
					geometry.gaussianResults.modeLineStarts[modeIndex+currentMode] = new int2(lineNumber+1, modeIndex);
				}
				currentMode += modesPerLine;
				break;
			default:

				break;
		}
	}

	public static IEnumerator ParseNormalMode(float3[] normalMode, string path, int size, int modeStartLineNumber, int modeIndex) {

		int atomIndex = 0;
		int startIndex = 2 + 3 * modeIndex;

		// First try sed to grab file section

		string command = $"sed -n \"{modeStartLineNumber+1},{modeStartLineNumber+size}p;{modeStartLineNumber+size+1}q\" {path}";

		Bash.ProcessResult result = new Bash.ProcessResult();

		yield return Bash.ExecuteShellCommand(command, result, logOutput:false);

		IEnumerable<string> enumerable;

		if (result.ExitCode == 0) {
			string output = result.Output;
			enumerable = output.Split(new string[] {FileIO.newLine}, StringSplitOptions.RemoveEmptyEntries);
		} else {
			enumerable = FileIO.EnumerateLines(path).Skip(modeStartLineNumber).Take(size);
		}

		foreach (string line in enumerable) {
			string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);
			if (splitLine.Length < startIndex + 3) {
				CustomLogger.LogFormat(EL.ERROR, $"Failed to parse Normal Mode (Line {modeStartLineNumber+atomIndex}): \"{line}\" (Line too short)");
				yield break;
			}
			for (int coord=0,index=startIndex; coord<3; coord++,index++) {
				float x;
				if (!float.TryParse(splitLine[index], out x)) {
					CustomLogger.LogFormat(EL.ERROR, $"Failed to parse Normal Mode (Line {modeStartLineNumber+atomIndex}): \"{line}\" (Error parsing float)");
					yield break;
				}
				normalMode[atomIndex][coord] = x;
			}
			atomIndex++;
			if (Timer.yieldNow) {yield return null;}
		}

	}

	bool ExpectEnergy() {

		if (isONIOM) {
			if (!line.StartsWith(" ONIOM: extrapolated")) {
				return false;
			}
		} else {
			if (!line.StartsWith(" SCF Done:")) {
				return false;
			}
		}
		float energy;
		
		string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

		if (splitLine.Length != (isONIOM ? 5 : 9)) {
			throw new System.Exception(string.Format(
				"Failed to read Energy: invalid line specification"
			));
		}

		string energyString = splitLine[4];
		if (!float.TryParse(energyString, out energy)) {
			throw new System.Exception(string.Format(
				"Failed to read Energy: couldn't cast {0} to a float",
				energyString
			));
		}

		energies.Add(energy);

		RemoveKey(ParseDictKey.ENERGY);
		if (readFreq) {
			AddKey(ParseDictKey.FORCES);
			AddKey(ParseDictKey.FREQ);
		} else if (readForce) {
			AddKey(ParseDictKey.FORCES);
		} else {
			AddKey(ParseDictKey.STANDARD_ORIENTATION);
		}

		return true;
	}

	////////////////////
	// EXPECT METHODS //
	////////////////////


	bool ExpectKeywords() {

		if (!line.StartsWith(" #")) {return false;}

		keywordLines = new List<string>();
		ParseKeywords();
		activeParser = ParseKeywords;

		return true;
	}

	bool ExpectLayerInfo() {

		if (!line.StartsWith(" Charge = ")) {return false;}

		atomIndex = 0;

		//Run this once on the current line
		ParseONIOMLayers();

		//Set the parser
		activeParser = ParseONIOMLayers;

		return true;
	}

	bool ExpectStandardOrientation() {
		
		if (!line.StartsWith("                         Standard orientation:")) {return false;}

		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Reading Standard Orientation block"
		);
		skipLines = 4;
		atomIndex = 0;
		activeParser = ParseStandardOrientation;

		return true;
	}

	bool ExpectForces() {

		if (
			!(line.StartsWith(" Center     Atomic") && line.Substring(42, 6) == "Forces")
		) {return false;}
		
		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Reading Forces block"
		);
		skipLines = 2;
		atomIndex = 0;
		activeParser = ParseForces;

		return true;
	}

	bool ExpectElectricTDMs() {

		if (!line.StartsWith(" Ground to excited state transition electric dipole moments (Au):")) {return false;}
		skipLines = 1;
		currentTransitionDipoleMoments = new List<float[]>();
		activeParser = ParseElectricDipoleMoments;

		return true;
	}

	bool ExpectExcitedStates() {

		if (!line.StartsWith(" Excited State ")) {return false;}
		string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);
		int stateIndex;
		string stateIndexStr = splitLine[2].Trim(':');
		if (!int.TryParse(stateIndexStr, out stateIndex)) {
			charNum = GetSplitStringIndex(line, 2);
			throw new System.Exception(string.Format(
				"Unrecognised Excited State on line: {0}",
				splitLine[2]
			));
		}

		if (stateIndex == 1) {
			//First Excited State - reset currentExcitedStates
			currentExcitedStates = new List<ExcitedState>();
		}

		string symmetry = splitLine[3];
		
		float excitationEnergy;
		if (!float.TryParse(splitLine[4], out excitationEnergy)) {
			charNum = GetSplitStringIndex(line, 4);
			throw new System.Exception(string.Format(
				"Unrecognised Excitation Energy on line: {0}",
				splitLine[4]
			));
		}
		
		float oscillatorStrength;
		try {
			oscillatorStrength = float.Parse(splitLine[8].Split('=')[1]);
		} catch {
			charNum = GetSplitStringIndex(line, 8);
			throw new System.Exception(string.Format(
				"Unrecognised Oscillator Strength: {0}",
				splitLine[8]
			));
		}
		
		float spinContamination;
		try {
			spinContamination = float.Parse(splitLine[9].Split('=')[1]);
		} catch {
			charNum = GetSplitStringIndex(line, 9);
			throw new System.Exception(string.Format(
				"Unrecognised Spin Contamination: {0}",
				splitLine[9]
			));
		}

		ExcitedState excitedState = new ExcitedState(stateIndex);
		excitedState.symmetry = symmetry;
		excitedState.excitationEnergy = excitationEnergy;
		excitedState.oscillatorStrength = oscillatorStrength;
		excitedState.spinContamination = spinContamination;

		if (currentTransitionDipoleMoments != null && currentExcitedStates.Count >= stateIndex) {
			excitedState.transitionDipoleMoment = currentTransitionDipoleMoments[stateIndex-1];
		}

		currentStateIndex = stateIndex-1;
		CustomLogger.LogFormat(
			EL.DEBUG,
			"Adding Excited State Index {0} (Symmetry: {1}, Excitation Energy: {2} eV, Oscillator Strength: {3}, Spin contamination: {4}, Transition Dipole Moment: {5}",
			stateIndex,
			symmetry,
			excitationEnergy,
			oscillatorStrength,
			spinContamination,
			CustomMathematics.ToString(excitedState.transitionDipoleMoment)
		);
		currentExcitedStates.Add(excitedState);

		activeParser = ParseExcitedState;

		return true;
	}

	bool ExpectESPs() {

		if (!line.StartsWith(" ESP charges:")) {return false;}

		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Reading ESPs"
		);
		skipLines = 1;
		atomIndex = 0;
		activeParser = ParseESPs;

		return true;
	}

	bool ExpectFrequencies() {
		
		if (!line.StartsWith(" Harmonic frequencies ")) {return false;}
		Debug.LogFormat("Freq");
		skipLines = 3;
		currentMode = 0;
		activeParser = ParseFrequencyInfo;

		return true;
	}

	///////////
	// TOOLS //
	///////////

	void AddFrequencyInfo(float[] array, string[] splitLine, int startIndex) {
		for (int i=0; i<modesPerLine; i++) {
			array[currentMode+i] = float.Parse(splitLine[startIndex++]);
		}
	}

	void InitialiseAtomsInfo() {

		if (!atomInfoSet) {
			CustomLogger.LogFormat(
				EL.VERBOSE,
				"Initialising Atoms Information with {0} geometry",
				size
			);
			atomNumbers = new int[size];
			currentPositions = new float3[size];
			currentForces = new float3[size];
			currentESPs = new float[size];
			atomInfoSet = true;

			geometry.gaussianResults = new GaussianResults(path, size);
		}
	}

	void AddONIOMLayerFromLine() {
		string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

		OLID layerID;
		if (splitLine.Length == 6) {
			//Not ONIOM
			layerID = OLID.REAL;
		} else if (splitLine.Length == 13) {

			//Get the ONIOM Layer ID
			switch (splitLine[11]) {
				case "real":
					layerID = OLID.REAL;
					break;
				case "mid":
					layerID = OLID.INTERMEDIATE;
					break;
				case "model":
					layerID = OLID.MODEL;
					break;
				default:
					charNum = GetSplitStringIndex(line, 1);
					throw new System.Exception(string.Format(
						"Unrecognised Layer ID: {0}",
						splitLine[11]
					));
			}

			if (layerDict.ContainsKey(layerID)) {
				//Layer already defined
				return;
			}

		} else {
			charNum = GetSplitStringIndex(line, 0);
			throw new System.Exception(string.Format(
				"Unrecognised Layer format"
			));
		}

		int layerCharge;
		if (! int.TryParse(splitLine[2], out layerCharge)) {
			charNum = GetSplitStringIndex(line, 2);
			throw new System.Exception(string.Format(
				"Couldn't parse charge: {0}",
				splitLine[2]
			));
		}

		int layerMultiplicity;
		if (! int.TryParse(splitLine[5], out layerMultiplicity)) {
			charNum = GetSplitStringIndex(line, 5);
			throw new System.Exception(string.Format(
				"Couldn't parse multiplicity: {0}",
				splitLine[5]
			));
		}

		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Adding Layer: {0} (Charge: {1}, Multiplicity: {2})",
			layerID,
			layerCharge,
			layerMultiplicity
		);
		//Add the layer
		layerDict[layerID] = new Layer(oniomLayer: layerID, charge: layerCharge, multiplicity: layerMultiplicity);

	}

	static int GetSplitStringIndex(string str, int nthString) {
		int charNum = -1;
		char chr;
		char prevChar = '\0';

		for (int i=0; i<str.Length; i++) {
			chr = str[i];
			if (chr != ' ' && chr != prevChar &&++charNum == nthString) {
				return i;
			}
			prevChar = chr;
 		}
		return -1;
	}

}

