using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using EL = Constants.ErrorLevel;
using ChainID = Constants.ChainID;
using System.Linq;

/// <summary>
/// File Reader Class
/// Loads information from files into an Geometry object
/// </summary>
public static class FileReader {

	
	/// <summary>
	/// Returns the appropriate Geometry Loader IEnumerator based on the path's file extension
	/// </summary>
	static IEnumerator GetGeometryLoader(Geometry geometry, string path, ChainID chainID=ChainID._) {

		string filetype = Path.GetExtension (path).ToLower();
		switch (filetype) {
		case ".com": 
		case ".gjf":
			return new GaussianInputReader(geometry, chainID).GeometryFromFile (path);
		case ".log":
			return new GaussianOutputReader(geometry, chainID).GeometryFromFile (path);
		case ".pdb":
			return new PDBReader(geometry, chainID).GeometryFromFile (path);
		case ".pqr":
			return new PQRReader(geometry, chainID).GeometryFromFile (path);
		case ".xat":
			return XATReader.GeometryFromXATFile (path, geometry, chainID);
		case ".mol2":
			return new Mol2Reader(geometry, chainID).GeometryFromFile(path);
		case ".chk":
			return GetCHKLoader(geometry, path);
		case ".fchk":
			return new FChkReader(geometry).GeometryFromFile(Path.ChangeExtension(path, ".fchk"));
		case ".cub":
			return new CubeReader(geometry).GeometryFromFile(path);
		default:
			throw new ErrorHandler.FileTypeNotRecognisedException (string.Format("Filetype {0} not recognised", filetype), filetype);
		}
	}

	
	/// <summary>
	/// Returns the appropriate Geometry Loader IEnumerator based on the path's file extension
	/// </summary>
	static IEnumerator GetGeometryLoader(Geometry geometry, TextAsset asset) {

		string filetype = Path.GetExtension (asset.name).ToLower();
		switch (filetype) {
		case ".com":
		case ".gjf":
			return new GaussianInputReader(geometry).GeometryFromAsset (asset);
		case ".pdb":
			return new PDBReader(geometry).GeometryFromAsset (asset);
		case ".pqr":
			return new PQRReader(geometry).GeometryFromAsset (asset);
		case ".xat":
			return XATReader.GeometryFromAsset (asset, geometry);
		case ".mol2":
			return new Mol2Reader(geometry).GeometryFromAsset(asset);
		default:
			throw new ErrorHandler.FileTypeNotRecognisedException (string.Format("Filetype {0} not recognised", filetype), filetype);
		}
	}

	static IEnumerator GetCHKLoader(Geometry geometry, string path) {
		yield return GaussianCalculator.Formchk(path);
		yield return new FChkReader(geometry).GeometryFromFile(Path.ChangeExtension(path, ".fchk"));
	}

	public static IEnumerator UpdateGeometry(
		Geometry geometry, 
		string path,
        bool updateAmbers=false, 
        bool updateCharges=false, 
        bool updatePositions=false, 
        Map<AtomID, int> atomMap=null,
		ChainID chainID=ChainID._
	) {
		
		Geometry tempGeometry = PrefabManager.InstantiateGeometry(null);

		tempGeometry.atomMap = atomMap ?? geometry.atomMap;

		yield return LoadGeometry(tempGeometry, path, chainID:chainID);


		if (chainID == ChainID._) {
			//Update all atoms with exact match
			foreach ((AtomID atomID, Atom atom) in geometry.EnumerateAtomIDPairs()) {
				//Skip atoms with a different chainID

				Atom tempAtom;
				if (tempGeometry.TryGetAtom(atomID, out tempAtom)) {
					if (updateAmbers) {
						atom.amber = tempAtom.amber;
					}
					if (updateCharges) {
						atom.partialCharge = tempAtom.partialCharge;
					}
					if (updatePositions) {
						atom.position = tempAtom.position;
					}
				}
				if (Timer.yieldNow) {
					yield return null;
				}
			}

		} else {
			// Update atoms forcing to a particular chain
			foreach ((AtomID atomID, Atom atom) in geometry.EnumerateAtomIDPairs()) {
				//Skip atoms with a different chainID
				if (atomID.residueID.chainID != chainID) {
					continue;
				}

				AtomID altAtomID = atomID;
				altAtomID.residueID.chainID = ChainID._;

				Atom tempAtom;
				if (tempGeometry.TryGetAtom(atomID, out tempAtom) || tempGeometry.TryGetAtom(altAtomID, out tempAtom)) {
					if (updateAmbers) {
						atom.amber = tempAtom.amber;
					}
					if (updateCharges) {
						atom.partialCharge = tempAtom.partialCharge;
					}
					if (updatePositions) {
						atom.position = tempAtom.position;
					}
				}
				if (Timer.yieldNow) {
					yield return null;
				}
			}
		}


		GameObject.Destroy(tempGeometry.gameObject);

	}

