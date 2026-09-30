using System;
using System.Collections.Generic;

namespace GiveUpNation
{
    // Two reusable buffers: evaluate each body once, then publish atomically.
    internal sealed class StatusSnapshot<T>
    {
        private Dictionary<T, ProspectingStatus> current = new Dictionary<T, ProspectingStatus>();
        private Dictionary<T, ProspectingStatus> pending = new Dictionary<T, ProspectingStatus>();
        private bool changed;

        internal void Clear() { current.Clear(); pending.Clear(); }
        internal void Begin() { pending.Clear(); changed = false; }
        internal void Add(T body, ProspectingStatus status)
        {
            pending[body] = status;
            if (!current.TryGetValue(body, out var old) || old != status) changed = true;
        }
        internal bool Complete()
        {
            changed |= current.Count != pending.Count;
            var old = current;
            current = pending;
            pending = old;
            pending.Clear();
            return changed;
        }
        internal ProspectingStatus Get(T body, Func<T, ProspectingStatus> evaluate)
        {
            if (!current.TryGetValue(body, out var status)) current[body] = status = evaluate(body);
            return status;
        }
    }
}
