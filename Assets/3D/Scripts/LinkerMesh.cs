using Unity.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;

public class LinkerMesh : MonoBehaviour {
    //This is the representation of a bond that joins two residues

    private Mesh mesh;
    private MeshFilter meshFilter;

    float[] radii;
    Color[] atomColours;
    Atom[] atoms;

    PDBID[] pdbIDs = new PDBID[2];
    float3 offset;
    Residue[] residues = new Residue[2];

    bool primaryResidue;
    float alphaMultiplier;
    float radiusMultiplier;

    void Awake() {
        meshFilter = GetComponent<MeshFilter>();
        mesh = meshFilter.mesh;
    }

    public void SetLinker(Residue residue0, Residue residue1, PDBID pdbID0, PDBID pdbID1, Vector3 offset, bool primaryResidue) {
        
        this.pdbIDs[0] = pdbID0;
        this.pdbIDs[1] = pdbID1;
        this.residues[0] = residue0;
        this.residues[1] = residue1;

        this.offset = offset;
        this.primaryResidue = primaryResidue;

        GetAtomsInfo();
        SetMesh();
    }

    private void GetAtomsInfo() {
        atoms = new Atom[2];
        radii = new float[2];

        for (int i=0; i<2; i++) {
            radii[i] = Settings.GetAtomRadiusFromElement(pdbIDs[i].element) * radiusMultiplier;
            
            Atom atom = residues[i].GetAtom(pdbIDs[i]);
            atoms[i] = atom;
        }

        int pow = CustomMathematics.IntPow(2, Settings.stickResolution + 2);
        int numVertices = 2 * pow;

        atomColours = new Color[2] {
            Settings.GetAtomColourFromElement(pdbIDs[0].element) * alphaMultiplier,
            Settings.GetAtomColourFromElement(pdbIDs[1].element) * alphaMultiplier
        };
    }

    private void SetMesh() {
        Cylinder.main.SetMesh (
            mesh,
            atoms.Select(x => x.position + offset).ToArray(),
            radii,
            new int2[] {new int2(0,1)},
            atomColours
        );
    }

    public void Refresh() {
        alphaMultiplier = primaryResidue ? 1f : Settings.secondaryResidueAlphaMultiplier;
        radiusMultiplier = (primaryResidue ? 1f : Settings.secondaryResidueRadiusMultiplier);

        GetAtomsInfo();
        SetMesh();
    }

}
