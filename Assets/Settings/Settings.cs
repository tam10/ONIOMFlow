using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using Element = Constants.Element;
using RCID = Constants.ResidueCheckerID;
using ACID = Constants.AtomCheckerID;
using TID = Constants.TaskID;
using BT = Constants.BondType;
using RS = Constants.ResidueState;
using PDT = Constants.PropertyDisplayType;
using RP = Constants.ResidueProperty;
using CT = Constants.ConnectionType;
using EL = Constants.ErrorLevel;
using SIZE = Constants.Size;
using OLID = Constants.OniomLayerID;
using Amber = Constants.Amber;
using UnityEditor;

public static class Settings {


	// SETTINGS FILES

	public static string projectSettingsFilename = "ProjectSettings.xml";
	public static string graphicsSettingsFilename;
	public static string flowSettingsFilename;
	public static string tasksSettingsFilename;
	public static string atomsSettingsFilename;
	public static string residueTableSettingsFilename;
	public static string gaussianMethodsFilename;
	public static string bondDistancesFilename;
	public static string standardResiduePDBsFilename;
	public static string defaultParametersFilename;
	public static string standardResiduesDirectory;
	public static string sasaFileName;
	public static string logFilename;

	//ENVIRONMENT

	public static string home;
	public static PlatformID platformID;
	public static bool isWindows;
	public static bool isRetina;
	
	public static string currentDirectory;
	public static string projectPath;
	public static string projectSettingsPath;
	public static string flowSettingsPath;
	public static string settingsPath;
	public static string dataPath;

	//GRAPHICS
	public static int _ballResolution = 1;
	public static int ballResolution {
		get => _ballResolution;
		set {
			Sphere.main.UpdateReference();
			_ballResolution = value;
		}
	}
	private static int _stickResolution = 1;
	public static int stickResolution {
		get => _stickResolution;
		set {
			Cylinder.main.UpdateReference();
			_stickResolution = value;
		}
	}

	//ATOMS
	public static float bondLeeway = 0.1f;
	public static Parameters defaultParameters;

	//REPRESENTATIONS
	private static Color unknownColour = new Color(1.0f, 0.0f, 1.0f, 1.0f);
	private static Dictionary<Element, Color> atomColours = new Dictionary<Element, Color> ();
	public static Color GetAtomColourFromElement(Element element) {
		Color colour;
		return atomColours.TryGetValue(element, out colour) ? colour : unknownColour;
	}

	public static Color GetAtomColourFromCharge(float charge) {
		if (charge > Settings.noChargeThreshold) {
			return Color.Lerp(Settings.neutralColour, Settings.positiveColour, charge);
		} else if (charge < -Settings.noChargeThreshold) {
			return Color.Lerp(Settings.neutralColour, Settings.negativeColour, -charge);
		} else {
			return Settings.nochargeColour;
		}
	}

	public static Color GetAtomColourFromAMBER(Amber amber) {
		switch (amber) {
			case Amber._: return maxPenaltyColour;
			case Amber.X: return maxPenaltyColour;
			case Amber.DU: return Color.yellow;
		}
		return zeroPenaltyColour;
	}

	public static Color GetAtomColourFromPenalty(float penalty, float max=100f) {
		if (penalty > max) {
			return maxPenaltyColour;
		} else {
			return Color.Lerp(zeroPenaltyColour, maxPenaltyColour, penalty / max);
		}
	}

	public static Color GetAtomColourFromSASA(float sasa, float max=1f) {
		if (sasa == 0) {return nochargeColour;}
		return Color.Lerp(neutralColour, positiveColour, sasa / max);
	}

	public static Color negativeColour = new Color(0f, 0f, 1f, 1f);
	public static Color neutralColour = new Color(0.5f, 0.5f, 0.5f, 0.5f);
	public static Color positiveColour = new Color(1f, 0f, 0f, 1f);
	public static Color nochargeColour = new Color(0.7f, 0.7f, 0.5f, 1f);

	public static Color zeroPenaltyColour = new Color(0f, 0.5f, 0f, 1f);
	public static Color maxPenaltyColour = new Color(1.5f, 0f, 0f, 1f);

	public static List<float> chargeDistributions = new List<float> {0.4f, 0.3f, 0.2f, 0.1f};

