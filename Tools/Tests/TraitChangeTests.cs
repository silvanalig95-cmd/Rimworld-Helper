using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Traits that change with what happens to a colonist (Persona/TraitChangeMath.cs, Persona/TraitChanges.cs and the
/// TraitChangeDefs): where a degree steps, the room for a new trait, the rate limits, the absorbing of a trait's nature
/// into what life had moved (the person's sides never jump), the game's conflicts, the facts and paces a rule reads,
/// the deeds counted on separate occasions, and the rules themselves: real traits only (never the body, sexuality or a
/// gene's), real marks, deeds and records, lines in several voices, and the two extreme traits once in a colony's life.
/// </summary>
public static class TraitChangeTests
{
    private const int Day = TraitChangeMath.DayTicks;

    // The game's trait defNames (Core, Biotech, Anomaly), from the game's own def lists (the official translation's
    // DefInjected TraitDef files name every one).
    private static readonly HashSet<string> GameTraits = new HashSet<string>
    {
        "Abrasive", "AnnoyingVoice", "Ascetic", "Asexual", "Bisexual", "Bloodlust", "BodyPurist", "Brawler", "Cannibal", "CreepyBreathing",
        "DislikesMen", "DislikesWomen", "FastLearner", "Gay", "Gourmand", "GreatMemory", "Greedy", "Jealous", "Kind", "Masochist", "NightOwl",
        "Nimble", "Nudist", "Psychopath", "Pyromaniac", "QuickSleeper", "SlowLearner", "TooSmart", "TorturedArtist", "Tough", "Transhumanist",
        "Undergrounder", "Wimp", "Beauty", "DrugDesire", "Immunity", "Industriousness", "NaturalMood", "Nerves", "Neurotic", "PsychicSensitivity",
        "ShootingAccuracy", "SpeedOffset", "Delicate", "Recluse", "BodyMastery", "Disturbing", "Joyous", "Occultist", "PerfectMemory", "VoidFascination"
    };

    // The degrees of the game's degree traits (Traits_Spectrum).
    private static readonly Dictionary<string, int[]> Degrees = new Dictionary<string, int[]>
    {
        ["Nerves"] = new[] { -2, -1, 1, 2 },
        ["NaturalMood"] = new[] { -2, -1, 1, 2 },
        ["Industriousness"] = new[] { -2, -1, 1, 2 },
        ["DrugDesire"] = new[] { -1, 1, 2 },
        ["SpeedOffset"] = new[] { -1, 1, 2 }
    };

    // The memories, deeds, tales, records and skills the rules name, each one the game has: memories from its thought
    // defs, deeds from HistoryEventDefOf, tales from TaleDefOf, records from its record defs.
    private static readonly HashSet<string> GameThoughts = new HashSet<string>
    {
        "AteHumanlikeMeatDirect", "AteHumanlikeMeatAsIngredient", "AteRawHumanlikeMeat", "AteHumanMeat_Disapproved", "AteHumanMeat_Horrible",
        "AteHumanMeat_Abhorrent", "AteHumanMeat_Preferred", "AteHumanMeat_RequiredStrong", "AteHumanMeat_RequiredRavenous"
    };
    private static readonly HashSet<string> GameEvents = new HashSet<string>
    {
        "ExecutedPrisoner", "ExecutedGuest", "ExecutedColonist", "ExecutedPrisonerGuilty", "ExecutedPrisonerInnocent", "HarvestedOrgan",
        "HarvestedOrganFromGuest", "HarvestedOrganFromColonist", "AteHumanMeat", "AteHumanMeatDirect", "AteHumanMeatAsIngredient"
    };
    private static readonly HashSet<string> GameTales = new HashSet<string> { "ExecutedPrisoner" };
    private static readonly HashSet<string> GameRecords = new HashSet<string>
    {
        "KillsHumanlikes", "Kills", "TimesInMentalState", "TimeGettingJoy", "TimeUnderRoof", "TimeAsColonistOrColonyAnimal", "TimeInBed"
    };

    /// <summary>A vow as the player's save has it: open, the colony at war with those who did it, none of them fallen yet.</summary>
    private static TraitChangeMath.Vow OpenVow(int sworn, string loss) => new TraitChangeMath.Vow(sworn, true, true, 0, loss);

    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "trait changes: " + what);
        }

        // ---------- Where a degree steps ----------
        var nerves = new List<int> { -2, -1, 1, 2 };
        var kind = new List<int> { 0 };
        var none = new List<int>();
        var up = TraitChangeMath.Plan(TraitChangeKind.StepUp, false, 0, 0, false, nerves, new List<int> { -1, 0 });
        Check(up.ok && up.Adds && up.to == 1, "stepping nerves up from none gains steadfast");
        var steadied = TraitChangeMath.Plan(TraitChangeKind.StepUp, true, -1, 0, false, nerves, new List<int> { -1, 0 });
        Check(steadied.ok && steadied.Removes && steadied.to == 0, "stepping nervous up takes the trait away (none is between)");
        var iron = TraitChangeMath.Plan(TraitChangeKind.StepUp, true, 1, 0, false, nerves, new List<int> { 1 });
        Check(iron.ok && iron.hadTrait && iron.hasAfter && !iron.Adds && iron.to == 2, "steadfast steps up to iron-willed, the same trait");
        Check(!TraitChangeMath.Plan(TraitChangeKind.StepUp, true, 2, 0, false, nerves, none).ok, "iron-willed can't step further up");
        Check(!TraitChangeMath.Plan(TraitChangeKind.StepDown, true, -2, 0, false, nerves, none).ok, "volatile can't step further down");
        Check(TraitChangeMath.Plan(TraitChangeKind.StepUp, true, 1, 0, false, nerves, new List<int> { -1, 0 }).why == "from", "a step only from the degrees it lists");
        var down = TraitChangeMath.Plan(TraitChangeKind.StepDown, false, 0, 0, false, new List<int> { -1, 1, 2 }, new List<int> { 0, 1 });
        Check(down.ok && down.Adds && down.to == -1, "an addiction overcome: from none down to teetotaler");
        var interest = TraitChangeMath.Plan(TraitChangeKind.StepDown, true, 1, 0, false, new List<int> { -1, 1, 2 }, new List<int> { 0, 1 });
        Check(interest.ok && interest.Removes, "a chemical interest stepped down is gone");
        var gain = TraitChangeMath.Plan(TraitChangeKind.Gain, false, 0, 0, false, kind, none);
        Check(gain.ok && gain.Adds, "kind can be gained by someone without it");
        Check(TraitChangeMath.Plan(TraitChangeKind.Gain, true, 0, 0, false, kind, none).why == "has", "a trait they have can't be gained again");
        Check(TraitChangeMath.Plan(TraitChangeKind.Gain, false, 0, 3, false, kind, none).why == "degree", "a degree the trait doesn't have can't be gained");
        var lose = TraitChangeMath.Plan(TraitChangeKind.Lose, true, 0, 0, false, kind, none);
        Check(lose.ok && lose.Removes, "a trait they have can fade");
        Check(TraitChangeMath.Plan(TraitChangeKind.Lose, false, 0, 0, false, kind, none).why == "lacks", "a trait they lack can't fade");
        Check(TraitChangeMath.Plan(TraitChangeKind.Lose, true, 1, -1, false, nerves, none).why == "degree"
              && TraitChangeMath.Plan(TraitChangeKind.Lose, true, 1, -1, true, nerves, none).ok, "a loss of one degree, or of any");

        // ---------- Room for a new trait ----------
        Check(TraitChangeMath.RoomFor(gain, 3) && !TraitChangeMath.RoomFor(gain, 4) && !TraitChangeMath.RoomFor(gain, 5), "nobody gains past four traits");
        Check(TraitChangeMath.RoomFor(iron, 5) && TraitChangeMath.RoomFor(lose, 5), "a degree change or a loss needs no room, so five stay five at most");

        // ---------- Rate limits ----------
        int now = 500 * Day;
        Check(TraitChangeMath.RateBlocked(-1, -1, -1, 120, now) == null, "nobody has changed: free to change");
        Check(TraitChangeMath.RateBlocked(-1, now - 9 * Day, -1, 0, now) == "colony", "one change in the colony per ten days");
        Check(TraitChangeMath.RateBlocked(-1, now - 11 * Day, -1, 0, now) == null, "after ten days the colony may see another");
        Check(TraitChangeMath.RateBlocked(now - 59 * Day, -1, -1, 0, now) == "person", "one change per person in sixty days");
        Check(TraitChangeMath.RateBlocked(now - 61 * Day, -1, -1, 0, now) == null, "after sixty days the same person may change again");
        Check(TraitChangeMath.RateBlocked(-1, -1, now - 100 * Day, 120, now) == "trait" && TraitChangeMath.RateBlocked(-1, -1, now - 130 * Day, 120, now) == null,
            "the same trait waits out its rule's gap");
        Check(TraitChangeMath.RateBlocked(now - 20 * Day, -1, -1, 0, now, true) == null, "taking away a trait Rimfolk gave isn't held back by the person's sixty days");
        Check(TraitChangeMath.RateBlocked(now - 20 * Day, now - 5 * Day, -1, 0, now, true) == "colony", "but the colony's ten days still hold");
        Check(TraitChangeMath.RateBlocked(now - 20 * Day, -1, now - 20 * Day, 45, now, true) == "trait", "and so does the rule's own gap");
        // A year of daily checks with something always ready: the colony sees at most one change per ten days, a person one per sixty.
        int last = -1;
        var lastBy = new Dictionary<int, int>();
        int made = 0, most = 0;
        for (int d = 0; d < 600; d++)
        {
            int t = d * Day;
            for (int p = 0; p < 8; p++)
            {
                int lp = lastBy.TryGetValue(p, out int x) ? x : -1;
                if (TraitChangeMath.RateBlocked(lp, last, -1, 0, t) != null) continue;
                last = t;
                lastBy[p] = t;
                made++;
                break;
            }
        }
        foreach (var p in lastBy.Keys) most = Math.Max(most, lastBy[p]);
        Check(made <= 61 && made >= 50, $"ten years of a colony always ready: about one change per ten days ({made})");

        // ---------- Absorbing: a trait stands for the change, nothing doubles ----------
        float Total(float n, float l, float d) => PersonaMath.Total(n, l, d);
        {
            // Many kindnesses moved warmth by 40 (15 for good, 25 fading); kind adds 50 to nature.
            var lasting = new float[8];
            var drift = new float[8];
            lasting[0] = 15f;
            drift[0] = 25f;
            float before = Total(10f, lasting[0], drift[0]);
            var delta = new float[8];
            delta[0] = 50f;
            float jump = TraitChangeMath.Absorb(lasting, drift, delta, PersonaMath.MaxLasting, PersonaMath.MaxDrift);
            Check(Math.Abs(Total(60f, lasting[0], drift[0]) - before) < 0.01f && jump == 0f, "gaining kind leaves warmth where the kindnesses had put it");
            Check(drift[0] == 0f && Math.Abs(lasting[0] - -10f) < 0.01f, "the fading part goes into the trait first (now for good), the rest from what stays");
            // Later, the fading would have gone anyway: the trait keeps the change.
            Check(Total(60f, lasting[0], 0f) > Total(10f, 15f, 0f), "once the fading part would have faded, the trait still holds the change");
        }
        {
            // Abrasive fades: nature warmth +25, temper -30, pride -15 (the trait's own shift taken away).
            var lasting = new float[] { 6f, -2f, 0f, 0f, 0f, 3f, 0f, 0f };
            var drift = new float[] { 14f, 0f, 0f, 0f, 0f, -5f, 0f, 0f };
            var nature = new float[] { -20f, 0f, 40f, 0f, 0f, 25f, 0f, 0f };
            var after = new float[] { 5f, 0f, 10f, 0f, 0f, 10f, 0f, 0f };
            var delta = new float[8];
            for (int i = 0; i < 8; i++) delta[i] = after[i] - nature[i];
            var before = new float[8];
            for (int i = 0; i < 8; i++) before[i] = Total(nature[i], lasting[i], drift[i]);
            TraitChangeMath.Absorb(lasting, drift, delta, PersonaMath.MaxLasting, PersonaMath.MaxDrift);
            bool same = true;
            for (int i = 0; i < 8; i++) same &= Math.Abs(Total(after[i], lasting[i], drift[i]) - before[i]) < 0.01f;
            Check(same, "losing abrasive puts its shifts back: every side stays where it was");
        }
        {
            // Many random cases: the sides never jump while the caps hold, and gaining then losing the same trait comes back.
            var rng = new Random(7);
            int ok = 0, tries = 0, back = 0;
            for (int k = 0; k < 2000; k++)
            {
                var l = new float[8];
                var dr = new float[8];
                var n = new float[8];
                var delta = new float[8];
                for (int i = 0; i < 8; i++)
                {
                    l[i] = (float)(rng.NextDouble() * 50 - 25);
                    dr[i] = (float)(rng.NextDouble() * 60 - 30);
                    n[i] = (float)(rng.NextDouble() * 80 - 40);
                    delta[i] = rng.Next(3) == 0 ? 0f : (float)(rng.NextDouble() * 50 - 25);
                }
                var b = new float[8];
                for (int i = 0; i < 8; i++) b[i] = Total(n[i], l[i], dr[i]);
                float jump = TraitChangeMath.Absorb(l, dr, delta, PersonaMath.MaxLasting, PersonaMath.MaxDrift);
                if (jump > 0f) continue;
                tries++;
                bool same = true;
                for (int i = 0; i < 8; i++) same &= Math.Abs(Total(n[i] + delta[i], l[i], dr[i]) - b[i]) < 0.01f;
                if (same) ok++;
                var neg = delta.Select(v => -v).ToArray();
                TraitChangeMath.Absorb(l, dr, neg, PersonaMath.MaxLasting, PersonaMath.MaxDrift);
                bool home = true;
                for (int i = 0; i < 8; i++) home &= Math.Abs(Total(n[i], l[i], dr[i]) - b[i]) < 0.01f;
                if (home) back++;
            }
            Check(tries > 1500 && ok == tries, $"in {tries} random cases every side stays put when a trait comes or goes");
            Check(back == tries, "a trait gained and lost again leaves the person as they were");
        }
        {
            // Both caps reached: the jump is reported, never hidden.
            var l = new float[] { -38f, 0, 0, 0, 0, 0, 0, 0 };
            var dr = new float[] { -48f, 0, 0, 0, 0, 0, 0, 0 };
            var delta = new float[] { 30f, 0, 0, 0, 0, 0, 0, 0 };
            float jump = TraitChangeMath.Absorb(l, dr, delta, PersonaMath.MaxLasting, PersonaMath.MaxDrift);
            Check(jump > 0f && l[0] == -PersonaMath.MaxLasting && dr[0] == -PersonaMath.MaxDrift, "past both caps, what can't be absorbed is reported as a jump");
        }
        Check(TraitChangeMath.Moved(60f, -70f) == PersonaMath.MaxLasting - PersonaMath.MaxDrift, "how far life moved a side counts each part within its cap");

        // ---------- Conflicts ----------
        Check(TraitChangeMath.Conflict("Kind", new[] { "Psychopath" }, null, "Psychopath", null, null), "a trait that names another conflicts with it");
        Check(TraitChangeMath.Conflict("VTE_Coward", null, null, "VTE_Brave", new[] { "VTE_Coward" }, null), "a conflict counts whichever of the two names it");
        Check(TraitChangeMath.Conflict("A", null, new[] { "Sleep" }, "B", null, new[] { "Sleep", "Food" }), "a shared exclusion tag is a conflict");
        Check(!TraitChangeMath.Conflict("Kind", new[] { "Psychopath" }, new[] { "X" }, "Brawler", new[] { "Wimp" }, new[] { "Y" }), "traits that name nothing of each other sit together");

        // ---------- Traits never touched ----------
        foreach (var t in new[] { "Gay", "Bisexual", "Asexual" }) Check(TraitChangeMath.Untouchable(t) == "sexuality", t + " is never changed");
        foreach (var t in new[] { "Nimble", "Beauty", "Immunity" }) Check(TraitChangeMath.Untouchable(t) == "body", t + " is the body, not character");
        foreach (var t in new[] { "Tough", "SpeedOffset" })
        {
            Check(TraitChangeMath.Untouchable(t) == null, t + " can be earned by the body's history");
            Check(TraitChangeMath.TrainedOnlyGrows(t, true) && !TraitChangeMath.TrainedOnlyGrows(t, false), t + " is only ever gained, never taken");
        }
        Check(TraitChangeMath.TrainedOnlyGrows("Kind", false), "character traits may be lost");
        Check(TraitChangeMath.Untouchable("Kind") == null && TraitChangeMath.Untouchable("Nerves") == null, "character traits may change");

        // ---------- Facts, deeds and paces ----------
        Check(TraitChangeMath.TryParseFacts("KillsHumanlikes>=30, Council_VotesLost>=3", out var facts, out _) && facts.Count == 2 && facts[0].atLeast
              && facts[0].value == 30f && facts[1].name == "Council_VotesLost", "facts read a name, a direction and a number");
        Check(!TraitChangeMath.TryParseFacts("Melee=10", out _, out _) && !TraitChangeMath.TryParseFacts(">=10", out _, out _), "a fact without > or < or a name is an error");
        Check(facts[0].Holds(31) && !facts[0].Holds(29), "a fact holds or not");
        var ticks = new List<int> { now - 200 * Day, now - 90 * Day, now - 20 * Day, now - 5 * Day };
        Check(TraitChangeMath.CountSince(ticks, now, -1, 0f) == 4 && TraitChangeMath.CountSince(ticks, now, -1, 60f) == 2, "deeds count, all kept or within the days asked");
        Check(TraitChangeMath.CountSince(ticks, now, now - 30 * Day, 0f) == 2, "only deeds since the trait last changed count: one stretch, one change");
        Check(!TraitChangeMath.SeparateTime(new List<int> { now - Day * 3 }, new List<int> { 42 }, now, 42), "the same victim twice is one occasion");
        Check(TraitChangeMath.SeparateTime(new List<int> { now - Day * 3 }, new List<int> { 42 }, now, 43), "another victim is another occasion");
        Check(!TraitChangeMath.SeparateTime(new List<int> { now - Day / 4 }, new List<int> { -1 }, now, -1) && TraitChangeMath.SeparateTime(new List<int> { now - Day }, new List<int> { -1 }, now, -1),
            "meals within half a day are one occasion; a day apart, two");
        {
            // Time at recreation over time in the colony, snapshots every ten days.
            var st = new List<int>();
            var nums = new List<float>();
            var dens = new List<float>();
            for (int i = 0; i < 8; i++)
            {
                st.Add(now - (80 - i * 10) * Day);
                nums.Add(i * 10 * Day * 0.03f);
                dens.Add(i * 10 * Day);
            }
            float share = TraitChangeMath.Pace(st, nums, dens, now, 80 * Day * 0.03f, 80 * Day, 60f);
            Check(Math.Abs(share - 0.03f) < 0.001f, "a pace reads the share of time over the stretch");
            Check(TraitChangeMath.Pace(st, nums, dens, now, 80 * Day * 0.03f, 80 * Day, 90f) < 0f, "no snapshot old enough: no pace");
            Check(TraitChangeMath.Pace(st, nums, dens, now, 80 * Day * 0.03f, 40 * Day, 60f) < 0f, "away for most of the stretch: no pace");
        }
        Check(TraitChangeMath.DaysSince(-1, now) == 0f && Math.Abs(TraitChangeMath.DaysSince(now - 3 * Day, now) - 3f) < 0.001f, "calm days count from the last breakdown");
        Check(Math.Abs(TraitChangeMath.CalmDays(now - 97 * Day, now - 24 * Day, now, true) - 24f) < 0.001f, "calm since the trait came: a calm from before it doesn't count");
        Check(Math.Abs(TraitChangeMath.CalmDays(now - 97 * Day, now - 24 * Day, now, false) - 97f) < 0.001f, "plain calm counts from the last breakdown");
        Check(Math.Abs(TraitChangeMath.CalmDays(now - 10 * Day, now - 24 * Day, now, true) - 10f) < 0.001f, "a breakdown since the trait came starts the calm again");
        Check(TraitChangeMath.CalmDays(-1, now - 24 * Day, now, true) == 0f, "never looked at: no calm days");

        // ---------- What Rimfolk gave, rivals ----------
        Check(TraitChangeMath.GaveIt(new List<int> { 100 * Day }, new List<bool> { true }), "a trait a change gave is Rimfolk's to undo");
        Check(!TraitChangeMath.GaveIt(new List<int>(), new List<bool>()), "a trait they came with isn't (no change of it kept)");
        Check(!TraitChangeMath.GaveIt(new List<int> { 100 * Day, 200 * Day }, new List<bool> { true, false }), "nor one a later change already took away");
        Check(TraitChangeMath.GaveIt(new List<int> { 300 * Day, 100 * Day, 200 * Day }, new List<bool> { true, true, false }), "the newest change decides, in any order");
        Check(TraitChangeMath.IsRival(-20, false) && TraitChangeMath.IsRival(5, true) && !TraitChangeMath.IsRival(-19, false),
            "a rival: a rivalry on the Bonds page, or an opinion of -20 or less, as the game calls it");

        // ---------- Vows of vengeance (the player's save, 17 November 2026) ----------
        {
            // Alder, killed by the mechanoid hive on day 98.27; Avila, Bautista and Guido swore on day 98.29. Avila became
            // vengeful on day 100.01, Bautista on day 112.01 (the old rule). Guido also swore on the people who carried off
            // Gocrirnas, on day 86.21. The save is on day 124.2.
            int sworn = (int)(98.29f * Day), at = (int)(124.2f * Day);
            var alder = OpenVow(sworn, "Human187324");
            var avila = new List<TraitChangeMath.Vow> { alder };
            var guido = new List<TraitChangeMath.Vow> { OpenVow((int)(86.21f * Day), "Human202409"), alder };
            Check(TraitChangeMath.AnyStanding(avila), "Avila's vow on the hive still stands (open, at war, none fallen)");
            int i = TraitChangeMath.Unpaid(avila, at, null, out float days);
            Check(i == 0 && Math.Abs(days - 25.9f) < 0.1f, $"it has stood unpaid {days:0.0} days");
            // One person per loss: the old changes kept no loss, so it's read from the vows sworn by then.
            var taken = new HashSet<string>(TraitChangeMath.LossesBehind(avila, (int)(100.01f * Day)));
            Check(taken.SetEquals(new[] { "Human187324" }), "Avila's vengeful is read as Alder's loss");
            Check(TraitChangeMath.LossesBehind(avila, (int)(98f * Day)).Count == 0, "a vow sworn after a change isn't behind it");
            i = TraitChangeMath.Unpaid(guido, at + 10 * Day, taken, out days);
            Check(i == 0 && Math.Abs(days - 48f) < 0.1f, "Guido's vow for Alder can't make him vengeful too; his other vow, for Gocrirnas, could");
            Check(TraitChangeMath.Unpaid(new List<TraitChangeMath.Vow> { alder }, at + 30 * Day, taken, out days) < 0 && days == 0f,
                "a loss that has made someone vengeful makes nobody else so");
            // Paid in part, over, or at peace.
            var paid = alder;
            paid.kills = 1;
            Check(TraitChangeMath.Unpaid(new List<TraitChangeMath.Vow> { paid }, at, null, out _) < 0 && TraitChangeMath.AnyStanding(new List<TraitChangeMath.Vow> { paid }),
                "one of them fallen: not unpaid any more, though the vow still stands");
            var over = alder;
            over.open = false;
            Check(!TraitChangeMath.AnyStanding(new List<TraitChangeMath.Vow> { over }), "avenged, laid down or lapsed: no vow stands");
            var peace = alder;
            peace.atWar = false;
            Check(!TraitChangeMath.AnyStanding(new List<TraitChangeMath.Vow> { peace }) && TraitChangeMath.Unpaid(new List<TraitChangeMath.Vow> { peace }, at, null, out _) < 0,
                "peace with those who did it: the vow no longer stands, and isn't unpaid");
            Check(!TraitChangeMath.AnyStanding(new List<TraitChangeMath.Vow>()), "no vow at all (or vendettas switched off): none stands");
        }

        // ---------- The rules ----------
        var markNames = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "PersonaDefs"), "Rimfolk_PersonaMarks_*.xml"))
            foreach (var mark in XDocument.Load(file).Root!.Elements("ColonyCouncil.PersonaMarkDef"))
            {
                markNames.Add((string)mark.Element("defName")!);
                var x = mark.Element("modExtensions")?.Elements("li").FirstOrDefault();
                if (x?.Element("family") != null) markNames.Add((string)x.Element("family")!);
                if (x?.Element("story") != null) markNames.Add((string)x.Element("story")!);
            }
        var modNames = new HashSet<string>(File.ReadAllLines(Path.Combine(root, "Tools", "Tests", "ModDefNames.txt")).Where(l => !l.StartsWith("#")).Select(l => l.Trim()));
        // Rimfolk's own traits: only ever earned (commonality 0), each with one degree.
        var ours = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "TraitDefs"), "*.xml"))
            foreach (var td in XDocument.Load(file).Root!.Elements("TraitDef"))
            {
                string n = (string)td.Element("defName") ?? "";
                ours.Add(n);
                Check(n.StartsWith("Rimfolk_"), $"{n}: one of Rimfolk's own traits is named Rimfolk_");
                Check((float?)td.Element("commonality") == 0f, $"{n}: never on a new arrival (commonality 0)");
                Check(td.Element("degreeDatas")?.Elements("li").Count() == 1, $"{n}: one degree");
            }
        var habits = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "QuirkDefs"), "*.xml"))
            foreach (var q in XDocument.Load(file).Root!.Elements("ColonyCouncil.QuirkDef"))
                habits.Add((string)q.Element("defName") ?? "");
        var rules = new List<XElement>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "PersonaDefs"), "Rimfolk_TraitChanges*.xml"))
            rules.AddRange(XDocument.Load(file).Root!.Elements("ColonyCouncil.TraitChangeDef"));
        Check(rules.Count >= 30, $"about thirty rules ({rules.Count})");
        IEnumerable<string> List(string s) => (s ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0);
        var tones = new[] { "plain", "warm", "blunt", "cheerful", "gloomy", "cold" };
        foreach (var r in rules)
        {
            string name = (string)r.Element("defName")!;
            string trait = (string)r.Element("trait") ?? "";
            string change = (string)r.Element("change") ?? "Gain";
            Check(GameTraits.Contains(trait) || modNames.Contains(trait) || ours.Contains(trait), $"{name}: {trait} is a trait the game, its mod or Rimfolk has");
            string habit = (string)r.Element("habit");
            if (habit != null) Check(habits.Contains(habit), $"{name}: grows from the habit {habit}, which is one");
            foreach (var other in r.Element("insteadOf")?.Elements("li") ?? Enumerable.Empty<XElement>())
                Check(modNames.Contains(other.Value), $"{name}: gives way to {other.Value}, a mod's trait");
            if (ours.Contains(trait)) Check(r.Element("change") == null || (string)r.Element("change") == "Gain", $"{name}: Rimfolk's own traits are only gained");
            Check(TraitChangeMath.TrainedOnlyGrows(trait, change == "Gain" || change == "StepUp"), $"{name}: a trait of the body is only earned, never taken");
            Check(TraitChangeMath.Untouchable(trait) == null, $"{name}: {trait} is character, not body or sexuality");
            if (change == "StepUp" || change == "StepDown")
            {
                Check(Degrees.ContainsKey(trait), $"{name}: steps only traits with degrees");
                var from = r.Element("from")?.Elements("li").Select(li => int.Parse(li.Value)).ToList() ?? new List<int>();
                Check(from.Count > 0, $"{name}: a step says which degrees it steps from");
                if (Degrees.TryGetValue(trait, out var degs))
                    foreach (int f in from)
                    {
                        var plan = TraitChangeMath.Plan(change == "StepUp" ? TraitChangeKind.StepUp : TraitChangeKind.StepDown, f != 0, f, 0, false, degs, from);
                        Check(plan.ok, $"{name}: a step from {f} leads somewhere");
                        if (plan.ok && !plan.hasAfter) Check(r.Element("newsLost") != null, $"{name}: a step from {f} ends the trait, and says so (newsLost)");
                    }
            }
            Check(!string.IsNullOrWhiteSpace((string)r.Element("news")), $"{name}: has news");
            var journal = r.Element("journal");
            int lines = journal?.Descendants("li").Count() ?? 0;
            int voices = tones.Count(t => journal?.Element(t)?.Elements("li").Any() ?? false);
            Check(lines >= 6 && voices >= 5, $"{name}: journal lines in several voices ({lines} lines, {voices} voices)");
            foreach (var need in r.Element("marks")?.Elements("li") ?? Enumerable.Empty<XElement>())
                foreach (var m in List((string)need.Element("any")))
                    Check(markNames.Contains(m), $"{name}: the mark {m} is a mark, a family or a story");
            foreach (var m in List((string)r.Element("notMarks")))
                Check(markNames.Contains(m), $"{name}: the mark {m} it waits out is a mark, a family or a story");
            foreach (var tally in r.Element("tallies")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                foreach (var th in List((string)tally.Element("thoughts"))) Check(GameThoughts.Contains(th), $"{name}: the memory {th} is the game's");
                foreach (var ev in List((string)tally.Element("events"))) Check(GameEvents.Contains(ev), $"{name}: the deed {ev} is the game's");
                foreach (var ta in List((string)tally.Element("tales"))) Check(GameTales.Contains(ta), $"{name}: the tale {ta} is the game's");
                Check(!string.IsNullOrWhiteSpace((string)tally.Element("label")), $"{name}: a deed counted is named in the letter");
            }
            if (TraitChangeMath.TryParseFacts((string)r.Element("records"), out var recs, out _))
                foreach (var f in recs) Check(GameRecords.Contains(f.name) || f.name.StartsWith("Council_"), $"{name}: the record {f.name} is kept by the game or Rimfolk");
            foreach (var pace in r.Element("pace")?.Elements("li") ?? Enumerable.Empty<XElement>())
                Check(GameRecords.Contains((string)pace.Element("record") ?? ""), $"{name}: the pace reads a record the game keeps");
            float chance = (float?)r.Element("chance") ?? 0.1f;
            Check(chance > 0f && chance <= 0.2f, $"{name}: a small chance per day once it holds ({chance})");
            if (trait == "Psychopath" || trait == "Cannibal")
            {
                Check((string)r.Element("onceKey") == trait, $"{name}: {trait} comes at most once in a colony's life");
                Check(r.Element("tallies") != null && (int?)r.Element("tallies")!.Elements("li").Max(li => (int?)li.Element("count") ?? 1) >= 1,
                    $"{name}: {trait} needs deeds counted, never a single happening");
                Check(((int?)r.Element("tone") ?? 0) < 0, $"{name}: {trait} is a negative letter");
            }
            if (trait == "Cannibal") Check(r.Element("tallies")!.Elements("li").All(li => ((int?)li.Element("count") ?? 1) >= 3), $"{name}: cannibalism needs at least three separate meals");
        }
        Check(rules.Count(r => (string)r.Element("trait") == "Psychopath") >= 1 && rules.Count(r => (string)r.Element("trait") == "Cannibal") >= 1, "the two extreme traits have their rules");
        foreach (var r in rules)
        {
            string name = (string)r.Element("defName")!;
            string vendetta = (string)r.Element("vendetta");
            if (vendetta != null) Check(vendetta == "unpaid" || vendetta == "over", $"{name}: a vow is unpaid or over");
            if (r.Element("onlyGiven") != null) Check((string)r.Element("change") == "Lose", $"{name}: only a loss undoes what Rimfolk gave");
            if (r.Element("calmSinceChange") != null) Check(((int?)r.Element("calmDays") ?? 0) > 0, $"{name}: calm since the change needs calm days");
        }

        // ---------- Vengeful: reined in, and a way out (the player's go, 17 November 2026) ----------
        {
            var vengeful = rules.Where(r => (string)r.Element("trait") == "VTE_Vengeful").ToList();
            var oath = vengeful.FirstOrDefault(r => ((string)r.Element("change") ?? "Gain") == "Gain");
            Check(oath != null, "vengeful still comes of an oath");
            if (oath != null)
            {
                Check((string)oath.Element("vendetta") == "unpaid" && ((int?)oath.Element("vendettaDays") ?? 30) >= 30, "only after a vow has stood 30 days unpaid");
                Check(((float?)oath.Element("chance") ?? 0.1f) < 0.08f, "at a lower chance than 8% a day");
                Check((bool?)oath.Element("noRival") == true, "never to someone with a rival in the colony");
                string meaning = (string)oath.Element("meaning") ?? "";
                Check(!meaning.Contains("roof") && meaning.Contains("rivals"), "the words say what the trait does: unhappy while a rival lives");
            }
            var outs = vengeful.Where(r => (string)r.Element("change") == "Lose").ToList();
            Check(outs.Count >= 2 && outs.All(r => (bool?)r.Element("onlyGiven") == true), "two ways out, each for a vengeful Rimfolk gave");
            Check(outs.All(r => ((int?)r.Element("gapDays") ?? 120) == 0), "neither waits out a gap since it came (the default 120 would hold it past day 220)");
            Check(outs.Any(r => (string)r.Element("vendetta") == "over"), "one when no vow of theirs stands");
            var calm = outs.FirstOrDefault(r => r.Element("calmDays") != null);
            Check(calm != null, "one after a long calm");
            if (calm != null)
            {
                int calmDays = (int?)calm.Element("calmDays") ?? 0;
                int need = calm.Element("marks")?.Elements("li").Sum(li => (int?)li.Element("count") ?? 1) ?? 0;
                Check(calm.Element("calmSinceChange") != null && calmDays >= 30 && calmDays <= 60, $"the calm is counted since it came, a season or more and at most a year ({calmDays} days)");
                Check(need >= 1 && need <= 2 && calm.Element("vendetta") == null, "with a good mark or two, and whatever the vow: a mechanoid hive never makes peace");
                // Avila: vengeful since day 100.01, no breakdown since day 27; Bautista since day 112.01, calm since day 50.
                Check(100.01f + calmDays < 160f && 112.01f + calmDays < 160f, "both of the player's colonists can be past the calm before day 160");
            }
        }
        Check(rules.Select(r => (string)r.Element("defName")).Distinct().Count() == rules.Count, "every rule has its own name");
        return cases;
    }
}
