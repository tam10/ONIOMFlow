using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using Element = Constants.Element;
using EL = Constants.ErrorLevel;
using AMID = Constants.AtomModificationID;
using OLID = Constants.OniomLayerID;
using GIID = Constants.GeometryInterfaceID;
using Unity.Mathematics;

public class ResidueRepresentation : MonoBehaviour {

    private DraggableInterface draggableInterface;
    private LineDrawer lineDrawer;
    public Transform meshHolder;
    public GIID geometryInterfaceID;
    public Geometry geometry;
    private List<ResidueID> residueIDs = new List<ResidueID>();
    private ResidueID primaryResidueID;
    private AtomsMesh primaryAtomsMesh;

    private bool pointerDown;

	/// <summary>Does this ResidueRepresentation's DraggableInterface implement dragging behaviour?</summary>
    private bool draggable = true;
	/// <summary>Is this ResidueRepresentation's DraggableInterface being dragged?</summary>
    private bool isBeingDragged;
	/// <summary>How long does the pointer need to be down before considering it dragged?</summary>
    public static float timeUntilDragged = 0.1f;
    Vector3 pointerDownPosition;

    public float xyRotSensitivity = 0.2f;
    public float zRotSensitivity = 0.1f;
    public float zoomSensitivity = 2f;

    private Vector3 cubePosition;
    public float minCubeDistance = 5f;
    public float maxCubeDistance = 20f;
    public float translationTime = 1f;
    private bool translating = false;

    private List<ResidueID> visibleResidueIDs = new List<ResidueID>();
    private float3 offset = new float3();

    private Coroutine coroutine;
    
    public AtomID closestAtomID;
    private bool closestAtomEnumeratorRunning = false;
    

    public Toolbox toolbox;

    void Awake() {
        lineDrawer = PrefabManager.InstantiateLineDrawer(transform);
        cubePosition = transform.localPosition;
        cubePosition.z = Mathf.Clamp(
            cubePosition.z - Input.GetAxis("Forward") * zoomSensitivity, 
            minCubeDistance, 
            maxCubeDistance
        );
        transform.localPosition = cubePosition;
    }

    public void SetGeometry(GIID geometryInterfaceID) {
        this.geometryInterfaceID = geometryInterfaceID;
        geometry = Flow.GetGeometry(geometryInterfaceID);
        residueIDs = geometry.EnumerateResidueIDs().ToList();
    }

    public void SetRepresentationResidue(ResidueID residueID, bool translate=false) {
        Clear();
        if (coroutine != null) {
            StopCoroutine(coroutine);
        }
        ResidueTable.main.primaryResidueID = residueID;
        ResidueTable.main.UpdateResidueTableItems();
        coroutine = StartCoroutine(GetRepresentationResidueIEnumerator(residueID, translate));
    }

