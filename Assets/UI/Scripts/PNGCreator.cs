using System.Collections;
using System.Collections.Generic;
using UnityEngine.PostProcessing;
using UnityEngine;
using EL = Constants.ErrorLevel;

public class PNGCreator : MonoBehaviour {

    private static PNGCreator _main;
    public static PNGCreator main {
        get {
            if (_main == null) {
                _main = (PNGCreator)FindObjectOfType<PNGCreator>();
            };
            return _main;
        }
    }

    int layer;
    public Canvas pngCanvas;
    public Camera renderCamera;
    public RectTransform pngCanvasRT;
    float canvasZ;

    public void Awake() {

        
        renderCamera.cameraType = CameraType.Game;
        pngCanvas.worldCamera = renderCamera;
        renderCamera.forceIntoRenderTexture = true;

        layer = pngCanvas.gameObject.layer;
        renderCamera.cullingMask = (1 << layer);

        canvasZ = renderCamera.transform.position.z + 10f;
    }

    public IEnumerator _ImageFromCanvas(RectTransform canvasRT, string filename, int orthographicSize=5, int padding=10) {

        //Prevent division by zero
        if (orthographicSize < 1) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "Cannot use an orthographic size of {0} for PNG rendering. Must be 1 or greater",
                orthographicSize
            );
            yield break;
        }

        Canvas canvas = canvasRT.GetComponent<Canvas>();

        if (canvas == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "RectTransform is not attached to a Canvas",
                orthographicSize
            );
            yield break;
        }

        Transform oldParentTransform = canvas.transform.parent;

        //Get original canvas properties
        Transform canvasTransform = canvas.transform;

        int canvasWidth = (int)canvasRT.sizeDelta.x;
        int canvasHeight = (int)canvasRT.sizeDelta.y;

        renderCamera.orthographicSize = orthographicSize;
        renderCamera.orthographicSize = orthographicSize;
        int chunkLength = orthographicSize * 2;// * 16;
        //renderCamera.orthographicSize = chunkLength;

        int paddedLength = chunkLength + 2 * padding;
        pngCanvasRT.sizeDelta = new Vector2(paddedLength, paddedLength);

        //Move old canvas to new canvas
        Vector3 oldPosition = canvasTransform.position;
        Vector3 oldScale = canvasTransform.localScale;
        int oldLayer = canvasTransform.gameObject.layer;

        canvasTransform.SetParent(pngCanvas.transform);
        foreach (Transform child in canvasTransform) {
            child.gameObject.layer = layer;
        }

        canvasTransform.localScale = Vector3.one;

        RenderTexture pngTexture = RenderTexture.GetTemporary(chunkLength, chunkLength, 32);
        pngTexture.autoGenerateMips = true;
        pngTexture.Create();
        pngTexture.name = "PNG Texture";
        Texture2D pngImage = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);

        renderCamera.targetTexture = pngTexture;
        RenderTexture.active = pngTexture;

        int jSteps = ((canvasWidth - 1) / chunkLength) + 1;
        int iSteps = ((canvasHeight - 1) / chunkLength) + 1;


        WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

        RenderTexture oldActiveTexture = RenderTexture.active;
        //RenderTexture.active = pngTexture;
        
        for (int i = iSteps - 1; i >= 0; i--) {
            for (int j = jSteps - 1; j >= 0; j--) {

                //Debug.Break();
                //yield return null;

                yield return endOfFrame;

                canvasTransform.position = new Vector3(
                    j * chunkLength, 
                    i * chunkLength, 
                    canvasZ
                );

                //canvasTransform.position = new Vector3(
                //    j * chunkLength, 
                //    10 * chunkLength, 
                //    canvasZ
                //);
                
                renderCamera.targetTexture = pngTexture;
                RenderTexture.active = pngTexture;
                renderCamera.Render();

                pngImage.ReadPixels(new Rect(0, 0, chunkLength, chunkLength), (jSteps - j - 1) * chunkLength, (iSteps - i - 1) * chunkLength, false);
                //pngImage.ReadPixels(new Rect(0, 0, chunkLength, chunkLength), (jSteps - j - 1) * chunkLength, (iSteps - 11) * chunkLength, false);

                //RenderTexture.active = oldActiveTexture;
                pngImage.Apply(true);

                //Debug.Break();
                //yield return null;

                //if (Timer.yieldNow) {
                //    yield return null;
                //}

            }
        }

        byte[] pngBytes = pngImage.EncodeToPNG();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Settings.projectPath, filename), pngBytes);

        canvasTransform.SetParent(oldParentTransform);
        foreach (Transform child in canvasTransform) {
            child.gameObject.layer = oldLayer;
        }

        canvasTransform.position = oldPosition;
        canvasTransform.localScale = oldScale;
    }

    public static IEnumerator ImageFromCanvas(RectTransform canvasRT, string filename, int orthographicSize=5, int padding=10) {

        if (main == null) {
            CustomLogger.LogFormat(
                EL.ERROR,
                "PNG Creator object was destroyed."
            );
            yield break;
        }

        yield return _main._ImageFromCanvas(canvasRT, filename, orthographicSize, padding);


    }
    
}