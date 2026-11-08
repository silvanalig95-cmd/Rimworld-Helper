using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A town of a people as the Peoples view lists it: how far, how big, how the colony's letters reach it.</summary>
    public sealed class PeopleTown
    {
        public Settlement s;
        public ThingRef thing;
        public float tiles;
        /// <summary>Days on the road by the post's reach rule (-1: no way by land), and whether that is worked out yet.</summary>
        public float days = -1f;
        public bool known;
        public ReachBand band = ReachBand.Near;
        public string daysWord, size;
    }

    /// <summary>One people of the list: its facts for the pure rules, and its words and colours, made once a second.</summary>
    public sealed class PeopleEntry
    {
        public Faction f;
        public ThingRef thing;
        public PeopleFacts facts;
        public string name, kind, stance, standing, ageWord, heading, daysWord, tip;
        /// <summary>The row's second line (stance and age), the header's trend line, the nearest town's line: made with the entry.</summary>
        public string second, trendLine, nearestLine;
        /// <summary>Whom the colony knows of theirs by name, alive, closest town first (PeopleActs.Notables), made with the entry.</summary>
        public List<Notable> notables = new List<Notable>();
        public Color stanceColor;
        /// <summary>Their towns, closest first (by tiles, then by days as they are worked out).</summary>
        public readonly List<PeopleTown> towns = new List<PeopleTown>();
        public PeopleTown nearest;
        /// <summary>Which way their regard is heading this season (-1, 0, 1) and the deeds that moved it, in words.</summary>
        public int trend;
        public readonly List<string> reasons = new List<string>();
        public DipPeople dip;
        public int mail;

        private Dictionary<string, string> words;

        /// <summary>A line naming them ("No town of {PEOPLE} is known..."), made once for the entry.</summary>
        public string Named(string key)
        {
            words ??= new Dictionary<string, string>();
            if (!words.TryGetValue(key, out var s)) words[key] = s = key.Translate(name.Named("PEOPLE")).Resolve();
            return s;
        }
    }

    /// <summary>
    /// The Peoples view's model (UI/Region/PeoplesView.cs): every people the colony can know with what the list shows of
    /// it (stance, regard, its nearest town's days by the post's reach rule, mail, its age), the groups in the player's
    /// sort, the filters' counts. Built at most once a second while the view shows; the towns' days are worked out a couple
    /// a frame (the world's paths, Post/PostReach.cs) and the list is put in order again as they come.
    /// </summary>
    public static class PeoplesModel
    {
        public static readonly List<PeopleEntry> entries = new List<PeopleEntry>();
        private static readonly List<PeopleFacts> facts = new List<PeopleFacts>();
        /// <summary>The list in its groups (indices into <see cref="entries"/>), and how many each filter would show.</summary>
        public static List<int>[] groups = { new List<int>(), new List<int>(), new List<int>() };
        public static readonly int[] counts = new int[3];
        public static Map home;
        private static float builtAt = -100f;
        private static Game builtGame;
        private static bool dirtyOrder = true;
        private static PeoplesSort orderSort;
        private static PeoplesFilter orderFilters;
        private static string orderQuery;

        /// <summary>The home the distances are from: the colony in view, else the seat.</summary>
        public static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat ?? HappeningTracker.HomeMap();

        /// <summary>Built again on the next draw (an act taken, a game loaded).</summary>
        public static void Dirty() => builtAt = -100f;

        /// <summary>Built when more than a second old; put in order again when the sort, the filters or the search changed, or a town's days came in.</summary>
        public static void Refresh()
        {
            float now = Time.realtimeSinceStartup;
            if (builtGame != Current.Game || now - builtAt > 1f || now < builtAt)
            {
                try
                {
                    Build();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region window: the peoples' list", e);
                    entries.Clear();
                    facts.Clear();
                }
                builtAt = now;
                builtGame = Current.Game;
                dirtyOrder = true;
            }
            if (Event.current == null || Event.current.type == EventType.Repaint) Settle();
            if (dirtyOrder || orderSort != RegionView.sort || orderFilters != RegionView.filters || orderQuery != RegionView.query)
            {
                orderSort = RegionView.sort;
                orderFilters = RegionView.filters;
                orderQuery = RegionView.query;
                dirtyOrder = false;
                groups = PeoplesViewMath.Grouped(facts, orderSort, orderFilters, orderQuery);
                for (int i = 0; i < PeoplesViewMath.Filters.Length; i++) counts[i] = PeoplesViewMath.Count(facts, PeoplesViewMath.Filters[i], orderQuery);
            }
        }

        /// <summary>The people chosen, or null (all peoples, or none in the list): the first of the list when none was chosen yet.</summary>
        public static PeopleEntry Chosen()
        {
            if (RegionView.chosen == RegionView.AllPeoples) return null;
            if (RegionView.chosen != RegionView.NoneChosen)
                foreach (var e in entries)
                    if (e.f.loadID == RegionView.chosen)
                        return e;
            foreach (var g in groups)
                if (g.Count > 0)
                {
                    RegionView.chosen = entries[g[0]].f.loadID;
                    return entries[g[0]];
                }
            return null;
        }

        public static PeopleEntry Of(Faction f)
        {
            if (f == null) return null;
            foreach (var e in entries)
                if (e.f == f)
                    return e;
            return null;
        }

        // ================= Building =================

        private static void Build()
        {
            entries.Clear();
            facts.Clear();
            home = Home;
            var all = Find.FactionManager?.AllFactionsListForReading;
            if (all == null) return;
            var dip = DiplomacyModel.Current;
            var byPeople = new Dictionary<Faction, DipPeople>();
            foreach (var p in dip.peoples)
                if (p?.people != null)
                    byPeople[p.people] = p;
            // Their towns, one pass over the world's settlements.
            var townsOf = new Dictionary<Faction, List<Settlement>>();
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s?.Faction == null || s.Destroyed || s.Faction.IsPlayer) continue;
                if (!townsOf.TryGetValue(s.Faction, out var l)) townsOf[s.Faction] = l = new List<Settlement>();
                l.Add(s);
            }
            // Within the region map's reach of a home: a neighbour.
            var near = new HashSet<Faction>();
            foreach (var r in RegionTowns.Reach)
                foreach (var s in r.towns)
                    if (s?.Faction != null)
                        near.Add(s.Faction);
            var mailFrom = MailFrom();
            var book = NotableBook.Instance;
            foreach (var f in all)
            {
                if (!PeoplesView.Listed(f)) continue;
                var e = new PeopleEntry { f = f, thing = ThingRefs.Of(f), name = f.Name, kind = f.def.LabelCap.ToString() };
                e.dip = byPeople.TryGetValue(f, out var dp) ? dp : null;
                int relation = f.PlayerRelationKind == FactionRelationKind.Hostile ? -1 : f.PlayerRelationKind == FactionRelationKind.Ally ? 1 : 0;
                int age = Peoples.AgeOf(f);
                e.ageWord = PeopleAgeWords.AgeWord(age);
                e.heading = PeopleAgeWords.Heading(f);
                e.standing = RegionMap.StandingText(f);
                e.stanceColor = RegionMap.StandingColor(f);
                e.stance = StanceWord(f, relation);
                mailFrom.TryGetValue(f, out e.mail);
                var search = new List<string> { f.Name, e.kind, e.ageWord };
                if (townsOf.TryGetValue(f, out var towns) && home != null)
                {
                    foreach (var s in towns)
                    {
                        if (s.Tile.Layer != home.Tile.Layer) continue;
                        var t = new PeopleTown { s = s, thing = ThingRefs.Of(s), tiles = LivingEvents.Distance(home.Tile, s.Tile), size = SizeOf(s) };
                        e.towns.Add(t);
                        search.Add(s.Name);
                    }
                    e.towns.Sort((a, b) => a.tiles.CompareTo(b.tiles));
                    // The towns that could be the nearest by the road: within a quarter again of the nearest in a straight line.
                    float first = e.towns.Count > 0 ? e.towns[0].tiles : 0f;
                    for (int i = 0; i < e.towns.Count; i++)
                    {
                        var t = e.towns[i];
                        if (i >= 3 || t.tiles > first * 1.25f + 2f) break;
                        Look(t);
                    }
                }
                if (book != null)
                    foreach (var n in book.notables)
                        if (n != null && n.faction == f && n.Alive)
                            search.Add(n.Name);
                Trend(e);
                e.second = e.stance + " · " + e.ageWord;
                e.facts = new PeopleFacts
                {
                    name = f.Name, search = string.Join(" ", search).ToLowerInvariant(), relation = relation, permanentEnemy = f.def.permanentEnemy, hasGoodwill = f.HasGoodwill,
                    goodwill = f.HasGoodwill ? f.PlayerGoodwill : 0, age = age, neighbour = near.Contains(f), mail = e.mail > 0,
                    // Its ties and what is under way, and the colony's dealings with them as a whole (Dealings/).
                    dealings = e.dip != null && (e.dip.ties.Count > 0 || e.dip.underway.Count > 0) || Dealings.AnyLive(f)
                };
                Nearest(e);
                e.notables = PeopleActs.Notables(e);
                entries.Add(e);
                facts.Add(e.facts);
            }
        }

        /// <summary>A town's reach now: its days and band, whether worked out yet, and its days in words.</summary>
        public static void Look(PeopleTown t)
        {
            if (home == null || t.s == null) return;
            var info = PostReach.Of(home, t.s, true);
            t.days = info.days;
            t.band = info.call || Ages.Ours >= WorldAge.Spacer ? ReachBand.Radio : info.band;
            t.known = PostReach.Kept(home, t.s);
            t.daysWord = DaysWord(t.days);
        }

        /// <summary>The nearest town by its days (by tiles until they're worked out, or with no way by land).</summary>
        private static void Nearest(PeopleEntry e)
        {
            PeopleTown best = null;
            foreach (var t in e.towns)
            {
                if (t.daysWord == null) continue;
                if (best == null || Key(t) < Key(best)) best = t;
            }
            e.nearest = best ?? (e.towns.Count > 0 ? e.towns[0] : null);
            e.facts.days = e.nearest?.days ?? -1f;
            e.facts.tiles = e.nearest?.tiles ?? 99999f;
            e.daysWord = e.nearest == null ? "CC_PplV_NoTown".Translate().Resolve() : e.nearest.daysWord ?? DaysWord(-1f);
            e.tip = e.standing + (e.nearest != null ? "\n" + "CC_PplV_NearestTip".Translate(e.nearest.s.Name.Named("TOWN"), e.daysWord.Named("DAYS")).Resolve() : "");
            e.nearestLine = e.nearest != null
                ? "CC_PplP_Nearest".Translate(ThingRefs.Link(e.nearest.s.Name, e.nearest.thing).Named("TOWN"), e.daysWord.Named("DAYS")).Resolve()
                : null;
        }

        private static float Key(PeopleTown t) => t.days >= 0f ? t.days : 100000f + t.tiles;

        /// <summary>A couple of towns' days a frame, as the world's paths are worked out; the list is put in order again when one comes in.</summary>
        private static void Settle()
        {
            if (home == null) return;
            int budget = 2;
            for (int i = 0; i < entries.Count && budget > 0; i++)
            {
                var e = entries[i];
                bool moved = false;
                foreach (var t in e.towns)
                {
                    if (t.daysWord == null || t.known || budget <= 0) continue;
                    budget--;
                    float before = t.days;
                    Look(t);
                    moved |= t.known || Mathf.Abs(before - t.days) > 0.01f;
                }
                if (!moved) continue;
                Nearest(e);
                facts[i] = e.facts;
                dirtyOrder = true;
            }
        }

        /// <summary>"3 days", "1 day", "no road".</summary>
        public static string DaysWord(float days)
        {
            if (days < 0f) return "CC_PplV_NoRoad".Translate().Resolve();
            int d = PostMath.RoughDays(days);
            return d <= 1 ? "CC_PplV_Day".Translate().Resolve() : "CC_PplV_Days".Translate(d.Named("DAYS")).Resolve();
        }

        /// <summary>A town's size in a word: a hamlet, a trading post, a new town, else their people's word for a town; and its fortune where it has one.</summary>
        private static string SizeOf(Settlement s)
        {
            string word;
            switch (SmallTowns.KindOf(s))
            {
                case SmallTown.Hamlet: word = "CC_PplV_Size_Hamlet".Translate().Resolve(); break;
                case SmallTown.Post: word = "CC_PplV_Size_Post".Translate().Resolve(); break;
                case SmallTown.NewTown: word = "CC_PplV_Size_New".Translate().Resolve(); break;
                default: word = Peoples.TownWord(s.Faction) ?? "CC_PplV_Size_Town".Translate().Resolve(); break;
            }
            var fortune = RegionFortunes.Of(s.ID);
            return fortune.HasValue ? word.CapitalizeFirst() + ", " + RegionFortunes.Word(fortune.Value, s.Faction) : word.CapitalizeFirst();
        }

        /// <summary>The stance in a word or two for a row: allies, friendly, fair terms, cool, unfriendly, at war, everyone's foe, no dealings.</summary>
        private static string StanceWord(Faction f, int relation)
        {
            var st = RegionMapMath.StandingOf(false, f.HasGoodwill, relation, f.HasGoodwill ? f.PlayerGoodwill : 0, f.def.permanentEnemy);
            return ("CC_PplV_Std_" + st).Translate().Resolve();
        }

        /// <summary>This season's trend from what they remember the colony did in it, and the deeds that moved it most.</summary>
        private static void Trend(PeopleEntry e)
        {
            var mem = FactionMemory.On ? FactionMemory.Instance : null;
            if (mem == null)
            {
                TrendLine(e);
                return;
            }
            int now = Find.TickManager.TicksGame;
            float favour = 0f, wrong = 0f;
            var season = new List<FactionDeed>();
            foreach (var d in mem.Of(e.f))
            {
                if (d == null || now - d.tick > GenDate.TicksPerSeason || now < d.tick) continue;
                if (d.side == DeedSide.Favor) favour += d.weight;
                else if (d.side == DeedSide.Wrong) wrong += d.weight;
                else continue;
                season.Add(d);
            }
            e.trend = PeoplesViewMath.Trend(favour, wrong);
            foreach (var d in season.OrderByDescending(d => d.weight).Take(3))
            {
                string text = FactionMemory.Text(d);
                if (!text.NullOrEmpty()) e.reasons.Add(text);
            }
            TrendLine(e);
        }

        /// <summary>"Warming this season: gave them a gift, freed Ana", "Steady this season", "Cooling this season: ...".</summary>
        private static void TrendLine(PeopleEntry e)
        {
            string key = e.trend > 0 ? "CC_PplP_Trend_Up" : e.trend < 0 ? "CC_PplP_Trend_Down" : e.reasons.Count > 0 ? "CC_PplP_Trend_Steady" : "CC_PplP_Trend_Quiet";
            e.trendLine = key.Translate(TextUtil.JoinAnd(e.reasons).Named("WHY")).Resolve();
        }

        /// <summary>
        /// Mail from each people: letters on their way in by the post (Post/PostBook.cs), their calls waiting on the colony's
        /// answer, and their letters waiting in the letter stack for a choice.
        /// </summary>
        private static Dictionary<Faction, int> MailFrom()
        {
            var d = new Dictionary<Faction, int>();
            void Add(Faction f)
            {
                if (f == null) return;
                d.TryGetValue(f, out int n);
                d[f] = n + 1;
            }
            try
            {
                var post = PostBook.Instance;
                if (post != null)
                    foreach (var i in post.incoming)
                        Add(i?.from);
                if (WorldLedger.Instance != null)
                    foreach (var t in FavourAsks.Waiting())
                        Add(Refs.Faction(t.to));
                foreach (var l in Find.LetterStack.LettersListForReading)
                    if (l is ChoiceLetter && l.relatedFaction != null)
                        Add(l.relatedFaction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region window: the peoples' mail", e);
            }
            return d;
        }
    }
}
