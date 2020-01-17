using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System;
using EL = Constants.ErrorLevel;

public static class FileIO {

	public static string newLine = Environment.NewLine;

	// Return array of strings for each line in file
	public static string[] Readlines(string filename, string commentString="") {
		
		if (!File.Exists (filename))
			throw new FileNotFoundException ("File " + filename + " does not exist.");

		string[] allLines = File.ReadAllLines (filename);

		List<string> lines = new List<string> ();

		for (int lineNum = 0; lineNum < allLines.Length; lineNum++) {
			string line = allLines [lineNum];

			if (commentString == "" || !line.StartsWith (commentString)) {
				lines.Add (line);
			}
		}		

		return lines.ToArray ();
	}
	public static string Read(string filename) {
		
		if (!File.Exists (filename))
			throw new FileNotFoundException ("File " + filename + " does not exist.");

		return File.ReadAllText (filename);
	}

	public static IEnumerable<string> EnumerateLines(string filename, string commentString) {
		
		if (!File.Exists (filename))
			throw new FileNotFoundException ("File " + filename + " does not exist.");

		using (FileStream fileStream = File.OpenRead(filename)) {
			using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8, true)) {
				string line;
				while ((line = streamReader.ReadLine()) != null) {
					if (!line.StartsWith (commentString)) {
						yield return line;
					}
				}
			}
		}

	}

	public static IEnumerable<string> EnumerateLines(string filename) {
		
		if (!File.Exists (filename))
			throw new FileNotFoundException ("File " + filename + " does not exist.");

		using (FileStream fileStream = File.OpenRead(filename)) {
			using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8, true)) {
				string line;
				while ((line = streamReader.ReadLine()) != null) {
					yield return line;
				}
			}
		}
	}

	public static IEnumerable<string> EnumerateAsset(TextAsset asset, string commentString) {
		
		bool removeComments = (commentString != "");

		if (asset == null) {
			throw new ArgumentNullException("asset");
		}

		string text = asset.text;
		string value;
		int start = 0;
		for (int end = text.IndexOf(newLine); end != -1; end = text.IndexOf(newLine, start)) {
			value = text.Substring(start, end - start);
			if (!removeComments || !value.StartsWith(commentString)) {
				yield return value;
			}
			start = end + newLine.Length;
		}

		value = text.Substring(start);
		if (!removeComments || !value.StartsWith(commentString)){
			yield return value;
		};

	}

	public static IEnumerable<string> EnumerateAsset(TextAsset asset) {
		
		if (asset == null) {
			throw new ArgumentNullException("asset");
		}

		string text = asset.text;
		int start = 0;
		for (int end = text.IndexOf(newLine); end != -1; end = text.IndexOf(newLine, start)) {
			yield return text.Substring(start, end - start);
			start = end + newLine.Length;
		}

		yield return text.Substring(start);
	}

	public static string[,] ReadCSV(string filename, int columns) {

		string[] lines = Readlines (filename, "#");
		string[,] data = new string[lines.Length, columns];

		for (int lineNum = 0; lineNum < lines.Length; lineNum++) {

			string[] splitLine = lines [lineNum].Split (new []{ "," }, System.StringSplitOptions.RemoveEmptyEntries);

			for (int columnNum = 0; columnNum < columns; columnNum++) {
				if (columnNum < splitLine.Length) {
					data [lineNum, columnNum] = splitLine [columnNum];
				} else {
					data [lineNum, columnNum] = string.Empty;
				}
			}

		}

		return data;
	}

	public static XDocument ReadXML(string filename) {

		XDocument xmlObj = XDocument.Load (filename, LoadOptions.SetBaseUri);
		return xmlObj;

	}

	public static string ParseXMLAttrString(XElement xElement, string name, string defaultValue=null) {
		System.Xml.Linq.XAttribute result = xElement.Attribute(name);
		if (result == null) {
			if (defaultValue == null) {
				throw new ErrorHandler.XMLParseError(string.Format(
					"Failed to read {0}: Couldn't find Attribute '{1}' in Element {2}. Element Trace: {3}", 
					xElement.Document.BaseUri,
					name,
					xElement.Name, 
					ElementTrace(xElement)
				));
			} else {
				return defaultValue;
			}
		}
		return result.Value;
	}

	public static float ParseXMLAttrFloat(XElement xElement, string name) {
		string value = ParseXMLAttrString(xElement, name);

		float result;
		if (!float.TryParse(value, out result)) {
			throw new ErrorHandler.XMLParseError(string.Format(
				"Failed to read {0}: Couldn't convert Attribute '{1}' in ({2}) to <float>. Element Trace: {3}", 
				xElement.Document.BaseUri,
				value,
				name, 
				ElementTrace(xElement)
			));
		}
		return result;
	} 

	public static float ParseXMLAttrFloat(XElement xElement, string name, float defaultValue) {
		string value = ParseXMLAttrString(xElement, name, "");
		if (value == "") return defaultValue;

		float result;
		if (!float.TryParse(value, out result)) {
			throw new ErrorHandler.XMLParseError(string.Format(
				"Failed to read {0}: Couldn't convert Attribute '{1}' in ({2}) to <float>. Element Trace: {3}", 
				xElement.Document.BaseUri,
				value,
				name, 
				ElementTrace(xElement)
			));
		}
		return result;
	} 

	public static int ParseXMLAttrInt(XElement xElement, string name) {
		string value = ParseXMLAttrString(xElement, name);
		
		int result;
		if (!int.TryParse(value, out result)) {
			throw new ErrorHandler.XMLParseError(string.Format(
				"Failed to read {0}: Couldn't convert Attribute '{1}' in ({2}) to <int>. Element Trace: {3}", 
				xElement.Document.BaseUri,
				value,
				name, 
				ElementTrace(xElement)
			));
		}
		return result;
	} 

	public static int ParseXMLAttrInt(XElement xElement, string name, int defaultValue) {
		string value = ParseXMLAttrString(xElement, name, "");
		if (value == "") return defaultValue;
		
		int result;
		if (!int.TryParse(value, out result)) {
			throw new ErrorHandler.XMLParseError(string.Format(
				"Failed to read {0}: Couldn't convert Attribute '{1}' in ({2}) to <int>. Element Trace: {3}", 
				xElement.Document.BaseUri,
				value,
				name, 
				ElementTrace(xElement)
			));
		}
		return result;
	} 

	public static string ParseXMLString(XElement xElement, string name) {
		XElement result = xElement.Element(name);
		if (result == null) {
			throw new ErrorHandler.XMLParseError(string.Format(
				"Failed to read {0}: Couldn't find Value '{1}' in Element {2}. Element Trace: {3}", 
				xElement.Document.BaseUri,
				name,
				xElement.Name, 
				ElementTrace(xElement)
			));
		}
		return result.Value.Replace("\\n", "<br>");
	}

	public static string ParseXMLString(XElement xElement, string name, string defaultValue=null) {
		XElement result = xElement.Element(name);
		if (result == null) {
			if (defaultValue == null) {
				throw new ErrorHandler.XMLParseError(string.Format(
					"Failed to read {0}: Couldn't find Value '{1}' in Element {2}. Element Trace: {3}", 
					xElement.Document.BaseUri,
					name,
					xElement.Name, 
					ElementTrace(xElement)
				));
			} else {
				return defaultValue;
			}
		}
		return result.Value.Replace("\\n", "<br>");
	}

	public static float ParseXMLFloat(XElement xElement, string name) {
		string value = ParseXMLString(xElement, name);
		float result;
		if (!float.TryParse(value, out result)) {
			throw new ErrorHandler.XMLParseError(string.Format(
				"Failed to read {0}: Couldn't convert Value '{1}' in ({2}) to <float>. Element Trace: {3}", 
				xElement.Document.BaseUri,
				value,
				name, 
				ElementTrace(xElement)
			));
		}
		return result;
	} 

	public static float ParseXMLFloat(XElement xElement, string name, float defaultValue) {
		string value = ParseXMLString(xElement, name, "");
		if (value == "") return defaultValue;

		float result;
		if (!float.TryParse(value, out result)) {
			throw new ErrorHandler.XMLParseError(string.Format(
				"Failed to read {0}: Couldn't convert Value '{1}' in ({2}) to <float>. Element Trace: {3}", 
				xElement.Document.BaseUri,
				value,
				name, 
				ElementTrace(xElement)
			));
		}
		return result;
	} 

	public static int ParseXMLInt(XElement xElement, string name) {
		string value = ParseXMLString(xElement, name);
		int result;
		if (!int.TryParse(value, out result)) {
			throw new ErrorHandler.XMLParseError(string.Format(
				"Failed to read {0}: Couldn't convert Value '{1}' in ({2}) to <int>. Element Trace: {3}", 
				xElement.Document.BaseUri,
				value,
				name, 
				ElementTrace(xElement)
			));
		}
		return result;
	} 

	public static int ParseXMLInt(XElement xElement, string name, int defaultValue) {
		string value = ParseXMLString(xElement, name, "");
		if (value == "") return defaultValue;

		int result;
		if (!int.TryParse(value, out result)) {
			throw new ErrorHandler.XMLParseError(string.Format(
				"Failed to read {0}: Couldn't convert Value '{1}' in ({2}) to <int>. Element Trace: {3}", 
				xElement.Document.BaseUri,
				value,
				name, 
				ElementTrace(xElement)
			));
		}
		return result;
	} 

	public static List<string> ParseXMLStringList(XElement xElement, string name, string itemName) {

		XElement listX = xElement.Element(name);
		if (listX == null) {
			throw new ErrorHandler.XMLParseError(string.Format(
				"Failed to read {0}: Couldn't find Element List '{1}'. Element Trace: {2}", 
				xElement.Document.BaseUri,
				name,
				ElementTrace(xElement)
			));
		}
		List<string> result = new List<string>();
		foreach (XElement itemX in listX.Elements(itemName)) {
			result.Add(itemX.Value);
		}
		return result;
	}

	public static Dictionary<string, string> ParseXMLStringDictionary(XElement xElement, string name, string itemName, string key) {

		XElement dictionaryX = xElement.Element(name);
		if (dictionaryX == null) {
			throw new ErrorHandler.XMLParseError(string.Format(
				"Failed to read {0}: Couldn't find Element List '{1}'. Element Trace: {2}", 
				xElement.Document.BaseUri,
				name,
				ElementTrace(xElement)
			));
		}
		Dictionary<string, string> result = new Dictionary<string, string>();
		foreach (XElement itemX in dictionaryX.Elements(itemName)) {
			string keyString = ParseXMLAttrString(itemX, key);
			string valueString = itemX.Value;
			result[keyString] = valueString;
		}
		return result;
	}

	public static string ElementTrace(XElement xElement) {
		XElement parent = xElement;
		StringBuilder traceSB = new StringBuilder(ElementXMLString(xElement));

		int recursionLevel = 0;
		int maxRecursionLevel = 16;
		
		while (parent != xElement.Document.Root) {
			parent = parent.Parent;
			traceSB.Insert(0, ElementXMLString(parent) + newLine);
			if (recursionLevel++>=maxRecursionLevel) {
				traceSB.AppendFormat("Maximum recusion level ({0}) reached {1}", maxRecursionLevel, newLine);
				break;
			}
		}
		return traceSB.ToString();
	}

	private static string ElementXMLString(XElement xElement) {
		StringBuilder elementSB = new StringBuilder();
		elementSB.AppendFormat("<{0}", xElement.Name);
		foreach (XAttribute xAttribute in xElement.Attributes()) {
			elementSB.AppendFormat("{0}=\"{1}\"", xAttribute.Name, xAttribute.Value);
		}
		elementSB.AppendFormat(">", xElement.Name);
		return elementSB.ToString();
	}

	public static string ExpandEnvironmentVariables(string input) {
		string output = input.Replace("~", Settings.home);
		output = Environment.ExpandEnvironmentVariables(output);
		return output;
	}

	public static T GetConstant<T>(XElement xElement, string key, Map<string, T> constantDict, bool fromAttribute=false) {
		string name = (fromAttribute)
			? FileIO.ParseXMLAttrString(xElement, key)
			: FileIO.ParseXMLString(xElement, key);
		
		T constant;
		if (!constantDict.TryGetValue(name, out constant)) {
			CustomLogger.LogFormat(
				EL.ERROR,
				"Failed to read {0}. String: {1}", 
				xElement.BaseUri, 
				name
			);
		}
		return constant;
	}

    public static void ThrowXMLError(XElement element, string path, string methodName, System.Exception error) {
        CustomLogger.LogFormat(
            EL.ERROR,
            "Failed to read {0}. Line: {1} (Failed on {2})",
            path,
            ((IXmlLineInfo)element).LineNumber,
            methodName
        );
        CustomLogger.LogOutput(
            "XML Trace: {0}{2} Trace:{1}{2}",
			ElementTrace(element),
            error.ToString(),
            FileIO.newLine
        );
    }

    public static void ThrowXMLError(XElement element, string path, string methodName) {
        CustomLogger.LogFormat(
            EL.ERROR,
            "Failed to read {0}. Line: {1} (Failed on {2})",
            path,
            ((IXmlLineInfo)element).LineNumber,
            methodName
        );
        CustomLogger.LogOutput(
            "XML Trace: {0}{1}",
			ElementTrace(element),
			FileIO.newLine
        );
    }

}
