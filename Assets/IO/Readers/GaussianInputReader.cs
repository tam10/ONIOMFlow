using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.IO;
using System.Text;
using Unity.Mathematics;
using BT = Constants.BondType;
using OLID = Constants.OniomLayerID;
using EL = Constants.ErrorLevel;
using Amber = Constants.Amber;

public class GaussianInputReader : GeometryReader {

	bool readConnectivity = false;

	List<string> keywordsList;
	List<string> titleLines;

	public GaussianInputReader(Geometry geometry) {

		commentString = "!";

		this.geometry = geometry;
		
		keywordsList = new List<string>();
		failed = false;

		geometry.gaussianCalculator.SetGeometry(geometry);

		activeParser = ParseLink0;
	}

	void ExpectLink0() {
		activeParser = ParseLink0;
	}

	void ExpectKeywords() {
		activeParser = ParseKeywords;
	}

	void ExpectTitle() {
		titleLines = new List<string>();
		activeParser = ParseTitle;
	}

	void ExpectChargeMultiplicity() {
		titleLines = new List<string>();
		activeParser = ParseChargeMultiplicity;
	}

	void ExpectAtoms() {
		atomIndex = 0;
		activeParser = ParseAtoms;
	}

	void ExpectConnectivity() {
		//skipLines = 1;
		activeParser = ParseConnectivity;
	}

	void ExpectParameters() {
		skipLines = 2;
		activeParser = ParseParameters;
	}

	void ParseLink0() {
		string line = this.line.Trim();
		if (line.StartsWith ("#")) {
			ExpectKeywords();
			ParseKeywords();
			return;

		} else if (line.StartsWith ("%CHK", StringComparison.OrdinalIgnoreCase)) {
			geometry.gaussianCalculator.checkpointPath = GetValueFromPair (line);
		} else if (line.StartsWith ("%OLDCHK", StringComparison.OrdinalIgnoreCase)) {
			geometry.gaussianCalculator.oldCheckpointPath = GetValueFromPair (line);
		} else if (line.StartsWith ("%MEM", StringComparison.OrdinalIgnoreCase)) {
			geometry.gaussianCalculator.jobMemoryMB = GetMemoryMB (GetValueFromPair (line));
		} else if (line.StartsWith ("%NPROC", StringComparison.OrdinalIgnoreCase)) {
			geometry.gaussianCalculator.numProcessors = int.Parse (GetValueFromPair (line));
		} else if (line.StartsWith ("%KJOB", StringComparison.OrdinalIgnoreCase)) {
			string [] stringArray = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
			geometry.gaussianCalculator.killJobLink = stringArray [1];

			if (stringArray.Length > 2) {
				geometry.gaussianCalculator.killJobAfter = int.Parse (stringArray [2]);
			} else {
				geometry.gaussianCalculator.killJobAfter = 1;
			}
		}
	}

