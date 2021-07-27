using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BT = Constants.BondType;
using RS = Constants.ResidueState;
using EL = Constants.ErrorLevel;
using System.Linq;
using Unity.Mathematics;

public class LineDrawer : MonoBehaviour {


    public static Color glowRedColour = new Color(1.5f, 0, 0);
    public static Color glowGreenColour = new Color(0, 1.5f, 0);
    public static Color glowBlueColour = new Color(0.5f, 0.5f, 2f);
    public static Color disabledColour = Color.gray;

    Camera activeCamera;
	public Material lineMaterial;

    private Dictionary<ResidueID, ResidueWireFrame> residueWireFrames = new Dictionary<ResidueID, ResidueWireFrame>();
    private Dictionary<(AtomID, AtomID), LinkerWireFrame> linkerWireFrames = new Dictionary<(AtomID, AtomID), LinkerWireFrame>();
    public List<Arc> arcs = new List<Arc>(); 
    public List<Line> lines = new List<Line>();

    public enum AtomColour: int {ELEMENT, CHARGE, HAS_AMBER, PARAMETERS, SASA, CAP, MUTATE, REMOVE}
    public static int numAtomColourTypes = 8; //Must be the length of the above enum

    public static float maxSASA = 0.001f;

    public static float3 cameraPosition;
    public static float frustumRatio;
    public static float focalLength;
    public static float4x4 localToWorldMatrix;

    public bool animateVibrations;
    public static float3 v = new float3(0.5f, 1f, -1f);
    public static float t;
    public float vibrationRate = 1f;
    public float vibrationMagnitude = 1f;

    void Update() {
        if (animateVibrations) {
            t = vibrationMagnitude * math.sin(math.PI * 2f * vibrationRate * Time.realtimeSinceStartup);
        } else {
            t = 0;
        }
    }

    void Start() {
        animateVibrations = false;
        activeCamera = Camera.main;
        maxSASA = 0.001f;
    }

    void Awake() {
        PostRenderer.main.lineDrawers.Add(this);
    }

    void OnDestroy() {
        if (PostRenderer.main != null) PostRenderer.main.lineDrawers.Remove(this);
    }

