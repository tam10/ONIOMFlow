using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using EL = Constants.ErrorLevel;
using CT = Constants.CoulombType;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

/// <summary>The Mathematics Static Class</summary>
/// <remarks>Contains functions that operate on Float3 Vectors.</remarks>
public static class CustomMathematics {

	/// <summary>Get the magnitude/length of a Float3.</summary>
	/// <param name="v">Input Float3.</param>
	public static float Magnitude3(in float[] v) {
		return Mathf.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
	}

	/// <summary>Get the magnitude/length squared of a Float3.</summary>
	/// <param name="v">Input Float3.</param>
	public static float MagnitudeSquared3(in float[] v) {
		return v[0] * v[0] + v[1] * v[1] + v[2] * v[2];
	}
    
	/// <summary>Get the distance between two positions.</summary>
	/// <param name="p0">Position 0, as a float[3]</param>
	/// <param name="p1">Position 1, as a float[3]</param>
    public static float Distance3(in float[] p0, in float[] p1) => Mathf.Sqrt(Distance3Squared(p0, p1));
    
	/// <summary>Get the distance squared between two positions.</summary>
	/// <param name="p0">Position 0, as a float[3]</param>
	/// <param name="p1">Position 1, as a float[3]</param>
	/// <remarks>This is mildly faster than Distance3 as a Sqrt isn't evaluated.</remarks>
    public static float Distance3Squared(in float[] p0, in float[] p1) {
        float distance = 0f;
        for (int i=0;i<3;i++) {
            distance += Squared(p0[i] - p1[i]);
        }
        return distance;
    }

	/// <summary>Get the distance squared between two positions in a positions array.</summary>
	/// <param name="positions">Array of n positions, as a float[3*n]</param>
	/// <param name="i0">Index in n of Position 0</param>
	/// <param name="i1">Index in n of Position 1</param>
    public static float Distance3Squared(in float[] positions, int i0, int i1) {
        float distance = 0f;
        for (int i=0;i<3;i++) {
            distance += Squared(positions[i0 * 3 + i] - positions[i1 * 3 + i]);
        }
        return distance;
    }

	/// <summary>Get the distance squared between two positions in a positions array.</summary>
	/// <param name="positions">Array of n positions, as a float[n,3]</param>
	/// <param name="i0">Index in n of Position 0</param>
	/// <param name="i1">Index in n of Position 1</param>
    public static float Distance3Squared(in float[,] positions, int i0, int i1) {
        float distance = 0f;
        for (int i=0;i<3;i++) {
            distance += Squared(positions[i0,i] - positions[i1,i]);
        }
        return distance;
    }

	/// <summary>Get the angle in radians between two vectors.</summary>
	/// <param name="v0">Vector 0</param>
	/// <param name="v1">Vector 1</param>
	/// <remarks>v0 and v1 do not need to be normalised.</remarks>
	public static float Angle3(in float[] v0, in float[] v1) {
		return Angle3Norm(Normalise3(v0), Normalise3(v1));
	}

	/// <summary>Get the angle in radians between two vectors.</summary>
	/// <param name="v0">Vector 0</param>
	/// <param name="v1">Vector 1</param>
	/// <remarks>v0 and v1 do not need to be normalised.</remarks>
	public static float Angle(in float3 v0, in float3 v1) {
		return AngleNorm(math.normalizesafe(v0), math.normalizesafe(v1));
	}

	/// <summary>Get the angle in radians between two normalised vectors.</summary>
	/// <param name="v0n">Vector 0 (normalised)</param>
	/// <param name="v1n">Vector 1 (normalised)</param>
	/// <remarks>v0 and v1 must be normalised.</remarks>
	public static float Angle3Norm(in float[] v0n, in float[] v1n) {
		return Mathf.Acos(Dot(v0n, v1n));
	}

	/// <summary>Get the angle in radians between two normalised vectors.</summary>
	/// <param name="v0n">Vector 0 (normalised)</param>
	/// <param name="v1n">Vector 1 (normalised)</param>
	/// <remarks>v0 and v1 must be normalised.</remarks>
	public static float AngleNorm(in float3 v0n, in float3 v1n) {
		return Mathf.Acos(math.dot(v0n, v1n));
	}

	/// <summary>Returns the square of a float.</summary>
	/// <param name="x">Input float.</param>
    public static float Squared(float x) => x * x;
	
	/// <summary>Returns an array whose elements are squared.</summary>
	/// <param name="v">Input array.</param>
    public static float[] Squared(in float[] v) {
		int length = v.Length;
		float[] result = new float[length];
		for (int i=0; i<length; i++) {
			result[i] = Squared(v[i]);
		}
		return result;
	}
	
	/// <summary>Squares the elements of an array in-place.</summary>
	/// <param name="v">Input array.</param>
    public static void SquareIP(float[] v) {
		int length = v.Length;
		for (int i=0; i<length; i++) {
			v[i] = Squared(v[i]);
		}
	}

	/// <summary>Returns the nth power of an integer.</summary>
	/// <param name="x">Integer to be raised by pow.</param>
	/// <param name="pow">Power to raise x by. A value of 0 or less will return 1.</param>
    public static int IntPow(int x, int pow) {
        int result = 1;
        uint _pow = (uint)pow;
        while (_pow > 0) {
            if ((_pow & 1) == 1) result *= x;
            x *= x;
            _pow >>= 1;
        }
        return result;
    }

	/// <summary>Map a value in one domain to another domain.
	/// <example>
	/// This example uses <see cref="Map"/> to map a value of 0.5f from the domain 0f to 1f to the new domain 0f to 5f.
	/// <code>
	/// float mapped = Map(0.5f, 0f, 1f, 0f, 5f);
	/// </code>
	/// mapped will have the value 2.5f.
	/// </example>
	/// </summary>
	/// <param name="x">Input value.</param>
	/// <param name="oldMin">Minimum value of old domain.</param>
	/// <param name="oldMax">Maximum value of old domain.</param>
	/// <param name="newMin">Minimum value of new domain.</param>
	/// <param name="newMax">Maximum value of new domain.</param>
    public static float Map(float x, float oldMin, float oldMax, float newMin, float newMax) {
        return newMin + (x - oldMin) * (newMax - newMin) / (oldMax - oldMin);
    }

	/// <summary>Get a Float3 from an array of floats by its index.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[n,3].</param>
	/// <param name="index">Index of the Float3 to get.</param>
	public static float[] Float3FromArray(in float[,] A, int index) {
		float[] result = new float[3];
		result[0] = A[index, 0];
		result[1] = A[index, 1];
		result[2] = A[index, 2];
		return result;
	}

	/// <summary>Get a Float3 from an array of floats by its index.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[3\*n].</param>
	/// <param name="index">Index of the Float3 to get.</param>
	public static float[] Float3FromArray(in float[] A, int index) {
		float[] result = new float[3];
		index *= 3;
		for (int i=0; i<3; i++) {
			result[i] = A[index++];
		}
		return result;
	}

	/// <summary>Get the average value in float3 of an array of float3's.</summary>
	/// <param name="A">Array of n float3s.</param>
	public static float3 AveragePosition(in IEnumerable<Atom> atoms) {

		float3 result = new float3();
		int length = 0;
		foreach (Atom atom in atoms) {
			result += atom.position;
			length++;
		}
		if (length == 0) {return result;}
        return result / length;
	}

	/// <summary>Get the average value in float3 of an array of float3's.</summary>
	/// <param name="A">Array of n float3s.</param>
	public static float3 Average(in float3[] A) {

		int length = A.GetLength(0);
		float3 result = new float3();
		if (length == 0) {return result;}
		for (int index = 0; index < length; index++) {
			result += A[index];
		}
        return result / length;
	}

	/// <summary>Get the average value in float3x3 of an array of float3x3's.</summary>
	/// <param name="A">Array of n float3x3s.</param>
	public static float3x3 Average(in float3x3[] A) {

		int length = A.GetLength(0);
		float3x3 result = new float3x3();
		if (length == 0) {return result;}
		for (int index = 0; index < length; index++) {
			result += A[index];
		}
        return result / length;
	}

	/// <summary>Get the average value in Float3 of an array of Float3's.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[n,3].</param>
	/// <param name="indices">Indices of Float3's to get average of.</param>
	public static float[] AverageFromArray(in float[,] A, int[] indices) {

		int length = indices.Length;
		float[] result = new float[3];
		for (int itemNum = 0; itemNum < length; itemNum++) {
			int index = indices[itemNum];
			result[0] += A[index, 0];
			result[1] += A[index, 1];
			result[2] += A[index, 2];
		}
        Divide3(result, length);
        return result;
	}

