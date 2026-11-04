using UnityEngine;

public class EnemyLightSensor : MonoBehaviour
{
    public EnemyLightSource BestSource { get; private set; }

    private EnemyAIConfig config;
    private float scanTimer;

    public void Initialize(EnemyAIConfig aiConfig)
    {
        config = aiConfig;
        scanTimer = 0f;
    }

    public void Tick(float deltaTime, EnemyLightSource excludedSource)
    {
        if (config == null)
            return;

        scanTimer -= deltaTime;
        if (scanTimer > 0f)
            return;

        scanTimer = config.lightScanInterval;
        BestSource = FindBestSource(excludedSource);
    }

    private EnemyLightSource FindBestSource(EnemyLightSource excludedSource)
    {
        EnemyLightSource best = null;
        float bestScore = float.NegativeInfinity;

        foreach (EnemyLightSource source in EnemyLightSource.ActiveSources)
        {
            if (source == null || source == excludedSource || !source.IsAttracting)
                continue;

            float distance = Vector3.Distance(transform.position, source.Position);
            if (distance > source.AttractionRadius)
                continue;

            float score = source.Strength / (1f + distance);
            if (score <= bestScore)
                continue;

            best = source;
            bestScore = score;
        }

        return best;
    }
}
