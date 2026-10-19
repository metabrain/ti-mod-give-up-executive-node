using PavonisInteractive.TerraInvicta;
using UnityEngine;

namespace GiveUpNation
{
    internal static class IntelUiCache
    {
        private static IntelScreenController controller;

        internal static void Reset() { controller = null; }

        internal static IntelScreenController OpenController()
        {
            var current = FindController();
            return current != null && current.filterProspected != null
                && current.filterProspected.gameObject.activeInHierarchy ? current : null;
        }

        internal static IntelScreenController FindController()
        {
            if (!GameControl.loadcycle100 || GameControl.control == null
                || GameControl.control.activePlayer == null) return null;
            // Discover only while the cached Unity object is missing/destroyed.
            if (controller == null) controller = Object.FindObjectOfType<IntelScreenController>();
            return controller;
        }
    }
}
