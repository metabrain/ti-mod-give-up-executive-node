using System;
using System.Collections.Generic;
using System.Linq;
using GiveUpNation;
using PavonisInteractive.TerraInvicta;
class Tests
{
    static int n;
    static void Check(bool ok) { if (!ok) throw new Exception("Check " + (n+1)); n++; }
    static float Amount(TIResourcesCost c, FactionResource r) => c.resourceCosts.Where(x=>x.resource==r).Sum(x=>x.value);
    static void Main()
    {
        var faction = new object();
        var ship = new TISpaceShipState { faction=faction, propellant_tons=250, ref_fleet=new TISpaceFleetState { dockedAtHab=true }, ref_hab=new Hab { faction=faction }, template=new ShipTemplate() };
        var original = new TIResourcesCost(); original.AddCost(FactionResource.Metals,-25,true); original.AddCost(FactionResource.Water,-10,true);
        var result=ScuttleFuelRefund.Calculate(ship,original);
        Check(Amount(result,FactionResource.Water)==-260);
        Check(Amount(result,FactionResource.Metals)==-25);
        Check(Amount(original,FactionResource.Water)==-10);
        Check(ship.propellant_tons==250 && ship.ref_hab.Inactives && !ship.ref_hab.Construction);
        Check(Amount(ScuttleFuelRefund.Calculate(ship,original),FactionResource.Water)==-260);
        ship.propellant_tons=25;
        Check(Amount(ScuttleFuelRefund.Calculate(ship,original),FactionResource.Water)==-35);
        ship.template.Mix=new Dictionary<FactionResource,float>{{FactionResource.Water,80},{FactionResource.Fissiles,19},{FactionResource.Antimatter,1}};
        result=ScuttleFuelRefund.Calculate(ship,original);
        Check(Amount(result,FactionResource.Water)==-30 && Amount(result,FactionResource.Fissiles)==-4.75f && Amount(result,FactionResource.Antimatter)==-.25f);
        ship.template.Multiplier=.5f;
        Check(Amount(ScuttleFuelRefund.Calculate(ship,original),FactionResource.Fissiles)==-2.375f);
        foreach(float value in new[]{0f,-1f,float.NaN,float.PositiveInfinity}) {ship.propellant_tons=value;Check(ReferenceEquals(ScuttleFuelRefund.Calculate(ship,original),original));}
        ship.propellant_tons=25;ship.ref_fleet.dockedAtHab=false;
        Check(ReferenceEquals(ScuttleFuelRefund.Calculate(ship,original),original));
        ship.ref_fleet.dockedAtHab=true;ship.ref_hab.faction=new object();
        Check(ReferenceEquals(ScuttleFuelRefund.Calculate(ship,original),original));
        ship.ref_hab.faction=faction;ship.ref_hab.Shipyard=false;
        Check(ReferenceEquals(ScuttleFuelRefund.Calculate(ship,original),original));
        ship.ref_hab.Shipyard=true;
        Check(ReferenceEquals(ScuttleFuelRefund.Calculate(null,original),original));
        ship.template.Mix=new Dictionary<FactionResource,float>{{FactionResource.Water,float.NaN}};
        bool threw=false;try{ScuttleFuelRefund.Calculate(ship,original);}catch(InvalidOperationException){threw=true;}
        Check(threw && Amount(original,FactionResource.Water)==-10);
        ship.template.Mix=new Dictionary<FactionResource,float>{{FactionResource.Water,100}};ship.template.Multiplier=1;
        float sum=0;foreach(float tons in new[]{25f,50f,125f}){ship.propellant_tons=tons;sum+=Amount(ScuttleFuelRefund.Calculate(ship,original),FactionResource.Water);}
        Check(sum==-230); // 200 fuel plus three native construction refunds of 10.
        DelayTests.Run();
        Console.WriteLine(n+" scuttle refund checks passed (production adapter with game stand-ins).");
    }
}
namespace PavonisInteractive.TerraInvicta
{
    public enum FactionResource {Water,Metals,Fissiles,Antimatter}
    public struct ResourceValue {public FactionResource resource;public float value;}
    public class TIResourcesCost
    {
        public List<ResourceValue> resourceCosts=new List<ResourceValue>();
        public TIResourcesCost(){}
        public TIResourcesCost(TIResourcesCost other){resourceCosts=new List<ResourceValue>(other.resourceCosts);}
        public void AddCost(FactionResource r,float v,bool allowNegative){if(!allowNegative)throw new Exception("Must permit refunds");resourceCosts.Add(new ResourceValue{resource=r,value=v});}
    }

    public class Hab {public object faction;public bool Shipyard=true,Inactives,Construction,underBombardment;public int Speed=2;public int ResupplySpeedDivisor()=>Speed;public bool AllowsShipConstruction(object f,bool checkInactives,bool checkUnderConstruction){Inactives=checkInactives;Construction=checkUnderConstruction;return Shipyard;}}
    public class ShipTemplate
    {
        public Dictionary<FactionResource,float> Mix=new Dictionary<FactionResource,float>{{FactionResource.Water,100}};public float Multiplier=1;
        public TIResourcesCost singlePropellantTankCost(object faction,float fraction){var c=new TIResourcesCost();foreach(var x in Mix)c.AddCost(x.Key,x.Value*fraction*Multiplier,true);return c;}
    }
    public class TISpaceShipState : TIGameState {public TISpaceFleetState ref_fleet;public float OfficerModifier;public float SumOfficerEffectsModifiers(OfficerEffectType type,float value)=>OfficerModifier;public Hab ref_hab;public object faction;public ShipTemplate template;public float propellant_tons;}
}
