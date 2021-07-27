using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
#if UNITY_STANDALONE_OSX
using Unity.Burst;
#endif
using Unity.Jobs;

///<summary>Cylinder Generator Singleton Class</summary>
/// <remarks>
/// Assigns cylinder geometries to Mesh objects.
/// </remarks>
public class Cylinder : MonoBehaviour {

	private static Cylinder _main;
	public static Cylinder main {
		get {
			if (ReferenceEquals(_main, null)) {
				GameObject go = new GameObject("Cylinder");
				_main = go.AddComponent<Cylinder>();
			}
			return _main;
		}
	}

	/// <summary>Reference cylinder vertices</summary>
	float3[] refVertices;
	/// <summary>Reference cylinder mesh normals</summary>
	float3[] refNormals;
	/// <summary>Reference cylinder mesh triangles</summary>
	int3[] refTriangles;
	/// <summary>Number of vertices in the reference cylinder</summary>
	int numVerticesPerCylinder;

	/// <summary>Regenerate the reference cylinder (use when changing resolution)</summary>
	public void UpdateReference() {
		numVerticesPerCylinder = CustomMathematics.IntPow(2, Settings.stickResolution + 3);
		GetCylinder(out refVertices, out refNormals, out refTriangles, Settings.stickResolution);
	}

	///<summary>Set the vertices, normals and triangles of a cylinder</summary>
	///<param name="vertices">The vertices of the new cylinder.</param>
	///<param name="normals">The normals (vectors of the norm of the surface for mesh mapping) of the new cylinder.</param>
	///<param name="triangles">Vertex indices that make up triangles, as int3.</param>
	///<param name="resolution">Resolution of the cylinder mesh, making 2^(resolution+2) divisions of a circle.</param>
	public void GetCylinder(
		out float3[] vertices, 
		out float3[] normals, 
		out int3[] triangles, 
		int resolution
	) {
		if (resolution < 0) {throw new System.Exception("Can't create a cylinder with negative resolution!");}

		//Number of times the circle that makes this cylinder should be divided
		int subdivisions = CustomMathematics.IntPow(2, resolution + 2);

		int pow = CustomMathematics.IntPow(2, Settings.stickResolution + 2);
		int numVerticesPerCylinder = 2 * pow;

		//Initialise arrays
		vertices = new float3[numVerticesPerCylinder];
		normals = new float3[numVerticesPerCylinder];
		triangles = new int3[numVerticesPerCylinder];

		//Use this flag to join end to start
		bool lastFace = false;

		int vertexIndex = 0;
		int startIndex = 0;

		int t0; int t1; int t2; int t3;

		float deltaAngle = 2 * Mathf.PI / subdivisions;
		
		int subdivision = 1;
		while (!lastFace) {

			//Build circle
			float angle = deltaAngle * subdivision;
			float x = Mathf.Cos(angle);
			float y = Mathf.Sin(angle);

			//First edge of pair of triangles
			t0 = vertexIndex++;
			t1 = vertexIndex++;

			//Second edge of pair of triangles
			if (subdivision == subdivisions) {
				
				//Go back to start if at the last pair
				t2 = 0;
				t3 = 1;

				lastFace = true;
			} else {

				//Add two tris and reset vertIndex
				t2 = vertexIndex++;
				t3 = vertexIndex++;
				vertexIndex -= 2;
			}

			//Assign arrays 
			vertices[startIndex  ] = new float3 (x, y, -1f);
			triangles[startIndex ] = new int3   (t0,t2, t1);
			normals [startIndex++] = new float3 (x, y,  0f);
			vertices[startIndex  ] = new float3 (x, y,  1f);
			triangles[startIndex ] = new int3   (t1,t2, t3);
			normals [startIndex++] = new float3 (x, y,  0f);


			subdivision++;
		}	
	}

