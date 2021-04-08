using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using System.Linq;
using CS = Constants.ColourScheme;
using EL = Constants.ErrorLevel;
using COL = Constants.Colour;
using OLID = Constants.OniomLayerID;
using GIID = Constants.GeometryInterfaceID;
using GIS = Constants.GeometryInterfaceStatus;
using SIZE = Constants.Size;
using ChainID = Constants.ChainID;
using TMPro;


/// <summary>The Output Updater Singleton Class</summary>
/// 
/// <remarks>
/// Derives from PopupWindow
/// Lazy instantiation
/// Activated when a Geometry is updated from the Interface
/// </remarks>

public class OutputUpdater : PopupWindow {
    
	/// <summary>The singleton instance of the Output Updater.</summary>
    private static OutputUpdater _main;
	/// <summary>Getter for the singleton instance of the Gaussian Output Updater.</summary>
    /// <remarks>
    /// Instantiates a singleton instance and runs Create() if _main is null
    /// </remarks>
    public new static OutputUpdater main {
        get {
            if (_main == null) {
                GameObject gameObject = new GameObject("OutputUpdater");
                _main = (OutputUpdater)gameObject.AddComponent(typeof(OutputUpdater));
                _main.StartCoroutine(_main.Create());
            };
            return _main;
        }
    }

    GaussianOutputReader gaussianOutputReader;
    GeometryInterface targetGeometryInterface;
    Geometry targetGeometry;
    Geometry linkGeometry;
    Geometry outputGeometry;

    public IEnumerator Initialise(GIID geometryInterfaceID) {

        while (isBusy) {
            yield return null;
        }

        canUpdatePositions = false;
        canUpdateAmbers = false;
        canUpdateCharges = false;
        canUpdateParameters = false;

        targetGeometryInterface = Flow.GetGeometryInterface(geometryInterfaceID);
        targetGeometry = targetGeometryInterface.geometry;
        outputGeometry = PrefabManager.InstantiateGeometry(transform);
        linkGeometry = PrefabManager.InstantiateGeometry(transform);

        yield return CheckFilesMap();
        table.Refresh();
        Show();
    }

    RectTransform tableRect;
    Table table;

    Image targetImage;
    Image linkImage;
    Image outputImage;

    TextMeshProUGUI infoText;


    bool _canUpdatePositions;
    bool _canUpdateAmbers;
    bool _canUpdateCharges;
    bool _canUpdateParameters;
    bool canUpdatePositions {
        get => _canUpdatePositions;
        set => _canUpdatePositions = value;
    }
    bool canUpdateAmbers {
        get => _canUpdateAmbers;
        set => _canUpdateAmbers = value;
    }
    bool canUpdateCharges {
        get => _canUpdateCharges;
        set => _canUpdateCharges = value;
    }
    bool canUpdateParameters {
        get => _canUpdateParameters;
        set => _canUpdateParameters = value;
    }

    
    public override IEnumerator Create() {

        activeTasks++;
        
        AddBackgroundCanvas();
        AddBlurBackground();

        GameObject edge = AddBackground();
        SetRect(edge, 0, 0, 1, 1, 250, 150, -150, -150);
        
        AddTopBar("Update Geometry");
        RectTransform bottomBarRect = AddBottomBar(confirm:true);

        GameObject infoBoxGO = AddText(
            bottomBarRect,
            "Info",
            "",
            textAlignmentOptions:TextAlignmentOptions.MidlineLeft
        );
        SetRect(infoBoxGO, 0, 0.5f, 1, 0.5f, 2, -20, -120, 20);
        infoText = infoBoxGO.GetComponent<TextMeshProUGUI>();

        AddContentRect();

        if (Timer.yieldNow) {yield return null;}

        GameObject tableGO = AddTable(contentRect, "Table", 8, 4, COL.DARK_75);
        tableRect = tableGO.GetComponent<RectTransform>();
        SetRect(
            tableRect,
            0, 0,
            1, 1,
            4, 0,
            -4, 0
        );
        table = tableGO.GetComponent<Table>();

        SetColumnTitleRect();
        SetRowTitleRect();
        SetTargetRect();
        SetLinkRect();
        SetOutputRect();
        
        activeTasks--;
        
    }

    void SetColumnTitleRect() {

        GameObject setFileTitle    = table.CreateTextCell(0, 1, "Current Geometry", textColour:COL.LIGHT_75, fontStyles:FontStyles.Bold);
        GameObject NumAtomsTitle   = table.CreateTextCell(0, 2, "Linking File", textColour:COL.LIGHT_75, fontStyles:FontStyles.Bold);
        GameObject PositionsTitle  = table.CreateTextCell(0, 3, "Output File", textColour:COL.LIGHT_75, fontStyles:FontStyles.Bold);

    }

