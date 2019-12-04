using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Face {
	private int[] vertices;

	public Face (int i0=0, int i1=0, int i2=0) {
		this.vertices = new int[3] {i0, i1, i2};
	}

	public Face (Face face) {
		this.vertices = new int[3] {face[0], face[1], face[2]};
	}

	public int this[int index] {
		get { return this.vertices [index]; }
		set { this.vertices [index] = value; }
	}

	public override string ToString ()
	{
		return string.Format ("[Face({0}, {1}, {2})]", vertices[0], vertices[1], vertices[2]);
	}
}
