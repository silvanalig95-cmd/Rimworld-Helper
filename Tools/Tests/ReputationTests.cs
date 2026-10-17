using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Reputation and gossip (Source/ColonyCouncil/Social/Gossip/RepMath.cs): how a teller's opinion and character colour a
/// story, how far a rumour may grow (and never turn round), how much a listener believes by their own character, how a
/// deed weighs with the bold, the warm and the wary, how talk fades, the caps, and what a name does. Also the deed defs:
/// every deed says something about someone, has a text and a topic for the log, and only names placeholders it has.
/// </summary>
public static class ReputationTests
{
    private static float[] Persona(PersonaAxis axis, float v)
    {
        var p = new float[PersonaMath.Axes];
        p[(int)axis] = v;
        return p;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "reputation: " + what);
        }

        // ---------- Who tells it how ----------
        Check(RepMath.SlantFor(1, 60f, true, false, 0f, false, 0.9f) == Slant.Praise, "a friend talks a good deed up");
        Check(RepMath.SlantFor(-1, 60f, true, false, 0f, false, 0.9f) == Slant.Praise, "a friend plays a bad deed down");
        Check(RepMath.SlantFor(1, -40f, false, true, 0f, false, 0.9f) == Slant.Scorn, "a rival plays a good deed down");
        Check(RepMath.SlantFor(-1, -40f, false, true, 0f, false, 0.9f) == Slant.Scorn, "a rival makes a bad deed worse");
        Check(RepMath.SlantFor(1, 5f, false, false, 0f, false, 0.9f) == Slant.Plain, "someone who doesn't care either way tells it plain");
        Check(RepMath.SlantFor(1, 0f, false, false, 90f, false, 0.05f) == Slant.Joke, "a born joker turns it into a joke");
        Check(RepMath.SlantFor(1, 0f, false, false, 10f, false, 0.0f) != Slant.Joke, "someone with no humour never jokes");
        Check(RepMath.SlantFor(-1, 0f, false, false, 100f, false, 0f, true) != Slant.Joke, "nobody jokes about someone hurt or a cruelty");
        Check(RepMath.SlantFor(1, 0f, false, false, 0f, true, 0.5f) == Slant.Boast, "their own deed is a boast");
        Check(RepMath.JokeChance(100f) <= 0.5f && RepMath.JokeChance(60f) > RepMath.JokeChance(40f), "jokes come more often with more humour, never most of the time");
        Check(RepMath.MayBoast(50f, 1) && !RepMath.MayBoast(50f, -1) && !RepMath.MayBoast(10f, 1), "only the proud boast, and only of the good");
        Check(RepMath.MayTellInFront(50f, false) && RepMath.MayTellInFront(0f, true) && !RepMath.MayTellInFront(0f, false),
            "only a hothead or a rival says it with the subject standing there");
        Check(RepMath.Grave(new[] { 0f, -8f, 0f, 0f, 0f, 8f }) && !RepMath.Grave(new[] { 0f, 0f, 0f, 0f, 0f, 7f }), "a cruelty is grave, a tantrum isn't");

        // ---------- How a story grows ----------
        float truth = 1f;
        Check(RepMath.Retold(1f, truth, Slant.Praise, 1, 1f) > 1f && RepMath.Retold(1f, truth, Slant.Praise, -1, 1f) < 1f, "praise grows the good and shrinks the bad");
        Check(RepMath.Retold(1f, truth, Slant.Scorn, 1, 1f) < 1f && RepMath.Retold(1f, truth, Slant.Scorn, -1, 1f) > 1f, "scorn shrinks the good and grows the bad");
        float v = truth;
        for (int i = 0; i < 50; i++) v = RepMath.Retold(v, truth, Slant.Scorn, -1, 1f);
        Check(Math.Abs(v - truth * RepMath.MaxGrowth) < 0.001f, $"a rumour grows only so far, however often it's told ({v})");
        v = truth;
        for (int i = 0; i < 50; i++) v = RepMath.Retold(v, truth, Slant.Praise, -1, 0f);
        Check(Math.Abs(v - truth * RepMath.MinShrink) < 0.001f && v > 0f, $"excused over and over, a bad deed still happened ({v})");
        bool bounded = true;
        var rnd = new Random(7);
        foreach (Slant s in Enum.GetValues(typeof(Slant)))
            for (int i = 0; i < 200; i++)
            {
                float x = RepMath.Retold((float)rnd.NextDouble() * 3f, 2f, s, rnd.Next(2) == 0 ? 1 : -1, (float)rnd.NextDouble());
                bounded &= x >= 2f * RepMath.MinShrink - 0.0001f && x <= 2f * RepMath.MaxGrowth + 0.0001f;
            }
        Check(bounded, "every retelling stays within the bounds of the truth");
        Check(RepMath.Retold(1f, 0f, Slant.Scorn, -1, 1f) == 0f, "nothing can't be made into something");

        // ---------- Who believes it ----------
        float wary = RepMath.Belief(-1, -80f, 0f, 0f, Slant.Plain), trusting = RepMath.Belief(-1, 80f, 0f, 0f, Slant.Plain);
        Check(wary > trusting, $"the wary believe the bad more readily ({wary} vs {trusting})");
        Check(RepMath.Belief(1, 80f, 0f, 0f, Slant.Plain) > RepMath.Belief(1, -80f, 0f, 0f, Slant.Plain), "the trusting believe the good more readily");
        Check(RepMath.Belief(-1, 0f, 80f, 0f, Slant.Plain) < RepMath.Belief(-1, 0f, -80f, 0f, Slant.Plain), "bad news about a friend is believed less");
        Check(RepMath.Belief(1, 0f, 0f, -80f, Slant.Plain) < RepMath.Belief(1, 0f, 0f, 80f, Slant.Plain), "a story from someone disliked is believed less");
        Check(RepMath.Belief(1, 0f, 0f, 0f, Slant.Boast) < RepMath.Belief(1, 0f, 0f, 0f, Slant.Plain), "a boast is believed less");
        Check(RepMath.Belief(1, 0f, 0f, 0f, Slant.Joke) < RepMath.Belief(1, 0f, 0f, 0f, Slant.Plain), "a joke is taken less seriously");
        Check(RepMath.Belief(-1, -100f, -100f, 100f, Slant.Plain) <= 1f && RepMath.Belief(1, -100f, -100f, -100f, Slant.Boast) >= 0.2f, "belief stays within 0.2..1");

        // ---------- What it means to them ----------
        Check(RepMath.Weigh(RepFacet.Courage, 10f, Persona(PersonaAxis.Nerve, 80f)) > RepMath.Weigh(RepFacet.Courage, 10f, Persona(PersonaAxis.Nerve, -80f)),
            "the bold admire courage more");
        Check(RepMath.Weigh(RepFacet.Kindness, -10f, Persona(PersonaAxis.Warmth, 80f)) > RepMath.Weigh(RepFacet.Kindness, 10f, Persona(PersonaAxis.Warmth, 80f)),
            "the warm condemn cruelty more than they praise kindness");
        Check(RepMath.Weigh(RepFacet.Kindness, -10f, Persona(PersonaAxis.Warmth, 80f)) > RepMath.Weigh(RepFacet.Kindness, -10f, Persona(PersonaAxis.Warmth, -80f)),
            "the warm condemn cruelty more than the cold do");
        Check(RepMath.Weigh(RepFacet.Discretion, -10f, Persona(PersonaAxis.Trust, -80f)) > RepMath.Weigh(RepFacet.Discretion, -10f, Persona(PersonaAxis.Trust, 80f)),
            "the wary mind a loose tongue more");
        Check(RepMath.Weigh(RepFacet.Temper, 10f, Persona(PersonaAxis.Temper, 80f)) < RepMath.Weigh(RepFacet.Temper, 10f, Persona(PersonaAxis.Temper, -80f)),
            "a hothead shrugs off a temper the even-tempered mind");
        bool weighBounded = true;
        foreach (var f in RepMath.All)
            foreach (var a in PersonaMath.All)
                foreach (float x in new[] { -100f, 100f })
                {
                    float w = RepMath.Weigh(f, -5f, Persona(a, x));
                    weighBounded &= w >= 0.5f && w <= 2f;
                }
        Check(weighBounded, "no character makes a deed weigh less than half or more than double");

        // ---------- Fading and caps ----------
        Check(Math.Abs(RepMath.Faded(2f, 60f, 60f) - 1f) < 0.001f && RepMath.Faded(2f, 0f, 60f) == 2f, "talk halves in a deed's fade days, and not before");
        Check(RepMath.Faded(1f, 600f, 60f) < RepMath.Forgotten, "an old deed is forgotten in the end");
        Check(RepMath.Merged(1f, 1f, 0.5f) == 1.5f && RepMath.Merged(2.9f, 5f, 1f) == RepMath.MaxSize, "a repeat grows the deed, to a point");
        Check(RepMath.CapFacet(999f) == RepMath.Cap && RepMath.CapFacet(-999f) == -RepMath.Cap, "a side of a name is capped");
        Check(RepMath.Standing(100f, 4) == 25f && RepMath.Standing(1000f, 4) == RepMath.Cap && RepMath.Standing(5f, 0) == 0f,
            "a name is what everyone makes of them, shared out over the colony, capped");
        Check(RepMath.Standing(12f, 1) > RepMath.Standing(12f, 9), "a story known to one of ten makes less of a name than one everyone knows");
        Check(RepMath.Degree(5.9f) == 0 && RepMath.Degree(6f) == 1 && RepMath.Degree(-18f) == -2, "a side shows from 6 and is strong from 18");
        Check(RepMath.MaxDeedsPerSubject > 0 && RepMath.MaxDeeds >= RepMath.MaxDeedsPerSubject * 20 && RepMath.MaxKnowers >= 20, "the caps leave room for a large colony");

        // ---------- What a name does ----------
        float last = -1f;
        bool rising = true;
        for (float e = -40f; e <= 40f; e += 1f)
        {
            int s = RepMath.OpinionStage(e);
            // No opinion sits between the bad stages (0..2) and the good ones (3..5).
            float rank = s < 0 ? 2.5f : s;
            rising &= rank >= last;
            last = rank;
        }
        Check(rising, "the opinion from what's heard only gets better as the esteem does");
        Check(RepMath.OpinionStage(0f) == -1 && RepMath.OpinionStage(-6f) >= 0 && RepMath.OpinionStage(6f) == -1, "small talk makes no opinion, and the bad is felt sooner than the good");
        Check(RepMath.Valence(new[] { 12f, 6f, 0f, 0f, 0f, 0f }) == 1 && RepMath.Valence(new[] { 0f, 0f, 0f, 0f, 0f, 7f }) == -1, "a rescue is good, a tantrum bad");
        Check(RepMath.BraveRelief(5f) == 0f && RepMath.BraveRelief(10f) > 0f && RepMath.BraveRelief(30f) > RepMath.BraveRelief(10f), "the brave ease those beside them in a fight");
        Check(RepMath.ConfideFactor(-30f) < RepMath.ConfideFactor(-10f) && RepMath.ConfideFactor(-10f) < 1f && RepMath.ConfideFactor(0f) == 1f,
            "a gossip is told fewer secrets");
        Check(Math.Abs(RepMath.VoteLean(1000f)) <= 2f && RepMath.VoteLean(-30f) < 0f && RepMath.VoteLean(30f) > 0f, "a name sways a vote, a little");
        Check(RepMath.EpithetEase(30f) < RepMath.EpithetEase(8f) && RepMath.EpithetEase(8f) < 1f && RepMath.EpithetEase(0f) == 1f, "a strong name earns a colony name sooner");
        Check(RepMath.TellWeight(1f, 0f, true, 0f, 0f, 0f) > RepMath.TellWeight(1f, 0f, false, 0f, 0f, 0f), "newcomers are told more");
        Check(RepMath.TellWeight(1f, 100f, true, 0f, 0f, 0f) == RepMath.TellWeight(1f, 0f, true, 0f, 0f, 0f), "old news is still news to a newcomer");
        Check(RepMath.TellWeight(1f, 60f, false, 0f, 0f, 0f) < RepMath.TellWeight(1f, 0f, false, 0f, 0f, 0f), "old news is told less");
        Check(RepMath.TellWeight(1f, 0f, false, 80f, 0f, 0f) > RepMath.TellWeight(1f, 0f, false, -80f, 0f, 0f), "jokers talk more");
        Check(RepMath.TellWeight(2f, 0f, false, 0f, 0f, 0f) > RepMath.TellWeight(1f, 0f, false, 0f, 0f, 0f), "a scandal travels faster");

        // ---------- The deed defs ----------
        var deeds = XDocument.Load(Path.Combine(root, "Defs", "DeedDefs", "Rimfolk_Deeds.xml")).Root!.Elements("ColonyCouncil.DeedDef").ToList();
        Check(deeds.Count >= 25, $"found the deeds ({deeds.Count})");
        var topics = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "InteractionDefs"), "Rimfolk_Gossip*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements("RulePackDef"))
                topics.Add(d.Element("defName")?.Value ?? "");
        var pools = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "Rimfolk_Gossip*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                pools.Add(d.Element("defName")?.Value ?? "");
        foreach (var d in deeds)
        {
            string name = d.Element("defName")?.Value ?? "?";
            var f = d.Element("facets");
            Check(f != null && f.Elements().Any(e => float.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture) != 0f), $"{name}: says something about someone");
            string text = d.Element("text")?.Value ?? "";
            Check(text.Length > 0 && text.Length <= 45, $"{name}: a short text ({text.Length} letters), said after a name");
            Check(!text.Contains("SUBJECT_"), $"{name}: its text doesn't refer back to the doer (it's said after a name, and after 'I')");
            foreach (var t in new[] { text, d.Element("textAlone")?.Value ?? "" })
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(t, @"\{([A-Z_a-z]+)\}"))
                    Check(m.Groups[1].Value is "VICTIM" or "DETAIL", $"{name}: only {{VICTIM}} and {{DETAIL}} in its text, not {{{m.Groups[1].Value}}}");
            string topic = d.Element("logTopic")?.Value ?? "";
            Check(topics.Contains(topic), $"{name}: its log topic {topic} exists");
            Check(!string.IsNullOrEmpty(d.Element("theme")?.Value), $"{name}: has a theme for its lines");
        }
        foreach (var slant in new[] { "Plain", "Praise", "Scorn", "Joke" })
            foreach (var side in new[] { "Good", "Bad" })
            {
                Check(pools.Contains($"Rimfolk_Gossip_{side}_{slant}"), $"there are lines for a {side.ToLower()} deed told {slant.ToLower()}");
                Check(topics.Contains($"Rimfolk_GossipLog_{side}_{slant}"), $"the log has words for a {side.ToLower()} deed told {slant.ToLower()}");
            }
        foreach (var r in new[] { "Admire", "Condemn", "Laugh", "Doubt", "Defend", "Shrug", "Subject" })
            Check(pools.Contains("Rimfolk_Gossip_React_" + r), $"there are answers of the {r.ToLower()} kind");
        return cases;
    }
}
