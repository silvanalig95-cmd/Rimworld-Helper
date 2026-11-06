using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Phase 4 of the review, part 1, one name (the player's decision 6 of 6 November 2026): a colonist's one slow "known as"
/// (NameMath: a name held a good while, passing only to one who clearly outdoes the holder over a season, never back and
/// forth; the one name of those they hold; its line) and the colony's one "known for" (ColonyNameMath: one list of what it
/// is called, the name as a key, what changed, one sentence), with an older save's names held and nothing told on load.
/// </summary>
public static class OneNameTests
{
    private const int Day = NameMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "one name: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;

        // ================= A name holds =================

        C(NameMath.Lead(1.15f) == NameMath.ClearLead && NameMath.Lead(1.5f) == 1.5f, "clearly ahead is a quarter at least, more where a name asks for it");
        C(!NameMath.Ahead(110f, 100f, 1.15f) && !NameMath.Ahead(124f, 100f, 1.15f) && NameMath.Ahead(125f, 100f, 1.15f), "fifteen in a hundred ahead is no longer enough");
        C(!NameMath.Ahead(0f, 0f, 1.15f) && NameMath.Ahead(1f, 0f, 1.15f), "ahead of a holder at nothing by anything at all");

        // Given now: nobody can take it for two seasons.
        var h = NameMath.Given(100 * Day, null);
        C(h.since == 100 * Day && h.lastHolder == null && h.challenger == null, "a name given is held from now");
        for (int d = 101; d < 100 + NameMath.HoldDays; d++)
            C(NameMath.Look(h, d * Day, "A", 100f, "B", 1000f, 1.15f) == NameMove.Keep && h.challenger == null, $"day {d - 100} of the hold: kept, however far ahead the other is");
        // The hold over: a challenge begins, and must stand a whole season.
        int start = (100 + NameMath.HoldDays) * Day;
        C(NameMath.Look(h, start, "A", 100f, "B", 200f, 1.15f) == NameMove.Keep && h.challenger == "B" && h.challengeFrom == start && h.challengerAt == 200f && h.holderAt == 100f,
            "clearly ahead after the hold: a challenge begins, with both counts");
        C(NameMath.Look(h, start + 14 * Day, "A", 101f, "B", 240f, 1.15f) == NameMove.Keep && h.challenger == "B", "fourteen days of it: still the holder's");
        C(NameMath.Look(h, start + NameMath.ChallengeDays * Day, "A", 101f, "B", 240f, 1.15f) == NameMove.Pass, "a season clearly ahead, doing far more of the work: it passes");

        // The holder still at the work keeps it, and the season starts again.
        h = NameMath.Given(0, null);
        NameMath.Look(h, 40 * Day, "A", 100f, "B", 130f, 1.15f);
        var kept = NameMath.Look(h, 55 * Day, "A", 120f, "B", 155f, 1.15f);
        C(kept == NameMove.Keep && h.challengeFrom == 55 * Day && h.challengerAt == 155f && h.holderAt == 120f,
            "ahead all season but the holder did nearly as much (25 against 20): kept, a new season to show it");
        C(NameMath.Look(h, 70 * Day, "A", 121f, "B", 190f, 1.15f) == NameMove.Pass, "the next season the holder stopped and the other kept on: it passes");

        // Falling back under the lead ends the challenge.
        h = NameMath.Given(0, null);
        NameMath.Look(h, 40 * Day, "A", 100f, "B", 130f, 1.15f);
        NameMath.Look(h, 45 * Day, "A", 110f, "B", 130f, 1.15f);
        C(h.challenger == null, "a day not clearly ahead ends the challenge");
        NameMath.Look(h, 46 * Day, "A", 110f, "B", 140f, 1.15f);
        C(h.challenger == "B" && h.challengeFrom == 46 * Day, "and a new one starts from that day");
        // Another overtakes the challenger: their own season.
        NameMath.Look(h, 50 * Day, "A", 110f, "C", 150f, 1.15f);
        C(h.challenger == "C" && h.challengeFrom == 50 * Day, "a new challenger starts their own season");

        // Twice ahead: more of the work is enough.
        C(NameMath.Outdid(15f, 10f, false) && !NameMath.Outdid(14f, 10f, false) && NameMath.Outdid(11f, 10f, true) && !NameMath.Outdid(10f, 10f, true),
            "half again as much of the work, or more of it when twice ahead on the count");
        C(!NameMath.Outdid(0f, 0f, true) && NameMath.Outdid(1f, 0f, false), "doing nothing is never outdoing; any work against a holder who stopped is");

        // The one who lost it can't take it back for a year.
        h = NameMath.Given(100 * Day, "A");
        C(h.lastHolder == "A" && h.lostAt == 100 * Day, "who had it before is kept");
        NameMath.Look(h, 131 * Day, "B", 100f, "A", 500f, 1.15f);
        C(h.challenger == null, "the one who lost it is no challenger within the year");
        NameMath.Look(h, 160 * Day, "B", 100f, "A", 500f, 1.15f);
        C(h.challenger == "A", "a year on, they may earn it back like anyone");

        // Nobody ahead, or the holder is the best: nothing happens.
        h = NameMath.Given(0, null);
        C(NameMath.Look(h, 90 * Day, "A", 100f, "A", 100f, 1.15f) == NameMove.Keep && h.challenger == null, "the holder is the best: kept");
        C(NameMath.Look(h, 90 * Day, "A", 100f, null, 0f, 1.15f) == NameMove.Keep, "nobody else may hold it: kept");

        // An older save: held from the first look, nothing moves at once.
        var old = new NameHold();
        C(NameMath.Look(old, 128 * Day, "Alo", 60f, "Harper", 200f, 1.15f) == NameMove.Keep && old.since == 128 * Day && old.challenger == null,
            "an older save's name is held from its first look");

        // The save's two doctors: "the Healer" went Harper, Alo, Harper, Alo in 70 days on the old 15% margin.
        int MovesOld(Func<int, (float a, float b)> counts, int days)
        {
            string holder = "Harper";
            int moves = 0;
            for (int d = 46; d <= days; d++)
            {
                var (a, b) = counts(d);
                float mine = holder == "Harper" ? a : b, theirs = holder == "Harper" ? b : a;
                if (theirs >= mine * 1.15f)
                {
                    holder = holder == "Harper" ? "Alo" : "Harper";
                    moves++;
                }
            }
            return moves;
        }
        int MovesNew(Func<int, (float a, float b)> counts, int days)
        {
            string holder = "Harper";
            var hold = NameMath.Given(46 * Day, null);
            int moves = 0;
            for (int d = 47; d <= days; d++)
            {
                var (a, b) = counts(d);
                string other = holder == "Harper" ? "Alo" : "Harper";
                float mine = holder == "Harper" ? a : b, theirs = holder == "Harper" ? b : a;
                string best = theirs > mine ? other : holder;
                if (NameMath.Look(hold, d * Day, holder, mine, best, Math.Max(mine, theirs), 1.15f) == NameMove.Pass)
                {
                    hold = NameMath.Given(d * Day, holder);
                    holder = other;
                    moves++;
                }
            }
            return moves;
        }
        // Each takes a turn at the sickbeds for a season while the other mostly works elsewhere: wounds tended since day 46,
        // when Harper was first called the Healer (40 to Alo's 30).
        (float, float) Turns(int d)
        {
            float a = 40f, b = 30f;
            for (int i = 47; i <= d; i++)
            {
                bool harpers = (i / 15) % 2 == 0;
                a += harpers ? 3f : 0.3f;
                b += harpers ? 0.3f : 3f;
            }
            return (a, b);
        }
        int oldMoves = MovesOld(Turns, 128), newMoves = MovesNew(Turns, 128);
        C(oldMoves >= 3, $"the old margin passes the name back and forth between two doctors who take turns ({oldMoves} moves)");
        C(newMoves == 0, $"the new rule keeps it with its holder while both still do the work ({newMoves} moves)");
        // One doctor who stops: the other earns it, once.
        (float, float) Stops(int d) => (60f + Math.Min(d, 60) * 1f, 40f + d * 1.6f);
        int stopMoves = MovesNew(Stops, 300);
        C(stopMoves == 1, $"a holder who stops doing the work loses it to the one who keeps on, once in five years ({stopMoves})");

        // ================= Saving =================

        var full = new NameHold { since = 12 * Day, challenger = "Human2077", challengeFrom = 40 * Day, challengerAt = 123.5f, holderAt = 99.25f, lastHolder = "Human686717", lostAt = 12 * Day };
        var back = NameMath.Unpack(NameMath.Pack(full));
        C(back != null && back.since == full.since && back.challenger == full.challenger && back.challengeFrom == full.challengeFrom && back.challengerAt == full.challengerAt
          && back.holderAt == full.holderAt && back.lastHolder == full.lastHolder && back.lostAt == full.lostAt, "a hold comes back from the save as it went in");
        var bare = NameMath.Unpack(NameMath.Pack(NameMath.Given(5, null)));
        C(bare != null && bare.challenger == null && bare.lastHolder == null && bare.since == 5, "a hold with nobody in it comes back empty");
        C(NameMath.Unpack(null) == null && NameMath.Unpack("") == null && NameMath.Unpack("1|2|3") == null && NameMath.Unpack("x||0|0|0||0") == null,
            "a line that can't be read is no hold (the name is held from its next look)");

        // ================= A colonist's one name =================

        var hector = new List<NameMath.Held>
        {
            new NameMath.Held("Council_Epithet_Tamer", 64 * Day, false), new NameMath.Held("Council_Epithet_Builder", 7 * Day, false),
            new NameMath.Held("Council_Epithet_Tinker", 104 * Day, false)
        };
        C(NameMath.Chief(hector) == 1, "Hector the Builder: the name held longest, not the newest");
        var harper = new List<NameMath.Held>
        {
            new NameMath.Held("Council_Epithet_Bottomless", 2 * Day, true), new NameMath.Held("Council_Epithet_OldHand", 120 * Day, false),
            new NameMath.Held("Council_Epithet_Vault", 42 * Day, false)
        };
        C(NameMath.Chief(harper) == 2, "Harper the Vault: a proud name before a dubious one held longer");
        C(NameMath.Chief(new List<NameMath.Held> { new NameMath.Held("Council_Epithet_Kindling", 5, true) }) == 0, "a dubious name alone is still their name");
        C(NameMath.Chief(new List<NameMath.Held> { new NameMath.Held("B", -1, false), new NameMath.Held("A", 50, false) }) == 1, "a name not yet looked at counts as the newest");
        C(NameMath.Chief(new List<NameMath.Held> { new NameMath.Held("B", 9, false), new NameMath.Held("A", 9, false) }) == 1, "a tie by name, the same each time");
        C(NameMath.Chief(new List<NameMath.Held>()) == -1 && NameMath.Chief(null) == -1, "no name, none");

        C(NameMath.SameLine("<color=#D09B61FF>Alo</color> became known as the Healer.", "Alo became known as the Healer.")
          && NameMath.SameLine("alo became known as the Healer.", "Alo became known as the Healer.")
          && !NameMath.SameLine("Alo became known as Goodhands.", "Alo became known as the Healer.") && !NameMath.SameLine("x", null),
            "an older save's name is found in the history by its line, colour and case aside");

        C(NameMath.LineKey(true, null, false) == "CC_Name_Own_Home" && NameMath.LineKey(true, "epithet", false) == "CC_Name_Own_Carries"
          && NameMath.LineKey(true, "contest", false) == "CC_Name_Own_Full" && NameMath.LineKey(true, "executed", true) == "CC_Name_Own_HomeAndBad",
            "the colony's name with what the peoples say, in one line");
        C(NameMath.LineKey(false, "contest", false) == "CC_Renown_Own_Line" && NameMath.LineKey(false, "executed", true) == "CC_Renown_Own_LineBad"
          && NameMath.LineKey(false, null, false) == null, "renown's own line without a name at home; nothing with neither");
        foreach (var k in new[] { "CC_Name_Own_Home", "CC_Name_Own_Carries", "CC_Name_Own_Full", "CC_Name_Own_HomeAndBad", "CC_Name_Own_Also", "CC_Renown_Own_Line", "CC_Renown_Own_LineBad" })
            C(keyed.TryGetValue(k, out var v) && v.EndsWith(".") && !Regex.IsMatch(v, @"^\{"), $"{k} has words, a sentence that doesn't start with a name");
        C(keyed["CC_Name_Own_Full"].Contains("{NAME}") && keyed["CC_Name_Own_Full"].Contains("{KNOWNAS}") && keyed["CC_Name_Own_Full"].Contains("{RENOWN}"),
            "the full line says the name, the deed and how far it carries");

        // ================= The colony's one name =================

        var order = ColonyNameMath.Order(new[]
        {
            new ColonyNameMath.Called(ColonyNameMath.TraitId("Council_Trait_Walled"), 1, false), new ColonyNameMath.Called(ColonyNameMath.WayId("Tradition", true), 1, true),
            new ColonyNameMath.Called(ColonyNameMath.WayId("Mercy", true), 2, true), new ColonyNameMath.Called(ColonyNameMath.TraitId("Council_Trait_Learned"), 1, false),
            new ColonyNameMath.Called(ColonyNameMath.WayId("Generosity", true), 1, true), new ColonyNameMath.Called("way:Toil+", 0, true)
        }).Select(c => c.id).ToList();
        C(order.SequenceEqual(new[] { "way:Mercy+", "way:Tradition+", "way:Generosity+", "trait:Council_Trait_Walled", "trait:Council_Trait_Learned" }),
            "one list: what it is known for far and wide first, then the council's ways, then what the work made it, each in its own order");
        C(ColonyNameMath.WayId("Mercy", false) == "way:Mercy-", "a way's other pole is its own id");

        var before = new[] { new ColonyNameMath.Called("way:Tradition+", 1, true), new ColonyNameMath.Called("way:Generosity+", 1, true), new ColonyNameMath.Called("trait:Council_Trait_Walled", 1, false) };
        string k0 = ColonyNameMath.Key(before, "None");
        C(k0 == "called=way:Tradition+:1,way:Generosity+:1,trait:Council_Trait_Walled:1;for=None", "the name as a key");
        C(ColonyNameMath.Diff(k0, k0).Empty, "the same name: nothing to tell");
        var gained = ColonyNameMath.Diff(k0, ColonyNameMath.Key(before.Concat(new[] { new ColonyNameMath.Called("trait:Council_Trait_Learned", 1, false) }), "None"));
        C(gained.started.SequenceEqual(new[] { "trait:Council_Trait_Learned" }) && gained.stopped.Count == 0 && gained.forNow == null, "a trait gained: started calling it learned");
        var lost = ColonyNameMath.Diff(k0, ColonyNameMath.Key(before.Take(2), "None"));
        C(lost.stopped.SequenceEqual(new[] { "trait:Council_Trait_Walled" }) && lost.started.Count == 0, "a trait lost: stopped calling it walled");
        var renowned = new[] { new ColonyNameMath.Called("way:Tradition+", 2, true), before[1], before[2] };
        var wider = ColonyNameMath.Diff(k0, ColonyNameMath.Key(renowned, "None"));
        C(wider.wider.SequenceEqual(new[] { "way:Tradition+" }) && wider.started.Count == 0, "a way known far and wide: travelers far and wide call it that now");
        C(ColonyNameMath.Diff(ColonyNameMath.Key(renowned, "None"), k0).Empty, "from far and wide back to known: still what it is called, nothing to tell (the news says it)");
        var forNow = ColonyNameMath.Diff(k0, ColonyNameMath.Key(before, "Healers"));
        C(forNow.forNow == "Healers" && forNow.forGone == null, "known for good doctors beyond its walls now");
        var forGone = ColonyNameMath.Diff(ColonyNameMath.Key(before, "Healers"), k0);
        C(forGone.forGone == "Healers" && forGone.forNow == null, "no longer known for good doctors");
        C(ColonyNameMath.Diff(k0, ColonyNameMath.Key(before, null)).Empty && ColonyNameMath.Diff(k0, ColonyNameMath.Key(null, "None")).Empty
          && ColonyNameMath.Diff(ColonyNameMath.Key(null, null), k0).Empty, "a part not read (a setting off, renown not yet looked at) changes nothing");
        C(ColonyNameMath.Merge(k0, ColonyNameMath.Key(null, "Healers")) == ColonyNameMath.Key(before, "Healers"), "a part not read is kept as it was");
        C(ColonyNameMath.Diff(null, k0).Empty && ColonyNameMath.Diff("", k0).Empty, "an older save's first look tells nothing");

        var both = ColonyNameMath.Diff(k0, ColonyNameMath.Key(new[] { before[0], new ColonyNameMath.Called("trait:Council_Trait_Learned", 1, false) }, "Healers"));
        var clauses = ColonyNameMath.Clauses(both);
        C(clauses.Select(c => c.key).SequenceEqual(new[] { "CC_Name_Changed_Started", "CC_Name_Changed_Stopped_It", "CC_Name_Changed_For_It" }),
            "one sentence: started, then stopped and known for, speaking of the colony as it after the first");
        C(ColonyNameMath.Clauses(ColonyNameMath.Diff(k0, k0)).Count == 0, "no change, no sentence");
        foreach (var k in ColonyNameMath.ClauseKeys())
            C(keyed.TryGetValue(k, out var v) && v.Length > 0 && char.IsLower(v[0]) && !v.EndsWith("."), $"{k}: a clause in lower case, joined into the sentence");
        C(keyed.TryGetValue("CC_Name_Changed_Line", out var line) && line == "{CLAUSES}.", "the clauses make one sentence");
        foreach (var k in ColonyNameMath.ClauseKeys().Where(k => k.Contains("_For")))
            C(keyed[k].Contains("{KNOWNFOR}"), $"{k} says what it is known for");
        foreach (var k in ColonyNameMath.ClauseKeys().Where(k => !k.Contains("_For")))
            C(keyed[k].Contains("{WORDS}"), $"{k} says what it is called");

        C(ColonyNameMath.CardKey(true, true, true) == "CC_Name_Card_CalledFor" && ColonyNameMath.CardKey(true, false, true) == "CC_Name_Card_Called"
          && ColonyNameMath.CardKey(false, true, true) == "CC_Name_Card_For" && ColonyNameMath.CardKey(false, false, true) == "CC_Name_Card_Bare"
          && ColonyNameMath.CardKey(true, true, false) == "CC_Name_Card_CalledHome" && ColonyNameMath.CardKey(false, false, false) == "CC_Name_Card_BareHome",
            "the card's first line by what there is");
        foreach (var k in new[] { "CC_Name_Card_CalledFor", "CC_Name_Card_Called", "CC_Name_Card_For", "CC_Name_Card_Bare" })
            C(keyed.TryGetValue(k, out var v) && v.Contains("{RENOWN}") && v.EndsWith("."), $"{k}: what it is known for and how far its name carries, in one sentence");
        foreach (var k in new[] { "CC_Name_Card_CalledHome", "CC_Name_Card_BareHome" })
            C(keyed.TryGetValue(k, out var v) && !v.Contains("{RENOWN}") && v.EndsWith("."), $"{k}: with renown off, nothing of its renown");
        foreach (var k in new[] { "CC_Name_Card_CalledFor", "CC_Name_Card_Called", "CC_Name_Card_CalledHome" })
            C(keyed[k].Contains("{CALLED}"), $"{k} says what it is called");

        // ================= Where it is read =================

        string ep = Read("Epithets/Epithets.cs");
        C(ep.Contains("NameMath.Look(hold,") && !ep.Contains("* def.margin) continue;"), "the day's look asks the slow rule, never the old margin");
        C(ep.Contains("if (Chief(p) == def) comp.AddChronicle("), "the chronicle writes a name's line only when it is the one they are known by");
        C(ep.Contains("\"Rimfolk_Journal_EpithetLost\"") && File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Journal_OneName.xml")).Contains("<defName>Rimfolk_Journal_EpithetLost</defName>"),
            "a name lost is written once in the holder's own journal");
        C(ep.Contains("Scribe_Collections.Look(ref holdsSaved, \"holds\", LookMode.Value, LookMode.Value);") && ep.Contains("private Dictionary<string, string> holdsSaved;"),
            "the holds are saved through a field, packed");
        C(ep.Contains("SinceInHistory(def, holder)") && ep.Contains("NameMath.SameLine(e.line, given)"), "an older save's name is held from the day the history wrote it");
        var allCode = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToDictionary(f => Path.GetRelativePath(src, f).Replace('\\', '/'), File.ReadAllText);
        C(!allCode.Values.Any(t => t.Contains("TitlesOf(")), "no reader joins every name a colonist holds: each reads the one");
        foreach (var rel in new[] { "UI/Cards/Cards.cs", "UI/ITab_Pawn_Story.cs", "UI/PeopleTable.cs", "UI/WishesPanel.cs", "Annals.cs", "Secrets/Vendettas.cs" })
            C(allCode[rel].Contains("epithets.NameOf("), $"{rel} shows the one name");
        C(allCode["Social/LetterNotes.cs"].Contains("comp.epithets.Chief(p)?.label"), "the death letter names the one name");
        C(allCode["Diplomacy/Renown.cs"].Contains("ep.Chief(p)") && allCode["Diplomacy/Renown.cs"].Contains("details[\"epithet\"] = chief.label"),
            "talk and letters abroad say the same name (\"the one the colony calls ...\")");
        string ka = allCode["Epithets/KnownAs.cs"];
        C(ka.Contains("CardSections.RegisterLines(CardKind.Colonist") && ka.Contains("Reputations.SidesLine(p)") && ka.Contains("\"CC_Name_Own_Also\""),
            "a colonist's card: the one line, then the other names and the sides of their name under it");
        C(!allCode["Diplomacy/RenownPanels.cs"].Contains("CardKind.Colonist") && !allCode["Diplomacy/RenownPanels.cs"].Contains("CardSections."),
            "renown no longer puts a second \"known as\" on the card, nor a second card on a home's");
        C(allCode["UI/RenownPage.cs"].Contains("KnownAs.Line(k)"), "People → Renown lists each by the one name");

        string cn = allCode["Character/ColonyName.cs"];
        C(cn.Contains("CardSections.HomeLines(") && allCode["UI/WaysPanel.cs"].Contains("ColonyName.Lines()") && allCode["UI/OverviewPanel.cs"].Contains("ColonyName.Main()")
          && allCode["UI/ITab_Pawn_Culture.cs"].Contains("ColonyName.CalledWords()"), "the colony's card: on Council → Ways, a home's card, the Overview and the Culture tab");
        C(cn.Contains("RenownPanels.From(r)") && cn.Contains("Remembered()") && cn.Contains("Choices()"), "the card has the deeds behind it and what each people remembers");
        string ch = allCode["Character/Character.cs"], tr = allCode["Character/ColonyTraits.cs"];
        C(!ch.Contains("AddChronicle(") && !tr.Contains("AddChronicle("), "the ways and the traits write no chronicle line of their own");
        C(ch.Contains("ColonyName.Look(comp);") && ch.Contains("if (Check(comp)) ColonyName.Look(comp);"), "the colony's name is looked at after the ways move and at the end of the day");
        C(ch.Contains("Scribe_Values.Look(ref nameKey, \"nameKey\");") && cn.Contains("if (ch.nameKey == null)"), "its name saved; an older save's first look tells nothing");
        C(allCode["Diplomacy/Renown.cs"].Contains("ColonyName.Settle();"), "renown's band moved is the one sentence, with what it is known for: not told twice");
        C(ch.Contains("\"CC_Name_Ways_Narrower\"") && keyed.TryGetValue("CC_Name_Ways_Narrower", out var narrower) && narrower.Contains("{0}"),
            "renowned to known: the news says the neighbors still call it that");
        foreach (var rel in new[] { "Happenings/Workers_Living.cs", "Happenings/Workers_More.cs" })
            C(allCode[rel].Contains("ColonyName.CalledWords()") && !allCode[rel].Contains("character.Describe()"), $"{rel}: the colony called by its one list");

        return n;
    }
}
