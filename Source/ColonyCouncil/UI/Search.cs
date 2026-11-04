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
    /// The search in every window's corner and on a key (docs/design/interface.md, "Back, forward, search, following"):
    /// it finds colonists, persons, places, peoples, houses, views and modes by name ("Bonds"), and Almanac articles,
    /// grouped by kind; Enter takes the first. The index is made when the field is clicked (never while it is shut),
    /// filtered as the player types (SearchMath, pure). A result opens the thing's card (in the drawer of the window in
    /// sight, else the floating card), the view, or the Almanac's window. Other parts add kinds with
    /// <see cref="Register"/>.
    /// </summary>
    public static class Search
    {
        private static readonly List<Func<IEnumerable<SearchItem>>> sources = new List<Func<IEnumerable<SearchItem>>>();
        private static KeyBindingDef key;
        private static bool looked;

        /// <summary>Adds things the search finds: asked each time the index is made; an item's own open, or its key, says what a click does.</summary>
        public static void Register(Func<IEnumerable<SearchItem>> items)
        {
            if (items != null) sources.Add(items);
        }

        // ---------------- The index, made when the field is clicked ----------------

        /// <summary>Everything the search can find now, its names folded once.</summary>
        public static List<SearchItem> Index()
        {
            portraits.Clear();
            var items = new List<SearchItem>();
            void Part(string what, Action add)
            {
                try
                {
                    add();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("search: " + what, e);
                }
            }
            var seen = new HashSet<string>();
            void Thing(SearchKind kind, ThingRef r, string name, string sub, string also = null)
            {
                if (r.IsNone || name.NullOrEmpty() || !seen.Add(r.ToString())) return;
                items.Add(new SearchItem(kind, name, r.ToString(), sub, also));
            }
            if (Current.Game != null)
            {
                Part("colonists", () =>
                {
                    foreach (var p in WeekModel.Colonists())
                        Thing(SearchKind.Colonist, ThingRefs.Of(p), p.LabelShort, p.story?.TitleShortCap, p.Name?.ToStringFull);
                });
                Part("persons", () =>
                {
                    var book = NotableBook.Instance;
                    if (book != null)
                        foreach (var n in book.notables)
                        {
                            if (n == null) continue;
                            string title = NotableBook.TitleOf(n);
                            string where = n.townName ?? n.faction?.Name;
                            Thing(SearchKind.Person, ThingRefs.Of(n), n.Name, where.NullOrEmpty() ? title : "CC_Search_TitleOf".Translate(title.Named("TITLE"), where.Named("TOWN")).Resolve(),
                                where);
                        }
                    var ledger = WorldLedger.Instance;
                    if (ledger != null)
                        foreach (var v in ledger.Persons)
                        {
                            if (v == null || v.person.IsNone) continue;
                            if (v.pawn != null && (v.pawn.IsColonist || v.pawn.Faction == Faction.OfPlayer)) continue;
                            Thing(SearchKind.Person, ThingRef.From(v.person), v.Name, v.people?.Name);
                        }
                });
                Part("places", () =>
                {
                    foreach (var map in Homes.All)
                        if (map?.Parent is Settlement s)
                            Thing(SearchKind.Place, ThingRefs.Of(s), Homes.Name(map), "CC_Card_ColonyHome".Translate());
                    foreach (var s in Find.WorldObjects.Settlements)
                    {
                        if (s == null || s.Destroyed || s.Faction == null || s.Faction.IsPlayer || !s.HasName) continue;
                        Thing(SearchKind.Place, ThingRefs.Of(s), s.Name, CardWords.TownKind(s, false), s.Faction.Name);
                    }
                    foreach (var (_, c) in RaidersCamps.Known())
                    {
                        var site = RaidersCamps.SiteOf(c);
                        if (site != null) Thing(SearchKind.Place, ThingRefs.Of(site), c.label, c.people?.Name, c.kind);
                    }
                });
                Part("peoples", () =>
                {
                    foreach (var f in Find.FactionManager.AllFactionsVisibleInViewOrder)
                        if (f != null && !f.IsPlayer && !f.defeated)
                            Thing(SearchKind.People, ThingRefs.Of(f), f.Name, f.def?.LabelCap.ToString());
                });
                Part("houses", () =>
                {
                    var houses = WorldLedger.Instance?.Houses;
                    if (houses != null)
                        foreach (var h in houses)
                            if (h != null)
                                Thing(SearchKind.House, ThingRef.From(h.house), RegionWords.HouseName(h), h.people?.Name, h.name);
                });
            }
            Part("views", () => Views(items));
            Part("articles", () =>
            {
                foreach (var a in DefDatabase<AlmanacArticleDef>.AllDefsListForReading)
                    if (a != null && a.DlcActive && !a.label.NullOrEmpty())
                        items.Add(new SearchItem(SearchKind.Article, a.LabelCap.ToString(), a.defName, ("CC_AlmanacCat_" + a.category).Translate().Resolve(),
                            a.keywords.NullOrEmpty() ? null : string.Join(" ", a.keywords)));
            });
            foreach (var src in sources)
                Part("another part", () =>
                {
                    var more = src();
                    if (more != null) items.AddRange(more.Where(x => x != null));
                });
            SearchMath.Prepare(items);
            return items;
        }

        /// <summary>Every view and mode of the four windows by its name ("Bonds", "Map: Trade"), with its window's and view's names to find it by.</summary>
        private static void Views(List<SearchItem> items)
        {
            var seen = new HashSet<string>();
            void Add(string name, string address, string sub, string also)
            {
                if (name.NullOrEmpty() || !seen.Add(name + "|" + address)) return;
                items.Add(new SearchItem(SearchKind.View, name, address, sub, also));
            }
            foreach (RimfolkWindow w in PagesMath.CornerOrder)
            {
                string window = Pages.WindowName(w);
                foreach (var view in PagesMath.ViewsOf(w))
                {
                    var modes = PagesMath.ModesOf(w, view);
                    string viewName = PagesMath.HasViews(w) ? Pages.ViewName(w, view) : window;
                    if (PagesMath.HasViews(w)) Add(viewName, view, window, window);
                    foreach (var page in modes)
                    {
                        if (page == "Faiths" && !Faiths.On) continue;
                        string name = Pages.ModeName(page);
                        Add(name, page, name == viewName ? window : "CC_Search_In".Translate(viewName.Named("VIEW"), window.Named("WINDOW")).Resolve(), viewName + " " + window);
                        // A region view's own modes and lenses ("Map: Trade", "Peoples: Faces").
                        foreach (var m in PagesMath.ModesOf(page).Skip(1)) Add(Pages.Name(page + "/" + m), page + "/" + m, window, viewName + " " + window);
                        foreach (var l in PagesMath.LensesOf(page)) Add(Pages.Name(page + "/" + l), page + "/" + l, window, viewName + " " + window);
                    }
                }
            }
        }

        // ---------------- Opening what was found ----------------

        /// <summary>A result taken: its card, its view or its Almanac article (another part's, its own open).</summary>
        public static void Open(SearchItem item)
        {
            if (item == null) return;
            try
            {
                if (item.open != null)
                {
                    item.open();
                    return;
                }
                switch (item.kind)
                {
                    case SearchKind.View:
                        Pages.Open(item.key);
                        break;
                    case SearchKind.Article:
                        var a = DefDatabase<AlmanacArticleDef>.GetNamedSilentFail(item.key);
                        if (a != null) Window_Almanac.Open(a);
                        break;
                    default:
                        if (ThingRef.TryParse(item.key, out var r)) Cards.Open(r);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("search: opening a result", e);
            }
        }

        /// <summary>
        /// The portraits of the results, found once per index: finding a pawn the ledger doesn't keep (a colonist, mostly)
        /// goes through every pawn of the world, too much for every frame.
        /// </summary>
        private static readonly Dictionary<string, Pawn> portraits = new Dictionary<string, Pawn>();

        /// <summary>A result's picture: a colonist's or a person's portrait, a people's banner, else its kind's icon.</summary>
        public static void Picture(Rect r, SearchItem item)
        {
            Pawn pawn = null;
            Faction faction = null;
            Texture2D icon;
            switch (item.kind)
            {
                case SearchKind.Colonist:
                case SearchKind.Person:
                    if (item.key != null && !portraits.TryGetValue(item.key, out pawn))
                    {
                        pawn = ThingRef.TryParse(item.key, out var pr) ? ThingRefs.Pawn(pr) : null;
                        portraits[item.key] = pawn;
                    }
                    icon = Icons.People;
                    break;
                case SearchKind.People:
                    if (ThingRef.TryParse(item.key, out var fr)) faction = ThingRefs.Faction(fr);
                    icon = Icons.People;
                    break;
                case SearchKind.Place: icon = Icons.World; break;
                case SearchKind.House: icon = Icons.People; break;
                case SearchKind.View: icon = Icons.Overview; break;
                case SearchKind.Article: icon = Icons.Almanac; break;
                default: icon = Icons.Search; break;
            }
            UiKit.Picture(r, pawn, faction, icon, UiKit.Faint);
        }

        /// <summary>A kind's heading over its results.</summary>
        public static string KindName(SearchKind k)
        {
            switch (k)
            {
                case SearchKind.Colonist: return "CC_Search_Kind_Colonist".Translate();
                case SearchKind.Person: return "CC_Search_Kind_Person".Translate();
                case SearchKind.Place: return "CC_Search_Kind_Place".Translate();
                case SearchKind.People: return "CC_Search_Kind_People".Translate();
                case SearchKind.House: return "CC_Search_Kind_House".Translate();
                case SearchKind.View: return "CC_Search_Kind_View".Translate();
                case SearchKind.Article: return "CC_Search_Kind_Article".Translate();
                default: return "CC_Search_Kind_Other".Translate();
            }
        }

        // ---------------- The field and the key ----------------

        /// <summary>The search opens under the corner's field (its rect on the screen), or at the top of the screen from the key; a second click shuts it.</summary>
        public static void Toggle(Rect anchor)
        {
            var open = Find.WindowStack?.WindowOfType<Window_Search>();
            if (open != null)
            {
                open.Close();
                return;
            }
            // The click on the corner's icon that shut it (as a click outside it) doesn't open it again.
            if (Time.realtimeSinceStartup - Window_Search.closedAt < 0.3f) return;
            Find.WindowStack?.Add(new Window_Search(anchor));
        }

        /// <summary>The key bound to the search, or null.</summary>
        public static string KeyLabel()
        {
            Look();
            try
            {
                return key == null || key.MainKey == KeyCode.None ? null : key.MainKeyLabel;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            key = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Council_Key_Search");
        }

        /// <summary>From the game's OnGUI (UI/WeekMemory.cs): the search's key, when no text field has the keyboard.</summary>
        public static void OnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.keyCode == KeyCode.None || GUIUtility.keyboardControl != 0) return;
            Look();
            if (key == null || !key.KeyDownEvent) return;
            e.Use();
            Toggle(Rect.zero);
        }
    }

    /// <summary>
    /// The search's field and its results, under the corner's field: the index made when it opens, the results grouped
    /// by kind as the player types, Enter (or a click) taking the one lit, the arrows moving it, Escape or a click outside
    /// closing it. It never pauses the game.
    /// </summary>
    public class Window_Search : Window
    {
        private readonly Rect anchor;
        private readonly List<SearchItem> index;
        private List<SearchGroup> groups = new List<SearchGroup>();
        private readonly List<SearchItem> flat = new List<SearchItem>();
        private string query = "", searched;
        private int lit;
        private bool focused;
        private Vector2 scroll;
        private float height;
        private const string Field = "RimfolkSearchField";
        /// <summary>When the last one shut (Search.Toggle).</summary>
        internal static float closedAt = -1f;
        private const float RowH = 34f, HeadH = 22f;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(400f, UI.screenWidth - 20f), Mathf.Min(460f, UI.screenHeight - 60f));

        protected override float Margin => 10f;

        public Window_Search(Rect anchor)
        {
            this.anchor = anchor;
            layer = WindowLayer.Dialog;
            doCloseX = false;
            closeOnAccept = false;
            // Enter is the game's accept key, which the game hands to the topmost window that takes it before any window
            // draws: the search takes it (OnAcceptKeyPressed), or the window under it would close instead.
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            // The keys typed into its field don't move the camera.
            preventCameraMotion = true;
            forcePause = false;
            soundAppear = SoundDefOf.TabOpen;
            soundClose = null;
            index = Search.Index();
        }

        public override void PostClose()
        {
            base.PostClose();
            closedAt = Time.realtimeSinceStartup;
        }

        protected override void SetInitialSizeAndPosition()
        {
            var size = InitialSize;
            float x = anchor.width > 0f ? anchor.xMax - size.x : (UI.screenWidth - size.x) / 2f;
            float y = anchor.height > 0f ? anchor.yMax + 4f : 60f;
            windowRect = new Rect(Mathf.Clamp(x, 4f, UI.screenWidth - size.x - 4f), Mathf.Clamp(y, 4f, UI.screenHeight - size.y - 4f), size.x, size.y).Rounded();
        }

        private void Take(SearchItem item)
        {
            if (item == null) return;
            SoundDefOf.Click.PlayOneShotOnCamera();
            Close(false);
            Search.Open(item);
        }

        /// <summary>The one lit, or the first found.</summary>
        private SearchItem Lit => lit >= 0 && lit < flat.Count ? flat[lit] : SearchMath.First(groups);

        /// <summary>Enter (the accept key): takes the one lit, and the key goes no further (to the window under the search).</summary>
        public override void OnAcceptKeyPressed()
        {
            Event.current.Use();
            Take(Lit);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // The keys before the field takes them: Enter takes the one lit, the arrows move it.
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    e.Use();
                    Take(Lit);
                    return;
                }
                if ((e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.UpArrow) && flat.Count > 0)
                {
                    lit = (lit + (e.keyCode == KeyCode.DownArrow ? 1 : -1) + flat.Count) % flat.Count;
                    e.Use();
                }
            }
            var fieldRect = new Rect(inRect.x, inRect.y, inRect.width, 30f);
            UiKit.Icon(new Rect(fieldRect.x + 6f, fieldRect.y + 7f, 16f, 16f), Icons.Search, UiKit.Faint);
            GUI.SetNextControlName(Field);
            query = Widgets.TextField(new Rect(fieldRect.x + 28f, fieldRect.y, fieldRect.width - 28f, fieldRect.height), query ?? "");
            if (!focused)
            {
                UI.FocusControl(Field, this);
                focused = true;
            }
            if (query != searched)
            {
                searched = query;
                groups = SearchMath.Find(query, index);
                flat.Clear();
                foreach (var g in groups) flat.AddRange(g.items);
                lit = 0;
                scroll = Vector2.zero;
            }
            var list = new Rect(inRect.x, fieldRect.yMax + 8f, inRect.width, inRect.height - fieldRect.height - 8f);
            if (query.NullOrEmpty())
            {
                UiKit.Paragraph(list.x + 4f, list.y + 4f, list.width - 8f, "CC_Search_Hint".Translate(), UiKit.Faint);
                return;
            }
            if (flat.Count == 0)
            {
                UiKit.Paragraph(list.x + 4f, list.y + 4f, list.width - 8f, "CC_Search_None".Translate(query.Named("QUERY")).Resolve(), UiKit.Faint);
                return;
            }
            var view = new Rect(0f, 0f, list.width - 16f, Mathf.Max(height, list.height - 1f));
            Widgets.BeginScrollView(list, ref scroll, view);
            try
            {
                float y = 0f;
                int i = 0;
                foreach (var g in groups)
                {
                    string head = Search.KindName(g.kind) + (g.total > g.items.Count ? "  " + "CC_Search_OfMore".Translate(g.items.Count.Named("SHOWN"), g.total.Named("ALL")).Resolve() : "");
                    UiKit.LabelFit(new Rect(4f, y, view.width - 8f, HeadH), head, UiKit.HeaderColor, GameFont.Tiny);
                    UiKit.Divider(0f, y + HeadH - 2f, view.width);
                    y += HeadH;
                    foreach (var item in g.items)
                    {
                        var r = new Rect(0f, y, view.width, RowH);
                        if (i == lit) Widgets.DrawBoxSolid(r, UiKit.CardSelected);
                        else if (Mouse.IsOver(r)) Widgets.DrawBoxSolid(r, UiKit.CardHover);
                        Search.Picture(new Rect(4f, y + 5f, 24f, 24f), item);
                        UiKit.LabelFit(new Rect(34f, y + 1f, view.width - 38f, 18f), item.name, i == lit ? UiKit.Gold : Color.white);
                        if (!item.sub.NullOrEmpty()) UiKit.LabelFit(new Rect(34f, y + 17f, view.width - 38f, 16f), item.sub, UiKit.Faint, GameFont.Tiny);
                        if (Widgets.ButtonInvisible(r))
                        {
                            Take(item);
                            return;
                        }
                        y += RowH;
                        i++;
                    }
                    y += 6f;
                }
                height = y;
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }
    }
}
