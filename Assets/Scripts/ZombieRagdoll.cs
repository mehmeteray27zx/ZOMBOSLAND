using UnityEngine;

namespace Zombosland
{
    // Bone rigidbodies start kinematic (hitboxes follow the animated pose). On death they become dynamic.
    public class ZombieRagdoll : MonoBehaviour
    {
        const float FirstCheckSeconds = 1f;
        const float CheckInterval = 0.5f;
        const float MaxSimSeconds = 5f;

        Rigidbody[] bodies;
        float settleDeadline;

        public Rigidbody[] Bodies { get { Collect(); return bodies; } }

        void Collect()
        {
            if (bodies != null) return;
            var all = GetComponentsInChildren<Rigidbody>(true);
            var list = new System.Collections.Generic.List<Rigidbody>();
            foreach (Rigidbody rb in all) if (rb.transform != transform) list.Add(rb);
            bodies = list.ToArray();
        }

        public bool AllKinematic
        {
            get
            {
                foreach (Rigidbody rb in Bodies) if (!rb.isKinematic) return false;
                return true;
            }
        }

        public void Activate(in DamageInfo info)
        {
            Collect();
            Rigidbody nearest = null;
            float best = float.MaxValue;
            foreach (Rigidbody rb in bodies)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.gameObject.layer = Layers.Corpse;
                float d = (rb.worldCenterOfMass - info.Point).sqrMagnitude;
                if (d < best) { best = d; nearest = rb; }
            }
            if (nearest == null) return;
            Vector3 dir = info.Direction.sqrMagnitude > 0.0001f ? info.Direction.normalized : transform.forward;
            float impulse = info.Zone == HitZone.Head ? 9f : 5f;
            nearest.AddForceAtPosition(dir * impulse, info.Point, ForceMode.Impulse);
            // Hit impulse is applied above; once settled, bodies go kinematic (colliders stay on for hits).
            settleDeadline = Time.time + MaxSimSeconds;
            CancelInvoke(nameof(CheckSettled));
            InvokeRepeating(nameof(CheckSettled), FirstCheckSeconds, CheckInterval);
        }

        void CheckSettled()
        {
            bool timedOut = Time.time >= settleDeadline;
            if (!timedOut)
            {
                foreach (Rigidbody rb in bodies) if (rb != null && !rb.isKinematic && !rb.IsSleeping()) return;
            }
            foreach (Rigidbody rb in bodies)
            {
                if (rb == null || rb.isKinematic) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            CancelInvoke(nameof(CheckSettled));
        }
    }
}
