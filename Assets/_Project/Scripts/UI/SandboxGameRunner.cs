using UnityEngine;

/// <summary>
/// Drop in a sandbox scene to run the real orders and meters there, so the HUD can be tried while walking the deck.
/// Use Anadi's debug keys to resolve orders: F5 obey, F6 fake, F7 refuse, F1 next part. Owner: Samprity.
/// </summary>
public class SandboxGameRunner : MonoBehaviour
{
    void Start() => GameSystems.Ensure();
}
