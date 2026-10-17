using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The topics a colony talks about (Defs/TopicDefs/Rimfolk_Topics.xml). Each worker says when its topic comes up,
    // what it's about, what someone's life now adds to their view, and the tags its lines can be written for. The views
    // themselves start from character, traits, passions and faith (the TopicDef's seed and leans).

    /// <summary>Helpers the topics share: notes, feelings, memories of the game's own thoughts.</summary>
    internal static class TopicUtil
    {
        public static TalkNote Latest(TopicDef def, float days) => TalkTracker.Instance?.Latest(def.defName, days);

        /// <summary>The note's tags (comma-separated) into the line's tags.</summary>
        public static void AddNoteTags(TalkNote n, HashSet<string> tags)
        {
            if (n?.tag == null) return;
            foreach (var t in n.tag.Split(','))
                if (!t.NullOrEmpty()) tags.Add(t.Trim());
        }

        public static float Feeling(Pawn p, string key)
        {
            var f = ArcUtil.Feeling(p, key);
            return f?.Share ?? 0f;
        }

        public static bool Remembers(Pawn p, string thought, float days = 2f) => LifeConditions.Remembered(p, new List<string> { thought }, days) != null;

        public static bool Remembers(Pawn p, IEnumerable<string> thoughts, float days = 2f) => LifeConditions.Remembered(p, thoughts.ToList(), days) != null;

        public static bool Other(Pawn x, Pawn a, Pawn b) => x != null && x != a && x != b && !x.Dead;

        public static float Mood(Pawn p) => p.needs?.mood?.CurLevelPercentage ?? 0.5f;

        public static bool HasLover(Pawn p) =>
            p.relations != null && p.relations.DirectRelations.Any(r => r.otherPawn != null && !r.otherPawn.Dead
                && (r.def == PawnRelationDefOf.Lover || r.def == PawnRelationDefOf.Spouse || r.def == PawnRelationDefOf.Fiance));
    }

    /// <summary>The last raid or fight: fight back (for) or wall up and keep out of it (against).</summary>
    public class TopicWorker_Raid : TopicWorker
    {
        public override float Relevance(Pawn a, Pawn b, TalkScene s) => s.daysSinceThreat < 900f || TopicUtil.Latest(def, 30f) != null ? def.relevance : def.relevance * 0.35f;

        public override float Hot(TalkScene s)
        {
            float h = base.Hot(s);
            if (s.daysSinceThreat < 2f) h = Mathf.Max(h, 1f - s.daysSinceThreat / 2f);
            return h;
        }

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => TalkSubject.Named(TopicUtil.Latest(def, 20f)?.subject);

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            float x = 0f;
            var v = CouncilGameComponent.Instance?.vendettas;
            if (v != null && v.vendettas.Any(d => d.avenger == p && !d.settled)) x += 0.25f;
            x -= TopicUtil.Feeling(p, "Fear") * 0.4f;
            x += TopicUtil.Feeling(p, "Anger") * 0.3f;
            if (p.health != null && p.health.summaryHealth.SummaryHealthPercent < 0.8f) x -= 0.1f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            var n = TopicUtil.Latest(def, 20f);
            TopicUtil.AddNoteTags(n, tags);
            if (n == null) tags.Add("noraid");
        }
    }

    /// <summary>The leader, the council and its decrees: trust in them (for) or not (against).</summary>
    public class TopicWorker_Council : TopicWorker
    {
        public override float Relevance(Pawn a, Pawn b, TalkScene s)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return 0f;
            float r = def.relevance * 0.6f;
            if (comp.decrees.active.Count > 0) r += 0.4f;
            if (s.leader != null) r += 0.2f;
            if (Find.TickManager.TicksGame - comp.ambitions.lastMeetingTick < GenDate.TicksPerDay * 10) r += 0.2f;
            return r;
        }

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s)
        {
            var decree = TopicUtil.Latest(def, 5f);
            if (decree?.tag == "decree" && !decree.subject.NullOrEmpty()) return TalkSubject.Named(decree.subject);
            return TopicUtil.Other(s.leader, a, b) ? TalkSubject.Of(s.leader) : null;
        }

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return 0f;
            float x;
            switch (comp.standings.LevelOf(p))
            {
                case TrustLevel.Devoted: x = 0.45f; break;
                case TrustLevel.Trusting: x = 0.2f; break;
                case TrustLevel.Doubtful: x = -0.25f; break;
                case TrustLevel.Bitter: x = -0.5f; break;
                default: x = 0f; break;
            }
            foreach (var d in comp.decrees.active)
            {
                if (d?.def == null) continue;
                switch (d.def.ReactionOf(p))
                {
                    case DecreeReaction.Love: x += 0.12f; break;
                    case DecreeReaction.Like: x += 0.06f; break;
                    case DecreeReaction.Dislike: x -= 0.08f; break;
                    case DecreeReaction.Hate: x -= 0.15f; break;
                }
            }
            if (s?.leader == p) x += 0.3f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            var n = TopicUtil.Latest(def, 5f);
            TopicUtil.AddNoteTags(n, tags);
            if (subject?.pawn != null && subject.pawn == s.leader) tags.Add("leader");
            if (s.leader == speaker) tags.Add("isleader");
            if (s.leader == other) tags.Add("toleader");
            if (n?.tag != "decree" && (CouncilGameComponent.Instance?.decrees.active.Count ?? 0) > 0) tags.Add("decrees");
        }
    }

    /// <summary>The food: it's fine, be grateful (for), or it's dreadful (against).</summary>
    public class TopicWorker_Food : TopicWorker
    {
        private static readonly string[] Fine = { "AteFineMeal", "VCE_AteFineGrill" };
        private static readonly string[] Lavish = { "AteLavishMeal", "VCE_AteLavishGrill", "VCE_AteGourmetGrill", "VCE_AteGourmetMeal" };

        public override float Hot(TalkScene s)
        {
            float h = base.Hot(s);
            if (s.foodDays < 3f) h = Mathf.Max(h, 0.8f);
            return h;
        }

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => TopicUtil.Other(s.bestCook, a, b) ? TalkSubject.Of(s.bestCook) : null;

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            float x = 0f;
            if (TopicUtil.Remembers(p, Lavish)) x += 0.3f;
            else if (TopicUtil.Remembers(p, Fine)) x += 0.15f;
            if (TopicUtil.Remembers(p, "AteRawFood")) x -= 0.25f;
            if (TopicUtil.Remembers(p, "AteNutrientPasteMeal")) x -= 0.12f;
            if (TopicUtil.Remembers(p, "AteWithoutTable")) x -= 0.1f;
            if ((p.needs?.food?.CurLevelPercentage ?? 1f) < 0.25f) x -= 0.2f;
            if (HediffDefOf.FoodPoisoning != null && p.health?.hediffSet?.HasHediff(HediffDefOf.FoodPoisoning) == true) x -= 0.3f;
            if (s != null && s.foodDays < 3f) x -= 0.15f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            TopicUtil.AddNoteTags(TopicUtil.Latest(def, 4f), tags);
            if (s.foodDays < 3f) tags.Add("lowfood");
            if (TopicUtil.Remembers(speaker, "AteNutrientPasteMeal")) tags.Add("paste");
            if (TopicUtil.Remembers(speaker, "AteRawFood")) tags.Add("raw");
            if (TopicUtil.Remembers(speaker, Lavish) || TopicUtil.Remembers(speaker, Fine)) tags.Add("finemeal");
            if (s.bestCook == speaker) tags.Add("iscook");
            if (s.bestCook == other) tags.Add("tocook");
        }
    }

    /// <summary>The work and who does too little: everyone pulls their weight (for), or some don't, and it's too much (against).</summary>
    public class TopicWorker_Work : TopicWorker
    {
        public override float Relevance(Pawn a, Pawn b, TalkScene s) => def.relevance + (s.slacker != null ? 0.3f : 0f);

        public override float Hot(TalkScene s) => Mathf.Max(base.Hot(s), s.strain >= 40f ? 0.5f : 0f);

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => TopicUtil.Other(s.slacker, a, b) ? TalkSubject.Of(s.slacker) : null;

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            float x = 0f;
            float strain = Heart.StrainOf(p);
            if (strain >= 50f) x -= 0.25f;
            else if (strain >= 30f) x -= 0.1f;
            if (s?.slacker != null && s.slacker != p)
            {
                var ind = TalkScene.Industriousness;
                if (ind != null && (p.story?.traits?.DegreeOfTrait(ind) ?? 0) > 0) x -= 0.2f;
            }
            if (s?.slacker == p) x += 0.2f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            if (subject != null) tags.Add("slacker");
            if (Heart.StrainOf(speaker) >= 40f) tags.Add("strained");
            if (s.slacker == other) tags.Add("toslacker");
        }
    }

    /// <summary>A newcomer, one view per newcomer: welcome them (for), or wait and see (against).</summary>
    public class TopicWorker_Newcomer : TopicWorker
    {
        public override float Relevance(Pawn a, Pawn b, TalkScene s) => s.Newcomer(a, b) != null ? def.relevance : 0f;

        public override float Hot(TalkScene s)
        {
            float days = s.newcomers.Count > 0 ? s.newcomers[0].days : 99f;
            return Mathf.Max(base.Hot(s), days < 3f ? 1f - days / 3f : 0f);
        }

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => TalkSubject.Of(s.Newcomer(a, b));

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            var n = subject?.pawn;
            if (n == null || n == p) return 0f;
            float x = (p.relations?.OpinionOf(n) ?? 0) / 100f * 0.7f;
            x += (Persona.Likeness(p, n) - 0.5f) * 0.6f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            if (subject?.pawn == null) return;
            if (s.NewcomerDays(subject.pawn) < 2f) tags.Add("arrived");
            if ((speaker.relations?.OpinionOf(subject.pawn) ?? 0) >= 25) tags.Add("likesthem");
            else if ((speaker.relations?.OpinionOf(subject.pawn) ?? 0) <= -15) tags.Add("dislikesthem");
            if (subject.pawn.DevelopmentalStage.Juvenile()) tags.Add("young");
        }
    }

    /// <summary>Faith: it matters and they hold to it (for), or they doubt it (against). With Ideology, theirs against another's.</summary>
    public class TopicWorker_Faith : TopicWorker
    {
        public override float Relevance(Pawn a, Pawn b, TalkScene s)
        {
            if (!ModsConfig.IdeologyActive || a.Ideo == null || b.Ideo == null) return def.relevance * 0.35f;
            return a.Ideo != b.Ideo ? def.relevance * 1.4f : def.relevance;
        }

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) =>
            ModsConfig.IdeologyActive && a.Ideo != null && !a.Ideo.name.NullOrEmpty() ? TalkSubject.Named(a.Ideo.name) : null;

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            if (!ModsConfig.IdeologyActive || p.ideo == null) return 0f;
            return (p.ideo.Certainty - 0.5f) * 0.9f;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            if (!ModsConfig.IdeologyActive || speaker.Ideo == null || other.Ideo == null)
            {
                tags.Add("noideo");
                return;
            }
            tags.Add(speaker.Ideo == other.Ideo ? "sameideo" : "otherideo");
            float c = speaker.ideo?.Certainty ?? 0.5f;
            if (c >= 0.8f) tags.Add("devout");
            else if (c <= 0.3f) tags.Add("doubting");
        }
    }

    /// <summary>The world outside, traders and other peoples: open to them (for), or keep to ourselves (against).</summary>
    public class TopicWorker_Outside : TopicWorker
    {
        public override float Hot(TalkScene s) => Mathf.Max(base.Hot(s), s.traders ? 0.7f : 0f);

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s)
        {
            var trader = s.map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.TraderKind != null && p.Faction != null && p.Faction != Faction.OfPlayer && !p.HostileTo(Faction.OfPlayer));
            return trader?.Faction != null ? TalkSubject.Named(trader.Faction.Name) : null;
        }

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            float x = 0f;
            if (s != null && s.daysSinceThreat < 5f) x -= 0.15f;
            if (s != null && s.traders) x += 0.1f;
            if (TopicUtil.Feeling(p, "Homesick") > 0.3f) x += 0.1f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            TopicUtil.AddNoteTags(TopicUtil.Latest(def, 3f), tags);
            if (s.traders) tags.Add("traders");
        }
    }

    /// <summary>Animals: part of the family (for), or livestock and trouble (against).</summary>
    public class TopicWorker_Animals : TopicWorker
    {
        private static Pawn Bonded(Pawn p) => p.relations?.DirectRelations.FirstOrDefault(r => r.def == PawnRelationDefOf.Bond && r.otherPawn != null && !r.otherPawn.Dead)?.otherPawn;

        public override float Relevance(Pawn a, Pawn b, TalkScene s)
        {
            if (s.tameAnimals == 0) return def.relevance * 0.3f;
            return def.relevance + (Bonded(a) != null || Bonded(b) != null ? 0.2f : 0f);
        }

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s)
        {
            var pet = Bonded(a);
            return pet != null ? TalkSubject.Named(pet.LabelShort) : null;
        }

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s) => Bonded(p) != null ? 0.3f : 0f;

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            if (Bonded(speaker) != null) tags.Add("bonded");
            if (s.tameAnimals == 0) tags.Add("noanimals");
        }
    }

    /// <summary>The weather and the seasons: take it as it comes (for), or dread and hate it (against).</summary>
    public class TopicWorker_Weather : TopicWorker
    {
        public override float Hot(TalkScene s)
        {
            float h = base.Hot(s);
            if (s.disaster != null) h = Mathf.Max(h, 1f);
            if (s.storm || s.cold || s.hot) h = Mathf.Max(h, 0.5f);
            return h;
        }

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => TalkSubject.Named(s.disaster);

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            float x = 0f;
            if (s?.disaster != null) x -= 0.2f;
            if (s != null && (s.cold || s.hot)) x -= 0.1f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            if (s.disaster != null) tags.Add("disaster");
        }
    }

    /// <summary>Art and beauty: worth the time (for), or a waste of good stone (against).</summary>
    public class TopicWorker_Art : TopicWorker
    {
        public override float Relevance(Pawn a, Pawn b, TalkScene s) => def.relevance + (s.bestArtist != null ? 0.3f : 0f);

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => TopicUtil.Other(s.bestArtist, a, b) ? TalkSubject.Of(s.bestArtist) : null;

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            var beauty = p.needs?.beauty;
            return beauty != null && beauty.CurLevelPercentage < 0.3f ? 0.1f : 0f;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            if (s.bestArtist == speaker) tags.Add("isartist");
            if (s.bestArtist == other) tags.Add("toartist");
        }
    }

    /// <summary>The future, and leaving: there's a future here (for), or it won't last and they want out (against).</summary>
    public class TopicWorker_Future : TopicWorker
    {
        public override float Hot(TalkScene s)
        {
            float h = base.Hot(s);
            if (s.disaster != null) h = Mathf.Max(h, 0.5f);
            if (TalkTracker.Instance?.Latest("Rimfolk_Topic_Dead", 3f) != null) h = Mathf.Max(h, 0.5f);
            return h;
        }

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            float x = (TopicUtil.Mood(p) - 0.5f) * 0.8f;
            x += TopicUtil.Feeling(p, "Hope") * 0.3f;
            x -= TopicUtil.Feeling(p, "Despair") * 0.4f;
            if (TalkTracker.Instance?.Latest("Rimfolk_Topic_Dead", 10f) != null) x -= 0.1f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            if (TalkTracker.Instance?.Latest("Rimfolk_Topic_Dead", 10f) != null) tags.Add("loss");
            if (TopicUtil.Feeling(speaker, "Homesick") > 0.3f) tags.Add("homesick");
        }
    }

    /// <summary>The dead: keep their memory, talk of them (for), or let them rest and move on (against).</summary>
    public class TopicWorker_Dead : TopicWorker
    {
        private static bool KnewThem(Pawn p, TalkNote n)
        {
            int since = Find.TickManager.TicksGame - n.tick;
            return (p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0) > since;
        }

        private TalkNote Death(Pawn a, Pawn b)
        {
            foreach (var n in TalkTracker.Instance?.Recent(def.defName, 60f) ?? Enumerable.Empty<TalkNote>())
                if (!n.subject.NullOrEmpty() && KnewThem(a, n) && KnewThem(b, n))
                    return n;
            return null;
        }

        public override float Relevance(Pawn a, Pawn b, TalkScene s) => Death(a, b) != null ? def.relevance : 0f;

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s)
        {
            var n = Death(a, b);
            return n == null ? null : new TalkSubject { name = n.subject, id = n.subjectId };
        }

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s) => TopicUtil.Feeling(p, "Grief") * 0.4f;

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            var n = TalkTracker.Instance?.Recent(def.defName, 60f).FirstOrDefault(x => x.subject == subject?.name);
            TopicUtil.AddNoteTags(n, tags);
            if (n != null && Find.TickManager.TicksGame - n.tick < GenDate.TicksPerDay * 5) tags.Add("fresh");
        }
    }

    /// <summary>Prisoners and slaves: treat them kindly (for), or harshly (against).</summary>
    public class TopicWorker_Prisoners : TopicWorker
    {
        public override float Relevance(Pawn a, Pawn b, TalkScene s) => s.prisoners.Count + s.slaves.Count > 0 ? def.relevance : 0f;

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s)
        {
            var newest = TopicUtil.Latest(def, 10f);
            var p = s.prisoners.Concat(s.slaves).FirstOrDefault(x => x.thingIDNumber == newest?.subjectId)
                    ?? s.prisoners.FirstOrDefault() ?? s.slaves.FirstOrDefault();
            return p != null && p != a && p != b ? TalkSubject.Of(p) : null;
        }

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s) => CharacterTracker.Affinity(p, Way.Mercy) * 0.3f;

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            if (subject?.pawn != null) tags.Add(subject.pawn.IsSlave ? "slave" : "prisoner");
            if (s.slaves.Count > 0) tags.Add("slaves");
            if (s.prisoners.Count > 0) tags.Add("prisoners");
        }
    }

    /// <summary>Drink and drugs: a drink never hurt anyone (for), or they ruin people (against).</summary>
    public class TopicWorker_Drugs : TopicWorker
    {
        private static bool Addicted(Pawn p) => p.health?.hediffSet?.hediffs.Any(h => h is Hediff_Addiction) == true;

        public override float Relevance(Pawn a, Pawn b, TalkScene s)
        {
            float r = def.relevance;
            if (TopicUtil.Latest(def, 10f) != null || s.colonists.Any(Addicted)) r += 0.4f;
            return r;
        }

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s) => Addicted(p) ? 0.25f : 0f;

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            TopicUtil.AddNoteTags(TopicUtil.Latest(def, 5f), tags);
            if (Addicted(speaker)) tags.Add("addicted");
        }
    }

    /// <summary>Technology and the old ways: progress (for), or the old ways (against).</summary>
    public class TopicWorker_Tech : TopicWorker
    {
        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => TalkSubject.Named(TopicUtil.Latest(def, 10f)?.subject);

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            TopicUtil.AddNoteTags(TopicUtil.Latest(def, 4f), tags);
        }
    }

    /// <summary>Children: a good place to raise them (for), or no place for them (against).</summary>
    public class TopicWorker_Children : TopicWorker
    {
        private static bool Parent(Pawn p, TalkScene s) => s.children.Any(c => c.GetMother() == p || c.GetFather() == p);

        public override float Relevance(Pawn a, Pawn b, TalkScene s)
        {
            if (s.children.Count > 0) return def.relevance;
            if (s.colonists.Any(p => p.health?.hediffSet?.hediffs.Any(h => h.def.defName.StartsWith("Pregnan")) == true)) return def.relevance * 0.8f;
            return ModsConfig.BiotechActive ? def.relevance * 0.3f : 0f;
        }

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s)
        {
            var born = TopicUtil.Latest(def, 10f);
            var child = s.children.FirstOrDefault(c => c.thingIDNumber == born?.subjectId) ?? s.children.FirstOrDefault();
            return child != null && child != a && child != b ? TalkSubject.Of(child) : null;
        }

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s) => s != null && Parent(p, s) ? 0.3f : 0f;

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            TopicUtil.AddNoteTags(TopicUtil.Latest(def, 5f), tags);
            if (Parent(speaker, s)) tags.Add("parent");
            if (s.children.Count == 0) tags.Add("nochildren");
        }
    }

    /// <summary>The colony's project (the council's ambition): worth it (for), or a waste of effort (against).</summary>
    public class TopicWorker_Project : TopicWorker
    {
        public override float Relevance(Pawn a, Pawn b, TalkScene s) => s.ambition != null ? def.relevance : 0f;

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => s.ambition?.def != null ? TalkSubject.Named(s.ambition.def.label) : null;

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            if (s?.ambition == null) return 0f;
            float x = s.ambition.proposer == p ? 0.4f : 0f;
            var level = CouncilGameComponent.Instance?.standings?.LevelOf(p) ?? TrustLevel.Neutral;
            if (level >= TrustLevel.Trusting) x += 0.1f;
            else if (level <= TrustLevel.Doubtful) x -= 0.15f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            if (s.ambition == null) return;
            if (s.ambition.proposer == speaker) tags.Add("proposer");
            if (s.ambition.proposer == other) tags.Add("toproposer");
            if (s.ambition.deadlineTick > 0 && s.ambition.deadlineTick - Find.TickManager.TicksGame < GenDate.TicksPerDay * 2) tags.Add("deadline");
        }
    }

    /// <summary>Money and wealth: silver matters (for), or it doesn't buy what counts (against).</summary>
    public class TopicWorker_Wealth : TopicWorker
    {
    }

    /// <summary>Love and pairings: all for it (for), or wary of it (against).</summary>
    public class TopicWorker_Love : TopicWorker
    {
        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => TalkSubject.Named(TopicUtil.Latest(def, 8f)?.subject);

        public override float Experience(Pawn p, TalkSubject subject, TalkScene s)
        {
            float x = TopicUtil.HasLover(p) ? 0.25f : 0f;
            x -= TopicUtil.Feeling(p, "Loneliness") * 0.3f;
            x += TopicUtil.Feeling(p, "Love") * 0.3f;
            return x;
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            TopicUtil.AddNoteTags(TopicUtil.Latest(def, 8f), tags);
            if (TopicUtil.HasLover(speaker)) tags.Add("inlove");
            else tags.Add("single");
        }
    }
}
