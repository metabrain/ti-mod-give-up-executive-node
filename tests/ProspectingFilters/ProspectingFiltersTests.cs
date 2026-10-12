using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GiveUpNation;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using UnityEngine.UI;

internal static class Program
{
    private static int checks;
    private static void Check(bool result, string name)
    {
        if (!result) throw new Exception(name);
        checks++;
    }

    private static void Main()
    {
        SnapshotChecks();
        HistoryChecks();
        var body = new TISpaceBodyState();
        var faction = new TIFactionState();
        for (int state = 0; state < 16; state++)
        {
            faction.Completed = (state & 1) != 0;
            faction.Probe = (state & 2) != 0;
            faction.Fleet = (state & 4) != 0;
            faction.CanProbe = (state & 8) != 0;
            var expected = faction.Completed ? ProspectingStatus.Prospected
                : (faction.Probe || faction.Fleet ? ProspectingStatus.Prospecting : faction.CanProbe ? ProspectingStatus.Prospectable : ProspectingStatus.Unprospected);
            Check(ProspectingState.GetStatus(faction, body) == expected, "native status mapping " + state);
        }

        var controller = new IntelScreenController { Ui = new ProspectingFiltersUi() };
        var (method, instructions) = Fixture();
        var patched = ProspectingFilterPatch.Transpiler(instructions).ToList();
        Emit(method.GetILGenerator(), patched);
        var visible = (Func<IntelScreenController, TIFactionState, TISpaceBodyState, bool, bool>)method.CreateDelegate(
            typeof(Func<IntelScreenController, TIFactionState, TISpaceBodyState, bool, bool>));

        // Execute the actual rewritten IL against game stand-ins, including the
        // unmodified native "other filter" branch after the status condition.
        for (int selection = 0; selection < 16; selection++)
        {
            controller.filterProspected.isOn = (selection & 1) != 0;
            controller.Ui.Unprospected = (selection & 2) != 0;
            controller.Ui.Prospecting = (selection & 4) != 0;
            controller.Ui.Prospectable = (selection & 8) != 0;
            string expectedStatuses = ((selection & 1) != 0 ? "C" : "")
                + ((selection & 2) != 0 ? "U" : "")
                + ((selection & 4) != 0 ? "P" : "")
                + ((selection & 8) != 0 ? "Q" : "");
            for (int state = 0; state < 16; state++)
            {
                faction.Completed = (state & 1) != 0;
                faction.Probe = (state & 2) != 0;
                faction.Fleet = (state & 4) != 0;
                faction.CanProbe = (state & 8) != 0;
                char category = faction.Completed ? 'C' : faction.Probe || faction.Fleet ? 'P' : faction.CanProbe ? 'Q' : 'U';
                Check(visible(controller, faction, body, true) == (expectedStatuses.Length == 0 || expectedStatuses.Contains(category)),
                    $"combined filters selection={selection} state={state}");
                Check(!visible(controller, faction, body, false), "other native filters still exclude rows");
            }
        }

        controller.filterProspected.isOn = false;
        controller.Ui.Unprospected = false;
        controller.Ui.Prospecting = true;
        controller.Ui.Prospectable = false;
        faction.Completed = faction.Probe = faction.Fleet = false;
        Check(!visible(controller, faction, body, true), "not started excluded from underway");
        faction.Probe = true;
        Check(visible(controller, faction, body, true), "launch immediately changes status");
        faction.Completed = true;
        Check(!visible(controller, faction, body, true), "completion overrides old probe flag");
        faction.Completed = faction.Probe = false;
        faction.Fleet = true;
        Check(visible(controller, faction, body, true), "fleet-only survey included");
        faction.Fleet = false;
        Check(!visible(controller, faction, body, true), "cancelled fleet survey removed");
        controller.Ui.Prospecting = false;
        controller.Ui.Prospectable = true;
        faction.CanProbe = true;
        Check(visible(controller, faction, body, true), "eligible unstarted body is prospectable");
        faction.CanProbe = false;
        Check(!visible(controller, faction, body, true), "ineligible unstarted body is not prospectable");
        controller.Ui.Prospectable = false;
        controller.Ui.Prospecting = true;
        var secondPlayer = new TIFactionState { Probe = true };
        Check(visible(controller, secondPlayer, body, true), "classification follows current faction");
        Check(!visible(controller, faction, body, true), "no cached other-faction knowledge");

        GiveUpNation.Main.Enabled = false;
        Check(visible(controller, faction, body, true), "disabled patch falls back to native unchecked");
        controller.filterProspected.isOn = true;
        Check(!visible(controller, faction, body, true), "disabled patch falls back to native checked");
        faction.Completed = true;
        Check(visible(controller, faction, body, true), "disabled native completed remains visible");
        GiveUpNation.Main.Enabled = true;
        controller.Ui = null;
        faction.Completed = false;
        Check(!visible(controller, faction, body, true), "unattached controller uses native filtering");
        controller.filterProspected.isOn = false;
        Check(visible(controller, faction, body, true), "unattached controller native no filter");
        controller.Ui = new ProspectingFiltersUi { Ready = false, Prospecting = true };
        Check(visible(controller, faction, body, true), "partially initialized UI has no effect");

        ExpectRejected(instructions.Where(i => !i.Calls(AccessTools.PropertyGetter(typeof(Toggle), "isOn"))), "missing gate");
        ExpectRejected(instructions.Concat(instructions), "duplicate condition");
        ExpectRejected(instructions.Select(i => i.Calls(AccessTools.Method(typeof(TIFactionState), "Prospected", new[] { typeof(TISpaceBodyState) }))
            ? new CodeInstruction(OpCodes.Nop) : i), "missing predicate");
        Check(patched.Count == instructions.Count + 2, "only two controller arguments inserted");
        Check(instructions.Any(i => i.Calls(AccessTools.PropertyGetter(typeof(Toggle), "isOn"))), "input IL remains untouched");
        Console.WriteLine($"{checks} prospecting-filter checks passed (production predicate/transpiler, game stand-ins).");
    }

