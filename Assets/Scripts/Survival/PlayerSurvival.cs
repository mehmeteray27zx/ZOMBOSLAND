using UnityEngine;

namespace Zombosland
{
    public enum UiTab { Inventory, Crafting }

    // Player component: bag, storage, crafting, quick slots and the state of the inventory / crafting screen.
    // The screen itself is drawn by the HUD; this class only exposes state. CombatSandbox has no PlayerSurvival.
    public class PlayerSurvival : MonoBehaviour
    {
        public static PlayerSurvival Instance;

        public ItemDatabase database;
        public GameObject meleeView;
        public ThrownStone stonePrefab;

        public const int BagSlots = 24, StorageSlots = 24, QuickSlots = 4;
        const float ThrowCooldown = 0.6f, ThrowSpeed = 13f, DefaultLureRadius = 16f;

        public Inventory Bag, Storage;
        public Crafter Crafter;
        public ItemDef[] Quick; // Quick[0] is always null = pistol (fixed equipment, takes no bag space); 1..3 are item references
        public bool UiOpen;
        public UiTab Tab;
        public StorageBox OpenStorage;
        public bool BenchInRange;
        public int ActiveQuick;

        Sandbox sandbox;
        float nextThrow;
        bool axeGiveDone;
        bool wasCrafting;

        void Awake()
        {
            if (BootRouter.IsAbandoned(gameObject)) return;
            Instance = this;
            Bag = new Inventory(BagSlots);
            Storage = new Inventory(StorageSlots);
            Crafter = new Crafter(Bag);
            Quick = new ItemDef[QuickSlots];
            Crafter.UnitCompleted += OnUnitCompleted;
            Crafter.Cancelled += OnCancelled;
            Bag.Changed += OnBagChanged;
        }

        void Start() { sandbox = GetComponent<Sandbox>(); }

        void OnDestroy()
        {
            if (Bag != null) Bag.Changed -= OnBagChanged;
            if (Crafter != null)
            {
                Crafter.UnitCompleted -= OnUnitCompleted;
                Crafter.Cancelled -= OnCancelled;
                Crafter.Cancel("Sahne kapandı");
            }
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Bag == null || sandbox == null) return;
            if (!axeGiveDone) { axeGiveDone = true; GiveAxeIfRequested(); } // first Update: after every Start, so a loaded save does not overwrite it
            BenchInRange = Workbench.AnyInRange(transform.position);
            if (sandbox.IsDead)
            {
                if (UiOpen) CloseUi();
                Crafter.Cancel("Öldün");
                SyncCrafting();
                return;
            }
            Crafter.Tick(Time.time, BenchInRange);
            SyncCrafting();
            if (sandbox.PerfMode || sandbox.IsPaused) return;

            if (Input.GetKeyDown(InputLabels.InventoryKey))
            {
                if (UiOpen) CloseUi(); else OpenUi(UiTab.Inventory);
            }
            else if (!UiOpen && Input.GetKeyDown(InputLabels.HolsterKey)) sandbox.ToggleHolster();
            else if (!UiOpen)
            {
                for (int i = 0; i < QuickSlots; i++)
                    if (Input.GetKeyDown(InputLabels.QuickKey(i))) { UseQuick(i); break; }
            }
        }

        // Tells the hands when the craft loop starts or ends.
        void SyncCrafting()
        {
            bool busy = Crafter.Busy;
            if (busy == wasCrafting) return;
            wasCrafting = busy;
            sandbox.NotifyCrafting(busy);
        }

        // --give-axe (manual trial only): one axe in the bag if there is none, on quick slot 2. Not equipped.
        void GiveAxeIfRequested()
        {
            if (!QaArgs.Has("--give-axe") || database == null) return;
            ItemDef axe = database.FindItem("axe");
            if (axe == null) { Debug.LogWarning("--give-axe: item 'axe' missing from the database (rebuild the scene assets)"); return; }
            if (Bag.Count(axe) <= 0) Bag.Add(axe, 1);
            AssignQuick(2, axe);
        }

        // ---------- inventory / crafting screen ----------

        public void OpenUi(UiTab tab, StorageBox storage = null)
        {
            if (Bag == null || sandbox == null || sandbox.IsDead || sandbox.IsPaused || sandbox.PerfMode) return;
            Tab = tab;
            OpenStorage = storage;
            if (UiOpen) return;
            UiOpen = true;
            sandbox.ApplyUiState(true);
        }

        // Closing the screen stops a running craft.
        public void CloseUi()
        {
            if (!UiOpen) return;
            UiOpen = false;
            OpenStorage = null;
            if (Crafter != null) Crafter.Cancel("Menü kapandı");
            if (sandbox != null) sandbox.ApplyUiState(false);
        }

        // ---------- quick slots ----------

        public void AssignQuick(int slot, ItemDef d)
        {
            if (Quick == null || slot < 1 || slot >= QuickSlots) return; // slot 0 is the pistol
            if (d != null)
            {
                if (d.kind != ItemKind.Consumable && d.kind != ItemKind.Throwable && d.kind != ItemKind.Melee) return;
                for (int i = 1; i < QuickSlots; i++) if (Quick[i] == d) Quick[i] = null; // an item sits in one slot only
            }
            Quick[slot] = d;
            if (d == null && ActiveQuick == slot) SelectPistol();
        }

