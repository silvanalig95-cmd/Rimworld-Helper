using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using LudeonTK;
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
    /// The shared illness budget, the game's side (the rules are IllnessBudgetMath): Rimfolk's serious illnesses take a share
    /// of the game's own disease rate instead of adding to it. A serious case of Rimfolk's that starts on a home owes a skip,
    /// and the game's next human disease the storyteller aims there is held back (<see cref="Patch_IllnessBudget"/>); once
    /// the game's own disease has come, Rimfolk's wait their gap there; and Rimfolk takes at most about half the cases. The
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
        /// Whether a new serious illness of Rimfolk's may start on this home now, within the budget; when it may not, the
        /// case is put off (counted, and now and then said). Asked once its own chance has come up and the colony's own room
        /// (one kind at a time, none within thirty days of the last) is open.
        /// </summary>
        public static bool Allows(Map map, HediffDef def, Pawn p)
        {
            if (!On || map == null) return true;
            var b = LedgerOf(map, false);
            if (b == null) return true;
            int now = Now;
            var room = IllnessBudgetMath.Room(b, now, KeepDays(map));
            if (room == BudgetRoom.Open) return true;
            PutOff(map, b, def, p, room, now);
            return false;
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
        /// The game's own counted disease is about to come to a home that owes a skip: held back (true), paying the oldest
        /// debt there. Only one that would really have come is held: with no one it could take, it passes as it would have.
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
            if (!IllnessBudgetMath.Hold(b, now, keep, out string paidFor)) return false;
            Held(map, b, fi.def, paidFor, now);
            return true;
        }

        /// <summary>The game's own counted disease came to a home: Rimfolk's serious illnesses wait their gap there.</summary>
        public static void Came(FiringIncident fi)
        {
            if (Which(fi) != BudgetFiring.Counts) return;
            IllnessBudgetMath.GameCame(LedgerOf((Map)fi.parms.target, true), Now);
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
                    .Append(", debts kept ").Append(keep.ToString("0")).Append(" days, Rimfolk's share at most ").Append(IllnessBudgetMath.MaxShare(mean).ToString("P0"));
                if (b == null)
                {
                    sb.AppendLine("; nothing owed, nothing recorded.");
                    continue;
                }
                sb.Append("; owed ").Append(IllnessBudgetMath.Owing(b, now, keep));
                for (int i = 0; i < b.owed.Count; i++)
                    sb.Append(i == 0 ? " (" : ", ").Append(b.owedFor.Count > i ? b.owedFor[i] : "?").Append(" ").Append(((now - b.owed[i]) / (float)GenDate.TicksPerDay).ToString("0.0")).Append(" days ago").Append(i == b.owed.Count - 1 ? ")" : "");
                sb.Append("; the game's last ").Append(Ago(b.lastGame, now)).Append(", last held back ").Append(Ago(b.lastSkip, now))
                    .Append("; held back ").Append(b.held).Append(", put off ").Append(b.putOff).Append("; room now: ").Append(IllnessBudgetMath.Room(b, now, keep)).AppendLine(".");
                if (!b.note.NullOrEmpty()) sb.Append("    last record (").Append(Ago(b.noteTick, now)).Append("): ").AppendLine(b.note);
            }
            return sb.ToString().TrimEnd();
        }

        private static string Ago(int tick, int now) => tick == IllnessBudgetMath.Never ? "never" : ((now - tick) / (float)GenDate.TicksPerDay).ToString("0.0") + " days ago";
    }

    /// <summary>
    /// The shared illness budget's hook (IllnessBudget): the game's own human disease the storyteller aims at a home that
    /// owes a skip is held back, not fired; one that comes is noted, so Rimfolk's serious illnesses wait their gap there.
    /// </summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class Patch_IllnessBudget
    {
        public static bool Prefix(FiringIncident fi, ref bool __result)
        {
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

    /// <summary>Dev mode (Debug actions, "Rimfolk"): a serious illness of Rimfolk's, the game's own disease as the storyteller sends it, and the budget's log.</summary>
    public static class IllnessBudgetDebug
    {
        [DebugAction("Rimfolk", "Illness budget: one of Rimfolk's serious illnesses now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RimfolkSerious()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            var options = AilmentTracker.Incidents.Where(d => AilmentTracker.InfoOf(d.diseaseIncident)?.serious ?? false).ToList();
            foreach (var def in options.InRandomOrder())
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                parms.forced = true;
                if (!def.Worker.CanFireNow(parms)) continue;
                if (Find.Storyteller.TryFire(new FiringIncident(def, null, parms)))
                {
                    Messages.Message("Rimfolk's " + def.diseaseIncident.label + " now: this home owes the game one skip.", MessageTypeDefOf.NeutralEvent, false);
                    return;
                }
            }
            Messages.Message("None of Rimfolk's serious illnesses can start here now (nobody it could take).", MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("Rimfolk", "Illness budget: the game's own disease, as the storyteller sends it", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GameDisease()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            var comp = Find.Storyteller?.storytellerComps?.FirstOrDefault(c => c?.props is StorytellerCompProperties_Disease p && p.category == IncidentCategoryDefOf.DiseaseHuman);
            if (comp == null)
            {
                Messages.Message("This storyteller sends none of the game's human diseases.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.DiseaseHuman, map);
            var usable = DefDatabase<IncidentDef>.AllDefsListForReading
                .Where(d => d.category == IncidentCategoryDefOf.DiseaseHuman && !(d.Worker is IncidentWorker_Ailment) && map.Biome.CommonalityOfDisease(d) > 0f && d.Worker.CanFireNow(parms))
                .ToList();
            if (!usable.TryRandomElementByWeight(d => map.Biome.CommonalityOfDisease(d), out var def))
            {
                Messages.Message("None of the game's diseases of this land can come now.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            int held = IllnessBudget.LedgerOf(map, false)?.held ?? 0;
            bool fired = Find.Storyteller.TryFire(new FiringIncident(def, comp, parms));
            bool wasHeld = (IllnessBudget.LedgerOf(map, false)?.held ?? 0) > held;
            Messages.Message("The game's own " + def.label + ": " + (wasHeld ? "held back by the budget." : fired ? "it came." : "it couldn't come."), MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Rimfolk", "Illness budget: the ledger of every home (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Log_() => Log.Message(IllnessBudget.Report());
    }
}
