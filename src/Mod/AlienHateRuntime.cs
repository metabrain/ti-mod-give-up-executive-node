using System;
using PavonisInteractive.TerraInvicta;

namespace GiveUpNation
{
    internal static class AlienHateRuntime
    {
        private static readonly ProbeScanScheduler scheduler = new ProbeScanScheduler();
        private static GeneralControlsController controller;
        private static AlienHateDebugReadout readout;

        internal static void Reset()
        {
            AlienHateHistory.Reset();
            if (readout != null) readout.Cleanup();
            readout = null;
            controller = null;
            scheduler.Reset();
        }

        internal static void Update(float elapsed)
        {
            scheduler.Tick(Main.Enabled, elapsed, Ready, Refresh, exception =>
            {
                if (readout != null) readout.Cleanup();
                readout = null;
                Main.Log(exception);
            });
        }

        private static bool Ready()
        {
            AlienHateHistory.SyncContext();
            if (GameControl.loadcycle100 && GameControl.control != null
                && GameControl.control.activePlayer != null) return true;
            if (readout != null) readout.Clear();
            return false;
        }

        private static void Refresh()
        {
            AlienHateHistory.Install();
            var current = GeneralControlsController.Singleton;
            if (controller != current)
            {
                if (readout != null) readout.Cleanup();
                readout = null;
                controller = current;
            }
            if (readout == null) readout = AlienHateDebugReadout.Attach(controller);
            try { if (readout != null) readout.UpdateValue(); }
            catch
            {
                if (readout != null) readout.Clear();
                throw;
            }
        }
    }
}
