using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TID = Constants.TaskID;
using VDWT = Constants.VanDerWaalsType;
using CT = Constants.CoulombType;
using EL = Constants.ErrorLevel;
using System.Diagnostics;
using System.Linq;

public class Parameters : MonoBehaviour {

	public float dielectricConstant;
	public Geometry parent;

	public List<AtomicParameter> atomicParameters;
	public List<Stretch> stretches;
	public List<Bend> bends;
	public List<Torsion> torsions;
	public List<ImproperTorsion> improperTorsions;
	public NonBonding nonbonding;

	// Use this for initialization
	void Awake () {
		nonbonding = new NonBonding ();
		atomicParameters = new List<AtomicParameter> ();
		stretches = new List<Stretch> ();
		bends = new List<Bend> ();
		torsions = new List<Torsion> ();
		improperTorsions = new List<ImproperTorsion> ();
		dielectricConstant = 1.0f;
	}

	public Parameters Duplicate(Transform parent=null) {

		Parameters newParameters = PrefabManager.InstantiateParameters(parent);

		newParameters.nonbonding = nonbonding.Copy();
		newParameters.atomicParameters = atomicParameters.Select(x => x.Copy()).ToList();
		newParameters.stretches = stretches.Select(x => x.Copy()).ToList();
		newParameters.bends = bends.Select(x => x.Copy()).ToList();
		newParameters.torsions = torsions.Select(x => x.Copy()).ToList();
		newParameters.improperTorsions = improperTorsions.Select(x => x.Copy()).ToList();
		newParameters.dielectricConstant = dielectricConstant;

		return newParameters;
	}

	public static void Copy(Geometry fromParent, Geometry toParent) {

		Parameters toParameters = toParent.parameters;
		Parameters fromParameters = fromParent.parameters;

		toParameters.nonbonding = fromParameters.nonbonding.Copy();
		toParameters.atomicParameters = fromParameters.atomicParameters.Select(x => x.Copy()).ToList();
		toParameters.stretches = fromParameters.stretches.Select(x => x.Copy()).ToList();
		toParameters.bends = fromParameters.bends.Select(x => x.Copy()).ToList();
		toParameters.torsions = fromParameters.torsions.Select(x => x.Copy()).ToList();
		toParameters.improperTorsions = fromParameters.improperTorsions.Select(x => x.Copy()).ToList();
		toParameters.dielectricConstant = fromParameters.dielectricConstant;
	}

	public static void UpdateParameters(Geometry fromParent, Geometry toParent, bool replace=false) {

		Parameters toParameters = toParent.parameters;
		Parameters fromParameters = fromParent.parameters;
		
		if (replace)
			toParameters.nonbonding = fromParameters.nonbonding;
		
		for (int otherIndex = 0; otherIndex < fromParameters.atomicParameters.Count; otherIndex++) {
			int thisIndex = toParameters.IndexAtomicParameter (fromParameters.atomicParameters [otherIndex]);
			if (thisIndex == -1) {
				toParameters.atomicParameters.Add (fromParameters.atomicParameters [otherIndex].Copy ());
			} else if (replace) {
				toParameters.atomicParameters [thisIndex] = fromParameters.atomicParameters [otherIndex].Copy ();
			}
		}

		for (int otherIndex = 0; otherIndex < fromParameters.stretches.Count; otherIndex++) {
			int thisIndex = toParameters.IndexStretch (fromParameters.stretches [otherIndex]);
			if (thisIndex == -1) {
				toParameters.stretches.Add (fromParameters.stretches [otherIndex].Copy ());
			} else if (replace) {
				toParameters.stretches [thisIndex] = fromParameters.stretches [otherIndex].Copy ();
			}
		}

		for (int otherIndex = 0; otherIndex < fromParameters.bends.Count; otherIndex++) {
			int thisIndex = toParameters.IndexBend (fromParameters.bends [otherIndex]);
			if (thisIndex == -1) {
				toParameters.bends.Add (fromParameters.bends [otherIndex].Copy ());
			} else if (replace) {
				toParameters.bends [thisIndex] = fromParameters.bends [otherIndex].Copy ();
			}
		}

		for (int otherIndex = 0; otherIndex < fromParameters.torsions.Count; otherIndex++) {
			int thisIndex = toParameters.IndexTorsion (fromParameters.torsions [otherIndex]);
			if (thisIndex == -1) {
				toParameters.torsions.Add (fromParameters.torsions [otherIndex].Copy ());
			} else if (replace) {
				toParameters.torsions [thisIndex] = fromParameters.torsions [otherIndex].Copy ();
			}
		}

		for (int otherIndex = 0; otherIndex < fromParameters.improperTorsions.Count; otherIndex++) {
			int thisIndex = toParameters.IndexImproperTorsion (fromParameters.improperTorsions [otherIndex]);
			if (thisIndex == -1) {
				toParameters.improperTorsions.Add (fromParameters.improperTorsions [otherIndex].Copy ());
			} else if (replace) {
				toParameters.improperTorsions [thisIndex] = fromParameters.improperTorsions [otherIndex].Copy ();
			}
		}

	}

