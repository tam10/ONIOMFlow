using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SIZE = Constants.Size;
using COL = Constants.Colour;
using CS = Constants.ColourScheme;

public class PrefabManager : MonoBehaviour {

	private static PrefabManager _main;
	public static PrefabManager main {
		get {
			if (_main == null) {
				_main = GameObject.FindObjectOfType<PrefabManager>();
			}
			return _main;
		}
	}
	

	public Sprite backgroundSprite; 
	public Sprite circleSprite;
	public Sprite whiteGlowSprite;
	public TMP_Dropdown dropdownPrefab;
	public TMP_InputField inputFieldPrefab;
	public Toggle togglePrefab;
	public Button buttonPrefab;
	public ScrollRect scrollViewPrefab;
	public ScrollText scrollTextPrefab;
	public Scrollbar scrollbarPrefab;


	// Geometry Interface
	public GeometryInterface geometryInterfacePrefab;
	public static GeometryInterface InstantiateGeometryInterface(Transform transform) {
		return GameObject.Instantiate<GeometryInterface>(main.geometryInterfacePrefab, transform);
	}

	public Arrow arrowPrefab;
	public static Arrow InstantiateArrow(Transform transform) {
		return GameObject.Instantiate<Arrow>(main.arrowPrefab, transform);
	}


    // Atoms
	public Geometry geometryPrefab;
	public static Geometry InstantiateGeometry(Transform transform) {
		return GameObject.Instantiate<Geometry>(main.geometryPrefab, transform);
	}

	public ResidueRepresentation residueRepresentationPrefab;
	public static ResidueRepresentation InstantiateResidueRepresentation(Transform transform) {
		return GameObject.Instantiate<ResidueRepresentation>(main.residueRepresentationPrefab, transform);
	}

	public AtomsMesh atomsMeshPrefab;
	public static AtomsMesh InstantiateAtomsMesh(Transform transform) {
		return GameObject.Instantiate<AtomsMesh>(main.atomsMeshPrefab, transform);
	}

	public BondsMesh bondsMeshPrefab;
	public static BondsMesh InstantiateBondsMesh(Transform transform) {
		return GameObject.Instantiate<BondsMesh>(main.bondsMeshPrefab, transform);
	}
	public LinkerMesh linkerMeshPrefab;
	public static LinkerMesh InstantiateLinkerMesh(Transform transform) {
		return GameObject.Instantiate<LinkerMesh>(main.linkerMeshPrefab, transform);
	}

	public LineDrawer lineDrawerPrefab;
	public static LineDrawer InstantiateLineDrawer(Transform transform) {
		return GameObject.Instantiate<LineDrawer>(main.lineDrawerPrefab, transform);
	}

	public GeometryAnalyser geometryAnalyserPrefab;
	public static GeometryAnalyser InstantiateGeometryAnalyser(Transform transform) {
		return GameObject.Instantiate<GeometryAnalyser>(main.geometryAnalyserPrefab, transform);
	}

	// AMBER
	public Parameters parametersPrefab;
	public static Parameters InstantiateParameters(Transform transform) {
		return GameObject.Instantiate<Parameters>(main.parametersPrefab, transform);
	}

	// Gaussian
	public GaussianCalculator gaussianCalculatorPrefab;
	public static GaussianCalculator InstantiateGaussianCalculator(Transform transform) {
		return GameObject.Instantiate<GaussianCalculator>(main.gaussianCalculatorPrefab, transform);
	}

	//UI

	public TextBox3D textBoxPrefab;
	public static TextBox3D InstantiateTextBox3D(Transform transform) {
		return GameObject.Instantiate<TextBox3D>(main.textBoxPrefab, transform);
	}

	public ListItem listItemPrefab;
	public static ListItem InstantiateListItem(Transform transform) {
		return GameObject.Instantiate<ListItem>(main.listItemPrefab, transform);
	}


    public ContextButton contextButtonPrefab;
	public static ContextButton InstantiateContextButton(Transform transform) {
		return GameObject.Instantiate<ContextButton>(main.contextButtonPrefab, transform);
	}

