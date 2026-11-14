using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Mog remade as Tamiu, the Keeper of the Threshold (the player, 13 and 14 November 2026: "Still a cat card, but kind of one
/// inspired by, you know, Egyptian mythology and the Sphinx, you know, with an Egyptian name ... with mummies and like
/// Egyptian gear wearing zombies style followers ... and there's also some sort of cult behind it"; "yes, remake mog and
/// create a corresponding sign for it", "gods effect should be noticeable"): the pure rules (her riddle, how often an answer
/// is wrong, the cuff, a new grave kept, the wrapped dead), her def (defName and key kept for saves, her new name, plain
/// line, domains, ties, stories), her shape and look (a great sand-gold cat from the player's mods, the game's cats as the
/// fallback), her own omens and presence, the riddle's card, the words (every age's riddles and forms, named only where the
/// colony knows), and the seams (the shared harm budget, her stay, the gods' hour, the name she is said by).
/// </summary>
public static class MogTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "Tamiu (Mog remade): " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        XDocument Def(string rel) => XDocument.Load(Path.Combine(root, "Defs", rel));
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);

        // ================= The riddle at the threshold =================

        C(!MogMath.RiddleDue(3f, true, 12, 0f) && !MogMath.RiddleDue(0.5f, false, 12, 0f) && !MogMath.RiddleDue(3f, false, 3, 0f) && !MogMath.RiddleDue(3f, false, 20, 0f)
          && MogMath.RiddleDue(3f, false, 12, 0f) && !MogMath.RiddleDue(3f, false, 12, MogMath.RiddleDayChance),
            "her riddle comes once a stay, from her second day, in the daytime hours, at its chance");
        float stay = MogMath.RiddleInAStay(12.5f);
        C(stay > 0.6f && stay < 0.85f && MogMath.RiddleInAStay(1f) == 0f, $"most of her seasons see her riddle ({stay:P0} of a season of twelve and a half days)");
        C(Math.Abs(MogMath.WrongChance(0) - 0.75f) < 0.001f && MogMath.WrongChance(10) < 0.35f && MogMath.WrongChance(15) == 0.1f && MogMath.WrongChance(20) == 0.1f
          && MogMath.WrongChance(-3) == MogMath.WrongChance(0),
            "an answer is wrong three times in four with no skill, about one in three at ten, one in ten from fifteen, never surer");
        bool falls = true;
        for (int l = 1; l <= 20; l++) falls &= MogMath.WrongChance(l) <= MogMath.WrongChance(l - 1);
        C(falls, "the better the answerer, the likelier they are right");
        bool guesses = true;
        for (int i = 0; i < MogMath.RiddlesPerAge; i++)
            for (int seed = -5; seed < 40; seed++)
            {
                int g = MogMath.WrongGuess(i, seed);
                guesses &= g != i && g >= 0 && g < MogMath.RiddlesPerAge;
            }
        C(guesses, "a wrong guess is always another of the age's answers, never the right one");
        C(Enumerable.Range(-50, 200).All(s => MogMath.Riddle(s) >= 0 && MogMath.Riddle(s) < MogMath.RiddlesPerAge), "any seed picks one of the age's riddles");
        C(MogMath.CuffChance > 0f && MogMath.CuffChance < 1f && MogMath.CuffLow > 0f && MogMath.CuffHigh <= 6f && MogMath.RightFavour > 0f && MogMath.NobodyFavour < 0f,
            "a wrong answer may earn a cuff of the paw, a light scratch; a right one pleases her, nobody going a little less");

        // ================= A new grave kept =================

        C(Math.Abs(MogMath.KeptChance(false, 0f) - 0.2f) < 0.001f && MogMath.KeptChance(true, 0f) > MogMath.KeptChance(false, 0f)
          && MogMath.KeptChance(true, 1f) == 0.7f && MogMath.KeptChance(false, 5f) <= 0.7f && MogMath.KeptChance(false, -1f) >= 0.2f,
            "a fifth of new graves kept, more where the colony holds her and the nearer she is drawn, never more than seven in ten");
        C(MogMath.KeeperChance > 0f && MogMath.KeeperChance < 1f && MogMath.CatDaysMin >= 1f && MogMath.CatDaysMax >= MogMath.CatDaysMin && MogMath.KeeperHours >= 8f
          && MogMath.DuskFrom >= 16 && MogMath.DuskTo <= 22 && MogMath.DuskTo > MogMath.DuskFrom,
            "her keeper comes at dusk and sits the night through, and the cat keeps the grave a day or two");

        // ================= The wrapped dead =================

        C(MogMath.RiseChance(true, 1f, MogMath.MinTombs - 1) == 0f && MogMath.RiseChance(false, 1f, 0) == 0f, "with too few tombs the dead never stand");
        C(MogMath.RiseChance(true, 0f, 5) == 0.5f && MogMath.RiseChance(false, MogMath.RiseNear - 0.01f, 5) == 0f
          && MogMath.RiseChance(false, 1f, 5) > 0f && MogMath.RiseChance(false, 1f, 5) <= 0.35f,
            "they stand in half the raids near the tombs while she is on the land, else only when she is drawn near, and less");
        C(MogMath.Risen(1) == 0 && MogMath.Risen(2) == 2 && MogMath.Risen(8) == 3 && MogMath.Risen(40) == 4, "two stand, more with more tombs, four at most");
        C(!MogMath.LieDown(MogMath.QuietTicks - 1, 100) && MogMath.LieDown(MogMath.QuietTicks, 100) && MogMath.LieDown(0, MogMath.LongestTicks),
            "they lie down as dust an hour after the raiders are gone, or after the longest watch");
        C(MogMath.RiseGapDays >= 5f && MogMath.GuardRadius > MogMath.WanderRadius && MogMath.EnemyNear >= MogMath.GuardRadius, "once in days at most, kept near the graves");

        // ================= Her def =================

        var god = Def("GodDefs/Rimfolk_Gods.xml").Root!.Elements("ColonyCouncil.GodDef").Single(d => d.Element("defName")?.Value == "Rimfolk_God_Mog");
        string V(string e) => god.Element(e)?.Value ?? "";
        List<string> L(XElement x, string e) => x.Element(e)?.Elements("li").Select(l => l.Value).ToList() ?? new List<string>();
        C(V("key") == "Mog" && V("label") == "Tamiu" && V("epithet") == "the Keeper of the Threshold", "the defName and key stay Mog for saves; her name is Tamiu, the Keeper of the Threshold");
        C(V("plain") == "A great cat who keeps the dead. She guards the graves, sets riddles at the threshold, and every cat is hers.", "her plain line");
        C(L(god, "otherNames").Contains("Mog") && L(god, "spacerNames").Contains("the Sphinx") && L(god, "tribalNames").Count > 0 && L(god, "medievalNames").Count > 0
          && L(god, "industrialNames").Count > 0, "a name in every age, and her old hearth-name Mog among her others");
        C(V("domain") == "Dead" && V("second") == "Hearth" && V("look") == "Rimfolk_GodLook_Mog" && V("nature") == "Proud",
            "the pantheon's keeper of the dead, proud, in her own look");
        var ties = god.Element("ties")!.Elements("li").ToDictionary(t => t.Element("other")!.Value, t => t.Element("kind")!.Value);
        C(ties.TryGetValue("Morel", out var tm) && tm == "Rivals" && ties.TryGetValue("Undertow", out var tu) && tu == "Rivals" && ties.TryGetValue("Ennu", out var te) && te == "Rivals",
            "apart from the rot and the hunger: Morel's rival over the dead, the Hunger's at the door, and she still stares at the Guest");
        var stories = god.Element("stories")!.Elements("li").ToList();
        C(stories.Count >= 4 && stories.All(s => s.Element("pieces")!.Elements("li").All(p => !p.Value.Contains("Mog")))
          && stories.Count(s => s.Element("pieces")!.Elements("li").Any(p => p.Value.Contains("Tamiu"))) >= 4,
            "her stories tell of Tamiu by name, so a teller says their own name for her, and never of the old gray cat");
        C(stories.Any(s => L(s, "teaches").Contains("Council_Evt_Stray")), "a cat walking in is still her sign, once its story is heard");
        string lore = V("whatItIs") + V("howSeen") + V("character") + V("wants");
        C(new[] { "tribes", "old kingdoms", "towns", "spacers" }.All(lore.Contains) && lore.Contains("riddle") && lore.Contains("wrapped") && lore.Contains("keepers"),
            "every age keeps her its own way; the riddle, the wrapped dead and her keepers are in her lore");
        C(!(V("whatItIs") + V("howSeen")).Contains("gray cat"), "the gray cat is gone");

        // ================= Her shape and look =================

        var shape = Def("GodDefs/Rimfolk_GodsOnMap_Shapes.xml").Root!.Elements("ColonyCouncil.GodShapeDef").Single(d => d.Element("defName")?.Value == "Rimfolk_GodShape_Mog");
        var beasts = L(shape, "beasts");
        C(beasts.FirstOrDefault() == "AEXP_Lion" && beasts.Contains("AA_SandLion") && beasts.Contains("Cougar") && beasts.Contains("Lynx") && !beasts.Contains("Cat"),
            "she walks as a great cat: Vanilla Animals Expanded's lion first, Alpha Animals' sand lion, the game's cougar or lynx as the fallback");
        C(shape.Element("kin")?.Value == "Cat" && shape.Element("feeds")?.Value == "true" && shape.Element("leads")?.Value == "false" && shape.Element("stayDays") != null,
            "the colony's cats follow her; she takes a bowl she didn't ask for, stays a season and leads nobody");
        C(L(shape, "omens").SequenceEqual(new[] { "CatsFacing", "GoldEyes", "Sand" }) && L(shape, "nearWhen").Contains("Death") && L(shape, "nearWhen").Contains("Cats"),
            "her own signs, and a death on the land draws her near");
        var look = Def("GodDefs/Rimfolk_GodsOnMap_Shapes.xml").Root!.Elements("ColonyCouncil.GodLookDef").Single(d => d.Element("defName")?.Value == "Rimfolk_GodLook_Mog");
        C(look.Element("label")?.Value == "great sand-gold cat" && look.Element("minTech")?.Value == "Archotech" && look.Element("domain")?.Value == "Dead"
          && L(look, "hum").Contains("AM_RitualSustainer_Kemetic") && L(look, "sting").Contains("AEXP_Pawn_Lion_Call") && L(look, "motes").Contains("SandBits"),
            "her look: sand-gold, hers alone, Alpha Memes' Kemetic chant while she is near, a lion's call at her coming, sand rising off her");
        var gs = Read("Gods/OnMap/GodShapes.cs");
        C(gs.Contains("s.gods.Contains(g.Key)") && Read("Gods/God.cs").Contains("name = def.label.NullOrEmpty() ? def.key : def.label;")
          && Read("Gods/World/GodWays.cs").Contains("\"godkey:\" + g.Key.ToLowerInvariant()"),
            "she is said by her name and found by her key everywhere (her shape, her ways, her lines)");

        // ================= Her own omens and presence =================

        foreach (var k in new[] { "CatsFacing", "GoldEyes", "Sand" })
        {
            C(Enum.TryParse<OmenKind>(k, out _) && keyed.ContainsKey("CC_GodM_Saw_" + k), $"the sign {k} is shown and kept in the book");
            C(Regex.IsMatch(File.ReadAllText(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsOnMap_Lines.xml")), @"\[" + k.ToLowerInvariant() + @"\][^<]+</li>"), $"someone says what they saw of {k}");
        }
        string own = Read("Gods/OnMap/GodOmensOwn.cs");
        C(own.Contains("case OmenKind.CatsFacing:") && own.Contains("case OmenKind.GoldEyes:") && own.Contains("case OmenKind.Sand:") && own.Contains("\"Filth_Sand\""),
            "each of her signs shows on the map: the cats sit facing the graves, two gold eyes in the dark, sand on a grave");
        var mapDefs = Def("GodDefs/Rimfolk_GodsOnMap_Mog.xml").Root!;
        var cond = mapDefs.Elements("GameConditionDef").SingleOrDefault(d => d.Element("defName")?.Value == "Rimfolk_GodPresence_Mog");
        string ways = Read("Gods/OnMap/MogsWays.cs");
        // Her presence is one of the gods' own (round 2: GodDef.presence, Gods/Presence/GodPresenceOwn.cs), its effect her watch.
        var condOwn = cond?.Element("modExtensions")?.Elements("li").FirstOrDefault(l => (string)l.Attribute("Class") == "ColonyCouncil.OwnPresence");
        string shows = Read("Gods/Presence/GodPresenceShows.cs");
        string ownMath = Read("Gods/Presence/GodPresenceOwnMath.cs");
        C(cond?.Attribute("ParentName")?.Value == "Rimfolk_GodPresenceOwnBase" && condOwn?.Element("god")?.Value == "Mog" && condOwn.Element("effect")?.Value == "Watch"
          && !ways.Contains("class GameCondition_MogPresence") && ways.Contains("GodPresenceOwn.Quiet(Her, a.Map)")
          && shows.Contains("case PresenceEffect.Watch:") && shows.Contains("exitMapAfterTick = now + Rand.Range(60, 1200)") && shows.Contains("MogsWays.CatsWatch(map)")
          && ownMath.Contains("e == PresenceEffect.Watch") && ownMath.Contains("EndsWhenGone"),
            "her own presence from her coming to her going, one of the gods' own: the rats run off, the cats sit with the graves, ended once she has gone");
        C(cond?.Element("label")?.Value == "the cats keeping watch" && !(cond.Element("description")?.Value ?? "{NAME}").Contains("{NAME}")
          && (condOwn?.Element("named")?.Value ?? "").Contains("{NAME}") && (condOwn?.Element("namedText")?.Value ?? "").Contains("{NAME}")
          && !keyed.ContainsKey("CC_Mog_Presence"),
            "her presence says what anyone can see, and names her only where the colony knows it is her");

        // ================= The riddle's card =================

        var inc = mapDefs.Elements("IncidentDef").Single(d => d.Element("defName")?.Value == MogsWaysNames.Riddle);
        var ext = inc.Element("modExtensions")!.Element("li")!;
        var opts = ext.Element("options")!.Elements("li").ToList();
        C(inc.Element("baseChance")?.Value == "0" && inc.Element("workerClass")?.Value == "ColonyCouncil.HappeningWorker_GodRiddle" && opts.Count == 3 && ext.Element("defaultOption")?.Value == "2",
            "her stay alone brings the card; send the wits, send the tongue, or let her lie");
        C(opts.Take(2).All(o => o.Element("riskChance") != null && o.Element("riskOutcome")?.Value == o.Element("outcome")?.Value),
            "the answer is right or wrong by the answerer's skill, and the card doesn't tell which until they reach her");
        var texts = L(ext, "texts");
        C(Enumerable.Range(0, 4).All(a => texts.Any(t => t.StartsWith("[age" + a + "]"))) && texts.All(t => t.Contains("{TALE}") && t.Contains("{BEAST}") && !t.Contains("Tamiu")),
            "her card in every age's words, the riddle on it, and no name for her");
        bool riddles = true;
        for (int a = 0; a < 4; a++)
            for (int i = 0; i < MogMath.RiddlesPerAge; i++)
                riddles &= keyed.ContainsKey($"CC_Mog_RiddleQ_{a}_{i}") && keyed.ContainsKey($"CC_Mog_RiddleA_{a}_{i}") && keyed[$"CC_Mog_RiddleQ_{a}_{i}"].TrimEnd().EndsWith("?");
        C(riddles, "each age has its own riddles, each with its answer");
        C(ways.Contains("public override float RiskChance(HappeningContext ctx, int option, float chance)") && ways.Contains("MogMath.WrongChance(Level(ctx.a, SkillDefOf.Intellectual))")
          && ways.Contains("MogMath.WrongChance(Level(ctx.b, SkillDefOf.Social))") && ways.Contains("MogsWays.Answered(beast, ctx.a, risk)"),
            "the wits answer by Intellectual, the tongue by Social, and the one sent walks out to her");
        C(ways.Contains("Harm.Roll(p, HarmKinds.GodRiddle, MogMath.CuffChance)") && ways.Contains("Harm.Injure(p, HurtKind.Scratch, HurtWhere.Arm, MogMath.CuffLow, MogMath.CuffHigh, HarmKinds.GodRiddle, HurtClass.Injury, a)")
          && HarmKinds.GodRiddle.system == "gods" && HarmKinds.All.Contains(HarmKinds.GodRiddle),
            "the cuff of the paw is at the shared harm budget's rate, by her, shown on a card");
        C(ways.Contains("if (s?.a == a) Favour(s, p);") && Read("Gods/OnMap/SacredAnimal.cs").Contains("if (IsMog(s) && s.chosen == null) MogsWays.Favour(s, by);")
          && Read("Gods/OnMap/SacredAnimal.cs").Contains("s.chosen == p && s.a.Spawned && IsMog(s)"),
            "her old luck carried over as her favour: the one who answers well (else who fed her) walks with her and is lucky while she stays");

        // ================= The grave kept and the wrapped dead =================

        C(ways.Contains("Arrivals.Friendly(x => god.factions.Contains(x))") && ways.Contains("Arrivals.Visit(new List<Pawn> { k }, map, f, spot, MogMath.KeeperHours)")
          && ways.Contains("return Cat(b, map, g, null);") && ways.Contains("GodLore.Heard(k, god, line, \"Gate\")"),
            "her keeper comes from a people that holds her, sits the night at the grave and says why; else a cat keeps it");
        C(Enumerable.Range(0, 4).All(a => keyed.ContainsKey("CC_Mog_Keeper_Text_" + a) && keyed.ContainsKey("CC_Mog_Risen_Text_" + a)),
            "her keeper and the wrapped dead in every age's words");
        C(ways.Contains("if (!ModsConfig.AnomalyActive") && ways.Contains("ShamblerKind = \"ShamblerSwarmer\"") && ways.Contains("Faction.OfEntities")
          && ways.Contains("new LordJob_DefendPoint(mid, MogMath.WanderRadius, MogMath.GuardRadius, false, false)")
          && ways.Contains("Harm.Roll(HarmKinds.GodDead, chance)") && ways.Contains("!Harm.On") && HarmKinds.GodDead.system == "gods",
            "the wrapped dead are the game's shamblers, with Anomaly only, kept to the graves, at the shared harm budget's rate and never with hurts switched off");
        C(ways.Contains("LetterDefOf.ThreatSmall") && keyed["CC_Mog_Risen_Text_1"].Contains("turn on anything that comes near them"),
            "the card says what they do: they turn on anything that comes near the graves");
        var hediff = mapDefs.Elements("HediffDef").Single(d => d.Element("defName")?.Value == "Rimfolk_WrappedDead");
        C(hediff.Element("comps")!.Elements("li").Any(l => (string)l.Attribute("Class") == "ColonyCouncil.HediffCompProperties_WrappedDead")
          && ways.Contains("MogMath.LieDown(now - loudAt, now - risenAt)") && ways.Contains("MogsWays.Fell(Pawn)"),
            "they lie down as dust when the raiders are gone, and a fallen one's body goes to dust");
        C(Regex.Matches(ways, "Apparel_Broadwrap|Apparel_Headwrap|Apparel_Robe|Apparel_WarVeil|Apparel_TribalA").Count >= 5 && ways.Contains("CompColorable"),
            "wrapped in gray cloth: Ideology's broadwrap and headwrap, else the game's robe, tribalwear and veil");

        // ================= The seams =================

        string book = Read("Gods/OnMap/GodsOnMapBook.cs");
        C(book.Contains("MogsWays.Daily(now)") && book.Contains("MogsWays.Hourly(now)") && Read("Gods/OnMap/SacredAnimal.cs").Contains("else if (IsMog(s)) MogsWays.Keep(s, a, now);"),
            "her ways run in the gods' day and hour, and in her stay");
        C(!ways.Contains("Scribe_Collections"), "nothing of hers is a growing list in the save");
        C(new[] { "Gods: Tamiu's riddle at the door now", "Gods: Tamiu's keeper at the newest grave now", "Gods: the wrapped dead stand guard now" }.All(Read("Gods/OnMap/GodsOnMapDebug.cs").Contains),
            "dev mode can bring her riddle, her keeper and the wrapped dead now");
        C(ways.Contains("if (hour < MogMath.DuskFrom || hour >= MogMath.DuskTo || MapFacts.ThreatOn(map)) return;"), "her keeper comes at dusk, as the card says");
        foreach (var pool in new[] { "Rimfolk_Pool_MogRiddleSaid", "Rimfolk_Pool_MogKeeper", "Rimfolk_Pool_MogGraveCat", "Rimfolk_Pool_MogWrappedDead" })
        {
            var lines = mapDefs.Elements("ColonyCouncil.FlavorPoolDef").Single(d => d.Element("defName")?.Value == pool).Descendants("li").Select(l => Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", "")).ToList();
            C(lines.Count >= 3 && lines.All(l => Regex.Matches(l, @"[.!?](\s|$)").Count == 1 && !l.Contains('!')), $"{pool}: one sentence each, no exclamations");
        }
        return n;
    }

    /// <summary>The names the defs and the code share (kept here so the test reads the same name as MogsWays).</summary>
    private static class MogsWaysNames
    {
        public const string Riddle = "Rimfolk_Evt_GodRiddle";
    }
}
