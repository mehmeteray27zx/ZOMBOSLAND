using UnityEngine;

namespace Zombosland
{
    // Written by IlkIkmalBuilder: where a critical gap is and how wide it must be. IlkIkmalValidation re-measures it with physics.
    public class ClearanceMarker : MonoBehaviour
    {
        public string label;
        public bool acrossX;       // true: the gap is measured along world X, false: along world Z
        public float minimum;      // metres
        public float builderValue; // metres, measured by the builder from its own geometry
    }
}