	void ParseKeywords() {

		string line = this.line.Trim();
		if (line == "" && keywordsList.Count > 0) {
			ProcessKeywords();
			ExpectTitle();
			return;
		} 

		string[] stringArray = line.Split (new []{ ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
		foreach (string keywordItem in stringArray) {
			keywordsList.Add (keywordItem.ToLower ());
		}
		
	}

	void ParseTitle() {
		
		string line = this.line.Trim();
		if (line == "" && titleLines.Count > 0) {
			geometry.gaussianCalculator.title = string.Join (FileIO.newLine, titleLines.ToArray ());
			ExpectChargeMultiplicity();
			return;
		} 
		titleLines.Add(line);
	}

	void ParseChargeMultiplicity() {
		string line = this.line.Trim();
		if (line == "") {
			throw new System.Exception("Missing Charge/Multiplicity Section!");
		}

		ProcessChargeMultiplicity();
		ExpectAtoms();
		
	}

	void ParseAtoms() {

		string line = this.line.Trim();

		if (line == "") {
			if (readConnectivity) {
				ExpectConnectivity();
				return;
			} else {
				ExpectParameters();
				return;
			}
		}

		AtomID atomID;
		try {
			atomID = GaussianPDBLineReader.ParseLine(geometry, line);
			if (GaussianPDBLineReader.failed) {
				if (geometry.size == 0) {
					
					throw new System.Exception(
						"Atoms object is empty!"
					);
				}
			}
		} catch {
			charNum = GaussianPDBLineReader.charNum;
			throw;
		}
		
		//Build Atom map if reading connectivity
		if (readConnectivity && !atomMapSet) {
			geometry.atomMap[atomIndex] = atomID;
		}

		atomIndex++;

	}

	void ParseConnectivity() {

		string line = this.line.Trim();
		if (line == "") {
			ExpectParameters();
			return;
		}

		string[] splitConn = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);

		int connectionIndex0;
		if (! TryGetInt(splitConn[0], false, "GetConnectionIndex0", out connectionIndex0)) {
			failed = true;
			return;
		}
		connectionIndex0 -= 1;

		AtomID atomID0 = geometry.atomMap[connectionIndex0];

		int numConnections = (splitConn.Length - 1) / 2;

		for (int i = 0; i < numConnections; i++) {
			
			int connectionIndex1;
			if (! TryGetInt(splitConn[i*2+1], false, "GetConnectionIndex1", out connectionIndex1)) {
				failed = true;
				return;
			}
			connectionIndex1 -= 1;
			
			AtomID atomID1 = geometry.atomMap[connectionIndex1];

			float bondFloat;
			if (! TryGetFloat(splitConn [i * 2 + 2], false, "GetBondType", out bondFloat)) {
				failed = true;
				return;
			}

			BT bondType;
			try {
				bondType = Settings.GetBondIDFromGaussFloat( bondFloat );
			} catch (ErrorHandler.InvalidBondType) {
				throw new ErrorHandler.FileParserException(
					string.Format("Failed to parse Bond Float from {0} in Gaussian Input: {1}:{2}", bondFloat, path, lineNumber),
					path,
					lineNumber
				);
			}

			geometry.Connect(atomID0, atomID1, bondType);
		}
	}

	void ParseParameters() {
		string line = this.line.Trim();
		if (line == "") {
			activeParser = Pass;
		}

		PRMReader.UpdateParameterFromLine(line, geometry.parameters);
	}

	void ProcessKeywords() {
		//Parse keywords - only one line so don't need to worry about optimisation
		foreach (string keywordItem in keywordsList) {

			//Print level
			if (keywordItem.StartsWith ("#")) {
				string printLevelString = keywordItem.Replace ("#", "");
				if (!Constants.GaussianPrintLevelMap.TryGetValue(printLevelString.ToUpper(), out geometry.gaussianCalculator.gaussianPrintLevel)) {
					geometry.gaussianCalculator.gaussianPrintLevel = Constants.GaussianPrintLevel.NORMAL;
					CustomLogger.LogFormat(
						EL.DEBUG,
						"Setting Gaussian Print Level to {0}",
						geometry.gaussianCalculator.gaussianPrintLevel
					);
				}

				//Method
			} else if (keywordItem.StartsWith ("oniom")) {
				string oniomMethodsStr = GetStringInParentheses (keywordItem);
				string oniomOptionsStr = GetStringInParentheses (GetValueFromPair (keywordItem, checkEnclosed: true));

				string[] oniomOptions = oniomOptionsStr.Split (new []{ "," }, System.StringSplitOptions.RemoveEmptyEntries);

				foreach (string oniomOption in oniomOptions) {
					geometry.gaussianCalculator.oniomOptions.Add (oniomOption);
					CustomLogger.LogFormat(
						EL.DEBUG,
						"Adding ONIOM option: {0}",
						oniomOption
					);
				}

				string[] methods = oniomMethodsStr.Split (new []{ ":" }, System.StringSplitOptions.RemoveEmptyEntries);

				string[] highMBO = GetMethodFromString (methods [0]);
				geometry.gaussianCalculator.AddLayer (highMBO [0], highMBO [1], new List<string> (highMBO [2].Split(new[] {','})), OLID.MODEL);
				CustomLogger.LogFormat(
					EL.DEBUG,
					"Adding Layer {0}",
					OLID.MODEL
				);

				if (methods.Length == 2) {
					string[] lowMBO = GetMethodFromString (methods [1]);
					geometry.gaussianCalculator.AddLayer (lowMBO [0], lowMBO [1], new List<string> (lowMBO [2].Split(new[] {','})), OLID.REAL);
					CustomLogger.LogFormat(
						EL.DEBUG,
						"Adding Layer {0}",
						OLID.REAL
					);
				} else if (methods.Length == 3) {
					string[] mediumMBO = GetMethodFromString (methods [1]);
					geometry.gaussianCalculator.AddLayer (mediumMBO [0], mediumMBO [1], new List<string> (mediumMBO [2].Split(new[] {','})), OLID.INTERMEDIATE);
					CustomLogger.LogFormat(
						EL.DEBUG,
						"Adding Layer {0}",
						OLID.INTERMEDIATE
					);

					string[] lowMBO = GetMethodFromString (methods [2]);
					geometry.gaussianCalculator.AddLayer (lowMBO [0], lowMBO [1], new List<string> (lowMBO [2].Split(new[] {','})), OLID.REAL);
					CustomLogger.LogFormat(
						EL.DEBUG,
						"Adding Layer {0}",
						OLID.REAL
					);
				}
			} else if (keywordItem.StartsWith ("guess")) {
				string guessOptionsStr = GetStringInParentheses (GetValueFromPair (keywordItem, checkEnclosed: true));
				string[] guessOptions = guessOptionsStr.Split (new []{ "," }, System.StringSplitOptions.RemoveEmptyEntries);
				foreach (string guessOption in guessOptions) {
					geometry.gaussianCalculator.guessOptions.Add (guessOption);
					CustomLogger.LogFormat(
						EL.DEBUG,
						"Adding SCF Guess option: {0}",
						guessOption
					);
					
				}
			} else if (keywordItem.StartsWith ("geom")) {
				string geomOptionsStr = GetStringInParentheses (GetValueFromPair (keywordItem, checkEnclosed: true));
				string[] geomOptions = geomOptionsStr.Split (new []{ "," }, System.StringSplitOptions.RemoveEmptyEntries);
				foreach (string geomOption in geomOptions) {
					geometry.gaussianCalculator.geomOptions.Add (geomOption);
					if (geomOption == "connectivity") {
						readConnectivity = true;
						CustomLogger.LogFormat(
							EL.DEBUG,
							"Switching on connectivity reader"
						);
					}
				}
			} else {
				foreach (string methodName in new List<string>(Data.gaussianMethods)) {
					if (keywordItem.StartsWith (methodName)) {
						string[] highMBO = GetMethodFromString (keywordItem);
						geometry.gaussianCalculator.AddLayer (highMBO [0], highMBO [1], new List<string> (highMBO [2].Split(new[] {','})), OLID.MODEL);
						CustomLogger.LogFormat(
							EL.DEBUG,
							"Adding Layer {0}",
							OLID.MODEL
						);
						break;
					}
				}
			}
		}
	}

	void ProcessChargeMultiplicity() {
		GaussianCalculator gc = geometry.gaussianCalculator;

		string[] cmStr = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);

		List<OLID> oniomLayers = gc.layerDict.Keys.OrderBy(x => x).ToList();
		int numLayersSpecified = cmStr.Length / 2;

		for (int layerIndex = 0; layerIndex < numLayersSpecified; layerIndex++) {

			int charge = int.Parse (cmStr [2 * layerIndex]);
			int multiplicity = int.Parse (cmStr [2 * layerIndex + 1]);

			foreach (OLID oniomLayerID in oniomLayers) {
				Layer layer = gc.layerDict[oniomLayerID];
				layer.charge = charge;
				layer.multiplicity = multiplicity;
				CustomLogger.LogFormat(
					EL.DEBUG,
					"Setting ({0}) Layer Charge: {1}, Multiplicity: {2}",
					oniomLayerID,
					charge,
					multiplicity
				);
			}

			if (oniomLayers.Count != 0) {
				CustomLogger.LogFormat(
					EL.DEBUG,
					"Removing Layer: {0}",
					oniomLayers[0]
				);
				oniomLayers.RemoveAt(0);
			}
		}
	}

