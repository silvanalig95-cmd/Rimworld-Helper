using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // S6, a dream of the gods (docs/quests-proposals.md; the rules in Flows/DreamFlow.cs, the words in
    // Defs/MissionDefs/Rimfolk_Storyline_Dream.xml). The colony's own faith, and the gods stay unseen (CLAUDE.md, "The gods
    // are unseen"): everything here is the dreamer's own, told as they would tell it, and the god is named only by the name
    // the colony knows it by (Worship.Called). No people of the world asked it, so its parts are the colony's own missions
    // (HappeningWorker_Mission.OfTheColony). Part 1: a devout colonist dreams of a place on the colony's ground (an area
    // marked on the map) and of a place for their god there: an altar, a shrine or a work of art (Gods/Worship/Shrines.cs)
    // built inside the area and given to the god, the worship's own dedication (someone walks over and says the words).
    // Part 2: the dreamer asks for a watch at the new place one night; whoever keeps it sees a light come over the place, the
    // god's sign (Worship.Sign: the presence's own moment, else a glow), named only as the colony may name it. Part 3: the
    // dream comes back and asks for an offering of a kind the god likes at the place within three days: the worship's own
    // demand of a dream (Gods/Worship/GodDreams.cs), carried by the dreamer first. Given, the colony's book of the gods keeps
    // it as the dreamer believes it (GodLore.Believes); refused or let go, the dreamer is troubled for a while. Never a
    // favour, a want or a rule shown.

    /// <summary>A dream of the gods beyond its parts: the dreamer, their god, how it ends for them.</summary>
    public class DreamRules : StorylineRules
    {
        public override string Kind => DreamFlow.Key;

        /// <summary>No people of the world: the dream is a colonist's own.</summary>
        public override Faction Giver(Map map) => null;

        public override bool NeedsGiver(Storyline s) => false;

        /// <summary>The god of the storyline (kept by its id), or null.</summary>
        public static God GodOf(Storyline s) =>
            s?.what != null && int.TryParse(s.what, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? Gods.ById(id) : null;

        /// <summary>
        /// Someone who could have the dream: one of the colony's own, devout and no scoffer, whose god the colony knows by name
        /// and takes offerings (the dream asks for one); the priest likelier.
        /// </summary>
        public static Pawn Dreamer(Map map)
        {
            if (map == null || !Worship.On) return null;
            return IncidentWorker_Happening.Colonists(map).Where(Fits).RandomElementByWeightWithFallback(p => DreamFlow.DreamerWeight(Worship.IsPriest(p), Worship.Psychic(p)));
        }

        private static bool Fits(Pawn p)
        {
            if (!Worship.Ours(p) || !p.DevelopmentalStage.Adult() || !Worship.Devout(p) || Worship.Scoffer(p) || p.Downed || p.InMentalState) return false;
            var g = Worship.GodOf(p);
            return g != null && g.kind != GodKind.Dead && Gods.Known(g) && GodWorshipDef.TakesOfferings(g) && !(GodWorshipDef.For(g)?.asksNothing ?? false)
                   && GodWorshipDef.Likes(g).Length > 0;
        }

        /// <summary>The dreamer is still the colony's: alive and one of its own.</summary>
        public static bool DreamerHere(Storyline s) => s?.x != null && !s.x.Dead && (s.x.IsFreeColonist || s.x.IsSlaveOfColony);

        /// <summary>The place built and given to the god, still standing and still the god's.</summary>
        public static Thing Place(Storyline s)
        {
            var t = s?.thing;
            if (t == null || t.Destroyed || !t.Spawned) return null;
            var g = GodOf(s);
            var r = Worship.Book?.ShrineOf(t);
            return g != null && r != null && !r.pending && r.god == g.id ? t : null;
        }

        /// <summary>The dreamer gone (dead, left the colony): it ends plainly.</summary>
        public override void Hourly(Storyline s, Map map, int now)
        {
            if (DreamerHere(s) && GodOf(s) != null) return;
            s.Set(GodOf(s) == null ? "godgone" : "dreamerlost");
            if (s.missionId >= 0)
            {
                var m = Missions.ById(s.missionId);
                if (m != null && m.Open) Missions.Finish(m, m.state == MissionState.Active ? MissionState.Failed : MissionState.Declined, "cancelled", null);
                return;
            }
            if (s.Running) Storylines.End(s, StorylineMath.Gone, map);
        }

        /// <summary>How it ends for the dreamer: honoured (and the book keeps it as they believe it), or troubled for a while.</summary>
        public override bool Ending(Storyline s, string key, Map map, HappeningContext ctx)
        {
            var p = DreamerHere(s) ? s.x : null;
            var g = GodOf(s);
            if (p == null) return true;
            if (DreamFlow.Honoured(key))
            {
                var t = StorytellerEvents.Thought("Rimfolk_Storyline_DreamHonoured");
                if (t != null) CouncilGameComponent.GiveThought(p, t);
                if (g != null)
                {
                    string words = "CC_Story3_Dream_Believes".Translate(Worship.Called(g, p).Named("GOD"), (s.tale ?? "").Named("KIND"), (s.xName ?? p.LabelShort).Named("PAWN")).Resolve();
                    GodLore.Believes(p, g, words);
                }
            }
            else if (DreamFlow.Troubled(key))
            {
                var t = StorytellerEvents.Thought("Rimfolk_Storyline_DreamTroubled");
                if (t != null) CouncilGameComponent.GiveThought(p, t);
            }
            return true;
        }

        /// <summary>The dream as the dreamer told it at breakfast: their journal, and the colony's book of the gods (a dream, as told).</summary>
        public static void Told(Storyline s, string key)
        {
            var p = DreamerHere(s) ? s.x : null;
            var g = GodOf(s);
            if (p == null || g == null) return;
            try
            {
                string words = key.Translate(Worship.Called(g, p).Named("GOD"), (s.tale ?? "").Named("KIND"), (s.way ?? "").Named("WAY")).Resolve();
                CouncilGameComponent.Instance?.journals?.WriteText(p, words);
                GodLore.Dreamt(p, g, words);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: a dream told", e);
            }
        }

        public override void DebugEnded(Storyline s, string part, string outcome)
        {
            // The place built, by hand: the colony's nearest place for the god stands in for it.
            if (part == "place" && outcome == "built" && Place(s) == null)
            {
                var g = GodOf(s);
                var map = s.Map;
                if (g != null && map != null) s.thing = Shrines.Of(map, g).Select(r => r.thing).FirstOrDefault();
            }
        }
    }

    /// <summary>What every part of the dream shares: the colony's own, the dreamer and their god in its words.</summary>
    public abstract class HappeningWorker_DreamPart : HappeningWorker_StorylinePart
    {
        public override bool OfTheColony => true;

        public override bool OwnMemory => true;

        /// <summary>The storyline's words: {GIVER} the dreamer, {NAME} the god as the colony knows it, {KIND} the place's word, {WAY} where it lies.</summary>
        protected static void Words(Storyline s, Map map)
        {
            var g = DreamRules.GodOf(s);
            if (g != null) s.name = Worship.Called(g, DreamRules.DreamerHere(s) ? s.x : null);
            s.tale = Worship.ShrineWord;
        }

        /// <summary>The part's own line with {ITEM} given (the offering's kind).</summary>
        protected string Line(List<string> lines, Storyline s, Mission m, Map map, string item, string days = null) =>
            Storylines.Line(lines, StorylineKit.Context(s, m, map), s, StorylineKit.Tags(s, m), item, days);
    }

    // ===================== Part 1: the place in the dream =====================

    /// <summary>
    /// A devout colonist dreams of a place on the colony's ground, well out from its middle, and of a place for their god
    /// there. The area is marked on the map; within six to nine days the colony builds an altar, a shrine or a work of art
    /// inside it, and once one stands there it is given to the god (the worship's own dedication: someone walks over and
    /// says the words). Built and given: done. Nothing given there in time: not built.
    /// </summary>
    public class HappeningWorker_DreamPlace : HappeningWorker_DreamPart, IStorylineArea, IStorylineDebugNow
    {
        protected override int Rating => 2;

        protected override bool Possible(Map map) => DreamRules.Dreamer(map) != null && Spot(map, out _);

        private static bool Spot(Map map, out IntVec3 cell) =>
            HomeQuests.FarCell(map, DreamFlow.PlaceFrom, c => !HomeQuests.NearEdge(map, c, (int)DreamFlow.AreaRadius + 6)
                                                               && StorylineRound3.AreaCells(map, c, DreamFlow.AreaRadius).Count(x => x.Standable(map)) >= 30, out cell);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var p = DreamRules.Dreamer(map);
            var g = Worship.GodOf(p);
            if (s == null || p == null || g == null || !Spot(map, out var cell)) return false;
            s.x = p;
            s.xName = p.LabelShort;
            s.what = g.id.ToString(CultureInfo.InvariantCulture);
            s.spot = cell;
            s.way = HomeQuests.Direction(map, cell);
            if (Worship.IsPriest(p)) s.Set("priest");
            Words(s, map);
            m.a = p;
            m.area = cell;
            m.days = Rand.Range(DreamFlow.PlaceMinDays, DreamFlow.PlaceMaxDays);
            Fill(m, ctx);
            ctx.a = p;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (!base.Begin(ctx, ext)) return false;
            DreamRules.Told(Story(Missions.Of(ctx)), "CC_Story3_Dream_Told");
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null || !s.spot.IsValid) return false;
            m.stage = 0;
            StorylineKit.Accepted(P, s, m, map, new LookTargets(new TargetInfo(s.spot, map)));
            return true;
        }

        public bool Marked(Mission m, Map map, out IntVec3 center, out float radius)
        {
            var s = Story(m);
            center = s?.spot ?? IntVec3.Invalid;
            radius = DreamFlow.AreaRadius;
            return s != null && center.IsValid && center.InBounds(map) && m.stage < 2;
        }

        /// <summary>A place for a god of the colony's own inside the area (an altar, a shrine, a work of art), the god's own first.</summary>
        private static Thing Built(Storyline s, Map map, God g)
        {
            if (!s.spot.IsValid) return null;
            var all = map.listerBuildings.allBuildingsColonist.Where(b => Shrines.Is(b) && b.Position.InHorDistOf(s.spot, DreamFlow.AreaRadius + 0.5f)).ToList();
            return all.FirstOrDefault(b => Shrines.GodAt(b) == g) ?? all.FirstOrDefault();
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            var g = DreamRules.GodOf(s);
            if (s == null || g == null || Worship.Book == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            var t = Built(s, map, g);
            if (t != null)
            {
                var r = Worship.Book.ShrineOf(t);
                if (r != null && !r.pending && r.god == g.id)
                {
                    s.thing = t;
                    m.thing = t;
                    m.stage = 2;
                    Say(DreamRules.DreamerHere(s) ? s.x : null, m, "built");
                    End(m, MissionState.Done, "built", new LookTargets(t), Find.ActiveLanguageWorker.WithIndefiniteArticle(t.LabelShort));
                    return;
                }
                // It stands: someone goes to give it to the god (asked once; asked again only if it was given to another).
                if (m.stage == 0 || r == null || r.god != g.id)
                {
                    m.stage = 1;
                    m.thing = t;
                    Shrines.Ask(t, g);
                    Note(m, map, "standing", new LookTargets(t));
                }
            }
            else if (m.stage == 1) m.stage = 0;
            if (now < m.deadline) return;
            End(m, MissionState.Failed, "notbuilt", new LookTargets(new TargetInfo(s.spot, map)));
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            if (m.thing != null && m.thing.Spawned) return new LookTargets(m.thing);
            return s != null && map != null && s.spot.IsValid ? new LookTargets(new TargetInfo(s.spot, map)) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            return m.stage == 1 && m.thing != null
                ? "CC_Story3_Dream_Standing".Translate(m.thing.LabelShort.Named("PLACE"), (s.name ?? "?").Named("GOD")).Resolve()
                : "CC_Story3_Dream_Nothing".Translate((s.name ?? "?").Named("GOD"), (s.way ?? "?").Named("WAY")).Resolve();
        }

        public string DebugNow(Mission m, Map map)
        {
            var s = Story(m);
            var g = DreamRules.GodOf(s);
            var t = s == null || g == null ? null : Built(s, map, g);
            if (t == null) return "build an altar, a shrine or a work of art inside the marked area first.";
            var r = Worship.Book?.ShrineOf(t);
            if (r == null || r.god != g.id) Shrines.Ask(t, g);
            r = Worship.Book?.ShrineOf(t);
            if (r != null) r.pending = false;
            Check(m, map, Find.TickManager.TicksGame);
            return "the place is given to the god now.";
        }
    }

    // ===================== Part 2: the watch at the place =====================

    /// <summary>
    /// The dreamer asks for a watch at the new place one night, within three nights: someone of the colony at the place for
    /// two hours of the night (the dreamer goes out each night to keep it; anyone of the colony there counts). Partway through
    /// it, a light comes over the place for a short while, the god's sign (Worship.Sign, named only as the colony may name it),
    /// seen by whoever keeps the watch. No watch kept: nothing is seen.
    /// </summary>
    public class HappeningWorker_DreamWatch : HappeningWorker_DreamPart, IStorylineDebugNow
    {
        protected override int Rating => 1;

        protected override bool Possible(Map map) => Storylines.current != null && DreamRules.DreamerHere(Storylines.current) && DreamRules.Place(Storylines.current) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            if (s == null || DreamRules.Place(s) == null) return false;
            Words(s, ctx.map);
            m.a = s.x;
            m.thing = s.thing;
            m.days = DreamFlow.WatchNights + 0.5f;
            Fill(m, ctx);
            ctx.a = s.x;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null || DreamRules.Place(s) == null) return false;
            m.stage = 0;
            m.tally = 0;
            m.lastLook = Find.TickManager.TicksGame;
            m.target = Mathf.RoundToInt(DreamFlow.SignAt(Rand.Value) * GenDate.TicksPerHour);
            m.misses = -1;
            StorylineKit.Accepted(P, s, m, map, new LookTargets(s.thing));
            return true;
        }

        /// <summary>Someone of the colony awake at the place now.</summary>
        private static Pawn Watcher(Thing place, Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && p.Awake() && p.Position.InHorDistOf(place.Position, DreamFlow.WatchWithin))
                .OrderBy(p => p.Position.DistanceToSquared(place.Position)).FirstOrDefault();

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            var place = DreamRules.Place(s);
            if (s == null || place == null || place.Map != map)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            int dt = Mathf.Max(0, now - m.lastLook);
            m.lastLook = now;
            int hour = GenLocalDate.HourOfDay(map);
            if (DreamFlow.Night(hour))
            {
                // The dreamer goes out once each night to keep it (their own walk out, the play-out's visit).
                int night = GenLocalDate.DayOfYear(map) + 60 * GenLocalDate.Year(map);
                if (m.misses != night && hour >= DreamFlow.NightFrom)
                {
                    m.misses = night;
                    SendDreamer(m, s, place, map);
                }
                var watcher = Watcher(place, map);
                if (watcher != null)
                {
                    m.tally += dt;
                    m.a = watcher;
                    if (m.tally >= m.target)
                    {
                        Seen(m, s, place, map, watcher);
                        return;
                    }
                }
            }
            if (now < m.deadline) return;
            Say(DreamRules.DreamerHere(s) ? s.x : null, m, "unwatched");
            End(m, MissionState.Failed, "unwatched", new LookTargets(place));
        }

        /// <summary>The dreamer walks out to the place and stays there for the watch (with answers not played out, someone has to be sent).</summary>
        private void SendDreamer(Mission m, Storyline s, Thing place, Map map)
        {
            var p = DreamRules.DreamerHere(s) ? s.x : null;
            if (!PlayOuts.On || !StorylineRound3.Free(p) || p.Map != map) return;
            var ctx = new HappeningContext { map = map, a = p, place = place, incident = def };
            new DilemmaEffect_Visit
            {
                who = PlayWho.A, to = PlaySpot.Place, duration = new FloatRange(DreamFlow.WatchHours + 0.5f, DreamFlow.WatchHours + 1f), hours = new IntRange(DreamFlow.NightFrom, 2)
            }.Begin(ctx);
        }

        /// <summary>The sign at the place, seen by the one keeping watch: the presence's moment (else a glow), and a card that names the god only as the colony may.</summary>
        private void Seen(Mission m, Storyline s, Thing place, Map map, Pawn watcher)
        {
            var g = DreamRules.GodOf(s);
            if (g == null)
            {
                End(m, MissionState.Failed, "ended");
                return;
            }
            string title = "CC_Story3_Dream_SignTitle".Translate((s.tale ?? "").Named("KIND")).Resolve();
            string text = "CC_Story3_Dream_Sign".Translate(watcher.Named("PAWN"), (s.tale ?? "").Named("KIND")).Resolve();
            bool plain = GodNearMath.Plain(GodNear.Of(g, map), Rand.Value);
            // The part's own letter tells what was seen; the sign itself is the presence's moment (else a glow and a line in the news).
            Worship.Sign(g, map, place.Position, PresenceKind.Act, title, text, new LookTargets(place), false, GodActKind.Act, plain, "dream");
            Say(watcher, m, "seen");
            s.Set("seen");
            End(m, MissionState.Done, "seen", new LookTargets(place), watcher.LabelShort);
        }

        public override LookTargets Targets(Mission m) => m.thing != null && m.thing.Spawned ? new LookTargets(m.thing) : LookTargets.Invalid;

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            float hours = m.tally / (float)GenDate.TicksPerHour;
            return "CC_Story3_Dream_Watch".Translate(hours.ToString("0.0").Named("HOURS"), DreamFlow.WatchHours.ToString("0").Named("NEED"), (s.tale ?? "").Named("KIND")).Resolve();
        }

        public string DebugNow(Mission m, Map map)
        {
            var s = Story(m);
            var place = DreamRules.Place(s);
            if (place == null) return "the place is gone.";
            var who = Watcher(place, map) ?? (DreamRules.DreamerHere(s) && s.x.Spawned ? s.x : map.mapPawns.FreeColonistsSpawned.FirstOrDefault());
            if (who == null) return "nobody to keep the watch.";
            Seen(m, s, place, map, who);
            return "the sign at the place, seen by " + who.LabelShort + ".";
        }
    }

    // ===================== Part 3: the offering =====================

    /// <summary>
    /// The dream comes back and asks for an offering of a kind the god likes at the place within three days: the worship's own
    /// demand of a dream (the offerings' carrying brings it, the dreamer first). Set down at the place: given. Not by the end
    /// of the days: not given. Turned down: refused. What the god does with it is the worship's own, never told here.
    /// </summary>
    public class HappeningWorker_DreamOffering : HappeningWorker_DreamPart, IStorylineDebugNow
    {
        protected override int Rating => 1;

        protected override bool Possible(Map map)
        {
            var s = Storylines.current;
            var g = DreamRules.GodOf(s);
            return s != null && g != null && DreamRules.DreamerHere(s) && DreamRules.Place(s) != null && GodWorshipDef.Likes(g).Length > 0;
        }

        private static string KindLabel(Mission m) => m?.variant == null ? "?" : ("CC_Wsh_Kind_" + m.variant).Translate().Resolve();

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var g = DreamRules.GodOf(s);
            if (s == null || g == null) return false;
            var likes = GodWorshipDef.Likes(g);
            if (likes.Length == 0) return false;
            Words(s, ctx.map);
            m.variant = likes[Rand.Range(0, likes.Length)].ToString();
            m.a = s.x;
            m.thing = s.thing;
            m.days = DreamFlow.OfferingDays;
            Fill(m, ctx);
            ctx.a = s.x;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (!base.Begin(ctx, ext)) return false;
            DreamRules.Told(Story(Missions.Of(ctx)), "CC_Story3_Dream_ToldAgain");
            return true;
        }

        protected override string Extra(HappeningContext ctx)
        {
            var m = Missions.Of(ctx) ?? Making;
            var s = Story(m);
            return m == null || s == null ? null : Line(P.terms, s, m, ctx.map, KindLabel(m), TextUtil.DaysString(Mathf.RoundToInt(m.days * GenDate.TicksPerDay)));
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            var g = DreamRules.GodOf(s);
            var place = DreamRules.Place(s);
            var b = Worship.Book;
            if (s == null || g == null || place == null || b == null || !Enum.TryParse(m.variant, out OfferingKind kind)) return false;
            int now = Find.TickManager.TicksGame;
            // The worship's own demand of a dream: the offerings' carrying brings one of the kind to the place, the dreamer first.
            b.demands.Add(new DemandRecord
            {
                god = g.id, shrine = place, kind = kind, dreamer = s.x, home = Gods.HomeKey(map), until = now + Mathf.RoundToInt(DreamFlow.OfferingDays * GenDate.TicksPerDay)
            });
            while (b.demands.Count > WorshipBook.MaxDemands) b.demands.RemoveAt(0);
            if (!b.offerings.Any(o => o != null && o.shrine == place && o.fate == OfferingFate.Waiting))
                b.offerings.Add(new OfferingRecord { shrine = place, god = g.id, home = Gods.HomeKey(map), chosen = now, demand = true });
            m.stage = 0;
            m.lastLook = now;
            string line = Line(P.accepted, s, m, map, KindLabel(m));
            // direct: the answer to the player's click taking the part on.
            if (!line.NullOrEmpty()) Messages.Message(line, new LookTargets(place), MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        /// <summary>An offering of the demand set down at the place since the part was taken on.</summary>
        private static bool Given(Mission m, Thing place) =>
            Worship.Book?.offerings.Any(o => o != null && o.shrine == place && o.demand && o.placed >= 0 && o.placed >= m.started) ?? false;

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            var place = DreamRules.Place(s);
            if (s == null || place == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (Given(m, place))
            {
                Say(DreamRules.DreamerHere(s) ? s.x : null, m, "given");
                End(m, MissionState.Done, "given", new LookTargets(place), KindLabel(m));
                return;
            }
            if (now < m.deadline) return;
            End(m, MissionState.Failed, "notgiven", new LookTargets(place), KindLabel(m));
        }

        public override LookTargets Targets(Mission m) => m.thing != null && m.thing.Spawned ? new LookTargets(m.thing) : LookTargets.Invalid;

        public override string Status(Mission m)
        {
            var s = Story(m);
            return s == null ? null : "CC_Story3_Dream_Offering".Translate(KindLabel(m).Named("WHAT"), (s.tale ?? "").Named("KIND")).Resolve();
        }

        public string DebugNow(Mission m, Map map)
        {
            var s = Story(m);
            var place = DreamRules.Place(s);
            if (place == null) return "the place is gone.";
            Say(DreamRules.DreamerHere(s) ? s.x : null, m, "given");
            End(m, MissionState.Done, "given", new LookTargets(place), KindLabel(m));
            return "the offering counted as given (the worship's own carrying skipped).";
        }
    }
}
