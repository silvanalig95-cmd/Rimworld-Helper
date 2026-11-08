using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Which exact things have the "wrong" style (the player's question, 8 November 2026; the rules in
    /// <see cref="StyleSurroundingsMath"/>). With Ideology, outside classic mode, the game's style-surroundings mood
    /// (ThoughtWorker_StyleDominance, every diversity precept's) comes from a sum it never shows: every 900 ticks it picks a
    /// random cell within 5 of the colonist in sight of them and adds up, over the cells within 24.9 of it in their room (and
    /// the rooms a doorway joins), each thing's StyleDominance: plus for their ideoligion's style for that thing, minus for
    /// another style, plus for their own ideoligion's buildings, floors and relics and minus for another's; plain things
    /// nothing. At 10 points either way (a room's cell count under 10 cells) the mood shows. Here:
    /// <list type="bullet">
    /// <item>The mood's tooltip (<see cref="Patch_StyleSurroundingsTip"/>, a postfix on the worker's PostProcessDescription)
    /// ends with what counts against them around them now, by kind and style with how many and the points, the biggest first;
    /// what counts for them; the sum against the threshold; and one line of what would fix it.</item>
    /// <item>A button on a colonist with the bad mood, "Show the wrong-style things", selects them on the map.</item>
    /// <item>A styled thing's inspect text names the colonists on its map whose style it goes against.</item>
    /// </list>
    /// The points are the game's own private IdeoUtility.GetStyleDominance and its joined rooms (GetJoinedRooms), reached by
    /// name, so they can't drift from the game's; read from the colonist's own cell rather than the game's random one. No mood
    /// is changed and nothing is saved; the sums are worked out at most once every 250 ticks a colonist, and a thing's line
    /// once every 250 ticks a thing. The setting "Wrong-style things named" (How you're told, On the map).
    /// </summary>
    public static class StyleSurroundings
    {
        private static Func<Thing, Ideo, float> gameDominance;
        private static Action<IntVec3, Map, HashSet<Room>> gameJoinedRooms;
        private static bool looked, broken;

        /// <summary>The setting, the deeper hooks, Ideology outside classic mode, and the game's own helpers found.</summary>
        public static bool On
        {
            get
            {
                if (broken || !(ColonyCouncilMod.Settings?.styleNotes ?? true) || !RimfolkPatches.On || !ModsConfig.IdeologyActive || Current.Game == null) return false;
                var im = Find.IdeoManager;
                if (im == null || im.classicMode) return false;
                return Helpers();
            }
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;
        private static int Bucket => Now / Sched.RareTicks;

        /// <summary>The game's private helpers, found once by name; without them the feature is off.</summary>
        private static bool Helpers()
        {
            if (looked) return gameDominance != null && gameJoinedRooms != null;
            looked = true;
            try
            {
                var d = AccessTools.Method(typeof(IdeoUtility), "GetStyleDominance", new[] { typeof(Thing), typeof(Ideo) });
                var r = AccessTools.Method(typeof(IdeoUtility), "GetJoinedRooms", new[] { typeof(IntVec3), typeof(Map), typeof(HashSet<Room>) });
                if (d != null) gameDominance = (Func<Thing, Ideo, float>)Delegate.CreateDelegate(typeof(Func<Thing, Ideo, float>), d);
                if (r != null) gameJoinedRooms = (Action<IntVec3, Map, HashSet<Room>>)Delegate.CreateDelegate(typeof(Action<IntVec3, Map, HashSet<Room>>), r);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wrong-style things: the game's style helpers", e);
            }
            if (gameDominance == null || gameJoinedRooms == null)
            {
                Log.Warning("[Rimfolk] The game's IdeoUtility.GetStyleDominance or GetJoinedRooms isn't there in this version, so the wrong-style notes are off.");
                return false;
            }
            return true;
        }

        private static void Broke(string what, Exception e)
        {
            broken = true;
            CouncilLog.ErrorOnce("wrong-style things: " + what, e);
        }

        // ---------------- Which ideoligions mind ----------------

        private static readonly Dictionary<Ideo, bool> minds = new Dictionary<Ideo, bool>();
        private static int mindsBucket = -1;

        /// <summary>Its precepts give the style-surroundings mood (any diversity precept's; looked at again every 250 ticks, for a reform).</summary>
        public static bool Minds(Ideo ideo)
        {
            if (ideo == null) return false;
            if (mindsBucket != Bucket)
            {
                minds.Clear();
                mindsBucket = Bucket;
            }
            if (minds.TryGetValue(ideo, out bool m)) return m;
            m = ideo.cachedPossibleSituationalThoughts != null && ideo.cachedPossibleSituationalThoughts.Any(t =>
                t?.workerClass != null && typeof(ThoughtWorker_StyleDominance).IsAssignableFrom(t.workerClass));
            minds[ideo] = m;
            return m;
        }

        // ---------------- The count, the game's way ----------------

        /// <summary>What counts around one colonist now: the groups, the things behind them, the sum and the threshold.</summary>
        public sealed class Report
        {
            public Room room;
            public float total, threshold;
            public List<StyleGroup> against = new List<StyleGroup>(), forThem = new List<StyleGroup>();
            /// <summary>The things counted against them, by id (floors aren't things).</summary>
            public Dictionary<int, Thing> things = new Dictionary<int, Thing>();
            public string fixKey;
            public List<string> rebuildLabels = new List<string>();
            public bool HasRoom => room != null;
        }

        private static readonly Dictionary<int, (int bucket, Report report, string tip)> reports = new Dictionary<int, (int, Report, string)>();
        private static Game reportsGame;

        private static Report Cached(Pawn p, out string tip)
        {
            if (reportsGame != Current.Game)
            {
                reports.Clear();
                reportsGame = Current.Game;
            }
            int b = Bucket;
            if (reports.TryGetValue(p.thingIDNumber, out var c) && c.bucket == b)
            {
                tip = c.tip;
                return c.report;
            }
            var r = Count(p);
            tip = Tip(p, r);
            if (reports.Count > 200) reports.Clear();
            reports[p.thingIDNumber] = (b, r, tip);
            return r;
        }

        /// <summary>
        /// The game's sum from the colonist's own cell: the cells within 24.9 in their room and the rooms a doorway joins, each
        /// floor and each thing in them by the game's own rule.
        /// </summary>
        public static Report Count(Pawn p)
        {
            var r = new Report();
            var map = p?.Map;
            var ideo = p?.Ideo;
            if (map == null || ideo == null || !p.Spawned) return r;
            var room = p.GetRoom();
            if (room == null) return r;
            r.room = room;
            r.threshold = StyleSurroundingsMath.Threshold(room.IsDoorway, room.CellCount);
            var rooms = new HashSet<Room>();
            gameJoinedRooms(p.Position, map, rooms);
            if (rooms.Count == 0) return r;
            var primary = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            var items = new List<StyleItem>();
            int cells = GenRadial.NumCellsInRadius(StyleSurroundingsMath.Radius);
            for (int i = 0; i < cells; i++)
            {
                var c = p.Position + GenRadial.RadialPattern[i];
                if (!c.InBounds(map) || c.Fogged(map)) continue;
                var cr = c.GetRoom(map);
                if (cr == null || !rooms.Contains(cr)) continue;
                var terrain = c.GetTerrain(map);
                if (terrain != null)
                {
                    float tp = 0f;
                    if (ideo.cachedPossibleBuildables.Contains(terrain)) tp = terrain.GetStatValueAbstract(StatDefOf.StyleDominance);
                    else if (!terrain.canGenerateDefaultDesignator) tp = -terrain.GetStatValueAbstract(StatDefOf.StyleDominance);
                    if (tp != 0f)
                        items.Add(new StyleItem
                        {
                            key = "floor|" + terrain.defName, label = terrain.LabelCap.Resolve(), id = map.cellIndices.CellToIndex(c), points = tp, floor = true,
                            fix = StyleFix.Replace
                        });
                }
                foreach (var t in c.GetThingList(map))
                {
                    float pts = gameDominance(t, ideo);
                    if (pts == 0f) continue;
                    items.Add(Item(t, ideo, primary, pts));
                    if (pts < 0f) r.things[t.thingIDNumber] = t;
                }
            }
            r.total = StyleSurroundingsMath.Total(items);
            var groups = StyleSurroundingsMath.Group(items);
            r.against = StyleSurroundingsMath.Against(groups);
            r.forThem = StyleSurroundingsMath.For(groups);
            r.fixKey = StyleSurroundingsMath.FixKey(primary != null, primary == ideo, r.against.Select(g => g.fix));
            r.rebuildLabels = StyleSurroundingsMath.RebuildLabels(r.against);
            return r;
        }

        /// <summary>One thing as the tooltip names it and what would put it right, by the same branches as the game's rule.</summary>
        private static StyleItem Item(Thing t, Ideo ideo, Ideo primary, float pts)
        {
            var it = new StyleItem { id = t.thingIDNumber, points = pts };
            // A relic container: the relic's ideoligion counts, whatever the container's style.
            var relic = t.TryGetComp<CompRelicContainer>();
            var relicIdeo = relic != null && relic.Full ? relic.ContainedThing?.StyleSourcePrecept?.ideo : null;
            if (relicIdeo != null)
            {
                it.key = "relic|" + t.def.defName + "|" + relicIdeo.id;
                it.label = "CC_StyleNote_Relic".Translate(t.def.label.Named("THING"), relicIdeo.name.Named("IDEO")).Resolve().CapitalizeFirst();
                it.fix = StyleFix.Replace;
                return it;
            }
            var style = t.GetStyleDef();
            if (style != null)
            {
                it.key = t.def.defName + "|" + style.defName;
                string styleLabel = style.Category != null ? style.Category.LabelCap.Resolve() : style.LabelCap.Resolve();
                it.label = "CC_StyleNote_Styled".Translate(styleLabel.Named("STYLE"), t.def.label.Named("THING")).Resolve().CapitalizeFirst();
                if (t.def.category == ThingCategory.Building && t.def.BuildableByPlayer)
                {
                    var rebuilt = primary?.GetStyleFor(t.def);
                    // Built again it takes the main ideoligion's style: plain counts nothing, their own counts for them.
                    it.fix = rebuilt == null || rebuilt == ideo.GetStyleFor(t.def) ? StyleFix.Rebuild : StyleFix.NoRebuild;
                }
                else it.fix = t.def.category == ThingCategory.Item ? StyleFix.Move : StyleFix.Replace;
                return it;
            }
            // Another ideoligion's own building (or their own one's, for them).
            var of = t.StyleSourcePrecept?.ideo;
            it.key = "own|" + t.def.defName + "|" + (of?.id ?? -1);
            it.label = of != null ? "CC_StyleNote_OfIdeo".Translate(of.name.Named("IDEO"), t.def.label.Named("THING")).Resolve() : t.def.LabelCap.Resolve();
            it.fix = StyleFix.Replace;
            return it;
        }

        // ---------------- The mood's tooltip ----------------

        /// <summary>The lines added under the style mood's description, or null.</summary>
        public static string TipFor(Pawn p)
        {
            if (p == null || !p.Spawned || p.Ideo == null || !On) return null;
            try
            {
                Cached(p, out string tip);
                return tip;
            }
            catch (Exception e)
            {
                Broke("the mood's tooltip", e);
                return null;
            }
        }

        private static string Tip(Pawn p, Report r)
        {
            var ideo = p.Ideo;
            if (!r.HasRoom) return "CC_StyleNote_NoRoom".Translate(p.Named("PAWN")).Resolve();
            var lines = new List<string>();
            string where = r.room.PsychologicallyOutdoors ? "CC_StyleNote_Outdoors".Translate().Resolve() : r.room.GetRoomRoleLabel();
            lines.Add(("CC_StyleNote_Head".Translate(p.Named("PAWN"), where.Named("ROOM")).Resolve()).Colorize(ColoredText.TipSectionTitleColor));
            if (r.against.Count == 0) lines.Add("CC_StyleNote_NothingAgainst".Translate(ideo.name.Named("IDEO")).Resolve());
            else
            {
                lines.Add("CC_StyleNote_Against".Translate(ideo.name.Named("IDEO")).Resolve());
                AddLines(lines, r.against, StyleSurroundingsMath.MaxAgainst);
            }
            if (r.forThem.Count > 0)
            {
                lines.Add("CC_StyleNote_For".Translate().Resolve());
                AddLines(lines, r.forThem, StyleSurroundingsMath.MaxFor);
            }
            lines.Add("CC_StyleNote_Total".Translate(StyleSurroundingsMath.Points(r.total), StyleSurroundingsMath.Points(-r.threshold), StyleSurroundingsMath.Points(r.threshold))
                .Resolve());
            if (r.fixKey != null && p.styleObserver?.StyleDominanceThoughtIndex == 1)
            {
                var primary = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                string labels = r.rebuildLabels.ToCommaList(true);
                lines.Add(r.fixKey.Translate(p.Named("PAWN"), ideo.name.Named("IDEO"), (primary?.name ?? "").Named("PRIMARY"), labels.Named("LABELS")).Resolve());
            }
            return string.Join("\n", lines);
        }

        private static void AddLines(List<string> lines, List<StyleGroup> groups, int max)
        {
            var shown = StyleSurroundingsMath.Shown(groups, max, out int more, out float morePoints);
            foreach (var g in shown)
                lines.Add("  " + StyleSurroundingsMath.LineKey(g).Translate(g.label, g.count, StyleSurroundingsMath.Points(g.points)).Resolve());
            if (more > 0) lines.Add("  " + "CC_StyleNote_More".Translate(more, StyleSurroundingsMath.Points(morePoints)).Resolve());
        }

        // ---------------- The button ----------------

        private static Texture2D icon;

        public static Gizmo GizmoFor(Pawn p)
        {
            if (p == null || !p.IsColonistPlayerControlled || p.styleObserver == null || p.styleObserver.StyleDominanceThoughtIndex != 1) return null;
            if (!StyleSurroundingsMath.ButtonShows(On, true, Minds(p.Ideo), p.styleObserver.StyleDominanceThoughtIndex)) return null;
            icon ??= ContentFinder<Texture2D>.Get("UI/Commands/SelectAllLinked", false) ?? BaseContent.BadTex;
            var pawn = p;
            return new Command_Action
            {
                defaultLabel = "CC_StyleNote_Gizmo".Translate(),
                defaultDesc = "CC_StyleNote_GizmoDesc".Translate(p.Named("PAWN"), p.Ideo.name.Named("IDEO")).Resolve(),
                icon = icon,
                action = () => Show(pawn),
                groupKey = 0x52466803
            };
        }

        /// <summary>The things counted against them now, selected on the map (the camera moved to the first if it's out of view).</summary>
        public static void Show(Pawn p)
        {
            Report r;
            try
            {
                r = Count(p);
            }
            catch (Exception e)
            {
                Broke("the button", e);
                return;
            }
            var things = r.against.Where(g => !g.floor).SelectMany(g => g.ids).Select(id => r.things.TryGetValue(id, out var t) ? t : null)
                .Where(t => t != null && t.Spawned && t.Map == p.Map).Distinct().Take(150).ToList();
            if (things.Count == 0)
            {
                string why = r.against.Count > 0 ? "CC_StyleNote_OnlyFloors" : "CC_StyleNote_NothingHere";
                Messages.Message(why.Translate(p.Named("PAWN"), p.Ideo.name.Named("IDEO")).Resolve(), p, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var first = things[0];
            if (Find.CurrentMap != first.Map || !Find.CameraDriver.CurrentViewRect.Contains(first.Position)) CameraJumper.TryJump(first);
            Find.Selector.ClearSelection();
            foreach (var t in things) Find.Selector.Select(t, false, false);
        }

        // ---------------- A thing's own line ----------------

        private static readonly Dictionary<Map, (int bucket, List<(Ideo ideo, string names)> who)> watchers = new Dictionary<Map, (int, List<(Ideo, string)>)>();
        private static readonly Dictionary<int, string> thingLines = new Dictionary<int, string>();
        private static int thingLinesBucket = -1;
        private static Game thingLinesGame;

        /// <summary>The ideoligions with the style mood among the colony's people on the map, each with their names (every 250 ticks).</summary>
        private static List<(Ideo ideo, string names)> Watchers(Map map)
        {
            int b = Bucket;
            if (watchers.TryGetValue(map, out var w) && w.bucket == b) return w.who;
            var who = new List<(Ideo, string)>();
            var people = map.mapPawns.FreeColonistsSpawned.Concat(map.mapPawns.SlavesOfColonySpawned)
                .Where(x => x.Ideo != null && x.needs?.mood != null && Minds(x.Ideo)).ToList();
            foreach (var grp in people.GroupBy(x => x.Ideo))
                who.Add((grp.Key, grp.Select(x => x.LabelShort).Distinct().ToCommaList()));
            if (watchers.Count > 8) watchers.Clear();
            watchers[map] = (b, who);
            return who;
        }

        /// <summary>"Against Federalism's style: Aquason, Ian", for a styled thing that counts; null otherwise.</summary>
        public static string InspectLine(Thing t)
        {
            if (t == null || t is Pawn || !t.Spawned || t.Map == null || !On) return null;
            try
            {
                if (thingLinesGame != Current.Game || thingLinesBucket != Bucket)
                {
                    thingLines.Clear();
                    thingLinesBucket = Bucket;
                    thingLinesGame = Current.Game;
                }
                if (thingLines.TryGetValue(t.thingIDNumber, out string cached)) return cached;
                string line = null;
                var who = Watchers(t.Map);
                if (who.Count > 0 && StyleSurroundingsMath.ThingCounts(t.GetStatValue(StatDefOf.StyleDominance)))
                {
                    var parts = new List<string>();
                    foreach (var (ideo, names) in who)
                        if (gameDominance(t, ideo) < 0f)
                            parts.Add("CC_StyleNote_Inspect".Translate(ideo.name.Named("IDEO"), names.Named("NAMES")).Resolve());
                    if (parts.Count > 0) line = string.Join("\n", parts);
                }
                thingLines[t.thingIDNumber] = line;
                return line;
            }
            catch (Exception e)
            {
                Broke("a thing's line", e);
                return null;
            }
        }

        // ---------------- The dev action ----------------

        /// <summary>The count for a colonist, with the game's own sum from the same cell beside it, for the log.</summary>
        public static string Explain(Pawn p)
        {
            if (!On) return "off (the setting, the hooks, Ideology, classic mode or the game's helpers)";
            var r = Count(p);
            if (!r.HasRoom) return p.LabelShort + ": not in a room";
            float game = IdeoUtility.GetStyleDominanceFromCellsCenteredOn(p.Position, p.Position, p.Map, p.Ideo);
            var lines = new List<string>
            {
                p.LabelShort + " (" + p.Ideo.name + ", mood stage " + (p.styleObserver?.StyleDominanceThoughtIndex ?? -1) + ", minds styles " + Minds(p.Ideo) + "): "
                + StyleSurroundingsMath.Points(r.total) + " here, the game's own sum from this cell " + StyleSurroundingsMath.Points(game) + ", threshold " + r.threshold
                + ", fix " + (r.fixKey ?? "none")
            };
            foreach (var g in r.against.Concat(r.forThem))
                lines.Add("  " + g.label + " x" + g.count + (g.floor ? " cells" : "") + ": " + StyleSurroundingsMath.Points(g.points) + " (" + g.fix + ")");
            return string.Join("\n", lines);
        }
    }

    /// <summary>The style mood's tooltip ends with what counts against the colonist around them now, and what would fix it.</summary>
    [HarmonyPatch(typeof(ThoughtWorker_StyleDominance), nameof(ThoughtWorker_StyleDominance.PostProcessDescription))]
    public static class Patch_StyleSurroundingsTip
    {
        public static void Postfix(Pawn p, ref string __result)
        {
            if (!RimfolkPatches.On || p == null) return;
            string more = StyleSurroundings.TipFor(p);
            if (!more.NullOrEmpty()) __result = (__result ?? "").TrimEndNewlines() + "\n\n" + more;
        }
    }

    /// <summary>The button "Show the wrong-style things" on a colonist with the bad style mood.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_StyleSurroundingsGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;
            if (!RimfolkPatches.On) yield break;
            Gizmo c = null;
            try
            {
                c = StyleSurroundings.GizmoFor(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wrong-style things: the button", e);
            }
            if (c != null) yield return c;
        }
    }

    /// <summary>A styled thing's inspect text names the colonists on its map whose style it goes against.</summary>
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString))]
    public static class Patch_StyleSurroundingsInspect
    {
        public static void Postfix(ThingWithComps __instance, ref string __result)
        {
            if (!RimfolkPatches.On || __instance is Pawn) return;
            string line = StyleSurroundings.InspectLine(__instance);
            if (line.NullOrEmpty()) return;
            __result = __result.NullOrEmpty() ? line : __result.TrimEndNewlines() + "\n" + line;
        }
    }
}
