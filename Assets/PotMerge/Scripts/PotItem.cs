using System.Collections;
using UnityEngine;

namespace PotMerge
{
    /// <summary>A pot sitting on the board. Holds its level and handles its own juice animations.</summary>
    public class PotItem : MonoBehaviour
    {
        public int Level { get; private set; }
        public Vector2Int Cell { get; set; }
        public bool IsDragging { get; set; }

        Transform visual;
        Vector3 baseScale = Vector3.one;
        Coroutine moveRoutine;
        Coroutine scaleRoutine;
        float bobOffset;

        public void Init(int level, Transform visualRoot, float scale)
        {
            Level = level;
            visual = visualRoot;
            baseScale = Vector3.one * scale;
            visual.localScale = Vector3.zero;
            bobOffset = Random.value * 10f;
        }

        void Update()
        {
            if (visual == null || IsDragging) return;
            // gentle idle sway
            float t = Time.time * 1.6f + bobOffset;
            visual.localRotation = Quaternion.Euler(0f, Mathf.Sin(t) * 4f, Mathf.Sin(t * 0.7f) * 1.5f);
        }

        public void PlaySpawn(float delay = 0f) => StartScale(PopRoutine(delay, 1.25f));
        public void PlayPunch() => StartScale(PopFromCurrent(1.15f, 0.25f));

        public void PlayShake()
        {
            StartCoroutine(ShakeRoutine());
        }

        public void SetLifted(bool lifted)
        {
            IsDragging = lifted;
            if (lifted && moveRoutine != null) { StopCoroutine(moveRoutine); moveRoutine = null; }
            if (visual != null) visual.localRotation = Quaternion.identity;
            StartScale(ScaleTo(lifted ? baseScale * 1.12f : baseScale, 0.1f));
        }

        public void MoveTo(Vector3 target, float duration = 0.15f, System.Action onDone = null)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(MoveRoutine(target, duration, onDone));
        }

        void StartScale(IEnumerator r)
        {
            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            scaleRoutine = StartCoroutine(r);
        }

        IEnumerator MoveRoutine(Vector3 target, float duration, System.Action onDone)
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, duration);
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                transform.position = Vector3.LerpUnclamped(start, target, e);
                yield return null;
            }
            transform.position = target;
            moveRoutine = null;
            onDone?.Invoke();
        }

        IEnumerator PopRoutine(float delay, float overshoot)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.35f;
                visual.localScale = baseScale * BackOut(Mathf.Clamp01(t), overshoot);
                yield return null;
            }
            visual.localScale = baseScale;
        }

        IEnumerator PopFromCurrent(float peak, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float k = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                visual.localScale = new Vector3(baseScale.x * (1f + (peak - 1f) * k), baseScale.y * (1f - (peak - 1f) * 0.6f * k), baseScale.z * (1f + (peak - 1f) * k));
                yield return null;
            }
            visual.localScale = baseScale;
        }

        IEnumerator ScaleTo(Vector3 target, float duration)
        {
            Vector3 start = visual.localScale;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                visual.localScale = Vector3.Lerp(start, target, Mathf.Clamp01(t));
                yield return null;
            }
            visual.localScale = target;
        }

        IEnumerator ShakeRoutine()
        {
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                visual.localPosition = new Vector3(Mathf.Sin(t * 70f) * 0.08f * (1f - t / 0.3f), 0f, 0f);
                yield return null;
            }
            visual.localPosition = Vector3.zero;
        }

        static float BackOut(float t, float s)
        {
            s = (s - 1f) * 10f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