    public void AddResidue(Residue residue, float3 offset) {
        if (residue == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot add Residue to LineDrawer - Residue is null!"
            );
            return;
        }
        residueWireFrames[residue.residueID] = new ResidueWireFrame(this.transform, residue, offset);
    }

    public void AddLinker(Atom atom0, Atom atom1, AtomID atomID0, AtomID atomID1, float3 offset) {
        
        if (atom0 == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot add Linker to LineDrawer - atom0 ('{0}') is null!",
                atomID0
            );
            return;
        }
        
        if (atom1 == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot add Linker to LineDrawer - atom1 ('{0}') is null!",
                atomID1
            );
            return;
        }

        if (atomID1 > atomID0) {
            linkerWireFrames[(atomID0, atomID1)] = new LinkerWireFrame(this.transform, atom0, atom1, atomID0, atomID1, offset);
        } else {
            linkerWireFrames[(atomID1, atomID0)] = new LinkerWireFrame(this.transform, atom1, atom0, atomID1, atomID0, offset);

        }
    }

    public void AddLine(float3 start, float3 end, Color color, float3 offset) {
        lines.Add(new Line(start+offset, end+offset, color));
    }

    public void ClearLines() {
        lines.Clear();
    }

    public void RemoveResidue(ResidueID residueID) {
        if (!residueWireFrames.ContainsKey(residueID)) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot remove Residue '{0}' from LineDrawer - Residue not present in LineDrawer!",
                residueID
            );
            return;
        }
        residueWireFrames.Remove(residueID);
    }

    public void UpdateCapSites(ResidueID residueID, Residue residue) {
        
        if (residue == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot update cap site in LineDrawer - Residue is null!"
            );
            return;
        }

        ResidueWireFrame residueWireFrame;
        if (residueWireFrames.TryGetValue(residueID, out residueWireFrame)) {
            float3 offset = residueWireFrame.offset;
            residueWireFrames[residueID] = new ResidueWireFrame(this.transform, residue, offset);
        } else {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot update cap site in LineDrawer - ResidueWireFrame for Residue ID '{0}' is not present!"
            );
        }
    }

    public void RemoveLinker((AtomID, AtomID) key) {
        
        if (!linkerWireFrames.ContainsKey(key)) {
            return;
        }

        linkerWireFrames.Remove(key);
    }

    public void UpdatePosition(AtomID atomID, float3 newPosition) {
        (ResidueID residueID, PDBID pdbID) = atomID;
        ResidueWireFrame residueWireFrame;
        if (residueWireFrames.TryGetValue(residueID, out residueWireFrame)) {
            residueWireFrame.UpdatePosition(pdbID, newPosition);
        }
        foreach (LinkerWireFrame linkerWireFrame in linkerWireFrames.Values) {
            if (linkerWireFrame.UpdatePosition(atomID, newPosition)) {
                return;
            }
        }
    }
    
    public void UpdateVibrationVector(AtomID atomID, float3 newVector) {
        (ResidueID residueID, PDBID pdbID) = atomID;
        ResidueWireFrame residueWireFrame;
        if (residueWireFrames.TryGetValue(residueID, out residueWireFrame)) {
            residueWireFrame.UpdateVibrationVector(pdbID, newVector);
        }
        foreach (LinkerWireFrame linkerWireFrame in linkerWireFrames.Values) {
            if (linkerWireFrame.UpdateVibrationVector(atomID, newVector)) {
                return;
            }
        }
    }

    public void SetColours(AtomColour atomColour) {
        foreach (ResidueWireFrame residueWireFrame in residueWireFrames.Values) {
            residueWireFrame.colourType = atomColour;
        }
        foreach (LinkerWireFrame linkerWireFrame in linkerWireFrames.Values) {
            linkerWireFrame.colourType = atomColour;
        }
    }

    public void SetResidueColour(AtomColour atomColour, ResidueID residueID) {
        ResidueWireFrame residueWireFrame;
        if (residueWireFrames.TryGetValue(residueID, out residueWireFrame)) {
            residueWireFrame.colourType = atomColour;
        }
    }

    public void Clear() {
        residueWireFrames.Clear();
        linkerWireFrames.Clear();
        lines.Clear();
    }

    /// <summary>Draw all bonds and un-bonded atoms using GL.QUADS</summary>
	public void DrawGLConnections() {

        GL.PushMatrix();
        //GL.LoadPixelMatrix();
        //Initialise the material
        lineMaterial.SetPass(0);


        //Get Camera position for this frame
        cameraPosition = Camera.main.transform.position;

        //Get the relative Screen Width for a World Position
        frustumRatio = math.tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);

        //Get the distance from the observer to the screen
        focalLength = Camera.main.focalLength;

        //Get the transformation matrix to transform a Local Position to a World Position
        localToWorldMatrix = transform.localToWorldMatrix;


        //Tell GL to use QUADS (4 vertices at a time)
		GL.Begin(GL.QUADS);

        //Draw Residues
        residueWireFrames
            .Values
            .ForEach(rwf => rwf.DrawBondQuads());
        
        //Draw Linkers
        linkerWireFrames
            .Values
            .ForEach(lwf => lwf.DrawBondQuads());

        GL.End();


        GL.Begin(GL.LINES);
        foreach (Line line in lines) {
            DrawLine(line, localToWorldMatrix);
        }		
		GL.End();
        GL.Begin(GL.LINE_STRIP);

        foreach (Arc arc in arcs) {
            DrawArc(arc, localToWorldMatrix);
        }

        GL.End();
        GL.PopMatrix();
	}

    void OnPostRender() {
		DrawGLConnections();
    }

    void DrawLine(Line line, float4x4 localToWorldMatrix) {
        GL.Color(line.color);
        GL.Vertex(math.transform(
            localToWorldMatrix, 
            line.start
        ));
        GL.Vertex(math.transform(
            localToWorldMatrix, 
            line.end
        ));

    }

    void DrawArc(Arc arc, float4x4 localToWorldMatrix) {
        GL.Color(arc.color);

        float sin;
        float cos;
        for (float theta=arc.thetaI; theta <= arc.thetaF; theta += arc.dtheta) {
            math.sincos(theta, out sin, out cos);
            GL.Vertex(
                math.transform(
                    localToWorldMatrix, 
                    arc.centre + arc.radius * (cos * arc.axis0 + sin * arc.axis1)
                )
            );
        }
    }
}

