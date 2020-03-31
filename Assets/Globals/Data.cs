using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Linq;
using System.Linq;
using System;
using System.Text;
using Element = Constants.Element;
using BT = Constants.BondType;
using RS = Constants.ResidueState;
using EL = Constants.ErrorLevel;
using Amber = Constants.Amber;

public static class Data {

	//Data lists

	private static Dictionary<uint, float[]> bondDistancesDict;
	private static Dictionary<uint, float[]> bondDistancesSquaredDict;
	public static Dictionary<string, AminoAcid> aminoAcids;
	public static Dictionary<string, AminoAcid> waterResidues;
	public static Dictionary<string, AminoAcid> ionResidues;
	private static Dictionary<CNSCount, List<AminoAcidState>> atomCountFamilyDict;
	public static Dictionary<string, Dictionary<RS, Residue>> standardResidues;


	public static List<PDBID> backbonePDBs = new List<PDBID> {
		new PDBID(Element.N, "", 0),
		new PDBID(Element.C, "", 0),
		new PDBID(Element.O, "", 0),
		new PDBID(Element.C, "A", 0),
		new PDBID(Element.H, "", 0),
		new PDBID(Element.H, "A", 0)
	};

	public static Dictionary<string, string> residueName3To1 = new Dictionary<string, string> {
		{"ALA", "A"},
		{"ARG", "R"},
		{"ASN", "N"}, {"ASH", "N"},
		{"ASP", "D"},
		{"CYS", "C"}, {"CYM", "C"}, {"CYX", "C"},
		{"GLU", "E"}, {"GLH", "E"}, 
		{"GLN", "Q"},
		{"GLY", "G"},
		{"HIS", "H"}, {"HID", "H"}, {"HIE", "H"}, {"HIP", "H"}, 
		{"ILE", "I"},
		{"LEU", "L"},
		{"LYN", "K"},
		{"LYS", "K"},
		{"MET", "M"},
		{"PHE", "F"},
		{"PRO", "P"},
		{"SER", "S"},
		{"THR", "T"},
		{"TRP", "W"},
		{"TYR", "Y"},
		{"VAL", "V"}
	};

	public static PDBID CTER_ID = new PDBID(Element.O, "XT", 0);
	public static PDBID NTER_ID = new PDBID(Element.N, "", 2);
	public static List<Element> electronWithdrawingElements = new List<Element> {
		Element.N, 
		Element.O, 
		Element.F, 
		Element.S, 
		Element.Cl
	};

	public static Dictionary<Amber, Dictionary<int, float>> formalChargesDict = new Dictionary<Amber, Dictionary<int, float>>();
	public static Dictionary<Amber, float> aromaticAmbers = new Dictionary<Amber, float>();

	public static Amber waterAmberH = Amber.HO;
	public static Amber waterAmberO = Amber.OH;

	public const float angstromToBohr = 1.8897259886f;
	public const float kcalToHartree = 0.0015936011f;

	public static List<string> gaussianMethods;

