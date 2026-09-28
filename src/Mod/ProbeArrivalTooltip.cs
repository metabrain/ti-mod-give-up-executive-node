using System;
using System.Collections.Generic;
using ModelShark;
using PavonisInteractive.TerraInvicta;
using UnityEngine;
using UnityEngine.UI;

namespace GiveUpNation
{
    // A separate hover surface on the status icon leaves the launch/replacement
    // button's TooltipTrigger, dynamic delegates, and click action untouched.
    public sealed class ProbeArrivalTooltip : MonoBehaviour
    {
        private IntelSpaceBodyListItemController row;
        private GameObject hoverSurface;
        private TooltipTrigger trigger;
        private string currentText;
        private float nextCheck;
        private bool failed;
        private bool disposed;

        internal static void Attach(IntelSpaceBodyListItemController item)
        {
            if (!Main.Enabled || item == null) return;
            ProbeArrivalTooltip component = null;
            try
            {
                component = item.GetComponent<ProbeArrivalTooltip>();
                if (component == null)
                {
                    component = item.gameObject.AddComponent<ProbeArrivalTooltip>();
                    component.row = item;
                }
                component.RefreshSafely();
            }
            catch (Exception exception)
            {
                if (component != null) component.Fail(exception);
                else Main.Log(exception);
            }
        }

        private void CreateSurface()
        {
            var source = row.prospectTooltip;
            if (source == null || source.tooltipStyle == null)
                throw new InvalidOperationException("Probe row has no native tooltip style.");
            hoverSurface = new GameObject("Local.GiveUpNation.ProbeArrival", typeof(RectTransform));
            hoverSurface.SetActive(false);
            hoverSurface.layer = row.prospectedIcon.gameObject.layer;
            var rect = (RectTransform)hoverSurface.transform;
            rect.SetParent(row.prospectedIcon.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var hitTarget = hoverSurface.AddComponent<Image>();
            hitTarget.color = Color.clear;
            hitTarget.raycastTarget = true;
            // Keep the transparent graphic in raycasts without changing the icon.
            hitTarget.canvasRenderer.cullTransparentMesh = false;

            trigger = hoverSurface.AddComponent<TooltipTrigger>();
            trigger.tooltipStyle = source.tooltipStyle;
            trigger.tooltipType = source.tooltipType;
            trigger.tipPosition = source.tipPosition;
            trigger.backgroundTint = source.backgroundTint;
            trigger.minTextWidth = source.minTextWidth;
            trigger.maxTextWidth = source.maxTextWidth;
            trigger.neverRotate = source.neverRotate;
            trigger.disablePendingTooltipsOnMouseExit = true;
            trigger.parameterizedTextFields = new List<ParameterizedTextField>();
            trigger.dynamicImageFields = new List<DynamicImageField>();
            trigger.dynamicSectionFields = new List<DynamicSectionField>();
            trigger.SetDelegate("BodyText", TextOnHover);
            // Start initializes the native tooltip after all configuration is set
            // and the inactive surface is first made visible.
        }

        private string ReadText()
        {
            if (!Main.Enabled || !GameControl.loadcycle100 || row == null || row.prospectedIcon == null
                || !row.prospectedIcon.isActiveAndEnabled || row.orderProspecting == null
                || row.orderProspecting.gameObject.activeSelf) return null;
            var control = GameControl.control;
            return control == null ? null : ProbeArrivalText.Build(control.activePlayer, row.spaceBody);
        }

        private string TextOnHover()
        {
            try { return ReadText() ?? string.Empty; }
            catch (Exception exception) { Fail(exception); return string.Empty; }
        }

        internal void RefreshSafely()
        {
            if (failed || disposed || row == null) return;
            try
            {
                var text = ReadText();
                if (text == null)
                {
                    currentText = null;
                    if (hoverSurface != null) hoverSurface.SetActive(false);
                    return;
                }
                if (hoverSurface == null) CreateSurface();
                var changed = currentText != text;
                currentText = text;
                hoverSurface.SetActive(true);
                if (changed) trigger.ForceRefreshTooltipIfOpen();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private void LateUpdate()
        {
            // Poll even when no hover surface exists: launching a probe or reusing
            // this row can make a previously ineligible icon eligible.
            if (failed || disposed || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            RefreshSafely();
        }

        private void OnDisable()
        {
            if (hoverSurface != null) hoverSurface.SetActive(false);
            currentText = null;
        }

        private void OnEnable() { RefreshSafely(); }

        private void Fail(Exception exception)
        {
            if (failed || disposed) return;
            failed = true;
            Main.Log(new InvalidOperationException("In-flight probe tooltip disabled for this row.", exception));
            if (hoverSurface != null) hoverSurface.SetActive(false);
        }

        internal void Cleanup()
        {
            if (disposed) return;
            disposed = true;
            if (hoverSurface != null)
            {
                hoverSurface.SetActive(false);
                Destroy(hoverSurface);
            }
            Destroy(this);
        }

        private void OnDestroy()
        {
            if (disposed) return;
            disposed = true;
            if (hoverSurface != null) Destroy(hoverSurface);
        }
    }

}
