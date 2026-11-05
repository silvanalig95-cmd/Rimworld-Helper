using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The keeper of secrets (docs/design/stage2-diplomacy.md, item 8; decision 13; release 2c.1), a seat of Social and
    /// Intellectual that opens at six voters (Defs/OfficeDefs/Rimfolk_Offices_Keeper.xml). The office's own duties, with
    /// or without a task: the colony's own secrets slip half as often while the seat is filled (Secrets/Secrets.cs reads
    /// <see cref="SlipFactor"/>); after dark, with guests of another people on the map, the keeper walks with them a while
    /// (the game's own follow), and may overhear something of a notable of theirs; the right-click "Sound out" with someone
    /// of theirs face to face, and with the notable a secret is about, "remind them what the colony knows" (pressing it).
    /// The keeper's task, learning a chosen notable's secret, is a talk with whoever of their people comes by, a paid
    /// informer met at the gate, or a visit to their town (TaskVisits): each step a roll of Social and Intellectual
    /// against the notable's (KeeperMath), a Secret tie on success, found out one time in ten (their regard of the colony
    /// and of the keeper falls, and they say so at their next meeting). Release 2c.3's schemes read <see cref="Watching"/>
    /// (their schemes caught twice as often: KeeperMath.CatchFactor).
    /// </summary>
    public static class KeeperOfSecrets
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public static bool On => CouncilTasks.On;

        /// <summary>The keeper of secrets at a home, or null.</summary>
        public static Pawn At(Map home) => CouncilTasks.HolderAt(home, TaskMath.KeeperOffice);

        /// <summary>Is this colonist the keeper at their own colony.</summary>
        public static bool Is(Pawn p) => p != null && OfficeTracker.HeldBy(p)?.defName == TaskMath.KeeperOffice;

        // ================= Guarding the colony's own =================

        /// <summary>How often a secret of this colonist's slips (Secrets.cs): half while their colony's keeper sits.</summary>
        public static float SlipFactor(Pawn owner)
        {
            if (owner == null || !(ColonyCouncilMod.Settings?.offices ?? false)) return 1f;
            var council = ColonyState.Of(owner)?.offices;
            return KeeperMath.SlipFactor(council != null && council.Filled(TaskMath.KeeperOffice));
        }

        // ================= The watch after dark =================

        /// <summary>The night each home was last watched (a session's: a reload may watch again the same night).</summary>
        private static readonly Dictionary<int, int> watchedNight = new Dictionary<int, int>();

        /// <summary>Guests walked with lately (thingID to tick), for their talk (keeper:watched); a session's.</summary>
        private static readonly Dictionary<int, int> walkedWith = new Dictionary<int, int>();

        /// <summary>A walk under way, its overhearing settled when it ends: home's id to (guest, tick).</summary>
        private static readonly Dictionary<int, (Pawn guest, int tick)> walking = new Dictionary<int, (Pawn, int)>();

        private static Game game;

        private static void Fresh()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            watchedNight.Clear();
            walkedWith.Clear();
            walking.Clear();
        }

        private static int Night(int now) => (now + 5 * TaskMath.HourTicks) / TaskMath.DayTicks;

        /// <summary>The keeper walks with guests at this home tonight (release 2c.3: their schemes caught twice as often).</summary>
        public static bool Watching(Map home)
        {
            Fresh();
            var k = At(home);
            if (k == null || home == null) return false;
            if (k.CurJobDef == JobDefOf.FollowClose && k.CurJob?.targetA.Thing is Pawn g && Outsiders.IsOutsider(g)) return true;
            return watchedNight.TryGetValue(home.uniqueID, out int night) && night == Night(Now) && KeeperMath.AfterDark(GenLocalDate.HourOfDay(home));
        }

        /// <summary>The one spoken to was walked with after dark lately (keeper:watched).</summary>
        public static bool WalkedWith(Pawn x)
        {
            Fresh();
            return x != null && walkedWith.TryGetValue(x.thingIDNumber, out int at) && Now - at < KeeperMath.WatchedDays * TaskMath.DayTicks;
        }

        /// <summary>The hour: at each home after dark, the keeper walks with any guests once a night; a walk ended may have overheard something; hard words owed are said when they meet.</summary>
        public static void Hourly(int now)
        {
            if (!On) return;
            Fresh();
            foreach (var home in Homes.All)
            {
                if (home == null) continue;
                try
                {
                    var keeper = At(home);
                    if (keeper == null) continue;
                    Walked(home, keeper, now);
                    Watch(home, keeper, now);
                    HardWords(home, keeper);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("keeper of secrets: the hour", e);
                }
            }
        }

        private static IEnumerable<Pawn> Guests(Map home) =>
            home.mapPawns.AllPawnsSpawned.Where(p => Outsiders.IsOutsider(p) && !p.IsQuestLodger() && p.Faction != null && !p.Faction.IsPlayer && p.GetLord() != null && !p.Downed
                                                     && !p.DevelopmentalStage.Juvenile());

        private static void Watch(Map home, Pawn keeper, int now)
        {
            if (!KeeperMath.AfterDark(GenLocalDate.HourOfDay(home))) return;
            int night = Night(now);
            if (watchedNight.TryGetValue(home.uniqueID, out int was) && was == night) return;
            var guests = Guests(home).ToList();
            if (guests.Count == 0 || !CouncilTasks.Free(keeper, home)) return;
            var guest = guests.RandomElement();
            var job = JobMaker.MakeJob(JobDefOf.FollowClose, guest);
            job.expiryInterval = KeeperMath.WatchTicks;
            job.checkOverrideOnExpire = true;
            job.followRadius = 6f;
            job.locomotionUrgency = LocomotionUrgency.Walk;
            // together: the keeper walks over to the guests and keeps by them; nothing passes between them yet.
            keeper.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (keeper.CurJobDef != JobDefOf.FollowClose) return;
            watchedNight[home.uniqueID] = night;
            walkedWith[guest.thingIDNumber] = now;
            walking[home.uniqueID] = (guest, now);
            Outsiders.Say(keeper, "keeperwatch", null);
            Tell.Minor(NewsKind.Colony, Importance.Flavor, home, TaskWords.T("CC_Keeper_Watch", keeper.Named("PAWN"), TaskWords.People(Outsiders.PeopleOf(guest)).Named("PEOPLE")),
                new LookTargets(keeper), MessageTypeDefOf.SilentInput);
        }

        /// <summary>A walk with the guests over: now and then something was overheard about a notable of their people.</summary>
        private static void Walked(Map home, Pawn keeper, int now)
        {
            if (!walking.TryGetValue(home.uniqueID, out var w)) return;
            bool still = keeper.CurJobDef == JobDefOf.FollowClose && keeper.CurJob?.targetA.Thing == w.guest;
            if (still && now - w.tick < KeeperMath.WatchTicks) return;
            walking.Remove(home.uniqueID);
            if (w.guest == null || !Rand.Chance(KeeperMath.OverhearChance)) return;
            var f = Outsiders.PeopleOf(w.guest);
            var n = Outsiders.NotableOf(w.guest) ?? (f != null ? FavourCalls.HeadOf(f, home) : null);
            if (n == null || !n.Alive || SecretTies.About(n).Any(t => t.Live)) return;
            var tie = SecretTies.Learn(Diplomacy.Colony(home), n, KeeperMath.Pick(Rand.Value), keeper);
            if (tie == null) return;
            Told(home, keeper, n, tie, TaskWords.P("CC_Keeper_How_Overheard", keeper.Named("PAWN"), w.guest.Named("OTHER")));
        }

        /// <summary>A notable who caught the keeper at it says so when the two next meet (once).</summary>
        private static void HardWords(Map home, Pawn keeper)
        {
            var book = NotableBook.Instance;
            if (book == null) return;
            foreach (var n in book.notables)
            {
                var x = n?.pawn;
                if (x == null || !x.Spawned || x.Map != home || !n.done.Contains(KeeperMath.CaughtKey) || n.done.Contains(KeeperMath.CaughtSaidKey)) continue;
                var who = x;
                var note = n;
                Together.When("keepercaught|" + x.thingIDNumber + "|" + keeper.thingIDNumber, x, keeper, () =>
                {
                    // together: said once the two are together.
                    if (note.done.Contains(KeeperMath.CaughtSaidKey)) return;
                    note.done.Add(KeeperMath.CaughtSaidKey);
                    Outsiders.Say(who, "keepercaught", keeper);
                }, 12f);
            }
        }

        // ================= The task: learning their secret =================

        /// <summary>The keeper's task, an hour: a talk with whoever of their people comes by (once a day), and an informer's word once they have been paid.</summary>
        public static void TaskHour(DiplomacyHome h, CouncilTask t, Map home, int now)
        {
            t.nextTick = now + TaskMath.HourTicks;
            if (t.tripId >= 0)
            {
                var trip = CourierTracker.Instance?.Get(t.tripId);
                if (trip == null || trip.state == CourierState.Done)
                {
                    bool paid = trip != null && trip.taken > 0;
                    t.tripId = -1;
                    if (paid) Step(h, t, home, KeeperMath.InformerBonus, TaskStep.Informer, null);
                    return;
                }
                // At the gate: the keeper walks out for a word, when free.
                if (trip.state == CourierState.Working && trip.people.Count > 0 && CouncilTasks.Free(t.officer, home))
                {
                    var man = trip.people.FirstOrDefault(p => p != null && p.Spawned && p.Map == home);
                    if (man != null && t.officer.CurJobDef != JobDefOf.Goto) Meet(t.officer, man);
                }
            }
            int day = now / TaskMath.DayTicks;
            if (t.talkDay == day || t.letterId >= 0) return;
            var f = CouncilTasks.PeopleOf(t);
            var x = home.mapPawns.AllPawnsSpawned.Where(p => Outsiders.IsOutsider(p) && Outsiders.PeopleOf(p) == f && !p.Downed && !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (x == null || !CouncilTasks.Free(t.officer, home) || OutsiderTalk.Left(x) <= 0) return;
            t.talkDay = day;
            DiplomacyActs.Chose(t.officer, SoundCode);
            Outsiders.StartTalk(t.officer, x, OutsiderAct.SoundOut);
        }

        /// <summary>The keeper walks out to the informer at the gate (a word, nothing more: the roll comes once they are paid).</summary>
        private static void Meet(Pawn keeper, Pawn man)
        {
            if (!man.Position.IsValid) return;
            var cell = CellFinder.RandomClosewalkCellNear(man.Position, man.Map, 2);
            var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            // together: the keeper walks over to the informer at the gate.
            keeper.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>
        /// One step's roll on the task (a talk, an informer's word, a visit): a secret learned (the task done, the Secret tie
        /// made), nothing this time, or found out (the task failed). Returns its words (for a visit's letter); tells the rest.
        /// </summary>
        public static string Step(DiplomacyHome h, CouncilTask t, Map home, float bonus, TaskStep step, Pawn with)
        {
            var keeper = t.officer;
            var n = CouncilTasks.NotableOf(t);
            if (keeper == null || n == null || !n.Alive || !t.Live) return null;
            if (step == TaskStep.Talk) t.talks++;
            else if (step == TaskStep.Informer) t.carts++;
            t.last = step;
            t.lastTick = Now;
            var roll = Roll(keeper, n, bonus);
            t.lastHow = (int)roll;
            Diplomacy.Dirty();
            switch (roll)
            {
                case SecretRoll.Learned:
                {
                    var tie = SecretTies.Learn(Diplomacy.Colony(home), n, KeeperMath.Pick(Rand.Value), keeper);
                    if (tie == null) return null;
                    string how = TaskWords.P("CC_Keeper_How_" + step, keeper.Named("PAWN"), (with?.LabelShort ?? n.Name).Named("OTHER"), (t.townName ?? "?").Named("TOWN"));
                    string line = Told(home, keeper, n, tie, how, step != TaskStep.Visit);
                    CouncilTasks.End(h, t, TaskStage.Done, line, false);
                    return line;
                }
                case SecretRoll.FoundOut:
                {
                    string line = Caught(home, keeper, n, with, step);
                    CouncilTasks.End(h, t, TaskStage.Failed, line, step != TaskStep.Visit);
                    return line;
                }
                default:
                {
                    string line = TaskWords.P("CC_Keeper_Nothing_" + step, keeper.Named("PAWN"), (with?.LabelShort ?? n.Name).Named("OTHER"), n.Name.Named("NAME"));
                    if (step != TaskStep.Visit)
                        Tell.Minor(NewsKind.World, Importance.Flavor, home, line, keeper.Spawned ? new LookTargets(keeper) : LookTargets.Invalid, MessageTypeDefOf.SilentInput);
                    return line;
                }
            }
        }

        /// <summary>The keeper's Social and Intellectual against the notable's (their pawn's skills, else an ordinary five each).</summary>
        private static SecretRoll Roll(Pawn keeper, Notable n, float bonus)
        {
            int Skill(Pawn p, SkillDef s, int none) => p?.skills?.GetSkill(s) is SkillRecord r && !r.TotallyDisabled ? r.Level : none;
            float chance = KeeperMath.LearnChance(Skill(keeper, SkillDefOf.Social, 0), Skill(keeper, SkillDefOf.Intellectual, 0), Skill(n.pawn, SkillDefOf.Social, 5),
                Skill(n.pawn, SkillDefOf.Intellectual, 5), bonus);
            return KeeperMath.Roll(chance, Rand.Value);
        }

        /// <summary>A secret learned, told to the player (a letter: what it is, how it came, what it can be used for). Returns its words.</summary>
        private static string Told(Map home, Pawn keeper, Notable n, Tie tie, string how, bool letter = true)
        {
            string what = SecretTies.What(tie);
            string text = how + " " + TaskWords.P("CC_Keeper_Learned", n.Name.Named("NAME"), what.Named("SECRET")) + "\n\n" + TaskWords.P("CC_Keeper_Learned_Use", keeper.Named("PAWN"), n.Name.Named("NAME"));
            if (letter)
                Tell.Letter(NewsKind.World, Importance.Notable, home, TaskWords.P("CC_Keeper_Learned_Label", n.Name.Named("NAME")), text, LetterDefOf.NeutralEvent,
                    keeper.Spawned ? new LookTargets(keeper) : LookTargets.Invalid, n.faction);
            return text;
        }

        /// <summary>Found out: the notable thinks less of the colony and of the keeper, remembers it, and says so at the next meeting; the one who caught it says so now.</summary>
        private static string Caught(Map home, Pawn keeper, Notable n, Pawn with, TaskStep step)
        {
            n.personal += KeeperMath.CaughtRegard;
            n.RegardOf(keeper, true).personal += KeeperMath.CaughtKeeperRegard;
            n.Remember(TaskWords.P("CC_Keeper_Note_Caught", keeper.Named("PAWN")), KeeperMath.CaughtRegard);
            if (!n.done.Contains(KeeperMath.CaughtKey)) n.done.Add(KeeperMath.CaughtKey);
            n.done.Remove(KeeperMath.CaughtSaidKey);
            while (n.done.Count > 12) n.done.RemoveAt(0);
            NotableBook.Changed();
            // together: the talk that brought them together has just ended face to face.
            if (with != null && with.Spawned && keeper.Spawned) Outsiders.Say(with, "keepercaught", keeper);
            return TaskWords.T("CC_Keeper_Caught_" + step, keeper.Named("PAWN"), n.Name.Named("NAME"), (with?.LabelShort ?? n.Name).Named("OTHER"));
        }

        // ================= The informer =================

        /// <summary>Why the keeper can't pay an informer for word of their target now, or null.</summary>
        public static string InformerWhyNot(CouncilTask t, Map home)
        {
            if (t == null || !t.Live || t.kind != TaskKind.Secrets) return TaskWords.P("CC_Task_Why_Gone");
            if (t.tripId >= 0) return TaskWords.P("CC_Keeper_Why_Informer");
            if (!Couriers.CanSend(home, null)) return TaskWords.P("CC_Keeper_Why_NoInformer");
            int silver = KeeperMath.InformerSilver((int)Ages.Of(home));
            if (PlayCarry.Have(home, "silver", ThingDefOf.Silver) < silver) return TaskWords.P("CC_Keeper_Why_Silver", silver.Named("SILVER"));
            return null;
        }

        /// <summary>An informer paid: their man comes to the gate for the silver, the keeper has a word with them, and their word is the step's roll.</summary>
        public static void Informer(CouncilTask t, Map home)
        {
            if (InformerWhyNot(t, home) != null) return;
            int silver = KeeperMath.InformerSilver((int)Ages.Of(home));
            var trip = Couriers.CollectOrder(home, ThingDefOf.Silver, silver, 0, null);
            if (trip == null) return;
            t.tripId = trip.id;
            // apart: the council's choice, told in a message; the informer comes to the gate some hours later.
            Tell.Message(NewsKind.World, Importance.Notable, home, TaskWords.P("CC_Keeper_Msg_Informer", t.officer.Named("PAWN"), silver.Named("SILVER"), (t.targetName ?? "?").Named("NAME"),
                TaskWords.Form(home, null).word.Named("WORD")), new LookTargets(t.officer), MessageTypeDefOf.NeutralEvent);
            Diplomacy.Dirty();
        }

        // ================= Sounding out, face to face =================

        public const string SoundCode = "sound";
        public const string PressCode = "press:";

        /// <summary>"Sound out" is the keeper's: shown only to the keeper of secrets, with someone grown of another people at the colony.</summary>
        public static bool Shown(Pawn c, Pawn x) => On && Is(c) && Outsiders.PeopleOf(x) != null && !c.DevelopmentalStage.Juvenile() && !x.DevelopmentalStage.Juvenile() && !x.IsQuestLodger();

        /// <summary>The notable a sounding out learns about: the one spoken to if a notable, else the head of their people.</summary>
        public static Notable Target(Pawn x, Map home)
        {
            var n = Outsiders.NotableOf(x);
            if (n != null && n.Alive) return n;
            var f = Outsiders.PeopleOf(x);
            return f != null ? FavourCalls.HeadOf(f, home) : null;
        }

        public static string WhyNot(Pawn c, Pawn x)
        {
            var home = c.MapHeld;
            var n = Target(x, home);
            if (n == null) return TaskWords.P("CC_Keeper_Why_NoNotable", TaskWords.People(Outsiders.PeopleOf(x)).Named("PEOPLE"));
            bool known = SecretTies.About(n).Any(t => t.Live);
            bool press = Outsiders.NotableOf(x) == n && SecretTies.About(n).Any(t => t.state == Tie.State.Open);
            if (known && !press) return TaskWords.P("CC_Task_Why_Known", n.Name.Named("NAME"));
            return null;
        }

        public static string Label() => TaskWords.P("CC_Out_Act_SoundOut");

        /// <summary>What to do: sound them out about a notable of theirs, or (with the notable a secret is about) remind them of it and ask its price.</summary>
        public static void Choose(Pawn c, Pawn x, Action<Pawn, Pawn, string> pick)
        {
            var home = c.MapHeld;
            var n = Target(x, home);
            var opts = new List<FloatMenuOption>();
            if (n != null)
            {
                string label = TaskWords.P("CC_Keeper_Menu_Sound", n.Name.Named("NAME"));
                if (SecretTies.About(n).Any(t => t.Live)) opts.Add(new FloatMenuOption("CC_Out_Disabled".Translate(label.Named("LABEL"), TaskWords.P("CC_Task_Why_Known", n.Name.Named("NAME")).Named("WHY")).Resolve(), null));
                else opts.Add(new FloatMenuOption(label, () => pick(c, x, SoundCode)) { tooltip = TaskWords.P("CC_Keeper_Menu_Sound_Tip") });
            }
            if (Outsiders.NotableOf(x) is Notable self)
                foreach (var t in SecretTies.About(self).Where(t => t.state == Tie.State.Open))
                {
                    var tie = t;
                    string label = TaskWords.P("CC_Keeper_Menu_Press", self.Name.Named("NAME"), SecretTies.What(tie).Named("SECRET"), KeeperMath.Hush(SecretTies.Weight(tie)).Named("SILVER"));
                    string why = SecretTies.PressWhyNot(tie);
                    opts.Add(why != null ? new FloatMenuOption("CC_Out_Disabled".Translate(label.Named("LABEL"), why.Named("WHY")).Resolve(), null)
                        : new FloatMenuOption(label, () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(SecretTies.PressAsk(tie, x), () => pick(c, x, PressCode + tie.id)))));
                }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("CC_Out_Nothing".Translate().Resolve(), null));
            Find.WindowStack.Add(new FloatMenu(opts, TaskWords.P("CC_Keeper_Menu_Title", x.Named("OTHER"))));
        }

        /// <summary>
        /// The talk is over, face to face: sounding out is the roll (on the keeper's task when it is aimed at them, else on its
        /// own, about the notable they lead to); a secret pressed is their answer at once.
        /// </summary>
        public static Job Talked(Pawn c, Pawn x, string code)
        {
            var home = c.MapHeld;
            if (code != null && code.StartsWith(PressCode))
            {
                if (int.TryParse(code.Substring(PressCode.Length), out int id)) SecretTies.PressFace(WorldLedger.Get(id), c, x, home);
                return null;
            }
            var n = Target(x, home);
            if (n == null) return null;
            var task = CouncilTasks.Of(c, out var h);
            Outsiders.Remember(c, x, OutsiderAct.SoundOut, Outsiders.Note(OutsiderAct.SoundOut, c), 0.2f, 0.3f);
            if (task != null && task.kind == TaskKind.Secrets && task.Live && task.notable == n.id)
            {
                Step(h, task, home, 0f, TaskStep.Talk, x);
                return null;
            }
            if (SecretTies.About(n).Any(t => t.Live)) return null;
            // Sounding out without a task: the same roll about the notable they lead to.
            var roll = Roll(c, n, 0f);
            if (roll == SecretRoll.Learned)
            {
                var tie = SecretTies.Learn(Diplomacy.Colony(home), n, KeeperMath.Pick(Rand.Value), c);
                if (tie != null) Told(home, c, n, tie, TaskWords.P("CC_Keeper_How_Talk", c.Named("PAWN"), x.Named("OTHER"), "".Named("TOWN")));
            }
            else if (roll == SecretRoll.FoundOut)
            {
                string line = Caught(home, c, n, x, TaskStep.Talk);
                // together: told as the two stand face to face; the message carries what follows.
                Tell.Message(NewsKind.World, Importance.Important, home, line, new LookTargets(x), MessageTypeDefOf.NegativeEvent);
            }
            else
                Tell.Minor(NewsKind.World, Importance.Flavor, home, TaskWords.P("CC_Keeper_Nothing_Talk", c.Named("PAWN"), x.Named("OTHER"), n.Name.Named("NAME")), new LookTargets(c),
                    MessageTypeDefOf.SilentInput);
            return null;
        }
    }
}
