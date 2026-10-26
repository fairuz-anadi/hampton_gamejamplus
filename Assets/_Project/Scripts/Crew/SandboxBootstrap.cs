using UnityEngine;

/// <summary>
/// Drop this in a sandbox scene so GameSystems boots without Ship.unity. Owner: Rabib.
/// Ship.unity and any scene ending in "_Systems" get this for free; Rabib_Stations.unity doesn't,
/// so it needs this one object.
/// </summary>
public class SandboxBootstrap : MonoBehaviour
{
    void Awake()
    {
        GameSystems.Ensure();
    }
}