	/// <summary>
	/// Load <c>Geometry</c> from a file from path
	/// </summary>
	/// <param name="geometry">The Geometry object to load the file into.</param>
	/// <param name="path">The full path of the file.</param>
	/// <param name="loaderName">(optional) the class that is using this method.</param>
	/// <param name="chainID">(optional) the default Chain ID to set missing Chain IDs.</param>
	/// <returns>IEnumerator</returns>
	public static IEnumerator LoadGeometry(Geometry geometry, string path, string loaderName=null, ChainID chainID=ChainID._) {

		IEnumerator atomLoader = GetGeometryLoader(geometry, path, chainID);

		geometry.path = path;
		
		while (true) {
			object current;
			try {
				if (atomLoader.MoveNext() == false) break;
				current = atomLoader.Current;
			} catch (ErrorHandler.ResidueMismatchException e) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Failed to load {0}. Tried to add Residue Name {1} to Residue {2} ({3}).",
					path,
					e.NewResidueName,
					e.ResidueNum,
					e.ResidueName
				);
				yield break;
			} 
			yield return current;

		}

		if (geometry != null && geometry.size != 0) {
			if (loaderName == null) {
				CustomLogger.LogFormat(
					EL.INFO,
					"Geometry loaded from {0}", 
					Path.GetFileName(path)
				);
			} else {
				CustomLogger.LogFormat(
					EL.INFO,
					"Geometry loaded from {0} ({1})", 
					Path.GetFileName(path), 
					loaderName
				);
			}
		}
	}

	/// <summary>
	/// Load <c>Geometry</c> from a file from path
	/// </summary>
	/// <param name="geometry">The Geometry object to load the file into.</param>
	/// <param name="asset">The TextAsset to load.</param>
	/// <param name="loaderName">(optional) the class that is using this method.</param>
	/// <returns>IEnumerator</returns>
	public static IEnumerator LoadGeometry(Geometry geometry, TextAsset asset, string loaderName=null) {
		
		IEnumerator atomLoader = GetGeometryLoader(geometry, asset);
		
		while (true) {
			object current;
			try {
				if (atomLoader.MoveNext() == false) break;
				current = atomLoader.Current;
			} catch (ErrorHandler.ResidueMismatchException e) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Failed to load {0}. Tried to add Residue Name {1} to Residue {2} ({3}).",
					asset.name,
					e.NewResidueName,
					e.ResidueNum,
					e.ResidueName
				);
				yield break;
			} 
			yield return current;

		}

		if (loaderName != null) {
			CustomLogger.LogFormat(
				EL.INFO,
				"Geometry loaded from {0} ({1})", 
				asset.name, 
				loaderName
			);
		}
	}

	/// <summary>
	/// Notify the user of an error while reading a file
	/// </summary>
	/// <param name="path">The full path of the file.</param>
	/// <param name="lineNumber">The line number that the error occured on.</param>
	/// <param name="charNum">The character number that the error occured on.</param>
	/// <param name="methodName">The name of the method that caused the error.</param>
	/// <param name="line">The string representation of the line.</param>
	/// <param name="error">The error object.</param>
	/// <returns>void</returns>
	public static void ThrowFileReaderError(
		string path, 
		int lineNumber, 
		int charNum, 
		string methodName, 
		string line, 
		System.Exception error
	) {

		CustomLogger.LogFormat(EL.ERROR, error.Message);
		CustomLogger.LogOutput(
			"Failed to read {0}. Line: {1}:{2} (failed on {3}){7}{4}{7}{5}{7} Trace:{7}{6}",
			path,
			lineNumber,
			charNum,
			methodName,
			line,
			(charNum < 1) ? "^" : string.Join("", Enumerable.Repeat(' ', charNum - 1)) + "^",
			error.ToString(),
			FileIO.newLine
		);

	}
		
}