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
            if (!GameControl.loadcycle100 || GameControl.control == null
                || GameControl.control.activePlayer == null) return null;
            // Discover only while the cached Unity object is missing/destroyed.
            if (controller == null) controller = Object.FindObjectOfType<IntelScreenController>();
            return controller != null && controller.filterProspected != null
                && controller.filterProspected.gameObject.activeInHierarchy ? controller : null;
        }
    }
}
