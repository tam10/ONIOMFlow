﻿using System.Collections;
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

public static class GaussianOutputReader {


	static string path;

	static Dictionary<OLID, Layer> layerDict;

	
	public enum ParseDictKey {KEYWORDS, ONIOM, STANDARD_ORIENTATION, ENERGY, FORCES, E_DIP_MOM, EXCITED_STATE, ESP, FREQ}
	delegate bool Condition();
	static Dictionary<ParseDictKey, Condition> NormalParseDict = new Dictionary<ParseDictKey, Condition>();


	static string line;
	static string previousLine;
	delegate void LineParser();

	static LineParser activeParser;

	static int lineNumber = 0;
	static int linesToSkip = 0;
	static int charNum = 0;

	static bool failed;

	static int size;
	static int atomIndex;
	static Map<AtomID, int> atomMap;

	static List<string> keywordLines;
	static List<string> keywords;

	static bool atomInfoSet = false;
	static int[] atomNumbers;
	static string[] elements;
	static float[] masses;

	static List<float> energies;
	static float3[] currentPositions;
	static List<float3[]> standardPositions;

	static float3[] currentForces;
	static List<float3[]> allForces;

	static VibrationalAnalysis vibrationalAnalysis;

	static int currentStateIndex;
	static List<ExcitedState> currentExcitedStates;
	static List<float[]> currentTransitionDipoleMoments;
	static List<List<ExcitedState>> allExcitedStates;

	static float[] currentESPs;

	static Geometry geometry;
	static Geometry tempGeometry;

	static bool readForce;
	static bool readFreq;
	static bool readTD;
	static bool readESP;

	static bool updating;
	static bool oldMap;

	static int currentMode;
	static int modesPerLine;

	public static void Reset() {
		layerDict = new Dictionary<OLID, Layer>();

		NormalParseDict.Clear();
		AddKey(ParseDictKey.KEYWORDS);

		energies = new List<float>();
		standardPositions = new List<float3[]>();
		allForces = new List<float3[]>();
		atomMap = new Map<AtomID, int>();

		updating = false;
		oldMap = false;
		
		size = geometry.size;
		if (size != 0) {
			//Updating geometry
			updating = true;
		
			//Set arrays to fit size
			InitialiseAtomsInfo();

			if (geometry.atomMap != null && geometry.atomMap.Count == size) {
				oldMap = true;
			}
		}
		
		tempGeometry = PrefabManager.InstantiateGeometry(null);

	}

	public static void RemoveKey(ParseDictKey parseDictKey) {
		//Debug.LogFormat("Removing Keyword: {0}. Line: {1}", parseDictKey, lineNumber);
		NormalParseDict.Remove(parseDictKey);
	}

	public static void AddKey(ParseDictKey parseDictKey) {
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

	public static IEnumerator GeometryFromGaussianOutput(string filePath, Geometry geometry) {

		// Previous geometry
		// N: New geometry
		// U: Updating geometry, no old map
		// M: Updating geometry, old map

		// File geometry
		// O: Old style, no PDB info, no new map
		// P: New stle, PDB info, new map

		// Outcome table
		//   N U M
		// O 0 0 1
		// P 2 3 4

		// 0: Fails - close and don't change geometry
		// 1: Trust map but check atomic numbers align
		// 2: Read in as normal from scratch
		// 3: Create new map and check atomic numbers align backwards
		// 4: Create new map and check they are the same
		


		if (geometry == null) {
			CustomLogger.Log(
				EL.ERROR,
				"Cannot load into Atoms - Atoms is null!"
			);
			yield break;
		}
		GaussianOutputReader.geometry = geometry;

		Reset();

		path = filePath;
		tempGeometry.name = Path.GetFileName(path);

		//Parse the file
		activeParser = ParseNormal;
		lineNumber = 0;
		foreach (string logLine in FileIO.EnumerateLines(path)) {
			if (failed) {
				GameObject.Destroy(tempGeometry.gameObject);
				yield break;
			} 

			
			if (linesToSkip == 0) {
				line = logLine;
				try {
					//Read the line
					activeParser();
				} catch (System.Exception e) {
					//Pass error to user and close
					FileReader.ThrowFileReaderError(
						path,
						lineNumber,
						charNum,
						activeParser.Method.Name,
						line,
						e
					);
					failed = true;
					GameObject.Destroy(tempGeometry.gameObject);
					yield break;
				}
			} else if (linesToSkip > 0) {
				// Skip linesToSkip lines
				linesToSkip--;
			} else {
				throw new System.Exception("'linesToSkip' must not be negative in Gaussian Output Reader!");
			}

			if (Timer.yieldNow) {
				yield return null;
			}
			
			lineNumber++;
		}

		if (standardPositions.Count == 0) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"No positional data found in file!"
			);
			yield break;
		}
		float3[] positions = standardPositions.Last();

