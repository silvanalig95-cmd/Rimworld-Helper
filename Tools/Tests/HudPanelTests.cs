using ColonyCouncil;

/// <summary>
/// The alerts, health, and work and idle panels (Source/ColonyCouncil/Hud/): the health panel's filter (only what will
/// pass or needs a reaction, each exclusion on its own), the immunity race and the order of what matters, the work
/// panel's order and its cut-short job reports, the alerts' order and counts, where every panel starts (nothing
/// overlapping at 1080p, the alerts above the letters), and the mood trend's labels that never overlap.
/// </summary>
public static class HudPanelTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "on-screen panels: " + what);
        }

        // ---------- Health: what shows ----------
        // A plain illness and a plain wound, then one fact changed at a time.
        var flu = new HediffFacts { bad = true, immunizable = true, sick = true, tendable = true, lethalSeverity = 1f, severity = 0.4f, severityPerDay = 0.2f };
        var cut = new HediffFacts { bad = true, injury = true, tendable = true, tended = true, severity = 5f };
        Check(HudHealthMath.Shows(flu), "an illness racing immunity shows");
        Check(HudHealthMath.Shows(cut), "a fresh wound shows, even tended: it will pass");
        var scar = cut;
        scar.permanent = true;
        Check(!HudHealthMath.Shows(scar), "a scar (a permanent wound) never shows");
        var lost = new HediffFacts { bad = true, missingPart = true, tendable = true, needsTend = true, bleedRate = 0.2f };
        Check(!HudHealthMath.Shows(lost), "a lost body part never shows, not even a fresh one");
        var bionic = new HediffFacts { bad = false, addedPart = true };
        Check(!HudHealthMath.Shows(bionic), "an added part or implant never shows");
        var implantBad = new HediffFacts { bad = true, addedPart = true, severityPerDay = 0.1f };
        Check(!HudHealthMath.Shows(implantBad), "an implant never shows, even one the game calls bad");
        var badBack = new HediffFacts { bad = true, chronic = true, severity = 0.5f };
        Check(!HudHealthMath.Shows(badBack), "a chronic condition (bad back, cataract, frailty, dementia) never shows");
        var chronicTendable = new HediffFacts { bad = true, chronic = true, tendable = true, needsTend = true, lethalSeverity = 1f, severityPerDay = 0.01f };
        Check(!HudHealthMath.Shows(chronicTendable), "a chronic condition stays off even when it can be tended");
        var psylink = new HediffFacts { bad = false, severity = 3f };
        Check(!HudHealthMath.Shows(psylink), "what the game doesn't call bad (psylinks, auras, buffs) never shows");
        var buffPassing = new HediffFacts { bad = false, severity = 0.5f, severityPerDay = -1f };
        Check(!HudHealthMath.Shows(buffPassing), "a good effect that wears off never shows");
        var pregnant = new HediffFacts { bad = true, pregnancy = true, severity = 0.3f, severityPerDay = 0.05f };
        Check(!HudHealthMath.Shows(pregnant), "pregnancy and labour never show");
        var lasting = new HediffFacts { bad = true, severity = 0.5f };
        Check(!HudHealthMath.Shows(lasting), "a bad condition that never changes and needs nothing doesn't show");
        var curse = new HediffFacts { bad = true, severity = 0.5f, severityPerDay = -0.3f };
        Check(!HudHealthMath.Shows(curse), "a bad effect that passes but is no wound, illness or danger (a mod's curse, a hangover) doesn't show");
        var curseRising = new HediffFacts { bad = true, severity = 0.5f, severityPerDay = 0.3f };
        Check(!HudHealthMath.Shows(curseRising), "a bad effect that grows but can't be tended and can't kill doesn't show");
        // What does show.
        var bleeding = new HediffFacts { bad = true, bleedRate = 0.3f };
        Check(HudHealthMath.Shows(bleeding), "anything bleeding shows");
        var untended = new HediffFacts { bad = true, tendable = true, needsTend = true, severity = 0.3f };
        Check(HudHealthMath.Shows(untended), "anything wanting tending shows");
        var tendableUntended = new HediffFacts { bad = true, tendable = true, severity = 0.3f };
        Check(HudHealthMath.Shows(tendableUntended), "a tendable condition not yet tended shows");
        var tendedPassing = new HediffFacts { bad = true, tendable = true, tended = true, severity = 0.3f, severityPerDay = -0.1f };
        Check(HudHealthMath.Shows(tendedPassing), "a tended condition still going shows (it passes with care)");
        var tendedStill = new HediffFacts { bad = true, tendable = true, tended = true, severity = 0.3f };
        Check(HudHealthMath.Shows(tendedStill), "a tended condition shows while it can still be tended");
        var poisoning = new HediffFacts { bad = true, sick = true, severity = 0.5f, severityPerDay = -1f };
        Check(HudHealthMath.Shows(poisoning), "an illness without a race (food poisoning) shows");
        var hypothermia = new HediffFacts { bad = true, lethalSeverity = 1f, severity = 0.3f };
        Check(HudHealthMath.Shows(hypothermia), "something that can kill shows even when its severity is moved from outside (hypothermia)");
        var dependencyEarly = new HediffFacts { bad = true, lethalSeverity = 1f, severity = 0.028f, severityPerDay = 0.02f };
        Check(!HudHealthMath.Shows(dependencyEarly), "something that can kill shows only once it's some way along (a dependency at 3% doesn't)");
        var dependencyLate = new HediffFacts { bad = true, lethalSeverity = 1f, severity = 0.3f, severityPerDay = 0.02f };
        Check(HudHealthMath.Shows(dependencyLate), "the same at 30% shows");
        var cold = new HediffFacts { ailment = true, severity = 0.2f, severityPerDay = -0.2f };
        Check(HudHealthMath.Shows(cold), "Rimfolk's everyday ailments show: they pass");
        var lingering = new HediffFacts { ailment = true, bad = true, chronic = true };
        Check(!HudHealthMath.Shows(lingering), "an ailment marked chronic doesn't");

        // ---------- Health: the immunity race ----------
        Check(HudHealthMath.Race(1f, 0.2f, 0.5f, 0.3f, 1f) == HealthRace.Immune, "immune: on its way out");
        Check(HudHealthMath.Race(0.3f, 0.2f, 0.5f, -0.2f, 1f) == HealthRace.Recovering, "the severity falling: recovering");
        Check(HudHealthMath.Race(0.5f, 0.25f, 0.3f, 0.2f, 1f) == HealthRace.Recovering, "immunity in 2 days, death in 3.5: will recover");
        Check(HudHealthMath.Race(0.2f, 0.1f, 0.6f, 0.2f, 1f) == HealthRace.Losing, "immunity in 8 days, death in 2: losing");
        Check(HudHealthMath.Race(0.2f, 0f, 0.6f, 0.2f, 1f) == HealthRace.Losing, "no immunity coming and it can kill: losing");
        Check(HudHealthMath.Race(0.2f, 0.1f, 0.6f, 0.2f, 0f) == HealthRace.Recovering, "an illness that can't kill is beaten in the end");
        Check(Math.Abs(HudHealthMath.DaysToDeath(0.6f, 0.2f, 1f) - 2f) < 0.001f && HudHealthMath.DaysToDeath(0.6f, -0.1f, 1f) > 1000f, "days to death at today's pace");

        // ---------- Health: the order ----------
        var losingFlu = flu;
        losingFlu.severity = 0.5f;
        float sLosing = HudHealthMath.Score(losingFlu, HealthRace.Losing, false);
        var nearDeath = flu;
        nearDeath.severity = 0.85f;
        float sSoonLethal = HudHealthMath.Score(nearDeath, HealthRace.Losing, false);
        float sRecoveringHigh = HudHealthMath.Score(nearDeath, HealthRace.Recovering, false);
        float sUntended = HudHealthMath.Score(untended, HealthRace.None, false);
        float sHealing = HudHealthMath.Score(cut, HealthRace.None, false);
        float sBleeding = HudHealthMath.PawnScore(10f, 0f, 0);
        var cooling = hypothermia;
        cooling.severityPerDay = 0.5f;
        float sWorsening = HudHealthMath.Score(cooling, HealthRace.None, false);
        Check(sBleeding > sSoonLethal, "bleeding first");
        Check(HudRank.LevelOf(sSoonLethal) == HudLevel.Life, "an illness about to kill that is losing is life and limb");
        Check(sSoonLethal > sUntended && sUntended > sLosing && sLosing > sHealing, "then untended, then losing a race, then healing");
        Check(HudRank.LevelOf(sUntended) == HudLevel.Soon && HudRank.LevelOf(sLosing) == HudLevel.Soon && HudRank.LevelOf(sHealing) == HudLevel.Worth,
            "untended and losing are soon, healing is worth knowing");
        Check(sRecoveringHigh < HudRank.LifeFloor, "far along but winning the race isn't soon lethal");
        Check(HudRank.LevelOf(sWorsening) == HudLevel.Soon && sWorsening < sUntended, "something deadly getting worse is soon, after untended");
        Check(HudHealthMath.Score(lasting, HealthRace.None, false) == 0f, "what doesn't show scores nothing");
        Check(HudHealthMath.Score(hypothermia, HealthRace.None, true) >= HudRank.LifeFloor, "a life-threatening stage is soon lethal");
        Check(HudHealthMath.PawnScore(-1f, sHealing, 3) > HudHealthMath.PawnScore(-1f, sHealing, 1), "more to tend ranks higher");
        Check(HudHealthMath.PawnScore(-1f, 0f, 0) == 0f, "nothing to say, nothing to rank");

        // ---------- Health: colours ----------
        Check(HudHealthMath.TendTone(0.15f) == HealthTone.Bad && HudHealthMath.TendTone(0.44f) == HealthTone.Fair && HudHealthMath.TendTone(0.85f) == HealthTone.Good,
            "tending 15% is red, 44% yellow, 85% green");
        Check(HudHealthMath.TendTone(HudHealthMath.TendPoorBelow) == HealthTone.Fair && HudHealthMath.TendTone(HudHealthMath.TendGoodFrom) == HealthTone.Good,
            "the tending thresholds belong to the better colour");
        Check(HudHealthMath.StatusOf(true, 0f, 2, true) == HealthStatus.Bleeding, "bleeding is said first");
        Check(HudHealthMath.StatusOf(false, sSoonLethal, 1, false) == HealthStatus.Danger, "then soon deadly");
        Check(HudHealthMath.StatusOf(false, sUntended, 1, true) == HealthStatus.Untended, "then something to tend");
        Check(HudHealthMath.StatusOf(false, sLosing, 0, true) == HealthStatus.Losing, "then losing a race");
        Check(HudHealthMath.StatusOf(false, sWorsening, 0, false) == HealthStatus.Worsening, "then getting worse");
        Check(HudHealthMath.StatusOf(false, sHealing, 0, false) == HealthStatus.Healing, "else healing");
        Check(HudHealthMath.ToneOf(HealthStatus.Healing) == HealthTone.Good && HudHealthMath.ToneOf(HealthStatus.Worsening) == HealthTone.Warn
                                                                          && HudHealthMath.ToneOf(HealthStatus.Losing) == HealthTone.Warn
                                                                          && HudHealthMath.ToneOf(HealthStatus.Bleeding) == HealthTone.Bad
                                                                          && HudHealthMath.ToneOf(HealthStatus.Danger) == HealthTone.Bad
                                                                          && HudHealthMath.ToneOf(HealthStatus.Untended) == HealthTone.Fair,
            "healing green, to tend yellow, getting worse or losing orange, bleeding or in danger red");
        Check(HudHealthMath.HealthToneOf(0.95f) == HealthTone.Good && HudHealthMath.HealthToneOf(0.6f) == HealthTone.Fair
                                                                   && HudHealthMath.HealthToneOf(0.4f) == HealthTone.Warn && HudHealthMath.HealthToneOf(0.1f) == HealthTone.Bad,
            "the health mark by a name goes from green to red");

        // ---------- Health: how far along ----------
        Check(Math.Abs(HudHealthMath.Along(0.4f, 3f, 1f) - 0.4f) < 0.001f, "a wound's healed share is used when it's known");
        Check(Math.Abs(HudHealthMath.Along(-1f, 3f, 1f) - 0.75f) < 0.001f, "else three days had and one to go is three quarters along");
        Check(HudHealthMath.Along(-1f, 3f, -1f) < 0f, "not getting better: nothing to show");
        Check(HudHealthMath.Along(-1f, 0f, 0f) == 0f && HudHealthMath.Along(1.3f, 0f, 0f) == 1f, "along stays between 0 and 1");

        // ---------- Health: grouping the same condition ----------
        var groupOf = HudHealthMath.Group(new[] { "Bruise|t", "Bite|t", "Bruise|t", "Bruise|u" }, out var groupSizes);
        Check(groupOf.SequenceEqual(new[] { 0, 1, 0, 2 }) && groupSizes.SequenceEqual(new[] { 2, 1, 1 }),
            "two tended bruises make one chip (Bruise ×2), in the order first seen; an untended one stays apart");
        Check(HudHealthMath.Group(new string[0], out var noSizes).Length == 0 && noSizes.Length == 0, "nothing to group, no groups");

        // ---------- Health: chips wrap onto lines ----------
        var spots = new List<ChipSpot>();
        var flow3 = HudHealthMath.Flow(new[] { 50f, 50f, 50f }, 200f, 5f, 2, 20f, spots);
        Check(flow3.shown == 3 && flow3.more == 0 && flow3.lines == 1 && spots[1].x == 55f && spots[2].x == 110f && spots[2].line == 0,
            "three short chips sit on one line, a gap apart");
        var flow5 = HudHealthMath.Flow(new[] { 60f, 60f, 60f, 60f, 60f }, 200f, 5f, 2, 20f, spots);
        Check(flow5.shown == 5 && flow5.more == 0 && flow5.lines == 2 && spots[3].line == 1 && spots[3].x == 0f,
            "the fourth chip that doesn't fit wraps to a second line");
        var flow8 = HudHealthMath.Flow(new[] { 60f, 60f, 60f, 60f, 60f, 60f, 60f, 60f }, 200f, 5f, 2, 20f, spots);
        Check(flow8.lines == 2 && flow8.shown == 5 && flow8.more == 3 && flow8.moreLine == 1 && Math.Abs(flow8.moreX - 130f) < 0.01f,
            "past two lines the rest is \"+N\", a chip taken off the end to make room for it");
        Check(flow8.moreX + 20f <= 200f, "\"+N\" fits on the line");
        var flowWide = HudHealthMath.Flow(new[] { 40f, 300f, 40f }, 200f, 5f, 3, 20f, spots);
        Check(flowWide.shown == 3 && spots[1].line == 1 && spots[1].x == 0f && spots[1].w == 200f && spots[2].line == 2,
            "a chip wider than the line gets a line of its own, cut to it");
        var flowOne = HudHealthMath.Flow(new[] { 300f, 40f }, 200f, 5f, 1, 20f, spots);
        Check(flowOne.shown == 1 && flowOne.more == 1 && spots[0].w + 5f + 20f <= 200.01f && flowOne.moreLine == 0,
            "a long chip alone on the last line is cut to leave room for \"+N\"");
        Check(HudHealthMath.Flow(new float[0], 200f, 5f, 2, 20f, spots).lines == 0 && spots.Count == 0, "no chips, no lines");
        var flowNone = HudHealthMath.Flow(new[] { 50f, 50f }, 0f, 5f, 2, 20f, spots);
        Check(flowNone.shown == 0 && flowNone.more == 2, "no room at all: everything is \"+N\"");

        // ---------- Work and idle ----------
        string[] cutAt = { " to ", " at ", " from ", " into " };
        Check(HudWorkMath.ShortReport("Hauling steel to stockpile zone 1.", cutAt) == "Hauling steel", "a haul report is cut at its destination");
        Check(HudWorkMath.ShortReport("Cooking simple meal at fueled stove.", cutAt) == "Cooking simple meal", "a bill is cut at its bench");
        Check(HudWorkMath.ShortReport("Tending to Penguin.", cutAt) == "Tending to Penguin", "one word before the cut: kept whole");
        Check(HudWorkMath.ShortReport("Sleeping.", cutAt) == "Sleeping", "the closing full stop goes");
        Check(HudWorkMath.ShortReport("", cutAt) == "" && HudWorkMath.ShortReport(null, cutAt) == "", "no report, nothing");
        Check(HudWorkMath.ShortReport("Wandering.\nSecond line", cutAt) == "Wandering", "only the first line");
        string longReport = HudWorkMath.ShortReport("Constructing an extraordinarily long and complicated thing of great beauty", cutAt, 20);
        Check(longReport.Length <= 20 && longReport.EndsWith("…"), "a long report is cut with an ellipsis");
        float starving = HudRank.Work(true, 0.2f, true, true, false, false, 0f, true, false);
        float exhausted = HudRank.Work(false, 0f, false, true, false, false, 0f, true, false);
        float idleLong = HudRank.Work(false, 0f, false, false, false, true, 6f, false, false);
        float idleShort = HudRank.Work(false, 0f, false, false, false, true, 0.2f, false, false);
        float tired = HudRank.Work(false, 0f, false, false, false, false, 0f, true, false);
        float busy = HudRank.Work(false, 0f, false, false, false, false, 0f, false, false);
        float drafted = HudRank.Work(false, 0f, false, false, false, false, 0f, false, true);
        Check(starving > exhausted && exhausted > idleLong && idleLong > idleShort && idleShort > tired && tired > busy && busy > drafted,
            "starving, exhausted, idle (longest first), hungry or tired, busy, drafted");
        Check(HudRank.LevelOf(starving) == HudLevel.Life && HudRank.LevelOf(exhausted) == HudLevel.Soon && HudRank.LevelOf(idleShort) == HudLevel.Worth
              && HudRank.LevelOf(busy) == HudLevel.Quiet, "starving is life and limb, exhausted soon, idle worth knowing, busy quiet");
        Check(HudWorkMath.IdleHours(1000, 6000) == 2f && HudWorkMath.IdleHours(-1, 6000) == 0f, "idle hours from ticks");
        Check(HudWorkMath.Unmanned(true, 0) && !HudWorkMath.Unmanned(true, 1) && !HudWorkMath.Unmanned(false, 0), "work waiting with nobody on it is worth a line");

        // ---------- Alerts ----------
        Check(HudRank.Alert(2) >= HudRank.LifeFloor && HudRank.LevelOf(HudRank.Alert(1)) == HudLevel.Soon && HudRank.LevelOf(HudRank.Alert(0)) == HudLevel.Worth,
            "critical alerts are never cut, high ones come next, medium last");
        var kept = HudMath.Keep(new List<float> { HudRank.Alert(0), HudRank.Alert(2), HudRank.Alert(1), HudRank.Alert(2) }, 1, HudRank.LifeFloor, out int cutCount);
        Check(kept.SequenceEqual(new[] { 1, 3 }) && cutCount == 2, "with room for one, every critical alert still shows");
        Check(HudMath.ShowCount("Need warm clothes", 3) && !HudMath.ShowCount("Colonist wishes (5)", 5) && !HudMath.ShowCount("Low medicine", 1),
            "an alert's count shows from two, unless its label already says a number");

        // ---------- Where the panels start ----------
        foreach (var (sw, sh) in new[] { (1920f, 1080f), (2560f, 1440f) })
        {
            // The game's letters with three letters up (the bottom-right controls are about 250 pixels tall).
            float letters = sh - 262f - 3 * 42f;
            var boxes = new Dictionary<string, HudBox>();
            // Typical full sizes: the vitals' six lines, the council's six with "and more", ten colonists on the board and
            // the work panel (with its three colony lines), four on the health panel, eight alerts.
            var sizes = new Dictionary<string, (float w, float h)>
            {
                [HudIds.Moods] = (226f, 8f + 10 * 34f + 16f), [HudIds.Trend] = (224f, 100f), [HudIds.Vitals] = (232f, 8f + 6 * 20f),
                [HudIds.Council] = (244f, 8f + 6 * 20f + 16f), [HudIds.Alerts] = (248f, 8f + 8 * 20f + 16f), [HudIds.Health] = (252f, 8f + 4 * 32f + 16f),
                [HudIds.Work] = (256f, 8f + 3 * 18f + 4f + 10 * 20f + 16f), [HudIds.Inventory] = (126f, 8f + 25 * 20f + 16f)
            };
            foreach (var id in HudIds.All)
            {
                HudMath.DefaultPlace(id, sw, sh, out float fx, out float fy, out bool right, out bool bottom, out bool auto);
                var (w, h) = sizes[id];
                boxes[id] = auto ? HudMath.AlertsBox(w, h, sw, sh, letters, 0f) : HudMath.FromFraction(fx, fy, right, bottom, w, h, sw, sh);
                var b = boxes[id];
                Check(b.x >= 0f && b.y >= 0f && b.XMax <= sw + 0.01f && b.YMax <= sh + 0.01f, $"{id} starts on a {sw}x{sh} screen");
            }
            var ids = HudIds.All;
            for (int i = 0; i < ids.Length; i++)
            for (int j = i + 1; j < ids.Length; j++)
                Check(!HudMath.Overlap(boxes[ids[i]], boxes[ids[j]]), $"{ids[i]} and {ids[j]} don't overlap where they start ({sw}x{sh})");
            Check(boxes[HudIds.Alerts].YMax <= letters && Math.Abs(boxes[HudIds.Alerts].XMax - sw) < 0.01f, "the alerts start in the game's alert place, above the letters");
            // Clear of the colonist bar (its widest, 260 px from each side, two rows) and of the resource list's column.
            var bar = new HudBox(260f, 0f, sw - 520f, 140f);
            foreach (var id in ids) Check(!HudMath.Overlap(boxes[id], bar), $"{id} starts clear of the colonist bar ({sw}x{sh})");
        }
        var withList = HudMath.AlertsBox(248f, 100f, 1920f, 1080f, 800f, 120f);
        Check(withList.YMax <= 800f - 120f, "while the game's own list still shows, the panel stands above it");
        var noLetters = HudMath.AlertsBox(248f, 100f, 1920f, 1080f, -1f, 0f);
        Check(Math.Abs(noLetters.YMax - 1080f * 0.76f) < 0.01f, "without a letter line, a quarter up from the bottom");

        // ---------- The trend's labels ----------
        HudMath.FitThree(220f, 60f, 70f, 20f, 6f, out bool l1, out bool m1, out bool r1);
        Check(l1 && m1 && r1, "room for all three: all three show");
        HudMath.FitThree(140f, 60f, 70f, 20f, 6f, out bool l2, out bool m2, out bool r2);
        Check(l2 && !m2 && r2, "\"3 days ago\" and the key don't fit side by side: the key steps aside, never overlapping");
        HudMath.FitThree(70f, 60f, 0f, 20f, 6f, out bool l3, out _, out bool r3);
        Check(!l3 && r3, "too narrow for both ends: \"now\" stays");
        foreach (float width in new[] { 80f, 120f, 160f, 214f, 300f })
        {
            HudMath.FitThree(width, 62f, 84f, 22f, 6f, out bool a, out bool b, out bool c);
            float used = (a ? 62f + 6f : 0f) + (c ? 22f + 6f : 0f);
            bool middleClear = !b || ((width - 84f) / 2f >= (a ? 68f : 0f) && (width + 84f) / 2f <= width - (c ? 28f : 0f));
            Check(used <= width + 6f && middleClear, $"labels never overlap or leave a {width}-pixel line");
        }
        return cases;
    }
}
