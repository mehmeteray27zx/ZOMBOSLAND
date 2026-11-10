using System.Collections;
using System.IO;
using UnityEngine;

namespace Zombosland
{
    // --qa-screens-openworld (IlkIkmal, windowed with real rendering, not batchmode): HUD / terrain / crafting / storage screenshots
    // at four resolutions. PNGs go to QaScreens_openworld/<width>x<height>/ next to the exe. The player is teleported and turned,
    // zombies stay frozen, saving is off (SaveSystem.Enabled). The PNG writer is QaPng from QaScreens.cs.
    public class QaScreensOpenWorld : MonoBehaviour
    {
        static readonly int[,] Resolutions = { { 1280, 720 }, { 1920, 1080 }, { 2560, 1440 }, { 2560, 1080 } };

        Sandbox player;
        QuestFuse quest;
        PlayerSurvival ps;
        string root, dir, tag;
        float t0;
        int saved;

        IEnumerator TourShot(string shot, float fromRx, float fromRz, float toRx, float toRz, float lookUp)
        {
            Vector3 feet = WorldMap.RegionToWorld(fromRx, fromRz);
            feet.y = TerrainY(feet) + 0.1f;
            Vector3 target = WorldMap.RegionToWorld(toRx, toRz);
            target.y = TerrainY(target) + lookUp;
            yield return Look(feet, target, 0.9f);
            yield return Capture(shot);
        }

