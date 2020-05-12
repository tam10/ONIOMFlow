using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.Mathematics;
using EL = Constants.ErrorLevel;
using ChainID = Constants.ChainID;

public class Mol2Reader : GeometryReader {
    
    bool readAtoms;
    ChainID chainID;

    public Mol2Reader(Geometry geometry, ChainID chainID=ChainID._) {
        this.geometry = geometry;
        this.chainID = chainID;
        readAtoms = false;
		atomIndex = 0;
        activeParser = ParseAll;
    }

    public void ParseAll() {

		if (!readAtoms) {
            readAtoms = (line.StartsWith("@<TRIPOS>ATOM"));
        } else {
            if (line.StartsWith("@<TRIPOS>")) {
                readAtoms = false;
            } else {
                
                string[] splitLine = line.Split(new char[] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

                int residueNumber = int.Parse(splitLine[6]);
                string residueName = splitLine[7];

                ResidueID residueID;
                PDBID pdbID;
                if (geometry.atomMap != null) {
                    if (!geometry.atomMap.ContainsKey(atomIndex)) {
                        atomIndex++;
                        return;
                    }
                    AtomID atomID = geometry.atomMap[atomIndex];
                    residueID = atomID.residueID;
                    pdbID = atomID.pdbID;
                } else {
                    residueID = new ResidueID(chainID, residueNumber);


                    pdbID = GetMol2PDBID(line.Substring(8, 4), residueName);
                    if (pdbID.IsEmpty()) {
                        charNum = 8;
                        throw new System.Exception(string.Format(
                            "PDBID is empty!"
                        ));
                    }
                }


                string amber = splitLine[5];
                if (amber == "DU") {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Atom {0} in Residue {1} has AMBER Type {2} (not recognised) - Check that this residue is capped and protonated.",
                        residueID,
                        pdbID,
                        amber
                    );
                }
                
                //Position
                float3 position = new float3 (
                    float.Parse(splitLine[2]),
                    float.Parse(splitLine[3]),
                    float.Parse(splitLine[4])
                );

                float partialCharge = 0f;
                if (splitLine.Length >= 9) {
                    partialCharge = float.Parse(splitLine[8]);
                }
                
                if (!geometry.HasResidue(residueID)) {
                    geometry.AddResidue(residueID, new Residue(residueID, residueName, geometry));
                    CustomLogger.LogFormat(EL.VERBOSE, "Adding Residue. ID: {0}. Name: {1}", residueID, residueName);
                }
                Atom atom = new Atom(position, residueID, AmberCalculator.GetAmber(amber), partialCharge);
                geometry.GetResidue(residueID).AddAtom(pdbID, atom, false);
                CustomLogger.LogFormat(EL.VERBOSE, "Adding Atom. ID: {0}. Position: {1}", pdbID, position);
                
                atomIndex++;
            }
        }
	}

    public IEnumerator SetAtomAmbersFromMol2File(string path, Geometry geometry, ChainID chainID=ChainID.A, Map<AtomID, int> atomMap=null) {
        return FileReader.UpdateGeometry(geometry, path, updateAmbers:true, atomMap:atomMap, chainID:chainID);
    }

    public IEnumerator SetAtomChargesFromMol2File(string path, Geometry geometry, ChainID chainID=ChainID.A, Map<AtomID, int> atomMap=null) {
        return FileReader.UpdateGeometry(geometry, path, updateCharges:true, atomMap:atomMap, chainID:chainID);
    }

    private static PDBID GetMol2PDBID(string input, string residueName) {
        return PDBID.FromString(input[3] + input.Substring(0, 3), residueName);
    }

}
