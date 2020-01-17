GAUSSIAN RECIPES

Gaussian recipe files (.xml) contain user-generated proceedures for running
sequences of Gaussian calculations and internal actions. The results of these
calculations can update Geometries' positions or charges.


1. FILE STRUCTURE

The recipe should be enclosed be a <recipe> tag. Inside this, variables are
declared (<var> tag - see VARIABLES) followed by calculation groups
(<group> tag - see CALCUATION GROUPS).


2. VARIABLES

A variable replaces string values throughout the file. They are used to reduce
mistakes and improve readability. 


2.1 USER-DEFINED VARIABLES

A user-defined variable is declared as follows:

<var id="ID">VALUE</var>

where ID is the string to replace and VALUE is the string to replace with e.g.

<var id="NPROC">4</var>
<var id="MEM">8000</var>
<var id="NAME">1GFL</var>

Variables are accessed using the dollar sign and parentheses - $(ID). 
For example:

<title>Optimisation on $(NAME)</title>

will expand to:

<title>Optimisation on 1GFL</title>
        

2.2 RESERVED STRING VARIABLES

Certain variables are internally reserved. The reserved string variables are
as follows:

$(PROJECT)          The directory loaded on initialisation
$(CHARGES_DIR)      The value of <chargesDirectory> in ProjectSettings.xml
$(SETTINGS_PATH)    The Settings Path
$(DATA_PATH)        The Data Path
$(CHAINS)           Expands to the concatenation of Chain IDs
$(RESIDUES)         Expands to the concatenation of Residue IDs
$(GEOMETRY)         A reference to the Geometry that the recipe is being run on
        

2.2 SPECIAL VARIABLES

Some values in the recipe file must use special variables where strings cannot
be used. This is to prevent ambiguity. 

Residue State (RS):
    Select residues of a particular type
    If not specified, selects the entire geometry

    The following are accepted:
    $(STANDARD):     Standard residues
    $(NONSTANDARD):  Non-Standard residues
    $(ION):          Metal ions
    $(C_TERMINAL):   Carbon terminal residues
    $(N_TERMINAL):   Nitrogen terminal residues
    $(HETERO):       Hetero-Residues
    $(WATER):        Water residues
    $(CAP):          Cap residues
    
ONIOM Layer ID (OLID):
    Select an ONIOM Layer by ID

    The following are accepted:
    $(REAL):         Real Layer
    $(INTERMEDIATE): Intermediate Layer
    $(MODEL):        Model Layer

Gaussian Convergence Threshold (GCT):
    Sets an optimisation target:

    The following are accepted:
    $(NORMAL):      Normal Convergence Threshold
    $(TIGHT):       Strict Convergence Threshold
    $(VERY_TIGHT):  Very Strict Convergence Threshold
    $(LOOSE):       Loose Convergence Threshold
    $(EXPERT):      Very Loose Convergence Threshold


3: CALCUATION GROUPS

All calculations and actions are performed in <group> tags. The input Geometry
to the group is declared in the "source" attribute. Depending on how the group 
is cut up ("type" attribute - see GROUP TYPES), the "name" attribute defines 
the name of the current sub-group. e.g.

<group type="connected" name="group" source="protein">...</group>

will select all groups of connected residues from "protein", naming each of
them  "group" and performing the calculations and actions in the ellipses.


3.1 GROUP TYPES

There are 4 defined ways to cut up a Geometry:
connected:  run separate calculations for each group of connected residues
perResidue: run separate calculations for each residue
perChain:   run separate calculations for each chain
geometry:   run calculations on the entire geometry


3.2 ACTIONS

Actions are defined within a group in an <action> tag, with the action id in
the "id" attribute. e.g.

<action id="ID">VALUE</action>


3.2.1 Move To Layer

Moves the atoms selected in VALUE to the defined ONIOM Layer.

id: MoveToLayer
params: residueState (RS - optional), residueIDs (ResidueID - optional)
value: (OLID - optional)

examples:
<action id="MoveToLayer" residueState="$(WATER)">$(REAL)</action>
moves all water residues to the Real layer.

<action id="MoveToLayer" residueIDs="A31,A32,A33">$(MODEL)</action>
moves Residues A31, A32 and A33 to the Model layer.


3.2.2 Estimate Charge and Multiplicity

Predicts the group's charge and multiplicity.

id: EstimateChargeMultiplicity
params: confirm (true/false - optional defaulting to false)
value: None

examples:

<action id="EstimateChargeMultiplicity"></action>
Predicts the group's charge and multiplicity without a prompt.

<action id="EstimateChargeMultiplicity" confirm="true"></action>
Predicts the group's charge and multiplicity with a prompt.


3.2.3 Generate Atom Map

Maps the Atomic index to AtomID.
Should be performed whenever atoms might be changed before calculations.

id: GenerateAtomMap
params: None
value: None




            GenerateAtomMap:
                Maps the Atomic index to AtomID.
                Should be performed whenever atoms might be changed before calculations

            RedistributeCharge:
                Moves charges from Caps to Residues
                Params:
                    distribution:
                        The amount of charge to distribute to each bonded distance from the link between the Cap and the Residue
                        Should (usually) sum to 1.
                        Zeros the charge on the Cap residue/s
                        e.g.:
                            <distribution>0.4 0.3 0.2 0.1</distribution>
                            This will distribute:
                            0.4 times cap charge on 1st atom
                            0.3 times cap charge on atoms connected to 1st atom
                            0.2 times cap charge on atoms connected to 2nd atom/s
                            0.1 times cap charge on atoms connected to 3rd atom/s
            RoundCharge:
                Rounds the residue charge to the nearest integer charge


            ComputeConnectivity:
                Generates a connectivity graph that can be used by Gaussian
        -->
