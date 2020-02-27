using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using Element = Constants.Element;
using Amber = Constants.Amber;
using CT = Constants.ConnectionType;
using BT = Constants.BondType;
using OLID = Constants.OniomLayerID;
using EL = Constants.ErrorLevel;
using Unity.Mathematics;

///<summary>The Atom Class</summary>
///<remarks>Contains positional and connectivity information of an atom.</remarks>
public class Atom {

	/// <summary>The position of this Atom.</summary>
	public float3 position = new float3();

	/// <summary>The ID of this Atom's parent Residue</summary>
	public ResidueID residueID;

	/// <summary>The Amber type of this Atom.</summary>
	public Amber amber;
	/// <summary>The normalised Partial Charge of this Atom.</summary>
	public float partialCharge = 0f;
	/// <summary>The Connection Type of this Atom. Used for joining Residues and Caps.</summary>
	public CT connectionType = CT.NULL;
	/// <summary>The ONIOM Layer of this Atom.</summary>
	public OLID oniomLayer;
	/// <summary>Returns true if this Atom has an available bonding site according to its Connection Type.</summary>
	public bool valent => connectionType == CT.C_VALENT || connectionType == CT.N_VALENT || connectionType == CT.OTHER_VALENT;

	/// <summary>Is this Atom allowed to move during optimisations?</summary>
	public bool mobile;

	/// <summary>This is the penalty score on the parameters for this Atom.<summary>
	public float penalty;

	/// <summary>The dictionary of Atoms in this Atom's parent Residue this Atom is connected to, accessed by their PDBIDs and Bond Types as values.</summary>
	public Dictionary<PDBID, BT> internalConnections = new Dictionary<PDBID, BT>();
	/// <summary>The dictionary of Atoms outside this Atom's parent Residue this Atom is connected to, accessed by their Atom IDs and Bond Types as values.</summary>
	public Dictionary<AtomID, BT> externalConnections = new Dictionary<AtomID, BT>();

	/// <summary>Enumerates the internal connections of this Atom</summary>
	public IEnumerable<(AtomID, BT)> EnumerateInternalConnections() {
		return internalConnections.Select(kvp => (new AtomID(residueID, kvp.Key), kvp.Value));
	}

	/// <summary>Enumerates the internal connections of this Atom</summary>
	public IEnumerable<(AtomID, BT)> EnumerateExternalConnections() {
		return externalConnections.Select(kvp => (kvp.Key, kvp.Value));
	}

	/// <summary>Enumerates the internal and external connections of this Atom</summary>
	public IEnumerable<(AtomID, BT)> EnumerateConnections() {
		return EnumerateInternalConnections().Concat(EnumerateExternalConnections());
	}

	/// <summary>Enumerates the internal and external connections of this Atom</summary>
	public IEnumerable<AtomID> EnumerateNeighbours() {
		foreach (KeyValuePair<PDBID, BT> internalConnection in internalConnections) {
			yield return new AtomID(residueID, internalConnection.Key);
		}
		foreach (KeyValuePair<AtomID, BT> externalConnection in externalConnections) {
			yield return externalConnection.Key;
		}
	}

	public bool IsConnectedTo(AtomID atomID) {
		if (atomID.residueID == residueID && internalConnections.ContainsKey(atomID.pdbID)) {
			//Internal connection
			return true;
		} else if (externalConnections.ContainsKey(atomID)) {
			//External connection
			return true;
		} else {
			//Connection doesn't exist
			return false;
		}
	}

	/// <summary>Remove a Connection to this Atom if it exists.</summary>
	/// <param name="atomID">The AtomID of the connection to remove.</param>
	public bool TryDisconnect(AtomID atomID) {
		if (atomID.residueID == residueID && internalConnections.ContainsKey(atomID.pdbID)) {
			//Remove internal connection
			internalConnections.Remove(atomID.pdbID);
		} else if (externalConnections.ContainsKey(atomID)) {
			//Remove external connection
			externalConnections.Remove(atomID);
		} else {
			//Connection doesn't exist
			return false;
		}
		return true;
	}