    void SetRowTitleRect() {

        GameObject NumAtomsTitle   = table.CreateTextCell(1, 0, "Number of Atoms:", textColour:COL.LIGHT_75, fontStyles:FontStyles.Bold);
        GameObject PositionsTitle  = table.CreateTextCell(2, 0, "Positions:", textColour:COL.LIGHT_75, fontStyles:FontStyles.Bold);
        GameObject AmbersTitle     = table.CreateTextCell(3, 0, "Ambers:", textColour:COL.LIGHT_75, fontStyles:FontStyles.Bold);
        GameObject ChargesTitle    = table.CreateTextCell(4, 0, "Charges:", textColour:COL.LIGHT_75, fontStyles:FontStyles.Bold);
        GameObject ParametersTitle = table.CreateTextCell(5, 0, "Parameters:", textColour:COL.LIGHT_75, fontStyles:FontStyles.Bold);
        GameObject AtomMapTitle    = table.CreateTextCell(6, 0, "Atom Map:", textColour:COL.LIGHT_75, fontStyles:FontStyles.Bold);
        GameObject setFileTitle    = table.CreateTextCell(7, 0, "Set File:", textColour:COL.LIGHT_75, fontStyles:FontStyles.Bold);

    }

    void SetTargetRect() {

        GameObject targetBorder = table.CreateBorder(0, 1, 8, 1);
        targetImage = targetBorder.GetComponent<Image>();

        GameObject NumAtomsTarget   = table.CreateTextCell(1, 1, "0", () => GetNumAtomsString(targetGeometry));
        GameObject PositionsTarget  = table.CreateTextCell(2, 1, "");
        GameObject AmbersTarget     = table.CreateTextCell(3, 1, "0", () => GetAmberCountString(targetGeometry));
        GameObject ChargesTarget    = table.CreateTextCell(4, 1, "0", () => GetChargesCountString(targetGeometry));
        GameObject ParametersTarget = table.CreateTextCell(5, 1, "No", () => GetHasParametersString(targetGeometry));
        GameObject AtomMapTarget    = table.CreateTextCell(6, 1, "No", () => GetHasAtomMapString(targetGeometry));
        GameObject setFileTarget    = table.CreateTextCell(7, 1, "");

    }

    void SetLinkRect() {

        GameObject linkBorder = table.CreateBorder(0, 2, 8, 1);
        linkImage = linkBorder.GetComponent<Image>();

        GameObject NumAtomsLink   = table.CreateTextCell  (1, 2, "0", () => GetNumAtomsString(linkGeometry));
        GameObject PositionsLink  = table.CreateTextCell  (2, 2, "");
        GameObject AmbersLink     = table.CreateTextCell  (3, 2, "0", () => GetAmberCountString(linkGeometry));
        GameObject ChargesLink    = table.CreateTextCell  (4, 2, "0", () => GetChargesCountString(linkGeometry));
        GameObject ParametersLink = table.CreateTextCell  (5, 2, "No", () => GetHasParametersString(linkGeometry));
        GameObject AtomMapLink    = table.CreateTextCell  (6, 2, "No", () => GetHasAtomMapString(linkGeometry));
        GameObject setFileLink    = table.CreateButtonCell(7, 2, "Load", () => StartCoroutine(LoadLinkFile()));

    }

    void SetOutputRect() {

        GameObject outputBorder = table.CreateBorder(0, 3, 8, 1);
        outputImage = outputBorder.GetComponent<Image>();

        //This rect contains information on the File linking the Target Geometry to the Output Geometry

        GameObject NumAtomsOutput   = table.CreateTextCell  (1, 3, "0", () => GetNumAtomsString(outputGeometry));
        GameObject PositionsOutput  = table.CreateButtonCell(2, 3, "Update", () => StartCoroutine(UpdatePositions()), () => canUpdatePositions);
        GameObject AmbersOutput     = table.CreateButtonCell(3, 3, "Update", () => StartCoroutine(UpdateAmbers()), () => canUpdateAmbers);
        GameObject ChargesOutput    = table.CreateButtonCell(4, 3, "Update", () => StartCoroutine(UpdateCharges()), () => canUpdateCharges);
        GameObject ParametersOutput = table.CreateButtonCell(5, 3, "Update", () => UpdateParameters(), () => canUpdateParameters);
        GameObject AtomMapOutput    = table.CreateTextCell  (6, 3, "No", () => GetHasAtomMapString(outputGeometry));
        GameObject setFileOutput    = table.CreateButtonCell(7, 3, "Load", () => StartCoroutine(LoadOutputFile()));

    }
    