	public ContextButtonGroup contextButtonGroupPrefab;
	public static ContextButtonGroup InstantiateContextButtonGroup(Transform transform) {
		return GameObject.Instantiate<ContextButtonGroup>(main.contextButtonGroupPrefab, transform);
	}

	public GameObject spacerPrefab;
	public static GameObject InstantiateSpacerPrefab(Transform transform) {
		return GameObject.Instantiate<GameObject>(main.spacerPrefab, transform);
	}

	public OverlayButton overlayButtonPrefab;
	public static OverlayButton InstantiateOverlayButton(Transform transform) {
		return GameObject.Instantiate<OverlayButton>(main.overlayButtonPrefab, transform);
	}

	private Dictionary<int, GameObject> textDict = new Dictionary<int, GameObject>();
	public static GameObject InstantiateText(
		string textString,
		Transform transform,  
		COL colour=COL.BRIGHT_75,
		TMPro.FontStyles fontStyles=TMPro.FontStyles.Normal,
		TMPro.TextAlignmentOptions textAlignmentOptions=TMPro.TextAlignmentOptions.Center,
		bool autosize=true
	) {
		GameObject textPrefab;
		int key = (int)colour;
		if (!main.textDict.TryGetValue(key, out textPrefab)) {

			textPrefab = new GameObject("Text");
			textPrefab.AddComponent(typeof(TextMeshProUGUI));
			textPrefab.transform.SetParent(main.transform);

			TextMeshProUGUI text = textPrefab.GetComponent<TextMeshProUGUI>();
			text.color = ColorScheme.GetColor(colour);
			text.enableAutoSizing = autosize;
		}
		GameObject textGO = GameObject.Instantiate<GameObject>(textPrefab, transform);
		TextMeshProUGUI textGOText = textGO.GetComponent<TextMeshProUGUI>();
		textGOText.fontStyle = fontStyles;
		textGOText.alignment = textAlignmentOptions;
		textGOText.text = textString;
		return textGO;
	}
	private Dictionary<int, GameObject> rectImageDict = new Dictionary<int, GameObject>();
	public static GameObject InstantiateRectImage(
		Transform transform,
		COL colour=COL.BRIGHT_25,
		bool fillCenter=true
	) {
		GameObject imagePrefab;
		int key = (int)colour;
		if (!main.rectImageDict.TryGetValue(key, out imagePrefab)) {

			imagePrefab = new GameObject("Image");
			imagePrefab.AddComponent(typeof(Image));
			imagePrefab.transform.SetParent(main.transform);
			Image image = imagePrefab.GetComponent<Image>();
			image.sprite = main.backgroundSprite;
			image.type = Image.Type.Sliced;
			image.color = ColorScheme.GetColor(colour);

		}
		GameObject imageGO = GameObject.Instantiate<GameObject>(imagePrefab, transform);
		imageGO.GetComponent<Image>().fillCenter = fillCenter;
		return imageGO;
	}

	private Dictionary<int, GameObject> buttonDict = new Dictionary<int, GameObject>();
	public static GameObject InstantiateButton(
		string textString,
		Transform transform, 
		SIZE size=SIZE.SMALL, 
		CS colourScheme=CS.BRIGHT,
		TMPro.FontStyles fontStyles=TMPro.FontStyles.Normal,
		TMPro.TextAlignmentOptions textAlignmentOptions=TMPro.TextAlignmentOptions.Center,
		bool autosize=true
	) {
		GameObject buttonPrefab;
		int key = (int)size + 1024 * (int)colourScheme;
		if (!main.buttonDict.TryGetValue(key, out buttonPrefab)) {

			COL[] colours = ColorScheme.GetColorScheme(colourScheme);

			Button buttonPrefabButton = GameObject.Instantiate<Button>(main.buttonPrefab, main.transform);
			buttonPrefab = buttonPrefabButton.gameObject;
			foreach (Image image in buttonPrefabButton.GetComponentsInChildren<Image>()) {
				if (ReferenceEquals(image.gameObject, buttonPrefab)) {
					image.color = ColorScheme.GetColor(colours[2]);
				} else {
					image.color = ColorScheme.GetColor(colours[1]);
				}
			}
			
			buttonPrefabButton.colors = ColorScheme.GetColorSchemeBlock(colourScheme);

			TextMeshProUGUI text = buttonPrefabButton.GetComponentInChildren<TextMeshProUGUI>();
			text.color = ColorScheme.GetColor(colours[0]);

		}
		GameObject buttonGO = GameObject.Instantiate<GameObject>(buttonPrefab, transform);
		TextMeshProUGUI textGOText = buttonGO.GetComponentInChildren<TextMeshProUGUI>();
		textGOText.fontStyle = fontStyles;
		textGOText.alignment = textAlignmentOptions;
		textGOText.text = textString;
		textGOText.enableAutoSizing = autosize;
		return buttonGO;
	}

