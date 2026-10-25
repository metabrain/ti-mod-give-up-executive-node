namespace GiveUpNation
{
    internal static class TreatyNeeds
    {
        internal static bool Nap(string assessment) => assessment == "Losing" || assessment == "LosingBig";
        internal static bool Truce(string assessment, int enemies, bool strongest) =>
            Nap(assessment) || (assessment == "Even" && enemies > 2 && strongest);
        internal static bool Intel(string assessment, bool noSpies, bool war, bool hunting,
            bool close, bool otherNoStronger, bool otherWeaker)
        {
            if (assessment == "LosingBig") return true;
            if (assessment == "Losing") return (noSpies && (war || hunting)) || (close && otherNoStronger);
            if (assessment == "Even") return (noSpies && hunting) || (close && otherNoStronger);
            return close && otherWeaker;
        }
    }
}
