using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What the topics need to know about a home map, looked up once an hour (or when first asked in that hour): who
    /// lives there, who is new, the prisoners and the children, the animals, the weather and any disaster, how long since
    /// the last raid, the food, the leader, the best cook and artist, the colony's project, traders about. Topics read it
    /// whenever two colonists might talk, so nothing here is worked out more often than that.
    /// </summary>
    public sealed class TalkScene
    {
        public Map map;
        public int built = -999999;

        public readonly List<Pawn> colonists = new List<Pawn>();
        /// <summary>Colonists who joined in the last <see cref="NewDays"/> days, newest first, with how many days ago.</summary>
        public readonly List<(Pawn pawn, float days)> newcomers = new List<(Pawn, float)>();
        public readonly List<Pawn> prisoners = new List<Pawn>();
        public readonly List<Pawn> slaves = new List<Pawn>();
        public readonly List<Pawn> children = new List<Pawn>();
        public int tameAnimals;
        public float daysSinceThreat = 999f;
        /// <summary>A disaster on the map now (a cold snap, toxic fallout, an eclipse), by its label, or null.</summary>
        public string disaster;
        public bool storm, cold, hot;
        public float foodDays = 99f;
        public Pawn leader, bestCook, bestArtist;
        /// <summary>Someone who does markedly less than the rest (lazy by nature or barred from most work), or null.</summary>
        public Pawn slacker;
        public AmbitionState ambition;
        public bool traders;
        /// <summary>The people of a trader at the door, by name, or null.</summary>
        public string traderPeople;
        /// <summary>Someone here is addicted; someone here is with child.</summary>
        public bool addicted, expecting;
        public float strain;

        public const float NewDays = 15f;

        private static readonly Dictionary<int, TalkScene> scenes = new Dictionary<int, TalkScene>();
        private static Game game;

        /// <summary>The scene for a map, at most an hour old (null for no map).</summary>
        public static TalkScene Of(Map map)
        {
            if (map == null) return null;
            if (Current.Game != game)
            {
                game = Current.Game;
                scenes.Clear();
            }
            int now = Find.TickManager.TicksGame;
            if (!scenes.TryGetValue(map.uniqueID, out var s) || s.map != map)
            {
                if (scenes.Count > 8) scenes.Clear();
                s = new TalkScene { map = map };
                scenes[map.uniqueID] = s;
            }
            if (now - s.built >= GenDate.TicksPerHour || now < s.built)
            {
                s.built = now;
                try
                {
                    s.Build(now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("talk scene", e);
                }
            }
            return s;
        }

        public static void Reset() => scenes.Clear();

        private void Build(int now)
        {
            colonists.Clear();
            newcomers.Clear();
            prisoners.Clear();
            slaves.Clear();
            children.Clear();
            leader = bestCook = bestArtist = slacker = null;
            tameAnimals = 0;
            int bestCooking = -1, bestArt = -1;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p == null || p.Dead) continue;
                colonists.Add(p);
                if (p.DevelopmentalStage.Child() || p.DevelopmentalStage.Baby()) children.Add(p);
                float days = (p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 999999) / (float)GenDate.TicksPerDay;
                if (days < NewDays && !p.DevelopmentalStage.Baby()) newcomers.Add((p, days));
                var cook = p.skills?.GetSkill(SkillDefOf.Cooking);
                if (cook != null && !cook.TotallyDisabled && cook.Level > bestCooking)
                {
                    bestCooking = cook.Level;
                    bestCook = p;
                }
                var art = p.skills?.GetSkill(SkillDefOf.Artistic);
                if (art != null && !art.TotallyDisabled && art.Level >= 6 && art.Level > bestArt)
                {
                    bestArt = art.Level;
                    bestArtist = p;
                }
            }
            newcomers.Sort((x, y) => x.days.CompareTo(y.days));
            foreach (var p in map.mapPawns.PrisonersOfColonySpawned.ToList())
                if (p != null && !p.Dead) prisoners.Add(p);
            foreach (var p in map.mapPawns.SlavesOfColonySpawned.ToList())
                if (p != null && !p.Dead) slaves.Add(p);
            foreach (var p in map.mapPawns.SpawnedColonyAnimals)
                if (p != null && !p.Dead) tameAnimals++;
            slacker = FindSlacker();
            int threat = map.storyState?.LastThreatBigTick ?? -1;
            daysSinceThreat = threat > 0 ? (now - threat) / (float)GenDate.TicksPerDay : 999f;
            disaster = null;
            foreach (var c in map.gameConditionManager?.ActiveConditions ?? new List<GameCondition>())
            {
                if (c?.def != null && TalkHooks.IsDisaster(c.def))
                {
                    disaster = c.LabelCap;
                    break;
                }
            }
            var weather = map.weatherManager?.curWeather;
            storm = weather != null && Flavor.IsStorm(weather);
            float temp = map.mapTemperature?.OutdoorTemp ?? 15f;
            cold = temp <= -5f;
            hot = temp >= 35f;
            foodDays = MapFacts.Of(map)?.FoodDays ?? 99f;
            leader = Leader();
            var amb = CouncilGameComponent.Instance?.ambitions?.active;
            ambition = amb != null && amb.map == map ? amb : null;
            var trader = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.TraderKind != null && p.Faction != null && p.Faction != Faction.OfPlayer && !p.HostileTo(Faction.OfPlayer));
            traderPeople = trader?.Faction?.Name;
            traders = trader != null || map.passingShipManager?.passingShips?.Count > 0;
            addicted = colonists.Any(p => p.health?.hediffSet?.hediffs.Any(h => h is Hediff_Addiction) == true);
            expecting = colonists.Any(p => p.health?.hediffSet?.hediffs.Any(h => h.def.defName.StartsWith("Pregnan", StringComparison.Ordinal)) == true);
            strain = colonists.Count == 0 ? 0f : colonists.Average(p => Heart.StrainOf(p));
        }

        /// <summary>The speaker of the council, else the faith's leader, else nobody.</summary>
        private Pawn Leader()
        {
            var speaker = CouncilGameComponent.Instance?.offices?.HolderOf("Council_Office_Speaker");
            if (speaker != null && !speaker.Dead) return speaker;
            if (!ModsConfig.IdeologyActive) return null;
            foreach (var p in colonists)
            {
                var role = p.Ideo?.GetRole(p);
                if (role?.def != null && role.def == PreceptDefOf.IdeoRole_Leader) return p;
            }
            return null;
        }

        private static TraitDef industriousness;
        private static bool industriousnessLooked;

        public static TraitDef Industriousness
        {
            get
            {
                if (!industriousnessLooked)
                {
                    industriousnessLooked = true;
                    industriousness = DefDatabase<TraitDef>.GetNamedSilentFail("Industriousness");
                }
                return industriousness;
            }
        }

        /// <summary>Someone lazy by nature, or barred from most of the work, in a colony of four or more.</summary>
        private Pawn FindSlacker()
        {
            if (colonists.Count < 4) return null;
            Pawn worst = null;
            int worstScore = 2;
            foreach (var p in colonists)
            {
                if (p.DevelopmentalStage.Child() || p.DevelopmentalStage.Baby()) continue;
                int score = 0;
                var traits = p.story?.traits;
                int ind = traits != null && Industriousness != null && traits.HasTrait(Industriousness) ? traits.DegreeOfTrait(Industriousness) : 0;
                if (ind < 0) score += -ind * 2;
                var disabled = p.story?.DisabledWorkTagsBackstoryTraitsAndGenes ?? WorkTags.None;
                if ((disabled & WorkTags.ManualDumb) != 0) score += 2;
                if ((disabled & WorkTags.Hauling) != 0) score += 1;
                if ((disabled & WorkTags.Cleaning) != 0) score += 1;
                if (score > worstScore)
                {
                    worstScore = score;
                    worst = p;
                }
            }
            return worst;
        }

        /// <summary>The newest newcomer other than these two, or null.</summary>
        public Pawn Newcomer(Pawn a, Pawn b)
        {
            foreach (var (p, _) in newcomers)
                if (p != a && p != b && !p.Dead && p.Spawned)
                    return p;
            return null;
        }

        public float NewcomerDays(Pawn p)
        {
            foreach (var (n, d) in newcomers)
                if (n == p)
                    return d;
            return 999f;
        }
    }
}
