using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using TMPro;

public class TextBox3D : MonoBehaviour {

    public TextMeshProUGUI text;
    public Canvas canvas;

    void Awake() {
        canvas.worldCamera = Camera.main;
    }

    void Update() {
        float3 vector = transform.position - Camera.main.transform.position;
        if (math.lengthsq(vector) != 0) {
            transform.rotation = Quaternion.LookRotation(vector);
        }
    }
}
