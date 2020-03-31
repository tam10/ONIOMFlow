using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml;
using System.Linq;
using BT = Constants.BondType;
using EL = Constants.ErrorLevel;

public static class XATWriter {

    static XmlWriter x;
    static XmlWriterSettings xmlWriterSettings = new XmlWriterSettings() {
        Indent = true,
        IndentChars = "\t"
    };

    public static IEnumerator WriteXATFile(Geometry geometry, string fileName, bool writeConnectivity) {

        if (geometry == null) {
            CustomLogger.LogFormat(
                EL.WARNING,
                "Cannot save .XAT file with null Geometry!"
            );
            yield break;
        }

        x = XmlWriter.Create(fileName, xmlWriterSettings);
        x.WriteStartDocument();

        List<ResidueID> residueIDs = geometry.EnumerateResidueIDs()?.ToList();

        if (residueIDs == null) {
            CustomLogger.LogFormat(
                EL.WARNING,
                "Saving .XAT file with no residues!"
            );
        } else {
            residueIDs.Sort();

            x.WriteStartElement("geometry");

            foreach(ResidueID residueID in residueIDs) {
                WriteResidue(geometry, residueID, writeConnectivity);
                if (Timer.yieldNow) {yield return null;}
            }
        }
        


        x.WriteStartElement("parameters");
        WriteParameters(geometry.parameters);
        x.WriteEndElement();

        x.WriteEndElement();
        x.WriteEndDocument();
        x.Close();
    }

    static void WriteResidue(Geometry geometry, ResidueID residueID, bool writeConnectivity) {

        Residue residue;
        if (!geometry.TryGetResidue(residueID, out residue)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Could not write Residue '{0}' - Residue not present in Geometry!",
                residueID
            );
            return;
        }

        string residueName;
        float charge;

        if (residue == null) {
            residueName = "";
            charge = 0f;
        } else {
            residueName = residue.residueName;
            charge = residue.GetCharge();
        }

        //<residue ID="ID" charge="CHARGE" state="STATE">
        x.WriteStartElement("residue");
        x.WriteAttributeString("ID", residueID.ToString());
        x.WriteAttributeString("name", residueName);
        x.WriteAttributeString("charge", string.Format("{0:0.0000}", charge));
        x.WriteAttributeString("state", Constants.ResidueStateMap[residue.state]);

		foreach (PDBID pdbID in residue.pdbIDs) {
            WriteAtom(residue, pdbID, writeConnectivity);
        }

