using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Something the colony can ask of one kind of camp, beyond terms, gifts and hiring (CampKindDef.asks): ask the
    /// minstrels to play at a feast, sit vigil with the mourners, have the healer teach the colony's doctor, buy a beast
    /// from the drovers. Offered on the camp's page while they're settled; costs silver when it says so; its effects are
    /// the dilemma and event effects (play-outs included: a gathering at the camp's fire, a lesson at the healer's side),
    /// with {X} whoever speaks for the camp, {A} the colonist who goes (best at <see cref="skill"/>, else the one warmest
    /// with the camp), {FACTION}, {PLACE} and {COUNT} (the silver).
    /// </summary>
    public class CampAsk
    {
        public string key;
        [MustTranslate] public string label;
        [MustTranslate] public string tip;
        /// <summary>What happened, told as a message (tagged like any line).</summary>
        [MustTranslate] public List<string> outcomes;
        /// <summary>Silver it costs, scaled by the colony's wealth (0: free).</summary>
        public int silver;
        /// <summary>Asked once per camp (else once a day).</summary>
        public bool once = true;
        /// <summary>Only after they've been settled this many days.</summary>
        public float minDays;
        /// <summary>Who goes: the colonist best at this.</summary>
        public SkillDef skill;
        /// <summary>Only when this kind of thing is true: "sick" (a colonist is ill or ailing), "hungry" (they're short of food), "evening" (16h to 22h).</summary>
        public string needs;
        /// <summary>The camp's tension afterwards (negative eases it), and how much warmer A is with them.</summary>
        public float tension;
        public int warm = 1;
        /// <summary>What their people remember of it (a FactionMemory deed kind), naming A.</summary>
        public string deed;
        public List<DilemmaEffect> effects = new List<DilemmaEffect>();
    }

    public partial class Camp
    {
        /// <summary>The asks already made of this camp (once each), and the day each was last made.</summary>
        public List<string> asksMade = new List<string>();

        public int AskCost(CampAsk a) => a == null || a.silver <= 0 ? 0 : CampAnswers.Round10(a.silver * LivingEvents.WealthFactor(map));

        /// <summary>Why this can't be asked now (null: it can).</summary>
        public string AskBlocked(CampAsk a)
        {
            if (a == null || !CanTalk) return "CC_CampAsk_NotNow".Translate();
            if (!Settled) return "CC_Camp_Dlg_NotYet".Translate();
            string mark = a.once ? a.key : a.key + "@" + GenDate.DaysPassed;
            if (asksMade.Contains(mark)) return a.once ? "CC_CampAsk_Done".Translate() : "CC_CampAsk_Today".Translate();
            if (a.minDays > 0f && DaysHere < a.minDays) return "CC_CampAsk_TooSoon".Translate();
            if (AskColonist(a) == null) return "CC_CampAsk_Nobody".Translate();
            switch (a.needs)
            {
                case "sick":
                    if (!IncidentWorker_Happening.Colonists(map).Any(p => LifeChecks.Illness(p) != null || HappeningWorker_Setting.Ailment(p) != null)) return "CC_CampAsk_NobodySick".Translate();
                    break;
                case "evening":
                    int h = GenLocalDate.HourOfDay(map);
                    if (h < 15 || h > 21) return "CC_CampAsk_Evening".Translate();
                    break;
            }
            int cost = AskCost(a);
            if (cost > 0 && CampAnswers.Silver(map) < cost) return "CC_Dilemma_NotEnough".Translate(cost, ThingDefOf.Silver.label);
            if (a.effects.NullOrEmpty()) return null;
            var ctx = AskContext(a);
            return a.effects.Select(e => e.Unavailable(ctx)).FirstOrDefault(r => r != null);
        }

        /// <summary>Who of the colony goes: the best at the ask's skill, else whoever is warmest with the camp, else anyone grown.</summary>
        public Pawn AskColonist(CampAsk a)
        {
            if (map == null) return null;
            var people = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState).ToList();
            if (people.Count == 0) return null;
            // Someone to be seen to: whoever is ill, else whoever carries an everyday ailment.
            if (a?.needs == "sick")
                return people.FirstOrDefault(p => LifeChecks.Illness(p) != null) ?? people.FirstOrDefault(p => HappeningWorker_Setting.Ailment(p) != null);
            if (a?.skill != null)
                return people.Where(p => p.skills?.GetSkill(a.skill) is SkillRecord r && !r.TotallyDisabled).OrderByDescending(p => p.skills.GetSkill(a.skill).Level).FirstOrDefault();
            return people.OrderByDescending(p => warmth.TryGetValue(p, out int w) ? w : 0).ThenBy(p => Rand.Value).FirstOrDefault();
        }

        private Pawn Speaker => leader != null && !leader.Dead && Here.Contains(leader) ? leader : Here.FirstOrDefault();

        private HappeningContext AskContext(CampAsk a) => new HappeningContext
        {
            map = map, a = AskColonist(a), faction = faction, stranger = Speaker, placeName = place, count = AskCost(a),
            group = Here.Where(p => p != Speaker).ToList()
        };

        /// <summary>Makes the ask: pays, applies its effects (those that play out first), tells what happened. Returns the line told.</summary>
        public string DoAsk(CampAsk a)
        {
            if (AskBlocked(a) != null) return null;
            int cost = AskCost(a);
            if (cost > 0 && !CampAnswers.Pay(map, cost)) return null;
            var ctx = AskContext(a);
            asksMade.Add(a.once ? a.key : a.key + "@" + GenDate.DaysPassed);
            string line = null;
            try
            {
                var tags = new HashSet<string>();
                Flavor.AddEraTags(tags);
                CampWords.AddTags(tags, kind, faction, map);
                LetterTags.Add(tags, ctx.a, null, ctx.stranger, faction, false);
                string raw = a.outcomes.NullOrEmpty() ? null : Flavor.Pick(a.outcomes, tags) ?? Flavor.PickText(a.outcomes);
                line = Format(raw, ctx.stranger, ctx.a, cost);
                ctx.why = a.label;
                ctx.cause = line;
                // What people do plays out on the map (a gathering at the camp's fire, a lesson, a visit), the rest at once.
                using (Causes.Push(line)) PlayOuts.Run(a.effects, ctx, "camp ask " + a.key);
                if (a.tension != 0f) AddTension(a.tension);
                if (a.warm > 0) Warm(ctx.a, a.warm);
                if (!a.deed.NullOrEmpty()) KinMemory.Note(faction, a.deed, ctx.a, place);
                if (cost > 0) gifts++;
                if (!line.NullOrEmpty())
                {
                    Messages.Message(line, new LookTargets(new[] { ctx.a, ctx.stranger }.Where(p => p != null && p.Spawned)), MessageTypeDefOf.PositiveEvent, false);
                    CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, line, ctx.a));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp ask " + a.key, e);
            }
            return line;
        }
    }

    /// <summary>A beast bought from drovers, or given: an animal of one of these kinds that suits the weather, tame and the colony's.</summary>
    public class DilemmaEffect_SpawnAnimal : DilemmaEffect
    {
        /// <summary>PawnKindDef names, the first that suits the weather and exists; "Muffalo|Cow".</summary>
        public List<string> kinds = new List<string>();
        public int count = 1;

        private PawnKindDef Pick(Map map) =>
            kinds?.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n))
                .Where(k => k?.race?.race != null && k.RaceProps.Animal && (map == null || map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)))
                .RandomElementWithFallback();

        public override string Unavailable(DilemmaContext ctx) => Pick(ctx.map) == null ? "CC_CampAsk_NoBeast".Translate().ToString() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx.map;
            var kind = Pick(map);
            if (map == null || kind == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var near = ctx.stranger != null && ctx.stranger.Spawned && ctx.stranger.Map == map ? ctx.stranger.Position
                : ctx.a != null && ctx.a.Spawned ? ctx.a.Position : DropCellFinder.TradeDropSpot(map);
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                try
                {
                    var animal = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfPlayer, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                        fixedBiologicalAge: kind.RaceProps.lifeExpectancy * Rand.Range(0.2f, 0.4f)));
                    if (animal == null) continue;
                    var cell = CellFinder.RandomClosewalkCellNear(near, map, 4);
                    GenSpawn.Spawn(animal, cell, map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp beast", e);
                    return;
                }
            }
        }
    }
}
