using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TID = Constants.TaskID;
using AID = Constants.ArrowID;
using GIID = Constants.GeometryInterfaceID;
using EL = Constants.ErrorLevel;

public static class ArrowFunctions {

    // This function performs all the tasks to get from one GeometryInterface to another
    public delegate IEnumerator ArrowProcedure(GIID startID, GIID endID, List<TID> tasks);

    // This is an individual task and part of a Procedure
    public delegate IEnumerator Task(GIID geometryInterfaceID);

    // ARROW PROCEDURES

    public static IEnumerator GetArrowProcedure(AID arrowID, GIID startID, GIID endID, List<TID> tasks) {
        ArrowProcedure arrowProcedure;
        try {
            arrowProcedure = GetArrowProcedure(arrowID);
        } catch {
            CustomLogger.LogFormat(
                EL.ERROR, 
                "No Arrow Procedure for Arrow ID: {0}", 
                arrowID

            );
            yield break;
        }
        yield return arrowProcedure(startID, endID, tasks);
    }

    private static ArrowProcedure GetArrowProcedure(AID arrowID) {
        
        switch (arrowID) {
            case (AID.ORIGINAL_TO_CLEANED):
                return Cleaner.GetCleanedGeometry;
            case (AID.CLEANED_TO_NSR):
                return NonStandardResidueTools.GetNonStandardResidues;
            case (AID.CLEANED_TO_SR):
                return NonStandardResidueTools.GetStandardResidues;
            case (AID.SR_TO_PCSR):
                return Protonator.GetProtonatedStandardResidues;
            case (AID.NSR_TO_PNSR):
                return Protonator.GetProtonatedNonStandardResidues;
            case (AID.PNSR_TO_PCNSR):
                return PartialChargeCalculator.GetPartialCharges;
            case (AID.PCNSR_TO_COMBINED):
            case (AID.PCSR_TO_COMBINED):
                return NonStandardResidueTools.MergeGeometries;
            case (AID.COMBINED_TO_ONELAYER):
            case (AID.MODEL_TO_TWOLAYER):
            case (AID.INTERMEDIATE_TO_THREELAYER):
                return CalculationSetup.SetupCalculation;
            case (AID.COMBINED_TO_MODEL):
                return CalculationSetup.GetModelLayer;
            case (AID.MODEL_TO_INTERMEDIATE):
                return CalculationSetup.GetIntermediateLayer;
            default:
                throw new ErrorHandler.InvalidArrowID(
                    string.Format("No Arrow Procedure for Arrow ID: {0}", arrowID),
                    arrowID
                );
        }
    }

    // TASKS
    
    public static Task GetTask(TID taskID) {
        switch (taskID) {

            //Geometry Interface
            case (TID.LOAD_ATOMS):
                return GeometryInterface.LoadFile;
            case (TID.SAVE_ATOMS):
                return GeometryInterface.SaveFile;
            
            //Cleaner
            case (TID.STANDARDISE_WATERS): 
                return Cleaner.StandardiseWaters;
            case (TID.REMOVE_PDB_SPECIAL_CHARACTERS): 
                return Cleaner.RemovePDBSpecialCharacters;
            case (TID.GET_CHAIN): 
                return Cleaner.GetChain;
            case (TID.CHECK_GEOMETRY): 
                return Cleaner.CheckGeometry;
            case (TID.CALCULATE_CONNECTIVITY): 
                return Cleaner.CalculateConnectivity;
            case (TID.CLEAR_CONNECTIVITY): 
                return Cleaner.DisconnectAll;
            case (TID.FILL_MISSING_RESIDUES):
                return Cleaner.FillMissingResidues;
            case (TID.OPTIMISE_MISSING_RESIDUES):
                return Cleaner.OptimiseMissingResidues;
                
            // Non-Standard Residue Tasks
            case (TID.GET_SRS):
                return NonStandardResidueTools.GetStandardResidues;
            case (TID.GET_NSRS): 
                return NonStandardResidueTools.GetNonStandardResidues;
            case (TID.SELECT_NONSTANDARD_RESIDUES):
                return NonStandardResidueTools.SelectNonStandardResidues;
            case (TID.MERGE_NSRS_BY_CONNECTIVITY): 
                return NonStandardResidueTools.MergeNSRsByProximity;
            case (TID.MERGE_GEOMETRIES):
                return NonStandardResidueTools.MergeGeometries;

            //AMBER
            case (TID.CALCULATE_AMBER_TYPES): 
                return AmberCalculator.CalculateAMBERTypes;
            case (TID.CALCULATE_AMBER_TYPES_ANTECHAMBER): 
                return AmberCalculator.CalculateAMBERTypesAntechamber;
            case (TID.CALCULATE_PARAMETERS):
                return AmberCalculator.CalculateAMBERParameters;

            // Protonator
            case (TID.PROTONATE_PDB2PQR): 
                return Protonator.ProtonateWithPDB2PQR;
            case (TID.PROTONATE_REDUCE): 
                return Protonator.ProtonateNonStandard;

            // Partial Charges Calculator
            case (TID.CALCULATE_PARTIAL_CHARGES_RED):
                return PartialChargeCalculator.CalculatePartialChargesRED;
            case (TID.CALCULATE_PARTIAL_CHARGES_GAUSSIAN):
                return PartialChargeCalculator.CalculatePartialChargesGaussian;
            case (TID.GET_PARTIAL_CHARGES_FROM_MOL2):
                return PartialChargeCalculator.GetPartialChargesFromMol2;

            // Layers
            case (TID.MOVE_ALL_TO_REAL_LAYER):
                return CalculationSetup.MoveAllToRealLayer;
            case (TID.MOVE_ALL_TO_INTERMEDIATE_LAYER):
                return CalculationSetup.MoveAllToIntermediateLayer;
            case (TID.MOVE_ALL_TO_MODEL_LAYER):
                return CalculationSetup.MoveAllToModelLayer;
            case (TID.MOVE_SELECTION_TO_REAL_LAYER):
                return CalculationSetup.MoveSelectionToRealLayer;
            case (TID.MOVE_SELECTION_TO_INTERMEDIATE_LAYER):
                return CalculationSetup.MoveSelectionToIntermediateLayer;
            case (TID.MOVE_SELECTION_TO_MODEL_LAYER):
                return CalculationSetup.MoveSelectionToModelLayer;
            case (TID.VALIDATE_LAYERS):
                return CalculationSetup.ValidateGeometry;
            case (TID.GET_MODEL_LAYER):
                return CalculationSetup.GetModelLayer;
            case (TID.GET_INTERMEDIATE_LAYER):
                return CalculationSetup.GetIntermediateLayer;

            // Gaussian Calculation
            case (TID.SETUP_CALCULATION):
                return CalculationSetup.SetupCalculation;
            case (TID.RUN_GAUSSIAN_RECIPE):
                return GaussianRecipe.RunGaussianRecipe;

            default:
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Invalid Task ID: {0}",
                    taskID
                );
                return EmptyTask;
        }
    }

    private static IEnumerator EmptyTask(GIID geometryInterfaceID) {
        yield break;
    }

}
