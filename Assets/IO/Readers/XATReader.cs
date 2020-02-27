using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml.Linq;
using System.IO;
using Unity.Mathematics;
using EL = Constants.ErrorLevel;
using BT = Constants.BondType;
using Amber = Constants.Amber;

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
        
        try {
            xDocument = XDocument.Parse(text);
        } catch (System.Xml.XmlException e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to parse '{0}': {1}",
                path,
                e
            );
            yield break;
        }

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
                FileIO.ThrowXMLError(atomX, path, "ReadAtom", e);
            }
        }

        //Add the Residue to the Geometry object
        geometry.AddResidue(residueID, residue);
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
        string amberString = FileIO.ParseXMLAttrString(atomX, "amber", "");
        
        //Read Partial Charge
        float partialCharge = FileIO.ParseXMLAttrFloat(atomX, "charge", 0f);
        
        Amber amber = string.IsNullOrWhiteSpace(amberString) ? Amber.X : AmberCalculator.GetAmber(amberString);
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
                FileIO.ThrowXMLError(atomX, path, "ReadConnection", e);
                continue;
            }

            //Deconstruct Connection
            (AtomID atomID, BT bondType) = connection;

            if (atomID.IsEmpty()) {
                FileIO.ThrowXMLError(atomX, path, "ReadConnection");
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
        ReadAtomicParameters(parameters, parametersX.Element("atomicParameters"));
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

    /// <summary>Reads an Atomic Parameters XElement and populates a Parameters object from it.</summary>
    /// <param name="parameters">The Parameters object to populate.</param>
    /// <param name="atomicParametersX">The Atomic Parameters XElement to read.</param>
    private static void ReadAtomicParameters(Parameters parameters, XElement atomicParametersX) {
        if (atomicParametersX == null) {
            return;
        }
        foreach (XElement atomicParameterX in atomicParametersX.Elements("atomicParameter")) {
            Amber type = AmberCalculator.GetAmber(FileIO.ParseXMLAttrString(atomicParameterX, "type"));

            //Well Depth
            float depth = FileIO.ParseXMLAttrFloat(atomicParameterX, "depth", 0f);
            //Atomic Radius
            float radius = FileIO.ParseXMLAttrFloat(atomicParameterX, "radius", 0f);
            //Atomic Mass
            float mass = FileIO.ParseXMLAttrFloat(atomicParameterX, "mass", 0f);

            parameters.AddAtomicParameter(new AtomicParameter(type, radius:radius, wellDepth:depth, mass:mass));
        }
    }

    /// <summary>Reads a Stretches XElement and populates a Parameters object from it.</summary>
    /// <param name="parameters">The Parameters object to populate.</param>
    /// <param name="stretchesX">The Stretches XElement to read.</param>
    private static void ReadStretches(Parameters parameters, XElement stretchesX) {
        if (stretchesX == null) {
            return;
        }
        foreach (XElement stretchX in stretchesX.Elements("stretch")) {
            Amber2 types;
            if (!TryGetAmber2(stretchX, out types)) {
                continue;
            }

            //Equilibrium distance
            float req = FileIO.ParseXMLAttrFloat(stretchX, "req");
            //Force Constant
            float keq = FileIO.ParseXMLAttrFloat(stretchX, "keq");

            Stretch stretch= new Stretch(types, req, keq);
            parameters.AddStretch(stretch);
        }
    }

    private static void ReadBends(Parameters parameters, XElement bendsX) {
        if (bendsX == null) {
            return;
        }
        foreach (XElement bendX in bendsX.Elements("bend")) {
            Amber3 types;
            if (!TryGetAmber3(bendX, out types)) {
                continue;
            }

            //Equilibrium Angle
            float aeq = FileIO.ParseXMLAttrFloat(bendX, "aeq");
            //Force Constant
            float keq = FileIO.ParseXMLAttrFloat(bendX, "keq");

            Bend bend = new Bend(types, aeq, keq);
            parameters.AddBend(bend);
        }
    }

    private static void ReadTorsions(Parameters parameters, XElement torsionsX) {
        if (torsionsX == null) {
            return;
        }
        foreach (XElement torsionX in torsionsX.Elements("torsion")) {
            Amber4 types;
            if (!TryGetAmber4(torsionX, out types)) {
                continue;
            }
            
            //The number of dihedrals centred on this bond
            int nPaths = FileIO.ParseXMLAttrInt(torsionX, "nPaths");
            
            Torsion torsion = new Torsion(types, new float[4], new float[4], nPaths);

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
        if (improperTorsionsX == null) {
            return;
        }
        foreach (XElement improperTorsionX in improperTorsionsX.Elements("improperTorsion")) {
            Amber4 types;
            if (!TryGetAmber4(improperTorsionX, out types)) {
                continue;
            }
            
            //Periodicity of the torsion
            int period = FileIO.ParseXMLAttrInt(improperTorsionX, "period");
            //Phase Offset
            float phaseOffset = FileIO.ParseXMLAttrFloat(improperTorsionX, "gamma");
            //Barrier Height
            float barrierHeight = FileIO.ParseXMLAttrFloat(improperTorsionX, "barrier");
            
            ImproperTorsion improperTorsion = new ImproperTorsion(types, barrierHeight, phaseOffset, period);
            parameters.AddImproperTorsion(improperTorsion);
        }
    }

    private static Amber[] GetTypes(XElement typesX) {
        string typesString = FileIO.ParseXMLAttrString(typesX, "types", "");
        if (!string.IsNullOrWhiteSpace(typesString)) {
            return AmberCalculator.GetAmbers(typesString);
        }
        
        string t0String = FileIO.ParseXMLAttrString(typesX, "t0", "");
        if (string.IsNullOrWhiteSpace(t0String)) {
            return null;
        }
        Amber t0 = AmberCalculator.GetAmber(t0String);

        string t1String = FileIO.ParseXMLAttrString(typesX, "t1", "");
        if (string.IsNullOrWhiteSpace(t1String)) {
            return new Amber[1] {t0};
        }
        Amber t1 = AmberCalculator.GetAmber(t1String);

        string t2String = FileIO.ParseXMLAttrString(typesX, "t2", "");
        if (string.IsNullOrWhiteSpace(t2String)) {
            return new Amber[2] {t0, t1};
        }
        Amber t2 = AmberCalculator.GetAmber(t2String);

        string t3String = FileIO.ParseXMLAttrString(typesX, "t3", "");
        if (string.IsNullOrWhiteSpace(t3String)) {
            return new Amber[3] {t0, t1, t2};
        }
        Amber t3 = AmberCalculator.GetAmber(t3String);
        return new Amber[4] {t0, t1, t2, t3};

    }

    private static bool TryParseAMBER(XElement typesX, string key, out Amber amber) {
        amber = Amber.X;
        string typeString = FileIO.ParseXMLAttrString(typesX, key, "");
        if (string.IsNullOrWhiteSpace(typeString)) {
            return false;
        }
        amber = AmberCalculator.GetAmber(typeString);
        return true;
    }

    private static bool TryGetAmber1(XElement typesX, out Amber1 types1) {
        string typesString = FileIO.ParseXMLAttrString(typesX, "types", "");
        if (!string.IsNullOrWhiteSpace(typesString)) {
            types1 = new Amber1(typesString);
            return true;
        }
        types1 = new Amber1();

        Amber amber0;
        if (!TryParseAMBER(typesX, "t0", out amber0)) {return false;}

        types1 = new Amber1(amber0);
        return true;
    }

    private static bool TryGetAmber2(XElement typesX, out Amber2 types2) {
        string typesString = FileIO.ParseXMLAttrString(typesX, "types", "");
        if (!string.IsNullOrWhiteSpace(typesString)) {
            types2 = new Amber2(typesString);
            return true;
        }
        types2 = new Amber2();

        Amber amber0;
        if (!TryParseAMBER(typesX, "t0", out amber0)) {return false;}
        Amber amber1;
        if (!TryParseAMBER(typesX, "t1", out amber1)) {return false;}

        types2 = new Amber2(amber0, amber1);
        return true;
    }

    private static bool TryGetAmber3(XElement typesX, out Amber3 types3) {
        string typesString = FileIO.ParseXMLAttrString(typesX, "types", "");
        if (!string.IsNullOrWhiteSpace(typesString)) {
            types3 = new Amber3(typesString);
            return true;
        }
        types3 = new Amber3();

        Amber amber0;
        if (!TryParseAMBER(typesX, "t0", out amber0)) {return false;}
        Amber amber1;
        if (!TryParseAMBER(typesX, "t1", out amber1)) {return false;}
        Amber amber2;
        if (!TryParseAMBER(typesX, "t2", out amber2)) {return false;}

        types3 = new Amber3(amber0, amber1, amber2);
        return true;
    }

    private static bool TryGetAmber4(XElement typesX, out Amber4 types4) {
        string typesString = FileIO.ParseXMLAttrString(typesX, "types", "");
        if (!string.IsNullOrWhiteSpace(typesString)) {
            types4 = new Amber4(typesString);
            return true;
        }
        types4 = new Amber4();

        Amber amber0;
        if (!TryParseAMBER(typesX, "t0", out amber0)) {return false;}
        Amber amber1;
        if (!TryParseAMBER(typesX, "t1", out amber1)) {return false;}
        Amber amber2;
        if (!TryParseAMBER(typesX, "t2", out amber2)) {return false;}
        Amber amber3;
        if (!TryParseAMBER(typesX, "t3", out amber3)) {return false;}

        types4 = new Amber4(amber0, amber1, amber2, amber3);
        return true;
    }

}
