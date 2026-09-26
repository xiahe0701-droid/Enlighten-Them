using System;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class GeneratorInteractable : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private GameObject interactionButton;

    public event Action Solved;

    public bool IsSolved { get; private set; }
    public bool CanInteract => !IsSolved;

    [Header("Puzzle")]
    [FormerlySerializedAs("puzzlePanel")]
    [SerializeField] private RepairPuzzleBase puzzle;

    [SerializeField]
    private PuzzleDifficulty difficulty =
        PuzzleDifficulty.Easy;
    private void Awake()
    {
        SetInteractionAvailable(false);
    }

    /// <summary>
    /// Called by PlayerInteractor after Enter or a valid mouse click.
    /// </summary>
    public void Interact()
    {
        if (!CanInteract)
        {
            Debug.Log(
                $"{name} has already been repaired.",
                this
            );

            return;
        }

        if (puzzle == null)
        {
            Debug.LogError(
                $"{name} has no repair puzzle assigned.",
                this
            );

            return;
        }

        puzzle.Open(
            difficulty,
            CompleteRepair
        );
    }

    /// <summary>
    /// Shows or hides this generator's world-space interaction button.
    /// </summary>
    public void SetInteractionAvailable(bool available)
    {
        if (interactionButton == null)
            return;

        bool shouldShow = available && CanInteract;

        if (interactionButton.activeSelf != shouldShow)
            interactionButton.SetActive(shouldShow);
    }

    /// <summary>
    /// Called when this generator's puzzle is completed.
    /// </summary>
    public void CompleteRepair()
    {
        if (IsSolved)
            return;

        IsSolved = true;
        SetInteractionAvailable(false);
        Solved?.Invoke();
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Complete Repair")]
    private void DebugCompleteRepair()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode before completing the generator.",
                this
            );

            return;
        }

        CompleteRepair();
    }
#endif
}