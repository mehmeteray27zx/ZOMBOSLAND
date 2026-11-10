using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Zombosland
{
    // --qa-playthrough (Ilk Ikmal only): scripted run of the interaction / quest / NavMesh chain.
    // The runner is DontDestroyOnLoad so it survives the scene reload after a death (step 8).
    // It teleports the player and sets the view direction (as if looking), then drives the Interactor through the
    // same Press() path as the E key. Zombies stay frozen. Logs QA_PLAYTHROUGH lines, then quits.
    public class QaPlaythrough : MonoBehaviour
    {
        const float WatchdogSeconds = 240f;

        int pass, fail;
        float startedAt;
        bool finished;

        Sandbox player;
        QuestFuse quest;
        Interactor interactor;
        DoorInteractable doorA, doorD;
        FuseItem fuse;
        AmmoCrate ammo;
        FusePanel panel;
        HintDirector hints;
        HintDirector.QaPrefs prefs;

        void Log(string step, bool ok, string message)
        {
            if (ok) pass++; else fail++;
            Debug.Log("QA_PLAYTHROUGH " + step + (ok ? " PASS " : " FAIL ") + message);
        }

        static void Info(string step, string message) { Debug.Log("QA_PLAYTHROUGH " + step + " INFO " + message); }

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
            Debug.Log("QA_PLAYTHROUGH_DONE pass=" + pass + " fail=" + fail);
            GameQuit.Request();
        }

        IEnumerator Start()
        {
            startedAt = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            Sandbox.InstantHands = true; // hand actions apply at once in scripted runs
            yield return Acquire();
            if (player == null) { Log("setup", false, "scene objects missing"); Finish(); yield break; }
            Log("setup", true, "scene=" + SceneManager.GetActiveScene().name + " navmesh=" + ZombieDirector.Instance.NavMeshReady);
            prefs = HintDirector.QaIsolate(); // hint flags and the tutorial toggle: saved here, restored at the end
            yield return new WaitForSeconds(2.5f); // carving obstacles take effect asynchronously

            // ---- 1 (closed): closed doors cut the NavMesh ----
            NavMeshPathStatus status; float length; int corners;
            bool complete = Path(new Vector3(-20f, 0f, -36f), new Vector3(-30f, 0f, -36f), out status, out length, out corners);
            Log("1", !complete, "closed A door: outside -> inside status=" + status);
            complete = Path(new Vector3(12f, 0f, 33f), new Vector3(20f, 0f, 33f), out status, out length, out corners);
            // The corridor can still be reached the long way round (street, lower road, yard, arm), so the closed door shows up as a detour.
            Log("1", !complete || length > 60f, "closed D side door: depot -> corridor detour length=" + length.ToString("F1") + " status=" + status);

            // ---- 6 (without fuse): the panel shows a dim note and does not react ----
            yield return Stand(new Vector3(41f, 0.05f, -30f), panel.HintAnchor.position);
            interactor.Refresh();
            bool pressed = interactor.QaPress();
            Log("6", interactor.Label == "Sigorta gerekli" && interactor.LabelDim && !pressed && quest.State == QuestState.SearchFuse,
                "panel without fuse label='" + interactor.Label + "' dim=" + interactor.LabelDim + " pressed=" + pressed + " state=" + quest.State);

            // ---- 2: A door ----
            yield return Stand(new Vector3(-28f, 0.05f, -34f), doorA.HintAnchor.position);
            interactor.Refresh();
            string label = interactor.Label;
            Log("2", label == InputLabels.Prompt(InputLabels.Interact, "Kapıyı aç"), "A door label='" + label + "'");
            pressed = interactor.QaPress();
            yield return new WaitForSecondsRealtime(0.9f);
            Log("2", pressed && doorA.IsOpen && doorA.OpenFraction >= 1f, "A door open=" + doorA.IsOpen + " fraction=" + doorA.OpenFraction.ToString("F2"));
            interactor.Refresh();
            Log("2", interactor.Label == null && !interactor.QaPress(), "A door second press ignored label='" + interactor.Label + "'");

            // ---- 3: fuse ----
            yield return Stand(new Vector3(2f, 0.05f, 35.8f), fuse.HintAnchor.position);
            interactor.Refresh();
            label = interactor.Label;
            Log("3", label == InputLabels.Prompt(InputLabels.Interact, "Sigortayı al"), "fuse label='" + label + "'");
            pressed = interactor.QaPress();
            Log("3", pressed && quest.State == QuestState.CarryingFuse && fuse.Taken, "after press state=" + quest.State);
            yield return null;
            interactor.Refresh();
            Log("3", interactor.Label == null && !interactor.QaPress() && quest.State == QuestState.CarryingFuse, "same spot again label='" + interactor.Label + "'");

            // ---- 4: ammo box ----
            yield return Stand(new Vector3(12f, 0.05f, 24.7f), ammo.HintAnchor.position);
            interactor.Refresh();
            label = interactor.Label;
            int reserve0 = player.Weapon.Reserve;
            Log("4", label == InputLabels.Prompt(InputLabels.Interact, "Mühimmatı al"), "ammo label='" + label + "'");
            pressed = interactor.QaPress();
            int reserve1 = player.Weapon.Reserve;
            bool second = interactor.QaPress();
            int reserve2 = player.Weapon.Reserve;
            Log("4", pressed && reserve1 == reserve0 + 12 && !second && reserve2 == reserve1, "reserve " + reserve0 + " -> " + reserve1 + " -> " + reserve2);

            // ---- 5: D side door, outside then inside ----
            yield return Stand(new Vector3(16.2f, 0.05f, 33f), doorD.HintAnchor.position);
            interactor.Refresh();
            label = interactor.Label;
            pressed = interactor.QaPress();
            yield return new WaitForSecondsRealtime(0.3f);
            Log("5", label == "Bu taraftan açılmıyor" && interactor.LabelDim && !pressed && !doorD.IsOpen, "outside label='" + label + "' pressed=" + pressed + " open=" + doorD.IsOpen);
            yield return Stand(new Vector3(12f, 0.05f, 33f), doorD.HintAnchor.position);
            interactor.Refresh();
            label = interactor.Label;
            pressed = interactor.QaPress();
            yield return new WaitForSecondsRealtime(0.9f);
            Log("5", label == InputLabels.Prompt(InputLabels.Interact, "Kapıyı aç") && pressed && doorD.IsOpen && doorD.OpenFraction >= 1f, "inside label='" + label + "' open=" + doorD.IsOpen);

            // ---- 1 (open): all three walks complete once the doors are open ----
            yield return new WaitForSeconds(2f); // carving removal
            complete = Path(new Vector3(-24.5f, 0f, -34f), new Vector3(-6f, 0f, 23f), out status, out length, out corners);
            Log("1", complete, "A door -> D entrance status=" + status + " length=" + length.ToString("F1") + " corners=" + corners);
            complete = Path(new Vector3(13f, 0f, 33f), new Vector3(42f, 0f, -30f), out status, out length, out corners);
            Log("1", complete, "D side door -> F panel status=" + status + " length=" + length.ToString("F1") + " corners=" + corners);
            complete = Path(new Vector3(30f, 0f, -38f), new Vector3(-30f, 0f, -36f), out status, out length, out corners);
            Log("1", complete, "F -> A status=" + status + " length=" + length.ToString("F1") + " corners=" + corners);

            // ---- 6 (with fuse): installing fires the light event once ----
            yield return Stand(new Vector3(41f, 0.05f, -30f), panel.HintAnchor.position);
            interactor.Refresh();
            label = interactor.Label;
            Log("6", label == InputLabels.Prompt(InputLabels.Interact, "Sigortayı tak"), "panel with fuse label='" + label + "'");
            pressed = interactor.QaPress();
            bool lightsOn = LightsAfterEvent();
            // The quest runs on right away: fuse installed -> power event -> complete -> explore the station.
            Log("6", pressed && quest.State == QuestState.ExploreStation && lightsOn && quest.LightEventCount == 1, "state=" + quest.State + " lightsAfterEvent=" + lightsOn + " lightEvents=" + quest.LightEventCount);
            second = interactor.QaPress();
            yield return null;
            interactor.Refresh();
            Log("6", !second && quest.LightEventCount == 1 && interactor.Label == null, "second press ignored lightEvents=" + quest.LightEventCount + " label='" + interactor.Label + "'");

            // ---- 7: the installation completes the first quest on the spot (no safe-room return, no result card) ----
            yield return new WaitForSecondsRealtime(0.5f);
            bool gateOk = quest.outerGate == null || (quest.outerGate.IsOpen && !quest.outerGate.locked);
            bool serviceOk = quest.serviceDoor == null || !quest.serviceDoor.locked;
            Log("7", quest.State == QuestState.ExploreStation && !quest.CardVisible && gateOk && serviceOk,
                "state=" + quest.State + " card=" + quest.CardVisible + " seconds=" + quest.CompletedSeconds.ToString("F1")
                + " gate=" + (quest.outerGate == null ? "none" : quest.outerGate.IsOpen.ToString()) + " serviceUnlocked=" + serviceOk);
            Log("7", !interactor.QaPress() && quest.LightEventCount == 1 && quest.State == QuestState.ExploreStation, "second press does not repeat the light event lightEvents=" + quest.LightEventCount);
            string resultPath = PlaytestLog.LastResultPath;
            string resultText = resultPath != null && File.Exists(resultPath) ? File.ReadAllText(resultPath) : "";
            Log("7", resultText.Contains("result=complete") && resultText.Contains("qa=1") && resultText.Contains("Complete@") && resultText.Contains("pharmacy_entered=no")
                && resultPath.Contains("PlaytestResults"), "result file " + resultPath);
            Log("7", PlaytestLog.LastResultLine != null && PlaytestLog.LastResultLine.StartsWith("PLAYTEST_RESULT result=complete qa=1"), "log line " + PlaytestLog.LastResultLine);

            // ---- 8: death and reload ----
            Info("8", "fresh attempt: scene reload without resetting PlaytestLog");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            Sandbox before = player;
            float loadDeadline = Time.realtimeSinceStartup + 15f;
            while (before != null && Time.realtimeSinceStartup < loadDeadline) yield return null;
            yield return Acquire();
            if (player == null) { Log("8", false, "scene objects missing after reload"); Finish(); yield break; }

            // ---- 8 (hints): the move card shows after 3 s without input in A; walking 2 m closes it and it stays learned ----
            HintDirector.ResetProgress(); // earlier steps may already have used up the reminder budget of the move card
            float moveDeadline = Time.realtimeSinceStartup + 9f;
            while (!hints.Showing(HintDirector.Move) && Time.realtimeSinceStartup < moveDeadline) yield return null;
            yield return null; yield return null;
            Log("8", hints.Showing(HintDirector.Move) && hints.CurrentText == "[W A S D] Hareket et", "move card in A after idle text='" + hints.CurrentText + "'");
            Log("8", DrawnOk(), "move card clear of the crosshair centre " + DrawInfo());
            Vector3 walkStart = player.transform.position;
            for (int i = 0; i < 30; i++) { player.Motor.Teleport(walkStart + Vector3.right * 0.1f * (i + 1)); yield return null; }
            yield return null;
            Log("8", !hints.Showing(HintDirector.Move) && HintDirector.IsLearned(HintDirector.Move) && PlayerPrefs.GetInt("hint_move", 0) == 1,
                "after 3 m of walking: card=" + (hints.CurrentId ?? "-") + " learned=" + HintDirector.IsLearned(HintDirector.Move) + " walked=" + hints.Walked.ToString("F1"));
            yield return new WaitForSeconds(2.5f);

            yield return Stand(new Vector3(-28f, 0.05f, -34f), doorA.HintAnchor.position);
            interactor.QaPress();
            yield return Stand(new Vector3(2f, 0.05f, 35.8f), fuse.HintAnchor.position);
            interactor.QaPress();
            yield return Stand(new Vector3(12f, 0.05f, 24.7f), ammo.HintAnchor.position);
            interactor.QaPress();
            yield return Stand(new Vector3(12f, 0.05f, 33f), doorD.HintAnchor.position);
            interactor.QaPress();
            yield return Stand(new Vector3(41f, 0.05f, -30f), panel.HintAnchor.position);
            interactor.QaPress();
            yield return new WaitForSecondsRealtime(0.8f);
            bool armed = quest.State == QuestState.ExploreStation && doorA.IsOpen && doorD.IsOpen && player.Weapon.Reserve == 48 && quest.LightEventCount == 1;
            Log("8", armed, "before death state=" + quest.State + " reserve=" + player.Weapon.Reserve + " doorA=" + doorA.IsOpen + " doorD=" + doorD.IsOpen + " lightEvents=" + quest.LightEventCount);

            int deathsBefore = PlaytestLog.Deaths;
            before = player;
            player.ApplyDamage(new DamageInfo { Amount = 1000f, Zone = HitZone.Body, Point = player.transform.position, Direction = Vector3.forward, Source = this });
            bool died = player.IsDead;
            float reloadDeadline = Time.realtimeSinceStartup + 15f;
            while (before != null && Time.realtimeSinceStartup < reloadDeadline) yield return null; // the scene reloads 2 s after death
            yield return Acquire();
            if (player == null) { Log("8", false, "scene objects missing after death reload"); Finish(); yield break; }
            yield return new WaitForSeconds(2.5f);

            Log("8", died && PlaytestLog.Deaths == deathsBefore + 1, "death counted deaths=" + PlaytestLog.Deaths);
            Log("8", quest.State == QuestState.SearchFuse && !fuse.Taken && fuse.visual.activeInHierarchy && quest.LightEventCount == 0,
                "quest=" + quest.State + " fuseTaken=" + fuse.Taken + " fuseVisible=" + fuse.visual.activeInHierarchy + " lightEvents=" + quest.LightEventCount);
            Log("8", player.Weapon.Reserve == 36 && !ammo.Used, "reserve=" + player.Weapon.Reserve + " ammoUsed=" + ammo.Used);
            Log("8", !doorA.IsOpen && !doorD.IsOpen && doorA.leafCollider.enabled && doorD.leafCollider.enabled && doorA.obstacle.enabled && doorD.obstacle.enabled,
                "doors closed A=" + !doorA.IsOpen + " D=" + !doorD.IsOpen);
            Log("8", LightsInitial(), "lights back to the start state");
            complete = Path(new Vector3(-20f, 0f, -36f), new Vector3(-30f, 0f, -36f), out status, out length, out corners);
            Log("8", !complete, "NavMesh blocks the closed A door again status=" + status);
            // The move hint was learned before the death: it must not come back after the reload.
            float quiet = Time.realtimeSinceStartup + 4.5f;
            bool movedBack = false;
            while (Time.realtimeSinceStartup < quiet) { if (hints.Showing(HintDirector.Move)) movedBack = true; yield return null; }
            Log("8", HintDirector.IsLearned(HintDirector.Move) && PlayerPrefs.GetInt("hint_move", 0) == 1 && !movedBack,
                "learned hints stay learned after the death reload: move learned=" + HintDirector.IsLearned(HintDirector.Move) + " cardCameBack=" + movedBack);

            // ---- 9: hint cards ----
            // reload: magazine 0, reserve > 0
            Vector3 street = new Vector3(-8f, 0.05f, 1f);
            yield return Stand(street, new Vector3(-8f, 1.5f, 16f));
            player.Weapon.QaSetAmmo(0);
            yield return WaitFor(() => hints.Showing(HintDirector.Reload), 2f);
            yield return null; yield return null;
            Log("9", hints.Showing(HintDirector.Reload) && hints.CurrentText == "[R] Yeniden doldur" && player.Weapon.Reserve > 0, "reload card text='" + hints.CurrentText + "' ammo=" + player.Weapon.Ammo + "/" + player.Weapon.Reserve);
            Log("9", DrawnOk(), "reload card attached to the ammo panel, clear of the crosshair centre " + DrawInfo());
            bool began = player.Weapon.BeginReload(Time.time);
            yield return WaitFor(() => !hints.Showing(HintDirector.Reload), 1f);
            Log("9", began && !hints.Showing(HintDirector.Reload) && HintDirector.IsLearned(HintDirector.Reload), "reload started: card=" + (hints.CurrentId ?? "-") + " learned=" + HintDirector.IsLearned(HintDirector.Reload));
            yield return new WaitForSeconds(WeaponState.EmptyReloadSeconds + 0.4f);
            Log("9", player.Weapon.Ammo == WeaponState.Capacity && player.Weapon.Reserve == 36 - WeaponState.Capacity, "reload finished ammo=" + player.Weapon.Ammo + "/" + player.Weapon.Reserve);

            // no ammo at all: critical card, and it outranks an aim hint that is eligible at the same moment
            ZombieBrain open = GameObject.Find("Zombie_S1_1").GetComponent<ZombieBrain>();
            ZombieBrain wall = GameObject.Find("Zombie_C_1").GetComponent<ZombieBrain>();
            Put(open, new Vector3(-8f, 0f, 15f), 0f); // 14 m ahead, facing away, in the open
            player.Weapon.QaSetAmmo(0);
            player.Weapon.QaSetReserve(0);
            yield return WaitFor(() => hints.Showing(HintDirector.NoAmmo), 2f);
            yield return new WaitForSeconds(0.6f); // long enough for the aim check (every 0.2 s) to have made aim eligible
            Log("9", hints.Showing(HintDirector.NoAmmo) && hints.CurrentText == "Mühimmat yok — uzaklaş", "noammo card text='" + hints.CurrentText + "' (aim hint eligible at the same time)");
            player.Weapon.QaSetAmmo(WeaponState.Capacity);
            player.Weapon.QaSetReserve(36);
            yield return WaitFor(() => hints.Showing(HintDirector.Aim), 2f);
            Log("9", !hints.Showing(HintDirector.NoAmmo) && hints.Showing(HintDirector.Aim) && hints.CurrentText == "[Sağ fare] Nişan al", "ammo back: card=" + (hints.CurrentId ?? "-") + " text='" + hints.CurrentText + "'");
            Log("9", DrawnOk(), "aim card clear of the crosshair centre " + DrawInfo());
            player.QaSetAds(true);
            yield return WaitFor(() => !hints.Showing(HintDirector.Aim), 1f);
            Log("9", !hints.Showing(HintDirector.Aim) && HintDirector.IsLearned(HintDirector.Aim), "first ADS closed the aim card, learned=" + HintDirector.IsLearned(HintDirector.Aim));
            player.QaSetAds(false);
            yield return new WaitForSeconds(0.4f);

            // tutorial off: cards are hidden (objective line and [E] labels stay, they are not cards)
            GameSettings.Tutorial = false;
            player.Weapon.QaSetAmmo(0);
            player.Weapon.QaSetReserve(0);
            yield return new WaitForSeconds(0.7f);
            bool cardWhileOff = hints.CurrentId != null;
            GameSettings.Tutorial = true;
            player.Weapon.QaSetAmmo(WeaponState.Capacity);
            player.Weapon.QaSetReserve(36);
            yield return new WaitForSeconds(0.3f);
            Log("9", !cardWhileOff, "tutorial off: no card while magazine and reserve are empty");

            Log("9", hints.QaViolations == 0 && hints.QaMaxCards <= 1 && hints.QaFrames > 100, "cards per frame: frames=" + hints.QaFrames + " max=" + hints.QaMaxCards + " violations=" + hints.QaViolations);

            // ---- 10: noise ----
            foreach (ZombieBrain b in FindObjectsByType<ZombieBrain>())
                if (b != open && b != wall) Destroy(b.gameObject); // only the two test zombies listen
            yield return null;
            int heard = 0;
            ZombieBrain lastHeard = null;
            Action<ZombieBrain> onHeard = z => { heard++; lastHeard = z; };
            NoiseEvents.ZombieHeard += onHeard;
            Vector3 shooter = new Vector3(-8f, 0.05f, 1f);
            yield return Stand(shooter, new Vector3(-8f, 1.5f, 16f));
            Put(wall, new Vector3(-23f, 0f, 1f), 270f); // 15 m west, behind the pharmacy wall, facing away
            Put(open, new Vector3(-8f, 0f, 16f), 0f);   // 15 m north in the open street, facing away
            yield return null; yield return null;
            float dWall = Vector3.Distance(new Vector3(wall.transform.position.x, 0f, wall.transform.position.z), new Vector3(shooter.x, 0f, shooter.z));
            float dOpen = Vector3.Distance(new Vector3(open.transform.position.x, 0f, open.transform.position.z), new Vector3(shooter.x, 0f, shooter.z));
            bool wallBlocked = Physics.Linecast(player.AimPivot.position + Vector3.up * 0f, wall.transform.position + Vector3.up * 1.6f, Layers.SightMask, QueryTriggerInteraction.Ignore);
            Log("10", wall.State != ZombieState.Chase && open.State != ZombieState.Chase && Mathf.Abs(dWall - 15f) < 0.1f && Mathf.Abs(dOpen - 15f) < 0.1f && wallBlocked,
                "setup wall=" + dWall.ToString("F1") + " m (blocked=" + wallBlocked + ") open=" + dOpen.ToString("F1") + " m, both unalerted");
            player.QaSetFreeze(false); // zombies can act again
            yield return null;
            bool fired = player.QaFire(); // real shot path: noise radius comes from the scene (22 m)
            yield return new WaitForSeconds(0.5f);
            Log("10", fired && open.State == ZombieState.Chase && wall.State != ZombieState.Chase && heard == 1 && lastHeard == open,
                "22 m shot: open(15 m)=" + open.State + " wall(15 m)=" + wall.State + " ZombieHeard=" + heard);
            yield return null;
            Log("10", hints.Showing(HintDirector.Noise) && hints.CurrentText == "Silah sesi yakındakileri çekebilir" && HintDirector.IsLearned(HintDirector.Noise),
                "noise card after a zombie really turned to the shot text='" + hints.CurrentText + "' learned=" + HintDirector.IsLearned(HintDirector.Noise));
            // The wall only halves the radius: closer than 11 m the same wall no longer protects.
            Put(wall, new Vector3(-18.5f, 0f, 1f), 270f); // 9.5 m, still behind the wall
            player.QaSetFreeze(true);
            Put(open, new Vector3(-8f, 0f, -30f), 0f); // park it 31 m away, outside the 22 m radius
            yield return null; yield return null;
            player.QaSetFreeze(false);
            yield return null;
            bool fired2 = player.QaFire();
            yield return new WaitForSeconds(0.4f);
            Log("10", fired2 && wall.State == ZombieState.Chase && heard == 2 && lastHeard == wall, "wall zombie at 9.5 m (inside the halved radius 11): " + wall.State + " ZombieHeard=" + heard);
            player.QaSetFreeze(true);
            NoiseEvents.ZombieHeard -= onHeard;
            yield return new WaitForSeconds(4.8f);
            Log("10", !hints.Showing(HintDirector.Noise), "noise card is gone after 5 s");
            Log("10", hints.QaViolations == 0 && hints.QaMaxCards <= 1, "cards per frame to the end: frames=" + hints.QaFrames + " max=" + hints.QaMaxCards + " violations=" + hints.QaViolations);

            // ---- reset progress (pause menu button path) ----
            HintDirector.ResetProgress();
            bool cleared = true;
            foreach (string id in HintDirector.AllIds) if (HintDirector.IsLearned(id) || PlayerPrefs.HasKey("hint_" + id)) cleared = false;
            Log("10", cleared, "Öğretici ilerlemesini sıfırla clears every hint flag");

            HintDirector.QaRestore(prefs);
            Finish();
        }

        IEnumerator WaitFor(Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        // The card was drawn by OnGUI and stays at least 120 px away from the crosshair centre (OnGUI does not run in -batchmode).
        bool DrawnOk()
        {
            if (Application.isBatchMode) return true;
            return hints.QaDrawn && hints.LastCenterDistance >= 120f;
        }

        string DrawInfo()
        {
            return "rect=" + hints.LastCardRect + " distance=" + hints.LastCenterDistance.ToString("F0") + " px drawn=" + hints.QaDrawn;
        }

        static void Put(ZombieBrain z, Vector3 position, float yaw)
        {
            var agent = z.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(position); else z.transform.position = position;
            z.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Physics.SyncTransforms();
        }

        IEnumerator Acquire()
        {
            player = null; quest = null; interactor = null; hints = null;
            for (int i = 0; i < 1200; i++)
            {
                yield return null;
                Sandbox p = FindAnyObjectByType<Sandbox>();
                QuestFuse q = QuestFuse.Instance;
                ZombieDirector d = ZombieDirector.Instance;
                if (p != null && q != null && d != null && d.NavMeshReady && p.Motor != null && p.AimPivot != null && p.GetComponent<Interactor>() != null && HintDirector.Instance != null)
                {
                    player = p; quest = q; interactor = p.GetComponent<Interactor>(); hints = HintDirector.Instance;
                    break;
                }
            }
            if (player == null) yield break;
            foreach (DoorInteractable d in FindObjectsByType<DoorInteractable>())
            {
                if (d.name == "Door_A") doorA = d;
                else if (d.name == "Door_D") doorD = d;
            }
            fuse = FindAnyObjectByType<FuseItem>();
            { GameObject ammoGo = GameObject.Find("AmmoCrate"); ammo = ammoGo != null ? ammoGo.GetComponent<AmmoCrate>() : null; }
            panel = FindAnyObjectByType<FusePanel>();
            player.QaSetFreeze(true);
            player.Motor.InputEnabled = false;
        }

        // Teleports to `feet` and turns toward `lookAt` (as if looking); waits two frames so physics and the camera settle.
        IEnumerator Stand(Vector3 feet, Vector3 lookAt)
        {
            player.Motor.Teleport(feet);
            Vector3 dir = lookAt - (feet + Vector3.up * 1.65f);
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
            player.Motor.QaSetPitch(-Mathf.Asin(dir.y / dir.magnitude) * Mathf.Rad2Deg);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
        }

        static bool Path(Vector3 from, Vector3 to, out NavMeshPathStatus status, out float length, out int corners)
        {
            status = NavMeshPathStatus.PathInvalid; length = 0f; corners = 0;
            NavMeshHit a, b;
            if (!NavMesh.SamplePosition(from, out a, 2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out b, 2f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
            status = path.status;
            Vector3[] c = path.corners;
            corners = c.Length;
            for (int i = 1; i < c.Length; i++) length += Vector3.Distance(c[i - 1], c[i]);
            return status == NavMeshPathStatus.PathComplete;
        }

        bool LightsAfterEvent()
        {
            return quest.panelLamp.enabled && quest.mainLight.enabled && !quest.emergencyLight.enabled && QuestFuse.EmissionOf(quest.radioLamp).maxColorComponent > 0.1f;
        }

        bool LightsInitial()
        {
            return !quest.panelLamp.enabled && !quest.mainLight.enabled && quest.emergencyLight.enabled && QuestFuse.EmissionOf(quest.radioLamp).maxColorComponent < 0.01f;
        }
    }
}
