using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Element = Constants.Element;
using OLID = Constants.OniomLayerID;
using GIID = Constants.GeometryInterfaceID;
using TID = Constants.TaskID;
using GIS = Constants.GeometryInterfaceStatus;
using GOT = Constants.GaussianOptTarget;
using GCT = Constants.GaussianConvergenceThreshold;
using GFC = Constants.GaussianForceConstant;
using EL = Constants.ErrorLevel;
using BT = Constants.BondType;

public class CalculationSetup : MonoBehaviour {
    private static CalculationSetup _main;
    public  static CalculationSetup main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<CalculationSetup>();
            return _main;
        }
    }

    public Canvas canvas;
    public Button closeButton;
    public Button confirmButton;
    private Geometry geometry;
    
    //Tabs at the top of the interface
    [Header("Tabs")]
    public Button RealLayerTab;
    public Button IntermediateLayerTab;
    public Button ModelLayerTab;
    public Button OptimisationTab;
    public Button FrequencyTab;
    public Button OptionsTab;


    
    [Header("Layers")]
    public Canvas LayersCanvas;
    public TMP_InputField NumberOfAtomsResult;
    public TMP_InputField NumberOfResiduesResult;
    public TMP_InputField MethodsInput;
    public TMP_InputField BasisInput;
    public TMP_InputField MethodOptionsInput;
    public TMP_InputField SumOfChargesResult;
    public TMP_InputField ChargeInput;
    public TMP_InputField NumberOfElectronsResult;
    public TMP_InputField MultiplicityInput;


    [Header("Optimisation")]
    public Canvas OptimisationCanvas;
    public Toggle DoOptimisationToggle;
    public TMP_InputField NumStepsInput;
    public TMP_InputField StepSizeInput;
    public TMP_Dropdown GeometryTargetDropdown;
    public TMP_Dropdown ConvergenceCriteriaDropdown;
    public TMP_Dropdown ForceConstantsDropdown;
    public TMP_InputField RecomputeFCsInput;
    public Toggle DoMicroiterationsToggle;
    public Toggle DoQuadMacroToggle;


    [Header("Frequency")]
    public Canvas FrequencyCanvas;
    public Toggle DoFrequencyToggle;
    public Toggle UseHighPrecisionModesToggle;


    [Header("Options")]
    public Canvas OptionsCanvas;
    public TMP_InputField FileNameInput;
    public TMP_InputField OldChkFileNameInput;
    public TMP_InputField MemoryInput;
    public TMP_InputField NProcInput;


    [Header("UI")]
    public bool userResponded;
    public bool cancelled;

    //Atoms
    private IEnumerable<OLID> oniomLayerIDs;
    private Dictionary<OLID, int> layerAtomCount;
    private Dictionary<OLID, int> layerResidueCount;
    private Dictionary<OLID, float> layerCharges;
    private Dictionary<OLID, int> layerElectronCount;
    private Dictionary<OLID, Geometry> layerGeometryDict;
    private List<Link> links;

    private OLID currentLayerID;

    void Awake() {

        layerAtomCount = new Dictionary<OLID, int> ();
        layerResidueCount = new Dictionary<OLID, int> ();
        layerCharges = new Dictionary<OLID, float> ();
        layerElectronCount = new Dictionary<OLID, int> ();
        layerGeometryDict = new Dictionary<OLID, Geometry> ();

        confirmButton.onClick.AddListener(Confirm);
        closeButton.onClick.AddListener(Cancel);

        RealLayerTab.onClick.AddListener(ShowRealLayer);
        IntermediateLayerTab.onClick.AddListener(ShowIntermediateLayer);
        ModelLayerTab.onClick.AddListener(ShowModelLayer);
        OptimisationTab.onClick.AddListener(ShowOptimisation);
        FrequencyTab.onClick.AddListener(ShowFrequency);
        OptionsTab.onClick.AddListener(ShowOptions);
    }

    public static IEnumerator SetupCalculation(GIID startID, GIID targetID, List<TID> taskIDs) {
        Flow.GetGeometryInterface(targetID).activeTasks++;
        Flow.GetGeometryInterface(targetID).status = GIS.OK;
        yield return Flow.CopyGeometry(GIID.COMBINED, targetID);
        
        foreach (TID taskID in taskIDs) {
            yield return RunTask(targetID, taskID);
        }
        
        Flow.GetGeometryInterface(targetID).activeTasks--;
        Flow.GetGeometryInterface(startID).status = GIS.COMPLETED;
    }

    public static IEnumerator GetModelLayer(GIID startID, GIID targetID, List<TID> taskIDs) {
        Flow.GetGeometryInterface(targetID).activeTasks++;
        Flow.GetGeometryInterface(targetID).status = GIS.OK;
        yield return Flow.CopyGeometry(GIID.COMBINED, targetID);
        
        foreach (TID taskID in taskIDs) {
            yield return RunTask(targetID, taskID);
        }
        
        Flow.GetGeometryInterface(targetID).activeTasks--;
        Flow.GetGeometryInterface(GIID.COMBINED).status = GIS.COMPLETED;
    }

    public static IEnumerator GetIntermediateLayer(GIID startID, GIID targetID, List<TID> taskIDs) {
        Flow.GetGeometryInterface(targetID).activeTasks++;
        Flow.GetGeometryInterface(targetID).status = GIS.OK;
        yield return Flow.CopyGeometry(GIID.COMBINED, targetID);
        
        foreach (TID taskID in taskIDs) {
            yield return RunTask(targetID, taskID);
        }
        
        Flow.GetGeometryInterface(targetID).activeTasks--;
        Flow.GetGeometryInterface(GIID.COMBINED).status = GIS.COMPLETED;
    }

    private static IEnumerator RunTask(GIID geometryInterfaceID, TID taskID) {
        ArrowFunctions.Task task = ArrowFunctions.GetTask(taskID);
        yield return task(geometryInterfaceID);
    }

    public static IEnumerator SetupCalculation(Geometry geometry) {

        NotificationBar.SetTaskProgress(TID.SETUP_CALCULATION, 0f);
        main.geometry = geometry;

        yield return main.Initialise();
        while (!main.userResponded) {
            yield return null;
        }


        NotificationBar.ClearTask(TID.SETUP_CALCULATION);
    }

    public static IEnumerator SetupCalculation(GIID geometryInterfaceID) {

        NotificationBar.SetTaskProgress(TID.SETUP_CALCULATION, 0f);
        main.geometry = Flow.GetGeometry(geometryInterfaceID);

        GIS status = Flow.GetGeometryInterface(geometryInterfaceID).status;
        if (status != GIS.OK && status != GIS.COMPLETED && status != GIS.WARNING) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Geometry Interface '{0}' is not in an eligible state - Cannot proceed.",
                geometryInterfaceID
            );
            NotificationBar.ClearTask(TID.SETUP_CALCULATION);
            yield break;
        }

        yield return main.Initialise();
        while (!main.userResponded) {
            yield return null;
        }


        NotificationBar.ClearTask(TID.SETUP_CALCULATION);
    }

    public static IEnumerator MoveAllToLayer(GIID geometryInterfaceID, OLID oniomLayerID, TID taskID) {
        
        NotificationBar.SetTaskProgress(taskID, 0f);
        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        Geometry geometry = geometryInterface.geometry;
        yield return null;

        foreach (Atom atom in geometry.EnumerateAtoms()) {
            atom.oniomLayer = oniomLayerID;
        }
        
        NotificationBar.ClearTask(taskID);
    }

    public static IEnumerator MoveSelectionToLayer(GIID geometryInterfaceID, OLID oniomLayerID, TID taskID) {
        
        NotificationBar.SetTaskProgress(taskID, 0f);
        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        Geometry geometry = geometryInterface.geometry;
        yield return null;

        List<(AtomID AtomID, Atom atom)> selection = new List<(AtomID AtomID, Atom atom)>();
        geometry.GetUserSelection(selection);
        foreach ((AtomID atomID, Atom atom) in selection) {
            atom.oniomLayer = oniomLayerID;
        }
        
        NotificationBar.ClearTask(taskID);
    }

    public static IEnumerator GetLayer(GIID geometryInterfaceID, OLID oniomLayerID, TID taskID) {

        NotificationBar.SetTaskProgress(taskID, 0f);
        GeometryInterface geometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        Geometry geometry = geometryInterface.geometry;
        yield return null;
        
        IEnumerable<OLID> oniomLayerIDs = geometry.GetLayers();
        if (!oniomLayerIDs.Contains(oniomLayerID)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Geometry Interface '{0}' must have a defined {1} Layer.",
                geometryInterfaceID,
                oniomLayerID
            );
            NotificationBar.ClearTask(taskID);
            yield break;
        }

        List<Link> links = geometry.GetLinks();

        Geometry layerGeometry = geometry.TakeLayer(oniomLayerID, geometry.transform);

        //Convert links to new protons
        foreach (Link link in links) {
            if (link.connectionLayerID != oniomLayerID) {
                continue;
            }

            AtomID hostAtomID = link.hostAtomID;
            AtomID connectionAtomID = link.connectionAtomID;
            Atom connectionAtom = geometry.GetAtom(connectionAtomID);
            Atom hostAtom = geometry.GetAtom(hostAtomID);

            Atom linkAtom = new Atom(
                hostAtom.position, 
                link.connectionAtomID.residueID,
                Data.GetLinkType(connectionAtom, connectionAtomID.pdbID),
                0f, 
                oniomLayerID
            );

            float scaleFactor = 
                Data.GetBondDistances(Element.H, connectionAtomID.pdbID.element)[0] / 
                Data.GetBondDistances(hostAtomID.pdbID.element, connectionAtomID.pdbID.element)[0];
            CustomMathematics.ScaleDistance(connectionAtom, linkAtom, scaleFactor, 0f);
            
            PDBID linkPDBID = connectionAtomID.pdbID;
            linkPDBID.element = Element.H;

            ResidueID linkConnectionResidueID = link.connectionAtomID.residueID;
            Residue residue;
            if (!layerGeometry.TryGetResidue(linkConnectionResidueID, out residue)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Failed to generate Link - Could not find Residue '{0}' in Geometry!",
                    linkConnectionResidueID
                );
            }
            PDBID acceptedPDBID;
            residue.AddAtom(linkPDBID, linkAtom, out acceptedPDBID);

            //Connect link atom
            layerGeometry.Connect(
                new AtomID(linkConnectionResidueID, acceptedPDBID),
                connectionAtomID,
                BT.SINGLE
            );

            CustomLogger.LogFormat(
                EL.VERBOSE, 
                "Link between {0} ({1}) and {2} ({3}). Using scale factor {4}",
                link.connectionLayerID,
                connectionAtomID,
                link.hostLayerID,
                hostAtomID,
                scaleFactor
            );

        }
        yield return geometryInterface.SetGeometry(layerGeometry);

        NotificationBar.ClearTask(taskID);

    }

    public static IEnumerator GetModelLayer(GIID geometryInterfaceID) {
        yield return GetLayer(geometryInterfaceID, OLID.MODEL, TID.GET_MODEL_LAYER);
    }

    public static IEnumerator GetIntermediateLayer(GIID geometryInterfaceID) {
        yield return GetLayer(geometryInterfaceID, OLID.INTERMEDIATE, TID.GET_INTERMEDIATE_LAYER);
    }

    public static IEnumerator MoveAllToModelLayer(GIID geometryInterfaceID) {
        yield return MoveAllToLayer(geometryInterfaceID, OLID.MODEL, TID.MOVE_ALL_TO_MODEL_LAYER);
    } 

    public static IEnumerator MoveAllToIntermediateLayer(GIID geometryInterfaceID) {
        yield return MoveAllToLayer(geometryInterfaceID, OLID.INTERMEDIATE, TID.MOVE_ALL_TO_INTERMEDIATE_LAYER);
    } 

    public static IEnumerator MoveAllToRealLayer(GIID geometryInterfaceID) {
        yield return MoveAllToLayer(geometryInterfaceID, OLID.REAL, TID.MOVE_ALL_TO_REAL_LAYER);
    } 

    public static IEnumerator MoveSelectionToModelLayer(GIID geometryInterfaceID) {
        yield return MoveSelectionToLayer(geometryInterfaceID, OLID.MODEL, TID.MOVE_SELECTION_TO_MODEL_LAYER);
    } 

    public static IEnumerator MoveSelectionToIntermediateLayer(GIID geometryInterfaceID) {
        yield return MoveSelectionToLayer(geometryInterfaceID, OLID.INTERMEDIATE, TID.MOVE_SELECTION_TO_INTERMEDIATE_LAYER);
    } 

    public static IEnumerator MoveSelectionToRealLayer(GIID geometryInterfaceID) {
        yield return MoveSelectionToLayer(geometryInterfaceID, OLID.REAL, TID.MOVE_SELECTION_TO_REAL_LAYER);
    } 

    public static IEnumerator ValidateGeometry(GIID geometryInterfaceID) {

        Geometry geometry = Flow.GetGeometry(geometryInterfaceID);
        IEnumerable<OLID> oniomLayerIDs = geometry.GetLayers();
        
        bool hasReal = oniomLayerIDs.Contains(OLID.REAL);
        bool hasIntermediate = oniomLayerIDs.Contains(OLID.INTERMEDIATE);
        bool hasModel = oniomLayerIDs.Contains(OLID.MODEL);

        GIS status = GIS.ERROR;
        switch (geometryInterfaceID) {
            case (GIID.ONELAYER):
                if (hasReal) {
                    status = GIS.OK;
                } else {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Geometry Interface '{0}' must have a Real Layer for a One-Layer calculation.", 
                        geometryInterfaceID
                    );
                }
                break;
            case (GIID.TWOLAYER):
                if (hasReal && (hasModel || hasIntermediate)) {
                    status = GIS.OK;
                } else {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Geometry Interface '{0}' must have a Real Layer and one other Layer for a Two-Layer calculation.", 
                        geometryInterfaceID
                    );
                }
                break;
            case (GIID.THREELAYER):
                if (hasReal && hasIntermediate && hasModel) {
                    status = GIS.OK;
                } else {
                    CustomLogger.LogFormat(
                        EL.ERROR,
                        "Geometry Interface '{0}' must have a Real, Intermediate and Model Layer for a Three-Layer calculation.", 
                        geometryInterfaceID
                    );
                }
                break;
        }
        Flow.GetGeometryInterface(geometryInterfaceID).status = status;
        yield break;

    }
    
    public IEnumerator Initialise() {

        oniomLayerIDs = geometry.GetLayers();

        RealLayerTab.interactable = oniomLayerIDs.Contains(OLID.REAL);
        IntermediateLayerTab.interactable = oniomLayerIDs.Contains(OLID.INTERMEDIATE);
        ModelLayerTab.interactable = oniomLayerIDs.Contains(OLID.MODEL);

        if (!oniomLayerIDs.Contains(OLID.REAL)) {
            if (oniomLayerIDs.Contains(OLID.INTERMEDIATE)) {
                ShowIntermediateLayer();
            } else if (oniomLayerIDs.Contains(OLID.MODEL)) {
                ShowModelLayer();
            }
        }

        yield return GetLayerProperties(geometry);

        userResponded = false;
        cancelled = false;
        Show();
    }

    IEnumerator GetLayerProperties(Geometry geometry) {

        //Ensure the Gaussian Calculator has a valid layer for each layer specified in geometry
        foreach (OLID oniomLayerID in oniomLayerIDs) {
            if (! geometry.gaussianCalculator.layerDict.ContainsKey(oniomLayerID)) {
                geometry.gaussianCalculator.layerDict[oniomLayerID] = new Layer(oniomLayer:oniomLayerID);
            }
            if (Timer.yieldNow) {yield return null;}
        }

        //Get Link atoms
        links = geometry.GetLinks();

        //Get the layer atoms;
        foreach (KeyValuePair<OLID, Layer> oniomLayer in geometry.gaussianCalculator.layerDict) {
            OLID oniomLayerID = oniomLayer.Key;
            Geometry layerGeometry = oniomLayer.Value.GenerateLayerGeometry(geometry, transform);

            layerGeometryDict[oniomLayerID] = layerGeometry;
            layerAtomCount[oniomLayerID] = layerGeometry.size;
            layerResidueCount[oniomLayerID] = layerGeometry.residueCount;
            layerCharges[oniomLayerID] = layerGeometry.GetCharge();

            //Electron count includes one electron for each link from this layer
            layerElectronCount[oniomLayerID] = layerGeometry
                .EnumerateAtomIDs()
                .Select(x => x.pdbID.atomicNumber)
                .Sum() +
                links.Count(x => x.connectionLayerID == oniomLayerID);

            if (Timer.yieldNow) {yield return null;}
        }

        if (oniomLayerIDs.Contains(OLID.REAL)) {
            ShowRealLayer();
        }
    }

    void HideTabs() {
        LayersCanvas.enabled = false;
        OptimisationCanvas.enabled = false;
        FrequencyCanvas.enabled = false;
        OptionsCanvas.enabled = false;
    }

    void ShowRealLayer() {
        currentLayerID = OLID.REAL;
        ShowLayers();
    }

    void ShowIntermediateLayer() {
        currentLayerID = OLID.INTERMEDIATE;
        ShowLayers();
    }

    void ShowModelLayer() {
        currentLayerID = OLID.MODEL;
        ShowLayers();
    }

    void ShowLayers() {
        HideTabs();

        NumberOfAtomsResult.text = layerAtomCount[currentLayerID].ToString();
        NumberOfResiduesResult.text = layerResidueCount[currentLayerID].ToString();
        NumberOfElectronsResult.text = layerElectronCount[currentLayerID].ToString();
        SumOfChargesResult.text = layerCharges[currentLayerID].ToString();

        Layer currentLayer = GetCurrentLayer();

        string method = currentLayer.method;
        CheckLayerMethod(method);
        MethodsInput.text = method;

        BasisInput.text = currentLayer.basis;
        MethodOptionsInput.text = string.Join(" ", currentLayer.options);

        int charge = currentLayer.charge;
        ChargeInput.text = charge.ToString();

        string multiplicityString = currentLayer.multiplicity.ToString();
        int multiplicity;
        CheckLayerMultiplicity(multiplicityString, charge, layerElectronCount[currentLayerID], out multiplicity);
        MultiplicityInput.text = currentLayer.multiplicity.ToString();

        MethodsInput.onEndEdit.AddListener(ChangeLayerMethod);
        BasisInput.onEndEdit.AddListener(ChangeLayerBasis);
        ChargeInput.onEndEdit.AddListener(ChangeLayerCharge);
        MultiplicityInput.onEndEdit.AddListener(ChangeLayerMultiplicity);

        MethodsInput.onSubmit.AddListener(ChangeLayerMethod);
        BasisInput.onSubmit.AddListener(ChangeLayerBasis);
        ChargeInput.onSubmit.AddListener(ChangeLayerCharge);
        MultiplicityInput.onSubmit.AddListener(ChangeLayerMultiplicity);

        LayersCanvas.enabled = true;
    }

    void ShowOptimisation() {
        HideTabs();

        GaussianCalculator gaussianCalculator = GetGaussianCalculator();

        DoOptimisationToggle.isOn = gaussianCalculator.doOptimisation;
        DoMicroiterationsToggle.isOn = gaussianCalculator.doMicroiterations;
        DoQuadMacroToggle.isOn = gaussianCalculator.doQuadMacro;

        NumStepsInput.text = gaussianCalculator.numOptSteps.ToString();
        StepSizeInput.text = gaussianCalculator.optStepSize.ToString();

        //This line just creates a list of strings from the GaussianOptimisationType enum
        List<string> geometryTargetOptions = System.Enum.GetValues(typeof(GOT))
                .Cast<GOT>()
                .Select(x => Constants.GaussianOptTargetMap[x])
                .ToList();
        GeometryTargetDropdown.ClearOptions();
        GeometryTargetDropdown.AddOptions(geometryTargetOptions);

        List<string> convergenceThresholdOptions = System.Enum.GetValues(typeof(GCT))
                .Cast<GCT>()
                .Select(x => Constants.GaussianConvergenceThresholdMap[x])
                .ToList();
        ConvergenceCriteriaDropdown.ClearOptions();
        ConvergenceCriteriaDropdown.AddOptions(convergenceThresholdOptions);

        List<string> forceConstantOptions = System.Enum.GetValues(typeof(GFC))
                .Cast<GFC>()
                .Select(x => Constants.GaussianForceConstantMap[x])
                .ToList();
        ForceConstantsDropdown.ClearOptions();
        ForceConstantsDropdown.AddOptions(forceConstantOptions);

        RecomputeFCsInput.text = "0";

        GeometryTargetDropdown.onValueChanged.AddListener(ChangeGeometryOptimisationTarget);
        ConvergenceCriteriaDropdown.onValueChanged.AddListener(ChangeConvergenceThreshold);
        ForceConstantsDropdown.onValueChanged.AddListener(ChangeForceConstantOption);

        DoOptimisationToggle.onValueChanged.AddListener(ChangeDoOptimisation);
        DoMicroiterationsToggle.onValueChanged.AddListener(ChangeDoMicroiterations);
        DoQuadMacroToggle.onValueChanged.AddListener(ChangeDoQuadMacro);

        NumStepsInput.onValueChanged.AddListener(ChangeNumSteps);
        NumStepsInput.onEndEdit.AddListener(ChangeNumSteps);
        StepSizeInput.onValueChanged.AddListener(ChangeStepSize);
        StepSizeInput.onEndEdit.AddListener(ChangeStepSize);
        RecomputeFCsInput.onValueChanged.AddListener(ChangeRecomputeFCs);
        RecomputeFCsInput.onEndEdit.AddListener(ChangeRecomputeFCs);

        OptimisationCanvas.enabled = true;
    }

    void ShowFrequency() {
        HideTabs();

        GaussianCalculator gaussianCalculator = GetGaussianCalculator();

        DoFrequencyToggle.isOn = gaussianCalculator.doFreq;
        UseHighPrecisionModesToggle.isOn = gaussianCalculator.useHighPrecisionModes;

        DoFrequencyToggle.onValueChanged.AddListener(ChangeDoFrequency);
        UseHighPrecisionModesToggle.onValueChanged.AddListener(ChangeUseHighPrecisionModes);

        FrequencyCanvas.enabled = true;
    }

    void ShowOptions() {
        HideTabs();

        GaussianCalculator gaussianCalculator = GetGaussianCalculator();

        FileNameInput.text = gaussianCalculator.title;
        OldChkFileNameInput.text = gaussianCalculator.oldCheckpointPath;
        MemoryInput.text = gaussianCalculator.jobMemoryMB.ToString();
        NProcInput.text = gaussianCalculator.numProcessors.ToString();

        FileNameInput.onValueChanged.AddListener(ChangeFileName);
        FileNameInput.onEndEdit.AddListener(ChangeFileName);
        OldChkFileNameInput.onValueChanged.AddListener(ChangeOldChkFileName);
        OldChkFileNameInput.onEndEdit.AddListener(ChangeOldChkFileName);
        MemoryInput.onValueChanged.AddListener(ChangeMemory);
        MemoryInput.onEndEdit.AddListener(ChangeMemory);
        NProcInput.onValueChanged.AddListener(ChangeNProc);
        NProcInput.onEndEdit.AddListener(ChangeNProc);

        OptionsCanvas.enabled = true;
    }

    public void Confirm() {
        userResponded = true;
        cancelled = false;
        Hide();
    }

    public void Cancel() {
        userResponded = true;
        cancelled = true;
        Hide();
    }

    public void Hide() {
        HideTabs();
        canvas.enabled = false;
    }

    public void Show() {
        canvas.enabled = true;
    }

    private void SetInputColours(TMP_InputField inputField, bool ok) {
        inputField.colors = ok ? 
            ColorScheme.main.inputNormalCB : 
            ColorScheme.main.inputErrorCB;
        inputField.textComponent.color = ok ? 
            ColorScheme.main.inputNormalColour : 
            ColorScheme.main.inputErrorColour;
    }

    //LAYER OPTIONS/VALIDATION
    private Layer GetCurrentLayer() {
        return geometry.gaussianCalculator.layerDict[currentLayerID];
    }

    private bool CheckLayerMethod(string newMethod) {
        bool ok = Data.gaussianMethods.Contains(newMethod.ToLower());
        SetInputColours(MethodsInput, ok);
        return ok;
    }

    private bool CheckLayerCharge(string chargeString, out int charge) {
        bool ok = int.TryParse(chargeString, out charge);

        //Also validate multiplicity if changing the charge
        int multiplicity;
        CheckLayerMultiplicity(MultiplicityInput.text, charge, layerElectronCount[currentLayerID], out multiplicity);

        SetInputColours(ChargeInput, ok);
        return ok;
        
    }

    private bool CheckLayerMultiplicity(string multiplicityString, int layerCharge, int layerElectrons, out int multiplicity) {
        //Check that we have a valid Integer
        bool ok = int.TryParse(multiplicityString, out multiplicity);

        if (ok) {
            //Validate multiplicity against number of electrons
            ok = (multiplicity > 0) && 
                 (Mathf.Abs(layerCharge) + layerElectrons + multiplicity) % 2 == 1;
        }
        SetInputColours(MultiplicityInput, ok);
        return ok;
    }

    private void ChangeLayerMethod(string newMethod) {
        CheckLayerMethod(newMethod);
        GetCurrentLayer().method = newMethod;
    }

    private void ChangeLayerBasis(string newBasis) {
        GetCurrentLayer().basis = newBasis;
    }

    private void ChangeLayerMethodOptions(string newOptions) {
        List<string> options = newOptions.Split(new[] {' '}, System.StringSplitOptions.RemoveEmptyEntries).ToList();
        GetCurrentLayer().options = options;
    }

    private void ChangeLayerCharge(string newChargeString) {
        int charge;
        if (CheckLayerCharge(newChargeString, out charge)) {
            GetCurrentLayer().charge = charge;
        }
    }

    private void ChangeLayerMultiplicity(string newMultplicityString) {
        
        int multiplicity;
        if (
            CheckLayerMultiplicity(
                newMultplicityString, 
                GetCurrentLayer().charge, 
                layerElectronCount[currentLayerID], 
                out multiplicity
            )
        ) {
            GetCurrentLayer().multiplicity = multiplicity;
        }
    }
    
    //OPTIMISATION OPTIONS/VALIDATION
    private GaussianCalculator GetGaussianCalculator() {
        return geometry.gaussianCalculator;
    }

    private void ChangeGeometryOptimisationTarget(int index) {
        string geometryOptimisationTargetString = GeometryTargetDropdown.options[index].text;
        GOT geometryOptimisationTarget = Constants.GaussianOptTargetMap[geometryOptimisationTargetString];
        GetGaussianCalculator().optTarget = geometryOptimisationTarget;
    }

    private void ChangeConvergenceThreshold(int index) {
        string convergenceThresholdString = ConvergenceCriteriaDropdown.options[index].text;
        GCT convergenceThreshold = Constants.GaussianConvergenceThresholdMap[convergenceThresholdString];
        GetGaussianCalculator().convergenceThreshold = convergenceThreshold;
    }

    private void ChangeForceConstantOption(int index) {
        string forceConstantOptionString = ForceConstantsDropdown.options[index].text;
        GFC forceConstantOption = Constants.GaussianForceConstantMap[forceConstantOptionString];

        RecomputeFCsInput.interactable = forceConstantOption == GFC.RECALC;
        GetGaussianCalculator().forceConstantOption = forceConstantOption;
    }

    private bool CheckRecomputeFCs(string newRecomputeFCsString, out int recomputeFCs) {
        bool ok = int.TryParse(newRecomputeFCsString, out recomputeFCs);

        // If recomputeFCs is 1, also make the ForceConstantOption CALC_ALL
        // If recomputeFCs is less than 1, also make the ForceConstantOption CALC_FIRST
        SetInputColours(RecomputeFCsInput, ok);
        if (ok) {
            if (recomputeFCs == 1) {
                ChangeForceConstantOption(
                    ForceConstantsDropdown.options
                        .FindIndex(x => x.text.Equals(Constants.GaussianForceConstantMap[GFC.CALC_ALL]))
                );
            } else if (recomputeFCs < 1) {
                ChangeForceConstantOption(
                    ForceConstantsDropdown.options
                        .FindIndex(x => x.text.Equals(Constants.GaussianForceConstantMap[GFC.CALC_FIRST]))
                );
            }
        }
        return ok;
    }

    private bool CheckNumSteps(string newNumStepStrings, out int numSteps) {
        bool ok = int.TryParse(newNumStepStrings, out numSteps);
        SetInputColours(NumStepsInput, ok);
        return ok;
    }

    private bool CheckStepSize(string newStepSizeStrings, out int stepSize) {
        bool ok = int.TryParse(newStepSizeStrings, out stepSize);
        SetInputColours(StepSizeInput, ok);
        return ok;
    }

    private void ChangeRecomputeFCs(string newFCs) {
        int recomputeFCs;
        CheckRecomputeFCs(newFCs, out recomputeFCs);
        GetGaussianCalculator().forceConstantRecalcEveryNSteps = recomputeFCs;
    }

    private void ChangeNumSteps(string newNumStepsString) {
        int numSteps;
        CheckNumSteps(newNumStepsString, out numSteps);
        GetGaussianCalculator().numOptSteps = numSteps;
    }

    private void ChangeStepSize(string newStepSize) {
        int stepSize;
        CheckStepSize(newStepSize, out stepSize);
        GetGaussianCalculator().optStepSize = stepSize;
    }

    private void ChangeDoOptimisation(bool newDoOpt) {
        GetGaussianCalculator().doOptimisation = newDoOpt;
    }

    private void ChangeDoMicroiterations(bool newDoMicro) {
        GetGaussianCalculator().doMicroiterations = newDoMicro;
    }

    private void ChangeDoQuadMacro(bool newDoQuadMaro) {
        GetGaussianCalculator().doQuadMacro = newDoQuadMaro;
    }

    //FREQUENCY OPTIONS/VALIDATION

    private void ChangeDoFrequency(bool newDoFreq) {
        GetGaussianCalculator().doFreq = newDoFreq;
    }

    private void ChangeUseHighPrecisionModes(bool newHPModes) {
        GetGaussianCalculator().useHighPrecisionModes = newHPModes;
    }

    //LINK0 OPTIONS/VALIDATION

    private void ChangeFileName(string newFileName) {
        GetGaussianCalculator().title = newFileName;
    }

    private void ChangeOldChkFileName(string newFileName) {
        GetGaussianCalculator().oldCheckpointPath = newFileName;
    }

    private bool CheckMemory(string newMemoryString, out int memory) {
        bool ok = int.TryParse(newMemoryString, out memory);
        if (ok) {
            ok = memory > 0;
        }
        SetInputColours(MemoryInput, ok);
        return ok;
    }

    private bool CheckNProc(string newNPRocString, out int proc) {
        bool ok = int.TryParse(newNPRocString, out proc);
        if (ok) {
            ok = proc > 0;
        }
        SetInputColours(NProcInput, ok);
        return ok;
    }

    private void ChangeMemory(string newMemoryString) {
        int memory;
        CheckMemory(newMemoryString, out memory);
        GetGaussianCalculator().jobMemoryMB = memory;
    }

    private void ChangeNProc(string newNPRocString) {
        int nProc;
        CheckNProc(newNPRocString, out nProc);
        GetGaussianCalculator().numProcessors = nProc;
    }

}
