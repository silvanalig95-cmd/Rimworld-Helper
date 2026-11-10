using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// One Hospitality guest's part in the colony's work while they stay (saved; let go when they leave): the guest tab's
    /// switch, today's hours (everyday work and the trouble's), the visit's, the kinds of work they've said why they took up,
    /// the work types turned on for a trouble or a thanks day, and their day of work in thanks.
    /// </summary>
    public class GuestHelpRecord : IExposable
    {
        public Pawn pawn;
        /// <summary>The guest tab's "Let them help" switched off.</summary>
        public bool off;
        /// <summary>The local day the day's counts are for (year * 60 + day of the year), and the counts (ticks).</summary>
        public int day = -1, everyday, crisis;
        /// <summary>Everyday work over the whole visit (ticks), and the troubles they pitched in to.</summary>
        public int visitEveryday, crises;
        /// <summary>Overworked today (stopped all but saving a life, and held it against the colony).</summary>
        public bool overworked;
        /// <summary>The days they last stopped at the cap, unhappy, or were hurt at the colony's work (each said once a day).</summary>
        public int enoughDay = -1, unhappyDay = -1, hurtDay = -1;
        /// <summary>The kinds of work (work type defNames) they've said why they took up, GuestHelpMath.MaxTold at most.</summary>
        public List<string> told = new List<string>();
        /// <summary>Work types turned on in their work settings for a trouble or a thanks day, to turn off again.</summary>
        public List<string> enabled = new List<string>();
        /// <summary>The troubles (HelpCrisis flags) they've said they pitch in to, while each lasts.</summary>
        public int crisisSaid;
        /// <summary>Their day of work in thanks: until when, which work, why; offered once a visit.</summary>
        public int thanksUntil = -1;
        public string thanksWork;
        public HelpThanks thanksWhy;
        public bool thanksGiven;

        /// <summary>The troubles they pitched in to at the last pass (a trouble's job is stopped when its trouble ends).</summary>
        [Unsaved] public HelpCrisis lastCrises;

        /// <summary>The colony's work they were last seen at, and when (a wound that ended the job still counts as got at it).</summary>
        [Unsaved] public WorkTypeDef lastWork;
        [Unsaved] public int lastWorkTick = -99999;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref off, "off");
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref everyday, "everyday");
            Scribe_Values.Look(ref crisis, "crisis");
            Scribe_Values.Look(ref visitEveryday, "visitEveryday");
            Scribe_Values.Look(ref crises, "crises");
            Scribe_Values.Look(ref overworked, "overworked");
            Scribe_Values.Look(ref enoughDay, "enoughDay", -1);
            Scribe_Values.Look(ref unhappyDay, "unhappyDay", -1);
            Scribe_Values.Look(ref hurtDay, "hurtDay", -1);
            Scribe_Collections.Look(ref told, "told", LookMode.Value);
            Scribe_Collections.Look(ref enabled, "enabled", LookMode.Value);
            Scribe_Values.Look(ref crisisSaid, "crisisSaid");
            Scribe_Values.Look(ref thanksUntil, "thanksUntil", -1);
            Scribe_Values.Look(ref thanksWork, "thanksWork");
            Scribe_Values.Look(ref thanksWhy, "thanksWhy");
            Scribe_Values.Look(ref thanksGiven, "thanksGiven");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                told ??= new List<string>();
                enabled ??= new List<string>();
            }
        }
    }

    /// <summary>
    /// The guests' records (a game component), and the pass that follows them every 250 ticks at Sched.RareGuestHelp, only
    /// while Hospitality's guests are on a home map or a record waits to be let go.
    /// </summary>
    public class GuestHelpBook : GameComponent
    {
        public List<GuestHelpRecord> guests = new List<GuestHelpRecord>();

        private static GuestHelpBook cached;
        private static Game cachedGame;

        public GuestHelpBook(Game game)
        {
        }

        public static GuestHelpBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<GuestHelpBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            GuestHelp.Reset();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareGuestHelp)) return;
            if (guests.Count == 0 && !GuestHelp.Any) return;
            using var clock = PassClock.Time(Pass.RareGuestHelp);
            try
            {
                GuestHelp.Rare(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("guests helping out", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // Only guests still here are kept: someone gone is let go (their farewell came at the pass after they left).
            if (Scribe.mode == LoadSaveMode.Saving && guests.RemoveAll(r => r?.pawn == null || r.pawn.Destroyed || r.pawn.Dead || !r.pawn.SpawnedOrAnyParentSpawned) > 0)
                GuestHelp.Reindex();
            Scribe_Collections.Look(ref guests, "guests", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                guests ??= new List<GuestHelpRecord>();
                guests.RemoveAll(r => r?.pawn == null);
                while (guests.Count > GuestHelpMath.MaxGuests) guests.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Hospitality's guests helping out (the player's request, 10 November 2026; the rules are GuestHelpMath.cs). What
    /// causes it is Hospitality's own "guests help out": a guest in a good mood may take up the colony's work they're
    /// skilled at. Rimfolk narrows that in everyday times to work they love or that fits who they are, lets them pitch in
    /// to the colony's real trouble whatever their mood, has a guest the colony did right by offer a day of work, keeps
    /// the customs of the titled, the proud, each faith and people, caps the day's hours, and makes it count both ways.
    /// The guest tab shows it all, with a switch. Hospitality read by name; nothing without it.
    /// </summary>
    public static class GuestHelp
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Hospitality's think node is there (read at startup by name): the patches apply only with it.</summary>
        public static bool HospitalityHere => GenTypes.GetTypeInAnyAssembly("Hospitality.ThinkNode_ConditionalWantsToHelp") != null;

        private static bool? hospitalityActive;

        /// <summary>Hospitality is in the mod list (looked up once: asked for each of a guest's work givers).</summary>
        private static bool HospitalityActive => hospitalityActive ??= People.ModActive(Guests.Mod);

        /// <summary>"Guests help with what they love": work they love, refusals, the cap, thanks, regard, the tab.</summary>
        public static bool Everyday => RimfolkPatches.On && (S?.guestHelp ?? true) && HospitalityActive;

        /// <summary>"Guests pitch in when the colony is in trouble".</summary>
        public static bool PitchIn => RimfolkPatches.On && (S?.guestsPitchIn ?? true) && HospitalityActive;

        public static bool Any => Everyday || PitchIn;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= Hospitality's own settings, by name =================

        private static FieldInfo fWorkOff, fMinSkill, fArtCraftOff, fRescued;
        private static Type compGuest;
        private static bool looked;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                var s = GenTypes.GetTypeInAnyAssembly("Hospitality.ModSettings_Hospitality");
                if (s != null)
                {
                    fWorkOff = AccessTools.Field(s, "disableWork");
                    fMinSkill = AccessTools.Field(s, "minGuestWorkSkill");
                    fArtCraftOff = AccessTools.Field(s, "disableArtAndCraft");
                }
                compGuest = GenTypes.GetTypeInAnyAssembly("Hospitality.CompGuest");
                if (compGuest != null) fRescued = AccessTools.Field(compGuest, "rescued");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Hospitality's settings", e);
            }
        }

        /// <summary>Hospitality's "Disable guests helping" is on.</summary>
        public static bool HospitalityWorkOff
        {
            get
            {
                Look();
                return fWorkOff?.GetValue(null) is bool b && b;
            }
        }

        /// <summary>Hospitality's minimum skill for a guest's work (7 by default).</summary>
        public static int HospitalityMinSkill
        {
            get
            {
                Look();
                return fMinSkill?.GetValue(null) is int i ? i : 7;
            }
        }

        private static bool HospitalityArtCraftOff
        {
            get
            {
                Look();
                return fArtCraftOff?.GetValue(null) is bool b ? b : true;
            }
        }

        /// <summary>Hospitality's record says the colony carried them in when they were down.</summary>
        private static bool Rescued(Pawn g)
        {
            Look();
            if (fRescued == null || g?.AllComps == null) return false;
            var comp = g.AllComps.FirstOrDefault(c => c != null && compGuest.IsInstanceOfType(c));
            return comp != null && fRescued.GetValue(comp) is bool b && b;
        }

        // ================= The records =================

        private static readonly Dictionary<int, GuestHelpRecord> byId = new Dictionary<int, GuestHelpRecord>();
        private static GuestHelpBook indexed;

        public static void Reset()
        {
            byId.Clear();
            indexed = null;
            views.Clear();
            maps.Clear();
            pairs.Clear();
            tabs.Clear();
            looked = false;
        }

        /// <summary>The records changed outside the pass (some let go at saving): looked up afresh.</summary>
        public static void Reindex() => indexed = null;

        private static void Index(GuestHelpBook book)
        {
            if (indexed == book) return;
            indexed = book;
            byId.Clear();
            if (book == null) return;
            foreach (var r in book.guests)
                if (r?.pawn != null) byId[r.pawn.thingIDNumber] = r;
        }

        /// <summary>A guest's record, made for a Hospitality guest when asked to; null for anyone else.</summary>
        public static GuestHelpRecord Record(Pawn g, bool make)
        {
            var book = GuestHelpBook.Instance;
            if (book == null || g == null) return null;
            Index(book);
            if (byId.TryGetValue(g.thingIDNumber, out var r) && r.pawn == g) return r;
            if (!make || !g.Spawned || g.Dead || g.RaceProps?.Humanlike != true || !Strangers.IsHospitalityGuest(g)) return null;
            r = new GuestHelpRecord { pawn = g };
            book.guests.Add(r);
            byId[g.thingIDNumber] = r;
            while (book.guests.Count > GuestHelpMath.MaxGuests)
            {
                var old = book.guests[0];
                book.guests.RemoveAt(0);
                if (old?.pawn != null) byId.Remove(old.pawn.thingIDNumber);
            }
            return r;
        }

        /// <summary>The guest tab's switch.</summary>
        public static bool LetHelp(Pawn g) => !(Record(g, false)?.off ?? false);

        public static void SetLetHelp(Pawn g, bool on)
        {
            var r = Record(g, true);
            if (r == null || r.off == !on) return;
            r.off = !on;
            // Switched off mid-job: they put it down.
            if (r.off && g.CurJob?.workGiverDef != null && !GuestHelpMath.OwnCare(g.CurJob.workGiverDef.workType?.defName)) Stop(g);
            tabs.Remove(g.thingIDNumber);
        }

        private static bool ThanksOn(GuestHelpRecord r) => r != null && r.thanksUntil > Now && !r.thanksWork.NullOrEmpty();

        // ================= Who a guest is =================

        /// <summary>What's worked out about a guest at most once an hour (their people, faith, standing and loves), and their troubles every 250 ticks.</summary>
        internal sealed class View
        {
            public int tick = int.MinValue;
            public bool titled, highStanding, proud, hardWorker, noTrees;
            public string calling, title;
            public readonly List<string> precepts = new List<string>();
            public readonly Dictionary<WorkTypeDef, HelpFit> fit = new Dictionary<WorkTypeDef, HelpFit>();
            public readonly Dictionary<WorkTypeDef, int> passion = new Dictionary<WorkTypeDef, int>();
            public readonly Dictionary<WorkTypeDef, HelpRefusal> refuse = new Dictionary<WorkTypeDef, HelpRefusal>();
            public HelpCrisis crises;
            public int crisesTick = int.MinValue;
        }

        private static readonly Dictionary<int, View> views = new Dictionary<int, View>();

        private static TraitDef industrious;

        internal static View ViewOf(Pawn g)
        {
            int now = Now;
            if (!views.TryGetValue(g.thingIDNumber, out var v)) views[g.thingIDNumber] = v = new View();
            if (now - v.tick < GuestHelpMath.HourTicks && v.tick <= now) return v;
            if (views.Count > 80) views.Clear();
            v.tick = now;
            v.precepts.Clear();
            v.fit.Clear();
            v.passion.Clear();
            v.refuse.Clear();
            try
            {
                var title = g.royalty?.MostSeniorTitle;
                v.titled = title != null;
                v.title = title?.Label;
                var n = NotableBook.Instance?.notables.FirstOrDefault(x => x != null && x.pawn == g);
                v.highStanding = n != null && (n.role == NotableRole.Head || n.role == NotableRole.Heir);
                v.proud = n != null && Persona.Get(g, PersonaAxis.Pride) >= GuestHelpMath.ProudAt;
                v.calling = Peoples.CallingOf(g.Faction);
                industrious ??= DefDatabase<TraitDef>.GetNamedSilentFail("Industriousness");
                v.hardWorker = industrious != null && g.story?.traits != null && g.story.traits.HasTrait(industrious) && g.story.traits.DegreeOfTrait(industrious) > 0;
                if (ModsConfig.IdeologyActive && g.Ideo != null)
                    foreach (var p in g.Ideo.PreceptsListForReading)
                        if (p?.def != null) v.precepts.Add(p.def.defName);
                v.noTrees = GuestHelpMath.NoTrees(v.calling, v.precepts);
                foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                {
                    int passion = 0;
                    if (g.skills != null && wt.relevantSkills != null)
                        foreach (var sd in wt.relevantSkills)
                            passion = Math.Max(passion, People.PassionLevel(g.skills.GetSkill(sd)));
                    v.passion[wt] = passion;
                    v.fit[wt] = GuestHelpMath.Fit(wt.defName, passion, v.hardWorker, v.calling, v.precepts);
                    v.refuse[wt] = GuestHelpMath.Refuse(wt.defName, v.titled, v.highStanding, v.proud, v.calling, v.precepts, true);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("who a guest is, for their help", e);
            }
            return v;
        }

        private static HelpFit FitOf(View v, WorkTypeDef wt) => wt != null && v.fit.TryGetValue(wt, out var f) ? f : HelpFit.None;

        private static HelpRefusal RefusalOf(View v, WorkTypeDef wt) => wt != null && v.refuse.TryGetValue(wt, out var r) ? r : HelpRefusal.None;

        // ================= The colony in trouble, map by map =================

        private sealed class MapTrouble
        {
            public int tick = int.MinValue;
            public HelpCrisis on;
            public bool foodShort;
            public readonly List<IntVec3> fires = new List<IntVec3>();
        }

        private static readonly Dictionary<int, MapTrouble> maps = new Dictionary<int, MapTrouble>();

        private static MapTrouble TroubleOf(Map map)
        {
            int now = Now;
            if (!maps.TryGetValue(map.uniqueID, out var t)) maps[map.uniqueID] = t = new MapTrouble();
            if (now - t.tick < Sched.RareTicks && t.tick <= now) return t;
            t.tick = now;
            t.on = HelpCrisis.None;
            t.fires.Clear();
            if (!map.IsPlayerHome) return t;
            try
            {
                foreach (var f in map.listerThings.ThingsInGroup(ThingRequestGroup.Fire))
                {
                    if (f == null || !f.Spawned || !map.areaManager.Home[f.Position]) continue;
                    t.fires.Add(f.Position);
                    if (t.fires.Count >= 60) break;
                }
                if (t.fires.Count > 0) t.on |= HelpCrisis.Fire;
                var people = map.mapPawns.FreeColonistsSpawned.Concat(map.mapPawns.SlavesOfColonySpawned).ToList();
                if (GuestHelpMath.RescueNeeded(people.Count(p => p.Downed && !p.Dead && !p.InBed()))) t.on |= HelpCrisis.Rescue;
                int threat = map.storyState?.LastThreatBigTick ?? -1;
                int since = threat > 0 ? now - threat : -1;
                if (since >= 0 && since <= GuestHelpMath.TendWindowTicks
                    && GuestHelpMath.TendNeeded(GenHostility.AnyHostileActiveThreatToPlayer(map), since, people.Count(p => HealthAIUtility.ShouldBeTendedNowByPlayer(p))))
                    t.on |= HelpCrisis.Tend;
                if (GuestHelpMath.FrostHarvest(FrostMarkedStanding(map))) t.on |= HelpCrisis.Frost;
                t.foodShort = GuestHelpMath.FoodShort(MapFacts.Of(map)?.FoodDays ?? 999f, t.foodShort, map.mapPawns.FreeColonistsSpawnedCount);
                if (t.foodShort) t.on |= HelpCrisis.Food;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the colony's trouble, for guests to pitch in", e);
            }
            return t;
        }

        /// <summary>Crops a frost harvest marked (Fields/FieldWeather.cs) still standing to be brought in on this map.</summary>
        private static int FrostMarkedStanding(Map map)
        {
            var t = SuggestTracker.Instance;
            if (t == null || !FieldWeather.FrostOn) return 0;
            int n = 0;
            foreach (var d in t.done)
            {
                if (d == null || d.map != map || d.state != SuggestMath.Waiting || !(d.check is FrostHarvestCheck) || !(d.undo is UndoOrders u)) continue;
                foreach (var thing in u.marked)
                    if (thing is Plant p && p.Spawned && p.Map == map && map.designationManager.DesignationOn(p, DesignationDefOf.HarvestPlant) != null)
                        n++;
            }
            return n;
        }

        /// <summary>The troubles this guest pitches in to now (worked out every 250 ticks).</summary>
        public static HelpCrisis CrisesOf(Pawn g)
        {
            if (!PitchIn || g?.Map == null || !g.Map.IsPlayerHome) return HelpCrisis.None;
            var v = ViewOf(g);
            int now = Now;
            if (now - v.crisesTick < Sched.RareTicks && v.crisesTick <= now) return v.crises;
            v.crisesTick = now;
            v.crises = HelpCrisis.None;
            var r = Record(g, false);
            if (r == null || r.off || g.Downed || g.InMentalState) return v.crises;
            var t = TroubleOf(g.Map);
            if (t.on == HelpCrisis.None) return v.crises;
            float near = -1f;
            foreach (var c in t.fires)
            {
                float d = c.DistanceTo(g.Position);
                if (near < 0f || d < near) near = d;
            }
            var medicine = g.skills?.GetSkill(SkillDefOf.Medicine);
            var cooking = g.skills?.GetSkill(SkillDefOf.Cooking);
            bool canTend = medicine != null && !medicine.TotallyDisabled && medicine.Level >= GuestHelpMath.TendMedicine && !g.WorkTypeIsDisabled(WorkTypeDefOf.Doctor);
            bool canCook = cooking != null && !cooking.TotallyDisabled && cooking.Level >= GuestHelpMath.CookSkill && Doable(g, GuestHelpMath.Work.Cooking);
            bool canHarvest = Doable(g, GuestHelpMath.Work.Growing) || Doable(g, GuestHelpMath.Work.PlantCutting);
            v.crises = GuestHelpMath.ForGuest(t.on, GuestHelpMath.FireIsNear(near), canTend, canCook, canHarvest,
                GuestHelpMath.LifeOnly(v.titled, v.highStanding, v.proud), r.overworked, HospitalityWorkOff);
            return v.crises;
        }

        private static bool Doable(Pawn g, string workType)
        {
            var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workType);
            return wt != null && !g.WorkTypeIsDisabled(wt);
        }

        // ================= Hospitality's questions, answered =================

        /// <summary>The postfix on JobGiver_Work.PawnCanUseWorkGiver: Hospitality's answer, narrowed or (in trouble, on a thanks day) opened.</summary>
        public static bool CanUse(Pawn g, WorkGiver giver, bool hospitality)
        {
            var wt = giver?.def?.workType;
            if (wt == null || GuestHelpMath.OwnCare(wt.defName)) return hospitality;
            var r = Record(g, true);
            if (r == null) return hospitality;
            var v = ViewOf(g);
            var giverRefusal = GuestHelpMath.RefuseGiver(giver.def.defName, IsOperation(giver), v.precepts);
            var refusal = giverRefusal == HelpRefusal.Never ? HelpRefusal.Never : RefusalOf(v, wt) != HelpRefusal.None ? RefusalOf(v, wt) : giverRefusal;
            var inCrisis = CrisesOf(g);
            var giverCrisis = GuestHelpMath.CrisisOf(wt.defName, giver.def.defName, giver.GetType().Name);
            bool thanksWork = ThanksOn(r) && r.thanksWork == wt.defName && Everyday && !HospitalityWorkOff;
            var ask = new GuestHelpMath.Ask
            {
                hospitality = hospitality,
                everydayOn = Everyday,
                pitchInOn = PitchIn,
                letHelp = !r.off,
                ownCare = false,
                refused = refusal,
                fits = FitOf(v, wt) != HelpFit.None,
                capOpen = GuestHelpMath.CapOpen(r.everyday),
                overworked = r.overworked,
                inCrisis = inCrisis,
                giverCrisis = giverCrisis,
                crisisCan = (giverCrisis & inCrisis) != HelpCrisis.None && CanDo(g, giver),
                thanksWork = thanksWork,
                thanksCan = thanksWork && HospitalityAllowsButItsRoll(g, giver)
            };
            return GuestHelpMath.Allow(ask);
        }

        /// <summary>The game's own checks on a work giver (as Hospitality's own prefix makes them), without its skill minimum or roll.</summary>
        private static bool CanDo(Pawn g, WorkGiver giver)
        {
            var d = giver.def;
            if (d.workType != null && g.WorkTypeIsDisabled(d.workType)) return false;
            if (g.WorkTagIsDisabled(d.workTags)) return false;
            return !giver.ShouldSkip(g) && giver.MissingRequiredCapacity(g) == null;
        }

        /// <summary>Surgery: a bill on a person or an animal.</summary>
        private static bool IsOperation(WorkGiver giver) => giver is WorkGiver_DoBill && (giver.def.billGiversAllHumanlikes || giver.def.billGiversAllAnimals);

        /// <summary>
        /// Hospitality's own rules for a guest's work, all but its roll to help: its skill minimum, no talks or recruiting, no
        /// art or crafting with its setting, no surgery, no tending (its medical exclusion), no warden's work or feeding.
        /// </summary>
        private static bool HospitalityAllowsButItsRoll(Pawn g, WorkGiver giver)
        {
            if (!CanDo(g, giver)) return false;
            var wt = giver.def.workType;
            if (wt.relevantSkills != null && wt.relevantSkills.Count > 0 && (g.skills == null || g.skills.AverageOfRelevantSkillsFor(wt) < HospitalityMinSkill)) return false;
            string cls = giver.GetType().Name;
            if (cls == "WorkGiver_Diplomat" || cls == "WorkGiver_Recruiter") return false;
            if (HospitalityArtCraftOff && (wt.workTags & (WorkTags.Crafting | WorkTags.Artistic)) != 0) return false;
            if (IsOperation(giver) || giver is WorkGiver_Tend || giver is WorkGiver_Warden || giver is WorkGiver_FeedPatient) return false;
            return true;
        }

        /// <summary>The postfix on Hospitality's ThinkNode_ConditionalWantsToHelp: open in trouble and on a thanks day, whatever their mood.</summary>
        public static bool WantsToHelp(Pawn g, bool hospitality)
        {
            if (g?.Faction == null || g.Faction.IsPlayer) return hospitality;
            var r = Record(g, true);
            if (r == null) return hospitality;
            return GuestHelpMath.Gate(hospitality, Everyday, PitchIn, !r.off, CrisesOf(g), ThanksOn(r) && !HospitalityWorkOff);
        }

        /// <summary>Rimfolk's step before a guest's needs (ThinkNode_GuestPitchIn): in trouble they pitch in to.</summary>
        public static bool PitchingIn(Pawn g)
        {
            if (g?.Faction == null || g.Faction.IsPlayer || !PitchIn || g.Downed || g.Drafted) return false;
            var r = Record(g, false);
            if (r == null) return false;
            bool starving = g.needs?.food != null && g.needs.food.CurCategory >= HungerCategory.UrgentlyHungry;
            bool exhausted = g.needs?.rest != null && g.needs.rest.CurCategory >= RestCategory.Exhausted;
            return GuestHelpMath.PitchingIn(PitchIn, !r.off, CrisesOf(g), starving, exhausted);
        }

        // ================= A job chosen =================

        /// <summary>
        /// The postfix on JobGiver_Work.TryIssueJobPackage, for a guest's work: never into a private room, at the dead or a
        /// prisoner, nor a tree for those who never fell one (rescue aside); the first time they take up a kind of work,
        /// why in a sentence; the first time in a trouble, why, and who helps in a message.
        /// </summary>
        public static void JobChosen(Pawn g, ref ThinkResult result)
        {
            var job = result.Job;
            var giver = job?.workGiverDef;
            var wt = giver?.workType;
            if (wt == null || GuestHelpMath.OwnCare(wt.defName)) return;
            var r = Record(g, false);
            if (r == null) return;
            var v = ViewOf(g);
            if (Vetoed(g, job, v))
            {
                result = ThinkResult.NoJob;
                return;
            }
            var kind = GuestHelpMath.CrisisOf(wt.defName, giver.defName, giver.giverClass?.Name);
            var crises = CrisesOf(g);
            if ((kind & crises) != HelpCrisis.None)
            {
                Pitched(g, r, kind);
                return;
            }
            if (ThanksOn(r) && r.thanksWork == wt.defName) return;
            if (!Everyday || r.told.Contains(wt.defName)) return;
            var fit = FitOf(v, wt);
            if (fit == HelpFit.None) return;
            r.told.Add(wt.defName);
            while (r.told.Count > GuestHelpMath.MaxTold) r.told.RemoveAt(0);
            Say(g, FitPool, "why:" + FitTag(fit), wt, "w:" + wt.defName);
        }

        /// <summary>The first job they take in a trouble: why, said, and who helps in a message (one a trouble a map).</summary>
        internal static void Pitched(Pawn g, GuestHelpRecord r, HelpCrisis kind)
        {
            if ((r.crisisSaid & (int)kind) != 0) return;
            r.crisisSaid |= (int)kind;
            r.crises++;
            Say(g, CrisisPool, "c:" + CrisisTag(kind), null);
            // apart: who pitches in and why, as the bubble over them says it; the job itself shows it on the map.
            Tell.Message(NewsKind.World, Importance.Notable, g.Map,
                "CC_GuestHelp_Msg_Crisis".Translate(g.Named("PAWN"), PeopleName(g).Named("PEOPLE"), CrisisWords(kind).Named("WHAT")).Resolve(),
                new LookTargets(g), MessageTypeDefOf.PositiveEvent, "guesthelp|" + g.Map?.uniqueID + "|" + kind);
        }

        /// <summary>Never into a private room, at the dead, a prisoner, or a tree for those who never fell one (a rescue goes where it must).</summary>
        private static bool Vetoed(Pawn g, Job job, View v)
        {
            if (job.def == JobDefOf.Rescue) return false;
            var map = g.Map;
            if (map == null) return false;
            if (Bad(job.targetA, map, v) || Bad(job.targetB, map, v) || Bad(job.targetC, map, v)) return true;
            if (job.targetQueueA != null)
                foreach (var t in job.targetQueueA)
                    if (Bad(t, map, v)) return true;
            if (job.targetQueueB != null)
                foreach (var t in job.targetQueueB)
                    if (Bad(t, map, v)) return true;
            return false;
        }

        private static bool Bad(LocalTargetInfo t, Map map, View v)
        {
            if (!t.IsValid) return false;
            var thing = t.Thing;
            if (thing is Corpse c && c.InnerPawn?.RaceProps?.Humanlike == true) return true;
            if (thing is Building_Grave) return true;
            if (thing is Pawn p && p.IsPrisonerOfColony) return true;
            if (v.noTrees && thing is Plant pl && pl.def.plant != null && pl.def.plant.IsTree) return true;
            var cell = t.Cell;
            if (!cell.IsValid || !cell.InBounds(map)) return false;
            var room = cell.GetRoom(map);
            return room != null && !room.PsychologicallyOutdoors && GuestHelpMath.PrivateRoom(room.Role?.defName);
        }

        // ================= The pass =================

        private static readonly HashSet<int> seen = new HashSet<int>();

        /// <summary>
        /// Every 250 ticks: each guest on a home map counted (today's hours, the cap, unhappy, overworked), the troubles' work
        /// types turned on and off, a trouble's job stopped when it ends, side by side, hurt at the work, a thanks day
        /// offered; a guest gone, their farewell.
        /// </summary>
        public static void Rare(int now)
        {
            var book = GuestHelpBook.Instance;
            if (book == null) return;
            Index(book);
            seen.Clear();
            if (Any)
                foreach (var map in Find.Maps)
                {
                    if (map == null || !map.IsPlayerHome) continue;
                    foreach (var lord in map.lordManager.lords)
                    {
                        if (lord?.LordJob?.GetType().Namespace != "Hospitality") continue;
                        foreach (var g in lord.ownedPawns)
                        {
                            if (g == null || g.Dead || !g.SpawnedOrAnyParentSpawned || g.RaceProps?.Humanlike != true) continue;
                            var r = Record(g, g.Spawned);
                            if (r == null) continue;
                            seen.Add(g.thingIDNumber);
                            try
                            {
                                Follow(g, r, map, now);
                            }
                            catch (Exception e)
                            {
                                CouncilLog.ErrorOnce("a guest helping out", e);
                            }
                        }
                    }
                }
            for (int i = book.guests.Count - 1; i >= 0; i--)
            {
                var r = book.guests[i];
                if (r?.pawn != null && seen.Contains(r.pawn.thingIDNumber)) continue;
                book.guests.RemoveAt(i);
                if (r?.pawn != null)
                {
                    byId.Remove(r.pawn.thingIDNumber);
                    views.Remove(r.pawn.thingIDNumber);
                    tabs.Remove(r.pawn.thingIDNumber);
                }
                try
                {
                    Farewell(r);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a guest's farewell", e);
                }
            }
            if (pairs.Count > 400) pairs.Clear();
        }

        private static int DayOf(Map map) => GenLocalDate.Year(map) * 60 + GenLocalDate.DayOfYear(map);

        private static void Follow(Pawn g, GuestHelpRecord r, Map map, int now)
        {
            int day = DayOf(map);
            if (r.day != day)
            {
                r.day = day;
                r.everyday = r.crisis = 0;
                r.overworked = false;
            }
            var crises = CrisesOf(g);
            // A trouble over for them: said afresh next time.
            r.crisisSaid &= (int)crises;
            if (r.thanksUntil >= 0 && r.thanksUntil <= now) r.thanksUntil = -1;
            WorkTypesFor(g, r, crises);

            var job = g.CurJob;
            var giver = job?.workGiverDef;
            var wt = giver?.workType;
            if (wt != null && !GuestHelpMath.OwnCare(wt.defName))
            {
                var kind = GuestHelpMath.CrisisOf(wt.defName, giver.defName, giver.giverClass?.Name);
                bool crisisJob = (kind & crises) != HelpCrisis.None;
                bool thanksJob = !crisisJob && ThanksOn(r) && r.thanksWork == wt.defName;
                if (crisisJob) r.crisis += Sched.RareTicks;
                else if (!thanksJob)
                {
                    r.everyday += Sched.RareTicks;
                    r.visitEveryday += Sched.RareTicks;
                }
                r.lastWork = wt;
                r.lastWorkTick = now;
                bool stopped = false;
                // A trouble's job whose trouble just ended for them (a fire's and a rescue's end by themselves).
                if (!crisisJob && (kind & r.lastCrises) != HelpCrisis.None && (kind & GuestHelpMath.WithHospitalityOff) == HelpCrisis.None
                    && !(Everyday && FitOf(ViewOf(g), wt) != HelpFit.None && GuestHelpMath.CapOpen(r.everyday)))
                {
                    Stop(g);
                    stopped = true;
                }
                if (!stopped && !crisisJob && !thanksJob && Everyday)
                {
                    if (!GuestHelpMath.CapOpen(r.everyday))
                    {
                        Stop(g);
                        stopped = true;
                        if (r.enoughDay != day)
                        {
                            r.enoughDay = day;
                            Say(g, StopPool, "s:enough", wt);
                        }
                    }
                    else if (g.needs?.mood != null && GuestHelpMath.TooUnhappy(g.needs.mood.CurLevel))
                    {
                        Stop(g);
                        stopped = true;
                        if (r.unhappyDay != day)
                        {
                            r.unhappyDay = day;
                            Say(g, StopPool, "s:unhappy", wt);
                        }
                    }
                }
                if (!stopped) SideBySide(g, wt, map, day);
            }
            // Hurt at the colony's work: at it now, or at the last look (a wound that put an end to the job).
            if (r.lastWork != null && now - r.lastWorkTick <= 2 * Sched.RareTicks) Hurt(g, r, r.lastWork, map, day);
            // The day's limits are the everyday setting's: with it off, Hospitality's own helping is left as it is.
            if (Everyday && !r.overworked && GuestHelpMath.Overworked(r.everyday, r.crisis))
            {
                r.overworked = true;
                Overworked(g, r, map);
                var cur = g.CurJob?.workGiverDef;
                if (cur?.workType != null && !GuestHelpMath.OwnCare(cur.workType.defName)
                    && (GuestHelpMath.CrisisOf(cur.workType.defName, cur.defName, cur.giverClass?.Name) & GuestHelpMath.LifeSaving & crises) == HelpCrisis.None)
                    Stop(g);
            }
            r.lastCrises = crises;
            Thanks(g, r, map, now);
        }

        private static void Stop(Pawn g)
        {
            if (g.jobs?.curJob != null) g.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        /// <summary>
        /// The work types a trouble or their thanks day asks of them turned on in their work settings (their guest's settings
        /// start with only a few on), and turned off again once it's over. Firefighting and their own care are never touched.
        /// </summary>
        private static void WorkTypesFor(Pawn g, GuestHelpRecord r, HelpCrisis crises)
        {
            var ws = g.workSettings;
            if (ws == null) return;
            var want = new HashSet<string>();
            if ((crises & HelpCrisis.Tend) != 0) want.Add(GuestHelpMath.Work.Doctor);
            if ((crises & HelpCrisis.Frost) != 0)
            {
                want.Add(GuestHelpMath.Work.Growing);
                want.Add(GuestHelpMath.Work.PlantCutting);
            }
            if ((crises & HelpCrisis.Food) != 0) want.Add(GuestHelpMath.Work.Cooking);
            if (ThanksOn(r) && Everyday && !r.off) want.Add(r.thanksWork);
            foreach (var name in r.enabled.ToList())
            {
                if (want.Contains(name)) continue;
                r.enabled.Remove(name);
                var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(name);
                if (wt != null && ws.EverWork && !g.WorkTypeIsDisabled(wt)) ws.SetPriority(wt, 0);
            }
            foreach (var name in want)
            {
                if (r.enabled.Contains(name) || r.enabled.Count >= GuestHelpMath.MaxEnabled) continue;
                var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(name);
                if (wt == null || g.WorkTypeIsDisabled(wt)) continue;
                ws.EnableAndInitializeIfNotAlreadyInitialized();
                if (ws.GetPriority(wt) != 0) continue;
                ws.SetPriority(wt, 3);
                r.enabled.Add(name);
            }
        }

        /// <summary>The work types turned on for them, off again (they're going).</summary>
        private static void WorkTypesOff(Pawn g, GuestHelpRecord r)
        {
            var ws = g?.workSettings;
            if (ws == null || r.enabled.Count == 0 || g.Destroyed || g.Dead) return;
            foreach (var name in r.enabled)
            {
                var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(name);
                if (wt != null && ws.EverWork && !g.WorkTypeIsDisabled(wt)) ws.SetPriority(wt, 0);
            }
            r.enabled.Clear();
        }

        // ================= It counts both ways =================

        private static ThoughtDef besideDef, overworkedDef;

        /// <summary>The pairs who worked side by side today (guest and colonist ids, the day), so it counts once a day.</summary>
        private static readonly Dictionary<long, int> pairs = new Dictionary<long, int>();

        /// <summary>Working beside a colonist at the same work: a little more friendship both ways, once a day each pair.</summary>
        private static void SideBySide(Pawn g, WorkTypeDef wt, Map map, int day)
        {
            besideDef ??= DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_GuestWorkedBeside");
            if (besideDef == null || !Everyday && !PitchIn) return;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
            {
                if (c == null || c.Downed || c.DevelopmentalStage.Baby()) continue;
                float d = c.Position.DistanceTo(g.Position);
                if (!GuestHelpMath.SideBySide(d, wt.defName, c.CurJob?.workGiverDef?.workType?.defName)) continue;
                long key = ((long)g.thingIDNumber << 32) | (uint)c.thingIDNumber;
                if (pairs.TryGetValue(key, out int last) && last == day) continue;
                pairs[key] = day;
                // together: the two are at the same work within a few cells of each other on the map, this very moment.
                c.needs?.mood?.thoughts?.memories?.TryGainMemory(besideDef, g);
                g.needs?.mood?.thoughts?.memories?.TryGainMemory(besideDef, c);
            }
        }

        /// <summary>
        /// Hurt at the colony's work (a new wound while at it: the game's own fire, a raider's shot at a rescue, an animal's
        /// bite): their people's notables think less of the colony, and the colony is told. Once a day.
        /// </summary>
        private static void Hurt(Pawn g, GuestHelpRecord r, WorkTypeDef wt, Map map, int day)
        {
            if (r.hurtDay == day || g.health?.hediffSet == null) return;
            bool hurt = false;
            foreach (var h in g.health.hediffSet.hediffs)
                if (h is Hediff_Injury && h.ageTicks <= GuestHelpMath.NewInjuryTicks)
                {
                    hurt = true;
                    break;
                }
            if (!hurt) return;
            r.hurtDay = day;
            Regard(g, -GuestHelpMath.HurtRegard, "CC_GuestHelp_Note_Hurt");
            // apart: the wound is the game's own, seen on the map; the message says what it costs with their people.
            Tell.Message(NewsKind.World, Importance.Important, map,
                "CC_GuestHelp_Msg_Hurt".Translate(g.Named("PAWN"), wt.gerundLabel.Named("WORK"), PeopleName(g).Named("PEOPLE")).Resolve(),
                new LookTargets(g), MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>A day's work too long: they stop all but saving a life, feel it (a mood, so Hospitality's own score drops), and a notable among them thinks less of the colony.</summary>
        private static void Overworked(Pawn g, GuestHelpRecord r, Map map)
        {
            overworkedDef ??= DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_GuestOverworked");
            var memories = g.needs?.mood?.thoughts?.memories;
            if (overworkedDef != null && memories != null)
            {
                if (ThoughtMaker.MakeThought(overworkedDef) is Thought_Memory m)
                {
                    if (m is Thought_CouncilMemory cm) cm.scaled = true;
                    memories.TryGainMemory(m);
                }
            }
            var n = NotableBook.Instance?.notables.FirstOrDefault(x => x != null && x.pawn == g);
            if (n != null)
            {
                n.personal -= GuestHelpMath.OverworkRegard;
                n.Remember("CC_GuestHelp_Note_Overworked".Translate(g.LabelShort.Named("GUEST")).Resolve(), -GuestHelpMath.OverworkRegard);
            }
            Say(g, StopPool, "s:overworked", null);
            // apart: the line over them and their work put down show it; the evening's news names it.
            Tell.Minor(NewsKind.World, Importance.Flavor, map, "CC_GuestHelp_Msg_Overworked".Translate(g.Named("PAWN"), PeopleName(g).Named("PEOPLE")).Resolve(),
                new LookTargets(g));
        }

        /// <summary>
        /// What their people's notables hear of the colony from one of theirs (a good word, or a wound): each of them, up
        /// to GuestHelpMath.MaxNotablesTold, not those at war with the colony; the guest too, if a notable.
        /// </summary>
        private static void Regard(Pawn g, float shift, string noteKey)
        {
            var book = NotableBook.Instance;
            if (!NotableBook.On || book == null || g?.Faction == null || g.Faction.IsPlayer) return;
            string note = noteKey.Translate(g.LabelShort.Named("GUEST")).Resolve();
            int told = 0;
            foreach (var n in book.notables.ToList())
            {
                if (n == null || n.faction != g.Faction || NotableBook.Hostile(n)) continue;
                bool self = n.pawn == g;
                if (!self && told >= GuestHelpMath.MaxNotablesTold) continue;
                if (!self) told++;
                n.personal += shift;
                n.Remember(note, shift);
            }
        }

        /// <summary>
        /// A guest gone: the work types turned on for them off again, and if they helped and left as a friend (not dead,
        /// not taken in, not a prisoner, their people not at war), the good word they take home to their people's notables.
        /// </summary>
        private static void Farewell(GuestHelpRecord r)
        {
            var g = r?.pawn;
            if (g == null) return;
            WorkTypesOff(g, r);
            if (!Any || g.Dead || g.Destroyed || g.Faction == null || g.Faction.IsPlayer || g.IsPrisoner || g.Faction.HostileTo(Faction.OfPlayer)) return;
            float good = GuestHelpMath.SpeakWell(r.visitEveryday, r.crises, r.thanksGiven);
            if (good <= 0f) return;
            Regard(g, good, "CC_GuestHelp_Note_SpokeWell");
            // apart: they've left the map; their people's regard moves, told in the evening's news.
            Tell.Minor(NewsKind.World, Importance.Flavor, null, "CC_GuestHelp_Msg_SpokeWell".Translate(g.Named("PAWN"), PeopleName(g).Named("PEOPLE")).Resolve(),
                LookTargets.Invalid);
        }

        // ================= Thanks paid in work =================

        /// <summary>
        /// A guest the colony did right by (carried in, set free once, tended, feasted, a stay going very well) offers a day
        /// of their best-loved work, once a visit, in a morning: Hospitality's guests' helping on, not in trouble, up and about.
        /// </summary>
        private static void Thanks(Pawn g, GuestHelpRecord r, Map map, int now)
        {
            if (!Everyday || r.thanksGiven || r.off || HospitalityWorkOff || g.Downed || g.InMentalState || !g.Awake()) return;
            if (!GuestHelpMath.ThanksHour(GenLocalDate.HourOfDay(map)) || CrisesOf(g) != HelpCrisis.None || r.overworked) return;
            var why = GuestHelpMath.Thanks(Rescued(g), Freed(g), Tended(g), Feasted(g, now), Guests.VisitScore(g));
            if (why == HelpThanks.None) return;
            var v = ViewOf(g);
            var candidates = new List<(string, int, int)>();
            foreach (var kv in v.fit)
            {
                var wt = kv.Key;
                if (kv.Value == HelpFit.None || RefusalOf(v, wt) != HelpRefusal.None || g.WorkTypeIsDisabled(wt) || wt.defName == GuestHelpMath.Work.Doctor) continue;
                if (HospitalityArtCraftOff && (wt.workTags & (WorkTags.Crafting | WorkTags.Artistic)) != 0) continue;
                int skill = wt.relevantSkills != null && wt.relevantSkills.Count > 0 && g.skills != null ? Mathf.RoundToInt(g.skills.AverageOfRelevantSkillsFor(wt)) : 0;
                if (wt.relevantSkills != null && wt.relevantSkills.Count > 0 && skill < HospitalityMinSkill) continue;
                candidates.Add((wt.defName, v.passion.TryGetValue(wt, out int p) ? p : 0, skill));
            }
            string best = GuestHelpMath.BestLoved(candidates);
            if (best == null) return;
            var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(best);
            if (work == null) return;
            r.thanksGiven = true;
            r.thanksUntil = now + GuestHelpMath.ThanksTicks;
            r.thanksWork = best;
            r.thanksWhy = why;
            WorkTypesFor(g, r, CrisesOf(g));
            Say(g, ThanksPool, "t:" + why.ToString().ToLowerInvariant(), work);
            string line = ("CC_GuestHelp_Msg_Thanks_" + why).Translate(g.Named("PAWN"), PeopleName(g).Named("PEOPLE"), work.gerundLabel.Named("WORK")).Resolve();
            // apart: the line over them says it, and the day's work shows on the map; the evening's news and their history keep it.
            Tell.Minor(NewsKind.World, Importance.Notable, map, line, new LookTargets(g));
            History.Add("guestHelp:thanks", new List<HistoryActor>
            {
                new HistoryActor(WorldLedger.PersonOf(g), HistoryRole.Did),
                new HistoryActor(Refs.Of(g.Faction), HistoryRole.With)
            }, Refs.Colony(map), HistoryMath.Everyday, line);
            tabs.Remove(g.thingIDNumber);
        }

        /// <summary>Once set free by the colony: a face the colony knows, met as freed (World/Notables.cs).</summary>
        private static bool Freed(Pawn g) => NotableBook.Instance?.notables.Any(n => n != null && n.pawn == g && n.met == "freed") ?? false;

        /// <summary>Tended by one of the colony's doctors while here (World/KinMemory.cs's notes).</summary>
        private static bool Tended(Pawn g) =>
            FactionMemory.Instance?.notes.Any(n => n != null && n.stranger == g && n.kind == "tend" && KinMemory.Ours(n.colonist)) ?? false;

        /// <summary>A feast for their people while they were here: their notable feasted at the colony in the last three days.</summary>
        private static bool Feasted(Pawn g, int now) =>
            FactionMemory.Instance?.deeds.Any(d => d != null && d.faction == g.Faction && d.kind == "feasted" && now - d.tick <= 3 * GenDate.TicksPerDay) ?? false;

        // ================= Words =================

        public const string FitPool = "Rimfolk_Pool_GuestHelpFit", CrisisPool = "Rimfolk_Pool_GuestHelpCrisis", ThanksPool = "Rimfolk_Pool_GuestHelpThanks",
            StopPool = "Rimfolk_Pool_GuestHelpStop";

        public static string FitTag(HelpFit f)
        {
            switch (f)
            {
                case HelpFit.BurningPassion: return "burning";
                case HelpFit.Passion: return "passion";
                case HelpFit.Calling: return "calling";
                case HelpFit.Faith: return "faith";
                case HelpFit.HardWorker: return "hard";
                default: return "none";
            }
        }

        public static string CrisisTag(HelpCrisis c) => c.ToString().ToLowerInvariant();

        /// <summary>The trouble in words: "fighting the fire".</summary>
        public static string CrisisWords(HelpCrisis c) => ("CC_GuestHelp_Crisis_" + c).Translate().Resolve();

        private static string PeopleName(Pawn g) => g.Faction?.Name ?? "CC_GuestHelp_TheirPeople".Translate().Resolve();

        /// <summary>
        /// A line from one of the guest pools over the guest's head, of the moment's tag only (<paramref name="must"/>),
        /// by their people's ways (their:tribal...: their age, the Empire, their calling), the work and their tone.
        /// </summary>
        private static void Say(Pawn g, string pool, string must, WorkTypeDef work, params string[] more)
        {
            if (g == null || !g.Spawned) return;
            try
            {
                var def = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(pool);
                if (def?.lines == null) return;
                var tags = new HashSet<string> { must };
                if (more != null)
                    foreach (var m in more)
                        if (!m.NullOrEmpty()) tags.Add(m);
                var f = g.Faction;
                tags.Add("their:" + Ages.Tag(Ages.Of(f)));
                if (Ages.IsEmpire(f)) tags.Add("their:empire");
                Peoples.AddTags(f, tags);
                var tone = ToneUtility.ToneFor(g);
                var own = def.lines.For(tone).Where(l => HasTag(l, must)).ToList();
                var plain = def.lines.plain.Where(l => HasTag(l, must)).ToList();
                string line = null;
                if (tone != Tone.Plain && own.Count > 0 && Rand.Chance(0.5f)) line = Flavor.PickFresh(own, tags, def.lines.For(tone));
                line ??= Flavor.PickFresh(plain, tags, def.lines.plain);
                if (line.NullOrEmpty()) return;
                string text = line.Formatted(g.Named("PAWN"), (work?.gerundLabel ?? "").Named("WORK"), PeopleName(g).Named("PEOPLE")).Resolve();
                Speech.Say(g, text);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest's words about helping", e);
            }
        }

        /// <summary>The line carries this tag (as one of its tags' alternatives, not negated).</summary>
        public static bool HasTag(string line, string tag)
        {
            var p = LineTags.Of(line);
            if (p?.alts == null) return false;
            foreach (var alts in p.alts)
                foreach (var a in alts)
                    if (!a.neg && a.name == tag) return true;
            return false;
        }

        // ================= The guest tab =================

        /// <summary>The guest tab's section, worked out every 250 ticks: its lines and their colours.</summary>
        internal sealed class Tab
        {
            public int tick;
            public readonly List<(string text, Color color)> lines = new List<(string, Color)>();
        }

        private static readonly Dictionary<int, Tab> tabs = new Dictionary<int, Tab>();

        internal static Tab TabOf(Pawn g)
        {
            int now = Now;
            if (tabs.TryGetValue(g.thingIDNumber, out var t) && now - t.tick < Sched.RareTicks && t.tick <= now) return t;
            if (tabs.Count > 40) tabs.Clear();
            tabs[g.thingIDNumber] = t = new Tab { tick = now };
            try
            {
                BuildTab(g, t);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the guest tab's helping out", e);
                t.lines.Clear();
            }
            return t;
        }

        private static void BuildTab(Pawn g, Tab t)
        {
            var r = Record(g, true);
            if (r == null) return;
            var v = ViewOf(g);
            void Add(string text, Color c) => t.lines.Add((text, c));
            if (r.off)
            {
                Add("CC_GuestHelp_Tab_Off".Translate(g.Named("PAWN")).Resolve(), UiKit.Faint);
                return;
            }
            if (HospitalityWorkOff) Add("CC_GuestHelp_Tab_HospitalityOff".Translate().Resolve(), UiKit.Warn);
            var crises = CrisesOf(g);
            if (crises != HelpCrisis.None)
                Add("CC_GuestHelp_Tab_Crisis".Translate(string.Join(", ", GuestHelpMath.Crises.Where(c => (crises & c) != 0).Select(CrisisWords)).Named("WHAT")).Resolve(), UiKit.Good);
            if (!Everyday) return;
            if (ThanksOn(r))
            {
                var w = DefDatabase<WorkTypeDef>.GetNamedSilentFail(r.thanksWork);
                Add("CC_GuestHelp_Tab_Thanks".Translate((w?.gerundLabel ?? r.thanksWork).Named("WORK"),
                    ("CC_GuestHelp_Why_" + r.thanksWhy).Translate(g.Named("PAWN")).Named("WHY")).Resolve(), UiKit.Good);
            }
            string hours = GuestHelpMath.Hours(r.everyday).ToString("0.#");
            if (r.overworked) Add("CC_GuestHelp_Tab_Overworked".Translate(g.Named("PAWN")).Resolve(), UiKit.Bad);
            else
                Add((GuestHelpMath.CapOpen(r.everyday) ? "CC_GuestHelp_Tab_Today" : "CC_GuestHelp_Tab_TodayDone").Translate(hours.Named("HOURS"), GuestHelpMath.CapHours.Named("CAP"))
                    .Resolve(), GuestHelpMath.CapOpen(r.everyday) ? UiKit.Faint : UiKit.Warn);
            if (g.needs?.mood != null && GuestHelpMath.TooUnhappy(g.needs.mood.CurLevel) && crises == HelpCrisis.None && !ThanksOn(r))
                Add("CC_GuestHelp_Tab_Mood".Translate(g.needs.mood.CurLevel.ToStringPercent().Named("NOW"), GuestHelpMath.HappyEnough.ToStringPercent().Named("NEED"), g.Named("PAWN"))
                    .Resolve(), UiKit.Faint);
            // What they'd help with and why, then what they won't.
            if (v.calling == PeoplesMath.Mercenaries)
                Add("CC_GuestHelp_Tab_Hire".Translate(PeopleName(g).Named("PEOPLE")).Resolve(), UiKit.Warn);
            int min = HospitalityMinSkill;
            int fits = 0;
            foreach (var kv in v.fit.Where(x => x.Value != HelpFit.None).OrderByDescending(x => x.Key.naturalPriority))
            {
                var wt = kv.Key;
                if (g.WorkTypeIsDisabled(wt) || RefusalOf(v, wt) != HelpRefusal.None) continue;
                fits++;
                string text = FitText(g, v, wt, kv.Value);
                bool low = wt.relevantSkills != null && wt.relevantSkills.Count > 0 && g.skills != null && g.skills.AverageOfRelevantSkillsFor(wt) < min;
                if (low) text += " " + "CC_GuestHelp_Tab_LowSkill".Translate(min.Named("NEED")).Resolve();
                Add(text, low ? UiKit.Faint : UiKit.Good);
            }
            if (fits == 0 && v.calling != PeoplesMath.Mercenaries) Add("CC_GuestHelp_Tab_NoFits".Translate(g.Named("PAWN")).Resolve(), UiKit.Faint);
            foreach (var kv in v.refuse.Where(x => x.Value != HelpRefusal.None && x.Value != HelpRefusal.Hire && x.Value != HelpRefusal.Never).OrderByDescending(x => x.Key.naturalPriority))
            {
                if (g.WorkTypeIsDisabled(kv.Key)) continue;
                Add(RefusalText(g, v, kv.Key, kv.Value), UiKit.Warn);
            }
            if (v.noTrees) Add(("CC_GuestHelp_Tab_NoTrees_" + (v.calling == PeoplesMath.Grovekeepers ? "Calling" : "Faith")).Translate(PeopleName(g).Named("PEOPLE")).Resolve(), UiKit.Warn);
            Add("CC_GuestHelp_Tab_Never".Translate().Resolve(), UiKit.Faint);
        }

        private static string FitText(Pawn g, View v, WorkTypeDef wt, HelpFit fit)
        {
            string work = wt.gerundLabel.CapitalizeFirst();
            switch (fit)
            {
                case HelpFit.Calling:
                    return "CC_GuestHelp_Fit_Calling".Translate(work.Named("WORK"), PeopleName(g).Named("PEOPLE")).Resolve();
                case HelpFit.Faith:
                {
                    string precept = GuestHelpMath.FaithFor(wt.defName, v.precepts);
                    var pd = precept != null ? DefDatabase<PreceptDef>.GetNamedSilentFail(precept) : null;
                    return "CC_GuestHelp_Fit_Faith".Translate(work.Named("WORK"), (pd?.LabelCap.ToString() ?? precept ?? "?").Named("PRECEPT")).Resolve();
                }
                default:
                    return ("CC_GuestHelp_Fit_" + fit).Translate(work.Named("WORK")).Resolve();
            }
        }

        private static string RefusalText(Pawn g, View v, WorkTypeDef wt, HelpRefusal why)
        {
            string work = wt.gerundLabel;
            switch (why)
            {
                case HelpRefusal.Titled:
                    return "CC_GuestHelp_No_Titled".Translate(work.Named("WORK"), (v.title ?? "?").Named("TITLE")).Resolve();
                case HelpRefusal.Faith:
                {
                    string precept = GuestHelpMath.FaithAgainst(wt.defName, v.precepts);
                    var pd = precept != null ? DefDatabase<PreceptDef>.GetNamedSilentFail(precept) : null;
                    return "CC_GuestHelp_No_Faith".Translate(work.Named("WORK"), (pd?.LabelCap.ToString() ?? precept ?? "?").Named("PRECEPT")).Resolve();
                }
                default:
                    return ("CC_GuestHelp_No_" + why).Translate(work.Named("WORK"), g.Named("PAWN")).Resolve();
            }
        }
    }

    /// <summary>
    /// Rimfolk's step in a Hospitality guest's day (Defs/Misc/Rimfolk_GuestHelp.xml, on the game's Humanlike_PreMain hook,
    /// which Hospitality's guest duty offers before their needs): while the colony is in trouble they pitch in to, they
    /// carry the downed in and do the trouble's work before their own comforts. Nothing for anyone else.
    /// </summary>
    public class ThinkNode_GuestPitchIn : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            try
            {
                return GuestHelp.PitchingIn(pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest pitching in", e);
                return false;
            }
        }
    }

    /// <summary>
    /// A guest carries someone of the colony who is down outside a bed to a bed, in a rescue they pitch in to (the game's
    /// own rescue job; its work giver only rescues one's own people). The game's own rule: not while an enemy is near them.
    /// </summary>
    public class JobGiver_GuestRescue : ThinkNode_JobGiver
    {
        private static WorkGiverDef rescue;

        protected override Job TryGiveJob(Pawn pawn)
        {
            try
            {
                if ((GuestHelp.CrisesOf(pawn) & HelpCrisis.Rescue) == HelpCrisis.None) return null;
                if (pawn.WorkTagIsDisabled(WorkTags.Caring) || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return null;
                var map = pawn.Map;
                var downed = map.mapPawns.FreeColonistsSpawned.Concat(map.mapPawns.SlavesOfColonySpawned)
                    .Where(p => p.Downed && !p.Dead && !p.InBed()).OrderBy(p => p.Position.DistanceToSquared(pawn.Position)).ToList();
                foreach (var d in downed)
                {
                    if (GenAI.EnemyIsNear(d, 40f) || !pawn.CanReserveAndReach(d, PathEndMode.OnCell, Danger.Some)) continue;
                    var bed = RestUtility.FindBedFor(d, pawn, false, false);
                    if (bed == null || !d.CanReserve(bed)) continue;
                    var job = JobMaker.MakeJob(JobDefOf.Rescue, d, bed);
                    job.count = 1;
                    rescue ??= DefDatabase<WorkGiverDef>.GetNamedSilentFail("DoctorRescue");
                    job.workGiverDef = rescue;
                    var r = GuestHelp.Record(pawn, false);
                    if (r != null) GuestHelp.Pitched(pawn, r, HelpCrisis.Rescue);
                    return job;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest carrying someone in", e);
            }
            return null;
        }
    }

    // ---------------- The game's and Hospitality's hooks ----------------

    /// <summary>
    /// A guest's work givers (after Hospitality's own prefix, which decides for its guests): narrowed in everyday times to
    /// work they love, opened in trouble and on a thanks day (GuestHelp.CanUse). Only with Hospitality.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
    public static class Patch_GuestHelpWorkGiver
    {
        public static bool Prepare() => GuestHelp.HospitalityHere;

        public static string StandsDownWhy() => GuestHelp.HospitalityHere ? "" : "Hospitality isn't loaded";

        public static void Postfix(Pawn pawn, WorkGiver giver, ref bool __result)
        {
            var f = pawn?.Faction;
            if (f == null || f.IsPlayer || !GuestHelp.Any) return;
            try
            {
                __result = GuestHelp.CanUse(pawn, giver, __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest's work", e);
            }
        }
    }

    /// <summary>A guest's work job chosen: no private rooms, the dead or prisoners, and why they took it up (GuestHelp.JobChosen). Only with Hospitality.</summary>
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    public static class Patch_GuestHelpJob
    {
        public static bool Prepare() => GuestHelp.HospitalityHere;

        public static string StandsDownWhy() => GuestHelp.HospitalityHere ? "" : "Hospitality isn't loaded";

        public static void Postfix(Pawn pawn, ref ThinkResult __result)
        {
            if (!__result.IsValid) return;
            var f = pawn?.Faction;
            if (f == null || f.IsPlayer || !GuestHelp.Any) return;
            try
            {
                GuestHelp.JobChosen(pawn, ref __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest's work chosen", e);
            }
        }
    }

    /// <summary>Hospitality's "wants to help" (its mood line), by name: open in trouble and on a thanks day (GuestHelp.WantsToHelp).</summary>
    [HarmonyPatch]
    public static class Patch_GuestHelpWantsToHelp
    {
        public static bool Prepare() => Target() != null;

        public static string StandsDownWhy() => Target() == null ? "Hospitality isn't loaded" : "";

        public static MethodBase TargetMethod() => Target();

        private static MethodBase Target() =>
            GenTypes.GetTypeInAnyAssembly("Hospitality.ThinkNode_ConditionalWantsToHelp") is Type t ? AccessTools.Method(t, "Satisfied", new[] { typeof(Pawn) }) : null;

        public static void Postfix(Pawn __0, ref bool __result)
        {
            if (!GuestHelp.Any) return;
            try
            {
                __result = GuestHelp.WantsToHelp(__0, __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest wanting to help", e);
            }
        }
    }
}