	public static IEnumerator Initialise() {

		SettingsBuilder.AddProgressText("Loading Databases... " + FileIO.newLine);

		// STANDARD RESIDUES
		SettingsBuilder.AddProgressText("Loading Standard Residues... ");
		yield return GetLoaderIEnumerator(PopulateStandardResidues());

		// BOND DISTANCES
		SettingsBuilder.AddProgressText("Loading Bond Distances... ");
		yield return GetLoaderIEnumerator(PopulateBondDistancesDict());

		// AMINO ACIDS
		SettingsBuilder.AddProgressText("Loading Amino Acid Classifications... ");
		yield return GetLoaderIEnumerator(PopulateAminoAcidDicts());

		// GAUSSIAN METHODS
		SettingsBuilder.AddProgressText("Loading Gaussian Methods... ");
		yield return GetLoaderIEnumerator(PopulateGaussianMethods());

		SettingsBuilder.AddProgressText("Databases loaded." + FileIO.newLine + FileIO.newLine);
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

	private static IEnumerator PopulateGaussianMethods() {
		string gaussianMethodsPath;
		if (!Settings.TryGetPath(Settings.dataPath, Settings.gaussianMethodsFilename, out gaussianMethodsPath)) {
			throw new System.IO.FileNotFoundException("Could not find Projects Settings File: {0}", Settings.gaussianMethodsFilename);
		}
		XDocument gaussianMethodsX = FileIO.ReadXML (gaussianMethodsPath);

		gaussianMethods = new List<string> ();
		XElement methodsList = gaussianMethodsX.Element ("methods");

		foreach (XElement methodName in methodsList.Elements("method")) {
			if (!gaussianMethods.Contains(methodName.Value)) {
				gaussianMethods.Add(methodName.Value);
			}

			if (Timer.yieldNow) {yield return null;}
		}
	}

	private static IEnumerator PopulateAminoAcidDicts() {
		string standardResiduePath;
		if (!Settings.TryGetPath(Settings.dataPath, Settings.standardResiduePDBsFilename, out standardResiduePath)) {
			throw new System.IO.FileNotFoundException("Could not find Standard Residue Filename File: {0}", Settings.standardResiduePDBsFilename);
		}

		aminoAcids = new Dictionary<string, AminoAcid>();
		waterResidues = new Dictionary<string, AminoAcid>();
		ionResidues = new Dictionary<string, AminoAcid>();
		atomCountFamilyDict = new Dictionary<CNSCount, List<AminoAcidState>>();

		XDocument xDocument = FileIO.ReadXML (standardResiduePath);
		XElement standardResiduesX = xDocument.Element("standardResidues");

		foreach (XElement standardResidueX in standardResiduesX.Elements("residue")) {
			string residueName = FileIO.ParseXMLAttrString(standardResidueX, "name");
			string family = FileIO.ParseXMLAttrString(standardResidueX, "family");
			
			aminoAcids[residueName] = new AminoAcid(family);
			foreach (XElement atomsStateX in standardResidueX.Elements("atoms")) {

				string stateName = FileIO.ParseXMLAttrString(atomsStateX, "state");
				RS state = Settings.GetResidueState(stateName);
				AminoAcidState aminoAcidState = ParseAminoAcidState(atomsStateX, residueName, state, family);
				aminoAcids[residueName].AddState(state, aminoAcidState);

				CNSCount cnsCount = new CNSCount(aminoAcidState.pdbIDs.ToList());
				if (atomCountFamilyDict.ContainsKey(cnsCount)) {
					atomCountFamilyDict[cnsCount].Add(aminoAcidState);
				} else {
					atomCountFamilyDict[cnsCount] = new List<AminoAcidState> {aminoAcidState};
				}
			}

			if (Timer.yieldNow) {yield return null;}
		}

		foreach (XElement waterResidueX in standardResiduesX.Elements("waterResidue")) {
			string residueName = FileIO.ParseXMLAttrString(waterResidueX, "name");
			string family = FileIO.ParseXMLAttrString(waterResidueX, "family");

			waterResidues[residueName] = new AminoAcid(family);
			foreach (XElement atomsStateX in waterResidueX.Elements("atoms")) {

				string stateName = FileIO.ParseXMLAttrString(atomsStateX, "state");
				RS state = Settings.GetResidueState(stateName);
				AminoAcidState aminoAcidState = ParseAminoAcidState(atomsStateX, residueName, state, family);
				waterResidues[residueName].AddState(state, aminoAcidState);
			}

			if (Timer.yieldNow) {yield return null;}
		}

		foreach (XElement ionResidueX in standardResiduesX.Elements("ionResidue")) {
			string residueName = FileIO.ParseXMLAttrString(ionResidueX, "name");
			string family = FileIO.ParseXMLAttrString(ionResidueX, "family");

			ionResidues[residueName] = new AminoAcid(family);
			foreach (XElement atomsStateX in ionResidueX.Elements("atoms")) {

				string stateName = FileIO.ParseXMLAttrString(atomsStateX, "state");
				RS state = Settings.GetResidueState(stateName);
				AminoAcidState aminoAcidState = ParseAminoAcidState(atomsStateX, residueName, state, family);
				ionResidues[residueName].AddState(state, aminoAcidState);
			}

			if (Timer.yieldNow) {yield return null;}
		}
	}

	private static IEnumerator PopulateStandardResidues() {
		standardResidues = new Dictionary<string, Dictionary<RS, Residue>>();
		string standardResiduesPath = Path.Combine("Data", Settings.standardResiduesDirectory);

		TextAsset[] standardResidueAssets = Resources.LoadAll<TextAsset>(standardResiduesPath);

		for (int fileNum=0; fileNum<standardResidueAssets.Length; fileNum++) {
			TextAsset standardResidueAsset = standardResidueAssets[fileNum];
			
			Geometry geometry = PrefabManager.InstantiateGeometry(null);
			yield return FileReader.LoadGeometry(geometry, standardResidueAsset);

			if (geometry.residueCount != 1) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Wrong number of Residues ({0} - should be 1) in Atoms: {1} (PopulateStandardResidues)", 
					geometry.residueCount, 
					standardResidueAsset.name
				);
			} else {
				(ResidueID residueID, Residue residue) = geometry.EnumerateResidues().First();

				string residueName = residue.residueName;
				RS residueState = residue.state;

				Dictionary<RS, Residue> stateResidues;
				if (!standardResidues.TryGetValue(residueName, out stateResidues)) {
					standardResidues[residueName] = new Dictionary<RS, Residue> {{residueState, residue}};
				} else {
					standardResidues[residueName][residueState] = residue;
				}
			}

			GameObject.Destroy(geometry.gameObject);

			if (Timer.yieldNow) {yield return null;}

		}

	}

	private static AminoAcidState ParseAminoAcidState(XElement atomsStateX, string residueName, RS state, string family) {
		float charge = FileIO.ParseXMLAttrFloat(atomsStateX, "charge");

		List<string> pdbs = new List<string>();
		List<string> altPdbs = new List<string>();
		List<Amber> ambers = new List<Amber>();
		List<string> elements = new List<string>();
		List<float> partialCharges = new List<float>();
		List<float> radii = new List<float>();

		foreach (XElement atomX in atomsStateX.Elements("atom")) {
			string pdb = FileIO.ParseXMLString(atomX, "pdb");
			string altPdb = atomX.Elements("altPdb").Any() ? FileIO.ParseXMLString(atomX, "altPdb") : "";

			string amber = FileIO.ParseXMLString(atomX, "amber");
			string element = FileIO.ParseXMLString(atomX, "element");

			float partialCharge = FileIO.ParseXMLFloat(atomX, "partialCharge");
			float radius = FileIO.ParseXMLFloat(atomX, "radius");

			pdbs.Add(pdb);
			altPdbs.Add(altPdb);

			ambers.Add(AmberCalculator.GetAmber(amber));
			elements.Add(element);
			partialCharges.Add(partialCharge);
			radii.Add(radius);

		}

		XElement dihedrals = atomsStateX.Element("dihedrals");

		List<string> dihedralPDBIDs = new List<string>();

		Action<string> AddPDBID = x => {
			XElement pdbX = dihedrals.Element(x);
			if (pdbX != null) {
				dihedralPDBIDs.Add(pdbX.Value);
			}
		};

		if (dihedrals != null) {
			AddPDBID("pdb0");
			AddPDBID("pdb1");
			AddPDBID("pdb2");
			AddPDBID("pdb3");
			AddPDBID("pdb4");
			AddPDBID("pdb5");
		}

		AminoAcidState aminoAcidState = new AminoAcidState(
			charge,
			residueName,
			state,
			family,
			elements.ToArray(),
			pdbs.ToArray(),
			dihedralPDBIDs.ToArray(),
			ambers.ToArray(),
			partialCharges.ToArray(),
			radii.ToArray()
		);


		return aminoAcidState;
	}

	public static uint GetHash(Element element0, Element element1) {
		return (uint)element0 * 255 + (uint)element1;
	}

	private static IEnumerator PopulateBondDistancesDict() {
		string bondDistancePath;
		if (!Settings.TryGetPath(Settings.dataPath, Settings.bondDistancesFilename, out bondDistancePath)) {
			throw new System.IO.FileNotFoundException("Could not find Bond Distance File: {0}", Settings.gaussianMethodsFilename);
		}
		XDocument xDocument = FileIO.ReadXML (bondDistancePath);
		
		bondDistancesDict = new Dictionary<uint, float[]>();
		bondDistancesSquaredDict = new Dictionary<uint, float[]>();

		XElement elementsX = xDocument.Element("elements");
		foreach (XElement atom0X in elementsX.Elements("atom")) {
			//Atom 0
			Element element0 = Constants.ElementMap[FileIO.ParseXMLAttrString(atom0X, "element")];

			foreach (XElement atom1X in atom0X.Elements("other")) {
				//Atom 1
				Element element1 = Constants.ElementMap[FileIO.ParseXMLAttrString(atom1X, "element")];
				float[] initialDistances = new float[4] {0f, 0f, 0f, 0f};
				//Build distance array for each type
				// [a, b, c, d]
				//
				// e.g. l = distance
				//     l > a  -> NONE
				// a > l > b  -> SINGLE 
				// b > l > c  -> AROMATIC
				// c > l > d  -> DOUBLE
				// d > l      -> TRIPLE

				//Get distances from database
				foreach (XElement bondTypeX in atom1X.Elements()) {
					float distance = float.Parse(bondTypeX.Value);
					switch (Constants.BondTypeMap[bondTypeX.Name.ToString()]) {
						case (Constants.BondType.SINGLE):
							initialDistances[0] = distance;
							break;
						case (Constants.BondType.AROMATIC):
							initialDistances[1] = distance;
							break;
						case (Constants.BondType.DOUBLE):
							initialDistances[2] = distance;
							break;
						case (Constants.BondType.TRIPLE):
							initialDistances[3] = distance;
							break;
					}
				}

				float[] distances = new float[4] {0f, 0f, 0f, 0f};
				
				//Add bond leeway
				for (int i=0; i<4; i++) {
					if (initialDistances[i] > 0) {
						distances[i] = initialDistances[i] + Settings.bondLeeway;
						break;
					}
				}

				
				for (int i=3; i>=0; i--) {
					//Copy element to the right if distance isn't in data
					//This means the entry will be ignored
					if (i<3 && distances[i]<=0) {
						distances[i] = distances[i+1];
					}

					//If there's an element here and to the left,
					// this entry becomes the average of the two
					for (int j=i-1; j>=0; j--) {
						if (initialDistances[i] > 0 && initialDistances[j] > 0) {
							distances[i] = (initialDistances[i] + initialDistances[j]) * 0.5f;
							break;
						}
					}

				}
				float[] distancesSquared = CustomMathematics.Squared(distances);

				uint forwardKey = GetHash(element0, element1);
				uint backwardKey = GetHash(element1, element0);

				bondDistancesDict[forwardKey] = distances;
				bondDistancesDict[backwardKey] = distances;
				bondDistancesSquaredDict[forwardKey] = distancesSquared;
				bondDistancesSquaredDict[backwardKey] = distancesSquared;

			}
			if (Timer.yieldNow) {yield return null;}
		}

	}

	public static float[] GetBondDistances(Element element0, Element element1) {
		return bondDistancesDict[GetHash(element0, element1)];
	}

	public static bool TryGetBondDistances(Element element0, Element element1, out float[] bondDistances) {
		return bondDistancesDict.TryGetValue(GetHash(element0, element1), out bondDistances);
	}

	public static float[] GetBondDistancesSquared(Element element0, Element element1) {
		return bondDistancesSquaredDict[GetHash(element0, element1)];
	}

	public static bool TryGetBondDistancesSquared(Element element0, Element element1, out float[] bondDistances) {
		return bondDistancesSquaredDict.TryGetValue(GetHash(element0, element1), out bondDistances);
	}

	private static float[] cachedDistances;
	public static BT GetBondOrder(Element element0, Element element1, float distance) {

		if (!bondDistancesDict.TryGetValue(GetHash(element0, element1), out cachedDistances)) {
			return BT.NONE;
		}

		if (distance > cachedDistances[0]) return BT.NONE;
		if (distance > cachedDistances[1]) return BT.SINGLE;
		if (distance > cachedDistances[2]) return BT.AROMATIC;
		if (distance > cachedDistances[3]) return BT.DOUBLE;
		return BT.TRIPLE;

	}


	public static BT GetBondOrderDistanceSquared(Element element0, Element element1, float distanceSquared) {

		if (!bondDistancesSquaredDict.TryGetValue(GetHash(element0, element1), out cachedDistances)) {
			return BT.NONE;
		}

		if (distanceSquared > cachedDistances[0]) {return BT.NONE;}
		if (distanceSquared > cachedDistances[1]) {return BT.SINGLE;}
		if (distanceSquared > cachedDistances[2]) {return BT.AROMATIC;}
		if (distanceSquared > cachedDistances[3]) {return BT.DOUBLE;}
		return BT.TRIPLE;

	}

	public static Amber GetLinkType(Atom connectionAtom, PDBID connectionPDBID) {
		switch (connectionPDBID.element) {
			case (Element.C):
				int electronWithDrawingCount = connectionAtom.EnumerateConnections()
					.Select(x => electronWithdrawingElements.Contains(x.Item1.pdbID.element))
					.Count();
				if (connectionAtom.amber == Amber.CA) {
					//Aromatic H
					if (electronWithDrawingCount == 0) {return Amber.HA;}
					if (electronWithDrawingCount == 1) {return Amber.H4;}
					if (electronWithDrawingCount == 2) {return Amber.H5;}
				} else {
					//Aliphatic H
					if (electronWithDrawingCount == 1) {return Amber.H1;}
					if (electronWithDrawingCount == 2) {return Amber.H2;}
					if (electronWithDrawingCount == 2) {return Amber.H3;}
				}
				return Amber.HC;
			case (Element.O):
				return Amber.HO;
			case (Element.S):
				return Amber.HS;
		}
		return Amber.H;
	}

	public static void SetResidueProperties(ref Residue residue) {

		//Determine state using terminal atom PDBs
		List<PDBID> pdbIDs = residue.pdbIDs.ToList();
		int size = residue.size;;

		if (size > 3) {
			SetLargeResidueProperties(ref residue, pdbIDs, size);
		} else {
			SetSmallResidueProperties(ref residue, pdbIDs, size);
		}

	}

	public static void SetResidueAmbers(ref Residue residue) {

		string residueName = residue.residueName;

		if (residue.state == RS.ION) {
			SetIonAmbers(ref residue);
			return;
		}

		if (!(residue.standard || residue.state == RS.CAP)) return;
		
		Dictionary<RS, Residue> family;
		if (! standardResidues.TryGetValue(residue.residueName, out family)) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Could not find Residue Family {0}", 
				residue.residueName
			);
			return;
		}

		Residue stateResidue;
		if (! family.TryGetValue(residue.state, out stateResidue)) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Could not find Residue State {0} for Residue Family {1}", 
				residue.state, 
				residue.residueName
			);
			return;
		}

		foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
			Atom stateAtom;
			if (! stateResidue.TryGetAtom(pdbID, out stateAtom)) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Could not find PDBID {0} in Residue State {1} for Residue Family {2}", 
					pdbID, 
					residue.state, 
					residue.residueName
				);
				if (SetResidueAmbersWrongState(ref residue, family)) {
					CustomLogger.LogFormat(
						EL.INFO,
						"Changed Residue State to {0} for ResidueID {1}", 
						residue.state, 
						residue.residueID
					);
				} else {
					CustomLogger.LogFormat(
						EL.ERROR,
						"Could not determine Residue State for ResidueID {0}. Setting to {1}", 
						residue.residueID, 
						residue.state
					);
				}
				return;
			}
			atom.amber = stateAtom.amber;
		}
	}

	private static void SetIonAmbers(ref Residue residue) {

		PDBID[] pdbIDs = residue.pdbIDs.ToArray();
		PDBID pdbID = pdbIDs.First();
		Atom atom = residue.GetAtom(pdbID);
		Amber amber = Amber.X;

		AminoAcid ionResidue;
		if (ionResidues.TryGetValue(residue.residueName, out ionResidue)) {
			amber = ionResidue.GetAmbersFromPDBs(RS.ION, pdbIDs).First();
		} else {
			string element = pdbID.element.ToString();
			if (!AmberCalculator.TryGetAmber(element, out amber)) {
				amber = Amber.X;
				CustomLogger.LogFormat(
					EL.WARNING,
					"Ion Residue {0} not recognised - using X as AMBER name", 
					residue.residueName, 
					element
				);
			} else {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Ion Residue {0} not recognised - using {1} as AMBER name", 
					residue.residueName, 
					element
				);
			}
		}
		atom.amber = amber;
	}

	private static bool SetResidueAmbersWrongState(ref Residue residue, Dictionary<RS, Residue> family) {
		// If the wrong state is declared for a residue, look up all residues with the same name.
		// Set the correct Ambers for this state
		// Assign the correct state for the residue
		foreach ((RS residueState, Residue familyResidue) in family) {
			foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
				Atom stateAtom;
				if (! familyResidue.TryGetAtom(pdbID, out stateAtom)) {
					goto NEXT_FAMILY;
				}
				atom.amber = stateAtom.amber;
			}
			// Found the correct state
			residue.state = residueState;
			return true;

			NEXT_FAMILY:;
		}


		foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
			atom.amber = Amber.X;
		}
		residue.state = RS.UNKNOWN;
		return false;
	}

	private static void SetSmallResidueProperties(ref Residue residue, List<PDBID> pdbIDs, int size) {
		//Hetero-residue/Water
		if (size == 3) {
			if (residue.residueName == "ACE" || residue.residueName == "NME") { 
				SetLargeResidueProperties(ref residue, pdbIDs, size); 
				return;
			} else if (pdbIDs.Select(x => x.element).OrderBy(x => x).SequenceEqual(new List<Element> {Element.H, Element.H, Element.O})) {
				//Water
				residue.state = RS.WATER;
				residue.protonated = true;
				residue.residueName = Settings.standardWaterResidueName;
				foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
					if (pdbID.element == Element.H) {
						atom.amber = waterAmberH;
					} else if (pdbID.element == Element.O) {
						atom.amber = waterAmberO;
					}
				}
				return;
			}
		} else if (size == 1) {

			PDBID pdbID = pdbIDs.First();
			if (pdbID.element == Element.O) {
				//Water
				residue.state = RS.WATER;
				residue.protonated = false;
				residue.residueName = Settings.standardWaterResidueName;
				return;
			} else {
				foreach ((string ionName, AminoAcid ionState) in ionResidues) {
					PDBID[] ionPDBIDs = ionState.GetPDBIDs(RS.ION);

					if (ionPDBIDs != null && ionPDBIDs.First() == pdbID) {
						residue.state = RS.ION;
						residue.protonated = true;
						residue.residueName = ionName;
						float charge = ionState
							.GetPartialChargesFromPDBs(RS.ION, ionPDBIDs)
							.Sum();
						residue.GetAtom(pdbID).partialCharge = charge;
					}
				}
				return;
			}
		}

		//Residue not in database
		residue.state = RS.HETERO;
		residue.protonated = false;
	}

	private static void SetLargeResidueProperties(ref Residue residue, List<PDBID> pdbIDs, int size) {
		//Could be an amino acid. Use signature to determine what it is

		//Reduce number of lookups using carbon and nitrogen count
		List<AminoAcidState> families = GetResidueFamilies(pdbIDs);

		string matchedFamily = residue.residueName;
		residue.state = RS.NONSTANDARD;

		ResidueSignature signature = new ResidueSignature(pdbIDs);
		residue.protonated = (signature.hydrogenSignature != "");

		//Determine amino acid family
		foreach (AminoAcidState state in families) {
			if (state.signature.HeavyAtomsMatch(signature)) {
				matchedFamily = state.family;
				if (matchedFamily == "ACE" || matchedFamily == "NME") {
					residue.state = RS.CAP;
				} else {
					residue.state = GetTerminalState(pdbIDs, size);
				}
				break;
			}
		}

		//Get protonation state
		if (residue.protonated && residue.standard) {
			foreach (AminoAcidState state in families) {
				if (state.family == matchedFamily && state.signature.HydrogensMatch(signature)) {
					residue.residueName = state.residueName;
					residue.state = state.residueState;
					break;
				}
			}
		} else {
			residue.residueName = matchedFamily;
		}
	}

	public static Constants.ResidueState GetTerminalState(List<PDBID> pdbIDs, int size) {
		for (int i=0; i<size; i++) {
			if (pdbIDs[i] == Data.CTER_ID) {
				return RS.C_TERMINAL;
			} else if (pdbIDs[i] == Data.NTER_ID) {
				return RS.N_TERMINAL;
			}
		}
		return RS.STANDARD;
	}

	public static Amber[] GetResidueAmbers(PDBID[] pdbIDs, string residueName, RS state) {
		return aminoAcids[residueName].GetAmbersFromPDBs(state, pdbIDs);
	}

	private struct CNSCount {
		public int carbonCount;
		public int nitrogenCount;
		public int sulfurCount;

		public CNSCount(
			int carbonCount,
			int nitrogenCount,
			int sulfurCount
		) {
			this.carbonCount = carbonCount;
			this.nitrogenCount = nitrogenCount;
			this.sulfurCount = sulfurCount;
		}

		public CNSCount(List<PDBID> pdbIDs) {
			carbonCount = 0;
			nitrogenCount = 0;
			sulfurCount = 0;
			foreach (PDBID pdbID in pdbIDs) {
				switch (pdbID.element) {
					case(Element.C): carbonCount++; break;
					case(Element.N): nitrogenCount++; break;
					case(Element.S): sulfurCount++; break;
				}
			}
		}

		public override bool Equals(object other) {
			CNSCount cnsCount = (CNSCount)other;
			if (other == null) return false;
			return (
				this.carbonCount == cnsCount.carbonCount &&
				this.nitrogenCount == cnsCount.nitrogenCount &&
				this.sulfurCount == cnsCount.sulfurCount
			);
		}

		public static bool operator ==(CNSCount cnsCount0, CNSCount cnsCount1) {
		if (ReferenceEquals(cnsCount0, cnsCount1)) return true;
		if (ReferenceEquals(cnsCount0, null) || ReferenceEquals(null, cnsCount1)) return false;
		return cnsCount0.Equals(cnsCount1);
		}

		public static bool operator !=(CNSCount cnsCount0, CNSCount cnsCount1) {
			return !(cnsCount0 == cnsCount1);
		}

		public override int GetHashCode() {
			return 268435456 * carbonCount + 16384 * nitrogenCount + sulfurCount;
		}
	}

	//Residue family lookup
	private static List<AminoAcidState> GetResidueFamilies(List<PDBID> pdbIDs) {
		List<AminoAcidState> families;
		if (!atomCountFamilyDict.TryGetValue(new CNSCount(pdbIDs), out families)) {
			families = new List<AminoAcidState>(); 
		}
		return families;
	}

	public static (int, int) PredictChargeMultiplicity(Geometry geometry) {
		float aromaticCount = 0;
		float chargeContribution = 0f;
		int electrons = 0;
		foreach ((AtomID atomID, Atom atom) in geometry.EnumerateAtomIDPairs()) {
			Amber amber = atom.amber;
			electrons += atomID.pdbID.atomicNumber;

			float aromaticContribution;
			if (aromaticAmbers.TryGetValue(amber, out aromaticContribution)) {
				aromaticCount += aromaticContribution;
			}

			Dictionary<int, float> neighboursToCharge;
			if (formalChargesDict.TryGetValue(amber, out neighboursToCharge)) {
				
				int numNeighbours = atom.internalConnections.Count + atom.externalConnections.Count;
				if (neighboursToCharge.TryGetValue(numNeighbours, out chargeContribution)) {
					chargeContribution += chargeContribution;
				}
			}
		}
		int predictedCharge = Mathf.RoundToInt(chargeContribution) + Mathf.RoundToInt(aromaticCount) % 2;
		int predictedMultiplicity = ((electrons + predictedCharge) % 2)  + 1;
		return (predictedCharge, predictedMultiplicity);

	}

}

