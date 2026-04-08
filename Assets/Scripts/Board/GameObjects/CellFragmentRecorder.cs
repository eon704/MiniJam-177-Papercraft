using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attach alongside CellFragmentGroup.
/// In Play mode: press Record → physics sim runs → data saved to FragmentRecording asset.
/// Fragments need Rigidbody + Collider (add manually or let this script add them temporarily).
/// </summary>
public class CellFragmentRecorder : MonoBehaviour
{
    [Header("Fragment References")]
    [SerializeField] private Transform[] fragments;

    [Header("Physics Settings")]
    [SerializeField] private float mass            = 0.3f;
    [SerializeField] private float drag            = 0.1f;
    [SerializeField] private float angularDrag     = 0.3f;
    [SerializeField] private float staggerDelay    = 0.08f;
    [SerializeField] private float maxInitialSpeed = 4f;   // скорость нижнего фрагмента при активации (верхний = 0)

    [Header("Recording Settings")]
    [SerializeField] private float recordDuration = 2.5f;
    [SerializeField] private float frameInterval  = 0.02f;
    [SerializeField] private float floorOffset    = 3f; // пол ниже основания колонны — дай нижним фрагментам место для падения

    [Header("Output")]
    [SerializeField] private string savePath = "Assets/Game/Prefabs/Cells/FragmentRecording.asset";
    [SerializeField] private FragmentRecording previewRecording; // для тест-воспроизведения

    private bool         _recording;
    private Rigidbody[]  _rbs;
    private Vector3[]    _origLocalPos;
    private Quaternion[] _origLocalRot;

    private void Awake()
    {
        // Запоминаем исходные позиции сразу — чтобы Reset работал и без предварительной записи
        _origLocalPos = new Vector3[fragments.Length];
        _origLocalRot = new Quaternion[fragments.Length];
        for (int i = 0; i < fragments.Length; i++)
        {
            _origLocalPos[i] = fragments[i].localPosition;
            _origLocalRot[i] = fragments[i].localRotation;
        }
    }

    // ──────────────────────────────────────────────
    // PUBLIC ACTIONS (вызывать через Context Menu в Inspector)
    // ──────────────────────────────────────────────

