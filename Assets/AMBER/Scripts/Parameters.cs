using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TID = Constants.TaskID;
using VDWT = Constants.VanDerWaalsType;
using CT = Constants.CoulombType;
using EL = Constants.ErrorLevel;
using BT = Constants.BondType;
using Amber = Constants.Amber;
using System.Diagnostics;
using System.Linq;
using Unity.Mathematics;

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

	public static void UpdateParameters(Geometry fromParent, Geometry toParent, bool replace=false, bool skipInvalid=true) {

		Parameters updateTo = toParent.parameters;
		Parameters updateFrom = fromParent.parameters;
		
		updateTo.UpdateNonBonding(updateFrom);
		updateTo.UpdateAtomicParameters(updateFrom, replace, skipInvalid);
		updateTo.UpdateStretches(updateFrom, replace, skipInvalid);
		updateTo.UpdateBends(updateFrom, replace, skipInvalid);
		updateTo.UpdateTorsions(updateFrom, replace, skipInvalid);
		updateTo.UpdateImproperTorsions(updateFrom, replace, skipInvalid);

	}

	public void UpdateParameters(Parameters updateFrom, bool replace=false, bool skipInvalid=true) {
		UpdateNonBonding(updateFrom);
		UpdateAtomicParameters(updateFrom, replace, skipInvalid);
		UpdateStretches(updateFrom, replace, skipInvalid);
		UpdateBends(updateFrom, replace, skipInvalid);
		UpdateTorsions(updateFrom, replace, skipInvalid);
		UpdateImproperTorsions(updateFrom, replace, skipInvalid);
	}

	public void UpdateNonBonding(Parameters updateFrom) {
		nonbonding = updateFrom.nonbonding.Copy();
	}

	public void UpdateAtomicParameters(Parameters updateFrom, bool replace=false, bool skipInvalid=true) {
		for (int otherIndex = 0; otherIndex < updateFrom.atomicParameters.Count; otherIndex++) {
			AtomicParameter atomicParameter = updateFrom.atomicParameters [otherIndex];
			int thisIndex = IndexAtomicParameter(atomicParameter);
			if (atomicParameter.mass == 0f && skipInvalid) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Skipping invalid AtomicParameter: {0}",
					atomicParameter
				);
				continue;
			}
			if (thisIndex == -1) {
				atomicParameters.Add(atomicParameter.Copy());
			} else if (replace) {
				atomicParameters[thisIndex] = atomicParameter.Copy();
			}
		}
	}
	

	public void UpdateStretches(Parameters updateFrom, bool replace=false, bool skipInvalid=true) {
		for (int otherIndex = 0; otherIndex < updateFrom.stretches.Count; otherIndex++) {
			int thisIndex = IndexStretch (updateFrom.stretches [otherIndex]);
			Stretch stretch = updateFrom.stretches [otherIndex];
			if (stretch.req == 0f && skipInvalid) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Skipping invalid Stretch: {0}",
					stretch
				);
				continue;
			}
			if (thisIndex == -1) {
				stretches.Add (stretch.Copy());
			} else if (replace) {
				stretches [thisIndex] = stretch.Copy();
			}
		}
	}

	public void UpdateBends(Parameters updateFrom, bool replace=false, bool skipInvalid=true) {
		for (int otherIndex = 0; otherIndex < updateFrom.bends.Count; otherIndex++) {
			Bend bend = updateFrom.bends [otherIndex];
			int thisIndex = IndexBend (bend);
			if (bend.aeq == 0f && skipInvalid) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Skipping invalid Bend: {0}",
					bend
				);
				continue;
			}
			if (thisIndex == -1) {
				bends.Add (bend.Copy());
			} else if (replace) {
				bends[thisIndex] = bend.Copy();
			}
		}
	}
	
	public void UpdateTorsions(Parameters updateFrom, bool replace=false, bool skipInvalid=true) {
		for (int otherIndex = 0; otherIndex < updateFrom.torsions.Count; otherIndex++) {
			Torsion torsion = updateFrom.torsions[otherIndex];
			int thisIndex = IndexTorsion (torsion);
			if (torsion.npaths == 0 && skipInvalid) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Skipping invalid Torsion: {0}",
					torsion
				);
				continue;
			}
			if (thisIndex == -1) {
				torsions.Add (torsion.Copy());
			} else if (replace) {
				torsions [thisIndex] = torsion.Copy();
			}
		}
	}
	
	public void UpdateImproperTorsions(Parameters updateFrom, bool replace=false, bool skipInvalid=true) {
		for (int otherIndex = 0; otherIndex < updateFrom.improperTorsions.Count; otherIndex++) {
			ImproperTorsion improperTorsion = updateFrom.improperTorsions[otherIndex];
			int thisIndex = IndexImproperTorsion (improperTorsion);
			if (improperTorsion.barrierHeight == 0f && skipInvalid) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Skipping invalid Improper Torsion: {0}",
					improperTorsion
				);
				continue;
			}
			if (thisIndex == -1) {
				improperTorsions.Add (improperTorsion.Copy());
			} else if (replace) {
				improperTorsions [thisIndex] = improperTorsion.Copy();
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

		Bash.ExternalCommand externalCommand;
		try {
			externalCommand = Settings.GetExternalCommand("parameters");
		} catch (KeyNotFoundException e) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Could not find external command 'parameters'!"
			);
			CustomLogger.LogOutput(e.StackTrace);
			yield break;
		}

		externalCommand.SetSuffix("");
		yield return externalCommand.WriteInputAndExecute(
			parent,
			TID.CALCULATE_PARAMETERS,
			true,
			true,
			true,
			(float)parent.size / 5000
		);

        if (externalCommand.succeeded) {
			yield return FromFRCMODFile(externalCommand.GetOutputPath());
		}

        NotificationBar.ClearTask(TID.CALCULATE_PARAMETERS);
	}

	public IEnumerator Calculate2() {
		
        NotificationBar.SetTaskProgress(TID.CALCULATE_PARAMETERS, 0f);
        yield return null;

		Bash.ExternalCommand externalCommand;
		try {
			externalCommand = Settings.GetExternalCommand("parameters");
		} catch (KeyNotFoundException e) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Could not find external command 'parameters'!"
			);
			CustomLogger.LogOutput(e.StackTrace);
			NotificationBar.ClearTask(TID.CALCULATE_PARAMETERS);
			yield break;
		}


		//Loop through all connected groups
		foreach (ResidueID[] residueGroup in parent.GetGroupedResidues().Select(x => x.ToArray())) {

			//Loop through Residue IDs of group
			foreach (ResidueID residueID in residueGroup) {
				Residue residue;
				if (!parent.TryGetResidue(residueID, out residue)) {
					CustomLogger.LogFormat(
						EL.WARNING,
						"Cound not find Residue ID '{0}' in Geometry",
						residueID
					);
					continue;
				}

				//Create a subgroup of all the connected residues - stretches, bends, torsions etc can cross residues
				List<ResidueID> residueSubgroup = new List<ResidueID> {residueID};
				residueSubgroup.AddRange(residue.NeighbouringResidues());

				//Create a geometry from this - this also copies current set of parameters
				Geometry tempGeometry = parent.TakeResidues(residueSubgroup, transform);
				
				//Check if any parameters are missing 
				Parameters missingParameters = GetMissingParameters(tempGeometry, false);

				if (!missingParameters.IsEmpty()) {
					//Cap atoms
					yield return NonStandardResidueTools.CapAtoms(tempGeometry, parent, TID.CALCULATE_PARAMETERS);

					//Calculate Parameters

					externalCommand.SetSuffix(residueID.ToString());
					yield return externalCommand.WriteInputAndExecute(
						tempGeometry,
						TID.CALCULATE_PARAMETERS,
						true,
						true,
						true,
						(float)tempGeometry.size / 500
					);


					if (externalCommand.succeeded) {
						yield return missingParameters.FromFRCMODFile(
							externalCommand.GetOutputPath()
						);
						UpdateParameters(missingParameters);
					}
				}
			}

		}

		NotificationBar.ClearTask(TID.CALCULATE_PARAMETERS);

	}

	public bool ContainsAtomicParameter(AtomicParameter other, bool allowWild=false) {
		if (allowWild) {
			return atomicParameters.Any(x => x.TypeEquivalentOrWild(other));
		} else {
			return atomicParameters.Any(x => x.TypeEquivalent(other));
		}
	}
	public int IndexAtomicParameter(AtomicParameter other, bool allowWild=false) {
		if (allowWild) {
			return atomicParameters.FindIndex(x => x.TypeEquivalentOrWild(other));
		} else {
			return atomicParameters.FindIndex(x => x.TypeEquivalent(other));
		}
	}

	public bool ContainsAtomicParameter(Amber otherType, bool allowWild=false) {
		if (allowWild) {
			return atomicParameters.Any(x => x.TypeEquivalentOrWild(otherType));
		} else {
			return atomicParameters.Any(x => x.TypeEquivalent(otherType));
		}
	}
	public int IndexAtomicParameter(Amber otherType, bool allowWild=false) {
		if (allowWild) {
			return atomicParameters.FindIndex(x => x.TypeEquivalentOrWild(otherType));
		} else {
			return atomicParameters.FindIndex(x => x.TypeEquivalent(otherType));
		}
	}
		
	public bool ContainsStretch(Stretch other, bool allowWild=false) {
		if (allowWild) {
			return stretches.Any(x => x.TypeEquivalentOrWild(other));
		} else {
			return stretches.Any(x => x.TypeEquivalent(other));
		}
	}
	public int IndexStretch(Stretch other, bool allowWild=false) {
		if (allowWild) {
			return stretches.FindIndex(x => x.TypeEquivalentOrWild(other));
		} else {
			return stretches.FindIndex(x => x.TypeEquivalent(other));
		}
	}
		
	public bool ContainsStretch(
		Amber otherType0, 
		Amber otherType1,
		bool allowWild=false
	) {
		if (allowWild) {
			return stretches.Any(x => x.TypeEquivalentOrWild(new Amber[2] {otherType0, otherType1}));
		} else {
			return stretches.Any(x => x.TypeEquivalent(new Amber[2] {otherType0, otherType1}));
		}
	}
	public int IndexStretch(
		Amber otherType0, 
		Amber otherType1,
		bool allowWild=false
	) {
		if (allowWild) {
			return stretches.FindIndex(x => x.TypeEquivalentOrWild(new Amber[2] {otherType0, otherType1}));
		} else {
			return stretches.FindIndex(x => x.TypeEquivalent(new Amber[2] {otherType0, otherType1}));
		}
	}

	public bool ContainsBend(Bend other, bool allowWild=false) {
		if (allowWild) {
			return bends.Any(x => x.TypeEquivalentOrWild(other));
		} else {
			return bends.Any(x => x.TypeEquivalent(other));
		}
	}
	public int IndexBend(Bend other, bool allowWild=false) {
		if (allowWild) {
			return bends.FindIndex(x => x.TypeEquivalentOrWild(other));
		} else {
			return bends.FindIndex(x => x.TypeEquivalent(other));
		}
	}

	public bool ContainsBend(
		Amber otherType0, 
		Amber otherType1, 
		Amber otherType2,
		bool allowWild=false
	) {
		if (allowWild) {
			return bends.Any(x => x.TypeEquivalentOrWild(new Amber[3] {otherType0, otherType1, otherType2}));
		} else {
			return bends.Any(x => x.TypeEquivalent(new Amber[3] {otherType0, otherType1, otherType2}));
		}
	}
	public int IndexBend(
		Amber otherType0, 
		Amber otherType1, 
		Amber otherType2,
		bool allowWild=false
	) {
		if (allowWild) {
			return bends.FindIndex(x => x.TypeEquivalentOrWild(new Amber[3] {otherType0, otherType1, otherType2}));
		} else {
			return bends.FindIndex(x => x.TypeEquivalent(new Amber[3] {otherType0, otherType1, otherType2}));
		}
	}

	public bool ContainsTorsion(Torsion other, bool allowWild=false) {
		if (allowWild) {
			return torsions.Any(x => x.TypeEquivalentOrWild(other));
		} else {
			return torsions.Any(x => x.TypeEquivalent(other));
		}
	}
	public int IndexTorsion(Torsion other, bool allowWild=false) {
		if (allowWild) {
			return torsions.FindIndex(x => x.TypeEquivalentOrWild(other));
		} else {
			return torsions.FindIndex(x => x.TypeEquivalent(other));
		}
	}

	public bool ContainsTorsion(
		Amber otherType0, 
		Amber otherType1, 
		Amber otherType2,
		Amber otherType3,
		bool allowWild=false
	) {
		if (allowWild) {
			return torsions.Any(x => x.TypeEquivalentOrWild(new Amber[4] {otherType0, otherType1, otherType2, otherType3}));
		} else {
			return torsions.Any(x => x.TypeEquivalent(new Amber[4] {otherType0, otherType1, otherType2, otherType3}));
		}
	}
	public int IndexTorsion(
		Amber otherType0, 
		Amber otherType1, 
		Amber otherType2,
		Amber otherType3,
		bool allowWild=false
	) {
		if (allowWild) {
			return torsions.FindIndex(x => x.TypeEquivalentOrWild(new Amber[4] {otherType0, otherType1, otherType2, otherType3}));
		} else {
			return torsions.FindIndex(x => x.TypeEquivalent(new Amber[4] {otherType0, otherType1, otherType2, otherType3}));
		}
	}

	public bool ContainsImproperTorsion(ImproperTorsion other, bool allowWild=false) {
		if (allowWild) {
		return improperTorsions.Any(x => x.TypeEquivalentOrWild(other));
		} else {
		return improperTorsions.Any(x => x.TypeEquivalent(other));
		}
	}
	public int IndexImproperTorsion(ImproperTorsion other, bool allowWild=false) {
		if (allowWild) {
		return improperTorsions.FindIndex(x => x.TypeEquivalentOrWild(other));
		} else {
		return improperTorsions.FindIndex(x => x.TypeEquivalent(other));
		}
	}

	public bool ContainsImproperTorsion(
		Amber otherType0, 
		Amber otherType1, 
		Amber otherType2,
		Amber otherType3,
		bool allowWild=false
	) {
		if (allowWild) {
			return improperTorsions.Any(x => x.TypeEquivalentOrWild(new Amber[4] {otherType0, otherType1, otherType2, otherType3}));
		} else {
			return improperTorsions.Any(x => x.TypeEquivalent(new Amber[4] {otherType0, otherType1, otherType2, otherType3}));
		}
	}
	public int IndexImproperTorsion(
		Amber otherType0, 
		Amber otherType1, 
		Amber otherType2,
		Amber otherType3,
		bool allowWild=false
	) {
		if (allowWild) {
			return improperTorsions.FindIndex(x => x.TypeEquivalentOrWild(new Amber[4] {otherType0, otherType1, otherType2, otherType3}));
		} else {
			return improperTorsions.FindIndex(x => x.TypeEquivalent(new Amber[4] {otherType0, otherType1, otherType2, otherType3}));
		}
	}

	public bool IsEmpty() {
		if (atomicParameters.Count > 0) {return false;}
		if (stretches.Count > 0) {return false;}
		if (bends.Count > 0) {return false;}
		if (torsions.Count > 0) {return false;}
		if (improperTorsions.Count > 0) {return false;}
		return true;
	}

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
					EL.INFO,
					"Replacing {0} with {1}", 
					atomicParameters[index], 
					newAtomicParameter
				);
				atomicParameters[index] = newAtomicParameter;
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
					EL.INFO,
					"Replacing {0} with {1}", 
					stretches[index], 
					newStretch
				);
				stretches[index] = newStretch;
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
					EL.INFO,
					"Replacing {0} with {1}", 
					bends[index], 
					newBend
				);
				bends[index] = newBend;
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
					EL.INFO,
					"Replacing {0} with {1}", 
					torsions[index], 
					newTorsion
				);
				torsions[index] = newTorsion;
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
					EL.INFO,
					"Replacing {0} with {1}", 
					improperTorsions[index], 
					newImproperTorsion
				);
				improperTorsions[index] = newImproperTorsion;
			}
		} else {
			improperTorsions.Add(newImproperTorsion);
		}
	}

	public void SetNonBonding(VDWT vdwType=VDWT.AMBER, CT coulombType=CT.INVERSE, int vCutoff=0, int cCutoff=0, float vScale1=0f, float vScale2=0f, float vScale3=0.5f, float cScale1=0f, float cScale2=0f, float cScale3=-1.2f) {
		this.nonbonding = new NonBonding(vdwType, coulombType, vCutoff, cCutoff, vScale1, vScale2, vScale3, cScale1, cScale2, cScale3);
	}

	public static Parameters GetMissingParameters(Geometry geometry, bool checkMissingAtoms) {

		List<AtomID> missingAtoms = new List<AtomID>();
		List<AtomID> missingAmbers = new List<AtomID>();

		Atom GetAtom(AtomID atomID) {
			Atom atom;
			if (!geometry.TryGetAtom(atomID, out atom)) {
				if (!missingAtoms.Contains(atomID) && checkMissingAtoms) {
					CustomLogger.LogFormat(
						EL.WARNING,
						"Could not find AtomID '{0}' when getting missing Parameters.",
						atomID
					);
					missingAtoms.Add(atomID);
				}
			}
			return atom;
		}

		bool TryGetAmber(Atom atom, AtomID atomID, out Amber amber) {
			amber = atom.amber;
			if (amber == Amber.X) {
				if (!missingAmbers.Contains(atomID)) {
					CustomLogger.LogFormat(
						EL.WARNING,
						"Could not find AtomID '{0}' when getting missing Parameters.",
						atomID
					);
					missingAmbers.Add(atomID);
				}
				return false;
			}
			return true;
		}

		Parameters currentParameters = geometry.parameters;
		Parameters missingParameters = PrefabManager.InstantiateParameters(null);

		foreach ((AtomID atomID0, Atom atom0) in geometry.EnumerateAtomIDPairs()) {
			Amber amber0;
			if (!TryGetAmber(atom0, atomID0, out amber0)) {continue;}

			//Check Atomic Type
			if (
				!currentParameters.ContainsAtomicParameter(amber0) &&
				!missingParameters.ContainsAtomicParameter(amber0)
			) {
				AtomicParameter atomicParameter = new AtomicParameter(amber0);
				CustomLogger.LogFormat(
					EL.INFO,
					"Found missing Atomic Parameter: {0}",
					atomicParameter.ToString()
				);
				missingParameters.AddAtomicParameter(atomicParameter);
			}

            AtomID[] atom0Neighbours = atom0.EnumerateConnections()
                .Select(x => x.Item1)
                .ToArray();


			int numNeighbours = atom0Neighbours.Length;
			for (int index = 0; index < numNeighbours; index++) {
				AtomID atomID1 = atom0Neighbours[index];
				Atom atom1;
				if ((atom1 = GetAtom(atomID1)) == null) {continue;}
				Amber amber1;
				if (!TryGetAmber(atom1, atomID1, out amber1)) {continue;}

				//Check Stretch
				if (
					!currentParameters.ContainsStretch(amber0, amber1, true) &&
					!missingParameters.ContainsStretch(amber0, amber1)
				) {
					Stretch stretch = new Stretch(amber0, amber1);
					CustomLogger.LogFormat(
						EL.INFO,
						"Found missing Stretch Parameter: {0}",
						stretch.ToString()
					);
					missingParameters.AddStretch(stretch);
				}

				foreach ((AtomID atomID2, BT bondType2) in atom1.EnumerateConnections()) {
					if (atomID2 == atomID0) {continue;}
					Atom atom2;
					if ((atom2 = GetAtom(atomID2)) == null) {continue;}
					Amber amber2;
					if (!TryGetAmber(atom2, atomID2, out amber2)) {continue;}

					//Check Bend
					if (
						!currentParameters.ContainsBend(amber0, amber1, amber2, true) &&
						!missingParameters.ContainsBend(amber0, amber1, amber2)
					) {
						Bend bend = new Bend(amber0, amber1, amber2);
						CustomLogger.LogFormat(
							EL.INFO,
							"Found missing Bend Parameter: {0}",
							bend.ToString()
						);
						missingParameters.AddBend(bend);
					}

					foreach ((AtomID atomID3, BT bondType3) in atom2.EnumerateConnections()) {
						if (atomID3 == atomID0 || atomID3 == atomID1) {continue;}
						Atom atom3;
						if ((atom3 = GetAtom(atomID3)) == null) {continue;}
						Amber amber3;
						if (!TryGetAmber(atom3, atomID3, out amber3)) {continue;}

						//Check Torsion
						if (
							!currentParameters.ContainsTorsion(amber0, amber1, amber2, amber3, true) &&
							!missingParameters.ContainsTorsion(amber0, amber1, amber2, amber3)
						) {
							Torsion torsion = new Torsion(amber0, amber1, amber2, amber3);
							CustomLogger.LogFormat(
								EL.INFO,
								"Found missing Torsion Parameter: {0}",
								torsion.ToString()
							);
							missingParameters.AddTorsion(torsion);
						}
					}
				}
			}
		}
		return missingParameters;
	}
}