	/// <summary>Assign vertices, normals and triangles of a mesh of molecular bonds</summary>
	/// <param name="mesh">The mesh to assign</param>
	/// <param name="centresArray">The centres of the atoms</param>
	/// <param name="radiiArray">The radii of the atoms - these are scaled by the atomicRadiusToSphereRatio and atomicRadiusToCylinderRatio settings</param>
	/// <param name="bondsArray">int2 pairs of atoms to bond together - the indices correspond to <c>centresArray</c></param>
	/// <param name="atomColoursArray">The colours to apply to each atom</param>
	public void SetMesh(
		Mesh mesh,
		float3[] centresArray,
		float[] radiiArray,
		int2[] bondsArray,
		Color[] atomColoursArray
	) {
		
		int numAtoms = centresArray.Length;
		int numBonds = bondsArray.Length;

		//Do some checks
		if (numAtoms != radiiArray.Length) {
			throw new System.Exception("Cannot assign Cylinder Mesh: Number of atoms != number of radii");
		}
		if (numAtoms != atomColoursArray.Length) {
			throw new System.Exception("Cannot assign Cylinder Mesh: Number of atoms != number of colours");
		}
		for (int bondNum=0; bondNum<numBonds; bondNum++) {
			int2 bondPair = bondsArray[bondNum];
			if (bondPair.x > numAtoms) {
				throw new System.Exception(string.Format(
					"Cannot assign Cylinder Mesh: Bond pair {0} references atom index {1} when there are only {2} atoms",
					bondNum,
					bondPair.x,
					numAtoms
				));
			}
			if (bondPair.y > numAtoms) {
				throw new System.Exception(string.Format(
					"Cannot assign Cylinder Mesh: Bond pair {0} references atom index {1} when there are only {2} atoms",
					bondNum,
					bondPair.y,
					numAtoms
				));
			}
		}
		
        int numVertices = numVerticesPerCylinder * numBonds;
        int numVerts = 3 * numVertices;

		//Assign all the arrays - this bypasses Garbage Collection until job is complete and switches on SIMD
		NativeArray<float3> refVertices = new NativeArray<float3>(this.refVertices, Allocator.TempJob);
		NativeArray<float3> refNormals = new NativeArray<float3>(this.refNormals, Allocator.TempJob);
		NativeArray<int3> refTriangles = new NativeArray<int3>(this.refTriangles, Allocator.TempJob);

		NativeArray<float3> vertices = new NativeArray<float3>(numVertices, Allocator.TempJob);
		NativeArray<float3> normals = new NativeArray<float3>(numVertices, Allocator.TempJob);
		NativeArray<int> tris = new NativeArray<int>(numVerts, Allocator.TempJob);
		NativeArray<Color> meshColours = new NativeArray<Color>(numVertices, Allocator.TempJob);


		NativeArray<float3> centres = new NativeArray<float3>(centresArray, Allocator.TempJob);
		NativeArray<float> radii = new NativeArray<float>(radiiArray, Allocator.TempJob);
		NativeArray<int2> bonds = new NativeArray<int2>(bondsArray, Allocator.TempJob);
		NativeArray<Color> atomColours = new NativeArray<Color>(atomColoursArray, Allocator.TempJob);

		//Create job
        GetAllCylindersJob job = new GetAllCylindersJob {
			atomicRadiusToCylinderRatio = Settings.atomicRadiusToCylinderRatio,
			atomicRadiusToSphereRatio = Settings.atomicRadiusToSphereRatio,
			refVertices = refVertices,
			refNormals = refNormals,
			refTriangles = refTriangles,
			numVerticesPerCylinder = numVerticesPerCylinder,
            vertices = vertices,
            normals = normals,
            tris = tris,
			meshColours = meshColours,
            centres = centres,
            radii = radii,
            bonds = bonds,
			atomColours = atomColours,
            resolution = Settings.stickResolution
        };

		//Run job
        job.Schedule().Complete();
		
		//Clear and assign mesh
        mesh.Clear();
        mesh.SetVertices(vertices);
		mesh.SetNormals(normals);
		mesh.SetTriangles(tris.ToArray(), 0);
		mesh.SetColors(meshColours);

		//Dispose arrays to clear memory
		refVertices.Dispose();
		refNormals.Dispose();
		refTriangles.Dispose();
		vertices.Dispose();
		normals.Dispose();
		tris.Dispose();
		meshColours.Dispose();
		centres.Dispose();
		radii.Dispose();
		bonds.Dispose();
		atomColours.Dispose();

	}

	#if UNITY_STANDALONE_OSX
	/// <summary>Job container to generate cylinders</summary>
	[BurstCompile]
	#endif
	private struct GetAllCylindersJob : IJob {

		/// <summary>How much a cylinder's radius be scaled versus the atomic radius</summary>
		public float atomicRadiusToCylinderRatio;
		/// <summary>How much a sphere's radius be scaled versus the atomic radius</summary>
		public float atomicRadiusToSphereRatio;

