using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Rimfolk's scenario parts: a game that starts with stakes already in play. An enemy who knows the colony, a debt
    // coming due, deserters whose old unit is still looking, two families who never got on, a reputation, a secret.
    // Each part can also be added to any scenario in the scenario editor. The stakes are set up once, a few seconds
    // into the game (when everyone is out of their pods), and one letter says how things stand.

    /// <summary>What the parts share while the stakes are set up.</summary>
    public class ScenarioSetupContext
    {
        public CouncilGameComponent comp;
        public Map map;
        /// <summary>The colonists the game started with (no babies).</summary>
        public List<Pawn> colonists;
        /// <summary>The enemy a foe part made, for the parts after it (the boss whose money was taken).</summary>
        public Pawn foe;
        public Faction foeFaction;
        /// <summary>Who the foe's quarrel is with (the betrayed captain isn't the one who was in on it).</summary>
        public Pawn victim;
        /// <summary>Lines for the letter, one per part.</summary>
        public List<string> lines = new List<string>();
        public List<Pawn> look = new List<Pawn>();
        public bool threat;

        public List<Pawn> Adults => colonists.Where(p => !p.DevelopmentalStage.Juvenile()).ToList();
    }

    public static class ScenarioSetup
    {
        public static void TryRun()
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || comp.scenarioSetUp) return;
            int now = Find.TickManager.TicksGame;
            if (now < 600) return;
            var map = HappeningTracker.HomeMap();
            if (map == null) return;
            // Everyone out of their pods first (a while at most).
            bool pods = map.listerThings.ThingsOfDef(ThingDefOf.DropPodIncoming).Any() || map.listerThings.ThingsOfDef(ThingDefOf.ActiveDropPod).Any();
            if (pods && now < GenDate.TicksPerHour * 3) return;
            var colonists = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Baby()).ToList();
            if (colonists.Count == 0) return;
            comp.scenarioSetUp = true;
            var ctx = new ScenarioSetupContext { comp = comp, map = map, colonists = colonists };
            foreach (var part in Find.Scenario.AllParts.OfType<ScenPart_Council>().ToList())
            {
                try
                {
                    part.Setup(ctx);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("scenario " + part.def?.defName, e);
                }
            }
            if (ctx.lines.Count == 0) return;
            string text = "CC_Scen_LetterIntro".Translate() + "\n\n" + string.Join("\n\n", ctx.lines);
            var look = ctx.look.Where(p => p != null && p.Spawned).Distinct().ToList();
            Find.LetterStack.ReceiveLetter("CC_Scen_LetterLabel".Translate(), text, ctx.threat ? LetterDefOf.ThreatSmall : LetterDefOf.NeutralEvent,
                look.Count > 0 ? new LookTargets(look) : LookTargets.Invalid);
        }

        /// <summary>A hostile people that can send fighters; pirates and their like first, if asked.</summary>
        public static Faction Enemy(bool outlaws) =>
            (outlaws ? Raids.Enemy(f => f.def.permanentEnemy) : null) ?? Raids.Enemy();

        /// <summary>A hostile people with an army to desert from: not pirates, and of the colony's own age if there is one.</summary>
        public static Faction Army() =>
            Raids.Enemy(f => !f.def.permanentEnemy && f.def.techLevel >= (Avail.IsPreIndustrial ? TechLevel.Neolithic : TechLevel.Industrial))
            ?? Raids.Enemy(f => !f.def.permanentEnemy) ?? Raids.Enemy();

        /// <summary>Someone fit to lead their people: a leader kind if they have one, else one of their toughest.</summary>
        public static Pawn MakeLeader(Faction f, bool unarmed)
        {
            var kinds = (f.def.pawnGroupMakers ?? new List<PawnGroupMaker>())
                .Where(m => m.kindDef == PawnGroupKindDefOf.Combat && m.options != null)
                .SelectMany(m => m.options).Select(o => o.kind)
                .Where(k => k?.RaceProps != null && k.RaceProps.Humanlike).Distinct().ToList();
            var kind = kinds.Where(k => k.factionLeader).RandomElementWithFallback()
                       ?? kinds.OrderByDescending(k => k.combatPower).Take(3).RandomElementWithFallback()
                       ?? f.def.basicMemberKind;
            if (kind == null) return null;
            var req = new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, colonistRelationChanceFactor: 0f,
                mustBeCapableOfViolence: true, developmentalStages: DevelopmentalStage.Adult, dontGiveWeapon: unarmed);
            return PawnGenerator.GeneratePawn(req);
        }

        public static string Names(IEnumerable<Pawn> pawns) => pawns.Select(p => p.LabelShort).ToList().ToCommaList(true);
    }

    /// <summary>The base of Rimfolk's scenario parts: they set their stakes up once, together, early in the game.</summary>
    public abstract class ScenPart_Council : ScenPart
    {
        public override void Tick()
        {
            if (Find.TickManager.TicksGame % 60 == 0) ScenarioSetup.TryRun();
        }

        public abstract void Setup(ScenarioSetupContext ctx);

        protected static float Days(FloatRange r) => Mathf.Max(0f, r.RandomInRange);

        /// <summary>A steady id for this part's range sliders.</summary>
        protected int Id => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        protected Rect Rows(Listing_ScenEdit listing, int rows) => listing.GetScenPartRect(this, RowHeight * rows);

        protected static Rect Row(Rect r, int i) => new Rect(r.x, r.y + i * RowHeight, r.width, RowHeight);
    }

    // ===================== A sworn enemy =====================

    /// <summary>
    /// A named enemy of a hostile people who already has a quarrel with the colony: they hunt someone, were robbed by
    /// them, betrayed them, or held them in chains. Known from the first day (a nemesis), and they may come: once, or
    /// again and again while they live. A captive tyrant starts as the colony's prisoner, sleeping off a drugged cup.
    /// </summary>
    public class ScenPart_CouncilFoe : ScenPart_Council
    {
        public string deed = "hunts";
        public bool outlaws = true;
        public bool captive;
        /// <summary>When they first come (0~0: not until something else sets them off).</summary>
        public FloatRange firstDays = new FloatRange(10f, 14f);
        /// <summary>Days between later attacks (0~0: they come once).</summary>
        public FloatRange everyDays = FloatRange.Zero;
        /// <summary>How many attacks in all (0: until the one leading them falls).</summary>
        public int times;
        private string timesBuffer;
        /// <summary>How the attacks are told: hunt, mutiny, heist, tyrant.</summary>
        public string story = "hunt";

        private static readonly string[] Deeds = { "hunts", "robbed", "betrayed", "enslaved" };

        public override void Setup(ScenarioSetupContext ctx)
        {
            var faction = ScenarioSetup.Enemy(outlaws);
            if (faction == null)
            {
                ctx.lines.Add("CC_Scen_NoEnemy".Translate());
                return;
            }
            var adults = ctx.Adults;
            if (adults.Count == 0) return;
            // The mutiny was against whoever led; anything else, against anyone.
            var victim = deed == "betrayed"
                ? adults.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).First()
                : adults.RandomElement();
            var foe = ScenarioSetup.MakeLeader(faction, captive);
            if (foe == null) return;
            int now = Find.TickManager.TicksGame;
            if (captive)
            {
                var near = victim.Position;
                if (!CellFinder.TryFindRandomCellNear(near, ctx.map, 5, c => c.Standable(ctx.map) && c.GetFirstPawn(ctx.map) == null, out var cell))
                    cell = near;
                GenSpawn.Spawn(foe, cell, ctx.map);
                foe.guest?.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                // Out cold for a few hours: long enough to find somewhere to keep them.
                var sleep = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, foe);
                sleep.Severity = 1f;
                foe.health.AddHediff(sleep);
                foreach (var p in ctx.colonists)
                    p.needs?.mood?.thoughts?.memories?.TryGainMemory(CouncilDefOf.Council_Scen_Tyrant, foe);
                ctx.look.Add(foe);
            }
            else Find.WorldPawns.PassToWorld(foe, PawnDiscardDecideMode.KeepForever);
            ctx.foe = foe;
            ctx.foeFaction = faction;
            ctx.victim = victim;
            ctx.look.Add(victim);
            ctx.comp.nemeses.Add(foe, victim, Deeds.Contains(deed) ? deed : "hunts", captive);
            bool comes = firstDays.max > 0f;
            if (comes)
            {
                var m = ctx.comp.happenings.Remember("hunt", foe, faction, story);
                // Never the same day they land, and never back to back.
                m.due = now + Mathf.RoundToInt(Mathf.Max(1f, Days(firstDays)) * GenDate.TicksPerDay);
                m.everyMin = Mathf.Max(3f, everyDays.min);
                m.everyMax = Mathf.Max(m.everyMin, everyDays.max);
                m.left = everyDays.max > 0f ? (times > 0 ? times : -1) : 1;
                ctx.threat = true;
            }
            string line = ("CC_Scen_Foe_" + deed).Translate(foe.Named("FOE"), faction.Name.Named("FACTION"), victim.Named("VICTIM")).Resolve();
            if (captive) line += " " + "CC_Scen_Foe_Prisoner".Translate(foe.Named("FOE"), faction.Name.Named("FACTION")).Resolve();
            string again = everyDays.max <= 0f ? "CC_Scen_Foe_Once" : times > 0 ? "CC_Scen_Foe_Again" : "CC_Scen_Foe_Always";
            if (comes) line += " " + again.Translate(foe.Named("FOE"), faction.Name.Named("FACTION")).Resolve();
            ctx.lines.Add(line);
            ctx.comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, line, victim));
        }

        public override string Summary(Scenario scen) =>
            ("CC_ScenPart_Foe_" + deed).Translate().ToString() + (captive ? " " + "CC_ScenPart_Foe_Captive".Translate() : "")
            + (firstDays.max > 0f ? " " + (everyDays.max > 0f ? "CC_ScenPart_Foe_Again" : "CC_ScenPart_Foe_Once").Translate() : "");

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            var r = Rows(listing, 5);
            if (Widgets.ButtonText(Row(r, 0), ("CC_ScenPart_Deed_" + deed).Translate()))
                Find.WindowStack.Add(new FloatMenu(Deeds.Select(d => new FloatMenuOption(("CC_ScenPart_Deed_" + d).Translate(), () => deed = d)).ToList()));
            Widgets.CheckboxLabeled(Row(r, 1), "CC_ScenPart_Captive".Translate(), ref captive);
            Widgets.FloatRange(Row(r, 2), Id ^ 0x5ce1, ref firstDays, 0f, 40f, "CC_ScenPart_FirstDays", ToStringStyle.Integer, 0f, GameFont.Tiny, null, 1f);
            Widgets.FloatRange(Row(r, 3), Id ^ 0x5ce2, ref everyDays, 0f, 60f, "CC_ScenPart_EveryDays", ToStringStyle.Integer, 0f, GameFont.Tiny, null, 1f);
            Widgets.TextFieldNumericLabeled(Row(r, 4), "CC_ScenPart_Times".Translate(), ref times, ref timesBuffer, 0f, 20f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref deed, "deed", "hunts");
            Scribe_Values.Look(ref outlaws, "outlaws", true);
            Scribe_Values.Look(ref captive, "captive");
            Scribe_Values.Look(ref firstDays, "firstDays");
            Scribe_Values.Look(ref everyDays, "everyDays");
            Scribe_Values.Look(ref story, "story", "hunt");
            Scribe_Values.Look(ref times, "times");
        }
    }

    // ===================== A debt =====================

    /// <summary>
    /// The colony owes silver: a loan that paid for everything it landed with, or the take from a job that wasn't the
    /// colony's to take. When it comes due, the council chooses: pay, pay half and beg for time, or refuse and fight.
    /// </summary>
    public class ScenPart_CouncilDebt : ScenPart_Council
    {
        public int amount = 3000;
        public FloatRange dueDays = new FloatRange(30f, 36f);
        /// <summary>loan or heist.</summary>
        public string story = "loan";
        private string buffer;

        public override void Setup(ScenarioSetupContext ctx)
        {
            var faction = ctx.foeFaction ?? ScenarioSetup.Enemy(true);
            if (faction == null) return;
            var m = ctx.comp.happenings.Remember("debt", ctx.foe, faction, story);
            m.amount = Mathf.Max(10, amount);
            int days = Mathf.Max(1, Mathf.RoundToInt(Days(dueDays)));
            m.due = Find.TickManager.TicksGame + days * GenDate.TicksPerDay;
            string line = ("CC_Scen_Debt_" + story).Translate(m.amount.Named("AMOUNT"), faction.Name.Named("FACTION"), days.Named("DAYS"),
                (ctx.foe?.LabelShort ?? faction.Name).Named("FOE")).Resolve();
            ctx.lines.Add(line);
            ctx.threat = true;
            ctx.comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, line));
        }

        public override string Summary(Scenario scen) => ("CC_ScenPart_Debt_" + story).Translate(amount, Mathf.RoundToInt(dueDays.Average));

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            var r = Rows(listing, 3);
            if (Widgets.ButtonText(Row(r, 0), ("CC_ScenPart_Debt_Story_" + story).Translate()))
                Find.WindowStack.Add(new FloatMenu(new[] { "loan", "heist" }.Select(s => new FloatMenuOption(("CC_ScenPart_Debt_Story_" + s).Translate(), () => story = s)).ToList()));
            Widgets.TextFieldNumericLabeled(Row(r, 1), "CC_ScenPart_Amount".Translate(), ref amount, ref buffer, 10f, 100000f);
            Widgets.FloatRange(Row(r, 2), Id ^ 0x5ce3, ref dueDays, 1f, 90f, "CC_ScenPart_DueDays", ToStringStyle.Integer, 0f, GameFont.Tiny, null, 1f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref amount, "amount", 3000);
            Scribe_Values.Look(ref dueDays, "dueDays");
            Scribe_Values.Look(ref story, "story", "loan");
        }
    }

    // ===================== Deserters =====================

    /// <summary>
    /// Some of the colonists ran from a hostile people's army, and their old unit is still looking (the "deserter
    /// found" world event, the first time at a set point, later whenever it comes up).
    /// </summary>
    public class ScenPart_CouncilDeserters : ScenPart_Council
    {
        public int count = 2;
        public FloatRange firstDays = new FloatRange(18f, 26f);
        private string buffer;

        public override void Setup(ScenarioSetupContext ctx)
        {
            var faction = ctx.foeFaction ?? ScenarioSetup.Army();
            if (faction == null) return;
            // Soldiers by preference: it's their unit.
            var chosen = ctx.Adults.OrderByDescending(p => Pasts.Has(p, Past.Soldier) ? 1 : 0).ThenBy(_ => Rand.Value).Take(Mathf.Max(1, count)).ToList();
            if (chosen.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            bool first = true;
            foreach (var p in chosen)
            {
                var m = ctx.comp.happenings.Remember("deserter", p, faction);
                if (first && firstDays.max > 0f) m.due = now + Mathf.RoundToInt(Days(firstDays) * GenDate.TicksPerDay);
                first = false;
            }
            string line = "CC_Scen_Deserters".Translate(ScenarioSetup.Names(chosen).Named("NAMES"), faction.Name.Named("FACTION"), chosen.Count.Named("COUNT")).Resolve();
            ctx.lines.Add(line);
            ctx.look.AddRange(chosen);
            ctx.comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, line, chosen[0]));
        }

        public override string Summary(Scenario scen) => "CC_ScenPart_Deserters".Translate(count);

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            var r = Rows(listing, 2);
            Widgets.TextFieldNumericLabeled(Row(r, 0), "CC_ScenPart_Count".Translate(), ref count, ref buffer, 1f, 10f);
            Widgets.FloatRange(Row(r, 1), Id ^ 0x5ce4, ref firstDays, 0f, 60f, "CC_ScenPart_FirstDays", ToStringStyle.Integer, 0f, GameFont.Tiny, null, 1f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref count, "count", 2);
            Scribe_Values.Look(ref firstDays, "firstDays");
        }
    }

    // ===================== Two families =====================

    /// <summary>
    /// The colonists are two families who have never got on: each takes one surname, family stands by family, and
    /// there's bad blood across the line (it fades in a couple of months, if nothing feeds it).
    /// </summary>
    public class ScenPart_CouncilFamilies : ScenPart_Council
    {
        public override void Setup(ScenarioSetupContext ctx)
        {
            var people = ctx.colonists.Where(p => p.needs?.mood != null).ToList();
            if (people.Count < 4) return;
            // People already related (siblings, a couple) stay on the same side.
            var groups = new List<List<Pawn>>();
            foreach (var p in people.InRandomOrder())
            {
                // Someone related to people in two groups brings both groups together.
                var related = groups.Where(x => x.Any(o => p.GetRelations(o).Any() || o.GetRelations(p).Any())).ToList();
                var g = new List<Pawn> { p };
                foreach (var r in related)
                {
                    g.AddRange(r);
                    groups.Remove(r);
                }
                groups.Add(g);
            }
            var a = new List<Pawn>();
            var b = new List<Pawn>();
            foreach (var g in groups.OrderByDescending(g => g.Count)) (a.Count <= b.Count ? a : b).AddRange(g);
            if (a.Count == 0 || b.Count == 0) return;
            string nameA = Surname(a, null) ?? NewSurname(a[0], null);
            string nameB = Surname(b, nameA) ?? NewSurname(b[0], nameA);
            Rename(a, nameA);
            Rename(b, nameB);
            foreach (var x in people)
            foreach (var y in people)
            {
                if (x == y) continue;
                bool kin = a.Contains(x) == a.Contains(y);
                x.needs.mood.thoughts.memories.TryGainMemory(kin ? CouncilDefOf.Council_Scen_Kin : CouncilDefOf.Council_Scen_Feud, y);
            }
            string line = "CC_Scen_Families".Translate((nameA ?? "?").Named("NAMEA"), ScenarioSetup.Names(a).Named("FAMILYA"),
                (nameB ?? "?").Named("NAMEB"), ScenarioSetup.Names(b).Named("FAMILYB")).Resolve();
            ctx.lines.Add(line);
            ctx.comp.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, line));
        }

        /// <summary>The family's name: one of theirs, different from the other family's.</summary>
        private static string Surname(List<Pawn> family, string not) =>
            family.Select(p => (p.Name as NameTriple)?.Last).Where(n => !n.NullOrEmpty() && n != not).RandomElementWithFallback();

        /// <summary>A fresh surname when nobody in the family has one of their own to give.</summary>
        private static string NewSurname(Pawn p, string not)
        {
            for (int i = 0; i < 10; i++)
            {
                string last = (PawnBioAndNameGenerator.GeneratePawnName(p) as NameTriple)?.Last;
                if (!last.NullOrEmpty() && last != not) return last;
            }
            return null;
        }

        private static void Rename(List<Pawn> family, string last)
        {
            if (last.NullOrEmpty()) return;
            foreach (var p in family)
                if (p.Name is NameTriple n && n.Last != last) p.Name = new NameTriple(n.First, n.Nick, last);
        }

        public override string Summary(Scenario scen) => "CC_ScenPart_Families".Translate();
    }

    // ===================== A reputation =====================

    /// <summary>The colony starts already known for something (its ways; they fade like any reputation unless lived up to).</summary>
    public class ScenPart_CouncilWays : ScenPart_Council
    {
        public float mercy;
        public float generosity;
        public float tradition;
        public float openness;
        public float toil;

        public override void Setup(ScenarioSetupContext ctx)
        {
            var shift = new WayShift { mercy = mercy, generosity = generosity, tradition = tradition, openness = openness, toil = toil };
            if (!shift.Any) return;
            ctx.comp.character.Shift(shift, "CC_Scen_WaysWhy".Translate(Find.Scenario?.name ?? "").Resolve());
        }

        public override string Summary(Scenario scen)
        {
            var shift = new WayShift { mercy = mercy, generosity = generosity, tradition = tradition, openness = openness, toil = toil };
            return "CC_ScenPart_Ways".Translate(shift.Summary());
        }

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            var r = Rows(listing, 5);
            mercy = Slider(Row(r, 0), mercy, Way.Mercy);
            generosity = Slider(Row(r, 1), generosity, Way.Generosity);
            tradition = Slider(Row(r, 2), tradition, Way.Tradition);
            openness = Slider(Row(r, 3), openness, Way.Openness);
            toil = Slider(Row(r, 4), toil, Way.Toil);
        }

        private static float Slider(Rect r, float v, Way w) =>
            Widgets.HorizontalSlider(r, v, -50f, 50f, true, CharacterTracker.PoleLabel(w, false) + " / " + CharacterTracker.PoleLabel(w, true) + ": " + Mathf.RoundToInt(v), roundTo: 1f);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mercy, "mercy");
            Scribe_Values.Look(ref generosity, "generosity");
            Scribe_Values.Look(ref tradition, "tradition");
            Scribe_Values.Look(ref openness, "openness");
            Scribe_Values.Look(ref toil, "toil");
        }
    }

    // ===================== A secret =====================

    /// <summary>Someone in the colony is keeping something from the others (a scenario's own secret, or any).</summary>
    public class ScenPart_CouncilSecret : ScenPart_Council
    {
        public SecretDef secret;
        public int count = 1;
        private string buffer;

        public override void Setup(ScenarioSetupContext ctx)
        {
            if (secret == null) return;
            int given = 0;
            var adults = ctx.Adults;
            if (adults.Count > count) adults.Remove(ctx.victim);
            foreach (var p in adults.OrderByDescending(p => secret.Fits(p, out bool match) ? (match ? 2 : 1) : 0).ThenBy(_ => Rand.Value))
            {
                if (given >= count) break;
                if (ctx.comp.secrets.Give(p, secret)) given++;
            }
            if (given > 0) ctx.lines.Add("CC_Scen_SecretHint".Translate());
        }

        public override string Summary(Scenario scen) => "CC_ScenPart_Secret".Translate(secret?.label ?? "?");

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            var r = Rows(listing, 2);
            if (Widgets.ButtonText(Row(r, 0), secret?.LabelCap ?? "CC_ScenPart_PickSecret".Translate()))
                Find.WindowStack.Add(new FloatMenu(DefDatabase<SecretDef>.AllDefsListForReading.OrderBy(d => d.label)
                    .Select(d => new FloatMenuOption(d.LabelCap, () => secret = d)).ToList()));
            Widgets.TextFieldNumericLabeled(Row(r, 1), "CC_ScenPart_Count".Translate(), ref count, ref buffer, 1f, 10f);
        }

        public override void Randomize() =>
            secret = DefDatabase<SecretDef>.AllDefsListForReading.Where(d => d.scenario).RandomElementWithFallback()
                     ?? DefDatabase<SecretDef>.AllDefsListForReading.RandomElementWithFallback();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref secret, "secret");
            Scribe_Values.Look(ref count, "count", 1);
        }
    }

    // ===================== Hunts and debts as the game goes on =====================

    /// <summary>The scenario's stakes, hour by hour: hunts that come round, debts that come due, a deserter's unit.</summary>
    public static class ScenarioStakes
    {
        /// <summary>A scenario's memo stays until it's settled (never pruned for age or crowded out).</summary>
        public static bool Lasting(Memo m) => m != null && !m.used && (m.kind == "hunt" || m.kind == "debt" || m.kind == "event" || (m.kind == "deserter" && m.due > 0));

        public static void Hourly(HappeningTracker t, CouncilGameComponent comp, int now)
        {
            var map = HappeningTracker.HomeMap();
            foreach (var m in t.memos.Where(m => !m.used).ToList())
            {
                try
                {
                    switch (m.kind)
                    {
                        case "hunt": Hunt(t, comp, m, map, now); break;
                        case "debt": Debt(t, m, map, now); break;
                        case "deserter":
                            if (m.due > 0 && now >= m.due) Chase(m, map, now);
                            break;
                        case "event":
                            if (now >= m.due) Happen(t, m, map, now);
                            break;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("scenario " + m.kind, e);
                }
            }
        }

        // ---------------- Hunts ----------------

        /// <summary>How a hunt is told when its scenario didn't say.</summary>
        private const string PlainHunt = "hunt";

        /// <summary>Why a hunt is over, or null while it goes on.</summary>
        private static string Over(Memo m)
        {
            if (m.faction == null || m.faction.defeated) return "defeated";
            var x = m.pawn;
            // Led by someone the world has since lost track of.
            if (x == null && m.left < 0) return "gone";
            if (x != null)
            {
                if (x.Dead) return "dead";
                if (x.Discarded) return "gone";
                if (x.IsSlaveOfColony) return "enslaved";
                if (x.Faction == Faction.OfPlayer && !x.IsPrisonerOfColony) return "joined";
                // Sold (to a ship or a caravan), or gone over to another people.
                if (x.ParentHolder is ITrader || (x.Faction != m.faction && !x.IsPrisonerOfColony)) return "gone";
            }
            if (!m.faction.HostileTo(Faction.OfPlayer)) return "peace";
            return null;
        }

        private static void Hunt(HappeningTracker t, CouncilGameComponent comp, Memo m, Map map, int now)
        {
            string over = Over(m);
            // Collectors who came as often as they meant to: square, as far as they're concerned.
            if (over == null && m.left == 0 && now >= m.due) over = m.pawn != null ? "gaveup" : "settled";
            if (over != null)
            {
                Ended(t, comp, m, over);
                return;
            }
            // Keep the one leading it in the world between attacks (a raid they led lets go of them).
            var lead = m.pawn;
            if (lead != null && !lead.Spawned && !lead.Dead && Find.WorldPawns.Contains(lead)) Find.WorldPawns.ForcefullyKeptPawns.Add(lead);
            if (map == null || now < m.due || m.left == 0) return;
            if (!IncidentWorker_Happening.ScenarioFightsPossible || !Raids.CanRaid(m.faction))
            {
                m.due = now + GenDate.TicksPerDay * 3;
                return;
            }
            // One at a time.
            if (t.raids.Any(r => r.scenario && r.faction == m.faction))
            {
                m.due = now + GenDate.TicksPerDay;
                return;
            }
            var x = m.pawn;
            bool captive = x != null && x.IsPrisonerOfColony;
            var leader = x != null && !captive && !x.Spawned && !x.Dead && x.Faction == m.faction && Find.WorldPawns.Contains(x) && !x.IsPrisoner
                         && !People.Kidnapped(x) && x.GetCaravan() == null && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(x)
                         && x.health.summaryHealth.SummaryHealthPercent >= 0.6f
                ? x
                : null;
            string story = m.def.NullOrEmpty() ? PlainHunt : m.def;
            // This story told this way; else any hunt told this way; else this story; else any hunt.
            string how = captive ? "_Captive" : leader != null ? "_Led" : "";
            string key = new[] { "CC_Scen_Hunt_" + story + how, "CC_Scen_Hunt_" + PlainHunt + how, "CC_Scen_Hunt_" + story }
                .FirstOrDefault(k => (k + "_Label").CanTranslate()) ?? "CC_Scen_Hunt_" + PlainHunt;
            NamedArgument foe = (x?.LabelShort ?? m.faction.Name).Named("FOE");
            NamedArgument faction = m.faction.Name.Named("FACTION");
            string label = (key + "_Label").Translate(foe, faction).Resolve();
            string text = (key + "_Text").Translate(foe, faction).Resolve();
            t.AddRaid(map, m.faction, Rand.Range(0.45f, 0.65f), Rand.Range(6f, 12f), label, text, leader, true);
            Notify.Message("CC_Scen_HuntWarning".Translate(faction).Resolve(), LookTargets.Invalid, MessageTypeDefOf.ThreatSmall, Importance.Notable);
            m.times++;
            if (m.left > 0) m.left--;
            if (m.left == 0 || m.everyMax <= 0f) m.due = now + GenDate.TicksPerDay * 2;
            else m.due = now + Mathf.RoundToInt(Rand.Range(m.everyMin, m.everyMax) * GenDate.TicksPerDay);
            if (m.everyMax <= 0f) m.left = 0;
        }

        private static void Ended(HappeningTracker t, CouncilGameComponent comp, Memo m, string why)
        {
            t.Use(m);
            // A one-off attack that came and went isn't "the end of a hunt" worth a letter.
            if (why == "gaveup" && m.times <= 1) return;
            string name = m.pawn?.LabelShort ?? m.faction?.Name ?? "?";
            string text = ("CC_Scen_HuntOver_" + why).Translate(name.Named("FOE"), (m.faction?.Name ?? "?").Named("FACTION")).Resolve();
            // A nemesis's own end already came with a letter (slain, joined): this one is a message.
            bool told = (why == "dead" || why == "joined" || why == "enslaved") && m.pawn != null && comp.nemeses.all.Any(n => n.pawn == m.pawn && n.announced);
            if (told) Notify.Message(text, m.pawn.Spawned ? new LookTargets(m.pawn) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            else Find.LetterStack.ReceiveLetter("CC_Scen_HuntOver_Label".Translate(), text, LetterDefOf.PositiveEvent, m.pawn != null && m.pawn.Spawned ? new LookTargets(m.pawn) : LookTargets.Invalid);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, text));
            using (Causes.Push(text))
            {
                foreach (var p in PawnsFinder.AllMaps_FreeColonists.ToList().Where(People.HasInnerLife))
                    CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Scen_HuntOver);
            }
        }

        /// <summary>A hunt or debt for the World page, or null.</summary>
        public static string WorldLine(Memo m, int now)
        {
            if (m.used || m.faction == null) return null;
            if (m.kind == "hunt" && m.left != 0)
            {
                var x = m.pawn;
                if (x != null && x.IsPrisonerOfColony) return "CC_World_Hunt_Captive".Translate(m.faction.Name, x.LabelShort).Resolve();
                if (x != null && !x.Dead) return "CC_World_Hunt_Led".Translate(m.faction.Name, x.LabelShort).Resolve();
                return "CC_World_Hunt".Translate(m.faction.Name).Resolve();
            }
            if (m.kind == "debt" && m.due != int.MaxValue)
                return "CC_World_Debt".Translate(m.amount, m.faction.Name, TextUtil.DaysString(Mathf.Max(0, m.due - now))).Resolve();
            return null;
        }

        // ---------------- Debts ----------------

        private static bool Asking(Memo m) =>
            Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Happening h && !h.resolved && h.ctx?.incident == CouncilDefOf.Council_Evt_DebtDue && h.ctx.enemy == m.faction);

        private static void Debt(HappeningTracker t, Memo m, Map map, int now)
        {
            if (m.faction == null || m.faction.defeated)
            {
                t.Use(m);
                return;
            }
            if (m.due == int.MaxValue)
            {
                // The letter is gone without an answer somehow: ask again.
                if (!Asking(m)) m.due = now;
                return;
            }
            if (map == null || now < m.due) return;
            HappeningWorker_DebtDue.pending = m;
            bool ok;
            try
            {
                ok = (CouncilDefOf.Council_Evt_DebtDue.Worker as IncidentWorker_Happening)?.ScenarioFire(map) ?? false;
            }
            finally
            {
                HappeningWorker_DebtDue.pending = null;
            }
            m.due = ok ? int.MaxValue : now + GenDate.TicksPerHour * 6;
        }

        // ---------------- Scheduled events ----------------

        private static void Happen(HappeningTracker t, Memo m, Map map, int now)
        {
            if (map == null) return;
            // Not on top of a fight, or while another question waits for an answer.
            if (HappeningTracker.Waiting || MapFacts.ThreatOn(map))
            {
                m.due = now + GenDate.TicksPerHour * 6;
                return;
            }
            var workers = (m.text ?? "").Split(',').Select(n => DefDatabase<IncidentDef>.GetNamedSilentFail(n.Trim()))
                .Select(d => d?.Worker as IncidentWorker_Happening).Where(w => w != null).InRandomOrder().ToList();
            foreach (var w in workers)
            {
                if (!w.ScenarioCanFire(map) || !w.ScenarioFire(map)) continue;
                t.Use(m);
                return;
            }
            // None of them can happen yet (nobody fits, the wrong season): tomorrow, for a month.
            if (++m.times > 30) t.Use(m);
            else m.due = now + GenDate.TicksPerDay;
        }

        // ---------------- Deserters ----------------

        private static void Chase(Memo m, Map map, int now)
        {
            if (m.pawn == null || m.pawn.Dead || !m.pawn.IsColonist || m.faction == null || m.faction.defeated || !m.faction.HostileTo(Faction.OfPlayer))
            {
                m.due = 0;
                return;
            }
            if (map == null || !IncidentWorker_Happening.ScenarioFightsPossible || MapFacts.ThreatOn(map) || HappeningTracker.Waiting)
            {
                m.due = now + GenDate.TicksPerDay;
                return;
            }
            HappeningWorker_DeserterFound.forced = m;
            bool ok;
            try
            {
                ok = (CouncilDefOf.Council_Evt_DeserterFound.Worker as IncidentWorker_Happening)?.ScenarioFire(map) ?? false;
            }
            finally
            {
                HappeningWorker_DeserterFound.forced = null;
            }
            // Done (the event used the memo), or try again tomorrow.
            m.due = ok ? 0 : now + GenDate.TicksPerDay;
        }
    }

    // ===================== Scheduled world events =====================

    /// <summary>
    /// Something the scenario promises will happen, around a set day: one of a few world events (whichever can happen
    /// then), such as someone from the past coming to join, a scholar visiting, or a great beast. It waits for a
    /// quiet moment, and keeps trying for a month if none of them can happen yet.
    /// </summary>
    public class ScenPart_CouncilEvent : ScenPart_Council
    {
        public List<IncidentDef> events = new List<IncidentDef>();
        public FloatRange days = new FloatRange(6f, 10f);
        /// <summary>A line for the letter that says how things stand (a key; none for a surprise).</summary>
        public string hint;

        public override void Setup(ScenarioSetupContext ctx)
        {
            var defs = events.Where(d => d?.Worker is IncidentWorker_Happening).ToList();
            if (defs.Count == 0) return;
            var m = ctx.comp.happenings.Remember("event", null, null, null, string.Join(",", defs.Select(d => d.defName)));
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Mathf.Max(1f, Days(days)) * GenDate.TicksPerDay);
            if (!hint.NullOrEmpty() && hint.CanTranslate()) ctx.lines.Add(hint.Translate().Resolve());
        }

        public override string Summary(Scenario scen) =>
            "CC_ScenPart_Event".Translate(events.Where(d => d != null).Select(d => d.label).ToCommaList(false, true), Mathf.RoundToInt(days.min), Mathf.RoundToInt(days.max));

        public override void Randomize() => events = new List<IncidentDef> { DefDatabase<IncidentDef>.GetNamedSilentFail("Council_Evt_OldComrade") };

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            var r = Rows(listing, 2);
            string label = events.Count > 0 && events[0] != null ? events[0].LabelCap.ToString() : "CC_ScenPart_PickEvent".Translate().ToString();
            if (Widgets.ButtonText(Row(r, 0), label))
                Find.WindowStack.Add(new FloatMenu(HappeningTracker.All.Where(d => d != CouncilDefOf.Council_Evt_DebtDue).OrderBy(d => d.label)
                    .Select(d => new FloatMenuOption(d.LabelCap, () => events = new List<IncidentDef> { d })).ToList()));
            Widgets.FloatRange(Row(r, 1), Id ^ 0x5ce5, ref days, 1f, 60f, "CC_ScenPart_AroundDay", ToStringStyle.Integer, 0f, GameFont.Tiny, null, 1f);
        }

        public override bool CanCoexistWith(ScenPart other) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref events, "events", LookMode.Def);
            Scribe_Values.Look(ref days, "days");
            Scribe_Values.Look(ref hint, "hint");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (events == null) events = new List<IncidentDef>();
                events.RemoveAll(d => d == null);
            }
        }
    }

    // ===================== Bonds from the start =====================

    /// <summary>
    /// Two of the colonists start close: a couple (if they could be), a married couple, old friends, or a master
    /// teaching an apprentice their best craft.
    /// </summary>
    public class ScenPart_CouncilBond : ScenPart_Council
    {
        /// <summary>lovers, spouses, friends or mentor.</summary>
        public string kind = "friends";

        private static readonly string[] Kinds = { "lovers", "spouses", "friends", "mentor" };

        public override void Setup(ScenarioSetupContext ctx)
        {
            var adults = ctx.Adults.Where(p => p.needs?.mood != null).ToList();
            if (adults.Count < 2) return;
            Pawn a = null, b = null;
            string how = kind;
            if (kind == "lovers" || kind == "spouses")
            {
                foreach (var x in adults.InRandomOrder())
                {
                    if (LovePartnerRelationUtility.HasAnyLovePartner(x)) continue;
                    b = adults.FirstOrDefault(y => y != x && !LovePartnerRelationUtility.HasAnyLovePartner(y)
                                                   && RelationsUtility.AttractedToGender(x, y.gender) && RelationsUtility.AttractedToGender(y, x.gender));
                    if (b == null) continue;
                    a = x;
                    break;
                }
                if (a != null) a.relations.AddDirectRelation(kind == "spouses" ? PawnRelationDefOf.Spouse : PawnRelationDefOf.Lover, b);
                else how = "friends";
            }
            SkillDef skill = null;
            if (how == "mentor")
            {
                // The one with the most to teach; the least experienced to learn it.
                var best = adults.SelectMany(p => p.skills?.skills ?? new List<SkillRecord>(), (p, r) => (p, r))
                    .Where(x => !x.r.TotallyDisabled).OrderByDescending(x => x.r.Level).ToList();
                foreach (var (mentor, rec) in best)
                {
                    var student = ctx.colonists.Where(p => p != mentor && p.skills?.GetSkill(rec.def) is SkillRecord s && !s.TotallyDisabled && s.Level <= rec.Level - 4)
                        .OrderBy(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();
                    if (student == null) continue;
                    if (ctx.comp.social.StartMentorship(mentor, student, rec.def) == null) break;
                    a = mentor;
                    b = student;
                    skill = rec.def;
                    break;
                }
                if (a == null) how = "friends";
            }
            if (how == "friends")
            {
                var pair = adults.SelectMany(x => adults.Where(y => y != x), (x, y) => (x, y))
                    .OrderByDescending(p => p.x.relations.OpinionOf(p.y) + p.y.relations.OpinionOf(p.x)).FirstOrDefault();
                a = pair.x;
                b = pair.y;
                if (a == null || b == null) return;
                ctx.comp.social.Bump(a, b, TieKind.Friends, 50f, "CC_TieNote_Scenario".Translate(), true);
            }
            a.needs?.mood?.thoughts?.memories?.TryGainMemory(CouncilDefOf.Council_Scen_OldFriend, b);
            b.needs?.mood?.thoughts?.memories?.TryGainMemory(CouncilDefOf.Council_Scen_OldFriend, a);
            ctx.lines.Add(("CC_Scen_Bond_" + how).Translate(a.Named("A"), b.Named("B"), (skill?.label ?? "").Named("SKILL")).Resolve());
            ctx.look.Add(a);
            ctx.look.Add(b);
        }

        public override string Summary(Scenario scen) => ("CC_ScenPart_Bond_" + kind).Translate();

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            var r = Rows(listing, 1);
            if (Widgets.ButtonText(Row(r, 0), ("CC_ScenPart_Bond_" + kind).Translate()))
                Find.WindowStack.Add(new FloatMenu(Kinds.Select(k => new FloatMenuOption(("CC_ScenPart_Bond_" + k).Translate(), () => kind = k)).ToList()));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref kind, "kind", "friends");
        }
    }

    // ===================== The debt comes due =====================

    /// <summary>
    /// A scenario's debt is due: pay it all, pay half and ask for time (it grows, and they only wait twice), or refuse.
    /// Refusing brings the creditor's people now, and again later: until whoever leads them falls, or twice for a loan.
    /// </summary>
    public class HappeningWorker_DebtDue : IncidentWorker_Happening
    {
        /// <summary>The debt being asked about (set just around the firing).</summary>
        internal static Memo pending;

        private string story;

        // Comes when it's due, whatever the world-event settings: it's what the player chose the scenario for.
        protected override bool Gates(Map map, HappeningExtension ext) => Colonists(map).Count > 0;

        protected override bool CanHappen(Map map, HappeningExtension ext) => pending != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var m = pending;
            if (m == null || m.faction == null) return false;
            ctx.enemy = m.faction;
            ctx.faction = m.faction;
            ctx.count = m.amount;
            ctx.variant = m.times;
            var x = m.pawn;
            ctx.stranger = x != null && !x.Dead && !x.Discarded && !x.Spawned && x.Faction == m.faction ? x : null;
            ctx.scenario = true;
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile())
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
            story = m.def == "heist" ? "heist" : "loan";
            return ctx.a != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(story ?? "loan");
            if (ctx.variant > 0) tags.Add("late");
            if (ctx.stranger != null) tags.Add("boss");
        }

        // The boss is someone the colony knows: never thrown away.
        protected override void Abort(HappeningContext ctx) => ctx.stranger = null;

        protected override string Extra(HappeningContext ctx)
        {
            string have = "CC_Evt_TributeHave".Translate(ctx.map.resourceCounter.Silver, ctx.count).Resolve();
            return ctx.variant < 2 ? have + " " + "CC_Scen_DebtHalf".Translate(Later(ctx.count)).Resolve() : have + " " + "CC_Scen_NoMoreTime".Translate().Resolve();
        }

        /// <summary>What's left to pay after half now, with their interest.</summary>
        private static int Later(int amount) => Mathf.RoundToInt((amount - Mathf.CeilToInt(amount / 2f)) * 1.35f / 10f) * 10;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option == 1 && ctx.variant >= 2 ? "CC_Scen_NoMoreTime".Translate().ToString() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            outcome = base.Resolved(ctx, option, risk, byDefault, outcome);
            var comp = CouncilGameComponent.Instance;
            var t = comp?.happenings;
            var m = t?.memos.FirstOrDefault(x => x.kind == "debt" && !x.used && x.faction == ctx.enemy && x.due == int.MaxValue);
            if (m == null) return outcome;
            int now = Find.TickManager.TicksGame;
            switch (option)
            {
                case 0:
                    t.Use(m);
                    foreach (var p in Colonists(ctx.map)) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Scen_DebtPaid);
                    // Paid back: the one who was robbed calls it square.
                    if (m.pawn != null) comp.nemeses.Settle(m.pawn, "CC_Nemesis_End_Paid".Translate(m.pawn.LabelShort).Resolve());
                    break;
                case 1:
                    m.amount = Mathf.Max(10, Later(m.amount));
                    m.times++;
                    m.due = now + Mathf.RoundToInt(Rand.Range(18f, 24f) * GenDate.TicksPerDay);
                    break;
                default:
                    t.Use(m);
                    // They'll be back: a few more times with the one who was wronged at their head, or collectors once more.
                    bool led = ctx.stranger != null;
                    var hunt = t.Remember("hunt", ctx.stranger, m.faction, m.def);
                    hunt.everyMin = led ? 30f : 20f;
                    hunt.everyMax = led ? 45f : 30f;
                    hunt.left = led ? 2 : 1;
                    hunt.due = now + Mathf.RoundToInt(Rand.Range(hunt.everyMin, hunt.everyMax) * GenDate.TicksPerDay);
                    break;
            }
            return outcome;
        }
    }
}
