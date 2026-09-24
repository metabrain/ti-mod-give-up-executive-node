using System;
using System.Collections.Generic;
using ModelShark;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GiveUpNation
{
    public sealed class GiveUpNationUi : MonoBehaviour
    {
        private NationInfoController controller;
        private Button button;
        private GameObject dialog;
        private TMP_Text body;
        private ReleaseRequest pending;
        private readonly List<RectState> originalRects = new List<RectState>();
        private bool ready;
        private bool disposed;

        internal void Initialize(NationInfoController owner)
        {
            controller = owner;
            var original = owner.disableControlPointsButton;
            var originalRect = (RectTransform)original.transform;
            var parent = originalRect.parent;
            var policyContainer = parent.Find("Policy Container") as RectTransform;
            if (policyContainer == null || parent.GetComponent<LayoutGroup>() != null)
                throw new InvalidOperationException("Unsupported Policies tab layout; no UI changes applied.");

            // The inspected prefab reserves 43 px below the policy list. Reserve one
            // additional 35 px row, leaving the existing auto-renew checkbox intact.
            originalRects.Add(new RectState(originalRect));
            originalRects.Add(new RectState(policyContainer));
            var clone = Instantiate(original.gameObject, parent, false);
            button = clone.GetComponent<Button>();
            clone.SetActive(false);
            clone.name = "Local.GiveUpNation.Button";
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => Guard(Open));
            foreach (Transform child in clone.transform)
                if (child.name.StartsWith("Dummy_UITutorial_", StringComparison.Ordinal))
                { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var label = Counterpart(owner.disableControlPointsButtonText, original.transform, clone.transform);
            label.text = "Give up Nation";
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = owner.disableControlPointsButtonText.fontSize;
            foreach (var tip in clone.GetComponentsInChildren<TooltipTrigger>(true))
                tip.SetText("BodyText", "Release your executive control point in this nation. Your other control points remain yours. Requires ownership of the executive control point.");

            PlaceButton(originalRect, 0f, 0.5f, 5f, -3f);
            PlaceButton((RectTransform)clone.transform, 0.5f, 1f, 3f, -5f);
            policyContainer.offsetMin += new Vector2(0, 35);

            var source = owner.confirmDisableControlPointPanel;
            dialog = Instantiate(source, source.transform.parent, false);
            dialog.SetActive(false);
            dialog.name = "Local.GiveUpNation.Confirmation";
            // Replace the entire event, including serialized persistent callbacks.
            foreach (var b in dialog.GetComponentsInChildren<Button>(true))
                b.onClick = new Button.ButtonClickedEvent();
            Counterpart(owner.confirmDisableControlPointHeaderText, source.transform, dialog.transform).text = "Give up Nation";
            body = Counterpart(owner.confirmDisableControlPointBodyText, source.transform, dialog.transform);
            var confirmText = Counterpart(owner.confirmDisableControlConfirmButtonText, source.transform, dialog.transform);
            var cancelText = Counterpart(owner.confirmDisableControlCancelButtonText, source.transform, dialog.transform);
            confirmText.text = "Confirm";
            cancelText.text = "Cancel";
            // The dialog deliberately stays inactive during setup. Unity's default
            // parent lookup skips inactive ancestors, including both button objects.
            var confirmButton = confirmText.GetComponentInParent<Button>(true);
            var cancelButton = cancelText.GetComponentInParent<Button>(true);
            if (confirmButton == null || cancelButton == null)
                throw new InvalidOperationException("Confirmation dialog is missing its confirm/cancel buttons.");
            confirmButton.onClick.AddListener(() => Guard(Confirm));
            cancelButton.onClick.AddListener(() => Guard(Cancel));
            ready = true;
            clone.SetActive(true);
            RefreshState();
        }

        private static void PlaceButton(RectTransform rect, float left, float right, float leftInset, float rightInset)
        {
            rect.anchorMin = new Vector2(left, 0);
            rect.anchorMax = new Vector2(right, 0);
            rect.offsetMin = new Vector2(leftInset, 40);
            rect.offsetMax = new Vector2(rightInset, 70);
        }

        // Map references by hierarchy indices rather than translated text or duplicate names.
        private static T Counterpart<T>(T source, Transform sourceRoot, Transform cloneRoot) where T : Component
        {
            var indices = new Stack<int>();
            var current = source.transform;
            while (current != sourceRoot)
            {
                if (current == null) throw new InvalidOperationException("UI reference is outside its expected root.");
                indices.Push(current.GetSiblingIndex());
                current = current.parent;
            }
            while (indices.Count > 0) cloneRoot = cloneRoot.GetChild(indices.Pop());
            var result = cloneRoot.GetComponent<T>();
            if (result == null) throw new InvalidOperationException("Cloned UI reference is missing.");
            return result;
        }

        private void LateUpdate() { if (ready && !disposed) Guard(RefreshState); }

        private bool PanelVisible()
        {
            return controller != null && controller.nationPanelCanvas != null
                && controller.nationPanelCanvas.enabled
                && controller.nationPanelCanvas.gameObject.activeInHierarchy
                && controller.policiesTabController.IsSelected;
        }

        private void RefreshState()
        {
            var eligible = Main.Enabled && ReleaseRequest.Eligible(controller.nation, controller.activePlayer);
            button.interactable = eligible;
            if (pending != null && (!PanelVisible() || !eligible
                || !pending.IsCurrent(controller.nation, controller.activePlayer))) Cancel();
        }

        private void Open()
        {
            if (!Main.Enabled || !PanelVisible()) return;
            Cancel();
            var request = ReleaseRequest.Create(controller.nation, controller.activePlayer);
            if (request == null) return;
            controller.CloseAnySecondaryPanels(dialog, false);
            pending = request;
            // Disable rich text interpretation for a potentially renamed nation.
            body.richText = false;
            body.text = "Give up the executive control point in " + request.Nation.displayName + "?\n\n"
                + "You will lose executive control of this nation. The executive control point will become unowned.\n\n"
                + "Your other control points will remain yours. This is not temporary abandonment; you must regain the executive through normal gameplay.";
            dialog.transform.SetAsLastSibling();
            dialog.SetActive(true);
        }

        private void Confirm()
        {
            var request = pending;
            pending = null;
            dialog.SetActive(false);
            if (request == null) return;
            if (!Main.Enabled || !PanelVisible()) { request.Cancel(); return; }
            request.Confirm(controller.nation, controller.activePlayer);
            RefreshState();
        }

        private void Cancel()
        {
            pending?.Cancel();
            pending = null;
            if (dialog != null) dialog.SetActive(false);
        }

        internal bool CloseUnless(GameObject exceptPanel)
        {
            if (dialog == null || dialog == exceptPanel || !dialog.activeSelf) return false;
            Cancel();
            return true;
        }

        private void Guard(Action action)
        {
            if (disposed) return;
            try { action(); }
            catch (Exception exception) { Main.Log(exception); Cleanup(); }
        }

        internal void Cleanup()
        {
            if (disposed) return;
            disposed = true;
            ready = false;
            Cancel();
            if (button != null) { button.gameObject.SetActive(false); Destroy(button.gameObject); }
            if (dialog != null) Destroy(dialog);
            foreach (var rect in originalRects) rect.Restore();
            originalRects.Clear();
            Destroy(this);
        }

        private void OnDestroy() { Cleanup(); }

        private sealed class RectState
        {
            private readonly RectTransform rect;
            private readonly Vector2 min, max, position, size, pivot;
            internal RectState(RectTransform rect)
            {
                this.rect = rect;
                min = rect.anchorMin; max = rect.anchorMax; position = rect.anchoredPosition;
                size = rect.sizeDelta; pivot = rect.pivot;
            }
            internal void Restore()
            {
                if (rect == null) return;
                rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
                rect.sizeDelta = size; rect.anchoredPosition = position;
            }
        }
    }
}