public class AtomicParameter {
	public Amber type;
	public float radius;
	public float wellDepth;
	public float mass;

	public AtomicParameter(Amber type) {
		this.type = type;
	}

	public AtomicParameter(Amber type, float radius, float wellDepth, float mass) {
		this.type = type;
		this.radius = radius;
		this.wellDepth = wellDepth;
		this.mass = mass;
	}

	public bool TypeEquivalent(AtomicParameter other) => TypeEquivalent(other.type);
	public bool TypeEquivalent(Amber otherType) => TypeEquivalent(this.type, otherType);
	public static bool TypeEquivalent(AtomicParameter first, AtomicParameter second) => first.TypeEquivalent(second.type);
	public static bool TypeEquivalent(Amber firstType, Amber secondType) => (firstType == secondType);
	public bool TypeEquivalentOrWild(AtomicParameter other) => TypeEquivalentOrWild(other.type);
	public bool TypeEquivalentOrWild(Amber otherType) => TypeEquivalentOrWild(this.type, otherType);
	public static bool TypeEquivalentOrWild(AtomicParameter first, AtomicParameter second) => first.TypeEquivalentOrWild(second.type);
	public static bool TypeEquivalentOrWild(Amber firstType, Amber secondType) => (firstType == secondType || firstType == Amber._ || secondType == Amber._);
	

