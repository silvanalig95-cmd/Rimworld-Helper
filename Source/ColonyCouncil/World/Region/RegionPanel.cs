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
    /// What the region map's panel shows of the region (World/NeighbourPanel.cs): for a town in detail, its fortune in
    /// words with what moved it, and for a new town who founded it, from where and why, and its founding families; for
    /// a town being built within reach, its settlers and who leads them; for a home, how remote it is. Built from the
    /// ledger's records each time the panel draws a place (a handful of lines; nothing when the window is closed).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RegionPanel
    {
        private static readonly Color Accent = new Color(0.78f, 0.86f, 0.7f);

        static RegionPanel()
        {
            NeighbourPanel.Sections.Add(DrawTown);
            NeighbourPanel.PlaceSections.Add(DrawSite);
            NeighbourPanel.HomeSections.Add(DrawHome);
            NeighbourPanel.HomeSections.Add(DrawFarNews);
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The lines for a town: its fortune, its founding, its families. Empty when the region knows nothing of it.</summary>
        public static List<string> TownLines(Settlement s)
        {
            var lines = new List<string>();
            if (s == null) return lines;
            var rec = WorldLedger.TownRecord(s.ID);
            if (rec == null || rec.gone >= 0) return lines;
            if (RegionBook.FortunesOn && rec.Seeded) lines.Add("CC_Region_Panel_Fortune".Translate(RegionFortunes.Line(rec)).Resolve());
            if (!RegionBook.FacesOn || rec.founded < 0) return lines;
            var founder = RegionFaces.FounderOf(rec);
            string ago = FactionMemory.AgoText(rec.founded);
            lines.Add(founder != null
                ? "CC_Region_Panel_Founded".Translate(ago.Named("AGO"), founder.LabelShort.Named("FOUNDER"), RegionFaces.TitleOf(founder, s.Faction).Named("TITLE")).Resolve()
                : "CC_Region_Panel_Founded_NoFounder".Translate(ago.Named("AGO")).Resolve());
            if (!rec.motherName.NullOrEmpty() && rec.why != "mother") lines.Add("CC_Region_Panel_From".Translate(rec.motherName.Named("MOTHER")).Resolve());
            string why = RegionWords.Why(rec);
            if (!why.NullOrEmpty()) lines.Add("CC_Region_Panel_Why".Translate(why.Named("WHY")).Resolve());
            var houses = WorldLedger.HousesOf(s.ID).Where(h => h.founding).ToList();
            if (houses.Count > 0)
            {
                var named = houses.Select(h => h.members.Count == 0
                    ? RegionWords.HouseName(h)
                    : "CC_Region_Panel_Members".Translate(RegionWords.HouseName(h).Named("HOUSE"), string.Join(", ", h.members).Named("MEMBERS")).Resolve()).ToList();
                lines.Add("CC_Region_Panel_Families".Translate(TextUtil.JoinAnd(named).Named("FAMILIES")).Resolve());
            }
            return lines;
        }

        private static float DrawTown(Rect rect, Settlement s)
        {
            if (s == null || !RegionBook.FortunesOn && !RegionBook.FacesOn) return 0f;
            try
            {
                return Draw(rect, TownLines(s), "CC_Region_Panel_Header");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region panel: a town", e);
                return 0f;
            }
        }

        /// <summary>A town being built within reach: its settlers, and who leads them.</summary>
        private static float DrawSite(Rect rect, PlanetTile tile, WorldObject wo)
        {
            if (wo == null || !RegionBook.FacesOn) return 0f;
            try
            {
                var f = WorldLedger.FoundingAt(wo.ID);
                if (f == null || f.built >= 0 || f.people == null) return 0f;
                var founder = WorldLedger.PawnOf(f.founder);
                var lines = new List<string>
                {
                    founder != null
                        ? "CC_Region_Panel_Site_Founder".Translate(f.people.Name.Named("PEOPLE"), founder.LabelShort.Named("FOUNDER")).Resolve()
                        : "CC_Region_Panel_Site".Translate(f.people.Name.Named("PEOPLE")).Resolve()
                };
                if (!f.motherName.NullOrEmpty() && f.why != "mother") lines.Add("CC_Region_Panel_From".Translate(f.motherName.Named("MOTHER")).Resolve());
                string why = RegionWords.Why(new LedgerTown { why = f.why, motherName = f.motherName });
                if (!why.NullOrEmpty()) lines.Add("CC_Region_Panel_Why".Translate(why.Named("WHY")).Resolve());
                return Draw(rect, lines, "CC_Region_Panel_SiteHeader");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region panel: a town being built", e);
                return 0f;
            }
        }

        /// <summary>A home: how remote it is, from the nearest town at peace with the colony, and how many towns within reach are known in detail.</summary>
        private static float DrawHome(Rect rect, Map home)
        {
            try
            {
                var r = RegionTowns.Of(home);
                if (r == null) return 0f;
                var lines = new List<string> { ("CC_Region_Panel_Remote_" + RegionMath.RemoteLevel(r.remoteness)).Translate().Resolve() };
                lines.Add(r.nearestPeaceful >= 0f
                    ? "CC_Region_Panel_RemoteNearest".Translate(Mathf.RoundToInt(r.nearestPeaceful), r.peacefulWithin).Resolve()
                    : "CC_Region_Panel_RemoteNone".Translate().Resolve());
                if (r.towns.Count > 0 && (RegionBook.FortunesOn || RegionBook.FacesOn)) lines.Add("CC_Region_Panel_InDetail".Translate(r.towns.Count).Resolve());
                lines.AddRange(TravelLines(home));
                return Draw(rect, lines, "CC_Region_Panel_HomeHeader");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region panel: a home", e);
                return 0f;
            }
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
        private static float DrawFarNews(Rect rect, Map home)
        {
            if (!TravelLand.NewsOn) return 0f;
            try
            {
                var heard = FarNews.Heard(4);
                if (heard.Count == 0) return 0f;
                var lines = heard.Select(h => "CC_Travel_Panel_FarLine".Translate((h.townName ?? "?").Named("TOWN"), h.text.Named("TEXT"), FactionMemory.AgoText(h.tick).Named("AGO")).Resolve()).ToList();
                return Draw(rect, lines, "CC_Travel_Panel_FarHeader");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region panel: far news", e);
                return 0f;
            }
        }

        private static float Draw(Rect rect, List<string> lines, string header)
        {
            if (lines == null || lines.Count == 0) return 0f;
            float y = rect.y;
            UiKit.LabelFit(new Rect(rect.x, y, rect.width, 22f), header.Translate(), Accent);
            y += 24f;
            foreach (var l in lines) y += UiKit.Paragraph(rect.x, y, rect.width, l, Color.white, GameFont.Small) + 2f;
            return y + 2f - rect.y;
        }
    }
}
