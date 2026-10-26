using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks one room of the deck with a box. Crew use it for RoomId ("Boiler", "Corridor", "Wireless", "Bridge"),
/// which the Stoker, the witness check and the SuspicionTracker all rely on. Owner: Riham.
/// Put one on an empty GameObject per room and size the box to cover the floor (height doesn't matter).
/// Rabib: players can use RoomZone.IdAt(transform.position) for their RoomId too.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class RoomZone : MonoBehaviour
{
    public string roomId = "Boiler";

    public const int IgnoreRaycastLayer = 2;

    static readonly List<RoomZone> zones = new List<RoomZone>(8);

    BoxCollider box;

    public Vector3 Center => Box.bounds.center;

    BoxCollider Box => box != null ? box : box = GetComponent<BoxCollider>();

    void Reset() => GetComponent<BoxCollider>().isTrigger = true;

    void OnEnable()
    {
        Box.isTrigger = true;
        gameObject.layer = IgnoreRaycastLayer; // keeps the box out of NavMesh bakes
        if (!zones.Contains(this)) zones.Add(this);
    }

    void OnDisable() => zones.Remove(this);

    /// <summary>True when the point is inside the box on the deck plane (Y is ignored).</summary>
    public bool Contains(Vector3 p)
    {
        var b = Box.bounds;
        return p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z;
    }

    /// <summary>A random point inside the room, at the room's floor height.</summary>
    public Vector3 RandomPoint(float margin = 0.5f)
    {
        var b = Box.bounds;
        return new Vector3(
            Random.Range(b.min.x + margin, Mathf.Max(b.min.x + margin, b.max.x - margin)),
            b.min.y,
            Random.Range(b.min.z + margin, Mathf.Max(b.min.z + margin, b.max.z - margin)));
    }

    /// <summary>The room at a position, or "" if it isn't inside any room.</summary>
    public static string IdAt(Vector3 p)
    {
        foreach (var z in zones) if (z.Contains(p)) return z.roomId;
        return "";
    }

    public static RoomZone Find(string id)
    {
        foreach (var z in zones) if (z.roomId == id) return z;
        return null;
    }

    /// <summary>Which room a station lives in: from its position if zones exist, otherwise the layout default.</summary>
    public static string RoomOf(StationType type)
    {
        var st = StationRegistry.Get(type);
        if (st != null)
        {
            string id = IdAt(st.Position);
            if (id != "") return id;
        }
        return DefaultRoom(type);
    }

    public static string DefaultRoom(StationType type) => type switch
    {
        StationType.Boiler => "Boiler",
        StationType.Doors => "Corridor",
        StationType.Wireless => "Wireless",
        _ => "Bridge",
    };

    /// <summary>True when any human player is in that room.</summary>
    public static bool HumanIn(string room)
    {
        if (string.IsNullOrEmpty(room)) return false;
        foreach (var c in CrewRegistry.All)
            if (!c.IsBot && c.RoomId == room) return true;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => zones.Clear();

    void OnDrawGizmos()
    {
        var b = Box.bounds;
        Gizmos.color = new Color(0.3f, 0.8f, 0.5f, 0.15f);
        Gizmos.DrawCube(b.center, b.size);
    }
}
