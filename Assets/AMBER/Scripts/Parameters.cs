using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TID = Constants.TaskID;
using VDWT = Constants.VanDerWaalsType;
using CT = Constants.CoulombType;
using EL = Constants.ErrorLevel;
using BT = Constants.BondType;
using Amber = Constants.Amber;
using System.Linq;
using Unity.Mathematics;

public class Parameters : MonoBehaviour {

	public float dielectricConstant;
	public Geometry parent;

	public Dictionary<Amber1, AtomicParameter> atomicParameters;
	public Dictionary<Amber2, Stretch> stretches;
	public Dictionary<Amber3, Bend> bends;
	public Dictionary<Amber4, Torsion> torsions;
	public Dictionary<Amber4, ImproperTorsion> improperTorsions;
	public NonBonding nonbonding;

	// Use this for initialization
	void Awake () {
		nonbonding       = new NonBonding ();
		atomicParameters = new Dictionary<Amber1, AtomicParameter> ();
		stretches        = new Dictionary<Amber2, Stretch> ();
		bends            = new Dictionary<Amber3, Bend> ();
		torsions         = new Dictionary<Amber4, Torsion> ();
		improperTorsions = new Dictionary<Amber4, ImproperTorsion> ();
		dielectricConstant = 1.0f;
	}

	public Parameters Duplicate(Transform parent=null) {

		Parameters newParameters = PrefabManager.InstantiateParameters(parent);

		newParameters.nonbonding         = nonbonding.Copy();
		newParameters.atomicParameters   = atomicParameters.ToDictionary(x => x.Key, x => x.Value.Copy());
		newParameters.stretches          = stretches       .ToDictionary(x => x.Key, x => x.Value.Copy());
		newParameters.bends              = bends           .ToDictionary(x => x.Key, x => x.Value.Copy());
		newParameters.torsions           = torsions        .ToDictionary(x => x.Key, x => x.Value.Copy());
		newParameters.improperTorsions   = improperTorsions.ToDictionary(x => x.Key, x => x.Value.Copy());
		newParameters.dielectricConstant = dielectricConstant;

		return newParameters;
	}

