# ONIOM Flow - A GUI for Building ONIOM(QM/MM) Gaussian Input Files from PDB

This repository contains the source code and assets for building a Unity3D application for Windows and MacOS. As such, you must have a copy of Unity3D to use the code.

The software is aimed at simplifying ONIOM calculations on proteins by automating many of the tasks involved in the preparation of their inputs. The main GUI contains each of the major stages between a PDB crystal structure file and an Amber, two-layer or three layer ONIOM Gaussian input file. Stages, called Geometry Interfaces here, are linked by arrows, through which tasks can be sequentially performed to go between stages.

## Requirements before starting

For building deployables:

Unity3D


For usage:

Gaussian 09 or 16

antechamber, reduce, parmchk2

pdb2pqr


Optional: 

RED-vIII


## Setting up

Upon selecting a project folder to contain progress, you will be asked to locate the above executables if they are not available in your environment (~/.bashrc or ~/.bash_profile). This will create a new project. The settings files can be copied to new projects if needed.

## Interacting

Geometries can be loaded into any Geometry Interface (GI) by clicking the Open Geometry button (bottom centre of the GI). If a geometry is already loaded, you will be asked whether the previous geometry should be overwritten or updated (charges, positions or parameters - useful for loading in changes from external software).

Geometries can be saved to a file by clicking the Save Geometry button (bottom right of the GI). Available formats are .gjf, .com, .pdb, .p2n, .mol2 and .xat.

GIs can be dragged onto other GIs to copy, overwrite or update Geometries. This is useful for performing optimisations and updating positions of different GIs.

Clicking arrows linking GIs opens a window with a sequence of tasks that will be performed from top to bottom if the "Confirm" button is pressed. The order of tasks can be changed by dragging tasks up or down. Tasks can be added by dragging them from the panel on the right, and removed by dragging them out of the window (until they turn grey). 

The "Check Geometry" task is usually performed last. It will run a series of tests to check whether the Geometry is suitable for the next step. If the GI is green, it is ready to go. Yellow indicates something is probably wrong - a missing parameter, charge, proton, a strange or dangling bond etc. Red indicates the geometry is unsuitable for the next step. Errors and warnings that occur during tasks are flagged in the top left of the GUI and are written in the Flow.log file in the project directory.

Right clicking a GI opens a context menu, from which tasks can be performed directly on a GI, such as visualising atoms and running Macros.

The Geometry Visualiser is used to inspect and edit Geometries. Bonds, angles and dihedrals can be adjusted. When two, three or four atoms respectively are chosen, 5 symbols will appear indicating the left side lock status, left side handle, pivot point, right side handle and right side lock status. The lock status inicate what will happen when a handle is moved - locked: all atoms connected to that side's atom will move; sprung: atoms connected that are close will move, with movement scaled inversely to graph distance; unlocked: only that atom will move. The pivot point is used to apply a weight to the left and right side - by moving it towards the left, the right side will move more compared to the right.

Additionally in the visualiser, residues can be removed, added or mutated. Hydrogens can be added or removed manually. By changing the colour mode charges, atom types and parameters can be checked. ONIOM layers and boundaries can be inspected and modified.

## Macros

Macros allow an additional layer of customisable automation. Right click a GI and select "Run Macro" to be prrompted to select an .xml macro file. A few examples are given in the Recipes directory of the project.

## XAT Format

The XAT format, based on XML, is the native file format for storing geometries and is only used by this software. As such, it should just be used for saving or trransferring progress.