using System;
using FullSerializer;

namespace GiveUpNation
{
    internal sealed class HateHistorySaveProcessor : fsObjectProcessor
    {
        private readonly Type saveType;
        private readonly Func<HateHistorySaveData> capture;
        private readonly Func<bool> loading;
        private readonly Action<HateHistorySaveData> stage;
        private readonly Action<Exception> report;

        internal HateHistorySaveProcessor(Type saveType, Func<HateHistorySaveData> capture,
            Func<bool> loading, Action<HateHistorySaveData> stage, Action<Exception> report)
        {
            this.saveType = saveType; this.capture = capture; this.loading = loading;
            this.stage = stage; this.report = report;
        }
        public override bool CanProcess(Type type) => type == saveType;

        public override void OnAfterSerialize(Type storageType, object instance, ref fsData data)
        {
            try
            {
                var snapshot = capture();
                if (snapshot == null) return;
                if (!data.IsDictionary || data.AsDictionary.ContainsKey(HateHistorySaveCodec.Key))
                    throw new FormatException("Unexpected save root or existing mod history section.");
                // Build completely before touching the game-owned JSON tree.
                var section = HateHistorySaveCodec.Encode(snapshot);
                data.AsDictionary.Add(HateHistorySaveCodec.Key, section);
            }
            catch (Exception e) { report(e); }
        }

        public override void OnBeforeDeserialize(Type storageType, ref fsData data)
        {
            // Previews/metadata reads do not import history. Missing data means an
            // empty history, not history from the previously running campaign.
            try
            {
                if (!loading()) return;
                stage(null);
                if (data.IsDictionary && data.AsDictionary.TryGetValue(HateHistorySaveCodec.Key, out var section))
                    stage(HateHistorySaveCodec.Decode(section));
            }
            catch (Exception e) { report(e); }
        }
    }
}