	public static void Copy(Geometry fromParent, Geometry toParent) {

		Parameters toParameters = toParent.parameters;
		Parameters fromParameters = fromParent.parameters;

		toParameters.nonbonding         = fromParameters.nonbonding      .Copy();
		toParameters.atomicParameters   = fromParameters.atomicParameters.ToDictionary(x => x.Key, x => x.Value.Copy());
		toParameters.stretches          = fromParameters.stretches       .ToDictionary(x => x.Key, x => x.Value.Copy());
		toParameters.bends              = fromParameters.bends           .ToDictionary(x => x.Key, x => x.Value.Copy());
		toParameters.torsions           = fromParameters.torsions        .ToDictionary(x => x.Key, x => x.Value.Copy());
		toParameters.improperTorsions   = fromParameters.improperTorsions.ToDictionary(x => x.Key, x => x.Value.Copy());
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

	public void UpdateAtomicParameters(Parameters other, bool replace=false, bool skipInvalid=true) {
		foreach ((Amber1 types, AtomicParameter otherP) in other.atomicParameters) {

			AtomicParameter thisP;
			//Check if this has parameter
			if (atomicParameters.TryGetValue(types, out thisP)) {
				//Has parameter - See if it needs to be replaced
				if (replace && !thisP.ValuesClose(otherP)) {
					thisP = otherP.Copy();
				}
			} else {
				//Doesn't have parameter - Copy
				atomicParameters[types] = otherP.Copy();
			}
		}
	}
	

	public void UpdateStretches(Parameters other, bool replace=false, bool skipInvalid=true) {
		foreach ((Amber2 types, Stretch otherP) in other.stretches) {

			//Validate Parameter
			if (skipInvalid && otherP.IsInvalid()) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Skipping invalid Stretch: {0}",
					otherP
				);
				continue;
			}

			Stretch thisP;
			//Check if this has parameter
			if (stretches.TryGetValue(types, out thisP)) {
				//Has parameter - See if it needs to be replaced
				if (replace && !thisP.ValuesClose(otherP)) {
					thisP = otherP.Copy();
				}
			} else {
				//Doesn't have parameter - Copy
				stretches[types] = otherP.Copy();
			}
		}
	}

	public void UpdateBends(Parameters other, bool replace=false, bool skipInvalid=true) {
		foreach ((Amber3 types, Bend otherP) in other.bends) {

			//Validate Parameter
			if (skipInvalid && otherP.IsInvalid()) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Skipping invalid Bend: {0}",
					otherP
				);
				continue;
			}

			Bend thisP;
			//Check if this has parameter
			if (bends.TryGetValue(types, out thisP)) {
				//Has parameter - See if it needs to be replaced
				if (replace && !thisP.ValuesClose(otherP)) {
					thisP = otherP.Copy();
				}
			} else {
				//Doesn't have parameter - Copy
				bends[types] = otherP.Copy();
			}
		}
	}
	
	public void UpdateTorsions(Parameters other, bool replace=false, bool skipInvalid=true) {
		foreach ((Amber4 types, Torsion otherP) in other.torsions) {

			//Validate Parameter
			if (skipInvalid && otherP.IsInvalid()) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Skipping invalid Torsion: {0}",
					otherP
				);
				continue;
			}

			Torsion thisP;
			//Check if this has parameter
			if (torsions.TryGetValue(types, out thisP)) {
				//Has parameter - See if it needs to be replaced
				if (replace && !thisP.ValuesClose(otherP)) {
					thisP = otherP.Copy();
				}
			} else {
				//Doesn't have parameter - Copy
				torsions[types] = otherP.Copy();
			}
		}
	}
	
	public void UpdateImproperTorsions(Parameters other, bool replace=false, bool skipInvalid=true) {
		foreach ((Amber4 types, ImproperTorsion otherP) in other.improperTorsions) {

			//Validate Parameter
			if (skipInvalid && otherP.IsInvalid()) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Skipping invalid ImproperTorsion: {0}",
					otherP
				);
				continue;
			}

			ImproperTorsion thisP;
			//Check if this has parameter
			if (improperTorsions.TryGetValue(types, out thisP)) {
				//Has parameter - See if it needs to be replaced
				if (replace && !thisP.ValuesClose(otherP)) {
					thisP = otherP.Copy();
				}
			} else {
				//Doesn't have parameter - Copy
				improperTorsions[types] = otherP.Copy();
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
						UpdateParameters(missingParameters, true, true);
					}
				}
			}

		}

		NotificationBar.ClearTask(TID.CALCULATE_PARAMETERS);

	}

	public bool ContainsAtomicParameter(Amber otherType, bool allowWild=false) {
		if (allowWild) {
			return atomicParameters.Any(x => x.Key.TypeEquivalentOrWild(otherType));
		} else {
			return atomicParameters.ContainsKey(new Amber1(otherType));
		}
	}

	public bool ContainsAtomicParameter(Amber1 otherType, bool allowWild=false) {
		if (allowWild) {
			return atomicParameters.Any(x => x.Key.TypeEquivalentOrWild(otherType));
		} else {
			return atomicParameters.ContainsKey(otherType);
		}
	}

	public AtomicParameter GetAtomicParameter(Amber type, bool allowWild=false) {
		return GetAtomicParameter(new Amber1(type), allowWild);
	}

	public AtomicParameter GetAtomicParameter(Amber1 type, bool allowWild=false) {
		AtomicParameter parameter;
		if (!atomicParameters.TryGetValue(type, out parameter) && allowWild) {
			return atomicParameters.FirstOrDefault(x => x.Key.TypeEquivalentOrWild(type)).Value;
		}
		return parameter;
	}

	public bool TryGetAtomicParameter(Amber type, out AtomicParameter parameter, bool allowWild=false) {
		return TryGetAtomicParameter(new Amber1(type), out parameter, allowWild);
	}

	public bool TryGetAtomicParameter(Amber1 type, out AtomicParameter parameter, bool allowWild=false) {
		if (!atomicParameters.TryGetValue(type, out parameter)) {
			if (!allowWild) {return false;}
			foreach (KeyValuePair<Amber1, AtomicParameter> kvp in atomicParameters) {
				if (kvp.Key.TypeEquivalentOrWild(type)) {
					parameter = kvp.Value;
					return true;
				}
			}
			return false;
		}
		return true;
	}
		
	public bool ContainsStretch(
		Amber otherType0, 
		Amber otherType1,
		bool allowWild=false
	) {
		if (allowWild) {
			return stretches.Any(x => x.Key.TypeEquivalentOrWild(otherType0, otherType1));
		} else {
			return stretches.ContainsKey(new Amber2(otherType0, otherType1)) 
				|| stretches.ContainsKey(new Amber2(otherType1, otherType0));
		}
	}

	public Stretch GetStretch(Amber type0, Amber type1, bool allowWild=false) {
		return GetStretch(new Amber2(type0, type1), allowWild);
	}

	public Stretch GetStretch(Amber2 types, bool allowWild=false) {
		Stretch parameter;
		if (
			!stretches.TryGetValue(types, out parameter) && 
			!stretches.TryGetValue(types.Reversed(), out parameter) &&
			allowWild
		) {
			return stretches.FirstOrDefault(x => x.Key.TypeEquivalentOrWild(types)).Value;
		}
		return parameter;
	}

	public bool TryGetStretch(Amber type0, Amber type1, out Stretch parameter, bool allowWild=false) {
		return TryGetStretch(new Amber2(type0, type1), out parameter, allowWild);
	}

	public bool TryGetStretch(Amber2 type, out Stretch parameter, bool allowWild=false) {
		if (
			!stretches.TryGetValue(type, out parameter) &&
			!stretches.TryGetValue(type.Reversed(), out parameter)
		) {
			if (!allowWild) {return false;}
			foreach (KeyValuePair<Amber2, Stretch> kvp in stretches) {
				if (kvp.Key.TypeEquivalentOrWild(type)) {
					parameter = kvp.Value;
					return true;
				}
			}
			return false;
		}
		return true;
	}

	public bool ContainsBend(
		Amber otherType0, 
		Amber otherType1, 
		Amber otherType2,
		bool allowWild=false
	) {
		if (allowWild) {
			return bends.Any(x => x.Key.TypeEquivalentOrWild(otherType0, otherType1, otherType2));
		} else {
			return bends.ContainsKey(new Amber3(otherType0, otherType1, otherType2))
				|| bends.ContainsKey(new Amber3(otherType2, otherType1, otherType0));
		}
	}

	public Bend GetBend(Amber type0, Amber type1, Amber type2, bool allowWild=false) {
		return GetBend(new Amber3(type0, type1, type2), allowWild);
	}

	public Bend GetBend(Amber3 types, bool allowWild=false) {
		Bend parameter;
		if (
			!bends.TryGetValue(types, out parameter) && 
			!bends.TryGetValue(types.Reversed(), out parameter) && 
			allowWild
		) {
			return bends.FirstOrDefault(x => x.Key.TypeEquivalentOrWild(types)).Value;
		}
		return parameter;
	}

	public bool TryGetBend(Amber type0, Amber type1, Amber type2, out Bend parameter, bool allowWild=false) {
		return TryGetBend(new Amber3(type0, type1, type2), out parameter, allowWild);
	}

	public bool TryGetBend(Amber3 type, out Bend parameter, bool allowWild=false) {
		if (
			!bends.TryGetValue(type, out parameter) &&
			!bends.TryGetValue(type.Reversed(), out parameter)
		) {
			if (!allowWild) {return false;}
			foreach (KeyValuePair<Amber3, Bend> kvp in bends) {
				if (kvp.Key.TypeEquivalentOrWild(type)) {
					parameter = kvp.Value;
					return true;
				}
			}
			return false;
		}
		return true;
	}

	public bool ContainsTorsion(
		Amber otherType0, 
		Amber otherType1, 
		Amber otherType2,
		Amber otherType3,
		bool allowWild=false
	) {
		if (allowWild) {
			return torsions.Any(x => x.Key.TypeEquivalentOrWild(otherType0, otherType1, otherType2, otherType3));
		} else {
			return torsions.ContainsKey(new Amber4(otherType0, otherType1, otherType2, otherType3))
				|| torsions.ContainsKey(new Amber4(otherType3, otherType2, otherType1, otherType0));
		}
	}

	public Torsion GetTorsion(Amber type0, Amber type1, Amber type2, Amber type3, bool allowWild=false) {
		return GetTorsion(new Amber4(type0, type1, type2, type3), allowWild);
	}

	public Torsion GetTorsion(Amber4 types, bool allowWild=false) {
		Torsion parameter;
		if (
			!torsions.TryGetValue(types, out parameter) && 
			!torsions.TryGetValue(types.Reversed(), out parameter) && 
			allowWild
		) {
			return torsions.FirstOrDefault(x => x.Key.TypeEquivalentOrWild(types)).Value;
		}
		return parameter;
	}

	public bool TryGetTorsion(Amber type0, Amber type1, Amber type2, Amber type3, out Torsion parameter, bool allowWild=false) {
		return TryGetTorsion(new Amber4(type0, type1, type2, type3), out parameter, allowWild);
	}

	public bool TryGetTorsion(Amber4 type, out Torsion parameter, bool allowWild=false) {
		if (
			!torsions.TryGetValue(type, out parameter) &&
			!torsions.TryGetValue(type.Reversed(), out parameter)
		) {
			if (!allowWild) {return false;}
			foreach (KeyValuePair<Amber4, Torsion> kvp in torsions) {
				if (kvp.Key.TypeEquivalentOrWild(type)) {
					parameter = kvp.Value;
					return true;
				}
			}
			return false;
		}
		return true;
	}

	public bool ContainsImproperTorsion(
		Amber otherType0, 
		Amber otherType1, 
		Amber otherType2,
		Amber otherType3,
		bool allowWild=false
	) {
		if (allowWild) {
			return improperTorsions.Any(x => x.Key.TypeEquivalentOrWild(otherType0, otherType1, otherType2, otherType3));
		} else {
			return improperTorsions.ContainsKey(new Amber4(otherType0, otherType1, otherType2, otherType3))
				|| improperTorsions.ContainsKey(new Amber4(otherType3, otherType2, otherType1, otherType0));
		}
	}

	public ImproperTorsion GetImproperTorsion(Amber type0, Amber type1, Amber type2, Amber type3, bool allowWild=false) {
		return GetImproperTorsion(new Amber4(type0, type1, type2, type3), allowWild);
	}

	public ImproperTorsion GetImproperTorsion(Amber4 types, bool allowWild=false) {
		ImproperTorsion parameter;
		if (
			!improperTorsions.TryGetValue(types, out parameter) && 
			!improperTorsions.TryGetValue(types.Reversed(), out parameter) && 
			allowWild
		) {
			return improperTorsions.FirstOrDefault(x => x.Key.TypeEquivalentOrWild(types)).Value;
		}
		return parameter;
	}

	public bool TryGetImproperTorsion(Amber type0, Amber type1, Amber type2, Amber type3, out ImproperTorsion parameter, bool allowWild=false) {
		return TryGetImproperTorsion(new Amber4(type0, type1, type2, type3), out parameter, allowWild);
	}

	public bool TryGetImproperTorsion(Amber4 type, out ImproperTorsion parameter, bool allowWild=false) {
		if (
			!improperTorsions.TryGetValue(type, out parameter) &&
			!improperTorsions.TryGetValue(type.Reversed(), out parameter)
		) {
			if (!allowWild) {return false;}
			foreach (KeyValuePair<Amber4, ImproperTorsion> kvp in improperTorsions) {
				if (kvp.Key.TypeEquivalentOrWild(type)) {
					parameter = kvp.Value;
					return true;
				}
			}
			return false;
		}
		return true;
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
		foreach (Stretch stretch in stretches.Values) 
			paramsSB.Append (stretch.GetGaussianParamStr ());
		foreach (Bend bend in bends.Values) 
			paramsSB.Append (bend.GetGaussianParamStr ());
		foreach (Torsion torsion in torsions.Values)
			paramsSB.Append (torsion.GetGaussianParamStr ());
		foreach (ImproperTorsion improperTorsion in improperTorsions.Values) 
			paramsSB.Append (improperTorsion.GetGaussianParamStr ());
		foreach (AtomicParameter atomicParameter in atomicParameters.Values)
			paramsSB.Append (atomicParameter.GetGaussianParamStr ());

		
		return paramsSB.ToString();
	}

	public void AddAtomicParameter(AtomicParameter newParameter) {
		AtomicParameter oldParameter;
		//Check if this has parameter
		if (atomicParameters.TryGetValue(newParameter.type, out oldParameter)) {
			//Has parameter
			if (!oldParameter.ValuesClose(newParameter)) {
				CustomLogger.LogFormat(
					EL.INFO,
					"Replacing {0} with {1}", 
					oldParameter, 
					newParameter
				);
				atomicParameters[newParameter.type] = newParameter.Copy();
			}
		} else {
			atomicParameters[newParameter.type] = newParameter.Copy();
		}
	}

	public void AddStretch(Stretch newParameter) {
		Stretch oldParameter;
		//Check if this has parameter
		if (stretches.TryGetValue(newParameter.types, out oldParameter)) {
			//Has parameter
			if (!oldParameter.ValuesClose(newParameter)) {
				CustomLogger.LogFormat(
					EL.INFO,
					"Replacing {0} with {1}", 
					oldParameter, 
					newParameter
				);
				stretches[newParameter.types] = newParameter.Copy();
			}
		} else {
			stretches[newParameter.types] = newParameter.Copy();
		}
	}

	public void AddBend(Bend newParameter) {
		Bend oldParameter;
		//Check if this has parameter
		if (bends.TryGetValue(newParameter.types, out oldParameter)) {
			//Has parameter
			if (!oldParameter.ValuesClose(newParameter)) {
				CustomLogger.LogFormat(
					EL.INFO,
					"Replacing {0} with {1}", 
					oldParameter, 
					newParameter
				);
				bends[newParameter.types] = newParameter.Copy();
			}
		} else {
			bends[newParameter.types] = newParameter.Copy();
		}
	}

	public void AddTorsion(Torsion newParameter) {
		Torsion oldParameter;
		//Check if this has parameter
		if (torsions.TryGetValue(newParameter.types, out oldParameter)) {
			//Has parameter
			if (!oldParameter.ValuesClose(newParameter)) {
				CustomLogger.LogFormat(
					EL.INFO,
					"Replacing {0} with {1}", 
					oldParameter, 
					newParameter
				);
				torsions[newParameter.types] = newParameter.Copy();
			}
		} else {
			torsions[newParameter.types] = newParameter.Copy();
		}
	}

	public void AddImproperTorsion(ImproperTorsion newParameter) {
		ImproperTorsion oldParameter;
		//Check if this has parameter
		if (improperTorsions.TryGetValue(newParameter.types, out oldParameter)) {
			//Has parameter
			if (!oldParameter.ValuesClose(newParameter)) {
				CustomLogger.LogFormat(
					EL.INFO,
					"Replacing {0} with {1}", 
					oldParameter, 
					newParameter
				);
				improperTorsions[newParameter.types] = newParameter.Copy();
			}
		} else {
			improperTorsions[newParameter.types] = newParameter.Copy();
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

	public void GetAtomPenalty(AtomID atomID0, float noParameterPenalty=1000f) {

		Atom atom0 = parent.GetAtom(atomID0);

		//Penalty for this atom
		AtomicParameter atomicParameter;
		if (TryGetAtomicParameter(atom0.amber, out atomicParameter)) {
			atom0.penalty += atomicParameter.penalty;
			CustomLogger.LogFormat(
				EL.DEBUG,
				"Penalty: {0}, Atom ID: {1}, Amber: {2}, Atomic Parameter: {3}",
				() => new object[] {
					atomicParameter.penalty,
					atomID0,
					atom0.amber,
					atomicParameter
				}
			);
		} else {
			CustomLogger.LogFormat(
				EL.WARNING,
				"No Atomic Parameter for Atom ID: {0}, Amber: {1}",
				() => new object[] {
					atomID0,
					atom0.amber
				}
			);
			atom0.penalty = noParameterPenalty;
			return;
		}

		Amber1 type1         = new Amber1(atom0.amber);
        Amber2 stretchTypes  = new Amber2(atom0.amber, Amber.X);
        Amber3 bendTypes     = new Amber3(atom0.amber, Amber.X, Amber.X);
        Amber4 dihedralTypes = new Amber4(atom0.amber, Amber.X, Amber.X, Amber.X);

		foreach (AtomID atomID1 in atom0.EnumerateNeighbours()) {
			Atom atom1;
			if (!parent.TryGetAtom(atomID1, out atom1)) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Neighbour Atom: {0} not found! Try recomputing connectivity",
					() => new object[] {
						atomID1
					}
				);
				atom0.penalty = noParameterPenalty;
				atom1.penalty = noParameterPenalty;
				return;
			}

			stretchTypes.amber1 = atom1.amber;
			bendTypes.amber1 = atom1.amber;
			dihedralTypes.amber1 = atom1.amber;

			Stretch stretch;
			if (TryGetStretch(stretchTypes, out stretch, true)) {
				atom0.penalty += stretch.penalty;
				atom1.penalty += stretch.penalty;
				CustomLogger.LogFormat(
					EL.DEBUG,
					"Penalty: {0}, Atom ID: {1}, Amber: {2}, Stretch Parameter: {3}",
					() => new object[] {
						stretch.penalty,
						atomID0,
						atom0.amber,
						stretch
					}
				);
			} else {
				CustomLogger.LogFormat(
					EL.WARNING,
					"No Stretch Parameter for Atoms: '{0}'-'{1}'. Ambers: {2}-{3}",
					() => new object[] {
						atomID0,
						atomID1,
						stretchTypes.amber0,
						stretchTypes.amber1
					}
				);
				atom0.penalty = noParameterPenalty;
				atom1.penalty = noParameterPenalty;
				return;
			}

			foreach (AtomID atomID2 in atom1.EnumerateNeighbours()) {
				if (atomID2 == atomID0) {continue;}
				Atom atom2;
				if (!parent.TryGetAtom(atomID2, out atom2)) {
					atom0.penalty = noParameterPenalty;
					atom1.penalty = noParameterPenalty;
					atom2.penalty = noParameterPenalty;
					return;
				}

				bendTypes.amber2 = atom2.amber;
				dihedralTypes.amber2 = atom2.amber;


				Bend bend;
				if (TryGetBend(bendTypes, out bend, true)) {
					atom0.penalty += bend.penalty;
					atom1.penalty += bend.penalty;
					atom2.penalty += bend.penalty;
					CustomLogger.LogFormat(
						EL.DEBUG,
						"Penalty: {0}, Atom ID: {1}, Amber: {2}, Bend Parameter: {3}",
						() => new object[] {
							stretch.penalty,
							atomID0,
							atom0.amber,
							bend
						}
					);
				} else {
					CustomLogger.LogFormat(
						EL.WARNING,
						"No Bend Parameter for Atoms: '{0}'-'{1}'-'{2}'. Ambers: {3}-{4}-{5}",
						() => new object[] {
							atomID0,
							atomID1,
							atomID2,
							bendTypes.amber0,
							bendTypes.amber1,
							bendTypes.amber2
						}
					);
					atom0.penalty = noParameterPenalty;
					atom1.penalty = noParameterPenalty;
					atom2.penalty = noParameterPenalty;
					return;
				}

				

				foreach (AtomID atomID3 in atom2.EnumerateNeighbours()) {
					if (atomID3 == atomID0 || atomID3 == atomID1) {continue;}
					Atom atom3;
					if (!parent.TryGetAtom(atomID3, out atom3)) {
						atom0.penalty = noParameterPenalty;
						atom1.penalty = noParameterPenalty;
						atom2.penalty = noParameterPenalty;
						atom3.penalty = noParameterPenalty;
						return;
					}

					dihedralTypes.amber3 = atom3.amber;


					Torsion torsion;
					if (TryGetTorsion(dihedralTypes, out torsion, true)) {
						atom0.penalty += torsion.penalty;
						atom1.penalty += torsion.penalty;
						atom2.penalty += torsion.penalty;
						atom3.penalty += torsion.penalty;
						CustomLogger.LogFormat(
							EL.DEBUG,
							"Penalty: {0}, Atom ID: {1}, Amber: {2}, Torsion Parameter: {3}",
							() => new object[] {
								stretch.penalty,
								atomID0,
								atom0.amber,
								torsion
							}
						);
					}
				}
			}
		}
	}
}

