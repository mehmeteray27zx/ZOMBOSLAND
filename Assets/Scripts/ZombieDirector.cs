using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Zombosland
{
    // Builds the runtime NavMesh (no AI Navigation package), activates zombies, manages corpses and perf spawns.
    [DefaultExecutionOrder(-100)]
    public class ZombieDirector : MonoBehaviour
    {
        public static ZombieDirector Instance { get; private set; }

        public Sandbox player;
        public GameObject zombiePrefab;
        // Visual variants (Pxltiger Zombie1/2/3); when empty, zombiePrefab is used.
        public GameObject[] zombiePrefabs;
        public Vector3 navCenter = new Vector3(0, 2, 8);
        public Vector3 navSize = new Vector3(40, 12, 62);
        // Pre-baked NavMesh (IlkIkmal open region, built by the editor). When assigned it is added instead of building at run time.
        public NavMeshData bakedNavMesh;

        const int MaxCorpses = 24;
        const float CorpseSeconds = 20f;

        readonly List<ZombieBrain> corpses = new List<ZombieBrain>();
        readonly List<float> corpseTimes = new List<float>();
        NavMeshDataInstance navInstance;
        NavMeshData navData;

        public Sandbox Player { get { return player; } }
        public bool NavMeshReady { get; private set; }

        void Awake()
        {
            if (BootRouter.IsAbandoned(gameObject)) return; // first scene was replaced by QA routing; skip the NavMesh build
            Instance = this;
            // Dead bodies do not push the player or live zombies around.
            Physics.IgnoreLayerCollision(Layers.Corpse, Layers.Player, true);
            Physics.IgnoreLayerCollision(Layers.Corpse, Layers.ZombieMover, true);
            Physics.IgnoreLayerCollision(Layers.Corpse, Layers.ZombieHitbox, true);
            if (!Application.isPlaying) return;
            BuildNavMesh();
        }

        void Start()
        {
            foreach (ZombieBrain brain in FindObjectsByType<ZombieBrain>())
                if (brain.gameObject.activeInHierarchy) brain.Activate(this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleaseNavMesh();
        }

        // Runtime NavMesh from environment colliders. Returns false (and logs) when it cannot be built.
        public bool BuildNavMesh()
        {
            ReleaseNavMesh();
            if (bakedNavMesh != null)
            {
                navInstance = NavMesh.AddNavMeshData(bakedNavMesh);
                NavMeshReady = navInstance.valid;
                if (!NavMeshReady) Debug.LogWarning("ZombieDirector: baked NavMesh could not be added; fallback chase active.");
                return NavMeshReady;
            }
            try
            {
                var sources = new List<NavMeshBuildSource>();
                var markups = new List<NavMeshBuildMarkup>();
                var bounds = new Bounds(navCenter, navSize);
                // Moving bodies (door leaves) are not part of the static NavMesh; closed doors are cut by carving obstacles.
                foreach (NavIgnore ignore in FindObjectsByType<NavIgnore>())
                    markups.Add(new NavMeshBuildMarkup { root = ignore.transform, ignoreFromBuild = true, applyToChildren = true });
                NavMeshBuilder.CollectSources(bounds, 1 << Layers.Environment, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
                NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
                settings.agentRadius = 0.35f;
                settings.agentHeight = 1.8f;
                settings.agentClimb = 0.35f;
                settings.agentSlope = 40f;
                navData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                if (sources.Count == 0 || navData == null) throw new System.InvalidOperationException("no navmesh sources (" + sources.Count + ")");
                navInstance = NavMesh.AddNavMeshData(navData);
                NavMeshReady = navInstance.valid;
            }
            catch (System.Exception e)
            {
                NavMeshReady = false;
                Debug.LogWarning("ZombieDirector: NavMesh build failed, zombies use straight-line fallback. " + e.Message);
            }
            if (!NavMeshReady) Debug.LogWarning("ZombieDirector: NavMesh not available; fallback chase active.");
            return NavMeshReady;
        }

        public void ReleaseNavMesh()
        {
            if (navInstance.valid) NavMesh.RemoveNavMeshData(navInstance);
            navInstance = default(NavMeshDataInstance);
            navData = null;
            NavMeshReady = false;
        }

        // ---------- spawning (perf harness) ----------

        GameObject PickPrefab(int variant)
        {
            if (zombiePrefabs != null && zombiePrefabs.Length > 0)
            {
                int i = variant >= 0 ? variant % zombiePrefabs.Length : Random.Range(0, zombiePrefabs.Length);
                if (zombiePrefabs[i] != null) return zombiePrefabs[i];
            }
            return zombiePrefab;
        }

        public ZombieBrain Spawn(Vector3 position, Quaternion rotation, int variant = -1)
        {
            GameObject prefab = PickPrefab(variant);
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, position, rotation);
            ZombieBrain brain = go.GetComponent<ZombieBrain>();
            brain.player = player;
            brain.Activate(this);
            return brain;
        }

        // Deterministic spawn: same seed gives the same points. Existing scene zombies are removed first.
        public int SpawnFixed(int count, int seed)
        {
            foreach (ZombieBrain old in FindObjectsByType<ZombieBrain>()) Destroy(old.gameObject);
            var random = new System.Random(seed);
            int spawned = 0, attempts = 0;
            while (spawned < count && attempts < count * 40)
            {
                attempts++;
                var candidate = new Vector3((float)(random.NextDouble() * 26 - 13), 0, (float)(random.NextDouble() * 24 + 2));
                if ((candidate - player.transform.position).magnitude < 6f) continue;
                NavMeshHit hit;
                Vector3 position = candidate;
                if (NavMeshReady)
                {
                    if (!NavMesh.SamplePosition(candidate, out hit, 1.5f, NavMesh.AllAreas)) continue;
                    position = hit.position;
                }
                ZombieBrain brain = Spawn(position, Quaternion.Euler(0, (float)(random.NextDouble() * 360), 0), spawned);
                if (brain == null) break;
                brain.Alert();
                spawned++;
            }
            return spawned;
        }

        // ---------- corpses ----------

        public void RegisterCorpse(ZombieBrain brain)
        {
            corpses.Add(brain);
            corpseTimes.Add(Time.time);
        }

        void Update()
        {
            while (corpses.Count > 0 && (corpses.Count > MaxCorpses || Time.time - corpseTimes[0] > CorpseSeconds))
            {
                if (corpses[0] != null) Destroy(corpses[0].gameObject);
                corpses.RemoveAt(0);
                corpseTimes.RemoveAt(0);
            }
        }
    }
}
