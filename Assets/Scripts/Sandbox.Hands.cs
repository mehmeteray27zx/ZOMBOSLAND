using UnityEngine;

namespace Zombosland
{
    // Hand actions: one at a time, never over a reload, a swing or a menu. Timing lives here; the clips only show it.
    // The world changes exactly once, at the commit moment, through the same method as an instant use. Before that moment
    // cancelling leaves the world and the bag untouched; after it the action only finishes its animation.
    public partial class Sandbox
    {
        public static bool InstantHands; // QA: hand actions apply at once, without clips

        HandActionKind actionKind = HandActionKind.None;
        float actionStart, actionEnd, actionCommitAt;
        const float LidContactSeconds = 8f / 30f; // the hand closes on the lid on frame 9 of the container clip
        bool actionCommitted, swapPending, lidStarted;
        IHandInteractable actionTarget;
        Interactor actionSource;
        ItemDef actionItem;
        HandCarrier swapCarrier;
        ItemDef swapItem;
        bool craftingShown;

        public HandCarrier Carrier { get; private set; } = HandCarrier.Pistol;
        public bool HandsBusy { get { return actionKind != HandActionKind.None; } }
        public HandActionKind CurrentHandAction { get { return actionKind; } }
        public bool WeaponReady { get { return Carrier == HandCarrier.Pistol && !HandsBusy; } }
        internal bool HandActionCommitted { get { return actionCommitted; } }

        // null when a hand action may start now; otherwise a short reason for the player.
        public string HandActionBlockedReason()
        {
            if (dead || paused) return "Şu an olmaz";
            if (HandsBusy) return "Ellerin dolu";
            if (UiOpen) return "Önce menüyü kapat";
            if (Weapon.Reloading || (presenter != null && presenter.ReloadAnimating)) return "Doldurma bitmeden olmaz";
            if (MeleeSwinging) return "Vuruş bitmeden olmaz";
            return null;
        }

        void BeginAction(HandActionKind kind, float seconds, float commitAt)
        {
            actionKind = kind;
            actionStart = Time.time;
            actionEnd = actionStart + seconds;
            actionCommitAt = actionStart + commitAt;
            actionCommitted = false;
            lidStarted = false;
            adsHeld = false;
        }

        internal bool TryBeginItemAction(IHandInteractable target, Interactor source)
        {
            if (target == null || HandActionBlockedReason() != null) return false;
            float seconds, commit;
            if (target.HandKind == HandActionKind.OpenContainer) { seconds = ViewmodelPresenter.ContainerSeconds; commit = ViewmodelPresenter.ContainerCommit; }
            else { seconds = ViewmodelPresenter.PickupSeconds; commit = ViewmodelPresenter.PickupCommit; }
            BeginAction(target.HandKind, seconds, commit);
            actionTarget = target;
            actionSource = source;
            Transform anchor = target.HintAnchor;
            bool reach = target.HandKind == HandActionKind.Pickup && anchor != null;
            if (presenter != null) presenter.PlayHandAction(target.HandKind, reach, reach ? anchor.position : Vector3.zero);
            return true;
        }

        internal bool TryBeginBandage(ItemDef d)
        {
            if (d == null || survival == null || HandActionBlockedReason() != null) return false;
            if (Health >= MaxHealth - 0.001f) { Notifier.Show("Can dolu", 1.5f); return false; }
            if (survival.Bag.Available(d) <= 0) return false;
            BeginAction(HandActionKind.Bandage, ViewmodelPresenter.BandageSeconds, ViewmodelPresenter.BandageCommit);
            actionItem = d;
            if (presenter != null) presenter.PlayHandAction(HandActionKind.Bandage, false, Vector3.zero);
            return true;
        }

