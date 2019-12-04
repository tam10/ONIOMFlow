using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GIS = Constants.GeometryInterfaceStatus;
using COL = Constants.Colour;
using CS = Constants.ColourScheme;

public class ColorScheme : MonoBehaviour {

    private static  ColorScheme _main;
    public static ColorScheme main {
        get {
            if (_main == null) {
                _main = GameObject.FindObjectOfType<ColorScheme>();
                _main.SetDicts();
            }
            return _main;
        }
    }

    [Header("Colours")]
    public Color DARK_100;
    public Color DARK_75;
    public Color DARK_50;
    public Color DARK_25;
	public Color MEDIUM_100;
    public Color MEDIUM_75;
    public Color MEDIUM_50;
    public Color MEDIUM_25;
	public Color LIGHT_100;
    public Color LIGHT_75;
    public Color LIGHT_50;
    public Color LIGHT_25;
	public Color BRIGHT_100;
    public Color BRIGHT_75;
    public Color BRIGHT_50;
    public Color BRIGHT_25;
	public Color CLOSE;
    public Color ERROR;
    public Color WARNING;
    public Color INFO;
    public Color CLEAR;
    public Color DISABLED;

    [Header("Blur Material")]
    public Material blurMaterial;

    [Header("Line Glow")]
    public Material lineGlowMaterial;
    public Material lineGlowMaskMaterial;
    public Material lineGlowOscMaterial;
    public Material trailMaterial;


    [Header("Disabled")]
    public ColorBlock disabledCB;
    public Color disabledForegroundColor;
    public Color disabledBackgroundColor;

    [Space]
    [Header("Completed")]
    public ColorBlock completedCB;
    public Color completedForegroundColor;
    public Color completedBackgroundColor;
    
    [Space]
    [Header("OK")]
    public ColorBlock okCB;
    public Color okForegroundColor;
    public Color okBackgroundColor;

    [Space]
    [Header("Warning")]
    public ColorBlock warningCB;
    public Color warningForegroundColor;
    public Color warningBackgroundColor;
    
    [Space]
    [Header("Error")]
    public ColorBlock errorCB;
    public Color errorForegroundColor;
    public Color errorBackgroundColor;
    
    [Space]
    [Header("Loading")]
    public ColorBlock loadingCB;
    public Color loadingForegroundColor;
    public Color loadingBackgroundColor;

    [Space]
    [Header("Input - Normal")]
    public ColorBlock inputNormalCB;
    public Color inputNormalColour;
    [Header("Input - Error")]
    public ColorBlock inputErrorCB;
    public Color inputErrorColour;

    [Space]
    [Header("File Selection - Enabled File")]
    public ColorBlock enabledFileCB;
    [Header("File Selection - Disabled File")]
    public ColorBlock disabledFileCB;
    [Header("File Selection - Directory")]
    public ColorBlock directoryCB;


    private Dictionary<GIS, ColorBlock> colorBlockDict = new Dictionary<GIS, ColorBlock> ();
    private Dictionary<GIS, Color> statusColorDict = new Dictionary<GIS, Color>();
    private Dictionary<COL, Color> colorDict = new Dictionary<COL, Color>();

    private Dictionary<CS, COL[]> colorSchemeDict = new Dictionary<CS, COL[]>();
    private Dictionary<CS, ColorBlock> schemeColorBlockDict = new Dictionary<CS, ColorBlock>();