    public void SetRepresentationResidue(int index, bool translate=false) {

        if (index >= residueIDs.Count) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Index {0} is out of range in Residue IDs in Residue Representation with {1} Residues",
                index,
                residueIDs.Count
            );
        }
        ResidueID residueID = residueIDs[index];

        SetRepresentationResidue(residueID, translate);
    }

    IEnumerator GetRepresentationResidueIEnumerator(ResidueID residueID, bool translate=false) {
        
        Residue residue;
        if (!geometry.TryGetResidue(residueID, out residue)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Could not find Residue '{0}'! Failed to get Representation.",
                residueID
            );
            yield break;
        }

        float3 newOffset = residue.GetCentre();

        if (translate) {
            TranslateOverTime(offset + newOffset, Vector3.zero);
        } else {
            SetPosition(Vector3.zero);
        }

        offset = newOffset;
        AddResidue(residueID, primary:true);

        List<ResidueID> linkedResidueIDs = new List<ResidueID>();


        //Add bonded residues as secondaries
        foreach ((AtomID hostAtomID, AtomID linkerAtomID) in residue.NeighbouringAtomIDs()) {
            ResidueID linkedResidueID = linkerAtomID.residueID;

            //Add the neighbouring residue
            AddResidue(linkedResidueID, primary:false);
            linkedResidueIDs.Add(linkedResidueID);

            Residue residue1;
            if (!geometry.TryGetResidue(linkedResidueID, out residue1)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Could not add Linker to Representation - Could not find Residue '{0}' in Geometry!",
                    linkedResidueID
                );
                continue;
            }

            //Add the linker
            AddLinker(
                residue, 
                residue1, 
                hostAtomID.pdbID, 
                linkerAtomID.pdbID, 
                -offset,
                false
            );

            if (Timer.yieldNow) yield return null;
        }

        List<ResidueID> neabyResidueIDs = new List<ResidueID>();
        foreach (ResidueID nearbyResidueID0 in residue.ResiduesWithinDistance(8f)) {
            Residue nearbyResidue;
            if (! geometry.TryGetResidue(nearbyResidueID0, out nearbyResidue)) {
                continue;
            }

            if (!linkedResidueIDs.Contains(nearbyResidueID0)) {
                AddResidue(nearbyResidueID0, primary:false);
            }
            
            //See if we need to display a linker between nearby residues
            foreach ((AtomID hostAtomID, AtomID linkerAtomID) in nearbyResidue.NeighbouringAtomIDs()) {
                ResidueID linkerResidueID = linkerAtomID.residueID;
                
                Residue linkerResidue;
                if (! geometry.TryGetResidue(linkerResidueID, out linkerResidue)) {
                    continue;
                }
                
                foreach (ResidueID nearbyResidueID1 in neabyResidueIDs) {
                    if (linkerResidueID != nearbyResidueID1) {
                        continue;
                    }
                    
                    AddLinker(
                        nearbyResidue, 
                        linkerResidue, 
                        hostAtomID.pdbID, 
                        linkerAtomID.pdbID, 
                        -offset,
                        false
                    );
                    
                }
            }

            //Add this now so we only compare the upper triangle of a nearby residue matrix - no duplication
            neabyResidueIDs.Add(nearbyResidueID0);
            
            if (Timer.yieldNow) yield return null;
        }
    }

    public void AddResidue(ResidueID residueID, bool primary) {
        Residue residue = geometry.GetResidue(residueID);
        visibleResidueIDs.Add(residueID);
        if (primary) {
            primaryAtomsMesh = PrefabManager.InstantiateAtomsMesh(meshHolder);
            BondsMesh bondsMesh = PrefabManager.InstantiateBondsMesh(meshHolder);

            primaryAtomsMesh.SetAtoms(residue, -offset, primary);
            bondsMesh.SetBonds(residue, -offset, primary);

            primaryAtomsMesh.gameObject.name = string.Format ("{0} (Atoms)", residue.residueID);
            bondsMesh.gameObject.name = string.Format ("{0} (Bonds)", residue.residueID);

            primaryResidueID = residueID;
        } else {
            lineDrawer.AddResidue(residue, -offset);
        }
    }

    private void ClickPrimaryAtom(AtomID clickedAtomID) {
        switch (toolbox.atomModificationID) {
            case (AMID.HYDROGENS): 
                ModifyHydrogen(clickedAtomID);
                break;
            case (AMID.MODEL_ATOM):
                MoveAtomToLayer(clickedAtomID, OLID.MODEL);
                break;
            case (AMID.INT_ATOM):
                MoveAtomToLayer(clickedAtomID, OLID.INTERMEDIATE);
                break;
            case (AMID.REAL_ATOM):
                MoveAtomToLayer(clickedAtomID, OLID.REAL);
                break;
            case (AMID.MODEL_RESIDUE):
                MoveResidueToLayer(clickedAtomID, OLID.MODEL);
                break;
            case (AMID.INT_RESIDUE):
                MoveResidueToLayer(clickedAtomID, OLID.INTERMEDIATE);
                break;
            case (AMID.REAL_RESIDUE):
                MoveResidueToLayer(clickedAtomID, OLID.REAL);
                break;
            case (AMID.NULL):
                break;
        }
    }

    private void RefreshMeshResidue(AtomsMesh atomsMesh) {
        atomsMesh.Refresh();
        Residue residue = atomsMesh.residue;
        
        foreach (Transform meshTransform in meshHolder) {
            BondsMesh bondsMesh = meshTransform.GetComponent<BondsMesh>();
            if (bondsMesh != null && bondsMesh.residue == residue) {
                bondsMesh.Refresh();
            }
        }
        ResidueTable.main.primaryResidueID = residue.residueID;
        ResidueTable.main.UpdateResidueTableItems();
    }

    private void ModifyHydrogen(AtomID atomID) {

        if (atomID.pdbID.element == Element.H) {
            geometry.GetResidue(atomID.residueID).RemoveAtom(atomID);
        } else {
            geometry.GetResidue(atomID.residueID).AddProton(atomID.pdbID);
        }

        RefreshMeshResidue(primaryAtomsMesh);
    }

    private void MoveAtomToLayer(AtomID atomID, OLID layerID) {
        
        geometry.GetAtom(atomID).oniomLayer = layerID;
        RefreshMeshResidue(primaryAtomsMesh);
    }

    private void MoveResidueToLayer(AtomID atomID, OLID layerID) {
        foreach (Atom atom in geometry.GetResidue(atomID.residueID).atoms.Values) {
            atom.oniomLayer = layerID;
        }
        RefreshMeshResidue(primaryAtomsMesh);
    }

    public void AddLinker(Residue residue0, Residue residue1, PDBID pdbID0, PDBID pdbID1, Vector3 offset, bool primary) {
        LinkerMesh linkerMesh = PrefabManager.InstantiateLinkerMesh(meshHolder);

        linkerMesh.SetLinker(residue0, residue1, pdbID0, pdbID1, offset, primary);

        linkerMesh.gameObject.name = string.Format ("{0}-{1} (Linker)", residue0.residueID, residue1.residueID);
    }

    public void Clear() {
        visibleResidueIDs.Clear();
        foreach (Transform child in meshHolder) {
            GameObject.Destroy(child.gameObject);
        }
        lineDrawer.Clear();
    }

    public void MakeInteractive(DraggableInterface draggableInterface) {
        this.draggableInterface = draggableInterface;

        draggableInterface.OnPointerDownHandler = PointerDownHandler;
        draggableInterface.OnPointerUpHandler = PointerUpHandler;
        draggableInterface.OnScrollHandler = OnScrollHandler;
    }

    void PointerDownHandler(PointerEventData pointerEventData) {
        pointerDown = true;
        isBeingDragged = false;
        pointerDownPosition = Input.mousePosition;

        if (pointerEventData.button == PointerEventData.InputButton.Left) {
            StartCoroutine(DraggingTest());
        }
        //StartCoroutine(HandlePointer());
    }

    void PointerUpHandler(PointerEventData pointerEventData) {
        pointerDown = false;
        if (!isBeingDragged) {
            OnPointerClick(pointerEventData);
        }
        isBeingDragged = false;
    }

    IEnumerator DraggingTest() {
        float timer = 0f;
        while (pointerDown && timer < timeUntilDragged) {
            timer += Time.deltaTime;
            yield return null;
        }
        isBeingDragged = true;
		if (draggable) {
			StartCoroutine(EditTransform());
		}
    }

	public void OnPointerClick(PointerEventData pointerEventData) {
		if (pointerEventData.button == PointerEventData.InputButton.Left) {
            
            if (closestAtomID.residueID == primaryResidueID) {
                ClickPrimaryAtom(closestAtomID);
            } else {
                SetRepresentationResidue(closestAtomID.residueID);
            }

		} else if (pointerEventData.button == PointerEventData.InputButton.Right) {
			ShowContextMenu();
		}
	}

	/// <summary>Open the Context Menu (right clicked).</summary>
	public void ShowContextMenu() {
		ContextMenu contextMenu = ContextMenu.main;

		//Clear the Context Menu
		contextMenu.Clear();

		bool geometryEnabled = (geometry != null);

        IEnumerator CalculateConnectivity() {
            yield return Cleaner.CalculateConnectivity(geometryInterfaceID);
            SetRepresentationResidue(primaryResidueID, false);
        }

		//Add buttons and spacers
		contextMenu.AddButton(
            () => {StartCoroutine(CalculateConnectivity());}, 
            "Compute Connectivity", 
            geometryEnabled
        );

		contextMenu.AddSpacer();

        void ButtonPressed(AMID atomModificationID) {
            toolbox.ButtonPressed(toolbox.GetAtomModificationButton(atomModificationID));
            contextMenu.Hide();
        }

        ContextButtonGroup editGroup = contextMenu.AddButtonGroup("Edit Mode", true);
        editGroup.AddButton(() => ButtonPressed(AMID.HYDROGENS), "Hydrogens", true);
        editGroup.AddSpacer();
        editGroup.AddButton(() => ButtonPressed(AMID.MODEL_ATOM), "Atom -> Model", true);
        editGroup.AddButton(() => ButtonPressed(AMID.INT_ATOM), "Atom -> Intermediate", true);
        editGroup.AddButton(() => ButtonPressed(AMID.REAL_ATOM), "Atom -> Real", true);
        editGroup.AddSpacer();
        editGroup.AddButton(() => ButtonPressed(AMID.MODEL_RESIDUE), "Residue -> Model", true);
        editGroup.AddButton(() => ButtonPressed(AMID.INT_RESIDUE), "Residue -> Intermediate", true);
        editGroup.AddButton(() => ButtonPressed(AMID.REAL_RESIDUE), "Residue -> Real", true);

		contextMenu.AddSpacer();

		contextMenu.AddButton(() => ResidueTable.main.Hide(), "Exit", true);
		

		//Show the Context Menu
		contextMenu.Show();
	}

    //IEnumerator AtomClicked(PointerEventData pointerEventData) {
    //    
    //    Ray ray = Camera.main.ScreenPointToRay(pointerEventData.pressPosition);
    //    SortedDictionary<float, AtomID> hitAtoms = new SortedDictionary<float, AtomID>();
    //    foreach (ResidueID residueID in visibleResidueIDs) {
    //        foreach ((PDBID pdbID, Atom atom) in geometry.GetResidue(residueID).atoms) {
    //            Vector3 position = transform.TransformPoint(atom.position + offset);
    //            float distance = Vector3.Cross(ray.direction, position - ray.origin).magnitude;
    //            if (distance < Settings.GetAtomRadiusFromElement(pdbID.element)) {
    //                
    //                hitAtoms[distance] = new AtomID(residueID, pdbID);
    //            }

    //            if (Timer.yieldNow) {yield return null;}
    //        }
    //    }

    //    
    //    if (hitAtoms.Count > 0) {
    //        AtomID selectedAtomID = hitAtoms.First().Value;
    //        if (selectedAtomID.residueID == primaryResidueID) {
    //            ClickPrimaryAtom(selectedAtomID);
    //        } else {
    //            SetRepresentationResidue(selectedAtomID.residueID);
    //        }
    //        
    //    }

    //}







    IEnumerator GetClosestAtomID(Vector3 mousePosition) {

        closestAtomEnumeratorRunning = true;
        yield return null;
        
        Ray ray = Camera.main.ScreenPointToRay(mousePosition);
        AtomID bestAtomID = AtomID.Empty;
        float bestDistanceSq = 10f;

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
                    distanceSq < CustomMathematics.Squared(Settings.GetAtomRadiusFromElement(atomID.pdbID.element))
                ) {
                    bestDistanceSq = distanceSq;
                    bestAtomID = atomID;
                }
            }

            if (Timer.yieldNow) {yield return null;}
        }

        closestAtomID = bestAtomID;
        closestAtomEnumeratorRunning = false;
    }

    IEnumerator EditTransform() {
        float yRotation = 0f;
        float xRotation = 0f;
        float zRotation = 0f;
        float3 screenCentre = new Vector3(Screen.width / 2f, Screen.height / 2f);
        float3 oldMousePosition = Input.mousePosition;
        while (pointerDown) {
            
            float3 mousePosition = (float3)Input.mousePosition;
            float3 mouseDelta = mousePosition - oldMousePosition;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
                Vector3 torque = Vector3.Cross(
                    (mousePosition - screenCentre),
                    mouseDelta
                );
                zRotation = torque.z * zRotSensitivity / math.length(screenCentre);

                transform.Rotate(Vector3.forward, zRotation, Space.World);

            } else {
                
                yRotation = mouseDelta.x * xyRotSensitivity;
                xRotation = mouseDelta.y * xyRotSensitivity;
                
                transform.Rotate(Vector3.down, yRotation, Space.World);
                transform.Rotate(Vector3.right, xRotation, Space.World);
            }

            oldMousePosition = mousePosition;

            yield return null;
        }

        //Continue spinning until cursor is pressed
        if (xRotation != 0f || yRotation != 0f || zRotation != 0f) {
            while (!pointerDown) {
                transform.Rotate(Vector3.forward, zRotation, Space.World);
                transform.Rotate(Vector3.down,    yRotation, Space.World);
                transform.Rotate(Vector3.right,   xRotation, Space.World);
                yield return null;
            }
        }
    }

    void OnScrollHandler(PointerEventData pointerEventData) {
        if (translating) return;
        cubePosition.z = Mathf.Clamp(
            cubePosition.z - Input.GetAxis("Forward") * zoomSensitivity, 
            minCubeDistance, 
            maxCubeDistance
        );
        transform.localPosition = cubePosition;
    }
    

    public void TranslateOverTime(Vector3 start, Vector3 target) {
        StartCoroutine(TranslateOverTimeIEnumerator(start, target));
    }

    IEnumerator TranslateOverTimeIEnumerator(Vector3 start, Vector3 target) {
        translating = true;
        float timer = translationTime;
        meshHolder.localPosition = start;
        lineDrawer.transform.localPosition = start;
        yield return null;

        while (timer > 0f) {
            SetPosition(Vector3.Lerp(target, start, timer / translationTime));
            timer -= Time.deltaTime;
            yield return null;
        }

        translating = false;
        SetPosition(target);
        yield return null;

    }

    void SetPosition(Vector3 target) {
        meshHolder.localPosition = target;
        lineDrawer.transform.localPosition = target;

    }

    void Update() {
        if (!closestAtomEnumeratorRunning) {
            StartCoroutine(GetClosestAtomID(Input.mousePosition));
        }

    }
}
