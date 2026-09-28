using System;

namespace GiveUpNation
{
    // Readiness is evaluated before discovering any game UI. Errors are contained
    // here so this optional feature cannot fail UMM's main mod activation.
    internal sealed class ProbeScanScheduler
    {
        private float remaining;
        private bool reportedFailure;

        internal void Reset() { remaining = 0; reportedFailure = false; }

        internal void Tick(bool enabled, float elapsed, Func<bool> ready, Action scan, Action<Exception> log)
        {
            if (!enabled) { Reset(); return; }
            try
            {
                if (!ready()) { remaining = 0; return; }
                remaining -= Math.Max(0, elapsed);
                if (remaining > 0) return;
                remaining = 1f;
                scan();
            }
            catch (Exception exception)
            {
                remaining = 1f;
                if (reportedFailure) return;
                reportedFailure = true;
                log(exception);
            }
        }
    }
}
