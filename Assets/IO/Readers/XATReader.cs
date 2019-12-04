using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml.Linq;
using System.Xml;
using System.IO;
using Unity.Mathematics;
using EL = Constants.ErrorLevel;
using BT = Constants.BondType;

/// <summary>The static XAT Reader Class</summary>
/// <remarks>Populates an Geometry object using the contents of a .xat file.</remarks>
public static class XATReader {
    
    /// <summary>An internal reference to the XDocument that is being read.</summary>
    static XDocument xDocument;
    /// <summary>An internal reference to the dictionary of Residue IDs to Residue Names.</summary>
    static Dictionary<ResidueID, string> residueNameDict;
    
    static string path;
	static string text;

    /// <summary>Reads an XAT file and populates an Geometry object with the contents.</summary>
    /// <param name="filePath">The path of the XAT file.</param>
    /// <param name="geometry">The Geometry object to populate.</param>
    public static IEnumerator GeometryFromXATFile(string filePath, Geometry geometry) {

        path = filePath;
        geometry.name = Path.GetFileName (path);
		text = FileIO.Read (path);
        yield return ParseGeometry(geometry);
    }


    /// <summary>Reads an XAT file from a TextAsset and populates an Geometry object with the contents.</summary>
    /// <param name="asset">The TextAsset to load.</param>
    /// <param name="geometry">The Geometry object to populate.</param>
    public static IEnumerator GeometryFromAsset(TextAsset asset, Geometry geometry) {

		path = asset.name;
		geometry.name = Path.GetFileName(path);
		text = asset.text;

		yield return ParseGeometry(geometry);
    }

    /// <summary>Reads a string and populates an Geometry object with the contents.</summary>
    /// <param name="geometry">The Geometry object to populate.</param>
    public static IEnumerator ParseGeometry(Geometry geometry) {
        
        xDocument = XDocument.Parse(text);

        //Get the root element
        XElement atomsX = xDocument.Element("geometry");

        //Get all the names of the Residues to form a map
        residueNameDict = new Dictionary<ResidueID, string>();
        foreach (XElement residueX in atomsX.Elements("residue")) {
            ResidueID residueID = ResidueID.FromString(FileIO.ParseXMLAttrString(residueX, "ID"));
            string residueName = FileIO.ParseXMLAttrString(residueX, "name");
            residueNameDict[residueID] = residueName;
        }

        //Read the Residues
        foreach (XElement residueX in atomsX.Elements("residue")) {
            ReadResidue(geometry, residueX);
            if (Timer.yieldNow) {yield return null;}
        }

        //Read the Parameters if they exist
        XElement parametersX = atomsX.Element("parameters");
        if (parametersX != null) {
            ReadParameters(geometry.parameters, parametersX);
        }
    }

    /// <summary>Reads a Residue XElement and creates a Residue from it.</summary>
    /// <param name="geometry">The Geometry object to populate.</param>
    /// <param name="residueX">The Residue XElement to read.</param>
    private static void ReadResidue(Geometry geometry, XElement residueX) {
        //Get the ID and Name of the Residue
        ResidueID residueID = ResidueID.FromString(FileIO.ParseXMLAttrString(residueX, "ID"));
        string residueName = residueNameDict[residueID];

        //Create the Residue object
        Residue residue = new Residue(residueID, residueName, geometry);

        //Get the Residue State
        string stateString = FileIO.ParseXMLAttrString(residueX, "state", "");
        if (stateString != "") {
            residue.state = Constants.ResidueStateMap[FileIO.ParseXMLAttrString(residueX, "state")];
        }
        
        //Add to log if requested
        CustomLogger.LogFormat(
            EL.VERBOSE,
            "Adding Residue (ResidueID: '{0}'. Residue Name: '{1}'. State: '{2}')",
            () => new object[] {
                residueID,
                residueName,
                residue.state
            }
        );
        
        //Read all the Atom objects of this Residue
        foreach (XElement atomX in residueX.Elements("atom")) {
            try {
                PDBID pdbID = PDBID.FromString(FileIO.ParseXMLAttrString(atomX, "ID"), residueName);
                residue.AddAtom(pdbID, ReadAtom(geometry, atomX, residueID, pdbID));
            } catch (System.Exception e) {
                ThrowError(atomX, path, "ReadAtom", e);
            }
        }

        //Add the Residue to the Geometry object
        geometry.residueDict[residueID] = residue;
    }

