using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // A colonist a world event sends to a far town (the player's go of 20 November 2026: "go ahead with the next tasks").
    // Answers that send {A} to another people's town for a day or several (to advise, build, teach, read old writing, speak
    // or keep watch) used to leave them at the colony while the words sent them off. Now they really go, as the peoples'
    // events send a colonist away for a season (Peoples/Events2/SeasonAway.cs): a walk to the map's edge, a caravan of one
    // on the game's own roads, their days at the place (kept in the world there), and a caravan of one home again. What
    // their days there come to (learning, goodwill, a mood, a name, a wage) counts when they are home, and the letter then
    // says how it went. The rules are pure in FarErrandMath.cs.

    /// <summary>Where a world event sends its colonist.</summary>
    public enum ErrandPlace
    {
        /// <summary>The event's own town ({PLACE}).</summary>
        Town,
        /// <summary>A place on the land beside the colony (a river crossing, the hills), one tile out.</summary>
        Near
    }

    /// <summary>
    /// A world event's answer sends {A} to another people's town (or a place beside the colony) for <see cref="days"/> days,
    /// and they go: a caravan of one there and back (SeasonAway, kind "world"). The answer's own words tell it as set going;
    /// <see cref="effects"/> and the <see cref="wage"/> come when they are home after their days there, with the letter
    /// <see cref="home"/> (picked by the event's tags, and [risk] when the answer's gamble came true). Greyed while {A} can't
    /// leave (down, drafted, away, or one of the colony's last two grown hands) or the place is out of reach. While they are
    /// away, the event's letters that would fall to them wait, and the event doesn't end (WorldSituations, FarErrands).
    /// </summary>
    public class WorldEffect_Away : WorldEffect
    {
        /// <summary>Days at the place, the road both ways not counted.</summary>
        public FloatRange days = new FloatRange(1f, 1f);
        public ErrandPlace to = ErrandPlace.Town;
        /// <summary>With <see cref="to"/> Near: the land wanted beside the colony ("river", "hills"), else any open ground.</summary>
        public string near;
        /// <summary>With <see cref="to"/> Near: what the words call the place ("the river crossing below the colony").</summary>
        [MustTranslate] public string placeLabel;
        /// <summary>What their days there come to, applied when they are home after them (learning, goodwill, a mood, a name).</summary>
        public List<DilemmaEffect> effects = new List<DilemmaEffect>();
        /// <summary>The event's {COUNT} in silver, paid for their days there and set down at their feet as they come in.</summary>
        public bool wage;
        /// <summary>How it went, in the letter when they are home; none: a message that they are back.</summary>
        [MustTranslate] public List<string> home;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var a = ctx?.a;
            if (a == null || !Allowed(ctx)) return;
            var map = SituationEffectUtil.MapOf(ctx);
            if (!Place(ctx, out var tile, out string name, out _)) return;
            float road = SeasonAway.RoadDays(map, tile);
            int away = FarErrandMath.AwayDaysShown(road, days.Average);
            sheet.Add(ImpactKind.People, -1, "CC_PEv2_Impact_Away".Translate(a.LabelShort, TextUtil.DaysString(away * GenDate.TicksPerDay), name).Resolve(),
                "CC_PEv2_Impact_AwayBrief".Translate().Resolve(), "wsaway");
            sheet.Add(ImpactKind.Health, -1, "CC_PEv2_Impact_Road".Translate(a.LabelShort).Resolve(), "CC_PEv2_Impact_RoadBrief".Translate().Resolve(), "pev2road");
            var town = tile.Valid ? Find.WorldObjects.SettlementAt(tile) : null;
            if (Harm.On && town != null && SickHome.TownSickness(town) != SickTown.None)
                sheet.Add(ImpactKind.Health, -1, "CC_WS_Away_Impact_Sick".Translate(a.LabelShort, name).Resolve(), "CC_WS_Away_Impact_SickBrief".Translate().Resolve(), "wsawaysick");
            // What their days there come to, once they are home.
            foreach (var e in effects)
            {
                if (e == null) continue;
                try
                {
                    e.Describe(ctx, sheet);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("answer impact " + e.GetType().Name, ex);
                }
            }
            if (wage && ctx.count > 0) sheet.Silver(ctx.count);
        }

        /// <summary>Where they go and what the words call it; false when there is no such place now.</summary>
        public bool Place(DilemmaContext ctx, out PlanetTile tile, out string name, out Settlement town)
        {
            tile = PlanetTile.Invalid;
            name = null;
            town = null;
            var map = SituationEffectUtil.MapOf(ctx);
            if (map == null) return false;
            try
            {
                if (to == ErrandPlace.Near)
                {
                    tile = FarErrands.NearTile(map, near);
                    name = ctx.Format(placeLabel ?? "");
                }
                else
                {
                    var s = SituationEffectUtil.Of(ctx);
                    if (s != null)
                    {
                        town = Refs.Town(s.place);
                        if (town == null && s.place.kind == RefKind.Tile) tile = RiseFall.TileOf(s.place);
                    }
                    if (town != null && town.Destroyed) town = null;
                    if (town == null && !tile.Valid) town = WorldSituationWorker.NearestTown(ctx.faction, map);
                    if (town != null) tile = town.Tile;
                    name = (ctx as HappeningContext)?.placeName ?? town?.Name;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world event errand: the place", e);
                return false;
            }
            if (name.NullOrEmpty()) name = "CC_WS_Away_ThePlace".Translate().Resolve();
            return tile.Valid && tile.Layer == map.Tile.Layer && tile != map.Tile;
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var a = ctx?.a;
            var map = SituationEffectUtil.MapOf(ctx);
            if (map == null || SeasonAway.OutJob == null) return null;
            if (a == null || a.Dead) return "CC_WS_Away_Nobody".Translate().Resolve();
            if (!PeopleEvents2.MayLeave(a, map))
            {
                bool self = !a.Spawned || a.Map != map || a.Downed || a.Drafted || a.InMentalState || a.DevelopmentalStage.Juvenile() || a.GetCaravan() != null || SeasonAway.AwayNow(a);
                return (self ? "CC_PEv2_CantLeave" : "CC_WS_Away_FewHands").Translate(a.Named("PAWN")).Resolve();
            }
            if (!Place(ctx, out var tile, out string name, out _) || !Find.WorldReachability.CanReach(map.Tile, tile))
                return "CC_WS_Away_NoRoad".Translate(name.Named("PLACE")).Resolve();
            float road = SeasonAway.RoadDays(map, tile);
            if (!FarErrandMath.InReach(road))
                return "CC_WS_Away_TooFar".Translate(name.Named("PLACE"), TextUtil.DaysString(Mathf.RoundToInt(road * GenDate.TicksPerDay)).Named("DAYS")).Resolve();
            if (!RCellFinder.TryFindBestExitSpot(a, out _, TraverseMode.ByPawn)) return "CC_WS_Away_NoWayOut".Translate(a.Named("PAWN")).Resolve();
            return null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!Gate(ctx)) return;
            var map = SituationEffectUtil.MapOf(ctx);
            var s = SituationEffectUtil.Of(ctx);
            try
            {
                if (map != null && ctx.a != null && Unavailable(ctx) == null && Place(ctx, out var tile, out string name, out _) && FarErrands.Locate(s?.def, this, out var note))
                {
                    note.situation = s.id;
                    note.risk = FarErrands.AnswerRisk;
                    note.count = ctx.count;
                    if (SeasonAway.Send(FarErrandMath.Key, null, ctx.a, null, ctx.faction, tile, name, Mathf.Max(0f, days.RandomInRange), map, FarErrandMath.Write(note)) != null) return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world event errand: setting out", e);
            }
            // They couldn't be set going after all: what their days there would have brought comes now, as it always did.
            foreach (var e in effects) HappeningTracker.Safe(e, ctx);
            if (wage && ctx.count > 0) Couriers.PaySilver(map, IntVec3.Invalid, ctx.count, ctx.faction);
        }

        /// <summary>The letter's words when they are home: picked by the event's tags (and [risk]), filled in.</summary>
        public string HomeText(WorldSituation s, SituationContext ctx, bool risk)
        {
            if (home.NullOrEmpty()) return null;
            var tags = s?.Worker != null ? s.Worker.Tags(s) : new HashSet<string>();
            if (s == null) Flavor.AddEraTags(tags);
            if (risk) tags.Add(FarErrands.RiskTag);
            string line = Flavor.Pick(home, tags) ?? Flavor.PickText(home);
            return line.NullOrEmpty() ? null : WorldSituationWorker.Sentences(ctx.Format(line));
        }
    }

    /// <summary>The world event's errand as the season away runs it: there, staying on for a question, and home.</summary>
    public class FarErrandAway : AwayKind
    {
        public override string Key => FarErrandMath.Key;

        public override void Arrived(Visit v, Map home, Faction f, string place)
        {
            var p = v.x;
            Tell.Message(NewsKind.World, Importance.Notable, home, "CC_WS_Away_There".Translate(p.Named("PAWN"), place.Named("PLACE")).Resolve(), LookTargets.Invalid,
                MessageTypeDefOf.NeutralEvent);
            // Lodged at a town where sickness is going round (a plague or a famine of the region's): the illness budget's own
            // roll when they are home (Harm/SickHome.cs), as for a caravan stopped there.
            var tile = SeasonAway.TileOf(v);
            var town = tile.Valid ? Find.WorldObjects.SettlementAt(tile) : null;
            var kind = town != null ? SickHome.TownSickness(town) : SickTown.None;
            if (kind != SickTown.None) SickHome.Lodged(p, town, kind, PeopleEvents10Math.LodgedHours(SeasonAway.DaysOf(v)), Peoples.TendsLodgers(f));
        }

        public override bool Stays(Visit v, int now) => FarErrandMath.StaysOn(FarErrands.Asked(v), now, v.due);

        public override void Home(Visit v, Map home, Faction f, bool served) => FarErrands.Home(v, home, f, served);
    }

    /// <summary>The world events' errands: which is under way, what the events' letters do meanwhile, and the homecoming.</summary>
    public static class FarErrands
    {
        /// <summary>The tag a homecoming's words are picked by when the answer's gamble came true.</summary>
        public const string RiskTag = "risk";

        /// <summary>The answer being given now: whether its gamble came true (set by WorldSituations.Resolve around its effects).</summary>
        public static bool AnswerRisk;

        /// <summary>The event's number (WorldSituation.numbers) that marks it waited past its end for the colonist it sent.</summary>
        public const string HeldKey = "farErrandHeld";

        private static List<Visit> Visits => CouncilGameComponent.Instance?.happenings?.visits;

        /// <summary>One of the world events' errands, and its note.</summary>
        public static bool Note(Visit v, out FarErrandMath.Note n)
        {
            n = default;
            return v != null && v.kind == SeasonAway.Kind && SeasonAway.KeyOf(v) == FarErrandMath.Key && FarErrandMath.Read(SeasonAway.ExtraOf(v), out n);
        }

        /// <summary>The errand of this world event under way now (on the road there, there, or on the road home), or null.</summary>
        public static Visit Of(WorldSituation s) =>
            s == null ? null : Visits?.FirstOrDefault(v => v != null && !v.done && Note(v, out var n) && n.situation == s.id);

        public static bool Out(WorldSituation s) => Of(s) != null;

        /// <summary>This colonist is away on a world event's errand now.</summary>
        public static bool Away(Pawn p) => p != null && (Visits?.Any(v => v != null && !v.done && v.x == p && Note(v, out _)) ?? false);

        /// <summary>Where in its world event this effect sits (the beat, the answer, which list and where in it); false when it isn't in one.</summary>
        public static bool Locate(WorldSituationDef def, WorldEffect_Away e, out FarErrandMath.Note n)
        {
            n = default;
            if (def?.beats == null || e == null) return false;
            foreach (var b in def.beats)
            {
                if (b?.options == null) continue;
                for (int i = 0; i < b.options.Count; i++)
                {
                    var o = b.options[i];
                    int at = o.effects?.IndexOf(e) ?? -1;
                    bool risk = false;
                    if (at < 0)
                    {
                        at = o.riskEffects?.IndexOf(e) ?? -1;
                        risk = at >= 0;
                    }
                    if (at < 0) continue;
                    n = new FarErrandMath.Note { def = def.defName, beat = b.key, option = i, riskList = risk, index = at };
                    return true;
                }
            }
            return false;
        }

        /// <summary>The answer's errand effect a note names, or null (the def changed since).</summary>
        public static WorldEffect_Away Effect(FarErrandMath.Note n)
        {
            var def = DefDatabase<WorldSituationDef>.GetNamedSilentFail(n.def ?? "");
            var b = def?.Beat(n.beat);
            if (b?.options == null || n.option >= b.options.Count) return null;
            var o = b.options[n.option];
            var list = n.riskList ? o.riskEffects : o.effects;
            return list != null && n.index < list.Count ? list[n.index] as WorldEffect_Away : null;
        }

        /// <summary>
        /// What one of the event's beats does while its errand runs (FarErrandMath.Turn): one written from the far town comes
        /// on the colonist's last day there; one that would fall to the colonist away waits for them.
        /// </summary>
        public static FarErrandMath.BeatTurn Turn(WorldSituation s, SituationBeat beat, Map map, int now)
        {
            var v = Of(s);
            if (v == null && !beat.atPlace) return FarErrandMath.BeatTurn.Go;
            bool falls = v != null && FallsToAway(v.x, beat, map);
            return FarErrandMath.Turn(falls, beat.atPlace, v != null, v != null && v.stake == PeopleEvents2Math.There, v == null ? 0 : v.due - now);
        }

        /// <summary>
        /// The beat would be put to the colonist away: the event's own person (a beat with no colonist or skill of its own), or
        /// the colony's best at its skill when nobody at home is as good.
        /// </summary>
        private static bool FallsToAway(Pawn away, SituationBeat beat, Map map)
        {
            if (beat.colonist != null) return false;
            if (beat.skill == null) return true;
            var r = away?.skills?.GetSkill(beat.skill);
            if (r == null || r.TotallyDisabled) return false;
            int best = -1;
            if (map != null)
                foreach (var p in IncidentWorker_Happening.Colonists(map))
                    if (p != away && p.skills?.GetSkill(beat.skill) is SkillRecord o && !o.TotallyDisabled)
                        best = Mathf.Max(best, o.Level);
            return FarErrandMath.BestIsAway(r.Level, best, beat.minLevel);
        }

        /// <summary>A question for them at the far town is still to come, or not yet answered: they stay on for it.</summary>
        public static bool Asked(Visit v)
        {
            if (!Note(v, out var n)) return false;
            var s = WorldSituations.Instance?.Get(n.situation);
            if (s == null || s.ended || s.def == null) return false;
            if (s.beats.Any(b => s.def.Beat(b.key)?.atPlace == true)) return true;
            return Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Situation>().Any(l => !l.resolved && l.ctx?.situationId == s.id && l.Beat?.atPlace == true);
        }

        /// <summary>
        /// Home again: after their days there, what those days came to (the answer's effects, the wage set down at their
        /// feet) and the letter that says how it went; cut short, a message that they are back early.
        /// </summary>
        public static void Home(Visit v, Map map, Faction f, bool served)
        {
            var p = v.x;
            if (p == null || map == null) return;
            Note(v, out var n);
            var e = Effect(n);
            var s = WorldSituations.Instance?.Get(n.situation);
            if (s != null && s.ended) s = null;
            string place = SeasonAway.PlaceOf(v);
            if (!served || e == null)
            {
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_WS_Away_HomeEarly".Translate(p.Named("PAWN"), place.Named("PLACE")).Resolve(),
                    p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }
            var ctx = s != null ? s.ctx.Copy() : new SituationContext { situationId = -1, faction = f, placeName = place };
            ctx.a = p;
            ctx.map = map;
            ctx.count = n.count;
            var def = DefDatabase<WorldSituationDef>.GetNamedSilentFail(n.def ?? "");
            ctx.cause = "CC_WS_Cause".Translate(def != null ? def.LabelCap.ToString() : place).Resolve();
            using (Causes.Push(ctx.cause))
                foreach (var x in e.effects)
                    HappeningTracker.Safe(x, ctx);
            if (e.wage && n.count > 0 && p.Spawned)
                foreach (var t in Couriers.Make(ThingDefOf.Silver, n.count))
                    GenPlace.TryPlaceThing(t, p.Position, p.Map, ThingPlaceMode.Near);
            string text = null;
            try
            {
                text = e.HomeText(s, ctx, n.risk);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("world event errand: the homecoming's words", ex);
            }
            var look = p.Spawned ? new LookTargets(p) : LookTargets.Invalid;
            if (text.NullOrEmpty())
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_WS_Away_Home".Translate(p.Named("PAWN"), place.Named("PLACE")).Resolve(), look, MessageTypeDefOf.PositiveEvent);
            else
                Tell.Letter(NewsKind.World, Importance.Notable, map, "CC_WS_Away_HomeLabel".Translate(p.LabelShort, place).Resolve(), text, WorldSituationDefOf.Council_WorldNews, look, f);
            // Clear the letters' own copy of the people, as an answer's does, so the save doesn't hold them twice.
            ctx.a = ctx.b = ctx.stranger = ctx.other = null;
            ctx.map = null;
        }

        /// <summary>
        /// A tile beside the colony for a place one tile out: one with the land wanted ("river", "hills") first, then open
        /// ground with nothing on it, the lowest tile first so the same place is told each time.
        /// </summary>
        public static PlanetTile NearTile(Map map, string want)
        {
            if (map == null) return PlanetTile.Invalid;
            var around = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(map.Tile, around);
            var best = PlanetTile.Invalid;
            int bestScore = int.MinValue;
            foreach (var t in around.Where(t => t.Valid).OrderBy(t => t.tileId))
            {
                if (Find.World.Impassable(t)) continue;
                int score = 0;
                var tile = Find.WorldGrid[t];
                if (want == "river" && tile is SurfaceTile st && !st.Rivers.NullOrEmpty()) score += 4;
                if (want == "hills" && tile != null && tile.hilliness >= Hilliness.SmallHills && tile.hilliness <= Hilliness.Mountainous) score += 4;
                if (!Find.WorldObjects.AnyWorldObjectAt(t)) score += 1;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            return best;
        }
    }
}
