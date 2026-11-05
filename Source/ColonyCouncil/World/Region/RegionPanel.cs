using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a place's card shows of the region (UI/Cards/, what the region map's panel showed): for a town in detail, its
    /// fortune in words with what moved it (Now), and for a new town who founded it, from where and why, and its founding
    /// families (Story, each a link to its card); for a town being built within reach, its settlers and who leads them;
    /// for a home, how remote it is, and the word from far towns. Lines asked for once a second while the card shows
    /// (nothing when no card does).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RegionPanel
    {
        static RegionPanel()
        {
            CardSections.TownLines(CardSlot.Now, 20f, s => Safe("a town's fortune", () => FortuneLines(s)), null, "fortune");
            CardSections.TownLines(CardSlot.Story, 10f, s => Safe("a town's founding", () => FoundingLines(s, true)), "CC_Card_Head_Founding", "founding");
            CardSections.PlaceLines(CardSlot.Now, 30f, (tile, wo) => Safe("a town being built", () => SiteLines(wo)), "CC_Region_Panel_SiteHeader", "site");
            CardSections.HomeLines(CardSlot.Now, 40f, home => Safe("a home", () => HomeLines(home)), "CC_Region_Panel_HomeHeader", "remote");
            CardSections.HomeLines(CardSlot.Words, 30f, home => Safe("far news", FarNewsLines), "CC_Travel_Panel_FarHeader", "farnews");
        }

        private static List<string> Safe(string what, Func<List<string>> make)
        {
            try
            {
                return make();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region panel: " + what, e);
                return null;
            }
        }

        private static string Link(string label, Ref r) => r.IsNone ? label : ThingRefs.Link(label, ThingRef.From(r));

        /// <summary>A town's fortune in words with what moved it, or nothing.</summary>
        public static List<string> FortuneLines(Settlement s)
        {
            var lines = new List<string>();
            var rec = s == null ? null : WorldLedger.TownRecord(s.ID);
            if (rec == null || rec.gone >= 0) return lines;
            if (RegionBook.FortunesOn && rec.Seeded) lines.Add("CC_Region_Panel_Fortune".Translate(RegionFortunes.Line(rec)).Resolve());
            return lines;
        }

        /// <summary>
        /// A new town's founding: when and by whom, from which town, why, and its founding families; with
        /// <paramref name="links"/>, the founder, the mother town and the families as links to their cards (Rimfolk's
        /// windows only).
        /// </summary>
        public static List<string> FoundingLines(Settlement s, bool links)
        {
            var lines = new List<string>();
            var rec = s == null ? null : WorldLedger.TownRecord(s.ID);
            if (rec == null || rec.gone >= 0 || !RegionBook.FacesOn || rec.founded < 0) return lines;
            var founder = RegionFaces.FounderOf(rec);
            string ago = FactionMemory.AgoText(rec.founded);
            lines.Add(founder != null
                ? "CC_Region_Panel_Founded".Translate(ago.Named("AGO"), (links ? Link(founder.LabelShort, rec.founder.IsNone ? Refs.Of(founder) : rec.founder) : founder.LabelShort).Named("FOUNDER"),
                    RegionFaces.TitleOf(founder, s.Faction).Named("TITLE")).Resolve()
                : "CC_Region_Panel_Founded_NoFounder".Translate(ago.Named("AGO")).Resolve());
            if (!rec.motherName.NullOrEmpty() && rec.why != "mother")
                lines.Add("CC_Region_Panel_From".Translate((links ? Link(rec.motherName, rec.mother) : rec.motherName).Named("MOTHER")).Resolve());
            string why = RegionWords.Why(rec);
            if (!why.NullOrEmpty()) lines.Add("CC_Region_Panel_Why".Translate(why.Named("WHY")).Resolve());
            var houses = WorldLedger.HousesOf(s.ID).Where(h => h.founding).ToList();
            if (houses.Count > 0)
            {
                var named = houses.Select(h =>
                {
                    // A family's name, no link: houses have no card since 5 November 2026 (their town is what counts).
                    string name = RegionWords.HouseName(h);
                    return h.members.Count == 0
                        ? name
                        : "CC_Region_Panel_Members".Translate(name.Named("HOUSE"), string.Join(", ", h.members).Named("MEMBERS")).Resolve();
                }).ToList();
                lines.Add("CC_Region_Panel_Families".Translate(TextUtil.JoinAnd(named).Named("FAMILIES")).Resolve());
            }
            return lines;
        }

        /// <summary>The lines for a town as the region map's panel had them: its fortune, its founding, its families (plain words).</summary>
        public static List<string> TownLines(Settlement s)
        {
            var lines = FortuneLines(s);
            lines.AddRange(FoundingLines(s, false));
            return lines;
        }

        /// <summary>A town being built within reach: its settlers, and who leads them.</summary>
        private static List<string> SiteLines(WorldObject wo)
        {
            if (wo == null || !RegionBook.FacesOn) return null;
            var f = WorldLedger.FoundingAt(wo.ID);
            if (f == null || f.built >= 0 || f.people == null) return null;
            var founder = WorldLedger.PawnOf(f.founder);
            string people = ThingRefs.Link(f.people.Name, ThingRefs.Of(f.people));
            var lines = new List<string>
            {
                founder != null
                    ? "CC_Region_Panel_Site_Founder".Translate(people.Named("PEOPLE"), Link(founder.LabelShort, f.founder).Named("FOUNDER")).Resolve()
                    : "CC_Region_Panel_Site".Translate(people.Named("PEOPLE")).Resolve()
            };
            if (!f.motherName.NullOrEmpty() && f.why != "mother") lines.Add("CC_Region_Panel_From".Translate(Link(f.motherName, f.mother).Named("MOTHER")).Resolve());
            string why = RegionWords.Why(new LedgerTown { why = f.why, motherName = f.motherName });
            if (!why.NullOrEmpty()) lines.Add("CC_Region_Panel_Why".Translate(why.Named("WHY")).Resolve());
            return lines;
        }

        /// <summary>A home: how remote it is, from the nearest town at peace with the colony, and how many towns within reach are known in detail.</summary>
        private static List<string> HomeLines(Map home)
        {
            var r = RegionTowns.Of(home);
            if (r == null) return null;
            var lines = new List<string> { ("CC_Region_Panel_Remote_" + RegionMath.RemoteLevel(r.remoteness)).Translate().Resolve() };
            lines.Add(r.nearestPeaceful >= 0f
                ? "CC_Region_Panel_RemoteNearest".Translate(Mathf.RoundToInt(r.nearestPeaceful), r.peacefulWithin).Resolve()
                : "CC_Region_Panel_RemoteNone".Translate().Resolve());
            if (r.towns.Count > 0 && (RegionBook.FortunesOn || RegionBook.FacesOn)) lines.Add("CC_Region_Panel_InDetail".Translate(r.towns.Count).Resolve());
            lines.AddRange(TravelLines(home));
            return lines;
        }

        /// <summary>
        /// A home's travellers (World/Region/TravelLand.cs): how much more often they come for its remoteness, and what of
        /// its land draws them.
        /// </summary>
        public static List<string> TravelLines(Map home)
        {
            var lines = new List<string>();
            if (home == null) return lines;
            int more = Mathf.RoundToInt((TravelLand.VisitorFactor(home) - 1f) * 100f);
            if (more >= 5) lines.Add("CC_Travel_Panel_Pull".Translate(more.Named("PCT")).Resolve());
            var land = TravelLand.Read(home);
            var draws = TravelMath.Reasons.Where(land.Has).Select(land.What).Where(w => !w.NullOrEmpty()).ToList();
            if (draws.Count > 0) lines.Add("CC_Travel_Panel_Draws".Translate(TextUtil.JoinAnd(draws).Named("WHAT")).Resolve());
            return lines;
        }

        /// <summary>The news of far towns travellers told lately (FarNews), newest first: what the far world does.</summary>
        private static List<string> FarNewsLines()
        {
            if (!TravelLand.NewsOn) return null;
            var heard = FarNews.Heard(4);
            if (heard.Count == 0) return null;
            return heard.Select(h => "CC_Travel_Panel_FarLine".Translate((h.townName ?? "?").Named("TOWN"), h.text.Named("TEXT"), FactionMemory.AgoText(h.tick).Named("AGO")).Resolve()).ToList();
        }
    }
}
