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
    /// The new towns' faces (docs/design/stage1-region.md, item 1): a town begun or built within the region map's reach of
    /// a home gets a founder (a person of their people, their leaders' kind, aged 30 to 55; for the Empire one of its
    /// titled kinds), two founding families, the town its settlers came from and a reason read from the world. When it
    /// stands, the founder is its head notable and the town is kept beside the nearest four for its first year. Told
    /// once when begun and once when built in the colony news (in place of Faction Territories' own message within
    /// reach), and by the founder's first letter to a home within about 15 tiles (one a season per home).
    /// <para>
    /// Sources: Faction Territories' settlements begun and finished (through WorldMechanics' hooks), and Rimfolk's own
    /// new towns (the new-town, other-settlers and crossroads events, whose own letters tell them). Without another mod
    /// building towns, the new towns come only from what the game and Rimfolk already do.
    /// </para>
    /// <para>
    /// The full welcome (a runner at the gate, a gift hoped for) waits for the player's word (decision 1): its place is
    /// <see cref="Welcome"/>, which today always sends the letter.
    /// </para>
    /// </summary>
    public static class RegionFaces
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>
        /// How many towns begun or built the region has taken to tell of (counted this session): another mod's own message
        /// about the same town is dropped when it moved (Integrations/TerritoryNews.cs), so the player gets one, not two.
        /// </summary>
        public static int Claims { get; private set; }

        /// <summary>Sites a founder was tried for and couldn't be made (their people have no one to send): not tried again this session.</summary>
        private static readonly HashSet<int> tried = new HashSet<int>();
        private static Game triedGame;

        private static HashSet<int> Tried
        {
            get
            {
                if (triedGame != Current.Game)
                {
                    triedGame = Current.Game;
                    tried.Clear();
                }
                return tried;
            }
        }

        // ---------------- Hooks: a town begun, a town built, one of Rimfolk's own ----------------

        /// <summary>A people began building a town (Faction Territories, through WorldMechanics.Founded): within reach, a founding to give faces to.</summary>
        public static void Begun(Faction people, PlanetTile tile, int site, int started, int completes)
        {
            if (!RegionBook.FacesOn || people == null || site < 0 || !tile.Valid || !RegionTowns.InReach(tile)) return;
            if (WorldLedger.FoundingAt(site) != null) return;
            try
            {
                var f = new LedgerFounding { site = site, tile = Refs.Of(tile), people = people, started = started >= 0 ? started : Now, completes = completes };
                Origin(f, people, tile, null);
                if (WorldLedger.AddFounding(f) != null) Claims++;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: a town begun", e);
            }
        }

        /// <summary>
        /// A town being built was finished (Faction Territories, through WorldMechanics.Built): within reach, it gets its
        /// faces in the region's hour. A town begun out of reach, or before, gets its founding now.
        /// </summary>
        public static void Built(Faction people, Settlement town, int site)
        {
            if (!RegionBook.FacesOn || people == null || town == null || !RegionTowns.InReach(town.Tile)) return;
            try
            {
                var f = (site >= 0 ? WorldLedger.FoundingAt(site) : null) ?? FoundingOnTile(town.Tile);
                if (f == null)
                {
                    f = new LedgerFounding { site = site >= 0 ? site : town.ID, tile = Refs.Of(town.Tile), people = people, started = Now, completes = Now };
                    Origin(f, people, town.Tile, town);
                    f = WorldLedger.AddFounding(f);
                }
                if (f == null) return;
                f.built = town.ID;
                Claims++;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: a town built", e);
            }
        }

        /// <summary>
        /// One of Rimfolk's own new towns stands (the new-town, other-settlers and crossroads events): its faces come in the
        /// region's hour; the event's own letter tells it. <paramref name="founder"/>: who leads them, when the event has
        /// someone (the other settlers' leader), else one is made. <paramref name="why"/>: a reason the event gives (the
        /// crossroads' road), else read from the world.
        /// </summary>
        public static void Founded(Settlement town, Pawn founder, string why = null)
        {
            if (!RegionBook.FacesOn || town == null || town.Faction == null || !RegionTowns.InReach(town.Tile)) return;
            if (WorldLedger.FoundingAt(town.ID) != null || WorldLedger.TownFor(town.ID, null, null, false)?.founded >= 0) return;
            try
            {
                var f = new LedgerFounding
                {
                    site = town.ID, tile = Refs.Of(town.Tile), people = town.Faction, started = Now, completes = Now, built = town.ID, told = true, byEvent = true
                };
                Origin(f, town.Faction, town.Tile, town);
                if (!why.NullOrEmpty()) f.why = why;
                if (founder != null && !founder.Dead && !founder.Discarded)
                {
                    f.founder = WorldLedger.PersonOf(founder);
                    WorldLedger.Keep(founder, KeepFor.Founding, false);
                }
                WorldLedger.AddFounding(f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: one of Rimfolk's own towns", e);
            }
        }

        /// <summary>The town the settlers came from (their people's nearest other town) and why they chose the place.</summary>
        private static void Origin(LedgerFounding f, Faction people, PlanetTile tile, Settlement town)
        {
            var mother = MotherTown(people, tile, town);
            f.mother = Refs.Of(mother);
            f.motherName = mother?.HasName == true ? mother.Name : null;
            f.why = RegionMath.WhyFounded(Reasons(people, tile, mother), Rand.Value);
        }

        /// <summary>Their people's nearest other standing town on the same layer of the world, or null.</summary>
        public static Settlement MotherTown(Faction people, PlanetTile tile, Settlement except)
        {
            if (people == null || !tile.Valid) return null;
            Settlement best = null;
            float bestD = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s == null || s == except || s.Destroyed || s.Faction != people || s.Tile == tile || s.Tile.Layer != tile.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(s.Tile, tile);
                if (d >= bestD) continue;
                best = s;
                bestD = d;
            }
            return best;
        }

        /// <summary>What the world says of the place (RegionMath.Reasons): a town of theirs given up lately, a ruin by it, a flourishing mother town, a road, a river, the coast, the hills.</summary>
        private static HashSet<string> Reasons(Faction people, PlanetTile tile, Settlement mother)
        {
            var holds = new HashSet<string>();
            int now = Now;
            // A town of theirs given up in the last 120 days: its people are the settlers.
            var ws = WorldSituations.Instance;
            if (ws != null && ws.history.Any(r => r != null && r.faction == people && r.defName == "Council_WS_Abandoned" && r.ending != "holdouts" && r.ending != WorldSituations.Quietly
                                                  && now - r.endTick < RegionMath.GivenUpDays * GenDate.TicksPerDay))
                holds.Add("refugees");
            var l = WorldLedger.Instance;
            if (l != null && l.Towns.Any(t => t != null && t.people == people && t.gone >= 0 && now - t.gone < RegionMath.GivenUpDays * GenDate.TicksPerDay)) holds.Add("refugees");
            // The old walls of a ruin on the place or beside it.
            foreach (var o in Find.WorldObjects.AllWorldObjects)
                if (o is AbandonedSettlement && !o.Destroyed && o.Tile.Layer == tile.Layer && Find.WorldGrid.ApproxDistanceInTiles(o.Tile, tile) <= 1.5f)
                {
                    holds.Add("ruin");
                    break;
                }
            var rec = mother != null ? WorldLedger.TownRecord(mother.ID) : null;
            if (rec != null && rec.Seeded && RegionMath.Band(rec.prosperity) == Fortune.Flourishing) holds.Add("mother");
            try
            {
                if (Find.WorldGrid[tile] is SurfaceTile st)
                {
                    if (!st.Roads.NullOrEmpty()) holds.Add("road");
                    if (!st.Rivers.NullOrEmpty()) holds.Add("river");
                }
                var t = Find.WorldGrid[tile];
                if (t != null && (t.hilliness == Hilliness.LargeHills || t.hilliness == Hilliness.Mountainous)) holds.Add("hills");
                if (Find.World.CoastDirectionAt(tile).IsValid) holds.Add("coast");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: the land of a new town", e);
            }
            return holds;
        }

        private static LedgerFounding FoundingOnTile(PlanetTile tile)
        {
            var l = WorldLedger.Instance;
            if (l == null) return null;
            var r = Refs.Of(tile);
            foreach (var f in l.Foundings)
                if (f != null && f.tile == r)
                    return f;
            return null;
        }

        // ---------------- The hour ----------------

        /// <summary>
        /// The region's hour: each founding gets its founder (two made a day at most; a day's wait at most, then it goes
        /// on without), its line in the news when begun, and its faces when the town stands.
        /// </summary>
        internal static void Hourly(RegionBook book, int now)
        {
            var l = WorldLedger.Instance;
            if (l == null || l.Foundings.Count == 0) return;
            foreach (var f in l.Foundings.ToList())
            {
                if (f == null) continue;
                try
                {
                    if (f.founder.IsNone && !Tried.Contains(f.site) && book.MayMakeFounder(true))
                    {
                        var p = MakeFounder(f.people);
                        if (p != null) f.founder = WorldLedger.PersonOf(p);
                        else Tried.Add(f.site);
                    }
                    bool ready = !f.founder.IsNone || Tried.Contains(f.site) || now - f.started >= GenDate.TicksPerDay;
                    if (!ready) continue;
                    if (!f.told && f.built < 0)
                    {
                        f.told = true;
                        TellBegun(f);
                    }
                    if (f.built >= 0) Stand(book, f, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region: a founding's hour", e);
                }
            }
        }

        /// <summary>
        /// The day's look: a founding whose site is gone with no town standing in its place (razed, abandoned) or that
        /// has fallen out of every home's reach is let go, and its founder with it.
        /// </summary>
        internal static void Daily(int now)
        {
            var l = WorldLedger.Instance;
            if (l == null || l.Foundings.Count == 0) return;
            var building = WorldMechanics.SettlementsBeingBuilt();
            foreach (var f in l.Foundings.ToList())
            {
                if (f == null || f.built >= 0) continue;
                bool stands = building.Any(b => b != null && b.id == f.site);
                var tile = TileOf(f.tile);
                if (stands && RegionTowns.InReach(tile)) continue;
                // Finished meanwhile without the hook seeing it (the setting was off): the town on its tile.
                var town = tile.Valid ? Find.WorldObjects.SettlementAt(tile) : null;
                if (town != null && town.Faction == f.people && RegionBook.FacesOn && RegionTowns.InReach(tile))
                {
                    f.built = town.ID;
                    continue;
                }
                WorldLedger.RemoveFounding(f);
                FoundingGone(f);
            }
        }

        private static PlanetTile TileOf(Ref r)
        {
            if (r.kind != RefKind.Tile || Find.WorldGrid == null) return PlanetTile.Invalid;
            try
            {
                return Find.WorldGrid.PlanetLayers.ContainsKey(r.TileLayer) ? new PlanetTile(r.TileId, r.TileLayer) : PlanetTile.Invalid;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: a founding's tile", e);
                return PlanetTile.Invalid;
            }
        }

        // ---------------- The town stands ----------------

        /// <summary>
        /// A town stands: its record (founded now, its founder, mother town and reason; prosperity 30 with the fortunes
        /// on), its two founding families, its line in the news (not for Rimfolk's own towns, whose events told it), the
        /// history's entry for Rimfolk's own, and its first word to a home near it.
        /// </summary>
        private static void Stand(RegionBook book, LedgerFounding f, int now)
        {
            WorldLedger.RemoveFounding(f);
            var town = NotableBook.TownOf(f.built);
            if (town == null || town.Destroyed || town.Faction == null)
            {
                FoundingGone(f);
                return;
            }
            var rec = WorldLedger.TownFor(town.ID, town.HasName ? town.Name : null, town.Faction, true);
            if (rec == null) return;
            if (town.HasName) rec.name = town.Name;
            rec.people = town.Faction;
            rec.kind = TownKind.Town;
            rec.founded = now;
            rec.founder = f.founder;
            rec.mother = f.mother;
            rec.motherName = f.motherName;
            rec.why = f.why;
            if (RegionBook.FortunesOn)
            {
                rec.prosperity = RegionMath.NewTownStart;
                rec.movedAt = now;
                rec.cause = "CC_Region_Cause_Founded".Translate().Resolve();
                rec.causeDir = 0;
                rec.causeAt = now;
                rec.lowSince = -1;
            }
            var founder = WorldLedger.PawnOf(f.founder);
            Families(town, founder);
            WorldLedger.TownChanged();
            NotableBook.Changed();
            var home = RegionTowns.NearestHome(town.Tile, out float tiles);
            if (f.byEvent)
            {
                // Rimfolk's own: its event's letter told it; the history has the town's founding with its founder.
                var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(town.Faction), HistoryRole.Did), new HistoryActor(Refs.Of(town.Tile), HistoryRole.About) };
                if (!f.founder.IsNone) actors.Insert(1, new HistoryActor(f.founder, HistoryRole.Did));
                History.Add(WorldMath.Built, actors, Refs.Of(town), WorldMath.Weight(false), values: town.HasName ? new List<string> { town.Name } : null);
            }
            else TellBuilt(rec, town, founder, home, tiles);
            if (home != null) Welcome(book, rec, town, founder, home, tiles);
        }

        /// <summary>
        /// The new town's first word to the home near it: the founder's letter, within RegionMath.LetterTiles and at most
        /// one a season per home. Decision 1 (the full welcome: a runner at the gate, a gift hoped for, at most once a year)
        /// would be chosen here when a world-event slot is free; until the player decides, it is always the letter.
        /// </summary>
        private static void Welcome(RegionBook book, LedgerTown rec, Settlement town, Pawn founder, Map home, float tiles)
        {
            var mine = book.HomeOf(home);
            if (mine == null || founder == null || founder.Dead || !RegionMath.LetterDue(tiles, mine.letterAt, Now)) return;
            if (!(ColonyCouncilMod.Settings?.neighbourLetters ?? true)) return;
            mine.letterAt = Now;
            RegionWords.FoundersLetter(rec, town, founder, home);
        }

        // ---------------- Founders ----------------

        /// <summary>
        /// A founder of their people: of their leaders' kind (for a people of royal titles, one of its titled kinds, never a
        /// second ruler), aged 30 to 55, kept in the world for the founding. Null when they have no one to send.
        /// </summary>
        public static Pawn MakeFounder(Faction f)
        {
            if (f == null || f.def == null || !f.def.humanlikeFaction) return null;
            try
            {
                PawnKindDef kind = f.def.HasRoyalTitles ? TitledKind(f) : f.def.fixedLeaderKinds?.Where(k => k?.RaceProps?.Humanlike == true).RandomElementWithFallback();
                kind ??= Arrivals.PersonKind(f);
                if (kind == null) return null;
                var req = new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    colonistRelationChanceFactor: 0f, biologicalAgeRange: new FloatRange(RegionMath.FounderMinAge, RegionMath.FounderMaxAge),
                    developmentalStages: DevelopmentalStage.Adult, dontGiveWeapon: true);
                var p = PawnGenerator.GeneratePawn(req);
                if (p == null) return null;
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                WorldLedger.Keep(p, KeepFor.Founding, false);
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: a founder made for " + f.def?.defName, e);
                return null;
            }
        }

        /// <summary>One of a titled people's kinds that comes with a title, from the lower half of their ranks (a founder, not a ruler).</summary>
        private static PawnKindDef TitledKind(Faction f)
        {
            var kinds = new List<(PawnKindDef kind, int rank)>();
            foreach (var g in f.def.pawnGroupMakers ?? new List<PawnGroupMaker>())
                foreach (var o in (g.options ?? new List<PawnGenOption>()).Concat(g.traders ?? new List<PawnGenOption>()))
                {
                    var k = o?.kind;
                    if (k?.RaceProps?.Humanlike != true || kinds.Any(x => x.kind == k)) continue;
                    var title = k.titleRequired ?? k.minTitleRequired ?? k.titleSelectOne?.FirstOrDefault();
                    if (title != null) kinds.Add((k, title.seniority));
                }
            if (kinds.Count == 0) return null;
            kinds.Sort((a, b) => a.rank.CompareTo(b.rank));
            return kinds.Take(Mathf.Max(1, (kinds.Count + 1) / 2)).RandomElement().kind;
        }

        /// <summary>
        /// What a town's head is called by their people's age ("headman", "reeve", "mayor", "administrator"), and for the
        /// Empire the title the game shows for them.
        /// </summary>
        public static string TitleOf(Pawn p, Faction f)
        {
            if (p != null && Ages.IsEmpire(f))
            {
                var t = WorldMechanics.Title(p);
                if (t != null && !t.label.NullOrEmpty()) return t.label;
            }
            string era = Ages.Of(f) == WorldAge.Tribal ? "Tribal" : Ages.Of(f) == WorldAge.Medieval ? "Medieval" : Ages.Of(f) == WorldAge.Industrial ? "Industrial" : "Spacer";
            string key = "CC_Notable_Title_Head_" + era;
            if (p?.gender == Gender.Female && (key + "_F").CanTranslate()) key += "_F";
            return key.Translate().Resolve();
        }

        /// <summary>
        /// Lets a founder go once nothing names them any more (the founding over, their town no longer kept): back to
        /// nothing when Rimfolk made them and nobody else holds them, else back to the world's own keeping.
        /// </summary>
        public static void LetFounderGo(Ref person)
        {
            var p = WorldLedger.PawnOf(person) ?? Refs.Pawn(person);
            if (p == null || p.Discarded) return;
            bool free = WorldLedger.Release(p, KeepFor.Founding);
            // One who joined the colony since (the other settlers absorbed) is the colony's own now.
            if (!free || p.Spawned || PawnUtility.IsFactionLeader(p) || p.Faction != null && p.Faction.IsPlayer) return;
            bool ties = p.relations != null && p.relations.DirectRelations.Any(r => r.otherPawn != null && r.otherPawn.Faction == Faction.OfPlayer);
            if (!p.Dead && WorldSituationWorker.FreeInWorld(p) && !ties) Arrivals.Discard(p);
            else Arrivals.Release(p);
        }

        /// <summary>A founding over with no town (razed, abandoned, out of reach): its founder let go when nothing else names them.</summary>
        public static void FoundingGone(LedgerFounding f)
        {
            if (f == null || f.founder.IsNone || WorldLedger.FounderNamed(f.founder)) return;
            try
            {
                LetFounderGo(f.founder);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: a founding gone", e);
            }
        }

        /// <summary>The founder of the founding at a site, for the history's entry of the town built there; none when not known.</summary>
        public static Ref FounderAt(int site, PlanetTile tile)
        {
            var f = (site >= 0 ? WorldLedger.FoundingAt(site) : null) ?? (tile.Valid ? FoundingOnTile(tile) : null);
            return f?.founder ?? Ref.None;
        }

        // ---------------- Families ----------------

        /// <summary>The founder's line and one more founding family, each with a few members known by name.</summary>
        private static void Families(Settlement town, Pawn founder)
        {
            var people = town.Faction;
            // A people who go by one name have kin named after their eldest, not families with surnames.
            bool surnames = founder == null || founder.Name is NameTriple tri0 && !tri0.Last.NullOrEmpty();
            if (founder != null)
            {
                string line = surnames ? ((NameTriple)founder.Name).Last : founder.LabelShort;
                WorldLedger.AddHouse(town.ID, people, line, WorldLedger.PersonOf(founder), Members(people, line, surnames, Rand.RangeInclusive(2, 4)), true, !surnames);
            }
            string other = surnames ? Surname() : SingleName(people);
            if (!other.NullOrEmpty()) WorldLedger.AddHouse(town.ID, people, other, Ref.None, Members(people, other, surnames, Rand.RangeInclusive(2, 4)), true, !surnames);
        }

        private static Gender AnyGender => Rand.Bool ? Gender.Male : Gender.Female;

        /// <summary>A family name from the game's own bank of names.</summary>
        private static string Surname()
        {
            try
            {
                return PawnNameDatabaseShuffled.BankOf(PawnNameCategory.HumanStandard).GetName(PawnNameSlot.Last, AnyGender, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: a family's name", e);
                return null;
            }
        }

        /// <summary>A first name from the game's own bank of names.</summary>
        private static string FirstName()
        {
            try
            {
                return PawnNameDatabaseShuffled.BankOf(PawnNameCategory.HumanStandard).GetName(PawnNameSlot.First, AnyGender, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: a family member's name", e);
                return null;
            }
        }

        /// <summary>A name in their people's own way (a single name, for peoples who go by one), from their culture's namer.</summary>
        private static string SingleName(Faction people)
        {
            try
            {
                var maker = people?.ideos?.PrimaryCulture?.GetPawnNameMaker(AnyGender);
                if (maker == null) return FirstName();
                string name = NameGenerator.GenerateName(maker, (IEnumerable<string>)null, false, "r_name");
                if (name.NullOrEmpty()) return null;
                // A namer that gives first 'nick' last gives the nickname, as the game shows such a name.
                var parts = name.Split('\'');
                string shown = parts.Length >= 3 ? parts[1] : name;
                return shown.Trim().CapitalizeFirst();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: a name in their way", e);
                return null;
            }
        }

        /// <summary>A few members of a house, by their first names (or single names), none twice.</summary>
        private static List<string> Members(Faction people, string house, bool surnames, int count)
        {
            var list = new List<string>();
            for (int i = 0; i < count * 2 && list.Count < count; i++)
            {
                string n = surnames ? FirstName() : SingleName(people);
                if (n.NullOrEmpty()) break;
                if (n != house && !list.Contains(n)) list.Add(n);
            }
            return list;
        }

        /// <summary>The town's second founding family's name, while it has no head yet (the town's envoy is born to it), else null.</summary>
        public static string SecondFamily(int town)
        {
            foreach (var h in WorldLedger.HousesOf(town))
                if (h.founding && h.head.IsNone && !h.kin && !h.name.NullOrEmpty())
                    return h.name;
            return null;
        }

        /// <summary>Someone of the town born to one of its families (the envoy of the second): its head from now on.</summary>
        public static void Joined(int town, Pawn p)
        {
            if (p == null || !(p.Name is NameTriple tri) || tri.Last.NullOrEmpty()) return;
            foreach (var h in WorldLedger.HousesOf(town))
                if (h.head.IsNone && h.name == tri.Last)
                {
                    h.head = WorldLedger.PersonOf(p);
                    WorldLedger.TownChanged();
                    return;
                }
        }

        // ---------------- Asking ----------------

        /// <summary>A town of the ledger founded in its first year (60 days), with its record; null otherwise.</summary>
        public static LedgerTown NewTown(int town)
        {
            if (!RegionBook.FacesOn) return null;
            var rec = WorldLedger.TownRecord(town);
            return rec != null && rec.gone < 0 && RegionMath.IsNew(rec.founded, Now) ? rec : null;
        }

        /// <summary>A town's founder as a pawn while the game keeps one, or null.</summary>
        public static Pawn FounderOf(LedgerTown rec) => rec == null || rec.founder.IsNone ? null : WorldLedger.PawnOf(rec.founder) ?? Refs.Pawn(rec.founder);

        /// <summary>The towns founded within reach in their first year, newest first.</summary>
        public static List<LedgerTown> NewTowns()
        {
            var list = new List<LedgerTown>();
            var l = WorldLedger.Instance;
            if (l == null || !RegionBook.FacesOn) return list;
            int now = Now;
            foreach (var t in l.Towns)
                if (t != null && t.gone < 0 && RegionMath.IsNew(t.founded, now))
                    list.Add(t);
            list.Sort((a, b) => b.founded.CompareTo(a.founded));
            return list;
        }

        // ---------------- Telling ----------------

        /// <summary>"Word from a traveller: settlers of the Lenlia under Tamsin Coor, from Ashford, have begun a town two days north."</summary>
        private static void TellBegun(LedgerFounding f)
        {
            var tile = TileOf(f.tile);
            var home = RegionTowns.NearestHome(tile, out float tiles);
            if (home == null || f.people == null) return;
            var founder = WorldLedger.PawnOf(f.founder);
            string text = RegionWords.Begun(home, f.people, founder, f.motherName, RegionWords.Where(home, tile, tiles));
            var site = Find.WorldObjects.AllWorldObjects.FirstOrDefault(o => o != null && o.ID == f.site);
            Tell.Minor(NewsKind.World, Importance.Notable, home, text, site != null ? new LookTargets(site) : new LookTargets(tile), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>"Word from a traveller: Brightwater of the Lenlia stands two days north, its new hearth lit before the elders, with Tamsin Coor as its headwoman."</summary>
        private static void TellBuilt(LedgerTown rec, Settlement town, Pawn founder, Map home, float tiles)
        {
            if (home == null) return;
            string text = RegionWords.Built(home, town, founder, RegionWords.Where(home, town.Tile, tiles));
            Tell.Minor(NewsKind.World, Importance.Notable, home, text, new LookTargets(town), MessageTypeDefOf.NeutralEvent);
        }
    }
}
