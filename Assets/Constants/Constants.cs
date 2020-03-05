using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class Constants {
    // These Constants are aimed to be shared across multiple classes and namespaces.
    // Using enum to prevent any ambiguity - even though they are essentially int types, they cannot be confused and methods should only accept the enum version.
    // This static class also contains forward and backward name translation for settings parsers

	public enum ErrorLevel : int { NULL, FATAL, ERROR, WARNING, NOTIFICATION, INFO, VERBOSE, DEBUG };
	public static Map<string, ErrorLevel> ErrorLevelMap = new Map<string, ErrorLevel> {
		{"None", ErrorLevel.NULL},
		{"Fatal", ErrorLevel.FATAL},
		{"Error", ErrorLevel.ERROR},
		{"Warning", ErrorLevel.WARNING},
		{"Notification", ErrorLevel.NOTIFICATION},
		{"Information", ErrorLevel.INFO},
		{"Verbose", ErrorLevel.VERBOSE},
		{"Debug", ErrorLevel.DEBUG}
	};

	public enum Size : int { VSMALL, SMALL, MSMALL, MEDIUM, MLARGE, LARGE, VLARGE }
	public enum Colour : int { 
		DARK_100, DARK_75, DARK_50, DARK_25,
		MEDIUM_100, MEDIUM_75, MEDIUM_50, MEDIUM_25,
		LIGHT_100, LIGHT_75, LIGHT_50, LIGHT_25,
		BRIGHT_100, BRIGHT_75, BRIGHT_50, BRIGHT_25,
		CLOSE, ERROR, WARNING, INFO, CLEAR, DISABLED
	}
	public enum ColourScheme : int { DARK, MEDIUM, LIGHT, BRIGHT, PROMPT_BUTTON, CLOSE_BUTTON }

    //GEOMETRY INTERFACES
    public enum GeometryInterfaceID : int { NONE, ORIGINAL, CLEANED, SR, NSR, PCSR, PNSR, 
	PCNSR, COMBINED, MODEL, INTERMEDIATE, ONELAYER, TWOLAYER, THREELAYER}
    public static Map<string, GeometryInterfaceID> GeometryInterfaceIDMap = new Map<string, GeometryInterfaceID> {
        {"none", GeometryInterfaceID.NONE},
		{"original", GeometryInterfaceID.ORIGINAL},
        {"cleaned", GeometryInterfaceID.CLEANED},
        {"sr", GeometryInterfaceID.SR},
        {"nsr", GeometryInterfaceID.NSR},
        {"pcsr", GeometryInterfaceID.PCSR},
        {"pnsr", GeometryInterfaceID.PNSR},
        {"pcnsr", GeometryInterfaceID.PCNSR},
        {"combined", GeometryInterfaceID.COMBINED},
        {"model", GeometryInterfaceID.MODEL},
        {"intermediate", GeometryInterfaceID.INTERMEDIATE},
        {"oneLayer", GeometryInterfaceID.ONELAYER},
        {"twoLayer", GeometryInterfaceID.TWOLAYER},
        {"threeLayer", GeometryInterfaceID.THREELAYER}
	};

    public enum GeometryInterfaceStatus : int { DISABLED, OK, COMPLETED, LOADING, WARNING, ERROR }
	public static Map<string, GeometryInterfaceStatus> GeometryInterfaceStatusMap = new Map<string, GeometryInterfaceStatus> {
		{"disabled", GeometryInterfaceStatus.DISABLED},
		{"completed", GeometryInterfaceStatus.COMPLETED},
		{"ok", GeometryInterfaceStatus.OK},
		{"loading", GeometryInterfaceStatus.LOADING},
		{"warning", GeometryInterfaceStatus.WARNING},
		{"error", GeometryInterfaceStatus.ERROR}
	};


    //ARROW PROCEDURES
    public enum ArrowID : int { NONE, ORIGINAL_TO_CLEANED, CLEANED_TO_SR, CLEANED_TO_NSR, 
	SR_TO_PCSR, NSR_TO_PNSR, PNSR_TO_PCNSR, PCNSR_TO_COMBINED, PCSR_TO_COMBINED, 
	COMBINED_TO_MODEL, COMBINED_TO_ONELAYER, MODEL_TO_INTERMEDIATE, MODEL_TO_TWOLAYER, 
	INTERMEDIATE_TO_THREELAYER }
	public static Map<string, ArrowID> ArrowIDMap = new Map<string, ArrowID> {
		{"originalToCleaned", ArrowID.ORIGINAL_TO_CLEANED},
		{"cleanedToSR", ArrowID.CLEANED_TO_SR},
		{"cleanedToNSR", ArrowID.CLEANED_TO_NSR},
		{"srToPCSR", ArrowID.SR_TO_PCSR},
		{"nsrToPNSR", ArrowID.NSR_TO_PNSR},
		{"pnsrToPCNSR", ArrowID.PNSR_TO_PCNSR},
		{"pcnsrToCombined", ArrowID.PCNSR_TO_COMBINED},
		{"pcsrToCombined", ArrowID.PCSR_TO_COMBINED},
		{"combinedToModel", ArrowID.COMBINED_TO_MODEL},
		{"combinedToOneLayer", ArrowID.COMBINED_TO_ONELAYER},
		{"modelToIntermediate", ArrowID.MODEL_TO_INTERMEDIATE},
		{"modelToTwoLayer", ArrowID.MODEL_TO_TWOLAYER},
		{"intermediateToThreeLayer", ArrowID.INTERMEDIATE_TO_THREELAYER}
	};
    
    //TASKS
    public enum TaskID : int { NONE, STANDARDISE_WATERS, REMOVE_PDB_SPECIAL_CHARACTERS, 
	MERGE_NSRS_BY_CONNECTIVITY, GET_CHAIN, PROTONATE_PDB2PQR, PROTONATE_REDUCE, GET_SRS, 
	GET_NSRS, CHECK_GEOMETRY, CALCULATE_AMBER_TYPES, CALCULATE_AMBER_TYPES_ANTECHAMBER, 
	CALCULATE_CONNECTIVITY, CLEAR_CONNECTIVITY, SELECT_NONSTANDARD_RESIDUES, 
	CALCULATE_PARTIAL_CHARGES_RED, CALCULATE_PARTIAL_CHARGES_GAUSSIAN, GET_PARTIAL_CHARGES_FROM_MOL2, 
	FILL_MISSING_RESIDUES, OPTIMISE_MISSING_RESIDUES, MUTATE_RESIDUE,
	MERGE_GEOMETRIES, CALCULATE_PARAMETERS, OPTIMISE_AMBER, VALIDATE_LAYERS, SETUP_CALCULATION, 
	MOVE_ALL_TO_MODEL_LAYER, MOVE_ALL_TO_INTERMEDIATE_LAYER, MOVE_ALL_TO_REAL_LAYER,
	MOVE_SELECTION_TO_MODEL_LAYER, MOVE_SELECTION_TO_INTERMEDIATE_LAYER, MOVE_SELECTION_TO_REAL_LAYER,
	GET_MODEL_LAYER, GET_INTERMEDIATE_LAYER, RUN_GAUSSIAN_RECIPE, RUN_MACRO,
	LOAD_ATOMS, SAVE_ATOMS, 
	COPY_GEOMETRY, COPY_POSITIONS, REPLACE_PARAMETERS, UPDATE_PARAMETERS, 
	COPY_PARTIAL_CHARGES, COPY_AMBERS, ALIGN_GEOMETRIES, COMPUTE_PARAMETER_SCORES,
	FORMAT_CHECKPOINT, CUBE_GEN }
	public static Map<string, TaskID> TaskIDMap = new Map<string, TaskID> {
		//Checker
		{"checkGeometry", TaskID.CHECK_GEOMETRY},
		//Cleaner
		{"standardiseWaters", TaskID.STANDARDISE_WATERS},
		{"removePDBSpecialCharacters", TaskID.REMOVE_PDB_SPECIAL_CHARACTERS},
		{"mergeNonStandardResidues", TaskID.MERGE_NSRS_BY_CONNECTIVITY},
		{"getChain", TaskID.GET_CHAIN},
		{"calculateAMBERTypes", TaskID.CALCULATE_AMBER_TYPES},
		{"calculateAMBERTypesWithAntechamber", TaskID.CALCULATE_AMBER_TYPES_ANTECHAMBER},
		{"calculateConnectivity", TaskID.CALCULATE_CONNECTIVITY},
		{"clearConnectivity", TaskID.CLEAR_CONNECTIVITY},
		{"selectNonStandardResidues", TaskID.SELECT_NONSTANDARD_RESIDUES},
		{"fillMissingResidues", TaskID.FILL_MISSING_RESIDUES},
		{"optimiseMissingResidues", TaskID.OPTIMISE_MISSING_RESIDUES},
		{"optimiseWithAmber", TaskID.OPTIMISE_AMBER},
		//Protonator
		{"protonateWithPDB2PQR", TaskID.PROTONATE_PDB2PQR},
		{"protonateWithReduce", TaskID.PROTONATE_REDUCE},
		{"getNSRs", TaskID.GET_NSRS},
		{"getSRs", TaskID.GET_SRS},
		//Mutation
		{"mutateResidue", TaskID.MUTATE_RESIDUE},
		//Partial Charges
		{"calculatePartialChargesRED", TaskID.CALCULATE_PARTIAL_CHARGES_RED},
		{"calculatePartialChargesGaussian", TaskID.CALCULATE_PARTIAL_CHARGES_GAUSSIAN},
		{"getPartialChargesFromMol2", TaskID.GET_PARTIAL_CHARGES_FROM_MOL2},
		//Geometry Merger
		{"mergeGeometries", TaskID.MERGE_GEOMETRIES},
		//Parameters
		{"calculateParameters", TaskID.CALCULATE_PARAMETERS},
		{"computeParameterScores", TaskID.COMPUTE_PARAMETER_SCORES},
		//Layers
		{"moveAllToModelLayer", TaskID.MOVE_ALL_TO_MODEL_LAYER},
		{"moveAllToIntermediateLayer", TaskID.MOVE_ALL_TO_INTERMEDIATE_LAYER},
		{"moveAllToRealLayer", TaskID.MOVE_ALL_TO_REAL_LAYER},
		{"moveSelectionToModelLayer", TaskID.MOVE_SELECTION_TO_MODEL_LAYER},
		{"moveSelectionToIntermediateLayer", TaskID.MOVE_SELECTION_TO_INTERMEDIATE_LAYER},
		{"moveSelectionToRealLayer", TaskID.MOVE_SELECTION_TO_REAL_LAYER},
		{"validateLayers", TaskID.VALIDATE_LAYERS},
		{"getModelLayer", TaskID.GET_MODEL_LAYER},
		{"getIntermediateLayer", TaskID.GET_INTERMEDIATE_LAYER},
		//Gaussian
		{"setupCalculation", TaskID.SETUP_CALCULATION},
		{"runGaussianRecipe", TaskID.RUN_GAUSSIAN_RECIPE},
		{"formchk", TaskID.FORMAT_CHECKPOINT},
		{"cubegen", TaskID.CUBE_GEN},
		//Macros
		{"runMacro", TaskID.RUN_MACRO},

		//Geometry Interface
		{"loadAtoms", TaskID.LOAD_ATOMS},
		{"saveAtoms", TaskID.SAVE_ATOMS},
		//Dragged GI Tasks
		{"copyGeometry", TaskID.COPY_GEOMETRY},
		{"copyPositions", TaskID.COPY_POSITIONS},
		{"replaceParameters", TaskID.REPLACE_PARAMETERS},
		{"updateParameters", TaskID.UPDATE_PARAMETERS},
		{"copyPartialCharges", TaskID.COPY_PARTIAL_CHARGES},
		{"copyAmbers", TaskID.COPY_AMBERS},
		{"alignGeometries", TaskID.ALIGN_GEOMETRIES}
	};


    //BOND TYPES
    public enum BondType : int { NONE, SINGLE, AROMATIC, DOUBLE, TRIPLE }
	public static Map<string, BondType> BondTypeMap = new Map<string, BondType> {
		{"", BondType.NONE},
		{"S", BondType.SINGLE},
		{"A", BondType.AROMATIC},
		{"D", BondType.DOUBLE},
		{"T", BondType.TRIPLE}
	};

    //CHECKERS
    public enum ResidueCheckerID : int { NONE, PROTONATED, PDBS_UNIQUE, STANDARD, PARTIAL_CHARGES, INTEGER_CHARGE }

	public static Map<string, ResidueCheckerID> ResidueCheckerIDMap = new Map<string, ResidueCheckerID> {
		{"none", ResidueCheckerID.NONE},
		{"protonated", ResidueCheckerID.PROTONATED},
		{"pdbsUnique", ResidueCheckerID.PDBS_UNIQUE},
		{"standard", ResidueCheckerID.STANDARD},
		{"partialCharges", ResidueCheckerID.PARTIAL_CHARGES},
		{"integerCharge", ResidueCheckerID.INTEGER_CHARGE}
	};


    public enum AtomCheckerID : int { NONE, HAS_PDB, HAS_AMBER, HAS_VALID_AMBER, PDBS_ALPHANUM }
	public static Map<string, AtomCheckerID> AtomCheckerIDMap = new Map<string, AtomCheckerID> {
		{"none", AtomCheckerID.NONE},
        {"hasPDB", AtomCheckerID.HAS_PDB},
		{"hasAMBER", AtomCheckerID.HAS_AMBER},
		{"hasValidAMBER", AtomCheckerID.HAS_VALID_AMBER},
        {"pdbsAlphanum", AtomCheckerID.PDBS_ALPHANUM}
	};

	//GAUSSIAN
	public enum GaussianPrintLevel : int {TERSE, NORMAL, ADDITIONAL}
	public static Map<string, GaussianPrintLevel> GaussianPrintLevelMap = new Map<string, GaussianPrintLevel> {
		{"T", GaussianPrintLevel.TERSE},
		{"N", GaussianPrintLevel.NORMAL},
		{"P", GaussianPrintLevel.ADDITIONAL}
	};


	//The order of OniomLayerID enum is important! 
	//REAL layer contains INTERMEDIATE layer contains MODEL layer, not the other way around.
	//See Residue.TakeLayer()
	public enum OniomLayerID : int {REAL, INTERMEDIATE, MODEL}
	public static Map<char, OniomLayerID> OniomLayerIDCharMap = new Map<char, OniomLayerID> {
		{'L', OniomLayerID.REAL},
		{'M', OniomLayerID.INTERMEDIATE},
		{'H', OniomLayerID.MODEL}
	};
	public static Map<string, OniomLayerID> OniomLayerIDStringMap = new Map<string, OniomLayerID> {
		{"Low", OniomLayerID.REAL},
		{"Intermediate", OniomLayerID.INTERMEDIATE},
		{"Model", OniomLayerID.MODEL}
	};


    //PDB
    public enum ResidueState : int {UNKNOWN, STANDARD, NONSTANDARD, C_TERMINAL, N_TERMINAL, HETERO, WATER, CAP, ION }
    public static Map<string, ResidueState> ResidueStateMap = new Map<string, ResidueState> {
		{"C_TER", ResidueState.C_TERMINAL},
		{"N_TER", ResidueState.N_TERMINAL},
		{"HETERO", ResidueState.HETERO},
		{"STANDARD", ResidueState.STANDARD},
		{"UNKNOWN", ResidueState.UNKNOWN},
		{"NONSTANDARD", ResidueState.NONSTANDARD},
		{"WATER", ResidueState.WATER},
		{"CAP", ResidueState.CAP},
		{"ION", ResidueState.ION}
	};


    //UI
    public enum ResidueProperty : int { NONE, CHAINID, RESIDUE_NUMBER, RESIDUE_NAME, STATE, STANDARD, PROTONATED, CHARGE, SIZE, SELECTED }
	public static Map<string, ResidueProperty> ResiduePropertyMap = new Map<string, ResidueProperty> {
        {"none", ResidueProperty.NONE},
		{"chainID", ResidueProperty.CHAINID},
		{"residueNumber", ResidueProperty.RESIDUE_NUMBER},
		{"residueName", ResidueProperty.RESIDUE_NAME},
		{"state", ResidueProperty.STATE},
		{"standard", ResidueProperty.STANDARD},
		{"protonated", ResidueProperty.PROTONATED},
		{"charge", ResidueProperty.CHARGE},
		{"size", ResidueProperty.SIZE},
		{"selected", ResidueProperty.SELECTED}
	};

    public enum PropertyDisplayType : int { NONE, STRING_EDITABLE, STRING_NONEDITABLE, FLOAT_EDITABLE, FLOAT_NONEDITABLE, INT_EDITABLE, INT_NONEDITABLE, BOOL_EDITABLE, BOOL_NONEDITABLE, BUTTON }
	public static Map<string, PropertyDisplayType> PropertyDisplayTypeMap = new Map<string, PropertyDisplayType> {
		{"stringEditable", PropertyDisplayType.STRING_EDITABLE},
		{"stringNonEditable", PropertyDisplayType.STRING_NONEDITABLE},
		{"floatEditable", PropertyDisplayType.FLOAT_EDITABLE},
		{"floatNonEditable", PropertyDisplayType.FLOAT_NONEDITABLE},
		{"intEditable", PropertyDisplayType.INT_EDITABLE},
		{"intNonEditable", PropertyDisplayType.INT_NONEDITABLE},
		{"boolEditable", PropertyDisplayType.BOOL_EDITABLE},
		{"boolNonEditable", PropertyDisplayType.BOOL_NONEDITABLE},
		{"button", PropertyDisplayType.BUTTON}
	};

    public enum GeometryInterfaceCallbackID : int { VIEW_ATOMS, VIEW_RESIDUE_TABLE, VIEW_ANALYSIS }
    public static Map<string, GeometryInterfaceCallbackID> GeometryInterfaceCallbackIDMap = new Map<string, GeometryInterfaceCallbackID> {
        {"viewAnalysis", GeometryInterfaceCallbackID.VIEW_ANALYSIS},
        {"viewAtoms", GeometryInterfaceCallbackID.VIEW_ATOMS},
        {"viewResidueTable", GeometryInterfaceCallbackID.VIEW_RESIDUE_TABLE},
    };

	public enum VanDerWaalsType : int { NONE, DREIDING, UFF, AMBER, MMFF94, MM2, OPLS }
	public static Map<int, VanDerWaalsType> VanDerWaalsTypeIntMap = new Map<int, VanDerWaalsType> {
		{0, VanDerWaalsType.NONE}, 
		{1, VanDerWaalsType.DREIDING}, 
		{2, VanDerWaalsType.UFF}, 
		{3, VanDerWaalsType.AMBER}, 
		{4, VanDerWaalsType.MMFF94},
		{5, VanDerWaalsType.MM2}, 
		{6, VanDerWaalsType.OPLS}
	};

	public static Map<string, VanDerWaalsType> VanDerWaalsTypeStringMap = new Map<string, VanDerWaalsType> {
		{"NONE", VanDerWaalsType.NONE}, 
		{"DREIDING", VanDerWaalsType.DREIDING}, 
		{"UFF", VanDerWaalsType.UFF}, 
		{"AMBER", VanDerWaalsType.AMBER}, 
		{"MMFF94", VanDerWaalsType.MMFF94},
		{"MM2", VanDerWaalsType.MM2}, 
		{"OPLS", VanDerWaalsType.OPLS}
	};

	public enum CoulombType : int { NONE, INVERSE, INVERSE_SQUARED, INVERSE_BUFFERED, DIPOLE }
	public static Map<int, CoulombType> CoulombTypeIntMap = new Map<int, CoulombType> {
		{0, CoulombType.NONE}, 
		{1, CoulombType.INVERSE}, 
		{2, CoulombType.INVERSE_SQUARED}, 
		{3, CoulombType.INVERSE_BUFFERED},
		{6, CoulombType.DIPOLE}
	};

	public static Map<string, CoulombType> CoulombTypeStringMap = new Map<string, CoulombType> {
		{"NONE", CoulombType.NONE}, 
		{"INVERSE", CoulombType.INVERSE}, 
		{"INVERSE_SQUARED", CoulombType.INVERSE_SQUARED}, 
		{"INVERSE_BUFFERED", CoulombType.INVERSE_BUFFERED},
		{"DIPOLE", CoulombType.DIPOLE}
	};

	public enum ConnectionType : int { NULL, C_VALENT, N_VALENT, SG_VALENT, C_OCCUPIED, N_OCCUPIED, SG_OCCUPIED, OTHER_VALENT, OTHER_OCCUPIED }
	public static Map<string, ConnectionType> ConnectionTypeMap = new Map<string, ConnectionType> {
		{"NULL", ConnectionType.NULL},
		{"C_VALENT", ConnectionType.C_VALENT},
		{"N_VALENT", ConnectionType.N_VALENT},
		{"SG_VALENT", ConnectionType.SG_VALENT},
		{"C_OCCUPIED", ConnectionType.C_OCCUPIED},
		{"N_OCCUPIED", ConnectionType.N_OCCUPIED},
		{"SG_OCCUPIED", ConnectionType.SG_OCCUPIED},
		{"OTHER_VALENT", ConnectionType.OTHER_VALENT},
		{"OTHER_OCCUPIED", ConnectionType.OTHER_OCCUPIED}
	};

	public enum AtomDisplayType : int { BALL_AND_STICK, TUBE, WIREFRAME }
	public enum AtomDisplayOption : int { SMALL, TRANSPARENT, PULSING }

	public enum AtomModificationID : int { NULL, HYDROGENS, REAL_ATOM, INT_ATOM, MODEL_ATOM, REAL_RESIDUE, INT_RESIDUE, MODEL_RESIDUE }

	public enum GaussianOptTarget : int { MINIMUM, TS }
	public static Map<GaussianOptTarget, string> GaussianOptTargetMap = new Map<GaussianOptTarget, string> {
		{ GaussianOptTarget.MINIMUM, "Minimum (Default)" },
		{ GaussianOptTarget.TS, "TS "}
	};

	public enum GaussianConvergenceThreshold : int { NORMAL, TIGHT, VERY_TIGHT, LOOSE, EXPERT }
	public static Map<GaussianConvergenceThreshold, string> GaussianConvergenceThresholdMap = new Map<GaussianConvergenceThreshold, string> {
		{ GaussianConvergenceThreshold.NORMAL, "Normal (Default)" },
		{ GaussianConvergenceThreshold.TIGHT, "Tight" }, 
		{ GaussianConvergenceThreshold.VERY_TIGHT, "VeryTight" }, 
		{ GaussianConvergenceThreshold.LOOSE, "Loose" }, 
		{ GaussianConvergenceThreshold.EXPERT, "Expert" }
	};

	public enum GaussianForceConstant : int { ESTIMATE, CALC_FIRST, CALC_ALL, RECALC, READ_FIRST, READ_CARTESIAN, OLD_ESTIMATE }
	public static Map<GaussianForceConstant, string> GaussianForceConstantMap = new Map<GaussianForceConstant, string> {
		{GaussianForceConstant.ESTIMATE, "NewEstmFC (Default)"}, 
		{GaussianForceConstant.CALC_FIRST, "CalcFC"}, 
		{GaussianForceConstant.CALC_ALL, "CalcAll"}, 
		{GaussianForceConstant.RECALC, "RecalcFC"}, 
		{GaussianForceConstant.READ_FIRST, "ReadFC"}, 
		{GaussianForceConstant.READ_CARTESIAN, "RCFC"}, 
		{GaussianForceConstant.OLD_ESTIMATE, "EstmFC"}
	};

	public enum Element : int {
		X, H , He,
		Li, Be, B , C , N , O , F , Ne,
		Na, Mg, Al, Si, P , S , Cl, Ar, 
		K , Ca, Sc, Ti, V , Cr, Mn, Fe, Co, Ni, Cu, Zn, Ga, Ge, As, Se, Br, Kr,
		Rb, Sr, Y , Zr, Nb, Mo, Tc, Ru, Rh, Pd, Ag, Cd, In, Sn, Sb, Te, I , Xe,
		Cs, Ba, La, Ce, Pr, Nd, Pm, Sm, Eu, Gd, Tb, Dy, Ho, Er, Tm, Yb, Lu, Hf, Ta, W , Re, Os, Ir, Pt, Au, Hg, Tl, Pb, Bi, Po, At, Rn,
		Fr, Ra, Ac, Th, Pa, U , Np, Pu, Am, Cm, Bk, Cf, Es, Fm, Md, No, Lr, Rf, Db, Sg, Bh, Hs, Mt, Ds, Rg, Cn, Nh, Fl, Mc, Lv, Ts, Og
	}


	public static Map<string, Element> ElementMap = System.Enum.GetValues(typeof(Element)).Cast<Element>().ToMap(x => x.ToString(), x => x);

	public enum Amber : System.Int16 {

		X, // Generic No Amber
		_, // * - all Ambers
		DU, // Unrecognised Amber
		//Hydrogens
		H, HA, HC, HO, HP, HS, HW, H1, H2, H3, H4, H5,
		// Carbons
		C, C_, CA, CB, CC, CD, CE, CF, CG, CH, CI, CJ, CK, CM, CN, CQ, CR, CP, CT, CV, CW, CX, CY, CZ, C2, C3, 
		// Nitrogens
		N, N_, NA, NB, NC, NO, NP, NT, N2, N3, 
		// Oxygen
		O, OH, OS, OW, O2, S, SH, 
		// Phosphorus
		P, 
		// Other
		CL, Cs, CS, CU, CO, F, FE, I, IB, IM, IP, K, Li, MG, QC, QK, QL, QN, QR, Rb, LP,

	}
	
	public static Map<string, Amber> AmberMap = System.Enum.GetValues(typeof(Amber)).Cast<Amber>().ToMap(x => x.ToString().Replace("_", "*"), x => x);



}
