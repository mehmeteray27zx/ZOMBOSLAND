using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zombosland
{
    // --qa-reload (IlkIkmal, windowed with real rendering): saves PNGs of the empty reload at fixed clip frames to Build/QaReload/, then runs a real empty
    // reload through WeaponState and measures when the slide closes, when the camera shake starts, its peak, when ammo lands and when the arms return to the pose.
    // Log lines: QA_RELOAD ..., QA_RELOAD_RESULT PASS|FAIL <reason>, QA_RELOAD_DONE. Saving is off (SaveSystem.Enabled).
    public class QaReload : MonoBehaviour
    {
        const float SlideTravel = 0.02f, ShakeVisibleDegrees = 0.05f; // the slide is "closed" once it has moved 2 cm from its locked position
        // 1-based Blender frames.
        static readonly int[] Frames = { 34, 38, 41, 44, 46, 47, 48, 52, 62 };

        Sandbox player;
        ViewmodelPresenter presenter;
        string dir;
        int saved;
        readonly List<string> failures = new List<string>();

        void Fail(string reason) { failures.Add(reason); }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Sandbox.InstantHands = true; // hand actions apply at once in scripted runs
            if (!Application.isBatchMode) Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            player = GetComponent<Sandbox>();
            for (int i = 0; i < 600; i++)
            {
                presenter = GetComponentInChildren<ViewmodelPresenter>();
                if (player != null && player.Motor != null && player.AimPivot != null && presenter != null && presenter.armsAnimator != null) break;
                yield return null;
            }
            if (presenter == null || presenter.armsAnimator == null) { Finish("presenter / animator missing"); yield break; }
            yield return new WaitForSecondsRealtime(1.0f);

            dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "QaReload");
            Directory.CreateDirectory(dir);
            player.QaSetFreeze(true);
            player.Motor.InputEnabled = false;
            player.Motor.QaSetPitch(0f);
            Camera cam = player.GetComponentInChildren<Camera>();
            CameraEffects effects = player.GetComponentInChildren<CameraEffects>();
            Debug.Log("QA_RELOAD start arms=" + presenter.ArmsSet + " shakeSetting=" + GameSettings.CameraShake + " fov=" + (cam != null ? cam.fieldOfView : 0f) + " batch=" + Application.isBatchMode);
            if (presenter.ArmsSet != "survivalist") Fail("Survivalist arms set is not active");
            if (effects == null) { Finish("CameraEffects missing"); yield break; }
            if (GameSettings.CameraShake <= 0f) Fail("camera shake setting is 0 on this machine");

            Animator arms = presenter.armsAnimator, gun = presenter.pistolAnimator;
            if (gun == null) { Finish("pistol animator missing"); yield break; }

            // Poses: both animators frozen on the clip frame.
            arms.speed = gun.speed = 0f;
            foreach (int frame in Frames)
            {
                float normalized = (frame - 1) / 82f;
                arms.Play("ReloadEmpty", 0, 0f);
                gun.Play("ReloadEmpty", 0, 0f);
                arms.SetFloat("ReloadTime", normalized);
                gun.SetFloat("ReloadTime", normalized);
                arms.Update(0f);
                gun.Update(0f);
                yield return new WaitForEndOfFrame();
                Capture("reload_" + frame);
            }
            arms.SetFloat("ReloadTime", 0f);
            gun.SetFloat("ReloadTime", 0f);
            arms.Play("Pose", 0, 0f);
            gun.Play("Idle", 0, 0f);
            arms.speed = gun.speed = 1f;
            yield return new WaitForSeconds(0.5f);

            // Real path: empty magazine, reload started through WeaponState, sampled every rendered frame.
            WeaponState weapon = player.Weapon;
            weapon.QaSetReserve(24);
            weapon.QaSetAmmo(0);
            yield return new WaitForSeconds(0.4f);
            Transform slide = FindDeep(gun.transform, "slide");
            if (slide == null) { Finish("slide transform missing"); yield break; }

            float start = Time.time;
            if (!weapon.BeginReload(start)) { Finish("BeginReload refused"); yield break; }
            Debug.Log("QA_RELOAD begin fromEmpty=" + weapon.ReloadFromEmpty + " ammo=" + weapon.Ammo + " reserve=" + weapon.Reserve);

            float slideClosed = -1f, shakeStart = -1f, peak = 0f, peakAt = 0f, shakeEnd = -1f, ammoAt = -1f, poseAt = -1f, before = 0f;
            Vector3 lockedSlide = slide.localPosition;
            bool lockedRead = false;
            int frames = 0;
            while (Time.time - start < 3.6f)
            {
                yield return new WaitForEndOfFrame();
                frames++;
                float t = Time.time - start;
                if (!lockedRead && t > 1f) { lockedSlide = slide.localPosition; lockedRead = true; }
                if (lockedRead && slideClosed < 0f && (slide.localPosition - lockedSlide).magnitude > SlideTravel) slideClosed = t;
                float angle = Quaternion.Angle(Quaternion.identity, effects.transform.localRotation);
                if (shakeStart < 0f)
                {
                    if (t < ViewmodelPresenter.ReloadEmptySlamSeconds - 0.04f) before = Mathf.Max(before, angle);
                    else if (angle > ShakeVisibleDegrees) shakeStart = t;
                }
                if (angle > peak) { peak = angle; peakAt = t; }
                if (shakeStart > 0f && angle > ShakeVisibleDegrees) shakeEnd = t; // last visible sample
                if (ammoAt < 0f && weapon.Ammo == WeaponState.Capacity) ammoAt = t;
                if (poseAt < 0f && t > 2f && arms.GetCurrentAnimatorStateInfo(0).IsName("Pose") && !arms.IsInTransition(0)) poseAt = t;
            }

            Debug.Log("QA_RELOAD measured frames=" + frames + " slideClosed=" + slideClosed.ToString("F3") + " expected=" + ViewmodelPresenter.ReloadEmptySlamSeconds.ToString("F3")
                + " shakeBefore=" + before.ToString("F3") + " shakeStart=" + shakeStart.ToString("F3") + " peakDeg=" + peak.ToString("F3") + " peakAt=" + peakAt.ToString("F3") + " shakeEnd=" + shakeEnd.ToString("F3")
                + " ammoAt=" + ammoAt.ToString("F3") + " ammo=" + weapon.Ammo + " reserve=" + weapon.Reserve + " poseAt=" + poseAt.ToString("F3"));

            float frameTime = 3.6f / Mathf.Max(1, frames);
            float slam = ViewmodelPresenter.ReloadEmptySlamSeconds;
            float window = Mathf.Max(0.1f, frameTime * 3f);
            if (slideClosed < 0f || Mathf.Abs(slideClosed - slam) > window) Fail("slide closed at " + slideClosed + " instead of " + slam);
            if (before > 0.02f) Fail("camera moved before the slam: " + before);
            if (shakeStart < 0f || shakeStart < slam - frameTime || shakeStart > slam + window) Fail("shake started at " + shakeStart);
            if (GameSettings.CameraShake >= 1f && (peak < 0.3f || peak > 1.5f)) Fail("shake peak out of range: " + peak);
            if (Mathf.Abs(ammoAt - WeaponState.EmptyReloadSeconds) > window) Fail("ammo landed at " + ammoAt);
            if (weapon.Ammo != WeaponState.Capacity || weapon.Reserve != 12) Fail("ammo totals wrong: " + weapon.Ammo + "/" + weapon.Reserve);
            if (poseAt < 0f || poseAt > 3.2f) Fail("arms did not return to the pose: " + poseAt);
            Finish(null);
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
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

        void Finish(string earlyFailure)
        {
            if (earlyFailure != null) Fail(earlyFailure);
            Debug.Log("QA_RELOAD pngs=" + saved);
            Debug.Log(failures.Count == 0 ? "QA_RELOAD_RESULT PASS" : "QA_RELOAD_RESULT FAIL " + string.Join("; ", failures.ToArray()));
            Debug.Log("QA_RELOAD_DONE");
            GameQuit.Request();
        }
    }
}
