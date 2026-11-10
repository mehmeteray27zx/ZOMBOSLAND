using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Zombosland;

// Loot pickups, yard workbench / storage / lamps, quest wiring, player survival components, baked NavMesh and open-region zombie spots.
internal static partial class OpenWorldBuilder
{
    // ---------- loot plan ----------

    struct LootDef
    {
        public string id, item, verb, look;
        public int amount;
        public float rx, rz;
        public bool hasY;   // shelf items: absolute world y instead of snapping to the surface
        public float fixedY;
    }

    static readonly List<LootDef> lootDefs = new List<LootDef>();

    static string VerbFor(string item)
    {
        switch (item)
        {
            case "cloth": return "Kumaşı al";
            case "scrap": return "Hurdayı al";
            case "wood": return "Odunu al";
            case "tape": return "Bandı al";
            case "supply_cache": return "Erzak çantasını al";
            default: return "Taşı al";
        }
    }

    static void AddLoot(string id, string item, int amount, string look, Vector2 r, float worldY = float.NaN)
    {
        lootDefs.Add(new LootDef { id = id, item = item, amount = amount, look = look, verb = VerbFor(item), rx = r.x, rz = r.y, hasY = !float.IsNaN(worldY), fixedY = float.IsNaN(worldY) ? 0f : worldY });
    }

    static Vector2 RoadSide(float s, float lateral) { return road.PointAt(s) + RightOf(road.Dir(Mathf.Min(road.IndexAt(s), road.Count - 1))) * lateral; }

    static Vector2 PathSide(int index, float lateral) { return path.p[index] + RightOf(path.Dir(index)) * lateral; }

    static int HouseDoorSide(Frame f)
    {
        Vector2 toTower = TowerPoi - f.o;
        float dx = Vector2.Dot(toTower, f.Right), dz = Vector2.Dot(toTower, f.Fwd);
        return Mathf.Abs(dx) > Mathf.Abs(dz) ? (dx > 0 ? 0 : 1) : (dz > 0 ? 2 : 3);
    }

    // Stable ids; positions come from the frames planned before the terrain exists (so trees and grass keep clear of them).
    static void PlanLoot()
    {
        lootDefs.Clear();
        // Road crate near R(150,78), visible from the road within the first minute.
        float sCrate = road.s[road.Nearest(new Vector2(150f, 78f))];
        AddLoot("loot_road_scrap_1", "scrap", 2, "crate", RoadSide(sCrate, 5.2f));

        // Station: 3 cloth piles, 2 tape, 3 scrap.
        AddLoot("loot_station_cloth_1", "cloth", 2, "cloth", station.ToR(22.0f, 2.9f));
        AddLoot("loot_station_cloth_2", "cloth", 2, "cloth", station.ToR(17.5f, -6.5f));
        AddLoot("loot_station_cloth_3", "cloth", 2, "cloth", station.ToR(15.5f, 8.8f));
        AddLoot("loot_station_tape_1", "tape", 1, "tape", station.ToR(22.6f, -2.4f));
        AddLoot("loot_station_tape_2", "tape", 1, "tape", station.ToR(12.8f, 0f));
        AddLoot("loot_station_scrap_1", "scrap", 2, "scrap", station.ToR(27.4f, -2.8f));
        AddLoot("loot_station_scrap_2", "scrap", 2, "scrap", station.ToR(29.0f, -5.8f));
        AddLoot("loot_station_scrap_3", "scrap", 2, "scrap", station.ToR(-4.0f, -5.4f));
        // Lure ingredients near the road (van wreck, rear strip) and the supply bag on the shop cabinet shelf (shelf top 1.05 m above the floor).
        AddLoot("loot_van_cloth_1", "cloth", 2, "cloth", station.ToR(-4.4f, -6.5f));
        AddLoot("loot_rear_stone_1", "stone", 2, "stone", station.ToR(6f, -19f));
        AddLoot("loot_supply_cache", "supply_cache", 1, "supply_cache", station.ToR(CabinetLocal.x, CabinetLocal.y), stationLevel + CabinetShelfTop);

        // Hut: 4 wood, 2 cloth.
        AddLoot("loot_hut_wood_1", "wood", 3, "wood", hut.ToR(-1.4f, -1.9f));
        AddLoot("loot_hut_wood_2", "wood", 3, "wood", hut.ToR(-1.3f, 1.8f));
        AddLoot("loot_hut_wood_3", "wood", 3, "wood", hut.ToR(-3.3f, 1.2f));
        AddLoot("loot_hut_wood_4", "wood", 3, "wood", hut.ToR(3.6f, 2.3f));
        AddLoot("loot_hut_cloth_1", "cloth", 2, "cloth", hut.ToR(1.4f, 1.6f));
        AddLoot("loot_hut_cloth_2", "cloth", 2, "cloth", hut.ToR(-0.9f, 4.2f));

        // Road / creek: 5 stones.
        AddLoot("loot_road_stone_1", "stone", 2, "stone", RoadSide(120f, -5.2f));
        AddLoot("loot_road_stone_2", "stone", 2, "stone", RoadSide(210f, 5.2f));
        AddLoot("loot_road_stone_3", "stone", 2, "stone", RoadSide(300f, -5.2f));
        AddLoot("loot_creek_stone_1", "stone", 2, "stone", PathSide(Mathf.Max(0, fordPathIndex - 7), 2.4f));
        AddLoot("loot_creek_stone_2", "stone", 2, "stone", PathSide(Mathf.Min(path.Count - 1, fordPathIndex + 7), -2.4f));

        // Bridge: 2 scrap, 1 tape (both ends, on the shoulders).
        AddLoot("loot_bridge_scrap_1", "scrap", 2, "scrap", RoadSide(road.ctrlS[7] - 7f, 4.9f));
        AddLoot("loot_bridge_scrap_2", "scrap", 2, "scrap", RoadSide(road.ctrlS[8] + 7f, -4.9f));
        AddLoot("loot_bridge_tape_1", "tape", 1, "tape", RoadSide(road.ctrlS[7] - 12f, -4.9f));

        // Tower houses: 2 cloth, 1 tape, 2 wood.
        AddLoot("loot_tower_cloth_1", "cloth", 2, "cloth", houses[0].ToR(-1.8f, 1.4f));
        AddLoot("loot_tower_cloth_2", "cloth", 2, "cloth", houses[1].ToR(0f, 3.7f));
        AddLoot("loot_tower_tape_1", "tape", 1, "tape", houses[0].ToR(-1.8f, -1.4f));
        AddLoot("loot_tower_wood_1", "wood", 3, "wood", houses[0].ToR(1.9f, 1.5f));
        AddLoot("loot_tower_wood_2", "wood", 3, "wood", houses[2].ToR(-3.9f, 1.0f));
    }

