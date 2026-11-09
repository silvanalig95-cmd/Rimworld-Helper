using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The heart, felt (docs/design/heart-felt.md, release 1; the player's words of 9 November 2026): a lasting weight from
/// heavy things that rest can't take strain below, rest easing less under real strain, and the two sides at once (the good
/// side growing at any strain, slower the more there is; good things easing strain and filling the good side together;
/// hardship spending part of the good side as a cushion). The rules are checked one by one, then run over weeks of play
/// like the player's save (a colonist looked at once an hour, as HeartLogic does, through the same pure rules) and held to
/// the design's targets: a calm colony steady in about a week and in good heart in two to three; one death keeping those
/// close strained about a week and barely touching the rest; a plague season leaving most worn; breaking only after a real
/// disaster; the good side still growing while strained; the day off rare, never in a calm colony. Then the map: the acts'
/// pools and jobs, the lines, the sign over the head, the bar's line and the messages, the settings and the save.
/// </summary>
public static class HeartFeltTests
{
    /// <summary>The pools' names and the signs' shapes, read from the code (the tests don't link the game's types).</summary>
    private static class HeartPools
    {
        public const string Band = "Rimfolk_Pool_HeartBand", Act = "Rimfolk_Pool_HeartAct";

        /// <summary>The shape FeelingIcons.GlyphOf gives a feeling, read from its switch.</summary>
        public static string Glyph(string code, string key, bool bright)
        {
            int at = code.IndexOf("public static string GlyphOf(", StringComparison.Ordinal);
            if (at < 0) return null;
            int end = code.IndexOf("public static Texture2D For(", at, StringComparison.Ordinal);
            string body = code.Substring(at, end - at);
            int k = body.IndexOf("case \"" + key + "\":", StringComparison.Ordinal);
            if (k < 0) return bright ? "Star" : "Cloud";
            var m = Regex.Match(body.Substring(k), @"return ""(\w+)"";");
            return m.Success ? m.Groups[1].Value : null;
        }
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the heart, felt: " + what);
        }

        Rules(C);
        Simulate(C);
        Seams(root, C);
        // Release 2: the heart, made to count.
        CountRules(C);
        CountSimulate(C);
        CountSeams(root, C);
        return n;
    }

    // ---------------- On the map, the settings and the save ----------------

    private static void Seams(string root, Action<bool, string> C)
    {
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string felt = Code("Heart/HeartFelt.cs"), acts = Code("Heart/HeartActs.cs"), job = Code("Heart/HeartActJob.cs"), logic = Code("Heart/HeartLogic.cs");

        // The heart's hour: the weight, the sink, the settling, then the map.
        C(logic.Contains("HeartFelt.WeighLosses(h, losses, speed, traits, now);") && logic.Contains("HeartFelt.Downed(p, h, speed, traits, now);")
          && logic.Contains("HeartFelt.Hold(p, h, m, speed, traits, now);"), "the hour weighs a loss, a downing and what lasts");
        C(logic.Contains("float sink = HeartFeltMath.SinkIn(h.strain + built, floor, hours);") && logic.Contains("GoodHeart.Spend(h, StrainSource.Heavy, sink)")
          && logic.Contains("h.recentBuild[(int)StrainSource.Heavy] += added;"), "strain sinks toward the weight, cushioned by the good side, on the record as heavy things");
        C(logic.Contains("HeartFelt.Ease(h, (settled.GoodEased + settled.catharsis) * HeartFeltMath.GoodEasesWeight, hours, now);"), "good things and a cry lift the weight, and it lifts with time");
        C(logic.Contains("HeartFelt.AfterHour(p, h, m, now);"), "the map's part comes from the heart's own hour (no tick or slot of its own)");
        C(!felt.Contains("Sched.") && !acts.Contains("Sched.") && !job.Contains("Sched."), "no new Sched slot");
        // The safety rule: the game's mood need, its breaker and its break picker stay untouched.
        foreach (var (name, code) in new[] { ("HeartFelt", felt), ("HeartActs", acts), ("HeartActJob", job) })
            C(!code.Contains("HarmonyPatch") && !code.Contains("MentalBreaker") && !code.Contains("Need_Mood") && !code.Contains("MentalBreakWorker") && !code.Contains("TryStartMentalState"),
                name + ": no patch, and nothing of the mood need, the breaker or the break picker");
        // Shown, said and told the Rimfolk way.
        C(felt.Contains("Notify.Message(text, p, type, importance);") && felt.Contains("if (tell == HeartTell.None || !S.heartMessages || !p.IsColonist) return;"),
            "messages through Notify, under their own setting (the bands told are kept even with it off)");
        C(felt.Contains("Speech.Say(p, line)") && acts.Contains("Speech.Say(p, line, other)") && !felt.Contains("MoteMaker.ThrowText") && !acts.Contains("MoteMaker.ThrowText"),
            "every line through Speech.Say");
        C(felt.Contains("vt.Free(p, map, view, now)") && felt.Contains("vt.Spoke(p,"), "lines keep the touches' one pace for what is said over a head");
        C(felt.Contains("mote.SetupMoteBubble(tex, null, def.color);") && felt.Contains("p.Map != Find.CurrentMap"), "the sign is the game's own thought bubble, on the map being looked at");
        C(acts.Contains("Together.When(key, p, friend,") && acts.Contains("InteractionDefOf.Chitchat"), "joy shared face to face, with the game's own chat");
        int ordinaryAt = acts.IndexOf("OrdinaryWork = new HashSet<string>", StringComparison.Ordinal);
        string ordinary = ordinaryAt < 0 ? "" : acts.Substring(ordinaryAt, acts.IndexOf("};", ordinaryAt, StringComparison.Ordinal) - ordinaryAt);
        C(acts.Contains("if (!AtOrdinaryWork(p) ||") && ordinary.Contains("\"Construction\"")
          && new[] { "Firefighter", "Doctor", "Patient", "Warden", "Childcare", "Hunting", "Handling" }.All(w => !ordinary.Contains("\"" + w + "\"")),
            "a step outside only from ordinary work, never a fire, a patient, a prisoner, a child, a hunt or an animal");
        C(acts.Contains("Habit(p, \"Council_HabitGiver_Grave\")") && acts.Contains("Habit(p, \"Council_HabitGiver_LookOut\")"), "grief and homesickness use the small habits' own visit and look out");
        C(Code("Social/Family.cs").Contains("HeartActs.Notify_Died(__instance, __state);"), "the bed of the one lost, from the death hook's own look");
        C(Regex.IsMatch(acts, @"public static bool Safe\(Pawn p, HeartMapContext m\)[\s\S]{0,900}p\.Drafted[\s\S]{0,600}ThreatOn[\s\S]{0,400}ShouldSeekMedicalRest"),
            "never drafted, in danger or in need of a doctor");

        // The jobs: one for every act, with its own driver and kind.
        var jobs = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_HeartActs.xml")).Root!.Elements("JobDef").ToList();
        int enumAt = acts.IndexOf("public enum HeartActKind", StringComparison.Ordinal);
        var kinds = enumAt < 0 ? new List<string>()
            : Regex.Matches(acts.Substring(enumAt, acts.IndexOf('}', enumAt) - enumAt), @"^\s+(\w+),?\s*$", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
        C(kinds.Count == 12, $"twelve acts with a job of their own ({kinds.Count})");
        foreach (var k in kinds)
        {
            var d = jobs.FirstOrDefault(j => (string)j.Element("defName") == "Rimfolk_HeartAct_" + k);
            C(d != null && (string)d.Element("driverClass") == "ColonyCouncil.JobDriver_HeartAct"
              && d.Descendants("kind").Any(e => e.Value == k.ToString()), $"a job for {k}");
        }

        // The pools: every act has lines of its own, every cause the code names has lines, the placeholders come with their tags.
        var pools = new Dictionary<string, List<string>>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
            foreach (var el in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                pools[(string)el.Element("defName")] = el.Element("lines")?.Descendants("li").Select(li => li.Value).ToList() ?? new List<string>();
        C(pools.ContainsKey(HeartPools.Band) && pools.ContainsKey(HeartPools.Act), "the two pools exist");
        var band = pools.GetValueOrDefault(HeartPools.Band) ?? new List<string>();
        var act = pools.GetValueOrDefault(HeartPools.Act) ?? new List<string>();
        IEnumerable<string> TagsOf(string line) => line.StartsWith("[") && line.Contains(']') ? line.Substring(1, line.IndexOf(']') - 1).Split(',').Select(t => t.Trim()) : Enumerable.Empty<string>();
        C(band.All(l => TagsOf(l).Any(t => t.StartsWith("band:") || t.StartsWith("good:") || t == "both" || t == "back")),
            "every line of how they are has a band, [both] or [back]: nobody is given a heart they don't have");
        foreach (var a in new[] { "sit", "stareout", "stepout", "pace", "sharp", "hum", "sing", "linger", "dance", "lostbed", "indoors", "door", "sitwith", "homefire", "sharejoy", "cry",
                     "badday", "dayoff", "friend" })
            C(act.Count(l => TagsOf(l).Contains("act:" + a)) >= 3, $"the act {a} has lines");
        foreach (var why in new[] { "loss", "sickself", "sickclose", "downed", "raid", "fight", "sleep", "letdowns", "pain", "friends", "purpose", "gathering" })
            C(band.Any(l => TagsOf(l).Contains("why:" + why)) && felt.Contains("\"why:" + why + "\""), $"the cause {why}: named by the code, with lines");
        foreach (var b in new[] { "band:strained", "band:worn", "band:breaking", "good:goodheart", "good:highspirits", "both", "back" })
        {
            C(band.Count(l => TagsOf(l).Contains(b)) >= 4, $"{b} has lines");
            C(band.Any(l => TagsOf(l).SequenceEqual(new[] { b })), $"{b} has lines that need nothing more");
        }
        C(band.Where(l => l.Contains("{LOST}")).All(l => TagsOf(l).Contains("why:loss")) && band.Where(l => l.Contains("{ILLNESS}")).All(l => TagsOf(l).Contains("why:sickself"))
          && band.Where(l => l.Contains("{SICK}")).All(l => TagsOf(l).Contains("why:sickclose")), "a name or an illness is said only where it is known");
        C(act.Where(l => l.Contains("{LOST}")).All(l => TagsOf(l).Contains("lost")) && act.Where(l => l.Contains("{OTHER")).All(l => TagsOf(l).Contains("with") || TagsOf(l).Contains("about")),
            "the one lost and whom it is said to, only with their tags");

        // The signs: every shape a feeling can wear is drawn.
        var keys = new[] { "Grief", "Despair", "Fear", "Dread", "Worry", "Anger", "Frustration", "Envy", "Love", "Tenderness", "Gratitude", "Joy", "Contentment", "Relief",
            "Homesick", "Nostalgia", "Loneliness", "Fellowship", "Pride", "Shame" };
        foreach (var k in keys)
            foreach (var bright in new[] { true, false })
            {
                string glyph = HeartPools.Glyph(felt, k, bright);
                C(glyph != null && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Icons", "Feelings", glyph + ".png")), $"the sign for {k} ({glyph}) is drawn");
            }

        // The colonist bar's line.
        C(BarBadgeMath.HeartLine(5f, 5f, 0f, 0f, 48f, 48f, 1f, out _, out _, out _, out _) == 0, "a trace of either side shows nothing on the bar");
        C(BarBadgeMath.HeartLine(60f, 0f, 0f, 0f, 48f, 48f, 1f, out float top, out float thick, out float sw, out float gw) == 1 && Math.Abs(sw - 48f * 0.6f) < 0.01f && gw == 0f,
            "strain alone: one line, as long as it is full");
        C(BarBadgeMath.HeartLine(60f, 50f, 0f, 0f, 48f, 48f, 1f, out top, out thick, out sw, out gw) == 2 && Math.Abs(gw - 24f) < 0.01f && thick >= 2f,
            "both at once: two lines, strain's and the good side's");
        C(top >= 48f - BarBadgeMath.LabelLift + BarBadgeMath.LabelHeight, "the line is under the name, not over the portrait or the game's icons");
        string badges = Code("ColonistBar/BarBadges.cs");
        C(badges.Contains("if (p != null && !p.Dead && S != null && S.barHeartLine && RimfolkPatches.On) HeartLine(rect, p, map);") && badges.Contains("var e = LineOf(p);")
          && Regex.IsMatch(badges, @"if \(now - e\.at >= RefreshSeconds \|\| now < e\.at\)\s*\{\s*e\.at = now;\s*var h = Heart\.Eligible"),
            "the bar line has its own setting and draws from a cache built once a second");

        // The settings: one for each new visible part, on, under the heart's.
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements())
            .GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);
        foreach (var (field, label) in new[] { ("heartActs", "HeartActs"), ("heartFeelingActs", "HeartFeelingActs"), ("heartSays", "HeartSays"), ("heartSigns", "HeartSigns"),
                     ("heartMessages", "HeartMessages"), ("barHeartLine", "BarHeartLine") })
        {
            C(settings.Contains("public bool " + field + " = true;") && settings.Contains("Scribe_Values.Look(ref " + field + ", \"" + field + "\", true);"), field + ": a setting, on");
            C(Regex.IsMatch(catalog, "Toggle\\(\"" + field + "\", \"CC_Settings_" + label + "\", \"CC_SetNote_" + label + "\", \"[^\"]*heart\"\\);"), field + ": on the settings' page, needing the heart");
            C(keyed.ContainsKey("CC_Settings_" + label) && keyed.ContainsKey("CC_Settings_" + label + "_Tip") && keyed.ContainsKey("CC_SetNote_" + label), field + ": its words");
        }

        // The save: values on the heart, an older save without them loads at nothing and only takes note.
        string data = Code("Heart/HeartData.cs");
        foreach (var f in new[] { "weight", "weightWhy", "weightAbout", "weightKind", "held", "heldWhy", "heldAbout", "heldKind" })
            C(data.Contains("Scribe_Values.Look(ref " + f + ", \"" + f + "\");"), f + " saved");
        foreach (var f in new[] { "weightUntil", "heldUntil", "toldStrain", "toldGood" })
            C(data.Contains("Scribe_Values.Look(ref " + f + ", \"" + f + "\", -1);"), f + " saved, an older save at never");
        C(!Regex.IsMatch(data, @"Scribe_Collections\.Look\(ref (weight|held|told)"), "no new saved collection");
        C(data.Contains("[Unsaved] public Dictionary<int, (int tick, float weight)> weighedFor;") && data.Contains("[Unsaved] public bool wasDowned;"), "what the hour notes on the side isn't saved");
    }

    // ---------------- The rules one by one ----------------

    private static void Rules(Action<bool, string> C)
    {
        bool Near(float a, float b, float tol = 0.001f) => Math.Abs(a - b) <= tol;

        // The weight.
        C(Near(HeartFeltMath.Combine(40f, 0f), 40f) && Near(HeartFeltMath.Combine(0f, 40f), 40f), "a weight on nothing is itself");
        C(HeartFeltMath.Combine(40f, 40f) > 40f && HeartFeltMath.Combine(40f, 40f) < 80f, "two losses weigh more than one and less than both in full");
        C(HeartFeltMath.Combine(90f, 90f) == HeartMath.Max && HeartFeltMath.Combine(float.NaN, -3f) == 0f, "never past 100, nothing from nothing");
        C(HeartFeltMath.Eased(40f, true, 24f) > HeartFeltMath.Eased(40f, false, 24f), "it lifts slowly at first, then faster");
        C(HeartFeltMath.Eased(40f, true, 24f) < 40f && HeartFeltMath.Eased(0.04f, false, 1f) == 0f, "it always lifts, and a trace is gone");
        C(HeartFeltMath.HoldDaysFor(55f) > HeartFeltMath.HoldDaysFor(42f) && HeartFeltMath.HoldDaysFor(42f) >= 5f && HeartFeltMath.HoldDaysFor(42f) <= 8f
          && HeartFeltMath.HeldHoldDays < HeartFeltMath.HoldDaysFor(0f), "the heavier, the longer it holds; what lasted while it lasted lifts sooner once over");
        C(HeartFeltMath.Floor(70f, 70f) == HeartFeltMath.FloorMax && HeartFeltMath.FloorMax < HeartMath.EpisodeAt && HeartFeltMath.FloorMax > HeartMath.BandStart(StrainBand.Breaking),
            "the weights alone can bring someone near breaking, never to the day off");
        C(HeartFeltMath.SinkIn(10f, 40f, 1f) == HeartFeltMath.SinkPerHour && HeartFeltMath.SinkIn(39f, 40f, 1f) == 1f && HeartFeltMath.SinkIn(50f, 40f, 1f) == 0f,
            "strain sinks toward the weight, never past it");
        C(40f / HeartFeltMath.SinkPerHour <= 24f, "a heavy thing is felt fully within a day");
        C(HeartFeltMath.LossWeight("MySonDied") > HeartFeltMath.LossWeight("PawnWithGoodOpinionDied")
          && HeartFeltMath.LossWeight("PawnWithGoodOpinionDied") > HeartFeltMath.LossWeight("KnowColonistDied") && HeartFeltMath.LossWeight("KnowColonistDied") > 0f,
            "a partner's or kin's death weighs most, a friend's next, anyone's a little");
        C(HeartFeltMath.LossWeight("AteWithoutTable") == 0f && HeartFeltMath.LossWeight(null) == 0f, "only heavy things weigh");
        C(HeartFeltMath.LossWeight("KnowColonistDied") < HeartMath.BandStart(StrainBand.Strained), "the death of someone not close never strains on its own");
        C(HeartFeltMath.LossWeight("PawnWithGoodOpinionDied") >= HeartMath.BandStart(StrainBand.Strained), "a friend's does");
        C(HeartFeltMath.RaidWeight(0) == 0f && HeartFeltMath.RaidWeight(2) > HeartFeltMath.RaidWeight(1) && HeartFeltMath.RaidWeight(20) <= 36f, "a fight going badly, by how many are down");
        C(HeartFeltMath.SickSelfWeight(0f) == 0f && HeartFeltMath.SickSelfWeight(0.9f) > HeartFeltMath.SickCloseWeight(0.9f) && HeartFeltMath.SickCloseWeight(0.1f) == 0f,
            "one's own sickness weighs more than a friend's, and a cold caught today nothing yet");

        // Rest under strain.
        C(HeartFeltMath.RestFactor(0f) == 1f && HeartFeltMath.RestFactor(HeartFeltMath.RestFullBelow) == 1f, "rest at full rate at the low end");
        C(HeartFeltMath.RestFactor(60f) < HeartFeltMath.RestFactor(40f) && Near(HeartFeltMath.RestFactor(100f), HeartFeltMath.RestLeast) && HeartFeltMath.RestLeast > 0f,
            "less the higher strain is, never nothing");

        // The two sides at once.
        C(HeartFeltMath.Growth(0f) == 1f && HeartFeltMath.Growth(50f) < 1f && Near(HeartFeltMath.Growth(100f), HeartFeltMath.GrowthLeast) && HeartFeltMath.GrowthLeast > 0f,
            "the good side grows at any strain, slower the more there is, never shut");
        float good = 50f;
        float rest = HeartMath.Spend(10f, ref good, out float spent);
        C(Near(rest, 10f * (1f - HeartMath.CushionShare)) && Near(spent, 10f * HeartMath.CushionShare * HeartMath.SpendShare) && Near(good, 50f - spent),
            "hardship spends part of the good side as a cushion and builds strain with the rest");
        good = 1f;
        rest = HeartMath.Spend(10f, ref good, out spent);
        C(good == 0f && Near(spent, 1f) && Near(rest, 10f - 1f / HeartMath.SpendShare), "the cushion goes as far as the good side does");
        good = 0f;
        C(HeartMath.Spend(10f, ref good, out _) == 10f, "with nothing on the good side, strain builds in full");
        var settled = new HeartFeltMath.Settled();
        var things = new float[HeartMath.GoodSourceCount];
        things[(int)GoodSource.Friends] = 4f;
        float left = HeartFeltMath.Settle(40f, 0f, 0f, 0f, things, settled);
        C(Near(settled.eased[(int)GoodSource.Friends], 4f * (1f - HeartFeltMath.GoodSplit)) && settled.toGood[(int)GoodSource.Friends] > 0f && Near(left, 40f - 4f * (1f - HeartFeltMath.GoodSplit)),
            "a good talk under strain eases it and fills the good side together");
        left = HeartFeltMath.Settle(0f, 0f, 0f, 0f, things, settled);
        C(left == 0f && settled.eased[(int)GoodSource.Friends] == 0f && Near(settled.toGood[(int)GoodSource.Friends], 4f * HeartMath.GoodShare(GoodSource.Friends)),
            "with no strain the whole of it goes to the good side");
        left = HeartFeltMath.Settle(40f, 35f, 20f, 0f, null, settled);
        C(Near(left, 35f) && Near(settled.rest, 5f), "rest can't take strain below the weight");
        left = HeartFeltMath.Settle(40f, 35f, 0f, 30f, null, settled);
        C(Near(left, 10f), "a cry lets it out, below the weight too");
        left = HeartFeltMath.Settle(30f, 0f, 2f, 0f, null, settled);
        float leftHigh = HeartFeltMath.Settle(70f, 0f, 2f, 0f, null, settled);
        C(30f - left > 70f - leftHigh, "the same rest eases less from higher up");

        // Telling.
        int ts = -1, tg = -1;
        C(HeartFeltMath.Tell(ref ts, ref tg, 60f, 50f, true) == HeartTell.None && ts == (int)StrainBand.Worn && tg == (int)GoodBand.GoodHeart, "the first look only takes note");
        ts = (int)StrainBand.Strained;
        tg = (int)GoodBand.None;
        C(HeartFeltMath.Tell(ref ts, ref tg, 56f, 0f, true) == HeartTell.Worn, "worn down is told");
        C(HeartFeltMath.Tell(ref ts, ref tg, 54f, 0f, true) == HeartTell.None && HeartFeltMath.Tell(ref ts, ref tg, 56f, 0f, true) == HeartTell.None, "hovering at the line isn't told twice");
        C(HeartFeltMath.Tell(ref ts, ref tg, 76f, 0f, true) == HeartTell.Breaking, "near breaking is told");
        C(HeartFeltMath.Tell(ref ts, ref tg, 60f, 0f, true) == HeartTell.None && HeartFeltMath.Tell(ref ts, ref tg, 44f, 0f, true) == HeartTell.Back, "coming back from it is told once it's well past");
        C(HeartFeltMath.Tell(ref ts, ref tg, 40f, 46f, true) == HeartTell.GoodHeart && HeartFeltMath.Tell(ref ts, ref tg, 40f, 47f, true) == HeartTell.None, "in good heart is told once");
        C(HeartFeltMath.Tell(ref ts, ref tg, 40f, 66f, true) == HeartTell.HighSpirits, "in high spirits too");
        C(HeartFeltMath.Tell(ref ts, ref tg, 40f, 30f, false) == HeartTell.None && tg == (int)GoodBand.None, "with the good side off it is never told");

        // Acts and signs.
        C(HeartFeltMath.ActChance(StrainBand.Steady, GoodBand.None, 0f) == 0f && HeartFeltMath.ActChance(StrainBand.Steady, GoodBand.Steady, 0.3f) == 0f,
            "someone steady with nothing strong does nothing out of the way");
        C(HeartFeltMath.ActChance(StrainBand.Worn, GoodBand.None, 0f) > HeartFeltMath.ActChance(StrainBand.Strained, GoodBand.None, 0f)
          && HeartFeltMath.ActChance(StrainBand.Worn, GoodBand.GoodHeart, 0f) > HeartFeltMath.ActChance(StrainBand.Worn, GoodBand.None, 0f), "both sides act, and both at once");
        C(HeartFeltMath.ActPawnHours >= 12 && HeartFeltMath.ActColonyHours >= 2, "at most an act a colonist in half a day and one in the colony in two hours");
        C(HeartFeltMath.SignNow(true, HeartFeltMath.SignHours, 0.99f) && !HeartFeltMath.SignNow(true, HeartFeltMath.SignHours - 1, 0f) && !HeartFeltMath.SignNow(false, 99, 0.99f),
            "a sign when a feeling grows strong, now and then while it holds, never too often");
    }

    // ---------------- Weeks of play ----------------

    /// <summary>One colonist in the simulation, looked at once an hour.</summary>
    private sealed class Col
    {
        public int id;
        public float strain, good, lately, weight, held;
        public int weightUntil = -99999, heldUntil = -99999;
        public readonly float[] today = new float[HeartMath.SourceCount];
        public float todayTotal, todayGathering, todayFriends, todayJoys;
        public readonly float[] goodToday = new float[HeartMath.GoodSourceCount];
        public float goodTodayTotal;
        public int lastEpisode = -99999, episodes;
        public float baseMood = 0.72f;
        public float belonging = 72f, purpose = 66f;
        public StrainTraits traits;
        public readonly HashSet<int> close = new HashSet<int>();
        public int partner = -1;
        public bool dead;
        public float sick;
        public float grief;
        // The heart, made to count: time off under way (1 a day off, 2 a bad afternoon) and its waits; held at a strain (a
        // colonist worn down for weeks, for spreading); what spread built.
        public int offKind, offUntil = -1, lastBadDay = -99999, badDays, dayOffs;
        public float holdAt = -1f, spreadBuilt, spreadMax;
        // The game's break lines (decompiled MentalBreaker: minor 35%, major 20%, extreme 5%; mean times 4, 0.8 and 0.5 days):
        // the breaks to expect, with the bands as felt now and as they were (mild), the mood otherwise the same.
        public float breaksFelt, breaksMild;
        public int underMinorFelt, underMinorMild, underMajorFelt, underMajorMild, hours;
        // Measured.
        public int steadyDay = -1, goodHeartDay = -1;
        public readonly List<float> strainByHour = new List<float>(), goodByHour = new List<float>();
        public float filledWhileStrained, hoursStrainedFilled, filledWhileSteady, hoursSteadyFilled;
    }

    private sealed class Colony
    {
        public readonly List<Col> people = new List<Col>();
        public int lastEpisode = -99999;
        public readonly Random rng;
        /// <summary>Hours of a fight at home; the hour each loss happens (who dies); hours of plague by colonist.</summary>
        public Func<int, bool> fight = _ => false;
        public readonly Dictionary<int, int> deaths = new Dictionary<int, int>();
        public readonly Dictionary<int, (int from, int peak, int to)> plague = new Dictionary<int, (int, int, int)>();
        public readonly Dictionary<int, int> downedAt = new Dictionary<int, int>();
        public int partyEvery = 5;
        /// <summary>The heart, made to count: bad afternoons and spreading on (the settings), the colony's last bad afternoon, a day off given.</summary>
        public bool badDays = true, spread = true;
        public int lastBadDay = -99999;
        public Func<Col, int, bool> dayOff = (_, _) => false;

        public Colony(int seed) => rng = new Random(seed);
    }

    private static Colony Make(int seed, int count, bool allClose)
    {
        var c = new Colony(seed);
        for (int i = 0; i < count; i++)
        {
            var p = new Col { id = i };
            p.baseMood = 0.68f + (float)c.rng.NextDouble() * 0.1f;
            p.belonging = 62f + (float)c.rng.NextDouble() * 20f;
            p.purpose = 58f + (float)c.rng.NextDouble() * 20f;
            double t = c.rng.NextDouble();
            p.traits = t < 0.1 ? StrainTraits.Nervous : t < 0.18 ? StrainTraits.Steadfast : t < 0.24 ? StrainTraits.Optimist : t < 0.3 ? StrainTraits.Pessimist : StrainTraits.None;
            c.people.Add(p);
        }
        // Friends: everyone in a small colony; in a bigger one, each with two or three, and couples.
        for (int i = 0; i < count; i++)
            for (int j = 0; j < count; j++)
                if (i != j && (allClose || Math.Abs(i - j) == 1 || (i + j) % 5 == 0))
                    c.people[i].close.Add(j);
        for (int i = 0; i + 1 < count; i += 4)
        {
            c.people[i].partner = i + 1;
            c.people[i + 1].partner = i;
            c.people[i].close.Add(i + 1);
            c.people[i + 1].close.Add(i);
        }
        return c;
    }

    private static float Sev(Colony c, int who, int hour)
    {
        if (!c.plague.TryGetValue(who, out var s) || hour < s.from || hour > s.to) return 0f;
        if (hour <= s.peak) return 0.9f * (hour - s.from) / Math.Max(1f, s.peak - s.from);
        return 0.9f * (1f - (hour - s.peak) / Math.Max(1f, s.to - s.peak));
    }

    /// <summary>The game's break lines (MentalBreaker: the threshold stat's usual 35%, 4/7 and 1/7 of it).</summary>
    private const float MinorLine = 0.35f, MajorLine = 0.2f, ExtremeLine = 0.05f;

    /// <summary>The chance of a mood-caused break in an hour at this mood, by the game's mean times (0.5, 0.8 and 4 days).</summary>
    private static float BreakChance(float mood) =>
        mood < ExtremeLine ? 1f / (0.5f * 24f) : mood < MajorLine ? 1f / (0.8f * 24f) : mood < MinorLine ? 1f / (4f * 24f) : 0f;

    /// <summary>Time off over: strain eased (in full when it ran its course, half when cut short), the weight lifted a little.</summary>
    private static void EndOff(Col p, bool full)
    {
        float eased = p.offKind == 1 ? HeartCountMath.DayOffEnd(full) : HeartCountMath.BadDayRelief(p.strain, full);
        eased = Math.Min(p.strain, eased * HeartMath.ReliefTraitFactor(p.traits));
        p.strain = HeartMath.Clamp(p.strain - eased);
        p.weight = Math.Max(0f, p.weight - eased * HeartFeltMath.GoodEasesWeight);
        p.held = Math.Max(0f, p.held - eased * HeartFeltMath.GoodEasesWeight);
        p.offKind = 0;
        p.offUntil = -1;
    }

    private static readonly float[] goodTmp = new float[HeartMath.GoodSourceCount];
    private static readonly HeartFeltMath.Settled settledTmp = new HeartFeltMath.Settled();

    /// <summary>One hour of one colonist, as HeartLogic.Evaluate and GoodHeart.Hour do it, with the colony's day around them.</summary>
    private static void Hour(Colony c, Col p, int hour)
    {
        var rng = c.rng;
        int hod = hour % 24;
        if (hod == 0)
        {
            Array.Clear(p.today, 0, p.today.Length);
            p.todayTotal = p.todayGathering = p.todayFriends = p.todayJoys = 0f;
            Array.Clear(p.goodToday, 0, p.goodToday.Length);
            p.goodTodayTotal = 0f;
        }
        float built = 0f;
        float Build(StrainSource s, float raw)
        {
            int i = (int)s;
            float today = p.today[i];
            float added = HeartMath.Add(raw, 1f, HeartMath.TraitFactor(s, p.traits), ref today, HeartMath.SourceCap(s), ref p.todayTotal, HeartMath.DailyCap);
            p.today[i] = today;
            added = HeartMath.Spend(added, ref p.good, out _);
            built += added;
            return added;
        }

        p.sick = Sev(c, p.id, hour);
        bool sick = p.sick > 0.05f;
        bool fight = c.fight(hour);
        // Time off: over when its hours are up (eased in full), cut short by a fight (half); a day off given now.
        if (p.offUntil >= 0 && (hour >= p.offUntil || fight))
            EndOff(p, hour >= p.offUntil && !fight);
        if (p.offUntil < 0 && c.dayOff(p, hour))
        {
            p.offKind = 1;
            p.offUntil = hour + HeartCountMath.DayOffHours;
            p.dayOffs++;
        }
        bool dayOff = p.offKind == 1 && p.offUntil > hour, badDay = p.offKind == 2 && p.offUntil > hour;
        bool asleep = sick ? hod < 7 || hod >= 20 || (hod >= 13 && hod < 15) : hod < 6 || hod >= 22;
        // A day off's lie-in: the first hours after the night slept in.
        if (dayOff && hod >= 6 && hod < 6 + HeartCountMath.LieInHours) asleep = true;
        bool down = c.downedAt.TryGetValue(p.id, out int dAt) && hour >= dAt && hour < dAt + 6;

        // The everyday trickle: small letdowns (their cap), a bad night now and then, the dark, a missed meal.
        Build(StrainSource.Letdowns, 0.0625f);
        if (hod == 6 && rng.NextDouble() < 0.25) Build(StrainSource.BadSleep, 2f);
        if (hod == 21 && rng.NextDouble() < 0.15) Build(StrainSource.Dark, 0.5f);
        if (hod == 13 && rng.NextDouble() < 0.1) Build(StrainSource.Hunger, 0.75f);
        // Sickness: pain and low health; a fight: fighting and fear; downed.
        if (sick)
        {
            Build(StrainSource.Pain, (0.25f + 0.4f * p.sick) * 5f);
            if (p.sick > 0.5f) Build(StrainSource.Pain, 1f);
        }
        if (fight)
        {
            Build(StrainSource.Combat, 4f);
            Build(StrainSource.Fear, 0.3f);
        }
        if (down) Build(StrainSource.Downed, 3f);
        float rest0 = p.baseMood - (sick ? 0.15f + 0.2f * p.sick : 0f) - (p.grief > 0f ? 0.12f : 0f);
        var sband = HeartMath.BandFor(p.strain);
        var gband = HeartMath.GoodBandFor(p.good);
        float mood = rest0 + (HeartMath.MoodOffset(sband) + HeartMath.GoodMood(gband)) / 100f;
        float moodMild = rest0 + (HeartCountMath.StrainMood(sband, BandStrength.Mild) + HeartCountMath.GoodMood(gband, BandStrength.Mild)) / 100f;
        p.breaksFelt += BreakChance(mood);
        p.breaksMild += BreakChance(moodMild);
        if (mood < MinorLine) p.underMinorFelt++;
        if (moodMild < MinorLine) p.underMinorMild++;
        if (mood < MajorLine) p.underMajorFelt++;
        if (moodMild < MajorLine) p.underMajorMild++;
        p.hours++;
        if (mood < 0.2f) Build(StrainSource.LowMood, 1f);
        else if (mood < 0.3f) Build(StrainSource.LowMood, 0.5f);
        if (p.grief > 0f)
        {
            Build(StrainSource.Grief, 0.25f);
            p.grief -= 1f;
        }
        // Hearts spread a little: someone close worn down wears on them (never past the ceiling), someone in high spirits lifts them.
        float lifted = 0f;
        if (c.spread)
        {
            float wear = 0f, up = 0f;
            foreach (int o in p.close)
            {
                var other = c.people[o];
                if (other.dead) continue;
                float closeness = other.id == p.partner ? 1f : 0.6f;
                wear += HeartCountMath.SpreadFrom(HeartMath.BandFor(other.strain), closeness);
                up += HeartCountMath.LiftFrom(HeartMath.GoodBandFor(other.good), closeness);
            }
            float room = HeartCountMath.Spread(wear, p.strain + built, 1f);
            float share = HeartMath.TraitFactor(StrainSource.Spread, p.traits);
            if (room > 0f && share > 0f)
            {
                float a = Build(StrainSource.Spread, room / Math.Max(1f, share));
                p.spreadBuilt += a;
            }
            lifted = HeartCountMath.Lift(up, 1f);
        }

        // Heavy things: a death (each memory of it once; the heaviest weighs), being downed, the plague in the house.
        foreach (var kv in c.deaths)
        {
            if (kv.Value != hour || kv.Key == p.id) continue;
            var dead = c.people[kv.Key];
            bool partner = p.partner == dead.id, close = p.close.Contains(dead.id);
            Build(StrainSource.Loss, 4f);
            if (close) Build(StrainSource.Loss, partner ? 8f : 4f);
            float w = HeartFeltMath.LossWeight(partner ? "MyWifeDied" : close ? "PawnWithGoodOpinionDied" : "KnowColonistDied");
            p.weight = HeartFeltMath.Combine(p.weight, w);
            p.weightUntil = Math.Max(p.weightUntil, hour + (int)(HeartFeltMath.HoldDaysFor(p.weight) * 24f));
            if (close) p.grief = 24f * (partner ? 8f : 5f);
        }
        if (down && dAt == hour)
        {
            p.weight = HeartFeltMath.Combine(p.weight, HeartFeltMath.DownedWeight);
            p.weightUntil = Math.Max(p.weightUntil, hour + (int)(HeartFeltMath.HoldDaysFor(p.weight) * 24f));
        }
        float target = HeartFeltMath.SickSelfWeight(p.sick / 0.95f);
        foreach (int o in p.close)
        {
            var other = c.people[o];
            if (other.dead) continue;
            target = HeartFeltMath.Combine(target, HeartFeltMath.SickCloseWeight(Sev(c, o, hour) / 0.95f));
        }
        if (fight)
        {
            int downedNow = c.people.Count(q => !q.dead && c.downedAt.TryGetValue(q.id, out int at) && hour >= at && hour < at + 6);
            target = HeartFeltMath.Combine(target, HeartFeltMath.RaidWeight(downedNow));
        }
        if (target > 0f && target >= p.held - 1f)
        {
            p.held = Math.Max(p.held, target);
            p.heldUntil = hour + (int)(HeartFeltMath.HeldHoldDays * 24f);
        }
        float floor = HeartFeltMath.Floor(p.weight, p.held);
        float sink = HeartFeltMath.SinkIn(p.strain + built, floor, 1f);
        if (sink > 0f) built += HeartMath.Spend(sink, ref p.good, out _);

        // Rest: sleep in a warm bed, quiet hours, an evening's recreation; faster in a good mood and for someone in good heart lately.
        bool calm = !fight && !down;
        // Time off: at leisure all their waking hours (recreation); a day off eases faster and a little more each hour.
        bool leisure = (dayOff || badDay) && !asleep;
        float quiet = calm && !asleep ? 0.15f : 0f, sleep = asleep ? 0.6f : 0f, rec = !asleep && !sick && (leisure || hod == 19 || hod == 20) ? 0.5f : 0f;
        float factor = mood >= 0.85f ? 1.5f : mood >= 0.7f ? 1.25f : mood < 0.3f ? 0.7f : 1f;
        factor *= HeartMath.ReliefTraitFactor(p.traits) * HeartMath.RecoveryFactor(HeartMath.GoodBandFor(p.lately));
        if (dayOff) factor *= HeartCountMath.DayOffRestFactor;
        float restNow = (quiet + sleep + rec) * factor + (dayOff && calm ? HeartCountMath.DayOffPerHour * factor : 0f);

        // Good things: talks with friends now and then, a party every few days, small joys, the comforts of home.
        Array.Clear(goodTmp, 0, goodTmp.Length);
        float reliefTraits = HeartMath.ReliefTraitFactor(p.traits);
        if (!asleep && rng.NextDouble() < (sick ? 0.06 : 0.14))
        {
            float a = Math.Min(2f * reliefTraits, Math.Max(0f, 6f - p.todayFriends));
            p.todayFriends += a;
            goodTmp[(int)GoodSource.Friends] += a;
        }
        if (!sick && !fight && hod == 19 && (hour / 24) % c.partyEvery == 2)
        {
            float a = Math.Min(6f * reliefTraits, Math.Max(0f, 12f - p.todayGathering));
            p.todayGathering += a;
            goodTmp[(int)GoodSource.Gathering] += a;
        }
        if (!asleep && rng.NextDouble() < 0.06)
        {
            float a = Math.Min(1.2f, Math.Max(0f, 3f - p.todayJoys));
            p.todayJoys += a;
            goodTmp[(int)GoodSource.Joys] += a;
        }
        if (!sick) goodTmp[(int)GoodSource.Comforts] += 0.06f;
        goodTmp[(int)GoodSource.Lifted] += lifted;

        float before = p.strain + built;
        float left = HeartFeltMath.Settle(before, floor, restNow, 0f, goodTmp, settledTmp);
        float lift = settledTmp.GoodEased * HeartFeltMath.GoodEasesWeight;
        p.weight = Math.Max(0f, p.weight - lift);
        p.held = Math.Max(0f, p.held - lift);
        p.strain = HeartMath.Clamp(left);

        // The good side: what the good things bring, belonging and purpose well met, slower under strain; it fades.
        float filled = 0f;
        float Fill(GoodSource s, float amount)
        {
            int i = (int)s;
            float st = p.goodToday[i];
            float a = HeartMath.Fill(amount, ref p.good, ref st, HeartMath.GoodCap(s), ref p.goodTodayTotal, HeartMath.GoodDailyCap);
            p.goodToday[i] = st;
            filled += a;
            return a;
        }
        for (int i = 0; i < HeartMath.GoodSourceCount; i++)
            if (settledTmp.toGood[i] > 0f) Fill((GoodSource)i, settledTmp.toGood[i]);
        float growth = HeartFeltMath.Growth(p.strain);
        float belonging = p.belonging - (p.grief > 0f ? 15f : 0f), purpose = p.purpose - (sick ? 20f : 0f);
        Fill(GoodSource.Belonging, HeartMath.NeedFillPerHour(belonging) * growth);
        Fill(GoodSource.Purpose, HeartMath.NeedFillPerHour(purpose) * growth);
        if (hod == 12 && rng.NextDouble() < 0.4) Fill(GoodSource.Purpose, HeartMath.GoalMovedFill * growth);
        p.good = HeartMath.Faded(p.good, 1f);
        if (p.good < 0.05f) p.good = 0f;
        p.lately = HeartMath.Lately(p.lately, p.good, 1f);
        p.weight = HeartFeltMath.Eased(p.weight, hour < p.weightUntil, 1f);
        p.held = HeartFeltMath.Eased(p.held, hour < p.heldUntil, 1f);

        // The day off: at 85 and past, by its own chance and cooldowns.
        if (p.strain >= HeartMath.EpisodeAt && !sick && !fight && hour - p.lastEpisode >= 240 && hour - c.lastEpisode >= 48
            && rng.NextDouble() < HeartMath.EpisodeChancePerHour(p.strain))
        {
            p.episodes++;
            p.lastEpisode = c.lastEpisode = hour;
            p.strain = HeartMath.Clamp(p.strain - HeartMath.EpisodeRelief);
            p.weight = Math.Max(0f, p.weight - HeartMath.EpisodeRelief * HeartFeltMath.GoodEasesWeight);
            p.held = Math.Max(0f, p.held - HeartMath.EpisodeRelief * HeartFeltMath.GoodEasesWeight);
        }

        // A bad afternoon from worn down: in the afternoon, safe (not ill, no fight), its waits past, by its chance.
        if (c.badDays && p.offUntil < 0 && !sick && !fight && !down && hod >= HeartCountMath.BadDayStartHour && hod <= HeartCountMath.BadDayLastHour
            && hour - p.lastBadDay >= HeartCountMath.BadDayPawnDays * 24 && hour - c.lastBadDay >= HeartCountMath.BadDayColonyDays * 24
            && rng.NextDouble() < HeartCountMath.BadDayChancePerHour(p.strain))
        {
            p.offKind = 2;
            p.offUntil = hour + 4;
            p.lastBadDay = c.lastBadDay = hour;
            p.badDays++;
        }
        // Held worn down for weeks (spreading's runaway test): a long weight that keeps them there.
        if (p.holdAt >= 0f && p.strain < p.holdAt) p.strain = p.holdAt;

        if (p.strain >= HeartMath.BandStart(StrainBand.Strained))
        {
            p.filledWhileStrained += filled;
            p.hoursStrainedFilled++;
        }
        else
        {
            p.filledWhileSteady += filled;
            p.hoursSteadyFilled++;
        }
        p.strainByHour.Add(p.strain);
        p.goodByHour.Add(p.good);
        if (p.holdAt < 0f) p.spreadMax = Math.Max(p.spreadMax, p.strain);
        if (p.steadyDay < 0 && p.good >= HeartMath.SteadyAt) p.steadyDay = hour / 24;
        if (p.goodHeartDay < 0 && p.good >= HeartMath.GoodHeartAt) p.goodHeartDay = hour / 24;
    }

    private static void RunDays(Colony c, int days)
    {
        for (int hour = 0; hour < days * 24; hour++)
            foreach (var p in c.people)
            {
                if (p.dead) continue;
                if (c.deaths.TryGetValue(p.id, out int at) && hour >= at)
                {
                    p.dead = true;
                    continue;
                }
                Hour(c, p, hour);
            }
    }

    private static float Median(List<float> xs)
    {
        if (xs.Count == 0) return float.NaN;
        var s = xs.OrderBy(x => x).ToList();
        return s[s.Count / 2];
    }

    private static int HoursAtLeast(Col p, float v, int from, int to)
    {
        int h = 0;
        for (int i = from; i < to && i < p.strainByHour.Count; i++)
            if (p.strainByHour[i] >= v) h++;
        return h;
    }

    private static void Simulate(Action<bool, string> C)
    {
        const int runs = 40;

        // ---- A calm, well-run colony: eight colonists, six weeks ----
        var steadyDays = new List<float>();
        var goodHeartDays = new List<float>();
        int strainedHours = 0, allHours = 0, calmEpisodes = 0;
        float maxCalm = 0f;
        for (int r = 0; r < runs; r++)
        {
            var c = Make(100 + r, 8, false);
            RunDays(c, 42);
            foreach (var p in c.people)
            {
                steadyDays.Add(p.steadyDay < 0 ? 99 : p.steadyDay);
                goodHeartDays.Add(p.goodHeartDay < 0 ? 99 : p.goodHeartDay);
                strainedHours += HoursAtLeast(p, HeartMath.BandStart(StrainBand.Strained), 0, p.strainByHour.Count);
                allHours += p.strainByHour.Count;
                calmEpisodes += p.episodes;
                maxCalm = Math.Max(maxCalm, p.strainByHour.Max());
            }
        }
        float steadyMedian = Median(steadyDays), goodHeartMedian = Median(goodHeartDays);
        float steadyByWeek = steadyDays.Count(d => d <= 8) / (float)steadyDays.Count;
        Console.WriteLine($"  A calm colony: steady on day {steadyMedian:0} (median; {steadyByWeek:P0} within eight days), in good heart on day {goodHeartMedian:0}, "
                          + $"strained {strainedHours / (float)allHours:P1} of the hours (highest strain {maxCalm:0}), {calmEpisodes} days off in {runs} runs.");
        C(steadyMedian >= 4f && steadyMedian <= 9f && steadyByWeek >= 0.6f, $"a calm colony: most steady within about a week (median day {steadyMedian})");
        C(goodHeartMedian >= 12f && goodHeartMedian <= 22f, $"and in good heart in two to three weeks (median day {goodHeartMedian})");
        C(strainedHours / (float)allHours < 0.01f && calmEpisodes == 0, "a calm colony is never strained to speak of, and never takes a day off");

        // ---- One death of a friend: day 21, in a calm colony of eight ----
        var closeDays = new List<float>();
        var partnerDays = new List<float>();
        var restMax = new List<float>();
        int restStrained = 0;
        float fillStrained = 0f, hStrained = 0f, fillSteady = 0f, hSteady = 0f;
        for (int r = 0; r < runs; r++)
        {
            var c = Make(300 + r, 8, false);
            int deadId = 1, at = 21 * 24 + 15;
            c.deaths[deadId] = at;
            RunDays(c, 42);
            var dead = c.people[deadId];
            foreach (var p in c.people)
            {
                if (p.id == deadId) continue;
                float days = HoursAtLeast(p, HeartMath.BandStart(StrainBand.Strained), at, p.strainByHour.Count) / 24f;
                if (p.partner == deadId) partnerDays.Add(days);
                else if (p.close.Contains(deadId))
                {
                    closeDays.Add(days);
                    fillStrained += p.filledWhileStrained;
                    hStrained += p.hoursStrainedFilled;
                }
                else
                {
                    restMax.Add(p.strainByHour.Skip(at).Max());
                    if (days > 0f) restStrained++;
                    fillSteady += p.filledWhileSteady;
                    hSteady += p.hoursSteadyFilled;
                }
            }
        }
        float closeMedian = Median(closeDays), partnerMedian = Median(partnerDays), restMedian = Median(restMax);
        float perDayStrained = hStrained > 0f ? fillStrained / hStrained * 24f : 0f, perDaySteady = hSteady > 0f ? fillSteady / hSteady * 24f : 0f;
        Console.WriteLine($"  One death: a friend strained {closeMedian:0.0} days (median), a partner {partnerMedian:0.0}; the rest at {restMedian:0} at most (median), "
                          + $"{restStrained} of {restMax.Count} strained at all. The good side fills {perDayStrained:0.0} a day while strained, {perDaySteady:0.0} while not.");
        C(closeMedian >= 5f && closeMedian <= 9f, $"one death: a friend strained for about a week ({closeMedian:0.0} days)");
        C(partnerMedian >= closeMedian && partnerMedian <= 14f, $"a partner longer, within two weeks ({partnerMedian:0.0} days)");
        C(restMedian < HeartMath.BandStart(StrainBand.Strained) && restStrained <= restMax.Count / 10, $"the rest barely touched (at most {restMedian:0})");
        C(perDayStrained > 0.3f && perDayStrained < perDaySteady, $"the good side still grows while strained, slower ({perDayStrained:0.0} a day against {perDaySteady:0.0})");

        // ---- A plague season like the save's: four close friends, two of them ill for ten days ----
        var wornShare = new List<float>();
        int plagueBreaking = 0, plagueEpisodes = 0, plagueColonists = 0;
        float plagueGoodHeart = 0f;
        for (int r = 0; r < runs; r++)
        {
            var c = Make(500 + r, 4, true);
            int from = 20 * 24, peak = from + 7 * 24, to = peak + 3 * 24;
            c.plague[0] = (from, peak, to);
            c.plague[1] = (from + 12, peak + 12, to + 12);
            RunDays(c, 36);
            int worn = 0;
            foreach (var p in c.people)
            {
                plagueColonists++;
                // Worn for its length: three days or more of the season (from the first sick day to two days after the last).
                if (HoursAtLeast(p, HeartMath.BandStart(StrainBand.Worn), from, to + 12 + 48) >= 72) worn++;
                if (HoursAtLeast(p, HeartMath.BandStart(StrainBand.Breaking), from, p.strainByHour.Count) > 0) plagueBreaking++;
                plagueEpisodes += p.episodes;
                if (p.goodByHour.Skip(from).Take(to - from).Any(g => g >= HeartMath.GoodHeartAt)) plagueGoodHeart++;
            }
            wornShare.Add(worn / 4f);
        }
        float wornMedian = Median(wornShare);
        Console.WriteLine($"  A plague season: {wornMedian:P0} of the colony worn three days or more (median), {plagueBreaking} of {plagueColonists} near breaking, "
                          + $"{plagueEpisodes} days off; {plagueGoodHeart / plagueColonists:P0} still in good heart at some point in it.");
        C(wornMedian >= 0.75f, $"a plague season: most worn for its length ({wornMedian:P0})");
        C(plagueBreaking <= plagueColonists / 10 && plagueEpisodes == 0, "but nobody breaking without deaths");

        // ---- Real disasters: a plague with deaths; a raid that kills two and downs a third, then a siege ----
        int disBreaking = 0, disEpisodes = 0, disPeople = 0, disRuns = 0;
        for (int r = 0; r < runs; r++)
        {
            var c = Make(700 + r, 6, true);
            int from = 20 * 24, peak = from + 7 * 24, to = peak + 3 * 24;
            c.plague[0] = (from, peak, to);
            c.plague[1] = (from + 12, peak + 12, to + 12);
            c.plague[2] = (from + 30, peak + 20, to + 20);
            c.deaths[0] = peak + 6;
            c.deaths[1] = peak + 30;
            RunDays(c, 40);
            bool any = false;
            foreach (var p in c.people.Where(q => !c.deaths.ContainsKey(q.id)))
            {
                disPeople++;
                if (HoursAtLeast(p, HeartMath.BandStart(StrainBand.Breaking), from, p.strainByHour.Count) > 0)
                {
                    disBreaking++;
                    any = true;
                }
                disEpisodes += p.episodes;
            }
            if (any) disRuns++;
        }
        int plagueDeathEpisodes = disEpisodes;
        for (int r = 0; r < runs; r++)
        {
            var c = Make(900 + r, 6, true);
            int at = 20 * 24 + 10;
            c.fight = h => (h >= at && h < at + 5) || (h >= at + 48 && h < at + 48 + 30);
            c.deaths[0] = at + 2;
            c.deaths[1] = at + 3;
            c.downedAt[2] = at + 2;
            c.downedAt[3] = at + 50;
            RunDays(c, 34);
            bool any = false;
            foreach (var p in c.people.Where(q => !c.deaths.ContainsKey(q.id)))
            {
                disPeople++;
                if (HoursAtLeast(p, HeartMath.BandStart(StrainBand.Breaking), at, p.strainByHour.Count) > 0)
                {
                    disBreaking++;
                    any = true;
                }
                disEpisodes += p.episodes;
            }
            if (any) disRuns++;
        }
        Console.WriteLine($"  Real disasters (a plague with two deaths, a raid with two dead then a siege): someone near breaking in {disRuns} of {runs * 2} runs, "
                          + $"{disBreaking} of {disPeople} colonists; {disEpisodes} days off ({plagueDeathEpisodes} after the plague, {disEpisodes - plagueDeathEpisodes} after the raid), "
                          + $"{disEpisodes / (float)(runs * 2):0.0} a disaster.");
        C(disRuns >= runs, $"breaking after a real disaster ({disRuns} of {runs * 2} runs)");
        C(disEpisodes > 0 && disEpisodes <= runs * 2 * 3 / 2, $"the day off rare but possible in a disaster ({disEpisodes / (float)(runs * 2):0.0} a disaster)");
    }

    // ================= Release 2: the heart, made to count =================

    private static void CountRules(Action<bool, string> C)
    {
        bool Near(float a, float b, float tol = 0.0005f) => Math.Abs(a - b) <= tol;

        // 9. Stronger bands.
        C(HeartMath.MoodOffset(StrainBand.Strained) == -4f && HeartMath.MoodOffset(StrainBand.Worn) == -8f && HeartMath.MoodOffset(StrainBand.Breaking) == -14f,
            "strain's bands: -4, -8, -14");
        C(HeartMath.GoodMood(GoodBand.Steady) == 3f && HeartMath.GoodMood(GoodBand.GoodHeart) == 6f && HeartMath.GoodMood(GoodBand.HighSpirits) == 10f,
            "the good side's: +3, +6, +10");
        C(HeartCountMath.StrainMood(StrainBand.Worn, BandStrength.Mild) == -5f && HeartCountMath.StrainMood(StrainBand.Breaking, BandStrength.Strong) == -18f
          && HeartCountMath.StrainMood(StrainBand.Steady, BandStrength.Strong) == 0f, "mild as before, strong stronger, steady nothing");
        foreach (BandStrength s in Enum.GetValues(typeof(BandStrength)))
        {
            C(HeartCountMath.GoodMood(GoodBand.Steady, s) < -HeartCountMath.StrainMood(StrainBand.Strained, s)
              && HeartCountMath.GoodMood(GoodBand.HighSpirits, s) < -HeartCountMath.StrainMood(StrainBand.Breaking, s), s + ": each good band smaller than strain's across from it");
            C(HeartCountMath.StrainMood(StrainBand.Strained, s) > HeartCountMath.StrainMood(StrainBand.Worn, s)
              && HeartCountMath.StrainMood(StrainBand.Worn, s) > HeartCountMath.StrainMood(StrainBand.Breaking, s), s + ": worse with each band");
        }

        // 10. Effects beyond mood, mild, within bounds.
        C(Near(HeartCountMath.Work(StrainBand.Strained, true), 0.97f) && Near(HeartCountMath.Work(StrainBand.Worn, true), 0.93f) && Near(HeartCountMath.Work(StrainBand.Breaking, true), 0.88f)
          && HeartCountMath.Work(StrainBand.Steady, true) == 1f, "work from the first band: 3%, 7%, 12% slower");
        foreach (StrainBand sb in Enum.GetValues(typeof(StrainBand)))
            foreach (GoodBand gb in Enum.GetValues(typeof(GoodBand)))
            {
                float imm = HeartCountMath.Factor(HeartEffect.Immunity, sb, gb, true), heal = HeartCountMath.Factor(HeartEffect.Healing, sb, gb, true);
                float learn = HeartCountMath.Factor(HeartEffect.Learning, sb, gb, true), rest = HeartCountMath.Factor(HeartEffect.Rest, sb, gb, true);
                C(imm >= HeartCountMath.ImmunitySlowest - 1e-5f && imm <= 1.1f, $"immunity at {sb}/{gb} never more than 5% slower ({imm:0.000}): the plague");
                C(heal >= 0.9f - 1e-5f && heal <= 1.1f && learn >= 0.9f - 1e-5f && learn <= 1.1f && rest >= 1f && rest <= 1.1f, $"healing, learning and sleep at {sb}/{gb} within a tenth");
                foreach (HeartEffect e in Enum.GetValues(typeof(HeartEffect)))
                    if (e != HeartEffect.Work) C(HeartCountMath.Factor(e, sb, gb, false) == 1f, $"{e} at {sb}/{gb}: nothing with the effects beyond mood off");
            }
        C(HeartCountMath.Factor(HeartEffect.Immunity, StrainBand.Breaking, GoodBand.None, true) == HeartCountMath.ImmunitySlowest && HeartCountMath.ImmunitySlowest >= 0.95f,
            "near breaking, immunity 5% slower at most");
        C(HeartCountMath.Factor(HeartEffect.Learning, StrainBand.Worn, GoodBand.None, true) < 1f && HeartCountMath.Factor(HeartEffect.Rest, StrainBand.Worn, GoodBand.None, true) > 1f
          && HeartCountMath.Factor(HeartEffect.Healing, StrainBand.Worn, GoodBand.None, true) < 1f, "the worn learn slower, need more sleep and heal slower");
        C(HeartCountMath.Factor(HeartEffect.Learning, StrainBand.Steady, GoodBand.HighSpirits, true) > 1f && HeartCountMath.Factor(HeartEffect.Healing, StrainBand.Steady, GoodBand.GoodHeart, true) > 1f
          && HeartCountMath.Factor(HeartEffect.Immunity, StrainBand.Steady, GoodBand.HighSpirits, true) > 1f, "in good heart and high spirits a little faster");
        C(HeartCountMath.GoodSide(HeartEffect.Work, GoodBand.HighSpirits) == 1f && HeartCountMath.GoodSide(HeartEffect.Rest, GoodBand.HighSpirits) == 1f,
            "the good side never touches work or sleep");
        C(HeartCountMath.Factor(HeartEffect.Learning, StrainBand.Strained, GoodBand.None, true) == 1f && HeartCountMath.Factor(HeartEffect.Immunity, StrainBand.Strained, GoodBand.None, true) == 1f,
            "strained: only work, the rest from worn");

        // 11. A bad afternoon.
        C(HeartCountMath.BadDayChancePerHour(54f) == 0f && HeartCountMath.BadDayChancePerHour(55f) > 0f
          && HeartCountMath.BadDayChancePerHour(90f) > HeartCountMath.BadDayChancePerHour(60f) && HeartCountMath.BadDayChancePerHour(100f) <= 0.05f,
            "a bad afternoon only from worn down, likelier the more worn, rare");
        C(HeartCountMath.BadDayPawnDays >= 5f && HeartCountMath.BadDayColonyDays >= 1f && HeartCountMath.BadDayMaxHours <= 6 && HeartCountMath.BadDayMinHours >= 2,
            "a few hours, at most once in so many days");
        C(Near(HeartCountMath.BadDayRelief(60f, true), 18f) && Near(HeartCountMath.BadDayRelief(60f, false), 9f), "it eases a good share of strain, half when cut short");

        // 14. A day off.
        C(HeartCountMath.DayOffFrom == HeartMath.BandStart(StrainBand.Strained) && HeartCountMath.DayOffHours == 24 && HeartCountMath.DayOffCooldownDays >= 3f,
            "a day off from strained, for a day, once in a while");
        C(HeartCountMath.DayOffReadyIn(0f) == HeartCountMath.DayOffCooldownDays && HeartCountMath.DayOffReadyIn(99f) == 0f && HeartCountMath.DayOffEnd(true) > HeartCountMath.DayOffEnd(false),
            "its wait and its easing");

        // 12. Hearts spread a little.
        C(HeartCountMath.Closeness(true, false, 0f) == 1f && HeartCountMath.Closeness(false, true, 0f) == 0.8f && HeartCountMath.Closeness(false, false, 19f) == 0f
          && HeartCountMath.Closeness(false, false, 80f) > HeartCountMath.Closeness(false, false, 20f), "closeness: a partner, kin, a friend by opinion; nobody else");
        C(HeartCountMath.SpreadFrom(StrainBand.Strained, 1f) == 0f && HeartCountMath.SpreadFrom(StrainBand.Worn, 1f) > 0f
          && HeartCountMath.SpreadFrom(StrainBand.Breaking, 1f) > HeartCountMath.SpreadFrom(StrainBand.Worn, 1f) && HeartCountMath.SpreadFrom(StrainBand.Worn, 0f) == 0f,
            "only the worn down spread, the more the closer");
        C(HeartCountMath.SpreadCeiling < HeartMath.BandStart(StrainBand.Worn) && HeartCountMath.Spread(5f, HeartCountMath.SpreadCeiling - 0.05f, 1f) <= 0.05f + 1e-5f
          && HeartCountMath.Spread(5f, HeartCountMath.SpreadCeiling + 3f, 1f) == 0f && HeartCountMath.Spread(5f, 0f, 1f) == HeartCountMath.SpreadHourCap,
            "spreading never past the ceiling, short of worn down, and within its hour's cap: it can't make anyone worn on its own");
        C(HeartCountMath.LiftFrom(GoodBand.HighSpirits, 1f) > 0f && HeartCountMath.LiftFrom(GoodBand.GoodHeart, 1f) == 0f && HeartCountMath.Lift(9f, 1f) == HeartCountMath.LiftHourCap,
            "only high spirits lift, within an hour's cap");
        C(HeartMath.SourceCap(StrainSource.Spread) == HeartCountMath.SpreadDailyCap && HeartCountMath.SpreadDailyCap <= 3f && HeartMath.GoodCap(GoodSource.Lifted) < 1f,
            "both small a day");
        C(HeartMath.TraitFactor(StrainSource.Spread, StrainTraits.Psychopath) == 0f, "a psychopath doesn't take it on");

        // 13. Advice, ranked from what wears on them now.
        var f = new HeartCountMath.AdviceFacts { strain = 61f };
        f.recent[(int)StrainSource.Combat] = 14f;
        f.recent[(int)StrainSource.Grief] = 6f;
        f.recent[(int)StrainSource.Pain] = 9f;
        f.recent[(int)StrainSource.Exhaustion] = 5f;
        f.recent[(int)StrainSource.Hunger] = 4f;
        f.inPain = true;
        var ranked = HeartCountMath.Rank(f);
        C(ranked.Count >= 4 && ranked[0].kind == AdviceKind.DayOff, "Avila at 61 (fighting, grief, pain, exhaustion, hunger): a day off first");
        C(ranked.Any(r => r.kind == AdviceKind.Pain) && ranked.Any(r => r.kind == AdviceKind.Peace) && ranked.Any(r => r.kind == AdviceKind.Meal),
            "then her pain treated, a rest from fighting and a proper meal");
        C(ranked.FindIndex(r => r.kind == AdviceKind.Pain) < ranked.FindIndex(r => r.kind == AdviceKind.Meal), "the most easing first");
        f.dayOff = false;
        C(HeartCountMath.Rank(f).All(r => r.kind != AdviceKind.DayOff), "no day off offered with its setting off");
        var calm = new HeartCountMath.AdviceFacts { strain = 16f };
        C(HeartCountMath.Rank(calm).Count == 0, "Bautista at 16: nothing to say");
        C(HeartCountMath.DecreeWeight(f, null) > HeartCountMath.DecreeWeight(f, new[] { StrainSource.Hunger }) && HeartCountMath.DecreeWeight(f, new[] { StrainSource.Dark }) == 0f,
            "days of rest ease all of strain; another decree only what it answers");
    }

    private static void CountSimulate(Action<bool, string> C)
    {
        const int runs = 40;
        Console.WriteLine("  The heart, made to count: bands strain -4/-8/-14 (mild -3/-5/-8, strong -5/-10/-18), the good side +3/+6/+10 (mild +2/+3/+5, strong +4/+8/+13).");

        // Breaks by the game's own lines and bad afternoons, in the calm colony, the plague and the disasters.
        (float felt, float mild, float minorFelt, float minorMild, float majorFelt, float majorMild, float bad, float years) Measure(IEnumerable<Col> people, int days)
        {
            float felt = 0f, mild = 0f, bad = 0f, years = 0f;
            int minorF = 0, minorM = 0, majorF = 0, majorM = 0, hours = 0;
            foreach (var p in people)
            {
                felt += p.breaksFelt;
                mild += p.breaksMild;
                bad += p.badDays;
                minorF += p.underMinorFelt;
                minorM += p.underMinorMild;
                majorF += p.underMajorFelt;
                majorM += p.underMajorMild;
                hours += p.hours;
            }
            years = hours / (24f * 60f);
            return (felt / years, mild / years, minorF / (float)Math.Max(1, hours), minorM / (float)Math.Max(1, hours), majorF / (float)Math.Max(1, hours),
                majorM / (float)Math.Max(1, hours), bad / years, years);
        }

        var calmPeople = new List<Col>();
        for (int r = 0; r < runs; r++)
        {
            var c = Make(100 + r, 8, false);
            RunDays(c, 42);
            calmPeople.AddRange(c.people);
        }
        var calm = Measure(calmPeople, 42);
        Console.WriteLine($"  A calm colony: {calm.felt:0.00} breaks a colonist-year by the game's lines (mild bands {calm.mild:0.00}), under the minor line {calm.minorFelt:P1} "
                          + $"({calm.minorMild:P1}); {calm.bad:0.00} bad afternoons a colonist-year.");
        C(calm.bad == 0f, "a calm colony: never a bad afternoon");
        C(calm.felt <= calm.mild + 0.05f && calm.felt < 0.2f, $"a calm colony barely breaks more ({calm.felt:0.00} against {calm.mild:0.00} a colonist-year)");

        var plaguePeople = new List<Col>();
        for (int r = 0; r < runs; r++)
        {
            var c = Make(500 + r, 4, true);
            int from = 20 * 24, peak = from + 7 * 24, to = peak + 3 * 24;
            c.plague[0] = (from, peak, to);
            c.plague[1] = (from + 12, peak + 12, to + 12);
            RunDays(c, 36);
            plaguePeople.AddRange(c.people);
        }
        var plague = Measure(plaguePeople, 36);
        Console.WriteLine($"  A plague season: {plague.felt:0.00} breaks a colonist-year (mild bands {plague.mild:0.00}), under the minor line {plague.minorFelt:P1} ({plague.minorMild:P1}), "
                          + $"under the major {plague.majorFelt:P1} ({plague.majorMild:P1}); {plague.bad:0.00} bad afternoons a colonist-year.");
        C(plague.felt >= plague.mild, "a plague season: the worn down break more often, as they should");

        var disPeople = new List<Col>();
        int disRuns = 0;
        for (int r = 0; r < runs; r++)
        {
            var c = Make(700 + r, 6, true);
            int from = 20 * 24, peak = from + 7 * 24, to = peak + 3 * 24;
            c.plague[0] = (from, peak, to);
            c.plague[1] = (from + 12, peak + 12, to + 12);
            c.plague[2] = (from + 30, peak + 20, to + 20);
            c.deaths[0] = peak + 6;
            c.deaths[1] = peak + 30;
            RunDays(c, 40);
            disPeople.AddRange(c.people.Where(q => !c.deaths.ContainsKey(q.id)));
            disRuns++;
        }
        for (int r = 0; r < runs; r++)
        {
            var c = Make(900 + r, 6, true);
            int at = 20 * 24 + 10;
            c.fight = h => (h >= at && h < at + 5) || (h >= at + 48 && h < at + 48 + 30);
            c.deaths[0] = at + 2;
            c.deaths[1] = at + 3;
            c.downedAt[2] = at + 2;
            c.downedAt[3] = at + 50;
            RunDays(c, 34);
            disPeople.AddRange(c.people.Where(q => !c.deaths.ContainsKey(q.id)));
        }
        var dis = Measure(disPeople, 37);
        int disBad = disPeople.Sum(p => p.badDays);
        Console.WriteLine($"  Real disasters: {dis.felt:0.00} breaks a colonist-year (mild bands {dis.mild:0.00}), under the minor line {dis.minorFelt:P1} ({dis.minorMild:P1}), "
                          + $"under the major {dis.majorFelt:P1} ({dis.majorMild:P1}); {disBad} bad afternoons among {disPeople.Count} colonists ({dis.bad:0.00} a colonist-year).");
        C(dis.felt > dis.mild, "after a real disaster the worn down break more often than with the mild bands");
        C(disBad > 0 && disBad <= disPeople.Count, $"bad afternoons come after a disaster, at most about one each ({disBad} among {disPeople.Count})");

        // How much a day off eases: Avila at 61 (fighting, grief, pain: the save's), a day off or not, in a calm colony.
        float Eased(bool give, float weight)
        {
            var c = Make(42, 6, false);
            int at = 10 * 24 + 9;
            var p = c.people[0];
            c.dayOff = (q, h) => give && q.id == 0 && h == at;
            float start = 0f, end = 0f;
            for (int hour = 0; hour < 12 * 24; hour++)
                foreach (var q in c.people)
                {
                    if (q.id == 0 && hour == at)
                    {
                        q.strain = 61f;
                        q.weight = weight;
                        q.weightUntil = hour + (int)(HeartFeltMath.HoldDaysFor(weight) * 24f);
                        start = q.strain;
                    }
                    Hour(c, q, hour);
                    if (q.id == 0 && hour == at + 24) end = q.strain;
                }
            return start - end;
        }
        float with = Eased(true, 0f), without = Eased(false, 0f), withW = Eased(true, 30f), withoutW = Eased(false, 30f);
        Console.WriteLine($"  A day off at strain 61: eased {with:0} in the day against {without:0} on an ordinary day; with a weight of 30 under it, {withW:0} against {withoutW:0}.");
        C(with >= without + 12f, $"a day off eases well beyond an ordinary day ({with:0} against {without:0})");
        C(withW > withoutW && withW < with, "a weight under it still holds part of it (rest can't ease below it)");

        // Hearts spread, never running away: one colonist held near breaking for fifty days among seven close friends.
        float maxWith = 0f, maxWithout = 0f, spreadDay = 0f;
        int wornOthers = 0;
        for (int r = 0; r < runs; r++)
        {
            foreach (bool on in new[] { true, false })
            {
                var c = Make(1100 + r, 8, true);
                c.spread = on;
                c.badDays = false;
                c.people[0].holdAt = 80f;
                RunDays(c, 56);
                foreach (var p in c.people.Skip(1))
                {
                    if (on)
                    {
                        maxWith = Math.Max(maxWith, p.spreadMax);
                        spreadDay += p.spreadBuilt / 56f;
                        if (p.strainByHour.Any(s => s >= HeartMath.BandStart(StrainBand.Worn))) wornOthers++;
                    }
                    else maxWithout = Math.Max(maxWithout, p.spreadMax);
                }
            }
        }
        spreadDay /= runs * 7;
        Console.WriteLine($"  Spreading: one colonist held near breaking for eight weeks among seven close friends; the others at {maxWith:0} at most ({maxWithout:0} without spreading), "
                          + $"spread building {spreadDay:0.0} a day each; {wornOthers} of {runs * 7} ever worn down.");
        C(wornOthers == 0 && maxWith < HeartMath.BandStart(StrainBand.Worn), "one worn colonist alone never wears another down: no spiral");
        C(spreadDay <= HeartCountMath.SpreadDailyCap && spreadDay > 0f, $"spreading is small: {spreadDay:0.0} a day, within its cap");
    }

    private static void CountSeams(string root, Action<bool, string> C)
    {
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string effects = Code("Heart/HeartEffects.cs"), off = Code("Heart/HeartTimeOff.cs"), advice = Code("Heart/HeartAdvice.cs"), logic = Code("Heart/HeartLogic.cs");
        string count = Code("Heart/HeartCountMath.cs");

        // The safety rule: never the game's mood need, its breaker or its break picker; time off is never a mental state.
        foreach (var (name, code) in new[] { ("HeartEffects", effects), ("HeartTimeOff", off), ("HeartAdvice", advice), ("HeartCountMath", count) })
            C(!code.Contains("MentalBreaker") && !code.Contains("Need_Mood") && !code.Contains("MentalBreakWorker") && !code.Contains("TryStartMentalState"),
                name + ": nothing of the mood need, the breaker or the break picker");
        C(Regex.Matches(off, @"\[HarmonyLib\.HarmonyPatch").Count == 1 && off.Contains("[HarmonyLib.HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]"), "one patch: the day off's button on a colonist");
        C(!Regex.IsMatch(effects + advice, @"HarmonyPatch"), "no patch for the effects or the advice");
        C(!off.Contains("Sched.") && !advice.Contains("Sched.") && !effects.Contains("Sched."), "no new Sched slot");

        // The bands' moods: the thought's stages are the rule's (the setting's strength in code).
        var heart = XDocument.Load(Path.Combine(root, "Defs", "ThoughtDefs", "Council_Thoughts_Heart.xml")).Root!.Elements("ThoughtDef")
            .First(e => (string)e.Element("defName") == "Council_Heart_Strain");
        var stages = heart.Element("stages")!.Elements("li").Select(li => float.Parse((string)li.Element("baseMoodEffect")!, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        C(stages.SequenceEqual(new[] { HeartMath.MoodOffset(StrainBand.Strained), HeartMath.MoodOffset(StrainBand.Worn), HeartMath.MoodOffset(StrainBand.Breaking) }), "strain's thought: its stages are the rule's");
        C(effects.Contains("protected override float BaseMoodOffset => HeartCountMath.StrainMood((StrainBand)(CurStageIndex + 1), ColonyCouncilMod.Settings?.heartBands ?? BandStrength.Felt);")
          && Code("Heart/GoodHeart.cs").Contains("protected override float BaseMoodOffset => HeartCountMath.GoodMood((GoodBand)(CurStageIndex + 1)"),
            "both moods sized by the setting through the thought itself");
        C(Code("Moods/GoodLife.cs").Contains("var lasting = SituationalOf(handler.situational);") && !Code("Moods/GoodLife.cs").Contains("Thought_HeartGood"),
            "the good side's mood stays in good things growing ordinary's sum, as a lasting good mood (no exemption)");

        // The effects beyond mood: stat parts patched in conditionally, on four stats besides work.
        string patch = File.ReadAllText(Path.Combine(root, "Patches", "Council_Heart.xml"));
        foreach (var (stat, eff) in new[] { ("GlobalLearningFactor", "Learning"), ("RestFallRateFactor", "Rest"), ("InjuryHealingFactor", "Healing"), ("ImmunityGainSpeed", "Immunity") })
            C(patch.Contains("Defs/StatDef[defName=\"" + stat + "\"]/parts") && patch.Contains("<li Class=\"ColonyCouncil.StatPart_HeartFelt\"><effect>" + eff + "</effect></li>"),
                stat + ": the heart's stat part, patched conditionally");
        C(Regex.IsMatch(effects, @"public override void TransformValue\(StatRequest req, ref float val\)\s*\{\s*if \(!HeartEffects\.On"), "nothing with the effects off");
        C(effects.Contains("return h == null ? null : HeartEffects.StatLines(h, effect);"), "each effect says itself in the stat's explanation");

        // Time off: under the day off's safety rules, through the timetable and a node of Rimfolk's own, put back after, ended by danger.
        C(Regex.IsMatch(off, @"public static string Unsafe[\s\S]{0,600}Drafted[\s\S]{0,300}ThreatOn[\s\S]{0,200}ShouldSeekMedicalRest[\s\S]{0,200}Nursing[\s\S]{0,800}TooFew"),
            "the safety rules: at home, not drafted, no danger, nobody needing a doctor or a baby fed, enough hands");
        C(off.Contains("else if (MapFacts.ThreatOn(p.Map)) End(p, h, now, false, \"CC_Count_Cut_Danger\");"), "danger ends it at once (every slice), so they can be drafted");
        C(off.Contains("if (was != null && set != null && tt.GetAssignment(hour) == set) tt.SetAssignment(hour, was);"), "the timetable put back only where nobody changed it since");
        C(off.Contains("cal?.TakeRestHour(p, i)"), "an hour the calendar's rest day gave is taken over, so each is put back once");
        C(off.Contains("Notify.Message(text, p, MessageTypeDefOf.NegativeEvent, Importance.Notable);") && off.Contains("h.offWhy = HeartFelt.StrainWhy(h);"),
            "a bad afternoon's message says why");
        C(off.Contains("HeartActs.Say(p, null, \"act:dayoff\", tag)") && off.Contains("HeartActs.Say(p, null, \"act:badday\", tag)"), "a line said as each begins (Speech.Say)");
        C(logic.Contains("HeartTimeOff.Hour(p, h, m, now);") && logic.Contains("if (dayOff) factor *= HeartCountMath.DayOffRestFactor;"), "the heart's hour: a bad afternoon maybe, a day off easing faster");
        var tree = XDocument.Load(Path.Combine(root, "Defs", "HeartDefs", "Rimfolk_Heart_TimeOff.xml")).Root!.Element("ThinkTreeDef")!;
        var subs = tree.Element("thinkRoot")!.Element("subNodes")!.Elements("li").Select(li => (string)li.Attribute("Class")).ToList();
        C((string)tree.Element("insertTag") == "Humanlike_PreMain" && (string)tree.Element("thinkRoot")!.Attribute("Class") == "ColonyCouncil.ThinkNode_ConditionalHeartTimeOff"
          && subs.FirstOrDefault() == "JobGiver_Work" && tree.Descendants("emergency").Any(e => e.Value == "true") && !subs.Skip(1).Contains("JobGiver_Work"),
            "the think tree: before the main work, emergencies first, no ordinary work while it runs");
        C(Regex.IsMatch(off, @"protected override bool Satisfied\(Pawn pawn\)[\s\S]{0,500}Drafted[\s\S]{0,400}TimeAssignmentDefOf\.Joy[\s\S]{0,200}ShouldSeekMedicalRest\(pawn\) \|\| MapFacts\.ThreatOn"),
            "the node stands down when drafted, outside its hours, needing a doctor or in danger");
        C(Regex.IsMatch(off, @"if \(!running && h\.strain < HeartCountMath\.DayOffFrom\) return null;"), "the button shows from strained on (or to end a day off)");
        C(Code("Heart/HeartActs.cs").Contains("if (!FirstPot.WillDrink(p, t.def)") || Code("Heart/HeartActs.cs").Contains("!FirstPot.WillDrink(p, t.def)"),
            "a drink only if they drink: their policy, their nature and their faith");

        // Advice: the council's own path for a decree; buttons where Rimfolk can act.
        C(advice.Contains("tracker.Petition(def, asker)") && advice.Contains("MainTabWindow_Council.OpenTab(\"Decrees\");") && Code("Decrees/Decrees.cs").Contains("public DecreeRequest Petition(DecreeDef def, Pawn asker)"),
            "a decree is put to the council as a petition, and the Decrees page opens where it is declared");
        C(advice.Contains("GatheringDefOf.Party") && advice.Contains("HeartTimeOff.GiveDayOff(p)") && advice.Contains("HeartActs.FriendVisit(p, friend)"),
            "a party called, a day off given, a friend sent over");
        C(Code("Heart/HeartFelt.cs").Contains("HeartAdvice.Short(p, h, 3)"), "the message when someone is worn down says what would help");
        C(Code("Heart/HeartCard.cs").Contains("CardActs.Register(CardKind.Colonist, DayOffLabel, DayOffWhyNot") && Code("Heart/HeartView.cs").Contains("m.advice = HeartAdvice.Colony(list.Select(x => x.p));"),
            "on the card (with the day off among its acts) and the council's Moods page");
        var decrees = Directory.GetFiles(Path.Combine(root, "Defs", "DecreeDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.DecreeDef")).ToList();
        var easing = decrees.Where(d => d.Element("heartEases") != null || d.Element("strainRelief") != null).ToList();
        C(easing.Count >= 6, $"the colony's decrees that ease strain are named ({easing.Count})");
        foreach (var d in easing)
        {
            string dn = (string)d.Element("defName");
            C(!string.IsNullOrEmpty((string)d.Element("heartHelp")) && !string.IsNullOrEmpty((string)d.Element("heartCost")), dn + ": what it does for them, and what it costs");
            foreach (var li in d.Element("heartEases")?.Elements("li") ?? Enumerable.Empty<XElement>())
                C(Enum.TryParse<StrainSource>(li.Value, out _), dn + ": eases " + li.Value + ", a strain source");
        }

        // Settings: each new part under the heart's, on (the bands felt).
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements())
            .GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);
        foreach (var (field, label) in new[] { ("heartEffects", "HeartEffects"), ("heartBadDays", "HeartBadDays"), ("heartSpread", "HeartSpread"), ("heartAdvice", "HeartAdvice"), ("heartDayOff", "HeartDayOff") })
        {
            C(settings.Contains("public bool " + field + " = true;") && settings.Contains("Scribe_Values.Look(ref " + field + ", \"" + field + "\", true);"), field + ": a setting, on");
            C(Regex.IsMatch(catalog, "Toggle\\(\"" + field + "\", \"CC_Settings_" + label + "\", \"CC_SetNote_" + label + "\", \"heart\"\\);"), field + ": on the heart's settings");
            C(keyed.ContainsKey("CC_Settings_" + label) && keyed.ContainsKey("CC_Settings_" + label + "_Tip") && keyed.ContainsKey("CC_SetNote_" + label), field + ": its words");
        }
        C(settings.Contains("public BandStrength heartBands = BandStrength.Felt;") && catalog.Contains("Choice(\"heartBands\", \"CC_Settings_HeartBands\""), "how strongly the bands are felt: a choice, felt by default");
        foreach (var o in new[] { "Mild", "Felt", "Strong" })
            C(keyed.ContainsKey("CC_HeartBands_" + o) && keyed.ContainsKey("CC_HeartBands_" + o + "_Tip") && keyed.ContainsKey("CC_SetNote_HeartBands_" + o), o + ": its words");
        foreach (StrainSource s in Enum.GetValues(typeof(StrainSource)))
            C(keyed.ContainsKey("CC_Heart_Source_" + s), "words for strain's source " + s);
        foreach (StrainRelief r in Enum.GetValues(typeof(StrainRelief)))
            C(keyed.ContainsKey("CC_Heart_Relief_" + r), "words for strain's relief " + r);

        // The save: values on the heart, the old timetable a short list (modelled in save_estimate.py); lists padded by position.
        string data = Code("Heart/HeartData.cs");
        foreach (var fld in new[] { "offKind", "offWhy" })
            C(data.Contains("Scribe_Values.Look(ref " + fld + ", \"" + fld + "\");"), fld + " saved");
        foreach (var fld in new[] { "offStart", "offUntil" })
            C(data.Contains("Scribe_Values.Look(ref " + fld + ", \"" + fld + "\", -1);"), fld + " saved, an older save at none");
        foreach (var fld in new[] { "lastDayOffTick", "lastBadDayTick" })
            C(data.Contains("Scribe_Values.Look(ref " + fld + ", \"" + fld + "\", -999999);"), fld + " saved, an older save's waits past");
        C(data.Contains("Scribe_Collections.Look(ref offHours, \"offHours\", LookMode.Value);") && data.Contains("offHours ??= new List<string>();"), "their old timetable saved, an older save's empty");
        C(Code("Heart/HeartTracker.cs").Contains("Scribe_Values.Look(ref lastColonyBadDayTick, \"lastColonyBadDayTick\", -999999);"), "the colony's last bad afternoon saved");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("\"Heart/HeartData.cs:offHours\""), "the old timetable is modelled in the save estimate");
        C(data.Contains("[Unsaved] public List<HeartAdvice.Item> advice;"), "the advice is worked out again, never saved");

        // The Mood tab: the summary first, the sections remembered.
        string tab = Code("Moods/ITab_Pawn_Mood.cs");
        C(tab.Contains("glance = MoodGlance.For(p, mood);") && tab.Contains("float cy = glance != null ? glance.Draw(0f, 0f, w) : 0f;"), "the Mood tab opens on its summary, built with its model");
        C(tab.Contains("Folds.Remembered(\"mood.sections\"") && Code("UI/RimfolkTab.cs").Contains("PageMemory.Set(remember, string.Join(\",\", keys));"), "its sections fold and are remembered");
        C(Code("Moods/MoodGlance.cs").Contains("MoodView.Number(l.mood)") && Code("Moods/MoodGlance.cs").Contains("h.strain.ToString(\"0\")"),
            "the summary's numbers are the lists' own (the moods' sizes, strain to the point)");

        // RimHUD: by name, as before; the heart at a glance, the good side's bar and what would help.
        string hud = Code("Integrations/RimHud.cs"), hudDefs = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Council_RimHUD.xml"));
        foreach (var cls in new[] { "RimHud_Strain", "RimHud_HeartAdvice", "RimHud_GoodBar" })
            C(hud.Contains("public static class " + cls) && hudDefs.Contains("<defClass>ColonyCouncil." + cls + "</defClass>"), cls + ": a RimHUD widget with its def");
        C(!hud.Contains("using RimHUD"), "no reference to RimHUD's own code");
    }
}
