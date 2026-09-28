using System;
using System.Collections.Generic;
using GiveUpNation;
using PavonisInteractive.TerraInvicta;

// Stand-ins test the production text provider's use of the current game's data.
// They do not emulate the scheduler, Unity raycasting, or TooltipTrigger lifecycle.
namespace PavonisInteractive.TerraInvicta
{
    public sealed class TISpaceBodyState { }
    public sealed class TIDateTime
    {
        public string Formatted;
        public double Day;
        public double DifferenceInDays(TIDateTime other) => Day - other.Day;
        public string ToCustomDateString() => Formatted;
    }
    public static class TITimeState
    {
        public static TIDateTime Current = new TIDateTime();
        public static TIDateTime Now() => Current;
    }
    public sealed class TIFactionState
    {
        public readonly Dictionary<TISpaceBodyState, TIDateTime> Arrivals = new();
        public TIDateTime ProspectorArrival(TISpaceBodyState body) => Arrivals.TryGetValue(body, out var date) ? date : null;
    }
}
public static class Loc
{
    public static readonly Dictionary<string, string> Text = new()
    {
        ["UI.Space.ProspectorEnRoute"] = "Prospector Probe En Route",
        ["UI.Space.ProbeArrival"] = "Survey Completion: {0}"
    };
    public static string T(string key) => Text[key];
    public static string T(string key, params object[] values) => string.Format(Text[key], values);
}
internal static class Tests
{
    private static int passed;
    private static void Test(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
    public static void Main()
    {
        var f = new TIFactionState();
        var body = new TISpaceBodyState();
        Test("null faction has no tooltip", () => Check(ProbeArrivalText.Build(null, body) == null));
        Test("null body has no tooltip", () => Check(ProbeArrivalText.Build(f, null) == null));
        Test("no scheduled probe has no date", () => Check(ProbeArrivalText.Build(f, body) == null));
        Test("in-flight probe uses scheduled date and game formatter", () => {
            f.Arrivals[body] = new TIDateTime { Formatted = "12 March 2031" };
            Check(ProbeArrivalText.Build(f, body) == "Prospector Probe En Route\nSurvey Completion: 12 March 2031 (0.0 days)");
        });
        Test("replacement probe date is read afresh", () => {
            f.Arrivals[body] = new TIDateTime { Formatted = "1 February 2031" };
            Check(ProbeArrivalText.Build(f, body).Contains("1 February 2031"));
        });
        Test("reused row does not retain another body's date", () => {
            var nextBody = new TISpaceBodyState();
            Check(ProbeArrivalText.Build(f, nextBody) == null);
            f.Arrivals[nextBody] = new TIDateTime { Formatted = "9 April 2032" };
            Check(ProbeArrivalText.Build(f, nextBody).Contains("9 April 2032"));
        });
        Test("player switch does not expose prior faction's date", () => Check(ProbeArrivalText.Build(new TIFactionState(), body) == null));
        Test("survey completion clears the old arrival text", () => {
            f.Arrivals.Remove(body);
            Check(ProbeArrivalText.Build(f, body) == null);
        });
        Test("remaining days show decimals and follow the current campaign clock", () => {
            f.Arrivals[body] = new TIDateTime { Formatted = "future", Day = 42.5 };
            TITimeState.Current.Day = 40;
            Check(ProbeArrivalText.Build(f, body).EndsWith("(2.5 days)"));
            TITimeState.Current.Day = 42;
            Check(ProbeArrivalText.Build(f, body).EndsWith("(0.5 days)"));
            TITimeState.Current.Day = 42.5;
            Check(ProbeArrivalText.Build(f, body).EndsWith("(0.0 days)"));
            TITimeState.Current.Day = 43;
            Check(ProbeArrivalText.Build(f, body).EndsWith("(0.0 days)"));
            TITimeState.Current.Day = 0;
        });
        Test("native localization is applied to both lines", () => {
            Loc.Text["UI.Space.ProspectorEnRoute"] = "Sonde en route";
            Loc.Text["UI.Space.ProbeArrival"] = "Fin du relevé : {0}";
            f.Arrivals[body] = new TIDateTime { Formatted = "2 mai 2031" };
            Check(ProbeArrivalText.Build(f, body) == "Sonde en route\nFin du relevé : 2 mai 2031 (0.0 days)");
        });
        Test("disabled mod does not inspect game readiness", () => {
            var scheduler = new ProbeScanScheduler();
            scheduler.Tick(false, 1, () => throw new Exception("Readiness touched"), () => throw new Exception("Scanned"), _ => throw new Exception("Logged"));
        });
        Test("startup waits for readiness before scanning", () => {
            var scheduler = new ProbeScanScheduler(); var scans = 0;
            scheduler.Tick(true, 100, () => false, () => scans++, _ => { });
            Check(scans == 0);
            scheduler.Tick(true, 0, () => true, () => scans++, _ => { });
            Check(scans == 1);
        });
        Test("ready UI discovery is throttled", () => {
            var scheduler = new ProbeScanScheduler(); var scans = 0;
            scheduler.Tick(true, 0, () => true, () => scans++, _ => { });
            scheduler.Tick(true, 0.2f, () => true, () => scans++, _ => { });
            Check(scans == 1);
            scheduler.Tick(true, 0.9f, () => true, () => scans++, _ => { });
            Check(scans == 2);
        });
        Test("discovery failure is isolated and retry can recover", () => {
            var scheduler = new ProbeScanScheduler(); var logs = 0; var scans = 0;
            scheduler.Tick(true, 0, () => true, () => throw new Exception("Not ready"), _ => logs++);
            scheduler.Tick(true, 1, () => true, () => scans++, _ => logs++);
            Check(logs == 1 && scans == 1);
        });
        Test("readiness failures cannot escape or spam logs", () => {
            var scheduler = new ProbeScanScheduler(); var logs = 0;
            for (var i = 0; i < 5; i++)
                scheduler.Tick(true, 1, () => throw new Exception("Readiness unavailable"), () => throw new Exception("Must not scan"), _ => logs++);
            Check(logs == 1);
        });
        Test("loading another campaign suspends and resumes discovery", () => {
            var scheduler = new ProbeScanScheduler(); var scans = 0;
            scheduler.Tick(true, 0, () => true, () => scans++, _ => { });
            scheduler.Tick(true, 0.1f, () => false, () => scans++, _ => { });
            Check(scans == 1);
            scheduler.Tick(true, 0, () => true, () => scans++, _ => { });
            Check(scans == 2);
        });
        Test("disable and reenable resets the scan delay", () => {
            var scheduler = new ProbeScanScheduler(); var scans = 0;
            scheduler.Tick(true, 0, () => true, () => scans++, _ => { });
            scheduler.Tick(false, 0, () => false, () => scans++, _ => { });
            scheduler.Tick(true, 0, () => true, () => scans++, _ => { });
            Check(scans == 2);
        });
        Console.WriteLine($"{passed} probe-tooltip checks passed (offline stand-ins; no Unity execution).");
    }
}