	public void UpdateParameters(Parameters updateFrom, bool replace=false) {

		if (replace)
			nonbonding = updateFrom.nonbonding;
		
		for (int otherIndex = 0; otherIndex < updateFrom.atomicParameters.Count; otherIndex++) {
			int thisIndex = IndexAtomicParameter (updateFrom.atomicParameters [otherIndex]);
			if (thisIndex == -1) {
				atomicParameters.Add (updateFrom.atomicParameters [otherIndex].Copy ());
			} else if (replace) {
				atomicParameters [thisIndex] = updateFrom.atomicParameters [otherIndex].Copy ();
			}
		}

		for (int otherIndex = 0; otherIndex < updateFrom.stretches.Count; otherIndex++) {
			int thisIndex = IndexStretch (updateFrom.stretches [otherIndex]);
			if (thisIndex == -1) {
				stretches.Add (updateFrom.stretches [otherIndex].Copy ());
			} else if (replace) {
				stretches [thisIndex] = updateFrom.stretches [otherIndex].Copy ();
			}
		}

		for (int otherIndex = 0; otherIndex < updateFrom.bends.Count; otherIndex++) {
			int thisIndex = IndexBend (updateFrom.bends [otherIndex]);
			if (thisIndex == -1) {
				bends.Add (updateFrom.bends [otherIndex].Copy ());
			} else if (replace) {
				bends [thisIndex] = updateFrom.bends [otherIndex].Copy ();
			}
		}

		for (int otherIndex = 0; otherIndex < updateFrom.torsions.Count; otherIndex++) {
			int thisIndex = IndexTorsion (updateFrom.torsions [otherIndex]);
			if (thisIndex == -1) {
				torsions.Add (updateFrom.torsions [otherIndex].Copy ());
			} else if (replace) {
				torsions [thisIndex] = updateFrom.torsions [otherIndex].Copy ();
			}
		}

		for (int otherIndex = 0; otherIndex < updateFrom.improperTorsions.Count; otherIndex++) {
			int thisIndex = IndexImproperTorsion (updateFrom.improperTorsions [otherIndex]);
			if (thisIndex == -1) {
				improperTorsions.Add (updateFrom.improperTorsions [otherIndex].Copy ());
			} else if (replace) {
				improperTorsions [thisIndex] = updateFrom.improperTorsions [otherIndex].Copy ();
			}
		}

	}

	public IEnumerator FromPRMFile(string filename) {
		return PRMReader.ParametersFromPRMFile(filename, this);
	}

	public IEnumerator FromFRCMODFile(string filename) {
		return FRCMODReader.ParametersFromFRCMODFile(filename, this);
	}

	public IEnumerator Calculate() {

        NotificationBar.SetTaskProgress(TID.CALCULATE_PARAMETERS, 0f);
        yield return null;

		string mol2Path = Settings.parmchkCalcPath + ".mol2";
		string frcmodPath = Settings.parmchkCalcPath + ".frcmod";

		yield return MOL2Writer.WriteMol2File(parent, mol2Path);
		string command = string.Format(
                "{0} {1} -f mol2 -i {2} -o {3}",
                Settings.parmchkCommand,
                string.Join(" ", Settings.parmchkOptions),
                mol2Path,
				frcmodPath
		);

		Bash.ProcessResult result = new Bash.ProcessResult();
		IEnumerator processEnumerator = Bash.ExecuteShellCommand(command, result, logOutput:true, logError:true);

		float progress = 0.1f;
		float waitTime = (float)parent.size / 5000;
		while (processEnumerator.MoveNext()) {
			NotificationBar.SetTaskProgress(TID.CALCULATE_PARAMETERS, progress);
			//Show that external command is running
			progress = progress < 0.9f? progress + 0.01f : progress;
			yield return new WaitForSeconds(waitTime);
		}
		

		if (result.ExitCode != 0) {
			System.IO.File.Copy(mol2Path, Settings.parmchkCalcPath + "_failed.mol2", true);
			CustomLogger.LogFormat(
				EL.ERROR,
				"{0} failed!", 
				Settings.parmchkCommand
			);
			NotificationBar.ClearTask(TID.CALCULATE_PARAMETERS);
			yield break;
		} else {
			//CustomLogger.LogFormat(
			//	EL.INFO,
			//	result.Output
			//);
		}

		yield return FromFRCMODFile(frcmodPath);

        NotificationBar.ClearTask(TID.CALCULATE_PARAMETERS);
	}