public class AtomicParameter {
	public Amber1 type;
	public float radius;
	public float wellDepth;
	public float mass;
	public float penalty;

	public AtomicParameter(Amber type) {
		this.type = new Amber1(type);
	}

	public AtomicParameter(Amber1 type) {
		this.type = type;
	}

	public AtomicParameter(Amber1 type, float radius, float wellDepth, float mass, float penalty=0f) {
		this.type = type;
		this.radius = radius;
		this.wellDepth = wellDepth;
		this.mass = mass;
		this.penalty = penalty;
	}

	public AtomicParameter(Amber type, float radius, float wellDepth, float mass, float penalty=0f) {
		this.type = new Amber1(type);
		this.radius = radius;
		this.wellDepth = wellDepth;
		this.mass = mass;
		this.penalty = penalty;
	}

	public bool TypeEquivalent(AtomicParameter other) => TypeEquivalent(other.type);
	public bool TypeEquivalent(Amber1 otherType) => TypeEquivalent(this.type, otherType);
	public static bool TypeEquivalent(AtomicParameter first, AtomicParameter second) => first.TypeEquivalent(second.type);
	public static bool TypeEquivalent(Amber firstType, Amber secondType) => (firstType == secondType);
	public static bool TypeEquivalent(Amber1 firstType, Amber1 secondType) => (firstType.amber == secondType.amber);
	public bool TypeEquivalentOrWild(AtomicParameter other) => TypeEquivalentOrWild(other.type);
	public bool TypeEquivalentOrWild(Amber1 otherType) => TypeEquivalentOrWild(this.type, otherType);
	public static bool TypeEquivalentOrWild(AtomicParameter first, AtomicParameter second) => first.TypeEquivalentOrWild(second.type);
	public static bool TypeEquivalentOrWild(Amber firstType, Amber secondType) => (firstType == secondType || firstType == Amber._ || secondType == Amber._);
	public static bool TypeEquivalentOrWild(Amber1 firstType, Amber1 secondType) => (
		firstType.amber == secondType.amber || 
		firstType.amber == Amber._ || 
		secondType.amber == Amber._);
	