    /// <summary>Reads an Atom XElement and creates an Atom from it.</summary>
    /// <param name="geometry">The Geometry object to populate.</param>
    /// <param name="atomX">The Atom XElement to read.</param>
    /// <param name="residueID">The ID of the Residue this Atom belongs to.</param>
    /// <param name="pdbID">The PDB ID of this Atom.</param>
    private static Atom ReadAtom(Geometry geometry, XElement atomX, ResidueID residueID, PDBID pdbID) {
        //Read position
        string[] positionStrings = FileIO.ParseXMLString(atomX, "xyz").Split(new[] {','});
        float3 position = new float3 (
            float.Parse(positionStrings[0]),
            float.Parse(positionStrings[1]),
            float.Parse(positionStrings[2])
        );

        //Read AMBER type
        string amber = FileIO.ParseXMLAttrString(atomX, "amber", "");
        
        //Read Partial Charge
        float partialCharge = FileIO.ParseXMLAttrFloat(atomX, "charge", 0f);
        
        Atom atom = new Atom(position, residueID, amber, partialCharge);

        //Parse connections
        ReadBonds(atomX, atom, residueID, pdbID);

        //Read ONIOM Layer
        char oniomLayerString = FileIO.ParseXMLAttrString(atomX, "layer", " ")[0];
        atom.oniomLayer = oniomLayerString == ' ' ? 
            Constants.OniomLayerID.REAL : 
            Constants.OniomLayerIDCharMap[oniomLayerString];

        //Log results if requested
        CustomLogger.LogFormat(
            EL.DEBUG,
            "Adding Atom. PDBID: '{0}', AMBER: '{1}', Position: '{2}', ONIOM Layer: '{3}'. Partial Charge: '{4}'",
            () => new object[] {
                pdbID,
                amber,
                position,
                atom.oniomLayer,
                partialCharge
            }
        );

        return atom;
    }

    /// <summary>Reads the Connections in an Atom XElement and forms Connections.</summary>
    /// <param name="atomX">The Atom XElement to read.</param>
    /// <param name="atom">The Atom to form Connections from.</param>
    /// <param name="residueID">The ID of the Residue this Atom belongs to.</param>
    /// <param name="pdbID">The PDB ID of this Atom.</param>
    private static void ReadBonds(XElement atomX, Atom atom, ResidueID residueID, PDBID pdbID) {
        string bondsString = FileIO.ParseXMLString(atomX, "bonds", "");

        //Return if the string is empty
        if (string.IsNullOrEmpty(bondsString)) {
            return;
        }

        //Connections are comma separated
        string[] bondStrings = bondsString.Split(new[] {','});
        foreach (string bondString in bondStrings) {

            Connection connection;
            try {
                //Try to parse a connection
                connection = Connection.FromString(bondString, residueID, residueNameDict);
            } catch (System.Exception e) {
                //Syntax problem - report and skip
                ThrowError(atomX, path, "ReadConnection", e);
                continue;
            }

            //Deconstruct Connection
            (AtomID atomID, BT bondType) = connection;

            if (atomID.IsEmpty()) {
                ThrowError(atomX, path, "ReadConnection");
                continue;
            }

            //Connect Atom
            atom.Connect(atomID, bondType);

            //Log if requested
            CustomLogger.LogFormat(
                EL.DEBUG,
                "Connecting Geometry '{0}'-'{1}' ({2})",
                () => new object[] {
                    new AtomID(residueID, pdbID),
                    atomID,
                    bondType
                }
            );
        }
        
    }

