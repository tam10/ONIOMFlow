using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;
using System.Linq;

///<summary>Icosphere Generator Singleton Class</summary>
/// <remarks>
/// Assigns sphere geometries to Mesh objects.
/// </remarks>

public class Sphere : MonoBehaviour {

	private static Sphere _main;
	public static Sphere main {
		get {
			if (ReferenceEquals(_main, null)) {
				GameObject go = new GameObject("Sphere");
				_main = go.AddComponent<Sphere>();
			}
			return _main;
		}
	}

	/// <summary>Reference sphere vertices</summary>
	float3[] refVertices;
	/// <summary>Reference sphere mesh triangles</summary>
	int3[] refTriangles;
	/// <summary>Number of vertices in the reference sphere</summary>
	int numVerticesPerAtom;
	/// <summary>Number of triangles in the reference sphere</summary>
	int numTrianglesPerAtom;

	/// <summary>Regenerate the reference sphere (use when changing resolution)</summary>
	public void UpdateReference() {
		int pow = CustomMathematics.IntPow(4, Settings.ballResolution);

        numVerticesPerAtom = 10 * pow + 2;
        numTrianglesPerAtom = 20 * pow;

		//Get the reference sphere
		GetSphere(out refVertices, out refTriangles, Settings.ballResolution);

	}

	///<summary>Set the vertices, normals and triangles of a sphere</summary>
	///<param name="vertices">The vertices of the new sphere.</param>
	///<param name="triangles">Vertex indices that make up triangles, as int3.</param>
	///<param name="resolution">Resolution of the sphere mesh, equal to the number of times the icosahedron's faces are refined.</param>
	public void GetSphere(
		out float3[] vertices, 
		out int3[] triangles, 
		int resolution
	) {
		if (resolution < 0) {
			throw new System.Exception("Can't create a sphere with negative resolution!");
		}

		int pow = CustomMathematics.IntPow(4, resolution);

        int numVerticesPerAtom = 10 * pow + 2;
        int numTrianglesPerAtom = 20 * pow;

		//Assign arrays
		vertices = new float3[numVerticesPerAtom];
		triangles = new int3[numTrianglesPerAtom];

		//Get the reference Icosahedron
		GetIcosahedronVertices(vertices);
		GetIcosahedronTriangles(triangles);

		//These are cached vertices - shouldn't be generating new vertices at already occupied positions
		int numCached = 0;
		//List of hashes of edges
		uint[] cachedVertKeys = new uint[numVerticesPerAtom];
		//Indices of verts correponding to the hashes
		int[] cachedVertIndices = new int[numVerticesPerAtom];
		
		//Smooth the sphere by refining each of its edges
		for (int subdivision=0; subdivision<resolution; subdivision++) {
			RefineFaces(
				vertices, 
				triangles, 
				subdivision, 
				cachedVertKeys, 
				cachedVertIndices, 
				ref numCached
			);
		}
		
	}

	///<summary>Refines every face (triangle) of a sphere</summary>
	///<param name="vertices">The vertices of the sphere - must have space for the new vertices.</param>
	///<param name="triangles">The triangles of the sphere - must have space for the new triangles.</param>
	///<param name="subdivision">The current level of subdivision.</param>
	///<param name="cachedVertKeys">Array of hashes of cached verts.</param>
	///<param name="cachedVertIndices">Vert indices of cachedVertKeys.</param>
	///<param name="numCached">Number of indices that are currently cached.</param>
	private void RefineFaces(
		float3[] vertices, 
		int3[] triangles, 
		int subdivision,
		uint[] cachedVertKeys,
		int[] cachedVertIndices,
		ref int numCached
	) {

        int newFaceNum = 20 * CustomMathematics.IntPow(4, subdivision);
		int totalNumFaces = newFaceNum;
        int currentVertexNum = newFaceNum / 2 + 2;

		//Normalisation scale to bring each new vertex out to the unit sphere
		//Every time a face is refined, the new vertices are slightly too short
		int3 firstTriangle = triangles[0];
		float normalisationFactor = GetNormalisationFactor(vertices[firstTriangle.x], vertices[firstTriangle.y]);

		//Loop through each face (triangle) and refine by splitting it into 4 new triangles
		for (int oldFaceNum=0; oldFaceNum<totalNumFaces; oldFaceNum++) {
			RefineFace(
				vertices, 
				triangles,
				oldFaceNum, 
				ref currentVertexNum, 
				ref newFaceNum, 
				cachedVertKeys,
				cachedVertIndices,
				ref numCached,
				normalisationFactor
			);
		}
	}

