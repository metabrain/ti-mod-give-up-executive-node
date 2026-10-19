using System;
using System.Collections.Generic;
using System.Linq;
using ModelShark;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace GiveUpNation
{
    public sealed class TradeAcceptanceUi : MonoBehaviour
    {
        private GameObject panel, leaderHover;
        private TMP_Text benefits, result;
        private RectTransform restrictionsViewport;
        private ScrollRect restrictionsScroll;
        private readonly List<TooltipTrigger> tooltips = new List<TooltipTrigger>();
        private string detail;
        private RectTransform list;
        private Vector2 originalSize, originalPosition;
        private bool disposed;
        internal void Initialize(DiplomacyController c)
        {
            var feedback = c.aiFeedbackDialogText;
            var councilor = feedback.transform.parent.parent as RectTransform;
            var scroll = c.aiTableItemsContent.GetComponentInParent<ScrollRect>(true);
            var source = c.GetComponentsInChildren<TooltipTrigger>(true).FirstOrDefault(x => x.tooltipStyle != null);
            if (councilor == null || scroll == null || source == null) throw new InvalidOperationException("Trade screen layout or tooltip style unavailable.");
            var listRect = (RectTransform)scroll.transform;
            if (listRect.parent != councilor.parent || listRect.rect.height < 200) throw new InvalidOperationException("Unexpected trade offer list layout.");
            list = listRect;
            originalSize = list.sizeDelta; originalPosition = list.anchoredPosition;
            list.sizeDelta -= new Vector2(0, 120);
            list.anchoredPosition -= new Vector2(0, 120 * (1 - list.pivot.y));
            panel = new GameObject("Local.GiveUpNation.TradeBreakdown", typeof(RectTransform));
            panel.SetActive(false); panel.layer = feedback.gameObject.layer;
            var rect = (RectTransform)panel.transform;
            rect.SetParent(councilor, false);
            rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -4); rect.sizeDelta = new Vector2(-10, 112);
            benefits = Label(feedback, "Benefits", 0, 38);
            var viewport = new GameObject("Restrictions", typeof(RectTransform));
            viewport.layer = feedback.gameObject.layer;
            restrictionsViewport = (RectTransform)viewport.transform;
            restrictionsViewport.SetParent(panel.transform, false);
            restrictionsViewport.anchorMin = new Vector2(0, 1); restrictionsViewport.anchorMax = Vector2.one;
            restrictionsViewport.pivot = new Vector2(.5f, 1);
            restrictionsViewport.anchoredPosition = new Vector2(0, -38);
            restrictionsViewport.sizeDelta = new Vector2(0, 72);
            viewport.AddComponent<RectMask2D>();
            var hit = viewport.AddComponent<Image>(); hit.color = Color.clear;
            hit.canvasRenderer.cullTransparentMesh = false;
            restrictionsScroll = viewport.AddComponent<ScrollRect>();
            restrictionsScroll.viewport = restrictionsViewport;
            restrictionsScroll.horizontal = false; restrictionsScroll.vertical = true;
            restrictionsScroll.movementType = ScrollRect.MovementType.Clamped;
            restrictionsScroll.scrollSensitivity = 25;
            result = Label(feedback, "Result", 0, 72);
            result.rectTransform.SetParent(restrictionsViewport, false);
            result.enableAutoSizing = false; result.fontSize = 15;
            restrictionsScroll.content = result.rectTransform;
            AddHover(panel, source);
            // Owned child surface also covers native leader dialogue without changing its components.
            leaderHover = new GameObject("Local.GiveUpNation.TradeLeaderHover", typeof(RectTransform));
            leaderHover.SetActive(false); leaderHover.layer = feedback.gameObject.layer;
            var hoverRect = (RectTransform)leaderHover.transform;
            hoverRect.SetParent(feedback.transform, false);
            hoverRect.anchorMin = Vector2.zero; hoverRect.anchorMax = Vector2.one;
            hoverRect.offsetMin = hoverRect.offsetMax = Vector2.zero;
            AddHover(leaderHover, source);
        }
        private void AddHover(GameObject target, TooltipTrigger source)
        {
            var graphic = target.AddComponent<Image>();
            graphic.color = Color.clear; graphic.raycastTarget = true;
            graphic.canvasRenderer.cullTransparentMesh = false;
            AddTooltip(target, source);
        }
        private TooltipTrigger AddTooltip(GameObject target, TooltipTrigger source)
        {
            var tooltip = target.AddComponent<TooltipTrigger>();
            tooltip.tooltipStyle = source.tooltipStyle; tooltip.tooltipType = source.tooltipType;
            tooltip.tipPosition = source.tipPosition; tooltip.backgroundTint = source.backgroundTint;
            tooltip.minTextWidth = source.minTextWidth; tooltip.maxTextWidth = 600;
            tooltip.neverRotate = source.neverRotate; tooltip.disablePendingTooltipsOnMouseExit = true;
            tooltip.parameterizedTextFields = new List<ParameterizedTextField>();
            tooltip.dynamicImageFields = new List<DynamicImageField>();
            tooltip.dynamicSectionFields = new List<DynamicSectionField>();
            tooltip.SetDelegate("BodyText", () => detail ?? string.Empty);
            tooltips.Add(tooltip);
            return tooltip;
        }
        private TMP_Text Label(TMP_Text source, string name, float top, float height)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = source.gameObject.layer;
            var rect = (RectTransform)go.transform; rect.SetParent(panel.transform, false);
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -top); rect.sizeDelta = new Vector2(0, height);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = source.font; text.fontSharedMaterial = source.fontSharedMaterial;
            text.color = source.color; text.fontSize = 15; text.fontSizeMin = 12; text.fontSizeMax = 15;
            text.enableAutoSizing = true; text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
            return text;
        }
        internal void Show(string values, string outcome, string explanation)
        {
            benefits.text = values;
            bool changed = result.text != outcome;
            result.text = outcome; detail = explanation;
            float width = Mathf.Max(100, ((RectTransform)panel.transform).rect.width);
            float contentHeight = string.IsNullOrEmpty(outcome) ? 0 : Mathf.Max(1, result.GetPreferredValues(outcome, width, 0).y);
            float visibleHeight = Mathf.Clamp(contentHeight, 0, 140);
            restrictionsViewport.gameObject.SetActive(contentHeight > 0);
            result.rectTransform.sizeDelta = new Vector2(0, contentHeight);
            restrictionsViewport.sizeDelta = new Vector2(0, visibleHeight);
            float panelHeight = 38 + visibleHeight + 2;
            ((RectTransform)panel.transform).sizeDelta = new Vector2(-10, panelHeight);
            float reserved = panelHeight + 8;
            list.sizeDelta = originalSize - new Vector2(0, reserved);
            list.anchoredPosition = originalPosition - new Vector2(0, reserved * (1 - list.pivot.y));
            if (changed) restrictionsScroll.verticalNormalizedPosition = 1;
            panel.SetActive(true);
            leaderHover.SetActive(true);
            foreach (var tooltip in tooltips) tooltip.ForceRefreshTooltipIfOpen();
        }
        internal void Cleanup()
        {
            if (disposed) return;
            disposed = true;
            if (list != null) { list.sizeDelta = originalSize; list.anchoredPosition = originalPosition; }
            if (panel != null) { panel.SetActive(false); Destroy(panel); }
            if (leaderHover != null) { leaderHover.SetActive(false); Destroy(leaderHover); }
            TradeAcceptanceRuntime.Forget(this); Destroy(this);
        }
        private void OnDestroy() { Cleanup(); }
    }
}
