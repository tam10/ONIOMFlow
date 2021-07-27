using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using System.Linq;
using System.Text;
using System.IO;
using OLID = Constants.OniomLayerID;
using BT = Constants.BondType;
using EL = Constants.ErrorLevel;

public static class GaussianInputWriter {

    private static bool writeParameters;
    public static IEnumerator WriteGaussianInput(Geometry geometry, string path, bool writeConnectivity=true) {
        
        
        using (StreamWriter streamWriter = new StreamWriter(path)) {

            GaussianCalculator gc = geometry.gaussianCalculator;
            //Get reverse sorted list
            List<OLID> oniomLayers = gc.layerDict.Keys.OrderBy(x => -(int)x).ToList();

            IEnumerable<ResidueID> residueIDs = geometry.EnumerateResidueIDs().OrderBy(x => x);
            if (geometry.atomMap == null || geometry.atomMap.Count == 0) {
                // Atom map for connectivity and links
                geometry.GenerateAtomMap();
            }

            //Link0
            streamWriter.Write(GetLink0(gc, path));
            //Keywords
            streamWriter.Write(GetKeywords(gc, oniomLayers, writeConnectivity));
            
            //Title
            streamWriter.Write(string.Format("{1}{0}{1}{1}", gc.title, FileIO.newLine));

            //Charge/Multiplicity
            streamWriter.Write(GetChargeMultiplicityString(gc, oniomLayers));
            yield return null;

            //Atoms
            StringBuilder connectivitySB = new StringBuilder();
            foreach ((AtomID atomID, int atomNum) in geometry.atomMap) {
                (ResidueID residueID, PDBID pdbID) = atomID;
                Residue residue;
                if (!geometry.TryGetResidue(residueID, out residue)) {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        $"Residue '{residueID}' not present in Geometry!"
                    );
                    continue;
                }

                Atom atom;
                if (!residue.TryGetAtom(pdbID, out atom)) {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        $"Couldn't find Atom '{pdbID}' in Residue '{residue.residueID}'!"
                    );
                    streamWriter.WriteLine($"*** ATOM '{atomID}' NOT FOUND ***");
                    connectivitySB.AppendLine($"*** ATOM '{atomID}' NOT FOUND ***");
                    continue;
                }

                streamWriter.Write(GetAtomLine(atom, geometry, residue, pdbID));

                //Build up connectivity string in same loop
                if (writeConnectivity) {
                    connectivitySB.Append(GetConnectivityString(atom, geometry, atomID, atomNum));
                }

            }
            streamWriter.Write(FileIO.newLine);

            //Add connectivity
            if (writeConnectivity) {
                streamWriter.Write(connectivitySB.ToString());
                streamWriter.Write(FileIO.newLine);
            }

            //Extra line for non-existent secondary structure
            streamWriter.Write(FileIO.newLine);