	private static float unknownRadius = 1f;
	private static Dictionary<Element, float> atomRadii = new Dictionary<Element, float> ();
	public static float GetAtomRadiusFromElement(Element element) {
		float radius;
		return atomRadii.TryGetValue(element, out radius) ? radius : unknownRadius;
	}

	private static float unknownMass = 1f;
	private static Dictionary<Element, float> atomMasses = new Dictionary<Element, float> ();
	public static float GetAtomMassFromElement(Element element) {
		float mass;
		return atomMasses.TryGetValue(element, out mass) ? mass : unknownMass;
	}


	public static float atomicRadiusToSphereRatio = 0.25f;
	public static float atomicRadiusToCylinderRatio = 0.1f;
	public static float secondaryResidueAlphaMultiplier = 0.5f;
	public static float secondaryResidueRadiusMultiplier = 0.5f;
 
 
	//NOT YET IN FILE

	public static float maxNonBondingCutoff = 15f;

	public static float fogStartDistance = 1f;
	public static float fogEndDistance = 50f;
	public static float fogRatio = 0.5f;
	public static float GetFogAmount(float distance) => Mathf.Lerp(0f, fogRatio, (distance - fogStartDistance) / fogEndDistance);
	public static float lineThickness = 0.2f;
	public static Dictionary<OLID, float> layerLineThicknesses = new Dictionary<OLID, float> {
		{OLID.REAL, 0.5f},
		{OLID.INTERMEDIATE, 1f},
		{OLID.MODEL, 2f}
	};

	public static bool useParallelLineDrawer = false;
 
	public static int amberRepResolution = 2;
	public static float amberRepThickness = 0.1f;
	public static float amberStretchInterval = 0.05f;
	public static int amberStretchSteps = 10;
	public static float amberStretchRepOffset = 0.2f;

	public static Dictionary<SIZE, int> fontSizeDict = new Dictionary<SIZE, int> {
		{SIZE.VSMALL, 12},
		{SIZE.SMALL, 14},
		{SIZE.MSMALL, 18},
		{SIZE.MEDIUM, 20},
		{SIZE.MLARGE, 24},
		{SIZE.LARGE, 28},
		{SIZE.VLARGE, 32}
	};

	//CONNECTIVITY
	//PDBIDs that link residues
	public static Dictionary<PDBID, CT> standardConnectionDict = new Dictionary<PDBID, CT> ();

	//Elements that can connect to other residues
	public static List<Element> nonStandardConnections = new List<Element> ();

	private static Dictionary<string, Bash.ExternalCommand> externalCommands = new Dictionary<string, Bash.ExternalCommand>();
	public static Bash.ExternalCommand GetExternalCommand(string name) {
		Bash.ExternalCommand externalCommand;
		if (!externalCommands.TryGetValue(name, out externalCommand)) {
			throw new KeyNotFoundException(string.Format(
				"Could not find external command: {0}", name
			));
		}
		return externalCommand;
	}
	
	private static string _tempFolder;
	public static string tempFolder {
		get {
			if (!Directory.Exists(_tempFolder))
				Directory.CreateDirectory(_tempFolder);
			return _tempFolder;
		}
		set {_tempFolder = value;}
	}

	//INTERACTIONS
	public static Dictionary<string, List<PDBID>> positiveChargeSites = new Dictionary<string, List<PDBID>>();
	public static Dictionary<string, List<PDBID>> negativeChargeSites = new Dictionary<string, List<PDBID>>();
	public static Dictionary<string, List<List<PDBID>>> ringSites = new Dictionary<string, List<List<PDBID>>>();

	//PARTIAL CHARGES
	private static string redCalcBaseName;
	public static string redCalcPath;
	public static string redCommandPath;
	public static string redDirectory;
	public static string chargesDirectory;
	public static string redChargesPath;

	public static float noChargeThreshold = 0.0001f;
	public static float partialChargeThreshold = 0.1f;
	public static float integerChargeThreshold = 0.1f;

	//WATER
	public static string standardWaterResidueName = "HOH";

	//LAYERS
	public static Dictionary<int, bool> layerSphereRenderDict = new Dictionary<int, bool>() {{0, true}, {1, true}, {2, false}};
	public static Dictionary<int, bool> layerWireRenderDict = new Dictionary<int, bool>() {{0, false}, {1, false}, {2, true}};

	//NONSTANDARD RESIDUES
	public static string baseNSRName = "CR";

