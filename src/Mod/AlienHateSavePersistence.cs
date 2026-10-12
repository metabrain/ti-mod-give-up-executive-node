using System;
using FullSerializer;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using System.Threading;

namespace GiveUpNation
{
    internal static class AlienHateSavePersistence
    {
        private static Harmony harmony;
        private static fsSerializer serializer;
        private static bool reported;
        private static int epoch;
        [ThreadStatic] private static SaveFrame saving;
        [ThreadStatic] private static LoadFrame loading;
        internal sealed class SaveFrame
        {
            internal SaveFrame Parent;
            internal HateHistorySaveData Snapshot;
        }
        internal sealed class LoadFrame
        {
            internal LoadFrame Parent;
            internal HateHistorySaveData Snapshot;
            internal int Epoch;
            internal bool Enabled;
        }

        // Installed at mod activation, before the first save is loaded. The
        // serializer initializer only constructs fsSerializer (verified in IL).
        internal static void Install()
        {
            if (harmony != null) return;
            reported = false;
            var patcher = new Harmony("Local.GiveUpNation.HateSave");
            try
            {
                serializer = (fsSerializer)AccessTools.Field(typeof(StringSerializationAPI), "_serializer").GetValue(null);
                serializer.AddProcessor(new HateHistorySaveProcessor(typeof(SaveStructure),
                    () => Main.Enabled ? saving?.Snapshot : null,
                    () => Main.Enabled && loading != null,
                    snapshot => { if (loading != null) loading.Snapshot = snapshot; }, Report));
                patcher.Patch(AccessTools.Method(typeof(GameStateManager), "SaveAllGameStates"),
                    prefix: new HarmonyMethod(typeof(AlienHateSavePersistence), nameof(BeforeSave)),
                    finalizer: new HarmonyMethod(typeof(AlienHateSavePersistence), nameof(FinishSave)));
                patcher.Patch(AccessTools.Method(typeof(GameStateManager), "LoadAllGameStates"),
                    prefix: new HarmonyMethod(typeof(AlienHateSavePersistence), nameof(BeforeLoad)),
                    finalizer: new HarmonyMethod(typeof(AlienHateSavePersistence), nameof(FinishLoad)));
                patcher.Patch(AccessTools.Method(typeof(GameStateManager), "ClearAllGameStates"),
                    prefix: new HarmonyMethod(typeof(AlienHateSavePersistence), nameof(ClearCampaign)));
                harmony = patcher;
            }
            catch (Exception e)
            {
                patcher.UnpatchAll(patcher.Id);
                serializer?.RemoveProcessor<HateHistorySaveProcessor>();
                serializer = null;
                Report(e);
            }
        }
        internal static void Reset()
        {
            Interlocked.Increment(ref epoch);
            harmony?.UnpatchAll(harmony.Id);
            harmony = null;
            serializer?.RemoveProcessor<HateHistorySaveProcessor>();
            serializer = null;
            saving = null; loading = null;
        }
        private static void BeforeSave(out SaveFrame __state)
        {
            __state = new SaveFrame { Parent = saving };
            saving = __state;
            try { if (Main.Enabled) __state.Snapshot = AlienHateHistory.ExportForSave(); }
            catch (Exception e) { Report(e); }
        }
        private static void FinishSave(SaveFrame __state) { saving = __state?.Parent; }
        private static void BeforeLoad(out LoadFrame __state)
        {
            __state = new LoadFrame { Parent = loading, Epoch = Interlocked.Increment(ref epoch), Enabled = Main.Enabled };
            loading = __state;
            try { if (Main.Enabled) AlienHateHistory.BeginLoad(); }
            catch (Exception e) { Report(e); }
        }
        private static void FinishLoad(bool __result, Exception __exception, LoadFrame __state)
        {
            try
            {
                if (Main.Enabled && __state != null && __state.Enabled && __state.Epoch == Volatile.Read(ref epoch))
                    AlienHateHistory.FinishLoad(__exception == null && __result ? __state.Snapshot : null);
            }
            catch (Exception e) { Report(e); }
            finally { loading = __state?.Parent; }
            // A void finalizer preserves the game's original result/exception.
        }
        private static void ClearCampaign()
        {
            Interlocked.Increment(ref epoch);
            try { AlienHateHistory.ClearCampaign(); }
            catch (Exception e) { Report(e); }
        }
        private static void Report(Exception e)
        {
            if (reported) return;
            reported = true;
            Main.Log(new InvalidOperationException("Alien hate history persistence: " + e.Message, e));
        }
    }
}
