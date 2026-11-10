using UnityEngine;

namespace Zombosland
{
    public enum HitZone { Body, Head, Limb }

    // Which limb a hitbox belongs to (None for torso and head). Used only for hit reactions.
    public enum HumanLimb { None, LeftArm, RightArm, LeftLeg, RightLeg }

    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point, Direction;
        public HitZone Zone;
        public Object Source;
    }

    public interface IDamageable
    {
        // Returns true when this hit killed the target (exactly once).
        bool ApplyDamage(in DamageInfo info);
    }

    public static class DamageRules
    {
        public const float BaseDamage = 34f;
        // Heavy stagger needs a head hit or this much damage inside the window, and a cooldown since the last one.
        public const float PoiseThreshold = 60f;
        public const float PoiseWindow = 1.2f;
        public const float HeavyStaggerCooldown = 1.5f;

        public static float Multiplier(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return 3f;
                case HitZone.Limb: return 0.7f;
                default: return 1f;
            }
        }

        public static float HeavyStaggerSeconds(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return 0.5f;
                case HitZone.Limb: return 0.35f;
                default: return 0.45f;
            }
        }
    }
}
