using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Every thing's card (docs/design/interface.md, "Things and their cards"): one builder for each of the eight kinds,
    /// each card's model built at most once a second while it shows (nothing while no card does), and the one way to
    /// open a card. <see cref="Open(ThingRef)"/> opens it where the player is: in the drawer of the window in sight (the
    /// council, region and planning windows hold one), else in a small floating card window (from the defense window, a
    /// panel, a letter). <see cref="ShowOnMap"/>
    /// opens a place on the region map with its card as the map's panel; that is where "Show" leads now, the game's world
    /// view only one small button on the place card's head.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Cards
    {
        // ---------------- Opening ----------------

        /// <summary>
        /// Opens a thing's card where the player is: the drawer in sight (the council, region or planning window's, or one
        /// another window holds), else the floating card. Nothing for none.
        /// </summary>
        public static void Open(ThingRef r)
        {
            if (r.IsNone) return;
            Previews.Hide();
            try
            {
                var host = Drawer.Active;
                if (host != null)
                {
                    host.Open(r);
                    return;
                }
                Window_Card.Show(r);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cards: opening a card", e);
            }
        }

        public static void Open(Pawn p) => Open(ThingRefs.Of(p));

        public static void Open(Faction f) => Open(ThingRefs.Of(f));

        public static void Open(WorldObject o) => Open(ThingRefs.Of(o));

        public static void Open(Notable n) => Open(ThingRefs.Of(n));

        public static void Open(Tie t) => Open(ThingRefs.Of(t));

        public static void Open(Ref r) => Open(ThingRef.From(r));

        /// <summary>
        /// A place on the region map: the region window on its map, the place picked and its card the map's panel (a new
        /// trail of cards). Anything that isn't a place opens its card instead.
        /// </summary>
        public static void ShowOnMap(ThingRef r)
        {
            if (r.IsNone) return;
            if (!r.IsPlace)
            {
                Open(r);
                return;
            }
            Previews.Hide();
            Drawer.Open(RimfolkWindow.World, r, true);
            Pages.Open("Map");
        }

        public static void ShowOnMap(WorldObject o)
        {
            if (o != null && !o.Destroyed) ShowOnMap(ThingRefs.Of(o));
        }

        /// <summary>A target on the world (a world object or a tile) on the region map; anything on a map is jumped to as before.</summary>
        public static void ShowOnMap(GlobalTargetInfo t)
        {
            if (!t.IsValid) return;
            if (t.HasWorldObject)
            {
                ShowOnMap(t.WorldObject);
                return;
            }
            if (t.IsMapTarget || t.HasThing)
            {
                Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJumpAndSelect(t);
                return;
            }
            ShowOnMap(ThingRefs.OfTile(t.Tile));
        }

        /// <summary>
        /// "Open in its window" from the floating card: a colonist on their page in the People roster, a place on the
        /// region map, anything else in the region window's drawer on the page that lists its kind.
        /// </summary>
        public static void OpenInWindow(ThingRef r)
        {
            if (r.IsNone) return;
            var kind = ThingRefs.CardOf(r);
            if (kind == CardKind.Colonist)
            {
                var p = ThingRefs.Pawn(r);
                if (p != null) MainTabWindow_Council.OpenPerson(p);
                return;
            }
            if (kind == CardKind.Place)
            {
                ShowOnMap(r);
                return;
            }
            Drawer.Open(RimfolkWindow.World, r, true);
            Pages.Open(FullPage(kind));
        }

        /// <summary>The region page that lists a kind of thing (where its card opens in full until the region's views come).</summary>
        public static string FullPage(CardKind k)
        {
            switch (k)
            {
                case CardKind.Place: return "Map";
                case CardKind.People:
                case CardKind.Tie: return "Peoples";
                case CardKind.Event: return "WorldHistory";
                case CardKind.Letter: return "Letters";
                default: return "Neighbours";
            }
        }

        // ---------------- Models ----------------

        private static readonly Dictionary<ThingRef, CardModel> models = new Dictionary<ThingRef, CardModel>();

        /// <summary>A thing's card model, built again when it is more than a second old (only cards that show ask).</summary>
        public static CardModel Model(ThingRef r)
        {
            float now = Time.realtimeSinceStartup;
            if (models.TryGetValue(r, out var m) && now - m.builtAt < 1f && now >= m.builtAt) return m;
            m = Build(r);
            m.builtAt = now;
            if (models.Count > 64) models.Clear();
            models[r] = m;
            return m;
        }

        /// <summary>Every model built again at the next ask (an act changed what they show).</summary>
        public static void Dirty() => models.Clear();

        /// <summary>Forget the last game's cards (a game loaded).</summary>
        public static void Reset()
        {
            models.Clear();
            ThingRefs.Reset();
        }

        private static CardModel Build(ThingRef r)
        {
            var m = new CardModel { thing = r, kind = ThingRefs.CardOf(r) };
            try
            {
                if (!ThingRefs.Exists(r))
                {
                    m.gone = true;
                    m.title = ThingRefs.Name(r);
                    m.subtitle = "CC_Card_Gone".Translate().Resolve();
                    return m;
                }
                switch (m.kind)
                {
                    case CardKind.Colonist: Colonist(m); break;
                    case CardKind.Person: Person(m); break;
                    case CardKind.Place: Place(m); break;
                    case CardKind.People: People(m); break;
                    case CardKind.House: House(m); break;
                    case CardKind.Tie: TieCard(m); break;
                    case CardKind.Event: EventCard(m); break;
                    case CardKind.Letter: LetterCard(m); break;
                }
                Registered(m);
                // Slot by slot, by order, the builder's and the features' sections together (a stable sort: equals keep their order).
                var sorted = m.blocks.OrderBy(b => b.slot).ThenBy(b => b.order).ToList();
                m.blocks.Clear();
                m.blocks.AddRange(sorted);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cards: a card of " + m.kind, e);
                m.title ??= ThingRefs.Name(r);
            }
            return m;
        }

        /// <summary>The lines and acts features added to this kind of card.</summary>
        private static void Registered(CardModel m)
        {
            foreach (var e in CardSections.registry.Of(m.kind))
            {
                var sec = e.value;
                if (sec.lines == null) continue;
                List<string> lines = null;
                try
                {
                    lines = sec.lines(m.thing);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("cards: the section " + e.id, ex);
                }
                if (lines == null || lines.Count == 0) continue;
                var b = m.Block(e.slot, e.order, e.id, sec.heading.NullOrEmpty() ? null : sec.heading.Translate().Resolve());
                foreach (var l in lines) b.Line(l);
            }
            foreach (var e in CardActs.registry.In(m.kind, CardSlot.Acts))
            {
                var a = e.value;
                try
                {
                    if (a.shows != null && !a.shows(m.thing)) continue;
                    var thing = m.thing;
                    m.Act(a.label(thing), a.whyNot?.Invoke(thing), () => a.act(thing), a.tip);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("cards: the act " + e.id, ex);
                }
            }
        }

        private static string T(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        private static string Link(ThingRef r) => ThingRefs.Link(r);

        private static string Link(string label, ThingRef r) => ThingRefs.Link(label, r);

        // ---------------- A colonist ----------------

        private static void Colonist(CardModel m)
        {
            var p = ThingRefs.Pawn(m.thing);
            var comp = CouncilGameComponent.Instance;
            m.pawn = p;
            m.title = p.LabelShortCap;
            if (comp != null)
            {
                string titles = comp.epithets.TitlesOf(p);
                if (!titles.NullOrEmpty()) m.title += ", " + titles;
                m.ring = UiKit.TrustColor(comp.standings.TrustOf(p));
            }
            var parts = new List<string>();
            if (p.story != null) parts.Add(p.story.TitleShortCap);
            if (p.ageTracker != null) parts.Add(T("CC_People_Age", p.ageTracker.AgeBiologicalYears));
            // The years asleep beside the age, as the roster showed them.
            string slept = FamilyYears.SleptNote(p);
            if (slept != null) parts.Add(slept);
            if (p.IsPrisonerOfColony) parts.Add(T("CC_Card_Prisoner"));
            else if (p.IsSlaveOfColony) parts.Add(T("CC_Card_Slave"));
            m.subtitle = string.Join("  ·  ", parts);
            if (comp != null && p.IsColonist)
            {
                string trust = ("CC_Trust_" + PawnStanding.LevelFor(comp.standings.TrustOf(p))).Translate();
                m.State(trust, m.ring, T("CC_Trust_Header"));
                m.Glance(T("CC_Story_Trust", trust));
            }
            if (p.needs?.mood != null) m.Glance(T("CC_People_Mood", p.needs.mood.CurLevelPercentage.ToStringPercent()));
            var goal = comp != null && ColonyCouncilMod.Settings.aspirations ? comp.aspirations.Active(p) : null;
            if (goal != null) m.Glance(goal.Goal);
            var pawn = p;
            m.Act(T("CC_Card_Act_Roster"), null, () => MainTabWindow_Council.OpenPerson(pawn));
            m.Act(T("CC_Card_Act_Jump"), p.Spawned ? null : T("CC_Card_Why_NotOnMap", p.LabelShort.Named("NAME")), () =>
            {
                Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJumpAndSelect(pawn);
            });
        }

        // ---------------- Someone outside ----------------

        private static void Person(CardModel m)
        {
            var lr = m.thing.ToRef();
            var v = WorldLedger.Person(lr);
            var n = ThingRefs.Notable(m.thing);
            m.pawn = v?.pawn ?? n?.pawn;
            m.title = v?.name ?? ThingRefs.Name(m.thing);
            m.faction = v?.people ?? n?.faction;
            int townId = v != null && v.town >= 0 ? v.town : n?.town ?? -1;
            var town = townId >= 0 ? ThingRefs.Of(Ref.Town(townId)) : ThingRef.None;
            string townName = !town.IsNone && ThingRefs.Exists(town) ? Link(town) : v?.townName ?? n?.townName;
            string people = m.faction != null ? Link(m.faction.Name, ThingRefs.Of(m.faction)) : null;
            string title = v?.title ?? (n != null ? NotableBook.TitleOf(n) : null);
            if (!title.NullOrEmpty() && !townName.NullOrEmpty()) m.subtitle = T("CC_Card_TitleOf", title.Named("TITLE"), townName.Named("TOWN"));
            else if (!title.NullOrEmpty() && people != null) m.subtitle = T("CC_Card_TitleOfPeople", title.Named("TITLE"), people.Named("PEOPLE"));
            else if (v?.role == "departed") m.subtitle = T("CC_Card_Departed");
            else if (people != null) m.subtitle = T("CC_Card_OneOf", people.Named("PEOPLE"));
            else m.subtitle = T("CC_Card_Someone");
            // The town and the people as chips where the line above doesn't name them.
            if (!town.IsNone && ThingRefs.Exists(town) && m.subtitle.IndexOf(town.ToString(), StringComparison.Ordinal) < 0) m.Chip(town);
            if (m.faction != null && m.subtitle.IndexOf(ThingRefs.Of(m.faction).ToString(), StringComparison.Ordinal) < 0)
                m.Chip(ThingRefs.Of(m.faction), m.faction.Name, m.faction);
            if (v != null && !v.alive) m.State(T("CC_Card_Dead"), UiKit.Dim);
            if (n != null)
            {
                int op = NotableBook.Opinion(n);
                m.ring = NotableBook.RegardColor(op);
                string regard = NotableBook.RegardWords(op);
                m.State(regard, m.ring);
                m.Glance(regard.CapitalizeFirst() + ".");
                // Now: a letter on its way to them, in steps.
                var l = NeighbourLetters.OpenTo(n);
                if (l != null)
                {
                    var b = m.Block(CardSlot.Now, 10f, "letter", T("CC_Card_Head_Letter"));
                    b.steps = CardWords.LetterSteps(l);
                    b.step = CardWords.LetterStep(l);
                    string status = NeighboursPanel.StatusWords(l, n);
                    b.Line(Link(CardWords.LetterName(l), ThingRefs.Of(l)), UiKit.Info, GameFont.Tiny);
                    if (!status.NullOrEmpty()) m.Glance(status);
                }
                // Who of ours knows them.
                var known = NotableBook.Known(n);
                if (known.Count > 0)
                {
                    var b = m.Block(CardSlot.Who, 10f, "known", T("CC_Card_Head_Known"));
                    b.count = known.Count;
                    foreach (var (pawn, why) in known.Take(6))
                    {
                        string who = Link(pawn.LabelShort, ThingRefs.Of(pawn));
                        b.rows.Add(new CardRow { thing = ThingRefs.Of(pawn), pawn = pawn, ring = UiKit.TrustColor(CouncilGameComponent.Instance?.standings.TrustOf(pawn) ?? 0f), name = who, second = why.NullOrEmpty() ? null : why.CapitalizeFirst() });
                    }
                    m.Glance(T("CC_Card_KnowsLine", TextUtil.JoinAnd(known.Take(2).Select(k => k.pawn.LabelShort).ToList()).Named("NAMES")));
                }
                // What they remember of the colony.
                if (n.notes.Count > 0)
                {
                    var b = m.Block(CardSlot.Words, 10f, "remembers", T("CC_Card_Head_Remembers"));
                    b.count = n.notes.Count;
                    for (int i = n.notes.Count - 1; i >= 0 && b.lines.Count < 5; i--)
                        b.Line(T("CC_Card_NoteLine", n.notes[i].text.Named("TEXT"), FactionMemory.AgoText(n.notes[i].tick).Named("AGO")), UiKit.Faint);
                }
                var note = n;
                string why0 = NeighboursPanel.WhyNoLetter(n);
                m.Act(T("CC_Neighbours_Write"), why0, () => Find.WindowStack.Add(new Dialog_NeighbourLetter(note)));
            }
            else if (v != null && v.metBy.Count > 0)
            {
                var b = m.Block(CardSlot.Who, 10f, "known", T("CC_Card_Head_Known"));
                b.count = v.metBy.Count;
                foreach (var met in v.metBy.Take(6))
                {
                    var c = met.colonist;
                    b.rows.Add(new CardRow { thing = ThingRefs.Of(c), pawn = c, name = c != null ? Link(met.name ?? c.LabelShort, ThingRefs.Of(c)) : met.name });
                }
            }
            if (!(v?.royal?.label).NullOrEmpty()) m.State(v.royal.label, UiKit.Gold);
            if (!town.IsNone && ThingRefs.Exists(town))
            {
                var t = town;
                m.Act(T("CC_Card_Act_Town"), null, () => Open(t));
            }
            CardWords.Story(m, lr, Ref.None);
        }

        // ---------------- A place ----------------

        private static void Place(CardModel m)
        {
            var r = m.thing;
            var wo = ThingRefs.WorldObject(r);
            var s = wo as Settlement;
            var tile = ThingRefs.TileOf(r);
            var home = ThingRefs.Home(r);
            m.place = wo;
            m.tile = tile;
            Faction f = wo?.Faction;
            if (r.kind == ThingKind.Tile)
            {
                m.title = T("CC_Card_Tile");
                m.icon = Icons.World;
                m.iconColor = UiKit.Faint;
                m.subtitle = T("CC_Card_TileSub");
            }
            else if (wo == null)
            {
                // A town gone from the world: what the ledger kept of it.
                var view = r.kind == ThingKind.Town ? WorldLedger.Town(r.id) : null;
                m.title = view?.name ?? ThingRefs.Name(r);
                m.gone = true;
                f = view?.people;
                m.subtitle = T("CC_Card_GoneTown", (f != null ? Link(f.Name, ThingRefs.Of(f)) : T("CC_Card_NoPeople")).Named("PEOPLE"));
            }
            else
            {
                m.title = home != null && Homes.All.Count <= 1 ? T("CC_Region_YourColony", wo.LabelCap.Named("NAME")) : wo.LabelCap.ToString();
                try
                {
                    m.icon = wo.ExpandingIcon;
                    m.iconColor = wo.ExpandingIconColor;
                }
                catch (Exception)
                {
                    m.icon = null;
                }
                if (m.icon == BaseContent.BadTex) m.icon = null;
                if (m.icon == null) m.icon = Icons.World;
                if (s != null && f != null && !f.IsPlayer) m.subtitle = CardWords.TownKind(s);
                else if (f != null && f.IsPlayer && s != null) m.subtitle = T(home != null ? "CC_Card_ColonyHome" : "CC_Card_Colony");
                else m.subtitle = wo.def?.LabelCap.ToString() ?? "";
            }
            if (f != null && !f.IsPlayer && !(wo is Caravan))
            {
                m.faction = f;
                string st = RegionMap.StandingText(f);
                var color = RegionMap.StandingColor(f);
                m.State(st, color);
                m.Glance(st);
            }
            string far = CardWords.Distance(tile);
            if (!far.NullOrEmpty())
            {
                m.State(far, UiKit.Faint);
                m.Glance(far);
            }
            // Now: what Rimfolk knows of their people at this place.
            if (f != null && !f.IsPlayer)
            {
                var b = m.Block(CardSlot.Now, 0f, "known");
                var ws = WorldSituations.Instance;
                if (ws != null && ColonyCouncilMod.Settings.worldSituations)
                    foreach (var sit in ws.Running)
                        if (sit.ctx?.faction == f && !sit.Label.NullOrEmpty())
                            b.Line(T("CC_Region_InTheWorld", sit.Label), UiKit.WorldColor, GameFont.Small, Icons.WorldEvent);
                if (FactionMemory.On && FactionMemory.Remembers(f))
                {
                    int tone = FactionMemory.Instance.Tone(f);
                    if (tone != 0) b.Line(T("CC_Region_Remember", ("CC_Memory_Tone_" + tone).Translate()), UiKit.WorldColor);
                }
                if (b.Empty) m.blocks.Remove(b);
            }
            // The land itself, quietly, after what is happening.
            if (tile.Valid)
            {
                string land = RegionMap.LandText(tile);
                if (!land.NullOrEmpty()) m.Block(CardSlot.Now, 95f, "land").Line(land, UiKit.Faint, GameFont.Tiny);
            }
            // Story: what happened there and what the town did.
            if (r.kind == ThingKind.Town || r.kind == ThingKind.Colony) CardWords.Story(m, Ref.None, r.ToRef(), 50f);
            if (r.kind == ThingKind.Town && s != null)
            {
                var book = NotableBook.Instance;
                var head = book?.notables.FirstOrDefault(x => x != null && x.town == s.ID && x.Alive && x.role == NotableRole.Head)
                    ?? book?.notables.FirstOrDefault(x => x != null && x.town == s.ID && x.Alive);
                if (head != null && NotableBook.On)
                {
                    var to = head;
                    m.Act(T("CC_Card_Act_WriteThere"), NeighboursPanel.WhyNoLetter(head), () => Find.WindowStack.Add(new Dialog_NeighbourLetter(to)),
                        T("CC_Card_Act_WriteThere_Tip", head.Name.Named("NAME")));
                }
            }
        }

        // ---------------- A people ----------------

        private static void People(CardModel m)
        {
            var f = ThingRefs.Faction(m.thing);
            var v = WorldLedger.People(f);
            m.faction = f;
            m.title = f.Name;
            m.subtitle = f.def?.LabelCap.ToString() ?? "";
            string st = RegionMap.StandingText(f);
            m.State(st, RegionMap.StandingColor(f));
            m.Glance(st);
            // Now: their wars and the world's events about them.
            var now = m.Block(CardSlot.Now, 0f, "now");
            if (f.def != null && f.def.permanentEnemy) now.Line(T("CC_Peoples_EveryonesFoe"), UiKit.Faint);
            else if (Facts.Counts(f))
            {
                var foes = new List<string>();
                foreach (var g in Find.FactionManager.AllFactionsListForReading)
                    if (g != f && Facts.Counts(g) && f.HostileTo(g))
                        foes.Add(Link(g.Name, ThingRefs.Of(g)));
                if (foes.Count > 0)
                {
                    string wars = T("CC_Peoples_Wars", TextUtil.JoinAnd(foes).Named("FOES"));
                    now.Line(wars, UiKit.Warn);
                    m.Glance(LinkTextMath.Plain(wars));
                }
            }
            var ws = ColonyCouncilMod.Settings.worldSituations ? WorldSituations.Instance : null;
            if (ws != null)
                foreach (var sit in ws.Running)
                    if ((sit.ctx?.faction == f || sit.ctx?.enemy == f) && !sit.Label.NullOrEmpty())
                        now.Line(T("CC_Region_InTheWorld", sit.Label), UiKit.WorldColor, GameFont.Small, Icons.WorldEvent);
            if (now.Empty) m.blocks.Remove(now);
            // Who: their leader, and their towns within reach, nearest first.
            var who = m.Block(CardSlot.Who, 10f, "who", T("CC_Card_Head_Theirs"));
            if (f.leader != null && !f.leader.Dead)
            {
                var lr = ThingRefs.Of(f.leader);
                who.rows.Add(new CardRow { thing = lr, pawn = f.leader, name = Link(f.leader.LabelShort, lr), second = T("CC_Card_Leader") });
            }
            var homeMap = HappeningTracker.HomeMap();
            if (homeMap != null && NotableBook.Instance != null)
                foreach (var t in NotableBook.Instance.Towns(homeMap))
                {
                    if (t.Faction != f) continue;
                    var tr = ThingRefs.Of(t);
                    who.rows.Add(new CardRow { thing = tr, icon = t.ExpandingIcon == BaseContent.BadTex ? Icons.World : t.ExpandingIcon, ring = f.Color, name = Link(t.Name, tr), second = CardWords.Distance(t.Tile) });
                    if (who.rows.Count >= 8) break;
                }
            who.count = who.rows.Count;
            if (who.rows.Count == 0) m.blocks.Remove(who);
            // Words: what they remember of the colony, strongest first.
            if (v != null && v.remembers.Count > 0)
            {
                var words = m.Block(CardSlot.Words, 10f, "remembers", T("CC_Card_Head_TheyRemember"));
                words.count = v.remembers.Count;
                foreach (var d in v.remembers.Take(4))
                {
                    string text = FactionMemory.Text(new FactionDeed { kind = d.kind, count = d.count, faction = f, name = d.name });
                    if (!text.NullOrEmpty()) words.Line(text.CapitalizeFirst() + ".", d.side == DeedSide.Wrong || d.side == DeedSide.TheirWrong ? UiKit.Bad : UiKit.Good);
                }
                if (words.Empty) m.blocks.Remove(words);
            }
            CardWords.Story(m, Refs.Of(f), Ref.None);
            var fr = Refs.Of(f);
            m.Act(T("CC_Peoples_TheirHistory"), null, () => WorldHistoryPage.ShowFor(fr, Ref.None));
        }

        // ---------------- A house ----------------

        private static void House(CardModel m)
        {
            var h = ThingRefs.House(m.thing);
            m.title = RegionWords.HouseName(h);
            m.faction = h.people;
            m.icon = Icons.People;
            m.iconColor = h.people?.Color ?? UiKit.Faint;
            var town = h.town >= 0 ? ThingRefs.Of(Ref.Town(h.town)) : ThingRef.None;
            string townName = !town.IsNone && ThingRefs.Exists(town) ? Link(town) : WorldLedger.TownRecord(h.town)?.name ?? T("CC_Card_NoPeople");
            m.subtitle = T(h.founding ? "CC_Card_FoundingHouseOf" : "CC_Card_HouseOf", townName.Named("TOWN"));
            if (h.people != null) m.Chip(ThingRefs.Of(h.people), h.people.Name, h.people);
            var who = m.Block(CardSlot.Who, 10f, "members", T("CC_Card_Head_Members"));
            if (!h.head.IsNone)
            {
                var hr = ThingRef.From(h.head);
                who.rows.Add(new CardRow { thing = hr, pawn = WorldLedger.PawnOf(h.head), name = Link(ThingRefs.Name(hr), hr), second = T("CC_Card_HeadOfHouse") });
            }
            if (h.members.Count > 0) who.Line(T("CC_Card_MembersLine", string.Join(", ", h.members).Named("MEMBERS")), UiKit.Faint);
            who.count = h.members.Count + (h.head.IsNone ? 0 : 1);
            if (who.Empty) m.blocks.Remove(who);
            m.Glance(LinkTextMath.Plain(m.subtitle).CapitalizeFirst() + ".");
            if (h.members.Count > 0) m.Glance(T("CC_Card_MembersLine", string.Join(", ", h.members).Named("MEMBERS")));
            CardWords.Story(m, h.house, Ref.None);
        }

        // ---------------- A tie ----------------

        private static void TieCard(CardModel m)
        {
            var t = ThingRefs.Tie(m.thing);
            m.title = CardWords.TieName(t);
            m.icon = Icons.Promise ?? Icons.Trust;
            m.iconColor = t.Live ? UiKit.Gold : UiKit.Faint;
            var from = ThingRef.From(t.from);
            var to = ThingRef.From(t.to);
            m.subtitle = T("CC_Card_TieBetween", Link(from).Named("FROM"), Link(to).Named("TO"));
            string state = CardWords.TieState(t);
            m.State(state, t.Live ? UiKit.Gold : UiKit.Done);
            m.Glance(state);
            var now = m.Block(CardSlot.Now, 0f, "terms");
            if (t.kind == Tie.Kind.Favour)
            {
                now.Line(DipWords.DebtLine(t));
                now.Line(DipWords.StateLine(t), UiKit.Faint);
                m.Glance(DipWords.DebtLine(t));
            }
            if (t.terms != null && !t.terms.about.IsNone)
            {
                var about = ThingRef.From(t.terms.about);
                now.Line(T("CC_Card_TieAbout", Link(about).Named("WHO")), UiKit.Faint);
            }
            if (now.Empty) m.blocks.Remove(now);
            // Its story: the history's lines of this tie's kind between the two.
            var b = m.Block(CardSlot.Story, 50f, "story", T("CC_Card_Head_Story"));
            string fam = HistoryKinds.OfTie(t.kind, "made");
            fam = fam.Substring(0, fam.LastIndexOf(':'));
            foreach (var e in History.Of(t.from, new[] { fam }, max: 20))
            {
                if (!e.actors.Exists(a => a.who == t.to)) continue;
                b.lines.Add(new CardLine(CardWords.HistoryDate(e, true) + "  " + CardWords.HistoryLine(e, true), UiKit.Faint));
                if (b.lines.Count >= 4) break;
            }
            if (b.Empty) m.blocks.Remove(b);
            var people = from.kind == ThingKind.People ? from : to.kind == ThingKind.People ? to : ThingRef.None;
            if (!people.IsNone) m.Act(T("CC_Card_Act_People"), null, () => Open(people));
        }

        // ---------------- An event ----------------

        private static void EventCard(CardModel m)
        {
            var e = ThingRefs.Entry(m.thing);
            m.title = WorldHistoryMath.Words(e.kind).CapitalizeFirst();
            string fam = HistoryKinds.Family(e.kind);
            int ck = Array.IndexOf(HistoryKinds.Chronicle, fam);
            m.icon = ck >= 0 ? Icons.For((ChronicleKind)ck) : fam == HistoryKinds.Deed ? Icons.Trust ?? Icons.Chronicle : fam == HistoryKinds.Tie ? Icons.Promise : Icons.WorldEvent;
            m.iconColor = ck >= 0 ? UiKit.ChronicleColor((ChronicleKind)ck) : UiKit.WorldColor;
            var (from, to) = WorldHistoryMath.Days(e);
            string day = from < to ? T("CC_Card_Days", "CC_DayN".Translate(from).Named("FROM"), "CC_DayN".Translate(to).Named("TO")) : "CC_DayN".Translate(to).Resolve();
            var place = ThingRef.From(e.place);
            m.subtitle = !place.IsNone && place.kind != ThingKind.Tile && ThingRefs.Exists(place) ? T("CC_Card_DayAt", day.Named("DAY"), Link(place).Named("PLACE")) : day;
            if (e.weight > HistoryMath.Everyday) m.State(T("CC_Card_Milestone"), UiKit.Gold);
            string line = CardWords.HistoryLine(e, true);
            m.Block(CardSlot.Now, 0f, "what").Line(line);
            m.Glance(LinkTextMath.Plain(line));
            var who = m.Block(CardSlot.Who, 10f, "who", T("CC_Card_Head_TookPart"));
            foreach (var a in e.actors)
            {
                var r = ThingRef.From(a.who);
                if (r.IsNone || !ThingRefs.Exists(r)) continue;
                who.rows.Add(new CardRow
                {
                    thing = r, pawn = r.kind == ThingKind.Person ? ThingRefs.Pawn(r) : null, faction = ThingRefs.Faction(r), name = Link(r),
                    second = ("CC_Card_Role_" + a.role).Translate().Resolve()
                });
            }
            who.count = who.rows.Count;
            if (who.rows.Count == 0) m.blocks.Remove(who);
            Ref people = Ref.None, town = Ref.None;
            foreach (var a in e.actors)
                if (people.IsNone && a.who.kind == RefKind.People)
                    people = a.who;
            if (e.place.kind == RefKind.Town) town = e.place;
            m.Act(T("CC_Card_Act_InHistory"), null, () => WorldHistoryPage.ShowFor(people, town));
        }

        // ---------------- A letter ----------------

        private static void LetterCard(CardModel m)
        {
            var l = ThingRefs.Letter(m.thing);
            var n = NotableBook.Instance?.Get(l.notable);
            m.title = CardWords.LetterName(l);
            m.icon = l.channel == LetterChannel.Console ? Icons.Visitor : Icons.Journal;
            m.iconColor = l.Open ? UiKit.Info : UiKit.Faint;
            string writer = l.writer != null && !l.writer.Discarded ? Link(l.WriterName, ThingRefs.Of(l.writer)) : l.WriterName;
            m.subtitle = T("CC_Card_LetterFrom", writer.Named("PAWN"), MainTabWindow_Council.DayString(l.created).Named("DAY"));
            if (n != null) m.Chip(ThingRefs.Of(n), n.Name);
            var b = m.Block(CardSlot.Now, 0f, "steps");
            if (l.stage == LetterStage.Cancelled) b.Line(T("CC_LettersPg_CalledOff"), UiKit.Faint);
            else
            {
                b.steps = CardWords.LetterSteps(l);
                b.step = CardWords.LetterStep(l);
                string status = l.stage == LetterStage.Done
                    ? T("CC_LettersPg_Answered", NeighbourLetters.ToneWord(l.tone).Named("TONE"))
                    : n != null ? NeighboursPanel.StatusWords(l, n) : null;
                b.Line(status, l.stage == LetterStage.Done ? UiKit.Done : UiKit.Info);
                m.Glance(status);
            }
            if (!l.topic.NullOrEmpty()) m.Block(CardSlot.Words, 10f, "about", T("CC_Card_Head_About")).Line(l.topic.CapitalizeFirst());
            m.Act(T("CC_Card_Act_Letters"), null, () => Pages.Open("Letters"));
        }

        /// <summary>A click sound for opening a card.</summary>
        public static void Click() => SoundDefOf.Click.PlayOneShotOnCamera();
    }
}