	///<summary>Refines a single triangle of a sphere</summary>
	///<param name="vertices">The vertices of the sphere - must have space for the new vertices.</param>
	///<param name="triangles">The triangles of the sphere - must have space for the new triangles.</param>
	///<param name="oldFaceNum">The current face index that is being refined.</param>
	///<param name="currentVertexNum">The current vertex that is being added.</param>
	///<param name="newFaceNum">The current face index that is being added.</param>
	///<param name="cachedVertKeys">Array of hashes of cached verts.</param>
	///<param name="cachedVertIndices">Vert indices of cachedVertKeys.</param>
	///<param name="numCached">Number of indices that are currently cached.</param>
	///<param name="normalisationFactor">The amount to scale each new vertex to bring it to the unit sphere.</param>
	private void RefineFace(
		float3[] vertices, 
		int3[] triangles,
		int oldFaceNum,
		ref int currentVertexNum,
		ref int newFaceNum,
		uint[] cachedVertKeys,
		int[] cachedVertIndices,
		ref int numCached,
		float normalisationFactor
	) {

		/*

		Split a triangle into four triangles to increase resolution of a mesh
		
			O
		   / \
		  I---I
		 / \ / \
		O---I---O

		O: Outer/Original triangle
		I: Inner triangle

		*/
		
		//The triangle to be refined
		int3 outerTriangle = new int3(triangles[oldFaceNum]);
		//The triangle composed of the midpoints of the outerTriangle
		int3 innerTriangle = new int3();

		//Loop through each edge
		for (int edgeIndex0=0; edgeIndex0<3; edgeIndex0++) {
			//Check if we're at the last edge - wrap around if it's the case
			int edgeIndex1 = (edgeIndex0 == 2) ? 0 : edgeIndex0 + 1;

			//Get an edge of the outer triangle
			int2 outerEdge = new int2(outerTriangle[edgeIndex0], outerTriangle[edgeIndex1]);
			//Make sure the edge is in ascending order
			if (outerEdge.y > outerEdge.x) {
				outerEdge = new int2(outerEdge.y, outerEdge.x);
			}

			//Create a hash for the edge
			uint key = math.hash(outerEdge);

			//Check to see if the hash exists and therefor the vertex
			int innerVertexIndex = System.Array.IndexOf(cachedVertKeys, key);

			//Index of -1 means it's not cached - create a new vertex
			if (innerVertexIndex == -1) {

				//Build the vertex using the midpoint of the edge and scale using normalisation
				vertices[currentVertexNum] = normalisationFactor * (
					vertices[outerEdge.x] + 
					vertices[outerEdge.y]
				);
				
				//Add hash to cache
				cachedVertKeys[numCached] = key;
			
				//Assign vertex to inner triangle
				innerTriangle[edgeIndex0] = cachedVertIndices[numCached] = currentVertexNum++;

				numCached++;
			} else {
				//Assign vertex to inner triangle
				innerTriangle[edgeIndex0] = cachedVertIndices[innerVertexIndex];
			}


		}
		
		//Build 3 new triangles from 1 outer vertex and 2 inner vertices each (see diagram at top)
		triangles[newFaceNum++] = new int3(innerTriangle.x, outerTriangle.y, innerTriangle.y);
		triangles[newFaceNum++] = new int3(innerTriangle.y, outerTriangle.z, innerTriangle.z);
		triangles[newFaceNum++] = new int3(innerTriangle.z, outerTriangle.x, innerTriangle.x);

		//Recycle outer triangle by pointing it to inner triangle
		triangles[oldFaceNum] = innerTriangle;

	}

