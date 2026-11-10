using UnityEngine;

namespace Zombosland
{
    // Yard storage chest: opens the inventory with the storage side. Contents live in PlayerSurvival.Storage.
    // An optional lid hinge swings open while this box is the open storage.
    public class StorageBox : MonoBehaviour, IHandInteractable, IFocusTitle
    {
        public Transform hintAnchor;
        public Transform lid;              // hinge transform (rotates around local Z); null = no animation
        public float lidOpenAngle = 70f;

        const float OpenSeconds = 0.25f, CloseSeconds = 0.3f;

        Quaternion closedRotation = Quaternion.identity;
        bool lidWanted;
        float lidProgress, holdOpenUntil;

        public string Verb { get { return "Depoyu aç"; } }
        public Transform HintAnchor { get { return hintAnchor != null ? hintAnchor : transform; } }
        public HandActionKind HandKind { get { return HandActionKind.OpenContainer; } }
        public bool RequiresLineOfSight { get { return false; } }
        public string Title { get { return "Depo"; } }

        void Awake()
        {
            if (BootRouter.IsAbandoned(gameObject)) return;
            if (lid != null) closedRotation = lid.localRotation;
        }

        public bool CanInteract(Sandbox p)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            return ps != null && !ps.UiOpen;
        }

        public void Interact(Sandbox p)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (ps == null) return;
            ps.OpenUi(UiTab.Inventory, this);
            if (ps.OpenStorage != this || lidWanted) return;
            lidWanted = true;
            if (SfxBank.Instance != null) SfxBank.Instance.Play(Sfx.LidOpen, transform.position, 0.8f, 1f, true);
        }

        // The hand reached the lid: it starts lifting before the screen opens. If the action is cancelled it simply closes again.
        public void BeginLidOpen()
        {
            holdOpenUntil = Time.time + 0.4f;
            if (lidWanted) return;
            lidWanted = true;
            if (SfxBank.Instance != null) SfxBank.Instance.Play(Sfx.LidOpen, transform.position, 0.8f, 1f, true);
        }

        void Update()
        {
            if (lid == null) return;
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (lidWanted && Time.time >= holdOpenUntil && (ps == null || ps.OpenStorage != this))
            {
                lidWanted = false;
                if (lidProgress > 0f && SfxBank.Instance != null) SfxBank.Instance.Play(Sfx.LidClose, transform.position, 0.8f, 1f, true);
            }
            float target = lidWanted ? 1f : 0f;
            if (Mathf.Approximately(lidProgress, target)) return;
            float step = Time.unscaledDeltaTime / (lidWanted ? OpenSeconds : CloseSeconds);
            lidProgress = Mathf.MoveTowards(lidProgress, target, step);
            lid.localRotation = closedRotation * Quaternion.Euler(0f, 0f, lidOpenAngle * Mathf.SmoothStep(0f, 1f, lidProgress));
        }
    }
}