    [ContextMenu("1. Start Recording")]
    public void StartRecording()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Recorder] Only in Play mode."); return; }
        if (_recording)              { Debug.LogWarning("[Recorder] Already recording."); return; }
        StartCoroutine(RecordRoutine());
    }

    [ContextMenu("2. Preview Playback")]
    public void PreviewPlayback()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Recorder] Only in Play mode."); return; }
        if (previewRecording == null){ Debug.LogWarning("[Recorder] Assign previewRecording first."); return; }
        StopAllCoroutines();
        ResetToOriginal();
        StartCoroutine(PreviewRoutine());
    }

    private IEnumerator PreviewRoutine()
    {
        var rec        = previewRecording;
        int frameCount = rec.tracks[0].positions.Count;
        if (frameCount == 0) yield break;

        float t = 0f;
        float totalDuration = (frameCount - 1) * rec.frameInterval;

        while (t < totalDuration)
        {
            float frameF = t / rec.frameInterval;
            int   frameA = Mathf.Clamp(Mathf.FloorToInt(frameF), 0, frameCount - 1);
            int   frameB = Mathf.Clamp(frameA + 1,               0, frameCount - 1);
            float blend  = frameF - frameA;

            for (int i = 0; i < fragments.Length; i++)
            {
                fragments[i].localPosition = Vector3.Lerp(
                    rec.tracks[i].positions[frameA], rec.tracks[i].positions[frameB], blend);
                fragments[i].localRotation = Quaternion.Slerp(
                    rec.tracks[i].rotations[frameA], rec.tracks[i].rotations[frameB], blend);
            }

            t += Time.deltaTime / 3f;
            yield return null;
        }

        // Снэп на последний кадр
        for (int i = 0; i < fragments.Length; i++)
        {
            fragments[i].localPosition = rec.tracks[i].positions[frameCount - 1];
            fragments[i].localRotation = rec.tracks[i].rotations[frameCount - 1];
        }
    }

    [ContextMenu("3. Reset Fragments")]
    public void ResetToOriginal()
    {
        if (_origLocalPos == null) return;
        for (int i = 0; i < fragments.Length; i++)
        {
            fragments[i].localPosition = _origLocalPos[i];
            fragments[i].localRotation = _origLocalRot[i];
        }
    }

    // ──────────────────────────────────────────────
    // RECORDING
    // ──────────────────────────────────────────────

    private IEnumerator RecordRoutine()
    {
        _recording = true;

        // Если у родителя есть Rigidbody — дочерние коллайдеры станут compound и не будут сталкиваться друг с другом
        if (transform.parent != null && transform.parent.GetComponentInParent<Rigidbody>() != null)
            Debug.LogWarning("[Recorder] Parent has Rigidbody — fragments won't collide with each other! Remove it before recording.");

        Debug.Log("[Recorder] Starting...");

        // Сохраняем исходные позиции
        _origLocalPos = new Vector3[fragments.Length];
        _origLocalRot = new Quaternion[fragments.Length];
        for (int i = 0; i < fragments.Length; i++)
        {
            _origLocalPos[i] = fragments[i].localPosition;
            _origLocalRot[i] = fragments[i].localRotation;
        }

        // Порядок задаётся вручную в инспекторе: fragments[0] = низ колонны, последний = верх
        var sorted = fragments;

        // Сохраняем оригинальные слои и переводим на Default (0) — гарантирует self-collision
        int[] originalLayers = new int[fragments.Length];
        for (int i = 0; i < fragments.Length; i++)
        {
            originalLayers[i] = fragments[i].gameObject.layer;
            fragments[i].gameObject.layer = 0; // Default
            Debug.Log($"[Recorder] '{fragments[i].name}' layer: {LayerMask.LayerToName(originalLayers[i])} → Default");
        }

        // Добавляем Rigidbody на все фрагменты — сразу кинематические (не падают)
        _rbs = new Rigidbody[fragments.Length];
        bool[] addedRb = new bool[fragments.Length];
        for (int i = 0; i < fragments.Length; i++)
        {
            _rbs[i] = fragments[i].GetComponent<Rigidbody>();
            if (_rbs[i] == null)
            {
                _rbs[i]    = fragments[i].gameObject.AddComponent<Rigidbody>();
                addedRb[i] = true;
            }
            _rbs[i].mass                   = mass;
            _rbs[i].linearDamping          = drag;
            _rbs[i].angularDamping         = angularDrag;
            _rbs[i].isKinematic            = true;
            _rbs[i].collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // MeshCollider с Rigidbody обязан быть Convex
            var mc = fragments[i].GetComponent<MeshCollider>();
            var bc = fragments[i].GetComponent<BoxCollider>();
            var sc = fragments[i].GetComponent<SphereCollider>();
            Collider col = mc != null ? (Collider)mc : bc != null ? (Collider)bc : sc;

            if (mc != null)
            {
                Debug.Log($"[Recorder] Fragment '{fragments[i].name}': MeshCollider convex={mc.convex} isTrigger={mc.isTrigger} enabled={mc.enabled}");
                mc.convex = true;
            }
            else if (col != null)
                Debug.Log($"[Recorder] Fragment '{fragments[i].name}': {col.GetType().Name} isTrigger={col.isTrigger} enabled={col.enabled}");
            else
                Debug.LogWarning($"[Recorder] Fragment '{fragments[i].name}': NO COLLIDER FOUND");

            // Проверяем родительский Rigidbody (compound collider проблема)
            var parentRb = fragments[i].GetComponentInParent<Rigidbody>();
            if (parentRb != null && parentRb != _rbs[i])
                Debug.LogWarning($"[Recorder] Fragment '{fragments[i].name}': parent '{parentRb.name}' has Rigidbody — compound collider, no self-collision!");
        }

        // Находим нижнюю точку колонны по bounds мешей
        float lowestY = float.MaxValue;
        foreach (var f in fragments)
        {
            var mf = f.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                float bottom = f.TransformPoint(mf.sharedMesh.bounds.min).y;
                if (bottom < lowestY) lowestY = bottom;
            }
        }
        if (lowestY == float.MaxValue) lowestY = -1f;

        // Временный пол прямо под колонной — нижний фрагмент упадёт и остановится,
        // верхние упадут на него → взаимодействие между фрагментами
        var floor = new GameObject("_RecorderFloor");
        floor.transform.position = new Vector3(
            transform.position.x,
            lowestY - floorOffset,
            transform.position.z);
        var floorCol = floor.AddComponent<BoxCollider>();
        floorCol.size   = new Vector3(10f, 0.1f, 10f);
        floorCol.center = Vector3.zero;
        Debug.Log($"[Recorder] Floor placed at y={lowestY - floorOffset:F3} (column bottom={lowestY:F3})");

        yield return new WaitForFixedUpdate();

        // Диапазон индексов для нормализации начальной скорости (0=низ, last=верх)
        float indexRange = Mathf.Max(sorted.Length - 1, 1);

        // Два прохода: сначала каждый второй (0,2,4...), потом пропущенные (1,3,5...)
        // Это даёт неравномерное рассыпание
        var activationOrder = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < sorted.Length; i += 2) activationOrder.Add(sorted[i]); // чётные
        for (int i = 1; i < sorted.Length; i += 2) activationOrder.Add(sorted[i]); // нечётные

        // Пишем кадры и по очереди включаем гравитацию
        var data = new List<(Vector3[] pos, Quaternion[] rot)>();
        float elapsed     = 0f;
        int   nextToFall  = 0;
        float nextFallAt  = 0f;

        while (elapsed < recordDuration)
        {
            // Активируем следующий фрагмент если пришло время
            while (nextToFall < activationOrder.Count && elapsed >= nextFallAt)
            {
                var t2 = activationOrder[nextToFall];
                var rb = t2.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    // Чем ниже фрагмент — тем больше начальная скорость вниз
                    float t01 = 1f - (nextToFall / indexRange); // 1=нижний (index 0), 0=верхний (последний)
                    rb.linearVelocity = Vector3.down * (t01 * maxInitialSpeed);
                    string pass = nextToFall < (activationOrder.Count + 1) / 2 ? "pass1" : "pass2";
                    Debug.Log($"[Recorder] t={elapsed:F2}s [{pass}] — '{t2.name}' idx={nextToFall} v0={t01 * maxInitialSpeed:F2}");
                }
                nextToFall++;
                nextFallAt += staggerDelay;
            }

            // Каждые 0.5с логируем позиции первых двух фрагментов для проверки движения
            if (data.Count % Mathf.RoundToInt(0.5f / Time.fixedDeltaTime) == 1 && fragments.Length >= 2)
                Debug.Log($"[Recorder] pos[0]={fragments[0].localPosition} pos[1]={fragments[1].localPosition}");

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;

            var pos = new Vector3[fragments.Length];
            var rot = new Quaternion[fragments.Length];
            for (int i = 0; i < fragments.Length; i++)
            {
                pos[i] = fragments[i].localPosition;
                rot[i] = fragments[i].localRotation;
            }
            data.Add((pos, rot));
        }

        // Убираем временный пол, Rigidbody и возвращаем оригинальные слои
        Destroy(floor);
        for (int i = 0; i < fragments.Length; i++)
        {
            if (addedRb[i]) Destroy(_rbs[i]);
            fragments[i].gameObject.layer = originalLayers[i];
        }

        _recording = false;
        Debug.Log($"[Recorder] Done. {data.Count} frames recorded.");

        SaveRecording(data);
        ResetToOriginal();
    }

    private static float GetMeshCenterY(Transform t)
    {
        var mf = t.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            return t.TransformPoint(mf.sharedMesh.bounds.center).y; // world space
        return t.position.y;
    }

    private void SaveRecording(List<(Vector3[] pos, Quaternion[] rot)> data)
    {
#if UNITY_EDITOR
        var asset = ScriptableObject.CreateInstance<FragmentRecording>();
        asset.frameInterval = frameInterval;
        asset.tracks = new FragmentTrack[fragments.Length];

        for (int i = 0; i < fragments.Length; i++)
        {
            asset.tracks[i] = new FragmentTrack();
            foreach (var frame in data)
            {
                asset.tracks[i].positions.Add(frame.pos[i]);
                asset.tracks[i].rotations.Add(frame.rot[i]);
            }
        }

        AssetDatabase.CreateAsset(asset, savePath);
        AssetDatabase.SaveAssets();
        previewRecording = asset;
        Debug.Log($"[Recorder] Saved to {savePath}");
#else
        Debug.LogWarning("[Recorder] Saving only works in Editor.");
#endif
    }

}
