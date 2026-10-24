using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Topics from what the colony lives with now (Defs/TopicDefs/Rimfolk_Topics_Life.xml): a day on the calendar coming
    // or just kept, the world's newest news, the camp on the map, and growing up here (a child's talk). Each comes up only
    // while there is such a thing; the scene (TalkScene) finds it once an hour, so these only read what it holds.

    /// <summary>A day on the colony calendar, coming, today or just kept: worth keeping (for), or a working day lost (against).</summary>
    public class TopicWorker_Calendar : TopicWorker
    {
        private static readonly string[] WayTags = BuildWayTags();

        private static string[] BuildWayTags()
        {
            var names = Enum.GetNames(typeof(DayWay));
            var tags = new string[names.Length];
            for (int i = 0; i < names.Length; i++) tags[i] = "way:" + names[i].ToLowerInvariant();
            return tags;
        }

        /// <summary>A day of mourning: the dead's day, a companion's, or any day kept as a remembrance.</summary>
        public static bool Sombre(CalendarDay d) => d != null && (d.way == DayWay.Remembrance || d.kind == CalendarKind.Death || d.kind == CalendarKind.Companion);

        public override float Relevance(Pawn a, Pawn b, TalkScene s) => s.calendarDay != null ? def.relevance : 0f;

        public override float Hot(TalkScene s)
        {
            if (s.calendarDay == null) return 0f;
            float h = s.calendarWhen == 0 ? 1f : Math.Abs(s.calendarWhen) == 1 ? 0.6f : 0.3f;
            return Mathf.Max(base.Hot(s), h);
        }

        /// <summary>The day by its name, unless it's one of the two talking's own (their lines say "my birthday", "your day").</summary>
        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s)
        {
            var d = s.calendarDay;
            if (d == null || Of(d, a) || Of(d, b)) return null;
            return TalkSubject.Named(d.label);
        }

        private static bool Of(CalendarDay d, Pawn p) => p != null && (d.pawnId != null || d.otherId != null) && (d.pawnId == p.ThingID || d.otherId == p.ThingID);

        public override bool NoTease(TalkScene s) => def.noTease || Sombre(s?.calendarDay);

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            var d = s?.calendarDay;
            if (d == null || p == null) return 0f;
            float x = 0f;
            if (Of(d, p)) x += 0.3f;
            else if (d.knew != null && d.knew.Contains(p.ThingID)) x += 0.1f;
            // The worn out are glad of a day off.
            if (Heart.StrainOf(p) >= 50f) x += 0.1f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            var d = s.calendarDay;
            if (d == null) return;
            tags.Add(s.calendarWhen > 0 ? "coming" : s.calendarWhen == 0 ? "today" : "kept");
            if (s.calendarWhen == 1) tags.Add("tomorrow");
            int way = (int)d.way;
            if (way >= 0 && way < WayTags.Length) tags.Add(WayTags[way]);
            if (d.tradition) tags.Add("tradition");
            if (Sombre(d)) tags.Add("sombre");
            switch (d.kind)
            {
                case CalendarKind.Birthday: tags.Add("birthday"); break;
                case CalendarKind.Wedding: tags.Add("wedding"); break;
                case CalendarKind.Founding: tags.Add("founding"); break;
                case CalendarKind.Battle: tags.Add("battle"); break;
            }
            if (Of(d, speaker)) tags.Add("myday");
            else if (Of(d, other)) tags.Add("yourday");
        }
    }

    /// <summary>The world's newest news (a world situation): help the neighbors (for), or it's none of ours (against).</summary>
    public class TopicWorker_World : TopicWorker
    {
        private static readonly Dictionary<string, string> groups = new Dictionary<string, string>();

        /// <summary>A situation's kind as a broader kind of news its lines are written for ("news:war", "news:hunger"...), or null.</summary>
        public static string Group(string kind)
        {
            if (kind.NullOrEmpty()) return null;
            if (groups.TryGetValue(kind, out var g)) return g;
            switch (kind)
            {
                case "war":
                case "feud":
                case "sellswords":
                case "roads":
                case "levy":
                    g = "news:war";
                    break;
                case "hunger":
                case "drought":
                case "scarcity":
                case "weather":
                case "water":
                case "fire":
                    g = "news:hunger";
                    break;
                case "sickness":
                case "healer":
                    g = "news:sickness";
                    break;
                case "trade":
                case "fair":
                case "rush":
                case "drove":
                case "road":
                case "toll":
                case "town":
                case "harvest":
                    g = "news:trade";
                    break;
                case "faith":
                case "creed":
                case "songs":
                case "mourning":
                    g = "news:faith";
                    break;
                case "court":
                case "peace":
                    g = "news:court";
                    break;
                case "sky":
                case "comet":
                case "wreck":
                case "herds":
                    g = "news:wonder";
                    break;
                default:
                    g = "news:people";
                    break;
            }
            if (groups.Count < 200) groups[kind] = g;
            return g;
        }

        public override float Relevance(Pawn a, Pawn b, TalkScene s) => s.news != null ? def.relevance : 0f;

        public override float Hot(TalkScene s)
        {
            if (s.news == null) return 0f;
            float days = s.news.DaysIn;
            return Mathf.Max(base.Hot(s), days < 2f ? 1f - days / 2f : 0f);
        }

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => TalkSubject.Named(s.news?.def?.label);

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            float x = TopicUtil.Feeling(p, "Homesick") * 0.15f - TopicUtil.Feeling(p, "Fear") * 0.2f;
            if (s?.news?.sufferers != null && s.news.sufferers.Contains(p)) x -= 0.1f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            var n = s.news;
            if (n?.def == null) return;
            tags.Add(n.def.bad ? "badnews" : "goodnews");
            if (n.help > 0) tags.Add("helped");
            string g = Group(n.def.kind);
            if (g != null) tags.Add(g);
            if (n.DaysIn < 2f) tags.Add("fresh");
        }
    }

    /// <summary>The camp on the map: glad of the neighbors (for), or wants them gone and the land back (against).</summary>
    public class TopicWorker_Camp : TopicWorker
    {
        private static readonly Dictionary<CampKindDef, string[]> kindTags = new Dictionary<CampKindDef, string[]>();

        /// <summary>"camp:Hunters" and the broader kinds it belongs to (faithful, healers, music, mourners), once per kind.</summary>
        private static string[] KindTags(CampKindDef k)
        {
            if (k == null) return null;
            if (kindTags.TryGetValue(k, out var t)) return t;
            string shortName = k.defName.StartsWith("Council_Camp_", StringComparison.Ordinal) ? k.defName.Substring("Council_Camp_".Length) : k.defName;
            var list = new List<string> { "camp:" + shortName };
            switch (shortName)
            {
                case "Pilgrims":
                case "Procession":
                case "Penitents":
                case "TreeSingers":
                    list.Add("faithful");
                    break;
                case "Healers":
                case "LoneHealer":
                    list.Add("healers");
                    break;
                case "Players":
                case "Minstrels":
                    list.Add("music");
                    break;
                case "Mourners":
                    list.Add("mourners");
                    break;
            }
            t = list.ToArray();
            if (kindTags.Count < 200) kindTags[k] = t;
            return t;
        }

        private static int Now => Find.TickManager.TicksGame;

        public override float Relevance(Pawn a, Pawn b, TalkScene s) => s.camp != null ? def.relevance : 0f;

        public override float Hot(TalkScene s)
        {
            var c = s.camp;
            if (c == null) return 0f;
            float h = base.Hot(s);
            if (c.phase == CampPhase.Coming) h = Mathf.Max(h, 0.8f);
            else if (c.DaysHere < 2f) h = Mathf.Max(h, 1f - c.DaysHere / 2f);
            if (c.tension >= 40f) h = Mathf.Max(h, 0.6f);
            return h;
        }

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => TalkSubject.Named(s.campName);

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            var c = s?.camp;
            if (c == null || p == null) return 0f;
            float x = -c.tension / 100f * 0.35f;
            if (c.warmth != null && c.warmth.TryGetValue(p, out int w)) x += Mathf.Min(w, 6) * 0.04f;
            if (c.treesFelled + c.animalsHunted * 3 >= 12) x -= 0.1f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            var c = s.camp;
            if (c?.kind == null) return;
            var kt = KindTags(c.kind);
            if (kt != null)
                foreach (var t in kt)
                    tags.Add(t);
            if (c.phase == CampPhase.Coming) tags.Add("coming");
            if (c.tension >= 40f) tags.Add("tense");
            if (c.AnyTerms) tags.Add("terms");
            if (c.kind.trader) tags.Add("trader");
            if (c.kind.family) tags.Add("campfamily");
            if (c.animalsHunted > 0 || c.kind.huntsPerDay >= 0.5f) tags.Add("hunting");
            if (c.treesFelled > 0) tags.Add("felling");
            if (c.untilTick != int.MaxValue && c.untilTick - Now < GenDate.TicksPerDay) tags.Add("leaving");
            if (speaker != null && c.warmth != null && c.warmth.TryGetValue(speaker, out int w) && w >= 2) tags.Add("knowsthem");
        }
    }

    /// <summary>
    /// Growing up here, a child's talk: let a child be a child (for), or children here have to grow up fast (against).
    /// Only between a child and whoever they're with (<see cref="TopicDef.forChildren"/>).
    /// </summary>
    public class TopicWorker_Growing : TopicWorker
    {
        private static bool ParentOf(Pawn parent, Pawn child) => parent != null && child != null && (child.GetMother() == parent || child.GetFather() == parent);

        public override float Relevance(Pawn a, Pawn b, TalkScene s) => Talk.IsChild(a) || Talk.IsChild(b) ? def.relevance : 0f;

        /// <summary>Another child of the colony, a playmate to talk about (not a baby, not one of the two).</summary>
        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s)
        {
            foreach (var c in s.children)
                if (c != a && c != b && !c.Dead && c.DevelopmentalStage.Child())
                    return TalkSubject.Of(c);
            return null;
        }

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            if (p == null) return 0f;
            // A happy child is glad to be one; an unhappy one wants to be grown.
            if (Talk.IsChild(p)) return (TopicUtil.Mood(p) - 0.5f) * 0.6f;
            float x = 0f;
            if (s != null)
                foreach (var c in s.children)
                    if (ParentOf(p, c))
                    {
                        x += 0.15f;
                        break;
                    }
            if (Heart.StrainOf(p) >= 50f) x -= 0.15f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            bool kid = Talk.IsChild(speaker), otherKid = Talk.IsChild(other);
            if (otherKid) tags.Add("tochild");
            if (kid && otherKid) tags.Add("kids");
            if (kid && ParentOf(other, speaker)) tags.Add("toparent");
            if (otherKid && ParentOf(speaker, other)) tags.Add("tomychild");
            if (kid && (speaker.ageTracker?.AgeBiologicalYears ?? 10) < 7) tags.Add("little");
        }
    }
}
