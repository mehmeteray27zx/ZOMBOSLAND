using System.Globalization;
using UnityEngine;

namespace Zombosland
{
    // Order is part of the save format: new states are only appended.
    public enum QuestState { SearchFuse, CarryingFuse, FuseInstalled, Complete, ExploreStation, ReturnToYard, FreeRoam, CraftLure, RetrieveCache }

    // Scene component: the quest state machine. Transitions only happen through a successful action and never go back.
    // Fuse -> power event (gate opens, service door unlocks) -> station (stone + cloth) -> craft a lure -> fetch the supply bag from the shop cabinet
    // -> back to the yard (reward) -> free roam. The enum order is the save format, so the two newest states sit at the end.
    public class QuestFuse : MonoBehaviour
    {
        public static QuestFuse Instance { get; private set; }

        public Sandbox player;
        public Collider safeRoomTrigger; // no longer ends the quest; kept for the hint director
        public Bounds pharmacyBounds; // C interior; only used to record whether the player went in
        public Light panelLamp, mainLight, emergencyLight;
        public Renderer radioLamp, panelLampMesh;
        public Color radioGlow = new Color(1f, 0.55f, 0.15f), panelGlow = new Color(0.7f, 1f, 0.5f);

        // Open region (v0.7).
        public Light[] yardLamps;
        public Light benchLamp;
        public Renderer[] lampMeshes;
        public Color lampGlow = new Color(1f, 0.85f, 0.55f);
        public DoorInteractable outerGate, serviceDoor;
        public Bounds stationBounds, yardBounds; // checked on the ground plane (height is ignored)
        public Vector3 stationMarker, yardMarker, cacheMarker;
        // Station routes and the front-half zombie spots (world positions, set by the builder; used by QA).
        public Vector3[] rearRoute, frontRoute, stationZombieSpots;

        const float StallSeconds = 20f, StallRadius = 1.5f;
        public const string ClothId = "cloth", StoneId = "stone", LureId = "lure", CacheId = "supply_cache", BandageId = "bandage";
        const float ForecourtWarnDistance = 45f;

        float stallSince;
        bool lightEventDone, pharmacySeen, stallCounted, bandageIntroShown;
        bool yardInit, wasInYard, forecourtWarned;
        Vector3 stallAnchor;

        public QuestState State { get; private set; } = QuestState.SearchFuse;
        public int LightEventCount { get; private set; }
        public float StateChangedAt { get; private set; } = -100f; // unscaled time, for the HUD highlight
        public float CompletedSeconds { get; private set; }
        public int CardDeaths { get; private set; }
        public int CardShots { get; private set; }
        public int CardHits { get; private set; }
        // The result card is gone (the quest goes on into the open region); the API stays for the HUD.
        public bool CardVisible { get { return false; } }
        public bool BandageIntroShown { get { return bandageIntroShown; } }

        public string ObjectiveHeading
        {
            get { return (int)State <= (int)QuestState.Complete ? "İLK İKMAL" : "DIŞ BÖLGE"; }
        }

        public string ObjectiveText
        {
            get
            {
                switch (State)
                {
                    case QuestState.SearchFuse: return "Depodaki sigortayı bul";
                    case QuestState.CarryingFuse: return "Sigortayı panoya tak";
                    case QuestState.ExploreStation: return "İstasyona git: taş ve kumaş topla";
                    case QuestState.CraftLure: return "Dikkat taşı üret — " + InputLabels.Key(InputLabels.Inventory);
                    case QuestState.RetrieveCache: return "Dükkândan erzak çantasını al";
                    case QuestState.ReturnToYard: return "Erzak çantasını avluya getir";
                    case QuestState.FreeRoam: return "Bölgeyi keşfet";
                    default: return "Elektrik geri geldi";
                }
            }
        }

        public bool HasObjectiveMarker
        {
            get
            {
                return State == QuestState.ExploreStation || State == QuestState.CraftLure || State == QuestState.RetrieveCache || State == QuestState.ReturnToYard;
            }
        }

        public Vector3 ObjectiveMarker
        {
            get
            {
                if (State == QuestState.ExploreStation || State == QuestState.CraftLure) return stationMarker;
                if (State == QuestState.RetrieveCache) return cacheMarker;
                if (State == QuestState.ReturnToYard) return yardMarker;
                return Vector3.zero;
            }
        }

        void Awake()
        {
            if (BootRouter.IsAbandoned(gameObject)) return;
            Instance = this;
            PlaytestLog.BeginLevel();
        }

