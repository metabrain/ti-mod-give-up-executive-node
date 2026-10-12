using System;
using System.Text;

namespace GiveUpNation
{
    internal sealed class HateHistorySaveData
    {
        internal readonly string FactionId;
        internal readonly HateHistory.Entry[] Entries; // Oldest first.
        internal HateHistorySaveData(string factionId, HateHistory.Entry[] entries)
        {
            FactionId = factionId;
            Entries = (HateHistory.Entry[])entries.Clone();
        }
    }

    internal sealed class HateHistory
    {
        internal struct Entry
        {
            internal string Date, Cause;
            internal float Requested, Before, After;
        }
        private readonly Entry[] entries = new Entry[100];
        private int next;
        internal int Count { get; private set; }
        internal void Clear() { Array.Clear(entries, 0, entries.Length); next = Count = 0; }
        internal void Add(string date, string cause, float requested, float before, float after)
        {
            if (before == after) return;
            entries[next] = new Entry { Date = date, Cause = cause, Requested = requested, Before = before, After = after };
            next = (next + 1) % entries.Length;
            Count = Math.Min(Count + 1, entries.Length);
        }
        internal Entry Recent(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return entries[(next - 1 - index + entries.Length) % entries.Length];
        }
        internal HateHistorySaveData Snapshot(string factionId)
        {
            var copy = new Entry[Count];
            for (int i = 0; i < Count; i++) copy[i] = Recent(Count - 1 - i);
            return new HateHistorySaveData(factionId, copy);
        }
        internal void Restore(HateHistorySaveData snapshot)
        {
            Clear();
            foreach (var entry in snapshot.Entries)
                Add(entry.Date, entry.Cause, entry.Requested, entry.Before, entry.After);
        }
        internal string Tooltip()
        {
            var text = new StringBuilder("Alien hate changes\n");
            if (Count == 0) return text.Append("No changes recorded yet.").ToString();
            int shown = Math.Min(Count, 12);
            for (int i = 0; i < shown; i++)
            {
                var e = Recent(i);
                text.Append('\n').Append(e.Date).Append(" — ")
                    .Append(string.IsNullOrWhiteSpace(e.Cause) ? "Unspecified cause" : e.Cause.Replace('<', '[').Replace('>', ']'))
                    .Append('\n').Append((e.After - e.Before).ToString("+0.##;-0.##;0"))
                    .Append("   ").Append(e.Before.ToString("0.##")).Append(" -> ").Append(e.After.ToString("0.##"))
                    .Append(" (requested ").Append(e.Requested.ToString("+0.##;-0.##;0")).Append(")\n");
            }
            if (Count > shown) text.Append("\nShowing latest ").Append(shown).Append(" of ").Append(Count).Append(" recorded changes.");
            return text.ToString();
        }
    }
}