	/// <summary>Assign vertices, normals and triangles of a mesh of atomic spheres</summary>
	/// <param name="mesh">The mesh to assign</param>
	/// <param name="centresArray">The centres of the atoms</param>
	/// <param name="radiiArray">The radii of the atoms - these are scaled by the atomicRadiusToSphereRatio and atomicRadiusToCylinderRatio settings</param>
	/// <param name="bondsArray">int2 pairs of atoms to bond together - the indices correspond to <c>centresArray</c></param>
	/// <param name="offset">A translation vector to apply to each centre</param>
	/// <param name="atomColoursArray">The colours to apply to each atom</param>
	public void SetMesh(
		Mesh mesh,
		float3[] centresArray,
		float[] radiiArray,
		Color[] atomColoursArray
	) {

		int numAtoms = centresArray.Length;
		
        int numVertices = numVerticesPerAtom * numAtoms;
        int numTriangles = numTrianglesPerAtom * numAtoms;
        int numTris = 3 * numTriangles;

		//Do some checks
		if (numAtoms != radiiArray.Length) {
			throw new System.Exception("Cannot assign Sphere Mesh: Number of atoms != number of radii");
		}
		if (numAtoms != atomColoursArray.Length) {
			throw new System.Exception("Cannot assign Sphere Mesh: Number of atoms != number of colours");
		}

		//Assign all the arrays - this bypasses Garbage Collection until job is complete and switches on SIMD
		NativeArray<float3> refVertices = new NativeArray<float3>(this.refVertices, Allocator.TempJob);
		NativeArray<int3> refTriangles = new NativeArray<int3>(this.refTriangles, Allocator.TempJob);

		NativeArray<float3> vertices = new NativeArray<float3>(numVertices, Allocator.TempJob);
		NativeArray<float3> normals = new NativeArray<float3>(numVertices, Allocator.TempJob);
		NativeArray<int> tris = new NativeArray<int>(numTris, Allocator.TempJob);
		NativeArray<Color> meshColours = new NativeArray<Color>(numVertices, Allocator.TempJob);


		NativeArray<float3> centres = new NativeArray<float3>(centresArray, Allocator.TempJob);
		NativeArray<float> radii = new NativeArray<float>(radiiArray, Allocator.TempJob);
		NativeArray<Color> atomColours = new NativeArray<Color>(atomColoursArray, Allocator.TempJob);

		//Create job
        GetAllSpheresJob job = new GetAllSpheresJob {
			
			atomicRadiusToSphereRatio = Settings.atomicRadiusToSphereRatio,
			refVertices = refVertices,
			refTriangles = refTriangles,
			numVerticesPerAtom = numVerticesPerAtom,
			numTrianglesPerAtom = numTrianglesPerAtom,
            vertices = vertices,
            normals = normals,
            tris = tris,
			meshColours = meshColours,
            centres = centres,
            radii = radii,
			atomColours = atomColours,
            resolution = Settings.ballResolution
        };

		//Run job
        job.Schedule().Complete();
		
		//Clear and assign mesh
        mesh.Clear();
        mesh.SetVertices(vertices);
		mesh.SetNormals(normals);
		mesh.triangles = tris.ToArray();
		mesh.SetColors(meshColours);

		//Dispose arrays to clear memory
		refVertices.Dispose();
		refTriangles.Dispose();
		vertices.Dispose();
		normals.Dispose();
		tris.Dispose();
		meshColours.Dispose();
		centres.Dispose();
		radii.Dispose();
		atomColours.Dispose();

	}

	public void SetMeshColours(
		Mesh mesh,
		int numAtoms,
		Color[] atomColoursArray
	) {
		Color[] meshColours = new Color[numAtoms * numVerticesPerAtom];
		int vertexIndex = 0;
		for (int atomNum = 0; atomNum < numAtoms; atomNum++) {
			for (int vertexNum = 0; vertexNum < numVerticesPerAtom; vertexNum++) {
				meshColours[vertexIndex++] = atomColoursArray[atomNum];
			}
		}
	}

	/// <summary>Job container to generate spheres</summary>
	[BurstCompile]
	private struct GetAllSpheresJob : IJob {

		/// <summary>How much a sphere's radius be scaled versus the atomic radius</summary>
		public float atomicRadiusToSphereRatio;

		/// <summary>The reference vertices for an icosphere</summary>
		public NativeArray<float3> refVertices;
		/// <summary>The reference triangles for an icosphere</summary>
		public NativeArray<int3> refTriangles;
		/// <summary>The number of vertices the reference sphere should have</summary>
		public int numVerticesPerAtom;
		/// <summary>The number of triangles the reference sphere should have</summary>
		public int numTrianglesPerAtom;

		/// <summary>Mesh vertices to generate</summary>
		public NativeArray<float3> vertices;
		/// <summary>Mesh normals to generate</summary>
		public NativeArray<float3> normals;
		/// <summary>Mesh tris to generate</summary>
		public NativeArray<int> tris;
		/// <summary>Mesh colours to generate</summary>
		public NativeArray<Color> meshColours;

