using UnityEngine;

/// <summary>
/// Streams a foam strip's texture along its length (the mesh's V) at the ship's speed, for the wake and bow waves.
/// Owner: Samprity. Positive speed moves the foam towards larger V, which the deck builder points aft.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class FoamScroll : MonoBehaviour
{
    [Tooltip("Metres per second the foam streams along the strip.")]
    public float speed = 4f;
    [Tooltip("Metres of strip one texture repeat covers.")]
    [Min(0.1f)] public float metresPerRepeat = 18f;
    [Tooltip("A slow sideways wander, in repeats per second.")]
    public float drift = 0.01f;

    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    Renderer rend;
    MaterialPropertyBlock block;
    Vector4 st;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        st = rend.sharedMaterial != null ? rend.sharedMaterial.GetVector(BaseMapST) : new Vector4(1, 1, 0, 0);
    }

    void Update()
    {
        float t = Time.time;
        rend.GetPropertyBlock(block);
        block.SetVector(BaseMapST, new Vector4(st.x, st.y, (st.z + drift * Mathf.Sin(t * 0.3f)) % 1f, (st.w - speed / metresPerRepeat * t) % 1f));
        rend.SetPropertyBlock(block);
    }
}
