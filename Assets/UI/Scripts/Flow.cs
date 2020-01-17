using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using GIID = Constants.GeometryInterfaceID;
using GIS = Constants.GeometryInterfaceStatus;
using EL = Constants.ErrorLevel;
using Unity.Mathematics;
using System.Xml.Linq;

/// <summary>The Flow Singleton Class</summary>
/// 
/// <remarks>
/// Handles application opening and closing
/// Contains references to Geometry Interfaces
/// Handles interactions with the interface (scrolling and zooming)
/// </remarks>
public class Flow : 
	MonoBehaviour, 
	IPointerClickHandler,
	IScrollHandler, 
	IDragHandler {

    /// <summary>The Flow Singleton instance.</summary>
	private static Flow _main;
	/// <summary>Gets the reference to the Flow Singleton.</summary>
	public static Flow main {
		get {
			if (_main == null) _main = GameObject.FindObjectOfType<Flow>();
			return _main;
		}
	}

	/// <summary>The RectTransform that contains GeometryInterfaces and Arrows.</summary>
	public RectTransform contentRectTransform;

	/// <summary>The Canvas to draw the Flow UI onto. Disable to hide.</summary>
	public Canvas canvas;

	/// <summary>The coordinates of the cube that the Flow UI is limited to.</summary>
	public Vector3 boundaryMin;
	/// <summary> The coordinates of the cube that the Flow UI is limited to.</summary>
	public Vector3 boundaryMax;


	/// <summary>How fast should the UI zoom when scrolled on?</summary>
	public float zoomSensitivity;


	/// <summary>The current position of the content in the cube.</summary>
	public Vector3 contentPosition;


	public static List<string> loadTypes = new List<string>();
	public static List<string> saveTypes = new List<string>();

	

	/// <summary>This is the gameObject that is created when a GI is dragged.</summary>
	public ListItem draggedGIItem;

	/// <summary>Dictionary containing refereces to the Geometry Interfaces, using their GeometryInterfaceID as a key.</summary>
	public Dictionary<GIID, GeometryInterface> geometryDict = new Dictionary<GIID, GeometryInterface>();

	//This is the calculated offset to define where arrows start and finish 
	//if directly connected to geometry interfaces. 
	//It is added to geometryArrowPadding which is user-defined.
	/// <summary>How far away an arrow should be drawn from the centre of a Geometry Interface.</summary>
	public static Vector3 geometryArrowOffset;
	/// <summary>How far away an arrow should be drawn from the edge of a Geometry Interface.</summary>
	public static Vector2 geometryArrowPadding = new Vector2(2f, 2f); 

	/// <summary>Populates the Flow UI from an XML Element.</summary>
	/// <param name="flowX">An XML Element containing Flow settings</param>
	public static void FromXML() {
		
		XDocument sX = FileIO.ReadXML (Settings.flowSettingsPath);
		XElement flowX = sX.Element ("flow");

		loadTypes = FileIO.ParseXMLStringList(flowX, "loadTypes", "loadType");
		saveTypes = FileIO.ParseXMLStringList(flowX, "saveTypes", "saveType");
		
		// Get the RectTransform of the Geometry Interface prefab
		RectTransform geometryInterfacePrefabRect = PrefabManager.main.geometryInterfacePrefab.GetComponent<RectTransform>();
		// Calculate geometryArrowOffset using the dimensions of the RectTransform 
		geometryArrowOffset = new Vector3(
			geometryInterfacePrefabRect.rect.width / 2f + geometryArrowPadding.x,
			geometryInterfacePrefabRect.rect.height / 2f + geometryArrowPadding.y,
			0f
		);

		//Create a GeometryInterface for each <geometryInterface> tag
		foreach (XElement geometryInterfaceX in flowX.Elements("geometryInterface")) {
			GeometryInterface geometryInterface = GeometryInterface.FromXML(geometryInterfaceX, main.contentRectTransform);
			main.geometryDict[geometryInterface.id] = geometryInterface;
		}

		//Create an arrow for each <connection> tag
		foreach (XElement connectionX in flowX.Elements("connection")) {
			Arrow.FromXML(connectionX, main.contentRectTransform);
		}
	}

	/// <summary>Return the reference to a Geometry Interface using a GeometryInterfaceID.</summary>
	public static GeometryInterface GetGeometryInterface(GIID geometryInterfaceID) {
		GeometryInterface geometryInterface;
		if (!main.geometryDict.TryGetValue(geometryInterfaceID, out geometryInterface)) {
			return null;
		}
		return geometryInterface;
	}

	/// <summary>Return the reference to a Geometry Interface's Geometry using a GeometryInterfaceID.</summary>
	public static Geometry GetGeometry(GIID geometryInterfaceID) {

		//Check Geometry Interface
		GeometryInterface geometryInterface = GetGeometryInterface(geometryInterfaceID);
		if (geometryInterface == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Geometry Interface {0} is null.",
				geometryInterfaceID
			);

			return null;
		}

		//Check Geometry
		if (geometryInterface.geometry == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Geometry of Geometry Interface {0} ({1}) is null.",
				geometryInterfaceID,
				geometryInterface.fullName
			);

			return null;
		}

		//Return Geometry
		return geometryInterface.geometry;
	}

	
	/// <summary>Copy the Atoms from one Geometry Interface to another.</summary>
	public static IEnumerator CopyGeometry(GIID copyFrom, GIID copyTo) {

		//Check CopyFrom GI exists
		GeometryInterface copyFromGI = GetGeometryInterface(copyFrom);
		if (copyFromGI == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Failed to copy Geometry from Geometry Interface {0} to Geometry Interface {1} - {0} is null.",
				copyFrom,
				copyTo
			);
		}

		//Check CopyTo GI exists
		GeometryInterface copyToGI = GetGeometryInterface(copyTo);
		if (copyToGI == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Failed to copy Geometry from Geometry Interface {0} ({1}) to Geometry Interface {2} - {2} is null.",
				copyFrom,
				copyFromGI,
				copyTo
			);
		}

		//Check CopyFrom Geometry exists
		if (copyFromGI.geometry == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Failed to copy Geometry from Geometry Interface {0} ({1}) to Geometry Interface {2} - Geometry of {0} is null.",
				copyFrom,
				copyFromGI,
				copyTo
			);
		}

		CustomLogger.LogFormat(
			EL.INFO,
			"Copying Geometry {0} ({1}) to {2} ({3})",
			copyFrom,
			copyFromGI.fullName,
			copyTo,
			copyToGI.fullName
		);

		//Copy geometry here
		copyToGI.status = GIS.OK;
		yield return copyToGI.SetGeometry(copyFromGI.geometry.Take(copyToGI.transform));
	}

	private float _x;
	/// <summary>
	/// Gets/Sets the x position of contentRect.
	/// </summary>
	///
	/// <remarks>
	/// Value is clamped by boundaryMin.x and boundaryMax.x
	/// </remarks>
	private float x {
		get {return _x;}
		set {
			_x = Mathf.Clamp(value, boundaryMin.x, boundaryMax.x);
			contentPosition.x = _x;
		}
	}

	private float _y;
	/// <summary>
	/// Gets/Sets the y position of contentRect.
	/// </summary>
	///
	/// <remarks>
	/// Value is clamped by boundaryMin.y and boundaryMax.y
	/// </remarks>
	private float y {
		get {return _y;}
		set {
			_y = Mathf.Clamp(value, boundaryMin.y, boundaryMax.y);
			contentPosition.y = _y;
		}
	}

	private float _z;
	/// <summary>
	/// Gets/Sets the z position of contentRect.
	/// </summary>
	///
	/// <remarks>
	/// Value is clamped by boundaryMin.z and boundaryMax.z
	/// x and y position are also modified based on the position of the cursor.
	/// </remarks>
	private float z {
		get {return _z;}
		set {
			_z = Mathf.Clamp(value, boundaryMin.z, boundaryMax.z);
			// The setter for z is more involved
			// When scrolling, the aim is to give the effect of zooming into where the cursor is currently
			// This gives the UI a more familiar feel

			// Get the local scale (which is multiplied after getting the z value)
			// This makes the zoom effect independent on the local scale
			float s = transform.localScale.z;

			// Get the position of the camera (not affected by local scale!)
			float cameraZ = Camera.main.transform.position.z;

			// Calculate the current distance from the camera to the content
			float contentDistance = transform.position.z + contentPosition.z * s - cameraZ;

			// Calculate the new distance from the camera to the content
			float newDistance = transform.position.z + _z * s - cameraZ;

			// This ratio is how much x and y need to move with respect to the cursor position
			float ratio = 1f - (newDistance) / (contentDistance);
			
			// Change x and y
			x -= ratio * (Input.mousePosition.x - Screen.width / 2f);
			y -= ratio * (Input.mousePosition.y - Screen.height / 2f);

			// Set the content position
			contentPosition.z = _z;
		}
	}

	/// <summary>
	/// Called on Instantiation.
	/// </summary>
	/// <remarks>
	/// Called by Unity
	/// </remarks>
	void Awake() {
		//Prevent further instantiation
		if (!ReferenceEquals(_main, null) && !ReferenceEquals(_main, this)) {
			CustomLogger.Log(
				EL.WARNING,
				"Tried to instantiate more than one Flow - only one is allowed!"
			);
			Destroy(gameObject);
			return;
		}

		//Load from the XML file
		FromXML();
		
		//Show the canvas
		Show();



		// Set the x, y and z values - this will also clamp the initial position if it's been set badly in the Unity Inspector
		x = contentPosition.x;
		y = contentPosition.y;
		z = contentPosition.z;
		contentRectTransform.localPosition = contentPosition;
	}

	/// <summary>Make the UI visible by enabling the canvas</summary>
	void Show() {
		canvas.enabled = true;
	}


	/// <summary>Hide the UI by disabling the canvas.</summary>
	void Hide() {
		canvas.enabled = false;
	}

	/// <summary>
	/// Called when the background is dragged with the cursor.
	/// Move the x and y components of contentRect.</summary>
	/// <remarks>Called by Unity</remarks>
	public void OnDrag(PointerEventData eventData) {
		// Move x and y based on the drag delta.
		x += eventData.delta.x;
		y += eventData.delta.y;
		contentRectTransform.localPosition = contentPosition;
	}

	/// <summary>
	/// Called when the background is scrolled.
	/// Move the z component of contentRect.
	/// Also changes the x and y components of contentRect.
	/// </summary>
	/// <remarks>Called by Unity</remarks>
	public void OnScroll(PointerEventData eventData) {
		z -= Input.GetAxis("Forward") * zoomSensitivity;
		contentRectTransform.localPosition = contentPosition;
	}

	/// <summary>Called when the background is clicked on.</summary>
	/// <remarks>Called by Unity</remarks>
	public void OnPointerClick(PointerEventData eventData) {
		//StartCoroutine(TEST());
	}

	//private IEnumerator TEST() {
