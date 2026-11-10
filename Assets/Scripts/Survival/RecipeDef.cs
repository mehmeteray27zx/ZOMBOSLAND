using UnityEngine;

namespace Zombosland
{
    [CreateAssetMenu(menuName = "Zombosland/Recipe")]
    public class RecipeDef : ScriptableObject
    {
        public string id, displayName;
        [TextArea] public string description;
        public ItemDef output;
        public int outputCount = 1;
        public ItemAmount[] inputs;
        public float seconds;
        public bool needsBench;
        public bool available = true;
    }
}