        //</res>
        x.WriteEndElement();

    }

    static void WriteAtom(Residue residue, PDBID pdbID, bool writeConnectivity) {
        Atom atom = residue.GetAtom(pdbID);
        //<atom ID="ID" charge="CHARGE" amber="AMBER">
        x.WriteStartElement("atom");
        x.WriteAttributeString("ID", pdbID.ToString());
        x.WriteAttributeString("layer", Constants.OniomLayerIDCharMap[atom.oniomLayer].ToString());
        x.WriteAttributeString("charge", string.Format("{0:0.0000}", atom.partialCharge));
        x.WriteAttributeString("amber", AmberCalculator.GetAmberString(atom.amber));

        string position = string.Format(
            "{0:0.0000},{1:0.0000},{2:0.0000}",
            atom.position.x,
            atom.position.y,
            atom.position.z
        );
        WriteSingleElement("xyz", position);

        int connectionNum = 0;
        int numConnections = atom.internalConnections.Count + atom.externalConnections.Count;
        if (writeConnectivity && numConnections > 0) {
            x.WriteStartElement("bonds");
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach ((AtomID, BT) connection in atom.EnumerateConnections()) {
                if (connection.Item1.residueID != residue.residueID) {
                    sb.AppendFormat("[{0}]", connection.Item1.residueID);
                }
                sb.Append(connection.Item1.pdbID);
                sb.AppendFormat("({0})", Constants.BondTypeMap[connection.Item2]);
                connectionNum++;
                if (connectionNum < numConnections) {
                    sb.Append(",");
                }
            }
            x.WriteString(sb.ToString());
            x.WriteEndElement();
        }

        // <bonds>[A2] C  (S), CA (S)</bonds>

        //</atom>
        x.WriteEndElement();

    }

    static void WriteSingleElement(string name, string value) {
        x.WriteStartElement(name);
        x.WriteString(value);
        x.WriteEndElement();
    }

    static void WriteParameters(Parameters parameters) {
        WriteNonBonding(parameters.nonbonding);
        WriteAtomicParameters(parameters.atomicParameters.Values);
        WriteStretches(parameters.stretches.Values);
        WriteBends(parameters.bends.Values);
        WriteTorsions(parameters.torsions.Values);
        WriteImproperTorsions(parameters.improperTorsions.Values);
    }

    static void WriteNonBonding(NonBonding nonbonding) {
        x.WriteStartElement("nonbonding");
        
        WriteSingleElement("vdwType", Constants.VanDerWaalsTypeStringMap[nonbonding.vdwType]);
        WriteSingleElement("vdwCutoff", nonbonding.vCutoff.ToString());

        string vScaleFactors = string.Format(
            "{0:0.0000},{1:0.0000},{2:0.0000},{3:0.0000}",
            nonbonding.vScales[0],
            nonbonding.vScales[1],
            nonbonding.vScales[2],
            nonbonding.vScales[3]
        );
        WriteSingleElement("vdwScaleFactor", vScaleFactors);

        WriteSingleElement("coulombType", Constants.CoulombTypeStringMap[nonbonding.coulombType]);
        WriteSingleElement("coulombCutoff", nonbonding.cCutoff.ToString());

        string cScaleFactors = string.Format(
            "{0:0.0000},{1:0.0000},{2:0.0000},{3:0.0000}",
            nonbonding.cScales[0],
            nonbonding.cScales[1],
            nonbonding.cScales[2],
            nonbonding.cScales[3]
        );
        WriteSingleElement("coulombScaleFactor", cScaleFactors);

        x.WriteEndElement();
    }

    static void WriteAtomicParameters(IEnumerable<AtomicParameter> atomicParameters) {
        x.WriteStartElement("atomicParameters");
        foreach (AtomicParameter atomicParameter in atomicParameters) {
            x.WriteStartElement("atomicParameter");
            x.WriteAttributeString("type", AmberCalculator.GetAmberString(atomicParameter.type.amber));
            x.WriteAttributeString("depth", string.Format("{0:0.0000}", atomicParameter.wellDepth));
            x.WriteAttributeString("mass", string.Format("{0:0.0000}", atomicParameter.mass));
            x.WriteAttributeString("radius", string.Format("{0:0.0000}", atomicParameter.radius));
            x.WriteEndElement();
        }
        x.WriteEndElement();
    }

    static void WriteStretches(IEnumerable<Stretch> stretches) {
        x.WriteStartElement("stretches");
        foreach (Stretch stretch in stretches) {
            x.WriteStartElement("stretch");
            x.WriteAttributeString("types", stretch.GetTypesString());
            x.WriteAttributeString("req", stretch.req.ToString());
            x.WriteAttributeString("keq", stretch.keq.ToString());
            x.WriteEndElement();
        }
        x.WriteEndElement();
    }

    static void WriteBends(IEnumerable<Bend> bends) {
        x.WriteStartElement("bends");
        foreach (Bend bend in bends) {
            x.WriteStartElement("bend");
            x.WriteAttributeString("types", bend.GetTypesString());
            x.WriteAttributeString("aeq", bend.aeq.ToString());
            x.WriteAttributeString("keq", bend.keq.ToString());
            x.WriteEndElement();
        }
        x.WriteEndElement();
    }

    static void WriteTorsions(IEnumerable<Torsion> torsions) {
        x.WriteStartElement("torsions");
        foreach (Torsion torsion in torsions) {
            x.WriteStartElement("torsion");
            x.WriteAttributeString("types", torsion.GetTypesString());
            x.WriteAttributeString("nPaths", torsion.npaths.ToString());
            for (int term=0; term<4; term++) {
                float barrierHeight = torsion.barrierHeights[term];
                if (barrierHeight > 0f) {
                    x.WriteStartElement("term");
                    x.WriteAttributeString("period", (term+1).ToString());
                    x.WriteAttributeString("barrier", barrierHeight.ToString());
                    x.WriteAttributeString("gamma", torsion.phaseOffsets[term].ToString());
                    x.WriteEndElement();
                }
            }
            x.WriteEndElement();
        }
        x.WriteEndElement();
    }

    static void WriteImproperTorsions(IEnumerable<ImproperTorsion> improperTorsions) {
        x.WriteStartElement("improperTorsions");
        foreach (ImproperTorsion improperTorsion in improperTorsions) {
            x.WriteStartElement("improperTorsion");
            x.WriteAttributeString("types", improperTorsion.GetTypesString());
            x.WriteAttributeString("period", improperTorsion.periodicity.ToString());
            x.WriteAttributeString("barrier", improperTorsion.barrierHeight.ToString());
            x.WriteAttributeString("gamma", improperTorsion.phaseOffset.ToString());
            x.WriteEndElement();
        }
        x.WriteEndElement();
    }

}
