using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using System.Linq;
using System.Text;
using System.IO;
using OL = Constants.OniomLayerID;
using BT = Constants.BondType;

public static class GaussianInputWriter {

    private static bool writeParameters;
    public static IEnumerator WriteGaussianInput(Geometry geometry, string path, bool writeConnectivity=true) {
        
        
        using (StreamWriter streamWriter = new StreamWriter(path)) {

            GaussianCalculator gc = geometry.gaussianCalculator;
            //Get reverse sorted list
            List<OL> oniomLayers = gc.layerDict.Keys.OrderBy(x => -(int)x).ToList();

            // Atom map for connectivity and links
            geometry.atomMap = new Map<AtomID, int>();
            int atomNum = 1;
            IEnumerable<ResidueID> residueIDs = geometry.residueDict.Keys.OrderBy(x => x);
            foreach (ResidueID residueID in residueIDs) {
                IEnumerable<PDBID> pdbIDs = geometry.residueDict[residueID].pdbIDs.OrderBy(x => x);
                foreach (PDBID pdbID in pdbIDs) {
                    AtomID atomID = new AtomID(residueID, pdbID);
                    geometry.atomMap[atomID] = atomNum++;
                }
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
            atomNum = 1;
            StringBuilder connectivitySB = new StringBuilder();
            foreach (ResidueID residueID in residueIDs) {
                Residue residue  = geometry.residueDict[residueID];
                IEnumerable<PDBID> pdbIDs = residue.pdbIDs.OrderBy(x => x);
                foreach (PDBID pdbID in pdbIDs) {
                    AtomID atomID = new AtomID(residueID, pdbID);
                    streamWriter.Write(GetAtomLine(geometry, residue, pdbID, geometry.atomMap));

                    //Build up connectivity string in same loop
                    if (writeConnectivity) {
                        connectivitySB.AppendFormat("{0} ", atomNum++);
                        Atom atom = residue.atoms[pdbID];
                        foreach ((AtomID neighbourID, BT bondType) in atom.EnumerateConnections()) {
                            int neighbourNum = geometry.atomMap[neighbourID];

                            if (neighbourNum > atomNum) {
                                connectivitySB.AppendFormat(
                                    "{0} {1} ", 
                                    geometry.atomMap[neighbourID], 
                                    Settings.GetBondGaussString(bondType)
                                );
                            }
                        }
                        connectivitySB.Append(FileIO.newLine);
                    }
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

    public static string GetKeywords(GaussianCalculator gc, List<OL> oniomLayers, bool writeConnectivity=true) {
        
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

    public static string GetChargeMultiplicityString(GaussianCalculator gc, List<OL> oniomLayers) {

        StringBuilder sb = new StringBuilder();
        foreach (OL oniomLayerID in oniomLayers) {
            Layer layer = gc.layerDict[oniomLayerID];
            sb.AppendFormat("{0} {1} ", layer.charge, layer.multiplicity);            
        }
        sb.Append(FileIO.newLine);
        return sb.ToString();

    }

    public static string GetMethodsItem(GaussianCalculator gc, List<OL> oniomLayers) {
        
        StringBuilder sb = new StringBuilder();

        List<string> methodStrings = oniomLayers.Select(x => gc.layerDict[x].ToMethodItem()).ToList();

        writeParameters = oniomLayers.Any(x => gc.layerDict[x].method.ToUpper() == "AMBER");
        
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

    public static string GetAtomLine(Geometry geometry, Residue residue, PDBID pdbID, Map<AtomID, int> atomMap) {
        Atom atom = residue.atoms[pdbID];
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
        OL oniomLayer = atom.oniomLayer;
        layerSB.Append(Constants.OniomLayerIDCharMap[oniomLayer]);
        foreach (AtomID neighbourID in atom.EnumerateNeighbours()) {
            Atom neighbour = geometry.GetAtom(neighbourID);
            if (neighbour.oniomLayer > oniomLayer) {
                //This is a link atom. Tell Gaussian what to replace this atom with and which is the host atom
                layerSB.AppendFormat(" H-{0} {1}", Data.GetLinkType(neighbour, neighbourID.pdbID), atomMap[neighbourID]);
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
}