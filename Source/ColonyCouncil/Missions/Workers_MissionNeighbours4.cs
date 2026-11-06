using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // The fourth round of neighbours' requests (the player's request, 6 November 2026; texts in
    // Defs/MissionDefs/Rimfolk_Missions_Neighbours4.xml, the pure rules in NeighboursFourMath.cs): hands lent to a
    // neighbour's town for a few days (builders after a storm, a healer for their sickhouse, a teacher for their young, a
    // witness at a truce, a smith before the harvest), a store of theirs kept in the colony's stockpiles over a season, a
    // young beast of theirs raised at the colony, someone of theirs lost on the colony's land (an old one wandered off, a
    // hunter overdue), and a traveller of theirs walked to their town by the colony's caravan. Each is told from data on
    // its def and plays out where it says: on the world map by the colony's caravan, or on the colony's own map.

    /// <summary>
    /// Every mission offer shares one draw (<see cref="NeighboursFourMath.MissionPoolWeight"/>): the factor on a mission's
    /// base chance, in the storyteller's pool and in Rimfolk's own (Happenings.cs), 1 for anything else.
    /// </summary>
    public static class MissionPool
    {
        private static float total = -1f;

        public static float Factor(IncidentDef def)
        {
            if (def?.workerClass == null || !typeof(HappeningWorker_Mission).IsAssignableFrom(def.workerClass)) return 1f;
            if (total < 0f)
            {
                total = 0f;
                foreach (var d in DefDatabase<IncidentDef>.AllDefsListForReading)
                    if (d.workerClass != null && typeof(HappeningWorker_Mission).IsAssignableFrom(d.workerClass) && d.baseChance > 0f)
                        total += d.baseChance;
            }
            return NeighboursFourMath.PoolFactor(total);
        }
    }

    /// <summary>One way a request can come: a tag for its texts, when it fits (the season, the colony's era, the asking people's).</summary>
    public class MissionWay
    {
        public string tag;
        public float weight = 1f;
        public List<Season> seasons;
        /// <summary>The colony's era, as a tag expression ("lowtech", "industrial|spacer").</summary>
        public string era;
        /// <summary>The asking people's: lowtech (tribal or medieval) or hightech (industrial and later).</summary>
        public string theirs;

        public bool Fits(Map map, Faction f) =>
            (seasons.NullOrEmpty() || map != null && seasons.Contains(GenLocalDate.Season(map))) && HappeningWorker_Setting.EraFits(era)
            && (theirs.NullOrEmpty() || f == null || (theirs == "lowtech") == (f.def.techLevel <= TechLevel.Medieval));

        public static MissionWay Pick(List<MissionWay> ways, Map map, Faction f) =>
            ways?.Where(w => w != null && w.Fits(map, f)).RandomElementByWeightWithFallback(w => Mathf.Max(0.01f, w.weight));

        public static bool Any(List<MissionWay> ways, Map map) => ways.NullOrEmpty() || ways.Any(w => w != null && w.Fits(map, null));
    }

    // ===================== Hands lent to a neighbour's town =====================

    /// <summary>What a request for hands asks: who (good at what), how many, how long at the town, and what it brings.</summary>
    public class MissionHands : DefModExtension
    {
        public string kind;
        public float restDays = 60f;
        public int rating = 2;
        /// <summary>The farthest their town may be, in a caravan's days.</summary>
        public float maxTravelDays = 3f;
        public SkillDef skill;
        public int minLevel = 4;
        /// <summary>A hand must be able to do this work too (Doctor for a healer), if given.</summary>
        public WorkTypeDef work;
        public int hands = 1;
        /// <summary>Hours the hands stay at the town for the whole share.</summary>
        public int hours = 36;
        /// <summary>The silver, before the colony's wealth.</summary>
        public FloatRange reward = new FloatRange(120f, 220f);
        /// <summary>Things of the colony's era worth this much, in the packs with a whole stay (Loot).</summary>
        public float gift;
        public int goodwill = 10;
        public int penalty = 4;
        /// <summary>Experience in the skill for each hand with a whole stay.</summary>
        public float xp = 1000f;
        public ThoughtDef thought;
        /// <summary>What their people remember of it, naming the first hand.</summary>
        public string deed = "lentHands";
        public List<MissionWay> forms;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (kind.NullOrEmpty()) yield return "hands mission has no kind";
            if (skill == null) yield return "hands mission has no skill";
            if (hands < 1 || hours < 1) yield return "hands mission needs hands and hours";
        }
    }

    /// <summary>
    /// A friendly town asks for some of the colony's people good at something for a few days: builders after a storm, a
    /// healer for their sickhouse, a teacher for their young, a witness at a truce, a smith before the harvest. The
    /// colony's caravan with enough of them reaches the town and waits there while they work; they come home with silver,
    /// a gift of the town's making, what they learned, and the town's memory of who came. Half the stay earns half.
    /// </summary>
    public class HappeningWorker_MissionHands : HappeningWorker_MissionJourney
    {
        private MissionHands H => def.GetModExtension<MissionHands>();

        protected override string Kind => H?.kind ?? def.defName;

        protected override float RestDays => H?.restDays ?? 60f;

        protected override int Rating => H?.rating ?? 2;

        protected override float MaxTravelDays => H?.maxTravelDays ?? 3f;

        /// <summary>One of the colony's people who can do what's asked.</summary>
        private bool Hand(Pawn p)
        {
            var h = H;
            return h?.skill != null && p != null && !p.Dead && p.skills?.GetSkill(h.skill) is SkillRecord r && !r.TotallyDisabled && r.Level >= h.minLevel
                   && (h.work == null || !p.WorkTypeIsDisabled(h.work));
        }

        protected override bool Possible(Map map)
        {
            var h = H;
            if (h?.skill == null || !MissionWay.Any(h.forms, map)) return false;
            return NeighboursFourMath.CanSpare(Colonists(map).Count(Hand), StorytellerEvents.Adults(map).Count(), h.hands) && Asker(map) != null;
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var h = H;
            var map = ctx.map;
            if (h == null) return false;
            // A people whose ways fit one of the request's (a lowtech town's ploughs, a hightech town's pumps).
            MissionWay way = null;
            for (int tries = 0; tries < 4; tries++)
            {
                m.faction = Asker(map);
                way = h.forms.NullOrEmpty() ? null : MissionWay.Pick(h.forms, map, m.faction);
                if (m.faction != null && (h.forms.NullOrEmpty() || way != null)) break;
                m.faction = null;
            }
            var town = MissionPeoples.Town(m.faction, map);
            if (m.faction == null || town == null) return false;
            m.variant = way?.tag;
            m.place = town.Name;
            m.a = Colonists(map).Where(Hand).OrderByDescending(p => p.skills.GetSkill(h.skill).Level).FirstOrDefault();
            if (m.a == null) return false;
            m.target = h.hands;
            float travel = MissionPeoples.TravelDays(map, town);
            m.tale = TextUtil.DaysString(Mathf.RoundToInt(Mathf.Max(0.3f, travel) * GenDate.TicksPerDay));
            m.days = NeighboursFourMath.HandsDays(travel, h.hours);
            m.reward = StoryChains.Silver(map, h.reward.min, h.reward.max);
            m.goodwill = h.goodwill;
            m.penalty = h.penalty;
            Fill(m, ctx);
            return true;
        }

        public override bool Start(Mission m, Map map) => MissionPeoples.TownOf(m) != null;

        public override void Check(Mission m, Map map, int now)
        {
            var h = H;
            if (h == null || TownGone(m)) return;
            var town = MissionPeoples.TownOf(m);
            var here = MissionPeoples.CaravansAt(town).OrderByDescending(c => MissionPeoples.Colonists(c).Count(Hand)).FirstOrDefault();
            int hands = here == null ? 0 : MissionPeoples.Colonists(here).Count(Hand);
            if (hands >= m.target)
            {
                if (m.stage == 0)
                {
                    m.stage = 1;
                    m.lastLook = now;
                    string line = Line(More?.ready, m, map, null, TextUtil.DaysString(h.hours * GenDate.TicksPerHour));
                    if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(here), MessageTypeDefOf.PositiveEvent);
                }
                else m.tally += Mathf.Max(0, now - Mathf.Max(m.lastLook, m.started));
            }
            m.lastLook = now;
            int need = h.hours * GenDate.TicksPerHour;
            float share = NeighboursFourMath.HandsShare(m.tally, need);
            if (share >= 1f && here != null)
            {
                Worked(m, here, 1f);
                Missions.Goodwill(m, m.goodwill);
                Missions.Finish(m, MissionState.Done, "worked", null, new LookTargets(here));
                return;
            }
            if (now < m.deadline) return;
            if (share > 0f && here != null)
            {
                Worked(m, here, share);
                Missions.Goodwill(m, m.goodwill / 2);
                Missions.Finish(m, MissionState.Done, "partial", null, new LookTargets(here), LetterDefOf.NeutralEvent);
                return;
            }
            Missions.Goodwill(m, -m.penalty);
            FactionMemory.Note(m.faction, "absent", m.place);
            Missions.Finish(m, MissionState.Failed, "missed", null);
        }

        /// <summary>The pay and the town's gift in the caravan's packs, what the hands learned, and the town's memory of who came.</summary>
        private void Worked(Mission m, Caravan c, float share)
        {
            var h = H;
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = NeighboursFourMath.SharePay(m.reward, share);
            CaravanInventoryUtility.GiveThing(c, silver);
            var home = m.Map;
            if (h.gift > 0f && share >= 1f)
                foreach (var t in Loot.Make(h.gift * (home != null ? LivingEvents.WealthFactor(home) : 1f)))
                    CaravanInventoryUtility.GiveThing(c, t);
            var hands = MissionPeoples.Colonists(c).Where(Hand).ToList();
            foreach (var p in hands)
            {
                p.skills?.Learn(h.skill, h.xp * share);
                if (h.thought != null) CouncilGameComponent.GiveThought(p, h.thought);
            }
            KinMemory.Note(m.faction, h.deed.NullOrEmpty() ? "lentHands" : h.deed, hands.OrderByDescending(p => p.skills.GetSkill(h.skill).Level).FirstOrDefault(), m.place,
                share < 1f ? 5 : 0);
        }

        public override string Status(Mission m)
        {
            var h = H;
            if (h == null) return null;
            if (m.stage == 0)
                return "CC_Mission_Hands_Go".Translate(m.target, h.skill.label, h.minLevel, m.place ?? "?", m.tale ?? "?").Resolve();
            int hours = Mathf.Min(h.hours, m.tally / GenDate.TicksPerHour);
            return "CC_Mission_Hands_There".Translate(m.place ?? "?", hours, h.hours).Resolve();
        }
    }

    // ===================== A store kept for them =====================

    /// <summary>One way a store can come: its tag, when, and the goods (a thing's defName, else <see cref="instead"/>) and how many.</summary>
    public class MissionKeepWay : MissionWay
    {
        public string what;
        public string instead;
        public FloatRange count = new FloatRange(100f, 200f);

        public ThingDef Def => DefDatabase<ThingDef>.GetNamedSilentFail(what ?? "") ?? DefDatabase<ThingDef>.GetNamedSilentFail(instead ?? "");
    }

    public class MissionKeep : DefModExtension
    {
        public string kind = "keep";
        public float restDays = 75f;
        public int rating = 1;
        public FloatRange days = new FloatRange(12f, 18f);
        public int goodwill = 10;
        public int penalty = 8;
        public List<MissionKeepWay> forms = new List<MissionKeepWay>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (forms.NullOrEmpty()) yield return "keep mission has no forms";
        }
    }

    /// <summary>
    /// A friendly people asks the colony to keep a store of theirs over a season (grain, timber, iron, cloth): their carts
    /// bring it to the gate, and when the time is up their carts come back for as much of it as they left, paying a
    /// quarter of its worth for the keeping. What the colony used meanwhile it pays for in silver at its plain worth;
    /// without the silver, they take what there is and remember it.
    /// </summary>
    public class HappeningWorker_MissionKeep : HappeningWorker_Mission
    {
        private MissionKeep K => def.GetModExtension<MissionKeep>();

        protected override string Kind => K?.kind ?? def.defName;

        protected override float RestDays => K?.restDays ?? 75f;

        protected override int Rating => K?.rating ?? 1;

        private List<MissionKeepWay> Ways(Map map, Faction f) => K?.forms?.Where(w => w != null && w.Def != null && w.Fits(map, f)).ToList() ?? new List<MissionKeepWay>();

        protected override bool Possible(Map map) => Neighbor(map) != null && Ways(map, null).Count > 0 && map.zoneManager.AllZones.Any(z => z is Zone_Stockpile);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var k = K;
            var map = ctx.map;
            if (k == null) return false;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            var way = Ways(map, m.faction).RandomElementByWeightWithFallback(w => Mathf.Max(0.01f, w.weight));
            if (way == null) return false;
            var d = way.Def;
            m.variant = way.tag;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = Interplay.MissionHead(map) ?? VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            int n = VarietyUtil.Round(Mathf.Max(10, Mathf.RoundToInt(way.count.RandomInRange)));
            m.goals.Add(new MissionGoal { what = d.defName, count = n });
            m.target = n;
            m.reward = NeighboursFourMath.KeepFee(n * d.BaseMarketValue);
            m.goodwill = k.goodwill;
            m.penalty = k.penalty;
            m.days = k.days.RandomInRange;
            Fill(m, ctx);
            return true;
        }

        private static ThingDef DefOf(Mission m) => m.goals.Count == 0 ? null : DefDatabase<ThingDef>.GetNamedSilentFail(m.goals[0].what);

        /// <summary>Taken on: their carts bring the store to the gate (Couriers/); else it is set down where traders leave goods.</summary>
        public override bool Start(Mission m, Map map)
        {
            var d = DefOf(m);
            if (d == null || m.goals.Count == 0) return false;
            var spot = DropCellFinder.TradeDropSpot(map);
            var things = Couriers.Make(d, m.goals[0].count);
            if (!Couriers.Deliver(map, things, m.faction, spot)) StorytellerEvents.Hand(d, m.goals[0].count, map, spot);
            m.goals[0].have = Missions.Count(map, d.defName);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var g = m.goals.FirstOrDefault();
            var d = DefOf(m);
            if (g == null || d == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            g.have = Missions.Count(map, g.what);
            // All of it in the stores: no reminder that it's short (Missions.Hourly reminds only at stage 0).
            m.stage = g.have >= g.count ? 1 : 0;
            if (now < m.deadline) return;
            var spot = DropCellFinder.TradeDropSpot(map);
            var look = new LookTargets(new TargetInfo(spot, map));
            int silver = Missions.Count(map, ThingDefOf.Silver.defName);
            int missing = Mathf.Max(0, g.count - g.have);
            int shortfall = NeighboursFourMath.Shortfall(missing, d.BaseMarketValue);
            switch (NeighboursFourMath.KeepOutcome(g.have, g.count, silver, shortfall))
            {
                case KeepEnd.Kept:
                    // Their carts come for it and pay the keeping on the spot; else it's taken and paid at once.
                    if (!Couriers.CollectGoals(map, new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(g.what, g.count) }, m.faction, m.reward))
                    {
                        Missions.Take(map, g.what, g.count);
                        VarietyUtil.PaySilver(map, spot, m.reward);
                    }
                    Missions.Goodwill(m, m.goodwill);
                    Missions.Finish(m, MissionState.Done, "kept", m.reward.ToString(), look);
                    return;
                case KeepEnd.MadeGood:
                {
                    // What is there, and silver for the rest at its plain worth.
                    var goals = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(ThingDefOf.Silver.defName, shortfall) };
                    if (g.have > 0) goals.Insert(0, new KeyValuePair<string, int>(g.what, g.have));
                    if (!Couriers.CollectGoals(map, goals, m.faction, 0))
                    {
                        Missions.Take(map, g.what, g.have);
                        Missions.Take(map, ThingDefOf.Silver.defName, shortfall);
                    }
                    Missions.Finish(m, MissionState.Done, "madegood", shortfall.ToString(), look, LetterDefOf.NeutralEvent);
                    return;
                }
                default:
                    if (g.have > 0 && !Couriers.CollectGoals(map, new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(g.what, g.have) }, m.faction, 0))
                        Missions.Take(map, g.what, g.have);
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "short", missing.ToString(), look);
                    return;
            }
        }

        public override string Status(Mission m)
        {
            var g = m.goals.FirstOrDefault();
            if (g == null) return null;
            return "CC_Mission_Keep_Status".Translate((m.faction?.Name ?? "?").Named("FACTION"), Missions.Need(g).Named("ITEM")).Resolve();
        }
    }

    // ===================== A young beast raised for them =====================

    /// <summary>One way a beast can come: its tag, when, and the kinds (PawnKindDef names: the first that suits the weather and the land).</summary>
    public class MissionBeastWay : MissionWay
    {
        public List<string> kinds = new List<string>();

        public PawnKindDef Kind(Map map)
        {
            var ok = kinds?.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n))
                .Where(k => k?.race?.race != null && k.RaceProps.Animal && !k.RaceProps.predator && (map == null || map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)))
                .ToList();
            if (ok.NullOrEmpty()) return null;
            var local = map?.Biome == null ? ok : ok.Where(k => map.Biome.AllWildAnimals.Contains(k) || map.Biome.IsPackAnimalAllowed(k.race)).ToList();
            return (local.Count > 0 ? local : ok).RandomElement();
        }
    }

    public class MissionBeast : DefModExtension
    {
        public string kind = "foster";
        public float restDays = 90f;
        public int rating = 2;
        public FloatRange days = new FloatRange(12f, 18f);
        public FloatRange reward = new FloatRange(180f, 320f);
        public int goodwill = 12;
        public int penalty = 8;
        public ThoughtDef thought;
        public List<MissionBeastWay> forms = new List<MissionBeastWay>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (forms.NullOrEmpty()) yield return "beast mission has no forms";
        }
    }

    /// <summary>
    /// A friendly people asks the colony to raise a young beast of theirs for a season (a foal, a calf, a pup for their
    /// herds): their drover walks it in to the gate and hands it over, and it is the colony's to feed and keep until the
    /// drover comes back for it and pays, all of it kept well, half of it kept poorly. Dead or lost, they remember it;
    /// bonded to one of the colony's people, they leave it where it is loved.
    /// </summary>
    public class HappeningWorker_MissionBeast : HappeningWorker_Mission
    {
        private MissionBeast B => def.GetModExtension<MissionBeast>();

        protected override string Kind => B?.kind ?? def.defName;

        protected override float RestDays => B?.restDays ?? 90f;

        protected override int Rating => B?.rating ?? 2;

        private static Pawn Keeper(Map map) => VarietyUtil.Best(map, SkillDefOf.Animals);

        protected override bool Possible(Map map) =>
            Neighbor(map) != null && Keeper(map) != null && (B?.forms?.Any(w => w != null && w.Fits(map, null) && w.Kind(map) != null) ?? false);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var b = B;
            var map = ctx.map;
            if (b == null) return false;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            var way = b.forms.Where(w => w != null && w.Fits(map, m.faction) && w.Kind(map) != null).RandomElementByWeightWithFallback(w => Mathf.Max(0.01f, w.weight));
            var kind = way?.Kind(map);
            var drover = Arrivals.PersonKind(m.faction) is PawnKindDef pk ? Arrivals.Make(pk, m.faction, new FloatRange(22f, 60f)) : null;
            if (kind == null || drover == null)
            {
                if (drover != null) Arrivals.Discard(drover);
                return false;
            }
            Pawn beast = null;
            try
            {
                var stages = kind.race.race.lifeStageAges;
                float young = stages != null && stages.Count > 1 ? stages[1].minAge : kind.RaceProps.lifeExpectancy * 0.04f;
                float grown = stages != null && stages.Count > 0 ? stages[stages.Count - 1].minAge : kind.RaceProps.lifeExpectancy * 0.15f;
                float age = Rand.Range(young, Mathf.Max(young, (young + grown) / 2f));
                beast = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, m.faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false, fixedBiologicalAge: age, fixedChronologicalAge: age));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mission beast", e);
            }
            if (beast == null)
            {
                Arrivals.Discard(drover);
                return false;
            }
            string name = More?.names.NullOrEmpty() ?? true ? kind.label : Flavor.PickText(More.names);
            beast.Name = new NameSingle(name);
            Arrivals.Hold(beast);
            Arrivals.Hold(drover);
            m.x = beast;
            m.group.Add(drover);
            m.name = name;
            m.variant = way.tag;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = Keeper(map);
            m.days = b.days.RandomInRange;
            m.wait = Rand.Range(0.3f, 0.6f);
            m.reward = StoryChains.Silver(map, b.reward.min, b.reward.max);
            m.goodwill = b.goodwill;
            m.penalty = b.penalty;
            Fill(m, ctx);
            ctx.thing = beast.def;
            return true;
        }

        private static Pawn Drover(Mission m) => m.group.FirstOrDefault(p => p != null && p.RaceProps.Humanlike);

        public override bool Start(Mission m, Map map)
        {
            if (m.x == null || m.x.Dead || m.x.Discarded || Drover(m) == null) return false;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(m.wait * GenDate.TicksPerDay);
            return true;
        }

        /// <summary>Well fed, not sick, no wound left untended; else why not.</summary>
        private static bool Well(Pawn x, out string why)
        {
            why = null;
            var hs = x?.health?.hediffSet;
            if (hs == null) return true;
            float thin = hs.GetFirstHediffOfDef(HediffDefOf.Malnutrition)?.Severity ?? 0f;
            bool sick = hs.hediffs.Any(h => h != null && h.Visible && (h.TryGetComp<HediffComp_Immunizable>() != null || h.def.makesSickThought));
            bool untended = x.health.HasHediffsNeedingTend();
            if (NeighboursFourMath.KeptWell(thin, sick, untended)) return true;
            why = (thin >= NeighboursFourMath.BeastThin ? "CC_Mission_Beast_Thin" : sick ? "CC_Mission_Beast_Sick" : "CC_Mission_Beast_Hurt").Translate().Resolve();
            return false;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            var drover = Drover(m);
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (x.Dead)
            {
                Missions.Goodwill(m, -m.penalty);
                FactionMemory.Note(m.faction, "letDown", m.name, 6);
                Missions.Finish(m, MissionState.Failed, "died", null, x.Corpse != null && x.Corpse.Spawned ? new LookTargets(x.Corpse) : LookTargets.Invalid);
                return;
            }
            if (m.stage == 0)
            {
                if (now < m.due) return;
                // Their drover walks it in from the road, the beast at their side, and makes for the gate.
                var gate = PlaySpots.GateCell(map, null);
                if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
                if (drover == null || drover.Dead || Arrivals.Visit(new List<Pawn> { drover, x }, map, m.faction, gate, 8f) == null)
                {
                    Missions.Finish(m, MissionState.Failed, "ended", null);
                    return;
                }
                m.stage = 1;
                m.due = now + GenDate.TicksPerHour * 6;
                return;
            }
            if (m.stage == 1)
            {
                if (!x.Spawned || x.Map != map)
                {
                    Missions.Finish(m, MissionState.Failed, "ended", null);
                    return;
                }
                // At the gate (or long enough on the way): the drover hands it over and goes back up the road.
                var flag = drover?.GetLord()?.CurLordToil?.FlagLoc ?? IntVec3.Invalid;
                bool there = drover != null && drover.Spawned && flag.IsValid && drover.Position.InHorDistOf(flag, 8f);
                if (!there && now < m.due && drover != null && drover.Spawned && !drover.Dead) return;
                x.GetLord()?.RemovePawn(x);
                x.SetFaction(Faction.OfPlayer);
                if (drover != null && drover.Spawned) Arrivals.SendAway(new[] { drover });
                m.stage = 2;
                string line = Line(More?.ready, m, map, null, TextUtil.DaysString(Mathf.Max(0, m.deadline - now)));
                if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(x), MessageTypeDefOf.PositiveEvent);
                return;
            }
            if (x.Faction != Faction.OfPlayer)
            {
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "lost", null);
                return;
            }
            if (now < m.deadline) return;
            if (!x.Spawned || x.Map != map)
            {
                // Off with a caravan: they wait a little, then count it lost.
                if (now < m.deadline + GenDate.TicksPerDay * 2) return;
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "lost", null);
                return;
            }
            // Bonded to one of the colony's people: their drover leaves it where it is loved, and takes nothing.
            if (HappeningWorker_MissionRunaway.Bonded(x))
            {
                Missions.Goodwill(m, m.goodwill);
                Missions.Finish(m, MissionState.Done, "bonded", null, new LookTargets(x));
                return;
            }
            bool well = Well(x, out _);
            int pay = NeighboursFourMath.SharePay(m.reward, NeighboursFourMath.BeastShare(well));
            m.reward = pay;
            if (B?.thought != null && m.a != null && !m.a.Dead && m.a.IsColonist && well) CouncilGameComponent.GiveThought(m.a, B.thought);
            // Their drover comes for it and pays once they have it (the runaway's way: Couriers/, else it walks off).
            HappeningWorker_MissionRunaway.HandBack(m, map, well ? "returned" : "poorly", m.goodwill * (well ? 1 : 0));
        }

        public override void Release(Mission m)
        {
            foreach (var p in m.group.Prepend(m.x).Where(p => p != null && !p.Dead))
            {
                if (!p.Spawned && p.Faction != Faction.OfPlayer && m.started < 0) Arrivals.Discard(p);
                else Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            }
        }

        public override string Status(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return null;
            if (m.stage < 2) return "CC_Mission_Beast_Coming".Translate((m.name ?? "?").Named("NAME"), (Drover(m)?.LabelShort ?? "?").Named("DROVER")).Resolve();
            return Well(x, out string why)
                ? "CC_Mission_Beast_Well".Translate((m.name ?? "?").Named("NAME")).Resolve()
                : "CC_Mission_Beast_Poorly".Translate((m.name ?? "?").Named("NAME"), why.Named("WHY")).Resolve();
        }

        public override LookTargets Targets(Mission m) => m.x != null && m.x.Spawned ? new LookTargets(m.x) : base.Targets(m);
    }

    // ===================== Someone of theirs lost on the land =====================

    /// <summary>Who is lost: how old, the tag for the texts, who of the colony deals with it, what is remembered.</summary>
    public class MissionLost : DefModExtension
    {
        public string kind;
        public float restDays = 90f;
        public int rating = 1;
        public FloatRange ages = new FloatRange(60f, 80f);
        public string variant;
        public SkillDef finder;
        public string deed = "foundLost";
        public string thought;
        public FloatRange days = new FloatRange(1.6f, 2.2f);
        public FloatRange reward = new FloatRange(120f, 240f);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (kind.NullOrEmpty()) yield return "lost mission has no kind";
        }
    }

    /// <summary>
    /// Someone of a friendly people lost somewhere on the colony's land (an old one wandered off, a hunter overdue), the
    /// lost child's way: found by whoever of the colony comes within a few steps in sight, they follow them back and
    /// wait for their kin, who come for them and pay.
    /// </summary>
    public class HappeningWorker_MissionLost : HappeningWorker_MissionLostChild
    {
        private MissionLost L => def.GetModExtension<MissionLost>();

        protected override string Kind => L?.kind ?? def.defName;

        protected override float RestDays => L?.restDays ?? 90f;

        protected override int Rating => L?.rating ?? 1;

        protected override FloatRange LostAges => L?.ages ?? new FloatRange(60f, 80f);

        protected override bool LostIsChild => false;

        protected override string LostVariant => L?.variant ?? "lost";

        protected override string FoundDeed => L?.deed ?? "foundLost";

        protected override string FoundThought => L?.thought ?? "Council_Mission_ChildHome";

        protected override SkillDef FinderSkill => L?.finder ?? SkillDefOf.Animals;

        protected override FloatRange SearchDays => L?.days ?? new FloatRange(1.6f, 2.2f);

        protected override FloatRange RewardRange => L?.reward ?? new FloatRange(120f, 240f);
    }

    // ===================== A traveller walked to their town =====================

    public class MissionEscortWho : DefModExtension
    {
        public string kind;
        public float restDays = 75f;
        public FloatRange ages = new FloatRange(30f, 60f);
        /// <summary>The tags the texts are written for (one picked): "trader", "healer", "scholar".</summary>
        public List<string> variants = new List<string>();
        public FloatRange reward = new FloatRange(160f, 300f);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (kind.NullOrEmpty()) yield return "escort mission has no kind";
        }
    }

    /// <summary>The pilgrim's way for another traveller of theirs (a trader past a bad road, a healer bound for a sickhouse, a scholar): the colony's caravan walks them to their town.</summary>
    public class HappeningWorker_MissionEscortWho : HappeningWorker_MissionEscort
    {
        private MissionEscortWho E => def.GetModExtension<MissionEscortWho>();

        protected override string Kind => E?.kind ?? def.defName;

        protected override float RestDays => E?.restDays ?? 75f;

        protected override FloatRange TravellerAges => E?.ages ?? new FloatRange(30f, 60f);

        protected override string TravellerVariant(Faction f) => E?.variants.NullOrEmpty() ?? true ? "trader" : E.variants.RandomElement();

        protected override string SetOffKey => "CC_Mission_EscortWho_Set";

        protected override FloatRange RewardRange => E?.reward ?? new FloatRange(160f, 300f);
    }
}
