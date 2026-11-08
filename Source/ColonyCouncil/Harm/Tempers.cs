using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 7: tempers in the colony (Harm/HarmSeven.cs is the hub). Each fight is the game's own social fight
    /// through the harm service (Harm.Fight: the cap for both, never a child), told by the game's own message; the rest is
    /// seen on the map, in the line said and a few words over them.
    /// <list type="bullet">
    /// <item>The peacemaker's sit-down goes wrong (1.1.2): the worst-feuding pair sat down together come to blows one
    /// time in ten at a rivalry of 70, nothing once it has cooled to 30, less with the peacemaker's Social, half without a
    /// hot temper between them; the peacemaker steps between them and takes a punch one time in three. With the offices
    /// and the social web.</item>
    /// <item>Words with the speaker (1.3.1): under demanding stakes, a fifth of the promises broken to a bitter,
    /// hot-tempered colonist bring them to the speaker within the day; a speaker with good Social talks them down, else
    /// it comes to blows. With the offices and wishes, under demanding stakes only.</item>
    /// <item>The thing that went to someone else (1.3.2): one who feels passed over by a wish granted to another, if
    /// jealous or abrasive, has a sharp word for them the first two times they meet in the week after, each at the game's
    /// own fight chance for a pair at -20; the peacemaker's word settles it. With wishes and jealousy.</item>
    /// <item>A meeting that boils over (1.5.1): two rivals who both pitched at the table, one idea winning: one meeting in
    /// twelve the loser shoves the winner, half with the peacemaker there, less with a chair of good Social. With the
    /// projects.</item>
    /// <item>Friends stand by (3.5.2): one social fight in six, a close friend near runs over to pull them apart, and one
    /// time in two takes a blow for it; the two stop. Not preventable. With the social web.</item>
    /// <item>Said to their face (3.7.1): gossip run down to its subject's face, a hot-tempered subject comes to blows with
    /// the teller one telling in ten. With the names.</item>
    /// <item>Found out by the wronged one (3.9.1): a secret that wronged the colony (its stores, its work, its people),
    /// let slip to one of the colony, brings that one to its owner one time in four (half with the peacemaker in office),
    /// to blows; never one its owner confessed. With the secrets.</item>
    /// <item>Told by the wrong friend (3.9.2): a confidence let slip by the friend it was told to brings the keeper to
    /// them one leak in five: hard words, and blows half the time. With the secrets.</item>
    /// <item>A rival in love comes to blows (3.10.1): in "a rival in love", one story in four the stiff words between the
    /// rivals become a shove, and the one they both like thinks less of the one who started it. With personal
    /// stories.</item>
    /// <item>Words at the altar (3.13.1): once after the council names one faith the colony's, at an evening of it, one
    /// time in three a devout of another faith comes to interrupt and comes to blows with the evening's speaker. Named on
    /// the decree's card. With faith in the colony and answers on the map.</item>
    /// <item>The feast that ends in a fistfight (4.1.3): rivals quarrelling at a day's table come to blows one quarrel in
    /// four when drink was poured. With the calendar.</item>
    /// <item>The toast's second round (4.7.3): with Vanilla Brewing Expanded and strong drink in plenty, one toast in three
    /// pours a second round for those who drank the first and may have another; two rivals who both took it come to blows
    /// one time in four within the hour. With answers on the map and the mod.</item>
    /// <item>The soldier's nightmare (3.12.2): one in twenty of the nightmares that leave someone with a soldier's or an
    /// outlaw's past shaken, in a shared bedroom, they strike out at the roommate who wakes them (the mildest bruise on an
    /// arm). A bedroom of their own keeps it from anyone. With the dreams.</item>
    /// </list>
    /// </summary>
    public static class Tempers
    {
        private static int Now => Find.TickManager.TicksGame;

        public static bool DebugSitDown, DebugSpeaker, DebugPassed, DebugMeeting, DebugFriend, DebugFace, DebugWronged, DebugLeak, DebugLove, DebugAltar, DebugFeast,
            DebugRound, DebugSoldier;

        /// <summary>Occasions already looked at (a fight, a toast, a nightmare, an evening): session memory, one look each.</summary>
        private static readonly HashSet<string> looked = new HashSet<string>();

        /// <summary>The social fights seen going on (the pair's key), and when first seen.</summary>
        private static readonly Dictionary<long, int> fights = new Dictionary<long, int>();

        public static void Reset()
        {
            looked.Clear();
            fights.Clear();
        }

        private static bool Look(string key)
        {
            if (looked.Count > 600) looked.Clear();
            return looked.Add(key);
        }

        private static bool CanFight(Pawn p) => p != null && Together.Able(p) && !p.DevelopmentalStage.Juvenile() && p.interactions != null
                                               && !p.WorkTagIsDisabled(WorkTags.Violent) && Harm.May(p, HurtClass.Fight);

        private static int Social(Pawn p) => p?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;

        private static string AgeTag => Ages.Ours.ToString().ToLowerInvariant();

        /// <summary>Two people at odds: rivals in the social web, or an opinion of -20 or worse either way.</summary>
        public static bool Rivals(Pawn a, Pawn b)
        {
            if (a == null || b == null || a == b) return false;
            var tie = CouncilGameComponent.Instance?.social?.Get(a, b, TieKind.Rivals);
            if (tie != null && tie.announced) return true;
            return (a.relations?.OpinionOf(b) ?? 0) <= HarmSevenMath.PassedOverOpinion || (b.relations?.OpinionOf(a) ?? 0) <= HarmSevenMath.PassedOverOpinion;
        }

        private static Pawn Peacemaker(Map map) => map == null ? null : ColonyState.For(map)?.offices?.Peacemaker;

        /// <summary>The hour (the harm book's first slice): the one faith's evenings.</summary>
        public static void Hourly(int now)
        {
            if (!Harm.On) return;
            Altars(now);
        }

        /// <summary>Each colonist's hour: a soldier's nightmare.</summary>
        public static void Hour(Pawn p, int now)
        {
            if (!Harm.On) return;
            Soldier(p);
        }

        // ================= The peacemaker's sit-down goes wrong (1.1.2) =================

        /// <summary>
        /// The peacemaker has the worst-feuding pair sat down together (Offices.Mediate, the three together): maybe the two
        /// hot tempers come to blows instead. True when it went wrong (the rivalry is not mended this time).
        /// </summary>
        public static bool SitDown(Pawn peacemaker, Pawn a, Pawn b, float strength)
        {
            if (!Harm.On || !Harm.SystemOn(HarmKinds.SitDown.system) || !CanFight(a) || !CanFight(b)) return false;
            bool hot = HarmSevenMath.Hot(Outsiders.Temper(a)) || HarmSevenMath.Hot(Outsiders.Temper(b));
            bool debug = DebugSitDown;
            DebugSitDown = false;
            if (!debug && !Harm.Roll(HarmKinds.SitDown, HarmSevenMath.SitDown(strength, Social(peacemaker), hot))) return false;
            var starter = Outsiders.Temper(a) >= Outsiders.Temper(b) ? a : b;
            var other = starter == a ? b : a;
            // together: Mediate is called once the peacemaker and both of the pair are together.
            HarmFour.Say(starter, "Rimfolk_Pool_SitDownBlows", other);
            if (!Harm.Fight(starter, other, HarmKinds.SitDown)) return false;
            HarmFour.Mote(starter, "CC_HarmSeven_Mote_SitDown");
            if (peacemaker == null || !Together.Able(peacemaker)) return true;
            // together: the peacemaker is at the table with them, and steps between.
            HarmFour.Say(peacemaker, "Rimfolk_Pool_PeacemakerBetween", starter);
            if (Rand.Chance(HarmSevenMath.PeacemakerPunch)
                && Harm.Injure(peacemaker, HurtKind.Bruise, HurtWhere.Arm, HarmSevenMath.PunchLow, HarmSevenMath.PunchHigh, HarmKinds.SitDown, HurtClass.Injury, starter) != null)
                HarmFour.Mote(peacemaker, "CC_HarmSeven_Mote_Punch");
            return true;
        }

        // ================= Words with the speaker (1.3.1) =================

        /// <summary>A promise broken (CouncilDynamics.Expire): a bitter, hot-tempered one under demanding stakes may go to the speaker with it.</summary>
        public static void PromiseBroken(Pawn p)
        {
            var s = ColonyCouncilMod.Settings;
            if (!Harm.On || p?.Map == null || s == null || !Harm.SystemOn(HarmKinds.SpeakerWords.system) || p.DevelopmentalStage.Juvenile()) return;
            var speaker = OfficeTracker.For(p.Map)?.Speaker;
            if (speaker == null || speaker == p || speaker.Dead) return;
            var level = CouncilGameComponent.Instance?.standings.LevelOf(p) ?? TrustLevel.Neutral;
            bool bitter = level <= TrustLevel.Doubtful;
            bool debug = DebugSpeaker;
            DebugSpeaker = false;
            if (!debug && !Harm.Roll(HarmKinds.SpeakerWords, HarmSevenMath.SpeakerWords(s.Harsh, bitter, HarmSevenMath.Hot(Outsiders.Temper(p))))) return;
            if (HarmSeven.WaitingOf(HarmSeven.Speaker, w => w.pawn == p) != null) return;
            HarmFour.Wait(new HarmWaiting { what = HarmSeven.Speaker, kind = HarmKinds.SpeakerWords.key, pawn = p, other = speaker, made = Now });
        }

        /// <summary>The bitter one goes to find the speaker in their free time. True when done with.</summary>
        public static bool SpeakerHour(HarmWaiting x, int now)
        {
            var p = x.pawn;
            var sp = x.other;
            if (p == null || sp == null || p.Dead || sp.Dead || x.flag) return true;
            Together.When("speaker|" + p.thingIDNumber, p, sp, () =>
            {
                if (x.flag || p.Dead || sp.Dead) return;
                x.flag = true;
                // together: the bitter one came to the speaker (Together.When), face to face.
                HarmFour.Say(p, "Rimfolk_Pool_SpeakerWords", sp, AgeTag);
                if (Rand.Chance(HarmSevenMath.TalksDown(Social(sp))) || !CanFight(p) || !CanFight(sp) || !Harm.Fight(p, sp, HarmKinds.SpeakerWords))
                    HarmFour.Say(sp, "Rimfolk_Pool_SpeakerCalms", p);
            }, HarmSevenMath.SpeakerHours, 0f);
            return x.flag;
        }

        // ================= The thing that went to someone else (1.3.2) =================

        /// <summary>One felt passed over by a wish granted to another (CouncilDynamics.GrantRipples): the jealous or abrasive hold it for a week.</summary>
        public static void PassedOver(Pawn p, Pawn holder)
        {
            if (!Harm.On || p?.Map == null || holder == null || p == holder || !Harm.SystemOn(HarmKinds.PassedOver.system) || p.DevelopmentalStage.Juvenile()) return;
            var traits = p.story?.traits;
            bool jealous = traits?.HasTrait(TraitDefOf.Jealous) ?? false, abrasive = traits?.HasTrait(TraitDefOf.Abrasive) ?? false;
            if (!HarmSevenMath.PassedOverNature(jealous, abrasive) && !DebugPassed) return;
            DebugPassed = false;
            if (HarmSeven.WaitingOf(HarmSeven.Passed, w => w.pawn == p && w.other == holder) != null) return;
            var x = new HarmWaiting { what = HarmSeven.Passed, kind = HarmKinds.PassedOver.key, pawn = p, other = holder, made = Now, due = Now };
            HarmFour.Wait(x);
            // The peacemaker's word settles it, when the two meet.
            var pm = Peacemaker(p.Map);
            if (pm != null && pm != p && pm != holder)
                Together.When("passedword|" + p.thingIDNumber, pm, p, () =>
                {
                    if (x.count >= HarmSevenMath.PassedOverWords) return;
                    x.count = HarmSevenMath.PassedOverWords;
                    // together: the peacemaker and the passed-over one are together (Together.When).
                    HarmFour.Say(pm, "Rimfolk_Pool_PeacemakerWord", p);
                }, 48f, 6f);
        }

        /// <summary>The week after: the first two times they meet, a day apart at least, a sharp word. True when done with.</summary>
        public static bool PassedHour(HarmWaiting x, int now)
        {
            var p = x.pawn;
            var h = x.other;
            if (p == null || h == null || p.Dead || h.Dead || x.count >= HarmSevenMath.PassedOverWords) return true;
            if (now < x.due || !Together.Can(p, h)) return false;
            x.count++;
            x.due = now + HarmMath.DayTicks;
            // together: Together.Can, the two in talking range.
            HarmFour.Say(p, "Rimfolk_Pool_PassedOver", h);
            if (CanFight(p) && CanFight(h) && Harm.Roll(HarmKinds.PassedOver, HarmSevenMath.PassedOverChance)) Harm.Fight(p, h, HarmKinds.PassedOver);
            return x.count >= HarmSevenMath.PassedOverWords;
        }

        // ================= A meeting that boils over (1.5.1) =================

        /// <summary>The council meeting decided (Ambitions.Conclude, after the vote): two rivals who both pitched and are there, one idea winning.</summary>
        public static void MeetingDecided(CouncilMeeting m)
        {
            if (!Harm.On || m?.map == null || !Harm.SystemOn(HarmKinds.MeetingBoils.system) || m.Tied) return;
            var won = m.Favorite?.proposer;
            if (won == null || !m.Present(won) || !CanFight(won)) return;
            var lost = m.pitches.Select(p => p.proposer).Where(p => p != null && p != won && m.Present(p) && CanFight(p) && Rivals(p, won) && Together.Can(p, won))
                .OrderByDescending(Outsiders.Temper).FirstOrDefault();
            if (lost == null) return;
            var pm = Peacemaker(m.map);
            bool pmThere = pm != null && m.Present(pm);
            bool debug = DebugMeeting;
            DebugMeeting = false;
            if (!debug && !Harm.Roll(HarmKinds.MeetingBoils, HarmSevenMath.Meeting(true, pmThere, Social(m.chair)))) return;
            // together: Together.Can, both at the table.
            HarmFour.Say(lost, "Rimfolk_Pool_MeetingBoils", won);
            if (!Harm.Fight(lost, won, HarmKinds.MeetingBoils)) return;
            HarmFour.Mote(lost, "CC_HarmSeven_Mote_Meeting");
        }

        // ================= Friends stand by (3.5.2) =================

        private static long PairKey(Pawn a, Pawn b)
        {
            int x = Math.Min(a.thingIDNumber, b.thingIDNumber), y = Math.Max(a.thingIDNumber, b.thingIDNumber);
            return (long)x * 1000003L + y;
        }

        /// <summary>Every 250 ticks: a social fight newly begun at a home, one look each for a close friend near.</summary>
        public static void Fights(int now)
        {
            if (!Harm.On || !Harm.SystemOn(HarmKinds.FriendsStand.system)) return;
            if (fights.Count > 0)
                foreach (var k in fights.Where(kv => now - kv.Value > HarmMath.HourTicks * 3).Select(kv => kv.Key).ToList()) fights.Remove(k);
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                if (!map.IsPlayerHome) continue;
                foreach (var a in map.mapPawns.FreeColonistsSpawned)
                {
                    if (a.MentalStateDef != MentalStateDefOf.SocialFighting || !(a.MentalState is MentalState_SocialFighting sf) || sf.otherPawn == null) continue;
                    var b = sf.otherPawn;
                    long key = PairKey(a, b);
                    if (fights.ContainsKey(key)) continue;
                    fights[key] = now;
                    StandBy(a, b);
                }
            }
        }

        private static bool Close(Pawn f, Pawn p)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (social != null && social.Strength(f, p, TieKind.Friends) >= HarmSevenMath.CloseFriend) return true;
            return (f.relations?.OpinionOf(p) ?? 0) >= HarmSevenMath.CloseOpinion;
        }

        private static void StandBy(Pawn a, Pawn b)
        {
            var map = a.Map;
            var friend = map.mapPawns.FreeColonistsSpawned.Where(f => f != a && f != b && Together.Able(f) && !f.DevelopmentalStage.Baby() && f.Position.InHorDistOf(a.Position, HarmSevenMath.FriendRange)
                                                                     && (Close(f, a) || Close(f, b)) && f.CanReach(a, PathEndMode.Touch, Danger.Some))
                .OrderBy(f => f.Position.DistanceToSquared(a.Position)).FirstOrDefault();
            if (friend == null) return;
            bool debug = DebugFriend;
            DebugFriend = false;
            var cls = HarmFour.Child(friend) ? HurtClass.Mild : HurtClass.Injury;
            if (!debug && !Harm.Roll(friend, HarmKinds.FriendsStand, HarmSevenMath.FriendSteps, cls)) return;
            var mine = Close(friend, a) ? a : b;
            var theirs = mine == a ? b : a;
            // apart: the shout as they run over to the two fighting.
            HarmFour.Say(friend, "Rimfolk_Pool_FriendStepsIn");
            var go = JobMaker.MakeJob(JobDefOf.Goto, mine.Position);
            go.locomotionUrgency = LocomotionUrgency.Sprint;
            go.expiryInterval = 600;
            friend.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
            HarmSeven.Later(300, () => PullApart(friend, mine, theirs));
        }

        /// <summary>The friend is there: a blow one time in two, and the two pulled apart.</summary>
        private static void PullApart(Pawn friend, Pawn mine, Pawn theirs)
        {
            if (friend == null || !friend.Spawned || mine?.Map != friend.Map || friend.Downed) return;
            if (!friend.Position.InHorDistOf(mine.Position, 4f)) return;
            var cls = HarmFour.Child(friend) ? HurtClass.Mild : HurtClass.Injury;
            if (Rand.Chance(HarmSevenMath.FriendBlow)
                && Harm.Injure(friend, HurtKind.Bruise, Rand.Bool ? HurtWhere.Arm : HurtWhere.Torso, HarmSevenMath.FriendLow, HarmSevenMath.FriendHigh, HarmKinds.FriendsStand, cls, theirs) != null)
            {
                HarmFour.Say(friend, "Rimfolk_Pool_FriendTookBlow");
                HarmFour.Mote(friend, "CC_HarmSeven_Mote_FriendBlow");
            }
            foreach (var p in new[] { mine, theirs })
                if (p?.MentalStateDef == MentalStateDefOf.SocialFighting) p.MentalState.RecoverFromState();
        }

        // ================= Said to their face (3.7.1) =================

        /// <summary>Gossip told with its subject right there (Gossip.InFront, the two together): run down, a hot-tempered subject may come to blows with the teller.</summary>
        public static void SaidToFace(Pawn teller, Pawn subject, bool against)
        {
            if (!Harm.On || teller == null || subject == null || !Harm.SystemOn(HarmKinds.SaidToFace.system)) return;
            bool debug = DebugFace;
            DebugFace = false;
            if (!debug && !Harm.Roll(HarmKinds.SaidToFace, HarmSevenMath.Face(against, Outsiders.Temper(subject)))) return;
            // The words are said first (the gossip's own turns), the blows after.
            HarmSeven.Later(HarmSevenMath.FaceDelayTicks, () =>
            {
                if (!Together.Can(subject, teller) || !CanFight(subject) || !CanFight(teller)) return;
                // together: Together.Can, still face to face.
                HarmFour.Say(subject, "Rimfolk_Pool_SaidToFace", teller);
                Harm.Fight(subject, teller, HarmKinds.SaidToFace);
            });
        }

        // ================= Found out by the wronged one (3.9.1), told by the wrong friend (3.9.2) =================

        /// <summary>A secret let slip to someone of the colony (SecretTracker.SayInCompany, after the reveal): one that wronged the colony brings the hearer to its owner.</summary>
        public static void SecretHeard(Secret s, Pawn speaker, Pawn heard, bool confessed)
        {
            if (!Harm.On || s?.def == null || s.owner == null || heard == null || heard == s.owner || !Harm.SystemOn(HarmKinds.WrongedOne.system)) return;
            if (!HarmFour.Ours(heard) || heard.DevelopmentalStage.Juvenile() || s.owner.DevelopmentalStage.Juvenile()) return;
            bool debug = DebugWronged;
            DebugWronged = false;
            if (!debug && !Harm.Roll(HarmKinds.WrongedOne, HarmSevenMath.Wronged(s.def.wrongsColony, confessed, Peacemaker(heard.Map) != null))) return;
            HarmFour.Wait(new HarmWaiting { what = HarmSeven.Wronged, kind = HarmKinds.WrongedOne.key, pawn = heard, other = s.owner, name = s.def.label, made = Now });
        }

        /// <summary>A confidence betrayed (SecretTracker.Reveal): the keeper may go to the friend who told.</summary>
        public static void Betrayed(Pawn keeper, Pawn friend)
        {
            if (!Harm.On || keeper == null || friend == null || keeper == friend || !Harm.SystemOn(HarmKinds.ToldByFriend.system)) return;
            if (!HarmFour.Ours(keeper) || !HarmFour.Ours(friend) || keeper.DevelopmentalStage.Juvenile() || friend.DevelopmentalStage.Juvenile()) return;
            bool debug = DebugLeak;
            DebugLeak = false;
            if (!debug && !Harm.Roll(HarmKinds.ToldByFriend, HarmSevenMath.LeakChance)) return;
            HarmFour.Wait(new HarmWaiting { what = HarmSeven.Leak, kind = HarmKinds.ToldByFriend.key, pawn = keeper, other = friend, made = Now });
        }

        /// <summary>The wronged one goes to find the owner. True when done with.</summary>
        public static bool WrongedHour(HarmWaiting x, int now)
        {
            var p = x.pawn;
            var o = x.other;
            if (p == null || o == null || p.Dead || o.Dead || x.flag) return true;
            Together.When("wronged|" + p.thingIDNumber, p, o, () =>
            {
                if (x.flag) return;
                x.flag = true;
                // together: the wronged one came to the owner (Together.When), face to face.
                HarmFour.Say(p, "Rimfolk_Pool_WrongedOne", o);
                if (CanFight(p) && CanFight(o)) Harm.Fight(p, o, HarmKinds.WrongedOne);
            }, HarmSevenMath.FindHours, 0f);
            return x.flag;
        }

        /// <summary>The betrayed keeper goes to find the friend: hard words, and blows half the time. True when done with.</summary>
        public static bool LeakHour(HarmWaiting x, int now)
        {
            var p = x.pawn;
            var f = x.other;
            if (p == null || f == null || p.Dead || f.Dead || x.flag) return true;
            Together.When("leak|" + p.thingIDNumber, p, f, () =>
            {
                if (x.flag) return;
                x.flag = true;
                // together: the keeper came to the friend who told (Together.When), face to face.
                HarmFour.Say(p, "Rimfolk_Pool_ToldByFriend", f);
                if (Rand.Chance(HarmSevenMath.LeakBlows) && CanFight(p) && CanFight(f) && Harm.Fight(p, f, HarmKinds.ToldByFriend)) return;
                HarmFour.Say(f, "Rimfolk_Pool_ToldByFriendReply", p);
            }, HarmSevenMath.FindHours, 0f);
            return x.flag;
        }

        // ================= A rival in love comes to blows (3.10.1) =================

        private static ThoughtDef startedIt;

        /// <summary>
        /// "A rival in love" leaves its rival's stage (ArcWorker_Suitors.Settle): its stiff words were just said between the
        /// two; one story in four they become a shove, and the one they both like thinks less of the one who started it.
        /// </summary>
        public static void RivalsInLove(Arc arc)
        {
            if (!Harm.On || arc?.pawn == null || arc.third == null || arc.other == null || !Harm.SystemOn(HarmKinds.LoveRivals.system)) return;
            var a = arc.pawn;
            var b = arc.third;
            bool debug = DebugLove;
            DebugLove = false;
            if (!debug && (arc.talkedTick < 0 || Now - arc.talkedTick > HarmSevenMath.LoveTalkTicks)) return;
            if (!Together.Can(a, b) || !CanFight(a) || !CanFight(b)) return;
            if (!debug && !Harm.Roll(HarmKinds.LoveRivals, HarmSevenMath.LoveRivalChance)) return;
            var starter = Outsiders.Temper(a) >= Outsiders.Temper(b) ? a : b;
            var other = starter == a ? b : a;
            // together: Together.Can, the two rivals face to face after their words.
            HarmFour.Say(starter, "Rimfolk_Pool_LoveRivals", other);
            if (!Harm.Fight(starter, other, HarmKinds.LoveRivals)) return;
            var loved = arc.other;
            startedIt ??= DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Harm_StartedTheFight");
            if (startedIt != null && loved.needs?.mood != null && !loved.Dead) CouncilGameComponent.GiveThought(loved, startedIt, starter);
            // apart: the one they both like, watching, if they're in sight of it.
            if (Together.Able(loved) && loved.Map == starter.Map && loved.Position.InHorDistOf(starter.Position, 15f)) HarmFour.Say(loved, "Rimfolk_Pool_LovedWatches");
        }

        // ================= Words at the altar (3.13.1) =================

        private static ActiveDecree OneFaithDecree()
        {
            var def = DecreeFaith.Def;
            var all = CouncilGameComponent.Instance?.colonies;
            if (def == null || all == null) return null;
            foreach (var c in all)
            {
                var a = c?.decrees?.active?.FirstOrDefault(d => d?.def == def);
                if (a != null) return a;
            }
            return null;
        }

        /// <summary>The hour: an evening of the named faith running while the decree holds, its one roll after the decree.</summary>
        private static void Altars(int now)
        {
            if (!Harm.SystemOn(HarmKinds.AltarWords.system)) return;
            var faith = DecreeFaith.FaithOfColony;
            var tr = FaithCouncilTracker.Instance;
            var plays = PlayOutTracker.Instance;
            if (faith == null || tr?.plans == null || plays == null) return;
            var decree = OneFaithDecree();
            if (decree == null || HarmSeven.WaitingOf(HarmSeven.Altar, w => w.id == decree.startTick) != null) return;
            foreach (var plan in tr.plans)
            {
                if (plan?.faith != faith) continue;
                var play = plays.plays.FirstOrDefault(p => p != null && p.id == plan.playId && p.state == PlayState.Running);
                var zealot = plan.speaker;
                if (play?.map == null || zealot == null || !zealot.Spawned || zealot.Map != play.map || !CanFight(zealot)) continue;
                var devout = play.map.mapPawns.FreeColonistsSpawned.Where(p => p != zealot && Faiths.Devout(p) && Faiths.Of(p) != faith && CanFight(p)
                                                                              && PlayOuts.Interruptible(p) && p.CanReach(zealot, PathEndMode.Touch, Danger.Some))
                    .OrderByDescending(Outsiders.Temper).FirstOrDefault();
                if (devout == null) continue;
                // Once after the decree: the first evening with a devout of another faith at home has the roll.
                HarmFour.Wait(new HarmWaiting { what = HarmSeven.Altar, kind = HarmKinds.AltarWords.key, id = decree.startTick, made = now });
                bool debug = DebugAltar;
                DebugAltar = false;
                if (!debug && !Harm.Roll(HarmKinds.AltarWords, HarmSevenMath.AltarChance)) return;
                var theirs = Faiths.Of(devout);
                Together.When("altar|" + devout.thingIDNumber, devout, zealot, () =>
                {
                    // together: the devout came up to the evening's speaker (Together.When), face to face.
                    HarmFour.Say(devout, "Rimfolk_Pool_AltarWords", zealot);
                    if (CanFight(devout) && CanFight(zealot) && Harm.Fight(devout, zealot, HarmKinds.AltarWords))
                        HarmFour.Mote(devout, "CC_HarmSeven_Mote_Altar", (theirs?.name ?? "").Named("ONE"), faith.name.Named("OTHER"));
                }, 4f, 0f);
                return;
            }
        }

        // ================= The feast that ends in a fistfight (4.1.3) =================

        private static bool Drunk(Pawn p) => p?.health?.hediffSet != null && p.health.hediffSet.HasHediff(HediffDefOf.AlcoholHigh);

        /// <summary>Two rivals quarrelled at a day's table (SetbackTimes.Quarrel): with drink poured, it may come to blows.</summary>
        public static void TableQuarrel(Pawn starter, Pawn other)
        {
            if (!Harm.On || starter == null || other == null || !Harm.SystemOn(HarmKinds.FeastFists.system)) return;
            bool drink = Drunk(starter) || Drunk(other);
            bool debug = DebugFeast;
            DebugFeast = false;
            if (!Together.Can(starter, other) || !CanFight(starter) || !CanFight(other)) return;
            if (!debug && !Harm.Roll(HarmKinds.FeastFists, HarmSevenMath.FeastFists(drink))) return;
            // together: Together.Can, the two still at the day's table.
            HarmFour.Say(starter, "Rimfolk_Pool_FeastFists", other);
            if (Harm.Fight(starter, other, HarmKinds.FeastFists)) HarmFour.Mote(starter, "CC_HarmSeven_Mote_Feast");
        }

        // ================= The toast's second round (4.7.3) =================

        private static bool? brewing;

        /// <summary>Vanilla Brewing Expanded is loaded: its strong drink in plenty can pour a second round.</summary>
        public static bool BrewingLoaded => brewing ??= ModsConfig.IsActive("vanillaexpanded.vbrewe");

        private static bool Alcohol(ThingDef d) => ToastDrinks.IsToastDrink(d) && d.GetCompProperties<CompProperties_Drug>()?.chemical == ChemicalDefOf.Alcohol;

        /// <summary>A toast's 250-tick look (PlayOutLord): once its first round is drunk, with strong drink in plenty, one toast in three pours a second.</summary>
        public static void SecondRound(Lord lord, Map map, IntVec3 spot, bool words)
        {
            if (words || lord == null || map == null || !Harm.On || !Harm.SystemOn(HarmKinds.SecondRound.system)) return;
            if (lord.ticksInToil < HarmSevenMath.SecondRoundAfter || lord.ticksInToil > HarmSevenMath.SecondRoundEnd || !Look("round|" + lord.loadID)) return;
            var drank = lord.ownedPawns.Where(p => p != null && p.Spawned && p.Map == map && Drunk(p) && !AddictionUtility.AddictedToAnything(p) && PlayOuts.CanJoin(p)
                                                    && p.CurJobDef != JobDefOf.Ingest).ToList();
            if (drank.Count < 2) return;
            var drinks = map.listerThings.ThingsInGroup(ThingRequestGroup.Drug)
                .Where(t => t != null && t.Spawned && !t.IsForbidden(Faction.OfPlayer) && Alcohol(t.def) && (t.IsInAnyStorage() || map.areaManager.Home[t.Position])).ToList();
            bool debug = DebugRound;
            DebugRound = false;
            if (!HarmSevenMath.Plenty(drinks.Sum(t => t.stackCount), drank.Count) && !debug) return;
            if (!debug && !Rand.Chance(HarmSevenMath.SecondRoundChance)) return;
            var second = new List<Pawn>();
            foreach (var p in drank)
            {
                DrugPolicyEntry entry = null;
                var t = drinks.Where(d => !d.Destroyed && d.Spawned && !d.IsForbidden(p) && p.CanReserve(d, 10, 1) && p.CanReach(d, PathEndMode.ClosestTouch, Danger.Some))
                    .OrderBy(d => d.Position.DistanceToSquared(spot)).FirstOrDefault();
                if (t == null) continue;
                try
                {
                    entry = p.drugs?.CurrentPolicy?[t.def];
                }
                catch
                {
                    // Not in their policy: it doesn't allow it.
                }
                if (entry == null || !entry.allowedForJoy || Beliefs.MindsDrug(p, t.def)) continue;
                var job = JobMaker.MakeJob(JobDefOf.Ingest, t);
                job.count = 1;
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (p.CurJobDef == JobDefOf.Ingest) second.Add(p);
            }
            if (second.Count == 0) return;
            // apart: the call for another round, to the whole toast.
            HarmFour.Say(second[0], "Rimfolk_Pool_SecondRound");
            HarmSeven.Later(HarmSevenMath.SecondQuarrelDelay, () => SecondQuarrel(second));
        }

        private static void SecondQuarrel(List<Pawn> second)
        {
            for (int i = 0; i < second.Count; i++)
                for (int j = i + 1; j < second.Count; j++)
                {
                    var a = second[i];
                    var b = second[j];
                    if (!Rivals(a, b) || !Together.Can(a, b) || !CanFight(a) || !CanFight(b)) continue;
                    if (!Harm.Roll(HarmKinds.SecondRound, HarmSevenMath.SecondRoundFists)) return;
                    var starter = Outsiders.Temper(a) >= Outsiders.Temper(b) ? a : b;
                    var other = starter == a ? b : a;
                    // together: Together.Can, the two still at the toast.
                    HarmFour.Say(starter, "Rimfolk_Pool_FeastFists", other, "second");
                    if (Harm.Fight(starter, other, HarmKinds.SecondRound)) HarmFour.Mote(starter, "CC_HarmSeven_Mote_Feast");
                    return;
                }
        }

        /// <summary>Dev mode: a quarrel between the nearest two rivals here, as after the second round, past its rate.</summary>
        public static string DebugQuarrel(Map map)
        {
            var ours = map.mapPawns.FreeColonistsSpawned.ToList();
            foreach (var a in ours)
                foreach (var b in ours)
                    if (a != b && Rivals(a, b) && Together.Can(a, b) && CanFight(a) && CanFight(b))
                    {
                        HarmFour.Say(a, "Rimfolk_Pool_FeastFists", b, "second");
                        return Harm.Fight(a, b, HarmKinds.SecondRound) ? a.LabelShort + " and " + b.LabelShort + " came to blows over the second round." : "the cap held them back.";
                    }
            return "no two rivals together and free to fight here.";
        }

        // ================= The soldier's nightmare (3.12.2) =================

        private static HediffDef shaken;

        private static void Soldier(Pawn p)
        {
            if (p.DevelopmentalStage.Juvenile() || RestUtility.Awake(p) || !p.InBed() || !HarmFiveMath.SmallHours(GenLocalDate.HourOfDay(p.Map))) return;
            if (!Harm.SystemOn(HarmKinds.SoldierNight.system)) return;
            shaken ??= HarmFour.Def("Council_Dream_Shaken");
            var h = shaken != null ? p.health.hediffSet.GetFirstHediffOfDef(shaken) : null;
            if (h == null || !Look("soldier|" + h.loadID)) return;
            string titles = (p.story?.Childhood?.title ?? "") + " " + (p.story?.Adulthood?.title ?? "");
            bool debug = DebugSoldier;
            if (!HarmSevenMath.SoldierPast(titles) && !debug) return;
            var room = RestUtility.CurrentBed(p)?.GetRoom();
            Pawn mate = null;
            if (room != null && !room.PsychologicallyOutdoors)
                foreach (var bed in room.ContainedBeds)
                    foreach (var o in bed.CurOccupants)
                        if (o != p && HarmFour.Ours(o) && !o.DevelopmentalStage.Baby())
                            mate = o;
            // A bedroom of their own: nobody to wake them, nobody to strike.
            if (mate == null) return;
            DebugSoldier = false;
            var cls = HarmFour.Child(mate) ? HurtClass.Mild : HurtClass.Injury;
            // The nightmare's own roll, the same after a reload.
            if (!debug && (!Harm.May(mate, cls) || Rand.ValueSeeded(h.loadID * 7919 + 131) >= Harm.Chance(HarmKinds.SoldierNight, HarmSevenMath.SoldierChance))) return;
            // together: the two share the bedroom; the roommate wakes them, and they come up swinging.
            mate.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            HarmFour.Say(p, "Rimfolk_Pool_SoldierShout");
            if (Harm.Injure(mate, HurtKind.Bruise, HurtWhere.Arm, HarmSevenMath.StrikeLow, HarmSevenMath.StrikeHigh, HarmKinds.SoldierNight, HurtClass.Mild, p) == null) return;
            // apart: the roommate, holding an arm, to the dark.
            HarmFour.Say(mate, "Rimfolk_Pool_SoldierStruck");
            HarmFour.Mote(mate, "CC_HarmSeven_Mote_Soldier");
        }
    }
}
