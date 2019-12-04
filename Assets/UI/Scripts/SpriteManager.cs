using UnityEngine.UI;
using UnityEngine;

public class SpriteManager : MonoBehaviour {

    private static SpriteManager _main;
	public static SpriteManager main {
		get {
			if (_main == null) {
				_main = GameObject.FindObjectOfType<SpriteManager>();
			}
			return _main;
		}
	}

    public Sprite _whiteCircle;
    public Sprite _whiteTriangle;

	public Sprite _dragIcon;
	public Sprite _linkedIcon;
	public Sprite _unlinkedIcon;
	public Sprite _springIcon;

    public static Sprite whiteCircle => main._whiteCircle;
    public static Sprite whiteTriangle => main._whiteTriangle;

    public static Sprite dragIcon => main._dragIcon;
	public static Sprite linkedIcon => main._linkedIcon;
	public static Sprite unlinkedIcon => main._unlinkedIcon;
	public static Sprite springIcon => main._springIcon;

}