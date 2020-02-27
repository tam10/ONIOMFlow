using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class GeometryWriter {

    
	public Geometry geometry;
	public bool writeConnectivity;
    
	public List<StringBuilder> fileSections = new List<StringBuilder> ();


	public bool generateAtomMap;
	public Map<AtomID, int> atomMap;
	public int atomNum;

    public bool cancelled;
    
	public delegate bool LineWriter();
	public LineWriter lineWriter = () => false;

    public IEnumerator WriteToFile(string filePath, bool writeConnectivity) {
		this.writeConnectivity = writeConnectivity;

        geometry.path = filePath;
        
		atomNum = 0;

        while (lineWriter()) {
            if (cancelled) {
                yield break;
            }
            if (Timer.yieldNow) {
                yield return null;
            }
        }

        File.WriteAllText(filePath, "");
        foreach (StringBuilder section in fileSections) {
            File.AppendAllText(filePath, section.ToString());
        }

    }


}