	static string GetValueFromPair (string inputStr, string delimiter = "=", bool checkEnclosed=false, char openChar='(', char closeChar=')') {

		//There's the possibility of the delimiter being inside parentheses
		//e.g. key(option1=value1)=value
		if (checkEnclosed) {

			for (int i = 0; i < inputStr.Length; i++) {
				if (inputStr [i].ToString () == delimiter) {
					if (!StringIndexEnclosed (inputStr, i, openChar, closeChar)) {
						return inputStr.Substring (i + 1, inputStr.Length - i - 1);
					}
				}
			}
			return "";
		} 

		string[] keyValue = inputStr.Split (new []{ delimiter }, System.StringSplitOptions.RemoveEmptyEntries);
		return keyValue [1];
	}

	static bool StringIndexEnclosed(string inputStr, int index, char openChar='(', char closeChar=')') {

		int depth = 0;
		char chr;

		for (int i = 0; i < inputStr.Length; i++) {
			chr = inputStr [i];

			if (index == i) {
				return (depth != 0);
			} else if (chr == openChar) {
				depth += 1;
			} else if (chr == closeChar) {
				depth -= 1;
			}
		}

		return false;
	}

	static int GetMemoryMB(string memoryString) {
		int oldMem = int.Parse(ToNumber (memoryString));
		string oldUnits = ToAlpha (memoryString).ToUpper();

		if (oldUnits == "KB") {
			return oldMem / 1024;
		} else if (oldUnits == "MB") {
			return oldMem;
		} else if (oldUnits == "GB") {
			return oldMem * 1024;
		} else if (oldUnits == "TB") {
			return oldMem * 1048576;
		} else {
			throw new System.Exception(string.Format(
				"Memory units '{0}' not recognised.", 
				oldUnits
			));
		}
	}