        IEnumerator Start()
        {
            if (Application.isBatchMode) { Debug.LogError("QA_SCREENS_OPENWORLD FAIL batchmode has no rendering"); GameQuit.Request(); yield break; }
            Application.runInBackground = true;
            Sandbox.InstantHands = true; // hand actions apply at once in scripted runs
            player = GetComponent<Sandbox>();
            for (int i = 0; i < 600; i++)
            {
                quest = QuestFuse.Instance;
                ps = player != null ? player.Survival : null;
                if (player != null && player.Motor != null && player.AimPivot != null && quest != null && ps != null && ps.Bag != null && ps.database != null) break;
                yield return null;
            }
            if (quest == null || ps == null || ps.database == null) { Debug.LogError("QA_SCREENS_OPENWORLD FAIL IlkIkmal scene objects missing"); GameQuit.Request(); yield break; }
            yield return new WaitForSecondsRealtime(1.0f);

            root = Path.Combine(Path.GetDirectoryName(Application.dataPath), "QaScreens_openworld");
            Directory.CreateDirectory(root);
            player.QaSetFreeze(true);
            player.Motor.InputEnabled = false;
            t0 = Time.time;
            Debug.Log("QA_SCREENS_OPENWORLD start save=" + SaveSystem.Enabled + " dir=" + root);

            ItemDef bandage = ps.database.FindItem("bandage");
            RecipeDef bandageRecipe = ps.database.FindRecipe("bandage");
            Workbench bench = FindAnyObjectByType<Workbench>();
            StorageBox box = FindAnyObjectByType<StorageBox>();
            quest.RestoreState(QuestState.ExploreStation); // objective + compass marker, no events

            for (int r = 0; r < Resolutions.GetLength(0); r++)
            {
                int rw = Resolutions[r, 0], rh = Resolutions[r, 1];
                tag = rw + "x" + rh;
                dir = Path.Combine(root, tag);
                Directory.CreateDirectory(dir);
                Screen.SetResolution(rw, rh, FullScreenMode.Windowed);
                for (int f = 0; f < 6; f++) yield return null;
                yield return new WaitForSecondsRealtime(1.0f);
                Debug.Log("QA_SCREENS_OPENWORLD resolution requested=" + tag + " actual=" + Screen.width + "x" + Screen.height);

                ps.CloseUi();
                player.Motor.InputEnabled = false;
                FillBag(bandage);

                // 01: on the road at region (160, 85), facing east.
                Vector3 road = WorldMap.RegionToWorld(160f, 85f);
                road.y = TerrainY(road) + 0.1f;
                yield return Look(road, road + new Vector3(30f, 1.6f, 0f), 0.9f);
                yield return Capture("01_hud_road");

                // 02: just outside the gate, looking east at the terrain.
                Vector3 gate = new Vector3(53f, 0f, -38f);
                gate.y = TerrainY(gate) + 0.1f;
                yield return Look(gate, gate + new Vector3(30f, 1.8f, 0f), 0.9f);
                yield return Capture("02_terrain_gate_view");

                // 03: the old station, seen from the yard side.
                Vector3 mark = quest.stationMarker;
                Vector3 from = quest.yardMarker;
                Vector3 dirToStation = (from.sqrMagnitude > 0.1f ? mark - from : Vector3.right);
                dirToStation.y = 0f;
                dirToStation = dirToStation.sqrMagnitude > 0.01f ? dirToStation.normalized : Vector3.right;
                Vector3 stand = mark - dirToStation * 16f;
                stand.y = TerrainY(stand) + 0.1f;
                yield return Look(stand, mark + Vector3.up * 2f, 0.9f);
                yield return Capture("03_station");

                // 04: crafting screen at the workbench with the bandage recipe selected.
                if (bench != null)
                {
                    Vector3 bp = bench.transform.position;
                    Vector3 toYard = quest.yardBounds.size.sqrMagnitude > 0f ? quest.yardBounds.center - bp : -bench.transform.forward;
                    toYard.y = 0f;
                    toYard = toYard.sqrMagnitude > 0.01f ? toYard.normalized : Vector3.back;
                    Vector3 feet = bp + toYard * 2f;
                    feet.y = GroundY(feet, bp.y);
                    yield return Look(feet, bench.HintAnchor.position, 0.6f);
                    SurvivalUi.QaSelectRecipe(bandageRecipe);
                    ps.OpenUi(UiTab.Crafting);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Debug.Log("QA_SCREENS_OPENWORLD crafting open=" + ps.UiOpen + " benchInRange=" + ps.BenchInRange);
                    yield return Capture("04_crafting_screen");
                    ps.CloseUi();
                    player.Motor.InputEnabled = false;
                }
                else Debug.LogWarning("QA_SCREENS_OPENWORLD no Workbench in the scene, 04 skipped");

                // 05: inventory with the storage side, a bag item selected.
                if (box != null)
                {
                    ps.Storage.Clear();
                    ps.Storage.Add(ps.database.FindItem("wood"), 8);
                    ps.Storage.Add(ps.database.FindItem("scrap"), 3);
                    Vector3 sp = box.transform.position;
                    Vector3 toYard = quest.yardBounds.size.sqrMagnitude > 0f ? quest.yardBounds.center - sp : -box.transform.forward;
                    toYard.y = 0f;
                    toYard = toYard.sqrMagnitude > 0.01f ? toYard.normalized : Vector3.back;
                    Vector3 feet = sp + toYard * 1.8f;
                    feet.y = GroundY(feet, sp.y);
                    yield return Look(feet, box.HintAnchor.position, 0.6f);
                    int slot = 0;
                    for (int i = 0; i < ps.Bag.Capacity; i++) if (!ps.Bag[i].IsEmpty && ps.Bag[i].item == bandage) { slot = i; break; }
                    SurvivalUi.QaSelectSlot(slot);
                    ps.OpenUi(UiTab.Inventory, box);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Debug.Log("QA_SCREENS_OPENWORLD storage open=" + ps.UiOpen + " storage=" + (ps.OpenStorage != null));
                    yield return Capture("05_inventory_storage");
                    ps.CloseUi();
                    player.Motor.InputEnabled = false;
                }
                else Debug.LogWarning("QA_SCREENS_OPENWORLD no StorageBox in the scene, 05 skipped");
            }

            Debug.Log("QA_SCREENS_OPENWORLD pngs=" + saved);
            // World tour at 1920x1080: the places the road screenshots do not reach (visual review, nothing asserted).
            tag = "world";
            dir = Path.Combine(root, tag);
            Directory.CreateDirectory(dir);
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            for (int f = 0; f < 6; f++) yield return null;
            yield return new WaitForSecondsRealtime(1.0f);
            ps.CloseUi();
            player.Motor.InputEnabled = false;
            if (quest.outerGate != null) quest.outerGate.ForceOpen(true);
            yield return TourShot("06_bridge", 300f, 258f, 322f, 295f, 3f);
            yield return TourShot("07_ford", 258f, 310f, 278f, 301f, 0.5f);
            yield return TourShot("08_water_tower", 366f, 366f, 390f, 390f, 14f);
            yield return TourShot("09_hut", 136f, 283f, 145f, 300f, 2f);
            Vector3 gateView = new Vector3(36f, 0.05f, -38f);
            yield return Look(gateView, new Vector3(52f, 1.6f, -38f), 0.9f);
            yield return Capture("10_yard_gate_open_lamps");
            BuildSlot service = BuildSlot.Find("barricade_service"), chestSlot = BuildSlot.Find("chest_yard");
            if (service != null) service.ForceState(true, BuildSlot.MaxHealth);
            yield return Look(new Vector3(40f, 0.05f, -22f), new Vector3(44f, 1.2f, -22f), 0.9f);
            yield return Capture("11_barricade");
            if (chestSlot != null) chestSlot.ForceState(true, BuildSlot.MaxHealth);
            yield return Look(new Vector3(29f, 0.05f, -35.5f), new Vector3(25.2f, 0.5f, -35.5f), 0.9f);
            yield return Capture("12_chest_workbench");
            ItemDef batItem = ps.database.FindItem("bat");
            ps.Bag.Add(batItem, 1);
            ps.AssignQuick(2, batItem);
            ps.UseQuick(2);
            yield return Look(new Vector3(38f, 0.05f, -30f), new Vector3(30f, 1.4f, -30f), 0.9f);
            yield return Capture("13_bat_equipped");
            // v0.8 station rear: supply cabinet (closed, open) and the rear approach with the fence, gate gap and sign.
            GameObject cabGo = GameObject.Find("Cache_Cabinet");
            if (cabGo != null)
            {
                DoorInteractable cab = cabGo.GetComponent<DoorInteractable>();
                Vector3 front = cabGo.transform.TransformPoint(new Vector3(0f, 0f, -1.4f));
                front.y = GroundY(front, cabGo.transform.position.y);
                Vector3 shelf = cabGo.transform.TransformPoint(new Vector3(0f, 1.2f, 0f));
                yield return Look(front, shelf, 0.9f);
                yield return Capture("14_cabinet_closed");
                if (cab != null) cab.ForceOpen(true);
                yield return Look(front, shelf, 0.9f);
                yield return Capture("15_cabinet_open");
            }
            else Debug.LogWarning("QA_SCREENS_OPENWORLD no Cache_Cabinet in the scene, 14/15 skipped");
            if (quest.rearRoute != null && quest.rearRoute.Length >= 6)
            {
                Vector3 a = quest.rearRoute[2], b = quest.rearRoute[3];
                a.y = TerrainY(a) + 0.1f;
                yield return Look(a, b + Vector3.up * 1.6f, 0.9f);
                yield return Capture("16_rear_route_fence");
                Vector3 g = quest.rearRoute[3];
                g.y = TerrainY(g) + 0.1f;
                yield return Look(g, quest.rearRoute[4] + Vector3.up * 1.6f, 0.9f);
                yield return Capture("17_rear_gate_sign");
            }
            Debug.Log("QA_SCREENS_OPENWORLD_DONE");
            GameQuit.Request();
        }

