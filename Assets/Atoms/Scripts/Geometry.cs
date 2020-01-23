using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using Unity.Mathematics;
using Element = Constants.Element;
using BT = Constants.BondType;
using RS = Constants.ResidueState;
using OLID = Constants.OniomLayerID;
using EL = Constants.ErrorLevel;

/// <summary>Geometry Class</summary>
/// <remarks>
/// Contains Residues and represents a single geometry.
/// Performs tasks on collections of Residues and Atom objects.
/// Holds a reference to a Molecular Mechanics Parameters object.
/// Holds a reference to a Gaussian Calculator object.
/// </remarks>
public class Geometry : MonoBehaviour {

	/// <summary>Return the number of Atoms in this Geometry object</summary>
	public int size => 
		//Select all Residues
		residueDict.Values
			//Parallelise
			.AsParallel()
			//Add up the number of atoms in each residue
			.Sum(x => x.size);
	
	/// <summary>Return the number of Residues in this Geometry object</summary>
	public int residueCount => residueDict.Count();

	/// <summary>Return the unique chainIDs in this Geometry object</summary>
	public IEnumerable<string> GetChainIDs() => 
		//Select all ResidueIDs
		residueDict.Keys
			//Get their chainIDs
			.Select(x => x.chainID)
			//Get the unique set of chainIDs
			.Distinct();

	/// <summary>Enumerate the AtomIDs in this Geometry object</summary>
	public IEnumerable<AtomID> EnumerateAtomIDs() =>
		//Loop through all Residues
		residueDict.SelectMany(
			kvp => kvp.Value.pdbIDs
				//Yield the AtomID formed by the ResidueID and PDBID
				.Select(pdbID => new AtomID(kvp.Key, pdbID)));
	
	/// <summary>Enumerate each Atom in this Geometry object</summary>
	public IEnumerable<(AtomID, Atom)> EnumerateAtomIDPairs()  =>
		//Loop through all Residues
		residueDict.SelectMany(
			rKVP => rKVP.Value.atoms
				//Yield the AtomID formed by the ResidueID and PDBID
				.Select(aKVP => (new AtomID(rKVP.Key, aKVP.Key), aKVP.Value)));
	
	/// <summary>Enumerate each Atom in this Geometry object</summary>
	public IEnumerable<Atom> EnumerateAtoms()  =>
		//Loop through all Residues
		residueDict.SelectMany(
			rKVP => rKVP.Value.atoms
				//Yield the AtomID formed by the ResidueID and PDBID
				.Select(aKVP => aKVP.Value));

	/// <summary>Return the unique ONIOM Layer IDs in this Geometry object</summary>
	public IEnumerable<OLID> GetLayers() => 
		//Loop through each atom
		EnumerateAtoms()
			//Get all the ONIOM Layer IDs
			.Select(x => x.oniomLayer)
			//Select the unique ones
			.Distinct();

	/// <summary>Get the total charge of this Geometry object using the sum of each Atom's partialCharge.</summary>
	public float GetCharge() => 
		//Loop through each atom
		EnumerateAtoms()
			//Parallelise
			.AsParallel()
			//Get all the partial charges
			.Select(x => x.partialCharge)
			//Return the sum
			.Sum();

	/// <summary>Enumerate Residues whose ResidueIDs meet a condition.</summary>
	/// <param name="residueIDCondition">The Condition Delegate that the ResidueID must meet.</param>
	public IEnumerable<(ResidueID, Residue)> EnumerateResidues(Func<ResidueID, bool> residueIDCondition) =>
		//Loop through residueDict
		residueDict
			//Select items whose Residues have the requested Residue State
			.Where(x => residueIDCondition(x.Key))
			//Return the ResidueIDs
			.Select(x => (x.Key,x.Value));

	/// <summary>Enumerate Residues that meet a condition.</summary>
	/// <param name="residueIDCondition">The Condition Delegate that the Residue must meet.</param>
	public IEnumerable<(ResidueID, Residue)> EnumerateResidues(Func<Residue, bool> residueCondition) =>
		//Loop through residueDict
		residueDict
			//Select items whose Residues have the requested Residue State
			.Where(x => residueCondition(x.Value))
			//Return the ResidueIDs
			.Select(x => (x.Key,x.Value));

	/// <summary>Dictionary of all Residues in this Geometry object.</summary>
	public Dictionary<ResidueID, Residue> residueDict = new Dictionary<ResidueID, Residue> ();

	/// <summary>Dictionary of Residues that are missing in this Geometry object.</summary>
	public Dictionary<ResidueID, string> missingResidues = new Dictionary<ResidueID, string> ();

	/// <summary>Map of AtomIDs to Atom Nums.</summary>
	public Map<AtomID, int> atomMap;
	/// <summary>Generate a Map of AtomIDs to Atom Nums.</summary>
	public void GenerateAtomMap() {
		atomMap = EnumerateAtomIDs()
			.Select((v,i) => (v,i))
			.ToMap(x => x.v, x => x.i);
	}

	/// <summary>The reference to this Geometry's Gaussian Calculator.</summary>
	public GaussianCalculator gaussianCalculator;
	/// <summary>The reference to this Geometry's Molecular Mechanics Parameters.</summary>
	public Parameters parameters;
	
	void Awake () {
		parameters = PrefabManager.InstantiateParameters(transform);
		parameters.parent = this;
		gaussianCalculator = PrefabManager.InstantiateGaussianCalculator(transform);

	}

	void UpdateResidueDict(ResidueID residueID, string residueName) {
		// Keep track of the map between residue number and residue name
		// Check each residue to see if it's protonated and/or standard

		if (residueName == "" || residueID.residueNumber == 0) {
			//Ignore blanks
			return;
		} 
		
		if (residueDict.ContainsKey(residueID)) {
			if (residueDict[residueID].residueName != residueName) {
				throw new ErrorHandler.ResidueMismatchException(
					"Mismatch between residue number and residue name",
					residueID.residueNumber,
					residueDict[residueID].residueName,
					residueName
				);
			}
		} else {
			//New residue
			residueDict[residueID] = new Residue(residueID, residueName, this);
		}
	}

