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

    public enum AtomColour: int {ELEMENT, CHARGE, HAS_AMBER, PARAMETERS, CAP, MUTATE, REMOVE}
    public static int numAtomColourTypes = 7; //Must be the length of the above enum

    void Start() {
        activeCamera = Camera.main;
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
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot remove Linker '{0}'-'{1}' from LineDrawer - key not present in LinkerWireFrames!",
                key.Item1,
                key.Item2
            );
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
        foreach (LinkerWireFrame linkerWireFrame in linkerWireFrames.Where(x => x.Key.Item1 == atomID || x.Key.Item2 == atomID).Select(x => x.Value)) {
            linkerWireFrame.UpdatePosition(atomID, newPosition);
            return;
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
        //Tell GL to use QUADS (4 vertices at a time)
		GL.Begin(GL.QUADS);


        //Get Camera position for this frame
        float3 cameraPosition = Camera.main.transform.position;

        //Get the relative Screen Width for a World Position
        float frustumRatio = math.tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);

        //Get the distance from the observer to the screen
        float focalLength = Camera.main.focalLength;

        //Get the transformation matrix to transform a Local Position to a World Position
        float4x4 localToWorldMatrix = transform.localToWorldMatrix;


        //This gets the colors and World Positions of each bond
        IEnumerable<(Color, float3, float3, float3, float3)> bondEnumerator;
        if (Settings.useParallelLineDrawer) {
            bondEnumerator = residueWireFrames
            //Get all the Residue Wire Frames
            .Values
            //Parallelise the selection
            .AsParallel()
            //SelectMany will join the selection 
            .SelectMany(
                //Calculate the World Positions of the Quads representing bonds etc
                x => x.EnumerateBondQuads(
                    cameraPosition, 
                    frustumRatio, 
                    focalLength,
                    localToWorldMatrix
                )
            )
            //Join parallel threads
            .AsSequential();
        } else {
            bondEnumerator = residueWireFrames
            //Get all the Residue Wire Frames
            .Values
            //SelectMany will join the selection 
            .SelectMany(
                //Calculate the World Positions of the Quads representing bonds etc
                x => x.EnumerateBondQuads(
                    cameraPosition, 
                    frustumRatio, 
                    focalLength,
                    localToWorldMatrix
                )
            );
        }

        
        IEnumerable<(Color, float3, float3, float3, float3)> linkerEnumerator;
        if (Settings.useParallelLineDrawer) {
            linkerEnumerator = linkerWireFrames
            //Parallelise the selection
            .AsParallel()
            //SelectMany will join the selection 
            .SelectMany(
                //Calculate the World Positions of the Quads representing bonds etc
                x => x.Value.EnumerateBondQuads(
                    cameraPosition, 
                    frustumRatio, 
                    focalLength,
                    localToWorldMatrix
                )
            )
            //Join parallel threads
            .AsSequential();
        } else {
            linkerEnumerator = linkerWireFrames
            //SelectMany will join the selection 
            .SelectMany(
                //Calculate the World Positions of the Quads representing bonds etc
                x => x.Value.EnumerateBondQuads(
                    cameraPosition, 
                    frustumRatio, 
                    focalLength,
                    localToWorldMatrix
                )
            );
        }

        //Draw each quad with GL
        foreach ((Color, float3, float3, float3, float3) bondQuad in bondEnumerator) {
            DrawBond(bondQuad);
        }

        foreach ((Color, float3, float3, float3, float3) bondQuad in linkerEnumerator) {
            DrawBond(bondQuad);
        }

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
	//void OnDrawGizmos() {
	//	DrawGLConnections();
	//}

    void DrawBond((Color color, float3 v0, float3 v1, float3 v2, float3 v3) bondQuad) {
        
        GL.Color(bondQuad.color);
        GL.Vertex(bondQuad.v0);
        GL.Vertex(bondQuad.v1);
        GL.Vertex(bondQuad.v2);
        GL.Vertex(bondQuad.v3);
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
    Dictionary<LineDrawer.AtomColour, Color[]> bondColours;
    float[] bondWidths;
    BT[] bondTypes;
    int[] bondPairs;

    public LineDrawer.AtomColour colourType;

    int numNonBondedAtoms;
    float3[] nonBondedVertices;
    Dictionary<LineDrawer.AtomColour, Color[]> nonBondedColours;
    float[] nonBondedWidths;
    int[] nonBondedAtoms;
    PDBID[] pdbIDs;

    public float3 offset;

    public ResidueWireFrame(Transform parentTransform, Residue residue, float3 offset) {
        this.parentTransform = parentTransform;
        pdbIDs = residue.atoms.Keys.ToArray();
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
            Atom atom = residue.atoms[pdbID];

            positions[positionIndex++] = atom.position + offset;

            atomColours[LineDrawer.AtomColour.ELEMENT][atomNum] = Settings.GetAtomColourFromElement(pdbID.element);
            atomColours[LineDrawer.AtomColour.CHARGE][atomNum] = Settings.GetAtomColourFromCharge(atom.partialCharge);
            atomColours[LineDrawer.AtomColour.HAS_AMBER][atomNum] = Settings.GetAtomColourFromAMBER(atom.amber);
            atomColours[LineDrawer.AtomColour.PARAMETERS][atomNum] = Settings.GetAtomColourFromPenalty(atom.penalty);
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
        
        bondColours = Enumerable.Range(0, LineDrawer.numAtomColourTypes)
            .ToDictionary(
                x => (LineDrawer.AtomColour)x, 
                x => new Color[numBonds * 2]
            );
        bondWidths = new float[numBonds];

        int bondAtomIndex = 0;
        int bondVertexIndex = 0;
        for (int bondNum=0; bondNum<numBonds; bondNum++) {
            AddBondGeometry(ref bondAtomIndex, ref bondVertexIndex, bondNum);
        }
        bondTypes = bondTypeList.ToArray();

        nonBondedVertices = new float3[numNonBondedAtoms * 6];
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
        bondColours[LineDrawer.AtomColour.CAP][bondAtomIndex] = atomColours[LineDrawer.AtomColour.CAP][i0];
        bondColours[LineDrawer.AtomColour.MUTATE][bondAtomIndex] = atomColours[LineDrawer.AtomColour.MUTATE][i0];
        bondColours[LineDrawer.AtomColour.REMOVE][bondAtomIndex] = atomColours[LineDrawer.AtomColour.REMOVE][i0];

        bondAtomIndex++;

        bondVertices[bondVertexIndex++] = positions[i0++];
        
        int i1 = bondPairs[bondAtomIndex];

        float width1 = widths[i1];

        bondColours[LineDrawer.AtomColour.ELEMENT][bondAtomIndex] = atomColours[LineDrawer.AtomColour.ELEMENT][i1];
        bondColours[LineDrawer.AtomColour.CHARGE][bondAtomIndex] = atomColours[LineDrawer.AtomColour.CHARGE][i1];
        bondColours[LineDrawer.AtomColour.HAS_AMBER][bondAtomIndex] = atomColours[LineDrawer.AtomColour.HAS_AMBER][i1];
        bondColours[LineDrawer.AtomColour.PARAMETERS][bondAtomIndex] = atomColours[LineDrawer.AtomColour.PARAMETERS][i1];
        bondColours[LineDrawer.AtomColour.CAP][bondAtomIndex] = atomColours[LineDrawer.AtomColour.CAP][i1];
        bondColours[LineDrawer.AtomColour.MUTATE][bondAtomIndex] = atomColours[LineDrawer.AtomColour.MUTATE][i1];
        bondColours[LineDrawer.AtomColour.REMOVE][bondAtomIndex] = atomColours[LineDrawer.AtomColour.REMOVE][i1];
        
        bondAtomIndex++;
        bondVertices[bondVertexIndex++] = positions[i1++];

        bondWidths[bondNum] = Mathf.Min(width0, width1);
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

    public IEnumerable<(Color, float3, float3, float3, float3)> EnumerateBondQuads(
        float3 cameraPosition, 
        float frustumRatio, 
        float focalLength,
        float4x4 localToWorldMatrix
    ) {

        float distance;

        float3 start = new float3();
        float3 mid = new float3();
        float3 end = new float3();

        Color[] bondColorArray = bondColours[colourType];
        Color[] nonbondedColorArray = nonBondedColours[colourType];

        int bondVertexIndex = 0;
        int colourIndex = 0;
        for (int bondNum=0; bondNum<numBonds; bondNum++) {

            start = math.transform(localToWorldMatrix, bondVertices[bondVertexIndex++]);            
            end = math.transform(localToWorldMatrix, bondVertices[bondVertexIndex++]);
            mid = (start + end) * 0.5f;

            distance = math.distance(cameraPosition, mid) + focalLength;

            float width = bondWidths[bondNum] / (distance * frustumRatio);
            
            float3 norm = math.normalizesafe(math.cross(start, end)) * width;

            yield return (
                bondColorArray[colourIndex++], 
                start + norm,
                start - norm,
                mid - norm,
                mid + norm
            );

            yield return (
                bondColorArray[colourIndex++], 
                mid + norm,
                mid - norm,
                end - norm,
                end + norm
            );
        }

        int nonBondVertexIndex = 0;
        for (int nonBondNum=0; nonBondNum<numNonBondedAtoms; nonBondNum++) {
            
            float width = 0;
            for (int coord=0; coord<3; coord++) {
                start = nonBondedVertices[nonBondVertexIndex++];
                end = nonBondedVertices[nonBondVertexIndex++];

                start = math.transform(localToWorldMatrix, start);
                end = math.transform(localToWorldMatrix, end);

                //Only compute mid and width once for each atom
                if (coord == 0) {

                    mid = (start + end) * 0.5f;

                    distance = math.distance(cameraPosition, mid) + focalLength;
                    width = nonBondedWidths[nonBondNum] / (distance * frustumRatio);
                }
            
                float3 norm = math.normalizesafe(math.cross(start, end)) * width;
                
                yield return (
                    nonbondedColorArray[nonBondNum], 
                    start + norm,
                    start - norm,
                    mid - norm,
                    mid + norm
                );

                yield return (
                    nonbondedColorArray[nonBondNum], 
                    mid + norm,
                    mid - norm,
                    end - norm,
                    end + norm
                );
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
    AtomID linkerStartID;
    AtomID linkerEndID;
    float bondWidth;


    public LinkerWireFrame(Transform parentTransform, Atom atom0, Atom atom1, AtomID atomID0, AtomID atomID1, float3 offset) {

        linkerStart = atom0.position + offset;
        linkerEnd = atom1.position + offset;

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
    
    public IEnumerable<(Color, float3, float3, float3, float3)> EnumerateBondQuads(
        float3 cameraPosition, 
        float frustumRatio, 
        float focalLength,
        float4x4 localToWorldMatrix
    ) {

        float3 start = math.transform(localToWorldMatrix, linkerStart);
        float3 end = math.transform(localToWorldMatrix, linkerEnd);
        float3 mid = (start + end) * 0.5f;

        float distance = math.distance(cameraPosition, mid) + focalLength;

        float width = bondWidth / (distance * frustumRatio);

        float3 norm = math.normalizesafe(math.cross(start, end)) * width;
        
        yield return (
            startColours[colourType], 
            start + norm,
            start - norm,
            mid - norm,
            mid + norm
        );

        yield return (
            endColours[colourType], 
            mid + norm,
            mid - norm,
            end - norm,
            end + norm
        );
        
	}

    
    public void UpdatePosition(AtomID atomID, float3 position) {
        if (atomID == this.linkerStartID) {
            linkerStart = position;
        } else if (atomID == this.linkerEndID) {
            linkerEnd = position;
        }
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
