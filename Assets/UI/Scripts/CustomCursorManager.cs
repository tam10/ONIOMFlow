using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomCursorManager : MonoBehaviour {

	public Texture2D arrow;
	public Texture2D text;

	public void UseArrowCursor() {
		Cursor.SetCursor(arrow, new Vector2(0,0), CursorMode.Auto);
	}

	public void UseTextCursor() {
		Cursor.SetCursor(text, new Vector2(3,10), CursorMode.Auto);
	}
}
