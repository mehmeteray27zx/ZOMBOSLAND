using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace Zombosland
{
    // --qa-screens-ilkikmal (windowed run with real rendering): scripted cameras for the Ilk Ikmal gray box.
    // PNGs go to QaScreens_ilkikmal/ next to the exe. The player is teleported and turned (as if looking); interactions use the
    // Interactor's Press() path, the hint cards come from the real HintDirector. Zombies stay frozen.
    public class QaScreensIlkIkmal : MonoBehaviour
    {
        Sandbox player;
        QuestFuse quest;
        Interactor interactor;
        HintDirector hints;
        string dir;
        float t0;
        int saved;

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Sandbox.InstantHands = true; // hand actions apply at once in scripted runs
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            player = GetComponent<Sandbox>();
            for (int i = 0; i < 600; i++)
            {
                quest = QuestFuse.Instance;
                hints = HintDirector.Instance;
                interactor = GetComponent<Interactor>();
                if (player.Motor != null && player.AimPivot != null && quest != null && hints != null && interactor != null
                    && ZombieDirector.Instance != null && ZombieDirector.Instance.NavMeshReady) break;
                yield return null;
            }
            if (quest == null || hints == null || interactor == null) { Debug.LogError("QA_SCREENS_ILKIKMAL FAIL scene objects missing"); GameQuit.Request(); yield break; }
            yield return new WaitForSecondsRealtime(1.0f);

            dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "QaScreens_ilkikmal");
            Directory.CreateDirectory(dir);
            HintDirector.QaPrefs prefs = HintDirector.QaIsolate(); // hint flags start clean; the user's values come back at the end
            player.QaSetFreeze(true);
            player.Motor.InputEnabled = false;
            t0 = Time.time;
            Debug.Log("QA_SCREENS_ILKIKMAL start screen=" + Screen.width + "x" + Screen.height + " dir=" + dir);

            DoorInteractable doorA = null, doorD = null;
            foreach (DoorInteractable d in FindObjectsByType<DoorInteractable>())
            {
                if (d.name == "Door_A") doorA = d;
                else if (d.name == "Door_D") doorD = d;
            }
            FuseItem fuse = FindAnyObjectByType<FuseItem>();
            FusePanel panel = FindAnyObjectByType<FusePanel>();

            // 07: A interior before the light event (emergency light only).
            Vector3 aView = new Vector3(-27.6f, 0.05f, -33.2f);
            Vector3 aTarget = new Vector3(-32.9f, 1.0f, -37.4f);
            yield return Look(aView, aTarget, 0.9f);
            yield return Capture("07_A_before_light_event");

            // 01: from the A door towards B. The door is opened with the real interaction first.
            yield return Look(new Vector3(-28f, 0.05f, -34f), doorA.HintAnchor.position, 0.4f);
            bool pressed = interactor.QaPress();
            yield return new WaitForSeconds(0.9f);
            Debug.Log("QA_SCREENS_ILKIKMAL door A opened=" + doorA.IsOpen + " pressed=" + pressed);
            yield return Look(new Vector3(-26f, 0.05f, -34f), new Vector3(14f, 1.6f, -32.6f), 0.7f);
            yield return Capture("01_A_to_B");

            // 02: the minivan on the main street.
            yield return Look(new Vector3(-9.2f, 0.05f, -22f), new Vector3(-6f, 1.4f, -5f), 0.7f);
            yield return Capture("02_minivan_street");

            // 03: depot workbench, fuse and its [E] label.
            yield return Look(new Vector3(2f, 0.05f, 35.8f), fuse.HintAnchor.position, 0.7f);
            yield return Capture("03_depot_bench_fuse");
            pressed = interactor.QaPress();
            Debug.Log("QA_SCREENS_ILKIKMAL fuse taken=" + fuse.Taken + " pressed=" + pressed + " state=" + quest.State);

            // 04: D side door seen from inside.
            yield return Look(new Vector3(12.3f, 0.05f, 33f), doorD.HintAnchor.position, 0.7f); // 1.9 m: inside the 2.2 m interaction range
            yield return Capture("04_D_side_door_inside");

            // 05: the E arm, looking south towards the first widening.
            yield return Look(new Vector3(34f, 0.05f, 25f), new Vector3(34f, 1.6f, 12f), 0.7f);
            yield return Capture("05_E_widening");

            // 06: yard panel with the fuse in hand.
            yield return Look(new Vector3(41f, 0.05f, -30f), panel.HintAnchor.position, 0.8f);
            yield return Capture("06_F_panel_with_fuse");
            pressed = interactor.QaPress();
            Debug.Log("QA_SCREENS_ILKIKMAL fuse installed=" + panel.Installed + " pressed=" + pressed + " state=" + quest.State + " lightEvents=" + quest.LightEventCount);

            // 08: A interior after the light event. The quest already moved on to the station when the fuse went in (no result card).
            yield return Look(aView, aTarget, 0.9f);
            yield return new WaitForSeconds(0.5f);
            Debug.Log("QA_SCREENS_ILKIKMAL back in A state=" + quest.State + " card=" + quest.CardVisible);
            yield return Capture("08_A_after_light_event");

            // 09: reload hint card (magazine empty, reserve left).
            player.Weapon.QaSetAmmo(0);
            yield return Look(new Vector3(-8f, 0.05f, 8f), new Vector3(-8f, 1.6f, 30f), 0.4f);
            float guard = Time.time + 3f;
            while (!hints.Showing(HintDirector.Reload) && Time.time < guard) yield return null;
            yield return new WaitForSeconds(0.4f);
            Debug.Log("QA_SCREENS_ILKIKMAL reload card showing=" + hints.Showing(HintDirector.Reload) + " text='" + hints.CurrentText + "' ammo=" + player.Weapon.Ammo + "/" + player.Weapon.Reserve);
            yield return Capture("09_reload_hint_card");
            player.Weapon.QaSetAmmo(WeaponState.Capacity);

            // 10: objective line and a zombie about 15 m ahead (the aim hint may be up as well).
            ZombieBrain z = GameObject.Find("Zombie_S1_1").GetComponent<ZombieBrain>();
            Vector3 stand = new Vector3(-8f, 0.05f, 0f);
            Place(z, new Vector3(-8f, 0f, 15f), stand);
            yield return Look(stand, new Vector3(-8f, 1.5f, 15f), 1.2f);
            Debug.Log("QA_SCREENS_ILKIKMAL zombie distance=" + Vector3.Distance(new Vector3(z.transform.position.x, 0f, z.transform.position.z), new Vector3(stand.x, 0f, stand.z)).ToString("F1")
                + " card=" + hints.CurrentId + " objective='" + quest.ObjectiveText + "'");
            yield return Capture("10_objective_and_zombie");

            // extra (not one of the ten): the pause menu with the two tutorial entries.
            player.QaSetPaused(true);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("11_pause_menu");
            player.QaSetPaused(false);

            Debug.Log("QA_SCREENS_ILKIKMAL hint cards: frames=" + hints.QaFrames + " violations=" + hints.QaViolations + " maxCards=" + hints.QaMaxCards + " pngs=" + saved);
            HintDirector.QaRestore(prefs);
            Debug.Log("QA_SCREENS_ILKIKMAL_DONE");
            GameQuit.Request();
        }

        // Teleports to `feet`, turns toward `target` and waits so physics, the camera effects and the HUD settle.
        IEnumerator Look(Vector3 feet, Vector3 target, float settle)
        {
            player.Motor.Teleport(feet);
            Vector3 dir3 = target - (feet + Vector3.up * 1.65f);
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir3.x, dir3.z) * Mathf.Rad2Deg, 0f);
            player.Motor.QaSetPitch(-Mathf.Asin(dir3.y / dir3.magnitude) * Mathf.Rad2Deg);
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
            string path = Path.Combine(dir, name + ".png");
            QaPng.Write(path, tex);
            Destroy(tex);
            saved++;
            Debug.Log("QA_SCREENS_ILKIKMAL saved " + name + ".png " + Screen.width + "x" + Screen.height + " t=" + (Time.time - t0).ToString("F1")
                + " quest=" + quest.State + " card=" + (hints.CurrentId ?? "-") + " label='" + (interactor.Label ?? "") + "'");
        }

        static void Place(ZombieBrain z, Vector3 position, Vector3 lookAt)
        {
            position.y = 0f;
            Vector3 facing = lookAt - position; facing.y = 0f;
            var agent = z.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.enabled = false;
            z.transform.position = position;
            if (facing.sqrMagnitude > 0.0001f) z.transform.rotation = Quaternion.LookRotation(facing);
        }
    }
}