		/// <summary>The reference vertices for a cylinder pointing in the z-axis</summary>
		public NativeArray<float3> refVertices;
		/// <summary>The reference normals for a cylinder pointing in the z-axis</summary>
		public NativeArray<float3> refNormals;
		/// <summary>The reference triangle for a cylinder pointing in the z-axis</summary>
		public NativeArray<int3> refTriangles;
		/// <summary>The number of vertices the reference cylinder should have</summary>
		public int numVerticesPerCylinder;

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
		/// <summary>Molecular bonds</summary>
		public NativeArray<int2> bonds;
		/// <summary>Atomic colours</summary>
		public NativeArray<Color> atomColours;
		/// <summary>The resolution of the mesh</summary>
		public int resolution;

		/// <summary>Run the job - called on job.Schedule().Complete()</summary>
		public void Execute() {

			int numBonds = bonds.Length;

			//The direction of the reference cylinder
			float3 alignmentVector = new float3(0f, 0f, 1f);

        	int colorIndex = 0;
			int vertexIndex = 0;
			int triangleIndex = 0;
			//Loop through each bond
			for (int bondNum=0; bondNum<numBonds; bondNum++) {

				//Get the bond pairs
				int2 bondAtomIndices = bonds[bondNum];

				//Get the colours of each side of the bond
				Color colour0 = atomColours[bondAtomIndices.x];
				Color colour1 = atomColours[bondAtomIndices.y];

				//Assign colours for each vertex
				for (int c=0; c<numVerticesPerCylinder / 2; c++) {
					meshColours[colorIndex++] = colour0;
					meshColours[colorIndex++] = colour1;
				}

				//Get the positions of each end of the cylinder
				float3 centre0 = centres[bondAtomIndices.x];
				float3 centre1 = centres[bondAtomIndices.y];

				//The centre of the cylinder is the midpoint of these positions
				float3 bondCentre = (centre0 + centre1) * 0.5f;

				//Get the vector of the bond
				float3 bondVector = centre1 - centre0;

				//Get the radii of the atoms
				float radius0 = radii[bondAtomIndices.x];
				float radius1 = radii[bondAtomIndices.y];

				//The actual radius is the average scaled by the cylinder ratio
				float radius = (radius0 + radius1) * 0.5f * atomicRadiusToCylinderRatio;

				//Get the length of the bond
				float bondLength = math.length(bondVector);

				//Truncate the bond to include the spheres so transparent meshes look better
				//Pythagorean maths to decide how much to cut off the cylinder
				float trunc0 = CustomMathematics.Squared(radius0 * atomicRadiusToSphereRatio) - CustomMathematics.Squared(radius);
				//Possible that the sphere ratio is set smaller than the cylinder ratio - this will prevent crashes
				trunc0 = (trunc0 > 0f) ? math.sqrt(trunc0) : 0f;
				//Truncate cylinder
				float centreLength0 = bondLength * 0.5f - trunc0;

				//Same as above but for other side
				float trunc1 = CustomMathematics.Squared(radius1 * atomicRadiusToSphereRatio) - CustomMathematics.Squared(radius);
				trunc1 = (trunc1 > 0f) ? math.sqrt(trunc1) : 0f;
				float centreLength1 = bondLength * 0.5f - trunc1;

				//Get the rotation matrix that will rotate the reference cylinder
				float3x3 R = CustomMathematics.GetRotationMatrix(
					alignmentVector,
					bondVector / bondLength
				);

				//Create a scale to apply to each vertex
				float3 scale = new float3 (radius, radius, 0f);
				for (int refVertexNum=0; refVertexNum<numVerticesPerCylinder; refVertexNum++) {

					//Scale the z axis using the truncated vectors
					scale[2] = (refVertexNum % 2 == 0) ? centreLength0 : centreLength1;

					// Scale, rotate and translate vertex
					vertices[vertexIndex  ] = CustomMathematics.Dot(R, refVertices[refVertexNum] * scale) + bondCentre;
					normals [vertexIndex++] = CustomMathematics.Dot(R, refNormals[refVertexNum]);
				}

				for (int refTriangleIndex=0; refTriangleIndex<numVerticesPerCylinder; refTriangleIndex++) {
					int3 triangle = refTriangles[refTriangleIndex];
					int triOffset =  numVerticesPerCylinder * bondNum;
					tris[triangleIndex++] = triangle.x + triOffset;
					tris[triangleIndex++] = triangle.y + triOffset;
					tris[triangleIndex++] = triangle.z + triOffset;
				}
			}

		}
	}

}