    string GetNumAtomsString(Geometry geometry) {
        if (geometry == null) {
            return "";
        }
        return $"{geometry.size}";
    }

    string GetAmberCountString(Geometry geometry) {
        if (geometry == null) {
            return "";
        }
        return $"{geometry.AmberCount()}";
    }

    string GetHasParametersString(Geometry geometry) {
        if (geometry == null) {
            return "";
        }
        return geometry.parameters.IsEmpty() ? "No" : "Yes";
    }

    string GetChargesCountString(Geometry geometry) {
        if (geometry == null) {
            return "";
        }
        return $"{geometry.ChargeCount()}";
    }

    string GetHasAtomMapString(Geometry geometry) {
        if (geometry == null) {
            return "";
        }

        return HasAtomMap(geometry) ? "Yes" : "No";
    }

    bool HasAtomMap(Geometry geometry) {
        return geometry != null && geometry.atomMap != null && geometry.atomMap.Count() > 0;
    }

    IEnumerator UpdatePositions() {
        if (!isBusy && targetGeometry != null && outputGeometry != null) {
            activeTasks++;

            if (gaussianOutputReader == null) {
                yield return outputGeometry.AlignTo(targetGeometry);
                yield return targetGeometry.UpdateFrom(outputGeometry, updatePositions:true);
            } else {
                RectTransform stepRect = AddRect(contentRect, "Step", COL.DARK_75);
                table.SetCell(stepRect, 0, 2, 8, 2);
                
                GameObject dropdownGO = AddDropdown(stepRect, "Step Selector");
                SetRect(dropdownGO, 0, 0.5f, 1, 0.5f, 10, -20, -10, 20);
                TMP_Dropdown dropdown = dropdownGO.GetComponent<TMP_Dropdown>();

                List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

                int numSteps = gaussianOutputReader.standardPositions.Count;
                int numEnergies = gaussianOutputReader.energies.Count;
                for (int step=0; step<numSteps; step++) {
                    string stepString = $"{step}: ";
                    if (step < numEnergies) {
                        stepString += $"{gaussianOutputReader.energies[step]:0.########}";
                    }

                    options.Add(new TMP_Dropdown.OptionData(stepString));
                }

                dropdown.AddOptions(options);
                dropdown.SetValueWithoutNotify(numSteps - 1);

                bool userResponded = false;
                bool cancelled = false;

                GameObject cancelButtonGO = AddButton(stepRect, "Cancel", "Cancel", COL.CLEAR, CS.BRIGHT, () => {userResponded=true; cancelled=true;});
                SetRect(cancelButtonGO, 1, 0, 1, 0, -244, 2, -124, 62);

                GameObject confirmButtonGO = AddButton(stepRect, "Confirm", "Confirm", COL.CLEAR, CS.BRIGHT, () => {userResponded=true; cancelled=false;});
                SetRect(confirmButtonGO, 1, 0, 1, 0, -122, 2, 2, 62);

                while (!userResponded) {
                    yield return null;
                }

                int selectedStep = dropdown.value;

                GameObject.Destroy(stepRect.gameObject);

                if (!cancelled) {
                    float3[] positions = gaussianOutputReader.standardPositions[selectedStep];
                    int numAtoms = positions.Length;
                    foreach ((AtomID atomID, int atomIndex) in outputGeometry.atomMap) {
                        Atom atom;
                        if (outputGeometry.TryGetAtom(atomID, out atom)) {
                            if (atomIndex >= numAtoms) {
                                CustomLogger.LogFormat(EL.ERROR, $"Atom index '{atomIndex}' exceeded size of positions array '{numAtoms}'! Cannot update positions");
                                yield break;
                            }
                            atom.position = positions[atomIndex];
                        }
                    }
                    yield return outputGeometry.AlignTo(targetGeometry);
                    yield return targetGeometry.UpdateFrom(outputGeometry, updatePositions:true);

                    targetGeometry.gaussianResults = outputGeometry.gaussianResults;
                }
            }

            infoText.text = "Positions updated";
            activeTasks--;
        }
    }

    IEnumerator UpdateAmbers() {
        if (!isBusy && targetGeometry != null && outputGeometry != null) {
            activeTasks++;
            yield return targetGeometry.UpdateFrom(outputGeometry, updateAmbers:true);
            infoText.text = "Ambers updated";
            activeTasks--;
        }
    }

