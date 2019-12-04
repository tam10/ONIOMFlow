using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using EL = Constants.ErrorLevel;
using System.Text.RegularExpressions;

public class GeometryReader {
    
    public string path;
	public int lineNumber;
	public string line;
    public int charNum;
    public int skipLines;

    static Regex ToAlphaRegex = new Regex(@"[^a-zA-Z -]", RegexOptions.Compiled);
    static Regex ToNumberRegex = new Regex(@"[^0-9 -]", RegexOptions.Compiled);

    public string commentString = "";
    
    
    public IEnumerator GeometryFromFile(string filePath, Geometry geometry) {

        path = filePath;
        geometry.name = Path.GetFileName (path);

        return GeometryFromEnumerator(FileIO.EnumerateLines(path, commentString), geometry);
    }

	public IEnumerator GeometryFromAsset(TextAsset asset, Geometry geometry) {

        path = asset.name;
        geometry.name = Path.GetFileName (path);

        return GeometryFromEnumerator(FileIO.EnumerateAsset(asset, commentString), geometry);
	}

    public IEnumerator GeometryFromEnumerator(IEnumerable<string> lineEnumerator, Geometry geometry) {

        geometry.atomMap = new Map<AtomID, int>();
        lineNumber = 0;
        skipLines = 0;

        foreach (string line in lineEnumerator) {
            if (skipLines > 0) {
                skipLines--;
            } else {
                this.line = line;
                if (!ParseLine(geometry)) {
                    yield break;
                }
            }


            if (Timer.yieldNow) {
                yield return null;
            }

            lineNumber++;
        }

    }

    public virtual bool ParseLine(Geometry geometry) {
        return false;
    }

	public static string ToAlpha(string inputStr) {
		return ToAlphaRegex.Replace (inputStr, string.Empty);
	}

	public static string ToNumber(string inputStr) {
		return ToNumberRegex.Replace (inputStr, string.Empty);
	}

	public bool TryGetString(string line, int startChar, int length, bool trim, string methodName, out string outputString) {
		try {
			outputString = line.Substring (startChar, length);
			if (trim) {
				outputString = outputString.Trim();
			}
			return true;
		} catch (System.Exception e) {
			ThrowError(methodName, e);
			outputString = "";
			return false;
		}
	}

	public bool TryGetFloat(string line, int startChar, int length, bool trim, string methodName, out float outputFloat) {
		try {
			if (trim) {
				outputFloat = float.Parse(line.Substring (startChar, length).Trim());
			} else {
				outputFloat = float.Parse(line.Substring (startChar, length));
			}
			return true;
		} catch (System.Exception e) {
			ThrowError(methodName, e);
			outputFloat = 0f;
			return false;
		}
	}

	public bool TryGetFloat(string line, string value, string methodName, out float outputFloat) {
		try {
			outputFloat = float.Parse(value);
			return true;
		} catch (System.Exception e) {
            charNum = line.IndexOf(value);
            charNum = (charNum <= 0) ? 0 : charNum;
			ThrowError(methodName, e);
			outputFloat = 0f;
			return false;
		}
	}

	public bool TryGetInt(string line, int startChar, int length, bool trim, string methodName, out int outputInt) {
		try {
			if (trim) {
				outputInt = int.Parse(line.Substring (startChar, length).Trim());
			} else {
				outputInt = int.Parse(line.Substring (startChar, length));
			}
			return true;
		} catch (System.Exception e) {
			ThrowError(methodName, e);
			outputInt = 0;
			return false;
		}
	}

	public bool TryGetInt(string line, string value, string methodName, out int outputInt) {
		try {
			outputInt = int.Parse(value);
			return true;
		} catch (System.Exception e) {
            charNum = line.IndexOf(value);
            charNum = (charNum <= 0) ? 0 : charNum;
			ThrowError(methodName, e);
			outputInt = 0;
			return false;
		}
	}

	public bool TryGetFloat(string value, bool trim, string methodName, out float outputFloat) {
		try {
			if (trim) {
				outputFloat = float.Parse(value.Trim());
			} else {
				outputFloat = float.Parse(value);
			}
			return true;
		} catch (System.Exception e) {
			ThrowError(methodName, e);
			outputFloat = 0f;
			return false;
		}
	}

	public bool TryGetInt(string value, bool trim, string methodName, out int outputInt) {
		try {
			if (trim) {
				outputInt = int.Parse(value.Trim());
			} else {
				outputInt = int.Parse(value);
			}
			return true;
		} catch (System.Exception e) {
			ThrowError(methodName, e);
			outputInt = 0;
			return false;
		}
	}
    

	/// <summary>
	/// Notify the user of an error while reading a file
	/// </summary>
	/// <param name="methodName">The name of the method that caused the error.</param>
	/// <param name="line">The string representation of the line.</param>
	/// <param name="error">The error object.</param>
	/// <returns>void</returns>
	public void ThrowError(
		string methodName,
		System.Exception error
	) {
		CustomLogger.LogFormat(EL.ERROR, error.Message);
		CustomLogger.LogOutput(
			"Failed to read {0}. Line: {1}:{2} (Failed on {3}){7}{4}{7}{5}{7} Trace:{7}{6}",
			path,
			lineNumber,
			charNum,
			methodName,
			line,
			(charNum < 1) ? "^" : string.Join("", Enumerable.Repeat(' ', charNum - 1)) + "^",
			error.ToString(),
			FileIO.newLine
		);

	}
    

	/// <summary>
	/// Notify the user of an error while reading a file
	/// </summary>
	/// <param name="methodName">The name of the method that caused the error.</param>
	/// <param name="line">The string representation of the line.</param>
	/// <param name="error">The error object.</param>
	/// <returns>void</returns>
	public void ThrowError(
		string methodName
	) {
		CustomLogger.LogFormat(
            EL.ERROR, "Failed to read {0}. (Failed on {1})",
			path,
			methodName
		);
		CustomLogger.LogOutput(
			"Failed to read {0}. Line: {1}:{2} (Failed on {3}){6}{4}{6}{5}",
			path,
			lineNumber,
			charNum,
			methodName,
			line,
			(charNum < 1) ? "^" : string.Join("", Enumerable.Repeat(' ', charNum - 1)) + "^",
			FileIO.newLine
		);

	}

}