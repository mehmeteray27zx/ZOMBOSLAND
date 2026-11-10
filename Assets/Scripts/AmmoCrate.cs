using UnityEngine;

namespace Zombosland
{
    // Depot ammunition box: +12 reserve rounds, once.
    public class AmmoCrate : MonoBehaviour, IHandInteractable, IFocusTitle
    {
        public const int Rounds = 12;
        public GameObject roundsVisual; // hidden once emptied
        public Renderer body;
        public Material emptyMaterial;
        public Collider trigger;
        public Transform hintAnchor;

        bool used;

        public bool Used { get { return used; } }
        public string Verb { get { return "Mühimmatı al"; } }
        public HandActionKind HandKind { get { return HandActionKind.Pickup; } }
        public bool RequiresLineOfSight { get { return true; } }
        public string Title { get { return "Mühimmat kutusu ×" + Rounds; } }
        public Transform HintAnchor { get { return hintAnchor != null ? hintAnchor : transform; } }
        public bool CanInteract(Sandbox p) { return !used; }

        public void Interact(Sandbox p)
        {
            if (used) return;
            p.Weapon.AddReserve(Rounds);
            MarkUsed();
            if (SfxBank.Instance != null) SfxBank.Instance.Play(Sfx.MagIn, transform.position, 0.9f, 0.9f, true);
        }

        // Loading a save: the box is empty again, without ammunition or sound.
        public void ForceUsed() { MarkUsed(); }

        void MarkUsed()
        {
            used = true;
            if (roundsVisual != null) roundsVisual.SetActive(false);
            if (body != null && emptyMaterial != null) body.sharedMaterial = emptyMaterial;
            if (trigger != null) trigger.enabled = false;
        }
    }
}
