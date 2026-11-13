using UnityEngine;

namespace Zombosland
{
    // Zombie tuning values as data. The field defaults are the standard zombie: a freshly created asset
    // (or no asset at all) uses them. Standard zombie = L4D2-style runner: walks while idle, sprints on sight.
    [CreateAssetMenu(menuName = "Zombosland/Zombie")]
    public class ZombieDef : ScriptableObject
    {
        public const float DefaultAttackWindupSeconds = 0.75f;

        [Header("Movement")]
        public float walkSpeed = 1.1f;
        public float investigateSpeedFactor = 1.5f;
        // Player walks 4, sprints 6 m/s: a chasing zombie catches a walking player, sprinting escapes.
        // Above ~4.8 m/s the run clip cannot keep up (ZombieAnimator.AnimRate caps at 1.6x) and feet start to slide.
        public float chaseSpeedMin = 4.5f;   // was 1.6
        public float chaseSpeedMax = 5.0f;   // was 2.2
        // NavMeshAgent turning and speed-up. The old 8 / 240 were tuned for a 2 m/s walker; a runner with those values orbits its target.
        public float acceleration = 16f;
        public float turnSpeed = 420f;       // deg/s

        [Header("Health")]
        public float maxHealth = 55f;        // was 100. Pistol: 2 body shots (34 each) or 1 head shot

        [Header("Attack")]
        public float attackDamage = 10f;
        public float attackRange = 1.6f;
        public float attackCooldown = 1.2f;
        public float attackWindupSeconds = DefaultAttackWindupSeconds;

        [Header("Perception")]
        public float sightRange = 18f;
        public float sightHalfAngle = 55f;
        public float closeSense = 2f;

        static ZombieDef fallback;

        // Shared in-memory defaults used when a zombie has no def assigned (never saved to disk).
        public static ZombieDef Fallback
        {
            get
            {
                if (fallback == null)
                {
                    fallback = CreateInstance<ZombieDef>();
                    fallback.hideFlags = HideFlags.HideAndDontSave;
                }
                return fallback;
            }
        }
    }
}
