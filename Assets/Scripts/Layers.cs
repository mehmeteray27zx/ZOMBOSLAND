using UnityEngine;

namespace Zombosland
{
    // Layer indices; names are registered by SandboxBuilder in TagManager.
    public static class Layers
    {
        public const int ZombieMover = 8;
        public const int Player = 9;
        public const int Corpse = 10;
        public const int ZombieHitbox = 11;
        public const int Environment = 12;
        public const int Interactable = 13; // trigger colliders of usable objects; shots and sight ignore them
        // Shots ignore the movement capsules of zombies and the player, and interaction triggers.
        public const int ShotMask = Physics.DefaultRaycastLayers & ~((1 << ZombieMover) | (1 << Player) | (1 << Interactable));
        // Line of sight ignores zombies (movement, hitboxes, corpses) but sees the player.
        public const int SightMask = Physics.DefaultRaycastLayers & ~((1 << ZombieMover) | (1 << Corpse) | (1 << ZombieHitbox) | (1 << Interactable));
    }
}
