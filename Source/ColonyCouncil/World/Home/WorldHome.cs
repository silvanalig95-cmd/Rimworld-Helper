using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The world brought home (the content update's integrations, part B: B1, B2, B3, B23, B25, B26 and B32 of
    /// docs/content-update-proposals.md; the player's go of 20 November 2026). The running world events reach into the
    /// colony's own life: the quests and storylines they make likelier name them (<see cref="WorldSteer"/>), the messengers
    /// tell the road's news (<see cref="RoadNews"/>), encounters and council questions come only in their season
    /// (<see cref="WorldSeason"/>), a plague's keeping apart (WorldQuestions.cs), and a devout colonist sets a great one down
    /// in the colony's book as a god's doing (<see cref="GreatTaken"/>). The peoples' own paragraphs in the world events'
    /// letters are WorldSituationWorker.Theirs; what the colony is known for drawing quests and camps is RenownMath. The pure
    /// rules are WorldHomeMath; what is saved is WorldHomeBook and the world events' own flags.
    /// </summary>
    public static class WorldHome
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The world events' hour (WorldSituations, at its own tick): a plague kept apart from visitors, the great ones taken for a god's doing, the vows kept.</summary>
        public static void Hourly(int now)
        {
            if (WorldSituations.Instance == null || S == null || !S.worldSituations) return;
            Safe("kept apart", () => WorldApart.Hour(now));
            Safe("the great events in the book", () => GreatTaken.Hour(now));
            Safe("the vows and colours", () => WorldHomeBook.Instance?.Hour(now));
        }

        internal static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the world brought home: " + what, e);
            }
        }
    }

    /// <summary>Which world events run now, and what their words call them (B25, B26: things that come only in their season).</summary>
    public static class WorldSeason
    {
        /// <summary>The world events running now.</summary>
        public static IEnumerable<WorldSituation> Running => WorldSituations.Instance?.Running ?? Enumerable.Empty<WorldSituation>();

        /// <summary>The kinds and names of the world events running now (sickness, hunger, war, Council_WS_HolyYear...).</summary>
        public static IEnumerable<string> RunningKeys => Running.Where(s => s?.def != null).SelectMany(s => s.def.kind.NullOrEmpty() ? new[] { s.def.defName } : new[] { s.def.kind, s.def.defName });

        /// <summary>One of these keys names this world event: its kind or its defName.</summary>
        private static bool Named(WorldSituation s, IList<string> keys) => s?.def != null && (keys.Contains(s.def.defName) || s.def.kind != null && keys.Contains(s.def.kind));

        /// <summary>The newest running world event of one of these kinds or names, or null (none listed: null).</summary>
        public static WorldSituation Of(IList<string> keys) =>
            keys == null || keys.Count == 0 ? null : Running.Where(s => Named(s, keys)).OrderByDescending(s => s.startTick).FirstOrDefault();

        /// <summary>Something that comes only while a world event of these kinds or names runs can come now (none listed: always).</summary>
        public static bool Fits(IList<string> keys) => WorldHomeMath.SeasonFits(keys, RunningKeys);

        /// <summary>What the words call a world event: "the gray cough", or its label.</summary>
        public static string NameOf(WorldSituation s) => s == null ? "" : Texts.WithThe(s.ctx?.name, s.def?.label ?? "");

        /// <summary>Where it is ({PLACE}): its town or place, else "the region".</summary>
        public static string PlaceOf(WorldSituation s) => !(s?.ctx?.placeName).NullOrEmpty() ? s.ctx.placeName : "CC_WH_TheRegion".Translate().Resolve();

        /// <summary>The tile it happens at (its town's, else its place's), or invalid.</summary>
        public static PlanetTile TileOf(WorldSituation s)
        {
            try
            {
                if (s?.place == null) return PlanetTile.Invalid;
                var town = Refs.Town(s.place);
                return town != null ? town.Tile : RiseFall.TileOf(s.place);
            }
            catch
            {
                return PlanetTile.Invalid;
            }
        }

        /// <summary>World tiles between two places on the same layer, or NaN.</summary>
        public static float Tiles(PlanetTile a, PlanetTile b)
        {
            try
            {
                if (!a.Valid || !b.Valid || a.Layer != b.Layer) return float.NaN;
                return Find.WorldGrid.ApproxDistanceInTiles(a, b);
            }
            catch
            {
                return float.NaN;
            }
        }

        /// <summary>{SITUATION}, {SITUATIONPLACE} and {SITUATIONPEOPLE} filled in a line written for a world event's season (before its own placeholders).</summary>
        public static string Fill(string line, WorldSituation s)
        {
            if (line.NullOrEmpty() || s == null) return line;
            return Texts.Fill(line, ("SITUATIONPLACE", PlaceOf(s)), ("SITUATIONPEOPLE", s.ctx?.faction?.Name ?? PlaceOf(s)), ("SITUATION", NameOf(s)));
        }

        /// <summary>The tags of a world event's season for a line: its kind and its name ([ws:sickness], [wsdef:Council_WS_Plague]).</summary>
        public static void AddTags(HashSet<string> tags, WorldSituation s)
        {
            if (tags == null || s?.def == null) return;
            if (!s.def.kind.NullOrEmpty()) tags.Add("ws:" + s.def.kind);
            tags.Add("wsdef:" + s.def.defName);
        }
    }

    // ===================== B2. World events steer the quests and storylines offered =====================

    /// <summary>
    /// While a world event runs, the home quests and storylines that fit it come more often: its <c>&lt;rimfolk&gt;</c>
    /// modifiers name their first parts (Defs/WorldSituationDefs/). Such a first part is "steered": its people are likelier
    /// the world event's own (the fever's town asks for the medicine, <see cref="Weight"/>), its first letter is picked by
    /// the world event's tags (ws:sickness, wsdef:Council_WS_Plague, ws:theirs when it is at the asker's own people) and,
    /// where a line is written for the season, names it: {WSNAME} the world event, {WSPLACE} where it is.
    /// </summary>
    public static class WorldSteer
    {
        /// <summary>The world event steering the event being cast right now (set around its Cast), or null.</summary>
        [ThreadStatic] private static WorldSituation casting;

        /// <summary>The running world event whose modifiers make this event likelier now (the asker's people's own first), or null.</summary>
        public static WorldSituation For(IncidentDef def, Faction people = null)
        {
            if (def == null || WorldSituations.Instance == null || !(ColonyCouncilMod.Settings?.worldSituations ?? false)) return null;
            WorldSituation best = null;
            foreach (var s in WorldSituations.Instance.Running)
            {
                if (s?.def?.modifiers == null) continue;
                bool raises = false;
                foreach (var m in s.def.modifiers)
                    if (!m.rimfolk.NullOrEmpty() && m.rimfolk == def.defName && m.Factor > 1f && m.Applies(s))
                    {
                        raises = true;
                        break;
                    }
                if (!raises) continue;
                if (people != null && s.ctx?.faction == people) return s;
                if (best == null || s.startTick > best.startTick) best = s;
            }
            return best;
        }

        /// <summary>The event is being cast now: while it is, its people lean toward the steering world event's own.</summary>
        public static Scope During(IncidentDef def) => new Scope(For(def));

        public readonly struct Scope : IDisposable
        {
            private readonly WorldSituation was;

            public Scope(WorldSituation s)
            {
                was = casting;
                casting = s;
            }

            public void Dispose() => casting = was;
        }

        /// <summary>A people's weight while a steered event is cast (Arrivals.Friendly, MissionPeoples.Grateful): the world event's own people likelier.</summary>
        public static float Weight(Faction f) => casting == null ? 1f : WorldHomeMath.SteerWeight(f != null && casting.ctx?.faction == f);

        /// <summary>The steered event's tags for its lines (IncidentWorker_Happening.TextTags).</summary>
        public static void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var s = ctx?.incident == null ? null : For(ctx.incident, ctx.People);
            if (s?.def == null || tags == null) return;
            foreach (var t in WorldHomeMath.SteerTags(s.def.kind, s.def.defName, ctx.People != null && s.ctx?.faction == ctx.People)) tags.Add(t);
        }

        /// <summary>{WSNAME} and {WSPLACE} for a steered event's lines (HappeningContext.AddArgs).</summary>
        public static void AddArgs(HappeningContext ctx, List<NamedArgument> args)
        {
            var s = ctx?.incident == null ? null : For(ctx.incident, ctx.People);
            if (s == null) return;
            args.Add(WorldSeason.NameOf(s).Named("WSNAME"));
            args.Add(WorldSeason.PlaceOf(s).Named("WSPLACE"));
        }

        /// <summary>
        /// The lines a steered event's first letter is picked from: those written for the world event's season that fit
        /// (a [ws:...] or [wsdef:...] tag among theirs), when there are any; else all of them.
        /// </summary>
        public static List<string> Prefer(List<string> lines, HashSet<string> context)
        {
            if (lines.NullOrEmpty() || context == null || !context.Contains("ws:steered")) return lines;
            List<string> season = null;
            foreach (var l in lines)
            {
                if (l.NullOrEmpty()) continue;
                Flavor.Strip(l, out var t);
                if (!WorldHomeMath.WorldTagged(t) || Flavor.Match(l, context) < 0) continue;
                (season ??= new List<string>()).Add(l);
            }
            return season ?? lines;
        }
    }

    // ===================== B3. Messengers tell the road's news =====================

    /// <summary>
    /// A messenger setting the bag down at the mailbag (a courier, the post rider, the mail run, a people's runner, a
    /// carrier) says a sentence about the road when a world event runs near it: at the colony, at their own town, or on the
    /// way between (WorldHomeMath.OnRoad). One sentence from Rimfolk_CourierLines, tagged [road] with the world event's
    /// kind ([ws:water]) or name ([wsdef:Council_WS_Flood]), and [ws:theirs] for one at their own people's town. {PLACE} is
    /// where it is, {NAME} what it is called, {FACTION} the people it is about.
    /// </summary>
    public static class RoadNews
    {
        private const string Pool = "Rimfolk_CourierLines";

        /// <summary>The road's news said over the messenger's head, if there is any to tell; false when they say their usual word instead.</summary>
        public static bool Say(Pawn messenger, Faction of, Map map, bool always = false)
        {
            if (messenger == null || !messenger.Spawned || map == null || !messenger.RaceProps.Humanlike) return false;
            try
            {
                var s = News(of, map);
                if (s == null || !always && !Rand.Chance(WorldHomeMath.RoadNewsChance)) return false;
                var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(Pool);
                if (pool?.lines == null) return false;
                var tags = Flavor.TagsFor(messenger);
                bool theirs = of != null && s.ctx?.faction == of;
                foreach (var t in WorldHomeMath.RoadTags(s.def.kind, s.def.defName, theirs)) tags.Add(t);
                tags.Add(of?.def != null && Peoples.TechOf(of) <= TechLevel.Medieval ? "theirLowtech" : "theirHightech");
                Peoples.AddTags(of, tags);
                var lines = pool.lines.For(ToneUtility.ToneFor(messenger)).Where(l => StorylineKit.HasTag(l, "road")).ToList();
                string line = Flavor.PickTagged(lines, tags) ?? Flavor.PickTagged(pool.lines.plain.Where(l => StorylineKit.HasTag(l, "road")), tags);
                if (line.NullOrEmpty()) return false;
                line = Texts.Fill(line, ("PLACE", WorldSeason.PlaceOf(s)), ("NAME", WorldSeason.NameOf(s)), ("FACTION", s.ctx?.faction?.Name ?? WorldSeason.PlaceOf(s)));
                return Speech.Say(messenger, WorldSituationWorker.Sentences(line));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the road's news", e);
                return false;
            }
        }

        /// <summary>The running world event on the messenger's road (theirs first, then the nearest), or null.</summary>
        public static WorldSituation News(Faction of, Map map)
        {
            var home = map?.Tile ?? PlanetTile.Invalid;
            var town = of != null ? WorldSituationWorker.NearestTown(of, map) : null;
            var townTile = town?.Tile ?? PlanetTile.Invalid;
            WorldSituation best = null;
            float bestTiles = float.MaxValue;
            foreach (var s in WorldSeason.Running)
            {
                if (s?.def == null || s.def.kind.NullOrEmpty()) continue;
                if (of != null && s.ctx?.faction == of) return s;
                var at = WorldSeason.TileOf(s);
                float h = WorldSeason.Tiles(home, at), t = WorldSeason.Tiles(townTile, at);
                if (!WorldHomeMath.OnRoad(h, float.IsNaN(t) ? h : t, WorldSeason.Tiles(home, townTile))) continue;
                if (h < bestTiles)
                {
                    best = s;
                    bestTiles = h;
                }
            }
            return best;
        }
    }

    // ===================== B26. Kept apart from travellers in a plague's season =====================

    /// <summary>
    /// The council kept the colony apart from travellers while a sickness runs (Rimfolk_Dlm_WS_Apart): the sickness at the
    /// gate and the couriers who arrive sick come half as often (Harm/Carriers.cs reads <see cref="Apart"/>), the colony's
    /// people have no moments with visitors and traders (Strangers/Encounters.cs), and each group of visitors or traders kept
    /// at arm's length thinks a little less of the colony (a point of goodwill, the game's message saying why). The world
    /// event's flag "apart" holds it; it ends with the world event.
    /// </summary>
    public static class WorldApart
    {
        public const string Flag = "apart";

        /// <summary>The colony keeps apart from travellers now (a running sickness flagged by the council's answer).</summary>
        public static bool Apart => WorldSeason.Running.Any(s => s.Has(Flag));

        private static readonly HashSet<int> seen = new HashSet<int>();

        public static void Reset() => seen.Clear();

        /// <summary>The hour: each group of visitors or traders at a home's gate, once, while the colony keeps apart.</summary>
        public static void Hour(int now)
        {
            if (!Apart) return;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || map.lordManager == null) continue;
                foreach (var lord in map.lordManager.lords)
                {
                    if (lord?.faction == null || lord.faction.IsPlayer || lord.faction.HostileTo(Faction.OfPlayer)) continue;
                    if (!(lord.LordJob is LordJob_VisitColony) && !(lord.LordJob is LordJob_TradeWithColony)) continue;
                    if (!seen.Add(lord.loadID)) continue;
                    if (seen.Count > 300) seen.Clear();
                    WorldSituationWorker.Goodwill(lord.faction, WorldHomeMath.ApartGoodwill, GoodwillWhy.Council);
                }
            }
        }
    }

    // ===================== B32. The region's great events in the colony's book =====================

    /// <summary>
    /// When a great world event comes (a plague, a comet, a bumper year, a great fire, a year without a summer, a famine, a
    /// flood, a drought, a hard winter, a murrain), a devout colonist may set it down in the colony's book as a god's doing,
    /// as SeasonGods does for a named season: the most devout of those who hold one of the gods their people tie to it
    /// (WorldHomeMath.GreatGods) says so over their head, and the book keeps it as their belief, under the name the colony
    /// knows the god by (an unknown god unnamed); another who takes it for another god says so beside them; one who believes
    /// none of it answers, and the book keeps the doubt. Only who believes it, never that it was so: the gods stay unseen.
    /// Once for each world event while it is new (its flag "wh:gods"). No harm.
    /// </summary>
    public static class GreatTaken
    {
        public const string Pool = "Rimfolk_Pool_WorldHome_Great";
        public const string Flag = "wh:gods";

        private static bool Able(Pawn p) => p != null && p.Spawned && !p.Dead && !p.Downed && !p.InMentalState && !p.Drafted && p.Awake() && Worship.Ours(p)
                                            && !p.DevelopmentalStage.Baby() && People.HasInnerLife(p);

        private static God Holds(Pawn p, List<God> gods) =>
            gods.Where(g => PawnGods.HoldOf(p, g) >= PawnGodsMath.LeansFrom && !PawnGods.Dreads(p, g)).OrderByDescending(g => PawnGods.HoldOf(p, g)).FirstOrDefault();

        public static void Hour(int now)
        {
            if (!Worship.On || GodLore.Instance == null) return;
            foreach (var s in WorldSeason.Running.ToList())
            {
                if (s?.def == null || WorldHomeMath.GreatGods(s.def.defName).Length == 0) continue;
                if (WorldHomeMath.GreatPast(s.Has(Flag), s.DaysIn))
                {
                    if (!s.Has(Flag)) s.Set(Flag);
                    continue;
                }
                var map = s.ctx?.map != null && Homes.IsHome(s.ctx.map) ? s.ctx.map : Homes.Seat;
                if (map == null || !map.mapPawns.FreeColonistsSpawned.Any(Able)) continue;
                s.Set(Flag);
                if (Rand.Chance(WorldHomeMath.GreatChance)) Take(s, map);
            }
        }

        /// <summary>The believers' word on a great world event at this home. Null when nobody here holds a god they'd take it for; else what was said.</summary>
        public static string Take(WorldSituation s, Map map)
        {
            var gods = WorldHomeMath.GreatGods(s?.def?.defName).Select(OwnHappenings.God).Where(g => g != null).ToList();
            if (gods.Count == 0 || map == null) return null;
            var people = map.mapPawns.FreeColonistsSpawned.Where(Able).ToList();
            var believers = people.Where(p => GodLore.Credence(p) >= GodLoreMath.BelieverFrom && Holds(p, gods) != null)
                .OrderByDescending(p => PawnGods.HoldOf(p, Holds(p, gods))).ToList();
            if (believers.Count == 0) return null;
            var first = believers[0];
            var g = Holds(first, gods);
            string what = What(s);
            var tags = new List<string> { "named" };
            if (!s.def.kind.NullOrEmpty()) tags.Add("ws:" + s.def.kind);
            tags.Add("wsdef:" + s.def.defName);
            var args = new[] { what.Named("WHAT"), WorldSeason.PlaceOf(s).Named("PLACE") };
            string line = Worship.LineWith(Pool, first, g, null, args, tags.ToArray());
            if (line.NullOrEmpty()) return null;
            // apart: a believer's word on news from the region everyone at home has heard, said to nobody in particular.
            Speech.Say(first, line);
            GodLore.Believes(first, g, Belief(what, g, first));
            string said = first.LabelShort + ": " + line;
            // Another who takes it for another god, beside them: they say so, and each holds their own the harder.
            var other = believers.Skip(1).FirstOrDefault(p => Together.Can(first, p) && Holds(p, gods) != g);
            if (other != null)
            {
                var og = Holds(other, gods);
                tags[0] = "other";
                string reply = Worship.LineWith(Pool, other, og, first, args.Concat(new[] { PawnGods.NameOf(g, other).Named("RIVAL") }).ToArray(), tags.ToArray());
                if (!reply.NullOrEmpty() && Speech.Say(other, reply, first))
                {
                    GodLore.Believes(other, og, Belief(what, og, other));
                    PawnGods.Move(first, g, BeliefEvent.Bickered);
                    PawnGods.Move(other, og, BeliefEvent.Bickered);
                    said += " / " + other.LabelShort + ": " + reply;
                }
            }
            // One who believes none of it answers.
            var doubter = people.Where(p => p != first && p != other && Together.Can(first, p) && GodLore.Credence(p) < GodLoreMath.ScofferBelow).OrderBy(GodLore.Credence).FirstOrDefault();
            if (doubter != null)
            {
                tags[0] = "scoff";
                string scoff = Worship.LineWith(Pool, doubter, g, first, args, tags.ToArray());
                if (!scoff.NullOrEmpty() && Speech.Say(doubter, scoff, first))
                {
                    GodLore.Disputed(doubter, g, scoff);
                    said += " / " + doubter.LabelShort + ": " + scoff;
                }
            }
            return said;
        }

        /// <summary>What the book calls the world event: "the gray cough at Ashford", "the bearded star" (a comet is over everyone, not at a town).</summary>
        private static string What(WorldSituation s) =>
            (s.ctx?.placeName).NullOrEmpty() || s.def.defName == "Council_WS_Comet" ? WorldSeason.NameOf(s)
                : "CC_WH_Great_What".Translate(WorldSeason.NameOf(s).Named("NAME"), s.ctx.placeName.Named("PLACE")).Resolve();

        /// <summary>The believer's words in the book, in the gods' book's old voice: whose doing they hold it to be.</summary>
        private static string Belief(string what, God g, Pawn p) =>
            ("CC_WH_Great_Belief_" + Rand.RangeInclusive(1, 3)).Translate(what.Named("WHAT"), Worship.Called(g, p).Named("GOD")).Resolve().CapitalizeFirst();
    }
}
