using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.Mathematics;
using EL = Constants.ErrorLevel;
using OLID = Constants.OniomLayerID;
using Amber = Constants.Amber;

public class PQRReader : GeometryReader {
	
	public PQRReader(Geometry geometry) {
		this.geometry = geometry;
		atomIndex = 0;
		commentString = "#";
		activeParser = ParseAll;
	}

	public void ParseAll() {

		geometry.atomMap = new Map<AtomID, int>();

		if ( line.StartsWith("ATOM") ) {
			ReadAtom(geometry, OLID.REAL);
		} else if ( line.StartsWith("HETATM") ) {
			ReadAtom(geometry, OLID.MODEL);
		}
	}

	void ReadAtom(Geometry geometry, OLID oniomLayerID) {

		//PDB - use all 4 characters to distinguish "NA  " (Sodium) from " NA " (Nitrogen)
		string pdbName = line.Substring(12, 4);

		//Use PDB to get Element
		//If 12th column is a letter, it is a metal
		string trimmedPDB = pdbName.TrimStart();
		string element;
		if (char.IsLetter (pdbName [0])) {
			element = line.Substring(0, 2);
		} else {
			element = trimmedPDB[0].ToString();
		}
		element = ToAlpha(element);
		if (element.Length > 1) {
			element = element.Substring(0,1).ToUpper() + element.Substring(1).ToLower();
		}

		//Residue
		string residueName = line.Substring(17, 3);

		//PDBID
		PDBID pdbID = PDBID.FromString(pdbName, residueName);
		if (pdbID.IsEmpty()) {
			charNum = 12;
			throw new System.Exception(string.Format(
				"PDBID is empty!"
			));
		}

		//Chain ID is optional in PQR, but can also merge with residue number
		string chainID = line.Substring(21, 1);
		int residueNumber = int.Parse(line.Substring(22, 4));
		ResidueID residueID = new ResidueID(chainID, residueNumber);

		//Position
		float3 position = new float3 (
			float.Parse(line.Substring(30, 8)),
			float.Parse(line.Substring(38, 8)),
			float.Parse(line.Substring(46, 8))
		);

		//Partial Charge
		float partialCharge = float.Parse(line.Substring(55, 7));

		//VdW Radius
		float vdwRadius = float.Parse(line.Substring(63, 6));

		//Add atom to residue
		if (!geometry.residueDict.ContainsKey (residueID)) {
			geometry.residueDict[residueID] = new Residue(residueID, residueName, geometry);
		}
		geometry.residueDict[residueID].AddAtom(pdbID, new Atom(position, residueID, Amber.X, partialCharge), false);

		geometry.atomMap[atomIndex++] = new AtomID(residueID, pdbID);

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
