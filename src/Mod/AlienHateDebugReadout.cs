using System;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;

namespace GiveUpNation
{
    internal sealed class AlienHateDebugReadout : MonoBehaviour
    {
        private TMP_Text text;
        private TMP_Text floorText;
        private float nextUpdate;

        internal static void Attach(GeneralControlsController controller)
        {
            if (controller == null || controller.alienThreatPanel == null) return;
            var parent = controller.alienThreatPanel.transform.parent;
            if (parent == null) return;
            var existing = parent.GetComponent<AlienHateDebugReadout>();
            if (existing != null) return;
            var readout = parent.gameObject.AddComponent<AlienHateDebugReadout>();
            readout.Initialize(controller.alienThreatPanel.transform);
        }

        private void Initialize(Transform panel)
        {
            var go = new GameObject("GiveUpNation.RawAlienHate", typeof(RectTransform), typeof(TextMeshProUGUI));
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
            floorGo.name = "GiveUpNation.RawAlienHateFloor";
            floorText = floorGo.GetComponent<TextMeshProUGUI>();
            floorText.fontSize = 12;
            floorText.rectTransform.anchoredPosition += new Vector2(0, -18);
            UpdateValue();
        }

        internal void UpdateValue()
        {
            if (text == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 2f;
            var player = GameControl.control == null ? null : GameControl.control.activePlayer;
            var alien = GameStateManager.AlienFaction();
            if (player == null || alien == null)
            {
                text.text = string.Empty;
                floorText.text = string.Empty;
                return;
            }
            // This is the raw alien faction hate table entry, not the player's
            // assessedAlienHateOfMe estimate used by the native meter.
            text.text = alien.GetFactionHate(player).ToString("0.##");
            floorText.text = "(>" + alien.MinimumFactionHate(player).ToString("0.##") + ")";
        }

        internal void Cleanup()
        {
            if (this == null) return;
            Destroy(gameObject);
        }
    }
}
