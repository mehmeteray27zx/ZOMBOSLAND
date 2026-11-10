using UnityEngine;

namespace Zombosland
{
    // The fuse lying on the depot bench. Taking it hides it and moves the quest to CarryingFuse.
    public class FuseItem : MonoBehaviour, IHandInteractable, IFocusTitle
    {
        public GameObject visual;
        public Collider trigger;
        public Transform hintAnchor;

        bool taken;

        public bool Taken { get { return taken; } }
        public string Verb { get { return "Sigortayı al"; } }
        public HandActionKind HandKind { get { return HandActionKind.Pickup; } }
        public bool RequiresLineOfSight { get { return true; } }
        public string Title { get { return "Sigorta"; } }
        public Transform HintAnchor { get { return hintAnchor != null ? hintAnchor : transform; } }

        public bool CanInteract(Sandbox p)
        {
            QuestFuse q = QuestFuse.Instance;
            return !taken && q != null && q.State == QuestState.SearchFuse;
        }

        public void Interact(Sandbox p)
        {
            if (taken) return;
            QuestFuse q = QuestFuse.Instance;
            if (q == null || !q.TakeFuse()) return;
            Hide();
            if (SfxBank.Instance != null) SfxBank.Instance.Play(Sfx.MagIn, transform.position, 0.8f, 1.2f, true);
        }

        // Loading a save: the fuse is already taken (no quest event, no sound).
        public void ForceTaken() { Hide(); }

        void Hide()
        {
            taken = true;
            if (visual != null) visual.SetActive(false);
            if (trigger != null) trigger.enabled = false;
        }
    }
}
