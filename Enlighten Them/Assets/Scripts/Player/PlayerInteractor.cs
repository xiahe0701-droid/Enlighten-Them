using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Keyboard Input")]
    [SerializeField] private KeyCode primaryKey = KeyCode.Return;
    [SerializeField] private KeyCode secondaryKey = KeyCode.KeypadEnter;

    [Header("Detection")]
    [SerializeField] private LayerMask interactionRangeLayer;

    [Header("Mouse Input")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private LayerMask interactionButtonLayer;

    private readonly List<GeneratorInteractable> nearbyGenerators =
        new List<GeneratorInteractable>();

    private GeneratorInteractable currentGenerator;
    private bool interactionEnabled = true;

    public GeneratorInteractable CurrentGenerator =>
        currentGenerator;

    private void Awake()
    {
        if (interactionCamera == null)
            interactionCamera = Camera.main;

        if (interactionCamera == null)
        {
            Debug.LogWarning(
                "PlayerInteractor has no interaction camera.",
                this
            );
        }
    }

    private void Update()
    {
        UpdateCurrentGenerator();

        if (!interactionEnabled || currentGenerator == null)
            return;

        if (WasEnterPressed())
        {
            currentGenerator.Interact();
            return;
        }

        if (Input.GetMouseButtonDown(0))
            TryInteractWithMouse();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsLayerInMask(
                other.gameObject.layer,
                interactionRangeLayer))
        {
            return;
        }

        GeneratorInteractable generator =
            other.GetComponentInParent<GeneratorInteractable>();

        if (generator == null)
            return;

        if (!nearbyGenerators.Contains(generator))
            nearbyGenerators.Add(generator);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsLayerInMask(
                other.gameObject.layer,
                interactionRangeLayer))
        {
            return;
        }

        GeneratorInteractable generator =
            other.GetComponentInParent<GeneratorInteractable>();

        if (generator == null)
            return;

        nearbyGenerators.Remove(generator);

        if (currentGenerator == generator)
            ChangeCurrentGenerator(null);
    }

    private void OnDisable()
    {
        foreach (GeneratorInteractable generator in nearbyGenerators)
        {
            if (generator != null)
                generator.SetInteractionAvailable(false);
        }

        nearbyGenerators.Clear();
        currentGenerator = null;
    }

    private bool WasEnterPressed()
    {
        return
            Input.GetKeyDown(primaryKey) ||
            Input.GetKeyDown(secondaryKey);
    }

    private void TryInteractWithMouse()
    {
        if (interactionCamera == null)
            return;

        Ray ray = interactionCamera.ScreenPointToRay(
            Input.mousePosition
        );

        bool didHit = Physics.Raycast(
            ray,
            out RaycastHit hit,
            Mathf.Infinity,
            interactionButtonLayer,
            QueryTriggerInteraction.Collide
        );

        if (!didHit)
            return;

        GeneratorInteractable clickedGenerator =
            hit.collider.GetComponentInParent<GeneratorInteractable>();

        // Only the closest generator currently in range may respond.
        if (clickedGenerator != currentGenerator)
            return;

        if (clickedGenerator.CanInteract)
            clickedGenerator.Interact();
    }

    private void UpdateCurrentGenerator()
    {
        RemoveInvalidGenerators();

        GeneratorInteractable closestGenerator = null;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (GeneratorInteractable generator in nearbyGenerators)
        {
            if (!generator.CanInteract)
                continue;

            float sqrDistance = (
                generator.transform.position -
                transform.position
            ).sqrMagnitude;

            if (sqrDistance >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistance;
            closestGenerator = generator;
        }

        ChangeCurrentGenerator(closestGenerator);
    }

    private void ChangeCurrentGenerator(
        GeneratorInteractable newGenerator)
    {
        if (currentGenerator == newGenerator)
            return;

        if (currentGenerator != null)
            currentGenerator.SetInteractionAvailable(false);

        currentGenerator = newGenerator;

        if (currentGenerator != null && interactionEnabled)
            currentGenerator.SetInteractionAvailable(true);
    }

    private void RemoveInvalidGenerators()
    {
        for (int i = nearbyGenerators.Count - 1; i >= 0; i--)
        {
            GeneratorInteractable generator =
                nearbyGenerators[i];

            if (generator == null ||
                !generator.isActiveAndEnabled)
            {
                nearbyGenerators.RemoveAt(i);
            }
        }
    }

    private static bool IsLayerInMask(
        int objectLayer,
        LayerMask layerMask)
    {
        return (layerMask.value & (1 << objectLayer)) != 0;
    }

    /// <summary>
    /// Called by the puzzle panel to lock or unlock interaction.
    /// </summary>
    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;

        if (currentGenerator != null)
        {
            currentGenerator.SetInteractionAvailable(
                interactionEnabled
            );
        }
    }
}