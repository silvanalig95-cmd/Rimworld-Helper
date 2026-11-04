using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // The region's provider (World/Region/): their town's fortune and its founding as the world ledger keeps it, and a new
    // town near the colony that isn't theirs. It reads the records as they stand, with each part's setting.
    public static partial class Facts
    {
        /// <summary>
        /// Their town in detail: its fortune (town:struggling to town:flourishing, {FORTUNE}, with the fortunes on), and in
        /// its first year its founding (town:new; town:founder with {FOUNDER}, them:founder when the one spoken to led
        /// them; town:mother with {MOTHERTOWN}; town:why:KEY with {WHYFOUNDED}). A town founded near the colony in its
        /// first year that isn't theirs: region:newtown, region:newtown:theirs (of their people) or :other, {NEWTOWN},
        /// {NEWTOWNPEOPLE}.
        /// </summary>
        private static void Region(FactScene s, FactSet f)
        {
            // Trading posts near the colony (World/Region/TradingPostFacts.cs).
            Posts(s, f);
            int now = Now;
            var rec = s.place != null ? WorldLedger.TownRecord(s.place.ID) : null;
            if (rec != null && rec.gone < 0)
            {
                if (RegionBook.FortunesOn && rec.Seeded)
                {
                    var band = RegionMath.Band(rec.prosperity);
                    f.Tag(RegionMath.Tag(band));
                    f.Value("FORTUNE", RegionFortunes.Word(band, rec.people ?? s.people));
                }
                if (RegionBook.FacesOn && RegionMath.IsNew(rec.founded, now))
                {
                    f.Tag("town:new", FactsMath.NewTownHeat(Days(rec.founded)));
                    var founder = RegionFaces.FounderOf(rec);
                    if (founder != null && !founder.Discarded)
                    {
                        f.Tag("town:founder");
                        f.Value("FOUNDER", founder.LabelShort);
                        if (s.listener != null && s.listener == founder) f.Tag("them:founder", 2f);
                    }
                    if (!rec.motherName.NullOrEmpty())
                    {
                        f.Tag("town:mother");
                        f.Value("MOTHERTOWN", rec.motherName);
                    }
                    string why = RegionWords.Why(rec);
                    if (!why.NullOrEmpty())
                    {
                        f.Tag("town:why:" + rec.why);
                        f.Value("WHYFOUNDED", why);
                    }
                }
            }
            if (!RegionBook.FacesOn) return;
            var home = s.colony ?? s.speaker?.MapHeld;
            var near = RegionTowns.Of(home);
            var other = RegionFaces.NewTowns().FirstOrDefault(t => (s.place == null || t.id != s.place.ID) && !t.name.NullOrEmpty()
                                                                   && (near == null || near.towns.Any(x => x != null && x.ID == t.id)));
            if (other == null) return;
            f.Tag("region:newtown", FactsMath.NewTownHeat(Days(other.founded)));
            f.Tag(s.people != null && other.people == s.people ? "region:newtown:theirs" : "region:newtown:other");
            f.Value("NEWTOWN", other.name);
            if (other.people != null) f.Value("NEWTOWNPEOPLE", other.people.Name);
        }
    }
}