	public bool ContainsAtomicParameter(AtomicParameter other) => atomicParameters.Any(x => x.TypeEquivalent(other));
	public int IndexAtomicParameter(AtomicParameter other) => atomicParameters.FindIndex(x => x.TypeEquivalent(other));
		
	public bool ContainsStretch(Stretch other) => stretches.Any(x => x.TypeEquivalent(other));
	public int IndexStretch(Stretch other) => stretches.FindIndex(x => x.TypeEquivalent(other));

	public bool ContainsBend(Bend other) => bends.Any(x => x.TypeEquivalent(other));
	public int IndexBend(Bend other) => bends.FindIndex(x => x.TypeEquivalent(other));

	public bool ContainsTorsion(Torsion other) => torsions.Any(x => x.TypeEquivalent(other));
	public int IndexTorsion(Torsion other) => torsions.FindIndex(x => x.TypeEquivalent(other));

	public bool ContainsImproperTorsion(ImproperTorsion other) => improperTorsions.Any(x => x.TypeEquivalent(other));
	public int IndexImproperTorsion(ImproperTorsion other) => improperTorsions.FindIndex(x => x.TypeEquivalent(other));


	public string GetGaussianParamsStr() {
		System.Text.StringBuilder paramsSB = new System.Text.StringBuilder();

		paramsSB.Append(nonbonding.GetGaussianParamStr());
		foreach (Stretch stretch in stretches) 
			paramsSB.Append (stretch.GetGaussianParamStr ());
		foreach (Bend bend in bends) 
			paramsSB.Append (bend.GetGaussianParamStr ());
		foreach (Torsion torsion in torsions)
			paramsSB.Append (torsion.GetGaussianParamStr ());
		foreach (ImproperTorsion improperTorsion in improperTorsions) 
			paramsSB.Append (improperTorsion.GetGaussianParamStr ());
		foreach (AtomicParameter atomicParameter in atomicParameters)
			paramsSB.Append (atomicParameter.GetGaussianParamStr ());

		
		return paramsSB.ToString();
	}

	public void AddAtomicParameter(AtomicParameter newAtomicParameter) {
		int index = IndexAtomicParameter(newAtomicParameter);
		if (index != -1) {
			if (!newAtomicParameter.Equals(atomicParameters[index])) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Replacing {0} with {1}", 
					atomicParameters[index], 
					newAtomicParameter
				);
			}
		} else {
			atomicParameters.Add(newAtomicParameter);
		}
	}

	public void AddStretch(Stretch newStretch) {
		int index = IndexStretch(newStretch);
		if (index != -1) {
			if (!newStretch.Equals(stretches[index])) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Replacing {0} with {1}", 
					stretches[index], 
					newStretch
				);
			}
		} else {
			stretches.Add(newStretch);
		}
	}

	public void AddBend(Bend newBend) {
		int index = IndexBend(newBend);
		if (index != -1) {
			if (!newBend.Equals(bends[index])) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Replacing {0} with {1}", 
					bends[index], 
					newBend
				);
			}
		} else {
			bends.Add(newBend);
		}
	}

	public void AddTorsion(Torsion newTorsion) {
		int index = IndexTorsion(newTorsion);
		if (index != -1) {
			if (!newTorsion.Equals(torsions[index])) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Replacing {0} with {1}", 
					torsions[index], 
					newTorsion
				);
			}
		} else {
			torsions.Add(newTorsion);
		}
	}

	public void AddImproperTorsion(ImproperTorsion newImproperTorsion) {
		int index = IndexImproperTorsion(newImproperTorsion);
		if (index != -1) {
			if (!newImproperTorsion.Equals(improperTorsions[index])) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Replacing {0} with {1}", 
					improperTorsions[index], 
					newImproperTorsion
				);
			}
		} else {
			improperTorsions.Add(newImproperTorsion);
		}
	}

	public void SetNonBonding(VDWT vdwType=VDWT.AMBER, CT coulombType=CT.INVERSE, int vCutoff=0, int cCutoff=0, float vScale1=0f, float vScale2=0f, float vScale3=0.5f, float cScale1=0f, float cScale2=0f, float cScale3=-1.2f) {
		this.nonbonding = new NonBonding(vdwType, coulombType, vCutoff, cCutoff, vScale1, vScale2, vScale3, cScale1, cScale2, cScale3);
	}

}