		if (!updating) {
			tempGeometry.CopyTo(geometry);
		}

		if (updating && geometry.size != atomMap.Count) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Size of Atoms ({0}) inconsistent with Atom Map ({1})!",
				geometry.size,
				atomMap.Count
			);
		}

		if (positions.GetLength(0) != atomMap.Count) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Size of Positions inconsistent with Atom Map!",
				positions.GetLength(0),
				atomMap.Count
			);
		}

		foreach ((AtomID atomID, int atomIndex) in atomMap) {
			Atom atom;
			if (!geometry.TryGetAtom(atomID, out atom)) {
				throw new System.Exception(string.Format(
					"Couldn't find Atom ID '{0}' in geometry",
					atomID
				));
			}
			geometry.GetAtom(atomID).position = positions[atomIndex];
		}

		GameObject.Destroy(tempGeometry.gameObject);

		yield return null;

	}




	//////////////////
	// SUB-PARSERS  //
	//////////////////

	static void ParseNormal() {
		foreach ((ParseDictKey key, Condition condition) in NormalParseDict) {
			if (condition()) {
				break;
			}
		}
	}

	static void ParseKeywords() {
		if (line.StartsWith(" --")) {
			keywords = keywordLines.SelectMany(x => x.Split(new [] {' '})).ToList();
			RemoveKey(ParseDictKey.KEYWORDS);

			bool isONIOM = false;
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

					if (splitKeyword.Length > 1 && splitKeyword.Last().StartsWith("EMBED", StringComparison.OrdinalIgnoreCase)) {
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
			}

			if (!isONIOM) {
				throw new System.Exception("File is not an ONIOM output");
			}

			CustomLogger.LogFormat(
				EL.VERBOSE,
				"Reading ONIOM File (readFreq: {0}, readTD: {1}, readESP: {2}, readForce: {3})",
				readFreq,
				readTD,
				readESP,
				readForce
			);

			AddKey(ParseDictKey.ONIOM);
			activeParser = ParseNormal;
		} else {
			keywordLines.Add(line);
		}
	}

	///<summary>Defines ONIOM layers</summary>
	static void ParseONIOMLayers() {
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

	static void ParseAtomInfo() {
		if (string.IsNullOrWhiteSpace(line)) {
			size = atomIndex;
			atomInfoSet = false;
			InitialiseAtomsInfo();

			if (atomMap == null) {
				throw new System.Exception(
					"Atoms do not have an Atom Map! Try loading on top of the input file that generated this log file."
				);
			}

			if (atomMap.Count != size) {
				throw new System.Exception(string.Format(
					"Atom Map ({0}) not the same size as Atoms ({1}).",
					atomMap.Count,
					size
				));
			}

			//Check atom maps are the same if there's an old map
			if (oldMap && (atomMap.Count != geometry.atomMap.Count && atomMap.Except(geometry.atomMap).Any())) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Old Atom Map is not the same as New Atom Map. It's possible that these geometries are not related or the order changed."
				);
			}

			AddKey(ParseDictKey.STANDARD_ORIENTATION);
			activeParser = ParseNormal;
		} else {
			//Read the atom
			AtomID atomID;
			try {
				atomID = GaussianPDBLineReader.ParseLine(tempGeometry, line);
			} catch {
				charNum = GaussianPDBLineReader.charNum;
				throw;
			}
			
			if (GaussianPDBLineReader.failed) {
				//Old style of geometry - can't validate on-the-fly
				if (!updating) {
					throw new System.Exception(
						"Invalid Atoms Section and geometry object is empty! Try loading on top of an existing geometry."
					);
				}

				if (!oldMap) {
					throw new System.Exception(
						"Atoms do not have an Atom Map! Try loading on top of the input file that generated this log file."
					);
				}

				CustomLogger.LogFormat(
					EL.WARNING,
					"Using old style Gaussian output (problematic if geometry do not map properly) - don't use 'geom=allcheck' to avoid this."
				);

				atomMap = geometry.atomMap;
				activeParser = ParseNormal;
				return;
			} else {
				if (updating && !geometry.ContainsAtom(atomID)) {
					throw new System.Exception(string.Format(
						"Geometries do not align by Atom ID! Atoms '{0}' does not contain '{1}'.",
						geometry.name,
						atomID
					));
				}
			}

			//Build new map
			atomMap[atomIndex++] = atomID;
		}
	}

	static void ParseStandardOrientation() {

		if (line.StartsWith(" --")) {
			standardPositions.Add((float3[])currentPositions.Clone());
			
			RemoveKey(ParseDictKey.STANDARD_ORIENTATION);
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

			if (atomMap[atomIndex].pdbID.atomicNumber != atomicNumber) {
				throw new System.Exception(string.Format(
					"Atoms do not align! Atomic number of index {0} ({1}) does not equal Atomic number of AtomID {2} ({3}) from Atom Map",
					atomIndex,
					atomicNumber,
					atomMap[atomIndex],
					atomMap[atomIndex].pdbID.atomicNumber
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

	static void ParseForces() {

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

	static void ParseElectricDipoleMoments() {
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

	static void ParseExcitedState() {
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

	static void ParseESPs() {
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

	static void ParseFrequencyInfo() {
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
				
				AddFrequencyInfo(vibrationalAnalysis.frequencies, splitLine, 2);
				break;
			case "Red.":
				AddFrequencyInfo(vibrationalAnalysis.reducedMasses, splitLine, 3);
				break;
			case "Frc":
				AddFrequencyInfo(vibrationalAnalysis.forceConstants, splitLine, 3);
				break;
			case "IR":
				AddFrequencyInfo(vibrationalAnalysis.intensities, splitLine, 3);
				break;
			case "Atom":
				atomIndex = 0;
				activeParser = ParseNormalModes;
				break;
			default:

				break;
		}
	}

	static void ParseNormalModes() {
		string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);
		int startIndex = 2;
		for (int modeNum=currentMode; modeNum<currentMode+modesPerLine; modeNum++) {
			for (int coord=0; coord<3; coord++) {
				vibrationalAnalysis.normalModes[modeNum, atomIndex, coord] = float.Parse(splitLine[startIndex++]);
			}
		}
		if (++atomIndex == size) {
			currentMode += modesPerLine;
			activeParser = ParseFrequencyInfo;
		}
	}

	static bool ExpectEnergy() {

		if (!line.StartsWith(" ONIOM: extrapolated")) {return false;}
		float energy;
		
		string[] splitLine = line.Split(new [] {' '}, System.StringSplitOptions.RemoveEmptyEntries);
		if (splitLine.Length != 5) {
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


	static bool ExpectKeywords() {

		if (!line.StartsWith(" #")) {return false;}

		keywordLines = new List<string>();
		ParseKeywords();
		activeParser = ParseKeywords;

		return true;
	}

	static bool ExpectLayerInfo() {

		if (!line.StartsWith(" Charge = ")) {return false;}

		atomIndex = 0;

		//Run this once on the current line
		ParseONIOMLayers();

		//Set the parser
		activeParser = ParseONIOMLayers;

		return true;
	}

	static bool ExpectStandardOrientation() {

		if (!line.StartsWith("                         Standard orientation:")) {return false;}

		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Reading Standard Orientation block"
		);
		linesToSkip = 4;
		atomIndex = 0;
		activeParser = ParseStandardOrientation;

		return true;
	}

	static bool ExpectForces() {

		if (!line.StartsWith(" Center     Atomic") && line.Substring(42, 6) == "Forces") {return false;}
		
		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Reading Forces block"
		);
		linesToSkip = 2;
		atomIndex = 0;
		activeParser = ParseForces;

		return true;
	}

	static bool ExpectElectricTDMs() {

		if (!line.StartsWith(" Ground to excited state transition electric dipole moments (Au):")) {return false;}
		linesToSkip = 1;
		currentTransitionDipoleMoments = new List<float[]>();
		activeParser = ParseElectricDipoleMoments;

		return true;
	}

	static bool ExpectExcitedStates() {

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

	static bool ExpectESPs() {

		if (!line.StartsWith(" ESP charges:")) {return false;}

		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Reading ESPs"
		);
		linesToSkip = 1;
		atomIndex = 0;
		activeParser = ParseESPs;

		return true;
	}

	static bool ExpectFrequencies() {
		
		if (!line.StartsWith(" Harmonic frequencies ")) {return false;}

		linesToSkip = 3;
		currentMode = 0;
		activeParser = ParseFrequencyInfo;

		return true;
	}

	///////////
	// TOOLS //
	///////////

	static void AddFrequencyInfo(float[] array, string[] splitLine, int startIndex) {
		ArraySegment<float> segment = new ArraySegment<float>(array, currentMode, modesPerLine);
		for (int i=2; i<2+modesPerLine; i++) {
			segment.Array[i] = float.Parse(splitLine[startIndex++]);
		}
	}

	static void InitialiseAtomsInfo() {

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

			vibrationalAnalysis = new VibrationalAnalysis(size);
		}
	}

	static void AddONIOMLayerFromLine() {
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

