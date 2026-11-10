using UnityEngine;

namespace Zombosland
{
    // LPSP clip events land here so Unity finds a receiver; game state does not depend on any of them.
    public class ViewmodelAnimationEvents : MonoBehaviour
    {
        public void OnAmmunitionFill() { }
        public void OnAnimationEndedHolster() { }
        public void OnAnimationEndedInspect() { }
        public void OnAnimationEndedReload() { }
        public void OnEjectCasing() { }
        public void OnSlideBack() { }
    }
}
