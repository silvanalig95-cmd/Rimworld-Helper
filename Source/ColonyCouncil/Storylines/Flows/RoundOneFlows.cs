namespace ColonyCouncil
{
    // The rules of round 1's three storylines (released 0.262.0), moved here from StorylineMath's switches unchanged; their
    // numbers stay in StorylineMath.

    /// <summary>S1, the factor's goods (StorylineFactor.cs).</summary>
    public sealed class FactorFlow : StorylineFlow
    {
        public override string Kind => StorylineMath.Factor;

        public override int Number => 1;

        public override string[] Parts => new[] { "keep", "rival", "makegood", "debt" };

        public override string[] Endings => new[] { "declined", "share", "neverknew", "debtpaid", "guards", "madegood", "unpaid", "gone", "faded" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "keep": return new[] { "kept", "lost" };
                case "rival": return new[] { "sold", "refused", "told" };
                case "makegood":
                case "debt": return new[] { "paid", "unpaid" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "lost" || outcome == "unpaid";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "keep":
                    if (outcome == "kept") return To("rival", 2f, 5f);
                    if (outcome == "lost") return To("makegood", 1f, 2f);
                    if (outcome == StorylineMath.Declined) return End("declined");
                    return End(StorylineMath.Faded);
                case "rival":
                    // Sold behind her back: she comes for her crates and finds them gone. Most likely she hears who bought them.
                    if (outcome == "sold") return roll < StorylineMath.FoundOutChance ? To("debt", 3f, 6f, "foundout") : End("neverknew", 3f, 6f);
                    // Refused, told her, or never met the rival: she comes for her crates and pays.
                    return End("share", 3f, 6f);
                case "makegood":
                    return outcome == "paid" ? End("madegood") : End("unpaid", 0.2f, 1f);
                case "debt":
                    return outcome == "paid" ? End("debtpaid") : End("guards", 0.5f, 1.5f);
            }
            return End(StorylineMath.Faded);
        }

        public override bool EndingHasLetter(string ending) => ending == "share" || ending == "neverknew" || ending == "guards" || ending == "unpaid" || ending == "gone";
    }

    /// <summary>S4, the beast coming down the valley (StorylineBeast.cs).</summary>
    public sealed class BeastFlow : StorylineFlow
    {
        public override string Kind => StorylineMath.Beast;

        public override int Number => 4;

        public override string[] Parts => new[] { "ready", "signs", "beast" };

        public override string[] Endings => new[] { "declined", "killed", "away", "gone", "faded" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "ready": return new[] { "ready", "partly", "unready" };
                case "signs": return new[] { "found", "missed" };
                case "beast": return new[] { "killed", "driven", "left" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "unready" || outcome == "missed";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "ready":
                    if (outcome == StorylineMath.Declined) return End("declined");
                    if (outcome == "ready" || outcome == "partly" || outcome == "unready") return To("signs", 2f, 4f, outcome);
                    return To("signs", 2f, 4f, "unready");
                case "signs":
                    return outcome == "found" ? To("beast", 1f, 3f, "found") : To("beast", 1f, 2f, "missed");
                case "beast":
                    if (outcome == "killed") return End("killed");
                    // Driven off or gone on its own: back next season, at most twice, then it stays away.
                    if ((outcome == "driven" || outcome == "left") && times <= StorylineMath.MaxReturns)
                        return To("beast", StorylineMath.SeasonDays - 2f, StorylineMath.SeasonDays + 2f, "back", true);
                    return End("away");
            }
            return End(StorylineMath.Faded);
        }

        public override bool EndingHasLetter(string ending) => ending == "away" || ending == "gone";
    }

    /// <summary>S8, the children from the burned village (StorylineChildren.cs).</summary>
    public sealed class ChildrenFlow : StorylineFlow
    {
        public override string Kind => StorylineMath.Children;

        public override int Number => 8;

        public override string[] Parts => new[] { "shelter", "back" };

        public override string[] Endings => new[] { "declined", "takenin", "harmed", "joined", "grown", "passed", "gone", "faded" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "shelter": return new[] { "takein", "place", "sendon", "harmed" };
                case "back": return new[] { "joined", "visited", "traded", "notjoined" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "harmed";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "shelter":
                    if (outcome == "takein") return End("takenin");
                    if (outcome == "place" || outcome == "sendon") return To("back", StorylineMath.ReturnMinDays, StorylineMath.ReturnMaxDays, outcome, true);
                    if (outcome == "harmed") return End("harmed");
                    if (outcome == StorylineMath.Declined) return End("declined");
                    return End(StorylineMath.Faded);
                case "back":
                    if (outcome == "joined") return End("joined");
                    if (outcome == StorylineMath.Declined || outcome == StorylineMath.Ended) return End("passed");
                    return End("grown");
            }
            return End(StorylineMath.Faded);
        }

        public override bool EndingHasLetter(string ending) => ending == "gone";
    }
}
