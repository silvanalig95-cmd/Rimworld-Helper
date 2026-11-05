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
    /// One of the colony's own vassals at a home (Vassals.cs): the hamlet once sworn, or a struggling town taken under its
    /// protection. What isn't the tie: the town and its people, the council's tribute level, and when its harvest, its
    /// season's tribute, the quartermaster's dues, its levies and its last ask were, when its head's regard fell low, and
    /// whether it asked to be released.
    /// </summary>
    public class VassalTerms : IExposable
    {
        public int town = -1;
        public VassalKind kind;
        public Faction people;
        public string name;
        public TributeLevel level = TributeLevel.Usual;
        public int since = -1, harvestAt = -1, seasonAt = -1, duesAt = -1, leviesAt = -1, leviesDue = -1, askAt = -1, lowSince = -1, releaseAt = -1;
        /// <summary>Their dead in the colony's fights, all told.</summary>
        public int fell;

        public void ExposeData()
        {
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref level, "level", TributeLevel.Usual);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref harvestAt, "harvestAt", -1);
            Scribe_Values.Look(ref seasonAt, "seasonAt", -1);
            Scribe_Values.Look(ref duesAt, "duesAt", -1);
            Scribe_Values.Look(ref leviesAt, "leviesAt", -1);
            Scribe_Values.Look(ref leviesDue, "leviesDue", -1);
            Scribe_Values.Look(ref askAt, "askAt", -1);
            Scribe_Values.Look(ref lowSince, "lowSince", -1);
            Scribe_Values.Look(ref releaseAt, "releaseAt", -1);
            Scribe_Values.Look(ref fell, "fell");
        }
    }

    /// <summary>
    /// Vassals of the colony's own (docs/design/stage2-diplomacy.md, round c, item 10; decision 12; release 2c.2; the rules
    /// in FealtyMath.cs). They come three ways: the hamlet that swore fealty (World/Region/HamletLife.cs), a struggling town
    /// within reach (under 20 prosperity for 30 days, its people at peace with the colony and at war elsewhere) taken under
    /// its protection when it asks (once a year a home, two at most), and Faction Territories' vassals as the mod makes them.
    /// Each vassal's head is a notable who writes: a town's own head, and for the mod's outposts one Rimfolk gives them
    /// (World/VassalNotables.cs), whose regard shows with what moved it.
    /// <para>Of the colony's own: at harvest their carter brings produce to the gate, the amount set by the council (light,
    /// usual, heavy: heavy costs their goodwill each season); the quartermaster collects a season's dues by letter, on time or
    /// explained (CouncilTasks' Dues); when a home is raided the council may call their levies (their people's war band, a
    /// third of the threat, once a season), whose dead weigh on their head's regard; their head, sour for a season (-20),
    /// asks to be released, and refused they break away. Asks of any vassal (once a season): help in a hard season, the
    /// colony's judgement in a quarrel with a neighbour, a camp of the colony's enemies near them to be broken, and for the
    /// mod's outposts its invasion of them, joined through the mod's own arrival. The mod's vassalage points, purchases and
    /// tribute stay the mod's: Rimfolk takes no tribute or levies from them and changes nothing of theirs.</para>
    /// </summary>
    public static class Vassals
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private const int Day = FealtyMath.DayTicks;

        public static bool On => Fealty.On;

        // ================= Reading =================

        /// <summary>A home's own vassals (none made).</summary>
        public static List<VassalTerms> Of(Map home) => DiplomacyBook.Instance?.Existing(home)?.vassals ?? new List<VassalTerms>();

        /// <summary>Every home's own vassals, with the home.</summary>
        public static List<(Map home, VassalTerms v)> All()
        {
            var list = new List<(Map, VassalTerms)>();
            var book = DiplomacyBook.Instance;
            if (book == null) return list;
            foreach (var h in book.Live())
                foreach (var v in h.vassals)
                    if (v != null)
                        list.Add((h.Map, v));
            return list;
        }

        /// <summary>The colony's own vassal that a town is, with its home; null when it is none.</summary>
        public static VassalTerms At(int town, out Map home)
        {
            home = null;
            if (town < 0) return null;
            foreach (var (m, v) in All())
                if (v.town == town)
                {
                    home = m;
                    return v;
                }
            return null;
        }

        public static Settlement TownOf(VassalTerms v)
        {
            var s = v != null ? NotableBook.TownOf(v.town) : null;
            return s != null && !s.Destroyed ? s : null;
        }

        /// <summary>A vassal's tie to the colony: the hamlet's own, or the protection's.</summary>
        public static Tie TieOf(VassalTerms v, Map home)
        {
            if (v == null) return null;
            if (v.kind == VassalKind.Hamlet) return Hamlets.TieOf(Hamlets.Peek(home));
            var at = Ref.Town(v.town);
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Fealty))
                if (t != null && t.Live && t.origin == FealtyMath.VassalOrigin && t.from == at)
                    return t;
            return null;
        }

        /// <summary>A vassal's head: the town's head the notables know.</summary>
        public static Notable HeadOf(int town)
        {
            var book = NotableBook.Instance;
            if (book == null || town < 0) return null;
            return book.notables.FirstOrDefault(n => n != null && n.town == town && n.role == NotableRole.Head && n.Alive);
        }

        public static int Regard(int town)
        {
            var n = HeadOf(town);
            return n != null ? NotableBook.Opinion(n) : 0;
        }

        /// <summary>Faction Territories' vassals of this home (views, read each 250-tick stretch).</summary>
        public static List<Holding> Outposts(Map home) => WorldMechanics.OurVassals(home);

        /// <summary>One of Faction Territories' vassals by its world object's id, or null.</summary>
        public static Holding Outpost(int id)
        {
            foreach (var h in WorldMechanics.OurVassals())
                if (h.id == id)
                    return h;
            return null;
        }

        private static readonly HashSet<int> outpostIds = new HashSet<int>();
        private static int outpostsAt = -1;
        private static Game outpostsGame;

        /// <summary>The ids of Faction Territories' vassal outposts, read once a 250-tick stretch (none without the mod).</summary>
        private static HashSet<int> OutpostIds()
        {
            int stretch = Now / Sched.RareTicks;
            if (outpostsAt == stretch && outpostsGame == Current.Game) return outpostIds;
            outpostsAt = stretch;
            outpostsGame = Current.Game;
            outpostIds.Clear();
            if (Territories.Active)
                foreach (var h in WorldMechanics.OurVassals())
                    outpostIds.Add(h.id);
            return outpostIds;
        }

        /// <summary>A notable who heads one of the mod's vassal outposts (not a town of their people).</summary>
        public static bool OutpostHead(Notable n) => n != null && n.role == NotableRole.Head && n.town >= 0 && OutpostIds().Contains(n.town);

        /// <summary>A vassal's head is kept among the notables while the vassal stands, wherever it lies (World/Notables.cs).</summary>
        public static bool KeepsHead(Notable n) => n != null && n.role == NotableRole.Head && n.town >= 0 && (OutpostIds().Contains(n.town) || At(n.town, out _) != null);

        /// <summary>The vassals' names in words, for the cards and the This week rows.</summary>
        public static string NameOf(VassalTerms v) => TownOf(v)?.Name ?? v?.name ?? "?";

        // ================= The day =================

        /// <summary>The vassals' day (Fealty.Daily): the records kept true, each vassal's season, harvest, low regard and asks; struggling towns' asks; the mod's outposts' faces and asks.</summary>
        public static void Daily(int now)
        {
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            foreach (var home in Homes.All.ToList())
            {
                try
                {
                    var h = book.HomeOf(home);
                    Sync(home, h, now);
                    if (!On) continue;
                    foreach (var v in h.vassals.ToList()) Day1(home, v, now);
                    Outposts(home, now);
                    if (Tell.CanAsk) LookForStruggling(home, h, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("vassals: the day", e);
                }
            }
            Promises(now);
        }

        /// <summary>The records match what stands: the hamlet sworn has one; one whose town or tie is gone is let go.</summary>
        private static void Sync(Map home, DiplomacyHome h, int now)
        {
            var hamlet = Hamlets.Peek(home);
            bool sworn = hamlet != null && hamlet.stage == HamletStage.Sworn && hamlet.town >= 0 && Hamlets.TownOf(hamlet) != null;
            var rec = h.vassals.FirstOrDefault(v => v.kind == VassalKind.Hamlet);
            if (sworn && rec == null && h.vassals.Count < FealtyMath.MaxVassals)
                h.vassals.Add(new VassalTerms { town = hamlet.town, kind = VassalKind.Hamlet, people = hamlet.people, name = hamlet.name, since = now, seasonAt = now, harvestAt = hamlet.harvestAt });
            else if (rec != null && (!sworn || rec.town != hamlet.town)) h.vassals.Remove(rec);
            foreach (var v in h.vassals.Where(v => v.kind == VassalKind.Protected).ToList())
            {
                var t = TownOf(v);
                var tie = TieOf(v, home);
                if (t == null || tie == null || t.Faction != v.people || v.people == null || v.people.defeated)
                {
                    if (tie != null && tie.Live) WorldLedger.Set(tie, Tie.State.Ended);
                    h.vassals.Remove(v);
                    Diplomacy.Dirty();
                    continue;
                }
                if (t.HasName) v.name = t.Name;
                // Their people turned on the colony: the protection ends with nobody at fault, told.
                if (Diplomacy.Hostile(v.people))
                {
                    WorldLedger.Set(tie, Tie.State.Ended);
                    h.vassals.Remove(v);
                    // apart: news of the town under the colony's protection, told in a message.
                    Tell.Message(NewsKind.World, Importance.Important, home, FealtyWords.P("CC_Vassal_Msg_War", v.name.Named("TOWN"), FealtyWords.People(v.people).Named("PEOPLE")),
                        LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
                    Diplomacy.Dirty();
                }
            }
        }

        /// <summary>One vassal's day: the season's tribute weighing on them (heavy) or warming them (light), a protected town's harvest, their head's low regard, an ask.</summary>
        private static void Day1(Map home, VassalTerms v, int now)
        {
            var head = HeadOf(v.town);
            if (v.kind == VassalKind.Protected) NotableBook.StaffVassal(TownOf(v));
            if (v.seasonAt < 0) v.seasonAt = now;
            if (now - v.seasonAt >= FealtyMath.SeasonDays * Day)
            {
                v.seasonAt = now;
                if (v.level == TributeLevel.Heavy)
                {
                    Goodwill(v.people, -FealtyMath.HeavyGoodwill);
                    Regard(v.town, -FealtyMath.HeavyRegard, FealtyWords.P("CC_Vassal_Note_Heavy"));
                }
                else if (v.level == TributeLevel.Light) Regard(v.town, FealtyMath.LightRegard, FealtyWords.P("CC_Vassal_Note_Light"));
            }
            if (v.kind == VassalKind.Protected && HarvestTime(home) && (v.harvestAt < 0 || now - v.harvestAt >= 30 * Day)) Harvest(home, v, now);
            int regard = head != null ? NotableBook.Opinion(head) : 0;
            v.lowSince = head != null ? FealtyMath.LowSince(regard, v.lowSince, now) : -1;
            if (!Tell.CanAsk || !FealtyMath.MayAsk(v.askAt, now)) return;
            bool release = v.releaseAt < 0 && FealtyMath.AsksRelease(v.lowSince, now);
            if (!release && !Rand.Chance(FealtyMath.AskDaily)) return;
            var town = TownOf(v);
            if (town == null) return;
            var ask = FealtyMath.AskOf(release, CampNear(town.Tile) != null, false, HardSeason(v.town, town.Tile), Neighbour(v.people, town.Tile) != null, false);
            if (ask == null) return;
            v.askAt = now;
            if (ask == VassalAsk.Release) v.releaseAt = now;
            Ask(home, v.town, v.people, ask.Value, town.Tile);
        }

        private static bool HarvestTime(Map home)
        {
            var s = GenLocalDate.Season(home);
            if (s == Season.Fall) return true;
            if (s != Season.PermanentSummer && s != Season.PermanentWinter) return false;
            return GenDate.Quadrum(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(home.Tile).x) == Quadrum.Septober;
        }

        /// <summary>A protected town's carter brings the first of its harvest to the gate, as much as the council's level asks.</summary>
        private static void Harvest(Map home, VassalTerms v, int now)
        {
            v.harvestAt = now;
            var def = new[] { "RawPotatoes", "RawCorn", "RawRice" }.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null);
            if (def == null) return;
            var rec = WorldLedger.TownRecord(v.town);
            int n = FealtyMath.Produce(FealtyMath.ProtectedProduce(rec != null && rec.Seeded ? rec.prosperity : 30f), v.level);
            var gate = PlaySpots.GateCell(home, null);
            var things = Couriers.Make(def, n);
            bool carried = Couriers.Deliver(home, things, v.people, gate);
            if (!carried) Loot.Deliver(things, home, gate, false);
            FactionMemory.Note(v.people, HamletMath.DeedHarvest, v.name);
            Write(home, v.town, v.people, FealtyWords.P("CC_Vassal_Hist_Harvest", NameOf(v).Named("TOWN"), Couriers.Amount(n, null, def).Named("GOODS")));
            Tell.Letter(NewsKind.World, Importance.Notable, home, FealtyWords.P("CC_Vassal_Harvest_Label", NameOf(v).Named("TOWN")),
                FealtyWords.T("CC_Vassal_Harvest", NameOf(v).Named("TOWN"), Couriers.Amount(n, null, def).Named("GOODS"), FealtyWords.Level(v.level).Named("LEVEL")), LetterDefOf.PositiveEvent,
                new LookTargets(gate, home), v.people);
        }

        /// <summary>The hamlet's harvest by the council's level once it has sworn (World/Region/HamletLife.cs: its carter's load).</summary>
        public static int HamletProduce(Map home, HamletHome h, int usual)
        {
            if (h == null || h.stage != HamletStage.Sworn || !On) return usual;
            var v = Of(home).FirstOrDefault(x => x.kind == VassalKind.Hamlet && x.town == h.town);
            return v == null ? usual : FealtyMath.Produce(usual, v.level);
        }

        // ================= A struggling town asks for protection =================

        private static void LookForStruggling(Map home, DiplomacyHome h, int now)
        {
            int count = h.vassals.Count(v => v.kind == VassalKind.Protected);
            var reach = RegionTowns.Of(home);
            if (reach == null || count >= FealtyMath.MaxProtected || !Rand.Chance(FealtyMath.ProtectAskDaily)) return;
            foreach (var s in reach.towns)
            {
                var f = s?.Faction;
                if (f == null || f.IsPlayer || s.Destroyed || !Diplomacy.Deals(f) || At(s.ID, out _) != null) continue;
                if (Hamlets.IsHamlet(s.ID) || TradingPosts.IsPost(s.ID) || NotableBook.SeatOf(f) == s) continue;
                var rec = WorldLedger.TownRecord(s.ID);
                if (rec == null || !rec.Seeded) continue;
                if (!FealtyMath.AsksProtection(rec.prosperity, rec.lowSince, now, Diplomacy.Hostile(f), AtWarElsewhere(f), Ages.IsEmpire(f), count, h.protectAskAt)) continue;
                h.protectAskAt = now;
                NotableBook.StaffVassal(s);
                Ask(home, s.ID, f, VassalAsk.Protection, s.Tile);
                return;
            }
        }

        private static bool AtWarElsewhere(Faction f) =>
            Find.FactionManager.AllFactionsListForReading.Any(o => o != null && o != f && !o.IsPlayer && !o.defeated && !o.Hidden && o.def != null && o.def.humanlikeFaction && o.HostileTo(f));

        /// <summary>The colony takes the town under its protection: the tie (from the town to the colony), the record, the history and the letter.</summary>
        public static void Take(Map home, Settlement s)
        {
            var h = DiplomacyBook.Instance?.HomeOf(home);
            if (h == null || s == null || s.Destroyed || At(s.ID, out _) != null || h.vassals.Count(v => v.kind == VassalKind.Protected) >= FealtyMath.MaxProtected) return;
            var f = s.Faction;
            var t = WorldLedger.AddTie(Tie.Kind.Fealty, Refs.Of(s), Diplomacy.Colony(home), new TieTerms { key = FealtyMath.ProtectedKey, about = Refs.Of(f) }, Tie.Never, FealtyMath.VassalOrigin);
            if (t == null) return;
            int now = Now;
            h.vassals.Add(new VassalTerms { town = s.ID, kind = VassalKind.Protected, people = f, name = s.Name, since = now, seasonAt = now });
            NotableBook.StaffVassal(s);
            Regard(s.ID, 10f, FealtyWords.P("CC_Vassal_Note_Taken"));
            FactionMemory.Note(f, "swore", s.Name);
            var form = FealtyWords.ProtectionForm(home, f);
            Write(home, s.ID, f, FealtyWords.P("CC_Vassal_Hist_Taken", s.Name.Named("TOWN"), form.word.Named("WORD")), true);
            Tell.Letter(NewsKind.World, Importance.Important, home, FealtyWords.P("CC_Vassal_Taken_Label", s.Name.Named("TOWN")),
                FealtyWords.T("CC_Vassal_Taken", s.Name.Named("TOWN"), FealtyWords.People(f).Named("PEOPLE"), form.word.Named("WORD")), LetterDefOf.PositiveEvent, new LookTargets(s), f);
            Diplomacy.Dirty();
        }

        // ================= The mod's outposts =================

        /// <summary>Faction Territories' vassals of a home: a head given to each whose first people are at peace with the colony, and their asks.</summary>
        private static void Outposts(Map home, int now)
        {
            foreach (var o in Outposts(home))
            {
                if (o?.thing == null || o.people == null || o.people.defeated || Diplomacy.Hostile(o.people)) continue;
                var head = NotableBook.StaffOutpost(o.thing, o.people, o.name);
                if (head == null || !Tell.CanAsk) continue;
                int last = head.Last(LetterPurpose.Fealty);
                if (!FealtyMath.MayAsk(last, now)) continue;
                bool invaded = Territories.InvasionAtOutpost(o.thing, out _);
                if (!invaded && !Rand.Chance(FealtyMath.AskDaily)) continue;
                var ask = FealtyMath.AskOf(false, CampNear(o.tile) != null, invaded, HardSeason(-1, o.tile), Neighbour(o.people, o.tile) != null, true);
                if (ask == null) continue;
                head.SetLast(LetterPurpose.Fealty, now);
                Ask(home, o.id, o.people, ask.Value, o.tile);
            }
        }

        // ================= Their asks =================

        /// <summary>A hostile site (a camp of the colony's enemies) near a tile, or null.</summary>
        public static Site CampNear(PlanetTile tile)
        {
            if (!tile.Valid) return null;
            foreach (var site in Find.WorldObjects.Sites)
                if (site != null && !site.Destroyed && site.Faction != null && site.Faction.HostileTo(Faction.OfPlayer) && site.Tile.Layer == tile.Layer
                    && Find.WorldGrid.ApproxDistanceInTiles(site.Tile, tile) <= FealtyMath.CampTiles)
                    return site;
            return null;
        }

        /// <summary>A hard season at a vassal: its fortune under 20, or a bad world event of hunger, sickness or cold near it.</summary>
        public static bool HardSeason(int town, PlanetTile tile)
        {
            var rec = town >= 0 ? WorldLedger.TownRecord(town) : null;
            if (rec != null && rec.Seeded && rec.prosperity < FealtyMath.StruggleProsperity) return true;
            return BadNear(tile, out _);
        }

        private static bool BadNear(PlanetTile tile, out bool sickness)
        {
            sickness = false;
            var ws = WorldSituations.Instance;
            if (ws == null || !tile.Valid) return false;
            foreach (var s in ws.Running)
            {
                if (s?.def == null || !s.def.bad || s.def.kind != "sickness" && s.def.kind != "hunger" && s.def.kind != "cold") continue;
                var at = RiseFall.TileOf(s.place);
                if (!at.Valid || at.Layer != tile.Layer || Find.WorldGrid.ApproxDistanceInTiles(at, tile) > 10f) continue;
                sickness = s.def.kind == "sickness";
                return true;
            }
            return false;
        }

        /// <summary>A neighbouring town of another people at peace with the colony, near the vassal: the other side of a quarrel.</summary>
        public static Settlement Neighbour(Faction people, PlanetTile tile)
        {
            if (people == null || !tile.Valid) return null;
            foreach (var s in Find.WorldObjects.Settlements)
                if (s != null && !s.Destroyed && s.Faction != null && s.Faction != people && !s.Faction.IsPlayer && !Diplomacy.Hostile(s.Faction) && s.Tile.Layer == tile.Layer
                    && Find.WorldGrid.ApproxDistanceInTiles(s.Tile, tile) <= FealtyMath.CampTiles && Diplomacy.Deals(s.Faction))
                    return s;
            return null;
        }

        /// <summary>Their head's letter asking something of the colony, its choices with what each costs named.</summary>
        public static void Ask(Map home, int town, Faction people, VassalAsk ask, PlanetTile tile)
        {
            var def = FealtyDefOf.Rimfolk_VassalAsk;
            if (def == null || home == null || people == null) return;
            var head = HeadOf(town);
            string place = NotableBook.TownOf(town)?.Name ?? Outpost(town)?.name ?? "?";
            string who = head != null ? NotableBook.Called(head) : FealtyWords.People(people);
            var args = new List<NamedArgument> { who.Named("WHO"), place.Named("TOWN"), FealtyWords.People(people).Named("PEOPLE") };
            string other = null;
            int camp = -1;
            bool sickness = false;
            switch (ask)
            {
                case VassalAsk.Camp:
                {
                    var site = CampNear(tile);
                    if (site == null) return;
                    camp = site.ID;
                    other = FealtyWords.People(site.Faction);
                    break;
                }
                case VassalAsk.Judgement:
                {
                    var n = Neighbour(people, tile);
                    if (n == null) return;
                    camp = n.ID;
                    other = n.Name;
                    break;
                }
                case VassalAsk.Help:
                    BadNear(tile, out sickness);
                    other = PlayCarry.Label(FealtyMath.HelpNeed(sickness), null);
                    break;
                case VassalAsk.Defend:
                    Territories.InvasionAtOutpost(Outpost(town)?.thing, out var attacker);
                    other = FealtyWords.People(attacker);
                    break;
            }
            args.Add((other ?? "").Named("OTHER"));
            args.Add(FealtyMath.HelpCount(sickness).Named("COUNT"));
            string text = FealtyWords.T("CC_Vassal_Ask_" + ask, args.ToArray()) + "\n\n" + FealtyWords.P("CC_Vassal_AskWhat_" + ask, args.ToArray());
            var letter = LetterMaker.MakeLetter(FealtyWords.P("CC_Vassal_Ask_" + ask + "_Label", args.ToArray()), text, def,
                NotableBook.TownOf(town) is Settlement s ? new LookTargets(s) : tile.Valid ? new LookTargets(tile) : LookTargets.Invalid, people);
            if (letter is ChoiceLetter_VassalAsk l)
            {
                l.home = Homes.KeyOf(home);
                l.town = town;
                l.people = people;
                l.ask = (int)ask;
                l.other = camp;
                l.sickness = sickness;
            }
            Tell.Ask(letter, home);
        }

        /// <summary>Their ask still stands (the letter's choices): the town still there and theirs or the colony's.</summary>
        public static bool AskValid(int town, Faction people, VassalAsk ask)
        {
            if (people == null || people.defeated || Diplomacy.Hostile(people) || !On) return false;
            if (ask == VassalAsk.Protection) return NotableBook.TownOf(town) is Settlement s && s.Faction == people && At(town, out _) == null;
            return At(town, out _) != null || Outpost(town) != null;
        }

        /// <summary>The colony's answer to an ask (ChoiceLetter_VassalAsk): what it does, their head's regard and a line on the map or in the news.</summary>
        public static void Answer(Map home, int town, Faction people, VassalAsk ask, int choice, int other, bool sickness)
        {
            var head = HeadOf(town);
            string place = NotableBook.TownOf(town)?.Name ?? Outpost(town)?.name ?? "?";
            switch (ask)
            {
                case VassalAsk.Help:
                    if (choice == 0)
                    {
                        string what = FealtyMath.HelpNeed(sickness);
                        int count = FealtyMath.HelpCount(sickness);
                        var ctx = new DilemmaContext { map = home, faction = people };
                        // apart: one of the colony carries the help out to the gate, where their carter waits to take it home.
                        new DilemmaEffect_CarryOut { what = what, count = count, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = FealtyWords.P("CC_Vassal_HelpLabel", place.Named("TOWN")) }
                            .Apply(ctx);
                        Regard(town, FealtyMath.HelpRegard, FealtyWords.P("CC_Vassal_Note_Helped"));
                        FactionMemory.Note(people, HamletMath.DeedHelped, place);
                    }
                    else Regard(town, FealtyMath.RefusedRegard, FealtyWords.P("CC_Vassal_Note_Refused"));
                    break;
                case VassalAsk.Judgement:
                {
                    var n = NotableBook.TownOf(other);
                    var f2 = n?.Faction;
                    if (choice == 0)
                    {
                        Regard(town, FealtyMath.SidedRegard, FealtyWords.P("CC_Vassal_Note_Sided"));
                        Goodwill(f2, -FealtyMath.JudgementGoodwill);
                    }
                    else if (choice == 1)
                    {
                        Regard(town, FealtyMath.SidedAgainstRegard, FealtyWords.P("CC_Vassal_Note_SidedAgainst", (n?.Name ?? "?").Named("OTHER")));
                        Goodwill(f2, FealtyMath.JudgementGoodwill);
                    }
                    else Regard(town, FealtyMath.KeptOutRegard, FealtyWords.P("CC_Vassal_Note_KeptOut"));
                    break;
                }
                case VassalAsk.Camp:
                case VassalAsk.Defend:
                    if (choice == 0) Promise(home, town, people, ask, other);
                    else Regard(town, FealtyMath.CampDeclinedRegard, FealtyWords.P("CC_Vassal_Note_Declined"));
                    if (choice == 0 && ask == VassalAsk.Defend && home != null) Find.WindowStack.Add(new Dialog_FormCaravan(home));
                    break;
                case VassalAsk.Release:
                    if (choice == 0) Release(home, town);
                    else BreakAway(home, town);
                    break;
                case VassalAsk.Protection:
                    if (choice == 0) Take(home, NotableBook.TownOf(town));
                    else if (head != null) head.Remember(FealtyWords.P("CC_Vassal_Note_TurnedDown"), -3f);
                    break;
            }
            var actors = new List<HistoryActor> { new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did), new HistoryActor(Refs.Of(people), HistoryRole.For) };
            if (ask != VassalAsk.Protection && ask != VassalAsk.Release)
                Diplomacy.Write(DiplomacyMath.Task, actors, Ref.Town(town), FealtyWords.P("CC_Vassal_Hist_Ask_" + ask + "_" + Math.Min(choice, ask == VassalAsk.Judgement ? 2 : 1), place.Named("TOWN")));
            Diplomacy.Dirty();
        }

        // ================= Promises: a camp broken, an invasion fought =================

        public const string CampKey = "vassal:camp", DefendKey = "vassal:defend";

        /// <summary>The colony's word to break a camp near them or to fight beside them: a Promise tie to their head, for a season (an invasion: five days), its day in the terms.</summary>
        private static void Promise(Map home, int town, Faction people, VassalAsk ask, int target)
        {
            var head = HeadOf(town);
            var to = head != null ? head.Person : Refs.Of(people);
            float days = ask == VassalAsk.Defend ? FealtyMath.LevyDays : FealtyMath.CampPromiseDays;
            // Its day in the terms (not the ledger's until, which would end it quietly): kept, or broken at its day.
            WorldLedger.AddTie(Tie.Kind.Promise, Diplomacy.Colony(home), to,
                new TieTerms { key = ask == VassalAsk.Defend ? DefendKey : CampKey, count = target, about = Ref.Town(town), days = Now / (float)Day + days }, Tie.Never, FealtyMath.VassalOrigin);
        }

        /// <summary>Promises kept or broken: the camp gone; the colony's people at their outpost while the fight is on, or the fight over with it still the colony's.</summary>
        private static void Promises(int now)
        {
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Promise).ToList())
            {
                if (t == null || !t.Live || t.origin != FealtyMath.VassalOrigin) continue;
                try
                {
                    int town = t.terms.about.kind == RefKind.Town ? t.terms.about.id : -1;
                    bool kept = false, over = false;
                    if (t.terms.key == CampKey) kept = !Find.WorldObjects.Sites.Any(s => s != null && s.ID == t.terms.count && !s.Destroyed);
                    else if (t.terms.key == DefendKey)
                    {
                        var o = Outpost(town);
                        if (o == null) over = true;
                        else if (OursThere(o.tile)) kept = true;
                        else if (!Territories.InvasionAtOutpost(o.thing, out _)) over = true;
                    }
                    if (kept)
                    {
                        WorldLedger.Set(t, Tie.State.Kept);
                        Regard(town, t.terms.key == CampKey ? FealtyMath.CampKeptRegard : FealtyMath.DefendRegard, FealtyWords.P(t.terms.key == CampKey ? "CC_Vassal_Note_CampBroken" : "CC_Vassal_Note_Defended"));
                    }
                    else if (over) WorldLedger.Set(t, Tie.State.Ended);
                    else if (t.terms.days > 0f && now >= t.terms.days * Day)
                    {
                        WorldLedger.Set(t, Tie.State.Broken);
                        Regard(town, FealtyMath.CampBrokenRegard, FealtyWords.P(t.terms.key == CampKey ? "CC_Vassal_Note_CampLeft" : "CC_Vassal_Note_NotCome"));
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("vassals: a promise", e);
                }
            }
        }

        /// <summary>Two grown colonists or more at a tile: a caravan of the colony's there, or the fight's map there.</summary>
        private static bool OursThere(PlanetTile tile)
        {
            foreach (var c in Find.WorldObjects.Caravans)
                if (c != null && c.IsPlayerControlled && c.Tile == tile && c.PawnsListForReading.Count(p => p != null && p.IsColonist && !p.DevelopmentalStage.Juvenile()) >= 2)
                    return true;
            foreach (var m in Find.Maps)
                if (m != null && m.Tile == tile && !m.IsPlayerHome && m.mapPawns.FreeColonistsSpawned.Count(p => !p.DevelopmentalStage.Juvenile()) >= 2)
                    return true;
            return false;
        }

        // ================= Tribute level =================

        /// <summary>The council sets how much a vassal brings at harvest; their head hears of it.</summary>
        public static void SetLevel(VassalTerms v, TributeLevel level)
        {
            if (v == null || v.level == level) return;
            bool lighter = level < v.level;
            v.level = level;
            Regard(v.town, lighter ? 3f : -3f, FealtyWords.P(lighter ? "CC_Vassal_Note_Lighter" : "CC_Vassal_Note_Heavier", FealtyWords.Level(level).Named("LEVEL")));
            Diplomacy.Dirty();
        }

        // ================= The quartermaster's dues =================

        /// <summary>The quartermaster's dues task with this people at this home is under way (the liege's tribute seen to, or a vassal's dues).</summary>
        public static bool QuartermasterOn(Map home, Faction f) =>
            f != null && CouncilTasks.Running().Any(x => x.task.kind == TaskKind.Dues && x.task.Live && x.task.people == f && x.home?.Map == home);

        /// <summary>The liege's tribute came on time and in full with the quartermaster seeing to it: their head thinks a little better of the colony.</summary>
        public static void Punctual(Map home, Faction liege)
        {
            var head = FavourCalls.HeadOf(liege, home);
            if (head == null) return;
            head.personal += FealtyMath.PunctualRegard;
            head.Remember(FealtyWords.P("CC_Fealty_Note_Punctual"), FealtyMath.PunctualRegard);
        }

        /// <summary>A people the quartermaster's dues can be aimed at from this home: the liege, or the people of one of its vassals.</summary>
        public static bool DuesTarget(Faction f, Map home) => f != null && (Fealty.Liege(home) == f || Of(home).Any(v => v.people == f));

        /// <summary>The vassal town of that people whose dues are due first, or null.</summary>
        public static Settlement DuesTown(Faction f, Map home) =>
            TownOf(Of(home).Where(v => v.people == f).OrderBy(v => FealtyMath.DuesDue(v.duesAt, Now) ? 0 : 1).FirstOrDefault());

        /// <summary>
        /// Why the quartermaster can't collect dues now (CouncilTasks.DuesWhyNot): with no people given, why none at all (no
        /// liege and no vassal); with one, why not theirs (nothing owed and the tribute not due, collected this season).
        /// </summary>
        public static string DuesWhyNot(Faction f, Map home)
        {
            if (!On) return FealtyWords.P("CC_Fealty_Why_Off");
            if (home == null) return TaskWords.P("CC_Task_Why_NoHome");
            if (f == null) return Fealty.Of(home) == null && Of(home).Count == 0 ? TaskWords.P("CC_Task_Why_NoTarget_Dues") : null;
            if (Fealty.Liege(home) == f) return null;
            var due = Of(home).Where(v => v.people == f).ToList();
            if (due.Count == 0) return FealtyWords.P("CC_Vassal_Why_NotVassal", FealtyWords.People(f).Named("PEOPLE"));
            if (!due.Any(v => FealtyMath.DuesDue(v.duesAt, Now))) return FealtyWords.P("CC_Vassal_Why_DuesPaid");
            if (!due.Any(v => HeadOf(v.town) != null)) return FealtyWords.P("CC_Vassal_Why_NoHead", NameOf(due[0]).Named("TOWN"));
            return null;
        }

        /// <summary>
        /// The dues task's hour (CouncilTasks.DuesStep). With the liege: what is owed goes by cart at once (their porter
        /// fetches it at the gate; with nobody to send, it is carried out), and the season's tribute is made up of more kinds
        /// of goods so it is paid in full; done when nothing is owed and a season's tribute came in full. With a vassal: the
        /// quartermaster writes to their head; answered, their carter brings the season's dues to the gate (on time, or half
        /// and explained in a hard season), and it is done.
        /// </summary>
        public static void DuesStep(CouncilTask t, DiplomacyHome h, int now)
        {
            var home = h?.Map;
            var f = t.people;
            t.nextTick = now + 3 * TaskMath.HourTicks;
            if (home == null || f == null) return;
            var liege = Fealty.Of(home);
            if (liege != null && Fealty.LiegeOf(liege) == f)
            {
                LiegeDues(t, h, home, liege, now);
                return;
            }
            var v = Of(home).FirstOrDefault(x => x.town == t.town) ?? Of(home).FirstOrDefault(x => x.people == f);
            if (v == null)
            {
                CouncilTasks.End(h, t, TaskStage.Off, TaskWords.P("CC_Task_Result_Gone", t.OfficerName.Named("PAWN"), (t.targetName ?? "?").Named("TARGET")), true);
                return;
            }
            if (t.asked)
            {
                Collected(t, h, home, v, now);
                return;
            }
            if (t.letterId >= 0)
            {
                var open = NeighbourLetters.Get(t.letterId);
                if (open != null && open.Open) return;
                t.letterId = -1;
            }
            if (t.letters > 0) return;
            var head = HeadOf(v.town);
            if (head == null || !NeighbourLetters.On)
            {
                t.nextTick = now + 12 * TaskMath.HourTicks;
                return;
            }
            var l = TaskLetters.Send(head, t.officer, TaskLetters.TaskKey(t), TaskWords.Topic(t));
            if (l == null)
            {
                t.nextTick = now + Mathf.RoundToInt(TaskMath.RetryHours * TaskMath.HourTicks);
                return;
            }
            t.letterId = l.id;
            Diplomacy.Dirty();
        }

        /// <summary>Their answer to the quartermaster's letter (TaskResults.LetterRead): their carter will bring the dues, on time or explained.</summary>
        public static string DuesRead(CouncilTask t, NeighbourLetter l, Notable n, ref AnswerTone tone)
        {
            var home = CouncilTasks.MapOf(t);
            var v = Of(home).FirstOrDefault(x => x.town == t.town) ?? Of(home).FirstOrDefault(x => x.people == t.people);
            if (v == null) return null;
            var rec = WorldLedger.TownRecord(v.town);
            float p = rec != null && rec.Seeded ? rec.prosperity : 30f;
            t.asked = true;
            t.progress = FealtyMath.Dues(p, v.level);
            t.lastHow = FealtyMath.Explained(p) ? 1 : 0;
            if (tone > AnswerTone.Cool) tone = AnswerTone.Cool;
            t.nextTick = Now + Mathf.RoundToInt(Rand.Range(6f, 18f) * TaskMath.HourTicks);
            return FealtyWords.T(FealtyMath.Explained(p) ? "CC_Vassal_Dues_Explained" : "CC_Vassal_Dues_OnTime", n.Name.Named("NAME"), ((int)t.progress).Named("SILVER"));
        }

        /// <summary>Their carter brings the dues to the gate: the task done, the record's season noted.</summary>
        private static void Collected(CouncilTask t, DiplomacyHome h, Map home, VassalTerms v, int now)
        {
            if (now < t.nextTick) return;
            int silver = Mathf.Max(10, Mathf.RoundToInt(t.progress));
            var gate = PlaySpots.GateCell(home, null);
            var things = Couriers.Make(ThingDefOf.Silver, silver);
            if (!Couriers.Deliver(home, things, v.people, gate)) Loot.Deliver(things, home, gate, false);
            v.duesAt = now;
            t.carts++;
            t.last = TaskStep.Cart;
            t.lastTick = now;
            FactionMemory.Note(v.people, "tribute", silver.ToString());
            Write(home, v.town, v.people, FealtyWords.P("CC_Vassal_Hist_Dues", NameOf(v).Named("TOWN"), silver.Named("SILVER")));
            CouncilTasks.End(h, t, TaskStage.Done, FealtyWords.P(t.lastHow == 1 ? "CC_Task_Result_Dues_Explained" : "CC_Task_Result_Dues", t.OfficerName.Named("PAWN"),
                NameOf(v).Named("TOWN"), silver.Named("SILVER")), true);
        }

        /// <summary>The liege's tribute seen to: what is owed sent by cart; done once nothing is owed and the season's tribute came in full.</summary>
        private static void LiegeDues(CouncilTask t, DiplomacyHome h, Map home, Tie liege, int now)
        {
            var f = Fealty.LiegeOf(liege);
            if (Fealty.Owed(liege))
            {
                var (def, count) = OwedCart(home, liege.terms.count);
                if (def == null) return;
                var trip = CouncilTasks.SendCart(home, def, count, f);
                if (trip != null)
                {
                    // The debt is the cart's from now, as when what is owed is carried out (Fealty.PayOwed): the courier
                    // trip is let go once their porter has left, so its tally can't be read back afterwards, and a debt
                    // still standing would send the cart again and again (and leave "Send what is owed" open beside it).
                    Fealty.Paid(liege, Mathf.RoundToInt(count * def.BaseMarketValue));
                    t.carts++;
                    t.last = TaskStep.Cart;
                    t.lastTick = now;
                    // apart: the council's business, told in a message; their porter comes to the gate for the cart.
                    Tell.Message(NewsKind.World, Importance.Notable, home, FealtyWords.P("CC_Fealty_Msg_Cart", t.officer.Named("PAWN"), Couriers.Amount(count, null, def).Named("GOODS"),
                        FealtyWords.People(f).Named("PEOPLE")), new LookTargets(t.officer), MessageTypeDefOf.NeutralEvent);
                }
                else if (Fealty.PayWhyNot(liege) == null) Fealty.PayOwed(liege, t.officer);
                Diplomacy.Dirty();
                return;
            }
            // Nothing owed: a season's tribute paid in full since the task was set (its next day moved on a season past it).
            float due = liege.terms.number;
            if (due * Day - FealtyMath.SeasonDays * Day > t.started)
                CouncilTasks.End(h, t, TaskStage.Done, FealtyWords.P("CC_Task_Result_Tribute", t.OfficerName.Named("PAWN"), FealtyWords.People(f).Named("PEOPLE")), true);
        }

        /// <summary>A cart for what is owed: silver when the colony holds it, else its most plentiful good, worth the debt.</summary>
        private static (ThingDef def, int count) OwedCart(Map home, int owed)
        {
            if (PlayCarry.Have(home, "silver", ThingDefOf.Silver) >= owed) return (ThingDefOf.Silver, owed);
            foreach (var (def, count) in Pacts.CartGoods(home))
            {
                int n = Mathf.CeilToInt(owed / Mathf.Max(0.1f, def.BaseMarketValue));
                if (PlayCarry.Have(home, def.defName, def) >= n) return (def, n);
            }
            return (null, 0);
        }

        // ================= Levies =================

        /// <summary>Why the council can't call this vassal's levies now, or null.</summary>
        public static string LeviesWhyNot(VassalTerms v, Map home)
        {
            if (!On) return FealtyWords.P("CC_Fealty_Why_Off");
            if (v == null || TownOf(v) == null) return FealtyWords.P("CC_Fealty_Why_Gone");
            // Their people turned on the colony (the day's look ends the protection): no band of theirs is called in.
            if (v.people == null || Diplomacy.Hostile(v.people)) return DipWords.P("CC_Dip_Why_War", FealtyWords.People(v.people).Named("PEOPLE"));
            if (home == null || !MapFacts.ThreatOn(home)) return PactWords.P("CC_Pact_Why_NoThreat");
            if (v.leviesDue >= 0) return FealtyWords.P("CC_Vassal_Why_LeviesComing", NameOf(v).Named("TOWN"));
            if (!FealtyMath.MayCallLevies(v.leviesAt, Now)) return FealtyWords.P("CC_Vassal_Why_LeviesSeason", NameOf(v).Named("TOWN"));
            if (CampTracker.WarBandAt(home) != null) return PactWords.P("CC_Pact_Why_BandHere");
            if (!CallsToArms.Fields(v.people)) return FealtyWords.P("CC_Vassal_Why_NoFighters", NameOf(v).Named("TOWN"));
            return null;
        }

        /// <summary>The council calls their levies: their band walks in some hours later (a third of the threat).</summary>
        public static bool CallLevies(VassalTerms v, Map home)
        {
            if (LeviesWhyNot(v, home) != null) return false;
            int now = Now;
            var t = TownOf(v);
            float tiles = t != null ? WorldSituationWorker.Distance(home, t) : 6f;
            v.leviesAt = now;
            v.leviesDue = now + Mathf.RoundToInt(PactMath.BandHours(tiles) * TaskMath.HourTicks);
            var form = FealtyWords.LevyForm(home, v.people);
            // apart: the council's call, sent by the colony's means; their band walks in later, on the map.
            Tell.Message(NewsKind.World, Importance.Important, home, FealtyWords.T("CC_Vassal_Msg_Called", NameOf(v).Named("TOWN"), form.rite.Named("RITE"), DipWords.Means(home).Named("MEANS")),
                LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            Diplomacy.Dirty();
            return true;
        }

        /// <summary>The hour: levies due walk in (or turn back with the fight over).</summary>
        public static void Hourly(int now)
        {
            foreach (var (home, v) in All())
            {
                if (v.leviesDue < 0 || now < v.leviesDue) continue;
                try
                {
                    // Their people at war with the colony since the call: their band would walk in as enemies; it doesn't come.
                    if (v.people == null || v.people.defeated || Diplomacy.Hostile(v.people))
                    {
                        v.leviesDue = -1;
                        Diplomacy.Dirty();
                        continue;
                    }
                    if (Pacts.Arrive(v.people, home, WarCamps.Attacker(home), "vassal", out bool wait, "vassal", FealtyMath.LevyStrength) || !wait)
                    {
                        v.leviesDue = -1;
                        Diplomacy.Dirty();
                    }
                    else v.leviesDue = now + 2 * TaskMath.HourTicks;
                }
                catch (Exception e)
                {
                    v.leviesDue = -1;
                    CouncilLog.ErrorOnce("vassals: their levies", e);
                }
            }
        }

        /// <summary>Their band went home (Camps/WarCamp.cs): their dead weigh on their head's regard.</summary>
        public static void LeviesHome(WarBand b)
        {
            if (b?.faction == null || b.map == null) return;
            var v = Of(b.map).Where(x => x.people == b.faction && x.leviesAt >= 0).OrderByDescending(x => x.leviesAt).FirstOrDefault();
            if (v == null) return;
            int fell = b.Fell;
            if (fell <= 0)
            {
                Regard(v.town, 2f, FealtyWords.P("CC_Vassal_Note_LeviesHome"));
                return;
            }
            v.fell += fell;
            Regard(v.town, FealtyMath.FallenRegard(fell), FealtyWords.P("CC_Vassal_Note_LeviesFell", fell.Named("COUNT")));
            Write(b.map, v.town, v.people, FealtyWords.P("CC_Vassal_Hist_Fell", NameOf(v).Named("TOWN"), fell.Named("COUNT")), true);
        }

        // ================= Release =================

        /// <summary>Released: the protection ends without blame, or the hamlet's oath is given back (it stays under the colony's protection).</summary>
        public static void Release(Map home, int town)
        {
            var v = At(town, out var m);
            home ??= m;
            if (v == null || home == null) return;
            var h = DiplomacyBook.Instance?.HomeOf(home);
            if (v.kind == VassalKind.Hamlet) Hamlets.ReleaseOath(home, Hamlets.Peek(home));
            else
            {
                var tie = TieOf(v, home);
                if (tie != null && tie.Live) WorldLedger.Set(tie, Tie.State.Ended);
            }
            h?.vassals.Remove(v);
            Regard(town, 10f, FealtyWords.P("CC_Vassal_Note_Released"));
            Write(home, town, v.people, FealtyWords.P("CC_Vassal_Hist_Released", NameOf(v).Named("TOWN")), true);
            Tell.Letter(NewsKind.World, Importance.Notable, home, FealtyWords.P("CC_Vassal_Released_Label", NameOf(v).Named("TOWN")),
                FealtyWords.T(v.kind == VassalKind.Hamlet ? "CC_Vassal_Released_Hamlet" : "CC_Vassal_Released", NameOf(v).Named("TOWN"), FealtyWords.People(v.people).Named("PEOPLE")),
                LetterDefOf.NeutralEvent, TownOf(v) is Settlement s ? new LookTargets(s) : LookTargets.Invalid, v.people);
            Diplomacy.Dirty();
        }

        /// <summary>Release refused: they break away. The tie is broken, and their people's goodwill drops by 15.</summary>
        public static void BreakAway(Map home, int town)
        {
            var v = At(town, out var m);
            home ??= m;
            if (v == null || home == null) return;
            var h = DiplomacyBook.Instance?.HomeOf(home);
            if (v.kind == VassalKind.Hamlet) Hamlets.BreakAway(home, Hamlets.Peek(home));
            else
            {
                var tie = TieOf(v, home);
                if (tie != null && tie.Live) WorldLedger.Set(tie, Tie.State.Broken);
            }
            h?.vassals.Remove(v);
            Goodwill(v.people, -FealtyMath.BreakAwayGoodwill);
            Write(home, town, v.people, FealtyWords.P("CC_Vassal_Hist_BrokeAway", NameOf(v).Named("TOWN")), true);
            Tell.Letter(NewsKind.World, Importance.Important, home, FealtyWords.P("CC_Vassal_BrokeAway_Label", NameOf(v).Named("TOWN")),
                FealtyWords.T("CC_Vassal_BrokeAway", NameOf(v).Named("TOWN"), FealtyWords.People(v.people).Named("PEOPLE"), FealtyMath.BreakAwayGoodwill.Named("GOODWILL")),
                LetterDefOf.NegativeEvent, TownOf(v) is Settlement s ? new LookTargets(s) : LookTargets.Invalid, v.people);
            Diplomacy.Dirty();
        }

        /// <summary>The colony attacked a people whose town is under its protection (PactsHooks): that protection is broken.</summary>
        public static void BrokenByUs(Faction f, string how)
        {
            if (f == null) return;
            foreach (var (home, v) in All().ToList())
            {
                if (v.people != f || v.kind != VassalKind.Protected) continue;
                var tie = TieOf(v, home);
                if (tie != null && tie.Live) WorldLedger.Set(tie, Tie.State.Broken);
                DiplomacyBook.Instance?.HomeOf(home)?.vassals.Remove(v);
                Tell.Letter(NewsKind.World, Importance.Important, home, FealtyWords.P("CC_Vassal_BrokeAway_Label", NameOf(v).Named("TOWN")),
                    FealtyWords.T("CC_Vassal_BrokenByUs", NameOf(v).Named("TOWN"), FealtyWords.People(f).Named("PEOPLE"), how.Named("HOW")), LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
                Diplomacy.Dirty();
            }
        }

        // ================= Helpers =================

        /// <summary>Their head's regard moves, with a line saying why (the notables' own memory, shown on their card).</summary>
        public static void Regard(int town, float shift, string why)
        {
            var n = HeadOf(town);
            if (n == null || shift == 0f) return;
            n.personal += shift;
            n.Remember(why, shift);
        }

        private static void Goodwill(Faction f, int amount)
        {
            if (f == null || amount == 0 || f.IsPlayer) return;
            try
            {
                f.TryAffectGoodwillWith(Faction.OfPlayer, amount, false, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("vassals: goodwill", e);
            }
        }

        /// <summary>Into the history (the family's dip:tribute for what a vassal brings and how the colony keeps it): the colony with their people, at their town.</summary>
        private static void Write(Map home, int town, Faction people, string line, bool milestone = false)
        {
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(people), HistoryRole.Did), new HistoryActor(Diplomacy.Colony(home), HistoryRole.For) };
            Diplomacy.Write(DiplomacyMath.Tribute, actors, Ref.Town(town), line, milestone);
        }
    }

    /// <summary>A vassal's head asks something of the colony (or a struggling town asks for its protection), as a letter with its choices.</summary>
    public class ChoiceLetter_VassalAsk : ChoiceLetter
    {
        public int home = HomesMath.None;
        public int town = -1;
        public Faction people;
        public int ask;
        /// <summary>The camp (a site's id) or the neighbouring town (its id) the ask is about, or -1.</summary>
        public int other = -1;
        public bool sickness;

        private VassalAsk Kind => (VassalAsk)ask;

        private bool Valid => Vassals.AskValid(town, people, Kind);

        /// <summary>Still waiting for the colony's answer (the inbox's row).</summary>
        public bool Waiting => !ArchivedOnly && Valid;

        public string TownName => NotableBook.TownOf(town)?.Name ?? Vassals.Outpost(town)?.name ?? "?";

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || !Valid)
                {
                    yield return Option_Close;
                    yield break;
                }
                int n = Kind == VassalAsk.Judgement ? 3 : 2;
                for (int i = 0; i < n; i++)
                {
                    int choice = i;
                    var opt = new DiaOption(FealtyWords.P("CC_Vassal_Opt_" + Kind + "_" + i, TownName.Named("TOWN")))
                    {
                        action = () =>
                        {
                            Find.LetterStack.RemoveLetter(this);
                            if (!Valid) return;
                            Vassals.Answer(Homes.MapOf(home) ?? Homes.Seat, town, people, Kind, choice, other, sickness);
                        },
                        resolveTree = true
                    };
                    if (Kind == VassalAsk.Help && i == 0)
                    {
                        var map = Homes.MapOf(home) ?? Homes.Seat;
                        string what = FealtyMath.HelpNeed(sickness);
                        int count = FealtyMath.HelpCount(sickness);
                        if (PlayCarry.Have(map, what, null) < count) opt.Disable("CC_Dilemma_NotEnough".Translate(count, PlayCarry.Label(what, null)).ToString());
                    }
                    yield return opt;
                }
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref ask, "ask");
            Scribe_Values.Look(ref other, "other", -1);
            Scribe_Values.Look(ref sickness, "sickness");
        }
    }
}
