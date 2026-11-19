using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Rooms judged by what is in them (the player, 19 November 2026: "Rooms judged by what's in them. That sounds good.";
/// before that: "I want the gatherings where food is involved, of course, happening in my dining room, even if maybe
/// another room is kind of the one for the ideological items"). The pure reading (RoomUseMath.cs), the helpers in
/// RoomUtil.cs, and every place that means a room where people eat or relax using them.
/// </summary>
public static class RoomUseTests
{
    private record R(string Name, string Role, int Tables, int Games, int Cells, bool Cell = false);

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "rooms by what is in them: " + what);
        }

        // What a room serves as.
        C(RoomUseMath.ServesAsDining(1, false) && RoomUseMath.ServesAsDining(6, false), "one dining table or more makes a room where people eat");
        C(!RoomUseMath.ServesAsDining(0, false), "no table, no dining room");
        C(RoomUseMath.ServesAsRec(1, false) && !RoomUseMath.ServesAsRec(0, false), "one recreation building makes a room for recreation, none doesn't");
        C(!RoomUseMath.ServesAsDining(2, true) && !RoomUseMath.ServesAsRec(2, true), "a private room never counts, whatever is in it");

        // Which rooms are private.
        foreach (var role in new[] { "Bedroom", "Barracks", "Hospital", "PrisonCell", "PrisonBarracks", "GuestRoom", "Nursery", "DeathrestChamber", "PublicBathroom", "SaunaRoom" })
            C(RoomUseMath.Private(role, false), role + " is private: nobody gathers there");
        foreach (var role in new[] { "WorshipRoom", "VFEE_Ballroom", "Kitchen", "Workshop", "ThroneRoom", "RecRoom", "DiningRoom", "Room", "None" })
            C(!RoomUseMath.Private(role, false), role + " is open to gatherings when it holds tables or games");
        C(RoomUseMath.Private(null, true) && RoomUseMath.Private("Room", true), "a prison cell is private whatever the game calls it");
        C(!RoomUseMath.Private(null, false), "no role, not private");

        // The order rooms are tried in.
        C(RoomUseMath.Rank(true, false) == 0 && RoomUseMath.Rank(true, true) == 0, "a room the game calls so comes first");
        C(RoomUseMath.Rank(false, true) == 1, "then a room that serves as one");
        C(RoomUseMath.Rank(false, false) == -1, "a room that is neither is left out");
        var order = RoomUseMath.Order(new[] { 1, 0, -1, 1, 0 }, new[] { 50, 20, 99, 80, 40 });
        C(order.SequenceEqual(new[] { 4, 1, 3, 0 }), "named first, larger first within each, the rest left out (" + string.Join(",", order) + ")");
        C(RoomUseMath.Order(new int[0], new int[0]).Count == 0, "no rooms, none tried");

        // The kind a room is read as where Rimfolk tells kinds apart.
        C(RoomUseMath.ReadAs("Kitchen", true, true, false) == "Kitchen", "a kind of its own stays: a kitchen with a table is a kitchen first");
        C(RoomUseMath.ReadAs("WorshipRoom", false, true, true) == "DiningRoom", "a temple with tables reads as a dining room");
        C(RoomUseMath.ReadAs("VFEE_Ballroom", false, false, true) == "RecRoom", "a ballroom with games reads as a rec room");
        C(RoomUseMath.ReadAs("ThroneRoom", false, false, false) == "ThroneRoom", "a room with neither keeps the game's role");

        // The player's rooms: the dining room with tables, chairs, an altar, a lectern, a ritual spot and a drum (the game
        // shows it as a temple: the altar's 2000 beats the tables' 12 a table), a separate rec room, bedrooms with a table.
        var rooms = new List<R>
        {
            new R("dining room with the altar", "WorshipRoom", 3, 1, 90),
            new R("rec room", "RecRoom", 0, 4, 60),
            new R("bedroom with a table", "Bedroom", 1, 0, 20),
            new R("kitchen", "Kitchen", 0, 0, 30),
            new R("prison cell", "PrisonCell", 1, 0, 12, true),
        };
        List<string> Read(string named, Func<R, bool> serves)
        {
            var ranks = rooms.Select(r => RoomUseMath.Rank(r.Role == named, serves(r))).ToList();
            return RoomUseMath.Order(ranks, rooms.Select(r => r.Cells).ToList()).Select(i => rooms[i].Name).ToList();
        }
        bool Dining(R r) => RoomUseMath.ServesAsDining(r.Tables, RoomUseMath.Private(r.Role, r.Cell));
        bool Rec(R r) => RoomUseMath.ServesAsRec(r.Games, RoomUseMath.Private(r.Role, r.Cell));
        var dining = Read("DiningRoom", Dining);
        C(dining.SequenceEqual(new[] { "dining room with the altar" }), "the player's dining room shown as a temple is where meals and meetings go (" + string.Join(", ", dining) + ")");
        var rec = Read("RecRoom", Rec);
        C(rec.First() == "rec room", "the room the game calls a rec room comes first for recreation");
        C(rec.Contains("dining room with the altar"), "a game in the temple makes it serve for recreation too, after the rec room");
        rooms.Add(new R("the old dining hall", "DiningRoom", 2, 0, 40));
        dining = Read("DiningRoom", Dining);
        C(dining.SequenceEqual(new[] { "the old dining hall", "dining room with the altar" }), "a room the game calls a dining room first, even a smaller one (" + string.Join(", ", dining) + ")");

        // The helpers in RoomUtil.
        string S(params string[] p) => File.ReadAllText(Path.Combine(new[] { root, "Source", "ColonyCouncil" }.Concat(p).ToArray()));
        string util = S("RoomUtil.cs");
        C(util.Contains("d.surfaceType == SurfaceType.Eat") && util.Contains("d.building.joyKind != null"), "a dining table by its eating surface, a game by its kind of recreation, as the game counts them");
        C(util.Contains("t.Faction != Faction.OfPlayer") && util.Contains("d.IsFrame || d.IsBlueprint"), "only the colony's own, built ones");
        C(util.Contains("room.ProperRoom && !room.IsHuge && !room.PsychologicallyOutdoors"), "only a proper room, not the open air or a whole cave");
        C(util.Contains("RoomUseMath.Private(room.Role?.defName, room.IsPrisonCell)"), "private rooms by the game's role and the prison flag");
        C(util.Contains("public static List<Room> DiningRooms(Map map) => ByUse(map, DiningRoom, ServesAsDining);")
          && util.Contains("public static List<Room> RecRooms(Map map) => ByUse(map, RecRoom, ServesAsRec);"), "the dining and rec rooms in their order");
        C(util.Contains("if (role == DiningRoom) return DiningRooms(map);") && util.Contains("if (role == RecRoom) return RecRooms(map);"), "a role named by a goal or a plan reads the same");

        // Every place that means a room where people eat or relax uses them.
        string meeting = S("Ambitions", "CouncilMeeting.cs");
        C(meeting.Contains("foreach (var room in RoomUtil.DiningRooms(map))") && meeting.Contains("foreach (var room in RoomUtil.RecRooms(map))"), "council meetings: the dining rooms, then the rec rooms");
        string spots = S("PlayOut", "PlaySpots.cs");
        C(spots.Contains("foreach (var room in RoomUtil.DiningRooms(map))") && spots.Contains("foreach (var room in RoomUtil.RecRooms(map))"), "play-outs and feasts: the same");
        C(spots.Contains("\"CC_Venue_InRoom\".Translate(room.GetRoomRoleLabel().Named(\"ROOM\"))"), "a rec room the game calls something else is named as the map shows it");
        C(S("Visitors", "Visitors.cs").Contains("RoomUtil.DiningRooms(map).Concat(RoomUtil.RecRooms(map)).Distinct()"), "visitors kept in from the cold");
        C(S("Happenings", "VisitorServices.cs").Contains("if (RoomUtil.IsCommonRoom(r)) score += 4f;"), "a guest's corner for the night");
        string ideas = S("Advisor", "AdvisorSignals.cs");
        C(ideas.Contains("!RoomUtil.AnyRecRoom(map) && Avail.AnyBuildable(map, Avail.IsJoyGiver)"), "no idea of building a rec room where a room serves as one");
        C(ideas.Contains("if (!RoomUtil.AnyRecRoom(map)) s.template = "), "nor a rec room plan on the recreation advice");
        C(ideas.Contains("var dining = RoomUtil.DiningRooms(map);"), "dressing up the dining room");
        C(S("Advisor", "AdvisorSignals_Rooms.cs").Contains("foreach (var room in RoomUtil.DiningRooms(map))")
          && S("Advisor", "AdvisorSignals_Rooms.cs").Contains("foreach (var room in RoomUtil.RecRooms(map))"), "the dining room's seats and the rec rooms' games");
        C(S("Advisor", "AdvisorSignals_Rooms2.cs").Contains("foreach (var room in RoomUtil.DiningRooms(map))"), "the cramped dining room");
        C(S("Advisor", "AdvisorSignals5.cs").Contains("RoomUtil.DiningRooms(map).Sum("), "the seats for those who ate without a table");
        C(S("Advisor", "AdvisorSignals2.cs").Contains("RoomUtil.IsCommonRoom(room)"), "rooms with no light");
        string work = S("Wishes", "WishWorkers_Work.cs");
        C(Regex.Matches(work, @"RoomUtil\.IsCommonRoom").Count >= 3, "the art wish: art where people eat and relax");
        C(S("Wishes", "WishWorkers_LifeNew.cs").Contains("RoomUtil.DiningRank(b.GetRoom())"), "the table at the end of a roofed way");
        C(S("Happenings", "Workers_Callers.cs").Contains("RoomUtil.AnyDiningRoom(map)"), "a wedding needs a marriage spot or a room to eat in");
        C(S("Ambitions", "Ambitions.cs").Contains("if (RoomUtil.AnyDiningRoom(map)) return \"CC_Venue_Dining\""), "the meeting's words");
        C(S("Ambitions", "AmbitionGoals.cs").Contains("RoomUtil.RoomsFor(map, roleDef)") && S("Ambitions", "AmbitionGoals2.cs").Contains("RoomUtil.RoomsFor(map, roleDef)"), "ambitions' rooms");
        C(S("Building", "TemplatePlanner.cs").Contains("RoomUtil.RoomsFor(map, role)"), "a planned room's neighbours");
        string review = S("Building", "BaseReview.cs");
        C(review.Contains("RoomUseMath.ReadAs(name, false, RoomUtil.ServesAsDining(room), RoomUtil.ServesAsRec(room))") && review.Contains("also = true"), "the base review and its little map");
        C(S("Items", "ItemCardArt.cs").Contains("RoomUseMath.ReadAs(role, false, RoomUtil.ServesAsDining(r), RoomUtil.ServesAsRec(r))"), "a work of art's card: the moods of eating and relaxing");
        C(S("Dilemmas", "DilemmaWorkers_Faith.cs").Contains("foreach (var r in RoomUtil.RecRooms(map))"), "a corner for each faith");
        C(S("Integrations", "GuestWill.cs").Contains("bool has = RoomUtil.AnyRecRoom(map);"), "what a guest would stay for");
        C(S("Harm", "Quarrels.cs").Contains("return RoomUtil.IsCommonRoom(room);"), "two at a table, for a quarrel");
        C(S("Lives", "LifeNew.cs").Contains("RoomUtil.IsDiningRoom(room)"), "in from the rain to the table");
        C(S("Social", "Groups", "GroupHabits.cs").Contains("Leisure(p) && RoomUtil.IsRecRoom(p.GetRoom())"), "a group's evenings in the rec room");
        string pressures = S("Pressures", "Pressures.cs");
        C(pressures.Contains("bool dining = RoomUtil.IsDiningRoom(room);") && pressures.Contains("!RoomUtil.IsRecRoom(room)"), "crowded rooms");

        // Only the places that mean the game's own role, or name what the game shows, compare it directly.
        var allowed = new HashSet<string> { "RoomUtil.cs", "BaseReview.cs", "PlaySpots.cs", "DilemmaWorkers_Faith.cs", "Autonomy.cs" };
        var direct = new Regex(@"RoomsWithRole\(map, RoomUtil\.(DiningRoom|RecRoom)\)|Role [!=]= RoomUtil\.(DiningRoom|RecRoom)|== ""(DiningRoom|RecRoom)""|Named<RoomRoleDef>\(""(DiningRoom|RecRoom)""\)|role == RoomUtil\.(DiningRoom|RecRoom)");
        var stray = Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !allowed.Contains(Path.GetFileName(f)) && direct.IsMatch(File.ReadAllText(f))).Select(Path.GetFileName).ToList();
        C(stray.Count == 0, "no other place reads the dining or rec room by the game's one name (" + string.Join(", ", stray) + ")");
        C(S("Social", "Autonomy.cs").Contains("return n == \"RecRoom\" || n == \"DiningRoom\";"), "kept: where someone without a bed may lie down goes by the game's kind of room");

        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "ColonyCouncil.xml"));
        C(keyed.Contains("<CC_Venue_InRoom>in the {ROOM}</CC_Venue_InRoom>"), "the venue's words");
        C(File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Faith_Council.xml")).Contains("<CC_FaithClash_CornerIn>a corner of the {ROOM}</CC_FaithClash_CornerIn>"), "a faith's corner's words");
        return n;
    }
}