class ResidueWireFrame {
    Transform parentTransform;
    float3[] positions;
    Dictionary<LineDrawer.AtomColour, Color[]> atomColours;
    float[] radii;
    float[] widths;

    int numBonds;
    float3[] bondVertices;
    float3[] bondVibrationVectors;
    Dictionary<LineDrawer.AtomColour, Color[]> bondColours;
    float[] bondWidths;
    BT[] bondTypes;
    int[] bondPairs;

    public LineDrawer.AtomColour colourType;

    int numNonBondedAtoms;
    float3[] nonBondedVertices;
    float3[] nonBondVibrationVectors;
    Dictionary<LineDrawer.AtomColour, Color[]> nonBondedColours;
    float[] nonBondedWidths;
    int[] nonBondedAtoms;
    PDBID[] pdbIDs;

    public float3 offset;

    public ResidueWireFrame(Transform parentTransform, Residue residue, float3 offset) {
        this.parentTransform = parentTransform;
        pdbIDs = residue.pdbIDs.ToArray();
        int numAtoms = pdbIDs.Length;

        positions = new float3[numAtoms];

        this.offset = offset;

        atomColours = Enumerable.Range(0, LineDrawer.numAtomColourTypes)
            .ToDictionary(
                x => (LineDrawer.AtomColour)x, 
                x => new Color[numAtoms]
            );
            
        radii = new float[numAtoms];
        widths = new float[numAtoms];

        colourType = LineDrawer.AtomColour.ELEMENT;

        int positionIndex = 0;
        numBonds = 0;
        
        List<int> bondPairs = new List<int>();
        List<int> nonBondedAtomList = new List<int>();
        List<BT> bondTypeList = new List<BT>();

        bool standard = residue.state == RS.STANDARD;
        Color standardColour = standard ? LineDrawer.glowGreenColour : LineDrawer.disabledColour;

        List<int> capSites = residue.EnumerateCapSites()
            .Select(x => System.Array.IndexOf(pdbIDs, x))
            .ToList();
        bool canCap = capSites.Count > 0;



        for (int atomNum=0; atomNum < numAtoms; atomNum++) {
            PDBID pdbID = pdbIDs[atomNum];
            Atom atom = residue.GetAtom(pdbID);

            positions[positionIndex++] = atom.position + offset;

            atomColours[LineDrawer.AtomColour.ELEMENT][atomNum] = Settings.GetAtomColourFromElement(pdbID.element);
            atomColours[LineDrawer.AtomColour.CHARGE][atomNum] = Settings.GetAtomColourFromCharge(atom.partialCharge);
            atomColours[LineDrawer.AtomColour.HAS_AMBER][atomNum] = Settings.GetAtomColourFromAMBER(atom.amber);
            atomColours[LineDrawer.AtomColour.PARAMETERS][atomNum] = Settings.GetAtomColourFromPenalty(atom.penalty);
            atomColours[LineDrawer.AtomColour.SASA][atomNum] = Settings.GetAtomColourFromSASA(atom.sasa / LineDrawer.maxSASA);
            if (canCap) {
                atomColours[LineDrawer.AtomColour.CAP][atomNum] = capSites.Contains(atomNum) 
                    ? LineDrawer.glowGreenColour
                    : LineDrawer.glowBlueColour;
            } else {
                atomColours[LineDrawer.AtomColour.CAP][atomNum] = LineDrawer.disabledColour;
            }
            atomColours[LineDrawer.AtomColour.MUTATE][atomNum] = standardColour;
            atomColours[LineDrawer.AtomColour.REMOVE][atomNum] = LineDrawer.glowRedColour;

            radii[atomNum] = Settings.GetAtomRadiusFromElement(pdbID.element);
            widths[atomNum] = Settings.layerLineThicknesses[atom.oniomLayer] * parentTransform.lossyScale.x;
            

            int numConnections = atom.internalConnections.Count + atom.externalConnections.Count;
            if (numConnections == 0) {
                nonBondedAtomList.Add(atomNum);
                numNonBondedAtoms++;
            } else {
                foreach (KeyValuePair<PDBID, BT> connection in atom.internalConnections) {
                    if (! (pdbID > connection.Key)) {
                        continue;
                    }
                    
                    int bondTo = System.Array.IndexOf (pdbIDs, connection.Key);
                    if (bondTo == -1) {
                        continue;
                    }

                    bondPairs.Add(atomNum);
                    bondPairs.Add(bondTo);
                    bondTypeList.Add(connection.Value);
                    numBonds++;
                    
                }
            }
        }

        this.bondPairs = bondPairs.ToArray();
        this.nonBondedAtoms = nonBondedAtomList.ToArray();

        bondVertices = new float3[numBonds * 2];
        bondVibrationVectors = new float3[numBonds * 2];
        
        bondColours = Enumerable.Range(0, LineDrawer.numAtomColourTypes)
            .ToDictionary(
                x => (LineDrawer.AtomColour)x, 
                x => new Color[numBonds * 2]
            );
        bondWidths = new float[numBonds * 2];

        int bondAtomIndex = 0;
        int bondVertexIndex = 0;
        for (int bondNum=0; bondNum<numBonds; bondNum++) {
            AddBondGeometry(ref bondAtomIndex, ref bondVertexIndex, bondNum);
        }
        bondTypes = bondTypeList.ToArray();

        nonBondedVertices = new float3[numNonBondedAtoms * 6];
        nonBondVibrationVectors = new float3[numNonBondedAtoms];
        nonBondedColours = Enumerable.Range(0, LineDrawer.numAtomColourTypes)
            .ToDictionary(
                x => (LineDrawer.AtomColour)x, 
                x => new Color[numNonBondedAtoms]
            );
        nonBondedWidths = new float[numNonBondedAtoms];

        int nonBondedVertexIndex = 0;
        for (int nonBondedAtomNum=0; nonBondedAtomNum<numNonBondedAtoms; nonBondedAtomNum++) {
            AddNonbondedWireframeGeometry(ref nonBondedVertexIndex, nonBondedAtomNum);
        }
    }

