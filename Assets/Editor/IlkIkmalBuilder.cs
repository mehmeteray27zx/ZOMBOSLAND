using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Zombosland;

// Generates the Ilk Ikmal scene. Never hand-edit IlkIkmal.unity; change this and re-run.
// Shared helpers (materials, Box, player, effects, audio, post, lighting) come from SandboxBuilder.
public static class IlkIkmalBuilder
{
    const string ScenePath = "Assets/Scenes/IlkIkmal.unity";
    static readonly Vector3 PlayerSpawn = new Vector3(-30f, .05f, -36f);
    static readonly Vector3 MapCenter = new Vector3(2.5f, 0f, 0f);
    static readonly Vector3 MapSize = new Vector3(100f, 0f, 95f);

    const float WallHeight = 4.5f;
    const float WallBase = -2f;
    const float DoorHeight = 2.4f;

    // Axis aligned rectangle on the ground plane (x east, z north).
    struct Rc
    {
        public float x0, x1, z0, z1;
        public Rc(float x0, float x1, float z0, float z1) { this.x0 = x0; this.x1 = x1; this.z0 = z0; this.z1 = z1; }
        public bool Contains(float x, float z) { return x > x0 && x < x1 && z > z0 && z < z1; }
        public float Area { get { return (x1 - x0) * (z1 - z0); } }
    }

    // Walkable space (the map table in the spec). Everything outside of it inside the outer bounds is solid.
    static readonly List<Rc> Free = new List<Rc>();
    // Solid rectangles (wall blocks and big furniture) used for the builder's own clearance measurements.
    static readonly List<Rc> Solid = new List<Rc>();

    [MenuItem("Zombosland/Regenerate IlkIkmal (overwrites scene)")]
    public static void Regenerate()
    {
        Generate();
        SandboxBuilder.WriteBuildSettings();
    }

    [MenuItem("Zombosland/Regenerate All and Build")]
    public static void BuildAll()
    {
        SandboxBuilder.GenerateScene();
        Generate();
        SandboxBuilder.WriteBuildSettings();
        SandboxBuilder.BuildOnly();
    }

    internal static void Generate()
    {
        SandboxBuilder.Report.Clear();
        LookDevSetup.Report.Clear();
        SandboxBuilder.PrepareProject();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var mats = SandboxBuilder.CreateMaterials();

        LookDevSetup.SetupSky();
        // Open region: long view distances, thinner fog than the walled map (linear colour is unchanged).
        RenderSettings.fogDensity = 0.0024f;
        // Outdoor shaded sides must stay readable: explicit sky/horizon/ground ambient instead of the dim HDRI average.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.60f, .65f, .72f);
        RenderSettings.ambientEquatorColor = new Color(.46f, .47f, .43f);
        RenderSettings.ambientGroundColor = new Color(.27f, .25f, .20f);
        SandboxBuilder.BuildLighting(new Vector3(MapCenter.x, 3f, MapCenter.z), new Vector3(MapSize.x, 12f, MapSize.z));
        // Soft shadowless sky fill from the side opposite the sun, so faces turned away from it keep their colour.
        var fill = new GameObject("Sky fill").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(.78f, .84f, .95f);
        fill.intensity = 0.55f;
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(32f, 145f, 0f);
        BuildMap(mats);
        OpenWorldBuilder.BuildWorld(mats);              // terrain, roads, bridge, water, vegetation, station, hut, tower

        var player = SandboxBuilder.BuildPlayer(mats.metalPlate, mats.rustyMetal, mats.accent);
        player.transform.position = PlayerSpawn;
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // facing +X (east)
        player.AddComponent<Interactor>();
        player.GetComponentInChildren<Camera>().farClipPlane = 900f;
        var game = player.GetComponent<Sandbox>();
        SandboxBuilder.ConfigureSandbox(game, 36, 22f); // start reserve 36, shot noise radius 22 m (CombatSandbox: 72 / 30)
        OpenWorldBuilder.BuildPlayerSurvival(player);

        var zombiePrefabs = ZombieFactory.CreatePrefabs(mats.coat, mats.skin, mats.denim, SandboxBuilder.Report);
        var director = BuildDirector(game, zombiePrefabs);
        BuildInteractables(mats, game);                 // doors, fuse chain, yard workbench / storage / lamps, loot, ammo boxes, quest wiring
        OpenWorldBuilder.BakeNavMesh(director);         // after every static collider exists, before zombies are sampled
        BuildZombies(game, zombiePrefabs);
        SandboxBuilder.BuildEffectsAndAudio();
        SandboxBuilder.BuildPostProcessing(player.GetComponentInChildren<Camera>());
        ReportLights();
        SandboxBuilder.Report.Add("open region summary: " + OpenWorldBuilder.Summary());

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        foreach (string line in LookDevSetup.Report) SandboxBuilder.Report.Add(line);
        foreach (string line in SandboxBuilder.Report) Debug.Log("ILKIKMAL_BUILD_NOTE: " + line);
    }