	public bool ValuesClose(AtomicParameter other, float epsilon=0.00001f) {
		return (
			Mathf.Abs(radius - other.radius) < epsilon &&
			Mathf.Abs(wellDepth - other.wellDepth) < epsilon &&
			Mathf.Abs(mass - other.mass) < epsilon
		);
	}

	public string GetGaussianParamStr() {
		return string.Format ("VDW {0,-2} {1,7:F4} {2,7:F4}{3}", AmberCalculator.GetAmberString(type.amber), radius, wellDepth, FileIO.newLine);
	}

	public override string ToString () {
		return string.Format ("VdW(type={0}, radius={1}, wellDepth={2}, mass={3})", AmberCalculator.GetAmberString(type.amber), radius, wellDepth, mass);
	}

	public AtomicParameter Copy() => new AtomicParameter (type, radius, wellDepth, mass, penalty);
	
}

public struct Stretch {

	public Amber2 types;
	public float req;
	public float keq;
	public float penalty;

	public bool IsInvalid() => req <= 0.00000001f;

	public Stretch(Amber t0, Amber t1, float req=0f, float keq=0f, float penalty=0f) {
		this.types = new Amber2(t0, t1);
		this.req = req;
		this.keq = keq;
		this.penalty = penalty;
	}

	public Stretch(Amber2 types, float req=0f, float keq=0f, float penalty=0f) {
		this.types = types;
		this.req = req;
		this.keq = keq;
		this.penalty = penalty;
	}

