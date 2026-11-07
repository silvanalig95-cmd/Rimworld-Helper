using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A hurt from everyday life still to come (harm, part 4), saved in the harm book: a sickness caught and showing in a
    /// day or two, a guest's cough and who sat closest, a sore loser's walk to the winner, a rematch owed by a people, a
    /// games day waiting for its gathering. Kept only while it can still come.
    /// </summary>
    public sealed class HarmWaiting : IExposable
    {
        public const string Caught = "caught", Cough = "cough", Sore = "sore", Rematch = "rematch", Games = "games";

        public string what;
        /// <summary>The HarmKind's key it comes under.</summary>
        public string kind;
        /// <summary>Who it comes to (the one who caught it, the loser, the colonist owed a rematch), or the coughing guest.</summary>
        public Pawn pawn;
        /// <summary>Who it is about (the patient, the winner, the one who sat closest to a coughing guest).</summary>
        public Pawn other;
        public HediffDef def;
        public int due, made;
        /// <summary>A people's loadID (a rematch), a home map's uniqueID (a games day), or a guest's thingIDNumber.</summary>
        public int id = -1, id2 = -1;
        /// <summary>A name to say: the patient's, the office lost, the one beaten, the day.</summary>
        public string name;
        /// <summary>A duel rather than a bout (a rematch); the beaten one has gone home since (a rematch).</summary>
        public bool flag, flag2;
        /// <summary>Hours near a coughing guest of the closest one.</summary>
        public int count;

        public HarmKind Kind => HarmKinds.Of(kind);

        /// <summary>Nothing left to do: who it comes to is gone (a games day and a rematch need nobody but the people).</summary>
        public bool Broken => what == null || (what != Games && pawn == null) || pawn != null && pawn.Dead && what != Cough;

        /// <summary>Its time has passed without it coming.</summary>
        public bool Lapsed(int now)
        {
            switch (what)
            {
                case Caught: return HarmFourMath.OnsetLapsed(due, now);
                case Sore: return now - due > HarmFourMath.SoreKeepDays * HarmMath.DayTicks;
                case Rematch: return !HarmFourMath.RematchKept(made, now);
                case Games: return now - made > HarmFourMath.GamesKeepHours * HarmMath.HourTicks;
                case Cough: return now - made > 6 * HarmMath.DayTicks;
                default: return true;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref pawn, "pawn", true);
            Scribe_References.Look(ref other, "other", true);
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref due, "due");
            Scribe_Values.Look(ref made, "made");
            Scribe_Values.Look(ref id, "id", -1);
            Scribe_Values.Look(ref id2, "id2", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref flag, "flag");
            Scribe_Values.Look(ref flag2, "flag2");
            Scribe_Values.Look(ref count, "count");
        }
    }

    /// <summary>
    /// Harm, part 4: the fifteen proposals the player took on 7 November 2026 (docs/harm-review-proposals.md, each marked
    /// built there), each through the harm service with its kind, so the shared budget, the setting, the storyteller and
    /// the cap hold for all of them (Harm/Harm.cs, HarmBudgetMath.cs; the rates in HarmFourMath.cs). This is the hub: the
    /// harm book's hour (what is waiting, the visits, the evenings, the lessons) and its 250-tick slices (each colonist's
    /// hour: the pens in the rut, a cold bedroom, a drink before hot work; the riders). Nothing here has a clock of its
    /// own. The rest is in Catching.cs, Mishaps.cs, AnimalHurts.cs, Quarrels.cs and GamesDay.cs.
    /// </summary>
    public static class HarmFour
    {
        private static readonly List<Pawn> slice = new List<Pawn>();

        private static int Now => Find.TickManager.TicksGame;

        public static void Reset()
        {
            Catching.Reset();
            Mishaps.Reset();
            AnimalHurts.Reset();
            Quarrels.Reset();
            GamesDay.Reset();
        }

        /// <summary>The harm book's hour (its first slice): what is waiting, then the colony's looks.</summary>
        public static void Hourly(HarmBook book, int now)
        {
            var list = book.waiting;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var x = list[i];
                bool done;
                try
                {
                    done = x == null || x.Broken || x.Lapsed(now) || Due(x, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("harm four: something waiting (" + x?.what + ")", e);
                    done = true;
                }
                if (done && i < list.Count) list.RemoveAt(i);
            }
            Safe("the visits' coughs", () => Catching.Visits(book, now));
            Safe("the guests' evenings", () => Quarrels.GuestEvenings(now));
            Safe("the herdmaster's lessons", () => AnimalHurts.Lessons(now));
        }

        /// <summary>Something waiting, at the hour: true when it is done with (come, or let go).</summary>
        private static bool Due(HarmWaiting x, int now)
        {
            switch (x.what)
            {
                case HarmWaiting.Caught: return now >= x.due && Catching.Show(x);
                case HarmWaiting.Cough: return Catching.CoughHour(x, now);
                case HarmWaiting.Sore: return now >= x.due && Quarrels.SoreHour(x, now);
                case HarmWaiting.Rematch: return Quarrels.RematchHour(x, now);
                case HarmWaiting.Games: return GamesDay.Hour(x, now);
                default: return true;
            }
        }

        /// <summary>One slice of the colony's hour (every 250 ticks): each colonist in it once an hour; the riders every time.</summary>
        public static void Slice(HarmBook book, int which, int now)
        {
            slice.Clear();
            var all = PawnsFinder.AllMaps_FreeColonistsSpawned;
            for (int i = 0; i < all.Count; i++)
                if (Sched.SliceOf(all[i].thingIDNumber, Sched.Slices) == which) slice.Add(all[i]);
            for (int i = 0; i < slice.Count; i++)
            {
                var p = slice[i];
                if (p?.Map == null || p.Dead || p.RaceProps?.Humanlike != true || p.health?.hediffSet == null) continue;
                try
                {
                    Catching.ColdHour(p, now);
                    Mishaps.HotHour(p, now);
                    AnimalHurts.RutHour(p, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("harm four: the hour of " + p.LabelShort, e);
                }
            }
            slice.Clear();
            Safe("the riders", () => AnimalHurts.Riders(now));
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm four: " + what, e);
            }
        }

        /// <summary>Adds something waiting to the harm book.</summary>
        public static void Wait(HarmWaiting x)
        {
            var book = HarmBook.Instance;
            if (book == null || x == null) return;
            x.made = x.made == 0 ? Now : x.made;
            book.waiting.Add(x);
        }

        // ================= On the map =================

        private static readonly Color SayColor = new Color(1f, 0.7f, 0.6f);

        /// <summary>
        /// A line from a pool over someone's head (one sentence), tagged for what happened; to someone else only when the two
        /// are together (the caller brings them face to face or checks Together.Can).
        /// </summary>
        public static void Say(Pawn p, string pool, Pawn to = null, string tag = null, string tag2 = null)
        {
            try
            {
                if (p == null || !p.Spawned) return;
                if (to == p) to = null;
                string line = Flavor.FromPool(pool, p, null, null, tag, tag2);
                if (line.NullOrEmpty()) return;
                var args = to != null ? new[] { p.Named("PAWN"), to.Named("OTHER") } : new[] { p.Named("PAWN") };
                // together: a word to the other only when the caller has them face to face.
                Speech.Say(p, line.Formatted(args).Resolve(), to, SayColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm four: a line from " + pool, e);
            }
        }

        /// <summary>A few words over them where the hurt happened, for a hurt the game would tell with no message of its own.</summary>
        public static void Mote(Pawn p, string key, params NamedArgument[] args)
        {
            try
            {
                if (p?.Map == null || !p.Spawned) return;
                MoteMaker.ThrowText(p.DrawPos + new Vector3(0f, 0f, 0.6f), p.Map, key.Translate(args).Resolve(), new Color(1f, 0.55f, 0.45f), 3.5f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm four: a mote " + key, e);
            }
        }

        /// <summary>The work stops; they sit down hard a few moments where they are (a fall, a kick, a charge).</summary>
        public static void SitDown(Pawn p, int ticks = 240)
        {
            try
            {
                if (p?.jobs == null || !p.Spawned || p.Downed) return;
                if (p.carryTracker?.CarriedThing != null) p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
                var wait = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
                wait.expiryInterval = ticks;
                p.jobs.StartJob(wait, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm four: sitting down", e);
            }
        }

        /// <summary>The news, as harm always is (Importance.Important), for the hurts the game would tell too: an illness caught, an animal turning on its handler.</summary>
        public static void TellHurt(Pawn p, string key, params NamedArgument[] args)
        {
            try
            {
                string text = TextVariants.Vary(key).Translate(args).Resolve().TrimEnd();
                ColonyCouncil.Tell.Message(NewsKind.Health, Importance.Important, p.MapHeld, text, new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm four: telling " + key, e);
            }
        }

        /// <summary>A child (the mildest only), not a baby.</summary>
        public static bool Child(Pawn p) => p.DevelopmentalStage.Juvenile() && !p.DevelopmentalStage.Baby();

        /// <summary>One of the colony's own humanlike people at home, up and about.</summary>
        public static bool Ours(Pawn p) => p != null && !p.Dead && p.Spawned && p.RaceProps?.Humanlike == true && p.Faction == Faction.OfPlayer && !p.IsQuestLodger()
                                           && p.Map != null && p.Map.IsPlayerHome && p.health?.hediffSet != null;

        /// <summary>The colony's best doctor within talking range of someone, up and about (null: none there).</summary>
        public static Pawn DoctorNear(Pawn p)
        {
            Pawn best = null;
            int bestLevel = -1;
            if (p?.Map == null) return null;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || o.DevelopmentalStage.Juvenile() || o.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) || !Together.Can(o, p)) continue;
                int level = o.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
                if (level > bestLevel)
                {
                    best = o;
                    bestLevel = level;
                }
            }
            return best;
        }

        private static readonly Dictionary<string, HediffDef> defs = new Dictionary<string, HediffDef>();

        public static HediffDef Def(string name)
        {
            if (defs.TryGetValue(name, out var d) && d != null) return d;
            d = DefDatabase<HediffDef>.GetNamedSilentFail(name);
            defs[name] = d;
            return d;
        }

        private static readonly Dictionary<string, DecreeDef> decrees = new Dictionary<string, DecreeDef>();

        /// <summary>A decree in force for someone, by its defName.</summary>
        public static bool Decree(Pawn p, string defName)
        {
            if (!decrees.TryGetValue(defName, out var d))
            {
                d = DefDatabase<DecreeDef>.GetNamedSilentFail(defName);
                decrees[defName] = d;
            }
            return d != null && DecreeTracker.InForceFor(p, d);
        }
    }
}
