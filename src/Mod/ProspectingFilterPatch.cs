using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using UnityEngine.UI;

namespace GiveUpNation
{
    // Deliberately no HarmonyPatch attribute: install only after game readiness,
    // separately from startup PatchAll and the working nation/tooltip features.
    internal static class ProspectingFilterPatch
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var toggleField = AccessTools.Field(typeof(IntelScreenController), "filterProspected");
            var isOn = AccessTools.PropertyGetter(typeof(Toggle), "isOn");
            var prospected = AccessTools.Method(typeof(TIFactionState), "Prospected", new[] { typeof(TISpaceBodyState) });
            var gates = Enumerable.Range(1, code.Count - 1).Where(i =>
                code[i - 1].LoadsField(toggleField) && code[i].Calls(isOn)).ToList();
            var predicates = Enumerable.Range(0, code.Count).Where(i => code[i].Calls(prospected)).ToList();
            if (gates.Count != 1 || predicates.Count != 1 || gates[0] >= predicates[0])
                throw new InvalidOperationException("Prospecting filters: native status condition changed; refusing to patch.");

            // Preserve the original branches and every other filter. Each helper
            // consumes the original call arguments plus this controller.
            for (int i = 0; i < code.Count; i++)
            {
                if (i != gates[0] && i != predicates[0]) { yield return code[i]; continue; }
                var loadController = new CodeInstruction(OpCodes.Ldarg_0);
                loadController.labels.AddRange(code[i].labels);
                loadController.blocks.AddRange(code[i].blocks);
                yield return loadController;
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ProspectingFilterPatch),
                    i == gates[0] ? nameof(HasSelection) : nameof(Matches)));
            }
        }

        internal static bool HasSelection(Toggle original, IntelScreenController controller)
        {
            var ui = controller.GetComponent<ProspectingFiltersUi>();
            return original.isOn || (Main.Enabled && ui != null && ui.HasAdditionalSelection);
        }

        internal static bool Matches(TIFactionState faction, TISpaceBodyState body, IntelScreenController controller)
        {
            var ui = controller.GetComponent<ProspectingFiltersUi>();
            if (!Main.Enabled || ui == null || !ui.Ready) return faction.Prospected(body);
            return ui.Matches(faction, body);
        }
    }
}
