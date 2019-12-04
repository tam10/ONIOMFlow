using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Plotter : MonoBehaviour {
    
    private static Plotter _main;
    public static Plotter main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<Plotter>();
            return _main;
        }
    }

    public Canvas canvas;
    public RectTransform lineHolder;
    public LineRenderer lineRendererPrefab;

    List<(LineRenderer, Queue<float>)> plots = new List<(LineRenderer, Queue<float>)>();

    List<(LineRenderer, float)> horizontalLinePlots = new List<(LineRenderer, float)>();
    List<(LineRenderer, float)> verticalLinePlots = new List<(LineRenderer, float)>();

    float yMax = 0f;

    int maxPoints = 100;


    public void Show() {
        canvas.enabled = true;
    }

    public void Hide() {
        canvas.enabled = false;
        Clear();
    }

    public void Clear() {

        foreach (Transform child in lineHolder) {
            GameObject.Destroy(child.gameObject);
        }

        plots = new List<(LineRenderer, Queue<float>)>();

        horizontalLinePlots = new List<(LineRenderer, float)>();
        verticalLinePlots = new List<(LineRenderer, float)>();
    }

    public int AddHorizontalLine(Color startColor, Color endColor, float value) {

        int axisNum = horizontalLinePlots.Count;
        
        LineRenderer lineRenderer = AddLineRenderer(startColor, endColor, 0.1f, 0.1f);
        lineRenderer.positionCount = 2;
        lineRenderer.material = ColorScheme.GetLineGlowMaterial();
        horizontalLinePlots.Add((lineRenderer, value));

        return axisNum;

    }

    public int AddVerticalLine(Color startColor, Color endColor, float value) {

        int axisNum = verticalLinePlots.Count;

        LineRenderer lineRenderer = AddLineRenderer(startColor, endColor, 0.1f, 0.1f);
        lineRenderer.positionCount = 2;
        lineRenderer.material = ColorScheme.GetLineGlowMaterial();
        verticalLinePlots.Add((lineRenderer, value));
        
        return axisNum;

    }

    public int AddAxis(Color startColor, Color endColor) {

        int axisNum = plots.Count;

        LineRenderer lineRenderer = AddLineRenderer(startColor, endColor, 0.1f, 0.1f);
        lineRenderer.material = ColorScheme.GetTrailMaterial();
        plots.Add((lineRenderer, new Queue<float>()));

        return axisNum;
    }

    private LineRenderer AddLineRenderer(Color startColor, Color endColor, float startWidth, float endWidth) {
        LineRenderer lineRenderer = Instantiate<LineRenderer>(lineRendererPrefab, lineHolder);

        lineRenderer.startColor = startColor;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endColor = endColor;
        lineRenderer.endWidth = endWidth;

        return lineRenderer;
    }

    public void AddPoint(int axisNum, float value) {
        Queue<float> values = plots[axisNum].Item2;
        values.Enqueue(value);
        if (values.Count > maxPoints) {values.Dequeue();}
    }

    float GetYMaxHorizontal() {
        if (horizontalLinePlots.Count == 0) {return 0f;}
        return horizontalLinePlots.Select(x => x.Item2).Max();
    }

    float GetYMaxPlots() {
        if (plots.Count == 0) {return 0f;}
        return plots.Select(x => x.Item2.Count == 0 ? 0f : x.Item2.Max()).Max();
    }

    void Update() {

        if (!canvas.enabled) {return;}

        yMax = Mathf.Max(
            GetYMaxHorizontal(), 
            GetYMaxPlots()
        );

        float lineHolderHeight = lineHolder.rect.height / lineRendererPrefab.transform.localScale.y;
        float lineHolderWidth = lineHolder.rect.width / lineRendererPrefab.transform.localScale.x;

        foreach ((LineRenderer lineRenderer, float value) in horizontalLinePlots) {
            float yPos = lineHolderHeight * value / yMax;
            lineRenderer.SetPosition(0, new Vector3(0f, yPos, 0f));
            lineRenderer.SetPosition(1, new Vector3(lineHolderWidth, yPos, 0f));
        }
        

        foreach ((LineRenderer lineRenderer, Queue<float> values) in plots) {
            int index = 0;
            int count = 0;
            count = values.Count;
            lineRenderer.positionCount = count;
            lineRenderer.SetPositions(
                values
                    .Select(x => new Vector3(
                        lineHolderWidth * (float)index++ / count, 
                        lineHolderHeight * x / yMax,
                        0f)
                    )
                    .ToArray()
            );
            
        }
    }
}
