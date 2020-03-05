using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using TMPro;
using Element = Constants.Element;
using GIID = Constants.GeometryInterfaceID;
using OLID = Constants.OniomLayerID;
using EL = Constants.ErrorLevel;
using BT = Constants.BondType;
using RS = Constants.ResidueState;
using Unity.Mathematics;
using UnityEngine.UI;

/// <summary>Atoms Visualiser Singleton Class</summary>
/// <remarks>
/// Visualises a single geometry with GL.
/// Differentiates ONIOM Layers using line width.
/// Provides a UI to Geometry modification
/// </remarks>
public class AtomsVisualiser : MonoBehaviour {
    
	/// <summary>The Atoms Visualiser Singleton instance.</summary>
    private static AtomsVisualiser _main;
	/// <summary>Gets the reference to the Atoms Visualiser Singleton.</summary>
    public static AtomsVisualiser main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<AtomsVisualiser>();
            return _main;
        }
    }

	/// <summary>An interface to the Graphics Library to draw bonds.</summary>
    LineDrawer lineDrawer;
	/// <summary>The Geometry Interface ID containing the Geometry being visualised.</summary>
    GIID geometryInterfaceID;
	/// <summary>The Geometry being visualised.</summary>
    public Geometry geometry;
	/// <summary>The List of IDs of currectly selected Atoms.</summary>
    public List<AtomID> selectedAtomIDs;
	/// <summary>The dictionary containing references to the Spheres that highlight currently selected atoms.</summary>
    private Dictionary<AtomID, GameObject> selectedAtomSpheres;

	/// <summary>The reference to the Draggable Interface allowing user interaction with the visualiser.</summary>
    public DraggableInterface draggableInterface;
	/// <summary>The reference to the Transform containing Meshes.</summary>
    public Transform meshHolder;
	/// <summary>The reference to the Transform containing the lineDrawer.</summary>
    public Transform atomsRepresentation;
	/// <summary>The initial position of the Atoms Representation relative to the Atoms Visualiser.</summary>
    public Vector3 atomsRepresentationStartPosition = new Vector3(0, 0, 15);
	/// <summary>The Transform containing the Cube that forms the background of the Atoms Visualiser.</summary>
    public Transform cubeTransform;
    public GameObject dialogue;

    public GameObject trail0Prefab;
    public GameObject trail1Prefab;
    public GameObject spherePrefab;
    public GameObject particlesPrefab;
    public GameObject glowParticle;

    Texture3D texture3D;
    public Material orbitalMaterial;
    public RawImage imagePrefab;
    public Transform cameraOrbitalTransform;
    public Vector3 gridScale;

    float rotationSensitivity = 0.1f;
    float translationSensitivity = 0.02f;
    float zoomSensitivity = 0.5f;

    float3 offset = new float3();


	//INPUT HANDLING

	/// <value>Was the pointer pressed down on this AtomsVisualiser's DraggableInterface?</value>
	private bool pointerDown;
	/// <value>Is this AtomsVisualiser's DraggableInterface being dragged?</value>
	private bool isBeingDragged;

	/// <value>Does this AtomsVisualiser's DraggableInterface implement dragging behaviour?</value>
    private bool draggable;
	/// <value>How long does the pointer need to be down before considering it dragged?</value>
    public static float timeUntilDragged = 0.1f;
	
	/// <value>Where was the cursor when OnPointerDown was called?</value>
	float3 pointerDownPosition;

    public TextMeshProUGUI title;
    
    AtomID closestAtomID;
    Atom closestAtom;
    AtomID[] chainAtomIDs = new AtomID[4];
    Atom[] chainAtoms = new Atom[4];
    private bool closestAtomEnumeratorRunning = false;
    private bool tooltipEnumeratorRunning = false;


    bool cmdMod;
    bool ctrlMod;
    bool shiftMod;

    enum EditMode : int { 
        ROTATEXY, ROTATEZ, TRANSLATE, 
        CONNECTIVITY, OPTIMISE, 
        BOND, ANGLE, DIHEDRAL,
        REMOVE_RESIDUE, CAP_RESIDUE, MUTATE_RESIDUE 
    };
    private EditMode selectedEditMode = EditMode.ROTATEXY;
    private EditMode finalEditMode = EditMode.ROTATEXY;

    public GeometryHistory geometryHistory;
    //public Transform historyTransform;
    int maxHistory=20;
    //public int historyStep;


    void Awake() { 

        GameObject historyGO = new GameObject("History");
        historyGO.transform.SetParent(transform);
        geometryHistory = historyGO.AddComponent<GeometryHistory>();
        
        if (lineDrawer == null) {
            lineDrawer = PrefabManager.InstantiateLineDrawer(atomsRepresentation);
        }

        title.text = "Geometry Visualiser - (Edit Transform)";

        //if (label == null) {
        //    label = PrefabManager.InstantiateTextBox3D(lineDrawer.transform);
        //    label.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        //    label.text.text = "";
        //    label.text.fontSize = 1f;
        //}

        ResetPointerHandler();
    }

    public IEnumerator Redraw() {
        
        ClearSelectedAtoms();
        Clear();

        foreach ((ResidueID residueID, Residue residue) in geometry.EnumerateResidues()) {
            lineDrawer.AddResidue(residue, -offset);

            foreach ((AtomID hostID, AtomID linkerID, Atom hostAtom, Atom linkerAtom) in residue.NeighbouringAtoms()) {
                if (hostID.residueID > linkerAtom.residueID) {
                    lineDrawer.AddLinker(hostAtom, linkerAtom, hostID, linkerID, -offset);
                }
            }
            if (Timer.yieldNow) {yield return null;}
        }

    }

    public IEnumerator VisualiseGeometry(GIID geometryInterfaceID) {
        this.geometryInterfaceID = geometryInterfaceID;
        geometry = Flow.GetGeometry(geometryInterfaceID);

        geometryHistory.Initialise(geometry, maxHistory);
        //ResetHistory();
        Clear();
        Display();
        
        Bounds bounds = geometry.GetBounds();

        offset = bounds.centre;

        float radius = bounds.GetRadius();

        if (math.isnan(radius)) {
            CustomLogger.LogFormat(
                EL.WARNING,
                "Radius of Atoms {0} is invalid",
                geometry
            );
            meshHolder.localScale = cubeTransform.localScale;
            lineDrawer.transform.localScale = cubeTransform.localScale;
        } else {
            Vector3 scale = cubeTransform.localScale / (2 * radius);
            meshHolder.localScale = scale;
            lineDrawer.transform.localScale = scale;
        }


        foreach ((ResidueID residueID, Residue residue) in geometry.EnumerateResidues()) {
            lineDrawer.AddResidue(residue, -offset);

            foreach ((AtomID hostID, AtomID linkerID, Atom hostAtom, Atom linkerAtom) in residue.NeighbouringAtoms()) {
                if (hostID.residueID > linkerAtom.residueID) {
                    lineDrawer.AddLinker(
                        hostAtom, 
                        linkerAtom, 
                        hostID, 
                        linkerID, 
                        -offset
                    );
                }
            }
            if (Timer.yieldNow) {yield return null;}
        }

        //TEMP
        foreach (List<float3> bezier in MissingResidueTools.main.beziers) {

            int bezierCount = bezier.Count;
            
            if (bezierCount > 0) {
                GameObject bezierHolder = new GameObject("Bezier");
                bezierHolder.transform.SetParent(meshHolder);
                bezierHolder.transform.localPosition = Vector3.zero;
                bezierHolder.transform.localRotation = Quaternion.identity;
                bezierHolder.transform.localScale = Vector3.one;

                LineRenderer lineRenderer = bezierHolder.AddComponent<LineRenderer>();

                lineRenderer.positionCount = bezierCount;
                lineRenderer.useWorldSpace = false;
                lineRenderer.startWidth = 0.04f;
                lineRenderer.endWidth = 0.04f;
                lineRenderer.textureMode = LineTextureMode.DistributePerSegment;
                lineRenderer.material = ColorScheme.GetLineGlowOSCMaterial();
                lineRenderer.startColor = Color.red;
                lineRenderer.endColor = Color.blue;
                lineRenderer.SetPositions(
                    bezier.Select(x => (Vector3)(x - offset)).ToArray()
                );

            }
        }

        MakeInteractive(draggableInterface);

    }

    LineDrawer.AtomColour colourMode;

    void SetColourMode(LineDrawer.AtomColour colourMode) {
        this.colourMode = colourMode;
        lineDrawer.SetColours(colourMode);
    }

    void SetResidueColourMode(LineDrawer.AtomColour colourMode, ResidueID residueID) {
        lineDrawer.SetResidueColour(colourMode, residueID);
    }

    void ResetResidueColourMode(ResidueID residueID) {
        lineDrawer.SetResidueColour(colourMode, residueID);
    }

    IEnumerator ShowMolecularOrbital() {
        Geometry tempGeo = geometry.Take(transform);
        tempGeo.atomMap = geometry.atomMap.ToMap(x => x.Key, x => x.Value);
        CubeReader cubeReader = new CubeReader(tempGeo);

        yield return geometry.gaussianCalculator.CubeGen(cubeReader);
        
        Color positiveColour = Color.red;
        Color noColour = Color.clear;
        Color negativeColour = Color.blue;
        Color[] colourArray = new Color[cubeReader.gridLength];

        texture3D = new Texture3D ( 
            cubeReader.dimensions[0], 
            cubeReader.dimensions[1], 
            cubeReader.dimensions[2], 
            TextureFormat.RGBA32, 
            true
        );

        gridScale = cubeReader.gridScale;

        for (int gridNum = 0; gridNum < cubeReader.gridLength; gridNum++) {
            //float value = grid[gridNum] / norm;
            float value = cubeReader.grid[gridNum];
        
            Color finalColour;
            if (value > 0) {
                finalColour = positiveColour;
                finalColour.a = value;
                //finalColour = Color.Lerp(noColour, positiveColour, value);
            } else {
                finalColour = negativeColour;
                finalColour.a = - value;
                //finalColour = Color.Lerp(noColour, negativeColour, -value);
            }
            colourArray[gridNum] = finalColour;
        }

        texture3D.SetPixels (colourArray);
        texture3D.Apply ();

        orbitalMaterial.SetTexture("_Tex3D", texture3D);


        float startZ = 500;
        float endZ = -100;
        float zStep = 50;


        for (float z = startZ; z >= endZ; z -= zStep) {
            RawImage imageClone = GameObject.Instantiate<RawImage>(imagePrefab, cameraOrbitalTransform);
            imageClone.material = orbitalMaterial;
            imageClone.enabled = true;
            imageClone.transform.localPosition = new Vector3(0, 0, z);
            imageClone.color = Color.Lerp(
                imageClone.color,
                Color.clear,
                CustomMathematics.Map(
                    z,
                    startZ,
                    endZ,
                    0,
                    1
                )
            );
        }


    }

    public void Clear() {
        lineDrawer.Clear();
        foreach (Transform child in meshHolder) {
            GameObject.Destroy(child.gameObject);
        }

        if (main.pointer != null) {
            GameObject.Destroy(main.pointer.gameObject);
        }
    }

    public void Display() {
        dialogue.gameObject.SetActive(true);
        atomsRepresentation.gameObject.SetActive(true);
        atomsRepresentation.transform.localPosition = atomsRepresentationStartPosition;
        Flow.main.GetComponent<Canvas>().enabled = false;

        selectedAtomIDs = new List<AtomID>();
        selectedAtomSpheres = new Dictionary<AtomID, GameObject>();

        mainCamera = Camera.main;
        initialFieldOfView = mainCamera.fieldOfView;
        initialCameraPosition = mainCamera.transform.position; 
        initialCameraRotation = mainCamera.transform.rotation;

        colourMode = LineDrawer.AtomColour.ELEMENT;

        RotationCube rotationCube = Camera.main.GetComponentInChildren<RotationCube>();
        if (rotationCube != null) {
            rotationCube.LinkTransform(atomsRepresentation);
        }

    } 

    public void Hide() {
        dialogue.gameObject.SetActive(false);
        atomsRepresentation.gameObject.SetActive(false);
        lineDrawer.Clear();
        Clear();
        Flow.main.GetComponent<Canvas>().enabled = true;
        moving = false;
        this.enabled = false;

        foreach (Transform child in cameraOrbitalTransform) {
            GameObject.Destroy(child.gameObject);
        }

        main.mainCamera.transform.rotation = main.initialCameraRotation;
        main.mainCamera.transform.position = main.initialCameraPosition;
        main.mainCamera.fieldOfView = main.initialFieldOfView;
        ClearSelectedAtoms();
        
        RotationCube rotationCube = Camera.main.GetComponentInChildren<RotationCube>();
        if (rotationCube != null) {
            rotationCube.UnlinkTransform();
        }

    }

    public void MakeInteractive(DraggableInterface draggableInterface) {
        this.draggableInterface = draggableInterface;
        draggable = true;

        draggableInterface.OnPointerDownHandler = OnPointerDown;
        draggableInterface.OnPointerUpHandler = OnPointerUp;
        draggableInterface.OnScrollHandler = OnScroll;
    }

    void OnPointerDown(PointerEventData pointerEventData) {
        pointerDown = true;
        isBeingDragged = false;

		pointerDownPosition = Input.mousePosition;

		if (pointerEventData.button == PointerEventData.InputButton.Left) {
            StartCoroutine(DraggingTest());
        }
    }

    void OnPointerUp(PointerEventData pointerEventData) {
        pointerDown = false;
        if (!isBeingDragged) {
            OnPointerClick(pointerEventData);
        }
        isBeingDragged = false;
    }

    private IEnumerator DraggingTest() {
        float timer = 0f;
        while (pointerDown && timer < timeUntilDragged) {
            timer += Time.deltaTime;
            yield return null;
        }
        isBeingDragged = true;
		if (draggable) {
            if (finalEditMode == EditMode.OPTIMISE && chainAtomIDs[0].IsEmpty()) {
                StartCoroutine(BrushOptimise());
            } else {
                StartCoroutine(EditTransform());
            }
		}
    }

    delegate void PointerClickHandler();
    PointerClickHandler pointerClickHandler = () => {};

    delegate void AtomEnterHandler(AtomID atomID);
    AtomEnterHandler atomEnterHandler = (AtomID atomID) => {};

    delegate void AtomExitHandler(AtomID atomID);
    AtomExitHandler atomExitHandler = (AtomID atomID) => {};

    void ResetPointerHandler() {
        pointerClickHandler = () => {
            switch (finalEditMode) {
                case EditMode.BOND:
                    StartCoroutine(ModifyAtomChain(
                        2, 
                        Color.white, 
                        Color.blue, 
                        Color.blue, 
                        ModifyBond()
                    ));
                    break;
                case EditMode.ANGLE:
                    StartCoroutine(ModifyAtomChain(
                        3, 
                        Color.white, 
                        Color.blue, 
                        Color.blue, 
                        ModifyAngle()
                    ));
                    break;
                case EditMode.DIHEDRAL:
                    StartCoroutine(ModifyAtomChain(
                        4, 
                        Color.white, 
                        Color.blue, 
                        Color.blue, 
                        ModifyDihedral()
                    ));
                    break;
                case EditMode.CONNECTIVITY:
                    StartCoroutine(ModifyAtomChain(
                        2, 
                        Color.white, 
                        Color.red, 
                        Color.green, 
                        ToggleConnection()
                    ));
                    break;
                case EditMode.REMOVE_RESIDUE:
                    Residue removeResidue;

                    if (closestAtom != null && geometry.TryGetResidue(closestAtom.residueID, out removeResidue)) {
                        RemoveResidue(closestAtomID.residueID);
                    }
                    break;
                case EditMode.MUTATE_RESIDUE:
                    Residue mutateResidue;

                    if (closestAtom != null && geometry.TryGetResidue(closestAtom.residueID, out mutateResidue)) {
                        MutateResidue(closestAtomID.residueID);
                    }
                    break;
                case EditMode.CAP_RESIDUE:
                    Residue capResidue;

                    if (closestAtom != null && geometry.TryGetResidue(closestAtom.residueID, out capResidue)) {
                        CapResidue(closestAtomID);
                    }
                    break;
            }
        };
    }

	public void OnPointerClick(PointerEventData pointerEventData) {
		if (pointerEventData.button == PointerEventData.InputButton.Left) {
            pointerClickHandler();
		} else if (pointerEventData.button == PointerEventData.InputButton.Right) {
			ShowContextMenu();
		}
	}

    //////////
    // KEYS //
    //////////

    void SetSelectedEditMode(EditMode newEditMode) {
        selectedEditMode = newEditMode;
        SetMod(shiftMod, ctrlMod, cmdMod);
    }

    void SetMod(bool shiftMod, bool ctrlMod, bool cmdMod) {
        if (
            shiftMod == this.shiftMod && 
            ctrlMod == this.ctrlMod &&
            cmdMod == this.cmdMod && 
            selectedEditMode == finalEditMode
        ) {
            return;
        }

        this.shiftMod = shiftMod;
        this.ctrlMod = ctrlMod;
        this.cmdMod = cmdMod;
        
        EditMode newEditMode;
        if (shiftMod) {
            newEditMode = ctrlMod ? EditMode.CONNECTIVITY : EditMode.TRANSLATE;
        } else {
            newEditMode = ctrlMod ? EditMode.ROTATEZ : selectedEditMode;
        }

        if (newEditMode != finalEditMode) {
            SetFinalEditMode(newEditMode);
        }
        
    }

    void SetFinalEditMode(EditMode finalEditMode) {
        this.finalEditMode = finalEditMode;
        switch (finalEditMode) {
            case (EditMode.ROTATEXY):
                title.text = "Geometry Visualiser - (Rotate XY)";
                atomEnterHandler = (closestAtomID) => ShowToolTip(closestAtomID);
                break;
            case (EditMode.ROTATEZ):
                title.text = "Geometry Visualiser - (Rotate Z)";
                atomEnterHandler = (closestAtomID) => ShowToolTip(closestAtomID);
                break;
            case (EditMode.TRANSLATE):
                title.text = "Geometry Visualiser - (Translate)";
                atomEnterHandler = (closestAtomID) => ShowToolTip(closestAtomID);
                break;
            case (EditMode.CONNECTIVITY):
                title.text = "Geometry Visualiser - (Edit Connectivity)";
                atomEnterHandler = (closestAtomID) => ShowToolTip(closestAtomID);
                break;
            case (EditMode.OPTIMISE):
                title.text = "Geometry Visualiser - (Local Optimisation)";
                atomEnterHandler = (closestAtomID) => ShowToolTip(closestAtomID);
                break;
            case (EditMode.BOND):
                title.text = "Geometry Visualiser - (Edit Distances)";
                atomEnterHandler = (closestAtomID) => ShowToolTip(closestAtomID);
                break;
            case (EditMode.ANGLE):
                title.text = "Geometry Visualiser - (Edit Angles)";
                atomEnterHandler = (closestAtomID) => ShowToolTip(closestAtomID);
                break;
            case (EditMode.DIHEDRAL):
                title.text = "Geometry Visualiser - (Edit Dihedrals)";
                atomEnterHandler = (closestAtomID) => ShowToolTip(closestAtomID);
                break;
            case (EditMode.REMOVE_RESIDUE):
                title.text = "Geometry Visualiser - (Remove Residue)";
                atomEnterHandler = (closestAtomID) => {
                    if (closestAtom != null) {
                        SetResidueColourMode(LineDrawer.AtomColour.REMOVE, closestAtomID.residueID);
                        atomExitHandler = (emptyID) => {
                            ResetResidueColourMode(closestAtomID.residueID);
                        };
                    }
                };
                break;
            case (EditMode.CAP_RESIDUE):
                title.text = "Geometry Visualiser - (Cap Residue)";
                atomEnterHandler = (closestAtomID) => {
                    if (closestAtom != null) {
                        SetResidueColourMode(LineDrawer.AtomColour.CAP, closestAtomID.residueID);
                        atomExitHandler = (emptyID) => {
                            ResetResidueColourMode(closestAtomID.residueID);
                        };
                    }
                };
                break;
            case (EditMode.MUTATE_RESIDUE):
                title.text = "Geometry Visualiser - (Mutate Residue)";
                atomEnterHandler = (closestAtomID) => {
                    if (closestAtom != null) {
                        SetResidueColourMode(LineDrawer.AtomColour.MUTATE, closestAtomID.residueID);
                        atomExitHandler = (emptyID) => {
                            ResetResidueColourMode(closestAtomID.residueID);
                        };
                    }
                };
                break;
            default:
                title.text = "Geometry Visualiser";
                break;
        }
        ResetPointerHandler();
    }

    //////////////////
    // CONTEXT MENU //
    //////////////////

	/// <summary>Open the Context Menu (right clicked).</summary>
	public void ShowContextMenu() {
		ContextMenu contextMenu = ContextMenu.main;

		//Clear the Context Menu
		contextMenu.Clear();

		bool geometryEnabled = (geometry != null);

        IEnumerator CalculateConnectivity() {
            yield return Cleaner.CalculateConnectivity(geometryInterfaceID);
            yield return Redraw();
            geometryHistory.SaveState("Calculate Connectivity");
        }

        void HideContextMenu() {
            contextMenu.Hide();
        }

		//Add buttons and spacers
        
		contextMenu.AddButton(
            () => {StartCoroutine(Redraw());}, 
            "Refresh", 
            true
        );

		contextMenu.AddButton(
            () => {StartCoroutine(CalculateConnectivity());}, 
            "Compute Connectivity", 
            geometryEnabled
        );

        bool orbitalsAvailable = (geometry.gaussianCalculator != null && geometry.gaussianCalculator.orbitalsAvailable);

		contextMenu.AddSpacer();

        ContextButtonGroup editGroup = contextMenu.AddButtonGroup("Edit Mode", true);
        editGroup.AddButton(() => {SetSelectedEditMode(EditMode.ROTATEXY); HideContextMenu();}, "Transform", true);
        editGroup.AddButton(() => {SetSelectedEditMode(EditMode.CONNECTIVITY); HideContextMenu();}, "Connectivity", true);
        editGroup.AddSpacer();
        editGroup.AddButton(() => {SetSelectedEditMode(EditMode.BOND); HideContextMenu();}, "Distance", true);
        editGroup.AddButton(() => {SetSelectedEditMode(EditMode.ANGLE); HideContextMenu();}, "Angle", true);
        editGroup.AddButton(() => {SetSelectedEditMode(EditMode.DIHEDRAL); HideContextMenu();}, "Dihedral", true);
        editGroup.AddSpacer();
        editGroup.AddButton(() => {SetSelectedEditMode(EditMode.REMOVE_RESIDUE); HideContextMenu();}, "Remove Residue", true);
        editGroup.AddButton(() => {SetSelectedEditMode(EditMode.CAP_RESIDUE); HideContextMenu();}, "Cap Residue", true);
        editGroup.AddButton(() => {SetSelectedEditMode(EditMode.MUTATE_RESIDUE); HideContextMenu();}, "Mutate Residue", true);

		contextMenu.AddSpacer();
        
        ContextButtonGroup colourGroup = contextMenu.AddButtonGroup("Colour Mode", true);
        colourGroup.AddButton(() => {SetColourMode(LineDrawer.AtomColour.ELEMENT); HideContextMenu();}, "Element", true);
        colourGroup.AddButton(() => {SetColourMode(LineDrawer.AtomColour.CHARGE); HideContextMenu();}, "Charge", true);
        colourGroup.AddButton(() => {SetColourMode(LineDrawer.AtomColour.HAS_AMBER); HideContextMenu();}, "Has AMBER", true);
        colourGroup.AddButton(() => {SetColourMode(LineDrawer.AtomColour.PARAMETERS); HideContextMenu();}, "Parameter Penalty", true);

		contextMenu.AddSpacer();

        contextMenu.AddButton(() => {StartCoroutine(ShowMolecularOrbital()); HideContextMenu();}, "Show Molecular Orbital", orbitalsAvailable);

		contextMenu.AddSpacer();

        ContextButtonGroup layerButtonGroup = contextMenu.AddButtonGroup("Add Selection to Layer", true);
		layerButtonGroup.AddButton(() => {AddSelectionToLayer(OLID.REAL); HideContextMenu();}, "Real", true);
		layerButtonGroup.AddButton(() => {AddSelectionToLayer(OLID.INTERMEDIATE); HideContextMenu();}, "Intermediate", true);
		layerButtonGroup.AddButton(() => {AddSelectionToLayer(OLID.MODEL); HideContextMenu();}, "Model", true);
        layerButtonGroup.AddButton(() => {ClearSelectedAtoms(); HideContextMenu();}, "Clear Selection", true);

        contextMenu.AddSpacer();

		contextMenu.AddButton(() => {Hide(); HideContextMenu();}, "Exit", true);
		

		//Show the Context Menu
		contextMenu.Show();
	}

    bool CheckChainAtoms(int numToCheck) {
        if (numToCheck < 0 || numToCheck > 4) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Can only check 0 to 4 atoms in AtomsVisualiser Atoms Chain"
            );
            return false;
        }

        for (int i=0; i<numToCheck; i++) {
            if (
                chainAtomIDs[i].IsEmpty() ||
                chainAtoms[i] == null
            ) {
                return false;
            }
            for (int j=i+1; j<numToCheck; j++) {
                if (chainAtomIDs[j] == chainAtomIDs[i]) {
                    return false;
                }
            }
        }

        return true;
    }

    void AddSelectionToLayer(OLID oniomLayerID) {
        foreach (AtomID atomID in selectedAtomIDs) {
            Atom atom;
            if (geometry.TryGetAtom(atomID, out atom)) {
                atom.oniomLayer = oniomLayerID;
            }
        }
        StartCoroutine(Redraw());
        geometryHistory.SaveState(string.Format("Add selection to {0}", oniomLayerID));
    }

    void ClearSelectedAtoms() {
        Color finalColor = new Color(0, 0, 0, 0);
        foreach (GameObject sphere in selectedAtomSpheres.Values) {
            if (sphere == null) {
                continue;
            }
            AtomCollider atomCollider = sphere.GetComponent<AtomCollider>();
            if (atomCollider == null) {
                continue;
            }

            StartCoroutine(atomCollider.FadeAndDestroy(finalColor, 0.5f));
        }
        selectedAtomSpheres.Clear();
        selectedAtomIDs.Clear();
    }

    IEnumerator GetClosestAtomID(Vector3 screenPosition) {

        closestAtomEnumeratorRunning = true;
        yield return null;
        
        AtomID bestAtomID = AtomID.Empty;
        float bestDistanceSq = 10f;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        float3 origin = lineDrawer.transform.InverseTransformPoint(ray.origin);
        float3 direction = lineDrawer.transform.InverseTransformDirection(ray.direction);
        

        foreach ((AtomID atomID, Atom atom) in geometry.EnumerateAtomIDPairs()) {

            float3 vector = atom.position - offset - origin;

            //Check atom is at least 2 units of distance in front of camera
            float dot = math.dot(vector, direction);
            if (dot > 2f) {

                float3 projection = dot * direction;
                float3 residual = projection - vector;

                float distanceSq = math.lengthsq(residual);

                if (
                    distanceSq < bestDistanceSq 
                    &&
                    distanceSq < Settings.GetAtomRadiusFromElement(atomID.pdbID.element).Squared()
                ) {
                    bestDistanceSq = distanceSq;
                    bestAtomID = atomID;
                }
            }
            
        }

        atomExitHandler(bestAtomID);
        if (bestAtomID.IsEmpty()) {
            closestAtom = null;
        } else {
            geometry.TryGetAtom(bestAtomID, out closestAtom);
            atomEnterHandler(bestAtomID);
        }

        this.closestAtomID = bestAtomID;
        closestID = closestAtomID.ToString();
        closestAtomEnumeratorRunning = false;
        
    }

    void ShowToolTip(AtomID atomID) {
        if (tooltipEnumeratorRunning) {
            return;
        }
        Atom atom;
        if (geometry.TryGetAtom(atomID, out atom)) {
            StartCoroutine(ShowToolTip(atomID, atom));
        }
    }

    IEnumerator ShowToolTip(AtomID hoveredAtomID, Atom hoveredAtom) {

        tooltipEnumeratorRunning = true;
        if (hoveredAtomID.IsEmpty() || hoveredAtom == null) {
            tooltipEnumeratorRunning = false;
            yield break;
        }


        float timer = 1f;
        float time = 0f;
        while (time < timer) {

            if (closestAtomID != hoveredAtomID || ! dialogue.gameObject.activeSelf) {
                tooltipEnumeratorRunning = false;
                yield break;
            }

            yield return null;
            time += Time.deltaTime;
        }

        
        GameObject sphere = Instantiate<GameObject>(spherePrefab, meshHolder);
        sphere.transform.localPosition = hoveredAtom.position - offset;


        string title = string.Format("Residue: {0}. PDB: {1}", hoveredAtomID.residueID, hoveredAtomID.pdbID);
        string description;
        Color sphereColour;

        switch (colourMode) {
            case LineDrawer.AtomColour.CHARGE:
                description = string.Format("Partial Charge: {0,7:F4}", hoveredAtom.partialCharge);
                sphereColour = Settings.GetAtomColourFromCharge(hoveredAtom.partialCharge);
                break;
            case LineDrawer.AtomColour.HAS_AMBER:
                description = string.Format("AMBER: {0}", AmberCalculator.GetAmberString(hoveredAtom.amber));
                sphereColour = Settings.GetAtomColourFromAMBER(hoveredAtom.amber);
                break;
            case LineDrawer.AtomColour.PARAMETERS:
                description = geometry.parameters.GetAtomPenaltyString(hoveredAtomID);
                sphereColour = Settings.GetAtomColourFromPenalty(hoveredAtom.penalty);
                break;
            case LineDrawer.AtomColour.ELEMENT:
            default:
                description = string.Format("Element: {0}", hoveredAtomID.pdbID.element);
                sphereColour = Settings.GetAtomColourFromElement(hoveredAtomID.pdbID.element);
                break;
        }

        Tooltip tooltip = Tooltip.main;
        if (tooltip != null) {
            tooltip.Show(title, description, 0f);
        }

        AtomCollider collider = sphere.GetComponent<AtomCollider>();
        if (collider != null) {
            sphereColour.a = 0.5f;
            collider.SetColor(sphereColour);
        }

        while (closestAtomID == hoveredAtomID && dialogue.gameObject.activeSelf) {
            yield return null;
        }

        if (collider != null) {
            StartCoroutine(collider.FadeAndDestroy(new Color(1f, 1f, 1f, 0f), 1f));
        }
        
        if (tooltip != null) {
            tooltip.Hide();
        }

        tooltipEnumeratorRunning = false;
    }

    IEnumerator ModifyAtomChain(int numAtomsToSelect, Color unsnappedColour, Color connectedColour, Color disconnectedColour, IEnumerator action) {
        
        if (numAtomsToSelect < 2) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Must select at least 2 atoms in ModifyAtomChain"
            );
            yield break;
        }
        
        int numLines = numAtomsToSelect - 1;
        GameObject[] lineGOs = new GameObject[numLines];

        void CleanUp() {
            for (int lineNum=0; lineNum<numLines; lineNum++) {
                GameObject lineGO = lineGOs[lineNum];
                if ((lineGO) != null) {
                    GameObject.Destroy(lineGO);
                }
            }
            for (int atomIndex=0; atomIndex<numAtomsToSelect; atomIndex++) {
                chainAtomIDs[atomIndex] = AtomID.Empty;
                chainAtoms[atomIndex] = null;
            }
            ResetPointerHandler();
        }


        if ((chainAtomIDs[0] = closestAtomID) == AtomID.Empty) {
            CleanUp();
            yield break;
        }

        if (!geometry.TryGetAtom(chainAtomIDs[0], out chainAtoms[0]) || chainAtoms[0] == null) {
            CleanUp();
            yield break;
        }

        chainAtomIDs[1] = AtomID.Empty;

        float unsnappedWidth = Settings.layerLineThicknesses[OLID.REAL] * lineDrawer.transform.lossyScale.x * 0.1f;

        float3 GetSnappedPosition(LineRenderer line, int chainIndex) {

            if (chainAtoms[chainIndex].IsConnectedTo(chainAtomIDs[chainIndex+1])) {
                line.startColor = connectedColour;
                line.endColor = connectedColour;
            } else {
                line.startColor = disconnectedColour;
                line.endColor = disconnectedColour;
            }

            if (chainAtoms[chainIndex+1] != null) {
                return chainAtoms[chainIndex+1].position - offset;
            } else {
                return float3.zero;
            }
        }

        float3 GetUnsnappedPosition(LineRenderer line, int chainIndex) {
            line.startColor = unsnappedColour;
            line.endColor = unsnappedColour;
            
            Vector3 pos = Input.mousePosition;
            pos.z = math.length(
                lineDrawer.transform.TransformPoint(chainAtoms[chainIndex].position - offset)
                - Camera.main.transform.position
            );

            return lineDrawer.transform.InverseTransformPoint(Camera.main.ScreenToWorldPoint(pos));
        }

        bool clicked = false;
        pointerClickHandler = () => {clicked = true;};

        for (int lineNum=0, atomNum=1; lineNum < numLines; lineNum++, atomNum++) {

            if (chainAtoms[lineNum] == null) {
                CleanUp();
                yield break;
            }
            
            Vector3 startPosition = chainAtoms[lineNum].position - offset;
            Vector3 endPosition = lineDrawer.transform.InverseTransformPoint(Camera.main.ScreenToWorldPoint(Input.mousePosition));

            GameObject lineGO = lineGOs[lineNum] = DrawLine(
                startPosition,
                endPosition,
                ColorScheme.GetLineGlowMaterial(),
                unsnappedColour,
                unsnappedColour,
                unsnappedWidth,
                unsnappedWidth,
                lineDrawer.transform
            );

            LineRenderer line = lineGO.GetComponent<LineRenderer>();

            while (!clicked) {
                if (Input.GetKey(KeyCode.Escape)) {
                    CleanUp();
                    yield break;
                }

                chainAtomIDs[atomNum] = closestAtomID;
                chainAtoms[atomNum] = closestAtom;
                
                if (CheckChainAtoms(atomNum+1)) {
 
                    float snappedWith = math.min(
                        Settings.layerLineThicknesses[chainAtoms[lineNum].oniomLayer],
                        Settings.layerLineThicknesses[closestAtom.oniomLayer]
                    ) * lineDrawer.transform.lossyScale.x * 0.1f;

                    line.startWidth = snappedWith;
                    line.endWidth = snappedWith;
                    endPosition = GetSnappedPosition(line, lineNum);

                } else {
                    line.startWidth = unsnappedWidth;
                    line.endWidth = unsnappedWidth;
                    endPosition = GetUnsnappedPosition(line, lineNum);
                }

                line.SetPosition(1, endPosition);

                if (Timer.yieldNow) {
                    yield return null;
                }

            }
            clicked = false;

        }

        for (int lineNum=0; lineNum<numLines; lineNum++) {
            GameObject lineGO = lineGOs[lineNum];
            if ((lineGO) != null) {
                GameObject.Destroy(lineGO);
            }
        }
        yield return action;
        CleanUp();

    }

    float modifyPivot = 0.5f;
    enum ConnectionCycle : int {UNLINKED, SPRING, LINKED};
    ConnectionCycle startConnectionCycle = ConnectionCycle.UNLINKED;
    ConnectionCycle endConnectionCycle = ConnectionCycle.UNLINKED;

    IEnumerator PivotDragged(
        RectTransform startHandleRect, 
        RectTransform endHandleRect, 
        OverlayButton topPivotHandle,
        OverlayButton bottomPivotHandle
    ) {
        OverlayButton draggedPivot = (topPivotHandle.isBeingDragged) ? topPivotHandle : bottomPivotHandle;
        Vector2 screenBondVector = endHandleRect.position - startHandleRect.position;

        topPivotHandle.button.image.color = Color.green;
        bottomPivotHandle.button.image.color = Color.green;

        while (draggedPivot.isBeingDragged) {
            Vector2 pivotVector = Input.mousePosition - startHandleRect.position;
            modifyPivot = math.dot(screenBondVector, pivotVector) / math.dot(screenBondVector, screenBondVector);
            modifyPivot = Mathf.Clamp(modifyPivot, 0f, 1f);
            yield return null;
        }

        topPivotHandle.button.image.color = Color.white;
        bottomPivotHandle.button.image.color = Color.white;
    }

    void TranslateConnectedAtoms(
        ConnectionCycle connectionCycle, 
        List<(Atom, AtomID, int)> atomConnections, 
        Dictionary<AtomID, float3> startPositions, 
        float3 translation, 
        int maxDepth
    ) {
        if (connectionCycle == ConnectionCycle.LINKED) {
            foreach ((Atom atom, AtomID atomID, int depth) in atomConnections) {
                atom.position = startPositions[atomID] + translation;
                lineDrawer.UpdatePosition(atomID, atom.position - offset);
            }
        } else if (connectionCycle == ConnectionCycle.SPRING) {
            foreach ((Atom atom, AtomID atomID, int depth) in atomConnections) {
                if (depth > maxDepth) {
                    break;
                } else if (depth == 0f) {
                    continue;
                }
                atom.position = startPositions[atomID] + translation / (depth + 1);
                lineDrawer.UpdatePosition(atomID, atom.position - offset);
            }
        }
    }

    void RotateConnectedAtoms(
        ConnectionCycle connectionCycle, 
        List<(Atom, AtomID, int)> atomConnections, 
        Dictionary<AtomID, float3> startPositions, 
        Quaternion rotation, 
        float3 origin,
        int maxDepth
    ) {
        
        if (connectionCycle == ConnectionCycle.LINKED) {
            foreach ((Atom atom, AtomID atomID, int depth) in atomConnections) {
                if (depth == 0f) {
                    continue; 
                }
                atom.position = (float3)(rotation * (atom.position - origin)) + origin;
                lineDrawer.UpdatePosition(atomID, atom.position - offset);
            }
        } else if (connectionCycle == ConnectionCycle.SPRING) {
            foreach ((Atom atom, AtomID atomID, int depth) in atomConnections) {
                if (depth > maxDepth) {
                    break;
                } else if (depth == 0f) {
                    continue;
                }
                Quaternion slerped = Quaternion.Slerp(rotation, Quaternion.identity, 1f / (depth + 1f));
                atom.position = (float3)(slerped * (atom.position - origin)) + origin;
                lineDrawer.UpdatePosition(atomID, atom.position - offset);
            }
        }
    }

    Sprite GetConnectionCycleSprite(ConnectionCycle connectionCycle) {
        switch (connectionCycle) {
            case (ConnectionCycle.UNLINKED):
                return SpriteManager.unlinkedIcon;
            case (ConnectionCycle.SPRING):
                return SpriteManager.springIcon;
            case (ConnectionCycle.LINKED):
                return SpriteManager.linkedIcon;
            default:
                return SpriteManager.whiteCircle;
        }
    }

    IEnumerator BondHandleDragged(
        RectTransform atom0HandleRect, 
        RectTransform atom1HandleRect,
        OverlayButton atom0Handle, 
        OverlayButton atom1Handle,
        List<(Atom, AtomID, int)> atom0Connections,
        List<(Atom, AtomID, int)> atom1Connections
    ) {

        OverlayButton draggedHandle; 
        Vector2 screenBondVector; //This is the bond vector as seen by the Camera (pixel position on screen)
        if (atom0Handle.isBeingDragged) {
            draggedHandle = atom0Handle;
            screenBondVector = atom1HandleRect.position - atom0HandleRect.position;
        }  else {
            draggedHandle = atom1Handle;
            screenBondVector = atom0HandleRect.position - atom1HandleRect.position;
        }

        Vector2 originalPosition = draggedHandle.button.GetComponent<RectTransform>().position;

        //Get the atom properties from the chain
        AtomID atomID0 = chainAtomIDs[0];
        AtomID atomID1 = chainAtomIDs[1];
        Atom atom0 = chainAtoms[0];
        Atom atom1 = chainAtoms[1];
        float3 p0 = atom0.position;
        float3 p1 = atom1.position;
        float3 v01 = p1 - p0; //Bond vector in geometry space

        //Change the handle colour
        draggedHandle.button.image.color = Color.green;

        //This is the furthest graph distance of atoms that are moved in spring mode 
        int maxDepth = 4;

        Dictionary<AtomID, float3> startConnectionsInitialPositions = new Dictionary<AtomID, float3>();
        if (startConnectionCycle == ConnectionCycle.LINKED || startConnectionCycle == ConnectionCycle.SPRING) {
            startConnectionsInitialPositions = atom0Connections.ToDictionary(x => x.Item2, x => x.Item1.position);
        }
        
        Dictionary<AtomID, float3> atom1NeighbourStartPositions = new Dictionary<AtomID, float3>();
        if (endConnectionCycle == ConnectionCycle.LINKED || endConnectionCycle == ConnectionCycle.SPRING) {
            atom1NeighbourStartPositions = atom1Connections.ToDictionary(x => x.Item2, x => x.Item1.position);
        }

        while (draggedHandle.isBeingDragged) {

            float3 modifyVector = (
                v01 *
                math.dot(screenBondVector, (Vector2)Input.mousePosition - originalPosition) /
                (math.dot(screenBondVector, screenBondVector)) * 2f
            );

            float3 atom0ModifyVector = modifyVector * modifyPivot;
            float3 atom1ModifyVector = modifyVector * (modifyPivot - 1f);

            //Move the selected atoms
            atom0.position = p0 + atom0ModifyVector;
            atom1.position = p1 + atom1ModifyVector;

            //Update the lineDrawer with the new positions
            lineDrawer.UpdatePosition(atomID0, atom0.position - offset);
            lineDrawer.UpdatePosition(atomID1, atom1.position - offset);

            TranslateConnectedAtoms(startConnectionCycle, atom0Connections, startConnectionsInitialPositions, atom0ModifyVector, maxDepth);
            TranslateConnectedAtoms(endConnectionCycle  , atom1Connections, atom1NeighbourStartPositions, atom1ModifyVector, maxDepth);

            yield return null;
        }
        
        draggedHandle.button.image.color = Color.white;

    }

    IEnumerator AngleHandleDragged(
        RectTransform startHandleRect, 
        RectTransform endHandleRect,
        OverlayButton startHandle, 
        OverlayButton endHandle,
        List<(Atom, AtomID, int)> startConnections,
        List<(Atom, AtomID, int)> endConnections
    ) {
        OverlayButton draggedHandle; 
        RectTransform draggedRect;
        Atom draggedAtom;
        
        if (startHandle.isBeingDragged) {
            draggedHandle = startHandle;
            draggedRect = startHandleRect;
            draggedAtom = chainAtoms[0];
        }  else {
            draggedHandle = endHandle;
            draggedRect = endHandleRect;
            draggedAtom = chainAtoms[2];
        }
        
        //Get the atom properties from the chain
        AtomID startID  = chainAtomIDs[0];
        AtomID centreID = chainAtomIDs[1];
        AtomID endID    = chainAtomIDs[2];
        Atom startAtom  = chainAtoms[0];
        Atom centreAtom = chainAtoms[1];
        Atom endAtom    = chainAtoms[2];
        float3 p0 = startAtom.position;
        float3 p1 = centreAtom.position;
        float3 p2 = endAtom.position;
        float3 v10 = p0 - p1; //Bond vector from atom1 to atom0 in geometry space
        float3 v12 = p2 - p1; //Bond vector from atom1 to atom2 in geometry space

        float3 normVec;
        if (!CustomMathematics.GetVectorNorm(v10, v12, out normVec)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Unable to define angle between atoms '{0}'-'{1}'-'{2}'",
                startID,
                centreID,
                endID
            );
            yield break;
        }

        draggedHandle.button.image.color = Color.green;

        int maxDepth = 4;

        Dictionary<AtomID, float3> startConnectionsInitialPositions = new Dictionary<AtomID, float3>();
        if (startConnectionCycle == ConnectionCycle.LINKED || startConnectionCycle == ConnectionCycle.SPRING) {
            startConnectionsInitialPositions = startConnections.ToDictionary(x => x.Item2, x => x.Item1.position);
        }
        
        Dictionary<AtomID, float3> endConnectionsInitialPositions = new Dictionary<AtomID, float3>();
        if (endConnectionCycle == ConnectionCycle.LINKED || endConnectionCycle == ConnectionCycle.SPRING) {
            endConnectionsInitialPositions = endConnections.ToDictionary(x => x.Item2, x => x.Item1.position);
        }
        
        float r10 = math.length(v10);
        float3 v10n = v10 / r10;
        float3 cross = math.cross(v10n, normVec);
        Arc arc1 = new Arc(
            0f, 
            math.PI * 2, 
            math.PI / 32f, 
            r10, 
            v10n, 
            cross,
            centreAtom.position - offset, 
            (startHandle.isBeingDragged) ? Color.green : Color.white
        );
        lineDrawer.arcs.Add(arc1);
        
        float r12 = math.length(v12);
        Arc arc2 = new Arc(
            0f, 
            math.PI * 2, 
            math.PI / 32f, 
            r12, 
            v10n, 
            cross,
            centreAtom.position - offset, 
            (endHandle.isBeingDragged) ? Color.green : Color.white
        );
        lineDrawer.arcs.Add(arc2);

        while (draggedHandle.isBeingDragged) {

            float3 v1dn = math.normalize(draggedAtom.position - p1);

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            float3 origin = lineDrawer.transform.InverseTransformPoint(ray.origin);
            float3 direction = lineDrawer.transform.InverseTransformDirection(ray.direction);

            float3 v1m = p1 - offset - origin;
            float3 projection = math.dot(v1m, direction) * direction;
            float3 residual = projection - v1m;
            float3 v1mn = math.normalize(residual);

            float angle;
            if (startHandle.isBeingDragged) {
                angle = math.degrees(CustomMathematics.SignedAngleRad(v1dn, v1mn, normVec));
            } else {
                angle = - math.degrees(CustomMathematics.SignedAngleRad(v1dn, v1mn, normVec));
            }

            Quaternion R0 = Quaternion.AngleAxis(angle * modifyPivot, normVec);
            Quaternion R2 = Quaternion.AngleAxis(angle * (modifyPivot - 1f), normVec);

            startAtom.position = (float3)(R0 * (startAtom.position - centreAtom.position)) + centreAtom.position;
            endAtom.position   = (float3)(R2 * (endAtom.position   - centreAtom.position)) + centreAtom.position;

            lineDrawer.UpdatePosition(startID, startAtom.position - offset);
            lineDrawer.UpdatePosition(endID  , endAtom.position   - offset);

            RotateConnectedAtoms(startConnectionCycle, startConnections, startConnectionsInitialPositions, R0, centreAtom.position, maxDepth);
            RotateConnectedAtoms(endConnectionCycle  , endConnections  , endConnectionsInitialPositions, R2, centreAtom.position, maxDepth);

            yield return null;
        }

        lineDrawer.arcs.Remove(arc1);
        lineDrawer.arcs.Remove(arc2);
        
        draggedHandle.button.image.color = Color.white;


        yield break;
    }

    IEnumerator DihedralHandleDragged(
        RectTransform startHandleRect, 
        RectTransform endHandleRect,
        OverlayButton startHandle, 
        OverlayButton endHandle,
        List<(Atom, AtomID, int)> startConnections,
        List<(Atom, AtomID, int)> endConnections
    ) {

        OverlayButton draggedHandle; 
        RectTransform draggedRect;
        Atom draggedAtom;
        
        if (startHandle.isBeingDragged) {
            draggedHandle = startHandle;
            draggedRect = startHandleRect;
            draggedAtom = chainAtoms[0];
        }  else {
            draggedHandle = endHandle;
            draggedRect = endHandleRect;
            draggedAtom = chainAtoms[3];
        }
        
        //Get the atom properties from the chain
        AtomID startID = chainAtomIDs[0];
        AtomID atomID1 = chainAtomIDs[1];
        AtomID atomID2 = chainAtomIDs[2];
        AtomID endID   = chainAtomIDs[3];
        Atom startAtom = chainAtoms[0];
        Atom atom1     = chainAtoms[1];
        Atom atom2     = chainAtoms[2];
        Atom endAtom   = chainAtoms[3];
        float3 centre  = (atom1.position + atom2.position) * 0.5f;

        float3 v01 = atom1.position - startAtom.position;
        float3 v21 = atom1.position - atom2.position;
        float3 v32 = atom2.position - endAtom.position;

        float3 v01n = math.normalize(v01);
        float3 v21n = math.normalize(v21);
        float3 v32n = math.normalize(v32);
        
		float3 w01 = v01n - v21n * math.dot(v01n, v21n);
		float3 w32 = v32n - v21n * math.dot(v32n, v21n);

        float dihedral = CustomMathematics.SignedAngleRad(w01, w32, v21n);

        draggedHandle.button.image.color = Color.green;

        int maxDepth = 4;

        Dictionary<AtomID, float3> startConnectionsInitialPositions = new Dictionary<AtomID, float3>();
        if (startConnectionCycle == ConnectionCycle.LINKED || startConnectionCycle == ConnectionCycle.SPRING) {
            startConnectionsInitialPositions = startConnections.ToDictionary(x => x.Item2, x => x.Item1.position);
        }
        
        Dictionary<AtomID, float3> endConnectionsInitialPositions = new Dictionary<AtomID, float3>();
        if (endConnectionCycle == ConnectionCycle.LINKED || endConnectionCycle == ConnectionCycle.SPRING) {
            endConnectionsInitialPositions = endConnections.ToDictionary(x => x.Item2, x => x.Item1.position);
        }
        
        float3 pj1  = v21n * math.dot(v01, v21n);
        float3 res1 = v01 - pj1;
        Arc arc1 = new Arc(
            0f, 
            math.PI * 2, 
            math.PI / 32f, 
            1f, 
            res1, 
            math.cross(res1, v21n),
            atom1.position - pj1 - offset, 
            (startHandle.isBeingDragged) ? Color.green : Color.white
        );
        lineDrawer.arcs.Add(arc1);
        
        float3 pj2  = v21n * math.dot(v32, v21n);
        float3 res2 = v32 - pj2;
        float rres2 = math.length(res2);
        Arc arc2 = new Arc(
            0f, 
            math.PI * 2, 
            math.PI / 32f, 
            1f, 
            res2, 
            math.cross(res2, v21n), 
            atom2.position - pj2 - offset, 
            (endHandle.isBeingDragged) ? Color.green : Color.white
        );
        lineDrawer.arcs.Add(arc2);

        Tooltip tooltip = Tooltip.main;

        if (tooltip != null) {
            tooltip.Show("Dihedral", "0000.00", 0f);
        }
        
        while (draggedHandle.isBeingDragged) {

            v01n = math.normalize(atom1.position - startAtom.position);
            v32n = math.normalize(atom2.position - endAtom.position);

            w01 = v01n - v21n * math.dot(v01n, v21n);
            w32 = v32n - v21n * math.dot(v32n, v21n);

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            float3 origin = lineDrawer.transform.InverseTransformPoint(ray.origin);
            float3 direction = lineDrawer.transform.InverseTransformDirection(ray.direction);

            float3 vcm = math.normalize(centre - offset - origin);

            float3 projection = math.dot(vcm, direction) * direction;
            float3 residual = projection - vcm;
            float3 v1mn = -math.normalize(residual);

            float angle;
            if (startHandle.isBeingDragged) {
                angle = + math.degrees(CustomMathematics.SignedAngleRad(w01, v1mn, v21n));
            } else {
                angle = - math.degrees(CustomMathematics.SignedAngleRad(w32, v1mn, v21n));
            }
            
            if (tooltip != null) {
                tooltip.infoText.text = string.Format("{0,7:F2}", angle);
            }

            this.w01 = w01;
            this.w32 = w32;
            this.angle = angle;
            this.vcm = vcm;
            this.projection = projection;
            this.residual = residual;
            this.v1mn = v1mn;
            
            Quaternion R0 = Quaternion.AngleAxis(angle * modifyPivot, v21n);
            Quaternion R3 = Quaternion.AngleAxis(angle * (modifyPivot - 1f), v21n);

            startAtom.position = (float3)(R0 * (startAtom.position - centre)) + centre;
            endAtom.position   = (float3)(R3 * (endAtom.position   - centre)) + centre;

            lineDrawer.UpdatePosition(startID, startAtom.position - offset);
            lineDrawer.UpdatePosition(endID  , endAtom.position   - offset);

            RotateConnectedAtoms(startConnectionCycle, startConnections, startConnectionsInitialPositions, R0, centre, maxDepth);
            RotateConnectedAtoms(endConnectionCycle  , endConnections  , endConnectionsInitialPositions, R3, centre, maxDepth);

            //yield return new WaitForSeconds(0.5f);

            yield return null;
        }
        
        if (tooltip != null) {
            tooltip.Hide();
        }
        
        draggedHandle.button.image.color = Color.white;

        lineDrawer.arcs.Remove(arc1);
        lineDrawer.arcs.Remove(arc2);
    }

    public float angle;
    public float3 w01;
    public float3 w32;
    public float3 vcm; 
    public float3 projection;
    public float3 residual;
    public float3 v1mn;
    
    void ConnectionCycleToggled(OverlayButton toggledButton, OverlayButton atom0connectionButton, OverlayButton atom1connectionButton) {
        int cycleCount = System.Enum.GetNames(typeof(ConnectionCycle)).Length;
        if (toggledButton == atom0connectionButton) {
            switch (startConnectionCycle) {
                case (ConnectionCycle.UNLINKED):
                    atom0connectionButton.button.image.sprite = SpriteManager.springIcon;
                    startConnectionCycle = ConnectionCycle.SPRING;
                    break;
                case (ConnectionCycle.SPRING):
                    atom0connectionButton.button.image.sprite = SpriteManager.linkedIcon;
                    startConnectionCycle = ConnectionCycle.LINKED;
                    break;
                case (ConnectionCycle.LINKED):
                    atom0connectionButton.button.image.sprite = SpriteManager.unlinkedIcon;
                    startConnectionCycle = ConnectionCycle.UNLINKED;
                    break;
            }
        } else {
            switch (endConnectionCycle) {
                case (ConnectionCycle.UNLINKED):
                    atom1connectionButton.button.image.sprite = SpriteManager.springIcon;
                    endConnectionCycle = ConnectionCycle.SPRING;
                    break;
                case (ConnectionCycle.SPRING):
                    atom1connectionButton.button.image.sprite = SpriteManager.linkedIcon;
                    endConnectionCycle = ConnectionCycle.LINKED;
                    break;
                case (ConnectionCycle.LINKED):
                    atom1connectionButton.button.image.sprite = SpriteManager.unlinkedIcon;
                    endConnectionCycle = ConnectionCycle.UNLINKED;
                    break;
            }
        }

    }

    IEnumerator ModifyBond() {
        if (!CheckChainAtoms(2)) {
            yield break;
        }

        AtomID startID = chainAtomIDs[0];
        AtomID endID = chainAtomIDs[1];
        Atom startAtom = chainAtoms[0];
        Atom endAtom = chainAtoms[1];

        List<(Atom, AtomID, int)> startConnections = geometry.GetConnectedAtoms(startID, new HashSet<AtomID>{endID}).ToList();
        List<(Atom, AtomID, int)> endConnections = geometry.GetConnectedAtoms(endID, new HashSet<AtomID>{startID}).ToList();

        OverlayButton startHandle = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton endHandle = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton topPivotHandle = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton bottomPivotHandle = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton startConnectionButton = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton endConnectionButton = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);

        RectTransform startHandleRect = startHandle.button.GetComponent<RectTransform>();
        RectTransform endHandleRect = endHandle.button.GetComponent<RectTransform>();
        RectTransform topPivotHandleRect = topPivotHandle.button.GetComponent<RectTransform>();
        RectTransform bottomPivotHandleRect = bottomPivotHandle.button.GetComponent<RectTransform>();
        RectTransform startConnectionRect = startConnectionButton.button.GetComponent<RectTransform>();
        RectTransform endConnectionRect = endConnectionButton.button.GetComponent<RectTransform>();

        float scale = 0.002f * startHandleRect.rect.width;
        modifyPivot = 0.5f;

        startHandleRect.sizeDelta = new Vector2(56, 56);
        endHandleRect.sizeDelta = new Vector2(56, 56);
        startConnectionRect.sizeDelta = new Vector2(56, 56);
        endConnectionRect.sizeDelta = new Vector2(56, 56);

        startHandle.button.image.sprite = SpriteManager.dragIcon;
        endHandle.button.image.sprite = SpriteManager.dragIcon;
        topPivotHandle.button.image.sprite = SpriteManager.whiteTriangle;
        bottomPivotHandle.button.image.sprite = SpriteManager.whiteTriangle;

        startConnectionButton.button.image.sprite = GetConnectionCycleSprite(startConnectionCycle);
        endConnectionButton.button.image.sprite = GetConnectionCycleSprite(endConnectionCycle);

        startHandle.onBeginDrag    = () => {StartCoroutine(BondHandleDragged(startHandleRect, endHandleRect, startHandle, endHandle, startConnections, endConnections));};
        endHandle.onBeginDrag    = () => {StartCoroutine(BondHandleDragged(startHandleRect, endHandleRect, startHandle, endHandle, startConnections, endConnections));};
        topPivotHandle.onBeginDrag    = () => {StartCoroutine(PivotDragged(startHandleRect, endHandleRect, topPivotHandle, bottomPivotHandle));};
        bottomPivotHandle.onBeginDrag = () => {StartCoroutine(PivotDragged(startHandleRect, endHandleRect, topPivotHandle, bottomPivotHandle));};

        topPivotHandle.button.enabled = false;
        bottomPivotHandle.button.enabled = false;
        startHandle.button.enabled = false;
        endHandle.button.enabled = false;
        startConnectionButton.button.onClick.AddListener(() => {ConnectionCycleToggled(startConnectionButton, startConnectionButton, endConnectionButton);});
        endConnectionButton.button.onClick.AddListener(() => {ConnectionCycleToggled(endConnectionButton, startConnectionButton, endConnectionButton);});

        bool cancelled = false;
        while (true) {

            if (Input.GetKey(KeyCode.Escape)) {
                cancelled = true;
                break;
            } else if (Input.GetKey(KeyCode.Return)) {
                break;
            }

            Vector3 bondVector = lineDrawer.transform.TransformDirection(endAtom.position - startAtom.position);
            Vector3 pivotWorldPos = lineDrawer.transform.TransformPoint((startAtom.position * (1f - modifyPivot) + endAtom.position * modifyPivot) - offset);
            Vector3 cross = math.normalizesafe(math.cross(bondVector, pivotWorldPos - Camera.main.transform.position));

            startHandleRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(startAtom.position - offset));
            endHandleRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(endAtom.position - offset));

            topPivotHandleRect.position = Camera.main.WorldToScreenPoint(pivotWorldPos + cross * scale);
            bottomPivotHandleRect.position = Camera.main.WorldToScreenPoint(pivotWorldPos - cross * scale);

            startConnectionRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(startAtom.position - offset) - (Vector3)math.normalize(bondVector) * 0.25f);
            endConnectionRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(endAtom.position - offset) + (Vector3)math.normalize(bondVector) * 0.25f);

            float angle = math.degrees(math.atan2(cross.y, cross.x));

            startHandleRect.rotation = topPivotHandleRect.rotation = Quaternion.Euler(0f, 0f, angle + 90f);
            endHandleRect.rotation = bottomPivotHandleRect.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

            yield return null;
        }

        GameObject.Destroy(startHandle.gameObject);
        GameObject.Destroy(endHandle.gameObject);
        GameObject.Destroy(topPivotHandle.gameObject);
        GameObject.Destroy(bottomPivotHandle.gameObject);
        GameObject.Destroy(startConnectionButton.gameObject);
        GameObject.Destroy(endConnectionButton.gameObject);

        geometryHistory.SaveState(string.Format("Modify bond '{0}'-'{1}'", startID, endID));
        if (cancelled) {
            geometryHistory.Undo();            
        }
        StartCoroutine(Redraw());
        

    }

    IEnumerator ModifyAngle() {
        if (!CheckChainAtoms(3)) {
            yield break;
        }

        AtomID startID      = chainAtomIDs[0];
        AtomID centreAtomID = chainAtomIDs[1];
        AtomID endID        = chainAtomIDs[2];
        Atom startAtom      = chainAtoms[0];
        Atom centreAtom     = chainAtoms[1];
        Atom endAtom        = chainAtoms[2];

        List<(Atom, AtomID, int)> startConnections = geometry.GetConnectedAtoms(startID, new HashSet<AtomID>{centreAtomID, endID}).ToList();
        List<(Atom, AtomID, int)> endConnections   = geometry.GetConnectedAtoms(endID  , new HashSet<AtomID>{startID, centreAtomID}).ToList();

        OverlayButton startHandle = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton endHandle = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton pivotHandle = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton startConnectionButton = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton endConnectionButton = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);

        RectTransform startHandleRect = startHandle.button.GetComponent<RectTransform>();
        RectTransform endHandleRect = endHandle.button.GetComponent<RectTransform>();
        RectTransform pivotHandleRect = pivotHandle.button.GetComponent<RectTransform>();
        RectTransform startConnectionRect = startConnectionButton.button.GetComponent<RectTransform>();
        RectTransform endConnectionRect = endConnectionButton.button.GetComponent<RectTransform>();

        float scale = 0.002f * startHandleRect.rect.width;
        modifyPivot = 0.5f;
        float3 v10 = startAtom.position - centreAtom.position;
        float3 v12 = endAtom.position - centreAtom.position;
        float r10 = math.length(v10);
        float r12 = math.length(v12);
        float averageDistance = (r10 + r12) * 0.5f;

        startHandleRect.sizeDelta = new Vector2(56, 56);
        endHandleRect.sizeDelta = new Vector2(56, 56);
        startConnectionRect.sizeDelta = new Vector2(56, 56);
        endConnectionRect.sizeDelta = new Vector2(56, 56);

        startHandle.button.image.sprite = SpriteManager.dragIcon;
        endHandle.button.image.sprite = SpriteManager.dragIcon;
        pivotHandle.button.image.sprite = SpriteManager.whiteTriangle;

        startConnectionButton.button.image.sprite = GetConnectionCycleSprite(startConnectionCycle);
        endConnectionButton.button.image.sprite = GetConnectionCycleSprite(endConnectionCycle);
        
        startHandle.onBeginDrag  = () => {StartCoroutine(AngleHandleDragged(startHandleRect, endHandleRect, startHandle, endHandle, startConnections, endConnections));};
        endHandle.onBeginDrag    = () => {StartCoroutine(AngleHandleDragged(startHandleRect, endHandleRect, startHandle, endHandle, startConnections, endConnections));};
        pivotHandle.onBeginDrag  = () => {StartCoroutine(PivotDragged(startHandleRect, endHandleRect, pivotHandle, pivotHandle));};

        startHandle.button.enabled = false;
        endHandle.button.enabled = false;
        pivotHandle.button.enabled = false;

        startConnectionButton.button.onClick.AddListener(() => {ConnectionCycleToggled(startConnectionButton, startConnectionButton, endConnectionButton);});
        endConnectionButton.button.onClick.AddListener(()   => {ConnectionCycleToggled(endConnectionButton, startConnectionButton, endConnectionButton);});


        bool cancelled = false;
        while (true) {

            if (Input.GetKey(KeyCode.Escape)) {
                cancelled = true;
                break;
            } else if (Input.GetKey(KeyCode.Return)) {
                break;
            }

            v10 = startAtom.position - centreAtom.position;
            v12 = endAtom.position - centreAtom.position;

            float3 centreToPivotVectorNormalised = math.normalizesafe(v10 * (1f - modifyPivot) + v12 * modifyPivot);
            float3 centreToPivotVector = centreToPivotVectorNormalised * (averageDistance + scale);
            float3 pivotPosition = lineDrawer.transform.TransformPoint(centreAtom.position + centreToPivotVector - offset);
            
            startHandleRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(startAtom.position - offset));
            endHandleRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(endAtom.position - offset));

            pivotHandleRect.position = Camera.main.WorldToScreenPoint(pivotPosition);

            startConnectionRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(startAtom.position - offset) + (Vector3)math.normalizesafe(v10) * 0.25f);
            endConnectionRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(endAtom.position - offset) + (Vector3)math.normalizesafe(v12) * 0.25f);

            float angle = math.degrees(math.atan2(centreToPivotVectorNormalised.y, centreToPivotVectorNormalised.x));

            pivotHandleRect.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

            yield return null;
        }

        GameObject.Destroy(startHandle.gameObject);
        GameObject.Destroy(endHandle.gameObject);
        GameObject.Destroy(pivotHandle.gameObject);
        GameObject.Destroy(startConnectionButton.gameObject);
        GameObject.Destroy(endConnectionButton.gameObject);

        geometryHistory.SaveState(string.Format("Modify angle '{0}'-'{1}'-'{2}'", startID, centreAtomID, endID));
        if (cancelled) {
            geometryHistory.Undo();            
        }
        StartCoroutine(Redraw());
        
    }

    IEnumerator ModifyDihedral() {
        if (!CheckChainAtoms(4)) {
            yield break;
        }

        AtomID startID = chainAtomIDs[0];
        AtomID atomID1 = chainAtomIDs[1];
        AtomID atomID2 = chainAtomIDs[2];
        AtomID endID   = chainAtomIDs[3];
        Atom startAtom = chainAtoms[0];
        Atom atom1     = chainAtoms[1];
        Atom atom2     = chainAtoms[2];
        Atom endAtom   = chainAtoms[3];

        List<(Atom, AtomID, int)> startConnections = geometry.GetConnectedAtoms(startID, new HashSet<AtomID>{atomID2, endID}).ToList();
        List<(Atom, AtomID, int)> endConnections = geometry.GetConnectedAtoms(endID, new HashSet<AtomID>{startID, atomID1}).ToList();

        OverlayButton startHandle = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton endHandle = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton topPivotHandle = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton bottomPivotHandle = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton startConnectionButton = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);
        OverlayButton endConnectionButton = PrefabManager.InstantiateOverlayButton(lineDrawer.transform);

        RectTransform startHandleRect = startHandle.button.GetComponent<RectTransform>();
        RectTransform endHandleRect = endHandle.button.GetComponent<RectTransform>();
        RectTransform topPivotHandleRect = topPivotHandle.button.GetComponent<RectTransform>();
        RectTransform bottomPivotHandleRect = bottomPivotHandle.button.GetComponent<RectTransform>();
        RectTransform startConnectionRect = startConnectionButton.button.GetComponent<RectTransform>();
        RectTransform endConnectionRect = endConnectionButton.button.GetComponent<RectTransform>();

        float scale = 0.002f * startHandleRect.rect.width;

        modifyPivot = 0.5f;
        float3 v01 = math.normalize(atom1.position - startAtom.position);
        float3 v21 = math.normalize(atom1.position - atom2.position);
        float3 v32 = math.normalize(atom2.position - endAtom.position);
        float r01 = math.length(v01);
        float r21 = math.length(v21);
        float r32 = math.length(v32);

        startHandleRect.sizeDelta = new Vector2(56, 56);
        endHandleRect.sizeDelta = new Vector2(56, 56);
        startConnectionRect.sizeDelta = new Vector2(56, 56);
        endConnectionRect.sizeDelta = new Vector2(56, 56);


        startHandle.button.image.sprite = SpriteManager.dragIcon;
        endHandle.button.image.sprite = SpriteManager.dragIcon;
        topPivotHandle.button.image.sprite = SpriteManager.whiteTriangle;
        bottomPivotHandle.button.image.sprite = SpriteManager.whiteTriangle;

        startConnectionButton.button.image.sprite = GetConnectionCycleSprite(startConnectionCycle);
        endConnectionButton.button.image.sprite = GetConnectionCycleSprite(endConnectionCycle);

        startHandle.onBeginDrag       = () => {StartCoroutine(DihedralHandleDragged(startHandleRect, endHandleRect, startHandle, endHandle, startConnections, endConnections));};
        endHandle.onBeginDrag         = () => {StartCoroutine(DihedralHandleDragged(startHandleRect, endHandleRect, startHandle, endHandle, startConnections, endConnections));};
        topPivotHandle.onBeginDrag    = () => {StartCoroutine(PivotDragged(startHandleRect, endHandleRect, topPivotHandle, bottomPivotHandle));};
        bottomPivotHandle.onBeginDrag = () => {StartCoroutine(PivotDragged(startHandleRect, endHandleRect, topPivotHandle, bottomPivotHandle));};

        topPivotHandle.button.enabled = false;
        bottomPivotHandle.button.enabled = false;
        startHandle.button.enabled = false;
        endHandle.button.enabled = false;
        startConnectionButton.button.onClick.AddListener(() => {ConnectionCycleToggled(startConnectionButton, startConnectionButton, endConnectionButton);});
        endConnectionButton.button.onClick.AddListener(() => {ConnectionCycleToggled(endConnectionButton, startConnectionButton, endConnectionButton);});

        
        bool cancelled = false;
        while (true) {

            if (Input.GetKey(KeyCode.Escape)) {
                cancelled = true;
                break;
            } else if (Input.GetKey(KeyCode.Return)) {
                break;
            }

            Vector3 bondVector = lineDrawer.transform.TransformDirection(atom1.position - atom2.position);
            Vector3 pivotWorldPos = lineDrawer.transform.TransformPoint((atom1.position * (1f - modifyPivot) + atom2.position * modifyPivot) - offset);
            Vector3 cross = math.normalizesafe(math.cross(bondVector, pivotWorldPos - Camera.main.transform.position));
            
            startHandleRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(startAtom.position - offset));
            endHandleRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(endAtom.position - offset));

            topPivotHandleRect.position = Camera.main.WorldToScreenPoint(pivotWorldPos + cross * scale);
            bottomPivotHandleRect.position = Camera.main.WorldToScreenPoint(pivotWorldPos - cross * scale);

            startConnectionRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(startAtom.position - offset) - (Vector3)math.normalize(bondVector) * 0.25f);
            endConnectionRect.position = Camera.main.WorldToScreenPoint(lineDrawer.transform.TransformPoint(endAtom.position - offset) + (Vector3)math.normalize(bondVector) * 0.25f);

            float angle = math.degrees(math.atan2(cross.y, cross.x));

            topPivotHandleRect.rotation    = Quaternion.Euler(0f, 0f, angle + 90f);
            bottomPivotHandleRect.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

            
            yield return null;
        }

        GameObject.Destroy(startHandle.gameObject);
        GameObject.Destroy(endHandle.gameObject);
        GameObject.Destroy(topPivotHandle.gameObject);
        GameObject.Destroy(bottomPivotHandle.gameObject);
        GameObject.Destroy(startConnectionButton.gameObject);
        GameObject.Destroy(endConnectionButton.gameObject);

        geometryHistory.SaveState(string.Format("Modify dihedral '{0}'-'{1}'-'{2}'-'{3}'", startID, atomID1, atomID2, endID));
        if (cancelled) {
            geometryHistory.Undo();            
        }
        StartCoroutine(Redraw());
    }

    IEnumerator EditTransform() {
        
        float yRotation = 0f;
        float xRotation = 0f;
        float zRotation = 0f;
        float3 screenCentre = new float3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        float3 oldMousePosition = Input.mousePosition;
        while (pointerDown && !moving) {
            
            float3 mousePosition = (float3)Input.mousePosition;
            float3 mouseDelta = mousePosition - oldMousePosition;

            if (finalEditMode == EditMode.TRANSLATE) {
                atomsRepresentation.Translate(Vector3.right * mouseDelta.x * translationSensitivity, Space.World);
                atomsRepresentation.Translate(Vector3.up  * mouseDelta.y * translationSensitivity, Space.World);
            } else if (finalEditMode == EditMode.ROTATEZ) {
                float3 torque = math.cross(
                    (mousePosition - screenCentre),
                    mouseDelta
                );
                zRotation = torque.z * rotationSensitivity / math.length(screenCentre);

                atomsRepresentation.Rotate(Vector3.forward, zRotation, Space.World);
                
            } else {
                yRotation = mouseDelta.x * rotationSensitivity;
                xRotation = mouseDelta.y * rotationSensitivity;

                atomsRepresentation.Rotate(Vector3.down,  yRotation, Space.World);
                atomsRepresentation.Rotate(Vector3.right, xRotation, Space.World);

            }

            oldMousePosition = mousePosition;

            yield return null;
        }

        //Continue spinning until cursor is pressed
        if (xRotation != 0f || yRotation != 0f || zRotation != 0f) {
            while (!pointerDown) {
                atomsRepresentation.Rotate(Vector3.forward, zRotation, Space.World);
                atomsRepresentation.Rotate(Vector3.down,    yRotation, Space.World);
                atomsRepresentation.Rotate(Vector3.right,   xRotation, Space.World);
                yield return null;
            }
        }
    }

    IEnumerator ToggleConnection() {
        if (!CheckChainAtoms(2)) {
            yield break;
        }

        if (chainAtoms[0].IsConnectedTo(chainAtomIDs[1])) {
            geometry.Disconnect(chainAtomIDs[0], chainAtomIDs[1]);
            geometryHistory.SaveState(string.Format("Disconnect '{0}'-'{1}'", chainAtomIDs[0], chainAtomIDs[1]));
        } else {
            geometry.Connect(chainAtomIDs[0], chainAtomIDs[1], BT.SINGLE);
            geometryHistory.SaveState(string.Format("Connect '{0}'-'{1}'", chainAtomIDs[0], chainAtomIDs[1]));
        }

        StartCoroutine(Redraw());
    }

    void RemoveResidue(ResidueID residueID) {

        Residue residue0;
        if (geometry.TryGetResidue(residueID, out residue0)) {

            lineDrawer.RemoveResidue(residueID);

            List<ResidueID> neighbourIDs = new List<ResidueID>();

            foreach ((AtomID atomID0, AtomID atomID1) in residue0.NeighbouringAtomIDs()) {
                lineDrawer.RemoveLinker((atomID0, atomID1));
                lineDrawer.RemoveLinker((atomID1, atomID0));

                neighbourIDs.Add(atomID1.residueID);
            }

            geometry.RemoveResidue(residueID);

            foreach (ResidueID neighbourID in neighbourIDs) {
                Residue neighbour;
                if (geometry.TryGetResidue(neighbourID, out neighbour)) {
                    lineDrawer.UpdateCapSites(neighbourID, neighbour);
                }
            }
            

            geometryHistory.SaveState(string.Format("Remove Residue '{0}'", closestAtom.residueID));
        }
    }

    void MutateResidue(ResidueID residueID) {
        Residue residue;
        if (!geometry.TryGetResidue(residueID, out residue)) {
            return;
        }

        //Check if Residue is standard
        if (residue.state != RS.STANDARD) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot mutate Residue '{0}' - Residue is not standard ('{1}' - State: {2})",
                residueID,
                residue.residueName,
                residue.state
            );
            return;
        }

        StartCoroutine(MutateResidue(residue));
    }

    IEnumerator MutateResidue(Residue residue) {

        bool cancelled = false;
        bool optimise = false;

        MultiPrompt multiPrompt = MultiPrompt.main;
        multiPrompt.Initialise(
            "Select New Standard Residue",
            string.Format(
                "Input the 3-letter name of the Residue to mutate '{0}' ({1}).",
                residue.residueID,
                residue.residueName
            ),
            new ButtonSetup("No Opt", () => {}),
            new ButtonSetup("Optimise", () => {optimise=true;}),
            new ButtonSetup("Cancel", () => {cancelled = true;}),
            input:true
        );



        while (!multiPrompt.userResponded) {
            yield return null;
        }

        multiPrompt.Hide();

        if (cancelled || multiPrompt.cancelled) {
            yield break;
        }

        string newResidueName = multiPrompt.inputField.text.ToUpper();

        Residue newResidue;
        try {
            newResidue = Residue.FromString(newResidueName);
        } catch {
            newResidue = null;
        }

        if (newResidue == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Standard residue '{0}' not found in database!",
                newResidueName
            );
            yield break;
        }

        ResidueMutator residueMutator;
        try {
            residueMutator = new ResidueMutator(geometry, residue.residueID);
        } catch (System.Exception e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to Mutate Residue: {1}",
                e.Message
            );
            yield break;
        }

        yield return residueMutator.MutateStandard(
            newResidue,
            optimise
        );

        StartCoroutine(Redraw());
        
        geometryHistory.SaveState(string.Format("Mutate Residue '{0}'", closestAtom.residueID));

    }

    void CapResidue(AtomID atomID) {
        Residue residue;
        if (!geometry.TryGetResidue(atomID.residueID, out residue)) {
            return;
        }

        PDBID pdbID = atomID.pdbID;
        if (!residue.EnumerateCapSites().Contains(pdbID)) {
            return;
        }

        //Check if Residue is standard
        if (residue.state != RS.STANDARD) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot cap Residue '{0}' - Residue is not standard ('{1}' - State: {2})",
                atomID.residueID,
                residue.residueName,
                residue.state
            );
            return;
        }

        StartCoroutine(CapResidue(residue, pdbID));
    }

    IEnumerator CapResidue(Residue residue, PDBID pdbID) {

        bool cancelled = false;
        bool residueCap = false;

        MultiPrompt multiPrompt = MultiPrompt.main;

        if (pdbID.element == Element.C) {
            multiPrompt.Initialise(
                "Cap C Terminus",
                "Select NME Cap or carboxylate cap.",
                new ButtonSetup("NME", () => {residueCap = true;}),
                new ButtonSetup("COO-", () => {}),
                new ButtonSetup("Cancel", () => {cancelled = true;})
            );
        } else {
            multiPrompt.Initialise(
                "Cap N Terminus",
                "Select ACE Cap or `amine cap.",
                new ButtonSetup("NME", () => {residueCap = true;}),
                new ButtonSetup("NH3+", () => {}),
                new ButtonSetup("Cancel", () => {cancelled = true;})
            );
        }

        while (!multiPrompt.userResponded) {
            yield return null;
        }

        multiPrompt.Hide();

        if (cancelled || multiPrompt.cancelled) {
            yield break;
        }

        Residue newResidue;
        if (pdbID.element == Element.C) {
            if (residueCap) {
                ResidueID newResidueID = residue.residueID.GetNextID();
                while (residue.parent.HasResidue(newResidueID)) {
                    newResidueID = residue.residueID.GetNextID();
                }
                newResidue = residue.AddNeighbourResidue("NME", pdbID, newResidueID);
                residue.parent.AddResidue(newResidueID, newResidue);
            } else {
                residue.AddAtomToHost(pdbID, new PDBID("O", "XT"));
                residue.state = RS.C_TERMINAL;
            }
        } else {
            if (residueCap) {
                ResidueID newResidueID = residue.residueID.GetPreviousID();
                while (residue.parent.HasResidue(newResidueID)) {
                    newResidueID = residue.residueID.GetPreviousID();
                }
                newResidue = residue.AddNeighbourResidue("ACE", pdbID, newResidueID);
                residue.parent.AddResidue(newResidueID, newResidue);
            } else {
                residue.AddAtomToHost(pdbID, new PDBID("H", "", 2));
                residue.AddAtomToHost(pdbID, new PDBID("H", "", 3));
                residue.state = RS.N_TERMINAL;
            }
        }
        StartCoroutine(Redraw());
        
        geometryHistory.SaveState(string.Format("Cap Residue '{0}'", closestAtom.residueID));

    }

    IEnumerator BrushOptimise() {

        if (CheckChainAtoms(1)) {
            yield break;
        }

        // UNUSED

//        AtomID optimiseAtomID = chainAtomIDs[0];
//
//
//        ResidueID optimiseResidueID = optimiseAtomID.residueID;
//        Residue optimiseResidue = geometry.GetResidue(optimiseResidueID);
//
//        if (optimiseResidue == null) {
//            yield break;
//        }
//
//        Atom optimiseAtom;
//        if (!optimiseResidue.atoms.TryGetValue(optimiseAtomID.pdbID, out optimiseAtom)) {
//            yield break;
//        }
//
//        float stepSize = 0.2f;
//        float maxStep = 0.4f;
//        float cutoffStart = 2f;
//        float cutoffEnd = 6f;
//
//        float cutoffStartSq = cutoffStart.Squared();
//        float cutoffEndSq = cutoffEnd.Squared();
//
//        int numNearbyAtoms = 0;
//        List<(AtomID, Atom)> optimiseAtoms = new List<(AtomID, Atom)>();
//        List<float> forceMultipliers = new List<float>();
//        List<float3> positionsList = new List<float3>();
//        Dictionary<AtomID, int> atomIDToIndex = new Dictionary<AtomID, int>();
//
//        foreach (ResidueID nearbyResidueID in optimiseResidue.ResiduesWithinDistance(Settings.maxNonBondingCutoff).Append(optimiseResidueID)) {
//            Residue nearbyResidue;
//            if (!geometry.TryGetResidue(nearbyResidueID, out nearbyResidue)) {
//                continue;
//            }
//            
//            Dictionary<RS, Residue> standardFamily;
//            if (! Data.standardResidues.TryGetValue(nearbyResidue.residueName, out standardFamily)) {
//                continue;
//            }
//            
//            Residue standardResidue;
//            if ( ! (
//                    standardFamily.TryGetValue(RS.STANDARD, out standardResidue)
//                    || standardFamily.TryGetValue(RS.WATER, out standardResidue)
//            ) ) {
//                continue;
//            }
//
//
//            foreach ((PDBID nearbyPDBID, Atom nearbyAtom) in nearbyResidue.EnumerateAtoms()) {
//                
//                Atom standardAtom;
//                if (standardResidue.atoms.TryGetValue(nearbyPDBID, out standardAtom)) {
//                    nearbyAtom.partialCharge = standardAtom.partialCharge;
//                }
//
//                float distanceSq = math.distancesq(optimiseAtom.position, nearbyAtom.position);
//                if (distanceSq > cutoffEndSq) {
//                    continue;
//                }
//
//                float forceScale;
//                if (distanceSq < cutoffStartSq) {
//                    forceScale = 1f;
//                } else {
//                    forceScale = CustomMathematics.Map(distanceSq, cutoffEndSq, cutoffStartSq, 0f, 1f);
//                }
//
//                AtomID nearbyAtomID = new AtomID(nearbyResidueID, nearbyPDBID);
//                optimiseAtoms.Add((nearbyAtomID, nearbyAtom));
//                forceMultipliers.Add(forceScale);
//                positionsList.Add(nearbyAtom.position);
//                atomIDToIndex[nearbyAtomID] = numNearbyAtoms++;
//            }
//        }
//        
//        int3[] improperCycles = new int3[] {
//            new int3(0, 1, 2), new int3(0, 2, 1),
//            new int3(1, 0, 2), new int3(1, 2, 0),
//            new int3(2, 0, 1), new int3(2, 1, 0)
//        };
//
//        List<PrecomputedNonBonding> nonBondings = new List<PrecomputedNonBonding>();
//        List<DihedralCalculator> impropers = new List<DihedralCalculator>();
//        List<DihedralCalculator> torsions = new List<DihedralCalculator>();
//        List<StretchCalculator> stretches = new List<StretchCalculator>();
//        List<BendCalculator> bends = new List<BendCalculator>();
//        
//
//
//        (AtomID, Atom)[] optimiseAtomArray = optimiseAtoms.ToArray();
//  
//        for (int i=0; i<numNearbyAtoms; i++) {
//
//            (AtomID atomID0, Atom atom0) = optimiseAtomArray[i];
//
//            //Non-bonding terms
//            for (int j=i+1; j<numNearbyAtoms; j++) {
//
//                (AtomID atom1ID, Atom atom1) = optimiseAtomArray[j];
//
//                PrecomputedNonBonding pnb;
//                try {
//                    pnb = new PrecomputedNonBonding(
//                        atom0, 
//                        atom1, 
//                        new int2(i,j), 
//                        geometry.GetGraphDistance(atomID0, atom1ID, 3)
//                    );
//                } catch {
//                    continue;
//                } 
//                nonBondings.Add(pnb);
//            }
//
//            AtomID[] atom0Neighbours = atom0.EnumerateConnections()
//                .Select(x => x.Item1)
//                .ToArray();
//                
//            if (atom0Neighbours.Length == 3) {
//
//                //Cycle over the 6 possible improper combinations for this central Atom
//                foreach (int3 improperCycle in improperCycles) {
//                    AtomID atomID1 = atom0Neighbours[improperCycle.x];
//                    AtomID atomID2 = atom0Neighbours[improperCycle.y];
//                    AtomID atomID3 = atom0Neighbours[improperCycle.z];
//
//                    Atom atom1 = geometry.GetAtom(atomID1);
//                    Atom atom2 = geometry.GetAtom(atomID2);
//                    Atom atom3 = geometry.GetAtom(atomID3);
//
//                    try { 
//                        //Check this hasn't been added in reverse
//                        int4 dihedralKey = new int4(
//                            atomIDToIndex[atomID1], 
//                            atomIDToIndex[atomID0],
//                            atomIDToIndex[atomID2],
//                            atomIDToIndex[atomID3]
//                        );
//                        if (!impropers.Select(
//                            x => x.key == dihedralKey.wzyx
//                        ).Any(x => math.all(x))) {
//                            impropers.Add(new DihedralCalculator(atom1, atom0, atom2, atom3, false, dihedralKey));
//                        }
//                    } catch {}
//                }
//                
//            }
//            
//            //Stretches
//            foreach (AtomID atomID1 in atom0Neighbours) {
//                Atom atom1 = geometry.GetAtom(atomID1);
//                
//                try { 
//                    //Check this hasn't been added in reverse
//                    int2 stretchKey = new int2(
//                        atomIDToIndex[atomID0], 
//                        atomIDToIndex[atomID1]
//                    );
//                    if (!stretches.Select(
//                        x => x.key == stretchKey.yx
//                    ).Any(x => math.all(x))) {
//                        stretches.Add(new StretchCalculator(atom0, atom1, stretchKey));
//                    }
//                } catch {}
//
//                //Bends
//                foreach ((AtomID atomID2, BT bondType12) in atom1.EnumerateConnections()) {
//                    if (atomID2 == atomID0) {continue;}
//                    Atom atom2 = geometry.GetAtom(atomID2);
//                    
//                    try { 
//                        //Check this hasn't been added in reverse
//                        int3 bendKey = new int3(
//                            atomIDToIndex[atomID0], 
//                            atomIDToIndex[atomID1],
//                            atomIDToIndex[atomID2]
//                        );
//                        if (!bends.Select(
//                            x => x.key.zyx == bendKey
//                        ).Any(x => math.all(x))) {
//                            bends.Add(new BendCalculator(atom0, atom1, atom2, bendKey));
//                        }
//                    } catch {}
//                    
//                    
//                    //Propers
//                    foreach ((AtomID atomID3, BT bondType23) in atom2.EnumerateConnections()) {
//                        if (atomID3 == atomID1) {continue;}
//                        Atom atom3 = geometry.GetAtom(atomID3);
//
//                        try { 
//                            //Check this hasn't been added in reverse
//                            int4 dihedralKey = new int4(
//                                atomIDToIndex[atomID0], 
//                                atomIDToIndex[atomID1],
//                                atomIDToIndex[atomID2],
//                                atomIDToIndex[atomID3]
//                            );
//                            if (!torsions.Select(
//                                x => x.key.wzyx == dihedralKey
//                            ).Any(x => math.all(x))) {
//                                torsions.Add(new DihedralCalculator(atom0, atom1, atom2, atom3, true, dihedralKey));
//                            }
//                        } catch {}
//                    }
//                }
//            }
//        }
//
//        float3[] forces = new float3[numNearbyAtoms];
//        float3[] positions = positionsList.ToArray();
//        float[] forceMultiplierArray = forceMultipliers.ToArray();
//
//        while (pointerDown && chainAtomIDs[0] == optimiseAtomID) {
//
//            for (int i=0; i<numNearbyAtoms; i++) {
//                forces[i] = 0f;
//            }
//
//            stretches  .ForEach(x => x.AddForces(forces, positions, forceMultiplierArray));
//            bends      .ForEach(x => x.AddForces(forces, positions, forceMultiplierArray));
//            torsions   .ForEach(x => x.AddForces(forces, positions, forceMultiplierArray));
//            impropers  .ForEach(x => x.AddForces(forces, positions, forceMultiplierArray));
//            nonBondings.ForEach(x => x.AddForces(forces, positions, forceMultiplierArray));
//
//
//            float maxDis = forces.Select(x => math.length(x) * stepSize).Max();
//            if (maxDis > maxStep) {
//                stepSize *= maxStep / maxDis;
//            }
//
//            for (int i=0; i<numNearbyAtoms; i++) {
//                float3 position = positions[i] += forces[i] * stepSize;
//                (AtomID atomID, Atom atom) = optimiseAtomArray[i];
//                atom.position = position;
//                lineDrawer.UpdatePosition(atomID, position - offset);
//
//            }
//
//            yield return null;
//        }
    }

    public void OnScroll(PointerEventData pointerEventData) {
        atomsRepresentation.Translate(- mainCamera.transform.forward * pointerEventData.scrollDelta.y * zoomSensitivity, Space.World);
    }

    /// <summary>Draws a line between start and end.</summary>
    /// <param name="start">The starting position of the line.</param>
    /// <param name="end">The end position of the line.</param>
    /// <param name="material">The shared material to use for the line.</param>
    /// <param name="startColor">The color of the start of the line.</param>
    /// <param name="endColor">The color of the end of the line.</param>
    /// <param name="startWidth">The width of the line at the start.</param>
    /// <param name="endWidth">The width of the line at the end.</param>
    /// <param name="parent">The Transform of the parent. Leave as null for a World Space line.</param>
    GameObject DrawLine(
        Vector3 start, 
        Vector3 end, 
        Material material, 
        Color startColor, 
        Color endColor, 
        float startWidth = 0.05f,
        float endWidth = 0.05f,
        Transform parent=null
    ) {
        GameObject lineRendererGO = new GameObject();
        lineRendererGO.transform.SetParent(parent);
        lineRendererGO.transform.localScale = Vector3.one;
        lineRendererGO.transform.localPosition = Vector3.zero;
        lineRendererGO.transform.localRotation = Quaternion.identity;

        LineRenderer lineRenderer = lineRendererGO.AddComponent<LineRenderer>();
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;
        lineRenderer.sharedMaterial = material;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.useWorldSpace = (parent == null);
        lineRenderer.SetPositions( new Vector3[2] {start, end});
        
        return lineRendererGO;
    }










    public float mouseDeltaX = 0f;
    public float mouseDeltaY = 0f;
    public float pitchAxis = 0f;
    public float rollAxis = 0f;
    public float forwardAxis = 0f;
    public float yawAxis = 0f;
    bool view = true;

    public Matrix4x4 matrix;
    public Vector4 vecScale;
    public Vector3 position;
    public bool boolmatrix;
    public bool boolvecScale;
    public bool boolposition;
    
    public Matrix4x4 altmatrix;
    public Vector4 altvecScale;
    public Vector3 altposition;
    

    void Update() {

        if (
            ContextMenu.main.canvas.enabled ||
            MultiPrompt.main.canvas.enabled
        ) {
            closestAtomID = AtomID.Empty;
            return;
        }

        if (texture3D != null) {
            //matrix = Matrix4x4.Transpose(cubeTransform.localToWorldMatrix);
            matrix = cubeTransform.localToWorldMatrix * Matrix4x4.Rotate(Quaternion.Euler(0,90,0));
            //vecScale = cameraOrbitalTransform.lossyScale / cubeTransform.lossyScale.x;
            vecScale.x = gridScale[0];
            vecScale.y = gridScale[1];
            vecScale.z = gridScale[2];
            vecScale.w = 1;
            position = cubeTransform.position;
            position.z -= 15;
            
            orbitalMaterial.SetMatrix("_Rotation", boolmatrix ? altmatrix : matrix);
            orbitalMaterial.SetVector("_Scale", boolvecScale ? altvecScale : vecScale);
            orbitalMaterial.SetVector("_Position", boolposition ? altposition : position);
            
            //orbitalMaterial.SetMatrix("_Rotation", Matrix4x4.Transpose(cubeTransform.localToWorldMatrix));// * cameraOrbitalTransform.localToWorldMatrix);
            //orbitalMaterial.SetVector("_Scale", cameraOrbitalTransform.lossyScale / cubeTransform.lossyScale.x);
            //orbitalMaterial.SetVector("_Position", cubeTransform.position);
        }

        mouseDeltaX = Input.GetAxis("Mouse X");
        mouseDeltaY = -Input.GetAxis("Mouse Y");

        if (moving) {
            forwardAxis = Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f;
            rollAxis = -mouseDeltaX + (Input.GetKey(KeyCode.LeftArrow) ? 1f : Input.GetKey(KeyCode.RightArrow) ? -1f : 0f);
            pitchAxis = mouseDeltaY + (Input.GetKey(KeyCode.UpArrow) ? 1f : Input.GetKey(KeyCode.DownArrow) ? -1f : 0f);
            yawAxis = Input.GetKey(KeyCode.D) ? 1f : Input.GetKey(KeyCode.A) ? -1f : 0f;
            if (Input.GetKeyDown(KeyCode.C)) {
                if (view) {
                    mainCamera.transform.position = pointer.position;
                    pointer.localPosition = Vector3.zero;
                } else {
                    mainCamera.transform.Translate(-Vector3.Scale(separationVector,scale));
                    pointer.Translate(separationVector);
                }
                view = !view;
            }

            if (Input.GetKeyDown(KeyCode.P)) {
                Exit();
            }
            if (Input.GetKeyDown(KeyCode.Space)) {
                GetCollider();
            }
        } else {
            
            SetMod(
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift),
                
                Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl),

                Application.isEditor || 
                (Settings.isWindows && (
                    Input.GetKey(KeyCode.LeftControl) ||
                    Input.GetKey(KeyCode.RightControl)
                )) ||
                Input.GetKey(KeyCode.LeftCommand) ||
                Input.GetKey(KeyCode.RightCommand)
            ); 

            if (cmdMod) {
                if (Input.GetKeyDown(KeyCode.Z)) {
                    geometryHistory.Undo();
                    StartCoroutine(Redraw());
                } else if (Input.GetKeyDown(KeyCode.Y)) {
                    geometryHistory.Redo();
                    StartCoroutine(Redraw());
                }

            }

            if (Input.GetKeyDown(KeyCode.P)) {
                GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);

                if (geometryInterface == null || !geometryInterface.selfDraggedGesture) {
                    return;
                }
                Cursor.lockState = CursorLockMode.Locked;
                StartCoroutine(MoveCamera());
                return;
            }
           

            
            if (!closestAtomEnumeratorRunning) {
                StartCoroutine(GetClosestAtomID(Input.mousePosition));
            }
        }

        ID0 = chainAtomIDs[0].ToString();
        ID1 = chainAtomIDs[1].ToString();
        ID2 = chainAtomIDs[2].ToString();
        ID3 = chainAtomIDs[3].ToString();
    }

    public string ID0;
    public string ID1;
    public string ID2;
    public string ID3;
    public string closestID;


    Camera mainCamera;
    float initialFieldOfView;
    Vector3 initialCameraPosition;
    Quaternion initialCameraRotation;
    public float minSpeed = 0.02f;
    float zAcc = 2f;
    float zDrag = 0.005f;
    float brakeDragRatio = 0.08f;
    float backwardRatio = 0.4f;
    float rollAcc = 180f;
    float rollDrag = 0.05f;
    float yawAcc = 120f;
    float yawDrag = 0.15f;
    float pitchAcc = 240f;
    float pitchDrag = 0.04f;
    Vector3 translation;
    float dZ = 0f;

    Dictionary<int, AtomCollider> colliderDict;
    Dictionary<int, string> residueDict;
    int count;
    int counter;
    int colliderCount;

    public bool moving = false;
    private Transform pointer;
    private Vector3 separationVector = new Vector3(0, -0.1f, 1f);
    private Vector3 scale = new Vector3(0.25f, 0.25f, 0.25f);
    IEnumerator MoveCamera() {

        if (moving) {yield break;}

        colliderCount = 10;

        float forward = 0f;
        float backward = 0f;

        float ddZ = 0f;
        float dRoll = 0f;
        float ddRoll = 0f;
        float dYaw = 0f;
        float ddYaw = 0f;
        float dPitch = 0f;
        float ddPitch = 0f;

        float fieldOfView = initialFieldOfView * 0.5f;

        translation = new Vector3();

        AddPointer(separationVector);

        colliderDict = new Dictionary<int, AtomCollider>();
        residueDict = new Dictionary<int, string>();

        PDBID backboneN = PDBID.N;
        count = 0;
        counter = 0;
        foreach ((AtomID atomID, Atom atom) in geometry.EnumerateAtomIDPairs()) {
            if (atomID.pdbID == backboneN) {
                GameObject sphere = Instantiate<GameObject>(spherePrefab, meshHolder);
                sphere.transform.localPosition = atom.position - offset;

                AtomCollider collider = sphere.GetComponent<AtomCollider>();
                collider.Set(true, count==0);
                colliderDict[count] = collider;
                if (geometry.HasResidue(atomID.residueID)) {
                    residueDict[count] = string.Format(
                        "{0}[{1}]", 
                        atomID.residueID, 
                        geometry.GetResidue(atomID.residueID).residueName
                    );
                    count++;
                }
            } else if (atomID.pdbID.element == Element.O) {
                GameObject sphere = Instantiate<GameObject>(spherePrefab, meshHolder);
                sphere.transform.localPosition = atom.position - offset;

                AtomCollider collider = sphere.GetComponent<AtomCollider>();
                collider.Set(false, false);
            }
        }

        string originalTitle = title.text;

        title.text = string.Format(
            "{0} / {1} (Next: {2}) Count: {3}", 
            counter, 
            count, 
            residueDict[counter],
            colliderCount
        );

        moving = true;
        while (moving) {

            // Z Axis
            forward = (forwardAxis >= 0f) ? forwardAxis : 0f;
            backward = (forwardAxis < 0f) ? - forwardAxis : 0f;

            ddZ = forward * zAcc
                - backward * zAcc * backwardRatio
                - dZ * (zDrag + backward * brakeDragRatio) / Time.deltaTime;
            dZ += ddZ * Time.deltaTime;

            fieldOfView = initialFieldOfView + 5f * dZ;
            mainCamera.fieldOfView = fieldOfView;

            translation.z = dZ * Time.deltaTime + minSpeed;
            mainCamera.transform.Translate(translation);

            // Roll
            ddRoll = rollAxis * rollAcc - (rollDrag * dRoll / Time.deltaTime);
            dRoll += ddRoll * Time.deltaTime;
            mainCamera.transform.RotateAround(pointer.position, mainCamera.transform.forward, dRoll * Time.deltaTime);
            pointer.RotateAround(pointer.position, mainCamera.transform.forward, ddRoll * Time.deltaTime);

            // Yaw
            ddYaw = yawAxis * yawAcc - (yawDrag * dYaw / Time.deltaTime);;
            dYaw += ddYaw * Time.deltaTime;
            mainCamera.transform.RotateAround(pointer.position, mainCamera.transform.up, dYaw * Time.deltaTime);
            pointer.RotateAround(pointer.position, mainCamera.transform.up, ddYaw * Time.deltaTime);

            //Pitch
            ddPitch = pitchAxis * pitchAcc - (pitchDrag * dPitch / Time.deltaTime);;
            dPitch += ddPitch * Time.deltaTime;
            mainCamera.transform.RotateAround(pointer.position, mainCamera.transform.right, dPitch * Time.deltaTime);
            pointer.RotateAround(pointer.position, mainCamera.transform.right, ddPitch * Time.deltaTime * 0.4f);

            pointer.rotation = Quaternion.RotateTowards(pointer.rotation, pointer.parent.rotation, 0.4f);

            yield return null;
        }
    
        title.text = originalTitle;

    }


    void AddPointer(Vector3 localPosition) {
        Vector3[] verts = new Vector3[20] {
            new Vector3(0, 0, 0), //FRONT

            new Vector3(-0.3f, 0, -0.3f), //MID EDGE
            new Vector3(0.3f, 0, -0.3f), //2

            new Vector3(-0.5f, 0, -0.5f), //CORNER
            new Vector3(0.5f, 0, -0.5f), //4

            new Vector3(-0.35f, 0.02f, -0.45f), //OUTER EX
            new Vector3(0.35f, 0.02f, -0.45f), //6
            new Vector3(-0.35f, -0.02f, -0.45f), //7
            new Vector3(0.35f, -0.02f, -0.45f), //8

            new Vector3(-0.3f, 0.04f, -0.45f), //EX
            new Vector3(0.3f, 0.04f, -0.45f), //10
            new Vector3(-0.3f, -0.04f, -0.45f), //11
            new Vector3(0.3f, -0.04f, -0.45f), //12

            new Vector3(-0.25f, 0.02f, -0.45f), //INNER EX
            new Vector3(0.25f, 0.02f, -0.45f), //14
            new Vector3(-0.25f, -0.02f, -0.45f), //15
            new Vector3(0.25f, -0.02f, -0.45f), //16

            new Vector3(0f, 0f, -0.5f), //BACK
            new Vector3(-0.3f, 0, -0.5f), //18
            new Vector3(0.3f, 0, -0.5f) // 19
        };

        int[] tris = new int[] {
            0, 2, 14,
            0, 14, 17,
            0, 17, 13,
            0, 13, 1,
            0, 1, 15,
            0, 15, 17,
            0, 17, 16,
            0, 16, 2,

            1, 13, 9,
            1, 9, 5,
            1, 5, 3,
            1, 3, 7,
            1, 7, 11,
            1, 11, 15,

            2, 16, 12,
            2, 12, 8,
            2, 8, 4,
            2, 4, 6,
            2, 6, 10,
            2, 10, 14,

            3, 5, 7,
            5, 18, 7,
            7, 18, 11,
            11, 18, 15,
            15, 18, 13,
            13, 18, 9,
            9, 18, 5,
            13, 17, 15,

            4, 8, 6,
            8, 19, 6,
            6, 19, 10,
            10, 19, 14,
            14, 19, 16,
            16, 19, 12,
            12, 19, 8,
            14, 16, 17
        };

        Color[] colours = Enumerable.Repeat(Color.grey, verts.Count()).ToArray();

        GameObject pointerGO = new GameObject("Pointer");
        pointerGO.tag = "Pointer";
        pointer = pointerGO.transform;
        pointer.SetParent(Camera.main.transform);

        pointer.localPosition = localPosition;
        pointer.localScale = scale;

        MeshFilter meshFilter = pointerGO.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = pointerGO.AddComponent<MeshRenderer>();
        meshRenderer.material = PrefabManager.main.atomsMeshPrefab.GetComponent<MeshRenderer>().sharedMaterial;
        Mesh mesh = meshFilter.mesh;
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.colors = colours;
        mesh.RecalculateNormals();

        BoxCollider collider = pointerGO.AddComponent<BoxCollider>();
        collider.isTrigger = true;

        Instantiate<GameObject>(trail0Prefab, pointer).transform.localPosition = new Vector3(-0.3f, 0f, -0.5f);
        Instantiate<GameObject>(trail0Prefab, pointer).transform.localPosition = new Vector3(0.3f, 0f, -0.5f);
        Instantiate<GameObject>(trail1Prefab, pointer).transform.localPosition = new Vector3(-0.3f, 0f, -0.5f);
        Instantiate<GameObject>(trail1Prefab, pointer).transform.localPosition = new Vector3(0.3f, 0f, -0.5f);
    }

    public static void NextEligible() {
        AtomCollider nextCollider;
        main.colliderCount += 5;

        if (main.colliderDict.TryGetValue(++main.counter, out nextCollider)) {
            nextCollider.Set(true, true);
            if (main.title != null) {
                main.title.text = string.Format(
                    "{0} / {1} (Next: {2}) Count: {3}", 
                    main.counter, 
                    main.count, 
                    main.residueDict[main.counter], 
                    main.colliderCount
                );
            }
        }
    }

    void GetCollider() {

        if (colliderCount == 0) {return;}
        colliderCount--;
        string nextResidue = "";
        residueDict.TryGetValue(main.counter, out nextResidue);

        title.text = string.Format(
            "{0} / {1} (Next: {2}) Count: {3}", 
            counter, 
            count, 
            nextResidue, 
            colliderCount
        );
        
        float distance = 100f;
        Vector3 start = pointer.transform.position + Vector3.Scale(new Vector3(0, -0.05f, 0), scale);
        Vector3 end = start + pointer.transform.forward * distance;;
        RaycastHit hit;
        if (Physics.Raycast(start, pointer.transform.forward, out hit, distance)) {
            AtomCollider collider = hit.collider.gameObject.GetComponent<AtomCollider>();

            if (collider != null) {
                collider.OnTriggerEnter(hit.collider);
                end = hit.point;
            }

        }
        GameObject lineRendererGO = DrawLine(
            start, 
            end, 
            ColorScheme.GetLineGlowMaterial(),
            Color.red,
            Color.red,
            startWidth: 0.005f
        );
        lineRendererGO.transform.SetParent(pointer);
        GameObject.Destroy(lineRendererGO, 0.2f);
        
        ParticleSystem red = Instantiate<GameObject>(glowParticle, lineRendererGO.transform).GetComponent<ParticleSystem>();
        GameObject.Destroy(red.gameObject, red.main.duration);
    }

    public static IEnumerator ExitCoroutine() {
        
        Cursor.lockState = CursorLockMode.None;
        float timer = 0f;
        main.pointer.SetParent(null);
        GameObject particles = Instantiate<GameObject>(main.particlesPrefab,  main.pointer);
        main.pointer.GetComponent<MeshRenderer>().enabled = false;
        main.moving = false;
        yield return null;
        while (timer < 5f) {
            if (main == null) {
                yield break;
            }
            main.dZ *= 0.9f * Time.deltaTime;
            main.translation.z = main.dZ * Time.deltaTime + main.minSpeed;
            main.pointer.Translate(main.translation);
            timer += Time.deltaTime;
            yield return null;
        }

        main.Hide();
    }

    public static void Exit() {
        
        Cursor.lockState = CursorLockMode.None;
        //float timer = 0f;
        main.pointer.SetParent(null);
        //GameObject particles = Instantiate<GameObject>(main.particlesPrefab,  main.pointer);
        main.pointer.GetComponent<MeshRenderer>().enabled = false;
        main.moving = false;
        //yield return null;
        //while (timer < 5f) {
        //    if (main == null) {
        //        yield break;
        //    }
        //    main.dZ *= 0.9f * Time.deltaTime;
        //    main.translation.z = main.dZ * Time.deltaTime + main.minSpeed;
        //    main.pointer.Translate(main.translation);
        //    timer += Time.deltaTime;
        //    yield return null;
        //}

        main.Hide();
    }



}
