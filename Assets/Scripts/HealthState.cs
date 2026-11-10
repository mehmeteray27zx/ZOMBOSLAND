namespace Zombosland
{
    public sealed class HealthState
    {
        public float Current { get; private set; }
        public float Max { get; private set; }
        public bool IsDead => Current <= 0;
        public HealthState(float initial)
        {
            Current = System.Math.Max(0, initial);
            Max = Current;
        }
        // Returns true exactly once, on the transition to death.
        public bool Damage(float amount)
        {
            if (IsDead || amount <= 0) return false;
            Current = System.Math.Max(0, Current - amount);
            return IsDead;
        }
        // Healing never revives and never goes above the starting maximum.
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0) return;
            Current = System.Math.Min(Max, Current + amount);
        }
        // Sets the value directly (loading a save), clamped to 0..Max.
        public void Restore(float value)
        {
            Current = System.Math.Max(0, System.Math.Min(Max, value));
        }
    }
}
