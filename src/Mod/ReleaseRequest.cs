using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;

namespace GiveUpNation
{
    // Transient confirmation state only; nothing is added to campaign saves.
    internal sealed class ReleaseRequest
    {
        internal readonly TINationState Nation;
        internal readonly TIControlPoint Point;
        internal readonly TIFactionState Faction;
        private bool consumed;

        private ReleaseRequest(TINationState nation, TIFactionState faction)
        {
            Nation = nation;
            Point = nation.executiveControlPoint;
            Faction = faction;
        }

        internal static bool Eligible(TINationState nation, TIFactionState faction)
        {
            return nation != null && faction != null && nation.extant && !nation.alienNation
                && nation.executiveControlPoint != null
                && nation.executiveControlPoint.faction == faction;
        }

        internal static ReleaseRequest Create(TINationState nation, TIFactionState faction)
        {
            return Eligible(nation, faction) ? new ReleaseRequest(nation, faction) : null;
        }

        internal bool IsCurrent(TINationState nation, TIFactionState faction)
        {
            return !consumed && nation == Nation && faction == Faction
                && Eligible(Nation, Faction) && Nation.executiveControlPoint == Point;
        }

        internal void Cancel() { consumed = true; }

        internal bool Confirm(TINationState nation, TIFactionState faction)
        {
            if (!IsCurrent(nation, faction)) { Cancel(); return false; }
            consumed = true;
            faction.playerControl.StartAction(new ReleaseExecutive(Nation, Point, Faction));
            return true;
        }

        private sealed class ReleaseExecutive : PlayerAction
        {
            private readonly TINationState nation;
            private readonly TIControlPoint point;
            private readonly TIFactionState faction;
            private bool executed;

            internal ReleaseExecutive(TINationState nation, TIControlPoint point, TIFactionState faction)
            { this.nation = nation; this.point = point; this.faction = faction; }

            public override void Execute()
            {
                if (executed) return;
                executed = true;
                if (!Eligible(nation, faction) || nation.executiveControlPoint != point) return;
                // No existing cause represents voluntary surrender. None avoids falsely
                // reporting a coup/trade and avoids the alien-reward branches.
                nation.ChangeControlPointOwner(point.positionInNation, ControlPointChangeCause.None, null);
            }
        }
    }
}
