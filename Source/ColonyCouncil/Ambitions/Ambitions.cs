using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>A bigger colony goal that a colonist pitches at a council meeting.</summary>
    public class AmbitionDef : Def
    {
        public List<AmbitionGoal> goals = new List<AmbitionGoal>();
        public float days = 20f;
        public float weight = 1f;
        /// <summary>Colonists passionate about these skills like to pitch this.</summary>
        public List<SkillDef> proposerSkills = new List<SkillDef>();
        public List<TraitDef> proposerTraits = new List<TraitDef>();
        /// <summary>Advisor categories that make this more likely to come up.</summary>
        public List<AdvisorCategory> relevantCategories = new List<AdvisorCategory>();
        public ToneLines pitch = new ToneLines();
        public ToneLines achieved = new ToneLines();
        public RoomTemplateDef template;

        public bool Possible(Map map) => goals.Count > 0 && goals.All(g => g.Possible(map));

        /// <summary>True when there's nothing left to do (goals counting new work never start out met).</summary>
        public bool AlreadyMet(Map map) => goals.All(g => g.Met(map, g.Baseline(map)));

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (goals == null || goals.Count == 0) yield return "ambition has no goals";
            if (pitch == null || !pitch.Any) yield return "ambition has no pitch lines";
        }
    }

    /// <summary>The ambition the colony is currently working on.</summary>
    public class AmbitionState : IExposable
    {
        public AmbitionDef def;
        public Pawn proposer;
        public Map map;
        public int startTick;
        public int deadlineTick;
        public List<float> baselines = new List<float>();
        public bool halfwaySaid;
        public bool deadlineSaid;

        public float Baseline(int i) => baselines != null && i < baselines.Count ? baselines[i] : 0f;

        public float Progress
        {
            get
            {
                if (def == null || map == null) return 0f;
                float sum = 0f;
                for (int i = 0; i < def.goals.Count; i++) sum += def.goals[i].Progress(map, Baseline(i));
                return def.goals.Count == 0 ? 0f : sum / def.goals.Count;
            }
        }

        public bool AllMet
        {
            get
            {
                if (def == null || map == null) return false;
                for (int i = 0; i < def.goals.Count; i++)
                    if (!def.goals[i].Met(map, Baseline(i))) return false;
                return true;
            }
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref proposer, "proposer");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref deadlineTick, "deadlineTick");
            Scribe_Collections.Look(ref baselines, "baselines", LookMode.Value);
            Scribe_Values.Look(ref halfwaySaid, "halfwaySaid");
            Scribe_Values.Look(ref deadlineSaid, "deadlineSaid");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && baselines == null) baselines = new List<float>();
        }
    }

    /// <summary>Schedules council meetings and tracks the active ambition.</summary>
    public class AmbitionTracker : IExposable
    {
        public AmbitionState active;
        /// <summary>A meeting that has been called and hasn't ended yet.</summary>
        public CouncilMeeting meeting;
        private int interruptions;
        public int nextMeetingTick = -1;
        public int lastMeetingTick = -999999;
        public int meetingsHeld;
        private Dictionary<string, int> lastOffered = new Dictionary<string, int>();
        private List<string> tmpK;
        private List<int> tmpV;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref active, "active");
            Scribe_Deep.Look(ref meeting, "meeting");
            Scribe_Values.Look(ref interruptions, "interruptions");
            Scribe_Values.Look(ref nextMeetingTick, "nextMeetingTick", -1);
            Scribe_Values.Look(ref lastMeetingTick, "lastMeetingTick", -999999);
            Scribe_Values.Look(ref meetingsHeld, "meetingsHeld");
            Scribe_Collections.Look(ref lastOffered, "lastOffered", LookMode.Value, LookMode.Value, ref tmpK, ref tmpV);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lastOffered == null) lastOffered = new Dictionary<string, int>();
                if (active != null && (active.def == null || active.map == null)) active = null;
                if (meeting != null && (meeting.map == null || meeting.pitches.Count == 0)) meeting = null;
            }
        }

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        public int TicksUntilMeeting => nextMeetingTick < 0 ? -1 : nextMeetingTick - Find.TickManager.TicksGame;

        public bool CanCallMeetingNow(out string reason)
        {
            reason = null;
            if (active != null)
            {
                reason = "CC_Meeting_BusyAmbition".Translate();
                return false;
            }
            if (meeting != null)
            {
                reason = "CC_Meeting_AlreadyCalled".Translate();
                return false;
            }
            int since = Find.TickManager.TicksGame - lastMeetingTick;
            if (since < GenDate.TicksPerDay * 2)
            {
                reason = "CC_Meeting_TooSoon".Translate(TextUtil.DaysString(GenDate.TicksPerDay * 2 - since));
                return false;
            }
            if (MeetingMap() == null)
            {
                reason = "CC_Meeting_NoOne".Translate();
                return false;
            }
            return true;
        }

        public void HourlyTick(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            if (nextMeetingTick < 0)
                nextMeetingTick = now + Mathf.RoundToInt(GenDate.TicksPerDay * Mathf.Min(4f, Settings.daysBetweenMeetings));

            if (active != null)
            {
                if (active.map == null || !Find.Maps.Contains(active.map))
                {
                    active = null;
                }
                else if (active.AllMet)
                {
                    Complete(comp);
                }
                else if (now > active.deadlineTick)
                {
                    Fail(comp);
                }
                else
                {
                    CheckIn(active, now);
                }
                if (active != null && now >= nextMeetingTick) nextMeetingTick = now + GenDate.TicksPerDay;
                return;
            }

            if (meeting != null)
            {
                MeetingUpkeep(comp);
                return;
            }
            if (now >= nextMeetingTick) HoldMeeting(comp, false);
        }

        /// <summary>The proposer cheers at halfway and frets when time is short and the work isn't.</summary>
        private void CheckIn(AmbitionState a, int now)
        {
            var p = a.proposer;
            if (p == null || p.Dead || !p.Spawned || p.Map != a.map) return;
            float progress = a.Progress;
            string pool = null;
            if (!a.halfwaySaid && progress >= 0.5f)
            {
                a.halfwaySaid = true;
                pool = "Council_Pool_AmbitionHalfway";
            }
            else if (!a.deadlineSaid && a.deadlineTick - now < GenDate.TicksPerDay * 3 && progress < 0.75f)
            {
                a.deadlineSaid = true;
                pool = "Council_Pool_AmbitionDeadline";
            }
            if (pool == null) return;
            string line = Flavor.FromPool(pool, p);
            if (line.NullOrEmpty()) return;
            line = line.Formatted(p.Named("PAWN"), a.def.label.Named("AMBITION")).Resolve();
            CouncilGameComponent.Speak(p, line, new Color(0.7f, 0.85f, 1f));
            Messages.Message("CC_Ambition_CheckIn".Translate(p.Named("PAWN"), a.def.label.Named("AMBITION"), progress.ToStringPercent().Named("PROGRESS")).Resolve() + " \"" + TextUtil.ShortSpeech(line, 120) + "\"",
                new LookTargets(p), MessageTypeDefOf.SilentInput, false);
        }

        public static Map MeetingMap()
        {
            Map best = null;
            int bestCount = 0;
            foreach (var m in Find.Maps)
            {
                if (!m.IsPlayerHome) continue;
                int n = m.mapPawns.FreeColonistsSpawned.Count(CouncilGameComponent.CanVoiceWishes);
                if (n > bestCount)
                {
                    best = m;
                    bestCount = n;
                }
            }
            return best;
        }

        private struct Pitch
        {
            public AmbitionDef def;
            public Pawn proposer;
        }

        public void HoldMeeting(CouncilGameComponent comp, bool calledByPlayer)
        {
            int now = Find.TickManager.TicksGame;
            nextMeetingTick = now + Mathf.RoundToInt(GenDate.TicksPerDay * Settings.daysBetweenMeetings);
            var map = MeetingMap();
            if (map == null) return;
            var speakers = map.mapPawns.FreeColonistsSpawned.Where(CouncilGameComponent.CanVoiceWishes).ToList();
            if (speakers.Count == 0) return;

            var advice = CouncilMapComponent.For(map)?.Signals ?? new List<AdvisorSignal>();
            var pool = new List<(AmbitionDef def, float w)>();
            foreach (var def in DefDatabase<AmbitionDef>.AllDefsListForReading)
            {
                bool possible;
                bool met;
                try
                {
                    possible = def.Possible(map);
                    met = possible && def.AlreadyMet(map);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("ambition " + def.defName, e);
                    continue;
                }
                if (!possible || met) continue;
                float relevance = advice.Where(s => def.relevantCategories.Contains(s.def.category)).Sum(s => s.Score);
                float w = def.weight * (1f + 3f * relevance);
                if (lastOffered.TryGetValue(def.defName, out int when) && now - when < GenDate.TicksPerDay * 30) w *= 0.25f;
                pool.Add((def, w));
            }
            if (pool.Count == 0)
            {
                if (calledByPlayer) Messages.Message("CC_Meeting_NothingToPropose".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            var pitches = new List<Pitch>();
            var usedSpeakers = new HashSet<Pawn>();
            while (pitches.Count < 3 && pool.Count > 0)
            {
                var pick = pool.RandomElementByWeight(p => p.w);
                pool.Remove(pick);
                var proposer = BestProposer(pick.def, speakers, usedSpeakers);
                if (proposer == null) continue;
                usedSpeakers.Add(proposer);
                pitches.Add(new Pitch { def = pick.def, proposer = proposer });
                lastOffered[pick.def.defName] = now;
            }
            if (pitches.Count == 0) return;

            lastMeetingTick = now;
            interruptions = 0;
            meeting = new CouncilMeeting
            {
                map = map,
                calledTick = now,
                calledByPlayer = calledByPlayer,
                chair = PickChair(speakers, comp),
                pitches = pitches.Select(p => new MeetingPitch
                {
                    def = p.def,
                    proposer = p.proposer,
                    line = (p.def.pitch.Pick(ToneUtility.ToneFor(p.proposer), Flavor.TagsFor(p.proposer)) ?? p.def.LabelCap.ToString()).Formatted(p.proposer.Named("PAWN")).Resolve()
                }).ToList()
            };
            // Too few people, or no way to gather: the council meets "in passing" and the letter comes at once.
            if (speakers.Count < 2 || CouncilMeeting.GatheringDef == null)
            {
                meeting.attendees.AddRange(speakers);
                Conclude(comp, true);
                return;
            }
            TryStartMeeting(comp);
        }

        /// <summary>Who runs the meeting: the best talker, helped by how much the others trust them.</summary>
        private static Pawn PickChair(List<Pawn> speakers, CouncilGameComponent comp)
        {
            var speaker = comp.offices.Speaker;
            if (speaker != null && speakers.Contains(speaker)) return speaker;
            return speakers.OrderByDescending(p => (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0) + comp.standings.TrustOf(p) / 20f + Rand.Value).FirstOrDefault();
        }

        private void MeetingUpkeep(CouncilGameComponent comp)
        {
            var m = meeting;
            int now = Find.TickManager.TicksGame;
            if (m.map == null || !Find.Maps.Contains(m.map))
            {
                meeting = null;
                return;
            }
            if (!m.Started)
            {
                // Waited too long for a calm evening: hold it over supper instead.
                if (now - m.calledTick > GenDate.TicksPerDay * 1.5f)
                {
                    m.attendees.AddRange(m.map.mapPawns.FreeColonistsSpawned.Where(CouncilGameComponent.CanVoiceWishes).Except(m.attendees));
                    Conclude(comp, true);
                }
                else TryStartMeeting(comp);
                return;
            }
            // Started but the gathering has vanished (lord lost): wrap up with what was said.
            bool lordAlive = m.map.lordManager.lords.Any(l => l.LordJob is LordJob_CouncilMeeting);
            if (!lordAlive && now - m.startedTick > 2500) Conclude(comp, false);
        }

        private void TryStartMeeting(CouncilGameComponent comp)
        {
            var m = meeting;
            var map = m.map;
            var gd = CouncilMeeting.GatheringDef;
            int now = Find.TickManager.TicksGame;
            int hour = GenLocalDate.HourOfDay(map);
            bool overdue = now - m.calledTick > GenDate.TicksPerDay * 0.75f;
            if (!m.calledByPlayer && !overdue && (hour < 16 || hour > 21)) return;
            if (!GatheringsUtility.AcceptableGameConditionsToStartGathering(map, gd)) return;
            if (m.chair == null || !GatheringsUtility.PawnCanStartOrContinueGathering(m.chair) || m.chair.Map != map)
            {
                m.chair = PickChair(map.mapPawns.FreeColonistsSpawned.Where(p => CouncilGameComponent.CanVoiceWishes(p) && GatheringsUtility.PawnCanStartOrContinueGathering(p)).ToList(), comp);
                if (m.chair == null) return;
            }
            if (!CouncilMeeting.FindVenue(map, m.chair, out var spot, out var venue)) return;
            m.spot = spot;
            m.venue = venue;
            m.startedTick = now;
            m.talkCount = 0;
            LordMaker.MakeNewLord(Faction.OfPlayer, new LordJob_CouncilMeeting(spot, m.chair, gd), map, new[] { m.chair });
            string text = "CC_Meeting_Gathering".Translate(m.chair.Named("PAWN"), venue.Named("VENUE"), TextUtil.JoinAnd(m.pitches.Select(p => p.def.label).ToList()).Named("TOPICS")).Resolve();
            Messages.Message(text, new LookTargets(new TargetInfo(spot, map)), MessageTypeDefOf.PositiveEvent, false);
            meetingsHeld++;
        }

        /// <summary>Called by the gathering when it ends: finished, or broken up early.</summary>
        public void EndMeeting(bool finished)
        {
            var comp = CouncilGameComponent.Instance;
            var m = meeting;
            if (comp == null || m == null) return;
            float elapsed = m.Started ? (Find.TickManager.TicksGame - m.startedTick) / (float)LordJob_CouncilMeeting.MeetingTicks : 1f;
            if (!finished && elapsed < 0.5f && interruptions < 2)
            {
                interruptions++;
                m.startedTick = -1;
                m.calledTick = Find.TickManager.TicksGame - GenDate.TicksPerDay / 2;
                Messages.Message("CC_Meeting_BrokenUp".Translate(), MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            Conclude(comp, false);
        }

        private void Conclude(CouncilGameComponent comp, bool inPassing)
        {
            var m = meeting;
            meeting = null;
            if (m == null || m.map == null) return;
            m.attendees.RemoveAll(p => p == null || p.Dead);
            if (m.attendees.Count == 0) m.attendees.AddRange(m.pitches.Select(p => p.proposer).Where(p => p != null && !p.Dead));
            m.Vote();
            foreach (var p in m.attendees)
            {
                if (p.needs?.mood != null && Rand.Chance(0.8f)) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_HeardAtMeeting);
                comp.standings.Change(p, 1f, null);
            }
            SendMeetingLetter(comp, m, inPassing);
        }

        private static Pawn BestProposer(AmbitionDef def, List<Pawn> speakers, HashSet<Pawn> used)
        {
            Pawn best = null;
            float bestScore = -1f;
            foreach (var p in speakers)
            {
                float score = Rand.Range(0f, 1f);
                if (used.Contains(p)) score -= 3f;
                foreach (var sk in def.proposerSkills)
                {
                    var rec = p.skills?.GetSkill(sk);
                    if (rec == null || rec.TotallyDisabled) continue;
                    score += (int)rec.passion * 1.5f + rec.Level * 0.1f;
                }
                foreach (var tr in def.proposerTraits)
                    if (p.story?.traits?.HasTrait(tr) ?? false) score += 2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        private void SendMeetingLetter(CouncilGameComponent comp, CouncilMeeting m, bool inPassing)
        {
            var map = m.map;
            var sb = new StringBuilder();
            string venue = m.venue ?? MeetingVenue(map);
            if (inPassing || !m.Started && m.attendees.Count <= 1)
                sb.AppendLine("CC_Meeting_Intro".Translate(venue).Resolve());
            else
                sb.AppendLine("CC_Meeting_IntroGathered".Translate(venue.Named("VENUE"), m.attendees.Count.Named("COUNT"),
                    TextUtil.JoinAnd(m.attendees.Select(p => p.LabelShort).ToList()).Named("NAMES"), (m.chair?.LabelShort ?? "?").Named("CHAIR")).Resolve());
            sb.AppendLine();
            int i = 1;
            foreach (var p in m.pitches)
            {
                sb.AppendLine("<b>" + i + ". " + p.def.LabelCap + "</b> (" + "CC_Meeting_ProposedBy".Translate(p.proposer.LabelShort) + ")");
                sb.AppendLine("\"" + p.line + "\"");
                foreach (var g in p.def.goals) sb.AppendLine("  - " + g.Describe(map, g.Baseline(map)));
                sb.AppendLine("  " + "CC_Meeting_Deadline".Translate((p.def.days * Settings.ambitionDeadlineFactor).ToString("0")));
                if (m.attendees.Count > 1)
                    sb.AppendLine("  " + "CC_Meeting_Votes".Translate(p.voters.Count, TextUtil.JoinAnd(p.voters.Select(v => v.LabelShort).ToList())));
                sb.AppendLine();
                i++;
            }
            foreach (var r in m.remarks) sb.AppendLine(r);
            if (m.remarks.Count == 0)
            {
                string heckle = Heckle(m.attendees, m.pitches);
                if (!heckle.NullOrEmpty()) sb.AppendLine(heckle);
            }
            sb.AppendLine();
            var fav = m.Favorite;
            if (m.attendees.Count > 1 && fav != null)
                sb.AppendLine(m.Tied ? "CC_Meeting_Split".Translate().Resolve() : "CC_Meeting_Leans".Translate(fav.def.label.Named("AMBITION"), fav.proposer.Named("PAWN")).Resolve());
            sb.Append("CC_Meeting_Outro".Translate());

            var letterDef = DefDatabase<LetterDef>.GetNamedSilentFail("Council_MeetingLetter");
            if (letterDef == null) return;
            var letter = (ChoiceLetter_CouncilMeeting)LetterMaker.MakeLetter("CC_Meeting_Title".Translate(), sb.ToString().TrimEnd(), letterDef, new LookTargets(m.pitches.Select(p => p.proposer)));
            letter.map = map;
            letter.options = m.pitches.Select(p => p.def).ToList();
            letter.proposers = m.pitches.Select(p => p.proposer).ToList();
            letter.voterPawns = new List<Pawn>();
            letter.voterChoice = new List<int>();
            for (int k = 0; k < m.pitches.Count; k++)
            {
                foreach (var v in m.pitches[k].voters)
                {
                    letter.voterPawns.Add(v);
                    letter.voterChoice.Add(k);
                }
            }
            Find.LetterStack.ReceiveLetter(letter);

            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Meeting,
                "CC_Chronicle_Meeting".Translate(TextUtil.JoinAnd(m.pitches.Select(p => p.def.label + " (" + p.proposer.LabelShort + ")").ToList())).Resolve(),
                null));
        }

        private static string MeetingVenue(Map map)
        {
            if (map == null) return "CC_Venue_Outside".Translate();
            if (RoomUtil.ColonyRooms(map).Any(r => r.Role == RoomUtil.DiningRoom)) return "CC_Venue_Dining".Translate();
            if (Avail.ColonyHas(map, d => d.defName == "Campfire")) return "CC_Venue_Campfire".Translate();
            return "CC_Venue_Outside".Translate();
        }

        /// <summary>Someone always has an opinion.</summary>
        private static string Heckle(List<Pawn> speakers, List<MeetingPitch> pitches)
        {
            var proposers = new HashSet<Pawn>(pitches.Select(p => p.proposer));
            var others = speakers.Where(p => !proposers.Contains(p)).ToList();
            if (others.Count == 0 || !Rand.Chance(0.75f)) return null;
            var p = others.RandomElement();
            var target = pitches.RandomElement();
            var tone = ToneUtility.ToneFor(p);
            string fromPool = Flavor.FromPool("Council_Pool_Heckle", p, null, tone);
            if (!fromPool.NullOrEmpty())
                return fromPool.Formatted(p.Named("PAWN"), target.proposer.Named("OTHER"), target.def.label.Named("AMBITION")).Resolve();
            string key = "CC_Heckle_" + tone;
            if (!key.CanTranslate()) key = "CC_Heckle_Plain";
            return key.Translate(p.Named("PAWN"), target.proposer.Named("OTHER"), target.def.label.Named("AMBITION")).Resolve();
        }

        /// <summary>
        /// The decision. Backing the colony's favorite pleases its voters; overruling a clear majority costs a
        /// little trust with them (when downsides are on). The proposer of the chosen idea is delighted either way.
        /// </summary>
        public void Choose(AmbitionDef def, Pawn proposer, Map map, ChoiceLetter_CouncilMeeting letter = null)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || def == null || map == null) return;
            if (letter != null && letter.voterPawns != null)
            {
                int chosen = letter.options.IndexOf(def);
                var counts = letter.options.Select((o, k) => letter.voterChoice.Count(c => c == k)).ToList();
                int max = counts.Count > 0 ? counts.Max() : 0;
                bool clearMajority = counts.Count(c => c == max) == 1 && max >= 2;
                int favorite = counts.IndexOf(max);
                for (int k = 0; k < letter.voterPawns.Count; k++)
                {
                    var v = letter.voterPawns[k];
                    if (v == null || v.Dead) continue;
                    if (letter.voterChoice[k] == chosen) comp.standings.Change(v, 2f, "CC_Log_VoteWon".Translate(def.label));
                    else if (clearMajority && letter.voterChoice[k] == favorite && Settings.Negatives)
                        comp.standings.Change(v, -2f, "CC_Log_Overruled".Translate(letter.options[favorite].label));
                }
            }
            if (proposer != null) comp.standings.Change(proposer, 5f, "CC_Log_IdeaChosen".Translate(def.label));
            int now = Find.TickManager.TicksGame;
            active = new AmbitionState
            {
                def = def,
                proposer = proposer,
                map = map,
                startTick = now,
                deadlineTick = now + Mathf.RoundToInt(def.days * Settings.ambitionDeadlineFactor * GenDate.TicksPerDay),
                baselines = def.goals.Select(g => g.Baseline(map)).ToList()
            };
            Messages.Message("CC_Ambition_Chosen".Translate(def.LabelCap.ToString(), proposer?.LabelShort ?? "?"), new LookTargets(proposer), MessageTypeDefOf.PositiveEvent, false);
            if (proposer != null)
            {
                CouncilGameComponent.Speak(proposer, "CC_Ambition_ChosenSpeech".Translate(), new Color(0.6f, 1f, 0.6f));
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                    if (p != proposer && p.needs?.mood != null && Rand.Chance(0.5f)) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_HeardAtMeeting);
            }
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.AmbitionChosen,
                "CC_Chronicle_AmbitionChosen".Translate(def.label, proposer?.LabelShort ?? "?").Resolve(), proposer));
        }

        public void Postpone(ChoiceLetter_CouncilMeeting letter = null)
        {
            if (letter != null && Settings.Negatives)
            {
                var comp = CouncilGameComponent.Instance;
                foreach (var p in letter.proposers)
                    if (p != null && !p.Dead) comp?.standings.Change(p, -1f, "CC_Log_NoneChosen".Translate());
            }
            nextMeetingTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * 5;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Chronicle_MeetingPostponed".Translate().Resolve()));
        }

        private void Complete(CouncilGameComponent comp)
        {
            var a = active;
            active = null;
            var map = a.map;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                CouncilGameComponent.GiveThought(p, p == a.proposer ? CouncilDefOf.Council_MyAmbitionAchieved : CouncilDefOf.Council_AmbitionAchieved);
                comp.standings.Change(p, p == a.proposer ? 12f : 5f, "CC_Log_AmbitionAchieved".Translate(a.def.label));
            }
            if (a.proposer != null && !a.proposer.Dead && Settings.MoodEffects && Rand.Chance(0.35f))
            {
                var h = a.proposer.mindState?.inspirationHandler;
                var insp = h?.GetRandomAvailableInspirationDef();
                if (insp != null && !h.Inspired) h.TryStartInspiration(insp, "CC_Ambition_Inspired".Translate(a.proposer.Named("PAWN")).Resolve());
            }
            string line = a.proposer != null ? (a.def.achieved.Pick(ToneUtility.ToneFor(a.proposer)) ?? "").Formatted(a.proposer.Named("PAWN")).Resolve() : "";
            string text = "CC_Ambition_AchievedText".Translate(a.def.label, ((Find.TickManager.TicksGame - a.startTick) / (float)GenDate.TicksPerDay).ToString("0.#")).Resolve();
            if (!line.NullOrEmpty()) text = "\"" + line + "\"\n\n" + text;
            Find.LetterStack.ReceiveLetter("CC_Ambition_AchievedTitle".Translate(a.def.LabelCap.ToString()), text, LetterDefOf.PositiveEvent, new LookTargets(a.proposer));
            if (a.proposer != null) CouncilGameComponent.Speak(a.proposer, line, new Color(0.6f, 1f, 0.6f));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.AmbitionAchieved,
                "CC_Chronicle_AmbitionAchieved".Translate(a.def.label).Resolve(), a.proposer));
            nextMeetingTick = Mathf.Max(nextMeetingTick, Find.TickManager.TicksGame + GenDate.TicksPerDay * 3);
        }

        private void Fail(CouncilGameComponent comp)
        {
            var a = active;
            active = null;
            foreach (var p in a.map.mapPawns.FreeColonistsSpawned)
            {
                comp.standings.Change(p, p == a.proposer ? -8f : -3f, "CC_Log_AmbitionFailed".Translate(a.def.label));
                if (Settings.Negatives) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_AmbitionFailed, null, Settings.PenaltyScale);
            }
            Messages.Message("CC_Ambition_Failed".Translate(a.def.LabelCap.ToString(), a.Progress.ToStringPercent()), new LookTargets(a.proposer), MessageTypeDefOf.NeutralEvent, false);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.AmbitionFailed,
                "CC_Chronicle_AmbitionFailed".Translate(a.def.label, a.Progress.ToStringPercent()).Resolve(), a.proposer));
            nextMeetingTick = Mathf.Min(nextMeetingTick, Find.TickManager.TicksGame + GenDate.TicksPerDay * 2);
        }

        public void Abandon()
        {
            if (active == null) return;
            var a = active;
            active = null;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.AmbitionAbandoned,
                "CC_Chronicle_AmbitionAbandoned".Translate(a.def.label).Resolve(), a.proposer));
            nextMeetingTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * 3;
        }

        /// <summary>A granted wish that serves the colony's current ambition pleases whoever proposed it.</summary>
        public void Notify_WishGranted(Wish w)
        {
            if (active?.def?.template == null || w == null) return;
            RoomTemplateDef wishTemplate = null;
            try
            {
                wishTemplate = w.Worker.SuggestedTemplate(w);
            }
            catch
            {
            }
            if (wishTemplate != active.def.template || active.proposer == null || active.proposer.Dead) return;
            CouncilGameComponent.Instance?.standings.Change(active.proposer, 3f, "CC_Log_AmbitionHelped".Translate(active.def.label));
            CouncilGameComponent.Speak(active.proposer, "CC_Ambition_WishHelped".Translate(active.def.label), new Color(0.6f, 1f, 0.6f));
        }
    }

    /// <summary>The meeting letter: pick one of the pitched ambitions, or none.</summary>
    public class ChoiceLetter_CouncilMeeting : ChoiceLetter
    {
        public Map map;
        public List<AmbitionDef> options = new List<AmbitionDef>();
        public List<Pawn> proposers = new List<Pawn>();
        public List<Pawn> voterPawns = new List<Pawn>();
        public List<int> voterChoice = new List<int>();

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly)
                {
                    yield return Option_Close;
                    yield break;
                }
                var tracker = CouncilGameComponent.Instance?.ambitions;
                for (int i = 0; i < options.Count; i++)
                {
                    var def = options[i];
                    var proposer = i < proposers.Count ? proposers[i] : null;
                    var opt = new DiaOption("CC_Meeting_Choose".Translate(def.LabelCap.ToString(), proposer?.LabelShort ?? "?"));
                    if (def == null || map == null || tracker == null || tracker.active != null) opt.Disable("CC_Meeting_Unavailable".Translate());
                    opt.action = () =>
                    {
                        tracker?.Choose(def, proposer, map, this);
                        Find.LetterStack.RemoveLetter(this);
                    };
                    opt.resolveTree = true;
                    yield return opt;
                }
                var none = new DiaOption("CC_Meeting_None".Translate());
                none.action = () =>
                {
                    tracker?.Postpone(this);
                    Find.LetterStack.RemoveLetter(this);
                };
                none.resolveTree = true;
                yield return none;
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "map");
            Scribe_Collections.Look(ref options, "options", LookMode.Def);
            Scribe_Collections.Look(ref proposers, "proposers", LookMode.Reference);
            Scribe_Collections.Look(ref voterPawns, "voterPawns", LookMode.Reference);
            Scribe_Collections.Look(ref voterChoice, "voterChoice", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (options == null) options = new List<AmbitionDef>();
                if (proposers == null) proposers = new List<Pawn>();
                if (voterPawns == null || voterChoice == null || voterPawns.Count != voterChoice.Count)
                {
                    voterPawns = new List<Pawn>();
                    voterChoice = new List<int>();
                }
            }
        }
    }
}