	//These are the default column titles of the residue table. Allows future modification by the user.
	public static List<RP> residueTableProperties = new List<RP> {
		RP.CHAINID, 
		RP.RESIDUE_NUMBER, 
		RP.RESIDUE_NAME, 
		RP.STATE, 
		RP.PROTONATED, 
		RP.CHARGE, 
		RP.SIZE,
		RP.SELECTED
	};


	public static IEnumerator Initialise(string path) {

		//Environment
		platformID = Environment.OSVersion.Platform;
		isWindows = (platformID != PlatformID.Unix && platformID != PlatformID.MacOSX);
    	home = isWindows ? Environment.SpecialFolder.Personal.ToString() : Environment.GetEnvironmentVariable("HOME");

		#if UNITY_EDITOR
		isRetina = EditorGUIUtility.pixelsPerPoint == 2;
		#else
		isRetina = false;
		#endif

		projectPath = path;
		currentDirectory = path;
		settingsPath = Path.Combine(projectPath, "Settings");
		dataPath = Path.Combine(projectPath, "Data");

		yield return GetSettings ();
	}

	static IEnumerator GetSettings() {

		SettingsBuilder.AddProgressText("Loading Settings... " + FileIO.newLine);
		
		// PROJECT SETTINGS
		SettingsBuilder.AddProgressText("Loading Project Settings... ");
		yield return GetLoaderIEnumerator(GetProjectSettings());
		
		// GRAPHICS SETTINGS
		SettingsBuilder.AddProgressText("Loading Graphics Settings... ");
		yield return GetLoaderIEnumerator(GetGraphicsSettings());

		// TASKS SETTINGS
		SettingsBuilder.AddProgressText("Loading Tasks Settings... ");
		yield return GetLoaderIEnumerator(GetTasksSettings());
		
		// ATOMS SETTINGS
		SettingsBuilder.AddProgressText("Loading Atoms Settings... ");
		yield return GetLoaderIEnumerator(GetAtomsSettings());
		
		// FLOW SETTINGS
		SettingsBuilder.AddProgressText("Loading Flow Settings... ");
		yield return GetLoaderIEnumerator(GetFlowSettings());

		// RESIDUE TABLE SETTINGS
		SettingsBuilder.AddProgressText("Loading Residue Table Settings... ");
		yield return GetLoaderIEnumerator(GetResidueTableSettings());

		SettingsBuilder.AddProgressText("Settings loaded." + FileIO.newLine + FileIO.newLine);
	}

	private static IEnumerator GetLoaderIEnumerator(IEnumerator iEnumerator) {
		while (true) {
			try {
				if (!iEnumerator.MoveNext()) {break;}
			} catch (SystemException e) {
				SettingsBuilder.AddProgressText("<color=#ff0000>Error!</color>" + FileIO.newLine + e.Message + FileIO.newLine + e.StackTrace);

			}
			yield return iEnumerator.Current;
		}
		SettingsBuilder.AddProgressText("<color=#00ff00>Done.</color>" + FileIO.newLine);
	}

	// PATH

	public static bool TryGetPath(string directory, string filename, out string path) {
		path = "";

		string fullpath = Path.Combine (directory, filename);

		if (File.Exists (fullpath) || Directory.Exists(fullpath)) {
			path = Path.GetFullPath(fullpath);
			return true;
		}
		return false;
	}

	public static string JoinDirectories(params string[] directories) {
		string path = "";
		foreach (string directory in directories) {
			path = Path.Combine(path, directory);
		}
		return path;
	}

	

