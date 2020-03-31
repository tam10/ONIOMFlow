using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Text;
using System.IO;
using System.Linq;
using RS = Constants.ResidueState;


/// <summary>
/// P2N Writer class
/// Writes a geometry into a P2N file for R.E.D.
/// </summary>
public class P2NWriter : GeometryWriter { 
	
	// Format for atoms
	string atomLineFormat = "ATOM  {0,5} {1,-4} {2,3}  {3,4}    {4,8:.000}{5,8:.000}{6,8:.000}                      {7,2}" + FileIO.newLine;
	string connectionFormat = "CONECT {0,5}";
	string atomFormat = "{0,5}";
	

	int formalCharge;
	int multiplicity;
	float totalCharge;
	float absCharge;
	
	// Keep track of capping atoms
	List<int> cappingAtoms;
	

	IEnumerator<AtomID> atomEnumerator;
	IEnumerator<int> connectionEnumerator;
	IEnumerator chargeEnumerator;

	StringBuilder headerSb;
	StringBuilder atomsSb;
	StringBuilder connectivitySb;

	public P2NWriter(Geometry geometry) {
		this.geometry = geometry;
        
		if (geometry.atomMap == null || geometry.atomMap.Count != geometry.size) {
			atomMap = new Map<AtomID, int>();
			generateAtomMap = true;
		} else {
			atomMap = geometry.atomMap;
			generateAtomMap = false;
		}

		totalCharge = 0f;
		absCharge = 0f;
		cappingAtoms = new List<int>();

		headerSb = new StringBuilder();
        headerSb.AppendFormat("REMARK TITLE {0}{1}", geometry.name, FileIO.newLine);
		atomsSb = new StringBuilder();
		connectivitySb = new StringBuilder();

		fileSections = new List<StringBuilder> ();
		fileSections.Add(headerSb);
		fileSections.Add(atomsSb);
		fileSections.Add(connectivitySb);

		if (generateAtomMap) {
			atomEnumerator = geometry.EnumerateAtomIDs().GetEnumerator();
		} else {
			atomEnumerator = geometry.atomMap.Select(x => x.Key).GetEnumerator();
		}

		lineWriter = WriteAtomLine;
	}

	bool WriteAtomLine() {
		Debug.Log("Atom");
		if (!atomEnumerator.MoveNext() || atomEnumerator.Current == null) {
			if (writeConnectivity) {
				connectionEnumerator = Enumerable.Range(0, atomNum).GetEnumerator();
				lineWriter = WriteConnectionLine;
			} else {
				lineWriter = EstimateChargeMultiplicity;
			}
			return true;
		} else {
			return WriteAtom(atomEnumerator.Current);
		}
	}

	bool WriteConnectionLine() {

		if (!connectionEnumerator.MoveNext()) {
			lineWriter = EstimateChargeMultiplicity;
			return true;
		} else {
			return WriteConnection(connectionEnumerator.Current);
		}
	}

	bool EstimateChargeMultiplicity() {

		multiplicity = 1;
		formalCharge = 0;

		bool estimateCharge;

		if (absCharge >= 0.01f) {
			// Partial charges given - use these

			// Get total number of electrons for the system
			int electrons = geometry.EnumerateAtomIDs().Select(x => x.pdbID.atomicNumber).Sum();
			formalCharge = Mathf.RoundToInt(totalCharge);

			// Calculate low spin multiplicity
			multiplicity = ((electrons + formalCharge) % 2)  + 1;

			// Estimate charge if a doublet
			estimateCharge = (multiplicity == 2);
		} else {
			// Partial charges not given - estimate
			estimateCharge = true;
		}

		if (estimateCharge) {

			//Estimate total charge of the system if partial charges aren't given
			(formalCharge, multiplicity) = Data.PredictChargeMultiplicity(geometry);

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

		}

		chargeEnumerator = GetChargesFromUser();
		lineWriter = WaitForUser;

		return true;

	}

	bool WaitForUser() {
		if (!chargeEnumerator.MoveNext()) {

			//Write charge/multiplicity
			headerSb.AppendFormat("REMARK CHARGE-VALUE {0}{1}", formalCharge, FileIO.newLine);
			headerSb.AppendFormat("REMARK MULTIPLICITY-VALUE {0}{1}", multiplicity, FileIO.newLine);
         	headerSb.AppendFormat("REMARK INTRA-MCC 0.0 | {0} | Remove{1}", string.Join(" ", cappingAtoms), FileIO.newLine);
			return false;
		} else {
			return true;
		}
	}

	IEnumerator GetChargesFromUser() {

		// Get user to confirm or edit charges
		MultiPrompt multiPrompt = MultiPrompt.main;

		multiPrompt.Initialise(
			"Set Charge/Multiplicity", 
			string.Format(
				"Set the charge and multiplicity, separated by a space, for ({0}).",
				string.Join("-",geometry.EnumerateResidues().Select(x => x.residue.residueName))
			), 
			new ButtonSetup(text:"Confirm", action:() => {}),
			new ButtonSetup(text:"Skip", action:() => multiPrompt.Cancel()),
			input:true
		);

		multiPrompt.inputField.text = string.Format("{0} {1}", formalCharge, multiplicity);

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
				cancelled = true;
				break;
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

		//Cancelled - don't write file
		if (multiPrompt.cancelled) {
			cancelled = true;
			yield break;
		}
	}

	private bool WriteAtom(AtomID atomID) {
		if (writeConnectivity) {
			atomMap[atomID] = atomNum;
		}

		(ResidueID residueID, PDBID pdbID) = atomID;
		Residue residue = geometry.GetResidue(residueID);

		Atom atom = residue.GetAtom(pdbID);
		float3 position = atom.position;
		atomNum++;
		atomsSb.Append (
			string.Format (
				atomLineFormat,
				atomNum,
				pdbID,
				residue.residueName,
				residueID.residueNumber,
				position.x,
				position.y,
				position.z,
				pdbID.element
			)
		);

		totalCharge += atom.partialCharge;
		absCharge += math.abs(atom.partialCharge);
		if (residue.state == RS.CAP) {
			cappingAtoms.Add(atomNum);
		}

		return true;
	}

	private bool WriteConnection(int atomNum) {
		

		Atom atom = geometry.GetAtom(atomMap[atomNum]);

		List<AtomID> neighbours = atom.EnumerateNeighbours().ToList();
		List<int> connectionList = new List<int>();

		foreach (AtomID neighbour in neighbours) {
			int connectionIndex;
			try {
				connectionIndex = atomMap[neighbour];
			} catch (KeyNotFoundException) {
				//Atom might have been deleted. Remove connection from this atom
				//atom.TryDisconnect(neighbour);
				continue;
			}
			connectionList.Add(connectionIndex);
		}

		if (connectionList.Count == 0) {
			return true;
		}

		atomsSb.AppendFormat(connectionFormat, atomNum);
		connectionList.Sort();
		foreach (int connectionIndex in connectionList) {
			atomsSb.AppendFormat(atomFormat, connectionIndex);
		}
		atomsSb.Append (FileIO.newLine);

		return true;
	}
}
