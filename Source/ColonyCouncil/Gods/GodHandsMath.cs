using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What a god's hand in one of the game's events is, as people read it: a gift, a sign, or a wrath.</summary>
    public enum HandKind
    {
        Gift,
        Sign,
        Wrath
    }

    /// <summary>One row of the hands' table: whose hand people see in an event, what kind, how often they say so at all, and the god it meets there, if any.</summary>
    public readonly struct GodHand
    {
        public readonly string god;
        public readonly HandKind kind;
        /// <summary>The chance people remark on it at all, once the god's quiet spell is over.</summary>
        public readonly float chance;
        /// <summary>A second god in it, meeting the first (the eclipse: Helios and Selene), or null.</summary>
        public readonly string with;

        public GodHand(string god, HandKind kind, float chance, string with = null)
        {
            this.god = god;
            this.kind = kind;
            this.chance = chance;
            this.with = with;
        }

        public bool Valid => !string.IsNullOrEmpty(god);
    }

    /// <summary>
    /// The gods' hands in the game's events (docs/gods-pantheon-brainstorm.md, section C; docs/design/gods.md, "the
    /// contract"): which god people see behind which event (by the IncidentDef's name, else its worker's class), how often
    /// they say so, how long a god keeps quiet after, and how the colony says it by how real the gods are. People only say
    /// it: a believer's line over their head, and while the gods are only felt a scoffer's answer; never a letter, a news
    /// line or a word of what the god thinks. Pure, so Tools/Tests checks it.
    /// </summary>
    public static class GodHandsMath
    {
        /// <summary>A god keeps quiet this many days after people last saw its hand.</summary>
        public const int QuietDays = 20;

        /// <summary>And no god's hand is remarked on twice within this many days at a home.</summary>
        public const int AnyQuietDays = 4;

        private static GodHand H(string god, HandKind kind, float chance = 0.5f, string with = null) => new GodHand(god, kind, chance, with);

        /// <summary>The events by their IncidentDef's name: the game's, its DLCs', Rimfolk's own and the player's mods' (each looked up by name only).</summary>
        public static readonly Dictionary<string, GodHand> ByDef = new Dictionary<string, GodHand>(StringComparer.Ordinal)
        {
            // Hollin: green things growing where nobody sowed, and the trees answering.
            ["AmbrosiaSprout"] = H("Hollin", HandKind.Gift, 0.6f),
            ["AnimaTreeSpawn"] = H("Hollin", HandKind.Sign, 0.7f),
            ["GauranlenPodSpawn"] = H("Hollin", HandKind.Sign, 0.6f),
            ["PoluxTreeSpawn"] = H("Hollin", HandKind.Sign, 0.5f),
            ["Council_Evt_DesertBloom"] = H("Hollin", HandKind.Gift, 0.4f),
            ["Council_Evt_WildBounty"] = H("Hollin", HandKind.Gift, 0.3f),

            ["Rimfolk_Evt_LeaningTree"] = H("Hollin", HandKind.Sign, 0.3f),
            ["VEE_CropSprout"] = H("Hollin", HandKind.Gift, 0.4f),
            ["VPEF_Weeds"] = H("Hollin", HandKind.Wrath, 0.3f),
            // Smoulder: the mountain's breath, its dreams of metal, and its turning in its sleep.
            ["VolcanicWinter"] = H("Smoulder", HandKind.Wrath, 0.6f),
            ["LavaEmergence"] = H("Smoulder", HandKind.Wrath, 0.7f),
            ["LavaFlow"] = H("Smoulder", HandKind.Wrath, 0.7f),
            ["VolcanicDebris"] = H("Smoulder", HandKind.Wrath, 0.5f),
            ["VolcanicAsh"] = H("Smoulder", HandKind.Sign, 0.5f),
            ["Council_Evt_RichSeam"] = H("Smoulder", HandKind.Gift, 0.5f),
            ["Council_Evt_Rockslide"] = H("Smoulder", HandKind.Wrath, 0.5f),
            ["Council_Evt_WarmVent"] = H("Smoulder", HandKind.Gift, 0.5f),
            ["Rimfolk_Evt_DigCreak"] = H("Smoulder", HandKind.Sign, 0.4f),
            ["Earthquake"] = H("Smoulder", HandKind.Wrath, 0.6f),
            ["VEE_Earthquake"] = H("Smoulder", HandKind.Wrath, 0.6f),
            ["DrillQuake"] = H("Smoulder", HandKind.Wrath, 0.6f),
            // Bellwether: the thrumbo passing, and his kin walking his road.
            ["ThrumboPasses"] = H("Bellwether", HandKind.Sign, 0.8f),
            ["HerdMigration"] = H("Bellwether", HandKind.Sign, 0.2f),
            ["AA_BehemothPasses"] = H("Bellwether", HandKind.Sign, 0.4f),
            ["AEXP_ExtintAnimalsPasses"] = H("Bellwether", HandKind.Sign, 0.4f),
            ["VEE_MigratoryHerds"] = H("Bellwether", HandKind.Sign, 0.2f),
            // Morel: blight on the fields, rot, mushrooms overnight.
            ["CropBlight"] = H("Morel", HandKind.Wrath, 0.6f),
            ["Council_Evt_MushroomFlush"] = H("Morel", HandKind.Gift, 0.5f),
            ["BioluminescentSpores"] = H("Morel", HandKind.Sign, 0.5f),
            ["BoomshroomSprout"] = H("Morel", HandKind.Sign, 0.5f),
            ["GillRot"] = H("Morel", HandKind.Wrath, 0.5f),
            // Ennu, the Guest: a stranger at the gate.
            ["WandererJoin"] = H("Ennu", HandKind.Sign, 0.35f),
            ["RefugeePodCrash"] = H("Ennu", HandKind.Sign, 0.3f),
            ["StrangerInBlackJoin"] = H("Ennu", HandKind.Sign, 0.6f),
            ["TravelerGroup"] = H("Ennu", HandKind.Sign, 0.1f),
            ["Council_Evt_QuietStranger"] = H("Ennu", HandKind.Sign, 0.5f),
            ["Council_Evt_MidwinterGuest"] = H("Ennu", HandKind.Sign, 0.6f),
            ["VSE_ManInTheCoatJoin"] = H("Ennu", HandKind.Sign, 0.5f),
            ["HappyGuestJoins"] = H("Ennu", HandKind.Gift, 0.4f),
            // Pip: the machines in a mood.
            ["ShortCircuit"] = H("Pip", HandKind.Wrath, 0.6f),
            ["Rimfolk_Evt_TwitchingScrap"] = H("Pip", HandKind.Sign, 0.6f),
            ["Council_Evt_Signal"] = H("Pip", HandKind.Sign, 0.4f),
            // Cirrus: what falls from the sky, and the sky's moods.
            ["MeteoriteImpact"] = H("Cirrus", HandKind.Gift, 0.5f),
            ["ShipChunkDrop"] = H("Cirrus", HandKind.Gift, 0.3f),
            ["ResourcePodCrash"] = H("Cirrus", HandKind.Gift, 0.3f),
            ["Aurora"] = H("Cirrus", HandKind.Sign, 0.6f),
            ["Flashstorm"] = H("Cirrus", HandKind.Wrath, 0.6f),
            ["OrbitalDebris"] = H("Cirrus", HandKind.Sign, 0.4f),
            ["Council_Evt_Starfall"] = H("Cirrus", HandKind.Gift, 0.6f),
            ["Council_Evt_StarIron"] = H("Cirrus", HandKind.Gift, 0.6f),
            ["Council_Evt_Comet"] = H("Cirrus", HandKind.Sign, 0.6f),
            ["Council_Evt_StruckTree"] = H("Cirrus", HandKind.Sign, 0.4f),
            ["VEE_Aurora"] = H("Cirrus", HandKind.Sign, 0.6f),
            ["VEE_MeteoriteShower"] = H("Cirrus", HandKind.Gift, 0.5f),
            ["VESWW_FullMapFlashstorm"] = H("Cirrus", HandKind.Wrath, 0.6f),
            // Hush: the cold coming early, the longest nights.
            ["ColdSnap"] = H("Hush", HandKind.Wrath, 0.6f),
            ["Council_Evt_KillingFrost"] = H("Hush", HandKind.Wrath, 0.6f),
            ["Council_Evt_LongestNight"] = H("Hush", HandKind.Sign, 0.5f),
            ["Council_Evt_FirstSnow"] = H("Hush", HandKind.Sign, 0.3f),
            ["VEE_LongNight"] = H("Selene", HandKind.Sign, 0.5f),
            ["VEE_Whiteout"] = H("Hush", HandKind.Wrath, 0.6f),
            // Undertow: the psychic drone, and the dark that answers (Anomaly's).
            ["PsychicDrone"] = H("Undertow", HandKind.Wrath, 0.5f),
            ["UnnaturalDarkness"] = H("Undertow", HandKind.Wrath, 0.8f),
            ["DeathPall"] = H("Undertow", HandKind.Wrath, 0.7f),
            ["BloodRain"] = H("Undertow", HandKind.Wrath, 0.7f),
            ["HateChanters"] = H("Undertow", HandKind.Wrath, 0.5f),
            ["Revenant"] = H("Undertow", HandKind.Wrath, 0.4f),
            ["SightstealerArrival"] = H("Undertow", HandKind.Wrath, 0.5f),
            ["SightstealerSwarm"] = H("Undertow", HandKind.Wrath, 0.5f),
            ["ShamblerSwarm"] = H("Undertow", HandKind.Wrath, 0.4f),
            ["FleshbeastAttack"] = H("Undertow", HandKind.Wrath, 0.4f),
            ["PitGate"] = H("Undertow", HandKind.Wrath, 0.8f),
            ["MonolithMigration"] = H("Undertow", HandKind.Sign, 0.7f),
            ["GoldenCubeArrival"] = H("Undertow", HandKind.Sign, 0.6f),
            ["CreepJoinerJoin"] = H("Undertow", HandKind.Sign, 0.4f),
            ["VoidCuriosity"] = H("Undertow", HandKind.Sign, 0.6f),
            ["VREA_PsychicStorm"] = H("Undertow", HandKind.Wrath, 0.5f),
            ["VRE_BloodMoon"] = H("Undertow", HandKind.Wrath, 0.6f),
            // Wake: the sun and the moon, its open and its dreaming eye (eclipse, flare, heat, the long clear days).
            ["Drought"] = H("Helios", HandKind.Wrath, 0.5f),
            ["VEE_Drought"] = H("Helios", HandKind.Wrath, 0.5f),
            ["SolarFlare"] = H("Helios", HandKind.Wrath, 0.5f),
            ["VGE_SpaceSolarFlare"] = H("Helios", HandKind.Wrath, 0.5f),
            ["HeatWave"] = H("Helios", HandKind.Wrath, 0.35f),
            ["Council_Evt_GrowingSpell"] = H("Helios", HandKind.Gift, 0.3f),
            ["Council_Evt_WhiteNights"] = H("Helios", HandKind.Sign, 0.3f),
            // Selene: at night, what was done by day comes back (a kindness as a find or a healing, a cruelty as a nightmare or
            // a beast that returns); and the eclipse, her stepping between Helios and the world.
            ["Eclipse"] = H("Selene", HandKind.Sign, 0.7f, "Helios"),
            ["AA_Eclipse"] = H("Selene", HandKind.Sign, 0.7f, "Helios"),
            ["Council_Evt_Eclipse"] = H("Selene", HandKind.Sign, 0.6f, "Helios"),
            ["Council_Ail_Inc_Dreamfever"] = H("Selene", HandKind.Wrath, 0.4f),
            ["Council_Evt_FormerPatient"] = H("Selene", HandKind.Gift, 0.3f),
            ["Council_Story_FeverRepaid"] = H("Selene", HandKind.Gift, 0.4f),
            ["Council_Story_DebtRepaid"] = H("Selene", HandKind.Gift, 0.3f),
            ["Council_Evt_PetFind"] = H("Selene", HandKind.Gift, 0.2f),
            ["Council_Evt_NemesisReturns"] = H("Selene", HandKind.Wrath, 0.5f),
            ["Council_Evt_Revenge"] = H("Selene", HandKind.Wrath, 0.4f),
            // Mog: a cat wandering in.
            ["Council_Evt_Stray"] = H("Mog", HandKind.Sign, 0.3f),
            // Murmur: rain after dry weeks, drought and flood.
            ["SeasonalFlooding"] = H("Murmur", HandKind.Wrath, 0.5f),
            ["Council_Evt_LongRains"] = H("Murmur", HandKind.Gift, 0.3f),
            ["Council_Evt_HighWater"] = H("Murmur", HandKind.Wrath, 0.4f),
            ["VEE_FertileRains"] = H("Murmur", HandKind.Gift, 0.4f),
            ["VEE_TidalFloodingIncident"] = H("Murmur", HandKind.Wrath, 0.4f),
            // Ducat: a trader at the right time.
            ["TraderCaravanArrival"] = H("Ducat", HandKind.Gift, 0.08f),
            ["OrbitalTraderArrival"] = H("Ducat", HandKind.Gift, 0.1f),
            ["VTE_CaravanArriveForItems"] = H("Ducat", HandKind.Gift, 0.2f),
            ["Council_Evt_FarMarket"] = H("Ducat", HandKind.Gift, 0.3f)
        };

        /// <summary>The events by their worker's class (for a mod's event made with the game's worker), when the name says nothing.</summary>
        public static readonly Dictionary<string, GodHand> ByWorker = new Dictionary<string, GodHand>(StringComparer.Ordinal)
        {
            ["IncidentWorker_AmbrosiaSprout"] = H("Hollin", HandKind.Gift, 0.4f),
            ["IncidentWorker_ThrumboPasses"] = H("Bellwether", HandKind.Sign, 0.5f),
            ["IncidentWorker_CropBlight"] = H("Morel", HandKind.Wrath, 0.5f),
            ["IncidentWorker_WandererJoin"] = H("Ennu", HandKind.Sign, 0.2f),
            ["IncidentWorker_ShortCircuit"] = H("Pip", HandKind.Wrath, 0.5f),
            ["IncidentWorker_MeteoriteImpact"] = H("Cirrus", HandKind.Gift, 0.4f),
            ["IncidentWorker_Aurora"] = H("Cirrus", HandKind.Sign, 0.5f),
            ["IncidentWorker_Flashstorm"] = H("Cirrus", HandKind.Wrath, 0.5f),
            ["IncidentWorker_ColdSnap"] = H("Hush", HandKind.Wrath, 0.5f),
            ["IncidentWorker_TraderCaravanArrival"] = H("Ducat", HandKind.Gift, 0.05f),
            ["IncidentWorker_PsychicDrone"] = H("Undertow", HandKind.Wrath, 0.4f)
        };

        /// <summary>
        /// Whose hand an event is: by its def's name, else its worker's class, else none. A blight on the axe-happy (a colony
        /// Hollin is displeased with, hidden state) is Hollin's, paying it back, not Morel's.
        /// </summary>
        public static GodHand Of(string defName, string workerClass, bool rootsDispleased = false)
        {
            GodHand h = default;
            if (defName != null && ByDef.TryGetValue(defName, out var byDef)) h = byDef;
            else if (workerClass != null && ByWorker.TryGetValue(workerClass, out var byWorker)) h = byWorker;
            if (h.Valid && h.god == "Morel" && h.kind == HandKind.Wrath && (defName == "CropBlight" || workerClass == "IncidentWorker_CropBlight") && rootsDispleased)
                return new GodHand("Hollin", HandKind.Wrath, h.chance, h.with);
            return h;
        }

        /// <summary>
        /// People remark on a god's hand now: its own quiet spell is over (<see cref="QuietDays"/> since its last), no god's
        /// hand was remarked on at the home lately (<see cref="AnyQuietDays"/>), and the event's own chance holds.
        /// <paramref name="lastGod"/> and <paramref name="lastAny"/> are ticks, below 0 for never.
        /// </summary>
        public static bool Remarked(int lastGod, int lastAny, int now, float chance, float roll, int ticksPerDay = 60000)
        {
            if (lastGod >= 0 && now - lastGod < QuietDays * ticksPerDay) return false;
            if (lastAny >= 0 && now - lastAny < AnyQuietDays * ticksPerDay) return false;
            return roll < chance;
        }

        /// <summary>
        /// How the colony says it, by how real the gods are: a believer's line always; a scoffer's answer only while the gods
        /// are felt (some say so, and someone says otherwise); none when they are only believed (the colony is sure) or walk
        /// plainly (nobody doubts it).
        /// </summary>
        public static bool Scoffer(GodRealness r) => r == GodRealness.Felt;

        /// <summary>
        /// The tags a line about a hand may carry ([gift], [sign], [wrath]; [believed], [felt], [walking]; [meeting], for two
        /// gods meeting in it): it fits when each of its tags holds.
        /// </summary>
        public static bool Fits(string line, HandKind kind, GodRealness r, bool meeting = false)
        {
            if (string.IsNullOrEmpty(line)) return false;
            if (!line.StartsWith("[")) return true;
            int end = line.IndexOf(']');
            if (end < 0) return false;
            foreach (var raw in line.Substring(1, end - 1).Split(','))
            {
                string t = raw.Trim().ToLowerInvariant();
                if (t.Length == 0) continue;
                bool ok = t switch
                {
                    "gift" => kind == HandKind.Gift,
                    "sign" => kind == HandKind.Sign,
                    "wrath" => kind == HandKind.Wrath,
                    "believed" => r == GodRealness.Believed,
                    "felt" => r == GodRealness.Felt,
                    "walking" => r == GodRealness.Walking,
                    "meeting" => meeting,
                    _ => false
                };
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>A tagged line without its tags.</summary>
        public static string Body(string line)
        {
            if (string.IsNullOrEmpty(line) || !line.StartsWith("[")) return line;
            int end = line.IndexOf(']');
            return end < 0 ? line : line.Substring(end + 1).TrimStart();
        }

        /// <summary>The lines that fit a hand, the ones tagged for it first: a line made for the moment beats one for any.</summary>
        public static List<string> Fitting(IList<string> lines, HandKind kind, GodRealness r, bool meeting = false)
        {
            var tagged = new List<string>();
            var plain = new List<string>();
            if (lines == null) return tagged;
            foreach (var l in lines)
            {
                if (!Fits(l, kind, r, meeting)) continue;
                (l.StartsWith("[") ? tagged : plain).Add(Body(l));
            }
            return tagged.Count > 0 ? tagged : plain;
        }

        /// <summary>The gods whose hand is seen only at night (Selene returns at night what was done by day).</summary>
        public static bool NightOnly(string god) => god == "Selene";

        /// <summary>The night, for a god whose hand is seen only then: from dusk to dawn.</summary>
        public static bool Night(int hour) => hour >= 19 || hour < 6;

        /// <summary>A neighbour's letter may speak of a god's hand at the colony this many days after (and not before a few days have passed, for the word to travel).</summary>
        public const int LetterFromDays = 3, LetterUntilDays = 40;

        /// <summary>The hand is the kind of thing a neighbour would write about: within the days after it, and not yet written of.</summary>
        public static bool Writable(int handTick, int now, bool written, int ticksPerDay = 60000) =>
            !written && handTick >= 0 && now - handTick >= LetterFromDays * ticksPerDay && now - handTick <= LetterUntilDays * ticksPerDay;
    }
}
