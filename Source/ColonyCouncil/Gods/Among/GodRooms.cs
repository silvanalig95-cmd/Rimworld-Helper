using System;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A shrine room for each god (C26 of docs/content-update-proposals.md; Defs/RoomTemplateDefs/Rimfolk_RoomTemplates_GodRooms.xml):
    /// the room planner offers a shrine room the way the colony's people picture each god it knows (a garden of planters for
    /// Hollin, a stone room with a brazier for Smoulder, a megalith in the open for Bellwether ...). A room is shown only once
    /// its god has a chapter in the colony's book (a page, GodLore.PageOf), named by the colony's name for it, and says
    /// nothing of what the god wants: only what the room holds and how the colony's people picture the god. The mods' pieces
    /// (Alpha Memes' megalith and urns, the Vanilla Expanded statues and obelisks, Dragons Descent's dragon statue, Alpha
    /// Props' obelisk fountain, Dubs Bad Hygiene's well, Ideology's terminal) are used where the colony can build them, the
    /// game's own lights, pots, columns and sarcophagi otherwise. Without Ideology too: its art and statues can be
    /// dedicated to the god (Shrines.Is). A room under the open sky leaves its roof off (RoomTemplateDef.openSky).
    /// </summary>
    public static class GodRooms
    {
        /// <summary>The room is shown in the planner: not a god's room, or one whose god the colony has a page for.</summary>
        public static bool Shown(RoomTemplateDef t)
        {
            if (t == null || t.god.NullOrEmpty()) return true;
            if (!Gods.On) return false;
            var g = OwnHappenings.God(t.god);
            return GodsAmongMath.RoomShown(g != null, g != null && GodLore.Instance?.PageOf(g) != null);
        }

        /// <summary>A god's room named by the colony's name for its god ("Hollin's garden shrine"), its purpose too; nothing for any other room.</summary>
        public static void Name(RoomTemplateDef t)
        {
            if (t == null || t.god.NullOrEmpty() || t.godLabel.NullOrEmpty()) return;
            try
            {
                var g = OwnHappenings.God(t.god);
                var page = g == null ? null : GodLore.Instance?.PageOf(g);
                if (page == null) return;
                string label = t.godLabel.Formatted(page.name.Named("GOD")).Resolve();
                if (label == t.label) return;
                t.label = label;
                if (!t.godPurpose.NullOrEmpty()) t.purpose = t.godPurpose.Formatted(page.name.Named("GOD")).Resolve();
                t.ClearCachedData();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: a shrine room's name", e);
            }
        }
    }
}
