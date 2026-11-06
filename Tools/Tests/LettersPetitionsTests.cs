using ColonyCouncil;

/// <summary>
/// Phase 6 of the review, part 1 (the player's decisions 12 and 11 of 6 November 2026): letters to notables that arrive
/// in days by the colony's means and answers that bring something (World/LetterMath.cs), and shared wants that come to
/// the council as one petition (Wishes/PetitionMath.cs).
/// </summary>
public static class LettersPetitionsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "letters and petitions: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        Road(Check);
        Answers(Check);
        Petitions(Check);
        Wiring(Code, Check);
        Words(root, Check);
        return cases;
    }

    /// <summary>Every key the code builds from these rules' names is written.</summary>
    private static void Words(string root, Action<bool, string> Check)
    {
        var keys = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => System.Xml.Linq.XDocument.Load(f).Root!.Elements()).Select(e => e.Name.LocalName));
        foreach (LetterWhy w in Enum.GetValues(typeof(LetterWhy)))
            Check(keys.Contains("CC_LetterWhy_" + w), $"why in words: {w}");
        Check(keys.Contains("CC_LetterWhy_NoTraveller"), "why in words: nobody free to travel");
        foreach (RegardMove m in Enum.GetValues(typeof(RegardMove)))
            Check(keys.Contains("CC_LetterRegard_" + m), $"regard moved in words: {m}");
        for (int i = 0; i <= 6; i++)
            Check(keys.Contains("CC_LetterRegard_Level_" + i), $"regard band {i} in a word");
        foreach (LetterMeans m in Enum.GetValues(typeof(LetterMeans)))
            Check(keys.Contains("CC_LetterRoad_Status_Travelling_" + m) && keys.Contains("CC_LetterRoad_Status_Answering_" + m), $"on the road by {m}, in words");
        foreach (var k in new[] { "CC_Petition_Joined", "CC_Petition_Header", "CC_Petition_Pill", "CC_Petition_PillTip", "CC_Petition_Week", "CC_Petition_RefuseConfirm",
                     "CC_Petition_Promised", "CC_Petition_Refused", "CC_Petition_PartMet", "CC_Petition_AllMet", "CC_Petition_LapsedTitle", "CC_Petition_BrokenTitle",
                     "CC_Chronicle_PetitionJoined", "CC_Chronicle_PetitionPromised", "CC_Chronicle_PetitionRefused", "CC_Chronicle_PetitionDismissed",
                     "CC_Chronicle_PetitionGranted", "CC_Chronicle_PetitionLapsed", "CC_Chronicle_PetitionBroken" })
            Check(keys.Contains(k), "a petition's words: " + k);
    }

    // ---------------- Decision 12: letters that arrive in days ----------------

    private static void Road(Action<bool, string> Check)
    {
        Check(LetterMath.MeansOf(WorldAge.Tribal) == LetterMeans.Runner && LetterMath.MeansOf(WorldAge.Medieval) == LetterMeans.Rider
              && LetterMath.MeansOf(WorldAge.Industrial) == LetterMeans.Courier && LetterMath.MeansOf(WorldAge.Spacer) == LetterMeans.Shuttle,
            "the colony's means by its age: a runner, a rider, a courier, a courier by shuttle");
        var means = new[] { LetterMeans.Runner, LetterMeans.Rider, LetterMeans.Courier, LetterMeans.Shuttle };
        foreach (float tiles in new[] { 10f, 25f, 50f, 80f })
            for (int i = 0; i + 1 < means.Length; i++)
                Check(LetterMath.LegDays(tiles, means[i]) > LetterMath.LegDays(tiles, means[i + 1]), $"at {tiles} tiles a {means[i]} is slower than a {means[i + 1]}");
        foreach (var m in means)
        {
            Check(LetterMath.LegDays(80f, m) > LetterMath.LegDays(10f, m) || LetterMath.LegDays(10f, m) == LetterMath.MaxLegDays(m), $"farther takes longer by {m}");
            Check(LetterMath.LegDays(0f, m) == LetterMath.LegStartDays && LetterMath.LegDays(-5f, m) == LetterMath.LegStartDays, $"half a day at least by {m}");
            Check(LetterMath.LegDays(5000f, m) == LetterMath.MaxLegDays(m) && LetterMath.MaxLegDays(m) <= 6f, $"never more than {LetterMath.MaxLegDays(m)} days by {m}, however far");
        }
        // A near town in about a day, a far neighbour in days, never weeks.
        Check(LetterMath.LegDays(10f, LetterMeans.Runner) <= 1.5f && LetterMath.LegDays(10f, LetterMeans.Courier) <= 1f, "a town ten tiles off hears within a day or so");
        Check(LetterMath.LegDays(50f, LetterMeans.Runner) <= 5f && LetterMath.LegDays(50f, LetterMeans.Courier) <= 2.5f, "fifty tiles off: five days by runner, two and a half by courier");
        // The player's save: three letters to towns 50 and more tiles away, each leg twelve days (the old cap), the one
        // answer 26 days after it was written. The same letter by the colony's courier (industrial): under three days a leg.
        Check(LetterMath.OldLegDays(55f, NotableMath.Industrial) == 12f && LetterMath.LegDays(55f, LetterMeans.Courier) < 3f,
            "the save's letter: twelve days a leg before, under three by courier now");
        foreach (float tiles in new[] { 10f, 25f, 50f, 80f })
        foreach (var (age, tech) in new[] { (WorldAge.Tribal, NotableMath.Neolithic), (WorldAge.Medieval, NotableMath.Medieval), (WorldAge.Industrial, NotableMath.Industrial), (WorldAge.Spacer, NotableMath.Spacer) })
            Check(LetterMath.LegDays(tiles, LetterMath.MeansOf(age)) < LetterMath.OldLegDays(tiles, tech), $"at {tiles} tiles a {age} colony's letter is quicker than before");
    }

    private static void Answers(Action<bool, string> Check)
    {
        Check(LetterMath.Brings(LetterPurpose.Greetings, AnswerTone.Warm) == AnswerBrings.Token
              && LetterMath.Brings(LetterPurpose.Greetings, AnswerTone.Cordial) == AnswerBrings.TownNews
              && LetterMath.Brings(LetterPurpose.Greetings, AnswerTone.Cool) == AnswerBrings.TownNews, "greetings: a token with a warm answer, their town's news with a kind or cool one");
        Check(LetterMath.Brings(LetterPurpose.Condolences, AnswerTone.Warm) == AnswerBrings.Remembered
              && LetterMath.Brings(LetterPurpose.Condolences, AnswerTone.Cordial) == AnswerBrings.Remembered
              && LetterMath.Brings(LetterPurpose.Condolences, AnswerTone.Cool) == AnswerBrings.Nothing, "condolences taken to heart are remembered by their people");
        Check(LetterMath.Brings(LetterPurpose.Warning, AnswerTone.Warm) == AnswerBrings.FavourOwed
              && LetterMath.Brings(LetterPurpose.Warning, AnswerTone.Cool) == AnswerBrings.Remembered, "a warning heeded warmly is a debt of honour, heeded at all is remembered");
        Check(LetterMath.HeededWarmly >= 10, "a warning heeded warmly weighs enough to be a debt of honour (FavourMath.MinSize)");
        foreach (LetterPurpose p in Enum.GetValues(typeof(LetterPurpose)))
            Check(LetterMath.Brings(p, AnswerTone.Curt) == AnswerBrings.Nothing && LetterMath.Brings(p, AnswerTone.Refusal) == AnswerBrings.Nothing
                  && LetterMath.Brings(p, AnswerTone.Silence) == AnswerBrings.Nothing, $"a curt answer, a refusal or silence brings nothing extra ({p})");
        Check(LetterMath.TokenValue(0f) == 50f && LetterMath.TokenValue(1f) == 100f && LetterMath.TokenValue(9f) == 100f, "a token is modest");

        // Regard in words.
        Check(LetterMath.Move(5, 12, 7f) == RegardMove.UpBand && LetterMath.Move(12, 5, -7f) == RegardMove.DownBand, "into another band is said as such");
        Check(LetterMath.Move(14, 20, 6f) == RegardMove.Up && LetterMath.Move(14, 15, 1f) == RegardMove.Little && LetterMath.Move(14, 14, 0f) == RegardMove.Same
              && LetterMath.Move(14, 13, -1f) == RegardMove.Down, "within a band: a good deal, a little, not at all, cooler");

        // Why: the heaviest thing against it.
        var x = new LetterInput { purpose = LetterPurpose.Greetings, channel = LetterChannel.Courier, opinion = 20, ofWriter = 10, social = 10, warmth = 0 };
        Check(LetterMath.Why(x) == LetterWhy.Luck, "nothing much against it: not the day for it");
        Check(LetterMath.Why(With(x, y => y.hostile = true)) == LetterWhy.War, "a people at war: the war, before anything");
        Check(LetterMath.Why(With(x, y => y.opinion = -40)) == LetterWhy.Regard, "a cool regard of the colony");
        Check(LetterMath.Why(With(x, y => y.ofWriter = -60)) == LetterWhy.Writer, "no liking for the writer");
        Check(LetterMath.Why(With(x, y => y.social = 0)) == LetterWhy.Words, "a writer with no way with words");
        Check(LetterMath.Why(With(x, y => y.warmth = -2)) == LetterWhy.Nature, "a cold nature");
        Check(LetterMath.Why(With(x, y => y.lately = true)) == LetterWhy.Lately, "written for the same only lately");
        Check(LetterMath.Why(With(x, y => y.channel = LetterChannel.Console)) == LetterWhy.ByCall, "a call counts for less");
        Check(LetterMath.Why(With(x, y => y.purpose = LetterPurpose.Help)) == LetterWhy.Asked, "help asked: a great deal to ask");
        Check(LetterMath.Why(With(x, y => { y.purpose = LetterPurpose.Help; y.troubled = true; })) == LetterWhy.HardTimes, "help asked in hard times: little to spare");
        Check(LetterMath.Why(With(x, y => y.troubled = true)) == LetterWhy.Luck, "hard times don't stop kind words");
        Check(LetterMath.SaysWhy(AnswerTone.Curt) && LetterMath.SaysWhy(AnswerTone.Refusal) && !LetterMath.SaysWhy(AnswerTone.Silence) && !LetterMath.SaysWhy(AnswerTone.Cool),
            "a curt answer or a refusal says why; silence says nothing");
        Check(LetterMath.Business(LetterPurpose.Help) && LetterMath.Business(LetterPurpose.Pact) && !LetterMath.Business(LetterPurpose.Greetings) && !LetterMath.Business(LetterPurpose.Invitation),
            "business asks something; kind words don't");
    }

    private static LetterInput With(LetterInput x, Action<Box> f)
    {
        var b = new Box { x = x };
        f(b);
        return b.x;
    }

    /// <summary>A struct changed in a lambda.</summary>
    private sealed class Box
    {
        public LetterInput x;
        public LetterPurpose purpose { set => x.purpose = value; }
        public LetterChannel channel { set => x.channel = value; }
        public int opinion { set => x.opinion = value; }
        public int ofWriter { set => x.ofWriter = value; }
        public int social { set => x.social = value; }
        public int warmth { set => x.warmth = value; }
        public bool hostile { set => x.hostile = value; }
        public bool troubled { set => x.troubled = value; }
        public bool lately { set => x.lately = value; }
    }

    // ---------------- Decision 11: shared wants as one petition ----------------

    private static WantKey Want(int id, string def, int asker, int created, int petition = -1, string subject = "", int colony = 0, bool shareable = true, string thing = "") =>
        new WantKey { id = id, def = def, asker = asker, created = created, petition = petition, subject = subject, colony = colony, shareable = shareable, thing = thing };

    private static void Petitions(Action<bool, string> Check)
    {
        Check(PetitionMath.Shareable(false, false) && !PetitionMath.Shareable(true, false) && !PetitionMath.Shareable(false, true),
            "a want of one's own is shared; a wish many back for one thing is co-signed, one spoken for someone else stays theirs");
        var open = new List<WantKey> { Want(1, "Armor", 10, 100), Want(2, "Bedroom", 11, 50) };
        Check(PetitionMath.Join(open, Want(3, "Armor", 12, 200)) == 0, "a second fighter wanting armor joins the first");
        Check(PetitionMath.Join(open, Want(3, "Armor", 10, 200)) == -1, "nobody joins their own want");
        Check(PetitionMath.Join(open, Want(3, "Armor", 12, 200, colony: 1)) == -1, "another colony's want is theirs");
        Check(PetitionMath.Join(open, Want(3, "Armor", 12, 200, subject: "melee")) == -1 && PetitionMath.Join(open, Want(3, "Armor", 12, 200, thing: "Plate")) == -1,
            "only the same want: the same subject and thing");
        Check(PetitionMath.Join(open, Want(3, "Armor", 12, 200, shareable: false)) == -1, "a want that can't be shared stays a wish of its own");
        Check(PetitionMath.Join(new List<WantKey> { Want(1, "Armor", 10, 100, shareable: false) }, Want(3, "Armor", 12, 200)) == -1, "nor joins one that can't");
        var two = new List<WantKey> { Want(1, "Armor", 10, 100), Want(4, "Armor", 13, 150, 4), Want(5, "Armor", 14, 160, 4) };
        Check(PetitionMath.Join(two, Want(6, "Armor", 15, 200)) == 1, "a petition under way first, though another asked earlier");
        Check(PetitionMath.PetitionOf(Want(1, "Armor", 10, 100)) == 1 && PetitionMath.PetitionOf(two[1]) == 4, "the petition is named for its first want");
        Check(PetitionMath.Names(two, 4) == 2 && PetitionMath.Names(two, 9) == 0 && PetitionMath.Names(two, -1) == 0, "names counted");

        const int day = 60000;
        Check(PetitionMath.Deadline(10 * day, 5 * day, 12 * day, day) == 12 * day, "two days more for a name");
        Check(PetitionMath.Deadline(10 * day, 0, 11 * day, day) == 11 * day, "never past a fresh wish's full time");
        Check(PetitionMath.Deadline(10 * day, 0, 4 * day, day) == 10 * day, "never sooner than it stood");

        var mixed = new List<WantKey> { Want(1, "Armor", 10, 100, 1), Want(3, "Armor", 12, 200, 1), Want(2, "Bedroom", 11, 50), Want(5, "Armor", 14, 90, 5), Want(6, "Armor", 15, 95, 5) };
        Check(PetitionMath.Leads(mixed, 0) && !PetitionMath.Leads(mixed, 1) && PetitionMath.Leads(mixed, 2) && PetitionMath.Leads(mixed, 3) && !PetitionMath.Leads(mixed, 4),
            "one stands for each petition: its oldest");
        Check(PetitionMath.Count(mixed) == 3, "a petition counts once toward the open wishes");
        Check(PetitionMath.LetterAtLast(0) && !PetitionMath.LetterAtLast(2), "the letter waits for the last answered");

        // The player's save (days 82 to 128): proper armor wished six times by six fighters, given up five times. Each want
        // joins what is open when it is voiced; it stays open until it was given up (or to the save's end).
        var save = new (string who, float wished, float gaveUp)[]
        {
            ("Toni", 82.71f, 92.92f), ("Alo", 84.96f, 92.71f), ("Stace", 103.46f, 112.09f), ("Dukialgac", 103.92f, 113.75f), ("Harper", 104.04f, 115.79f),
            ("Shigeko", 127.5f, 999f)
        };
        var petitions = new HashSet<int>();
        var all = new List<WantKey>();
        for (int i = 0; i < save.Length; i++)
        {
            float now = save[i].wished;
            var openNow = all.Where((w, k) => save[k].gaveUp > now).ToList();
            var want = Want(i + 1, "Council_Wish_Armor", i + 1, (int)(now * day));
            int j = PetitionMath.Join(openNow, want);
            if (j >= 0)
            {
                int p = PetitionMath.PetitionOf(openNow[j]);
                want.petition = p;
                // The first want is in the petition from now on, with whoever joined it before.
                for (int k = 0; k < all.Count; k++)
                    if (all[k].petition == p || all[k].id == p)
                    {
                        var m = all[k];
                        m.petition = p;
                        all[k] = m;
                    }
            }
            all.Add(want);
            petitions.Add(want.petition >= 0 ? want.petition : want.id);
        }
        Check(petitions.Count == 3, "the save's six armor wishes come to the council as three: Toni and Alo's, the three fighters' of day 103, Shigeko's");
        Check(all.Count(w => w.petition == all[2].petition) == 3 && all[2].petition == all[2].id, "Stace, Dukialgac and Harper ask as one, Stace's petition");
        int lapses = save.Where(s => s.gaveUp < 999f).Select((s, i) => all[i].petition >= 0 ? all[i].petition : all[i].id).Distinct().Count();
        Check(lapses == 2, "five given up become two petitions let lapse");
    }

    // ---------------- In the code ----------------

    private static void Wiring(Func<string, string> Code, Action<bool, string> Check)
    {
        string letters = Code("World/NeighbourLetters.cs");
        Check(letters.Contains("LetterMath.LegDays(") && letters.Contains("LetterMath.MeansOf(Ages.Ours)"), "letters go at the colony's means by its age");
        Check(!letters.Contains("NotableMath.TravelDays("), "no letter goes at the other people's walking pace any more");
        Check(letters.Contains("LetterMath.Brings(") && letters.Contains("LetterMath.Why("), "an answer brings something, or says why not");
        Check(letters.Contains("Owed(l, n);") && Code("Diplomacy/FavoursOwed.cs").Contains("public static void Quietly("),
            "a warning's debt is made when the answer comes, and told once in the answer's words");
        string wishes = Code("CouncilGameComponent.cs") + Code("CouncilDynamics.cs") + Code("Wishes/Petitions.cs");
        Check(wishes.Contains("PetitionMath.Join(") && wishes.Contains("PetitionMath.Deadline("), "a want joins the petition for the same thing");
        Check(Code("Wishes/Wish.cs").Contains("Scribe_Values.Look(ref petition, \"petition\", -1)"), "a wish keeps its petition, -1 in an older save");
    }
}
