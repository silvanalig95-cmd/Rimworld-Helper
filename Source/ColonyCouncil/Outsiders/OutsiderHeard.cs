using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town's "Heard lately": the news its people told the colony's people in their talks (OutsiderTalk.Heard), newest
    /// first, each with who told whom and when. Registered here in the town's card (its Words slot); the lines, with the
    /// colonist told and the town as links, are made here for their people's page on the region window's Peoples too (the
    /// Overview's newest few, each town's newest on the Towns tab, the news naming a notable on the People tab:
    /// UI/Region/PeoplePage.cs). The history keeps each as "world:heard".
    /// </summary>
    [StaticConstructorOnStartup]
    public static class OutsiderHeard
    {
        static OutsiderHeard()
        {
            CardSections.TownLines(CardSlot.Words, 10f, CardLines, "CC_Out_Heard_Header", "heard");
        }

        /// <summary>The town's news on its card (Words), each line with the colonist who was told as a link to their card.</summary>
        private static List<string> CardLines(Settlement s)
        {
            if (s == null || !Outsiders.On) return null;
            try
            {
                var list = OutsiderTalk.HeardAt(s.ID, OutsiderTalkMath.HeardShown);
                return list.Count == 0 ? null : Lines(list, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heard lately on a town's card", e);
                return null;
            }
        }

        /// <summary>
        /// News heard as lines to draw with links: the colonist told a link to their card, and with <paramref name="withTown"/>
        /// the town it came from a link to its card (a people with no town names the people); with <paramref name="onMap"/>, news
        /// that named a place ends with "Show on map" (World/HeardPlaces.cs).
        /// </summary>
        public static List<string> Lines(List<HeardNews> list, bool withTown, bool onMap = true)
        {
            var lines = new List<string>();
            if (list == null || list.Count == 0) return lines;
            var names = new List<KeyValuePair<string, string>>();
            foreach (var p in PawnsFinder.AllMaps_FreeColonists)
                if (p != null && !p.LabelShort.NullOrEmpty())
                    names.Add(new KeyValuePair<string, string>(p.LabelShort, ThingRefs.Of(p).ToString()));
            foreach (var h in list)
            {
                if (h == null) continue;
                string line;
                if (withTown)
                {
                    string from = h.town >= 0 && ThingRefs.Exists(ThingRefs.Of(Ref.Town(h.town))) ? ThingRefs.Link(h.townName ?? "?", ThingRefs.Of(Ref.Town(h.town))) : h.townName ?? h.faction?.Name ?? "?";
                    line = "CC_Out_Heard_LineFrom".Translate(h.text.Named("TEXT"), (h.teller ?? "?").Named("TELLER"), (h.told ?? "?").Named("PAWN"), Ago(h.tick).Named("AGO"),
                        from.Named("FROM")).Resolve();
                }
                else line = "CC_Out_Heard_Line".Translate(h.text.Named("TEXT"), (h.teller ?? "?").Named("TELLER"), (h.told ?? "?").Named("PAWN"), Ago(h.tick).Named("AGO")).Resolve();
                line = LinkTextMath.MarkNames(line, names);
                // News that named a place: "Show on map" leads to it on the region map (World/HeardPlaces.cs).
                string show = onMap ? HeardPlaces.ShowOnMap(h.place) : null;
                if (!show.NullOrEmpty()) line += " " + show;
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>"today", "yesterday", "5 days ago" (never a countdown).</summary>
        public static string Ago(int tick)
        {
            int days = Mathf.Max(0, (Find.TickManager.TicksGame - tick) / GenDate.TicksPerDay);
            if (days == 0) return "CC_Out_Heard_Today".Translate().Resolve();
            if (days == 1) return "CC_Out_Heard_Yesterday".Translate().Resolve();
            return "CC_Out_Heard_DaysAgo".Translate(days).Resolve();
        }
    }
}