	/// <summary>Form a Connection between this Atom and another.</summary>
	/// <param name="atomID">The AtomID of the Atom to connect.</param>
	/// <param name="bondType">The Bond Type of the Connection.</param>
	public void Connect(AtomID atomID, BT bondType) {
		if (atomID.residueID == residueID) {
			//Form internal connection
			internalConnections[atomID.pdbID] = bondType;
		} else {
			//Form external connection
			externalConnections[atomID] = bondType;
		}
	}

	/// <summary>Creates a new Atom.</summary>
	/// <param name="position">The position to set this Atom to.</param>
	/// <param name="amber">The Amber type to give this Atom.</param>
	/// <param name="partialCharge">The Partial Charge to give this Atom.</param>
	/// <param name="oniomLayer">The ONIOM Layer give this Atom.</param>
	public Atom(float3 position, ResidueID residueID, Amber amber, float partialCharge=0f, OLID oniomLayer=OLID.REAL) {
		this.position = position;
		this.amber = amber;
		this.residueID = residueID;
		this.partialCharge = partialCharge;
		this.oniomLayer = oniomLayer;
	}

	/// <summary>Makes a copy of this Atom.</summary>
	public Atom Copy() {
		Atom atom = new Atom(
			position, 
			residueID,
			amber, 
			partialCharge, 
			oniomLayer
		);
		atom.connectionType = this.connectionType;
		//Clone the connections dictionary
		atom.externalConnections = this.externalConnections.ToDictionary(x => x.Key, x => x.Value);
		atom.internalConnections = this.internalConnections.ToDictionary(x => x.Key, x => x.Value);
		return atom;
	}

	public override string ToString() {
		return string.Format(
			"Atom(AMBER: {0}, Position: {1}, Layer: {2}, Charge: {3})",
			amber,
			position,
			oniomLayer,
			partialCharge
		);
	}
}

/// <summary>PDB ID Structure</summary>
/// <remarks>The unique ID of an Atom in a Residue composed of an Element, PDB Identifier and a number.</remarks>
public struct PDBID : IComparable<PDBID> {
	/// <summary>The Element of this PDB ID.</summary>
	public Element element;
	/// <summary>The Identifier of this PDB ID.</summary>
	/// <remarks>This is typically the Greek letter position of this Atom in the Residue.</remarks>
	public string identifier;
	/// <summary>The number of this PDB ID.</summary>
	/// <remarks>This is used when there are multiple elements in the same position (same identifier).</remarks>
	public int number;
	/// <summary>The Atomic Number of this PDB ID.</summary>
	public int atomicNumber => (int)element;

	///<summary>Creates a PDB ID structure.</summary>
	///<param name="element">The Element of this PDB ID.</summary>
	///<param name="identifier">The Identifier of this PDB ID. This is typically the Greek letter position of this Atom in the Residue.</summary>
	///<param name="number">The number of this PDB ID. This is used when there are multiple elements in the same position (same identifier).</summary>
	public PDBID(Element element, string identifier="",int number=0) {
		this.element = element;
		this.identifier = identifier;
		this.number = number;
	}

	///<summary>Creates a PDB ID structure.</summary>
	///<param name="element">The Element of this PDB ID.</summary>
	///<param name="identifier">The Identifier of this PDB ID. This is typically the Greek letter position of this Atom in the Residue.</summary>
	///<param name="number">The number of this PDB ID. This is used when there are multiple elements in the same position (same identifier).</summary>
	public PDBID(string element, string identifier="",int number=0) {
		if (!Constants.ElementMap.TryGetValue(element, out this.element)) {
			throw new ErrorHandler.PDBIDException(string.Format(
				"Couldn't convert string '{0}' to element!",
				element
			));
		}

		this.identifier = identifier;
		this.number = number;
	}