            //Add AMBER Parameters
            if (writeParameters) {
                streamWriter.Write(geometry.parameters.GetGaussianParamsStr());
                streamWriter.Write(FileIO.newLine);
            }

        }
    }

    public static string GetLink0(GaussianCalculator gc, string path) {
        StringBuilder sb = new StringBuilder();

        //Link0
        sb.AppendFormat("%nprocshared={0}{1}", gc.numProcessors, FileIO.newLine);
        sb.AppendFormat("%mem={0}MB{1}", gc.jobMemoryMB, FileIO.newLine);
        
        string checkpointPath = gc.checkpointPath;
        if (checkpointPath == "") {
            string fileExtension = Path.GetExtension(path);
            sb.AppendFormat("%chk={0}{1}", path.Replace(fileExtension, ".chk"), FileIO.newLine);
        } else {
            sb.AppendFormat("%chk={0}{1}", gc.checkpointPath, FileIO.newLine);
        }
        
        string oldCheckpointPath = gc.oldCheckpointPath;
        if (oldCheckpointPath != "") {
            sb.AppendFormat("%oldchk={0}{1}", gc.oldCheckpointPath, FileIO.newLine);
        }

        if (gc.killJobLink != "") {
            sb.AppendFormat("%kjob={0} {1}{2}", gc.killJobLink, gc.killJobAfter, FileIO.newLine);
        }

        return sb.ToString();
    }

    public static string GetKeywords(GaussianCalculator gc, List<OLID> oniomLayers, bool writeConnectivity=true) {
        
        StringBuilder sb = new StringBuilder();
        //Print Level
        sb.AppendFormat("#{0} ", Constants.GaussianPrintLevelMap[gc.gaussianPrintLevel]);
        
        //Method/s
        sb.AppendFormat("{0} ", GetMethodsItem(gc, oniomLayers));

        //Opt?
        if (gc.doOptimisation) {
            sb.AppendFormat("{0} ", GetKeywordItem("opt", gc.GetOptimisationOptions()));
        }

        //Freq?
        if (gc.doFreq) {
            sb.AppendFormat("{0} ", GetKeywordItem("freq", gc.GetFreqOptions()));
        }

        //Guess
        if (gc.guessOptions.Count != 0) {
            sb.AppendFormat("{0} ", GetKeywordItem("guess", gc.guessOptions));
        }

        //Geom
        if (writeConnectivity && !gc.geomOptions.Contains("connectivity")) {
            gc.geomOptions.Add("connectivity");
        }
        if (gc.geomOptions.Count != 0) {
            sb.AppendFormat("{0} ", GetKeywordItem("geom", gc.geomOptions));
        }

        sb.AppendFormat("{0}{1}", string.Join(" ", gc.additionalKeywords), FileIO.newLine);
        return sb.ToString();

    }

    public static string GetKeywordItem(string keyword, List<string> options=null) {
        if (options == null) return keyword;
        int optionsCount = options.Count;
        if (optionsCount == 0) return keyword;

        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("{0}=", keyword);

        if (optionsCount == 1) {
            sb.AppendFormat("{0} ", options[0]);
            return sb.ToString();
        }

        sb.Append("(");
        sb.Append(string.Join(",", options));
        sb.Append(")");
        return sb.ToString();
    }

    public static string GetChargeMultiplicityString(GaussianCalculator gc, List<OLID> oniomLayers) {

        StringBuilder sb = new StringBuilder();
        foreach (OLID oniomLayerID in oniomLayers.OrderBy(x => x)) {
            Layer layer = gc.layerDict[oniomLayerID];
            sb.AppendFormat("{0} {1} ", layer.charge, layer.multiplicity);            
        }
        sb.Append(FileIO.newLine);
        return sb.ToString();

    }

    public static string GetMethodsItem(GaussianCalculator gc, List<OLID> oniomLayers) {
        
        StringBuilder sb = new StringBuilder();

        List<string> methodStrings = oniomLayers.Select(x => gc.layerDict[x].ToMethodItem()).ToList();

        writeParameters = oniomLayers.Any(x => gc.layerDict[x].method.ToUpper().StartsWith("AMBER"));
        
        int layerCount = oniomLayers.Count;
        if (layerCount == 1) {
            sb.Append(methodStrings[0]);
        } else {
            sb.Append("ONIOM(");
            sb.Append(string.Join(":", methodStrings));
            sb.Append(")");

            List<string> oniomOptions = gc.oniomOptions;
            if (oniomOptions != null) {
                switch (oniomOptions.Count) {
                    case (0):
                        break;
                    case (1):
                        sb.AppendFormat("={0}", oniomOptions[0]);
                        break;
                    default:
                        sb.Append("(");
                        sb.Append(string.Join(",", oniomOptions));
                        sb.Append(")");
                        break;
                }
            }
        }

        return sb.ToString();
    }

    public static string GetAtomLine(Atom atom, Geometry geometry, Residue residue, PDBID pdbID) {

        string atomSpec = string.Format(
            "{0}-{1}-{2}(PDBName={3},ResName={4},ResNum={5})",
            pdbID.element,
            AmberCalculator.GetAmberString(atom.amber),
            atom.partialCharge,
            pdbID.ToString().Trim(),
            residue.residueName,
            residue.number
        );

        float3 position = atom.position;

        StringBuilder layerSB = new StringBuilder();
        OLID oniomLayer = atom.oniomLayer;
        layerSB.Append(Constants.OniomLayerIDCharMap[oniomLayer]);
        foreach (AtomID neighbourID in atom.EnumerateNeighbours()) {
            Atom neighbour = geometry.GetAtom(neighbourID);
            if (neighbour.oniomLayer > oniomLayer) {
                //This is a link atom. Tell Gaussian what to replace this atom with and which is the host atom
                layerSB.AppendFormat(" H-{0} {1}", Data.GetLinkType(neighbour, neighbourID.pdbID), geometry.atomMap[neighbourID] + 1);
            }
        }


        return string.Format(
            "{0,-50} 0 {1,20:.0000000000} {2,20:.0000000000} {3,20:.0000000000} {4}{5}",
            atomSpec,
            position.x,
            position.y,
            position.z,
            layerSB.ToString(),
            FileIO.newLine
        );
    }

    public static string GetConnectivityString(Atom atom, Geometry geometry, AtomID atomID, int atomNum) {
        string connectivityString = $"{atomNum + 1}";
        foreach ((AtomID neighbourID, BT bondType) in atom.EnumerateConnections()) {
            int neighbourNum;
            if (! geometry.atomMap.TryGetValue(neighbourID, out neighbourNum)) {
                CustomLogger.LogFormat(
                    EL.WARNING,
                    $"Couldn't find Neighbour ID '{neighbourID}' of Atom ID '{atomID}' in Atom Map"
                );
                continue;
            }
            //Use Gaussian Indexing
            neighbourNum++;

            if (neighbourNum > atomNum) {
                connectivityString += $" {neighbourNum} {Settings.GetBondGaussString(bondType)}";
            }
        }
        return connectivityString + FileIO.newLine;
    }
}