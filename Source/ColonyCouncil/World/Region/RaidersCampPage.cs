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
    /// Where the colony sees its raiders' camps (World/Region/RaidersCamp.cs): Region → Map's Threats lens (each camp on
    /// the map with its line to the home it raids, and as a list: where, since when, its raids, its price with a way to
    /// pay it, how the last ones ended; UI/Region/LensModel.cs), the place card's section for the camp's site and for the
    /// home it threatens, and a line on This week while one stands. A camp not yet found shows nowhere. Its old page name,
    /// "RaidersCamps", forwards to the Threats lens's list.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RaidersCampPage
    {
        public const string Name = "RaidersCamps";

        static RaidersCampPage()
        {
            CardSections.PlaceLines(CardSlot.Now, 10f, SitePanel, "CC_RaidCampPg_PanelHeader", "raidcamp");
            CardSections.HomeLines(CardSlot.Now, 10f, HomePanel, "CC_RaidCampPg_PanelHeader", "raidcamp-home");
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- Words ----------------

        /// <summary>"A brigands' hold of the Kotar" with its leader, where it lies from its home, since when, its raids, its price.</summary>
        public static List<string> Lines(Map home, RaidersCamp c, bool withHome)
        {
            var lines = new List<string>();
            if (home == null || c == null || !c.Shown) return lines;
            var people = c.people?.Name ?? "?";
            string where = RaidersCamps.Where(home, c);
            lines.Add(withHome && Homes.All.Count > 1
                ? "CC_RaidCampPg_WhereHome".Translate(where.Named("WHERE"), Homes.Name(home).Named("HOME")).Resolve().CapitalizeFirst()
                : where.CapitalizeFirst() + ".");
            lines.Add("CC_RaidCampPg_Since".Translate(FactionMemory.AgoText(c.foundAt).Named("FOUND"), people.Named("PEOPLE")).Resolve());
            if (c.leader != null && !RaidersCamps.LeaderLost(c)) lines.Add("CC_RaidCampPg_Leader".Translate(c.leader.LabelShort.Named("LEADER")).Resolve());
            lines.Add(c.raids == 0 ? "CC_RaidCampPg_NoRaids".Translate().Resolve()
                : "CC_RaidCampPg_Raids".Translate(c.raids.Named("RAIDS"), c.beaten.Named("BEATEN")).Resolve());
            if (c.stage == CampStage.Paying) lines.Add("CC_RaidCampPg_Paying".Translate(c.price.Named("SILVER")).Resolve());
            else if (c.offered) lines.Add("CC_RaidCampPg_Price".Translate(c.price.Named("SILVER")).Resolve());
            else lines.Add("CC_RaidCampPg_NoTerms".Translate().Resolve());
            return lines;
        }

        /// <summary>
        /// The player's click on Pay (the Threats lens's list): pay their price now, after a word to confirm; the answer to
        /// the click itself is a direct message.
        /// </summary>
        internal static void AskToPay(Map home, RaidersCamp c)
        {
            string why = RaidersCampTerms.CantPay(home, c);
            if (why != null)
            {
                // direct: the answer to the player's own click.
                Messages.Message(why, MessageTypeDefOf.RejectInput, false);
                return;
            }
            string text = "CC_RaidCampPg_PayConfirm".Translate(c.kind.Named("KIND"), c.price.Named("SILVER")).Resolve();
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () =>
            {
                var now = RaidersCamps.CampOf(home);
                if (now == c) RaidersCamps.Pay(home, c);
                LensModel.Dirty();
            }));
        }

        // ---------------- The region map's panel ----------------

        /// <summary>A camp's site, on its card: what the camp is, whose, and its raids so far.</summary>
        private static List<string> SitePanel(PlanetTile tile, WorldObject wo)
        {
            if (!(wo is Site s)) return null;
            try
            {
                var c = RaidersCamps.CampAt(s, out var home);
                if (c == null) return null;
                var lines = Lines(home, c, true);
                string people = c.people != null ? ThingRefs.Link(c.people.Name, ThingRefs.Of(c.people)) : "?";
                lines.Insert(0, "CC_RaidCampPg_Title".Translate(c.label.Named("NAME"), people.Named("PEOPLE")).Resolve());
                return lines;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region panel: a raiders' camp", e);
                return null;
            }
        }

        /// <summary>A home a camp raids, on the home's card: the camp, where, and its raids so far.</summary>
        private static List<string> HomePanel(Map home)
        {
            try
            {
                var c = RaidersCamps.CampOf(home);
                if (c == null || !c.Shown) return null;
                string people = c.people != null ? ThingRefs.Link(c.people.Name, ThingRefs.Of(c.people)) : "?";
                return new List<string>
                {
                    "CC_RaidCampPg_HomeLine".Translate(c.kind.Named("KIND"), people.Named("PEOPLE"), RaidersCamps.Where(home, c).Named("WHERE")).Resolve(),
                    c.raids == 0 ? "CC_RaidCampPg_NoRaids".Translate().Resolve() : "CC_RaidCampPg_Raids".Translate(c.raids.Named("RAIDS"), c.beaten.Named("BEATEN")).Resolve()
                };
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region panel: a home's raiders' camp", e);
                return null;
            }
        }

        // ---------------- This week ----------------

        /// <summary>A line for each camp standing near a home, on the card of missions, camps and guests (UI/WeekModel.cs): no timer, its raids so far.</summary>
        public static void WeekRows(WeekCard card)
        {
            if (card == null || RegionBook.Instance == null) return;
            foreach (var (home, c) in RaidersCamps.Known())
            {
                var site = RaidersCamps.SiteOf(c);
                string text = "CC_RaidCampPg_Week".Translate(c.label.Named("NAME"), (c.people?.Name ?? "?").Named("PEOPLE")).Resolve();
                if (Homes.All.Count > 1) text = Homes.Name(home) + ": " + text;
                card.rows.Add(new WeekRow
                {
                    icon = Icons.Raid,
                    color = UiKit.Bad,
                    text = text,
                    note = c.stage == CampStage.Paying ? "CC_RaidCampPg_WeekPaying".Translate().Resolve()
                        : c.raids == 0 ? "CC_RaidCampPg_WeekNoRaid".Translate().Resolve() : "CC_RaidCampPg_WeekRaids".Translate(c.raids.Named("RAIDS")).Resolve(),
                    noteColor = UiKit.Bad,
                    tip = string.Join("\n", Lines(home, c, false)),
                    click = site != null ? WeekModel.Go(new GlobalTargetInfo(site)) : WeekModel.Page(Name),
                    order = -2
                });
            }
        }
    }
}