    /// <summary>Reads a Parameters XElement and populates a Parameters object from it.</summary>
    /// <param name="parameters">The Parameters object to populate.</param>
    /// <param name="parametersX">The Parameters XElement to read.</param>
    private static void ReadParameters(Parameters parameters, XElement parametersX) {
        //Read each component
        ReadNonBonding(parameters.nonbonding, parametersX.Element("nonbonding"));
        ReadStretches(parameters, parametersX.Element("stretches"));
        ReadBends(parameters, parametersX.Element("bends"));
        ReadTorsions(parameters, parametersX.Element("torsions"));
        ReadImproperTorsions(parameters, parametersX.Element("improperTorsions"));
    }

    /// <summary>Reads a NonBonding XElement and populates a NonBonding object from it.</summary>
    /// <param name="nonbonding">The NonBonding object to populate.</param>
    /// <param name="nonBondingX">The NonBonding XElement to read.</param>
    private static void ReadNonBonding(NonBonding nonbonding, XElement nonBondingX) {

        //Read the Van der Waals Type and Cutoff
        nonbonding.vdwType = Constants.VanDerWaalsTypeStringMap[FileIO.ParseXMLString(nonBondingX, "vdwType")];
        nonbonding.vCutoff = FileIO.ParseXMLInt(nonBondingX, "vdwCutoff");
        
        //Read the Van der Waals Scale Factors
        string[] vScaleStrings = FileIO.ParseXMLString(nonBondingX, "vdwScaleFactor").Split(new[] {','});
        nonbonding.vScales[0] = float.Parse(vScaleStrings[0]);
        nonbonding.vScales[1] = float.Parse(vScaleStrings[1]);
        nonbonding.vScales[2] = float.Parse(vScaleStrings[2]);
        nonbonding.vScales[3] = float.Parse(vScaleStrings[3]);
        
        //Read the Coulomb Type and Cutoff
        nonbonding.coulombType = Constants.CoulombTypeStringMap[FileIO.ParseXMLString(nonBondingX, "coulombType")];
        nonbonding.cCutoff = FileIO.ParseXMLInt(nonBondingX, "coulombCutoff");
        
        //Read the Coulomb Scale Factors
        string[] cScaleStrings = FileIO.ParseXMLString(nonBondingX, "coulombScaleFactor").Split(new[] {','});
        nonbonding.cScales[0] = float.Parse(cScaleStrings[0]);
        nonbonding.cScales[1] = float.Parse(cScaleStrings[1]);
        nonbonding.cScales[2] = float.Parse(cScaleStrings[2]);
        nonbonding.cScales[3] = float.Parse(cScaleStrings[3]);
    }

    /// <summary>Reads a Stretches XElement and populates a Parameters object from it.</summary>
    /// <param name="parameters">The Parameters object to populate.</param>
    /// <param name="stretchesX">The Stretches XElement to read.</param>
    private static void ReadStretches(Parameters parameters, XElement stretchesX) {
        foreach (XElement stretchX in stretchesX.Elements("stretch")) {
            //Atom 0 AMBER Type
            string t0 = FileIO.ParseXMLAttrString(stretchX, "t0");
            //Atom 1 AMBER Type
            string t1 = FileIO.ParseXMLAttrString(stretchX, "t1");

            //Equilibrium distance
            float req = FileIO.ParseXMLAttrFloat(stretchX, "req");
            //Force Constant
            float keq = FileIO.ParseXMLAttrFloat(stretchX, "keq");

            parameters.AddStretch(new Stretch(t0, t1, req, keq));
        }
    }

    private static void ReadBends(Parameters parameters, XElement bendsX) {
        foreach (XElement bendX in bendsX.Elements("bend")) {
            //Atom 0 AMBER Type
            string t0 = FileIO.ParseXMLAttrString(bendX, "t0");
            //Atom 1 AMBER Type
            string t1 = FileIO.ParseXMLAttrString(bendX, "t1");
            //Atom 2 AMBER Type
            string t2 = FileIO.ParseXMLAttrString(bendX, "t2");

            //Equilibrium Angle
            float aeq = FileIO.ParseXMLAttrFloat(bendX, "aeq");
            //Force Constant
            float keq = FileIO.ParseXMLAttrFloat(bendX, "keq");

            parameters.AddBend(new Bend(t0, t1, t2, aeq, keq));
        }
    }

