using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// What a camp lays out is built, and what it needs is gathered on the land (Camps/CampGatherMath.cs, CampGather.cs): the
/// player's save of 16 November 2026 had a surveyors' camp whose stools round the fire were laid out and never built (no
/// wood, and nobody went for it) and whose second shelf stood as a frame with all its wood and was never finished.
/// </summary>
public static class CampGatherTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "camp gather: " + what);
        }

        // ---------------- What the land gives ----------------

        var wood = new GatherFacts { source = CampSource.Trees, shortBy = 50, treeYield = 125, feller = true, miner = true, hauler = true };
        C(CampGatherMath.LandGives(wood) && CampGatherMath.Next(wood) == CampGatherStep.Fell, "short of wood with trees near: they fell one");
        GatherFacts W(Func<GatherFacts, GatherFacts> f) => f(wood);
        C(CampGatherMath.Next(W(f => { f.shortBy = 0; return f; })) == CampGatherStep.Bring, "with enough wood they carry it to the piece");
        C(CampGatherMath.Next(W(f => { f.shortBy = -5; f.treeYield = 0; f.feller = false; return f; })) == CampGatherStep.Bring, "...trees or none, when they have it already");
        C(CampGatherMath.Next(W(f => { f.treeYield = 40; return f; })) == CampGatherStep.GiveUp, "trees round them that give less than they lack: the land can't give it");
        C(CampGatherMath.Next(W(f => { f.treeYield = 0; return f; })) == CampGatherStep.GiveUp, "no trees within their reach: given up");
        C(CampGatherMath.Next(W(f => { f.feller = false; return f; })) == CampGatherStep.GiveUp, "nobody of them can fell a tree: given up");
        C(CampGatherMath.Next(W(f => { f.packing = true; return f; })) == CampGatherStep.GiveUp, "packing up to go: given up");
        C(CampGatherMath.Next(W(f => { f.packing = true; f.shortBy = 0; return f; })) == CampGatherStep.GiveUp, "...even with what it needs in hand");

        var stone = new GatherFacts { source = CampSource.Rock, shortBy = 40, cutter = true, miner = true, hauler = true, feller = true, rockCells = 20 };
        GatherFacts S(Func<GatherFacts, GatherFacts> f) => f(stone);
        C(CampGatherMath.Next(stone) == CampGatherStep.Mine, "short of stone blocks with plain rock near: they mine it");
        C(CampGatherMath.Next(S(f => { f.chunkYield = 20; return f; })) == CampGatherStep.Cut, "a chunk of theirs is dressed into blocks first");
        C(CampGatherMath.Next(S(f => { f.freeChunkYield = 20; return f; })) == CampGatherStep.Pick, "a chunk lying free is fetched before rock is mined");
        C(CampGatherMath.Next(S(f => { f.freeChunkYield = 20; f.hauler = false; return f; })) == CampGatherStep.Mine, "...by one who carries; else rock is mined");
        C(CampGatherMath.Next(S(f => { f.cutter = false; f.chunkYield = 200; return f; })) == CampGatherStep.GiveUp, "no stonecutter's table and no hand-dressing for their age: stone is given up");
        C(CampGatherMath.Next(S(f => { f.rockCells = 0; return f; })) == CampGatherStep.GiveUp, "no plain rock within their reach and no chunks: given up");
        C(CampGatherMath.Next(S(f => { f.rockCells = 3; return f; })) == CampGatherStep.GiveUp, "too little rock for what they lack (a chunk in four cells): given up");
        C(CampGatherMath.Next(S(f => { f.rockCells = 0; f.chunkYield = 20; f.freeChunkYield = 20; return f; })) == CampGatherStep.Cut, "chunks enough between theirs and those lying free");
        C(CampGatherMath.Next(S(f => { f.miner = false; return f; })) == CampGatherStep.GiveUp, "nobody of them mines and no chunks: given up");

        var steel = new GatherFacts { source = CampSource.Carried, shortBy = 10, treeYield = 999, rockCells = 99, cutter = true, feller = true, miner = true, hauler = true };
        C(CampGatherMath.Next(steel) == CampGatherStep.GiveUp, "steel they don't carry: the land doesn't give it");
        steel.shortBy = 0;
        C(CampGatherMath.Next(steel) == CampGatherStep.Bring, "steel they carry is carried to the piece");

        // ---------------- What a piece is made of ----------------

        StuffOption O(CampSource s, bool listed, int shortBy, int trees = 0, int rock = 0, bool cutter = false) =>
            new StuffOption { listed = listed, facts = new GatherFacts { source = s, shortBy = shortBy, treeYield = trees, rockCells = rock, cutter = cutter, feller = true, miner = true, hauler = true } };
        // A tribal camp: its seats of wood, the trees round it.
        C(CampGatherMath.ChooseStuff(new[] { O(CampSource.Trees, true, 0) }) == 0, "the wood they carry");
        C(CampGatherMath.ChooseStuff(new[] { O(CampSource.Trees, true, 55, 200) }) == 0, "wood they lack, with trees to fell: wood");
        C(CampGatherMath.ChooseStuff(new[] { O(CampSource.Trees, true, 55, 20) }) == -1, "wood they lack and the trees can't give: not laid out");
        // An industrial camp: posts of wood or steel.
        C(CampGatherMath.ChooseStuff(new[] { O(CampSource.Trees, true, 30, 0), O(CampSource.Carried, true, 0) }) == 1, "an industrial camp with no trees round it uses the steel it carries");
        C(CampGatherMath.ChooseStuff(new[] { O(CampSource.Trees, true, 0), O(CampSource.Carried, true, 0) }) == 0, "...the first listed they have enough of");
        C(CampGatherMath.ChooseStuff(new[] { O(CampSource.Trees, true, 30, 100), O(CampSource.Carried, true, 10) }) == 0, "steel short, trees near: wood");
        // A spacer camp: steel first, then wood.
        C(CampGatherMath.ChooseStuff(new[] { O(CampSource.Carried, true, 20), O(CampSource.Trees, true, 60, 300) }) == 1, "a spacer camp short of steel builds of the wood round it");
        // No trees but rock: stone where they can dress it.
        var bare = new[] { O(CampSource.Trees, true, 50, 0), O(CampSource.Rock, false, 50, 0, 30, true) };
        C(CampGatherMath.ChooseStuff(bare) == 1, "on bare rock, stone blocks where they can dress them");
        bare[1].facts.cutter = false;
        C(CampGatherMath.ChooseStuff(bare) == -1, "...and nothing where they can't");
        C(CampGatherMath.ChooseStuff(new[] { O(CampSource.Rock, false, 0, 0, 0, true), O(CampSource.Trees, true, 25, 100) }) == 1, "stone not listed is no first choice even in hand: wood from the trees first");
        C(CampGatherMath.ChooseStuff(new StuffOption[0]) == -1 && CampGatherMath.ChooseStuff(null) == -1, "nothing to choose from");

        // ---------------- Giving up ----------------

        var going = new BuildFacts { builder = true, hauler = true, needsCarrying = true, landGives = true, sinceProgress = 0 };
        C(CampGatherMath.GiveUpWhy(going) == null, "a piece the land gives, with builders and carriers: it goes on");
        BuildFacts G(Func<BuildFacts, BuildFacts> f) => f(going);
        C(CampGatherMath.GiveUpWhy(G(f => { f.packing = true; return f; })) == "packing", "they are packing");
        C(CampGatherMath.GiveUpWhy(G(f => { f.builder = false; return f; })) == "builder", "nobody of them can build it");
        C(CampGatherMath.GiveUpWhy(G(f => { f.hauler = false; return f; })) == "hauler", "nobody carries what it still needs");
        C(CampGatherMath.GiveUpWhy(G(f => { f.hauler = false; f.needsCarrying = false; return f; })) == null, "...but a frame with all its materials needs no carrier");
        C(CampGatherMath.GiveUpWhy(G(f => { f.landGives = false; return f; })) == "land", "the land can't give it");
        C(CampGatherMath.GiveUpWhy(G(f => { f.sinceProgress = CampGatherMath.StallTicks; return f; })) == null, "two days of nothing moving is still waited out");
        C(CampGatherMath.GiveUpWhy(G(f => { f.sinceProgress = CampGatherMath.StallTicks + 1; return f; })) == "stalled", "past two days with nothing moving: given up as out of their reach");
        C(CampGatherMath.StallTicks > (int)(1.5f * 60000), "longer than the day and a half that gave the save's stools up");

        // ---------------- When, and how many ----------------

        C(Enumerable.Range(0, 24).Where(h => CampGatherMath.BuildHour(h, false)).SequenceEqual(Enumerable.Range(7, 10)), "once the camp stands they build and gather from seven to five");
        C(Enumerable.Range(0, 24).All(h => CampGatherMath.BuildHour(h, true)), "setting up, at any waking hour");
        C(Enumerable.Range(17, 5).All(h => !CampGatherMath.BuildHour(h, false) && CampDayMath.HourOf(h) != CampHour.Work), "the evening is for the fire");
        C(CampGatherMath.Gatherers(1) == 1 && CampGatherMath.Gatherers(5) == 1 && CampGatherMath.Gatherers(6) == 2 && CampGatherMath.Gatherers(12) == 2, "one of them gathers, two in a camp of six");
        C(CampGatherMath.Cutter(true, false, "spacer") && CampGatherMath.Cutter(false, true, "tribal") && CampGatherMath.Cutter(false, true, "medieval"), "a stonecutter's table, or by hand at their crafting spot for a tribe or a medieval people");
        C(!CampGatherMath.Cutter(false, true, "industrial") && !CampGatherMath.Cutter(false, true, "spacer") && !CampGatherMath.Cutter(false, false, "tribal"), "an industrial or spacer people need the table");
        C(CampGatherMath.TreesFor(50, 25) == 2 && CampGatherMath.TreesFor(51, 25) == 3 && CampGatherMath.TreesFor(0, 25) == 0 && CampGatherMath.TreesFor(5, 0) == 5, "the trees a shortfall takes");

        // ---------------- Played out ----------------

        // The save: two hands (the third away in the colony), two seats round the fire at 25 logs each, no wood left, pines
        // round the camp; laid out at half past five in the evening.
        var save = PlayOut(hands: 2, need: 50, have: 0, trees: new[] { 25, 25, 30, 20, 25 }, startHour: 17);
        C(save.built, $"the stools short of wood are built ({save.log})");
        C(save.firstWork == 7, "nothing is done in the evening or the night: the first felling is at seven the next morning");
        C(save.builtAt <= 24 + 13, $"built by the next midday ({save.builtAt - 24}h)");
        C(save.trees == 2, $"two trees felled for fifty logs ({save.trees})");
        C(save.most == 1, "one of them at the felling at a time in a camp of two");
        // The save's shelf: its frame holds all its wood.
        var shelf = PlayOut(hands: 2, need: 0, have: 0, trees: new int[0], startHour: 18);
        C(shelf.built && shelf.trees == 0 && shelf.builtAt == 24 + 7, "a frame with all its wood is finished in the first working hour, nothing felled");
        // A big camp of six: two at the felling.
        var big = PlayOut(hands: 6, need: 100, have: 20, trees: Enumerable.Repeat(25, 10).ToArray(), startHour: 8);
        C(big.built && big.most == 2 && big.trees == 4, $"six hands: two fell at once, four trees for eighty logs ({big.log})");
        C(big.builtAt < 17, "...and it stands the same day");
        // The trees run short: given up at once, nothing promised.
        var few = PlayOut(hands: 3, need: 75, have: 20, trees: new[] { 20, 10 }, startHour: 9);
        C(!few.built && few.why == "land" && few.trees == 0, $"too few trees for what they lack: given up before a tree is felled ({few.log})");
        // Packing before it's done.
        var pack = PlayOut(hands: 2, need: 75, have: 0, trees: Enumerable.Repeat(25, 6).ToArray(), startHour: 9, packAt: 11);
        C(!pack.built && pack.why == "packing", "packing up before it's done: given up");

        // ---------------- The mod's side ----------------

        string camps = Path.Combine(root, "Source", "ColonyCouncil", "Camps");
        string Code(string f) => Regex.Replace(File.ReadAllText(Path.Combine(camps, f)), @"//[^\n]*", "");
        string work = Code("CampWork.cs"), gather = Code("CampGather.cs"), grow = Code("CampGrow.cs"), life = Code("CampLife.cs"), buildMath = Code("CampBuildMath.cs");
        C(!work.Contains("JobDefOf.FinishFrame") && work.Contains("JobMaker.MakeJob(CampGatherDefOf.Rimfolk_CampBuild, f)"),
            "a frame with all its materials is finished by their own construction job, not the colony's");
        C(Regex.IsMatch(gather, @"class JobDriver_CampBuild : JobDriver, IBuildableDriver[\s\S]*?frame\.CompleteConstruction\(pawn\)") && !Regex.IsMatch(gather, @"JobDriver_CampBuild[\s\S]*?IsForbidden"),
            "the game's own finishing, without the colony's forbidding");
        C(gather.Contains("public override bool TryMakePreToilReservations(bool errorOnFailed) => true;")
          && Regex.IsMatch(work, @"public static Job BuildStep[\s\S]*?o\.CurJob\.def == CampGatherDefOf\.Rimfolk_CampBuild && o\.CurJob\.targetA\.Thing == t")
          && !Regex.IsMatch(work, @"public static Job BuildStep[\s\S]*?pawn\.CanReserve\(t\)[\s\S]*?private static bool Clearable"),
            "one of them at a frame at a time by their own count: the map's reservations never keep them from their own frame (the save's shelf was passed over)");
        C(work.Contains("CampGatherMath.BuildHour(hour, camp.phase == CampPhase.Building) && camp.BuildWork()"), "building comes in their working hours once the camp stands");
        C(Regex.IsMatch(work, @"BuildWork\(\)\)[\s\S]*?Build\(camp, pawn\)[\s\S]*?Fetch\(camp, pawn\)[\s\S]*?Evening\(camp, pawn, hour\)[\s\S]*?Day\(camp, pawn\)"), "after meals and sleep, before the day's other work");
        C(gather.Contains("public bool BuildWork() => Unfinished().Count > 0 || plans.Any(p => p.waiting && !p.placed && !p.dropped);"),
            "a piece waiting for its ground counts as building to do (it was never cleared once the camp stood)");
        C(work.Contains("return camp.GatherForBuildJob(pawn);") && gather.Contains("CampGatherMath.Next(f)") && gather.Contains("CampGatherMath.Gatherers(Hands)"), "short of something, one of them gathers it");
        C(Regex.IsMatch(gather, @"FellFor[\s\S]*?CampWork\.Tree\(this, p, wood\)[\s\S]*?trees\.Add\(tree\)"), "a tree felled for the building is one of theirs to watch and fetch from");
        C(Regex.IsMatch(Code("CampWork.cs"), @"internal static Plant Tree\(Camp camp, Pawn pawn, ThingDef wood = null\)[\s\S]*?camp\.Fellable\(p\)"), "the same trees as their felling: wild, unmarked, clear of the colony (CampTake)");
        C(gather.Contains("CampTake.May(p, this)") && gather.Contains("CampTake.May(rock, this)") && gather.Contains("CampTake.May(t, this)"), "every tree, rock and chunk asked of the take rule");
        C(work.Contains("var goods = camp.BuildGoods();") && gather.Contains("list.AddRange(WorkLeft());") && work.Contains("camp.Own(res);"),
            "the logs of a tree they felled are carried straight to the piece, theirs");
        C(Regex.IsMatch(work, @"enrouteManager\.GetEnroute\(e, need\.thingDef, pawn\)"), "what is on its way to a blueprint is counted, so two don't bring the same");
        C(grow.Contains("GrowHoldUp(mine)") && !buildMath.Contains("GrowGiveUpDays") && gather.Contains("CampGatherMath.GiveUpWhy(f)"),
            "an addition is given up only by the rule, no longer after a day and a half");
        C(Regex.IsMatch(grow, @"GrowStuff[\s\S]*?CampGatherMath\.ChooseStuff\(choice\)") && Regex.IsMatch(grow, @"Affords[\s\S]*?CampGatherMath\.LandGives"), "laid out only of what they have or the land gives");
        C(grow.Contains("if (!CanBuildPiece(def)) return false;"), "nothing laid out that nobody of them could build");
        C(!grow.Contains("chopsToday++"), "no longer one more tree on the day's count and nothing more");
        C(life.Contains("BuildHourly();"), "what they carry for it goes down at their stash each hour");
        C(Regex.IsMatch(Code("CampWork.cs"), @"Rimfolk_CampCutStone\) camp\.CutStone\(pawn, job\.targetC\.Thing\)"), "the stone dressed comes into their stash");
        string trees = Regex.Replace(File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Building", "BuildersTrees.cs")), @"//[^\n]*", "");
        C(trees.Contains("if (constructible.Faction != Faction.OfPlayer || worker.Faction != Faction.OfPlayer) return false;"),
            "the colony's builders leave trees to its cutters, never a camp's: its trees are not marked for the colony");

        // ---------------- The defs ----------------

        var kits = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKits.xml"));
        var jobs = kits.Root!.Elements("JobDef").ToDictionary(e => e.Element("defName")!.Value);
        C(jobs.TryGetValue("Rimfolk_CampBuild", out var build) && build.Element("driverClass")?.Value == "ColonyCouncil.JobDriver_CampBuild", "their construction job");
        C(jobs.TryGetValue("Rimfolk_CampCutStone", out var cut) && cut.ToString().Contains("<effecter>CutStone</effecter>"), "stone dressed with the game's own stone-cutting look");
        var pool = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_CampDay_Lines.xml"));
        var lines = pool.Descendants("li").Select(e => e.Value).ToList();
        foreach (var tag in new[] { "[timber]", "[stone]" })
        {
            var some = lines.Where(l => l.StartsWith(tag)).ToList();
            C(some.Count >= 3, $"{tag} lines as they gather ({some.Count})");
            C(some.All(l => !l.Contains("!") && !l.Contains("\u2014") && Regex.Matches(l.Substring(tag.Length).Trim(), @"[.?](\s|$)").Count == 1), $"{tag} lines are one plain sentence each");
        }
        return n;
    }

    /// <summary>What came of a piece played out hour by hour.</summary>
    private struct Outcome
    {
        public bool built;
        public int builtAt, firstWork, trees, most;
        public string why, log;
    }

    /// <summary>
    /// A piece played out hour by hour with the rules, as the camp's people would: by day they carry what they have to it,
    /// fell a tree an hour each (as many at once as the rule allows) and carry its logs over the next hour; once it holds
    /// all it needs, an hour's building. <paramref name="need"/>: what the piece still needs in all; <paramref name="have"/>:
    /// what lies at their stash for it. Played over three days from <paramref name="startHour"/>.
    /// </summary>
    private static Outcome PlayOut(int hands, int need, int have, int[] trees, int startHour, int packAt = -1)
    {
        var o = new Outcome { firstWork = -1, builtAt = -1 };
        var standing = new List<int>(trees);
        int stash = have, lying = 0, delivered = 0, last = 0;
        var log = new List<string>();
        for (int h = 0; h < 72; h++)
        {
            int at = startHour + h, hour = at % 24;
            bool packing = packAt >= 0 && at >= packAt;
            int shortBy = need - delivered - stash - lying;
            var facts = new GatherFacts { source = CampSource.Trees, shortBy = shortBy, treeYield = standing.Sum(), feller = true, miner = true, hauler = true, packing = packing };
            var hold = new BuildFacts { packing = packing, builder = true, hauler = true, needsCarrying = delivered < need, landGives = CampGatherMath.LandGives(facts), sinceProgress = (h - last) * 2500 };
            o.why = CampGatherMath.GiveUpWhy(hold);
            if (o.why != null)
            {
                log.Add($"{hour}h given up ({o.why})");
                break;
            }
            if (!CampGatherMath.BuildHour(hour, false)) continue;
            if (o.firstWork < 0) o.firstWork = hour;
            // What they have, and what the last felling left lying, carried to the piece.
            if (stash + lying > 0 && delivered < need)
            {
                int bring = Math.Min(stash + lying, need - delivered), fromLying = Math.Min(lying, bring);
                delivered += bring;
                lying -= fromLying;
                stash -= bring - fromLying;
                last = h;
                log.Add($"{hour}h brought {bring}");
            }
            if (delivered >= need)
            {
                o.built = true;
                o.builtAt = at;
                log.Add($"{hour}h built");
                break;
            }
            // Felling for what is still short: as many as the rule allows, a tree an hour each.
            int felling = 0;
            while (CampGatherMath.Next(facts) == CampGatherStep.Fell && felling < CampGatherMath.Gatherers(hands) && standing.Count > 0)
            {
                lying += standing[0];
                standing.RemoveAt(0);
                felling++;
                o.trees++;
                facts.shortBy = need - delivered - stash - lying;
                facts.treeYield = standing.Sum();
            }
            if (felling > 0)
            {
                last = h;
                log.Add($"{hour}h felled {felling}");
            }
            o.most = Math.Max(o.most, felling);
        }
        o.log = string.Join(", ", log);
        return o;
    }
}