        // Pistol / melee / empty hands. The state changes at the commit moment (the bottom of the holster motion).
        void BeginCarrierSwap(HandCarrier carrier, ItemDef item)
        {
            bool instant = InstantHands || presenter == null || !presenter.animated || HandActionBlockedReason() != null;
            if (instant)
            {
                if (HandsBusy) CancelHandAction("Değişim");
                ApplyCarrier(carrier, item, true);
                return;
            }
            float holster = Carrier == HandCarrier.Pistol ? ViewmodelPresenter.PistolHolsterSeconds : ViewmodelPresenter.DipSeconds;
            float draw = carrier == HandCarrier.Pistol ? ViewmodelPresenter.PistolDrawSeconds : carrier == HandCarrier.Hands ? ViewmodelPresenter.HandsDrawSeconds : ViewmodelPresenter.HeldDrawSeconds;
            BeginAction(HandActionKind.Draw, holster + draw, holster);
            swapPending = true;
            swapCarrier = carrier;
            swapItem = item;
            presenter.PlayHolster(holster);
            if (SfxBank.Instance != null) SfxBank.Instance.Play2D(Sfx.Holster, 0.45f);
        }

        // H key: pistol away / back. A melee weapon goes to empty hands too.
        internal void ToggleHolster()
        {
            if (dead || paused || UiOpen || HandsBusy) return;
            if (Carrier == HandCarrier.Hands)
            {
                if (survival != null) survival.ActiveQuick = 0;
                BeginCarrierSwap(HandCarrier.Pistol, null);
                return;
            }
            if (survival != null) survival.ActiveQuick = -1; // no quick slot is highlighted
            BeginCarrierSwap(HandCarrier.Hands, null);
        }

        void UpdateHands()
        {
            if (!HandsBusy) return;
            float now = Time.time;
            if (actionKind == HandActionKind.OpenContainer && !lidStarted && now - actionStart >= LidContactSeconds)
            {
                lidStarted = true;
                var box = actionTarget as StorageBox;
                if (box != null) box.BeginLidOpen();
            }
            if (!actionCommitted && now >= actionCommitAt)
            {
                actionCommitted = true;
                Commit();
                if (actionKind == HandActionKind.None) return; // the commit ended or cancelled the action
            }
            if (now >= actionEnd) FinishAction();
        }

        void Commit()
        {
            switch (actionKind)
            {
                case HandActionKind.Draw:
                    if (swapPending) { swapPending = false; ApplyCarrier(swapCarrier, swapItem, false); }
                    if (presenter != null) presenter.PlayDraw();
                    if (SfxBank.Instance != null) SfxBank.Instance.Play2D(Sfx.Equip, 0.5f);
                    break;
                case HandActionKind.Bandage:
                    if (survival != null && survival.TryUseBandage(actionItem) && SfxBank.Instance != null) SfxBank.Instance.Play2D(Sfx.BandageTie, 0.8f);
                    break;
                default:
                    // The same world change as an instant use; a target that became invalid leaves everything as it was.
                    if (actionSource == null || !actionSource.Commit(actionTarget)) CancelHandAction("Geçersiz");
                    break;
            }
        }

        void FinishAction()
        {
            bool stayLowered = UiOpen;
            ClearAction();
            if (presenter != null) presenter.EndHandAction(stayLowered);
            if (craftingShown && presenter != null) presenter.SetCrafting(true);
        }

        void ClearAction()
        {
            actionKind = HandActionKind.None;
            actionTarget = null;
            actionSource = null;
            actionItem = null;
        }

        // Safe at any time. Before the commit nothing has changed. A pending swap is applied so the player never ends up with the wrong item state.
        internal void CancelHandAction(string reason)
        {
            if (!HandsBusy) return;
            bool wasSwap = actionKind == HandActionKind.Draw && swapPending;
            swapPending = false;
            ClearAction();
            if (presenter != null) presenter.EndHandAction(UiOpen);
            if (wasSwap) ApplyCarrier(swapCarrier, swapItem, true);
        }

        void OnUiStateForHands(bool open)
        {
            if (open && HandsBusy && !actionCommitted) CancelHandAction("Menü");
            if (!HandsBusy && presenter != null) presenter.SetLowered(open);
            if (!open) NotifyCrafting(false);
        }

        // Craft loop clip while Crafter.Busy (the screen is open then).
        public void NotifyCrafting(bool active)
        {
            if (craftingShown == active) return;
            craftingShown = active;
            if (presenter != null && !HandsBusy) presenter.SetCrafting(active);
        }

        // QA: state of the running action.
        internal float QaActionElapsed { get { return HandsBusy ? Time.time - actionStart : -1f; } }
    }
}