	/// <summary>Get the average value in Float3 of a Jagged array of Float3's.</summary>
	/// <param name="A">Array of n Float3's, as a float[n][3].</param>
	public static float[] AverageFromArray(in float[][] A) {

		int length = A.GetLength(0);
		float[] result = new float[3];
		for (int index = 0; index < length; index++) {
			Add3IP(result, A[index]);
		}
        Divide3(result, length);
        return result;
	}

	/// <summary>Get the average value in Float3 of a Jagged array of Float3's.</summary>
	/// <param name="A">Array of n Float3's, as a float[n][3].</param>
	/// <param name="indices">Indices of Float3's to get average of.</param>
	public static float[] AverageFromArray(in float[][] A, int[] indices) {

		int length = indices.Length;
		float[] result = new float[3];
		for (int itemNum = 0; itemNum < length; itemNum++) {
			int index = indices[itemNum];
			Add3IP(result, A[index]);
		}
        Divide3(result, length);
        return result;
	}

	/// <summary>Get the average value in Float3 of an array of Float3's.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[n,3].</param>
	public static float RMS(in float[,] A) {

		int length = A.Length;
		float result = 0f;
		for (int index = 0; index < length; index++) {
			result += MagnitudeSquared3(Float3FromArray(A, index));
		}
        return Mathf.Sqrt(result / length);
	}

	/// <summary>Get the average value in Float3 of an array of Float3's.</summary>
	/// <param name="A">Array of n Float3's, as a float[n][3].</param>
	public static float RMS(in float[][] A) {

		int length = A.Length;
		float result = 0f;
		for (int index = 0; index < length; index++) {
			result += MagnitudeSquared3(A[index]);
		}
        return Mathf.Sqrt(result / length);
	}

	/// <summary>Get the average value in float3 of an array of float3's.</summary>
	/// <param name="A">Array of n float3's, as a float3[n].</param>
	public static float RMS(in float3[] A) {

		int length = A.Length;
		float result = 0f;
		for (int index = 0; index < length; index++) {
			result += math.length(A[index]);
		}
        return Mathf.Sqrt(result / length);
	}


	/// <summary>Get the index of the minimum value of a vector.</summary>
	/// <param name="v">Array of floats.</param>
	public static int IndexOfMin(in float[] v) {
		if (v == null) {
			throw new System.ArgumentNullException("v");
		}
		if (v.Length == 0) {
			throw new System.ArgumentOutOfRangeException("v");
		}
		int index = 0;
		float min = v[index];
		for (int i=1; i<v.Length; i++) {
			if (v[i] < min) {
				min = v[i];
				index = i;
			}
		}
		return index;
	}
	
	/// <summary>Get the index of the minimum value of a List of floats.</summary>
	/// <param name="v">List of floats.</param>
	public static int IndexOfMin(in IList<float> v) {
		if (v == null) {
			throw new System.ArgumentNullException("v");
		}
		if (!v.Any()) {
			throw new System.ArgumentOutOfRangeException("v");
		}
		int index = 0;
		float min = v[index];
		for (int i=1; i<v.Count; i++) {
			if (v[i] < min) {
				min = v[i];
				index = i;
			}
		}
		return index;
	}
	
	/// <summary>Get the key of the minimum value of a Dictionary of floats.</summary>
	/// <param name="dictionary">Dictionary of floats.</param>
	public static T IndexOfMin<T>(in IDictionary<T, float> dictionary) {
		if (dictionary == null) {
			throw new System.ArgumentNullException("dictionary");
		}
		T index = dictionary.First().Key;
		float min = dictionary.First().Value;
		foreach (KeyValuePair<T, float> kvp in dictionary) {
			if (kvp.Value < min) {
				min = kvp.Value;
				index = kvp.Key;
			}
		}
		return index;
	}

	/// <summary>Get the index of the maximum value of a vector.</summary>
	/// <param name="v">Array of floats.</param>
	public static int IndexOfMax(in float[] v) {
		if (v == null) {
			throw new System.ArgumentNullException("v");
		}
		if (v.Length == 0) {
			throw new System.ArgumentOutOfRangeException("v");
		}
		int index = 0;
		float max = v[index];
		for (int i=1; i<v.Length; i++) {
			if (v[i] > max) {
				max = v[i];
				index = i;
			}
		}
		return index;
	}

	/// <summary>Get the index of the maximum value of a List of floats.</summary>
	/// <param name="v">List of floats.</param>
	public static int IndexOfMax(in IList<float> v) {
		if (v == null) {
			throw new System.ArgumentNullException("v");
		}
		if (!v.Any()) {
			throw new System.ArgumentOutOfRangeException("v");
		}
		int index = 0;
		float max = v[index];
		for (int i=1; i<v.Count; i++) {
			if (v[i] > max) {
				max = v[i];
				index = i;
			}
		}
		return index;
	}
	
	/// <summary>Get the key of the maximum value of a Dictionary of floats.</summary>
	/// <param name="dictionary">Dictionary of floats.</param>
	public static T IndexOfMax<T>(in IDictionary<T, float> dictionary) {
		if (dictionary == null) {
			throw new System.ArgumentNullException("dictionary");
		}
		T index = dictionary.First().Key;
		float max = dictionary.First().Value;
		foreach (KeyValuePair<T, float> kvp in dictionary) {
			if (kvp.Value > max) {
				max = kvp.Value;
				index = kvp.Key;
			}
		}
		return index;
	}

	/// <summary>Get the minumum value of a vector.</summary>
	/// <param name="v">Array of floats.</param>
	public static float Min(in float[] v) {
		float min = v[0];
		for (int i=1; i<v.Length; i++) {
			min = Mathf.Min(min, v[i]);
		}
		return min;
	}

	/// <summary>Get the maximum value of a vector.</summary>
	/// <param name="v">Array of floats.</param>
	public static float Max(in float[] v) {
		float max = v[0];
		for (int i=1; i<v.Length; i++) {
			max = Mathf.Max(max, v[i]);
		}
		return max;
	}

	public static float[] Midpoint(in float[] p0, in float[] p1) {
		float[] result = new float[3];
		for (int i=0; i<3; i++) {
			result[i] = (p0[i] + p1[i]) * 0.5f;
		}
		return result;
	}

	/// <summary>Get the midpoint of two positions in an array of Float3's and save to result.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[n,3].</param>
	/// <param name="index0">Index of Position 0.</param>
	/// <param name="index1">Index of Position 1.</param>
	/// <param name="result">Float3 to save the average position to.</param>
	/// <remarks>Faster than MidpointFromArray.</remarks>
	public static void MidpointFromArrayIP(in float[,] A, int index0, int index1, float[] result) {
		result[0] = (A[index0, 0] + A[index1, 0]) * 0.5f;
		result[1] = (A[index0, 1] + A[index1, 1]) * 0.5f;
		result[2] = (A[index0, 2] + A[index1, 2]) * 0.5f;
	}

	/// <summary>>Get the midpoint of two positions in an array of Float3's.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[n,3].</param>
	/// <param name="index0">Index of Position 0.</param>
	/// <param name="index1">Index of Position 1.</param>
	public static float[] MidpointFromArray(in float[,] A, int index0, int index1) {
		float[] result = new float[3];
		result[0] = (A[index0, 0] + A[index1, 0]) * 0.5f;
		result[1] = (A[index0, 1] + A[index1, 1]) * 0.5f;
		result[2] = (A[index0, 2] + A[index1, 2]) * 0.5f;
		return result;
	}

	/// <summary>Get the midpoint of two positions in an array of Float3's and save to result.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[3\*n].</param>
	/// <param name="index0">Index of Position 0.</param>
	/// <param name="index1">Index of Position 1.</param>
	/// <param name="result">Float3 to save the average position to.</param>
	/// <remarks>Faster than MidpointFromArray.</remarks>
	public static void MidpointFromArrayIP(in float[] A, int index0, int index1, float[] result) {
		index0 *= 3;
		index1 *= 3;
		result[0] = (A[index0++] + A[index1++]) * 0.5f;
		result[1] = (A[index0++] + A[index1++]) * 0.5f;
		result[2] = (A[index0] + A[index1]) * 0.5f;
	}

