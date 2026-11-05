using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// Banishment by the council (the player's request, 5 November 2026; DESIGN.md, "Banishment by the council"): who the
    /// council is asked about, and when. Someone keeps causing trouble (social fights, chiefly, and the insults they throw)
    /// and most of the colony can't stand them, with almost nobody standing by them: first the council has a word, and only
    /// if the trouble goes on after it does a later question offer to send them away. Pure (no game types): the thresholds
    /// are named here and Tools/Tests checks them (BanishTests).
    /// </summary>
    public static class BanishMath
    {
        public const int DayTicks = 60000;

        // ---------------- The trouble ----------------

        /// <summary>How far back the trouble is read: the last month or so.</summary>
        public const float WindowDays = 30f;
        /// <summary>A fight they threw the first punch in.</summary>
        public const float StartedWeight = 1f;
        /// <summary>A fight someone else started with them: it takes two, but less is theirs.</summary>
        public const float JoinedWeight = 0.6f;
        /// <summary>An insult they threw (the game keeps the insulted person's memory of it for twenty days).</summary>
        public const float InsultWeight = 0.25f;
        /// <summary>Insults never count for more than this together: fights are the trouble.</summary>
        public const float InsultsAtMost = 1f;
        /// <summary>
        /// One fight is seen more than once (Rimfolk's note as it starts, and both fighters' memories of it once it ends):
        /// sightings of a fight with the same person this close together are one fight.
        /// </summary>
        public const int MergeTicks = DayTicks / 2;

        /// <summary>The first question needs at least this many fights in the window, and this much trouble.</summary>
        public const int WordFights = 2;
        public const float WordScore = 1.6f;

        /// <summary>
        /// After the council stepped in (a word, or their nights kept apart from the others), the second question needs
        /// this much trouble since, with at least one fight, and at least a few days gone by.
        /// </summary>
        public const float AgainScore = 1.6f;
        public const int AgainFights = 1;
        public const float MinDaysAfterStep = 3f;
        /// <summary>A word or the nights apart are remembered this long; quiet that long, and the council starts again with a word.</summary>
        public const float StepKeepDays = 60f;
        /// <summary>After "let it be", nobody asks about them again for this long.</summary>
        public const float LetBeDays = 20f;

        // ---------------- The colony's view of them ----------------

        /// <summary>A colony needs at least this many people (lodgers aside) to spare one.</summary>
        public const int MinPeople = 6;
        /// <summary>Someone of the colony whose opinion of them is this or lower dislikes them.</summary>
        public const int DislikeOpinion = -10;
        /// <summary>Someone whose opinion is this or higher stands by them; so do a partner or close kin who don't dislike them.</summary>
        public const int StandByOpinion = 20;
        /// <summary>Most of the colony: this share of the others, and never fewer than <see cref="MinDislikers"/>.</summary>
        public const float MostShare = 0.6f;
        public const int MinDislikers = 3;
        /// <summary>Almost nobody: at most this many stand by them.</summary>
        public const int MaxStandBy = 1;
        /// <summary>Those whose opinion is this or lower are glad to see them go (the game's own line for a rival).</summary>
        public const int GladOpinion = -20;

        // ---------------- After ----------------

        /// <summary>The chance that someone sent away goes with a grudge (decided as they go).</summary>
        public const float GrudgeChance = 0.3f;
        /// <summary>One with a grudge may ride with a raid of hostile people against the colony: this chance a raid, once at most.</summary>
        public const float RaidChance = 0.12f;
        /// <summary>Never sooner than this after they were sent away.</summary>
        public const float RaidAfterDays = 15f;
        /// <summary>A skill they're the colony's best at, from this level; one they're good at, from this level.</summary>
        public const int BestFrom = 6;
        public const int GoodFrom = 10;

        // ---------------- Fights ----------------

        /// <summary>One sighting of a fight: who with (their id), when, and whether they threw the first punch (null: not known).</summary>
        public struct Fight
        {
            public int other;
            public int tick;
            public bool? started;

            public Fight(int other, int tick, bool? started)
            {
                this.other = other;
                this.tick = tick;
                this.started = started;
            }
        }

        /// <summary>
        /// The fights among the sightings, each once: sightings with the same person within <see cref="MergeTicks"/> of each
        /// other are one fight (who started it is known if any sighting knows), the earliest tick kept.
        /// </summary>
        public static List<Fight> Merge(IEnumerable<Fight> seen)
        {
            var list = new List<Fight>();
            if (seen == null) return list;
            foreach (var f in seen.OrderBy(x => x.other).ThenBy(x => x.tick))
            {
                int last = list.Count - 1;
                if (last >= 0 && list[last].other == f.other && f.tick - list[last].tick <= MergeTicks)
                {
                    var m = list[last];
                    if (m.started == null) m.started = f.started;
                    else if (f.started == true) m.started = true;
                    list[last] = m;
                    continue;
                }
                list.Add(f);
            }
            return list.OrderBy(x => x.tick).ToList();
        }

        /// <summary>How much trouble someone caused since <paramref name="from"/>: the fights, who started them, the insults.</summary>
        public struct Trouble
        {
            public int fights;
            public int started;
            public int joined;
            public int insults;
            public float score;
            /// <summary>The newest fight's tick, or -1.</summary>
            public int lastFight;
        }

        /// <summary>The score for so many fights started and joined and insults thrown.</summary>
        public static float Score(int started, int joined, int insults) =>
            started * StartedWeight + joined * JoinedWeight + Math.Min(InsultsAtMost, Math.Max(0, insults) * InsultWeight);

        /// <summary>The trouble from the fights (merged) at or after <paramref name="from"/>, and the insult ticks at or after it.</summary>
        public static Trouble Of(IEnumerable<Fight> seen, IEnumerable<int> insultTicks, int from)
        {
            var t = new Trouble { lastFight = -1 };
            foreach (var f in Merge(seen))
            {
                if (f.tick < from) continue;
                t.fights++;
                if (f.started == true) t.started++;
                else t.joined++;
                if (f.tick > t.lastFight) t.lastFight = f.tick;
            }
            t.insults = insultTicks?.Count(x => x >= from) ?? 0;
            t.score = Score(t.started, t.joined, t.insults);
            return t;
        }

        /// <summary>Enough trouble in the window for the council's first word.</summary>
        public static bool WordDue(Trouble t) => t.fights >= WordFights && t.score >= WordScore;

        /// <summary>Enough trouble since the council stepped in for the second question.</summary>
        public static bool AgainDue(Trouble since) => since.fights >= AgainFights && since.score >= AgainScore;

        /// <summary>The window's first tick at <paramref name="now"/>.</summary>
        public static int WindowFrom(int now) => now - (int)(WindowDays * DayTicks);

        // ---------------- Where they stand with the council ----------------

        /// <summary>Which question about them is due, if any.</summary>
        public enum Ask
        {
            None,
            /// <summary>The first: a word, the nights apart, or let it be.</summary>
            Word,
            /// <summary>The second: banishment beside the milder answers.</summary>
            Banish
        }

        /// <summary>
        /// The question due about someone at <paramref name="now"/>: <paramref name="stepTick"/> when the council last stepped
        /// in (-1 never), <paramref name="letBeUntil"/> the end of a "let it be" (-1 none), the trouble in the window and since
        /// the step. A step older than <see cref="StepKeepDays"/> is forgotten (they kept the peace that long).
        /// </summary>
        public static Ask Due(int now, int stepTick, int letBeUntil, Trouble window, Trouble sinceStep)
        {
            if (letBeUntil >= 0 && now < letBeUntil) return Ask.None;
            if (StepHolds(now, stepTick))
            {
                if (now - stepTick < MinDaysAfterStep * DayTicks) return Ask.None;
                return AgainDue(sinceStep) ? Ask.Banish : Ask.None;
            }
            return WordDue(window) ? Ask.Word : Ask.None;
        }

        /// <summary>The council's last step is still remembered.</summary>
        public static bool StepHolds(int now, int stepTick) => stepTick >= 0 && now - stepTick <= StepKeepDays * DayTicks;

        // ---------------- The colony ----------------

        /// <summary>The colony can spare someone: <paramref name="people"/> of its own, lodgers aside, the person included.</summary>
        public static bool BigEnough(int people) => people >= MinPeople;

        /// <summary>
        /// Most of the colony dislikes them and almost nobody stands by them: <paramref name="others"/> the others of the colony
        /// with a say (not babies), <paramref name="dislike"/> those who dislike them, <paramref name="standBy"/> those who stand
        /// by them.
        /// </summary>
        public static bool Disliked(int others, int dislike, int standBy) =>
            others > 0 && dislike >= MinDislikers && dislike >= MostShare * others && standBy <= MaxStandBy;

        /// <summary>Someone's opinion of them makes them one who dislikes them.</summary>
        public static bool Dislikes(int opinion) => opinion <= DislikeOpinion;

        /// <summary>Someone stands by them: a good opinion, or a partner or close kin who doesn't dislike them.</summary>
        public static bool StandsBy(int opinion, bool closeKinOrPartner) => opinion >= StandByOpinion || (closeKinOrPartner && opinion > DislikeOpinion);

        /// <summary>Someone is glad to see them go.</summary>
        public static bool Glad(int opinion) => opinion <= GladOpinion;

        // ---------------- The letter's voices ----------------

        /// <summary>A council member's stance by their opinion of the person: "for" sending them away, "against", or "unsure".</summary>
        public static string Stance(int opinion, bool closeKinOrPartner)
        {
            if (StandsBy(opinion, closeKinOrPartner)) return "against";
            if (opinion <= GladOpinion) return "for";
            return "unsure";
        }

        /// <summary>
        /// How the person takes it, for their own say in the letter: "defiant" when they threw the first punch more often
        /// than not, "bitter" when they're low, else "sorry".
        /// </summary>
        public static string Say(int started, int joined, float mood) =>
            started > joined ? "defiant" : mood < 0.35f ? "bitter" : "sorry";

        // ---------------- What the colony loses ----------------

        /// <summary>
        /// The skills the colony would lose a hand at: those they're the colony's best at (from <see cref="BestFrom"/>, above
        /// everyone else), then those they're good at (from <see cref="GoodFrom"/>), by name. <paramref name="theirs"/> their
        /// levels, <paramref name="othersBest"/> the best level of anyone else at home.
        /// </summary>
        public static void Lost(IDictionary<string, int> theirs, IDictionary<string, int> othersBest, List<string> best, List<string> good)
        {
            if (theirs == null) return;
            foreach (var kv in theirs.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
            {
                int other = othersBest != null && othersBest.TryGetValue(kv.Key, out int o) ? o : 0;
                if (kv.Value >= BestFrom && kv.Value > other) best?.Add(kv.Key);
                else if (kv.Value >= GoodFrom) good?.Add(kv.Key);
            }
        }

        // ---------------- Every age ----------------

        /// <summary>The word for it by the colony's age (tribal, medieval, industrial, spacer): CC_Banish_Word_tribal.</summary>
        public static string WordKey(string ageTag) => "CC_Banish_Word_" + (ageTag ?? "industrial");

        /// <summary>The same as a verb in an answer's label: CC_Banish_Verb_tribal ("Cast {0} out from the fire").</summary>
        public static string VerbKey(string ageTag) => "CC_Banish_Verb_" + (ageTag ?? "industrial");

        public static readonly string[] AgeTags = { "tribal", "medieval", "industrial", "spacer" };

        // ---------------- After ----------------

        /// <summary>One sent away with a grudge may ride with this raid: long enough gone, never twice.</summary>
        public static bool MayRide(bool grudge, bool rodeAlready, int banishedTick, int now) =>
            grudge && !rodeAlready && banishedTick >= 0 && now - banishedTick >= RaidAfterDays * DayTicks;
    }
}
