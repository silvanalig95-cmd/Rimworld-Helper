using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Every choice letter of Rimfolk's own. It is the game's choice letter in every way (the stack, its sound and colour,
    /// its timeout, the History tab, its choices and what they do); only opening it is Rimfolk's: the event card while
    /// Rimfolk's own look and "Rimfolk's event cards" are on, the game's letter window otherwise.
    /// </summary>
    public abstract class RimfolkLetter : ChoiceLetter
    {
        public override void OpenLetter()
        {
            if (EventCards.TryOpen(this)) return;
            base.OpenLetter();
        }

        /// <summary>
        /// What the card shows beyond the letter's own data: who and what it is about, what the council leans to, the
        /// colony's custom among its answers (<paramref name="facts"/>; its answers' indexes are those of the options the
        /// letter offered, in order). Read once, when the card opens. Who its look targets name and its people are added
        /// after, so a letter adds only what those don't say.
        /// </summary>
        public virtual void Describe(EventCardFacts facts)
        {
        }
    }

    /// <summary>What a letter tells its card beyond the game's own data, gathered when the card opens.</summary>
    public sealed class EventCardFacts
    {
        /// <summary>Who it is about, the most important first.</summary>
        public readonly List<Pawn> people = new List<Pawn>();

        /// <summary>The people (faction) it is from or about.</summary>
        public Faction faction;

        /// <summary>The thing it is about: one on the map, or a kind of thing.</summary>
        public Thing thing;

        public ThingDef thingDef;

        /// <summary>The answer the council leans to (an option's index), and the line saying so; -1 and null where it has no view.</summary>
        public int lean = -1;

        public string leanText;

        /// <summary>Those for each answer (a vote), by the option's index.</summary>
        public readonly Dictionary<int, List<Pawn>> backers = new Dictionary<int, List<Pawn>>();

        /// <summary>The answer the colony's custom keeps (a dilemma that is a custom of the colony's), and its words.</summary>
        public int custom = -1;

        public string customText;

        public void Person(Pawn p)
        {
            if (p != null && !p.Destroyed && !people.Contains(p)) people.Add(p);
        }

        public void Backer(int option, Pawn p)
        {
            if (p == null || option < 0) return;
            if (!backers.TryGetValue(option, out var list)) backers[option] = list = new List<Pawn>();
            if (!list.Contains(p)) list.Add(p);
        }

        /// <summary>Who and what a question's context names: its two, the stranger and the second person, the pet, those who came with them; its people, the thing.</summary>
        public void Context(DilemmaContext ctx)
        {
            if (ctx == null) return;
            Person(ctx.a);
            Person(ctx.b);
            if (ctx is SituationContext s) Person(s.other);
            Person(ctx.stranger);
            Person(ctx.pet);
            if (ctx is HappeningContext h)
            {
                if (h.group != null)
                    foreach (var p in h.group)
                        Person(p);
                if (thing == null && h.place != null && !h.place.Destroyed && !(h.place is Pawn)) thing = h.place;
                if (thing == null && h.things != null) thing = h.things.FirstOrDefault(t => t != null && !t.Destroyed && !(t is Pawn));
                faction ??= h.enemy;
            }
            faction ??= ctx.faction ?? Strangers.FactionOf(ctx.stranger);
            thingDef ??= ctx.thing;
        }

        /// <summary>
        /// The custom among a question's answers: the answer that keeps it, when the question is a custom of the colony's (its
        /// tradition, or a custom kept before), with how it stands.
        /// </summary>
        public void Custom(DilemmaContext ctx)
        {
            var def = ctx?.def;
            if (def == null || def.traditionOption < 0 || def.traditionOption >= def.options.Count) return;
            var tracker = DilemmaTracker.ForQuestion(ctx);
            if (tracker == null) return;
            if (tracker.IsTradition(def)) customText = "CC_EventCard_Custom_Tradition".Translate((def.traditionLabel ?? def.label).Named("CUSTOM")).Resolve();
            else if (tracker.CustomDay(def) != null)
                customText = "CC_EventCard_Custom_Kept".Translate((def.traditionLabel ?? def.label).Named("CUSTOM"), tracker.TraditionCount(def).Named("YEARS")).Resolve();
            else return;
            custom = def.traditionOption;
        }
    }

    /// <summary>
    /// Rimfolk's event cards (docs/design/rimfolk-look.md, stage 3, point 1): whether a letter opens as one, and which of
    /// the letters made of the game's own plain class are Rimfolk's (its LetterDefs, and the letters the teller sends with
    /// the game's kinds, kept by their ID in <see cref="EventCardsBook"/>). The game's own letters and other mods' are never
    /// touched.
    /// </summary>
    public static class EventCards
    {
        /// <summary>"Rimfolk's event cards" (on at first): Rimfolk's letters open as its cards while its own look is on.</summary>
        public static bool On => WindowFrame.OwnLookOn && (ColonyCouncilMod.Settings?.eventCards ?? true) && Current.Game != null;

        /// <summary>Set while a card that failed gives the letter back to the game's own window.</summary>
        private static bool toGame;

        /// <summary>Opens the letter as Rimfolk's card, if the cards are on; false leaves it to the game's own window.</summary>
        public static bool TryOpen(ChoiceLetter letter)
        {
            if (toGame || letter == null || !On) return false;
            try
            {
                EventCardWindow.Open(letter);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("event card: opening", e);
                return false;
            }
        }

        /// <summary>The game's own window for a letter whose card can't be drawn (it closed itself with an error).</summary>
        public static void OpenInGame(ChoiceLetter letter)
        {
            if (letter == null) return;
            toGame = true;
            try
            {
                letter.OpenLetter();
            }
            finally
            {
                toGame = false;
            }
        }

        /// <summary>A letter that only tells, sent by Rimfolk with one of the game's kinds: kept, so it opens as a card too.</summary>
        public static void Mark(Letter letter)
        {
            if (letter == null) return;
            try
            {
                EventCardsBook.Instance?.Mark(letter.ID);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("event card: keeping a plain letter", e);
            }
        }

        /// <summary>
        /// A letter of the game's plain class that is Rimfolk's: one of Rimfolk's own LetterDefs, or one the teller sent and
        /// kept (<see cref="Mark"/>).
        /// </summary>
        public static bool IsRimfolks(Letter letter)
        {
            if (letter == null) return false;
            if (letter.def?.modContentPack != null && letter.def.modContentPack == ColonyCouncilMod.Pack) return true;
            return EventCardsBook.Instance?.Has(letter.ID) ?? false;
        }
    }

    /// <summary>
    /// The plain letters of Rimfolk's (made of the game's StandardLetter, with one of the game's kinds) by their ID, so they
    /// open as event cards after a load too: the newest <see cref="EventCardMath.MaxPlain"/>, those no longer on the stack
    /// or in the History tab let go at each save.
    /// </summary>
    public class EventCardsBook : GameComponent
    {
        private List<int> plain = new List<int>();
        [Unsaved] private HashSet<int> set;

        public EventCardsBook(Game game)
        {
        }

        public static EventCardsBook Instance => Current.Game?.GetComponent<EventCardsBook>();

        private HashSet<int> Set => set ??= new HashSet<int>(plain);

        public void Mark(int id)
        {
            if (!Set.Add(id)) return;
            plain.Add(id);
            if (plain.Count > EventCardMath.MaxPlain + 40) Trim(null);
        }

        public bool Has(int id) => Set.Contains(id);

        /// <summary>Keeps those given still present (null: every one), the newest.</summary>
        private void Trim(ICollection<int> present)
        {
            plain = EventCardMath.Keep(plain, present);
            set = null;
        }

        /// <summary>The IDs of the letters on the stack and in the History tab.</summary>
        private static HashSet<int> Present()
        {
            var ids = new HashSet<int>();
            var stack = Find.LetterStack?.LettersListForReading;
            if (stack != null)
                foreach (var l in stack)
                    if (l != null)
                        ids.Add(l.ID);
            var archive = Find.Archive?.ArchivablesListForReading;
            if (archive != null)
                foreach (var a in archive)
                    if (a is Letter l)
                        ids.Add(l.ID);
            return ids;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    Trim(Present());
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("event card: the plain letters before a save", e);
                }
            }
            Scribe_Collections.Look(ref plain, "plainLetters", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                plain ??= new List<int>();
                set = null;
            }
        }
    }

    /// <summary>
    /// Rimfolk's letters made of the game's plain class (StandardLetter: its LetterDefs for world news, and what the teller
    /// sends with the game's kinds) open as event cards, as Rimfolk's own letter classes do by their override. Only the
    /// plain class, only Rimfolk's; every other letter goes to the game's window untouched.
    /// </summary>
    [HarmonyPatch(typeof(ChoiceLetter), nameof(ChoiceLetter.OpenLetter))]
    public static class Patch_EventCardPlainLetter
    {
        public static bool Prefix(ChoiceLetter __instance)
        {
            if (!RimfolkPatches.On || __instance == null || __instance.GetType() != typeof(StandardLetter)) return true;
            try
            {
                return !(EventCards.IsRimfolks(__instance) && EventCards.TryOpen(__instance));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("event card: a plain letter", e);
                return true;
            }
        }
    }
}