	static IEnumerator GetProjectSettings() {
		if (!TryGetPath(settingsPath, projectSettingsFilename, out projectSettingsPath)) {
			throw new System.IO.FileNotFoundException(
				string.Format("Could not find Projects Settings File: {0}", projectSettingsFilename)
			);
		}

		XDocument sX = FileIO.ReadXML (projectSettingsPath);
		XElement pX = sX.Element ("projectSettings");

		//Filenames
		graphicsSettingsFilename = FileIO.ParseXMLString(pX, "graphicsSettingsFilename");
		flowSettingsFilename = FileIO.ParseXMLString(pX, "flowSettingsFilename");
		tasksSettingsFilename = FileIO.ParseXMLString(pX, "tasksSettingsFilename");
		atomsSettingsFilename = FileIO.ParseXMLString(pX, "atomsSettingsFilename");
		residueTableSettingsFilename = FileIO.ParseXMLString(pX, "residueTableSettingsFilename");

		//Logs
		logFilename = FileIO.ParseXMLString(pX, "logFilename");
		CustomLogger.logPath = Path.Combine(projectPath, logFilename);

		//Data files
		gaussianMethodsFilename = FileIO.ParseXMLString(pX, "gaussianMethodsFilename");
		sasaFileName = FileIO.ParseXMLString(pX, "sasaFileName");
		bondDistancesFilename = FileIO.ParseXMLString(pX, "bondDistancesFilename");
		standardResiduePDBsFilename = FileIO.ParseXMLString(pX, "standardResiduePDBsFilename");
		standardResiduesDirectory = FileIO.ParseXMLString(pX, "standardResiduesDirectory");

		//Fullpaths
		tempFolder = Path.Combine( Application.persistentDataPath, "Temp");

		defaultParametersFilename = FileIO.ParseXMLString(pX, "defaultParametersFilename");
		
		defaultParameters = PrefabManager.InstantiateParameters(null);
		yield return PRMReader.ParametersFromAsset(defaultParametersFilename, defaultParameters);

		foreach (XElement externalCommandX in pX.Element("externalCommands").Elements("externalCommand")) {
			yield return Bash.ExternalCommand.FromXML(externalCommandX, externalCommands);
		}

		sX.Save(projectSettingsPath);

		//PARTIAL CHARGES
		XElement partialChargesX = pX.Element("partialCharges");
		redCommandPath = FileIO.ExpandEnvironmentVariables(FileIO.ParseXMLString(partialChargesX, "commandPath"));
		redCalcBaseName = FileIO.ParseXMLString(partialChargesX, "redBaseName");
		redCalcPath = FileIO.ExpandEnvironmentVariables(Path.Combine(projectPath, redCalcBaseName));
		redDirectory = Path.Combine(projectPath, FileIO.ParseXMLString(partialChargesX, "redDirectory"));
		chargesDirectory = Path.Combine(projectPath, FileIO.ParseXMLString(partialChargesX, "chargesDirectory"));
		redChargesPath = FileIO.ExpandEnvironmentVariables(Path.Combine(redDirectory, FileIO.ParseXMLString(partialChargesX, "redChargeFilename")));

	}

	// GRAPHICS

	static IEnumerator GetGraphicsSettings() {
		string graphicsSettingsPath;
		if (!TryGetPath(settingsPath, graphicsSettingsFilename, out graphicsSettingsPath)) {
			throw new System.IO.FileNotFoundException("Could not find Graphics Settings File: {0}", graphicsSettingsFilename);
		}

		int qualitySetting = QualitySettings.GetQualityLevel ();

		XDocument sX = FileIO.ReadXML (graphicsSettingsPath);
		foreach (XElement el in sX.Element("graphics").Elements("globalResolution")) {
			if (FileIO.ParseXMLAttrInt(el, "value") == qualitySetting) {
				ballResolution = FileIO.ParseXMLInt(el, "ballResolution");
				stickResolution = FileIO.ParseXMLInt(el, "stickResolution");
			}
		}
		yield return null;

	}

