using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using Element = Constants.Element;
using RS = Constants.ResidueState;
using BT = Constants.BondType;
using EL = Constants.ErrorLevel;
using OLID = Constants.OniomLayerID;
using Amber = Constants.Amber;
using Unity.Mathematics;

/// <summary>Residue Class</summary>
/// <remarks>
/// Contains Atom objects.
/// Performs tasks on collections of Atom objects.
/// </remarks>
public class Residue {
	/// <summary>The ID of this Residue.</summary>
	private ResidueID _residueID;
	/// <summary>The ID of this Residue.</summary>
	public ResidueID residueID {
		get => GetResidueID();
		set => SetResidueID(value);
	}
	/// <summary>The reference to the parent of this Residue.</summary>
	public Geometry parent;
	/// <summary>The PDB Name of this Residue.</summary>
	public string residueName;
	/// <summary>Is this Residue fully protonated?</summary>
	public bool protonated = false;
	/// <summary>Return the number of protons in this residue.</summary>
	public int protonCount => atoms.Count(x => x.Key.element == Element.H);
	/// <summary>The current Residue State of this Residue.</summary>
	/// <remarks>The Residue State is used to distinguish Residues during Tasks and other calculations.</remarks>
	/// <remarks>Examples of Residue States are STANDARD, CAP, NONSTANDARD and WATER.</remarks>
	public RS state;

	/// <summary>Returns the number of Atom objects in this Residue.</summary>
	public int size => atoms.Count;
	/// <summary>Returns the Chain ID of this Residue ID.</summary>
	public string chainID {
		get => residueID.chainID;
	}
	/// <summary>Returns the Number of this Residue ID.</summary>
	public int number {
		get => residueID.residueNumber;
	}

	/// <summary>Gets the sum of the Partial Charges of the Atoms of this residue.</summary>
	public float GetCharge() => atoms.Sum(x => x.Value.partialCharge);

	/// <summary>
	/// Gets the Float3 position of the centre of this Residue's atoms.
	/// </summary>
	public float3 GetCentre() {
		return CustomMathematics.AveragePosition(atoms.Values);
	}

	/// <summary>
	/// Sets the centre of this Residue's atoms.
	/// </summary>
	public void SetCentre(float3 value) {
			float3 _oldCentre = GetCentre();
			float3 _offset = value - _oldCentre;

			foreach (Atom atom in atoms.Values) {
				atom.position += _offset;
			}
	}

	/// <summary>Returns true if this Residue's Name is 'HOH' or 'WAT'.</summary>
	public bool isWater => (residueName == "HOH" || residueName == "WAT");

	/// <summary>Returns true if this Residue's ResidueState is STANDARD, C_TERMINAL, N_TERMINAL, CAP or ION.</summary>
	public bool standard => (
		state == RS.STANDARD || 
		state == RS.C_TERMINAL || 
		state == RS.N_TERMINAL ||
		state == RS.STANDARD ||
		state == RS.CAP ||
		state == RS.ION
	);


	//Atoms are accessed by their PDB Names

	/// <summary>The dictionary of Atom objects in this Residue, accessed by their PDBIDs.</summary>
	private Dictionary<PDBID, Atom> atoms = new Dictionary<PDBID, Atom>();
	/// <summary>Enumerates this Residue's PDBIDs.</summary>
	public IEnumerable<PDBID> pdbIDs {get => atoms.Keys;}


	/// <summary>Creates a new Residue object.</summary>
	/// <param name="residueID">The ID of the Residue.</param>
	/// <param name="residueName">The Name of the Residue.</param>
	/// <param name="parent">The Parent Geometry object of the Residue.</param>
	public Residue(ResidueID residueID, string residueName, Geometry parent) {
		this._residueID = residueID;
		this.residueName = residueName;
		this.parent = parent;
	}