	/// <summary>>Get the midpoint of two positions in an array of Float3's.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[3\*n].</param>
	/// <param name="index0">Index of Position 0.</param>
	/// <param name="index1">Index of Position 1.</param>
	public static float[] MidpointFromArray(in float[] A, int index0, int index1) {
		float[] result = new float[3];
		index0 *= 3;
		index1 *= 3;
		result[0] = (A[index0++] + A[index1++]) * 0.5f;
		result[1] = (A[index0++] + A[index1++]) * 0.5f;
		result[2] = (A[index0] + A[index1]) * 0.5f;
		return result;
	}

	/// <summary>Get the vector between two Float3's in an array of Float3's.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[n,3].</param>
	/// <param name="from">Index of the Position to measure the vector from.</param>
	/// <param name="to">Index of the Position to measure the vector to.</param>
	/// <param name="result">Float3 to save the vector to.</param>
	/// <remarks>Faster than Float3VectorFromArray.</remarks>
	public static void Float3VectorFromArrayIP(in float[,] A, int from, int to, float[] result) {
		result[0] = A[to, 0] - A[from, 0];
		result[1] = A[to, 1] - A[from, 1];
		result[2] = A[to, 2] - A[from, 2];
	}


	/// <summary>Get the vector between two Float3's in an array of Float3's.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[n,3].</param>
	/// <param name="from">Index of Position 0.</param>
	/// <param name="to">Index of Position 1.</param>
	public static float[] Float3VectorFromArray(in float[,] A, int from, int to) {
		float[] result = new float[3];
		result[0] = A[to, 0] - A[from, 0];
		result[1] = A[to, 1] - A[from, 1];
		result[2] = A[to, 2] - A[from, 2];
		return result;
	}

	/// <summary>Get the vector between two Float3's in an array of Float3's.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[3\*n].</param>
	/// <param name="from">Index of the Position to measure the vector from.</param>
	/// <param name="to">Index of the Position to measure the vector to.</param>
	/// <param name="result">Float3 to save the vector to.</param>
	/// <remarks>Faster than Float3VectorFromArray.</remarks>
	public static void Float3VectorFromArrayIP(in float[] A, int from, int to, float[] result) {
		from *= 3;
		to *= 3;
		result[0] = A[to++] - A[from++];
		result[1] = A[to++] - A[from++];
		result[2] = A[to] - A[from];
	}

	/// <summary>Get the vector between two Float3's in an array of Float3's.</summary>
	/// <param name="A">Array of 3\*n floats or n Float3's, as a float[3\*n].</param>
	/// <param name="from">Index of Position 0.</param>
	/// <param name="to">Index of Position 1.</param>
	public static float[] Float3VectorFromArray(in float[] A, int from, int to) {
		float[] result = new float[3];
		from *= 3;
		to *= 3;
		result[0] = A[to++] - A[from++];
		result[1] = A[to++] - A[from++];
		result[2] = A[to] - A[from];
		return result;
	}

	/// <summary>Get the Float3 form of a Vector3 and save to result.</summary>
	/// <param name="v">Input Vector3.</param>
	/// <param name="result">Output Float3.</param>
	public static void Vector3ToFloat3IP(in Vector3 v, float[] result) {
		for (int i=0; i<3; i++) {
			result[i] = v[i];
		}
	}

	/// <summary>Get the Float3 form of a Vector3.</summary>
	/// <param name="v">Input Vector3.</param>
	public static float[] Vector3ToFloat3(Vector3 v) {
		return new float[3] {v[0], v[1], v[2]};
	}

	/// <summary>Get the Vector3 form of a Float3 and save to result.</summary>
	/// <param name="v">Input Float3.</param>
	/// <param name="result">Output Vector3.</param>
	public static void Float3ToVector3IP(in float[] v, Vector3 result) {
		for (int i=0; i<3; i++) {
			result[i] = v[i];
		}
	}

	/// <summary>Get the Vector3 form of a Float3.</summary>
	/// <param name="v">Input Float3.</param>
	/// <param name="result">Output Vector3.</param>
	public static Vector3 Float3ToVector3(in float[] v) {
		return new Vector3(v[0], v[1], v[2]);
	}

	/// <summary>Get the Vector3 form of a Float3.</summary>
	/// <param name="v">Input Float3.</param>
	/// <param name="result">Output Vector3.</param>
	public static float3 Float3Tofloat3(in float[] v) {
		return new float3(v[0], v[1], v[2]);
	}

	/// <summary>Invert each element in a Vector3 in-place.</summary>
	/// <param name="v">Input Vector3.</param>
	public static void InvertVector3IP(Vector3 v) {
		for (int i=0; i<3; i++) {
			v[i] = 1f / v[i];
		}
	}

	/// <summary>Invert each element in a Vector3.</summary>
	/// <param name="v">Input Vector3.</param>
	public static Vector3 InvertVector3(in Vector3 v) {
		return new Vector3(1f / v[0], 1f / v[1], 1f / v[2]);
	}

	/// <summary>Copy a Float3.</summary>
	/// <param name="from">Float3 to copy from.</param>
	/// <param name="to">Float3 to copy to.</param>
	public static void Copy(in float[] from, float[] to) {
		for (int i=0; i<3; i++) {
			to[i] = from[i];
		}
	}

	/// <summary>Check whether two arrays are equal.</summary>
	/// <param name="v0">Array 0.</param>
	/// <param name="v1">Array 1.</param>
	public static bool IsEqual(float[] v0, float[] v1) {
		return Enumerable.SequenceEqual(v0, v1);
	}

	public static float[] Random3() {
		return new float[] {
			UnityEngine.Random.Range(-1f, 1f),
			UnityEngine.Random.Range(-1f, 1f),
			UnityEngine.Random.Range(-1f, 1f)
		};
	}

	/// <summary>Add two Float3's and return the result.</summary>
	/// <param name="v0">Float3 0.</param>
	/// <param name="v1">Float3 1.</param>
	public static float[] Add3(in float[] v0, in float[] v1) {
        float[] result = new float[3];
		for (int i=0; i<3; i++) {
			result[i] = v0[i] + v1[i];
		}
        return result;
	}
	
	public static float[] Sum3(params float[][] args) {
        float[] result = new float[3];
		for (int argNum=0; argNum<args.Length; argNum++) {
			Add3IP(result, args[argNum]);
		}
        return result;
	}

	/// <summary>Add two Float3's and save the result to v0.</summary>
	/// <param name="v0">Float3 0.</param>
	/// <param name="v1">Float3 1.</param>
	public static void Add3IP(float[] v0, in float[] v1) {
		for (int i=0; i<3; i++) {
			v0[i] = v0[i] + v1[i];
		}
	}

	/// <summary>Subtract two Float3's and return the result.</summary>
	/// <param name="v0">Float3 0 to subtract from.</param>
	/// <param name="v1">Float3 1 to subtract by.</param>
	public static float[] Subtract3(in float[] v0, in float[] v1) {
        float[] result = new float[3];
		for (int i=0; i<3; i++) {
			result[i] = v0[i] - v1[i];
		}
        return result;
	}

	/// <summary>Subtract two Float3's and save the result to v0.</summary>
	/// <param name="v0">Float3 0 to subtract from.</param>
	/// <param name="v1">Float3 1 to subtract by.</param>
	public static void Subtract3IP(float[] v0, in float[] v1) {
		for (int i=0; i<3; i++) {
			v0[i] = v0[i] - v1[i];
		}
	}

	/// <summary>Divide a Float3 by a float and return the result.</summary>
	/// <param name="v">Float3 to divide.</param>
	/// <param name="d">Float to divide by.</param>
	public static float[] Divide3(in float[] v, float d) {
        float[] result = new float[3];
		for (int i=0; i<3; i++) {
			result[i] = v[i] / d;
		}
        return result;
	}

	/// <summary>Divide a Float3 in-place by a float.</summary>
	/// <param name="v">Float3 to divide.</param>
	/// <param name="d">Float to divide by.</param>
	public static void Divide3IP(float[] v, float d) {
		for (int i=0; i<3; i++) {
			v[i] /= d;
		}
	}

	/// <summary>Scale a Float3 by a float and return the result.</summary>
	/// <param name="v">Float3 to scale.</param>
	/// <param name="m">Float to scale by.</param>
	public static float[] Scale3(in float[] v, float m) {
        float[] result = new float[3];
		for (int i=0; i<3; i++) {
			result[i] = v[i] * m;
		}
        return result;
	}

	/// <summary>Scale a Float3 in-place by a float.</summary>
	/// <param name="v">Float3 to scale.</param>
	/// <param name="m">float to scale by.</param>
	public static void Scale3IP(float[] v, float m) {
		for (int i=0; i<3; i++) {
			v[i] *= m;
		}
	}