    public void UpdateCapSites(Residue residue) {
        
        List<int> capSites = residue.EnumerateCapSites()
            .Select(x => System.Array.IndexOf(pdbIDs, x))
            .ToList();
        bool canCap = capSites.Count > 0;

        Debug.LogFormat("canCap {0}", canCap);

        int numAtoms = pdbIDs.Length;

        for (int atomNum=0; atomNum < numAtoms; atomNum++) {
            if (canCap) {
                atomColours[LineDrawer.AtomColour.CAP][atomNum] = capSites.Contains(atomNum) 
                    ? LineDrawer.glowGreenColour
                    : LineDrawer.glowBlueColour;
            } else {
                atomColours[LineDrawer.AtomColour.CAP][atomNum] = LineDrawer.disabledColour;
            }
        }
    }

    public void UpdatePosition(PDBID pdbID, float3 position) {
        int index = System.Array.IndexOf (pdbIDs, pdbID);
        positions[index] = position;

        for (int nonBondedAtomNum=0; nonBondedAtomNum<numNonBondedAtoms; nonBondedAtomNum++) {
            int nonBondedAtomIndex = nonBondedAtoms[nonBondedAtomNum];
            if (nonBondedAtomIndex == index) {

                int nonBondedVertexIndex = 6 * nonBondedAtomNum;
                float radius = radii[nonBondedAtomIndex] * 0.5f * Settings.atomicRadiusToSphereRatio;

                SetNonBondedCross(position, radius, ref nonBondedVertexIndex);
                return; //Can only be one of these per geometry so no need to look at bonds
            }
        }

        for (int bondAtomIndex=0; bondAtomIndex<numBonds*2; bondAtomIndex++) {
            int bondVertexIndex = bondPairs[bondAtomIndex];
            if (bondVertexIndex == index) {
                bondVertices[bondAtomIndex] = position;
            } 
        }
    }

