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

    public class Clique
    {
        public string name;
        public List<Pawn> members = new List<Pawn>();
    }

    public class SocialWeb : IExposable
    {
        public List<SocialTie> ties = new List<SocialTie>();
        [Unsaved] public List<Clique> cliques = new List<Clique>();
        private Dictionary<Pawn, float> damageAtThreat = new Dictionary<Pawn, float>();
        private int threatTracked;
        private int threatResolved;
        private List<Pawn> dmgKeys;
        private List<float> dmgValues;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        // ---------------- Queries ----------------

        public SocialTie Get(Pawn x, Pawn y, TieKind kind) => ties.FirstOrDefault(t => t.kind == kind && t.Between(x, y));

        public IEnumerable<SocialTie> Of(Pawn p) => ties.Where(t => t.Involves(p));

        public float Strength(Pawn x, Pawn y, TieKind kind) => Get(x, y, kind)?.strength ?? 0f;

        public Clique CliqueOf(Pawn p) => cliques.FirstOrDefault(c => c.members.Contains(p));

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
                var names = ties.Where(t => t.kind == kind && t.announced && t.Involves(p) && t.Other(p) != null && !t.Other(p).Dead)
                    .OrderByDescending(t => t.strength).Select(t => t.Other(p).LabelShort).ToList();
                if (names.Count > 0) parts.Add(("CC_Social_Describe_" + kind).Translate(TextUtil.JoinAnd(names)).Resolve());
            }
            foreach (var t in ties.Where(t => t.kind == TieKind.Mentor && t.Involves(p) && t.skill != null && t.Other(p) != null))
                parts.Add((t.a == p ? "CC_Social_Describe_Teaching" : "CC_Social_Describe_Learning").Translate(t.Other(p).LabelShort, t.skill.label).Resolve());
            var circle = CliqueOf(p);
            if (circle != null) parts.Add("CC_Social_Describe_Circle".Translate(circle.name).Resolve());
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        private static bool Eligible(Pawn p) => p != null && !p.Dead && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby();

        // ---------------- Changes ----------------

        /// <summary>Something happened between them: strengthens (or creates) a tie of this kind.</summary>
        public SocialTie Bump(Pawn x, Pawn y, TieKind kind, float amount, string note = null)
        {
            if (!S.socialWeb || !Eligible(x) || !Eligible(y) || x == y) return null;
            var t = Get(x, y, kind);
            if (t == null)
            {
                // Mentorships only start in TryStartMentorship, where the skill is known.
                if (amount <= 0f || kind == TieKind.Mentor) return null;
                t = new SocialTie { a = x, b = y, kind = kind, strength = 0f, formedTick = Find.TickManager.TicksGame };
                ties.Add(t);
                t.strength = Mathf.Clamp(amount, 0f, 100f);
                t.Note(note);
                if (t.strength >= 20f) Announce(t, true);
                return t;
            }
            t.strength = Mathf.Clamp(t.strength + amount, 0f, 100f);
            t.Note(note);
            if (!t.announced && t.strength >= 20f) Announce(t, true);
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

        private void End(SocialTie t, bool graduated)
        {
            ties.Remove(t);
            if (!t.announced || t.a == null || t.b == null || t.a.Dead || t.b.Dead) return;
            string key = graduated ? "CC_Tie_Graduated" : "CC_Tie_Ended_" + t.kind;
            if (!key.CanTranslate()) return;
            var comp = CouncilGameComponent.Instance;
            Messages.Message(key.Translate(t.a.Named("A"), t.b.Named("B"), (t.skill?.label ?? "").Named("SKILL")).Resolve(), new LookTargets(t.a, t.b), MessageTypeDefOf.NeutralEvent, false);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, key.Translate(t.a.Named("A"), t.b.Named("B"), (t.skill?.label ?? "").Named("SKILL")).Resolve(), t.a));
            string prefix = graduated ? "Council_Journal_TieGraduated_" : "Council_Journal_TieEnded_";
            comp?.journals.Write(t.a, JournalPool(prefix, t, t.a), null, t.b.Named("OTHER"), (t.skill?.label ?? "").Named("SKILL"));
            comp?.journals.Write(t.b, JournalPool(prefix, t, t.b), null, t.a.Named("OTHER"), (t.skill?.label ?? "").Named("SKILL"));
        }

        private void Announce(SocialTie t, bool formed)
        {
            t.announced = true;
            var comp = CouncilGameComponent.Instance;
            string key = "CC_Tie_Formed_" + t.kind;
            var args = new[] { t.a.Named("A"), t.b.Named("B"), (t.skill?.label ?? "").Named("SKILL") };
            Messages.Message(key.Translate(args).Resolve(), new LookTargets(t.a, t.b), t.kind == TieKind.Rivals ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent, false);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, key.Translate(args).Resolve(), t.a));
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
            // Someone died: the ones who were close (or at war with them) write about it.
            if (S.socialWeb)
            {
                foreach (var t in ties.Where(t => t.a != null && t.b != null && t.a.Dead != t.b.Dead && t.announced).ToList())
                {
                    var dead = t.a.Dead ? t.a : t.b;
                    var alive = t.Other(dead);
                    if (Eligible(alive)) comp.journals.Write(alive, t.kind == TieKind.Rivals ? "Council_Journal_RivalLost" : "Council_Journal_TieLost", null, dead.Named("OTHER"));
                }
            }
            ties.RemoveAll(t => t.a == null || t.b == null || t.a.Dead || t.b.Dead || t.a.Destroyed || t.b.Destroyed);
            if (!S.socialWeb) return;
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(Eligible).ToList();

            // Feelings become bonds: steady mutual liking makes friends, steady loathing makes rivals.
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
                    var friends = Get(x, y, TieKind.Friends);
                    var rivals = Get(x, y, TieKind.Rivals);
                    if (friends != null) Drift(friends, low - 25f);
                    else if (low >= 45 && Rand.Chance(0.35f)) Bump(x, y, TieKind.Friends, low - 20f, "CC_TieNote_Grew".Translate());
                    if (rivals != null) Drift(rivals, -high - 15f);
                    else if (high <= -30 && Rand.Chance(0.35f)) Bump(x, y, TieKind.Rivals, -high - 10f, "CC_TieNote_Soured".Translate());
                }
            }
            // Comrades slowly fade without new battles; mentorships teach and graduate.
            foreach (var t in ties.ToList())
            {
                if (t.kind == TieKind.Comrades && (t.strength -= 0.5f) <= 1f) End(t, false);
                if (t.kind == TieKind.Mentor) Mentor(t, comp);
            }
            TryStartMentorship(pawns);
            TrackBattles(comp, pawns);
            BuildCliques(pawns);
            // Circles have a way of making the people outside them feel it.
            if (cliques.Count > 0 && Rand.Chance(0.15f))
            {
                var lonely = pawns.Where(p => CliqueOf(p) == null && p.needs?.mood != null
                                              && !ties.Any(t => t.Involves(p) && t.kind == TieKind.Friends && t.strength >= 20f)
                                              && p.story?.traits?.HasTrait(TraitDefOf.Psychopath) != true).RandomElementWithFallback();
                if (lonely != null) CouncilGameComponent.GiveThought(lonely, CouncilDefOf.Council_Soc_LeftOut);
            }
        }

        /// <summary>Moves a tie a tenth of the way towards where their feelings now are; ends it at zero.</summary>
        private void Drift(SocialTie t, float target)
        {
            t.strength += (Mathf.Clamp(target, 0f, 100f) - t.strength) * 0.1f;
            if (t.strength <= 1f) End(t, false);
            else if (!t.announced && t.strength >= 20f) Announce(t, true);
        }

        private void Mentor(SocialTie t, CouncilGameComponent comp)
        {
            var mentor = t.a;
            var student = t.b;
            var sk = t.skill;
            var rec = sk != null ? student.skills?.GetSkill(sk) : null;
            if (rec == null || rec.TotallyDisabled || mentor.relations == null || mentor.relations.OpinionOf(student) < -10)
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
                rec.Learn(400f, true);
                t.strength = Mathf.Min(100f, t.strength + 2f);
            }
        }

        private void TryStartMentorship(List<Pawn> pawns)
        {
            if (!Rand.Chance(0.06f)) return;
            foreach (var sk in DefDatabase<SkillDef>.AllDefsListForReading.InRandomOrder())
            {
                var mentors = pawns.Where(p => p.skills?.GetSkill(sk) is SkillRecord r && !r.TotallyDisabled && r.Level >= 12 && r.passion != Passion.None).ToList();
                if (mentors.Count == 0) continue;
                var students = pawns.Where(p => p.skills?.GetSkill(sk) is SkillRecord r && !r.TotallyDisabled && r.Level <= 7 && r.passion != Passion.None
                                                && !ties.Any(t => t.kind == TieKind.Mentor && t.b == p)).ToList();
                foreach (var student in students.InRandomOrder())
                {
                    var mentor = mentors.Where(m => m != student && m.relations != null && m.relations.OpinionOf(student) >= 0 && student.relations.OpinionOf(m) >= 0
                                                    && ties.Count(t => t.kind == TieKind.Mentor && t.a == m) < 2).RandomElementWithFallback();
                    if (mentor == null) continue;
                    var t = new SocialTie { a = mentor, b = student, kind = TieKind.Mentor, strength = 20f, formedTick = Find.TickManager.TicksGame, skill = sk };
                    ties.Add(t);
                    t.Note("CC_TieNote_Mentor".Translate(sk.label));
                    Announce(t, true);
                    return;
                }
            }
        }

        /// <summary>Who fought in the last battle: everyone who dealt damage becomes comrades with the others who did.</summary>
        private void TrackBattles(CouncilGameComponent comp, List<Pawn> pawns)
        {
            int threat = Find.Maps.Where(m => m.IsPlayerHome).Select(m => m.storyState.LastThreatBigTick).DefaultIfEmpty(0).Max();
            int now = Find.TickManager.TicksGame;
            // The snapshot is refreshed every quiet day, so when trouble comes it holds everyone's damage from before it.
            if (threat > threatTracked)
            {
                threatTracked = threat;
                if (damageAtThreat.Count == 0) Snapshot(pawns);
                return;
            }
            if (threatTracked > threatResolved)
            {
                if (now - threatTracked < GenDate.TicksPerDay) return;
                threatResolved = threatTracked;
                var fighters = pawns.Where(p => p.records != null && damageAtThreat.TryGetValue(p, out float before) && p.records.GetValue(RecordDefOf.DamageDealt) - before > 10f).ToList();
                for (int i = 0; i < fighters.Count; i++)
                    for (int j = i + 1; j < fighters.Count; j++)
                        Bump(fighters[i], fighters[j], TieKind.Comrades, 25f, "CC_TieNote_Battle".Translate());
            }
            Snapshot(pawns);
        }

        private void Snapshot(List<Pawn> pawns) =>
            damageAtThreat = pawns.Where(p => p.records != null).ToDictionary(p => p, p => p.records.GetValue(RecordDefOf.DamageDealt));

        /// <summary>Groups of three or more held together by strong friendships and shared battles.</summary>
        private void BuildCliques(List<Pawn> pawns)
        {
            cliques.Clear();
            var strong = ties.Where(t => (t.kind == TieKind.Friends || t.kind == TieKind.Comrades) && t.strength >= 30f).ToList();
            var seen = new HashSet<Pawn>();
            foreach (var start in pawns)
            {
                if (seen.Contains(start) || !strong.Any(t => t.Involves(start))) continue;
                var group = new List<Pawn>();
                var queue = new Queue<Pawn>();
                queue.Enqueue(start);
                seen.Add(start);
                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    group.Add(p);
                    foreach (var t in strong.Where(t => t.Involves(p)))
                    {
                        var o = t.Other(p);
                        if (o != null && pawns.Contains(o) && seen.Add(o)) queue.Enqueue(o);
                    }
                }
                if (group.Count < 3) continue;
                var leader = group.OrderByDescending(p => strong.Count(t => t.Involves(p)) * 10 + (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0)).First();
                cliques.Add(new Clique { name = "CC_Clique_Name".Translate(leader.Named("PAWN")).Resolve(), members = group });
            }
        }

        public void RebuildCliques() => BuildCliques(PawnsFinder.AllMaps_FreeColonists.Where(Eligible).ToList());

        public void ExposeData()
        {
            Scribe_Collections.Look(ref ties, "ties", LookMode.Deep);
            Scribe_Collections.Look(ref damageAtThreat, "damageAtThreat", LookMode.Reference, LookMode.Value, ref dmgKeys, ref dmgValues);
            Scribe_Values.Look(ref threatTracked, "threatTracked");
            Scribe_Values.Look(ref threatResolved, "threatResolved");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (ties == null) ties = new List<SocialTie>();
                ties.RemoveAll(t => t == null || t.a == null || t.b == null);
                if (damageAtThreat == null) damageAtThreat = new Dictionary<Pawn, float>();
                foreach (var k in damageAtThreat.Keys.Where(k => k == null).ToList()) damageAtThreat.Remove(k);
                cliques = new List<Clique>();
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
            if (comp == null || !ColonyCouncilMod.Settings.socialWeb || other == null) return ThoughtState.Inactive;
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
