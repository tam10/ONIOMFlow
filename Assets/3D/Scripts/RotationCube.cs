using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationCube : MonoBehaviour {

    public RotationQuad x;
    public RotationQuad y;
    public RotationQuad z;
    public RotationQuad x_;
    public RotationQuad y_;
    public RotationQuad z_;


    private Transform linkedTransform;
    Vector3 initialPosition = Vector3.zero;

    bool rotating;

    public void Awake() {
        x.SetColour (new Color(0.9f, 0.2f, 0.2f), 0.9f, 0.5f);
        x_.SetColour(new Color(0.4f, 0.0f, 0.0f), 0.9f, 0.5f);
        y.SetColour (new Color(0.2f, 0.9f, 0.2f), 0.9f, 0.5f);
        y_.SetColour(new Color(0.0f, 0.4f, 0.0f), 0.9f, 0.5f);
        z.SetColour (new Color(0.2f, 0.2f, 0.9f), 0.9f, 0.5f);
        z_.SetColour(new Color(0.0f, 0.0f, 0.4f), 0.9f, 0.5f);
    }

    public void LinkTransform(Transform transform, Vector3 initialPosition) {
        this.initialPosition = initialPosition;
        linkedTransform = transform;

        x.mouseDownHandler  = () => {RotateTo(new Vector3( 0,90,0));};
        x_.mouseDownHandler = () => {RotateTo(new Vector3(0,-90,0));};
        y.mouseDownHandler  = () => {RotateTo(new Vector3(-90,0,0));};
        y_.mouseDownHandler = () => {RotateTo(new Vector3(90,0,0));};
        z.mouseDownHandler  = () => {RotateTo(new Vector3(0,180,0));};
        z_.mouseDownHandler = () => {RotateTo(new Vector3(0,0,0));};
        Show();
    }

    public void UnlinkTransform() {
        linkedTransform = null;
        Hide();
    }

    public void RotateTo(Vector3 target) {

        linkedTransform.localPosition = initialPosition;
        linkedTransform.eulerAngles = target;
        
    }

    public void Show() {
        x.Show();
        y.Show();
        z.Show();
        x_.Show();
        y_.Show();
        z_.Show();
    }

    public void Hide() {
        x.Hide();
        y.Hide();
        z.Hide();
        x_.Hide();
        y_.Hide();
        z_.Hide();
    }

    void Update() {
        if (linkedTransform != null) {
            transform.rotation = linkedTransform.rotation;
        }
    }
}