	/// <summary>Returns true if this PDB ID's Element, Identifier and Number matches other.</summary>
	public override bool Equals(object other) {
		PDBID pdbId = (PDBID)other;
		if (ReferenceEquals(this, other)) {return true;}
		if (pdbId == null) return false;
		return (
			this.element == pdbId.element && 
			this.identifier.Equals(pdbId.identifier, StringComparison.Ordinal) &&
			this.number == pdbId.number
		);
	}

	public static PDBID C => new PDBID(Element.C);
	public static PDBID CA => new PDBID(Element.C, "A");
	public static PDBID CB => new PDBID(Element.C, "B");
	public static PDBID CG => new PDBID(Element.C, "G");
	public static PDBID CD => new PDBID(Element.C, "D");
	public static PDBID CE => new PDBID(Element.C, "E");
	public static PDBID CZ => new PDBID(Element.C, "Z");
	public static PDBID N => new PDBID(Element.N);
	public static PDBID NA => new PDBID(Element.N, "A");
	public static PDBID NB => new PDBID(Element.N, "B");
	public static PDBID NG => new PDBID(Element.N, "C");
	public static PDBID ND => new PDBID(Element.N, "D");
	public static PDBID NE => new PDBID(Element.N, "E");
	public static PDBID NZ => new PDBID(Element.N, "Z");
	public static PDBID O => new PDBID(Element.O);
	public static PDBID OA => new PDBID(Element.O, "A");
	public static PDBID OB => new PDBID(Element.O, "B");
	public static PDBID OG => new PDBID(Element.O, "C");
	public static PDBID OD => new PDBID(Element.O, "D");
	public static PDBID OE => new PDBID(Element.O, "E");
	public static PDBID OZ => new PDBID(Element.O, "Z");
	public static PDBID H => new PDBID(Element.H);

	/// <summary>Returns true if this PDB ID's Element matches other.</summary>
	public bool ElementEquals(PDBID other) => this.element == other.element;
	/// <summary>Returns true if this PDB ID's Element and Identifier matches other.</summary>
	public bool TypeEquals(PDBID other) => (
		this.element == other.element && 
		this.identifier.Equals(other.identifier, StringComparison.Ordinal)
	);

	/// <summary>Returns the Hash Code of this PDB ID.</summary>
	public override int GetHashCode() {
		return ToString().GetHashCode();
	}

	public static bool operator ==(PDBID pdbId0, PDBID pdbId1) {
		if (ReferenceEquals(pdbId0, pdbId1)) return true;
		if (ReferenceEquals(pdbId0, null) || ReferenceEquals(null, pdbId1)) return false;
		return pdbId0.Equals(pdbId1);
	}

	public static bool operator !=(PDBID pdbId0, PDBID pdbId1) {
		return !(pdbId0 == pdbId1);
	}

	public static bool operator <(PDBID pdbId0, PDBID pdbId1) {
		return pdbId0.CompareTo(pdbId1) < 0;
	}

	public static bool operator >(PDBID pdbId0, PDBID pdbId1) {
		return pdbId0.CompareTo(pdbId1) > 0;
	}

	public string ToOldString() {
		string numStr = number == 0 ? " " : number.ToString();
		string pdbName;
		string elementString = Constants.ElementMap[element];
		if (elementString.Length == 2) {
			pdbName = (elementString + identifier + numStr).PadRight(4);
		} else {
			pdbName = elementString + identifier;
			pdbName = pdbName.Length == 3 ?  pdbName + numStr : (" " + pdbName + numStr).PadRight(4);
		}
		return pdbName;
	}

	public override string ToString() {
		string numStr = number == 0 ? " " : number.ToString();
		string pdbName;
		string elementString = Constants.ElementMap[element];
		if (elementString.Length == 2) {
			pdbName = (elementString + identifier + numStr).PadRight(4);
		} else {
			pdbName = elementString + identifier;
			pdbName = pdbName.Length == 3 ? numStr + pdbName : (" " + pdbName + numStr).PadRight(4);
		}
		return pdbName;
	}

