using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Each colonist's own belief in the gods (the player, 14 November 2026: "pawns should have and develop kind of a relationship
/// in a way, or rather a belief to gods ... the more they pray and interact to a god, that they kind of, yeah, get a stronger
/// belief in that god", "pawn belief should be reflected in book and culture tab", "gods effect should be noticeable";
/// docs/gods-next-proposals.md, section E, round 3): what draws them (never their kin), how it grows and fades, dread, being
/// won over, whom they turn to, the small rite of each god, rival devotees, the words it shows in, and the seams it is fed by.
/// </summary>
public static class PawnGodsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "each one's gods: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keys = new HashSet<string>(Regex.Matches(keyedAll, @"<([A-Za-z0-9_]+)>").Cast<Match>().Select(m => m.Groups[1].Value));
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => (f, doc: XDocument.Load(f))).ToList();

        // ================= The words =================

        C(PawnGodsMath.Word(0f, 0f) == BeliefWord.None && PawnGodsMath.Word(PawnGodsMath.LeansFrom, 0f) == BeliefWord.Leans
          && PawnGodsMath.Word(PawnGodsMath.HoldsFrom, 0f) == BeliefWord.Holds && PawnGodsMath.Word(PawnGodsMath.DevotedFrom, 0f) == BeliefWord.Devoted
          && PawnGodsMath.Word(PawnGodsMath.FerventFrom, 0f) == BeliefWord.Fervent, "leaning toward, holding to, devoted, wholly given, by how strongly");
        C(PawnGodsMath.LeansFrom < PawnGodsMath.HoldsFrom && PawnGodsMath.HoldsFrom < PawnGodsMath.DevotedFrom && PawnGodsMath.DevotedFrom < PawnGodsMath.FerventFrom
          && PawnGodsMath.FerventFrom < PawnGodsMath.Max, "the steps in order");
        C(PawnGodsMath.Word(40f, 50f) == BeliefWord.Dread && PawnGodsMath.Word(10f, 25f) == BeliefWord.Uneasy && PawnGodsMath.Word(60f, 30f) == BeliefWord.Devoted,
            "dread shows where it is as strong as their belief (they still believe, in fear); else their belief");
        C(PawnGodsMath.Fears(BeliefWord.Dread) && PawnGodsMath.Fears(BeliefWord.Uneasy) && !PawnGodsMath.Fears(BeliefWord.Devoted), "the words of fear");
        C(PawnGodsMath.Dreads(20f, 30f) && !PawnGodsMath.Dreads(40f, 30f) && !PawnGodsMath.Dreads(0f, 10f), "dread rules only where it is as strong as belief, and real");
        foreach (BeliefWord w in Enum.GetValues(typeof(BeliefWord)))
        {
            C(keys.Contains("CC_Belief_Word_" + w), $"words for {w} on the Culture tab");
            C(keys.Contains("CC_Belief_Short_" + w), $"words for {w} in the book");
        }
        C(Regex.Match(keyedAll, @"<CC_Belief_Word_Devoted>([^<]*)<").Groups[1].Value == "devoted to {GOD}"
          && Regex.Match(keyedAll, @"<CC_Belief_Word_Leans>([^<]*)<").Groups[1].Value == "leans toward {GOD}"
          && Regex.Match(keyedAll, @"<CC_Belief_Word_Dread>([^<]*)<").Groups[1].Value == "in dread of {GOD}"
          && Regex.Match(keyedAll, @"<CC_Belief_Scoffer>([^<]*)<").Groups[1].Value == "Doubts them all", "the player's own words: devoted to, leans toward, in dread of, doubts them all");

        // ================= What draws them =================

        var kind = new BeliefSketch();
        kind.traits.Add(("Kind", 0, "kind"));
        var d1 = PawnGodsMath.Draw(kind);
        C(d1.Count == 1 && d1[0].god == "Ennu" && d1[0].amount >= PawnGodsMath.LeansFrom && d1[0].kind == DrawKind.Trait && d1[0].what == "kind",
            "a kind heart draws them to the Guest, and says so");
        var many = new BeliefSketch { xenotype = "VRE_Fungoid", xenotypeLabel = "fungoid" };
        many.traits.Add(("Kind", 0, "kind"));
        many.traits.Add(("Rimfolk_BornCook", 0, "born cook"));
        many.traits.Add(("VTE_Wanderlust", 0, "wanderlust"));
        many.passions.Add(("Social", 2, "social"));
        many.passions.Add(("Cooking", 2, "cooking"));
        many.backstories.Add(("UrbworldInnkeeper innkeeper", "innkeeper"));
        var d2 = PawnGodsMath.Draw(many);
        C(d2.First().god == "Ennu" && d2.First().amount == PawnGodsMath.DrawCap, "character alone takes them only so far (DrawCap)");
        C(PawnGodsMath.DrawCap >= PawnGodsMath.HoldsFrom && PawnGodsMath.DrawCap < PawnGodsMath.DevotedFrom, "past holding to a god, short of devotion: devotion comes from what they do");
        C(d2.Any(x => x.god == "Morel" && x.kind == DrawKind.Xenotype), "what they are draws them (a fungoid to the Soft Mother)");
        C(d2.Count <= PawnGodsMath.MaxHeld && d2.Zip(d2.Skip(1), (a, b) => a.amount >= b.amount).All(x => x), "strongest first, a few at most");
        var minor = new BeliefSketch();
        minor.passions.Add(("Mining", 1, "mining"));
        var major = new BeliefSketch();
        major.passions.Add(("Mining", 2, "mining"));
        C(PawnGodsMath.Draw(minor).Single().amount * 2f == PawnGodsMath.Draw(major).Single().amount && PawnGodsMath.Draw(major).Single().god == "Smoulder",
            "a burning passion draws twice a minor one (mining to the Coil)");
        var scoffer = new BeliefSketch { scoffer = true };
        scoffer.traits.Add(("Kind", 0, "kind"));
        var devout = new BeliefSketch { devout = true };
        devout.traits.Add(("Kind", 0, "kind"));
        C(PawnGodsMath.Draw(scoffer).Single().amount < PawnGodsMath.LeansFrom && PawnGodsMath.Draw(devout).Single().amount > d1[0].amount,
            "a scoffer is drawn far less (doubts them all), the devout a little more");
        var past = new BeliefSketch();
        past.backstories.Add(("UrbworldMiner miner", "miner"));
        past.backstories.Add(("Apprentice apprentice police", "apprentice"));
        var dp = PawnGodsMath.Draw(past);
        C(dp.Count == 1 && dp[0].god == "Smoulder" && dp[0].kind == DrawKind.Backstory, "their past by its words, each word from its start (a miner, not 'ice' in an apprentice)");
        C(PawnGodsMath.Words("UrbworldMiner42").SequenceEqual(new[] { "urbworld", "miner" }) && PawnGodsMath.Words("glitterworld surgeon").SequenceEqual(new[] { "glitterworld", "surgeon" }),
            "a past's words, a defName's capitals split");
        var people = new BeliefSketch { people = "the League", peopleGods = new List<string> { "Ducat", "Ennu", "Pip", "Hush" } };
        var dpe = PawnGodsMath.Draw(people);
        C(dpe.Count == 3 && dpe.All(x => x.kind == DrawKind.People && x.what == "the League" && x.amount == PawnGodsMath.PeopleDraw), "the gods of the people they came from, three at most");
        var faith = PawnGodsMath.FaithLean(Scores((GodDomain.Wild, 3f), (GodDomain.Machine, 0.5f)),
            new[] { ("Hollin", GodDomain.Wild, GodDomain.Harvest), ("Bellwether", GodDomain.Hunt, GodDomain.Wild), ("Pip", GodDomain.Machine, GodDomain.Craft) });
        C(faith["Hollin"] == 1f && Math.Abs(faith["Bellwether"] - 0.5f) < 0.001f && !faith.ContainsKey("Pip"), "their faith's memes lean them by domain, a real lean only");
        var bond = new BeliefSketch { catBond = true, animalBond = true };
        var db = PawnGodsMath.Draw(bond);
        C(db.Any(x => x.god == "Mog" && x.kind == DrawKind.Bond) && db.Any(x => x.god == "Bellwether"), "a bond with a cat draws them to Tamiu, with a beast to the Old Horn");
        C(PawnGodsMath.Draw(new BeliefSketch()).Count == 0 && PawnGodsMath.Draw(null).Count == 0, "nothing draws someone with nothing: they fall back on the moment's domain");

        // Never from kin: nothing reads a family.
        var sketchFields = typeof(BeliefSketch).GetFields().Select(f => f.Name.ToLowerInvariant()).ToList();
        C(!sketchFields.Any(f => f.Contains("parent") || f.Contains("kin") || f.Contains("family") || f.Contains("mother") || f.Contains("father") || f.Contains("child")),
            "the sketch has nothing of parents or kin");
        string pawnGods = Read("Gods/Belief/PawnGods.cs"), acts = Read("Gods/Belief/PawnGodsActs.cs"), book = Read("Gods/Belief/PawnGodsBook.cs"), mathSrc = Read("Gods/Belief/PawnGodsMath.cs");
        string beliefCode = pawnGods + acts + book;
        C(!beliefCode.Contains("familyByBlood") && !beliefCode.Contains("GetRelations(") && !beliefCode.Contains("PawnRelationDefOf.Parent") && !beliefCode.Contains("PawnRelationDefOf.Child")
          && pawnGods.Contains("rel?.def != PawnRelationDefOf.Bond"), "never inherited: the only relation read is a bond with an animal");

        // Every name in the tables is the player's: active rows of docs/player-mods.
        var traits = Active(Path.Combine(root, "docs", "player-mods", "traits.tsv"));
        var xenos = Active(Path.Combine(root, "docs", "player-mods", "xenotypes.tsv"));
        foreach (var t in PawnGodsMath.TraitDraws.Select(x => x.trait).Distinct())
            C(traits.Contains(t), $"trait {t} is in the player's enabled mods");
        foreach (var x in PawnGodsMath.XenotypeDraws.Select(x => x.xeno).Distinct())
            C(xenos.Contains(x), $"xenotype {x} is in the player's enabled mods");
        var skills = new HashSet<string> { "Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants", "Animals", "Crafting", "Artistic", "Medicine", "Social", "Intellectual" };
        C(PawnGodsMath.PassionDraws.All(x => skills.Contains(x.skill)) && skills.All(s => s == "Melee" || PawnGodsMath.WorkDomain(s) != GodDomain.None),
            "the game's own skills, and each has a domain of work");
        var gods = new HashSet<string>(GodLoreMath.Keys);
        C(PawnGodsMath.TraitDraws.All(x => gods.Contains(x.god)) && PawnGodsMath.PassionDraws.All(x => gods.Contains(x.god)) && PawnGodsMath.XenotypeDraws.All(x => gods.Contains(x.god))
          && PawnGodsMath.BackstoryDraws.All(x => gods.Contains(x.god)), "every draw is toward one of the fourteen");
        C(GodLoreMath.Keys.All(k => PawnGodsMath.TraitDraws.Any(x => x.god == k) || PawnGodsMath.PassionDraws.Any(x => x.god == k)
                                                                       || PawnGodsMath.XenotypeDraws.Any(x => x.god == k) || PawnGodsMath.BackstoryDraws.Any(x => x.god == k)),
            "each of the fourteen draws someone by who they are");

        // ================= How it grows and fades =================

        C(PawnGodsMath.Grow(0f, 10f) == 10f && PawnGodsMath.Grow(80f, 10f) - 80f < 4f && PawnGodsMath.Grow(80f, 10f) > 80f, "each step smaller the more they hold already");
        C(PawnGodsMath.Grow(50f, -10f) == 40f && PawnGodsMath.Grow(5f, -10f) == 0f && PawnGodsMath.Grow(99f, 50f) <= PawnGodsMath.Max, "a loss in full, never past the bounds");
        C(PawnGodsMath.Amount(BeliefEvent.PrayedAtShrine) > PawnGodsMath.Amount(BeliefEvent.Prayed) && PawnGodsMath.Amount(BeliefEvent.Answered) > PawnGodsMath.Amount(BeliefEvent.Credited)
          && PawnGodsMath.Amount(BeliefEvent.GreatVowKept) > PawnGodsMath.Amount(BeliefEvent.VowKept) && PawnGodsMath.Amount(BeliefEvent.SawPlain) > PawnGodsMath.Amount(BeliefEvent.Saw)
          && PawnGodsMath.Amount(BeliefEvent.Saw) > PawnGodsMath.Amount(BeliefEvent.SawOmen) && PawnGodsMath.Amount(BeliefEvent.Offered) > 0f && PawnGodsMath.Amount(BeliefEvent.Festival) > 0f
          && PawnGodsMath.Amount(BeliefEvent.Told) > 0f && PawnGodsMath.Amount(BeliefEvent.OwnRite) > 0f && PawnGodsMath.Amount(BeliefEvent.Dreamt) > 0f && PawnGodsMath.Amount(BeliefEvent.Gift) > 0f,
            "prayer, an answer, offerings, vows, festivals, their own rite, moments lived through, dreams, tales: each grows it, the stronger the more");
        C(PawnGodsMath.Amount(BeliefEvent.Unanswered) < 0f && PawnGodsMath.Amount(BeliefEvent.Crisis) < PawnGodsMath.Amount(BeliefEvent.Unanswered)
          && PawnGodsMath.Amount(BeliefEvent.VowBroken) < 0f && PawnGodsMath.Amount(BeliefEvent.Mocked) < 0f && PawnGodsMath.Amount(BeliefEvent.TurnedFrom) < PawnGodsMath.Amount(BeliefEvent.Crisis),
            "a prayer unanswered, a crisis of faith, a broken vow, a scoffer's word fade it; turning away the most");
        C(PawnGodsMath.Fade(40f, 30f, 5f) == 40f && PawnGodsMath.Fade(40f, 30f, 20f) == 40f - PawnGodsMath.FadePerDay && PawnGodsMath.Fade(15.2f, 30f, 20f) == 15f
          && PawnGodsMath.Fade(10f, 30f, 20f) == 10f, "unused, it fades a little a day, down to half of what their character drew, never up to it");
        float h = 60f;
        for (int day = 0; day < 200; day++) h = PawnGodsMath.Fade(h, 0f, 30f);
        C(h == 0f && PawnGodsMath.FadePerDay * 60f < 40f, "a god never used again is let go over months, not days");
        C(PawnGodsMath.FadeDread(50f, 3f) == 50f && PawnGodsMath.FadeDread(50f, 20f) < 50f && PawnGodsMath.DreadKeepDays > PawnGodsMath.NeglectDays, "dread lingers longer");
        C(PawnGodsMath.DreadAmount(true, false, 0f) > PawnGodsMath.DreadAmount(false, true, 0f) && PawnGodsMath.DreadAmount(false, true, 0f) > PawnGodsMath.DreadAmount(false, false, 0f)
          && PawnGodsMath.DreadAmount(true, false, 80f) > PawnGodsMath.DreadAmount(true, false, 0f) && PawnGodsMath.DreadAmount(true, false, 0f) >= PawnGodsMath.UneasyFrom,
            "a god's anger on them leaves them uneasy at once, beside them less, only seen least; a believer fears it the more");

        // ================= Won over =================

        C(!PawnGodsMath.WonOver(50f, 0f, 30f, 1f, false, 0f), "never won over to the god already their first");
        C(!PawnGodsMath.WonOver(20f, 40f, 30f, 1f, false, 0f), "never won over to a god they dread");
        C(PawnGodsMath.WonOver(10f, 0f, 40f, 1f, false, 0.2f) && !PawnGodsMath.WonOver(10f, 0f, 40f, 1f, false, 0.4f) && !PawnGodsMath.WonOver(10f, 0f, 40f, 0f, false, 0f),
            "a strong moment wins someone over now and then; nothing that isn't one does");
        C(PawnGodsMath.Strength(BeliefEvent.SawPlain) > PawnGodsMath.Strength(BeliefEvent.SawBlessing) && PawnGodsMath.Strength(BeliefEvent.Prayed) == 0f
          && PawnGodsMath.Strength(BeliefEvent.Gift) == 1f, "a god seen plainly or a gift the strongest, a prayer nothing");
        C(PawnGodsMath.WonOver(2f, 0f, 0f, 1f, true, 0.05f) && !PawnGodsMath.WonOver(2f, 0f, 0f, 1f, true, 0.2f), "a scoffer far more rarely");
        C(PawnGodsMath.WonOverHold(10f, 40f) > 40f && PawnGodsMath.WonOverHold(5f, 0f) >= PawnGodsMath.HoldsFrom, "won over, it is their first, held to at least");
        C(acts.Contains("r.wonOver = Now;") && Read("Gods/Worship/Worship.cs").Contains("if (!noReligion && PawnGods.WonOver(p)) return false;"),
            "a scoffer won over scoffs no more (unless their faith despises religion)");
        C(acts.Contains("Speech.Say(p, line)") && acts.Contains("journals.WriteText(p, journal)") && acts.Contains("GodLore.Believes(p, g, line)") && acts.Contains("\"CC_Belief_WonOver_News\""),
            "won over: said in a bubble, in their journal, the news and the colony's book");

        // ================= Whom they turn to =================

        C(PawnGodsMath.Choose(new List<(float, float, bool)>(), false) == -1 && PawnGodsMath.Choose(new[] { (5f, 0f, false) }, false) == -1, "nothing held: the fallback by domain");
        C(PawnGodsMath.Choose(new[] { (20f, 0f, false), (40f, 0f, false) }, false) == 1, "their first god, the one they hold most");
        C(PawnGodsMath.Choose(new[] { (40f, 50f, false), (20f, 0f, false) }, false) == 1, "never a god they dread");
        C(PawnGodsMath.Choose(new[] { (40f, 0f, false), (15f, 0f, true) }, true) == 1 && PawnGodsMath.Choose(new[] { (70f, 0f, false), (15f, 0f, true) }, true) == 0,
            "for the moment's domain: one of it they hold, unless they are devoted to their first");
        C(PawnGodsMath.Choose(new[] { (40f, 0f, false) }, true) == 0 && PawnGodsMath.Choose(new[] { (20f, 0f, false) }, true) == -1,
            "with none of its domain: their first if they hold to it, else the domain's own");
        string gods2 = Read("Gods/Gods.cs"), worship = Read("Gods/Worship/Worship.cs");
        C(gods2.Contains("var held = PawnGods.Held(p, about);") && gods2.Contains("if (about == GodDomain.None) about = PawnGods.WorkDomain(p);")
          && worship.Contains("var held = PawnGods.Held(p, about);\n            if (held != null) return held;"),
            "the god picked by id number replaced: their own belief first, then the moment's domain, then their work's");
        C(pawnGods.Contains("if (!create || !Worship.Ours(p)) return null;") && pawnGods.Contains("!Worship.Ours(r.pawn)"), "the colony's own people only: no guest or prisoner holds anything here");

        // ================= The small rite =================

        foreach (var k in GodLoreMath.Keys)
            C(PawnGodsMath.RiteOf(k) != null, $"{k}'s devotees have a small rite of their own");
        C(PawnGodsMath.Rites.Values.Distinct().Count() == GodLoreMath.Keys.Length, "one rite a god, each its own");
        C(PawnGodsMath.RiteOf("Mog") == BeliefRite.Grave && PawnGodsMath.RiteHour(BeliefRite.Grave, 18, 15f) && !PawnGodsMath.RiteHour(BeliefRite.Grave, 10, 15f),
            "Tamiu's: by a grave at dusk");
        C(PawnGodsMath.RiteHour(BeliefRite.Sunrise, 6, 15f) && !PawnGodsMath.RiteHour(BeliefRite.Sunrise, 14, 15f) && PawnGodsMath.RiteHour(BeliefRite.Sky, 18, 15f)
          && PawnGodsMath.RiteHour(BeliefRite.Dark, 20, 15f) && !PawnGodsMath.RiteHour(BeliefRite.Dark, 12, 15f) && PawnGodsMath.RiteHour(BeliefRite.Moon, 1, 15f),
            "each at its hour: the sunrise at dawn, the sky at dusk, the dark and the moon at night");
        C(PawnGodsMath.RiteHour(BeliefRite.Cold, 12, -5f) && !PawnGodsMath.RiteHour(BeliefRite.Cold, 12, 20f) && PawnGodsMath.RiteHour(BeliefRite.Cold, 21, 20f),
            "Hush's silence in the cold, else at nightfall");
        C(PawnGodsMath.RiteChance(BeliefWord.Leans) == 0f && PawnGodsMath.RiteChance(BeliefWord.Dread) == 0f && PawnGodsMath.RiteChance(BeliefWord.Holds) > 0f
          && PawnGodsMath.RiteChance(BeliefWord.Fervent) > PawnGodsMath.RiteChance(BeliefWord.Devoted) && PawnGodsMath.RiteChance(BeliefWord.Devoted) > PawnGodsMath.RiteChance(BeliefWord.Holds),
            "kept by those who hold to it, the more devoted the oftener");
        C(PawnGodsMath.RiteChance(BeliefWord.Fervent) <= 0.2f && PawnGodsMath.RiteGapDays >= 1f && PawnGodsMath.RitesAnHour <= 2, "now and then: once a day at most a person, never a crowd");
        foreach (BeliefRite r in Enum.GetValues(typeof(BeliefRite)))
            C(keys.Contains("CC_Belief_Report_" + r), $"the {r} rite says what they are doing");
        C(keys.Contains("CC_Belief_Report_Grave_Cat") && acts.Contains("PawnGods.IsCat(a)") && acts.Contains("t is Building_Grave"), "Tamiu's by a grave, else feeding a cat");
        C(acts.Contains("job.count = g.id;") && acts.Contains("Worship.Say(pawn, \"Rimfolk_Pool_Belief_Rite\", g, null, tag);") && acts.Contains("PawnGodsActs.RiteKept(pawn, God)"),
            "a job on the map, one sentence over their head as they arrive, a little more sure after");
        C(acts.Contains("\"WaterWellInlet\"") && Defs(root).Contains("WaterWellInlet"), "Murmur's at the well of Dubs Bad Hygiene by name, else the water's edge");

        // ================= Rival devotees =================

        C(PawnGodsMath.BickerWeight(50f, 50f, false, false) == 0f && PawnGodsMath.BickerWeight(50f, 50f, true, true) == 0f && PawnGodsMath.BickerWeight(20f, 50f, true, false) == 0f,
            "only devotees of two different gods set against each other, both holding to theirs");
        C(PawnGodsMath.BickerWeight(30f, 30f, true, false) > 0f && PawnGodsMath.BickerWeight(90f, 90f, true, false) > PawnGodsMath.BickerWeight(30f, 30f, true, false)
          && PawnGodsMath.BickerWeight(100f, 100f, true, false) <= 0.06f, "the more devoted the likelier, a small share beside the game's chit-chat");
        C(acts.Contains("Gods.Rivals(ga).Contains(gb)") && acts.Contains("PawnGodsMath.BickerGapDays * GenDate.TicksPerDay"), "rivals by the gods' own ties, once in a few days a pair");
        var interactions = defs.SelectMany(x => x.doc.Root!.Elements("InteractionDef")).ToList();
        var rival = interactions.FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Belief_RivalGods");
        C(rival != null && rival.Element("workerClass")?.Value == "ColonyCouncil.InteractionWorker_RivalGods" && rival.Element("initiatorThought")?.Value == "Rimfolk_Belief_GodsQuarrel",
            "through the game's own social interactions");
        C(rival != null && float.Parse(rival.Element("socialFightBaseChance")?.Value ?? "1", System.Globalization.CultureInfo.InvariantCulture) <= 0.005f,
            "a scuffle at the game's own insult's rate at most");
        C(rival != null && !rival.Descendants("li").Any(l => GodLoreMath.Keys.Any(k => l.Value.Contains(k))), "the log line names no god (their own words do, by the colony's names)");

        // ================= Moods =================

        var thoughts = defs.SelectMany(x => x.doc.Root!.Elements("ThoughtDef")).Where(e => e.Element("defName") != null).GroupBy(e => e.Element("defName")!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        float Mood(string def) => float.Parse(thoughts[def].Descendants("baseMoodEffect").First().Value, System.Globalization.CultureInfo.InvariantCulture);
        C(thoughts.ContainsKey("Rimfolk_Belief_GodActed") && Mood("Rimfolk_Belief_GodActed") > 0f && Mood("Rimfolk_Belief_GodActed") <= 3f, "a small good mood when their god acts before them");
        C(Mood("Rimfolk_Belief_ShrineLost") < 0f && Mood("Rimfolk_Belief_FeastPassed") < 0f && Mood("Rimfolk_Belief_ShrineLost") >= -3f && Mood("Rimfolk_Belief_FeastPassed") >= -3f,
            "small bad moods when it is slighted: its place gone, its feast day let pass");
        C(Mood("Rimfolk_Belief_Flinched") < 0f && Mood("Rimfolk_Belief_Flinched") >= -2f && Mood("Rimfolk_Belief_WonOver") > 0f && Mood("Rimfolk_Belief_WonOver") <= 4f,
            "a flinch at a dreaded sign, being won over: small");
        foreach (var t in new[] { "Rimfolk_Belief_GodActed", "Rimfolk_Belief_ShrineLost", "Rimfolk_Belief_FeastPassed", "Rimfolk_Belief_Flinched", "Rimfolk_Belief_WonOver" })
        {
            C(pawnGods.Contains("public static ThoughtDef " + t + ";"), t + " reached from the code");
            C(File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Rates.cs")).Contains("(\"" + t + "\""), t + " in the balance model");
        }

        // ================= Dread =================

        C(acts.Contains("Shrines.Of(map, g)") && acts.Contains("PawnGodsMath.KeepAwayDistance") && acts.Contains("\"Rimfolk_Pool_Belief_KeepAway\"") && acts.Contains("JobDefOf.Goto"),
            "one in dread of a god walks away from its place, saying why");
        C(pawnGods.Contains("if (Dreads(p, taken)) PawnGodsActs.Flinch(p, taken, cell);") && acts.Contains("\"Rimfolk_Pool_Belief_Flinch\""), "and flinches at its signs");
        C(pawnGods.Contains("case GodActKind.Wrath:") && pawnGods.Contains("PawnGodsMath.DreadAmount(onThem, beside, HoldOf(p, taken))"), "dread from its anger on them or beside them");
        C(Read("Gods/Worship/FaithHeart.cs").Contains("!PawnGods.Dreads(p, x)"), "never turned to a god they dread");

        // ================= The pools =================

        var pools = defs.SelectMany(x => x.doc.Root!.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.FlavorPoolDef"))
            .GroupBy(e => e.Element("defName")!.Value).ToDictionary(g => g.Key, g => g.SelectMany(e => e.Descendants("li")).Select(l => l.Value).ToList());
        string Tags(string l) => Regex.Match(l, @"^\[([^\]]*)\]").Groups[1].Value;
        string Body(string l) => Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
        var sentence = new Regex(@"[.?!][""']?\s+[A-Z{]");
        foreach (var pool in new[] { "Rimfolk_Pool_Belief_Rite", "Rimfolk_Pool_Belief_WonOver", "Rimfolk_Pool_Belief_Flinch", "Rimfolk_Pool_Belief_KeepAway", "Rimfolk_Pool_Belief_ShrineLost",
                     "Rimfolk_Pool_Belief_Rival", "Rimfolk_Pool_Belief_RivalReply" })
        {
            var lines = pools.TryGetValue(pool, out var ls) ? ls : new List<string>();
            C(lines.Count >= 3 && lines.All(l => !sentence.IsMatch(Body(l)) && Body(l).Length <= 90 && !Body(l).StartsWith("\"") && !l.Contains("—") && !l.Contains("!")),
                pool + ": spoken, one short sentence each, no dashes, no exclamations");
            C(pawnGods.Contains("\"" + pool + "\"") || acts.Contains("\"" + pool + "\""), pool + " is said");
        }
        foreach (var pool in new[] { "Rimfolk_Pool_Belief_WonOverJournal", "Rimfolk_Pool_Belief_DreadJournal" })
            C(pools.ContainsKey(pool) && pools[pool].Count >= 3 && pools[pool].All(l => !l.Contains("—")), pool + ": the journal's words");
        var rite = pools["Rimfolk_Pool_Belief_Rite"];
        foreach (var k in GodLoreMath.Keys)
            C(rite.Count(l => Tags(l).Split(',').Contains("godkey:" + k.ToLowerInvariant())) >= 2, $"{k}'s rite has its own words");
        C(rite.All(l => Tags(l).StartsWith("godkey:")), "every rite line is one god's: never another god's words");
        C(rite.Count(l => Tags(l) == "godkey:mog,rite:grave") >= 2 && rite.Count(l => Tags(l) == "godkey:mog,rite:cat") >= 2, "Tamiu's, by a grave and with a cat");
        C(pools["Rimfolk_Pool_Belief_Rival"].All(l => l.Contains("{GOD}") && l.Contains("{RIVAL}")) && pools["Rimfolk_Pool_Belief_RivalReply"].All(l => l.Contains("{GOD}")),
            "rival devotees name both gods, each by the colony's name for it");
        C(pools["Rimfolk_Pool_Belief_WonOver"].Any(l => Tags(l).Contains("scoffer")) && pools["Rimfolk_Pool_Belief_WonOver"].Any(l => Tags(l).Length == 0), "a scoffer won over has words of their own");

        // ================= Keys =================

        foreach (Match m in Regex.Matches(beliefCode + Read("UI/ITab_Pawn_Culture.cs") + Read("Gods/GodLorePage.cs"), "\"(CC_(?:Belief|GodBook_(?:Hold|Dread))[A-Za-z_]*)\""))
        {
            string k = m.Groups[1].Value;
            if (k.EndsWith("_")) continue;
            C(keys.Contains(k), "key " + k);
        }
        foreach (DrawKind dk in Enum.GetValues(typeof(DrawKind)))
            if (dk != DrawKind.None)
                C(keys.Contains("CC_Belief_Why_" + dk), $"the line of why for {dk}");
        foreach (var dk in new[] { DrawKind.Trait, DrawKind.Passion, DrawKind.Xenotype, DrawKind.Backstory, DrawKind.People, DrawKind.Faith })
            C(keys.Contains("CC_Belief_Why_" + dk + "_None"), $"the line of why for {dk} with nothing to name");
        C(!Regex.IsMatch(Regex.Match(keyedAll, @"<CC_Belief_Section>[\s\S]*?<CC_Belief_Short_Dread>[^<]*<").Value, "—"), "no dashes in the words");

        // ================= How it shows =================

        string culture = Read("UI/ITab_Pawn_Culture.cs");
        string section = Regex.Match(culture, @"private static PaneSection TheirGods\(Pawn p\)[\s\S]*?\n        }\n").Value;
        C(culture.Contains("Add(list, TheirGods(p));") && section.Contains("PawnGods.ShownFor(p)") && section.Contains("CC_Belief_Scoffer") && section.Contains("b.why"),
            "the Culture tab: each of their gods in words, the line of why when hovered, a scoffer doubts them all");
        C(!section.Contains(".hold") && !section.Contains("HoldOf") && !section.Contains("ToString(\"") && !section.Contains("Favour") && !section.Contains("ToStringPercent"),
            "never a number, never the god's favour");
        C(section.Contains("Worship.Ours(p)"), "only for the colony's own people");
        C(pawnGods.Contains("if (!Usable(g)) continue;") && pawnGods.Contains("Gods.Known(g) || GodLore.Instance?.PageOf(g) != null") && pawnGods.Contains("name = NameOf(g, p)"),
            "a god the colony hasn't heard of isn't named; one heard of under another name shows that name");
        string page = Read("Gods/GodLorePage.cs");
        C(page.Contains("y = Holders(x, y, w, p);") && page.Contains("GodLore.Instance?.PageOf(g)?.name != p.title") && page.Contains("CC_GodBook_HoldersHand") && page.Contains("PawnGods.HoldersOf(g)"),
            "the book: under each chapter, who holds to it and who dreads it, in the keeper's hand, under the name the colony keeps it by");
        C(!Regex.Match(page, @"private static float Holders[\s\S]*?\n        }\n").Value.Contains("HoldOf"), "in words there too");

        // ================= The seams =================

        string prayers = Read("Gods/Worship/Prayers.cs"), faithHeart = Read("Gods/Worship/FaithHeart.cs"), vows = Read("Gods/Worship/Vows.cs"), festivals = Read("Gods/Worship/Festivals.cs");
        C(prayers.Contains("PawnGods.Prayed(p, g, r.atShrine);") && prayers.Contains("PawnGods.Answered(p, g, r.aboutName);") && prayers.Contains("PawnGods.Credited(p, g);"),
            "prayer: said, answered, come to pass");
        C(faithHeart.Contains("PawnGods.Unanswered(p, g, crisis);") && faithHeart.Contains("PawnGods.Turned(p, from, to);") && faithHeart.Contains("PawnGods.Mocked(believer, g);")
          && faithHeart.Contains("var held = PawnGods.NextHeld(p, from);"), "unanswered, a crisis, turning away through their beliefs, a scoffer's word");
        C(Read("Gods/Worship/Offerings.cs").Contains("PawnGods.Offered(p, g);") && vows.Contains("PawnGods.Vowed(p, g, kept,"), "offerings and vows");
        C(festivals.Contains("PawnGods.Festival(home, g, d.label);") && festivals.Contains("PawnGods.RiteLed(p, g);") && festivals.Contains("PawnGodsActs.FeastPassed(map, g, d.label);"),
            "festivals kept, led and let pass");
        C(Read("Gods/Worship/GodDreams.cs").Contains("PawnGods.Dreamt(p, g);") && Regex.Matches(Read("Gods/GodTales.cs"), @"PawnGods\.Told\(listener, teller, g\);").Count == 3,
            "dreams, and what they were told (a tale's piece, what it is, a saying)");
        C(gods2.Contains("PawnGods.Saw(pawns, ByKey(taken), act.kind, false, act.text, act.cell, map);") && gods2.Contains("PawnGods.Saw(witnesses, taken, PawnGods.KindOf(spec.kind), plain, words, spec.cell, spec.map);")
          && Read("Gods/GodHands.cs").Contains("PawnGods.Saw(witnesses, taken,"), "what they saw, under the god they took it for (maybe wrongly), in acts, moments and the hands in events");
        C(Read("Gods/World/GodWorldGifts.cs").Contains("PawnGods.Gift(p, g, label);") && pawnGods.Contains("GodLoreMath.Attribute(g.Key, lore.KnownOf(), Rand.Value)"),
            "a gift, under the god they take it for");
        C(worship.Contains("Safe(\"each one's gods\", () => PawnGods.Hourly(map, now));") && worship.Contains("Safe(\"each one's gods\", () => PawnGods.Daily(now));")
          && worship.Contains("Safe(\"a god's place gone\", PawnGodsActs.ShrinesLost);"), "kept in worship's own hour and day: no pass of its own, no Sched slot");
        C(!beliefCode.Contains("Sched."), "no scheduler offset of its own");
        C(!beliefCode.Contains("FavourValue") && !beliefCode.Contains("Gods.Favour(") && !beliefCode.Contains("AddFavour"), "belief is the pawn's own: it never reads or moves a god's favour");

        // ================= Saved, and small =================

        C(book.Contains("Scribe_Collections.Look(ref people, \"people\", LookMode.Deep);") && book.Contains("Scribe_Collections.Look(ref gods, \"gods\", LookMode.Deep);")
          && book.Contains("people.RemoveAll(r => r?.pawn == null || r.pawn.Discarded);"), "saved, nobody the game no longer keeps written");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("\"Gods/Belief/PawnGodsBook.cs:people\"") && est.Contains("\"Gods/Belief/PawnGodsBook.cs:gods\"") && est.Contains("PawnGodsMath.cs\", \"MaxHeld\""),
            "the saved collections modelled, capped by PawnGodsMath.MaxHeld");
        C(PawnGodsMath.Keep(Enumerable.Range(0, 10).Select(i => ((float)i * 5f, 0f)).ToList()).Count == PawnGodsMath.MaxHeld
          && PawnGodsMath.Keep(new[] { (0.5f, 0.2f), (30f, 0f) }).Count == 1, "at most a few gods a person, the faded let go");
        C(PawnGodsMath.Short(new string('a', 30) + " " + new string('b', 200)).Length <= PawnGodsMath.WhyLength + 3 && PawnGodsMath.Short("short") == "short", "the line of why kept short");

        return n;
    }

    private static float[] Scores(params (GodDomain d, float w)[] s)
    {
        var a = GodsMath.NewScores();
        foreach (var (d, w) in s) a[(int)d] = w;
        return a;
    }

    private static HashSet<string> Active(string tsv)
    {
        var set = new HashSet<string>();
        if (!File.Exists(tsv)) return set;
        foreach (var line in File.ReadAllLines(tsv).Skip(1))
        {
            var c = line.Split('\t');
            if (c.Length > 2 && c[2] == "yes") set.Add(c[0]);
        }
        return set;
    }

    private static string defsCache;

    private static string Defs(string root)
    {
        if (defsCache != null) return defsCache;
        using var gz = new System.IO.Compression.GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), System.IO.Compression.CompressionMode.Decompress);
        using var r = new StreamReader(gz);
        defsCache = string.Join("\n", r.ReadToEnd().Split('\n').Where(l => l.EndsWith("\tyes") || l.EndsWith("\tyes\r")));
        return defsCache;
    }
}
