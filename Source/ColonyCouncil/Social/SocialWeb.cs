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
    /// Friends grow out of how they feel about each other once they know each other (Social/Bonds.cs); rivals out of
    /// steady loathing or a grudge with a cause; comrades out of fighting near each other; a mentor (a) teaches a
    /// student (b) a skill. Everything that happens between them moves the strength.
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
        /// <summary>A rivalry's cause, in a few words (a tie note): what started it, or the biggest thing that renewed it since.</summary>
        public string cause;
        /// <summary>When a rivalry was last renewed by something that happened (it holds a few days after, then fades slowly); -1: never.</summary>
        public int renewedTick = -1;

        public bool Involves(Pawn p) => p == a || p == b;

        public Pawn Other(Pawn p) => p == a ? b : a;

        public bool Between(Pawn x, Pawn y) => (a == x && b == y) || (a == y && b == x);

        public void Note(string text)
        {
            if (text.NullOrEmpty()) return;
            string line = "CC_DayN".Translate(Find.TickManager.TicksGame / GenDate.TicksPerDay + 1) + ": " + text;
            // The same thing twice in a day (a string of cross words) is one line.
            if (history.Count > 0 && history[history.Count - 1] == line) return;
            history.Add(line);
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
            Scribe_Values.Look(ref cause, "cause");
            Scribe_Values.Look(ref renewedTick, "renewedTick", -1);
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
        /// <summary>How things stand between pairs lately, so moments between them make sense over time (Social/Moments.cs).</summary>
        public MomentTones tones = new MomentTones();
        /// <summary>What is kept about each pair: how well they know each other, fights side by side, seats lost (Social/Bonds.cs).</summary>
        public PairBook bonds = new PairBook();
        [Unsaved] public List<Clique> cliques = new List<Clique>();
        [Unsaved] private bool cliquesBuilt;
        /// <summary>The circles were built from the groups (Social/Groups/): with groups since turned off, they're built the old way again.</summary>
        [Unsaved] private bool cliquesFromGroups;

        /// <summary>The circles as they stand still fit the setting (groups on or off).</summary>
        private bool CliquesCurrent => cliquesBuilt && cliquesFromGroups == GroupLife.Active;
        /// <summary>While an old save loads: its one fight, read from where it always was, for the seat.</summary>
        [Unsaved] private Battle oldBattle;

        /// <summary>
        /// The fights going on or just over, one per home (stage 0, step 7b): two fights at two homes are two fights, each
        /// with its own fighters, foe and tally. An old save's one fight is the seat's.
        /// </summary>
        private List<Battle> battles = new List<Battle>();
        /// <summary>When the colony last celebrated a victory (clean wins within a few days of it are only chronicled).</summary>
        public int lastVictoryTick = -999999;

        /// <summary>The fight at this home (going on, or its count begun), or null.</summary>
        public Battle BattleAt(Map map)
        {
            if (map == null || battles == null) return null;
            int key = Homes.KeyOf(map);
            for (int i = 0; i < battles.Count; i++)
            {
                var b = battles[i];
                if (b == null) continue;
                // An old save's one fight: the seat's.
                if (b.home == HomesMath.None && map == Homes.Seat) b.home = key;
                if (b.home == key) return b;
            }
            return null;
        }

        private Battle BattleFor(Map map)
        {
            var b = BattleAt(map);
            if (b != null || map == null) return b;
            b = new Battle { home = Homes.KeyOf(map) };
            battles.Add(b);
            return b;
        }

        /// <summary>Rimfolk sent a raid to this home itself (Raids.Send): the fight it brings is a real one (Battle.WasThreat).</summary>
        public void Notify_RaidSent(Map map)
        {
            if (map == null || !map.IsPlayerHome) return;
            BattleFor(map).raidSent = Find.TickManager.TicksGame;
        }

        /// <summary>A fight is going on at this home.</summary>
        public bool BattleOn(Map map) => BattleAt(map)?.active ?? false;

        /// <summary>A fight is going on at any home.</summary>
        public bool AnyBattle => battles != null && battles.Any(b => b != null && b.active);

        /// <summary>The fight going on that began last (the only one, with one colony), or null.</summary>
        public Battle Current => battles?.Where(b => b != null && b.active).OrderByDescending(b => b.countStart).FirstOrDefault();

        /// <summary>An enemy fell in a fight at home (Harmony hook): counted in that home's fight.</summary>
        public void Notify_EnemyFell(Pawn killer, Map map)
        {
            if (map == null || !map.IsPlayerHome) return;
            var b = BattleFor(map);
            b.StartCount();
            b.kills++;
            if (killer != null && killer.IsColonist && killer.RaceProps.Humanlike)
                b.killsBy[killer] = (b.killsBy.TryGetValue(killer, out int n) ? n : 0) + 1;
        }

        /// <summary>
        /// One of ours died at home during a fight, or in the quiet hours after it (someone downed in the fight who bled
        /// out later still fell in it). Harmony hook, after the death.
        /// </summary>
        public void Notify_ColonistFell(Pawn p, Map map)
        {
            if (p == null || map == null || !map.IsPlayerHome) return;
            if (!BattleOn(map) && !MapFacts.ThreatOn(map)) return;
            var b = BattleFor(map);
            b.StartCount();
            if (b.fallen.Count < 20) b.fallen.Add(p.LabelShort);
        }

        /// <summary>Existing friendships and feuds are taken in quietly the first time the web runs on a save.</summary>
        private bool seeded;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        // ---------------- Queries ----------------

        // Ties by pair, for the opinion thoughts that ask about every pair of colonists. Rebuilt only when a tie is added
        // or dropped (every change to the list goes through TiesChanged), not every tick.
        [Unsaved] private Dictionary<(int, int, TieKind), SocialTie> index;
        [Unsaved] private List<SocialTie> indexList;
        [Unsaved] private int indexCount = -1;
        [Unsaved] private int tiesVersion;
        [Unsaved] private int indexVersion = -1;

        private void TiesChanged() => tiesVersion++;

        public SocialTie Get(Pawn x, Pawn y, TieKind kind)
        {
            if (x == null || y == null) return null;
            if (index == null || indexVersion != tiesVersion || indexList != ties || ties.Count != indexCount)
            {
                index = index ?? new Dictionary<(int, int, TieKind), SocialTie>();
                index.Clear();
                foreach (var t in ties)
                {
                    if (t?.a == null || t.b == null) continue;
                    var k = Key(t.a, t.b, t.kind);
                    if (!index.ContainsKey(k)) index[k] = t;
                }
                indexVersion = tiesVersion;
                indexList = ties;
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
            // With groups on, a circle is someone's first group (Social/Groups/).
            if (GroupLife.Active) return GroupLife.CliqueOf(p);
            if (!CliquesCurrent) RebuildCliques();
            return cliques.FirstOrDefault(c => c.members.Contains(p));
        }

        public bool SameClique(Pawn x, Pawn y)
        {
            if (S.socialWeb && GroupLife.Active) return Groups.Same(x, y);
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
            // One of us counts for something; with groups on, so does being of the other lot (Social/Groups/).
            w += GroupLife.Active ? Groups.Warmth(x, y) : SameClique(x, y) ? 20f : 0f;
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

        /// <summary>
        /// Something happened between them: strengthens (or creates) a tie of this kind. Quiet: no announcement. A good
        /// turn between two counts as much as they get on, and a friendship goes no further than it can now (how they get
        /// on, how well they know each other: Bonds.FriendCap); a grudge grows as their tempers and their chemistry take
        /// it, and keeps its cause (<paramref name="note"/>).
        /// </summary>
        public SocialTie Bump(Pawn x, Pawn y, TieKind kind, float amount, string note = null, bool quiet = false)
        {
            if (!S.socialWeb || !Eligible(x) || !Eligible(y) || x == y) return null;
            float cap = 100f;
            if (kind == TieKind.Friends && amount > 0f)
            {
                if (Bonds.Limits) amount *= ChemistryMath.WarmFactor(Bonds.Affinity(x, y));
                cap = Bonds.FriendCap(x, y);
            }
            if (kind == TieKind.Rivals && amount > 0f && Bonds.DeedsOn) amount *= Bonds.GrudgeFactor(x, y);
            var t = Get(x, y, kind);
            if (t == null)
            {
                // Mentorships only start in TryStartMentorship, where the skill is known; no friendship with no room for one.
                if (amount <= 0f || kind == TieKind.Mentor || cap < 1f) return null;
                t = new SocialTie { a = x, b = y, kind = kind, strength = 0f, formedTick = Find.TickManager.TicksGame };
                ties.Add(t);
                TiesChanged();
            }
            float before = t.strength;
            // Up to the cap, never pulled down by it here (the day's drift does that, slowly).
            t.strength = amount > 0f ? Mathf.Clamp(Mathf.Max(before, Mathf.Min(before + amount, cap)), 0f, 100f) : Mathf.Clamp(before + amount, 0f, 100f);
            t.Note(note);
            if (kind == TieKind.Rivals && amount > 0f)
            {
                t.renewedTick = Find.TickManager.TicksGame;
                if (!note.NullOrEmpty() && (t.cause.NullOrEmpty() || amount >= 5f)) t.cause = note;
            }
            if (kind == TieKind.Friends && t.announced && before < 70f && t.strength >= 70f) Bonds.Notify_FriendTie(t, true);
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
            // apart: news of a bond ending; a mentorship's end comes with its word of thanks, face to face (Mentor).
            if (ties.Remove(t)) TiesChanged();
            if (!t.announced || t.a == null || t.b == null || t.a.Dead || t.b.Dead) return;
            // Bonds with people who left end without a word; comradeship fades without ceremony, only a line in each journal.
            if (!graduated && (!t.a.IsColonist || !t.b.IsColonist)) quiet = true;
            bool fades = !graduated && t.kind == TieKind.Comrades;
            var aspirations = CouncilGameComponent.Instance?.aspirations;
            if (graduated) aspirations?.Notify(t.a, "graduated");
            else if (t.kind == TieKind.Rivals && t.a.IsColonist && t.b.IsColonist)
            {
                aspirations?.Notify(t.a, "peace");
                aspirations?.Notify(t.b, "peace");
            }
            if (quiet) return;
            var comp = CouncilGameComponent.Instance;
            string key = graduated ? "CC_Tie_Graduated" : "CC_Tie_Ended_" + t.kind;
            if (!fades && !key.CanTranslate()) return;
            if (!fades)
            {
                News.Post(key.Translate(t.a.Named("A"), t.b.Named("B"), (t.skill?.label ?? "").Named("SKILL")).Resolve(), new LookTargets(t.a, t.b), NewsKind.People, MessageTypeDefOf.NeutralEvent, graduated ? Importance.Notable : Importance.Flavor);
                comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, key.Translate(t.a.Named("A"), t.b.Named("B"), (t.skill?.label ?? "").Named("SKILL")).Resolve(), t.a));
            }
            string prefix = graduated ? "Council_Journal_TieGraduated_" : "Council_Journal_TieEnded_";
            comp?.journals.Write(t.a, JournalPool(prefix, t, t.a), null, t.b.Named("OTHER"), (t.skill?.label ?? "").Named("SKILL"));
            comp?.journals.Write(t.b, JournalPool(prefix, t, t.b), null, t.a.Named("OTHER"), (t.skill?.label ?? "").Named("SKILL"));
        }

        private void Announce(SocialTie t)
        {
            t.announced = true;
            var comp = CouncilGameComponent.Instance;
            // A rivalry is told with its cause when it has one (what happened between them).
            string key = t.kind == TieKind.Rivals && !t.cause.NullOrEmpty() ? "CC_Tie_Formed_RivalsCause" : "CC_Tie_Formed_" + t.kind;
            var args = new[] { t.a.Named("A"), t.b.Named("B"), (t.skill?.label ?? "").Named("SKILL"), (t.cause ?? "").Named("CAUSE") };
            News.Post(key.Translate(args).Resolve(), new LookTargets(t.a, t.b), NewsKind.People, t.kind == TieKind.Rivals ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent, Importance.Notable);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, key.Translate(args).Resolve(), t.a));
            if (t.kind == TieKind.Friends) Bonds.Notify_FriendTie(t, t.strength >= 70f);
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
                    // Written once, whoever notices the death first (the grief, the mark it leaves, the group: FirstOnLoss).
                    if (comp.journals.FirstOnLoss(alive, dead))
                        comp.journals.Write(alive, t.kind == TieKind.Rivals ? "Council_Journal_RivalLost" : "Council_Journal_TieLost", null, dead.Named("OTHER"));
                    lost.Add(new LostBond { survivor = alive, name = dead.LabelShort, kind = t.kind, tick = Find.TickManager.TicksGame });
                    PersonaMarks.Notify_BondLost(alive, dead, t.kind, t.kind == TieKind.Mentor && t.b == alive);
                }
                lost.RemoveAll(l => l.survivor == null || l.survivor.Dead);
                while (lost.Count > 40) lost.RemoveAt(0);
            }
            if (ties.RemoveAll(t => t.a == null || t.b == null || t.a.Dead || t.b.Dead || t.a.Destroyed || t.b.Destroyed) > 0) TiesChanged();
            tones.Prune();
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

            // How well each pair knows each other settles for the day, and envy of a better room or title (Social/Bonds.cs).
            try
            {
                Bonds.Daily(this, pawns);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("bonds: the day", e);
            }

            // Feelings become bonds: liking makes friends once they know each other, steady loathing makes rivals.
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
                    // A friendship needs both good feeling and knowing each other, and goes as far as the two can (Social/Bonds.cs).
                    Evolve(x, y, TieKind.Friends, first ? low >= 45 : Bonds.FriendsForm(x, y, low), Mathf.Min(low - 20f, Bonds.FriendCap(x, y)),
                        Bonds.FriendTarget(x, y, low), "CC_TieNote_Grew", first);
                    Evolve(x, y, TieKind.Rivals, high <= -30, -high - 10f, -high - 15f, "CC_TieNote_Soured", first);
                }
            }
            // Comrades fade in long quiet spells, down to what their fights together leave; mentorships teach and graduate.
            int today = Find.TickManager.TicksGame;
            foreach (var t in ties.ToList())
            {
                if (t.kind == TieKind.Comrades)
                {
                    if (Bonds.Danger)
                    {
                        var r = bonds.Get(t.a, t.b, false);
                        int quiet = r == null || r.lastBattle < 0 ? 999 : (today - r.lastBattle) / GenDate.TicksPerDay;
                        t.strength = BondsMath.ComradeDay(t.strength, quiet, r?.battles ?? 0);
                    }
                    else t.strength -= 0.5f;
                    if (t.strength <= 1f) End(t, false);
                }
                if (t.kind == TieKind.Mentor && ties.Contains(t)) Mentor(t, comp);
            }
            TryStartMentorship(pawns);
            // With groups on (Social/Groups/), the circles are the colony's groups, and their day runs here; the left-out
            // feeling is theirs too.
            if (GroupLife.Active)
            {
                try
                {
                    GroupLife.Daily(comp, this, pawns);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("groups", e);
                }
                return;
            }
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
                    if (kind == TieKind.Rivals)
                    {
                        if (t.cause.NullOrEmpty()) t.cause = noteKey.Translate();
                        t.renewedTick = Find.TickManager.TicksGame;
                    }
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
        /// attacked a hostile. When it's been quiet for a few hours, those who fought near each other become comrades (or
        /// closer comrades), more when one pulled the other out (Social/FightSides.cs), with one message.
        /// </summary>
        public void Hourly(CouncilGameComponent comp)
        {
            // Fights are tracked even with the social web off: vendettas and quirks rely on them. Each home's on its own.
            int now = Find.TickManager.TicksGame;
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome).ToList())
                BattleHour(comp, map, now);
            // A fight at a home no longer ours is over without a word.
            battles.RemoveAll(b => b == null || (b.home != HomesMath.None && Homes.MapOf(b.home) == null));
        }

        private void BattleHour(CouncilGameComponent comp, Map map, int now)
        {
            var b = BattleAt(map);
            if (b != null && !b.active) b.faction = null;
            if (MapFacts.ThreatOn(map))
            {
                b ??= BattleFor(map);
                // Who it's against: the faction with the most fighters here now (none for animals and empty mech clusters).
                bool stale = b.faction == null || !map.mapPawns.AllPawnsSpawned.Any(x => x.Faction == b.faction && !x.Dead && !x.Downed);
                if (stale)
                {
                    var top = map.mapPawns.AllPawnsSpawned.Where(x => !x.Dead && !x.Downed && x.Faction != null && x.Faction != Faction.OfPlayer && x.Faction.HostileTo(Faction.OfPlayer) && !x.IsPrisoner)
                        .GroupBy(x => x.Faction).OrderByDescending(g => g.Count()).FirstOrDefault();
                    b.faction = top?.Key;
                }
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (!Eligible(p) || b.fighters.Contains(p)) continue;
                    var ms = p.mindState;
                    bool attacked = ms != null && now - ms.lastAttackTargetTick < GenDate.TicksPerHour * 2
                                    && ms.lastAttackedTarget.Thing is Pawn target && target.Faction != null && target.Faction.HostileTo(Faction.OfPlayer);
                    if (p.Drafted || p.CurJobDef == JobDefOf.ManTurret || attacked) b.fighters.Add(p);
                }
                if (!b.active && (b.countStart < 0 || now - b.countStart > GenDate.TicksPerHour * 3))
                {
                    b.ResetCount();
                    b.countStart = now;
                }
                b.active = true;
                b.quietHours = 0;
                b.heldHours = 0;
                return;
            }
            if (b == null || !b.active) return;
            if (++b.quietHours < 3) return;
            // Not over while one of ours lies bleeding or the enemy is still about (a few hours at most).
            if (b.heldHours < 8 && (Battle.Bleeding(map) || Battle.EnemyLingers(b.faction, map)))
            {
                b.heldHours++;
                return;
            }
            b.active = false;
            b.heldHours = 0;
            var foe = b.faction;
            b.faction = null;
            var list = b.fighters.Where(Eligible).Distinct().ToList();
            b.fighters.Clear();
            try
            {
                // Won: a real threat, seen off (not still out there), and nobody of ours carried away.
                if (b.WasThreat(map) && Battle.Kidnapped() <= b.kidnapped && !Battle.EnemyLingers(foe, map))
                    WorldReactions.Victory(comp, list, foe, b.kills, b.fallen.ToList(), new Dictionary<Pawn, int>(b.killsBy), map);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("victory", e);
            }
            // When the fight began, read before the count is reset (the quirks look for wounds since then).
            int began = b.countStart;
            b.ResetCount();
            foreach (var p in list)
            {
                comp.quirks.Notify_Fought(p, began);
                comp.aspirations.Notify_Event(p, "battle");
            }
            TalkHooks.Battle(list, foe);
            try
            {
                if (!S.socialWeb) return;
                if (Bonds.Danger) SharedDanger(comp, list);
                else if (list.Count >= 2) Comrades(comp, list, list.SelectMany(x => list.Where(y => y.thingIDNumber > x.thingIDNumber).Select(y => (x, y, 25f, 0))).ToList());
            }
            finally
            {
                // What happened in the fight is read; another home's fight still going keeps its own.
                if (!AnyBattle) FightSides.Clear();
            }
        }

        /// <summary>
        /// The fight is over: comradeship between those who fought near each other (seen side by side in it), more when
        /// one carried the other out, tended them under fire or stood over them while they were down. Every fight together
        /// counts in full. Someone helped under fire counts even if they went down before they were noted as fighting.
        /// The rescued and the tended are grateful, and the rescued go and say so once they're up.
        /// </summary>
        private void SharedDanger(CouncilGameComponent comp, List<Pawn> fighters)
        {
            // apart: the fight's outcome, told after it; who stood beside whom was seen on the map (FightSides).
            var people = new List<Pawn>(fighters);
            foreach (var p in PawnsFinder.AllMaps_FreeColonists.ToList())
                if (Eligible(p) && !people.Contains(p) && fighters.Any(h => FightSides.Deeds(h, p) != 0)) people.Add(p);
            var pairs = new List<(Pawn x, Pawn y, float bond, int deeds)>();
            for (int i = 0; i < people.Count; i++)
                for (int j = i + 1; j < people.Count; j++)
                {
                    var x = people[i];
                    var y = people[j];
                    int deeds = FightSides.Deeds(x, y) | FightSides.Deeds(y, x);
                    float bond = fighters.Contains(x) && fighters.Contains(y)
                        ? BondsMath.BattleBond(FightSides.Near(x, y), FightSides.Samples, deeds)
                        : deeds != 0 ? 8f + BondsMath.DeedBond(deeds) : 0f;
                    if (bond > 0f) pairs.Add((x, y, bond, deeds));
                }
            Comrades(comp, people, pairs);
            int now = Find.TickManager.TicksGame;
            foreach (var (x, y, _, deeds) in pairs)
            {
                var r = bonds.Get(x, y, true);
                r.battles++;
                r.lastBattle = now;
                Bonds.Know(x, y, BondsMath.BattleKnow);
                // A rivalry eases between two who fought side by side, more when one saved the other.
                if (Get(x, y, TieKind.Rivals) != null) Weaken(x, y, TieKind.Rivals, BondsMath.BattleMend(deeds), "CC_TieNote_FoughtTogether".Translate());
            }
            if (!Bonds.DeedsOn) return;
            foreach (var helped in people)
                foreach (var helper in people)
                {
                    if (helper == helped) continue;
                    int d = FightSides.Deeds(helper, helped);
                    if ((d & BondsMath.DeedRescued) != 0) Saved(helper, helped, true);
                    else if ((d & BondsMath.DeedTended) != 0) Saved(helper, helped, false);
                }
        }

        /// <summary>The fight's comradeship for each pair (with how much it adds), one message for everyone who gained some.</summary>
        private void Comrades(CouncilGameComponent comp, List<Pawn> people, List<(Pawn x, Pawn y, float bond, int deeds)> pairs)
        {
            // apart: the fight's outcome, told after it.
            if (pairs.Count == 0) return;
            var formed = new List<Pawn>();
            var bonded = new List<Pawn>();
            foreach (var (x, y, bond, deeds) in pairs)
            {
                bool was = Get(x, y, TieKind.Comrades)?.announced ?? false;
                string note = (deeds & BondsMath.DeedRescued) != 0 ? "CC_TieNote_Rescued".Translate().Resolve()
                    : (deeds & BondsMath.DeedTended) != 0 ? "CC_TieNote_TendedUnderFire".Translate().Resolve()
                    : "CC_TieNote_Battle".Translate().Resolve();
                var t = Bump(x, y, TieKind.Comrades, bond, note, true);
                if (t == null) continue;
                if (!bonded.Contains(x)) bonded.Add(x);
                if (!bonded.Contains(y)) bonded.Add(y);
                if (!was && t.announced)
                {
                    if (!formed.Contains(x)) formed.Add(x);
                    if (!formed.Contains(y)) formed.Add(y);
                }
            }
            if (bonded.Count < 2) return;
            bonded = people.Where(bonded.Contains).ToList();
            string text = "CC_Tie_Battle".Translate(TextUtil.JoinAnd(bonded.Select(p => p.LabelShort).ToList())).Resolve();
            Notify.Message(text, new LookTargets(bonded), MessageTypeDefOf.PositiveEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, text, bonded[0]));
            // Those who stood next to someone new for the first time write about one of them.
            foreach (var p in formed)
            {
                var other = bonded.Where(o => o != p && (Get(p, o, TieKind.Comrades)?.announced ?? false)).RandomElementWithFallback();
                if (other != null) comp.journals.Write(p, "Council_Journal_TieFormed_Comrades", null, other.Named("OTHER"), "".Named("SKILL"));
            }
        }

        /// <summary>
        /// <paramref name="helper"/> carried <paramref name="helped"/> out of the fight (or tended them under fire): the
        /// helped one is grateful, and someone carried out goes to say thanks once both are up (within a few days).
        /// </summary>
        private static void Saved(Pawn helper, Pawn helped, bool carried)
        {
            // apart: gratitude for what was done under fire (seen on the map); the thanks itself is said face to face (Together).
            CouncilGameComponent.GiveThought(helped, CouncilDefOf.Council_SavedMe, helper, -1f, null, carried ? 1 : 0);
            if (!carried) return;
            Together.When("saved|" + helper.thingIDNumber + "|" + helped.thingIDNumber, helped, helper, () =>
            {
                string line = Interplay.Line("Rimfolk_Pool_ThanksSaved", helped, null, helper.Named("OTHER"));
                if (!line.NullOrEmpty()) Speech.Say(helped, line, helper);
            }, 96f);
        }

        /// <summary>
        /// Moves a tie a tenth of the way towards where their feelings now are; ends it at zero. A grudge (with "What
        /// people did is remembered") only follows their feelings when those are worse; otherwise it holds a few days after
        /// it was last renewed and then fades slowly (BondsMath.GrudgeDay).
        /// </summary>
        private void Drift(SocialTie t, float target)
        {
            float before = t.strength;
            target = Mathf.Clamp(target, 0f, 100f);
            if (t.kind == TieKind.Rivals && Bonds.DeedsOn && target <= t.strength) t.strength = Bonds.GrudgeDay(t);
            else t.strength += (target - t.strength) * 0.1f;
            if (t.strength <= 1f) End(t, false);
            else if (!t.announced && t.strength >= 20f) Announce(t);
            else if (t.kind == TieKind.Friends && t.announced && before < 70f && t.strength >= 70f) Bonds.Notify_FriendTie(t, true);
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
            string pair = mentor.thingIDNumber + "|" + student.thingIDNumber;
            if (rec.Level >= 11)
            {
                // Graduation is a word of thanks, face to face: when they're together, or the student goes to find the
                // mentor in their free time. Until then the mentorship stands (asked again each day).
                Together.When("graduate|" + pair, student, mentor, () =>
                {
                    if (!ties.Contains(t)) return;
                    CouncilGameComponent.GiveThought(mentor, CouncilDefOf.Council_ProudMentor, student);
                    CouncilGameComponent.GiveThought(student, CouncilDefOf.Council_GratefulStudent, mentor);
                    // The thanks in the student's own voice; the keyed line when the pool has nothing that fits.
                    string skillLabel = sk?.label ?? "";
                    string thanks = Interplay.Line("Council_Pool_Together_Graduated", student, null, mentor.Named("OTHER"), skillLabel.Named("SKILL"))
                                    ?? "CC_Together_Graduated".Translate(mentor.Named("A"), student.Named("B"), skillLabel.Named("SKILL")).Resolve();
                    SocialLog.Add(student, mentor, thanks);
                    End(t, true);
                }, 48f, 4f);
                return;
            }
            // The day's lesson happens when they're together (working side by side, a meal, an evening): within the day.
            Together.When("lesson|" + pair, mentor, student, () =>
            {
                if (!ties.Contains(t) || rec.TotallyDisabled) return;
                rec.Learn(400f);
                t.strength = Mathf.Min(100f, t.strength + 2f);
            }, 24f);
        }

        /// <summary>Starts a mentorship directly (the council arranged it), unless one already exists between them.</summary>
        public SocialTie StartMentorship(Pawn mentor, Pawn student, SkillDef skill)
        {
            if (!S.socialWeb || !Eligible(mentor) || !Eligible(student) || mentor == student || skill == null) return null;
            var rec = student.skills?.GetSkill(skill);
            if (rec == null || rec.TotallyDisabled || Get(mentor, student, TieKind.Mentor) != null || ties.Any(t => t.kind == TieKind.Mentor && t.b == student)) return null;
            var t = new SocialTie { a = mentor, b = student, kind = TieKind.Mentor, strength = 25f, formedTick = Find.TickManager.TicksGame, skill = skill };
            ties.Add(t);
            TiesChanged();
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
                                                    && ties.Count(t => t.kind == TieKind.Mentor && t.a == m) < 2).RandomElementByWeightWithFallback(PersonaLean.Mentoring);
                    if (mentor == null) continue;
                    var t = new SocialTie { a = mentor, b = student, kind = TieKind.Mentor, strength = 20f, formedTick = Find.TickManager.TicksGame, skill = sk };
                    ties.Add(t);
                    TiesChanged();
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
            // A new list: the groups' circles may still be held by whoever asked for them.
            cliques = new List<Clique>();
            cliquesBuilt = true;
            cliquesFromGroups = false;
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
                // Named for their friendship, never after the one at its middle; the same name while they lead it.
                var taken = cliques.Select(c => c.name).ToList();
                cliques.Add(new Clique { name = GroupText.CircleName(leader.thingIDNumber, taken), members = group });
            }
        }

        /// <summary>The circles, built on first use after loading.</summary>
        public List<Clique> Cliques
        {
            get
            {
                if (!CliquesCurrent) RebuildCliques();
                return cliques;
            }
        }

        public void RebuildCliques()
        {
            if (GroupLife.Active)
            {
                cliques = GroupLife.BuildCliques();
                cliquesBuilt = true;
                cliquesFromGroups = true;
                return;
            }
            BuildCliques(PawnsFinder.AllMaps_FreeColonists.Where(Eligible).ToList());
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref ties, "ties", LookMode.Deep);
            Scribe_Collections.Look(ref lost, "lost", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) battles?.RemoveAll(b => b == null || (!b.active && b.countStart < 0));
            Scribe_Collections.Look(ref battles, "battles", LookMode.Deep);
            // A save from before there was a fight per home: its one fight, where it always was, is the seat's.
            if (Scribe.mode == LoadSaveMode.LoadingVars && battles == null)
            {
                oldBattle = new Battle();
                battles = new List<Battle> { oldBattle };
            }
            if (oldBattle != null && Scribe.mode != LoadSaveMode.Saving) oldBattle.ExposeData();
            Scribe_Values.Look(ref lastVictoryTick, "lastVictoryTick", -999999);
            Scribe_Values.Look(ref seeded, "seeded");
            Scribe_Deep.Look(ref tones, "tones");
            Scribe_Deep.Look(ref bonds, "bonds");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (tones == null) tones = new MomentTones();
                if (bonds == null) bonds = new PairBook();
                if (ties == null) ties = new List<SocialTie>();
                ties.RemoveAll(t => t == null || t.a == null || t.b == null);
                TiesChanged();
                if (lost == null) lost = new List<LostBond>();
                lost.RemoveAll(l => l == null || l.survivor == null || l.name == null);
                oldBattle = null;
                battles ??= new List<Battle>();
                battles.RemoveAll(b => b == null);
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
