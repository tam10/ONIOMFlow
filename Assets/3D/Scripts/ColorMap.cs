using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ColorMap {
	private static Color[] colors = new Color[6] {
		new Color(0f, 0f, 0.5f, 1f),
		new Color(0f, 0f, 1f, 0.9f),
		new Color(0f, 1f, 1f, 0.8f),
		new Color(1f, 1f, 0f, 0.6f),
		new Color(1f, 0f, 0f, 0.4f),
		new Color(0.5f, 0f, 0f, 0.2f)
	};

	private static float[] keys = new float[6] {
		0f, 0.1f, 0.35f, 0.65f, 0.9f, 1f
	};

	public static Color GetColor(float t) {
		Color color = colors[5];
		for (int i = 0; i < 5; i++) {
			if (t <= keys[i]) {
				color = Color.Lerp(colors[i], colors[i+1], (t-keys[i+1]) / (keys[i+1] - keys[i]));
				break;
			}
		}
		return color;
	}
}
