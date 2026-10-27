using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the hull's portholes glow unevenly, like real gas/electric lamps seen from outside, instead of sitting at
/// one flat emission. Same breathing flicker as FireFlicker, just driven per-renderer via a MaterialPropertyBlock
/// (each with its own random phase) so every porthole keeps sharing Samprity's one Ship_PortholeGlow material.
/// Owner: Rabib. Spawns itself in gameplay scenes, like HUD/MiniMap, so nobody has to edit Ship.unity or
/// ShipDeckBuilder.cs.
/// </summary>
public class PortholeFlicker : MonoBehaviour
{
    [Range(0f, 1f)] public float amount = 0.3f;
    public float speed = 2.5f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    struct Porthole { public Renderer renderer; public Color baseEmission; public float seed; }

    readonly List<Porthole> portholes = new List<Porthole>(32);
    MaterialPropertyBlock block;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || FindAnyObjectByType<PortholeFlicker>() != null || !GameSystems.IsGameplayScene(scene.name)) return;
        new GameObject("[PortholeFlicker]").AddComponent<PortholeFlicker>();
    }

    void Start()
    {
        block = new MaterialPropertyBlock();
        var deck = GameObject.Find("ShipDeck");
        if (deck == null) return;

        foreach (var t in deck.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Porthole") continue;
            var r = t.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(EmissionColor)) continue;
            portholes.Add(new Porthole
            {
                renderer = r,
                baseEmission = r.sharedMaterial.GetColor(EmissionColor),
                seed = Random.value * 100f,
            });
        }
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < portholes.Count; i++)
        {
            var p = portholes[i];
            float n = Mathf.PerlinNoise(p.seed, t * speed) * 0.7f + Mathf.PerlinNoise(p.seed + 7f, t * speed * 2.7f) * 0.3f;
            block.SetColor(EmissionColor, p.baseEmission * (1f - amount + amount * 2f * n));
            p.renderer.SetPropertyBlock(block);
        }
    }
}
