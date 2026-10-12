using System;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;

namespace GiveUpNation
{
    internal static class AlienHateHistory
    {
        private static readonly HateHistory history = new HateHistory();
        private static Harmony harmony;
        private static TIFactionState player;
        private static object control;
        private static int generation;
        private static bool reported;
        private static readonly object gate = new object();
        private static HateHistorySaveData pendingRestore;
        private static bool loadInProgress;

        internal struct Capture
        {
            internal bool Valid;
            internal int Generation;
            internal float Before, Requested;
            internal string Cause;
        }

        internal static void Reset()
        {
            harmony?.UnpatchAll(harmony.Id);
            harmony = null;
            ClearCampaign();
            reported = false;
        }
        private static void Clear()
        {
            history.Clear(); player = null; control = null; generation++;
        }
        internal static bool SyncContext()
        {
            lock (gate) return SyncContextLocked();
        }
        private static bool SyncContextLocked()
        {
            if (loadInProgress) return false;
            if (!Main.Enabled || !GameControl.loadcycle100 || GameControl.control == null
                || GameControl.control.activePlayer == null)
            {
                if (player != null || control != null) Clear();
                return false;
            }
            if (!ReferenceEquals(control, GameControl.control) || !ReferenceEquals(player, GameControl.control.activePlayer))
            {
                Clear(); control = GameControl.control; player = GameControl.control.activePlayer;
            }
            if (pendingRestore != null)
            {
                if (pendingRestore.FactionId == player.ID.ToString()) history.Restore(pendingRestore);
                pendingRestore = null;
            }
            return true;
        }
        internal static HateHistorySaveData ExportForSave()
        {
            lock (gate) return SyncContextLocked() ? history.Snapshot(player.ID.ToString()) : null;
        }
        internal static void BeginLoad()
        {
            lock (gate) { Clear(); pendingRestore = null; loadInProgress = true; }
        }
        internal static void FinishLoad(HateHistorySaveData snapshot)
        {
            lock (gate) { pendingRestore = snapshot; loadInProgress = false; }
        }
        internal static void ClearCampaign()
        {
            lock (gate) { Clear(); pendingRestore = null; loadInProgress = false; }
        }
        internal static void Install()
        {
            if (harmony != null || !SyncContext()) return;
            var patcher = new Harmony("Local.GiveUpNation.HateHistory");
            try
            {
                patcher.Patch(AccessTools.Method(typeof(TIFactionState), "GainFactionHate",
                    new[] { typeof(TIFactionState), typeof(float), typeof(bool), typeof(string), typeof(bool) }),
                    prefix: new HarmonyMethod(typeof(AlienHateHistory), nameof(Before)),
                    postfix: new HarmonyMethod(typeof(AlienHateHistory), nameof(After)));
                harmony = patcher;
            }
            catch { patcher.UnpatchAll(patcher.Id); throw; }
        }
        private static void Before(TIFactionState __instance, TIFactionState enemyCouncil, float value, string cause, out Capture __state)
        {
            __state = default;
            try
            {
                lock (gate)
                {
                    if (!SyncContextLocked() || !ReferenceEquals(enemyCouncil, player)
                        || !ReferenceEquals(__instance, GameStateManager.AlienFaction())) return;
                    __state = new Capture { Valid = true, Generation = generation,
                        Before = __instance.GetFactionHate(enemyCouncil), Requested = value, Cause = cause };
                }
            }
            catch (Exception e) { Report(e); }
        }
        private static void After(TIFactionState __instance, TIFactionState enemyCouncil, Capture __state)
        {
            try
            {
                lock (gate)
                {
                    if (!__state.Valid || !SyncContextLocked() || __state.Generation != generation) return;
                    float after = __instance.GetFactionHate(enemyCouncil);
                    if (after == __state.Before) return;
                    history.Add(TITimeState.Now().ToCustomDateString(), __state.Cause,
                        __state.Requested, __state.Before, after);
                }
            }
            catch (Exception e) { Report(e); }
        }
        internal static string Tooltip()
        {
            try { lock (gate) return SyncContextLocked() ? history.Tooltip() : string.Empty; }
            catch (Exception e) { Report(e); return string.Empty; }
        }
        private static void Report(Exception e)
        {
            if (reported) return;
            reported = true;
            Main.Log(e);
        }
    }
}