public class AtomicParameter {
	public string type;
	public float radius;
	public float wellDepth;
	public float mass;

	public AtomicParameter(string type) {
		this.type = type;
	}

	public AtomicParameter(string type, float radius, float wellDepth, float mass) {
		this.type = type;
		this.radius = radius;
		this.wellDepth = wellDepth;
		this.mass = mass;
	}

	public bool TypeEquivalent(AtomicParameter other) => TypeEquivalent(other.type);
	public bool TypeEquivalent(string otherType) => TypeEquivalent(this.type, otherType);
	public static bool TypeEquivalent(AtomicParameter first, AtomicParameter second) => first.TypeEquivalent(second.type);
	public static bool TypeEquivalent(string firstType, string secondType) => (firstType == secondType || firstType == "*" || secondType == "*");
	

	public string GetGaussianParamStr() {
		return string.Format ("VDW {0,-2} {1,7:F4} {2,7:F4}{3}", type, radius, wellDepth, FileIO.newLine);
	}

	public override string ToString () {
		return string.Format ("VdW(type={0}, radius={1}, wellDepth={2}, mass={3}", type, radius, wellDepth, mass);
	}

	public AtomicParameter Copy() => new AtomicParameter (type, radius, wellDepth, mass);
	
}

public struct Stretch {

	public string[] types;
	public float req;
	public float keq;
	public int wildcardCount => types.Sum(x => (x == "*" ? 1 : 0));

	public Stretch(string t0, string t1, float req, float keq) {
		this.types = new string[2] {t0, t1};
		this.req = req;
		this.keq = keq;
	}

	public Stretch(string[] types, float req, float keq) {
		this.types = types;
		this.req = req;
		this.keq = keq;
	}

	public bool TypeEquivalent(Stretch other) => 
		this.types.SequenceEqual(other.types) || 
		this.types.SequenceEqual(other.types.Reverse());

	public string GetGaussianParamStr() {
		return string.Format (
			"HrmStr1 {0,-2} {1,-2} {2,7:F4} {3,7:F4}{4}", 
			types[0], 
			types[1], 
			keq, 
			req, 
			FileIO.newLine
		);
	}

	public override string ToString () {
		return string.Format (
			"Stretch(t0 = {0}, t1 = {1}, req = {2}, keq = {3})", 
			types[0], 
			types[1], 
			req, 
			keq
		);
	}

	public Stretch Copy() => new Stretch (types[0], types[1], req, keq);
	
	public bool IsDefault() {
		return types == null;
	}
}

public struct Bend {

	public string[] types;
	public float aeq;
	public float keq;
	public int wildcardCount => types.Sum(x => (x == "*" ? 1 : 0));

	public Bend(string t0, string t1, string t2, float aeq, float keq) {
		this.types = new string[3] {t0, t1, t2};
		this.aeq = aeq;
		this.keq = keq;
	}

	public Bend(string[] types, float aeq, float keq) {
		this.types = types;
		this.aeq = aeq;
		this.keq = keq;
	}

	public bool TypeEquivalent(Bend other) => 
		this.types.SequenceEqual(other.types) || 
		this.types.SequenceEqual(other.types.Reverse());

	public string GetGaussianParamStr() {
		return string.Format (
			"HrmBnd1 {0,-2} {1,-2} {2,-2} {3,7:F4} {4,7:F4}{5}", 
			types[0], 
			types[1], 
			types[2], 
			keq, 
			aeq, 
			FileIO.newLine
		);
	}

	public override string ToString () {
		return string.Format (
			"Bend(t0 = {0}, t1 = {1}, t2 = {2}, req = {3}, keq = {4})",
			types[0], 
			types[1], 
			types[2], 
			aeq, 
			keq
		);
	}

