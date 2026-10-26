using UnityEngine;

/// <summary>Makes a light breathe like fire (the boiler's firebox). Owner: Samprity.</summary>
[RequireComponent(typeof(Light))]
public class FireFlicker : MonoBehaviour
{
    [Range(0f, 1f)] public float amount = 0.35f;
    public float speed = 6f;

    Light fire;
    float baseIntensity, seed;

    void Awake()
    {
        fire = GetComponent<Light>();
        baseIntensity = fire.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed) * 0.7f + Mathf.PerlinNoise(seed + 7f, Time.time * speed * 2.7f) * 0.3f;
        fire.intensity = baseIntensity * (1f - amount + amount * 2f * n);
    }
}
