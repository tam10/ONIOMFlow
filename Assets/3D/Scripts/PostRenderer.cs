using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PostRenderer : MonoBehaviour {

	private static PostRenderer _main;
	public static PostRenderer main {
		get {
			if (_main == null) {
				_main = GameObject.FindObjectOfType<PostRenderer>();
			}
			return _main;
		}
	}

    public List<LineDrawer> lineDrawers;

	void OnPostRender() {
        foreach (LineDrawer lineDrawer in lineDrawers) {
            if (lineDrawer != null) lineDrawer.DrawGLConnections();
        }
	}
}