    private static void ReadTorsions(Parameters parameters, XElement torsionsX) {
        foreach (XElement torsionX in torsionsX.Elements("torsion")) {
            //Atom 0 AMBER Type
            string t0 = FileIO.ParseXMLAttrString(torsionX, "t0");
            //Atom 1 AMBER Type
            string t1 = FileIO.ParseXMLAttrString(torsionX, "t1");
            //Atom 2 AMBER Type
            string t2 = FileIO.ParseXMLAttrString(torsionX, "t2");
            //Atom 3 AMBER Type
            string t3 = FileIO.ParseXMLAttrString(torsionX, "t3");
            
            //The number of dihedrals centred on this bond
            int nPaths = FileIO.ParseXMLAttrInt(torsionX, "nPaths");
            
            Torsion torsion = new Torsion(t0, t1, t2, t3, new float[4], new float[4], nPaths);
            foreach (XElement termX in torsionX.Elements("term")) {

                //Periodicity of the term
                int period = FileIO.ParseXMLAttrInt(termX, "period") - 1;
                //Phase Offset
                torsion.phaseOffsets[period] = FileIO.ParseXMLAttrFloat(termX, "gamma");
                //Barrier Height
                torsion.barrierHeights[period] = FileIO.ParseXMLAttrFloat(termX, "barrier");
            }
            parameters.AddTorsion(torsion);
        }
    }

    private static void ReadImproperTorsions(Parameters parameters, XElement improperTorsionsX) {
        foreach (XElement improperTorsionX in improperTorsionsX.Elements("improperTorsion")) {
            //Atom 0 AMBER Type
            string t0 = FileIO.ParseXMLAttrString(improperTorsionX, "t0");
            //Atom 1 AMBER Type
            string t1 = FileIO.ParseXMLAttrString(improperTorsionX, "t1");
            //Atom 2 AMBER Type
            string t2 = FileIO.ParseXMLAttrString(improperTorsionX, "t2");
            //Atom 3 AMBER Type
            string t3 = FileIO.ParseXMLAttrString(improperTorsionX, "t3");
            
            //Periodicity of the torsion
            int period = FileIO.ParseXMLAttrInt(improperTorsionX, "period");
            //Phase Offset
            float phaseOffset = FileIO.ParseXMLAttrFloat(improperTorsionX, "gamma");
            //Barrier Height
            float barrierHeight = FileIO.ParseXMLAttrFloat(improperTorsionX, "barrier");
            
            parameters.AddImproperTorsion(new ImproperTorsion(t0, t1, t2, t3, barrierHeight, phaseOffset, period));
        }
    }

    private static void ThrowError(XElement element, string path, string methodName, System.Exception error) {
        CustomLogger.LogFormat(
            EL.ERROR,
            "Failed to read {0}. Line: {1} (Failed on {2})",
            path,
            ((IXmlLineInfo)element).LineNumber,
            methodName
        );
        CustomLogger.LogOutput(
            "Failed to read {0}. Line: {1} (Failed on {2}){4} Trace:{4}{3}",
            path,
            ((IXmlLineInfo)element).LineNumber,
            methodName,
            error.ToString(),
            FileIO.newLine
        );
    }

    private static void ThrowError(XElement element, string path, string methodName) {
        CustomLogger.LogFormat(
            EL.ERROR,
            "Failed to read {0}. Line: {1} (Failed on {2})",
            path,
            ((IXmlLineInfo)element).LineNumber,
            methodName
        );
        CustomLogger.LogOutput(
            "Failed to read {0}. Line: {1} (Failed on {2})",
            path,
            ((IXmlLineInfo)element).LineNumber,
            methodName
        );
    }

}
