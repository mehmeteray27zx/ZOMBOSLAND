using UnityEngine;

namespace Zombosland
{
    // First-person melee weapon pose. A swing is a code-driven curve on the local transform (no animator clip).
    // It is deliberately not scaled by GameSettings.WeaponSway: the swing is gameplay feedback, not sway.
    public class MeleeViewmodel : MonoBehaviour
    {
        public float defaultSeconds = 0.7f;

        Vector3 restPosition;
        Quaternion restRotation;
        float start = -100f, duration = 0.7f;
        bool captured;

        public bool Playing { get { return Time.time - start < duration; } }
        public float Progress { get { return Mathf.Clamp01((Time.time - start) / Mathf.Max(0.01f, duration)); } }

        void Capture()
        {
            if (captured) return;
            captured = true;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        void Awake() { Capture(); }

        void OnEnable()
        {
            Capture();
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
        }

        public void Play() { Play(defaultSeconds); }

        public void Play(float seconds)
        {
            Capture();
            duration = seconds > 0.05f ? seconds : defaultSeconds;
            start = Time.time;
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        void LateUpdate()
        {
            if (!captured) return;
            float t = (Time.time - start) / Mathf.Max(0.01f, duration);
            if (t < 0f || t >= 1f)
            {
                transform.localPosition = restPosition;
                transform.localRotation = restRotation;
                return;
            }
            // 0..0.3 wind up (back and up), 0.3..0.5 strike (fast forward and down), 0.5..1 recover.
            float windup, strike;
            if (t < 0.3f) { windup = Smooth(t / 0.3f); strike = 0f; }
            else if (t < 0.5f) { windup = 1f - Smooth((t - 0.3f) / 0.2f); strike = Smooth((t - 0.3f) / 0.2f); windup *= 1f - strike; }
            else { windup = 0f; strike = 1f - Smooth((t - 0.5f) / 0.5f); }
            Vector3 offset = new Vector3(0.04f, 0.10f, -0.08f) * windup + new Vector3(-0.08f, -0.14f, 0.20f) * strike;
            Vector3 euler = new Vector3(-40f, 10f, 12f) * windup + new Vector3(55f, -14f, -10f) * strike;
            transform.localPosition = restPosition + offset;
            transform.localRotation = restRotation * Quaternion.Euler(euler);
        }
    }
}
