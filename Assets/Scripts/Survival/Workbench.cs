using System.Collections.Generic;
using UnityEngine;

namespace Zombosland
{
    // Crafting bench. Standing within BenchRange metres enables the bench-only recipes.
    public class Workbench : MonoBehaviour, IInteractable
    {
        public const float BenchRange = 3.5f;
        public Transform hintAnchor;

        static readonly List<Workbench> all = new List<Workbench>();
        public static List<Workbench> All { get { return all; } }

        public string Verb { get { return "Tezgâhı kullan"; } }
        public Transform HintAnchor { get { return hintAnchor != null ? hintAnchor : transform; } }

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() { all.Remove(this); }

        public bool CanInteract(Sandbox p)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            return ps != null && !ps.UiOpen;
        }

        public void Interact(Sandbox p)
        {
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (ps != null) ps.OpenUi(UiTab.Crafting);
        }

        // True when any bench is within range of the point.
        public static bool AnyInRange(Vector3 point)
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (all[i] == null) { all.RemoveAt(i); continue; }
                if ((all[i].transform.position - point).sqrMagnitude <= BenchRange * BenchRange) return true;
            }
            return false;
        }
    }
}
