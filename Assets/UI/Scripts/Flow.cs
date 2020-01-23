using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using GIID = Constants.GeometryInterfaceID;
using GIS = Constants.GeometryInterfaceStatus;
using EL = Constants.ErrorLevel;
using Unity.Mathematics;
using System.Xml.Linq;
using System.IO;
using System;

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

	public static IEnumerator SaveState() {
		string stateDirectory = Path.Combine(
			Settings.projectPath,
			DateTime.Now.ToString(".yyyy-MM-ddTHH.mm.ss")
		);

		if (!Directory.Exists(stateDirectory)) {
			Directory.CreateDirectory(stateDirectory);
			DirectoryInfo directoryInfo = new DirectoryInfo(stateDirectory);
			directoryInfo.Attributes |= FileAttributes.Hidden;
		}

		foreach ((GIID geometryInterfaceID, GeometryInterface geometryInterface) in main.geometryDict) {
			Geometry geometry = geometryInterface.geometry;


			if (geometry != null) {
				string fileName = string.Format("{0}.xat", geometryInterface.id);
				string path = Path.Combine(stateDirectory, fileName);
				yield return FileWriter.WriteFile(geometry, path, true);
			}
		}
	}

	public static IEnumerator LoadState() {

		string[] directories = Directory.GetDirectories(
			Settings.projectPath,
			".*",
			searchOption:SearchOption.TopDirectoryOnly
		);

		List<string> stateDirectories = new List<string>();

		FileSelector fileSelector = FileSelector.main;
		fileSelector.saveMode = true;
		fileSelector.cancelled = false;
		fileSelector.SetFileTypes(new List<string>());

		while (fileSelector.isBusy) {
			yield return null;
		}

		fileSelector.Clear();

		fileSelector.SetPromptText("Load a previously saved State");

		Dictionary<string, string> timeStampToDirectory = new Dictionary<string, string>();

		foreach (string path in directories) {
			string directory = Path.GetFileName(path);
			Debug.Log(directory);
			DateTime timeStamp;
			try {
				timeStamp = DateTime.Parse(directory.TrimStart(new char[] {'.'}).Replace(".", ":"));
			} catch {
				Debug.Log(directory.TrimStart(new char[] {'.'}).Replace(".", ":"));
				continue;
			}
			string timeStampString = timeStamp.ToString();
			timeStampToDirectory[timeStampString] = directory;
			stateDirectories.Add(directory);
			fileSelector.AddItem(directory, true, true, timeStampString);
		}

		while (!fileSelector.userResponded) {
			yield return null;
		}

		fileSelector.Hide();
		if (fileSelector.cancelled) {
			yield break;
		}

		string statePath = fileSelector.confirmedText;

		yield return null;

		if (!Directory.Exists(statePath)) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Directory does not exist: {0}",
				statePath
			);
			yield break;
		}

		foreach ((GIID geometryInterfaceID, GeometryInterface geometryInterface) in main.geometryDict) {

			string fileName = string.Format("{0}.xat", geometryInterface.id);
			string path = Path.Combine(statePath, fileName);
			if (File.Exists(path)) {
				geometryInterface.InitialiseEmptyGeometry();
				yield return FileReader.LoadGeometry(geometryInterface.geometry, path);
				yield return geometryInterface.CheckAll();
			}
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
		if (eventData.button == PointerEventData.InputButton.Right) {
			ShowContextMenu();
		}
	}

	void ShowContextMenu() {
		ContextMenu contextMenu = ContextMenu.main;

		//Clear the Context Menu
		contextMenu.Clear();
        
		contextMenu.AddButton(
            () => {StartCoroutine(SaveState());}, 
            "Save State", 
            true
        );
		contextMenu.AddButton(
			() => {StartCoroutine(LoadState());},
			"Load State",
			true
		);

		//Show the Context Menu
		contextMenu.Show();
	}


}