    // ---------- map ----------

    static void AddFree(float x0, float x1, float z0, float z1) { Free.Add(new Rc(x0, x1, z0, z1)); }

    static void BuildMap(SandboxBuilder.MaterialSet mats)
    {
        Free.Clear();
        Solid.Clear();

        // A safe room, its east door gap, lower road, B main street, C pharmacy + connector, D depot, depot entrance and side door,
        // E side passage (corridor, long arm, two widenings), F service yard.
        AddFree(-34f, -26f, -40f, -30f);        // A interior
        AddFree(-26f, -25.6f, -35f, -33f);      // A door gap (2.0 m)
        AddFree(-25.6f, 24f, -40f, -32f);       // lower road (8 m), always open
        AddFree(-11.5f, -0.5f, -32f, 20.6f);    // B main street (11 m)
        AddFree(-26f, -18f, -8f, 4f);           // C pharmacy interior
        AddFree(-18f, -11.5f, -4f, 0f);         // C connector (4 m)
        AddFree(-10f, 14f, 21f, 39f);           // D depot interior
        AddFree(-7.1f, -4.9f, 20.6f, 21f);      // D south entrance (2.2 m, open gap)
        AddFree(14f, 14.4f, 32f, 34f);          // D east side door gap (2.0 m)
        AddFree(14.4f, 35.75f, 31.25f, 34.75f); // E corridor east (3.5 m)
        AddFree(32.25f, 35.75f, -19f, 34.75f);  // E arm south (3.5 m)
        AddFree(30.5f, 37.5f, 12f, 18f);        // E widening 1, centre (34, 15)
        AddFree(30.5f, 37.5f, -3f, 3f);         // E widening 2, centre (34, 0)
        AddFree(24f, 44f, -41f, -19f);          // F service yard interior
        AddFree(44f, 51f, -40f, -36f);          // main gate passage through the 7 m east wall (4 m)
        AddFree(44f, 51f, -23.1f, -20.9f);      // service door passage (2.2 m)

        // Ground slab = the exact wall footprint (x -46..51, z -42..46) so it never pokes out of the open-region Terrain (which is carved below it).
        SandboxBuilder.Box("Ground", new Vector3(2.5f, -.5f, 2f), new Vector3(97f, 1f, 88f), mats.asphalt, 3f);

        var mapRoot = new GameObject("Map").transform;
        BuildSolids(mats, mapRoot);

        // A: ceiling, table with pistol and radio, notice board.
        SandboxBuilder.Box("A_Roof", new Vector3(-30f, 3.35f, -35f), new Vector3(8f, 0.3f, 10f), mats.wall, 3f, mapRoot);
        SandboxBuilder.Box("A_Table", new Vector3(-32.9f, 0.4f, -37f), new Vector3(1f, 0.8f, 2.4f), mats.metalPlate, 0.6f, mapRoot);
        Visual("A_Pistol", new Vector3(-32.9f, 0.82f, -36.3f), new Vector3(0.04f, 0.04f, 0.18f), mats.gunFlat, mapRoot);
        SandboxBuilder.Box("A_Radio", new Vector3(-32.9f, 0.925f, -38f), new Vector3(0.35f, 0.25f, 0.2f), mats.metalFlat, 0.5f, mapRoot);
        Visual("A_NoticeBoard", new Vector3(-33.975f, 1.6f, -32.5f), new Vector3(0.05f, 0.6f, 0.8f), mats.accent, mapRoot);

        // B: van crossing the street at 25 degrees, low barrier, warm direction lamp over the depot entrance.
        SandboxBuilder.Box("Minivan", new Vector3(-6f, 1.2f, -5f), new Vector3(2.2f, 2.4f, 5.5f), mats.rustyMetal, 2f, mapRoot, 25f);
        SandboxBuilder.Box("Low_Barrier", new Vector3(-2f, 0.45f, -20f), new Vector3(3f, 0.9f, 0.6f), mats.paintedConcrete, 1.5f, mapRoot);
        Block(-3.5f, -0.5f, -20.3f, -19.7f);

        // C: green sign box over the connector, outside face of the pharmacy.
        Visual("Pharmacy_Sign", new Vector3(-11.4f, 3.0f, -2f), new Vector3(0.2f, 0.9f, 3.2f), SandboxBuilder.Mat("IlkIkmal_Green", new Color(.12f, .62f, .24f), .25f), mapRoot);

        // D: two shelf rows (x -3 and 7, z 25..35, 0.8 deep, 2.4 high), back bench, ammo platform.
        SandboxBuilder.Box("Shelf_1", new Vector3(-3f, 1.2f, 30f), new Vector3(0.8f, 2.4f, 10f), mats.rustyMetal, 2f, mapRoot);
        Block(-3.4f, -2.6f, 25f, 35f);
        SandboxBuilder.Box("Shelf_2", new Vector3(7f, 1.2f, 30f), new Vector3(0.8f, 2.4f, 10f), mats.rustyMetal, 2f, mapRoot);
        Block(6.6f, 7.4f, 25f, 35f);
        SandboxBuilder.Box("Depot_Bench", new Vector3(2f, 0.45f, 37.5f), new Vector3(4f, 0.9f, 1.2f), mats.metalPlate, 0.8f, mapRoot);
        SandboxBuilder.Box("Ammo_Platform", new Vector3(12f, 0.3f, 23f), new Vector3(1f, 0.6f, 1f), mats.rustyMetal, 1f, mapRoot);

        // E: pipe boxes along the walls (above head height, visual only), crates in the widenings.
        Visual("Pipe_Corridor", new Vector3(25f, 2.6f, 34.6f), new Vector3(10f, 0.3f, 0.3f), mats.rustyMetal, mapRoot);
        Visual("Pipe_Arm_1", new Vector3(32.4f, 2.6f, 26f), new Vector3(0.3f, 0.3f, 8f), mats.rustyMetal, mapRoot);
        Visual("Pipe_Arm_2", new Vector3(35.6f, 2.6f, 8f), new Vector3(0.3f, 0.3f, 8f), mats.rustyMetal, mapRoot);
        Visual("Pipe_Arm_3", new Vector3(32.4f, 2.6f, -10f), new Vector3(0.3f, 0.3f, 8f), mats.rustyMetal, mapRoot);
        SandboxBuilder.Box("Crate_E1", new Vector3(36.4f, 0.6f, 13.2f), new Vector3(1.6f, 1.2f, 1.6f), mats.rustyMetal, 1f, mapRoot);
        SandboxBuilder.Box("Crate_E2", new Vector3(31.4f, 0.6f, -1.8f), new Vector3(1.6f, 1.2f, 1.6f), mats.rustyMetal, 1f, mapRoot);

        // F: two obstacle boxes that make two lanes.
        SandboxBuilder.Box("F_Obstacle_1", new Vector3(32f, 1.2f, -26.5f), new Vector3(6f, 2.4f, 3f), mats.rustyMetal, 2f, mapRoot);
        Block(29f, 35f, -28f, -25f);
        SandboxBuilder.Box("F_Obstacle_2", new Vector3(39f, 1.2f, -34f), new Vector3(4f, 2.4f, 4f), mats.rustyMetal, 2f, mapRoot);
        Block(37f, 41f, -36f, -32f);
    }

