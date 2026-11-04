using System.Collections.Generic;
using UnityEngine;

public class EnemyLightSource : MonoBehaviour
{
    private static readonly HashSet<EnemyLightSource> Sources =
        new HashSet<EnemyLightSource>();

    [SerializeField] private bool attracting = true;
    [Min(0f)] [SerializeField] private float strength = 1f;
    [Min(0f)] [SerializeField] private float attractionRadius = 8f;

    public static IEnumerable<EnemyLightSource> ActiveSources => Sources;
    public bool IsAttracting => isActiveAndEnabled && attracting;
    public float Strength => strength;
    public float AttractionRadius => attractionRadius;
    public Vector3 Position => transform.position;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        Sources.Clear();
    }
}