    private static void ExpectRejected(IEnumerable<CodeInstruction> instructions, string name)
    {
        try { ProspectingFilterPatch.Transpiler(instructions).ToList(); }
        catch (InvalidOperationException) { checks++; return; }
        throw new Exception("Unsafe patch accepted: " + name);
    }

    private static (DynamicMethod, List<CodeInstruction>) Fixture()
    {
        var method = new DynamicMethod("NativeVisibilityFixture", typeof(bool),
            new[] { typeof(IntelScreenController), typeof(TIFactionState), typeof(TISpaceBodyState), typeof(bool) }, typeof(Program), true);
        var generator = method.GetILGenerator();
        var keepChecking = generator.DefineLabel();
        var otherFilter = new CodeInstruction(OpCodes.Ldarg_3);
        otherFilter.labels.Add(keepChecking);
        // Label attached to a replaced call must move to the inserted ldarg.0.
        var gateLabel = generator.DefineLabel();
        var gate = new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Toggle), "isOn"));
        gate.labels.Add(gateLabel);
        return (method, new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(IntelScreenController), "filterProspected")),
            gate,
            new CodeInstruction(OpCodes.Brfalse, keepChecking),
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(TIFactionState), "Prospected", new[] { typeof(TISpaceBodyState) })),
            new CodeInstruction(OpCodes.Brtrue, keepChecking),
            new CodeInstruction(OpCodes.Ldc_I4_0),
            new CodeInstruction(OpCodes.Ret),
            otherFilter,
            new CodeInstruction(OpCodes.Ret)
        });
    }

    private static void SnapshotChecks()
    {
        var snapshot = new StatusSnapshot<int>();
        int evaluations = 0;
        ProspectingStatus Evaluate(int body) { evaluations++; return ProspectingStatus.Prospectable; }
        snapshot.Begin();
        for (int i = 0; i < 1000; i++) snapshot.Add(i, Evaluate(i));
        Check(snapshot.Complete(), "initial snapshot changes visibility");
        for (int pass = 0; pass < 3; pass++)
            for (int i = 0; i < 1000; i++) snapshot.Get(i, Evaluate);
        Check(evaluations == 1000, "visibility reuses the snapshot without game queries");
        snapshot.Begin();
        for (int i = 0; i < 1000; i++) snapshot.Add(i, Evaluate(i));
        Check(!snapshot.Complete(), "unchanged snapshot avoids list rebuild");
        snapshot.Begin();
        for (int i = 1; i <= 1000; i++) snapshot.Add(i, Evaluate(i));
        Check(snapshot.Complete(), "same-count replacement detected");
        int before = evaluations;
        snapshot.Get(0, Evaluate);
        snapshot.Get(0, Evaluate);
        Check(evaluations == before + 1, "removed/new body evaluated once on demand");
        snapshot.Clear();
        snapshot.Get(0, Evaluate);
        Check(evaluations == before + 2, "context invalidation discards stale knowledge");
        snapshot.Begin();
        snapshot.Add(0, ProspectingStatus.Prospected);
        Check(snapshot.Complete(), "completion changes visibility");
        Check(snapshot.Get(0, Evaluate) == ProspectingStatus.Prospected, "completed result published");
        snapshot.Begin();
        Check(snapshot.Complete(), "empty list removes prior bodies");
    }

    private static void HistoryChecks()
    {
        var history = new HateHistory();
        Check(history.Tooltip().Contains("No changes"), "empty hate history");
        history.Add("date", "ignored", 10, 20, 20);
        Check(history.Count == 0, "clamped no-op omitted");
        history.Add("date", "<color=red>cause", 10, 20, 23);
        Check(history.Recent(0).Requested == 10 && history.Recent(0).After == 23, "requested and actual values distinct");
        Check(!history.Tooltip().Contains("<color"), "cause cannot inject rich text");
        history.Add("date", "retaliation", -5, 23, 18);
        Check(history.Recent(0).After - history.Recent(0).Before == -5, "hate reductions recorded");
        for (int i = 0; i < 110; i++) history.Add(i.ToString(), "event", 1, i, i + 1);
        Check(history.Count == 100, "history bounded at 100");
        Check(history.Recent(0).Before == 109 && history.Recent(99).Before == 10, "ring order and eviction");
        Check(history.Tooltip().Contains("latest 12 of 100"), "tooltip bounded to readable recent history");
        history.Clear();
        Check(history.Count == 0 && history.Tooltip().Contains("No changes"), "clear removes prior campaign history");
        var alien = GameStateManager.Alien;
        var player = GameControl.control.activePlayer;
        var before = AccessTools.Method(typeof(AlienHateHistory), "Before");
        var after = AccessTools.Method(typeof(AlienHateHistory), "After");
        object Capture(TIFactionState owner, TIFactionState target, float requested, string cause)
        {
            object[] args = { owner, target, requested, cause, null };
            before.Invoke(null, args);
            return args[4];
        }
        alien.Hate = 20;
        var captured = Capture(alien, player, 10, "capture test");
        alien.Hate = 23;
        after.Invoke(null, new[] { alien, player, captured });
        Check(AlienHateHistory.Tooltip().Contains("20 -> 23") && AlienHateHistory.Tooltip().Contains("requested +10"), "production interception captures actual delta");
        var other = Capture(new TIFactionState(), player, 10, "wrong faction");
        after.Invoke(null, new[] { alien, player, other });
        Check(!AlienHateHistory.Tooltip().Contains("wrong faction"), "other faction ignored");
        GameControl.loadcycle100 = false;
        AlienHateHistory.SyncContext();
        GameControl.loadcycle100 = true;
        after.Invoke(null, new[] { alien, player, captured });
        Check(AlienHateHistory.Tooltip().Contains("No changes"), "loading clears history and rejects stale call");
        captured = Capture(alien, player, 10, "old player");
        GameControl.control.activePlayer = new TIFactionState();
        after.Invoke(null, new[] { alien, player, captured });
        Check(AlienHateHistory.Tooltip().Contains("No changes"), "player switch rejects stale call");
        var savedHistory = new HateHistory();
        savedHistory.Add("saved date", "saved event", 5, 10, 15);
        var saved = savedHistory.Snapshot("42");
        GameControl.loadcycle100 = false;
        var loader = new System.Threading.Thread(() =>
        {
            AlienHateHistory.BeginLoad();
            AlienHateHistory.FinishLoad(saved);
        });
        loader.Start(); loader.Join();
        Check(!AlienHateHistory.SyncContext() && AlienHateHistory.ExportForSave() == null, "pending restore waits for ready campaign");
        GameControl.control.activePlayer = new TIFactionState { ID = 42 };
        GameControl.loadcycle100 = true;
        Check(AlienHateHistory.Tooltip().Contains("10 -> 15"), "pending history restored to matching saved faction");
        player = GameControl.control.activePlayer;
        alien.Hate = 15;
        captured = Capture(alien, player, 2, "later event");
        alien.Hate = 17;
        after.Invoke(null, new[] { alien, player, captured });
        var detached = AlienHateHistory.ExportForSave();
        Check(detached.Entries.Length == 2 && detached.Entries[1].After == 17, "recording continues after restore without replaying old snapshot");
        AlienHateHistory.BeginLoad();
        Check(!AlienHateHistory.SyncContext(), "load scope blocks recording even before readiness flag changes");
        AlienHateHistory.FinishLoad(saved);
        after.Invoke(null, new[] { alien, player, captured });
        Check(!AlienHateHistory.Tooltip().Contains("later event") && AlienHateHistory.ExportForSave().Entries.Length == 1,
            "older save replaces future timeline and stale intercepted calls");
        Check(detached.Entries.Length == 2, "save snapshot survives later history clearing");
        AlienHateHistory.BeginLoad(); AlienHateHistory.FinishLoad(null);
        Check(AlienHateHistory.Tooltip().Contains("No changes"), "missing/rejected save payload starts empty");
        AlienHateHistory.BeginLoad(); AlienHateHistory.FinishLoad(saved);
        GameControl.control.activePlayer = new TIFactionState { ID = 99 };
        Check(AlienHateHistory.Tooltip().Contains("No changes"), "different saved faction cannot inherit history");
        GameControl.control.activePlayer = new TIFactionState { ID = 42 };
        Check(AlienHateHistory.Tooltip().Contains("No changes"), "rejected faction payload not retained for later switch");
        AlienHateHistory.BeginLoad(); AlienHateHistory.FinishLoad(saved);
        AlienHateHistory.ClearCampaign();
        Check(AlienHateHistory.Tooltip().Contains("No changes"), "new campaign discards pending restore");
        AlienHateHistory.BeginLoad(); AlienHateHistory.FinishLoad(saved);
        AlienHateHistory.Reset();
        Check(AlienHateHistory.Tooltip().Contains("No changes"), "mod reset discards pending restore");
    }

    private static void Emit(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            foreach (var label in instruction.labels) generator.MarkLabel(label);
            if (instruction.operand is MethodInfo method) generator.Emit(instruction.opcode, method);
            else if (instruction.operand is FieldInfo field) generator.Emit(instruction.opcode, field);
            else if (instruction.operand is Label label) generator.Emit(instruction.opcode, label);
            else if (instruction.operand == null) generator.Emit(instruction.opcode);
            else throw new Exception("Unsupported fixture operand");
        }
    }
}

