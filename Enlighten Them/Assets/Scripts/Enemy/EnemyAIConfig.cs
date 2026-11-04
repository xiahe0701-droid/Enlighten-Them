using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyAIConfig",
    menuName = "Enlighten Them/Enemy/AI Config")]
public class EnemyAIConfig : ScriptableObject
{
    [Header("Player Perception")]
    [Min(0.1f)] public float visionRange = 7f;
    [Range(1f, 360f)] public float visionAngle = 120f;
    [Min(0f)] public float eyeHeight = 0.5f;
    public LayerMask visionBlockingLayers;
    [Min(0.01f)] public float awarenessGainPerSecond = 1.2f;
    [Min(0.01f)] public float awarenessDecayPerSecond = 0.35f;
    [Range(0f, 1f)] public float noticedThreshold = 0.12f;
    [Range(0f, 1f)] public float confirmedThreshold = 0.95f;

    [Header("State Timing")]
    [Min(0f)] public float noticedReactionTime = 0.35f;
    [Min(0f)] public float confirmedReactionTime = 0.4f;
    [Min(0f)] public float chaseLoseSightGrace = 1.25f;
    [Min(0f)] public float suspiciousSearchTime = 3f;
    [Min(0f)] public float inspectLightTime = 2f;
    [Min(0f)] public float sameLightCooldown = 4f;

    [Header("Movement")]
    [Min(0f)] public float patrolSpeed = 1.4f;
    [Min(0f)] public float suspiciousSpeed = 1.8f;
    [Min(0f)] public float chaseSpeed = 3.2f;
    [Min(0f)] public float lightAttractionSpeed = 2.2f;
    [Min(0.01f)] public float patrolPointDistance = 0.35f;
    [Min(0f)] public float patrolPauseTime = 0.6f;
    [Min(0.01f)] public float playerStopDistance = 0.7f;
    [Min(0.01f)] public float lightStopDistance = 0.65f;

    [Header("Obstacle Avoidance")]
    public LayerMask obstacleLayers;
    [Min(0f)] public float obstacleCheckDistance = 0.9f;
    [Min(0f)] public float obstacleAvoidStrength = 1.5f;

    [Header("Light Distraction")]
    [Min(0.02f)] public float lightScanInterval = 0.2f;
    [Min(0f)] public float chaseInterruptLightStrength = 2f;

    private void OnValidate()
    {
        confirmedThreshold = Mathf.Max(confirmedThreshold, noticedThreshold);
    }
}
