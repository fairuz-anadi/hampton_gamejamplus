using UnityEngine;

/// <summary>
/// Real-time shadows from the room lamps, on a budget: only the lamps nearest the camera cast soft shadows (each
/// shadowed point light renders six shadow views), so every room still gets moving shadows where the player is
/// while the whole long ship stays cheap enough for an ordinary laptop. Reads PartMood's lamp list. Owner: Anadi.
/// </summary>
public class LampShadowBudget : MonoBehaviour
{
    [Tooltip("How many of the nearest lamps cast shadows at once.")]
    [Range(0, 8)] public int shadowedLamps = 4;
    [Tooltip("Seconds between re-sorting the lamps by distance.")]
    [Min(0.05f)] public float interval = 0.25f;

    Light[] lamps = new Light[0];
    float next;

    void Start()
    {
        if (TryGetComponent(out PartMood mood) && mood.lamps != null) lamps = mood.lamps;
    }

    void Update()
    {
        if (Time.unscaledTime < next || lamps.Length == 0) return;
        next = Time.unscaledTime + interval;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 eye = cam.transform.position;

        // Rank each lamp by how many lamps are closer than it; the closest few get shadows.
        for (int i = 0; i < lamps.Length; i++)
        {
            if (lamps[i] == null) continue;
            float d = (lamps[i].transform.position - eye).sqrMagnitude;
            int closer = 0;
            for (int j = 0; j < lamps.Length; j++)
                if (j != i && lamps[j] != null && (lamps[j].transform.position - eye).sqrMagnitude < d) closer++;
            var want = closer < shadowedLamps ? LightShadows.Soft : LightShadows.None;
            if (lamps[i].shadows != want) lamps[i].shadows = want;
        }
    }
}
