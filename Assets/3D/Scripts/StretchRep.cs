using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class StretchRep {

	public static void GenerateStretchRep(int resolution, float thickness, float offset, List<float> lengths, List<float> energies, Mesh mesh) {

		//Get colors
		List<Color> energyColors = new List<Color>();

		float minEnergy = energies[0];
		float maxEnergy = energies[0];
		float energy;
		for (int energyNum = 1; energyNum < energies.Count; energyNum++) {
			energy = energies[energyNum];
			if (energy < minEnergy) {
				minEnergy = energy;
			}
			if (energy > maxEnergy) {
				maxEnergy = energy;
			}
		}

		for (int energyNum = 0; energyNum < energies.Count; energyNum++) {
			energy = energies[energyNum];
			energyColors.Add(ColorMap.GetColor((energy - minEnergy) / (maxEnergy - minEnergy)));
		}

		List<Vector3> vertices = new List<Vector3>();
		List<Face> faces = new List<Face>();
		List<Color> colors = new List<Color>();

		int numSegments = lengths.Count - 1;
		for (int segmentNum = 0; segmentNum < numSegments; segmentNum++) {
			AddSegment(
				resolution, 
				lengths[segmentNum], 
				lengths[segmentNum + 1], 
				thickness, 
				offset,
				energyColors[segmentNum], 
				energyColors[segmentNum + 1], 
				vertices, 
				faces, 
				colors
			);
		}

		mesh.vertices = vertices.ToArray();
		mesh.triangles = FacesToTriangles(faces);
		mesh.colors = colors.ToArray();

	}

	public static void AddSegment(int resolution, float startZ, float endZ, float thickness, float offset, Color startColor, Color endColor, List<Vector3> vertices, List<Face> faces, List<Color> colors) {
		
		//Add a rectangle with <resolution> colors between startZ and endZ, with height (Y) of thickness.
		//Rectangle composed of:
		//	2 * (2 + resolution) vertices (-2 if not the first segment)
		//	2 * (1 + resolution) triangles

		float zPos = startZ;
		Color color;
		resolution += 1;
		float dZ = (endZ - startZ) / resolution;
		int numVerts = vertices.Count;

		for (int i = 0; i < resolution; i++) {
			color = Color.Lerp(startColor, endColor, ((float)i) / resolution);
			
			//First segment needs 2 more vertices
			if (i == 0 && numVerts == 0) {
				vertices.Add(new Vector3(0f, offset, zPos));
				vertices.Add(new Vector3(0f, offset + thickness, zPos));
				colors.Add(color);
				colors.Add(color);
				numVerts += 2;
			}

			zPos += dZ;
			vertices.Add(new Vector3(0f, offset, zPos));
			vertices.Add(new Vector3(0f, offset + thickness, zPos));
			colors.Add(color);
			colors.Add(color);
			numVerts += 2;

			//Add front and back face
			faces.Add(new Face(numVerts - 4, numVerts - 3, numVerts - 1));
			faces.Add(new Face(numVerts - 4, numVerts -1, numVerts - 2));
			faces.Add(new Face(numVerts - 4, numVerts - 1, numVerts - 3));
			faces.Add(new Face(numVerts - 4, numVerts - 2, numVerts - 1));

		}
	}
	private static int[] FacesToTriangles(List<Face> faces) {
		int[] triangles = new int[faces.Count * 3];
		int j;
		for (int i=0;i<faces.Count;i++) {
			for (j = 0; j < 3; j++) {
				triangles [i * 3 + j] = faces [i] [j];
			}
		}
		return triangles;
	}
}