	/// <summary>Sets the ResidueID of this Residue.</summary>
	/// <param name="newResidueID">The ResidueID to set this Residue to.</param>
	/// <remarks>
	/// This function will make sure that all Atom objects connected to this Residue's atoms are pointing to the new ResidueID.
	/// </remarks>
	public void SetResidueID(ResidueID newResidueID) {
		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Setting ResidueID {0} to {1}.",
			_residueID,
			newResidueID
		);
		foreach (PDBID pdbID in pdbIDs) {
			Atom atom = atoms[pdbID];

			atom.residueID = newResidueID;
			List<(AtomID, BT)> externalNeighbourIDs = atom.EnumerateExternalConnections().ToList();

			if (externalNeighbourIDs.Count > 0) {
				AtomID oldAtomID = new AtomID(_residueID, pdbID);
				AtomID newAtomID = new AtomID(newResidueID, pdbID);
				foreach ((AtomID neighbourID, BT bondType) in externalNeighbourIDs) {
					CustomLogger.LogFormat(
						EL.VERBOSE,
						"Updating connection: '{0}'-'{1}' is now '{2}-{1}'",
						() => new object[] {
							oldAtomID,
							neighbourID,
							newAtomID
						}
					);
					Atom neighbourAtom;
					if (parent.TryGetAtom(neighbourID, out neighbourAtom)) {
						neighbourAtom.TryDisconnect(oldAtomID);
						neighbourAtom.externalConnections[newAtomID] = bondType;
					}
				}
			}
		}
		_residueID = newResidueID;
	}

	/// <summary>Gets the ResidueID of this Residue.</summary>
	public ResidueID GetResidueID() {
		return _residueID;
	}

    /// <summary>Enumerate all the Atom objects in this Residue as a ValueTuple of PDBID to Atom</summary>
	public IEnumerable<(PDBID pdbID, Atom atom)> EnumerateAtoms() {
		return atoms.Select(kvp => (kvp.Key, kvp.Value));
	}

    /// <summary>Enumerate all the Atom objects in this Residue that meet a condition as a ValueTuple of PDBID to Atom</summary>
	public IEnumerable<(PDBID pdbID, Atom atom)> EnumerateAtoms(Func<PDBID, bool> condition) {
		return atoms
            .Where(kvp => condition(kvp.Key))
            .Select(kvp => (kvp.Key, kvp.Value));
	}

    /// <summary>Enumerate all the Atom objects in this Residue that meet a condition as a ValueTuple of PDBID to Atom</summary>
	public IEnumerable<(PDBID pdbID, Atom atom)> EnumerateAtoms(Func<Atom, bool> condition) {
		return atoms
            .Where(kvp => condition(kvp.Value))
            .Select(kvp => (kvp.Key, kvp.Value));
	}

	/// <summary>
	/// Adds a Proton/Hydrogen to this Residue. 
	/// </summary>
	/// <param name="hostPDBID">The PDBID to attach the proton to.</param>
	/// <remarks>
	/// Recursive function. Potentially problematic on very large residues.
	/// </remarks>
	public PDBID AddProton(PDBID hostPDBID) {
		int pdbNumber = 0;
		while (pdbNumber < 8) {
			PDBID protonPDBID = new PDBID(Element.H, hostPDBID.identifier, pdbNumber);
			
			if (!pdbIDs.Contains(protonPDBID)) {
				AddAtomToHost(hostPDBID, protonPDBID);
				return protonPDBID;
			}
			pdbNumber++;
		}
		throw new System.Exception("Number of connected atoms exceeded");
	}

	/// <summary>
	/// Adds an Atom to this Residue. 
	/// If the PDBID already exists, a new PDBID will be used and set to acceptedPDBID.
	/// </summary>
	/// <param name="pdbID">The target PDBID to point to the new Atom.</param>
	/// <param name="atom">The Atom to add.</param>
	/// <param name="acceptedPDBID">The actual PDBID given to the Atom.</param>
	/// <remarks>
	/// This function will make sure that all Atom objects connected to this Residue's atoms are pointing to the new ResidueID.
	/// </remarks>
	public void AddAtom(PDBID pdbID, Atom atom, out PDBID acceptedPDBID) {

		//This is what the final PDBID will be
		acceptedPDBID = pdbID;

		//If the Residue doesn't contain the PDBID it will never enter the loop
		while (atoms.ContainsKey(acceptedPDBID)) {
			//If it does contain the PDBID, increment the PDBID number and try again.
			acceptedPDBID = acceptedPDBID.NextNumber();
		}

		//Add the atom
		AddAtom(acceptedPDBID, atom);
	}

	/// <summary>
	/// Adds an Atom to this Residue. 
	/// If the PDBID already exists, a new PDBID will be used and set to acceptedPDBID.
	/// </summary>
	/// <param name="pdbID">The target PDBID to point to the new Atom.</param>
	/// <param name="atom">The Atom to add.</param>
	/// <param name="throwIfExists">Throw an expection if the PDBID already exists.</param>
	/// <remarks>
	/// Recursive function. Potentially problematic on very large residues.
	/// </remarks>
	public void AddAtom(PDBID pdbID, Atom atom, bool throwIfExists=true) {
		
		//Check if this Residue contains the pdbID
		if (atoms.ContainsKey(pdbID)) {
			//Residue contains pdbID
			if (throwIfExists) {
				//Residue contains pdbID - throw an error.
				throw new ErrorHandler.PDBIDException(string.Format(
					"PDB {0} already exists in residue {1}",
					pdbID,
					residueID
				));
			} else {
				//Increment pdb number if exists
				AddAtom(pdbID.NextNumber(), atom, false);
			}
		} else {
			//Add the atom if this Residue doesn't contain the pdbID
			atoms[pdbID] = atom;
			atom.residueID = residueID;
			if (pdbID.element == Element.H) {protonated = true;}
		}
	}

	/// <summary>
	/// Adds an Atom using an AtomID to this Residue and attaches it to a host Atom.
	/// </summary>
	/// <param name="hostPDBID">The PDBID of the Atom to attach the new Atom to.</param>
	/// <param name="atomPDBID">The PDBID of the Atom to add.</param>
	public void AddAtomToHost(PDBID hostPDBID, PDBID atomPDBID) {

		//Get the host Atom
		Atom host = atoms[hostPDBID];

		//Get the list of AtomIDs that are connected to the host Atom
		Atom neighbour;
		List<AtomID> neighbourIDs = host
			.EnumerateNeighbours()
			.Where(x => parent.TryGetAtom(x, out neighbour))
			.ToList();
			
		//Number of atoms already connected to the host
		int numNeighbours = neighbourIDs.Count();
		//Allocate a float array for the positions of the neighbours
		float3[] neighbourPositions = new float3[numNeighbours];
		//Allocate a mask for neighbours that are allowed to move i.e. Hydrogen atoms
		bool[] isFlexibleMask = new bool[numNeighbours];

		//Get positions of heavy atoms and protons
		//Assign mask by checking elements
		int neighbourNum = 0;
		foreach (AtomID neighbourID in neighbourIDs) {
			neighbour = parent.GetAtom(neighbourID);

			neighbourPositions[neighbourNum] = neighbour.position;

			isFlexibleMask[neighbourNum++] = (neighbourID.pdbID.element == Element.H);
		}

		//Generate a small icosphere to decide position of flexible atoms
		int resolution = 4;

		float3[] vertices;
		int3[] tris;

		//Generate the sphere
		Sphere.main.GetSphere(out vertices, out tris, resolution);

		//Get initial position of new atom
		//normalisedOffset is the vector on the sphere that points furthest from all the existing neighbours
		float3 normalisedOffset = CustomMathematics.GetBestPositionOnSphere(vertices, host.position, neighbourPositions);
		//bondOffset is the normalisedOffset multiplied by the bond length
		float newbondLength = Data.GetBondDistances(hostPDBID.element, atomPDBID.element)[0] - Settings.bondLeeway * 1.5f;
		float3 bondOffset = normalisedOffset * newbondLength;

		//Calculate the initial position by translating to the existing host atom
		float3 position = host.position + bondOffset;

		//Add atom with initial position
		AtomID atomID = new AtomID(residueID, atomPDBID);
        Amber amber = (atomPDBID.element == Element.H) ? Data.GetLinkType(host, hostPDBID) : Amber.X;
		Atom newAtom = new Atom(position, residueID, amber, 0f, oniomLayer:host.oniomLayer);
		AddAtom(atomPDBID, newAtom, false);

		//Connect the atom to the host
		parent.Connect(atomID, new AtomID(residueID, hostPDBID), BT.SINGLE);

		//Optimise positions of all flexible atoms
		for (int step=0; step<3; step++) {
			for (neighbourNum=0; neighbourNum<numNeighbours; neighbourNum++) {

				//Ignore static atoms - they shouldn't be moved
				if (!isFlexibleMask[neighbourNum]) continue;

				//Shuffle atoms so all are moved around
				AtomID neighbourID = neighbourIDs[neighbourNum];
				neighbourIDs[neighbourNum] = atomID;
				atomID = neighbourID;

				//Get the new position
				neighbourPositions[neighbourNum] = position;

				//Recalculate shuffled positions as before
				normalisedOffset = CustomMathematics.GetBestPositionOnSphere(vertices, host.position, neighbourPositions);
				bondOffset = normalisedOffset * newbondLength;
				position = host.position + bondOffset;

				//Set the new position
				parent.GetAtom(neighbourID).position = position;


			}
		}
	}

	/// <summary>
	/// Deletes an Atom from this Residue.
	/// </summary>
	/// <param name="pdbID">The PDBID of the Atom to remove.</param>
	/// <remarks>
	/// Also removes all connections to this atom.
	/// </remarks>
	public void RemoveAtom(PDBID pdbID) {
		AtomID atomID = new AtomID(residueID, pdbID);
		foreach (AtomID neighbourID in atoms[pdbID].EnumerateNeighbours()) {
			Atom neighbour;
			if (parent.TryGetAtom(neighbourID, out neighbour) && neighbour.TryDisconnect(atomID)) {
				continue;
			}
			CustomLogger.LogFormat(
				EL.WARNING,
				"Unable to remove bond '{0}'-'{1}'",
				() => new object[] {
					atomID,
					neighbourID
				}
			);
				
		}
		atoms.Remove(pdbID);
		if (protonCount == 0) {protonated = false;}
	}

	/// <summary>
	/// Deletes an Atom from this Residue.
	/// </summary>
	/// <param name="atomID">The AtomID of the Atom to remove.</param>
	/// <remarks>
	/// Also removes all connections to this atom.
	/// </remarks>
	public void RemoveAtom(AtomID atomID) {
		foreach (AtomID neighbourID in atoms[atomID.pdbID].EnumerateNeighbours()) {
			Atom neighbour;
			if (parent.TryGetAtom(neighbourID, out neighbour) && neighbour.TryDisconnect(atomID)) {
				continue;
			}
			CustomLogger.LogFormat(
				EL.WARNING,
				"Unable to remove bond '{0}'-'{1}'",
				() => new object[] {
					atomID,
					neighbourID
				}
			);
		}
		atoms.Remove(atomID.pdbID);
		if (protonCount == 0) {protonated = false;}
	}


	/// <summary>
	/// Change the PDBID of an Atom in this Residue.
	/// </summary>
	/// <param name="oldPDBID">The PDBID of the Atom to change.</param>
	/// <param name="newPDBID">The new PDBID to be assigned to the Atom.</param>
	/// <remarks>
	/// Also points all old connections to the new PDBID.
	/// </remarks>
	public void ChangePDBID(PDBID oldPDBID, PDBID newPDBID) {
		if (oldPDBID == newPDBID) {return;}
		Atom atom;
		if (!atoms.TryGetValue(oldPDBID, out atom)) {
			throw new ErrorHandler.PDBIDException(
				string.Format("PDBID {0} not in residue {1}", oldPDBID, residueID),
				oldPDBID.ToString()
			);
		} 
		if (atoms.ContainsKey(newPDBID)) {
			throw new ErrorHandler.PDBIDException(
				string.Format("PDBID {0} already in residue {1}", oldPDBID, residueID),
				newPDBID.ToString()
			);
		} 
		atoms.Remove(oldPDBID);
		atoms[newPDBID] = atom;
		List<(AtomID, BT)> connections = atom.EnumerateConnections().ToList();
		//Make all neighbours point to new AtomID
		if (connections.Count > 0) { 
			AtomID oldAtomID = new AtomID (residueID, oldPDBID);
			AtomID newAtomID = new AtomID (residueID, newPDBID);
			foreach ((AtomID neighbourID, BT bondType) in connections) {
				Atom neighbour = parent.GetAtom(neighbourID);
				neighbour.internalConnections.Remove(oldPDBID);
				neighbour.internalConnections[newPDBID] = bondType;
			}
		}
	}


	/// <summary>Enumerates the ResidueIDs of the Residues that are connected to this Residue.</summary>
	/// <remarks>Uses the connected of the atoms in this Residue to check connectivity.</remarks>
	public IEnumerable<ResidueID> NeighbouringResidues() => 
		atoms
			//Loop through the atoms of this Residue
			.Values
			//Select all the AtomIDs of the connections
			.SelectMany(x => x.externalConnections.Keys)
			//Select the ResidueIDs of the connections
			.Select(y => y.residueID)
			//Get only one copy of each ResidueID
			.Distinct();

	/// <summary>Enumerates the pairs of AtomIDs of the connections between this Residue and its neighbours.</summary>
	public IEnumerable<(AtomID, AtomID)> NeighbouringAtomIDs() {

		//Loop through the atoms of this Residue
		foreach (KeyValuePair<PDBID, Atom> keyValuePair in atoms) {
			//Select all the AtomIDs of the connections
			foreach (AtomID neighbourAtomID in keyValuePair.Value.externalConnections.Keys) {
				ResidueID neighbourID = neighbourAtomID.residueID;
				//Make sure the ResidueIDs are not this Residue
				if (neighbourID != residueID) {
					AtomID thisAtomID = new AtomID (residueID, keyValuePair.Key);
					yield return (thisAtomID, neighbourAtomID);
				}
			}
		}
	}

	/// <summary>Enumerates the pairs of AtomIDs of the connections between this Residue and its neighbours.</summary>
	public IEnumerable<(AtomID, AtomID, Atom, Atom)> NeighbouringAtoms() {

		//Loop through the atoms of this Residue
		foreach ((PDBID thisPDBID, Atom thisAtom) in atoms) {
			//Select all the AtomIDs of the connections
			foreach (AtomID neighbourAtomID in thisAtom.externalConnections.Keys) {
				ResidueID neighbourID = neighbourAtomID.residueID;
				//Make sure the ResidueIDs are not this Residue
				if (neighbourID != residueID) {
					AtomID thisAtomID = new AtomID (residueID, thisPDBID);
					Atom neighbourAtom = parent.GetAtom(neighbourAtomID);
					yield return (
						thisAtomID, 
						neighbourAtomID,
						thisAtom,
						neighbourAtom
					);
				}
			}
		}
	}

	/// <summary>Enumerates the ResidueIDs of the Residues whose centres are within a target distance of this Residue's centre.</summary>
	/// <param name="distance">The maximum distance between this Residue's centre and all matching Residues' centres.</param>
	public IEnumerable<ResidueID> ResiduesWithinDistance(float distance) {

		float distancesq = distance * distance;
		//Evaluate the centre once
		float3 _centre = GetCentre();
		foreach ((ResidueID otherResidueID, Residue otherResidue) in parent.EnumerateResidues()) {
			//Ignore this Residue
			if (otherResidueID == residueID) continue;
			//Yield all ResidueIDs whose Residues are within the distance
			if (math.distancesq(_centre, otherResidue.GetCentre()) <= distancesq) {
				yield return otherResidueID;
			}
		}
	}

	/// <summary>Make a copy of this Residue and assign its parent to newGeometry.</summary>
	/// <param name="newGeometry">The Geometry that will contain the copied Residue.</param>
	public Residue Take(Geometry newGeometry) {
		//Make a new empty Residue
		Residue residue = new Residue(residueID, residueName, newGeometry);
		//Give it the same Residue State and Protonation
		residue.state = state;
		residue.protonated = protonated;
		//Copy each atom
		residue.atoms = atoms
			.ToDictionary(x => x.Key, x => x.Value.Copy());
		return residue;
	}

	/// <summary>Make a copy of this Residue and assign its parent to newGeometry.</summary>
	/// <param name="newGeometry">The Geometry that will contain the copied Residue.</param>
	/// <param name="condition">The condition that must be met for each Atom to be kept.</param>
	public Residue Take(Geometry newGeometry, Func<(PDBID pdbID, Atom atom), bool> condition) {
		//Make a new empty Residue
		Residue residue = new Residue(residueID, residueName, newGeometry);
		//Give it the same Residue State and Protonation
		residue.state = state;
		residue.protonated = protonated;

		Dictionary<PDBID, Atom> removedAtoms = new Dictionary<PDBID, Atom>();
		foreach ((PDBID pdbID, Atom atom) in atoms) {
			if (condition((pdbID, atom))) {
				try {
					residue.AddAtom(pdbID, atom.Copy(), true);
				} catch (SystemException e) {
					//This really should not happen!
					//It would require two keys being identical
					CustomLogger.LogFormat(
						EL.ERROR,
						"Duplication of PDBID ({0}) when copying Residue! {1}",
						pdbID,
						e
					);
				}
			} else {
				removedAtoms[pdbID] = atom;
			}
		}

		//Disconnect and connections to removed Atoms
		foreach ((PDBID pdbID, Atom atom) in removedAtoms) {
			AtomID removedAtomID = new AtomID(residueID, pdbID);
			foreach ((AtomID neighbourID, BT bondType) in atom.EnumerateInternalConnections()) {
				Atom neighbourAtom;
				if (residue.atoms.TryGetValue(neighbourID.pdbID, out neighbourAtom)) {
					neighbourAtom.TryDisconnect(removedAtomID);
				}
			}
		}

		//Return null if there are no matching atoms
		return (residue.atoms.Count == 0) ? null : residue;
	}

	/// <summary>Make a copy of this Residue without its hydrogens and assign its parent to newGeometry.</summary>
	/// <param name="newGeometry">The Geometry that will contain the copied Residue.</param>
	public Residue TakeDeprotonated(Geometry newGeometry) {
		Residue residue = Take(newGeometry, x => x.pdbID.element != Element.H);
		
		//New residue is not protonated
		residue.protonated = false;
		
		return residue;
	}

	///<summary>
	///Make a copy of Residue where atoms have the specified oniomLayerID OR
	///are in a layer that is contained by the specified oniomLayerID.
	///Returns null if no atoms are in the ONIOM Layer 
	///</summary>
	/// <param name="oniomLayerID">ID of the ONIOM Layer to take atoms from.</param>
	/// <param name="newAtoms">The Atoms that will contain the copied Residue.</param>
	public Residue TakeLayer(OLID oniomLayerID, Geometry newGeometry) {
		return Take(newGeometry, x => x.atom.oniomLayer == oniomLayerID);
	}

	///<summary>
	/// Remove all connections to other Residues
	///</summary>
	public void DisconnectExternal() {
		foreach (PDBID pdbID in pdbIDs) {
			List<AtomID> atomIDs = atoms[pdbID].externalConnections.Keys.ToList();
			foreach (AtomID neighbourID in atomIDs) {
				atoms[pdbID].externalConnections.Remove(neighbourID);
			}
		}
	}

	public IEnumerable<PDBID> EnumerateCapSites() {
		Atom cAtom;
		if (
			atoms.TryGetValue(PDBID.C, out cAtom) 
			//Atom has no external connections
			&& cAtom.externalConnections.Count == 0
			//Atom only attached to 2 internal atoms (CA and C usually)
			&& cAtom.internalConnections.Count == 2
		) {
			yield return PDBID.C;
		}
		Atom nAtom;
		if (
			atoms.TryGetValue(PDBID.N, out nAtom) 
			//Atom has no external connections
			&& nAtom.externalConnections.Count == 0
			//Atom only attached to 1 non-H internal atom (CA)
			&& nAtom.internalConnections
				.Where(x => x.Key.element != Element.H)
				.Count() == 1
		) {
			yield return PDBID.N;
		}
	}

	///<summary>
	/// Convert this Residue to an ACE Cap residue
	///</summary>
	public Residue ConvertToACE() {
		return ConvertToACE(PDBID.C);
	}

	///<summary>
	/// Convert this Residue to an ACE Cap residue
	///</summary>
	///<param name="cPDBID">The PDBID of the atom that will be the central C Atom of the ACE Cap</param>
	public Residue ConvertToACE(PDBID cPDBID) {
		// Get the ACE cap equivalent of this residue:
		// Translate ACE ' C  ' to this ' C  '
		// Rotate ACE ' O  ' to this ' O  '
		// Rotate ACE ' CH3' to this ' CA '
		//
		//
		//  neigh   this   -->  neigh   ACE
		//  -bour               -bour
  		//         O                    O    
		//     |   |                |   |    
		//     CA  C   N            CA  C    
		//  ~ / \ ~ \ / ~  -->   ~ / \ ~ \   
		//   C   N   CA           C   N   CH3
		//   |       |            |          
		//   O                    O          
		//
		//
		Residue ace = Data.standardResidues["ACE"][RS.CAP].Take(parent);

		// Get ACE anchor PDBIDs
		PDBID aceCPDBID = PDBID.C;
		PDBID aceOPDBID = PDBID.O;
		PDBID aceCH3PDBID = new PDBID(Element.C,"H",3);

		// Get this residue's anchor Atoms
		Atom cAtom = atoms[cPDBID];
		Atom oAtom = cAtom.internalConnections
			.Where(x => x.Key.element == Element.O)
			.Select(x => atoms[x.Key])
			.FirstOrDefault();
		if (oAtom == null) {
			throw new SystemException(string.Format(
				"Cannot cap Atom '{0}' with ACE - must have an internal Oxygen connection!"
			));
		}

		Atom caAtom = cAtom.internalConnections
			.Where(x => x.Key.element == Element.C)
			.Select(x => atoms[x.Key])
			.FirstOrDefault();
		if (cAtom == null) {
			throw new SystemException(string.Format(
				"Cannot cap Atom '{0}' with ACE - must have an internal Carbon connection!"
			));
		}

		float3 pC = cAtom.position;
		float3 pCA = caAtom.position;
		float3 pO = oAtom.position;

		float3 vC_CA = pCA - pC;
		float3 vC_O = pO - pC;

		//Translate ACE ' C  ' to residue ' C  '
		ace.TranslateTo(aceCPDBID, cAtom.position);

		//Align ACE [' C  '->' CH3'] to residue [' C  '-> ' CA ']
		ace.AlignBond(aceCPDBID, aceCH3PDBID, vC_CA);
		
		//ace.AlignBond(aceCPDBID, aceOPDBID, vC_O);

		//Align ACE [' C  '->' O  '] to residue [' C  '-> ' O  ']
		CustomMathematics.DihedralRuler dihedralRuler = new CustomMathematics.DihedralRuler(
			ace.atoms[aceOPDBID],
			ace.atoms[aceCH3PDBID],
			ace.atoms[aceCPDBID],
			oAtom
		);

		ace.atoms[aceOPDBID].position = math.rotate(quaternion.AxisAngle(
				dihedralRuler.v21n,
				dihedralRuler.dihedral
			), (
			ace.atoms[aceOPDBID].position -
			ace.atoms[aceCPDBID].position
		)) + ace.atoms[aceCPDBID].position;

		return ace;
	}

	///<summary>
	/// Convert this Residue to an NME CAP residue
	///</summary>
	public Residue ConvertToNME() {
		return ConvertToNME(PDBID.N);
	}

	///<summary>
	/// Convert this Residue to an NME Cap residue
	///</summary>
	///<param name="nPDBID">The PDBID of the atom that will be the central N Atom of the NME Cap</param>
	public Residue ConvertToNME(PDBID nPDBID) {
		// Get the NME cap equivalent of this residue:
		// Translate NME ' N  ' to this ' N  '
		// Rotate NME ' CH3' to this ' CA '
		//
		//
		//   this  neigh   -->    NME  neigh
		//  	   -bour               -bour
  		//         O                   O    
		//     |   |                   |    
		//     CA  C   N           CH3 C   N 
		//  ~ / \ ~ \ / ~  -->      \ ~ \ / ~   
		//   C   N   CA              N   CA
		//   |       |                   |  
		//   O                              
		//
		//
		Residue nme = Data.standardResidues["NME"][RS.CAP].Take(parent);

		// Get NME anchor PDBIDs
		PDBID nmeNPDBID = PDBID.N;
		PDBID nmeCH3PDBID = new PDBID(Element.C,"H",3);
		PDBID nmeHPDBID = PDBID.H;

		// Get this residue's anchor Atoms
		Atom nAtom = atoms[nPDBID];
		Atom caAtom = nAtom.internalConnections
			.Where(x => x.Key.element == Element.C)
			.Select(x => atoms[x.Key])
			.FirstOrDefault();
		if (caAtom == null) {
			throw new SystemException(string.Format(
				"Cannot cap Atom '{0}' with NME - must have an internal Carbon connection!"
			));
		}

		float3 pN = nAtom.position;
		float3 pCA = caAtom.position;
		float3 vN_CA = pCA - pN;

		//Translate NME ' N  ' to residue ' N  '
		nme.TranslateTo(nmeNPDBID, nAtom.position);

		//Align NME [' N  '->' CH3'] to residue [' N  '-> ' CA ']
		nme.AlignBond(nmeNPDBID, nmeCH3PDBID, vN_CA);

		//See if we can align the H using the neighbouring residue
		if (nAtom.externalConnections.Count == 1) {
			AtomID neighbourAtomID = nAtom.externalConnections.First().Key;
			Atom neighbourAtom;
			if (parent.TryGetAtom(neighbourAtomID, out neighbourAtom)) {
				float3 vN_Neighbour = neighbourAtom.position - pN;

				//Alignment is the negative of the average of two bonds from N.
				float3 hAlignment = - (math.normalize(vN_Neighbour) + math.normalize(vN_CA)) * 0.5f;

				nme.AlignBond(nmeNPDBID, nmeHPDBID, hAlignment);

			}
		}

		return nme;
	}


	///<summary>
	/// Move this Residue by translating one of its atoms.
	///</summary>
	///<param name="referenceAtom">The PDBID of the Atom whose position will be moved to newReferencePosition.</param>
	///<param name="newReferencePosition">The new position of referenceAtom.</param>
	public void TranslateTo(PDBID referencePDBID, float3 newReferencePosition) {
        TranslateTo(atoms[referencePDBID], newReferencePosition);
	}
    
	///<summary>
	/// Move this Residue by translating one of its atoms.
	///</summary>
	///<param name="referenceAtom">The Atom whose position will be moved to newReferencePosition.</param>
	///<param name="newReferencePosition">The new position of referenceAtom.</param>
	public void TranslateTo(Atom referenceAtom, float3 newReferencePosition) {
		float3 vector = newReferencePosition - referenceAtom.position;
		foreach (Atom atom in atoms.Values) {
			atom.position += vector;
		}
	}


	///<summary>
	/// Rotate this Residue by aligning one of its bonds to a new direction.
	///</summary>
	///<param name="anchorAtom">The PDBID of the atom that will be the origin of the rotation and vectors.</param>
	///<param name="bondAtom">The PDBID of the atom whose Vector to anchorAtom will be aligned.</param>
	///<param name="newDirection">The new direction that the Vector between anchorAtom and bondAtom should have.</param>
	public void AlignBond(PDBID anchorAtom, PDBID bondAtom, float3 newDirection) {
		//Rotate the residue such that the bond from anchorAtom to bondAtom has newDirection
		
        if (anchorAtom == bondAtom) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot Align bond. Anchor Atom and Bond Atom are the same."
            );
            return;
        }

        float3 p0 = atoms[anchorAtom].position;
		float3 p1 = atoms[bondAtom].position;

        if (math.all(p0 == p1)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot Align bond. Anchor Atom and Bond Atom have the same position."
            );
            return;
        }

		float3 v01n = math.normalize(p1 - p0);
		float3 v23n = math.normalize(newDirection);

		float3x3 rotationMatrix = CustomMathematics.GetRotationMatrix(v01n, v23n);
		
		foreach (PDBID pdbID in pdbIDs) {
			//Skip anchor atom - doesn't rotate
			if (pdbID == anchorAtom) continue;
			float3 pos = atoms[pdbID].position;

			pos = CustomMathematics.Dot(rotationMatrix, pos - p0) + p0;
			atoms[pdbID].position = pos;
		}

		CustomLogger.LogFormat(
			EL.DEBUG,
			@"Aligning Internal Bond '{0}'-'{1}' of Residue {2} to vector {3}.
			Before rotation: {4}. After rotation: {5}. Dot product: {6}. Rotation matrix: {7}{8}",
			() => {
				float3 newVector = math.normalize(atoms[anchorAtom].position - atoms[bondAtom].position);
				return new object[] {
					anchorAtom,
					bondAtom,
					residueID,
					v23n,
					v01n,
					newVector,
					math.dot(newVector, v23n),
					FileIO.newLine,
					rotationMatrix
				};
			}
		);
	}


	///<summary>
	/// Rotate this Residue by aligning the vector from an anchor to an Atom to a new direction.
	///</summary>
	///<param name="anchorPosition">The origin of the rotation and vectors.</param>
	///<param name="bondAtom">The PDBID of the atom whose Vector to anchorAtom will be aligned.</param>
	///<param name="newDirection">The new direction that the Vector between anchorAtom and bondAtom should have.</param>
	public void AlignBond(float3 anchorPosition, PDBID bondAtom, float3 newDirection) {
		//Rotate the residue such that the bond from anchorAtom to bondAtom has newDirection
		
		float3 p1 = atoms[bondAtom].position;

        if (math.all(anchorPosition == p1)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot Align bond. Anchor and Bond Atom have the same position."
            );
            return;
        }

		float3 v01n = math.normalize(p1 - anchorPosition);
		float3 v23n = math.normalize(newDirection);

		float3x3 rotationMatrix = CustomMathematics.GetRotationMatrix(v01n, v23n);
		
		foreach (PDBID pdbID in pdbIDs) {
			//Skip anchor atom - doesn't rotate
			float3 pos = atoms[pdbID].position;

			pos = CustomMathematics.Dot(rotationMatrix, pos - anchorPosition) + anchorPosition;
			atoms[pdbID].position = pos;
		}

		CustomLogger.LogFormat(
			EL.DEBUG,
			@"Aligning Anchor-Atom '{0}'-'{1}' of Residue {2} to vector {3}.
			Before rotation: {4}. After rotation: {5}. Dot product: {6}. Rotation matrix: {7}{8}",
			() => {
				float3 newVector = math.normalize(
					anchorPosition - atoms[bondAtom].position
				);
				return new object[] {
					anchorPosition,
					bondAtom,
					residueID,
					v23n,
					v01n,
					newVector,
					math.dot(newVector, v23n),
					FileIO.newLine,
					rotationMatrix
				};
			}
		);
	}

	///<summary>
	/// Rotate this Residue by a Quaternion.
	///</summary>
	///<param name="quaternion">The Quaternion rotation.</param>
	///<param name="origin">The centre of rotation.</param>
	public void Rotate(Quaternion quaternion, float3 origin) {
		foreach (Atom atom in atoms.Values) {
			float3 pos = atom.position - origin;
			pos = quaternion * pos;
			atom.position = pos + origin;
		}
	}
    
	///<summary>
	/// Load a new Residue from the database of standard amino acids.
	///</summary>
	///<param name="newResidueName">The three letter name of the Residue.</param>
	///<param name="residueState">The Residue State of the new Residue.</param>
    public static Residue FromString(string newResidueName, RS residueState=RS.STANDARD, Geometry parent=null, Func<(PDBID pdbID, Atom atom), bool> condition=null) {
        //Use the name to get all the Residues of that family
        Dictionary<RS, Residue> residueFamily;
        
        if (!Data.standardResidues.TryGetValue(newResidueName, out residueFamily)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't find matching Residue Family for Residue Name: '{0}'.",
                newResidueName
            );
            return null;
        }

        //Get the individual Residue from the Residue State
        Residue newResidue;
        if (!residueFamily.TryGetValue(residueState, out newResidue)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't find matching Residue for Residue State {0} in family: '{1}'.",
                Constants.ResidueStateMap[RS.STANDARD],
                newResidueName
            );
            return null;
        }

        //Copy the Residue with a condition if needed
        if (condition == null) {
            return newResidue.Take(parent);
        } else {
            return newResidue.Take(parent, condition);
        }
    }

	/// <summary>
	/// Returns true if this Residue has pdbID.
	/// </summary>
	/// <param name="pdbID">The PDB ID to check</param>
	public bool HasAtom(PDBID pdbID) {
		return atoms.ContainsKey(pdbID);
	}

	///<summary>
	/// Returns the Atom that has a PDBID.
	///</summary>
	/// ///<param name="pdbID">The PDB ID to look up.</param>
    public Atom GetAtom(PDBID pdbID) {
        return atoms[pdbID];
    }

	///<summary>
	/// Returns the Atom that has a particular element and identifier.
    /// Returns null if no matching atoms are found.
	///</summary>
	///<param name="pdbID">The PDB ID to look up.</param>
    public Atom GetSingleAtom(PDBID pdbID) {
        //Get the list of atoms that have the same element and identifier
        List<Atom> matchingAtoms = EnumerateAtoms(x => (x.element == pdbID.element && x.identifier == pdbID.identifier))
            .Select(x => x.Item2)
            .ToList();

        //Check that there is only one Atom that matches
        if (matchingAtoms.Count != 1) {
            return null;
        }
        return matchingAtoms.First();
    }

	///<summary>
	/// Assigns atom to the Atom in this residue with a matching PDB ID.
	/// Returns true if atom was assigned, and false if not.
	///</summary>
	/// ///<param name="pdbID">The PDB ID to look up.</param>
    public bool TryGetAtom(PDBID pdbID, out Atom atom) {
        return atoms.TryGetValue(pdbID, out atom);
    }
    
	///<summary>
	/// Load a new Residue from the database of standard amino acids and adds it as a neighbour to this Residue.
	///</summary>
	///<param name="newResidueName">The three letter name of the Residue.</param>
	///<param name="hostAtom">The Atom to attach the new Residue to.</param>
	///<param name="newResidueID">The Residue ID to give the new Residue.</param>
	///<param name="residueState">The Residue State of the new Residue.</param>
    public Residue AddNeighbourResidue(string newResidueName, PDBID hostAtom, ResidueID newResidueID, RS residueState=RS.STANDARD) {

        //Check the host atom actually exists
        if (!atoms.ContainsKey(hostAtom)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Host Atom '{0}' doesn't exist in Residue '{1}' when adding Residue.",
                hostAtom,
                residueID
            );
            return null;
        }

        //Check the new Residue ID isn't already in the parent Atoms object
        if (parent.HasResidue(newResidueID)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Residue ID {0} already exists in Atoms - can't add it again as a neighbour.",
                newResidueID
            );
            return null;
        }

        //Get the lookup PDBIDs for the backbone Atoms
        PDBID cPDBID = new PDBID(Element.C);
        PDBID caPDBID = new PDBID(Element.C, "A");
        PDBID nPDBID = new PDBID(Element.N);

        //Check what kind of Atom the new Residue is linking to
        bool cTerminal = hostAtom == cPDBID;
        bool nTerminal = hostAtom == nPDBID;
        if (!cTerminal && !nTerminal) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Can only add Residues at C or N termini"
            );
            return null;
        }

        //Get the new Residue.
        Residue newResidue;
        if (protonated) {
            newResidue = FromString(newResidueName, residueState, parent);
        } else {
            newResidue = FromString(newResidueName, residueState, parent, (x) => x.pdbID.element != Element.H);
        }
        if (newResidue == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Couldn't find Residue '{0}' with Residue State '{1}'",
                newResidueName,
                residueState
            );
            return null;
        }
       
        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Adding Residue '{0}' to '{1}'",
            newResidueName,
            residueID
        );

        newResidue.residueID = newResidueID;
        newResidue.residueName = newResidueName;
        newResidue.protonated = protonated;

        /* 
        Reference for Atom names

                |
              myCA           newConn        newAnchor
              /   \           /   \           /
            /       \      ~~~      \       /
          /           \   /           \   /
      myAnchor        myConn          newCA
                                        |

         */

		bool TryGetAtom(Residue residue, PDBID pdbID, out Atom atom) {
			atom = residue.GetSingleAtom(pdbID);
			if (atom == null) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Cannot add Residue '{0}' to '{1}' - Required Atom {2} is null.",
					newResidueName,
					residueID,
					new AtomID(residue.residueID, pdbID)
				);
				return false;
			}

			if (math.all(math.isnan(atom.position))) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Cannot add Residue '{0}' to '{1}' - Position of atom {2} is {3}.",
					newResidueName,
					residueID,
					new AtomID(residue.residueID, pdbID),
					atom.position
				);
				return false;
			}
			return true;
		}
		
        //Get the above six Atom objects
		
        Atom myCA;
        Atom myConnector;
        Atom myAnchor;

		Atom newCA;
        Atom newConnector;
        Atom newAnchor;

		if (!TryGetAtom(this, caPDBID, out myCA)) {return null;}
		if (!TryGetAtom(newResidue, caPDBID, out newCA)) {return null;}

		if (cTerminal) {
			if (!TryGetAtom(this, cPDBID, out myConnector)) {return null;}
			if (!TryGetAtom(this, nPDBID, out myAnchor)) {return null;}
			if (!TryGetAtom(newResidue, nPDBID, out newConnector)) {return null;}
			if (!TryGetAtom(newResidue, cPDBID, out newAnchor)) {return null;}
		} else {
			if (!TryGetAtom(this, nPDBID, out myConnector)) {return null;}
			if (!TryGetAtom(this, cPDBID, out myAnchor)) {return null;}
			if (!TryGetAtom(newResidue, cPDBID, out newConnector)) {return null;}
			if (!TryGetAtom(newResidue, nPDBID, out newAnchor)) {return null;}
		}

        //Use myAnchor -> myCA as initial bond offset for myConnector -> newConnector
        float3 normalisedOffset = math.normalize(myCA.position - myAnchor.position);
        float interResidueBondDistance = Data.GetBondDistances(cPDBID.element, nPDBID.element)[0] - Settings.bondLeeway * 1.5f;
        float3 bondOffset = normalisedOffset * interResidueBondDistance;

        newResidue.TranslateTo(newConnector, myConnector.position + bondOffset);


        //Set angle myConnector-newConnector-newCA to 120 degrees
        CustomMathematics.AngleRuler angleRuler = new CustomMathematics.AngleRuler(myConnector, newConnector, newCA);
        float angle = angleRuler.angle;


        newResidue.Rotate(
            Quaternion.AngleAxis(
                (2f * Mathf.PI / 3f - angle) * Mathf.Rad2Deg, 
                angleRuler.norm
            ), 
            newConnector.position
        );

        //Set dihedral myCA-myConnector-newConnector-newCA to 180 degrees
        CustomMathematics.DihedralRuler dihedralRuler = new CustomMathematics.DihedralRuler(myCA, myConnector, newConnector, newCA);
        float dihedral = dihedralRuler.dihedral;
        newResidue.Rotate(
            Quaternion.AngleAxis(
                (Mathf.PI - dihedral) * Mathf.Rad2Deg, 
                dihedralRuler.v21n
            ), 
            newConnector.position
        );

        //Set dihedral myConnector-newConnector-newCA-newAnchor to 180 degrees
        dihedralRuler = new CustomMathematics.DihedralRuler(myConnector, newConnector, newCA, newAnchor);
        dihedral = dihedralRuler.dihedral;
        newResidue.Rotate(
            Quaternion.AngleAxis(
                (Mathf.PI - dihedral) * Mathf.Rad2Deg, 
                dihedralRuler.v21n
            ), 
            newConnector.position
        );

        //Connect the Residues
        if (cTerminal) {
            myConnector.externalConnections[new AtomID(newResidueID, nPDBID)] = BT.SINGLE;
            newConnector.externalConnections[new AtomID(residueID, cPDBID)] = BT.SINGLE;
        } else {
            myConnector.externalConnections[new AtomID(newResidueID, cPDBID)] = BT.SINGLE;
            newConnector.externalConnections[new AtomID(residueID, nPDBID)] = BT.SINGLE;
        }

        //Add the Residue to the parent
        parent.AddResidue(newResidueID, newResidue);

        return newResidue;
    }

}

