using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// An evening of faith (Faith/, with Ideology): the council calls a gathering where one faith is spoken for, by its
    /// moral guide if it has one at home, else its most devout believer with the best Social skill, at its altar or
    /// ideogram if one is built (else round a fire or at the dining table). Believers of the faith come to hear it;
    /// colonists of other faiths choose for themselves (their openness, how sure they are of their own faith, what they
    /// think of the speaker), and those whose faith frowns on being preached to stay away and say so. It plays out on
    /// the map as a gathering (PlayOut/); when it's over, how it went (the speaker's Social skill and warmth, how many
    /// came, the altar) decides what it did: listeners of other faiths swayed through <see cref="Faiths.Sway"/>, the
    /// faith's own believers steadied, a good moment for everyone who came; a poor evening changes nothing but a line.
    /// At most one evening every five days in the colony, and ten for the same faith (FaithCouncilMath).
    /// <para>
    /// Called from the calendar (a day kept as an evening of faith), from a council question the devout put
    /// (Dilemmas/DilemmaWorkers_Faith.cs), and from anywhere else through <see cref="CanCall(Map, Ideo, out string)"/>
    /// and <see cref="Call(Map, Ideo)"/>.
    /// </para>
    /// </summary>
    public static class FaithEvenings
    {
        public const string GatheringPool = "Council_Pool_FaithEvening";
        public const int HourFrom = 17, HourTo = 23;
        public const float Hours = 2f;

        private static readonly Color SpeechColor = new Color(0.86f, 0.83f, 1f);

        private static FaithCouncilTracker Tracker => FaithCouncilTracker.Instance;

        /// <summary>Evenings of faith can be held at all: Ideology, "Faith in the colony", and answers playing out on the map.</summary>
        public static bool On => Faiths.On && PlayOuts.On;

        // ---------------- Can one be called? ----------------

        /// <summary>The colony can hold an evening of faith now, for at least one of its faiths; else why not.</summary>
        public static bool CanCall(Map map, out string why)
        {
            why = WhyNot(map, null, false);
            if (why != null) return false;
            foreach (var f in Faiths.Census())
            {
                if (f?.ideo == null) continue;
                if (WhyNot(map, f.ideo, false) == null) return true;
            }
            why = "CC_FaithEve_Why_NoFaith".Translate();
            return false;
        }

        /// <summary>This faith can hold an evening of faith now; else why not.</summary>
        public static bool CanCall(Map map, Ideo faith, out string why)
        {
            why = faith == null ? "CC_FaithEve_Why_NoFaith".Translate().ToString() : WhyNot(map, faith, false);
            return why == null;
        }

        private static string WhyNot(Map map, Ideo faith, bool calendar)
        {
            if (!Faiths.On) return "CC_FaithEve_Why_Off".Translate();
            if (!PlayOuts.On) return "CC_FaithEve_Why_PlayOut".Translate();
            var tr = Tracker;
            if (map == null || !map.IsPlayerHome || tr == null) return "CC_FaithEve_Why_NoMap".Translate();
            if (tr.Pending(null) != null) return "CC_FaithEve_Why_Pending".Translate();
            if (!calendar && tr.DaysSinceEvening < FaithCouncilMath.EveningGapDays) return "CC_FaithEve_Why_Soon".Translate();
            if (faith == null) return null;
            if (!calendar && tr.DaysSinceFaith(faith) < FaithCouncilMath.FaithEveningGapDays) return "CC_FaithEve_Why_FaithSoon".Translate(faith.name.Named("FAITH")).Resolve();
            var speaker = Speaker(map, faith);
            if (speaker == null) return "CC_FaithEve_Why_NoSpeaker".Translate(faith.name.Named("FAITH")).Resolve();
            if (!map.mapPawns.FreeColonistsSpawned.Any(p => p != speaker && Listener(p))) return "CC_FaithEve_Why_Alone".Translate();
            return null;
        }

        // ---------------- Who speaks, and where ----------------

        /// <summary>Up to speaking for a faith: one of the colony's own, grown, free, up and able to talk.</summary>
        private static bool CanSpeak(Pawn p, Map map) =>
            PlayOuts.OurPerson(p) && p.Spawned && p.Map == map && !p.DevelopmentalStage.Juvenile() && !p.IsSlave && !p.Downed
            && (p.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) ?? false);

        /// <summary>Someone who could come to hear it: one of the colony's own, not a baby.</summary>
        private static bool Listener(Pawn p) => PlayOuts.OurPerson(p) && p.Spawned && Faiths.Holds(p);

        /// <summary>The faith's moral guide, wherever they are, or null.</summary>
        public static Pawn MoralGuide(Ideo faith)
        {
            if (faith == null) return null;
            try
            {
                var moralist = PreceptDefOf.IdeoRole_Moralist;
                foreach (var r in faith.RolesListForReading)
                    if (r != null && moralist != null && r.def == moralist)
                    {
                        var p = r.ChosenPawnSingle();
                        if (p != null) return p;
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith: moral guide", e);
            }
            return null;
        }

        /// <summary>Who speaks for a faith at home: its moral guide if they're here, else its most devout believer with the best Social skill.</summary>
        public static Pawn Speaker(Map map, Ideo faith)
        {
            if (map == null || faith == null) return null;
            var guide = MoralGuide(faith);
            if (guide != null && Faiths.Of(guide) == faith && CanSpeak(guide, map)) return guide;
            var believers = map.mapPawns.FreeColonistsSpawned.Where(p => Faiths.Of(p) == faith && CanSpeak(p, map)).ToList();
            if (believers.Count == 0) return null;
            var devout = believers.Where(Faiths.Devout).ToList();
            return (devout.Count > 0 ? devout : believers)
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0)
                .ThenByDescending(Faiths.Certainty)
                .First();
        }

        /// <summary>A building that serves a faith's rites: an altar, an ideogram, a shrine (by name, so other mods' count).</summary>
        private static bool IsAltar(ThingDef d)
        {
            if (d?.building == null || d.defName == null) return false;
            string n = d.defName;
            return n.IndexOf("Altar", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Ideogram", StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("Shrine", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// The faith's altar or ideogram at home (a soft check): one built in its own style first; else, for the colony's
        /// own faith, any altar or ideogram that isn't another faith's. Null when there's none.
        /// </summary>
        public static Thing Place(Map map, Ideo faith)
        {
            if (map == null || faith == null) return null;
            try
            {
                Thing plain = null;
                foreach (var b in Avail.ColonyBuildings(map, IsAltar))
                {
                    if (b == null || !b.Spawned) continue;
                    var of = b.StyleSourcePrecept?.ideo;
                    if (of == faith) return b;
                    if (of == null && plain == null) plain = b;
                }
                return plain != null && faith == DecreeWork.ColonyFaith ? plain : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith: altar", e);
                return null;
            }
        }

        /// <summary>Any faith's altar or ideogram at home (for the council question when faiths clash), the largest faith's first.</summary>
        public static Thing AnyPlace(Map map)
        {
            foreach (var f in Faiths.Census())
            {
                var t = Place(map, f?.ideo);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>The faith by its name (a calendar day keeps the name), or null.</summary>
        public static Ideo Named(string name)
        {
            if (name.NullOrEmpty() || Find.IdeoManager == null) return null;
            return Find.IdeoManager.IdeosListForReading.FirstOrDefault(i => i != null && i.name == name);
        }

        // ---------------- Calling one ----------------

        /// <summary>Calls an evening of faith for <paramref name="faith"/> on <paramref name="map"/>. True when one was set going.</summary>
        public static bool Call(Map map, Ideo faith) => Call(map, faith, false);

        /// <summary>A day on the calendar kept as an evening of faith: its own faith (the colony's largest if it has none), whatever the gap since the last.</summary>
        public static bool FromCalendar(CalendarDay d, Map map)
        {
            if (d == null || map == null || !On) return false;
            var faith = Named(d.subject) ?? Faiths.Census().FirstOrDefault()?.ideo;
            if (faith == null || WhyNot(map, faith, true) != null) return false;
            return Call(map, faith, true);
        }

        private static bool Call(Map map, Ideo faith, bool calendar)
        {
            if (WhyNot(map, faith, calendar) != null) return false;
            var tr = Tracker;
            var plays = PlayOutTracker.Instance;
            if (tr == null || plays == null) return false;
            try
            {
                var speaker = Speaker(map, faith);
                var place = Place(map, faith);
                var come = new List<Pawn>();
                var away = new List<Pawn>();
                var declined = new List<Pawn>();
                // apart: each colonist makes up their own mind about coming; those who'll stay away say so where they are.
                foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
                {
                    if (p == speaker || !Listener(p)) continue;
                    bool same = Faiths.Of(p) == faith;
                    bool dislikes = !same && Faiths.DislikesPreaching(p);
                    float chance = FaithCouncilMath.ComeChance(same, dislikes, Persona.Get(p, PersonaAxis.Openness),
                        p.relations?.OpinionOf(speaker) ?? 0f, Faiths.Certainty(p));
                    if (dislikes) away.Add(p);
                    else if (Rand.Chance(chance)) come.Add(p);
                    else declined.Add(p);
                }
                string label = "CC_FaithEve_Label".Translate(faith.name.Named("FAITH")).Resolve();
                if (come.Count == 0)
                {
                    // Offered and nobody came: the faith can't ask again for a while (no rolling the dice until someone does).
                    tr.Held(faith, false);
                    Notify.Message("CC_FaithEve_NobodyCame".Translate(speaker.Named("SPEAKER"), faith.name.Named("FAITH")).Resolve(), speaker,
                        MessageTypeDefOf.NeutralEvent, Importance.Important);
                    SayAway(away, faith);
                    return false;
                }
                int now = Find.TickManager.TicksGame;
                var p0 = new PlayOut
                {
                    kind = PlayKind.Gathering,
                    map = map,
                    created = now,
                    label = label,
                    invited = new List<Pawn> { speaker }.Concat(come).ToList(),
                    organizer = speaker,
                    spots = place != null
                        ? new List<PlaySpot> { PlaySpot.Place, PlaySpot.Fire, PlaySpot.Dining, PlaySpot.Gather, PlaySpot.Outdoors }
                        : new List<PlaySpot> { PlaySpot.Fire, PlaySpot.Dining, PlaySpot.Gather, PlaySpot.Rec, PlaySpot.Outdoors },
                    place = place,
                    durationTicks = PlayOutMath.DurationTicks(Hours),
                    pool = GatheringPool,
                    words = true,
                    hourFrom = HourFrom,
                    hourTo = HourTo
                };
                p0.deadline = PlayOutMath.Deadline(now, GenLocalDate.HourOfDay(map), p0.hourFrom, p0.hourTo, PlayOutMath.DefaultWaitHours);
                plays.Add(p0);
                tr.plans.Add(new FaithEveningPlan { playId = p0.id, faith = faith, speaker = speaker, place = place, created = now });
                Notify.Message(Planned(p0, faith, speaker, place, come, away, declined), new LookTargets(speaker), MessageTypeDefOf.PositiveEvent, Importance.Important);
                SayAway(away, faith);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith: calling an evening", e);
                return false;
            }
        }

        private static string Names(List<Pawn> who) => TextUtil.JoinAnd(who.Select(p => p.LabelShort).ToList());

        /// <summary>The word that it's planned: when, who speaks and where, who means to come and who stays away.</summary>
        private static string Planned(PlayOut p, Ideo faith, Pawn speaker, Thing place, List<Pawn> come, List<Pawn> away, List<Pawn> declined)
        {
            var parts = new List<string> { PlayWords.Planned(p) };
            parts.Add(place != null
                ? "CC_FaithEve_SpeakerAt".Translate(speaker.Named("SPEAKER"), faith.name.Named("FAITH"), place.LabelShort.Named("PLACE")).Resolve()
                : "CC_FaithEve_Speaker".Translate(speaker.Named("SPEAKER"), faith.name.Named("FAITH")).Resolve());
            var others = come.Where(x => Faiths.Of(x) != faith).ToList();
            parts.Add(others.Count > 0 ? "CC_FaithEve_Listeners".Translate(Names(others)).Resolve() : "CC_FaithEve_NoListeners".Translate().Resolve());
            if (away.Count > 0) parts.Add("CC_FaithEve_StayAway".Translate(Names(away)).Resolve());
            if (declined.Count > 0) parts.Add("CC_FaithEve_Declined".Translate(Names(declined)).Resolve());
            return string.Join(" ", parts.Where(x => !x.NullOrEmpty()));
        }

        /// <summary>One or two of those whose faith frowns on being preached to say why they won't come (to nobody in particular).</summary>
        private static void SayAway(List<Pawn> away, Ideo faith)
        {
            // apart: said to nobody in particular, wherever they are when they hear of the evening.
            foreach (var p in away.InRandomOrder().Take(2))
            {
                string line = Line("Council_Pool_FaithEveningAway", p, null, faith.name.Named("FAITH"));
                if (line != null) Speech.Say(p, line, null, SpeechColor);
            }
        }

        private static string Line(string pool, Pawn p, string[] tags, params NamedArgument[] args)
        {
            try
            {
                string line = Flavor.FromPool(pool, p, null, null, tags ?? new string[0]);
                if (line.NullOrEmpty()) return null;
                var all = new List<NamedArgument> { p.Named("PAWN") };
                if (args != null) all.AddRange(args);
                return line.Formatted(all.ToArray()).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith line " + pool, e);
                return null;
            }
        }

        // ---------------- When it's over ----------------

        /// <summary>
        /// A gathering is over (Interplay.Notify_GatheringOver): if it was an evening of faith, it's judged and does its
        /// work, and its outcome becomes the gathering's closing words.
        /// </summary>
        public static void Notify_GatheringOver(PlayOut play, List<Pawn> attended, bool early)
        {
            var tr = Tracker;
            if (play == null || tr == null) return;
            var plan = tr.PlanFor(play.id);
            if (plan == null) return;
            tr.plans.Remove(plan);
            if (attended.NullOrEmpty() || plan.faith == null || !Faiths.On) return;
            try
            {
                Conclude(play, plan, attended, early, tr);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith: an evening over", e);
            }
        }

        private static void Conclude(PlayOut play, FaithEveningPlan plan, List<Pawn> attended, bool early, FaithCouncilTracker tr)
        {
            // together: only those the gathering itself counted as there (the play-out's lord), with the speaker among them.
            var faith = plan.faith;
            var speaker = plan.speaker;
            tr.Held(faith, true);
            bool there = speaker != null && !speaker.Dead && attended.Contains(speaker);
            var listeners = attended.Where(x => x != null && x != speaker && !x.Dead && Faiths.Holds(x)).ToList();
            var others = listeners.Where(x => Faiths.Of(x) != faith).ToList();
            var believers = listeners.Where(x => Faiths.Of(x) == faith).ToList();
            bool atAltar = plan.place != null && plan.place.Spawned && plan.place.Map == play.map;
            int social = there ? speaker.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0 : 0;
            float warmth = there ? Persona.Get(speaker, PersonaAxis.Warmth) : 0f;
            float q = FaithCouncilMath.EveningQuality(social, warmth, listeners.Count, atAltar, there, early);
            var tier = FaithCouncilMath.Tier(q);
            foreach (var l in listeners) tr.Heard(l, faith);
            if (there)
            {
                var rec = speaker.skills?.GetSkill(SkillDefOf.Social);
                if (rec != null && !rec.TotallyDisabled) rec.Learn(250f, true);
            }

            var moved = new List<Pawn>();
            if (tier != EveningTier.Poor && there)
            {
                string cause = "CC_FaithEve_Cause".Translate().Resolve();
                float sway = FaithCouncilMath.EveningSway(q);
                foreach (var o in others)
                    if (Faiths.Sway(o, speaker, sway, cause) == Faiths.SwayResult.Moved) moved.Add(o);
                float steady = FaithCouncilMath.EveningSteady(q) * Interplay.FaithFactor;
                foreach (var b in believers) Faiths.Steady(b, steady);
                var thought = tier == EveningTier.Fine ? FaithCouncilDefs.FineEvening : FaithCouncilDefs.GoodEvening;
                string why = "CC_FaithEve_ThoughtCause".Translate(faith.name.Named("FAITH"), speaker.Named("SPEAKER")).Resolve();
                foreach (var a in attended)
                    if (a != null && !a.Dead) CouncilGameComponent.GiveThought(a, thought, null, -1f, why);
                var mem = FaithCouncilDefs.EveningMemory;
                if (mem != null)
                    foreach (var o in others)
                        SharedMemories.Add(speaker, o, mem, "CC_FaithEve_MemLabel".Translate(faith.name.Named("FAITH")).Resolve(), 0.7f);
            }

            // How it went, said when the gathering ends (its closing words), and kept in the chronicle and the journals.
            var tags = new List<string> { tier.ToString().ToLowerInvariant() };
            if (atAltar) tags.Add("altar");
            if (others.Count == 0) tags.Add("believers");
            if (moved.Count > 0) tags.Add("moved");
            if (there && MoralGuide(faith) == speaker) tags.Add("guide");
            var teller = speaker ?? attended.First();
            string told = Line("Council_Pool_FaithEveningOutcome", teller, tags.ToArray(), faith.name.Named("FAITH"), listeners.Count.Named("COUNT"),
                (plan.place?.LabelShort ?? "").Named("PLACE"));
            if (told.NullOrEmpty()) told = ("CC_FaithEve_Outcome_" + tier).Translate((speaker ?? teller).Named("SPEAKER"), faith.name.Named("FAITH")).Resolve();
            if (moved.Count > 0) told += " " + "CC_FaithEve_Moved".Translate(Names(moved)).Resolve();
            if (believers.Count > 0 && tier != EveningTier.Poor) told += " " + "CC_FaithEve_Steadied".Translate(faith.name.Named("FAITH")).Resolve();
            if (early) News.Post(told, new LookTargets(attended.Where(x => x != null && x.Spawned)), NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            else play.outcome = told;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Council,
                ("CC_FaithEve_Chronicle_" + tier).Translate((speaker ?? teller).Named("SPEAKER"), faith.name.Named("FAITH"), listeners.Count.Named("COUNT")).Resolve(), speaker));
            Journals(speaker, there, others, moved, faith, tier);
        }

        /// <summary>The speaker writes about it, and so may one of those of another faith who came.</summary>
        private static void Journals(Pawn speaker, bool there, List<Pawn> others, List<Pawn> moved, Ideo faith, EveningTier tier)
        {
            var journals = CouncilGameComponent.Instance?.journals;
            if (journals == null) return;
            string t = tier.ToString().ToLowerInvariant();
            if (there) journals.WriteTagged(speaker, "Council_Journal_FaithEvening", new[] { "speaker", t }, null, faith.name.Named("FAITH"), speaker.Named("OTHER"));
            var listener = moved.FirstOrDefault() ?? others.RandomElementWithFallback();
            if (listener == null || speaker == null || !Rand.Chance(0.6f)) return;
            journals.WriteTagged(listener, "Council_Journal_FaithEvening", new[] { "listener", t, moved.Contains(listener) ? "moved" : "unmoved" }, null,
                faith.name.Named("FAITH"), speaker.Named("OTHER"));
        }
    }

    /// <summary>The defs the council's side of faith uses, looked up once and kept (none of them is required).</summary>
    public static class FaithCouncilDefs
    {
        private static readonly DefRef<ThoughtDef> good = new DefRef<ThoughtDef>("Council_Faith_GoodEvening");
        private static readonly DefRef<ThoughtDef> fine = new DefRef<ThoughtDef>("Council_Faith_FineEvening");
        private static readonly DefRef<SharedMemoryDef> evening = new DefRef<SharedMemoryDef>("Rimfolk_Mem_FaithEvening");
        private static readonly DefRef<SharedMemoryDef> learned = new DefRef<SharedMemoryDef>("Rimfolk_Mem_FaithLearned");

        public static ThoughtDef GoodEvening => good.Def;
        public static ThoughtDef FineEvening => fine.Def;
        public static SharedMemoryDef EveningMemory => evening.Def;
        public static SharedMemoryDef LearnedMemory => learned.Def;
    }
}