	///<summary>Creates a PDB ID structure from a Gaussian-style string.</summary>
	///<param name="pdbName">The Gaussian-style string to read.</summary>
	///<param name="element">The Element of this PDB ID.</summary>
	///<param name="residueName">The ID of the Residue containing this PDB ID. Used for special Residues such as metal ion.</summary>
	public static PDBID FromGaussString(string pdbName, string element, string residueName) {
		//Correctly format the PDB Name then pass through FromString
		if (pdbName.Substring(0,1).Equals(element, StringComparison.Ordinal)) {
			if (pdbName.Length == 4) {
				// eg 'CA1' -> ' CA1', 'C' -> ' C  '
				pdbName = pdbName.PadRight(4);
			} else {
				pdbName = (" " + pdbName).PadRight(4);
			}
		} else if (pdbName.Substring(1,1).Equals(element, StringComparison.Ordinal)) {
			// eg '1HH' -> '1HH ', '1HH3' -> '1HH3'
			pdbName = pdbName.PadRight(4) ;
		} else if (pdbName.Substring(0,2).Equals(element, StringComparison.Ordinal)) {
			// eg 'NA' -> 'NA  '
			pdbName = pdbName.PadRight(4) ;
		} else {
			throw new ErrorHandler.PDBIDException(
				string.Format("Misaligned PDB in Gaussian PDB String. Element '{0}' not found in '{1}'", element, pdbName),
				pdbName
			);
		}
		return FromString(pdbName, residueName);
	}

	///<summary>Creates a PDB ID structure from a 4-letter string.</summary>
	///<param name="pdbName">The 4-letter string to read.</summary>
	///<param name="residueName">The ID of the Residue containing this PDB ID. Used for special Residues such as metal ion.</summary>
	public static PDBID FromString(string pdbName, string residueName="") {
		if (pdbName.Length != 4) {
			throw new ErrorHandler.PDBIDException(
				string.Format("Incorrect length of PDB String '{0}': {1}. Should be 4", pdbName, pdbName.Length),
				pdbName
			);
		}

		string element = "";
		string trimmed = pdbName.Trim();

		//Metal ion
		if (string.Equals(trimmed, residueName, StringComparison.CurrentCultureIgnoreCase)) {
			char[] lower = trimmed.ToLower().ToCharArray();
			lower[0] = char.ToUpper(lower[0]);
			return new PDBID(new string(lower));
		}

		string identifier = "";
		int number = 0;

		//Differentiate between eg 'HG11' (Old) and '1HG1' (New)
		//Residues with single elements (and no identifier) with numbers > 9 should land here too
		if (char.IsDigit(pdbName[2]) && char.IsDigit(pdbName[3])) {
			if (char.IsWhiteSpace(pdbName[0])) {
				//eg ' H10'
				element = pdbName.Substring(1,1);
				number = int.Parse(pdbName.Substring(2,2).Trim());
			} else {
				//Old style eg 'HG11'
				element = pdbName.Substring(0,1);
				identifier = pdbName.Substring(1,2).Trim();
				number = int.Parse(pdbName.Substring(3,1));
			}
		} else {
			if (char.IsDigit(pdbName[0])) {
				// eg '1HG1'
				element = pdbName.Substring(1,1);
				identifier = pdbName.Substring(2,2).Trim();
				number = int.Parse(pdbName.Substring(0,1));
			} else if (char.IsWhiteSpace(pdbName[0])) {
				// eg ' N  ', ' N1 ', ' NA ', ' NA1'
				if (char.IsDigit(pdbName[2])) {
					// eg ' N1 '
					element = pdbName.Substring(1,1);
					number = int.Parse(pdbName.Substring(2,1));

				} else if (char.IsWhiteSpace(pdbName[2])) {
					// eg ' N  '
					element = pdbName.Substring(1,1);

				} else if (char.IsDigit(pdbName[3])) {
					// eg ' NA1' - This one is a problem. We don't know if the number is part of the identifier
					// Safest to assume it's the number and check later?
					element = pdbName.Substring(1,1);
					identifier = pdbName.Substring(2,1);
					number = int.Parse(pdbName.Substring(3,1));

				} else {
					// eg ' NA ', ' OXT'
					element = pdbName.Substring(1,1);
					identifier = pdbName.Substring(2,2).Trim();
				}
			} else {
				//eg 'NA  ', 'NA1 ', 'NAB1'
				if (char.IsDigit(pdbName[2])) {
					//eg 'NA1 ' - unlikely
					element = new string(new char[] {
						char.ToUpper(pdbName[0]),
						char.ToLower(pdbName[1])
					});
					number = int.Parse(pdbName.Substring(2,1));
				} else if (char.IsDigit(pdbName[3])) {
					//eg 'NAB1' - unlikely but could be a weird naming error in an external program
					element = pdbName.Substring(0,1);
					identifier = pdbName.Substring(1,2);
					number = int.Parse(pdbName.Substring(3,1));
				} else {
					//eg 'NA  ' - most likely one ion per residue
					element = new string(new char[] {
						char.ToUpper(pdbName[0]),
						char.ToLower(pdbName[1])
					});
				}
			}
		}

		if (pdbName == "HOG1") CustomLogger.LogOutput(element);
		return new PDBID(element, identifier, number);

	}

