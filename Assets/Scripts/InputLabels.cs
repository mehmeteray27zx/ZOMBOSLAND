namespace Zombosland
{
    // Single place for key names shown in the HUD. Texts are built from these; keys are never typed into strings.
    public static class InputLabels
    {
        public const string Move = "WASD";
        public const string MoveKeys = "W A S D"; // spaced form used on the hint card
        public const string Sprint = "Shift";
        public const string Crouch = "Ctrl";
        public const string Jump = "Space";
        public const string Fire = "Sol fare";
        public const string Aim = "Sağ fare";
        public const string Reload = "R";
        public const string Interact = "E";
        public const string Menu = "Esc";
        public const string Help = "F1";
        public const string Confirm = "Enter";

        public const string Holster = "H";
        public const UnityEngine.KeyCode HolsterKey = UnityEngine.KeyCode.H;

        public const string Inventory = "Tab";
        public const UnityEngine.KeyCode InventoryKey = UnityEngine.KeyCode.Tab;

        // Quick slots 0..3 are keys 1..4 (slot 0 is the pistol).
        public static UnityEngine.KeyCode QuickKey(int i) { return UnityEngine.KeyCode.Alpha1 + Clamp(i); }
        public static string Quick(int i) { return (Clamp(i) + 1).ToString(); }
        static int Clamp(int i) { return i < 0 ? 0 : (i > 3 ? 3 : i); }

        public static string Key(string key) { return "[" + key + "]"; }
        public static string Prompt(string key, string text) { return "[" + key + "] " + text; }
    }
}