	public bool IsDefault() {
		return types == null;
	}

	public Bend Copy() => new Bend (types[0], types[1], types[2], aeq, keq);
	
}

public class Torsion {
	public string[] types;
	public float[] barrierHeights;
	public float[] phaseOffsets;
	public int npaths;
	public int wildcardCount => types.Sum(x => (x == "*" ? 1 : 0));

	public Torsion(string t0, string t1, string t2, string t3, float[] barrierHeights, float[] phaseOffsets, int npaths=0) {
		this.types = new string[4] {t0, t1, t2, t3};
		this.barrierHeights = barrierHeights;
		this.phaseOffsets = phaseOffsets;
		this.npaths = npaths;
	}

	public Torsion(string t0, string t1, string t2, string t3, float v0=0f, float v1=0f, float v2=0f, float v3=0f, float gamma0=0f, float gamma1=0f, float gamma2=0f, float gamma3=0f, int npaths=0) {
		this.types = new string[4] {t0, t1, t2, t3};
		this.barrierHeights = new float[4] {v0, v1, v2, v3};
		this.phaseOffsets = new float[4] {gamma0, gamma1, gamma2, gamma3};
		this.npaths = npaths;
	}

	public Torsion(string[] types, float[] barrierHeights, float[] phaseOffsets, int npaths=0) {
		this.types = types;
		this.barrierHeights = barrierHeights;
		this.phaseOffsets = phaseOffsets;
		this.npaths = npaths;
	}

	public Torsion(string[] types, float v0=0f, float v1=0f, float v2=0f, float v3=0f, float gamma0=0f, float gamma1=0f, float gamma2=0f, float gamma3=0f, int npaths=0) {
		this.types = types;
		this.barrierHeights = new float[4] {v0, v1, v2, v3};
		this.phaseOffsets = new float[4] {gamma0, gamma1, gamma2, gamma3};
		this.npaths = npaths;
	}

	public void Modify(int periodicity, float newBarrierHeight, float newPhaseOffset) {
		this.barrierHeights[periodicity] = newBarrierHeight;
		this.phaseOffsets[periodicity] = newPhaseOffset;
	}

	public bool TypeEquivalent(Torsion other) => TypeEquivalent(other.types);

	public bool TypeEquivalent(string[] otherTypes) {
		return (
			this.types
				.Zip(otherTypes, (x,y) => (x,y))
				.All(zipped => AtomicParameter.TypeEquivalent(zipped.x, zipped.y)
			) || 
			this.types
				.Zip(otherTypes.Reverse(), (x,y) => (x,y))
				.All(zipped => AtomicParameter.TypeEquivalent(zipped.x, zipped.y)
			)
		);
	}

	public string GetGaussianParamStr() {
		return string.Format(
			"AmbTrs {0,-2} {1,-2} {2,-2} {3,-2} {4,6:F0} {5,6:F0} {6,6:F0} {7,6:F0} {8,7:F1} {9,7:F1} {10,7:F1} {11,7:F1} {12,4:F1}{13}", 
			types[0], 
			types[1], 
			types[2], 
			types[3],  
			phaseOffsets[0], 
			phaseOffsets[1], 
			phaseOffsets[2], 
			phaseOffsets[3], 
			barrierHeights[0], 
			barrierHeights[1], 
			barrierHeights[2], 
			barrierHeights[3],
			npaths, 
			FileIO.newLine
		);
	}

	public override string ToString () {
		return string.Format (
			"Torsion(t0 = {0}, t1 = {1}, t2 = {2}, t3 = {3})", 
			types[0], 
			types[1], 
			types[2], 
			types[3]
		);
	}

	public Torsion Copy() => new Torsion (types[0], types[1], types[2], types[3], barrierHeights, phaseOffsets, npaths);
	
	public bool IsDefault() {
		return types == null;
	}
}

public class ImproperTorsion {
	public string[] types;
	public float barrierHeight;
	public float phaseOffset;
	public int periodicity;
	public int wildcardCount => types.Sum(x => (x == "*" ? 1 : 0));

	public ImproperTorsion(string t0, string t1, string t2, string t3, float barrierHeight, float phaseOffset, int periodicity) {
		this.types = new string[4] {t0, t1, t2, t3};
		this.barrierHeight = barrierHeight;
		this.phaseOffset = phaseOffset;
		this.periodicity = periodicity;
	}

