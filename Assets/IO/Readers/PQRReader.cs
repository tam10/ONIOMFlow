using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.Mathematics;
using EL = Constants.ErrorLevel;
using OLID = Constants.OniomLayerID;

public class PQRReader : GeometryReader {
	
	static int[] positionStartChars = new int[] {30, 38, 46};
	
	public PQRReader(Geometry geometry) {
		commentString = "#";
	}

	public override bool ParseLine(Geometry geometry) {

		int atomIndex = 0;
		geometry.atomMap = new Map<AtomID, int>();

		if ( line.StartsWith("ATOM") ) {

			return ReadAtom(geometry, OLID.REAL, ref atomIndex);
			
		} else if ( line.StartsWith("HETATM") ) {

			return ReadAtom(geometry, OLID.MODEL, ref atomIndex);
			
		}

		return true;
	}

	bool ReadAtom(Geometry geometry, OLID oniomLayerID, ref int atomIndex) {

		//PDB - use all 4 characters to distinguish "NA  " (Sodium) from " NA " (Nitrogen)
		string pdbName;
		if (!TryGetString(line, 12, 4, false, "ReadPDB", out pdbName)) {
			return false;
		}

		//Use PDB to get Element
		//If 12th column is a letter, it is a metal
		string trimmedPDB = pdbName.TrimStart();
		string element;
		if (char.IsLetter (pdbName [0])) {
			if (!TryGetString(trimmedPDB, 0, 2, true, "ReadElement", out element)) {
				return false;
			}
		} else {
			element = trimmedPDB[0].ToString();
		}
		element = ToAlpha(element);
		if (element.Length > 1) {
			element = element.Substring(0,1).ToUpper() + element.Substring(1).ToLower();
		}

		//Residue
		string residueName;
		if (!TryGetString(line, 17, 3, false, "ReadResidueName", out residueName)) {
			return false;
		}

		//PDBID
		PDBID pdbID = PDBID.FromString(pdbName, residueName);
		if (pdbID.IsEmpty()) {
			charNum = 12;
			ThrowError("ReadPDBID");
			return false;
		}

		//Chain ID is optional in PQR, but can also merge with residue number
		string chainID;
		if (!TryGetString(line, 21, 1, false, "ReadChainID", out chainID)) {
			return false;
		}
		int residueNumber;
		if (!TryGetInt(line, 22, 4, false, "ReadResidueNumber", out residueNumber)) {
			return false;
		}
		ResidueID residueID = new ResidueID(chainID, residueNumber);

		//Position
		float3 position = new float3();
		for (int positionIndex=0; positionIndex<3; positionIndex++) {
			float p;
			if (!TryGetFloat(line, positionStartChars[positionIndex], 8, false, "ReadPosition", out p)) {
				return false;
			}
			position[positionIndex] = p;
		}

		//Partial Charge
		float partialCharge;
		if (!TryGetFloat(line, 55, 7, false, "ReadPartialCharge", out partialCharge)) {
			return false;
		}

		//VdW Radius
		float vdwRadius;
		if (!TryGetFloat(line, 63, 6, false, "ReadVdWRadius", out vdwRadius)) {
			return false;
		}

		//Add atom to residue
		if (!geometry.residueDict.ContainsKey (residueID)) {
			geometry.residueDict[residueID] = new Residue(residueID, residueName, geometry);
		}
		geometry.residueDict[residueID].AddAtom(pdbID, new Atom(position, residueID, "", partialCharge), false);

		geometry.atomMap[atomIndex++] = new AtomID(residueID, pdbID);

		return true;
	}

	public IEnumerator NonStandardResiduesFromPQRFile(string path, List<int> nonStandardResidueList) {

		bool readResidues = false;
		int residueNumber;

		string[] lines = FileIO.Readlines (path);
		string[] splitLine;
		for ( lineNumber=0; lineNumber < lines.Length; lineNumber++) {

			line = lines [lineNumber];
			if (line.StartsWith ("REMARK")) {
				if (line.Contains("(omitted below):")) {
					readResidues = true;
					continue;
				} else if (line.Contains("This is usually due")) {
					break;
				}
			} 
			if (readResidues) {
				splitLine = line.Split(new char[] {' '}, System.StringSplitOptions.RemoveEmptyEntries);
				residueNumber = int.Parse(splitLine[splitLine.Length - 1]);

				if (!nonStandardResidueList.Contains(residueNumber)) {
					nonStandardResidueList.Add(residueNumber);
				}
			}
		}

		yield return null;
	}
}