	private Dictionary<int, GameObject> dropdownDict = new Dictionary<int, GameObject>();
	public static GameObject InstantiateDropdown(
		Transform transform,
		CS colourScheme=CS.BRIGHT,
		TMPro.FontStyles fontStyles=TMPro.FontStyles.Normal
	) {
		GameObject dropdownPrefabGO;
		int key = (int)colourScheme;
		if (!main.dropdownDict.TryGetValue(key, out dropdownPrefabGO)) {

			COL[] colours = ColorScheme.GetColorScheme(colourScheme);
			ColorBlock colorBlock = ColorScheme.GetColorSchemeBlock(colourScheme);

			TMP_Dropdown dropdownPrefab = GameObject.Instantiate<TMP_Dropdown>(main.dropdownPrefab, main.transform);
			dropdownPrefabGO = dropdownPrefab.gameObject;
			dropdownPrefabGO.transform.SetParent(main.transform);
			
			dropdownPrefab.colors = colorBlock;
			dropdownPrefab.template.GetComponent<Image>().color = ColorScheme.GetColor(colours[3]);
			dropdownPrefab.template.GetComponentInChildren<Toggle>().colors = colorBlock;

			Color color5 = ColorScheme.GetColor(colours[5]);
			dropdownPrefab.captionText.color = color5;
			dropdownPrefab.captionText.fontStyle = fontStyles;
			dropdownPrefab.itemText.color = color5;
			dropdownPrefab.itemText.fontStyle = fontStyles;

			dropdownPrefab.template.GetComponent<ScrollRect>().verticalScrollbar.colors = colorBlock;

			//Preface has an AspectRatioFitter on each checkmark
			dropdownPrefab
				.GetComponentInChildren<AspectRatioFitter>()
				.GetComponent<Image>()
				.color = color5;
			dropdownPrefab
				.itemText
				.transform
				.parent
				.GetComponentInChildren<AspectRatioFitter>()
				.GetComponent<Image>()
				.color = color5;
		}
		
		GameObject dropdownGO = GameObject.Instantiate<GameObject>(dropdownPrefabGO, transform);
		return dropdownGO;
	}

	private Dictionary<int, GameObject> inputFieldDict = new Dictionary<int, GameObject>();
	public static GameObject InstantiateInputField(
		Transform transform,
		CS colourScheme=CS.BRIGHT,
		TMPro.FontStyles fontStyles=TMPro.FontStyles.Normal,
		TMP_InputField.ContentType contentType=TMP_InputField.ContentType.Standard
	) {
		GameObject inputPrefabGO;
		int key = (int)colourScheme;
		if (!main.inputFieldDict.TryGetValue(key, out inputPrefabGO)) {

			COL[] colours = ColorScheme.GetColorScheme(colourScheme);
			ColorBlock colorBlock = ColorScheme.GetColorSchemeBlock(colourScheme);

			TMP_InputField inputFieldPrefab = Instantiate<TMP_InputField>(main.inputFieldPrefab, main.transform);
			inputPrefabGO = inputFieldPrefab.gameObject;
			
			inputFieldPrefab.colors = colorBlock;
			inputFieldPrefab.textComponent.color = ColorScheme.GetColor(colours[5]);
		}
		
		GameObject inputFieldGO = GameObject.Instantiate<GameObject>(inputPrefabGO, transform);
		TMP_InputField inputField = inputFieldGO.GetComponent<TMP_InputField>();
		inputField.contentType = contentType;
		inputField.textComponent.fontStyle = fontStyles;
		return inputFieldGO;
	}