	static string GetStringInParentheses(string inputStr) {

		//Get the indices of the pair of parentheses
		int startIndex = -1;
		int endIndex = -1;

		//How many parenthesis pairs are we in?
		int depth = 0;

		for (int i = 0; i < inputStr.Length; i++) {
			switch (inputStr [i]) {
			case '(':
				if (depth == 0 && startIndex == -1)
					startIndex = i + 1;
				depth += 1;
				break;
			case ')':
				if (depth == 1 && endIndex == -1)
					endIndex = i - 1;
				depth -= 1;
				break;
			}
		}

		//No parentheses
		if (startIndex == -1)
			return inputStr;

		return inputStr.Substring (startIndex, endIndex - startIndex + 1);
	}

	static string[] GetMethodFromString(string inputStr) {
		string[] methodBasisSplit = inputStr.Split (new []{ "/" }, System.StringSplitOptions.RemoveEmptyEntries);

		string method = methodBasisSplit [0];
		string basis = "";
		string options = "";

		if (methodBasisSplit.Length == 1) {
			string[] methodOptionsSplit = method.Split (new []{ "=" }, System.StringSplitOptions.RemoveEmptyEntries);
			method = methodOptionsSplit [0];
			if (methodOptionsSplit.Length == 2)
				options = methodOptionsSplit [1];
		} else {
			string[] basisOptionsSplit = methodBasisSplit [1].Split (new []{ "/" }, System.StringSplitOptions.RemoveEmptyEntries);
			basis = basisOptionsSplit [0];
			if (basisOptionsSplit.Length == 2)
				options = basisOptionsSplit [1];
		}

		return new string[]{ method, basis, options };
	}

}

