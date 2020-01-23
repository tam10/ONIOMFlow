using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationQuad : MonoBehaviour {
    
    public delegate void MouseDownHandler();
    public MouseDownHandler mouseDownHandler = () => {};

    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;
    public Mesh mesh;

    Color colour = Color.white;
    float hoveredAlpha = 0f;
    float notHoveredAlpha = 0f;

    void Awake() {
        mesh = meshFilter.mesh;
    }

    public void OnMouseDown() {
        mouseDownHandler();
    }

    public void Show() {
        meshRenderer.enabled = true;
    }

    public void Hide() {
        meshRenderer.enabled = false;
    }

    public void SetColour(Color colour, float hoveredAlpha, float notHoveredAlpha) {
        this.colour = colour;
        this.hoveredAlpha = hoveredAlpha;
        this.notHoveredAlpha = notHoveredAlpha;
        this.colour.a = notHoveredAlpha;
        SetColours();
    }

    public void OnMouseEnter() {
        this.colour.a = hoveredAlpha;
        SetColours();
    }

    public void OnMouseExit() {
        this.colour.a = notHoveredAlpha;
        SetColours();
    }

    void SetColours() {
        int count = mesh.vertexCount;
        Color[] colours = new Color[count];
        for (int i=0; i<count; i++) {
            colours[i] = colour;
        }
        mesh.SetColors(colours);
    }
    
}
