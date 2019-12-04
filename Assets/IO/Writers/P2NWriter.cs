using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Text;
using System.IO;
using System.Linq;
using RS = Constants.ResidueState;

public static class P2NWriter { 

    public static IEnumerator WriteP2NFile(Geometry geometry, string path, bool writeConnectivity) {
        
		StringBuilder atomsSb = new StringBuilder ();
		StringBuilder headerSb = new StringBuilder ();

		string format = "ATOM  {0,5} {1,-4} {2,3}  {3,4}    {4,8:.000}{5,8:.000}{6,8:.000}                      {7,2}" + FileIO.newLine;

		// Atom map for connectivity
		Map<AtomID, int> atomMap;
		bool generateAtomMap = false;
		if (geometry.atomMap == null || geometry.atomMap.Count != geometry.size) {
			atomMap = new Map<AtomID, int>();
			generateAtomMap = true;
		} else {
			atomMap = geometry.atomMap;
			generateAtomMap = false;
		}

        // Keep track of capping atoms
        List<int> cappingAtoms = new List<int>();

        float totalCharge = 0f;

		if (generateAtomMap) {
			
			List<ResidueID> residueIDs = geometry.residueDict.Keys.ToList();
			residueIDs.Sort();

			int numResidues = residueIDs.Count;
			int atomNum = 0;
			for (int residueNum = 0; residueNum < numResidues; residueNum++) {
				ResidueID residueID = residueIDs[residueNum];
				Residue residue = geometry.residueDict[residueID];
				List<PDBID> pdbIDs = residue.pdbIDs.ToList();
				pdbIDs.Sort();
				foreach (PDBID pdbID in pdbIDs) {

					if (writeConnectivity) {
						AtomID atomID = new AtomID(residueID, pdbID);
						atomMap[atomID] = atomNum;
					}

					Atom atom = residue.atoms[pdbID];
					float3 position = atom.position;
					atomNum++;
					atomsSb.Append (
						string.Format (
							format,
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
					if (residue.state == RS.CAP) {
						cappingAtoms.Add(atomNum);
					}
				}

				if (Timer.yieldNow) {yield return null;}

			}
		} else {
			for (int atomNum = 0; atomNum < atomMap.Count; atomNum++) {
				(ResidueID residueID, PDBID pdbID) = atomMap[atomNum];
				Residue residue = geometry.GetResidue(residueID);
				Atom atom = residue.atoms[pdbID];
				float3 position = atom.position;
				atomNum++;
				atomsSb.Append (
					string.Format (
						format,
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
				if (residue.state == RS.CAP) {
					cappingAtoms.Add(atomNum);
				}

				if (Timer.yieldNow) {yield return null;}

			}

		}

		if (writeConnectivity) {
			
			string cFormat = "CONECT {0,5}";
			format = "{0,5}";

			//Second loop for connectivity once atomMap is created
			for (int atomNum = 0; atomNum < atomMap.Count; atomNum++) {

				Atom atom = geometry.GetAtom(atomMap[atomNum]);

				List<AtomID> neighbours = atom.EnumerateNeighbours().ToList();
				List<int> connectionList = new List<int>();

				foreach (AtomID neighbour in neighbours) {
					int connectionIndex;
					try {
						connectionIndex = atomMap[neighbour];
					} catch (KeyNotFoundException) {
						//Atom might have been deleted. Remove connection from this atom
						atom.TryDisconnect(neighbour);
						continue;
					}
					connectionList.Add(connectionIndex);
				}

				if (connectionList.Count == 0) {
					continue;
				}

				atomsSb.AppendFormat(cFormat, atomNum);
				connectionList.Sort();
				foreach (int connectionIndex in connectionList) {
					atomsSb.AppendFormat(format, connectionIndex);
				}
				atomsSb.Append (FileIO.newLine);
			}

			if (Timer.yieldNow) {yield return null;}
        }
            

        headerSb.AppendFormat("REMARK{0}", FileIO.newLine);
        headerSb.AppendFormat("REMARK TITLE {0}{1}", geometry.name, FileIO.newLine);
        headerSb.AppendFormat("REMARK CHARGE-VALUE {0}{1}", Mathf.RoundToInt(totalCharge), FileIO.newLine);
        headerSb.AppendFormat("REMARK MULTIPLICITY-VALUE 1{0}", FileIO.newLine);
        headerSb.AppendFormat("REMARK INTRA-MCC 0.0 | {0} | Remove{1}", string.Join(" ", cappingAtoms), FileIO.newLine);
        headerSb.AppendFormat("REMARK{0}", FileIO.newLine);

		File.WriteAllText(path, headerSb.ToString ());
		File.AppendAllText(path, atomsSb.ToString ());
    }
}
