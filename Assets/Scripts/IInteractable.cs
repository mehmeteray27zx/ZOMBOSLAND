namespace Zombosland
{
    // Something the player can use with the interact key. The trigger collider lives on the Interactable layer.
    public interface IInteractable
    {
        string Verb { get; }
        bool CanInteract(Sandbox p);
        void Interact(Sandbox p);
        UnityEngine.Transform HintAnchor { get; }
    }

    public enum HandActionKind { None, Pickup, OpenContainer, Bandage, Draw }

    // What the first-person hands hold.
    public enum HandCarrier { Pistol, Hands, Bat, Axe }

    // A use that the hands perform: the world change happens once, at the commit moment of the clip.
    public interface IHandInteractable : IInteractable
    {
        HandActionKind HandKind { get; }
        bool RequiresLineOfSight { get; }
    }

    // Name line of the world label ("Kumaş ×2"). Null = only the verb line.
    public interface IFocusTitle { string Title { get; } }

    // Called when the interact key is pressed on something that cannot be used right now.
    public interface IBlockedPress { void OnBlockedPress(Sandbox p); }

    // Optional: text for a focused object that cannot be used right now (null = show nothing).
    public interface IBlockedLabel
    {
        string BlockedLabel(Sandbox p);
        bool BlockedBriefly { get; } // true: shown only for a short time after focus starts or after a failed press
    }
}
