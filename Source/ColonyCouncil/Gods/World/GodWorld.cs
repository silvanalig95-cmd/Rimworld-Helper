using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods in the wider world (docs/design/gods.md, stage 2's "The world"; brainstorm items 17, 18, 19, 21, 22 and the
    /// drama between the gods), for the nine of docs/gods-pantheon-brainstorm.md, each in its own way (GodWays). The gods are
    /// unseen (the player's rule, docs/design/gods.md, "The gods are unseen"): nothing here shows what a god thinks, wants
    /// or feels about the colony, names a rule or a price, or puts a god's standing in words. The player meets what happens
    /// on the map and in the world, and what people say of it. Kept in the region's day and hour (World/Region/RegionBook.cs;
    /// no pass of its own), each part in a file of its own under Gods/World/:
    /// <list type="bullet">
    /// <item>GodLetters: neighbours' letters that swear by their gods and tell of signs, curses, myths and holy wars; an
    /// invitation to their god's feast; the colony's letter that slights their god.</item>
    /// <item>GodPeoples: peoples who share a god get on better, a feud that is really the gods'; a holy war that asks the
    /// colony in; a temple's envoy asking for a relic.</item>
    /// <item>GodCamps: pilgrims at a place one people calls holy, a hermit seer at the map's edge who says true things
    /// darkly.</item>
    /// <item>GodHealth: each god's kindness and anger at a home in its own shape (a fever broken, wild food springing up, a
    /// vein pushed up, a machine mended, a meteorite; blight, a cold snap, a heat wave, a flashstorm, a freezer failing, a
    /// short circuit), lightning or beasts for one who offended, Bellwether's kin harmed; under the shared harm budget.</item>
    /// <item>GodGifts: a dream told at breakfast, a night under the open sky, and a lasting gift that arrives without a word
    /// (A RimWorld of Magic's classes and spells, Vanilla Psycasts Expanded's paths, a green hand, gold for silver, a
    /// passion found); its price paid the same way; a rival's dream asking for it back.</item>
    /// <item>GodVoid: Undertow, the Hunger, which answers anyone desperate enough, always pays and always takes more.</item>
    /// <item>GodDrama: two gods' quarrel seen in the weather or on the ground, one hunted for their gods' quarrel asking to be
    /// hidden, a sentence of an old story told at supper.</item>
    /// </list>
    /// Everything calls only the core's <see cref="Gods"/> and the two seams; a god's moment goes through
    /// <see cref="GodPresenceSeam"/> and, while the presence part isn't there, a plain card that says what was seen
    /// (<see cref="Moment"/>).
    /// </summary>
    public static class GodWorld
    {
        public static bool On => Gods.On && GodWorldBook.Instance != null;

        public static int Now => Find.TickManager?.TicksGame ?? 0;

        public static GodWorldBook Book => Gods.On ? GodWorldBook.Instance : null;

        /// <summary>The dev tools' push: while set, every chance comes up, every gap is past and every question may be asked (GodWorldDebug).</summary>
        public static bool Force;

        /// <summary>A chance, which the dev tools can make come up.</summary>
        public static bool Roll(float chance) => Force || Rand.Chance(chance);

        /// <summary>One of Rimfolk's questions may come now (Tell.CanAsk), or the dev tools ask it.</summary>
        public static bool CanAsk => Force || Tell.CanAsk;

        /// <summary>The scale on a chance for how real the gods are; 0 for what only a real god can do while they are only believed.</summary>
        public static float Factor(bool supernatural = false) => GodWorldMath.Factor(Gods.Realness, supernatural);

        // ================= The day and the hour =================

        /// <summary>The region's day (World/Region/RegionBook.cs).</summary>
        public static void Daily(int now)
        {
            var b = Book;
            if (b == null) return;
            Safe("letters", () => GodLetters.Daily(b, now));
            Safe("peoples", () => GodPeoples.Daily(b, now));
            Safe("camps", () => GodCamps.Daily(b, now));
            Safe("gifts", () => GodGifts.Daily(b, now));
            Safe("wrath", () => GodHealth.Daily(b, now));
            Safe("the void", () => GodVoid.Daily(b, now));
            Safe("the drama", () => GodDrama.Daily(b, now));
            Safe("tidying", b.Prune);
        }

        /// <summary>The region's hour.</summary>
        public static void Hourly(int now)
        {
            var b = Book;
            if (b == null) return;
            Safe("the envoys' hour", () => GodPeoples.Hour(b, now));
            Safe("blessings and lightning", () => GodHealth.Hourly(b, now));
            Safe("gifts received", () => GodGifts.Hourly(b, now));
            Safe("the void's ear", () => GodVoid.Hourly(b, now));
            Safe("the drama's hour", () => GodDrama.Hourly(b, now));
        }

        public static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: " + what, e);
            }
        }

        // ================= Words =================

        /// <summary>A key with a god's placeholders ({GOD}, {GODEPITHET}, {GODOF}) and more.</summary>
        public static string T(string key, God g, params NamedArgument[] more)
        {
            var args = new List<NamedArgument>(Gods.Args(g));
            if (more != null) args.AddRange(more);
            return key.Translate(args.ToArray()).Resolve();
        }

        /// <summary>The colony's home where something happens, or its seat.</summary>
        public static Map HomeOr(Map map) => map != null && Homes.IsHome(map) ? map : Homes.Seat;

        /// <summary>The colony's grown people up and about on a home map.</summary>
        public static List<Pawn> Grown(Map map) =>
            map == null
                ? new List<Pawn>()
                : map.mapPawns.FreeColonistsSpawned.Where(p => p != null && !p.Dead && !p.Downed && p.DevelopmentalStage.Adult() && !p.IsQuestLodger()).ToList();

        /// <summary>One of the colony on the map who could say a word (awake, up, not fighting), nearest the cell when one is given.</summary>
        public static Pawn Witness(Map map, IntVec3 near, Pawn not = null)
        {
            var all = Grown(map).Where(p => p != not && p.Awake() && !p.Drafted && !p.InMentalState).ToList();
            if (all.Count == 0) return null;
            return near.IsValid ? all.OrderBy(p => p.Position.DistanceToSquared(near)).First() : all.RandomElement();
        }

        /// <summary>
        /// A colonist's one sentence about what just happened (Rimfolk_Pool_GodWorld, by the moment's tag, how real the gods
        /// are, and the god's own key and domain), over their head through Speech.Say; to nobody in particular. This is where
        /// a god is named, if anywhere: a believer's guess, a scoffer's doubt. False when nothing was said.
        /// </summary>
        public static bool Say(Pawn p, string moment, God g, params NamedArgument[] more)
        {
            if (p == null || !p.Spawned || moment.NullOrEmpty()) return false;
            try
            {
                var tags = new List<string> { moment, "real:" + GodWorldMath.Voice(Gods.Realness).ToLowerInvariant() };
                if (g != null)
                {
                    tags.AddRange(GodSpeech.Tags(g));
                    tags.AddRange(GodWays.Tags(g));
                }
                string line = Flavor.FromPool(Pool, p, null, null, tags.ToArray());
                if (line.NullOrEmpty()) return false;
                var args = new List<NamedArgument> { p.Named("PAWN") };
                if (g != null) args.AddRange(Gods.Args(g));
                if (more != null) args.AddRange(more);
                return Speech.Say(p, line.Formatted(args.ToArray()).Resolve().CapitalizeFirst());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a word about a god", e);
                return false;
            }
        }

        public const string Pool = "Rimfolk_Pool_GodWorld";

        /// <summary>One who saw it says so: the colonist nearest the cell.</summary>
        public static void Remark(Map map, IntVec3 near, string moment, God g, Pawn not = null, params NamedArgument[] more)
        {
            var w = Witness(map, near, not);
            if (w != null) Say(w, moment, g, more);
        }

        // ================= A god's moment =================

        /// <summary>
        /// A god's moment on the map: through the presence part (Gods/Presence/: the sound, the sky, its own card) when it is
        /// there, else a plain card: a letter for what matters (<paramref name="letter"/>), else a message. The caller's words
        /// say only what was seen and heard, with no why and no god's name. Remembered among the god's acts either way.
        /// </summary>
        public static void Moment(God g, Map map, PresenceKind kind, IntVec3 cell, Pawn avatar, string title, string text, bool letter = true,
            LetterDef def = null, LookTargets look = null, Faction faction = null, Importance importance = Importance.Notable)
        {
            if (g == null || text.NullOrEmpty()) return;
            var home = HomeOr(map);
            look ??= cell.IsValid && map != null ? new LookTargets(new TargetInfo(cell, map)) : avatar != null && avatar.Spawned ? new LookTargets(avatar) : LookTargets.Invalid;
            bool shown = false;
            try
            {
                var spec = PresenceSpec.For(g, map, kind, cell.IsValid ? cell : default, avatar);
                spec.title = title;
                spec.text = text;
                shown = GodPresenceSeam.Call(spec);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a god's moment", e);
            }
            if (!shown)
            {
                // The plain card while the presence part isn't there: a letter for what matters, else a message as it happens.
                if (letter) ColonyCouncil.Tell.Letter(NewsKind.World, importance, map ?? home, title, text, def ?? LetterDefOf.NeutralEvent, look, faction);
                else
                    ColonyCouncil.Tell.Message(NewsKind.World, importance, map ?? home, text, look,
                        kind == PresenceKind.Blessing ? MessageTypeDefOf.PositiveEvent : kind == PresenceKind.Wrath ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.NeutralEvent);
            }
            Gods.Remember(new GodAct
            {
                god = g.id, home = Gods.HomeKey(home), tick = Now, kind = ActKind(kind), text = FirstSentence(text), cell = cell
            });
        }

        private static GodActKind ActKind(PresenceKind k) => k switch
        {
            PresenceKind.Appear => GodActKind.Appear,
            PresenceKind.Reveal => GodActKind.Reveal,
            PresenceKind.Depart => GodActKind.Depart,
            PresenceKind.Blessing => GodActKind.Blessing,
            PresenceKind.Wrath => GodActKind.Wrath,
            PresenceKind.Omen => GodActKind.Omen,
            _ => GodActKind.Act
        };

        public static string FirstSentence(string text)
        {
            if (text.NullOrEmpty()) return "";
            int cut = text.IndexOf(". ", StringComparison.Ordinal);
            if (cut < 0) cut = text.IndexOf(".\n", StringComparison.Ordinal);
            string s = cut > 0 && cut < 260 ? text.Substring(0, cut + 1) : text.Length > 260 ? text.Substring(0, 260) + "..." : text;
            return s.Replace("\n", " ");
        }

        /// <summary>
        /// An act of the god remembered in words only, without a card (the colony's book of the gods is written from these,
        /// with the colonists who could see it: Gods.Remember). Always in the world's voice: what was seen, heard or told and
        /// by whom ("as Mirel of Karond-Keth wrote"), never what the god felt; a second god where two are in it.
        /// </summary>
        public static void Remember(God g, Map map, GodActKind kind, string text, God other = null, IntVec3? cell = null)
        {
            if (g == null || text.NullOrEmpty()) return;
            Gods.Remember(new GodAct { god = g.id, other = other?.id ?? -1, home = Gods.HomeKey(HomeOr(map)), tick = Now, kind = kind, text = text, cell = cell ?? IntVec3.Invalid });
        }

        /// <summary>A line for the colony's story (the one history).</summary>
        public static void Chronicle(string text, Pawn about = null)
        {
            if (text.NullOrEmpty()) return;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, about));
        }

        /// <summary>The colony's wealth as a factor near 1 for prices and gifts (LivingEvents).</summary>
        public static float Wealth(Map map)
        {
            try
            {
                return LivingEvents.WealthFactor(map);
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>The gods the colony knows of.</summary>
        public static IEnumerable<God> Known => Gods.All.Where(Gods.Known);

        /// <summary>The colony's gods at a home and the ones that have noticed it there.</summary>
        public static List<God> Near(Map home) => Gods.ColonyGods(home).Concat(Known.Where(g => Gods.Favour(g, home) >= FavourWord.Noticed)).Distinct().ToList();
    }
}
