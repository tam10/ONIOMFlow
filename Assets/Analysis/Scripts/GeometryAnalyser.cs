using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;
using UnityEngine.UI;
using Unity.Mathematics;
using Element = Constants.Element;
using GIID = Constants.GeometryInterfaceID;
using TMPro;

public class GeometryAnalyser : MonoBehaviour {

	public TextMeshProUGUI nameText;
	public TextMeshProUGUI sizeText;
	public TextMeshProUGUI formulaText;

    public Canvas mainCanvas;
    public CanvasGroup canvasGroup;
    public RectTransform scrollviewContent;

    public RectTransform interactionMapTransform;
    public Button residueButtonPrefab;
    public LineRenderer lineRendererPrefab;

    Dictionary<AtomID, float3> backboneNCentres;
    Dictionary<AtomID, float3> backboneOCentres;

    Dictionary<AtomID, float3> ionicPositiveCentres;
    Dictionary<AtomID, float3> ionicNegativeCentres;

    Dictionary<AtomID, (float3, float3)> ringVectors;


    Map<int, ResidueID> residueMap;
    Dictionary<ResidueID, Dictionary<ResidueID, float>> hBondScores;
    Dictionary<ResidueID, Dictionary<ResidueID, float>> ionicScores;
    Dictionary<ResidueID, Dictionary<ResidueID, float>> ringScores;
    bool[] covalentConnections;

    int numResidues;

    public GeometryInterface geometryInterface;
    public Geometry geometry;
    public int size;
    public string atomsName;
    public string formula;

    float plotWidth;
    float plotHeight;

    
    public Color positiveColor = Color.red;
    public Color negativeColor = Color.blue;
    public Color ringColor = Color.white;
    public Color normalColor = Color.grey;
    public Color waterColor = Color.cyan;

    
    public Color hBondStartColor = new Color(0.7f, 0.7f, 0f);
    public Color hBondEndColor   = new Color(0f, 0.7f, 0.7f);
    public Color ringStartColor = new Color(0.7f, 0.7f, 0.7f);
    public Color ringEndColor   = new Color(0.7f, 0.7f, 0.7f);
    public Color ionicStartColor = new Color(1f, 0f, 0f);
    public Color ionicEndColor   = new Color(0f, 0f, 1f);

    public float buttonSeparation = 20f;

    public void Analyse(GIID geometryInterfaceID) {
        this.geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        this.geometry = geometryInterface.geometry;
        size = geometry.size;
        atomsName = geometry.name;
        formula = GetFormula();

        StartCoroutine(GetInteractionMap());
    }

    public void Awake() {
        Camera mainCamera = Camera.main;
        mainCanvas.worldCamera = mainCamera;
        float frustrumRatio = (
            2f * 
            math.abs(mainCanvas.transform.position.z - mainCamera.transform.position.z) * 
            math.tan(math.radians(mainCamera.fieldOfView * 0.5f))
        );

        float scale = frustrumRatio / Screen.height;

        mainCanvas.transform.localScale = new Vector3(scale, scale, scale);
    }


    public void Display() {
        nameText.text = atomsName;
        sizeText.text = size.ToString();
        formulaText.text = formula;
        mainCanvas.enabled = true;
        canvasGroup.alpha = 1f;
        //ColorScheme.SetLineGlowAmount(1f);
    }

    public void Hide() {
        //ColorScheme.SetLineGlowAmount();
        GameObject.Destroy(this.gameObject);
    }
    
    private IEnumerator GetInteractionMap() {

        geometryInterface.activeTasks++;

        CustomLogger.LogOutput("Analysis: ({0})", geometry.name);

        //Dictionary<ResidueID, float3> residueCentres = new Dictionary<ResidueID, float3>();

        backboneNCentres = new Dictionary<AtomID, float3>();
        backboneOCentres = new Dictionary<AtomID, float3>();

        ionicPositiveCentres = new Dictionary<AtomID, float3>();
        ionicNegativeCentres = new Dictionary<AtomID, float3>();

        ringVectors = new Dictionary<AtomID, (float3, float3)>();

        hBondScores = new Dictionary<ResidueID, Dictionary<ResidueID, float>>();
        ionicScores = new Dictionary<ResidueID, Dictionary<ResidueID, float>>();
        ringScores = new Dictionary<ResidueID, Dictionary<ResidueID, float>>();

        yield return null;

        yield return GetCentres();

        yield return ComputeBackboneHInteractions();
        yield return ComputeIonicInteractions();
        yield return ComputeRingInteractions();


        Display();

        yield return PlotScores();
        
        geometryInterface.activeTasks--;

    }

