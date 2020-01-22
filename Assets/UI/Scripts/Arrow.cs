using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Xml.Linq;
using System.Linq;
using GIS = Constants.GeometryInterfaceStatus;
using GIID = Constants.GeometryInterfaceID;
using TID = Constants.TaskID;
using AID = Constants.ArrowID;
using EL = Constants.ErrorLevel;

public class Arrow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler {

	public Material material;
	public Material hoveredMaterial;

	public GameObject connectorPrefab;
	public GameObject startCapPrefab;
	public GameObject endCapPrefab;

	public float width = 4f;

	private GIS status;

    public GIID startGIID;
    public GIID endGIID;

    public AID arrowID;

	public List<TID> defaultTasks;


	public static Arrow FromXML(XElement arrowX, Transform transform) {

		Arrow arrow = PrefabManager.InstantiateArrow(transform);

		//Parse XML Element arrowX
		arrow.arrowID = FileIO.GetConstant(arrowX, "name", Constants.ArrowIDMap, fromAttribute:true);

		arrow.defaultTasks = new List<TID>();
		foreach (XElement taskX in arrowX.Element("defaultTasks").Elements("task")) {
			arrow.defaultTasks.Add(Constants.TaskIDMap[taskX.Value]);
		}

		string arrowStartGIName = FileIO.ParseXMLString(arrowX, "startName");
		string arrowEndGIName = FileIO.ParseXMLString(arrowX, "endName");

		arrow.startGIID = Constants.GeometryInterfaceIDMap[arrowStartGIName];
		arrow.endGIID = Constants.GeometryInterfaceIDMap[arrowEndGIName];

		string elbowType = "";
		if (arrowX.Element("elbow") != null) {
			elbowType = arrowX.Element("elbow").Value.ToString();
		}
		arrow.ConnectGeometryInterfaces(elbowType);

		return arrow;
	}

