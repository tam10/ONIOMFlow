using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using System;
using System.Linq;
using EL = Constants.ErrorLevel;
using Amber = Constants.Amber;

public static class FRCMODReader {

    enum ParamType : int { NULL, MASS, BOND, ANGLE, DIHEDRAL, IMPROPER, NONBON}
    static Dictionary<string, ParamType> paramTypeDict = new Dictionary<string, ParamType> {
        {"MASS", ParamType.MASS},
        {"BOND", ParamType.BOND},
        {"ANGLE", ParamType.ANGLE},
        {"DIHE", ParamType.DIHEDRAL},
        {"IMPROPER", ParamType.IMPROPER},
        {"NONBON", ParamType.NONBON}
    };
    
    static ParamType paramType;
    static FileStream fileStream;
    static StreamReader streamReader;
    static int lineNum;
    static string line;

    public static IEnumerator ParametersFromFRCMODFile(string path, Parameters parameters) {
        lineNum = 0;
        paramType = ParamType.NULL;
        using (fileStream = File.OpenRead(path)) {
            using (streamReader = new StreamReader(fileStream, Encoding.UTF8, true)) {
                while (GetNextLine()) {
                    ProcessLine(parameters);

                    if (Timer.yieldNow) {
                        yield return null;
                    }
                }
            }
        }
    }

    private static bool GetNextLine() {
        line = streamReader.ReadLine();
        if (line is null) {return false;}
        lineNum++;
        line = line.Trim();
        return true;
    }