	public Stretch(string typesString, float req=0f, float keq=0f, float penalty=0f) {
		types = new Amber2(typesString);
		this.req = req;
		this.keq = keq;
		this.penalty = penalty;
	}

	public bool TypeEquivalent(Stretch other) => types.TypeEquivalent(other.types);
	public bool TypeEquivalent(Amber2 otherTypes) => types.TypeEquivalent(otherTypes);
	public bool TypeEquivalentOrWild(Stretch other) => types.TypeEquivalentOrWild(other.types);
	public bool TypeEquivalentOrWild(Amber2 otherTypes) => types.TypeEquivalentOrWild(otherTypes);
	
	public bool ValuesClose(Stretch other, float epsilon=0.00001f) {
		return (
			Mathf.Abs(req - other.req) < epsilon &&
			Mathf.Abs(keq - other.keq) < epsilon
		);
	}

	public string GetGaussianParamStr() {
		return string.Format (
			"HrmStr1 {0,-2} {1,-2} {2,7:F4} {3,7:F4}{4}", 
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1), 
			keq, 
			req, 
			FileIO.newLine
		);
	}

	public override string ToString () {
		return string.Format (
			"Stretch({0}-{1}, req = {2}, keq = {3})", 
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1),  
			req, 
			keq
		);
	}

	public string GetTypesString() {
		return string.Format (
			"{0}-{1}", 
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1)
		);
	}

	public Stretch Copy() => new Stretch (types, req, keq, penalty);
	
	public bool IsDefault() {
		return types.IsEmpty();
	}
}

public struct Bend {

	public Amber3 types;
	public float aeq;
	public float keq;
	public float penalty;
	public bool IsInvalid() => aeq <= 0.00000001f;

	public Bend(Amber t0, Amber t1, Amber t2, float aeq=0f, float keq=0f, float penalty=0f) {
		this.types = new Amber3 (t0, t1, t2);
		this.aeq = aeq;
		this.keq = keq;
		this.penalty = penalty;
	}

	public Bend(Amber3 types, float aeq=0f, float keq=0f, float penalty=0f) {
		this.types = types;
		this.aeq = aeq;
		this.keq = keq;
		this.penalty = penalty;
	}

	public Bend(string typesString, float aeq, float keq, float penalty=0f) {
		types = new Amber3(typesString);
		this.aeq = aeq;
		this.keq = keq;
		this.penalty = penalty;
	}

	public bool TypeEquivalent(Bend other) => types.TypeEquivalent(other.types);
	public bool TypeEquivalent(Amber3 otherTypes) => types.TypeEquivalent(otherTypes);
	public bool TypeEquivalentOrWild(Bend other) => types.TypeEquivalentOrWild(other.types);
	public bool TypeEquivalentOrWild(Amber3 otherTypes) => types.TypeEquivalentOrWild(otherTypes);
	public bool ValuesClose(Bend other, float epsilon=0.00001f) {
		return (
			Mathf.Abs(aeq - other.aeq) < epsilon &&
			Mathf.Abs(keq - other.keq) < epsilon
		);
	}

	public string GetGaussianParamStr() {
		return string.Format (
			"HrmBnd1 {0,-2} {1,-2} {2,-2} {3,7:F4} {4,7:F4}{5}", 
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1), 
			AmberCalculator.GetAmberString(types.amber2), 
			keq, 
			aeq, 
			FileIO.newLine
		);
	}

	public override string ToString () {
		return string.Format (
			"Bend({0}-{1}-{2}, req = {3}, keq = {4})",
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1), 
			AmberCalculator.GetAmberString(types.amber2), 
			aeq, 
			keq
		);
	}

	public string GetTypesString() {
		return string.Format (
			"{0}-{1}-{2}", 
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1), 
			AmberCalculator.GetAmberString(types.amber2)
		);
	}

	public bool IsDefault() {
		return types.IsEmpty();
	}

	public Bend Copy() => new Bend (types, aeq, keq, penalty);
	
}

public struct Torsion {
	public Amber4 types;
	public float[] barrierHeights;
	public float[] phaseOffsets;
	public int npaths;
	public float penalty;
	public bool IsInvalid() => npaths == 0;

	public Torsion(Amber t0, Amber t1, Amber t2, Amber t3, float[] barrierHeights, float[] phaseOffsets, int npaths=0, float penalty=0f) {
		this.types = new Amber4 (t0, t1, t2, t3);
		this.barrierHeights = barrierHeights;
		this.phaseOffsets = phaseOffsets;
		this.npaths = npaths;
		this.penalty = penalty;
	}

	public Torsion(Amber t0, Amber t1, Amber t2, Amber t3, float v0=0f, float v1=0f, float v2=0f, float v3=0f, float gamma0=0f, float gamma1=0f, float gamma2=0f, float gamma3=0f, int npaths=0, float penalty=0f) {
		this.types = new Amber4 (t0, t1, t2, t3);
		this.barrierHeights = new float[4] {v0, v1, v2, v3};
		this.phaseOffsets = new float[4] {gamma0, gamma1, gamma2, gamma3};
		this.npaths = npaths;
		this.penalty = penalty;
	}