//
	//	Graph graph = new Graph();
	//	Atoms testAtoms = PrefabManager.InstantiateAtoms(null);
	//	ResidueID tID = new ResidueID("A", 1);
	//	Residue testRes = new Residue(tID, "TST", testAtoms);
	//	PDBID c1 = new PDBID("C", "", 1);
	//	PDBID c2 = new PDBID("C", "", 2);
	//	PDBID c3 = new PDBID("C", "", 3);
	//	PDBID c4 = new PDBID("C", "", 4);
	//	Atom atom1 = new Atom( new float[3] {-0.5f, 0, 1}, tID, "CT");
	//	Atom atom2 = new Atom( new float[3] {-0.5f, 0, 0}, tID, "CT");
	//	Atom atom3 = new Atom( new float[3] { 0.5f, 0, 0}, tID, "CT");
	//	Atom atom4 = new Atom( new float[3] { 0.5f, 1, 0}, tID, "CT");
//
	//	testRes.AddAtom(c1, atom1);
	//	testRes.AddAtom(c2, atom2);
	//	testRes.AddAtom(c3, atom3);
	//	testRes.AddAtom(c4, atom4);
//
	//	CustomLogger.LogOutput(
	//		string.Format(
	//			"{0} {1}",
    //            CustomMathematics.GetAngle(atom1, atom2, atom3) * Mathf.Rad2Deg,
	//			CustomMathematics.GetAngle(atom2, atom3, atom4) * Mathf.Rad2Deg
	//		)
	//	);
