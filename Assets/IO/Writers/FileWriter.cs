using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class FileWriter {
    IEnumerator writer;

    public FileWriter(Geometry geometry, string path, bool writeConnectivity) {
        string filetype = Path.GetExtension(path);

        switch (filetype) {
            case ".xat":
                writer = XATWriter.WriteXATFile(geometry, path, writeConnectivity);
                break;
            case ".pdb":
                writer = PDBWriter.WritePDBFile(geometry, path, writeConnectivity);
                break;
            case ".p2n":
                writer = new P2NWriter(geometry).WriteToFile(path, writeConnectivity);
                break;
            case ".gjf":
            case ".com":
                writer = GaussianInputWriter.WriteGaussianInput(geometry, path, writeConnectivity);
                break;
            case ".mol2":
                writer = MOL2Writer.WriteMol2File(geometry, path, writeConnectivity);
                break;
            default:
                throw new System.ArgumentException(string.Format("Filetype '{0}' not recognised", filetype));
        }
    }

    public IEnumerator WriteFile() {
        if (writer == null) {
            throw new System.NullReferenceException("Writer is not initialised!");
        }
        yield return writer;
    }
}
