using System;
using System.Collections.Generic;
using System.Linq;
using ModelShark;
using PavonisInteractive.TerraInvicta;
using UnityEngine;
namespace GiveUpNation
{
    public sealed class TreatyTooltips : MonoBehaviour
    {
        private static readonly HashSet<TreatyTooltips> owned = new HashSet<TreatyTooltips>();
        private readonly List<Action> restore = new List<Action>();
        private readonly HashSet<TooltipTrigger> bound = new HashSet<TooltipTrigger>();
        private DiplomacyController controller;
        private bool failed;
        internal static void BeforeBankRefresh(DiplomacyController __instance)
        {
            var ui = __instance.GetComponent<TreatyTooltips>();
            if (ui != null) ui.Restore();
        }
        internal static void Attach(DiplomacyController __instance)
        {
            if (!Main.Enabled || !GameControl.loadcycle100 || __instance.tradingFaction == null) return;
            TreatyTooltips ui = null;
            try
            {
                ui = __instance.GetComponent<TreatyTooltips>();
                if (ui == null) { ui = __instance.gameObject.AddComponent<TreatyTooltips>(); owned.Add(ui); }
                ui.Restore(); ui.controller = __instance;
                if (ui.failed) return;
                ui.BindAll();
            }
            catch (Exception e) { if (ui != null) { ui.Restore(); ui.failed = true; } Main.Log(new InvalidOperationException("Treaty diagnostics unavailable for this screen.", e)); }
        }
        private void BindAll()
        {
            var c = controller;
            BindInfo(c.playerBankNAPItem_Info, TradeOffer.TreatyType.NAP);
            BindInfo(c.aiBankNAPItem_Info, TradeOffer.TreatyType.NAP);
            BindInfo(c.playerBankTruceItem_Info, TradeOffer.TreatyType.Truce);
            BindInfo(c.aiBankTruceItem_Info, TradeOffer.TreatyType.Truce);
            BindInfo(c.playerBankIntelItem_Info, TradeOffer.TreatyType.Intel);
            BindInfo(c.aiBankIntelItem_Info, TradeOffer.TreatyType.Intel);
            Bind(c.playerBankTreatyItem.tooltipTrigger, () => c.playerTableTreatyItem.treaty);
            Bind(c.aiBankTreatyItem.tooltipTrigger, () => c.aiTableTreatyItem.treaty);
            Bind(c.playerTableTreatyItem.tooltipTrigger, () => c.playerTableTreatyItem.treaty);
            Bind(c.aiTableTreatyItem.tooltipTrigger, () => c.aiTableTreatyItem.treaty);
            Bind(c.playerBankExchangeIntelItem.tooltipTrigger, () => TradeOffer.TreatyType.Intel);
            Bind(c.aiBankExchangeIntelItem.tooltipTrigger, () => TradeOffer.TreatyType.Intel);
            Bind(c.playerTableExchangeIntelItem.tooltipTrigger, () => TradeOffer.TreatyType.Intel);
            Bind(c.aiTableExchangeIntelItem.tooltipTrigger, () => TradeOffer.TreatyType.Intel);
        }
        private void BindInfo(DiplomacyBankListItem item, TradeOffer.TreatyType treaty)
        {
            if (item == null) return;
            var ai = controller.tradingFaction;
            var player = GameControl.control.activePlayer;
            bool unavailable = !ai.CanTradeTreaty(player, treaty) || AIEvaluators.GetWillingnessToTradeTreaty(ai, player, treaty) <= 0;
            // Native UI omits some informational rows when eligibility passes but willingness fails.
            if (unavailable && !item.gameObject.activeSelf)
            {
                if (item.button != null)
                {
                    bool interactable = item.button.interactable;
                    item.button.interactable = false;
                    restore.Add(() => { if (item != null && item.button != null) item.button.interactable = interactable; });
                }
                item.gameObject.SetActive(true);
                restore.Add(() => { if (item != null) item.gameObject.SetActive(false); });
            }
            Bind(item.tooltipTrigger, () => treaty);
        }
        private void Bind(TooltipTrigger trigger, Func<TradeOffer.TreatyType> treaty)
        {
            if (trigger == null || !bound.Add(trigger)) return;
            if (trigger.parameterizedTextFields == null)
            {
                var fields = new List<ParameterizedTextField>();
                trigger.parameterizedTextFields = fields;
                restore.Add(() => { if (trigger != null && ReferenceEquals(trigger.parameterizedTextFields, fields)) trigger.parameterizedTextFields = null; });
            }
            var field = trigger.parameterizedTextFields.FirstOrDefault(x => x.name == "BodyText");
            if (field == null)
            {
                field = new ParameterizedTextField { name = "BodyText", value = string.Empty };
                var fields = trigger.parameterizedTextFields;
                fields.Add(field);
                var added = field;
                restore.Add(() => fields.Remove(added));
            }
            var original = field.del;
            bool demand = field.valueOnDemand;
            string value = field.value;
            int width = trigger.maxTextWidth;
            ParameterizedTextField.BuildStringOnTooltipHover wrapper = () =>
            {
                string native = demand && original != null ? original() : value;
                if (!Main.Enabled || failed || controller == null || !GameControl.loadcycle100) return native;
                try { return native + TreatyDiagnostics.Build(controller.tradingFaction, GameControl.control.activePlayer, treaty()); }
                catch (Exception e) { if (!failed) Main.Log(e); failed = true; return native; }
            };
            field.del = wrapper; field.valueOnDemand = true; trigger.maxTextWidth = Math.Max(width, 700);
            restore.Add(() =>
            {
                // Do not overwrite a later replacement from native UI or another mod.
                if (field.del == wrapper) { field.del = original; field.valueOnDemand = demand; }
                if (trigger != null && trigger.maxTextWidth == Math.Max(width, 700)) trigger.maxTextWidth = width;
            });
        }
        private void Restore()
        {
            foreach (var action in restore) action();
            restore.Clear(); bound.Clear();
        }
        private void OnDestroy() { Restore(); owned.Remove(this); }
        internal static void ResetAll()
        {
            foreach (var ui in owned.ToArray()) if (ui != null) { ui.Restore(); Destroy(ui); }
            owned.Clear();
        }
    }
}
