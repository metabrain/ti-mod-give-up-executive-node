using System;
using System.Collections.Generic;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GiveUpNation
{
    internal sealed class ProspectingFiltersUi : MonoBehaviour
    {
        private const float RowHeight = 34f;
        private IntelScreenController controller;
        private Toggle unprospected, prospecting;
        private GameObject staging;
        private readonly List<RectState> layout = new List<RectState>();
        private readonly Dictionary<TISpaceBodyState, ProspectingStatus> statuses = new Dictionary<TISpaceBodyState, ProspectingStatus>();
        private TIFactionState player;
        private bool cleaned;
        internal bool Ready { get; private set; }
        internal bool HasAdditionalSelection => Ready && (unprospected.isOn || prospecting.isOn);

        internal void Initialize(IntelScreenController owner)
        {
            controller = owner;
            var original = controller.filterProspected;
            var originalRect = (RectTransform)original.transform;
            var upper = originalRect.parent as RectTransform;
            if (upper == null || controller.spacebodyListAdapter == null)
                throw new InvalidOperationException("Prospecting filters: missing native filter container or list adapter.");
            var lower = controller.spacebodyListAdapter.transform as RectTransform;
            while (lower != null && lower.parent != upper.parent) lower = lower.parent as RectTransform;
            if (upper == null || lower == null || lower == upper || upper.GetComponent<LayoutGroup>() != null
                || originalRect.anchorMin != new Vector2(0, .5f) || originalRect.anchorMax != originalRect.anchorMin
                || upper.anchorMin.y != 1 || upper.anchorMax.y != 1
                || lower.anchorMin.y != 0 || lower.anchorMax.y != 1
                || controller.filterProspectedText.transform != original.transform)
                throw new InvalidOperationException("Prospecting filters: unsupported native filter layout.");

            // Clones stay inactive until their persistent callbacks have been replaced.
            staging = new GameObject("GiveUpNation.ProspectingStaging", typeof(RectTransform));
            staging.SetActive(false);
            staging.transform.SetParent(upper, false);
            unprospected = Clone(original, "Unprospected", "Filter Unprospected");
            prospecting = Clone(original, "Prospecting", "Filter Prospecting");

            Remember(upper);
            Remember(lower);
            Remember(originalRect);
            // Mid-anchored controls would move down when the container grows.
            // Compensate to leave the search and dropdown row in its old position.
            foreach (Transform child in upper)
            {
                var rect = child as RectTransform;
                if (rect == null || rect == originalRect || rect.anchorMin.y != .5f || rect.anchorMax.y != .5f) continue;
                Remember(rect);
                rect.anchoredPosition += new Vector2(0, RowHeight / 2);
            }
            upper.sizeDelta += new Vector2(0, RowHeight);
            lower.offsetMax -= new Vector2(0, RowHeight);
            originalRect.anchorMin = originalRect.anchorMax = new Vector2(0, 1);
            originalRect.anchoredPosition = new Vector2(8, -RowHeight * 1.5f);
            unprospected.transform.SetParent(upper, false);
            prospecting.transform.SetParent(upper, false);
            PositionClones();
            unprospected.gameObject.SetActive(true);
            prospecting.gameObject.SetActive(true);
            Destroy(staging);
            staging = null;
            player = controller.activePlayer;
            Ready = true;
        }

        private Toggle Clone(Toggle original, string suffix, string label)
        {
            var clone = Instantiate(original, staging.transform, false);
            clone.gameObject.name = "GiveUpNation.Filter" + suffix;
            clone.gameObject.SetActive(false);
            clone.onValueChanged = new Toggle.ToggleEvent();
            clone.group = null;
            clone.SetIsOnWithoutNotify(false);
            clone.GetComponent<TMP_Text>().text = label;
            clone.onValueChanged.AddListener(OnChanged);
            return clone;
        }

        private void OnChanged(bool value)
        {
            if (!Ready || !Main.Enabled) return;
            try { controller.UpdateProspectedFilter(); }
            catch (Exception exception) { Main.Log(exception); }
        }

        internal bool Matches(TIFactionState faction, TISpaceBodyState body)
        {
            return ProspectingSelection.Matches(ProspectingState.GetStatus(faction, body), controller.filterProspected.isOn,
                unprospected.isOn, prospecting.isOn);
        }

        internal void Tick()
        {
            if (!Ready) return;
            PositionClones();
            if (!ReferenceEquals(player, controller.activePlayer))
            {
                player = controller.activePlayer;
                unprospected.SetIsOnWithoutNotify(false);
                prospecting.SetIsOnWithoutNotify(false);
                statuses.Clear();
                RefreshList();
            }
            if (player == null || (!controller.filterProspected.isOn && !HasAdditionalSelection))
            {
                statuses.Clear();
                return;
            }
            // Include hidden bodies so launches/completions can bring a row back.
            bool changed = statuses.Count != controller.spacebodyModels.Count;
            foreach (var model in controller.spacebodyModels)
            {
                var body = model.IntelScreenSpacebodyListItemData.spacebodyState;
                var status = ProspectingState.GetStatus(player, body);
                if (!statuses.TryGetValue(body, out var previous) || status != previous) changed = true;
                statuses[body] = status;
            }
            if (changed)
            {
                // Drop stale model references after a list rebuild.
                statuses.Clear();
                foreach (var model in controller.spacebodyModels)
                {
                    var body = model.IntelScreenSpacebodyListItemData.spacebodyState;
                    statuses[body] = ProspectingState.GetStatus(player, body);
                }
                RefreshList();
            }
        }

        private void RefreshList()
        {
            controller.UpdateSpaceBodiesListVisibility();
            controller.UpdateSpaceBodyListModelData();
        }

        private void PositionClones()
        {
            var original = (RectTransform)controller.filterProspected.transform;
            float x = original.anchoredPosition.x + Width(controller.filterProspected) + 12;
            foreach (var toggle in new[] { unprospected, prospecting })
            {
                var rect = (RectTransform)toggle.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(x, -RowHeight * 1.5f);
                x += Width(toggle) + 12;
            }
        }

        private static float Width(Toggle toggle)
        {
            return Math.Max(((RectTransform)toggle.transform).rect.width, toggle.GetComponent<TMP_Text>().preferredWidth);
        }

        private void Remember(RectTransform rect) { layout.Add(new RectState(rect)); }

        internal void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            bool wasReady = Ready;
            Ready = false;
            foreach (var toggle in new[] { unprospected, prospecting })
            {
                if (toggle == null) continue;
                toggle.onValueChanged.RemoveListener(OnChanged);
                toggle.gameObject.SetActive(false);
                Destroy(toggle.gameObject);
            }
            if (staging != null) Destroy(staging);
            foreach (var state in layout) state.Restore();
            layout.Clear();
            statuses.Clear();
            try
            {
                if (wasReady && controller != null && GameControl.loadcycle100 && controller.activePlayer != null)
                    RefreshList();
            }
            finally { Destroy(this); }
        }

        private void OnDestroy()
        {
            // Scene destruction needs no refresh of a disappearing adapter.
            Ready = false;
            Cleanup();
        }

        private sealed class RectState
        {
            private readonly RectTransform rect;
            private readonly Vector2 min, max, position, size;
            internal RectState(RectTransform rect)
            {
                this.rect = rect; min = rect.anchorMin; max = rect.anchorMax;
                position = rect.anchoredPosition; size = rect.sizeDelta;
            }
            internal void Restore()
            {
                if (rect == null) return;
                rect.anchorMin = min; rect.anchorMax = max;
                rect.anchoredPosition = position; rect.sizeDelta = size;
            }
        }
    }
}
