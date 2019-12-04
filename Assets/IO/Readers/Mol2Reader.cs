using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.Mathematics;
using EL = Constants.ErrorLevel;

public class Mol2Reader : GeometryReader {
    
    bool readAtoms;
    string chainID = "A";

    public Mol2Reader(Geometry geometry) {
        readAtoms = false;
    }

    public override bool ParseLine(Geometry geometry) {

		geometry.atomMap = new Map<AtomID, int>();

		if (!readAtoms) {
            readAtoms = (line.StartsWith("@<TRIPOS>ATOM"));
        } else {
            if (line.StartsWith("@<TRIPOS>")) {
                readAtoms = false;
            } else {
                
                string[] splitLine = line.Split(new char[] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

                int residueNumber;
                if (!TryGetInt(line, splitLine[6], "GetResidueNumber", out residueNumber)) {
                    return false;
                }

                ResidueID residueID = new ResidueID(chainID, residueNumber);
                string residueName = splitLine[7];

                //Add atom to residue
                if (!geometry.residueDict.ContainsKey (residueID)) {
                    geometry.residueDict[residueID] = new Residue(residueID, residueName, geometry);
                    CustomLogger.LogFormat(EL.VERBOSE, "Adding Residue. ID: {0}. Name: {1}", residueID, residueName);
                }
                
                PDBID pdbID = GetMol2PDBID(line.Substring(8, 4), residueName);
                if (pdbID.IsEmpty()) {
                    ThrowError("ReadPDBID");
                    return false;
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
                float3 position = float3.zero;
                if (!TryGetFloat(line, splitLine[2], "GetPositionX", out position.x)) {return false;}
                if (!TryGetFloat(line, splitLine[3], "GetPositionY", out position.y)) {return false;}
                if (!TryGetFloat(line, splitLine[4], "GetPositionZ", out position.z)) {return false;}

                float partialCharge = 0f;
                if (splitLine.Length >= 9) {
                    if (!TryGetFloat(line, splitLine[8], "GetPartialCharge", out partialCharge)) {
                        return false;
                    }
                }
                
                Atom atom = new Atom(position, residueID, amber, partialCharge);
                geometry.residueDict[residueID].AddAtom(pdbID, atom, false);
                CustomLogger.LogFormat(EL.VERBOSE, "Adding Atom. ID: {0}. Position: {1}", pdbID, position);
            }
            
        }

		return true;
	}

    public IEnumerator SetAtomAmbersFromMol2File(string path, Geometry geometry, string chainID="A", Dictionary<int, AtomID> atomNumToAtomIDDict=null) {
        return SetAtomsInfoFromMol2File(path, geometry, chainID, setAmber:true, atomNumToAtomIDDict:atomNumToAtomIDDict);
    }

    public IEnumerator SetAtomChargesFromMol2File(string path, Geometry geometry, string chainID="A", Dictionary<int, AtomID> atomNumToAtomIDDict=null) {
        return SetAtomsInfoFromMol2File(path, geometry, chainID, setCharge:true, atomNumToAtomIDDict:atomNumToAtomIDDict);
    }

    private IEnumerator SetAtomsInfoFromMol2File(string path, Geometry geometry, string chainID="A", bool setAmber=false, bool setCharge=false, Dictionary<int, AtomID> atomNumToAtomIDDict=null) {
        //Add AMBER information to geometry using Antechamber
        //Need to supply chainID info which is omitted in Antechamber
        //This means the method will break if multiple chains are used (which aren't supported by Antechamber anyway)

		string[] lines = FileIO.Readlines (path, "#");

        bool readAtoms = false;
        int atomNum = 0;
		for ( lineNumber=0; lineNumber < lines.Length; lineNumber++) {
            line = lines[lineNumber];
            if (line == FileIO.newLine) continue;

            if (!readAtoms) {
                readAtoms = (line.StartsWith("@<TRIPOS>ATOM"));
            } else {
                if (line.StartsWith("@<TRIPOS>")) {
                    readAtoms = false;
                } else {
                    //Read geometry
                    string[] splitLine = line.Split(new char[] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

                    string residueName = splitLine[7];
                    int residueNumber = int.Parse(splitLine[6]);

                    ResidueID residueID;
                    PDBID pdbID;
                    if (atomNumToAtomIDDict != null) {
                        if (!atomNumToAtomIDDict.ContainsKey(atomNum)) {
                            atomNum++;
                            continue;
                        }
                        AtomID atomID = atomNumToAtomIDDict[atomNum];
                        residueID = atomID.residueID;
                        pdbID = atomID.pdbID;
                    } else {
                        residueID = new ResidueID(chainID, residueNumber);
                        pdbID = GetMol2PDBID(line.Substring(8, 4), residueName);
                        if (pdbID.IsEmpty()) {
                            ThrowError("ReadPDB");
                        }
                    }

                    Atom atom;
                    if (!geometry.TryGetAtom(residueID, pdbID, out atom)) {
                        throw new ErrorHandler.PDBIDException(
                            string.Format(
                                "PDBID {0} was not found in Residue {1} of Geometry {2}",
                                pdbID,
                                residueID,
                                geometry.name
                            )
                        );
                    }
                    

                    if (setAmber) {
                        string amber = splitLine[5];
                        if (amber == "DU") {
                            CustomLogger.LogFormat(
                                EL.ERROR,
                                "Atom {0} in Resiude {1} has AMBER Type {2} (not recognised) - Check that this residue is capped and protonated.",
                                residueID,
                                pdbID,
                                amber
                            );
                        }
                        atom.amber = amber;
                    }
                        
                    if (setCharge) {
                        atom.partialCharge = float.Parse(splitLine[8]);
                    }

                    atomNum++;
                }
                if (Timer.yieldNow) {yield return null;}
            }
        }
    }

    private static PDBID GetMol2PDBID(string input, string residueName) {
        return PDBID.FromString(input[3] + input.Substring(0, 3), residueName);
    }

}