	private static IEnumerator GetAtomsSettings() {
		string atomsSettingsPath;
		if (!TryGetPath(settingsPath, atomsSettingsFilename, out atomsSettingsPath)) {
			throw new System.IO.FileNotFoundException("Could not find Atoms Settings File: {0}", atomsSettingsFilename);
		}
		
		XDocument sX = FileIO.ReadXML (atomsSettingsPath);
		XElement atomsX = sX.Element ("atoms");

		foreach (XElement elementX in atomsX.Element("elements").Elements("element")) {
			
			Element element = FileIO.GetConstant(elementX, "ID", Constants.ElementMap, true);

			float red = FileIO.ParseXMLFloat(elementX, "red", 1f);
			float green = FileIO.ParseXMLFloat(elementX, "green", 0f);
			float blue = FileIO.ParseXMLFloat(elementX, "blue", 1f);
			float alpha = FileIO.ParseXMLFloat(elementX, "alpha", 0.6f);
			Color colour = new Color(red, green, blue, alpha);

			float mass = FileIO.ParseXMLFloat(elementX, "mass", 1.0f);
			float radius = FileIO.ParseXMLFloat(elementX, "radius", 1.0f);

			int atomicNumber = FileIO.ParseXMLInt(elementX, "atomicNumber", 0);

			if (element == Element.X) {
				unknownColour = colour;
				unknownRadius = radius;
				unknownMass = mass;
			} 

			atomColours[element] = colour;
			atomRadii[element] = radius;
			atomMasses[element] = mass;
			

			if (Timer.yieldNow) {yield return null;}
		}

		bondLeeway = FileIO.ParseXMLFloat(atomsX, "bondLeeway");
		standardWaterResidueName = FileIO.ParseXMLString(atomsX, "standardWaterResidueName");

		foreach (XElement standardConnectionX in atomsX.Element("standardConnections").Elements("connection")) {
			PDBID connectionPDBID = PDBID.FromString(standardConnectionX.Value, "");
			CT connectionType = GetConstant(standardConnectionX, "type", Constants.ConnectionTypeMap, true);
			standardConnectionDict[connectionPDBID] = connectionType;
		}

		foreach (XElement nonStandardConnectionX in atomsX.Element("nonStandardConnections").Elements("element")) {
			nonStandardConnections.Add(Constants.ElementMap[nonStandardConnectionX.Value]);
		}

		foreach (XElement bondX in atomsX.Element("bonds").Elements("bond")) {
			string bondName = FileIO.ParseXMLString(bondX, "name");
			string gaussString = FileIO.ParseXMLString(bondX, "gaussString");
			string triposString = FileIO.ParseXMLString(bondX, "triposString");

			BT bondType = GetConstant(bondX, "ID", Constants.BondTypeMap, true);

			bondIDToNameDict[bondType] = bondName;

			bondIDToGaussStringDict[bondType] = gaussString;
			bondGaussFloatToIDDict[float.Parse(gaussString)] = bondType;
			
			bondIDToTriposStringDict[bondType] = triposString;
			bondTriposStringToIDDict[triposString] = bondType;

			if (Timer.yieldNow) {yield return null;}
		}

		foreach (XElement positiveResidueX in atomsX.Element("positiveChargeSites").Elements("residue")) {
			string residueName = FileIO.ParseXMLAttrString(positiveResidueX, "name");
			List<PDBID> pdbIDs = new List<PDBID>();

			foreach (XElement pdbIDX in positiveResidueX.Elements("pdbID")) {
				pdbIDs.Add(PDBID.FromString(pdbIDX.Value, residueName));
			}
			positiveChargeSites[residueName] = pdbIDs;
		}

		foreach (XElement negativeResidueX in atomsX.Element("negativeChargeSites").Elements("residue")) {
			string residueName = FileIO.ParseXMLAttrString(negativeResidueX, "name");
			List<PDBID> pdbIDs = new List<PDBID>();

			foreach (XElement pdbIDX in negativeResidueX.Elements("pdbID")) {
				pdbIDs.Add(PDBID.FromString(pdbIDX.Value, residueName));
			}
			negativeChargeSites[residueName] = pdbIDs;
		}

		foreach (XElement ringResidueX in atomsX.Element("ringSites").Elements("residue")) {
			string residueName = FileIO.ParseXMLAttrString(ringResidueX, "name");

			List<List<PDBID>> ringList = new List<List<PDBID>>();
			foreach (XElement ringX in ringResidueX.Elements("ring")) {

				List<PDBID> pdbIDs = new List<PDBID>();

				foreach (XElement pdbIDX in ringX.Elements("pdbID")) {
					pdbIDs.Add(PDBID.FromString(pdbIDX.Value, residueName));
				}
				ringList.Add(pdbIDs);
			}

			ringSites[residueName] = ringList;
		}

		foreach (XElement formalChargeAtomX in atomsX.Element("formalCharges").Elements("atom")) {
			string amber = FileIO.ParseXMLAttrString(formalChargeAtomX, "amber");

			Dictionary<int, float> neighboursToCharges = new Dictionary<int, float>();

			foreach (XElement entryX in formalChargeAtomX.Elements("entry")) {
				int numNeighbours = FileIO.ParseXMLAttrInt(entryX, "neighbours");
				float formalCharge = float.Parse(entryX.Value);

				neighboursToCharges[numNeighbours] = formalCharge;
			}

			Data.formalChargesDict[AmberCalculator.GetAmber(amber)] = neighboursToCharges;
		}

		foreach (XElement aromaticAmberX in atomsX.Element("aromaticAmbers").Elements("entry")) {
			string amber = FileIO.ParseXMLAttrString(aromaticAmberX, "amber");
			float count = float.Parse(aromaticAmberX.Value);

			Data.aromaticAmbers[AmberCalculator.GetAmber(amber)] = count;
		}

	}