    public void UpdateVibrationVector(PDBID pdbID, float3 vector) {
        int index = System.Array.IndexOf (pdbIDs, pdbID);

        for (int nonBondedAtomNum=0; nonBondedAtomNum<numNonBondedAtoms; nonBondedAtomNum++) {
            int nonBondedAtomIndex = nonBondedAtoms[nonBondedAtomNum];
            if (nonBondedAtomIndex == index) {

                nonBondVibrationVectors[nonBondedAtomNum] = vector;

                return; //Can only be one of these per geometry so no need to look at bonds
            }
        }

        for (int bondAtomIndex=0; bondAtomIndex<numBonds*2; bondAtomIndex++) {
            int bondVertexIndex = bondPairs[bondAtomIndex];
            if (bondVertexIndex == index) {
                bondVibrationVectors[bondAtomIndex] = vector;
            } 
        }
    }

    void AddBondGeometry(
        ref int bondAtomIndex,
        ref int bondVertexIndex,
        int bondNum
    ) {
        
        int i0 = bondPairs[bondAtomIndex];

        float width0 = widths[i0];

        bondColours[LineDrawer.AtomColour.ELEMENT][bondAtomIndex] = atomColours[LineDrawer.AtomColour.ELEMENT][i0];
        bondColours[LineDrawer.AtomColour.CHARGE][bondAtomIndex] = atomColours[LineDrawer.AtomColour.CHARGE][i0];
        bondColours[LineDrawer.AtomColour.HAS_AMBER][bondAtomIndex] = atomColours[LineDrawer.AtomColour.HAS_AMBER][i0];
        bondColours[LineDrawer.AtomColour.PARAMETERS][bondAtomIndex] = atomColours[LineDrawer.AtomColour.PARAMETERS][i0];
        bondColours[LineDrawer.AtomColour.SASA][bondAtomIndex] = atomColours[LineDrawer.AtomColour.SASA][i0];
        bondColours[LineDrawer.AtomColour.CAP][bondAtomIndex] = atomColours[LineDrawer.AtomColour.CAP][i0];
        bondColours[LineDrawer.AtomColour.MUTATE][bondAtomIndex] = atomColours[LineDrawer.AtomColour.MUTATE][i0];
        bondColours[LineDrawer.AtomColour.REMOVE][bondAtomIndex] = atomColours[LineDrawer.AtomColour.REMOVE][i0];

        bondAtomIndex++;
        bondWidths[bondVertexIndex] = width0;
        bondVertices[bondVertexIndex++] = positions[i0++];
        
        int i1 = bondPairs[bondAtomIndex];

        float width1 = widths[i1];

        bondColours[LineDrawer.AtomColour.ELEMENT][bondAtomIndex] = atomColours[LineDrawer.AtomColour.ELEMENT][i1];
        bondColours[LineDrawer.AtomColour.CHARGE][bondAtomIndex] = atomColours[LineDrawer.AtomColour.CHARGE][i1];
        bondColours[LineDrawer.AtomColour.HAS_AMBER][bondAtomIndex] = atomColours[LineDrawer.AtomColour.HAS_AMBER][i1];
        bondColours[LineDrawer.AtomColour.PARAMETERS][bondAtomIndex] = atomColours[LineDrawer.AtomColour.PARAMETERS][i1];
        bondColours[LineDrawer.AtomColour.SASA][bondAtomIndex] = atomColours[LineDrawer.AtomColour.SASA][i1];
        bondColours[LineDrawer.AtomColour.CAP][bondAtomIndex] = atomColours[LineDrawer.AtomColour.CAP][i1];
        bondColours[LineDrawer.AtomColour.MUTATE][bondAtomIndex] = atomColours[LineDrawer.AtomColour.MUTATE][i1];
        bondColours[LineDrawer.AtomColour.REMOVE][bondAtomIndex] = atomColours[LineDrawer.AtomColour.REMOVE][i1];
        
        bondAtomIndex++;
        bondWidths[bondVertexIndex] = width1;
        bondVertices[bondVertexIndex++] = positions[i1++];
    }