public class AminoAcid
{
	private Dictionary<RS, AminoAcidState> stateDict = new Dictionary<RS, AminoAcidState>();
	public string family;

	public AminoAcid(string family) {
		this.family = family;
	}

	public void AddState(RS residueState, AminoAcidState aminoAcidState) {
		stateDict[residueState] = aminoAcidState;
	}
	
	public PDBID[] GetPDBIDs(RS residueState) {

		AminoAcidState aminoAcidState;
		return stateDict.TryGetValue(residueState, out aminoAcidState) ? aminoAcidState.pdbIDs : null;
	}
	
	public PDBID[][] GetDihedralPDBIDs(RS residueState) {

		AminoAcidState aminoAcidState;
		return stateDict.TryGetValue(residueState, out aminoAcidState) ? aminoAcidState.dihedrals : null;
	}
	
	public Amber[] GetAmbersFromPDBs(RS residueState, PDBID[] pdbIDs) {
		AminoAcidState aminoAcidState = stateDict[residueState];
		int size = pdbIDs.Length;

		Amber[] ambers = new Amber[size];
		for (int i=0; i<size; i++){
			ambers[i] = aminoAcidState.GetAmber(pdbIDs[i]);
		}
		return ambers;
	}
	
	public float[] GetPartialChargesFromPDBs(RS residueState, PDBID[] pdbIDs) {
		AminoAcidState aminoAcidState = stateDict[residueState];
		int size = pdbIDs.Length;
		
		float[] partialCharges = new float[size];
		for (int i=0; i<size; i++){
			partialCharges[i] = aminoAcidState.GetPartialCharge(pdbIDs[i]);
		}
		return partialCharges;
	}
	