/// <summary>Residue ID Structure</summary>
/// <remarks>The unique ID of a Residue composed of a Chain ID and Residue Number.</remarks>
public struct ResidueID : IComparable<ResidueID> {
	/// <summary>The Single-letter Chain identifier of this Residue ID.</summary>
	public string chainID;
	/// <summary>The Index of this Residue in its Chain.</summary>
	public int residueNumber;
	
	///<summary>Creates a Residue structure.</summary>
	///<param name="chainID">The Single-letter Chain identifier of this Residue ID.</summary>
	///<param name="residueNumber">The Index of this Residue in its Chain.</summary>
	public ResidueID(string chainID, int residueNumber) {
		this.chainID = chainID;
		this.residueNumber = residueNumber;
	}

	/// <summary>Returns true if this Residue ID's Chain ID and Residue Number matches other.</summary>
	public override bool Equals(object other) {
		//Convert object to ResidueID (C# casting requirement)
		ResidueID rid = (ResidueID)other;
		//rid is null if it isn't a ResidueID
		if (rid == null) return false;
		return (this.residueNumber == rid.residueNumber && this.chainID == rid.chainID);
	}

	/// <summary>Returns the Hash Code of this Residue ID.</summary>
	public override int GetHashCode() {
		return ToString().GetHashCode();
	}

