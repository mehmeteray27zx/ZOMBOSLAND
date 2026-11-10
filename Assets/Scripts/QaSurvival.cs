using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Zombosland
{
    // --qa-survival (Ilk Ikmal only): inventory / crafting / quest / save / NavMesh / walking checks.
    // Pure Inventory and Crafter checks use runtime-created ScriptableObjects; world checks use the real scene objects
    // (Workbench, YardStorage, LootPickup, Gate_Outer, Door_Service). Own save slot "qa_survival" (wiped by RuntimeBootstrap).
    // Output: QA_SURVIVAL <id> PASS|FAIL <detail>, then QA_SURVIVAL_DONE pass= fail=.
    public class QaSurvival : MonoBehaviour
    {
        const float WatchdogSeconds = 480f;
        const int WalkStepsPerFrame = 20, WalkStallSteps = 120;
        const float WalkStep = 0.1f;

        int pass, fail;
        float startedAt;
        bool finished;

        Sandbox player;
        PlayerSurvival ps;
        QuestFuse quest;
        ZombieDirector director;
        Interactor interactor;
        FuseItem fuse;
        FusePanel panel;
        AmmoCrate crate;
        DoorInteractable gate, serviceDoor;
        Workbench bench;
        StorageBox storage;
        readonly List<DoorInteractable> doorList = new List<DoorInteractable>();
        ItemDef cloth, scrap, wood, tape, stone, bandage, bat, lure;
        RecipeDef rBandage;
        // Build-slot state left behind by BuildChecks, expected back after the save / reload.
        bool qaBarricadeBuilt, qaChestBuilt;
        float qaBarricadeHealth;

        // POI world positions (region coordinates run through WorldMap).
        static readonly Vector3 Yard = WorldMap.RegionToWorld(80f, 80f);
        static readonly Vector3 Station = WorldMap.RegionToWorld(245f, 155f);
        static readonly Vector3 Hut = WorldMap.RegionToWorld(145f, 300f);
        static readonly Vector3 BridgeSw = WorldMap.RegionToWorld(306f, 268f);
        static readonly Vector3 BridgeNe = WorldMap.RegionToWorld(322f, 295f);
        static readonly Vector3 Ford = WorldMap.RegionToWorld(267f, 305f);
        static readonly Vector3 Tower = WorldMap.RegionToWorld(390f, 390f);

        // ---------- logging ----------

        void Log(string id, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            Debug.Log("QA_SURVIVAL " + id + (ok ? " PASS " : " FAIL ") + detail);
        }

        static void Info(string id, string detail) { Debug.Log("QA_SURVIVAL " + id + " INFO " + detail); }

        static string F(float v) { return v.ToString("F1", CultureInfo.InvariantCulture); }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - startedAt > WatchdogSeconds)
            {
                Log("watchdog", false, "run did not finish within " + WatchdogSeconds + " s");
                Finish();
            }
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            Time.timeScale = 1f;
            Debug.Log("QA_SURVIVAL_DONE pass=" + pass + " fail=" + fail);
            GameQuit.Request();
        }

        // ---------- run ----------

        IEnumerator Start()
        {
            startedAt = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            Sandbox.InstantHands = true; // hand actions apply at once in scripted runs
            yield return Acquire();
            if (player == null) { Log("setup", false, "scene objects missing"); Finish(); yield break; }
            Log("setup", ps.Bag != null && ps.Storage != null && ps.Crafter != null && ps.Quick != null && ps.Quick.Length == PlayerSurvival.QuickSlots && ps.Quick[0] == null,
                "scene=" + SceneManager.GetActiveScene().name + " navmesh=" + director.NavMeshReady + " bag=" + ps.Bag.Capacity + " storage=" + ps.Storage.Capacity);

            try { RunPure(); }
            catch (Exception e) { Log("pure_exception", false, e.ToString()); }

            ResolveItems();
            WorldObjectChecks();
            PoiChecks();
            yield return new WaitForSeconds(2.5f); // carving obstacles take effect asynchronously

            // ---- closed gate cuts the way out ----
            Vector3 yardP = Ground(Yard);
            float closedLen;
            bool closedComplete = NavPath("nav_gate_closed_probe", yardP, Ground(Station), out closedLen, false);
            Info("nav_gate_closed", "yard->station complete=" + closedComplete + " length=" + F(closedLen) + " gateLocked=" + (gate != null && gate.locked) + " gateOpen=" + (gate != null && gate.IsOpen));

            yield return UiChecks();
            yield return CraftingWorldChecks();
            yield return BandageChecks();
            yield return HandsChecks();
            yield return MeleeChecks();
            yield return StoneChecks();
            yield return QuestFlow();

            // ---- gate open: routes complete, and the closed gate really was an obstacle ----
            yield return new WaitForSeconds(2.5f); // carving removal
            float openLen;
            bool openComplete = NavPath("nav_yard_station", yardP, Ground(Station), out openLen, true);
            Log("nav_gate_blocks", gate != null && (!closedComplete || openLen + 1f < closedLen),
                "closed complete=" + closedComplete + " length=" + F(closedLen) + " | open complete=" + openComplete + " length=" + F(openLen));
            float len;
            NavPath("nav_station_bridge", Ground(Station), Ground(BridgeSw), out len, true);
            NavPath("nav_bridge_tower", Ground(BridgeNe), Ground(Tower), out len, true);
            NavPath("nav_yard_hut", yardP, Ground(Hut), out len, true);
            NavPath("nav_hut_ford", Ground(Hut), Ground(Ford), out len, true);
            NavPath("nav_ford_bridge", Ground(Ford), Ground(BridgeSw), out len, true);

            // ---- real walking with the CharacterController ----
            ParkZombies(); // a frozen zombie standing in a doorway would block the capsule and say nothing about the route
            yield return Walk("walk_yard_station", yardP, Ground(Station));
            yield return Walk("walk_yard_hut", yardP, Ground(Hut));
            yield return RouteChecks();

            yield return StationAndYard();
            ParkZombies();
            yield return BuildChecks();
            yield return SaveReloadChecks();
            yield return DeathChecks();
            Finish();
        }

        // ---------- scene discovery ----------

        IEnumerator Acquire()
        {
            player = null; ps = null; quest = null; director = null; interactor = null;
            for (int i = 0; i < 1500; i++)
            {
                yield return null;
                Sandbox p = FindAnyObjectByType<Sandbox>();
                QuestFuse q = QuestFuse.Instance;
                ZombieDirector d = ZombieDirector.Instance;
                if (p != null && q != null && d != null && d.NavMeshReady && p.Motor != null && p.AimPivot != null && PlayerSurvival.Instance != null
                    && WorldState.Instance != null && p.GetComponent<Interactor>() != null)
                {
                    player = p; quest = q; director = d; ps = PlayerSurvival.Instance; interactor = p.GetComponent<Interactor>();
                    break;
                }
            }
            if (player == null) yield break;
            doorList.Clear();
            gate = null; serviceDoor = null;
            foreach (DoorInteractable d in FindObjectsByType<DoorInteractable>())
            {
                doorList.Add(d);
                if (d.name == "Gate_Outer") gate = d;
                else if (d.name == "Door_Service") serviceDoor = d;
            }
            if (gate == null) gate = quest.outerGate;
            if (serviceDoor == null) serviceDoor = quest.serviceDoor;
            fuse = FindAnyObjectByType<FuseItem>();
            panel = FindAnyObjectByType<FusePanel>();
            { GameObject crateGo = GameObject.Find("AmmoCrate"); crate = crateGo != null ? crateGo.GetComponent<AmmoCrate>() : null; }
            bench = FindAnyObjectByType<Workbench>();
            { GameObject ysGo = GameObject.Find("YardStorage"); storage = ysGo != null ? ysGo.GetComponent<StorageBox>() : null; }
            player.QaSetFreeze(true); // zombies stay still unless a check lets them act
        }

        void ResolveItems()
        {
            ItemDatabase db = ps.database;
            if (db == null) { Log("database", false, "PlayerSurvival.database is not set"); return; }
            cloth = db.FindItem("cloth"); scrap = db.FindItem("scrap"); wood = db.FindItem("wood"); tape = db.FindItem("tape");
            stone = db.FindItem("stone"); bandage = db.FindItem("bandage"); bat = db.FindItem("bat"); lure = db.FindItem("lure");
            rBandage = db.FindRecipe("bandage");
            bool all = cloth != null && scrap != null && wood != null && tape != null && stone != null && bandage != null && bat != null && lure != null && rBandage != null;
            Log("database", all, "items=" + (db.items != null ? db.items.Length : 0) + " recipes=" + (db.recipes != null ? db.recipes.Length : 0)
                + " cloth=" + (cloth != null) + " bandage=" + (bandage != null) + " bat=" + (bat != null) + " lure=" + (lure != null) + " recipeBandage=" + (rBandage != null));
            if (all)
            {
                RecipeDef rLure = db.FindRecipe("lure"), rBat = db.FindRecipe("bat");
                Log("database_values", bandage.healAmount == 25f && bandage.maxStack == 5 && bat.meleeDamage == 40f && bat.meleeRange == 1.9f && bat.meleeSeconds == 0.7f
                    && lure.noiseRadius == 16f && cloth.maxStack == 10 && rBandage.seconds == 3f && rLure != null && rBat != null,
                    "bandage heal=" + bandage.healAmount + " bat dmg=" + bat.meleeDamage + " range=" + bat.meleeRange + " lure radius=" + lure.noiseRadius);
            }
        }

        void ResetBag()
        {
            ps.Crafter.Cancel("qa");
            ps.Bag.Clear();
            ps.Storage.Clear();
            for (int i = 1; i < PlayerSurvival.QuickSlots; i++) ps.AssignQuick(i, null);
            ps.UseQuick(0);
        }

        // ---------- pure checks (no scene needed) ----------

        static ItemDef MakeItem(string id, string name, int max, ItemKind kind)
        {
            ItemDef d = ScriptableObject.CreateInstance<ItemDef>();
            d.name = id; d.id = id; d.displayName = name; d.maxStack = max; d.kind = kind;
            return d;
        }

        static ItemAmount A(ItemDef d, int n) { return new ItemAmount { item = d, count = n }; }

        static RecipeDef MakeRecipe(string id, ItemDef output, int outputCount, float seconds, bool needsBench, params ItemAmount[] inputs)
        {
            RecipeDef r = ScriptableObject.CreateInstance<RecipeDef>();
            r.name = id; r.id = id; r.displayName = id; r.output = output; r.outputCount = outputCount; r.seconds = seconds;
            r.needsBench = needsBench; r.available = true; r.inputs = inputs;
            return r;
        }

        void RunPure()
        {
            ItemDef clothP = MakeItem("cloth", "Kumaş", 10, ItemKind.Resource);
            ItemDef stoneP = MakeItem("stone", "Taş", 10, ItemKind.Resource);
            ItemDef bandP = MakeItem("bandage", "Bandaj", 5, ItemKind.Consumable);
            RecipeDef rec = MakeRecipe("bandage", bandP, 1, 3f, false, A(clothP, 2));
            RecipeDef recBench = MakeRecipe("bench", bandP, 1, 4f, true, A(clothP, 2));

            // stacks and capacity
            var inv = new Inventory(3);
            int a1 = inv.Add(clothP, 25), a2 = inv.Add(clothP, 10);
            Log("inv_stack", a1 == 25 && a2 == 5 && inv.Count(clothP) == 30 && inv.UsedSlots == 3 && inv[0].count == 10 && inv[2].count == 10 && inv.FreeSpaceFor(clothP) == 0,
                "added " + a1 + " then " + a2 + " (partial) count=" + inv.Count(clothP) + " used=" + inv.UsedSlots + " free=" + inv.FreeSpaceFor(clothP));
            var small = new Inventory(2);
            small.Add(clothP, 10);
            Log("inv_capacity", small.FreeSpaceFor(stoneP) == 10 && small.FreeSpaceFor(clothP) == 10 && small.Add(stoneP, 15) == 10 && small.UsedSlots == 2,
                "capacity 2: stone added=" + small.Count(stoneP) + " used=" + small.UsedSlots);
            var neg = new Inventory(2);
            neg.Add(clothP, 3);
            Log("inv_negative", neg.Add(clothP, -5) == 0 && !neg.TryRemove(clothP, -1) && !neg.Reserve(clothP, -3) && neg.Count(clothP) == 3 && neg.Reserved(clothP) == 0,
                "negative amounts do nothing count=" + neg.Count(clothP));

            // reservation
            var rv = new Inventory(3);
            rv.Add(clothP, 10);
            bool r1 = rv.Reserve(clothP, 4), r2 = rv.Reserve(clothP, 7);
            int avail = rv.Available(clothP);
            bool rm1 = rv.TryRemove(clothP, 7), rm2 = rv.TryRemove(clothP, 6);
            Log("inv_reserve", r1 && !r2 && avail == 6 && !rm1 && rm2 && rv.Count(clothP) == 4 && rv.Available(clothP) == 0 && rv.Reserved(clothP) == 4,
                "reserve 4 ok, 7 refused, available=" + avail + ", remove 7 refused, remove 6 ok, count=" + rv.Count(clothP));
            rv.Release(clothP, 4);
            Log("inv_release", rv.Available(clothP) == 4 && rv.Reserved(clothP) == 0, "released: available=" + rv.Available(clothP));

            // atomic commit
            var cm = new Inventory(2);
            cm.Add(clothP, 2); cm.Add(stoneP, 10);
            cm.Reserve(clothP, 1);
            int changes = 0;
            cm.Changed += () => changes++;
            bool failed = cm.CommitReserved(new[] { A(clothP, 1) }, bandP, 1);
            Log("inv_commit_atomic", !failed && cm.Count(clothP) == 2 && cm.Count(stoneP) == 10 && cm.Count(bandP) == 0 && cm.Reserved(clothP) == 1 && changes == 0,
                "commit with no room for the output changed nothing: cloth=" + cm.Count(clothP) + " reserved=" + cm.Reserved(clothP) + " changed=" + changes);
            var cm2 = new Inventory(1);
            cm2.Add(clothP, 2); cm2.Reserve(clothP, 2);
            int changes2 = 0;
            cm2.Changed += () => changes2++;
            bool okCommit = cm2.CommitReserved(new[] { A(clothP, 2) }, bandP, 1);
            Log("inv_commit_slot_reuse", okCommit && cm2.Count(bandP) == 1 && cm2.Count(clothP) == 0 && cm2.Reserved(clothP) == 0 && changes2 == 1,
                "output used the slot the inputs freed: bandage=" + cm2.Count(bandP) + " reserved=" + cm2.Reserved(clothP) + " changedEvents=" + changes2);
            var cm3 = new Inventory(2);
            cm3.Add(clothP, 2);
            Log("inv_commit_unreserved", !cm3.CommitReserved(new[] { A(clothP, 2) }, bandP, 1) && cm3.Count(clothP) == 2, "commit without a reservation is refused");

            // moving between inventories never takes reserved items
            var src = new Inventory(3); var dst = new Inventory(1);
            src.Add(clothP, 5); src.Reserve(clothP, 2);
            int moved = src.MoveSlotTo(0, dst);
            Log("inv_move", moved == 3 && src.Count(clothP) == 2 && src.Reserved(clothP) == 2 && dst.Count(clothP) == 3, "moved " + moved + " of 5 (2 reserved)");

            // snapshot / load
            ItemStack[] snap = rv.Snapshot();
            snap[0].count = 1; // must not touch the inventory
            var copy = new Inventory(3);
            copy.Load(rv.Snapshot());
            Log("inv_snapshot", rv.Count(clothP) == 4 && copy.Count(clothP) == 4 && copy.Reserved(clothP) == 0, "snapshot is a copy; load restores counts");

            // crafter
            var bag = new Inventory(24);
            bag.Add(clothP, 1);
            var cr = new Crafter(bag);
            CraftCheck ck = cr.Check(rec, 1, false);
            Log("craft_missing_msg", ck.block == CraftBlock.MissingInput && ck.message == "Kumaş: 1 / 2 — 1 eksik" && !ck.Ok, "message='" + ck.message + "'");

            bag = new Inventory(24); bag.Add(clothP, 4);
            cr = new Crafter(bag);
            int done = 0;
            cr.UnitCompleted += r => done++;
            bool started = cr.Start(rec, 2, 0f, false);
            bool reservedOk = bag.Reserved(clothP) == 4 && bag.Available(clothP) == 0 && cr.Busy && cr.Total == 2 && cr.Remaining == 2;
            cr.Tick(2.9f, false);
            bool early = bag.Count(bandP) == 0 && cr.UnitProgress(2.9f) > 0.9f;
            cr.Tick(3.0f, false);
            bool first = bag.Count(bandP) == 1 && cr.Remaining == 1 && cr.Busy && bag.Reserved(clothP) == 2;
            cr.Tick(5.9f, false);
            bool notYet = bag.Count(bandP) == 1;
            cr.Tick(6.0f, false);
            Log("craft_normal", started && reservedOk && early && first && notYet && bag.Count(bandP) == 2 && !cr.Busy && bag.Count(clothP) == 0 && bag.Reserved(clothP) == 0 && done == 2,
                "qty 2: bandage=" + bag.Count(bandP) + " cloth=" + bag.Count(clothP) + " busy=" + cr.Busy + " units=" + done);

            // a full bag: the slot the input frees can hold the output
            bag = new Inventory(1); bag.Add(clothP, 2);
            cr = new Crafter(bag);
            CraftCheck freed = cr.Check(rec, 1, false);
            bool s1 = cr.Start(rec, 1, 0f, false);
            cr.Tick(3f, false);
            Log("craft_full_bag_slot", freed.Ok && s1 && bag.Count(bandP) == 1 && bag.Count(clothP) == 0, "1-slot bag: craft ok=" + freed.Ok + " bandage=" + bag.Count(bandP));
            bag = new Inventory(2); bag.Add(clothP, 3); bag.Add(stoneP, 10);
            cr = new Crafter(bag);
            CraftCheck full = cr.Check(rec, 1, false);
            Log("craft_bag_full_block", full.block == CraftBlock.BagFull && full.message == "Çantada yer yok" && !cr.Start(rec, 1, 0f, false) && bag.Reserved(clothP) == 0,
                "no room for the output: block=" + full.block + " message='" + full.message + "'");

            // double start
            bag = new Inventory(24); bag.Add(clothP, 6);
            cr = new Crafter(bag);
            bool st1 = cr.Start(rec, 1, 0f, false);
            bool st2 = cr.Start(rec, 1, 0f, false);
            CraftCheck busy = cr.Check(rec, 1, false);
            Log("craft_double_start", st1 && !st2 && busy.block == CraftBlock.Busy && busy.message == "Üretim sürüyor" && bag.Reserved(clothP) == 2,
                "second Start refused, reserved=" + bag.Reserved(clothP) + " message='" + busy.message + "'");

            // cancel gives the rest back
            bag = new Inventory(24); bag.Add(clothP, 4);
            cr = new Crafter(bag);
            int cancelled = 0;
            cr.Cancelled += s => cancelled++;
            cr.Start(rec, 2, 0f, false);
            cr.Tick(3f, false);
            cr.Cancel("test");
            cr.Cancel("test again");
            Log("craft_cancel_refund", !cr.Busy && bag.Count(bandP) == 1 && bag.Count(clothP) == 2 && bag.Reserved(clothP) == 0 && cancelled == 1,
                "after one unit and a cancel: bandage=" + bag.Count(bandP) + " cloth=" + bag.Count(clothP) + " reserved=" + bag.Reserved(clothP) + " cancelEvents=" + cancelled);

            // race: finishing and cancelling in the same moment never doubles the output
            bag = new Inventory(24); bag.Add(clothP, 2);
            cr = new Crafter(bag);
            cr.Start(rec, 1, 0f, false);
            cr.Tick(3f, false);
            cr.Cancel("late");
            cr.Tick(3.1f, false);
            bool afterDone = bag.Count(bandP) == 1 && bag.Count(clothP) == 0 && bag.Reserved(clothP) == 0;
            bag = new Inventory(24); bag.Add(clothP, 2);
            cr = new Crafter(bag);
            cr.Start(rec, 1, 0f, false);
            cr.Cancel("early");
            cr.Tick(3f, false);
            Log("craft_race", afterDone && bag.Count(bandP) == 0 && bag.Count(clothP) == 2 && bag.Reserved(clothP) == 0,
                "tick then cancel: one bandage, cancel then tick: none (cloth=" + bag.Count(clothP) + ")");

            // cancel from inside event handlers
            bag = new Inventory(24); bag.Add(clothP, 6);
            cr = new Crafter(bag);
            int handlerCancels = 0;
            cr.Cancelled += s => handlerCancels++;
            cr.UnitCompleted += r => cr.Cancel("handler");
            cr.Start(rec, 3, 0f, false);
            cr.Tick(3f, false);
            bool inUnit = !cr.Busy && bag.Count(bandP) == 1 && bag.Count(clothP) == 4 && bag.Reserved(clothP) == 0 && handlerCancels == 1;
            bag = new Inventory(24); bag.Add(clothP, 4);
            cr = new Crafter(bag);
            bool cancelOnChange = false;
            bag.Changed += () => { if (cancelOnChange) cr.Cancel("changed"); };
            cr.Start(rec, 2, 0f, false);
            cancelOnChange = true;
            cr.Tick(3f, false);
            cancelOnChange = false;
            Log("craft_reentrant_cancel", inUnit && !cr.Busy && bag.Count(bandP) == 1 && bag.Count(clothP) == 2 && bag.Reserved(clothP) == 0,
                "cancel inside UnitCompleted ok=" + inUnit + "; cancel inside Changed: bandage=" + bag.Count(bandP) + " cloth=" + bag.Count(clothP) + " reserved=" + bag.Reserved(clothP));

            // bench
            bag = new Inventory(24); bag.Add(clothP, 2);
            cr = new Crafter(bag);
            CraftCheck nb = cr.Check(recBench, 1, false);
            CraftCheck yb = cr.Check(recBench, 1, true);
            string cancelReason = null;
            cr.Cancelled += s => cancelReason = s;
            bool sb = cr.Start(recBench, 1, 0f, true);
            cr.Tick(1f, false);
            Log("craft_needs_bench", nb.block == CraftBlock.NeedsBench && nb.message == "Tezgâh gerekli" && yb.Ok && sb && !cr.Busy && cancelReason == "Tezgâhtan uzaklaştın" && bag.Reserved(clothP) == 0,
                "check message='" + nb.message + "' leaving the bench cancels: '" + cancelReason + "'");

            // unavailable / bad quantity
            bag = new Inventory(24); bag.Add(clothP, 6);
            cr = new Crafter(bag);
            RecipeDef off = MakeRecipe("off", bandP, 1, 1f, false, A(clothP, 1));
            off.available = false;
            Log("craft_bad_input", cr.Check(off, 1, true).block == CraftBlock.Unavailable && !cr.Start(rec, 0, 0f, false) && !cr.Start(rec, -3, 0f, false)
                && cr.Check(rec, 100, false).block == CraftBlock.Unavailable && !cr.Busy && bag.Reserved(clothP) == 0 && cr.Check(null, 1, false).block == CraftBlock.Unavailable,
                "unavailable recipe, zero / negative / huge quantity and null recipe are refused");
        }

        // ---------- world objects and POIs ----------

        void WorldObjectChecks()
        {
            Log("world_workbench", bench != null && Workbench.All.Count > 0 && GameObject.Find("Workbench") != null, "Workbench found=" + (bench != null));
            GameObject ys = GameObject.Find("YardStorage");
            Log("world_storage", storage != null && ys != null && ys.GetComponent<StorageBox>() != null, "YardStorage StorageBox found=" + (ys != null && ys.GetComponent<StorageBox>() != null));
            LootPickup[] loot = FindObjectsByType<LootPickup>();
            var ids = new HashSet<string>();
            int dup = 0, noItem = 0, noId = 0;
            foreach (LootPickup l in loot)
            {
                if (string.IsNullOrEmpty(l.lootId)) { noId++; continue; }
                if (!ids.Add(l.lootId)) dup++;
                if (l.item == null || l.amount <= 0) noItem++;
            }
            Log("world_loot", loot.Length > 0 && dup == 0 && noItem == 0 && noId == 0, "LootPickup count=" + loot.Length + " unique ids=" + ids.Count + " duplicates=" + dup + " missingItem=" + noItem + " missingId=" + noId);
            Log("world_gate", gate != null, "Gate_Outer " + (gate != null ? "found locked=" + gate.locked + " open=" + gate.IsOpen : "missing"));
            Log("world_service_door", serviceDoor != null, "Door_Service " + (serviceDoor != null ? "found locked=" + serviceDoor.locked : "missing"));
            Log("world_quest_refs", fuse != null && panel != null && crate != null && quest.stationBounds.size.sqrMagnitude > 0f && quest.yardBounds.size.sqrMagnitude > 0f,
                "fuse=" + (fuse != null) + " panel=" + (panel != null) + " ammoCrate=" + (crate != null) + " stationBounds=" + quest.stationBounds.size + " yardBounds=" + quest.yardBounds.size);
        }

        void PoiChecks()
        {
            Vector3[] all = { Yard, Station, Hut, BridgeSw, BridgeNe, Ford, Tower };
            bool inside = true;
            foreach (Vector3 p in all) if (!WorldMap.InBounds(p)) inside = false;
            Log("poi_bounds", inside && Mathf.Abs(Yard.x - 34f) < 0.01f && Mathf.Abs(Yard.z + 30f) < 0.01f && Mathf.Abs(Tower.x - 344f) < 0.01f,
                "yard=" + Yard + " station=" + Station + " hut=" + Hut + " tower=" + Tower + " all inside WorldMap bounds=" + inside);
            Vector2 back = WorldMap.WorldToRegion(Station);
            Log("poi_roundtrip", Mathf.Abs(back.x - 245f) < 0.01f && Mathf.Abs(back.y - 155f) < 0.01f, "region round trip " + back);
        }

        // ---------- terrain / NavMesh helpers ----------

        static Vector3 Ground(Vector3 p)
        {
            // The ray starts just above the terrain: from the sky it would land on roofs and on the water tower tank.
            // (Terrain under the old map is 1.5 m below its floor, so +2.5 m still starts above that floor.)
            Terrain t = Terrain.activeTerrain;
            float from = t != null ? t.SampleHeight(p) + t.transform.position.y + 2.5f : 1000f;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, from, p.z), Vector3.down, out hit, 2000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            return p;
        }

        static bool Sample(Vector3 p, out NavMeshHit hit)
        {
            if (NavMesh.SamplePosition(p, out hit, 4f, NavMesh.AllAreas)) return true;
            Vector3 flat = p; flat.y = 0f;
            return NavMesh.SamplePosition(flat, out hit, 4f, NavMesh.AllAreas);
        }

        static string Corners(Vector3[] c)
        {
            var sb = new StringBuilder();
            int shown = Mathf.Min(c.Length, 10);
            for (int i = 0; i < shown; i++) sb.Append('(').Append(F(c[i].x)).Append(',').Append(F(c[i].z)).Append(')');
            if (c.Length > shown) sb.Append("...+").Append(c.Length - shown);
            return sb.ToString();
        }

        // expectComplete = false only measures (the result line is INFO, not counted).
        bool NavPath(string id, Vector3 from, Vector3 to, out float length, bool expectComplete)
        {
            length = 0f;
            NavMeshHit a, b;
            if (!Sample(from, out a) || !Sample(to, out b))
            {
                if (expectComplete) Log(id, false, "no NavMesh within 4 m of " + (Sample(from, out a) ? "end " + to : "start " + from));
                else Info(id, "no NavMesh near the end points");
                return false;
            }
            var path = new NavMeshPath();
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
            Vector3[] c = path.corners;
            for (int i = 1; i < c.Length; i++) length += Vector3.Distance(c[i - 1], c[i]);
            bool complete = path.status == NavMeshPathStatus.PathComplete;
            string detail = "status=" + path.status + " length=" + F(length) + " corners=" + c.Length + " " + Corners(c);
            if (expectComplete) Log(id, complete, detail); else Info(id, detail);
            return complete;
        }

        // Teleports (as if looking) and waits two frames.
        IEnumerator Stand(Vector3 feet, Vector3 lookAt)
        {
            player.Motor.Teleport(feet);
            Vector3 dir = lookAt - (feet + Vector3.up * 1.65f);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
            player.Motor.QaSetPitch(-Mathf.Asin(Mathf.Clamp(dir.y / dir.magnitude, -1f, 1f)) * Mathf.Rad2Deg);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
        }

        static void Put(ZombieBrain z, Vector3 position)
        {
            var agent = z.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(position); else z.transform.position = position;
            Physics.SyncTransforms();
        }

        // Moves every living zombie onto the old main street, in a grid, out of the routes and the yard.
        static void ParkZombies()
        {
            int i = 0;
            foreach (ZombieBrain z in FindObjectsByType<ZombieBrain>())
            {
                if (z == null || z.IsDead) continue;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(new Vector3(-10f + (i % 5) * 1.6f, 0f, 2f + (i / 5) * 1.6f), out hit, 2f, NavMesh.AllAreas)) Put(z, hit.position);
                i++;
            }
        }

        static float Planar(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static ZombieBrain PickZombie(ZombieBrain not)
        {
            foreach (ZombieBrain z in FindObjectsByType<ZombieBrain>())
                if (z != null && z != not && !z.IsDead) return z;
            return null;
        }

        // ---------- walking ----------

        IEnumerator Walk(string id, Vector3 from, Vector3 to)
        {
            NavMeshHit a, b;
            if (!Sample(from, out a) || !Sample(to, out b)) { Log(id, false, "no NavMesh near start or end"); yield break; }
            var path = new NavMeshPath();
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
            if (path.status != NavMeshPathStatus.PathComplete) { Log(id, false, "path " + path.status); yield break; }
            Vector3[] c = path.corners;
            float plan = 0f;
            for (int i = 1; i < c.Length; i++) plan += Vector3.Distance(c[i - 1], c[i]);
            var controller = player.GetComponent<CharacterController>();
            if (controller == null) { Log(id, false, "player has no CharacterController"); yield break; }

            bool inputWas = player.Motor.InputEnabled;
            player.Motor.InputEnabled = false;
            player.Motor.Teleport(a.position + Vector3.up * 0.1f);
            Physics.SyncTransforms();
            yield return null;

            int steps = 0, stall = 0, corner = 1;
            float best = float.MaxValue, walked = 0f;
            bool stalled = false;
            Vector3 stallAt = Vector3.zero;
            while (corner < c.Length && !stalled && steps < 200000)
            {
                for (int k = 0; k < WalkStepsPerFrame && corner < c.Length; k++)
                {
                    Vector3 pos = player.transform.position;
                    Vector3 d = c[corner] - pos; d.y = 0f;
                    float dist = d.magnitude;
                    float reach = corner == c.Length - 1 ? 0.5f : 0.6f;
                    if (dist < reach) { corner++; best = float.MaxValue; stall = 0; continue; }
                    controller.Move(d / dist * Mathf.Min(WalkStep, dist) + Vector3.down * 0.08f);
                    steps++;
                    Vector3 after = player.transform.position;
                    Vector3 moved = after - pos; moved.y = 0f;
                    walked += moved.magnitude;
                    Vector3 rest = c[corner] - after; rest.y = 0f;
                    float nd = rest.magnitude;
                    if (nd < best - 0.002f) { best = nd; stall = 0; }
                    else if (++stall >= WalkStallSteps) { stalled = true; stallAt = after; break; }
                }
                yield return null;
            }
            player.Motor.InputEnabled = inputWas;
            float end = Planar(player.transform.position, c[c.Length - 1]);
            if (stalled) Log(id, false, "stalled for " + WalkStallSteps + " steps near corner " + corner + "/" + (c.Length - 1) + " at (" + F(stallAt.x) + "," + F(stallAt.y) + "," + F(stallAt.z) + ") walked=" + F(walked) + " of " + F(plan) + " m");
            else Log(id, corner >= c.Length && end < 1.0f, "reached the end steps=" + steps + " walked=" + F(walked) + " m planned=" + F(plan) + " m corners=" + c.Length + " endDistance=" + F(end));
        }

        // ---------- UI and input gate ----------

        IEnumerator UiChecks()
        {
            ResetBag();
            player.Motor.InputEnabled = true;
            player.QaSetAds(true);
            yield return null;
            Log("ui_ads_before", player.AdsHeld, "forced ADS is held with no menu open");
            ps.OpenUi(UiTab.Inventory);
            yield return null; yield return null;
            Log("ui_open", ps.UiOpen && player.UiOpen && !player.Motor.InputEnabled && !player.AdsHeld, "UiOpen=" + ps.UiOpen + " InputEnabled=" + player.Motor.InputEnabled + " adsHeld=" + player.AdsHeld);
            Info("ui_cursor", "visible=" + Cursor.visible + " lock=" + Cursor.lockState);
            interactor.Refresh();
            Log("ui_interactor", interactor.Focus == null && interactor.Label == null && !interactor.QaPress(), "Interactor idle while the screen is open");
            ps.CloseUi();
            yield return null;
            Log("ui_close", !ps.UiOpen && player.Motor.InputEnabled, "after CloseUi InputEnabled=" + player.Motor.InputEnabled);
            player.QaSetAds(false);

            // Esc: closes the screen first, never opens the pause menu on top of it.
            ps.OpenUi(UiTab.Crafting);
            player.QaEscape();
            bool escClosed = !ps.UiOpen && !player.IsPaused && player.Motor.InputEnabled;
            player.QaEscape();
            bool escPause = player.IsPaused;
            player.QaEscape();
            Log("ui_esc", escClosed && escPause && !player.IsPaused && Mathf.Approximately(Time.timeScale, 1f), "Esc closed the screen=" + escClosed + ", next Esc paused=" + escPause + ", then resumed timeScale=" + Time.timeScale);

            // Tab and quick keys are off while paused / dead (state level: OpenUi refuses).
            player.QaSetPaused(true);
            ps.OpenUi(UiTab.Inventory);
            bool refused = !ps.UiOpen;
            player.QaSetPaused(false);
            Log("ui_paused_refuses", refused, "the screen does not open while paused");
            player.Motor.InputEnabled = false; // QA holds the player still
        }

        // ---------- crafting against the real player and scene ----------

        IEnumerator CraftingWorldChecks()
        {
            if (rBandage == null || cloth == null || bandage == null) { Log("craft_world", false, "database items missing"); yield break; }

            // closing the screen stops the craft
            ResetBag();
            ps.Bag.Add(cloth, 4);
            ps.OpenUi(UiTab.Crafting);
            bool started = ps.Crafter.Start(rBandage, 2, Time.time, ps.BenchInRange);
            int reservedWhileBusy = ps.Bag.Reserved(cloth);
            ps.CloseUi();
            Log("craft_menu_close", started && reservedWhileBusy == 4 && !ps.Crafter.Busy && ps.Bag.Reserved(cloth) == 0 && ps.Bag.Count(cloth) == 4 && ps.Crafter.LastMessage == "Menü kapandı",
                "started=" + started + " reservedWhileBusy=" + reservedWhileBusy + " afterClose busy=" + ps.Crafter.Busy + " reserved=" + ps.Bag.Reserved(cloth) + " cloth=" + ps.Bag.Count(cloth));

            // a real-time craft finishes through PlayerSurvival.Update and fills a quick slot
            ResetBag();
            ps.Bag.Add(cloth, 4);
            started = ps.Crafter.Start(rBandage, 1, Time.time, ps.BenchInRange);
            yield return new WaitForSeconds(rBandage.seconds + 0.6f);
            Log("craft_realtime", started && !ps.Crafter.Busy && ps.Bag.Count(bandage) == 1 && ps.Bag.Count(cloth) == 2 && ps.Bag.Reserved(cloth) == 0 && ps.Quick[1] == bandage,
                "bandage=" + ps.Bag.Count(bandage) + " cloth=" + ps.Bag.Count(cloth) + " quick1=" + (ps.Quick[1] != null ? ps.Quick[1].id : "-"));

            // leaving the bench cancels a bench recipe
            if (bench == null) { Log("craft_bench_leave", false, "no Workbench in the scene"); yield break; }
            ResetBag();
            ps.Bag.Add(cloth, 2);
            RecipeDef benchRecipe = MakeRecipe("qa_bench", bandage, 1, 10f, true, A(cloth, 1));
            Vector3 benchPos = bench.transform.position;
            Vector3 away = Ground(benchPos + new Vector3(30f, 0f, 0f));
            yield return Stand(away, benchPos);
            CraftCheck far = ps.Crafter.Check(benchRecipe, 1, ps.BenchInRange);
            bool farBlocked = !ps.BenchInRange && far.block == CraftBlock.NeedsBench;
            Vector3 near = Ground(benchPos + new Vector3(0f, 0f, 1.5f));
            yield return Stand(near, benchPos);
            bool inRange = ps.BenchInRange;
            bool startedBench = ps.Crafter.Start(benchRecipe, 1, Time.time, ps.BenchInRange);
            yield return null;
            bool stillBusy = ps.Crafter.Busy;
            yield return Stand(away, benchPos);
            yield return null; yield return null;
            Log("craft_bench_leave", farBlocked && inRange && startedBench && stillBusy && !ps.Crafter.Busy && ps.Crafter.LastMessage == "Tezgâhtan uzaklaştın" && ps.Bag.Reserved(cloth) == 0 && ps.Bag.Count(cloth) == 2,
                "far blocked=" + farBlocked + " near inRange=" + inRange + " started=" + startedBench + " after leaving: busy=" + ps.Crafter.Busy + " message='" + ps.Crafter.LastMessage + "'");

            // saving cancels the craft first
            ResetBag();
            ps.Bag.Add(cloth, 4);
            ps.Crafter.Start(rBandage, 2, Time.time, ps.BenchInRange);
            bool wasBusy = ps.Crafter.Busy;
            bool saved = SaveSystem.Save("qa_cancel");
            Log("craft_save_cancels", wasBusy && saved && !ps.Crafter.Busy && ps.Bag.Reserved(cloth) == 0 && ps.Bag.Count(cloth) == 4,
                "was busy=" + wasBusy + " saved=" + saved + " afterwards busy=" + ps.Crafter.Busy + " reserved=" + ps.Bag.Reserved(cloth) + " cloth=" + ps.Bag.Count(cloth));
            ResetBag();
        }

        // ---------- bandage ----------

        IEnumerator BandageChecks()
        {
            if (bandage == null) { Log("bandage", false, "bandage item missing"); yield break; }
            ResetBag();
            ps.Bag.Add(bandage, 3);
            ps.AssignQuick(1, bandage);
            player.RestoreHealth(player.MaxHealth);
            bool usedFull = ps.UseQuick(1);
            Log("bandage_full_health", !usedFull && ps.Bag.Count(bandage) == 3 && Mathf.Approximately(player.Health, 100f), "full health: used=" + usedFull + " count=" + ps.Bag.Count(bandage));
            player.RestoreHealth(50f);
            bool used = ps.UseQuick(1);
            Log("bandage_heals", used && Mathf.Abs(player.Health - 75f) < 0.01f && ps.Bag.Count(bandage) == 2, "50 -> " + F(player.Health) + " count=" + ps.Bag.Count(bandage));
            player.RestoreHealth(90f);
            bool used2 = ps.UseQuick(1);
            Log("bandage_max", used2 && Mathf.Abs(player.Health - 100f) < 0.01f && ps.Bag.Count(bandage) == 1, "90 -> " + F(player.Health) + " (max 100) count=" + ps.Bag.Count(bandage));
            player.RestoreHealth(40f);
            ps.Bag.Reserve(bandage, 1);
            bool usedReserved = ps.UseQuick(1);
            Log("bandage_reserved", !usedReserved && ps.Bag.Count(bandage) == 1 && Mathf.Abs(player.Health - 40f) < 0.01f, "the last bandage is reserved by a craft: used=" + usedReserved);
            ps.Bag.ReleaseAll();
            player.RestoreHealth(player.MaxHealth);
            ResetBag();
            yield return null;
        }

        // ---------- melee ----------

        // A runtime wood pile: the trigger sits `triggerOffset` from the pile, the anchor at the pile.
        LootPickup MakePile(string id, Vector3 at, int amount, Vector3 triggerOffset)
        {
            var go = new GameObject(id);
            go.transform.position = at;
            var t = new GameObject("Trigger");
            t.transform.SetParent(go.transform, false);
            t.transform.localPosition = triggerOffset;
            t.layer = Layers.Interactable;
            var box = t.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.8f, 0.8f, 0.3f);
            var pile = go.AddComponent<LootPickup>();
            pile.lootId = id;
            pile.item = wood;
            pile.verb = "Odunu al";
            pile.amount = amount;
            pile.trigger = box;
            return pile;
        }

        IEnumerator HandsChecks()
        {
            if (wood == null) { Log("loot_partial", false, "wood item missing"); yield break; }
            ResetBag();
            Vector3 far = Ground(Yard) + Vector3.up * 40f;

            ItemDef filler = MakeItem("qa_filler2", "Dolgu", 1, ItemKind.Resource);
            LootPickup a = MakePile("qa_pile_a", far, 3, Vector3.zero);
            ps.Bag.Add(filler, ps.Bag.Capacity - 1);
            ps.Bag.Add(wood, Mathf.Max(0, wood.maxStack - 1));
            a.Interact(player);
            Log("loot_partial", ps.Bag.Count(wood) == wood.maxStack && a.Remaining == 2, "bag wood=" + ps.Bag.Count(wood) + " remaining=" + a.Remaining);

            int before = a.Remaining;
            a.OnBlockedPress(player);
            Log("blocked_press", a.Remaining == before && ps.Bag.Count(wood) == wood.maxStack, "remaining=" + a.Remaining + " canInteract=" + a.CanInteract(player));

            ResetBag();
            LootPickup b = MakePile("qa_pile_b", far + Vector3.right * 3f, 3, Vector3.zero);
            b.Interact(player);
            b.Interact(player);
            Log("loot_double_interact", ps.Bag.Count(wood) == 3 && b.Remaining == 0, "bag wood=" + ps.Bag.Count(wood) + " remaining=" + b.Remaining);

            // A trigger poking through a wall must not expose the item behind it.
            Vector3 feet = Ground(Yard);
            Vector3 eye = feet + Vector3.up * 1.65f;
            LootPickup c = MakePile("qa_pile_c", eye + Vector3.forward * 1.8f, 1, Vector3.back * 0.9f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = Layers.Environment;
            wall.transform.position = eye + Vector3.forward * 1.4f;
            wall.transform.localScale = new Vector3(3f, 3f, 0.1f);
            Physics.SyncTransforms();
            yield return Stand(feet, eye + Vector3.forward * 2f);
            interactor.Refresh();
            bool hidden = !ReferenceEquals(interactor.Focus, c);
            wall.SetActive(false);
            Physics.SyncTransforms();
            yield return null;
            interactor.Refresh();
            bool shown = ReferenceEquals(interactor.Focus, c);
            Log("interactor_los", hidden && shown, "behind wall hidden=" + hidden + " clear shown=" + shown);

            Destroy(wall); Destroy(a.gameObject); Destroy(b.gameObject); Destroy(c.gameObject);
            ResetBag();
        }

        IEnumerator MeleeChecks()
        {
            if (bat == null) { Log("melee", false, "bat item missing"); yield break; }
            ZombieBrain z = PickZombie(null);
            if (z == null) { Log("melee", false, "no zombie in the scene"); yield break; }
            ResetBag();
            ps.Bag.Add(bat, 1);
            ps.AssignQuick(1, bat);
            bool equipped = ps.UseQuick(1);
            ViewmodelPresenter pres = player.GetComponentInChildren<ViewmodelPresenter>();
            Log("melee_equip", equipped && player.MeleeEquipped && ps.ActiveQuick == 1 && (pres == null || !pres.PistolVisible),
                "equipped=" + player.MeleeEquipped + " activeQuick=" + ps.ActiveQuick + " pistolHidden=" + (pres == null ? "n/a" : (!pres.PistolVisible).ToString()));

            Vector3 feet = Ground(Yard);
            NavMeshHit h;
            if (Sample(feet, out h)) feet = h.position;
            // Far swing (3 m): no damage.
            Vector3 look;
            if (!PlaceInFront(z, feet, 3.0f, out look)) { Log("melee_far", false, "could not place a zombie 3 m ahead in the yard"); yield break; }
            yield return Stand(feet, look);
            float h0 = z.Health;
            int landed0 = player.MeleeLanded;
            bool swung = player.QaMeleeSwing();
            yield return new WaitForSeconds(1.0f);
            Log("melee_far", swung && Mathf.Approximately(z.Health, h0) && player.MeleeLanded == landed0, "zombie 3.0 m away: swing=" + swung + " health " + F(h0) + " -> " + F(z.Health));

            // Near swing (1.5 m): hits.
            if (!PlaceInFront(z, feet, 1.5f, out look)) { Log("melee_near", false, "could not place a zombie 1.5 m ahead in the yard"); yield break; }
            yield return Stand(feet, look);
            h0 = z.Health;
            swung = player.QaMeleeSwing();
            yield return new WaitForSeconds(1.0f);
            Log("melee_near", swung && z.Health < h0 && player.MeleeLanded == landed0 + 1, "zombie 1.5 m away: swing=" + swung + " health " + F(h0) + " -> " + F(z.Health) + " landed=" + player.MeleeLanded + " hit=" + player.LastMeleeResult + " zombie=" + z.name + " at " + z.transform.position + " look=" + look + " feet=" + feet);

            // Back to the pistol, and a bat that leaves the bag cannot stay in hand.
            ps.UseQuick(0);
            bool pistol = !player.MeleeEquipped && (pres == null || pres.PistolVisible);
            ps.UseQuick(1);
            ps.Bag.TryRemove(bat, 1);
            bool dropped = !player.MeleeEquipped && ps.ActiveQuick == 0;
            Log("melee_unequip", pistol && dropped && (pres == null || pres.PistolVisible), "pistol back=" + pistol + " bat removed from the bag -> pistol=" + dropped);
            ResetBag();
        }

        // Puts the zombie `distance` m ahead of the player on open ground (tries 8 directions). `look` = chest height of the zombie.
        bool PlaceInFront(ZombieBrain z, Vector3 feet, float distance, out Vector3 look)
        {
            look = feet + Vector3.forward * distance;
            for (int k = 0; k < 8; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(feet + dir * distance, out hit, 0.6f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(Planar(hit.position, feet) - distance) > 0.25f) continue;
                Vector3 chest = hit.position + Vector3.up * 1.3f;
                if (Physics.Linecast(feet + Vector3.up * 1.3f, chest, Layers.SightMask, QueryTriggerInteraction.Ignore)) continue;
                Put(z, hit.position);
                z.transform.rotation = Quaternion.LookRotation(-dir);
                Physics.SyncTransforms();
                look = hit.position + Vector3.up * 1.3f;
                return Mathf.Abs(Planar(z.transform.position, feet) - distance) < 0.4f;
            }
            return false;
        }

        // ---------- distraction stone ----------

        IEnumerator StoneChecks()
        {
            ZombieBrain z = PickZombie(null);
            if (z == null) { Log("stone", false, "no zombie in the scene"); yield break; }

            // Test points near the yard: zombie at A, stone lands at B (7 m away), the player is far away so nothing is chased.
            Vector3 centre = Ground(Yard);
            NavMeshHit ha, hb;
            Vector3 a = Vector3.zero, b = Vector3.zero;
            bool found = false;
            if (Sample(centre, out ha))
            {
                Vector3[] offsets = { new Vector3(7f, 0f, 0f), new Vector3(-7f, 0f, 0f), new Vector3(0f, 0f, 7f), new Vector3(0f, 0f, -7f) };
                foreach (Vector3 o in offsets)
                {
                    if (!NavMesh.SamplePosition(ha.position + o, out hb, 1.5f, NavMesh.AllAreas)) continue;
                    float dd = Planar(ha.position, hb.position);
                    if (dd < 5.5f || dd > 8f) continue;
                    if (Physics.Linecast(ha.position + Vector3.up * 1.6f, hb.position + Vector3.up * 1.6f, Layers.SightMask, QueryTriggerInteraction.Ignore)) continue;
                    a = ha.position; b = hb.position; found = true; break;
                }
            }
            if (!found) { Log("stone_setup", false, "no clear 7 m pair of NavMesh points near the yard"); yield break; }

            Vector3 faraway = Ground(Hut);
            yield return Stand(faraway, faraway + Vector3.forward);
            Put(z, a);
            z.transform.rotation = Quaternion.LookRotation(a - b);
            player.QaSetFreeze(false);
            yield return new WaitForSeconds(0.3f);

            ThrownStone st = ThrownStone.Spawn(ps.stonePrefab, b + Vector3.up * 1.2f, Vector3.zero, 16f, null);
            float limit = Time.realtimeSinceStartup + 4f;
            while (!st.Emitted && Time.realtimeSinceStartup < limit) yield return null;
            yield return new WaitForSeconds(0.3f);
            float d0 = Planar(z.transform.position, b);
            bool investigating = z.State == ZombieState.Investigate;
            yield return new WaitForSeconds(3.0f);
            float d1 = Planar(z.transform.position, b);
            Log("stone_investigate", st.Emitted && investigating && (d1 < d0 - 1f || d1 < 2f),
                "stone emitted=" + st.Emitted + " zombie state=" + (investigating ? "Investigate" : z.State.ToString()) + " distance to the point " + F(d0) + " -> " + F(d1));

            // Arrival: waits about 3 s and goes back to wandering (no chase).
            limit = Time.realtimeSinceStartup + 16f;
            while (z.State == ZombieState.Investigate && Time.realtimeSinceStartup < limit) yield return null;
            Log("stone_ends", z.State != ZombieState.Investigate && z.State != ZombieState.Chase, "after arriving the zombie is " + z.State);

            // Outside the radius nothing happens.
            bool placedFar = false;
            Vector3[] farOffsets = { new Vector3(22f, 0f, 0f), new Vector3(-22f, 0f, 0f), new Vector3(0f, 0f, 22f), new Vector3(0f, 0f, -22f), new Vector3(30f, 0f, 0f) };
            foreach (Vector3 o in farOffsets)
            {
                NavMeshHit hf;
                if (!NavMesh.SamplePosition(b + o, out hf, 2f, NavMesh.AllAreas)) continue;
                if (Planar(hf.position, b) < 18f) continue;
                Put(z, hf.position);
                z.transform.rotation = Quaternion.LookRotation(hf.position - b);
                placedFar = true;
                break;
            }
            yield return null; yield return null;
            ZombieState before = z.State;
            NoiseEvents.EmitDistraction(b, 16f);
            yield return null;
            Log("stone_out_of_range", placedFar && z.State != ZombieState.Investigate, "zombie " + F(Planar(z.transform.position, b)) + " m away (radius 16): " + before + " -> " + z.State);
            player.QaSetFreeze(true);
            yield return null;
        }

        // ---------- quest flow ----------

        IEnumerator QuestFlow()
        {
            Log("quest_start", quest.State == QuestState.SearchFuse && quest.LightEventCount == 0 && !fuse.Taken && !panel.Installed,
                "state=" + quest.State + " lightEvents=" + quest.LightEventCount + " objective='" + quest.ObjectiveText + "' heading='" + quest.ObjectiveHeading + "'");
            Log("quest_objective_text", quest.ObjectiveHeading == "İLK İKMAL" && !quest.HasObjectiveMarker, "heading=" + quest.ObjectiveHeading + " marker=" + quest.HasObjectiveMarker);
            bool taken = fuse.CanInteract(player);
            fuse.Interact(player);
            Log("quest_fuse", taken && quest.State == QuestState.CarryingFuse && fuse.Taken, "state=" + quest.State);
            bool canInstall = panel.CanInteract(player);
            panel.Interact(player);
            yield return null;
            bool lit = LightsLit();
            Log("quest_install", canInstall && quest.State == QuestState.ExploreStation && quest.LightEventCount == 1 && lit && panel.Installed,
                "state=" + quest.State + " lightEvents=" + quest.LightEventCount + " lightsLit=" + lit + " objective='" + quest.ObjectiveText + "'");
            Log("quest_doors", (gate == null || (gate.IsOpen && !gate.locked)) && (serviceDoor == null || !serviceDoor.locked),
                "gate open=" + (gate != null && gate.IsOpen) + " serviceLocked=" + (serviceDoor != null && serviceDoor.locked));
            Log("quest_result", PlaytestLog.LastResultLine != null && PlaytestLog.LastResultLine.StartsWith("PLAYTEST_RESULT result=complete"), "log line " + PlaytestLog.LastResultLine);
            Log("quest_markers", quest.HasObjectiveMarker && quest.ObjectiveHeading == "DIŞ BÖLGE" && quest.ObjectiveMarker == quest.stationMarker, "heading=" + quest.ObjectiveHeading + " marker=" + quest.ObjectiveMarker);

            // A second install must not repeat anything.
            int completeMarks = CountQuestMarks("Complete");
            bool again = quest.InstallFuse();
            panel.Interact(player);
            Log("quest_once", !again && quest.LightEventCount == 1 && quest.State == QuestState.ExploreStation && CountQuestMarks("Complete") == completeMarks && completeMarks == 1,
                "second InstallFuse=" + again + " lightEvents=" + quest.LightEventCount + " completeMarks=" + CountQuestMarks("Complete"));
        }

        static int CountQuestMarks(string state)
        {
            int n = 0;
            foreach (PlaytestLog.QuestMark m in PlaytestLog.QuestTimes) if (m.state == state) n++;
            return n;
        }

        bool LightsLit()
        {
            bool lit = true;
            if (quest.panelLamp != null) lit &= quest.panelLamp.enabled;
            if (quest.mainLight != null) lit &= quest.mainLight.enabled;
            if (quest.emergencyLight != null) lit &= !quest.emergencyLight.enabled;
            if (quest.benchLamp != null) lit &= quest.benchLamp.enabled;
            if (quest.yardLamps != null) foreach (Light l in quest.yardLamps) if (l != null) lit &= l.enabled;
            return lit;
        }

        // ---------- station route (v0.8): lure, supply cabinet, reward ----------

        IEnumerator StationAndYard()
        {
            ItemDef cacheItem = ps.database != null ? ps.database.FindItem("supply_cache") : null;
            RecipeDef rLure = ps.database != null ? ps.database.FindRecipe("lure") : null;
            if (cloth == null || stone == null || lure == null || bandage == null || cacheItem == null || rLure == null)
            {
                Log("station_loot", false, "cloth / stone / lure / bandage / supply_cache item or the lure recipe is missing");
                yield break;
            }
            ResetBag();
            Log("route_explore", quest.State == QuestState.ExploreStation && quest.ObjectiveText == "İstasyona git: taş ve kumaş topla" && quest.HasObjectiveMarker && quest.ObjectiveMarker == quest.stationMarker,
                "state=" + quest.State + " objective='" + quest.ObjectiveText + "' marker=" + quest.ObjectiveMarker);

            LootPickup clothPile = null, stonePile = null, cachePile = null;
            float bestD = float.MaxValue;
            Vector3 sc = quest.stationBounds.center;
            foreach (LootPickup l in FindObjectsByType<LootPickup>())
            {
                if (l.lootId == "loot_supply_cache") cachePile = l;
                else if (l.lootId == "loot_rear_stone_1") stonePile = l;
                if (l.item == null || l.item.id != QuestFuse.ClothId || l.Remaining <= 0) continue;
                float dist = Planar(l.transform.position, sc);
                if (dist < bestD) { bestD = dist; clothPile = l; }
            }
            if (clothPile == null || stonePile == null || cachePile == null)
            {
                Log("station_loot", false, "cloth pile=" + (clothPile != null) + " loot_rear_stone_1=" + (stonePile != null) + " loot_supply_cache=" + (cachePile != null));
                yield break;
            }
            int before = ps.Bag.Count(cloth);

            // Full bag: blocked label, nothing is taken.
            ItemDef filler = MakeItem("qa_filler", "Dolgu", 1, ItemKind.Resource);
            ps.Bag.Add(filler, ps.Bag.Capacity);
            bool blocked = !clothPile.CanInteract(player) && clothPile.BlockedLabel(player) == "Çantada yer yok";
            ps.Bag.Clear();
            Log("loot_bag_full", blocked, "full bag: CanInteract=" + clothPile.CanInteract(player) + " label='" + clothPile.BlockedLabel(player) + "'");

            // Cloth alone keeps the quest at the station (the lure needs a stone too) and shows the bandage hint once.
            int pileBefore = clothPile.Remaining;
            Vector3 inside = Ground(new Vector3(sc.x, 0f, sc.z));
            yield return Stand(inside, inside + Vector3.forward);
            clothPile.Interact(player);
            yield return null; yield return null;
            int got = ps.Bag.Count(cloth) - before;
            Log("station_loot", got == pileBefore && clothPile.Remaining == 0 && !clothPile.CanInteract(player) && quest.State == QuestState.ExploreStation && quest.BandageIntroShown,
                "took " + got + " of " + pileBefore + " state=" + quest.State + " intro=" + quest.BandageIntroShown + " objective='" + quest.ObjectiveText + "'");
            clothPile.Interact(player);
            Log("loot_once", ps.Bag.Count(cloth) - before == got, "a second press adds nothing");

            // A stone as well: craft the lure.
            int stoneBefore = ps.Bag.Count(stone);
            stonePile.Interact(player);
            yield return null; yield return null;
            Log("route_craft_state", ps.Bag.Count(stone) > stoneBefore && quest.State == QuestState.CraftLure && quest.ObjectiveText == "Dikkat taşı üret — " + InputLabels.Key(InputLabels.Inventory)
                && quest.HasObjectiveMarker && quest.ObjectiveMarker == quest.stationMarker,
                "state=" + quest.State + " objective='" + quest.ObjectiveText + "' marker=" + quest.ObjectiveMarker);
            bool started = ps.Crafter.Start(rLure, 1, Time.time, ps.BenchInRange);
            yield return new WaitForSeconds(rLure.seconds + 0.6f);
            yield return null;
            Log("route_retrieve_state", started && ps.Bag.Count(lure) == 1 && quest.State == QuestState.RetrieveCache && quest.ObjectiveText == "Dükkândan erzak çantasını al"
                && quest.cacheMarker.sqrMagnitude > 0f && quest.ObjectiveMarker == quest.cacheMarker,
                "started=" + started + " lure=" + ps.Bag.Count(lure) + " state=" + quest.State + " objective='" + quest.ObjectiveText + "' marker=" + quest.ObjectiveMarker);

            yield return CabinetChecks(cachePile, cacheItem);
            Log("route_return_state", quest.State == QuestState.ReturnToYard && quest.ObjectiveText == "Erzak çantasını avluya getir" && quest.ObjectiveMarker == quest.yardMarker,
                "state=" + quest.State + " objective='" + quest.ObjectiveText + "' marker=" + quest.ObjectiveMarker);

            // Back in the yard: the quest ends once, the reward is paid once.
            int bandBefore = ps.Bag.Count(bandage) + ps.Storage.Count(bandage);
            int reserveBefore = player.Weapon.Reserve;
            Vector3 yc = Ground(new Vector3(quest.yardBounds.center.x, 0f, quest.yardBounds.center.z));
            yield return Stand(yc, yc + Vector3.forward);
            yield return new WaitForSeconds(0.6f);
            Log("yard_return", quest.State == QuestState.FreeRoam && !quest.HasObjectiveMarker && quest.ObjectiveText == "Bölgeyi keşfet" && File.Exists(SaveSystem.FilePath),
                "state=" + quest.State + " objective='" + quest.ObjectiveText + "' autosave file=" + File.Exists(SaveSystem.FilePath));
            int bandGain = ps.Bag.Count(bandage) + ps.Storage.Count(bandage) - bandBefore;
            Log("reward_first_trip", bandGain == 2 && player.Weapon.Reserve == reserveBefore + 12 && ps.Bag.Count(cacheItem) == 0,
                "bandages +" + bandGain + " reserve " + reserveBefore + " -> " + player.Weapon.Reserve + " supply bags left=" + ps.Bag.Count(cacheItem));

            // Leaving and coming back pays nothing more.
            int bandNow = ps.Bag.Count(bandage) + ps.Storage.Count(bandage);
            int reserveNow = player.Weapon.Reserve;
            Vector3 away = Ground(Station);
            yield return Stand(away, away + Vector3.forward);
            yield return new WaitForSeconds(0.3f);
            yield return Stand(yc, yc + Vector3.forward);
            yield return new WaitForSeconds(0.6f);
            Log("reward_once", quest.State == QuestState.FreeRoam && ps.Bag.Count(bandage) + ps.Storage.Count(bandage) == bandNow && player.Weapon.Reserve == reserveNow,
                "after leaving and returning: bandages " + bandNow + " -> " + (ps.Bag.Count(bandage) + ps.Storage.Count(bandage)) + " reserve " + reserveNow + " -> " + player.Weapon.Reserve);
        }

        // The supply bag sits in the shop cabinet: hidden while the door is closed, usable once it is open, taken exactly once.
        IEnumerator CabinetChecks(LootPickup cachePile, ItemDef cacheItem)
        {
            GameObject cabGo = GameObject.Find("Cache_Cabinet");
            DoorInteractable cab = cabGo != null ? cabGo.GetComponent<DoorInteractable>() : null;
            if (cab == null) { Log("cabinet_los", false, "Cache_Cabinet with a DoorInteractable is missing"); yield break; }
            Vector3 feet = Ground(cab.transform.TransformPoint(new Vector3(0f, 0.05f, -0.95f)));
            Vector3 look = cachePile.hintAnchor != null ? cachePile.hintAnchor.position : cachePile.transform.position;
            yield return Stand(feet, look);
            interactor.Refresh();
            string closedLabel = interactor.Label ?? "";
            string closedTitle = interactor.Title ?? "";
            bool closedHidden = !cab.IsOpen && !ReferenceEquals(interactor.Focus, cachePile) && !closedLabel.Contains("Erzak") && !closedTitle.Contains("Erzak");
            cab.ForceOpen(true);
            Physics.SyncTransforms();
            yield return null; yield return null;
            interactor.Refresh();
            bool openShown = cab.IsOpen && ReferenceEquals(interactor.Focus, cachePile) && interactor.CanUse && interactor.Label != null && interactor.Label.Contains("Erzak");
            Log("cabinet_los", closedHidden && openShown, "closed: hidden=" + closedHidden + " label='" + closedLabel + "' | open: label='" + (interactor.Label ?? "") + "' title='" + (interactor.Title ?? "") + "' focusIsBag=" + ReferenceEquals(interactor.Focus, cachePile));

            int c0 = ps.Bag.Count(cacheItem);
            cachePile.Interact(player);
            int c1 = ps.Bag.Count(cacheItem);
            cachePile.Interact(player);
            int c2 = ps.Bag.Count(cacheItem);
            yield return null; yield return null;
            Log("cache_once", c1 == c0 + 1 && c2 == c1 && cachePile.Remaining == 0 && !cachePile.CanInteract(player) && quest.State == QuestState.ReturnToYard,
                "supply bags " + c0 + " -> " + c1 + " -> " + c2 + " remaining=" + cachePile.Remaining + " state=" + quest.State);
        }

        // ---------- station routes (v0.8) ----------

        // NavMesh path through consecutive points; false when any leg is incomplete.
        bool ChainPath(Vector3[] pts, out List<Vector3> corners, out string why)
        {
            corners = new List<Vector3>();
            why = null;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                NavMeshHit a, b;
                if (!Sample(pts[i], out a) || !Sample(pts[i + 1], out b)) { why = "no NavMesh near leg " + i + " (" + F(pts[i].x) + "," + F(pts[i].z) + ") -> (" + F(pts[i + 1].x) + "," + F(pts[i + 1].z) + ")"; return false; }
                var path = new NavMeshPath();
                NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
                if (path.status != NavMeshPathStatus.PathComplete) { why = "leg " + i + " path " + path.status; return false; }
                Vector3[] c = path.corners;
                for (int k = corners.Count == 0 ? 0 : 1; k < c.Length; k++) corners.Add(c[k]);
            }
            return corners.Count > 1;
        }

        static float PathLength(List<Vector3> c)
        {
            float len = 0f;
            for (int i = 1; i < c.Count; i++) len += Vector3.Distance(c[i - 1], c[i]);
            return len;
        }

        IEnumerator RouteChecks()
        {
            Vector3[] rear = quest.rearRoute, front = quest.frontRoute, spots = quest.stationZombieSpots;
            bool data = rear != null && rear.Length >= 2 && front != null && front.Length >= 2 && spots != null && spots.Length > 0;
            Log("route_data", data, "rear=" + (rear != null ? rear.Length : 0) + " front=" + (front != null ? front.Length : 0) + " zombie spots=" + (spots != null ? spots.Length : 0));
            if (!data) yield break;
            List<Vector3> fc, rc;
            string why;
            bool frontOk = ChainPath(front, out fc, out why);
            Log("route_front", frontOk, frontOk ? "road -> forecourt -> shop front door -> cabinet: NavMesh complete, length " + F(PathLength(fc)) + " m" : why);
            bool rearOk = ChainPath(rear, out rc, out why);
            if (!rearOk) { Log("route_rear", false, "NavMesh: " + why); yield break; }
            ParkZombies();
            yield return WalkRoute("route_rear", rc, rear, spots);
        }

        // Walks the corners with the CharacterController and, about once per metre, checks that every station zombie spot is blocked from seeing the player.
        // The station frame is rebuilt from the first two route points (local +x runs from point 0 to point 1; point 0 is local (4, -22)).
        IEnumerator WalkRoute(string id, List<Vector3> c, Vector3[] route, Vector3[] spots)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller == null) { Log(id, false, "player has no CharacterController"); yield break; }
            Vector2 right = new Vector2(route[1].x - route[0].x, route[1].z - route[0].z).normalized;
            Vector2 fwd = new Vector2(-right.y, right.x);
            Vector2 origin = new Vector2(route[0].x, route[0].z) - right * 4f + fwd * 22f;

            bool inputWas = player.Motor.InputEnabled;
            player.Motor.InputEnabled = false;
            player.Motor.Teleport(c[0] + Vector3.up * 0.1f);
            Physics.SyncTransforms();
            yield return null;

            int steps = 0, stall = 0, corner = 1, samples = 0, closedAll = 0, outer = 0, closedOuter = 0;
            float best = float.MaxValue, walked = 0f, sinceSample = 99f;
            bool stalled = false;
            Vector3 stallAt = Vector3.zero, firstOpen = Vector3.zero;
            while (corner < c.Count && !stalled && steps < 200000)
            {
                for (int k = 0; k < WalkStepsPerFrame && corner < c.Count; k++)
                {
                    Vector3 pos = player.transform.position;
                    Vector3 d = c[corner] - pos; d.y = 0f;
                    float dist = d.magnitude;
                    float reach = corner == c.Count - 1 ? 0.5f : 0.6f;
                    if (dist < reach) { corner++; best = float.MaxValue; stall = 0; continue; }
                    controller.Move(d / dist * Mathf.Min(WalkStep, dist) + Vector3.down * 0.08f);
                    steps++;
                    Vector3 after = player.transform.position;
                    Vector3 moved = after - pos; moved.y = 0f;
                    walked += moved.magnitude;
                    sinceSample += moved.magnitude;
                    Vector3 rest = c[corner] - after; rest.y = 0f;
                    float nd = rest.magnitude;
                    if (nd < best - 0.002f) { best = nd; stall = 0; }
                    else if (++stall >= WalkStallSteps) { stalled = true; stallAt = after; break; }

                    if (sinceSample >= 1f)
                    {
                        sinceSample = 0f;
                        Physics.SyncTransforms();
                        Vector2 rel = new Vector2(after.x, after.z) - origin;
                        float lx = Vector2.Dot(rel, right), lz = Vector2.Dot(rel, fwd);
                        bool isOuter = (lz < -15.5f && lx >= 6.5f) || lx > 32.3f; // outside the fence line and beyond the south fence's shadow
                        bool allClosed = true;
                        foreach (Vector3 spot in spots)
                        {
                            Vector3 from = spot + Vector3.up * 1.6f, to = after + Vector3.up * 1.2f, dir = to - from;
                            RaycastHit hit;
                            bool seen = Physics.Raycast(from, dir.normalized, out hit, dir.magnitude + 0.1f, Layers.SightMask, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<Sandbox>() == player;
                            if (seen) { allClosed = false; break; }
                        }
                        samples++;
                        if (allClosed) closedAll++;
                        if (isOuter)
                        {
                            outer++;
                            if (allClosed) closedOuter++;
                            else if (firstOpen == Vector3.zero) firstOpen = after;
                        }
                    }
                }
                yield return null;
            }
            player.Motor.InputEnabled = inputWas;
            float end = Planar(player.transform.position, c[c.Count - 1]);
            string ratio = "sight closed on " + closedOuter + "/" + outer + " outside samples, " + closedAll + "/" + samples + " overall";
            if (stalled) Log(id, false, "stalled near corner " + corner + "/" + (c.Count - 1) + " at (" + F(stallAt.x) + "," + F(stallAt.y) + "," + F(stallAt.z) + ") walked=" + F(walked) + " m; " + ratio);
            else Log(id, corner >= c.Count && end < 1.0f && outer > 0 && closedOuter == outer,
                "reached=" + (corner >= c.Count && end < 1.0f) + " walked=" + F(walked) + " m; " + ratio + (firstOpen != Vector3.zero ? "; first visible outside sample at (" + F(firstOpen.x) + "," + F(firstOpen.z) + ")" : ""));
        }

        // ---------- crafting stage 2: bench kits, barricade, chest ----------

        static bool Needs(RecipeDef r, ItemDef d, int n)
        {
            if (r == null || r.inputs == null) return false;
            foreach (ItemAmount a in r.inputs) if (a.item == d && a.count == n) return true;
            return false;
        }

        static bool VisualOn(BuildSlot s) { Transform t = s.transform.Find("BuiltVisual"); return t != null && t.gameObject.activeSelf; }

        static bool ObstacleOn(BuildSlot s)
        {
            Transform t = s.transform.Find("NavBlock");
            NavMeshObstacle o = t != null ? t.GetComponent<NavMeshObstacle>() : null;
            return o != null && o.enabled;
        }

        static bool VisualHasEnvironmentCollider(BuildSlot s)
        {
            Transform t = s.transform.Find("BuiltVisual");
            if (t == null) return false;
            Collider c = t.GetComponentInChildren<Collider>(true);
            return c != null && c.gameObject.layer == Layers.Environment;
        }

        IEnumerator BuildChecks()
        {
            ItemDatabase db = ps.database;
            if (db == null) { Log("build", false, "database missing"); yield break; }
            ItemDef kitB = db.FindItem("barricade_kit"), kitC = db.FindItem("chest_kit"), kitR = db.FindItem("repair_kit");
            RecipeDef rRepair = db.FindRecipe("repair_kit"), rBarr = db.FindRecipe("barricade_kit"), rChest = db.FindRecipe("chest_kit");
            BuildSlot barricade = BuildSlot.Find("barricade_service"), gateSlot = BuildSlot.Find("barricade_gate"), chest = BuildSlot.Find("chest_yard");
            bool items = kitB != null && kitC != null && kitR != null && scrap != null && wood != null && tape != null && bat != null;
            bool recipes = rRepair != null && rBarr != null && rChest != null;
            if (!items || !recipes) { Log("build_data", false, "kits=" + items + " recipes=" + recipes); yield break; }
            Log("build_data", rRepair.available && rBarr.available && rChest.available && rRepair.needsBench && rBarr.needsBench && rChest.needsBench
                && rRepair.output == kitR && rBarr.output == kitB && rChest.output == kitC && rRepair.seconds == 6f && rBarr.seconds == 8f && rChest.seconds == 8f
                && Needs(rRepair, scrap, 3) && Needs(rRepair, tape, 1) && Needs(rBarr, wood, 5) && Needs(rBarr, scrap, 2) && Needs(rChest, wood, 6) && Needs(rChest, scrap, 2)
                && kitR.kind == ItemKind.Kit && kitB.kind == ItemKind.Kit && kitC.kind == ItemKind.Kit && kitR.maxStack == 3 && kitB.maxStack == 2 && kitC.maxStack == 1,
                "available + bench-only, costs and times match the design");
            Log("build_slots", barricade != null && gateSlot != null && chest != null && BuildSlot.All.Count == 3
                && barricade.Kind == BuildKind.Barricade && gateSlot.Kind == BuildKind.Barricade && chest.Kind == BuildKind.Chest
                && !barricade.Built && !gateSlot.Built && !chest.Built && !VisualOn(barricade) && !ObstacleOn(barricade) && ps.Storage.Capacity == PlayerSurvival.StorageSlots,
                "slots=" + BuildSlot.All.Count + " storage=" + ps.Storage.Capacity);
            if (barricade == null || gateSlot == null || chest == null) yield break;

            // an old save file has no buildSlots array and still parses
            SaveData old = JsonUtility.FromJson<SaveData>("{\"version\":1,\"quest\":4}");
            Log("save_old_format", old != null && old.version == 1 && old.quest == 4 && (old.buildSlots == null || old.buildSlots.Length == 0), "a save without buildSlots parses; slots are left as the scene made them");

            // ---- bench recipes use the real bench range ----
            ResetBag();
            Log("kit_not_usable", !ps.TryUseBandage(kitR) && !ps.TryUseBandage(kitB) && !ps.TryUseBandage(kitC) && ps.Bag.Count(kitR) == 0, "kits have no heal action");
            if (bench != null)
            {
                ps.Bag.Add(scrap, 3); ps.Bag.Add(tape, 1);
                Vector3 benchPos = bench.transform.position;
                Vector3 away = Ground(benchPos + new Vector3(30f, 0f, 0f));
                Vector3 near = Ground(benchPos + new Vector3(0f, 0f, 1.5f));
                yield return Stand(away, benchPos);
                CraftCheck far = ps.Crafter.Check(rRepair, 1, ps.BenchInRange);
                bool startedFar = ps.Crafter.Start(rRepair, 1, Time.time, ps.BenchInRange);
                bool refused = !ps.BenchInRange && far.block == CraftBlock.NeedsBench && far.message == "Tezgâh gerekli" && !startedFar && !ps.Crafter.Busy && ps.Bag.Reserved(scrap) == 0;
                yield return Stand(near, benchPos);
                bool startedNear = ps.Crafter.Start(rRepair, 1, Time.time, ps.BenchInRange);
                yield return null;
                bool busy = ps.Crafter.Busy && ps.Bag.Reserved(scrap) == 3 && ps.Bag.Reserved(tape) == 1;
                yield return Stand(away, benchPos);
                yield return null; yield return null;
                Log("bench_recipe_range", refused && startedNear && busy && !ps.Crafter.Busy && ps.Crafter.LastMessage == "Tezgâhtan uzaklaştın" && ps.Bag.Reserved(scrap) == 0 && ps.Bag.Count(scrap) == 3 && ps.Bag.Count(tape) == 1 && ps.Bag.Count(kitR) == 0,
                    "far refused=" + refused + " near started=" + startedNear + " busy=" + busy + " after leaving busy=" + ps.Crafter.Busy + " message='" + ps.Crafter.LastMessage + "' scrap=" + ps.Bag.Count(scrap));
                yield return Stand(near, benchPos);
                bool started = ps.Crafter.Start(rRepair, 1, Time.time, ps.BenchInRange);
                yield return new WaitForSeconds(rRepair.seconds + 0.6f);
                Log("bench_recipe_craft", started && !ps.Crafter.Busy && ps.Bag.Count(kitR) == 1 && ps.Bag.Count(scrap) == 0 && ps.Bag.Count(tape) == 0 && ps.Quick[1] == null && ps.Quick[2] == null && ps.Quick[3] == null,
                    "started=" + started + " busy=" + ps.Crafter.Busy + " bench=" + ps.BenchInRange + " msg='" + ps.Crafter.LastMessage + "' repair kit=" + ps.Bag.Count(kitR) + " scrap=" + ps.Bag.Count(scrap) + " tape=" + ps.Bag.Count(tape) + " (a kit takes no quick slot)");
            }
            else Log("bench_recipe_range", false, "no Workbench in the scene");

            // ---- barricade install ----
            ResetBag();
            if (serviceDoor != null) serviceDoor.ForceOpen(true);
            yield return new WaitForSeconds(2.5f);
            Vector3 outsideP = Ground(new Vector3(47f, 0f, -22f)), yardP = Ground(new Vector3(38f, 0f, -22f));
            float len;
            bool openBefore = NavPath("nav_barricade_open_probe", outsideP, yardP, out len, false);

            Vector3 standP = Ground(new Vector3(41.6f, 0f, -21.2f));
            Vector3 aim = new Vector3(43.4f, 1.0f, -21.1f);
            yield return Stand(standP, aim);
            interactor.Refresh();
            bool noKit = !barricade.CanInteract(player) && barricade.BlockedLabel(player) == "Barikat kiti gerekli" && ReferenceEquals(interactor.Focus, barricade) && interactor.Label == "Barikat kiti gerekli";
            ps.Bag.Add(kitB, 2);
            interactor.Refresh();
            bool prompt = ReferenceEquals(interactor.Focus, barricade) && interactor.CanUse && interactor.Label != null && interactor.Label.Contains("Barikat kur");
            bool press1 = interactor.QaPress();
            bool press2 = interactor.QaPress();
            barricade.Interact(player); // a direct second use cannot consume a kit either
            bool installed = press1 && !press2 && barricade.Built && ps.Bag.Count(kitB) == 1 && Mathf.Approximately(barricade.Health, BuildSlot.MaxHealth);
            bool visual = VisualOn(barricade) && ObstacleOn(barricade) && VisualHasEnvironmentCollider(barricade);
            if (!barricade.Built) barricade.Interact(player); // keep the later checks meaningful
            Log("barricade_install", noKit && prompt && installed && visual,
                "no-kit label=" + noKit + " prompt=" + prompt + " press1=" + press1 + " press2=" + press2 + " kits left=" + ps.Bag.Count(kitB) + " health=" + F(barricade.Health) + " visual+obstacle+collider=" + visual);

            // The yard has two exits: only with the gate barricaded too is the service barricade the sole way in.
            gateSlot.ForceState(true, BuildSlot.MaxHealth);
            yield return new WaitForSeconds(2.5f);
            bool closedPath = NavPath("nav_barricade_closed_probe", outsideP, yardP, out len, false);
            Log("barricade_carves", openBefore && !closedPath, "service passage -> yard complete before=" + openBefore + " after the barricade=" + closedPath);

            // ---- a zombie with no route breaks it ----
            ZombieBrain z = PickZombie(null);
            NavMeshHit zh;
            if (z == null || !Sample(outsideP, out zh)) Log("zombie_breaks_barricade", false, "no zombie or no NavMesh behind the barricade");
            else
            {
                yield return Stand(yardP, yardP + Vector3.right * 5f);
                Put(z, zh.position);
                z.transform.rotation = Quaternion.LookRotation(Vector3.left);
                float hp0 = barricade.Health;
                player.QaSetFreeze(false);
                yield return null;
                z.Alert();
                bool sawBreak = false;
                float limit = Time.realtimeSinceStartup + 5.5f;
                while (Time.realtimeSinceStartup < limit)
                {
                    if (z.State == ZombieState.BreakBarricade) sawBreak = true;                    yield return null;
                }
                player.QaSetFreeze(true);
                float hp1 = barricade.Health;
                Log("zombie_breaks_barricade", sawBreak && hp1 < hp0 && barricade.Built, "zombie behind the barricade: saw BreakBarricade=" + sawBreak + " barricade health " + F(hp0) + " -> " + F(hp1) + " state=" + z.State);
                NavMeshHit hh;
                if (Sample(Ground(Hut), out hh)) Put(z, hh.position); // out of the way
            }

            // ---- repair ----
            ResetBag();
            barricade.ForceState(true, 70f);
            yield return Stand(standP, aim);
            interactor.Refresh();
            bool noRepair = !barricade.CanInteract(player) && barricade.BlockedLabel(player) == "Tamir kiti gerekli" && interactor.Label == "Tamir kiti gerekli";
            ps.Bag.Add(kitR, 2);
            bool rep1 = interactor.QaPress();
            bool capped = Mathf.Approximately(barricade.Health, BuildSlot.MaxHealth) && ps.Bag.Count(kitR) == 1;
            bool rep2 = interactor.QaPress();
            bool fullClean = !barricade.CanInteract(player) && barricade.BlockedLabel(player) == null && interactor.Label == null;
            barricade.Interact(player); // direct use at full health consumes nothing
            Log("barricade_repair_cap", noRepair && rep1 && capped && !rep2 && fullClean && ps.Bag.Count(kitR) == 1,
                "no kit label=" + noRepair + " 70 + 60 capped at " + F(barricade.Health) + " kits left=" + ps.Bag.Count(kitR) + " second press=" + rep2 + " clean at full health=" + fullClean);
            barricade.ForceState(true, 20f);
            bool rep3 = interactor.QaPress();
            Log("barricade_repair_amount", rep3 && Mathf.Abs(barricade.Health - 80f) < 0.01f && ps.Bag.Count(kitR) == 0, "20 + 60 = " + F(barricade.Health) + " kits left=" + ps.Bag.Count(kitR));

            // ---- destroyed: reopens, kit is lost ----
            ps.Bag.Add(kitB, 1);
            barricade.ForceState(true, 5f);
            barricade.ApplyDamage(10f);
            bool fell = !barricade.Built && !VisualOn(barricade) && !ObstacleOn(barricade) && ps.Bag.Count(kitB) == 1;
            barricade.ApplyDamage(10f); // nothing to damage any more
            yield return new WaitForSeconds(2.5f);
            bool reopened = NavPath("nav_barricade_destroyed_probe", outsideP, yardP, out len, false);
            Log("barricade_destroyed_reopens", fell && !barricade.Built && reopened, "fell=" + fell + " passage complete again=" + reopened);
            gateSlot.ForceState(false, 0f);

            // rebuild and leave it damaged for the save checks
            yield return Stand(standP, aim);
            bool rebuilt = interactor.QaPress() && barricade.Built && ps.Bag.Count(kitB) == 0;
            barricade.ApplyDamage(45f);
            qaBarricadeBuilt = barricade.Built;
            qaBarricadeHealth = barricade.Health;
            Log("barricade_rebuild", rebuilt && Mathf.Abs(qaBarricadeHealth - 55f) < 0.01f, "rebuilt=" + rebuilt + " health after 45 damage=" + F(qaBarricadeHealth));

            // ---- chest ----
            ResetBag();
            int capBefore = ps.Storage.Capacity;
            Vector3 standC = Ground(new Vector3(27.4f, 0f, -35.5f));
            Vector3 aimC = new Vector3(25.6f, 0.9f, -35.5f);
            yield return Stand(standC, aimC);
            interactor.Refresh();
            bool noKitC = !chest.CanInteract(player) && chest.BlockedLabel(player) == "Sandık kiti gerekli" && ReferenceEquals(interactor.Focus, chest) && interactor.Label == "Sandık kiti gerekli";
            ps.Bag.Add(kitC, 1);
            interactor.Refresh();
            bool promptC = ReferenceEquals(interactor.Focus, chest) && interactor.CanUse && interactor.Label != null && interactor.Label.Contains("Sandık kur");
            bool c1 = interactor.QaPress();
            bool builtC = chest.Built && VisualOn(chest) && VisualHasEnvironmentCollider(chest) && ps.Bag.Count(kitC) == 0 && ps.Storage.Capacity == PlayerSurvival.StorageSlots + BuildSlot.ChestExtraSlots;
            bool c2 = interactor.QaPress(); // built: the press now opens the storage, it cannot build twice
            bool opened = c2 && ps.UiOpen && ps.OpenStorage != null && ps.Bag.Count(kitC) == 0;
            ps.CloseUi();
            player.Motor.InputEnabled = false; // QA holds the player still
            int added = ps.Storage.Add(bat, 30);
            Log("chest_install", noKitC && promptC && c1 && builtC && capBefore == 24 && opened,
                "no-kit label=" + noKitC + " prompt=" + promptC + " built=" + builtC + " capacity " + capBefore + " -> " + ps.Storage.Capacity + " opens storage=" + opened + " 30 single-slot items fit=" + (added == 30 && ps.Storage.UsedSlots == 30));
            Log("chest_capacity_36", added == 30 && ps.Storage.UsedSlots == 30 && ps.Storage.Capacity == 36, "storage used=" + ps.Storage.UsedSlots + " / " + ps.Storage.Capacity);
            qaChestBuilt = chest.Built;
            ResetBag();
        }

        // ---------- save and reload ----------

        sealed class Snap
        {
            public QuestState quest;
            public Dictionary<string, int> bag, store, loot;
            public string quick1;
            public HashSet<string> openDoors;
            public Vector3 pos;
            public float yaw, health;
            public int ammo, reserve;
            public bool fuseTaken, panelInstalled, crateUsed, lightsLit;
        }

        static Dictionary<string, int> Counts(Inventory inv)
        {
            var d = new Dictionary<string, int>();
            ItemStack[] s = inv.Snapshot();
            foreach (ItemStack st in s)
            {
                if (st.IsEmpty) continue;
                int n; d.TryGetValue(st.item.id, out n); d[st.item.id] = n + st.count;
            }
            return d;
        }

        Snap Capture()
        {
            var s = new Snap();
            s.quest = quest.State;
            s.bag = Counts(ps.Bag); s.store = Counts(ps.Storage);
            s.quick1 = ps.Quick[1] != null ? ps.Quick[1].id : "";
            s.loot = new Dictionary<string, int>();
            foreach (LootPickup l in FindObjectsByType<LootPickup>()) s.loot[l.lootId] = l.Remaining;
            s.openDoors = new HashSet<string>();
            foreach (DoorInteractable d in FindObjectsByType<DoorInteractable>()) if (d.IsOpen) s.openDoors.Add(d.name);
            s.pos = player.transform.position; s.yaw = player.transform.eulerAngles.y;
            s.health = player.Health; s.ammo = player.Weapon.Ammo; s.reserve = player.Weapon.Reserve;
            s.fuseTaken = fuse.Taken; s.panelInstalled = panel.Installed; s.crateUsed = crate.Used; s.lightsLit = LightsLit();
            return s;
        }

        static string Dict(Dictionary<string, int> d)
        {
            var keys = new List<string>(d.Keys);
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            foreach (string k in keys) sb.Append(k).Append('=').Append(d[k]).Append(' ');
            return sb.ToString().Trim();
        }

        // First difference between the saved world and the live one, or null.
        string Diff(Snap s)
        {
            Snap n = Capture();
            if (n.quest != s.quest) return "quest " + s.quest + " -> " + n.quest;
            if (Dict(n.bag) != Dict(s.bag)) return "bag [" + Dict(s.bag) + "] -> [" + Dict(n.bag) + "]";
            if (Dict(n.store) != Dict(s.store)) return "storage [" + Dict(s.store) + "] -> [" + Dict(n.store) + "]";
            if (n.quick1 != s.quick1) return "quick1 '" + s.quick1 + "' -> '" + n.quick1 + "'";
            if (Dict(n.loot) != Dict(s.loot)) return "loot differs";
            if (!n.openDoors.SetEquals(s.openDoors)) return "open doors differ (" + n.openDoors.Count + " vs " + s.openDoors.Count + ")";
            if (Planar(n.pos, s.pos) > 1.5f) return "position " + s.pos + " -> " + n.pos;
            if (Mathf.Abs(Mathf.DeltaAngle(n.yaw, s.yaw)) > 3f) return "yaw " + F(s.yaw) + " -> " + F(n.yaw);
            if (Mathf.Abs(n.health - s.health) > 0.01f) return "health " + F(s.health) + " -> " + F(n.health);
            if (n.ammo != s.ammo || n.reserve != s.reserve) return "ammo " + s.ammo + "/" + s.reserve + " -> " + n.ammo + "/" + n.reserve;
            if (n.fuseTaken != s.fuseTaken || n.panelInstalled != s.panelInstalled || n.crateUsed != s.crateUsed) return "fuse/panel/crate flags differ";
            if (n.lightsLit != s.lightsLit) return "lights " + s.lightsLit + " -> " + n.lightsLit;
            return null;
        }

        IEnumerator SaveReloadChecks()
        {
            if (cloth == null || wood == null || scrap == null || bat == null || tape == null || bandage == null) { Log("save", false, "database items missing"); yield break; }
            // A state worth saving: items in bag and storage, a quick slot, ammo box used, health, a rotated player.
            ps.Crafter.Cancel("qa");
            ps.Bag.Clear(); ps.Storage.Clear();
            ps.Bag.Add(cloth, 7); ps.Bag.Add(wood, 12); ps.Bag.Add(bat, 1); ps.Bag.Add(bandage, 2);
            ps.Storage.Add(scrap, 9); ps.Storage.Add(tape, 3);
            int storedBats = ps.Storage.Add(bat, 26); // single-slot items: reaches slot 27, beyond the old 24 slots (needs the chest capacity)
            ps.AssignQuick(1, bandage); ps.AssignQuick(2, bat);
            if (!crate.Used) crate.Interact(player);
            if (serviceDoor != null && !serviceDoor.IsOpen) serviceDoor.Interact(player);
            player.Weapon.QaSetAmmo(7);
            player.Weapon.QaSetReserve(37);
            player.RestoreHealth(63f);
            Vector3 yc = Ground(new Vector3(quest.yardBounds.center.x, 0f, quest.yardBounds.center.z));
            yield return Stand(yc, yc + new Vector3(1f, 0f, 1f));
            yield return new WaitForSeconds(0.3f);

            Snap saved = Capture();
            bool wrote = SaveSystem.Save("qa_manual");
            Log("save_write", wrote && SaveSystem.HasSave && File.Exists(SaveSystem.FilePath) && !File.Exists(SaveSystem.FilePath + ".tmp"), "saved=" + wrote + " file=" + SaveSystem.FilePath);
            Log("save_state", saved.quest == QuestState.FreeRoam && saved.lightsLit && saved.openDoors.Count >= 1, "quest=" + saved.quest + " lightsLit=" + saved.lightsLit + " openDoors=" + saved.openDoors.Count + " bag=[" + Dict(saved.bag) + "] storage=[" + Dict(saved.store) + "]");

            // Change things, then reload the scene: the save must come back, nothing multiplies.
            ps.Bag.Clear();
            player.RestoreHealth(10f);
            yield return Reload();
            if (player == null) { Log("save_reload", false, "scene objects missing after reload"); yield break; }
            yield return new WaitForSeconds(1.0f);
            Log("save_loaded", WorldState.Instance != null && WorldState.Instance.LoadedFromSave, "WorldState.LoadedFromSave=" + (WorldState.Instance != null && WorldState.Instance.LoadedFromSave));
            string diff = Diff(saved);
            Log("save_reload", diff == null, diff == null ? "inventory, storage, quick slot, loot, doors, lights, quest, position, health and ammo are back" : "difference: " + diff);
            Log("save_no_duplicates", Dict(Counts(ps.Bag)) == Dict(saved.bag) && Dict(Counts(ps.Storage)) == Dict(saved.store), "bag=[" + Dict(Counts(ps.Bag)) + "] storage=[" + Dict(Counts(ps.Storage)) + "]");
            Log("save_loot_gone", AllLootStaysGone(saved), "piles taken before the save are still gone");
            Log("save_quest_lights", quest.State == QuestState.FreeRoam && quest.LightEventCount == 1 && LightsLit() && (gate == null || gate.IsOpen), "state=" + quest.State + " lightEvents=" + quest.LightEventCount + " lights=" + LightsLit());

            // build slots: chest capacity first (items in slots 24..35 survive), barricade health and visuals come back
            BuildSlot rb = BuildSlot.Find("barricade_service"), rc = BuildSlot.Find("chest_yard"), rg = BuildSlot.Find("barricade_gate");
            bool slotsBack = rb != null && rc != null && rg != null && rb.Built == qaBarricadeBuilt && Mathf.Abs(rb.Health - qaBarricadeHealth) < 0.01f && rc.Built == qaChestBuilt && !rg.Built;
            bool visualsBack = rb != null && rc != null && VisualOn(rb) == rb.Built && ObstacleOn(rb) == rb.Built && VisualOn(rc) == rc.Built;
            bool capacityBack = ps.Storage.Capacity == PlayerSurvival.StorageSlots + BuildSlot.ChestExtraSlots && storedBats == 26 && ps.Storage.Capacity > 27 && ps.Storage[27].item == bat;
            Log("save_build_slots", qaChestBuilt && qaBarricadeBuilt && slotsBack && visualsBack && capacityBack,
                "barricade built=" + (rb != null && rb.Built) + " health=" + (rb != null ? F(rb.Health) : "-") + " (saved " + F(qaBarricadeHealth) + ") chest built=" + (rc != null && rc.Built)
                + " visuals=" + visualsBack + " storage capacity=" + ps.Storage.Capacity + " slot 27 item=" + (ps.Storage.Capacity > 27 && !ps.Storage[27].IsEmpty ? ps.Storage[27].item.id : "-"));
            yield return null;
        }

        bool AllLootStaysGone(Snap saved)
        {
            foreach (LootPickup l in FindObjectsByType<LootPickup>())
            {
                int rem;
                if (!saved.loot.TryGetValue(l.lootId, out rem)) continue;
                if (rem <= 0 && (l.Remaining > 0 || l.CanInteract(player))) return false;
            }
            return true;
        }

        // Scene reload as the death path does it; waits for the new scene's objects.
        IEnumerator Reload()
        {
            Sandbox before = player;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (before != null && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Acquire();
        }

        // ---------- death ----------

        IEnumerator DeathChecks()
        {
            if (cloth == null || rBandage == null) { Log("death", false, "database items missing"); yield break; }
            Snap saved = Capture(); // equals the file written above
            ps.Bag.Add(cloth, 4); // changes since the save must not survive the reload
            ps.OpenUi(UiTab.Crafting);
            bool started = ps.Crafter.Start(rBandage, 2, Time.time, ps.BenchInRange);
            player.QaSetFreeze(true);
            int deathsBefore = PlaytestLog.Deaths;
            Sandbox before = player;
            player.ApplyDamage(new DamageInfo { Amount = 1000f, Zone = HitZone.Body, Point = player.transform.position, Direction = Vector3.forward, Source = this });
            yield return null; yield return null;
            Log("death_closes_ui", started && player.IsDead && !ps.UiOpen && !ps.Crafter.Busy && ps.Bag.Reserved(cloth) == 0,
                "dead=" + player.IsDead + " uiOpen=" + ps.UiOpen + " crafting=" + ps.Crafter.Busy + " reserved=" + ps.Bag.Reserved(cloth));
            float deadline = Time.realtimeSinceStartup + 15f;
            while (before != null && Time.realtimeSinceStartup < deadline) yield return null; // the scene reloads 2 s after death
            yield return Acquire();
            if (player == null) { Log("death_reload", false, "scene objects missing after the death reload"); yield break; }
            yield return new WaitForSeconds(1.0f);
            string diff = Diff(saved);
            Log("death_reload", PlaytestLog.Deaths == deathsBefore + 1 && !player.IsDead && diff == null && WorldState.Instance != null && WorldState.Instance.LoadedFromSave,
                "deaths=" + PlaytestLog.Deaths + " loadedFromSave=" + (WorldState.Instance != null && WorldState.Instance.LoadedFromSave) + (diff == null ? " last save applied" : " difference: " + diff));
        }
    }
}
