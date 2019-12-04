using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

public static class Interop {
    const string moduleFile = "Fortran";

    [DllImport(
		moduleFile, 
		CallingConvention = CallingConvention.Cdecl, 
        EntryPoint = "__fortran_MOD_getspheregeometry",
		CharSet = CharSet.Ansi)
	]
    public static extern void GetSphereGeometry(
		[In, Out] float[] verts, 
		[In, Out] int[] tris,
        ref int resolution,
		ref int numVerts,
		ref int numTris
    );

    [DllImport(
		moduleFile, 
		CallingConvention = CallingConvention.Cdecl, 
        EntryPoint = "__fortran_MOD_getallspheres",
		CharSet = CharSet.Ansi)
	]
    public static extern void GetAllSpheres(
		[In, Out] float[] verts, 
		[In, Out] float[] norms, 
		[In, Out] int[] tris,
        ref int resolution,
		ref int numVerts,
		ref int numTris,
		[In] float[] atomPositions, 
		[In] float[] atomRadii, 
		ref int numAtoms
    );

	[DllImport(
		moduleFile, 
		CallingConvention = CallingConvention.Cdecl, 
        EntryPoint = "__fortran_MOD_getallcylinders",
		CharSet = CharSet.Ansi
	)]
	public static extern void GetAllCylinders(
		[In, Out] float[] verts, 
		[In, Out] float[] norms, 
		[In, Out] int[] tris,
        ref int resolution,
		ref int numVerts,
		ref int numTris,
		[In] float[] atomPositions, 
		[In] float[] atomRadii, 
		ref int numAtoms,
		[In] int[] bonds, 
		ref int numBonds,
		ref float radiusRatio
	);

	[DllImport(
		moduleFile, 
		CallingConvention = CallingConvention.Cdecl, 
        EntryPoint = "__fortran_MOD_rotatearraywithmatrix",
		CharSet = CharSet.Ansi
	)]
	public static extern void RotateArrayWithMatrix(
		[In, Out] float[] A,
		[In] float[,] R,
		[In] int numRows
	);

	[DllImport(
		moduleFile, 
		CallingConvention = CallingConvention.Cdecl, 
        EntryPoint = "__fortran_MOD_rotatevectorwithmatrix",
		CharSet = CharSet.Ansi
	)]
	public static extern void RotateVectorWithMatrix(
		[In, Out] float[] v,
		[In] float[,] R
	);

	[DllImport(
		moduleFile, 
		CallingConvention = CallingConvention.Cdecl, 
        EntryPoint = "__fortran_MOD_getrotationmatrix",
		CharSet = CharSet.Ansi
	)]
	public static extern void GetRotationMatrix(
		[In] float[] v1n,
		[In] float[] v2n,
		[Out] float[,] R
	);

}