using UnityEngine;

namespace Zombosland
{
    // Visual-only camera offsets (headbob position, shake rotation, FOV). The aim ray never reads this transform.
    public class CameraEffects : MonoBehaviour
    {
        Sandbox player;
        PlayerMotor motor;
        Camera cam;
        float bobPhase, bobAmount, trauma;

        void Awake()
        {
            cam = GetComponent<Camera>();
            player = GetComponentInParent<Sandbox>();
            motor = GetComponentInParent<PlayerMotor>();
        }

        public void AddShake(float amount) { trauma = Mathf.Min(1f, trauma + amount); }

        void LateUpdate()
        {
            if (player == null || player.IsPaused) return;
            float dt = Time.deltaTime;
            float ads = player.AdsAmount;

            // Headbob: grounded and moving only.
            bool bobbing = motor.Grounded && motor.HorizontalSpeed > 0.5f;
            bobAmount = Mathf.MoveTowards(bobAmount, bobbing ? 1f : 0f, 6f * dt);
            float sprint = motor.Sprinting ? 1f : 0f;
            if (bobbing) bobPhase += dt * Mathf.PI * 2f * Mathf.Lerp(1.8f, 2.6f, sprint) * (motor.Crouching ? 0.7f : 1f);
            float amp = GameSettings.Headbob * bobAmount * Mathf.Lerp(1f, 0.2f, ads) * Mathf.Lerp(1f, 1.5f, sprint);
            Vector3 bob = new Vector3(Mathf.Sin(bobPhase) * 0.012f, Mathf.Sin(bobPhase * 2f) * 0.025f, 0) * amp;
            transform.localPosition = bob;

            // Shake: Perlin rotation noise scaled by trauma^2 and the setting (0 = off).
            trauma = Mathf.MoveTowards(trauma, 0f, 2.2f * dt);
            float s = trauma * trauma * GameSettings.CameraShake;
            float t = Time.time * 22f;
            Vector3 shake = new Vector3(Noise(t, 0), Noise(t, 11), Noise(t, 23) * 0.6f) * (2.2f * s);
            transform.localRotation = Quaternion.Euler(shake);

            float targetFov = Mathf.Lerp(GameSettings.Fov, GameSettings.Fov * 0.75f, ads);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-16f * dt));
        }

        static float Noise(float t, float seed) { return Mathf.PerlinNoise(t, seed) * 2f - 1f; }
    }
}