    void AddNonbondedWireframeGeometry(
        ref int nonBondedVertexIndex,
        int nonBondedAtomNum
    ) {

        int nonBondedAtomIndex = nonBondedAtoms[nonBondedAtomNum];

        nonBondedColours[LineDrawer.AtomColour.ELEMENT][nonBondedAtomNum] = atomColours[LineDrawer.AtomColour.ELEMENT][nonBondedAtomIndex];
        nonBondedColours[LineDrawer.AtomColour.CHARGE][nonBondedAtomNum] = atomColours[LineDrawer.AtomColour.CHARGE][nonBondedAtomIndex];
        nonBondedColours[LineDrawer.AtomColour.HAS_AMBER][nonBondedAtomNum] = atomColours[LineDrawer.AtomColour.HAS_AMBER][nonBondedAtomIndex];
        nonBondedColours[LineDrawer.AtomColour.PARAMETERS][nonBondedAtomNum] = atomColours[LineDrawer.AtomColour.PARAMETERS][nonBondedAtomIndex];
        nonBondedColours[LineDrawer.AtomColour.SASA][nonBondedAtomNum] = atomColours[LineDrawer.AtomColour.SASA][nonBondedAtomIndex];
        nonBondedColours[LineDrawer.AtomColour.CAP][nonBondedAtomNum] = atomColours[LineDrawer.AtomColour.CAP][nonBondedAtomIndex];
        nonBondedColours[LineDrawer.AtomColour.MUTATE][nonBondedAtomNum] = atomColours[LineDrawer.AtomColour.MUTATE][nonBondedAtomIndex];
        nonBondedColours[LineDrawer.AtomColour.REMOVE][nonBondedAtomNum] = atomColours[LineDrawer.AtomColour.REMOVE][nonBondedAtomIndex];

        nonBondedWidths[nonBondedAtomNum] = widths[nonBondedAtomIndex];
        float radius = radii[nonBondedAtomIndex] * 0.5f * Settings.atomicRadiusToSphereRatio;


        float3 position = positions[nonBondedAtomIndex];

        SetNonBondedCross(position, radius, ref nonBondedVertexIndex);

    }

    void SetNonBondedCross(float3 position, float radius, ref int nonBondedVertexIndex) {

        nonBondedVertices[nonBondedVertexIndex] = position;
        nonBondedVertices[nonBondedVertexIndex++].x -= radius;
        nonBondedVertices[nonBondedVertexIndex] = position;
        nonBondedVertices[nonBondedVertexIndex++].x += radius;

        //Draw bottom to top
        nonBondedVertices[nonBondedVertexIndex] = position;
        nonBondedVertices[nonBondedVertexIndex++].y -= radius;
        nonBondedVertices[nonBondedVertexIndex] = position;
        nonBondedVertices[nonBondedVertexIndex++].y += radius;

        //Draw back to front
        nonBondedVertices[nonBondedVertexIndex] = position;
        nonBondedVertices[nonBondedVertexIndex++].z -= radius;
        nonBondedVertices[nonBondedVertexIndex] = position;
        nonBondedVertices[nonBondedVertexIndex++].z += radius;

    }