	/// <summary>Sets the Residue State of all Residues and Amber Types of their Atoms from the Standard Residues Database.</summary>
	public IEnumerator SetAllResidueProperties() {
		foreach (ResidueID residueID in residueDict.Keys) {
			SetResidueProperties(residueID);
			if (Timer.yieldNow) yield return null;
		}
	}

	/// <summary>Sets the Residue State of a Residue and Amber Types of its Atoms from the Standard Residues Database.</summary>
	/// <param name="residueID">The Residue ID of the Residue to set.</param>
	public void SetResidueProperties(ResidueID residueID) {
		Residue residue = residueDict[residueID];
		Data.SetResidueProperties(ref residue);
	}

	public void SetResidue(ResidueID residueID, Residue residue) {
		this.residueDict[residueID] = residue;
		residue.parent = this;
	}

	public void SetResidueDict(Dictionary<ResidueID, Residue> residueDict) {
		this.residueDict = residueDict;
		foreach (Residue residue in this.residueDict.Values) {
			residue.parent = this;
		}
	}

	public Geometry Take(Transform transform) {
		Geometry newGeometry = PrefabManager.InstantiateGeometry(transform);
		Parameters.Copy(this, newGeometry);
		newGeometry.missingResidues = missingResidues
			.ToDictionary(x => x.Key, x => x.Value);
		newGeometry.SetResidueDict(
			residueDict
				.ToDictionary(x => x.Key, x => x.Value.Take(newGeometry))
		);
		return newGeometry;
	}

	public Geometry Take(
		Transform transform, 
		Func<(PDBID pdbID, Atom Atom), bool> atomCondition
	) {
		Geometry newGeometry = PrefabManager.InstantiateGeometry(transform);
		Parameters.Copy(this, newGeometry);
		newGeometry.SetResidueDict(
			residueDict
				.ToDictionary(x => x.Key, x => x.Value.Take(newGeometry, atomCondition))
		);
		return newGeometry;
	}

	public Geometry CopyTo(Geometry newGeometry) {
		Parameters.Copy(this, newGeometry);
		newGeometry.missingResidues = missingResidues
			.ToDictionary(x => x.Key, x => x.Value);
		newGeometry.SetResidueDict(
			residueDict
				.ToDictionary(x => x.Key, x => x.Value.Take(newGeometry))
		);
		return newGeometry;
	}

	public Geometry TakeResidue(ResidueID residueID, Transform transform) {
		Geometry newGeometry = PrefabManager.InstantiateGeometry(transform);
		Parameters.Copy(this, newGeometry);
		newGeometry.SetResidueDict(
			residueDict
				.Where(x => x.Key == residueID)
				.ToDictionary(x => x.Key, x => x.Value.Take(newGeometry))
		);
		return newGeometry;
	}

	public Geometry TakeResidues(IEnumerable<ResidueID> residueIDs, Transform transform) {
		Geometry newGeometry = PrefabManager.InstantiateGeometry(transform);
		Parameters.Copy(this, newGeometry);
		newGeometry.SetResidueDict(
			residueDict
				.Where(x => residueIDs.Contains(x.Key))
				.ToDictionary(x => x.Key, x => x.Value.Take(newGeometry))
		);
		return newGeometry;
	}

	public Geometry TakeResidues(
		IEnumerable<ResidueID> residueIDs, 
		Transform transform, 
		Func<(PDBID pdbID, Atom Atom), bool> atomCondition
	) {
		Geometry newGeometry = PrefabManager.InstantiateGeometry(transform);
		Parameters.Copy(this, newGeometry);
		newGeometry.SetResidueDict(
			residueDict
				.Where(x => residueIDs.Contains(x.Key))
				.ToDictionary(x => x.Key, x => x.Value.Take(newGeometry, atomCondition))
		);
		return newGeometry;
	}

	public Geometry TakeLayer(OLID oniomLayer, Transform transform) {
		Geometry newGeometry = PrefabManager.InstantiateGeometry(transform);
		Parameters.Copy(this, newGeometry);
		newGeometry.SetResidueDict(
			residueDict
				.ToDictionary(x => x.Key, x => x.Value.TakeLayer(oniomLayer, newGeometry))
				.Where(x => x.Value != null)
				.ToDictionary(x => x.Key, x => x.Value)
		);
		return newGeometry;
	}

	public Geometry TakeChain(string chainID, Transform transform) {
		Geometry newGeometry = PrefabManager.InstantiateGeometry(transform);
		Parameters.Copy(this, newGeometry);
		newGeometry.missingResidues = missingResidues
			.Where(x => x.Key.chainID == chainID)
			.ToDictionary(x => x.Key, x => x.Value);
		newGeometry.SetResidueDict(
			residueDict
				.Where(x => x.Key.chainID == chainID)
				.ToDictionary(x => x.Key, x => x.Value)
		);
		return newGeometry;
	}

	public Geometry TakeDry(Transform transform) {
		Geometry newGeometry = PrefabManager.InstantiateGeometry(transform);
		Parameters.Copy(this, newGeometry);
		newGeometry.SetResidueDict(
			residueDict
				.Where(x => x.Value.state != RS.WATER)
				.ToDictionary(x => x.Key, x => x.Value)
		);
		return newGeometry;
	}

	public void Connect(AtomID atomID0, AtomID atomID1, BT bondType) {
		if (bondType == BT.NONE) {
			Disconnect(atomID0, atomID1);
		} else {
			if (atomID0.residueID == atomID1.residueID) {
				GetAtom(atomID0).internalConnections[atomID1.pdbID] = bondType;
				GetAtom(atomID1).internalConnections[atomID0.pdbID] = bondType;
			} else {
				GetAtom(atomID0).externalConnections[atomID1] = bondType;
				GetAtom(atomID1).externalConnections[atomID0] = bondType;
			}
		}
	}
	public void Disconnect(AtomID atomID0, AtomID atomID1) {
		if (atomID0.residueID == atomID1.residueID) {
			GetAtom(atomID0)?.internalConnections.Remove(atomID1.pdbID);
			GetAtom(atomID1)?.internalConnections.Remove(atomID0.pdbID);
		} else {
			GetAtom(atomID0)?.externalConnections.Remove(atomID1);
			GetAtom(atomID1)?.externalConnections.Remove(atomID0);
		}
	}

