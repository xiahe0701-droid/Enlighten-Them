using System.Collections.Generic;
using UnityEngine;

public class EnemyLightSource : MonoBehaviour
{
    private static readonly HashSet<EnemyLightSource> Sources =
        new HashSet<EnemyLightSource>();

    [SerializeField] private bool attracting = true;
    [Min(0f)] [SerializeField] private float strength = 1f;
    [Min(0f)] [SerializeField] private float attractionRadius = 8f;

    [Tooltip(
        "When this list is empty, any enemy inside Attraction Radius may react. " +
        "When it contains enemies, only those enemies react and distance is ignored.")]
    [SerializeField] private EnemyBrain[] affectedEnemies;

    public static IEnumerable<EnemyLightSource> ActiveSources => Sources;
    public bool IsAttracting => isActiveAndEnabled && attracting;
    public float Strength => strength;
    public float AttractionRadius => attractionRadius;
    public Vector3 Position => transform.position;
    public int ExplicitTargetCount =>
        affectedEnemies == null ? 0 : affectedEnemies.Length;

    private void OnEnable()
    {
        Sources.Add(this);
    }

    private void OnDisable()
    {
        Sources.Remove(this);
    }

    public void SetAttracting(bool value)
    {
        attracting = value;
    }

    public bool CanAttract(EnemyBrain enemy, float distance)
    {
        if (!IsAttracting || enemy == null)
            return false;

        if (affectedEnemies == null || affectedEnemies.Length == 0)
            return distance <= attractionRadius;

        return IsExplicitTarget(enemy);
    }

    public bool IsExplicitTarget(EnemyBrain enemy)
    {
        if (enemy == null || affectedEnemies == null)
            return false;

        for (int i = 0; i < affectedEnemies.Length; i++)
        {
            if (affectedEnemies[i] == enemy)
                return true;
        }

        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        Sources.Clear();
    }
}