        void Start()
        {
            if (Instance != this) return;
            LogState();
            StateChangedAt = Time.unscaledTime;
            if (player != null) stallAnchor = player.transform.position;
            stallSince = Time.time;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool TakeFuse()
        {
            if (State != QuestState.SearchFuse) return false;
            SetState(QuestState.CarryingFuse);
            return true;
        }

        // Power comes back once: lights, gate opens, service door unlocks, then the quest moves on to the station.
        public bool InstallFuse()
        {
            if (State != QuestState.CarryingFuse) return false;
            SetState(QuestState.FuseInstalled);
            RunLightEvent();
            if (outerGate != null) outerGate.ForceOpen(false);
            if (serviceDoor != null) serviceDoor.Unlock();

            CompletedSeconds = PlaytestLog.LevelSeconds;
            CardDeaths = PlaytestLog.Deaths;
            CardShots = PlaytestLog.ShotsFired;
            CardHits = PlaytestLog.Hits;
            SetState(QuestState.Complete);
            PlaytestLog.WriteComplete();
            Notifier.Show("Elektrik geri geldi", 4f);

            SetState(QuestState.ExploreStation);
            SaveSystem.Save("fuse");
            return true;
        }

        // Kept for the existing HUD / QA callers; there is no card any more.
        public void DismissCard() { }

        // Called when loot reaches the bag. First cloth shows the recipe hint; the supply bag moves the quest on to the yard.
        public void NotifyLoot(ItemDef item, int count)
        {
            if (item == null || count <= 0) return;
            if (item.id == ClothId && !bandageIntroShown)
            {
                bandageIntroShown = true;
                Notifier.Show("Yeni tarif: Bandaj — " + InputLabels.Key(InputLabels.Inventory) + " çantayı açar", 5f);
            }
            if (item.id == CacheId && (State == QuestState.ExploreStation || State == QuestState.CraftLure || State == QuestState.RetrieveCache))
                SetState(QuestState.ReturnToYard);
        }

        // Loading a save: state and lights are applied without running any event. Door states come from WorldState.
        public void RestoreState(QuestState s)
        {
            State = s;
            StateChangedAt = Time.unscaledTime;
            if ((int)s >= (int)QuestState.FuseInstalled)
            {
                lightEventDone = true;
                LightEventCount = 1;
                ApplyLights();
                if (outerGate != null) outerGate.Unlock();
                if (serviceDoor != null) serviceDoor.Unlock();
            }
            yardInit = false;
            LogState();
        }

        public void RestoreBandageIntro(bool shown) { bandageIntroShown = shown; }

        static bool InBounds(Bounds b, Vector3 p)
        {
            return b.size.sqrMagnitude > 0f && p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z;
        }

        void Update()
        {
            if (Instance != this) return;
            TrackPlaytest();
            UpdateRoute();
            UpdateYard();
        }

        static int BagCount(string id)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (ps == null || ps.Bag == null || ps.database == null) return 0;
            ItemDef d = ps.database.FindItem(id);
            return d != null ? ps.Bag.Available(d) : 0;
        }

        // Forward-only checks on the bag: stone + cloth -> craft a lure -> fetch the supply bag. Also the forecourt warning.
        void UpdateRoute()
        {
            if (State == QuestState.ExploreStation)
            {
                if (BagCount(LureId) > 0) SetState(QuestState.RetrieveCache);
                else if (BagCount(StoneId) >= 1 && BagCount(ClothId) >= 1) SetState(QuestState.CraftLure);
            }
            else if (State == QuestState.CraftLure)
            {
                if (BagCount(LureId) >= 1) SetState(QuestState.RetrieveCache);
            }
            if (State == QuestState.RetrieveCache && BagCount(CacheId) > 0) SetState(QuestState.ReturnToYard);

            if (!forecourtWarned && player != null && (State == QuestState.ExploreStation || State == QuestState.CraftLure || State == QuestState.RetrieveCache))
            {
                Vector3 d = player.transform.position - stationMarker;
                d.y = 0f;
                if (stationMarker.sqrMagnitude > 0f && d.magnitude <= ForecourtWarnDistance)
                {
                    forecourtWarned = true;
                    Notifier.Show("Ön avlu dolu: taşı uzağa at ya da arkadan dolaş", 5f);
                }
            }
        }

