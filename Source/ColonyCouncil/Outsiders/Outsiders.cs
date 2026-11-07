using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class OutsiderDefOf
    {
        public static JobDef Council_OutsiderTalk;
        public static JobDef Council_OutsiderListen;
        public static JobDef Council_OutsiderGift;
        public static JobDef Council_OutsiderLesson;
        public static JobDef Council_OutsiderTeach;
        public static JobDef Council_OutsiderBout;
        public static JobDef Council_OutsiderWatch;
        public static JobDef Council_OutsiderBring;
        public static JobDef Council_OutsiderMeal;
        public static ThoughtDef Rimfolk_OutsiderTalk;
        public static ThoughtDef Rimfolk_OutsiderGift;
        public static ThoughtDef Rimfolk_OutsiderMeal;
        public static ThoughtDef Rimfolk_OutsiderTaught;
        public static ThoughtDef Rimfolk_OutsiderBout;
        public static ThoughtDef Rimfolk_OutsiderFriend;
        public static ThoughtDef Rimfolk_OutsiderSweet;
        public static ThoughtDef Rimfolk_OutsiderInsulted;
        public static ThoughtDef Rimfolk_SawDuel;

        static OutsiderDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(OutsiderDefOf));
        }
    }

    /// <summary>
    /// Talking with people from outside the colony, the way Crusader Kings' character menu does it: with a colonist
    /// selected, right-clicking someone who isn't of the colony and isn't hostile (a visitor, a trader, a Hospitality
    /// guest, someone of a camp, a messenger, an envoy, a quest's lodger) offers "Talk with X...", a small menu of
    /// things to do together. Each plays out on the map: the colonist walks over, both stop and face each other and
    /// talk, and what comes of it follows both their natures, how the two get on (Social/Bonds.cs), how well they know
    /// each other from before, and what the outsider's people think of the colony (goodwill, the peoples' memory, a
    /// notable's own regard). Everything is remembered: on the pair's record (OutsiderBook), and by the outsider as a
    /// notable (World/Notables.cs), or as a face the colony met once they're home. The outcomes, meals and bouts are in
    /// OutsiderOutcomes.cs, the jobs in OutsiderJobs.cs, the menu and pickers in OutsiderMenu.cs.
    /// </summary>
    public static partial class Outsiders
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The menu and the everyday things (the setting).</summary>
        public static bool On => S?.outsiderTalk ?? true;
        /// <summary>A practice bout (the setting): blunt, no instigator, so it needs no deeper hooks.</summary>
        public static bool SparOn => On && (S?.outsiderSpar ?? true);
        /// <summary>Hard words (the setting): a fistfight needs the deeper hooks, so their people don't count each blow as an attack.</summary>
        public static bool ProvokeOn => On && (S?.outsiderProvoke ?? true) && RimfolkPatches.On;
        /// <summary>A duel of honour (the setting), with the deeper hooks for the same reason.</summary>
        public static bool DuelOn => On && (S?.outsiderDuels ?? true) && RimfolkPatches.On;

        public const string Pool = "Rimfolk_Pool_OutsiderSays";

        private static int Now => Find.TickManager.TicksGame;

        private static OutsiderBook Book => OutsiderBook.Instance;

        /// <summary>A new game or a load: nothing under way carries over (meals, bouts and brawls aren't saved).</summary>
        public static void Reset()
        {
            meals.Clear();
            bouts.Clear();
            OutsiderTalk.Reset();
        }

        // ---------------- Who is an outsider ----------------

        /// <summary>
        /// Someone the colony can talk with this way: a person on the map who isn't the colony's own (a quest's lodger
        /// counts), held by it, or hostile to it.
        /// </summary>
        public static bool IsOutsider(Pawn x)
        {
            if (x == null || x.Dead || !x.Spawned || x.RaceProps == null || !x.RaceProps.Humanlike) return false;
            if (x.IsMutant || x.DevelopmentalStage.Baby()) return false;
            if (x.IsPrisoner || x.IsSlave) return false;
            if (x.Faction == Faction.OfPlayer && !x.IsQuestLodger()) return false;
            if (x.HostileTo(Faction.OfPlayer)) return false;
            return x.Faction != null || x.IsQuestLodger();
        }

        /// <summary>Their people: their faction, or a lodger's home faction.</summary>
        public static Faction PeopleOf(Pawn x)
        {
            if (x == null) return null;
            var f = x.Faction;
            if (f != null && !f.IsPlayer) return f;
            try
            {
                var h = x.GetExtraHomeFaction() ?? x.HomeFaction;
                return h != null && !h.IsPlayer ? h : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Their record as a notable, if the colony knows them by name.</summary>
        public static Notable NotableOf(Pawn x)
        {
            if (x == null || !NotableBook.On) return null;
            return NotableBook.Instance?.notables.FirstOrDefault(n => n.pawn == x);
        }

        /// <summary>How they regard the colony: their own regard as a notable, else as their people's goodwill and memory and their nature make it.</summary>
        public static int OfColony(Pawn x)
        {
            var n = NotableOf(x);
            if (n != null) return NotableBook.Opinion(n);
            var f = PeopleOf(x);
            if (f == null) return 0;
            int goodwill = f.HasGoodwill ? f.PlayerGoodwill : 0;
            float favor = 0f, wrong = 0f, owed = 0f;
            var mem = FactionMemory.Instance;
            if (mem != null && FactionMemory.On)
            {
                favor = mem.Sum(f, DeedSide.Favor);
                wrong = mem.Sum(f, DeedSide.Wrong);
                owed = mem.Sum(f, DeedSide.TheirFavor, true);
            }
            return NotableMath.Baseline(goodwill, favor, wrong, owed, NotableBook.Warmth(x), f.HostileTo(Faction.OfPlayer));
        }

        /// <summary>How they regard the colonist: the game's opinion, and a little of the bond between the two from before.</summary>
        public static int OfColonist(Pawn x, Pawn c)
        {
            if (x?.relations == null || c == null) return 0;
            int op;
            try
            {
                op = x.relations.OpinionOf(c);
            }
            catch
            {
                op = 0;
            }
            var pair = Book?.Pair(c, x, false);
            return NotableMath.Clamp(op + Mathf.RoundToInt((pair?.warmth ?? 0f) * 0.25f), -100, 100);
        }

        /// <summary>What a talk between the two is worked out from.</summary>
        public static TalkInput Input(Pawn c, Pawn x, OutsiderAct act)
        {
            var pair = Book?.Pair(c, x, false);
            return new TalkInput
            {
                act = act, affinity = Affinity(c, x), ofColony = OfColony(x), ofColonist = OfColonist(x, c), social = c.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 4,
                warmth = NotableBook.Warmth(x), familiarity = pair?.fam ?? 0f
            };
        }

        public static float Affinity(Pawn c, Pawn x)
        {
            try
            {
                return Bonds.Affinity(c, x);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>The town they come from: their people's town nearest the colony.</summary>
        public static Settlement TownOf(Pawn x)
        {
            var n = NotableOf(x);
            var t = n != null ? NotableBook.TownOf(n.town) : null;
            if (t != null) return t;
            var map = x?.MapHeld ?? HappeningTracker.HomeMap();
            return WorldSituationWorker.NearestTown(PeopleOf(x), map);
        }

        /// <summary>"Ana Bell, a trader from Kettleford (the Rough Riders)": their whole name, what they are, and where they're from.</summary>
        public static string Called(Pawn x)
        {
            var n = NotableOf(x);
            string role = n != null ? NotableBook.TitleOf(n) : RoleOf(x);
            var f = PeopleOf(x);
            string town = TownOf(x)?.Name;
            string name = x.Name?.ToStringFull ?? x.LabelShort;
            if (f == null) return "CC_Out_Called_Plain".Translate(name.Named("NAME"), role.Named("ROLE")).Resolve();
            if (town.NullOrEmpty()) return "CC_Out_Called_NoTown".Translate(name.Named("NAME"), role.Named("ROLE"), f.Name.Named("FACTION")).Resolve();
            return "CC_Out_Called".Translate(name.Named("NAME"), role.Named("ROLE"), town.Named("TOWN"), (f?.Name ?? "").Named("FACTION")).Resolve();
        }

        /// <summary>What they are, in a word or two: a guest, a trader, a lodger, else their kind ("caravan guard", "villager").</summary>
        public static string RoleOf(Pawn x)
        {
            if (Strangers.IsHospitalityGuest(x)) return "CC_Out_Role_Guest".Translate().Resolve();
            if (x.IsQuestLodger()) return "CC_Out_Role_Lodger".Translate().Resolve();
            if (x.TraderKind != null) return "CC_Out_Role_Trader".Translate().Resolve();
            string k = x.kindDef?.label;
            return k.NullOrEmpty() ? "CC_Out_Role_Visitor".Translate().Resolve() : k;
        }

        /// <summary>How the colony met them, for their record as a face it knows once they're home.</summary>
        public static string MetKind(Pawn x)
        {
            if (Strangers.IsHospitalityGuest(x)) return "guest";
            if (x.TraderKind != null) return "trader";
            return "visitor";
        }

        /// <summary>A people of tribes or medieval ways (letters' and lines' "theirLowtech").</summary>
        public static bool Lowtech(Pawn x) => NeighbourLetters.Traditional(PeopleOf(x));

        // ---------------- Temper ----------------

        private static bool Has(Pawn p, TraitDef t) => t != null && (p?.story?.traits?.HasTrait(t) ?? false);

        private static bool HasNamed(Pawn p, string defName, int degree = int.MinValue)
        {
            var traits = p?.story?.traits?.allTraits;
            if (traits == null) return false;
            foreach (var t in traits)
                if (t?.def?.defName == defName && (degree == int.MinValue || t.Degree == degree))
                    return true;
            return false;
        }

        /// <summary>
        /// A fighting temper (-2 to 4), read from their character (PersonaMath.FightTemper: temper and nerve up, warmth down),
        /// the one reading of who someone is, which their traits set.
        /// </summary>
        public static int Temper(Pawn p) => p?.story == null ? 0 : PersonaMath.FightTemper(Persona.Values(p));

        public static int Melee(Pawn p) => p?.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;

        // ---------------- Why not ----------------

        private static string Why(string key, params NamedArgument[] args) => ("CC_Out_Why_" + key).Translate(args).Resolve();

        /// <summary>Why the outsider won't stop for this now, or null.</summary>
        public static string Unwilling(Pawn x, Pawn c, OutsiderAct act)
        {
            string why = Busy(x, c, act);
            if (why != null) return why;
            if (OutsiderMath.Shuns(OfColony(x), OfColonist(x, c))) return Why("Shuns", c.Named("PAWN"));
            return null;
        }

        /// <summary>Why they can't stop for anything now (down, asleep, leaving, busy with their own), whoever asks; or null.</summary>
        public static string Busy(Pawn x, Pawn c, OutsiderAct act)
        {
            if (x == null || x.Dead || !x.Spawned) return Why("Gone");
            if (x.Downed) return Why("Downed");
            if (!x.Awake()) return Why("Asleep");
            if (x.InMentalState) return Why("Upset");
            if (x.Map.dangerWatcher.DangerRating != StoryDanger.None) return Why("Danger");
            if (Leaving(x)) return Why("Leaving");
            if (TradingNow(x)) return Why("Trading");
            if (x.carryTracker?.CarriedThing != null) return Why("HandsFull");
            var job = x.CurJob;
            if (job != null)
            {
                var d = job.def;
                bool withC = job.targetA.Thing == c;
                if ((d == JobDefOf.Ingest || d == OutsiderDefOf.Council_OutsiderMeal) && act != OutsiderAct.Meal && act != OutsiderAct.Drink) return Why("Eating");
                if ((d == OutsiderDefOf.Council_OutsiderListen || d == OutsiderDefOf.Council_OutsiderTeach || d == OutsiderDefOf.Council_OutsiderBout
                     || d == OutsiderDefOf.Council_OutsiderBring || d == OutsiderDefOf.Council_OutsiderWatch) && !withC)
                    return Why("Busy");
                if (d == JobDefOf.LayDown || d == JobDefOf.LayDownResting || d == JobDefOf.Lovin || d == JobDefOf.Rescue || d == JobDefOf.TendPatient) return Why("Busy");
            }
            return null;
        }

        /// <summary>
        /// In a trade with the colony right now: the game's trade session names them and a trade window is open. The
        /// session alone isn't enough: it can stay set after the window closes (the player's report: a trader stayed
        /// "trading" after the trade ended), so the open window is what counts.
        /// </summary>
        public static bool TradingNow(Pawn x)
        {
            if (!TradeSession.Active || !(TradeSession.trader is Pawn t) || t != x) return false;
            var windows = Find.WindowStack?.Windows;
            if (windows == null) return false;
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                if (w is Dialog_Trade || (w != null && w.GetType().Name.IndexOf("Trade", StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            }
            return false;
        }

        /// <summary>On their way out: their group leaving, fleeing or carrying off their wounded, or walking off the map.</summary>
        public static bool Leaving(Pawn x)
        {
            try
            {
                if (x.CurJob != null && x.CurJob.exitMapOnArrival) return true;
                string duty = x.mindState?.duty?.def?.defName ?? "";
                if (duty.Contains("Exit") || duty.Contains("Flee") || duty.Contains("Escort")) return true;
                var toil = x.GetLord()?.CurLordToil;
                if (toil != null)
                {
                    string n = toil.GetType().Name;
                    if (n.Contains("Exit") || n.Contains("Leave") || n.Contains("Flee") || n.Contains("Panic") || n.Contains("TakeWounded")) return true;
                }
            }
            catch
            {
                // Some other mod's lord: don't hold anyone up for it.
            }
            return false;
        }

        /// <summary>Why the colonist can't go and do anything with them now, or null.</summary>
        public static string ColonistWhyNot(Pawn c, Pawn x)
        {
            if (c == null || !c.Spawned || c.Map != x?.Map) return Why("Gone");
            if (c.Downed || c.InMentalState) return Why("NotNow", c.Named("PAWN"));
            if (!(c.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) ?? false)) return Why("CantTalk", c.Named("PAWN"));
            if (!c.CanReach(x, PathEndMode.Touch, Danger.Some)) return Why("NoReach");
            return null;
        }

        /// <summary>Whether this belongs in the menu at all (a whole kind of thing that can't be: a DLC, a setting, a people's ways).</summary>
        public static bool Shown(Pawn c, Pawn x, OutsiderAct act)
        {
            bool adults = !c.DevelopmentalStage.Juvenile() && !x.DevelopmentalStage.Juvenile();
            switch (act)
            {
                case OutsiderAct.GoodWord:
                case OutsiderAct.Letter: return NotableBook.On && adults && (act != OutsiderAct.Letter || NeighbourLetters.On) && PeopleOf(x) != null;
                case OutsiderAct.Teach:
                case OutsiderAct.Gift: return PeopleOf(x) != null || x.IsQuestLodger();
                case OutsiderAct.Bring: return PeopleOf(x) != null && !x.IsQuestLodger();
                case OutsiderAct.Invite: return adults && !Strangers.IsHospitalityGuest(x) && !x.IsQuestLodger() && x.Faction != null && !x.Faction.IsPlayer;
                case OutsiderAct.Court: return adults;
                case OutsiderAct.Faith: return ModsConfig.IdeologyActive && c.Ideo != null && x.Ideo != null;
                case OutsiderAct.Spar: return SparOn && adults;
                case OutsiderAct.Provoke: return ProvokeOn && adults;
                case OutsiderAct.Duel:
                {
                    if (!DuelOn || !adults) return false;
                    var f = PeopleOf(x);
                    if (f == null) return false;
                    bool titled = x.royalty != null && x.royalty.AllTitlesInEffectForReading.Count > 0;
                    return OutsiderMath.DuelFits((int)f.def.techLevel, f.def.HasRoyalTitles, titled);
                }
                default:
                    if (DiplomacyActs.Owns(act)) return DiplomacyActs.Shown(c, x, act);
                    return true;
            }
        }

        /// <summary>Why the colonist can't do this with them now (the menu shows it greyed with this), or null.</summary>
        public static string WhyNot(Pawn c, Pawn x, OutsiderAct act)
        {
            string why = ColonistWhyNot(c, x) ?? Unwilling(x, c, act);
            if (why != null) return why;
            var book = Book;
            int now = Now;
            // They've had enough of the colony for now (OutsiderTalk.cs): patience comes back with time, or on their next visit.
            why = OutsiderTalk.Tired(x, act);
            if (why != null) return why;
            // An ask made again within its wait gets "you asked me that already" (the menu says so); the rest wait.
            int last = OutsiderMath.PerOutsider(act) ? book?.LastWith(x, act) ?? -1 : book?.Pair(c, x, false)?.Last(act) ?? -1;
            if (!OutsiderMath.Waited(last, now, act) && !OutsiderTalkMath.Asks(act)) return Why(OutsiderMath.PerOutsider(act) ? "LatelyAnyone" : "Lately");
            int ofColony = OfColony(x), ofColonist = OfColonist(x, c);
            switch (act)
            {
                case OutsiderAct.AskHome:
                    if (TownOf(x) == null) return Why("NoHome");
                    break;
                case OutsiderAct.Meal:
                case OutsiderAct.Drink:
                    if (!Portions(c, x, act == OutsiderAct.Drink, out _, out _)) return Why(act == OutsiderAct.Drink ? "NoDrink" : "NoFood");
                    break;
                case OutsiderAct.Gift:
                    if (!GiftChoices(c, x).Any()) return Why("NoGoods");
                    break;
                case OutsiderAct.GoodWord:
                    if (WordTo(x) == null) return Why("NoNotable");
                    if (book != null && GoodWords.PromisedBy(x)) return Why("Promised");
                    if (!OutsiderMath.WillPutInWord(ofColony, ofColonist)) return Why("NotWell");
                    break;
                case OutsiderAct.Letter:
                {
                    var to = LetterTo(x);
                    if (to == null) return Why("NoNotable");
                    if (NeighbourLetters.OpenTo(to) != null) return Why("OneLetter", to.Name.Named("NAME"));
                    if (NeighbourLetters.OpenCount >= NotableMath.MaxOpen) return Why("TooManyLetters");
                    break;
                }
                case OutsiderAct.Teach:
                    if (TeachSkill(c, x) == null) return Why("NoSkill", c.Named("PAWN"));
                    if (!OutsiderMath.WillDoFavour(ofColony, ofColonist)) return Why("NotWell");
                    break;
                case OutsiderAct.Bring:
                    if (book != null && Favours.AskedOf(PeopleOf(x))) return Why("Asked");
                    if (book != null && Favours.Count >= OutsiderBook.MaxFavours) return Why("TooManyAsked");
                    if (BringChoices(x).Count == 0) return Why("NoBring");
                    if (!OutsiderMath.WillBring(ofColony, ofColonist)) return Why("NotWell");
                    break;
                case OutsiderAct.Invite:
                {
                    if (PawnUtility.IsFactionLeader(x)) return Why("Leader");
                    var will = GuestWill.Of(x);
                    if (will.noRoom.Count > 0) return Why("NoRoom", will.noRoom[0].Named("WHY"));
                    if (Reasons(x, c) == 0) return Why("NoReason");
                    break;
                }
                case OutsiderAct.Befriend:
                    if (book?.Pair(c, x, false)?.friends ?? false) return Why("Friends");
                    break;
                case OutsiderAct.Court:
                {
                    if (LovePartnerRelationUtility.LovePartnerRelationExists(c, x)) return Why("Lovers");
                    var a = RelationsUtility.RomanceEligiblePair(c, x, false);
                    if (!a.Accepted) return a.Reason.NullOrEmpty() ? Why("NotDrawn") : a.Reason;
                    var b = RelationsUtility.RomanceEligiblePair(x, c, false);
                    if (!b.Accepted) return Why("NotDrawnThem");
                    if (Beliefs.Forbids(c, Belief.NoLovin) || Beliefs.Forbids(x, Belief.NoLovin)) return Why("Belief");
                    if ((book?.Pair(c, x, false)?.fam ?? 0f) < OutsiderMath.CourtKnowing) return Why("Strangers");
                    break;
                }
                case OutsiderAct.Spar:
                    if (Peaceful(c)) return Why("Peaceful", c.Named("PAWN"));
                    if (Peaceful(x)) return Why("PeacefulThem");
                    if (Hurt(c) || Hurt(x)) return Why("Hurt");
                    break;
                case OutsiderAct.Duel:
                    if (Peaceful(c)) return Why("Peaceful", c.Named("PAWN"));
                    if (Peaceful(x)) return Why("PeacefulThem");
                    if (Hurt(c) || Hurt(x)) return Why("Hurt");
                    break;
                default:
                    if (DiplomacyActs.Owns(act)) return DiplomacyActs.WhyNot(c, x, act);
                    break;
            }
            return null;
        }

        /// <summary>Someone who won't fight: incapable of violence, or unable to swing.</summary>
        private static bool Peaceful(Pawn p) =>
            p.WorkTagIsDisabled(WorkTags.Violent) || !(p.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false)
            || !(p.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) ?? false);

        /// <summary>Already hurt enough that a bout would be no sport.</summary>
        private static bool Hurt(Pawn p) => (p.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.8f || (p.health?.hediffSet?.BleedRateTotal ?? 0f) > 0f;

        // ---------------- Starting ----------------

        /// <summary>The colonist walks over to talk about <paramref name="act"/> (with <paramref name="extra"/>: duel terms; <paramref name="thing"/>: what to bring).</summary>
        public static void StartTalk(Pawn c, Pawn x, OutsiderAct act, int extra = 0, ThingDef thing = null)
        {
            if (c?.jobs == null || x == null) return;
            var job = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderTalk, x);
            job.count = (int)act + 100 * extra;
            job.thingDefToCarry = thing;
            job.locomotionUrgency = act == OutsiderAct.Provoke ? LocomotionUrgency.Jog : LocomotionUrgency.Walk;
            c.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        /// <summary>The colonist fetches the gift from the stores and hands it over.</summary>
        public static void StartGift(Pawn c, Pawn x, Thing thing, int count)
        {
            if (c?.jobs == null || x == null || thing == null) return;
            var job = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderGift, x, thing);
            job.count = Mathf.Max(1, count);
            job.haulMode = HaulMode.ToCellNonStorage;
            c.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        /// <summary>
        /// Holds the outsider where they are, facing the colonist, for up to <paramref name="ticks"/> (or until the
        /// colonist's job with them ends). False when they couldn't be stopped.
        /// </summary>
        public static bool Hold(Pawn x, Pawn c, int ticks)
        {
            if (x?.jobs == null || c == null) return false;
            if (Holding(x, c)) return true;
            var job = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderListen, c);
            job.count = Mathf.Max(300, ticks);
            x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return Holding(x, c);
        }

        public static bool Holding(Pawn x, Pawn c) => x?.CurJob != null && x.CurJobDef == OutsiderDefOf.Council_OutsiderListen && x.CurJob.targetA.Thing == c;

        /// <summary>The colonist is still at a job with this outsider (the listener's job ends when it does).</summary>
        public static bool WithMe(Pawn c, Pawn x)
        {
            var job = c?.CurJob;
            if (job == null) return false;
            var d = job.def;
            return (d == OutsiderDefOf.Council_OutsiderTalk || d == OutsiderDefOf.Council_OutsiderGift || d == OutsiderDefOf.Council_OutsiderLesson) && job.targetA.Thing == x;
        }

        // ---------------- Words ----------------

        /// <summary>
        /// A line over someone's head at a moment of a talk with an outsider (one sentence, from Rimfolk_Pool_OutsiderSays),
        /// said to <paramref name="other"/>. {NAME} is the other's name, {TOWN} the outsider's town, {ASKER} who asked before;
        /// <paramref name="also"/> a tag of the moment only its caller knows (a house's name fallen from the region's band,
        /// what is on the plate at a meal). Lines can ask for the outsider's people by age (their:tribal... their:empire) and
        /// for how well the two know each other (first, met, known).
        /// </summary>
        public static bool Say(Pawn p, string moment, Pawn other, Pawn asker = null, string also = null)
        {
            if (p == null || !p.Spawned) return false;
            string line = Line(p, moment, other, asker, also);
            if (line.NullOrEmpty()) return false;
            // together: said only to someone standing with them (Speech.Say checks it).
            return Speech.Say(p, line, other);
        }

        /// <summary>The line <paramref name="p"/> would say at that moment (for a scene said in turn), or null.</summary>
        public static string Line(Pawn p, string moment, Pawn other, Pawn asker = null, string also = null)
        {
            if (p == null || moment.NullOrEmpty()) return null;
            try
            {
                var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(Pool);
                if (pool == null) return null;
                var tags = Flavor.TagsFor(p);
                tags.Add(moment);
                if (!also.NullOrEmpty()) tags.Add(also);
                // Who speaks: one of the colony's ("us") or the outsider ("them"); a line for either has neither tag.
                tags.Add(KinMemory.Ours(p) ? "us" : "them");
                var x = KinMemory.Ours(p) ? other : p;
                var people = x != null ? PeopleOf(x) : null;
                if (people != null)
                {
                    tags.Add(Lowtech(x) ? "theirLowtech" : "theirHightech");
                    // Their people's ways by age, and the Empire apart (its own titles and honour).
                    tags.Add("their:" + Ages.Tag(Ages.Of(people)));
                    if (Ages.IsEmpire(people)) tags.Add("their:empire");
                    // One of Rimfolk's peoples, by what it lives by: their:traders, their:healers, their:herders (Peoples/).
                    Peoples.AddTags(people, tags);
                }
                // How well the two know each other: never talked (first), met before (met), on a visit since the last
                // (returning), known (familiar enough).
                var c = KinMemory.Ours(p) ? p : other;
                var pair = c != null && x != null ? Book?.Pair(c, x, false) : null;
                if (pair == null || pair.talks == 0 && pair.lastTick < 0) tags.Add("first");
                else
                {
                    tags.Add("met");
                    if (pair.lastTick >= 0 && OutsiderMath.NewVisit(pair.lastTick, Now)) tags.Add("returning");
                    if (pair.fam >= 30f) tags.Add("known");
                }
                if (Has(p, TraitDefOf.Kind)) tags.Add("kind");
                if (Has(p, TraitDefOf.Abrasive)) tags.Add("abrasive");
                // The colony's name (Diplomacy/Renown.cs): colony:known, colony:renowned, colony:knownfor:KIND.
                Renown.AddTags(tags, KinMemory.Ours(p) ? p : other);
                var lines = pool.lines.For(ToneUtility.ToneFor(p)).Where(l => LineTags.Of(l).tags?.Contains(moment) ?? false).ToList();
                var plain = pool.lines.plain.Where(l => LineTags.Of(l).tags?.Contains(moment) ?? false).ToList();
                // Not a line given at this moment lately while another fits (two traders greeted in a row).
                string key = Pool + ":" + moment;
                var recent = Flavor.Memory.Recent(key);
                string line = Flavor.PickTagged(lines, tags, recent) ?? Flavor.PickTagged(lines, tags)
                              ?? Flavor.PickTagged(plain, tags, recent) ?? Flavor.PickTagged(plain, tags);
                if (line.NullOrEmpty()) return null;
                Flavor.Memory.Remember(key, line, Math.Max(lines.Count, plain.Count));
                return Texts.Fill(line, ("NAME", other?.LabelShort ?? ""), ("TOWN", (x != null ? TownOf(x)?.Name : null) ?? ""), ("ASKER", asker?.LabelShort ?? ""));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider words", e);
                return null;
            }
        }

        /// <summary>The colonist's opening line for each thing.</summary>
        public static string Opening(OutsiderAct a)
        {
            switch (a)
            {
                case OutsiderAct.Chat: return "greet";
                case OutsiderAct.AskHome: return "homeask";
                case OutsiderAct.Meal: return "mealask";
                case OutsiderAct.Drink: return "drinkask";
                case OutsiderAct.GoodWord: return "wordask";
                case OutsiderAct.Letter: return "letter";
                case OutsiderAct.Teach: return "teachask";
                case OutsiderAct.Bring: return "bringask";
                case OutsiderAct.Invite: return "inviteask";
                case OutsiderAct.Befriend: return "befriend";
                case OutsiderAct.Court: return "court";
                case OutsiderAct.Faith: return "faith";
                case OutsiderAct.Spar: return "sparask";
                case OutsiderAct.Provoke: return "provoke";
                case OutsiderAct.Duel: return "duelask";
                default: return DiplomacyActs.Owns(a) ? DiplomacyActs.Opening(a) : "greet";
            }
        }

        /// <summary>The moment for the lines while they talk (either of them).</summary>
        public static string During(OutsiderAct a)
        {
            switch (a)
            {
                case OutsiderAct.AskHome: return "home";
                case OutsiderAct.Faith: return "faithtalk";
                case OutsiderAct.Court: return "courttalk";
                case OutsiderAct.Provoke: return "provoked";
                default: return DiplomacyActs.Owns(a) ? DiplomacyActs.During(a) : "chat";
            }
        }

        // ---------------- Remembering ----------------

        /// <summary>
        /// What passed between the two is remembered: on their record (the bond, how well they know each other, the
        /// visit, the wait), and by the outsider: as a notable (their regard of the colony and the colonist, and a line
        /// of memory), or as a face to be known once they're home. <paramref name="regard"/> is the colonist's share; the
        /// colony gets half.
        /// </summary>
        public static OutsiderPair Remember(Pawn c, Pawn x, OutsiderAct act, string note, float regard, float warmth, bool setLast = true, float knowing = 1f)
        {
            var book = Book;
            int now = Now;
            var pair = book?.Pair(c, x, true);
            if (pair != null)
            {
                if (OutsiderMath.NewVisit(pair.lastTick, now)) pair.visits++;
                pair.lastTick = now;
                // A talk of their own doesn't count as the thing done (the menu's wait is for what the player orders).
                if (setLast) pair.SetLast(act, now);
                pair.warmth = Mathf.Clamp(pair.warmth + warmth, -OutsiderMath.WarmthMax, OutsiderMath.WarmthMax);
                pair.fam = Mathf.Min(OutsiderMath.KnowMax, pair.fam + OutsiderMath.FamiliarityGain(act, Affinity(c, x)) * knowing);
                pair.outsiderName = x.LabelShort;
            }
            var n = NotableOf(x);
            if (n != null)
            {
                n.personal += regard * 0.5f;
                n.RegardOf(c, true).personal += regard;
                if (!note.NullOrEmpty()) n.Remember(note, regard * 0.5f);
                if (pair != null) pair.carried = true;
                NotableBook.Changed();
            }
            else if (NotableBook.On && PeopleOf(x) != null)
            {
                // apart: noted so they're known by face once home, remembering the colonist.
                NotableBook.Notify_Seen(x, MetKind(x), c);
            }
            return pair;
        }

        /// <summary>A line for the notable's memory: "Chatted with Bo at the colony, warmly".</summary>
        public static string Note(OutsiderAct act, Pawn c, string how = null)
        {
            string key = "CC_Out_Note_" + act;
            if (!key.CanTranslate()) return null;
            return key.Translate(c.LabelShort.Named("PAWN"), (how ?? "").Named("HOW")).Resolve();
        }

        public static string ToneWord(TalkTone t) => ("CC_Out_ToneWord_" + t).Translate().Resolve();

        // ---------------- Who a good word or a letter goes to ----------------

        /// <summary>Whom they'd put in a good word with: their town's head, else its heir or envoy, else anyone of their people the colony knows by name.</summary>
        public static Notable WordTo(Pawn x)
        {
            var book = NotableBook.Instance;
            var f = PeopleOf(x);
            if (book == null || f == null) return null;
            var town = TownOf(x);
            return book.notables.Where(n => n.faction == f && n.Alive && n.pawn != x && !n.pawn.Spawned && n.role != NotableRole.Acquaintance)
                .OrderBy(n => town != null && n.town == town.ID ? 0 : 1).ThenBy(n => (int)n.role).FirstOrDefault();
        }

        /// <summary>Whom a letter carried home goes to by default: the same as a good word, or anyone of their people known by name.</summary>
        public static Notable LetterTo(Pawn x)
        {
            var to = WordTo(x);
            if (to != null) return to;
            var f = PeopleOf(x);
            return NotableBook.Instance?.notables.FirstOrDefault(n => n.faction == f && n.Alive && n.pawn != x && !n.pawn.Spawned);
        }

        // ---------------- The hour ----------------

        /// <summary>Good words land once the one who promised is home (gone from the map into the world).</summary>
        public static void WordsHourly(OutsiderBook book, int now)
        {
            var open = GoodWords.Open();
            if (open.Count == 0) return;
            foreach (var w in open)
            {
                var x = w.outsider;
                if (x == null || x.Dead || x.Discarded || now - w.asked > OutsiderMath.GoodWordDays * GenDate.TicksPerDay)
                {
                    GoodWords.Close(w, false);
                    continue;
                }
                if (x.Spawned || !Find.WorldPawns.Contains(x)) continue;
                var n = NotableBook.Instance?.Get(w.notable);
                GoodWords.Close(w, n != null && n.Alive);
                if (n == null || !n.Alive) continue;
                n.personal += w.shift;
                if (KinMemory.Ours(w.by)) n.RegardOf(w.by, true).personal += w.shift * 0.7f;
                n.Remember("CC_Out_Note_WordLanded".Translate(w.outsiderName.Named("OTHER"), (w.byName ?? "?").Named("PAWN")).Resolve(), w.shift);
                NotableBook.Changed();
                // apart: word from home, told in a message.
                Notify.Message("CC_Out_Msg_WordLanded".Translate(w.outsiderName.Named("OTHER"), NotableBook.Called(n).Named("NAME"), (w.byName ?? "?").Named("PAWN")).Resolve(),
                    LookTargets.Invalid, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            }
        }

        /// <summary>
        /// A favour asked for their people's next visit: once some of their people are at the colony again (not the
        /// same visit), one of them walks over with it; nobody in thirty days and it's let go.
        /// </summary>
        public static void FavoursHourly(OutsiderBook book, int now)
        {
            var open = Favours.Open();
            if (open.Count == 0) return;
            foreach (var f in open)
            {
                if (f.faction == null || f.faction.defeated || f.def == null || now - f.asked > OutsiderMath.BringGiveUpDays * GenDate.TicksPerDay)
                {
                    Favours.Close(f, false);
                    // apart: a favour that never came, told in a message.
                    if (f.faction != null && f.def != null)
                        Notify.Message("CC_Out_Msg_BringLapsed".Translate((f.faction?.Name ?? "?").Named("FACTION"), f.def.label.Named("THING"), (f.outsiderName ?? "?").Named("OTHER")).Resolve(),
                            LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                    continue;
                }
                if (f.bearer != null && f.bearer.Spawned && f.bearer.CurJobDef == OutsiderDefOf.Council_OutsiderBring) continue;
                f.bearer = null;
                if (now - f.asked < OutsiderMath.BringSoonestDays * GenDate.TicksPerDay) continue;
                var bearer = Bearer(f);
                if (bearer == null) continue;
                var to = KinMemory.Ours(f.by) && f.by.Spawned && f.by.Map == bearer.Map && Together.Able(f.by) && bearer.CanReach(f.by, PathEndMode.Touch, Danger.Some) ? f.by : null;
                LocalTargetInfo target = to != null ? (LocalTargetInfo)to : DropCellFinder.TradeDropSpot(bearer.Map);
                var job = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderBring, target);
                job.thingDefToCarry = f.def;
                job.count = f.count;
                job.locomotionUrgency = LocomotionUrgency.Walk;
                bearer.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (bearer.CurJobDef == OutsiderDefOf.Council_OutsiderBring) f.bearer = bearer;
            }
        }

        /// <summary>Someone of their people at the colony now, free to walk over with it: the one who promised, else any grown one of them.</summary>
        private static Pawn Bearer(Favour f)
        {
            // Not the visit the favour was asked on: the one who promised goes home first.
            if (f.outsider != null && f.outsider.Spawned) return null;
            // At the colony of the one who asked (with more than one colony; any home once they are gone).
            var asked = ColonyState.Several && f.by != null && !f.by.Dead && People.StillOurs(f.by) ? Homes.Of(f.by) : null;
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome && (asked == null || m == asked)))
            {
                if (map.dangerWatcher.DangerRating != StoryDanger.None) continue;
                var people = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == f.faction && IsOutsider(p) && !p.DevelopmentalStage.Juvenile() && p.jobs != null
                                                                     && p.Spawned && Busy(p, null, OutsiderAct.Bring) == null && p.GetLord() != null && !Leaving(p)).ToList();
                if (people.Count == 0) continue;
                return people.Contains(f.outsider) ? f.outsider : people.OrderByDescending(p => p.TraderKind != null ? 1 : 0).First();
            }
            return null;
        }

        /// <summary>The bearer set it down by the colonist who asked (from their job): it's the colony's now, and their people did the colony a good turn.</summary>
        public static void Brought(Pawn bearer, Pawn to, ThingDef def, int count)
        {
            var book = Book;
            var f = book != null ? Favours.Open().FirstOrDefault(v => v.def == def && v.faction == bearer.Faction) : null;
            if (f == null || def == null) return;
            Favours.Close(f, true);
            try
            {
                var things = Couriers.Make(def, Mathf.Max(1, count));
                foreach (var t in things)
                    if (GenPlace.TryPlaceThing(t, bearer.Position, bearer.Map, ThingPlaceMode.Near, out var lying))
                        lying?.SetForbidden(false, false);
                Say(bearer, "brought", to);
                KinMemory.Note(f.faction, "broughtAsked", KinMemory.Ours(f.by) ? f.by : null, def.label);
                // together: they walked over to the colonist and set it down there (or by the gate, with nobody to hand it to).
                Notify.Message("CC_Out_Msg_Brought".Translate(bearer.Named("BEARER"), Couriers.Describe(things).Named("THING"), (f.byName ?? "?").Named("PAWN"),
                    (f.outsiderName ?? "?").Named("OTHER")).Resolve(), new LookTargets(bearer), MessageTypeDefOf.PositiveEvent, Importance.Notable);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider favour brought", e);
            }
        }

        // ---------------- A letter carried home ----------------

        /// <summary>The letter for <paramref name="x"/> to carry, written and waiting to be handed over, if any.</summary>
        public static NeighbourLetter LetterFor(Pawn x) =>
            NotableBook.Instance?.letters.FirstOrDefault(l => l.stage == LetterStage.AwaitMessenger && l.bearer == x && l.tripId < 0);

        /// <summary>
        /// The letter is written: the writer (or, when they can't, anyone free) walks over and hands it to the one who'll
        /// carry it home. False when the bearer is gone or no one can (then a messenger comes as for any letter).
        /// </summary>
        public static bool HandLetter(NeighbourLetter l, Pawn writer)
        {
            var x = l?.bearer;
            var map = l?.map;
            if (x == null || map == null || !IsOutsider(x) || x.Map != map || Leaving(x)) return false;
            if (map.mapPawns.FreeColonistsSpawned.Any(c => c.CurJobDef == OutsiderDefOf.Council_OutsiderTalk && c.CurJob.targetA.Thing == x && c.CurJob.count % 100 == (int)OutsiderAct.Letter))
                return true;
            // The writer, just done writing it (their next job), or free; else anyone free nearest the bearer.
            bool justWritten = writer != null && writer.CurJobDef == NotableDefOf.Council_WriteLetter;
            var by = writer != null && writer.Spawned && writer.Map == map && Together.Able(writer) && ColonistWhyNot(writer, x) == null
                     && (justWritten || PlayOuts.CanJoin(writer) && PlayOuts.Interruptible(writer)) ? writer : null;
            by ??= NeighbourLetters.Writers(map).Where(c => Together.Able(c) && PlayOuts.CanJoin(c) && PlayOuts.Interruptible(c) && ColonistWhyNot(c, x) == null)
                .OrderBy(c => c.Position.DistanceToSquared(x.Position)).FirstOrDefault();
            if (by == null) return true;
            var job = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderTalk, x);
            job.count = (int)OutsiderAct.Letter;
            job.locomotionUrgency = LocomotionUrgency.Walk;
            if (by == writer && justWritten) by.jobs.jobQueue.EnqueueFirst(job, JobTag.Misc);
            else by.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return true;
        }
    }
}
