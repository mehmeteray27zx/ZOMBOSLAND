using UnityEngine;

namespace Zombosland
{
    // Hitbox on a bone. Forwards damage to the owning zombie; the zone decides the multiplier.
    public class ZombiePart : MonoBehaviour
    {
        public ZombieBrain owner;
        public HitZone zone = HitZone.Body;
        public HumanLimb limb = HumanLimb.None;

        public bool Hit(in DamageInfo info)
        {
            DamageInfo copy = info;
            copy.Zone = zone;
            return owner != null && owner.ApplyDamage(copy, this);
        }
    }
}
