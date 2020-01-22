using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Element = Constants.Element;
using RCID = Constants.ResidueCheckerID;
using ACID = Constants.AtomCheckerID;
using GIS = Constants.GeometryInterfaceStatus;
using EL = Constants.ErrorLevel;
using RS = Constants.ResidueState;
using Amber = Constants.Amber;


/// <summary>Atoms Checker Class</summary>
/// <remarks>
/// Contains Residues and represents a single geometry.
/// Performs tasks on collections of Residues and Atom objects.
/// Holds a reference to a Molecular Mechanics Parameters object.
/// Holds a reference to a Gaussian Calculator object.
/// </remarks>
public class GeometryChecker : MonoBehaviour {
    //Perform a series of checks on an Geometry object
    //Checks should be a list of functions so the expensive
    // Take() method of Geometry is called as few times as possible 
    
    private Geometry geometry;

    private int size;
    public int numResidues;

    private List<int> protonatedResidues;
    public List<RCID> residueCheckerOrder;
    public List<ACID> atomCheckerOrder;

    public Dictionary<RCID, GIS> residueErrorLevels;
    public Dictionary<ACID, GIS> atomErrorLevels;

    public Dictionary<RCID, ResidueChecker> residueCheckers;
    public Dictionary<ACID, AtomChecker> atomCheckers;

    public GIS errorLevel;

    public void Awake() {
        residueCheckerOrder = new List<RCID>();
        atomCheckerOrder = new List<ACID>();

        residueCheckers = new Dictionary<RCID, ResidueChecker>();
        atomCheckers = new Dictionary<ACID, AtomChecker>();

        residueErrorLevels = new Dictionary<RCID, GIS>();
        atomErrorLevels = new Dictionary<ACID, GIS>();
    }

    public void SetGeometry(Geometry geometry) {
        if (geometry == null) {
            throw new System.NullReferenceException("Cannot use empty Geometry for checker");
        }
        this.geometry = geometry;
        this.size = geometry.size;
        this.numResidues = geometry.residueDict.Count;
        this.errorLevel = GIS.OK;
    }

    public void SetChecks(
        List<RCID> residueCheckIDs,
        List<ACID> atomCheckIDs,
        Dictionary<RCID, GIS> residueErrorLevels,
        Dictionary<ACID, GIS> atomErrorLevels
    ) {

        residueCheckerOrder = residueCheckIDs;
        atomCheckerOrder = atomCheckIDs;

        this.residueErrorLevels = residueErrorLevels;
        this.atomErrorLevels = atomErrorLevels;

        foreach (RCID residueCheckID in residueCheckIDs) {
            residueCheckers[residueCheckID] = new ResidueChecker(residueCheckID, residueErrorLevels[residueCheckID]);
        }

        foreach (ACID atomCheckID in atomCheckIDs) {
            atomCheckers[atomCheckID] = new AtomChecker(atomCheckID, atomErrorLevels[atomCheckID]);
        }
    }

    public IEnumerator Check() {

        GIS result;

        foreach (ResidueID residueID in geometry.residueDict.Keys.ToList()) {
            foreach (RCID rcid in residueCheckerOrder) {
                result = residueCheckers[rcid].Check(geometry, residueID);
                errorLevel = (GIS)Mathf.Max((int)result, (int)errorLevel);
            }

            foreach (PDBID pdbID in geometry.residueDict[residueID].pdbIDs) {
                foreach (ACID acid in atomCheckerOrder) {
                    result = atomCheckers[acid].Check(geometry, residueID, pdbID);
                    errorLevel = (GIS)Mathf.Max((int)result, (int)errorLevel);
                }
            }

            if (Timer.yieldNow) { yield return null;}
        }
        yield return null;

    }
    
}

public class ResidueChecker {
    //Class that hold a function to check a residue of an Geometry object
    //Pass in just the residue
    public delegate GIS CheckResidueFunction(Geometry geometry, ResidueID residueID);

