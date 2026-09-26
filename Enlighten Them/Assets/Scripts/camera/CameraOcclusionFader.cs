using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]
public class CameraOcclusionFader : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("玩家下面的 CameraTarget 空物体")]
    [SerializeField] private Transform target;

    [Header("Occlusion Detection")]
    [Tooltip("只选择 CameraOccluder 层")]
    [SerializeField] private LayerMask occluderMask;

    [Tooltip("检测范围宽度。太大可能误判旁边的墙")]
    [SerializeField, Min(0f)] private float sphereRadius = 0.2f;

    [Tooltip("检测终点与玩家之间留出少量距离")]
    [SerializeField, Min(0f)] private float targetPadding = 0.1f;

    [Header("Fade")]
    [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.25f;

    [SerializeField, Min(0.01f)] private float fadeSpeed = 4f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugLine = true;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    private readonly HashSet<Renderer> currentlyBlocking =
        new HashSet<Renderer>();

    private readonly Dictionary<Renderer, FadeState> fadeStates =
        new Dictionary<Renderer, FadeState>();

    private MaterialPropertyBlock propertyBlock;
    private int baseColorProperty;

    private class FadeState
    {
        public Color originalColor;
        public float currentAlpha;
    }

    private void Awake()
    {
        // Unity objects must be created here, not in a field initializer.
        propertyBlock = new MaterialPropertyBlock();
        baseColorProperty = Shader.PropertyToID("_BaseColor");
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        DetectBlockingWalls();
        UpdateWallTransparency();
    }

    private void DetectBlockingWalls()
    {
        currentlyBlocking.Clear();

        Vector3 origin = transform.position;
        Vector3 targetPosition = target.position;
        Vector3 offsetToTarget = targetPosition - origin;

        float fullDistance = offsetToTarget.magnitude;

        if (fullDistance <= 0.001f)
            return;

        Vector3 direction = offsetToTarget / fullDistance;

        // 检测只到玩家前面为止，不会检测玩家身后的墙。
        float detectionDistance =
            Mathf.Max(0f, fullDistance - targetPadding);

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            sphereRadius,
            direction,
            hitBuffer,
            detectionDistance,
            occluderMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hitBuffer[i].collider;

            if (hitCollider == null)
                continue;

            Renderer wallRenderer =
                hitCollider.GetComponent<Renderer>();

            if (wallRenderer == null)
            {
                wallRenderer =
                    hitCollider.GetComponentInParent<Renderer>();
            }

            if (wallRenderer == null)
                continue;

            currentlyBlocking.Add(wallRenderer);
            RegisterRendererIfNeeded(wallRenderer);
        }

        if (drawDebugLine)
        {
            Color lineColor =
                hitCount > 0 ? Color.red : Color.green;

            Debug.DrawLine(
                origin,
                origin + direction * detectionDistance,
                lineColor
            );
        }
    }

    private void RegisterRendererIfNeeded(Renderer wallRenderer)
    {
        if (fadeStates.ContainsKey(wallRenderer))
            return;

        Material material = wallRenderer.sharedMaterial;

        if (material == null ||
            !material.HasProperty(baseColorProperty))
        {
            return;
        }

        Color originalColor =
            material.GetColor(baseColorProperty);

        fadeStates.Add(
            wallRenderer,
            new FadeState
            {
                originalColor = originalColor,
                currentAlpha = originalColor.a
            }
        );
    }

    private void UpdateWallTransparency()
    {
        foreach (KeyValuePair<Renderer, FadeState> pair in fadeStates)
        {
            Renderer wallRenderer = pair.Key;
            FadeState state = pair.Value;

            if (wallRenderer == null)
                continue;

            bool isBlocking =
                currentlyBlocking.Contains(wallRenderer);

            float targetAlpha =
                isBlocking
                    ? fadedAlpha
                    : state.originalColor.a;

            state.currentAlpha = Mathf.MoveTowards(
                state.currentAlpha,
                targetAlpha,
                fadeSpeed * Time.deltaTime
            );

            Color displayedColor = state.originalColor;
            displayedColor.a = state.currentAlpha;

            wallRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(
                baseColorProperty,
                displayedColor
            );
            wallRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void OnDisable()
    {
        RestoreAllWalls();
    }

    private void RestoreAllWalls()
    {
        if (propertyBlock == null)
            return;

        foreach (KeyValuePair<Renderer, FadeState> pair in fadeStates)
        {
            Renderer wallRenderer = pair.Key;

            if (wallRenderer == null)
                continue;

            wallRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(
                baseColorProperty,
                pair.Value.originalColor
            );
            wallRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}