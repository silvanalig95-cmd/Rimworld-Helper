using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The ordinary days in a colonist's journal: now and then, one of the things the game says is on their mind
    /// right now (a meal without a table, a cold night, a deep talk, a corpse by the path) becomes the day's entry,
    /// from the pool <c>Council_Journal_Day_{Key}</c>. Lines may use {OTHER_...} for moments with someone else, and the
    /// tags good/bad for moments that can go either way (the bedroom, the surroundings).
    /// </summary>
    public static class EverydayJournal
    {
        private sealed class Moment
        {
            public string key;
            public string[] thoughts;
            public float weight;
            /// <summary>Only when it weighs on them (hunger, not a full belly).</summary>
            public bool badOnly;
        }

        private static Moment M(string key, float weight, params string[] thoughts) => new Moment { key = key, weight = weight, thoughts = thoughts };

        private static Moment Bad(string key, float weight, params string[] thoughts) => new Moment { key = key, weight = weight, thoughts = thoughts, badOnly = true };

        private static readonly Moment[] Moments =
        {
            // Food
            M("NoTable", 1f, "AteWithoutTable"),
            M("RawFood", 1.2f, "AteRawFood"),
            // Vanilla Cooking Expanded's grills and gourmet dishes leave memories of their own.
            M("FineMeal", 0.6f, "AteFineMeal", "VCE_AteFineGrill"),
            M("LavishMeal", 1f, "AteLavishMeal", "VCE_AteLavishGrill", "VCE_AteGourmetGrill", "VCE_AteGourmetMeal"),
            M("Paste", 0.8f, "AteNutrientPasteMeal"),
            Bad("Hungry", 1.2f, "NeedFood"),
            // Sleep
            M("Cold", 1.2f, "SleptInCold"),
            M("Heat", 1.2f, "SleptInHeat"),
            M("Outside", 1f, "SleptOutside", "SleptOnGround"),
            M("Disturbed", 0.8f, "SleepDisturbed"),
            M("Barracks", 0.6f, "SleptInBarracks"),
            M("Bedroom", 0.7f, "SleptInBedroom"),
            Bad("Tired", 0.8f, "NeedRest"),
            // Home
            M("Dining", 0.6f, "AteInImpressiveDiningRoom"),
            M("RecRoom", 0.6f, "JoyActivityInImpressiveRecRoom"),
            M("Dark", 0.8f, "EnvironmentDark"),
            M("Beauty", 0.6f, "NeedBeauty"),
            Bad("CabinFever", 1f, "NeedOutdoors"),
            Bad("Bored", 0.8f, "NeedJoy"),
            // The body
            M("Wet", 1f, "SoakingWet"),
            M("Pain", 1f, "Pain"),
            M("Ragged", 0.8f, "ApparelDamaged"),
            M("DeadMansClothes", 1f, "DeadMansApparel"),
            M("Naked", 1f, "Naked"),
            M("Corpse", 1.2f, "ObservedLayingCorpse", "ObservedLayingRottingCorpse"),
            // Other people (and animals)
            M("DeepTalk", 1.5f, "DeepTalk"),
            M("Insulted", 1.5f, "Insulted", "Slighted"),
            M("KindWords", 1.2f, "KindWords", "KindWordsMood"),
            M("Lovin", 1f, "GotSomeLovin"),
            M("Rebuffed", 1.5f, "RebuffedMyRomanceAttempt"),
            M("Fight", 1.5f, "HadAngeringFight", "HadCatharticFight"),
            M("Rescued", 2f, "RescuedMe"),
            M("Taught", 1f, "WasTaught"),
            M("Lesson", 1f, "GaveLesson"),
            M("Nuzzled", 1.5f, "Nuzzled"),
            M("CryingBaby", 1f, "CryingBaby", "MyCryingBaby"),
            M("Party", 1.2f, "AttendedParty"),
            M("Concert", 1.2f, "AttendedConcert"),
            M("Speech", 1f, "InspirationalSpeech", "EncouragingSpeech"),
            M("Catharsis", 1.5f, "Catharsis"),
            M("Optimism", 0.5f, "NewColonyOptimism"),
            // Dubs Bad Hygiene (pools in Council_Journal_Everyday_Hygiene.xml); without it these are never on anyone's mind.
            M("OpenGround", 1.2f, "openDefecation"),
            M("SoiledSelf", 1.5f, "SoiledSelf"),
            M("WalkedIn", 1.2f, "ToiletPrivacy", "WashPrivacy"),
            M("HotBath", 1f, "HotBath", "HotShower", "HeatedPool"),
            M("ColdWash", 0.8f, "ColdBath", "ColdShower", "ColdWater"),
            M("Grimy", 0.8f, "HygieneLevel"),
            M("DirtyWater", 1.2f, "DrankDirtyWater", "DrankUrine"),
            M("OwnBathroom", 0.6f, "UsedPrivateBathroom"),
            Bad("Bursting", 0.6f, "BowelLevel"),
            // Vanilla Brewing Expanded
            M("Tea", 0.6f, "VBE_DrankTea"),
            // Rimfolk's everyday frictions (pools in Council_Journal_Everyday_Frictions.xml).
            M("Chores", 1f, "Council_Friction_ChoreSpat"),
            M("BadNight", 1.2f, "Council_Friction_Snoring", "Council_Friction_Woken"),
            M("Gossip", 1.3f, "Council_Friction_TalkedAboutMe"),
            M("NotReturned", 0.8f, "Council_Friction_NotReturned"),
            M("ColdShoulder", 1.2f, "Council_Friction_ColdShoulder", "Council_Friction_SilentTreatment"),
            M("Teased", 1.2f, "Council_Friction_BadJoke", "Council_Friction_Teased"),
        };

        private static Dictionary<string, Moment> byThought;

        private static Dictionary<string, Moment> ByThought
        {
            get
            {
                if (byThought == null)
                {
                    byThought = new Dictionary<string, Moment>();
                    foreach (var m in Moments)
                        foreach (var t in m.thoughts) byThought[t] = m;
                }
                return byThought;
            }
        }

        /// <summary>The same moment isn't written twice within this many days.</summary>
        private const int RepeatDays = 12;

        /// <summary>Chance per colonist per day of an everyday entry, when something fits.</summary>
        private const float DailyChance = 0.25f;

        private static readonly List<Thought> scratch = new List<Thought>();

        /// <summary>
        /// Once a day per colonist: maybe one line about something that's on their mind today. Skipped if they've
        /// already written today. <paramref name="seen"/> remembers who wrote about what, and when.
        /// </summary>
        public static void Daily(JournalTracker journals, List<Pawn> colonists, Dictionary<string, int> seen)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var p in colonists)
            {
                if (!Rand.Chance(DailyChance) || p.needs?.mood?.thoughts == null || !People.HasInnerLife(p)) continue;
                var last = journals.For(p)?.entries.LastOrDefault();
                if (last != null && now - last.tick < GenDate.TicksPerDay) continue;
                var pick = Pick(p, seen, now);
                if (pick.moment == null) continue;
                seen[p.ThingID + "|" + pick.moment.key] = now;
                var tags = new[] { pick.good ? "good" : "bad" };
                if (pick.other != null) journals.WriteTagged(p, "Council_Journal_Day_" + pick.moment.key, tags, null, pick.other.Named("OTHER"));
                else journals.WriteTagged(p, "Council_Journal_Day_" + pick.moment.key, tags);
            }
            foreach (var k in seen.Where(kv => now - kv.Value > GenDate.TicksPerDay * RepeatDays * 2).Select(kv => kv.Key).ToList()) seen.Remove(k);
            // Colonists posted at an outpost write home now and then (Integrations/Outposts.cs).
            OutpostLife.Letters(journals, seen);
        }

        private static (Moment moment, Pawn other, bool good) Pick(Pawn p, Dictionary<string, int> seen, int now)
        {
            var candidates = new List<(Moment moment, Pawn other, bool good, float weight)>();
            void Consider(Thought t, Pawn other)
            {
                if (t?.def == null || !ByThought.TryGetValue(t.def.defName, out var m)) return;
                if (seen.TryGetValue(p.ThingID + "|" + m.key, out int when) && now - when < GenDate.TicksPerDay * RepeatDays) return;
                float mood;
                try
                {
                    mood = t.MoodOffset();
                }
                catch
                {
                    mood = 0f;
                }
                if (m.badOnly && mood >= 0f) return;
                // A pool needs its other person to still be around to be named.
                if (other != null && (other.Destroyed || other.Discarded)) other = null;
                if (other == null && SocialMoments.Contains(m.key)) return;
                if (DefDatabase<FlavorPoolDef>.GetNamedSilentFail("Council_Journal_Day_" + m.key) == null) return;
                candidates.Add((m, other, mood >= 0f, m.weight * (1f + UnityEngine.Mathf.Min(System.Math.Abs(mood), 20f) / 8f)));
            }
            var memories = p.needs.mood.thoughts.memories?.Memories;
            if (memories != null)
                foreach (var mem in memories)
                    if (mem.age < GenDate.TicksPerDay) Consider(mem, mem.otherPawn);
            scratch.Clear();
            try
            {
                p.needs.mood.thoughts.GetAllMoodThoughts(scratch);
            }
            catch
            {
                scratch.Clear();
            }
            foreach (var t in scratch)
                if (!(t is Thought_Memory)) Consider(t, null);
            scratch.Clear();
            if (candidates.Count == 0) return (null, null, false);
            var c = candidates.RandomElementByWeight(x => x.weight);
            return (c.moment, c.other, c.good);
        }

        /// <summary>Moments that are about someone else: their lines name {OTHER}, so they need that person.</summary>
        private static readonly HashSet<string> SocialMoments = new HashSet<string>
        {
            "DeepTalk", "Insulted", "KindWords", "Lovin", "Rebuffed", "Fight", "Rescued", "Taught", "Lesson", "Nuzzled",
            "Chores", "BadNight", "Gossip", "NotReturned", "ColdShoulder", "Teased"
        };
    }
}
