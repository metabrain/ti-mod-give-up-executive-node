using System;
using System.Collections.Generic;
using System.Linq;
using PavonisInteractive.TerraInvicta;

namespace GiveUpNation
{
    internal static class ScuttleDelay
    {
        internal static bool Eligible(TISpaceShipState ship)
        {
            return ship != null && !ship.deleted && ship.ref_fleet != null && ship.ref_fleet.dockedAtHab
                && ship.ref_hab != null && ship.faction != null && ship.ref_hab.faction == ship.faction
                && ship.ref_hab.AllowsShipConstruction(ship.faction, checkInactives: true, checkUnderConstruction: false);
        }

        internal static float Duration(TISpaceShipState ship)
        {
            if (!Eligible(ship)) return 0;
            if (float.IsNaN(ship.propellant_tons) || float.IsInfinity(ship.propellant_tons) || ship.propellant_tons < 0)
                throw new InvalidOperationException("Invalid ship propellant quantity.");
            if (ship.propellant_tons == 0) return 0;
            float rate = TemplateManager.global.daysToRefuelAPropellantTank;
            rate += ship.SumOfficerEffectsModifiers(OfficerEffectType.DockResupplySpeed, rate);
            return TransferDays(ship.propellant_tons, rate, ship.ref_hab.ResupplySpeedDivisor());
        }

        internal static float TransferDays(float tons, float daysPerTank, int divisor)
        {
            if (float.IsNaN(tons) || float.IsInfinity(tons) || float.IsNaN(daysPerTank)
                || float.IsInfinity(daysPerTank) || tons < 0 || daysPerTank < 0 || divisor < 1)
                throw new InvalidOperationException("Invalid scuttle unloading duration.");
            double days = (double)tons / 100 * daysPerTank / divisor;
            if (days > float.MaxValue) throw new InvalidOperationException("Scuttle unloading duration overflow.");
            return tons > 0 ? (float)Math.Max(1d / 86400, days) : 0;
        }

        // Return true only when native immediate scuttling should run. Never pay at scheduling time.
        internal static bool Schedule(TISpaceFleetState fleet, List<TISpaceShipState> selected)
        {
            if (fleet == null || fleet.deleted || selected == null) return false;
            var operation = (ScuttleShipsOperation)OperationsManager.operationsLookup[typeof(ScuttleShipsOperation)];
            if (!operation.ActorCanPerformOperation(fleet, fleet)) return false;
            var ships = selected.Where(s => s != null && !s.deleted && s.ref_fleet == fleet && fleet.ships.Contains(s)).Distinct().ToList();
            if (ships.Count == 0 || selected.Any(s => s == null || s.deleted || s.ref_fleet != fleet || !fleet.ships.Contains(s))) return false;
            // Calculate the complete plan before adding any operations. Invalid data must not scuttle instantly.
            var durations = ships.Select(Duration).ToArray();
            if (!durations.Any(d => d > 0)) return true;
            var added = new List<OperationData>();
            try
            {
                var finish = new TIDateTime(TITimeState.Now());
                for (int i = 0; i < ships.Count; i++)
                {
                    finish.AddDays(Math.Max(1f / 86400, durations[i]));
                    var before = fleet.CurrentOperations();
                    try
                    {
                        if (!operation.OperationConfirmed(fleet, ships[i], new TIDateTime(finish)))
                            throw new InvalidOperationException("Could not schedule scuttle operation.");
                    }
                    finally { added.AddRange(fleet.CurrentOperations().Except(before)); }
                }
            }
            catch
            {
                foreach (var pending in added) fleet.CancelOperation(pending);
                throw;
            }
            return false;
        }

        internal static void Complete(TIGameState actor, TIGameState target)
        {
            var fleet = actor as TISpaceFleetState;
            var ship = target as TISpaceShipState;
            if (fleet == null || fleet.deleted || !Eligible(ship) || ship.ref_fleet != fleet || !fleet.ships.Contains(ship)
                || fleet.inCombatOrWaitingForCombat || fleet.underBombardment || ship.ref_hab.underBombardment) return;
            // A canceled/stale event cannot pay. The native completion handler removes the record afterwards.
            if (!fleet.CurrentOperations().Any(o => o.operation is ScuttleShipsOperation && o.target == ship
                && o.completionDate <= TITimeState.Now())) return;
            fleet.ScuttleShips(new List<TISpaceShipState> { ship });
        }
    }
}