	public bool ContainsResidue(ResidueID residueID) {
		return residueDict.ContainsKey(residueID);
	}

	public bool ContainsAtom(AtomID atomID) {
		(ResidueID residueID, PDBID pdbID) = atomID;
		return ContainsAtom(residueID, pdbID);
	}

	public bool ContainsAtom(ResidueID residueID, PDBID pdbID) {
		Residue residue;
		if (TryGetResidue(residueID, out residue)) {
			return residue.Contains(pdbID);
		}
		return false;
	}

	public Atom GetAtom(AtomID atomID) {
		(ResidueID residueID, PDBID pdbID) = atomID;
		return GetAtom(residueID, pdbID);
	}

	public Atom GetAtom(ResidueID residueID, PDBID pdbID) {
		return residueDict[residueID].atoms[pdbID];
	}

	public bool TryGetAtom(AtomID atomID, out Atom atom) {
		(ResidueID residueID, PDBID pdbID) = atomID;
		return TryGetAtom(residueID, pdbID, out atom);
	}

	public bool TryGetAtom(ResidueID residueID, PDBID pdbID, out Atom atom) {
		Residue residue;
		atom = null;
		if (! TryGetResidue(residueID, out residue)) return false;
		residue.atoms.TryGetValue(pdbID, out atom);
		return (atom != null);
	}

	public Residue GetResidue(ResidueID residueID) {
		return residueDict[residueID];
	} 

	public bool TryGetResidue(ResidueID residueID, out Residue residue) {
		return residueDict.TryGetValue(residueID, out residue);
	}

	public Bounds GetBounds() {
		return new Bounds(this);
	}

	public List<Link> GetLinks() {

		List<Link> links = new List<Link>();

		foreach (ResidueID residueID in residueDict.Keys) {
			Residue residue = residueDict[residueID];
			//foreach (KeyValuePair<PDBID, Atom> keyValuePair in residue.atoms) {
			foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
				OLID connectionLayerID = atom.oniomLayer;

				//Only look at intermediate and model layers for speed
				if (connectionLayerID == OLID.REAL) {
					continue;
				}

				foreach (AtomID neighbourID in atom.EnumerateNeighbours()) {
					Atom hostAtom;
					if (!TryGetAtom(neighbourID, out hostAtom)) {
						continue;
					}

					OLID hostLayerID = hostAtom.oniomLayer;
					if (hostLayerID < connectionLayerID) {
						//Found a link atom

						links.Add(new Link(
							hostLayerID, 
							connectionLayerID,
							neighbourID,
							new AtomID(residueID, pdbID)
						));
					}
				}
			}
		}

