using System.Collections;
using DG.Tweening;
using UnityEngine;

public class CellFragmentGroup : MonoBehaviour
{
    [SerializeField] private Transform[] fragments;

    [Header("Baked Playback (приоритет если назначен)")]
    [SerializeField] private FragmentRecording recording;

    [Header("Procedural Fallback (если recording не назначен)")]
    [SerializeField] private float fallDepth  = 3.5f;
    [SerializeField] private float duration   = 1.4f;
    [SerializeField] private float maxStagger = 0.35f;

    private Vector3[]    _origPos;
    private Quaternion[] _origRot;
    private float[]      _collapseDelays;
    private Sequence     _seq;
    private Coroutine    _playbackCoroutine;

    private void Awake()
    {
        _origPos = new Vector3[fragments.Length];
        _origRot = new Quaternion[fragments.Length];
        for (int i = 0; i < fragments.Length; i++)
        {
            _origPos[i] = fragments[i].localPosition;
            _origRot[i] = fragments[i].localRotation;
        }
    }

    // ──────────────────────────────────────────────
    // COLLAPSE
    // ──────────────────────────────────────────────

    public void PlayCollapse()
    {
        StopPlayback();

        if (recording != null)
            _playbackCoroutine = StartCoroutine(PlayRecordingRoutine(recording, forward: true, onComplete: null));
        else
            PlayProceduralCollapse();
    }

    private void PlayProceduralCollapse()
    {
        _seq?.Kill();
        _seq = DOTween.Sequence();

        _collapseDelays = new float[fragments.Length];
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < fragments.Length; i++)
        {
            if (_origPos[i].y < minY) minY = _origPos[i].y;
            if (_origPos[i].y > maxY) maxY = _origPos[i].y;
        }
        float yRange = Mathf.Max(maxY - minY, 0.001f);
        for (int i = 0; i < fragments.Length; i++)
        {
            float t = (_origPos[i].y - minY) / yRange;
            _collapseDelays[i] = t * maxStagger;
        }

        for (int i = 0; i < fragments.Length; i++)
        {
            Vector3 target = _origPos[i] + Vector3.down * fallDepth;
            Vector3 rot    = new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
            int idx = i;
            _seq.Insert(_collapseDelays[idx], fragments[idx].DOLocalMove(target, duration).SetEase(Ease.InCubic));
            _seq.Insert(_collapseDelays[idx], fragments[idx].DOLocalRotate(rot, duration, RotateMode.LocalAxisAdd).SetEase(Ease.InQuad));
        }

        _seq.Insert(duration * 0.6f, transform.DOScale(Vector3.zero, duration * 0.4f).SetEase(Ease.InQuad));
    }

    // ──────────────────────────────────────────────
    // RESET (undo)
    // ──────────────────────────────────────────────

    public void ResetFragments(bool animated = false, System.Action onComplete = null)
    {
        StopPlayback();
        _seq?.Kill();
        _seq = null;
        transform.localScale = Vector3.one;

        if (!animated)
        {
            SnapToOriginal();
            onComplete?.Invoke();
            return;
        }

        if (recording != null)
        {
            // Воспроизводим запись в обратном направлении, быстрее
            _playbackCoroutine = StartCoroutine(PlayRecordingRoutine(recording, forward: false, onComplete: onComplete));
        }
        else
        {
            PlayProceduralReset(onComplete);
        }
    }

    private void PlayProceduralReset(System.Action onComplete)
    {
        if (_collapseDelays == null) { SnapToOriginal(); onComplete?.Invoke(); return; }

        float maxDelay = 0f;
        for (int i = 0; i < _collapseDelays.Length; i++)
            if (_collapseDelays[i] > maxDelay) maxDelay = _collapseDelays[i];

        float resetDur = duration * 0.55f * 0.3f;
        _seq = DOTween.Sequence();

        for (int i = 0; i < fragments.Length; i++)
        {
            float reverseDelay = maxDelay - _collapseDelays[i];
            int   idx          = i;
            _seq.Insert(reverseDelay, fragments[idx].DOLocalMove(_origPos[idx], resetDur).SetEase(Ease.OutBack));
            _seq.Insert(reverseDelay, fragments[idx].DOLocalRotate(Vector3.zero, resetDur, RotateMode.Fast).SetEase(Ease.OutCubic));
        }

        if (onComplete != null) _seq.OnComplete(() => onComplete());
    }

    // ──────────────────────────────────────────────
    // BAKED PLAYBACK
    // ──────────────────────────────────────────────

    private IEnumerator PlayRecordingRoutine(FragmentRecording rec, bool forward, System.Action onComplete)
    {
        int frameCount = rec.tracks[0].positions.Count;
        if (frameCount == 0) { onComplete?.Invoke(); yield break; }

        // Скорость воспроизведения: forward = нормально, reverse = 70% быстрее (x3.3)
        float speed = forward ? 1f : 3.3f;

        float t = forward ? 0f : (frameCount - 1) * rec.frameInterval;
        float end = forward ? (frameCount - 1) * rec.frameInterval : 0f;

        while (forward ? t < end : t > end)
        {
            SampleRecording(rec, t);
            t += (forward ? 1f : -1f) * speed * Time.deltaTime;
            yield return null;
        }

        // Снэп к конечному кадру
        int lastFrame = forward ? frameCount - 1 : 0;
        for (int i = 0; i < fragments.Length; i++)
        {
            fragments[i].localPosition = rec.tracks[i].positions[lastFrame];
            fragments[i].localRotation = rec.tracks[i].rotations[lastFrame];
        }

        // При обратном воспроизведении (undo) — сразу снэп на оригинальные позиции
        if (!forward) SnapToOriginal();

        onComplete?.Invoke();
    }

    private void SampleRecording(FragmentRecording rec, float t)
    {
        float frameF = t / rec.frameInterval;
        int   frameA = Mathf.Clamp(Mathf.FloorToInt(frameF), 0, rec.tracks[0].positions.Count - 1);
        int   frameB = Mathf.Clamp(frameA + 1,               0, rec.tracks[0].positions.Count - 1);
        float blend  = frameF - frameA;

        for (int i = 0; i < fragments.Length; i++)
        {
            fragments[i].localPosition = Vector3.Lerp(
                rec.tracks[i].positions[frameA],
                rec.tracks[i].positions[frameB], blend);
            fragments[i].localRotation = Quaternion.Slerp(
                rec.tracks[i].rotations[frameA],
                rec.tracks[i].rotations[frameB], blend);
        }
    }

    // ──────────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────────

    private void SnapToOriginal()
    {
        for (int i = 0; i < fragments.Length; i++)
        {
            fragments[i].localPosition = _origPos[i];
            fragments[i].localRotation = _origRot[i];
        }
    }

    private void StopPlayback()
    {
        if (_playbackCoroutine != null)
        {
            StopCoroutine(_playbackCoroutine);
            _playbackCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        _seq?.Kill();
        StopPlayback();
    }
}
