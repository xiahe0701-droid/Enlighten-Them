using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyMotor : MonoBehaviour
{
    [Header("Patrol Route")]
    [SerializeField] private Transform[] patrolPoints;

    [Header("Visuals")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Rigidbody body;
    private EnemyAIConfig config;
    private int patrolIndex;
    private bool waitingAtPatrolPoint;
    private float patrolWaitUntil;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.freezeRotation = true;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Initialize(EnemyAIConfig aiConfig)
    {
        config = aiConfig;
    }

    public void TickPatrol()
    {
        if (config == null || patrolPoints == null || patrolPoints.Length == 0)
        {
            Stop();
            return;
        }

        Transform point = patrolPoints[patrolIndex];
        if (point == null)
        {
            AdvancePatrolPoint();
            Stop();
            return;
        }

        if (HasReached(point.position, config.patrolPointDistance))
        {
            if (!waitingAtPatrolPoint)
            {
                waitingAtPatrolPoint = true;
                patrolWaitUntil = Time.time + config.patrolPauseTime;
            }

            Stop();

            if (Time.time >= patrolWaitUntil)
                AdvancePatrolPoint();

            return;
        }

        waitingAtPatrolPoint = false;
        MoveTowards(point.position, config.patrolSpeed, config.patrolPointDistance);
    }

    public void MoveTowards(Vector3 target, float speed, float stopDistance)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= stopDistance * stopDistance)
        {
            Stop();
            return;
        }

        direction.Normalize();
        Vector3 avoidance = GetAvoidDirection(direction);
        Vector3 finalDirection = (direction + avoidance * config.obstacleAvoidStrength).normalized;
        Vector3 velocity = finalDirection * speed;

        body.velocity = new Vector3(velocity.x, body.velocity.y, velocity.z);
        UpdateVisuals(finalDirection, true);
    }

    public bool HasReached(Vector3 target, float distance)
    {
        Vector3 offset = target - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= distance * distance;
    }

    public void Stop()
    {
        if (body != null)
            body.velocity = new Vector3(0f, body.velocity.y, 0f);

        UpdateVisuals(Vector3.zero, false);
    }

    private void AdvancePatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        waitingAtPatrolPoint = false;
    }

    private Vector3 GetAvoidDirection(Vector3 moveDirection)
    {
        if (config == null || config.obstacleLayers.value == 0)
            return Vector3.zero;

        Vector3 origin = transform.position + Vector3.up * config.eyeHeight;
        if (!Physics.Raycast(
                origin,
                moveDirection,
                out RaycastHit hit,
                config.obstacleCheckDistance,
                config.obstacleLayers,
                QueryTriggerInteraction.Ignore))
        {
            return Vector3.zero;
        }

        Vector3 slide = Vector3.ProjectOnPlane(moveDirection, hit.normal);
        if (slide.sqrMagnitude < 0.001f)
            slide = Vector3.Cross(Vector3.up, moveDirection);

        return slide.normalized;
    }

    private void UpdateVisuals(Vector3 direction, bool moving)
    {
        if (animator != null)
        {
            animator.SetBool("Move", moving);
            if (moving)
                animator.SetBool("Back", direction.z > 0.1f);
        }

        if (moving && spriteRenderer != null && Mathf.Abs(direction.x) > 0.05f)
            spriteRenderer.flipX = direction.x > 0f;
    }
}