    IEnumerator UpdateCharges() {
        if (!isBusy && targetGeometry != null && outputGeometry != null) {
            activeTasks++;
            yield return targetGeometry.UpdateFrom(outputGeometry, updateCharges:true);
            infoText.text = "Charges updated";
            activeTasks--;
        }
    }

    void UpdateParameters() {
        if (!isBusy && targetGeometry != null && outputGeometry != null) {
            Parameters.UpdateParameters(outputGeometry, targetGeometry);
            infoText.text = "Parameters updated";
        }
    }

    IEnumerator LoadOutputFile() {
        SetBorder(outputImage, GIS.LOADING);
        infoText.text = "Loading Output File...";
        CustomLogger.LogFormat(EL.VERBOSE, "Loading Output File");

        // Destroy old geometry
        if (outputGeometry != null && outputGeometry.gameObject != null) {
            GameObject.Destroy(outputGeometry.gameObject);
        }
        //Create new geometry
        outputGeometry = PrefabManager.InstantiateGeometry(transform);

        //Use Link map ideally, otherwise target
        if (HasAtomMap(linkGeometry)) {
            CustomLogger.LogFormat(EL.VERBOSE, "Using Link Geometry Atom Map");
            outputGeometry.atomMap = linkGeometry.atomMap.ToMap(x => x.Key, x => x.Value);
        } else if (HasAtomMap(targetGeometry)) {
            CustomLogger.LogFormat(EL.VERBOSE, "Using Target Geometry Atom Map");
            outputGeometry.atomMap = targetGeometry.atomMap.ToMap(x => x.Key, x => x.Value);
        }

        yield return LoadFile(outputGeometry, Flow.loadTypes);
        yield return CheckFilesMap();
        table.Refresh();
    }

    IEnumerator LoadLinkFile() {
        SetBorder(linkImage, GIS.LOADING);
        infoText.text = "Loading Link File...";
        yield return LoadFile(linkGeometry, new List<string> {"com", "gjf"});
        
        //Ensure Chain IDs are matched
        List<ChainID> chainIDs = targetGeometry.GetChainIDs().ToList();

        if (chainIDs.Count == 1) {
            //One Chain ID - force all Atom IDs of link geometry to have same Chain ID
            ChainID chainID = chainIDs.First();
            CustomLogger.LogFormat(EL.VERBOSE, "Forcing Link Geometry into Chain ID '{0}'", chainID);
            linkGeometry.atomMap = linkGeometry.atomMap.ToMap(
                x => new AtomID(new ResidueID(chainID, x.Key.residueID.residueNumber), x.Key.pdbID),
                x => x.Value
            );
        } else {
            //Multiple Chain IDs - Get as many matches as possible
            Map<AtomID, int> newAtomMap = new Map<AtomID, int>();
            CustomLogger.LogFormat(EL.WARNING, "Multiple Chain IDs in target - using best Atom ID matches");
            foreach ((AtomID atomID, int atomIndex) in linkGeometry.atomMap) {
                foreach (ChainID chainID in chainIDs) {
                    AtomID newAtomID = new AtomID(new ResidueID(chainID, atomID.residueID.residueNumber), atomID.pdbID);
                    if (targetGeometry.HasAtom(newAtomID)) {
                        newAtomMap[newAtomID] = atomIndex;
                    }
                }
                if (Timer.yieldNow) {
                    yield return null;
                }
            }
            linkGeometry.atomMap = newAtomMap;
        }
        yield return CheckFilesMap();
        table.Refresh();
    }

