using UnityEngine;
using UnityEngine.AI;

namespace Zombosland
{
    // Hinged door: opens once by 90 degrees and stays open. While closed a carving NavMeshObstacle cuts the NavMesh.
    public class DoorInteractable : MonoBehaviour, IHandInteractable, IFocusTitle, IBlockedLabel
    {
        public Transform hinge;          // rotates around Y
        public Collider leafCollider;    // blocks the player while closed
        public NavMeshObstacle obstacle; // carves the gap while closed
        public Transform hintAnchor;
        public bool insideOnly;          // side door: only usable from the depot side (x < insideMaxX)
        public float insideMaxX = 14f;
        public float openAngle = 90f;
        public bool locked;              // locked until the quest unlocks it (power)
        public string lockedLabel = "Elektrik gerekli";

        const float OpenSeconds = 0.6f;

        Quaternion closedRotation = Quaternion.identity;
        bool opened;
        float progress;

        public bool IsOpen { get { return opened; } }
        public float OpenFraction { get { return progress; } }
        public bool cabinet;             // a cabinet: opened by the hands like a container
        public string Verb { get { return cabinet ? "Dolabı aç" : "Kapıyı aç"; } }
        public HandActionKind HandKind { get { return cabinet ? HandActionKind.OpenContainer : HandActionKind.None; } }
        public bool RequiresLineOfSight { get { return false; } }
        public string Title { get { return cabinet ? "Dolap" : null; } }
        public Transform HintAnchor { get { return hintAnchor != null ? hintAnchor : transform; } }
        public bool BlockedBriefly { get { return true; } }

        void Awake()
        {
            if (BootRouter.IsAbandoned(gameObject)) return;
            if (hinge != null) closedRotation = hinge.localRotation;
        }

        bool PlayerOnUsableSide(Sandbox p) { return !insideOnly || p.transform.position.x < insideMaxX; }

        public bool CanInteract(Sandbox p) { return !opened && !locked && PlayerOnUsableSide(p); }

        public string BlockedLabel(Sandbox p)
        {
            if (opened) return null;
            if (locked) return lockedLabel;
            return !PlayerOnUsableSide(p) ? "Bu taraftan açılmıyor" : null;
        }

        public void Interact(Sandbox p)
        {
            if (opened || locked) return; // at most once
            Open(false);
        }

        public void Unlock() { locked = false; }

        // Opens without a player press (quest event or loading a save). instant = no animation. Also unlocks.
        public void ForceOpen(bool instant)
        {
            locked = false;
            if (opened)
            {
                if (instant) SnapOpen();
                return;
            }
            Open(instant);
        }

        void Open(bool instant)
        {
            opened = true;
            if (leafCollider != null) leafCollider.enabled = false;
            if (obstacle != null) obstacle.enabled = false;
            if (instant) SnapOpen();
            else if (SfxBank.Instance != null) SfxBank.Instance.Play(cabinet ? Sfx.LidOpen : Sfx.Slide, transform.position + Vector3.up, 0.7f, cabinet ? 1f : 0.8f, true);
        }

        void SnapOpen()
        {
            progress = 1f;
            if (hinge != null) hinge.localRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        }

        void Update()
        {
            if (!opened || progress >= 1f || hinge == null) return;
            progress = Mathf.Min(1f, progress + Time.unscaledDeltaTime / OpenSeconds);
            hinge.localRotation = closedRotation * Quaternion.Euler(0f, openAngle * Mathf.SmoothStep(0f, 1f, progress), 0f);
        }
    }
}
