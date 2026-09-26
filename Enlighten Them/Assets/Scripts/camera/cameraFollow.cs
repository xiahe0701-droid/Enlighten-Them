using UnityEngine;

[DefaultExecutionOrder(100)]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("玩家下面的 CameraTarget 空物体")]
    [SerializeField] private Transform target;

    [Header("Position")]
    [Tooltip("启动时自动记录相机和目标之间的距离")]
    [SerializeField] private bool captureOffsetOnStart = true;

    [SerializeField] private Vector3 offset;

    [Header("Smoothing")]
    [Min(0.01f)]
    [SerializeField] private float smoothTime = 0.15f;

    [Tooltip("限制相机追赶速度。数值越高，限制越不明显")]
    [Min(0f)]
    [SerializeField] private float maxFollowSpeed = 100f;

    private Vector3 followVelocity;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError(
                "CameraFollow has no target assigned.",
                this
            );

            enabled = false;
            return;
        }

        if (captureOffsetOnStart)
        {
            offset = transform.position - target.position;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 targetPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref followVelocity,
            smoothTime,
            maxFollowSpeed
        );
    }

    public void SnapToTarget()
    {
        if (target == null)
            return;

        followVelocity = Vector3.zero;
        transform.position = target.position + offset;
    }
}