using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GiveUpNation
{
    // Headers and cells share geometry; native sort buttons shrink with their labels.
    internal sealed class ProspectingTravelRow : MonoBehaviour
    {
        private TMP_Text travel;
        private ColumnState source, neighbor;
        private bool cleaned;

        internal void Initialize(TMP_Text text, TMP_Text preceding = null, Action clicked = null)
        {
            bool isHeader = clicked != null;
            var rect = isHeader ? text.transform.parent as RectTransform : text.rectTransform;
            var prior = preceding == null ? null
                : isHeader ? preceding.transform.parent as RectTransform : preceding.rectTransform;
            if (rect == null || rect.anchorMin != rect.anchorMax || rect.pivot != new Vector2(.5f, .5f)
                || rect.sizeDelta.x < 200 || (prior != null && (prior.parent != rect.parent || prior.sizeDelta.x < 160)))
                throw new InvalidOperationException("Unsupported travel column layout.");
            source = new ColumnState(text, rect);
            if (prior != null) neighbor = new ColumnState(preceding, prior);
            var originalPosition = rect.anchoredPosition;
            var originalSize = rect.sizeDelta;
            if (neighbor != null)
            {
                neighbor.Resize(80, 0);
                source.Resize(80, 80);
            }
            else source.Resize(160, 0);

            var go = new GameObject("GiveUpNation.TravelColumn", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(rect.parent, false);
            go.layer = text.gameObject.layer;
            travel = go.GetComponent<TextMeshProUGUI>();
            travel.font = text.font;
            travel.fontSize = 12;
            travel.color = new Color(.65f, .85f, 1f);
            travel.alignment = TextAlignmentOptions.Midline;
            travel.raycastTarget = isHeader;
            travel.textWrappingMode = TextWrappingModes.NoWrap;
            travel.overflowMode = TextOverflowModes.Ellipsis;
            var added = travel.rectTransform;
            added.anchorMin = rect.anchorMin;
            added.anchorMax = rect.anchorMax;
            added.pivot = rect.pivot;
            added.anchoredPosition = originalPosition + new Vector2((originalSize.x - 160) / 2, 0);
            added.sizeDelta = new Vector2(160, originalSize.y);
            if (isHeader)
            {
                var button = go.AddComponent<Button>();
                button.targetGraphic = travel;
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => clicked());
                travel.text = "Travel (days | km/s)";
            }
        }

        internal void SetText(string value)
        {
            if (!cleaned && travel != null) travel.text = value ?? string.Empty;
        }

        internal void Cleanup() { Restore(); Destroy(this); }

        private void Restore()
        {
            if (cleaned) return;
            cleaned = true;
            source?.Restore(); neighbor?.Restore();
            if (travel != null) { travel.gameObject.SetActive(false); Destroy(travel.gameObject); }
        }

        private void OnDestroy() { Restore(); }

        private sealed class ColumnState
        {
            private readonly TMP_Text text;
            private readonly RectTransform rect;
            private readonly Vector2 position, size;
            private readonly TextWrappingModes wrapping;
            private readonly TextOverflowModes overflow;
            internal ColumnState(TMP_Text text, RectTransform rect)
            {
                this.text = text; this.rect = rect;
                position = rect.anchoredPosition; size = rect.sizeDelta;
                wrapping = text.textWrappingMode; overflow = text.overflowMode;
            }
            internal void Resize(float shrink, float moveLeft)
            {
                rect.sizeDelta = size - new Vector2(shrink, 0);
                rect.anchoredPosition = position - new Vector2(moveLeft + shrink / 2, 0);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }
            internal void Restore()
            {
                if (rect != null) { rect.anchoredPosition = position; rect.sizeDelta = size; }
                if (text != null) { text.textWrappingMode = wrapping; text.overflowMode = overflow; }
            }
        }
    }
}
