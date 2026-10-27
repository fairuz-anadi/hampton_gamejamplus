using UnityEngine;

/// <summary>
/// Slides the sea's ripple texture so the water looks alive, and streams it past the hull so the ship reads as under
/// way (bow = +Z) while the deck itself never moves: physics, the NavMesh and the crew stay put. Owner: Samprity.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class SeaScroll : MonoBehaviour
{
    [Tooltip("Ripple drift in texture repeats per second.")]
    public Vector2 speed = new Vector2(0.004f, 0.012f);
    [Tooltip("The ship's speed through the water, metres per second. The sea streams aft at this speed.")]
    public float forwardSpeed = 4f;

    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    Renderer rend;
    MaterialPropertyBlock block;
    Vector4 st;
    Vector2 aftPerSecond;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        st = rend.sharedMaterial != null ? rend.sharedMaterial.GetVector(BaseMapST) : new Vector4(1, 1, 0, 0);
        aftPerSecond = UvPerMetreAlongZ() * forwardSpeed;
    }

    /// <summary>How the texture offset must change per metre for the pattern to move one metre towards -Z.</summary>
    Vector2 UvPerMetreAlongZ()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return Vector2.zero;
        var mesh = mf.sharedMesh;
        var v = mesh.vertices;
        var uv = mesh.uv;
        if (uv.Length != v.Length || v.Length < 2) return Vector2.zero;
        // Find two vertices that differ only along Z to read d(uv)/d(world z), tiling included.
        for (int i = 1; i < v.Length; i++)
        {
            var dw = transform.TransformVector(v[i] - v[0]);
            if (Mathf.Abs(dw.x) < 1e-3f && Mathf.Abs(dw.z) > 1e-3f)
            {
                var duv = Vector2.Scale(uv[i] - uv[0], new Vector2(st.x, st.y));
                // A feature at uv u* sits where uv(z) = u* - offset; growing the offset by d(uv)/dz moves it towards -Z.
                return duv / dw.z;
            }
        }
        return Vector2.zero;
    }

    void Update()
    {
        var offset = new Vector2(st.z, st.w) + (speed + aftPerSecond) * Time.time;
        rend.GetPropertyBlock(block);
        block.SetVector(BaseMapST, new Vector4(st.x, st.y, offset.x % 1f, offset.y % 1f));
        rend.SetPropertyBlock(block);
    }
}