	public static bool operator ==(ResidueID rid0, ResidueID rid1) {
		if (ReferenceEquals(rid0, rid1)) return true;
		if (ReferenceEquals(rid0, null) || ReferenceEquals(null, rid1)) return false;
		return rid0.Equals(rid1);
	}

	public static bool operator !=(ResidueID rid0, ResidueID rid1) {
		return !(rid0 == rid1);
	}

	public override string ToString() {
		return string.Concat(chainID, residueNumber);
	}

	public int CompareTo(ResidueID other) {
		//Allow sorting
		// sorted eg: A1, A2, A3, B1
		int comparison = chainID.CompareTo(other.chainID);
		if (comparison != 0) return comparison;
		return residueNumber.CompareTo(other.residueNumber);
	}
 
	public static int Compare(ResidueID left, ResidueID right) {
		if (object.ReferenceEquals(left, right)) {
			return 0;
		}
		if (object.ReferenceEquals(left, null)) {
			return -1;
		}
		return left.CompareTo(right);
	}

	public static bool operator <(ResidueID left, ResidueID right) {
		return Compare(left, right) < 0;
	}

	public static bool operator >(ResidueID left, ResidueID right) {
		return Compare(left, right) > 0;
	}

	///<summary>Creates a Residue structure from a string.</summary>
	///<param name="residueIDString">>The string to convert.</summary>
	public static ResidueID FromString(string residueIDString) {
		List<char> chainID = new List<char>();
		List<char> numberString = new List<char>();
		foreach (char idChar in residueIDString) {
			if (char.IsDigit(idChar)) {
				numberString.Add(idChar);
			} else {
				chainID.Add(idChar);
			}
		}
		return new ResidueID(string.Concat(chainID), int.Parse(string.Concat(numberString)));
	}