public static class GaussianPDBLineReader {

	delegate void CharParser();
	static CharParser charParser;
	static string line;
	public static int charNum;
	static char lineChar;


	enum PDBOption {PDBNAME,RESNAME,RESNUM}
	static PDBOption pdbOption;

	static string elementStr;
	static string pdbName;
	static string amberName;
	static ResidueID residueID;
	static string residueName;
	static float3 position = new float3();
	static float partialCharge;
	static bool mobile;
	static OLID oniomLayerID;

	public static bool failed = false;

	public static AtomID ParseLine(Geometry geometry, string line) {
		GaussianPDBLineReader.line = line;
		pdbOption = PDBOption.PDBNAME;
		charNum = -1;
		residueID = ResidueID.Empty;
		partialCharge = 0f;
		pdbName = "";
		amberName = "";
		failed = false;
		mobile = true;

		//Loop through the atom specification line.
		//This line can take a few forms so need a very flexible parser

		charParser = ReadStart;

		while (charParser != null && !failed) {
			charParser();
		}

		if (failed) {
			return AtomID.Empty;
		}
		
		//See if residue is already present
		Residue residue;
		if (!geometry.TryGetResidue (residueID, out residue)) {
			//Not present - create a new one
			geometry.AddResidue(residueID, new Residue(residueID, residueName, geometry));
		}

		Amber amber;
		if (string.IsNullOrWhiteSpace(amberName)) {
			amber = Amber.X;
		} else {
			amber = AmberCalculator.GetAmber(amberName);
		}
		
		PDBID pdbID;
		if (string.IsNullOrEmpty(pdbName)) {
			pdbID = PDBID.FromGaussString(elementStr, elementStr, residueName);
		} else {
			pdbID = PDBID.FromGaussString(pdbName, elementStr, residueName);	
		} 
		Atom newAtom = new Atom(position, residueID, amber, partialCharge, oniomLayerID);

		CustomLogger.LogFormat(
			EL.DEBUG,
			"Adding Atom (ID: {0}): {1}",
			() => new object[] {
				new AtomID(residueID, pdbID),
				newAtom
			}
		);
		
		newAtom.mobile = mobile;
		residue.AddAtom(pdbID, newAtom, out pdbID);

		return new AtomID(residueID, pdbID);
	}

	public static string GetCurrentMethodName() {
		if (charParser == null) {
			return "null";
		} else {
			return charParser.Method.Name;
		}
	}
	
	static void ReadStart() {

		if (line.Length == 0) {
			failed = true;
			return;
		}

		elementStr = "";
		while (++charNum <= line.Length) {
			lineChar = line [charNum];
			if (lineChar != ' ') {
				elementStr += lineChar;
				charParser = ReadElement;
				break;
			}
		}
	}

	static void ReadElement() {
		//Read Element
		//Termination char:     Next phase
		// '-'                  AMBER_NAME
		// '('                  PDB_OPTION
		// ' '                  FROZEN

		while (++charNum <= line.Length) {
			lineChar = line[charNum];
			switch (lineChar) {
				case '-':
					charParser = ReadAmber;
					CheckElement();
					return;
				case '(':
					charParser = ReadPDBOption;
					CheckElement();
					return;
				case ' ':
					charParser = ReadFrozen;
					CheckElement();
					return;
				case ',':
					CheckElement();
					//Old format with no PDB Info
					failed = true;
					charParser = null;
					return;
				default:
					elementStr += lineChar;
					break;
			}
		}

		void CheckElement() {
			if (elementStr == "") {
				throw new System.Exception("Element is empty!");
			}
		}
	}