	public Torsion(Amber4 types, float[] barrierHeights, float[] phaseOffsets, int npaths=0, float penalty=0f) {
		this.types = types;
		this.barrierHeights = barrierHeights;
		this.phaseOffsets = phaseOffsets;
		this.npaths = npaths;
		this.penalty = penalty;
	}

	public Torsion(Amber4 types, float v0=0f, float v1=0f, float v2=0f, float v3=0f, float gamma0=0f, float gamma1=0f, float gamma2=0f, float gamma3=0f, int npaths=0, float penalty=0f) {
		this.types = types;
		this.barrierHeights = new float[4] {v0, v1, v2, v3};
		this.phaseOffsets = new float[4] {gamma0, gamma1, gamma2, gamma3};
		this.npaths = npaths;
		this.penalty = penalty;
	}

	public Torsion(string typesString, float[] barrierHeights, float[] phaseOffsets, int npaths=0, float penalty=0f) {
		types = new Amber4(typesString);
		this.barrierHeights = barrierHeights;
		this.phaseOffsets = phaseOffsets;
		this.npaths = npaths;
		this.penalty = penalty;
	}

	public Torsion(string typesString, float v0=0f, float v1=0f, float v2=0f, float v3=0f, float gamma0=0f, float gamma1=0f, float gamma2=0f, float gamma3=0f, int npaths=0, float penalty=0f) {
		types = new Amber4(typesString);
		this.barrierHeights = new float[4] {v0, v1, v2, v3};
		this.phaseOffsets = new float[4] {gamma0, gamma1, gamma2, gamma3};
		this.npaths = npaths;
		this.penalty = penalty;
	}

	public void Modify(int periodicity, float newBarrierHeight, float newPhaseOffset) {
		this.barrierHeights[periodicity] = newBarrierHeight;
		this.phaseOffsets[periodicity] = newPhaseOffset;
	}

	public bool TypeEquivalent(Torsion other) => types.TypeEquivalent(other.types);
	public bool TypeEquivalent(Amber4 otherTypes) => types.TypeEquivalent(otherTypes);
	public bool TypeEquivalentOrWild(Torsion other) => types.TypeEquivalentOrWild(other.types);
	public bool TypeEquivalentOrWild(Amber4 otherTypes) => types.TypeEquivalentOrWild(otherTypes);
	public bool ValuesClose(Torsion other, float epsilon=0.00001f) {
		return (
			npaths == other.npaths &&
			barrierHeights.Select((x,i) => Mathf.Abs(x - other.barrierHeights[i]) < epsilon).All(b => b) &&
			phaseOffsets  .Select((x,i) => Mathf.Abs(x - other.phaseOffsets[i]  ) < epsilon).All(b => b)
		);
	}

	public string GetGaussianParamStr() {
		return string.Format(
			"AmbTrs {0,-2} {1,-2} {2,-2} {3,-2} {4,6:F0} {5,6:F0} {6,6:F0} {7,6:F0} {8,7:F1} {9,7:F1} {10,7:F1} {11,7:F1} {12,4:F1}{13}", 
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1), 
			AmberCalculator.GetAmberString(types.amber2), 
			AmberCalculator.GetAmberString(types.amber3),  
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
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1), 
			AmberCalculator.GetAmberString(types.amber2), 
			AmberCalculator.GetAmberString(types.amber3)
		);
	}

	public string GetTypesString() {
		return string.Format (
			"{0}-{1}-{2}-{3}", 
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1),
			AmberCalculator.GetAmberString(types.amber2),
			AmberCalculator.GetAmberString(types.amber3)
		);
	}

	public Torsion Copy() => new Torsion (types, barrierHeights, phaseOffsets, npaths, penalty);
	
	public bool IsDefault() {
		return types.IsEmpty();
	}
}

public struct ImproperTorsion {
	public Amber4 types;
	public float barrierHeight;
	public float phaseOffset;
	public int periodicity;
	public float penalty;
	public bool IsInvalid() => periodicity == 0;

	public ImproperTorsion(Amber t0, Amber t1, Amber t2, Amber t3, float barrierHeight=0f, float phaseOffset=0f, int periodicity=1, float penalty=0f) {
		this.types = new Amber4 (t0, t1, t2, t3);
		this.barrierHeight = barrierHeight;
		this.phaseOffset = phaseOffset;
		this.periodicity = periodicity;
		this.penalty = penalty;
	}

	public ImproperTorsion(Amber4 types, float barrierHeight=0f, float phaseOffset=0f, int periodicity=0, float penalty=0f) {
		this.types = types;
		this.barrierHeight = barrierHeight;
		this.phaseOffset = phaseOffset;
		this.periodicity = periodicity;
		this.penalty = penalty;
	}

	public ImproperTorsion(string typesString, float barrierHeight=0f, float phaseOffset=0f, int periodicity=0, float penalty=0f) {
		types = new Amber4(typesString);
		this.barrierHeight = barrierHeight;
		this.phaseOffset = phaseOffset;
		this.periodicity = periodicity;
		this.penalty = penalty;
	}

	public bool TypeEquivalent(ImproperTorsion other) => types.TypeEquivalent(other.types);
	public bool TypeEquivalent(Amber4 otherTypes) => types.TypeEquivalent(otherTypes);
	public bool TypeEquivalentOrWild(ImproperTorsion other) => types.TypeEquivalentOrWild(other.types);
	public bool TypeEquivalentOrWild(Amber4 otherTypes) => types.TypeEquivalentOrWild(otherTypes);
	public bool ValuesClose(ImproperTorsion other, float epsilon=0.00001f) {
		return (
			periodicity == other.periodicity &&
			barrierHeight == other.barrierHeight &&
			phaseOffset == other.phaseOffset
		);
	}

