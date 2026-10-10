using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>One ambition pitched at a meeting, and who backed it.</summary>
    public class MeetingPitch : IExposable
    {
        public AmbitionDef def;
        public Pawn proposer;
        public string line;
        public bool spoken;
        public List<Pawn> voters = new List<Pawn>();

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref proposer, "proposer");
            Scribe_Values.Look(ref line, "line");
            Scribe_Values.Look(ref spoken, "spoken");
            Scribe_Collections.Look(ref voters, "voters", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (voters == null) voters = new List<Pawn>();
                voters.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// A council meeting from the moment it's called until the decision letter: the pitches, the venue, who
    /// turned up and what was said. The colonists actually gather (a vanilla gathering with its own lord job),
    /// talk it over, and only when the meeting ends does the letter arrive.
    /// </summary>
    public class CouncilMeeting : IExposable
    {
        public Map map;
        public IntVec3 spot = IntVec3.Invalid;
        public string venue;
        public Pawn chair;
        public List<MeetingPitch> pitches = new List<MeetingPitch>();
        public List<Pawn> attendees = new List<Pawn>();
        public List<string> remarks = new List<string>();
        public int calledTick;
        public int startedTick = -1;
        public bool calledByPlayer;
        public int talkCount;

        public bool Started => startedTick >= 0;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref spot, "spot", IntVec3.Invalid);
            Scribe_Values.Look(ref venue, "venue");
            Scribe_References.Look(ref chair, "chair");
            Scribe_Collections.Look(ref pitches, "pitches", LookMode.Deep);
            Scribe_Collections.Look(ref attendees, "attendees", LookMode.Reference);
            Scribe_Collections.Look(ref remarks, "remarks", LookMode.Value);
            Scribe_Values.Look(ref calledTick, "calledTick");
            Scribe_Values.Look(ref startedTick, "startedTick", -1);
            Scribe_Values.Look(ref calledByPlayer, "calledByPlayer");
            Scribe_Values.Look(ref talkCount, "talkCount");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pitches == null) pitches = new List<MeetingPitch>();
                if (attendees == null) attendees = new List<Pawn>();
                if (remarks == null) remarks = new List<string>();
                pitches.RemoveAll(p => p == null || p.def == null || p.proposer == null);
                attendees.RemoveAll(p => p == null);
            }
        }

        public static GatheringDef GatheringDef => DefDatabase<GatheringDef>.GetNamedSilentFail("Council_MeetingGathering");

        // ---------------- The venue ----------------

        /// <summary>
        /// Where a colony would hold a meeting: around the dining table, else the rec room, else a gather spot
        /// (campfire, party spot), else under the sky near the beds. Returns the venue's name for the letter.
        /// </summary>
        public static bool FindVenue(Map map, Pawn chair, out IntVec3 spot, out string venue)
        {
            // First with the game's own checks for a gathering spot; if those are too picky, without them.
            return FindVenue(map, chair, true, out spot, out venue) || FindVenue(map, chair, false, out spot, out venue);
        }

        private static bool FindVenue(Map map, Pawn chair, bool strict, out IntVec3 spot, out string venue)
        {
            spot = IntVec3.Invalid;
            venue = "CC_Venue_Outside".Translate();
            var gd = GatheringDef;
            bool Ok(IntVec3 c) => c.IsValid && c.InBounds(map) && c.Standable(map) && (chair == null || chair.CanReach(c, Verse.AI.PathEndMode.Touch, Danger.None))
                                  && (!strict || gd == null || GatheringsUtility.ValidateGatheringSpot(c, gd, chair, true, true));

            foreach (var room in RoomUtil.RoomsWithRole(map, RoomUtil.DiningRoom).OrderByDescending(r => r.CellCount))
            {
                var table = room.ContainedAndAdjacentThings.FirstOrDefault(t => t.def.surfaceType == SurfaceType.Eat && t.Faction == Faction.OfPlayer);
                var c = table != null ? FreeCellNear(table.Position, map) : room.Cells.FirstOrDefault(x => x.Standable(map));
                if (Ok(c))
                {
                    spot = c;
                    venue = "CC_Venue_Dining".Translate();
                    return true;
                }
            }
            foreach (var room in RoomUtil.RoomsWithRole(map, RoomUtil.RecRoom).OrderByDescending(r => r.CellCount))
            {
                var c = room.Cells.Where(x => x.Standable(map)).OrderBy(x => x.DistanceToSquared(room.Cells.First())).Skip(room.CellCount / 2).FirstOrDefault();
                if (Ok(c))
                {
                    spot = c;
                    venue = "CC_Venue_RecRoom".Translate();
                    return true;
                }
            }
            var gather = map.gatherSpotLister.activeSpots.Select(s => s.parent).Where(t => t.Faction == Faction.OfPlayer).OrderBy(t => t.def.defName == "Campfire" ? 0 : 1).FirstOrDefault();
            if (gather != null)
            {
                var c = FreeCellNear(gather.Position, map);
                if (Ok(c))
                {
                    spot = c;
                    venue = gather.def.defName.Contains("Campfire") ? "CC_Venue_Campfire".Translate() : "CC_Venue_GatherSpot".Translate(gather.LabelShort);
                    return true;
                }
            }
            var center = TemplatePlanner.ColonyCenter(map);
            foreach (var c in GenRadial.RadialCellsAround(center, 12f, true))
            {
                if (Ok(c) && !c.Roofed(map))
                {
                    spot = c;
                    venue = "CC_Venue_Outside".Translate();
                    return true;
                }
            }
            return false;
        }

        private static IntVec3 FreeCellNear(IntVec3 c, Map map)
        {
            foreach (var x in GenRadial.RadialCellsAround(c, 3f, true))
            {
                if (x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null) return x;
            }
            return IntVec3.Invalid;
        }

        // ---------------- Talking ----------------

        /// <summary>Called every few seconds while the meeting runs: a pitch, an argument, a joke.</summary>
        public void Talk(Lord lord)
        {
            var here = lord.ownedPawns.Where(p => p.Spawned && !p.Downed && p.Map == map && GatheringsUtility.InGatheringArea(p.Position, spot, map)).ToList();
            foreach (var p in here)
                if (!attendees.Contains(p)) attendees.Add(p);
            if (here.Count == 0) return;
            talkCount++;

            // The chair opens.
            if (talkCount == 1 && chair != null && here.Contains(chair))
            {
                string open = Line("Council_Pool_MeetingOpen", chair, null, null);
                if (open != null) CouncilGameComponent.Speak(chair, open, SpeechColor);
                return;
            }

            // Then every proposer makes their case, if they've turned up.
            var next = pitches.FirstOrDefault(p => !p.spoken && here.Contains(p.proposer));
            if (next != null)
            {
                next.spoken = true;
                CouncilGameComponent.Speak(next.proposer, next.line, SpeechColor);
                return;
            }

            // And then everyone has opinions.
            if (pitches.Count == 0) return;
            var speaker = here.RandomElement();
            var target = pitches.Where(p => p.proposer != speaker).RandomElementWithFallback(pitches.RandomElement());
            int opinion = speaker.relations != null && target.proposer != null ? speaker.relations.OpinionOf(target.proposer) : 0;
            string pool;
            if (target.proposer == speaker) pool = "Council_Pool_MeetingSupport";
            else if (opinion <= -15 || (opinion < 10 && Rand.Chance(0.35f))) pool = "Council_Pool_MeetingDoubt";
            else if (Rand.Chance(0.3f)) pool = "Council_Pool_MeetingAside";
            else pool = "Council_Pool_MeetingSupport";
            string line = Line(pool, speaker, target.proposer, target.def);
            if (line == null) return;
            CouncilGameComponent.Speak(speaker, line, SpeechColor);
            if (remarks.Count < 4 && Rand.Chance(0.5f))
                remarks.Add("CC_Meeting_Remark".Translate(speaker.LabelShort, line).Resolve());
            // Arguing about it brings people closer, or not.
            if (target.proposer != null && target.proposer != speaker && target.proposer.Spawned && Rand.Chance(0.25f))
            {
                bool abrasive = speaker.story?.traits?.HasTrait(TraitDefOf.Abrasive) ?? false;
                var interaction = pool == "Council_Pool_MeetingDoubt" && abrasive ? DefDatabase<InteractionDef>.GetNamedSilentFail("Insult") : InteractionDefOf.Chitchat;
                if (interaction != null) speaker.interactions?.TryInteractWith(target.proposer, interaction);
            }
        }

        private static readonly Color SpeechColor = new Color(0.7f, 0.85f, 1f);

        private string Line(string pool, Pawn speaker, Pawn proposer, AmbitionDef def)
        {
            string line = Flavor.FromPool(pool, speaker);
            if (line.NullOrEmpty()) return null;
            return line.Formatted(speaker.Named("PAWN"), (proposer ?? speaker).Named("OTHER"), (def?.label ?? "").Named("AMBITION"),
                (venue ?? "").Named("VENUE")).Resolve();
        }

        // ---------------- Voting ----------------

        /// <summary>
        /// Everyone who came votes: proposers for their own idea, the rest by what they care about (skills,
        /// traits, the colony's troubles) and how much they like the one who proposed it.
        /// </summary>
        public void Vote()
        {
            foreach (var p in pitches) p.voters.Clear();
            var advice = CouncilMapComponent.For(map)?.Signals ?? new List<AdvisorSignal>();
            foreach (var voter in attendees.Where(p => p != null && !p.Dead))
            {
                MeetingPitch best = null;
                float bestScore = float.MinValue;
                foreach (var pitch in pitches)
                {
                    float score = Rand.Range(0f, 1.2f);
                    if (pitch.proposer == voter) score += 6f;
                    if (voter.relations != null && pitch.proposer != null) score += voter.relations.OpinionOf(pitch.proposer) / 35f;
                    foreach (var sk in pitch.def.proposerSkills)
                    {
                        var rec = voter.skills?.GetSkill(sk);
                        if (rec != null && !rec.TotallyDisabled) score += (int)rec.passion * 0.6f;
                    }
                    foreach (var tr in pitch.def.proposerTraits)
                        if (voter.story?.traits?.HasTrait(tr) ?? false) score += 1f;
                    score += advice.Where(s => pitch.def.relevantCategories.Contains(s.def.category)).Sum(s => s.Score) * 0.8f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = pitch;
                    }
                }
                best?.voters.Add(voter);
            }
        }

        public MeetingPitch Favorite => pitches.OrderByDescending(p => p.voters.Count).FirstOrDefault();

        public bool Tied
        {
            get
            {
                var sorted = pitches.OrderByDescending(p => p.voters.Count).ToList();
                return sorted.Count > 1 && sorted[0].voters.Count == sorted[1].voters.Count;
            }
        }
    }

    /// <summary>The gathering itself: colonists drift over, stand around the table and talk it out.</summary>
    public class LordJob_CouncilMeeting : LordJob_Joinable_Gathering
    {
        public const int MeetingTicks = 4500;

        public LordJob_CouncilMeeting()
        {
        }

        public LordJob_CouncilMeeting(IntVec3 spot, Pawn organizer, GatheringDef gatheringDef) : base(spot, organizer, gatheringDef)
        {
            durationTicks = MeetingTicks;
        }

        public override bool AllowStartNewGatherings => false;

        protected override LordToil CreateGatheringToil(IntVec3 spot, Pawn organizer, GatheringDef gatheringDef) => new LordToil_Party(spot, gatheringDef, 1.5E-05f);

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var meet = CreateGatheringToil(spot, organizer, gatheringDef);
            graph.AddToil(meet);
            var end = new LordToil_End();
            graph.AddToil(end);

            var calledOff = new Transition(meet, end);
            calledOff.AddTrigger(new Trigger_TickCondition(() => ShouldBeCalledOff()));
            calledOff.AddTrigger(new Trigger_PawnKilled());
            calledOff.AddTrigger(new Trigger_PawnLost(PawnLostCondition.LeftVoluntarily, organizer));
            calledOff.AddPreAction(new TransitionAction_Custom(() => CouncilGameComponent.Instance?.ambitions.EndMeeting(false)));
            graph.AddTransition(calledOff);

            timeoutTrigger = GetTimeoutTrigger();
            var done = new Transition(meet, end);
            done.AddTrigger(timeoutTrigger);
            done.AddPreAction(new TransitionAction_Custom(() => CouncilGameComponent.Instance?.ambitions.EndMeeting(true)));
            graph.AddTransition(done);
            return graph;
        }

        public override void LordJobTick()
        {
            base.LordJobTick();
            if (Find.TickManager.TicksGame % 300 != 0) return;
            try
            {
                CouncilGameComponent.Instance?.ambitions.meeting?.Talk(lord);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meeting talk", e);
            }
        }

        public override string GetReport(Pawn pawn) => "CC_Meeting_Report".Translate();
    }

    /// <summary>Required by the gathering def; council meetings are only ever started by the council.</summary>
    public class GatheringWorker_CouncilMeeting : GatheringWorker
    {
        public override bool CanExecute(Map map, Pawn organizer = null) => false;

        protected override bool TryFindGatherSpot(Pawn organizer, out IntVec3 spot) =>
            CouncilMeeting.FindVenue(organizer.Map, organizer, out spot, out _);

        protected override LordJob CreateLordJob(IntVec3 spot, Pawn organizer) => new LordJob_CouncilMeeting(spot, organizer, def);
    }
}