    public void DrawBondQuads() {

        float distance;

        float3 start = new float3();
        float3 mid = new float3();
        float3 end = new float3();

        float3 cameraVec = new float3();
        float3 norm;

        float3 normStart;
        float3 normMid;
        float3 normEnd;

        Color[] bondColorArray = bondColours[colourType];
        Color[] nonbondedColorArray = nonBondedColours[colourType];

        int bondVertexIndex = 0;

        Color startColour;
        Color endColour;
        for (int bondNum=0; bondNum<numBonds; bondNum++) {

            float startWidth = bondWidths[bondVertexIndex];
            startColour = bondColorArray[bondVertexIndex];
            start = math.transform(LineDrawer.localToWorldMatrix, bondVertices[bondVertexIndex] + LineDrawer.t * bondVibrationVectors[bondVertexIndex]); 
            bondVertexIndex++;

            float endWidth = bondWidths[bondVertexIndex];
            endColour = bondColorArray[bondVertexIndex];
            end = math.transform(LineDrawer.localToWorldMatrix, bondVertices[bondVertexIndex] + LineDrawer.t * bondVibrationVectors[bondVertexIndex]);
            bondVertexIndex++;

            mid = (start + end) * 0.5f;

            cameraVec = mid - LineDrawer.cameraPosition;

            float screenScale = 1f / (LineDrawer.frustumRatio * (math.length(cameraVec) + LineDrawer.focalLength));
            
            norm = math.normalizesafe(math.cross(end - start, cameraVec)) * screenScale;

            normStart = norm * startWidth;
            normMid = norm * (endWidth + startWidth) * 0.5f;
            normEnd = norm * endWidth;

            GL.Color(startColour);
            GL.Vertex(start + normStart);
            GL.Vertex(start - normStart);
            GL.Vertex(mid - normMid);
            GL.Vertex(mid + normMid);

            GL.Color(endColour);
            GL.Vertex(mid + normMid);
            GL.Vertex(mid - normMid);
            GL.Vertex(end - normEnd);
            GL.Vertex(end + normEnd);
            
        }

        int nonBondVertexIndex = 0;
        for (int nonBondNum=0; nonBondNum<numNonBondedAtoms; nonBondNum++) {
            
            float width = 0;
            for (int coord=0; coord<3; coord++) {
                start = nonBondedVertices[nonBondVertexIndex++] + LineDrawer.t * nonBondVibrationVectors[nonBondNum];
                end = nonBondedVertices[nonBondVertexIndex++] + LineDrawer.t * nonBondVibrationVectors[nonBondNum];

                start = math.transform(LineDrawer.localToWorldMatrix, start);
                end = math.transform(LineDrawer.localToWorldMatrix, end);

                //Only compute mid and width once for each atom
                if (coord == 0) {

                    mid = (start + end) * 0.5f;
                    
                    cameraVec = mid - LineDrawer.cameraPosition;

                    distance = math.length(cameraVec) + LineDrawer.focalLength;

                    width = nonBondedWidths[nonBondNum] / (distance * LineDrawer.frustumRatio);

                }
            
                norm = math.normalizesafe(math.cross(start, end)) * width;
                
                GL.Color(nonbondedColorArray[nonBondNum]);
                GL.Vertex(start + norm);
                GL.Vertex(start - norm);
                GL.Vertex(mid - norm);
                GL.Vertex(mid + norm);

                GL.Color(nonbondedColorArray[nonBondNum]);
                GL.Vertex(mid + norm);
                GL.Vertex(mid - norm);
                GL.Vertex(end - norm);
                GL.Vertex(end + norm);
            }
        }
	}
}


class LinkerWireFrame {
    
    public LineDrawer.AtomColour colourType;
    Dictionary<LineDrawer.AtomColour, Color> startColours;
    Dictionary<LineDrawer.AtomColour, Color> endColours;
    float3 linkerStart;
    float3 linkerEnd;
    float3 startVibrationVector;
    float3 endVibrationVector;
    AtomID linkerStartID;
    AtomID linkerEndID;
    float bondWidth;


