using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zombosland
{
    // --qa-hands (IlkIkmal, windowed with real rendering): saves PNGs of each hand clip at fixed frames to Build/QaHands/, then checks the real flows:
    // a pickup commits once at the clip's contact moment, firing and reload wait for it, a cancelled pickup changes nothing, carriers swap correctly.
    // Log lines: QA_HANDS <id> PASS|FAIL ..., QA_HANDS_DONE pass= fail=. Saving is off (SaveSystem.Enabled).
    public class QaHands : MonoBehaviour
    {
        struct Shot { public string state, label; public float normalized; public HandCarrier carrier; }

        static readonly Shot[] Shots =
        {
            new Shot { state = "Handgun_Pickup", label = "gun_pickup_05", normalized = 0.33f, carrier = HandCarrier.Pistol },
            new Shot { state = "Handgun_Pickup", label = "gun_pickup_07", normalized = 0.50f, carrier = HandCarrier.Pistol },
            new Shot { state = "Handgun_Pickup", label = "gun_pickup_10", normalized = 0.75f, carrier = HandCarrier.Pistol },
            new Shot { state = "Hands_Idle", label = "hands_idle", normalized = 0.20f, carrier = HandCarrier.Hands },
            new Shot { state = "Hands_Pickup", label = "hands_pickup_07", normalized = 0.50f, carrier = HandCarrier.Hands },
            new Shot { state = "Bat_Idle", label = "bat_idle", normalized = 0.50f, carrier = HandCarrier.Bat },
            new Shot { state = "Bat_Draw", label = "bat_draw_05", normalized = 0.24f, carrier = HandCarrier.Bat },
            new Shot { state = "Bat_Draw", label = "bat_draw_09", normalized = 0.47f, carrier = HandCarrier.Bat },
            new Shot { state = "Bat_Swing", label = "bat_swing_05", normalized = 0.20f, carrier = HandCarrier.Bat },
            new Shot { state = "Bat_Swing", label = "bat_swing_09", normalized = 0.40f, carrier = HandCarrier.Bat },
            new Shot { state = "Bat_Pickup", label = "bat_pickup_07", normalized = 0.50f, carrier = HandCarrier.Bat },
            new Shot { state = "Handgun_OpenContainer", label = "gun_container_09", normalized = 0.33f, carrier = HandCarrier.Pistol },
            new Shot { state = "Handgun_OpenContainer", label = "gun_container_15", normalized = 0.58f, carrier = HandCarrier.Pistol },
            new Shot { state = "Handgun_Bandage", label = "bandage_18", normalized = 0.33f, carrier = HandCarrier.Pistol },
            new Shot { state = "Handgun_Bandage", label = "bandage_28", normalized = 0.53f, carrier = HandCarrier.Pistol },
            new Shot { state = "Handgun_Lower", label = "gun_lowered", normalized = 0.99f, carrier = HandCarrier.Pistol },
            new Shot { state = "Handgun_Craft", label = "craft", normalized = 0.30f, carrier = HandCarrier.Pistol },
            new Shot { state = "Pistol_Draw", label = "pistol_draw_mid", normalized = 0.35f, carrier = HandCarrier.Pistol },
        };

        Sandbox player;
        ViewmodelPresenter presenter;
        Interactor interactor;
        PlayerSurvival ps;
        string dir;
        int pass, fail, saved;

        void Log(string id, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            Debug.Log("QA_HANDS " + id + (ok ? " PASS " : " FAIL ") + detail);
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            if (!Application.isBatchMode) Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            Sandbox.InstantHands = false;
            player = GetComponent<Sandbox>();
            interactor = GetComponent<Interactor>();
            for (int i = 0; i < 600; i++)
            {
                presenter = GetComponentInChildren<ViewmodelPresenter>();
                ps = PlayerSurvival.Instance;
                if (player != null && player.Motor != null && player.AimPivot != null && presenter != null && presenter.armsAnimator != null && ps != null && ps.Bag != null) break;
                yield return null;
            }
            if (presenter == null || presenter.armsAnimator == null) { Log("setup", false, "presenter / animator missing"); Finish(); yield break; }
            yield return new WaitForSecondsRealtime(1.0f);

            dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "QaHands");
            Directory.CreateDirectory(dir);
            player.QaSetFreeze(true);
            player.Motor.InputEnabled = false;
            player.Motor.QaSetPitch(12f);

            yield return Poses();
            presenter.SetCarrier(HandCarrier.Pistol, true);
            presenter.armsAnimator.speed = 1f;
            yield return new WaitForSeconds(0.4f);
            yield return Flows();
            Finish();
        }

        IEnumerator Poses()
        {
            Animator arms = presenter.armsAnimator;
            Log("states", AllStatesPresent(arms), "every hand action state exists in the controller");
            arms.speed = 0f;
            foreach (Shot s in Shots)
            {
                presenter.SetCarrier(s.carrier, false);
                presenter.HideHeldForQa(s.carrier);
                arms.Play(s.state, 0, s.normalized);
                arms.Update(0f);
                yield return new WaitForEndOfFrame();
                Capture(s.label);
            }
            arms.speed = 1f;
        }

        bool AllStatesPresent(Animator a)
        {
            foreach (Shot s in Shots) if (!a.HasState(0, Animator.StringToHash(s.state))) { Debug.Log("QA_HANDS missing state " + s.state); return false; }
            return a.HasState(0, Animator.StringToHash("Pistol_Holster")) && a.HasState(0, Animator.StringToHash("Bat_Idle"));
        }

        // A throwaway IHandInteractable with counters, so the flow is checked without any scene content.
        sealed class Probe : IHandInteractable
        {
            public int uses;
            public bool usable = true;
            readonly Transform anchor;
            public Probe(Transform anchor) { this.anchor = anchor; }
            public HandActionKind HandKind { get { return HandActionKind.Pickup; } }
            public bool RequiresLineOfSight { get { return false; } }
            public string Verb { get { return "Test"; } }
            public bool CanInteract(Sandbox p) { return usable; }
            public void Interact(Sandbox p) { uses++; }
            public Transform HintAnchor { get { return anchor; } }
        }

        IEnumerator Flows()
        {
            WeaponState w = player.Weapon;
            var probe = new Probe(player.AimPivot);
            float started = Time.time;

            // Normal pickup: one commit at the contact moment, weapon input off during the clip, movement stays on.
            bool begun = player.TryBeginItemAction(probe, interactor);
            player.Motor.InputEnabled = true;
            int ammo = w.Ammo;
            bool second = player.TryBeginItemAction(probe, interactor);
            float commitAt = -1f, endAt = -1f;
            while (Time.time - started < 0.7f)
            {
                if (commitAt < 0f && probe.uses > 0) commitAt = Time.time - started;
                if (endAt < 0f && commitAt > 0f && !player.HandsBusy) endAt = Time.time - started;
                if (player.HandsBusy) { player.QaFireIfAllowed(); }
                yield return null;
            }
            Log("pickup_once", begun && !second && probe.uses == 1, "begun=" + begun + " second=" + second + " uses=" + probe.uses);
            Log("pickup_timing", Mathf.Abs(commitAt - ViewmodelPresenter.PickupCommit) < 0.08f && Mathf.Abs(endAt - ViewmodelPresenter.PickupSeconds) < 0.08f, "commitAt=" + commitAt.ToString("F3") + " endAt=" + endAt.ToString("F3"));
            Log("pickup_no_fire", w.Ammo == ammo, "ammo " + ammo + " -> " + w.Ammo + " while the hands were busy");
            Log("pickup_moves", player.Motor.InputEnabled, "input stays enabled during a pickup");

            // Cancelled before contact: nothing changes.
            probe.uses = 0;
            player.TryBeginItemAction(probe, interactor);
            yield return new WaitForSeconds(0.05f);
            player.CancelHandAction("qa");
            yield return new WaitForSeconds(0.4f);
            Log("cancel_before_commit", probe.uses == 0 && !player.HandsBusy, "uses=" + probe.uses + " busy=" + player.HandsBusy);

            // Target invalid at the commit moment: refused, nothing applied.
            probe.usable = true;
            probe.uses = 0;
            player.TryBeginItemAction(probe, interactor);
            yield return null;
            probe.usable = false;
            yield return new WaitForSeconds(0.45f);
            Log("commit_recheck", probe.uses == 0 && !player.HandsBusy, "uses=" + probe.uses + " busy=" + player.HandsBusy);
            probe.usable = true;

            // Reload blocks the action, including the clip tail.
            w.QaSetReserve(24);
            w.QaSetAmmo(3);
            bool reloadStarted = w.BeginReload(Time.time);
            yield return new WaitForSeconds(0.05f);
            int usesBefore = probe.uses;
            bool during = player.TryBeginItemAction(probe, interactor);
            yield return new WaitForSeconds(w.ReloadDuration + 0.05f);
            bool tail = player.HandActionBlockedReason() != null && !w.Reloading;
            while (presenter.ReloadAnimating) yield return null;
            Log("reload_blocks", reloadStarted && !during && probe.uses == usesBefore, "started=" + reloadStarted + " pickupDuringReload=" + during);
            Log("reload_tail_protected", tail, "blocked while the reload clip tail plays");
            yield return new WaitForSeconds(0.3f);

            // Carrier swaps: pistol -> empty hands -> pistol, states and pistol renderers.
            player.ToggleHolster();
            bool busy = player.HandsBusy;
            yield return new WaitForSeconds(1.2f);
            Log("holster", busy && player.Carrier == HandCarrier.Hands && !presenter.PistolVisible && !player.HandsBusy, "carrier=" + player.Carrier + " pistolVisible=" + presenter.PistolVisible);
            player.ToggleHolster();
            yield return new WaitForSeconds(1.2f);
            Log("draw", player.Carrier == HandCarrier.Pistol && presenter.PistolVisible && player.WeaponReady, "carrier=" + player.Carrier + " ready=" + player.WeaponReady);

            // Bandage: the item is consumed at the commit moment, not before.
            ItemDef bandage = ps.database != null ? ps.database.FindItem("bandage") : null;
            if (bandage != null)
            {
                ps.Bag.Clear();
                ps.Bag.Add(bandage, 2);
                player.RestoreHealth(50f);
                bool bandageStarted = player.TryBeginBandage(bandage);
                float bandageT = Time.time;
                yield return new WaitForSeconds(ViewmodelPresenter.BandageCommit - 0.15f);
                bool early = ps.Bag.Count(bandage) == 2 && Mathf.Approximately(player.Health, 50f);
                yield return new WaitForSeconds(0.3f);
                bool applied = ps.Bag.Count(bandage) == 1 && player.Health > 50f;
                yield return new WaitForSeconds(Mathf.Max(0f, ViewmodelPresenter.BandageSeconds - (Time.time - bandageT)) + 0.2f);
                Log("bandage_commit", bandageStarted && early && applied && !player.HandsBusy, "before=" + early + " after=" + applied + " health=" + player.Health);
                ps.Bag.Clear();
            }

            // Bat and axe: real swaps through the item path.
            foreach (string id in new[] { "bat", "axe" })
            {
                ItemDef item = ps.database != null ? ps.database.FindItem(id) : null;
                if (item == null) { Log("swap_" + id, false, "item missing"); continue; }
                ps.Bag.Clear();
                ps.Bag.Add(item, 1);
                ps.AssignQuick(1, item);
                ps.UseQuick(1);
                yield return new WaitForSeconds(0.2f);
                Capture(id + "_swap_early");
                yield return new WaitForSeconds(1.0f);
                bool ok = player.MeleeEquipped && player.MeleeItem == item && !presenter.PistolVisible && !player.HandsBusy;
                player.QaMeleeSwing();
                yield return new WaitForSeconds(0.28f);
                Capture(id + "_swing");
                yield return new WaitForSeconds(0.8f);
                ps.UseQuick(0);
                yield return new WaitForSeconds(1.2f);
                Log("swap_" + id, ok && !player.MeleeEquipped && presenter.PistolVisible, "equipped=" + ok + " backToPistol=" + !player.MeleeEquipped);
                ps.Bag.Clear();
                ps.AssignQuick(1, null);
            }
        }

        void Capture(string name)
        {
            if (Application.isBatchMode) return;
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            QaPng.Write(Path.Combine(dir, name + ".png"), tex);
            Destroy(tex);
            saved++;
        }

        void Finish()
        {
            Debug.Log("QA_HANDS pngs=" + saved);
            Debug.Log("QA_HANDS_DONE pass=" + pass + " fail=" + fail);
            GameQuit.Request();
        }
    }
}