	public float[] GetRadiiFromPDBs(RS residueState, PDBID[] pdbIDs) {
		AminoAcidState aminoAcidState = stateDict[residueState];
		int size = pdbIDs.Length;
		
		float[] radii = new float[size];
		for (int i=0; i<size; i++){
			radii[i] = aminoAcidState.GetRadius(pdbIDs[i]);
		}
		return radii;
	}
}

public class AminoAcidState {
	public readonly int size;
	public readonly float charge;
	public readonly string residueName;
	public readonly RS residueState;
	public readonly string family;
	public PDBID[] pdbIDs;
	public PDBID[][] dihedrals;
	private Amber[] ambers;
	private float[] radii;
	private float[] partialCharges;
	public ResidueSignature signature;

	public AminoAcidState(
		float charge,
		string residueName,
		RS residueState,
		string family,
		string[] elements,
		string[] pdbs,
		string[] dihedralPDBs,
		Amber[] ambers,
		float[] partialCharges,
		float[] radii
	) {
		this.residueName = residueName;
		this.residueState = residueState;
		this.family = family;
		this.charge = charge;

		this.size = pdbs.Length;
		this.pdbIDs = new PDBID[size];
		this.ambers = ambers;
		this.radii = radii;
		this.partialCharges = partialCharges;

		for (int i=0; i<pdbs.Length; i++) {
			PDBID pdbID = PDBID.FromString(pdbs[i], residueName);
			if (pdbID.IsEmpty()) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"PDBID Empty while generating Amino Acid State ({0} {1} {2})",
					family,
					residueName,
					residueState
				);
			} else {
				this.pdbIDs[i] = pdbID;
			}
		}

		PDBID[] dihedralPDBIDs = new PDBID[dihedralPDBs.Length + 3];
		dihedralPDBIDs[0] = PDBID.N;
		dihedralPDBIDs[1] = PDBID.CA;
		dihedralPDBIDs[2] = PDBID.CB;

		//Build up PDBID chains from backbone - these are the flexible dihedrals
		dihedrals = new PDBID[dihedralPDBs.Length][];
		for (int i=0; i<dihedralPDBs.Length; i++) {
			PDBID dihedralPDBID = PDBID.FromString(dihedralPDBs[i], residueName);

			if (!pdbIDs.Contains(dihedralPDBID)) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Dihedral PDBID '{0}' not present in Amino Acid State ({1} {2} {3})",
					dihedralPDBID,
					family,
					residueName,
					residueState
				);
			}

			dihedralPDBIDs[i + 3] = dihedralPDBID;
			dihedrals[i] = new PDBID[4] {
				dihedralPDBIDs[i],
				dihedralPDBIDs[i+1],
				dihedralPDBIDs[i+2],
				dihedralPDBIDs[i+3]
			};
		}

		signature = new ResidueSignature(this.pdbIDs);
	}

	public int IndexOf(PDBID pdbID) {
		for (int i=0; i<size; i++) {if (pdbIDs[i] == pdbID) {return i;}}
		throw new ErrorHandler.PDBIDException(string.Format("PDBID '{0}' not found in Amino Acid State {1}({2})", pdbID, residueName, residueState), pdbID.ToString());
	}

	public Amber GetAmber(PDBID pdbID) {
		return ambers[IndexOf(pdbID)];
	}
	public float GetPartialCharge(PDBID pdbID) {
		return partialCharges[IndexOf(pdbID)];
	}
	public float GetRadius(PDBID pdbID) {
		return radii[IndexOf(pdbID)];
	}
}

