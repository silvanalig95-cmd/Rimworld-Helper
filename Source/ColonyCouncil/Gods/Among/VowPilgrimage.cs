using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A pilgrim home from a holy place with a tale of that people's to tell at supper (VowPilgrimage).</summary>
    public class PilgrimTale : IExposable
    {
        public Pawn pawn;
        public int god = -1;
        /// <summary>The people whose tale it is (the colony's own faction for an old shrine), and their name for the god.</summary>
        public Faction people;
        public string name;
        public int until = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref until, "until", -1);
        }
    }

    /// <summary>
    /// A vowed pilgrimage that really walks out (C6 of docs/content-update-proposals.md). The pilgrimage vow
    /// (Gods/Worship/Vows.cs) used to walk to the far edge of the colony's own land. When a holy place of the vower's god lies
    /// within reach in the region (GodsAmongMath.InReach: a road of one to six days each way), they go there instead:
    /// <list type="bullet">
    /// <item>a town of a people that holds the god (Gods.Of), first;</item>
    /// <item>the pilgrim fellowship's shrine, a town of theirs;</item>
    /// <item>an old shrine by a landmark of the region.</item>
    /// </list>
    /// They go as the fellowship's pilgrims and the world events' errands do (Peoples/Events2/SeasonAway.cs): a walk to the
    /// map's edge, a caravan of one on the game's own roads, their days at the place, a caravan of one home. At the place the
    /// pilgrimage is made (the vow is kept when they are home, Vows.Daily). Home again they bring something of the place
    /// (Vanilla Ideology Expanded - Relics and Artifacts' small inert relics from a people's town or an old shrine, the
    /// fellowship's shell from theirs, else a pilgrim's token), the book hears that people's name for the god, and at the
    /// next supper they tell one piece of that people's tale of the god (GodTales.Tell, by that name). The road's own harm
    /// is the fellowship's: the pilgrims' cough from a crowded town, through the budget. Someone the colony can't spare,
    /// or with no holy place in reach, walks on the colony's own land as before.
    /// </summary>
    public static class VowPilgrimage
    {
        public const string AwayKey = "vowpilgrim";

        private static readonly string[] TownTokens = { "VIE_RelicInertStatuette", "VIE_RelicInertIdol", "VIE_RelicInertMedallion", "VIE_RelicInertAmulet" };
        private static readonly string[] OldTokens = { "VIE_RelicInertPolishedRock", "VIE_RelicInertFossil" };

        public static ThingDef TokenDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_PilgrimToken");

        private class Holy
        {
            public GodsAmongMath.HolyKind kind;
            public PlanetTile tile;
            public string name;
            public float road;
            public Faction people;
        }

        /// <summary>The holy places of the god within reach of a walk from this home, each with its kind and road.</summary>
        private static List<Holy> Places(Map map, God g)
        {
            var list = new List<Holy>();
            try
            {
                var fellowship = PeoplesD.Friendly(PeoplesMath.Pilgrims);
                foreach (var s in Find.WorldObjects.Settlements)
                {
                    if (s == null || s.Destroyed || s.Faction == null || s.Faction.IsPlayer || s.Faction.HostileTo(Faction.OfPlayer) || s.Tile.Layer != map.Tile.Layer) continue;
                    if (!Find.WorldReachability.CanReach(map.Tile, s.Tile)) continue;
                    float road = SeasonAway.RoadDays(map, s.Tile);
                    if (!GodsAmongMath.InReach(road)) continue;
                    if (Gods.Of(s.Faction).Contains(g)) list.Add(new Holy { kind = GodsAmongMath.HolyKind.Town, tile = s.Tile, name = s.Name, road = road, people = s.Faction });
                    else if (s.Faction == fellowship)
                        list.Add(new Holy
                        {
                            kind = GodsAmongMath.HolyKind.Fellowship, tile = s.Tile, name = "CC_GA_FellowshipShrine".Translate(s.Name.Named("TOWN")).Resolve(), road = road, people = s.Faction
                        });
                }
                var marks = Find.World?.landmarks?.landmarks;
                if (marks != null)
                    foreach (var kv in marks)
                    {
                        if (kv.Value == null || kv.Value.name.NullOrEmpty() || !kv.Key.Valid || kv.Key.Layer != map.Tile.Layer || kv.Key == map.Tile) continue;
                        if (!Find.WorldReachability.CanReach(map.Tile, kv.Key)) continue;
                        float road = SeasonAway.RoadDays(map, kv.Key);
                        if (!GodsAmongMath.InReach(road)) continue;
                        list.Add(new Holy { kind = GodsAmongMath.HolyKind.Old, tile = kv.Key, name = "CC_PEv2_OldShrine".Translate(kv.Value.name.Named("NAME")).Resolve(), road = road });
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: a pilgrimage's holy places", e);
            }
            return list;
        }

        /// <summary>
        /// The vow's hour has come to set out (Vows.TrySetOut): a holy place of the god in reach and someone the colony can
        /// spare, and they set out for it as a caravan of their own. False: they walk on the colony's own land instead.
        /// </summary>
        public static bool TryFar(Pawn p, God g, Map map)
        {
            if (p == null || g == null || map == null || SeasonAway.OutJob == null || !PeopleEvents2.MayLeave(p, map)) return false;
            if (!RCellFinder.TryFindBestExitSpot(p, out _, TraverseMode.ByPawn)) return false;
            var places = Places(map, g);
            int i = GodsAmongMath.PickHoly(places.Select(x => (x.kind, x.road)).ToList());
            if (i < 0) return false;
            var h = places[i];
            float days = GodsAmongMath.DaysThere(Rand.Value);
            var f = h.people ?? Faction.OfPlayer;
            var v = SeasonAway.Send(AwayKey, null, p, null, f, h.tile, h.name, days, map, g.id + "/" + h.kind);
            if (v == null) return false;
            Worship.Say(p, "Rimfolk_Pool_Worship_PilgrimOut", g);
            Tell.Minor(NewsKind.People, Importance.Notable, map, "CC_GA_PilgrimFarOut".Translate(p.Named("PAWN"), Worship.Called(g).Named("GOD"), h.name.Named("PLACE"),
                GodsAmongMath.AwayDays(h.road, days).Named("DAYS")).Resolve(), p, MessageTypeDefOf.NeutralEvent);
            return true;
        }

        /// <summary>The god and the kind of place a vowed pilgrimage under way is for (its season's own part, "god/kind").</summary>
        internal static bool Read(Visit v, out God g, out GodsAmongMath.HolyKind kind)
        {
            g = null;
            kind = GodsAmongMath.HolyKind.Old;
            var bits = (SeasonAway.ExtraOf(v) ?? "").Split('/');
            if (bits.Length < 2 || !int.TryParse(bits[0], out int id)) return false;
            g = Gods.ById(id);
            Enum.TryParse(bits[1], out kind);
            return g != null;
        }

        /// <summary>That people's name for a god (its age's, by the people, the same each time); the colony's own for an old shrine.</summary>
        public static string PeopleName(God g, Faction f)
        {
            if (g == null) return "";
            if (f == null || f.IsPlayer || g.def == null) return Worship.Called(g);
            int age = (int)Ages.Of(f);
            return GodLoreMath.NameFor(g.name, g.def.NamesOf(age), f.loadID, g.id);
        }

        /// <summary>Something of the place, set down at their feet as they come in: its label, or null.</summary>
        internal static string Keepsake(Pawn p, GodsAmongMath.HolyKind kind)
        {
            if (p == null || !p.Spawned) return null;
            ThingDef def = null;
            if (kind == GodsAmongMath.HolyKind.Fellowship) def = Pilgrimage.ShellDef;
            else if (kind == GodsAmongMath.HolyKind.Town) def = TownTokens.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).RandomElementWithFallback();
            else def = OldTokens.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).RandomElementWithFallback();
            def ??= TokenDef;
            if (def == null) return null;
            try
            {
                var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                return GenPlace.TryPlaceThing(t, p.Position, p.Map, ThingPlaceMode.Near) ? t.LabelNoCount : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: a pilgrim's keepsake", e);
                return null;
            }
        }

        /// <summary>The tale owed at supper, kept until told (or its days are up).</summary>
        internal static void Owe(Pawn p, God g, Faction f, string name)
        {
            var b = GodsAmongBook.Book;
            if (b == null || p == null || g == null) return;
            b.tales.RemoveAll(t => t == null || t.pawn == p);
            b.tales.Add(new PilgrimTale { pawn = p, god = g.id, people = f, name = name, until = GodsAmongBook.Now + GodsAmongMath.TaleDays * GenDate.TicksPerDay });
            while (b.tales.Count > GodsAmongBook.MaxTales) b.tales.RemoveAt(0);
        }

        /// <summary>The gods' hour: a pilgrim home at supper time tells someone beside them one piece of that people's tale of the god.</summary>
        public static void Hourly(int now)
        {
            var b = GodsAmongBook.Book;
            if (b == null || b.tales.Count == 0) return;
            foreach (var t in b.tales.ToList())
            {
                var p = t?.pawn;
                var g = t == null ? null : Gods.ById(t.god);
                if (p == null || g == null || p.Dead || now > t.until)
                {
                    b.tales.Remove(t);
                    continue;
                }
                if (!p.Spawned || !Homes.IsHome(p.Map) || p.Downed || p.InMentalState || p.Drafted || !p.Awake()) continue;
                int hour = GenLocalDate.HourOfDay(p.Map);
                if (hour < GodsAmongMath.TaleFrom || hour >= GodsAmongMath.TaleTo) continue;
                var listener = p.Map.mapPawns.FreeColonistsSpawned.Where(x => x != p && Together.Can(p, x)).InRandomOrder().FirstOrDefault();
                if (listener == null) continue;
                b.tales.Remove(t);
                // together: the listener was found beside the pilgrim (Together.Can), and Speech.Say checks it again.
                if (!GodTales.Tell(p, listener, new List<string> { g.Key }, "Supper", t.name))
                    GodTales.WhatOf(p, listener, new List<string> { g.Key }, false, "Supper");
            }
        }

        /// <summary>Dev: a vowed pilgrimage set going now for someone with a god, to the best holy place in reach (the vow made first when they have none).</summary>
        public static string DevSend(Pawn p)
        {
            var g = Worship.GodOf(p);
            if (g == null) return p.LabelShort + " holds no god.";
            if (!Vows.HasOpen(p, VowKind.Pilgrimage) && Vows.Make(p, g, VowKind.Pilgrimage) == null) return "no vow could be made.";
            return TryFar(p, g, p.Map) ? p.LabelShort + " sets out for a holy place of " + Gods.Name(g) + "." : "no holy place of " + Gods.Name(g) + " in reach, or " + p.LabelShort + " can't be spared.";
        }
    }

    /// <summary>The vowed pilgrimage's season: there, the pilgrimage made; home, something of the place, that people's name for the god, and a tale owed at supper.</summary>
    public class VowPilgrimAway : AwayKind
    {
        public override string Key => VowPilgrimage.AwayKey;

        public override void Arrived(Visit v, Map home, Faction f, string place)
        {
            var p = v.x;
            if (!VowPilgrimage.Read(v, out var g, out _)) return;
            // The pilgrimage is made at the place: the vow reads it when they are home (Vows.Daily).
            Worship.Book?.AddMark(p, "pilgrimage", g.id);
            Worship.Favour(g, home, 3f, "CC_Wsh_Why_Pilgrimage".Translate(p.LabelShort.Named("PAWN")).Resolve(), FavourSource.Rite);
            Tell.Message(NewsKind.People, Importance.Notable, home, "CC_GA_PilgrimThere".Translate(p.Named("PAWN"), place.Named("PLACE"), Worship.Called(g).Named("GOD")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        public override void Home(Visit v, Map home, Faction f, bool served)
        {
            var p = v.x;
            string place = SeasonAway.PlaceOf(v);
            if (!VowPilgrimage.Read(v, out var g, out var kind) || p == null)
            {
                Tell.Message(NewsKind.People, Importance.Notable, home, "CC_PEv2_PilgrimHomeEarly".Translate(p.Named("PAWN"), place.Named("PLACE")).Resolve(),
                    p != null && p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }
            var people = f != null && !f.IsPlayer ? f : null;
            if (!served)
            {
                Tell.Message(NewsKind.People, Importance.Notable, home, "CC_PEv2_PilgrimHomeEarly".Translate(p.Named("PAWN"), place.Named("PLACE")).Resolve(),
                    p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }
            string thing = VowPilgrimage.Keepsake(p, kind);
            string name = VowPilgrimage.PeopleName(g, people);
            string colony = Worship.Called(g);
            // The book hears that people's name for the god, as the pilgrim heard it there.
            if (people != null && !name.NullOrEmpty())
                GodLore.Heard(p.LabelShort, g, "CC_GA_PilgrimNameHeard".Translate(place.Named("PLACE"), people.Name.Named("PEOPLE"), name.Named("NAME")).Resolve(), "Pilgrim", null, -1,
                    name, people, p.LabelShort);
            VowPilgrimage.Owe(p, g, people, people != null ? name : null);
            string key = thing != null ? people != null && name != colony ? "CC_GA_PilgrimHomeNamed" : "CC_GA_PilgrimHome" : "CC_GA_PilgrimHomeNothing";
            Tell.Message(NewsKind.People, Importance.Notable, home, key.Translate(p.Named("PAWN"), place.Named("PLACE"), (thing ?? "").Named("THING"), colony.Named("GOD"),
                (people?.Name ?? "").Named("PEOPLE"), name.Named("NAME")).Resolve(), p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_GA_PilgrimChronicle".Translate(p.Named("PAWN"), place.Named("PLACE"), colony.Named("GOD")).Resolve(), p));
            // The road's own harm, from a crowded town (the fellowship's cough), through the budget and shown.
            var cold = DefDatabase<HediffDef>.GetNamedSilentFail("Council_Ail_HeadCold");
            if (people != null && cold != null && AilmentTracker.On && p.Spawned && Harm.Roll(p, HarmKinds.PilgrimCough, PeopleEvents2Math.PilgrimCough, HurtClass.Mild)
                && Harm.Ail(p, cold, Mathf.Max(0.1f, cold.initialSeverity), HarmKinds.PilgrimCough, HurtClass.Mild) != null)
                Tell.Message(NewsKind.Health, Importance.Notable, home, "CC_PEv2_PilgrimCough".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
        }
    }
}
