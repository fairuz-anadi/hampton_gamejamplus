using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ship's other work. The Captain's orders go to the players (OrderDirector), so a bot with nothing more urgent
/// (no repair, help call, ice warning or sabotage) goes about chores instead of loitering: it walks to one of the
/// deck's Chore_* markers (galley stove, mess table, saloon, bunks, coal pile, buckets, rope), faces it and works
/// there a while, then moves on. One bot per spot. BotBrain calls Tick from its idle branch; CrewVisual asks
/// IsDoingChore to play the work loop. Owner: Anadi.
/// </summary>
public static class BotChores
{
    class State
    {
        public Transform spot;
        public float workUntil = -1f;   // < 0: still walking there
        public float restUntil;         // a short loiter between chores
        public float lastTick = -10f;   // BotBrain thinks every 0.2 s, not every frame
    }

    static readonly Dictionary<BotBrain, State> states = new Dictionary<BotBrain, State>();
    static readonly List<Transform> spots = new List<Transform>();
    static int spotsScene = -1;
    const float TickGap = 0.6f; // longer than this between Ticks = the bot went off to do something else

    /// <summary>One idle step for this bot. True while it's walking to or working a chore; false = let it wander.</summary>
    public static bool Tick(BotBrain bot)
    {
        if (bot == null || bot.Mover == null) return false;
        if (!states.TryGetValue(bot, out var s)) states[bot] = s = new State();
        // Called again after doing something else (an order, a repair): start over.
        if (Time.time - s.lastTick > TickGap) Release(s);
        s.lastTick = Time.time;

        if (s.spot == null)
        {
            if (Time.time < s.restUntil) return false;
            s.spot = PickSpot(bot);
            if (s.spot == null) { s.restUntil = Time.time + 3f; return false; }
            s.workUntil = -1f;
            bot.Mover.GoTo(BotMover.OnNavMesh(s.spot.position), 0.35f);
            return true;
        }

        if (s.workUntil < 0f)
        {
            if (!bot.Mover.HasArrived) return true;
            s.workUntil = Time.time + Random.Range(8f, 14f);
        }
        // At the spot: face the work and keep at it.
        bot.transform.rotation = Quaternion.Slerp(bot.transform.rotation, Quaternion.Euler(0f, s.spot.eulerAngles.y, 0f), Time.deltaTime * 6f);
        if (Time.time < s.workUntil) return true;

        Release(s);
        s.restUntil = Time.time + Random.Range(2f, 5f);
        return false;
    }

    /// <summary>The bot is standing at a chore and working it (for the work animation).</summary>
    public static bool IsDoingChore(BotBrain bot) =>
        bot != null && states.TryGetValue(bot, out var s) && s.spot != null && s.workUntil >= 0f
        && Time.time - s.lastTick <= TickGap;

    static void Release(State s)
    {
        s.spot = null;
        s.workUntil = -1f;
    }

    static Transform PickSpot(BotBrain bot)
    {
        RefreshSpots();
        var free = new List<Transform>();
        foreach (var t in spots)
        {
            if (t == null) continue;
            bool taken = false;
            foreach (var kv in states)
                if (kv.Key != bot && kv.Key != null && kv.Value.spot == t) { taken = true; break; }
            if (!taken) free.Add(t);
        }
        return free.Count > 0 ? free[Random.Range(0, free.Count)] : null;
    }

    static void RefreshSpots()
    {
        int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode(); // unique per loaded scene (.handle as int is obsolete in Unity 6.6)
        if (scene == spotsScene && spots.Count > 0 && spots[0] != null) return;
        spotsScene = scene;
        spots.Clear();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            if (t.name.StartsWith("Chore_")) spots.Add(t);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        states.Clear();
        spots.Clear();
        spotsScene = -1;
    }
}
