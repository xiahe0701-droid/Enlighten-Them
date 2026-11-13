using UnityEngine;

public class EnemyPerception : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform sightOrigin;

    private const float PlayerLookupInterval = 0.5f;

    public Transform Player => player;
    public bool CanSeePlayer { get; private set; }
    public float Awareness { get; private set; }
    public bool HasLastSeenPosition { get; private set; }
    public Vector3 LastSeenPosition { get; private set; }

    private EnemyAIConfig config;
    private float playerLookupTimer;

    public void Initialize(EnemyAIConfig aiConfig)
    {
        config = aiConfig;
        ResolvePlayerIfMissing();
    }

    public void SetPlayer(Transform target)
    {
        player = target;
        playerLookupTimer = 0f;
    }

    public void Tick(float deltaTime)
    {
        if (player == null)
        {
            playerLookupTimer -= deltaTime;
            if (playerLookupTimer <= 0f)
            {
                ResolvePlayerIfMissing();
                playerLookupTimer = PlayerLookupInterval;
            }
        }

        if (config == null || player == null)
        {
            CanSeePlayer = false;
            Awareness = Mathf.MoveTowards(Awareness, 0f, deltaTime);
            return;
        }

        CanSeePlayer = EvaluatePlayerVisibility();

        if (CanSeePlayer)
        {
            Awareness = Mathf.MoveTowards(
                Awareness,
                1f,
                config.awarenessGainPerSecond * deltaTime);

            LastSeenPosition = player.position;
            HasLastSeenPosition = true;
        }
        else
        {
            Awareness = Mathf.MoveTowards(
                Awareness,
                0f,
                config.awarenessDecayPerSecond * deltaTime);
        }
    }

    public void ClearLastSeenPosition()
    {
        HasLastSeenPosition = false;
    }

    private void ResolvePlayerIfMissing()
    {
        if (player != null)
            return;

        PlayerMovement playerMovement = FindObjectOfType<PlayerMovement>();
        if (playerMovement != null)
            player = playerMovement.transform;
    }

    private bool EvaluatePlayerVisibility()
    {
        Vector3 origin = sightOrigin != null
            ? sightOrigin.position
            : transform.position + Vector3.up * config.eyeHeight;

        Vector3 target = player.position;
        Vector3 toPlayer = target - origin;
        Vector3 flatDirection = Vector3.ProjectOnPlane(toPlayer, Vector3.up);

        if (flatDirection.sqrMagnitude > config.visionRange * config.visionRange)
            return false;

        if (flatDirection.sqrMagnitude > 0.0001f && config.visionAngle < 359.9f)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.Angle(flatForward, flatDirection);
                if (angle > config.visionAngle * 0.5f)
                    return false;
            }
        }

        float distance = toPlayer.magnitude;
        if (distance <= 0.001f)
            return true;

        return !Physics.Raycast(
            origin,
            toPlayer / distance,
            distance,
            config.visionBlockingLayers,
            QueryTriggerInteraction.Ignore);
    }

    private void OnDrawGizmosSelected()
    {
        if (config == null)
            return;

        Vector3 origin = sightOrigin != null
            ? sightOrigin.position
            : transform.position + Vector3.up * config.eyeHeight;

        Gizmos.color = CanSeePlayer ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(origin, config.visionRange);
    }
}
