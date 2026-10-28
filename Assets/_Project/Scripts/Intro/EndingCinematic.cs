using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A short film of how the voyage ended, played over the Results screen before it shows. Owner: Samprity.
/// Spawns itself in Results when a run has an ending (no scene edit) and builds its own stage from Resources/
/// EndingSet: the ship, the sea, lifeboats, the bottle and Grey Cove. Three shots, about 14 s, sound only, no music.
///   Dawn:         first light on the crumpled bow among the ice; the crew at the wheel while the Captain stands
///                 apart at the window, his bottle left on the chart table; in Grey Cove the wreckers' lanterns
///                 go out one by one over an empty sea.
///   Many saved:   the ship listing under red lamps as the lifeboats pull away; your crew in a boat with a lantern;
///                 from high up, the ship goes dark and a sea of lanterns is left (too many witnesses).
///   Record Time:  the ship goes down bow first; in Grey Cove the Captain, sober, stands in the wreckers' boat and
///                 drops the bottle; the bottle bobs alone as the last light goes out.
/// Enter / Space / Esc or A / Start skips it. When it ends it puts everything back and fades into the results.
/// </summary>
public class EndingCinematic : MonoBehaviour
{
    /// <summary>The film is on screen (the results wait underneath).</summary>
    public static bool Playing { get; private set; }
    /// <summary>For EndingChecks: the shot on screen, 1-3, 0 before and after.</summary>
    public static int Shot { get; private set; }
    /// <summary>For EndingChecks: seconds of film played so far (game time, so slow frames don't count against it).</summary>
    public static float Elapsed { get; private set; }

    const float SeaY = -2.2f;
    const float FadeTime = 0.35f;
    static readonly Vector3 CovePos = new Vector3(-150f, 0f, 190f);
    const string RecordTimeLine = "Every order was his. Every ‘yes’ was yours.";

    EndingSet set;
    Transform stage, deck, sea;
    PartMood mood;
    DayNightCycle cycle;
    Camera cam;
    RectTransform canvasRect;
    Image fader;
    CanvasGroup titleGroup, lineGroup, skipGroup;
    Text titleText, lineText;
    readonly List<GameObject> hidden = new List<GameObject>();
    readonly List<Behaviour> paused = new List<Behaviour>();
    AudioCategory seaSound, creakSound, floodSound, oneShots;
    bool skip;