	/// <summary>Multiply a Float3 by a Float3 component-wise and return the result.</summary>
	/// <param name="v0">First Float3 to multiply.</param>
	/// <param name="v1">Second Float3 to multiply.</param>
	public static float[] Multiply3(in float[] v0, float[] v1) {
        float[] result = new float[3];
		for (int i=0; i<3; i++) {
			result[i] = v0[i] * v1[i];
		}
        return result;
	}

	/// <summary>Normalise a Float3 and return the result.</summary>
	/// <param name="v">Float3 to normalise.</param>
	public static float[] Normalise3(in float[] v) {
		float mag = Magnitude3(in v);
		return Divide3(in v, mag);
	}

	/// <summary>Normalise a Float3 in-place.</summary>
	/// <param name="v">Float3 to normalise.</param>
	public static void Normalise3IP(float[] v) {
		float mag = Magnitude3(in v);
		Divide3IP(v, mag);
	}

	/// <summary>Get the cross product between two Float3's.</summary>
	/// <param name="v0">Float3 0.</param>
	/// <param name="v1">Float3 1.</param>
	public static float[] Cross3(in float[] v0, in float[] v1) {
        float[] result = new float[3];
		result[0] = v0[1] * v1[2] - v0[2] * v1[1];
		result[1] = v0[2] * v1[0] - v0[0] * v1[2];
		result[2] = v0[0] * v1[1] - v0[1] * v1[0];
        return result;
	}

	/// <summary>Get the dot product between two Float3's.</summary>
	/// <param name="v0">Float3 0.</param>
	/// <param name="v1">Float3 1.</param>
	public static float Dot3(in float[] v0, in float[] v1) {
		return v0[0] * v1[0] + v0[1] * v1[1] + v0[2] * v1[2];
	}

	/// <summary>Get the dot product between a 3x3Matrix and a Float3.</summary>
	/// <param name="v0">Float3 0.</param>
	/// <param name="v1">Float3 1.</param>
	public static float[] Dot3(in float[,] A, in float[] v) {
		float[] result = new float[3];
		for (int i=0; i<3; i++) {
			for (int j=0; j<3; j++) {
				result[i] += A[i,j] * v[j];
			}
		}
		return result;
	}

	/// <summary>Get the dot product between a float3x3 and a float3.</summary>
	/// <param name="v0">float3 0.</param>
	/// <param name="v1">float3 1.</param>
	public static float3 Dot(in float3x3 A, in float3 v) {
		float3 result = new float3();
		for (int i=0; i<3; i++) {
			result[i] = math.dot(A[i], v);
		}
		return result;
	}

	/// <summary>Get the dot product between two arrays of floats.</summary>
	/// <param name="v0">Array 0.</param>
	/// <param name="v1">Array 1.</param>
	public static float Dot(in float[] v0, in float[] v1) {
		int length = v0.Length;
		if (v1.Length != length) {
			throw new System.Exception(
				string.Format("Length of v0 {0} and v1 {1} must match in Dot Product", length, v1.Length)
			);
		}
		float dot = 0f;
		for (int i=0; i<length; i++) {
			dot += v0[i] * v1[i];
		}
		return dot;
	}

	/// <summary>Get the signed angle in radians between two vectors using a reference vector.</summary>
	/// <param name="v0n">Normalised Vector 0.</param>
	/// <param name="v1n">Normalised Vector 1.</param>
	/// <param name="refV">Normalised Vector norm to the plane that v0n and v1n form.</param>
	public static float SignedAngleRad3(in float[] v0n, in float[] v1n, in float[] refV) {
		float[] cross = Cross3(v0n, v1n);
		float x = Dot3(v0n, v1n);
		float y = Dot3(refV, cross);
		return Mathf.Atan2(y, x);
	}

	/// <summary>Get the signed angle in radians between two vectors using a reference vector.</summary>
	/// <param name="v0n">Normalised Vector 0.</param>
	/// <param name="v1n">Normalised Vector 1.</param>
	/// <param name="refV">Normalised Vector norm to the plane that v0n and v1n form.</param>
	public static float SignedAngleRad(in float3 v0n, in float3 v1n, in float3 refV) {
		float3 cross = math.cross(v0n, v1n);
		float x = math.dot(v0n, v1n);
		float y = math.dot(refV, cross);
		return Mathf.Atan2(y, x);
	}

	/// <summary>Get the unsigned angle in radians between two vectors.</summary>
	/// <param name="v0n">Normalised Vector 0.</param>
	/// <param name="v1n">Normalised Vector 1.</param>
	public static float UnsignedAngleRad3(in float[] v0n, in float[] v1n) {
		float dot = Dot3(v0n, v1n);
		return Mathf.Acos(dot);
	}

	/// <summary>Get the unsigned angle in radians between two vectors.</summary>
	/// <param name="v0n">Normalised Vector 0.</param>
	/// <param name="v1n">Normalised Vector 1.</param>
	public static float UnsignedAngleRad(in float3 v0n, in float3 v1n) {
		float dot = math.dot(v0n, v1n);
		return Mathf.Acos(dot);
	}

	/////////////////////
	//Matrix Operations//
	/////////////////////

	/// <summary>Get the 3x3Matrix needed to rotate one normalised vector to another.</summary>
	/// <param name="v0n">Normalised Vector to rotate from.</param>
	/// <param name="v1n">Normalised Vector  to rotate from.</param>
	public static float[,] GetRotationMatrix(in float[] v0n, in float[] v1n) {
		//float[,] R = new float[3,3];
		//Interop.GetRotationMatrix(v0n, v1n, R);
		//return R;
		float[] v = Cross3(v0n, v1n);
		float s = Magnitude3(v);
		float c = Dot3(v0n, v1n);
		float m = ((1 - c) / (s * s));

		float[,] S = new float[3,3] {
			{0f, v[2], -v[1]},
			{-v[2], 0f, v[0]},
			{v[1], -v[0], 0f}
		};

		float[,] R = new float[3,3];
		for (int i=0; i<3; i++) {
			R[i,i] = 1f;
			for (int j=0; j<3; j++) {
				float x = 0;
				for (int k=0; k<3; k++) {
					x += S[k,i] * S[j,k];
				}
				R[i,j] += S[i,j] + m * x;
			}
		}
		return R;
	}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	/// <summary>Get the 3x3Matrix needed to rotate one normalised vector to another.</summary>
	/// <param name="v0n">Normalised Vector to rotate from.</param>
	/// <param name="v1n">Normalised Vector  to rotate from.</param>
	public static float3x3 GetRotationMatrix(in float3 v0n, in float3 v1n) {
		//float[,] R = new float[3,3];
		//Interop.GetRotationMatrix(v0n, v1n, R);
		//return R;
		float3 v = math.cross(v0n, v1n);
		float s = math.length(v);
		float c = math.dot(v0n, v1n);
		float m = ((1 - c) / (s * s));
		

		float3x3 S = new float3x3 (
			0f, v.z, -v.y,
			-v.z, 0f, v.x,
			v.y, -v.x, 0f
		);

		float3x3 R = new float3x3();
		for (int i=0; i<3; i++) {
			R[i][i] = 1f;
			for (int j=0; j<3; j++) {
				float x = 0;
				for (int k=0; k<3; k++) {
					x += S[k][i] * S[j][k];
				}
				R[i][j] += S[i][j] + m * x;
			}
		}
		return R;
	}


	/// <summary>Swap the rows and columns of a Matrix and return the result.</summary>
	/// <param name="A">Matrix to transpose.</param>
	public static float[,] Transpose(in float[,] A) {
		int A_l0 = A.GetLength(0);
		int A_l1 = A.GetLength(1);
		float[,] At = new float[A_l1, A_l0];
		for (int l0 = 0; l0 < A_l0; l0++) {
			for (int l1 = 0; l1 < A_l1; l1++) {
				At[l1, l0] = A[l0, l1];
			}
		}
		return At;
	}

