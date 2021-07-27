﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class AtomCollider : MonoBehaviour {
    
    public Mesh mesh;

    private bool isEligible;
    private bool isActive;

    void Awake() {
        this.mesh = this.GetComponent<MeshFilter>().mesh;
    }

    public void Set(bool isEligible, bool isActive) {
        this.isEligible = isEligible;
        this.isActive = isActive;
        Color color;
        if (isEligible) {
            color = (isActive) ? Color.green : Color.blue;
        } else {
            color = Color.red;
            transform.localScale = transform.localScale / 2f;
        }
        SetColor(color);
    }

    public void SetColor(Color color) {
        if (mesh == null) {return;}
        Color[] colors = Enumerable.Repeat(color, mesh.vertices.Count()).ToArray();
        mesh.colors = colors;
    }

    public Color GetColor() {
        return mesh.colors.First();
    }

    public void OnTriggerEnter(Collider other) {
        if (other == null) {return;}
        if (isEligible) {
            if (isActive && other.tag == "Pointer") {
                AtomsVisualiser.NextEligible();
                Color fadeTo = new Color(1f, 1f, 1f, 0f);
                StartCoroutine(FadeAndDestroy(fadeTo, 1f));
            }
        } else {
            if (other.tag == "Pointer") {
                StartCoroutine(AtomsVisualiser.ExitCoroutine());
            } else {
                Color fadeTo = new Color(1f, 1f, 1f, 0f);
                StartCoroutine(FadeAndDestroy(fadeTo, 1f));
            }
        }
    }

    public IEnumerator FadeAndDestroy(Color fadeTo, float fadeTime) {
        SphereCollider collider = GetComponent<SphereCollider>();
        if (collider == null) {yield break;}
        collider.enabled = false;

        Color originalColor = mesh.colors[0];
        yield return Fade(originalColor, fadeTo, fadeTime);

        if (this != null) {
            GameObject.Destroy(gameObject);
        }
    }

    public IEnumerator Fade(Color fadeFrom, Color fadeTo, float fadeTime) {

        float timer = 0f;
        while (timer < fadeTime) {
            SetColor(Color.Lerp(fadeFrom, fadeTo, timer / fadeTime));
            timer += Time.deltaTime;
            yield return null;
        }
        SetColor(fadeTo);
    }

}
