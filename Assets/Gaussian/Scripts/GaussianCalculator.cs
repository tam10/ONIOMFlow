using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System;
using System.Linq;
using OLID = Constants.OniomLayerID;
using GPL = Constants.GaussianPrintLevel;
using GOT = Constants.GaussianOptTarget;
using GCT = Constants.GaussianConvergenceThreshold;
using GFC = Constants.GaussianForceConstant;
using EL = Constants.ErrorLevel;

/*
A Gaussian user from Leiben
tried to TS opt psilocybin.
But one negative sign
failed Link 9999.
- should've used opt=noeigen.
*/

public class GaussianCalculator : MonoBehaviour {

	private Geometry parent;

	public int numProcessors;
	public int jobMemoryMB;

	public string checkpointPath;
	public string oldCheckpointPath;

	public string killJobLink;
	public int killJobAfter;

	
	//KEYWORDS
	//Character after # in keywords
	public GPL gaussianPrintLevel = GPL.NORMAL;

	//Methods, basis, options
	public List<string> oniomOptions;

	//Optimisation
	public bool doOptimisation;
	public bool doMicroiterations;
	public bool doQuadMacro;
	public int numOptSteps;
	public int optStepSize;
	public GCT convergenceThreshold;
	public GFC forceConstantOption;
	public int forceConstantRecalcEveryNSteps;
	public GOT optTarget;

	public List<string> GetOptimisationOptions() {
		List<string> options = new List<string>();
		if (numOptSteps > 0) {
			options.Add(string.Format("MaxCycles={0}", numOptSteps));
		}
		if (optStepSize > 0) {
			options.Add(string.Format("MaxStep={0}", optStepSize));
		}
		if (optTarget != GOT.MINIMUM) {
			options.Add(Constants.GaussianOptTargetMap[optTarget]);
		}
		if (convergenceThreshold != GCT.NORMAL) {
			options.Add(Constants.GaussianConvergenceThresholdMap[convergenceThreshold]);
		}
		if (forceConstantOption == GFC.RECALC) {
			if (forceConstantRecalcEveryNSteps == 1) {
				options.Add(Constants.GaussianForceConstantMap[GFC.RECALC]);
			} else if (forceConstantRecalcEveryNSteps < 1) {
				options.Add(Constants.GaussianForceConstantMap[GFC.CALC_FIRST]);
			} else {
				options.Add(string.Format(
					"{0}={1}",
					Constants.GaussianForceConstantMap[forceConstantOption],
					forceConstantRecalcEveryNSteps
				));
			}
		} else if (forceConstantOption != GFC.ESTIMATE) {
			options.Add(Constants.GaussianForceConstantMap[forceConstantOption]);
		}
		if (!doMicroiterations) {
			options.Add("NoMicro");
		}
		if (doQuadMacro) {
			options.Add("QuadMacro");
		}
		
		return options;
	}

	//Guess
	public List<string> guessOptions;

	//Geometry
	public List<string> geomOptions;

	//Frequency
	public bool doFreq;
	public bool useHighPrecisionModes;
	public List<string> GetFreqOptions() {
		List<string> options = new List<string>();
		if (useHighPrecisionModes) {
			options.Add("HPModes");
		}
		return options;
	}

	public List<string> additionalKeywords;

	//TITLE
	public string title;

	//LAYERS
	public Dictionary<OLID, Layer> layerDict = new Dictionary<OLID, Layer> {
		{
			OLID.REAL, 
			new Layer(
				oniomLayer:OLID.REAL
			)
		}
	};

	// Use this for initialization
	void Awake () {
		
		numProcessors = 1;
		jobMemoryMB = 4000;

		checkpointPath = "";
		oldCheckpointPath = "";

		killJobLink = "";
		killJobAfter = 1;

		doFreq = false;

		oniomOptions = new List<string> ();
		guessOptions = new List<string> ();
		geomOptions = new List<string> ();
		additionalKeywords = new List<string> ();

		title = "Title";

		SetGeometry(GetComponentInParent<Geometry>());

		if (parent == null) {
			CustomLogger.LogFormat(EL.ERROR, "Gaussian Calculator parent is not an Atoms object");
		}

	}

