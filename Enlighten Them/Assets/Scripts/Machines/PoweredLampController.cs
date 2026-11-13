using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PoweredLampController : MonoBehaviour
{
    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    [Header("Power Source")]
    [SerializeField] private GeneratorInteractable sourceGenerator;

    [Header("Lamp Output")]
    [SerializeField] private Light[] lampLights;
    [SerializeField] private Renderer[] emissiveRenderers;
    [SerializeField] private EnemyLightSource enemyLightSource;

    [Header("Power-On Look")]
    [SerializeField, Min(0f)] private float maximumLightIntensity = 4f;
    [SerializeField] private Color emissionColor =
        new Color(1f, 0.58f, 0.18f, 1f);
    [SerializeField, Min(0f)] private float maximumEmissionIntensity = 2.5f;
    [SerializeField, Min(0f)] private float transitionDuration = 0.8f;
    [SerializeField] private AnimationCurve transitionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private MaterialPropertyBlock propertyBlock;

    private Coroutine transitionRoutine;
    private float currentPower;

    public bool IsPowered { get; private set; }
    public GeneratorInteractable SourceGenerator => sourceGenerator;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ApplyPower(0f);
    }

    private void OnEnable()
    {
        if (sourceGenerator == null)
            return;

        sourceGenerator.Solved += TurnOn;

        if (sourceGenerator.IsSolved)
            TurnOn();
    }

    private void OnDisable()
    {
        if (sourceGenerator != null)
            sourceGenerator.Solved -= TurnOn;

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
    }

    public void TurnOn()
    {
        SetPowered(true);
    }

    public void TurnOff()
    {
        SetPowered(false);
    }

    public void SetPowered(bool powered)
    {
        if (IsPowered == powered && transitionRoutine == null)
            return;

        IsPowered = powered;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(
            AnimatePower(powered ? 1f : 0f)
        );
    }

    private IEnumerator AnimatePower(float targetPower)
    {
        float startPower = currentPower;

        if (transitionDuration <= 0f)
        {
            ApplyPower(targetPower);
            transitionRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(
                elapsed / transitionDuration
            );

            float curvedTime = transitionCurve.Evaluate(normalizedTime);
            ApplyPower(Mathf.Lerp(startPower, targetPower, curvedTime));

            yield return null;
        }

        ApplyPower(targetPower);
        transitionRoutine = null;
    }

    private void ApplyPower(float power)
    {
        currentPower = Mathf.Clamp01(power);

        if (lampLights != null)
        {
            for (int i = 0; i < lampLights.Length; i++)
            {
                Light lampLight = lampLights[i];
                if (lampLight == null)
                    continue;

                lampLight.enabled = currentPower > 0.001f;
                lampLight.intensity =
                    maximumLightIntensity * currentPower;
            }
        }

        Color poweredEmission =
            emissionColor * maximumEmissionIntensity * currentPower;

        if (emissiveRenderers != null)
        {
            for (int i = 0; i < emissiveRenderers.Length; i++)
            {
                Renderer targetRenderer = emissiveRenderers[i];
                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, poweredEmission);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        if (enemyLightSource != null)
        {
            // The visual fade begins immediately, but moths only react once
            // the lamp is almost fully powered.
            enemyLightSource.SetAttracting(
                IsPowered && currentPower >= 0.9f
            );
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Power On")]
    private void DebugPowerOn()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode before testing the powered lamp.",
                this
            );
            return;
        }

        TurnOn();
    }

    [ContextMenu("Debug/Power Off")]
    private void DebugPowerOff()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode before testing the powered lamp.",
                this
            );
            return;
        }

        TurnOff();
    }
#endif
}
