using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// On a crisis-of-faith mark (<see cref="PersonaMarkDef"/>): how much certainty it takes, before their nature (the
    /// mark's lean scales it, within <see cref="FaithPeopleMath.MinShake"/>..<see cref="FaithPeopleMath.MaxShake"/>), how
    /// far it draws them toward the other person's faith (a rescuer's), and how the faith pages say what caused it.
    /// </summary>
    public class FaithShake : DefModExtension
    {
        public float shake = 0.15f;
        /// <summary>A sway toward the other person's faith, before the listener's character (0: none).</summary>
        public float towardOther;
        /// <summary>What caused it, for the faith pages: {PAWN}, {OTHER} (by name), {LABEL}.</summary>
        [MustTranslate] public string note;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (shake <= 0f || shake > FaithPeopleMath.MaxShake) yield return "shake must be above 0 and at most " + FaithPeopleMath.MaxShake;
            if (towardOther < 0f || towardOther > FaithMath.MaxSway) yield return "towardOther must be 0.." + FaithMath.MaxSway;
            if (note.NullOrEmpty()) yield return "needs a note";
        }
    }

    /// <summary>
    /// A crisis of faith (Faith/, with Ideology): a turning point that shakes someone's certainty for real and stays on
    /// their character page with what caused it. Four happenings, each a family of persona marks
    /// (Defs/PersonaDefs/Rimfolk_PersonaMarks_Faith.xml) with a journal line in their own voice:
    /// <list type="bullet">
    /// <item>FaithLoss: someone they loved died though their faith had a funeral or a prayer to offer; felt in the days after.</item>
    /// <item>FaithRescued: carried out of a fight, or nursed through an illness, by someone of another faith; a shake and a
    /// small sway toward the rescuer's faith, a day or so after.</item>
    /// <item>FaithBurden: the same demand of their own faith weighing on their mood day after day.</item>
    /// <item>FaithNearDeath: they came close to dying; felt once they're up again.</item>
    /// </list>
    /// Rare: each mark has its chance and repeat window, and nobody has two crises within
    /// <see cref="FaithPeopleMath.CrisisGapDays"/> days; nothing is announced before it comes. Ways in are one line each in
    /// Persona/Marks.cs (a mark given, the hourly look at the downed, the daily look). Nothing without Faiths.On and
    /// social life. Contentment rebuilding faith is the game's own daily drift by mood; nothing more is added here.
    /// </summary>
    public static class FaithCrisis
    {
        public const string Loss = "FaithLoss", Rescued = "FaithRescued", Burden = "FaithBurden", NearDeath = "FaithNearDeath";

        /// <summary>The marks of a loved one's death (their story is "Loss").</summary>
        private static readonly HashSet<string> LossFamilies = new HashSet<string> { "LostLove", "LostChild", "LostKin", "LostFriend" };
        /// <summary>The marks of being saved: carried out of a fight, nursed through an illness.</summary>
        private static readonly HashSet<string> RescueFamilies = new HashSet<string> { "SavedBy", "NursedBack" };

        private static bool On => Faiths.On && PersonaMarks.On;

        private static int Now => Find.TickManager.TicksGame;

        // ---------------- Ways in ----------------

        /// <summary>A mark was just given (PersonaMarks.Give): a loved one lost, or a rescue, may become a crisis of faith later.</summary>
        public static void AfterMark(Pawn p, string family, Pawn other)
        {
            try
            {
                if (family == null || other == null || !On || !FaithPeople.Ours(p)) return;
                bool loss = LossFamilies.Contains(family), rescue = !loss && RescueFamilies.Contains(family);
                if (!loss && !rescue) return;
                var tr = FaithPeople.Tracker;
                if (tr == null) return;
                var faith = Faiths.Of(p);
                if (loss)
                {
                    // Their faith had something to offer (a funeral, a prayer), and it wasn't enough: felt in the days after.
                    if (!other.Dead || !OffersComfort(faith)) return;
                    tr.Wait(p, Loss, other, faith.name, Now + FaithPeopleMath.DueAfter(1f, 3f, Rand.Value));
                }
                else
                {
                    if (!Faiths.Holds(other) || Faiths.Same(p, other) || other.Dead) return;
                    tr.Wait(p, Rescued, other, Faiths.Of(other).name, Now + FaithPeopleMath.DueAfter(0.4f, 1.5f, Rand.Value));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith crisis: a mark", e);
            }
        }

        /// <summary>Lying downed (QuirkTracker's hourly look, through PersonaMarks.Notify_Downed): close to death, it waits until they're up.</summary>
        public static void Notify_Downed(Pawn p)
        {
            try
            {
                if (!On || !FaithPeople.Ours(p) || !p.Downed || p.health?.hediffSet == null) return;
                var tr = FaithPeople.Tracker;
                if (tr == null || !CloseToDeath(p)) return;
                tr.Wait(p, NearDeath, null, Faiths.Of(p).name, Now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith crisis: near death", e);
            }
        }

        private static bool CloseToDeath(Pawn p)
        {
            var set = p.health.hediffSet;
            float blood = set.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;
            float worst = 0f;
            foreach (var h in set.hediffs)
                if (h?.def != null && h.def.lethalSeverity > 0f)
                    worst = Math.Max(worst, h.Severity / h.def.lethalSeverity);
            float hours = -1f;
            int ticks = HealthUtility.TicksUntilDeathDueToBloodLoss(p);
            if (ticks >= 0 && ticks < GenDate.TicksPerDay * 3) hours = ticks / (float)GenDate.TicksPerHour;
            return FaithPeopleMath.NearDeath(blood, worst, hours);
        }

        /// <summary>A faith with a funeral or a prayer to offer its dead (most have; read by name, so a mod's own rites count).</summary>
        public static bool OffersComfort(Ideo ideo)
        {
            if (ideo == null) return false;
            foreach (var pr in ideo.PreceptsListForReading)
            {
                var def = pr?.def;
                if (def == null) continue;
                if (def == PreceptDefOf.Funeral || def == PreceptDefOf.FuneralNoCorpse) return true;
                string n = def.defName ?? "";
                if (n.IndexOf("Funeral", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Pray", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                string pattern = def.ritualPatternBase?.defName ?? "";
                if (pattern.IndexOf("Funeral", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // ---------------- Once a day ----------------

        /// <summary>
        /// The daily look (PersonaMarks.Daily): each believer's view about faith kept in step with their certainty, a
        /// faith's demands held too long, and the crises that have come due.
        /// </summary>
        public static void Daily()
        {
            if (!On) return;
            var tr = FaithPeople.Tracker;
            if (tr == null) return;
            try
            {
                tr.Prune();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith: the day's clean-up", e);
            }
            int now = Now;
            foreach (var p in PawnsFinder.AllMaps_FreeColonists.ToList())
            {
                if (!FaithPeople.Ours(p) || !PersonaMarks.CanBeMarked(p)) continue;
                try
                {
                    FaithPeople.KeepView(p, 0f);
                    LookAtBurden(tr, p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("faith crisis: daily", e);
                }
            }
            foreach (var w in tr.waiting.Where(w => w.due <= now).ToList())
            {
                try
                {
                    if (Due(w)) tr.waiting.Remove(w);
                }
                catch (Exception e)
                {
                    tr.waiting.Remove(w);
                    CouncilLog.ErrorOnce("faith crisis: waiting", e);
                }
            }
        }

        /// <summary>A waiting crisis whose time has come: felt now (true), or still waiting (false: they're still down).</summary>
        private static bool Due(FaithWaiting w)
        {
            // At home or away with a caravan: a crisis follows them.
            var p = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.FirstOrDefault(x => x.thingIDNumber == w.pawn);
            if (p == null || !FaithPeople.Ours(p) || !PersonaMarks.CanBeMarked(p)) return true;
            if (w.family == NearDeath && p.Downed) return false;
            Pawn other = null;
            if (w.other >= 0)
            {
                other = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.FirstOrDefault(x => x.thingIDNumber == w.other);
                if (other == null) return true;
                // Saved by someone who has since taken up their faith: no crisis in it.
                if (w.family == Rescued && (!Faiths.Holds(other) || Faiths.Same(p, other))) return true;
            }
            Crisis(p, w.family, other, w.label);
            return true;
        }

        /// <summary>The demand of their faith that weighs on them now, followed day by day; held long and hard enough, a crisis.</summary>
        private static void LookAtBurden(FaithPeopleTracker tr, Pawn p)
        {
            var asks = FaithAsks.Of(p);
            var r = tr.Of(p, asks.Count > 0);
            if (r == null) return;
            var held = r.burden == null ? null : asks.FirstOrDefault(a => a.def.defName == r.burden);
            r.burdenDays = FaithPeopleMath.BurdenStep(r.burdenDays, asks.Count > 0, held != null);
            if (held == null)
            {
                held = asks.FirstOrDefault();
                r.burden = held?.def.defName;
            }
            if (held == null || !FaithPeopleMath.BurdenDue(r.burdenDays, held.mood)) return;
            // One try for each long stretch: whether or not it comes, the days start again.
            r.burdenDays = 0;
            Crisis(p, Burden, null, Faiths.Of(p).name, held.label);
        }

        // ---------------- A crisis ----------------

        /// <summary>
        /// A crisis of this family for them now, unless they had one lately or the mark's own chance and limits say not:
        /// the mark (their character, the page, the journal), then the certainty it takes and, for a rescue, the pull
        /// toward the rescuer's faith. Returns whether it came.
        /// </summary>
        public static bool Crisis(Pawn p, string family, Pawn other, string label, string what = null)
        {
            var tr = FaithPeople.Tracker;
            if (tr == null || !FaithPeople.Ours(p)) return false;
            int now = Now;
            var rec = tr.Of(p, true);
            if (!FaithPeopleMath.CrisisReady(rec.lastCrisis, now)) return false;
            var mark = PersonaMarks.Give(p, family, other, label);
            if (mark?.def == null) return false;
            rec.lastCrisis = now;
            var x = mark.def.GetModExtension<FaithShake>();
            var leans = MarkDefs.Ext(mark.def)?.Leans;
            float shake = FaithPeopleMath.CrisisShake(x?.shake ?? 0.15f, MarksMath.Scale(leans, Persona.Values(p)));
            string note = (x?.note ?? "").Formatted(p.Named("PAWN"), (other?.LabelShort ?? "").Named("OTHER"), (what ?? label ?? "").Named("LABEL")).Resolve();
            FaithPeople.Shake(p, shake, note);
            if (x != null && x.towardOther > 0f && other != null && !other.Dead && Faiths.Holds(other) && !Faiths.Same(p, other))
            {
                // apart: the pull of a rescue is felt later, wherever they are; the rescue itself was face to face.
                float amount = FaithMath.Sway(x.towardOther, 1f, Persona.Get(p, PersonaAxis.Openness), Persona.Get(p, PersonaAxis.Pride),
                    p.relations?.OpinionOf(other) ?? 0, Faiths.DislikesPreaching(p));
                string toward = "CC_FaithC_NoteToward".Translate(other.Named("OTHER"), Faiths.Of(other).name.Named("FAITH")).Resolve();
                string cause = "CC_FaithC_ConvertCause".Translate(other.Named("OTHER"), p.Named("PAWN")).Resolve();
                FaithPeople.SwayToward(p, Faiths.Of(other), amount, cause, other, toward, true);
            }
            return true;
        }
    }
}