        // First trip reward: the supply bag is taken out of the bag, +2 bandages (overflow goes to storage) and 12 rounds. Runs once, on the state change.
        void GiveFirstTripReward()
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            ItemDef cache = ps != null && ps.database != null ? ps.database.FindItem(CacheId) : null;
            if (cache == null || ps.Bag == null || !ps.Bag.TryRemove(cache, 1))
            {
                Notifier.Show("İlk sefer tamamlandı", 4f);
                return;
            }
            ItemDef bandage = ps.database.FindItem(BandageId);
            if (bandage != null)
            {
                int added = ps.Bag.Add(bandage, 2);
                if (added < 2 && ps.Storage != null) ps.Storage.Add(bandage, 2 - added);
            }
            if (player != null && player.Weapon != null) player.Weapon.AddReserve(12);
            Notifier.Show("İlk sefer tamamlandı: +2 Bandaj, +12 mermi", 5f);
        }

        void UpdateYard()
        {
            if (player == null) return;
            if (State != QuestState.ReturnToYard && State != QuestState.FreeRoam) return;
            bool inYard = InBounds(yardBounds, player.transform.position);
            if (State == QuestState.ReturnToYard)
            {
                if (!inYard) return;
                SetState(QuestState.FreeRoam);
                GiveFirstTripReward();
                wasInYard = true;
                yardInit = true;
                SaveSystem.Save("yard");
                return;
            }
            if (!yardInit) { yardInit = true; wasInYard = inYard; return; }
            if (inYard && !wasInYard) SaveSystem.Save("yard"); // coming back into the yard from outside
            wasInYard = inYard;
        }

        // Playtest measurements: whether the pharmacy was entered, and stretches of 20 s or more spent within 1.5 m of one spot
        // (game time, so the pause menu does not count; one count per stretch). Only until the first quest is complete.
        void TrackPlaytest()
        {
            if (player == null || (int)State >= (int)QuestState.Complete) return;
            Vector3 p = player.transform.position;
            if (!pharmacySeen && pharmacyBounds.size.sqrMagnitude > 0f && pharmacyBounds.Contains(p + Vector3.up * 0.9f))
            {
                pharmacySeen = true;
                PlaytestLog.RecordPharmacyEntry();
            }
            Vector3 d = p - stallAnchor;
            d.y = 0f;
            if (d.magnitude > StallRadius)
            {
                stallAnchor = p;
                stallSince = Time.time;
                stallCounted = false;
            }
            else if (!stallCounted && Time.time - stallSince >= StallSeconds)
            {
                stallCounted = true;
                PlaytestLog.RecordStall();
            }
        }

        void SetState(QuestState next)
        {
            State = next;
            StateChangedAt = Time.unscaledTime;
            LogState();
        }

        void LogState()
        {
            float t = PlaytestLog.LevelSeconds;
            PlaytestLog.RecordQuest(State.ToString(), t);
            Debug.Log("QUEST state=" + State + " t=" + t.ToString("F1", CultureInfo.InvariantCulture));
        }

        // Fires exactly once: the power comes back.
        void RunLightEvent()
        {
            if (lightEventDone) return;
            lightEventDone = true;
            LightEventCount++;
            ApplyLights();
            if (SfxBank.Instance != null)
            {
                Vector3 at = panelLamp != null ? panelLamp.transform.position : transform.position;
                SfxBank.Instance.Play(Sfx.Slide, at, 0.9f, 0.7f, true);
            }
        }

        // The lit state (event and save restore). Missing references (old scenes) are skipped.
        void ApplyLights()
        {
            if (panelLamp != null) panelLamp.enabled = true;
            if (mainLight != null) mainLight.enabled = true;
            if (emergencyLight != null) emergencyLight.enabled = false;
            if (benchLamp != null) benchLamp.enabled = true;
            if (yardLamps != null)
                foreach (Light l in yardLamps) if (l != null) l.enabled = true;
            Glow(radioLamp, radioGlow);
            Glow(panelLampMesh, panelGlow);
            if (lampMeshes != null)
                foreach (Renderer r in lampMeshes) Glow(r, lampGlow);
        }

        static void Glow(Renderer r, Color c)
        {
            if (r == null) return;
            Material m = r.material; // instance, so the shared asset is not changed
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c);
        }

        // Emission colour of a lamp renderer (instance material), for QA.
        public static Color EmissionOf(Renderer r)
        {
            return r != null && r.material.HasProperty("_EmissionColor") ? r.material.GetColor("_EmissionColor") : Color.black;
        }
    }
}
