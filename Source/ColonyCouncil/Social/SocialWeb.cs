using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    public enum TieKind
    {
        Friends,
        Rivals,
        Comrades,
        Mentor,
        /// <summary>Only as a requirement in defs: no tie needed.</summary>
        None
    }

    /// <summary>
    /// A lasting bond between two colonists, with a strength (0-100) and a short history of what shaped it.
    /// Friends and rivals grow out of how they feel about each other; comrades out of fighting side by side;
    /// a mentor (a) teaches a student (b) a skill. Everything that happens between them moves the strength.
    /// </summary>
    public class SocialTie : IExposable
    {
        public Pawn a;
        public Pawn b;
        public TieKind kind;
        public float strength;
        public int formedTick;
        public SkillDef skill;
        /// <summary>Strong enough to have been announced: a real bond rather than a budding one.</summary>
        public bool announced;
        public List<string> history = new List<string>();

        public bool Involves(Pawn p) => p == a || p == b;

        public Pawn Other(Pawn p) => p == a ? b : a;

        public bool Between(Pawn x, Pawn y) => (a == x && b == y) || (a == y && b == x);

        public void Note(string text)
        {
            if (text.NullOrEmpty()) return;
            history.Add("CC_DayN".Translate(Find.TickManager.TicksGame / GenDate.TicksPerDay + 1) + ": " + text);
            if (history.Count > 6) history.RemoveAt(0);
        }

        public string Label => ("CC_Tie_" + kind).Translate();

        public void ExposeData()
        {
            Scribe_References.Look(ref a, "a");
            Scribe_References.Look(ref b, "b");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref strength, "strength");
            Scribe_Values.Look(ref formedTick, "formedTick");
            Scribe_Defs.Look(ref skill, "skill");
            Scribe_Values.Look(ref announced, "announced");
            Scribe_Collections.Look(ref history, "history", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && history == null) history = new List<string>();
        }
    }

    /// <summary>Someone a colonist was bonded to who has died, remembered by name (for dreams and memories).</summary>
    public class LostBond : IExposable
    {
        public Pawn survivor;
        public string name;
        public TieKind kind;
        public int tick;

        public void ExposeData()
        {
            Scribe_References.Look(ref survivor, "survivor");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    public class Clique
    {
        public string name;
        public List<Pawn> members = new List<Pawn>();
    }

    public class SocialWeb : IExposable
    {
        public List<SocialTie> ties = new List<SocialTie>();
        public List<LostBond> lost = new List<LostBond>();
        [Unsaved] public List<Clique> cliques = new List<Clique>();
        [Unsaved] private bool cliquesBuilt;
        /// <summary>The fight going on now: who has fought in it so far, and who it's against.</summary>
        private List<Pawn> fighters = new List<Pawn>();
        public Faction battleFaction;
        [Unsaved] public Map battleMap;
        public bool battleActive;
        /// <summary>The fight's tally, for the colony's account of its victory: enemies fallen, who brought them down, our dead.</summary>
        private int battleKills;
        private int battleCountStart = -1;
        private List<string> battleFallen = new List<string>();
        [Unsaved] private Dictionary<Pawn, int> killsBy = new Dictionary<Pawn, int>();

        /// <summary>An enemy fell in a fight at home (Harmony hook).</summary>
        public void Notify_EnemyFell(Pawn killer)
        {
            StartCount();
            battleKills++;
            if (killer != null && killer.IsColonist && killer.RaceProps.Humanlike)
                killsBy[killer] = (killsBy.TryGetValue(killer, out int n) ? n : 0) + 1;
        }

        /// <summary>
        /// One of ours died at home during a fight, or in the quiet hours after it (someone downed in the fight who bled
        /// out later still fell in it). Harmony hook, after the death.
        /// </summary>
        public void Notify_ColonistFell(Pawn p, Map map)
        {
            if (p == null || map == null || !map.IsPlayerHome) return;
            if (!battleActive && !GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)) return;
            StartCount();
            if (battleFallen.Count < 20) battleFallen.Add(p.LabelShort);
        }

        /// <summary>Counts from the first blood of a fight; anything older than that fight is forgotten.</summary>
        private void StartCount()
        {
            int now = Find.TickManager.TicksGame;
            if (battleActive || (battleCountStart >= 0 && now - battleCountStart < GenDate.TicksPerHour * 3)) return;
            ResetCount();
            battleCountStart = now;
        }

        private void ResetCount()
        {
            battleKills = 0;
            battleCountStart = -1;
            battleKidnapped = Kidnapped();
            battleFallen.Clear();
            killsBy.Clear();
        }
        private int quietHours;
        /// <summary>Colonists held by other peoples when the fight began: if that went up, the fight wasn't won.</summary>
        private int battleKidnapped;
        /// <summary>When the colony last celebrated a victory (clean wins within a few days of it are only chronicled).</summary>
        public int lastVictoryTick = -999999;
        /// <summary>Hours the end of a fight was held back while one of ours lay bleeding (or the enemy lingered).</summary>
        [Unsaved] private int heldHours;

        private static int Kidnapped() =>
            Find.FactionManager?.AllFactionsListForReading.Sum(f => f.kidnapped?.KidnappedPawnsListForReading.Count ?? 0) ?? 0;

        /// <summary>One of ours lies bleeding: whether they make it is part of the fight's story.</summary>
        private static bool Bleeding() =>
            Find.Maps.Where(m => m.IsPlayerHome).Any(m => m.mapPawns.FreeColonistsSpawned.Any(p => p.Downed && !p.Dead && p.health.hediffSet.BleedRateTotal > 0.01f));

        /// <summary>The enemy is still about, though not attacking: fleeing slowly, or insects guarding their hive.</summary>
        private static bool EnemyLingers(Faction foe) =>
            foe != null && Find.Maps.Where(m => m.IsPlayerHome).Any(m =>
                m.mapPawns.AllPawnsSpawned.Any(x => x.Faction == foe && !x.Dead && !x.Downed && !x.IsPrisoner && x.HostileTo(Faction.OfPlayer)));

        /// <summary>A real threat came this time (a raid, a pack, an infestation...), not a scuffle with guests or a hunt gone wrong.</summary>
        private bool WasThreat(int start) =>
            start >= 0 && Find.Maps.Any(m => m.IsPlayerHome && m.storyState.LastThreatBigTick >= start - GenDate.TicksPerDay);

        /// <summary>Existing friendships and feuds are taken in quietly the first time the web runs on a save.</summary>
        private bool seeded;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        // ---------------- Queries ----------------

        // Ties by pair, for the opinion thoughts that ask about every pair of colonists. Rebuilt at most once a tick,
        // and whenever a tie is added or dropped.
        [Unsaved] private Dictionary<(int, int, TieKind), SocialTie> index;
        [Unsaved] private int indexTick = -1;
        [Unsaved] private int indexCount = -1;

        public SocialTie Get(Pawn x, Pawn y, TieKind kind)
        {
            if (x == null || y == null) return null;
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (index == null || tick != indexTick || ties.Count != indexCount)
            {
                index = index ?? new Dictionary<(int, int, TieKind), SocialTie>();
                index.Clear();
                foreach (var t in ties)
                {
                    if (t?.a == null || t.b == null) continue;
                    var k = Key(t.a, t.b, t.kind);
                    if (!index.ContainsKey(k)) index[k] = t;
                }
                indexTick = tick;
                indexCount = ties.Count;
            }
            return index.TryGetValue(Key(x, y, kind), out var found) ? found : null;
        }

        private static (int, int, TieKind) Key(Pawn x, Pawn y, TieKind kind) =>
            x.thingIDNumber < y.thingIDNumber ? (x.thingIDNumber, y.thingIDNumber, kind) : (y.thingIDNumber, x.thingIDNumber, kind);

        public IEnumerable<SocialTie> Of(Pawn p) => ties.Where(t => t.Involves(p));

        public float Strength(Pawn x, Pawn y, TieKind kind) => Get(x, y, kind)?.strength ?? 0f;

        public Clique CliqueOf(Pawn p)
        {
            if (!S.socialWeb || p == null) return null;
            if (!cliquesBuilt) RebuildCliques();
            return cliques.FirstOrDefault(c => c.members.Contains(p));
        }

        public bool SameClique(Pawn x, Pawn y)
        {
            var c = CliqueOf(x);
            return c != null && c.members.Contains(y);
        }

        /// <summary>How warmly x regards y because of their ties: positive for friends and comrades, negative for rivals.</summary>
        public float Warmth(Pawn x, Pawn y)
        {
            if (!S.socialWeb || x == null || y == null || x == y) return 0f;
            float w = 0f;
            foreach (var t in ties)
            {
                if (!t.Between(x, y)) continue;
                switch (t.kind)
                {
                    case TieKind.Friends: w += t.strength; break;
                    case TieKind.Comrades: w += t.strength * 0.6f; break;
                    case TieKind.Mentor: w += 30f + t.strength * 0.3f; break;
                    case TieKind.Rivals: w -= t.strength; break;
                }
            }
            if (SameClique(x, y)) w += 20f;
            return w;
        }

        /// <summary>One line naming someone's bonds, for tooltips: "Friends with Ann and Bo. Rivals with Cy."</summary>
        public string Describe(Pawn p)
        {
            if (!S.socialWeb || p == null) return null;
            var parts = new List<string>();
            foreach (var kind in new[] { TieKind.Friends, TieKind.Rivals, TieKind.Comrades })
            {
                var names = ties.Where(t => t.kind == kind && t.announced && t.Involves(p) && t.Other(p) != null && !t.Other(p).Dead && t.Other(p).IsColonist)
                    .OrderByDescending(t => t.strength).Select(t => t.Other(p).LabelShort).ToList();
                if (names.Count > 0) parts.Add(("CC_Social_Describe_" + kind).Translate(TextUtil.JoinAnd(names)).Resolve());
            }
            foreach (var t in ties.Where(t => t.kind == TieKind.Mentor && t.Involves(p) && t.skill != null && t.Other(p) != null && t.Other(p).IsColonist))
                parts.Add((t.a == p ? "CC_Social_Describe_Teaching" : "CC_Social_Describe_Learning").Translate(t.Other(p).LabelShort, t.skill.label).Resolve());
            var circle = CliqueOf(p);
            if (circle != null) parts.Add("CC_Social_Describe_Circle".Translate(circle.name).Resolve());
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        private static bool Eligible(Pawn p) => People.HasInnerLife(p) && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby();

        // ---------------- Changes ----------------

        /// <summary>Something happened between them: strengthens (or creates) a tie of this kind.</summary>
        /// <summary>Something happened between them: strengthens (or creates) a tie of this kind. Quiet: no announcement.</summary>
        public SocialTie Bump(Pawn x, Pawn y, TieKind kind, float amount, string note = null, bool quiet = false)
        {
            if (!S.socialWeb || !Eligible(x) || !Eligible(y) || x == y) return null;
            var t = Get(x, y, kind);
            if (t == null)
            {
                // Mentorships only start in TryStartMentorship, where the skill is known.
                if (amount <= 0f || kind == TieKind.Mentor) return null;
                t = new SocialTie { a = x, b = y, kind = kind, strength = 0f, formedTick = Find.TickManager.TicksGame };
                ties.Add(t);
            }
            t.strength = Mathf.Clamp(t.strength + amount, 0f, 100f);
            t.Note(note);
            if (!t.announced && t.strength >= 20f)
            {
                if (quiet) t.announced = true;
                else Announce(t);
            }
            // Friends who keep fighting stop being friends; rivals who make peace stop being rivals.
            if (kind == TieKind.Rivals && amount > 0f) Weaken(x, y, TieKind.Friends, amount * 0.7f, null);
            if (kind == TieKind.Friends && amount > 0f) Weaken(x, y, TieKind.Rivals, amount * 0.7f, null);
            return t;
        }

        public void Weaken(Pawn x, Pawn y, TieKind kind, float amount, string note)
        {
            var t = Get(x, y, kind);
            if (t == null) return;
            t.strength -= amount;
            t.Note(note);
            if (t.strength <= 0f) End(t, false);
        }

        private void End(SocialTie t, bool graduated, bool quiet = false)
        {
            ties.Remove(t);
            if (!t.announced || t.a == null || t.b == null || t.a.Dead || t.b.Dead) return;
            // Comradeship fades without ceremony; so do bonds with people who left.
            if (!graduated && (t.kind == TieKind.Comrades || !t.a.IsColonist || !t.b.IsColonist)) quiet = true;
            var aspirations = CouncilGameComponent.Instance?.aspirations;
            if (graduated) aspirations?.Notify(t.a, "graduated");
            else if (t.kind == TieKind.Rivals && t.a.IsColonist && t.b.IsColonist)
            {
                aspirations?.Notify(t.a, "peace");
                aspirations?.Notify(t.b, "peace");
            }
            if (quiet) return;
            string key = graduated ? "CC_Tie_Graduated" : "CC_Tie_Ended_" + t.kind;
            if (!key.CanTranslate()) return;
            var comp = CouncilGameComponent.Instance;
            Notify.Message(key.Translate(t.a.Named("A"), t.b.Named("B"), (t.skill?.label ?? "").Named("SKILL")).Resolve(), new LookTargets(t.a, t.b), MessageTypeDefOf.NeutralEvent, graduated ? Importance.Notable : Importance.Flavor);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, key.Translate(t.a.Named("A"), t.b.Named("B"), (t.skill?.label ?? "").Named("SKILL")).Resolve(), t.a));
            string prefix = graduated ? "Council_Journal_TieGraduated_" : "Council_Journal_TieEnded_";
            comp?.journals.Write(t.a, JournalPool(prefix, t, t.a), null, t.b.Named("OTHER"), (t.skill?.label ?? "").Named("SKILL"));
            comp?.journals.Write(t.b, JournalPool(prefix, t, t.b), null, t.a.Named("OTHER"), (t.skill?.label ?? "").Named("SKILL"));
        }

        private void Announce(SocialTie t)
        {
            t.announced = true;
            var comp = CouncilGameComponent.Instance;
            string key = "CC_Tie_Formed_" + t.kind;
            var args = new[] { t.a.Named("A"), t.b.Named("B"), (t.skill?.label ?? "").Named("SKILL") };
            Notify.Message(key.Translate(args).Resolve(), new LookTargets(t.a, t.b), t.kind == TieKind.Rivals ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent, Importance.Notable);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, key.Translate(args).Resolve(), t.a));
            comp?.annals.Notify_Tie(t);
            comp?.journals.Write(t.a, JournalPool("Council_Journal_TieFormed_", t, t.a), null, t.b.Named("OTHER"), (t.skill?.label ?? "").Named("SKILL"));
            comp?.journals.Write(t.b, JournalPool("Council_Journal_TieFormed_", t, t.b), null, t.a.Named("OTHER"), (t.skill?.label ?? "").Named("SKILL"));
        }

        /// <summary>Journal pool for one side of a tie: the student of a mentorship writes from the "Student" pool.</summary>
        private static string JournalPool(string prefix, SocialTie t, Pawn writer) =>
            prefix + (t.kind == TieKind.Mentor && writer == t.b ? "Student" : t.kind.ToString());

        // ---------------- Daily evolution ----------------

        public void Daily(CouncilGameComponent comp)
        {
            // Someone died: the ones who were close (or at war with them) write about it, once per pair.
            if (S.socialWeb)
            {
                var written = new HashSet<(Pawn, Pawn)>();
                foreach (var t in ties.Where(t => t.a != null && t.b != null && t.a.Dead != t.b.Dead && t.announced).OrderBy(t => t.kind == TieKind.Rivals).ToList())
                {
                    var dead = t.a.Dead ? t.a : t.b;
                    var alive = t.Other(dead);
                    if (!Eligible(alive) || !written.Add((alive, dead))) continue;
                    comp.journals.Write(alive, t.kind == TieKind.Rivals ? "Council_Journal_RivalLost" : "Council_Journal_TieLost", null, dead.Named("OTHER"));
                    lost.Add(new LostBond { survivor = alive, name = dead.LabelShort, kind = t.kind, tick = Find.TickManager.TicksGame });
                }
                lost.RemoveAll(l => l.survivor == null || l.survivor.Dead);
                while (lost.Count > 40) lost.RemoveAt(0);
            }
            ties.RemoveAll(t => t.a == null || t.b == null || t.a.Dead || t.b.Dead || t.a.Destroyed || t.b.Destroyed);
            if (!S.socialWeb)
            {
                cliques.Clear();
                cliquesBuilt = false;
                return;
            }
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(Eligible).Distinct().ToList();

            // People who left (banished, walked out, arrested): lessons stop, other bonds fade away.
            foreach (var t in ties.Where(t => Gone(t.a) || Gone(t.b)).ToList())
            {
                if (t.kind == TieKind.Mentor || (t.strength -= 3f) <= 1f) End(t, false, true);
            }

            // Feelings become bonds: steady mutual liking makes friends, steady loathing makes rivals.
            bool first = !seeded;
            seeded = true;
            for (int i = 0; i < pawns.Count; i++)
            {
                for (int j = i + 1; j < pawns.Count; j++)
                {
                    var x = pawns[i];
                    var y = pawns[j];
                    if (x.relations == null || y.relations == null) continue;
                    int xy = x.relations.OpinionOf(y);
                    int yx = y.relations.OpinionOf(x);
                    int low = Mathf.Min(xy, yx);
                    int high = Mathf.Max(xy, yx);
                    Evolve(x, y, TieKind.Friends, low >= 45, low - 20f, low - 25f, "CC_TieNote_Grew", first);
                    Evolve(x, y, TieKind.Rivals, high <= -30, -high - 10f, -high - 15f, "CC_TieNote_Soured", first);
                }
            }
            // Comrades slowly fade without new battles; mentorships teach and graduate.
            foreach (var t in ties.ToList())
            {
                if (t.kind == TieKind.Comrades && (t.strength -= 0.5f) <= 1f) End(t, false);
                if (t.kind == TieKind.Mentor && ties.Contains(t)) Mentor(t, comp);
            }
            TryStartMentorship(pawns);
            BuildCliques(pawns);
            // Circles have a way of making the people outside them feel it.
            if (cliques.Count > 0 && Rand.Chance(0.15f))
            {
                var lonely = pawns.Where(p => CliqueOf(p) == null && p.needs?.mood != null
                                              && !ties.Any(t => t.Involves(p) && t.kind == TieKind.Friends && t.strength >= 20f)
                                              && p.story?.traits?.HasTrait(TraitDefOf.Psychopath) != true).RandomElementWithFallback();
                if (lonely != null) CouncilGameComponent.GiveThought(lonely, CouncilDefOf.Council_Soc_LeftOut, null, -1f, "CC_Cause_LeftOut".Translate().Resolve());
            }
        }

        /// <summary>
        /// One kind of bond between two people, once a day: it forms when their feelings have been strong enough
        /// for a while (or catches up, if a budding one already exists), then drifts with how they feel now.
        /// </summary>
        private void Evolve(Pawn x, Pawn y, TieKind kind, bool forms, float formAt, float driftTo, string noteKey, bool quiet)
        {
            var t = Get(x, y, kind);
            if (t != null && t.announced)
            {
                Drift(t, driftTo);
                return;
            }
            if (forms && (quiet || Rand.Chance(0.35f)))
            {
                if (t == null) Bump(x, y, kind, formAt, noteKey.Translate(), quiet);
                else
                {
                    t.strength = Mathf.Max(t.strength, formAt);
                    t.Note(noteKey.Translate());
                    if (quiet) t.announced = true;
                    else Announce(t);
                }
                return;
            }
            if (t != null) Drift(t, driftTo);
        }

        /// <summary>No longer one of the colony, and not just away or kidnapped.</summary>
        private static bool Gone(Pawn p)
        {
            if (p == null || p.Dead || p.IsColonist) return false;
            if (p.IsPrisonerOfColony || p.IsSlaveOfColony) return true;
            return !Find.FactionManager.AllFactionsListForReading.Any(f => f.kidnapped != null && f.kidnapped.KidnappedPawnsListForReading.Contains(p));
        }

        // ---------------- Battles ----------------

        /// <summary>
        /// Hourly: while there's a fight on a home map, note everyone who is drafted, manning a turret or has just
        /// attacked a hostile. When it's been quiet for a few hours, they all become comrades, with one message.
        /// </summary>
        public void Hourly(CouncilGameComponent comp)
        {
            // Fights are tracked even with the social web off: vendettas and quirks rely on them.
            int now = Find.TickManager.TicksGame;
            bool fighting = false;
            if (!battleActive) battleFaction = null;
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                if (!GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)) continue;
                fighting = true;
                battleMap = map;
                // Who it's against: the faction with the most fighters here now (none for animals and empty mech clusters).
                bool stale = battleFaction == null || !map.mapPawns.AllPawnsSpawned.Any(x => x.Faction == battleFaction && !x.Dead && !x.Downed);
                if (stale)
                {
                    var top = map.mapPawns.AllPawnsSpawned.Where(x => !x.Dead && !x.Downed && x.Faction != null && x.Faction != Faction.OfPlayer && x.Faction.HostileTo(Faction.OfPlayer) && !x.IsPrisoner)
                        .GroupBy(x => x.Faction).OrderByDescending(g => g.Count()).FirstOrDefault();
                    battleFaction = top?.Key;
                }
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (!Eligible(p) || fighters.Contains(p)) continue;
                    var ms = p.mindState;
                    bool attacked = ms != null && now - ms.lastAttackTargetTick < GenDate.TicksPerHour * 2
                                    && ms.lastAttackedTarget.Thing is Pawn target && target.Faction != null && target.Faction.HostileTo(Faction.OfPlayer);
                    if (p.Drafted || p.CurJobDef == JobDefOf.ManTurret || attacked) fighters.Add(p);
                }
            }
            if (fighting)
            {
                if (!battleActive && (battleCountStart < 0 || now - battleCountStart > GenDate.TicksPerHour * 3))
                {
                    ResetCount();
                    battleCountStart = now;
                }
                battleActive = true;
                quietHours = 0;
                heldHours = 0;
                return;
            }
            if (!battleActive) return;
            if (++quietHours < 3) return;
            // Not over while one of ours lies bleeding or the enemy is still about (a few hours at most).
            if (heldHours < 8 && (Bleeding() || EnemyLingers(battleFaction)))
            {
                heldHours++;
                return;
            }
            battleActive = false;
            heldHours = 0;
            var foe = battleFaction;
            battleFaction = null;
            var list = fighters.Where(Eligible).Distinct().ToList();
            fighters.Clear();
            try
            {
                // Won: a real threat, seen off (not still out there), and nobody of ours carried away.
                if (WasThreat(battleCountStart) && Kidnapped() <= battleKidnapped && !EnemyLingers(foe))
                    WorldReactions.Victory(comp, list, foe, battleKills, battleFallen.ToList(), new Dictionary<Pawn, int>(killsBy));
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("victory", e);
            }
            ResetCount();
            foreach (var p in list)
            {
                comp.quirks.Notify_Fought(p);
                comp.aspirations.Notify_Event(p, "battle");
            }
            if (list.Count < 2 || !S.socialWeb) return;
            var formed = new List<Pawn>();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    bool was = Get(list[i], list[j], TieKind.Comrades)?.announced ?? false;
                    var t = Bump(list[i], list[j], TieKind.Comrades, 25f, "CC_TieNote_Battle".Translate(), true);
                    if (t != null && !was && t.announced)
                    {
                        if (!formed.Contains(list[i])) formed.Add(list[i]);
                        if (!formed.Contains(list[j])) formed.Add(list[j]);
                    }
                }
            }
            string text = "CC_Tie_Battle".Translate(TextUtil.JoinAnd(list.Select(p => p.LabelShort).ToList())).Resolve();
            Notify.Message(text, new LookTargets(list), MessageTypeDefOf.PositiveEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, text, list[0]));
            // Those who stood next to someone new for the first time write about one of them.
            foreach (var p in formed)
            {
                var other = list.Where(o => o != p && (Get(p, o, TieKind.Comrades)?.announced ?? false)).RandomElementWithFallback();
                if (other != null) comp.journals.Write(p, "Council_Journal_TieFormed_Comrades", null, other.Named("OTHER"), "".Named("SKILL"));
            }
        }

        /// <summary>Moves a tie a tenth of the way towards where their feelings now are; ends it at zero.</summary>
        private void Drift(SocialTie t, float target)
        {
            t.strength += (Mathf.Clamp(target, 0f, 100f) - t.strength) * 0.1f;
            if (t.strength <= 1f) End(t, false);
            else if (!t.announced && t.strength >= 20f) Announce(t);
        }

        private void Mentor(SocialTie t, CouncilGameComponent comp)
        {
            using var _cause = Causes.Push(t.skill != null ? "CC_Cause_Lessons".Translate(t.a?.LabelShort ?? "?", t.b?.LabelShort ?? "?", t.skill.label).Resolve() : null);
            var mentor = t.a;
            var student = t.b;
            var sk = t.skill;
            var rec = sk != null ? student.skills?.GetSkill(sk) : null;
            if (rec == null || rec.TotallyDisabled || mentor.relations == null || mentor.relations.OpinionOf(student) < -10 || !mentor.IsColonist || !student.IsColonist)
            {
                End(t, false);
                return;
            }
            if (rec.Level >= 11)
            {
                CouncilGameComponent.GiveThought(mentor, CouncilDefOf.Council_ProudMentor, student);
                CouncilGameComponent.GiveThought(student, CouncilDefOf.Council_GratefulStudent, mentor);
                End(t, true);
                return;
            }
            if (mentor.MapHeld != null && mentor.MapHeld == student.MapHeld && !mentor.Downed && !student.Downed)
            {
                rec.Learn(400f);
                t.strength = Mathf.Min(100f, t.strength + 2f);
            }
        }

        /// <summary>Starts a mentorship directly (the council arranged it), unless one already exists between them.</summary>
        public SocialTie StartMentorship(Pawn mentor, Pawn student, SkillDef skill)
        {
            if (!S.socialWeb || !Eligible(mentor) || !Eligible(student) || mentor == student || skill == null) return null;
            var rec = student.skills?.GetSkill(skill);
            if (rec == null || rec.TotallyDisabled || Get(mentor, student, TieKind.Mentor) != null || ties.Any(t => t.kind == TieKind.Mentor && t.b == student)) return null;
            var t = new SocialTie { a = mentor, b = student, kind = TieKind.Mentor, strength = 25f, formedTick = Find.TickManager.TicksGame, skill = skill };
            ties.Add(t);
            t.Note("CC_TieNote_Mentor".Translate(skill.label));
            Announce(t);
            return t;
        }

        private void TryStartMentorship(List<Pawn> pawns)
        {
            if (!Rand.Chance(0.06f)) return;
            foreach (var sk in DefDatabase<SkillDef>.AllDefsListForReading.InRandomOrder())
            {
                var mentors = pawns.Where(p => p.skills?.GetSkill(sk) is SkillRecord r && !r.TotallyDisabled && r.Level >= 12 && People.Passionate(r)).ToList();
                if (mentors.Count == 0) continue;
                var students = pawns.Where(p => p.skills?.GetSkill(sk) is SkillRecord r && !r.TotallyDisabled && r.Level <= 7 && People.Passionate(r)
                                                && !ties.Any(t => t.kind == TieKind.Mentor && t.b == p)).ToList();
                // One teaching relationship per pair, in one direction, between people who live together.
                foreach (var student in students.InRandomOrder())
                {
                    var mentor = mentors.Where(m => m != student && m.MapHeld != null && m.MapHeld == student.MapHeld && Get(m, student, TieKind.Mentor) == null
                                                    && m.relations != null && m.relations.OpinionOf(student) >= 0 && student.relations.OpinionOf(m) >= 0
                                                    && ties.Count(t => t.kind == TieKind.Mentor && t.a == m) < 2).RandomElementWithFallback();
                    if (mentor == null) continue;
                    var t = new SocialTie { a = mentor, b = student, kind = TieKind.Mentor, strength = 20f, formedTick = Find.TickManager.TicksGame, skill = sk };
                    ties.Add(t);
                    t.Note("CC_TieNote_Mentor".Translate(sk.label));
                    Announce(t);
                    return;
                }
            }
        }

        /// <summary>
        /// Groups of three to six held together by strong friendships: each built around the best-connected person
        /// left and their close friends, so a long chain of acquaintances doesn't become one giant circle.
        /// </summary>
        private void BuildCliques(List<Pawn> pawns)
        {
            cliques.Clear();
            cliquesBuilt = true;
            var strong = ties.Where(t => t.kind == TieKind.Friends && t.announced && t.strength >= 35f && pawns.Contains(t.a) && pawns.Contains(t.b)).ToList();
            var free = new HashSet<Pawn>(pawns.Where(p => strong.Any(t => t.Involves(p))));
            while (free.Count >= 3)
            {
                int Links(Pawn p) => strong.Count(t => t.Involves(p) && free.Contains(t.Other(p)));
                var leader = free.OrderByDescending(p => Links(p) * 10 + (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0)).First();
                var group = new List<Pawn> { leader };
                group.AddRange(strong.Where(t => t.Involves(leader) && free.Contains(t.Other(leader))).OrderByDescending(t => t.strength).Select(t => t.Other(leader)).Take(5));
                foreach (var p in group) free.Remove(p);
                if (group.Count < 3) continue;
                cliques.Add(new Clique { name = "CC_Clique_Name".Translate(leader.Named("PAWN")).Resolve(), members = group });
            }
        }

        /// <summary>The circles, built on first use after loading.</summary>
        public List<Clique> Cliques
        {
            get
            {
                if (!cliquesBuilt) RebuildCliques();
                return cliques;
            }
        }

        public void RebuildCliques() => BuildCliques(PawnsFinder.AllMaps_FreeColonists.Where(Eligible).ToList());

        public void ExposeData()
        {
            Scribe_Collections.Look(ref ties, "ties", LookMode.Deep);
            Scribe_Collections.Look(ref lost, "lost", LookMode.Deep);
            Scribe_Collections.Look(ref fighters, "fighters", LookMode.Reference);
            Scribe_References.Look(ref battleFaction, "battleFaction");
            Scribe_Values.Look(ref battleActive, "battleActive");
            Scribe_Values.Look(ref battleKills, "battleKills");
            Scribe_Values.Look(ref battleCountStart, "battleCountStart", -1);
            Scribe_Collections.Look(ref battleFallen, "battleFallen", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && battleFallen == null) battleFallen = new List<string>();
            Scribe_Values.Look(ref quietHours, "quietHours");
            Scribe_Values.Look(ref battleKidnapped, "battleKidnapped");
            Scribe_Values.Look(ref lastVictoryTick, "lastVictoryTick", -999999);
            Scribe_Values.Look(ref seeded, "seeded");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (ties == null) ties = new List<SocialTie>();
                ties.RemoveAll(t => t == null || t.a == null || t.b == null);
                if (lost == null) lost = new List<LostBond>();
                lost.RemoveAll(l => l == null || l.survivor == null || l.name == null);
                if (fighters == null) fighters = new List<Pawn>();
                fighters.RemoveAll(p => p == null);
                cliques = new List<Clique>();
                cliquesBuilt = false;
            }
        }
    }

    /// <summary>Opinion from an announced tie, by kind and strength: def name Council_Tie_Friends etc. Stages: new, solid (45+), deep (70+).</summary>
    public class ThoughtWorker_SocialTie : ThoughtWorker
    {
        private TieKind? kind;

        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.social == null || !ColonyCouncilMod.Settings.socialWeb || other == null) return ThoughtState.Inactive;
            if (kind == null)
            {
                string suffix = def.defName.Substring(def.defName.LastIndexOf('_') + 1);
                kind = System.Enum.TryParse(suffix, out TieKind k) ? k : TieKind.Friends;
            }
            var t = comp.social.Get(p, other, kind.Value);
            if (t == null || !t.announced) return ThoughtState.Inactive;
            return ThoughtState.ActiveAtStage(t.strength >= 70f ? 2 : t.strength >= 45f ? 1 : 0);
        }
    }
}