	private Dictionary<int, GameObject> toggleDict = new Dictionary<int, GameObject>();
	public static GameObject InstantiateToggle(
		Transform transform,
		COL colour,
		CS colourScheme=CS.BRIGHT
	) {
		GameObject togglePrefabGo;
		int key = (int)colour + 1024 * (int)colourScheme;
		if (!main.toggleDict.TryGetValue(key, out togglePrefabGo)) {

			COL[] colours = ColorScheme.GetColorScheme(colourScheme);
			ColorBlock colorBlock = ColorScheme.GetColorSchemeBlock(colourScheme);

			Toggle toggle = Instantiate<Toggle>(main.togglePrefab, main.transform);
			togglePrefabGo = toggle.gameObject;

			toggle.colors = colorBlock;
			toggle.graphic.color = ColorScheme.GetColor(colour);
		}
		return Instantiate<GameObject>(togglePrefabGo, transform);
	}

	private Dictionary<int, GameObject> scrollViewDict = new Dictionary<int, GameObject>();
	public static GameObject InstantiateScrollView(
		Transform transform,
		CS colourScheme=CS.BRIGHT
	) {
		GameObject scrollViewPrefabGO;
		int key = (int)colourScheme;
		if (!main.scrollViewDict.TryGetValue(key, out scrollViewPrefabGO)) {

			COL[] colours = ColorScheme.GetColorScheme(colourScheme);
			ColorBlock colorBlock = ColorScheme.GetColorSchemeBlock(colourScheme);

			ScrollRect scrollViewPrefab = Instantiate<ScrollRect>(main.scrollViewPrefab);
			scrollViewPrefabGO = scrollViewPrefab.gameObject;
			scrollViewPrefabGO.transform.SetParent(main.transform);
			
			scrollViewPrefab.GetComponent<Image>().color = ColorScheme.GetColor(colours[2]);
			scrollViewPrefab.horizontalScrollbar.colors = colorBlock;
			scrollViewPrefab.verticalScrollbar.colors = colorBlock;
		}
		
		GameObject scrollViewGO = GameObject.Instantiate<GameObject>(scrollViewPrefabGO, transform);
		
		return scrollViewGO;
	}

	private Dictionary<int, GameObject> scrollTextDict = new Dictionary<int, GameObject>();
	public static GameObject InstantiateScrollText(
		string textString,
		Transform transform,
		CS colourScheme=CS.DARK,
		TMPro.FontStyles fontStyles=TMPro.FontStyles.Normal,
		TMPro.TextAlignmentOptions textAlignmentOptions=TMPro.TextAlignmentOptions.Center

	) {
		GameObject scrollTextPrefabGO;
		int key = (int)colourScheme;
		if (!main.scrollTextDict.TryGetValue(key, out scrollTextPrefabGO)) {

			COL[] colours = ColorScheme.GetColorScheme(colourScheme);
			ColorBlock colorBlock = ColorScheme.GetColorSchemeBlock(colourScheme);

			ScrollText scrollTextPrefab = Instantiate<ScrollText>(main.scrollTextPrefab);
			scrollTextPrefabGO = scrollTextPrefab.gameObject;
			scrollTextPrefabGO.transform.SetParent(main.transform);
			
			scrollTextPrefab.scrollRect.horizontalScrollbar.colors = colorBlock;
			scrollTextPrefab.scrollRect.verticalScrollbar.colors = colorBlock;
		}
		
		GameObject scrollViewGO = GameObject.Instantiate<GameObject>(scrollTextPrefabGO, transform);
		TextMeshProUGUI textGOText = scrollViewGO.GetComponent<ScrollText>().text;
		textGOText.fontStyle = fontStyles;
		textGOText.alignment = textAlignmentOptions;
		textGOText.text = textString;
		
		return scrollViewGO;
	}

	public static GameObject InstantiateScrollBar(
		Transform transform,
		CS colourScheme=CS.DARK
	) {
		Scrollbar scrollbar = GameObject.Instantiate<Scrollbar>(main.scrollbarPrefab, transform);
		scrollbar.colors = ColorScheme.GetColorSchemeBlock(colourScheme);
		return scrollbar.gameObject;
	}
}