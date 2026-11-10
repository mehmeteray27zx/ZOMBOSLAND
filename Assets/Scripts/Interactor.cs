using System;
using UnityEngine;

namespace Zombosland
{
    // Player component. Every frame it casts a thin sphere from the aim pivot and exposes what could be used.
    // The E key and the QA entry point share Press(); it never touches firing.
    public class Interactor : MonoBehaviour
    {
        public const float Range = 2.2f, Radius = 0.12f, Reach = 2.5f, CommitReach = 3.4f;
        public const int Mask = (1 << Layers.Environment) | (1 << Layers.Interactable);
        const float BlockedSeconds = 2.5f;

        Sandbox player;
        IInteractable prevFocus;
        float focusSince;

        public IInteractable Focus { get; private set; }
        public bool CanUse { get; private set; }
        public string Title { get; private set; }   // name line above the label, null = verb line only
        public string Label { get; private set; }   // text for the world label, null = nothing
        public bool LabelDim { get; private set; }  // true for "cannot use" texts
        public Transform Anchor { get; private set; }
        public event Action<IInteractable> Used;

        void Awake()
        {
            if (BootRouter.IsAbandoned(gameObject)) return;
            player = GetComponent<Sandbox>();
        }

        void Update()
        {
            if (player == null || player.PerfMode) return;
            Refresh();
            if (!player.UiOpen && Input.GetKeyDown(KeyCode.E)) Press();
        }

        internal void Refresh()
        {
            Focus = null; CanUse = false; Label = null; Title = null; LabelDim = false; Anchor = null;
            if (player != null && player.UiOpen) { prevFocus = null; return; } // inventory / crafting open: nothing is selectable
            Transform pivot = player != null ? player.AimPivot : null;
            if (pivot == null) { prevFocus = null; return; }
            RaycastHit hit;
            IInteractable target = null;
            if (Physics.SphereCast(pivot.position, Radius, pivot.forward, out hit, Range, Mask, QueryTriggerInteraction.Collide))
            {
                // A wall (Environment) hit first means nothing behind it can be selected.
                // The visible body of an interactable also sits on Environment and belongs to its root.
                target = hit.collider.GetComponentInParent<IInteractable>();
            }
            var handTarget = target as IHandInteractable;
            if (handTarget != null && !Reachable(pivot, handTarget)) target = null;
            float now = Time.unscaledTime;
            if (target != prevFocus) focusSince = now;
            prevFocus = target;
            if (target == null) return;

            Focus = target;
            Anchor = target.HintAnchor;
            var titled = target as IFocusTitle;
            Title = titled != null ? titled.Title : null;
            if (target.CanInteract(player))
            {
                CanUse = true;
                Label = InputLabels.Prompt(InputLabels.Interact, target.Verb);
                return;
            }
            var blocked = target as IBlockedLabel;
            if (blocked == null) return;
            string text = blocked.BlockedLabel(player);
            if (text == null) return;
            if (blocked.BlockedBriefly && now - focusSince > BlockedSeconds) return;
            Label = text;
            LabelDim = true;
        }

        // Hand targets: within reach and (when required) not behind a wall.
        static bool Reachable(Transform pivot, IHandInteractable t)
        {
            Transform anchor = t.HintAnchor;
            if (anchor == null) return false;
            Vector3 eye = pivot.position, to = anchor.position;
            if (Vector3.Distance(eye, to) > Reach) return false;
            return !t.RequiresLineOfSight || LineClear(eye, to, t);
        }

        static bool LineClear(Vector3 eye, Vector3 to, IInteractable t)
        {
            RaycastHit hit;
            if (!Physics.Linecast(eye, to, out hit, 1 << Layers.Environment, QueryTriggerInteraction.Ignore)) return true;
            var c = t as Component;
            if (c == null) return true;
            // The first obstacle must belong to the target itself.
            return hit.collider.transform.IsChildOf(c.transform) || hit.collider.GetComponentInParent<IInteractable>() == t;
        }

        // Called at the commit moment of a hand action: the world change happens here, once.
        internal bool Commit(IHandInteractable t)
        {
            if (t == null || player == null || !t.CanInteract(player)) return false;
            Transform anchor = t.HintAnchor;
            Transform pivot = player.AimPivot != null ? player.AimPivot : player.transform;
            if (anchor == null || Vector3.Distance(pivot.position, anchor.position) >= CommitReach) return false;
            if (t.RequiresLineOfSight && !LineClear(pivot.position, anchor.position, t)) return false;
            t.Interact(player);
            if (Used != null) Used(t);
            Refresh();
            return true;
        }

        // Uses the focused object. False when nothing usable is focused.
        internal bool Press()
        {
            if (Focus == null || player == null) return false;
            if (!Focus.CanInteract(player))
            {
                focusSince = Time.unscaledTime; // show a brief "cannot" text again
                var blockedPress = Focus as IBlockedPress;
                if (blockedPress != null) blockedPress.OnBlockedPress(player);
                return false;
            }
            IInteractable used = Focus;
            var hand = used as IHandInteractable;
            if (hand != null && hand.HandKind != HandActionKind.None && !Sandbox.InstantHands)
            {
                if (player.TryBeginItemAction(hand, this)) return true;
                string why = player.HandActionBlockedReason();
                if (why != null) Notifier.Show(why, 1.2f);
                return false;
            }
            used.Interact(player);
            if (Used != null) Used(used);
            Refresh();
            return true;
        }

        // QA entry point: same code path as the key.
        internal bool QaPress()
        {
            Refresh();
            return Press();
        }
    }
}
