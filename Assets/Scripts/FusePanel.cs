using UnityEngine;

namespace Zombosland
{
    // Yellow service-yard panel. Accepts the fuse once; without a fuse it shows a dim "fuse needed" note.
    public class FusePanel : MonoBehaviour, IInteractable, IBlockedLabel
    {
        public GameObject fuseInSlot; // shown after installing
        public Transform hintAnchor;

        bool installed;

        public bool Installed { get { return installed; } }
        public string Verb { get { return "Sigortayı tak"; } }
        public Transform HintAnchor { get { return hintAnchor != null ? hintAnchor : transform; } }
        public bool BlockedBriefly { get { return false; } }

        public bool CanInteract(Sandbox p)
        {
            QuestFuse q = QuestFuse.Instance;
            return !installed && q != null && q.State == QuestState.CarryingFuse;
        }

        public string BlockedLabel(Sandbox p)
        {
            QuestFuse q = QuestFuse.Instance;
            return !installed && q != null && q.State == QuestState.SearchFuse ? "Sigorta gerekli" : null;
        }

        public void Interact(Sandbox p)
        {
            if (installed) return;
            QuestFuse q = QuestFuse.Instance;
            if (q == null) return;
            // Marked before the quest runs, so the autosave inside InstallFuse already sees the panel as installed.
            ForceInstalled();
            if (!q.InstallFuse())
            {
                installed = false;
                if (fuseInSlot != null) fuseInSlot.SetActive(false);
            }
        }

        // Loading a save: the fuse is already in the panel (no quest event).
        public void ForceInstalled()
        {
            installed = true;
            if (fuseInSlot != null) fuseInSlot.SetActive(true);
        }
    }
}