		return links;
	}

	public IEnumerator UpdateFrom(
		Geometry other, 
		bool updatePositions=false,
		bool updateCharges=false,
		bool updateAmbers=false
	) {
		foreach ((AtomID atomID, Atom otherAtom) in other.EnumerateAtomIDPairs()) {
			Atom thisAtom;
			if (TryGetAtom(atomID, out thisAtom)) {
				if (updatePositions) {
					thisAtom.position = otherAtom.position.xyz;
				}
				if (updateCharges) {
					thisAtom.partialCharge = otherAtom.partialCharge;
				}
				if (updateCharges) {
					thisAtom.amber = otherAtom.amber;
				}
			}
			if (Timer.yieldNow) {
				yield return null;
			}
		}
	}

	public IEnumerator FitAgainst(Geometry other, List<AtomID> atomsToFit=null) {
		//Translate and rotate these Atoms' positions to fit against other
		//Can provide a List of AtomIDs to use
		//Note this is not the best way to align geometries, but no SVD implementation in Unity or Math

		int numAtomsToFit;
		if (atomsToFit == null) {

			List<PDBID> pdbIDs = new List<PDBID> {
				PDBID.N,  //First try backbone N's
				new PDBID(Element.N, "*"), //Then try all N's
				new PDBID(Element.O, "*"), //Then try all O's
				new PDBID(Element.C, "*")  //Then try all C's
			};

			numAtomsToFit = 0;

			atomsToFit = new List<AtomID>();
			foreach (PDBID pdbID in pdbIDs) {
				CustomLogger.LogFormat(EL.DEBUG, "PDBID {0}", pdbID);
				foreach (ResidueID thisResidueID in residueDict.Keys) {
					CustomLogger.LogFormat(EL.DEBUG, "ResidueID {0}", thisResidueID);
					Residue thisResidue = residueDict[thisResidueID];
					Residue otherResidue;
					if (!other.residueDict.TryGetValue(thisResidueID, out otherResidue)) {
						//Other Geometry doesn't have the same Residue
						CustomLogger.Log(EL.DEBUG, "otherResidue doesn't contain ResidueID");
						continue;
					}
					if (thisResidue.residueName != otherResidue.residueName) {
						//Residues not the same
						CustomLogger.LogFormat(EL.DEBUG, "Names don't match: {0} {1}", thisResidue.residueName, otherResidue.residueName);
						continue;
					}
					if (Timer.yieldNow) {yield return null;}

					if (pdbID.identifier != "*") {
						if (!thisResidue.atoms.ContainsKey(pdbID)) {
							//This residue doesn't have pdbID
							CustomLogger.LogFormat(EL.DEBUG, "thisResidue doesn't contain PDBID: {0}", pdbID);
							continue;
						}
						if (!otherResidue.atoms.ContainsKey(pdbID)) {
							//Other residue doesn't have pdbID
							CustomLogger.LogFormat(EL.DEBUG, "otherResidue doesn't contain PDBID: {0}", pdbID);
							continue;
						}
						AtomID atomID = new AtomID(thisResidueID, pdbID);
						if (atomsToFit.Contains(atomID)) {
							continue;
						}
						CustomLogger.LogFormat(EL.DEBUG, "Adding AtomID: {0}", atomID);
						atomsToFit.Add(atomID);
					} else {
						foreach (PDBID thisPDBID in thisResidue.atoms.Keys) {
							if (thisPDBID.element != pdbID.element) {
								continue;
							}
							if (!otherResidue.atoms.ContainsKey(thisPDBID)) {
								//Other residue doesn't have pdbID
								CustomLogger.LogFormat(EL.DEBUG, "otherResidue doesn't contain PDBID: {0}", thisPDBID);
								continue;
							}
							AtomID atomID = new AtomID(thisResidueID, thisPDBID);
							if (atomsToFit.Contains(atomID)) {
								continue;
							}
							CustomLogger.LogFormat(EL.DEBUG, "Adding AtomID: {0}", atomID);
							atomsToFit.Add(atomID);

						}
					}
				}

				if (atomsToFit.Count > 2) {break;}
			}
		}
		numAtomsToFit = atomsToFit.Count;
		if (numAtomsToFit < 3) {
			CustomLogger.Log(EL.ERROR, "Need at least 2 atoms to fit");
			yield break;
		}

		CustomLogger.LogFormat(EL.VERBOSE, "Alignment using {0} atoms", numAtomsToFit);
		CustomLogger.LogFormat(
			EL.INFO, 
			"Atoms to align: ['{0}']", 
			() => new object[1] {
				string.Join("', '", atomsToFit)
			}
		);

		//Get a list of positions for each Atoms
		float3[] thisPositions = atomsToFit
			.Select(x => GetAtom(x).position.xyz)
			.ToArray();
		float3[] otherPositions = atomsToFit
			.Select(x => other.GetAtom(x).position.xyz)
			.ToArray();

		if (Timer.yieldNow) {yield return null;}

		//Calculate the average position
		float3 thisAverage = CustomMathematics.Average(thisPositions);

		CustomLogger.LogFormat(
			EL.VERBOSE, 
			"Average position of Atoms to fit: {0}", 
			thisAverage
		);

		float3 otherAverage = CustomMathematics.Average(otherPositions);

		CustomLogger.LogFormat(
			EL.VERBOSE, 
			"Average position of Atoms to fit against: {0}", 
			otherAverage
		);

		if (Timer.yieldNow) {yield return null;}

		//Subtract in place
		for (int i=0; i<numAtomsToFit; i++) {
			thisPositions[i] -= thisAverage;
			otherPositions[i] -= otherAverage;
		}
		
		if (Timer.yieldNow) {yield return null;}

		//Get all rotation matrices
		float3x3[] rotationMatrices = Enumerable.Range(0, numAtomsToFit)
			.Select(x => CustomMathematics.GetRotationMatrix(
				math.normalize(thisPositions[x]), 
				math.normalize(otherPositions[x])
			))
			.Where(x => {
				//Make sure there are no NaNs
				for (int i=0; i<3; i++) {
					if (math.any(math.isnan(x[i]))) {
						return false;
					}
				}
				return true;
			})
			.ToArray();

		if (rotationMatrices.Length == 0) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Unable to find a rotation matrix for alignment"
			);
			yield break;
		}

		//Get average rotation matrix
		float3x3 averageRotationMatrix = CustomMathematics.Average(rotationMatrices);

		CustomLogger.LogFormat(
			EL.VERBOSE, 
			"Average Rotation Matrix: {0}", 
			averageRotationMatrix
		);

		float3 translation = otherAverage - CustomMathematics.Dot(averageRotationMatrix, thisAverage);

		CustomLogger.LogFormat(
			EL.VERBOSE, 
			"Translation Vector: {0}", 
			translation
		);

		foreach (Atom atom in EnumerateAtoms()) {
			atom.position = CustomMathematics.Dot(averageRotationMatrix, atom.position) + translation;

			if (Timer.yieldNow) {yield return null;}
		}

	}

	///<summary>Enumerates the Atoms that are linked to startAtomID.</summary>
	///<remarks>Used for separating each side of a bond for transformations.</remarks>
	///<param name="startAtomID">AtomID of Atom to start expansion from.</param>
	///<param name="excludeList">(optional) HashSet containing AtomIDs to discard. Search will stop at these AtomIDs.</param>
	///<param name="depth">(optional) maximum distance to expand mask by. A negative value will mean all connected Atom objects are selected.</param>
	public IEnumerable<(AtomID, int)> GetConnectedAtomIDs(AtomID startAtomID, HashSet<AtomID> excludeList=null, int depth=-1) {

		//Create a Stack and add the first Atom along with a depth of 1
		Stack<(int, AtomID)> stack = new Stack<(int, AtomID)>();
		stack.Push((0, startAtomID));

		if (excludeList == null) {
			excludeList = new HashSet<AtomID>();
		}

		//Grow the mask until no more Atom objects are available
		while (stack.Count != 0) {
			//Current AtomID to look at
			(int currentDepth, AtomID atomID) = stack.Pop();

			//Skip if it's been seen, should be excluded or depth has been reached
			if (excludeList.Contains(atomID) || currentDepth == depth) {continue;}

			Atom connectedAtom;
			if (!TryGetAtom(atomID, out connectedAtom)) {
				continue;
			}

			//This AtomID is now seen
			excludeList.Add(atomID);

			//Yield the AtomID
			yield return (atomID, currentDepth);

			//Add all the neighbouring AtomIDs to the Stack
			foreach (AtomID neighbourID in connectedAtom.EnumerateNeighbours()) {
				stack.Push(((currentDepth + 1, neighbourID)));
			}
		}
	}

	///<summary>Enumerates the Atoms that are linked to startAtomID.</summary>
	///<remarks>Used for separating each side of a bond for transformations.</remarks>
	///<param name="startAtomID">AtomID of Atom to start expansion from.</param>
	///<param name="excludeList">(optional) HashSet containing AtomIDs to discard. Search will stop at these AtomIDs.</param>
	///<param name="depth">(optional) maximum distance to expand mask by. A negative value will mean all connected Atom objects are selected.</param>
	public IEnumerable<(Atom, AtomID, int)> GetConnectedAtoms(AtomID startAtomID, HashSet<AtomID> excludeList=null, int depth=-1) {

		//Create a Stack and add the first Atom along with a depth of 1
		Stack<(int, AtomID)> stack = new Stack<(int, AtomID)>();
		stack.Push((0, startAtomID));

		if (excludeList == null) {
			excludeList = new HashSet<AtomID>();
		}

		//Grow the mask until no more Atom objects are available
		while (stack.Count != 0) {
			//Current AtomID to look at
			(int currentDepth, AtomID atomID) = stack.Pop();

			//Skip if it's been seen, should be excluded or depth has been reached
			if (excludeList.Contains(atomID) || currentDepth == depth) {continue;}

			Atom connectedAtom;
			if (!TryGetAtom(atomID, out connectedAtom)) {
				continue;
			}

			//This AtomID is now seen
			excludeList.Add(atomID);

			//Yield the AtomID
			yield return (connectedAtom, atomID, currentDepth);

			//Add all the neighbouring AtomIDs to the Stack
			foreach (AtomID neighbourID in connectedAtom.EnumerateNeighbours()) {
				stack.Push(((currentDepth + 1, neighbourID)));
			}
		}
	}
	
	///<summary>Enumerates Residues Atom that are linked to startResidueIDs.</summary>
    ///<param name="startResidueIDs">Group containing initial Residue</param>
    ///<param name="state">Target Residue State of Residue Group</param>
    ///<param name="excludeList">HashSet of Residue IDs to exclude from Group</param>
    ///<param name="depth">Maximum depth of neighbouring Residues to add. A negative value will allow all Residues to be searched</param>
    public IEnumerable<(ResidueID, int)> GetConnectedResidueIDs(
        HashSet<ResidueID> startResidueIDs, 
        RS state, 
        HashSet<ResidueID> excludeList=null, 
        int depth=-1
    ) {

        Stack<(int, ResidueID)> stack = new Stack<(int, ResidueID)>();
        foreach (ResidueID residueID in startResidueIDs) {
            stack.Push((0, residueID));
        }

        if (excludeList == null) {
            excludeList = new HashSet<ResidueID>();
        }

        while (stack.Count != 0) {
            (int currentDepth, ResidueID residueID) = stack.Pop();

            if (excludeList.Contains(residueID) || currentDepth == depth) {continue;}

            excludeList.Add(residueID);

            yield return (residueID, currentDepth);

            foreach (ResidueID neighbourResidueID in GetResidue(residueID).NeighbouringResidues()) {
                if (GetResidue(neighbourResidueID).state == state) {
                    stack.Push((currentDepth + 1, neighbourResidueID));
                }
            }
        }
    }

	///<summary>Enumerates Residues Atom that are linked to startResidueIDs.</summary>
    ///<param name="startResidueIDs">Group containing initial Residue</param>
    ///<param name="state">Target Residue State of Residue Group</param>
    ///<param name="excludeList">HashSet of Residue IDs to exclude from Group</param>
    ///<param name="depth">Maximum depth of neighbouring Residues to add. A negative value will allow all Residues to be searched</param>
    public IEnumerable<(Residue, ResidueID, int)> GetConnectedResidues(
        HashSet<ResidueID> startResidueIDs, 
        RS state, 
        HashSet<ResidueID> excludeList=null, 
        int depth=-1
    ) {

        Stack<(int, ResidueID)> stack = new Stack<(int, ResidueID)>();
        foreach (ResidueID residueID in startResidueIDs) {
            stack.Push((0, residueID));
        }

        if (excludeList == null) {
            excludeList = new HashSet<ResidueID>();
        }

        while (stack.Count != 0) {
            (int currentDepth, ResidueID residueID) = stack.Pop();

            if (excludeList.Contains(residueID) || currentDepth == depth) {continue;}

            excludeList.Add(residueID);

			Residue connectedResidue;
			if (!TryGetResidue(residueID, out connectedResidue)) {
				continue;
			}

            yield return (connectedResidue, residueID, currentDepth);

            foreach (ResidueID neighbourResidueID in GetResidue(residueID).NeighbouringResidues()) {
                if (GetResidue(neighbourResidueID).state == state) {
                    stack.Push((currentDepth + 1, neighbourResidueID));
                }
            }
        }
    }

	///<summary>Enumerates the Residues whose Atoms are linked to startResidueID</summary>
	///<param name="startResidueID">ResidueID of Residue to start expansion from.</param>
	///<param name="excludeList">(optional) HashSet containing ResidueIDs to discard. Search will stop at these ResidueIDs.</param>
	///<param name="depth">(optional) maximum distance to expand mask by. A negative value will mean all connected Residues are selected.</param>
	public IEnumerable<ResidueID> GetConnectedResidueIDs(ResidueID startResidueID, HashSet<ResidueID> excludeList=null, int depth=-1) {

		//Create a Stack and add the first Atom along with a depth of 1
		Stack<(int, ResidueID)> stack = new Stack<(int, ResidueID)>();
		stack.Push(ValueTuple.Create(1, startResidueID));

		if (excludeList == null) {
			excludeList = new HashSet<ResidueID>();
		}

		//Grow the mask until no more Residues are available
		while (stack.Count != 0) {
			//Current ResidueID to look at
			(int currentDepth, ResidueID residueID) = stack.Pop();

			//Skip if it's been seen, should be excluded or depth has been reached
			if (excludeList.Contains(residueID) || currentDepth == depth) {continue;}

			//This ResidueID is now seen
			excludeList.Add(residueID);

			//Yield the ResidueID
			yield return residueID;

			//Add all the neighbouring AtomIDs to the Stack
			foreach (ResidueID neighbourID in GetResidue(residueID).NeighbouringResidues()) {
				stack.Push((currentDepth + 1, neighbourID));
			}
		}
	}

	public IEnumerable<List<ResidueID>> GetGroupedResidues() {
		
        List<List<ResidueID>> groups = new List<List<ResidueID>>();
		
        foreach (ResidueID residueID in residueDict.Keys) {
            // Check if residueID is grouped already
            if (groups.Any(x => x.Any(y => y == residueID))) {continue;}

			List<ResidueID> group = GetConnectedResidueIDs(residueID).ToList();

            yield return group;

			groups.Add(group);
		}
	}

	public IEnumerator GetUserSelection(List<(AtomID, Atom)> selection, string initialSelectionString="all") {

		MultiPrompt multiPrompt = MultiPrompt.main;

		string defaultDescription = "Input Atoms selection:";;

		multiPrompt.Initialise(
			"Select Atoms", 
			defaultDescription,
			new ButtonSetup(text:"Confirm", action:() => {}),
			new ButtonSetup(text:"Cancel", action:() => multiPrompt.Cancel()),
			input:true
		);

		bool validInput = false;
		while (!validInput) {
			while (!multiPrompt.userResponded) {
				yield return null;
			}

			if (multiPrompt.cancelled) {
				break;
			}

			yield return null;

			try {
				selection = GetSelection(multiPrompt.inputField.text).ToList();
			} catch {
				multiPrompt.description.text = "Invalid selection string!";
				multiPrompt.userResponded = false;
				
				IEnumerator ResetText() {
					yield return new WaitForSeconds(5);
					multiPrompt.description.text = defaultDescription;
				}

				StartCoroutine(ResetText());

				continue;
			}
			validInput = true;
		}
		
		multiPrompt.Hide();
		
		if (multiPrompt.cancelled) {
			selection = null;
		}
	}

	public IEnumerable<(AtomID, Atom)> GetSelection(string selectionString) {


		Func<(AtomID, Atom), bool> GetSelector(string str) {
			if (str.StartsWith("[")) {
				// Element
				str = str.TrimStart(new char[] {'['}).TrimEnd(new char[] {']'});
				Element element;
				if (!Constants.ElementMap.TryGetValue(str, out element)) {
					throw new SystemException(string.Format(
						"Element '{0}' not found!",
						str
					));
				}
				return x => x.Item1.pdbID.element == element;
			} else if (str.StartsWith("{")) {
				// AtomID
				str = str.TrimStart(new char[] {'{'}).TrimEnd(new char[] {'}'});
				AtomID atomID = AtomID.FromString(str);
				return x => x.Item1 == atomID;
			} else {
				// ResidueID

				if (str.Contains('-')) {
					string[] residueIDstrings = str.Split(new char[] {'-'}, System.StringSplitOptions.RemoveEmptyEntries);
					if (residueIDstrings.Length != 2) {
						throw new SystemException(string.Format(
							"Invalid number of range tokens (-) in string '{0}'!",
							str
						));
					}
					ResidueID startResidueID = ResidueID.FromString(residueIDstrings[0]);
					ResidueID endResidueID = ResidueID.FromString(residueIDstrings[1]);

					if (startResidueID.chainID != endResidueID.chainID) {
						throw new SystemException(string.Format(
							"Chains must be identical in range: '{0}'!",
							str
						));
					}

					if (startResidueID.residueNumber >= endResidueID.residueNumber) {
						throw new SystemException(string.Format(
							"Start Residue Number must be less than end Residue Number: '{0}'!",
							str
						));
					}

					List<ResidueID> residueIDs = new List<ResidueID> {startResidueID};
					while (startResidueID != endResidueID) {
						startResidueID = startResidueID.GetNextID();
						residueIDs.Add(startResidueID);
					}

					return x => residueIDs.Contains(x.Item1.residueID);

				} else {
					ResidueID residueID = ResidueID.FromString(str.Trim());
					return x => x.Item1.residueID == residueID;
				}


			}
		}

		void Interpret(IEnumerable<(AtomID, Atom)> selection, string str) {
			if (str.StartsWith("&")) {
				str = str.TrimStart(new char[] {'&'}).Trim();
				if (str.StartsWith("!")) {
					str = str.TrimStart(new char[] {'!'}).Trim();
					var selector = GetSelector(str);
					selection = selection.Where(x => !selector(x));
				} else {
					var selector = GetSelector(str);
					selection = selection.Where(x => selector(x));
				}
			} else {
				var selector = GetSelector(str);
				selection.Concat(EnumerateAtomIDPairs().Where(x => selector(x)));
			}
		}

		IEnumerable<(AtomID, Atom)> currentSelection = EnumerateAtomIDPairs();

		if (string.Equals(selectionString, "all", StringComparison.CurrentCultureIgnoreCase)) {
			return currentSelection;
		}

		string[] stringArray = selectionString.Split(new char[] {','}, System.StringSplitOptions.RemoveEmptyEntries);

		foreach (string str in stringArray) {
			string newStr = str.Trim();
			Interpret(currentSelection, newStr);
		}

		return currentSelection;

	}

	///<summary>Returns the number of edges between two Atoms.</summary>
	///<remarks>Returns 0 in the case that the AtomIDs are the same.</remarks>
	///<remarks>Returns -1 if the search is exhausted or depth is reached.</remarks>
	///<param name="startAtomID">AtomID of Atom to start search from.</param>
	///<param name="endAtomID">AtomID of Atom to get distance from.</param>
	///<param name="depth">(optional) maximum distance to expand mask by. A negative value will mean all connected Atom objects are checked.</param>
	public int GetGraphDistance(AtomID startAtomID, AtomID endAtomID, int depth=-1) {

		//Same Atom => 0
		if (startAtomID == endAtomID) {return 0;}

		//Create a Stack and add the first Atom along with a depth of 1
		Stack<(int, AtomID)> stack = new Stack<(int, AtomID)>();
		stack.Push((0, startAtomID));

		HashSet<AtomID> excludeList = new HashSet<AtomID>();
		

		//Grow the mask until no more Atom objects are available
		while (stack.Count != 0) {
			//Current AtomID to look at
			(int currentDepth, AtomID atomID) = stack.Pop();

			if (atomID == endAtomID) {return currentDepth;}

			//Skip if it's been seen, should be excluded or depth has been reached
			if (excludeList.Contains(atomID) || currentDepth == depth) {continue;}

			//This AtomID is now seen
			excludeList.Add(atomID);

			//Add all the neighbouring AtomIDs to the Stack
			foreach (AtomID neighbourID in GetAtom(atomID).EnumerateNeighbours()) {
				stack.Push(((currentDepth + 1, neighbourID)));
			}
		}

		//No more Atoms to search => -1
		return -1;
	}

	/// <summary>Change the distance between two Atoms.</summary>
	/// <param name="atomID0">AtomID of atom0 to be moved.</param>
	/// <param name="atomID1">AtomID of atom1 to be moved.</param>
	/// <param name="newLength">New distance between atom0 and atom1.</param>
	/// <param name="pivot">The point that remains stationary during transformation.</param>
	/// <param name="moveGroup0">Should all the atoms attached to atom0 move too?</param>
	/// <param name="moveGroup1">Should all the atoms attached to atom1 move too?</param>
	/// <remarks>moveGroup0 and moveGroup1 are set to false if mask0 and mask1 intersect.</remarks>
	public void ModifyBond(AtomID atomID0, AtomID atomID1, float newLength, float pivot, bool moveGroup0, bool moveGroup1) {
		
		Atom atom0 = GetAtom(atomID0);
		Atom atom1 = GetAtom(atomID1);
		
		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Modifying bond '{0}'-'{1}'. Old Length: {2} New Length: {3}. Pivot point: {4}. Move Group 0? {5}. Move Group 1? {6}",
			() => new object[] {
				atomID0,
				atomID1,
				CustomMathematics.GetDistance(atom0, atom1),
				newLength,
				pivot,
				moveGroup0,
				moveGroup1
			}
		);
		
		//Get all the atoms attached to atom0 and atom1
		List<AtomID> mask0 = GetConnectedAtomIDs(atomID0, new HashSet<AtomID> {atomID1}).Select(x => x.Item1).ToList();
		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Mask 0 has {0} atoms",
			() => new object[] {mask0.Count()}
		);
		CustomLogger.LogFormat(
			EL.DEBUG,
			"Mask 0: {0}",
			() => new object[] {string.Join(", ", mask0)}
		);

		List<AtomID> mask1 = GetConnectedAtomIDs(atomID1, new HashSet<AtomID> {atomID0}).Select(x => x.Item1).ToList();
		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Mask 1 has {0} atoms",
			() => new object[] {mask1.Count()}
		);
		CustomLogger.LogFormat(
			EL.DEBUG,
			"Mask 1: {0}",
			() => new object[] {string.Join(", ", mask1)}
		);

		if (mask0.Any(x => mask1.Contains(x))) {
			// atom0 and atom1 are members of a loop - their masks intersect
			// Only move atom0 and atom1, not the atoms of their masks
			if (moveGroup0) {
				CustomLogger.LogFormat(
					EL.WARNING, 
					"Cannot move group 0 ('{0}'). Mask intersects '{1}' - they are part of a loop.", 
					atomID0,
					atomID1
				);
				moveGroup0 = false;
			}
			if (moveGroup1) {
				CustomLogger.LogFormat(
					EL.WARNING, 
					"Cannot move group 1 ('{1}'). Mask intersects '{0}' - they are part of a loop.", 
					atomID0,
					atomID1
				);
				moveGroup1 = false;
			}
		}

		// Calculate the displacement vectors to move atoms 
		float3 displacement0 = CustomMathematics.GetOffsetVectorFromNewDistance(
			atom0, 
			atom1, 
			newLength, 
			pivot
		);
		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Displacement Vector for Group 0: {0}",
			displacement0
		);

		float3 displacement1 = CustomMathematics.GetOffsetVectorFromNewDistance(
			atom0, 
			atom1, 
			newLength, 
			pivot - 1 //Use (pivot - 1) to get the correct direction and pivot
		);
		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Displacement Vector for Group 1: {0}",
			displacement1
		);

		//Apply displacement to atom0 or mask0
		if (moveGroup0) {
			foreach(AtomID maskedAtomID in mask0) {
				GetAtom(maskedAtomID).position += displacement0;
			}
		} else {
			atom0.position += displacement0;
		}
		
		//Apply displacement to atom1 or mask1
		if (moveGroup1) {
			foreach(AtomID maskedAtomID in mask1) {
				GetAtom(maskedAtomID).position += displacement1;
			}
		} else {
			atom1.position += displacement1;
		}
		
	}

}



