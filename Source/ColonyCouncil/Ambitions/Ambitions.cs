using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

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
        public string rimTalkTopic;
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
            if (Scribe.mode == LoadSaveMode.PostLoadInit && baselines == null) baselines = new List<float>();
        }
    }

    /// <summary>Schedules council meetings and tracks the active ambition.</summary>
    public class AmbitionTracker : IExposable
    {
        public AmbitionState active;
        public int nextMeetingTick = -1;
        public int lastMeetingTick = -999999;
        public int meetingsHeld;
        private Dictionary<string, int> lastOffered = new Dictionary<string, int>();
        private List<string> tmpK;
        private List<int> tmpV;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref active, "active");
            Scribe_Values.Look(ref nextMeetingTick, "nextMeetingTick", -1);
            Scribe_Values.Look(ref lastMeetingTick, "lastMeetingTick", -999999);
            Scribe_Values.Look(ref meetingsHeld, "meetingsHeld");
            Scribe_Collections.Look(ref lastOffered, "lastOffered", LookMode.Value, LookMode.Value, ref tmpK, ref tmpV);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lastOffered == null) lastOffered = new Dictionary<string, int>();
                if (active != null && (active.def == null || active.map == null)) active = null;
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
                if (active != null && now >= nextMeetingTick) nextMeetingTick = now + GenDate.TicksPerDay;
                return;
            }

            if (now >= nextMeetingTick) HoldMeeting(comp, false);
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
            meetingsHeld++;
            SendMeetingLetter(comp, map, pitches, speakers);
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

        private void SendMeetingLetter(CouncilGameComponent comp, Map map, List<Pitch> pitches, List<Pawn> speakers)
        {
            var sb = new StringBuilder();
            string venue = MeetingVenue(map);
            sb.AppendLine("CC_Meeting_Intro".Translate(venue).Resolve());
            sb.AppendLine();
            int i = 1;
            foreach (var p in pitches)
            {
                var tone = ToneUtility.ToneFor(p.proposer);
                string line = p.def.pitch.Pick(tone) ?? "";
                line = line.Formatted(p.proposer.Named("PAWN")).Resolve();
                sb.AppendLine("<b>" + i + ". " + p.def.LabelCap + "</b> (" + "CC_Meeting_ProposedBy".Translate(p.proposer.LabelShort) + ")");
                sb.AppendLine("\"" + line + "\"");
                foreach (var g in p.def.goals) sb.AppendLine("  - " + g.Describe(map, g.Baseline(map)));
                sb.AppendLine("  " + "CC_Meeting_Deadline".Translate((p.def.days * Settings.ambitionDeadlineFactor).ToString("0")));
                sb.AppendLine();
                i++;
            }
            string heckle = Heckle(speakers, pitches);
            if (!heckle.NullOrEmpty())
            {
                sb.AppendLine(heckle);
                sb.AppendLine();
            }
            sb.Append("CC_Meeting_Outro".Translate());

            var letterDef = DefDatabase<LetterDef>.GetNamedSilentFail("Council_MeetingLetter");
            if (letterDef == null) return;
            var letter = (ChoiceLetter_CouncilMeeting)LetterMaker.MakeLetter("CC_Meeting_Title".Translate(), sb.ToString().TrimEnd(), letterDef, new LookTargets(pitches.Select(p => p.proposer)));
            letter.map = map;
            letter.options = pitches.Select(p => p.def).ToList();
            letter.proposers = pitches.Select(p => p.proposer).ToList();
            Find.LetterStack.ReceiveLetter(letter);

            foreach (var p in pitches)
                CouncilGameComponent.Speak(p.proposer, p.def.LabelCap, new Color(0.7f, 0.85f, 1f));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Meeting,
                "CC_Chronicle_Meeting".Translate(TextUtil.JoinAnd(pitches.Select(p => p.def.label + " (" + p.proposer.LabelShort + ")").ToList())).Resolve(),
                null, "the council met"));
            RimTalkBridge.OnMeeting(pitches.Select(p => (p.proposer, p.def)).ToList());
        }

        private static string MeetingVenue(Map map)
        {
            if (RoomUtil.ColonyRooms(map).Any(r => r.Role == RoomUtil.DiningRoom)) return "CC_Venue_Dining".Translate();
            if (Avail.ColonyHas(map, d => d.defName == "Campfire")) return "CC_Venue_Campfire".Translate();
            return "CC_Venue_Outside".Translate();
        }

        /// <summary>Someone always has an opinion.</summary>
        private static string Heckle(List<Pawn> speakers, List<Pitch> pitches)
        {
            var proposers = new HashSet<Pawn>(pitches.Select(p => p.proposer));
            var others = speakers.Where(p => !proposers.Contains(p)).ToList();
            if (others.Count == 0 || !Rand.Chance(0.75f)) return null;
            var p = others.RandomElement();
            var target = pitches.RandomElement();
            var tone = ToneUtility.ToneFor(p);
            string key = "CC_Heckle_" + tone;
            if (!key.CanTranslate()) key = "CC_Heckle_Plain";
            return key.Translate(p.Named("PAWN"), target.proposer.Named("OTHER"), target.def.label.Named("AMBITION")).Resolve();
        }

        public void Choose(AmbitionDef def, Pawn proposer, Map map)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || def == null || map == null) return;
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
                "CC_Chronicle_AmbitionChosen".Translate(def.label, proposer?.LabelShort ?? "?").Resolve(), proposer, "the colony chose the ambition: " + def.label));
            RimTalkBridge.OnAmbitionChosen(active);
        }

        public void Postpone()
        {
            nextMeetingTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * 5;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Chronicle_MeetingPostponed".Translate().Resolve()));
        }

        private void Complete(CouncilGameComponent comp)
        {
            var a = active;
            active = null;
            var map = a.map;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
                CouncilGameComponent.GiveThought(p, p == a.proposer ? CouncilDefOf.Council_MyAmbitionAchieved : CouncilDefOf.Council_AmbitionAchieved);
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
                "CC_Chronicle_AmbitionAchieved".Translate(a.def.label).Resolve(), a.proposer, "the colony achieved its ambition: " + a.def.label));
            RimTalkBridge.OnAmbitionAchieved(a);
            nextMeetingTick = Mathf.Max(nextMeetingTick, Find.TickManager.TicksGame + GenDate.TicksPerDay * 3);
        }

        private void Fail(CouncilGameComponent comp)
        {
            var a = active;
            active = null;
            if (Settings.stakes == StakesMode.Demanding)
            {
                foreach (var p in a.map.mapPawns.FreeColonistsSpawned) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_AmbitionFailed);
            }
            Messages.Message("CC_Ambition_Failed".Translate(a.def.LabelCap.ToString(), a.Progress.ToStringPercent()), new LookTargets(a.proposer), MessageTypeDefOf.NeutralEvent, false);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.AmbitionFailed,
                "CC_Chronicle_AmbitionFailed".Translate(a.def.label, a.Progress.ToStringPercent()).Resolve(), a.proposer, "the colony fell short of its ambition: " + a.def.label));
            nextMeetingTick = Mathf.Min(nextMeetingTick, Find.TickManager.TicksGame + GenDate.TicksPerDay * 2);
        }

        public void Abandon()
        {
            if (active == null) return;
            var a = active;
            active = null;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.AmbitionAbandoned,
                "CC_Chronicle_AmbitionAbandoned".Translate(a.def.label).Resolve(), a.proposer, "the colony gave up on: " + a.def.label));
            nextMeetingTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * 3;
        }

        public void Notify_WishGranted(Wish w)
        {
        }
    }

    /// <summary>The meeting letter: pick one of the pitched ambitions, or none.</summary>
    public class ChoiceLetter_CouncilMeeting : ChoiceLetter
    {
        public Map map;
        public List<AmbitionDef> options = new List<AmbitionDef>();
        public List<Pawn> proposers = new List<Pawn>();

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
                        tracker?.Choose(def, proposer, map);
                        Find.LetterStack.RemoveLetter(this);
                    };
                    opt.resolveTree = true;
                    yield return opt;
                }
                var none = new DiaOption("CC_Meeting_None".Translate());
                none.action = () =>
                {
                    tracker?.Postpone();
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
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (options == null) options = new List<AmbitionDef>();
                if (proposers == null) proposers = new List<Pawn>();
            }
        }
    }
}
