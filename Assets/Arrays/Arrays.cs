using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Linq;
using BT = Constants.BondType;
using System.Runtime.InteropServices;

public static class ArrayInterop {
//	private const string dllName = "Arrays";
//	private const CallingConvention cc = CallingConvention.Cdecl;
//	private const CharSet cs = CharSet.Ansi;
//
//	[DllImport(
//			dllName, CallingConvention = CallingConvention.Cdecl,
//			EntryPoint = "__arraysmod_MOD_distance_cs", CharSet = CharSet.Ansi)
//	]
//	public static extern float get_distance(
//		[In] float[] positions_array, 
//		ref int index0, 
//		ref int index1
//	);
//
//	[DllImport(
//			dllName, CallingConvention = CallingConvention.Cdecl,
//			EntryPoint = "__arraysmod_MOD_get_distance_matrix", CharSet = CharSet.Ansi)
//	]
//	public static extern void get_distance_matrix(
//		[In] float[] positions_array, 
//		ref int size, 
//		[In, Out] float[,] distance_matrix
//	);
//
//}
//
//public class StringArray {
//
//	/*
//		STRING ARRAY CLASS
//
//	*/
//
//	private string[] _array;
//
//	private int _size;
//	public int size {
//		get {return _size;}
//	}
//
//	public StringArray(int size) {
//		_array = new string[size];
//		_size = size;
//	}
//
//	public StringArray(string[] array) {
//		_array = array;
//		_size = array.Length;
//		
//	}
//
//	public string[] ToArray() {
//		return _array.Clone() as string[];
//	}
//
//	public void FromArray(string[] array) {
//		if (array.Length != size) 
//			throw new System.IndexOutOfRangeException( string.Format(
//				"Wrong size for array: ({0} != {1})",
//				array.Length,
//				size
//			));
//		_array = array;
//	}
//
//	public string this[int index] {
//		get {
//			return _array[WrappedIndex(index)];
//		}
//		set {
//			_array[WrappedIndex(index)] = value;
//		}
//	}
//
//	public string[] this[int[] indices] {
//		get {
//			string[] array = new string[indices.Length];
//			for (int index=0; index<indices.Length; index++) {
//				array[index] = this[indices[index]];
//			}
//			return array;
//		}
//		set {
//			for (int index=0; index<indices.Length; index++) {
//				this[indices[index]] = value[index];
//			}
//		}
//	}
//
//	private int WrappedIndex(int index) {
//		int i = index;
//		if (i < 0) i += size;
//		if (i < 0 || i > size) throw new System.IndexOutOfRangeException();
//		return i;
//	}
//
//	public void Resize(int newSize) {
//		string[] oldArray = _array.Clone() as string[];
//		this._array = new string[newSize];
//		CopyArray(oldArray, this._array, _size);
//		this._size = newSize;
//	}
//
//	public void Resize(int count, int insertIndex) {
//		// Resize the array by inserting blank items
//
//		int newSize = count + size;
//		int i = WrappedIndex(insertIndex);
//
//		//Make temp copy of _array
//		string[] oldArray = _array.Clone() as string[];
//		_array = new string[newSize];
//
//		//Copy elements before insertIndex
//		CopyArray(oldArray, _array, 0, 0, i);
//		//Copy elements after insertIndex
//		CopyArray(oldArray, _array, i, i + count, newSize - i - count);
//		this._size = newSize;
//
//	}
//
//	public void Add(string item) {
//		Resize(size + 1);
//		this[_size - 1] = item;
//	}
//
//	public void Insert(string item, int insertIndex) {
//		int w = WrappedIndex(insertIndex);
//		Resize(1, w);
//		this[w] = item;
//	}
//
//	public void Insert(StringArray intArray, int insertIndex) {
//		int count = intArray.size;
//		int w = WrappedIndex(insertIndex);
//		Resize(count, w);
//		CopyArray(intArray._array, _array, 0, w, count);
//	}
//	private void CopyArray(string[] oldA, string[] newA, int size) {
//		for (int i = 0; i < size; i++) newA[i] = oldA[i];
//	}
//
//	private void CopyArray(string[] oldA, string[] newA, int oldStartIndex, int newStartIndex, int count) {
//		for (int i = 0; i < count; i++) 
//			newA[newStartIndex + i] = oldA[oldStartIndex + i];
//	}
//}
//
//public class IntArray {
//
//	/*
//		INT ARRAY CLASS
//		
//	*/
//
//	private int[] _array;
//
//	private int _size;
//	public int size {
//		get {return _size;}
//	}
//
//	public IntArray(int size) {
//		_array = new int[size];
//		_size = size;
//	}
//
//	public IntArray(int[] array) {
//		_array = array;
//		_size = array.Length;
//	}
//
//	public int[] ToArray() {
//		return _array.Clone() as int[];
//	}
//
//	public void FromArray(int[] array) {
//		if (array.Length != size) 
//			throw new System.IndexOutOfRangeException( string.Format(
//				"Wrong size for array: ({0} != {1})",
//				array.Length,
//				size
//			));
//		_array = array;
//	}
//
//	public int this[int index] {
//		get {
//			return _array[WrappedIndex(index)];
//		}
//		set {
//			_array[WrappedIndex(index)] = value;
//		}
//	}
//
//	public int[] this[int[] indices] {
//		get {
//			int[] array = new int[indices.Length];
//			for (int index=0; index<indices.Length; index++) {
//				array[index] = this[indices[index]];
//			}
//			return array;
//		}
//		set {
//			for (int index=0; index<indices.Length; index++) {
//				this[indices[index]] = value[index];
//			}
//		}
//	}
//
//	private int WrappedIndex(int index) {
//		int i = index;
//		if (i < 0) i += size;
//		if (i < 0 || i > size) throw new System.IndexOutOfRangeException();
//		return i;
//	}
//
//	public void Resize(int newSize) {
//		int[] oldArray = _array.Clone() as int[];
//		this._array = new int[newSize];
//		CopyArray(oldArray, this._array, _size);
//		this._size = newSize;
//	}
//
//	public void Resize(int count, int insertIndex) {
//		// Resize the array by inserting blank items
//
//		int newSize = count + size;
//		int i = WrappedIndex(insertIndex);
//
//		//Make temp copy of _array
//		int[] oldArray = _array.Clone() as int[];
//		_array = new int[newSize];
//
//		//Copy elements before insertIndex
//		CopyArray(oldArray, _array, 0, 0, i);
//		//Copy elements after insertIndex
//		CopyArray(oldArray, _array, i, i + count, newSize - i - count);
//		this._size = newSize;
//
//	}
//
//	public void Add(int item) {
//		Resize(_size + 1);
//		this[_size - 1] = item;
//	}
//
//	public void Insert(int item, int insertIndex) {
//		int w = WrappedIndex(insertIndex);
//		Resize(1, w);
//		this[w] = item;
//	}
//
//	public void Insert(IntArray intArray, int insertIndex) {
//		int count = intArray.size;
//		int w = WrappedIndex(insertIndex);
//		Resize(count, w);
//		CopyArray(intArray._array, _array, 0, w, count);
//	}
//
//	private void CopyArray(int[] oldA, int[] newA, int size) {
//		for (int i = 0; i < size; i++) newA[i] = oldA[i];
//	}
//
//	private void CopyArray(int[] oldA, int[] newA, int oldStartIndex, int newStartIndex, int count) {
//		for (int i = 0; i < count; i++) 
//			newA[newStartIndex + i] = oldA[oldStartIndex + i];
//	}
//}
//
//public class FloatArray {
//
//	/*
//		FLOAT ARRAY CLASS
//		
//	*/
//
//	private float[] _array;
//
//	private int _size;
//	public int size {
//		get {return _size;}
//	}
//
//	public FloatArray(int size) {
//		_array = new float[size];
//		_size = size;
//	}
//
//	public FloatArray(float[] array) {
//		_array = array;
//		_size = array.Length;
//	}
//
//	public float[] ToArray() {
//		return _array.Clone() as float[];
//	}
//
//	public void FromArray(float[] array) {
//		if (array.Length != size) 
//			throw new System.IndexOutOfRangeException( string.Format(
//				"Wrong size for array: ({0} != {1})",
//				array.Length,
//				size
//			));
//		_array = array;
//	}
//
//	public float this[int index] {
//		get {
//			return _array[WrappedIndex(index)];
//		}
//		set {
//			_array[WrappedIndex(index)] = value;
//		}
//	}
//
//	public float[] this[int[] indices] {
//		get {
//			float[] array = new float[indices.Length];
//			for (int index=0; index<indices.Length; index++) {
//				array[index] = this[indices[index]];
//			}
//			return array;
//		}
//		set {
//			for (int index=0; index<indices.Length; index++) {
//				this[indices[index]] = value[index];
//			}
//		}
//	}
//
//	private int WrappedIndex(int index) {
//		int i = index;
//		if (i < 0) i += size;
//		if (i < 0 || i > size) throw new System.IndexOutOfRangeException();
//		return i;
//	}
//
//	public void Resize(int newSize) {
//		float[] oldArray = _array.Clone() as float[];
//		this._array = new float[newSize];
//		CopyArray(oldArray, this._array, _size);
//		this._size = newSize;
//	}
//
//	public void Resize(int count, int insertIndex) {
//		// Resize the array by inserting blank items
//
//		int newSize = count + size;
//		int i = WrappedIndex(insertIndex);
//
//		//Make temp copy of _array
//		float[] oldArray = _array.Clone() as float[];
//		_array = new float[newSize];
//
//		//Copy elements before insertIndex
//		CopyArray(oldArray, _array, 0, 0, i);
//		//Copy elements after insertIndex
//		CopyArray(oldArray, _array, i, i + count, newSize - i - count);
//		this._size = newSize;
//
//	}
//
//	public void Add(float item) {
//		Resize(_size + 1);
//		this[_size - 1] = item;
//	}
//
//	public void Insert(float item, int insertIndex) {
//		int w = WrappedIndex(insertIndex);
//		Resize(1, w);
//		this[w] = item;
//	}
//
//	public void Insert(FloatArray floatArray, int insertIndex) {
//		int count = floatArray.size;
//		int w = WrappedIndex(insertIndex);
//		Resize(count, w);
//		CopyArray(floatArray._array, _array, 0, w, count);
//	}
//
//	private void CopyArray(float[] oldA, float[] newA, int size) {
//		for (int i = 0; i < size; i++) newA[i] = oldA[i];
//	}
//
//	private void CopyArray(float[] oldA, float[] newA, int oldStartIndex, int newStartIndex, int count) {
//		for (int i = 0; i < count; i++) 
//			newA[newStartIndex + i] = oldA[oldStartIndex + i];
//	}
//}
//
//public class VectorArray {
//
//	/*
//		VECTOR ARRAY CLASS
//		
//		A = [
//		// j=0     1     2
//			[a0_0, a0_1, a0_2], // index = 0
//			[a1_0, a1_1, a1_2], // index = 1
//			...
//			[an_0, an_1, an_2]  // index = n
//		]
//
//		store as 1D array
//
//		// i=     0     1     2     3     4     5        n*3
//		_array = [a0_0, a0_1, a0_2, a1_0, a1_1, a1_2 ... an_2]
//
//		i = index * 3 + j
//
//	*/
//	
//	private float[] _array;
//
//	private int _size;
//	public int size {
//		get {return _size;}
//	}
//
//	public VectorArray(int size) {
//		_size = size;
//		_array = new float[size * 3];
//	}
//
//	public VectorArray(float[,] array) {
//		_size = array.Length;
//		_array = new float[size * 3];
//		CopyArray(array, _array, _size);
//	}
//
//	public VectorArray(float[] array) {
//		_size = array.Length / 3;
//		_array = new float[size * 3];
//		CopyArray(array, _array, _size);
//	}
//
//	public float[,] ToArray() {
//		float[,] array = new float[size, 3];
//		
//		for (int i = 0; i < _size; i++) {
//			for (int j = 0; j < 3; j++) {
//				array[i, j] = _array[i * 3 + j];
//			}
//		}
//		return array;
//	}
//
//	public void FromArray(float[,] array) {
//		if (array.GetLength(0) != size) 
//			throw new System.IndexOutOfRangeException( string.Format(
//				"Wrong size for array: ({0} != {1})",
//				array.Length,
//				size
//			));
//		for (int i = 0; i < _size; i++) {
//			for (int j = 0; j < 3; j++) {
//				_array[i * 3 + j] = array[i, j];
//			}
//		}
//	}
//
//	public List<Vector3> ToList() {
//		List<Vector3> list = new List<Vector3>();
//
//		for (int index = 0; index < size; index++)
//			list.Add(GetVector(index));
//
//		return list;
//	}
//
//	public void FromList(List<Vector3> list) {
//		
//	}
//
//	public float[] this[int index] {
//		get {
//			if (index > size) throw new System.IndexOutOfRangeException();
//			int i = index * 3;
//			return new float[3] {
//				_array[i++],
//				_array[i++],
//				_array[i]
//			};
//		}
//		set {
//			if (index > size) throw new System.IndexOutOfRangeException();
//			int i = index * 3;
//			_array[i++] = value[0];
//			_array[i++] = value[1];
//			_array[i] = value[2];
//		}
//	}
//
//	public VectorArray this[List<int> indices] {
//		get {
//			int newSize = indices.Count;
//			VectorArray vectorArray = new VectorArray(newSize);
//			for (int i = 0; i < newSize; i++) {
//				vectorArray[i] = this[WrappedIndex(indices[i])];
//			}
//			return vectorArray;
//		}
//		set {
//			int newSize = indices.Count;
//			VectorArray vectorArray = new VectorArray(newSize);
//			for (int i = 0; i < newSize; i++) {
//				this[WrappedIndex(indices[i])] = vectorArray[i];
//			}
//		}
//	}
//
//	public Vector3 GetVector(int index) {
//		float[] item = this[index];
//		return new Vector3(item[0], item[1], item[2]);
//	}
//
//	public float GetDistance(int index0, int index1) {
//		return ArrayInterop.get_distance(_array, ref index0, ref index1);
//	}
//
//	public float[,] GetDistanceMatrix() {
//		float[,] distance_matrix = new float[_size, _size];
//		ArrayInterop.get_distance_matrix(_array, ref _size, distance_matrix);
//		return distance_matrix;
//	}
//
//	private int WrappedIndex(int index) {
//		int i = index;
//		if (i < 0) i += size;
//		if (i < 0 || i > size) throw new System.IndexOutOfRangeException();
//		return i;
//	}
//
//	public void Resize(int newSize) {
//		float[] oldArray = _array.Clone() as float[];
//		_array = new float[newSize * 3];
//		CopyArray(oldArray, _array, _size);
//		_size = newSize;
//	}
//
//	public void Resize(int count, int insertIndex) {
//		// Resize the array by inserting blank items
//
//		int newSize = count + size;
//		int i = WrappedIndex(insertIndex);
//
//		//Make temp copy of _array
//		float[] oldArray = _array.Clone() as float[];
//		_array = new float[newSize * 3];
//
//		//Copy elements before insertIndex
//		CopyArray(oldArray, _array, 0, 0, i);
//		//Copy elements after insertIndex
//		CopyArray(oldArray, _array, i, i + count, newSize - i - count);
//		this._size = newSize;
//
//	}
//
//	public void Add(float[] item) {
//		Resize(_size + 1);
//		this[_size - 1] = item;
//	}
//
//	public void Insert(float[] item, int insertIndex) {
//		int w = WrappedIndex(insertIndex);
//		Resize(1, w);
//		this[w] = item;
//	}
//
//	public void Insert(VectorArray vectorArray, int insertIndex) {
//		int count = vectorArray.size;
//		int w = WrappedIndex(insertIndex);
//		Resize(count, w);
//		CopyArray(vectorArray._array, _array, 0, w, count);
//	}
//
//	private void CopyArray(float[] oldA, float[] newA, int size) {
//		for (int i = 0; i < size * 3; i++) newA[i] = oldA[i];
//	}
//
//	private void CopyArray(float[,] oldA, float[] newA, int size) {
//		int newI = 0;
//		for (int i = 0; i < size; i++) {
//			newA[newI++] = oldA[i, 0];
//			newA[newI++] = oldA[i, 1];
//			newA[newI++] = oldA[i, 2];
//		}
//	}
//
//	private void CopyArray(float[] oldA, float[] newA, int oldStartIndex, int newStartIndex, int count) {
//		
//		for (int i = 0; i < count * 3; i++) 
//			newA[newStartIndex + i] = oldA[oldStartIndex + i];
//	}
//
//	private void CopyArray(float[,] oldA, float[] newA, int oldStartIndex, int newStartIndex, int count) {
//		int newI = newStartIndex * 3;
//		for (int i = oldStartIndex; i < oldStartIndex + count; i++) {
//				newA[newI++] = oldA[i, 0];
//				newA[newI++] = oldA[i, 1];
//				newA[newI++] = oldA[i, 2];
//			}
//	}
//}
//
//public class ConnectionTable {
//
//	/*
//		CONNECTION TABLE CLASS
//
//		This is essentially a jagged array with functions to manage index shuffling
//
//		Connectivity isn't directional at this level.
//		We should be able to make the appropriate disconnections when an atom is removed.
//	*/
//
//	private ConnectionItem[] _table;
//	private int _size;
//	public int size{
//		get {return _size;}
//	}
//
//	public ConnectionTable(int size) {
//		_table = new ConnectionItem[size];
//		for (int w = 0; w < size; w++) {
//			_table[w] = new ConnectionItem();
//		}
//		this._size = size;
//	}
//
//	public ConnectionTable(ConnectionItem[] table) {
//		_table = table;
//		this._size = table.Length;
//	}
//
//	public ConnectionItem this[int index] {
//		get {
//			return _table[WrappedIndex(index)];
//		}
//		set {
//			_table[WrappedIndex(index)] = value;
//		}
//	}
//
//	public ConnectionTable this[int[] indices] {
//		get {
//			ConnectionTable table = new ConnectionTable(indices.Length);
//
//			for (int newIndex=0; newIndex < indices.Length; newIndex++) {
//				int oldIndex = indices[newIndex];
//
//				Bond[] oldBonds = _table[oldIndex].ToArray();
//
//				List<Bond> newBondsList = new List<Bond>();
//
//				for (int bondNum=0; bondNum < oldBonds.Length; bondNum++) {
//					Bond oldBond = oldBonds[bondNum];
//					for (int newBondIndex=0; newBondIndex < indices.Length; newBondIndex++) {
//						if (oldBond.index == indices[newBondIndex]) {
//							newBondsList.Add(new Bond(newBondIndex, oldBond.bondType));
//						}
//					}
//				}
//				ConnectionItem connection = new ConnectionItem(newBondsList.ToArray());
//			}
//
//			return table;
//		}
//	}
//
//	private int WrappedIndex(int index) {
//		int i = index;
//		if (i < 0) i += size;
//		if (i < 0 || i > size) throw new System.IndexOutOfRangeException();
//		return i;
//	}
//
//	public Bond[][] ToJaggedArray(bool directed) {
//
//		// Get the directed or non-directed graph for this ConnectionTable
//		Bond[][] jaggedArray = new Bond[size][];
//		if (directed) {
//			for (int w = 0; w < size; w++) {
//				jaggedArray[w] = _table[w].ToCulledArray(w);
//			}
//		} else {
//			for (int w = 0; w < size; w++) {
//				jaggedArray[w] = _table[w].ToArray();
//			}
//		}
//		return jaggedArray;
//	}
//
//	public string ToGaussianConnectivityString() {
//		StringBuilder sb = new StringBuilder();
//
//		Bond[][] jaggedArray = ToJaggedArray(true);
//
//		//Gaussian uses Fortran-like indexing. Add 1 to all indexes.
//
//		for (int w = 0; w < size; w++) {
//			sb.AppendFormat(" {0}", w + 1);
//			Bond[] connections = jaggedArray[w];
//
//			for (int c = 0; c < connections.Length; c++) {
//				Bond bond = connections[c];
//				sb.AppendFormat(" {0} {1}", bond.index + 1, Settings.GetBondGaussString(bond.bondType));
//			}
//			sb.Append("\n");
//		}
//
//		return sb.ToString();
//	}
//
//	public void FromJaggedArray(Bond[][] jaggedArray, int size) {
//		this._size = size;
//		for (int w = 0; w < size; w++) {
//			_table[w].FromArray(jaggedArray[w]);
//		}
//	}
//
//	public void Resize(int newSize) {
//		ConnectionItem[] oldArray = _table.Clone() as ConnectionItem[];
//		_table = new ConnectionItem[newSize];
//		CopyArray(oldArray, _table, _size);
//		_size = newSize;
//	}
//
//	public void Resize(int count, int insertIndex) {
//		// Resize the array by inserting blank items
//
//		int newSize = count + size;
//		int i = WrappedIndex(insertIndex);
//
//		//Make temp copy of _array
//		ConnectionItem[] oldArray = _table.Clone() as ConnectionItem[];
//		_table = new ConnectionItem[newSize];
//
//		//Copy elements before insertIndex
//		CopyArray(oldArray, _table, 0, 0, i);
//		//Copy elements after insertIndex
//		CopyArray(oldArray, _table, i, i + count, newSize - i - count);
//		this._size = newSize;
//	}
//
//	public void ResizeAndShift(int count, int insertIndex) {
//		// Resize the array by inserting blank items
//
//		int newSize = count + size;
//		int i = WrappedIndex(insertIndex);
//
//		//Make temp copy of _array
//		ConnectionItem[] oldArray = _table.Clone() as ConnectionItem[];
//		_table = new ConnectionItem[newSize];
//
//		//Copy elements before insertIndex
//		CopyArrayAndShift(oldArray, _table, 0, 0, i, count, insertIndex);
//		//Copy elements after insertIndex
//		CopyArrayAndShift(oldArray, _table, i, i + count, newSize - i - count, count, insertIndex);
//		this._size = newSize;
//	}
//
//	public void Add() {
//		Resize(_size + 1);
//		this[size - 1] = new ConnectionItem();
//	}
//
//	public void Add(ConnectionItem item) {
//		Resize(_size + 1);
//		this[size - 1] = item;
//		for (int c = 0; c < item.numNeighbours; c++) {
//			Connect(item[c].index, -1);
//		}
//	}
//
//	public void Insert(ConnectionItem item, int insertIndex) {
//		int w = WrappedIndex(insertIndex);
//		ResizeAndShift(1, w);
//		ConnectionItem shifted = item.Shifted(1, insertIndex);
//		this[w] = shifted;
//		for (int c = 0; c < shifted.numNeighbours; c++) {
//			Connect(shifted[c].index, w);
//		}
//	}
//
//	public void Insert(int insertIndex) {
//		int w = WrappedIndex(insertIndex);
//		ResizeAndShift(1, w);
//		this[w] = new ConnectionItem();
//	}
//
//	public void Remove(int removeIndex) {
//		int w = WrappedIndex(removeIndex);
//		ResizeAndShift( -1, w);
//	}
//
//	public void Connect(int index0, int index1, BT order=BT.SINGLE) {
//		if (order == (int)Constants.BondType.NONE) return;
//		int w0 = WrappedIndex(index0);
//		int w1 = WrappedIndex(index1);
//		_table[w0].Connect(w1, order);
//		_table[w1].Connect(w0, order);
//	}
//
//	public void Disonnect(int index0, int index1) {
//		int w0 = WrappedIndex(index0);
//		int w1 = WrappedIndex(index1);
//		_table[w0].Disonnect(w1);
//		_table[w1].Disonnect(w0);
//	}
//
//	private void CopyArray(ConnectionItem[] oldA, ConnectionItem[] newA, int size) {
//		for (int i = 0; i < size; i++) {
//			newA[i] = oldA[i];
//		};
//	}
//
//	private void CopyArray(ConnectionItem[] oldA, ConnectionItem[] newA, int oldStartIndex, int newStartIndex, int count) {
//		for (int i = 0; i < count; i++) 
//			newA[newStartIndex + i] = oldA[oldStartIndex + i];
//	}
//
//	private void CopyArrayAndShift(
//		ConnectionItem[] oldA, 
//		ConnectionItem[] newA, 
//		int oldStartIndex, 
//		int newStartIndex, 
//		int count, 
//		int shiftBy, 
//		int shiftIfGreaterOrEqualTo
//	) {
//		for (int i = 0; i < count; i++) 
//			newA[newStartIndex + i] = oldA[oldStartIndex + i].Shifted(shiftBy, shiftIfGreaterOrEqualTo);
//	}
//}
//
//public class ConnectionItem {
//
//	/*
//		CONNECTION ITEM CLASS
//
//		An atom connected to a0 and a1:
//		[atomIndex0, atomIndex1]
//
//		NOTATION:
//		atomIndex: index of atom in Atoms object that this ConnectionItem is connected to
//		c: index of _connections. Can be negative (pythonic). Do not use as an accessor
//		w: wrapped index of _connections. C#-like. Use only this as an accessor
//
//		e.g. for a ConnectionItem of size 4 these three are equivalent:
//
//		w:  0  1  2  3
//		c:  0  1  2  3
//		c:  0  1 -2 -1
//		
//	*/
//	private Bond[] _neighbours;
//	private int _size;
//	public int numNeighbours {
//		get {return _size;}
//	}
//
//	private int _bondOrders;
//
//	private int WrappedIndex(int c) {
//		int w = c;
//		if (w < 0) w += numNeighbours;
//		if (w < 0 || w > numNeighbours) throw new System.IndexOutOfRangeException();
//		return w;
//	}
//
//	public Bond this[int index] {
//		get {return _neighbours[index];}
//		set {_neighbours[index] = value;}
//	}
//
//	public Bond[] ToArray() {
//		return _neighbours.Clone() as Bond[];
//	}
//	public void FromArray(Bond[] array) {
//		_neighbours = array.Clone() as Bond[];
//		_size = array.Length;
//	}
//
//	public Bond[] ToCulledArray(int cullIfLessThan) {
//		List<Bond> list = new List<Bond>();
//		for (int w = 0; w < numNeighbours; w++) {
//			if (_neighbours[w].index >= cullIfLessThan) {
//				list.Add(_neighbours[w]);
//			}
//		}
//		return list.ToArray();
//	}
//		
//	
//	public ConnectionItem() {
//		_neighbours = new Bond[0];
//	}
//	
//	public ConnectionItem(int numNeighbours) {
//		_neighbours = new Bond[numNeighbours];
//	}
//	
//	public ConnectionItem(Bond[] neighbours) {
//		_neighbours = neighbours;
//		_size = neighbours.Length;
//	}
//	
//	public ConnectionItem(int[] neighbourIndices) {
//		int size = neighbourIndices.Length;
//
//		Bond[] neighbours = new Bond[size];
//		for (int w = 0; w < size; w++) {
//			neighbours[w] = new Bond(neighbourIndices[w]);
//		}
//		_neighbours = neighbours;
//		_size = size;
//	}
//	
//	public ConnectionItem(int[] neighbourIndices, BT[] bondOrders) {
//		int size = neighbourIndices.Length;
//		if (size != bondOrders.Length) {
//			Debug.LogErrorFormat(
//				"Neighbour Indices must have same length as Bond Orders ({0} != {1})",
//				size,
//				neighbourIndices.Length
//			);
//		}
//
//		Bond[] neighbours = new Bond[size];
//		for (int w = 0; w < size; w++) {
//			neighbours[w] = new Bond(neighbourIndices[w], bondOrders[w]);
//		}
//		_neighbours = neighbours;
//		_size = size;
//	}
//
//	public bool ConnectedTo(int atomIndex) {
//		for (int c = 0; c < numNeighbours; c++) {
//			if (atomIndex == _neighbours[c].index)
//				return true;
//		}
//		return false;
//	}
//
//	public void Connect(int atomIndex, BT bondType=BT.SINGLE) {
//		if (! ConnectedTo(atomIndex)) {
//			Resize(numNeighbours + 1);
//			_neighbours[numNeighbours - 1] = new Bond(atomIndex, bondType);
//		}
//	}
//
//	public void Disonnect(int atomIndex) {
//		if (ConnectedTo(atomIndex)) {
//			Remove(atomIndex);
//		}
//	}
//
//	private void Resize(int newSize) {
//		Bond[] oldArray = _neighbours.Clone() as Bond[];
//		this._neighbours = new Bond[newSize];
//		CopyArray(oldArray, this._neighbours, _size);
//		this._size = newSize;
//	}
//
//	public void Remove(int atomIndex) {
//		for (int w = 0; w < numNeighbours; w++) {
//			if (atomIndex == _neighbours[w].index)
//				RemoveAt(w);
//		}
//	}
//
//	public void RemoveAt(int connectionIndex) {
//
//		int w = WrappedIndex(connectionIndex);
//		_size -= 1;
//
//		//Make temp copy of _connections
//		Bond[] oldArray = _neighbours.Clone() as Bond[];
//		_neighbours = new Bond[_size];
//
//		//Copy elements before index
//		CopyArray(oldArray, _neighbours, 0, 0, w);
//		//Copy elements after index
//		CopyArray(oldArray, _neighbours, w, w + 1, _size - w - 1);
//
//	}
//
//	public ConnectionItem Shifted(int shiftBy, int shiftIfGreaterOrEqualTo) {
//		ConnectionItem connectionItem = new ConnectionItem(numNeighbours);
//		Bond[] shiftedNeighbours = new Bond[numNeighbours];
//		CopyArrayAndShift(_neighbours, shiftedNeighbours, numNeighbours, shiftBy, shiftIfGreaterOrEqualTo);
//		connectionItem.FromArray(shiftedNeighbours);
//		return connectionItem;
//
//	}
//
//	private void CopyArray(Bond[] oldA, Bond[] newA, int size) {
//		for (int w = 0; w < size; w++) newA[w] = oldA[w];
//	}
//
//	private void CopyArray(Bond[] oldA, Bond[] newA, int oldStartIndex, int newStartIndex, int count) {
//		for (int w = 0; w < count; w++) 
//			newA[newStartIndex + w] = oldA[oldStartIndex + w];
//	}
//
//	private void CopyArrayAndShift(Bond[] oldA, Bond[] newA, int size, int shiftBy, int shiftIfGreaterOrEqualTo) {
//		for (int w = 0; w < size; w++) {
//			int newAtomIndex = oldA[w].index;
//			if (newAtomIndex >= shiftIfGreaterOrEqualTo) {
//				newA[w] = new Bond(oldA[w].index + shiftBy, oldA[w].bondType);
//			} else {
//				newA[w] = oldA[w];
//			}
//		}
//	}
//
//	private void CopyArrayAndShift(
//		int[] oldA, 
//		int[] newA, 
//		int oldStartIndex, 
//		int newStartIndex, 
//		int count, 
//		int shiftBy, 
//		int shiftIfGreaterOrEqualTo
//	) {
//		for (int w = 0; w < count; w++) {
//			int newAtomIndex = oldA[oldStartIndex + w];
//			if (newAtomIndex >= shiftIfGreaterOrEqualTo) {
//				newA[newStartIndex + w] = oldA[oldStartIndex + w] + shiftBy;
//			} else {
//				newA[newStartIndex + w] = oldA[oldStartIndex + w];
//			}
//		}
//	}
//}
//
//public class IntCondition {
//
//	private delegate bool CheckFunction(int value);
//	public const int INDICES = 0;
//	public const int TAGS = 1;
//	public const int RESNUMS = 2;
//	public const int FORMALCHARGES = 3;
//
//	public const int EQ = 0;
//	public const int NEQ = 1;
//	public const int GT = 2;
//	public const int GE = 3;
//	public const int LT = 4;
//	public const int LE = 5;
//	
//
//	public int arrayName;
//	private int compareWith;
//	private CheckFunction checkFunction;
//
//	public IntCondition(int arrayName, int operationType, int compareWith) {
//		this.compareWith = compareWith;
//		this.arrayName = arrayName;
//		this.checkFunction = GetCheckFunction(operationType);
//	}
//
//	private CheckFunction GetCheckFunction(int operationType) {
//		switch (operationType) {
//			case (EQ): return EqualTo;
//			case (NEQ): return NotEqualTo;
//			case (GT): return GreaterThan;
//			case (GE): return GreaterOrEqualTo;
//			case (LT): return LessThan;
//			case (LE): return LessOrEqualTo;
//		}
//		throw new System.Exception(
//			string.Format(
//				"Operation type ({0}) for Int Condition not available",
//				operationType
//			)
//		);
//	}
//
//	public int[] GetArray(Atoms atoms) {
//		switch (arrayName) {
//			case (IntCondition.INDICES): return Enumerable.Range(0, atoms.size).ToArray();
//			case (IntCondition.TAGS): return atoms.tags;
//			case (IntCondition.RESNUMS): return atoms.residueNumbers;
//			case (IntCondition.FORMALCHARGES): return atoms.formalCharges;
//		}
//		throw new System.Exception(
//			string.Format(
//				"Array name ({0}) for Int Condition not available",
//				arrayName
//			)
//		);
//	}
//
//	public bool Check(int value) {return checkFunction(value);}
//	private bool EqualTo(int value) {return compareWith == value;}
//	private bool NotEqualTo(int value) {return compareWith != value;}
//	private bool GreaterThan(int value) {return value > compareWith;}
//	private bool GreaterOrEqualTo(int value) {return value >= compareWith;}
//	private bool LessThan(int value) {return value < compareWith;}
//	private bool LessOrEqualTo(int value) {return value <= compareWith;}
//}
//
//public class StringCondition {
//
//	private delegate bool CheckFunction(string value);
//	public const int ELEMENTS = 0;
//	public const int PDBS = 1;
//	public const int AMBER = 2;
//	public const int RESNAMES = 3;
//	public const int CHAINS = 4;
//
//	public const int EQ = 0;
//	public const int NEQ = 1;
//	public const int IN = 2;
//	
//
//	public int arrayName;
//	private List<string> compareWith;
//	private CheckFunction checkFunction;
//
//	public StringCondition(int arrayName, int operationType, List<string> compareWith) {
//		this.compareWith = compareWith;
//		this.arrayName = arrayName;
//		this.checkFunction = GetCheckFunction(operationType);
//	}
//
//	private CheckFunction GetCheckFunction(int operationType) {
//		switch (operationType) {
//			case (EQ): return EqualTo;
//			case (NEQ): return NotEqualTo;
//			case (IN): return Contains;
//		}
//		throw new System.Exception(
//			string.Format(
//				"Operation type ({0}) for String Condition not available",
//				operationType
//			)
//		);
//	}
//
//	public bool Check(string value) {return checkFunction(value);}
//	private bool EqualTo(string value) {return compareWith.Contains(value);}
//	private bool NotEqualTo(string value) {return (!EqualTo(value));}
//	private bool Contains(string value) {
//		
//		foreach (string compareValue in compareWith) {
//			if (value.Contains(compareValue)) {
//				return true;
//			}
//		}
//		return false;
//	}
//	
//	public string[] GetArray(Atoms atoms) {
//		switch (arrayName) {
//			case (StringCondition.ELEMENTS): return atoms.elements;
//			case (StringCondition.PDBS): return atoms.pdbNames;
//			case (StringCondition.AMBER): return atoms.amberNames;
//			case (StringCondition.RESNAMES): return atoms.residueNames;
//			case (StringCondition.CHAINS): return atoms.chains;
//		}
//		throw new System.Exception(
//			string.Format(
//				"Array name ({0}) for String Condition not available",
//				arrayName
//			)
//		);
//	}
}