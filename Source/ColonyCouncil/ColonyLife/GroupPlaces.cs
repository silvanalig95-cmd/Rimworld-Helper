using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A10: a place for the group (the content update, the player's go of 20 November 2026). A close group (the card's
    /// "close", GroupMath.CloseAt) whose habit has nowhere to happen asks the council for its place: the fireside a fire pit
    /// with seats round it, the readers a bookcase with a chair by it, the game night a game table, the musicians an
    /// instrument, the faithful a shrine. Which habit: the first of the group's habits, in its own order, that wants a place
    /// of its own (GroupPlaceMath.FirstWanting; the rest, a shared table or working side by side, need none). The anchor asks,
    /// and every member at home signs it (one wish, the co-signers the council already knows). Met when the colony has the
    /// place: a first evening there (the habit called as a group's gathering, counted in the week's gatherings), and from
    /// then on the anchor calls the group to it (Social/Groups/GroupHabits.cs, Daily), the habits with no gathering of their
    /// own included (reading, music, prayer). What counts is read from the player's whole mod pool by kind: any fire pit,
    /// any bookcase (the game's, [sbz] Bookcase's), any game table (anything the game counts as a game of skill or chance),
    /// any instrument (Royalty's and any mod's), any altar or shrine (Ideology's and any mod's), the game's own first.
    /// </summary>
    public static class GroupPlaces
    {
        // ---------------- What counts ----------------

        public static bool IsFire(ThingDef d) =>
            d?.building != null && !d.IsFrame && !d.IsBlueprint && !IsProp(d)
            && (Has(d, "Campfire") || Has(d, "Bonfire") || Has(d, "FirePit")) && !Has(d, "AfterRitual");

        public static bool IsBooks(ThingDef d)
        {
            if (d?.building == null || d.IsFrame || d.IsBlueprint || IsProp(d)) return false;
            for (var t = d.thingClass; t != null && t != typeof(object); t = t.BaseType)
                if (t == typeof(Building_Bookcase)) return true;
            return Has(d, "bookcase") || Has(d, "bookshelf");
        }

        public static bool IsGames(ThingDef d)
        {
            if (d?.building == null || d.IsFrame || d.IsBlueprint || IsProp(d)) return false;
            string k = d.building.joyKind?.defName;
            return k == "Gaming_Cerebral" || k == "Gaming_Dexterity";
        }

        public static bool IsMusic(ThingDef d)
        {
            if (d?.building == null || d.IsFrame || d.IsBlueprint || IsProp(d)) return false;
            for (var t = d.thingClass; t != null && t != typeof(object); t = t.BaseType)
                if (t == typeof(Building_MusicalInstrument)) return true;
            return false;
        }

        public static bool IsShrine(ThingDef d)
        {
            if (d?.building == null || d.IsFrame || d.IsBlueprint || IsProp(d)) return false;
            if (Has(d, "Shrineshield") || Has(d, "Smashing")) return false;
            return d.isAltar || Has(d, "altar") || Has(d, "shrine");
        }

        private static bool Has(ThingDef d, string word) => d.defName.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Vanilla Furniture Expanded's props are decorations, not the real thing.</summary>
        private static bool IsProp(ThingDef d) => d.defName.StartsWith("VFEPD_", StringComparison.Ordinal);

        public static Func<ThingDef, bool> Matcher(GroupPlaceKind k)
        {
            switch (k)
            {
                case GroupPlaceKind.Fire: return IsFire;
                case GroupPlaceKind.Books: return IsBooks;
                case GroupPlaceKind.Games: return IsGames;
                case GroupPlaceKind.Music: return IsMusic;
                case GroupPlaceKind.Shrine: return IsShrine;
                default: return null;
            }
        }

        /// <summary>The game's own first, then the player's mods' (a stone fire pit, a bookshelf), else the cheapest that fits.</summary>
        internal static string[] Preferred(GroupPlaceKind k)
        {
            switch (k)
            {
                case GroupPlaceKind.Fire: return new[] { "Campfire", "Stone_Campfire" };
                case GroupPlaceKind.Books: return new[] { "Bookcase", "BookcaseSmall", "sbz_Bookshelf", "sbz_TwoTierBookcase" };
                case GroupPlaceKind.Games: return new[] { "ChessTable", "PokerTable", "BilliardsTable", "HorseshoesPin" };
                case GroupPlaceKind.Music: return new[] { "Harp", "Harpsichord", "Piano" };
                case GroupPlaceKind.Shrine: return new[] { "Altar_Small", "IncenseShrine", "Altar_Medium" };
                default: return new string[0];
            }
        }

        // ---------------- The place ----------------

        /// <summary>The place kind a habit wants (none for a habit that needs no place of its own).</summary>
        public static GroupPlaceKind KindOf(GroupHabitDef h) => h == null ? GroupPlaceKind.None : GroupPlaceMath.KindOf(h.where);

        /// <summary>The habit the group would ask a place for, and what kind of place; null when none of its habits wants one.</summary>
        public static GroupHabitDef Wanted(SocialGroup g, out GroupPlaceKind kind)
        {
            kind = GroupPlaceKind.None;
            if (g == null) return null;
            var habits = g.Habits;
            int i = GroupPlaceMath.FirstWanting(habits.Select(KindOf).ToList());
            if (i < 0) return null;
            kind = KindOf(habits[i]);
            return habits[i];
        }

        /// <summary>A place of this kind on the map, with the seats it wants round it: the newest. Null when there is none.</summary>
        public static Thing Find(Map map, GroupPlaceKind k)
        {
            var pred = Matcher(k);
            if (map == null || pred == null) return null;
            int seats = GroupPlaceMath.SeatsWanted(k);
            Thing best = null;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (b == null || !b.Spawned || b is Frame || !pred(b.def)) continue;
                if (seats > 0 && Seats(b, map) < seats) continue;
                if (best == null || b.thingIDNumber > best.thingIDNumber) best = b;
            }
            return best;
        }

        /// <summary>Seats round a fire or a bookcase (within GroupPlaceMath.SeatRadius; for a bookcase, in its room).</summary>
        private static int Seats(Thing t, Map map)
        {
            int n = 0;
            var room = t.GetRoom();
            bool indoors = room != null && !room.PsychologicallyOutdoors;
            foreach (var o in GenRadial.RadialDistinctThingsAround(t.Position, map, GroupPlaceMath.SeatRadius, true))
            {
                if (o == t || !(o is Building) || o.def.building == null || !o.def.building.isSittable) continue;
                if (indoors && o.GetRoom() != room) continue;
                n++;
            }
            return n;
        }

        /// <summary>The group's own place while it stands (by its ThingID, else another of the same kind now on the map); null when it has none.</summary>
        public static Thing PlaceOf(SocialGroup g, Map map)
        {
            if (g == null || map == null || g.placeKind == GroupPlaceKind.None) return null;
            if (!g.placeId.NullOrEmpty())
                foreach (var b in map.listerBuildings.allBuildingsColonist)
                    if (b != null && b.Spawned && b.ThingID == g.placeId)
                        return b;
            // Moved, rebuilt, or one built in its stead: whatever of the kind the colony has now.
            var other = Find(map, g.placeKind);
            if (other != null) g.placeId = other.ThingID;
            return other;
        }

        /// <summary>
        /// The habit the anchor calls the group to at its own place, and the place (Social/Groups/GroupHabits.cs, Daily):
        /// null when it has no place standing, or no longer has the habit it was for.
        /// </summary>
        public static GroupHabitDef CallAt(SocialGroup g, Map map, out Thing place)
        {
            place = null;
            if (g == null || g.placeKind == GroupPlaceKind.None) return null;
            var habit = g.Habits.FirstOrDefault(h => KindOf(h) == g.placeKind);
            if (habit == null) return null;
            place = PlaceOf(g, map);
            return GroupPlaceMath.CalledAtPlace(g.placeKind, place != null) ? habit : null;
        }

        /// <summary>How a gathering at the place is held: round the fire, else a gathering at the place itself.</summary>
        public static PlayKind PlayAt(GroupPlaceKind k) => k == GroupPlaceKind.Fire ? PlayKind.Bonfire : PlayKind.Gathering;

        public static SocialGroup GroupOf(Wish w)
        {
            var key = w?.subjectKey;
            if (key == null || !key.StartsWith("group:", StringComparison.Ordinal)) return null;
            var parts = key.Split(':');
            if (parts.Length < 2 || !int.TryParse(parts[1], out int id)) return null;
            return GroupTracker.Instance?.groups.FirstOrDefault(g => g.id == id);
        }

        public static GroupPlaceKind KindOf(Wish w)
        {
            var parts = w?.subjectKey?.Split(':');
            return parts != null && parts.Length >= 3 && Enum.TryParse(parts[2], out GroupPlaceKind k) ? k : GroupPlaceKind.None;
        }

        // ---------------- The first evening ----------------

        /// <summary>
        /// The place is there: the group's first evening at it (with "Answers play out on the map"), led by its anchor, the
        /// habit's own lines said there; those who come keep the habit, as at any of the group's gatherings.
        /// </summary>
        public static void FirstEvening(SocialGroup g, Thing place, GroupHabitDef habit)
        {
            var tracker = PlayOutTracker.Instance;
            var map = place?.Map;
            if (g == null || place == null || habit == null || map == null || tracker == null || !PlayOuts.On || g.playId >= 0) return;
            var anchor = g.Holder(GroupRole.Anchor) ?? g.members.OrderByDescending(m => m.attachment).FirstOrDefault()?.pawn;
            var invited = g.Pawns.Where(p => PlayOuts.OurPerson(p) && p.MapHeld == map).ToList();
            if (anchor == null || invited.Count < 2) return;
            int now = Verse.Find.TickManager.TicksGame;
            var spots = new List<PlaySpot> { PlaySpot.Place };
            spots.AddRange(habit.Spots.Count > 0 ? habit.Spots : new List<PlaySpot> { PlaySpot.Rec, PlaySpot.Gather });
            var play = new PlayOut
            {
                kind = PlayAt(g.placeKind),
                map = map,
                created = now,
                label = "CC_Life_GroupPlace_FirstLabel".Translate(g.Name.Named("GROUP"), place.def.label.Named("THING")).Resolve(),
                invited = invited,
                organizer = invited.Contains(anchor) ? anchor : null,
                place = place,
                spots = spots,
                durationTicks = PlayOutMath.DurationTicks(2f),
                pool = habit.pool,
                hourFrom = 17,
                hourTo = 22,
                music = g.placeKind == GroupPlaceKind.Music || g.placeKind == GroupPlaceKind.Fire,
                routine = true
            };
            play.deadline = PlayOutMath.Deadline(now, GenLocalDate.HourOfDay(map), play.hourFrom, play.hourTo, 30f);
            string cause = "CC_Cause_GroupHabit".Translate(habit.label, g.Name).Resolve();
            if (GroupDefs.Habit != null)
                foreach (var x in invited)
                    play.moods.Add(new HeldMood { pawn = x, thought = GroupDefs.Habit, cause = cause });
            tracker.Add(play);
            g.playId = play.id;
            g.lastArranged = now;
            g.gatherings++;
        }
    }

    /// <summary>
    /// A10's wish: a close group's anchor asks the council for the group's place (<see cref="GroupPlaces"/>), every member at
    /// home signing it. Its subject is the group and the kind of place ("group:12:Fire"); met when the colony has the place.
    /// </summary>
    public class WishWorker_GroupPlace : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var t = GroupTracker.Instance;
            var map = pawn?.MapHeld;
            if (t == null || map == null || !map.IsPlayerHome || !GroupLife.Active) yield break;
            foreach (var g in t.groups)
            {
                if (g.Holder(GroupRole.Anchor) != pawn) continue;
                int home = g.Pawns.Count(p => p.MapHeld == map && PlayOuts.OurPerson(p));
                if (!GroupPlaceMath.MayAsk(g.cohesion, home)) continue;
                var habit = GroupPlaces.Wanted(g, out var kind);
                if (habit == null || GroupPlaces.Find(map, kind) != null) continue;
                var best = Avail.BestBuildable(map, GroupPlaces.Matcher(kind), GroupPlaces.Preferred(kind));
                if (best == null) continue;
                yield return new WishCandidate
                {
                    weight = 1f, thingDef = best, subjectKey = "group:" + g.id + ":" + kind, subjectLabel = g.Name, targetValue = 1f,
                    reason = ("CC_Life_GroupPlace_Why_" + kind).Translate(pawn.Named("PAWN"), g.Name.Named("GROUP"), habit.label.Named("HABIT")).Resolve()
                };
            }
        }

        public override bool IsFulfilled(Wish wish) => GroupPlaces.Find(wish.Beneficiary?.MapHeld, GroupPlaces.KindOf(wish)) != null;

        /// <summary>The group is still there, they're still in it, and something that fits can still be had.</summary>
        public override bool StillValid(Wish wish)
        {
            var g = GroupPlaces.GroupOf(wish);
            return g != null && g.Has(wish.pawn) && StillObtainable(wish);
        }

        public override bool StillObtainable(Wish wish)
        {
            if (!base.StillObtainable(wish)) return false;
            var map = wish.Beneficiary?.MapHeld;
            var k = GroupPlaces.KindOf(wish);
            if (map == null) return true;
            return GroupPlaces.Find(map, k) != null || Avail.AnyBuildable(map, GroupPlaces.Matcher(k) ?? (d => false));
        }

        public override string FulfilledWhen(Wish wish) =>
            ("CC_Life_GroupPlace_When_" + GroupPlaces.KindOf(wish)).Translate((wish.subjectLabel ?? "").Named("GROUP"), (wish.thingDef?.label ?? "").Named("THING")).Resolve();

        /// <summary>Every member at home signs it with the anchor; the anchor's words are for the place the group asks for.</summary>
        public override void OnMade(Wish wish)
        {
            var g = GroupPlaces.GroupOf(wish);
            var map = wish.pawn?.MapHeld;
            if (g == null || map == null) return;
            foreach (var p in g.Pawns)
                if (p != wish.pawn && p.MapHeld == map && PlayOuts.OurPerson(p) && !wish.coSigners.Contains(p)) wish.coSigners.Add(p);
            var tags = Flavor.TagsFor(wish.pawn, wish);
            tags.Add("place:" + GroupPlaces.KindOf(wish).ToString().ToLowerInvariant());
            string line = def.request.Pick(wish.tone, tags, wish.pawn);
            if (!line.NullOrEmpty()) wish.quote = wish.Format(line);
            g.Note("CC_Life_GroupPlace_Asked".Translate(wish.pawn.Named("PAWN"), (wish.thingDef?.label ?? "").Named("THING")).Resolve());
        }

        /// <summary>The place stands: it is the group's from now on, and the first evening there is set going.</summary>
        public override void OnMet(Wish wish)
        {
            var g = GroupPlaces.GroupOf(wish);
            var map = wish.pawn?.MapHeld;
            var kind = GroupPlaces.KindOf(wish);
            var place = GroupPlaces.Find(map, kind);
            if (g == null || place == null) return;
            g.placeKind = kind;
            g.placeId = place.ThingID;
            g.Note("CC_Life_GroupPlace_Got".Translate(place.def.label.Named("THING")).Resolve(), true);
            var habit = g.Habits.FirstOrDefault(h => GroupPlaces.KindOf(h) == kind);
            GroupPlaces.FirstEvening(g, place, habit);
        }

        public override LookTargets LookAt(Wish wish) => new LookTargets(wish.pawn);

        public override string ProgressText(Wish wish) => null;
    }
}
