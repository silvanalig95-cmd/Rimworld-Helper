using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Handing a colony over (docs/design/stage3-many-colonies.md, section 2; release 2a, the player's decisions 2A,
    /// 6B, 8A and 9A of 6 November 2026). From the colony's card or the settlement's world buttons the player chooses who
    /// stays and who goes (each grown-up's wish shown), the head and the head's second (by office or standing, never by
    /// blood), the people's name by the age's word, the terms (free, or a defence or trade pact), what the colony's
    /// prisoners and slaves do, and where those who go go (another home, a fresh start in the same act, or a caravan the
    /// player forms). Then, all on the map: the colony's meeting gathers and votes (each member by their wish and their
    /// regard for the council); the player goes ahead (against the vote it costs trust among those who stay) and the day
    /// is set; that day those who stay and those who go gather at the meeting spot, the head speaks, the banner, totem,
    /// keys or codes change hands by the age's form, and those close say their farewells; those who go leave by the
    /// colony's means. When the last of them has left the map, the town is made: a new people of Rimfolk's own for the
    /// colony's age (decision 6B), its faith the colony's, its leader the head, allied with the colony and half as warm
    /// as the colony with every other people; those who stay, and the animals, are its people; the settlement is theirs;
    /// the game closes the map as it does any town's. The records: the colony's ends "handed over", a town the colony was
    /// (its fortune seeded from the colony's wealth per head, its founding the colony's, its founder), its notables (the
    /// head, the head's second, the envoy, the trader), the rest kept in the world, the milestone and a letter. The bond
    /// in goodwill (decision 8A) is <see cref="GoodwillSituationWorker_OnceOf"/>; the heads' rule is
    /// <see cref="NextLeader"/>, through a prefix on the game's own succession (Patch_HandOverLeader).
    /// <para>
    /// Nothing per tick and no schedule slot of its own: what is under way rides the region's hour (<see cref="Pass.Region"/>),
    /// the bond's look its day. With one colony and nothing handed over nothing runs and nothing is saved.
    /// Release 2b: the colony's layout is kept as the town is made (<see cref="Make"/>, Colonies/TownLayout.cs, decision
    /// 7A); what the town does afterwards (its letters, trade, days, homecomings, moving there) is Colonies/TownsAfter.cs,
    /// and taking it back, by its people's word or by force (decision 10A), Colonies/TownTakeBack.cs.
    /// </para>
    /// </summary>
    public static class HandOvers
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Hand-overs are offered (Many colonies: "Handing over").</summary>
        public static bool On => S?.handOvers ?? true;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>What a play-out of a hand-over says when it is over (PlayOut.afterward): "handover:ID:meeting" or ":ceremony".</summary>
        public const string AfterPrefix = "handover:";

        // ================= Reading =================

        public static IEnumerable<HandOverPlan> All => Homes.Instance?.handOvers?.Where(h => h != null) ?? Enumerable.Empty<HandOverPlan>();

        public static bool Any => (Homes.Instance?.handOvers?.Count ?? 0) > 0;

        /// <summary>The hand-over under way (one at a time), or null.</summary>
        public static HandOverPlan Current => All.FirstOrDefault();

        public static HandOverPlan Of(int id) => All.FirstOrDefault(h => h.id == id);

        /// <summary>The hand-over of this home, or null.</summary>
        public static HandOverPlan At(Map home)
        {
            if (home == null) return null;
            int key = Homes.KeyOf(home);
            return All.FirstOrDefault(h => h.home == key);
        }

        /// <summary>A fresh start in the same act, kept with its hand-over until the ceremony is over (no other founding meanwhile).</summary>
        public static FoundingPlan PendingFounding => All.Select(h => h.founding).FirstOrDefault(f => f != null);

        /// <summary>The hand-over a founding that has set out belongs to, or null.</summary>
        public static HandOverPlan OfFounding(int foundingId) => foundingId < 0 ? null : All.FirstOrDefault(h => h.foundingId == foundingId);

        /// <summary>One of the colony's former people, kept in the world for a town the colony was (KeepFor.Former).</summary>
        public static bool Holds(Pawn p)
        {
            if (p == null) return false;
            foreach (var r in WorldLedger.Formers)
                if (r != null && r.formers.Contains(p) && r.Standing)
                    return true;
            return false;
        }

        /// <summary>A town the colony was, standing (its notables stay among the colony's neighbours wherever it lies).</summary>
        public static bool IsFormerTown(int id)
        {
            var r = WorldLedger.FormerOf(id);
            return r != null && r.Standing;
        }

        // ================= Before: the checks =================

        /// <summary>
        /// Why this home can't be handed over now, before anyone is chosen (in words), or null: the setting, a home of the
        /// colony's on the ground that is a settlement, one hand-over at a time, no founding under way from it, no fight
        /// and no banishment there.
        /// </summary>
        public static string WhyNot(Map home)
        {
            if (!On) return "CC_HandOver_No_Off".Translate();
            if (home == null || !Homes.IsHome(home)) return "CC_HandOver_No_NotHome".Translate();
            if (!(home.Parent is Settlement) || MapFacts.InVacuum(home)) return "CC_HandOver_No_NotSettlement".Translate();
            var under = Current;
            if (under != null) return "CC_HandOver_No_UnderWay".Translate((under.homeName ?? "?").Named("HOME")).Resolve();
            var f = Foundings.Current;
            if (f != null && f.from == Homes.KeyOf(home)) return "CC_HandOver_No_Founding".Translate(f.place.Named("PLACE")).Resolve();
            if (Fight(home)) return "CC_HandOver_No_Fight".Translate();
            if (Banishing(home)) return "CC_HandOver_No_Banishment".Translate();
            return null;
        }

        /// <summary>A fight or a threat at that home now.</summary>
        public static bool Fight(Map home) => home != null && (MapFacts.ThreatOn(home) || (CouncilGameComponent.Instance?.social?.BattleOn(home) ?? false));

        /// <summary>A banishment under way at that home (someone to be told, or gathering their things).</summary>
        public static bool Banishing(Map home)
        {
            var b = BanishTracker.Instance;
            if (b == null || home == null) return false;
            return b.records.Any(r => r?.pawn != null && !r.pawn.Dead && (r.stage == BanishStage.ToTell || r.stage == BanishStage.Gathering) && Homes.Of(r.pawn) == home);
        }

        /// <summary>A grav anchor on the map (Odyssey): the town can't keep it, and the game would keep the map a home.</summary>
        public static bool Anchor(Map home) => ModsConfig.OdysseyActive && home != null && home.listerThings.AnyThingWithDef(ThingDefOf.GravAnchor);

        /// <summary>The gravship's engine on the map (Odyssey): it leaves with those who go, or is taken apart, before the town is made.</summary>
        public static bool Engine(Map home) => ModsConfig.OdysseyActive && home != null && home.listerThings.AnyThingWithDef(ThingDefOf.GravEngine);

        /// <summary>Grown colonists of the player's anywhere but this home and those listed as going.</summary>
        public static int ColonistsElsewhere(Map home, ICollection<Pawn> going)
        {
            int n = 0;
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
                if (p != null && p.DevelopmentalStage.Adult() && (going == null || !going.Contains(p)) && p.MapHeld != home && Homes.Of(p) != home)
                    n++;
            return n;
        }

        /// <summary>
        /// Why the hand-over can't be set going as the window stands (in words), or null: the static checks, then the
        /// rule's (three grown-ups stay, someone of the player's elsewhere, no grav anchor).
        /// </summary>
        public static string WhyNot(Map home, List<FounderRow> rows, ICollection<Pawn> going, bool newcomers)
        {
            string no = WhyNot(home);
            if (no != null) return no;
            int grownStaying = rows.Count(r => r.grown && !going.Contains(r.pawn));
            int grownGoing = rows.Count(r => r.grown && going.Contains(r.pawn));
            bool elsewhere = HandOverMath.PlayersElsewhere(grownGoing, ColonistsElsewhere(home, going), newcomers);
            string key = HandOverMath.WhyNot(true, false, false, grownStaying, elsewhere, Anchor(home));
            return key == null ? null : ("CC_HandOver_No_" + key).Translate(HandOverMath.MinGrownStay.Named("COUNT")).Resolve();
        }

        // ================= The head and the head's second =================

        /// <summary>How the others think of someone: their opinions of them, and the colony's view of them (Social/Gossip/).</summary>
        public static float Standing(Pawn p, IEnumerable<Pawn> others)
        {
            if (p == null) return 0f;
            float sum = 0f;
            int n = 0;
            foreach (var o in others)
            {
                if (o == null || o == p || o.relations == null) continue;
                sum += o.relations.OpinionOf(p);
                n++;
            }
            float view = 0f;
            try
            {
                view = RepMath.Esteem(Reputations.Standing(p)) * 0.5f;
            }
            catch (Exception)
            {
                view = 0f;
            }
            return (n > 0 ? sum / n : 0f) + view;
        }

        /// <summary>Who of those who stay could head the town, in the order given, each by office or standing.</summary>
        public static List<HeadCandidate> Candidates(Map home, List<Pawn> grownStaying)
        {
            var colony = ColonyState.For(home);
            var speaker = colony?.offices?.Speaker;
            return grownStaying.Select(p => new HeadCandidate(p == speaker, colony?.offices?.OfficeOf(p)?.order ?? -1, Standing(p, grownStaying))).ToList();
        }

        /// <summary>The head the rule gives (the player may pick another in the window).</summary>
        public static Pawn HeadOf(Map home, List<Pawn> grownStaying, Pawn picked = null)
        {
            if (grownStaying == null || grownStaying.Count == 0) return null;
            int i = HandOverMath.Head(Candidates(home, grownStaying), picked != null ? grownStaying.IndexOf(picked) : -1);
            return i >= 0 ? grownStaying[i] : null;
        }

        /// <summary>The head's second the rule gives (the player may pick another in the window).</summary>
        public static Pawn SecondOf(Map home, List<Pawn> grownStaying, Pawn head, Pawn picked = null)
        {
            if (grownStaying == null || grownStaying.Count < 2) return null;
            int i = HandOverMath.Second(Candidates(home, grownStaying), grownStaying.IndexOf(head), picked != null ? grownStaying.IndexOf(picked) : -1);
            return i >= 0 ? grownStaying[i] : null;
        }

        // ================= Setting it going =================

        /// <summary>The window, from the colony's card or the settlement's button (or from the founding window, with its founding).</summary>
        public static void Open(Map home, FoundingPlan founding = null)
        {
            string why = WhyNot(home);
            if (why != null)
            {
                Messages.Message(why, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.WindowStack.Add(new Dialog_HandOver(home, founding));
        }

        /// <summary>"Put it to the meeting": the plan stored and the colony's meeting called to vote on it.</summary>
        public static void Begin(HandOverPlan plan)
        {
            var h = Homes.Instance;
            var home = Homes.MapOf(plan.home);
            if (h == null || home == null) return;
            plan.id = h.nextHandOver++;
            plan.started = Now;
            plan.stage = HandOverStage.Meeting;
            plan.homeName = Homes.Name(home);
            h.handOvers.Add(plan);
            Meeting(plan, home);
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>The members who vote: the home's grown-ups there now, of the colony's own.</summary>
        private static List<Pawn> Members(Map home) =>
            Homes.People(home).Where(p => p.DevelopmentalStage.Adult() && PlayOuts.OurPerson(p) && p.MapHeld == home).ToList();

        /// <summary>
        /// The colony's meeting on it: a gathering at the meeting spot (the dining table first, as the council's own
        /// meetings), its chair the speaker, the question talked over; the vote is counted among those who came when it is
        /// over. With answers not played out on the map, the members' view is counted at once.
        /// </summary>
        private static void Meeting(HandOverPlan plan, Map home)
        {
            var members = Members(home);
            var speaker = ColonyState.For(home)?.offices?.Speaker;
            var chair = members.Contains(speaker) ? speaker : members.Contains(plan.head) ? plan.head : members.FirstOrDefault();
            var tracker = PlayOutTracker.Instance;
            if (PlayOuts.On && tracker != null && members.Count > 0)
            {
                try
                {
                    var p = new PlayOut
                    {
                        kind = PlayKind.Toast, map = home, created = Now, label = "CC_HandOver_MeetingLabel".Translate().Resolve(), invited = members, organizer = chair,
                        spots = new List<PlaySpot> { PlaySpot.Dining, PlaySpot.Rec, PlaySpot.Gather, PlaySpot.Outdoors }, durationTicks = PlayOutMath.DurationTicks(HandOverMath.MeetingHours),
                        pool = "Rimfolk_Pool_HandOverMeeting", hourFrom = HandOverMath.MeetingFrom, hourTo = HandOverMath.MeetingTo, words = true, music = false,
                        afterward = AfterPrefix + plan.id + ":meeting"
                    };
                    p.deadline = PlayOutMath.Deadline(Now, GenLocalDate.HourOfDay(home), p.hourFrom, p.hourTo, HandOverMath.WaitHours);
                    tracker.Add(p);
                    plan.play = p.id;
                    // apart: the colony told of the meeting called; the members gather for it on the map.
                    Tell.Message(NewsKind.Colony, Importance.Important, home, "CC_HandOver_MeetingCalled".Translate(plan.homeName.Named("HOME"), (chair?.LabelShort ?? "?").Named("CHAIR")).Resolve(),
                        new LookTargets(members.Where(x => x.Spawned)), MessageTypeDefOf.NeutralEvent);
                    return;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hand-over: the meeting", e);
                }
            }
            Vote(plan, null);
        }

        /// <summary>
        /// The meeting's vote: each member by their wish and their regard for the council (HandOverMath.VoteOf), among those
        /// who came (or every member, when nobody gathered), told in a letter that asks the player to go ahead or call it off.
        /// </summary>
        private static void Vote(HandOverPlan plan, List<Pawn> came)
        {
            var home = Homes.MapOf(plan.home);
            if (home == null) return;
            var members = Members(home);
            var voters = came != null && came.Count > 0 ? members.Where(came.Contains).ToList() : members;
            var going = new HashSet<Pawn>(plan.going);
            var rows = Foundings.Rows(home, going);
            var trust = CouncilGameComponent.Instance?.standings;
            var yes = new List<Pawn>();
            var no = new List<Pawn>();
            var torn = new List<Pawn>();
            foreach (var p in voters)
            {
                var row = rows.FirstOrDefault(r => r.pawn == p);
                var wish = row?.wish ?? FoundingWish.Torn;
                var v = HandOverMath.VoteOf(wish, going.Contains(p), trust?.TrustOf(p) ?? 0f);
                (v == HandOverVote.For ? yes : v == HandOverVote.Against ? no : torn).Add(p);
            }
            plan.voteFor = yes.Count;
            plan.voteAgainst = no.Count;
            plan.voted = true;
            plan.stage = HandOverStage.Voted;
            plan.play = -1;
            bool carried = HandOverMath.Carried(yes.Count, no.Count);
            string text = (carried ? "CC_HandOver_VoteFor" : "CC_HandOver_VoteAgainst").Translate(plan.homeName.Named("HOME"), yes.Count.Named("FOR"), no.Count.Named("AGAINST")).Resolve();
            if (yes.Count > 0) text += "\n\n" + "CC_HandOver_VoteYes".Translate(Foundings.Names(yes).Named("NAMES")).Resolve();
            if (no.Count > 0) text += "\n" + "CC_HandOver_VoteNo".Translate(Foundings.Names(no).Named("NAMES")).Resolve();
            if (torn.Count > 0) text += "\n" + "CC_HandOver_VoteTorn".Translate(Foundings.Names(torn).Named("NAMES")).Resolve();
            text += "\n\n" + (carried ? "CC_HandOver_VoteAsk" : "CC_HandOver_VoteAskAgainst").Translate(Mathf.RoundToInt(-HandOverMath.TrustAgainstVote).Named("TRUST")).Resolve();
            FoundingLetters.AskHandOver(plan, home, "CC_HandOver_VoteLabel".Translate(plan.homeName.Named("HOME")).Resolve(), text);
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>
        /// The player's word after the vote: go ahead (the day set for tomorrow; against the vote it costs trust among those
        /// who stay), or call it off.
        /// </summary>
        public static void GoAhead(HandOverPlan plan)
        {
            var home = Homes.MapOf(plan?.home ?? HomesMath.None);
            if (plan == null || home == null || plan.stage != HandOverStage.Voted) return;
            bool carried = HandOverMath.Carried(plan.voteFor, plan.voteAgainst);
            float cost = HandOverMath.TrustCost(carried, true);
            if (cost < 0f)
            {
                plan.wentAgainst = true;
                var comp = CouncilGameComponent.Instance;
                string why = "CC_HandOver_TrustWhy".Translate(plan.homeName.Named("HOME")).Resolve();
                if (comp != null)
                    foreach (var p in plan.staying.Where(p => p != null && !p.Dead && p.IsColonist))
                        comp.standings.Change(p, cost, why);
            }
            plan.stage = HandOverStage.Set;
            plan.dayAt = HandOverMath.DaySet(Now, GenLocalDate.HourOfDay(home));
            FoundingLetters.RemoveHandOverLetters(plan);
            // direct: the answer to the player's own click in the vote's letter.
            Messages.Message("CC_HandOver_DaySetMsg".Translate(plan.homeName.Named("HOME"), DateOf(home, plan.dayAt).Named("DATE")).Resolve(), MessageTypeDefOf.NeutralEvent, false);
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>The hand-over called off (the vote's letter, or the colony's card): nothing more comes of it, and its founding, not yet set out, with it.</summary>
        public static void CallOff(HandOverPlan plan, bool quiet = false)
        {
            var h = Homes.Instance;
            if (h == null || plan == null) return;
            var tracker = PlayOutTracker.Instance;
            var play = plan.play >= 0 ? tracker?.Get(plan.play) : null;
            plan.play = -1;
            if (play != null && play.state != PlayState.Done)
            {
                play.afterward = null;
                tracker.Fallback(play, "CC_HandOver_CalledOffWhy".Translate().Resolve(), true);
            }
            h.handOvers.Remove(plan);
            FoundingLetters.RemoveHandOverLetters(plan);
            // direct: the answer to the player's own click (the card's or the letter's "Call it off").
            if (!quiet) Messages.Message("CC_HandOver_CalledOff".Translate((plan.homeName ?? "?").Named("HOME")).Resolve(), MessageTypeDefOf.NeutralEvent, false);
            LensModel.Dirty();
            Cards.Dirty();
        }

        // ================= The hour =================

        /// <summary>
        /// In the region's hour: the meeting or the ceremony that fell out of the play-outs' hands is counted all the same,
        /// the ceremony is set going on the day set, and once those who go have left the map the town is made.
        /// </summary>
        public static void Hourly(int now)
        {
            foreach (var plan in All.ToList())
            {
                try
                {
                    Hour(plan, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hand-over: the hour", e);
                }
            }
        }

        private static void Hour(HandOverPlan plan, int now)
        {
            var home = Homes.MapOf(plan.home);
            if (home == null || !(home.Parent is Settlement))
            {
                // The home is gone (abandoned, taken, a gravship's takeoff): nothing more comes of it.
                Homes.Instance?.handOvers.Remove(plan);
                if (home == null && plan.founding != null)
                    Tell.Minor(NewsKind.Colony, Importance.Notable, null, "CC_HandOver_HomeGone".Translate((plan.homeName ?? "?").Named("HOME")).Resolve(), LookTargets.Invalid);
                LensModel.Dirty();
                Cards.Dirty();
                return;
            }
            var tracker = PlayOutTracker.Instance;
            switch (plan.stage)
            {
                case HandOverStage.Meeting:
                    if (plan.play < 0 || tracker?.Get(plan.play) == null || tracker.Get(plan.play).state == PlayState.Done) Vote(plan, null);
                    break;
                case HandOverStage.Set:
                    int hour = GenLocalDate.HourOfDay(home);
                    if (now >= plan.dayAt && hour >= HandOverMath.CeremonyFrom && hour < HandOverMath.CeremonyTo && !Fight(home)) Ceremony(plan, home);
                    break;
                case HandOverStage.Ceremony:
                    if (plan.play < 0 || tracker?.Get(plan.play) == null || tracker.Get(plan.play).state == PlayState.Done) CeremonyOver(plan, false, null);
                    break;
                case HandOverStage.Leaving:
                    WhenLeft(plan, home);
                    break;
            }
        }

        // ================= The ceremony =================

        /// <summary>
        /// The ceremony, on the day set: those who stay and those who go gather at the meeting spot and the head speaks
        /// (their words while it lasts, Rimfolk_Pool_HandOver); when it is over the banner, totem, keys or codes change hands
        /// and those close say their farewells. With answers not played out on the map, the head's word and the farewells
        /// at once.
        /// </summary>
        private static void Ceremony(HandOverPlan plan, Map home)
        {
            plan.stage = HandOverStage.Ceremony;
            var everyone = plan.going.Concat(plan.staying).Where(p => p != null && PlayOuts.OurPerson(p) && p.MapHeld == home).Distinct().ToList();
            var head = HeadNow(plan, home);
            var tracker = PlayOutTracker.Instance;
            if (PlayOuts.On && tracker != null && everyone.Count > 0)
            {
                try
                {
                    var form = Form(plan);
                    var p = new PlayOut
                    {
                        kind = PlayKind.Toast, map = home, created = Now, label = "CC_HandOver_CeremonyLabel".Translate(form.word.Named("FORM")).Resolve(), invited = everyone,
                        organizer = everyone.Contains(head) ? head : null, spots = new List<PlaySpot> { PlaySpot.Dining, PlaySpot.Rec, PlaySpot.Gather, PlaySpot.Outdoors },
                        durationTicks = PlayOutMath.DurationTicks(HandOverMath.CeremonyHours), pool = "Rimfolk_Pool_HandOver", hourFrom = HandOverMath.CeremonyFrom,
                        hourTo = HandOverMath.CeremonyTo, words = true, music = false, afterward = AfterPrefix + plan.id + ":ceremony"
                    };
                    p.deadline = PlayOutMath.Deadline(Now, GenLocalDate.HourOfDay(home), p.hourFrom, p.hourTo, HandOverMath.WaitHours);
                    tracker.Add(p);
                    plan.play = p.id;
                    // apart: the colony told of the ceremony; everyone gathers for it on the map.
                    Tell.Message(NewsKind.Colony, Importance.Important, home, "CC_HandOver_CeremonyCalled".Translate(plan.homeName.Named("HOME"), form.word.Named("FORM")).Resolve(),
                        new LookTargets(everyone.Where(x => x.Spawned)), MessageTypeDefOf.NeutralEvent);
                    return;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hand-over: the ceremony", e);
                }
            }
            CeremonyOver(plan, true, null);
        }

        /// <summary>A play-out of a hand-over is over (PlayAfter.Run): the meeting's vote, or the ceremony's end.</summary>
        public static void PlayOver(PlayOut p, string what, bool happened, List<Pawn> took)
        {
            if (p == null || what == null || !what.StartsWith(AfterPrefix, StringComparison.Ordinal)) return;
            var parts = what.Substring(AfterPrefix.Length).Split(':');
            if (parts.Length < 2 || !int.TryParse(parts[0], out int id)) return;
            var plan = Of(id);
            if (plan == null || plan.play != p.id) return;
            try
            {
                if (parts[1] == "meeting" && plan.stage == HandOverStage.Meeting) Vote(plan, happened ? took : null);
                else if (parts[1] == "ceremony" && plan.stage == HandOverStage.Ceremony) CeremonyOver(plan, happened, took);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hand-over: after the gathering", e);
            }
        }

        /// <summary>
        /// The ceremony is over (or never gathered): the token changes hands where the head and the one who hands it over
        /// are together, those close say their farewells, and those who go set out by the colony's means.
        /// </summary>
        private static void CeremonyOver(HandOverPlan plan, bool happened, List<Pawn> took)
        {
            var home = Homes.MapOf(plan.home);
            plan.play = -1;
            plan.stage = HandOverStage.Leaving;
            if (home == null) return;
            if (happened) Token(plan, home);
            Farewells(plan, home);
            Leave(plan, home);
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>
        /// The banner, totem, keys or codes handed over by the age's form: the one who gives it (the speaker if they go,
        /// else an office holder who goes, else the eldest who goes) says so to the head, and the head answers, when the two
        /// are together; else it waits a few hours for them to be.
        /// </summary>
        private static void Token(HandOverPlan plan, Map home)
        {
            var head = HeadNow(plan, home);
            var giver = Giver(plan, home);
            if (head == null || giver == null || head == giver) return;
            Together.When("handover-token-" + plan.id, giver, head, () =>
            {
                plan.tokenGiven = true;
                if (!Speech.On) return;
                // together: said face to face, the moment the two are together (Together.When).
                Speech.Say(giver, Flavor.FromPool("Rimfolk_Pool_HandOverGive", giver, null, null), head);
                Speech.Say(head, Flavor.FromPool("Rimfolk_Pool_HandOverTake", head, null, null), giver);
                SocialLog.Add(giver, head, "CC_HandOver_TokenLog".Translate(giver.Named("PAWN"), head.Named("OTHER"), TokenWord(plan).Named("TOKEN")).Resolve(), false);
            }, 4f, 0f);
        }

        /// <summary>Who hands the token over: the speaker if they go, else an office holder who goes, else the eldest grown-up who goes; with nobody going, the eldest who stays but the head.</summary>
        private static Pawn Giver(HandOverPlan plan, Map home)
        {
            var colony = ColonyState.For(home);
            var going = plan.going.Where(p => p != null && !p.Dead && p.Spawned && p.Map == home && p.DevelopmentalStage.Adult() && PlayOuts.OurPerson(p)).ToList();
            var speaker = colony?.offices?.Speaker;
            if (speaker != null && going.Contains(speaker)) return speaker;
            var office = going.Where(p => colony?.offices?.OfficeOf(p) != null).OrderBy(p => colony.offices.OfficeOf(p).order).FirstOrDefault();
            if (office != null) return office;
            var eldest = going.OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();
            if (eldest != null) return eldest;
            var head = HeadNow(plan, home);
            return plan.staying.Where(p => p != null && p != head && !p.Dead && p.Spawned && p.Map == home && p.DevelopmentalStage.Adult() && PlayOuts.OurPerson(p))
                .OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();
        }

        /// <summary>
        /// Farewells: each who goes says goodbye to their closest who stay (a partner, a parent or child, a sibling, a close
        /// friend: two each, twelve in all), face to face, now if they are together, else when they meet in the next hours
        /// (one may go and find the other).
        /// </summary>
        private static void Farewells(HandOverPlan plan, Map home)
        {
            int n = 0;
            var stay = plan.staying.Where(p => p != null && !p.Dead && p.MapHeld == home && PlayOuts.OurPerson(p)).ToList();
            foreach (var g in plan.going.Where(p => p != null && !p.Dead && p.MapHeld == home && PlayOuts.OurPerson(p)))
            {
                int each = 0;
                foreach (var s in stay.Where(s => Foundings.IsClose(g, s)).OrderByDescending(s => g.relations.OpinionOf(s)))
                {
                    if (each >= HandOverMath.FarewellsEach || n >= HandOverMath.MaxFarewells) break;
                    each++;
                    n++;
                    var a = g;
                    var b = s;
                    Together.When("handover-farewell-" + plan.id + "-" + a.thingIDNumber + "-" + b.thingIDNumber, a, b, () =>
                    {
                        if (Speech.On)
                        {
                            // together: said face to face, the moment the two are together (Together.When).
                            Speech.Say(a, Flavor.FromPool("Rimfolk_Pool_HandOverFarewell", a, null, null, "going"), b);
                            Speech.Say(b, Flavor.FromPool("Rimfolk_Pool_HandOverFarewell", b, null, null, "staying"), a);
                        }
                        SocialLog.Add(a, b, "CC_HandOver_FarewellLog".Translate(a.Named("PAWN"), b.Named("OTHER")).Resolve(), false);
                    }, 8f, 1f);
                }
            }
        }

        /// <summary>
        /// Those who go set out by the colony's means: to another home by the journey planner's plan; with a founding
        /// party, the fresh start of the same act sets out (its caravan, pods, gravship or newcomers); or the player forms
        /// their caravan, or sends them by pods or the gravship (a letter says so).
        /// </summary>
        private static void Leave(HandOverPlan plan, Map home)
        {
            var going = plan.going.Where(p => p != null && !p.Dead && p.Spawned && p.Map == home).ToList();
            switch (plan.where)
            {
                case HandOverWhere.Founding:
                    if (plan.founding != null)
                    {
                        var f = plan.founding;
                        plan.founding = null;
                        f.party = plan.going.Where(p => p != null && !p.Dead).ToList();
                        f.handOver = true;
                        Foundings.Begin(f);
                        plan.foundingId = f.id;
                    }
                    break;
                case HandOverWhere.Home:
                    var to = Homes.MapOf(plan.toHome);
                    if (to != null && going.Count > 0 && JourneyBook.On && JourneyBook.PlanAt(home) == null)
                    {
                        var j = PlanJourney(home, to, going);
                        if (j != null)
                        {
                            plan.journey = j.id;
                            if (Find.CurrentMap == home) Dialog_JourneyPlan.Open(true);
                            break;
                        }
                    }
                    if (going.Count > 0) FoundingLetters.AskHandOverCaravan(plan, home, going, to);
                    break;
                default:
                    if (going.Count > 0) FoundingLetters.AskHandOverCaravan(plan, home, going, null);
                    break;
            }
        }

        /// <summary>The journey planner's plan for those who go, one way to the other home, with food for the road.</summary>
        private static JourneyPlan PlanJourney(Map from, Map to, List<Pawn> going)
        {
            try
            {
                int now = Find.TickManager.TicksAbs;
                var j = new JourneyPlan
                {
                    map = from, travellers = going.Where(p => p.RaceProps.Humanlike).ToList(),
                    departAbs = JourneyRun.HourStart(now) + JourneyMath.DefaultHoursAhead(JourneyRun.HourOfDay(now, from)) * GenDate.TicksPerHour,
                    andBack = false, destTile = to.Tile.tileId, destObject = to.Parent.ID, destName = Homes.Name(to), daysChosen = false
                };
                var route = JourneyPack.Route(from, j.DestTile, j.travellers, 0f, j.departAbs, false, -1f);
                j.days = route.oneWayTicks > 0 ? JourneyMath.TripDays(route.oneWayTicks, false) : 2f;
                j.low = route.low;
                j.high = route.high;
                j.packing = JourneyPack.Packing(from, j.travellers, j.animals, j.days, route, null);
                JourneyRun.Confirm(j, true);
                return j;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hand-over: the journey", e);
                return null;
            }
        }

        /// <summary>
        /// Once the ceremony is over: when those who go have left the map (and anyone the colony hosts with them, and the
        /// new colony of the same act stands when nobody goes from here), the town is made at the next frame. What it
        /// waits for is told once each.
        /// </summary>
        private static void WhenLeft(HandOverPlan plan, Map home)
        {
            var goingHere = plan.going.Where(p => p != null && !p.Dead && p.MapHeld == home).Concat(Visitors(plan, home)).ToList();
            var lodgers = home.mapPawns.AllPawnsSpawned.Where(p => p.IsQuestLodger() && p.HostFaction == Faction.OfPlayer).ToList();
            var founding = plan.foundingId >= 0 ? Foundings.Of(plan.foundingId) : null;
            bool toStand = plan.where == HandOverWhere.Founding && plan.going.Count == 0 && (plan.founding != null || founding != null && founding.stage == FoundingStage.Going);
            int grown = StayingNow(plan, home).Count(p => p.RaceProps.Humanlike && p.DevelopmentalStage.Adult());
            string key = HandOverMath.WaitsFor(goingHere.Count, lodgers.Count, toStand, grown, Engine(home), AnyoneElsewhere(home));
            if (key == null)
            {
                plan.waitTold = "made";
                LongEventHandler.QueueLongEvent(() => Make(plan), "CC_HandOver_Long", false, null);
                return;
            }
            if (plan.waitTold == key) return;
            // The founding party's caravan forming at the gate is on its way: nothing to tell yet.
            if (key == "Going" && goingHere.Any(p => p.Spawned && Verse.AI.Group.LordUtility.GetLord(p)?.LordJob is LordJob_FormAndSendCaravan)) return;
            plan.waitTold = key;
            var names = key == "Going" ? Foundings.Names(goingHere) : key == "Lodgers" ? Foundings.Names(lodgers) : "";
            Tell.Message(NewsKind.Colony, Importance.Important, home, ("CC_HandOver_Wait_" + key).Translate(plan.homeName.Named("HOME"), names.Named("NAMES"),
                HandOverMath.MinGrownStay.Named("COUNT")).Resolve(), new LookTargets(goingHere.Concat(lodgers).Where(p => p.Spawned)), MessageTypeDefOf.CautionInput);
        }

        // ================= The town is made =================

        /// <summary>A free colonist of the player's anywhere but this map (another home, a caravan, pods), or the gravship in flight.</summary>
        private static bool AnyoneElsewhere(Map home)
        {
            if (Find.CurrentGravship != null) return true;
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
                if (p != null && p.MapHeld != home)
                    return true;
            return false;
        }

        /// <summary>Colonists of another home here now (a visit): they go home before the town is made, not stay with it.</summary>
        private static List<Pawn> Visitors(HandOverPlan plan, Map home) =>
            home.mapPawns.FreeColonists.Where(p => p != null && !p.Dead && !plan.going.Contains(p) && !plan.staying.Contains(p) && Homes.LastHome(p) != null && Homes.LastHome(p) != home)
                .ToList();

        /// <summary>Those of the colony who stay on the map now: its free people (held there too, as in a casket), and its prisoners and slaves.</summary>
        private static List<Pawn> StayingNow(HandOverPlan plan, Map home) =>
            home.mapPawns.AllPawns.Where(p => p != null && !p.Dead && p.RaceProps.Humanlike && !plan.going.Contains(p)
                                              && (p.IsFreeColonist && !p.IsQuestLodger() || p.IsPrisonerOfColony || p.IsSlaveOfColony)).ToList();

        /// <summary>The head now: the one chosen while they stay, else the rule's among those who stay now.</summary>
        private static Pawn HeadNow(HandOverPlan plan, Map home)
        {
            var grown = StayingNow(plan, home).Where(p => p.IsFreeColonist && p.DevelopmentalStage.Adult()).ToList();
            if (plan.head != null && grown.Contains(plan.head)) return plan.head;
            return HeadOf(home, grown);
        }

        /// <summary>
        /// The gravship takes off without an anchor from a home being handed over, the ceremony over and those who stay on
        /// the ground (Odyssey): the town is made instead of the game's abandon (Patch_HandOverGravship). False: the game's
        /// own abandon goes ahead.
        /// </summary>
        public static bool TakesOverAbandon(Map map)
        {
            var plan = At(map);
            if (plan == null || plan.stage != HandOverStage.Leaving || !(map?.Parent is Settlement)) return false;
            var founding = plan.foundingId >= 0 ? Foundings.Of(plan.foundingId) : null;
            bool toStand = plan.where == HandOverWhere.Founding && plan.going.Count == 0 && (plan.founding != null || founding != null && founding.stage == FoundingStage.Going);
            int grown = StayingNow(plan, map).Count(p => p.Spawned && p.DevelopmentalStage.Adult());
            // The engine is leaving with the ship; anyone of those who go still on the ground is left there and stays.
            if (HandOverMath.WaitsFor(0, 0, toStand, grown, false) != null) return false;
            foreach (var p in plan.going.Where(p => p != null && !p.Dead && p.MapHeld == map).ToList())
            {
                plan.going.Remove(p);
                plan.staying.Add(p);
            }
            return Make(plan);
        }

        /// <summary>
        /// The town is made, in order: the colony's record ends "handed over"; the new people (its def by the age, its name,
        /// faith, relations; then added); those who stay, the animals, the mechs and the colony's other people on the map are
        /// theirs, the prisoners and slaves set free; the head leads; the settlement is theirs, its look cleared (a
        /// gravship-landed home closed Rimfolk's way); the records, the notables, the terms, the moods, the milestone and
        /// the letter. The game closes the map at the settlement's next tick, as any town's: those who stay become world
        /// pawns of their people, and no "lost" letter comes. False when it couldn't be made (then nothing changed).
        /// </summary>
        public static bool Make(HandOverPlan plan)
        {
            var h = Homes.Instance;
            var home = Homes.MapOf(plan?.home ?? HomesMath.None);
            if (h == null || plan == null || home == null || !(home.Parent is Settlement s) || !h.handOvers.Contains(plan)) return false;
            var def = HandOverPeoples.DefFor(plan.age) ?? HandOverPeoples.DefFor(WorldAge.Industrial);
            if (def == null)
            {
                CouncilLog.ErrorOnce("hand-over: no people's def for " + plan.age, null);
                return false;
            }
            int key = plan.home;
            string colonyName = Homes.Name(home);
            Faction people = null;
            try
            {
                var stayingHumans = StayingNow(plan, home);
                var free = stayingHumans.Where(p => p.IsFreeColonist).ToList();
                var grown = free.Where(p => p.DevelopmentalStage.Adult()).ToList();
                var head = HeadNow(plan, home);
                var second = plan.second != null && grown.Contains(plan.second) && plan.second != head ? plan.second : SecondOf(home, grown, head);
                // The record of every colony: this one's, from its own founding day, ended "handed over" with the town it left.
                int founded = CalendarTracker.Instance?.Colony(home)?.FoundedTick ?? 0;
                WorldLedger.NoteColony(home, founded);
                float wealth = home.wealthWatcher?.WealthTotal ?? 0f;
                int heads = free.Count + plan.going.Count(p => p != null && !p.Dead);
                var founder = FounderOf(home, free, head);
                // The colony as it is left, kept before the place is its people's (the player's decision 7A): its map, when
                // the game makes it again, is the colony's own (Colonies/TownLayout.cs).
                var layout = TownLayouts.Capture(home, free);
                // The people.
                people = HandOverPeoples.Make(def, plan.peopleName.NullOrEmpty() ? DefaultName(plan.age, colonyName) : plan.peopleName, Faction.OfPlayer.ideos?.PrimaryIdeo);
                if (people == null) return false;
                // A home a gravship landed on stays one whatever its settlement's people: closed Rimfolk's way.
                home.wasSpawnedViaGravShipLanding = false;
                foreach (var p in stayingHumans)
                {
                    try
                    {
                        if (p.IsPrisonerOfColony)
                        {
                            // Set free there: their own people's again (one with none becomes the town's).
                            p.guest?.SetGuestStatus(null);
                            if (p.Faction == null || p.Faction == Faction.OfPlayer) p.SetFaction(people);
                            continue;
                        }
                        if (p.Faction != people) p.SetFaction(people);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("hand-over: one who stays", e);
                    }
                }
                // The animals and mechs that stay, and anyone else of the colony's on the map (a ghoul): the town's.
                foreach (var p in home.mapPawns.AllPawns.Where(p => p != null && !p.Dead && p.Faction == Faction.OfPlayer && !plan.going.Contains(p)).ToList())
                {
                    try
                    {
                        p.SetFaction(people);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("hand-over: an animal that stays", e);
                    }
                }
                if (head != null && head.Faction == people) people.leader = head;
                if (people.leader == null) people.leader = free.FirstOrDefault(p => p.Faction == people);
                // The settlement is theirs, and looks it.
                s.SetFaction(people);
                try
                {
                    AccessTools.Field(typeof(Settlement), "cachedMat")?.SetValue(s, null);
                    s.trader?.TryDestroyStock();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hand-over: the settlement's look", e);
                }
                WorldLedger.EndColony(key, ColonyEnd.HandedOver, s.ID);
                Records(plan, home, s, people, free, head, second, founder, founded, wealth, heads, colonyName, layout);
                h.handOvers.Remove(plan);
                FoundingLetters.RemoveHandOverLetters(plan);
                var linked = plan.foundingId >= 0 ? Foundings.Of(plan.foundingId) : null;
                if (linked != null) Foundings.HandedOver(linked);
                Homes.Clear();
                Find.GameEnder.CheckOrUpdateGameOver();
                LensModel.Dirty();
                Cards.Dirty();
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hand-over: making the town", e);
                // Once the people stands, never a second one for the same plan: the hand-over ends where it got to.
                if (people != null)
                {
                    h.handOvers.Remove(plan);
                    Homes.Clear();
                    Find.GameEnder.CheckOrUpdateGameOver();
                }
                return people != null;
            }
        }

        /// <summary>The colony's founder for the town's record: a founder of the colony who stays, else the head, else one who goes.</summary>
        private static Pawn FounderOf(Map home, List<Pawn> free, Pawn head)
        {
            var ep = CouncilGameComponent.Instance?.epithets;
            if (ep != null)
            {
                var f = free.Where(ep.IsFounder).OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();
                if (f != null) return f;
            }
            return head;
        }

        /// <summary>The town's records, its notables, the terms, the moods, the milestone and the letter.</summary>
        private static void Records(HandOverPlan plan, Map home, Settlement s, Faction people, List<Pawn> free, Pawn head, Pawn second, Pawn founder, int founded, float wealth,
            int heads, string colonyName, LedgerLayout layout)
        {
            int now = Now;
            // The town the colony was, with the colony's layout as it was left.
            var rec = new LedgerFormer
            {
                key = s.ID, people = people, colonyName = colonyName, age = plan.age, founded = founded, handedAt = now, terms = plan.terms, second = second,
                formers = free.ToList(), wentTo = plan.where == HandOverWhere.Home ? plan.toHome : HomesMath.None, layout = layout, seed = HandOverMath.Seed(wealth, heads),
                tile = s.Tile.tileId
            };
            WorldLedger.AddFormer(rec);
            foreach (var p in free) WorldLedger.Keep(p, KeepFor.Former, false);
            // Its record in detail: a town the colony was, its fortune seeded from the colony's wealth per head.
            var town = WorldLedger.TownFor(s.ID, s.Name, people, true);
            if (town != null)
            {
                town.kind = TownKind.Former;
                town.founded = founded;
                town.founder = founder != null ? WorldLedger.PersonOf(founder) : Ref.None;
                town.prosperity = HandOverMath.Seed(wealth, heads);
                town.movedAt = now;
                town.cause = "CC_HandOver_FortuneCause".Translate(colonyName.Named("COLONY")).Resolve();
                town.causeDir = 0;
                town.causeAt = now;
                WorldLedger.TownChanged();
            }
            // Its notables: the head, the head's second, the envoy (the most social), the trader (the best trader).
            try
            {
                NotableBook.Instance?.FormerTown(s, people, free, head, second);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hand-over: the notables", e);
            }
            // The terms: a pact for a year, renewed by their letter as any (decision 9A).
            string termsLine = "";
            if (plan.terms != HandOverTerms.Free)
            {
                // Bound to the seat, or the colony as a whole while it has no other home yet (a new colony on its way).
                var bound = Homes.Seat != home ? Homes.Seat : Homes.All.FirstOrDefault(m => m != home);
                var k = plan.terms == HandOverTerms.Defence ? PactKind.Defence : PactKind.Trade;
                ThingDef cart = null;
                int count = 0;
                if (k == PactKind.Trade && bound != null)
                {
                    var goods = Pacts.CartGoods(bound);
                    if (goods.Count > 0) (cart, count) = goods[0];
                }
                if (Pacts.Make(people, bound, k, true, cart, count) != null) termsLine = ("CC_HandOver_Terms_Made_" + plan.terms).Translate(people.Name.Named("PEOPLE")).Resolve();
            }
            else termsLine = "CC_HandOver_Terms_Made_Free".Translate(people.Name.Named("PEOPLE")).Resolve();
            Moods(plan, free);
            // The milestone, under the colony and the town.
            var actors = new List<HistoryActor> { new HistoryActor(Ref.Colony(plan.home), HistoryRole.About), new HistoryActor(Refs.Of(people), HistoryRole.For) };
            if (head != null) actors.Add(new HistoryActor(WorldLedger.PersonOf(head), HistoryRole.Did));
            History.Add(FoundingMath.KindHandedOver, actors, Ref.Town(s.ID), HistoryMath.Milestone,
                "CC_HandOver_History".Translate(colonyName.Named("HOME"), people.Name.Named("PEOPLE"), (head?.LabelShort ?? "?").Named("HEAD")).Resolve());
            // The letter, pointing at the town.
            string title = people.LeaderTitle;
            string text = "CC_HandOver_LetterText".Translate(colonyName.Named("HOME"), people.Name.Named("PEOPLE"), (head?.LabelShort ?? "?").Named("HEAD"), title.Named("TITLE"),
                Foundings.Names(free).Named("NAMES")).Resolve();
            if (plan.tokenGiven) text += " " + "CC_HandOver_LetterToken".Translate(Form(plan).word.Named("FORM")).Resolve();
            if (second != null) text += " " + "CC_HandOver_LetterSecond".Translate(second.LabelShort.Named("SECOND"), SecondTitle(plan.age).Named("TITLE")).Resolve();
            if (!termsLine.NullOrEmpty()) text += "\n\n" + termsLine;
            text += "\n\n" + "CC_HandOver_LetterAfter".Translate(people.Name.Named("PEOPLE")).Resolve();
            Tell.Letter(NewsKind.World, Importance.Important, Homes.All.FirstOrDefault(m => m != home), "CC_HandOver_LetterLabel".Translate(colonyName.Named("HOME"), people.Name.Named("PEOPLE")).Resolve(),
                text, LetterDefOf.PositiveEvent, new LookTargets(s));
        }

        /// <summary>
        /// Uprooted (a season) for those sent or kept against their wish, and parted (a season) from the closest on the
        /// other side, for those who go and those who stay alike (release 1's moods).
        /// </summary>
        private static void Moods(HandOverPlan plan, List<Pawn> stayed)
        {
            foreach (var p in plan.sentAgainst.Where(p => p != null && !p.Dead))
                CouncilGameComponent.GiveThought(p, FoundingDefOf.Rimfolk_Uprooted, null, -1f, "CC_HandOver_UprootedSent".Translate((plan.homeName ?? "?").Named("HOME")).Resolve());
            foreach (var p in plan.keptAgainst.Where(p => p != null && !p.Dead))
                CouncilGameComponent.GiveThought(p, FoundingDefOf.Rimfolk_Uprooted, null, -1f, "CC_HandOver_UprootedKept".Translate((plan.homeName ?? "?").Named("HOME")).Resolve());
            // apart: close bonds split by the hand-over, those who went and those who stayed; they live in two places now, which is the mood.
            var went = plan.going.Where(p => p != null && !p.Dead).ToList();
            void Parted(Pawn a, IEnumerable<Pawn> others)
            {
                if (a == null || !People.HasInnerLife(a)) return;
                int n = 0;
                foreach (var b in others.Where(b => b != null && b != a && Foundings.IsClose(a, b)).OrderByDescending(b => a.relations.OpinionOf(b)))
                {
                    if (n++ >= FoundingMath.MaxParted) break;
                    CouncilGameComponent.GiveThought(a, FoundingDefOf.Rimfolk_Parted, b);
                }
            }
            foreach (var a in went) Parted(a, stayed);
            foreach (var a in stayed) Parted(a, went);
        }

        // ================= The town's heads =================

        /// <summary>
        /// The people's next leader when the head dies or is lost (Patch_HandOverLeader, the game's own succession, so its
        /// letter names them): the head's second, else one of the colony's former people by standing; then a new second
        /// is chosen, former colonists first. Null: nobody of theirs is left, and the game makes its own.
        /// </summary>
        public static Pawn NextLeader(Faction f)
        {
            var rec = WorldLedger.FormerOf(f);
            if (rec == null) return null;
            var old = f.leader;
            bool Fit(Pawn p) => p != null && p != old && !p.Dead && !p.Discarded && p.Faction == f && !p.IsPrisoner && p.RaceProps.Humanlike && p.DevelopmentalStage.Adult();
            var next = Fit(rec.second) ? rec.second : null;
            var people = Members(f).Where(Fit).ToList();
            if (next == null && people.Count > 0)
            {
                var c = people.Select(p => new HeadCandidate(false, -1, Standing(p, people), rec.formers.Contains(p))).ToList();
                int i = HandOverMath.NextSecond(c, -1);
                if (i >= 0) next = people[i];
            }
            if (next == null) return null;
            // A new second, former colonists first.
            var rest = people.Where(p => p != next).ToList();
            rec.second = null;
            if (rest.Count > 0)
            {
                var c = rest.Select(p => new HeadCandidate(false, -1, Standing(p, rest), rec.formers.Contains(p))).ToList();
                int i = HandOverMath.NextSecond(c, -1);
                if (i >= 0) rec.second = rest[i];
            }
            // The new second, a notable of the town already (its envoy, its trader), is its heir from now on; the place they
            // leave is filled again from its former people first (NotableFor).
            var book = NotableBook.Instance;
            var known = rec.second != null ? book?.notables.FirstOrDefault(n => n != null && n.town == rec.key && n.pawn == rec.second && n.role != NotableRole.Head) : null;
            if (known != null) known.role = NotableRole.Heir;
            WorldLedger.FormerChanged();
            NotableBook.Changed();
            return next;
        }

        /// <summary>A people's living members the game keeps (in the world, and on any map).</summary>
        private static List<Pawn> Members(Faction f)
        {
            var list = new List<Pawn>();
            if (f == null) return list;
            foreach (var p in Find.WorldPawns.AllPawnsAlive)
                if (p != null && p.Faction == f)
                    list.Add(p);
            foreach (var m in Find.Maps)
                foreach (var p in m.mapPawns.AllPawns)
                    if (p != null && !p.Dead && p.Faction == f && !list.Contains(p))
                        list.Add(p);
            return list;
        }

        /// <summary>The town's second for the notables (the heir): the record's, while they live and are its people's.</summary>
        public static Pawn SecondFor(Settlement s)
        {
            var rec = s != null ? WorldLedger.FormerOf(s.ID) : null;
            var p = rec?.second;
            return p != null && !p.Dead && !p.Discarded && p.Faction == rec.people && p != rec.people.leader ? p : null;
        }

        /// <summary>The best of these for a notable's place: the most social for the envoy, the best trader for the trader.</summary>
        public static Pawn BestOf(List<Pawn> pawns, NotableRole role)
        {
            if (pawns == null || pawns.Count == 0) return null;
            var stat = role == NotableRole.Trader ? StatDefOf.TradePriceImprovement : StatDefOf.SocialImpact;
            var scores = pawns.Select(p =>
            {
                try
                {
                    return p.GetStatValue(stat);
                }
                catch (Exception)
                {
                    return 0f;
                }
            }).ToList();
            int i = HandOverMath.Best(scores, null);
            return i >= 0 ? pawns[i] : null;
        }

        /// <summary>
        /// A place among a town the colony was's notables left empty (World/Notables.cs, Staff): filled from its former
        /// people first, never someone a notable already: the head's second for the heir, the most social for the envoy,
        /// the best trader for the trader. Null: none, and the game's own people of theirs are made as for any town.
        /// </summary>
        public static Pawn NotableFor(Settlement s, NotableRole role, NotableBook book)
        {
            var rec = s != null ? WorldLedger.FormerOf(s.ID) : null;
            if (rec == null || !rec.Standing || role == NotableRole.Head || role == NotableRole.Acquaintance) return null;
            if (role == NotableRole.Heir)
            {
                var second = SecondFor(s);
                return second != null && !(book?.IsNotable(second) ?? false) ? second : null;
            }
            var free = rec.formers.Where(p => p != null && !p.Dead && !p.Discarded && p.Faction == rec.people && p != rec.people.leader && p != rec.second && !p.Spawned
                                              && p.DevelopmentalStage.Adult() && !(book?.IsNotable(p) ?? false)).ToList();
            return BestOf(free, role);
        }

        // ================= The day =================

        /// <summary>The region's day: a war between a town the colony was and the colony ends the bond (decision 8A), said once.</summary>
        public static void Daily(int now)
        {
            foreach (var r in WorldLedger.Formers)
            {
                if (r == null || r.bondEnded || r.people == null) continue;
                if (!HandOverMath.BondEnds(r.people.HostileTo(Faction.OfPlayer), r.end == TownEnd.Beaten)) continue;
                r.bondEnded = true;
                r.bondEndedAt = now;
                WorldLedger.FormerChanged();
                History.Add(FoundingMath.Family + ":bondEnded", new List<HistoryActor> { new HistoryActor(Refs.Of(r.people), HistoryRole.Against) }, Ref.Town(r.key), HistoryMath.Everyday,
                    "CC_HandOver_BondEndedHistory".Translate(r.people.Name.Named("PEOPLE"), (r.colonyName ?? "?").Named("COLONY")).Resolve());
                Tell.Minor(NewsKind.World, Importance.Notable, Homes.Seat, "CC_HandOver_BondEnded".Translate(r.people.Name.Named("PEOPLE"), (r.colonyName ?? "?").Named("COLONY")).Resolve(),
                    r.Town != null ? new LookTargets(r.Town) : LookTargets.Invalid);
            }
        }

        // ================= Words =================

        /// <summary>The hand-over's form by the colony's age: word and rite both the colony's (it is done among its own).</summary>
        public static AgeForm Form(HandOverPlan plan) => Ages.Form(AgeFeature.HandOver, plan?.age ?? Ages.Ours, plan?.age ?? Ages.Ours);

        /// <summary>The banner, totem, keys or codes, by the age.</summary>
        public static string TokenWord(HandOverPlan plan) => ("CC_HandOver_Token_" + (plan?.age ?? Ages.Ours)).Translate().Resolve();

        /// <summary>The head's second's title for a town of this age (the headman's second, the reeve's deputy, the deputy mayor, the deputy administrator).</summary>
        public static string SecondTitle(WorldAge age) => ("CC_HandOver_Second_" + age).Translate().Resolve();

        /// <summary>The people's name by the age's word: "Folk of Oakhold".</summary>
        public static string DefaultName(WorldAge age, string place) => HandOverMath.NameKey(age).Translate(place.Named("PLACE")).Resolve().CapitalizeFirst();

        /// <summary>A day by its date at a home (no countdown).</summary>
        public static string DateOf(Map home, int tick) =>
            GenDate.DateFullStringAt(Find.TickManager.TicksAbs + Math.Max(0, tick - Now), Find.WorldGrid.LongLatOf(home.Tile));

        /// <summary>A hand-over under way, in a line for the colony's card: where it stands, with names and dates, no countdown.</summary>
        public static string Line(HandOverPlan plan)
        {
            var home = Homes.MapOf(plan.home);
            string stage;
            switch (plan.stage)
            {
                case HandOverStage.Meeting: stage = "CC_HandOver_Stage_Meeting".Translate().Resolve(); break;
                case HandOverStage.Voted:
                    stage = (HandOverMath.Carried(plan.voteFor, plan.voteAgainst) ? "CC_HandOver_Stage_VotedFor" : "CC_HandOver_Stage_VotedAgainst")
                        .Translate(plan.voteFor.Named("FOR"), plan.voteAgainst.Named("AGAINST")).Resolve();
                    break;
                case HandOverStage.Set: stage = "CC_HandOver_Stage_Set".Translate((home != null ? DateOf(home, plan.dayAt) : "?").Named("DATE")).Resolve(); break;
                case HandOverStage.Ceremony: stage = "CC_HandOver_Stage_Ceremony".Translate().Resolve(); break;
                default:
                    var here = home != null ? plan.going.Where(p => p != null && !p.Dead && p.MapHeld == home).ToList() : new List<Pawn>();
                    stage = here.Count > 0 ? "CC_HandOver_Stage_Leaving".Translate(Foundings.Names(here).Named("NAMES")).Resolve() : "CC_HandOver_Stage_Left".Translate().Resolve();
                    break;
            }
            return "CC_HandOver_Line".Translate((plan.peopleName ?? "?").Named("PEOPLE"), stage.Named("STAGE")).Resolve();
        }
    }

    /// <summary>The colony's card lines and acts for a hand-over, and the town's card line for a town the colony was.</summary>
    [StaticConstructorOnStartup]
    public static class HandOverCards
    {
        static HandOverCards()
        {
            CardSections.HomeLines(CardSlot.Now, 5.5f, home =>
            {
                var plan = HandOvers.At(home);
                return plan == null ? null : new List<string> { HandOvers.Line(plan) };
            }, "CC_HandOver_CardHead", "colony-handover");
            CardSections.TownLines(CardSlot.Now, 4.5f, s =>
            {
                var r = s != null ? WorldLedger.FormerOf(s.ID) : null;
                if (r == null || r.people != s.Faction) return null;
                var lines = new List<string>
                {
                    "CC_HandOver_TownLine".Translate((r.colonyName ?? "?").Named("COLONY"), FactionMemory.AgoText(r.handedAt).Named("AGO"), r.formers.Count(p => p != null && !p.Dead).Named("COUNT")).Resolve()
                };
                int bond = HandOverMath.Bond(r.bondEnded, r.FormerHeads);
                lines.Add(r.bondEnded ? "CC_HandOver_TownBondEnded".Translate().Resolve() : "CC_HandOver_TownBond".Translate(bond.Named("BOND"), (r.colonyName ?? "?").Named("COLONY")).Resolve());
                return lines;
            }, "CC_HandOver_TownHead", "town-former");
        }
    }
}
