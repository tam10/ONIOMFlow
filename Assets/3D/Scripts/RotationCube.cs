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

    public void Awake() {
        x.SetColour (new Color(0.9f, 0.2f, 0.2f), 0.9f, 0.5f);
        x_.SetColour(new Color(0.4f, 0.0f, 0.0f), 0.9f, 0.5f);
        y.SetColour (new Color(0.2f, 0.9f, 0.2f), 0.9f, 0.5f);
        y_.SetColour(new Color(0.0f, 0.4f, 0.0f), 0.9f, 0.5f);
        z.SetColour (new Color(0.2f, 0.2f, 0.9f), 0.9f, 0.5f);
        z_.SetColour(new Color(0.0f, 0.0f, 0.4f), 0.9f, 0.5f);
    }

    /// <summary>Shows the cube and follows the rotation of a Transform.</summary>
    /// <param name="transform">The Transform to follow.</param>
    public void LinkTransform(Transform transform) {
        this.initialPosition = transform.localPosition;

        //This is the transform to follow in Update()
        linkedTransform = transform;

        //Callbacks for when faces of cube are clicked
        // x+ Rotate 90 deg around Y to look at right side
        x.mouseDownHandler  = () => {RotateTo(new Vector3( 0,90,0));};
        // x- Rotate -90 deg around Y to look at left side
        x_.mouseDownHandler = () => {RotateTo(new Vector3(0,-90,0));};
        // y+ Rotate -90 deg around X to look at top side
        y.mouseDownHandler  = () => {RotateTo(new Vector3(-90,0,0));};
        // y- Rotate 90 deg around X to look at bottom side
        y_.mouseDownHandler = () => {RotateTo(new Vector3(90,0,0));};
        // z+ Rotate 180 deg around Y to look at back side
        z.mouseDownHandler  = () => {RotateTo(new Vector3(0,180,0));};
        // z- No rotation to look at front side
        z_.mouseDownHandler = () => {RotateTo(new Vector3(0,0,0));};

        //Show all the sides
        Show();
    }

    /// <summary>Stops following the rotation of a Transform and hides the cube.</summary>
    public void UnlinkTransform() {
        linkedTransform = null;
        Hide();
    }

    /// <summary>Snaps the rotation of the linked transform (and this transform after Update()) to eulerAngles.</summary>
    /// <param name="eulerAngles">The ordered rotations about the X, Y and Z axes.</param>
    public void RotateTo(Vector3 eulerAngles) {

        if (linkedTransform == null) {
            return;
        }

        linkedTransform.localPosition = initialPosition;
        linkedTransform.eulerAngles = eulerAngles;
        
    }

    /// <summary>Shows the faces of the cube.</summary>
    public void Show() {
        x?.Show();
        y?.Show();
        z?.Show();
        x_?.Show();
        y_?.Show();
        z_?.Show();
    }

    /// <summary>Hides the faces of the cube.</summary>
    public void Hide() {
        x?.Hide();
        y?.Hide();
        z?.Hide();
        x_?.Hide();
        y_?.Hide();
        z_?.Hide();
    }

    /// <summary>Update is called once per frame - called by Unity.</summary>
    void Update() {
        if (linkedTransform != null) {
            transform.rotation = linkedTransform.rotation;
        }
    }
}