	public string GetGaussianParamStr() {
		return string.Format ("VDW {0,-2} {1,7:F4} {2,7:F4}{3}", AmberCalculator.GetAmberString(type), radius, wellDepth, FileIO.newLine);
	}

	public override string ToString () {
		return string.Format ("VdW(type={0}, radius={1}, wellDepth={2}, mass={3})", AmberCalculator.GetAmberString(type), radius, wellDepth, mass);
	}

	public AtomicParameter Copy() => new AtomicParameter (type, radius, wellDepth, mass);
	
}

public struct Stretch {

	public Amber[] types;
	public float req;
	public float keq;
	public int wildcardCount => types.Sum(x => (x == Amber._ ? 1 : 0));

	public Stretch(Amber t0, Amber t1, float req=0f, float keq=0f) {
		this.types = new Amber[2] {t0, t1};
		this.req = req;
		this.keq = keq;
	}

	public Stretch(Amber[] types, float req=0f, float keq=0f) {
		if (types.Length != 2) {
			throw new System.Exception(string.Format(
				"Wrong length of 'types' for Stretch ({0} - should be {1})",
				types.Length,
				2
			));
		}
		this.types = types;
		this.req = req;
		this.keq = keq;
	}

	public Stretch(string typesString, float req=0f, float keq=0f) {
		types = AmberCalculator.GetAmbers(typesString);
		if (types.Length != 2) {
			throw new System.Exception(string.Format(
				"Wrong length of 'types' for Stretch ({0} - should be {1})",
				types.Length,
				2
			));
		}
		this.req = req;
		this.keq = keq;
	}

