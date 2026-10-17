using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // More of Rimfolk's scenario parts (see Scenarios.cs): a council already seated, the days a people brought with them,
    // a life goal from before, a people the colony is beholden to, children in its care, and a world event set in motion.
    // Like the others they are set up once, a few seconds into the game, and each adds its line to the one letter.

    // ===================== A council already formed =====================

    /// <summary>
    /// Some of the council's seats are filled before anyone votes: by the eldest who can hold them (a tribe's elder), or
    /// by those best at the work (a charter that names its officers). Quietly; this year's election counts as held, and
    /// an empty seat still has its by-election. Nothing with the offices switched off.
    /// </summary>
    public class ScenPart_CouncilSeats : ScenPart_Council
    {
        public List<OfficeDef> offices = new List<OfficeDef>();
        /// <summary>"eldest" or "best".</summary>
        public string how = "best";

        public override void Setup(ScenarioSetupContext ctx)
        {
            if (!ColonyCouncilMod.Settings.offices || offices.NullOrEmpty()) return;
            var seated = new List<string>();
            int grown = ctx.Adults.Count;
            foreach (var office in offices.Where(o => o != null && o.Available))
            {
                // A seat for a bigger colony waits for one (but the eldest's place at the fire is the custom's, not the vote's).
                if (how != "eldest" && office.minColonists > grown) continue;
                var able = ctx.Adults.Where(p => p.Spawned && office.CanHold(p) && ctx.comp.offices.OfficeOf(p) == null);
                var pick = how == "eldest"
                    ? able.OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault()
                    : able.OrderByDescending(p => office.Competence(p)).FirstOrDefault();
                if (pick == null || !ctx.comp.offices.Seat(office, pick)) continue;
                seated.Add("CC_Scen_Seat".Translate(pick.Named("PAWN"), office.label.Named("OFFICE")).Resolve());
                ctx.look.Add(pick);
            }
            if (seated.Count == 0) return;
            string key = how == "eldest" ? "CC_Scen_Seats_Eldest" : "CC_Scen_Seats";
            ctx.lines.Add(key.Translate(TextUtil.JoinAnd(seated).Named("SEATS")).Resolve());
        }

        public override string Summary(Scenario scen) =>
            ("CC_ScenPart_Seats_" + how).Translate(offices.Where(o => o != null).Select(o => o.label).ToCommaList(true));

        public override bool HasNullDefs() => base.HasNullDefs() || offices == null || offices.Any(o => o == null);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref offices, "offices", LookMode.Def);
            Scribe_Values.Look(ref how, "how", "best");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (offices == null) offices = new List<OfficeDef>();
                offices.RemoveAll(o => o == null);
            }
        }
    }

    // ===================== Days the colony keeps =====================

    /// <summary>One day a people brought with them: its name, how it's kept, how many days from the start it first comes.</summary>
    public class ScenDay : IExposable
    {
        public string label;
        public DayWay way = DayWay.Feast;
        public int inDays = 10;

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref way, "way", DayWay.Feast);
            Scribe_Values.Look(ref inDays, "inDays", 10);
        }
    }

    /// <summary>
    /// The colony starts with customs of its own: days it brought from the old home, on the calendar as traditions from
    /// the first year (so letting one pass is a tradition broken). They count toward a name for keeping its days only
    /// once they have been kept here, year after year. Nothing with the calendar switched off.
    /// </summary>
    public class ScenPart_CouncilDays : ScenPart_Council
    {
        public List<ScenDay> days = new List<ScenDay>();

        public override void Setup(ScenarioSetupContext ctx)
        {
            var cal = CalendarTracker.Instance;
            if (!CalendarTracker.On || cal == null || days.NullOrEmpty()) return;
            int today = GenLocalDate.DayOfYear(ctx.map);
            int year = GenLocalDate.Year(ctx.map);
            int now = Find.TickManager.TicksGame;
            var named = new List<string>();
            for (int i = 0; i < days.Count; i++)
            {
                var sd = days[i];
                if (sd == null || sd.label.NullOrEmpty()) continue;
                int day = CalendarMath.Wrap(today + Mathf.Max(1, sd.inDays));
                // As if the colony had named it (it comes in its first year, and the player can rename or drop it), and a
                // tradition already.
                var d = new CalendarDay
                {
                    id = "scenario|" + i, kind = CalendarKind.Holiday, way = sd.way == DayWay.Personal ? DayWay.Feast : sd.way, label = sd.label,
                    byPlayer = true, day = day, sinceYear = year, tick = now, weight = CalendarMath.FoundingWeight, tradition = true,
                    lastYear = CalendarMath.InitialLastYear(today, year, day, false)
                };
                if (cal.TryAdd(d, false) == null) continue;
                named.Add("CC_Scen_Day".Translate(sd.label.Named("NAME"), ("CC_Cal_Way_" + d.way).Translate().Named("WAY"),
                    Mathf.Max(1, sd.inDays).Named("DAYS")).Resolve());
            }
            if (named.Count > 0) ctx.lines.Add("CC_Scen_Days".Translate(TextUtil.JoinAnd(named).Named("DAYS")).Resolve());
        }

        public override string Summary(Scenario scen) => "CC_ScenPart_Days".Translate(days.Where(d => d != null).Select(d => d.label).ToCommaList(true));

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref days, "days", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && days == null) days = new List<ScenDay>();
        }
    }

    // ===================== A life goal from before =====================

    /// <summary>
    /// Someone came with their heart already set on something: the one whose kin the feud killed, the one who carries
    /// the secret, the eldest, or one who looks after the children. Nothing with life goals switched off, or if the goal
    /// doesn't fit them.
    /// </summary>
    public class ScenPart_CouncilGoal : ScenPart_Council
    {
        public AspirationDef goal;
        /// <summary>"avenger" (whom the sworn enemy has a quarrel with), "keeper" (the secret's), "eldest", "carer", or "any".</summary>
        public string who = "any";
        /// <summary>Whatever their age (a goal meant for the old, wanted early).</summary>
        public bool anyAge;

        public override void Setup(ScenarioSetupContext ctx)
        {
            if (goal == null || !ColonyCouncilMod.Settings.aspirations) return;
            var adults = ctx.Adults;
            IEnumerable<Pawn> order;
            switch (who)
            {
                case "avenger": order = new[] { ctx.victim }; break;
                case "keeper": order = new[] { ctx.keeper }; break;
                case "eldest": order = adults.OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat); break;
                case "carer": order = (ctx.carers.Count > 0 ? ctx.carers : adults).InRandomOrder(); break;
                default: order = adults.InRandomOrder(); break;
            }
            foreach (var p in order.Where(p => p != null).ToList())
            {
                var a = ctx.comp.aspirations.GiveFromStart(ctx.comp, p, goal, anyAge);
                if (a == null) continue;
                ctx.lines.Add("CC_Scen_Goal".Translate(p.Named("PAWN"), a.Goal.Named("GOAL")).Resolve());
                ctx.look.Add(p);
                return;
            }
        }

        public override string Summary(Scenario scen) =>
            "CC_ScenPart_Goal".Translate(goal?.goal == null ? "?"
                : goal.goal.Formatted((goal.skill?.label ?? "").Named("SKILL"), goal.amount.ToString("0").Named("COUNT")).Resolve());

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref goal, "goal");
            Scribe_Values.Look(ref who, "who", "any");
            Scribe_Values.Look(ref anyAge, "anyAge");
        }
    }

    // ===================== Beholden to a people =====================

    /// <summary>
    /// The colony holds its land by another people's grant, and owes them for it: they remember it (a debt in the
    /// region's memory, World/FactionMemory.cs) and will one day call it in, as a favour asked. Their goodwill starts warm.
    /// A people of the old ways first (a lord's realm), then the Empire, then any people at peace with the colony.
    /// </summary>
    public class ScenPart_CouncilBeholden : ScenPart_Council
    {
        /// <summary>The deed they remember (a FactionMemory kind on their side: "granted").</summary>
        public string deed = "granted";
        /// <summary>How big a debt it is (it fades slowly from there).</summary>
        public int weight = 25;
        /// <summary>Their goodwill to start with, at least.</summary>
        public int goodwill = 40;

        public static Faction Liege()
        {
            var peoples = Find.FactionManager.AllFactionsListForReading
                .Where(f => FactionMemory.Remembers(f) && !f.defeated && !f.HostileTo(Faction.OfPlayer) && !f.def.permanentEnemy).ToList();
            return peoples.Where(f => f.def.techLevel == TechLevel.Medieval).RandomElementWithFallback()
                   ?? peoples.Where(f => ModsConfig.RoyaltyActive && f == Faction.OfEmpire).RandomElementWithFallback()
                   ?? peoples.Where(f => f.def.techLevel <= TechLevel.Medieval).RandomElementWithFallback()
                   ?? peoples.RandomElementWithFallback();
        }

        public override void Setup(ScenarioSetupContext ctx)
        {
            var f = Liege();
            if (f == null) return;
            string lord = f.leader != null && !f.leader.Dead ? f.leader.LabelShort : null;
            if (f.PlayerGoodwill < goodwill) f.TryAffectGoodwillWith(Faction.OfPlayer, goodwill - f.PlayerGoodwill, false, false);
            var d = FactionMemory.Note(f, deed, lord, weight);
            string key = "CC_Scen_Beholden_" + deed + (lord != null ? "_Lord" : "");
            if (!key.CanTranslate()) key = "CC_Scen_Beholden";
            string line = key.Translate(f.Name.Named("FACTION"), (lord ?? "").Named("LORD")).Resolve();
            if (d != null) line += " " + "CC_Scen_Beholden_Remembered".Translate(f.Name.Named("FACTION")).Resolve();
            ctx.lines.Add(line);
            ctx.comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, line));
        }

        public override string Summary(Scenario scen) => "CC_ScenPart_Beholden".Translate();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref deed, "deed", "granted");
            Scribe_Values.Look(ref weight, "weight", 25);
            Scribe_Values.Look(ref goodwill, "goodwill", 40);
        }
    }

    // ===================== Children in the colony's care =====================

    /// <summary>
    /// Children come with the colonists, with nobody of their own: the grown-ups who brought them are all the family they
    /// have (they start fond of each other). Only with Biotech, where there are children at all; without it, nothing.
    /// </summary>
    public class ScenPart_CouncilWards : ScenPart_Council
    {
        public IntRange count = new IntRange(3, 4);
        public FloatRange ages = new FloatRange(5f, 12f);

        public override IEnumerable<Thing> PlayerStartingThings()
        {
            if (!ModsConfig.BiotechActive || Faction.OfPlayerSilentFail == null) yield break;
            int n = Mathf.Clamp(count.RandomInRange, 1, 6);
            for (int i = 0; i < n; i++)
            {
                Pawn child = null;
                try
                {
                    var req = new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
                        forceGenerateNewPawn: true, canGeneratePawnRelations: false, colonistRelationChanceFactor: 0f,
                        developmentalStages: DevelopmentalStage.Child, biologicalAgeRange: ages);
                    child = PawnGenerator.GeneratePawn(req);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("scenario wards", e);
                }
                if (child != null) yield return child;
            }
        }

        public override void Setup(ScenarioSetupContext ctx)
        {
            // apart: the start of a game, what the children and the grown-ups who brought them already are to each other.
            var kids = ctx.colonists.Where(p => p.DevelopmentalStage.Juvenile()).ToList();
            var carers = ctx.Adults;
            if (kids.Count == 0 || carers.Count == 0) return;
            ctx.wards.AddRange(kids);
            ctx.carers.AddRange(carers);
            foreach (var c in carers)
            foreach (var k in kids)
            {
                c.needs?.mood?.thoughts?.memories?.TryGainMemory(CouncilDefOf.Council_Scen_OldFriend, k);
                k.needs?.mood?.thoughts?.memories?.TryGainMemory(CouncilDefOf.Council_Scen_OldFriend, c);
                if (ColonyCouncilMod.Settings.socialWeb) ctx.comp.social.Bump(c, k, TieKind.Friends, 30f, "CC_TieNote_Scenario".Translate(), true);
            }
            ctx.lines.Add("CC_Scen_Wards".Translate(ScenarioSetup.Names(carers).Named("CARERS"), ScenarioSetup.Names(kids).Named("NAMES"),
                kids.Count.Named("COUNT")).Resolve());
            ctx.look.AddRange(kids);
        }

        public override string Summary(Scenario scen) => "CC_ScenPart_Wards".Translate(count.min, count.max);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref count, "count", new IntRange(3, 4));
            Scribe_Values.Look(ref ages, "ages", new FloatRange(5f, 12f));
        }
    }

    // ===================== A world event set in motion =====================

    /// <summary>
    /// Something already set in motion beyond the walls comes to a head around a set day: a world event (a ship coming
    /// down, a blood feud), started then if it can be. It waits for a quiet moment and a free place among the world
    /// events, tries again each day for a month, and never comes with the world events switched off.
    /// </summary>
    public class ScenPart_CouncilSituation : ScenPart_Council
    {
        public WorldSituationDef situation;
        public FloatRange days = new FloatRange(12f, 18f);
        /// <summary>A line for the letter that says how things stand (a key; none for a surprise).</summary>
        public string hint;

        public override void Setup(ScenarioSetupContext ctx)
        {
            if (situation == null || !ColonyCouncilMod.Settings.worldSituations) return;
            var m = ctx.comp.happenings.Remember("situation", null, null, null, situation.defName);
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Mathf.Max(1f, Days(days)) * GenDate.TicksPerDay);
            if (!hint.NullOrEmpty() && hint.CanTranslate()) ctx.lines.Add(hint.Translate().Resolve());
        }

        /// <summary>The day has come: the world event starts now, or tomorrow, for a month.</summary>
        public static void Begin(HappeningTracker t, Memo m, Map map, int now)
        {
            var def = DefDatabase<WorldSituationDef>.GetNamedSilentFail(m.text ?? "");
            var inst = WorldSituations.Instance;
            if (def == null || inst == null || !ColonyCouncilMod.Settings.worldSituations)
            {
                t.Use(m);
                return;
            }
            if (inst.ActiveOf(def) != null)
            {
                t.Use(m);
                return;
            }
            bool quiet = map != null && !MapFacts.ThreatOn(map) && !HappeningTracker.Waiting && LetterGap.Open && inst.Running.Count() < WorldSituations.MaxActive;
            if (quiet && inst.TryStart(map, d => d == def, true) != null)
            {
                t.Use(m);
                return;
            }
            if (++m.times > 30) t.Use(m);
            else m.due = now + GenDate.TicksPerDay;
        }

        public override string Summary(Scenario scen) =>
            "CC_ScenPart_Situation".Translate(situation?.label ?? "?", Mathf.RoundToInt(days.min), Mathf.RoundToInt(days.max));

        public override bool HasNullDefs() => base.HasNullDefs() || situation == null;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref situation, "situation");
            Scribe_Values.Look(ref days, "days");
            Scribe_Values.Look(ref hint, "hint");
        }
    }
}
