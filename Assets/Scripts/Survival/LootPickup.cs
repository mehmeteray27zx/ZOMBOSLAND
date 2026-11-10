using UnityEngine;

namespace Zombosland
{
    // A pile of resources in the world. Interacting moves it to the bag (partly when the bag is nearly full).
    // The hands perform the pickup; Interact is the single atomic commit.
    public class LootPickup : MonoBehaviour, IHandInteractable, IFocusTitle, IBlockedLabel, IBlockedPress
    {
        public string lootId, verb;
        public ItemDef item;
        public int amount = 1;
        public GameObject visual;
        public Collider trigger;
        public Transform hintAnchor;

        int remaining;
        bool initialized;

        public int Remaining { get { Init(); return remaining; } }
        public bool Collected { get { return Remaining <= 0; } }
        public string Verb { get { return string.IsNullOrEmpty(verb) ? "Topla" : verb; } }
        public Transform HintAnchor { get { return hintAnchor != null ? hintAnchor : transform; } }
        public bool BlockedBriefly { get { return false; } }
        public HandActionKind HandKind { get { return HandActionKind.Pickup; } }
        public bool RequiresLineOfSight { get { return true; } }
        public string Title { get { return item == null ? null : UiTheme.Name(item) + " ×" + Remaining; } }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            remaining = Mathf.Max(0, amount);
        }

        void Awake()
        {
            if (BootRouter.IsAbandoned(gameObject)) return;
            Init();
            WorldState.Register(this);
        }

        void OnDestroy() { WorldState.Unregister(this); }

        public bool CanInteract(Sandbox p)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            return Remaining > 0 && item != null && ps != null && ps.Bag != null && ps.Bag.FreeSpaceFor(item) > 0;
        }

        public string BlockedLabel(Sandbox p)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (Remaining <= 0 || item == null || ps == null || ps.Bag == null) return null;
            return ps.Bag.FreeSpaceFor(item) <= 0 ? "Çantada yer yok" : null;
        }

        // E pressed while the bag cannot take any of it: message and deny sound, nothing else changes.
        public void OnBlockedPress(Sandbox p)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (Remaining <= 0 || item == null || ps == null || ps.Bag == null || ps.Bag.FreeSpaceFor(item) > 0) return;
            Notifier.Show("Çantada yer yok — " + UiTheme.Name(item) + " yerde kaldı", 2f);
            if (SfxBank.Instance != null) SfxBank.Instance.Play2D(Sfx.Deny, 0.75f);
        }

        public void Interact(Sandbox p)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (ps == null || item == null || Remaining <= 0) return;
            int added = ps.Bag.Add(item, remaining);
            if (added <= 0) return;
            remaining -= added;
            Apply();
            WorldState.MarkLoot(lootId, remaining);
            ps.NotifyPickup(item, added);
            if (remaining > 0) Notifier.Show("Çanta doldu: " + remaining + " " + UiTheme.Name(item) + " yerde kaldı", 2f);
            if (SfxBank.Instance != null) SfxBank.Instance.Play2D(IsSoft(item) ? Sfx.PickupSoft : Sfx.PickupHard, 0.75f);
        }

        static bool IsSoft(ItemDef d)
        {
            string id = d.id;
            return id == "cloth" || id == "tape" || id == "bandage" || id == "supply_cache";
        }

        // Used when a save is applied (no sound, no notification).
        public void SetRemaining(int value)
        {
            Init();
            remaining = Mathf.Clamp(value, 0, Mathf.Max(0, amount));
            Apply();
        }

        void Apply()
        {
            bool gone = remaining <= 0;
            if (visual != null) visual.SetActive(!gone);
            if (trigger != null) trigger.enabled = !gone;
        }
    }
}
