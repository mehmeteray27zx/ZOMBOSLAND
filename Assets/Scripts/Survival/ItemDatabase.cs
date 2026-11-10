using UnityEngine;

namespace Zombosland
{
    [CreateAssetMenu(menuName = "Zombosland/Database")]
    public class ItemDatabase : ScriptableObject
    {
        public ItemDef[] items;
        public RecipeDef[] recipes;

        public ItemDef FindItem(string id)
        {
            if (items == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < items.Length; i++)
                if (items[i] != null && items[i].id == id) return items[i];
            return null;
        }

        public RecipeDef FindRecipe(string id)
        {
            if (recipes == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < recipes.Length; i++)
                if (recipes[i] != null && recipes[i].id == id) return recipes[i];
            return null;
        }
    }
}
