using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombosland;

/// <summary>Integration checks for the generated Ilk Ikmal scene (editor, batch). Run: -executeMethod IlkIkmalValidation.Run</summary>
public static class IlkIkmalValidation
{
    private const string ScenePath = "Assets/Scenes/IlkIkmal.unity";

    public static void Run()
    {
        var results = new List<string>();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Physics.SyncTransforms();

        Sandbox player = null;
        QuestFuse quest = null;
        var interactables = new List<MonoBehaviour>();

        Check(results, "scene_no_missing_scripts", () =>
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var go in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go.gameObject) == 0, "Missing script on " + HierarchyPath(go));
        });

        Check(results, "single_quest_interactables_player_wiring", () =>
        {
            var quests = UnityEngine.Object.FindObjectsByType<QuestFuse>();
            Require(quests.Length == 1, "Expected exactly one QuestFuse, found " + quests.Length);
            quest = quests[0];
            player = UnityEngine.Object.FindAnyObjectByType<Sandbox>();
            Require(player != null && quest.player == player, "QuestFuse.player is not the scene player");
            Require(player.GetComponent<Interactor>() != null, "Interactor missing on the player");
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>())
                if (mb is IInteractable) interactables.Add(mb);
            // The original five (2 doors, fuse, ammo box, panel) plus the open region: 2 gates, 3 ammo boxes in total, workbench, storage, loot pickups.
            Require(interactables.Count >= 5 + 2 + 2 + 2 + 20, "Expected the original 5 interactables plus the open region ones, found " + interactables.Count);
            Require(UnityEngine.Object.FindObjectsByType<DoorInteractable>().Length == 5, "Expected 5 doors (A, D, Gate_Outer, Door_Service, Cache_Cabinet)");
            Require(UnityEngine.Object.FindObjectsByType<FuseItem>().Length == 1 && UnityEngine.Object.FindObjectsByType<AmmoCrate>().Length == 3 && UnityEngine.Object.FindObjectsByType<FusePanel>().Length == 1, "Expected one fuse, three ammo boxes (depot, station, tower), one panel");
            Require(quest.safeRoomTrigger != null && quest.safeRoomTrigger.isTrigger, "Safe room trigger missing");
            Require(quest.panelLamp != null && quest.mainLight != null && quest.emergencyLight != null && quest.radioLamp != null && quest.panelLampMesh != null, "Light event references missing");
            var so = new SerializedObject(player);
            Require(so.FindProperty("startReserve").intValue == 36, "IlkIkmal startReserve must be 36");
            Require(player.Weapon.Reserve == 36, "IlkIkmal weapon reserve must start at 36");
            Require(Vector3.Distance(player.transform.position, new Vector3(-30f, 0.05f, -36f)) < 0.001f, "Player spawn moved from (-30, 0.05, -36)");
            Require(Vector3.Dot(player.transform.forward, Vector3.right) > 0.99f, "Player does not face east");
        });

        Check(results, "interactable_triggers_on_interactable_layer_and_masks_exclude_it", () =>
        {
            Require(Layers.Interactable == 13 && LayerMask.NameToLayer("Interactable") == 13, "Layer 13 is not registered as Interactable");
            Require((Layers.ShotMask & (1 << Layers.Interactable)) == 0, "ShotMask includes the Interactable layer");
            Require((Layers.SightMask & (1 << Layers.Interactable)) == 0, "SightMask includes the Interactable layer");
            Require((Interactor.Mask & (1 << Layers.Environment)) != 0 && (Interactor.Mask & (1 << Layers.Interactable)) != 0, "Interactor mask must be Environment | Interactable");
            Require(Mathf.Approximately(Interactor.Range, 2.2f) && Mathf.Approximately(Interactor.Radius, 0.12f), "Interactor range/radius must be 2.2 / 0.12");
            foreach (var mb in interactables)
            {
                int triggers = 0, bodies = 0;
                foreach (var c in mb.GetComponentsInChildren<Collider>(true))
                {
                    if (c.isTrigger)
                    {
                        triggers++;
                        Require(c.gameObject.layer == Layers.Interactable, HierarchyPath(c.transform) + " trigger is not on the Interactable layer");
                    }
                    else
                    {
                        bodies++;
                        Require(c.gameObject.layer == Layers.Environment, HierarchyPath(c.transform) + " body is not on the Environment layer");
                    }
                }
                // The supply cabinet has no use trigger on purpose: its closed leaf (Environment) is what the interact ray hits.
                var cabinetDoor = mb as DoorInteractable;
                if (cabinetDoor == null || !cabinetDoor.cabinet) Require(triggers >= 1, mb.name + " has no trigger collider");
                if (!(mb is LootPickup)) Require(bodies >= 1, mb.name + " has no body collider"); // loot piles are walk-through visuals
                Require(((IInteractable)mb).HintAnchor != null, mb.name + " has no hint anchor");
            }
        });

        Check(results, "doors_carve_and_are_excluded_from_navmesh_bake", () =>
        {
            foreach (var door in UnityEngine.Object.FindObjectsByType<DoorInteractable>())
            {
                if (door.cabinet)
                {
                    Require(door.obstacle == null && door.hinge != null && door.hinge.GetComponent<NavIgnore>() != null && door.leafCollider != null && door.leafCollider.enabled && !door.IsOpen, door.name + " (cabinet) must have no obstacle, an ignored hinge and a closed leaf");
                    continue;
                }
                Require(door.obstacle != null && door.obstacle.carving && door.obstacle.enabled, door.name + " lacks an enabled carving NavMeshObstacle");
                Require(door.hinge != null && door.hinge.GetComponent<NavIgnore>() != null, door.name + " hinge lacks NavIgnore");
                Require(door.leafCollider != null && door.leafCollider.enabled && !door.leafCollider.isTrigger, door.name + " leaf collider missing");
                Require(!door.IsOpen, door.name + " must start closed");
            }
            var a = GameObject.Find("Door_A").GetComponent<DoorInteractable>();
            var d = GameObject.Find("Door_D").GetComponent<DoorInteractable>();
            Require(!a.insideOnly && d.insideOnly && Mathf.Approximately(d.insideMaxX, 14f), "Door A must be usable from both sides, door D only from inside (x < 14)");
        });

        Check(results, "clearances_match_spec_minimums", () =>
        {
            var markers = UnityEngine.Object.FindObjectsByType<ClearanceMarker>();
            Require(markers.Length >= 12, "Expected at least 12 clearance markers, found " + markers.Length);
            foreach (var m in markers)
            {
                float width = MeasureWidth(m.transform.position, m.acrossX);
                Require(width + 0.01f >= m.minimum, "Clearance " + m.label + " is " + width.ToString("F2") + " m, minimum " + m.minimum.ToString("F1") + " m");
                Require(Mathf.Abs(width - m.builderValue) < 0.05f, "Clearance " + m.label + " physics " + width.ToString("F2") + " m differs from builder " + m.builderValue.ToString("F2") + " m");
            }
            var van = GameObject.Find("Minivan").GetComponent<Collider>().bounds;
            Require(van.min.x - (-11.5f) >= 3f && -0.5f - van.max.x >= 3f, "Van passages are narrower than 3 m (west " + (van.min.x + 11.5f).ToString("F2") + ", east " + (-0.5f - van.max.x).ToString("F2") + ")");
            foreach (var shelf in new[] { "Shelf_1", "Shelf_2" })
            {
                var bounds = GameObject.Find(shelf).GetComponent<Collider>().bounds;
                Require(Mathf.Abs(bounds.size.x - 0.8f) < 0.01f && Mathf.Abs(bounds.size.y - 2.4f) < 0.01f, shelf + " must be 0.8 deep and 2.4 high");
                Require(bounds.min.z >= 24.99f && bounds.max.z <= 35.01f, shelf + " must span z 25..35");
            }
            Require(Mathf.Abs(GameObject.Find("Shelf_1").transform.position.x + 3f) < 0.01f && Mathf.Abs(GameObject.Find("Shelf_2").transform.position.x - 7f) < 0.01f, "Shelf rows must sit at x = -3 and x = 7");
        });

        Check(results, "key_positions_match_spec_table", () =>
        {
            var panel = GameObject.Find("FusePanel").transform;
            Require(Vector3.Distance(panel.position, new Vector3(43.4f, 0f, -30f)) < 0.01f, "Panel not at (43.4, -30)");
            var body = panel.Find("Body").GetComponent<BoxCollider>();
            Require(Vector3.Distance(body.size, new Vector3(0.4f, 1.8f, 1.2f)) < 0.01f && Mathf.Abs(body.transform.position.y - 1f) < 0.01f, "Panel body must be 1.2 x 1.8 x 0.4 with centre y = 1.0");
            Require(Vector3.Distance(GameObject.Find("AmmoCrate").transform.position, new Vector3(12f, 0.6f, 23f)) < 0.01f, "Ammo box not at (12, 23)");
            var fuse = GameObject.Find("Fuse").transform.position;
            Require(Mathf.Abs(fuse.x - 2f) < 0.01f && Mathf.Abs(fuse.z - 37.5f) < 0.01f, "Fuse not on the bench at (2, 37.5)");
            var bench = GameObject.Find("Depot_Bench").transform.position;
            Require(Mathf.Abs(bench.x - 2f) < 0.01f && Mathf.Abs(bench.z - 37.5f) < 0.01f, "Bench not at (2, 37.5)");
            var minivan = GameObject.Find("Minivan").transform;
            Require(Mathf.Abs(minivan.position.x + 6f) < 0.01f && Mathf.Abs(minivan.position.z + 5f) < 0.01f && Mathf.Abs(Mathf.DeltaAngle(minivan.eulerAngles.y, 25f)) < 0.5f, "Van must stand at (-6, -5) turned 25 degrees");
            Require(Vector3.Distance(GameObject.Find("Door_A").transform.position, new Vector3(-25.8f, 0f, -34f)) < 0.01f, "Door A not on the A east wall at z = -34");
            Require(Vector3.Distance(GameObject.Find("Door_D").transform.position, new Vector3(14.2f, 0f, 33f)) < 0.01f, "Door D not on the depot east wall at z = 33");
        });

        Check(results, "realtime_lights_and_initial_light_state", () =>
        {
            int point = 0, directional = 0;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>())
            {
                if (l.type == LightType.Directional)
                {
                    directional++;
                    Require(l.name != "Sky fill" || l.shadows == LightShadows.None, "The sky fill light must not cast shadows");
                }
                else if (l.type == LightType.Point) point++;
                else Require(false, "Unexpected light type on " + l.name);
            }
            Require(directional == 2 && GameObject.Find("Sky fill") != null, "Expected the sun plus the Sky fill light, found " + directional + " directional lights");
            Require(point >= 5 && point <= 5 + 8, "Expected the original five point lights plus at most 8 open-region lights, found " + point);
            foreach (var name in new[] { "Depot_Lamp", "Bench_Lamp", "A_Emergency", "A_Main", "Panel_Lamp" }) Require(GameObject.Find(name) != null, "Missing original light " + name);
            Require(quest.yardLamps != null && quest.yardLamps.Length == 2 && quest.benchLamp != null, "Yard lamps / bench lamp not wired to QuestFuse");
            Require(!quest.benchLamp.enabled && !quest.yardLamps[0].enabled && !quest.yardLamps[1].enabled, "Yard lamps must start off");
            Require(quest.emergencyLight.enabled && !quest.mainLight.enabled && !quest.panelLamp.enabled, "A_Emergency must be on, A_Main and Panel_Lamp off at start");
            Require(GameObject.Find("Depot_Lamp").GetComponent<Light>().enabled && GameObject.Find("Bench_Lamp").GetComponent<Light>().enabled, "Depot and bench lamps must be on at start");
            Require(QuestFuse.EmissionOf(quest.radioLamp).maxColorComponent < 0.01f, "Radio lamp must start dark");
            Debug.Log("ILKIKMAL_VALIDATION real-time lights: sun + sky fill + " + point + " point");
        });

        Check(results, "navmesh_closed_doors_cut_open_gaps_walkable", () =>
        {
            var director = UnityEngine.Object.FindAnyObjectByType<ZombieDirector>();
            Require(director != null && director.player == player, "ZombieDirector missing or not wired to the player");
            Require(director.navCenter.x > 0f && director.navSize.x >= 100f && director.navSize.z >= 95f, "ZombieDirector nav bounds do not cover the map");
            try
            {
                Require(director.BuildNavMesh(), "ZombieDirector.BuildNavMesh() failed in edit mode");
                // Closed: the carving obstacles cut both door gaps.
                Require(!PathComplete(new Vector3(-20f, 0f, -36f), new Vector3(-30f, 0f, -36f)), "closed A door must cut road -> A interior");
                float closedD = PathLength(new Vector3(12f, 0f, 33f), new Vector3(20f, 0f, 33f));
                Require(closedD > 60f, "closed D side door must force a long detour depot -> corridor, got " + closedD.ToString("F1") + " m");
                // Open: with the obstacles off the gaps themselves must be walkable (door leaves are excluded from the bake).
                foreach (var door in UnityEngine.Object.FindObjectsByType<DoorInteractable>()) if (door.obstacle != null) door.obstacle.enabled = false;
                Require(director.BuildNavMesh(), "ZombieDirector.BuildNavMesh() failed in edit mode (open doors)");
                float openD = PathLength(new Vector3(12f, 0f, 33f), new Vector3(20f, 0f, 33f));
                Require(openD > 0f && openD < 15f, "open D side door must give a short way depot -> corridor, got " + openD.ToString("F1") + " m");
                RequirePath(new Vector3(-24.5f, 0f, -34f), new Vector3(-6f, 0f, 23f), "A door -> depot entrance");
                RequirePath(new Vector3(-30f, 0f, -36f), new Vector3(-24.5f, 0f, -34f), "A interior -> A door -> road (door gap)");
                RequirePath(new Vector3(13f, 0f, 33f), new Vector3(42f, 0f, -30f), "depot side door -> F panel");
                RequirePath(new Vector3(30f, 0f, -38f), new Vector3(-30f, 0f, -36f), "F -> A");
                RequirePath(new Vector3(-6f, 0f, 15f), new Vector3(-23f, 0f, 1f), "street -> pharmacy");
                // Open region (all door obstacles are off here): yard -> station and station -> tower along the baked terrain NavMesh.
                RequirePath(new Vector3(34f, 0f, -30f), quest.stationMarker, "yard -> station");
                RequirePath(quest.stationMarker, new Vector3(346f, 26f, 272f), "station -> water tower");
            }
            finally { director.ReleaseNavMesh(); }
        });

        Check(results, "build_settings_two_scenes_in_order", () =>
        {
            var scenes = EditorBuildSettings.scenes;
            Require(scenes.Length == 2, "Expected 2 scenes in build settings, found " + scenes.Length);
            Require(scenes[0].path == ScenePath && scenes[0].enabled, "Build index 0 must be IlkIkmal");
            Require(scenes[1].path == "Assets/Scenes/CombatSandbox.unity" && scenes[1].enabled, "Build index 1 must be CombatSandbox");
            Require(PlayerSettings.bundleVersion == "0.7", "bundleVersion must be 0.7, found " + PlayerSettings.bundleVersion);
        });

        Check(results, "hint_director_single_and_wired", () =>
        {
            var hints = UnityEngine.Object.FindObjectsByType<HintDirector>();
            Require(hints.Length == 1, "Expected exactly one HintDirector, found " + hints.Length);
            Require(hints[0].player == player && hints[0].quest == quest, "HintDirector is not wired to the player and the quest");
            Require(HintDirector.AllIds.Length == 5, "HintDirector must know exactly five hints (move, aim, reload, noammo, noise)");
        });

        Check(results, "zombies_placed_not_alerted_wander_radius_and_noise_radius", () =>
        {
            var zombies = UnityEngine.Object.FindObjectsByType<ZombieBrain>();
            Require(zombies.Length == 9 + 15, "Expected 24 zombies (9 old map + 15 open region), found " + zombies.Length);
            var expected = new Dictionary<string, Vector3>
            {
                { "Zombie_S1_1", new Vector3(-4f, 0f, -14f) }, { "Zombie_S1_2", new Vector3(-8f, 0f, -10f) },
                { "Zombie_S2_1", new Vector3(2f, 0f, 29f) }, { "Zombie_S2_2", new Vector3(-6f, 0f, 35f) }, { "Zombie_S2_3", new Vector3(24f, 0f, 33f) },
                { "Zombie_S3_1", new Vector3(33f, 0f, 1f) }, { "Zombie_S3_2", new Vector3(35f, 0f, 2.5f) }, { "Zombie_S3_3", new Vector3(38f, 0f, -26f) },
                { "Zombie_C_1", new Vector3(-23f, 0f, 1f) }
            };
            var director = UnityEngine.Object.FindAnyObjectByType<ZombieDirector>();
            Require(director != null && director.BuildNavMesh(), "NavMesh could not be built to sample the zombie positions");
            try
            {
                foreach (var z in zombies)
                {
                    Vector3 want;
                    if (!expected.TryGetValue(z.name, out want))
                    {
                        // Open-region zombies (ST station, RD road, HU hut, FD ford, BR bridge, TW tower): on the baked NavMesh, unalerted.
                        Require(System.Text.RegularExpressions.Regex.IsMatch(z.name, "^Zombie_(ST|RD|HU|FD|BR|TW)_[0-9]+$"), "Unexpected zombie " + z.name);
                        NavMeshHit owHit;
                        Require(NavMesh.SamplePosition(z.transform.position, out owHit, 0.6f, NavMesh.AllAreas), z.name + " does not sample onto the NavMesh within 0.6 m");
                        Require(z.State == ZombieState.Wander && !z.CanSeePlayer && z.player == player, z.name + " must start unalerted and wired to the player");
                        continue;
                    }
                    float planar = Vector2.Distance(new Vector2(z.transform.position.x, z.transform.position.z), new Vector2(want.x, want.z));
                    Require(planar <= 0.3f, z.name + " is " + planar.ToString("F2") + " m away from the spec table position (" + want.x + ", " + want.z + ")");
                    float radius = z.name.StartsWith("Zombie_S1") || z.name.StartsWith("Zombie_S2") ? 3f : 2f;
                    Require(Mathf.Approximately(z.WanderRadius, radius), z.name + " wanderRadius " + z.WanderRadius + ", expected " + radius);
                    Require(z.State == ZombieState.Wander && !z.CanSeePlayer, z.name + " must start unalerted (Wander)");
                    Require(z.player == player, z.name + " is not wired to the player");
                    NavMeshHit hit;
                    Require(NavMesh.SamplePosition(z.transform.position, out hit, 0.5f, NavMesh.AllAreas), z.name + " does not sample onto the NavMesh within 0.5 m");
                    expected.Remove(z.name);
                }
                Require(expected.Count == 0, "Missing zombies: " + string.Join(", ", new List<string>(expected.Keys).ToArray()));
            }
            finally { director.ReleaseNavMesh(); }
            var so = new SerializedObject(player);
            Require(Mathf.Approximately(so.FindProperty("noiseRadius").floatValue, 22f), "IlkIkmal noiseRadius must be 22");
            Require(quest.pharmacyBounds.size.sqrMagnitude > 0f, "Pharmacy bounds missing on QuestFuse");
            var probe = new GameObject("wander_default_probe");
            try { Require(Mathf.Approximately(probe.AddComponent<ZombieBrain>().WanderRadius, 6f), "ZombieBrain.wanderRadius default must stay 6"); }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        });

        Check(results, "open_region_terrain_gates_loot_survival_wiring", () =>
        {
            var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
            Require(terrain != null && terrain.GetComponent<TerrainCollider>() != null, "Terrain with a TerrainCollider missing");
            Require(terrain.gameObject.layer == Layers.Environment, "Terrain must be on the Environment layer");
            Require(Vector3.Distance(terrain.transform.position, new Vector3(-46f, -8f, -110f)) < 0.01f && Vector3.Distance(terrain.terrainData.size, new Vector3(500f, 56f, 500f)) < 0.01f, "Terrain position / size differ from the spec");
            Require(terrain.terrainData.treeInstanceCount >= 1000, "Expected at least 1000 terrain trees, found " + terrain.terrainData.treeInstanceCount);
            var director = UnityEngine.Object.FindAnyObjectByType<ZombieDirector>();
            Require(director != null && director.bakedNavMesh != null, "ZombieDirector.bakedNavMesh is not assigned");
            Require(quest.outerGate != null && quest.outerGate.name == "Gate_Outer" && quest.outerGate.locked, "Gate_Outer missing, not wired or not locked");
            Require(quest.serviceDoor != null && quest.serviceDoor.name == "Door_Service" && quest.serviceDoor.locked, "Door_Service missing, not wired or not locked");
            Require(quest.stationBounds.size.y >= 13.9f && quest.yardBounds.size.sqrMagnitude > 0f, "Station / yard bounds missing");
            var loot = UnityEngine.Object.FindObjectsByType<LootPickup>();
            Require(loot.Length >= 28, "Expected about 30 loot pickups, found " + loot.Length);
            var ids = new HashSet<string>();
            foreach (var l in loot) Require(!string.IsNullOrEmpty(l.lootId) && ids.Add(l.lootId) && l.item != null && l.trigger != null, "Loot pickup " + l.name + " is not complete or has a duplicate id");
            Require(GameObject.Find("Workbench") != null && GameObject.Find("YardStorage") != null && GameObject.Find("AmmoCrate_Station") != null && GameObject.Find("AmmoCrate_Tower") != null, "Workbench / YardStorage / ammo boxes missing");
            var survival = player.GetComponent<PlayerSurvival>();
            Require(survival != null && survival.database != null && survival.stonePrefab != null && survival.meleeView != null && !survival.meleeView.activeSelf, "PlayerSurvival is not fully wired");
            Require(quest.GetComponent<WorldState>() != null, "WorldState missing on the QuestFuse object");
        });

        Check(results, "station_rear_route_cabinet_and_fence_v08", () =>
        {
            var cabinetGo = GameObject.Find("Cache_Cabinet");
            Require(cabinetGo != null && cabinetGo.GetComponent<DoorInteractable>() != null && cabinetGo.GetComponent<DoorInteractable>().cabinet, "Cache_Cabinet missing or not flagged as a cabinet");
            Require(quest.cacheMarker.sqrMagnitude > 0f && quest.rearRoute != null && quest.rearRoute.Length == 7 && quest.frontRoute != null && quest.frontRoute.Length >= 3, "Cache marker / rear route / front route not wired on QuestFuse");
            Require(quest.stationZombieSpots != null && quest.stationZombieSpots.Length == 4, "Station zombie spots not wired on QuestFuse");
            LootPickup cache = null;
            foreach (var l in UnityEngine.Object.FindObjectsByType<LootPickup>()) if (l.lootId == "loot_supply_cache") cache = l;
            Require(cache != null && cache.item != null && cache.item.id == "supply_cache" && cache.amount == 1, "loot_supply_cache missing or wrong item");
            Require((cache.transform.position - cabinetGo.transform.position).sqrMagnitude < 1.5f * 1.5f && cache.transform.position.y - cabinetGo.transform.position.y > 0.9f, "the supply bag is not on the cabinet shelf");
            foreach (var id in new[] { "loot_van_cloth_1", "loot_rear_stone_1" })
            {
                bool found = false;
                foreach (var l in UnityEngine.Object.FindObjectsByType<LootPickup>()) if (l.lootId == id) found = true;
                Require(found, id + " missing");
            }
            foreach (var fenceName in new[] { "Fence_East_South", "Fence_East_North", "Fence_South" })
            {
                var f = GameObject.Find(fenceName);
                Require(f != null && f.layer == Layers.Environment && f.GetComponent<BoxCollider>() != null, fenceName + " missing or not an Environment box");
                Require(Mathf.Abs(f.GetComponent<BoxCollider>().size.y - 2.1f) < 0.01f, fenceName + " must be a 2 m (2.1 with the sunk foot) fence");
            }
            Require(UnityEngine.Object.FindObjectsByType<ZombieBrain>().Length == 24, "zombie count changed");
        });

        string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/IlkIkmalValidation.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, string.Join(Environment.NewLine, results) + Environment.NewLine, Encoding.UTF8);
        foreach (var result in results) Debug.Log("ILKIKMAL_VALIDATION " + result);
        Debug.Log("ILKIKMAL_VALIDATION_REPORT " + reportPath);

        if (results.Exists(r => r.StartsWith("FAIL ", StringComparison.Ordinal)))
        {
            Debug.Log("ILKIKMAL_VALIDATION_RESULT FAIL");
            EditorApplication.Exit(1);
            throw new InvalidOperationException("Ilk Ikmal validation failed. See " + reportPath);
        }
        Debug.Log("ILKIKMAL_VALIDATION_RESULT PASS");
        EditorApplication.Exit(0);
    }

    // Open width at a floor point: two rays at chest height along the axis, door leaves ignored (they sit inside the gap).
    private static float MeasureWidth(Vector3 center, bool acrossX)
    {
        Vector3 axis = acrossX ? Vector3.right : Vector3.forward;
        Vector3 origin = new Vector3(center.x, 1f, center.z);
        return FirstSolid(origin, axis) + FirstSolid(origin, -axis);
    }

    private static float FirstSolid(Vector3 origin, Vector3 direction)
    {
        var hits = Physics.RaycastAll(origin, direction, 60f, 1 << Layers.Environment, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<NavIgnore>() != null) continue;
            return hit.distance;
        }
        return 60f;
    }

    private static float PathLength(Vector3 from, Vector3 to)
    {
        NavMeshHit a, b;
        if (!NavMesh.SamplePosition(from, out a, 2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out b, 2f, NavMesh.AllAreas)) return -1f;
        var path = new NavMeshPath();
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        if (path.status != NavMeshPathStatus.PathComplete) return -1f;
        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return length;
    }

    private static bool PathComplete(Vector3 from, Vector3 to)
    {
        NavMeshHit a, b;
        if (!NavMesh.SamplePosition(from, out a, 2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out b, 2f, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        return path.status == NavMeshPathStatus.PathComplete;
    }

    private static void RequirePath(Vector3 from, Vector3 to, string label)
    {
        NavMeshHit a, b;
        Require(NavMesh.SamplePosition(from, out a, 2f, NavMesh.AllAreas), label + ": start is not on the NavMesh");
        Require(NavMesh.SamplePosition(to, out b, 2f, NavMesh.AllAreas), label + ": end is not on the NavMesh");
        var path = new NavMeshPath();
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        Require(path.status == NavMeshPathStatus.PathComplete, label + ": path status " + path.status);
    }

    private static void Check(List<string> results, string name, Action action)
    {
        try
        {
            action();
            results.Add("PASS " + name);
        }
        catch (Exception e)
        {
            results.Add("FAIL " + name + " :: " + e.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
