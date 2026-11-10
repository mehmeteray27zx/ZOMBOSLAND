using UnityEngine;

namespace Zombosland
{
    public enum ItemKind { Resource, Consumable, Throwable, Melee, Kit }

    // Item definition asset (edited in the Unity editor).
    [CreateAssetMenu(menuName = "Zombosland/Item")]
    public class ItemDef : ScriptableObject
    {
        public string id, displayName;
        [TextArea] public string description;
        public ItemKind kind;
        public int maxStack = 10;
        public Texture2D icon;
        public float healAmount, meleeDamage, meleeRange, meleeSeconds, noiseRadius;
    }

    [System.Serializable]
    public struct ItemAmount
    {
        public ItemDef item;
        public int count;
    }
}
