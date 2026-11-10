using UnityEngine;

namespace Zombosland
{
    // Distraction stone: makes noise on the first impact (or after 6 s in the air), then vanishes.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class ThrownStone : MonoBehaviour
    {
        public float noiseRadius = 16f;

        const float MaxFlightSeconds = 6f, LingerSeconds = 4f;

        float spawnedAt;
        bool emitted;

        public bool Emitted { get { return emitted; } }

        void Awake() { spawnedAt = Time.time; }

        // Creates a stone (from the prefab, or a small sphere when there is no prefab) and throws it.
        public static ThrownStone Spawn(ThrownStone prefab, Vector3 position, Vector3 velocity, float radius, Collider[] ignore)
        {
            ThrownStone stone;
            if (prefab != null) stone = Instantiate(prefab, position, Quaternion.identity);
            else
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "ThrownStone";
                go.transform.position = position;
                go.transform.localScale = Vector3.one * 0.14f;
                Collider primitive = go.GetComponent<Collider>();
                if (primitive != null && !(primitive is SphereCollider)) { Destroy(primitive); }
                var rbNew = go.GetComponent<Rigidbody>();
                if (rbNew == null) go.AddComponent<Rigidbody>();
                stone = go.AddComponent<ThrownStone>();
            }
            stone.Launch(velocity, radius, ignore);
            return stone;
        }

        public void Launch(Vector3 velocity, float radius, Collider[] ignore)
        {
            if (radius > 0f) noiseRadius = radius;
            spawnedAt = Time.time;
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = 0.3f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.linearVelocity = velocity;
            }
            Collider own = GetComponent<Collider>();
            if (own != null && ignore != null)
                for (int i = 0; i < ignore.Length; i++)
                    if (ignore[i] != null) Physics.IgnoreCollision(own, ignore[i], true);
        }

        void OnCollisionEnter(Collision collision)
        {
            Vector3 at = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Emit(at);
        }

        void Update()
        {
            if (!emitted && Time.time - spawnedAt >= MaxFlightSeconds) Emit(transform.position);
        }

        void Emit(Vector3 point)
        {
            if (emitted) return;
            emitted = true;
            NoiseEvents.EmitDistraction(point, noiseRadius);
            Destroy(gameObject, LingerSeconds);
        }
    }
}
