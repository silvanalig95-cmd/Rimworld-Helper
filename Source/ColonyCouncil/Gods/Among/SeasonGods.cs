using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Named seasons taken for a god's doing (C18 of docs/content-update-proposals.md). When the weather book names a season
    /// far from the land's usual ("the winter of the long cold", WeatherBook.NameText), the colony's believers say whose doing
    /// they take it for, by the gods their people tie to it (GodsAmongMath.GodsFor: Hush for a long cold, Helios or Smoulder
    /// for a hot summer, Murmur for a wet one, Cirrus for a stormy one, Helios or Murmur for a dry one), each the god they
    /// hold of those:
    /// <list type="bullet">
    /// <item>The most devout of them says it over their head, and the book keeps it as their belief under that god's name.</item>
    /// <item>Another who takes it for another god says so to their face when the two are together; both hold their own the
    /// harder for it (the argument over a sign), and the book keeps both.</item>
    /// <item>One who believes none of it answers, and the book keeps the doubt.</item>
    /// <item>A neighbour of a people that holds that god may write of it (the hands kept for letters, GodTales.Letter).</item>
    /// <item>When the same season comes round the year after, the council may be asked to keep a day against it
    /// (Rimfolk_Dlm_SeasonDay, a council question in the flavour style), as the customs are asked.</item>
    /// </list>
    /// Nothing says it was the god's doing: only who believes it. No harm.
    /// </summary>
    public static class SeasonGods
    {
        public const string Pool = "Rimfolk_Pool_GodsAmong_Season";
        public const string Question = "Rimfolk_Dlm_SeasonDay";

        private static bool Able(Pawn p) => p != null && p.Spawned && !p.Dead && !p.Downed && !p.InMentalState && !p.Drafted && p.Awake() && Worship.Ours(p)
                                            && !p.DevelopmentalStage.Baby() && People.HasInnerLife(p);

        /// <summary>The god of these a colonist holds most (leaning toward it at least, not in dread of it), or null.</summary>
        private static God Holds(Pawn p, List<God> gods) =>
            gods.Where(g => PawnGods.HoldOf(p, g) >= PawnGodsMath.LeansFrom && !PawnGods.Dreads(p, g)).OrderByDescending(g => PawnGods.HoldOf(p, g)).FirstOrDefault();

        /// <summary>A season ended and was named (WeatherBook.EndQuarter): the believers say whose doing they take it for.</summary>
        public static bool Named(Map map, WeatherQuarter q, string named)
        {
            var b = GodsAmongBook.Book;
            if (b == null || !Worship.On || map == null || q?.name == null || named.NullOrEmpty() || !Homes.IsHome(map)) return false;
            var gods = GodsAmongMath.GodsFor(q.name, WeatherMath.SeasonWord(q.season)).Select(OwnHappenings.God).Where(g => g != null).ToList();
            if (gods.Count == 0) return false;
            var people = map.mapPawns.FreeColonistsSpawned.Where(Able).ToList();
            var believers = people.Where(p => GodLore.Credence(p) >= GodLoreMath.BelieverFrom && Holds(p, gods) != null)
                .OrderByDescending(p => PawnGods.HoldOf(p, Holds(p, gods))).ToList();
            if (believers.Count == 0) return false;
            var first = believers[0];
            var g = Holds(first, gods);
            var seasonArg = named.Named("SEASON");
            string line = Worship.LineWith(Pool, first, g, null, new[] { seasonArg }, "named");
            if (line.NullOrEmpty()) return false;
            // apart: a believer's word on a season everyone at home lived through, said to nobody in particular.
            Speech.Say(first, line);
            GodLore.Believes(first, g, "CC_GA_Season_Belief".Translate(named.Named("SEASON"), Worship.Called(g, first).Named("GOD")).Resolve());
            // Another who takes it for another god, beside them: they say so, and each holds their own the harder.
            var other = believers.Skip(1).FirstOrDefault(p => Together.Can(first, p) && GodsAmongMath.Argue(g.Key, Holds(p, gods)?.Key));
            if (other != null)
            {
                var og = Holds(other, gods);
                string reply = Worship.LineWith(Pool, other, og, first, new[] { seasonArg, PawnGods.NameOf(g, other).Named("RIVAL") }, "other");
                if (!reply.NullOrEmpty() && Speech.Say(other, reply, first))
                {
                    GodLore.Believes(other, og, "CC_GA_Season_Belief".Translate(named.Named("SEASON"), Worship.Called(og, other).Named("GOD")).Resolve());
                    PawnGods.Move(first, g, BeliefEvent.Bickered);
                    PawnGods.Move(other, og, BeliefEvent.Bickered);
                }
            }
            // One who believes none of it answers.
            var doubter = people.Where(p => p != first && p != other && Together.Can(first, p) && GodLore.Credence(p) < GodLoreMath.ScofferBelow).OrderBy(GodLore.Credence).FirstOrDefault();
            if (doubter != null)
            {
                string scoff = Worship.LineWith(Pool, doubter, g, first, new[] { seasonArg }, "scoff");
                if (!scoff.NullOrEmpty() && Speech.Say(doubter, scoff, first)) GodLore.Disputed(doubter, g, scoff);
            }
            // Kept for a neighbour's letter, and for the same season next year.
            var lore = GodLore.Instance;
            if (lore != null)
            {
                lore.hands.Add(new LoreHand { god = g.id, tick = GodsAmongBook.Now, home = Homes.KeyOf(map), what = named });
                while (lore.hands.Count > 8) lore.hands.RemoveAt(0);
            }
            b.seasons.Add(new SeasonTaken { home = map, year = q.year, season = q.quadrum, kind = q.name, text = named, god = g.id, believer = first.LabelShort });
            while (b.seasons.Count > GodsAmongMath.MaxSeasons) b.seasons.RemoveAt(0);
            return true;
        }

        /// <summary>The named season of the year before that has come round again at this home, not yet asked, or null.</summary>
        public static SeasonTaken ComingRound(Map map)
        {
            var b = GodsAmongBook.Book;
            if (b == null || map == null) return null;
            int year = GenLocalDate.Year(map), doy = GenLocalDate.DayOfYear(map);
            int quadrum = doy / WeatherMath.QuarterDays, inQuadrum = doy % WeatherMath.QuarterDays;
            return b.seasons.LastOrDefault(s => s != null && !s.asked && s.home == map && Gods.ById(s.god) != null && GodsAmongMath.ComesRound(s.year, s.season, year, quadrum, inQuadrum));
        }

        /// <summary>The gods' day: a named season of last year come round again brings its council question (once).</summary>
        internal static void Daily(GodsAmongBook b, int now)
        {
            if (!(ColonyCouncilMod.Settings?.enableDilemmas ?? false)) return;
            foreach (var map in Homes.All.ToList())
            {
                var s = ComingRound(map);
                if (s == null) continue;
                var comp = CouncilGameComponent.Instance;
                var tracker = DilemmaTracker.For(map) ?? comp?.dilemmas;
                if (tracker != null && tracker.TryFire(true, d => d.defName == Question)) s.asked = true;
            }
        }

        /// <summary>Dev: the believers' word on a season named now (the land's last season, as if it had been named so).</summary>
        public static string DevNamed(Map map, string kind)
        {
            var q = new WeatherQuarter { year = GenLocalDate.Year(map), quadrum = GenLocalDate.DayOfYear(map) / WeatherMath.QuarterDays, season = (int)GenLocalDate.Season(map), name = kind };
            string named = WeatherBook.NameText(q);
            return Named(map, q, named) ? "said of " + named + "." : "nobody here holds a god they would take " + named + " for.";
        }

        /// <summary>Dev: last season's named season comes round now, and the council is asked.</summary>
        public static string DevAsk(Map map)
        {
            var b = GodsAmongBook.Book;
            var s = b?.seasons.LastOrDefault(x => x?.home == map);
            if (s == null) return "no named season taken for a god's doing at this home yet.";
            s.year = GenLocalDate.Year(map) - 1;
            s.season = GenLocalDate.DayOfYear(map) / WeatherMath.QuarterDays;
            s.asked = false;
            var tracker = DilemmaTracker.For(map) ?? CouncilGameComponent.Instance?.dilemmas;
            return tracker != null && tracker.TryFire(true, d => d.defName == Question) ? "asked of " + s.text + "." : "the council couldn't be asked now (a question is waiting, or nobody to ask).";
        }
    }

    /// <summary>A council question about a named season come round: the god the colony took it for ({GOD}), its place ({PLACE}) and the season's name ({SEASON}).</summary>
    public class SeasonDilemmaContext : GodDilemmaContext
    {
        public string season;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((season ?? "").Named("SEASON"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref season, "season");
        }
    }

    /// <summary>
    /// A named season of last year come round (Rimfolk_Dlm_SeasonDay): the one who took it for a god's doing (else the most
    /// devout who holds that god) asks the council to keep a day against it.
    /// </summary>
    public class DilemmaWorker_SeasonDay : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var s = SeasonGods.ComingRound(map);
            var g = s == null ? null : Gods.ById(s.god);
            if (g == null) return false;
            var a = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.LabelShort == s.believer && Worship.Ours(p) && !p.DevelopmentalStage.Juvenile() && def.roleA.Allows(p, null))
                    ?? GodSpeaker.Of(map, def.roleA, g);
            if (a == null || Worship.GodOf(a) != g && PawnGods.HoldOf(a, g) < PawnGodsMath.LeansFrom) return false;
            ctx = new SeasonDilemmaContext { def = def, map = map, a = a, god1 = g.id, season = s.text };
            s.asked = true;
            return true;
        }
    }

    /// <summary>The council kept a day against the season (or let it pass): the colony's book keeps it, under the god it was taken for.</summary>
    public class DilemmaEffect_SeasonDay : DilemmaEffect
    {
        public bool kept = true;

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer, written into the colony's book.
            var c = ctx as SeasonDilemmaContext;
            var g = c?.God1;
            if (g == null || ctx.a == null) return;
            string words = (kept ? "CC_GA_SeasonDay_Kept" : "CC_GA_SeasonDay_Passed").Translate((c.season ?? "").Named("SEASON"), Worship.Called(g).Named("GOD")).Resolve();
            if (kept) GodLore.Kept(g, words);
            GodLore.Believes(ctx.a, g, words, !kept && Worship.Scoffer(ctx.a));
        }
    }
}
