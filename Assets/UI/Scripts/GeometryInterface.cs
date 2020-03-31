using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.IO;
using TMPro;
using System;
using System.Linq;
using System.Xml.Linq;
using GIS = Constants.GeometryInterfaceStatus;
using GIID = Constants.GeometryInterfaceID;
using RCID = Constants.ResidueCheckerID;
using ACID = Constants.AtomCheckerID;
using GICB = Constants.GeometryInterfaceCallbackID;
using TID = Constants.TaskID;
using EL = Constants.ErrorLevel;

/// <summary>The Geometry Interface Monobehaviour Class</summary>
/// 
/// <remarks>
/// Derives from Monobehaviour - follows Instantiation pattern
/// Instantiate using PrefabManager
/// Contains a reference to a Geometry object
/// Provides interfaces (load, save, check, copy etc) to its Geometry object
/// Contains references to Arrows that link it to other Geometry Interfaces through Tasks
/// </remarks>
public class GeometryInterface : 
	MonoBehaviour, 
	IPointerDownHandler, 
	IPointerEnterHandler,
	IPointerExitHandler,
	IPointerUpHandler,
	IDragHandler {

	/// <summary>The unique ID of this Geometry Interface.</summary>
	public GIID id;

	/// <summary>The full name of this Geometry Interface.</summary>
	public string fullName;

	/// <summary>The reference to the Geometry that belongs to this Geometry Interface.</summary>
	public Geometry geometry;

	/// <summary>The reference to the GeometryChecker that belong to this Geometry Interface.</summary>
	public GeometryChecker checker;

	/// <summary>A list of the atomCheckerIDs to use to check the Geometry of this Geometry Interface.</summary>
	private List<ACID> atomCheckerIDs;
	/// <summary>A list of the residueCheckerIDs to use to check the Geometry of this Geometry Interface.</summary>
	private List<RCID> residueCheckerIDs;
	/// <summary>A list of the atomCheckErrorLevels that are used if the Geometry fails a check.</summary>
	private Dictionary<ACID, GIS> atomCheckErrorLevels;
	/// <summary>A list of the residueCheckErrorLevels that are used if the Geometry fails a check.</summary>
	private Dictionary<RCID, GIS> residueCheckErrorLevels;

	/// <summary>The list of Arrows that point away from this Geometry Interface.</summary>
	public List<Arrow> arrows;


	/// <summary>The reference to the title to this Geometry Interface that is visible to the user.</summary>
	public TextMeshProUGUI titleText;
	/// <summary>The reference to the Load Button of this Geometry Interface.</summary>
	public Button loadButton;
	/// <summary>The reference to the Save Button of this Geometry Interface.</summary>
	public Button saveButton;

	/// <summary>The reference to the glowing edge image of this Geometry Interface.</summary>
	public Image edge;
	/// <summary>The reference to the background image of this Geometry Interface.</summary>
	public Image background;

	/// <summary>Has this Geometry Interface been dragged onto itself?</summary>
	public bool selfDraggedGesture;
	/// <summary>How long the selfDraggedGesture is active for.</summary>
	public float selfDraggedTime = 5f;


	/// <summary>A Dictionary containing all the descriptions to use in a tooltip, accessed by its status.</summary>
	private Dictionary<GIS, string> allDescriptions = new Dictionary<GIS, string> {
		{GIS.DISABLED, ""},
		{GIS.COMPLETED, ""},
		{GIS.OK, ""},
		{GIS.WARNING, ""},
		{GIS.ERROR, ""}
	};

	private bool _canLoad;
	/// <summary>Gets/Sets whether the Load Button is active.</summary>
	public bool canLoad {
		get {return _canLoad;}
		set {
			loadButton.interactable = value;
			_canLoad = value;
		}
	}

	private bool _canSave;
	/// <summary>Gets/Sets whether the Save Button is active.</summary>
	public bool canSave {
		get {return _canSave;}
		set {
			saveButton.interactable = value;
			_canSave = value;
		}
	}

	private int _activeTasks = 0;
	/// <summary>Gets/Sets the number of tasks currently running on this Geometry Interface.</summary>
	public int activeTasks {
		get {return _activeTasks;}
		set {
			_activeTasks = Mathf.Max (0, value);
			if (_activeTasks < 2) {
				//Crossing between 0 and 1 active task - reset materials
				SetMaterials();
			}
		}
	}

	private GIS _status;
	/// <summary>Gets/Sets the Status (e.g. error/ok/disabled/loading) of this Geometry Interface.</summary>
	public GIS status {
		get {return _status;}
		set {
			_status = value;
			SetMaterials();
		}
	}

	//INPUT HANDLING

	/// <summary>Is the pointer being pressed down on this Geometry Interface?</summary>
	private bool pointerDown;
	/// <summary>Is this Geometry Interface being dragged?</summary>
	private bool isBeingDragged;

	/// <summary>Does this geometry interface implement dragging behaviour?</summary>
    private bool draggable;
	/// <summary>How long does the pointer need to be down before considering it dragged?</summary>
    public static float timeUntilDragged = 0.1f;
	
	/// <summary>Where was the cursor when OnPointerDown was called?</summary>
	Vector3 pointerDownPosition;


	/// <summary>Instantiates a Geometry Interface from an XML Element.</summary>
	/// <param name="giX">An XML Element containing Geometry Interface settings</param>
	public static GeometryInterface FromXML(XElement giX, Transform transform) {

		//Instantiate a new Geometry Interface
		GeometryInterface geometryInterface = PrefabManager.InstantiateGeometryInterface(transform);

		//Get geometry interface ID from "name" attribute
		geometryInterface.id = FileIO.GetConstant(giX, "name", Constants.GeometryInterfaceIDMap, true);

		geometryInterface.name = geometryInterface.id.ToString();

		//Fill in title and full name
		geometryInterface.titleText.text = FileIO.ParseXMLString(giX, "title");
		geometryInterface.fullName = FileIO.ParseXMLString(giX, "fullName");

		//Descriptions
		geometryInterface.allDescriptions[GIS.DISABLED] = FileIO.ParseXMLString(giX, "disabledDescription");
		geometryInterface.allDescriptions[GIS.COMPLETED] = FileIO.ParseXMLString(giX, "completedDescription");
		geometryInterface.allDescriptions[GIS.OK] = FileIO.ParseXMLString(giX, "okDescription");
		geometryInterface.allDescriptions[GIS.LOADING] = FileIO.ParseXMLString(giX, "loadingDescription");
		geometryInterface.allDescriptions[GIS.WARNING] = FileIO.ParseXMLString(giX, "warningDescription");
		geometryInterface.allDescriptions[GIS.ERROR] = FileIO.ParseXMLString(giX, "errorDescription");

		//Get the initial position
		XElement positionX = giX.Element("position");
		geometryInterface.transform.GetComponent<RectTransform>().anchoredPosition = new Vector2(
			FileIO.ParseXMLFloat(positionX, "x"),
			FileIO.ParseXMLFloat(positionX, "y")
		);

		//Residue Checks and error levels
		geometryInterface.residueCheckerIDs = new List<RCID>();
		geometryInterface.residueCheckErrorLevels = new Dictionary<RCID, GIS>();
		foreach (XElement residueCheckerX in giX.Elements("residueCheck")) {

			RCID residueCheckID = FileIO.GetConstant(residueCheckerX, "name", Constants.ResidueCheckerIDMap);
			GIS errorLevel = FileIO.GetConstant(residueCheckerX, "errorLevel", Constants.GeometryInterfaceStatusMap);

			geometryInterface.residueCheckerIDs.Add(residueCheckID);
			geometryInterface.residueCheckErrorLevels[residueCheckID] = errorLevel;

		}

		//Atom Checks and error levels
		geometryInterface.atomCheckerIDs = new List<ACID>();
		geometryInterface.atomCheckErrorLevels = new Dictionary<ACID, GIS>();
		foreach (XElement atomCheckerX in giX.Elements("atomCheck")) {

			ACID atomCheckID = FileIO.GetConstant(atomCheckerX, "name", Constants.AtomCheckerIDMap);
			GIS errorLevel = FileIO.GetConstant(atomCheckerX, "errorLevel", Constants.GeometryInterfaceStatusMap);
			
			geometryInterface.atomCheckerIDs.Add(atomCheckID);
			geometryInterface.atomCheckErrorLevels[atomCheckID] = errorLevel;

		}

		//Set initial property values
		geometryInterface.canLoad = true;
		geometryInterface.canSave = false;
		geometryInterface.draggable = true;
		geometryInterface.status = GIS.DISABLED;

		return geometryInterface;
	}

	/// <summary>Set the material of the Edge, Background and Arrows of this Geometry Interface based on its status.</summary>
	void SetMaterials() {

		GIS materialStatus = activeTasks > 0 ? GIS.LOADING : status;

		ColorBlock cb = ColorScheme.GetColorBlock(materialStatus);
		Color backgroundColor = ColorScheme.GetStatusColor(materialStatus);

		edge.color = cb.normalColor;
		background.color = backgroundColor;

		if (materialStatus == GIS.LOADING) {
			edge.material = ColorScheme.GetLineGlowOSCMaterial();
		} else {
			edge.material = ColorScheme.GetLineGlowMaterial();
		}

		loadButton.colors = cb;
		saveButton.colors = cb;
		titleText.color = cb.highlightedColor;

		foreach (Arrow arrow in arrows) {
			arrow.UpdateStatus();
		};

	}

	/// <summary>Instantiates an empty Geometry object for this Geometry Interface.</summary>
	public void InitialiseEmptyGeometry() {
		geometry = PrefabManager.InstantiateGeometry(transform);
	}

	/// <summary>Sets this Geometry Interface's Geometry to geometry.</summary>
	/// <param name="geometry">Geometry to set.</param>
	public IEnumerator SetGeometry(Geometry geometry) {
		activeTasks++;
		this.geometry = geometry;
		yield return SetGeometry();
		activeTasks--;

	}

	/// <summary>Sets geometry's Parent Transform and makes sure its Residues are pointing to geometry.</summary>
	public IEnumerator SetGeometry() {
		activeTasks++;
		if (geometry != null) {
			//Set the parent transform of geometry to this transform
			geometry.transform.parent = transform;
			//Set the parent of each residue in geometry to the geometry (in case setting from a Residue Dictionary)
			foreach ((ResidueID residueID, Residue residue) in geometry.EnumerateResidues()) {
				residue.parent = geometry;
				if (Timer.yieldNow) {yield return null;}
			}
		} else {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Failed to set geometry of Geometry Interface {0} with null Geometry",
				id
			);
		}
		activeTasks--;

	}

	/// <summary>Copy geometry to another Geometry Interface.</summary>
	/// <param name="copyToGIID">The ID of the Geometry Interface to copy geometry to.</param>
	public IEnumerator CopyGeometry(GIID copyToGIID) {
		GeometryInterface copyToGI = Flow.GetGeometryInterface(copyToGIID);
		if (copyToGI == null) {
			CustomLogger.LogFormat(
				EL.INFO,
				"Failed to copy Geometry Interface Geometry {0} ({1}) to Geometry Interface {2} - {2} is null.",
				id,
				fullName,
				copyToGIID
			);
			yield break;
		}
		yield return copyToGI.SetGeometry(geometry.Take(copyToGI.transform));
		CustomLogger.LogFormat(
			EL.INFO,
			"Copied Geometry Interface Geometry {0} ({1}) to Geometry Interface {2} ({3})",
			id,
			fullName,
			copyToGIID,
			copyToGI.fullName
		);
	}

	/// <summary>A helper delegate function to make RunDraggedGITask flexible.</summary>
	/// <remarks>Performs an action given two Atom objects</remarks>
	/// <param name="copyFromAtom">Atom to copy information from.</param>
	/// <param name="copyToAtom">Atom to copy information to.</param>
	private delegate void DraggedGITask(Atom copyFromAtom, Atom copyToAtom);

	/// <summary>Copy information about this Geometry Interface's Geometry to another Geometry Interface's Geometry.</summary>
	/// <param name="copyToGIID">The ID of the Geometry Interface to copy information to.</param>
	/// <param name="draggedGITask">A function to apply to matching atoms <code>(Atom copyFromAtom, Atom copyToAtom) => void</code>.</param>
	/// <param name="taskID">The ID of the Task that this function is running under.</param>
	private IEnumerator RunDraggedGITask(GIID copyToGIID, DraggedGITask draggedGITask, TID taskID) {

		NotificationBar.SetTaskProgress(taskID, 0f);
		yield return null;

		//Get geometry to copy information to
		if (geometry == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Couldn't run Geometry Interface Task {0} ({1}) - Geometry {2} ({3}) is null",
				taskID,
				Constants.TaskIDMap[taskID],
				id,
				fullName
			);
			yield break;
		}

		//Get geometry to copy information to
		Geometry copyToGeometry = Flow.GetGeometry(copyToGIID);
		if (copyToGeometry == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Couldn't run Geometry Interface Task {0} ({1}) - Geometry {2} is null",
				taskID,
				Constants.TaskIDMap[taskID],
				copyToGIID
			);
			yield break;
		}

		//Use numResidues and residueNum to track progress for Task Progress
		int numResidues = geometry.residueCount;
		int residueNum = 0;

		foreach ((ResidueID residueID, Residue thisResidue) in geometry.EnumerateResidues()) {

			//Continue to next Residue if copyToGeometry does not contain residueID
			Residue otherResidue;
			if (!copyToGeometry.TryGetResidue(residueID, out otherResidue)) {
				continue;
			}

			foreach (PDBID pdbID in thisResidue.pdbIDs) {

				//Continue to next Atom if copyToGeometry does not contain PDBID
				Atom copyToAtom;
				if (!otherResidue.TryGetAtom(pdbID, out copyToAtom)) {
					continue;
				}

				//Run the draggedGITask on this pair of matching atoms
				try {
					draggedGITask(copyToAtom, thisResidue.GetAtom(pdbID));
				} catch (System.Exception e) {

					//Report failed atom
					AtomID failedAtomID = new AtomID(residueID, pdbID);
					CustomLogger.LogFormat(
						EL.ERROR,
						"Error while running DraggedGITask {0} on {1}",
						taskID,
						failedAtomID
					);
					CustomLogger.LogOutput(string.Format(
						"Error while running DraggedGITask {0} on {1}:\n{1}",
						taskID,
						failedAtomID,
						e.StackTrace
					));
					NotificationBar.ClearTask(taskID);
					yield break;
				}
					
 			}

			//Update Task Bar
			if (Timer.yieldNow) {
				NotificationBar.SetTaskProgress(
					taskID,
					CustomMathematics.Map(++residueNum, 0f, numResidues, 0f, 1f)
				);
				yield return null;
			}
		}

		NotificationBar.ClearTask(taskID);
	}

	/// <summary>Copy positions of matching atoms to another Geometry Interface.</summary>
	/// <param name="copyToGIID">The ID of the Geometry Interface to copy positions to.</param>
	public IEnumerator CopyPositions(GIID copyToGIID) {
		return RunDraggedGITask(
			copyToGIID, 
			(copyFromAtom, copyToAtom) => copyToAtom.position.xyz = copyFromAtom.position,
			TID.COPY_POSITIONS
		);
	}

	/// <summary>Copy Ambers of matching atoms to another Geometry Interface.</summary>
	/// <param name="copyToGIID">The ID of the Geometry Interface to copy Ambers to.</param>
	public IEnumerator CopyAmbers(GIID copyToGIID) {
		return RunDraggedGITask(
			copyToGIID, 
			(copyFromAtom, copyToAtom) => copyToAtom.amber = copyFromAtom.amber,
			TID.COPY_AMBERS
		);
	}

	/// <summary>Copy partial charges of matching atoms to another Geometry Interface.</summary>
	/// <param name="copyToGIID">The ID of the Geometry Interface to copy partial charges to.</param>
	public IEnumerator CopyPartialCharges(GIID copyToGIID) {
		return RunDraggedGITask(
			copyToGIID, 
			(copyFromAtom, copyToAtom) => copyToAtom.partialCharge = copyFromAtom.partialCharge,
			TID.COPY_PARTIAL_CHARGES
		);
	}

	/// <summary>Replace the MM Parameters of copyToGIID with this Geometry's Parameters.</summary>
	/// <param name="copyToGIID">The ID of the Geometry Interface whose Geometry's Parameters will be replaced.</param>
	public IEnumerator ReplaceParameters(GIID copyToGIID) {
		Geometry copyToGeometry = Flow.GetGeometry(copyToGIID);
		if (copyToGeometry == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"'{0}' is empty. Cannot replace Parameters!",
				copyToGIID
			);
			yield break;
		}
		Parameters.UpdateParameters(geometry, copyToGeometry, true);
		yield return null;
	}

	/// <summary>Update the MM Parameters of copyToGIID with this Geometry's Parameters.</summary>
	/// <param name="copyToGIID">The ID of the Geometry Interface whose Geometry's Parameters will beupdated.</param>
	public IEnumerator UpdateParameters(GIID copyToGIID) {
		Geometry copyToGeometry = Flow.GetGeometry(copyToGIID);
		if (copyToGeometry == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"'{0}' is empty. Cannot update Parameters!",
				copyToGIID
			);
			yield break;
		}
		Parameters.UpdateParameters(geometry, copyToGeometry, false);
		yield return null;
	}

	/// <summary>Align this Geometry to alignToGIID's Geometry.</summary>
	/// <param name="alignToGIID">The ID of the Geometry Interface to align to.</param>
	public IEnumerator AlignGeometries(GIID alignToGIID) {
		Geometry alignTo = Flow.GetGeometry(alignToGIID);
		if (alignTo == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"'{0}' is empty. Cannot align!",
				alignToGIID
			);
			yield break;
		}
		yield return geometry.AlignTo(alignTo);
	}

	/// <summary>Triggered when this Geometry Interface is dragged onto itself.</summary>
	private IEnumerator DraggedOntoSelf() {
		float timer = 0f;
		//Set this to true for selfDraggedTime seconds
		selfDraggedGesture = true;
		while (timer < selfDraggedTime) {
			timer += Time.deltaTime;
			yield return null;
		}
		selfDraggedGesture = false;
	}

	/// <summary>Perform analysis on this Geometry Interface's Geometry.</summary>
	public IEnumerator Analyse() {
		if (geometry != null) {
			activeTasks++;
			GeometryAnalyser geometryAnalyser = PrefabManager.InstantiateGeometryAnalyser(transform);
			geometryAnalyser.Analyse(id);
			activeTasks--;
		}
		yield return null;
	}

	/// <summary>Load a Geometry into this Geometry Interface from a file path.</summary>
	/// <param name="path">The path of the file to load.</param>
	/// <param name="checkAll">Perform checks on the loaded Geometry for validity.</param>
	/// <param name="analyse">Perform analysis on the loaded Geometry.</param>
	public IEnumerator LoadGeometry(string path, bool checkAll=true, bool analyse=true) {
		activeTasks++;

		//Instantiate an empty Geometry object to load file into
		Geometry geometry = PrefabManager.InstantiateGeometry(transform);

		//Reset the status in case the previous status was an error
		status = GIS.OK;

		//Load the file into the Geometry
		yield return FileReader.LoadGeometry(geometry, path, fullName);

		//Link the Geometry to this Geometry Interface
		yield return SetGeometry(geometry);

		//Perform checks if needed
		if (checkAll) yield return CheckAll();

		//Perform analysis if needed
		//if (analyse) yield return Analyse();

		activeTasks--;

	}

	/// <summary>Update this Geometry Interface's Geometry from a file path.</summary>
	/// <param name="path">The path of the file to load.</param>
	/// <param name="checkAll">Perform checks on the loaded Geometry for validity.</param>
	public IEnumerator UpdateGeometry(string path, bool checkAll=true) {
		activeTasks++;

		//Reset the status in case the previous status was an error
		status = GIS.OK;

		//Load the file into the Geometry
		yield return FileReader.LoadGeometry(geometry, path, fullName);

		//Link the Geometry to this Geometry Interface
		yield return SetGeometry(geometry);

		//Perform checks if needed
		if (checkAll) yield return CheckAll();

		activeTasks--;

	}

	/// <summary>Delete this Geometry Interface's Geometry.</summary>
	void RemoveGeometry() {
		if (geometry != null) {
			//Destroy Atoms
			GameObject.Destroy(geometry.gameObject);

			//Reset statuses
			geometry = null;
			canSave = false;
			status = Constants.GeometryInterfaceStatus.DISABLED;
		}
	}

	/// <summary>Open a FileSelector to load a file.</summary>
	/// <remarks>Used by the Load Button</remarks>
	public void LoadFile() {
		//Make sure the GeometryInterface isn't busy
		if (activeTasks == 0 && status != GIS.LOADING) {

			StartCoroutine(LoadFileEnumerator());
		}
	}

	/// <summary>Open a FileSelector to load a file.</summary>
	private IEnumerator LoadFileEnumerator() {
		activeTasks++;

		bool update = false;
		if (geometry != null && geometry.size != 0) {
			MultiPrompt multiPrompt = MultiPrompt.main;
			multiPrompt.Initialise(
				"Update File?", 
				"Update file or load a new geometry.", 
				new ButtonSetup(text:"Update Geometry", action:() => update = true),
				new ButtonSetup(text:"Overwrite", action:() => update = false),
				new ButtonSetup(text:"Cancel", action:() => multiPrompt.Cancel())
			);

			while (!multiPrompt.userResponded) {
				yield return null;
			}

			multiPrompt.Hide();

			if (multiPrompt.cancelled) {
				activeTasks--;
				yield break;
			}
		}

		FileSelector loadPrompt = FileSelector.main;

		//Set FileSelector to Load mode
		yield return loadPrompt.Initialise(saveMode:false, Flow.loadTypes);
		//Wait for user response
		while (!loadPrompt.userResponded) {
			yield return null;
		}

		if (loadPrompt.cancelled) {
			GameObject.Destroy(loadPrompt.gameObject);
			activeTasks--;
			yield break;
		}

		//Got a non-cancelled response from the user
		string path = loadPrompt.confirmedText;
		//Close the FileSelector
		GameObject.Destroy(loadPrompt.gameObject);

		//Check the file exists
		if (!File.Exists(path)) {
			CustomLogger.LogFormat(EL.ERROR, "File does not exist: {0}", path);
			GameObject.Destroy(loadPrompt.gameObject);
			activeTasks--;
			yield break;
		}

		//File exists. Load from path
		if (update) {
			yield return UpdateGeometry(path);
		} else {
			yield return LoadGeometry(path);
		}
		activeTasks--;
		//yield return Cleaner.CalculateConnectivity(id);
	}

	/// <summary>Open a FileSelector to load a file.</summary>
	/// <remarks>Static method</remarks>
	public static IEnumerator LoadFile(GIID geometryInterfaceID) {
		yield return Flow.GetGeometryInterface(geometryInterfaceID).LoadFileEnumerator();
	}

	/// <summary>Open a FileSelector to save a file.</summary>
	/// <remarks>Used by the Save Button</remarks>
	public void SaveFile() {
		if (activeTasks == 0 && status != GIS.LOADING && geometry != null) {
			StartCoroutine(SaveFileEnumerator());
		}
	}

	/// <summary>Open a FileSelector to save a file.</summary>
	public IEnumerator SaveFileEnumerator() {
		activeTasks++;

		//Open the FileSelector
		FileSelector savePrompt = FileSelector.main;

		//Set FileSelector to Save mode
		yield return savePrompt.Initialise(true, Flow.saveTypes);


		bool overwrite = false;
		string path;
		//Continue prompting until there is a cancel or confirm
		while (true) {
			//Wait for user response
			while (!savePrompt.userResponded) {
				yield return null;
			}

			if (savePrompt.cancelled) {
				GameObject.Destroy(savePrompt.gameObject);
				activeTasks--;
				yield break;
			}

			//Got a non-cancelled response from the user
			path = savePrompt.confirmedText;

			//Check the file exists
			if (!File.Exists(path)) {
				break;
			}


			//Prompt to ask whether user wants to overwrite file
			MultiPrompt multiPrompt = MultiPrompt.main;
			multiPrompt.Initialise(
				"Overwrite File?", 
				string.Format("Are you sure you want to overwrite {0}", path),
				new ButtonSetup("Yes", () => overwrite = true),
				new ButtonSetup("No", () => overwrite = false)
			);

			//Wait for user response
			while (!multiPrompt.userResponded) {
				yield return null;
			}

			multiPrompt.Hide();
			savePrompt.userResponded = false;
			savePrompt.cancelled = false;

			//multiPrompt was cancelled - User does not want to save
			if (multiPrompt.cancelled) {
				activeTasks--;
				yield break;
			} else if (overwrite) {
				break;
			}
			//Continue
				
		}

		if (overwrite) {
			//Log that a file will be overwritten
			CustomLogger.LogFormat(EL.WARNING, "Overwriting File: {0}", path);
		}

		//Close the FileSelector
		GameObject.Destroy(savePrompt.gameObject);

		//Write the file
        FileWriter fileWriter;
        try {
            fileWriter = new FileWriter(geometry, path, true);
        } catch (System.ArgumentException e) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Failed to save Geometry! {0}",
                e.Message
            );
            yield break;
        }
        yield return fileWriter.WriteFile();

		CustomLogger.LogFormat(
			EL.INFO, 
			"Saved geometry to path {0} ({1})",
			path,
			fullName
		);

		activeTasks--;
	}

	/// <summary>Open a FileSelector to save a file.</summary>
	/// <remarks>Static method</remarks>
	public static IEnumerator SaveFile(GIID geometryInterfaceID) {
		yield return Flow.main.geometryDict[geometryInterfaceID].SaveFileEnumerator();
	}

	/// <summary>Show the Analysis Window.</summary>
	public void ShowAnalysis() {
		if (geometry != null) {
			GeometryAnalyser geometryAnalyser = PrefabManager.InstantiateGeometryAnalyser(null);
			geometryAnalyser.Analyse(id);
		}
	}

	/// <summary>Open the Context Menu (right clicked).</summary>
	public void ShowContextMenu() {
		ContextMenu contextMenu = ContextMenu.main;

		//Clear the Context Menu
		contextMenu.Clear();

		bool geometryEnabled = (geometry != null);

		//Add buttons and spacers
		contextMenu.AddButton(() => ViewAtoms(), "View Atoms", geometryEnabled);
		contextMenu.AddButton(() => ViewResidueTable(), "View Residue Table", geometryEnabled);
		
		contextMenu.AddSpacer();
		
		contextMenu.AddButton(() => ComputeConnectivity(), "Compute Connectivity", geometryEnabled);
		contextMenu.AddButton(() => StartCoroutine(CheckAll()), "Check Atoms", geometryEnabled);
		contextMenu.AddButton(() => ShowAnalysis(), "Analyse", geometryEnabled);
		contextMenu.AddButton(() => StartCoroutine(ComputeParameterScores()), "Get Parameter Scores", geometryEnabled);

		contextMenu.AddSpacer();

		contextMenu.AddButton(() => StartCoroutine(RunMacro()), "Run Macro File", true);

		contextMenu.AddSpacer();

		contextMenu.AddButton(() => LoadFile(), "Load File", true);
		contextMenu.AddButton(() => SaveFile(), "Save File", geometryEnabled);

		//Show the Context Menu
		contextMenu.Show();
	}

	private void ViewAtoms() {
		if (geometry == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Geometry Interface {0} is null",
				id
			);
			return;
		}

		AtomsVisualiser atomsVisualiser = AtomsVisualiser.main;
		if (atomsVisualiser == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Atoms Visualiser is null"
			);
			return;
		}

		atomsVisualiser.enabled = true;
		StartCoroutine(atomsVisualiser.VisualiseGeometry(id));

	}

	private void ViewResidueTable() {
		if (geometry == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Geometry Interface {0} is null",
				id
			);
			return;
		}

		ResidueTable residueTable = ResidueTable.main;
		if (residueTable == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Residue Table is null"
			);
			return;
		}

		residueTable.enabled = true;
		StartCoroutine(residueTable.Populate(id));

	}

	public void ComputeConnectivity() {
		if (geometry == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Geometry Interface {0} is null",
				id
			);
			return;
		}

		StartCoroutine(Cleaner.CalculateConnectivity(id));

	}

	/// <summary>Run the GeometryChecker.</summary>
	/// <remarks>Sets the status of this Geometry Interface based on whether the checks passed</remarks>
	public IEnumerator CheckAll() {

		if (geometry == null || geometry.size == 0) {
            CustomLogger.LogFormat(
                EL.ERROR, 
                "Geometry is empty on Geometry Interface ID: {0}.",
				id
            );
			status = GIS.DISABLED;
			yield break;
		}

		activeTasks++;
		canSave = false;
        NotificationBar.SetTaskProgress(TID.CHECK_GEOMETRY, 0f);
		yield return null;


        try {
			//Make sure the Geometry are set first
			checker.SetGeometry(geometry);
        } catch (System.NullReferenceException e) {
            CustomLogger.LogFormat(
                EL.ERROR, 
                "Trying to perform checks with no Geometry on Geometry Interface ID: {0}. Error: {1}",
                id,
                e.StackTrace
            );
			activeTasks--;
			NotificationBar.ClearTask(TID.CHECK_GEOMETRY);
			yield break;
        }

		//Add checks to the Checker
		checker.SetChecks(residueCheckerIDs, atomCheckerIDs, residueCheckErrorLevels, atomCheckErrorLevels);

		//Run the checks
		yield return checker.Check();

		NotificationBar.SetTaskProgress(TID.CHECK_GEOMETRY,0.90f);
		yield return null;

		//Get information about residues
		List<ResidueID> residueIDs = geometry.EnumerateResidueIDs().ToList();
        foreach (ResidueID residueID in residueIDs) {
			geometry.SetResidueProperties(residueID);
		}

		NotificationBar.SetTaskProgress(TID.CHECK_GEOMETRY,0.95f);
		yield return null;

		if (geometry.gaussianCalculator != null) {
			yield return geometry.gaussianCalculator.CheckOrbitalsAvailable();
		}


		//Set status
		status = (GIS)Mathf.Max((int)checker.errorLevel, (int)status);
		
		canSave = true;	
		
		NotificationBar.ClearTask(TID.CHECK_GEOMETRY);
		yield return null;
		activeTasks--;
	}

	public IEnumerator ComputeParameterScores() {

		if (geometry == null || geometry.size == 0) {
            CustomLogger.LogFormat(
                EL.ERROR, 
                "Geometry is empty on Geometry Interface ID: {0}.",
				id
            );
			yield break;
		}

		geometry.parameters.UpdateParameters(Settings.defaultParameters);

		activeTasks++;
        NotificationBar.SetTaskProgress(TID.COMPUTE_PARAMETER_SCORES, 0f);
		yield return null;

		int numAtoms = geometry.size;
		int atomNum = 0;
		foreach (Atom atom in geometry.EnumerateAtoms()) {
			atom.penalty = 0f;
			if (Timer.yieldNow) {
				NotificationBar.SetTaskProgress(
					TID.COMPUTE_PARAMETER_SCORES,
					CustomMathematics.Map(atomNum, 0, numAtoms, 0, 0.2f)
				);
				yield return null;
			}
		}

		atomNum = 0;
		foreach ((AtomID atomID, Atom atom) in geometry.EnumerateAtomIDPairs()) {
			geometry.parameters.GetAtomPenalty(atomID);
			CustomLogger.LogFormat(
				EL.VERBOSE,
				"Setting Penalty Score for Atom ({0}): {1}",
				atomID,
				atom.penalty
			);
			if (Timer.yieldNow) {
				NotificationBar.SetTaskProgress(
					TID.COMPUTE_PARAMETER_SCORES,
					CustomMathematics.Map(atomNum, 0, numAtoms, 0.2f, 1)
				);
				yield return null;
			}
			atomNum++;
		}

        NotificationBar.ClearTask(TID.COMPUTE_PARAMETER_SCORES);
		activeTasks--;

	}

	public IEnumerator RunMacro() {
		activeTasks++;
		yield return Macro.RunMacro(id);
		activeTasks--;
	}

	/// <summary>
	/// Called when the pointer enters the screen space occupied by this Geometry Interface
	/// </summary>
	/// <remarks>Called by Unity</remarks>
	public void OnPointerEnter(PointerEventData pointerEventData) {
		Flow flow = Flow.main;
		if (flow.draggedGIItem == null) {
			Tooltip.main.Show(fullName, allDescriptions[status]);
		} else {
			flow.draggedGIItem.value = id;
		}
	}

	/// <summary>
	/// Called when the pointer leaves the screen space occupied by this Geometry Interface
	/// </summary>
	/// <remarks>Called by Unity</remarks>
		public void OnPointerExit(PointerEventData pointerEventData) {
		Flow flow = Flow.main;
		if (flow.draggedGIItem == null) {
			Tooltip.main.Hide();
		} else {
			flow.draggedGIItem.value = null;
		}
	}


	/// <summary>
	/// Called when the screen space occupied by this Geometry Interface is clicked on
	/// </summary>
	public void OnPointerClick(PointerEventData pointerEventData) {
		if (pointerEventData.button == PointerEventData.InputButton.Left) {

		} else if (pointerEventData.button == PointerEventData.InputButton.Right) {
			ShowContextMenu();
		}
	}

	/// <summary>
	/// Called when the screen space occupied by this Geometry Interface is pressed down by the pointer
	/// </summary>
	/// <remarks>Called by Unity</remarks>
    public void OnPointerDown(PointerEventData pointerEventData) {
        pointerDown = true;
        isBeingDragged = false;

		pointerDownPosition = Input.mousePosition;

        //Check whether to invoke drag or click callback
		if (pointerEventData.button == PointerEventData.InputButton.Left) {
	        StartCoroutine(DraggingTest());
		}
    }

	/// <summary>
	/// Called when the pointer is no longer active on the screen space occupied by this Geometry Interface
	/// </summary>
	/// <remarks>Called by Unity</remarks>
    public void OnPointerUp(PointerEventData pointerEventData) {
        pointerDown = false;
        if (!isBeingDragged) {
            OnPointerClick(pointerEventData);
        }
        isBeingDragged = false;
    }
    
	/// <summary>
	/// Check whether the Geometry Interface is being dragged
	/// </summary>
    private IEnumerator DraggingTest() {
        float timer = 0f;
        while (timer < timeUntilDragged && pointerDown) {
            timer += Time.deltaTime;
            yield return null;
        }
        isBeingDragged = true;
		if (draggable) {
			StartCoroutine(OnBeginDrag());
		}

    }

	/// <summary>Called when the background is being dragged.</summary>
	/// <remarks>
	/// Called by Unity
	/// Override the native OnDrag so the EventSystems handler doesn't call ScrollRect.OnDrag()
	/// </remarks>
    public void OnDrag(PointerEventData pointerEventData) {}

    
	/// <summary>
	/// Called when dragging begins on this Geometry Interface
	/// </summary>
    private IEnumerator OnBeginDrag() {

		//Make sure GI is available
		if (status != GIS.DISABLED && status != GIS.LOADING) {
			Tooltip.main.Hide();
			yield return SpawnDraggedGI();
		}
    }


    
	/// <summary>
	/// Create a representation of this Geometry Interface that follows the cursor
	/// Spawn position is where OnPointerDown occurred
	/// </summary>
	private IEnumerator SpawnDraggedGI() {
		//Instantiate the representation
		ListItem draggedGIItem = PrefabManager.InstantiateListItem(transform);
		draggedGIItem.value = GIID.NONE;
		
		//Give it the same colours as this GI
		draggedGIItem.edge.material = edge.material;
		draggedGIItem.edge.color = edge.color;
		draggedGIItem.background.color = background.color;

		RectTransform draggedGIItemTransform = draggedGIItem.GetComponent<RectTransform>();
		RectTransform rectTransform = GetComponent<RectTransform>();

		//Make sure the representation has the same size and position as this
		draggedGIItemTransform.anchorMin = new Vector2(0.5f, 0.5f);
		draggedGIItemTransform.anchorMax = new Vector2(0.5f, 0.5f);
		draggedGIItemTransform.pivot = new Vector2(0.5f, 0.5f);
		draggedGIItemTransform.anchoredPosition = new Vector2(0f, 0f);
		draggedGIItemTransform.sizeDelta = rectTransform.sizeDelta;

		//Now attach it to Flow
		draggedGIItemTransform.SetParent(Flow.main.transform);
		Flow.main.draggedGIItem = draggedGIItem;

		//Make sure the representation doesn't block Rays from the cursor (i.e. it shouldn't block other GIs)
		CanvasGroup canvasGroup = draggedGIItem.GetComponent<CanvasGroup>();
		canvasGroup.blocksRaycasts = false;
		canvasGroup.interactable = false;

		Vector3 initialPosition = draggedGIItemTransform.transform.localPosition;

		//Shrink the rep
		StartCoroutine(draggedGIItem.ChangeSizeOverTime(new Vector2(20f, 20f), 0.2f));

        while (pointerDown) {
			//Follow the cursor until OnPointerUp is called i.e. it is dropped
			draggedGIItemTransform.transform.localPosition = initialPosition + Input.mousePosition - pointerDownPosition;
			
            yield return null;
        }

		//Call OnDrop
		yield return OnDrop(draggedGIItem);
	}

	/// <summary>
	/// Called when a draggedGIItem is dropped
	/// </summary>
	/// <remarks>
	/// Does nothing unless the draggedGIItem was dropped on a Geometry Interface
	/// Executes a DraggedGITask after opening the DraggedGIPopup
	/// </remarks>
	private IEnumerator OnDrop(ListItem draggedGIItem) {
		//Get the GIID of the GI that the draggedGIItem is on top of, if applicable
		GIID draggedToID;
		try {
			draggedToID = (GIID)draggedGIItem.value;
		} catch {
			GameObject.Destroy(draggedGIItem.gameObject);
			yield break;
		}
		
		//Destroy the draggedGIItem
		GameObject.Destroy(draggedGIItem.gameObject);
		
		//Return if dropping on the GI that spawned the draggedGIItem or if it's dropped on the background
		if (draggedToID == GIID.NONE) {
			draggedGIItem.value = GIID.NONE;
			yield break;
		} else if (draggedToID == id) {

			StartCoroutine(DraggedOntoSelf());

			draggedGIItem.value = GIID.NONE;
			yield break;
		}
			
		//Dropped on a valid GI - open the DraggedGIPopup
		DraggedGIPopup draggedGIPopup = DraggedGIPopup.main;
		draggedGIPopup.Initialise(id, draggedToID);

		//Wait for user response
		while (!draggedGIPopup.userResponded) {
			yield return null;
		}

		//Close the DraggedGIPopup
		draggedGIPopup.Hide();

		//User cancelled
		if (draggedGIPopup.selectedTask == TID.NONE) {
			yield break;
		}

		//Get the task and run it
		yield return GetDraggedGITask(draggedGIPopup.selectedTask, draggedToID);
		//Run the checker for that GI
		yield return Flow.main.geometryDict[draggedToID].CheckAll();
		
		draggedGIItem.value = GIID.NONE;
	}

	/// <summary>
	/// Get the Task chosen by the user with the DraggedGIPopup
	/// </summary>
	private IEnumerator GetDraggedGITask(TID taskID, GIID draggedToID) {
		switch (taskID) {
			case (TID.COPY_GEOMETRY):
				yield return CopyGeometry(draggedToID);
				break;
			case (TID.COPY_POSITIONS):
				yield return CopyPositions(draggedToID);
				break;
			case (TID.COPY_AMBERS):
				yield return CopyAmbers(draggedToID);
				break;
			case (TID.COPY_PARTIAL_CHARGES):
				yield return CopyPartialCharges(draggedToID);
				break;
			case (TID.UPDATE_PARAMETERS):
				yield return UpdateParameters(draggedToID);
				break;
			case (TID.REPLACE_PARAMETERS):
				yield return ReplaceParameters(draggedToID);
				break;
			case (TID.ALIGN_GEOMETRIES):
				yield return AlignGeometries(draggedToID);
				break;
			default:
				CustomLogger.LogFormat(
					EL.ERROR,
					"No DraggedGITask for TaskID: {0}",
					taskID
				);
				yield break;
		}
	}


}
