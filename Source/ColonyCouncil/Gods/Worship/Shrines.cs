using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Places dedicated to a god (docs/design/gods.md, "Worship": shrines and offerings; brainstorm 11). The game's altars and
    /// ritual places (Ideology's altars, ideograms, lecterns and slabs, Alpha Memes' and Vanilla Ideology Expanded - Memes
    /// and Structures' alike: anything with a ritual focus), its art (sculptures, statues), Royalty's nature shrines, and an
    /// ancient terminal of the archotechs can each be dedicated to a god: a button on the place asks which god, and a
    /// colonist (the priest first, else one who holds that god, else anyone free) walks over and says the words there. Only
    /// then is it the god's: its name on the place, a small light where the gods are felt, the god pleased. Taken from one
    /// god for another, the first is displeased. In the colony's age it is a shrine, a chapel, a church or a terminal; one
    /// standing in a fine room indoors is a temple.
    /// </summary>
    public static class Shrines
    {
        private static readonly HashSet<string> Named = new HashSet<string>
        {
            "NatureShrine_Small", "NatureShrine_Large", "AncientTerminal_Worshipful", "IncenseShrine", "Reliquary"
        };

        /// <summary>The colony's own building that can be a god's place: a ritual focus, art, a nature shrine, a worshipful terminal, or anything called an altar or a shrine.</summary>
        public static bool Is(Thing t)
        {
            if (!(t is Building b) || b.def == null || !b.Spawned || b.Faction != Faction.OfPlayer) return false;
            var def = b.def;
            if (def.IsFrame || def.IsBlueprint) return false;
            if (def.ritualFocus != null || def.IsArt || Named.Contains(def.defName)) return true;
            string n = def.defName;
            return n.IndexOf("Altar", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Shrine", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>The god a place is dedicated to (or about to be), or null.</summary>
        public static God GodAt(Thing t)
        {
            var r = Worship.Book?.ShrineOf(t);
            return r == null ? null : Gods.ById(r.god);
        }

        /// <summary>The god's dedicated places at a home (the words said, still standing).</summary>
        public static List<ShrineRecord> Of(Map map, God g)
        {
            var b = Worship.Book;
            if (b == null || map == null) return new List<ShrineRecord>();
            return b.shrines.Where(s => s != null && !s.pending && s.Live && s.thing.Map == map && (g == null || s.god == g.id)).ToList();
        }

        /// <summary>The god's dedicated place nearest to someone (who can reach it), or null.</summary>
        public static Thing Nearest(Pawn p, God g)
        {
            if (p?.Map == null || g == null) return null;
            Thing best = null;
            float bestD = float.MaxValue;
            foreach (var s in Of(p.Map, g))
            {
                float d = s.thing.Position.DistanceToSquared(p.Position);
                if (d >= bestD || !p.CanReach(s.thing, PathEndMode.Touch, Danger.Some)) continue;
                best = s.thing;
                bestD = d;
            }
            return best;
        }

        /// <summary>A dedicated place is a temple: indoors in a fine room (WorshipMath.IsTemple).</summary>
        public static bool IsTemple(Thing t)
        {
            if (t?.Map == null || !t.Spawned) return false;
            try
            {
                var room = t.GetRoom();
                if (room == null || room.PsychologicallyOutdoors || !room.ProperRoom) return false;
                return WorshipMath.IsTemple(true, room.GetStat(RoomStatDefOf.Impressiveness), room.CellCount);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The god's temple at a home, or null.</summary>
        public static Thing Temple(Map map, God g) => Of(map, g).Select(s => s.thing).FirstOrDefault(IsTemple);

        /// <summary>The gods worth dedicating to from here: the colony's own first, then those it knows (no dead god).</summary>
        public static List<God> Choices(Map map)
        {
            var list = new List<God>();
            foreach (var g in Gods.ColonyGods(map))
                if (!list.Contains(g)) list.Add(g);
            var patron = Gods.Patron(map);
            if (patron != null && !list.Contains(patron)) list.Add(patron);
            foreach (var g in Gods.All.Where(x => x.known && x.kind != GodKind.Dead).OrderByDescending(x => Gods.FavourValue(x, map)))
                if (!list.Contains(g)) list.Add(g);
            return list.Take(16).ToList();
        }

        // ================= Asking and dedicating =================

        /// <summary>The player asks for a place to be dedicated to a god: someone will walk over and say the words.</summary>
        public static void Ask(Thing t, God g)
        {
            var b = Worship.Book;
            if (b == null || t == null || g == null) return;
            var r = b.ShrineOf(t);
            if (r != null && !r.pending && r.god == g.id) return;
            if (r == null)
            {
                b.shrines.Add(r = new ShrineRecord { thing = t });
                b.Changed();
            }
            else if (!r.pending) r.before = r.god;
            r.god = g.id;
            r.pending = true;
            r.asked = Worship.Now;
            Messages.Message("CC_Wsh_DedicateAsked".Translate(t.LabelShort.Named("PLACE"), Gods.FullName(g).Named("GOD")).Resolve(), t, MessageTypeDefOf.SilentInput, false);
        }

        /// <summary>The player takes a place from its god: it is the god's no longer, and the god notices.</summary>
        public static void Undedicate(Thing t)
        {
            var b = Worship.Book;
            var r = b?.ShrineOf(t);
            if (r == null) return;
            int god = r.pending ? r.before : r.god;
            b.shrines.Remove(r);
            b.Changed();
            var g = Gods.ById(god);
            if (g != null)
                Gods.AddFavour(g, t.MapHeld, WorshipMath.RededicateFavour, "CC_Wsh_Why_Undedicated".Translate(t.LabelShort.Named("PLACE")).Resolve(), FavourSource.Neglect);
        }

        /// <summary>The region's hour: a place waiting to be dedicated gets someone to go and say the words; too long waiting, it is let go.</summary>
        public static void Hourly(WorshipBook b, Map map, int now)
        {
            foreach (var r in b.shrines.Where(s => s != null && s.pending).ToList())
            {
                if (!r.Live || r.thing.Map != map) continue;
                if (now - r.asked > WorshipMath.DedicateWaitDays * GenDate.TicksPerDay)
                {
                    LetGo(b, r, map);
                    continue;
                }
                var g = Gods.ById(r.god);
                if (g == null) continue;
                // Someone already on their way.
                if (map.mapPawns.FreeColonistsSpawned.Any(p => p.CurJobDef?.defName == WorshipJobs.DedicateJob && p.CurJob.targetA.Thing == r.thing)) continue;
                var who = Dedicator(map, g, r.thing);
                if (who == null) continue;
                WorshipJobs.Start(who, WorshipJobs.At(WorshipJobs.DedicateJob, r.thing, Rand.Range(500, 800)));
            }
        }

        private static void LetGo(WorshipBook b, ShrineRecord r, Map map)
        {
            if (r.before >= 0)
            {
                r.god = r.before;
                r.before = -1;
                r.pending = false;
            }
            else
            {
                b.shrines.Remove(r);
                b.Changed();
            }
            Tell.Minor(NewsKind.Colony, Importance.Flavor, map, "CC_Wsh_DedicateLetGo".Translate(r.thing.LabelShort.Named("PLACE")).Resolve(), r.thing);
        }

        /// <summary>Who goes to dedicate it: the priest, else the free colonist who holds that god most surely, else anyone free (never a scoffer).</summary>
        private static Pawn Dedicator(Map map, God g, Thing t)
        {
            var priest = Worship.Priest(map);
            if (priest != null && priest.Map == map && WorshipJobs.Free(priest) && priest.CanReach(t, PathEndMode.Touch, Danger.Some)) return priest;
            Pawn best = null;
            float bestScore = float.MinValue;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (!Worship.Ours(p) || p.DevelopmentalStage.Juvenile() || Worship.Scoffer(p) || !WorshipJobs.Free(p)) continue;
                if (!p.CanReach(t, PathEndMode.Touch, Danger.Some)) continue;
                float s = (Worship.GodOf(p) == g ? 10f : 0f) + (Worship.Devout(p) ? 5f : 0f) - p.Position.DistanceTo(t.Position) / 20f;
                if (s <= bestScore) continue;
                best = p;
                bestScore = s;
            }
            return best;
        }

        /// <summary>The dedicator has walked over: their words to the god, said there.</summary>
        public static void Arrive(Pawn p, Thing t)
        {
            var g = GodAt(t);
            if (g == null) return;
            Worship.Say(p, "Rimfolk_Pool_Worship_Dedicate", g);
            Worship.PriestLight(p, t.Position);
        }

        /// <summary>The words are said: the place is the god's, the god pleased (the one it was taken from, not).</summary>
        public static void Dedicated(Pawn p, Thing t)
        {
            var b = Worship.Book;
            var r = b?.ShrineOf(t);
            if (r == null || !r.pending || !r.Live) return;
            var g = Gods.ById(r.god);
            if (g == null) return;
            var map = t.Map;
            r.pending = false;
            r.by = p;
            r.byName = p?.LabelShort;
            r.since = Worship.Now;
            var old = r.before >= 0 && r.before != r.god ? Gods.ById(r.before) : null;
            r.before = -1;
            string place = Worship.ShrineOf(g);
            string why = "CC_Wsh_Why_Dedicated".Translate((p?.LabelShort ?? "?").Named("PAWN"), t.LabelShort.Named("PLACE"), place.Named("SHRINE")).Resolve();
            Worship.Favour(g, map, WorshipMath.DedicateFavour, why, FavourSource.Rite);
            if (old != null)
                Gods.AddFavour(old, map, WorshipMath.RededicateFavour, "CC_Wsh_Why_TakenFrom".Translate(t.LabelShort.Named("PLACE"), g.name.Named("OTHER")).Resolve(),
                    FavourSource.Neglect, GodOffence.Rivals);
            Worship.Glow(map, t.Position, g);
            string text = ("CC_Wsh_Dedicated_" + Worship.Step).Translate((p?.LabelShort ?? "?").Named("PAWN"), t.LabelShort.Named("PLACE"), Gods.FullName(g).Named("GOD"),
                place.Named("SHRINE")).Resolve();
            Tell.Minor(NewsKind.Colony, Importance.Notable, map, text, t);
            Gods.Remember(new GodAct { god = g.id, home = Gods.HomeKey(map), kind = GodActKind.Mark, text = why, cell = t.Position });
            if (p != null) CouncilGameComponent.Instance?.journals.WriteText(p, Worship.Line("Rimfolk_Pool_Worship_DedicateJournal", p, g) ?? why);
        }

        // ================= The button and the inspect line =================

        internal static Gizmo GizmoFor(Thing t)
        {
            if (!Worship.On || !Is(t)) return null;
            var map = t.Map;
            var r = Worship.Book?.ShrineOf(t);
            var g = r != null ? Gods.ById(r.god) : null;
            string label = r == null ? "CC_Wsh_Gizmo_Dedicate".Translate().Resolve()
                : r.pending ? "CC_Wsh_Gizmo_Pending".Translate(Gods.Name(g).Named("GOD")).Resolve()
                : "CC_Wsh_Gizmo_Dedicated".Translate(Gods.Name(g).Named("GOD")).Resolve();
            string desc = "CC_Wsh_Gizmo_Desc".Translate(Worship.ShrineWord.Named("SHRINE")).Resolve();
            if (g != null) desc += "\n\n" + "CC_Wsh_Gizmo_Of".Translate(Gods.FullName(g).Named("GOD"), Gods.AGodOf(g).Named("GODOF"), GodWords.Word(Gods.Favour(g, map)).Named("WORD")).Resolve();
            var tex = g != null ? GodSymbols.For(g.domain) : GodSymbols.Pantheon;
            var cmd = new Command_Action
            {
                defaultLabel = label,
                defaultDesc = desc,
                icon = tex ?? BaseContent.BadTex,
                defaultIconColor = g?.colour ?? Color.white,
                action = () => Menu(t)
            };
            var choices = Choices(map);
            if (choices.Count == 0) cmd.Disable("CC_Wsh_Gizmo_NoGods".Translate());
            return cmd;
        }

        private static void Menu(Thing t)
        {
            var map = t.Map;
            var r = Worship.Book?.ShrineOf(t);
            var opts = new List<FloatMenuOption>();
            var mine = Gods.ColonyGods(map);
            foreach (var g in Choices(map))
            {
                var god = g;
                string label = "CC_Wsh_Menu_God".Translate(Gods.FullName(god).Named("GOD"), Gods.GodOf(god).Named("GODOF"),
                    (mine.Contains(god) ? "CC_Wsh_Menu_Ours" : "CC_Wsh_Menu_Known").Translate().Named("WHOSE")).Resolve();
                if (r != null && r.god == god.id) label += " " + "CC_Wsh_Menu_Now".Translate();
                if (god.kind == GodKind.Archotech) label += " " + "CC_Wsh_Menu_Archotech".Translate();
                opts.Add(new FloatMenuOption(label, () => Ask(t, god), GodSymbols.For(god.domain), god.colour));
            }
            if (r != null) opts.Add(new FloatMenuOption("CC_Wsh_Menu_Undedicate".Translate(), () => Undedicate(t)));
            if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts));
        }

        internal static string InspectLine(Thing t)
        {
            if (!Worship.On) return null;
            var r = Worship.Book?.ShrineOf(t);
            if (r == null) return null;
            var g = Gods.ById(r.god);
            if (g == null) return null;
            if (r.pending) return "CC_Wsh_Inspect_Pending".Translate(Gods.FullName(g).Named("GOD")).Resolve();
            string line = (IsTemple(t) ? "CC_Wsh_Inspect_Temple" : "CC_Wsh_Inspect").Translate(Gods.FullName(g).Named("GOD"), Worship.ShrineWord.Named("SHRINE"),
                GodWords.Word(Gods.Favour(g, t.Map)).Named("WORD")).Resolve();
            var waiting = Worship.Book.offerings.FirstOrDefault(o => o != null && o.set && o.fate == OfferingFate.Waiting && o.shrine == t);
            if (waiting != null) line += "\n" + "CC_Wsh_Inspect_Offering".Translate((waiting.label ?? "?").Named("WHAT")).Resolve();
            return line;
        }
    }

    /// <summary>The button on a place that can be a god's: dedicate it, or see whose it is (Gods/Worship/Shrines.cs).</summary>
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetGizmos))]
    public static class Patch_ShrineGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, ThingWithComps __instance)
        {
            if (__result != null)
                foreach (var g in __result)
                    yield return g;
            Gizmo more = null;
            if (RimfolkPatches.On && __instance is Building && __instance.Faction == Faction.OfPlayer)
                try
                {
                    more = Shrines.GizmoFor(__instance);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("worship: the shrine's button", e);
                }
            if (more != null) yield return more;
        }
    }

    /// <summary>A dedicated place says whose it is (Gods/Worship/Shrines.cs).</summary>
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString))]
    public static class Patch_ShrineInspect
    {
        public static void Postfix(ThingWithComps __instance, ref string __result)
        {
            if (!RimfolkPatches.On || !(__instance is Building)) return;
            try
            {
                string line = Shrines.InspectLine(__instance);
                if (line.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? line : __result + "\n" + line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: the shrine's line", e);
            }
        }
    }
}
