using System;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using ModelShark;
using System.Collections.Generic;

namespace GiveUpNation
{
    internal sealed class AlienHateDebugReadout : MonoBehaviour
    {
        private TMP_Text text;
        private TMP_Text floorText;
        private TMP_Text quietnessText;
        private float nextUpdate;
        private GameObject ownedText, ownedFloor, ownedQuietness;
        private bool cleaned;

        internal static AlienHateDebugReadout Attach(GeneralControlsController controller)
        {
            if (controller == null || controller.alienThreatPanel == null) return null;
            var parent = controller.alienThreatPanel.transform.parent;
            if (parent == null) return null;
            var existing = parent.GetComponent<AlienHateDebugReadout>();
            if (existing != null) return existing.cleaned ? null : existing;
            var readout = parent.gameObject.AddComponent<AlienHateDebugReadout>();
            try { readout.Initialize(controller.alienThreatPanel.transform, controller.alienAlertTip); }
            catch { readout.Cleanup(); throw; }
            return readout;
        }

        private void Initialize(Transform panel, TooltipTrigger source)
        {
            var go = new GameObject("GiveUpNation.RawAlienHate", typeof(RectTransform), typeof(TextMeshProUGUI));
            ownedText = go;
            go.transform.SetParent(panel, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(1, .5f);
            rect.anchoredPosition = new Vector2(-40, -48);
            rect.sizeDelta = new Vector2(48, 24);
            text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = 14;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.color = Color.red;
            text.raycastTarget = false;
            // Keep the readout alive when the native panel is hidden by the
            // campaign's threat-monitor unlock gate.
            go.transform.SetParent(transform, true);
            var floorGo = Instantiate(go, transform, true);
            ownedFloor = floorGo;
            floorGo.name = "GiveUpNation.RawAlienHateFloor";
            floorText = floorGo.GetComponent<TextMeshProUGUI>();
            floorText.fontSize = 12;
            floorText.rectTransform.anchoredPosition += new Vector2(0, -18);
            var quietnessGo = Instantiate(floorGo, transform, true);
            ownedQuietness = quietnessGo;
            quietnessGo.name = "GiveUpNation.AlienQuietness";
            quietnessText = quietnessGo.GetComponent<TextMeshProUGUI>();
            quietnessText.rectTransform.anchoredPosition += new Vector2(0, -18);
            quietnessText.rectTransform.sizeDelta = new Vector2(64, 24);
            if (source != null && source.tooltipStyle != null)
            {
                go.SetActive(false);
                go.layer = source.gameObject.layer;
                text.raycastTarget = true;
                var trigger = go.AddComponent<TooltipTrigger>();
                trigger.tooltipStyle = source.tooltipStyle;
                trigger.tooltipType = source.tooltipType;
                trigger.tipPosition = source.tipPosition;
                trigger.backgroundTint = source.backgroundTint;
                trigger.minTextWidth = source.minTextWidth;
                trigger.maxTextWidth = 650;
                trigger.neverRotate = source.neverRotate;
                trigger.disablePendingTooltipsOnMouseExit = true;
                trigger.parameterizedTextFields = new List<ParameterizedTextField>();
                trigger.dynamicImageFields = new List<DynamicImageField>();
                trigger.dynamicSectionFields = new List<DynamicSectionField>();
                trigger.SetDelegate("BodyText", AlienHateHistory.Tooltip);
                go.SetActive(true);
            }
            UpdateValue();
        }

        internal void UpdateValue()
        {
            if (cleaned) return;
            if (!Main.Enabled || !GameControl.loadcycle100 || GameControl.control == null
                || GameControl.control.activePlayer == null) { Clear(); return; }
            if (text == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 2f;
            var player = GameControl.control == null ? null : GameControl.control.activePlayer;
            var alien = GameStateManager.AlienFaction();
            if (player == null || alien == null)
            {
                Clear();
                return;
            }
            // This is the raw alien faction hate table entry, not the player's
            // assessedAlienHateOfMe estimate used by the native meter.
            text.text = alien.GetFactionHate(player).ToString("0.##");
            floorText.text = "(>" + alien.MinimumFactionHate(player).ToString("0.##") + ")";
            // Global raw 0–1 value used by the alien AI, on the same refresh timer.
            quietnessText.text = "Q " + AIEvaluators.GetAlienQuietness().ToString("0.000");
        }

        internal void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            RemoveOwnedText();
            Destroy(this);
        }

        internal void Clear()
        {
            if (text != null) text.text = string.Empty;
            if (floorText != null) floorText.text = string.Empty;
            if (quietnessText != null) quietnessText.text = string.Empty;
            nextUpdate = 0;
        }

        private void RemoveOwnedText()
        {
            if (ownedText != null) { ownedText.SetActive(false); Destroy(ownedText); }
            if (ownedFloor != null) { ownedFloor.SetActive(false); Destroy(ownedFloor); }
            if (ownedQuietness != null) { ownedQuietness.SetActive(false); Destroy(ownedQuietness); }
            ownedText = ownedFloor = ownedQuietness = null;
        }

        private void OnDestroy() { cleaned = true; RemoveOwnedText(); }
    }
}