    // Render settings the stage changes, put back afterwards.
    Material savedSky;
    Color savedAmbient, savedAmbientSky, savedAmbientEquator, savedAmbientGround, savedFogColor;
    UnityEngine.Rendering.AmbientMode savedAmbientMode;
    bool savedFog;
    float savedFogDensity, savedReflection;
    FogMode savedFogMode;
    Light savedSun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive || scene.name != "Results" || !RunRecord.HasEnding) return;
        if (EndingSet.Load() == null || EndingSet.Load().deck == null) return;
        new GameObject("[EndingCinematic]").AddComponent<EndingCinematic>();
    }

    void OnDestroy()
    {
        Playing = false;
        Shot = 0;
    }

    void Update()
    {
        if (Playing) Elapsed += Time.deltaTime; // the film's own clock (the shots run on it)
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)))
            skip = true;
        if (skipGroup != null) skipGroup.alpha = Mathf.MoveTowards(skipGroup.alpha, Playing && Time.time > 1.5f ? 0.75f : 0f, Time.unscaledDeltaTime * 2f);
    }

    IEnumerator Start()
    {
        Playing = true;
        Elapsed = 0f;
        set = EndingSet.Load();
        BuildOverlay();
        yield return null; // the results screen builds itself first; then it waits underneath
        HideResults();
        SaveRender();
        BuildStage();
        BuildAudio();

        switch (RunRecord.Ending)
        {
            case Ending.WhatIf: yield return Dawn(); break;
            case Ending.ManySaved: yield return ManySaved(); break;
            default: yield return RecordTime(); break;
        }
        yield return Finish();
    }

    // ---------- The three films ----------

    IEnumerator Dawn()
    {
        Phase("Dawn", 0.03f);
        mood.ApplyImmediate(1);
        // Ice from the berg, drifting past the bow.
        var rnd = new System.Random(3);
        for (int i = 0; i < 6; i++)
        {
            var chunk = Spawn(set.iceberg, new Vector3(Mathf.Lerp(-11f, 12f, (float)rnd.NextDouble()), SeaY - 0.7f, 80f + i * 4f + (float)rnd.NextDouble() * 3f), (float)rnd.NextDouble() * 360f);
            chunk.transform.localScale = Vector3.one * (0.05f + 0.06f * (float)rnd.NextDouble());
            foreach (var l in chunk.GetComponentsInChildren<Light>()) l.enabled = false;
        }
        Ambience(Bank?.seaHull, 0.55f);
        Loop(creakSound, Bank?.hullCreak, 0.35f);
        yield return Black(0.5f);
        Gull();

        // 1. First light on the crumpled bow.
        yield return Shot1(4f, new Vector3(16f, -0.6f, 97f), new Vector3(2f, 0.6f, 76f), new Vector3(12.5f, -0.3f, 91f), new Vector3(1f, 1.2f, 74f), 42f, null);

        // 2. The bridge: the crew at the wheel, the Captain alone at the window, the bottle left on the chart table.
        var a = Crew(0, new Vector3(-0.7f, 0f, 67.9f), 0f, "Push");
        var b = Crew(2, new Vector3(0.7f, 0f, 67.9f), 0f, "Push");
        var captain = Captain(new Vector3(-4.1f, 0f, 71.1f), 0f);
        var bottle = TakeBottle(captain);
        if (bottle != null) { bottle.SetParent(stage, true); bottle.position = new Vector3(-4.05f, 1.0f, 66.4f); bottle.rotation = Quaternion.identity; }
        yield return Shot2(4f, new Vector3(2.7f, 1.75f, 64.4f), new Vector3(-1.6f, 1.25f, 70.6f), new Vector3(1.9f, 1.62f, 65.5f), new Vector3(-2.3f, 1.35f, 70.9f), 46f, null);
        Destroy(a); Destroy(b);

        // 3. Grey Cove, from the empty sea outside it: the wreckers waited for a ship that never came, and their
        //    lanterns go out one by one.
        var cove = Spawn(set.cove, CovePos, 0f);
        var lamps = Lanterns(cove.transform);
        yield return Shot3(4.8f, CovePos + new Vector3(7f, 3.4f, -50f), CovePos + new Vector3(0f, 2.2f, 14f), CovePos + new Vector3(4.5f, 3.1f, -43f), CovePos + new Vector3(0f, 2.4f, 14f), 46f,
            k =>
            {
                for (int i = 0; i < lamps.Count; i++) if (k > 0.22f + i * 0.08f) PutOut(lamps[i]);
                Title(k > 0.45f);
            });
    }

    IEnumerator ManySaved()
    {
        Phase("Night", 0.4f);
        mood.ApplyImmediate(3);
        Tilt(2f, 7f, 1.1f);
        HideFoam();
        Ambience(Bank?.seaStorm, 0.4f);
        Loop(creakSound, Bank?.hullCreak, 0.55f);
        Loop(floodSound, Bank?.flood, 0.45f);

        // Lifeboats: three pulling away from the starboard side, the rest already out on the water.
        var boats = new List<Transform>();
        var rnd = new System.Random(11);
        for (int i = 0; i < 14; i++)
        {
            Vector3 p = i < 3 ? new Vector3(9.5f + i * 1.2f, SeaY, 34f + i * 7f)
                              : new Vector3(Mathf.Lerp(14f, 75f, (float)rnd.NextDouble()) * (i % 4 == 0 ? -1f : 1f), SeaY, Mathf.Lerp(-25f, 95f, (float)rnd.NextDouble()));
            boats.Add(Spawn(set.lifeboat, p, 70f + (float)rnd.NextDouble() * 40f).transform);
        }
        var crewBoat = boats[1];
        var c1 = Crew(0, Vector3.zero, 0f, "Torch");
        var c2 = Crew(2, Vector3.zero, 0f, null);
        Seat(c1, crewBoat, new Vector3(0.25f, -0.25f, -0.7f), 200f);
        Seat(c2, crewBoat, new Vector3(-0.2f, -0.25f, 0.9f), 160f);
        yield return Black(0.5f);

        // The two boats either side pull away; your crew's boat (the middle one) holds still for its close-up.
        System.Action<float> row = k => { foreach (int i in new[] { 0, 2 }) boats[i].position += boats[i].forward * Time.deltaTime * 0.9f; };

        // 1. Red lamps, a listing ship, the boats pulling away.
        yield return Shot1(4f, new Vector3(26f, -1.1f, 26f), new Vector3(4f, 1.2f, 44f), new Vector3(24f, -1f, 30f), new Vector3(4f, 1.4f, 46f), 44f, row);
        // 2. Your crew in a boat, the ship behind them.
        yield return Shot2(4f, crewBoat.position + new Vector3(4.6f, 0.9f, -2.2f), crewBoat.position + new Vector3(-1f, 1.1f, 1.4f),
            crewBoat.position + new Vector3(3.8f, 0.85f, -1.2f), crewBoat.position + new Vector3(-1.4f, 1.15f, 1.8f), 44f, row);
        // 3. From high up: the ship goes dark and a sea of lanterns is left.
        yield return Shot3(4.8f, new Vector3(70f, 32f, -18f), new Vector3(8f, -3f, 42f), new Vector3(63f, 34f, -12f), new Vector3(8f, -3f, 44f), 48f,
            k =>
            {
                row(k);
                if (k > 0.3f) ShipLightsOut();
                Title(k > 0.45f);
            });
    }

    IEnumerator RecordTime()
    {
        Phase("Night", 0.55f);
        mood.ApplyImmediate(3);
        HideFoam();
        Ambience(Bank?.wind, 0.35f);
        Loop(creakSound, Bank?.hullCreak, 0.7f);
        Loop(floodSound, Bank?.flood, 0.6f);
        yield return Black(0.5f);
        OneShot(Bank?.horn, 0.5f);

        // 1. Right on schedule: bow first.
        yield return Shot1(4f, new Vector3(-58f, 4f, 8f), new Vector3(0f, -1f, 44f), new Vector3(-52f, 3.6f, 14f), new Vector3(0f, -1.5f, 46f), 42f,
            k => Tilt(Mathf.Lerp(3f, 12f, k), -2f, Mathf.Lerp(0.6f, 3.2f, k)));

        // 2. Grey Cove: the Captain, upright and sober, in the wreckers' boat. He lets the bottle go.
        var cove = Spawn(set.cove, CovePos, 0f);
        var boat = cove.transform.Find("WreckerBoat");
        var at = boat != null ? boat : cove.transform;
        var camFrom = at.position + new Vector3(2.8f, 1.5f, -3.8f);
        var captain = Captain(Vector3.zero, 0f);
        captain.transform.SetParent(at, false);
        captain.transform.localPosition = new Vector3(0f, -0.2f, 0.4f);
        var look = camFrom - captain.transform.position;
        look.y = 0f;
        captain.transform.rotation = Quaternion.LookRotation(look);
        var bottle = (Transform)null;
        Vector3 fall = Vector3.zero;
        yield return Shot2(4f, camFrom, at.position + new Vector3(0f, 1.2f, 0.4f), at.position + new Vector3(2.2f, 1.45f, -3.1f), at.position + new Vector3(0f, 1.1f, 0.5f), 40f,
            k =>
            {
                if (bottle == null && k > 0.4f)
                {
                    bottle = TakeBottle(captain);
                    if (bottle != null) { bottle.SetParent(stage, true); fall = captain.transform.forward * 0.6f; }
                }
                if (bottle != null) Fall(bottle, ref fall);
            });

        // 3. The bottle, alone on the water, and the last light of the ship going out behind it.
        Tilt(18f, -3f, 6f);
        var drift = Spawn(set.bottle, new Vector3(-24f, SeaY, 28f), 0f).transform;
        drift.localScale = Vector3.one * 1.5f;
        var toShip = (new Vector3(0f, 0f, 46f) - drift.position);
        toShip.y = 0f;
        toShip.Normalize();
        // A little cold moonlight picks the bottle out on the black water.
        var glint = new GameObject("BottleGlint", typeof(Light)).GetComponent<Light>();
        glint.transform.SetParent(stage, false);
        glint.transform.position = drift.position + Vector3.up * 1.1f - toShip * 0.9f;
        glint.type = LightType.Point;
        glint.color = new Color(0.72f, 0.82f, 1f);
        glint.range = 4f;
        glint.intensity = 3f;
        var eye = drift.position - toShip * 2f + Vector3.up * 0.5f;
        yield return Shot3(4.8f, eye, drift.position + toShip * 3.5f + Vector3.up * 0.3f, eye + toShip * 0.45f, drift.position + toShip * 3.5f + Vector3.up * 0.25f, 40f,
            k =>
            {
                Bob(drift);
                if (k > 0.35f) ShipLightsOut();
                Title(k > 0.4f);
                Line(k > 0.55f, RecordTimeLine);
            });
    }

    // ---------- Shots ----------

    IEnumerator Shot1(float seconds, Vector3 fromPos, Vector3 fromLook, Vector3 toPos, Vector3 toLook, float fov, System.Action<float> tick) => Move(1, seconds, fromPos, fromLook, toPos, toLook, fov, tick);
    IEnumerator Shot2(float seconds, Vector3 fromPos, Vector3 fromLook, Vector3 toPos, Vector3 toLook, float fov, System.Action<float> tick) => Move(2, seconds, fromPos, fromLook, toPos, toLook, fov, tick);
    IEnumerator Shot3(float seconds, Vector3 fromPos, Vector3 fromLook, Vector3 toPos, Vector3 toLook, float fov, System.Action<float> tick) => Move(3, seconds, fromPos, fromLook, toPos, toLook, fov, tick, holdEnd: true);

    /// <summary>A slow camera move from one framing to another, faded in from black and out to black.</summary>
    IEnumerator Move(int shot, float seconds, Vector3 fromPos, Vector3 fromLook, Vector3 toPos, Vector3 toLook, float fov, System.Action<float> tick, bool holdEnd = false)
    {
        if (skip) yield break;
        Shot = shot;
        cam.fieldOfView = fov;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (skip) yield break;
            float k = t / seconds;
            float e = Mathf.SmoothStep(0f, 1f, k);
            cam.transform.position = Vector3.Lerp(fromPos, toPos, e);
            cam.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(fromLook, toLook, e) - cam.transform.position);
            tick?.Invoke(k);
            // In from black at the start; out to black at the end (the last shot holds its title a moment longer).
            float fadeIn = Mathf.Clamp01(t / FadeTime);
            float fadeOut = holdEnd ? 1f : Mathf.Clamp01((seconds - t) / FadeTime);
            SetFade(1f - Mathf.Min(fadeIn, fadeOut));
            yield return null;
        }
        if (!holdEnd) SetFade(1f);
    }

    IEnumerator Black(float seconds)
    {
        SetFade(1f);
        for (float t = 0f; t < seconds && !skip; t += Time.deltaTime) yield return null;
    }

    IEnumerator Finish()
    {
        // Out to black (the title stays a beat), then the results fade up from under it.
        for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime) { SetFade(Mathf.Max(fader.color.a, t / 0.8f)); yield return null; }
        if (skip) yield return new WaitForSecondsRealtime(0.15f);
        SetFade(1f);
        titleGroup.alpha = 0f;
        lineGroup.alpha = 0f;
        Shot = 0;
        TearDown();
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
        {
            SetFade(1f - t);
            if (seaSound != null) seaSound.BaseVolume *= 0.95f;
            yield return null;
        }
        Playing = false;
        Destroy(canvasRect.gameObject);
        Destroy(gameObject);
    }

    // ---------- Stage ----------

    void BuildStage()
    {
        // The ship, built inactive and stripped of everything that plays the game, so only the look is left.
        var root = new GameObject("[EndingStage]");
        root.SetActive(false);
        stage = root.transform;
        var d = Instantiate(set.deck, stage);
        deck = d.transform;
        Strip(d);
        var seaTf = FindDeep(deck, "Sea");
        if (seaTf != null) { seaTf.SetParent(stage, true); sea = seaTf; }
        // Dawn: she steams on, slowly. Otherwise she has stopped dead. (SeaScroll reads this once, as it wakes.)
        bool underWay = RunRecord.Ending == Ending.WhatIf;
        foreach (var scroll in stage.GetComponentsInChildren<SeaScroll>(true)) scroll.forwardSpeed = underWay ? 2f : 0f;
        root.SetActive(true);
        // The water itself: the swell streams at the ship's speed, and at night the white caps are kept low (under the
        // red Part 3 moon they read as pink smears). A property block, so the game's sea material is never touched.
        if (sea != null && sea.TryGetComponent(out Renderer seaRenderer))
        {
            var block = new MaterialPropertyBlock();
            seaRenderer.GetPropertyBlock(block);
            block.SetFloat("_Flow", underWay ? 2f : 0f);
            if (!underWay) block.SetFloat("_FoamAmount", 0.12f);
            // A low swell: out past the hull's calm zone the full one (0.7 m) would bury the boats, the floes and the
            // bottle. That night the sea was flat calm anyway.
            block.SetFloat("_Height", underWay ? 0.3f : 0.12f);
            seaRenderer.SetPropertyBlock(block);
        }
        mood = d.GetComponentInChildren<PartMood>();
        cycle = d.GetComponentInChildren<DayNightCycle>();

        var camGo = new GameObject("EndingCamera", typeof(Camera), typeof(AudioListener));
        camGo.transform.SetParent(stage, false);
        cam = camGo.GetComponent<Camera>();
        cam.depth = 50f;
        cam.nearClipPlane = 0.08f;
        cam.farClipPlane = 1500f;
        var urp = cam.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = true;
        urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    }

    static readonly System.Type[] Keep = { typeof(PartMood), typeof(DayNightCycle), typeof(SeaScroll), typeof(FoamScroll) };

    /// <summary>Removes every script on the deck except the ones that only make it look right.</summary>
    static void Strip(GameObject root)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            bool left = false;
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || System.Array.IndexOf(Keep, mb.GetType()) >= 0) continue;
                if (RequiredByOthers(mb)) { left = true; continue; }
                DestroyImmediate(mb);
            }
            if (!left) break;
        }
    }

    static bool RequiredByOthers(MonoBehaviour mb)
    {
        foreach (var other in mb.GetComponents<MonoBehaviour>())
        {
            if (other == null || other == mb) continue;
            foreach (RequireComponent req in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                if ((req.m_Type0 != null && req.m_Type0.IsInstanceOfType(mb)) || (req.m_Type1 != null && req.m_Type1.IsInstanceOfType(mb)) || (req.m_Type2 != null && req.m_Type2.IsInstanceOfType(mb)))
                    return true;
        }
        return false;
    }

    void Phase(string name, float into)
    {
        if (cycle == null) return;
        // `into` the named part of the day: 0 = its start.
        float p = cycle.PhaseOf(name);
        float span = name == "Night" ? (1f - cycle.dayShare) * 0.5f : cycle.dayShare * 0.06f;
        cycle.SetPhase(p - span * 0.5f + span * into);
    }

    void HideFoam()
    {
        foreach (var foam in deck.GetComponentsInChildren<FoamScroll>(true)) foam.gameObject.SetActive(false);
    }

    /// <summary>Lists the ship: pitch (bow down), roll (to port when positive), and how far she has settled.</summary>
    void Tilt(float pitch, float roll, float sink)
    {
        var pivot = new Vector3(0f, 0f, 38f);
        var rot = Quaternion.Euler(pitch, 0f, roll);
        deck.SetPositionAndRotation(pivot - rot * pivot + Vector3.down * sink, rot);
    }

    bool lightsOut;

    void ShipLightsOut()
    {
        if (lightsOut) return;
        lightsOut = true;
        if (mood != null) mood.enabled = false; // it would keep lighting the lamps
        foreach (var l in deck.GetComponentsInChildren<Light>(true))
            if (l.type != LightType.Directional) l.enabled = false;
        // The glowing windows and portholes go dark with them.
        foreach (var r in deck.GetComponentsInChildren<Renderer>(true))
            if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Ship_PortholeGlow")) r.enabled = false;
    }

    GameObject Spawn(GameObject prefab, Vector3 pos, float yaw)
    {
        if (prefab == null) return new GameObject("Missing");
        return Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), stage);
    }

    GameObject Crew(int index, Vector3 pos, float yaw, string state)
    {
        var crewSet = CrewVisualSet.Load();
        if (crewSet == null || crewSet.crew.Length == 0) return new GameObject("NoCrew");
        var go = Instantiate(crewSet.crew[index % crewSet.crew.Length], pos, Quaternion.Euler(0f, yaw, 0f), stage);
        var anim = go.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            if (crewSet.controller != null) anim.runtimeAnimatorController = crewSet.controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (state != null && anim.HasState(0, Animator.StringToHash(state))) anim.Play(state, 0, Random.value);
            else anim.Play("Locomotion", 0, Random.value);
        }
        return go;
    }

    static void Seat(GameObject who, Transform boat, Vector3 local, float yaw)
    {
        who.transform.SetParent(boat, false);
        who.transform.localPosition = local;
        who.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    GameObject Captain(Vector3 pos, float yaw)
    {
        var cs = CaptainSet.Load();
        if (cs == null || cs.prefab == null) return new GameObject("NoCaptain");
        var go = Instantiate(cs.prefab, pos, Quaternion.Euler(0f, yaw, 0f), stage);
        var anim = go.GetComponent<Animator>();
        if (anim != null) { anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        return go;
    }

    static Transform TakeBottle(GameObject captain) => captain != null ? FindDeep(captain.transform, "Bottle") : null;

    void Fall(Transform bottle, ref Vector3 velocity)
    {
        if (bottle.position.y > SeaY + 0.02f)
        {
            velocity += Vector3.down * 9.8f * Time.deltaTime;
            bottle.position += velocity * Time.deltaTime;
            bottle.rotation = Quaternion.AngleAxis(240f * Time.deltaTime, Vector3.right) * bottle.rotation;
        }
        else Bob(bottle);
    }

    /// <summary>Floating on its side, rocking with the swell.</summary>
    static void Bob(Transform t)
    {
        float s = Time.time;
        var p = t.position;
        p.y = SeaY + 0.03f + Mathf.Sin(s * 1.6f) * 0.04f;
        t.position = p;
        t.rotation = Quaternion.Euler(80f + Mathf.Sin(s * 1.1f) * 6f, 35f, Mathf.Sin(s * 1.6f + 1f) * 8f);
    }

    /// <summary>Every lantern in the cove, on the rocks and in the boats, left to right.</summary>
    static List<Transform> Lanterns(Transform cove)
    {
        var list = new List<Transform>();
        foreach (var t in cove.GetComponentsInChildren<Transform>(true)) if (t.name == "Lantern") list.Add(t);
        list.Sort((a, b) => a.position.x.CompareTo(b.position.x));
        return list;
    }

    /// <summary>A lantern going out: the light dies and the glass goes dark (it takes the cage's iron).</summary>
    static void PutOut(Transform lantern)
    {
        var light = lantern.Find("Light");
        if (light == null || !light.gameObject.activeSelf) return;
        light.gameObject.SetActive(false);
        var glass = lantern.Find("Glass");
        var cage = lantern.Find("Cage");
        if (glass != null && cage != null) glass.GetComponent<Renderer>().sharedMaterial = cage.GetComponent<Renderer>().sharedMaterial;
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    // ---------- The results underneath, and putting the scene back ----------

    void HideResults()
    {
        foreach (var r in FindObjectsByType<ResultsScreen>(FindObjectsSortMode.None)) { r.enabled = false; paused.Add(r); }
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.isRootCanvas && c.transform != canvasRect && c.gameObject.activeSelf) { c.gameObject.SetActive(false); hidden.Add(c.gameObject); }
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c.enabled) { c.enabled = false; paused.Add(c); }
        foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l.enabled) { l.enabled = false; paused.Add(l); }
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.enabled) { l.enabled = false; paused.Add(l); }
    }

    void SaveRender()
    {
        savedSky = RenderSettings.skybox;
        savedAmbientMode = RenderSettings.ambientMode;
        savedAmbient = RenderSettings.ambientLight;
        savedAmbientSky = RenderSettings.ambientSkyColor;
        savedAmbientEquator = RenderSettings.ambientEquatorColor;
        savedAmbientGround = RenderSettings.ambientGroundColor;
        savedFog = RenderSettings.fog;
        savedFogColor = RenderSettings.fogColor;
        savedFogDensity = RenderSettings.fogDensity;
        savedFogMode = RenderSettings.fogMode;
        savedReflection = RenderSettings.reflectionIntensity;
        savedSun = RenderSettings.sun;
    }

    void TearDown()
    {
        if (stage != null) Destroy(stage.gameObject);
        foreach (var src in new[] { seaSound, creakSound, floodSound, oneShots }) if (src != null) Destroy(src.gameObject);
        RenderSettings.skybox = savedSky;
        RenderSettings.ambientMode = savedAmbientMode;
        RenderSettings.ambientLight = savedAmbient;
        RenderSettings.ambientSkyColor = savedAmbientSky;
        RenderSettings.ambientEquatorColor = savedAmbientEquator;
        RenderSettings.ambientGroundColor = savedAmbientGround;
        RenderSettings.fog = savedFog;
        RenderSettings.fogColor = savedFogColor;
        RenderSettings.fogDensity = savedFogDensity;
        RenderSettings.fogMode = savedFogMode;
        RenderSettings.reflectionIntensity = savedReflection;
        RenderSettings.sun = savedSun;
        foreach (var go in hidden) if (go != null) go.SetActive(true);
        foreach (var b in paused) if (b != null) b.enabled = true;
    }

    // ---------- Sound (no music) ----------

    static AudioBank bank;
    static AudioBank Bank => bank != null ? bank : (bank = AudioBank.Load());

    void BuildAudio()
    {
        seaSound = Source("EndingSea", true);
        creakSound = Source("EndingCreak", true);
        floodSound = Source("EndingFlood", true);
        oneShots = Source("EndingOneShots", false);
    }

    AudioCategory Source(string name, bool loop)
    {
        var go = new GameObject(name, typeof(AudioSource));
        go.transform.SetParent(transform, false);
        var s = go.GetComponent<AudioSource>();
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        var cat = go.AddComponent<AudioCategory>();
        cat.kind = AudioCategory.Kind.Effects;
        cat.BaseVolume = 0f;
        return cat;
    }

    void Ambience(AudioClip clip, float volume) => Loop(seaSound, clip, volume);

    static void Loop(AudioCategory cat, AudioClip clip, float volume)
    {
        if (cat == null || clip == null) return;
        var s = cat.GetComponent<AudioSource>();
        s.clip = clip;
        cat.BaseVolume = volume;
        s.Play();
    }

    void OneShot(AudioClip clip, float volume)
    {
        if (oneShots == null || clip == null || volume <= 0f) return;
        oneShots.BaseVolume = 1f;
        oneShots.GetComponent<AudioSource>().PlayOneShot(clip, volume);
    }

    void Gull()
    {
        var gulls = Bank != null ? Bank.gulls : null;
        if (gulls != null && gulls.Length > 0) OneShot(gulls[Random.Range(0, gulls.Length)], 0.5f);
    }

    // ---------- Overlay: black fader, letterbox, title, skip hint ----------

    void SetFade(float a)
    {
        var c = fader.color;
        c.a = Mathf.Clamp01(a);
        fader.color = c;
    }

    void Title(bool on) => titleGroup.alpha = Mathf.MoveTowards(titleGroup.alpha, on ? 1f : 0f, Time.deltaTime * 1.4f);

    void Line(bool on, string text)
    {
        if (on && lineText.text != text) lineText.text = text;
        lineGroup.alpha = Mathf.MoveTowards(lineGroup.alpha, on ? 1f : 0f, Time.deltaTime * 1.2f);
    }

    void BuildOverlay()
    {
        var theme = UiTheme.Load();
        Font F(Font f) => f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvas = new GameObject("[EndingFilm]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300; // over the results
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvas.transform;

        // Letterbox bars, cinema-style.
        foreach (var top in new[] { true, false })
        {
            var bar = Img("Bar", Color.black);
            var r = bar.rectTransform;
            r.anchorMin = new Vector2(0f, top ? 1f : 0f);
            r.anchorMax = new Vector2(1f, top ? 1f : 0f);
            r.pivot = new Vector2(0.5f, top ? 1f : 0f);
            r.sizeDelta = new Vector2(0f, 120f);
        }

        var ink = new Color(0.96f, 0.93f, 0.86f);
        titleGroup = Group("Title", out var titleRect);
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 40f);
        titleText = Label(titleRect, F(theme ? theme.title : null), 88, ink);
        titleText.text = EndingData.For(RunRecord.Ending)?.title ?? RunRecord.Ending.ToString();
        var shadow = titleText.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -3f);

        lineGroup = Group("Line", out var lineRect);
        lineRect.anchorMin = lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = new Vector2(0f, -40f);
        lineText = Label(lineRect, F(theme ? theme.italic : null), 38, ink);
        lineText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);

        skipGroup = Group("Skip", out var skipRect);
        skipRect.anchorMin = skipRect.anchorMax = new Vector2(1f, 0f);
        skipRect.anchoredPosition = new Vector2(-170f, 60f);
        var skipText = Label(skipRect, F(theme ? theme.body : null), 24, new Color(0.85f, 0.82f, 0.75f));
        skipText.text = Gamepad.current != null ? "A  Skip" : "Enter  Skip";

        fader = Img("Fade", Color.black);
        fader.rectTransform.anchorMin = Vector2.zero;
        fader.rectTransform.anchorMax = Vector2.one;
        fader.rectTransform.sizeDelta = Vector2.zero;
        // The fader sits under the title so a title can show over black; letterbox and skip stay on top of all.
        fader.transform.SetSiblingIndex(2);
        skipGroup.transform.SetAsLastSibling();
    }

    Image Img(string name, Color c)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(canvasRect, false);
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    CanvasGroup Group(string name, out RectTransform rect)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvasRect, false);
        rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(1600f, 120f);
        var g = go.GetComponent<CanvasGroup>();
        g.alpha = 0f;
        g.blocksRaycasts = false;
        return g;
    }

    static Text Label(RectTransform parent, Font font, int size, Color c)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.color = c;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        var r = t.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero;
        return t;
    }
}