	/// <summary>Return the Matrix Multiplication result of Transpose(A) and B.</summary>
	/// <param name="A">Matrix A, as a float[n, m].</param>
	/// <param name="B">Matrix B, as a float[n, m].</param>
	public static float[,] MatMul_T(in float[,] A, in float[,] B) {
		int A_l0 = A.GetLength(0);
		int A_l1 = A.GetLength(1);
		int B_l0 = B.GetLength(0);
		int B_l1 = B.GetLength(1);

		if (A_l0 != B_l0) {throw new System.Exception(string.Format("dim(0) of A ({0}) must match dim(0) of B ({1})", A_l0, B_l0));}
		if (A_l1 != B_l1) {throw new System.Exception(string.Format("dim(1) of A ({0}) must match dim(1) of B ({1})", A_l1, B_l1));}

		float[,] AtxB = new float[A_l0, A_l0];
		
		for (int l0 = 0; l0 < A_l0; l0++) {
			for (int l1 = 0; l1 < B_l1; l1++) {
				for (int i = 0; i < A_l1; i++) {
					AtxB[l0, l1] = AtxB[l0, l1] + A[i, l0] * B[i, l1];
				}
			}
		}
		
		return AtxB;
	}

	/// <summary>Return the Matrix Multiplication result of A and B.</summary>
	/// <param name="A">Matrix A, as a float[n, m].</param>
	/// <param name="B">Matrix B, as a float[m, p].</param>
	public static float[,] MatMul(in float[,] A, in float[,] B) {
		int A_l0 = A.GetLength(0);
		int A_l1 = A.GetLength(1);
		int B_l0 = B.GetLength(0);
		int B_l1 = B.GetLength(1);

		if (A_l1 != B_l0) {throw new System.Exception(string.Format("dim(1) of A ({0}) must match dim(0) of B ({1})", A_l1, B_l0));}

		float[,] AxB = new float[A_l0, B_l1];
		
		for (int l0 = 0; l0 < A_l0; l0++) {
			for (int l1 = 0; l1 < B_l1; l1++) {
				for (int i = 0; i < A_l1; i++) {
					AxB[l0, l1] = AxB[l0, l1] + A[l0, i] * B[i, l1];
				}
			}
		}
		return AxB;
	}

	public static float[,] Identity(int length) {
		float[,] I = new float[length, length];
		for (int i = 0; i < length; i++) {
			I[i,i] = 1f;
		}
		return I;
	}
	

	///<summary>Gets the vector that is norm to a plane defined by two direction vectors.</summary>
	/// <param name="v0">Input vector tangent to plane.</param>
	/// <param name="v1">Input vector tangent to plane.</param>
	/// <param name="norm">Output vector norm to plane.</param>
	/// <param name="tolerance">Lower bound on the tolerance of the length of the input vectors and their cross product - returns false if failed.</param>
	public static bool GetVectorNorm(float3 v0, float3 v1, out float3 norm, float tolerance=0.01f) {
		norm = float3.zero;

		//Test length of v0
        float r0 = math.length(v0);
		if (r0 < tolerance) {
			return false;
		}
		
		//Test length of v1
        float r1 = math.length(v1);
		if (r1 < tolerance) {
			return false;
		}

		//Normalise v0 and v1
		float3 v0n = v0 / r0;
		float3 v1n = v1 / r1;

		//Get cross product and its length
        norm = math.cross(v0n, v1n);
		float rnorm = math.length(norm);

		if (rnorm < tolerance) {
			//Input vectors are parallel - infinite solutions

			//Regenerate norm using arbitrary vector
            norm = math.cross(v0n, new float3(1f, 0f, 0f));
			rnorm = math.length(norm);

            if (rnorm < tolerance) {
				//Try once more
                norm = math.cross(v0n, new float3(0f, 1f, 0f));
				rnorm = math.length(norm);

                if (rnorm < tolerance) {
					norm = float3.zero;
                    return false;
                }
            }
        }

		norm /= rnorm;
		return true;

	}


	///////////////////
	//Icosphere Maths//
	///////////////////
	//
	// DEFINITIONS
	// 
	// Vertex     : A point on the icosphere in cartesian space ([x,y,z])
	// Vert       : One of the three components of a vertex (x, y, or z)
	// Triangle   : Three indices of an array of Vertices that make up a graphical triangle
	// Tri        : One of the three indices of a triangle
	// Resolution : The number of subdivisions in an icosphere
	// 
	// SIZES
	//
	// Vertices   = float[10 * 4^resolution + 2]
	// Verts      = float[30 * 4^resolution + 6]
	// Triangle   = float[20 * 4^resolution]
	// Tris       = float[60 * 4^resolution]
	//

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	/// <summary>Return the number of vertices in an icosphere given a resolution.</summary>
	/// <param name="resolution">Resolution (number of subdivisions) of the icosphere.</param>
	public static int VerticesPerSphere(int resolution) => CustomMathematics.IntPow(4, resolution) * 10 + 2;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	/// <summary>Return the number of triangles in an icosphere given a resolution.</summary>
	/// <param name="resolution">Resolution (number of subdivisions) of the icosphere.</param>
	public static int TrianglesPerSphere(int resolution) => CustomMathematics.IntPow(4, resolution) * 20;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	/// <summary>Return the number of verts in an icosphere given a resolution.</summary>
	/// <param name="resolution">Resolution (number of subdivisions) of the icosphere.</param>
	public static int VertsPerSphere(int resolution) => VerticesPerSphere(resolution) * 3;
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	/// <summary>Return the number of tris in an icosphere given a resolution.</summary>
	/// <param name="resolution">Resolution (number of subdivisions) of the icosphere.</param>
	public static int TrisPerSphere(int resolution) => TrianglesPerSphere(resolution) * 3;

	/// <summary>Return the point on an icosphere which is furthest from a list of existing points on the icosphere.</summary>
	/// <param name="sphereVerts">Array of n float3s of the n vertices of an icosphere.</param>
	/// <param name="hostPosition"></param>
	/// <param name="positions"></param>
	public static float3 GetBestPositionOnSphere(
		float3[] sphereVertices, 
		float3 hostPosition, 
		float3[] neighbourPositions
	) {

		int numVertices = sphereVertices.Length;
		int numNeighbours = neighbourPositions.Length;
		float[] positionScores = new float[numNeighbours];
		
		//Get the positions of the neighbours, where the host Atom is centred on the origin
		//This allows us to compare a unit vector to each neighbour position easily
		float3[] normalisedNeighbourPositions = Enumerable.Range(0, numNeighbours)
			//Loop through each neighbour
			.Select(neighbourIndex => math.normalize(
				//Subtract the position of the host
					neighbourPositions[neighbourIndex] - hostPosition
				)
			)
			//Convert to jagged array so it is only evaluated once
			.ToArray();

		//Create a dictionary of a score for each point on the sphere
		Dictionary<int, float> vertexScores = Enumerable
			// For each vertex...
			.Range(0, numVertices)
			// Make parallel
			.AsParallel()
			// Create the dictionary
			.ToDictionary(
				// Assign the Key to the vertex number
				x => x, 
				// The Value is a score calculated in GetVertexScoreOnSphere
				x => GetVertexScoreOnSphere(
					// Get the vertex position to test
					sphereVertices[x], 
					// Positions of neighbours already attached to the host atom
					normalisedNeighbourPositions, 
					// The number of neighbours already attached to the host atom
					numNeighbours
				)
			);


		//The best score is the index of the lowest score ie furthest position from all neighbouring points
		int best = IndexOfMin(vertexScores);

		//Return the direction that the new Atom should be relative to the host Atom
		return math.normalize(sphereVertices[best]);
	}

	/// <summary>Return the Vertex Score of a vertex given an array of neighbour positions</summary>
	/// <param name="vertex">Float3 position of the vertex to test.</param>
	/// <param name="normalisedNeighbourPositions">Jagged array of Float3, as a float[numNeighbours][3].</param>
	/// <param name="numNeighbours">The number of neighbours.</param>
	private static float GetVertexScoreOnSphere(float3 vertex, float3[] normalisedNeighbourPositions, int numNeighbours) {
		if (numNeighbours == 0) {
			throw new System.ArgumentException(string.Format(
				"numNeighbours ({0}) must be 1 or greater",
				numNeighbours
			));
		}
		return Enumerable.Range(0, numNeighbours)
			.Select(neighbourIndex => math.dot(
				vertex, 
				normalisedNeighbourPositions[neighbourIndex]
			))
			.Max();
	}

	//////////////////////
	//String Conversions//
	//////////////////////

	/// <summary>Convert an array of one type to a string</summary>
	public static string ToString<T>(T[] v) {
		return string.Format(
			"[ {0} ]",
			string.Join(" ", v)
		);
	}

	/// <summary>Convert a 2-dimensional array of one type to a string</summary>
	public static string ToString<T>(T[,] A) {
		int l0 = A.GetLength(0);
		int l1 = A.GetLength(1);
		return string.Format(
			"[ {0} ]",
			string.Join(
				"\n",
				Enumerable.Range(0, l0)
					.Select(i =>string.Format(
						"[ {0} ]",
						string.Join(" ", Enumerable.Range(0, l1).Select(j => A[i,j]))
					)
				)
			)
		);
	}

