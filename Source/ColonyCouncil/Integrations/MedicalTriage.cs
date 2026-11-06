using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Medical triage with Move the Patient (the player's request, 6 November 2026: "i want them to always take the best
    /// bed, and once a patient with a more serious injury arrives, the previous pawn should be put into another bed via
    /// 'transfer to bed'"). Read by name (types, fields, its job def), so the mod isn't needed; without it nothing here
    /// is patched or runs.
    /// <list type="bullet">
    /// <item>Best bed first: a patient going to a medical bed takes the best free one they may use (the mod's priority,
    /// then the game's medical quality, then the nearest; <see cref="MedicalTriageMath"/>), after the mod's own choice
    /// and only among beds the game and the mod would allow. A surgery bed counts for an ordinary patient only while
    /// nobody waiting for an operation could use it. The mod keeps every patient on its reservation scheme otherwise:
    /// a lighter case on the lowest bed that fits, the High and Critical beds kept free, a surgery bed never used for
    /// anyone else while another is free.</item>
    /// <item>Making room: when someone more serious (the mod's own severity), or someone waiting for an operation when
    /// the surgery bed holds an ordinary patient, needs a better bed than any free one, the lighter patient is carried
    /// to the best other free medical bed with the mod's own transfer job by the nearest colonist who does doctoring,
    /// and the serious one is brought in (the game's rescue, the mod's transfer, or on their own feet). One move at a
    /// time per map, nobody moved twice within four hours, never someone under the knife, being tended, in a mental
    /// state or in labour, never into a bed of the other kind, never when no other bed is free.</item>
    /// <item>Shown: the carrier says why as they set off, the colony news tells it once the lighter one lies in their new
    /// bed, and the bed's and the moved patient's inspect lines say why.</item>
    /// </list>
    /// The "medicalTriage" setting and the deeper game hooks switch it off; the mod's "by distance" mode stands it down.
    /// Nothing is saved: a load forgets a move under way (the carry itself goes on) and who was moved lately.
    /// </summary>
    public static class MedicalTriage
    {
        public const string PackageId = "konstantynopolitaneczka.movethepatient";
        /// <summary>The mod's Harmony id: Rimfolk's bed choice runs after its own.</summary>
        public const string HarmonyId = "patientbedtransfer.mod";

        private const string Ns = "PatientBedTransfer.";

        // ================= The mod, read by name =================

        private static bool looked, ready;
        private static FieldInfo settingsField, priorityField, modeField;
        private static MethodInfo bedPriority, severity;
        private static Func<Building_Bed, bool> isSurgeryBed;
        private static Func<Pawn, bool> pendingSurgery;
        private static Func<Pawn, Building_Bed, bool> xenoRestricted, bedValidFor;
        private static Func<int, bool> transferActive;
        private static Action<int> markTransfer;
        private static Func<Pawn, bool, bool> canTransfer;
        private static Func<bool> selectorBusy;
        private static JobDef transferJob;
        private static readonly object[] one = new object[1];

        /// <summary>Move the Patient is here and every part of it triage uses was found.</summary>
        public static bool Ready
        {
            get
            {
                Look();
                return ready;
            }
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                var mod = GenTypes.GetTypeInAnyAssembly(Ns + "PatientBedTransferMod");
                if (mod == null) return;
                var settingsType = GenTypes.GetTypeInAnyAssembly(Ns + "PatientBedTransferSettings");
                var triage = GenTypes.GetTypeInAnyAssembly(Ns + "PatientTriage");
                var beds = GenTypes.GetTypeInAnyAssembly(Ns + "BedUtility");
                var selector = GenTypes.GetTypeInAnyAssembly(Ns + "MedicalBedSelector");
                var transfer = GenTypes.GetTypeInAnyAssembly(Ns + "PatientTransferUtility");
                settingsField = AccessTools.Field(mod, "settings");
                if (settingsType != null)
                {
                    priorityField = AccessTools.Field(settingsType, "enablePrioritySystem");
                    modeField = AccessTools.Field(settingsType, "priorityMode");
                }
                if (triage != null)
                {
                    bedPriority = AccessTools.Method(triage, "GetBedPriority", new[] { typeof(Building_Bed) });
                    severity = AccessTools.Method(triage, "CalculatePatientSeverity", new[] { typeof(Pawn) });
                }
                isSurgeryBed = Static<Func<Building_Bed, bool>>(beds, "IsSurgeryBed", typeof(Building_Bed));
                pendingSurgery = Static<Func<Pawn, bool>>(beds, "HasPendingSurgeries", typeof(Pawn));
                xenoRestricted = Static<Func<Pawn, Building_Bed, bool>>(beds, "IsXenotypeRestricted", typeof(Pawn), typeof(Building_Bed));
                bedValidFor = Static<Func<Pawn, Building_Bed, bool>>(beds, "IsBedValidForPawn", typeof(Pawn), typeof(Building_Bed));
                transferActive = Static<Func<int, bool>>(beds, "IsTransferActive", typeof(int));
                markTransfer = Static<Action<int>>(beds, "MarkTransferActive", typeof(int));
                canTransfer = Static<Func<Pawn, bool, bool>>(transfer, "CanPawnPerformTransfer", typeof(Pawn), typeof(bool));
                var busy = selector != null ? AccessTools.PropertyGetter(selector, "Busy") : null;
                if (busy != null && busy.IsStatic) selectorBusy = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), busy);
                transferJob = DefDatabase<JobDef>.GetNamedSilentFail("TransferPatientToBed");
                ready = settingsField != null && priorityField != null && modeField != null && bedPriority != null && severity != null && isSurgeryBed != null
                        && pendingSurgery != null && xenoRestricted != null && bedValidFor != null && transferActive != null && markTransfer != null
                        && canTransfer != null && selectorBusy != null && transferJob != null;
                if (!ready) Log.Warning("[Rimfolk] Move the Patient is loaded but isn't as Rimfolk knows it, so medical triage (best bed first, making room) is off.");
            }
            catch (Exception e)
            {
                ready = false;
                Log.Warning("[Rimfolk] Couldn't read Move the Patient, so medical triage is off: " + e.Message);
            }
        }

        private static T Static<T>(Type type, string name, params Type[] args) where T : Delegate
        {
            var m = type != null ? AccessTools.Method(type, name, args) : null;
            if (m == null || !m.IsStatic) return null;
            try
            {
                return (T)Delegate.CreateDelegate(typeof(T), m);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The mod's settings as triage reads them, once a tick.</summary>
        private struct ModSettings
        {
            public bool found;
            /// <summary>Its priority system is on: the player's ranks count.</summary>
            public bool priority;
            /// <summary>Its priority system is on and set to pick by distance: the player chose the nearest bed, so triage stands down.</summary>
            public bool byDistance;
        }

        private static ModSettings modSettings;
        private static int modSettingsTick = -1;

        private static ModSettings Mod
        {
            get
            {
                int now = Now;
                if (now == modSettingsTick) return modSettings;
                modSettingsTick = now;
                modSettings = default;
                try
                {
                    var s = settingsField?.GetValue(null);
                    if (s != null)
                    {
                        modSettings.found = true;
                        modSettings.priority = (bool)priorityField.GetValue(s);
                        // PriorityMode: ByPriority = 0, ByDistance = 1.
                        modSettings.byDistance = modSettings.priority && Convert.ToInt32(modeField.GetValue(s)) == 1;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("medical triage: the mod's settings", e);
                }
                return modSettings;
            }
        }

        /// <summary>Triage may act now: the mod is here and as expected, the setting and the deeper hooks are on, the mod isn't set to the nearest bed.</summary>
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.medicalTriage ?? true) && Ready && Mod.found && !Mod.byDistance;

        private static int Now => Current.Game?.tickManager?.TicksGame ?? 0;

        /// <summary>The mod's severity for a patient: 1 Low to 4 Critical.</summary>
        private static int Severity(Pawn p)
        {
            try
            {
                one[0] = p;
                return Convert.ToInt32(severity.Invoke(null, one));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medical triage: a patient's severity", e);
                return MedicalTriageMath.Normal;
            }
            finally
            {
                one[0] = null;
            }
        }

        /// <summary>The player's rank for a bed with the mod's priority: 1 Low to 4 Critical, Normal while its priority system is off.</summary>
        private static int Priority(Building_Bed b)
        {
            if (!Mod.priority) return MedicalTriageMath.Normal;
            try
            {
                one[0] = b;
                return Convert.ToInt32(bedPriority.Invoke(null, one));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medical triage: a bed's priority", e);
                return MedicalTriageMath.Normal;
            }
            finally
            {
                one[0] = null;
            }
        }

        private static bool SurgeryBed(Building_Bed b) => b != null && isSurgeryBed(b);

        private static bool WaitsForOperation(Pawn p) => p != null && pendingSurgery(p);

        // ================= What triage remembers (not saved) =================

        /// <summary>Someone being moved out so someone worse off can have their bed.</summary>
        private sealed class Move
        {
            public Map map;
            public Pawn lighter, worse, carrier, carrierIn;
            /// <summary>The bed freed for the worse one, and the bed the lighter one is carried to.</summary>
            public Building_Bed best, other;
            /// <summary>The room is made for an operation.</summary>
            public bool operation;
            public int started;
            /// <summary>The lighter one lies in their new bed; now the worse one comes in.</summary>
            public bool lighterOut;
        }

        /// <summary>The move under way on each map (by its uniqueID): one at a time.</summary>
        private static readonly Dictionary<int, Move> moves = new Dictionary<int, Move>();
        /// <summary>When triage last moved each patient (thingIDNumber).</summary>
        private static readonly Dictionary<int, int> lastMoved = new Dictionary<int, int>();
        /// <summary>Until when each map is looked at every 250 ticks: a patient went to a lesser bed than the best.</summary>
        private static readonly Dictionary<int, int> soon = new Dictionary<int, int>();
        /// <summary>A bed's or a patient's inspect line, and until when it shows.</summary>
        private static readonly Dictionary<int, (string line, int until)> lines = new Dictionary<int, (string, int)>();
        private static Game game;

        /// <summary>A new game or a load starts with nothing remembered.</summary>
        public static void Reset()
        {
            game = Current.Game;
            moves.Clear();
            lastMoved.Clear();
            soon.Clear();
            lines.Clear();
            opCache.Clear();
            bedsMap = null;
        }

        private static void Fresh()
        {
            if (Current.Game != game) Reset();
        }

        /// <summary>The 250-tick pass has something to follow: a move under way, or a map to look at soon.</summary>
        public static bool Waiting
        {
            get
            {
                Fresh();
                return moves.Count > 0 || soon.Count > 0;
            }
        }

        /// <summary>The patient a bed is kept for while a move frees it, or null.</summary>
        private static Pawn KeptFor(Building_Bed b)
        {
            if (moves.Count == 0) return null;
            foreach (var m in moves.Values)
                if (m.best == b) return m.worse;
            return null;
        }

        // ================= The beds =================

        private static readonly List<Building_Bed> bedsBuf = new List<Building_Bed>();
        private static Map bedsMap;
        private static int bedsTick = -1;

        /// <summary>The colony's medical beds for people on a map, worked out at most once a tick.</summary>
        private static List<Building_Bed> MedicalBeds(Map map)
        {
            int now = Now;
            if (bedsMap == map && bedsTick == now) return bedsBuf;
            bedsMap = map;
            bedsTick = now;
            bedsBuf.Clear();
            var things = map.listerThings.ThingsInGroup(ThingRequestGroup.Bed);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Building_Bed b && b.Spawned && b.Medical && b.Faction == Faction.OfPlayer && b.def.building != null && b.def.building.bed_humanlike)
                    bedsBuf.Add(b);
            return bedsBuf;
        }

        /// <summary>How well the game treats someone in this bed (MedicalTriageMath.Quality).</summary>
        private static float Quality(Building_Bed b)
        {
            float tend = b.GetStatValue(StatDefOf.MedicalTendQualityOffset);
            float immunity = b.GetStatValue(StatDefOf.ImmunityGainSpeedFactor);
            var room = b.GetRoom();
            float infection = room != null ? room.GetStat(RoomStatDefOf.InfectionChanceFactor) : 1f;
            return MedicalTriageMath.Quality(tend, immunity, infection);
        }

        private static MedicalTriageMath.Rank RankOf(Building_Bed b, IntVec3 from) =>
            new MedicalTriageMath.Rank(Priority(b), Quality(b), (b.Position - from).LengthHorizontalSquared);

        /// <summary>Who on a map waits for an operation and isn't in a surgery bed, looked at once a second at most.</summary>
        private static readonly Dictionary<int, (int tick, List<Pawn> who)> opCache = new Dictionary<int, (int, List<Pawn>)>();

        private static List<Pawn> OperationsWaiting(Map map)
        {
            int now = Now;
            if (opCache.TryGetValue(map.uniqueID, out var c) && now >= c.tick && now - c.tick < 60) return c.who;
            var who = c.who ?? new List<Pawn>();
            who.Clear();
            var all = map.mapPawns.AllHumanlikeSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p.Dead || !Ours(p)) continue;
                if (p.InBed() && SurgeryBed(p.CurrentBed())) continue;
                if (WaitsForOperation(p)) who.Add(p);
            }
            opCache[map.uniqueID] = (now, who);
            return who;
        }

        /// <summary>Someone waiting for an operation could use this surgery bed (not counting <paramref name="except"/>).</summary>
        private static bool OperationNeeds(Building_Bed b, Pawn except)
        {
            var who = OperationsWaiting(b.Map);
            for (int i = 0; i < who.Count; i++)
                if (who[i] != except && Kind(who[i], b) && bedValidFor(who[i], b)) return true;
            return false;
        }

        /// <summary>The surgery bed rule: a surgery bed counts for this patient (always when it isn't one).</summary>
        private static bool SurgeryOpen(Building_Bed b, Pawn p, bool forOperation) =>
            !SurgeryBed(b) || MedicalTriageMath.SurgeryBedOpen(forOperation, OperationNeeds(b, p));

        /// <summary>A bed of the patient's own kind (MedicalTriageMath.SameKind).</summary>
        private static bool Kind(Pawn p, Building_Bed b) => MedicalTriageMath.SameKind(p.IsPrisonerOfColony, p.IsSlaveOfColony, b.ForPrisoners, b.ForSlaves);

        /// <summary>The colony looks after them: humanlike, not a baby, not an enemy unless held as a prisoner.</summary>
        private static bool Ours(Pawn p) =>
            p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby() && (p.IsPrisonerOfColony || !p.HostileTo(Faction.OfPlayer));

        private static bool PrefersSlabBed(Pawn p)
        {
            var precepts = p.Ideo?.PreceptsListForReading;
            if (precepts == null) return false;
            for (int i = 0; i < precepts.Count; i++)
                if (precepts[i].def.prefersSlabBed) return true;
            return false;
        }

        // ================= Best bed first =================

        private static bool choosing;

        /// <summary>
        /// The bed the game and the mod chose for a patient (<paramref name="chosen"/>, a medical bed), made the best free
        /// one they may use: each candidate passes the same IsValidBedFor the call asked (forbidden, its owner type, a
        /// prisoner's bed, reachable, reservable), the mod's xenotype rule, and is no more dangerous than the chosen one.
        /// Someone already lying in a medical bed that counts keeps it (making room moves people, not this). One waiting
        /// for an operation keeps the surgery bed the mod gave them. A bed kept for someone a move frees it for is theirs.
        /// </summary>
        public static Building_Bed BestFirst(Pawn sleeper, Pawn traveler, Building_Bed chosen, bool social, bool allowNoCare, bool ignoreReservations, GuestStatus? guest)
        {
            if (choosing || sleeper == null || chosen == null || !chosen.Medical) return chosen;
            choosing = true;
            try
            {
                if (!On || selectorBusy()) return chosen;
                var map = sleeper.MapHeld;
                if (map == null || chosen.Map != map || !Ours(sleeper) || PrefersSlabBed(sleeper)) return chosen;
                Fresh();
                bool operation = WaitsForOperation(sleeper);
                if (operation && SurgeryBed(chosen)) return chosen;
                var beds = MedicalBeds(map);
                if (beds.Count < 2) return chosen;
                traveler ??= sleeper;
                Danger limit = chosen.Position.GetDangerFor(sleeper, map);
                IntVec3 from = sleeper.PositionHeld;

                bool Counts(Building_Bed b)
                {
                    var kept = KeptFor(b);
                    if (kept != null && kept != sleeper) return false;
                    if (!SurgeryOpen(b, sleeper, operation)) return false;
                    if (b == chosen) return true;
                    if (!b.Spawned || b.Map != map || (int)b.Position.GetDangerFor(sleeper, map) > (int)limit) return false;
                    if (xenoRestricted(sleeper, b)) return false;
                    return RestUtility.IsValidBedFor(b, sleeper, traveler, social, allowNoCare, ignoreReservations, guest);
                }

                var cur = sleeper.CurrentBed();
                if (cur != null && cur.Medical && cur.Map == map && Counts(cur)) return cur;

                Building_Bed best = null;
                var bestRank = default(MedicalTriageMath.Rank);
                var top = default(MedicalTriageMath.Rank);
                bool anyTop = false;
                for (int i = 0; i < beds.Count; i++)
                {
                    var b = beds[i];
                    var rank = RankOf(b, from);
                    if (Kind(sleeper, b) && (!anyTop || MedicalTriageMath.Better(rank, top)))
                    {
                        top = rank;
                        anyTop = true;
                    }
                    if (!Counts(b)) continue;
                    if (best == null || MedicalTriageMath.Compare(rank, bestRank) < 0)
                    {
                        best = b;
                        bestRank = rank;
                    }
                }
                if (best == null) return chosen;
                // A better bed of their kind is taken: look at the beds again soon, someone may be moved for them.
                if (anyTop && MedicalTriageMath.Better(top, bestRank)) soon[map.uniqueID] = Now + MedicalTriageMath.SoonTicks;
                return best;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medical triage: the best bed", e);
                return chosen;
            }
            finally
            {
                choosing = false;
            }
        }

        // ================= Making room =================

        /// <summary>A patient as the pass sees them: where they lie (null on the floor), how bad, waiting for an operation.</summary>
        private struct Patient
        {
            public Pawn pawn;
            public Building_Bed bed;
            public int severity;
            public bool operation;
        }

        private static readonly List<Patient> patients = new List<Patient>();
        private static readonly List<Building_Bed> passBeds = new List<Building_Bed>();
        private static readonly List<(Building_Bed bed, bool surgery, MedicalTriageMath.Rank rank)> ranked = new List<(Building_Bed, bool, MedicalTriageMath.Rank)>();

        /// <summary>
        /// The pass: on the hour every home map, otherwise the maps where a patient lately went to a lesser bed, and the
        /// moves under way.
        /// </summary>
        public static void Pass(int now, bool hour)
        {
            Fresh();
            if (!On) return;
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                int id = map.uniqueID;
                if (moves.TryGetValue(id, out var m))
                {
                    Follow(m, now);
                    continue;
                }
                bool due = soon.TryGetValue(id, out int until) && now <= until;
                if (!due) soon.Remove(id);
                if (!map.IsPlayerHome || (!hour && !due)) continue;
                MakeRoom(map, now);
            }
            // Maps gone (a caravan's camp packed up): their moves and looks go with them.
            if (moves.Count > 0 || soon.Count > 0)
            {
                var gone = new List<int>();
                foreach (var id in moves.Keys)
                    if (Find.Maps.Find(x => x.uniqueID == id) == null) gone.Add(id);
                foreach (var id in soon.Keys)
                    if (now > soon[id] || Find.Maps.Find(x => x.uniqueID == id) == null) gone.Add(id);
                foreach (var id in gone)
                {
                    if (moves.TryGetValue(id, out var m)) End(m);
                    soon.Remove(id);
                }
            }
            if (lastMoved.Count > 40) Caps.DropOlderThan(lastMoved, now, MedicalTriageMath.CooldownTicks);
            if (lines.Count > 0)
            {
                var old = new List<int>();
                foreach (var kv in lines)
                    if (now >= kv.Value.until) old.Add(kv.Key);
                foreach (var id in old) lines.Remove(id);
            }
        }

        /// <summary>Looks for one patient worse off than someone in a better bed, and if one is found, starts the move.</summary>
        private static void MakeRoom(Map map, int now)
        {
            passBeds.Clear();
            passBeds.AddRange(MedicalBeds(map));
            if (passBeds.Count < 2) return;
            patients.Clear();
            for (int i = 0; i < passBeds.Count; i++)
                foreach (var p in passBeds[i].CurOccupants)
                    if (p != null && Ours(p))
                        patients.Add(new Patient { pawn = p, bed = passBeds[i] });
            if (patients.Count == 0) return;
            var all = map.mapPawns.AllHumanlikeSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p.Downed && !p.Dead && !p.InBed() && Ours(p) && HealthAIUtility.ShouldSeekMedicalRest(p))
                    patients.Add(new Patient { pawn = p });
            }
            if (patients.Count < 2) return;
            for (int i = 0; i < patients.Count; i++)
            {
                var pt = patients[i];
                pt.severity = Severity(pt.pawn);
                pt.operation = WaitsForOperation(pt.pawn);
                patients[i] = pt;
            }
            // The worst off first; among equals, one waiting for an operation.
            patients.Sort((a, b) => a.severity != b.severity ? b.severity.CompareTo(a.severity) : b.operation.CompareTo(a.operation));
            for (int w = 0; w < patients.Count; w++)
                if (TryFor(patients[w], map, now)) return;
        }

        /// <summary>The beds the patient could have, if it were free: of their kind, the mod's xenotype rule, safe, the surgery bed rule.</summary>
        private static bool MayHave(Pawn p, Building_Bed b, bool operation)
        {
            if (!Kind(p, b) || !bedValidFor(p, b) || xenoRestricted(p, b)) return false;
            if (!RestUtility.CanUseBedEver(p, b.def)) return false;
            if (b.Position.GetDangerFor(p, b.Map) != Danger.None) return false;
            var kept = KeptFor(b);
            if (kept != null && kept != p) return false;
            return SurgeryOpen(b, p, operation);
        }

        /// <summary>A free bed the patient may be put in now (the game's own CanUseBedNow, as the mod's job tucks them in).</summary>
        private static bool FreeFor(Pawn p, Building_Bed b, bool operation) =>
            b.AnyUnoccupiedSleepingSlot && MayHave(p, b, operation) && RestUtility.CanUseBedNow(b, p, false);

        private static bool TryFor(Patient worse, Map map, int now)
        {
            var w = worse.pawn;
            if (!HealthAIUtility.ShouldEverReceiveMedicalCareFromPlayer(w) || !HealthAIUtility.ShouldSeekMedicalRest(w)) return false;
            if (w.InMentalState || transferActive(w.thingIDNumber) || Bringing(w)) return false;
            if (worse.bed != null && MedicalTriageMath.Recently(LastMoved(w), now)) return false;
            IntVec3 from = w.PositionHeld;
            bool op = worse.operation;
            // The beds as this patient ranks them, best first (a surgery bed first for an operation).
            ranked.Clear();
            for (int i = 0; i < passBeds.Count; i++)
                ranked.Add((passBeds[i], SurgeryBed(passBeds[i]), RankOf(passBeds[i], from)));
            ranked.Sort((a, b) => MedicalTriageMath.CompareFor(op, a.surgery, a.rank, b.surgery, b.rank));
            // What they have now, or the best free bed the game would carry them to.
            int have = -1;
            for (int i = 0; i < ranked.Count && have < 0; i++)
                if (worse.bed != null ? ranked[i].bed == worse.bed : FreeFor(w, ranked[i].bed, op))
                    have = i;
            for (int i = 0; i < ranked.Count; i++)
            {
                var (best, surgery, rank) = ranked[i];
                if (have >= 0 && !MedicalTriageMath.BetterFor(op, surgery, rank, ranked[have].surgery, ranked[have].rank)) break;
                if (best == worse.bed || !MayHave(w, best, op)) continue;
                for (int j = 0; j < patients.Count; j++)
                {
                    var lighter = patients[j];
                    if (lighter.bed != best || lighter.pawn == w) continue;
                    if (!MedicalTriageMath.Outranks(worse.severity, worse.operation, lighter.severity, lighter.operation, surgery)) continue;
                    var other = OtherBed(lighter, best, out bool anyFree);
                    var hold = HoldOf(lighter.pawn, anyFree, other != null, now);
                    if (hold != MedicalTriageMath.Hold.None) continue;
                    var carrier = Carrier(lighter.pawn, other, w);
                    if (carrier == null) continue;
                    if (Start(map, worse, lighter, best, other, carrier, surgery && worse.operation && !lighter.operation, now)) return true;
                }
            }
            return false;
        }

        /// <summary>The best other free medical bed for someone moved out, and whether any other medical bed is free at all.</summary>
        private static Building_Bed OtherBed(Patient lighter, Building_Bed best, out bool anyFree)
        {
            anyFree = false;
            Building_Bed found = null;
            var foundRank = default(MedicalTriageMath.Rank);
            var p = lighter.pawn;
            for (int i = 0; i < passBeds.Count; i++)
            {
                var b = passBeds[i];
                if (b == best || !b.AnyUnoccupiedSleepingSlot || KeptFor(b) != null) continue;
                anyFree = true;
                if (!FreeFor(p, b, lighter.operation)) continue;
                var r = RankOf(b, p.PositionHeld);
                if (found == null || MedicalTriageMath.Compare(r, foundRank) < 0)
                {
                    found = b;
                    foundRank = r;
                }
            }
            return found;
        }

        /// <summary>What holds someone in their bed (MedicalTriageMath.MayMove).</summary>
        private static MedicalTriageMath.Hold HoldOf(Pawn p, bool anyFree, bool ofKind, int now)
        {
            Attended(p, out bool operating, out bool tended);
            bool labor = p.health?.hediffSet?.InLabor() ?? false;
            bool carried = p.CarriedBy != null || !p.Spawned || transferActive(p.thingIDNumber) || Bringing(p);
            return MedicalTriageMath.MayMove(operating, tended, p.InMentalState, labor, carried, anyFree, ofKind, LastMoved(p), now);
        }

        private static int LastMoved(Pawn p) => lastMoved.TryGetValue(p.thingIDNumber, out int t) ? t : -1;

        /// <summary>Someone is operating on them, or tending them (or on the way to with medicine).</summary>
        private static void Attended(Pawn patient, out bool operating, out bool tended)
        {
            operating = tended = false;
            var map = patient.MapHeld;
            if (map == null) return;
            var ours = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < ours.Count; i++)
            {
                var job = ours[i].CurJob;
                if (job == null || job.targetA.Thing != patient) continue;
                if (job.def == JobDefOf.TendPatient) tended = true;
                else if (job.def == JobDefOf.DoBill) operating = true;
            }
        }

        /// <summary>Someone is already carrying them to a bed or on the way to: a rescue, a capture, a transfer.</summary>
        private static bool Bringing(Pawn patient)
        {
            var map = patient.MapHeld;
            if (map == null) return false;
            var ours = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < ours.Count; i++)
            {
                var job = ours[i].CurJob;
                if (job == null || job.targetA.Thing != patient) continue;
                var d = job.def;
                if (d == transferJob || d == JobDefOf.Rescue || d == JobDefOf.Capture || d == JobDefOf.TakeWoundedPrisonerToBed
                    || d.defName.Contains("TakeToBed") || d.defName.Contains("Rescue"))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The nearest colonist who does doctoring, awake and free to carry a patient (the mod's own test of who can, its
        /// strict one), who can reach both and reserve both. Never someone drafted, in a ritual or a party, at an
        /// operation, tending, rescuing, firefighting, carrying something, or on the player's own order.
        /// </summary>
        private static Pawn Carrier(Pawn patient, Building_Bed bed, Pawn not)
        {
            Pawn best = null;
            int bestDist = int.MaxValue;
            var all = patient.Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == patient || c == not || c.Downed || c.Dead || c.Drafted || c.InMentalState || !c.Awake() || c.InBed()) continue;
                if (c.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) || c.workSettings == null || !c.workSettings.WorkIsActive(WorkTypeDefOf.Doctor)) continue;
                if (Busy(c) || !canTransfer(c, true)) continue;
                int d = (c.Position - patient.Position).LengthHorizontalSquared;
                if (d >= bestDist) continue;
                if (!c.CanReach(patient, PathEndMode.ClosestTouch, Danger.Some) || !c.CanReach(bed, PathEndMode.Touch, Danger.Some)) continue;
                if (!c.CanReserve(patient) || !c.CanReserve(bed, bed.SleepingSlotsCount, 0)) continue;
                best = c;
                bestDist = d;
            }
            return best;
        }

        private static bool Busy(Pawn c)
        {
            if (c.GetLord() != null || c.carryTracker?.CarriedThing != null) return true;
            var job = c.CurJob;
            if (job == null) return false;
            if (job.playerForced || !job.def.casualInterruptible) return true;
            var d = job.def;
            return d == transferJob || d == JobDefOf.TendPatient || d == JobDefOf.Rescue || d == JobDefOf.Capture || d == JobDefOf.Arrest
                   || d == JobDefOf.BeatFire || d == JobDefOf.ExtinguishSelf || d == JobDefOf.TakeWoundedPrisonerToBed || d == JobDefOf.FeedPatient
                   || d == JobDefOf.DoBill && job.targetA.Thing is Pawn;
        }

        /// <summary>The mod's own transfer, as its "Transfer to bed" gives it: the patient to the bed, by this colonist.</summary>
        private static bool Transfer(Pawn carrier, Pawn patient, Building_Bed bed)
        {
            var job = JobMaker.MakeJob(transferJob, patient, bed);
            job.count = 1;
            job.playerForced = true;
            carrier.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, null, JobTag.Misc);
            if (carrier.CurJob != job) return false;
            markTransfer(patient.thingIDNumber);
            return true;
        }

        private static bool Start(Map map, Patient worse, Patient lighter, Building_Bed best, Building_Bed other, Pawn carrier, bool operation, int now)
        {
            if (!Transfer(carrier, lighter.pawn, other)) return false;
            var m = new Move
            {
                map = map, worse = worse.pawn, lighter = lighter.pawn, carrier = carrier, best = best, other = other, operation = operation,
                started = now
            };
            moves[map.uniqueID] = m;
            lastMoved[lighter.pawn.thingIDNumber] = now;
            soon.Remove(map.uniqueID);
            lines[best.thingIDNumber] = ((operation ? "CC_Triage_KeptOp" : "CC_Triage_Kept").Translate(worse.pawn.Named("WORSE")).Resolve(),
                now + MedicalTriageMath.GiveUpTicks);
            // apart: the carrier sets off with the mod's job and says why, of the one worse off, not to them.
            string raw = Flavor.FromPool(operation ? "Rimfolk_Pool_TriageSurgery" : "Rimfolk_Pool_TriageRoom", carrier);
            if (!raw.NullOrEmpty()) Speech.Say(carrier, raw.Formatted(worse.pawn.Named("OTHER")).Resolve());
            return true;
        }

        /// <summary>A move under way, every 250 ticks: the lighter one carried out, then the worse one brought in.</summary>
        private static void Follow(Move m, int now)
        {
            if (Gone(m) || MedicalTriageMath.GiveUp(m.started, now))
            {
                End(m);
                return;
            }
            if (!m.lighterOut)
            {
                if (m.lighter.InBed() && m.lighter.CurrentBed() == m.other)
                {
                    m.lighterOut = true;
                    Moved(m, now);
                }
                else
                {
                    // The carry was stopped (the bed taken, the carrier drafted or hurt): the move is over.
                    if (!Doing(m.carrier, m.lighter)) End(m);
                    return;
                }
            }
            var w = m.worse;
            if (w.InBed() && w.CurrentBed() == m.best || !m.best.AnyUnoccupiedSleepingSlot || !HealthAIUtility.ShouldSeekMedicalRest(w))
            {
                End(m);
                return;
            }
            if (m.carrierIn != null && Doing(m.carrierIn, w)) return;
            // Carried by someone, a rescue on its way, or the mod's own transfer: they come.
            if (!w.Spawned || w.CarriedBy != null || Bringing(w) || transferActive(w.thingIDNumber)) return;
            // Up and about, they go to bed on their own, and the best bed is kept for them.
            if (!w.InBed() && !w.Downed) return;
            if (w.InMentalState) return;
            Attended(w, out bool operating, out bool tended);
            if (operating || tended) return;
            var carrier = Carrier(w, m.best, m.lighter);
            if (carrier == null) return;
            if (!Transfer(carrier, w, m.best)) return;
            m.carrierIn = carrier;
            lastMoved[w.thingIDNumber] = now;
        }

        private static bool Doing(Pawn carrier, Pawn patient)
        {
            if (carrier == null || carrier.Dead || !carrier.Spawned) return false;
            var job = carrier.CurJob;
            return job != null && job.def == transferJob && job.targetA.Thing == patient;
        }

        private static bool Gone(Move m)
        {
            if (m.map == null || !Find.Maps.Contains(m.map)) return true;
            if (m.lighter == null || m.lighter.Dead || m.lighter.Destroyed) return true;
            if (m.worse == null || m.worse.Dead || m.worse.Destroyed || m.worse.MapHeld != m.map) return true;
            return m.best == null || !m.best.Spawned || m.other == null || !m.other.Spawned;
        }

        private static void End(Move m)
        {
            if (m == null) return;
            if (m.map != null) moves.Remove(m.map.uniqueID);
            else
            {
                int key = -1;
                foreach (var kv in moves)
                    if (kv.Value == m) key = kv.Key;
                if (key >= 0) moves.Remove(key);
            }
            if (m.best != null) lines.Remove(m.best.thingIDNumber);
        }

        /// <summary>The lighter one lies in their new bed: the colony news tells it, and their inspect line says why.</summary>
        private static void Moved(Move m, int now)
        {
            lines[m.lighter.thingIDNumber] = ((m.operation ? "CC_Triage_MadeRoomOp" : "CC_Triage_MadeRoom").Translate(m.worse.Named("WORSE")).Resolve(),
                now + MedicalTriageMath.LineTicks);
            try
            {
                string age = Ages.Ours.ToString();
                var room = m.other.GetRoom();
                string where = room != null && room.Role == RoomRoleDefOf.Hospital ? " " + ("CC_Triage_Ward_" + age).Translate().Resolve() : "";
                string key = TextVariants.Vary((m.operation ? "CC_Triage_MovedOp_" : "CC_Triage_Moved_") + age);
                // together: the carrier carried the patient to the bed; the one worse off is named as the reason, not as doing anything.
                string text = key.Translate(m.carrier.Named("CARRIER"), m.lighter.Named("LIGHTER"), m.worse.Named("WORSE"), m.best.def.label.Named("BED"),
                    where.Named("WHERE")).Resolve();
                Tell.Minor(NewsKind.Health, Importance.Notable, m.map, text, new LookTargets(m.lighter), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medical triage: the news", e);
            }
        }

        // ================= Inspect lines =================

        /// <summary>Whether any bed or patient has a line (the inspect patches ask this every frame).</summary>
        public static bool AnyLines => lines.Count > 0;

        /// <summary>The line under a bed's or a patient's inspect text: why the bed is kept, why they were moved.</summary>
        public static string LineFor(Thing t)
        {
            if (t == null || lines.Count == 0) return null;
            Fresh();
            return lines.TryGetValue(t.thingIDNumber, out var l) && Now < l.until ? l.line : null;
        }
    }

    /// <summary>
    /// Runs medical triage's pass (Integrations/MedicalTriage.cs) at tick 53 of the 250-tick cycle: on the hour on every
    /// home map, otherwise only while a move is under way or a map waits to be looked at. Nothing saved; with Move the
    /// Patient missing, one remainder a 250-tick stretch and nothing else.
    /// </summary>
    public class GameComponent_MedicalTriage : GameComponent
    {
        public GameComponent_MedicalTriage(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            MedicalTriage.Reset();
        }

        public override void GameComponentTick()
        {
            int t = Find.TickManager.TicksGame;
            if (Sched.Mod(t, Sched.RareTicks) != Sched.RareTriage || !MedicalTriage.Ready) return;
            bool hour = Sched.Hourly(t, Sched.RareTriage);
            if (!hour && !MedicalTriage.Waiting) return;
            using var clock = PassClock.Time(hour ? Pass.Triage : Pass.RareTriage);
            try
            {
                MedicalTriage.Pass(t, hour);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medical triage", e);
            }
        }
    }

    /// <summary>Best bed first for a patient (Integrations/MedicalTriage.cs), after Move the Patient's own choice.</summary>
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.FindBedFor), new[] { typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool), typeof(GuestStatus?) })]
    [HarmonyAfter(MedicalTriage.HarmonyId)]
    [HarmonyPriority(Priority.Low)]
    public static class Patch_TriageFindBedFor
    {
        public static bool Prepare() => MedicalTriage.Ready;

        public static void Postfix(Pawn sleeper, Pawn traveler, bool checkSocialProperness, bool ignoreOtherReservations, GuestStatus? guestStatus, ref Building_Bed __result)
        {
            if (__result == null || !__result.Medical) return;
            __result = MedicalTriage.BestFirst(sleeper, traveler, __result, checkSocialProperness, false, ignoreOtherReservations, guestStatus);
        }
    }

    /// <summary>Best bed first for a patient's own way to a sickbed (Integrations/MedicalTriage.cs), after Move the Patient's choice.</summary>
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.FindPatientBedFor))]
    [HarmonyAfter(MedicalTriage.HarmonyId)]
    [HarmonyPriority(Priority.Low)]
    public static class Patch_TriageFindPatientBedFor
    {
        public static bool Prepare() => MedicalTriage.Ready;

        public static void Postfix(Pawn pawn, ref Building_Bed __result)
        {
            if (__result == null || !__result.Medical || pawn == null) return;
            __result = MedicalTriage.BestFirst(pawn, pawn, __result, false, true, false, pawn.GuestStatus);
        }
    }

    /// <summary>A bed kept for someone worse off says so under its inspect text (Integrations/MedicalTriage.cs).</summary>
    [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetInspectString))]
    public static class Patch_TriageBedInspect
    {
        public static bool Prepare() => MedicalTriage.Ready;

        public static void Postfix(Building_Bed __instance, ref string __result)
        {
            if (!MedicalTriage.AnyLines || !RimfolkPatches.On) return;
            try
            {
                string line = MedicalTriage.LineFor(__instance);
                if (line.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? line : __result.TrimEndNewlines() + "\n" + line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medical triage: a bed's inspect line", e);
            }
        }
    }

    /// <summary>A patient moved to make room says why under their inspect text (Integrations/MedicalTriage.cs).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_TriagePatientInspect
    {
        public static bool Prepare() => MedicalTriage.Ready;

        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (!MedicalTriage.AnyLines || !RimfolkPatches.On) return;
            try
            {
                string line = MedicalTriage.LineFor(__instance);
                if (line.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? line : __result.TrimEndNewlines() + "\n" + line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medical triage: a patient's inspect line", e);
            }
        }
    }
}