/// <summary>Connection Class</summary>
/// <remarks>Describes a bond to an Atom referenced by its Atom ID.</remarks>
public class Connection {

	///<summary>The ID of the Atom that this Connection references.</summary>
	public AtomID atomID;
	///<summary>The Bond Type of this Connection.</summary>
	public BT bondType;

	///<summary>Creates a Connection object</summary>
	///<param name="atomID">The ID of the Atom that this Connection references.</param>
	///<param name="bondType">The Bond Type of this Connection.</param>
	public Connection(AtomID atomID, BT bondType) {
		this.atomID = atomID;
		this.bondType = bondType;
	}

	///<summary>Creates a Connection object</summary>
	///<param name="residueID">The Residue ID of the Atom that this Connection references.</param>
	///<param name="pdbID">The PDB ID of the Atom that this Connection references.</param>
	///<param name="bondType">The Bond Type of this Connection.</param>
	public Connection(ResidueID residueID, PDBID pdbID, BT bondType) {
		this.atomID = new AtomID(residueID, pdbID);
		this.bondType = bondType;
	}

	///<summary>Gets the string form of this connection in the form {AtomID}({BondType})</summary>
	public override string ToString() {
		return string.Format("{0}({1})", atomID, bondType);
	}

	///<summary>Creates a Connection object from a string</summary>
	///<param name="connectionString">A string in the form {AtomID}({BondType}) for an Internal Connection and [{ResidueID}]{AtomID}({BondType}) for an External Connection.</param>
	///<param name="residueID">The ID of the Residue containing the Atom that this Connection is coming from.</param>
	///<param name="residueNameDict">The dictionary of Residue IDs to Residue Names. Used for special Hetero-Residues such as metal ions.</param>
	public static Connection FromString(string connectionString, ResidueID residueID, Dictionary<ResidueID, string> residueNameDict) {
		//Use this for reading ToString() output e.g. XATWriter
		string[] splitString = connectionString.Split('(', ')');

		//Define the Bond Type
		BT bondType = BT.SINGLE;
		if (splitString.Length > 1) {
			string bondString = splitString[1];
			bondType = Constants.BondTypeMap[bondString]; 
		}

		string[] atomSplitString = splitString[0].Split('[', ']');

		PDBID pdbID;
		AtomID atomID;
		if (atomSplitString.Length > 1) {
			ResidueID externalResidueID = ResidueID.FromString(atomSplitString[1]);
			pdbID = PDBID.FromString(atomSplitString[2], residueNameDict[externalResidueID]);

			if (pdbID.IsEmpty()) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Unable to generate connection from PDBID string '{0}'",
					atomSplitString[2]
				);
				return new Connection(AtomID.Empty, BT.NONE);
			}

