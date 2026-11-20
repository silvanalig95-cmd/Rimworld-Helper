using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk"): each of colony life's integrations, part 2, tried at once: a group asks for its
    /// place or keeps its first evening there (A10), a colonist tries out a wished-for thing (A12), a wish talked up to
    /// someone near (A13), the ambition reached now (A14), an aim of Vanilla Aspirations Expanded's reached (A17), the year's
    /// first supper asked or set going (A27), the year read aloud tonight and a record keeper made (A29).
    /// </summary>
    public static class LifeDebug
    {
        private static void Say(string text) => Messages.Message(text, MessageTypeDefOf.SilentInput, false);

        private static void Menu(List<FloatMenuOption> options, string empty)
        {
            if (options.Count == 0) options.Add(new FloatMenuOption(empty, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static List<Pawn> Colonists => Find.CurrentMap?.mapPawns.FreeColonistsSpawned.Where(PlayOuts.OurPerson).ToList() ?? new List<Pawn>();

        // ---------------- A10 ----------------

        [DebugAction("Rimfolk", "Colony life: a group asks for its place...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GroupAsks()
        {
            var comp = CouncilGameComponent.Instance;
            var t = GroupTracker.Instance;
            var def = DefDatabase<WishDef>.GetNamedSilentFail("Rimfolk_Wish_GroupPlace");
            var options = new List<FloatMenuOption>();
            if (comp != null && t != null && def != null)
                foreach (var g in t.groups.ToList())
                {
                    var group = g;
                    var habit = GroupPlaces.Wanted(g, out var kind);
                    options.Add(new FloatMenuOption(g.Name + " (" + (habit == null ? "no habit wants a place" : habit.label + ": " + kind) + ")", () =>
                    {
                        var anchor = group.Holder(GroupRole.Anchor);
                        if (anchor == null || habit == null)
                        {
                            Say("Colony life: " + group.Name + " has no anchor, or none of its habits wants a place of its own.");
                            return;
                        }
                        // Close enough to ask, for the test.
                        group.cohesion = Mathf.Max(group.cohesion, GroupMath.CloseAt);
                        if (comp.OpenWishBy(anchor) is Wish open) comp.Resolve(open, WishState.Cancelled);
                        var c = def.Worker.CandidatesFor(anchor).FirstOrDefault(x => x.subjectKey.StartsWith("group:" + group.id + ":"));
                        if (c == null)
                        {
                            Say("Colony life: " + group.Name + " can't ask now (fewer than three of it at home, the place already there, or nothing of its kind the colony can build).");
                            return;
                        }
                        comp.CreateWish(def, anchor, anchor, c);
                    }));
                }
            Menu(options, "(no groups)");
        }

        [DebugAction("Rimfolk", "Colony life: a group's first evening at its place now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GroupEvening()
        {
            var t = GroupTracker.Instance;
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            if (t != null && map != null)
                foreach (var g in t.groups.ToList())
                {
                    var group = g;
                    GroupPlaces.Wanted(g, out var kind);
                    var place = kind == GroupPlaceKind.None ? null : GroupPlaces.Find(map, kind);
                    options.Add(new FloatMenuOption(g.Name + (place != null ? " (" + place.LabelShort + ")" : " (no place of its kind on the map)"), () =>
                    {
                        if (place == null) return;
                        group.placeKind = kind;
                        group.placeId = place.ThingID;
                        group.playId = -1;
                        GroupPlaces.FirstEvening(group, place, group.Habits.FirstOrDefault(h => GroupPlaces.KindOf(h) == kind));
                        Say("Colony life: " + group.Name + "'s first evening at the " + place.LabelShort + " is planned (this evening, when the colony's week has room).");
                    }));
                }
            Menu(options, "(no groups)");
        }

        // ---------------- A12 ----------------

        [DebugAction("Rimfolk", "Colony life: a colonist tries out a thing now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TryOut()
        {
            var options = new List<FloatMenuOption>();
            foreach (var p in Colonists)
            {
                var pawn = p;
                options.Add(new FloatMenuOption(p.LabelShort, () =>
                {
                    var book = LifeBook.Instance;
                    if (book == null) return;
                    Thing thing = pawn.ownership?.OwnedBed;
                    var act = TryAct.Lie;
                    if (thing == null)
                    {
                        thing = GenClosest.ClosestThing_Global(pawn.Position, pawn.Map.listerBuildings.allBuildingsColonist.Where(b => b.def.building != null && b.def.building.isSittable), 30f);
                        act = TryAct.Sit;
                    }
                    if (thing == null)
                    {
                        Say("Colony life: " + pawn.LabelShort + " has no bed and there is no seat near.");
                        return;
                    }
                    string thanks = DefDatabase<WishDef>.GetNamedSilentFail("Council_Wish_OwnRoom")?.thanks.Pick(ToneUtility.ToneFor(pawn)) ?? "Thank you.";
                    book.tryOuts.RemoveAll(x => x.pawn == pawn);
                    book.tryOuts.Add(new ColonyCouncil.TryOut { pawn = pawn, thing = thing, act = act, thanks = thanks, label = thing.def.label, since = Find.TickManager.TicksGame });
                    Say("Colony life: " + pawn.LabelShort + " will try out the " + thing.LabelShort + " in their free time (within a day).");
                }));
            }
            Menu(options, "(no colonists)");
        }

        // ---------------- A13 ----------------

        [DebugAction("Rimfolk", "Colony life: a wish talked up to someone near...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TalkUp()
        {
            var comp = CouncilGameComponent.Instance;
            var options = new List<FloatMenuOption>();
            if (comp != null)
                foreach (var p in Colonists)
                {
                    var w = comp.OpenWishBy(p);
                    if (w?.def == null || !w.def.allowPetition) continue;
                    var teller = p;
                    options.Add(new FloatMenuOption(p.LabelShort + ": " + w.Label, () => Say("Colony life: " + comp.DebugTalkUp(teller))));
                }
            Menu(options, "(nobody has an open wish many may back)");
        }

        // ---------------- A14 ----------------

        [DebugAction("Rimfolk", "Colony life: the ambition under way is reached now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AmbitionReached()
        {
            var comp = CouncilGameComponent.Instance;
            var tracker = AmbitionTracker.For(Find.CurrentMap);
            if (tracker?.active == null)
            {
                Say("Colony life: no ambition under way here (choose one at a council meeting first).");
                return;
            }
            string label = tracker.active.def.label;
            if (tracker.DebugReach(comp)) Say("Colony life: " + label + " reached; its scene is planned and its day is on the calendar.");
        }

        // ---------------- A17 ----------------

        [DebugAction("Rimfolk", "Colony life: an aim of their own reached (Vanilla Aspirations Expanded)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ModAim()
        {
            var options = new List<FloatMenuOption>();
            var aims = ModAims.All;
            foreach (var p in Colonists)
            {
                var pawn = p;
                options.Add(new FloatMenuOption(p.LabelShort, () =>
                {
                    var choices = new List<FloatMenuOption>();
                    foreach (var a in aims.Take(60))
                    {
                        var aim = a;
                        choices.Add(new FloatMenuOption(a.label + (ModAimsMath.Toasted(a.defName) ? "" : " (journal only)"), () => ModAims.Marked(pawn, aim)));
                    }
                    choices.Add(new FloatMenuOption("(an aim the letter doesn't name)", () => ModAims.Marked(pawn, null)));
                    Find.WindowStack.Add(new FloatMenu(choices));
                }));
            }
            Menu(options, "(no colonists)");
        }

        // ---------------- A27 ----------------

        [DebugAction("Rimfolk", "Colony life: the year's first supper asked now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SupperAsked()
        {
            var def = DefDatabase<DilemmaDef>.GetNamedSilentFail("Council_Dilemma_FirstSupper");
            var map = Find.CurrentMap;
            bool asked = def != null && (DilemmaTracker.For(map)?.AskCustom(def) ?? false);
            Say("Colony life: the year's first supper " + (asked ? "is asked now." : "can't be asked now (only in the year's first week, with someone who has a life goal, and no question waiting)."));
        }

        [DebugAction("Rimfolk", "Colony life: the year's first supper set going now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SupperNow()
        {
            var map = Find.CurrentMap;
            var plan = new DilemmaEffect_FirstSupper { label = "the year's first supper", chooseNew = true, hours = new IntRange(0, 23), duration = new FloatRange(2f, 2.5f) }
                .Begin(new DilemmaContext { map = map, a = Colonists.FirstOrDefault() });
            Say("Colony life: " + (plan != null ? "the year's first supper is set going (with answers playing out on the map)." : "nothing set going (answers playing out on the map is off, or nobody to come)."));
        }

        // ---------------- A29 ----------------

        [DebugAction("Rimfolk", "Colony life: the year read aloud tonight", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void YearReadNow()
        {
            var comp = CouncilGameComponent.Instance;
            var map = Find.CurrentMap;
            if (comp == null || map == null) return;
            var writer = comp.annals.Author(comp, out _);
            if (writer == null)
            {
                Say("Colony life: nobody here to write the year.");
                return;
            }
            int year = Mathf.Max(1, comp.life.yearRead + 1);
            var lines = YearRead.Lines(comp, map, comp.annals.Since, Find.TickManager.TicksGame, year);
            YearRead.Plan(comp, map, year, writer, lines);
            Say("Colony life: " + writer.LabelShort + " reads " + lines.Count + " line(s) of the year this evening (the year's figures so far; nothing is reset).");
        }

        [DebugAction("Rimfolk", "Colony life: make a colonist the record keeper...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RecordKeeper()
        {
            var options = new List<FloatMenuOption>();
            foreach (var p in Colonists)
            {
                var pawn = p;
                options.Add(new FloatMenuOption(p.LabelShort, () =>
                {
                    if (LifeBook.Instance == null) return;
                    LifeBook.Instance.recordKeeper = pawn;
                    Say("Colony life: " + pawn.LabelShort + " is the colony's record keeper and writes the next year letter.");
                }));
            }
            Menu(options, "(no colonists)");
        }
    }

    public partial class CouncilGameComponent
    {
        /// <summary>Dev mode: <paramref name="teller"/>'s wish talked up to the nearest colonist who would back it, chance and the day's limit aside.</summary>
        internal string DebugTalkUp(Pawn teller)
        {
            var w = OpenWishBy(teller);
            if (w == null) return teller.LabelShort + " has no open wish.";
            var listener = teller.Map?.mapPawns.FreeColonistsSpawned.Where(o => o != teller && WouldBack(w, o)).OrderBy(o => o.Position.DistanceToSquared(teller.Position)).FirstOrDefault();
            if (listener == null) return "nobody at home would want the same right now.";
            if (!Together.Can(teller, listener)) return listener.LabelShort + " would back it, but isn't near " + teller.LabelShort + " (they have to be talking).";
            w.talkBackedDay = GenDate.DaysPassed;
            JoinInTalk(w, listener, teller);
            return listener.LabelShort + " joined " + teller.LabelShort + "'s wish.";
        }
    }
}