    public RCID residueCheckerID;
    public string title;
    public string description;
    public GIS errorLevel;


    public CheckResidueFunction checkFunction;

    public ResidueChecker(RCID residueCheckerID, GIS errorLevel) {
        this.residueCheckerID = residueCheckerID;
        this.errorLevel = errorLevel;
        this.title = Settings.GetResidueCheckerTitle(residueCheckerID);
        this.description = Settings.GetResidueCheckerDescription(residueCheckerID);
        this.checkFunction = GetCheckResidueFunction(residueCheckerID);
    }

    public CheckResidueFunction GetCheckResidueFunction(RCID residueCheckerID) {
        switch (residueCheckerID) {
            case (RCID.PROTONATED): return Protonated;
            case (RCID.PDBS_UNIQUE): return NoRepeatedPDBS;
            case (RCID.STANDARD): return ResiduesAreStandard;
            case (RCID.PARTIAL_CHARGES): return HasCharges;
            case (RCID.INTEGER_CHARGE): return HasIntegerCharge;
        }
        throw new ErrorHandler.InvalidResidueCheckerID(
			string.Format(
				"Invalid Residue Checker ID: {0}",
				residueCheckerID
			)
		);
    }

    public GIS Check(Geometry geometry, ResidueID residueID) {
        return checkFunction(geometry, residueID);
    }

    private GIS Protonated(Geometry geometry, ResidueID residueID) {
        if (geometry.residueDict[residueID].protonated) {
            return GIS.OK;
        }
        Fail(string.Format("Residue {0} is not protonated in Geometry {1}", residueID, geometry.name));
        return errorLevel;
    }

    private GIS NoRepeatedPDBS(Geometry geometry, ResidueID residueID) {
        List<PDBID> pdbIDs = geometry.residueDict[residueID].pdbIDs.ToList();
        if (new HashSet<PDBID>(pdbIDs).Count == pdbIDs.Count) {
            return GIS.OK;
        }
        Fail(string.Format("Residue {0} has repeated PDBIDs in Geometry {1}", residueID, geometry.name));
        return errorLevel;
    }

    private GIS HasCharges(Geometry geometry, ResidueID residueID) {
        Residue residue = geometry.GetResidue(residueID);

        //Ignore Caps
        if (residue.state == RS.CAP) {return GIS.OK;}

        float absCharge = residue.EnumerateAtoms().Select(x => Mathf.Abs(x.Item2.partialCharge)).Sum();
        if (absCharge > Settings.partialChargeThreshold) {
            return GIS.OK;
        }
        Fail(string.Format("Residue {0} does not have partial charges in Geometry {1}", residueID, geometry.name));
        return errorLevel;
    }

    private GIS HasIntegerCharge(Geometry geometry, ResidueID residueID) {
        Residue residue = geometry.GetResidue(residueID);

        //Ignore Caps
        if (residue.state == RS.CAP) {return GIS.OK;}

        float totalCharge = residue.GetCharge();
        if (Mathf.Abs(totalCharge - Mathf.RoundToInt(totalCharge)) < Settings.integerChargeThreshold) {
            return GIS.OK;
        }
        Fail(
            string.Format(
                "Residue {0} does not have an integer charge in Geometry {1} - ({2})", 
                residueID, 
                geometry.name,
                totalCharge                
            )
        );
        return errorLevel;
    }

    private GIS ResiduesAreStandard(Geometry geometry, ResidueID residueID) {
        if (geometry.residueDict[residueID].standard) {
            return GIS.OK;
        }
        Fail(string.Format("Residue {0} is non-standard in Geometry {1}", residueID, geometry.name));
        return errorLevel;
    }

    private void Fail(string message) {
        if (errorLevel == GIS.ERROR) {
            CustomLogger.Log(EL.ERROR, message);
        } else if (errorLevel == GIS.WARNING) {
            CustomLogger.Log(EL.WARNING, message);
        }
    }
}