			atomID =  new AtomID(externalResidueID, pdbID);
		} else {
			pdbID = PDBID.FromString(atomSplitString[0], residueNameDict[residueID]);

			if (pdbID.IsEmpty()) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Unable to generate connection from PDBID string '{0}'",
					atomSplitString[0]
				);
				return new Connection(AtomID.Empty, BT.NONE);
			}

			atomID =  new AtomID(residueID, pdbID);
		}

		return new Connection(atomID, bondType);
	}

	///<summary>Deconstruct this Connection into an AtomID and BondType</summary>
	///<param name="atomID">This Connection's AtomID.</param>
	///<param name="bondType">This Connection's BondType.</param>
	public void Deconstruct(out AtomID atomID, out BT bondType) {
		atomID = this.atomID;
		bondType = this.bondType;
	}
}


/// <summary>Geometry Bounds Structure</summary>
/// <remarks>Calculates and stores the centre, lower and upper bounds of an Geometry object.</remarks>
public struct Bounds {
	/// <summary>The Centre of the Geometry object.</summary>
	public float3 centre;
	/// <summary>The most negative Cartesian position in this Geometry object</summary>
	public float3 minBound;
	/// <summary>The most positive Cartesian position in this Geometry object</summary>
	public float3 maxBound;

	/// <summary>Gets the Bounds of an Geometry object.</summary>
	/// <param name="geometry">The Geometry object to calculate the Bounds of.</param>
	public Bounds(Geometry geometry) {

		int _size = 0;
		centre = new float3();
		minBound = new float3();
		maxBound = new float3();
		foreach (Atom atom in geometry.EnumerateAtoms()) {
			float3 position = atom.position;
			//Initiate bounds
			if (_size == 0) {
				maxBound.xyz = minBound.xyz = position.xyz;
			}

			centre += position;
			minBound = math.min(minBound, position);
			maxBound = math.max(maxBound, position);

			_size++;
        }

		if (_size != 0) {
			centre /= _size;
		}

	}

