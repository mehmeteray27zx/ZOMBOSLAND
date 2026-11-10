using System.Collections.Generic;
using UnityEngine;

namespace Zombosland
{
    // Scene component (IlkIkmal). Knows the loot registry and gathers / applies the save. Zombies are not saved; looted piles stay looted.
    [DefaultExecutionOrder(200)]
    public class WorldState : MonoBehaviour
    {
        public static WorldState Instance { get; private set; }

        // Loot registry is static so LootPickup.Awake can register before this component exists.
        static readonly Dictionary<string, LootPickup> registry = new Dictionary<string, LootPickup>();
        static readonly Dictionary<string, int> marked = new Dictionary<string, int>();

        public static void Register(LootPickup p)
        {
            if (p == null) return;
            if (string.IsNullOrEmpty(p.lootId))
            {
                Debug.LogWarning("LOOT empty lootId on " + p.name);
                return;
            }
            LootPickup existing;
            if (registry.TryGetValue(p.lootId, out existing) && existing != null && existing != p
                && existing.gameObject.scene == p.gameObject.scene)
            {
                Debug.LogError("LOOT duplicate lootId '" + p.lootId + "' on " + p.name + " and " + existing.name);
                return;
            }
            registry[p.lootId] = p;
        }

        public static void Unregister(LootPickup p)
        {
            if (p == null || string.IsNullOrEmpty(p.lootId)) return;
            LootPickup existing;
            if (registry.TryGetValue(p.lootId, out existing) && existing == p) registry.Remove(p.lootId);
        }

        // Called by a pickup whenever its remaining amount changes.
        public static void MarkLoot(string id, int remaining)
        {
            if (!string.IsNullOrEmpty(id)) marked[id] = remaining;
        }

        public static LootPickup FindLoot(string id)
        {
            LootPickup p;
            return id != null && registry.TryGetValue(id, out p) && p != null ? p : null;
        }

        public static int MarkedRemaining(string id, int fallback)
        {
            int n;
            return id != null && marked.TryGetValue(id, out n) ? n : fallback;
        }

        public static List<LootPickup> AllLoot()
        {
            var list = new List<LootPickup>();
            foreach (KeyValuePair<string, LootPickup> kv in registry) if (kv.Value != null) list.Add(kv.Value);
            return list;
        }

        Sandbox player;
        QuestFuse quest;
        DoorInteractable[] doors;
        AmmoCrate[] crates;
        FuseItem fuse;
        FusePanel panel;

        public bool LoadedFromSave { get; private set; }
        public bool CanSave { get { return player != null && !player.IsDead; } }

        void Awake()
        {
            if (BootRouter.IsAbandoned(gameObject)) return;
            Instance = this;
            marked.Clear();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (Instance != this) return;
            player = FindAnyObjectByType<Sandbox>();
            quest = QuestFuse.Instance;
            doors = FindObjectsByType<DoorInteractable>();
            crates = FindObjectsByType<AmmoCrate>();
            fuse = FindAnyObjectByType<FuseItem>();
            panel = FindAnyObjectByType<FusePanel>();
            SaveData data = SaveSystem.TryLoad();
            if (data == null) return;
            Apply(data);
            LoadedFromSave = true;
            Debug.Log("SAVE loaded quest=" + (QuestState)data.quest + " bag=" + CountEntries(data.bagIds) + " storage=" + CountEntries(data.storageIds)
                + " doors=" + data.openDoors.Length + " loot=" + data.lootIds.Length);
        }

        static int CountEntries(string[] ids)
        {
            int n = 0;
            if (ids != null) foreach (string s in ids) if (!string.IsNullOrEmpty(s)) n++;
            return n;
        }

        // ---------- capture ----------

