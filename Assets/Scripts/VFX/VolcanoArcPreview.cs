using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws a dashed parabolic arc from a volcano to its next lava target,
/// rendered on top of all geometry (ZTest Always).
/// Assign a simple Unlit/Transparent (Additive) material in GameController to enable.
/// </summary>
public class VolcanoArcPreview : MonoBehaviour
{
    private const int   DashCount      = 6;
    private const float DashFraction   = 0.09f;
    private const int   PointsPerDash  = 5;
    private const float LineWidth      = 0.10f;
    private const float PulsePeriod    = 0.65f;

    private static readonly Color CoreNormal = new(1.00f, 0.60f, 0.00f, 1.00f);
    private static readonly Color CoreUrgent = new(1.00f, 0.15f, 0.00f, 1.00f);
    private static readonly Color DimNormal  = new(1.00f, 0.60f, 0.00f, 0.08f);
    private static readonly Color DimUrgent  = new(1.00f, 0.15f, 0.00f, 0.10f);

    private readonly List<GameObject> _dashObjects = new();
    private Material _matInstance;

    public void Draw(Vector3 from, Vector3 to, float arcHeight, int countdown, Material material)
    {
        Clear();

        // Instance so we can set ZTest without touching the shared material
        _matInstance = new Material(material);
        _matInstance.SetInt("_ZTest", (int)CompareFunction.Always);
        _matInstance.renderQueue = 4000; // Overlay — draws on top of everything

        bool urgent   = countdown <= 1;
        Color bright  = urgent ? CoreUrgent : CoreNormal;
        Color dim     = urgent ? DimUrgent  : DimNormal;

        // Control point shifted toward source → nearly vertical launch, flattens toward target
        Vector3 mid = Vector3.Lerp(from, to, 0.15f) + Vector3.up * arcHeight;

        for (int i = 0; i < DashCount; i++)
        {
            float tStart = (float)i / DashCount;
            float tEnd   = Mathf.Min(tStart + DashFraction, 1f);

            Vector3[] pts = SampleBezier(from, mid, to, tStart, tEnd, PointsPerDash);
            float delay   = (float)i / DashCount * PulsePeriod;

            var go = new GameObject($"arc_{i}");
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace        = true;
            lr.positionCount        = pts.Length;
            lr.startWidth           = LineWidth;
            lr.endWidth             = LineWidth;
            lr.material             = _matInstance;
            lr.shadowCastingMode    = ShadowCastingMode.Off;
            lr.receiveShadows       = false;
            lr.generateLightingData = false;
            lr.numCapVertices       = 2;
            // Render after all sprites regardless of their sorting layer
            var topLayer = SortingLayer.layers[SortingLayer.layers.Length - 1];
            lr.sortingLayerID = topLayer.id;
            lr.sortingOrder   = 32767;
            lr.SetPositions(pts);
            lr.startColor = lr.endColor = bright;

            DOTween.To(
                    () => lr.startColor,
                    col => { lr.startColor = col; lr.endColor = col; },
                    dim,
                    PulsePeriod * 0.5f)
                .SetDelay(delay)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(go);

            _dashObjects.Add(go);
        }
    }

    public void Clear()
    {
        foreach (var go in _dashObjects)
            if (go != null) Destroy(go);
        _dashObjects.Clear();

        if (_matInstance != null)
        {
            Destroy(_matInstance);
            _matInstance = null;
        }
    }

    private void OnDestroy() => Clear();

    private static Vector3[] SampleBezier(Vector3 a, Vector3 b, Vector3 c,
                                           float tStart, float tEnd, int count)
    {
        var pts = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Lerp(tStart, tEnd, (float)i / (count - 1));
            float u = 1f - t;
            pts[i] = u * u * a + 2f * u * t * b + t * t * c;
        }
        return pts;
    }
}