	public bool TypeEquivalent(Stretch other) => TypeEquivalent(other.types);

	public bool TypeEquivalent(Amber[] otherTypes) {
		bool forwardEqual = true;
		bool reverseEqual = true;
		for (int i=0, j=1; i<2; i++, j--) {
			Amber type = types[i];
			forwardEqual &= AtomicParameter.TypeEquivalent(type, otherTypes[i]);
			reverseEqual &= AtomicParameter.TypeEquivalent(type, otherTypes[j]);
			if (!(forwardEqual || reverseEqual)) {
				return false;
			}
		}
		return true;
	}

	public bool TypeEquivalentOrWild(Stretch other) => TypeEquivalentOrWild(other.types);

	public bool TypeEquivalentOrWild(Amber[] otherTypes) {
		bool forwardEqual = true;
		bool reverseEqual = true;
		for (int i=0, j=1; i<2; i++, j--) {
			Amber type = types[i];
			forwardEqual &= AtomicParameter.TypeEquivalentOrWild(type, otherTypes[i]);
			reverseEqual &= AtomicParameter.TypeEquivalentOrWild(type, otherTypes[j]);
			if (!(forwardEqual || reverseEqual)) {
				return false;
			}
		}
		return true;
	}

	public string GetGaussianParamStr() {
		return string.Format (
			"HrmStr1 {0,-2} {1,-2} {2,7:F4} {3,7:F4}{4}", 
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1]), 
			keq, 
			req, 
			FileIO.newLine
		);
	}

	public override string ToString () {
		return string.Format (
			"Stretch({0}-{1}, req = {2}, keq = {3})", 
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1]), 
			req, 
			keq
		);
	}

	public string GetTypesString() {
		return string.Format (
			"{0}-{1}", 
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1])
		);
	}

	public Stretch Copy() => new Stretch (types[0], types[1], req, keq);
	
	public bool IsDefault() {
		return types == null;
	}
}

