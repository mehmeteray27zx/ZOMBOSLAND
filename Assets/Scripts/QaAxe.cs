using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zombosland
{
    // --qa-axe (IlkIkmal, windowed with real rendering): equips the axe, freezes the arms animator and saves one PNG per pose to Build/QaAxe/
    // (idle, swing_12 ... swing_50 = frame/60 of the 1 s swing clip). For idle and swing_21 the bone and axe-mesh positions are measured in the
    // Arms_Survivalist local space and compared with the Blender values (tolerance 0.5 cm). Then the real game path runs: QaMeleeSwing(), return to the pistol.
    // Log lines: QA_AXE <label> <name> <measured> <expected> <diff>, QA_AXE_RESULT PASS|FAIL <reason>, QA_AXE_DONE. Saving is off (SaveSystem.Enabled).
    public class QaAxe : MonoBehaviour
    {
        const float Tolerance = 0.005f, ClipFps = 60f;

        struct Expected
        {
            public string name; public Vector3 position;
            public Expected(string name, float x, float y, float z) { this.name = name; position = new Vector3(x, y, z); }
        }

        static readonly Expected[] IdleExpected =
        {
            new Expected("hand_r", 0.14608f, -0.04998f, 0.25698f),
            new Expected("hand_l", 0.13697f, -0.25150f, 0.21557f),
            new Expected("lowerarm_r", 0.12821f, -0.10377f, -0.00675f),
            new Expected("axe_mesh", 0.15870f, -0.04035f, 0.43307f)
        };

        static readonly Expected[] Swing21Expected =
        {
            new Expected("hand_r", 0.08593f, -0.18229f, 0.33377f),
            new Expected("hand_l", 0.26879f, -0.17690f, 0.23922f),
            new Expected("lowerarm_r", 0.14717f, -0.27813f, 0.08917f),
            new Expected("axe_mesh", 0.09340f, -0.32416f, 0.43902f)
        };

        static readonly int[] SwingFrames = { 12, 16, 19, 21, 24, 36, 50 };

        Sandbox player;
        PlayerSurvival ps;
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
                ps = player != null ? player.Survival : null;
                presenter = GetComponentInChildren<ViewmodelPresenter>();
                if (player != null && player.Motor != null && player.AimPivot != null && ps != null && ps.Bag != null && ps.database != null && presenter != null) break;
                yield return null;
            }
            if (ps == null || ps.database == null || presenter == null) { Finish("PlayerSurvival / database / presenter missing (run in the IlkIkmal scene)"); yield break; }
            yield return new WaitForSecondsRealtime(1.0f);

            dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "QaAxe"); // next to the player exe: Build/QaAxe
            Directory.CreateDirectory(dir);
            player.QaSetFreeze(true);
            player.Motor.InputEnabled = false;
            player.Motor.QaSetPitch(0f);
            Debug.Log("QA_AXE start save=" + SaveSystem.Enabled + " arms=" + presenter.ArmsSet + " batch=" + Application.isBatchMode + " dir=" + dir);

            ItemDef axeItem = ps.database.FindItem("axe");
            if (axeItem == null) { Finish("item 'axe' missing from the database (rebuild the scene assets)"); yield break; }
            if (presenter.ArmsSet != "survivalist" || presenter.survivalist == null || presenter.survivalist.root == null) { Finish("Survivalist arms set is not active"); yield break; }
            if (ps.Bag.Count(axeItem) <= 0) ps.Bag.Add(axeItem, 1);
            ps.AssignQuick(2, axeItem);
            bool used = ps.UseQuick(2);
            yield return null;
            if (!used || !player.MeleeEquipped || !player.AxeInHand || !presenter.AxeMode) { Finish("axe did not equip: used=" + used + " melee=" + player.MeleeEquipped + " axeInHand=" + player.AxeInHand + " axeMode=" + presenter.AxeMode); yield break; }
            yield return new WaitForSecondsRealtime(0.5f);

            Animator animator = presenter.armsAnimator;
            Transform armsRoot = presenter.survivalist.root.transform;
            GameObject axeObject = presenter.survivalist.axe;
            animator.speed = 0f;

            // Idle.
            animator.Play("AxeIdle", 0, 0f);
            animator.Update(0f);
            yield return new WaitForEndOfFrame();
            Measure("idle", armsRoot, axeObject, IdleExpected);
            Capture("idle");

            // Swing frames: normalized time = frame / 60 (the clip is 61 frames at 60 fps = 1.000 s).
            foreach (int frame in SwingFrames)
            {
                animator.Play("AxeSwing", 0, frame / ClipFps);
                animator.Update(0f);
                yield return new WaitForEndOfFrame();
                string label = "swing_" + frame;
                if (frame == 21) Measure(label, armsRoot, axeObject, Swing21Expected);
                Capture(label);
            }

            // Real game path at normal speed.
            animator.Play("AxeIdle", 0, 0f);
            animator.Update(0f);
            animator.speed = 1f;
            yield return null;
            int swings0 = player.MeleeSwings;
            bool began = player.QaMeleeSwing();
            yield return new WaitForSeconds(1.2f);
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            bool backToIdle = st.IsName("AxeIdle") && !animator.IsInTransition(0);
            bool counted = player.MeleeSwings == swings0 + 1;
            Debug.Log("QA_AXE swing_path began=" + began + " swings " + swings0 + " -> " + player.MeleeSwings + " state=AxeIdle:" + st.IsName("AxeIdle") + " inTransition=" + animator.IsInTransition(0) + " swinging=" + player.MeleeSwinging);
            if (!began) Fail("QaMeleeSwing refused");
            if (!counted) Fail("MeleeSwings did not increase by 1");
            if (!backToIdle) Fail("not back in AxeIdle 1.2 s after the swing");

            // Back to the pistol (quick slot 0).
            ps.UseQuick(0);
            yield return new WaitForSeconds(0.4f);
            st = animator.GetCurrentAnimatorStateInfo(0);
            bool pose = st.IsName("Pose");
            bool axeOff = axeObject == null || !axeObject.activeSelf;
            Renderer gunRenderer = presenter.survivalist.pistolAnimator != null ? presenter.survivalist.pistolAnimator.GetComponentInChildren<Renderer>(true) : null;
            bool gunOn = gunRenderer != null && gunRenderer.enabled && presenter.PistolVisible;
            Debug.Log("QA_AXE pistol_back pose=" + pose + " axeInactive=" + axeOff + " gunRendererEnabled=" + gunOn + " axeMode=" + presenter.AxeMode + " melee=" + player.MeleeEquipped);
            if (!pose) Fail("state is not Pose 0.4 s after switching to the pistol");
            if (!axeOff) Fail("axe object still active after switching to the pistol");
            if (!gunOn) Fail("pistol renderer not enabled after switching to the pistol");
            if (presenter.AxeMode || player.MeleeEquipped) Fail("axe mode / melee still on after switching to the pistol");

            Finish(null);
        }

        // Positions in the local space of Arms_Survivalist; the axe mesh is its renderer bounds centre.
        void Measure(string label, Transform armsRoot, GameObject axeObject, Expected[] expected)
        {
            foreach (Expected e in expected)
            {
                Vector3 world;
                if (e.name == "axe_mesh")
                {
                    Renderer r = axeObject != null ? axeObject.GetComponent<Renderer>() : null;
                    if (r == null) { Debug.Log("QA_AXE " + label + " " + e.name + " missing"); Fail(label + " axe renderer missing"); continue; }
                    world = r.bounds.center;
                }
                else
                {
                    Transform bone = FindDeep(armsRoot, e.name);
                    if (bone == null) { Debug.Log("QA_AXE " + label + " " + e.name + " missing"); Fail(label + " " + e.name + " missing"); continue; }
                    world = bone.position;
                }
                Vector3 local = armsRoot.InverseTransformPoint(world);
                float diff = Vector3.Distance(local, e.position);
                Debug.Log("QA_AXE " + label + " " + e.name + " measured=" + local.ToString("F5") + " expected=" + e.position.ToString("F5") + " diff=" + diff.ToString("F4") + (diff <= Tolerance ? " ok" : " OUT"));
                if (diff > Tolerance) Fail(label + " " + e.name + " off by " + (diff * 100f).ToString("F2") + " cm");
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        void Capture(string name)
        {
            if (Application.isBatchMode) return; // no rendering in batchmode
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            QaPng.Write(Path.Combine(dir, name + ".png"), tex);
            Destroy(tex);
            saved++;
            Debug.Log("QA_AXE saved " + name + ".png " + Screen.width + "x" + Screen.height);
        }

        void Finish(string earlyFailure)
        {
            if (earlyFailure != null) Fail(earlyFailure);
            Debug.Log("QA_AXE pngs=" + saved);
            Debug.Log(failures.Count == 0 ? "QA_AXE_RESULT PASS" : "QA_AXE_RESULT FAIL " + string.Join("; ", failures.ToArray()));
            Debug.Log("QA_AXE_DONE");
            GameQuit.Request();
        }
    }
}
