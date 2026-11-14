using System;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // Dev mode: the second seven's happenings now (Debug actions, "Rimfolk", "Gods: <god>'s ... now"), one class a god, kept
    // out of Gods/OnMap as the first seven's are (OwnHappeningsDebug.cs): the map's own code speaks only through Speech.Say
    // and the teller. Each calls the god's own happening as its hour would, past its pace.

    /// <summary>Dev mode: Hush's happenings now (Debug actions, "Rimfolk", "Gods: Hush's ... now").</summary>
    public static class OwnHushDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: Hush's word in the frost now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWord()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var w = OwnHush.WordNow(map, out _).word;
            if (w == OwnHappeningsMath.FrostWord.None) w = OwnHappeningsMath.FrostWord.Bread;
            b.hush.wordMap = null;
            Say(OwnHush.Write(b, OwnHush.God, map, w) ? "the word " + w + " is in the frost; the first one up walks out to it." : "nowhere to write it.");
        }

        [DebugAction("Rimfolk", "Gods: Hush's vigil now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugVigil()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var frail = OwnHush.Frail(map) ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.InBed());
            var keeper = OwnHush.Keeper(map, frail);
            if (frail == null || keeper == null)
            {
                Say("nobody of the colony abed, or nobody free to sit with them.");
                return;
            }
            b.hush.EndVigil();
            OwnHush.BeginVigil(b, map, frail, keeper);
            Say(keeper.LabelShort + " sits up with " + frail.LabelShort + (OwnHappenings.Condition(map, "ColdSnap") == null ? " (no cold snap here: the vigil ends at once; start one with the game's dev tools)." : "; kept till five o'clock, the cold snap breaks."));
        }

        [DebugAction("Rimfolk", "Gods: Hush's walk round the edge now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWalk()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            b.hush.walkCardDue = false;
            OwnHush.BeginWalk(b, map);
            Say("the white figure walks round the colony at " + b.hush.walkRadius + " cells (by night only: it stops at five o'clock).");
        }

        [DebugAction("Rimfolk", "Gods: Hush's frost on the newest grave now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGrave()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var grave = OwnHush.NewestGrave(map, out _);
            if (grave == null)
            {
                Say("no grave of the colony's dead here.");
                return;
            }
            b.hush.grave = null;
            Say(OwnHush.Lay(b, OwnHush.God, map, grave) ? "frost on the grave; it melts into a ring of mushrooms in two to four days (\"Gods: Hush's frost melts now\")." : "it couldn't.");
        }

        [DebugAction("Rimfolk", "Gods: Hush's frost melts now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMelt()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            Say(b.hush.grave != null && OwnHush.Melt(b, map) ? "melted into a ring of mushrooms." : "no frost on a grave here.");
        }
    }

    /// <summary>Dev mode: the dark's happenings now (Debug actions, "Rimfolk", "Gods: Undertow's ... now"). Never named in play; named here for testing.</summary>
    public static class OwnUndertowDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: Undertow's beasts keep away now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugShun()
        {
            var b = OwnHappenings.Book;
            var p = Find.CurrentMap?.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).RandomElementWithFallback();
            if (b == null || p == null) return;
            OwnUndertow.Shun(b, p);
            Say("the beasts keep away from " + p.LabelShort + " for eight days.");
        }

        [DebugAction("Rimfolk", "Gods: Undertow's dark drifts now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDrift()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var p = OwnUndertow.Desperate(map, out _) ?? map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).OrderBy(x => x.needs?.mood?.CurLevel ?? 1f).FirstOrDefault();
            b.undertow.EndDrift();
            Say(p != null && OwnUndertow.BeginDrift(b, map, p) ? "the dark drifts toward " + p.LabelShort + " (at night only: it is gone at five o'clock)." : "nobody to drift to.");
        }

        [DebugAction("Rimfolk", "Gods: Undertow's hollow ear now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEar()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            var p = map?.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).RandomElementWithFallback();
            if (b == null || p == null) return;
            Say(OwnUndertow.Ear(b, OwnUndertow.God, map, p) ? p.LabelShort + " has a hollow ear." : "it couldn't.");
        }
    }

    /// <summary>Dev mode: Helios's happenings now (Debug actions, "Rimfolk", "Gods: Helios's ... now").</summary>
    public static class OwnHeliosDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: Helios looks back now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLookBack()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            OwnHelios.LookBack(b, OwnHelios.God, map);
            Say("the day after the eclipse: his hard long noon, and those out of doors shade their eyes.");
        }

        [DebugAction("Rimfolk", "Gods: Helios's first fruit in the sun now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFruit()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            b.helios.fruitBegun = false;
            Say(OwnHelios.BeginFruit(b, map) ? "they take fruit from the stores and walk out to the fields (" + (OwnHelios.Fruit(map)?.LabelCap ?? "no fruit in the stores") + ")." : "no open ground.");
        }

        [DebugAction("Rimfolk", "Gods: Helios's sun comes back now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDawn()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            b.helios.dawnUntil = -1;
            Say(OwnHelios.BeginDawn(b, map) ? "those up walk out to the east side and face the sunrise." : "no open ground to the east.");
        }
    }

    /// <summary>Dev mode: Selene's happenings now (Debug actions, "Rimfolk", "Gods: Selene's ... now").</summary>
    public static class OwnSeleneDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        private static readonly string[] Kinds = { "Found", "GoodDream", "Healed", "Nightmare", "Frost", "AnimalBack" };

        [DebugAction("Rimfolk", "Gods: Selene's morning card now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMorning()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            var p = map?.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).RandomElementWithFallback();
            if (b == null || p == null) return;
            Find.WindowStack.Add(new FloatMenu(Kinds.Select(k => new FloatMenuOption(k, () =>
            {
                OwnSelene.Note(b.selene, p, k, false);
                OwnSelene.TellMorning(b, OwnSelene.God, map, true);
                Say("the morning card for " + p.LabelShort + " (" + k + "; with Found, the newest thing by their bed).");
            })).ToList()));
        }

        [DebugAction("Rimfolk", "Gods: Selene's face in the water now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWater()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var water = OwnSelene.StillWater(map);
            var p = water.IsValid ? map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Ours(x) && x.Awake() && x.CanReach(water, PathEndMode.Touch, Danger.Some))
                .OrderBy(x => x.Position.DistanceToSquared(water)).FirstOrDefault() : null;
            Say(OwnSelene.BeginWater(b, map, water, p) ? p.LabelShort + " walks down to the water where the moon lies." : "no still water within reach (or nobody awake).");
        }

        [DebugAction("Rimfolk", "Gods: Selene keeps a watch company now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWatch()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var p = OwnSelene.Watcher(map) ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault(OwnHappenings.Ours);
            Say(OwnSelene.Company(b, map, p) ? "the moonlight keeps " + p.LabelShort + " company till dawn." : "nobody to keep company.");
        }

        [DebugAction("Rimfolk", "Gods: Selene's dark face kept now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDarkFace()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var p = OwnSelene.Keeper(map);
            Say(OwnSelene.BeginDarkFace(b, map, p) ? p.LabelShort + " goes to keep the door till three o'clock; the dark that drifts does not come in meanwhile." : "nobody free to keep it.");
        }
    }

    /// <summary>Dev mode: Murmur's happenings now (Debug actions, "Rimfolk", "Gods: Murmur's ... now").</summary>
    public static class OwnMurmurDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: Murmur sung to at the well now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSong()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            b.murmur.songUntil = -1;
            Say(OwnMurmur.BeginSong(b, map, true) ? "they walk to the water and sing; answered, the rain comes within the day (\"Gods: Murmur's rain now\")." : "no water, or nobody free.");
        }

        [DebugAction("Rimfolk", "Gods: Murmur's rain now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRain()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            Say(OwnMurmur.Rain(b, OwnMurmur.God, map) ? "the rain comes; when it stops, the double rainbow (with Seasonal Weather)." : "it couldn't.");
        }

        [DebugAction("Rimfolk", "Gods: Murmur's water gives back now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGiveBack()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            b.murmur.giveUntil = -1;
            Say(OwnMurmur.GiveBack(b, map) ? "things lie on the mud at the water's edge; someone walks down to them." : "no water's edge near the colony.");
        }

        [DebugAction("Rimfolk", "Gods: Murmur's frogs come in now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFrogs()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            Say(OwnMurmur.Frogs(b, map) ? "the frogs come in (Odyssey's bullfrogs; without it only their croaking), gone at first light." : "no frogs and no frog sound in this game.");
        }
    }

    /// <summary>Dev mode: Ducat's happenings now (Debug actions, "Rimfolk", "Gods: Ducat's ... now").</summary>
    public static class OwnDucatDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: Ducat's trail of coins now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTrail()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var store = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned).OrderByDescending(t => t.stackCount).FirstOrDefault();
            var from = store?.Position ?? OwnHappenings.Middle(map);
            b.ducat.trailUntil = -1;
            Say(OwnDucat.Lay(b, map, from, 30) ? "single coins lie from " + (store != null ? "the silver" : "the middle") + " out to the edge; in daylight the first one free follows them." : "no way out to lay them on.");
        }

        [DebugAction("Rimfolk", "Gods: Ducat's open purse now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPurse()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var giver = OwnDucat.Giver(map, OwnDucat.God, true);
            var v = giver == null ? null : OwnDucat.Visitor(map, giver);
            b.ducat.purseUntil = -1;
            Say(OwnDucat.BeginPurse(b, map, giver, v) ? giver.LabelShort + " walks over to " + v.LabelShort + " with a few coins." : "no visitor on the land, nobody free, or no silver in the stores.");
        }

        [DebugAction("Rimfolk", "Gods: Ducat's beetle leads to a coin now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBeetle()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var table = GodOmens.Table(map);
            var seer = table == null ? null : map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).OrderBy(p => p.Position.DistanceToSquared(table.Position)).FirstOrDefault();
            b.ducat.beetleUntil = -1;
            Say(OwnDucat.Lead(b, map, table, seer) ? "a golden beetle walks off the table; " + seer.LabelShort + " follows it to a coin." : "no table, or nobody near one.");
        }
    }

    /// <summary>Dev mode: Tamiu's two new happenings now (Debug actions, "Rimfolk", "Gods: Tamiu's ... now").</summary>
    public static class OwnTamiuDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: Tamiu's cat at the threshold now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugThreshold()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var p = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(OwnTamiu.NearDeath) ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault(x => x.Downed || x.InBed());
            b.tamiu.EndThreshold();
            Say(p != null && OwnTamiu.Threshold(b, map, p) ? "a cat comes to lie by " + p.LabelShort + " (it goes six hours after they are out of danger)." : "nobody down or abed here.");
        }

        [DebugAction("Rimfolk", "Gods: Tamiu's cat by the lost dead now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLost()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var body = OwnTamiu.LostBody(map, out _) ?? map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>()
                .FirstOrDefault(c => c.Spawned && c.InnerPawn?.Faction == Faction.OfPlayer && c.InnerPawn.RaceProps.Humanlike);
            b.tamiu.EndLost();
            Say(body != null && OwnTamiu.Lost(b, map, body) ? "a cat walks out to " + body.InnerPawn.LabelShort + "'s body and cries there until someone comes." : "no body of the colony's lying out.");
        }
    }
}
