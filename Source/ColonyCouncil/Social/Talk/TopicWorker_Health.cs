using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The sick and the hurt (Rimfolk_Topic_Health): comes up only while someone of the colony on this map is ill (one of
    /// Rimfolk's ailments or illnesses, or the game's own) or hurt and still healing, and is about them when it isn't one
    /// of the two talking ({SUBJECT}). The view is how the sick should be treated: put to bed and nursed whatever the work
    /// (for), or worked through, with the beds kept for the dying (against). Who is ill, how badly and who doctors best is
    /// looked up once an hour a map, as the talk's scene is.
    /// </summary>
    public class TopicWorker_Health : TopicWorker
    {
        private sealed class Look
        {
            public int tick = -999999;
            public readonly List<Pawn> sick = new List<Pawn>();
            /// <summary>The worst of them is ill with something that can kill (a serious illness, the game's plague).</summary>
            public Pawn serious;
            public Pawn doctor;
        }

        private static readonly Dictionary<int, Look> looks = new Dictionary<int, Look>();
        private static Game game;

        /// <summary>Ill with something that can kill: Rimfolk's serious illnesses, the game's diseases with a lethal severity.</summary>
        private static bool Serious(Pawn p)
        {
            if (LifeConditions.Ailing(p, true)) return true;
            var h = LifeChecks.Illness(p);
            return h != null && AilmentTracker.InfoOf(h.def) == null && h.def.lethalSeverity > 0f;
        }

        private static bool Sick(Pawn p) => LifeChecks.Illness(p) != null || LifeChecks.WoundSeverity(p) >= LifeChecks.HurtFrom;

        private static Look Of(TalkScene s)
        {
            if (s?.map == null) return null;
            if (Current.Game != game)
            {
                game = Current.Game;
                looks.Clear();
            }
            int now = Find.TickManager.TicksGame;
            if (!looks.TryGetValue(s.map.uniqueID, out var l))
            {
                if (looks.Count > 8) looks.Clear();
                l = new Look();
                looks[s.map.uniqueID] = l;
            }
            if (now - l.tick < GenDate.TicksPerHour && now >= l.tick) return l;
            l.tick = now;
            l.sick.Clear();
            l.serious = l.doctor = null;
            int best = 3;
            foreach (var p in s.colonists)
            {
                if (p == null || p.Dead || p.DevelopmentalStage.Baby()) continue;
                if (Sick(p))
                {
                    l.sick.Add(p);
                    if (l.serious == null && Serious(p)) l.serious = p;
                }
                var med = p.skills?.GetSkill(SkillDefOf.Medicine);
                if (med != null && !med.TotallyDisabled && med.Level > best)
                {
                    best = med.Level;
                    l.doctor = p;
                }
            }
            return l;
        }

        public override float Relevance(Pawn a, Pawn b, TalkScene s)
        {
            var l = Of(s);
            if (l == null || l.sick.Count == 0) return 0f;
            return def.relevance + (l.serious != null ? 0.3f : 0f);
        }

        public override float Hot(TalkScene s)
        {
            var l = Of(s);
            return l?.serious != null ? Mathf.Max(base.Hot(s), 0.7f) : base.Hot(s);
        }

        /// <summary>The worst off of the sick who is neither of the two (the seriously ill first), or nobody in particular.</summary>
        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s)
        {
            var l = Of(s);
            if (l == null) return null;
            if (l.serious != null && l.serious != a && l.serious != b) return TalkSubject.Of(l.serious);
            foreach (var p in l.sick)
                if (p != a && p != b)
                    return TalkSubject.Of(p);
            return null;
        }

        /// <summary>Nobody teases anyone about it while something that can kill is going round.</summary>
        public override bool NoTease(TalkScene s) => Of(s)?.serious != null || base.NoTease(s);

        /// <summary>The sick want the bed; a doctor wants their patients in it.</summary>
        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            if (p == null) return 0f;
            float x = Sick(p) ? 0.25f : 0f;
            if (Of(s)?.doctor == p) x += 0.15f;
            return x;
        }

        /// <summary>
        /// [ill] or [wounded]: the speaker is ill, or hurt and still healing; [otherill], [otherwounded]: the other is;
        /// [serious]: someone here has something that can kill, [subjectserious]: the one talked of; [doctor]: the speaker
        /// doctors best here.
        /// </summary>
        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            var l = Of(s);
            if (l == null) return;
            if (speaker != null && LifeChecks.Illness(speaker) != null) tags.Add("ill");
            else if (speaker != null && l.sick.Contains(speaker)) tags.Add("wounded");
            if (other != null && LifeChecks.Illness(other) != null) tags.Add("otherill");
            else if (other != null && l.sick.Contains(other)) tags.Add("otherwounded");
            if (l.serious != null) tags.Add("serious");
            if (l.serious != null && subject?.pawn == l.serious) tags.Add("subjectserious");
            if (speaker != null && l.doctor == speaker) tags.Add("doctor");
        }
    }
}