	public string GetGaussianParamStr() {
		return string.Format (
			"ImpTrs {0,-2} {1,-2} {2,-2} {3,-2} {4,7:F4} {5,6:F1} {6,4:F1}{7}", 
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1), 
			AmberCalculator.GetAmberString(types.amber2), 
			AmberCalculator.GetAmberString(types.amber3), 
			barrierHeight, 
			phaseOffset, 
			periodicity, 
			FileIO.newLine
		);
	}

	public override string ToString () {
		return string.Format (
			"ImproperTorsion({0}-{1}-{2}-{3})", 
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1), 
			AmberCalculator.GetAmberString(types.amber2), 
			AmberCalculator.GetAmberString(types.amber3)
		);
	}

	public string GetTypesString() {
		return string.Format (
			"{0}-{1}-{2}-{3}", 
			AmberCalculator.GetAmberString(types.amber0), 
			AmberCalculator.GetAmberString(types.amber1), 
			AmberCalculator.GetAmberString(types.amber2), 
			AmberCalculator.GetAmberString(types.amber3)
		);
	}

	public ImproperTorsion Copy() => new ImproperTorsion (types, barrierHeight, phaseOffset, periodicity, penalty);
	
	public bool IsDefault() {
		return types.IsEmpty();
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

public struct Amber1 {
	public Amber amber;

	public Amber1(Amber amber) {
		this.amber = amber;
	}

	public Amber1(string typeString) {
		this.amber = Constants.AmberMap[typeString];
	}

	public override bool Equals(object obj) {
		if (obj == null || !obj.GetType().Equals(this.GetType())) {
			return false;
		}
		Amber1 other = (Amber1)obj;
		return TypeEquivalent(other);
	}

	public override int GetHashCode() {
		return CustomMathematics.GetCombinedHash((int)amber);
	}

	public static bool TypeEquivalent(Amber amber0, Amber amber1) => amber0 == amber1;
	public static bool TypeEquivalent(Amber1 type0, Amber1 type1) => TypeEquivalent(type0.amber, type1.amber);
	public bool TypeEquivalent(Amber other) => TypeEquivalent(this.amber, other);
	public bool TypeEquivalent(Amber1 other) => TypeEquivalent(this, other);

	public static bool TypeEquivalentOrWild(Amber amber0, Amber amber1) => amber0 == amber1 || amber0 == Amber._ || amber1 == Amber._;
	public static bool TypeEquivalentOrWild(Amber1 type0, Amber1 type1) => TypeEquivalentOrWild(type0.amber, type1.amber);
	public bool TypeEquivalentOrWild(Amber other) => TypeEquivalentOrWild(this.amber, other);
	public bool TypeEquivalentOrWild(Amber1 other) => TypeEquivalentOrWild(this, other);

}

public struct Amber2 {
	public Amber amber0;
	public Amber amber1;

	public static Amber2 empty => new Amber2(Amber.X, Amber.X);
	public bool IsEmpty() => amber0 == Amber.X && amber1 == Amber.X;

	public Amber2(Amber amber0, Amber amber1) {
		this.amber0 = amber0;
		this.amber1 = amber1;
	}

	public Amber2(string typesString) {
		string[] ambers = typesString.Split(new char[1] {'-'}, System.StringSplitOptions.None);
		
		if (ambers.Length != 2) {
			throw new System.Exception(string.Format(
				"Wrong length of 'ambers' ({0} - should be {1})",
				ambers.Length,
				2
			));
		}

		amber0 = Constants.AmberMap[ambers[0]];
		amber1 = Constants.AmberMap[ambers[1]];

	}

	public override bool Equals(object obj) {
		if (obj == null || !obj.GetType().Equals(this.GetType())) {
			return false;
		}
		Amber2 other = (Amber2)obj;
		return TypeEquivalent(other);
	}

	public override int GetHashCode() {
		return CustomMathematics.GetCombinedHash((int)amber0, (int)amber1);
	}

	public bool TypeEquivalent(Amber2 other) {
		//Check forward Equality
		if (Amber1.TypeEquivalent(amber0, other.amber0)) {
			return Amber1.TypeEquivalent(amber1, other.amber1);
		//Check backward Equality
		} else if (Amber1.TypeEquivalent(amber0, other.amber1)) {
			return Amber1.TypeEquivalent(amber1, other.amber0);
		}
		return false;
	}

	public bool TypeEquivalent(Amber other0, Amber other1) {
		//Check forward Equality
		if (Amber1.TypeEquivalent(amber0, other0)) {
			return Amber1.TypeEquivalent(amber1, other1);
		//Check backward Equality
		} else if (Amber1.TypeEquivalent(amber0, other1)) {
			return Amber1.TypeEquivalent(amber1, other0);
		}
		return false;
	}

	public bool TypeEquivalentOrWild(Amber2 other) {
		//Check forward Equality
		if (Amber1.TypeEquivalentOrWild(amber0, other.amber0)) {
			return Amber1.TypeEquivalentOrWild(amber1, other.amber1);
		//Check backward Equality
		} else if (Amber1.TypeEquivalentOrWild(amber0, other.amber1)) {
			return Amber1.TypeEquivalentOrWild(amber1, other.amber0);
		}
		return false;
	}

	public bool TypeEquivalentOrWild(Amber other0, Amber other1) {
		//Check forward Equality
		if (Amber1.TypeEquivalentOrWild(amber0, other0)) {
			return Amber1.TypeEquivalentOrWild(amber1, other1);
		//Check backward Equality
		} else if (Amber1.TypeEquivalentOrWild(amber0, other1)) {
			return Amber1.TypeEquivalentOrWild(amber1, other0);
		}
		return false;
	}

	public Amber2 Reversed() {
		return new Amber2(amber1, amber0);
	}
}

public struct Amber3 {
	public Amber amber0;
	public Amber amber1;
	public Amber amber2;

	public static Amber3 empty => new Amber3(Amber.X, Amber.X, Amber.X);
	public bool IsEmpty() => amber0 == Amber.X && amber1 == Amber.X && amber2 == Amber.X;

	public Amber3(Amber amber0, Amber amber1, Amber amber2) {
		this.amber0 = amber0;
		this.amber1 = amber1;
		this.amber2 = amber2;
	}

	public Amber3(string typesString) {
		string[] ambers = typesString.Split(new char[1] {'-'}, System.StringSplitOptions.None);
		
		if (ambers.Length != 3) {
			throw new System.Exception(string.Format(
				"Wrong length of 'ambers' ({0} - should be {1})",
				ambers.Length,
				3
			));
		}

		amber0 = Constants.AmberMap[ambers[0]];
		amber1 = Constants.AmberMap[ambers[1]];
		amber2 = Constants.AmberMap[ambers[2]];

	}

	public override bool Equals(object obj) {
		if (obj == null || !obj.GetType().Equals(this.GetType())) {
			return false;
		}
		Amber3 other = (Amber3)obj;
		return TypeEquivalent(other);
	}

	public override int GetHashCode() {
		return CustomMathematics.GetCombinedHash((int)amber0, (int)amber1, (int)amber2);
	}

	public bool TypeEquivalent(Amber3 other) {
		//Middle AMBER must be the same
		if (Amber1.TypeEquivalent(amber1, other.amber1)) {
			//Check forward Equality
			if (Amber1.TypeEquivalent(amber0, other.amber0)) {
				return Amber1.TypeEquivalent(amber2, other.amber2);
			//Check backward Equality
			} else if (Amber1.TypeEquivalent(amber0, other.amber2)) {
				return Amber1.TypeEquivalent(amber2, other.amber0);
			}
		}
		return false;
	}

	public bool TypeEquivalent(Amber other0, Amber other1, Amber other2) {
		//Middle AMBER must be the same
		if (Amber1.TypeEquivalent(amber1, other1)) {
			//Check forward Equality
			if (Amber1.TypeEquivalent(amber0, other0)) {
				return Amber1.TypeEquivalent(amber2, other2);
			//Check backward Equality
			} else if (Amber1.TypeEquivalent(amber0, other2)) {
				return Amber1.TypeEquivalent(amber2, other0);
			}
		}
		return false;
	}

	public bool TypeEquivalentOrWild(Amber3 other) {
		//Middle AMBER must be the same
		if (Amber1.TypeEquivalentOrWild(amber1, other.amber1)) {
			//Check forward Equality
			if (Amber1.TypeEquivalentOrWild(amber0, other.amber0)) {
				return Amber1.TypeEquivalentOrWild(amber2, other.amber2);
			//Check backward Equality
			} else if (Amber1.TypeEquivalentOrWild(amber0, other.amber2)) {
				return Amber1.TypeEquivalentOrWild(amber2, other.amber0);
			}
		}
		return false;
	}

	public bool TypeEquivalentOrWild(Amber other0, Amber other1, Amber other2) {
		//Middle AMBER must be the same
		if (Amber1.TypeEquivalentOrWild(amber1, other1)) {
			//Check forward Equality
			if (Amber1.TypeEquivalentOrWild(amber0, other0)) {
				return Amber1.TypeEquivalentOrWild(amber2, other2);
			//Check backward Equality
			} else if (Amber1.TypeEquivalentOrWild(amber0, other2)) {
				return Amber1.TypeEquivalentOrWild(amber2, other0);
			}
		}
		return false;
	}

	public Amber3 Reversed() {
		return new Amber3(amber2, amber1, amber0);
	}
}

public struct Amber4 {
	public Amber amber0;
	public Amber amber1;
	public Amber amber2;
	public Amber amber3;

	public static Amber4 empty => new Amber4(Amber.X, Amber.X, Amber.X, Amber.X);
	public bool IsEmpty() => amber0 == Amber.X && amber1 == Amber.X && amber2 == Amber.X && amber3 == Amber.X;

	public Amber4(Amber amber0, Amber amber1, Amber amber2, Amber amber3) {
		this.amber0 = amber0;
		this.amber1 = amber1;
		this.amber2 = amber2;
		this.amber3 = amber3;
	}

	public Amber4(string typesString) {
		string[] ambers = typesString.Split(new char[1] {'-'}, System.StringSplitOptions.None);
		
		if (ambers.Length != 4) {
			throw new System.Exception(string.Format(
				"Wrong length of 'ambers' ({0} - should be {1})",
				ambers.Length,
				4
			));
		}

		amber0 = Constants.AmberMap[ambers[0]];
		amber1 = Constants.AmberMap[ambers[1]];
		amber2 = Constants.AmberMap[ambers[2]];
		amber3 = Constants.AmberMap[ambers[3]];

	}

	public override bool Equals(object obj) {
		if (obj == null || !obj.GetType().Equals(this.GetType())) {
			return false;
		}
		Amber4 other = (Amber4)obj;
		return TypeEquivalent(other);
	}

	public override int GetHashCode() {
		return CustomMathematics.GetCombinedHash((int)amber0, (int)amber1, (int)amber2, (int)amber3);
	}
	
	public bool TypeEquivalent(Amber4 other) {
		//Check forward Equality
		if (Amber1.TypeEquivalent(amber0, other.amber0)) {
			return Amber1.TypeEquivalent(amber1, other.amber1)
				&& Amber1.TypeEquivalent(amber2, other.amber2)
				&& Amber1.TypeEquivalent(amber3, other.amber3);
		//Check backward Equality
		} else if (Amber1.TypeEquivalent(amber0, other.amber3)) {
			return Amber1.TypeEquivalent(amber1, other.amber2)
				&& Amber1.TypeEquivalent(amber2, other.amber1)
				&& Amber1.TypeEquivalent(amber3, other.amber0);
		}
		return false;
	}
	
	public bool TypeEquivalent(Amber other0, Amber other1, Amber other2, Amber other3) {
		//Check forward Equality
		if (Amber1.TypeEquivalent(amber0, other0)) {
			return Amber1.TypeEquivalent(amber1, other1)
				&& Amber1.TypeEquivalent(amber2, other2)
				&& Amber1.TypeEquivalent(amber3, other3);
		//Check backward Equality
		} else if (Amber1.TypeEquivalent(amber0, other3)) {
			return Amber1.TypeEquivalent(amber1, other2)
				&& Amber1.TypeEquivalent(amber2, other1)
				&& Amber1.TypeEquivalent(amber3, other0);
		}
		return false;
	}
	
	public bool TypeEquivalentOrWild(Amber4 other) {
		//Check forward Equality
		if (Amber1.TypeEquivalentOrWild(amber0, other.amber0)) {
			return Amber1.TypeEquivalentOrWild(amber1, other.amber1)
				&& Amber1.TypeEquivalentOrWild(amber2, other.amber2)
				&& Amber1.TypeEquivalentOrWild(amber3, other.amber3);
		//Check backward Equality
		} else if (Amber1.TypeEquivalentOrWild(amber0, other.amber3)) {
			return Amber1.TypeEquivalentOrWild(amber1, other.amber2)
				&& Amber1.TypeEquivalentOrWild(amber2, other.amber1)
				&& Amber1.TypeEquivalentOrWild(amber3, other.amber0);
		}
		return false;
	}
	
	public bool TypeEquivalentOrWild(Amber other0, Amber other1, Amber other2, Amber other3) {
		//Check forward Equality
		if (Amber1.TypeEquivalentOrWild(amber0, other0)) {
			return Amber1.TypeEquivalentOrWild(amber1, other1)
				&& Amber1.TypeEquivalentOrWild(amber2, other2)
				&& Amber1.TypeEquivalentOrWild(amber3, other3);
		//Check backward Equality
		} else if (Amber1.TypeEquivalentOrWild(amber0, other3)) {
			return Amber1.TypeEquivalentOrWild(amber1, other2)
				&& Amber1.TypeEquivalentOrWild(amber2, other1)
				&& Amber1.TypeEquivalentOrWild(amber3, other0);
		}
		return false;
	}

	public Amber4 Reversed() {
		return new Amber4(amber3, amber2, amber1, amber0);
	}

}