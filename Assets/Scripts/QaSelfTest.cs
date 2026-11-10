using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace Zombosland
{
    // --qa-selftest: scripted run of the real shot pipeline, kill/ragdoll, and zombie attack. Logs QA_SELFTEST lines, then quits.
    public class QaSelfTest : MonoBehaviour
    {
        IEnumerator Start()
        {
            Sandbox.InstantHands = true; // hand actions apply at once in scripted runs
            Sandbox player = null;
            for (int i = 0; i < 300 && (player == null || player.Motor == null || ZombieDirector.Instance == null); i++)
            {
                yield return null;
                player = FindAnyObjectByType<Sandbox>();
            }
            yield return new WaitForSeconds(0.5f);
            ZombieBrain[] brains = FindObjectsByType<ZombieBrain>();
            if (player == null || brains.Length < 2) { Debug.LogError("QA_SELFTEST FAIL scene objects missing"); GameQuit.Request(); yield break; }
            ViewmodelPresenter vmPresenter = player.GetComponentInChildren<ViewmodelPresenter>();
            Debug.Log("QA_SELFTEST arms=" + (vmPresenter != null ? vmPresenter.ArmsSet : "none") + " armsAnimatorActive=" + (vmPresenter != null && vmPresenter.armsAnimator != null && vmPresenter.armsAnimator.isActiveAndEnabled));
            MethodInfo fire = typeof(Sandbox).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance);

            // 1. Real shot pipeline at a head.
            ZombieBrain target = brains[0];
            Place(target, player.transform.position + player.transform.forward * 6f, -player.transform.forward);
            yield return new WaitForFixedUpdate();
            yield return null;
            float before = target.Health;
            int ammoBefore = player.Weapon.Ammo;
            bool fired = player.Weapon.TryFire(Time.time);
            if (fired) fire.Invoke(player, null);
            Debug.Log("QA_SELFTEST shot1 fired=" + fired + " ammo " + ammoBefore + "->" + player.Weapon.Ammo + " zombieHealth " + before + "->" + target.Health + " state=" + target.State);

            // 2. Keep shooting the head until the zombie dies (a single head hit already kills a 100 HP zombie: 34 x 3 = 102).
            // No fixed shot count is assumed. The animated zombie reacts (head moves away), so re-aim before each follow-up shot.
            int shots = fired ? 1 : 0;
            for (int i = 0; i < 6 && !target.IsDead; i++)
            {
                yield return new WaitForSeconds(0.3f);
                if (target.IsDead) break;
                AimAtHead(player, target);
                yield return null;
                if (player.Weapon.TryFire(Time.time)) { fire.Invoke(player, null); shots++; }
            }
            Debug.Log("QA_SELFTEST kill shots=" + shots);
            yield return new WaitForSeconds(0.3f);
            var ragdoll = target.GetComponent<ZombieRagdoll>();
            Debug.Log("QA_SELFTEST kill dead=" + target.IsDead + " health=" + target.Health + " ragdollKinematic=" + ragdoll.AllKinematic + " ammo=" + player.Weapon.Ammo);
            yield return new WaitForSeconds(2f);
            Rigidbody hips = ragdoll.Bodies[0];
            Debug.Log("QA_SELFTEST ragdoll after 2s hipsY=" + hips.position.y.ToString("F2") + " speed=" + hips.linearVelocity.magnitude.ToString("F2") + " layer=" + LayerMask.LayerToName(hips.gameObject.layer));

            // 3. Zombie attack damages the player.
            ZombieBrain attacker = brains[1];
            float hp = player.Health;
            Place(attacker, player.transform.position + player.transform.forward * 1.3f, -player.transform.forward);
            attacker.Alert();
            float waited = 0f;
            while (player.Health >= hp && waited < 4f) { waited += Time.deltaTime; yield return null; }
            Debug.Log("QA_SELFTEST attack playerHealth " + hp + "->" + player.Health + " after " + waited.ToString("F2") + "s state=" + attacker.State);

            // 4. Reload cycle.
            bool began = player.Weapon.BeginReload(Time.time);
            float duration = player.Weapon.ReloadDuration;
            bool fromEmpty = player.Weapon.ReloadFromEmpty;
            yield return new WaitForSeconds(duration * 0.5f);
            float mid = player.Weapon.ReloadProgress(Time.time);
            yield return new WaitForSeconds(duration * 0.5f + 0.2f);
            Debug.Log("QA_SELFTEST reload began=" + began + " duration=" + duration.ToString("F2") + " fromEmpty=" + fromEmpty + " midProgress=" + mid.ToString("F2") + " ammo=" + player.Weapon.Ammo + " reloading=" + player.Weapon.Reloading);
            Debug.Log("QA_SELFTEST_DONE");
            GameQuit.Request();
        }

        static void AimAtHead(Sandbox player, ZombieBrain z)
        {
            ZombieAnimator za = z.GetComponent<ZombieAnimator>();
            if (za == null || za.head == null) return;
            Vector3 dir = za.head.position + Vector3.up * 0.1f - player.AimPivot.position;
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
            player.Motor.QaSetPitch(-Mathf.Asin(dir.y / dir.magnitude) * Mathf.Rad2Deg);
        }

        static void Place(ZombieBrain z, Vector3 position, Vector3 facing)
        {
            facing.y = 0;
            z.transform.rotation = Quaternion.LookRotation(facing);
            var agent = z.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(position); else z.transform.position = position;
        }
    }
}
