using System;
using System.Collections.Generic;
using System.Linq;
using GiveUpNation;
using PavonisInteractive.TerraInvicta;
static class DelayTests
{
    static int n;
    static void Check(bool b){if(!b)throw new Exception("Delay check "+(n+1));n++;}
    static void Throws(Action a){bool b=false;try{a();}catch(InvalidOperationException){b=true;}Check(b);}
    static TISpaceFleetState Fleet(params float[] fuel)
    {
        var f=new TISpaceFleetState{dockedAtHab=true};var owner=new object();var h=new Hab{faction=owner};
        foreach(float v in fuel)f.ships.Add(new TISpaceShipState{ref_fleet=f,ref_hab=h,faction=owner,propellant_tons=v});
        TITimeState.Current=0;ScuttleShipsOperation.Calls=ScuttleShipsOperation.FailAt=0;return f;
    }
    static bool Schedule(TISpaceFleetState f)=>ScuttleDelay.Schedule(f,f.ships.ToList());
    public static void Run()
    {
        Check(Math.Abs(ScuttleDelay.TransferDays(2500,.005f,2)*24-1.5)<.00001);
        Check(ScuttleDelay.TransferDays(1250,.005f,2)==ScuttleDelay.TransferDays(2500,.005f,2)/2);
        Check(ScuttleDelay.TransferDays(100,.005f,4)==ScuttleDelay.TransferDays(100,.005f,2)/2);
        Check(ScuttleDelay.TransferDays(0,.005f,2)==0);Check(ScuttleDelay.TransferDays(1,0,2)>0);
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,-1f})Throws(()=>ScuttleDelay.TransferDays(bad,.005f,2));
        Throws(()=>ScuttleDelay.TransferDays(100,-1,2));Throws(()=>ScuttleDelay.TransferDays(100,.005f,0));
        var f=Fleet(2500,1250);var a=f.ships[0];var b=f.ships[1];
        Check(!Schedule(f)&&f.currentOperations.Count==2&&f.Payments==0&&f.ships.Count==2);
        Check(Math.Abs(f.currentOperations[0].completionDate.Days-.0625)<.00001);
        Check(Math.Abs(f.currentOperations[1].completionDate.Days-.09375)<.00001);
        Check(!Schedule(f)&&f.currentOperations.Count==2);
        ScuttleDelay.Complete(f,a);Check(f.Payments==0);
        TITimeState.Current=.063;ScuttleDelay.Complete(f,a);Check(f.Payments==1&&a.deleted&&!b.deleted);
        ScuttleDelay.Complete(f,a);Check(f.Payments==1);
        f.CancelOperation(f.currentOperations[1]);TITimeState.Current=1;ScuttleDelay.Complete(f,b);
        Check(f.Payments==1&&!b.deleted&&b.propellant_tons==1250);
        f=Fleet(100);a=f.ships[0];a.OfficerModifier=-.0025f;Check(Math.Abs(ScuttleDelay.Duration(a)-.00125)<.00001);
        a.propellant_tons=float.NaN;Throws(()=>Schedule(f));Check(f.currentOperations.Count==0&&!a.deleted);
        f=Fleet(0);Check(Schedule(f)&&f.currentOperations.Count==0);
        f=Fleet(100);f.dockedAtHab=false;Check(Schedule(f)&&f.currentOperations.Count==0);
        f=Fleet(100);f.ships[0].ref_hab.Shipyard=false;Check(Schedule(f));
        f=Fleet(100);f.ships[0].ref_hab.faction=new object();Check(Schedule(f));
        f=Fleet(100);var stranger=Fleet(100).ships[0];Check(!ScuttleDelay.Schedule(f,new List<TISpaceShipState>{stranger})&&f.currentOperations.Count==0);
        f=Fleet(100);a=f.ships[0];Check(!ScuttleDelay.Schedule(f,new List<TISpaceShipState>{a,a})&&f.currentOperations.Count==1);
        f=Fleet(100,100);ScuttleShipsOperation.FailAt=2;Throws(()=>Schedule(f));Check(f.currentOperations.Count==0&&f.Payments==0);
        for(int change=0;change<5;change++)
        {
            f=Fleet(100);a=f.ships[0];Schedule(f);TITimeState.Current=1;
            if(change==0)f.dockedAtHab=false;if(change==1)a.ref_hab.faction=new object();
            if(change==2)a.ref_fleet=new TISpaceFleetState();if(change==3)f.inCombatOrWaitingForCombat=true;if(change==4)a.ref_hab.Shipyard=false;
            ScuttleDelay.Complete(f,a);Check(f.Payments==0&&!a.deleted);
        }
        Console.WriteLine(n+" scuttle delay checks passed (production adapter with game stand-ins).");
    }
}
// Models native operation/time behavior; not a Unity or native save/load test.
public class OperationData {public ScuttleShipsOperation operation;public TIGameState target;public TIDateTime completionDate;}
public class ScuttleShipsOperation
{
    public static int Calls,FailAt;
    public bool ActorCanPerformOperation(TISpaceFleetState f,TISpaceFleetState target)=>f.currentOperations.Count==0&&!f.inCombatOrWaitingForCombat;
    public bool OperationConfirmed(TISpaceFleetState f,TISpaceShipState s,TIDateTime date)
    {
        f.currentOperations.Add(new OperationData{operation=this,target=s,completionDate=date});
        if(++Calls==FailAt)throw new InvalidOperationException("simulated event creation failure");return true;
    }
}
public static class OperationsManager {public static Dictionary<Type,object> operationsLookup=new Dictionary<Type,object>{{typeof(ScuttleShipsOperation),new ScuttleShipsOperation()}};}
public static class TemplateManager {public static Globals global=new Globals();public class Globals{public float daysToRefuelAPropellantTank=.005f;}}
namespace PavonisInteractive.TerraInvicta
{
    public enum OfficerEffectType{DockResupplySpeed}
    public class TIGameState{public bool deleted;}
    public class TIDateTime
    {
        public double Days;public TIDateTime(double d){Days=d;}public TIDateTime(TIDateTime t){Days=t.Days;}
        public void AddDays(float d){Days+=d;}
        public static bool operator <=(TIDateTime a,TIDateTime b)=>a.Days<=b.Days;
        public static bool operator >=(TIDateTime a,TIDateTime b)=>a.Days>=b.Days;
    }
    public static class TITimeState {public static double Current;public static TIDateTime Now()=>new TIDateTime(Current);}
    public class TISpaceFleetState:TIGameState
    {
        public bool dockedAtHab,inCombatOrWaitingForCombat,underBombardment;public int Payments;
        public List<TISpaceShipState> ships=new List<TISpaceShipState>();public List<OperationData> currentOperations=new List<OperationData>();
        public List<OperationData> CurrentOperations()=>currentOperations.ToList();
        public void CancelOperation(OperationData o){currentOperations.Remove(o);}
        public void ScuttleShips(List<TISpaceShipState> ss){foreach(var s in ss){Payments++;s.deleted=true;ships.Remove(s);}}
    }
}
