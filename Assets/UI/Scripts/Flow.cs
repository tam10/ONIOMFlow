using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using GIID = Constants.GeometryInterfaceID;
using GIS = Constants.GeometryInterfaceStatus;
using EL = Constants.ErrorLevel;
using System.Text;
using System.Xml.Linq;
using System.Linq;
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

		if (main.geometryDict.All(x => x.Value.geometry == null)) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Cannot save empty State!"
			);
		}

		FileSelector fileSelector = FileSelector.main;
		while (fileSelector.isBusy) {
			yield return null;
		}

		string currentDirectory = Settings.currentDirectory;
		float oldAlpha = fileSelector.fullPathText.alpha;

		void CleanUp() {
			Settings.currentDirectory = currentDirectory;
			fileSelector.fullPathText.alpha = oldAlpha;
			fileSelector.fileNameInput.text = "";
		}

		Settings.currentDirectory = Settings.projectPath;

		string[] directories = Directory.GetDirectories(
			Settings.projectPath,
			".*",
			searchOption:SearchOption.TopDirectoryOnly
		);



		fileSelector.saveMode = true;
		fileSelector.cancelled = false;
		fileSelector.SetFileTypes(new List<string>());

		fileSelector.Show();

		fileSelector.Clear();

		fileSelector.fullPathText.text = Settings.projectPath;

		fileSelector.fullPathText.alpha = 0;
		fileSelector.fileNameInput.text = "";
		fileSelector.SetPromptText("Select Save State name:");

		foreach (string directory in directories) {
			try {
				string name = Path.GetFileName(directory).Replace(".", "");
				FlowState state = FlowState.FromName(name);

				fileSelector.AddItem(name, true, true, name);
			} catch (System.Exception e){
				CustomLogger.LogFormat(
					EL.VERBOSE,
					"Skipping invalid directory {0} in Save State: {1}",
					directory,
					e.Message
				);
				continue;
			}
		}

		while (!fileSelector.userResponded) {
			yield return null;
		}

		fileSelector.Hide();
		if (fileSelector.cancelled) {
			CleanUp();
			yield break;
		}

		FlowState flowState = FlowState.FromFlow(
			Path.GetFileNameWithoutExtension(
				fileSelector.confirmedText
					.Replace(".", "")
					.Replace(":", "")
			)
		);

		CleanUp();
		yield return flowState.Save();
	}

	public static IEnumerator LoadState() {

		Settings.currentDirectory = Settings.projectPath;

		string[] directories = Directory.GetDirectories(
			Settings.projectPath,
			".*",
			searchOption:SearchOption.TopDirectoryOnly
		);

		FileSelector fileSelector = FileSelector.main;
		while (fileSelector.isBusy) {
			yield return null;
		}

		string currentDirectory = Settings.currentDirectory;
		float oldAlpha = fileSelector.fullPathText.alpha;

		void CleanUp() {
			Settings.currentDirectory = currentDirectory;
			fileSelector.fullPathText.alpha = oldAlpha;
			fileSelector.fileNameInput.text = "";
		}

		fileSelector.saveMode = true;
		fileSelector.cancelled = false;
		fileSelector.SetFileTypes(new List<string>());

		fileSelector.Show();

		fileSelector.Clear();

		fileSelector.fullPathText.alpha = 0;
		fileSelector.fileNameInput.text = "";
		fileSelector.SetPromptText("Load a previously saved State:");

		Dictionary<string, string> timeStampToDirectory = new Dictionary<string, string>();

		foreach (string directory in directories) {
			try {
				string name = Path.GetFileName(directory).Replace(".", "");
				FlowState state = FlowState.FromName(name);

				fileSelector.AddItem(name, true, true, name);
			} catch (System.Exception e){
				CustomLogger.LogFormat(
					EL.VERBOSE,
					"Skipping invalid directory {0} in Load State: {1}",
					directory,
					e.Message
				);
				continue;
			}
		}

		while (!fileSelector.userResponded) {
			yield return null;
		}

		fileSelector.Hide();
		if (fileSelector.cancelled) {
			Settings.currentDirectory = currentDirectory;
			fileSelector.fileNameInput.text = "";
			yield break;
		}

		string stateName = Path.GetFileNameWithoutExtension(
			fileSelector.confirmedText
				.Replace(".", "")
				.Replace(":", "")
		);

		yield return null;

		FlowState flowState;
		try {
			flowState = FlowState.FromName(stateName);

		} catch (System.Exception e) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Unable to load State '{0}': {1}",
				stateName,
				e.Message
			);
			CleanUp();
			yield break;
		}

		CleanUp();
		yield return flowState.Load();
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
            ! main.geometryDict.All(x => x.Value.geometry == null)
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

public struct FlowState {

	public DateTime timeStamp;
	public string path;
	public string name;
	public Dictionary<GIID, string> geometryNames;