	static void ReadAmber() {
		//Read Amber name
		//Termination char:     Next phase
		// '-'                  PARTIAL_CHARGE
		// '('                  PDB_OPTION
		// ' '                  FROZEN
		
		amberName = "";
		while (++charNum <= line.Length) {
			lineChar = (charNum >= line.Length) ? ' ' : line [charNum];
			if (lineChar == '-') {
				charParser = ReadPartialCharge;
			} else if (lineChar == '(') {
				charParser = ReadPDBOption; 
			} else if (lineChar == ' ') {
				charParser = ReadFrozen;
			} else {
				amberName += lineChar;
				continue;
			}
			break;
		}
	}

	static void ReadPartialCharge() {
		//Read Partial Charge
		//Termination char:     Next phase
		// '('                  PDB_OPTION
		// ' '                  FROZEN
		
		string chargeString = "";
		while (++charNum <= line.Length) {
			lineChar = (charNum >= line.Length) ? ' ' : line [charNum];
			if (lineChar == '(') {
				charParser = ReadPDBOption;
			} else if (lineChar == ' ') {
				charParser = ReadFrozen;
			} else {
				chargeString += lineChar;
				continue;
			}
			break;
		}

		if (chargeString.Length > 0) partialCharge = float.Parse(chargeString);
	}

	static void ReadPDBOption() {
		//Read PDB_OPTION Information Block Key
		//Termination char:     Next phase
		// '='                  PDB_VALUE
		// ')'                  FROZEN
		
		string optionString = "";
		while (++charNum <= line.Length) {
			lineChar = (charNum >= line.Length) ? ' ' : line [charNum];
			if (lineChar == '=') {
				charParser = ReadPDBValue;
			} else if (lineChar == ')') {
				charParser = ReadFrozen;
			} else if (optionString.Length == 5) {
				//Just need 5 characters to differentiate PDBOptions
				pdbOption = GetPDBOption(optionString.ToUpper());
			} else {
				optionString += lineChar;
				continue;
			} 
			break;
		}

	}

	static void ReadPDBValue() {
		//Read PDB_OPTION Information Block Value
		//Termination char:     Next phase
		// ','                  PDB_OPTION
		// ')'                  FROZEN
		
		string infoString = "";
		while (++charNum <= line.Length) {
			lineChar = (charNum >= line.Length) ? ' ' : line [charNum];
			if (lineChar == ',') {
				ReadPDBInfo(infoString);
				charParser = ReadPDBOption;
			} else if (lineChar == ')') {
				ReadPDBInfo(infoString);
				charParser = ReadFrozen;
			} else {
				infoString += lineChar;
				continue;
			} 
			break;
		}
	}

	static void ReadPDBInfo(string infoString) {
		//Part of ReadPDBValue()

		switch (pdbOption) {
			case (PDBOption.PDBNAME):
				pdbName = infoString;
				return;
			case (PDBOption.RESNAME):
				residueName = infoString;
				residueID.chainID = "A";
				return;
			case (PDBOption.RESNUM):
				residueID.residueNumber = int.Parse(ToNumber(infoString));
				return;
		}
	}

	static void ReadFrozen() {
		//Read Frozen flag (OR position X)
		//Termination char:     Next phase
		// ' ' AND int          X
		// ' ' AND float        Y
		
		//This part can be binary (case (1) of sb.Length) - frozen flag
		//OR can be a float - in that case we've skipped over frozen and are reading X
		
		string frozenString = "";
		while (++charNum <= line.Length) {
			lineChar = line [charNum];	
			if (lineChar == ' ') {
				switch (frozenString.Length) {
					case (0):
						//Reading spaces - ignore
						continue;
					case (1):
						//Found frozen flag -> read X next
						int frozen = int.Parse (frozenString);
						mobile = (frozen != 0);
						charParser = ReadX;
						return;
					default:
						//Reading position X -> read Y next
						position[0] = float.Parse (frozenString);
						charParser = ReadY;
						return;
				}
			} else {
				frozenString += lineChar;
				continue;
			}
		}
	}

