using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Extensions {
    
    public static void Deconstruct<K, V>(this KeyValuePair<K, V> kvp, out K Key, out V Value) {
        Key = kvp.Key;
        Value = kvp.Value;
    }

    public static V? GetOrNull<K, V>(this Dictionary<K, V?> dictionary, K key) where V : struct {
        V? value;
        return dictionary.TryGetValue(key, out value) ? value : null;
    }

    public static IEnumerable<(T,T)> UpperTriangle<T>(this IEnumerable<T> enumerable) {
        int i = 0;
        foreach (T item1 in enumerable) {
            foreach (T item2 in enumerable.Skip(++i)) {
                yield return (item1, item2);
            }
        }
    }

    ///<summary>Gets the array of lines in a TextAsset</summary>
    ///<param name="textAsset">Input TextAsset</param>
    public static string[] ToArray(this TextAsset textAsset) => textAsset.text.Split(new string[] {FileIO.newLine}, System.StringSplitOptions.None);

    public static void Shuffle<T>(this IList<T> list) {
        System.Random selector = new System.Random();

        for (int i=list.Count - 1; i > 1; i--) {
            int randIndex = selector.Next(i+1);
            T temp = list[randIndex];
            list[randIndex] = list[i];
            list[i] = temp;
        }
    }

    public static Map<T1, T2> ToMap<T,T1,T2>(
        this IEnumerable<T> source,
        System.Func<T, T1> keySelector,
        System.Func<T, T2> valueSelector
    ) {
        Map<T1, T2> map = new Map<T1, T2>();
        foreach (T item in source) {
            map.Add(keySelector(item), valueSelector(item));
        }
        return map;
    }

    public static float Squared(this float x) {
        return x * x;
    }

    public static bool Contains(this string str, string value, System.StringComparison comparison) {
        return (!string.IsNullOrEmpty(str)) && (!string.IsNullOrEmpty(value)) && str.IndexOf(value, comparison) >= 0;
    }
}

public class Map<T1, T2> : IDictionary<T1, T2> {
    protected IDictionary<T1, T2> forward;
    protected IDictionary<T2, T1> reverse;

    public Map() {
        forward = new Dictionary<T1, T2>();
        reverse = new Dictionary<T2, T1>();
    }
    public Map(int capacity) {
        forward = new Dictionary<T1, T2>(capacity);
        reverse = new Dictionary<T2, T1>(capacity);
    }

    public void Add(T1 key, T2 value) {
        if (forward.ContainsKey(key)) {
            throw new System.ArgumentException(string.Format(
                "Duplicate entry ({0}){1} in Map!",
                typeof(T1),
                key
            ));
        }
        if (reverse.ContainsKey(value)) {
            throw new System.ArgumentException(string.Format(
                "Duplicate entry ({0}){1} in Map!",
                typeof(T2),
                value
            ));
        }
        forward[key] = value;
        reverse[value] = key;
    }
    public void Add(KeyValuePair<T1, T2> kvp) => Add(kvp.Key, kvp.Value);
    
    public virtual bool Remove(T1 key, T2 value) {
        if (!forward.ContainsKey(key)) {
            return false;
        }
        if (!reverse.ContainsKey(value)) {
            return false;
        }

        return (forward.Remove(key) && reverse.Remove(value));
        
    }
    public virtual bool Remove(KeyValuePair<T1, T2> kvp) {
        return Remove(kvp.Key, kvp.Value);
        
    }

    public virtual IEnumerator<KeyValuePair<T1, T2>> GetEnumerator() {
        return forward.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Remove(T1 key) {
        return Remove(key, forward[key]);
    }

    public bool Remove(T2 value) {
        return Remove(reverse[value], value);
    }

    public bool TryGetValue(T1 key, out T2 value) {
        return forward.TryGetValue(key, out value);
    }

    public bool TryGetValue(T2 value, out T1 key) {
        return reverse.TryGetValue(value, out key);
    }

    public T2 this[T1 k] {
        get => forward[k];
        set {
            reverse.Remove(value);
            forward[k] = value;
            reverse[value] = k;
        }
    }
    public T1 this[T2 v] {
        get => reverse[v];
        set {
            forward.Remove(value);
            reverse[v] = value;
            forward[value] = v;
        }
    }
    
    public virtual void Clear() {
        forward.Clear();
        reverse.Clear();
    }

    public virtual bool Contains(KeyValuePair<T1, T2> kvp) => forward.Contains(kvp);
    public virtual bool Contains(KeyValuePair<T2, T1> kvp) => reverse.Contains(kvp);
    
    public virtual void CopyTo(KeyValuePair<T1, T2>[] array, int index) => forward.CopyTo(array, index);
    public virtual void CopyTo(KeyValuePair<T2, T1>[] array, int index) => reverse.CopyTo(array, index);
    
    public virtual bool ContainsKey(T1 key) => forward.ContainsKey(key);
    public virtual bool ContainsKey(T2 value) => reverse.ContainsKey(value);
    public virtual int Count => forward.Count;

    public virtual ICollection<T1> Keys => forward.Keys;
    public virtual ICollection<T2> Values => forward.Values;
    public virtual bool IsReadOnly => forward.IsReadOnly;

}