public struct Bend {

	public Amber[] types;
	public float aeq;
	public float keq;
	public int wildcardCount => types.Sum(x => (x == Amber._ ? 1 : 0));

	public Bend(Amber t0, Amber t1, Amber t2, float aeq=0f, float keq=0f) {
		this.types = new Amber[3] {t0, t1, t2};
		this.aeq = aeq;
		this.keq = keq;
	}

	public Bend(Amber[] types, float aeq, float keq) {
		if (types.Length != 3) {
			throw new System.Exception(string.Format(
				"Wrong length of 'types' for Bend ({0} - should be {1})",
				types.Length,
				3
			));
		}
		this.types = types;
		this.aeq = aeq;
		this.keq = keq;
	}

	public Bend(string typesString, float aeq, float keq) {
		types = AmberCalculator.GetAmbers(typesString);
		if (types.Length != 3) {
			throw new System.Exception(string.Format(
				"Wrong length of 'types' for Bend ({0} - should be {1})",
				types.Length,
				3
			));
		}
		this.aeq = aeq;
		this.keq = keq;
	}

	public bool TypeEquivalent(Bend other) => TypeEquivalent(other.types);

	public bool TypeEquivalent(Amber[] otherTypes) {
		bool forwardEqual = true;
		bool reverseEqual = true;
		for (int i=0, j=2; i<3; i++, j--) {
			Amber type = types[i];
			forwardEqual &= AtomicParameter.TypeEquivalent(type, otherTypes[i]);
			reverseEqual &= AtomicParameter.TypeEquivalent(type, otherTypes[j]);
			if (!(forwardEqual || reverseEqual)) {
				return false;
			}
		}
		return true;
	}

