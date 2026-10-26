using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the ShipDeck prefab (the whole playable deck, stern to bow) and drops it into Samprity_Art.unity
/// with a walkable player. Owner: Samprity. Anadi drops the prefab into Ship.unity; Riham bakes the NavMesh on it.
///   In the Editor:  Port > Samprity > Build Ship Deck
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod ShipDeckBuilder.BuildBatch -quit
///
/// Layout (metres, bow is +Z, KayKit's 4 m grid, 12 m wide, open tops so the follow camera can rise over walls):
///   Boiler   z  0-8    boiler + coal chute (2-person)
///   Corridor z  8-16   4 m passage, lifeboat store to port, lever bank + muster bell to starboard
///   Wireless z 16-24   wireless desk, message tray
///   Bridge   z 24-32   ship's wheel (2-person), telegraph, chart table
///   Bow      z 32-36   open deck with railings
/// Markers teammates use: Station_*, WorkSpot_A/B, StatusLight, Spawn_1-4, PickUp_IceWarning, DropPoint_Bridge, Speaker_*.
/// </summary>
public static class ShipDeckBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefabs/Environment/ShipDeck.prefab";
    public const string ScenePath = "Assets/_Project/Scenes/Sandbox/Samprity_Art.unity";
    const string KayKit = "Assets/_Project/Art/ThirdParty/KayKit";
    const string MatFolder = "Assets/_Project/Art/Materials/Ship";
    const float WallScaleY = 0.7f; // KayKit walls are 4 m; 2.8 m fits human-sized crew and stays readable from the follow camera
    const string ParticleSource = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    static Material navyWall, darkFloor, brass, coal, cream, darkWood, steel, hull, sea, fire, statusRed, white, lampGlass,
        redPaint, buff, portholeGlow, smokeMat, steamMat, emberMat, dustMat;

    [MenuItem("Port/Samprity/Build Ship Deck")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        MovementPrototypeBuilder.SetUpCharacterImport();
        var locomotion = MovementPrototypeBuilder.BuildLocomotionController();
        RenderLookSetup.Apply();
        CreateMaterials();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

        var deck = BuildDeck();
        MovementPrototypeBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAssetAndConnect(deck, PrefabPath, InteractionMode.AutomatedAction);

        var player = MovementPrototypeBuilder.BuildPlayer(locomotion);
        var spawn = deck.transform.Find("Markers/Spawn_1");
        player.GetComponent<ThirdPersonMover>().Teleport(spawn.position, spawn.eulerAngles.y);
        MovementPrototypeBuilder.BuildCamera(player);
        deck.GetComponent<PartMood>().ApplyImmediate(1);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ShipDeck] Built {PrefabPath} and {ScenePath}");
    }

    public static void BuildBatch() => Build();

    // ---------- Deck ----------

    static GameObject BuildDeck()
    {
        var root = new GameObject("ShipDeck");
        var t = root.transform;
        var hullT = Group(t, "Hull");
        var floorT = Group(t, "Floors");
        var wallT = Group(t, "Walls");
        var roomT = Group(t, "Rooms");
        var stationT = Group(t, "Stations");
        var propT = Group(t, "Props");
        var lightT = Group(t, "Lights");
        var markerT = Group(t, "Markers");

        BuildHullAndSea(hullT);
        BuildFloors(floorT);
        BuildWalls(wallT);
        BuildRooms(roomT);

        BuildBoiler(stationT, propT);
        BuildCorridor(stationT, propT);
        BuildWireless(stationT, propT);
        BuildBridge(stationT, propT);
        BuildBow(propT);
        BuildDetails(propT);
        BuildExterior(hullT);
        var fxT = Group(t, "Atmosphere");
        BuildAtmosphere(fxT);

        var mood = root.AddComponent<PartMood>();
        mood.moon = BuildMoon(lightT);
        mood.lamps = BuildLamps(lightT);
        mood.calmVolume = PostVolume(fxT, "PostFX_Calm", "Calm", 1f);
        mood.offVolume = PostVolume(fxT, "PostFX_Off", "Off", 0f);
        mood.truthVolume = PostVolume(fxT, "PostFX_Truth", "Truth", 0f);
        mood.sky = AssetDatabase.LoadAssetAtPath<Material>(RenderLookSetup.SkyMaterialPath);

        Marker(markerT, "Spawn_1", new Vector3(-3f, 0.05f, 6.8f), 0f);
        Marker(markerT, "Spawn_2", new Vector3(-1f, 0.05f, 7.0f), 0f);
        Marker(markerT, "Spawn_3", new Vector3(1f, 0.05f, 7.0f), 0f);
        Marker(markerT, "Spawn_4", new Vector3(3f, 0.05f, 6.8f), 0f);
        return root;
    }

    static void BuildHullAndSea(Transform p)
    {
        // Ship's side below the deck, and the sea. The camera sees these over the low walls.
        Prim(PrimitiveType.Cube, "HullBody", p, new Vector3(0f, -1.3f, 17.5f), Vector3.zero, new Vector3(13.2f, 2.6f, 37.5f), hull);
        Prim(PrimitiveType.Cube, "HullBand", p, new Vector3(0f, -0.15f, 17.5f), Vector3.zero, new Vector3(13.4f, 0.3f, 37.7f), cream, false);
        var seaGo = Prim(PrimitiveType.Plane, "Sea", p, new Vector3(0f, -2.2f, 18f), Vector3.zero, new Vector3(60f, 1f, 60f), sea);
        Object.DestroyImmediate(seaGo.GetComponent<Collider>());
        seaGo.isStatic = false;
        seaGo.AddComponent<SeaScroll>();

        // One flat collider for the whole walkable deck, so the NavMesh and players don't snag on tile seams.
        var floor = new GameObject("DeckCollider");
        floor.transform.SetParent(p, false);
        var bc = floor.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, -0.25f, 18f);
        bc.size = new Vector3(12f, 0.6f, 36f);
        floor.isStatic = true;
    }

    static void BuildFloors(Transform p)
    {
        for (int zi = 0; zi < 9; zi++)
        for (int xi = -1; xi <= 1; xi++)
        {
            float z = 2f + zi * 4f, x = xi * 4f;
            string piece; Material mat = null;
            if (z < 4f && x != 0f) piece = "floor_tile_big_grate";             // stokehold grating either side of the boiler
            else if (z < 8f) { piece = "floor_tile_large"; mat = darkFloor; }  // boiler: sooty stone
            else if (z < 16f) piece = "floor_tile_large";                      // corridor
            else if (z < 32f) piece = "floor_wood_large";                      // wireless, bridge
            else piece = "floor_wood_large_dark";                              // bow deck
            Kit(p, "Dungeon", piece, new Vector3(x, 0f, z), 0f, null, false, mat);
        }
    }

    static void BuildWalls(Transform p)
    {
        var s = new Vector3(1f, WallScaleY, 1f);
        // Ship's sides: every other module has a window (porthole).
        for (int i = 0; i < 8; i++)
        {
            float z = 2f + i * 4f;
            string piece = i % 2 == 1 ? "wall_window_open" : "wall";
            Kit(p, "Dungeon", piece, new Vector3(-6f, 0f, z), 90f, s, true, navyWall);
            Kit(p, "Dungeon", piece, new Vector3(6f, 0f, z), 90f, s, true, navyWall);
        }
        // Stern.
        for (int xi = -1; xi <= 1; xi++) Kit(p, "Dungeon", "wall", new Vector3(xi * 4f, 0f, 0f), 0f, s, true, navyWall);
        // Bulkheads between rooms with a 4 m doorway in the middle; the bridge's front has windows.
        foreach (float z in new[] { 8f, 16f, 24f, 32f })
        {
            string piece = z == 32f ? "wall_window_open" : "wall";
            Kit(p, "Dungeon", piece, new Vector3(-4f, 0f, z), 0f, s, true, navyWall);
            Kit(p, "Dungeon", piece, new Vector3(4f, 0f, z), 0f, s, true, navyWall);
        }
        // Lifeboat store: closed room to port of the corridor.
        Kit(p, "Dungeon", "wall", new Vector3(-2f, 0f, 10f), 90f, s, true, navyWall);
        Kit(p, "Dungeon", "wall", new Vector3(-2f, 0f, 14f), 90f, s, true, navyWall);
    }

    static void BuildRooms(Transform p)
    {
        Room(p, "Boiler", new Vector3(0f, 1.5f, 4f), new Vector3(12f, 3f, 8f));
        Room(p, "Corridor", new Vector3(0f, 1.5f, 12f), new Vector3(12f, 3f, 8f));
        Room(p, "Wireless", new Vector3(0f, 1.5f, 20f), new Vector3(12f, 3f, 8f));
        Room(p, "Bridge", new Vector3(0f, 1.5f, 28f), new Vector3(12f, 3f, 8f));
        Room(p, "Bow", new Vector3(0f, 1.5f, 34f), new Vector3(12f, 3f, 4f));
    }

    // ---------- Rooms and stations ----------

    static void BuildBoiler(Transform stations, Transform props)
    {
        var st = Station(stations, "Station_Boiler", new Vector3(0f, 0f, 3.2f));
        var s = st.transform;
        Prim(PrimitiveType.Cylinder, "BoilerBody", s, new Vector3(0f, 1.4f, -0.9f), Vector3.zero, new Vector3(3f, 1.4f, 3f), steel);
        Prim(PrimitiveType.Cylinder, "BoilerBand", s, new Vector3(0f, 2.1f, -0.9f), Vector3.zero, new Vector3(3.1f, 0.08f, 3.1f), brass, false);
        Prim(PrimitiveType.Cylinder, "Flue", s, new Vector3(0f, 3.6f, -1.2f), Vector3.zero, new Vector3(0.8f, 1.2f, 0.8f), coal);
        Prim(PrimitiveType.Cube, "Firebox", s, new Vector3(0f, 0.75f, 0.62f), Vector3.zero, new Vector3(1.3f, 0.9f, 0.12f), fire, false);
        Prim(PrimitiveType.Cube, "FireboxFrame", s, new Vector3(0f, 0.75f, 0.58f), Vector3.zero, new Vector3(1.6f, 1.15f, 0.1f), coal, false);
        // Coal chute: two people shovel from the pile into the chute.
        Prim(PrimitiveType.Cube, "CoalChute", s, new Vector3(2.4f, 0.95f, 0.1f), new Vector3(-28f, 0f, 0f), new Vector3(1.1f, 0.12f, 2.2f), steel);
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Sphere, "Coal_" + i, s, new Vector3(3.3f + (i % 2) * 0.7f, 0.15f, 1.6f + (i / 2) * 0.6f), Vector3.zero, new Vector3(1.1f, 0.45f, 1.0f), coal);
        StatusLight(s, new Vector3(0f, 2.55f, 0.55f));
        WorkSpot(s, "WorkSpot_A", new Vector3(-0.8f, 0.05f, 1.6f), 180f);
        WorkSpot(s, "WorkSpot_B", new Vector3(0.8f, 0.05f, 1.6f), 180f);
        var fireLight = PointLight(s, "FireGlow", new Vector3(0f, 0.9f, 1.3f), new Color(1f, 0.45f, 0.15f), 4f, 7f);
        fireLight.shadows = LightShadows.Soft;
        fireLight.gameObject.AddComponent<FireFlicker>();
        Speaker(props, "Speaker_Boiler", new Vector3(-5.4f, 2.5f, 5f), 90f);

        Kit(props, "Dungeon", "barrel_large", new Vector3(-4.6f, 0f, 1.6f), 0f);
        Kit(props, "Dungeon", "barrel_small_stack", new Vector3(-4.7f, 0f, 3.6f), 30f);
        Kit(props, "Dungeon", "crates_stacked", new Vector3(4.4f, 0f, 1.5f), 0f);
        Kit(props, "Dungeon", "keg", new Vector3(-4.8f, 0f, 6.8f), 90f);
        Kit(props, "Restaurant", "pot_large", new Vector3(4.8f, 0f, 6.9f), 0f);
    }

    static void BuildCorridor(Transform stations, Transform props)
    {
        // Lever bank against the starboard side: two levers 2 m apart, so it takes two crew.
        var st = Station(stations, "Station_Doors", new Vector3(5.0f, 0f, 12f));
        var s = st.transform;
        Prim(PrimitiveType.Cube, "LeverBase", s, new Vector3(0.1f, 0.5f, 0f), Vector3.zero, new Vector3(0.6f, 1.0f, 3.0f), steel);
        Prim(PrimitiveType.Cube, "LeverPlate", s, new Vector3(-0.21f, 0.75f, 0f), Vector3.zero, new Vector3(0.04f, 0.5f, 2.6f), brass, false);
        foreach (float z in new[] { -1f, 1f })
        {
            var lever = Prim(PrimitiveType.Cube, z < 0 ? "Lever_A" : "Lever_B", s, new Vector3(-0.35f, 1.25f, z), new Vector3(0f, 0f, 20f), new Vector3(0.1f, 0.9f, 0.1f), steel, false);
            Prim(PrimitiveType.Sphere, "Knob", lever.transform, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(1.8f, 0.2f, 1.8f), statusRed, false);
        }
        StatusLight(s, new Vector3(0f, 1.75f, 0f));
        WorkSpot(s, "WorkSpot_A", new Vector3(-1.1f, 0.05f, -1f), 90f);
        WorkSpot(s, "WorkSpot_B", new Vector3(-1.1f, 0.05f, 1f), 90f);

        // Lifeboat store door, facing into the corridor. Rabib's Doors logic opens and locks it.
        var door = Kit(props, "Restaurant", "door_A", new Vector3(-1.45f, 0f, 10.8f), -90f, Vector3.one * 0.8f, false);
        door.name = "LifeboatStore_Door";
        Prim(PrimitiveType.Cube, "LifeboatStore_Sign", props, new Vector3(-1.46f, 2.05f, 10.15f), new Vector3(0f, -90f, 0f), new Vector3(1.4f, 0.3f, 0.05f), cream, false);
        // The lifeboats inside are visible from above, a cheap reminder of what's at stake.
        for (int i = 0; i < 2; i++)
            Prim(PrimitiveType.Capsule, "Lifeboat_" + i, props, new Vector3(-4.8f + i * 1.6f, 0.4f, 12f), new Vector3(90f, 0f, 0f), new Vector3(1.3f, 1.6f, 0.7f), white);

        // Muster bell on a post in the starboard alcove.
        var bell = Group(props, "MusterBell");
        bell.localPosition = new Vector3(3.2f, 0f, 9.4f);
        Prim(PrimitiveType.Cylinder, "Post", bell, new Vector3(0f, 1.1f, 0f), Vector3.zero, new Vector3(0.15f, 1.1f, 0.15f), darkWood);
        Prim(PrimitiveType.Cube, "Arm", bell, new Vector3(0f, 2.15f, 0.3f), Vector3.zero, new Vector3(0.1f, 0.1f, 0.7f), darkWood, false);
        Prim(PrimitiveType.Sphere, "Bell", bell, new Vector3(0f, 1.85f, 0.55f), Vector3.zero, new Vector3(0.45f, 0.5f, 0.45f), brass, false);

        Speaker(props, "Speaker_Corridor", new Vector3(5.4f, 2.5f, 15f), -90f);
        Kit(props, "Dungeon", "box_stacked", new Vector3(-4.4f, 0f, 9.3f), 0f);
        Kit(props, "Dungeon", "trunk_large_A", new Vector3(-4.6f, 0f, 14.6f), 90f);
    }

    static void BuildWireless(Transform stations, Transform props)
    {
        var st = Station(stations, "Station_Wireless", new Vector3(-4.6f, 0f, 20f));
        var s = st.transform;
        Prim(PrimitiveType.Cube, "Desk", s, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(1.2f, 0.9f, 2.6f), darkWood);
        Prim(PrimitiveType.Cube, "WirelessSet", s, new Vector3(-0.2f, 1.2f, 0.4f), Vector3.zero, new Vector3(0.6f, 0.6f, 1.1f), coal, false);
        Prim(PrimitiveType.Cylinder, "Dial_A", s, new Vector3(0.11f, 1.3f, 0.15f), new Vector3(0f, 0f, 90f), new Vector3(0.22f, 0.02f, 0.22f), brass, false);
        Prim(PrimitiveType.Cylinder, "Dial_B", s, new Vector3(0.11f, 1.3f, 0.65f), new Vector3(0f, 0f, 90f), new Vector3(0.22f, 0.02f, 0.22f), brass, false);
        Prim(PrimitiveType.Cube, "MorseKey", s, new Vector3(0.25f, 0.95f, -0.3f), Vector3.zero, new Vector3(0.2f, 0.06f, 0.3f), brass, false);
        Prim(PrimitiveType.Cube, "MessageTray", s, new Vector3(0.2f, 0.95f, -0.95f), Vector3.zero, new Vector3(0.45f, 0.05f, 0.35f), cream, false);
        Marker(s, "PickUp_IceWarning", new Vector3(0.2f, 1.0f, -0.95f), 0f);
        StatusLight(s, new Vector3(-0.2f, 1.75f, 0.4f));
        WorkSpot(s, "WorkSpot_A", new Vector3(1.1f, 0.05f, 0f), -90f);
        Kit(s, "Furniture", "chair_A", new Vector3(1.0f, 0f, 0.1f), -90f, null, false);
        PointLight(s, "DeskLamp", new Vector3(0.3f, 1.6f, -0.4f), new Color(1f, 0.75f, 0.45f), 1.4f, 3f);

        Speaker(props, "Speaker_Wireless", new Vector3(5.4f, 2.5f, 21f), -90f);
        Kit(props, "Furniture", "shelf_B_large_decorated", new Vector3(-4f, 0f, 22.9f), 180f);
        Kit(props, "Furniture", "cabinet_medium_decorated", new Vector3(4.9f, 0f, 19f), -90f);
        Kit(props, "Furniture", "rug_rectangle_stripes_A", new Vector3(1f, 0.06f, 20f), 90f, null, false);
    }

    static void BuildBridge(Transform stations, Transform props)
    {
        var st = Station(stations, "Station_Wheel", new Vector3(0f, 0f, 29f));
        var s = st.transform;
        Prim(PrimitiveType.Cylinder, "Pedestal", s, new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(0.45f, 0.6f, 0.45f), darkWood);
        var wheel = Group(s, "Wheel");
        wheel.localPosition = new Vector3(0f, 1.35f, -0.3f);
        Prim(PrimitiveType.Cylinder, "Rim", wheel, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(1.3f, 0.03f, 1.3f), darkWood, false);
        Prim(PrimitiveType.Cylinder, "Hub", wheel, new Vector3(0f, 0f, -0.05f), new Vector3(90f, 0f, 0f), new Vector3(0.3f, 0.08f, 0.3f), brass, false);
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Cube, "Spoke_" + i, wheel, new Vector3(0f, 0f, -0.06f), new Vector3(0f, 0f, i * 45f), new Vector3(0.07f, 1.8f, 0.07f), brass, false);
        StatusLight(s, new Vector3(0f, 2.35f, 0.1f));
        WorkSpot(s, "WorkSpot_A", new Vector3(-0.7f, 0.05f, -1.1f), 0f);
        WorkSpot(s, "WorkSpot_B", new Vector3(0.7f, 0.05f, -1.1f), 0f);

        // Engine telegraph ("full ahead") and the binnacle with the compass that will start to lie.
        var tele = Group(props, "Telegraph");
        tele.localPosition = new Vector3(2.2f, 0f, 29.6f);
        Prim(PrimitiveType.Cylinder, "Post", tele, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.25f, 0.55f, 0.25f), brass);
        Prim(PrimitiveType.Cylinder, "Face", tele, new Vector3(0f, 1.3f, -0.05f), new Vector3(90f, 0f, 0f), new Vector3(0.6f, 0.08f, 0.6f), cream, false);
        var bin = Group(props, "Binnacle");
        bin.localPosition = new Vector3(-2.2f, 0f, 29.6f);
        Prim(PrimitiveType.Cylinder, "Post", bin, new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.35f, 0.5f, 0.35f), darkWood);
        Prim(PrimitiveType.Sphere, "Compass", bin, new Vector3(0f, 1.15f, 0f), Vector3.zero, new Vector3(0.45f, 0.35f, 0.45f), brass, false);

        // Chart table: where ice warnings are delivered.
        var chart = Kit(props, "Dungeon", "table_medium", new Vector3(-4.2f, 0f, 26f), 90f);
        chart.name = "ChartTable";
        Marker(props, "DropPoint_Bridge", new Vector3(-4.2f, 1.0f, 26f), 0f);
        Kit(props, "Furniture", "lamp_table", new Vector3(-4.4f, 0.98f, 26.8f), 0f, Vector3.one * 0.6f, false);
        Speaker(props, "Speaker_Bridge", new Vector3(5.4f, 2.5f, 27f), -90f);
        Kit(props, "Furniture", "cabinet_medium", new Vector3(4.9f, 0f, 25.5f), -90f);
    }

    static void BuildBow(Transform props)
    {
        // Railings round the open bow deck.
        for (int xi = -1; xi <= 1; xi++) Kit(props, "Dungeon", "barrier", new Vector3(xi * 4f, 0f, 35.8f), 0f, null, true, darkWood);
        Kit(props, "Dungeon", "barrier", new Vector3(-5.8f, 0f, 34f), 90f, null, true, darkWood);
        Kit(props, "Dungeon", "barrier", new Vector3(5.8f, 0f, 34f), 90f, null, true, darkWood);
        Kit(props, "Dungeon", "barrel_small_stack", new Vector3(-4.8f, 0f, 33.2f), 0f);
    }

    static Light BuildMoon(Transform p)
    {
        var go = new GameObject("Moon");
        go.transform.SetParent(p, false);
        go.transform.rotation = Quaternion.Euler(42f, 35f, 0f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.shadows = LightShadows.Soft;
        l.color = new Color(0.62f, 0.7f, 1f);
        l.intensity = 0.45f;
        return l;
    }

    static Light[] BuildLamps(Transform p)
    {
        var lamps = new List<Light>();
        // Wall sconces, so no bulbs hang in the middle of the camera's view. side = which way the wall faces.
        var spots = new (string name, Vector3 pos, float side)[]
        {
            ("Lamp_Boiler", new Vector3(-5.25f, 2.3f, 5.5f), 1f),
            ("Lamp_Corridor", new Vector3(-1.25f, 2.3f, 13.5f), 1f),
            ("Lamp_Wireless", new Vector3(5.25f, 2.3f, 18.5f), -1f),
            ("Lamp_Bridge", new Vector3(-5.25f, 2.3f, 28.5f), 1f),
        };
        foreach (var (name, pos, side) in spots)
        {
            var l = PointLight(p, name, pos, new Color(1f, 0.76f, 0.48f), 2.2f, 11f);
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.85f;
            l.shadowNearPlane = 0.3f;
            Prim(PrimitiveType.Cube, "Bracket", l.transform, new Vector3(-0.12f * side, 0f, 0f), Vector3.zero, new Vector3(0.25f, 0.08f, 0.08f), brass, false);
            Prim(PrimitiveType.Sphere, "Bulb", l.transform, Vector3.zero, Vector3.zero, Vector3.one * 0.22f, lampGlass, false);
            lamps.Add(l);
        }
        return lamps.ToArray();
    }

    // ---------- Detail pass: the small things that make it read as a working ship ----------

    static void BuildDetails(Transform p)
    {
        var d = Group(p, "Details");

        // Stokehold pipework along the stern bulkhead, a valve, gauges on the boiler, shovels by the coal.
        Pipe(d, "SternPipe_Main", new Vector3(0f, 2.35f, 0.75f), new Vector3(0f, 0f, 90f), 10.4f, 0.22f, steel);
        Pipe(d, "SternPipe_Brass", new Vector3(0f, 1.95f, 0.72f), new Vector3(0f, 0f, 90f), 10.4f, 0.12f, brass);
        foreach (float x in new[] { -5.15f, 5.15f })
        {
            Pipe(d, "RiserPipe", new Vector3(x, 1.17f, 0.75f), Vector3.zero, 2.35f, 0.22f, steel);
            Prim(PrimitiveType.Sphere, "Elbow", d, new Vector3(x, 2.35f, 0.75f), Vector3.zero, Vector3.one * 0.3f, steel, false);
        }
        Pipe(d, "BoilerFeed", new Vector3(0f, 2.55f, 1.15f), new Vector3(90f, 0f, 0f), 0.9f, 0.25f, steel);
        Valve(d, new Vector3(-2.5f, 2.35f, 0.95f));
        Valve(d, new Vector3(3.2f, 2.35f, 0.95f));
        Gauge(d, new Vector3(-0.7f, 2.0f, 3.68f), 0f, 35f);
        Gauge(d, new Vector3(0.7f, 2.0f, 3.68f), 0f, -50f);
        Shovel(d, new Vector3(4.6f, 0f, 5.9f), -14f);
        Shovel(d, new Vector3(4.95f, 0f, 5.5f), -8f);

        // Corridor: fire buckets, a lifebuoy, the notice board, pipes overhead, a runner.
        Prim(PrimitiveType.Cube, "BucketRail", d, new Vector3(5.42f, 1.3f, 14.9f), Vector3.zero, new Vector3(0.06f, 0.06f, 1.3f), brass, false);
        for (int i = 0; i < 3; i++)
            Prim(PrimitiveType.Cylinder, "FireBucket_" + i, d, new Vector3(5.25f, 1.08f, 14.45f + i * 0.45f), Vector3.zero, new Vector3(0.28f, 0.17f, 0.28f), redPaint, false);
        Lifebuoy(d, new Vector3(-1.46f, 1.5f, 14.2f), 90f);
        var board = Group(d, "NoticeBoard");
        board.localPosition = new Vector3(4.0f, 1.6f, 15.47f);
        Prim(PrimitiveType.Cube, "Board", board, Vector3.zero, Vector3.zero, new Vector3(1.2f, 0.8f, 0.05f), darkWood, false);
        Prim(PrimitiveType.Cube, "Notice_A", board, new Vector3(-0.3f, 0.08f, -0.03f), new Vector3(0f, 0f, 4f), new Vector3(0.35f, 0.45f, 0.01f), cream, false);
        Prim(PrimitiveType.Cube, "Notice_B", board, new Vector3(0.25f, -0.05f, -0.03f), new Vector3(0f, 0f, -6f), new Vector3(0.4f, 0.3f, 0.01f), cream, false);
        Pipe(d, "CorridorPipe", new Vector3(-1.42f, 2.6f, 12f), new Vector3(90f, 0f, 0f), 8f, 0.12f, steel);
        Kit(d, "Furniture", "rug_rectangle_stripes_B", new Vector3(0f, 0.06f, 12f), 90f, null, false);

        // Wireless: the wall clock (Riham can turn its hands for "the clock skips back"), papers, cables, a chart.
        var clock = Group(d, "WirelessClock");
        clock.localPosition = new Vector3(-5.44f, 2.15f, 21.7f);
        clock.localRotation = Quaternion.Euler(0f, 90f, 0f);
        Prim(PrimitiveType.Cylinder, "Rim", clock, new Vector3(0f, 0f, -0.01f), new Vector3(90f, 0f, 0f), new Vector3(0.58f, 0.02f, 0.58f), brass, false);
        Prim(PrimitiveType.Cylinder, "Face", clock, new Vector3(0f, 0f, 0.01f), new Vector3(90f, 0f, 0f), new Vector3(0.5f, 0.02f, 0.5f), cream, false);
        ClockHand(clock, "HourHand", 0.13f, 0.035f, -60f);
        ClockHand(clock, "MinuteHand", 0.2f, 0.022f, 70f);
        Prim(PrimitiveType.Cube, "Papers_A", d, new Vector3(-4.35f, 0.915f, 19.4f), new Vector3(0f, 12f, 0f), new Vector3(0.3f, 0.01f, 0.42f), cream, false);
        Prim(PrimitiveType.Cube, "Papers_B", d, new Vector3(-4.5f, 0.925f, 19.05f), new Vector3(0f, -8f, 0f), new Vector3(0.3f, 0.01f, 0.42f), cream, false);
        Pipe(d, "AerialCable_A", new Vector3(-5.4f, 2.05f, 20.25f), Vector3.zero, 1.1f, 0.03f, coal);
        Pipe(d, "AerialCable_B", new Vector3(-5.4f, 2.05f, 20.55f), Vector3.zero, 1.1f, 0.03f, coal);
        Kit(d, "Furniture", "pictureframe_large_A", new Vector3(5.42f, 1.4f, 22.4f), -90f, null, false);
        Kit(d, "Furniture", "book_set", new Vector3(4.9f, 1.0f, 19.3f), -90f, null, false);

        // Bridge: voice pipe, the chart on the chart table, a lifebuoy.
        Pipe(d, "VoicePipe", new Vector3(1.25f, 0.7f, 30.6f), Vector3.zero, 1.4f, 0.12f, brass);
        Prim(PrimitiveType.Sphere, "VoicePipe_Mouth", d, new Vector3(1.25f, 1.45f, 30.55f), new Vector3(-20f, 0f, 0f), new Vector3(0.28f, 0.18f, 0.28f), brass, false);
        Prim(PrimitiveType.Cube, "Chart", d, new Vector3(-4.2f, 1.01f, 26f), new Vector3(0f, 93f, 0f), new Vector3(1.3f, 0.01f, 0.85f), cream, false);
        Lifebuoy(d, new Vector3(5.46f, 1.5f, 30.2f), -90f);

        // Bow: mooring bollards.
        foreach (float x in new[] { -4.4f, 4.4f })
            Prim(PrimitiveType.Cylinder, "Bollard", d, new Vector3(x, 0.3f, 34.4f), Vector3.zero, new Vector3(0.45f, 0.3f, 0.45f), coal);
    }

    /// <summary>Things outside the walls the camera sees: lit portholes down the hull, the funnel, the foremast.</summary>
    static void BuildExterior(Transform p)
    {
        var e = Group(p, "Exterior");
        for (float z = 1.5f; z <= 34.6f; z += 1.5f)
            foreach (float side in new[] { -1f, 1f })
                Prim(PrimitiveType.Cylinder, "Porthole", e, new Vector3(6.61f * side, -0.95f, z), new Vector3(0f, 0f, 90f), new Vector3(0.24f, 0.02f, 0.24f), portholeGlow, false);

        var funnel = Group(e, "Funnel");
        funnel.localPosition = new Vector3(0f, 0f, 1.6f);
        Prim(PrimitiveType.Cylinder, "Stack", funnel, new Vector3(0f, 7.5f, 0f), Vector3.zero, new Vector3(2.6f, 3.2f, 2.6f), buff, false);
        Prim(PrimitiveType.Cylinder, "Top", funnel, new Vector3(0f, 10.35f, 0f), Vector3.zero, new Vector3(2.66f, 0.4f, 2.66f), coal, false);

        var mast = Group(e, "Foremast");
        mast.localPosition = new Vector3(0f, 0f, 34.6f);
        Prim(PrimitiveType.Cylinder, "Mast", mast, new Vector3(0f, 6f, 0f), Vector3.zero, new Vector3(0.25f, 6f, 0.25f), darkWood, false);
        Prim(PrimitiveType.Cylinder, "CrowsNest", mast, new Vector3(0f, 8.6f, 0f), Vector3.zero, new Vector3(1.1f, 0.45f, 1.1f), darkWood, false);
        Rigging(mast, new Vector3(0f, 11.5f, 0f), new Vector3(-5.8f, 0.9f, -1.6f));
        Rigging(mast, new Vector3(0f, 11.5f, 0f), new Vector3(5.8f, 0.9f, -1.6f));
        Rigging(mast, new Vector3(0f, 11.5f, 0f), new Vector3(0f, 0.9f, 1.2f));
    }

    /// <summary>Smoke, steam, embers, dust in the lamplight, a reflection probe for the brass.</summary>
    static void BuildAtmosphere(Transform p)
    {
        Particles(p, "FunnelSmoke", new Vector3(0f, 10.9f, 1.6f), smokeMat, 6f, 7f, new Vector2(1.6f, 2.8f), new Vector2(0.8f, 1.4f), 10f, new Vector3(0f, 0.3f, -1.6f), 200);
        Particles(p, "BoilerSteam", new Vector3(0.9f, 2.9f, 2.4f), steamMat, 4f, 3.5f, new Vector2(0.3f, 0.7f), new Vector2(0.3f, 0.6f), 15f, Vector3.zero, 60);
        Particles(p, "ValveLeak", new Vector3(-2.5f, 2.45f, 1.05f), steamMat, 6f, 1.6f, new Vector2(0.15f, 0.35f), new Vector2(0.6f, 1.0f), 8f, Vector3.zero, 40);
        Particles(p, "FireboxEmbers", new Vector3(0f, 1.0f, 3.95f), emberMat, 5f, 1.4f, new Vector2(0.03f, 0.06f), new Vector2(0.4f, 1.1f), 25f, Vector3.zero, 40);
        foreach (var (name, z) in new[] { ("Dust_Boiler", 4f), ("Dust_Corridor", 12f), ("Dust_Wireless", 20f), ("Dust_Bridge", 28f) })
            Dust(p, name, new Vector3(0f, 1.4f, z));

        var probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
        probe.transform.SetParent(p, false);
        probe.transform.localPosition = new Vector3(0f, 1.6f, 18f);
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.size = new Vector3(16f, 8f, 40f);
        probe.resolution = 128;
    }

    static UnityEngine.Rendering.Volume PostVolume(Transform p, string name, string part, float weight)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var v = go.AddComponent<UnityEngine.Rendering.Volume>();
        v.isGlobal = true;
        v.priority = 1f;
        v.weight = weight;
        v.sharedProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(RenderLookSetup.ProfilePath(part));
        return v;
    }

    static void Pipe(Transform parent, string name, Vector3 pos, Vector3 euler, float length, float diameter, Material mat) =>
        Prim(PrimitiveType.Cylinder, name, parent, pos, euler, new Vector3(diameter, length / 2f, diameter), mat, false);

    static void Valve(Transform parent, Vector3 pos)
    {
        var v = Group(parent, "Valve");
        v.localPosition = pos;
        Prim(PrimitiveType.Cylinder, "Stem", v, new Vector3(0f, 0f, -0.08f), new Vector3(90f, 0f, 0f), new Vector3(0.06f, 0.1f, 0.06f), steel, false);
        Prim(PrimitiveType.Cylinder, "Wheel", v, new Vector3(0f, 0f, 0.02f), new Vector3(90f, 0f, 0f), new Vector3(0.42f, 0.025f, 0.42f), redPaint, false);
    }

    static void Gauge(Transform parent, Vector3 pos, float yaw, float needle)
    {
        var g = Group(parent, "PressureGauge");
        g.localPosition = pos;
        g.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Prim(PrimitiveType.Cylinder, "Rim", g, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.36f, 0.03f, 0.36f), brass, false);
        Prim(PrimitiveType.Cylinder, "Face", g, new Vector3(0f, 0f, 0.02f), new Vector3(90f, 0f, 0f), new Vector3(0.3f, 0.02f, 0.3f), cream, false);
        Prim(PrimitiveType.Cube, "Needle", g, new Vector3(0f, 0.04f, 0.045f), new Vector3(0f, 0f, needle), new Vector3(0.015f, 0.11f, 0.01f), redPaint, false);
    }

    static void Shovel(Transform parent, Vector3 pos, float lean)
    {
        var s = Group(parent, "Shovel");
        s.localPosition = pos;
        s.localRotation = Quaternion.Euler(0f, 0f, lean);
        Prim(PrimitiveType.Cylinder, "Handle", s, new Vector3(0f, 0.75f, 0f), Vector3.zero, new Vector3(0.045f, 0.55f, 0.045f), darkWood, false);
        Prim(PrimitiveType.Cube, "Blade", s, new Vector3(0f, 0.12f, 0f), new Vector3(15f, 0f, 0f), new Vector3(0.28f, 0.24f, 0.03f), steel, false);
    }

    /// <summary>A red-and-white ring made of 12 segments, hung flat on a wall facing yaw.</summary>
    static void Lifebuoy(Transform parent, Vector3 pos, float yaw)
    {
        var ring = Group(parent, "Lifebuoy");
        ring.localPosition = pos;
        ring.localRotation = Quaternion.Euler(0f, yaw, 0f);
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f;
            var at = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad), 0f) * 0.3f;
            Prim(PrimitiveType.Cube, "Segment", ring, at, new Vector3(0f, 0f, -a), new Vector3(0.17f, 0.1f, 0.1f), (i / 3) % 2 == 0 ? redPaint : white, false);
        }
    }

    static void ClockHand(Transform clock, string name, float length, float width, float angle)
    {
        var pivot = Group(clock, name);
        pivot.localPosition = new Vector3(0f, 0f, 0.03f);
        pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        Prim(PrimitiveType.Cube, "Hand", pivot, new Vector3(0f, length / 2f, 0f), Vector3.zero, new Vector3(width, length, 0.01f), coal, false);
    }

    static void Rigging(Transform parent, Vector3 from, Vector3 to)
    {
        var go = new GameObject("Rigging");
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = line.endWidth = 0.035f;
        line.sharedMaterial = coal;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static ParticleSystem Particles(Transform parent, string name, Vector3 pos, Material mat, float rate, float lifetime,
        Vector2 size, Vector2 speed, float coneAngle, Vector3 drift, int max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // the cone emits along local +Z, so point it up
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = 0.15f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));
        if (drift != Vector3.zero)
        {
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = drift.x; vel.y = drift.y; vel.z = drift.z;
        }
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static void Dust(Transform parent, string name, Vector3 pos)
    {
        var ps = Particles(parent, name, pos, dustMat, 4f, 9f, new Vector2(0.02f, 0.045f), new Vector2(0.02f, 0.08f), 0f, Vector3.zero, 40);
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(10f, 7f, 2.2f); // rotated -90 on X, so this is 10 wide, 7 deep, 2.2 high
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.3f;
    }

    // ---------- Building blocks ----------

    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Station(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        return go;
    }

    /// <summary>Big red/green light Rabib's station script switches. Starts red ("not done").</summary>
    static void StatusLight(Transform station, Vector3 localPos)
    {
        var bulb = Prim(PrimitiveType.Sphere, "StatusLight", station, localPos, Vector3.zero, Vector3.one * 0.32f, statusRed, false);
        var l = bulb.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.2f, 0.15f);
        l.intensity = 1.5f;
        l.range = 2.5f;
        l.shadows = LightShadows.None;
    }

    static void WorkSpot(Transform station, string name, Vector3 localPos, float yaw) => Marker(station, name, localPos, yaw);

    static void Marker(Transform parent, string name, Vector3 localPos, float yaw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    static void Speaker(Transform parent, string name, Vector3 pos, float yaw)
    {
        var g = Group(parent, name);
        g.localPosition = pos;
        g.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Prim(PrimitiveType.Cylinder, "Horn", g, new Vector3(0f, 0f, 0.15f), new Vector3(90f, 0f, 0f), new Vector3(0.35f, 0.15f, 0.35f), brass, false);
    }

    static void Room(Transform parent, string id, Vector3 center, Vector3 size)
    {
        var go = new GameObject("Room_" + id);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        var bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        bc.isTrigger = true;
        go.AddComponent<RoomVolume>().roomId = id;
    }

    static Light PointLight(Transform parent, string name, Vector3 localPos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 euler, Vector3 scale, Material mat, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.isStatic = true;
        return go;
    }

    /// <summary>Places a KayKit model with a box collider fitted to its meshes.</summary>
    static GameObject Kit(Transform parent, string pack, string piece, Vector3 localPos, float yaw,
        Vector3? scale = null, bool collider = true, Material overrideMat = null)
    {
        string path = $"{KayKit}/{pack}/{piece}.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null) { Debug.LogWarning($"[ShipDeck] Missing {path}"); return new GameObject("MISSING_" + piece); }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = scale ?? Vector3.one;
        if (overrideMat != null)
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = overrideMat;
                r.sharedMaterials = mats;
            }
        if (collider)
        {
            var b = LocalMeshBounds(go.transform);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = b.size;
        }
        foreach (var tr in go.GetComponentsInChildren<Transform>()) tr.gameObject.isStatic = true;
        return go;
    }

    static Bounds LocalMeshBounds(Transform root)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!any) { b = new Bounds(local, Vector3.zero); any = true; }
                else b.Encapsulate(local);
            }
        }
        return b;
    }

    // ---------- Materials ----------

    static void CreateMaterials()
    {
        var dungeonTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KayKit}/Dungeon/dungeon_texture.png");
        navyWall = ShipMat("Ship_WallNavy", new Color(0.50f, 0.60f, 0.92f), dungeonTex);
        darkFloor = ShipMat("Ship_FloorSoot", new Color(0.45f, 0.43f, 0.45f), dungeonTex);
        brass = ShipMat("Ship_Brass", new Color(0.80f, 0.60f, 0.26f), null, 0.7f, 0.55f);
        coal = ShipMat("Ship_Coal", new Color(0.08f, 0.08f, 0.09f), null, 0f, 0.2f);
        cream = ShipMat("Ship_Cream", new Color(0.92f, 0.87f, 0.75f));
        darkWood = ShipMat("Ship_DarkWood", new Color(0.33f, 0.21f, 0.13f));
        steel = ShipMat("Ship_Steel", new Color(0.24f, 0.26f, 0.30f), null, 0.6f, 0.35f);
        hull = ShipMat("Ship_Hull", new Color(0.07f, 0.08f, 0.12f));
        sea = ShipMat("Ship_Sea", new Color(0.04f, 0.10f, 0.18f), null, 0f, 0.85f);
        white = ShipMat("Ship_LifeboatWhite", new Color(0.93f, 0.92f, 0.88f));
        fire = ShipMat("Ship_FireGlow", new Color(1f, 0.45f, 0.12f), null, 0f, 0.2f, new Color(1f, 0.4f, 0.1f) * 3f);
        statusRed = ShipMat("Ship_StatusRed", new Color(1f, 0.15f, 0.1f), null, 0f, 0.5f, new Color(1f, 0.12f, 0.08f) * 2.5f);
        ShipMat("Ship_StatusGreen", new Color(0.2f, 1f, 0.35f), null, 0f, 0.5f, new Color(0.15f, 1f, 0.3f) * 2.5f);
        lampGlass = ShipMat("Ship_LampGlass", new Color(1f, 0.85f, 0.6f), null, 0f, 0.5f, new Color(1f, 0.75f, 0.45f) * 2f);
        redPaint = ShipMat("Ship_RedPaint", new Color(0.62f, 0.09f, 0.07f), null, 0f, 0.35f);
        buff = ShipMat("Ship_FunnelBuff", new Color(0.82f, 0.62f, 0.34f), null, 0f, 0.25f);
        portholeGlow = ShipMat("Ship_PortholeGlow", new Color(1f, 0.82f, 0.5f), null, 0f, 0.6f, new Color(1f, 0.7f, 0.35f) * 1.6f);

        // The sea: ripples from a generated normal map, tiled small so the moon glints break up.
        var ripples = AssetDatabase.LoadAssetAtPath<Texture2D>(RenderLookSetup.SeaNormalPath);
        sea.SetTexture("_BumpMap", ripples);
        sea.EnableKeyword("_NORMALMAP");
        sea.SetFloat("_BumpScale", 0.6f);
        sea.SetTextureScale("_BaseMap", new Vector2(70f, 70f));
        sea.SetFloat("_Smoothness", 0.92f);

        smokeMat = ParticleMat("Ship_Smoke", new Color(0.12f, 0.12f, 0.13f, 0.55f));
        steamMat = ParticleMat("Ship_Steam", new Color(0.85f, 0.85f, 0.9f, 0.3f));
        emberMat = ParticleMat("Ship_Embers", new Color(1f, 0.55f, 0.2f, 1f) * 2.5f);
        dustMat = ParticleMat("Ship_Dust", new Color(1f, 0.9f, 0.75f, 0.35f));
    }

    static Material ParticleMat(string name, Color tint)
    {
        string path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>(ParticleSource);
            mat = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", tint);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material ShipMat(string name, Color color, Texture2D baseMap = null, float metallic = 0f, float smoothness = 0.15f, Color? emission = null)
    {
        MovementPrototypeBuilder.EnsureFolder(MatFolder);
        string path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- Checks and screenshots ----------

    /// <summary>Walks stern to bow through every doorway and renders one shot per room into Logs/ShipShot_*.png.</summary>
    public static void CheckBatch()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ThirdPersonMover mover = null;
        AutoFollowCamera follow = null;
        PartMood mood = null;
        foreach (var r in scene.GetRootGameObjects())
        {
            if (mover == null) mover = r.GetComponentInChildren<ThirdPersonMover>();
            if (follow == null) follow = r.GetComponentInChildren<AutoFollowCamera>();
            if (mood == null) mood = r.GetComponentInChildren<PartMood>();
        }
        mover.readOwnInput = false;
        var sb = new StringBuilder("[ShipDeck checks]\n");
        bool ok = true;

        // Spawns and work spots are clear of everything except crew.
        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Spawn_") && !t.name.StartsWith("WorkSpot_")) continue;
            string blockedBy = null;
            foreach (var c in Physics.OverlapCapsule(t.position + Vector3.up * 0.45f, t.position + Vector3.up * 1.4f, 0.33f, ~0, QueryTriggerInteraction.Ignore))
                if (!(c is CharacterController)) { blockedBy = c.name; break; }
            ok &= blockedBy == null;
            sb.Append($"  {(blockedBy == null ? "PASS" : "FAIL")}  {t.parent.name}/{t.name} is clear{(blockedBy == null ? "" : " (hits " + blockedBy + ")")}\n");
        }

        // Stern to bow straight through the doorways (the wheel stops you at the bridge).
        var f = MovementChecks.Sim(mover, follow, new Vector3(0f, 0.05f, 6.2f), 0f, (8f, new Vector2(0f, 1f)));
        float reached = f[f.Count - 1].pos.z;
        int inWall = 0, hiddenRun = 0, run = 0;
        foreach (var x in f)
        {
            if (x.camInWall) inWall++;
            run = x.playerHidden ? run + 1 : 0;
            hiddenRun = Mathf.Max(hiddenRun, run);
        }
        bool walkOk = reached > 27f && inWall == 0 && hiddenRun <= 5;
        ok &= walkOk;
        sb.Append($"  {(walkOk ? "PASS" : "FAIL")}  Walk stern to bridge: reached z={reached:0.0}, camera-in-wall frames {inWall}, longest hidden run {hiddenRun}\n");

        // Into the lifeboat-store side and the lever alcove, then out, to exercise the camera near walls.
        var g = MovementChecks.Sim(mover, follow, new Vector3(0f, 0.05f, 12f), 0f, (1.5f, new Vector2(1f, 0f)), (2f, new Vector2(-1f, 0f)), (1f, new Vector2(0f, 1f)));
        int gWall = 0; foreach (var x in g) if (x.camInWall) gWall++;
        ok &= gWall == 0;
        sb.Append($"  {(gWall == 0 ? "PASS" : "FAIL")}  Corridor side to side: camera-in-wall frames {gWall}\n");

        Shots(mover, follow, mood);
        sb.Append(ok ? "ALL PASSED" : "SOME CHECKS FAILED");
        System.IO.File.WriteAllText("Logs/ShipDeckChecks.txt", sb.ToString());
        Debug.Log(sb.ToString());
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static void Shots(ThirdPersonMover mover, AutoFollowCamera follow, PartMood mood)
    {
        var cam = follow.GetComponent<Camera>();
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.aspect = 1280f / 720f;
        var idle = new Vector2(0f, 0f);
        cam.Render(); // the first headless render is garbage

        // Animators don't run in edit mode, so the crew shows its T-pose in these pictures.
        void Shot(string name, Vector3 pos, float yaw, int part, params (float, Vector2)[] steps)
        {
            mood.ApplyImmediate(part);
            MovementChecks.Sim(mover, follow, pos, yaw, steps);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes($"Logs/ShipShot_{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        Shot("1_boiler", new Vector3(-0.8f, 0.05f, 4.8f), 180f, 1, (1.5f, idle));
        Shot("2_corridor_levers", new Vector3(3.9f, 0.05f, 11f), 90f, 1, (1.5f, idle));
        Shot("3_lifeboat_store", new Vector3(1.5f, 0.05f, 11f), 270f, 1, (1.5f, idle));
        Shot("4_wireless", new Vector3(-1f, 0.05f, 20f), 270f, 2, (1.5f, idle));
        Shot("5_bridge_wheel", new Vector3(0f, 0.05f, 25.5f), 0f, 2, (1.5f, idle));
        Shot("6_walking_aft_truth", new Vector3(0f, 0.05f, 22f), 180f, 3, (1.2f, new Vector2(0f, 1f)));
        mood.ApplyImmediate(1);

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
    }
}
