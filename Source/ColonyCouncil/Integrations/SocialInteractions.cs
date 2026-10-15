using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where another mod already does the same kind of moment as Rimfolk, Rimfolk's version steps back while that mod
    /// is active: its moments and social events become rarer (weight times <see cref="factor"/>). Applied once at
    /// startup. Example: Vanilla Social Interactions Expanded's venting and teaching.
    /// </summary>
    public class ModOverlapDef : Def
    {
        /// <summary>Package ids; any one active is enough.</summary>
        public List<string> mods;
        /// <summary>Optional: a static bool setting of that mod that must be on (type full name and field name).</summary>
        public string settingType;
        public string settingField;
        public List<InteractionDef> moments;
        public List<SocialEventDef> socialEvents;
        public float factor = 0.5f;

        public bool Active
        {
            get
            {
                if (mods.NullOrEmpty() || !mods.Any(People.ModActive)) return false;
                if (settingType.NullOrEmpty() || settingField.NullOrEmpty()) return true;
                try
                {
                    var type = GenTypes.GetTypeInAnyAssembly(settingType);
                    var field = type?.GetField(settingField, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    // Can't read it (the mod changed): assume the feature is on, as it is by default.
                    return !(field?.GetValue(null) is bool on) || on;
                }
                catch
                {
                    return true;
                }
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (mods.NullOrEmpty()) yield return "mod overlap without mods";
            if (factor < 0f || factor > 1f) yield return "factor must be between 0 and 1";
        }
    }

    public static class ModOverlaps
    {
        public static void Apply()
        {
            foreach (var def in DefDatabase<ModOverlapDef>.AllDefsListForReading)
            {
                if (!def.Active) continue;
                if (def.moments != null)
                    foreach (var m in def.moments)
                    {
                        var ext = m?.GetModExtension<MomentExtension>();
                        if (ext != null) ext.weight *= def.factor;
                    }
                if (def.socialEvents != null)
                    foreach (var e in def.socialEvents)
                        if (e != null) e.weight *= def.factor;
                if (Prefs.DevMode) Log.Message("[Rimfolk] " + def.defName + ": Rimfolk's own version steps back for " + string.Join(", ", def.mods) + ".");
            }
        }
    }

    /// <summary>
    /// Vanilla Social Interactions Expanded feeding Rimfolk's social web where the two mean the same thing: its
    /// lessons strengthen a mentorship (and lessons that keep happening between a master and a keen learner become
    /// one), someone who vents to a friend grows closer to them, its memories of being saved in a fight, tended back
    /// to health, friends since childhood or coming to blows move the matching bond, and its best friends are noted
    /// in the chronicle and journals. Everything is read by def name from the play log and the tales the game already
    /// keeps; with VSIE missing, none of those defs exist and nothing here runs.
    /// </summary>
    public static class SocialInteractions
    {
        private static bool looked;
        private static InteractionDef teaching, vent;
        private static TaleDef savedFromRaiders, savedFromWounds, childhoodFriends, socialFight;
        private static PawnRelationDef bestFriend;
        private static FieldInfo fIntDef, fInitiator, fRecipient, fPacks;
        private static readonly Dictionary<System.Type, (FieldInfo first, FieldInfo second)> taleFields = new Dictionary<System.Type, (FieldInfo, FieldInfo)>();

        private static void Look()
        {
            if (looked) return;
            looked = true;
            teaching = DefDatabase<InteractionDef>.GetNamedSilentFail("VSIE_Teaching");
            vent = DefDatabase<InteractionDef>.GetNamedSilentFail("VSIE_Vent");
            savedFromRaiders = DefDatabase<TaleDef>.GetNamedSilentFail("VSIE_SavedMeFromRaiders");
            savedFromWounds = DefDatabase<TaleDef>.GetNamedSilentFail("VSIE_SavedMeFromMyWounds");
            childhoodFriends = DefDatabase<TaleDef>.GetNamedSilentFail("VSIE_HasBeenMyFriendSinceChildhood");
            socialFight = DefDatabase<TaleDef>.GetNamedSilentFail("VSIE_WeHadSocialFight");
            bestFriend = DefDatabase<PawnRelationDef>.GetNamedSilentFail("VSIE_BestFriend");
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var t = typeof(PlayLogEntry_Interaction);
            fIntDef = t.GetField("intDef", flags);
            fInitiator = t.GetField("initiator", flags);
            fRecipient = t.GetField("recipient", flags);
            fPacks = t.GetField("extraSentencePacks", flags);
        }

        private static bool LogReadable => fIntDef != null && fInitiator != null && fRecipient != null;

        public static bool Active
        {
            get
            {
                Look();
                return teaching != null || vent != null || savedFromRaiders != null || savedFromWounds != null || childhoodFriends != null
                       || socialFight != null || bestFriend != null;
            }
        }

        private static bool Ours(Pawn p) => p != null && !p.Dead && p.IsColonist && People.HasInnerLife(p);

        // ---------------- The play log: lessons and venting ----------------

        /// <summary>New entries in the play log since the last look (it keeps the last 150 or so).</summary>
        public static void ScanLog(GameComponent_PeopleMods state)
        {
            if (!Active || !LogReadable || (teaching == null && vent == null)) return;
            var entries = Find.PlayLog?.AllEntries;
            if (entries == null) return;
            int max = state.lastLogId;
            // The first look on a save only takes note of where the log stands; what came before is old news.
            bool first = state.lastLogId < 0;
            var fresh = new List<PlayLogEntry_Interaction>();
            foreach (var e in entries)
            {
                if (e == null) continue;
                if (e.LogID > max) max = e.LogID;
                if (!first && e.LogID > state.lastLogId && e is PlayLogEntry_Interaction pi) fresh.Add(pi);
            }
            state.lastLogId = max;
            if (first || fresh.Count == 0 || !ColonyCouncilMod.Settings.socialWeb) return;
            foreach (var e in fresh.OrderBy(x => x.LogID))
            {
                var def = fIntDef.GetValue(e) as InteractionDef;
                if (def == null || (def != teaching && def != vent)) continue;
                var a = fInitiator.GetValue(e) as Pawn;
                var b = fRecipient.GetValue(e) as Pawn;
                if (!Ours(a) || !Ours(b) || a == b) continue;
                if (def == teaching) Lesson(state, a, b, TaughtSkill(e));
                else Vented(state, a, b);
            }
        }

        /// <summary>VSIE names the skill in the extra sentence it adds: VSIE_Teaching_Shooting and so on.</summary>
        private static SkillDef TaughtSkill(PlayLogEntry_Interaction e)
        {
            if (!(fPacks?.GetValue(e) is List<RulePackDef> packs)) return null;
            foreach (var pack in packs)
            {
                if (pack?.defName == null || !pack.defName.StartsWith("VSIE_Teaching_")) continue;
                var skill = DefDatabase<SkillDef>.GetNamedSilentFail(pack.defName.Substring("VSIE_Teaching_".Length));
                if (skill != null) return skill;
            }
            return null;
        }

        private static void Lesson(GameComponent_PeopleMods state, Pawn teacher, Pawn pupil, SkillDef skill)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (social == null || skill == null) return;
            var tie = social.Get(teacher, pupil, TieKind.Mentor);
            if (tie != null)
            {
                // A lesson between mentor and student in their own subject: the bond grows (a note once a day at most).
                if (tie.a != teacher || tie.skill != skill) return;
                tie.strength = UnityEngine.Mathf.Min(100f, tie.strength + 1.5f);
                int day = Find.TickManager.TicksGame / GenDate.TicksPerDay;
                string key = "lesson|" + teacher.thingIDNumber + "|" + pupil.thingIDNumber;
                if (!state.lastNoted.TryGetValue(key, out int last) || last != day)
                {
                    state.lastNoted[key] = day;
                    tie.Note("CC_PM_TieNote_Lesson".Translate(skill.label).Resolve());
                }
                return;
            }
            // Lessons that keep coming, from a master to a keen learner, become a mentorship: three within fifteen days.
            var tRec = teacher.skills?.GetSkill(skill);
            var pRec = pupil.skills?.GetSkill(skill);
            if (tRec == null || pRec == null || tRec.TotallyDisabled || pRec.TotallyDisabled) return;
            if (tRec.Level < 10 || pRec.Level > 8 || !People.Passionate(pRec) || pupil.DevelopmentalStage.Baby()) return;
            if (social.ties.Any(t => t.kind == TieKind.Mentor && t.b == pupil) || social.ties.Count(t => t.kind == TieKind.Mentor && t.a == teacher) >= 2) return;
            if (teacher.relations == null || pupil.relations == null || teacher.relations.OpinionOf(pupil) < 0 || pupil.relations.OpinionOf(teacher) < 0) return;
            string lk = teacher.thingIDNumber + "|" + pupil.thingIDNumber + "|" + skill.defName;
            int now = Find.TickManager.TicksGame;
            if (!state.lessons.TryGetValue(lk, out var times)) state.lessons[lk] = times = new List<int>();
            times.RemoveAll(t => now - t > GenDate.TicksPerDay * 15 || t > now);
            times.Add(now);
            if (times.Count < 3) return;
            state.lessons.Remove(lk);
            social.StartMentorship(teacher, pupil, skill);
        }

        private static void Vented(GameComponent_PeopleMods state, Pawn venter, Pawn friend)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.social == null) return;
            comp.social.Bump(venter, friend, TieKind.Friends, 3f, "CC_PM_TieNote_Vent".Translate().Resolve(), true);
            // A bad day talked through goes in the journal now and then (not every time).
            int now = Find.TickManager.TicksGame;
            string key = "vent|" + venter.thingIDNumber;
            if (state.lastNoted.TryGetValue(key, out int last) && now - last < GenDate.TicksPerDay * 5 && last <= now) return;
            state.lastNoted[key] = now;
            comp.journals.Write(venter, "Council_Journal_PM_Vented", null, friend.Named("OTHER"));
        }

        // ---------------- Tales: VSIE's memories ----------------

        /// <summary>New tales since the last look: who saved whom, friends since childhood, a fight between two of ours.</summary>
        public static void ScanTales(GameComponent_PeopleMods state)
        {
            if (!Active || (savedFromRaiders == null && savedFromWounds == null && childhoodFriends == null && socialFight == null)) return;
            var tales = Find.TaleManager?.AllTalesListForReading;
            if (tales == null) return;
            int max = state.lastTaleId;
            bool first = state.lastTaleId < 0;
            var fresh = new List<Tale>();
            foreach (var t in tales)
            {
                if (t == null) continue;
                if (t.id > max) max = t.id;
                if (!first && t.id > state.lastTaleId && t.def != null
                    && (t.def == savedFromRaiders || t.def == savedFromWounds || t.def == childhoodFriends || t.def == socialFight)) fresh.Add(t);
            }
            state.lastTaleId = max;
            if (first || fresh.Count == 0 || !ColonyCouncilMod.Settings.socialWeb) return;
            var social = CouncilGameComponent.Instance?.social;
            if (social == null) return;
            // Some memories are kept by both people (two tales): one change per pair and kind.
            var done = new HashSet<(int, int, TaleDef)>();
            foreach (var t in fresh.OrderBy(x => x.id))
            {
                var (x, y) = Pair(t);
                if (!Ours(x) || !Ours(y) || x == y) continue;
                var key = x.thingIDNumber < y.thingIDNumber ? (x.thingIDNumber, y.thingIDNumber, t.def) : (y.thingIDNumber, x.thingIDNumber, t.def);
                if (!done.Add(key)) continue;
                if (t.def == savedFromRaiders) social.Bump(y, x, TieKind.Comrades, 10f, "CC_PM_TieNote_Saved".Translate().Resolve(), true);
                else if (t.def == savedFromWounds) social.Bump(y, x, TieKind.Friends, 4f, "CC_PM_TieNote_Tended".Translate().Resolve(), true);
                else if (t.def == childhoodFriends) social.Bump(x, y, TieKind.Friends, 10f, "CC_PM_TieNote_Childhood".Translate().Resolve(), true);
                else if (t.def == socialFight) social.Bump(x, y, TieKind.Rivals, 5f, "CC_PM_TieNote_Fight".Translate().Resolve(), true);
            }
        }

        /// <summary>The tale's first and second person (VSIE's three-person tales have the same two fields).</summary>
        private static (Pawn, Pawn) Pair(Tale t)
        {
            if (t is Tale_DoublePawn d) return (d.firstPawnData?.pawn, d.secondPawnData?.pawn);
            var type = t.GetType();
            if (!taleFields.TryGetValue(type, out var f))
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                f = (type.GetField("firstPawnData", flags), type.GetField("secondPawnData", flags));
                taleFields[type] = f;
            }
            var first = f.first?.GetValue(t) as TaleData_Pawn;
            var second = f.second?.GetValue(t) as TaleData_Pawn;
            return (first?.pawn, second?.pawn);
        }

        // ---------------- Best friends ----------------

        /// <summary>
        /// Once a day: VSIE's best friends (mutual opinion of 80 or more) are Rimfolk's closest friends. A new pair is
        /// noted once, in the chronicle and both journals; their friendship gets a lift.
        /// </summary>
        public static void BestFriends(GameComponent_PeopleMods state)
        {
            if (!Active || bestFriend == null) return;
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            var now = new HashSet<string>();
            var seen = new HashSet<int>();
            var found = new List<(Pawn, Pawn)>();
            foreach (var p in PawnsFinder.AllMaps_FreeColonists)
            {
                if (!Ours(p) || p.relations == null) continue;
                seen.Add(p.thingIDNumber);
                var other = p.relations.GetFirstDirectRelationPawn(bestFriend);
                if (!Ours(other) || p.thingIDNumber > other.thingIDNumber) continue;
                string key = p.thingIDNumber + "|" + other.thingIDNumber;
                if (!now.Add(key)) continue;
                if (!state.bestFriends.Contains(key)) found.Add((p, other));
            }
            bool first = !state.bestFriendsSeeded;
            state.bestFriendsSeeded = true;
            // Pairs that fell out can be noted again if they find their way back. Only a pair both seen at home without
            // the bond has fallen out: one of them away on a caravan or at an outpost hasn't.
            state.bestFriends.RemoveAll(k => !now.Contains(k) && k.Split('|').All(id => int.TryParse(id, out int n) && seen.Contains(n)));
            foreach (var (a, b) in found)
            {
                state.bestFriends.Add(a.thingIDNumber + "|" + b.thingIDNumber);
                // Best friends from before Rimfolk started keeping track are taken in quietly.
                if (first || !ColonyCouncilMod.Settings.socialWeb) continue;
                comp.social.Bump(a, b, TieKind.Friends, 10f, "CC_PM_TieNote_BestFriends".Translate().Resolve(), true);
                string text = "CC_PM_BestFriends".Translate(a.Named("A"), b.Named("B")).Resolve();
                Notify.Message(text, new LookTargets(a, b), MessageTypeDefOf.PositiveEvent, Importance.Notable);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, text, a));
                comp.journals.Write(a, "Council_Journal_PM_BestFriend", null, b.Named("OTHER"));
                comp.journals.Write(b, "Council_Journal_PM_BestFriend", null, a.Named("OTHER"));
            }
        }
    }

    /// <summary>
    /// Rimfolk's bookkeeping for other mods' people (one per game): how far it has read the play log and the tales,
    /// recent lessons, and best friends already noted. Also forgets the last game's cached tags.
    /// </summary>
    public class GameComponent_PeopleMods : GameComponent
    {
        public int lastLogId = -1;
        public int lastTaleId = -1;
        public List<string> bestFriends = new List<string>();
        public bool bestFriendsSeeded;
        /// <summary>Recent lessons per teacher, pupil and skill (not saved: after a load, the count starts over).</summary>
        [Unsaved] public Dictionary<string, List<int>> lessons = new Dictionary<string, List<int>>();
        [Unsaved] public Dictionary<string, int> lastNoted = new Dictionary<string, int>();

        public GameComponent_PeopleMods(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            PeopleTags.Reset();
            ModPeople.Reset();
        }

        public override void GameComponentTick()
        {
            int t = Find.TickManager.TicksGame;
            // Often enough that the play log (which keeps only the last 150 or so entries) never runs past us.
            if (Sched.Every(t, Sched.PeopleLogPeriod, Sched.PeopleLog)) Safe("social interactions", () =>
            {
                SocialInteractions.ScanLog(this);
                SocialInteractions.ScanTales(this);
            });
            if (Sched.Every(t, Sched.DayTicks, Sched.DailyBestFriends)) Safe("best friends", () => SocialInteractions.BestFriends(this));
        }

        private static void Safe(string what, System.Action a)
        {
            try
            {
                a();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce(what, e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastLogId, "lastLogId", -1);
            Scribe_Values.Look(ref lastTaleId, "lastTaleId", -1);
            // Pairs ("idA|idB") with someone the game has forgotten can never be seen together again; the rest are capped.
            if (Scribe.mode == LoadSaveMode.Saving && bestFriends != null && bestFriends.Count > 0)
            {
                var known = CouncilGameComponent.KnownPawnNumbers();
                bestFriends.RemoveAll(k =>
                {
                    var ids = k?.Split('|');
                    if (ids == null || ids.Length != 2) return true;
                    foreach (var id in ids)
                        if (!int.TryParse(id, out int n) || !known.Contains(n))
                            return true;
                    return false;
                });
                Caps.TrimOldest(bestFriends, Caps.BestFriendPairs);
            }
            Scribe_Collections.Look(ref bestFriends, "bestFriends", LookMode.Value);
            Scribe_Values.Look(ref bestFriendsSeeded, "bestFriendsSeeded");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && bestFriends == null) bestFriends = new List<string>();
        }
    }
}