        public SaveData Capture()
        {
            var d = new SaveData();
            if (player != null)
            {
                Vector3 p = player.transform.position;
                d.px = p.x; d.py = p.y; d.pz = p.z;
                d.yaw = player.transform.eulerAngles.y;
                d.health = player.Health;
                d.ammo = player.Weapon.Ammo;
                d.reserve = player.Weapon.Reserve;
            }
            if (quest != null)
            {
                d.quest = (int)quest.State;
                d.bandageIntro = quest.BandageIntroShown;
            }
            var open = new List<string>();
            if (doors != null) foreach (DoorInteractable door in doors) if (door != null && door.IsOpen) open.Add(door.gameObject.name);
            d.openDoors = open.ToArray();
            d.fuseTaken = fuse != null && fuse.Taken;
            d.panelInstalled = panel != null && panel.Installed;
            var used = new List<string>();
            if (crates != null) foreach (AmmoCrate c in crates) if (c != null && c.Used) used.Add(c.gameObject.name);
            d.usedAmmoCrates = used.ToArray();

            PlayerSurvival ps = PlayerSurvival.Instance;
            if (ps != null && ps.Bag != null)
            {
                ToArrays(ps.Bag.Snapshot(), out d.bagIds, out d.bagCounts);
                ToArrays(ps.Storage.Snapshot(), out d.storageIds, out d.storageCounts);
                d.quickIds = new string[PlayerSurvival.QuickSlots];
                for (int i = 0; i < d.quickIds.Length; i++) d.quickIds[i] = ps.Quick != null && i < ps.Quick.Length && ps.Quick[i] != null ? ps.Quick[i].id : "";
            }

            var ids = new List<string>();
            var remaining = new List<int>();
            foreach (KeyValuePair<string, LootPickup> kv in registry)
            {
                if (kv.Value == null) continue;
                ids.Add(kv.Key);
                remaining.Add(kv.Value.Remaining);
            }
            d.lootIds = ids.ToArray();
            d.lootRemaining = remaining.ToArray();

            var slots = new List<BuildSlotSave>();
            foreach (BuildSlot slot in BuildSlot.All)
                if (slot != null && !string.IsNullOrEmpty(slot.SlotId))
                    slots.Add(new BuildSlotSave { slotId = slot.SlotId, built = slot.Built, health = slot.Health });
            d.buildSlots = slots.ToArray();
            return d;
        }

        static void ToArrays(ItemStack[] slots, out string[] ids, out int[] counts)
        {
            ids = new string[slots.Length];
            counts = new int[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                bool has = !slots[i].IsEmpty && !string.IsNullOrEmpty(slots[i].item.id);
                ids[i] = has ? slots[i].item.id : "";
                counts[i] = has ? slots[i].count : 0;
            }
        }

        // ---------- apply ----------

        void Apply(SaveData d)
        {
            // Doors, fuse and ammo box: visuals and state without events.
            var open = new HashSet<string>(d.openDoors);
            if (doors != null)
                foreach (DoorInteractable door in doors)
                    if (door != null && open.Contains(door.gameObject.name)) door.ForceOpen(true);
            if (d.fuseTaken && fuse != null) fuse.ForceTaken();
            if (d.panelInstalled && panel != null) panel.ForceInstalled();
            var used = new HashSet<string>(d.usedAmmoCrates);
            if (crates != null)
                foreach (AmmoCrate c in crates)
                    if (c != null && used.Contains(c.gameObject.name)) c.ForceUsed();

            // Loot.
            for (int i = 0; i < d.lootIds.Length; i++)
            {
                LootPickup pile = FindLoot(d.lootIds[i]);
                if (pile != null) pile.SetRemaining(d.lootRemaining[i]);
            }

            // Build slots first: a built chest raises the storage capacity before the storage items are loaded.
            // A save without the array (older file) leaves the slots as the scene made them.
            if (d.buildSlots != null)
                foreach (BuildSlotSave s in d.buildSlots)
                {
                    BuildSlot slot = s != null ? BuildSlot.Find(s.slotId) : null;
                    if (slot != null) slot.ForceState(s.built, s.health);
                }

            // Inventory.
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (ps != null && ps.Bag != null)
            {
                if (ps.database == null) Debug.LogWarning("SAVE: PlayerSurvival.database missing, inventory not restored");
                else
                {
                    var quick = new ItemDef[PlayerSurvival.QuickSlots];
                    for (int i = 0; i < quick.Length && d.quickIds != null && i < d.quickIds.Length; i++) quick[i] = ps.database.FindItem(d.quickIds[i]);
                    ps.RestoreFromSave(ToStacks(ps.database, d.bagIds, d.bagCounts), ToStacks(ps.database, d.storageIds, d.storageCounts), quick);
                }
            }

            // Player.
            if (player != null)
            {
                player.Weapon.RestoreForSave(d.ammo, d.reserve);
                player.RestoreHealth(d.health);
                Vector3 pos = new Vector3(d.px, d.py, d.pz);
                if (player.Motor != null) player.Motor.Teleport(pos); else player.transform.position = pos;
                player.transform.rotation = Quaternion.Euler(0f, d.yaw, 0f);
                player.ResetSafePoint(pos);
            }

            // Quest last: it applies lights and unlocks without running the event.
            if (quest != null)
            {
                quest.RestoreState((QuestState)d.quest);
                quest.RestoreBandageIntro(d.bandageIntro);
            }
        }

        static ItemStack[] ToStacks(ItemDatabase db, string[] ids, int[] counts)
        {
            var stacks = new ItemStack[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i]) || counts[i] <= 0) continue;
                ItemDef item = db.FindItem(ids[i]);
                if (item == null) { Debug.LogWarning("SAVE: unknown item id '" + ids[i] + "' dropped"); continue; }
                stacks[i] = new ItemStack { item = item, count = counts[i] };
            }
            return stacks;
        }
    }
}