	private static IEnumerator GetTasksSettings() {
		string tasksSettingsPath;
		if (!TryGetPath(settingsPath, tasksSettingsFilename, out tasksSettingsPath)) {
			throw new System.IO.FileNotFoundException("Could not find Tasks Settings File: {0}", tasksSettingsFilename);
		}

		XDocument sX = FileIO.ReadXML (tasksSettingsPath);
		XElement tasksX = sX.Element ("tasks");

		Dictionary<string, string> parentNameToFullName = tasksX.Elements("parentClass")
			.ToDictionary(
				x => FileIO.ParseXMLString(x, "name"),
				x => FileIO.ParseXMLString(x, "fullName")
			);

		foreach (XElement taskX in tasksX.Elements("task")) {
			string parentClass = FileIO.ParseXMLString(taskX, "parentClass");


			try {
				TID taskID = GetConstant(taskX, "name", Constants.TaskIDMap);
				taskFullNameDict[taskID] = FileIO.ParseXMLString(taskX, "fullName");
				taskDescriptionDict[taskID] = FileIO.ParseXMLString(taskX, "description");
				string parentClassFullName;
				if (!parentNameToFullName.TryGetValue(parentClass, out parentClassFullName)) {
					throw new SystemException(string.Format(
						"Parent Class '{0}' not found! Cannot add Task '{1}'",
						parentClass,
						taskID
					));
				}
				taskParentClasses[taskID] = parentNameToFullName[parentClass];
			} catch (SystemException e) {
				FileIO.ThrowXMLError(taskX, tasksSettingsPath, "GetTasksSettings", e);
				throw e;
			}

			if (Timer.yieldNow) {yield return null;}
		}
	}

	private static IEnumerator GetFlowSettings() {
		if (!TryGetPath(settingsPath, flowSettingsFilename, out flowSettingsPath)) {
			throw new System.IO.FileNotFoundException("Could not find Flow Settings File: {0}", flowSettingsFilename);
		}
		yield return null;
	}

	private static IEnumerator GetResidueTableSettings() {

		string residueTableSettingsPath;
		if (!TryGetPath(settingsPath, residueTableSettingsFilename, out residueTableSettingsPath)) {
			throw new System.IO.FileNotFoundException("Could not find Residue Table Settings File: {0}", residueTableSettingsFilename);
		}
		
		XDocument sX = FileIO.ReadXML (residueTableSettingsPath);
		XElement rtX = sX.Element ("residueTable");

		foreach (XElement rpX in rtX.Elements("residueProperty")) {

			RP residueProperty = GetConstant(rpX, "name", Constants.ResiduePropertyMap, fromAttribute:true);

			residuePropertyTitles[residueProperty] = FileIO.ParseXMLString(rpX, "title");
			residuePropertyTableWidths[residueProperty] = FileIO.ParseXMLFloat(rpX, "columnWidth");
			string displayTypeName = FileIO.ParseXMLString(rpX, "displayType");
			residuePropertyDisplayTypes[residueProperty] = Constants.PropertyDisplayTypeMap[displayTypeName];

			if (Timer.yieldNow) {yield return null;}
		}

	}







	//ATOMS
	private static Dictionary<BT, string> bondIDToNameDict = new Dictionary<BT, string>();
	public static string GetBondName(BT bondType) {
		string bondName;
		if (!Constants.BondTypeMap.TryGetValue(bondType, out bondName)) {
			throw new ErrorHandler.InvalidBondType(string.Format("Invalid Bond Type: {0}", bondType), bondType);
		}
		return bondName;
	}

	private static Dictionary<BT, string> bondIDToGaussStringDict = new Dictionary<BT, string>();
	public static string GetBondGaussString(BT bondType) {
		string bondGaussString;
		if (!bondIDToGaussStringDict.TryGetValue(bondType, out bondGaussString)) {
			throw new ErrorHandler.InvalidBondType(string.Format("Invalid Bond Type: {0}", bondType), bondType);
		}
		return bondGaussString;
	}

