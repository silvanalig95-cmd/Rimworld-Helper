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
    /// The drama between the gods, played out as world events (docs/design/gods.md, "Drama between the gods"; the player:
    /// "there's drama between the gods"):
    /// <list type="bullet">
    /// <item>Two rival gods' quarrel seen in the weather: a god of the sky against one of the fields, the waters or the
    /// hearth, for a dozen days, a turn each two days: rain and thunder on the storm's days, a clear dry sky on the
    /// other's, the game's own weather where the land allows it. The colony may make an offering to one side, which ends it
    /// sooner in that god's favour.</item>
    /// <item>A god asks the colony to slight its rival (its word against the rival's people), or to hide one the rival's
    /// people hunt (they come to the gate and stay three days when hidden).</item>
    /// <item>Myths of the gods told at supper: one of the colony tells another, over the meal, a sentence of the old story
    /// of two gods the colony knows, and the story moves on as their ties' history grows (myth:0 to myth:2).</item>
    /// </list>
    /// A rival's avatar coming to undo a blessing is the gifts' (GodGifts); a feud between peoples that is really their
    /// gods' is the peoples' (GodPeoples, and the letters' holy wars).
    /// </summary>
    public static class GodDrama
    {
        private static int Now => GodWorld.Now;

        public static void Daily(GodWorldBook b, int now)
        {
            GodWorld.Safe("a quarrel's turn", () => Quarrels(b, now));
            GodWorld.Safe("a quarrel begins", () => Quarrel(b));
            GodWorld.Safe("a god's ask", () => Ask(b));
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
            foreach (var a in GodWorld.Known.Where(g => g.kind != GodKind.Dead && GodWays.Weather(g) == "storm").InRandomOrder().ToList())
            {
                var other = Gods.Rivals(a).FirstOrDefault(r => Gods.Known(r) && r.kind != GodKind.Dead && GodWays.Weather(r) == "dry");
                if (other == null) continue;
                b.Came("quarrel");
                var d = b.Add(new GodDoing
                {
                    kind = "quarrel", god = a.id, other = other.id, home = Homes.KeyOf(home), stage = -1, until = Now + GodWorldMath.QuarrelLastsDays * GodWorldMath.DayTicks
                });
                string text = GodWorld.Voice(GodWorld.T("CC_GW_Quarrel_Text", a, other.name.Named("OTHER"), (other.epithet ?? "").Named("OTHEREPITHET"),
                    GodWords.TieWord(Gods.TiesOf(a).FirstOrDefault(t => t.Has(other.id))?.kind ?? GodTieKind.Rivals).Named("TIE")));
                Gods.Remember(new GodAct { god = a.id, other = other.id, home = d.home, tick = Now, kind = GodActKind.Tie, text = GodWorld.T("CC_GW_Quarrel_Act", a, other.name.Named("OTHER")) });
                if (GodWorld.CanAsk)
                    GodAsks.Ask(GodAsks.Quarrel, home, a, GodWorld.T("CC_GW_Quarrel_Label", a, other.name.Named("OTHER")), text, null, other, null, null, 0, null, d.id, 3f);
                else GodWorld.Moment(a, home, PresenceKind.Omen, IntVec3.Invalid, null, GodWorld.T("CC_GW_Quarrel_Label", a, other.name.Named("OTHER")), text);
                Turn(d, 0);
                return;
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
                    ColonyCouncil.Tell.Minor(NewsKind.World, Importance.Flavor, map, GodWorld.Voice(GodWorld.T("CC_GW_Quarrel_Over", a, o.name.Named("OTHER"))), null);
                    continue;
                }
                int turn = GodWorldMath.QuarrelTurn((now - d.at) / GodWorldMath.DayTicks);
                if (turn != d.stage) Turn(d, turn);
            }
        }

        /// <summary>A quarrel's turn: the game's own weather of that side where the land allows it, a word in the news, and someone looks up.</summary>
        private static void Turn(GodDoing d, int turn)
        {
            d.stage = turn;
            var map = d.Map;
            var a = Gods.ById(d.god);
            var o = Gods.ById(d.other);
            if (map == null || a == null || o == null) return;
            var allowed = map.Biome?.baseWeatherCommonalities?.Where(w => w?.weather != null && w.commonality > 0f).Select(w => w.weather).ToList() ?? new List<WeatherDef>();
            WeatherDef to;
            if (turn == 0)
                to = allowed.Where(w => w.rainRate > 0f && w.eventMakers != null && w.eventMakers.Count > 0).RandomElementWithFallback() ?? allowed.Where(w => w.rainRate > 0f).RandomElementWithFallback();
            else to = allowed.FirstOrDefault(w => w == WeatherDefOf.Clear);
            var cur = map.weatherManager.curWeather;
            bool fits = turn == 0 ? cur != null && cur.rainRate > 0f : cur == WeatherDefOf.Clear;
            if (!fits && to != null)
            {
                try
                {
                    map.weatherManager.TransitionTo(to);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods in the world: a quarrel's weather", e);
                }
            }
            var side = turn == 0 ? a : o;
            ColonyCouncil.Tell.Minor(NewsKind.World, Importance.Flavor, map,
                GodWorld.Voice(GodWorld.T(turn == 0 ? "CC_GW_Quarrel_Storm" : "CC_GW_Quarrel_Dry", side, (turn == 0 ? o : a).name.Named("OTHER"))), null);
            GodWorld.Remark(map, IntVec3.Invalid, turn == 0 ? "quarrelstorm" : "quarreldry", side);
        }

        public static IEnumerable<GodAnswer> QuarrelAnswers(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            var a = l.God;
            var o = l.Other;
            if (d == null || a == null || o == null) yield break;
            yield return new GodAnswer("CC_GW_Quarrel_Side".Translate(a.name.Named("GOD")).Resolve(), x => Side(x, a, o));
            yield return new GodAnswer("CC_GW_Quarrel_Side".Translate(o.name.Named("GOD")).Resolve(), x => Side(x, o, a));
            yield return new GodAnswer("CC_GW_Quarrel_Out".Translate().Resolve(), x => { });
        }

        private static void Side(ChoiceLetter_GodWorld l, God chosen, God spurned)
        {
            var d = l.Doing;
            var map = l.HomeMap;
            if (d == null || map == null) return;
            Gods.AddFavour(chosen, map, GodWorldMath.SideTaken, GodWorld.T("CC_GW_Quarrel_WhySide", chosen, spurned.name.Named("OTHER")), FavourSource.Offering);
            Gods.AddFavour(spurned, map, GodWorldMath.SideSpurned, GodWorld.T("CC_GW_Quarrel_WhySpurned", spurned, chosen.name.Named("OTHER")), FavourSource.Spill, GodOffence.Rivals);
            // The chosen side has the last word: the quarrel ends after its next turn.
            d.until = Math.Min(d.until, Now + GodWorldMath.QuarrelTurnDays * GodWorldMath.DayTicks);
            GodWorld.Remark(map, IntVec3.Invalid, "side", chosen);
        }

        // ================= A god's ask: slight its rival, or hide one its rival hunts =================

        internal static void Ask(GodWorldBook b)
        {
            if (!GodWorld.CanAsk || !b.Gap("ask", GodWorldMath.AskGap) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.AskDays, GodWorld.Factor()))) return;
            var home = Homes.Seat;
            if (home == null) return;
            foreach (var g in GodWorld.Near(home).InRandomOrder().ToList())
            {
                var r = Gods.Rivals(g).Where(x => Gods.Known(x) && x.kind != GodKind.Dead).RandomElementWithFallback();
                if (r == null) continue;
                var rivalPeople = r.factions.FirstOrDefault(f => f != null && !f.IsPlayer && !f.defeated && !f.Hidden && f.HasGoodwill);
                var ownPeople = g.factions.FirstOrDefault(Arrivals.Usable);
                bool hide = ownPeople != null && Rand.Bool;
                if (hide && Hide(b, home, g, r, ownPeople, rivalPeople)) return;
                if (rivalPeople == null) continue;
                string text = GodWorld.Voice(GodWorld.T("CC_GW_Slight_Ask", g, r.name.Named("OTHER"), rivalPeople.Name.Named("PEOPLE")));
                if (GodAsks.Ask(GodAsks.Slight, home, g, GodWorld.T("CC_GW_Slight_AskLabel", g, r.name.Named("OTHER")), text, null, r, rivalPeople, null, 0, null, -1, 3f) != null)
                    b.Came("ask");
                return;
            }
        }

        public static IEnumerable<GodAnswer> SlightAnswers(ChoiceLetter_GodWorld l)
        {
            if (l.God == null || l.Other == null || l.people == null || l.people.defeated) yield break;
            yield return new GodAnswer("CC_GW_Ask_Do".Translate(l.God.name.Named("GOD")).Resolve(), Slighted);
            yield return new GodAnswer("CC_GW_Ask_Refuse".Translate().Resolve(), Refused);
        }

        private static void Slighted(ChoiceLetter_GodWorld l)
        {
            var map = l.HomeMap;
            Gods.AddFavour(l.God, map, GodWorldMath.AskDone, GodWorld.T("CC_GW_Slight_WhyDone", l.God, l.Other.name.Named("OTHER")));
            Gods.AddFavour(l.Other, map, GodWorldMath.AskRivalLoss, GodWorld.T("CC_GW_Slight_WhyRival", l.Other, l.God.name.Named("OTHER")), FavourSource.Deed, GodOffence.Scoffing);
            if (l.people != null && !l.people.HostileTo(Faction.OfPlayer))
            {
                StoryChains.Goodwill(l.people, GodWorldMath.AskRivalGoodwill);
                FactionMemory.Note(l.people, "slighted", l.Other.name);
            }
            ColonyCouncil.Tell.Minor(NewsKind.World, Importance.Notable, map, GodWorld.T("CC_GW_Slight_News", l.God, l.Other.name.Named("OTHER"), (l.people?.Name ?? "?").Named("PEOPLE")), null);
        }

        private static void Refused(ChoiceLetter_GodWorld l)
        {
            if (l.God == null) return;
            Gods.AddFavour(l.God, l.HomeMap, GodWorldMath.AskRefused, GodWorld.T("CC_GW_Ask_WhyRefused", l.God), FavourSource.Deed, GodOffence.BrokenWord);
        }

        /// <summary>One the rival's people hunt comes to the gate, sent by the god who asks the colony to hide them.</summary>
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
            string text = GodWorld.Voice(GodWorld.T("CC_GW_Hide_Text", g, p.Named("PAWN"), r.name.Named("OTHER"), (hunters?.Name ?? r.name).Named("HUNTERS"), own.Name.Named("PEOPLE")));
            if (GodAsks.Ask(GodAsks.Hide, home, g, GodWorld.T("CC_GW_Hide_Label", g, p.Named("PAWN")), text, new LookTargets(p), r, hunters, p, 0, null, d.id, 1f) == null)
            {
                d.done = true;
                Arrivals.SendAway(new[] { p });
                return false;
            }
            b.Came("ask");
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
            Gods.AddFavour(l.God, map, GodWorldMath.AskDone, GodWorld.T("CC_GW_Hide_WhyHidden", l.God, d.pawn.Named("PAWN")));
            if (l.Other != null) Gods.AddFavour(l.Other, map, GodWorldMath.AskRivalLoss, GodWorld.T("CC_GW_Hide_WhyRival", l.Other, d.pawn.Named("PAWN")));
            if (d.faction != null && !d.faction.HostileTo(Faction.OfPlayer)) StoryChains.Goodwill(d.faction, GodWorldMath.AskRivalGoodwill);
            if (d.pawn.Faction != null) FactionMemory.Note(d.pawn.Faction, "sheltered", d.pawn.LabelShort);
        }

        private static void TurnedAway(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            if (d == null) return;
            d.done = true;
            Gods.AddFavour(l.God, d.Map, GodWorldMath.AskRefused, GodWorld.T("CC_GW_Hide_WhyTurned", l.God, d.pawn.Named("PAWN")), FavourSource.Deed, GodOffence.Stinginess);
            if (l.Other != null) Gods.AddFavour(l.Other, d.Map, -GodWorldMath.AskRefused * 0.5f, GodWorld.T("CC_GW_Hide_WhyRivalPleased", l.Other, d.pawn.Named("PAWN")));
            Arrivals.SendAway(new[] { d.pawn });
        }

        private static void HiddenGoes(GodDoing d)
        {
            d.done = true;
            var map = d.Map;
            if (!PeoplesG.Up(d.pawn, map)) return;
            var g = Gods.ById(d.god);
            ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, map, GodWorld.T("CC_GW_Hide_Goes", g, d.pawn.Named("PAWN")), new LookTargets(d.pawn), MessageTypeDefOf.PositiveEvent);
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
                if (!b.Gap(key, GodWorldMath.MythGapDays) || !GodWorld.Roll(GodWorldMath.MythEveningChance / 3f)) continue;
                if (Supper(map)) b.Came(key);
            }
            foreach (var d in b.Of("undo").Concat(b.Of("hide")).Where(x => now > x.until).ToList())
            {
                d.done = true;
                if (PeoplesG.Up(d.pawn, d.Map)) Arrivals.SendAway(new[] { d.pawn });
            }
        }

        /// <summary>Two of the colony at their meal together: one tells the other a sentence of an old story of two gods the colony knows.</summary>
        private static bool Supper(Map map)
        {
            var eating = map.mapPawns.FreeColonistsSpawned.Where(p => p.CurJobDef == JobDefOf.Ingest && p.Awake() && p.DevelopmentalStage.Adult()).ToList();
            foreach (var teller in eating.InRandomOrder())
            {
                var listener = eating.FirstOrDefault(x => x != teller && Together.Can(teller, x));
                if (listener == null) continue;
                // A god of the teller's own with a tie to another the colony knows, else any two known gods tied.
                var own = Gods.SwearBy(teller);
                var pairs = (own != null ? new[] { own } : new God[0]).Concat(GodWorld.Known)
                    .SelectMany(g => Gods.TiesOf(g).Select(t => (g, t, other: Gods.ById(t.Other(g.id)))))
                    .Where(x => x.other != null && Gods.Known(x.other)).ToList();
                if (pairs.Count == 0) return false;
                var (a, tie, o) = pairs[0].g == own && GodWorld.Roll(0.7f) ? pairs[0] : pairs.RandomElement();
                int stage = GodWorldMath.MythStage(GodLetters.TieActs(a, o));
                var tags = new List<string> { "myth:" + tie.kind.ToString().ToLowerInvariant(), "myth:" + stage, "god:" + a.domain.ToString().ToLowerInvariant(),
                    "other:" + o.domain.ToString().ToLowerInvariant() };
                // A god's own myths first, where its ways name them (godkey:...).
                tags.AddRange(GodWays.Tags(a));
                string line = Flavor.FromPool(MythPool, teller, null, null, tags.ToArray());
                if (line.NullOrEmpty()) return false;
                string said = line.Formatted(teller.Named("PAWN"), a.name.Named("MYTHGOD"), o.name.Named("MYTHOTHER"), (a.epithet ?? "").Named("GODEPITHET")).Resolve().CapitalizeFirst();
                // together: the two are at their meal side by side (Together.Can above); the line is said to the listener.
                return Speech.Say(teller, said, listener);
            }
            return false;
        }
    }
}