        public bool UseQuick(int slot)
        {
            if (Quick == null || sandbox == null || sandbox.IsDead || sandbox.IsPaused || sandbox.PerfMode) return false;
            if (slot < 0 || slot >= QuickSlots) return false;
            if (sandbox.HandsBusy) return false; // input during a hand action is dropped, not queued
            if (slot == 0) { SelectPistol(); return true; }
            ItemDef d = Quick[slot];
            if (d == null) return false;
            if (Bag.Available(d) <= 0) { Notifier.Show(NameOf(d) + " yok", 1.5f); return false; }
            switch (d.kind)
            {
                case ItemKind.Melee:
                    ActiveQuick = slot;
                    sandbox.SetMelee(d);
                    return true;
                case ItemKind.Consumable:
                    if (Sandbox.InstantHands) return TryUseBandage(d);
                    if (d.healAmount <= 0f) return false;
                    if (sandbox.Health >= sandbox.MaxHealth - 0.001f) { Notifier.Show("Can dolu", 1.5f); return false; } // before the clip, so it is not played for nothing
                    return sandbox.TryBeginBandage(d);
                case ItemKind.Throwable:
                    return !UiOpen && TryThrow(d);
                default:
                    return false;
            }
        }

        void SelectPistol()
        {
            ActiveQuick = 0;
            if (sandbox != null) sandbox.SetMelee(null);
        }

        // Heals by item.healAmount (max health 100). Not consumed at full health. Reserved (being crafted) items cannot be used.
        public bool TryUseBandage(ItemDef d)
        {
            if (d == null || Bag == null || sandbox == null || sandbox.IsDead || d.healAmount <= 0f) return false;
            if (Bag.Available(d) <= 0) return false;
            if (sandbox.Health >= sandbox.MaxHealth - 0.001f) { Notifier.Show("Can dolu", 1.5f); return false; }
            if (!Bag.TryRemove(d, 1)) return false;
            sandbox.Heal(d.healAmount);
            Notifier.Show(NameOf(d) + " kullanıldı", 1.5f);
            return true;
        }

        // Throws one item along the aim direction (distraction stone).
        public bool TryThrow(ItemDef d)
        {
            if (d == null || Bag == null || sandbox == null || sandbox.AimPivot == null || sandbox.IsDead || sandbox.HandsBusy) return false;
            if (Time.time < nextThrow || Bag.Available(d) <= 0) return false;
            if (!Bag.TryRemove(d, 1)) return false;
            nextThrow = Time.time + ThrowCooldown;
            Transform aim = sandbox.AimPivot;
            Vector3 velocity = aim.forward * ThrowSpeed + Vector3.up * 1.5f;
            float radius = d.noiseRadius > 0f ? d.noiseRadius : DefaultLureRadius;
            ThrownStone.Spawn(stonePrefab, aim.position + aim.forward * 0.5f, velocity, radius, GetComponentsInChildren<Collider>());
            return true;
        }

        // ---------- events ----------

        public void NotifyPickup(ItemDef d, int n)
        {
            if (d == null || n <= 0) return;
            PickupFeed.Add(d, n);
            if (QuestFuse.Instance != null) QuestFuse.Instance.NotifyLoot(d, n);
        }

        void OnUnitCompleted(RecipeDef r)
        {
            if (r == null || r.output == null) return;
            Notifier.Show("Üretildi: " + NameOf(r.output) + (r.outputCount > 1 ? " x" + r.outputCount : ""), 2.5f);
            AutoAssign(r.output);
        }

        void OnCancelled(string reason)
        {
            if (reason == "Menü kapandı" || reason == "Kayıt alındı" || reason == "Öldün" || reason == "Sahne kapandı" || reason == "Kayıt yüklendi") return; // silent reasons
            Notifier.Show(reason, 2.5f);
        }

        // A crafted usable item goes to the first free quick slot (1..3).
        void AutoAssign(ItemDef d)
        {
            if (d == null || Quick == null) return;
            if (d.kind != ItemKind.Consumable && d.kind != ItemKind.Throwable && d.kind != ItemKind.Melee) return;
            for (int i = 1; i < QuickSlots; i++) if (Quick[i] == d) return;
            for (int i = 1; i < QuickSlots; i++) if (Quick[i] == null) { Quick[i] = d; return; }
        }

        // A bat that left the bag cannot stay in hand.
        void OnBagChanged()
        {
            if (sandbox != null && sandbox.MeleeEquipped && Bag.Count(sandbox.MeleeItem) <= 0) SelectPistol();
        }

        static string NameOf(ItemDef d) { return string.IsNullOrEmpty(d.displayName) ? d.name : d.displayName; }

        // ---------- save ----------

        public void RestoreFromSave(ItemStack[] bag, ItemStack[] storage, ItemDef[] quick)
        {
            if (Bag == null) return;
            if (sandbox != null) sandbox.CancelHandAction("Kayıt yüklendi");
            CloseUi();
            Crafter.Cancel("Kayıt yüklendi");
            Bag.Load(bag);
            Storage.Load(storage);
            Quick = new ItemDef[QuickSlots];
            if (quick != null)
                for (int i = 1; i < QuickSlots && i < quick.Length; i++) Quick[i] = quick[i];
            SelectPistol();
        }
    }
}
