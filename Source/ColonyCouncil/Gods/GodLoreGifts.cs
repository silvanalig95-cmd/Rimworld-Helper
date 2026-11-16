using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A gift the colony gave a god, as its book keeps it: when, under which name, what it was, who carried it and to which
    /// place, what became of it as the colony knows it (Found, Unseen, Plain, Seen, Given, Animal, Passerby, Returned,
    /// Spoiled), who found it or saw it, what was found in its place, whether it was something precious given with the
    /// colony's leave, and whose hand set it down. <see cref="god"/> is hidden: the god it went to, never shown.
    /// </summary>
    public class LoreGift : IExposable
    {
        public int tick;
        public int god = -1;
        public string page, what, plain, by, place, fate, who, returned, keeper;
        public bool asked;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref page, "page");
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref plain, "plain");
            Scribe_Values.Look(ref by, "by");
            Scribe_Values.Look(ref place, "place");
            Scribe_Values.Look(ref fate, "fate");
            Scribe_Values.Look(ref who, "who");
            Scribe_Values.Look(ref returned, "returned");
            Scribe_Values.Look(ref keeper, "keeper");
            Scribe_Values.Look(ref asked, "asked");
        }
    }

    /// <summary>
    /// A god's temper toward the colony as the book's keeper last read it (Gods/GodTemper.cs, GodTemperMath): the step it
    /// read (0 wrathful to 6 beloved; -1 never read), whether it was a guess with nothing to read it by (said as some say),
    /// what was given before it rose (a thing's name, for "since the steel was given"), when, and by whom. Words only on the
    /// page, never the step.
    /// </summary>
    public class LoreTemper : IExposable
    {
        public int god = -1, step = -1, tick = -1;
        public bool unsure;
        public string since, keeper;

        public void ExposeData()
        {
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref step, "step", -1);
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref unsure, "unsure");
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref keeper, "keeper");
        }
    }

    /// <summary>
    /// The colony's book keeps every gift it gave a god (the player, 16 November 2026: "the book should track the offerings
    /// and in this case also the god's favour should be tracked in the book, but the wording should also be kept a bit
    /// ominous, so not with a numeric value or so"): each gift taken, seen going or found gone, eaten, carried off or let go
    /// back, with what it was and where, under the name the colony gave it by (<see cref="Gave"/>), and what was found in its
    /// place (<see cref="Returned"/>); and each god's temper as its keeper reads it (<see cref="tempers"/>, Gods/GodTemper.cs).
    /// Both in the god's chapter (Gods/GodLorePage.cs). Saved with the book, capped (OfferingGiftMath.BookGifts, a god's
    /// BookGiftsAGod; one reading a god).
    /// </summary>
    public partial class GodLore
    {
        public List<LoreGift> gifts = new List<LoreGift>();
        public List<LoreTemper> tempers = new List<LoreTemper>();

        /// <summary>The book's gifts or a reading changed (for the page that keeps what it laid out).</summary>
        internal static void Changed() => Version++;

        private void ExposeGifts()
        {
            Scribe_Collections.Look(ref gifts, "gifts", LookMode.Deep);
            Scribe_Collections.Look(ref tempers, "tempers", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            gifts ??= new List<LoreGift>();
            gifts.RemoveAll(x => x == null || x.what.NullOrEmpty());
            tempers ??= new List<LoreTemper>();
            tempers.RemoveAll(x => x == null || x.god < 0);
        }

        /// <summary>
        /// A gift's fate, now the colony knows it: set down in the book under the name the colony gave it by (made a page if
        /// it is new), with what it was (<paramref name="what"/>, and its thing's name <paramref name="plain"/>), who carried it,
        /// the place, its fate and who found or saw it; precious ones with the colony's leave. Null with no book.
        /// </summary>
        public static LoreGift Gave(God g, Map map, string what, string plain, string by, string fate, string who, bool asked)
        {
            var b = Book;
            if (b == null || g == null || what.NullOrEmpty() || fate.NullOrEmpty()) return null;
            try
            {
                string name = b.PageOf(g)?.name ?? Gods.Name(g);
                string page = b.Page(name, g, "CC_GodGift_From".Translate().Resolve());
                if (page == null) return null;
                string place = Worship.ShrineOf(g);
                if (map != null && Homes.All.Count > 1) place = "CC_GodGift_PlaceAt".Translate(place.Named("PLACE"), (Homes.Name(map) ?? "").Named("HOME")).Resolve();
                var gift = new LoreGift
                {
                    tick = Now, god = g.id, page = page, what = what, plain = plain, by = by, place = place, fate = fate, who = who, asked = asked,
                    keeper = Keeper(out _)?.LabelShort
                };
                b.gifts.Add(gift);
                if (b.gifts.Count(x => x.god == g.id) > OfferingGiftMath.BookGiftsAGod) b.gifts.Remove(b.gifts.First(x => x.god == g.id));
                while (b.gifts.Count > OfferingGiftMath.BookGifts) b.gifts.RemoveAt(0);
                Version++;
                return gift;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the book, a gift", e);
                return null;
            }
        }

        /// <summary>Something was found where the gift lay: the book adds it to the gift.</summary>
        public static void Returned(LoreGift gift, string what)
        {
            if (gift == null || what.NullOrEmpty()) return;
            gift.returned = what;
            Version++;
        }

        /// <summary>The gifts the book keeps under a chapter (by its name, the names worked out to be one with it too), oldest first.</summary>
        public List<LoreGift> GiftsUnder(string title)
        {
            if (title.NullOrEmpty()) return new List<LoreGift>();
            return gifts.Where(x => x?.page != null && (Root(Named(x.page))?.name ?? x.page) == title).ToList();
        }

        /// <summary>The fates in which the god took the gift (seen or not).</summary>
        public static bool TakenFate(string fate) => fate == "Found" || fate == "Unseen" || fate == "Plain" || fate == "Seen" || fate == "Given";

        /// <summary>A god's reading, or null when the keeper never read it.</summary>
        public LoreTemper TemperOf(God g) => g == null ? null : tempers.FirstOrDefault(t => t != null && t.god == g.id);

        /// <summary>A god's reading, made if it has none yet (never read).</summary>
        internal LoreTemper TemperFor(God g)
        {
            var t = TemperOf(g);
            if (t != null || g == null) return t;
            tempers.Add(t = new LoreTemper { god = g.id });
            return t;
        }

        /// <summary>
        /// A gift in the book's words: what it was, who left it where (given with the colony's leave, for a precious one), what
        /// became of it as the colony knows it, and what was found in its place.
        /// </summary>
        public static string GiftWords(LoreGift x)
        {
            if (x == null) return "";
            string by = x.by.NullOrEmpty() ? "CC_GodLore_Someone".Translate().ToString() : x.by;
            string line = (x.asked ? "CC_GodGift_LineAsked" : "CC_GodGift_Line").Translate((x.what ?? "?").Named("WHAT"), by.Named("BY"), (x.place ?? "").Named("PLACE")).Resolve().CapitalizeFirst();
            string key = "CC_GodGift_Fate_" + x.fate + (x.who.NullOrEmpty() && (x.fate == "Seen" || x.fate == "Plain" || x.fate == "Found") ? "Nobody" : "");
            if (key.CanTranslate()) line += " " + key.Translate((x.who ?? "").Named("WHO")).Resolve();
            if (!x.returned.NullOrEmpty()) line += " " + "CC_GodGift_Returned".Translate(x.returned.Named("WHAT")).Resolve();
            return line;
        }
    }
}