	public bool TypeEquivalentOrWild(Bend other) => TypeEquivalentOrWild(other.types);

	public bool TypeEquivalentOrWild(Amber[] otherTypes) {
		bool forwardEqual = true;
		bool reverseEqual = true;
		for (int i=0, j=2; i<3; i++, j--) {
			Amber type = types[i];
			forwardEqual &= AtomicParameter.TypeEquivalentOrWild(type, otherTypes[i]);
			reverseEqual &= AtomicParameter.TypeEquivalentOrWild(type, otherTypes[j]);
			if (!(forwardEqual || reverseEqual)) {
				return false;
			}
		}
		return true;
	}

	public string GetGaussianParamStr() {
		return string.Format (
			"HrmBnd1 {0,-2} {1,-2} {2,-2} {3,7:F4} {4,7:F4}{5}", 
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1]), 
			AmberCalculator.GetAmberString(types[2]), 
			keq, 
			aeq, 
			FileIO.newLine
		);
	}

	public override string ToString () {
		return string.Format (
			"Bend({0}-{1}-{2}, req = {3}, keq = {4})",
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1]), 
			AmberCalculator.GetAmberString(types[2]), 
			aeq, 
			keq
		);
	}

	public string GetTypesString() {
		return string.Format (
			"{0}-{1}-{2}", 
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1]),
			AmberCalculator.GetAmberString(types[2])
		);
	}

	public bool IsDefault() {
		return types == null;
	}

	public Bend Copy() => new Bend (types[0], types[1], types[2], aeq, keq);
	
}