//
	//	testAtoms.SetResidue(tID, testRes);
	//	testAtoms.Connect(new AtomID(tID, c1), new AtomID(tID, c2), Constants.BondType.SINGLE);
	//	testAtoms.Connect(new AtomID(tID, c2), new AtomID(tID, c3), Constants.BondType.SINGLE);
	//	testAtoms.Connect(new AtomID(tID, c3), new AtomID(tID, c4), Constants.BondType.SINGLE);
//
	//	yield return graph.SetAtoms(testAtoms, new List<ResidueID> {tID});
//
	//	CustomLogger.LogOutput(
	//		"Stretch: " + string.Join(", ", graph.stretches.Select(
	//			x => x.atom0.amber + x.index0.ToString() + " " + 
	//				 x.atom1.amber + x.index1.ToString()
	//		))
	//	);
//
	//	CustomLogger.LogOutput(
	//		"Bend: " + string.Join(", ", graph.bends.Select(
	//			x => x.atom0.amber + x.index0.ToString() + " " + 
	//				 x.atom1.amber + x.index1.ToString() + " " + 
	//				 x.atom2.amber + x.index2.ToString()
	//		))
	//	);
//
	//	CustomLogger.LogOutput(
	//		"Torsions: " + string.Join(", ", graph.torsions.Select(
	//			x => x.atom0.amber + x.index0.ToString() + " " + 
	//				 x.atom1.amber + x.index1.ToString() + " " + 
	//				 x.atom2.amber + x.index2.ToString() + " " + 
	//				 x.atom3.amber + x.index3.ToString()
	//		))
	//	);
