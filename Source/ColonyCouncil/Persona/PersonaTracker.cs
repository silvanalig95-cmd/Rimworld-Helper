using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Keeps what Rimfolk knows of people beyond the game's own traits (<see cref="PawnPersona"/>) and what pairs of them
    /// went through together (<see cref="SharedMemory"/>), in a game component of its own so it has its own save data.
    /// Nothing here ticks: drift fades when it's read, and the records of people the game has let go are dropped when
    /// the game saves. The systems that use it go through <see cref="Persona"/> and <see cref="SharedMemories"/>.
    /// </summary>
    public class PersonaTracker : GameComponent
    {
        public Dictionary<int, PawnPersona> people = new Dictionary<int, PawnPersona>();
        public List<SharedMemory> memories = new List<SharedMemory>();

        /// <summary>Shared memories kept in all, and per pair.</summary>
        public const int MaxMemories = 900, MaxPerPair = 8;

        [Unsaved] private Dictionary<long, List<SharedMemory>> byPair;

        private static PersonaTracker cached;
        private static Game cachedGame;

        public static PersonaTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PersonaTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public PersonaTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            byPair = null;
            Persona.ResetCaches();
        }

        public List<SharedMemory> PairList(int a, int b, bool create)
        {
            if (byPair == null)
            {
                byPair = new Dictionary<long, List<SharedMemory>>();
                foreach (var m in memories) Index(m);
            }
            long key = PersonaMath.PairKey(a, b);
            if (!byPair.TryGetValue(key, out var list) && create)
            {
                list = new List<SharedMemory>();
                byPair[key] = list;
            }
            return list;
        }

        private void Index(SharedMemory m)
        {
            long key = PersonaMath.PairKey(m.a, m.b);
            if (!byPair.TryGetValue(key, out var list)) byPair[key] = list = new List<SharedMemory>();
            list.Add(m);
        }

        public void Forget(SharedMemory m)
        {
            memories.Remove(m);
            if (byPair != null && byPair.TryGetValue(PersonaMath.PairKey(m.a, m.b), out var list)) list.Remove(m);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    Prune();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("persona clean-up before saving", e);
                }
            }
            var list = people.Values.ToList();
            Scribe_Collections.Look(ref list, "people", LookMode.Deep);
            Scribe_Collections.Look(ref memories, "memories", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                people = new Dictionary<int, PawnPersona>();
                if (list != null)
                    foreach (var p in list)
                        if (p != null) people[p.id] = p;
                memories ??= new List<SharedMemory>();
                memories.RemoveAll(m => m?.def == null);
                byPair = null;
            }
        }

        /// <summary>Drops the records of people the game no longer knows, marks past their keeping, and faded memories.</summary>
        public void Prune()
        {
            var known = CouncilGameComponent.KnownPawnNumbers();
            int now = Find.TickManager.TicksGame;
            foreach (var id in people.Keys.Where(id => !known.Contains(id)).ToList()) people.Remove(id);
            foreach (var p in people.Values) p.PruneMarks(now);
            foreach (var id in people.Where(kv => kv.Value.Empty).Select(kv => kv.Key).ToList()) people.Remove(id);
            memories.RemoveAll(m => !known.Contains(m.a) || !known.Contains(m.b) || m.WeightNow(now) < 0.05f);
            if (memories.Count > MaxMemories)
                memories = memories.OrderByDescending(m => m.WeightNow(now)).Take(MaxMemories).ToList();
            byPair = null;
        }
    }

    /// <summary>
    /// A person as Rimfolk sees them: their nature from the game's traits, skills and age (<see cref="PersonaNatureDef"/>),
    /// moved by what has happened to them (<see cref="PersonaMarkDef"/>), and where they stand on things. Everything that
    /// talks, gossips, joins a group or picks a tone reads people from here.
    /// </summary>
    public static class Persona
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The "Personalities and social life" setting.</summary>
        public static bool On => S == null || S.socialLife;

        public static PersonaTracker Tracker => PersonaTracker.Instance;

        /// <summary>Someone with a personality worth keeping: a person with a story (not an animal or a mech).</summary>
        public static bool Eligible(Pawn p) => p != null && p.RaceProps != null && p.RaceProps.Humanlike && p.story != null;

        /// <summary>Records are kept for the colony's own people (colonists, prisoners, slaves); others have only their nature.</summary>
        public static bool Keeps(Pawn p) =>
            Eligible(p) && (p.Faction == Faction.OfPlayer || p.IsPrisonerOfColony || p.IsSlaveOfColony || p.HostFaction == Faction.OfPlayer);

        // ---------------- Nature ----------------

        private static readonly Dictionary<int, (int tick, float[] v)> natureCache = new Dictionary<int, (int, float[])>();

        public static void ResetCaches() => natureCache.Clear();

        /// <summary>Where a person starts on each side, before anything happened to them. Worked out at most once an hour.</summary>
        public static float[] Nature(Pawn p)
        {
            if (!Eligible(p)) return new float[PersonaMath.Axes];
            int now = Find.TickManager?.TicksGame ?? 0;
            if (natureCache.TryGetValue(p.thingIDNumber, out var c) && now - c.tick >= 0 && now - c.tick < GenDate.TicksPerHour) return c.v;
            var v = new float[PersonaMath.Axes];
            try
            {
                NatureInto(p, v);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona nature", e);
            }
            if (natureCache.Count > 600) natureCache.Clear();
            natureCache[p.thingIDNumber] = (now, v);
            return v;
        }

        private static void NatureInto(Pawn p, float[] v)
        {
            foreach (var a in PersonaMath.All) v[(int)a] = PersonaMath.Seed(p.thingIDNumber, a, 12f);
            var traits = p.story?.traits;
            if (traits != null)
            {
                foreach (var def in PersonaNatureDef.All)
                {
                    if (def.traits == null) continue;
                    foreach (var e in def.traits)
                    {
                        var td = e?.Def;
                        if (td == null) continue;
                        var t = traits.GetTrait(td);
                        if (t == null || t.Degree != e.degree) continue;
                        foreach (var a in PersonaMath.All) v[(int)a] += e.Of(a);
                    }
                }
            }
            // Ease with people shows in warmth and humour.
            var social = p.skills?.GetSkill(SkillDefOf.Social);
            if (social != null && !social.TotallyDisabled)
            {
                v[(int)PersonaAxis.Warmth] += (social.Level - 6) * 1.5f + (social.passion != Passion.None ? 5f : 0f);
                v[(int)PersonaAxis.Humour] += (social.Level - 6) * 1.2f;
            }
            // The young are open and trusting; the old have seen enough.
            float years = p.ageTracker?.AgeBiologicalYearsFloat ?? 30f;
            if (years < 13f)
            {
                v[(int)PersonaAxis.Openness] += 20f;
                v[(int)PersonaAxis.Trust] += 15f;
                v[(int)PersonaAxis.Humour] += 10f;
            }
            else if (years < 18f)
            {
                v[(int)PersonaAxis.Openness] += 10f;
                v[(int)PersonaAxis.Temper] += 5f;
            }
            else if (years >= 65f)
            {
                v[(int)PersonaAxis.Openness] -= 10f;
                v[(int)PersonaAxis.Temper] -= 5f;
                v[(int)PersonaAxis.Nerve] += 5f;
            }
            for (int i = 0; i < v.Length; i++) v[i] = PersonaMath.Clamp(v[i], PersonaMath.Max);
        }

        // ---------------- The person now ----------------

        /// <summary>The record kept for a person (null unless <paramref name="create"/> and they're one of the colony's people).</summary>
        public static PawnPersona Of(Pawn p, bool create = false)
        {
            var tr = Tracker;
            if (tr == null || p == null) return null;
            if (tr.people.TryGetValue(p.thingIDNumber, out var r))
            {
                if (r.name == null) r.name = p.LabelShort;
                return r;
            }
            if (!create || !Keeps(p)) return null;
            r = new PawnPersona { id = p.thingIDNumber, name = p.LabelShort, driftTick = Find.TickManager.TicksGame };
            tr.people[p.thingIDNumber] = r;
            return r;
        }

        /// <summary>Where a person is on one side now (nature, plus what happened to them when the setting is on).</summary>
        public static float Get(Pawn p, PersonaAxis axis) => Values(p)[(int)axis];

        /// <summary>All sides at once.</summary>
        public static float[] Values(Pawn p)
        {
            var nature = Nature(p);
            var v = new float[PersonaMath.Axes];
            var r = On ? Of(p) : null;
            if (r != null) r.Fade(Find.TickManager.TicksGame);
            for (int i = 0; i < v.Length; i++)
                v[i] = r == null ? nature[i] : PersonaMath.Total(nature[i], r.lasting[i], r.drift[i]);
            return v;
        }

        /// <summary>
        /// Something happened that changes a person: moves them by the mark (scaled), keeps the mark on them, and
        /// returns it (null when the setting is off or they aren't one of the colony's people).
        /// </summary>
        public static PersonaMark Shape(Pawn p, PersonaMarkDef def, Pawn other = null, string label = null, float scale = 1f)
        {
            if (!On || def == null || p == null) return null;
            var r = Of(p, true);
            if (r == null) return null;
            int now = Find.TickManager.TicksGame;
            r.Fade(now);
            float lastingShare = Mathf.Clamp01(def.lastingShare);
            foreach (var a in PersonaMath.All)
            {
                float amount = def.shifts.Of(a) * scale;
                if (amount == 0f) continue;
                int i = (int)a;
                r.lasting[i] = PersonaMath.Add(r.lasting[i], amount * lastingShare, PersonaMath.MaxLasting);
                r.drift[i] = PersonaMath.Add(r.drift[i], amount * (1f - lastingShare), PersonaMath.MaxDrift);
            }
            var mark = new PersonaMark
            {
                def = def,
                tick = now,
                other = other?.thingIDNumber ?? -1,
                otherName = other?.LabelShort,
                label = label ?? def.defaultLabel
            };
            r.marks.Add(mark);
            r.PruneMarks(now);
            return mark;
        }

        /// <summary>A small fading move with no mark (a talk that went well, a day of being teased).</summary>
        public static void Nudge(Pawn p, PersonaAxis axis, float amount)
        {
            if (!On || p == null || amount == 0f) return;
            var r = Of(p, true);
            if (r == null) return;
            r.Fade(Find.TickManager.TicksGame);
            r.drift[(int)axis] = PersonaMath.Add(r.drift[(int)axis], amount, PersonaMath.MaxDrift);
        }

        /// <summary>"What shaped them": a mark as the game says it.</summary>
        public static string Shaped(Pawn p, PersonaMark m)
        {
            if (m?.def == null || m.def.shaped.NullOrEmpty()) return null;
            return m.def.shaped.Formatted(p.Named("PAWN"), (m.otherName ?? "").Named("OTHER"), (m.label ?? "").Named("LABEL")).Resolve();
        }

        // ---------------- Words and tags ----------------

        /// <summary>The word for where someone is on a side ("warm", "quick-tempered"), or null if it doesn't show.</summary>
        public static string Word(PersonaAxis axis, float v)
        {
            int d = PersonaMath.Degree(v);
            if (d == 0) return null;
            return ("CC_Persona_" + axis + "_" + (d < 0 ? "M" : "P") + Mathf.Abs(d)).Translate();
        }

        /// <summary>"warm, quick to laugh and guarded": the sides that show most.</summary>
        public static string Words(Pawn p, int count = 3)
        {
            var v = Values(p);
            var words = PersonaMath.Strongest(v, count).Select(i => Word((PersonaAxis)i, v[i])).Where(w => w != null).ToList();
            return words.Count == 0 ? "CC_Persona_Even".Translate().ToString() : TextUtil.JoinAnd(words);
        }

        /// <summary>The persona:warm, persona:hothead... tags for lines written for a kind of person (Flavor tags).</summary>
        public static void AddTags(Pawn p, HashSet<string> tags)
        {
            if (!On || !Eligible(p)) return;
            try
            {
                var v = Values(p);
                foreach (var a in PersonaMath.All)
                {
                    var tag = PersonaMath.Tag(a, v[(int)a]);
                    if (tag != null) tags.Add(tag);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona tags", e);
            }
        }

        /// <summary>
        /// The tone a person's character gives their speech, for someone whose traits don't already decide it
        /// (<see cref="ToneUtility.ToneFor"/>): the side that shows most, or null.
        /// </summary>
        public static Tone? ToneOf(Pawn p)
        {
            if (!On || !Eligible(p)) return null;
            var v = Values(p);
            Tone? best = null;
            float strength = 0f;
            void Try(bool fits, float s, Tone t)
            {
                if (fits && s > strength)
                {
                    strength = s;
                    best = t;
                }
            }
            float warmth = v[(int)PersonaAxis.Warmth], temper = v[(int)PersonaAxis.Temper], humour = v[(int)PersonaAxis.Humour], outlook = v[(int)PersonaAxis.Outlook];
            Try(warmth >= 35f, warmth, Tone.Warm);
            Try(warmth <= -45f, -warmth, Tone.Cold);
            Try(temper >= 40f, temper, Tone.Blunt);
            Try(humour >= 35f, humour, Tone.Cheerful);
            Try(outlook <= -35f, -outlook, Tone.Gloomy);
            return best;
        }

        /// <summary>How alike two people are, 0..1 (who gets on with whom, who drifts together).</summary>
        public static float Likeness(Pawn a, Pawn b) => PersonaMath.Likeness(Values(a), Values(b));

        // ---------------- Stances ----------------

        /// <summary>Where someone stands on a topic, -1..1, or <paramref name="fallback"/> if they've never taken a view.</summary>
        public static float Stance(Pawn p, string topic, float fallback = 0f)
        {
            var r = Of(p);
            return r != null && topic != null && r.stances.TryGetValue(topic, out float v) ? v : fallback;
        }

        public static bool HasStance(Pawn p, string topic) => topic != null && (Of(p)?.stances.ContainsKey(topic) ?? false);

        public static void SetStance(Pawn p, string topic, float value)
        {
            if (!On || topic == null) return;
            var r = Of(p, true);
            if (r == null) return;
            r.stances[topic] = PersonaMath.Clamp(value, PersonaMath.StanceMax);
        }
    }

    /// <summary>What two people went through together (<see cref="SharedMemory"/>): adding, finding and recalling.</summary>
    public static class SharedMemories
    {
        /// <summary>
        /// Two people went through something together. The same thing again within a day only adds weight to the memory
        /// they already have; a pair keeps its <see cref="PersonaTracker.MaxPerPair"/> weightiest memories.
        /// </summary>
        public static SharedMemory Add(Pawn a, Pawn b, SharedMemoryDef def, string label, float weight = 1f)
        {
            var tr = PersonaTracker.Instance;
            if (!Persona.On || tr == null || def == null || a == null || b == null || a == b) return null;
            if (!Persona.Eligible(a) || !Persona.Eligible(b)) return null;
            int now = Find.TickManager.TicksGame;
            var list = tr.PairList(a.thingIDNumber, b.thingIDNumber, true);
            var same = list.FirstOrDefault(m => m.def == def && m.label == label && now - m.tick < GenDate.TicksPerDay);
            if (same != null)
            {
                same.weight = Mathf.Min(same.weight + weight * 0.5f, 3f);
                return same;
            }
            var mem = new SharedMemory
            {
                def = def,
                a = a.thingIDNumber,
                b = b.thingIDNumber,
                aName = a.LabelShort,
                bName = b.LabelShort,
                label = label,
                tick = now,
                weight = weight
            };
            tr.memories.Add(mem);
            list.Add(mem);
            if (list.Count > PersonaTracker.MaxPerPair) tr.Forget(list.OrderBy(m => m.WeightNow(now)).First());
            return mem;
        }

        /// <summary>Everything the pair went through, weightiest first.</summary>
        public static List<SharedMemory> Of(Pawn a, Pawn b)
        {
            var tr = PersonaTracker.Instance;
            if (tr == null || a == null || b == null) return new List<SharedMemory>();
            int now = Find.TickManager.TicksGame;
            var list = tr.PairList(a.thingIDNumber, b.thingIDNumber, false);
            return list == null ? new List<SharedMemory>() : list.OrderByDescending(m => m.WeightNow(now)).ToList();
        }

        /// <summary>
        /// A memory for the pair to bring up: weighted by how much it matters, less for one that came up lately, and only
        /// among those <paramref name="filter"/> allows. Null if they have none.
        /// </summary>
        public static SharedMemory Pick(Pawn a, Pawn b, Predicate<SharedMemory> filter = null)
        {
            int now = Find.TickManager.TicksGame;
            var options = Of(a, b).Where(m => filter == null || filter(m)).ToList();
            if (options.Count == 0) return null;
            return options.RandomElementByWeight(m =>
            {
                float w = Mathf.Max(0.01f, m.WeightNow(now));
                if (m.lastRecalled >= 0 && now - m.lastRecalled < GenDate.TicksPerDay * 3) w *= 0.2f;
                return w;
            });
        }

        /// <summary>It came up: counted, and a joke gets a little funnier (for a while).</summary>
        public static void Recalled(SharedMemory m)
        {
            if (m == null) return;
            m.recalls++;
            m.lastRecalled = Find.TickManager.TicksGame;
            if (m.def != null && m.def.joke && m.recalls <= 3) m.weight = Mathf.Min(m.weight + 0.25f, 3f);
        }

        /// <summary>The memory as the game shows it in the pair's history.</summary>
        public static string Text(SharedMemory m)
        {
            if (m?.def == null || m.def.text.NullOrEmpty()) return m?.label ?? "";
            return m.def.text.Formatted((m.label ?? "").Named("LABEL"), (m.aName ?? "").Named("A"), (m.bName ?? "").Named("B")).Resolve();
        }
    }
}