	///<summary>Returns a placeholder ResidueID</summary>
	public static ResidueID Empty = new ResidueID("", 0);
	///<summary>Returns true if this ResidueID is Empty or uninitialised</summary>
	public bool IsEmpty() {
		return (String.IsNullOrEmpty(chainID) && residueNumber == 0);
	}
	///<summary>Returns true if value is Empty or uninitialised</summary>
	public static bool IsEmpty(ResidueID value) {
		return value.IsEmpty();
	}

	///<summary>Returns the Residue ID with the same Chain ID but Residue Number + 1.</summary>
	public ResidueID GetNextID() => new ResidueID(chainID, residueNumber + 1);
	///<summary>Returns the Residue ID with the same Chain ID but Residue Number - 1.</summary>
	public ResidueID GetPreviousID() {
		if (residueNumber == 0) {
			throw new System.Exception(
				string.Format("Can't get previous Residue number - number can't be negative. ({0})", 
					residueNumber
				)
			);
		}
		return new ResidueID(chainID, residueNumber - 1);
	}

	///<summary>Deconstruct this Residue ID into a Chain ID and Residue Number.</summary>
	///<param name="chainID">This Residue ID's Chain ID.</param>
	///<param name="residueNumber">This Residue ID's Residue Number.</param>
	public void Deconstruct(out string chainID, out int residueNumber) {
		chainID = this.chainID;
		residueNumber = this.residueNumber;
	}

}