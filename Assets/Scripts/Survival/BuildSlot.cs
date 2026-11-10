using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Zombosland
{
    public enum BuildKind { Barricade, Chest }

    // A fixed yard slot where a kit can be installed (no free base-building). A barricade shuts a passage for zombies (carving
    // NavMeshObstacle) until they break it; a chest adds extra storage slots. State is saved through WorldState (ForceState).
    public class BuildSlot : MonoBehaviour, IHandInteractable, IFocusTitle, IBlockedLabel
    {
        public const float MaxHealth = 100f, RepairAmount = 60f;
        public const int ChestExtraSlots = 12;

        [SerializeField] string slotId;
        [SerializeField] BuildKind kind;
        [SerializeField] ItemDef kit;
        [SerializeField] ItemDef repairKit;
        [SerializeField] GameObject builtVisual;
        [SerializeField] NavMeshObstacle obstacle;
        [SerializeField] Transform hintAnchor;

        static readonly List<BuildSlot> all = new List<BuildSlot>();
        public static List<BuildSlot> All { get { return all; } }

        bool built;
        float health;
        StorageBox storageProxy; // chest only; created on first use, has no collider so it is never focused itself

        public string SlotId { get { return slotId; } }
        public BuildKind Kind { get { return kind; } }
        public bool Built { get { return built; } }
        public float Health { get { return health; } }
        public bool BlockedBriefly { get { return true; } }
        // Only the built chest is a container the hands open; building and repairing stay instant.
        public HandActionKind HandKind { get { return built && kind == BuildKind.Chest ? HandActionKind.OpenContainer : HandActionKind.None; } }
        public bool RequiresLineOfSight { get { return false; } }
        public string Title { get { return built && kind == BuildKind.Chest ? "Sandık" : null; } }
        public Transform HintAnchor { get { return hintAnchor != null ? hintAnchor : transform; } }

        public string Verb
        {
            get
            {
                if (!built) return kind == BuildKind.Barricade ? "Barikat kur" : "Sandık kur";
                return kind == BuildKind.Barricade ? "Barikatı onar" : "Sandığı aç";
            }
        }

        void Awake()
        {
            if (BootRouter.IsAbandoned(gameObject)) return;
            if (!all.Contains(this)) all.Add(this);
            ApplyVisuals();
        }

        void OnDestroy() { all.Remove(this); }

        // ---------- interaction ----------

        public bool CanInteract(Sandbox p)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (ps == null || ps.Bag == null || ps.UiOpen) return false;
            if (!built) return kit != null && ps.Bag.Available(kit) >= 1;
            if (kind == BuildKind.Chest) return true;
            return health < MaxHealth - 0.001f && repairKit != null && ps.Bag.Available(repairKit) >= 1;
        }

        public string BlockedLabel(Sandbox p)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (ps == null || ps.Bag == null || ps.UiOpen) return null;
            if (!built)
            {
                if (kit != null && ps.Bag.Available(kit) >= 1) return null;
                return kind == BuildKind.Barricade ? "Barikat kiti gerekli" : "Sandık kiti gerekli";
            }
            if (kind == BuildKind.Barricade && health < MaxHealth - 0.001f && (repairKit == null || ps.Bag.Available(repairKit) < 1)) return "Tamir kiti gerekli";
            return null;
        }

        public void Interact(Sandbox p)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (ps == null || ps.Bag == null || ps.UiOpen) return;
            if (!built)
            {
                // Exactly one kit; the slot only becomes built when the kit was really taken.
                if (kit == null || !ps.Bag.TryRemove(kit, 1)) return;
                built = true;
                health = MaxHealth;
                ApplyVisuals();
                Notifier.Show(kind == BuildKind.Barricade ? "Barikat kuruldu" : "Sandık kuruldu", 2.5f);
                return;
            }
            if (kind == BuildKind.Chest)
            {
                if (storageProxy == null)
                {
                    var go = new GameObject("StorageProxy");
                    go.transform.SetParent(transform, false);
                    storageProxy = go.AddComponent<StorageBox>();
                }
                ps.OpenUi(UiTab.Inventory, storageProxy);
                return;
            }
            if (health >= MaxHealth - 0.001f || repairKit == null || !ps.Bag.TryRemove(repairKit, 1)) return;
            health = Mathf.Min(MaxHealth, health + RepairAmount);
            Notifier.Show("Barikat onarıldı", 2f);
        }

        // ---------- state ----------

        // Zombie (or QA) damage. Only a built barricade takes it; at zero health the barricade falls and its kit is lost.
        public void ApplyDamage(float amount)
        {
            if (!built || kind != BuildKind.Barricade || amount <= 0f) return;
            health = Mathf.Max(0f, health - amount);
            if (health > 0f) return;
            built = false;
            ApplyVisuals();
            Notifier.Show("Barikat yıkıldı", 3f);
        }

        // Restores a saved state without events, notifications or item use.
        public void ForceState(bool isBuilt, float savedHealth)
        {
            built = isBuilt;
            health = isBuilt ? Mathf.Clamp(savedHealth, 1f, MaxHealth) : 0f;
            if (isBuilt && kind == BuildKind.Chest) health = MaxHealth;
            ApplyVisuals();
        }

        // Visual, collider and obstacle always follow `built` together; a chest also raises the shared storage capacity.
        void ApplyVisuals()
        {
            if (builtVisual != null) builtVisual.SetActive(built);
            if (obstacle != null) obstacle.enabled = built && kind == BuildKind.Barricade;
            if (built && kind == BuildKind.Chest)
            {
                PlayerSurvival ps = PlayerSurvival.Instance;
                if (ps != null && ps.Storage != null) ps.Storage.SetCapacity(PlayerSurvival.StorageSlots + ChestExtraSlots);
            }
        }

        // ---------- queries ----------

        // Planar distance from a point to the blocked area (the obstacle box when there is one, else the slot origin).
        public float PlanarDistance(Vector3 point)
        {
            if (obstacle != null && obstacle.shape == NavMeshObstacleShape.Box)
            {
                Transform t = obstacle.transform;
                Vector3 local = t.InverseTransformPoint(point) - obstacle.center;
                Vector3 half = obstacle.size * 0.5f;
                Vector3 inside = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), 0f, Mathf.Clamp(local.z, -half.z, half.z));
                Vector3 closest = t.TransformPoint(inside + obstacle.center);
                closest.y = point.y;
                return Vector3.Distance(closest, point);
            }
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        // Nearest built barricade within `range` metres (planar), or null.
        public static BuildSlot NearestBuiltBarricade(Vector3 point, float range)
        {
            BuildSlot best = null;
            float bestD = range;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                BuildSlot s = all[i];
                if (s == null) { all.RemoveAt(i); continue; }
                if (!s.built || s.kind != BuildKind.Barricade) continue;
                float d = s.PlanarDistance(point);
                if (d <= bestD) { bestD = d; best = s; }
            }
            return best;
        }

        public static BuildSlot Find(string id)
        {
            for (int i = all.Count - 1; i >= 0; i--)
                if (all[i] != null && all[i].slotId == id) return all[i];
            return null;
        }
    }
}