public class AtomChecker {
    //Class that hold a function to check an atom
    public delegate GIS CheckAtomFunction(Geometry geometry, ResidueID residueID, PDBID pdbID);

    public ACID atomCheckerID;
    public string title;
    public string description;
    public GIS errorLevelOnFail;

    private Regex alphanum = new Regex("^[a-zA-Z0-9 ]*$");

    private CheckAtomFunction checkFunction;

    public AtomChecker(ACID atomCheckerID, GIS errorLevelOnFail) {
        this.atomCheckerID = atomCheckerID;
        this.errorLevelOnFail = errorLevelOnFail;
        this.title = Settings.GetAtomCheckerTitle(atomCheckerID);
        this.description = Settings.GetAtomCheckerDescription(atomCheckerID);
        this.checkFunction = GetCheckAtomFunction(atomCheckerID);;
    }

    private CheckAtomFunction GetCheckAtomFunction(ACID atomCheckerID) {
        switch (atomCheckerID) {
            case (ACID.HAS_PDB): return HasPDB;
            case (ACID.HAS_AMBER): return HasAmber;
            case (ACID.HAS_VALID_AMBER): return HasValidAmber;
            case (ACID.PDBS_ALPHANUM): return AlphaNumericPDB;
        }
        throw new ErrorHandler.InvalidAtomCheckerID(
			string.Format(
				"Invalid Atom Checker ID: {0}",
				atomCheckerID
			)
		);
    }

    public GIS Check(Geometry geometry, ResidueID residueID, PDBID pdbID) {
        return checkFunction(geometry, residueID, pdbID);
    }

    private GIS HasPDB(Geometry geometry, ResidueID residueID, PDBID pdbID) {
        if (pdbID.element != Element.X) {
            return GIS.OK;
        }
        Fail(string.Format("Atom {0} in Residue {1} has no PDB Type in Geometry {2}", pdbID, residueID, geometry));
        return errorLevelOnFail;
    }

    private GIS HasAmber(Geometry geometry, ResidueID residueID, PDBID pdbID) {
        Amber amber = geometry.residueDict[residueID].atoms[pdbID].amber;
        if (amber != Amber._ && amber != Amber.X) {
            return GIS.OK;
        }
        Fail(string.Format("Atom {0} in Residue {1} has no Amber Type in Geometry {2}", pdbID, residueID, geometry));
        return errorLevelOnFail;
    }

    private GIS HasValidAmber(Geometry geometry, ResidueID residueID, PDBID pdbID) {
        Amber amber = geometry.residueDict[residueID].atoms[pdbID].amber;
        if (amber != Amber.DU) {
            return GIS.OK;
        }
        Fail(string.Format("Atom {0} in Residue {1} has a Dummy Amber Type in Geometry {2}", pdbID, residueID, geometry));
        return errorLevelOnFail;
    }

    private GIS AlphaNumericPDB(Geometry geometry, ResidueID residueID, PDBID pdbID) {
        if (alphanum.IsMatch(pdbID.ToString())) {
            return GIS.OK;
        }
        Fail(string.Format("Atom {0} in Residue {1} has an invalid PDBID in Geometry {2}", pdbID, residueID, geometry));
        return errorLevelOnFail;
    }

	private string ToAlpha(string inputStr) {
		return System.Text.RegularExpressions.Regex.Replace (inputStr, @"[^a-zA-Z-]", string.Empty);
	}

    private bool StringNotEmpty(string testString) {
        return (testString != null && testString != string.Empty);
    }

    private void Fail(string message) {
        if (errorLevelOnFail == GIS.ERROR) {
            CustomLogger.Log(EL.ERROR, message);
        } else if (errorLevelOnFail == GIS.WARNING) {
            CustomLogger.Log(EL.WARNING, message);
        }
    }

}
