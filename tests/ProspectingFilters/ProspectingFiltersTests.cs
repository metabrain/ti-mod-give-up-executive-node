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
        var body = new TISpaceBodyState();
        var faction = new TIFactionState();
        for (int state = 0; state < 8; state++)
        {
            faction.Completed = (state & 1) != 0;
            faction.Probe = (state & 2) != 0;
            faction.Fleet = (state & 4) != 0;
            var expected = faction.Completed ? ProspectingStatus.Prospected
                : (faction.Probe || faction.Fleet ? ProspectingStatus.Prospecting : ProspectingStatus.Unprospected);
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
        string[] expectedStatuses = { "UPC", "C", "U", "UC", "P", "PC", "UP", "UPC" };
        for (int selection = 0; selection < 8; selection++)
        {
            controller.filterProspected.isOn = (selection & 1) != 0;
            controller.Ui.Unprospected = (selection & 2) != 0;
            controller.Ui.Prospecting = (selection & 4) != 0;
            for (int state = 0; state < 8; state++)
            {
                faction.Completed = (state & 1) != 0;
                faction.Probe = (state & 2) != 0;
                faction.Fleet = (state & 4) != 0;
                char category = faction.Completed ? 'C' : faction.Probe || faction.Fleet ? 'P' : 'U';
                Check(visible(controller, faction, body, true) == expectedStatuses[selection].Contains(category),
                    $"combined filters selection={selection} state={state}");
                Check(!visible(controller, faction, body, false), "other native filters still exclude rows");
            }
        }

        controller.filterProspected.isOn = false;
        controller.Ui.Unprospected = false;
        controller.Ui.Prospecting = true;
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
        public bool Completed, Probe, Fleet;
        public bool Prospected(TISpaceBodyState body) => Completed;
        public bool ProspectorEnRoute(TISpaceBodyState body) => Probe;
        public bool FleetSurveyingPlanet(TISpaceBodyState body) => Fleet;
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
}
namespace GiveUpNation
{
    internal static class Main { internal static bool Enabled = true; }
    internal class ProspectingFiltersUi
    {
        internal bool Ready = true, Unprospected, Prospecting;
        internal Toggle Original;
        internal bool HasAdditionalSelection => Ready && (Unprospected || Prospecting);
        internal bool Matches(TIFactionState faction, TISpaceBodyState body) => ProspectingSelection.Matches(
            ProspectingState.GetStatus(faction, body), Original.isOn, Unprospected, Prospecting);
    }
}