    IEnumerator PlotScores() {

        for (int residueIndex = 0; residueIndex < numResidues; residueIndex++) {

            float residueImageOffset = - residueIndex * buttonSeparation;

            ResidueID residueID = residueMap[residueIndex];
            Residue residue = geometry.GetResidue(residueID);

            Button residueButton = Instantiate<Button>(residueButtonPrefab, interactionMapTransform);
            residueButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, residueImageOffset);

            residueButton.image.color = GetResidueColour(residue.residueName);

            residueButton.onClick.AddListener(() => ResidueButtonClicked(residueID));

            Dictionary<ResidueID, float> residueScoreDict;

            if (hBondScores.TryGetValue(residueID, out residueScoreDict)) {
                DrawLinkedResidues(residueScoreDict, residueImageOffset, buttonSeparation, 0.25f, hBondStartColor, hBondEndColor);
            }

            if (ionicScores.TryGetValue(residueID, out residueScoreDict)) {
                DrawLinkedResidues(residueScoreDict, residueImageOffset, buttonSeparation, 0.30f, ionicStartColor, ionicEndColor);
            }

            if (ringScores.TryGetValue(residueID, out residueScoreDict)) {
                DrawLinkedResidues(residueScoreDict, residueImageOffset, buttonSeparation, 0.35f, ringStartColor, ringEndColor);
            }

            if (Timer.yieldNow) {
                yield return null;
            }

        }

        plotHeight = buttonSeparation * numResidues;

        interactionMapTransform.sizeDelta = new Vector2(plotWidth, plotHeight);

