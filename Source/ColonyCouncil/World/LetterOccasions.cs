using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Letters by occasion (docs/design/post-and-letters.md, step 2, point 10; the rules are World/LetterOccasionMath.cs):
    /// what the occasions are read from, all of it what Rimfolk already tracks. A loss heard of and a threat known are the
    /// letters' own topics (NeighbourLetters.Topics); a feast coming is the home's feast with guests (Diplomacy/Feasts.cs);
    /// a gathering day is a day of the colony calendar kept with a feast, games, a bonfire, gifts or a custom, within the
    /// week (Calendar/); the guest beds are Hospitality's, with nobody from outside staying; the colony short is its days of
    /// food and its medicine as the advisor counts them (Lean/MapFacts.cs); the turn of the year is the date at the home; a
    /// gift is one their people remember the colony gave lately (World/FactionMemory.cs). The letter's dialog and the
    /// Peoples view ask <see cref="WhyNot"/>, so both grey a letter for the same reason. Nothing here is saved; each
    /// notable's facts are kept for a second.
    /// </summary>
    public static class LetterOccasions
    {
        /// <summary>The setting "Letters by occasion" (with the letters on).</summary>
        public static bool On => (ColonyCouncilMod.Settings?.letterOccasions ?? true) && NeighbourLetters.On;

        /// <summary>Their people remember the colony gave them something: a gift sent, a gift into their hands, learning given.</summary>
        private static readonly string[] GiftDeeds = { "gift", "gaveGift", "gaveLearning" };

        /// <summary>The calendar's ways of keeping a day that gather people: worth inviting a neighbour to.</summary>
        private static bool Gathers(DayWay w) => w == DayWay.Feast || w == DayWay.Games || w == DayWay.Bonfire || w == DayWay.Gifts || w == DayWay.Custom;

        private sealed class Kept
        {
            public OccasionFacts facts;
            public string loss, threat, gathering;
            public float at;
        }

        private static readonly Dictionary<long, Kept> kept = new Dictionary<long, Kept>();

        private static Map HomeFor(Map home) => home ?? HappeningTracker.HomeMap();

        /// <summary>What the occasions to write to them are read from (kept for a second).</summary>
        public static OccasionFacts Facts(Notable n, Map home = null) => Get(n, HomeFor(home))?.facts ?? default;

        private static Kept Get(Notable n, Map home)
        {
            if (n == null) return null;
            long key = ((long)(home?.uniqueID ?? -1) << 32) ^ (uint)n.id;
            float t = Time.realtimeSinceStartup;
            if (kept.TryGetValue(key, out var k) && t - k.at < 1f && t >= k.at) return k;
            k = new Kept { at = t };
            try
            {
                Build(k, n, home);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters by occasion", e);
            }
            if (kept.Count > 200) kept.Clear();
            kept[key] = k;
            return k;
        }

        private static void Build(Kept k, Notable n, Map home)
        {
            var f = new OccasionFacts { foodDays = 999f };
            var losses = NeighbourLetters.Topics(n, LetterPurpose.Condolences);
            var threats = NeighbourLetters.Topics(n, LetterPurpose.Warning);
            f.lossHeard = losses.Count > 0;
            f.threatKnown = threats.Count > 0;
            k.loss = losses.Count > 0 ? losses[0].text : null;
            k.threat = threats.Count > 0 ? threats[0].text : null;
            int now = Find.TickManager.TicksGame;
            var mem = FactionMemory.Instance;
            if (mem != null && n.faction != null)
                f.giftLately = mem.Of(n.faction).Any(d => GiftDeeds.Contains(d.kind) && LetterOccasionMath.Lately((now - d.tick) / (float)GenDate.TicksPerDay));
            if (home != null)
            {
                var plan = Feasts.On ? Feasts.At(home) : null;
                f.feastComing = plan != null && plan.Live && !plan.calledOff;
                int today = GenLocalDate.DayOfYear(home);
                f.dayOfYear = today;
                var cal = ColonyCalendar.On ? ColonyCalendar.For(home) : null;
                if (cal != null)
                {
                    CalendarDay best = null;
                    int bestIn = int.MaxValue;
                    foreach (var d in cal.days)
                    {
                        if (d == null || !d.Dated || !Gathers(d.way)) continue;
                        int until = CalendarMath.DaysUntil(today, d.day);
                        if (!LetterOccasionMath.Coming(until) || until >= bestIn) continue;
                        best = d;
                        bestIn = until;
                    }
                    f.gatheringComing = best != null;
                    k.gathering = best != null ? best.label ?? ColonyCalendar.DefaultLabel(best) : null;
                }
                f.guestBedsEmpty = home.listerBuildings.allBuildingsColonist.Any(b => b is Building_Bed && b.Spawned && GuestBeds.IsGuestBed(b))
                                   && !home.mapPawns.AllPawnsSpawned.Any(Outsiders.IsOutsider);
                var facts = MapFacts.Of(home);
                f.foodDays = facts.FoodDays;
                f.medicine = facts.Medicine;
                f.colonists = home.mapPawns.FreeColonistsSpawnedCount;
            }
            k.facts = f;
        }

        /// <summary>Why a letter of this kind can be written now, or <see cref="LetterOccasion.None"/>.</summary>
        public static LetterOccasion Of(Notable n, LetterPurpose p, Map home = null)
        {
            if (!LetterOccasionMath.ByOccasion(p)) return LetterOccasion.Own;
            var k = Get(n, HomeFor(home));
            return k == null ? LetterOccasion.None : LetterOccasionMath.Occasion(p, k.facts);
        }

        /// <summary>
        /// The reason in words (after the loss of their mayor, the stores are short of food, the turn of the year), or, with
        /// none, what would bring one ("No reason now: ..."); null for a purpose that isn't a kind letter.
        /// </summary>
        public static string Reason(Notable n, LetterPurpose p, Map home = null)
        {
            if (!LetterOccasionMath.ByOccasion(p)) return null;
            var k = Get(n, HomeFor(home));
            if (k == null) return null;
            var o = LetterOccasionMath.Occasion(p, k.facts);
            string about = o == LetterOccasion.Loss ? k.loss : o == LetterOccasion.Threat ? k.threat : o == LetterOccasion.Gathering ? k.gathering : "";
            return LetterOccasionMath.Key(p, o).Translate((about ?? "").Named("ABOUT"), (n.faction?.Name ?? "").Named("PEOPLE")).Resolve();
        }

        /// <summary>
        /// Why this letter can't be written to them now, or null: a kind letter with no reason first ("No reason now: ...",
        /// with the setting on; condolences and a warning always had theirs), then the letters' own checks
        /// (NeighbourLetters.WhyNot), whoever answers it (World/LetterRoutes.cs).
        /// </summary>
        public static string WhyNot(Notable n, LetterPurpose p) => LetterRoutes.WhyNot(n, p, true);

        /// <summary>
        /// A kind letter with no reason to write it now, held ("No reason now: ..."), read of the one who answers it; null when
        /// it has one, or isn't a kind letter (World/LetterRoutes.cs asks it before the letters' own checks).
        /// </summary>
        public static string Held(Notable n, LetterPurpose p)
        {
            if (n != null && n.Alive && LetterOccasionMath.ByOccasion(p) && NeighbourLetters.On && LetterOccasionMath.Holds(p, Of(n, p), On)) return Reason(n, p);
            return null;
        }

        /// <summary>The letters' cache let go (a game loaded).</summary>
        public static void Reset() => kept.Clear();
    }
}