	public int CompareTo(PDBID other) {
		//Allow sorting
		// sorted eg: ' C  ', ' CA  ', ' CB1', ' CB2'
		int comparison = element.CompareTo(other.element);
		if (comparison != 0) return comparison;
		comparison = identifier.CompareTo(other.identifier);
		if (comparison != 0) return comparison;
		return number.CompareTo(other.number);
	}

	///<summary>Returns the PDB ID with the same Element and Identifier but Number + 1.</summary>
	public PDBID NextNumber() => new PDBID(element, identifier, number+1);

	///<summary>Returns the PDB ID with the same Element and Identifier but Number - 1.</summary>
	public PDBID PreviousNumber() {
		if (number == 0) {
			throw new System.Exception(
				string.Format("Can't get previous number - number can't be negative. ({0})", 
					number
				)
			);
		}
		return new PDBID(element, identifier, number - 1);
	}

	///<summary>Returns a placeholder PDBID</summary>
	public static PDBID Empty => new PDBID(Element.X);
	///<summary>Returns true if this PDBID is Empty or uninitialised</summary>
	public bool IsEmpty() {
		return (element == Element.X && String.IsNullOrEmpty(identifier) && number == 0);
	}
	///<summary>Returns true if value is Empty or uninitialised</summary>
	public static bool IsEmpty(PDBID value) {
		return value.IsEmpty();
	}
}

/// <summary>Atom ID Structure</summary>
/// <remarks>The unique ID of an Atom in an Atoms object composed of Residue ID and a PDB ID.</remarks>
public struct AtomID {
	/// <summary>The Residue ID of this Atom ID.</summary>
	public ResidueID residueID;
	/// <summary>The PDB ID of this Atom ID.</summary>
	public PDBID pdbID;

	///<summary>Creates an Atom ID structure.</summary>
	///<param name="residueID">The Residue ID of this Atom ID.</summary>
	///<param name="pdbID">The PDB ID of this Atom ID.</summary>
	public AtomID(ResidueID residueID, PDBID pdbID) {
		this.residueID = residueID;
		this.pdbID = pdbID;
	}

	///<summary>Creates an Atom ID structure.</summary>
	///<param name="chainID">The Chain ID of the Residue ID of this Atom ID.</summary>
	///<param name="residueNumber">The Residue Number of the Residue ID of this Atom ID.</summary>
	///<param name="element">The element of the  PDB ID of this Atom ID.</summary>
	///<param name="identifier">The identifier of the  PDB ID of this Atom ID.</summary>
	///<param name="number">The number of the  PDB ID of this Atom ID.</summary>
	public AtomID(string chainID, int residueNumber, Element element, string identifier="", int number=0) {
		this.residueID = new ResidueID(chainID, residueNumber);
		this.pdbID = new PDBID(element, identifier, number);
	}