    public LinkerWireFrame(Transform parentTransform, Atom atom0, Atom atom1, AtomID atomID0, AtomID atomID1, float3 offset) {

        linkerStart = atom0.position + offset;
        linkerEnd = atom1.position + offset;

        startVibrationVector = 0f;
        endVibrationVector = 0f;

        this.linkerStartID = atomID0;
        this.linkerEndID = atomID1;

        startColours = new Dictionary<LineDrawer.AtomColour, Color>();
        endColours = new Dictionary<LineDrawer.AtomColour, Color>();
        
        startColours[LineDrawer.AtomColour.ELEMENT] = Settings.GetAtomColourFromElement(atomID0.pdbID.element); 
        endColours[LineDrawer.AtomColour.ELEMENT] = Settings.GetAtomColourFromElement(atomID1.pdbID.element);

        startColours[LineDrawer.AtomColour.CHARGE] = Settings.GetAtomColourFromCharge(atom0.partialCharge);
        endColours[LineDrawer.AtomColour.CHARGE] = Settings.GetAtomColourFromCharge(atom1.partialCharge);

        startColours[LineDrawer.AtomColour.HAS_AMBER] = Settings.GetAtomColourFromAMBER(atom0.amber);
        endColours[LineDrawer.AtomColour.HAS_AMBER] = Settings.GetAtomColourFromAMBER(atom1.amber);
        
        startColours[LineDrawer.AtomColour.PARAMETERS] = Settings.GetAtomColourFromPenalty(atom0.penalty);
        endColours[LineDrawer.AtomColour.PARAMETERS] = Settings.GetAtomColourFromPenalty(atom1.penalty);
        
        startColours[LineDrawer.AtomColour.SASA] = Settings.GetAtomColourFromSASA(atom0.sasa / LineDrawer.maxSASA);
        endColours[LineDrawer.AtomColour.SASA] = Settings.GetAtomColourFromSASA(atom1.sasa / LineDrawer.maxSASA);
        
        startColours[LineDrawer.AtomColour.CAP] = LineDrawer.disabledColour;
        endColours[LineDrawer.AtomColour.CAP] = LineDrawer.disabledColour;
        
        startColours[LineDrawer.AtomColour.MUTATE] = LineDrawer.disabledColour;
        endColours[LineDrawer.AtomColour.MUTATE] = LineDrawer.disabledColour;
        
        startColours[LineDrawer.AtomColour.REMOVE] = LineDrawer.disabledColour;
        endColours[LineDrawer.AtomColour.REMOVE] = LineDrawer.disabledColour;

        bondWidth = math.min(
            Settings.layerLineThicknesses[atom0.oniomLayer],
            Settings.layerLineThicknesses[atom1.oniomLayer]
        ) * parentTransform.lossyScale.x; 

        colourType = LineDrawer.AtomColour.ELEMENT;
    }
    
    public void DrawBondQuads() {

        float3 start = math.transform(LineDrawer.localToWorldMatrix, linkerStart);
        float3 end = math.transform(LineDrawer.localToWorldMatrix, linkerEnd);
        float3 mid = (start + end) * 0.5f;

        float distance = math.distance(LineDrawer.cameraPosition, mid) + LineDrawer.focalLength;

        float width = bondWidth / (distance * LineDrawer.frustumRatio);

        float3 norm = math.normalizesafe(math.cross(start, end)) * width;
        
        GL.Color(startColours[colourType]);
        GL.Vertex(start + norm);
        GL.Vertex(start - norm);
        GL.Vertex(mid - norm);
        GL.Vertex(mid + norm);

        GL.Color(endColours[colourType]);
        GL.Vertex(mid + norm);
        GL.Vertex(mid - norm);
        GL.Vertex(end - norm);
        GL.Vertex(end + norm);
        
	}

    
    public bool UpdatePosition(AtomID atomID, float3 position) {
        if (atomID == this.linkerStartID) {
            linkerStart = position;
            return true;
        } else if (atomID == this.linkerEndID) {
            linkerEnd = position;
            return true;
        }
        return false;
    }

    public bool UpdateVibrationVector(AtomID atomID, float3 vector) {
        if (atomID == this.linkerStartID) {
            startVibrationVector = vector;
            return true;
        } else if (atomID == this.linkerEndID) {
            endVibrationVector = vector;
            return true;
        }
        return false;

    }
    
}

public struct Line {
    public float3 start;
    public float3 end;
    public Color color;

    public Line(float3 start, float3 end, Color color) {
        this.start = start;
        this.end = end;
        this.color = color;
    }
}

public struct Arc {
    public float thetaI;
    public float thetaF;
    public float dtheta;
    public float radius;

    public float3 axis0;
    public float3 axis1;
    public float3 centre;

    public Color color;
    public Arc(float thetaI,
        float thetaF,
        float dtheta,
        float radius,
        float3 axis0,
        float3 axis1,
        float3 centre,
        Color color
    ) {
        this.thetaI = thetaI;
        this.thetaF = thetaF;
        this.dtheta = dtheta;
        this.radius = radius;
        this.axis0 = axis0;
        this.axis1 = axis1;
        this.centre = centre;
        this.color = color;
    }
}
