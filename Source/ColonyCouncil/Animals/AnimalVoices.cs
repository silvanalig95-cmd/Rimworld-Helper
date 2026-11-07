using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>How an animal comes to speak, and to whom (Animals/AnimalVoices.cs).</summary>
    public enum AnimalGift
    {
        None,
        /// <summary>A psychic-type Pokémon, to its trainer, in the mind (PokéWorld).</summary>
        Mind,
        /// <summary>A bonded animal of a druid or a ranger, who communes with it (A RimWorld of Magic).</summary>
        Commune,
        /// <summary>A bonded animal of a strong psycaster, who hears it (Royalty, psylink level).</summary>
        Psylink,
        /// <summary>A humanoid hybrid of Vanilla Genetics Expanded, whose making gave it human words.</summary>
        Hybrid,
        /// <summary>A tribe's spirit animal (or one of a faith that holds animals to be people): it speaks only in its person's dreams.</summary>
        Spirit
    }

    /// <summary>
    /// Animals that speak, rare and behind the setting "Animals speak" (a fantasy part: on Auto only while PokéWorld or
    /// A RimWorld of Magic is loaded, or someone in the colony has a psylink). Who can speak, softly by mod and by age:
    /// a psychic-type Pokémon to its trainer, in the mind; a druid's or a ranger's bonded animal (A RimWorld of Magic,
    /// their classes read by name); a strong psycaster's bonded animal (psylink level three or more); Vanilla Genetics
    /// Expanded's humanoid hybrids (their own defs give them conversation); and in a tribe, or a colony whose faith holds
    /// animals to be people, a bonded animal speaks in its person's dreams (Defs/DreamDefs/Council_Dreams_SpiritAnimal.xml).
    /// What it does, never in a letter or a message:
    /// <list type="bullet">
    /// <item>now and then a line to its person in its own voice, shaped by its kind and its temperament, when the two are
    /// together and in view (about once a day at most);</item>
    /// <item>a warning, said over its head, when it senses raiders or a predator really on the map near it, with where;</item>
    /// <item>a place at the council table without a vote: one line at a meeting its person sits in (written with the
    /// meeting's other remarks), and a word on the council's question about its person or itself, in the question's
    /// letter and over its head.</item>
    /// </list>
    /// Nothing is saved: the waits start again after a load.
    /// </summary>
    public static class AnimalVoices
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static int Now => Find.TickManager.TicksGame;

        private static readonly Color SpeechColor = new Color(0.82f, 0.9f, 0.82f);

        // ---------------- On, and the fantasy sources ----------------

        private static int fantasyAt = -99999;
        private static bool fantasy;

        /// <summary>
        /// A fantasy source is there: PokéWorld or A RimWorld of Magic loaded, or someone of the colony with a psylink
        /// (Royalty), looked at once an hour.
        /// </summary>
        public static bool Fantasy
        {
            get
            {
                if (Current.Game == null) return PokeWorldLink.Active || People.ModActive(SettingsCatalog.MagicId);
                int now = Now;
                if (now >= fantasyAt && now - fantasyAt < GenDate.TicksPerHour) return fantasy;
                fantasyAt = now;
                fantasy = PokeWorldLink.Active || People.ModActive(SettingsCatalog.MagicId) || AnyPsylink();
                return fantasy;
            }
        }

        private static bool AnyPsylink()
        {
            if (!ModsConfig.RoyaltyActive) return false;
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
                if (p.GetPsylinkLevel() > 0) return true;
            return false;
        }

        /// <summary>Animals speak now: pets as companions on, and the setting (Auto: with a fantasy source).</summary>
        public static bool On => AnimalLife.On && AnimalNatureMath.Speaks(S.speakingAnimals, Fantasy);

        // ---------------- Who speaks ----------------

        /// <summary>Animals a dev action lets speak, for trying it where nothing would (not saved).</summary>
        private static readonly HashSet<int> letSpeak = new HashSet<int>();
        private static readonly Dictionary<ThingDef, bool> hybrids = new Dictionary<ThingDef, bool>();
        private static Game game;

        private static void Fresh()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            letSpeak.Clear();
            spoke.Clear();
            fantasyAt = -99999;
        }

        /// <summary>A humanoid hybrid of Vanilla Genetics Expanded: its comps (read by name) give it conversation with the colony.</summary>
        public static bool Hybrid(ThingDef def)
        {
            if (def?.comps == null) return false;
            if (hybrids.TryGetValue(def, out bool h)) return h;
            h = def.comps.Any(c => c != null && c.GetType().Name == "CompProperties_HumanoidHybrid");
            hybrids[def] = h;
            return h;
        }

        private static TraitDef druid, ranger;
        private static bool traitsLooked;

        /// <summary>A druid or a ranger of A RimWorld of Magic (their classes are traits, read by name).</summary>
        public static bool Communes(Pawn p)
        {
            if (!traitsLooked)
            {
                traitsLooked = true;
                druid = DefDatabase<TraitDef>.GetNamedSilentFail("Druid");
                ranger = DefDatabase<TraitDef>.GetNamedSilentFail("Ranger");
            }
            var traits = p?.story?.traits;
            return traits != null && (druid != null && traits.HasTrait(druid) || ranger != null && traits.HasTrait(ranger));
        }

        private static MemeDef personhood;
        private static bool memeLooked;

        /// <summary>
        /// A tribe's ways, or a faith that holds animals to be people (Ideology's animal personhood): there a bonded animal
        /// is a spirit animal, speaking in its person's dreams.
        /// </summary>
        public static bool SpiritWays(Pawn p)
        {
            if (Avail.EffectiveTech <= TechLevel.Neolithic) return true;
            if (!ModsConfig.IdeologyActive || p?.Ideo == null) return false;
            if (!memeLooked)
            {
                memeLooked = true;
                personhood = DefDatabase<MemeDef>.GetNamedSilentFail("AnimalPersonhood");
            }
            return personhood != null && p.Ideo.HasMeme(personhood);
        }

        private static Pawn BondedTo(Pawn a, Func<Pawn, bool> who)
        {
            if (a?.relations == null) return null;
            foreach (var r in a.relations.DirectRelations)
                if (r.def == PawnRelationDefOf.Bond && r.otherPawn != null && !r.otherPawn.Dead && r.otherPawn.IsColonist && People.HasInnerLife(r.otherPawn) && who(r.otherPawn))
                    return r.otherPawn;
            return null;
        }

        /// <summary>How this animal speaks and to whom, whether or not speech is on now; None for most.</summary>
        public static AnimalGift GiftOf(Pawn a, out Pawn hearer)
        {
            hearer = null;
            if (!AnimalTempers.Has(a)) return AnimalGift.None;
            Fresh();
            if (PokeWorldLink.IsPokemon(a) && PokeWorldLink.TypesOf(a.def).Any(t => string.Equals(t, "Psychic", StringComparison.OrdinalIgnoreCase)))
            {
                hearer = PetTracker.PersonOf(a);
                if (hearer != null) return AnimalGift.Mind;
            }
            if ((hearer = BondedTo(a, Communes)) != null) return AnimalGift.Commune;
            if (ModsConfig.RoyaltyActive && (hearer = BondedTo(a, p => AnimalNatureMath.HearsByPsylink(p.GetPsylinkLevel()))) != null) return AnimalGift.Psylink;
            if (Hybrid(a.def) || letSpeak.Contains(a.thingIDNumber))
            {
                hearer = PetTracker.PersonOf(a);
                return AnimalGift.Hybrid;
            }
            if ((hearer = BondedTo(a, SpiritWays)) != null) return AnimalGift.Spirit;
            return AnimalGift.None;
        }

        /// <summary>It speaks aloud or in the mind now (not only in dreams), with whom it speaks to.</summary>
        public static bool Speaks(Pawn a, out Pawn hearer, out AnimalGift gift)
        {
            hearer = null;
            gift = AnimalGift.None;
            if (!On) return false;
            gift = GiftOf(a, out hearer);
            return gift != AnimalGift.None && gift != AnimalGift.Spirit;
        }

        /// <summary>The bonded animal that speaks in this colonist's dreams, if speech is on and their ways have spirit animals; else null.</summary>
        public static Pawn SpiritOf(Pawn person)
        {
            if (!On || person?.relations == null || !SpiritWays(person)) return null;
            foreach (var r in person.relations.DirectRelations)
                if (r.def == PawnRelationDefOf.Bond && AnimalFacts.Ours(r.otherPawn) && GiftOf(r.otherPawn, out var h) == AnimalGift.Spirit && h == person)
                    return r.otherPawn;
            return null;
        }

        /// <summary>A dev action: this animal may speak (as a hybrid does, aloud), for trying it out. Not saved.</summary>
        public static void LetSpeak(Pawn a)
        {
            Fresh();
            if (a != null) letSpeak.Add(a.thingIDNumber);
        }

        // ---------------- Its words ----------------

        /// <summary>Its tags: its voice, temperament and gift, a Pokémon's types, its age, and the moment's.</summary>
        public static List<string> Tags(Pawn a, AnimalGift gift, params string[] more)
        {
            var facts = AnimalTempers.Facts(a);
            var tags = new List<string> { "voice:" + AnimalNatureMath.Voice(facts), "gift:" + gift.ToString().ToLowerInvariant() };
            var t = AnimalTempers.Of(a);
            if (t != null) tags.Add("temper:" + AnimalNatureMath.Key(t.Value));
            if (facts.types != null)
                foreach (var type in facts.types)
                    tags.Add("type:" + (type ?? "").ToLowerInvariant());
            if (AnimalFacts.Old(a)) tags.Add("oldanimal");
            if (AnimalFacts.Young(a)) tags.Add("young");
            if (a.RaceProps.predator) tags.Add("hunter");
            if (a.RaceProps.baseBodySize >= 2.5f) tags.Add("huge");
            tags.AddRange(more.Where(m => !m.NullOrEmpty()));
            return tags;
        }

        /// <summary>A line in its own voice from a pool, with {PET} the animal and {PAWN} whom it speaks to; null when none fits.</summary>
        public static string Line(string pool, Pawn a, Pawn hearer, List<string> tags, params NamedArgument[] args)
        {
            try
            {
                string line = Flavor.FromPool(pool, a, null, Tone.Plain, tags.ToArray());
                if (line.NullOrEmpty()) return null;
                var all = new List<NamedArgument> { a.Named("PET"), (hearer ?? a).Named("PAWN") };
                if (args != null) all.AddRange(args);
                return line.Formatted(all.ToArray()).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal voice " + pool, e);
                return null;
            }
        }

        // ---------------- The hour ----------------

        private static readonly HashSet<string> spoke = new HashSet<string>();

        /// <summary>
        /// Once an hour (AnimalLife.Hourly): a speaking animal in view with its person says something to them now and then,
        /// and one that senses a predator about on its map warns of it.
        /// </summary>
        public static void Hourly()
        {
            if (!On) return;
            Fresh();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                var speakers = Speakers(map);
                if (speakers.Count == 0) continue;
                WarnOf(map, speakers, false);
                if (map.dangerWatcher.DangerRating != StoryDanger.None) continue;
                if (!VisualTouches.InView(out var viewMap, out var view) || viewMap != map) continue;
                foreach (var (a, hearer, gift) in speakers.InRandomOrder())
                {
                    if (!view.Contains(a.Position) || !Rand.Chance(AnimalNatureMath.SayHourChance)) continue;
                    if (Say(a, hearer, gift)) break;
                }
            }
        }

        /// <summary>Every 250 ticks while a fight is on at home (Sched.RareFightSides): a speaking animal warns of the raiders it senses.</summary>
        public static void FightLook()
        {
            if (!On) return;
            Fresh();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || !MapFacts.ThreatOn(map)) continue;
                var speakers = Speakers(map);
                if (speakers.Count > 0) WarnOf(map, speakers, true);
            }
        }

        private static List<(Pawn a, Pawn hearer, AnimalGift gift)> Speakers(Map map)
        {
            var list = new List<(Pawn, Pawn, AnimalGift)>();
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
                if (MoreHabits.Calm(a) && a.Awake() && Speaks(a, out var hearer, out var gift))
                    list.Add((a, hearer, gift));
            return list;
        }

        /// <summary>A line to its person (or, for a hybrid, to whoever is with it), when the two are together; about once a day at most.</summary>
        public static bool Say(Pawn a, Pawn hearer, AnimalGift gift, bool force = false)
        {
            var to = hearer != null && Together.Can(a, hearer) ? hearer : gift == AnimalGift.Hybrid ? Together.Company(a) : null;
            if (to == null) return false;
            if (!force && !AnimalTempers.Ready("voice|" + a.thingIDNumber, AnimalNatureMath.SayGapDays)) return false;
            var tags = Tags(a, gift, "idle");
            if (to != hearer) tags.Add("stranger");
            string line = Line("Council_Pool_AnimalVoice", a, to, tags);
            // together: said to them, the two together (Together.Can above; Speech.Say checks again).
            if (line.NullOrEmpty() || !Speech.Say(a, line, to, SpeechColor)) return false;
            AnimalTempers.Noted(a, "CC_VoiceDid_Spoke", to.Named("PAWN"));
            // The first time it speaks to them, they write it down.
            if (hearer != null && to == hearer && AnimalTempers.Ready("voice-first|" + a.thingIDNumber, 9999f))
                CouncilGameComponent.Instance?.journals.WriteTagged(hearer, "Council_Journal_AnimalVoice", Tags(a, gift, "first").ToArray(), null, a.Named("PET"));
            return true;
        }

        // ---------------- Warnings ----------------

        private static bool Raider(Pawn p, Map map) =>
            p != null && p.Spawned && !p.Dead && !p.Downed && !p.IsPrisoner && p.HostileTo(Faction.OfPlayer) && (p.RaceProps.Humanlike || p.RaceProps.IsMechanoid)
            && !p.Position.Fogged(map);

        private static bool Angry(Pawn p, Map map) =>
            p?.RaceProps != null && p.RaceProps.Animal && p.Spawned && !p.Dead && !p.Downed && p.InMentalState && p.HostileTo(Faction.OfPlayer) && !p.Position.Fogged(map);

        /// <summary>A wild predator about (or an angry beast) on the map, in view of the land.</summary>
        private static bool Predator(Pawn p, Map map) => AnimalFacts.WildPredator(p, map) || Angry(p, map);

        /// <summary>One worth warning this animal of: angry, on the hunt, or near its own size (AnimalNatureMath.Dangerous).</summary>
        private static bool Threat(Pawn danger, Pawn a, Map map) =>
            AnimalNatureMath.Dangerous(Angry(danger, map), danger.CurJobDef == JobDefOf.PredatorHunt, danger.RaceProps.baseBodySize, a.RaceProps.baseBodySize);

        /// <summary>
        /// What it senses near it (raiders in a fight, else a predator or an angry beast), said over its head with where
        /// it is: once a day for the same danger, one speaker at a time.
        /// </summary>
        private static void WarnOf(Map map, List<(Pawn a, Pawn hearer, AnimalGift gift)> speakers, bool raid)
        {
            var dangers = map.mapPawns.AllPawnsSpawned.Where(p => raid ? Raider(p, map) : Predator(p, map)).ToList();
            if (dangers.Count == 0) return;
            foreach (var (a, hearer, gift) in speakers)
            {
                var near = dangers.Where(d => d.Position.InHorDistOf(a.Position, AnimalNatureMath.WarnRange) && (raid || Threat(d, a, map)))
                    .OrderBy(d => d.Position.DistanceToSquared(a.Position)).FirstOrDefault();
                if (near == null) continue;
                // Predators: a warning every half day at most from one animal, whatever the beast (raids each their own).
                if (!raid && !AnimalTempers.Ready("warn-predator|" + a.thingIDNumber, AnimalNatureMath.PredatorWarnGapDays)) continue;
                if (Warn(a, hearer, gift, near, raid)) return;
            }
        }

        private static bool Warn(Pawn a, Pawn hearer, AnimalGift gift, Pawn danger, bool raid, bool force = false)
        {
            string key = "warn|" + a.thingIDNumber + "|" + (raid ? (danger.GetLord()?.loadID ?? danger.thingIDNumber) : danger.thingIDNumber);
            if (!force && !AnimalTempers.Ready(key, AnimalNatureMath.WarnGapDays)) return false;
            string dir = AnimalNatureMath.Compass(danger.Position.x - a.Position.x, danger.Position.z - a.Position.z);
            var tags = Tags(a, gift, "warn", raid ? "raid" : "predator", danger.RaceProps.IsMechanoid ? "mech" : null);
            var to = hearer != null && Together.Can(a, hearer) ? hearer : null;
            string line = Line("Council_Pool_AnimalVoice", a, to ?? hearer, tags, ("CC_Compass_" + dir).Translate().Named("PLACE"), danger.Named("OTHER"));
            // apart: a warning cried out to whoever hears, its person among them when they're near.
            if (line.NullOrEmpty() || !Speech.Say(a, line, null, SpeechColor)) return false;
            AnimalTempers.Noted(a, raid ? "CC_VoiceDid_WarnedRaid" : "CC_VoiceDid_WarnedPredator", danger.Named("OTHER"));
            return true;
        }

        // ---------------- At the council table ----------------

        /// <summary>
        /// A council meeting under way (CouncilMeeting.Talk): a speaking animal of someone sitting in it comes along, and
        /// once a meeting says one line, for its person's side (their own idea, or the one whose proposer they like best)
        /// or its own, written with the meeting's remarks. It never votes. True when it spoke (the meeting's turn).
        /// </summary>
        public static bool MeetingWord(CouncilMeeting m, List<Pawn> here)
        {
            if (!On || m?.map == null || here == null || m.pitches.Count == 0) return false;
            Fresh();
            string key = "meeting|" + m.calledTick;
            if (spoke.Contains(key)) return false;
            var put = m.pitches.Where(p => p.spoken || p.readOut).ToList();
            if (put.Count == 0) return false;
            foreach (var a in m.map.mapPawns.SpawnedColonyAnimals.ToList())
            {
                if (!MoreHabits.Calm(a) || !a.Awake() || !Speaks(a, out var hearer, out var gift) || hearer == null || !here.Contains(hearer)) continue;
                if (!SocialLog.Near(a, hearer))
                {
                    // It comes along to the table, at its person's heels.
                    if (a.GetLord() == null && AnimalTempers.Ready("meeting-come|" + a.thingIDNumber + "|" + m.calledTick, 1f) && a.CanReach(hearer, PathEndMode.Touch, Danger.Some))
                    {
                        var follow = JobMaker.MakeJob(JobDefOf.FollowClose, hearer);
                        follow.followRadius = 2f;
                        follow.expiryInterval = 2500;
                        follow.checkOverrideOnExpire = true;
                        a.jobs.StartJob(follow, JobCondition.InterruptForced, null, false, true);
                    }
                    continue;
                }
                var own = put.FirstOrDefault(p => p.proposer == hearer);
                MeetingPitch side;
                bool its = false;
                if (own != null && Rand.Chance(0.7f)) side = own;
                else if (Rand.Chance(0.6f))
                    side = put.OrderByDescending(p => p.proposer != null && p.proposer != hearer && hearer.relations != null ? hearer.relations.OpinionOf(p.proposer) : 0).First();
                else
                {
                    side = put.RandomElement();
                    its = true;
                }
                var tags = Tags(a, gift, "meeting", its ? "side:own" : "side:person", side.proposer == hearer ? "ownidea" : null);
                string line = Line("Council_Pool_AnimalVoice", a, hearer, tags, (side.def?.label ?? "").Named("AMBITION"), (side.proposer ?? hearer).Named("OTHER"));
                // together: the meeting's own gathering, its person beside it (SocialLog.Near above).
                if (line.NullOrEmpty() || !Speech.Say(a, line, hearer, SpeechColor)) continue;
                spoke.Add(key);
                if (spoke.Count > 200) spoke.Clear();
                if (m.remarks.Count < 5) m.remarks.Add("CC_Meeting_Remark".Translate(a.LabelShort, line).Resolve());
                AnimalTempers.Noted(a, "CC_VoiceDid_Meeting", hearer.Named("PAWN"));
                return true;
            }
            return false;
        }

        /// <summary>
        /// A council question about someone whose animal speaks (or about the animal itself): half the time it has a word,
        /// written into the question's letter and said over its head if its person is beside it. Null when it has none.
        /// </summary>
        public static string DilemmaWord(DilemmaContext ctx, bool force = false)
        {
            if (!On || ctx?.map == null) return null;
            force |= DebugNextDilemma;
            try
            {
                Pawn speaker = null, hearer = null;
                var gift = AnimalGift.None;
                foreach (var a in new[] { ctx.pet }.Concat(ctx.map.mapPawns.SpawnedColonyAnimals))
                {
                    if (a == null || !a.Spawned || a.Map != ctx.map || !Speaks(a, out var h, out var g)) continue;
                    if (a != ctx.pet && (h == null || (h != ctx.a && h != ctx.b))) continue;
                    speaker = a;
                    hearer = h;
                    gift = g;
                    break;
                }
                if (speaker == null || (!force && !Rand.Chance(AnimalNatureMath.DilemmaShare))) return null;
                bool aboutIt = speaker == ctx.pet;
                var tags = Tags(speaker, gift, "dilemma", aboutIt ? "aboutme" : Rand.Chance(0.6f) ? "side:person" : "side:own");
                string line = Line("Council_Pool_AnimalVoice", speaker, hearer, tags);
                if (line.NullOrEmpty()) return null;
                // together: over its head only when its person is beside it (Together.Can); the letter carries it either way.
                if (hearer != null && Together.Can(speaker, hearer)) Speech.Say(speaker, line, hearer, SpeechColor);
                AnimalTempers.Noted(speaker, "CC_VoiceDid_Dilemma");
                DebugNextDilemma = false;
                return "CC_Voice_DilemmaWord".Translate(speaker.Named("PET"), (hearer ?? speaker).Named("PAWN"), line.Named("LINE")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal voice: the council's question", e);
                return null;
            }
        }

        // ---------------- The card ----------------

        /// <summary>How it speaks and to whom, for its card, while speech is on; null otherwise.</summary>
        public static string InspectLine(Pawn a)
        {
            if (!On) return null;
            var gift = GiftOf(a, out var hearer);
            if (gift == AnimalGift.None) return null;
            if (hearer == null) return gift == AnimalGift.Hybrid ? "CC_Voice_Inspect_HybridAnyone".Translate(a.Named("PET")).Resolve() : null;
            return ("CC_Voice_Inspect_" + gift).Translate(a.Named("PET"), hearer.Named("PAWN")).Resolve();
        }

        // ---------------- Dev actions ----------------

        /// <summary>A dev action: the next council question about a speaking animal's person carries its word.</summary>
        public static bool DebugNextDilemma;

        /// <summary>The dev action: who could speak on this map, and why not for the rest of the named and bonded.</summary>
        public static string DebugWho(Map map)
        {
            if (map == null) return "no map.";
            var lines = new List<string>
            {
                "Animals speak: setting " + S.speakingAnimals + ", fantasy source " + (Fantasy ? "there" : "none") + " (PokéWorld " + PokeWorldLink.Active + ", A RimWorld of Magic "
                + People.ModActive(SettingsCatalog.MagicId) + ", a psylink " + AnyPsylink() + "): " + (On ? "on." : "off.")
            };
            foreach (var a in map.mapPawns.SpawnedColonyAnimals.Where(AnimalTempers.Has))
            {
                var gift = GiftOf(a, out var hearer);
                lines.Add("  " + a.LabelShort + " (" + AnimalFacts.KindLabel(a) + ", " + AnimalNatureMath.Voice(AnimalTempers.Facts(a)) + "): "
                          + (gift == AnimalGift.None ? "doesn't speak" : gift + (hearer != null ? " to " + hearer.LabelShort : "")));
            }
            Log.Message("[Rimfolk] " + string.Join("\n", lines));
            return lines[0] + " The list is in the log.";
        }

        /// <summary>The dev action: this speaking animal says a line now (to its person, else warns of the nearest danger), past its waits.</summary>
        public static string DebugSay(Pawn a)
        {
            if (!On) return "animals don't speak now (the setting, or no fantasy source on Auto).";
            if (!Speaks(a, out var hearer, out var gift)) return a.LabelShort + " doesn't speak (" + GiftOf(a, out _) + ").";
            var map = a.Map;
            var danger = map.mapPawns.AllPawnsSpawned.Where(p => Raider(p, map) || Predator(p, map)).OrderBy(p => p.Position.DistanceToSquared(a.Position)).FirstOrDefault();
            if (danger != null && danger.Position.InHorDistOf(a.Position, AnimalNatureMath.WarnRange) && Warn(a, hearer, gift, danger, Raider(danger, map), true))
                return a.LabelShort + " warned of " + danger.LabelShort + ".";
            return Say(a, hearer, gift, true) ? a.LabelShort + " spoke." : "nobody it speaks to is with it now (its person, or for a hybrid anyone, within talking range).";
        }
    }
}
