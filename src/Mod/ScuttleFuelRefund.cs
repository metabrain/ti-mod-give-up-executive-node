using System;
using PavonisInteractive.TerraInvicta;
namespace GiveUpNation
{
    internal static class ScuttleFuelRefund
    {
        internal static TIResourcesCost Calculate(TISpaceShipState ship, TIResourcesCost original)
        {
            if (original == null || ship == null || ship.ref_fleet == null || !ship.ref_fleet.dockedAtHab
                || ship.ref_hab == null || ship.faction == null || ship.ref_hab.faction != ship.faction
                || ship.template == null || !(ship.propellant_tons > 0) || float.IsInfinity(ship.propellant_tons)) return original;
            // Match native ScuttleCost exactly: includes inactive shipyards, excludes construction.
            if (!ship.ref_hab.AllowsShipConstruction(ship.ref_hab.faction, checkInactives: true, checkUnderConstruction: false)) return original;
            // Native tanks hold 100 tons. The helper applies the drive's mixture and faction modifiers.
            var fuel = ship.template.singlePropellantTankCost(ship.faction, ship.propellant_tons / 100f);
            if (fuel?.resourceCosts == null || fuel.resourceCosts.Count == 0) return original;
            foreach (var resource in fuel.resourceCosts)
                if (float.IsNaN(resource.value) || float.IsInfinity(resource.value) || resource.value < 0)
                    throw new InvalidOperationException("Invalid native propellant cost; retaining original scuttle cost.");
            // Copy so repeated previews cannot compound the refund or alter cached native costs.
            var result = new TIResourcesCost(original);
            foreach (var resource in fuel.resourceCosts)
                result.AddCost(resource.resource, -resource.value, allowNegative: true);
            return result;
        }
    }
}
