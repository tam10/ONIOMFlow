using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.Mathematics;
using EL = Constants.ErrorLevel;
using OLID = Constants.OniomLayerID;

public class PDBReader : GeometryReader {
	
	bool readMissingResidues;

	public PDBReader(Geometry geometry) {
		commentString = "#";
	}

	public override bool ParseLine(Geometry geometry) {

		int atomIndex = 0;
		geometry.atomMap = new Map<AtomID, int>();

		if ( line.StartsWith("ATOM") ) {

			return ReadAtom(geometry, OLID.REAL, ref atomIndex);
			
		} else if ( line.StartsWith("HETATM") ) {

			return ReadAtom(geometry, OLID.MODEL, ref atomIndex);
			
		} else if (line.StartsWith("REMARK 465") && line.Length > 24) {

			if (readMissingResidues) {
				CustomLogger.LogFormat(EL.DEBUG, "Missing Residue Line: {0}", line);
				string residueName = line.Substring (15, 3).Trim ();
				string chainID = line.Substring (19, 1);
				int residueNumber = int.Parse(line.Substring (21, 5).Trim());
				
				ResidueID residueID = new ResidueID(chainID, residueNumber);
				geometry.missingResidues[residueID] = residueName;
				CustomLogger.LogFormat(EL.VERBOSE, "Adding Missing Residue. ID: {0}. Name: {1}", residueID, residueName);
			} else if (line.Substring(15, 3) == "RES") {
				readMissingResidues = true;
				CustomLogger.LogFormat(EL.VERBOSE, "Found Missing Residue section on line {0}", lineNumber);
			}
		}

		return true;
	}

	bool ReadAtom(Geometry geometry, OLID oniomLayerID, ref int atomIndex) {

		//PDB - use all 4 characters to distinguish "NA  " (Sodium) from " NA " (Nitrogen)
		string pdbName = line.Substring (12, 4);
		if (string.IsNullOrWhiteSpace(pdbName)) {
			charNum = 12;
			ThrowError("ReadPDBID");
			return false;
		}

		//Residue
		string residueName = line.Substring (17, 3).Trim ();

		//PDBID
		PDBID pdbID = PDBID.FromString(pdbName, residueName);
		if (pdbID.IsEmpty()) {
			charNum = 12;
			ThrowError("ReadPDBID");
			return false;
		}

		int residueNumber = int.Parse (line.Substring (22, 4).Trim ());
		string chainID = line [21].ToString ();
		if (chainID == " ") {chainID = "A";}
		ResidueID residueID = new ResidueID(chainID, residueNumber);

		//Position
		float3 position = float3.zero;
		if (!TryGetFloat(line, 30, 8, true, "GetPositionX", out position.x)) {return false;}
		if (!TryGetFloat(line, 38, 8, true, "GetPositionY", out position.y)) {return false;}
		if (!TryGetFloat(line, 46, 8, true, "GetPositionZ", out position.z)) {return false;}


		//Add atom to residue
		if (!geometry.residueDict.ContainsKey (residueID)) {
			geometry.residueDict[residueID] = new Residue(residueID, residueName, geometry);
			CustomLogger.LogFormat(EL.VERBOSE, "Adding Residue. ID: {0}. Name: {1}", residueID, residueName);
		}


		geometry.residueDict[residueID].AddAtom(pdbID, new Atom(position, residueID, "", 0f, oniomLayerID), false);
		geometry.atomMap[atomIndex++] = new AtomID(residueID, pdbID);
		CustomLogger.LogFormat(EL.VERBOSE, "Adding Atom. ID: {0}. Position: {1}", pdbID, position);

		return true;
	}
}