public class Torsion {
	public Amber[] types;
	public float[] barrierHeights;
	public float[] phaseOffsets;
	public int npaths;
	public int wildcardCount => types.Sum(x => (x == Amber._ ? 1 : 0));

	public Torsion(Amber t0, Amber t1, Amber t2, Amber t3, float[] barrierHeights, float[] phaseOffsets, int npaths=0) {
		this.types = new Amber[4] {t0, t1, t2, t3};
		this.barrierHeights = barrierHeights;
		this.phaseOffsets = phaseOffsets;
		this.npaths = npaths;
	}

	public Torsion(Amber t0, Amber t1, Amber t2, Amber t3, float v0=0f, float v1=0f, float v2=0f, float v3=0f, float gamma0=0f, float gamma1=0f, float gamma2=0f, float gamma3=0f, int npaths=0) {
		this.types = new Amber[4] {t0, t1, t2, t3};
		this.barrierHeights = new float[4] {v0, v1, v2, v3};
		this.phaseOffsets = new float[4] {gamma0, gamma1, gamma2, gamma3};
		this.npaths = npaths;
	}

	public Torsion(Amber[] types, float[] barrierHeights, float[] phaseOffsets, int npaths=0) {
		if (types.Length != 4) {
			throw new System.Exception(string.Format(
				"Wrong length of 'types' for Torsion ({0} - should be {1})",
				types.Length,
				4
			));
		}
		this.types = types;
		this.barrierHeights = barrierHeights;
		this.phaseOffsets = phaseOffsets;
		this.npaths = npaths;
	}

	public Torsion(Amber[] types, float v0=0f, float v1=0f, float v2=0f, float v3=0f, float gamma0=0f, float gamma1=0f, float gamma2=0f, float gamma3=0f, int npaths=0) {
		if (types.Length != 4) {
			throw new System.Exception(string.Format(
				"Wrong length of 'types' for Torsion ({0} - should be {1})",
				types.Length,
				4
			));
		}
		this.types = types;
		this.barrierHeights = new float[4] {v0, v1, v2, v3};
		this.phaseOffsets = new float[4] {gamma0, gamma1, gamma2, gamma3};
		this.npaths = npaths;
	}

	public Torsion(string typesString, float[] barrierHeights, float[] phaseOffsets, int npaths=0) {
		types = AmberCalculator.GetAmbers(typesString);
		if (types.Length != 4) {
			throw new System.Exception(string.Format(
				"Wrong length of 'types' for Torsion ({0} - should be {1})",
				types.Length,
				4
			));
		}
		this.barrierHeights = barrierHeights;
		this.phaseOffsets = phaseOffsets;
		this.npaths = npaths;
	}

	public Torsion(string typesString, float v0=0f, float v1=0f, float v2=0f, float v3=0f, float gamma0=0f, float gamma1=0f, float gamma2=0f, float gamma3=0f, int npaths=0) {
		types = AmberCalculator.GetAmbers(typesString);
		if (types.Length != 4) {
			throw new System.Exception(string.Format(
				"Wrong length of 'types' for Torsion ({0} - should be {1})",
				types.Length,
				4
			));
		}
		this.barrierHeights = new float[4] {v0, v1, v2, v3};
		this.phaseOffsets = new float[4] {gamma0, gamma1, gamma2, gamma3};
		this.npaths = npaths;
	}

	public void Modify(int periodicity, float newBarrierHeight, float newPhaseOffset) {
		this.barrierHeights[periodicity] = newBarrierHeight;
		this.phaseOffsets[periodicity] = newPhaseOffset;
	}

	public bool TypeEquivalent(Torsion other) => TypeEquivalent(other.types);

	public bool TypeEquivalent(Amber[] otherTypes) {
		bool forwardEqual = true;
		bool reverseEqual = true;
		for (int i=0, j=3; i<4; i++, j--) {
			Amber type = types[i];
			forwardEqual &= AtomicParameter.TypeEquivalent(type, otherTypes[i]);
			reverseEqual &= AtomicParameter.TypeEquivalent(type, otherTypes[j]);
			if (!(forwardEqual || reverseEqual)) {
				return false;
			}
		}
		return true;
	}

	public bool TypeEquivalentOrWild(Torsion other) => TypeEquivalentOrWild(other.types);

	public bool TypeEquivalentOrWild(Amber[] otherTypes) {
		bool forwardEqual = true;
		bool reverseEqual = true;
		for (int i=0, j=3; i<4; i++, j--) {
			Amber type = types[i];
			forwardEqual &= AtomicParameter.TypeEquivalentOrWild(type, otherTypes[i]);
			reverseEqual &= AtomicParameter.TypeEquivalentOrWild(type, otherTypes[j]);
			if (!(forwardEqual || reverseEqual)) {
				return false;
			}
		}
		return true;
	}

