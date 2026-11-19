using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// A room read by what is in it (the player, 19 November 2026: "Rooms judged by what's in them. That sounds good."). The
    /// game shows one role per room, the highest score: a dining table scores 12, a recreation building 7, an altar of the
    /// colony's faith at least 2000, a stove 28, a work bench 27. So a dining room with an altar shows as a temple, and a
    /// kitchen with a table as a kitchen. Where Rimfolk means "a room where people eat" or "a room for recreation" it reads
    /// the room by its tables and games instead (RoomUtil.DiningRooms, RecRooms), the room the game calls so first. No game
    /// types, so Tools/Tests checks it.
    /// </summary>
    public static class RoomUseMath
    {
        /// <summary>
        /// Rooms where people sleep, are nursed, kept or wash, by the game's role (and Biotech's, Anomaly's, Hospitality's,
        /// Dubs Bad Hygiene's and the framework's coma room by name): a table or a game there makes no dining or rec room.
        /// </summary>
        public static readonly string[] PrivateRoles =
        {
            "Bedroom", "Barracks", "Hospital", "PrisonCell", "PrisonBarracks", "Nursery", "DeathrestChamber", "ContainmentCell",
            "GuestRoom", "EBSG_ComaRestChamber", "PrivateBathroom", "PublicBathroom", "SaunaRoom"
        };

        /// <summary>Nobody gathers here: a prison cell, or a room the game gives one of <see cref="PrivateRoles"/>.</summary>
        public static bool Private(string roleDefName, bool prisonCell) => prisonCell || roleDefName != null && PrivateRoles.Contains(roleDefName);

        /// <summary>People eat here: a dining table of the colony's (a building with an eating surface), and it isn't private.</summary>
        public static bool ServesAsDining(int diningTables, bool privateRoom) => diningTables > 0 && !privateRoom;

        /// <summary>People relax here: a recreation building of the colony's (one with a kind of recreation), and it isn't private.</summary>
        public static bool ServesAsRec(int recreation, bool privateRoom) => recreation > 0 && !privateRoom;

        /// <summary>The order rooms are tried in: 0 the game calls it so, 1 it serves as one by what is in it, -1 neither.</summary>
        public static int Rank(bool named, bool serves) => named ? 0 : serves ? 1 : -1;

        /// <summary>
        /// The rooms that count, as indices into the lists: the ones the game calls so first, then the ones that serve as
        /// one, the largest first within each.
        /// </summary>
        public static List<int> Order(IList<int> ranks, IList<int> cells)
        {
            var list = new List<int>();
            for (int i = 0; i < ranks.Count; i++)
                if (ranks[i] >= 0) list.Add(i);
            return list.OrderBy(i => ranks[i]).ThenByDescending(i => i < cells.Count ? cells[i] : 0).ThenBy(i => i).ToList();
        }

        /// <summary>
        /// The kind a room is read as where Rimfolk tells kinds apart (the base review, a work of art's card): its own
        /// role when Rimfolk has that kind (<paramref name="known"/>), else a dining room by its tables, a rec room by its
        /// games, else the game's role.
        /// </summary>
        public static string ReadAs(string role, bool known, bool dining, bool rec) =>
            known ? role : dining ? "DiningRoom" : rec ? "RecRoom" : role;
    }
}