	public static FlowState FromName(string name) {

		FlowState flowState = new FlowState();
		if (string.IsNullOrEmpty(name)) {
			throw new ArgumentException("State Name argument cannot be null or empty!");
		}
		flowState.name = name;

		//Find State File
		flowState.path = Path.Combine(Settings.projectPath, "." + name);
		string stateFile = Directory.GetFiles(flowState.path)
			.FirstOrDefault(x => Path.GetFileName(x) == ".state");
			
		if (string.IsNullOrEmpty(stateFile)) {
			throw new Exception("Directory has no state file!");
		}

		//Open State File
		string[] lines = FileIO.Readlines(stateFile);

		//Get Time Stamp from file
		if (!DateTime.TryParse(lines.FirstOrDefault().Replace(".", ":"), out flowState.timeStamp)) {
			throw new Exception("Invalid Timestamp in state file!");
		}

		//Initialise dictionary of GIID to file path
		flowState.geometryNames = Flow.main.geometryDict
			.ToDictionary(x => x.Key, x => "");

		//Loop through State File skipping first line
		foreach ((string line, int lineNumber) in lines.Select((x,i) => (x,i)).Skip(1)) {
			string[] splitLine = line.Split(new char[] {':'}, 2);

			//Get GIID
			GIID geometryInterfaceID;
			if (!Constants.GeometryInterfaceIDMap.TryGetValue(splitLine[0], out geometryInterfaceID)) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Failed to get Geometry Interface ID from string '{0}'. state file {1}:{2}",
					splitLine[0],
					stateFile,
					lineNumber + 1
				);
				continue;
			}

			string geometryPath = Path.Combine(
				flowState.path,
				geometryInterfaceID.ToString() + ".xat"
			);
			if (!File.Exists(geometryPath)) {
				CustomLogger.LogFormat(
					EL.WARNING,
					"Failed to get Geometry from Path '{0}'. state file {1}:{2}",
					geometryPath,
					stateFile,
					lineNumber + 1
				);
				continue;

			}

			//Get Name
			string geometryName = Path.Combine(flowState.path, splitLine[1]);

			flowState.geometryNames[geometryInterfaceID] = geometryName;
		}
		
		return flowState;
	}


	public static FlowState FromFlow(string name) {

		FlowState flowState = new FlowState();
		if (string.IsNullOrEmpty(name)) {
			throw new ArgumentException("State Name argument cannot be null or empty!");
		}
		flowState.name = name;

		flowState.timeStamp = DateTime.Now;

		flowState.path = Path.Combine(Settings.projectPath, "." + name);

		flowState.geometryNames = Flow.main.geometryDict
			.ToDictionary(
				x => x.Key, 
				x => x.Value.geometry != null 
					 ? x.Value.geometry.name
					 : ""
			);

		return flowState;

	}

	public IEnumerator Load() {

		if (geometryNames == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Cannot load state - Geometry Paths Dictionary is null!"
			);
		}

		foreach ((GIID geometryInterfaceID, string geometryName) in geometryNames) {

			if (string.IsNullOrEmpty(geometryName)) {
				continue;
			}

			GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);

			string geometryPath = Path.Combine(
				path,
				geometryInterfaceID.ToString() + ".xat"
			);

			if (File.Exists(geometryPath)) {
				geometryInterface.InitialiseEmptyGeometry();
				yield return FileReader.LoadGeometry(geometryInterface.geometry, geometryPath);
				yield return geometryInterface.CheckAll();
			} else {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Geometry '{0}' not found in path: {1}",
					Constants.GeometryInterfaceIDMap[geometryInterfaceID],
					geometryPath
				);
			}

			if (Timer.yieldNow) {
				yield return null;
			}
		}
	}

	public IEnumerator Save() {

		if (geometryNames == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Cannot save state - Geometry Paths Dictionary is null!"
			);
		}

		if (!Directory.Exists(path)) {
			Directory.CreateDirectory(path);
			DirectoryInfo directoryInfo = new DirectoryInfo(path);
			directoryInfo.Attributes |= FileAttributes.Hidden;
		}

		StringBuilder mapping = new StringBuilder();
		mapping.AppendLine(timeStamp.ToString("yyyy-MM-ddTHH.mm.ss"));

		foreach ((GIID geometryInterfaceID, string geometryName) in geometryNames) {

			if (string.IsNullOrEmpty(geometryName)) {
				continue;
			}

			string geometryPath = Path.Combine(
				path,
				geometryInterfaceID.ToString() + ".xat"
			);
			if (string.IsNullOrEmpty(geometryPath)) {
				continue;
			}

			Geometry geometry = Flow.GetGeometry(geometryInterfaceID);

			FileWriter fileWriter;
			try {
				fileWriter = new FileWriter(geometry, geometryPath, true);
			} catch (System.ArgumentException e) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Failed to save state! {0}",
					e.Message
				);
				yield break;
			}
			yield return fileWriter.WriteFile();

			mapping.AppendLine(
				string.Join(
					":",
			 		Constants.GeometryInterfaceIDMap[geometryInterfaceID],
					geometryPath
				)
			);

			if (Timer.yieldNow) {
				yield return null;
			}
		}

		string stateFilePath = Path.Combine(path, ".state");
		File.WriteAllText(stateFilePath, mapping.ToString());

	}

}