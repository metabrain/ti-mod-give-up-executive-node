using System;
using GiveUpNation;
class Tests
{
    static int count;
    static void Check(bool ok) { count++; if (!ok) throw new Exception("Failed check " + count); }
    static void Main()
    {
        Check(TradeAcceptanceText.Score(new float[] {100,-20,30}) == 70);
        Check(TradeAcceptanceText.Score(new float[] {-10,-20}) == -90);
        Check(TradeAcceptanceText.Score(new float[] {}) == 0);
        Check(TradeAcceptanceText.Share(0,0) == 0);
        Check(TradeAcceptanceText.Share(-1,10) == 0);
        Check(TradeAcceptanceText.Share(10,0) == 1);
        Check(TradeAcceptanceText.Share(10,-1) == 1);
        Check(Math.Abs(TradeAcceptanceText.Share(40,60) - .4f) < .0001);
        Check(TradeAcceptanceText.CountBlocker(1,0,2,0) == null);
        Check(TradeAcceptanceText.CountBlocker(2,0,0,0) != null);
        Check(TradeAcceptanceText.CountBlocker(2,1,0,0) != null);
        Check(TradeAcceptanceText.CountBlocker(2,2,2,0) == null);
        Check(TradeAcceptanceText.CountBlocker(0,0,3,0) != null);
        Check(TradeAcceptanceText.IncomeBlocker("Money",-40,100,false) == null);
        Check(TradeAcceptanceText.IncomeBlocker("Money",-41,100,false).Contains("59.0%"));
        Check(TradeAcceptanceText.IncomeBlocker("Money",-1,0,false) != null);
        Check(TradeAcceptanceText.IncomeBlocker("Money",-100,100,false) != null);
        Check(TradeAcceptanceText.IncomeBlocker("Money",0,-100,false) == null);
        Check(TradeAcceptanceText.IncomeBlocker("MC",-10,10,true) == null);
        Check(TradeAcceptanceText.IncomeBlocker("MC",-11,10,true) != null);
        Check(TradeAcceptanceText.Result(true,true,"blocked") == "");
        Check(TradeAcceptanceText.Result(false,true,"blocked").Contains("Bypassed"));
        Check(TradeAcceptanceText.Result(false,false,"blocked") == "blocked");
        Check(TradeAcceptanceText.Result(false,false,null) == "");
        Check(TradeAcceptanceText.Result(false,true,null) == "");
        Check(TradeAcceptanceText.Number(-2.34f) == "-2.3");
        Check(TradeAcceptanceText.Percent(.41f) == "41.0%");
        Check(TradeAcceptanceText.CountBlocker(3,1,0,0) == "Habs: AI gives 3; maximum 1");
        Check(TradeAcceptanceText.CountBlocker(0,0,5,1) == "Orgs: AI gives 5; maximum 3");
        Check(TradeAcceptanceText.IncomeBlocker("MC",-11,10,true).Contains("+10.0 -> -1.0; minimum 0"));
        Check(TradeAcceptanceText.IncomeBlocker("Money",-5,0,false).Contains("0.0 -> -5.0"));
        Check(TreatyNeeds.Nap("Losing") && TreatyNeeds.Nap("LosingBig"));
        Check(!TreatyNeeds.Nap("Even"));
        Check(!TreatyNeeds.Truce("Even", 2, true));
        Check(TreatyNeeds.Truce("Even", 3, true));
        Check(!TreatyNeeds.Truce("Even", 3, false));
        Check(TreatyNeeds.Intel("LosingBig", false, false, false, false, false, false));
        Check(TreatyNeeds.Intel("Losing", true, true, false, false, false, false));
        Check(!TreatyNeeds.Intel("Losing", false, true, false, false, false, false));
        Check(!TreatyNeeds.Intel("Even", true, true, false, false, false, false));
        Check(TreatyNeeds.Intel("Even", true, false, true, false, false, false));
        Check(TreatyNeeds.Intel("Even", false, false, false, true, true, false));
        Check(!TreatyNeeds.Intel("Winning", false, false, false, true, true, false));
        Check(TreatyNeeds.Intel("Winning", false, false, false, true, true, true));
        Console.WriteLine(count + " trade acceptance checks passed.");
    }
}