	/// <summary>The greatest distance from the centre of the Geometry object to its edge.</summary>
	public float GetRadius() {
		return math.length(math.max(math.abs(centre - minBound), centre - maxBound));
	}

	public override string ToString() {
		return string.Format(
			"Bounds(centre: {0}, minBound: {1}, maxBound: {2}, radius: {3})",
			centre,
			minBound,
			maxBound,
			GetRadius()
		);
	}
}

/// <summary>ONIOM Layer Link Structure</summary>
/// <remarks>Describes a Link between two ONIOM Layers.</remarks>
public struct Link {
	//
	// LW Chung - ‎2015
	// The ONIOM Method and Its Applications
	//
	// Quick reference:
	//
	// ********************************************
	//  MODEL          REAL
	//
	// Connection       Host *
	//   atom           atom * 
	//                       *    REAL PICTURE
	//    C ------|------ C  * 
	//                       *  
	// ********************************************
	//                       *  
	//    C ------|- H       * 
	//                       *    ONIOM PICTURE
	// Connection   Link     *
	//   atom       atom     * 
	// ********************************************

	/// <summary>The ONIOM Layer of the Host Atom</summary>
	public OLID hostLayerID;
	/// <summary>The ONIOM Layer of the Connection Atom</summary>
	public OLID connectionLayerID;
	/// <summary>The Atom ID of Host Atom</summary>
	public AtomID hostAtomID;
	/// <summary>The Atom ID of the Connection Atom</summary>
	public AtomID connectionAtomID;