    static void Block(float x0, float x1, float z0, float z1) { Solid.Add(new Rc(x0, x1, z0, z1)); }

    // Solid boxes for everything that is not walkable: a slab decomposition of the complement of `Free` inside the outer bounds.
    static void BuildSolids(SandboxBuilder.MaterialSet mats, Transform parent)
    {
        var bounds = new Rc(-46f, 51f, -42f, 46f);
        var xs = new SortedSet<float> { bounds.x0, bounds.x1 };
        var zs = new SortedSet<float> { bounds.z0, bounds.z1 };
        foreach (Rc f in Free) { xs.Add(f.x0); xs.Add(f.x1); zs.Add(f.z0); zs.Add(f.z1); }
        var X = new List<float>(xs);
        var Z = new List<float>(zs);

        var done = new List<Rc>();
        var open = new List<Rc>();
        for (int i = 0; i < X.Count - 1; i++)
        {
            float cx = (X[i] + X[i + 1]) * 0.5f;
            var runs = new List<Rc>();
            int j = 0;
            while (j < Z.Count - 1)
            {
                if (IsFree(cx, (Z[j] + Z[j + 1]) * 0.5f)) { j++; continue; }
                int k = j;
                while (k < Z.Count - 1 && !IsFree(cx, (Z[k] + Z[k + 1]) * 0.5f)) k++;
                runs.Add(new Rc(X[i], X[i + 1], Z[j], Z[k]));
                j = k;
            }
            var nextOpen = new List<Rc>();
            foreach (Rc r in runs)
            {
                int idx = open.FindIndex(o => o.z0 == r.z0 && o.z1 == r.z1 && o.x1 == r.x0);
                if (idx >= 0) { Rc merged = open[idx]; merged.x1 = r.x1; nextOpen.Add(merged); open.RemoveAt(idx); }
                else nextOpen.Add(r);
            }
            done.AddRange(open);
            open = nextOpen;
        }
        done.AddRange(open);

        int n = 0;
        foreach (Rc r in done)
        {
            n++;
            Material m = r.Area > 60f ? mats.brick : mats.wall;
            // Walls reach 2 m below the floor so they sit in the Terrain without a visible gap (tops stay at 4.5 m).
            var size = new Vector3(r.x1 - r.x0, WallHeight - WallBase, r.z1 - r.z0);
            SandboxBuilder.Box("Wall_" + n, new Vector3((r.x0 + r.x1) * 0.5f, (WallHeight + WallBase) * 0.5f, (r.z0 + r.z1) * 0.5f), size, m, m == mats.brick ? 2f : 3f, parent);
            Solid.Add(r);
        }
        SandboxBuilder.Report.Add("map: " + Free.Count + " walkable rectangles, " + n + " solid blocks");
    }