    // ---------- helpers ----------

    // Height of the first Environment collider under a point (slabs, deck, terrain); falls back to the terrain height.
    static float SurfaceY(float rx, float rz)
    {
        float gy = GroundY(rx, rz);
        RaycastHit hit;
        if (Physics.Raycast(RW(rx, rz, gy + 2.5f), Vector3.down, out hit, 6f, 1 << Layers.Environment, QueryTriggerInteraction.Ignore)) return hit.point.y;
        return gy;
    }

    static void TrySetRef(Component c, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(c);
        var p = so.FindProperty(field);
        if (p == null) { Note("NOTE " + c.GetType().Name + " has no serialized field '" + field + "'; assign it by hand or tell the builder the real name"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- gameplay build (called at the end of IlkIkmalBuilder.BuildInteractables) ----------

    public static void BuildGameplay(SandboxBuilder.MaterialSet mats, Sandbox game, Transform interactables, QuestFuse quest, DoorInteractable gate, DoorInteractable service)
    {
        Physics.SyncTransforms();
        quest.outerGate = gate;
        quest.serviceDoor = service;
        quest.gameObject.AddComponent<WorldState>();

        BuildYard(mats, interactables, quest);
        BuildLoot(mats, interactables);
        BuildRear(mats, interactables, quest);

        // Ammo boxes: station shop and tower house.
        Vector2 rs = station.ToR(21.0f, 3.2f);
        IlkIkmalBuilder.BuildAmmoCrate(interactables, "AmmoCrate_Station", RW(rs, SurfaceY(rs.x, rs.y)), station.yaw);
        Vector2 rt = houses[0].ToR(-2.0f, -1.6f);
        IlkIkmalBuilder.BuildAmmoCrate(interactables, "AmmoCrate_Tower", RW(rt, SurfaceY(rt.x, rt.y)), houses[0].yaw);

        quest.stationBounds = new Bounds(new Vector3(stationCenterWorld.x, stationLevel + 7f, stationCenterWorld.z), new Vector3(46f, 14f, 46f));
        quest.yardBounds = new Bounds(new Vector3(34f, 2f, -30f), new Vector3(20f, 8f, 22f));
        quest.stationMarker = new Vector3(stationCenterWorld.x, stationLevel, stationCenterWorld.z);
        quest.yardMarker = new Vector3(34f, 0f, -30f);
    }

    static void BuildYard(SandboxBuilder.MaterialSet mats, Transform interactables, QuestFuse quest)
    {
        Material timber = M("OW_Timber", .30f, .21f, .13f, .08f);
        Material lampMat = IlkIkmalBuilder.Emissive("IlkIkmal_YardLamp", new Color(.9f, .6f, .25f), Color.black);

        // Workbench against the west wall of the yard (x 24 face): body with collider, plank top, vise.
        var bench = new GameObject("Workbench").transform;
        bench.SetParent(interactables, false);
        bench.position = new Vector3(24.6f, 0f, -30f);
        SandboxBuilder.Box("Bench_Body", new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 2.2f), mats.rustyMetal, 1f, bench);
        IlkIkmalBuilder.Visual("Bench_Top", new Vector3(0f, 0.94f, 0f), new Vector3(1.0f, 0.08f, 2.3f), timber, bench);
        IlkIkmalBuilder.Visual("Bench_Vise", new Vector3(0.3f, 1.08f, 0.7f), new Vector3(0.2f, 0.2f, 0.25f), mats.metalFlat, bench);
        IlkIkmalBuilder.Visual("Bench_Tools", new Vector3(-0.1f, 1.0f, -0.6f), new Vector3(0.4f, 0.05f, 0.5f), mats.metalPlate, bench);
        IlkIkmalBuilder.AddTrigger(bench, "Trigger", new Vector3(0.5f, 1.0f, 0f), new Vector3(1.8f, 1.8f, 2.8f));
        var benchAnchor = IlkIkmalBuilder.AddAnchor(bench, new Vector3(0.3f, 1.2f, 0f));
        var workbench = bench.gameObject.AddComponent<Workbench>();
        TrySetRef(workbench, "hintAnchor", benchAnchor);

        // Storage crate beside it.
        var crate = new GameObject("YardStorage").transform;
        crate.SetParent(interactables, false);
        crate.position = new Vector3(24.75f, 0f, -33.2f);
        SandboxBuilder.Box("Crate_Body", new Vector3(0f, 0.45f, 0f), new Vector3(1.0f, 0.9f, 1.3f), timber, 1f, crate);
        // Lid hangs on a hinge at its -x edge; StorageBox swings the hinge around local Z.
        var lidHinge = new GameObject("Lid_Hinge").transform;
        lidHinge.SetParent(crate, false);
        lidHinge.localPosition = new Vector3(-0.53f, 0.93f, 0f);
        IlkIkmalBuilder.Visual("Crate_Lid", new Vector3(0.53f, 0f, 0f), new Vector3(1.06f, 0.08f, 1.36f), mats.rustyMetal, lidHinge);
        IlkIkmalBuilder.Visual("Crate_Band", new Vector3(0f, 0.45f, 0f), new Vector3(1.04f, 0.1f, 1.34f), mats.rustyMetal, crate);
        IlkIkmalBuilder.AddTrigger(crate, "Trigger", new Vector3(0.4f, 0.8f, 0f), new Vector3(1.8f, 1.6f, 2.2f));
        var crateAnchor = IlkIkmalBuilder.AddAnchor(crate, new Vector3(0.2f, 1.0f, 0f));
        var storage = crate.gameObject.AddComponent<StorageBox>();
        TrySetRef(storage, "hintAnchor", crateAnchor);
        TrySetRef(storage, "lid", lidHinge);

        // Lamps (all off until the fuse event): two wall lamps, one warm bench lamp. Emissive meshes are lit by QuestFuse.
        var lights = new GameObject("YardLights").transform;
        lights.SetParent(interactables, false);
        var meshes = new List<Renderer>();
        Func<string, Vector3, Vector3, Renderer> lampMesh = (name, p, size) =>
        {
            var m = IlkIkmalBuilder.Visual(name, p, size, lampMat, lights);
            return m.GetComponent<Renderer>();
        };
        meshes.Add(lampMesh("Yard_LampMesh_West", new Vector3(24.2f, 3.1f, -36.5f), new Vector3(0.3f, 0.2f, 0.45f)));
        IlkIkmalBuilder.Visual("Yard_LampBracket_West", new Vector3(24.08f, 3.1f, -36.5f), new Vector3(0.12f, 0.1f, 0.2f), mats.metalFlat, lights);
        meshes.Add(lampMesh("Yard_LampMesh_East", new Vector3(43.8f, 3.1f, -26.5f), new Vector3(0.3f, 0.2f, 0.45f)));
        IlkIkmalBuilder.Visual("Yard_LampBracket_East", new Vector3(43.92f, 3.1f, -26.5f), new Vector3(0.12f, 0.1f, 0.2f), mats.metalFlat, lights);
        meshes.Add(lampMesh("Yard_LampMesh_Bench", new Vector3(25.5f, 2.55f, -30f), new Vector3(0.4f, 0.14f, 0.4f)));
        IlkIkmalBuilder.Visual("Yard_LampBracket_Bench", new Vector3(24.8f, 2.7f, -30f), new Vector3(0.1f, 0.08f, 0.1f), mats.metalFlat, lights);

        Light west = IlkIkmalBuilder.MakeLight("Yard_Lamp_West", new Vector3(25.0f, 3.0f, -36.5f), new Color(1f, .78f, .5f), 5f, 13f, false);
        Light east = IlkIkmalBuilder.MakeLight("Yard_Lamp_East", new Vector3(43.0f, 3.0f, -26.5f), new Color(1f, .78f, .5f), 5f, 13f, false);
        Light benchLamp = IlkIkmalBuilder.MakeLight("Yard_Bench_Lamp", new Vector3(25.5f, 2.3f, -30f), new Color(1f, .7f, .4f), 4f, 7f, false);
        west.transform.SetParent(lights, true); east.transform.SetParent(lights, true); benchLamp.transform.SetParent(lights, true);

        quest.yardLamps = new[] { west, east };
        quest.benchLamp = benchLamp;
        quest.lampMeshes = meshes.ToArray();

        BuildBuildSlots(mats, interactables, timber);
    }

    // ---------- build slots (fixed yard spots for barricade / chest kits) ----------

    static void BuildBuildSlots(SandboxBuilder.MaterialSet mats, Transform interactables, Material timber)
    {
        ItemDef barricadeKit = SurvivalAssets.LoadItem("barricade_kit"), chestKit = SurvivalAssets.LoadItem("chest_kit"), repairKit = SurvivalAssets.LoadItem("repair_kit");
        if (barricadeKit == null || chestKit == null || repairKit == null) Note("WARNING build slots: kit items missing from Assets/Data/Items");

        // Barricades stand on the yard side of the 7 m east wall (x 43.5) so the door leaves, which swing east into the passage, stay clear.
        // The carving obstacle reaches to the wall line, so a closed barricade seals the passage mouth completely.
        BuildBarricadeSlot(mats, interactables, timber, "BuildSlot_Barricade_Service", "barricade_service", -22f, 2.2f, barricadeKit, repairKit);
        BuildBarricadeSlot(mats, interactables, timber, "BuildSlot_Barricade_Gate", "barricade_gate", -38f, 4f, barricadeKit, repairKit);

        // Chest on the yard floor near the west wall (x 24 face), between the storage crate and the south wall.
        var chest = new GameObject("BuildSlot_Chest").transform;
        chest.SetParent(interactables, false);
        chest.position = new Vector3(25.2f, 0f, -35.5f);
        var cv = new GameObject("BuiltVisual").transform;
        cv.SetParent(chest, false);
        SandboxBuilder.Box("Chest_Body", new Vector3(0f, 0.4f, 0f), new Vector3(0.7f, 0.8f, 1.1f), timber, 1f, cv);
        IlkIkmalBuilder.Visual("Chest_Lid", new Vector3(0f, 0.85f, 0f), new Vector3(0.76f, 0.1f, 1.16f), mats.rustyMetal, cv);
        IlkIkmalBuilder.Visual("Chest_Band_A", new Vector3(0f, 0.4f, -0.3f), new Vector3(0.74f, 0.82f, 0.08f), mats.rustyMetal, cv);
        IlkIkmalBuilder.Visual("Chest_Band_B", new Vector3(0f, 0.4f, 0.3f), new Vector3(0.74f, 0.82f, 0.08f), mats.rustyMetal, cv);
        IlkIkmalBuilder.Visual("Chest_Lock", new Vector3(0.37f, 0.75f, 0f), new Vector3(0.04f, 0.12f, 0.1f), mats.metalFlat, cv);
        cv.gameObject.SetActive(false);
        IlkIkmalBuilder.AddTrigger(chest, "Trigger", new Vector3(0.4f, 0.7f, 0f), new Vector3(1.6f, 1.4f, 1.8f));
        var chestAnchor = IlkIkmalBuilder.AddAnchor(chest, new Vector3(0.2f, 0.9f, 0f));
        var chestSlot = chest.gameObject.AddComponent<BuildSlot>();
        ConfigureSlot(chestSlot, "chest_yard", BuildKind.Chest, chestKit, repairKit, cv.gameObject, null, chestAnchor);
    }

    static void BuildBarricadeSlot(SandboxBuilder.MaterialSet mats, Transform interactables, Material timber, string name, string slotId, float cz, float gap, ItemDef kit, ItemDef repairKit)
    {
        var root = new GameObject(name).transform;
        root.SetParent(interactables, false);
        root.position = new Vector3(43.5f, 0f, cz);

        // Planks across the passage (1.9 m tall) with two posts; inactive until built.
        var visual = new GameObject("BuiltVisual").transform;
        visual.SetParent(root, false);
        const float height = 1.9f;
        for (int i = 0; i < 5; i++)
            SandboxBuilder.Box("Plank_" + i, new Vector3((i % 2 == 0 ? 0.03f : -0.03f), 0.2f + i * 0.38f, 0f), new Vector3(0.12f, 0.34f, gap), i % 2 == 0 ? timber : mats.rustyMetal, 1f, visual);
        SandboxBuilder.Box("Post_South", new Vector3(0f, height * 0.5f, -gap * 0.5f + 0.1f), new Vector3(0.2f, height, 0.2f), timber, 1f, visual);
        SandboxBuilder.Box("Post_North", new Vector3(0f, height * 0.5f, gap * 0.5f - 0.1f), new Vector3(0.2f, height, 0.2f), timber, 1f, visual);
        visual.gameObject.SetActive(false);

        // Always-active use trigger on the north half of the span, so the south half and the centre still reach the door behind it.
        float z0 = 0.25f, z1 = gap * 0.5f + 0.3f;
        IlkIkmalBuilder.AddTrigger(root, "Trigger", new Vector3(-0.15f, 0.95f, (z0 + z1) * 0.5f), new Vector3(1.5f, height, z1 - z0));
        var anchor = IlkIkmalBuilder.AddAnchor(root, new Vector3(-0.2f, 1.2f, (z0 + z1) * 0.5f));

        // Carving obstacle (starts disabled): x 43.25..44.25, a little wider than the passage.
        var block = new GameObject("NavBlock").transform;
        block.SetParent(root, false);
        var obstacle = block.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0.25f, height * 0.5f, 0f);
        obstacle.size = new Vector3(1.0f, height, gap + 0.8f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        obstacle.enabled = false;

        var slot = root.gameObject.AddComponent<BuildSlot>();
        ConfigureSlot(slot, slotId, BuildKind.Barricade, kit, repairKit, visual.gameObject, obstacle, anchor);
    }

    static void ConfigureSlot(BuildSlot slot, string slotId, BuildKind kind, ItemDef kit, ItemDef repairKit, GameObject visual, NavMeshObstacle obstacle, Transform anchor)
    {
        var so = new SerializedObject(slot);
        so.FindProperty("slotId").stringValue = slotId;
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.FindProperty("kit").objectReferenceValue = kit;
        so.FindProperty("repairKit").objectReferenceValue = repairKit;
        so.FindProperty("builtVisual").objectReferenceValue = visual;
        so.FindProperty("obstacle").objectReferenceValue = obstacle;
        so.FindProperty("hintAnchor").objectReferenceValue = anchor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- loot pickups ----------

    static void BuildLoot(SandboxBuilder.MaterialSet mats, Transform interactables)
    {
        var root = new GameObject("Loot").transform;
        root.SetParent(interactables, false);
        Material cloth = M("OW_LootCloth", .62f, .58f, .46f, .05f), stripe = M("OW_LootStripe", .36f, .42f, .48f, .05f);
        Material scrapA = M("OW_LootScrapA", .42f, .24f, .15f, .3f), scrapB = M("OW_LootScrapB", .30f, .32f, .34f, .35f);
        Material wood = M("OW_LootWood", .42f, .30f, .17f, .05f), tape = M("OW_LootTape", .58f, .56f, .46f, .15f);
        Material stone = UrpLit("Assets/Generated/Materials/OW_RockGrey.mat", new Color(.34f, .33f, .30f), .12f), crateMat = M("OW_Timber", .30f, .21f, .13f, .08f);
        var stoneMesh = ProceduralMeshes.Save("Proc_Stone_Loot", ProceduralMeshes.Rock(0.15f, 31));
        var tapeMesh = ProceduralMeshes.Save("Proc_Tape_Roll", ProceduralMeshes.Cylinder(0.1f, 0.1f, 0.07f, 12, 0.2f));
        var rnd = new System.Random(2024);
        int index = 0;
        foreach (LootDef def in lootDefs)
        {
            ItemDef item = SurvivalAssets.LoadItem(def.item);
            float surface = def.hasY ? def.fixedY : SurfaceY(def.rx, def.rz);
            bool cache = def.look == "supply_cache";
            var go = new GameObject(def.id);
            go.transform.SetParent(root, false);
            go.transform.position = RW(def.rx, def.rz, surface);
            float randomYaw = (float)rnd.NextDouble() * 360f;
            go.transform.rotation = Quaternion.Euler(0f, cache ? station.yaw : randomYaw, 0f);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            switch (def.look)
            {
                case "cloth":
                    IlkIkmalBuilder.Visual("Cloth_A", new Vector3(0f, 0.04f, 0f), new Vector3(0.44f, 0.07f, 0.34f), cloth, visual);
                    IlkIkmalBuilder.Visual("Cloth_B", new Vector3(0.02f, 0.1f, 0.01f), new Vector3(0.36f, 0.06f, 0.28f), cloth, visual).transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
                    IlkIkmalBuilder.Visual("Cloth_Stripe", new Vector3(0.02f, 0.135f, 0.01f), new Vector3(0.3f, 0.01f, 0.05f), stripe, visual).transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
                    break;
                case "scrap":
                    IlkIkmalBuilder.Visual("Scrap_A", new Vector3(0f, 0.03f, 0f), new Vector3(0.5f, 0.05f, 0.34f), scrapA, visual).transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
                    IlkIkmalBuilder.Visual("Scrap_B", new Vector3(0.05f, 0.08f, 0.04f), new Vector3(0.34f, 0.05f, 0.46f), scrapB, visual).transform.localRotation = Quaternion.Euler(4f, -25f, 3f);
                    IlkIkmalBuilder.Visual("Scrap_Pipe", new Vector3(-0.12f, 0.14f, -0.08f), new Vector3(0.1f, 0.1f, 0.34f), scrapA, visual).transform.localRotation = Quaternion.Euler(0f, 60f, 0f);
                    break;
                case "wood":
                    for (int k = 0; k < 3; k++)
                        IlkIkmalBuilder.Visual("Plank_" + k, new Vector3(0f, 0.04f + k * 0.07f, 0f), new Vector3(0.82f, 0.06f, 0.13f), wood, visual).transform.localRotation = Quaternion.Euler(0f, k == 0 ? 0f : k == 1 ? 32f : -22f, 0f);
                    break;
                case "tape":
                    {
                        var roll = new GameObject("Tape_Roll");
                        roll.transform.SetParent(visual, false);
                        roll.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                        roll.AddComponent<MeshFilter>().sharedMesh = tapeMesh;
                        roll.AddComponent<MeshRenderer>().sharedMaterial = tape;
                        IlkIkmalBuilder.Visual("Tape_Tail", new Vector3(0.1f, 0.075f, 0.03f), new Vector3(0.08f, 0.01f, 0.05f), tape, visual);
                        break;
                    }
                case "stone":
                    for (int k = 0; k < 2; k++)
                    {
                        var s = new GameObject("Stone_" + k);
                        s.transform.SetParent(visual, false);
                        s.transform.localPosition = new Vector3(k == 0 ? -0.07f : 0.1f, 0.05f, k == 0 ? 0.02f : -0.05f);
                        s.transform.localRotation = Quaternion.Euler(0f, k * 70f, 0f);
                        s.transform.localScale = Vector3.one * (k == 0 ? 1f : 0.75f);
                        s.AddComponent<MeshFilter>().sharedMesh = stoneMesh;
                        s.AddComponent<MeshRenderer>().sharedMaterial = stone;
                    }
                    break;
                case "supply_cache":
                    {
                        // Olive duffel: body, lid flap, two vertical straps ("columns") and a buckle; sits on the cabinet shelf.
                        Material olive = M("OW_CacheOlive", .24f, .30f, .17f, .08f), strap = M("OW_CacheStrap", .46f, .40f, .26f, .1f);
                        IlkIkmalBuilder.Visual("Cache_Body", new Vector3(0f, 0.17f, 0f), new Vector3(0.52f, 0.32f, 0.3f), olive, visual);
                        IlkIkmalBuilder.Visual("Cache_Flap", new Vector3(0f, 0.34f, 0f), new Vector3(0.54f, 0.05f, 0.32f), olive, visual);
                        IlkIkmalBuilder.Visual("Cache_Strap_L", new Vector3(-0.14f, 0.18f, 0f), new Vector3(0.05f, 0.36f, 0.33f), strap, visual);
                        IlkIkmalBuilder.Visual("Cache_Strap_R", new Vector3(0.14f, 0.18f, 0f), new Vector3(0.05f, 0.36f, 0.33f), strap, visual);
                        IlkIkmalBuilder.Visual("Cache_Buckle", new Vector3(0f, 0.3f, -0.17f), new Vector3(0.08f, 0.06f, 0.02f), mats.metalFlat, visual);
                        break;
                    }
                default: // crate
                    IlkIkmalBuilder.Visual("Crate_Body", new Vector3(0f, 0.25f, 0f), new Vector3(0.7f, 0.5f, 0.5f), crateMat, visual);
                    IlkIkmalBuilder.Visual("Crate_Slat", new Vector3(0f, 0.25f, 0f), new Vector3(0.72f, 0.08f, 0.52f), mats.rustyMetal, visual);
                    IlkIkmalBuilder.Visual("Crate_Scrap", new Vector3(0.1f, 0.54f, 0f), new Vector3(0.4f, 0.06f, 0.3f), scrapA, visual).transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
                    break;
            }
            // The cache trigger stays inside the cabinet (0.45 m deep), so it is small.
            var trigger = IlkIkmalBuilder.AddTrigger(go.transform, "Trigger", new Vector3(0f, cache ? 0.2f : 0.3f, 0f), cache ? new Vector3(0.6f, 0.4f, 0.34f) : new Vector3(1.0f, 0.8f, 1.0f));
            var anchor = IlkIkmalBuilder.AddAnchor(go.transform, new Vector3(0f, cache ? 0.2f : 0.35f, 0f));
            var pickup = go.AddComponent<LootPickup>();
            pickup.lootId = def.id;
            pickup.verb = def.verb;
            pickup.item = item;
            pickup.amount = def.amount;
            pickup.visual = visual.gameObject;
            pickup.trigger = trigger;
            pickup.hintAnchor = anchor;
            index++;
        }
        Note("loot: " + index + " LootPickup objects with stable ids (loot_*), snapped to the Environment surface; grass and trees keep 2.8 / 4.5 m clear around them");
    }

    // ---------- player survival ----------

    public static void BuildPlayerSurvival(GameObject player)
    {
        var survival = player.AddComponent<PlayerSurvival>();
        survival.database = SurvivalAssets.EnsureAll(SandboxBuilder.Report);

        // Thrown stone prefab: small deformed stone mesh, sphere collider 0.06, rigidbody 0.4 kg.
        var stoneMat = UrpLit("Assets/Generated/Materials/OW_RockGrey.mat", new Color(.34f, .33f, .30f), .12f);
        var mesh = ProceduralMeshes.Save("Proc_ThrownStone", ProceduralMeshes.Rock(0.07f, 12));
        var stone = new GameObject("ThrownStone");
        stone.AddComponent<MeshFilter>().sharedMesh = mesh;
        stone.AddComponent<MeshRenderer>().sharedMaterial = stoneMat;
        var sphere = stone.AddComponent<SphereCollider>();
        sphere.radius = 0.06f;
        var body = stone.AddComponent<Rigidbody>();
        body.mass = 0.4f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        stone.AddComponent<ThrownStone>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(stone, "Assets/Generated/Prefabs/ThrownStone.prefab");
        UnityEngine.Object.DestroyImmediate(stone);
        survival.stonePrefab = prefab.GetComponent<ThrownStone>();

        // Melee view model (temporary placeholder): bat and a sleeve under the camera, inactive until a bat is equipped.
        var cam = player.GetComponentInChildren<Camera>();
        var view = new GameObject("MeleeView");
        view.transform.SetParent(cam.transform, false);
        view.transform.localPosition = new Vector3(0.28f, -0.32f, 0.5f);
        var batMat = M("OW_Bat", .46f, .32f, .18f, .1f);
        var gripMat = M("OW_BatGrip", .12f, .12f, .11f, .1f);
        var sleeveMat = M("OW_Sleeve", .22f, .26f, .20f, .05f);
        var skinMat = M("OW_Hand", .50f, .38f, .30f, .1f);
        var batMesh = ProceduralMeshes.Save("Proc_Bat", ProceduralMeshes.Cylinder(0.018f, 0.045f, 0.78f, 10, 0.3f));
        var bat = new GameObject("Bat");
        bat.transform.SetParent(view.transform, false);
        bat.transform.localRotation = Quaternion.Euler(68f, 0f, 0f);
        bat.AddComponent<MeshFilter>().sharedMesh = batMesh;
        bat.AddComponent<MeshRenderer>().sharedMaterial = batMat;
        IlkIkmalBuilder.Visual("Grip", new Vector3(0f, -0.01f, 0.02f), new Vector3(0.04f, 0.04f, 0.2f), gripMat, view.transform).transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
        IlkIkmalBuilder.Visual("Hand", new Vector3(0f, -0.015f, 0.03f), new Vector3(0.09f, 0.08f, 0.1f), skinMat, view.transform);
        IlkIkmalBuilder.Visual("Sleeve", new Vector3(0.02f, -0.07f, -0.17f), new Vector3(0.11f, 0.11f, 0.34f), sleeveMat, view.transform).transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
        foreach (var r in view.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        view.AddComponent<MeleeViewmodel>();
        view.SetActive(false);
        survival.meleeView = view;
        Note("player survival: PlayerSurvival wired (database, ThrownStone prefab, inactive MeleeView under the camera, temporary placeholder)");
    }

    // ---------- NavMesh bake ----------

    public static NavMeshData BakeNavMesh(ZombieDirector director)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Physics.SyncTransforms();
        var bounds = new Bounds(new Vector3(205f, 20f, 140f), new Vector3(510f, 60f, 510f)); // world x -50..460, y -10..50, z -115..395
        var sources = new List<NavMeshBuildSource>();
        var markups = new List<NavMeshBuildMarkup>();
        foreach (NavIgnore ignore in UnityEngine.Object.FindObjectsByType<NavIgnore>())
            markups.Add(new NavMeshBuildMarkup { root = ignore.transform, ignoreFromBuild = true, applyToChildren = true });
        NavMeshBuilder.CollectSources(bounds, 1 << Layers.Environment, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
        int collected = sources.Count, terrainSources = 0;
        foreach (var s in sources) if (s.shape == NavMeshBuildSourceShape.Terrain) terrainSources++;
        if (terrainSources == 0 && terrain != null)
        {
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Terrain, sourceObject = terrain.terrainData,
                transform = Matrix4x4.TRS(terrain.transform.position, Quaternion.identity, Vector3.one), area = 0
            });
            terrainSources = 1;
        }
        // Every terrain tree blocks the NavMesh (0.9 m wide not-walkable volume, area 1).
        foreach (Vector3 t in treeWorld)
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox, transform = Matrix4x4.TRS(t + Vector3.up, Quaternion.identity, Vector3.one),
                size = new Vector3(0.9f, 4f, 0.9f), area = 1
            });

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.35f;
        settings.agentHeight = 1.8f;
        settings.agentClimb = 0.35f;
        settings.agentSlope = 40f;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.125f;
        settings.overrideTileSize = true;
        settings.tileSize = 256;
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (data == null) { Note("WARNING NavMesh bake returned no data"); return null; }

        const string path = "Assets/Generated/Nav/IlkIkmal_NavMesh.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Generated/Nav")) AssetDatabase.CreateFolder("Assets/Generated", "Nav");
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(data, path);
        director.bakedNavMesh = data;
        EditorUtility.SetDirty(director);
        Note("NavMesh baked in " + F((float)watch.Elapsed.TotalSeconds) + " s: " + collected + " collected sources (" + terrainSources + " terrain) + " + treeWorld.Count + " tree modifier boxes, voxel 0.125, tile 256, saved " + path);
        return data;
    }

    // ---------- open-region zombies ----------

    public struct ZombieSpotW
    {
        public string group;
        public Vector3 position;
        public float wander;
        public ZombieSpotW(string group, Vector3 position, float wander) { this.group = group; this.position = position; this.wander = wander; }
    }

    static Vector3 SpotAt(Vector2 r) { return RW(r, SurfaceY(r.x, r.y) + 0.1f); }

    public static List<ZombieSpotW> ZombieSpots()
    {
        Physics.SyncTransforms();
        var list = new List<ZombieSpotW>();
        // Station forecourt: 4.
        // All four stand in the front half; the rear stays empty (station frame, see StationZombieLocal).
        foreach (Vector2 l in StationZombieLocal) list.Add(new ZombieSpotW("ST", SpotAt(station.ToR(l.x, l.y)), 3f));
        // Road near R(180,100): 1.
        list.Add(new ZombieSpotW("RD", SpotAt(new Vector2(180f, 100f)), 3f));
        // Hut: 2.
        list.Add(new ZombieSpotW("HU", SpotAt(hut.ToR(-4.5f, 3.5f)), 3f));
        list.Add(new ZombieSpotW("HU", SpotAt(hut.ToR(4.5f, -2.5f)), 3f));
        // Ford: 1 on the footpath just before the crossing.
        list.Add(new ZombieSpotW("FD", SpotAt(path.p[Mathf.Max(0, fordPathIndex - 5)]), 3f));
        // Bridge: 2 at the ends, 1 on the deck.
        list.Add(new ZombieSpotW("BR", RoadPoint(road.ctrlS[7] - 9f, 0f, 0.1f), 3f));
        list.Add(new ZombieSpotW("BR", RoadPoint(road.ctrlS[8] + 9f, 1.5f, 0.1f), 3f));
        list.Add(new ZombieSpotW("BR", RoadPoint(bridgeCross, -1f, 0.1f), 2f));
        // Tower: 4.
        list.Add(new ZombieSpotW("TW", SpotAt(new Vector2(383f, 392f)), 3f));
        list.Add(new ZombieSpotW("TW", SpotAt(new Vector2(397f, 386f)), 3f));
        list.Add(new ZombieSpotW("TW", SpotAt(new Vector2(388f, 402f)), 3f));
        list.Add(new ZombieSpotW("TW", SpotAt(new Vector2(402f, 398f)), 3f));
        return list;
    }

    // ---------- lights for validation / reports ----------

    public static string Summary()
    {
        return lootDefs.Count + " loot pickups, " + treeWorld.Count + " trees, road " + F(road.length) + " m, footpath " + F(path.length) + " m, creek " + F(creek.length) + " m";
    }
}
