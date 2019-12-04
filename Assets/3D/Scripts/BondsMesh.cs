using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Mathematics;
using Unity.Jobs;
using BT = Constants.BondType;
using OLID = Constants.OniomLayerID;
using EL = Constants.ErrorLevel;

public class BondsMesh : MonoBehaviour {

    private Mesh mesh;
    private MeshFilter meshFilter;

    int numBonds;
    int numAtoms;

    //List<int> bondPairsFortran;
    List<int2> bondPairs;
    PDBID[] pdbIDs;

    float[] radii;
    Color[] atomColours;
    
    Atom[] atoms;

    float3 offset;
    public Residue residue;

    bool primaryResidue;
    float alphaMultiplier;
    float radiusMultiplier;

    void Awake() {
        meshFilter = GetComponent<MeshFilter>();
        mesh = meshFilter.mesh;
    }

    public void SetBonds(Residue residue, Vector3 offset, bool primaryResidue) {
        this.primaryResidue = primaryResidue;
        this.residue = residue;
        this.offset = offset;
        Refresh();
    }

    private void GetBondPairs() {
        
        ResidueID residueID = residue.residueID;
        this.numBonds = 0;
        //bondPairsFortran = new List<int>();
        bondPairs = new List<int2>();
        for (int atomNum=0; atomNum<numAtoms; atomNum++) {
            PDBID pdbID = pdbIDs[atomNum];

            foreach ((PDBID neighbourID, BT bondType) in residue.atoms[pdbID].internalConnections) {

                if (pdbID > neighbourID) {

                    int bondTo = System.Array.IndexOf (pdbIDs, neighbourID);

                    if (bondTo == -1) {
                        //Raise some error

                        List<string> toPDBIDs = residue
                            .atoms[pdbID]
                            .internalConnections
                            .Select(x => x.Key.ToString())
                            .ToList();

                        CustomLogger.LogFormat(
                            EL.ERROR,
                            "Element '{0}' is connected to '{1}' but '{1}' does not have '{0}' in its neighbours ('{2}')", 
                            pdbID,
                            neighbourID, 
                            string.Join("' '", toPDBIDs)
                        );
                    } else {
                        //bondPairsFortran.Add(atomNum + 1);
                        //bondPairsFortran.Add(bondTo + 1);
                        bondPairs.Add(new int2(atomNum, bondTo));
                        this.numBonds++;
                    }
                }
            }
        }
    }

    private void GetAtomsInfo() {
        atoms = new Atom[numAtoms];
        radii = new float[numAtoms];
        for (int atomNum=0; atomNum<numAtoms; atomNum++) {
            PDBID pdbID = pdbIDs[atomNum];

            float radius = Settings.GetAtomRadiusFromElement(pdbID.element) * radiusMultiplier;

            Atom atom = residue.atoms[pdbID];

            atoms[atomNum] = atom;
            radii[atomNum] = radius * (atom.oniomLayer == OLID.REAL ? 0.5f : 1f);

        }

        atomColours = pdbIDs
            .Select(x => Settings.GetAtomColourFromElement(x.element) * alphaMultiplier)
            .ToArray();
    }
    private void SetMesh() {
        Cylinder.main.SetMesh (
            mesh,
            atoms.Select(x => x.position + offset).ToArray(),
            radii,
            bondPairs.ToArray(),
            atomColours
        );
    }

    public void Refresh() {
        pdbIDs = residue.pdbIDs.ToArray();
        numAtoms = pdbIDs.Length;
        alphaMultiplier = primaryResidue ? 1f : Settings.secondaryResidueAlphaMultiplier;
        this.radiusMultiplier = (primaryResidue ? 1f : Settings.secondaryResidueRadiusMultiplier);
        
        GetBondPairs();
        GetAtomsInfo();
        SetMesh();
    }
}