    static bool IsFree(float x, float z)
    {
        foreach (Rc f in Free) if (f.Contains(x, z)) return true;
        return false;
    }

    internal static GameObject Visual(string name, Vector3 position, Vector3 size, Material material, Transform parent)
    {
        var o = SandboxBuilder.Box(name, position, size, material, 1f, parent);
        Object.DestroyImmediate(o.GetComponent<BoxCollider>());
        o.layer = 0;
        return o;
    }

    // ---------- director, interactables, zombies ----------

    static ZombieDirector BuildDirector(Sandbox game, GameObject[] zombiePrefabs)
    {
        var director = new GameObject("ZombieDirector").AddComponent<ZombieDirector>();
        director.player = game;
        director.zombiePrefab = zombiePrefabs[0];
        director.zombiePrefabs = zombiePrefabs;
        director.navCenter = MapCenter;
        director.navSize = new Vector3(100f, 10f, 95f);
        return director;
    }

    internal static Material Emissive(string name, Color baseColor, Color emission)
    {
        Material m = SandboxBuilder.Mat(name, baseColor, .2f);
        m.EnableKeyword("_EMISSION"); // keeps the variant in player builds; the colour is changed at run time
        m.SetColor("_EmissionColor", emission);
        EditorUtility.SetDirty(m);
        return m;
    }