		/// <summary>Atomic positions</summary>
		public NativeArray<float3> centres;
		/// <summary>Atomic radii</summary>
		public NativeArray<float> radii;
		/// <summary>Atomic colours</summary>
		public NativeArray<Color> atomColours;
		/// <summary>The resolution of the mesh</summary>
		public int resolution;

		/// <summary>Run the job - called on job.Schedule().Complete()</summary>
		public void Execute() {

			int numAtoms = centres.Length;
			int vertexIndex = 0;
			int triIndex = 0;
			//Loop through each atom
			for (int atomNum=0; atomNum<numAtoms; atomNum++) {
				
				//Get atomic poisition
				float3 position = centres[atomNum];
				//Sphere radius is atomic radius scaled by sphere ratio
				float radius = radii[atomNum] * atomicRadiusToSphereRatio;

				//Get colour
				Color color = atomColours[atomNum];

				//Assign vertices, normals and colours
				for (int vertexNum=0; vertexNum<numVerticesPerAtom; vertexNum++) {
					float3 refVertex = refVertices[vertexNum];
					meshColours[vertexIndex] = color;
					normals[vertexIndex] = refVertex;
					vertices[vertexIndex++] = refVertex * radius + position;
				}

				//Assign triangles
				int triOffset = numVerticesPerAtom * atomNum;
				for (int triNum=0; triNum<numTrianglesPerAtom; triNum++) {
					int3 refTriangle = refTriangles[triNum] + triOffset;
					tris[triIndex++] = refTriangle.x;
					tris[triIndex++] = refTriangle.y;
					tris[triIndex++] = refTriangle.z;
				}
			}
		}
	}

	/// <summary>Calculate the ratio to to scale a vector created from the midpoint of two points on a sphere to 1</summary>
	private static float GetNormalisationFactor(float3 vertex0, float3 vertex1) {
		return 1f / math.length(vertex0 + vertex1);
	}

	/// <summary>Get the vertices of a standard icosahedron</summary>
	private static void GetIcosahedronVertices(float3[] vertices) {

		float x = (1f + Mathf.Sqrt(5f)) / 2f;
		float a = 1f / Mathf.Sqrt(1f + x * x);
		float b = x * a;
		float c = 0f;
		
		int vertexIndex = 0;
		vertices[vertexIndex++] = new float3(-a,  b,  c);
		vertices[vertexIndex++] = new float3( a,  b,  c);
		vertices[vertexIndex++] = new float3(-a, -b,  c);
		vertices[vertexIndex++] = new float3( a, -b,  c);

		vertices[vertexIndex++] = new float3( c, -a,  b);
		vertices[vertexIndex++] = new float3( c,  a,  b);
		vertices[vertexIndex++] = new float3( c, -a, -b);
		vertices[vertexIndex++] = new float3( c,  a, -b);

		vertices[vertexIndex++] = new float3( b,  c, -a);
		vertices[vertexIndex++] = new float3( b,  c,  a);
		vertices[vertexIndex++] = new float3(-b,  c, -a);
		vertices[vertexIndex  ] = new float3(-b,  c,  a);

	}

	/// <summary>Get the triangle indices of a standard icosahedron</summary>
	private static void GetIcosahedronTriangles(int3[] triangles) {

		int i=0;
		triangles[i++] = new int3( 0, 1, 7);
		triangles[i++] = new int3( 7, 1, 8);
		triangles[i++] = new int3( 0, 7,10);
		triangles[i++] = new int3(10, 7, 6);
		triangles[i++] = new int3( 0,10,11);
		triangles[i++] = new int3(11,10, 2);
		triangles[i++] = new int3( 0,11, 5);
		triangles[i++] = new int3( 5,11, 4);
		triangles[i++] = new int3( 0, 5, 1);
		triangles[i++] = new int3( 1, 5, 9);
		triangles[i++] = new int3( 3, 6, 8);
		triangles[i++] = new int3( 8, 6, 7);
		triangles[i++] = new int3( 3, 2, 6);
		triangles[i++] = new int3( 6, 2,10);
		triangles[i++] = new int3( 3, 4, 2);
		triangles[i++] = new int3( 2, 4,11);
		triangles[i++] = new int3( 3, 9, 4);
		triangles[i++] = new int3( 4, 9, 5);
		triangles[i++] = new int3( 3, 8, 9);
		triangles[i++] = new int3( 9, 8, 1);

	}
}
