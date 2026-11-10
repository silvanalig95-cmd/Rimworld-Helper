using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One place the colony heard of: what was said of it, who told it, through whom, and when.</summary>
    public class HeardPlace : IExposable
    {
        /// <summary>The place's card in text form (ThingRef: "s40" a site, "t55" a town, "w12345" a tile): its key.</summary>
        public string place;
        /// <summary>What was said of it, the news line itself.</summary>
        public string what;
        /// <summary>Who told the colony: "Acraeraes of Karond-Keth 2", "Golakath, reeve of Karond-Keth 2".</summary>
        public string teller;
        /// <summary>The colonist told, or whose letter it answered.</summary>
        public string through;
        /// <summary>A letter passed on: who passed it on to the one who answered it.</summary>
        public string via;
        /// <summary>Told in a letter's answer (else in talk at the colony).</summary>
        public bool letter;
        public int tick;
        /// <summary>The letter whose answer tells of it, while that answer is still on its way (the mark waits for it), else -1.</summary>
        public int waitLetter = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref place, "place");
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref teller, "teller");
            Scribe_Values.Look(ref through, "through");
            Scribe_Values.Look(ref via, "via");
            Scribe_Values.Look(ref letter, "letter");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref waitLetter, "waitLetter", -1);
        }
    }

    /// <summary>
    /// Places heard of (the player, 10 November 2026: "if other colonies tell me about things that are nearby on their map,
    /// will these also be revealed on my own map for reference?"; the rules are World/HeardPlaceMath.cs). Each place a piece
    /// of news names that is a real world object (a site, a town) or a tile (a world event's place), told in talk with
    /// someone of theirs at the colony (Outsiders/OutsiderTalk.cs) or in a letter's news once its answer is in, gets a small
    /// mark beside it on Rimfolk's region map and, as a light overlay drawn over the game's world map (nothing is added to the
    /// world itself), on the planet; its tooltip says who told the colony, through whom and when, and the mark fades as the
    /// news's keep time runs out. The heard news lines' "Show on map" leads to it. Kept in the neighbours' book
    /// (NotableBook.places), HeardPlaceMath.Max at most.
    /// </summary>
    public static class HeardPlaces
    {
        public static bool On => NotableBook.On;

        private static NotableBook Book => NotableBook.Instance;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= Heard =================

        /// <summary>
        /// News told in talk named these facts (<paramref name="facts"/>, the facts service's: "site:40", "ws:7"): each place among
        /// them is heard of from now. Returns the first place's key, for the news line's "Show on map", or null.
        /// </summary>
        public static string Heard(IEnumerable<string> facts, string what, string teller, string through)
        {
            if (!On || facts == null) return null;
            string first = null;
            try
            {
                foreach (var f in facts)
                {
                    string key = PlaceOf(f);
                    if (key == null) continue;
                    Add(new HeardPlace { place = key, what = what, teller = teller, through = through, tick = Now });
                    first ??= key;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heard of: a place from talk", e);
            }
            return first;
        }

        /// <summary>
        /// A letter's answer tells of news (the first <paramref name="shown"/> lines): each place a line names is heard of once
        /// the answer is in (<see cref="AnswerIn"/>), told by <paramref name="n"/> to whoever wrote, passed on by whoever did.
        /// </summary>
        public static void FromLetter(NeighbourLetter l, Notable n, FactSet facts, int shown)
        {
            if (!On || l == null || n == null || facts == null) return;
            try
            {
                var via = l.passed && l.notable != n.id ? Book?.Get(l.notable) : null;
                for (int i = 0; i < Math.Min(shown, facts.news.Count) && i < facts.newsFacts.Count; i++)
                {
                    string key = PlaceOf(facts.newsFacts[i]);
                    if (key == null) continue;
                    Add(new HeardPlace
                    {
                        place = key, what = facts.news[i], teller = "CC_Heard_Teller".Translate(n.Name.Named("NAME"), (n.townName ?? n.faction?.Name ?? "?").Named("TOWN")).Resolve(),
                        through = l.WriterName, via = via?.Name, letter = true, tick = Now,
                        waitLetter = l.id
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heard of: a place from a letter", e);
            }
        }

        /// <summary>A letter's answer is in (NeighbourLetters.Owed): the places its news named are heard of from now.</summary>
        public static void AnswerIn(NeighbourLetter l)
        {
            var list = Book?.places;
            if (l == null || list == null) return;
            bool any = false;
            foreach (var h in list)
                if (h != null && h.waitLetter == l.id)
                {
                    h.waitLetter = -1;
                    h.tick = Now;
                    any = true;
                }
            if (any) Changed();
        }

        /// <summary>The place a fact names, as its card's text form: a site or a town by its world object, a world event's place by its town or tile; null for none.</summary>
        private static string PlaceOf(string fact)
        {
            if (!HeardPlaceMath.ParseFact(fact, out bool site, out int id)) return null;
            ThingRef r;
            if (site)
            {
                var o = Find.WorldObjects.AllWorldObjects.FirstOrDefault(x => x != null && x.ID == id);
                if (o == null || o.Destroyed) return null;
                r = ThingRefs.Of(o);
            }
            else
            {
                var s = WorldSituations.Instance?.Get(id);
                if (s == null || s.place.IsNone) return null;
                r = ThingRefs.Of(s.place);
            }
            return r.IsNone || !ThingRefs.TileOf(r).Valid ? null : r.ToString();
        }

        /// <summary>Kept: the same place heard again is one mark brought up to date with the newest telling; the oldest go past the cap.</summary>
        private static void Add(HeardPlace h)
        {
            var list = Book?.places;
            if (list == null || h?.place == null) return;
            int same = HeardPlaceMath.Same(list.Select(x => new HeardPlaceFacts { key = x?.place, tick = x?.tick ?? 0 }).ToList(), h.place);
            if (same >= 0) list.RemoveAt(same);
            int over = HeardPlaceMath.Over(list.Count);
            if (over > 0) list.RemoveRange(0, Math.Min(over, list.Count));
            list.Add(h);
            Changed();
        }

        /// <summary>Once a day (the neighbours' day): past their keep time, gone from the world, or waiting on an answer that never came: let go.</summary>
        public static void Daily(int now)
        {
            var list = Book?.places;
            if (list == null) return;
            int before = list.Count;
            list.RemoveAll(h => h == null || h.place == null
                                || h.waitLetter < 0 && HeardPlaceMath.Gone(h.tick, now, HeardPlaceMath.KeepDays)
                                || h.waitLetter >= 0 && !(NeighbourLetters.Get(h.waitLetter)?.Open ?? false)
                                || !ThingRefs.TileOf(ThingRef.Parse(h.place)).Valid);
            if (list.Count != before) Changed();
        }

        // ================= Shown =================

        /// <summary>A place heard of as it is drawn: where, how strongly, and its tooltip.</summary>
        public sealed class Shown
        {
            public HeardPlace h;
            public ThingRef thing;
            public PlanetTile tile;
            public float alpha;
            public string tip;
        }

        private static readonly List<Shown> shown = new List<Shown>();
        private static float shownAt = -100f;
        private static int version, shownVersion = -1;

        private static void Changed()
        {
            version++;
            NotableBook.Changed();
        }

        /// <summary>The places heard of that are marked now (their answer in, their time not run out, still on the world), worked out at most once a second.</summary>
        public static List<Shown> Current()
        {
            float t = Time.realtimeSinceStartup;
            if (shownVersion == version && t - shownAt < 1f && t >= shownAt) return shown;
            shownAt = t;
            shownVersion = version;
            shown.Clear();
            var list = Book?.places;
            if (!On || list == null) return shown;
            try
            {
                int now = Now;
                foreach (var h in list)
                {
                    if (h == null || h.waitLetter >= 0 || HeardPlaceMath.Gone(h.tick, now, HeardPlaceMath.KeepDays)) continue;
                    var r = ThingRef.Parse(h.place);
                    var tile = ThingRefs.TileOf(r);
                    if (!tile.Valid) continue;
                    shown.Add(new Shown { h = h, thing = r, tile = tile, alpha = HeardPlaceMath.Alpha(h.tick, now, HeardPlaceMath.KeepDays), tip = Tip(h) });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heard of: the marks", e);
            }
            return shown;
        }

        /// <summary>"Heard of: a bandit camp 4 tiles north of Ashford", and who told the colony, through whom, and when.</summary>
        public static string Tip(HeardPlace h)
        {
            if (h == null) return null;
            var lines = new List<string> { "CC_Heard_Tip_Head".Translate((h.what ?? "?").Named("WHAT")).Resolve() };
            string when = OutsiderHeard.Ago(h.tick) + " (" + DayOf(h.tick) + ")";
            string key = !h.letter ? "CC_Heard_Tip_Talk" : h.via.NullOrEmpty() ? "CC_Heard_Tip_Letter" : "CC_Heard_Tip_LetterVia";
            lines.Add(key.Translate((h.teller ?? "?").Named("TELLER"), (h.through ?? "?").Named("PAWN"), (h.via ?? "").Named("VIA"), when.Named("WHEN")).Resolve());
            return string.Join("\n", lines);
        }

        /// <summary>The tooltip lines of places heard of at this world object (a site or a town on the region map), or none.</summary>
        public static List<string> TipsAt(WorldObject o)
        {
            var list = new List<string>();
            if (o == null) return list;
            foreach (var s in Current())
                if (s.tile == o.Tile)
                    list.Add(s.tip);
            return list;
        }

        /// <summary>"Show on map" for a news line that named a place heard of, as a link (UI/Cards/LinkTextMath.cs, WrapOnMap); "" when the place is gone.</summary>
        public static string ShowOnMap(string place)
        {
            if (place.NullOrEmpty()) return "";
            var r = ThingRef.Parse(place);
            if (r.IsNone || !ThingRefs.TileOf(r).Valid) return "";
            return LinkTextMath.WrapOnMap("CC_Heard_ShowOnMap".Translate().Resolve(), r.ToString());
        }

        private static string DayOf(int tick)
        {
            var map = HappeningTracker.HomeMap();
            float lng = map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
            return GenDate.DateShortStringAt(GenDate.TickGameToAbs(tick), new Vector2(lng, 0f));
        }

        // ================= The game's world map =================

        /// <summary>The mark's look: a small letter on a dark disc.</summary>
        private static readonly Color Back = new Color(0.08f, 0.08f, 0.1f, 0.85f);

        public static readonly Color Ink = new Color(0.93f, 0.82f, 0.55f);

        /// <summary>
        /// Over the game's world map (Patch_HeardPlacesWorld): each place heard of on the layer in view and on the planet's
        /// near side, a small mark above and left of its tile, with its tooltip. Drawn as part of the world's interface, under
        /// any window; nothing is added to the world.
        /// </summary>
        public static void DrawWorld()
        {
            if (!On || !WorldRendererUtility.WorldRendered || Find.WorldGrid == null) return;
            var list = Current();
            if (list.Count == 0) return;
            var layer = Find.WorldSelector?.SelectedLayer;
            bool paint = Event.current.type == EventType.Repaint;
            foreach (var s in list)
            {
                if (layer != null && s.tile.Layer != layer) continue;
                Vector3 pos = Find.WorldGrid.GetTileCenter(s.tile);
                if (WorldRendererUtility.HiddenBehindTerrainNow(pos)) continue;
                Vector2 ui = GenWorldUI.WorldToUIPosition(pos);
                var r = new Rect(ui.x - 22f, ui.y - 22f, 13f, 13f);
                if (paint)
                {
                    UiKit.DiscFill(r.ExpandedBy(3f), UiKit.WithAlpha(Back, Back.a * s.alpha));
                    UiKit.Icon(r, Icons.Journal, UiKit.WithAlpha(Ink, s.alpha));
                }
                TooltipHandler.TipRegion(r.ExpandedBy(3f), new TipSignal(s.tip, 0x4EA4D ^ s.h.place.GetHashCode()));
            }
        }
    }

    /// <summary>
    /// Places heard of on the game's world map (World/HeardPlaces.cs): drawn after the world's own interface each frame the
    /// world is shown, behind the deeper hooks. If the game's method changes it is skipped with one warning and the marks are
    /// on Rimfolk's region map only.
    /// </summary>
    [HarmonyPatch(typeof(WorldInterface), nameof(WorldInterface.WorldInterfaceOnGUI))]
    public static class Patch_HeardPlacesWorld
    {
        internal static bool broken;

        public static void Postfix()
        {
            if (broken || !RimfolkPatches.On) return;
            try
            {
                HeardPlaces.DrawWorld();
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("heard of: the world map", e);
            }
        }
    }
}