        // Test items for the crafting / inventory shots (cloth 6, scrap 5, wood 4, tape 2, stone 3, bandage 1).
        void FillBag(ItemDef bandage)
        {
            ps.Bag.Clear();
            Give("cloth", 6);
            Give("scrap", 5);
            Give("wood", 4);
            Give("tape", 2);
            Give("stone", 3);
            Give("bandage", 1);
            if (bandage != null) ps.AssignQuick(1, bandage);
        }

        void Give(string id, int n)
        {
            ItemDef d = ps.database.FindItem(id);
            if (d == null) { Debug.LogWarning("QA_SCREENS_OPENWORLD item missing: " + id); return; }
            ps.Bag.Add(d, n);
        }

        static float TerrainY(Vector3 p)
        {
            Terrain t = Terrain.activeTerrain;
            return t != null ? t.SampleHeight(p) + t.transform.position.y : 0f;
        }

        // Ground below a point (floor slabs included); falls back to the given height.
        static float GroundY(Vector3 p, float fallback)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, fallback + 3f, p.z), Vector3.down, out hit, 8f)) return hit.point.y + 0.05f;
            return fallback;
        }

        // Teleports to `feet`, turns toward `target` and waits so physics, the camera effects and the HUD settle.
        IEnumerator Look(Vector3 feet, Vector3 target, float settle)
        {
            player.Motor.Teleport(feet);
            Vector3 dir3 = target - (feet + Vector3.up * 1.65f);
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir3.x, dir3.z) * Mathf.Rad2Deg, 0f);
            player.Motor.QaSetPitch(-Mathf.Asin(Mathf.Clamp(dir3.y / Mathf.Max(0.001f, dir3.magnitude), -1f, 1f)) * Mathf.Rad2Deg);
            Physics.SyncTransforms();
            yield return null;
            yield return new WaitForSeconds(settle);
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            QaPng.Write(Path.Combine(dir, name + ".png"), tex);
            Destroy(tex);
            saved++;
            Debug.Log("QA_SCREENS_OPENWORLD saved " + tag + "/" + name + ".png " + Screen.width + "x" + Screen.height + " t=" + (Time.time - t0).ToString("F1")
                + " label='" + (GetComponent<Interactor>() != null ? GetComponent<Interactor>().Label ?? "" : "") + "'");
        }
    }
}