	public override string ToString() {
		return string.Concat(residueID, pdbID);
	}

	/// <summary>Returns the Hash Code of this PDB ID.</summary>
	public override int GetHashCode() {
		return ToString().GetHashCode();
	}

	/// <summary>Returns true if this Atom ID's Residue ID and PDB ID matches other.</summary>
	public override bool Equals(object other) {
		AtomID atomID = (AtomID)other;
		if (atomID == null) return false;
		return (
			this.residueID == atomID.residueID && 
			this.pdbID == atomID.pdbID
		);
	}

	public int CompareTo(AtomID other) {
		//Allow sorting
		// sorted eg: ' C  ', ' CA  ', ' CB1', ' CB2'
		
		int comparison = residueID.CompareTo(other.residueID);
		if (comparison != 0) return comparison;
		return pdbID.CompareTo(other.pdbID);
	}

	public static bool operator ==(AtomID atomID0, AtomID atomID1) {
		if (ReferenceEquals(atomID0, atomID1)) return true;
		if (ReferenceEquals(atomID0, null) || ReferenceEquals(null, atomID1)) return false;
		return atomID0.Equals(atomID1);
	}

	public static bool operator !=(AtomID atomID0, AtomID atomID1) {
		return !(atomID0 == atomID1);
	}

	public static bool operator <(AtomID atomID0, AtomID atomID1) {
		return atomID0.CompareTo(atomID1) < 0;
	}

	public static bool operator >(AtomID atomID0, AtomID atomID1) {
		return atomID0.CompareTo(atomID1) > 0;
	}

	///<summary>Deconstruct this AtomID into a Residue ID and PDB ID.</summary>
	///<param name="residueID">This Atom ID's Residue ID.</param>
	///<param name="pdbID">This Atom ID's PDB ID.</param>
	public void Deconstruct(out ResidueID residueID, out PDBID pdbID) {
		residueID = this.residueID;
		pdbID = this.pdbID;
	}

	///<summary>Returns a placeholder AtomID</summary>
	public static AtomID Empty => new AtomID(ResidueID.Empty, PDBID.Empty);
	///<summary>Returns true if this AtomID is Empty or uninitialised</summary>
	public bool IsEmpty() {
		return (residueID.IsEmpty() && pdbID.IsEmpty());
	}
	///<summary>Returns true if value is Empty or uninitialised</summary>
	public static bool IsEmpty(AtomID value) {
		return value.IsEmpty();
	}

	public static AtomID FromString(string atomIDString) {
		int length = atomIDString.Length;
		string residueIDString = atomIDString.Substring(0, length - 4);
		string pdbIDString = atomIDString.Substring(length - 4);

		return new AtomID(ResidueID.FromString(residueIDString), PDBID.FromString(pdbIDString));
	}

}

public struct ElementPair {
	public int2 elements;

	public ElementPair(Element element0, Element element1) {
		this.elements = new int2((int)element0, (int)element1);
	}

	public ElementPair(PDBID pdbID0, PDBID pdbID1) {
		this.elements = new int2(pdbID0.atomicNumber, pdbID1.atomicNumber);
	}

	public static ElementPair Ordered(Element element0, Element element1) {
		if (element1 > element0) {
			return new ElementPair(element0, element1);
		} else {
			return new ElementPair(element1, element0);

		}
	}

	public override bool Equals(object other) {
		ElementPair ep1 = (ElementPair)other;
		if (ep1 == null) return false;
		return this.elements.Equals(ep1.elements);
	}

	public override int GetHashCode() {
		return this.elements.GetHashCode();
	}

	public static bool operator ==(ElementPair ep0, ElementPair ep1) {
		if (ReferenceEquals(ep0, ep1)) return true;
		if (ReferenceEquals(ep0, null) || ReferenceEquals(null, ep1)) return false;
		
		return ep0.Equals(ep1);
	}

	public static bool operator !=(ElementPair ep0, ElementPair ep1) {
		return !(ep0 == ep1);
	}


}
