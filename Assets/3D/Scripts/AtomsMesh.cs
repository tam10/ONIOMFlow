using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OLID = Constants.OniomLayerID;
using Unity.Mathematics;

public class AtomsMesh : MonoBehaviour {

    private Mesh mesh;
    private MeshFilter meshFilter;

    //Handle mouse interaction
    public delegate void PointerHandler(AtomsMesh atomMesh);
    public PointerHandler OnMouseDownHandler;
    public PointerHandler OnMouseOverHandler;
    public PointerHandler OnMouseUpHandler;
    public PointerHandler OnMouseClickHandler;
    public void Pass(AtomsMesh AtomsMesh) {}
    private Vector2 mouseDownPosition;
    public bool mouseOver;
    private bool clickEligible;
    public float clickMovementTolerance = 15f;

    private MeshCollider meshCollider;
    Ray mouseRay;
    RaycastHit hit;
    public Vector3 hitPosition;

    int numAtoms;


    float[] radii;
    Color[] elementColours;
    Color[] chargeColours;
    float3[] positions;

    float3 offset;
    public Residue residue;

    bool primaryResidue;
    float alphaMultiplier;
    float radiusMultiplier;

    void Awake() {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        mesh = meshFilter.mesh;

        OnMouseDownHandler = Pass;
        OnMouseOverHandler = Pass;
        OnMouseUpHandler = Pass;
        OnMouseClickHandler = Pass;
    }

    public void SetAtoms(Residue residue, Vector3 offset, bool primaryResidue) {
        this.primaryResidue = primaryResidue;
        this.residue = residue;
        this.offset = offset;
        Refresh();
    }

    private void GetAtomsInfo() {
        //positions = new float[numAtoms * 3];
        positions = new float3[numAtoms];
        radii = new float[numAtoms];
        elementColours = new Color[numAtoms];
        chargeColours = new Color[numAtoms];
        int positionIndex = 0;
        int atomNum = 0;
        foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {

            positions[positionIndex++] = atom.position + offset;

            float radius = Settings.GetAtomRadiusFromElement(pdbID.element)  * radiusMultiplier;
            Color colour = Settings.GetAtomColourFromElement(pdbID.element);

            radii[atomNum] = radius * (atom.oniomLayer == OLID.REAL ? 0.5f : 1f);
            colour.a *= alphaMultiplier * (atom.oniomLayer == OLID.REAL ? 0.5f : 1f);

            if (atom.oniomLayer == OLID.MODEL) {
                colour.r *= 1.5f;
                colour.g *= 1.5f;
                colour.b *= 1.5f;
            }

            elementColours[atomNum] = colour;
            chargeColours[atomNum] = Settings.GetAtomColourFromCharge(atom.partialCharge);

            atomNum++;
        }
    }

    private void GetGeometry() {
        Sphere.main.SetMesh(
			mesh, 
			positions, 
			radii,
            elementColours
        );

        meshCollider.sharedMesh = mesh;
    }

    public void SetColoursByCharge() {
        Sphere.main.SetMeshColours(
            mesh,
            numAtoms,
            chargeColours
        );
    }

    public void SetColoursByElement() {
        Sphere.main.SetMeshColours(
            mesh,
            numAtoms,
            elementColours
        );
    }

    void OnMouseDown() {
        mouseDownPosition = Input.mousePosition;
        //Add a proton if atom is clicked
        if (mouseOver && Input.GetMouseButtonDown(0)) {
            //Get where the click occured in local space
            mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(mouseRay, out hit)) {
                hitPosition = (float3)transform.InverseTransformPoint(hit.point) - offset;
            }
            
            OnMouseDownHandler(this);
            clickEligible = true;
        }
    }

    void OnMouseOver() {
        OnMouseOverHandler(this);
        if (clickEligible && Vector2.Distance(mouseDownPosition, Input.mousePosition) > clickMovementTolerance) {
            clickEligible = false;
        }
    }


    void OnMouseExit() {mouseOver = false;}
    void OnMouseEnter() {mouseOver = true;}

    void OnMouseUp() {
        OnMouseUpHandler(this);
        if (clickEligible) {OnMouseClick();}
    }

    void OnMouseClick() {
        OnMouseClickHandler(this);
    }

    public void Refresh() {
        numAtoms = residue.size;
        alphaMultiplier = primaryResidue ? 1f : Settings.secondaryResidueAlphaMultiplier;
        this.radiusMultiplier = (primaryResidue ? 1f : Settings.secondaryResidueRadiusMultiplier);

        GetAtomsInfo();
        GetGeometry();
    }

}