	private void ConnectGeometryInterfaces(string elbowType) {
		GeometryInterface startGeo = Flow.GetGeometryInterface(startGIID);
		if (startGeo == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Couldn't connect Geometry Inferface {0} to {1} - {0} is null",
				startGIID,
				endGIID
			);
			return;
		}
		GeometryInterface endGeo = Flow.GetGeometryInterface(endGIID);
		if (endGeo == null) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Couldn't connect Geometry Inferface {0} to {1} - {0} is null",
				startGIID,
				endGIID
			);
			return;
		}


		Vector3 startPos =  startGeo.GetComponent<RectTransform>().anchoredPosition;
		Vector3 endPos =  endGeo.GetComponent<RectTransform>().anchoredPosition;

		List<Vector3> vertices = new List<Vector3>{startPos};
		vertices.AddRange(GetElbows(startPos, endPos, elbowType));
		vertices.Add(endPos);

		int numConnectors = vertices.Count - 1;

		// Draw n - 1 connectors/arrows for n vertices.
		// If drawing more than one connector, calculate elbow positions.
		for (int i = 0; i < numConnectors; i++) {

			Vector3 connectorStartPos = vertices[i];
			Vector3 connectorEndPos = vertices[i + 1];

			bool isStart = i == 0;
			bool isFinal = i == numConnectors - 1;

			//Normalised vector from p0 to p1
			Vector3 v01n = (connectorEndPos - connectorStartPos).normalized;

			//Add the offsets so arrows don't point to/from the centre of the geo
			if (isStart) connectorStartPos += Vector3.Scale(v01n, Flow.geometryArrowOffset);
			if (isFinal) connectorEndPos -= Vector3.Scale(v01n, Flow.geometryArrowOffset);

			AddConnector(connectorStartPos, connectorEndPos, false, isFinal);

		}


		UpdateStatus();
		name = "Arrow" + startGeo + endGeo;
		startGeo.arrows.Add(this);



	}

	public void AddConnector(Vector3 start, Vector3 end, bool drawStartCap, bool drawEndCap) {

		//Normalised vector from p0 to p1
		Vector3 v01n = (end - start).normalized;

		Quaternion rotation;
		//In the case of drawing an arrow to the left, there are infinite solutions
		// and only one that doesn't use Y (we only want to rotate in Z)
		if (Mathf.Abs (Vector3.Dot(Vector3.up, v01n) + 1f) < 0.001) {
			rotation = Quaternion.Euler(0f, 0f, -180f);
		} else {
			rotation = Quaternion.FromToRotation(Vector3.up, end - start);
		}

		//Set length
		// If there's a start cap, shift forwards by width / 2 
		float length = (end - start).magnitude;
		if (drawStartCap) {
			start += v01n * width / 2f;
			length -= width / 2f;
		} else {
			start -= v01n * width / 2f;
			length += width / 2f;
		}

		if (drawEndCap){
			length -= width / 2f;
		} else {
			length += width / 2f;
		}

		GameObject connector = GameObject.Instantiate<GameObject>(connectorPrefab, transform);
		RectTransform connectorRect = connector.GetComponent<RectTransform>();
		connector.GetComponent<Image>().enabled = true;

		connectorRect.anchoredPosition = start;
		connectorRect.sizeDelta = new Vector2(width, length);
		connectorRect.rotation = rotation;

		if (drawStartCap) {
			GameObject startCap = GameObject.Instantiate<GameObject>(startCapPrefab, connectorRect);
			startCap.GetComponent<Image>().enabled = true;
		}

		if (drawEndCap) {
			GameObject endCap = GameObject.Instantiate<GameObject>(endCapPrefab, connectorRect);
			endCap.GetComponent<Image>().enabled = true;
		}
		
		CreateInteractionBox(connectorRect);

	} 

	private IEnumerable<Vector3> GetElbows(Vector3 start, Vector3 end, string elbowType) {
		/*
			Get elbows between 2 points.
			elbowTypes:
				One Elbow:
				"X": use start's Y and end's X (Move horizontally)
				"Y": use start's X and end's Y (Move vertically)

				Two Elbows
				"XX": 1: use start's Y and average X
				      2: use end's Y and average X
				"YY": 1: use start's X and average Y
				      2: use end's X and average Y
		*/
		List<Vector3> elbows = new List<Vector3>();
		if (elbowType == "X") {
			yield return new Vector3(end.x, start.y, start.z);
		} else if (elbowType == "Y") {
			yield return new Vector3(start.x, end.y, start.z);
		} else if (elbowType == "XX") {
			yield return new Vector3((start.x + end.x) / 2f, start.y, start.z);
			yield return new Vector3((start.x + end.x) / 2f, end.y, start.z);
		} else if (elbowType == "YY") {
			yield return new Vector3(start.x, (start.y + end.y) / 2f, start.z);
			yield return new Vector3(end.x, (start.y + end.y) / 2f, start.z);
		} 
	}





	//HANDLERS
	public void OnPointerEnter(PointerEventData pointerEventData) {
		SetConnectorMaterial(hoveredMaterial);
		SetCapMaterial(hoveredMaterial);
	}

	public void OnPointerExit(PointerEventData pointerEventData) {
		SetConnectorMaterial(material);
		SetCapMaterial(material);
	}

	public void OnPointerClick(PointerEventData pointerEventData) {
		if (status == GIS.DISABLED || status == GIS.ERROR) {
			return;
		}
		StartCoroutine(RunArrowProcedure());
	}

	private IEnumerator RunArrowProcedure() {

		List<TID> availableTasks = System.Enum
			.GetValues(typeof(TID))
			.Cast<TID>()
			.Where(x => x != TID.NONE)
			.ToList();

		ProceduresPopup proceduresPopup = ProceduresPopup.main;
		yield return proceduresPopup.Initialise(availableTasks, defaultTasks);
		while (!proceduresPopup.userResponded) {
			yield return null;
		}

		if (! proceduresPopup.cancelled) {
			//Copy this list, as deleting the procedures popup also deletes the list
			StartCoroutine(
				ArrowFunctions.GetArrowProcedure(
					arrowID, 
					startGIID, 
					endGIID, 
					proceduresPopup.finalTaskIDs
				)
			);
		}

	}

	private void CreateInteractionBox(RectTransform connectorRect) {
		//Increase the arrow's interaction zone
		//https://answers.unity.com/questions/844524/ugui-how-to-increase-hitzone-click-area-button-rec.html?childToView=1407230#answer-1407230

		GameObject interactionBox = new GameObject("InteractableArea");
		RectTransform rectTransform = interactionBox.AddComponent<RectTransform>();

		rectTransform.SetParent(transform);
		rectTransform.anchorMin = connectorRect.anchorMin;
		rectTransform.anchorMax = connectorRect.anchorMax;
		rectTransform.pivot = connectorRect.pivot;
		rectTransform.localPosition = connectorRect.localPosition;
		rectTransform.anchoredPosition = connectorRect.anchoredPosition;
		rectTransform.localScale = connectorRect.localScale;
		rectTransform.sizeDelta = new Vector2(width * 6, connectorRect.sizeDelta.y);
		rectTransform.localRotation = connectorRect.localRotation;

		interactionBox.AddComponent<TransparentGraphic>();
		AddEventTriggerListener(
			interactionBox, 
			EventTriggerType.PointerClick,
			(BaseEventData x) => {
				ExecuteEvents.Execute(
					gameObject, 
					x, 
					ExecuteEvents.pointerClickHandler
				);
			}
		);
		AddEventTriggerListener(
			interactionBox, 
			EventTriggerType.PointerEnter,
			(BaseEventData x) => {
				ExecuteEvents.Execute(
					gameObject, 
					x, 
					ExecuteEvents.pointerEnterHandler
				);
			}
		);
		AddEventTriggerListener(
			interactionBox, 
			EventTriggerType.PointerExit,
			(BaseEventData x) => {
				ExecuteEvents.Execute(
					gameObject, 
					x, 
					ExecuteEvents.pointerExitHandler
				);
			}
		);


	}
 
	private static void AddEventTriggerListener(GameObject target, EventTriggerType eventTriggerType, System.Action<BaseEventData> method) {
		EventTrigger eventTrigger = target.AddComponent<EventTrigger>();
		EventTrigger.Entry entry = new EventTrigger.Entry();
		entry.eventID = eventTriggerType;
		entry.callback = new EventTrigger.TriggerEvent();
		entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(method));
		eventTrigger.triggers.Add(entry);
	}

	void SetConnectorMaterial(Material material) {
		foreach (Image connectorImage in GetComponentsInChildren<Image>()) {
			connectorImage.material = material;
		}

	}

	void SetCapMaterial(Material material) {
		foreach (Transform child in transform) {
			foreach (Image capImage in child.GetComponentsInChildren<Image>()) {
				capImage.material = material;
			}
		}
	}

	void SetConnectorColor(Color color) {
		foreach (Image connectorImage in GetComponentsInChildren<Image>()) {
			connectorImage.color = color;
		}

	}

	void SetCapColor(Color color) {
		foreach (Transform child in transform) {
			foreach (Image capImage in child.GetComponentsInChildren<Image>()) {
				capImage.color = color;
			}
		}
	}

	public void UpdateStatus() {
		bool startIsBusy = Flow.main.geometryDict[startGIID].activeTasks > 0;
		GIS startStatus = Flow.main.geometryDict[startGIID].status;
		GIS endStatus = Flow.main.geometryDict[endGIID].status;

		status = GIS.DISABLED;
		if (startStatus == GIS.COMPLETED) {
			if (endStatus != GIS.DISABLED) {
				status = GIS.COMPLETED;
			} else {
				status = GIS.OK;
			}
		} else if ( ! startIsBusy) {
			if (startStatus == GIS.OK) {
				status = GIS.OK;
			} else if (startStatus == GIS.WARNING) {
				status = GIS.WARNING;
			}
		}

		Color color = ColorScheme.GetColorBlock(status).normalColor;

		SetConnectorColor(color);
		SetCapColor(color);

	}

	public void SetStatus(GIS status, bool isBusy) {
		this.status = status;

		//Can't use arrow if ERROR or LOADING
		GIS arrowStatus = (status == GIS.ERROR || status == GIS.LOADING || isBusy) ? GIS.DISABLED : status;

		Color color = ColorScheme.GetColorBlock(arrowStatus).normalColor;

		SetConnectorColor(color);
		SetCapColor(color);
	}
}

class TransparentGraphic : UnityEngine.UI.Graphic {
	protected override void OnPopulateMesh(VertexHelper v){v.Clear();}
}
