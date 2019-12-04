using System.Collections;
using System.IO;
using UnityEngine;
using VDWT = Constants.VanDerWaalsType;
using CT = Constants.CoulombType;

public static class PRMReader {

	public static IEnumerator ParametersFromAsset(string assetName, Parameters parameters) {
		string[] lines = Resources.Load<TextAsset>(Path.Combine("Data", "Parameters", assetName)).text.Split('\n');
		yield return ParametersFromPRMLines (lines, parameters);
	}

	public static IEnumerator ParametersFromPRMFile(string filename, Parameters parameters) {
		string[] lines = FileIO.Readlines (filename);
		
		yield return ParametersFromPRMLines (lines, parameters);
	}

	//This can be used to read parameters from gaussian input files as well
	public static IEnumerator ParametersFromPRMLines(string[] lines, Parameters parameters) {

		foreach (string line in lines) {
			UpdateParameterFromLine(line, parameters);
		}
		yield return null;

	}

	public static void UpdateParameterFromLine(string line, Parameters parameters) {
		
		string[] splitLine = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
		if (splitLine.Length == 0) {
			return;
		}

		string key = splitLine[0].ToUpper();
		if (key == "NONBON") {
			ParseNonBon(splitLine, parameters);
		} else if (key.StartsWith ("VDW")) {
			ParseVDW(splitLine, parameters);
		} else if (key.StartsWith ("HRMSTR")) {
			ParseStretch(splitLine, parameters);
		} else if (key.StartsWith ("HRMBND")) {
			ParseBend(splitLine, parameters);
		} else if (key.StartsWith ("AMBTRS")) {
			ParseTorsion(splitLine, parameters);
		} else if (key.StartsWith ("IMPTRS")) {
			ParseImproperTorsion(splitLine, parameters);
		}
	}

	static void ParseNonBon(string[] splitLine, Parameters parameters) {

		VDWT vType = Constants.VanDerWaalsTypeIntMap[int.Parse (splitLine [1])];
		CT cType = Constants.CoulombTypeIntMap[int.Parse (splitLine [2])];
		int vCutoff = int.Parse (splitLine [3]);
		int cCutoff = int.Parse (splitLine [4]);
		float vScale1 = float.Parse (splitLine [5]);
		float vScale2 = float.Parse (splitLine [6]);
		float vScale3 = float.Parse (splitLine [7]);
		float cScale1 = float.Parse (splitLine [8]);
		float cScale2 = float.Parse (splitLine [9]);
		float cScale3 = float.Parse (splitLine [10]);

		parameters.SetNonBonding (
			vType, 
			cType, 
			vCutoff, 
			cCutoff, 
			vScale1, 
			vScale2, 
			vScale3, 
			cScale1, 
			cScale2, 
			cScale3
		);
	}

	static void ParseVDW(string[] splitLine, Parameters parameters) {
		
		string t0 = splitLine [1];
		float req = float.Parse (splitLine [2]);
		float v = float.Parse (splitLine [3]);

		AtomicParameter atomicParameter = new AtomicParameter (t0, req, v, 0f);
		parameters.AddAtomicParameter (atomicParameter);
	}
	
	static void ParseStretch(string[] splitLine, Parameters parameters) {
		string t0 = splitLine [1];
		string t1 = splitLine [2];
		float keq = float.Parse (splitLine [3]);
		float req = float.Parse (splitLine [4]);

		Stretch stretch = new Stretch (t0, t1, req, keq);
		parameters.AddStretch (stretch);
	}
	
	static void ParseBend(string[] splitLine, Parameters parameters) {
		string t0 = splitLine [1];
		string t1 = splitLine [2];
		string t2 = splitLine [3];
		float keq = float.Parse (splitLine [4]);
		float req = float.Parse (splitLine [5]);

		Bend bend = new Bend (t0, t1, t2, req, keq);
		parameters.AddBend (bend);
	}
	
	static void ParseTorsion(string[] splitLine, Parameters parameters) {
		string t0 = splitLine [1];
		string t1 = splitLine [2];
		string t2 = splitLine [3];
		string t3 = splitLine [4];
		float gamma0 = float.Parse (splitLine [5]);
		float gamma1 = float.Parse (splitLine [6]);
		float gamma2 = float.Parse (splitLine [7]);
		float gamma3 = float.Parse (splitLine [8]);
		float v0 = float.Parse (splitLine [9]);
		float v1 = float.Parse (splitLine [10]);
		float v2 = float.Parse (splitLine [11]);
		float v3 = float.Parse (splitLine [12]);
		int npaths = (int)float.Parse (splitLine [13]);

		Torsion torsion = new Torsion (t0, t1, t2, t3, v0, v1, v2, v3, gamma0, gamma1, gamma2, gamma3, npaths);
		parameters.AddTorsion (torsion);
	}

	static void ParseImproperTorsion(string[] splitLine, Parameters parameters) {
		string t0 = splitLine [1];
		string t1 = splitLine [2];
		string t2 = splitLine [3];
		string t3 = splitLine [4];
		float v = float.Parse (splitLine [5]);;
		float gamma = float.Parse (splitLine [6]);
		int periodicity = (int)float.Parse (splitLine [7]);

		ImproperTorsion improperTorsion = new ImproperTorsion (t0, t1, t2, t3, v, gamma, periodicity);
		parameters.AddImproperTorsion (improperTorsion);

	}
}
