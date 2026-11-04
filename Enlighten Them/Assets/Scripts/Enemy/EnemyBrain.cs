using UnityEngine;

[RequireComponent(typeof(EnemyPerception))]
[RequireComponent(typeof(EnemyMotor))]
[RequireComponent(typeof(EnemyLightSensor))]
public class EnemyBrain : MonoBehaviour
{
    [SerializeField] private EnemyAIConfig config;
    [SerializeField] private EnemyPerception perception;
    [SerializeField] private EnemyMotor motor;
    [SerializeField] private EnemyLightSensor lightSensor;
    [SerializeField] private EnemyBubblePresenter bubblePresenter;

    public EnemyState CurrentState { get; private set; }

    private float stateTimer;
    private float lostPlayerTimer;
    private EnemyLightSource lightTarget;
    private EnemyLightSource ignoredLight;
    private float ignoreLightUntil;

    private void Awake()
    {
        if (perception == null)
            perception = GetComponent<EnemyPerception>();

        if (motor == null)
            motor = GetComponent<EnemyMotor>();

        if (lightSensor == null)
            lightSensor = GetComponent<EnemyLightSensor>();

        if (bubblePresenter == null)
            bubblePresenter = GetComponentInChildren<EnemyBubblePresenter>(true);

        if (config == null)
        {
            Debug.LogError("EnemyBrain requires an EnemyAIConfig.", this);
            enabled = false;
            return;
        }

        perception.Initialize(config);
        motor.Initialize(config);
        lightSensor.Initialize(config);
    }

    private void Start()
    {
        ChangeState(EnemyState.Patrol, true);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        stateTimer += deltaTime;

        perception.Tick(deltaTime);

        if (ignoredLight != null && Time.time >= ignoreLightUntil)
            ignoredLight = null;

        lightSensor.Tick(deltaTime, ignoredLight);
        EvaluateTransitions(deltaTime);
    }

    private void FixedUpdate()
    {
        switch (CurrentState)
        {
            case EnemyState.Patrol:
                motor.TickPatrol();
                break;

            case EnemyState.Suspicious:
                if (perception.HasLastSeenPosition)
                {
                    motor.MoveTowards(
                        perception.LastSeenPosition,
                        config.suspiciousSpeed,
                        config.patrolPointDistance);
                }
                else
                {
                    motor.Stop();
                }
                break;

            case EnemyState.Chase:
                if (perception.Player != null)
                {
                    motor.MoveTowards(
                        perception.Player.position,
                        config.chaseSpeed,
                        config.playerStopDistance);
                }
                else
                {
                    motor.Stop();
                }
                break;

            case EnemyState.AttractedToLight:
                if (IsLightTargetValid())
                {
                    motor.MoveTowards(
                        lightTarget.Position,
                        config.lightAttractionSpeed,
                        config.lightStopDistance);
                }
                else
                {
                    motor.Stop();
                }
                break;

            default:
                motor.Stop();
                break;
        }
    }

    private void EvaluateTransitions(float deltaTime)
    {
        switch (CurrentState)
        {
            case EnemyState.Patrol:
                if (TryBeginLightAttraction(false))
                    return;

                if (perception.Awareness >= config.noticedThreshold)
                    ChangeState(EnemyState.NoticedPlayer);
                break;

            case EnemyState.NoticedPlayer:
                if (perception.Awareness >= config.confirmedThreshold)
                {
                    ChangeState(EnemyState.ConfirmedPlayer);
                }
                else if (stateTimer >= config.noticedReactionTime)
                {
                    ChangeState(EnemyState.Suspicious);
                }
                break;

            case EnemyState.Suspicious:
                if (perception.Awareness >= config.confirmedThreshold)
                {
                    ChangeState(EnemyState.ConfirmedPlayer);
                    return;
                }

                if (TryBeginLightAttraction(false))
                    return;

                bool reachedLastSeen = !perception.HasLastSeenPosition ||
                    motor.HasReached(
                        perception.LastSeenPosition,
                        config.patrolPointDistance);

                if (!perception.CanSeePlayer &&
                    reachedLastSeen &&
                    stateTimer >= config.suspiciousSearchTime)
                {
                    perception.ClearLastSeenPosition();
                    ChangeState(EnemyState.Patrol);
                }
                break;

            case EnemyState.ConfirmedPlayer:
                if (stateTimer >= config.confirmedReactionTime)
                    ChangeState(EnemyState.Chase);
                break;

            case EnemyState.Chase:
                if (TryBeginLightAttraction(true))
                    return;

                if (perception.CanSeePlayer)
                {
                    lostPlayerTimer = 0f;
                }
                else
                {
                    lostPlayerTimer += deltaTime;
                    if (lostPlayerTimer >= config.chaseLoseSightGrace)
                        ChangeState(EnemyState.Suspicious);
                }
                break;

            case EnemyState.AttractedToLight:
                if (!IsLightTargetValid())
                {
                    ResumeAfterLight();
                    return;
                }

                if (motor.HasReached(lightTarget.Position, config.lightStopDistance))
                    ChangeState(EnemyState.InspectingLight);
                break;

            case EnemyState.InspectingLight:
                if (stateTimer >= config.inspectLightTime)
                {
                    ignoredLight = lightTarget;
                    ignoreLightUntil = Time.time + config.sameLightCooldown;
                    lightTarget = null;
                    ResumeAfterLight();
                }
                break;
        }
    }

    private bool TryBeginLightAttraction(bool interruptingChase)
    {
        EnemyLightSource candidate = lightSensor.BestSource;
        if (candidate == null || !candidate.IsAttracting)
            return false;

        if (interruptingChase &&
            candidate.Strength < config.chaseInterruptLightStrength)
        {
            return false;
        }

        lightTarget = candidate;
        ChangeState(EnemyState.AttractedToLight);
        return true;
    }

    private bool IsLightTargetValid()
    {
        return lightTarget != null && lightTarget.IsAttracting;
    }

    private void ResumeAfterLight()
    {
        if (perception.Awareness >= config.confirmedThreshold)
            ChangeState(EnemyState.ConfirmedPlayer);
        else if (perception.HasLastSeenPosition)
            ChangeState(EnemyState.Suspicious);
        else
            ChangeState(EnemyState.Patrol);
    }

    private void ChangeState(EnemyState nextState, bool force = false)
    {
        if (!force && CurrentState == nextState)
            return;

        CurrentState = nextState;
        stateTimer = 0f;

        if (nextState != EnemyState.Chase)
            lostPlayerTimer = 0f;

        if (bubblePresenter != null)
            bubblePresenter.ShowForState(nextState);
    }
}