	public void SetGeometry(Geometry geometry) {
		parent = geometry;
	}

	public void AddLayer(string method="", string basis="", List<string> options=null, OLID oniomLayer=OLID.REAL, int charge=0, int multiplicity=0 ) {
		Layer layer = new Layer(method, basis, options, oniomLayer, charge, multiplicity);
		layerDict[oniomLayer] = layer;
	}

	public List<OLID> GetLayersInAtoms() {
		List<OLID> oniomLayers = new List<OLID>();
		foreach (Atom atom in parent.EnumerateAtoms()) {
			OLID oniomLayer = atom.oniomLayer;
			if (!oniomLayers.Contains(oniomLayer)) {
				oniomLayers.Add(oniomLayer);
			}
		}
		oniomLayers.Sort();
		oniomLayers.Reverse();
		return oniomLayers;
		
	}

	
	
    public IEnumerator EstimateChargeMultiplicity(bool getChargesFromUser) {

		foreach ((OLID oniomLayerID, Layer layer) in layerDict) {
			Geometry tempGeometry = layer.GenerateLayerGeometry(parent, null);

			if (tempGeometry.size == 0) {
				layer.charge = 0;
				layer.multiplicity = 1;
				continue;
			}

			int formalCharge;
			int multiplicity;
        	(formalCharge, multiplicity) = Data.PredictChargeMultiplicity(tempGeometry);
			
			//Very uncommon to have a doublet - increment or decrement the formal charge
			if (multiplicity == 2) {

				//Slightly more likely that the charge is overestimated to be 0
				if (formalCharge < 0) {
					formalCharge++;
				} else {
					formalCharge--;
				}

				//Set back to singlet multiplicity
				multiplicity = 1;
			}
			
			layer.charge = formalCharge;
			layer.multiplicity = multiplicity;

			if (getChargesFromUser) {
				// Get user to confirm or edit charges
				MultiPrompt multiPrompt = MultiPrompt.main;

				multiPrompt.Initialise(
					"Set Charge/Multiplicity", 
					string.Format(
						"Set the charge and multiplicity, separated by a space, for ({0}).",
						tempGeometry.name
					), 
					new ButtonSetup(text:"Confirm", action:() => {}),
					new ButtonSetup(text:"Skip", action:() => multiPrompt.Cancel()),
					input:true
				);

				multiPrompt.inputField.text = string.Format(
					"{0} {1}", 
					layer.charge, 
					layer.multiplicity 
				);

				// While loop until a valid input format is given
				// Two integers separated by a space
				// e.g. '0 1', '-1 2' e.t.c
				bool validInput = false;
				while (!validInput) {

					//Wait for user response
					while (!multiPrompt.userResponded) {
						yield return null;
					}

					if (multiPrompt.cancelled) {
						continue;
					}

					yield return null;

					//Check input here
					string input = multiPrompt.inputField.text;
					string[] split = input.Split(new char[] {' '});
					if (split.Count() != 2) {
						multiPrompt.description.text = "Input must be two integers separated by a string!";
						multiPrompt.userResponded = false;
						continue;
					}
					if (!int.TryParse(split[0], out formalCharge)) {
						multiPrompt.description.text = "Input must be two integers separated by a string!";
						multiPrompt.userResponded = false;
						continue;
					}
					if (!int.TryParse(split[1], out multiplicity)) {
						multiPrompt.description.text = "Input must be two integers separated by a string!";
						multiPrompt.userResponded = false;
						continue;
					}
					validInput = true;
				}

				multiPrompt.Hide();

				if (multiPrompt.cancelled) {
					continue;
				}
			}
			
			layer.charge = formalCharge;
			layer.multiplicity = multiplicity;

		}
    }

	public static Bash.ExternalCommand GetGaussian() {
		Bash.ExternalCommand gaussian;
		foreach (string version in new List<string> {"g16", "g09", "g03"}) {
			try {
				CustomLogger.LogFormat(
					EL.VERBOSE,
					"Checking Gaussian version '{0}'",
					version
				);
				gaussian = Settings.GetExternalCommand(version);
				CustomLogger.LogFormat(
					EL.VERBOSE,
					"Gaussian version '{0}' available",
					version
				);
				return gaussian;
			} catch (KeyNotFoundException) {}
		}
		return null;
	}
}