    internal static Light MakeLight(string name, Vector3 position, Color color, float intensity, float range, bool enabled)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.position = position;
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        light.enabled = enabled;
        return light;
    }

    internal static BoxCollider AddTrigger(Transform parent, string name, Vector3 localPosition, Vector3 size)
    {
        var go = new GameObject(name);
        go.layer = Layers.Interactable;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var c = go.AddComponent<BoxCollider>();
        c.isTrigger = true;
        c.size = size;
        return c;
    }

    internal static Transform AddAnchor(Transform parent, Vector3 localPosition)
    {
        var a = new GameObject("HintAnchor").transform;
        a.SetParent(parent, false);
        a.localPosition = localPosition;
        return a;
    }

    // Hinged door in a 0.4 m wall: the gap runs along Z, the hinge sits at the south jamb, the leaf swings east by 90 degrees.
    internal static DoorInteractable BuildDoor(string name, float cx, float cz, float gap, float wallThickness, Material leafMaterial, Material wallMaterial, bool insideOnly)
    {
        var root = new GameObject(name).transform;
        root.position = new Vector3(cx, 0f, cz);
        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(root, false);
        hinge.localPosition = new Vector3(0f, 0f, -gap * 0.5f);
        hinge.gameObject.AddComponent<NavIgnore>();
        var leaf = SandboxBuilder.Box("Leaf", new Vector3(0f, DoorHeight * 0.5f, gap * 0.5f), new Vector3(0.1f, DoorHeight, gap - 0.04f), leafMaterial, 1f, hinge);
        AddTrigger(root, "Trigger", new Vector3(0f, DoorHeight * 0.5f, 0f), new Vector3(1f, DoorHeight, gap));
        SandboxBuilder.Box("Lintel", new Vector3(0f, DoorHeight + (WallHeight - DoorHeight) * 0.5f, 0f), new Vector3(wallThickness, WallHeight - DoorHeight, gap + 0.2f), wallMaterial, 3f, root);

        var block = new GameObject("NavBlock").transform;
        block.SetParent(root, false);
        var obstacle = block.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0f, DoorHeight * 0.5f, 0f);
        obstacle.size = new Vector3(wallThickness + 0.3f, DoorHeight, gap + 0.2f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;

        var door = root.gameObject.AddComponent<DoorInteractable>();
        door.hinge = hinge;
        door.leafCollider = leaf.GetComponent<Collider>();
        door.obstacle = obstacle;
        door.hintAnchor = AddAnchor(root, new Vector3(0f, 1.3f, 0f));
        door.insideOnly = insideOnly;
        door.insideMaxX = 14f;
        return door;
    }

    // Depot-style ammunition box: olive body, brass rounds on top until emptied. `position` is the floor point under the box.
    internal static AmmoCrate BuildAmmoCrate(Transform parent, string name, Vector3 position, float yaw)
    {
        Material olive = SandboxBuilder.Mat("IlkIkmal_Olive", new Color(.22f, .28f, .14f), .2f);
        Material oliveEmpty = SandboxBuilder.Mat("IlkIkmal_OliveEmpty", new Color(.07f, .09f, .06f), .1f);
        Material brass = SandboxBuilder.Mat("IlkIkmal_Brass", new Color(.8f, .62f, .2f), .5f);
        var crate = new GameObject(name).transform;
        crate.SetParent(parent, false);
        crate.position = position;
        crate.rotation = Quaternion.Euler(0f, yaw, 0f);
        var crateBody = SandboxBuilder.Box("Body", new Vector3(0f, 0.15f, 0f), new Vector3(0.6f, 0.3f, 0.4f), olive, 0.5f, crate);
        var rounds = Visual("Rounds", new Vector3(0f, 0.32f, 0f), new Vector3(0.5f, 0.04f, 0.3f), brass, crate);
        var crateTrigger = AddTrigger(crate, "Trigger", new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.7f, 0.8f));
        var ammo = crate.gameObject.AddComponent<AmmoCrate>();
        ammo.roundsVisual = rounds;
        ammo.body = crateBody.GetComponent<Renderer>();
        ammo.emptyMaterial = oliveEmpty;
        ammo.trigger = crateTrigger;
        ammo.hintAnchor = AddAnchor(crate, new Vector3(0f, 0.3f, 0f));
        return ammo;
    }

    static void BuildInteractables(SandboxBuilder.MaterialSet mats, Sandbox game)
    {
        Material green = SandboxBuilder.Mat("IlkIkmal_Green", new Color(.12f, .62f, .24f), .25f);
        Material yellow = SandboxBuilder.Mat("IlkIkmal_Yellow", new Color(.92f, .72f, .08f), .3f);
        Material red = SandboxBuilder.Mat("IlkIkmal_Red", new Color(.85f, .08f, .06f), .3f);
        Material lampWarm = Emissive("IlkIkmal_LampWarm", new Color(.9f, .6f, .25f), new Color(2.2f, 1.3f, .45f));
        Material radioLampMat = Emissive("IlkIkmal_RadioLamp", new Color(.25f, .12f, .05f), Color.black);
        Material panelLampMat = Emissive("IlkIkmal_PanelLamp", new Color(.2f, .3f, .15f), Color.black);

        var root = new GameObject("Interactables").transform;

        // --- doors ---
        DoorInteractable doorA = BuildDoor("Door_A", -25.8f, -34f, 2f, 0.4f, green, mats.wall, false);
        DoorInteractable doorD = BuildDoor("Door_D", 14.2f, 33f, 2f, 0.4f, mats.rustyMetal, mats.wall, true);
        doorA.transform.SetParent(root, true);
        doorD.transform.SetParent(root, true);
        // Yard exits through the 7 m east wall: faded green main gate (4 m) and the 2.2 m service door, both locked until the power is back.
        Material gateGreen = SandboxBuilder.Mat("OW_GateGreen", new Color(.22f, .33f, .25f), .25f);
        DoorInteractable gate = BuildDoor("Gate_Outer", 44.2f, -38f, 4f, 0.4f, gateGreen, mats.wall, false);
        DoorInteractable service = BuildDoor("Door_Service", 44.2f, -22f, 2.2f, 0.4f, mats.rustyMetal, mats.wall, false);
        gate.locked = true; gate.lockedLabel = "Elektrik gerekli";
        service.locked = true; service.lockedLabel = "Elektrik gerekli";
        gate.transform.SetParent(root, true);
        service.transform.SetParent(root, true);

        // --- fuse on the depot bench (red) ---
        var fuse = new GameObject("Fuse").transform;
        fuse.SetParent(root, false);
        fuse.position = new Vector3(2f, 0.9f, 37.5f);
        var fuseBody = SandboxBuilder.Box("Body", new Vector3(0f, 0.06f, 0f), new Vector3(0.1f, 0.1f, 0.28f), red, 0.5f, fuse);
        var fuseTrigger = AddTrigger(fuse, "Trigger", new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.5f, 0.6f));
        var fuseItem = fuse.gameObject.AddComponent<FuseItem>();
        fuseItem.visual = fuseBody;
        fuseItem.trigger = fuseTrigger;
        fuseItem.hintAnchor = AddAnchor(fuse, new Vector3(0f, 0.15f, 0f));

        // --- ammo box (olive, brass rounds on top while full) ---
        BuildAmmoCrate(root, "AmmoCrate", new Vector3(12f, 0.6f, 23f), 0f);

        // --- yellow panel on the F east wall: 1.2 wide (Z) x 1.8 tall x 0.4 deep, centre x 43.4 ---
        var panelRoot = new GameObject("FusePanel").transform;
        panelRoot.SetParent(root, false);
        panelRoot.position = new Vector3(43.4f, 0f, -30f);
        SandboxBuilder.Box("Body", new Vector3(0f, 1.0f, 0f), new Vector3(0.4f, 1.8f, 1.2f), yellow, 0.8f, panelRoot);
        Visual("FuseSlot", new Vector3(-0.21f, 1.2f, 0f), new Vector3(0.04f, 0.25f, 0.4f), mats.metalFlat, panelRoot);
        var inSlot = Visual("FuseInSlot", new Vector3(-0.235f, 1.2f, 0f), new Vector3(0.05f, 0.1f, 0.28f), red, panelRoot);
        inSlot.SetActive(false);
        AddTrigger(panelRoot, "Trigger", new Vector3(-0.3f, 1.0f, 0f), new Vector3(1f, 2f, 1.6f));
        var panel = panelRoot.gameObject.AddComponent<FusePanel>();
        panel.fuseInSlot = inSlot;
        panel.hintAnchor = AddAnchor(panelRoot, new Vector3(-0.25f, 1.3f, 0f));
        var panelLampMesh = Visual("Panel_LampMesh", new Vector3(0f, 2.35f, 0f), new Vector3(0.25f, 0.12f, 0.25f), panelLampMat, panelRoot);

        // --- lights: only these five (A emergency, A main, radio lamp is emissive only, depot lamp, bench lamp, panel lamp) ---
        var lights = new GameObject("Lights").transform;
        Light depotLamp = MakeLight("Depot_Lamp", new Vector3(-6f, 3.1f, 20.1f), new Color(1f, .72f, .4f), 6f, 10f, true);
        Visual("Depot_LampMesh", new Vector3(-6f, 3.0f, 20.45f), new Vector3(0.45f, 0.25f, 0.25f), lampWarm, root);
        Light benchLamp = MakeLight("Bench_Lamp", new Vector3(2f, 2.5f, 38f), new Color(1f, .72f, .4f), 5f, 8f, true);
        Visual("Bench_LampMesh", new Vector3(2f, 2.7f, 38.5f), new Vector3(0.4f, 0.2f, 0.4f), lampWarm, root);
        Visual("Bench_LampBracket", new Vector3(2f, 2.7f, 38.8f), new Vector3(0.08f, 0.08f, 0.4f), mats.metalFlat, root);
        Light emergency = MakeLight("A_Emergency", new Vector3(-30f, 3.0f, -35f), new Color(.5f, .68f, 1f), 1.6f, 7f, true);
        Light main = MakeLight("A_Main", new Vector3(-30f, 3.0f, -35.5f), new Color(1f, .82f, .55f), 5f, 12f, false);
        Light panelLamp = MakeLight("Panel_Lamp", new Vector3(43.0f, 2.7f, -30f), new Color(.9f, 1f, .7f), 3.5f, 7f, false);
        foreach (Light l in new[] { depotLamp, benchLamp, emergency, main, panelLamp }) l.transform.SetParent(lights, true);
        var radioLamp = Visual("Radio_Lamp", new Vector3(-32.7f, 0.97f, -38f), new Vector3(0.05f, 0.08f, 0.08f), radioLampMat, root);

        // --- A interior volume for the Complete condition (not on the interactable layer; ignored by every ray) ---
        var trigger = new GameObject("A_Interior_Trigger");
        trigger.layer = 2; // Ignore Raycast
        trigger.transform.position = new Vector3(-30f, 1f, -35f);
        var triggerBox = trigger.AddComponent<BoxCollider>();
        triggerBox.isTrigger = true;
        triggerBox.size = new Vector3(7.6f, 2f, 9.6f);

        var questObject = new GameObject("QuestFuse");
        var quest = questObject.AddComponent<QuestFuse>();
        quest.player = game;
        quest.safeRoomTrigger = triggerBox;
        quest.panelLamp = panelLamp;
        quest.mainLight = main;
        quest.emergencyLight = emergency;
        quest.radioLamp = radioLamp.GetComponent<Renderer>();
        quest.panelLampMesh = panelLampMesh.GetComponent<Renderer>();
        quest.pharmacyBounds = new Bounds(new Vector3(-22f, 1.5f, -2f), new Vector3(8f, 3f, 12f)); // C interior x -26..-18, z -8..4

        // Open region wiring: gates, yard bench / storage / lamps, loot, extra ammo boxes, stationBounds / yardBounds / markers, WorldState.
        OpenWorldBuilder.BuildGameplay(mats, game, root, quest, gate, service);

        var hintObject = new GameObject("HintDirector");
        var hints = hintObject.AddComponent<HintDirector>();
        hints.player = game;
        hints.quest = quest;

        BuildClearances();
    }

    // ---------- clearances ----------

    // Distance along an axis from (x, z) to the next solid rectangle, in both directions, summed (the open width at that point).
    static float Measure(float x, float z, bool acrossX)
    {
        float lo = 60f, hi = 60f;
        foreach (Rc r in Solid)
        {
            if (acrossX)
            {
                if (z <= r.z0 || z >= r.z1) continue;
                if (r.x0 >= x) hi = Mathf.Min(hi, r.x0 - x);
                if (r.x1 <= x) lo = Mathf.Min(lo, x - r.x1);
            }
            else
            {
                if (x <= r.x0 || x >= r.x1) continue;
                if (r.z0 >= z) hi = Mathf.Min(hi, r.z0 - z);
                if (r.z1 <= z) lo = Mathf.Min(lo, z - r.z1);
            }
        }
        return lo + hi;
    }

    static void Clearance(Transform parent, string label, float x, float z, bool acrossX, float minimum)
    {
        float width = Measure(x, z, acrossX);
        var go = new GameObject("Clearance_" + label);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, 0f, z);
        var marker = go.AddComponent<ClearanceMarker>();
        marker.label = label;
        marker.acrossX = acrossX;
        marker.minimum = minimum;
        marker.builderValue = width;
        string text = "clearance " + label + " = " + width.ToString("F2", CultureInfo.InvariantCulture) + " m (min " + minimum.ToString("F1", CultureInfo.InvariantCulture) + ") " + (width + 0.001f >= minimum ? "OK" : "TOO NARROW");
        SandboxBuilder.Report.Add(text);
        if (width + 0.001f < minimum) Debug.LogError("ILKIKMAL_BUILD_NOTE: " + text);
    }

    static void BuildClearances()
    {
        var parent = new GameObject("Clearances").transform;
        Clearance(parent, "A_door", -25.8f, -34f, false, 2f);
        Clearance(parent, "Depot_entrance", -6f, 20.8f, true, 2f);
        Clearance(parent, "Depot_side_door", 14.2f, 33f, false, 2f);
        Clearance(parent, "Pharmacy_connector", -15f, -2f, false, 2f);
        Clearance(parent, "E_corridor", 25f, 33f, false, 2f);
        Clearance(parent, "E_arm", 34f, 24f, true, 2f);
        Clearance(parent, "F_entrance", 34f, -18.5f, true, 2f);
        Clearance(parent, "Lower_road", 10f, -36f, false, 2f);
        Clearance(parent, "Main_street", -6f, 15f, true, 2f);
        Clearance(parent, "Shelf_aisle_west", -6.7f, 30f, true, 2.4f);
        Clearance(parent, "Shelf_aisle_middle", 2f, 30f, true, 2.4f);
        Clearance(parent, "Shelf_aisle_east", 10.7f, 30f, true, 2.4f);
        Clearance(parent, "F_lane_west", 26.5f, -26.5f, true, 2f);

        // Van at 25 degrees: the passages on both sides are measured at the van's corners, not at the middle.
        float yaw = 25f * Mathf.Deg2Rad;
        float halfX = 0.5f * (2.2f * Mathf.Cos(yaw) + 5.5f * Mathf.Sin(yaw));
        float west = (-6f - halfX) - (-11.5f), east = -0.5f - (-6f + halfX);
        SandboxBuilder.Report.Add("clearance Minivan side passages = " + west.ToString("F2", CultureInfo.InvariantCulture) + " m west / " + east.ToString("F2", CultureInfo.InvariantCulture)
            + " m east (spec: at least 3.0) " + (Mathf.Min(west, east) >= 3f ? "OK" : "TOO NARROW"));
    }

    static void ReportLights()
    {
        int directional = 0, point = 0, on = 0, onAfterEvent = 0;
        foreach (Light l in Object.FindObjectsByType<Light>())
        {
            if (l.type == LightType.Directional) directional++; else point++;
            if (l.enabled) on++;
        }
        onAfterEvent = on - 1 /* A_Emergency */ + 2 /* A_Main, Panel_Lamp */ + 3 /* yard lamps x2 + bench lamp (open region) */;
        SandboxBuilder.Report.Add("real-time lights in the scene: " + directional + " directional + " + point + " point; enabled at start: " + on
            + " (sun + " + (on - directional) + " point), after the fuse event: " + onAfterEvent + " (sun + " + (onAfterEvent - directional) + " point); "
            + "muzzle flash lights (4, pooled, only the active ones count) are created at run time by EffectPool");
    }

    // ---------- zombies ----------

    struct ZombieSpot
    {
        public string group;
        public float x, z, wander;
        public ZombieSpot(string group, float x, float z, float wander) { this.group = group; this.x = x; this.z = z; this.wander = wander; }
    }

    // Fixed placement from the spec table (no randomness except a fixed-seed facing). 8 on the route plus 1 optional in the pharmacy.
    // S1 street, S2 depot, S3 return leg (widenings of the E arm and the yard), C pharmacy.
    static readonly ZombieSpot[] ZombieSpots =
    {
        new ZombieSpot("S1", -4f, -14f, 3f), new ZombieSpot("S1", -8f, -10f, 3f),
        new ZombieSpot("S2", 2f, 29f, 3f), new ZombieSpot("S2", -6f, 35f, 3f), new ZombieSpot("S2", 24f, 33f, 3f),
        new ZombieSpot("S3", 33f, 1f, 2f), new ZombieSpot("S3", 35f, 2.5f, 2f), new ZombieSpot("S3", 38f, -26f, 2f),
        new ZombieSpot("C", -23f, 1f, 2f)
    };

    // Same prefab pipeline as SandboxBuilder.BuildDirectorAndZombies: ZombieFactory prefabs, brain.player wired through a SerializedObject.
    // They are not alerted: ZombieDirector.Start activates them in Wander/Idle, and Alert() is only used by the perf spawn path.
    // The 9 old-map zombies keep their table positions; the open region adds 15 more (OpenWorldBuilder.ZombieSpots), all sampled on the baked NavMesh.
    static void BuildZombies(Sandbox game, GameObject[] zombiePrefabs)
    {
        var director = Object.FindAnyObjectByType<ZombieDirector>();
        Physics.SyncTransforms();
        bool nav = director != null && director.BuildNavMesh();
        if (!nav) SandboxBuilder.Report.Add("WARNING zombie placement: the NavMesh could not be built in the editor, positions were not sampled");
        var root = new GameObject("Zombies").transform;
        var random = new System.Random(60601);
        var counters = new Dictionary<string, int>();

        var spots = new List<OpenWorldBuilder.ZombieSpotW>();
        foreach (ZombieSpot old in ZombieSpots) spots.Add(new OpenWorldBuilder.ZombieSpotW(old.group, new Vector3(old.x, 0f, old.z), old.wander));
        int oldCount = spots.Count;
        spots.AddRange(OpenWorldBuilder.ZombieSpots());

        for (int i = 0; i < spots.Count; i++)
        {
            OpenWorldBuilder.ZombieSpotW s = spots[i];
            Vector3 position = s.position;
            string note = "on the NavMesh";
            if (nav)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(position, out hit, 2f, NavMesh.AllAreas))
                {
                    Vector3 delta = hit.position - position;
                    delta.y = 0f;
                    float moved = delta.magnitude;
                    // Old map: floor is y = 0. Open region: keep the sampled height.
                    float y = i < oldCount ? 0f : hit.position.y;
                    if (moved > 0.25f) { position = new Vector3(hit.position.x, y, hit.position.z); note = "MOVED " + moved.ToString("F2", CultureInfo.InvariantCulture) + " m to stay on the NavMesh"; }
                    else
                    {
                        position = new Vector3(position.x, i < oldCount ? 0f : hit.position.y, position.z);
                        note = "on the NavMesh (offset " + moved.ToString("F2", CultureInfo.InvariantCulture) + " m)";
                    }
                }
                else note = "NOT ON THE NAVMESH within 2 m";
            }
            int n; counters.TryGetValue(s.group, out n); counters[s.group] = ++n;
            var z = (GameObject)PrefabUtility.InstantiatePrefab(zombiePrefabs[i % zombiePrefabs.Length]);
            z.name = "Zombie_" + s.group + "_" + n;
            z.transform.SetParent(root, false);
            z.transform.position = position;
            z.transform.rotation = Quaternion.Euler(0f, (float)(random.NextDouble() * 360.0), 0f);
            var so = new SerializedObject(z.GetComponent<ZombieBrain>());
            so.FindProperty("player").objectReferenceValue = game;
            so.FindProperty("wanderRadius").floatValue = s.wander;
            so.ApplyModifiedPropertiesWithoutUndo();
            string text = "zombie " + z.name + " at (" + position.x.ToString("F1", CultureInfo.InvariantCulture) + ", " + position.y.ToString("F1", CultureInfo.InvariantCulture) + ", " + position.z.ToString("F1", CultureInfo.InvariantCulture)
                + ") wanderRadius " + s.wander.ToString("F0", CultureInfo.InvariantCulture) + ": " + note;
            SandboxBuilder.Report.Add(text);
            if (note.StartsWith("NOT") || note.StartsWith("MOVED")) Debug.LogWarning("ILKIKMAL_BUILD_NOTE: " + text);
        }
        if (director != null) director.ReleaseNavMesh();
        SandboxBuilder.Report.Add("zombies placed: " + spots.Count + " (" + oldCount + " old map: 8 on the route + 1 optional; " + (spots.Count - oldCount) + " open region), none alerted");
    }
}