	/// <summary>Convert a jagged array of one type to a string</summary>
	public static string ToString<T>(T[][] A) {
		return string.Format(
			"[ {0} ]",
			string.Join(
				"\n",
				Enumerable.Range(0, A.Length)
					.Select(i =>string.Format(
						"[ {0} ]",
						string.Join(" ", Enumerable.Range(0, A[i].Length).Select(j => A[i][j]))
					)
				)
			)
		);
	}

	public static int GetCombinedHash(params int[] hashes) {
		if (ReferenceEquals(hashes, null)) {throw new System.ArgumentNullException("hashes");}
		int length = hashes.Length;
		if (length == 0) {throw new System.IndexOutOfRangeException();}
		int hash = hashes[0];
		for (int i=1; i<length; i++) {
			hash = GetCombinedHash(hash, hashes[i]);
		}
		return hash;
	}

	public static int GetCombinedHash(int hash0, int hash1) {
		return (hash0 << 6) + hash0 ^ hash1;
	}

	/// <summary>Get the binomial coefficients n choose k</summary>
	public static int Binomial(int n, int k) {
		if (k < 0 || k > n) {return 0;}
		if (k == 0 || k == n) {return 1;}
		k = Mathf.Min(k, n - k);
		float result = 1;
		for (int i=0; i<k; i++) {
			result *= (float)(n-i)/(i+1);
		}
		return Mathf.RoundToInt(result);
	}