    void SetDicts() {
        colorBlockDict[GIS.DISABLED] = disabledCB;
        colorBlockDict[GIS.COMPLETED] = completedCB;
        colorBlockDict[GIS.OK] = okCB;
        colorBlockDict[GIS.WARNING] = warningCB;
        colorBlockDict[GIS.ERROR] = errorCB;
        colorBlockDict[GIS.LOADING] = loadingCB;

        statusColorDict[GIS.DISABLED] = disabledBackgroundColor;
        statusColorDict[GIS.COMPLETED] = completedBackgroundColor;
        statusColorDict[GIS.OK] = okBackgroundColor;
        statusColorDict[GIS.WARNING] = warningBackgroundColor;
        statusColorDict[GIS.ERROR] = errorBackgroundColor;
        statusColorDict[GIS.LOADING] = loadingBackgroundColor;

        colorDict[COL.DARK_100] = DARK_100;
        colorDict[COL.DARK_75] = DARK_75;
        colorDict[COL.DARK_50] = DARK_50;
        colorDict[COL.DARK_25] = DARK_25;
        colorDict[COL.MEDIUM_100] = MEDIUM_100;
        colorDict[COL.MEDIUM_75] = MEDIUM_75;
        colorDict[COL.MEDIUM_50] = MEDIUM_50;
        colorDict[COL.MEDIUM_25] = MEDIUM_25;
        colorDict[COL.LIGHT_100] = LIGHT_100;
        colorDict[COL.LIGHT_75] = LIGHT_75;
        colorDict[COL.LIGHT_50] = LIGHT_50;
        colorDict[COL.LIGHT_25] = LIGHT_25;
        colorDict[COL.BRIGHT_100] = BRIGHT_100;
        colorDict[COL.BRIGHT_75] = BRIGHT_75;
        colorDict[COL.BRIGHT_50] = BRIGHT_50;
        colorDict[COL.BRIGHT_25] = BRIGHT_25;
        colorDict[COL.CLOSE] = CLOSE;
        colorDict[COL.ERROR] = ERROR;
        colorDict[COL.WARNING] = WARNING;
        colorDict[COL.INFO] = INFO;
        colorDict[COL.CLEAR] = CLEAR;
        colorDict[COL.DISABLED] = DISABLED;

        colorSchemeDict[CS.DARK] = new COL[9] {
            COL.DARK_100, 
            COL.DARK_75, 
            COL.DARK_50, 
            COL.DARK_25, 
            COL.CLEAR,
            COL.INFO,
            COL.WARNING,
            COL.ERROR,
            COL.DISABLED
        };
        schemeColorBlockDict[CS.DARK] = CreateColorBlock(CS.DARK);

        colorSchemeDict[CS.MEDIUM] = new COL[9] {
            COL.MEDIUM_100, 
            COL.MEDIUM_75, 
            COL.MEDIUM_50, 
            COL.MEDIUM_25, 
            COL.CLEAR,
            COL.INFO,
            COL.WARNING,
            COL.ERROR,
            COL.DISABLED
        };
        schemeColorBlockDict[CS.MEDIUM] = CreateColorBlock(CS.MEDIUM);

        colorSchemeDict[CS.LIGHT] = new COL[9] {
            COL.LIGHT_100, 
            COL.LIGHT_75, 
            COL.LIGHT_50, 
            COL.LIGHT_25, 
            COL.CLEAR,
            COL.INFO,
            COL.WARNING,
            COL.ERROR,
            COL.DISABLED
        };
        schemeColorBlockDict[CS.LIGHT] = CreateColorBlock(CS.LIGHT);

        colorSchemeDict[CS.BRIGHT] = new COL[9] {
            COL.BRIGHT_100, 
            COL.BRIGHT_75, 
            COL.BRIGHT_50, 
            COL.BRIGHT_25, 
            COL.CLEAR,
            COL.INFO,
            COL.WARNING,
            COL.ERROR,
            COL.DISABLED
        };
        schemeColorBlockDict[CS.BRIGHT] = CreateColorBlock(CS.BRIGHT);

        colorSchemeDict[CS.PROMPT_BUTTON] = new COL[9] {
            COL.LIGHT_75, 
            COL.CLEAR, 
            COL.MEDIUM_75, 
            COL.MEDIUM_50, 
            COL.CLEAR,
            COL.INFO,
            COL.WARNING,
            COL.ERROR,
            COL.DISABLED
        };
        schemeColorBlockDict[CS.PROMPT_BUTTON] = CreateColorBlock(CS.PROMPT_BUTTON);

        colorSchemeDict[CS.CLOSE_BUTTON] = new COL[9] {
            COL.CLEAR, 
            COL.CLEAR, 
            COL.CLOSE, 
            COL.CLOSE, 
            COL.CLEAR,
            COL.INFO,
            COL.WARNING,
            COL.ERROR,
            COL.DISABLED
        };
        schemeColorBlockDict[CS.CLOSE_BUTTON] = CreateColorBlock(CS.CLOSE_BUTTON);

    }

    public static ColorBlock GetColorBlock(GIS status) {
        return main.colorBlockDict[status];
    }

    public static Color GetStatusColor(GIS status) {
        return main.statusColorDict[status];
    }

    public static Material GetLineGlowMaterial() {
        return main.lineGlowMaterial;
    }

    public static void SetLineGlowAmount(float amount=0.25f) {
        main.lineGlowMaterial.SetFloat("_GlowAmount", amount);
    }

    public static Material GetMaskedLineGlowMaterial() {
        return main.lineGlowMaskMaterial;
    }

    public static Material GetLineGlowOSCMaterial() {
        return main.lineGlowOscMaterial;
    }

    public static Material GetTrailMaterial() {
        return main.trailMaterial;
    }

    public static Color GetColor(COL colorName) {
        return main.colorDict[colorName];
    }

    public static COL[] GetColorScheme(CS colourScheme) {
        return main.colorSchemeDict[colourScheme];
    }

    public static ColorBlock GetColorSchemeBlock(CS colourScheme) {
        return main.schemeColorBlockDict[colourScheme];
    }

    private ColorBlock CreateColorBlock(CS colourScheme) {
        ColorBlock colorBlock = new ColorBlock();
        COL[] colours = GetColorScheme(colourScheme);

        colorBlock.normalColor = GetColor(colours[2]);
        colorBlock.highlightedColor = GetColor(colours[1]);
        colorBlock.pressedColor = GetColor(colours[0]);
        colorBlock.disabledColor = GetColor(colours[8]);
        colorBlock.colorMultiplier = 1f;
        colorBlock.fadeDuration = 0.5f;
        return colorBlock;
    }

    public void SetDefaults() {
        SetLineGlowAmount();
    }

    void OnDestroy() {
        SetDefaults();
    }

}