        StartCoroutine(RenderPNG());
    }

    void ResidueButtonClicked(ResidueID residueID) {
        ContextMenu contextMenu = ContextMenu.main;
        contextMenu.Clear();

        Residue residue = geometry.GetResidue(residueID);

        contextMenu.AddButton(() => {}, string.Format("{0} ({1})", residueID, residue.residueName), true);
        
        contextMenu.AddSpacer();

        Dictionary<ResidueID, float> residueScoreDict;
        if (hBondScores.TryGetValue(residueID, out residueScoreDict)) {
            contextMenu.AddButton(() => {}, string.Format("Backbone Interactions:"), true);
            foreach ((ResidueID linkedResidueID, float score) in residueScoreDict) {
                Residue linkedResidue = geometry.GetResidue(linkedResidueID);
                contextMenu.AddButton(() => {}, string.Format("{0} ({1}): {2:P}", linkedResidueID, linkedResidue.residueName, score), true);
            }
            contextMenu.AddSpacer();
        }

        if (ionicScores.TryGetValue(residueID, out residueScoreDict)) {
            contextMenu.AddButton(() => {}, string.Format("Ionic Interactions:"), true);
            foreach ((ResidueID linkedResidueID, float score) in residueScoreDict) {
                Residue linkedResidue = geometry.GetResidue(linkedResidueID);
                contextMenu.AddButton(() => {}, string.Format("{0} ({1}): {2:P}", linkedResidueID, linkedResidue.residueName, score), true);
            }
            contextMenu.AddSpacer();
        }

        if (ringScores.TryGetValue(residueID, out residueScoreDict)) {
            contextMenu.AddButton(() => {}, string.Format("Ring Interactions:"), true);
            foreach ((ResidueID linkedResidueID, float score) in residueScoreDict) {
                Residue linkedResidue = geometry.GetResidue(linkedResidueID);
                contextMenu.AddButton(() => {}, string.Format("{0} ({1}): {2:P}", linkedResidueID, linkedResidue.residueName, score), true);
            }
        }

        contextMenu.Show();
    }

    void DrawLinkedResidues(Dictionary<ResidueID, float> residueScoreDict, float residueImageOffset, float buttonSeparation, float xMultiplier, Color startColor, Color endColor) {
        foreach ((ResidueID linkedResidueID, float score) in residueScoreDict) {

            float linkedResidueImageOffset = - residueMap[linkedResidueID] * buttonSeparation;
            float midX = math.abs(linkedResidueImageOffset - residueImageOffset) * xMultiplier;
            float midY = (linkedResidueImageOffset + residueImageOffset) * 0.5f;

            plotWidth = math.max(midX * 2f + 10f, plotWidth);

            float3[] points;
            
            if (linkedResidueImageOffset < residueImageOffset) {
                points = new float3[4] {
                    new float3(5f, residueImageOffset, 0f),
                    new float3(midX + 5f, midY + residueImageOffset, 0f) * 0.5f,
                    new float3(midX + 5f, midY + linkedResidueImageOffset, 0f) * 0.5f,
                    new float3(5f, linkedResidueImageOffset, 0f)
                };
            } else {
                points = new float3[4] {
                    new float3(-5f, residueImageOffset, 0f),
                    new float3(-(midX + 5f), midY + residueImageOffset, 0f) * 0.5f,
                    new float3(-(midX + 5f), midY + linkedResidueImageOffset, 0f) * 0.5f,
                    new float3(-5f, linkedResidueImageOffset, 0f)
                };
            }

            Vector3[] bezier = CustomMathematics.GetBezier(points, 40).Select(x => (Vector3)x).ToArray();

            LineRenderer lineRenderer = GameObject.Instantiate<LineRenderer>(lineRendererPrefab, interactionMapTransform);

            startColor.a = score;
            endColor.a = score;

            lineRenderer.positionCount = bezier.Length;
            lineRenderer.useWorldSpace = false;
            lineRenderer.startWidth = 0.2f;
            lineRenderer.endWidth = 0.2f;
            lineRenderer.textureMode = LineTextureMode.DistributePerSegment;
            lineRenderer.material = ColorScheme.GetMaskedLineGlowMaterial();
            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
            lineRenderer.SetPositions(bezier);
        }
    }

    IEnumerator RenderPNG() {

        Shader oldShader = GameObject.Instantiate<Shader>(lineRendererPrefab.material.shader);
        Shader lineShader = Shader.Find("Unlit/LineGlow");

        foreach (LineRenderer lineRenderer in interactionMapTransform.GetComponentsInChildren<LineRenderer>()) {
            lineRenderer.material.shader = lineShader;
            lineRenderer.startWidth = 1f;
            lineRenderer.endWidth = 1f;
        }

        yield return PNGCreator.ImageFromCanvas(interactionMapTransform, "test.png", 320);

        foreach (LineRenderer lineRenderer in interactionMapTransform.GetComponentsInChildren<LineRenderer>()) {
            lineRenderer.material.shader = oldShader;
            lineRenderer.startWidth = 0.2f;
            lineRenderer.endWidth = 0.2f;
        }
    }


    IEnumerator GetCentres() {

        Residue previousResidue = null;
        numResidues = geometry.residueCount;
        covalentConnections = new bool[numResidues - 1];

        StringBuilder sb = new StringBuilder();

        List<ResidueID> residueIDs = geometry.EnumerateResidueIDs().OrderBy(x => x).ToList();
        int residueIndex = 0;
        residueMap = residueIDs.ToMap(x => residueIndex++, x => x);

        int residueCount = 0;
        foreach (ResidueID residueID in residueIDs) {

            Residue residue = geometry.GetResidue(residueID);

            //Is this residue connected to the previous?
            if (residueCount != 0) {
                bool connected = previousResidue.NeighbouringResidues().Contains(residueID);
                covalentConnections[residueCount - 1] = connected;
                string connectionString = connected ? "-" : " ";
                sb.Append(connectionString);
            }
            sb.Append(residue.residueName);
            previousResidue = residue;

            residueCount++;

            foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
                switch (pdbID.element) {

                    case Element.N:
                        if (pdbID.identifier == "") {
                            backboneNCentres[new AtomID(residueID, pdbID)] = atom.position;
                        }
                        break;

                    case Element.O:
                        if (pdbID.identifier == "") {
                            backboneOCentres[new AtomID(residueID, pdbID)] = atom.position;
                        }
                        break;
                }
            }

            if (residue.standard || residue.isWater) {

                List<PDBID> negativeChargePDBIDs;
                if (Settings.negativeChargeSites.TryGetValue(residue.residueName, out negativeChargePDBIDs)) {
                    foreach (PDBID pdbID in negativeChargePDBIDs) {
                        AddPosition(ionicNegativeCentres, residue, residueID, pdbID);
                    }
                }

                List<PDBID> positiveChargePDBIDs;
                if (Settings.positiveChargeSites.TryGetValue(residue.residueName, out positiveChargePDBIDs)) {
                    foreach (PDBID pdbID in positiveChargePDBIDs) {
                        AddPosition(ionicPositiveCentres, residue, residueID, pdbID);
                    }
                }

                List<List<PDBID>> ringPDBIDList;
                if (Settings.ringSites.TryGetValue(residue.residueName, out ringPDBIDList)) {
                    foreach (List<PDBID> pdbIDList in ringPDBIDList) {
                        AddRing(ringVectors, residue, residueID, pdbIDList);
                    }
                }
            }

            if (Timer.yieldNow) {
                yield return null;
            }
        }
    
        CustomLogger.LogOutput("{0} Residues", residueCount);
        CustomLogger.LogOutput(sb.ToString());
        CustomLogger.LogOutput("{0} Positive Centres", ionicPositiveCentres.Count);
        CustomLogger.LogOutput("{0} Negative Centres", ionicNegativeCentres.Count);
        CustomLogger.LogOutput("{0} Rings", ringVectors.Count);
    }

    IEnumerator ComputeBackboneHInteractions() {
        
        float upperNOBondDistanceSq = 12f;
        float lowerNOBondDistanceSq = 6f;
        //Compute backbone H-bonds
        foreach ((AtomID nAtomID, float3 nPosition) in backboneNCentres) {
            foreach ((AtomID oAtomID, float3 oPosition) in backboneOCentres) {

                if (
                    nAtomID.residueID == oAtomID.residueID ||
                    nAtomID.residueID == oAtomID.residueID.GetNextID() ||
                    nAtomID.residueID == oAtomID.residueID.GetPreviousID()
                ) {
                    continue;
                }

                float distancesq = math.distancesq(nPosition, oPosition);
                if (distancesq < upperNOBondDistanceSq) { 
                    
                    float score = CustomMathematics.Map(
                        distancesq,
                        upperNOBondDistanceSq,
                        lowerNOBondDistanceSq,
                        0f,
                        1f
                    );

                    AddScore(hBondScores, nAtomID.residueID, oAtomID.residueID, score);
                }
            }

            if (Timer.yieldNow) {
                yield return null;
            }
        }
        CustomLogger.LogOutput("{0} Backbone Hydrogen Bond Interactions", hBondScores.Count);
        
    }

    IEnumerator ComputeIonicInteractions() {
        
        float upperIonicDistanceSq = 16f;
        float lowerIonicDistanceSq = 6f;

        //Compute ionic interactions
        foreach ((AtomID posAtomID, float3 posPosition) in ionicPositiveCentres) {
            foreach ((AtomID negAtomID, float3 negPosition) in ionicNegativeCentres) {

                if (posAtomID.residueID == negAtomID.residueID) {
                    continue;
                }

                float distancesq = math.distancesq(posPosition, negPosition);
                if (distancesq < upperIonicDistanceSq) { 
                    
                    float score = CustomMathematics.Map(
                        distancesq,
                        upperIonicDistanceSq,
                        lowerIonicDistanceSq,
                        0f,
                        1f
                    );

                    AddScore(ionicScores, posAtomID.residueID, negAtomID.residueID, score);
                }
            }

            if (Timer.yieldNow) {
                yield return null;
            }
        }
        
        CustomLogger.LogOutput("{0} Ionic Interactions", ionicScores.Count);
    }

    IEnumerator ComputeRingInteractions() {
        
        float stackDotMin = Mathf.Sqrt(3f) * 0.5f;
        float sideonDotMax = 0.5f;

        float upperSideonDistanceSq = 32f;
        float lowerSideonDistanceSq = 16f;
        float upperRingDistanceSq = 25f;
        float lowerRingDistanceSq = 10f;

        foreach ( (
            KeyValuePair<AtomID, (float3, float3)> ring1, 
            KeyValuePair<AtomID, (float3, float3)> ring2
        ) in ringVectors.UpperTriangle()) {

            float distancesq = math.distancesq(ring1.Value.Item1, ring2.Value.Item1);

            if (distancesq > upperSideonDistanceSq) {
                continue;
            }

            float dot = math.abs(math.dot(math.normalize(ring1.Value.Item2), math.normalize(ring2.Value.Item2)));

            float score;
            if (dot > stackDotMin) {
                score = CustomMathematics.Map(
                    distancesq,
                    upperRingDistanceSq,
                    lowerRingDistanceSq,
                    0f,
                    1f
                ) * CustomMathematics.Map(
                    dot,
                    stackDotMin,
                    1f,
                    0f,
                    1f
                );

            } else if (dot < sideonDotMax) {
                score = CustomMathematics.Map(
                    distancesq,
                    upperSideonDistanceSq,
                    lowerSideonDistanceSq,
                    0f,
                    1f
                ) * CustomMathematics.Map(
                    dot,
                    0f,
                    sideonDotMax,
                    0f,
                    1f
                );

            } else {
                continue;
            }

            AddScore(ringScores, ring1.Key.residueID, ring2.Key.residueID, score);

            if (Timer.yieldNow) {
                yield return null;
            }
        }
        
        CustomLogger.LogOutput("{0} Ring Interactions", ringScores.Count);
    }

    void AddPosition(Dictionary<AtomID, float3> positionDict, Residue residue, ResidueID residueID, PDBID pdbID) {
        
        foreach ((PDBID atomPDBID, Atom atom) in residue.EnumerateAtoms()) {
            if (pdbID == atomPDBID) {
                positionDict[new AtomID(residueID, pdbID)] = atom.position;
            }
        }
    }

    void AddRing(Dictionary<AtomID, (float3, float3)> ringDict, Residue residue, ResidueID residueID, List<PDBID> pdbIDs) {

        List<float3> positions = new List<float3>();
        float3 sum = float3.zero;
        foreach ((PDBID pdbID, Atom atom) in residue.EnumerateAtoms()) {
            if (pdbIDs.Contains(pdbID)) {
                positions.Add(atom.position);
                sum += atom.position;
            }
        }
        int length = positions.Count;
        if (length < 3) {
            return;
        }
        
        float3 averagePosition = sum / length;

        //Get the normal vector to the ring
        float3 firstVector = positions[0] - averagePosition;
        float3 crossVector = float3.zero;
        float crossMagSq = 0f;

        //Compare vectors to the vector from the first position to the average position
        //The largest vector cross product will be used as the norm to the ring
        for (int vertexIndex=1; vertexIndex<length; vertexIndex++) {
            float3 newVector = positions[vertexIndex] - averagePosition;
            float3 newCrossVector = math.cross(newVector, firstVector);
            float newMagSq = math.lengthsq(newCrossVector);
            if (newMagSq > crossMagSq) {
                crossVector = newCrossVector;
            }
        }

        ringDict[new AtomID(residueID, pdbIDs[0])] = (averagePosition, crossVector);
    }

    void AddScore(Dictionary<ResidueID, Dictionary<ResidueID, float>> scoreDict, ResidueID residueID1, ResidueID residueID2, float score) {

        Dictionary<ResidueID, float> residueScoreDict;

        if (scoreDict.TryGetValue(residueID1, out residueScoreDict)) {
            float residueScore;
            if (residueScoreDict.TryGetValue(residueID2, out residueScore)) {
                residueScore += score;
            } else {
                residueScoreDict[residueID2] = score;
            }
        } else {
            scoreDict[residueID1] = new Dictionary<ResidueID, float> {{residueID2, score}};
        }
    }

    public string GetFormula() {
        // Get the Hill system of ordered elements
        List<Element> uniqueElements = GetUniqueElements(geometry);

        StringBuilder sb = new StringBuilder();

        if (uniqueElements.Contains(Element.C)) {
            sb.AppendFormat("C<sub>{0}</sub>", CountElement(geometry, Element.C));
            uniqueElements.Remove(Element.C);
        }

        if (uniqueElements.Contains(Element.H)) {
            sb.AppendFormat("H<sub>{0}</sub>", CountElement(geometry, Element.H));
            uniqueElements.Remove(Element.H);
        }

        uniqueElements.Sort();
        foreach (Element uniqueElement in uniqueElements) {
            sb.AppendFormat(
                "{0}<sub>{1}</sub>", 
                uniqueElement, 
                CountElement(geometry, uniqueElement)
            );
        }

        return sb.ToString();
    }

    public static List<Element> GetUniqueElements(Geometry geometry) {
        return geometry
            .EnumerateAtomIDs()
            .Select(x => x.pdbID.element)
            .Distinct()
            .ToList();
    }

    public static int CountElement(Geometry geometry, Element element) {
        return geometry
            .EnumerateAtomIDs()
            .Count(x => x.pdbID.element == element);
    }

    public Color GetResidueColour(string residueName) {
        switch (residueName) {
            case "LYS":
            case "HIP":
            case "ARG":
                return positiveColor;
            case "ASP":
            case "GLU":
                return negativeColor;
            case "PHE":
            case "TYR":
            case "TRP":
                return ringColor;
            case "HOH":
            case "WAT":
                return waterColor;
        }
        return normalColor;
    }

}