namespace UnityEngine.UI
{
    public class Toggle { public bool isOn { get; set; } }
}
namespace PavonisInteractive.TerraInvicta
{
    public class TISpaceBodyState { }
    public class TIFactionState
    {
        public int ID = 42;
        public float Hate;
        public float GetFactionHate(TIFactionState target) => Hate;
        public bool Completed, Probe, Fleet, CanProbe;
        public bool Prospected(TISpaceBodyState body) => Completed;
        public bool ProspectorEnRoute(TISpaceBodyState body) => Probe;
        public bool FleetSurveyingPlanet(TISpaceBodyState body) => Fleet;
        public bool CanProspectWithProbe(TISpaceBodyState body, bool allowOvertake) => CanProbe;
    }
    public class IntelScreenController
    {
        public Toggle filterProspected = new Toggle();
        internal ProspectingFiltersUi Ui;
        public T GetComponent<T>() where T : class
        {
            if (Ui != null) Ui.Original = filterProspected;
            return Ui as T;
        }
    }
    public class GameControl
    {
        public static bool loadcycle100 = true;
        public static GameControl control = new GameControl();
        public TIFactionState activePlayer = new TIFactionState();
    }
    public static class GameStateManager
    {
        public static TIFactionState Alien = new TIFactionState();
        public static TIFactionState AlienFaction() => Alien;
    }
    public class TITimeState
    {
        public static TITimeState Now() => new TITimeState();
        public string ToCustomDateString() => "Test date";
    }
}
namespace GiveUpNation
{
    internal static class Main
    {
        internal static bool Enabled = true;
        internal static void Log(Exception e) { throw new Exception("Unexpected history error", e); }
    }
    internal class ProspectingFiltersUi
    {
        internal bool Ready = true, Unprospected, Prospectable, Prospecting;
        internal Toggle Original;
        internal bool HasAdditionalSelection => Ready && (Unprospected || Prospectable || Prospecting);
        internal bool Matches(TIFactionState faction, TISpaceBodyState body) => ProspectingSelection.Matches(
            ProspectingState.GetStatus(faction, body), Original.isOn, Unprospected, Prospectable, Prospecting);
    }
}
