using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The Empire (Royalty, a DLC: read through the game's own API, only with <c>ModsConfig.RoyaltyActive</c>): the royal
    /// title someone holds, a colonist's or a notable's, for the world ledger's person view. Their most senior title
    /// (<c>pawn.royalty.MostSeniorTitle</c>): its word for them (<c>RoyalTitleDef.GetLabelFor</c>, "baroness"), its
    /// def, the people who gave it and its seniority. The Empire keeps its own titles and honor; Ages marks it apart.
    /// </summary>
    public sealed class EmpireAdapter : WorldAdapter
    {
        public override string Name => "the Empire";

        public override bool Ready() => ModsConfig.RoyaltyActive;

        public override RoyalTitleView Title(Pawn p)
        {
            var t = p?.royalty?.MostSeniorTitle;
            if (t?.def == null) return null;
            return new RoyalTitleView { label = t.def.GetLabelFor(p), defName = t.def.defName, people = t.faction, seniority = t.def.seniority };
        }
    }
}
