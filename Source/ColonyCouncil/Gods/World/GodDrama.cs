using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The drama between the gods, played out as world events (docs/design/gods.md, "Drama between the gods"; the nine's
    /// ties in docs/gods-pantheon-brainstorm.md, section B), never told as the gods':
    /// <list type="bullet">
    /// <item>Two gods tied to each other on opposed sides (GodWays.Side) quarrel where the colony can see it, for a dozen
    /// days, a turn each two days: Hush's cold against Smoulder's warmth over the high valleys (snow, then a warm clear day,
    /// the game's own weather where the land allows it), Murmur's rain against Hush's snow, Hollin's growth against Morel's
    /// rot (wild plants springing up near the colony, then a ring of mushrooms overnight), Helios's sun against Pip's spark
    /// (one solar flare of the game's own, then clear days; a battery filled overnight), Ducat's flow against Smoulder's
    /// hoard (a gold coin found where none was dropped; a little silver sunk out of a store). Nothing says so; someone who
    /// saw it may say whose turn they think it was.</item>
    /// <item>One a god's rival's people hunt comes to the gate and asks to be hidden, in their own words (they stay three
    /// days near the colony when hidden).</item>
    /// <item>Myths told at supper: one of the colony tells another, over the meal, a sentence of an old story of a god the
    /// colony knows, in that god's own words (godkey:...; the stories themselves are the core's).</item>
    /// </list>
    /// A rival undoing a gift is the gifts' (GodGifts); a feud between peoples that is really their gods' is the peoples'
    /// (GodPeoples, and the letters' holy wars).
    /// </summary>
    public static class GodDrama
    {
        private static int Now => GodWorld.Now;

        public static void Daily(GodWorldBook b, int now)
        {
            GodWorld.Safe("a quarrel's turn", () => Quarrels(b, now));
            GodWorld.Safe("a quarrel begins", () => Quarrel(b));
            GodWorld.Safe("one hunted at the gate", () => Ask(b));
            foreach (var d in b.Of("hidden").ToList())
                if (now > d.until)
                    GodWorld.Safe("the hidden one goes", () => HiddenGoes(d));
        }

        // ================= A quarrel in the weather =================

        internal static void Quarrel(GodWorldBook b)
        {
            if (b.Of("quarrel").Any() || !b.Gap("quarrel", GodWorldMath.QuarrelGap) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.QuarrelDays, GodWorld.Factor()))) return;
            var home = Homes.Seat;
            if (home == null) return;
            foreach (var a in GodWorld.Known.Where(g => GodWays.Sides(g).Count > 0).InRandomOrder().ToList())
            {
                // Two gods tied to each other on opposed sides: the cold and the warm, the storm and the dry sky, growth and rot...
                foreach (var other in Gods.TiesOf(a).Select(t => Gods.ById(t.Other(a.id))).Where(r => r != null && Gods.Known(r)).InRandomOrder())
                {
                    string mine = GodWays.Sides(a).FirstOrDefault(m => GodWays.Sides(other).Any(t => GodWorldMath.Opposed(m, t)));
                    if (mine == null) continue;
                    string theirs = GodWays.Sides(other).First(t => GodWorldMath.Opposed(mine, t));
                    b.Came("quarrel");
                    var d = b.Add(new GodDoing
                    {
                        kind = "quarrel", god = a.id, other = other.id, home = Homes.KeyOf(home), stage = -1, what = mine + "|" + theirs,
                        until = Now + GodWorldMath.QuarrelLastsDays * GodWorldMath.DayTicks
                    });
                    Turn(d, 0);
                    return;
                }
            }
        }

        private static void Quarrels(GodWorldBook b, int now)
        {
            foreach (var d in b.Of("quarrel").ToList())
            {
                var a = Gods.ById(d.god);
                var o = Gods.ById(d.other);
                var map = d.Map;
                if (a == null || o == null || map == null)
                {
                    d.done = true;
                    continue;
                }
                if (now > d.until)
                {
                    d.done = true;
                    continue;
                }
                int turn = GodWorldMath.QuarrelTurn((now - d.at) / GodWorldMath.DayTicks);
                if (turn != d.stage) Turn(d, turn);
            }
        }

        /// <summary>
        /// A quarrel's turn: the side whose turn it is shows itself, in the game's own weather of that side where the land
        /// allows it (rain and thunder, a clear dry sky, snow, a warm clear day), or on the ground near the colony (wild
        /// plants springing up, a ring of mushrooms overnight); someone who sees it says a word.
        /// </summary>
        private static void Turn(GodDoing d, int turn)
        {
            d.stage = turn;
            var map = d.Map;
            var a = Gods.ById(d.god);
            var o = Gods.ById(d.other);
            if (map == null || a == null || o == null) return;
            var god = turn == 0 ? a : o;
            var other = turn == 0 ? o : a;
            // The sides the quarrel began with (the first god's, then the other's).
            var sides = (d.what ?? "").Split('|');
            string side = sides.Length == 2 ? sides[turn == 0 ? 0 : 1] : GodWays.Side(god);
            if (side.NullOrEmpty()) return;
            var cell = GodWorldMath.InTheSky(side) ? Sky(map, side, d) : Ground(map, side);
            if (cell == null) return;
            GodWorld.Remember(god, map, GodActKind.Tie, ("CC_GW_Seen_" + ("quarrel" + side)).Translate(Homes.Name(map).Named("HOME")).Resolve(), other, cell.Value);
            GodWorld.Remark(map, cell.Value, "quarrel" + side, god, null, other.name.Named("OTHER"));
        }

        /// <summary>
        /// The game's own weather of a side of the sky, where the land has it: Invalid (the sky over all), or null when it
        /// couldn't turn. The sun's first turn is the game's own solar flare (once a quarrel), the rest clear days.
        /// </summary>
        private static IntVec3? Sky(Map map, string side, GodDoing d)
        {
            if (side == "sun" && d.count == 0)
            {
                var flare = DefDatabase<IncidentDef>.GetNamedSilentFail("SolarFlare");
                var parms = flare?.Worker != null ? StorytellerUtility.DefaultParmsNow(flare.category, map) : null;
                if (parms != null)
                {
                    parms.forced = true;
                    if (flare.Worker.CanFireNow(parms) && flare.Worker.TryExecute(parms))
                    {
                        d.count = 1;
                        return IntVec3.Invalid;
                    }
                }
            }
            var allowed = map.Biome?.baseWeatherCommonalities?.Where(w => w?.weather != null && w.commonality > 0f).Select(w => w.weather).ToList() ?? new List<WeatherDef>();
            var cur = map.weatherManager.curWeather;
            WeatherDef to = null;
            bool fits;
            switch (side)
            {
                case "storm":
                    to = allowed.Where(w => w.rainRate > 0f && w.eventMakers != null && w.eventMakers.Count > 0).RandomElementWithFallback() ?? allowed.Where(w => w.rainRate > 0f).RandomElementWithFallback();
                    fits = cur != null && cur.rainRate > 0f;
                    break;
                case "cold":
                    to = allowed.Where(w => w.snowRate > 0f).RandomElementWithFallback();
                    fits = cur != null && cur.snowRate > 0f;
                    break;
                case "rain":
                    to = GodHealth.Rainy(map);
                    fits = cur != null && cur.rainRate > 0f;
                    break;
                default:
                    to = allowed.FirstOrDefault(w => w == WeatherDefOf.Clear);
                    fits = cur == WeatherDefOf.Clear;
                    break;
            }
            if (fits || to == null) return null;
            try
            {
                map.weatherManager.TransitionTo(to);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a quarrel's weather", e);
                return null;
            }
            return IntVec3.Invalid;
        }

        /// <summary>A side of the ground near the colony: wild plants springing up (growth), a ring of mushrooms overnight (rot); where, or null.</summary>
        private static IntVec3? Ground(Map map, string side)
        {
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) return null;
            IntVec3 at;
            switch (side)
            {
                case "growth":
                    at = GodHealth.Sprout(map, centre, 4);
                    break;
                case "spark":
                    at = GodHealth.Mend(map, out _)?.Position ?? IntVec3.Invalid;
                    break;
                case "flow":
                    at = GodHealth.Coins(map, centre, GodWorldMath.CoinsFound(Rand.Value));
                    break;
                case "hoard":
                    at = GodHealth.Sink(map);
                    break;
                default:
                    at = GodHealth.FairyRing(map, centre);
                    break;
            }
            return at.IsValid ? at : (IntVec3?)null;
        }

        // ================= One hunted, at the gate =================

        internal static void Ask(GodWorldBook b)
        {
            if (!GodWorld.CanAsk || !b.Gap("ask", GodWorldMath.AskGap) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.AskDays, GodWorld.Factor()))) return;
            var home = Homes.Seat;
            if (home == null) return;
            foreach (var g in GodWorld.Near(home).InRandomOrder().ToList())
            {
                var r = Gods.Rivals(g).Where(Gods.Known).RandomElementWithFallback();
                if (r == null) continue;
                var hunters = r.factions.FirstOrDefault(f => f != null && !f.IsPlayer && !f.defeated && !f.Hidden && f.HasGoodwill);
                var own = g.factions.FirstOrDefault(f => Arrivals.Usable(f) && f != hunters);
                if (hunters == null || own == null) continue;
                if (Hide(b, home, g, r, own, hunters)) return;
            }
        }

        /// <summary>One the rival's people hunt comes to the gate and asks, in their own words, to be hidden (the god who sent them stays unnamed).</summary>
        private static bool Hide(GodWorldBook b, Map home, God g, God r, Faction own, Faction hunters)
        {
            var kind = Arrivals.PersonKind(own);
            if (kind == null) return false;
            var p = Arrivals.Make(kind, own, new FloatRange(18f, 50f));
            if (p == null) return false;
            if (Arrivals.Visit(new List<Pawn> { p }, home, own, null, 20f) == null)
            {
                Arrivals.Discard(p);
                return false;
            }
            var d = b.Add(new GodDoing { kind = "hide", god = g.id, other = r.id, home = Homes.KeyOf(home), pawn = p, faction = hunters, who = null, until = Now + GenDate.TicksPerDay });
            string text = GodWorld.T("CC_GW_Hide_Text", g, p.Named("PAWN"), hunters.Name.Named("HUNTERS"), own.Name.Named("PEOPLE"));
            if (GodAsks.Ask(GodAsks.Hide, home, g, GodWorld.T("CC_GW_Hide_Label", g, p.Named("PAWN")), text, new LookTargets(p), r, hunters, p, 0, null, d.id, 1f) == null)
            {
                d.done = true;
                Arrivals.SendAway(new[] { p });
                return false;
            }
            b.Came("ask");
            GodWorld.Remember(g, home, GodActKind.Act, GodWorld.T("CC_GW_Told_Hide", g, p.Named("PAWN"), hunters.Name.Named("HUNTERS"), own.Name.Named("PEOPLE")), r);
            return true;
        }

        public static IEnumerable<GodAnswer> HideAnswers(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            if (d == null || d.kind != "hide" || !PeoplesG.Up(d.pawn, d.Map)) yield break;
            yield return new GodAnswer("CC_GW_Hide_Yes".Translate(d.pawn.Named("PAWN")).Resolve(), Hidden);
            yield return new GodAnswer("CC_GW_Hide_No".Translate(d.pawn.Named("PAWN")).Resolve(), TurnedAway);
        }

        private static void Hidden(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            if (d == null) return;
            var map = d.Map;
            d.kind = "hidden";
            d.until = Now + GodWorldMath.HiddenDays * GodWorldMath.DayTicks;
            // They stay on: their visit's lord made long enough for the days they are hidden.
            try
            {
                d.pawn.GetLord()?.RemovePawn(d.pawn);
                var spot = CampSite.ColonyCenter(map);
                if (!spot.IsValid) spot = d.pawn.Position;
                Verse.AI.Group.LordMaker.MakeNewLord(d.pawn.Faction, new LordJob_VisitOnErrand(d.pawn.Faction, spot, GodWorldMath.HiddenDays * GenDate.TicksPerDay + GenDate.TicksPerHour),
                    map, new List<Pawn> { d.pawn });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: one hidden", e);
            }
            if (l.God != null) Gods.AddFavour(l.God, map, GodWorldMath.AskDone, GodWorld.T("CC_GW_Hide_WhyHidden", l.God, d.pawn.Named("PAWN")));
            if (l.Other != null) Gods.AddFavour(l.Other, map, GodWorldMath.AskRivalLoss, GodWorld.T("CC_GW_Hide_WhyHidden", l.Other, d.pawn.Named("PAWN")));
            GodWorld.Remark(map, d.pawn.Position, "hidden", l.God, d.pawn);
            if (d.faction != null && !d.faction.HostileTo(Faction.OfPlayer)) StoryChains.Goodwill(d.faction, GodWorldMath.AskRivalGoodwill);
            if (d.pawn.Faction != null) FactionMemory.Note(d.pawn.Faction, "sheltered", d.pawn.LabelShort);
        }

        private static void TurnedAway(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            if (d == null) return;
            d.done = true;
            if (l.God != null) Gods.AddFavour(l.God, d.Map, GodWorldMath.AskRefused, GodWorld.T("CC_GW_Hide_WhyTurned", l.God, d.pawn.Named("PAWN")), FavourSource.Deed, GodOffence.Stinginess);
            if (l.Other != null) Gods.AddFavour(l.Other, d.Map, -GodWorldMath.AskRefused * 0.5f, GodWorld.T("CC_GW_Hide_WhyTurned", l.Other, d.pawn.Named("PAWN")));
            Arrivals.SendAway(new[] { d.pawn });
        }

        private static void HiddenGoes(GodDoing d)
        {
            d.done = true;
            var map = d.Map;
            if (!PeoplesG.Up(d.pawn, map)) return;
            ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, map, "CC_GW_Hide_Goes".Translate(d.pawn.Named("PAWN")).Resolve(), new LookTargets(d.pawn),
                MessageTypeDefOf.PositiveEvent);
            if (d.pawn.Faction != null && !d.pawn.Faction.IsPlayer) StoryChains.Goodwill(d.pawn.Faction, 3);
            Arrivals.SendAway(new[] { d.pawn });
        }

        // ================= Myths at supper =================

        public const string MythPool = "Rimfolk_Pool_GodMyth";

        public static void Hourly(GodWorldBook b, int now)
        {
            foreach (var map in Homes.All.ToList())
            {
                if (map == null || !GodWorldMath.SupperHour(GenLocalDate.HourOfDay(map))) continue;
                string key = "myth:" + Homes.KeyOf(map);
                if (!b.Gap(key, GodWorldMath.MythGapDays)
                    || !GodWorld.Roll(GodWorldMath.MythEveningChance / 3f * GodPaceMath.Ramp(b.DaysSince(key), GodWorldMath.MythGapDays, GodWorldMath.MythRampDays))) continue;
                if (Supper(map)) b.Came(key);
            }
            foreach (var d in b.Of("undo").Concat(b.Of("hide")).Where(x => now > x.until).ToList())
            {
                d.done = true;
                if (PeoplesG.Up(d.pawn, d.Map)) Arrivals.SendAway(new[] { d.pawn });
            }
        }

        /// <summary>
        /// Two of the colony at their meal together: one tells the other a sentence of an old story of a god the colony knows
        /// (their own first), in the god's own words where its ways tag them (godkey:...), else its domain's; a god tied to it
        /// the colony knows may come into it ({OTHER}, lines tagged tied). The stories themselves are the Book of the Gods'.
        /// </summary>
        private static bool Supper(Map map)
        {
            var eating = map.mapPawns.FreeColonistsSpawned.Where(p => p.CurJobDef == JobDefOf.Ingest && p.Awake() && p.DevelopmentalStage.Adult()).ToList();
            foreach (var teller in eating.InRandomOrder())
            {
                var listener = eating.FirstOrDefault(x => x != teller && Together.Can(teller, x));
                if (listener == null) continue;
                var own = Gods.SwearBy(teller);
                var god = own != null && Gods.Known(own) && GodWorld.Roll(0.7f) ? own : GodWorld.Known.RandomElementWithFallback();
                if (god == null) return false;
                // Not the god of the last story told at this table, where the colony knows another.
                string home = Homes.KeyOf(map).ToString();
                var told = GodPace.Forms("myth", home);
                if (told.Count > 0 && told[0] == god.Key) god = GodWorld.Known.Where(x => x != god).RandomElementWithFallback() ?? god;
                var other = Gods.TiesOf(god).Select(t => Gods.ById(t.Other(god.id))).Where(x => x != null && Gods.Known(x)).RandomElementWithFallback();
                var tags = new List<string> { "god:" + god.domain.ToString().ToLowerInvariant() };
                if (other != null) tags.Add("tied");
                tags.AddRange(GodWays.Tags(god));
                string line = Flavor.FromPool(MythPool, teller, null, null, tags.ToArray());
                if (line.NullOrEmpty()) return false;
                var args = new List<NamedArgument>(Gods.Args(god)) { teller.Named("PAWN"), (other?.name ?? "").Named("OTHER") };
                string said = line.Formatted(args.ToArray()).Resolve().CapitalizeFirst();
                // together: the two are at their meal side by side (Together.Can above); the line is said to the listener.
                if (!Speech.Say(teller, said, listener)) return false;
                GodWorld.Remember(god, map, GodActKind.Act, "CC_GW_Told_Supper".Translate(teller.Named("PAWN"), listener.Named("LISTENER"), said.Named("WORDS")).Resolve(), other);
                GodPace.Note("myth", god.Key, home);
                return true;
            }
            return false;
        }
    }
}