	public string GetGaussianParamStr() {
		return string.Format(
			"AmbTrs {0,-2} {1,-2} {2,-2} {3,-2} {4,6:F0} {5,6:F0} {6,6:F0} {7,6:F0} {8,7:F1} {9,7:F1} {10,7:F1} {11,7:F1} {12,4:F1}{13}", 
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1]), 
			AmberCalculator.GetAmberString(types[2]), 
			AmberCalculator.GetAmberString(types[3]),  
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
			"Torsion({0}-{1}-{2}-{3})", 
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1]), 
			AmberCalculator.GetAmberString(types[2]), 
			AmberCalculator.GetAmberString(types[3])
		);
	}

	public string GetTypesString() {
		return string.Format (
			"{0}-{1}-{2}-{3}", 
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1]),
			AmberCalculator.GetAmberString(types[2]),
			AmberCalculator.GetAmberString(types[3])
		);
	}

	public Torsion Copy() => new Torsion (types[0], types[1], types[2], types[3], barrierHeights, phaseOffsets, npaths);
	
	public bool IsDefault() {
		return types == null;
	}
}

public class ImproperTorsion {
	public Amber[] types;
	public float barrierHeight;
	public float phaseOffset;
	public int periodicity;
	public int wildcardCount => types.Sum(x => (x == Amber._ ? 1 : 0));

	public ImproperTorsion(Amber t0, Amber t1, Amber t2, Amber t3, float barrierHeight=0f, float phaseOffset=0f, int periodicity=1) {
		this.types = new Amber[4] {t0, t1, t2, t3};
		this.barrierHeight = barrierHeight;
		this.phaseOffset = phaseOffset;
		this.periodicity = periodicity;
	}

	public ImproperTorsion(Amber[] types, float barrierHeight=0f, float phaseOffset=0f, int periodicity=0) {
		this.types = types;
		this.barrierHeight = barrierHeight;
		this.phaseOffset = phaseOffset;
		this.periodicity = periodicity;
	}

	public ImproperTorsion(string typesString, float barrierHeight=0f, float phaseOffset=0f, int periodicity=0) {
		types = AmberCalculator.GetAmbers(typesString);
		if (types.Length != 4) {
			throw new System.Exception(string.Format(
				"Wrong length of 'types' for Improper Torsion ({0} - should be {1})",
				types.Length,
				4
			));
		}
		this.barrierHeight = barrierHeight;
		this.phaseOffset = phaseOffset;
		this.periodicity = periodicity;
	}

	public bool TypeEquivalent(ImproperTorsion other) => TypeEquivalent(other.types);

	public bool TypeEquivalent(Amber[] otherTypes) {
		bool forwardEqual = true;
		bool reverseEqual = true;
		for (int i=0, j=3; i<4; i++, j--) {
			Amber type = types[i];
			forwardEqual &= AtomicParameter.TypeEquivalent(type, otherTypes[i]);
			reverseEqual &= AtomicParameter.TypeEquivalent(type, otherTypes[j]);
			if (!(forwardEqual || reverseEqual)) {
				return false;
			}
		}
		return true;
	}

	public bool TypeEquivalentOrWild(ImproperTorsion other) => TypeEquivalentOrWild(other.types);

	public bool TypeEquivalentOrWild(Amber[] otherTypes) {
		bool forwardEqual = true;
		bool reverseEqual = true;
		for (int i=0, j=3; i<4; i++, j--) {
			Amber type = types[i];
			forwardEqual &= AtomicParameter.TypeEquivalentOrWild(type, otherTypes[i]);
			reverseEqual &= AtomicParameter.TypeEquivalentOrWild(type, otherTypes[j]);
			if (!(forwardEqual || reverseEqual)) {
				return false;
			}
		}
		return true;
	}

	public string GetGaussianParamStr() {
		return string.Format (
			"ImpTrs {0,-2} {1,-2} {2,-2} {3,-2} {4,7:F4} {5,6:F1} {6,4:F1}{7}", 
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1]), 
			AmberCalculator.GetAmberString(types[2]), 
			AmberCalculator.GetAmberString(types[3]), 
			barrierHeight, 
			phaseOffset, 
			periodicity, 
			FileIO.newLine
		);
	}

	public override string ToString () {
		return string.Format (
			"ImproperTorsion({0}-{1}-{2}-{3})", 
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1]), 
			AmberCalculator.GetAmberString(types[2]), 
			AmberCalculator.GetAmberString(types[3])
		);
	}

	public string GetTypesString() {
		return string.Format (
			"{0}-{1}-{2}-{3}", 
			AmberCalculator.GetAmberString(types[0]), 
			AmberCalculator.GetAmberString(types[1]),
			AmberCalculator.GetAmberString(types[2]),
			AmberCalculator.GetAmberString(types[3])
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

public class WildCardEqualityComparer : IEqualityComparer<Amber> {
	public bool Equals(Amber s0, Amber s1) {
		return s0 == s1 || s0 == Amber._ || s1 == Amber._;
	}
	public int GetHashCode(Amber s0) {
		return s0.GetHashCode();
	}
}