    private static void ProcessLine(Parameters parameters) {
        if (line == "") {
            paramType = ParamType.NULL;
        } else if (paramType == ParamType.NULL) {
            paramTypeDict.TryGetValue(line, out paramType);
        } else {
            bool needsRevision = NeedsRevision();

            AtomicParameter existingParameter;
            switch (paramType) {
                case (ParamType.NULL):
                    paramTypeDict.TryGetValue(line, out paramType);
                    break;
                case (ParamType.MASS):
                    AtomicParameter massParameter = ReadMass();
                    if (parameters.TryGetAtomicParameter(massParameter.type, out existingParameter)) {
                        existingParameter.mass = massParameter.mass;
                    } else {
                        parameters.AddAtomicParameter(massParameter);
                    }
                    
                    break;
                case (ParamType.NONBON):
                    AtomicParameter nonBonParameter = ReadNonBon();
                    if (parameters.TryGetAtomicParameter(nonBonParameter.type, out existingParameter)) {
                        existingParameter.wellDepth = nonBonParameter.wellDepth;
                        existingParameter.radius = nonBonParameter.radius;
                    } else {
                        parameters.AddAtomicParameter(nonBonParameter);
                    }
                    break;
                case (ParamType.BOND):
                    Stretch stretch = ReadBond();
                    if (needsRevision) {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "Bad Stretch added! ({0})", 
                            stretch
                        );
                    } else if (stretch.penalty > 0f) {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "Adding Stretch with penalty={0} ({1})", 
                            stretch.penalty, 
                            stretch
                        );
                    }
                    parameters.AddStretch(stretch);
                    break;
                case (ParamType.ANGLE):
                    Bend bend = ReadAngle();
                    if (needsRevision) {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "Bad Bend added! ({0})", 
                            bend
                        );
                    } else if (bend.penalty > 0f) {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "Adding Bend with penalty={0} ({1})", 
                            bend.penalty, 
                            bend
                        );
                    }
                    parameters.AddBend(bend);
                    break;
                case (ParamType.DIHEDRAL):
                    Torsion torsion = ReadDihedral();
                    if (needsRevision) {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "Bad Torsion added! ({0})", 
                            torsion
                        );
                    } else if (torsion.penalty > 0f) {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "Adding Torsion with penalty={0} ({1})", 
                            torsion.penalty, 
                            torsion
                        );
                    }
                    
                    parameters.AddTorsion(torsion);
                    break;
                case (ParamType.IMPROPER):
                    ImproperTorsion improper = ReadImproper();
                    if (needsRevision) {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "Bad Improper Torsion added! ({0})", 
                            improper
                        );
                    } else if (improper.penalty > 0f) {
                        CustomLogger.LogFormat(
                            EL.WARNING,
                            "Adding Improper Torsion with penalty={0} ({1})", 
                            improper.penalty, 
                            improper
                        );
                    }
                    
                    parameters.AddImproperTorsion(improper);
                    break;
            }
        }
    }

    private static float GetPenalty() {
        //See if there's a penalty score at the end of the line, and return it (or return zero if it doesn't exist)
        //Example:
        // CT-N   330.60   1.460       same as c3- n, penalty score=  0.0
        string penaltyString = line.Split(new[] {'='}, System.StringSplitOptions.RemoveEmptyEntries).Last().Trim();
        float penalty = 0f;
        float.TryParse(penaltyString, out penalty);
        return penalty;
    }

    private static bool NeedsRevision() {
        return line.Contains("ATTN");
    }

    private static AtomicParameter ReadMass() {
        //Read a mass (but ignore polarisability) for an element
        //Header: MASS
        //Example:
        //N  14.010        0.530               same as n 
        //type  mass  polarisability comment
        Amber type = AmberCalculator.GetAmber(line.Substring(0, 2).Trim());
        float mass = float.Parse(line.Substring(3, 7));

        AtomicParameter atomicParameter = new AtomicParameter(type);
        atomicParameter.mass = mass;
        return atomicParameter;
        
    }

    private static AtomicParameter ReadNonBon() {
        //Read a radius and well depth for an element
        //Header: NONBON
        //Example:
        //  N           1.8240  0.1700             same as n  
        //type  radius  wellDepth  comment
        //Note 2 spaces at start of line - but GetNextLine() is Trimming it anyway

        Amber type = AmberCalculator.GetAmber(line.Substring(0, 2).Trim());
        float radius = float.Parse(line.Substring(11, 7));
        float wellDepth = float.Parse(line.Substring(19, 7));
        float penalty = GetPenalty();

        AtomicParameter atomicParameter = new AtomicParameter(type);
        atomicParameter.radius = radius;
        atomicParameter.wellDepth = wellDepth;
        atomicParameter.penalty = penalty;
        return atomicParameter;
    }

    private static Stretch ReadBond() {
        //Read a force constant and equilibrium distance for a Stretch parameter
        //Header: BOND
        //Example:
        //CT-N   330.60   1.460       same as c3- n, penalty score=  0.0
        //type0-type1  keq  req  comment

        Amber t0 = AmberCalculator.GetAmber(line.Substring(0, 2).Trim());
        Amber t1 = AmberCalculator.GetAmber(line.Substring(3, 2).Trim());

        float keq = float.Parse(line.Substring(6,7));
        float req = float.Parse(line.Substring(15,6));

        float penalty = GetPenalty();

        if (req == 0f) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Error in parameters - stretch has a length of 0! This could mean a parameter is missing in the force field. ({0}-{1}) on line {3}.",
                () => {
                    return new object[3] {
                        t0, t1,
                        lineNum
                    };
                }
            );
        }

        return new Stretch(t0, t1, req, keq, penalty);
    }

    private static Bend ReadAngle() {
        //Read a force constant and equilibrium angle for a Bend parameter
        //Header: ANGLE
        //Example:
        //CK-CT-N    66.840     111.710   same as cc-c3-n , penalty score=  0.0
        //type0-type1-type2  keq  req  comment

        Amber t0 = AmberCalculator.GetAmber(line.Substring(0, 2).Trim());
        Amber t1 = AmberCalculator.GetAmber(line.Substring(3, 2).Trim());
        Amber t2 = AmberCalculator.GetAmber(line.Substring(6, 2).Trim());

        float keq = float.Parse(line.Substring(10,7));
        float aeq = float.Parse(line.Substring(22,7));

        float penalty = GetPenalty();

        if (aeq == 0f) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Error in parameters - bend has an angle of 0! This could mean a parameter is missing in the force field. ({0}-{1}-{2}) on line {3}.",
                () => {
                    return new object[4] {
                        t0, t1, t2,
                        lineNum
                    };
                }
            );
        }

        return new Bend(t0, t1, t2, aeq, keq, penalty);
    }

    private static Torsion ReadDihedral() {
        //Read a Torsion parameter
        //Header: DIHE
        //Example (single line):
        //NB-CK-CT-N    6    0.000         0.000           3.000      same as X -c3-cd-X , penalty score=  0.0
        //type0-type1-type2-type3  numPaths  barrierHeight  phase  periodicity  comment
        //
        //Example (multi-line):
        //CT-CT-OH-HO   1    0.160         0.000          -3.000      same as ho-oh-c3-c3
        //CT-CT-OH-HO   1    0.250         0.000           1.000      same as ho-oh-c3-c3, penalty score=  0.0
        //type0-type1-type2-type3  numPaths  barrierHeight  phase  periodicity  comment
        //A negative value of periodicity indicates the next line is also part of this Torsion

        Amber t0 = AmberCalculator.GetAmber(line.Substring(0, 2).Trim());
        Amber t1 = AmberCalculator.GetAmber(line.Substring(3, 2).Trim());
        Amber t2 = AmberCalculator.GetAmber(line.Substring(6, 2).Trim());
        Amber t3 = AmberCalculator.GetAmber(line.Substring(9, 2).Trim());
        int numPaths = int.Parse(line.Substring(14, 1));

        float penalty = GetPenalty();

        bool readTorsion = true;
        Torsion torsion = new Torsion(t0, t1, t2, t3, penalty:penalty);
        torsion.npaths = numPaths;


        while (readTorsion) {
            t0 = AmberCalculator.GetAmber(line.Substring(0, 2).Trim());
            t1 = AmberCalculator.GetAmber(line.Substring(3, 2).Trim());
            t2 = AmberCalculator.GetAmber(line.Substring(6, 2).Trim());
            t3 = AmberCalculator.GetAmber(line.Substring(9, 2).Trim());

            if (!torsion.types.TypeEquivalent(t0, t1, t2, t3)) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "FRCMOD file badly formatted - terms should be grouped together. Expecting ({0}), got ({1}-{2}-{3}-{4}) on line {5}.",
                    () => {
                        return new object[6] {
                            torsion.GetTypesString(),
                            t0, t1, t2, t3,
                            lineNum
                        };
                    }
                );
                return torsion;
            }

            float barrierHeight = float.Parse(line.Substring(17,7));
            float phase = float.Parse(line.Substring(31,7));
            float periodicity = float.Parse(line.Substring(47,7));

            int absPeriodicity = Mathf.RoundToInt(Math.Abs(periodicity)) - 1;

            if (absPeriodicity == -1) {
                CustomLogger.LogFormat(
                    EL.ERROR,
                    "Error in parameters - torsion has invalid periodicity! This could mean a parameter is missing in the force field. ({0}-{1}-{2}-{3}) on line {4}.",
                    () => {
                        return new object[5] {
                            t0, t1, t2, t3,
                            lineNum
                        };
                    }
                );
                return torsion;
            }

            torsion.phaseOffsets[absPeriodicity] = phase;
            torsion.barrierHeights[absPeriodicity] = barrierHeight;

            if (periodicity < 0) {
                GetNextLine();
            } else {
                readTorsion = false;
            }
        }

        return torsion;
    }

    private static ImproperTorsion ReadImproper() {
        //Read an Improper Torsion parameter
        //Header: IMPROPER
        //Example:
        //CC-N*-C -O          1.1          180.0         2.0          Using the default value
        //type0-type1-type2-type3  barrierHeight  phase  periodicity

        Amber t0 = AmberCalculator.GetAmber(line.Substring(0, 2).Trim());
        Amber t1 = AmberCalculator.GetAmber(line.Substring(3, 2).Trim());
        Amber t2 = AmberCalculator.GetAmber(line.Substring(6, 2).Trim());
        Amber t3 = AmberCalculator.GetAmber(line.Substring(9, 2).Trim());
        float barrierHeight = float.Parse(line.Substring(17, 7));
        float phase = float.Parse(line.Substring(31, 7));
        int periodicity = Mathf.RoundToInt(float.Parse(line.Substring(45, 7)));

        float penalty = GetPenalty();

        ImproperTorsion improper = new ImproperTorsion(t0, t1, t2, t3, barrierHeight, phase, periodicity, penalty);

        return improper;
    }

    
}