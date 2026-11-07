using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Schemes against the colony (Diplomacy/SchemeMath.cs; docs/design/stage2-diplomacy.md, item 11, theirs only, release
/// 2c.3): who schemes, how often (about two a year, one a season at most, none without a schemer), what they do by the
/// colony's means and their people's ways, who catches them, what it costs; and the mod's side: a Scheme tie unknown until
/// found out, the machine through the rival's sabotage job and the game's breakdown, a fight only through the harm service,
/// the words in two wordings and one sentence, no clock of its own, nothing saved but the tie.
/// </summary>
public static class SchemesTests
{
    private const int D = SchemeMath.DayTicks;

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "schemes: " + what);
        }

        bool Near(float a, float b, float eps = 0.0001f) => Math.Abs(a - b) < eps;

        // ================= Who schemes =================

        C(Near(SchemeMath.GrudgeAt, RaidersCampMath.GrudgeAt) && Near(SchemeMath.DeadWeight, RaidersCampMath.DeadWeight) && Near(SchemeMath.VendettaWeight, RaidersCampMath.VendettaWeight),
            "a grudge read as the raiders' camps read one (20: the wrongs held, 15 a dead held, 20 a vendetta)");
        C(Near(SchemeMath.Grudge(12f, 1, false), RaidersCampMath.Grudge(12f, 1, false)) && Near(SchemeMath.Grudge(-5f, 0, true), 20f), "the same sum, no wrongs below nothing");
        C(SchemeMath.PeopleSchemes(20f, false, true) && !SchemeMath.PeopleSchemes(19.9f, false, true), "a people with a grudge of 20 or more schemes, not less");
        C(!SchemeMath.PeopleSchemes(90f, true, true), "never a hostile people: none of theirs comes to the colony in peace to do it");
        C(!SchemeMath.PeopleSchemes(90f, false, false), "only a people within reach");
        C(SchemeMath.NotableSchemes(-40, true, false, true) && !SchemeMath.NotableSchemes(-39, true, false, true), "a notable who thinks -40 or less of the colony, not -39");
        C(!SchemeMath.NotableSchemes(-90, false, false, true) && !SchemeMath.NotableSchemes(-90, true, true, true) && !SchemeMath.NotableSchemes(-90, true, false, false),
            "alive, their people at peace, a neighbour of the home");

        // ================= How often =================

        C(Near(SchemeMath.Chance(1f), 0.08f) && SchemeMath.Chance(0f) == 0f && Near(SchemeMath.Chance(2f), 0.16f), "8% a day at 100%, nothing at 0%, twice at 200%");
        C(Near(SchemeMath.Rate(float.NaN), 1f) && SchemeMath.Rate(-1f) == 0f && Near(SchemeMath.Rate(5f), 2f), "the setting read safely");
        C(SchemeMath.SeasonPassed(-1, 0) && SchemeMath.SeasonPassed(0, 15 * D) && !SchemeMath.SeasonPassed(0, 15 * D - 1) && SchemeMath.SeasonPassed(10 * D, 5 * D),
            "one a season at most: a season since the last began (a reload before it never blocks)");
        C(SchemeMath.Until(3 * D) == 18 * D && SchemeMath.SeasonTicks == 15 * D, "given up after a season");
        float year = SchemeMath.YearRate(SchemeMath.Chance(1f));
        C(year >= 2f && year <= 2.5f, $"about two a year with a schemer in reach ({year:0.00})");
        C(Near(SchemeMath.YearRate(1f), 4f) && SchemeMath.YearRate(0f) == 0f, "never more than one a season (four a year), none at nothing");
        // Five thousand colony-years of the day's roll and the season's wait, as the game runs them.
        var rng = new Random(7);
        int begun = 0, crowded = 0;
        const int years = 5000;
        long lastDay = -1;
        for (long day = 0; day < years * 60L; day++)
        {
            // The ticks since the last began, as the game counts them (a long run of days kept within an int's reach).
            bool passed = lastDay < 0 ? SchemeMath.SeasonPassed(-1, 0) : SchemeMath.SeasonPassed(0, (int)Math.Min(day - lastDay, 1000) * D);
            if (!passed || rng.NextDouble() >= SchemeMath.Chance(1f)) continue;
            if (lastDay >= 0 && day - lastDay < SchemeMath.SeasonDays) crowded++;
            lastDay = day;
            begun++;
        }
        float perYear = begun / (float)years;
        C(Math.Abs(perYear - year) < 0.1f && crowded == 0, $"the day's roll over many years: {perYear:0.00} a year, never two in a season");

        // ================= The acts =================

        C(SchemeMath.Night(23) && SchemeMath.Night(0) && SchemeMath.Night(3) && !SchemeMath.Night(4) && !SchemeMath.Night(22) && !SchemeMath.Night(12), "the night's acts from eleven to four");
        C(SchemeMath.Day(9) && SchemeMath.Day(19) && !SchemeMath.Day(8) && !SchemeMath.Day(20), "a word in someone's ear by day");
        C(SchemeMath.Hour(SchemeAct.Fight, 12) && !SchemeMath.Hour(SchemeAct.Fight, 1) && SchemeMath.Hour(SchemeAct.Theft, 1) && !SchemeMath.Hour(SchemeAct.Machine, 12)
          && SchemeMath.Hour(SchemeAct.Smear, 2), "each act at its hour");
        C(!SchemeMath.MachineAge(WorldAge.Tribal) && !SchemeMath.MachineAge(WorldAge.Medieval) && SchemeMath.MachineAge(WorldAge.Industrial) && SchemeMath.MachineAge(WorldAge.Spacer),
            "a machine broken only in an industrial colony and after");
        var counts = new Dictionary<SchemeAct, int>();
        for (int i = 0; i < 4000; i++)
        {
            var a = SchemeMath.Pick(true, true, true, 1f, (i + 0.5f) / 4000f);
            counts[a] = counts.TryGetValue(a, out int k) ? k + 1 : 1;
        }
        C(Enum.GetValues(typeof(SchemeAct)).Cast<SchemeAct>().All(a => counts.TryGetValue(a, out int k) && Math.Abs(k - 1000) <= 2), "with everything to hand, each act a quarter");
        bool noFight = true, noMachine = true, noTheft = true;
        for (float r = 0f; r <= 1f; r += 0.01f)
        {
            noFight &= SchemeMath.Pick(true, true, true, 0f, r) != SchemeAct.Fight;
            noMachine &= SchemeMath.Pick(false, true, true, 1f, r) != SchemeAct.Machine;
            noTheft &= SchemeMath.Pick(true, false, true, 1f, r) != SchemeAct.Theft;
        }
        C(noFight, "with hurts from everyday life at nothing, no fight is stirred up");
        C(noMachine && noTheft, "no machine broken where there is none, nothing taken where there is nothing");
        C(SchemeMath.Pick(false, false, false, 1f, 0.3f) == SchemeAct.Smear && SchemeMath.Pick(false, false, false, 1f, 1f) == SchemeAct.Smear, "a smear needs nothing of the colony's");
        float fightShare = Enumerable.Range(0, 1000).Count(i => SchemeMath.Pick(true, true, true, 0.5f, (i + 0.5f) / 1000f) == SchemeAct.Fight) / 1000f;
        C(Math.Abs(fightShare - 0.5f / 3.5f) < 0.01f, $"a fight's share leans with the harm scale ({fightShare:P0} at half)");

        // ================= Every age =================

        C(SchemeMath.MachineForm(WorldAge.Spacer, true) == "Console" && SchemeMath.MachineForm(WorldAge.Spacer, false) == "Machine" && SchemeMath.MachineForm(WorldAge.Industrial, true) == "Machine",
            "a console's breakdown in a spacer colony, a jammed machine otherwise");
        C(SchemeMath.TheftOrder(WorldAge.Tribal)[0] == SchemeMath.Totem && SchemeMath.TheftOrder(WorldAge.Medieval)[0] == SchemeMath.Purse
          && SchemeMath.TheftOrder(WorldAge.Industrial)[0] == SchemeMath.Tool && SchemeMath.TheftOrder(WorldAge.Spacer)[0] == SchemeMath.Tool,
            "a stolen totem from a tribe, a cut purse, a tool in an industrial or spacer colony");
        foreach (WorldAge age in Enum.GetValues(typeof(WorldAge)))
            C(SchemeMath.TheftOrder(age).OrderBy(x => x).SequenceEqual(new[] { SchemeMath.Purse, SchemeMath.Tool, SchemeMath.Totem }), $"{age}: the thief looks for each kind in turn");
        C(SchemeMath.SmearForm(WorldAge.Tribal, false) == "Fires" && SchemeMath.SmearForm(WorldAge.Medieval, false) == "Halls" && SchemeMath.SmearForm(WorldAge.Industrial, false) == "Papers"
          && SchemeMath.SmearForm(WorldAge.Spacer, false) == "Feeds", "a smear by their people's ways: other fires, other halls, the papers, the feeds");
        C(Enum.GetValues(typeof(WorldAge)).Cast<WorldAge>().All(a => SchemeMath.SmearForm(a, true) == "Court"), "intrigue at court for the Empire, whatever its age");
        C(Enum.GetValues(typeof(WorldAge)).Cast<WorldAge>().All(a => SchemeMath.SmearForms.Contains(SchemeMath.SmearForm(a, false))), "every smear's form is one of the five");
        foreach (SchemeAct a in Enum.GetValues(typeof(SchemeAct))) C(SchemeMath.ActOf(a.ToString()) == a, $"{a}: its key names it");
        C(SchemeMath.ActOf("Console") == SchemeAct.Machine && SchemeMath.ActOf(SchemeMath.Totem) == SchemeAct.Theft && SchemeMath.ActOf(SchemeMath.Purse) == SchemeAct.Theft
          && SchemeMath.ActOf(SchemeMath.Tool) == SchemeAct.Theft && SchemeMath.SmearForms.All(s => SchemeMath.ActOf(s) == SchemeAct.Smear) && SchemeMath.ActOf(null) == SchemeAct.Smear,
            "the form it took names its act");

        // ================= A small valuable =================

        C(SchemeMath.SmallValuable(600f, 3f) && !SchemeMath.SmallValuable(601f, 1f) && !SchemeMath.SmallValuable(10f, 3.1f) && !SchemeMath.SmallValuable(0f, 0.1f),
            "a small valuable: 600 silver at most, 3 kg at most, worth something");
        C(SchemeMath.LootCount(1f, 500, 0f) == 60 && SchemeMath.LootCount(1f, 500, 1f) == 180 && SchemeMath.LootCount(1f, 30, 0.5f) == 30, "a purse of 60 to 180 silver, never more than the stack");
        C(SchemeMath.LootCount(32f, 20, 0.5f) == 4 && SchemeMath.LootCount(200f, 5, 0f) == 1 && SchemeMath.LootCount(0f, 5, 0.5f) == 0, "a few components, one advanced component, never nothing worth something");

        // ================= Caught at it =================

        C(SchemeMath.CatchChance(0, 0, 0) == 0f, "nobody awake, no guard, no dog: never caught");
        C(Near(SchemeMath.CatchChance(1, 0, 0), 0.5f) && Near(SchemeMath.CatchChance(0, 1, 0), 0.9f) && Near(SchemeMath.CatchChance(0, 0, 1), 0.75f) && Near(SchemeMath.CatchChance(2, 0, 0), 0.75f),
            "a colonist awake half the time, a guard nine in ten, a dog three in four, two awake three in four");
        C(Near(SchemeMath.CatchChance(1, 0, 0, SchemeMath.EarNotice), 0.4f), "a whispered lie overheard two times in five");
        C(SchemeMath.CatchChance(5, 5, 5) <= 1f && SchemeMath.CatchChance(1, 1, 1) > SchemeMath.CatchChance(1, 1, 0), "more eyes, likelier; never past certainty");
        var tally = new Dictionary<SchemeWatch, int>();
        for (int i = 0; i < 10000; i++)
        {
            var w = SchemeMath.Catcher(1, 1, 1, (i + 0.5f) / 10000f);
            tally[w] = tally.TryGetValue(w, out int k) ? k + 1 : 1;
        }
        int T(SchemeWatch w) => tally.TryGetValue(w, out int k) ? k : 0;
        C(Math.Abs(T(SchemeWatch.Guard) - 9000) <= 2 && Math.Abs(T(SchemeWatch.Colonist) - 500) <= 2 && Math.Abs(T(SchemeWatch.Dog) - 375) <= 2 && Math.Abs(T(SchemeWatch.None) - 125) <= 2,
            "a guard first, then a colonist awake, then a dog, by their chances");
        C(Enumerable.Range(0, 100).All(i => SchemeMath.Catcher(0, 0, 0, i / 100f) == SchemeWatch.None), "nobody watching, nobody catches it");
        C(SchemeMath.Catcher(1, 0, 0, 0f) == SchemeWatch.Colonist && SchemeMath.Catcher(0, 0, 1, 0f) == SchemeWatch.Dog && SchemeMath.Catcher(3, 0, 0, 0.99f) == SchemeWatch.None,
            "the one who is there catches it");
        C(SchemeMath.QuietRange < SchemeMath.SeeRange && SchemeMath.SeeRange < SchemeMath.GuardRange && SchemeMath.DogNear < SchemeMath.DogRange && SchemeMath.EarRange < SchemeMath.SeeRange,
            "nobody sets to it under someone's nose; a guard sees further than one just awake; a dog hears through a wall only close by");
        C(SchemeMath.CaughtGoodwill == 5, "caught, their people think 5 less of the colony");
        C(SchemeMath.SmearGoodwill > 0 && SchemeMath.SmearGoodwill <= 5, "a smear costs a little with each other people");
        C(SchemeMath.Loss(0, 5) == 5 && SchemeMath.Loss(-72, 5) == 2 && SchemeMath.Loss(-74, 5) == 0 && SchemeMath.Loss(-90, 5) == 0, "never so much as to turn a people hostile (-75)");
        C(SchemeMath.SmearLands(0, 0f) == D && SchemeMath.SmearLands(0, 1f) == 3 * D, "a smear is told round one to three days after it was gathered");
        C(SchemeMath.Carried == 1, "a smear carried home is the tie's count");

        // ================= The ledger, the history, the facts =================

        C(!TieRules.CanCallIn(Tie.Kind.Scheme) && TieRules.CanMove(Tie.Kind.Scheme, Tie.State.Open, Tie.State.Broken) && TieRules.CanMove(Tie.Kind.Scheme, Tie.State.Open, Tie.State.Kept)
          && TieRules.CanMove(Tie.Kind.Scheme, Tie.State.Open, Tie.State.Ended), "a scheme is kept (it worked), broken (found out) or ended (given up), never called in");
        C(TieRules.AtUntil(Tie.Kind.Scheme, Tie.State.Open) == Tie.State.Ended, "one not acted out by its end is given up");
        C(TieRules.KeepClosedDays >= SchemeMath.SeasonDays, "a closed scheme is kept longer than a season, so the season's wait reads it");
        C(SchemeMath.HistoryKind("broken") == "tie:scheme:theirs:broken" && HistoryKinds.Matches(SchemeMath.HistoryKind("broken"), "tie:scheme") && HistoryKinds.Matches(SchemeMath.HistoryKind("broken"), "tie"),
            "the history's kind of a scheme found out, in the ties' family (its tie's card reads it)");
        C(SchemeMath.FoundOutTag == SchemeMath.HistoryKind("broken") && SchemeMath.Tags.Contains(SchemeMath.CaughtTag) && SchemeMath.CaughtTag == "them:caught", "the facts: tie:scheme:theirs:broken, them:caught");
        C(DiplomacyMath.FoundOut == "dip:foundout" && DiplomacyMath.Kinds.Contains(DiplomacyMath.FoundOut), "found out, in diplomacy's own family");
        C(DiplomacyMath.Deeds.Contains(("schemedUs", "TheirWrong", 12)), "their people remember it: schemedUs, a wrong of theirs");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string sch = Code("Diplomacy/Schemes.cs"), dbg = Code("Diplomacy/SchemesDebug.cs"), facts = Code("Diplomacy/FactsSchemes.cs");
        C(!Regex.IsMatch(sch, @"TakeDamage\(|AddHediff\(|StartSocialFight\(") && sch.Contains("Harm.Fight(hot, other"), "nobody is hurt but through the harm service: the fight is its social fight, under the cap for both");
        C(sch.Contains("Harm.On ? Harm.ScaleFor(HarmKinds.SchemeFight) : 0f") && sch.Contains("case SchemeAct.Fight: return Harm.On && HotAtHome(home);"), "a fight's share leans with the harm scale, none with hurts at nothing");
        C(!Regex.IsMatch(sch + dbg + facts, @"Sched\.(Rare|Hourly|Every|Slice)|GameComponentTick"), "no clock of its own (no new Sched slot): diplomacy's hour and day");
        string dip = Code("Diplomacy/Diplomacy.cs");
        C(dip.Contains("Safe(\"schemes\", () => Schemes.Hourly(now));") && dip.Contains("Safe(\"schemes' day\", () => Schemes.Daily(now));"), "the hour and the day ride on diplomacy's");
        C(!sch.Contains("Scribe_") && !sch.Contains("ExposeData"), "nothing saved but the tie");
        C(sch.Contains("WorldLedger.AddTie(Tie.Kind.Scheme, who, colony") && sch.Contains("SchemeMath.Until(now), SchemeMath.Origin)"), "a Scheme tie from the schemer to the colony, given up after a season");
        C(Code("World/Ledger/History.cs").Contains("if (t.kind == ColonyCouncil.Tie.Kind.Scheme && t.origin == SchemeMath.Origin) return;"),
            "unknown until found out: nothing of it in the history (which the History page shows) before then");
        C(sch.Contains("History.Add(SchemeMath.HistoryKind(\"broken\")") && sch.Contains("Diplomacy.Write(DiplomacyMath.FoundOut"), "found out: tie:scheme:theirs:broken and dip:foundout");
        C(sch.Contains("FactionMemory.Note(f, \"schemedUs\"") && sch.Contains("LoseGoodwill(f, SchemeMath.CaughtGoodwill)"), "their people remember the colony caught them (schemedUs, 5 goodwill)");
        C(sch.Contains("new LordJob_ExitMapBest(LocomotionUrgency.Jog") && sch.Contains("TryDrop(took"), "caught: the thing dropped, the agent gone at once");
        C(sch.Contains("Sabotage.Job") && sch.Contains("comp.DoBreakdown();") && Code("Harm/Sabotage.cs").Contains("if (!Schemes.Done(pawn, job.targetA.Thing)) Sabotage.Done(pawn, Bench);"),
            "a machine through the rival's sabotage job and the game's own breakdown");
        C(sch.Contains("p.GetTraderCaravanRole() != TraderCaravanRole.Guard") && sch.Contains("LordJob_VisitColony") && sch.Contains("p.DevelopmentalStage.Juvenile()"),
            "one of theirs at the colony in peace, grown up, never the trader");
        C(sch.Contains("PlayOuts.WhyNotNow(home, false) != null") && sch.Contains("SchemeMath.QuietRange"), "never in danger, never under someone's nose");
        C(sch.Contains("Together.When(\"scheme fight|") && sch.Contains("Together.Hears(c, i.agent, SchemeMath.EarRange)"), "the fight had out face to face, the whisper overheard by whoever is in earshot");
        C(sch.Contains("by.caller?.DoCall(true)") && sch.Contains("RestUtility.WakeUp("), "a dog barks the colony awake (the game's own call)");
        C(Code("Text/Facts/FactsLedger.cs").Contains("SchemeFacts(s, f);") && facts.Contains("t.state != Tie.State.Broken"), "the facts only of what was found out");
        string model = Code("Diplomacy/DiplomacyModel.cs"), away = Code("UI/Region/HereAndAwayView.cs");
        C(model.Contains("Schemes.FoundRows(f)") && model.Contains("m.schemes.AddRange(Schemes.FoundRows());") && away.Contains("CC_Scheme_Found_Title"),
            "the pages list what was found out: the people's card, Here and away");
        C(sch.Contains("t.state == Tie.State.Broken") && !Regex.IsMatch(model, @"TieLine[^;]*Scheme"), "a scheme not found out is shown nowhere");
        C(Regex.Matches(dbg, @"\[DebugAction\(""Rimfolk"", ""Schemes: ").Count == 6, "six dev tools, each through the feature itself");
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        C(settings.Contains("public float theirSchemes = 1f;") && settings.Contains("Scribe_Values.Look(ref theirSchemes, \"theirSchemes\", 1f);")
          && catalog.Contains("Slider(\"theirSchemes\", \"CC_Settings_TheirSchemes\", \"CC_SetNote_TheirSchemes\", 0f, 2f,"), "the setting \"Schemes against the colony\", 0 to 200%, 100% by default");

        // The jobs.
        var jobs = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_Schemes.xml")).Root!.Elements("JobDef").ToDictionary(e => e.Element("defName")!.Value, e => e.Element("driverClass")!.Value);
        foreach (var j in new[] { "Rimfolk_SchemeTake", "Rimfolk_SchemeLook", "Rimfolk_SchemeWord" })
            C(jobs.TryGetValue(j, out var cls) && cls == "ColonyCouncil.JobDriver_Scheme", $"{j} is the schemes' own job");
        C(File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_HarmTwo.xml")).Contains("<defName>Rimfolk_Sabotage</defName>") && !jobs.ContainsKey("Rimfolk_Sabotage"),
            "the machine uses the sabotage's job, not one of its own");

        // The words: every told message in two wordings, every form and watcher with its words, no em dashes or exclamation marks.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Schemes.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var key in new[] { "CC_Scheme_Caught", "CC_Scheme_Fight", "CC_Scheme_Words", "CC_Scheme_Smear" })
            C(keyed.ContainsKey(key) && keyed.ContainsKey(key + "_2"), $"{key} has two wordings");
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes or exclamation marks");
        C(!keyed.Values.Any(v => Regex.IsMatch(v, @"(^|[.?]\s+|\\n)\{[A-Z]+_(pronoun|possessive|objective)\}")), "no sentence opens with a pronoun the game won't capitalize");
        foreach (var form in new[] { "Machine", "Console", SchemeMath.Totem, SchemeMath.Purse, SchemeMath.Tool })
            C(keyed.ContainsKey("CC_Scheme_What_" + form) && keyed.ContainsKey("CC_Scheme_Short_" + form), $"{form}: its words for the letter and the row");
        foreach (var form in SchemeMath.SmearForms)
            C(keyed.ContainsKey("CC_Scheme_What_Smear_" + form) && keyed.ContainsKey("CC_Scheme_SmearWhere_" + form), $"the smear by {form}: its words");
        foreach (var how in new[] { "Guard", "Awake", "Heard", "Dog" }) C(keyed.ContainsKey("CC_Scheme_How_" + how), $"caught by {how}: its words");
        foreach (SchemeAct a in Enum.GetValues(typeof(SchemeAct))) C(keyed.ContainsKey("CC_Scheme_After_" + (a == SchemeAct.Theft ? "Theft" : a.ToString())), $"{a}: what came of it");
        var allKeys = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                allKeys.Add(el.Name.LocalName);
        foreach (var code in new[] { sch, facts, model, away })
            foreach (Match m in Regex.Matches(code, "\"(CC_Scheme_[A-Za-z_]+)\""))
            {
                string k = m.Groups[1].Value;
                if (k.EndsWith("_")) continue;
                C(allKeys.Contains(k), $"{k} exists");
            }
        C(allKeys.Contains("CC_Memory_Deed_schemedUs") && allKeys.Contains("CC_Memory_Deed_schemedUs_Name") && allKeys.Contains("CC_Settings_TheirSchemes_Tip"),
            "the deed with the one caught named, the setting's tip");

        // Said over a head: one short sentence each, and every pool asked for there with lines.
        var pools = new Dictionary<string, List<string>>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                pools[d.Element("defName")!.Value] = d.Descendants("li").Select(li => li.Value).ToList();
        var asked = Regex.Matches(sch, "\"(Rimfolk_Pool_[A-Za-z]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(asked.Count == 4, $"the four pools are asked for ({asked.Count})");
        foreach (var pool in asked)
        {
            C(pools.TryGetValue(pool, out var lines) && lines.Count >= 6, $"{pool} exists with six lines or more");
            if (lines == null) continue;
            foreach (var line in lines)
            {
                string body = Regex.Replace(line, @"^\s*\[[^\]]*\]\s*", "").Trim();
                int cut = -1;
                for (int i = 12; i < body.Length; i++)
                    if (body[i] == '.' || body[i] == '!' || body[i] == '?')
                    {
                        cut = i;
                        break;
                    }
                C(cut < 0 || cut == body.Length - 1, $"{pool}: '{body}' is one sentence");
                C(body.Length <= 90 && !body.Contains("!") && !body.Contains("—"), $"{pool}: '{body}' is short, with no exclamation mark or em dash");
            }
        }
        C(pools.TryGetValue("Rimfolk_Pool_SchemeCaught", out var shouts) && shouts.All(l => !l.Contains("{OTHER") && !l.Contains("{ABOUT")),
            "a shout at a stranger names nobody");
        foreach (var tag in new[] { "machine", "theft", "fight", "smear" })
            C(shouts != null && shouts.Any(l => l.Contains("[act:" + tag + "]")), $"a shout for each act: {tag}");

        // The Almanac, the design, the balance model.
        var alm = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Schemes.xml")).Root!.Elements().First();
        C(alm.Element("defName")!.Value == "Council_Alm_C_Schemes" && alm.Element("keywords")!.Elements().Any(), "an Almanac page of its own");
        string almText = alm.ToString();
        C(!almText.Contains("—") && !almText.Contains("!"), "the Almanac page: no em dashes or exclamation marks");
        C(File.ReadAllText(Path.Combine(root, "DESIGN.md")).Contains("#### Schemes against the colony"), "DESIGN.md has it");
        C(File.ReadAllText(Path.Combine(root, "docs", "design", "stage2-diplomacy.md")).Contains("Built (2c.3, theirs only"), "the design marks it built");
        string balance = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Balance.csproj"));
        C(balance.Contains("Diplomacy/SchemeMath.cs") && File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Program.cs")).Contains("SchemeSim.Run(args)"),
            "the balance model's schemes mode reads the very rules");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
