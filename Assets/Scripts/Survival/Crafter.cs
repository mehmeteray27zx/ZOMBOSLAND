using System;
using System.Collections.Generic;

namespace Zombosland
{
    public enum CraftBlock { None, MissingInput, NeedsBench, BagFull, Busy, Unavailable }

    public struct CraftCheck
    {
        public CraftBlock block;
        public string message;
        public bool Ok { get { return block == CraftBlock.None; } }
    }

    // Timed crafting on one bag. Uses only the `now` it is given (never reads Time), so tests can drive it with a fake clock.
    public sealed class Crafter
    {
        public const int MaxQuantity = 99;

        readonly Inventory bag;
        float unitStart;
        bool committing;
        string pendingCancel;

        public RecipeDef Active;
        public bool Busy;
        public int Remaining, Total;
        public string LastMessage;

        public event Action<RecipeDef> UnitCompleted;
        public event Action<string> Cancelled;

        public Crafter(Inventory bag) { this.bag = bag; }

        public float UnitProgress(float now)
        {
            if (!Busy || Active == null) return 0f;
            if (Active.seconds <= 0f) return 1f;
            float p = (now - unitStart) / Active.seconds;
            return p < 0f ? 0f : (p > 1f ? 1f : p);
        }

        static CraftCheck Block(CraftBlock b, string message) { return new CraftCheck { block = b, message = message }; }

        // Sum of the inputs per distinct item, in recipe order.
        static List<ItemAmount> Totals(RecipeDef r, int qty)
        {
            var list = new List<ItemAmount>();
            if (r.inputs == null) return list;
            for (int i = 0; i < r.inputs.Length; i++)
            {
                ItemDef d = r.inputs[i].item;
                int n = r.inputs[i].count;
                if (d == null || n <= 0) continue;
                int at = -1;
                for (int k = 0; k < list.Count; k++) if (list[k].item == d) { at = k; break; }
                if (at < 0) list.Add(new ItemAmount { item = d, count = n * qty });
                else list[at] = new ItemAmount { item = d, count = list[at].count + n * qty };
            }
            return list;
        }

        public CraftCheck Check(RecipeDef r, int qty, bool benchInRange)
        {
            if (r == null || !r.available || r.output == null || r.outputCount <= 0 || qty <= 0 || qty > MaxQuantity)
                return Block(CraftBlock.Unavailable, "Tarif kullanılamıyor");
            if (Busy) return Block(CraftBlock.Busy, "Üretim sürüyor");
            if (r.needsBench && !benchInRange) return Block(CraftBlock.NeedsBench, "Tezgâh gerekli");

            List<ItemAmount> need = Totals(r, qty);
            for (int i = 0; i < need.Count; i++)
            {
                int have = bag.Available(need[i].item);
                if (have < need[i].count)
                {
                    string name = string.IsNullOrEmpty(need[i].item.displayName) ? need[i].item.name : need[i].item.displayName;
                    return Block(CraftBlock.MissingInput, name + ": " + have + " / " + need[i].count + " — " + (need[i].count - have) + " eksik");
                }
            }

            // Units are simulated one after another on a copy, so a slot freed by an input can take the output.
            ItemStack[] sim = bag.Snapshot();
            for (int u = 0; u < qty; u++)
            {
                if (r.inputs != null)
                    for (int i = 0; i < r.inputs.Length; i++)
                        if (r.inputs[i].item != null && r.inputs[i].count > 0) Inventory.SimRemove(sim, r.inputs[i].item, r.inputs[i].count);
                if (Inventory.SimAdd(sim, r.output, r.outputCount) < r.outputCount) return Block(CraftBlock.BagFull, "Çantada yer yok");
            }
            return new CraftCheck { block = CraftBlock.None, message = null };
        }

        public bool Start(RecipeDef r, int qty, float now, bool benchInRange)
        {
            CraftCheck c = Check(r, qty, benchInRange);
            if (!c.Ok) { LastMessage = c.message; return false; }
            List<ItemAmount> need = Totals(r, qty);
            for (int i = 0; i < need.Count; i++)
            {
                if (!bag.Reserve(need[i].item, need[i].count))
                {
                    for (int k = 0; k < i; k++) bag.Release(need[k].item, need[k].count);
                    LastMessage = "Çantada yer yok";
                    return false;
                }
            }
            Active = r;
            Busy = true;
            Total = Remaining = qty;
            unitStart = now;
            LastMessage = null;
            return true;
        }

        public void Tick(float now, bool benchInRange)
        {
            if (!Busy || Active == null || committing) return;
            if (Active.needsBench && !benchInRange) { Cancel("Tezgâhtan uzaklaştın"); return; }
            if (now - unitStart < Active.seconds) return;
            CompleteUnit(now);
        }

        void CompleteUnit(float now)
        {
            RecipeDef r = Active;
            committing = true;
            bool ok = bag.CommitReserved(r.inputs, r.output, r.outputCount);
            committing = false;
            string pending = pendingCancel;
            pendingCancel = null;
            if (!ok)
            {
                CancelNow(pending ?? "Çantada yer yok");
                return;
            }
            Remaining--;
            if (Remaining <= 0)
            {
                Busy = false;
                Active = null;
                Remaining = 0;
            }
            else unitStart = now;
            if (UnitCompleted != null) UnitCompleted(r);
            if (pending != null && Busy) CancelNow(pending);
        }

        // Releases what is still reserved, gives no output. Safe to call twice and from event handlers.
        public void Cancel(string reason)
        {
            if (!Busy) return;
            if (committing) { pendingCancel = reason; return; }
            CancelNow(reason);
        }

        void CancelNow(string reason)
        {
            if (!Busy) return;
            RecipeDef r = Active;
            int remaining = Remaining;
            // State first, so a Changed/Cancelled handler that calls Cancel again sees an idle crafter.
            Busy = false;
            Active = null;
            Remaining = 0;
            LastMessage = reason;
            if (r != null && remaining > 0)
            {
                List<ItemAmount> left = Totals(r, remaining);
                for (int i = 0; i < left.Count; i++) bag.Release(left[i].item, left[i].count);
            }
            if (Cancelled != null) Cancelled(reason);
        }
    }
}
