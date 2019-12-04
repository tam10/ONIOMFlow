using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AtomsTable : MonoBehaviour
{
    
    public RectTransform scrollRectTransform;
    public AtomsTableTitles atomsTableTitles;
    public AtomsTableInfo atomsTableInfoPrefab;
    public RectTransform DialogueTransform;
    public RectTransform TableTransform;
    public RectTransform titleGroupRect;


    public void Populate(Geometry geometry) {
        //string[] elements = atoms.elements;
        //float[,] positions = atoms.positions;
        //string[] pdbs = atoms.pdbNames;
        //string[] ambers = atoms.amberNames;
        //string[] residueNames = atoms.residueNames;
        //int[] residueNumbers = atoms.residueNumbers;
        //string[] chains = atoms.chains;

        //for (int i = 0; i < 10; i++) {
        //    AtomsTableInfo atomInfo = Instantiate<AtomsTableInfo> (atomsTableInfoPrefab, TableTransform);
        //    atomInfo.gameObject.SetActive(true);
        //    atomInfo.SetValues(
        //        i,
        //        false,
        //        elements[i],
        //        positions[i, 0],
        //        positions[i, 1],
        //        positions[i, 2],
        //        pdbs[i],
        //        ambers[i],
        //        residueNames[i],
        //        residueNumbers[i],
        //        chains[i]
        //    );
        //    atomInfo.SetWidths(atomsTableTitles);
        //}
    }

    public void Display() {
        DialogueTransform.gameObject.SetActive(true);
    }

    public void Hide() {
        DialogueTransform.gameObject.SetActive(false);
    }

    public void Update() {
        titleGroupRect.offsetMin = new Vector2(TableTransform.offsetMin.x, 0f);
    }
    
}