public class Layer {
	public OLID oniomLayer;
	public string method;
	public string basis;
	public List<string> options;
	public int charge;
	public int multiplicity;

	public Layer(string method="", string basis="", List<string> options=null, OLID oniomLayer=OLID.REAL, int charge=0, int multiplicity=1 ) {
		this.oniomLayer = oniomLayer;
		this.method = method;
		this.basis = basis;
		this.options = (options == null) ? new List<string>() : options;
		this.charge = charge;
		this.multiplicity = multiplicity;
	}

	public Geometry GenerateLayerGeometry(Geometry geometry, Transform transform) {
		//Create a new Geometry object from this layer.
		return geometry.TakeLayer(oniomLayer, transform);
	}

	public override string ToString () {
		return string.Format ("Layer(oniomLayer = {0}, method = {1}, basis = {2}, options = {3}, charge = {4}, multiplicity = {5})", oniomLayer, method, basis, options, charge, multiplicity);
	}

	public string ToMethodItem() {
		StringBuilder sb = new StringBuilder();
		sb.Append(method);
		if (basis != "") {
			sb.AppendFormat("/{0}", basis);
		}
		if (options != null) {
			switch (options.Count) {
				case (0):
					break;
				case (1):
					sb.AppendFormat("={0}", options[0]);
					break;
				default:
					sb.Append("(");
					sb.Append(string.Join(",", options));
					sb.Append(")");
					break;
			}
		}
		return sb.ToString();
	}
}














//UNUSED
public class Layers {
	public Dictionary<int, Dictionary<int, string>> linkAtomAmberType = new Dictionary<int, Dictionary<int, string>>();
	private int[] _layerList;

	private Geometry geometry;

	private int[] layerList {

		get {
			if (_layerList == null) {
				return new int[] {};
			}
			return _layerList;
		}
		set {_layerList = value;}
	}

	//private int layerList[int index] {
	//	get {return layerList[index];}
	//}

	private Dictionary<char, Layer> layerDict = new Dictionary<char, Layer>();
	public Dictionary<char, int> nameToNum = new Dictionary<char, int> { { 'H', 0 }, { 'M', 1 }, { 'L', 2 } };
	public Dictionary<int, char> numToName = new Dictionary<int, char> { { 0, 'H' }, { 1, 'M' }, { 2, 'L' } };
	public List<char> layerNames = new List<char> {};

	public void SetGeometry(Geometry geometry) {
		this.geometry = geometry;
		int[] tempArray = new int[geometry.size];
		for (int atomNum =0; atomNum<Mathf.Min( layerList.Length, geometry.size);atomNum++) {
			tempArray[atomNum] = layerList[atomNum];
		}
		layerList = tempArray;
	}

	public void SetHighAtoms(List<int> highAtomNums) {
		foreach (int atomNum in highAtomNums)
			AddAtomToLayerByNumber(0, atomNum);
	}

	public void SetMediumAtoms(List<int> mediumAtomNums) {
		foreach (int atomNum in mediumAtomNums)
			AddAtomToLayerByNumber(1, atomNum);
	}

	public void SetLowAtoms( List<int> lowAtomNums) {
		foreach (int atomNum in lowAtomNums)
			AddAtomToLayerByNumber(2, atomNum);
	}

	//public void AddLayer(string method="", string basis="", string options="", char layerName='H', int charge=0, int multiplicity=0 ) {
	//	Layer layer = new Layer (method, basis, options, layerName, charge, multiplicity);
	//	if (layerDict.ContainsKey (layerName)) {
	//		Debug.LogErrorFormat ("Layer {0} can't be added: layer already exists", layerName);
	//		return;
	//	}
//
	//	layerDict.Add (layerName, layer);
	//	if (!layerNames.Contains(layerName))
	//		layerNames.Add (layerName);
	//}

