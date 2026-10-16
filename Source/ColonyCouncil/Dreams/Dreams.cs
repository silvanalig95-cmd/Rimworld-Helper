using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    public enum DreamKind
    {
        Sweet,
        Strange,
        Nightmare,
        /// <summary>A glimpse of something that will happen.</summary>
        Omen,
        /// <summary>Someone who is gone comes back for a night.</summary>
        Lost
    }

    /// <summary>Who the dream is about, besides the dreamer.</summary>
    public enum DreamAbout
    {
        None,
        Friend,
        Rival,
        Comrade,
        /// <summary>The dreamer's teacher (the dreamer is a student). {SKILL} is the skill.</summary>
        Teacher,
        /// <summary>The dreamer's student. {SKILL} is the skill.</summary>
        Student,
        Lover,
        Family,
        /// <summary>Any other colonist, mostly someone they feel strongly about.</summary>
        Colonist,
        /// <summary>A dead family member, partner or close bond, by name: {LOST}.</summary>
        Lost,
        /// <summary>Their bonded animal: {PET}.</summary>
        Pet,
        /// <summary>Their life goal: {GOAL}.</summary>
        Goal,
        /// <summary>
        /// The personal story they're in the middle of: {OTHER} is its other person (or its third). Pair it with the
        /// dreamer's tags (story:Suitors, story:Grief...) for a dream about one story in particular.
        /// </summary>
        Story
    }

    /// <summary>A dream that comes true: the incident happens a few days later.</summary>
    public class DreamOmen
    {
        /// <summary>IncidentDef defName, looked up quietly so DLC incidents are just skipped without the DLC.</summary>
        public string incident;
        public IntRange days = new IntRange(1, 3);
        /// <summary>For raids and the like: how strong, relative to what the storyteller would send now.</summary>
        public float pointsFactor = 1f;
        /// <summary>A welcome event (visitors, a trader, a newcomer) or a threat.</summary>
        public bool good = true;

        public IncidentDef Def => incident.NullOrEmpty() ? null : DefDatabase<IncidentDef>.GetNamedSilentFail(incident);
    }

    /// <summary>
    /// A dream a colonist may have on waking. Lines are first person ("I dreamt...") and go in the journal and the
    /// mood thought's tooltip. Placeholders: {PAWN_...} the dreamer, {OTHER_...} the person it's about, {PET_...}
    /// their animal, {LOST} the name of someone dead, {SKILL} a skill (theirs, or the one being taught).
    /// Lines can be tagged like flavour lines; dreams add [again] (they've had this one before) and [fever]
    /// (they were unconscious).
    /// </summary>
    public class DreamDef : Def
    {
        public DreamKind kind = DreamKind.Strange;
        public float weight = 1f;
        /// <summary>Requirements on the dreamer (traits, passions, mood, office, child). Being asleep is fine.</summary>
        public DilemmaRole dreamer;
        /// <summary>Children can have it (without a dreamer role, dreams are for adults by default).</summary>
        public bool children;
        public DreamAbout about = DreamAbout.None;
        /// <summary>Only after one of these memories (ThoughtDef names), at most withinDays old. A social memory makes its other pawn {OTHER}.</summary>
        public List<string> afterThoughts;
        public float withinDays = 2f;
        /// <summary>Only within two days of a raid or other big threat.</summary>
        public bool afterThreat;
        /// <summary>Only with one of these conditions (HediffDef names): an addiction, a missing limb, an illness...</summary>
        public List<string> hediffs;
        /// <summary>Only after being knocked out (a fever dream).</summary>
        public bool unconscious;
        public List<Season> seasons;
        /// <summary>
        /// Only while they feel one of these (the heart's FeelingDef keys: Fear, Anger, Gratitude...), at least
        /// <see cref="minFeeling"/> of full strength; stronger feelings make it likelier. Never with the heart off.
        /// </summary>
        public List<string> feelings;
        public float minFeeling = 0.3f;
        /// <summary>The feeling must be about someone, who becomes OTHER (the one they're angry at, grateful to).</summary>
        public bool feelingTarget;
        /// <summary>Only while a camp (world events) is set up on the dreamer's map.</summary>
        public bool camp;
        /// <summary>Only while a world event of one of these kinds ("sickness", "hunger", "fair") or defNames runs.</summary>
        public List<string> situations;
        public ToneLines lines = new ToneLines();
        /// <summary>The mood memory. Empty = the usual one for the kind of dream.</summary>
        public ThoughtDef thought;
        public bool noThought;
        /// <summary>Dilemma effects: A is the dreamer, B the person it's about.</summary>
        public List<DilemmaEffect> effects = new List<DilemmaEffect>();
        public TieKind changeTie = TieKind.None;
        public float change;
        public DreamOmen omen;
        /// <summary>The dream leaves them wanting something.</summary>
        public WishDef wish;
        /// <summary>Worth a message even when dream messages are off, and a line in the chronicle.</summary>
        public bool notable;

        /// <summary>Only in games where mechanoids are around (some mods remove them).</summary>
        public bool needsMechanoids;

        public bool AboutEvents => !afterThoughts.NullOrEmpty() || afterThreat || !hediffs.NullOrEmpty() || unconscious;

        /// <summary>Grows out of what they feel or what's around them now (a feeling, a camp, the region's troubles).</summary>
        public bool AboutNow => !feelings.NullOrEmpty() || camp || !situations.NullOrEmpty();

        public bool Matters => notable || omen != null || wish != null || changeTie != TieKind.None || effects.Any(e => !(e is DilemmaEffect_Thought));

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (lines == null || !lines.Any) yield return "dream has no lines";
            if (omen != null && omen.incident.NullOrEmpty()) yield return "omen without an incident";
        }
    }

    /// <summary>
    /// A dream memory that carries the dream itself, shown in its tooltip. A good dream shares the budget of Rimfolk's
    /// good moods (SmallJoys); dreams aren't scaled by reward strength or standards. A dream that gives something else
    /// instead of mood (a sweet one rests, a strange one may inspire) does so as it's gained (RewardInstead).
    /// </summary>
    public class Thought_Dream : Thought_Memory, ICouncilMood
    {
        public string dream;

        public float OwnOffset() => base.MoodOffset();

        public bool Scaled
        {
            set { }
        }

        public override float MoodOffset()
        {
            float own = OwnOffset();
            return own > 0f ? own * SmallJoys.Factor(this) : own;
        }

        public override string Description =>
            GoodTimes.FadeNote(this, SmallJoys.Note(this, dream.NullOrEmpty() ? base.Description : base.Description + "\n\n\"" + dream + "\""));

        public override bool TryMergeWithExistingMemory(out bool showBubble)
        {
            RewardInstead.Gained(this);
            return base.TryMergeWithExistingMemory(out showBubble);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dream, "dream");
        }
    }

    public class DreamRecord : IExposable
    {
        public Pawn pawn;
        public string name;
        public string text;
        public DreamKind kind;
        public int tick;
        public bool cameTrue;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref cameTrue, "cameTrue");
        }
    }

    public class PendingOmen : IExposable
    {
        public DreamDef def;
        public Pawn dreamer;
        public Map map;
        public int fireTick;
        public string text;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref dreamer, "dreamer");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref fireTick, "fireTick");
            Scribe_Values.Look(ref text, "text");
        }
    }

    /// <summary>
    /// Watches who sleeps. Someone who slept (or lay unconscious) for four hours or more may wake from a dream:
    /// usually just a mood and a line in their journal, sometimes an inspiration, a lesson, a changed feeling
    /// about someone, a wish, or an omen that comes true.
    /// </summary>
    public class DreamTracker : IExposable
    {
        public List<DreamRecord> recent = new List<DreamRecord>();
        public List<PendingOmen> omens = new List<PendingOmen>();
        private Dictionary<string, int> seen = new Dictionary<string, int>();
        public int lastOmenTick = -999999;

        [Unsaved] private Dictionary<Pawn, int> sleptHours = new Dictionary<Pawn, int>();
        [Unsaved] private Dictionary<Pawn, bool> wasOut = new Dictionary<Pawn, bool>();
        [Unsaved] private Dictionary<Pawn, int> lastDreamTick = new Dictionary<Pawn, int>();
        [Unsaved] public Dictionary<Pawn, DreamMemo> untold = new Dictionary<Pawn, DreamMemo>();

        public class DreamMemo
        {
            public DreamKind kind;
            public Pawn other;
            public int tick;
        }

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public const float ChancePerWaking = 0.3f;

        private static List<ThoughtDef> dreamThoughts;

        private static List<ThoughtDef> DreamThoughts => dreamThoughts ?? (dreamThoughts = DefDatabase<ThoughtDef>.AllDefsListForReading.Where(d => d.ThoughtClass == typeof(Thought_Dream)).ToList());

        public void Hourly(CouncilGameComponent comp)
        {
            CheckOmens(comp);
            if (!S.dreams) return;
            int now = Find.TickManager.TicksGame;
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
            {
                if (p.IsQuestLodger() || !People.HasInnerLife(p) || p.DevelopmentalStage.Baby()) continue;
                bool asleep = !RestUtility.Awake(p);
                if (asleep)
                {
                    sleptHours[p] = (sleptHours.TryGetValue(p, out int h) ? h : 0) + 1;
                    if (p.Downed && !p.health.capacities.CanBeAwake) wasOut[p] = true;
                    continue;
                }
                if (!sleptHours.TryGetValue(p, out int slept) || slept == 0) continue;
                bool fever = wasOut.TryGetValue(p, out bool o) && o;
                sleptHours[p] = 0;
                wasOut[p] = false;
                if (slept < 4) continue;
                if (lastDreamTick.TryGetValue(p, out int last) && now - last < GenDate.TicksPerHour * 18) continue;
                if (!Rand.Chance(fever ? 0.7f : Mathf.Min(0.95f, ChancePerWaking * S.dreamFrequency))) continue;
                try
                {
                    if (TryDream(comp, p, fever)) lastDreamTick[p] = now;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("dream", e);
                }
            }
            // Forget people who left or died.
            foreach (var gone in sleptHours.Keys.Where(k => k == null || k.Dead || !k.Spawned).ToList())
            {
                sleptHours.Remove(gone);
                wasOut.Remove(gone);
            }
            // Not saved, so only held for this session: the dead and those the game let go of aren't held on to either.
            if (lastDreamTick.Count > 0) lastDreamTick.RemoveAll(kv => kv.Key == null || kv.Key.Dead || kv.Key.Discarded || now - kv.Value >= GenDate.TicksPerHour * 18);
            if (untold.Count > 0) untold.RemoveAll(kv => kv.Key == null || kv.Key.Dead || kv.Key.Discarded || kv.Value == null || now - kv.Value.tick >= GenDate.TicksPerHour * 12);
        }

        // ---------------- Choosing a dream ----------------

        private class Cast
        {
            public DreamDef def;
            public Pawn other;
            public Pawn pet;
            public string lost;
            public SkillDef skill;
            public float weight;
        }

        public bool TryDream(CouncilGameComponent comp, Pawn p, bool fever, DreamDef only = null)
        {
            var options = new List<Cast>();
            foreach (var def in only != null ? new List<DreamDef> { only } : DefDatabase<DreamDef>.AllDefsListForReading)
            {
                Cast c;
                try
                {
                    c = Resolve(comp, def, p, fever);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("dream " + def.defName, e);
                    continue;
                }
                if (c != null && c.weight > 0f) options.Add(c);
            }
            if (options.Count == 0) return false;
            Have(comp, p, options.RandomElementByWeight(c => c.weight), fever);
            return true;
        }

        private Cast Resolve(CouncilGameComponent comp, DreamDef def, Pawn p, bool fever)
        {
            if (def.unconscious && !fever) return null;
            if (!Fits(def, p)) return null;
            var map = p.MapHeld;
            if (!def.seasons.NullOrEmpty() && (map == null || !def.seasons.Contains(GenLocalDate.Season(map)))) return null;
            var c = new Cast { def = def };
            float w = def.weight;

            if (!def.afterThoughts.NullOrEmpty())
            {
                int maxAge = (int)(def.withinDays * GenDate.TicksPerDay);
                var memory = p.needs.mood.thoughts.memories.Memories
                    .Where(m => m.def != null && m.age <= maxAge && def.afterThoughts.Contains(m.def.defName)).RandomElementWithFallback();
                if (memory == null) return null;
                if (memory.otherPawn != null && memory.otherPawn != p && memory.otherPawn.RaceProps.Humanlike) c.other = memory.otherPawn;
            }
            if (def.afterThreat)
            {
                int threat = Find.Maps.Where(m => m.IsPlayerHome).Select(m => m.storyState.LastThreatBigTick).DefaultIfEmpty(0).Max();
                if (threat <= 0 || Find.TickManager.TicksGame - threat > GenDate.TicksPerDay * 2) return null;
            }
            if (def.needsMechanoids && !People.MechanoidsAround) return null;
            if (!def.hediffs.NullOrEmpty() && !(p.health?.hediffSet?.hediffs.Any(h => def.hediffs.Contains(h.def.defName)) ?? false)) return null;
            if (def.camp && !LifeConditions.CampOn(map)) return null;
            if (!def.situations.NullOrEmpty() && !LifeConditions.Situation(def.situations)) return null;
            float feeling = 0f;
            if (!def.feelings.NullOrEmpty())
            {
                var felt = LifeConditions.Feels(p, def.feelings, def.minFeeling, def.feelingTarget);
                if (felt == null) return null;
                if (def.feelingTarget) c.other = felt.target;
                feeling = felt.Share;
            }
            if (!About(comp, def.about, p, c)) return null;
            if (c.skill == null) c.skill = SkillOf(p);

            if (def.omen != null)
            {
                if (!S.dreamOmens || omens.Count > 0 || Find.TickManager.TicksGame - lastOmenTick < GenDate.TicksPerDay * 8) return null;
                var inc = def.omen.Def;
                if (inc == null || map == null || !map.IsPlayerHome || !OmenCanHappen(inc, map)) return null;
            }
            if (def.wish != null && (!S.enableWishes || comp.OpenWishBy(p) != null)) w *= 0.3f;

            // Dreams about what really happened come first; mood colours the rest.
            if (def.AboutEvents) w *= 4f;
            else if (def.AboutNow) w *= 1.5f + 1.5f * feeling;
            else if (def.about != DreamAbout.None) w *= 1.5f;
            float mood = p.needs.mood.CurLevelPercentage;
            if (def.kind == DreamKind.Nightmare) w *= mood < 0.35f ? 2f : mood > 0.75f ? 0.6f : 1f;
            if (def.kind == DreamKind.Sweet) w *= mood > 0.7f ? 1.5f : mood < 0.3f ? 0.6f : 1f;
            if (def.kind == DreamKind.Nightmare && Nerves(p) < 0) w *= 1.5f;
            // Strain turns dreams to nightmares; feelings color them (Heart/).
            w *= Heart.DreamFactor(p, def.kind);
            if (seen.TryGetValue(Key(p, def), out int n) && n > 0) w *= 0.6f;
            c.weight = w;
            return c;
        }

        private static int Nerves(Pawn p)
        {
            var nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
            return nerves != null && p.story?.traits != null && p.story.traits.HasTrait(nerves) ? p.story.traits.DegreeOfTrait(nerves) : 0;
        }

        private static bool Fits(DreamDef def, Pawn p) => DilemmaRole.Fits(def.dreamer, p, def.children);

        private static bool About(CouncilGameComponent comp, DreamAbout about, Pawn p, Cast c)
        {
            var social = comp.social;
            bool Alive(Pawn x) => x != null && !x.Dead && x != p && x.RaceProps.Humanlike;
            Pawn FromTie(TieKind kind) => social.ties.Where(t => t.kind == kind && t.announced && t.Involves(p) && Alive(t.Other(p)))
                .RandomElementByWeightWithFallback(t => t.strength)?.Other(p);
            switch (about)
            {
                case DreamAbout.None:
                    return true;
                case DreamAbout.Friend:
                    c.other = FromTie(TieKind.Friends);
                    break;
                case DreamAbout.Rival:
                    c.other = FromTie(TieKind.Rivals);
                    break;
                case DreamAbout.Comrade:
                    c.other = FromTie(TieKind.Comrades);
                    break;
                case DreamAbout.Teacher:
                case DreamAbout.Student:
                {
                    var tie = social.ties.Where(t => t.kind == TieKind.Mentor && (about == DreamAbout.Teacher ? t.b == p : t.a == p) && Alive(t.Other(p))).RandomElementWithFallback();
                    c.other = tie?.Other(p);
                    c.skill = tie?.skill;
                    break;
                }
                case DreamAbout.Lover:
                    c.other = LovePartnerRelationUtility.ExistingMostLikedLovePartner(p, false);
                    if (!Alive(c.other)) c.other = null;
                    break;
                case DreamAbout.Family:
                    c.other = p.relations?.RelatedPawns.Where(x => Alive(x) && p.GetRelations(x).Any(r => r.familyByBloodRelation)).RandomElementWithFallback();
                    break;
                case DreamAbout.Colonist:
                    c.other = PawnsFinder.AllMaps_FreeColonists.Where(x => Alive(x) && !x.IsQuestLodger() && p.relations != null)
                        .RandomElementByWeightWithFallback(x => 1f + Mathf.Abs(p.relations.OpinionOf(x)));
                    break;
                case DreamAbout.Lost:
                    c.lost = LostOnes(comp, p).RandomElementWithFallback();
                    return c.lost != null;
                case DreamAbout.Goal:
                    return ColonyCouncilMod.Settings.aspirations && comp.aspirations.Active(p) != null;
                case DreamAbout.Story:
                {
                    var arc = ArcTracker.On ? ArcTracker.Instance?.Of(p) : null;
                    if (arc?.def == null) return false;
                    c.other = Alive(arc.other) ? arc.other : Alive(arc.third) ? arc.third : null;
                    break;
                }
                case DreamAbout.Pet:
                    c.pet = p.relations?.DirectRelations.Where(r => r.def == PawnRelationDefOf.Bond && r.otherPawn != null && !r.otherPawn.Dead).Select(r => r.otherPawn).RandomElementWithFallback();
                    return c.pet != null;
            }
            return c.other != null;
        }

        /// <summary>Names of the dead they loved: family, partners, close bonds, a bonded animal.</summary>
        public static List<string> LostOnes(CouncilGameComponent comp, Pawn p)
        {
            var names = new List<string>();
            if (p.relations != null)
            {
                foreach (var r in p.relations.DirectRelations)
                {
                    var o = r.otherPawn;
                    if (o == null || !o.Dead) continue;
                    if (r.def.familyByBloodRelation || r.def == PawnRelationDefOf.Spouse || r.def == PawnRelationDefOf.Lover || r.def == PawnRelationDefOf.Fiance || r.def == PawnRelationDefOf.Bond)
                        names.Add(o.LabelShort);
                }
            }
            names.AddRange(comp.social.lost.Where(l => l.survivor == p && l.kind != TieKind.Rivals).Select(l => l.name));
            return names.Distinct().ToList();
        }

        private static SkillDef SkillOf(Pawn p)
        {
            var skills = p.skills?.skills.Where(s => !s.TotallyDisabled).ToList();
            if (skills == null || skills.Count == 0) return null;
            var passions = skills.Where(s => People.Passionate(s)).ToList();
            return (passions.Count > 0 ? passions.RandomElementByWeight(s => People.PassionLevel(s)) : skills.MaxBy(s => s.Level)).def;
        }

        private static string Key(Pawn p, DreamDef d) => p.ThingID + "|" + d.defName;

        // ---------------- Having it ----------------

        private void Have(CouncilGameComponent comp, Pawn p, Cast c, bool fever)
        {
            var def = c.def;
            string key = Key(p, def);
            seen.TryGetValue(key, out int times);
            seen[key] = times + 1;

            var tags = Flavor.TagsFor(p);
            tags.Add("dream");
            if (times > 0) tags.Add("again");
            if (fever) tags.Add("fever");
            string raw = def.lines.Pick(ToneUtility.ToneFor(p), tags);
            if (raw.NullOrEmpty()) return;
            string text = raw.Formatted(p.Named("PAWN"), (c.other ?? p).Named("OTHER"), (c.pet ?? p).Named("PET"), (c.lost ?? "").Named("LOST"), (c.skill?.label ?? "").Named("SKILL"),
                (comp.aspirations.Active(p)?.Goal ?? "").Named("GOAL")).Resolve();

            bool wasInspired = p.Inspired;
            int hediffsBefore = p.health?.hediffSet?.hediffs.Count ?? 0;
            var ctx = new DilemmaContext { map = p.MapHeld, a = p, b = c.other, pet = c.pet, skill = c.skill, why = "CC_Log_Dream".Translate().ToString() };
            foreach (var e in def.effects)
            {
                try
                {
                    e.Apply(ctx);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("dream effect " + def.defName, ex);
                }
            }
            GiveThought(p, def, text, fever);
            if (c.other != null && def.changeTie != TieKind.None && def.change != 0f)
            {
                string note = "CC_TieNote_Dream".Translate(p.LabelShort).Resolve();
                if (def.change > 0f) comp.social.Bump(p, c.other, def.changeTie, def.change, note);
                else comp.social.Weaken(p, c.other, def.changeTie, -def.change, note);
            }
            bool wished = def.wish != null && comp.TryWishFromDream(p, def.wish) != null;
            bool changed = def.notable || def.omen != null || wished || (!wasInspired && p.Inspired)
                           || (p.health?.hediffSet?.hediffs.Count ?? 0) > hediffsBefore || (c.other != null && Mathf.Abs(def.change) >= 8f);
            if (def.omen?.Def != null)
            {
                omens.Add(new PendingOmen { def = def, dreamer = p, map = p.MapHeld, fireTick = Find.TickManager.TicksGame + def.omen.days.RandomInRange * GenDate.TicksPerDay + Rand.Range(0, GenDate.TicksPerDay / 2), text = text });
                lastOmenTick = Find.TickManager.TicksGame;
            }
            // Everyday dreams only sometimes make it into the journal, so they don't crowd out the days.
            if (def.Matters || def.AboutEvents || def.AboutNow || def.about != DreamAbout.None || def.kind == DreamKind.Omen || Rand.Chance(0.35f))
                comp.journals.WriteText(p, text);
            recent.Add(new DreamRecord { pawn = p, name = p.LabelShort, text = text, kind = def.kind, tick = Find.TickManager.TicksGame });
            while (recent.Count > 80) recent.RemoveAt(0);
            untold[p] = new DreamMemo { kind = def.kind, other = c.other, tick = Find.TickManager.TicksGame };

            string message = "CC_Dream_Message".Translate(p.Named("PAWN"), text.Named("DREAM")).Resolve();
            News.Post(message, p, NewsKind.People, def.omen != null ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.SilentInput,
                S.allDreamMessages || changed ? Importance.Notable : Importance.Flavor);
            if (def.notable || def.omen != null) comp.AddChronicle(new ChronicleEntry(ChronicleKind.Dream, message, p));
        }

        private static ThoughtDef DefaultThought(DreamKind kind)
        {
            switch (kind)
            {
                case DreamKind.Sweet: return CouncilDefOf.Council_Dream_Sweet;
                case DreamKind.Nightmare: return CouncilDefOf.Council_Dream_Nightmare;
                case DreamKind.Omen: return CouncilDefOf.Council_Dream_Omen;
                case DreamKind.Lost: return CouncilDefOf.Council_Dream_Lost;
                default: return CouncilDefOf.Council_Dream_Strange;
            }
        }

        /// <summary>One dream memory at a time, carrying the dream's text. Nightmares follow the stakes setting.</summary>
        private static void GiveThought(Pawn p, DreamDef def, string text, bool fever)
        {
            if (def.noThought || !S.MoodEffects) return;
            var td = def.thought ?? (fever && (def.kind == DreamKind.Strange || def.kind == DreamKind.Nightmare) ? CouncilDefOf.Council_Dream_Fever : DefaultThought(def.kind));
            var memories = p.needs?.mood?.thoughts?.memories;
            if (td == null || memories == null) return;
            bool negative = td.stages.Any(s => s != null && s.baseMoodEffect < 0f);
            if (negative && S.PenaltyScale <= 0f) return;
            foreach (var d in DreamThoughts) memories.RemoveMemoriesOfDef(d);
            if (!(ThoughtMaker.MakeThought(td) is Thought_Memory m)) return;
            if (m is Thought_Dream dream) dream.dream = text;
            if (negative) m.moodPowerFactor = S.PenaltyScale;
            memories.TryGainMemory(m, null);
        }

        // ---------------- Omens ----------------

        /// <summary>
        /// Only foretell what the game would allow right now: the incident's own rules (biome, population, refire
        /// timers, the difficulty's disabled threats) are all respected, since the storyteller fires it.
        /// </summary>
        private static bool OmenCanHappen(IncidentDef inc, Map map)
        {
            if (!DefDatabase<IncidentDef>.AllDefsListForReading.Contains(inc) || !inc.TargetAllowed(map)) return false;
            if (inc.defName == "SolarFlare" && !Avail.HasElectricity) return false;
            var parms = StorytellerUtility.DefaultParmsNow(inc.category, map);
            return inc.Worker.CanFireNow(parms);
        }

        private void CheckOmens(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var o in omens.ToList())
            {
                // The map is gone (abandoned, left behind): the omen goes with it.
                if (o.map == null || !Find.Maps.Contains(o.map))
                {
                    omens.Remove(o);
                    continue;
                }
                if (now < o.fireTick) continue;
                var inc = o.def?.omen?.Def;
                if (inc == null || now - o.fireTick > GenDate.TicksPerDay * 2)
                {
                    omens.Remove(o);
                    continue;
                }
                var parms = StorytellerUtility.DefaultParmsNow(inc.category, o.map);
                if (o.def.omen.pointsFactor != 1f) parms.points *= o.def.omen.pointsFactor;
                bool fired;
                try
                {
                    fired = inc.TargetAllowed(o.map) && inc.Worker.CanFireNow(parms) && Find.Storyteller.TryFire(new FiringIncident(inc, null, parms), false);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("omen " + o.def.defName, e);
                    fired = false;
                    o.fireTick = now - GenDate.TicksPerDay * 3;
                }
                if (!fired) continue;
                omens.Remove(o);
                CameTrue(comp, o);
            }
        }

        private void CameTrue(CouncilGameComponent comp, PendingOmen o)
        {
            using var _cause = Causes.Push("CC_Cause_DreamTrue".Translate().Resolve());
            var p = o.dreamer;
            if (p == null || p.Dead) return;
            string text = "CC_Dream_CameTrue".Translate(p.Named("PAWN"), (o.text ?? "").Named("DREAM")).Resolve();
            Notify.Message(text, p.Spawned ? new LookTargets(p) : LookTargets.Invalid, o.def.omen.good ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent, Importance.Important);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Dream, text, p));
            comp.annals.Notify_Omen(p, o.def.label);
            comp.aspirations.Notify(p, "omen");
            comp.aspirations.Notify_Event(p, "omen");
            if (S.MoodEffects) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Dream_CameTrue);
            comp.journals.Write(p, "Council_Journal_DreamCameTrue", null, o.def.label.Named("DREAM"));
            var rec = recent.LastOrDefault(r => r.pawn == p && r.text == o.text);
            if (rec != null) rec.cameTrue = true;
        }

        public void ExposeData()
        {
            // A dream remembers its dreamer's name: one the game has let go of is kept by name only (as a reload would).
            if (Scribe.mode == LoadSaveMode.Saving)
                foreach (var r in recent)
                    if (r?.pawn != null && r.pawn.Discarded)
                        r.pawn = null;
            Scribe_Collections.Look(ref recent, "recent", LookMode.Deep);
            Scribe_Collections.Look(ref omens, "omens", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) CouncilGameComponent.PruneByPawn(seen);
            Scribe_Collections.Look(ref seen, "seen", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref lastOmenTick, "lastOmenTick", -999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (recent == null) recent = new List<DreamRecord>();
                recent.RemoveAll(r => r == null || r.text == null);
                if (omens == null) omens = new List<PendingOmen>();
                omens.RemoveAll(o => o == null || o.def == null || o.dreamer == null || o.map == null);
                if (seen == null) seen = new Dictionary<string, int>();
            }
        }
    }

    /// <summary>
    /// Telling someone about last night's dream. The def name's ending says which dreams it's for (Sweet,
    /// Strange, Nightmare, Omen, Lost), or "AboutYou": telling the person the dream was about.
    /// </summary>
    public class InteractionWorker_DreamTalk : InteractionWorker
    {
        private string suffix;

        private string Suffix => suffix ??= interaction.defName.Substring(interaction.defName.LastIndexOf('_') + 1);

        private static DreamTracker.DreamMemo Memo(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.dreams || p == null) return null;
            if (!comp.dreams.untold.TryGetValue(p, out var m) || m == null) return null;
            return Find.TickManager.TicksGame - m.tick < GenDate.TicksPerHour * 12 ? m : null;
        }

        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (recipient == null || !recipient.IsColonist || !initiator.IsColonist) return 0f;
            var m = Memo(initiator);
            if (m == null) return 0f;
            if (Suffix == "AboutYou") return m.other == recipient && m.kind != DreamKind.Nightmare ? 3f : 0f;
            return Suffix == m.kind.ToString() && m.other != recipient ? 0.8f : 0f;
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            comp.dreams.untold.Remove(initiator);
            if (Suffix == "AboutYou")
                comp.social.Bump(initiator, recipient, TieKind.Friends, 2f, "CC_TieNote_ToldDream".Translate());
        }
    }
}
