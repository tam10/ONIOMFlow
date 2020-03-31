using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using System.Text;
using System.IO;
using System.Linq;
using BT = Constants.BondType;

public static class PDBWriter {
	public static IEnumerator WritePDBFile(Geometry geometry, string path, bool writeConnectivity=true) {

		StringBuilder sb = new StringBuilder ();
		string format = "ATOM  {0,5} {1,-4} {2,3} {3,1}{4,4}    {5,8:.000}{6,8:.000}{7,8:.000}                      {8,2}" + FileIO.newLine;

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

		if (generateAtomMap) {
			
			List<ResidueID> residueIDs = geometry.EnumerateResidueIDs().ToList();
			residueIDs.Sort();

			int numResidues = residueIDs.Count;
			int atomNum = 0;
			for (int residueNum = 0; residueNum < numResidues; residueNum++) {
				ResidueID residueID = residueIDs[residueNum];
				Residue residue = geometry.GetResidue(residueID);
				List<PDBID> pdbIDs = residue.pdbIDs.ToList();
				pdbIDs.Sort();
				foreach (PDBID pdbID in pdbIDs) {

					if (writeConnectivity) {
						AtomID atomID = new AtomID(residueID, pdbID);
						atomMap[atomID] = atomNum;
					}

					float3 position = residue.GetAtom(pdbID).position;
					atomNum++;
					sb.Append (
						string.Format (
							format,
							atomNum,
							pdbID,
							residue.residueName,
							residue.chainID,
							residueID.residueNumber,
							position.x,
							position.y,
							position.z,
							pdbID.element
						)
					);
				}

				//Use TER record if not connected to next residue
				if (residueNum == numResidues - 1 || !residue.NeighbouringResidues().Contains(residueIDs[residueNum + 1])) {
					sb.Append ("TER" + FileIO.newLine);
				}

				if (Timer.yieldNow) {yield return null;}
			}
		} else {
			
			for (int atomNum = 0; atomNum < atomMap.Count; atomNum++) {
				(ResidueID residueID, PDBID pdbID) = atomMap[atomNum];
				Residue residue = geometry.GetResidue(residueID);
				float3 position = residue.GetAtom(pdbID).position;
				sb.Append (
					string.Format (
						format,
						atomNum + 1,
						pdbID,
						residue.residueName,
						residue.chainID,
						residueID.residueNumber,
						position.x,
						position.y,
						position.z,
						pdbID.element
					)
				);
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
						connectionIndex = atomMap[neighbour] + 1;
					} catch (KeyNotFoundException) {
						//Atom might have been deleted. Remove connection from this atom
						atom.TryDisconnect(neighbour);
						//geometry.Disconnect(new AtomID(residueID, pdbID), atomID1);
						continue;
					}
					connectionList.Add(connectionIndex);
				}

				if (connectionList.Count == 0) {
					continue;
				}

				sb.AppendFormat(cFormat, atomNum + 1);
				connectionList.Sort();
				foreach (int connectionIndex in connectionList) {
					sb.AppendFormat(format, connectionIndex);
				}
				sb.Append (FileIO.newLine);
			}

			if (Timer.yieldNow) {yield return null;}
			
		}

		File.WriteAllText (path, sb.ToString ());

	}
}
