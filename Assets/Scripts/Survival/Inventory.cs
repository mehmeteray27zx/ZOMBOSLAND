using System;
using System.Collections.Generic;

namespace Zombosland
{
    public struct ItemStack
    {
        public ItemDef item;
        public int count;
        public bool IsEmpty { get { return item == null || count <= 0; } }
    }

    // Pure slot storage with crafting reservations. No Unity scene needed (ItemDef assets can be created at runtime).
    public sealed class Inventory
    {
        ItemStack[] slots;
        readonly Dictionary<ItemDef, int> reserved = new Dictionary<ItemDef, int>();

        public event Action Changed;

        public Inventory(int capacity) { slots = new ItemStack[Math.Max(0, capacity)]; }

        public int Capacity { get { return slots.Length; } }

        public int UsedSlots
        {
            get
            {
                int n = 0;
                for (int i = 0; i < slots.Length; i++) if (!slots[i].IsEmpty) n++;
                return n;
            }
        }

        public ItemStack this[int slot]
        {
            get { return slot >= 0 && slot < slots.Length ? slots[slot] : default(ItemStack); }
        }

        void Fire() { if (Changed != null) Changed(); }

        static int MaxStack(ItemDef d) { return Math.Max(1, d.maxStack); }

        // ---------- queries ----------

        public int Count(ItemDef d) { return CountIn(slots, d); }

        public int Reserved(ItemDef d)
        {
            int n;
            return d != null && reserved.TryGetValue(d, out n) ? n : 0;
        }

        public int Available(ItemDef d) { return Math.Max(0, Count(d) - Reserved(d)); }

        public int FreeSpaceFor(ItemDef d)
        {
            if (d == null) return 0;
            int max = MaxStack(d), space = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty) space += max;
                else if (slots[i].item == d) space += Math.Max(0, max - slots[i].count);
            }
            return space;
        }

        // ---------- changes ----------

        public int Add(ItemDef d, int n)
        {
            int added = SimAdd(slots, d, n);
            if (added > 0) Fire();
            return added;
        }

        // Only items that are not reserved; all or nothing.
        public bool TryRemove(ItemDef d, int n)
        {
            if (d == null || n <= 0 || Available(d) < n) return false;
            SimRemove(slots, d, n);
            Fire();
            return true;
        }

        public bool Reserve(ItemDef d, int n)
        {
            if (d == null || n <= 0 || Available(d) < n) return false;
            reserved[d] = Reserved(d) + n;
            Fire();
            return true;
        }

        public void Release(ItemDef d, int n)
        {
            if (d == null || n <= 0) return;
            int cur = Reserved(d);
            if (cur <= 0) return;
            int left = cur - n;
            if (left > 0) reserved[d] = left; else reserved.Remove(d);
            Fire();
        }

        public void ReleaseAll()
        {
            if (reserved.Count == 0) return;
            reserved.Clear();
            Fire();
        }

        // Atomic: works on a copy and swaps it in only when every input is covered by a reservation and the output fits.
        public bool CommitReserved(ItemAmount[] inputs, ItemDef output, int outputCount)
        {
            ItemStack[] copy = (ItemStack[])slots.Clone();
            var res = new Dictionary<ItemDef, int>(reserved);
            if (inputs != null)
            {
                for (int i = 0; i < inputs.Length; i++)
                {
                    ItemDef d = inputs[i].item;
                    int n = inputs[i].count;
                    if (d == null || n <= 0) continue;
                    int have;
                    if (!res.TryGetValue(d, out have) || have < n) return false;
                    if (!SimRemove(copy, d, n)) return false;
                    if (have - n > 0) res[d] = have - n; else res.Remove(d);
                }
            }
            if (output != null && outputCount > 0 && SimAdd(copy, output, outputCount) < outputCount) return false;
            slots = copy;
            reserved.Clear();
            foreach (KeyValuePair<ItemDef, int> kv in res) reserved[kv.Key] = kv.Value;
            Fire();
            return true;
        }

        // Moves as many items as the other inventory accepts, but never reserved ones. Returns the moved count.
        public int MoveSlotTo(int slot, Inventory other)
        {
            if (other == null || other == this || slot < 0 || slot >= slots.Length || slots[slot].IsEmpty) return 0;
            ItemDef d = slots[slot].item;
            int movable = Math.Min(slots[slot].count, Available(d));
            if (movable <= 0) return 0;
            int moved = other.Add(d, movable);
            if (moved <= 0) return 0;
            slots[slot].count -= moved;
            if (slots[slot].count <= 0) slots[slot] = default(ItemStack);
            Fire();
            return moved;
        }

        public void SwapSlots(int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= slots.Length || b >= slots.Length) return;
            ItemStack t = slots[a]; slots[a] = slots[b]; slots[b] = t;
            Fire();
        }

        // Never shrinks.
        public void SetCapacity(int c)
        {
            if (c <= slots.Length) return;
            Array.Resize(ref slots, c);
            Fire();
        }

        public void Load(ItemStack[] source)
        {
            for (int i = 0; i < slots.Length; i++) slots[i] = default(ItemStack);
            reserved.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Length && i < slots.Length; i++)
                    if (!source[i].IsEmpty) slots[i] = source[i];
            }
            Fire();
        }

        public ItemStack[] Snapshot() { return (ItemStack[])slots.Clone(); }

        public void Clear()
        {
            for (int i = 0; i < slots.Length; i++) slots[i] = default(ItemStack);
            reserved.Clear();
            Fire();
        }

        // ---------- shared simulation helpers (also used by Crafter.Check on a copy) ----------

        internal static int CountIn(ItemStack[] s, ItemDef d)
        {
            if (d == null) return 0;
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (!s[i].IsEmpty && s[i].item == d) n += s[i].count;
            return n;
        }

        // Existing stacks first, then empty slots. Returns the added count (may be partial).
        internal static int SimAdd(ItemStack[] s, ItemDef d, int n)
        {
            if (d == null || n <= 0) return 0;
            int max = MaxStack(d), left = n;
            for (int i = 0; i < s.Length && left > 0; i++)
            {
                if (s[i].IsEmpty || s[i].item != d || s[i].count >= max) continue;
                int t = Math.Min(left, max - s[i].count);
                s[i].count += t;
                left -= t;
            }
            for (int i = 0; i < s.Length && left > 0; i++)
            {
                if (!s[i].IsEmpty) continue;
                int t = Math.Min(left, max);
                s[i] = new ItemStack { item = d, count = t };
                left -= t;
            }
            return n - left;
        }

        // All or nothing; takes from the last stacks first.
        internal static bool SimRemove(ItemStack[] s, ItemDef d, int n)
        {
            if (d == null || n <= 0 || CountIn(s, d) < n) return false;
            int left = n;
            for (int i = s.Length - 1; i >= 0 && left > 0; i--)
            {
                if (s[i].IsEmpty || s[i].item != d) continue;
                int t = Math.Min(left, s[i].count);
                s[i].count -= t;
                left -= t;
                if (s[i].count <= 0) s[i] = default(ItemStack);
            }
            return true;
        }
    }
}
