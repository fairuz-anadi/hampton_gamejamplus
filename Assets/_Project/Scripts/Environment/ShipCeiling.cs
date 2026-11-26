using UnityEngine;

/// <summary>
/// The decks stacked over the one you play on (cabins, top deck) have no colliders, so the NavMesh ignores them. That
/// also meant the follow camera's collision never saw them: rising over a wall, or tilted up, it went straight through
/// the ceiling and you saw through the roof. This treats the whole superstructure as one solid block for the camera
/// only: from just under the cabin floor up past the top deck, over the bays it covers. AutoFollowCamera adds it to
/// its own collision. Owner: Rabib.
/// Keep the numbers in step with ShipDeckBuilder (UpperBays 8-64, UpperHalfW 6.6, CabinFloorY 3.6 with a 0.2 slab).
/// </summary>
public static class ShipCeiling
{
    /// <summary>The superstructure runs from the corridor bulkhead to the bridge (ship-local Z).</summary>
    public const float FromZ = 8f, ToZ = 64f;
    /// <summary>Half its width, ship-local X.</summary>
    public const float HalfWidth = 6.6f;
    /// <summary>The underside of the cabin floor, which is the ceiling over the deck you play on.</summary>
    public const float CeilingY = 3.5f;
    /// <summary>Well above the top deck and its funnel; nothing the follow camera reaches is higher.</summary>
    public const float TopY = 14f;
    /// <summary>Room kept between the lens and the ceiling: the Ship camera's near plane is 0.3 m.</summary>
    public const float Gap = 0.3f;

    static Transform ship;
    static float nextLook;

    /// <summary>The ShipDeck root (it carries PartMood, as RoofFade also assumes), or null in scenes without the deck.</summary>
    static Transform Ship
    {
        get
        {
            if (ship == null && Time.unscaledTime >= nextLook)
            {
                nextLook = Time.unscaledTime + 1f;
                var mood = Object.FindAnyObjectByType<PartMood>();
                ship = mood != null ? mood.transform : null;
            }
            return ship;
        }
    }

    /// <summary>
    /// How far from origin along dir the camera can go before it would enter the superstructure (less Gap), or
    /// maxDist if it never does. A focus point already inside it (nobody stands up there) is left alone.
    /// </summary>
    public static float ClearDistance(Vector3 origin, Vector3 dir, float maxDist)
    {
        var s = Ship;
        if (s == null) return maxDist;
        Vector3 o = s.InverseTransformPoint(origin), d = s.InverseTransformDirection(dir);
        var min = new Vector3(-HalfWidth - Gap, CeilingY - Gap, FromZ - Gap);
        var max = new Vector3(HalfWidth + Gap, TopY, ToZ + Gap);
        if (o.x > min.x && o.x < max.x && o.y > min.y && o.y < max.y && o.z > min.z && o.z < max.z) return maxDist;

        // Ray against the block (slab test): where it enters, if it does within maxDist.
        float tEnter = 0f, tExit = maxDist;
        for (int axis = 0; axis < 3; axis++)
        {
            float od = d[axis], oo = o[axis], lo = min[axis], hi = max[axis];
            if (Mathf.Abs(od) < 1e-6f)
            {
                if (oo < lo || oo > hi) return maxDist; // parallel and outside this slab
                continue;
            }
            float t0 = (lo - oo) / od, t1 = (hi - oo) / od;
            if (t0 > t1) (t0, t1) = (t1, t0);
            tEnter = Mathf.Max(tEnter, t0);
            tExit = Mathf.Min(tExit, t1);
            if (tEnter > tExit) return maxDist;
        }
        return Mathf.Clamp(tEnter, 0f, maxDist);
    }
}