	/// <summary>Creates a Link Structure</summary>
	/// <param name="hostLayerID">The ONIOM Layer of the Host Atom.</param>
	/// <param name="connectionLayerID">The ONIOM Layer of the Connection Atom.</param>
	/// <param name="hostAtomID">The Atom ID of Host Atom.</param>
	/// <param name="connectionAtomID">The Atom ID of the Connection Atom.</param>
	public Link(OLID hostLayerID, OLID connectionLayerID, AtomID hostAtomID, AtomID connectionAtomID) {
		this.hostLayerID = hostLayerID;
		this.connectionLayerID = connectionLayerID;
		this.hostAtomID = hostAtomID;
		this.connectionAtomID = connectionAtomID;
	}

}

/// <summary>Excited State Structure</summary>
/// <remarks>Describes an Excited State in a TD calculation.</remarks>
public struct ExcitedState {
	/// <summary>The index of the excited state (1 => first excited state)</summary>
	public int stateIndex;
	/// <summary>The spin and spatial symmetry of the state</summary>
	public string symmetry;
	/// <summary>The energy difference between the ground state and this state</summary>
	public float excitationEnergy;
	/// <summary>The oscillator strength of the state</summary>
	public float oscillatorStrength;
	/// <summary>The value of S**2 of this state</summary>
	public float spinContamination;
	/// <summary>The transition dipole moment of this state</summary>
	public float[] transitionDipoleMoment;
	/// <summary>The orbital composition of this state</summary>
	public List<ExcitedStateComposition> composition;

	/// <summary>Creates an Excited State Structure</summary>
	/// <param name="stateIndex">The index of the excited state (1 => first excited state)</param>
	/// <param name="symmetry">ThThe spin and spatial symmetry of the state</param>
	/// <param name="excitationEnergy">The energy difference between the ground state and this state</param>
	/// <param name="oscillatorStrength">The oscillator strength of the state</param>
	/// <param name="spinContamination">The value of S**2 of this state</param>
	/// <param name="transitionDipoleMoment">The transition dipole moment of this state</param>
	/// <param name="composition">The orbital composition of this state</param>
	public ExcitedState(
		int stateIndex
	) {
		this.stateIndex = stateIndex;
		this.symmetry = "";
		this.excitationEnergy = 0f;
		this.oscillatorStrength = 0f;
		this.spinContamination = 0f;
		this.transitionDipoleMoment = new float[3];
		this.composition = new List<ExcitedStateComposition>();
	}
}

/// <summary>Excited State Orbital Composition Structure</summary>
/// <remarks>Describes the orbital composition of an ExcitedState object.</remarks>
public struct ExcitedStateComposition {
	/// <summary>The source orbital of the CI expansion coefficient</summary>
	public int fromOrbital;
	/// <summary>The destination orbital of the CI expansion coefficient</summary>
	public int toOrbital;
	/// <summary>The CI expansion coefficient</summary>
	public float coefficient;
}