	private static Dictionary<float, BT> bondGaussFloatToIDDict = new Dictionary<float, BT>();
	public static BT GetBondIDFromGaussFloat(float bondFloat) {
		BT bondType;

		foreach (KeyValuePair<float, BT> keyValuePair in bondGaussFloatToIDDict) {
			if (Mathf.Abs(keyValuePair.Key - bondFloat) < 0.001f) {
				bondType = keyValuePair.Value;
				goto INDICT;
			}
		}
		throw new ErrorHandler.InvalidBondType(string.Format("Unrecognised Bond Float from Gaussian Input: {0}", bondFloat));
		INDICT:

		return bondType;
	}

	private static Dictionary<BT, string> bondIDToTriposStringDict = new Dictionary<BT, string>();
	public static string GetBondTriposString(BT bondType) {
		string bondTriposString;
		if (!bondIDToTriposStringDict.TryGetValue(bondType, out bondTriposString)) {
			throw new ErrorHandler.InvalidBondType(string.Format("Invalid Bond Type: {0}", bondType), bondType);
		}
		return bondTriposString;
	}

	private static Dictionary<string, BT> bondTriposStringToIDDict = new Dictionary<string, BT>();
	public static BT GetBondIDFromTriposString(string bondString) {
		BT bondType;

		if (!bondTriposStringToIDDict.TryGetValue(bondString, out bondType)) {
			throw new ErrorHandler.InvalidBondType(string.Format("Invalid TRIPOS Bond String: {0}", bondString), bondType);
		}

		return bondType;
	}

	//TASKS

	private static Dictionary<TID, string> taskFullNameDict = new Dictionary<TID, string>();
	public static string GetTaskFullName(TID taskID) {
		string taskName;
		if (!taskFullNameDict.TryGetValue(taskID, out taskName)) {
			return taskID.ToString();
		}
        return taskName;
    }

	private static Dictionary<TID, string> taskDescriptionDict = new Dictionary<TID, string>();
	public static string GetTaskDescription(TID taskID) {
		string taskDescription;
		if (!taskDescriptionDict.TryGetValue(taskID, out taskDescription)) {
			return taskID.ToString();
		}
        return taskDescription;
    }

	public static Dictionary<TID, string> taskParentClasses = new Dictionary<TID, string>();
	public static string GetTaskParentClass(TID taskID) {
		string taskParentClass;
		if (!taskParentClasses.TryGetValue(taskID, out taskParentClass)) {
			return taskID.ToString();
		}
		return taskParentClass;
	}


	//CHECKERS

	private static Dictionary<RCID, string> residueCheckerTitles = new Dictionary<RCID, string> {
		{RCID.PROTONATED, "Is Protonated"},
		{RCID.PDBS_UNIQUE, "Has No Repeated PDB Names"},
		{RCID.STANDARD, "Is Standard"},
		{RCID.PARTIAL_CHARGES, "Has Partial Charges"},
		{RCID.INTEGER_CHARGE, "Has Integer Charge"}
	};
	public static string GetResidueCheckerTitle(RCID residueCheckerID) {
		string residueCheckerTitle;
		if (!residueCheckerTitles.TryGetValue(residueCheckerID, out residueCheckerTitle)) {
			throw new ErrorHandler.InvalidResidueCheckerID(
				string.Format("Invalid Residue Checker ID: {0}", residueCheckerID),
				residueCheckerID
			);
		}
		return residueCheckerTitle;
	}
	

	private static Dictionary<ACID, string> atomCheckerTitles = new Dictionary<ACID, string> {
		{ACID.HAS_PDB, "Element has PDB Type"},
		{ACID.HAS_AMBER, "Element has AMBER Type"},
		{ACID.HAS_VALID_AMBER, "Element has a valid AMBER Type"},
		{ACID.PDBS_ALPHANUM, "PBD has no special characters"}
	};
	public static string GetAtomCheckerTitle(ACID atomCheckerID) {
		string atomCheckerTitle;
		if (!atomCheckerTitles.TryGetValue(atomCheckerID, out atomCheckerTitle)) {
			throw new ErrorHandler.InvalidAtomCheckerID(
				string.Format("Invalid Atom Checker ID: {0}", atomCheckerID),
				atomCheckerID
			);
		}
		return atomCheckerTitle;
	}