	static void ReadX() {
		//Read Position X
		//Termination char:     Next phase
		// ' '                  Y
		
		string xString = "";
		while (++charNum <= line.Length) {
			lineChar = (charNum >= line.Length) ? ' ' : line [charNum];	
			if (lineChar == ' ') {
				if (xString.Length == 0) {
					//Reading spaces - ignore
				} else {
					break;
				}
			} else {
				xString += lineChar;
			}
		}
		position[0] = float.Parse (xString);
		charParser = ReadY;
	}

	static void ReadY() {
		//Read Position Y
		//Termination char:     Next phase
		// ' '                  Z
		
		string yString = "";
		while (++charNum <= line.Length) {
			lineChar = (charNum >= line.Length) ? ' ' : line [charNum];	
			if (lineChar == ' ') {
				if (yString.Length == 0) {
					//Reading spaces - ignore
				} else {
					break;
				}
			} else {
				yString += lineChar;
			}
		}
		position[1] = float.Parse (yString);
		charParser = ReadZ;
	}

	static void ReadZ() {
		//Read Position Z
		//Termination char:     Next phase
		// ' '                  LAYER_NAME
		
		string zString = "";
		while (++charNum <= line.Length) {
			lineChar = (charNum >= line.Length) ? ' ' : line [charNum];	
			if (lineChar == ' ') {
				if (zString.Length == 0) {
					//Reading spaces - ignore
				} else {
					break;
				}
			} else {
				zString += lineChar;
			}
		}
		position[2] = float.Parse (zString);
		charParser = ReadLayerName;
	}

	static void ReadLayerName() {
		//Read layer name
		//Termination char:     Next phase
		// ' '                  LINK
		
		string layerNameString = "";
		while (++charNum <= line.Length) {
			lineChar = (charNum >= line.Length) ? ' ' : line [charNum];	
			if (lineChar == ' ') {
				if (layerNameString.Length == 0) {
					//Reading spaces - ignore
					continue;
				} else {
					if (!Constants.OniomLayerIDCharMap.TryGetValue(layerNameString[0], out oniomLayerID)) {
						oniomLayerID = OLID.REAL;
					}
				}
			} else {
				layerNameString += lineChar;
				continue;
			}
			break;
		}
		charParser = ReadLinkAtom;
	}

	static void ReadLinkAtom() {
		//Read layer name
		//Termination char:     Next phase
		// ' '                  LINK_INDEX

		string linkType = "";
		while (++charNum <= line.Length) {
			lineChar = (charNum >= line.Length) ? ' ' : line [charNum];	
			if (lineChar == ' ') {
				if (linkType.Length == 0) {
					//Reading spaces - ignore
				} else {
					break;
				}
			} else {
				linkType += lineChar;
			}
		}
		charParser = ReadLinkIndex;
	}

	static void ReadLinkIndex() {
		//Read layer name
		//Termination char:     Next phase
		// ' '                  
		//Final part of line parser
		
		string linkIndexString = "";
		while (charNum++ <= line.Length) {
			lineChar = (charNum >= line.Length) ? ' ' : line [charNum];	
			if (lineChar == ' ') {
				if (linkIndexString.Length == 0) {
					//Reading spaces - ignore
					continue;
				} else {
					int linkIndex = int.Parse(linkIndexString); //UNUSED
				}
			} else {
				linkIndexString += lineChar;
				continue;
			}
			break;
		}
		charParser = null;
	}
	
	static PDBOption GetPDBOption(string pdbString) {
		switch (pdbString) {
			case ("PDBNA"):
				return PDBOption.PDBNAME;
			case ("RESNA"):
				return PDBOption.RESNAME;
			case ("RESNU"):
				return PDBOption.RESNUM;
		}
		throw new System.Exception(string.Format("Failed to read PDB_OPTION Option: {0}", pdbString));
	}

	static string ToNumber(string inputStr) {
		return System.Text.RegularExpressions.Regex.Replace (inputStr, @"[^0-9 -]", string.Empty);
	}

}