//
	//	CustomLogger.LogOutput(
	//		"Impropers: " + string.Join(", ", graph.impropers.Select(
	//			x => x.atom0.amber + x.index0.ToString() + " " + 
	//				 x.atom1.amber + x.index1.ToString() + " " + 
	//				 x.atom2.amber + x.index2.ToString() + " " + 
	//				 x.atom3.amber + x.index3.ToString()
	//		))
	//	);
//
	//	CustomLogger.LogOutput(
	//		"Non-Bonding: " + string.Join(", ", graph.nonBondings.Select(
	//			x => x.atom0.amber + x.index0.ToString() + " " + 
	//				 x.atom1.amber + x.index1.ToString()
	//		))
	//	);
//
	//	CustomLogger.LogOutput( 
	//		string.Format(
	//			"Stretches: \n{0}\nBends: \n{1}\nDihedrals: \n{2}\nNon-Bondings: \n{3}\n",
	//			CustomMathematics.ToString(graph.ComputeForces(true, false, false, false)),
	//			CustomMathematics.ToString(graph.ComputeForces(false, true, false, false)),
	//			CustomMathematics.ToString(graph.ComputeForces(false, false, true, false)),
	//			CustomMathematics.ToString(graph.ComputeForces(false, false, false, true))
	//		)
	//	);
//
	//	yield return FileWriter.WriteFile(testAtoms, Path.Combine(Settings.projectPath, "test_in.pdb"), true);
	//	float3[] forces;
	//	for (int i=0;i<1000;i++){
	//		forces = graph.ComputeForces(false, false, true, false);
	//		graph.TakeStep(forces, 10f, 0.2f);
	//		CustomLogger.LogOutput(
	//			string.Join(
	//				" ",
	//				testRes.atoms.Select(x => CustomMathematics.ToString(x.Value.position))
	//			)
	//		);
	//	}
	//	yield return FileWriter.WriteFile(testAtoms, Path.Combine(Settings.projectPath, "test_out.pdb"), true);
	//}

	/// <summary>
	/// Called every frame.
	/// Currently listens for ESC and will quit the application
	/// </summary>
	/// <remarks>Called by Unity</remarks>
	//void Update() {
	//	if (Input.GetKeyDown(KeyCode.Escape)) {
	//		#if UNITY_EDITOR
	//			UnityEditor.EditorApplication.isPlaying = false;
	//		#else
	//			Application.Quit();
	//		#endif
	//	}
	//}
}