	public ImproperTorsion(string[] types, float barrierHeight, float phaseOffset, int periodicity) {
		this.types = types;
		this.barrierHeight = barrierHeight;
		this.phaseOffset = phaseOffset;
		this.periodicity = periodicity;
	}

	public bool TypeEquivalent(ImproperTorsion other) => TypeEquivalent(other.types);

	public bool TypeEquivalent(string[] otherTypes) {
		return (
			this.types
				.Zip(otherTypes, (x,y) => (x,y))
				.All(zipped => AtomicParameter.TypeEquivalent(zipped.x, zipped.y)
			) || 
			this.types
				.Zip(otherTypes.Reverse(), (x,y) => (x,y))
				.All(zipped => AtomicParameter.TypeEquivalent(zipped.x, zipped.y)
			)
		);
	}

	public string GetGaussianParamStr() {
		return string.Format (
			"ImpTrs {0,-2} {1,-2} {2,-2} {3,-2} {4,7:F4} {5,6:F1} {6,4:F1}{7}", 
			types[0], 
			types[1], 
			types[2], 
			types[3], 
			barrierHeight, 
			phaseOffset, 
			periodicity, 
			FileIO.newLine
		);
	}

	public override string ToString () {
		return string.Format (
			"ImproperTorsion(t0 = {0}, t1 = {1}, t2 = {2}, t3 = {3})", 
			types[0], 
			types[1], 
			types[2], 
			types[3]
		);
	}

	public ImproperTorsion Copy() => new ImproperTorsion (types[0], types[1], types[2], types[3], barrierHeight, phaseOffset, periodicity);
	
	public bool IsDefault() {
		return types == null;
	}
}

public class NonBonding {
	public VDWT vdwType;
	public CT coulombType;
	public int vCutoff;
	public int cCutoff;
	public float[] vScales;
	public float[] cScales;

	public NonBonding(VDWT vdwType=VDWT.AMBER, CT coulombType=CT.INVERSE, int vCutoff=0, int cCutoff=0, float vScale1=0f, float vScale2=0f, float vScale3=0.5f, float cScale1=0f, float cScale2=0f, float cScale3=-1.2f) {
		this.vdwType = vdwType;
		this.coulombType = coulombType;
		this.vCutoff = vCutoff;
		this.cCutoff = cCutoff;

		/* 
		These are the scale factors for VdW and Coulombic interactions:
		Index 0: All interactions not listed below
		Index 1: Interactions 1 atom away
		Index 2: Interactions 2 atoms away
		Index 3: Interactions 3 atoms away
		*/
		this.vScales = new float[4] {1f, vScale1, vScale2, vScale3};
		this.cScales = new float[4] {1f, cScale1, cScale2, cScale3};
	}

	public NonBonding Copy() => new NonBonding(vdwType, coulombType, vCutoff, cCutoff, vScales[1], vScales[2], vScales[3], cScales[1], cScales[2], cScales[3]);
	
	public string GetGaussianParamStr() {
		return string.Format (
			"NonBon {0:D} {1:D} {2:D} {3:D} {4,7:F4} {5,7:F4} {6,7:F4} {7,7:F4} {8,7:F4} {9,7:F4}{10}", 
			Constants.VanDerWaalsTypeIntMap[vdwType], 
			Constants.CoulombTypeIntMap[coulombType], 
			vCutoff, 
			cCutoff, 
			vScales[1], 
			vScales[2], 
			vScales[3], 
			cScales[1], 
			cScales[2], 
			cScales[3], 
			FileIO.newLine
		);
	}

	public override string ToString () {
		return string.Format (
			"NonBonding(vType = {0}, cType = {1}, vCutoff = {2}, cCutoff = {3}, vScale1 = {4}, vScale2 = {5}, vScale3 = {6}, cScale1 = {7}, cScale2 = {8}, cScale3 = {9})", 
			vdwType, 
			coulombType, 
			vCutoff, 
			cCutoff, 
			vScales[1], 
			vScales[2], 
			vScales[3], 
			cScales[1], 
			cScales[2], 
			cScales[3]
		);
	}
}

public class WildCardEqualityComparer : IEqualityComparer<string> {
	public bool Equals(string s0, string s1) {
		return s0 == s1 || s0 == "*" || s1 == "*";
	}
	public int GetHashCode(string s0) {
		return s0.GetHashCode();
	}
}