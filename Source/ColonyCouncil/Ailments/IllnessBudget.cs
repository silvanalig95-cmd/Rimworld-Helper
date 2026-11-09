using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One home's part of the shared illness budget, saved in the ailments' component (AilmentTracker, "illnessBudget").</summary>
    public class IllnessLedger : IllnessBudgetBook, IExposable
    {
        public void ExposeData()
        {
            Scribe_Collections.Look(ref owed, "owed", LookMode.Value);
            Scribe_Collections.Look(ref owedFor, "owedFor", LookMode.Value);
            Scribe_Values.Look(ref lastGame, "lastGame", IllnessBudgetMath.Never);
            Scribe_Values.Look(ref lastSkip, "lastSkip", IllnessBudgetMath.Never);
            Scribe_Values.Look(ref lastPaid, "lastPaid");
            Scribe_Values.Look(ref lastPutOff, "lastPutOff", IllnessBudgetMath.Never);
            Scribe_Values.Look(ref held, "held");
            Scribe_Values.Look(ref cameAnyway, "cameAnyway");
            Scribe_Values.Look(ref putOff, "putOff");
            Scribe_Values.Look(ref note, "note");
            Scribe_Values.Look(ref noteTick, "noteTick", IllnessBudgetMath.Never);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                owed ??= new List<int>();
                owedFor ??= new List<string>();
            }
        }
    }

    /// <summary>
    /// The shared illness budget, the game's side (the rules are IllnessBudgetMath): Rimfolk's serious illnesses mostly take a
    /// turn of the game's own diseases instead of adding one. A serious case of Rimfolk's that starts on a home owes a skip,
    /// and the game's next human disease the storyteller aims there is held back three times in four
    /// (<see cref="Patch_IllnessBudget"/>), the debt let go when it comes all the same; one debt at a time; once the game's
    /// own disease has come, Rimfolk's rest a week there and come back over a month; with all that they come more readily,
    /// and far less to someone already fighting a serious illness (<see cref="SeriousFactor"/>, <see cref="Spare"/>). The
    /// records say so: a line in the news when a disease is held back or a case put off, the serious illness's letter, the
    /// advisor's sickness line. "Serious illness shares the game's own rate" (How hard, the ailments) off, or the deeper game
    /// hooks off: as it was before.
    /// </summary>
    public static class IllnessBudget
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool SettingOn => S != null && S.illnessBudget;

        /// <summary>The hook that holds the game's diseases back is in place and switched on.</summary>
        public static bool Hooked => RimfolkPatches.On && RimfolkPatches.Applied.Contains(nameof(Patch_IllnessBudget));

        public static bool On => SettingOn && AilmentTracker.SeriousOn && Hooked;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- The game's own rate ----------------

        /// <summary>The storyteller has a part that sends the game's human diseases (every one of the game's own does).</summary>
        public static bool StorytellerSends()
        {
            var comps = Find.Storyteller?.storytellerComps;
            if (comps == null) return false;
            for (int i = 0; i < comps.Count; i++)
                if (comps[i]?.props is StorytellerCompProperties_Disease p && p.category == IncidentCategoryDefOf.DiseaseHuman)
                    return true;
            return false;
        }

        /// <summary>The game's mean days between its human diseases on this home (infinite: it sends none).</summary>
        public static float GameMeanDays(Map map) =>
            IllnessBudgetMath.GameMeanDays(map?.Biome?.diseaseMtbDays ?? 60f, Find.Storyteller?.difficulty?.diseaseIntervalFactor ?? 1f,
                DebugSettings.enableRandomDiseases && StorytellerSends(), Difficulty.DiseaseIntervalFactorCutoff);

        public static float KeepDays(Map map) => IllnessBudgetMath.KeepDays(GameMeanDays(map));

        /// <summary>Whether this firing counts in the budget (the storyteller's own human disease aimed at a home), and if not why.</summary>
        public static BudgetFiring Which(FiringIncident fi)
        {
            var def = fi?.def;
            var parms = fi?.parms;
            bool home = parms?.target is Map m && m.IsPlayerHome;
            bool rimfolk = def?.workerClass != null && typeof(IncidentWorker_Ailment).IsAssignableFrom(def.workerClass);
            return IllnessBudgetMath.Which(fi?.source != null, def != null && def.category == IncidentCategoryDefOf.DiseaseHuman, rimfolk,
                parms?.forced ?? false, parms?.quest != null || fi?.sourceQuestPart != null, home);
        }

        public static IllnessLedger LedgerOf(Map map, bool create) => map == null ? null : AilmentTracker.Instance?.Ledger(map, create);

        // ---------------- Rimfolk's side ----------------

        /// <summary>The home a case of Rimfolk's counts against: the one they're on, else the colony they belong to.</summary>
        public static Map HomeOf(Pawn p)
        {
            var m = p?.MapHeld;
            if (m != null && m.IsPlayerHome) return m;
            var home = Homes.Of(p);
            return home != null && home.IsPlayerHome ? home : Find.AnyPlayerHomeMap;
        }

        /// <summary>
        /// Whether a new serious illness of Rimfolk's may start on this home now, within the budget: never while a debt is
        /// owed or in the pause after the game's own, at a lowered chance in the weeks after it. When it may not, the case is
        /// put off (counted, and now and then said). Asked once its own chance has come up and the colony's own room (one
        /// serious illness at a time, none within thirty days of the last) is open.
        /// </summary>
        public static bool Allows(Map map, HediffDef def, Pawn p)
        {
            if (!On || map == null) return true;
            var b = LedgerOf(map, false);
            if (b == null) return true;
            int now = Now;
            float keep = KeepDays(map);
            if (IllnessBudgetMath.Allows(b, now, keep, Rand.Value)) return true;
            PutOff(map, b, def, p, IllnessBudgetMath.Room(b, now, keep), now);
            return false;
        }

        // ---------------- Two at once ----------------

        private static HashSet<HediffDef> gameDiseases;
        private static readonly Dictionary<HediffDef, bool> seriousKinds = new Dictionary<HediffDef, bool>();

        /// <summary>
        /// A serious illness, for the overlap rule: one of Rimfolk's serious ones, anything the game's human disease events
        /// give (the game's own and mods'), or another illness that races immunity and can kill (a wound infection). Not the
        /// everyday ailments, food poisoning or the like.
        /// </summary>
        public static bool IsSeriousIllness(HediffDef def)
        {
            if (def == null) return false;
            if (seriousKinds.TryGetValue(def, out bool known)) return known;
            bool serious;
            var info = AilmentTracker.InfoOf(def);
            if (info != null) serious = info.serious;
            else
            {
                if (gameDiseases == null)
                {
                    gameDiseases = new HashSet<HediffDef>();
                    foreach (var inc in DefDatabase<IncidentDef>.AllDefsListForReading)
                        if (inc?.diseaseIncident != null && inc.category == IncidentCategoryDefOf.DiseaseHuman)
                            gameDiseases.Add(inc.diseaseIncident);
                }
                serious = gameDiseases.Contains(def) || (def.isBad && def.lethalSeverity > 0f && def.CompProps<HediffCompProperties_Immunizable>() != null);
            }
            seriousKinds[def] = serious;
            return serious;
        }

        /// <summary>Fighting a serious illness now (<see cref="IsSeriousIllness"/>), the game's or Rimfolk's, shown yet or not.</summary>
        public static bool SeriouslyIll(Pawn p)
        {
            var hs = p?.health?.hediffSet?.hediffs;
            if (hs == null) return false;
            for (int i = 0; i < hs.Count; i++)
                if (hs[i] != null && IsSeriousIllness(hs[i].def))
                    return true;
            return false;
        }

        /// <summary>
        /// For a new serious illness of Rimfolk's starting on them (not one passed on): with the budget on its chance is
        /// times <see cref="Readiness"/>, since most of them take a turn of the game's rather than add one, and times
        /// <see cref="IllnessBudgetMath.OverlapFactor"/> for someone already fighting a serious illness.
        /// </summary>
        public static float SeriousFactor(Pawn p) => On ? Readiness * IllnessBudgetMath.Overlap(SeriouslyIll(p)) : 1f;

        /// <summary>For a serious illness of Rimfolk's passed on between roommates: far less to someone already fighting one.</summary>
        public static float SpreadFactor(Pawn p) => On ? IllnessBudgetMath.Overlap(SeriouslyIll(p)) : 1f;

        /// <summary>
        /// How much more readily Rimfolk's serious illnesses come with the budget on, by the storyteller's disease interval
        /// (<see cref="IllnessBudgetMath.ReadinessAt"/>): for every new case and the catching clock's serious one. 1 with it off.
        /// </summary>
        public static float Readiness => On ? IllnessBudgetMath.ReadinessAt(Find.Storyteller?.difficulty?.diseaseIntervalFactor ?? 1f) : 1f;

        /// <summary>
        /// The catching clock's serious one, its victims picked: each already fighting a serious illness keeps their place
        /// only at the overlap factor, else someone well from the rest of <paramref name="pool"/> takes it (none left: one
        /// fewer). As picked with the budget off.
        /// </summary>
        public static List<Pawn> Spare(List<Pawn> chosen, IEnumerable<Pawn> pool)
        {
            if (!On || chosen == null || chosen.Count == 0) return chosen;
            List<Pawn> well = null;
            var kept = new List<Pawn>(chosen.Count);
            foreach (var p in chosen)
            {
                if (!SeriouslyIll(p) || Rand.Chance(IllnessBudgetMath.OverlapFactor))
                {
                    kept.Add(p);
                    continue;
                }
                well ??= (pool ?? Enumerable.Empty<Pawn>()).Where(o => o != null && !chosen.Contains(o) && !SeriouslyIll(o)).InRandomOrder().ToList();
                var other = well.FirstOrDefault(o => !kept.Contains(o));
                if (other != null) kept.Add(other);
            }
            return kept;
        }

        /// <summary>A serious case of Rimfolk's started (not one passed on): its home owes the game one skip.</summary>
        public static void Owe(Map map, HediffDef def, Pawn p)
        {
            if (!On || map == null || def == null) return;
            var b = LedgerOf(map, true);
            if (b == null) return;
            IllnessBudgetMath.Owe(b, Now, IllnessBudgetMath.Join(def.defName, p?.LabelShort));
        }

        private static void PutOff(Map map, IllnessLedger b, HediffDef def, Pawn p, BudgetRoom why, int now)
        {
            b.putOff++;
            if (!IllnessBudgetMath.SayPutOff(b, now)) return;
            b.lastPutOff = now;
            string illness = def != null ? AilmentTracker.NameOf(def) : "CC_IllBudget_AnIllness".Translate().Resolve();
            string key = (why == BudgetRoom.Waiting ? "CC_IllBudget_PutOff_Waiting" : "CC_IllBudget_PutOff_Share") + (p == null ? "Any" : "");
            string text;
            try
            {
                text = p != null
                    ? TextVariants.Vary(key).Translate(p.Named("PAWN"), illness.Named("ILLNESS")).Resolve()
                    : TextVariants.Vary(key).Translate(illness.Named("ILLNESS")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("illness budget: a case put off", e);
                return;
            }
            Record(map, b, text, now, p != null && p.Spawned ? new LookTargets(p) : null);
        }

        // ---------------- The game's side ----------------

        /// <summary>
        /// The game's own counted disease is about to come to a home that owes a skip: held back (true) three times in four,
        /// paying the oldest debt there; otherwise it comes, and <see cref="Came"/> lets the debt go. Only one that would
        /// really have come is asked: with no one it could take, it passes as it would have and the debt waits.
        /// </summary>
        public static bool TryHold(FiringIncident fi)
        {
            if (Which(fi) != BudgetFiring.Counts) return false;
            var map = (Map)fi.parms.target;
            var b = LedgerOf(map, false);
            if (b == null) return false;
            int now = Now;
            float keep = KeepDays(map);
            if (IllnessBudgetMath.Owing(b, now, keep) == 0) return false;
            if (!fi.def.Worker.CanFireNow(fi.parms)) return false;
            if (!IllnessBudgetMath.Hold(b, now, keep, Rand.Value, out string paidFor)) return false;
            Held(map, b, fi.def, paidFor, now);
            return true;
        }

        /// <summary>The game's own counted disease came to a home: a debt it didn't hold back for is let go, and Rimfolk's serious illnesses rest there a while.</summary>
        public static void Came(FiringIncident fi)
        {
            if (Which(fi) != BudgetFiring.Counts) return;
            var map = (Map)fi.parms.target;
            IllnessBudgetMath.GameCame(LedgerOf(map, true), Now, KeepDays(map));
        }

        private static void Held(Map map, IllnessLedger b, IncidentDef disease, string paidFor, int now)
        {
            IllnessBudgetMath.Split(paidFor, out string illnessName, out string who);
            var illnessDef = illnessName.NullOrEmpty() ? null : DefDatabase<HediffDef>.GetNamedSilentFail(illnessName);
            string illness = illnessDef != null ? AilmentTracker.NameOf(illnessDef) : "CC_IllBudget_AnIllness".Translate().Resolve();
            string sickness = disease?.diseaseIncident?.label ?? disease?.label ?? "";
            string text;
            try
            {
                text = who.NullOrEmpty()
                    ? TextVariants.Vary("CC_IllBudget_HeldAny").Translate(sickness.Named("DISEASE"), illness.Named("ILLNESS")).Resolve()
                    : TextVariants.Vary("CC_IllBudget_Held").Translate(sickness.Named("DISEASE"), illness.Named("ILLNESS"), who.Named("WHO")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("illness budget: a disease held back", e);
                return;
            }
            Record(map, b, text, now, null);
        }

        /// <summary>The council's records: a line in the colony's news, kept for the advisor's sickness line.</summary>
        private static void Record(Map map, IllnessLedger b, string text, int now, LookTargets look)
        {
            if (text.NullOrEmpty()) return;
            b.note = text.Length > 400 ? text.Substring(0, 400) : text;
            b.noteTick = now;
            Tell.Minor(NewsKind.Health, Importance.Notable, map, text, look, MessageTypeDefOf.NeutralEvent);
        }

        // ---------------- What the player reads ----------------

        /// <summary>
        /// For a serious illness's letter: the case counts against the game's own diseases (it owes its skip on that home, or
        /// one has paid for it). Null with the budget off, or for a case it never counted (given by hand, or by another mod).
        /// </summary>
        public static string LetterLine(Map map, HediffDef def, Pawn p)
        {
            if (!On || def == null) return null;
            var b = LedgerOf(map, false);
            return IllnessBudgetMath.Counted(b, IllnessBudgetMath.Join(def.defName, p?.LabelShort)) ? "CC_IllBudget_Letter".Translate().Resolve() : null;
        }

        /// <summary>For the advisor's sickness line: the council's last record on this home within a year, or null.</summary>
        public static string RecordLine(Map map)
        {
            if (!On) return null;
            var b = LedgerOf(map, false);
            if (b == null || b.note.NullOrEmpty() || !IllnessBudgetMath.Within(b.noteTick, Now, IllnessBudgetMath.YearDays)) return null;
            return b.note;
        }

        /// <summary>The budget on every home, for the dev tools.</summary>
        public static string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Rimfolk] The shared illness budget: setting " + (SettingOn ? "on" : "off") + ", serious illnesses " + (AilmentTracker.SeriousOn ? "on" : "off")
                          + ", hook " + (Hooked ? "in place" : "not in place") + "; the storyteller " + (StorytellerSends() ? "sends" : "doesn't send") + " the game's human diseases.");
            int now = Now;
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                float mean = GameMeanDays(map), keep = IllnessBudgetMath.KeepDays(mean);
                var b = LedgerOf(map, false);
                sb.Append("  ").Append(map.Parent?.LabelCap ?? map.ToString()).Append(": the game's own every ")
                    .Append(float.IsInfinity(mean) ? "never" : mean.ToString("0") + " days (" + IllnessBudgetMath.GamePerYear(mean).ToString("0.00") + " a year)")
                    .Append(", debts kept ").Append(keep.ToString("0")).Append(" days, held back ").Append(IllnessBudgetMath.HoldChance.ToString("P0"))
                    .Append(" of the time, in all at most ").Append((IllnessBudgetMath.MostInAll - 1f).ToString("P0")).Append(" above the game's own");
                if (b == null)
                {
                    sb.AppendLine("; nothing owed, nothing recorded.");
                    continue;
                }
                sb.Append("; owed ").Append(IllnessBudgetMath.Owing(b, now, keep));
                for (int i = 0; i < b.owed.Count; i++)
                    sb.Append(i == 0 ? " (" : ", ").Append(b.owedFor.Count > i ? b.owedFor[i] : "?").Append(" ").Append(((now - b.owed[i]) / (float)GenDate.TicksPerDay).ToString("0.0")).Append(" days ago").Append(i == b.owed.Count - 1 ? ")" : "");
                sb.Append("; the game's last ").Append(Ago(b.lastGame, now)).Append(", last held back ").Append(Ago(b.lastSkip, now))
                    .Append("; held back ").Append(b.held).Append(", came anyway ").Append(b.cameAnyway).Append(", put off ").Append(b.putOff)
                    .Append("; room now: ").Append(IllnessBudgetMath.Room(b, now, keep)).Append(" (chance ").Append(IllnessBudgetMath.Chance(b, now, keep).ToString("P0")).AppendLine(").");
                if (!b.note.NullOrEmpty()) sb.Append("    last record (").Append(Ago(b.noteTick, now)).Append("): ").AppendLine(b.note);
            }
            return sb.ToString().TrimEnd();
        }

        private static string Ago(int tick, int now) => tick == IllnessBudgetMath.Never ? "never" : ((now - tick) / (float)GenDate.TicksPerDay).ToString("0.0") + " days ago";
    }

    /// <summary>
    /// The shared illness budget's hook (IllnessBudget): the game's own human disease the storyteller aims at a home that
    /// owes a skip is held back three times in four, not fired; one that comes is noted (a debt let go, Rimfolk's serious
    /// illnesses resting there a while).
    /// The other prefixes on the storyteller's firing hold raids (Dealings/DealingPatches.cs, Peoples/PeoplesFPatches.cs)
    /// and the sky's conditions (Peoples/PeoplesEPatches.cs), never a disease; and a firing another prefix has already
    /// held is left to it (<c>__runOriginal</c>), so no debt is paid for a disease that wasn't going to come.
    /// </summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class Patch_IllnessBudget
    {
        public static bool Prefix(FiringIncident fi, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            if (fi?.def == null || !RimfolkPatches.On || !IllnessBudget.SettingOn || !AilmentTracker.SeriousOn) return true;
            try
            {
                if (!IllnessBudget.TryHold(fi)) return true;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("illness budget: holding a disease back", e);
                return true;
            }
        }

        public static void Postfix(FiringIncident fi, bool __result)
        {
            if (!__result || fi?.def == null || !RimfolkPatches.On || !IllnessBudget.SettingOn || !AilmentTracker.SeriousOn) return;
            try
            {
                IllnessBudget.Came(fi);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("illness budget: a disease that came", e);
            }
        }
    }
}