	/// <summary>Get points along a Rational Bezier curve between a set of points</summary>
	/// <param name="points">The points to interpolate.</param>
	/// <param name="divisions">The number of segments to divide the curve into.</param>
	/// <param name="weights">
	/// How close the curve should approximate each point. 
	/// This list must have the same length as points. 
	/// A null argument will default to a weight of 1 for each point.
	/// </param>
	public static IEnumerable<float3> GetBezier(float3[] points, int divisions, float[] weights=null) {
		
		int order = points.Length - 1;

		if (weights == null) {
			weights = Enumerable.Repeat(1f, order + 1).ToArray();
		}

		if (points.Length != weights.Length) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Length of points must be the same as weights in Bezier calculation!"
			);
		}

		for (int i=0; i<divisions; i++) {
			float t = (float)i / divisions;

			float3 point = new float3();
			
			
			float[] scaleFactors = Enumerable.Range(0, order + 1)
				.Select(k => Binomial(order, k) * Mathf.Pow(1 - t, order - k) * Mathf.Pow(t, k) * weights[k])
				.ToArray();

			float vectorScale = 0f;
			for (int k=0; k<=order; k++) {
				vectorScale += scaleFactors[k];
				point += points[k] * scaleFactors[k];
			}

			point /= vectorScale;


			yield return point;
		}

		yield return points.Last();


	}

	/////////
	//ATOMS//
	/////////

	////////////////////////
	//Two Atom calculators//
	//////////////////////// 

	/// <summary>Get the Vector from atom0's position to atom1's position.</summary>
	/// <param name="atom0">Atom to measure Vector from.</param>
	/// <param name="atom1">Atom to measure Vector to.</param>
	public static float3 GetVector(Atom atom0, Atom atom1) {
		return atom0.position - atom1.position;
	}

	/// <summary>Get the normalised Vector from atom0's position to atom1's position.</summary>
	/// <param name="atom0">Atom to measure Vector from.</param>
	/// <param name="atom1">Atom to measure Vector to.</param>
	public static float3 GetNormalisedVector(Atom atom0, Atom atom1) {
		return math.normalizesafe(atom0.position - atom1.position);
	}

	/// <summary>Get the distance between atom0 and atom1.</summary>
	/// <param name="atom0">Atom to measure distance from.</param>
	/// <param name="atom1">Atom to measure distance to.</param>
	public static float GetDistance(Atom atom0, Atom atom1) {
		return math.distancesq(atom0.position, atom1.position);
	}

	/// <summary>Get the distance between atom0 and atom1.</summary>
	/// <param name="positions">Positions array.</param>
	/// <param name="index0">Index of atom0.</param>
	/// <param name="index1">Index of atom1.</param>
	public static float GetDistance(float3[] positions, int index0, int index1) {
		return math.distance(positions[index0], positions[index1]);
	}

	/// <summary>Get the distance squared between atom0 and atom1.</summary>
	/// <param name="atom0">Atom to measure distance from.</param>
	/// <param name="atom1">Atom to measure distance to.</param>
	public static float GetDistanceSquared(Atom atom0, Atom atom1) {
		return math.distancesq(atom0.position, atom1.position);
	}

	/// <summary>Get the distance squared between atom0 and atom1.</summary>
	/// <param name="positions">Positions array.</param>
	/// <param name="index0">Index of atom0.</param>
	/// <param name="index1">Index of atom1.</param>
	public static float GetDistanceSquared(float3[] positions, int index0, int index1) {
		return math.distancesq(positions[index0], positions[index1]);
	}

	/// <summary>Increase the distance between atom0 and atom1.</summary>
	/// <param name="atom0">Atom 0.</param>
	/// <param name="atom1">Atom 1.</param>
	/// <param name="amount">Amount to increase distance by.</param>
	/// <param name="pivot">The normalised position between atom0 and atom1 that will remain stationary during transformation.</param>
	public static void IncreaseDistance(Atom atom0, Atom atom1, float amount, float pivot=0.5f) {
		float3 vector = GetNormalisedVector(atom0, atom1);
		atom0.position += vector * amount * pivot;
		atom1.position += vector * amount * (pivot - 1f);
	}

	/// <summary>Set the distance between atom0 and atom1.</summary>
	/// <param name="atom0">Atom 0.</param>
	/// <param name="atom1">Atom 1.</param>
	/// <param name="newDistance">Value to set distance to.</param>
	/// <param name="pivot">The normalised position between atom0 and atom1 that will remain stationary during transformation.</param>
	public static void SetDistance(Atom atom0, Atom atom1, float newDistance, float pivot=0.5f) {
		float3 vector = GetVector(atom0, atom1);
		float magnitude = math.length(vector);
		float amount = (newDistance - magnitude) / magnitude;
		atom0.position += vector * amount * pivot;
		atom1.position += vector * amount * (pivot - 1f);
	}

	/// <summary>Get the Float3 Vector needed to set the distance between atom0 and atom1 to newDistance.</summary>
	/// <param name="atom0">Atom 0.</param>
	/// <param name="atom1">Atom 1.</param>
	/// <param name="newDistance">Value to set distance to.</param>
	/// <param name="pivot">The normalised position between atom0 and atom1 that will remain stationary during transformation.</param>
	public static float3 GetOffsetVectorFromNewDistance(Atom atom0, Atom atom1, float newDistance, float pivot=0.5f) {
		float3 vector = GetVector(atom0, atom1);
		float magnitude = math.length(vector);
		float modifyAmount = (newDistance - magnitude) / magnitude;
		return vector * modifyAmount * pivot;
	}

	/// <summary>Scale the distance between atom0 and atom1 by scaleFactor.</summary>
	/// <param name="atom0">Atom 0.</param>
	/// <param name="atom1">Atom 1.</param>
	/// <param name="scaleFactor">Value to scale distance by.</param>
	/// <param name="pivot">The normalised position between atom0 and atom1 that will remain stationary during transformation.</param>
	public static void ScaleDistance(Atom atom0, Atom atom1, float scaleFactor, float pivot=0.5f) {
		float3 vector = GetVector(atom0, atom1);
		float amount = (scaleFactor - 1f) * math.length(vector);
		atom0.position += vector * amount * pivot;
		atom1.position += vector * amount * (pivot - 1f);
	}

	//////////////////////////
	//Three Atom calculators//
	//////////////////////////

	/// <summary>Get the angle in Radians between three atoms, centred on atom1.</summary>
	/// <param name="atom0">Atom 0.</param>
	/// <param name="atom1">Atom 1 - central atom.</param>
	/// <param name="atom2">Atom 2.</param>
	public static float GetAngle(Atom atom0, Atom atom1, Atom atom2) {
		return Angle(
			GetVector(atom0, atom1),
			GetVector(atom2, atom1)
		);
	}

	/// <summary>Get the angle in Radians between the first three Atoms in a list of Atoms.</summary>
	/// <param name="atoms">Atoms to measure angle of.</param>
	public static float GetAngle(List<Atom> atoms) {
		if (atoms.Count != 3) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Cannot get Angle - wrong number of atoms in input ({0}). Should be 3",
				atoms.Count
			);
		}
		return GetAngle(atoms[0], atoms[1], atoms[2]);
	}

	/// <summary>Get the distance between atom0 and atom1.</summary>
	/// <param name="positions">Positions array.</param>
	/// <param name="index0">Index of atom0.</param>
	/// <param name="index1">Index of atom1.</param>
	/// <param name="index2">Index of atom2.</param>
	public static float GetAngle(float3[] positions, int index0, int index1, int index2) {
		return Angle(positions[index0] - positions[index1], positions[index2] - positions[index1]);
	}

	/////////////////////////
	//Four Atom calculators//
	/////////////////////////

	/// <summary>Get the angle in Radians between four atoms, centred on the bond between atom1 and atom2.</summary>
	/// <param name="atom0">Atom 0.</param>
	/// <param name="atom1">Atom 1 - central atom.</param>
	/// <param name="atom2">Atom 2 - central atom.</param>
	/// <param name="atom3">Atom 3.</param>
	public static float GetDihedral(Atom atom0, Atom atom1, Atom atom2, Atom atom3) {
		//Normalised vector between atom0 and atom1
		float3 v01 = GetNormalisedVector(atom0, atom1);
		//Normalised vector between atom2 and atom1
		float3 v21 = GetNormalisedVector(atom2, atom1);
		//Normalised vector between atom3 and atom2
		float3 v32 = GetNormalisedVector(atom3, atom2);

		//Project vectors to so the normals are compared
		float3 w01 = v01 - v21 * math.dot(v01, v21);
		float3 w32 = v32 - v21 * math.dot(v32, v21);

		return SignedAngleRad(w01, w32, v21);
	}
	
	/// <summary>Get the dihedral angle between the first four Atoms in a list of Atoms.</summary>
	/// <param name="atoms">Atoms to measure dihedral angle of.</param>
	public static float GetDihedral(List<Atom> atoms) {
		if (atoms.Count != 4) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Cannot get Dihedral - wrong number of atoms in input {0}",
				atoms.Count
			);
		}
		return GetDihedral(atoms[0], atoms[1], atoms[2], atoms[3]);
	}

	/// <summary>Get the angle in Radians between four atoms, centred on the bond between atom1 and atom2.</summary>
	/// <param name="positions">Positions array.</param>
	/// <param name="index0">Index of atom0.</param>
	/// <param name="index1">Index of atom1.</param>
	/// <param name="index2">Index of atom2.</param>
	/// <param name="index3">Index of atom3.</param>
	public static float GetDihedral(float3[] positions, int index0, int index1, int index2, int index3) {
		//Normalised vector between atom0 and atom1
		float3 v01 = math.normalizesafe(positions[index0] - positions[index1]);
		//Normalised vector between atom2 and atom1
		float3 v21 = math.normalizesafe(positions[index2] - positions[index1]);
		//Normalised vector between atom3 and atom2
		float3 v32 = math.normalizesafe(positions[index3] - positions[index2]);

		//Project vectors to so the normals are compared
		float3 w01 = v01 - (v21 * math.dot(v01, v21));
		float3 w32 = v32 - (v21 * math.dot(v32, v21));

		return SignedAngleRad(w01, w32, v21);
	}


	public class AngleRuler {
		public float3 p0;
		public float3 p1;
		public float3 p2;

		public float3 v01n;
		public float3 v21n;

		public float angle;

		public float3 norm => math.cross(v01n, v21n);

		public AngleRuler(Atom atom0, Atom atom1, Atom atom2) {
			p0 = atom0.position;
			p1 = atom1.position;
			p2 = atom2.position;

			v01n = math.normalize(p1 - p0);
			v21n = math.normalize(p1 - p2);

			angle = AngleNorm(v01n, v21n);
		}
	}

	public class DihedralRuler {

		/// <summary>The position of Atom 0.</summary>
		public float3 p0;
		/// <summary>The position of Atom 1.</summary>
		public float3 p1;
		/// <summary>The position of Atom 2.</summary>
		public float3 p2;
		/// <summary>The position of Atom 3.</summary>
		public float3 p3;

		/// <summary>The vector from Atom 0 to Atom 1.</summary>
		public float3 v01;
		/// <summary>The normalised vector from Atom 2 to Atom 1.</summary>
		public float3 v21n;
		/// <summary>The vector from Atom 3 to Atom 2.</summary>
		public float3 v32;
		
		/// <summary>The vector from Atom 0 to Atom 1 projected onto the plane norm to v21n.</summary>
		public float3 w01;
		/// <summary>The vector from Atom 3 to Atom 2 projected onto the plane norm to v21n.</summary>
		public float3 w32;

		/// <summary>The dihedral in radians.</summary>
		public float dihedral;

		public DihedralRuler(Atom atom0, Atom atom1, Atom atom2, Atom atom3) {
			p0 = atom0.position;
			p1 = atom1.position;
			p2 = atom2.position;
			p3 = atom3.position;

			v01 = p1 - p0;
			v21n = math.normalize(p1 - p2);
			v32 = p2 - p3;

			w01 = v01 - v21n * math.dot(v01, v21n);
			w32 = v32 - v21n * math.dot(v32, v21n);

			dihedral = SignedAngleRad(w01, w32, v21n);
		}

	}




	////////////////
	//Force Fields//
	////////////////

	/// <summary>Get the Stretching Energy.</summary>
	/// <param name="deltaLength">The difference between length of the bond and the equilibrium distance.</param>
	/// <param name="keq">The force constant.</param>
	/// <param name="req">The equibrium distance.</param>
	/// <param name="order">The order of the derivative.</param>
	public static float EStretch(float deltaLength, float keq, int order) {
		switch (order) {
			case (0): return      keq * deltaLength * deltaLength;
			case (1): return 2f * keq * deltaLength;
			case (2): return 2f * keq;
			default:  return 0f;
		}
	}
	

	/// <summary>Get the Stretching Energy.</summary>
	/// <param name="deltaLength">The difference between length of the bond and the equilibrium distance.</param>
	/// <param name="keq">The force constant.</param>
	/// <param name="energies">The resulting list containing the 0th, 1st and 2nd derivatives of the energy.</param>
	public static void EStretch(float deltaLength, float keq, float[] energies) {
		if (keq == 0f) {return;}

		energies[2] += 2f * keq;
		energies[1] += energies[2] * deltaLength;
		energies[0] += energies[1] * deltaLength * 0.5f;

	}
	
	/// <summary>Get the Van der Waals Energy.</summary>
	/// <param name="length">The distance between two non-bonded atoms.</param>
	/// <param name="v">The energy well depth.</param>
	/// <param name="req">The equibrium distance.</param>
	/// <param name="order">The order of the derivative.</param>
	public static float EVdWAmber(float length, float v, float req, int order) {
		if (v == 0) {return 0f;}
		float _r = req / length;
		switch (order) {
			case (0):
				return v * (
					Mathf.Pow (_r, 12f) 
					- 2f * Mathf.Pow (_r, 6f)
				);
			case (1):
				return 12f * v * (
					Mathf.Pow (_r, 7f) 
					- Mathf.Pow (_r, 13f)
				) / length;
			case (2):
				return 12f * v * (
					   13f * Mathf.Pow (_r, 14f)
					  - 7f * Mathf.Pow (_r, 8f)
				) / (length * length);
			default:
				return 0f;
		}
	}

	/// <summary>Get the Van der Waals Energy.</summary>
	/// <param name="length">The distance between two non-bonded atoms.</param>
	/// <param name="v">The energy well depth.</param>
	/// <param name="req">The equibrium distance.</param>
	/// <param name="energies">The resulting list containing the 0th, 1st and 2nd derivatives of the energy.</param>
	public static void EVdWAmber(float length, float v, float req, float[] energies){
		if (v == 0) {
			return;
		}
		float _r = req / length;
		float _r2 = _r * _r;
		float _r6 = _r2 * _r2 * _r2;
		float _r12 = _r6 * _r6;

		energies[0] += v *       (      _r12 - 2  * _r6)                    ;
		energies[1] += v * 12f * (    - _r12 +      _r6) / (length         );
		energies[2] += v * 12f * (13f * _r12 - 7f * _r6) / (length * length);
	}

	/// <summary>Get the Van der Waals Energy using distances squared.</summary>
	/// <param name="length_squared">The distance squared between two non-bonded atoms.</param>
	/// <param name="v">The energy well depth.</param>
	/// <param name="req_squared">The equibrium distance squared.</param>
	public static float EVdWAmberSquared(float length_squared, float v, float req_squared){
		if (v == 0) {
			return 0;
		}

		float _r2 = req_squared / length_squared;
		float _r6 = _r2 * _r2 * _r2;
		float _r12 = _r6 * _r6;

		return v * (_r12 - 2  * _r6);
	}

	/// <summary>Get the Electrostatics Energy between two atoms.</summary>
	/// <param name="length">The distance between two non-bonded atoms.</param>
	/// <param name="coulombFactor">The product of the atoms' partial charges and scale factor divided by the dielectric constant.</param>
	/// <param name="cType">The charge type. 1: 1/(R). 2: 1/(R*R).</param>
	/// <param name="order">The order of the derivative.</param>
	public static float EElectrostatic(float length, float coulombFactor, CT cType, int order) {

		//No coulomb term
		if (cType == 0 || coulombFactor == 0f) {
			return 0f;
		} 
		
		switch (cType) {
			// Amber style 1/R
			case (CT.INVERSE):
				switch (order) {
					case (0): return      coulombFactor / (length);
					case (1): return     -coulombFactor / (length * length);
					case (2): return 2f * coulombFactor / (length * length * length);
					default:  return 0f;
				}
			case (CT.INVERSE_SQUARED):
			// 1/(R*R)
				switch (order) {
					case (0): return       coulombFactor / (length * length);
					case (1): return -2f * coulombFactor / (length * length * length);
					case (2): return  6f * coulombFactor / (length * length * length * length);
					default:  return  0f;
				}
			default: return 0f;
		}
	}

	/// <summary>Get the 1/(R) Electrostatic Energy between two atoms.</summary>
	/// <param name="length">The distance between two non-bonded atoms.</param>
	/// <param name="coulombFactor">The product of the atoms' partial charges and scale factor divided by the dielectric constant.</param>
	/// <param name="energies">The resulting list containing the 0th, 1st and 2nd derivatives of the energy.</param>
	public static void EElectrostaticR1(float length, float coulombFactor, float[] energies) {
		
		if (coulombFactor == 0) {
			return;
		}
		float _r = 1f / length;
		energies[0] += coulombFactor * _r;
		energies[1] -= energies[0] * _r;
		energies[2] -= 2f * energies[1] * _r;
	}

	/// <summary>Get the 1/(R*R) Electrostatic Energy between two atoms.</summary>
	/// <param name="length">The distance between two non-bonded atoms.</param>
	/// <param name="coulombFactor">The product of the atoms' partial charges and scale factor divided by the dielectric constant.</param>
	/// <param name="energies">The resulting list containing the 0th, 1st and 2nd derivatives of the energy.</param>
	public static void EElectrostaticR2(float r, float coulombFactor, float[] energies) {
		
		if (coulombFactor == 0) {
			return;
		}
		float _r = 1f / r;
		energies[0] += coulombFactor * _r * _r;
		energies[1] -= 2f * energies[0] * _r;
		energies[2] -= 3f * energies[1] * _r;
	}

	/// <summary>Get the 1/(R) Electrostatic Energy between two atoms using distance squared.</summary>
	/// <param name="length">The distance squared between two non-bonded atoms.</param>
	/// <param name="coulombFactor">The product of the atoms' partial charges and scale factor divided by the dielectric constant.</param>
	public static float EElectrostaticR1Squared(float length_squared, float coulombFactor) {
		
		if (coulombFactor == 0) {
			return 0;
		}
		return coulombFactor / math.sqrt(length_squared);
	}


	/// <summary>Get the 1/(R*R) Electrostatic Energy between two atoms using distance squared.</summary>
	/// <param name="length">The distance squared between two non-bonded atoms.</param>
	/// <param name="coulombFactor">The product of the atoms' partial charges and scale factor divided by the dielectric constant.</param>
	public static float EElectrostaticR2Squared(float length_squared, float coulombFactor) {
		
		if (coulombFactor == 0) {
			return 0;
		}
		return coulombFactor / length_squared;
	}
	/// <summary>Get the Bend Energy between 3 atoms.</summary>
	/// <param name="deltaAngle">The difference between the current angle and the equilibrium angle.</param>
	/// <param name="q0q1">The product of the atoms' partial charges.</param>
	/// <param name="eps">The Dielectric constant.</param>
	/// <param name="energies">The resulting list containing the 0th, 1st and 2nd derivatives of the energy.</param>
	public static void EBend(float deltaAngle, float keq, float[] energies) {
		if (keq == 0f) {
			return;
		}
		energies[2] += 2f * keq;
		energies[1] += energies[2] * deltaAngle;
		energies[0] += energies[1] * deltaAngle * 0.5f;
	}

	/// <summary>Get the Improper Torsion Energy between 4 atoms.</summary>
	/// <param name="dihedral">The dihedral angle between the 4 atoms.</param>
	/// <param name="v">The barrier height of the Improper Torsion.</param>
	/// <param name="gamma">The phase offset of the trigonometric function representing the torsion.</param>
	/// <param name="periodicity">The periodicity of the trigonometric function representing the torsion.</param>
	/// <param name="order">The order of the derivative.</param>
	public static float EImproperTorsion(float dihedral, float v, float gamma, float periodicity, int order) {
		if (v == 0f) {
			return 0f;
		}

		float t = periodicity * dihedral - gamma;
		switch (order) {
			case (0): return 0.5f * v * (1f + Mathf.Cos(t));
			case (1): return - 0.5f * v * periodicity * (Mathf.Sin (t));
			case (2): return -0.5f * v * periodicity * periodicity * (Mathf.Cos (t));
			default:  return 0;
		}
	}

	/// <summary>Get the Improper Torsion Energy between 4 atoms.</summary>
	/// <param name="dihedral">The dihedral angle between the 4 atoms.</param>
	/// <param name="v">The barrier height of the Improper Torsion.</param>
	/// <param name="gamma">The phase offset of the trigonometric function representing the torsion.</param>
	/// <param name="periodicity">The periodicity of the trigonometric function representing the torsion.</param>
	/// <param name="energies">The resulting list containing the 0th, 1st and 2nd derivatives of the energy.</param>
	public static void EImproperTorsion(float dihedral, float v, float gamma, float periodicity, float[] energies) {
		if (v == 0f) {
			return;
		}
		float t = periodicity * dihedral - gamma;
		v *= 0.5f;
		energies[0] += v * (1f + Mathf.Cos(t));
		energies[1] -= v * periodicity * (Mathf.Sin (t));
		energies[2] -= v * periodicity * periodicity * (Mathf.Cos (t));
	}

	/// <summary>Get the Torsion Energy between 4 atoms.</summary>
	/// <param name="dihedral">The dihedral angle between the 4 atoms.</param>
	/// <param name="barrierHeights">The barrier heights of the Torsion</param>
	/// <param name="phaseOffsets">The phase offset of the Torsion.</param>
	/// <param name="energies">The resulting list containing the 0th, 1st and 2nd derivatives of the energy.</param>
	public static void ETorsion(float dihedral, float[] barrierHeights, float[] phaseOffsets, float[] energies) {
		EImproperTorsion(dihedral, barrierHeights[0], phaseOffsets[0], 1f, energies);
		EImproperTorsion(dihedral, barrierHeights[1], phaseOffsets[1], 2f, energies);
		EImproperTorsion(dihedral, barrierHeights[2], phaseOffsets[2], 3f, energies);
		EImproperTorsion(dihedral, barrierHeights[3], phaseOffsets[3], 4f, energies);
	}

	/// <summary>Get the Torsion Energy between 4 atoms.</summary>
	/// <param name="dihedral">The dihedral angle between the 4 atoms.</param>
	/// <param name="barrierHeights">The barrier heights of the Torsion</param>
	/// <param name="phaseOffsets">The phase offset of the Torsion.</param>
	/// <param name="order">The order of the derivative.</param>
	public static float ETorsion(float dihedral, float[] barrierHeights, float[] phaseOffsets, int order) {
		float energy = 0f;
		energy += EImproperTorsion(dihedral, barrierHeights[0], phaseOffsets[0], 1f, order);
		energy += EImproperTorsion(dihedral, barrierHeights[1], phaseOffsets[1], 2f, order);
		energy += EImproperTorsion(dihedral, barrierHeights[2], phaseOffsets[2], 3f, order);
		energy += EImproperTorsion(dihedral, barrierHeights[3], phaseOffsets[3], 4f, order);
		return energy;
	}


}
