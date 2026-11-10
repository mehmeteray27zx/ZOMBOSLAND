using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zombosland
{
    // Build index 0 is IlkIkmal. QA / perf / sandbox command lines must keep running in CombatSandbox,
    // so they are routed there before the first scene is loaded.
    public static class BootRouter
    {
        public const string SandboxScene = "CombatSandbox";

        // True once QA routing has replaced the first scene; components of that scene must not do real work in Awake.
        public static bool Rerouted { get; private set; }

        public static bool IsAbandoned(GameObject go)
        {
            return Rerouted && go.scene.name != SandboxScene;
        }

        static bool WantsSandbox()
        {
            foreach (string a in Environment.GetCommandLineArgs())
                if (a == "--qa-selftest" || a == "--qa-screens" || a == "--sandbox" || a == "--perf" || a.StartsWith("--perf=", StringComparison.Ordinal)) return true;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Route()
        {
#if UNITY_EDITOR
            return; // in the editor the scene that is open is the one being played
#else
            if (WantsSandbox())
            {
                Rerouted = true;
                SceneManager.LoadScene(SandboxScene);
                Debug.Log("BOOT_SCENE=" + SandboxScene);
            }
            else Debug.Log("BOOT_SCENE=" + System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(0)));
#endif
        }

        // Evidence for QA: every scene that actually finishes loading (the routed run shows IlkIkmal first, then CombatSandbox).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void LogLoads()
        {
            SceneManager.sceneLoaded += (scene, mode) => Debug.Log("BOOT_SCENE_LOADED=" + scene.name);
        }
    }
}
