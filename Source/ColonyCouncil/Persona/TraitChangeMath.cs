using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColonyCouncil
{
    /// <summary>What a trait change does to the game's trait: one gained, one lost, or a degree up or down (Nerves, NaturalMood...).</summary>
    public enum TraitChangeKind
    {
        Gain,
        Lose,
        StepUp,
        StepDown
    }

    /// <summary>
    /// Where a change would take a trait: from which degree to which, whether they have the trait before and after. A
    /// degree trait without a degree 0 (Nerves) reads "none" as 0, so stepping nervous up loses the trait and stepping
    /// up from none gains steadfast.
    /// </summary>
    public struct TraitStep
    {
        public bool ok;
        /// <summary>Why not, when not ok: "has", "lacks", "degree", "from", "end".</summary>
        public string why;
        public bool hadTrait, hasAfter;
        public int from, to;

        /// <summary>A trait they didn't have: this adds to their traits (the count limit applies).</summary>
        public bool Adds => ok && !hadTrait && hasAfter;
        public bool Removes => ok && hadTrait && !hasAfter;
    }

    /// <summary>A fact about a person read from a rule ("KillsHumanlikes&gt;=30", "Melee&gt;=10"): a name, a direction, a number.</summary>
    public struct TraitFact
    {
        public string name;
        public bool atLeast;
        public float value;

        public bool Holds(float v) => atLeast ? v >= value : v <= value;
    }

    /// <summary>
    /// The arithmetic of traits that change (Persona/TraitChanges.cs), with no game types, so the tests can check it:
    /// where a degree steps, the rate limits, the absorbing of a trait's nature into what life had moved, the game's
    /// trait conflicts, the facts a rule reads, a pace over a stretch of days, and the traits never touched.
    /// </summary>
    public static class TraitChangeMath
    {
        public const int DayTicks = 60000;
        /// <summary>At most one trait change per person in this many days, and one in the whole colony in <see cref="ColonyGapDays"/>.</summary>
        public const float PersonGapDays = 60f, ColonyGapDays = 10f;
        /// <summary>A trait is gained only by someone with fewer than this many traits (so nobody gains past 4), and nobody keeps more than <see cref="MaxTraits"/>.</summary>
        public const int GainBelow = 4, MaxTraits = 5;
        /// <summary>A pace needs the person here for at least this share of the stretch it's measured over.</summary>
        public const float PaceMinPresence = 0.6f;

        // ---------------- Degrees ----------------

        /// <summary>
        /// Where a change goes. <paramref name="degrees"/> are the degrees the trait has (Nerves: -2, -1, 1, 2; Kind: 0).
        /// For a gain or a loss, <paramref name="degree"/> is the degree gained, or the one that must be lost
        /// (<paramref name="anyDegree"/>: whichever they have). For a step, <paramref name="from"/> lists the degrees it
        /// steps from (none is 0); empty steps from anywhere it can.
        /// </summary>
        public static TraitStep Plan(TraitChangeKind kind, bool has, int current, int degree, bool anyDegree, IList<int> degrees, IList<int> from)
        {
            var s = new TraitStep { hadTrait = has, from = has ? current : 0 };
            bool Have(int d) => degrees != null && degrees.Contains(d);
            switch (kind)
            {
                case TraitChangeKind.Gain:
                    if (has) return Fail(s, "has");
                    if (!Have(degree)) return Fail(s, "degree");
                    s.to = degree;
                    s.hasAfter = true;
                    break;
                case TraitChangeKind.Lose:
                    if (!has) return Fail(s, "lacks");
                    if (!anyDegree && current != degree) return Fail(s, "degree");
                    s.to = 0;
                    s.hasAfter = false;
                    break;
                default:
                {
                    int cur = s.from;
                    if (from != null && from.Count > 0 && !from.Contains(cur)) return Fail(s, "from");
                    int to = cur + (kind == TraitChangeKind.StepUp ? 1 : -1);
                    s.to = to;
                    if (Have(to)) s.hasAfter = true;
                    else if (to == 0 && has) s.hasAfter = false;
                    else return Fail(s, "end");
                    break;
                }
            }
            s.ok = true;
            return s;
        }

        private static TraitStep Fail(TraitStep s, string why)
        {
            s.ok = false;
            s.why = why;
            return s;
        }

        /// <summary>Gaining a new trait needs room: fewer than <see cref="GainBelow"/> traits now.</summary>
        public static bool RoomFor(TraitStep s, int traitCount) => !s.Adds || traitCount < GainBelow;

        // ---------------- Rate limits ----------------

        /// <summary>
        /// Why no trait may change now, or null: "person" (this person changed within <see cref="PersonGapDays"/>), "colony"
        /// (anyone did within <see cref="ColonyGapDays"/>), "trait" (this trait of theirs within <paramref name="gapDays"/>).
        /// A tick below 0 means never. With <paramref name="undoing"/> (the change takes away a trait Rimfolk gave them),
        /// the person's own 60 days don't hold it back: what Rimfolk brought may go as soon as its reason does. With
        /// <paramref name="wayOut"/> (the change leads out of a bad trait, <see cref="IsWayOut"/>), the colony's 10 days
        /// don't hold it back, so gains can't crowd the ways out out; a way out still counts as the colony's change, so
        /// gains wait 10 days after it.
        /// </summary>
        public static string RateBlocked(int lastPerson, int lastColony, int lastSameTrait, int gapDays, int now, bool undoing = false, bool wayOut = false)
        {
            if (!wayOut && lastColony >= 0 && now - lastColony < ColonyGapDays * DayTicks) return "colony";
            if (!undoing && lastPerson >= 0 && now - lastPerson < PersonGapDays * DayTicks) return "person";
            if (lastSameTrait >= 0 && gapDays > 0 && now - lastSameTrait < (long)gapDays * DayTicks) return "trait";
            return null;
        }

        /// <summary>
        /// A change that leads out of a bad trait: the person has the trait now, at one of the degrees the rule names as bad
        /// (<paramref name="wayOutOf"/>; 0 for a trait without degrees), and the change takes it away or steps it toward
        /// none. Stepping nervous up to none is one; stepping none up to steadfast, from the same rule, is a gain.
        /// </summary>
        public static bool IsWayOut(IList<int> wayOutOf, TraitStep step) =>
            step.ok && step.hadTrait && wayOutOf != null && wayOutOf.Contains(step.from);

        // ---------------- Absorbing ----------------

        /// <summary>
        /// A trait came or went, and moved the person's nature by <paramref name="delta"/> on each side. So that the
        /// trait stands for the change rather than adding to it, the same amount is taken out of what life had moved
        /// them: first from the fading part where it goes the same way (the change is now for good, in the trait), then
        /// from the lasting part, which may cross zero; what the lasting cap can't take goes back to the fading part.
        /// The person's sides stay where they were. Returns how far a side still jumped because both caps were reached
        /// (0 in all but extreme cases), summed over the sides.
        /// </summary>
        public static float Absorb(float[] lasting, float[] drift, float[] delta, float maxLasting, float maxDrift)
        {
            float jump = 0f;
            if (lasting == null || drift == null || delta == null) return 0f;
            int n = Math.Min(delta.Length, Math.Min(lasting.Length, drift.Length));
            for (int i = 0; i < n; i++)
            {
                float d = delta[i];
                if (d == 0f) continue;
                // The fading part that went the same way as the trait.
                if (drift[i] != 0f && (drift[i] > 0f) == (d > 0f))
                {
                    float take = Math.Sign(d) * Math.Min(Math.Abs(drift[i]), Math.Abs(d));
                    drift[i] -= take;
                    d -= take;
                }
                if (d == 0f) continue;
                float l = lasting[i] - d;
                float lc = PersonaMath.Clamp(l, maxLasting);
                lasting[i] = lc;
                float rest = l - lc;
                if (rest == 0f) continue;
                float dr = drift[i] + rest;
                float dc = PersonaMath.Clamp(dr, maxDrift);
                drift[i] = dc;
                jump += Math.Abs(dr - dc);
            }
            return jump;
        }

        /// <summary>How far life has moved a side: what stays and what is still fading, within their caps.</summary>
        public static float Moved(float lasting, float drift) =>
            PersonaMath.Clamp(lasting, PersonaMath.MaxLasting) + PersonaMath.Clamp(drift, PersonaMath.MaxDrift);

        // ---------------- Conflicts ----------------

        /// <summary>
        /// Whether a trait may sit beside another, as the game rules it: neither names the other among its conflicting
        /// traits, and they share no exclusion tag.
        /// </summary>
        public static bool Conflict(string a, IList<string> aConflicts, IList<string> aTags, string b, IList<string> bConflicts, IList<string> bTags)
        {
            if (a == null || b == null || a == b) return false;
            if (aConflicts != null && aConflicts.Contains(b)) return true;
            if (bConflicts != null && bConflicts.Contains(a)) return true;
            if (aTags != null && bTags != null)
                for (int i = 0; i < aTags.Count; i++)
                    if (aTags[i] != null && bTags.Contains(aTags[i])) return true;
            return false;
        }

        // ---------------- Traits never touched ----------------

        /// <summary>Who someone loves: never changed by what happens to them.</summary>
        public static readonly string[] Sexuality = { "Gay", "Bisexual", "Asexual", "DislikesMen", "DislikesWomen" };

        /// <summary>
        /// Traits of the body or of a gift they were born with, not of character: never changed here (but see
        /// <see cref="Trained"/>).
        /// </summary>
        public static readonly string[] Body =
        {
            "Nimble", "Beauty", "Immunity", "Delicate", "PsychicSensitivity", "AnnoyingVoice", "CreepyBreathing",
            "QuickSleeper", "BodyMastery", "FastLearner", "SlowLearner", "GreatMemory", "PerfectMemory", "TooSmart", "ShootingAccuracy",
            "VTE_BigBoned", "VTE_ThickSkinned", "VTE_ThinSkinned", "VTE_IronStomach", "VTE_HeatInclined", "VTE_ColdInclined", "VTE_HeavySleeper",
            "VTE_Clumsy", "AG_Beauty", "RT_Nearsighted", "RT_Farsighted", "RT_Athletic", "RT_Stiff", "RT_Muscular", "RT_Frail", "RT_Hardy",
            "RT_Feeble", "RT_Resilient", "RT_Fragile", "RT_Healthy", "RT_ChronicallyIll"
        };

        /// <summary>Why a trait may never be changed by a rule ("sexuality", "body"), or null.</summary>
        /// <summary>
        /// The body's traits its own history can earn: hardened by wounds survived (tough), quickened by a season on its
        /// feet (fast walker). Only ever gained or stepped up, never taken away.
        /// </summary>
        public static readonly string[] Trained = { "Tough", "SpeedOffset" };

        /// <summary>Whether a change of this kind may touch this trait: a trained one only grows.</summary>
        public static bool TrainedOnlyGrows(string trait, bool grows) => Array.IndexOf(Trained, trait) < 0 || grows;

        public static string Untouchable(string trait)
        {
            if (string.IsNullOrEmpty(trait)) return "none";
            if (Array.IndexOf(Sexuality, trait) >= 0) return "sexuality";
            if (Array.IndexOf(Body, trait) >= 0) return "body";
            return null;
        }

        // ---------------- Facts, counts and paces ----------------

        /// <summary>Reads "KillsHumanlikes&gt;=30, Council_VotesLost&gt;=3" (all must hold). Empty is no condition.</summary>
        public static bool TryParseFacts(string text, out List<TraitFact> facts, out string error)
        {
            facts = new List<TraitFact>();
            error = null;
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return true;
            foreach (var raw in text.Split(','))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                int op = part.IndexOfAny(new[] { '>', '<' });
                if (op <= 0)
                {
                    error = "'" + part + "' has no > or <";
                    return false;
                }
                string name = part.Substring(0, op).Trim();
                string num = part.Substring(op + 1).TrimStart('=').Trim();
                if (name.Length == 0 || !float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                {
                    error = "'" + part + "' should read name>=number";
                    return false;
                }
                facts.Add(new TraitFact { name = name, atLeast = part[op] == '>', value = v });
            }
            return true;
        }

        /// <summary>
        /// How many of these moments count: after <paramref name="after"/> (the last change of this trait: a mark or a
        /// deed counts towards one change only) and, with <paramref name="withinDays"/> above 0, within that many days.
        /// </summary>
        public static int CountSince(IList<int> ticks, int now, int after, float withinDays)
        {
            if (ticks == null) return 0;
            int n = 0;
            for (int i = 0; i < ticks.Count; i++)
            {
                int t = ticks[i];
                if (t <= after || t > now) continue;
                if (withinDays > 0f && now - t >= withinDays * DayTicks) continue;
                n++;
            }
            return n;
        }

        /// <summary>
        /// A new moment of a kind already counted: the same one again (about the same person, or within half a day for
        /// those about no one) isn't a separate time.
        /// </summary>
        public static bool SeparateTime(IList<int> ticks, IList<int> abouts, int tick, int about)
        {
            if (ticks == null) return true;
            for (int i = 0; i < ticks.Count; i++)
            {
                int other = abouts != null && i < abouts.Count ? abouts[i] : -1;
                if (about >= 0 && other == about) return false;
                if (about < 0 && Math.Abs(tick - ticks[i]) < DayTicks / 2) return false;
            }
            return true;
        }

        /// <summary>
        /// The share of time spent on something over the last <paramref name="windowDays"/> days, from snapshots of two
        /// running totals (say, time at recreation and time in the colony): measured from the newest snapshot at least
        /// that old. -1 when there's no snapshot old enough or they weren't here for most of it.
        /// </summary>
        public static float Pace(IList<int> ticks, IList<float> nums, IList<float> dens, int now, float numNow, float denNow, float windowDays)
        {
            if (ticks == null || nums == null || dens == null) return -1f;
            int best = -1;
            for (int i = 0; i < ticks.Count && i < nums.Count && i < dens.Count; i++)
            {
                if (now - ticks[i] < windowDays * DayTicks) continue;
                if (best < 0 || ticks[i] > ticks[best]) best = i;
            }
            if (best < 0) return -1f;
            float span = now - ticks[best];
            float den = denNow - dens[best];
            if (span <= 0f || den < span * PaceMinPresence) return -1f;
            float r = (numNow - nums[best]) / den;
            return r < 0f ? 0f : r > 1f ? 1f : r;
        }

        /// <summary>
        /// A zero count: how many days they have gone without any of these moments, counted from the newest of them or from
        /// <paramref name="after"/> (the trait's last change), whichever is later, so a stretch from before the trait came
        /// doesn't count toward it going. Infinity when there is neither (none counted, and the trait never changed).
        /// </summary>
        public static float NoneDays(IList<int> ticks, int now, int after)
        {
            int from = after;
            if (ticks != null)
                for (int i = 0; i < ticks.Count; i++)
                    if (ticks[i] <= now && ticks[i] > from) from = ticks[i];
            return from < 0 ? float.PositiveInfinity : DaysSince(from, now);
        }

        /// <summary>
        /// The kinds of work a practice fact adds up: "Cooking+Smithing" is the hours at the stove and the forge together;
        /// a single name is itself.
        /// </summary>
        public static List<string> PracticeParts(string name)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(name)) return list;
            foreach (var part in name.Split('+'))
                if (part.Trim().Length > 0 && !list.Contains(part.Trim())) list.Add(part.Trim());
            return list;
        }

        /// <summary>Days since <paramref name="since"/> (a tick), or 0 for never.</summary>
        public static float DaysSince(int since, int now) => since < 0 || now < since ? 0f : (now - since) / (float)DayTicks;

        /// <summary>
        /// Calm days: since the last breakdown, and with <paramref name="sinceChange"/> only since the trait last changed
        /// too (a calm that began before they became vengeful doesn't count toward it fading).
        /// </summary>
        public static float CalmDays(int calmSince, int lastChange, int now, bool sinceChange)
        {
            if (calmSince < 0) return 0f;
            int from = sinceChange && lastChange > calmSince ? lastChange : calmSince;
            return DaysSince(from, now);
        }

        // ---------------- What Rimfolk gave ----------------

        /// <summary>
        /// Rimfolk gave them this trait: of its changes kept for this person (the change records are Rimfolk's own), the
        /// newest gave it to them. A trait they came with, or one a change already took away, isn't Rimfolk's to undo.
        /// </summary>
        public static bool GaveIt(IList<int> ticks, IList<bool> gave) => GaveIt(ticks, gave, null, null);

        /// <summary>
        /// As <see cref="GaveIt(IList{int}, IList{bool})"/>, and with <paramref name="by"/> (rule defNames) only when one of
        /// those rules made that newest change: a cannibal that famine brought goes with the famine, one a faith brought
        /// with the faith. <paramref name="rules"/> are the rules of the changes, in the same order as the ticks.
        /// </summary>
        public static bool GaveIt(IList<int> ticks, IList<bool> gave, IList<string> rules, ICollection<string> by)
        {
            if (ticks == null || gave == null) return false;
            int newest = -1;
            for (int i = 0; i < ticks.Count && i < gave.Count; i++)
                if (newest < 0 || ticks[i] >= ticks[newest]) newest = i;
            if (newest < 0 || !gave[newest]) return false;
            if (by == null || by.Count == 0) return true;
            return rules != null && newest < rules.Count && rules[newest] != null && by.Contains(rules[newest]);
        }

        // ---------------- Where life is taking them ----------------

        /// <summary>The share of a rule's evidence that must be there before the character page shows it (ways out aside).</summary>
        public const float BuildingFrom = 0.34f;

        /// <summary>
        /// Whether the character page's "Where life is taking them" shows a rule: a way out of a bad trait always, from the
        /// start, so the player can see what would help; any other once some of its evidence is there, at least a third.
        /// <paramref name="have"/> and <paramref name="total"/> count the parts that are evidence (marks, deeds, records,
        /// time, sides); the parts that hold by default (nothing bad lately, a stretch without something) don't count here.
        /// </summary>
        public static bool BuildingShows(bool wayOut, int have, int total) =>
            wayOut || (total > 0 && have > 0 && have >= total * BuildingFrom);

        // ---------------- Rivals ----------------

        /// <summary>The game calls someone a rival at an opinion of -20 or less (Vanilla Traits Expanded's vengeful reads the same).</summary>
        public const int RivalOpinion = -20;

        /// <summary>A rival: a rivalry on the Bonds page, or the game's opinion of -20 or less.</summary>
        public static bool IsRival(int opinion, bool rivalTie) => rivalTie || opinion <= RivalOpinion;

        // ---------------- Vows of vengeance ----------------

        /// <summary>One of a person's vows of vengeance (Secrets/Vendettas.cs) as the trait rules read it.</summary>
        public struct Vow
        {
            /// <summary>When it was sworn.</summary>
            public int sworn;
            /// <summary>Not settled, the avenger alive and the people who did it still there.</summary>
            public bool open;
            /// <summary>That people still at war with the colony.</summary>
            public bool atWar;
            /// <summary>How many of them have fallen to the avenger (or been taken alive) so far.</summary>
            public int kills;
            /// <summary>The loss it was sworn for: the lost one's id, or their name in an older save.</summary>
            public string loss;

            public Vow(int sworn, bool open, bool atWar, int kills, string loss)
            {
                this.sworn = sworn;
                this.open = open;
                this.atWar = atWar;
                this.kills = kills;
                this.loss = loss;
            }

            /// <summary>Still to be paid: open, against a people at war with the colony.</summary>
            public bool Standing => open && atWar;
        }

        /// <summary>
        /// The vow that has stood longest unpaid: standing, with none of them fallen to the avenger yet, for a loss that hasn't
        /// already made someone take this trait (<paramref name="taken"/>). Its index, or -1; <paramref name="days"/> is how long.
        /// </summary>
        public static int Unpaid(IList<Vow> vows, int now, ICollection<string> taken, out float days)
        {
            days = 0f;
            int pick = -1;
            if (vows == null) return -1;
            for (int i = 0; i < vows.Count; i++)
            {
                var v = vows[i];
                if (!v.Standing || v.kills > 0) continue;
                if (taken != null && v.loss != null && taken.Contains(v.loss)) continue;
                float d = DaysSince(v.sworn, now);
                if (pick >= 0 && d <= days) continue;
                pick = i;
                days = d;
            }
            return pick;
        }

        /// <summary>Any vow still standing: open against a people at war with the colony.</summary>
        public static bool AnyStanding(IList<Vow> vows)
        {
            if (vows == null) return false;
            for (int i = 0; i < vows.Count; i++)
                if (vows[i].Standing) return true;
            return false;
        }

        /// <summary>
        /// The losses behind a change made on <paramref name="changeTick"/>, read from the person's vows when the change
        /// didn't keep it (an older save): the losses of every vow sworn by then.
        /// </summary>
        public static List<string> LossesBehind(IList<Vow> vows, int changeTick)
        {
            var list = new List<string>();
            if (vows == null) return list;
            for (int i = 0; i < vows.Count; i++)
                if (vows[i].loss != null && vows[i].sworn <= changeTick && !list.Contains(vows[i].loss)) list.Add(vows[i].loss);
            return list;
        }
    }
}