public class ResidueSignature
{
	private static List<string> cList = new List<string>();
	private static List<string> nList = new List<string>();
	private static List<string> oList = new List<string>();
	private static List<string> sList = new List<string>();
	private static List<string> hList = new List<string>();

	public string heavySignature;
	public string hydrogenSignature;
	public ResidueSignature(IEnumerable<PDBID> pdbIDs) {
		cList.Clear(); 
		nList.Clear(); 
		oList.Clear(); 
		sList.Clear(); 
		hList.Clear();
		foreach (PDBID pdbID in pdbIDs) {
			switch(pdbID.element) {
				case (Element.C): cList.Add(pdbID.identifier); break;
				case (Element.N): nList.Add(pdbID.identifier); break;
				case (Element.O): oList.Add(pdbID.identifier); break;
				case (Element.S): sList.Add(pdbID.identifier); break;
				case (Element.H): hList.Add(pdbID.identifier); break;
			}
		}
		cList.Sort(); 
		nList.Sort(); 
		oList.Sort(); 
		sList.Sort(); 
		hList.Sort();
		hydrogenSignature = string.Concat(hList);
		heavySignature = string.Concat(cList.Concat(nList).Concat(oList).Concat(sList));
	}

	public bool HydrogensMatch(ResidueSignature other) {
		return (hydrogenSignature == other.hydrogenSignature);
	}

	public bool HeavyAtomsMatch(ResidueSignature other) {
		return (heavySignature == other.heavySignature);
	}
}
