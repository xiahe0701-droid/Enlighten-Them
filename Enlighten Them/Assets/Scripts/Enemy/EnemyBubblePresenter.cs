using System.Collections;
using UnityEngine;

public class EnemyBubblePresenter : MonoBehaviour
{
    [SerializeField] private GameObject bubbleRoot;
    [SerializeField] private SpriteRenderer iconRenderer;

    [Header("State Icons")]
    [SerializeField] private Sprite noticedIcon;
    [SerializeField] private Sprite suspiciousIcon;
    [SerializeField] private Sprite confirmedIcon;
    [SerializeField] private Sprite attractedToLightIcon;
    [SerializeField] private Sprite inspectingLightIcon;

    [Min(0.05f)] [SerializeField] private float displayDuration = 1.1f;

    private Coroutine hideRoutine;

    private void Awake()
    {
        if (bubbleRoot == null && iconRenderer != null)
            bubbleRoot = iconRenderer.gameObject;

        HideImmediate();
    }

    public void ShowForState(EnemyState state)
    {
        Sprite icon = GetIcon(state);
        if (bubbleRoot == null || iconRenderer == null || icon == null)
            return;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        iconRenderer.sprite = icon;
        bubbleRoot.SetActive(true);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    public void HideImmediate()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (bubbleRoot != null)
            bubbleRoot.SetActive(false);
    }

    private Sprite GetIcon(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.NoticedPlayer:
                return noticedIcon;
            case EnemyState.Suspicious:
                return suspiciousIcon;
            case EnemyState.ConfirmedPlayer:
                return confirmedIcon;
            case EnemyState.AttractedToLight:
                return attractedToLightIcon;
            case EnemyState.InspectingLight:
                return inspectingLightIcon;
            default:
                return null;
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);

        if (bubbleRoot != null)
            bubbleRoot.SetActive(false);

        hideRoutine = null;
    }
}