    IEnumerator CheckFilesMap() {

        /*
        Situation 1:
        Link file: None
        Output file: Has PDB info (.pdb, .pqr, .xat etc)
        Behaviour: Update directly from output

        Situation 2:
        Link file: None
        Output file: does not have PDB info (.log)
        Behaviour: Update from target's Atom Map, fail if it doesn't align

        Situation 3:
        Link file: Present
        Output file: Has PDB info (.pdb, .pqr, .xat etc)
        Behaviour: Ignore link file, refer to situation 1

        Situation 4:
        Link file: Present
        Output file: does not have PDB info (.log)
        Behaviour: Update from link file's Atom Map
        */

        canUpdatePositions = false;
        canUpdateAmbers = false;
        canUpdateCharges = false;
        canUpdateParameters = false;

        SetBorder(linkImage, GIS.DISABLED);
        SetBorder(outputImage, GIS.DISABLED);

        // Check Target Geometry
        if (targetGeometry == null) {
            infoText.text = "No Target Geometry!";
            SetBorder(targetImage, GIS.ERROR);
            yield break;
        }

        int targetSize = targetGeometry.size;

        if (targetSize == 0) {
            infoText.text = "Target Geometry is empty";
            SetBorder(targetImage, GIS.ERROR);
            yield break;
        } else {
            SetBorder(targetImage, GIS.OK);
        }

        if (outputGeometry == null) {
            infoText.text = "No Output Geometry";
            SetBorder(outputImage, GIS.DISABLED);
            yield break;
        }

        int outputSize = outputGeometry.size;

        // Check Output Geometry
        if (outputSize == 0) {
            infoText.text = "Output Geometry is empty";
            SetBorder(outputImage, GIS.ERROR);
            yield break;
        }

        if (linkGeometry == null || linkGeometry.size == 0) {

            // No mapping file - Situation 1 or 2

            if (gaussianOutputReader != null) {

                // Output is Gaussian log - Situation 2

                if (!HasAtomMap(targetGeometry)) {
                    // No Atom Map in target geometry - fail
                    infoText.text = "Gaussian Output has no Atom Map! Use Linking File.";
                    SetBorder(outputImage, GIS.ERROR);
                    yield break;
                }

                if (targetSize != outputGeometry.size) {
                    // Inconsistent sizes - fail
                    infoText.text = "Gaussian Output has different size to Target!";
                    SetBorder(outputImage, GIS.ERROR);
                    yield break;
                }

            } else {

                // Output has PDB info - Situation 1

                canUpdateAmbers = outputGeometry.AmberCount() > 0;
                canUpdateParameters = !outputGeometry.parameters.IsEmpty();
            }

        } else {
            
            // Mapping file - Situation 3 or 4

            if (gaussianOutputReader != null) {

                if (!HasAtomMap(linkGeometry)) {
                    // No Atom Map in linking geometry - fail
                    infoText.text = "Linking File has no Atom Map!";
                    SetBorder(linkImage, GIS.ERROR);
                    SetBorder(outputImage, GIS.ERROR);
                    yield break;
                }

                if (linkGeometry.atomMap.Count() != outputSize) {
                    infoText.text = "Linking File Atom Map has different size to Output!";
                    SetBorder(linkImage, GIS.ERROR);
                    SetBorder(outputImage, GIS.ERROR);
                    yield break;
                }

                SetBorder(linkImage, GIS.OK);


                // Output is Gaussian log - Situation 4

            } else {

                // Output has PDB info - Situation 3
                SetBorder(linkImage, GIS.OK);

                canUpdateAmbers = outputGeometry.AmberCount() > 0;
                canUpdateParameters = !outputGeometry.parameters.IsEmpty();

            }

        }
            
        canUpdatePositions = true;
        canUpdateCharges = outputGeometry.ChargeCount() > 0;
        if (targetSize != outputSize) {
            SetBorder(outputImage, GIS.WARNING);
        } else {
            SetBorder(outputImage, GIS.OK);
        }

    }

    void SetBorder(Image border, GIS status) {

        border.color = ColorScheme.GetColorBlock(status).normalColor;
        if (status == GIS.LOADING) {
            border.material = ColorScheme.GetLineGlowOSCMaterial();
        } else {
            border.material = ColorScheme.GetLineGlowMaterial();
        }
    }

    IEnumerator LoadFile(Geometry geometry, List<string> loadTypes) {
        if (!isBusy) {
            activeTasks++;

            FileSelector loadPrompt = FileSelector.main;

            //Set FileSelector to Load mode
            yield return loadPrompt.Initialise(saveMode:false, loadTypes);
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
            if (!System.IO.File.Exists(path)) {
                CustomLogger.LogFormat(EL.ERROR, "File does not exist: {0}", path);
                GameObject.Destroy(loadPrompt.gameObject);
                activeTasks--;
                yield break;
            }

            if (path.ToLower().EndsWith(".log")) {

                CustomLogger.LogFormat(EL.VERBOSE, "Loading Gaussian Output");

                ChainID chainID = ChainID._;
                if (geometry.atomMap != null && geometry.atomMap.Count != 0) {
                    chainID = geometry.atomMap.First().Key.residueID.chainID;
                }

                gaussianOutputReader = new GaussianOutputReader(geometry, chainID);

                yield return gaussianOutputReader.GeometryFromFile(path);
            } else {

                gaussianOutputReader = null;

		        yield return FileReader.LoadGeometry(geometry, path, "FileUpdater");
            }

            activeTasks--;
        }
    }

}