using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A trigger box covering one room of the deck. Owner: Samprity.
/// Crew can answer ICrewActor.RoomId with RoomVolume.RoomAt(Position),
/// and the Stoker can ask whether a human shares its room. Room ids: Boiler, Corridor, Wireless, Bridge, Bow.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class RoomVolume : MonoBehaviour
{
    public string roomId = "Boiler";

    static readonly List<RoomVolume> rooms = new List<RoomVolume>(8);
    BoxCollider box;

    public static IReadOnlyList<RoomVolume> All => rooms;

    void OnEnable()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        if (!rooms.Contains(this)) rooms.Add(this);
    }

    void OnDisable() => rooms.Remove(this);

    /// <summary>True if a world position is inside this room (height ignored).</summary>
    public bool Contains(Vector3 worldPos)
    {
        if (box == null) box = GetComponent<BoxCollider>();
        Vector3 p = transform.InverseTransformPoint(worldPos);
        Vector3 min = box.center - box.size * 0.5f, max = box.center + box.size * 0.5f;
        return p.x >= min.x && p.x <= max.x && p.z >= min.z && p.z <= max.z;
    }

    /// <summary>The room a world position is in, or "Deck" if it's in none of them.</summary>
    public static string RoomAt(Vector3 worldPos)
    {
        foreach (var r in rooms) if (r.Contains(worldPos)) return r.roomId;
        return "Deck";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => rooms.Clear();

    void OnDrawGizmos()
    {
        if (box == null) box = GetComponent<BoxCollider>();
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.08f);
        Gizmos.DrawCube(box.center, box.size);
    }
}
