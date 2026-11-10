using System;
using UnityEngine;

namespace Zombosland
{
    // Gameplay noise (gunshots). Zombies subscribe and decide whether they hear it.
    public static class NoiseEvents
    {
        public static event Action<Vector3, float> Heard;
        // Fired by ZombieBrain.OnNoise when a gunshot actually switched a zombie to Chase (HintDirector, playtest log).
        public static event Action<ZombieBrain> ZombieHeard;

        public static void Emit(Vector3 position, float radius = 30f)
        {
            if (Heard != null) Heard(position, radius);
        }

        // A thrown distraction (stone): zombies walk to the point and look around, they do not start a chase.
        public static event Action<Vector3, float> Distraction;

        public static void EmitDistraction(Vector3 position, float radius)
        {
            if (Distraction != null) Distraction(position, radius);
        }

        public static void RaiseZombieHeard(ZombieBrain zombie)
        {
            if (ZombieHeard != null) ZombieHeard(zombie);
        }

        public static void ClearSubscribers() { Heard = null; ZombieHeard = null; Distraction = null; }
    }
}
