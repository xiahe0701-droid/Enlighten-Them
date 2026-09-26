using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DoorController : MonoBehaviour
{
    [Header("Signal Source")]
    [SerializeField]
    private GeneratorInteractable sourceGenerator;

    [Header("Rotation")]
    [SerializeField] private float openAngle = -90f;
    [SerializeField, Min(0f)] private float duration = 1f;

    [Header("Animation")]
    [SerializeField]
    private AnimationCurve rotationCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Coroutine rotationRoutine;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        closedRotation = transform.localRotation;

        openRotation =
            closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    private void OnEnable()
    {
        if (sourceGenerator == null)
            return;

        sourceGenerator.Solved += Open;

        // Handles cases where this door is enabled after
        // the generator has already been repaired.
        if (sourceGenerator.IsSolved)
            Open();
    }

    private void OnDisable()
    {
        if (sourceGenerator != null)
            sourceGenerator.Solved -= Open;

        if (rotationRoutine != null)
        {
            StopCoroutine(rotationRoutine);
            rotationRoutine = null;
        }
    }

    public void Open()
    {
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    private void SetOpen(bool shouldOpen)
    {
        Quaternion targetRotation =
            shouldOpen ? openRotation : closedRotation;

        bool alreadyAtTarget =
            Quaternion.Angle(
                transform.localRotation,
                targetRotation
            ) < 0.01f;

        if (alreadyAtTarget && rotationRoutine == null)
        {
            IsOpen = shouldOpen;
            return;
        }

        IsOpen = shouldOpen;

        if (rotationRoutine != null)
            StopCoroutine(rotationRoutine);

        rotationRoutine =
            StartCoroutine(RotateTo(targetRotation));
    }

    private IEnumerator RotateTo(Quaternion targetRotation)
    {
        Quaternion startRotation = transform.localRotation;

        if (duration <= 0f)
        {
            transform.localRotation = targetRotation;
            rotationRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(elapsed / duration);

            float curvedTime =
                rotationCurve.Evaluate(normalizedTime);

            transform.localRotation = Quaternion.SlerpUnclamped(
                startRotation,
                targetRotation,
                curvedTime
            );

            yield return null;
        }

        transform.localRotation = targetRotation;
        rotationRoutine = null;
    }
}