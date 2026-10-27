using UnityEngine;

/// <summary>Slides the sea's ripple texture so the water looks alive and the sun glints move. Owner: Samprity.</summary>
[RequireComponent(typeof(Renderer))]
public class SeaScroll : MonoBehaviour
{
    [Tooltip("Ripple drift in texture repeats per second.")]
    public Vector2 speed = new Vector2(0.004f, 0.012f);

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
        var offset = new Vector2(st.z, st.w) + speed * Time.time;
        rend.GetPropertyBlock(block);
        block.SetVector(BaseMapST, new Vector4(st.x, st.y, offset.x % 1f, offset.y % 1f));
        rend.SetPropertyBlock(block);
    }
}
