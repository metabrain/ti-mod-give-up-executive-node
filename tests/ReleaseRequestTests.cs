using System;
using GiveUpNation;
using PavonisInteractive.TerraInvicta;

// These stand-ins exercise the production confirmation/action code without loading Unity.
// They do not claim to validate the game's ownership-change internals or rendered UI.
public enum ControlPointChangeCause { None }
namespace PavonisInteractive.TerraInvicta.Actions
{
    public class PlayerAction { public virtual void Execute() { } }
}
namespace PavonisInteractive.TerraInvicta
{
    public class Player
    {
        public bool Defer;
        public Actions.PlayerAction Action;
        public void StartAction(Actions.PlayerAction action) { Action = action; if (!Defer) action.Execute(); }
    }
    public class TIFactionState { public Player playerControl = new Player(); }
    public class TIControlPoint { public TIFactionState faction; public int positionInNation; }
    public class TINationState
    {
        public bool extant = true, alienNation;
        public TIControlPoint executiveControlPoint;
        public TIControlPoint otherPoint;
        public int Changes;
        public void ChangeControlPointOwner(int index, ControlPointChangeCause cause, TIFactionState owner)
        {
            if (index != executiveControlPoint.positionInNation || cause != ControlPointChangeCause.None || owner != null)
                throw new Exception("Unexpected ownership operation");
            Changes++;
            executiveControlPoint.faction = owner;
        }
    }
}
internal static class Tests
{
    private static int passed;
    private static void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
    private static void Test(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    private static (TINationState n, TIFactionState f, ReleaseRequest r) Fixture()
    {
        var f = new TIFactionState();
        var n = new TINationState { executiveControlPoint = new TIControlPoint { faction = f, positionInNation = 3 }, otherPoint = new TIControlPoint { faction = f } };
        return (n, f, ReleaseRequest.Create(n, f));
    }
    public static void Main()
    {
        Test("eligible owner; confirmation alone does not release", () => { var (n, f, r) = Fixture(); Check(r != null && r.IsCurrent(n, f) && n.Changes == 0); });
        Test("confirm releases exactly executive once", () => { var (n, f, r) = Fixture(); Check(r.Confirm(n, f)); Check(n.Changes == 1 && n.executiveControlPoint.faction == null && n.otherPoint.faction == f); Check(!r.Confirm(n, f) && n.Changes == 1); f.playerControl.Action.Execute(); Check(n.Changes == 1); });
        Test("cancel makes request unusable", () => { var (n, f, r) = Fixture(); r.Cancel(); Check(!r.Confirm(n, f) && n.Changes == 0); });
        Test("switching nation invalidates confirmation", () => { var (n, f, r) = Fixture(); Check(!r.Confirm(new TINationState(), f) && n.Changes == 0); Check(!r.Confirm(n, f)); });
        Test("switching player invalidates confirmation", () => { var (n, f, r) = Fixture(); Check(!r.Confirm(n, new TIFactionState()) && n.Changes == 0); });
        Test("losing executive while popup open", () => { var (n, f, r) = Fixture(); n.executiveControlPoint.faction = new TIFactionState(); Check(!r.Confirm(n, f) && n.Changes == 0); });
        Test("replacement executive invalidates snapshot", () => { var (n, f, r) = Fixture(); n.executiveControlPoint = new TIControlPoint { faction = f }; Check(!r.Confirm(n, f) && n.Changes == 0); });
        Test("unowned executive cannot be surrendered", () => { var (n, f, r) = Fixture(); n.executiveControlPoint.faction = null; Check(ReleaseRequest.Create(n, f) == null); });
        Test("owning other points is insufficient", () => { var (n, f, r) = Fixture(); n.executiveControlPoint.faction = new TIFactionState(); Check(!ReleaseRequest.Eligible(n, f)); });
        Test("nonexistent and alien nations rejected", () => { var (n, f, r) = Fixture(); n.extant = false; Check(!r.Confirm(n, f)); n.extant = true; n.alienNation = true; Check(ReleaseRequest.Create(n, f) == null && n.Changes == 0); });
        Test("null selection safely disabled", () => { var (n, f, r) = Fixture(); Check(!ReleaseRequest.Eligible(null, f) && !ReleaseRequest.Eligible(n, null)); n.executiveControlPoint = null; Check(!ReleaseRequest.Eligible(n, f)); });
        Test("action execution rechecks ownership", () => { var (n, f, r) = Fixture(); f.playerControl.Defer = true; Check(r.Confirm(n, f)); n.executiveControlPoint.faction = null; f.playerControl.Action.Execute(); Check(n.Changes == 0); });
        Test("action execution rechecks executive identity", () => { var (n, f, r) = Fixture(); f.playerControl.Defer = true; Check(r.Confirm(n, f)); n.executiveControlPoint = new TIControlPoint { faction = f }; f.playerControl.Action.Execute(); Check(n.Changes == 0); });
        Console.WriteLine($"{passed} tests passed (offline stand-ins; no game execution).");
    }
}
