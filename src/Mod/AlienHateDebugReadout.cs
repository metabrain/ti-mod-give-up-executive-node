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
        private GameObject ownedText, ownedFloor;
        private bool cleaned;

        internal static AlienHateDebugReadout Attach(GeneralControlsController controller)
        {
            if (controller == null || controller.alienThreatPanel == null) return null;
            var parent = controller.alienThreatPanel.transform.parent;
            if (parent == null) return null;
            var existing = parent.GetComponent<AlienHateDebugReadout>();
            if (existing != null) return existing.cleaned ? null : existing;
            var readout = parent.gameObject.AddComponent<AlienHateDebugReadout>();
            try { readout.Initialize(controller.alienThreatPanel.transform); }
            catch { readout.Cleanup(); throw; }
            return readout;
        }

        private void Initialize(Transform panel)
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
            if (cleaned) return;
            cleaned = true;
            RemoveOwnedText();
            Destroy(this);
        }

        internal void Clear()
        {
            if (text != null) text.text = string.Empty;
            if (floorText != null) floorText.text = string.Empty;
            nextUpdate = 0;
        }

        private void RemoveOwnedText()
        {
            if (ownedText != null) { ownedText.SetActive(false); Destroy(ownedText); }
            if (ownedFloor != null) { ownedFloor.SetActive(false); Destroy(ownedFloor); }
            ownedText = ownedFloor = null;
        }

        private void OnDestroy() { cleaned = true; RemoveOwnedText(); }
    }
}
