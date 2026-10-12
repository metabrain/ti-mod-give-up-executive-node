using System;
using System.Collections.Generic;
using FullSerializer;

namespace GiveUpNation
{
    // Plain JSON only: never embed mod CLR types or game-state references.
    internal static class HateHistorySaveCodec
    {
        internal const string Key = "Local.GiveUpNation";
        internal const int Version = 1;
        private const int MaxCauseLength = 512, MaxDateLength = 128, MaxIdLength = 128;

        internal static fsData Encode(HateHistorySaveData snapshot)
        {
            if (snapshot.Entries.Length > 100) throw new FormatException("Too many hate-history entries.");
            var rows = new List<fsData>();
            foreach (var entry in snapshot.Entries)
            {
                ValidateNumber(entry.Requested); ValidateNumber(entry.Before); ValidateNumber(entry.After);
                rows.Add(new fsData(new Dictionary<string, fsData>
                {
                    ["date"] = new fsData(Bounded(entry.Date, MaxDateLength)),
                    ["cause"] = new fsData(Bounded(entry.Cause, MaxCauseLength)),
                    ["requested"] = new fsData((double)entry.Requested),
                    ["before"] = new fsData((double)entry.Before),
                    ["after"] = new fsData((double)entry.After)
                }));
            }
            if (string.IsNullOrEmpty(snapshot.FactionId) || snapshot.FactionId.Length > MaxIdLength)
                throw new FormatException("Invalid hate-history faction ID.");
            return new fsData(new Dictionary<string, fsData>
            {
                ["version"] = new fsData((long)Version),
                ["factionId"] = new fsData(snapshot.FactionId),
                ["hateHistory"] = new fsData(rows)
            });
        }

        internal static HateHistorySaveData Decode(fsData data)
        {
            if (data == null || !data.IsDictionary) throw new FormatException("Invalid hate-history section.");
            var fields = data.AsDictionary;
            if (!fields.TryGetValue("version", out var version) || !version.IsInt64 || version.AsInt64 != Version)
                throw new FormatException("Unsupported hate-history schema version.");
            string factionId = ReadString(fields, "factionId", MaxIdLength);
            if (factionId.Length == 0) throw new FormatException("Missing hate-history faction ID.");
            if (!fields.TryGetValue("hateHistory", out var rows) || !rows.IsList || rows.AsList.Count > 100)
                throw new FormatException("Invalid hate-history entry list.");
            var entries = new HateHistory.Entry[rows.AsList.Count];
            for (int i = 0; i < entries.Length; i++)
            {
                if (!rows.AsList[i].IsDictionary) throw new FormatException("Invalid hate-history entry.");
                var row = rows.AsList[i].AsDictionary;
                entries[i] = new HateHistory.Entry
                {
                    Date = ReadString(row, "date", MaxDateLength), Cause = ReadString(row, "cause", MaxCauseLength),
                    Requested = ReadNumber(row, "requested"), Before = ReadNumber(row, "before"), After = ReadNumber(row, "after")
                };
            }
            return new HateHistorySaveData(factionId, entries);
        }

        private static string Bounded(string value, int length) => value == null ? string.Empty
            : value.Length <= length ? value : value.Substring(0, length);
        private static string ReadString(Dictionary<string, fsData> fields, string key, int max)
        {
            if (!fields.TryGetValue(key, out var value) || !value.IsString || value.AsString.Length > max)
                throw new FormatException("Invalid hate-history text field: " + key);
            return value.AsString;
        }
        private static float ReadNumber(Dictionary<string, fsData> fields, string key)
        {
            if (!fields.TryGetValue(key, out var value) || (!value.IsDouble && !value.IsInt64))
                throw new FormatException("Invalid hate-history numeric field: " + key);
            double number = value.IsDouble ? value.AsDouble : value.AsInt64;
            float result = (float)number;
            ValidateNumber(result);
            return result;
        }
        private static void ValidateNumber(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new FormatException("Non-finite hate-history value.");
        }
    }
}
