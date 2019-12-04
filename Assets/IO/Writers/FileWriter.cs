using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public static class FileWriter {

    public static IEnumerator WriteFile(Geometry geometry, string path, bool writeConnectivity) {
        string filetype = Path.GetExtension(path);

        switch (filetype) {
            case ".xat":
                return XATWriter.WriteXATFile(geometry, path, writeConnectivity);
            case ".pdb":
                return PDBWriter.WritePDBFile(geometry, path, writeConnectivity);
            case ".p2n":
                return P2NWriter.WriteP2NFile(geometry, path, writeConnectivity);
            case ".gjf":
            case ".com":
                return GaussianInputWriter.WriteGaussianInput(geometry, path, writeConnectivity);
            case ".mol2":
                return MOL2Writer.WriteMol2File(geometry, path, writeConnectivity);
            default:
                throw new System.Exception(string.Format("Filetype {0} not recognised", filetype));
        }
    }
}
