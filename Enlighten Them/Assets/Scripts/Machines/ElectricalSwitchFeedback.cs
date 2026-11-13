using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ElectricalSwitchFeedback : MonoBehaviour
{
    [SerializeField] private GeneratorInteractable sourceGenerator;
    [SerializeField] private Transform switchTransform;
    [SerializeField] private Vector3 poweredLocalEulerOffset =
        new Vector3(-35f, 0f, 0f);
    [SerializeField, Min(0f)] private float duration = 0.25f;

    private Quaternion unpoweredRotation;
    private Coroutine rotationRoutine;

    private void Awake()
    {
        if (switchTransform != null)
            unpoweredRotation = switchTransform.localRotation;
    }

    private void OnEnable()
    {
        if (sourceGenerator == null)
            return;

        sourceGenerator.Solved += ShowPowered;

        if (sourceGenerator.IsSolved)
            ShowPowered();
    }

    private void OnDisable()
    {
        if (sourceGenerator != null)
            sourceGenerator.Solved -= ShowPowered;

        if (rotationRoutine != null)
        {
            StopCoroutine(rotationRoutine);
            rotationRoutine = null;
        }
    }

    private void ShowPowered()
    {
        if (switchTransform == null)
            return;

        Quaternion poweredRotation =
            unpoweredRotation * Quaternion.Euler(poweredLocalEulerOffset);

        if (rotationRoutine != null)
            StopCoroutine(rotationRoutine);

        rotationRoutine = StartCoroutine(
            RotateSwitch(poweredRotation)
        );
    }

    private IEnumerator RotateSwitch(Quaternion targetRotation)
    {
        Quaternion startRotation = switchTransform.localRotation;

        if (duration <= 0f)
        {
            switchTransform.localRotation = targetRotation;
            rotationRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            switchTransform.localRotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                Mathf.SmoothStep(0f, 1f, t)
            );

            yield return null;
        }

        switchTransform.localRotation = targetRotation;
        rotationRoutine = null;
    }
}
