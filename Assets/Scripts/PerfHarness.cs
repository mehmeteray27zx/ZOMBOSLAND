using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Zombosland
{
    // Creates the QA helpers requested by command line before any scene object needs them.
    public static class RuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            float seconds;
            // Timed QA runs must keep running when the window is not focused.
            if (QaArgs.Has("--perf") || QaArgs.TryGetFloat("--perf", out seconds) || QaArgs.TryGetFloat("--qa-autoquit", out seconds)) Application.runInBackground = true;
            if (QaArgs.TryGetFloat("--qa-autoquit", out seconds))
            {
                var go = new GameObject("QaAutoQuit");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<QaAutoQuit>().seconds = seconds;
            }
            if (QaArgs.Has("--qa-playthrough"))
            {
                Application.runInBackground = true;
                var go = new GameObject("QaPlaythrough");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<QaPlaythrough>();
            }
            if (QaArgs.Has("--qa-survival"))
            {
                Application.runInBackground = true;
                SaveSystem.UseSlot("qa_survival", true); // own save slot, wiped before the scene's WorldState reads it
                var go = new GameObject("QaSurvival");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<QaSurvival>();
            }
            if (QaArgs.Has("--qa-selftest"))
            {
                Application.runInBackground = true;
                var go = new GameObject("QaSelfTest");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<QaSelfTest>();
            }
            float openCount;
            if (QaArgs.TryGetFloat("--perf-openworld", out openCount))
            {
                Application.runInBackground = true;
                var go = new GameObject("PerfHarness");
                UnityEngine.Object.DontDestroyOnLoad(go);
                var harness = go.AddComponent<PerfHarness>();
                harness.openWorld = true;
                harness.zombieCount = (int)openCount;
                harness.warmupSeconds = QaArgs.GetFloat("--perf-warmup", 10f);
                harness.measureSeconds = QaArgs.GetFloat("--perf-seconds", 60f);
            }
            float count;
            if (QaArgs.TryGetFloat("--perf", out count))
            {
                var go = new GameObject("PerfHarness");
                UnityEngine.Object.DontDestroyOnLoad(go);
                var harness = go.AddComponent<PerfHarness>();
                harness.zombieCount = (int)count;
                harness.warmupSeconds = QaArgs.GetFloat("--perf-warmup", 10f);
                harness.measureSeconds = QaArgs.GetFloat("--perf-seconds", 60f);
            }
        }
    }

    public class QaAutoQuit : MonoBehaviour
    {
        public float seconds;
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(seconds);
            Debug.Log("QA_AUTOQUIT_REQUESTED after " + seconds.ToString("F1", CultureInfo.InvariantCulture) + " s");
            GameQuit.Request();
        }
    }

    // Fixed-seed perf scenario: player stands still, camera sweeps 2 full turns, zombies chase, no shooting.
    public class PerfHarness : MonoBehaviour
    {
        public int zombieCount = 10;
        public float warmupSeconds = 10f, measureSeconds = 60f;
        public bool openWorld; // --perf-openworld=N: IlkIkmal route from the gate to the water tower along the main road
        const int Seed = 12345;

        // Main road control points in region coordinates R (world = (Rx - 46, y, Rz - 110)); same list as OpenWorldBuilder.
        static readonly Vector2[] RoadCtrl =
        {
            new Vector2(97, 72), new Vector2(125, 70), new Vector2(160, 85), new Vector2(195, 115), new Vector2(232, 140), new Vector2(262, 175),
            new Vector2(285, 225), new Vector2(306, 268), new Vector2(322, 295), new Vector2(345, 330), new Vector2(372, 372), new Vector2(392, 382)
        };
        readonly List<Vector3> route = new List<Vector3>();
        readonly List<float> routeCum = new List<float>();
        float lastRouteY;

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Sandbox player = null;
            ZombieDirector director = null;
            for (int i = 0; i < 300 && (player == null || director == null || player.Motor == null); i++)
            {
                yield return null;
                player = FindAnyObjectByType<Sandbox>();
                director = ZombieDirector.Instance;
            }
            if (player == null || director == null) { Debug.LogError("PERF: scene objects not found"); GameQuit.Request(); yield break; }
            yield return null; // let ZombieDirector.Start activate the scene zombies first

            player.EnterPerfMode();
            int spawned;
            if (openWorld) spawned = OpenWorldSetup(player, director);
            else
            {
                player.Motor.ScriptedYawSpeed = 720f / Mathf.Max(1f, measureSeconds); // two full turns over the window
                spawned = director.SpawnFixed(zombieCount, Seed);
            }
            Debug.Log("PERF: spawned " + spawned + " of " + zombieCount + " zombies, navmesh=" + director.NavMeshReady);

            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < warmupSeconds) yield return null;

            long memAllocStart = Profiler.GetTotalAllocatedMemoryLong(), memReservedStart = Profiler.GetTotalReservedMemoryLong();
            int gcStart = GC.CollectionCount(0);
            var frameMs = new List<float>(8192);
            var cpuMs = new List<double>(8192);
            var gpuMs = new List<double>(8192);
            var timings = new FrameTiming[1];
            string error = null;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < measureSeconds)
            {
                yield return null;
                if (openWorld) MoveAlongRoute(player, (Time.realtimeSinceStartup - t0) / measureSeconds);
                frameMs.Add(Time.unscaledDeltaTime * 1000f);
                try
                {
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                    {
                        cpuMs.Add(timings[0].cpuFrameTime);
                        gpuMs.Add(timings[0].gpuFrameTime);
                    }
                }
                catch (Exception e) { error = e.Message; }
            }
            long memAllocEnd = Profiler.GetTotalAllocatedMemoryLong(), memReservedEnd = Profiler.GetTotalReservedMemoryLong();
            int gcEnd = GC.CollectionCount(0);

            try { WriteResults(frameMs, cpuMs, gpuMs, error, memAllocStart, memAllocEnd, memReservedStart, memReservedEnd, gcStart, gcEnd, spawned); }
            catch (Exception e) { Debug.LogError("PERF: failed to write results: " + e); }
            Debug.Log("PERF_DONE");
            GameQuit.Request();
        }

        // ---------- open world route ----------

        static Vector2 RouteTangent(int i)
        {
            return (RoadCtrl[Mathf.Min(i + 1, RoadCtrl.Length - 1)] - RoadCtrl[Mathf.Max(i - 1, 0)]).normalized;
        }

        void BuildRoute()
        {
            route.Clear();
            routeCum.Clear();
            for (int i = 0; i < RoadCtrl.Length - 1; i++)
            {
                Vector2 p0 = RoadCtrl[i], p1 = RoadCtrl[i + 1];
                float len = (p1 - p0).magnitude;
                Vector2 m0 = RouteTangent(i) * len, m1 = RouteTangent(i + 1) * len;
                for (int k = 0; k < 20; k++)
                {
                    float t = k / 20f, t2 = t * t, t3 = t2 * t;
                    Vector2 p = (2f * t3 - 3f * t2 + 1f) * p0 + (t3 - 2f * t2 + t) * m0 + (-2f * t3 + 3f * t2) * p1 + (t3 - t2) * m1;
                    route.Add(new Vector3(p.x - 46f, 0f, p.y - 110f));
                }
            }
            route.Add(new Vector3(RoadCtrl[RoadCtrl.Length - 1].x - 46f, 0f, RoadCtrl[RoadCtrl.Length - 1].y - 110f));
            float total = 0f;
            routeCum.Add(0f);
            for (int i = 1; i < route.Count; i++) { total += Vector3.Distance(route[i - 1], route[i]); routeCum.Add(total); }
        }

        Vector3 RoutePoint(float distance, out Vector3 direction)
        {
            distance = Mathf.Clamp(distance, 0f, routeCum[routeCum.Count - 1]);
            int i = 0;
            while (i < routeCum.Count - 2 && routeCum[i + 1] < distance) i++;
            float t = Mathf.InverseLerp(routeCum[i], routeCum[i + 1], distance);
            direction = (route[i + 1] - route[i]).normalized;
            return Vector3.Lerp(route[i], route[i + 1], t);
        }

        float GroundHeight(Vector3 p)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, 90f, p.z), Vector3.down, out hit, 200f, 1 << Layers.Environment, QueryTriggerInteraction.Ignore)) lastRouteY = hit.point.y;
            return lastRouteY;
        }

        void MoveAlongRoute(Sandbox player, float fraction)
        {
            Vector3 dir;
            Vector3 p = RoutePoint(fraction * routeCum[routeCum.Count - 1], out dir);
            p.y = GroundHeight(p) + 0.05f;
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
            player.Motor.Teleport(p);
        }

        // Opens the yard exits, spawns N alerted zombies along the first half of the road, puts the player at the gate.
        int OpenWorldSetup(Sandbox player, ZombieDirector director)
        {
            foreach (DoorInteractable door in FindObjectsByType<DoorInteractable>())
                if (door.name == "Gate_Outer" || door.name == "Door_Service") { door.Unlock(); door.ForceOpen(true); }
            BuildRoute();
            lastRouteY = 0f;
            MoveAlongRoute(player, 0f);
            float half = routeCum[routeCum.Count - 1] * 0.5f;
            var random = new System.Random(Seed);
            int spawned = 0;
            for (int i = 0; i < zombieCount; i++)
            {
                float distance = 20f + (i + 0.5f) / Mathf.Max(1, zombieCount) * (half - 20f);
                Vector3 dir;
                Vector3 p = RoutePoint(distance, out dir);
                Vector3 side = new Vector3(dir.z, 0f, -dir.x);
                p += side * ((float)random.NextDouble() * 12f - 6f);
                p.y = GroundHeight(p);
                float yaw = (float)random.NextDouble() * 360f;
                UnityEngine.AI.NavMeshHit hit;
                if (!UnityEngine.AI.NavMesh.SamplePosition(p, out hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                ZombieBrain brain = director.Spawn(hit.position, Quaternion.Euler(0f, yaw, 0f), i);
                if (brain == null) break;
                brain.Alert();
                spawned++;
            }
            return spawned;
        }

        void WriteResults(List<float> frameMs, List<double> cpuMs, List<double> gpuMs, string timingError,
                          long allocStart, long allocEnd, long reservedStart, long reservedEnd, int gcStart, int gcEnd, int spawned)
        {
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "PerfResults");
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string baseName = Path.Combine(dir, (openWorld ? "perf_open_" : "perf_") + zombieCount + "_" + stamp);
            CultureInfo inv = CultureInfo.InvariantCulture;

            var csv = new StringBuilder("frame,frame_ms,cpu_ms,gpu_ms\n");
            for (int i = 0; i < frameMs.Count; i++)
            {
                string cpu = i < cpuMs.Count ? cpuMs[i].ToString("F3", inv) : "N/A";
                string gpu = i < gpuMs.Count ? gpuMs[i].ToString("F3", inv) : "N/A";
                csv.Append(i).Append(',').Append(frameMs[i].ToString("F3", inv)).Append(',').Append(cpu).Append(',').Append(gpu).Append('\n');
            }
            File.WriteAllText(baseName + ".csv", csv.ToString());

            int n = frameMs.Count;
            var sorted = new List<float>(frameMs);
            sorted.Sort();
            double sum = 0; foreach (float f in frameMs) sum += f;
            double mean = n > 0 ? sum / n : 0;
            int worstCount = Mathf.Max(1, n / 100);
            double worstSum = 0; for (int i = 0; i < worstCount && n > 0; i++) worstSum += sorted[n - 1 - i];
            double worstMean = n > 0 ? worstSum / worstCount : 0;
            double p99 = n > 0 ? sorted[Mathf.Min(n - 1, (int)Math.Ceiling(n * 0.99) - 1 < 0 ? 0 : (int)Math.Ceiling(n * 0.99) - 1)] : 0;
            int over33 = 0, over50 = 0;
            foreach (float f in frameMs) { if (f > 33.3f) over33++; if (f > 50f) over50++; }

            // GPU time of 0 means the platform did not report it.
            double gpuSum = 0; int gpuValid = 0; foreach (double g in gpuMs) if (g > 0.0001) { gpuSum += g; gpuValid++; }
            double cpuSum = 0; int cpuValid = 0; foreach (double c in cpuMs) if (c > 0.0001) { cpuSum += c; cpuValid++; }

            var t = new StringBuilder();
            t.AppendLine("Zombosland perf run (fixed seed " + Seed + ")");
            t.AppendLine("zombies_requested=" + zombieCount + " zombies_spawned=" + spawned);
            if (openWorld) t.AppendLine("route=IlkIkmal main road gate -> water tower, length_m=" + (routeCum.Count > 0 ? routeCum[routeCum.Count - 1] : 0f).ToString("F1", inv) + " speed_mps=" + ((routeCum.Count > 0 ? routeCum[routeCum.Count - 1] : 0f) / Mathf.Max(1f, measureSeconds)).ToString("F2", inv));
            t.AppendLine("warmup_s=" + warmupSeconds.ToString("F1", inv) + " measure_s=" + measureSeconds.ToString("F1", inv));
            t.AppendLine("frames=" + n);
            t.AppendLine("avg_fps=" + (mean > 0 ? (1000.0 / mean).ToString("F2", inv) : "N/A"));
            t.AppendLine("avg_frame_ms=" + mean.ToString("F3", inv));
            t.AppendLine("one_percent_low_fps=" + (worstMean > 0 ? (1000.0 / worstMean).ToString("F2", inv) : "N/A") + " (from mean ms of slowest 1% frames, " + worstCount + " frames)");
            t.AppendLine("p99_frame_ms=" + p99.ToString("F3", inv));
            t.AppendLine("max_frame_ms=" + (n > 0 ? sorted[n - 1].ToString("F3", inv) : "N/A"));
            t.AppendLine("hitches_over_33.3ms=" + over33 + " hitches_over_50ms=" + over50);
            t.AppendLine("cpu_frame_ms_avg=" + (cpuValid > 0 ? (cpuSum / cpuValid).ToString("F3", inv) : "N/A") + " (samples " + cpuValid + ")");
            t.AppendLine("gpu_frame_ms_avg=" + (gpuValid > 0 ? (gpuSum / gpuValid).ToString("F3", inv) : "N/A") + " (samples " + gpuValid + ")");
            if (timingError != null) t.AppendLine("frame_timing_error=" + timingError);
            t.AppendLine("mem_allocated_start=" + allocStart + " end=" + allocEnd);
            t.AppendLine("mem_reserved_start=" + reservedStart + " end=" + reservedEnd);
            t.AppendLine("gc_collections_gen0_start=" + gcStart + " end=" + gcEnd + " delta=" + (gcEnd - gcStart));
            t.AppendLine("resolution=" + Screen.width + "x" + Screen.height + " fullscreen_mode=" + Screen.fullScreenMode);
            t.AppendLine("quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + " vsync=" + QualitySettings.vSyncCount + " targetFrameRate=" + Application.targetFrameRate);
            t.AppendLine("graphics_api=" + SystemInfo.graphicsDeviceType + " (" + SystemInfo.graphicsDeviceVersion + ")");
            t.AppendLine("cpu=" + SystemInfo.processorType + " gpu=" + SystemInfo.graphicsDeviceName + " ram_mb=" + SystemInfo.systemMemorySize + " os=" + SystemInfo.operatingSystem);
            t.AppendLine("build_version=" + Application.version + " unity=" + Application.unityVersion);
            File.WriteAllText(baseName + ".txt", t.ToString());
            Debug.Log("PERF: results written to " + baseName + ".txt");
        }
    }
}
