using UnityEngine;

namespace Zombosland
{
    // Melee weapon in hand (wooden bat). Equipped through PlayerSurvival; LMB swings.
    public partial class Sandbox
    {
        const float MeleeHitAt = 0.35f, MeleeSphere = 0.18f, MeleeNoiseRadius = 5f;
        const float DefaultMeleeDamage = 40f, DefaultMeleeRange = 1.9f, DefaultMeleeSeconds = 0.7f;

        ItemDef meleeItem;
        MeleeViewmodel meleeAnim;
        float meleeStart = -100f, meleeLength = DefaultMeleeSeconds;
        bool meleeSwinging, meleeHitDone;
        bool axeInHand; // the axe viewmodel (Survivalist / LPSP hands) is active instead of the bat view

        public bool MeleeEquipped { get { return meleeItem != null; } }
        public ItemDef MeleeItem { get { return meleeItem; } }
        public bool MeleeSwinging { get { return meleeSwinging; } }
        public bool AxeInHand { get { return axeInHand; } }
        // Swings whose hit window found a living zombie (QA / playtest).
        public int MeleeLanded { get; private set; }
        public int MeleeSwings { get; private set; }
        // What the last hit window found, for QA logs.
        internal string LastMeleeResult { get; private set; } = "none";

        static HandCarrier CarrierFor(ItemDef item)
        {
            if (item == null) return HandCarrier.Pistol;
            return item.id == "axe" ? HandCarrier.Axe : HandCarrier.Bat;
        }

        // null (or a non-melee item) = pistol in hand. Runs the holster / draw swap unless hand animation is off.
        internal void SetMelee(ItemDef item)
        {
            ItemDef next = item != null && item.kind == ItemKind.Melee ? item : null;
            if (next == meleeItem && Carrier == CarrierFor(next)) return;
            BeginCarrierSwap(CarrierFor(next), next);
        }

        // The swap itself: state, viewmodel and the placeholder melee view all change here, once.
        void ApplyCarrier(HandCarrier carrier, ItemDef item, bool instant)
        {
            meleeItem = item;
            meleeSwinging = false;
            adsHeld = false;
            Carrier = carrier;
            axeInHand = carrier == HandCarrier.Axe && presenter != null && presenter.AxeAvailable;
            bool batInHands = carrier == HandCarrier.Bat && presenter != null && presenter.BatAvailable;
            if (presenter != null) presenter.SetCarrier(carrier, instant);
            GameObject view = survival != null ? survival.meleeView : null;
            if (view != null)
            {
                view.SetActive(meleeItem != null && !axeInHand && !batInHands);
                meleeAnim = view.GetComponent<MeleeViewmodel>();
            }
        }

        internal bool TryMeleeSwing()
        {
            if (meleeItem == null || meleeSwinging || dead || paused || UiOpen || HandsBusy) return false;
            if (Time.time < meleeStart + meleeLength) return false;
            meleeLength = meleeItem.meleeSeconds > 0f ? meleeItem.meleeSeconds : DefaultMeleeSeconds;
            meleeStart = Time.time;
            meleeSwinging = true;
            meleeHitDone = false;
            MeleeSwings++;
            if (axeInHand) presenter.PlayAxeSwing();
            else if (Carrier == HandCarrier.Bat && presenter != null && presenter.BatAvailable) presenter.PlayBatSwing(meleeLength);
            else if (meleeAnim != null) meleeAnim.Play(meleeLength);
            return true;
        }

        // QA hook: same path as the left mouse button.
        internal bool QaMeleeSwing() { return TryMeleeSwing(); }

        void UpdateMelee()
        {
            if (!meleeSwinging) return;
            float t = Time.time - meleeStart;
            if (!meleeHitDone && t >= meleeLength * MeleeHitAt)
            {
                meleeHitDone = true;
                if (meleeItem != null) MeleeHit();
            }
            if (t >= meleeLength) meleeSwinging = false;
        }

        // One sphere sweep from the camera at the hit moment. Out of range there is no damage.
        void MeleeHit()
        {
            Physics.SyncTransforms();
            Vector3 origin = AimPivot.position, forward = AimPivot.forward;
            float range = meleeItem.meleeRange > 0f ? meleeItem.meleeRange : DefaultMeleeRange;
            float damage = meleeItem.meleeDamage > 0f ? meleeItem.meleeDamage : DefaultMeleeDamage;
            NoiseEvents.Emit(origin, MeleeNoiseRadius);

            RaycastHit hit;
            if (!Physics.SphereCast(origin, MeleeSphere, forward, out hit, range, ShotMask, QueryTriggerInteraction.Ignore)) { LastMeleeResult = "nothing origin=" + origin + " forward=" + forward; return; }
            LastMeleeResult = hit.collider.name + " layer=" + hit.collider.gameObject.layer + " dist=" + hit.distance.ToString("F2") + " part=" + (hit.collider.GetComponent<ZombiePart>() != null);

            EffectPool fx = EffectPool.Instance;
            SfxBank sfx = SfxBank.Instance;
            ZombiePart part = hit.collider.GetComponent<ZombiePart>();
            if (part == null)
            {
                if (fx != null) fx.WorldImpact(hit.point, hit.normal);
                if (sfx != null) sfx.Play(Sfx.HitConcrete, hit.point, 0.7f, UnityEngine.Random.Range(0.7f, 0.85f), true);
                return;
            }
            if (fx != null) fx.BloodImpact(hit.point, hit.normal);
            if (sfx != null) sfx.Play(Sfx.HitFlesh, hit.point, 1f, UnityEngine.Random.Range(0.8f, 0.95f), true);
            if (part.owner == null || part.owner.IsDead) return;
            var info = new DamageInfo
            {
                Amount = damage,
                Point = hit.point,
                Direction = forward,
                Zone = part.zone,
                Source = this
            };
            PlaytestLog.RecordHit(part.zone);
            MeleeLanded++;
            bool killed = part.Hit(info);
            if (sfx != null) sfx.Play(Sfx.HitTick, Vector3.zero, killed ? 0.4f : 0.25f, killed ? 0.85f : 1f, false);
            HitMarkerTime = Time.time;
            if (HitConfirmed != null) HitConfirmed(part.zone, killed);
        }
    }
}
