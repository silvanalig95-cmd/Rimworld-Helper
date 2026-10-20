using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Faith and the council (Faith/FaithCouncilMath.cs): who comes to an evening of faith, how it goes and how far it moves
/// those who came, how often the council can call one, the wish to learn another faith, the decree that names one faith
/// the colony's, and when faiths clash. Also the defs: every evening's tier has an outcome line, and the decree has no
/// mood of its own.
/// </summary>
public static class FaithCouncilTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "faith and the council: " + what);
        }

        // ---------------- Who comes ----------------
        Check(FaithCouncilMath.ComeChance(true, false, -100f, -100f, 1f) == 1f, "believers of the faith always come");
        Check(FaithCouncilMath.ComeChance(false, true, 100f, 100f, 0.2f) == 0f, "a faith that frowns on preaching keeps its people away, however open");
        float plain = FaithCouncilMath.ComeChance(false, false, 0f, 0f, 0.75f);
        Check(Math.Abs(plain - 0.35f) < 0.001f, "someone neither devout nor wavering comes about a third of the time");
        Check(FaithCouncilMath.ComeChance(false, false, 60f, 0f, 0.75f) > plain && FaithCouncilMath.ComeChance(false, false, -60f, 0f, 0.75f) < plain,
            "the open come more, the closed less");
        Check(FaithCouncilMath.ComeChance(false, false, 0f, 60f, 0.75f) > plain && FaithCouncilMath.ComeChance(false, false, 0f, -60f, 0.75f) < plain,
            "liking the speaker brings people, disliking keeps them away");
        Check(FaithCouncilMath.ComeChance(false, false, 0f, 0f, 0.4f) > plain && FaithCouncilMath.ComeChance(false, false, 0f, 0f, 0.95f) < plain,
            "the wavering come more, the devout less");
        Check(FaithCouncilMath.ComeChance(false, false, -100f, -100f, 1f) >= 0.05f && FaithCouncilMath.ComeChance(false, false, 100f, 100f, 0.1f) <= 0.95f,
            "never quite certain either way");

        // ---------------- How it goes ----------------
        Check(FaithCouncilMath.EveningQuality(20, 100f, 6, true, false, false) == 0f, "no speaker, no evening");
        Check(FaithCouncilMath.EveningQuality(20, 100f, 0, true, true, false) == 0f, "nobody to listen, no evening");
        float mid = FaithCouncilMath.EveningQuality(10, 0f, 3, false, true, false);
        Check(FaithCouncilMath.EveningQuality(16, 0f, 3, false, true, false) > mid, "a better talker, a better evening");
        Check(FaithCouncilMath.EveningQuality(10, 60f, 3, false, true, false) > mid && FaithCouncilMath.EveningQuality(10, -60f, 3, false, true, false) < mid,
            "warmth helps, coldness hurts");
        Check(FaithCouncilMath.EveningQuality(10, 0f, 6, false, true, false) > mid && FaithCouncilMath.EveningQuality(10, 0f, 12, false, true, false) == FaithCouncilMath.EveningQuality(10, 0f, 6, false, true, false),
            "a bigger crowd helps, up to six");
        Check(FaithCouncilMath.EveningQuality(10, 0f, 3, true, true, false) > mid, "the faith's own altar helps");
        Check(Math.Abs(FaithCouncilMath.EveningQuality(10, 0f, 3, false, true, true) - mid / 2f) < 0.001f, "broken off early, half");
        Check(FaithCouncilMath.EveningQuality(20, 100f, 6, true, true, false) <= 1f, "never more than 1");
        Check(FaithCouncilMath.Tier(FaithCouncilMath.EveningQuality(4, 0f, 1, false, true, false)) == EveningTier.Poor, "a poor speaker to one listener: a poor evening");
        Check(FaithCouncilMath.Tier(FaithCouncilMath.EveningQuality(8, 0f, 3, false, true, false)) == EveningTier.Good, "a fair speaker to three: a good evening");
        Check(FaithCouncilMath.Tier(FaithCouncilMath.EveningQuality(16, 50f, 5, true, true, false)) == EveningTier.Fine, "a gifted, warm speaker at the altar to five: a fine evening");

        // ---------------- What it does ----------------
        Check(FaithCouncilMath.EveningSway(0.1f) == 0f && FaithCouncilMath.EveningSteady(0.1f) == 0f, "a poor evening moves and steadies nobody");
        Check(Math.Abs(FaithCouncilMath.EveningSway(FaithCouncilMath.GoodFrom) - 0.06f) < 0.001f && Math.Abs(FaithCouncilMath.EveningSway(1f) - 0.12f) < 0.001f,
            "a good evening's base sway runs from 6% to 12%");
        Check(FaithCouncilMath.EveningSway(0.5f) < FaithCouncilMath.EveningSway(0.8f), "the better the evening, the further it moves");
        Check(FaithCouncilMath.EveningSteady(0.5f) > 0f && FaithCouncilMath.EveningSteady(1f) <= 0.06f + 0.0001f, "believers steadied a little, 6% at most");
        // The listener's own side still counts: through FaithMath.Sway, a proud devout barely moves, an open friend does.
        float proud = FaithMath.Sway(FaithCouncilMath.EveningSway(0.6f), 1f, -40f, 80f, -10f, false);
        float open = FaithMath.Sway(FaithCouncilMath.EveningSway(0.6f), 1f, 60f, 0f, 50f, false);
        Check(proud < 0.06f && open > 0.1f && open <= FaithMath.MaxSway, "a proud, closed listener barely moves; an open friend moves well, never past the cap");

        // ---------------- How often ----------------
        Check(!FaithCouncilMath.EveningAllowed(4.9f, 30f) && !FaithCouncilMath.EveningAllowed(30f, 9f) && FaithCouncilMath.EveningAllowed(5f, 10f),
            "one evening in five days, and the same faith one in ten");

        // ---------------- The wish ----------------
        Check(FaithCouncilMath.WishWeight(0.7f, 80f) == 0f && FaithCouncilMath.WishWeight(0.4f, 10f) == 0f, "only the wavering ask, and only of someone they think well of");
        Check(FaithCouncilMath.WishWeight(0.3f, 40f) > FaithCouncilMath.WishWeight(0.55f, 40f) && FaithCouncilMath.WishWeight(0.4f, 90f) > FaithCouncilMath.WishWeight(0.4f, 25f),
            "the shakier and the fonder, the likelier");
        Check(FaithCouncilMath.WishSway >= 0.1f && FaithCouncilMath.WishSway <= FaithMath.MaxSway && FaithCouncilMath.WishSway > FaithCouncilMath.EveningSway(0.5f),
            "an evening with the believer moves more than an ordinary evening of faith, within the cap");

        // ---------------- The decree ----------------
        Check(Math.Abs(FaithCouncilMath.DecreeSway(0f, 0f) - 0.012f) < 0.0001f && Math.Abs(FaithCouncilMath.DecreeSway(60f, 0f) - 0.02f) < 0.0001f,
            "a day's sway is 1.2%, up to 2% for those who like its believers");
        Check(FaithCouncilMath.DecreeSway(30f, 90f) > FaithCouncilMath.DecreeSway(30f, 0f) && FaithCouncilMath.DecreeSway(30f, -90f) < FaithCouncilMath.DecreeSway(30f, 0f)
              && FaithCouncilMath.DecreeSway(100f, 100f) <= 0.026f, "the open a little more, the closed a little less, never much past 2%");
        float openTrust = FaithCouncilMath.DecreeTrustPerDay(50f, 0.7f, false);
        float mostTrust = FaithCouncilMath.DecreeTrustPerDay(0f, 0.7f, false);
        float devoutTrust = FaithCouncilMath.DecreeTrustPerDay(50f, 0.95f, false);
        Check(openTrust > mostTrust && mostTrust > devoutTrust && devoutTrust > FaithCouncilMath.DecreeTrustPerDay(0f, 0.95f, true) && openTrust < 0f,
            "everyone of another faith trusts the council less: the open least, the devout more, a faith against preaching most");
        Check(FaithCouncilMath.DecreeTrustPerDay(0f, 0.7f, false) * 15f >= -10f, "over a whole decree, most lose under ten points of trust");
        Check(FaithCouncilMath.DecreeTalkFactor > 1f && FaithCouncilMath.DecreeTalkFactor <= 2f, "its believers talk of it more often, not endlessly");

        // ---------------- When faiths clash ----------------
        Check(!FaithCouncilMath.Clashing(1, 0.3f) && FaithCouncilMath.Clashing(2, 0.3f) && FaithCouncilMath.Clashing(0, 0.5f), "two faiths of two or more, or a split of a half");
        Check(FaithCouncilMath.ClashChance(false, false) < FaithCouncilMath.ClashChance(true, false) && FaithCouncilMath.ClashChance(false, true) == 1f,
            "a quarrel or an altar sharpens it");
        // The player's colony: six people, five faiths.
        Check(FaithCouncilMath.Clashing(1, FaithMath.Split(5, 6)), "six people of five faiths clash");

        // ---------------- The defs ----------------
        var defs = Path.Combine(root, "Defs");
        var pools = XDocument.Load(Path.Combine(defs, "FlavorPoolDefs", "Rimfolk_Pools_FaithCouncil.xml"));
        var outcome = pools.Root!.Elements("ColonyCouncil.FlavorPoolDef").FirstOrDefault(e => e.Element("defName")?.Value == "Council_Pool_FaithEveningOutcome");
        var lines = outcome?.Descendants("li").Select(l => l.Value).ToList() ?? new List<string>();
        foreach (var tier in new[] { "poor", "good", "fine" })
            Check(lines.Any(l => l.StartsWith("[" + tier + "]")), $"a {tier} evening always has an outcome line that needs nothing else");
        var decree = XDocument.Load(Path.Combine(defs, "DecreeDefs", "Rimfolk_Decrees_Faith.xml")).Root!.Element("ColonyCouncil.DecreeDef");
        Check(decree?.Element("oneFaith")?.Value == "true" && decree.Element("thought") == null && decree.Element("hediff") == null,
            "the decree is the one faith decree, with no mood or effect of its own");
        var thoughts = XDocument.Load(Path.Combine(defs, "ThoughtDefs", "Rimfolk_Thoughts_FaithCouncil.xml")).Root!.Elements("ThoughtDef").ToList();
        Check(thoughts.All(t => t.Descendants("baseMoodEffect").All(m => float.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture) >= 0f && float.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture) <= 3f)),
            "no bad moods, and the good ones small");
        return cases;
    }
}
