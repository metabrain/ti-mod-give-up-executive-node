using System;
using System.Globalization;
using System.Linq;
namespace GiveUpNation
{
    internal static class TradeAcceptanceText
    {
        internal static float Score(float[] categories) => categories.Where(x => x > 0).Sum() + 3f * categories.Where(x => x < 0).Sum();
        internal static float Share(float ai, float player) => ai <= 0 ? 0 : player <= 0 ? 1 : ai / (ai + player);
        internal static string Number(float value) => value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
        internal static string Percent(float value) => (value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        internal static string CountBlocker(int givenHabs, int receivedHabs, int givenOrgs, int receivedOrgs)
        {
            if (givenHabs > Math.Max(1, receivedHabs)) return "Habs: AI gives " + givenHabs + "; maximum " + Math.Max(1, receivedHabs);
            if (givenOrgs > receivedOrgs + 2) return "Orgs: AI gives " + givenOrgs + "; maximum " + (receivedOrgs + 2);
            return null;
        }
        internal static string IncomeBlocker(string resource, float change, float income, bool mc)
        {
            if (change >= 0) return null;
            if (mc) return income + change < 0 ? "Mission Control: " + Number(income) + " -> " + Number(income + change) + "; minimum 0" : null;
            if (income <= 0 || income + change <= 0) return "AI " + resource + " monthly income: " + Number(income) + " -> " + Number(income + change) + "; both must be positive when losing income";
            return (income + change) / income < .6f ? "AI " + resource + " income retained: " + Percent((income + change) / income) + " / 60% required" : null;
        }
        internal static string Result(bool empty, bool offered, string blocker)
        {
            if (empty || blocker == null) return string.Empty;
            return offered ? "Bypassed by original AI offer:\n" + blocker : blocker;
        }
    }
}
