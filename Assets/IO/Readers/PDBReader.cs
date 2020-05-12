﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.Mathematics;
using EL = Constants.ErrorLevel;
using OLID = Constants.OniomLayerID;
using Amber = Constants.Amber;
using ChainID = Constants.ChainID;

public class PDBReader : GeometryReader {
	
	bool readMissingResidues;
	ChainID chainID;

	public PDBReader(Geometry geometry, ChainID chainID=ChainID._) {
		this.geometry = geometry;
        this.chainID = chainID;
		commentString = "#";
		atomIndex = 0;
        activeParser = ParseAll;
    }

    public void ParseAll() {

		if ( line.StartsWith("ATOM") ) {

			ReadAtom(geometry, OLID.REAL, ref atomIndex);
			
		} else if ( line.StartsWith("HETATM") ) {

			ReadAtom(geometry, OLID.MODEL, ref atomIndex);
			
		} else if (line.StartsWith("REMARK 465") && line.Length > 24) {

			if (readMissingResidues) {
				CustomLogger.LogFormat(EL.DEBUG, "Missing Residue Line: {0}", line);
				string residueName = line.Substring (15, 3).Trim ();
				
				ResidueID residueID = ResidueID.FromString(line.Substring (19, 7));
				if (residueID.chainID == ChainID._) {residueID.chainID = chainID;}

				geometry.missingResidues[residueID] = residueName;
				CustomLogger.LogFormat(EL.VERBOSE, "Adding Missing Residue. ID: {0}. Name: {1}", residueID, residueName);
			} else if (line.Substring(15, 3) == "RES") {
				readMissingResidues = true;
				CustomLogger.LogFormat(EL.VERBOSE, "Found Missing Residue section on line {0}", lineNumber);
			}
		}
	}

	void ReadAtom(Geometry geometry, OLID oniomLayerID, ref int atomIndex) {

		//Residue
		string residueName = line.Substring ((charNum = 17), 3).Trim ();

		//PDBID
		PDBID pdbID;

		//PDB - use all 4 characters to distinguish "NA  " (Sodium) from " NA " (Nitrogen)
		string pdbName = line.Substring (charNum = 12, 4);
		if (string.IsNullOrWhiteSpace(pdbName)) {
			throw new System.Exception(string.Format(
				"PDBID is empty!"
			));
		}

		try {
			pdbID = PDBID.FromString(pdbName, residueName);
		} catch (ErrorHandler.PDBIDException) {
			throw new ErrorHandler.PDBIDException(string.Format(
				"Couldn't parse PDBID from name: {0} (Residue Name: {1})",
				pdbName,
				residueName
			));
		}

		if (pdbID.IsEmpty()) {
			throw new System.Exception(string.Format(
				"PDBID is empty!"
			));
		}

		// Get ResidueID from string
		ResidueID residueID = ResidueID.FromString(line.Substring((charNum = 21), 5));
		// Set ChainID to A if it's missing
		if (residueID.chainID == ChainID._) {
			residueID.chainID = ChainID.A;
		}

		//Position
		float3 position = new float3 (
			float.Parse(line.Substring((charNum = 30), 8)),
			float.Parse(line.Substring((charNum = 38), 8)),
			float.Parse(line.Substring((charNum = 46), 8))
		);

		//Add atom to residue
		if (!geometry.HasResidue (residueID)) {
			geometry.AddResidue(residueID, new Residue(residueID, residueName, geometry));
			CustomLogger.LogFormat(EL.VERBOSE, "Adding Residue. ID: {0}. Name: {1}", residueID, residueName);
		}

		geometry.GetResidue(residueID).AddAtom(pdbID, new Atom(position, residueID, Amber.X, 0f, oniomLayerID), false);
		geometry.atomMap[atomIndex++] = new AtomID(residueID, pdbID);
		CustomLogger.LogFormat(EL.VERBOSE, "Adding Atom. ID: {0}. Position: {1}", pdbID, position);

	}
}