	private static Dictionary<RCID, string> residueCheckerDescriptions = new Dictionary<RCID, string> {
		{RCID.PROTONATED, "Get the number of protonated residues"},
		{RCID.PDBS_UNIQUE, "Check there are no repeated PDB names in residues"},
		{RCID.STANDARD, "Check that each residue has the required Residue and PDB Names"},
		{RCID.PARTIAL_CHARGES, "Check that each residue has Partial Charges"},
		{RCID.INTEGER_CHARGE, "Check that each residue has an integer Total Charge"}
	};
	public static string GetResidueCheckerDescription(RCID residueCheckerID) {
		string residueCheckerDescription;
		if (!residueCheckerDescriptions.TryGetValue(residueCheckerID, out residueCheckerDescription)) {
			throw new ErrorHandler.InvalidResidueCheckerID(
				string.Format("Invalid Residue Checker ID: {0}", residueCheckerID),
				residueCheckerID
			);
		}
		return residueCheckerDescription;
	}

	private static Dictionary<ACID, string> atomCheckerDescriptions = new Dictionary<ACID, string> {
		{ACID.HAS_PDB, "Element has PDB Type"},
		{ACID.HAS_AMBER, "Element has AMBER Type"},
		{ACID.HAS_VALID_AMBER, "Element has a valid AMBER Type"},
		{ACID.PDBS_ALPHANUM, "PBDs have no special characters"}
	};
	public static string GetAtomCheckerDescription(ACID atomCheckerID) {
		string atomCheckerDescription;
		if (!atomCheckerDescriptions.TryGetValue(atomCheckerID, out atomCheckerDescription)) {
			throw new ErrorHandler.InvalidAtomCheckerID(
				string.Format("Invalid Atom Checker ID: {0}", atomCheckerID),
				atomCheckerID
			);
		}
		return atomCheckerDescription;
	}

	// STATES
	public static RS GetResidueState(string stateName) {
		RS state;
		if (!Constants.ResidueStateMap.TryGetValue(stateName, out state)) {
			throw new ErrorHandler.InvalidResidueState(
				string.Format("Invalid Residue State ID: {0}", state),
				state
			);
		}
		return state;
	}

	//Residue Table

	private static Dictionary<RP, string> residuePropertyTitles = new Dictionary<RP, string> ();
	public static string GetResiduePropertyTitle(RP residueProperty) {
		string residuePropertyTitle;
		if (!residuePropertyTitles.TryGetValue(residueProperty, out residuePropertyTitle)) {
			throw new ErrorHandler.InvalidResiduePropertyID(string.Format("Invalid Residue Property ID: {0}", residueProperty), residueProperty);
		}
		return residuePropertyTitle;
	} 

	private static Dictionary<RP, float> residuePropertyTableWidths = new Dictionary<RP, float> ();
	public static float GetResiduePropertyWidth(RP residueProperty) {
		float residuePropertyWidth;
		if (!residuePropertyTableWidths.TryGetValue(residueProperty, out residuePropertyWidth)) {
			throw new ErrorHandler.InvalidResiduePropertyID(string.Format("Invalid Residue Property ID: {0}", residueProperty), residueProperty);
		}
		return residuePropertyWidth;
	} 

	private static Dictionary<RP, PDT> residuePropertyDisplayTypes = new Dictionary<RP, PDT> ();
	public static PDT GetResiduePropertyDisplayType(RP residueProperty) {
		PDT displayType;
		if (!residuePropertyDisplayTypes.TryGetValue(residueProperty, out displayType)) {
			throw new ErrorHandler.InvalidResiduePropertyID(string.Format("Invalid Residue Property ID: {0}", residueProperty), residueProperty);
		}
		return displayType;
	}

	//Tools
	private static T GetConstant<T>(XElement xElement, string key, Map<string, T> constantDict, bool fromAttribute=false) {
		string name;
		if (fromAttribute) {
			name = FileIO.ParseXMLAttrString(xElement, key);
		} else {
			name = FileIO.ParseXMLString(xElement, key);
		}
		
		T constant;
		if (!constantDict.TryGetValue(name, out constant)) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Failed to read {0}. String: {1}", 
				xElement.BaseUri, 
				name
			);
		}
		return constant;
	}

}