	public void AddLink(int linkAtomConnection, int linkAtomHost, string linkAtomAmber) {
		if (!linkAtomAmberType.ContainsKey (linkAtomConnection)) {
			linkAtomAmberType.Add (linkAtomConnection, new Dictionary<int, string> ());
		}

		if (!linkAtomAmberType [linkAtomConnection].ContainsKey (linkAtomHost)) {
			linkAtomAmberType [linkAtomConnection].Add (linkAtomHost, linkAtomAmber);
		} 

	}

	public void AddAtomToLayerByName(char layerName, int atomNum) {
		AddAtomToLayerByNumber( nameToNum [layerName], atomNum);
	}

	public void AddAtomToLayerByNumber(int layerNum, int atomNum) {
		layerList [atomNum] = layerNum;
	}

	public IEnumerator AddAtomsToLayerByNumberAsync(int layerNum, List<int> atomNums) {
		foreach (int atomNum in atomNums) {
			AddAtomToLayerByNumber(layerNum, atomNum);
		}
		yield return null;
	}

	public IEnumerator AddAtomsToLayerByNameAsync(char layerName, List<int> atomNums) {
		foreach (int atomNum in atomNums) {
			AddAtomToLayerByName(layerName, atomNum);
		}
		yield return null;
	}

	public int GetAtomLayerFromAtomNum(int atomNum) {
		return layerList [atomNum];
	}

	public List<int> GetLayerAtomsByInt(int layerNum) {
		List<int> layerAtoms = new List<int> ();
		for (int atomNum = 0; atomNum < layerList.Length; atomNum++) {
			if (layerList [atomNum] == layerNum)
				layerAtoms.Add (atomNum);
		}
		return layerAtoms;
	}

	public List<int> GetLayerAtomsByName(char layerNum) {
		return GetLayerAtomsByInt( nameToNum[layerNum]);
	}

	public int GetLayerCharge(char layerName, bool cascade=true) {
		return GetLayerByName (layerName, cascade).charge;
	}

	public int GetLayerMultiplicity(char layerName, bool cascade=true) {
		return GetLayerByName (layerName, cascade).multiplicity;
	}

	public string GetLayerMethod(char layerName, bool cascade=true) {
		return GetLayerByName (layerName, cascade).method;
	}

	public string GetLayerBasis(char layerName, bool cascade=true) {
		return GetLayerByName (layerName, cascade).basis;
	}

	//public string GetLayerOptions(char layerName, bool cascade=true) {
	//	return GetLayerByName (layerName, cascade).options;
	//}

	public void SetLayerCharge(char layerName, int charge) {
		GetLayerByName (layerName).charge = charge;
	}

	public void SetLayerMultiplicity(char layerName, int multiplicity) {
		GetLayerByName (layerName).multiplicity = multiplicity;
	}

	public void SetLayerMethod(char layerName, string method) {
		GetLayerByName (layerName).method = method;
	}

	public void SetLayerBasis(char layerName, string basis) {
		GetLayerByName (layerName).basis = basis;
	}

	//public void SetLayerOptions(char layerName, string options) {
	//	GetLayerByName (layerName).options = options;
	//}

	//Hide structures behind methods
	private Layer GetLayerByName(char layerName, bool cascade=true) {
		if (!cascade)
			return layerDict [layerName];
		return layerDict [Cascade(layerName)];

	}

	//Use this to get the next layer up if this layerName doesn't exist
	private char Cascade(char layerName) {
		int layerNum = nameToNum [layerName];

		while (layerNum > 0) {
			layerName = numToName [layerNum];
			if (layerDict.ContainsKey (layerName))
				break;
			
			layerNum--;
		}
		return layerName;
	}

	public override string ToString ()
	{
		StringBuilder sb = new StringBuilder ();

		int layerNum = 0;
		while (true) {
			if (layerNum == layerNames.Count)
				break;
			sb.Append (string.Format("{0}: {1}", layerNames [layerNum], GetLayerAtomsByInt(layerNum).Count));
			sb.Append (", ");
			layerNum++;
		}
		return string.Format ("Layers: {0}", sb.ToString());
	}



}