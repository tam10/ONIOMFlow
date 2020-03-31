using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using System.Text;
using System.IO;
using System.Linq;
using BT = Constants.BondType;


public static class MOL2Writer {
    public static IEnumerator WriteMol2File(Geometry geometry, string path, bool writeConnectivity=true) {

		StringBuilder headerSB = new StringBuilder ();
		StringBuilder atomsSB = new StringBuilder ();
        headerSB.Append("@<TRIPOS>MOLECULE" + FileIO.newLine);
		headerSB.Append(geometry.name + FileIO.newLine);
        string format = "{0,6} {1,-8} {2,9:.0000}{3,9:.0000}{4,9:.0000} {5,-6} {6,4} {7,10:.000000}" + FileIO.newLine;
        
        List<ResidueID> residueIDs = geometry.EnumerateResidueIDs().ToList();
        residueIDs.Sort();

		// Atom map for connectivity
		Dictionary<AtomID, int> atomMap = new Dictionary<AtomID, int>();

        atomsSB.Append("@<TRIPOS>ATOM" + FileIO.newLine);

		int atomNum = 0;
		int numResidues = residueIDs.Count;
		for (int residueNum = 0; residueNum < numResidues; residueNum++) {
			ResidueID residueID = residueIDs[residueNum];
			Residue residue = geometry.GetResidue(residueID);
			List<PDBID> pdbIDs = residue.pdbIDs.ToList();
			pdbIDs.Sort();
            
			foreach (PDBID pdbID in pdbIDs) {
                Atom atom = residue.GetAtom(pdbID);
				float3 position = atom.position;
				atomNum++;
				atomsSB.Append (
					string.Format (
						format,
						atomNum,
						pdbID.ToOldString(),
						position.x,
						position.y,
						position.z,
						AmberCalculator.GetAmberString(atom.amber),
                        residueID.residueNumber,
                        residue.residueName,
                        atom.partialCharge
					)
				);
				if (writeConnectivity) {
					AtomID atomID = new AtomID(residueID, pdbID);
					atomMap[atomID] = atomNum;
				}
			}

			if (Timer.yieldNow) {yield return null;}

        }

        
        int connectionNum = 0;
		if (writeConnectivity) {

            atomsSB.Append("@<TRIPOS>BOND" + FileIO.newLine);
			
			string cFormat = "{0,5} {1,4} {2,4} {3}" + FileIO.newLine;

			atomNum = 0;

			//Second loop for connectivity once atomMap is created
			foreach (ResidueID residueID in residueIDs) {
				Residue residue = geometry.GetResidue(residueID);
				List<PDBID> pdbIDs = residue.pdbIDs.ToList();
				pdbIDs.Sort();
				foreach (PDBID pdbID in pdbIDs) {
					foreach ((AtomID, BT) neighbour in residue.GetAtom(pdbID).EnumerateConnections().ToList()) {
						int connectionIndex;
						try {
							connectionIndex = atomMap[neighbour.Item1];
						} catch (KeyNotFoundException) {
							//Atom might have been deleted. Remove connection from this atom
							residue.GetAtom(pdbID).TryDisconnect(neighbour.Item1);
                            continue;
							//geometry.Disconnect(new AtomID(residueID, pdbID), atomID1);
                        }
                            
                        atomsSB.AppendFormat(
                                cFormat, 
                                connectionNum + 1,
                                atomNum + 1,
                                connectionIndex,
                                Settings.GetBondTriposString(neighbour.Item2)
                                //NEED TO ADD BOND TYPE
                            );
                        connectionNum++;
			
					}
					atomNum++;
				}
				if (Timer.yieldNow) {yield return null;}
			}
        }

		headerSB.AppendFormat("{0,4} {1,5} {2,5} {3,5} {4,5} {5}", atomNum, connectionNum, 1, 0, 0, FileIO.newLine);
		headerSB.AppendFormat("SMALL" + FileIO.newLine);
		headerSB.AppendFormat("USER_CHARGES" + FileIO.newLine);
		File.WriteAllText (path, headerSB.ToString() + atomsSB.ToString ());
    }
}
