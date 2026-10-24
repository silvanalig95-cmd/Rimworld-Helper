using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== The challenger =====================

    /// <summary>
    /// A travelling champion challenges the colony's best at something: a shooting match at targets, wrestling (no
    /// weapons), crafting, cooking, art, a debate, handling animals. The colony may bet silver on its champion. Nobody
    /// gets hurt: it's a contest, not a fight.
    /// </summary>
    public class HappeningWorker_Champion : IncidentWorker_Happening
    {
        private static readonly string[] SkillNames = { "Shooting", "Melee", "Crafting", "Artistic", "Cooking", "Social", "Animals", "Intellectual", "Plants" };

        private static IEnumerable<(Pawn p, SkillDef s)> Champions(Map map)
        {
            var pawns = Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState).ToList();
            foreach (var name in SkillNames)
            {
                var sk = DefDatabase<SkillDef>.GetNamedSilentFail(name);
                if (sk == null) continue;
                bool violent = sk == SkillDefOf.Shooting || sk == SkillDefOf.Melee;
                var best = pawns.Where(p => p.skills?.GetSkill(sk) is SkillRecord r && !r.TotallyDisabled && (!violent || !p.WorkTagIsDisabled(WorkTags.Violent)))
                    .OrderByDescending(p => p.skills.GetSkill(sk).Level).FirstOrDefault();
                if (best != null && best.skills.GetSkill(sk).Level >= 6) yield return (best, sk);
            }
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Arrivals.Friendly() != null && Champions(map).Any() && !(CouncilGameComponent.Instance?.happenings.visits.Any(v => v.kind == "contest") ?? false);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var pick = Champions(ctx.map).RandomElementWithFallback();
            if (pick.p == null) return false;
            ctx.a = pick.p;
            ctx.skill = pick.s;
            ctx.contest = true;
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            int level = ctx.a.skills.GetSkill(ctx.skill).Level;
            for (int i = 0; i < 4 && ctx.stranger == null; i++)
            {
                var x = Arrivals.Make(Arrivals.PersonKind(ctx.faction), ctx.faction, ext.ages);
                var r = x.skills?.GetSkill(ctx.skill);
                if (r == null || r.TotallyDisabled)
                {
                    Arrivals.Discard(x);
                    continue;
                }
                r.Level = Mathf.Clamp(level + Rand.RangeInclusive(-3, 4), 5, 20);
                r.passion = Passion.Major;
                ctx.stranger = x;
            }
            if (ctx.stranger == null) return false;
            ctx.count = Mathf.Clamp(Mathf.RoundToInt((40f + level * 12f) / 10f) * 10, 50, 400);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.stranger != null && !ctx.stranger.Spawned) Arrivals.Discard(ctx.stranger);
            ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.skill != null) tags.Add(ctx.skill.defName);
        }

        protected override string Extra(HappeningContext ctx)
        {
            int ours = ctx.a?.skills?.GetSkill(ctx.skill)?.Level ?? 0;
            int theirs = ctx.stranger?.skills?.GetSkill(ctx.skill)?.Level ?? 0;
            return "CC_Evt_ChampionOdds".Translate(ctx.a.Named("A"), ctx.stranger.Named("X"), ours, theirs, ctx.SkillText).Resolve();
        }

        /// <summary>
        /// The contest, a couple of hours after the colony accepted (if both are still around), held where the two meet:
        /// at once if our champion is with the challenger, else when the champion walks over in their free time
        /// (Together), and it happens when they get there.
        /// </summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            if (v.done) return false;
            if (now < v.due) return true;
            var a = v.a;
            var x = v.x;
            bool aOk = a != null && !a.Dead && a.Spawned && a.Map == v.map && !a.Downed && !a.InMentalState;
            bool xOk = x != null && !x.Dead && x.Spawned && x.Map == v.map && !x.Downed;
            if (!aOk || !xOk || v.skill == null)
            {
                // Our champion is busy or hurt: the challenger waits while they can.
                if (now < v.until && xOk)
                {
                    v.due = now + GenDate.TicksPerHour * 2;
                    return true;
                }
                News.Post("CC_Evt_ContestOff".Translate().Resolve(), null, NewsKind.World, MessageTypeDefOf.NeutralEvent);
                return false;
            }
            string key = "contest|" + x.thingIDNumber;
            if (Together.When(key, a, x, () =>
                {
                    if (!v.done) Contest(v, comp);
                }, 8f, 0f))
                return false;
            // Still waiting for the two to meet; past the challenger's stay and the hours after it, it's off.
            if (now > v.until + GenDate.TicksPerHour * 8)
            {
                Together.Forget(key);
                News.Post("CC_Evt_ContestOff".Translate().Resolve(), null, NewsKind.World, MessageTypeDefOf.NeutralEvent);
                return false;
            }
            return true;
        }

        /// <summary>The contest itself, with the champion and the challenger together.</summary>
        private static void Contest(Visit v, CouncilGameComponent comp)
        {
            // together: called once the champion and the challenger are together (Hour, through Together.When).
            var a = v.a;
            var x = v.x;
            if (a == null || x == null || a.Dead || x.Dead || v.skill == null) return;
            v.done = true;
            int al = a.skills.GetSkill(v.skill)?.Level ?? 0;
            int xl = x.skills?.GetSkill(v.skill)?.Level ?? 0;
            bool won = Rand.Chance(Mathf.Clamp(0.5f + (al - xl) * 0.07f, 0.1f, 0.9f));
            var ctx = new HappeningContext { incident = v.incident, map = v.map, a = a, stranger = x, skill = v.skill, count = v.stake, contest = true };
            string what = ctx.SkillText;
            var ext = v.Ext;
            using var _cause = Causes.Push("CC_Cause_Challenge".Translate(what).Resolve());
            Arrivals.Say(a, (won ? "CC_Evt_Contest_Won" : "CC_Evt_Contest_Lost").Translate().Resolve());
            Arrivals.Say(x, (won ? "CC_Evt_Contest_Lost" : "CC_Evt_Contest_Won").Translate().Resolve());
            if (won)
            {
                CouncilGameComponent.GiveThought(a, CouncilDefOf.Council_Evt_WonChallenge);
                foreach (var p in IncidentWorker_Happening.Colonists(v.map).Where(p => p != a)) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Evt_ChampionWon, a);
                // The challenger counts the stake out in person (Couriers/), or it's left by the winner, as before.
                if (v.stake > 0 && !(Couriers.InPerson(x, a.Map) && Couriers.Deliver(a.Map, Couriers.Make(ThingDefOf.Silver, v.stake), x.Faction, a.Position, x)))
                {
                    var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = v.stake;
                    GenPlace.TryPlaceThing(silver, a.Position, a.Map, ThingPlaceMode.Near);
                }
                a.skills.Learn(v.skill, 600f, true);
            }
            else
            {
                CouncilGameComponent.GiveThought(a, CouncilDefOf.Council_Evt_LostChallenge);
                if (v.stake > 0) new DilemmaEffect_Consume { what = "silver", count = v.stake }.Apply(ctx);
                a.skills.Learn(v.skill, 900f, true);
            }
            string key = won ? (v.stake > 0 ? "CC_Evt_ContestWonStake" : "CC_Evt_ContestWon") : (v.stake > 0 ? "CC_Evt_ContestLostStake" : "CC_Evt_ContestLost");
            if (Rand.Bool) key += "B";
            string text = key
                .Translate(a.Named("A"), x.Named("X"), what, v.stake).Resolve();
            Find.LetterStack.ReceiveLetter((won ? "CC_Evt_ContestWonTitle" : "CC_Evt_ContestLostTitle").Translate(a.Named("A")).Resolve(), text,
                won ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent, new LookTargets(a));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, a));
            comp.journals.Write(a, won ? "Council_Journal_Evt_ContestWon" : "Council_Journal_Evt_ContestLost", null, x.Named("X"), what.Named("SKILL"));
            // They'll want a rematch one day (or to defend their title).
            // (Once: a rematch settles it.)
            if (x.Faction != null && x.RaceProps.Humanlike && !(v.incident?.Worker is HappeningWorker_Rematch))
                comp.happenings.Remember("challenger", x, x.Faction, v.skill.defName, null, won, a);
            Arrivals.SendAway(new[] { x });
        }
    }

    /// <summary>The colony takes up the challenge: the contest happens in a couple of hours.</summary>
    public class DilemmaEffect_Contest : DilemmaEffect
    {
        public bool stake;

        public override string Unavailable(DilemmaContext ctx) =>
            stake && ctx.map != null && ctx.map.resourceCounter.Silver < ctx.count ? "CC_Dilemma_NotEnough".Translate(ctx.count, ThingDefOf.Silver.label).ToString() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var h = ctx as HappeningContext;
            var t = CouncilGameComponent.Instance?.happenings;
            if (h == null || t == null || ctx.a == null || ctx.stranger == null) return;
            int now = Find.TickManager.TicksGame;
            var v = t.AddVisit("contest", h.incident, ctx.map, ctx.stranger, ctx.a, null, now + GenDate.TicksPerDay);
            v.skill = ctx.skill;
            v.stake = stake ? ctx.count : 0;
            v.due = now + Mathf.RoundToInt(GenDate.TicksPerHour * Rand.Range(1.5f, 3f));
        }
    }

    // ===================== The great beast =====================

    /// <summary>
    /// A legendary animal roams the land for a few days: bigger, tougher, with a name. Bring it down (or tame it) for
    /// glory, or let it pass. It only fights if provoked, like any animal.
    /// </summary>
    public class HappeningWorker_GreatBeast : IncidentWorker_Happening
    {
        /// <summary>A great beast can be any big, calm animal of the land: never a predator (a legend that hunts people would be a raid, not a challenge).</summary>
        public static bool Fits(PawnKindDef k, Map map) =>
            k?.race?.race != null && k.RaceProps.Animal && !k.RaceProps.Insect && !k.RaceProps.Dryad && !k.RaceProps.predator
            && k.RaceProps.baseBodySize >= 1f && k.combatPower >= 70f && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f);

        private static IEnumerable<PawnKindDef> Kinds(Map map) => PokeWorldLink.WildKinds(map.Biome.AllWildAnimals.Where(k => Fits(k, map)));

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            CouncilGameComponent.Instance?.happenings.beast == null && Kinds(map).Any();

        protected virtual PawnKindDef PickKind(HappeningContext ctx) =>
            Kinds(ctx.map).RandomElementByWeightWithFallback(k => k.RaceProps.baseBodySize * k.RaceProps.baseBodySize);

        protected virtual string PickName(HappeningContext ctx, HappeningExtension ext, PawnKindDef kind) =>
            ext.lines.NullOrEmpty() ? kind.label : Flavor.PickText(ext.lines);

        /// <summary>How old it is, as a share of its kind's lifespan.</summary>
        protected virtual FloatRange AgeShare => new FloatRange(0.5f, 0.7f);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var kind = PickKind(ctx);
            if (kind == null) return false;
            float age = kind.RaceProps.lifeExpectancy * AgeShare.RandomInRange;
            var beast = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, fixedBiologicalAge: age, fixedChronologicalAge: age));
            ctx.beast = PickName(ctx, ext, kind);
            beast.Name = new NameSingle(ctx.beast);
            if (CouncilDefOf.Council_Legendary != null) beast.health.AddHediff(CouncilDefOf.Council_Legendary);
            ctx.stranger = beast;
            ctx.thing = kind.race;
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.WorkTagIsDisabled(WorkTags.Violent))
                .OrderByDescending(p => Mathf.Max(p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0, p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0))
                .FirstOrDefault() ?? Colonists(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var beast = ctx.stranger;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, ctx.map, CellFinder.EdgeRoadChance_Animal, false, null)) return false;
            GenSpawn.Spawn(beast, cell, ctx.map);
            beast.mindState.exitMapAfterTick = Find.TickManager.TicksGame + Mathf.RoundToInt(ext.days.RandomInRange * GenDate.TicksPerDay);
            var t = CouncilGameComponent.Instance.happenings;
            t.beast = beast;
            t.beastName = ctx.beast;
            t.beastTick = Find.TickManager.TicksGame;
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.stranger != null && !ctx.stranger.Spawned && !ctx.stranger.Discarded) Find.WorldPawns.PassToWorld(ctx.stranger, PawnDiscardDecideMode.Discard);
            ctx.stranger = null;
        }

        /// <summary>Gone back into the wilds, or tamed.</summary>
        public static void Hourly(HappeningTracker t, CouncilGameComponent comp)
        {
            var beast = t.beast;
            if (beast == null) return;
            if (beast.Dead || beast.Discarded)
            {
                t.beast = null;
                return;
            }
            if (beast.Faction == Faction.OfPlayer)
            {
                var tamer = beast.playerSettings?.Master ?? beast.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
                if (tamer != null) CouncilGameComponent.GiveThought(tamer, CouncilDefOf.Council_Evt_TamedBeast);
                string text = "CC_Evt_BeastTamed".Translate(t.beastName ?? beast.LabelShort, (tamer?.LabelShort ?? "?").Named("PAWN")).Resolve();
                Find.LetterStack.ReceiveLetter("CC_Evt_BeastTamedTitle".Translate(t.beastName ?? beast.LabelShort).Resolve(), text, LetterDefOf.PositiveEvent, beast);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, tamer));
                t.beast = null;
                return;
            }
            if (!beast.Spawned)
            {
                News.Post("CC_Evt_BeastGone".Translate(t.beastName ?? beast.LabelShort).Resolve(), null, NewsKind.Land, MessageTypeDefOf.NeutralEvent);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_Evt_BeastGone".Translate(t.beastName ?? beast.LabelShort).Resolve()));
                t.beast = null;
            }
        }

        /// <summary>The beast is dying (Harmony hook on Kill, before the death).</summary>
        public static void Slain(HappeningTracker t, Pawn beast, DamageInfo? dinfo)
        {
            var comp = CouncilGameComponent.Instance;
            string name = t.beastName ?? beast.LabelShort;
            t.beast = null;
            if (comp == null) return;
            var killer = dinfo?.Instigator as Pawn;
            if (killer == null || killer.Faction != Faction.OfPlayer || !killer.RaceProps.Humanlike)
            {
                News.Post("CC_Evt_BeastDied".Translate(name).Resolve(), beast, NewsKind.Land, MessageTypeDefOf.NeutralEvent);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_Evt_BeastDied".Translate(name).Resolve()));
                return;
            }
            // apart: a great beast felled; the colony hears of it.
            using var _cause = Causes.Push("CC_Cause_Beast".Translate(name).Resolve());
            CouncilGameComponent.GiveThought(killer, CouncilDefOf.Council_Evt_FelledBeast);
            var map = beast.MapHeld ?? killer.MapHeld;
            if (map != null)
                foreach (var p in IncidentWorker_Happening.Colonists(map).Where(p => p != killer)) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Evt_BeastDown, killer);
            // Its young may come one day, to take its place.
            t.Remember("beast", null, null, beast.kindDef?.defName, name, false, killer);
            string text = "CC_Evt_BeastSlain".Translate(name, killer.Named("PAWN")).Resolve();
            Find.LetterStack.ReceiveLetter("CC_Evt_BeastSlainTitle".Translate(name).Resolve(), text, LetterDefOf.PositiveEvent, new LookTargets(killer));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, killer));
            comp.journals.Write(killer, "Council_Journal_Evt_Beast", null, name.Named("BEAST"));
        }
    }

    // ===================== The treasure map =====================

    /// <summary>A visitor or trader offers a map to something buried on the colony's land. It may be true.</summary>
    public class HappeningWorker_TreasureMap : IncidentWorker_Happening
    {
        private static IEnumerable<Pawn> Sellers(Map map) =>
            Strangers.On(map, StrangerKind.Visitor).Concat(Strangers.On(map, StrangerKind.Trader))
                .Where(x => x.RaceProps.Humanlike && x.Awake() && !x.Downed && x.Faction != null && !x.Faction.HostileTo(Faction.OfPlayer));

        protected override bool CanHappen(Map map, HappeningExtension ext) => Sellers(map).Any() && Spot(map, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.stranger = Sellers(ctx.map).RandomElementWithFallback();
            if (ctx.stranger == null) return false;
            ctx.faction = ctx.stranger.Faction;
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
            if (ctx.a == null) return false;
            float wealth = Mathf.Clamp(ctx.map.wealthWatcher.WealthTotal / 50000f, 0.6f, 2.5f);
            ctx.count = Mathf.RoundToInt(ext.amount.RandomInRange * wealth / 10f) * 10;
            return ctx.count > 0;
        }

        protected override void Abort(HappeningContext ctx) => ctx.stranger = null;

        /// <summary>Somewhere out on the land, away from the colony.</summary>
        private static bool Spot(Map map, out IntVec3 cell)
        {
            var center = map.IsPlayerHome && map.mapPawns.FreeColonistsSpawned.Any() ? map.mapPawns.FreeColonistsSpawned.First().Position : map.Center;
            return CellFinderLoose.TryGetRandomCellWith(c => c.Standable(map) && !c.Fogged(map) && !c.Roofed(map) && c.GetFirstBuilding(map) == null
                                                             && !map.areaManager.Home[c] && c.DistanceTo(center) > 25f, map, 500, out cell);
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Bought (blind, or after someone looked it over), and true: the cache is where the map says.
            if ((option == 0 || option == 2) && !risk && ctx.map != null && Spot(ctx.map, out var cell))
            {
                var things = Loot.Make(ctx.count * Rand.Range(2f, 3.5f), Rand.Chance(0.35f));
                foreach (var th in things) GenPlace.TryPlaceThing(th, cell, ctx.map, ThingPlaceMode.Near);
                if (ctx.a != null) CouncilGameComponent.GiveThought(ctx.a, CouncilDefOf.Council_Evt_TreasureFound);
                Find.LetterStack.ReceiveLetter("CC_Evt_TreasureTitle".Translate().Resolve(),
                    "CC_Evt_TreasureText".Translate(Loot.Describe(things)).Resolve(), LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(cell, ctx.map)));
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        public override void Cleanup(HappeningContext ctx)
        {
            // The seller is a visitor or trader already here: nothing to let go of.
        }
    }
}
