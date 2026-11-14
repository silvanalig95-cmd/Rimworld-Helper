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
    /// then is it the god's: its name on the place, a small light (a brighter one when the god is drawn near and shows
    /// itself), the god pleased. Taken from one
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
            // Those the colony's people pray to first, then the others it has heard of by name, in no order of their regard.
            var list = new List<God>();
            foreach (var g in Worship.PrayedTo(map))
                if (!list.Contains(g)) list.Add(g);
            foreach (var g in Gods.All.Where(x => x.known && x.kind != GodKind.Dead).OrderBy(x => x.name))
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
            Messages.Message("CC_Wsh_DedicateAsked".Translate(t.LabelShort.Named("PLACE"), Worship.Called(g).Named("GOD")).Resolve(), t, MessageTypeDefOf.SilentInput, false);
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
                Worship.Favour(g, t.MapHeld, WorshipMath.RededicateFavour, "CC_Wsh_Why_Undedicated".Translate(t.LabelShort.Named("PLACE")).Resolve(), FavourSource.Neglect);
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
                Worship.Favour(old, map, WorshipMath.RededicateFavour, "CC_Wsh_Why_TakenFrom".Translate(t.LabelShort.Named("PLACE"), g.name.Named("OTHER")).Resolve(),
                    FavourSource.Neglect, GodOffence.Rivals);
            bool plain = GodNear.Plain(g, map);
            Worship.Glow(map, t.Position, g, false, plain);
            string text = ("CC_Wsh_Dedicated_" + Worship.Step(plain)).Translate((p?.LabelShort ?? "?").Named("PAWN"), t.LabelShort.Named("PLACE"), Worship.Called(g).Named("GOD"),
                place.Named("SHRINE")).Resolve();
            // The card: what was done, and plainly what the colony's people will do here from now on.
            Tell.Letter(NewsKind.Colony, Importance.Notable, map, place.CapitalizeFirst(), text + "\n\n" + Doings(g, map) + " " + "CC_Wsh_Card_Nobody".Translate(Worship.Called(g).Named("GOD")).Resolve(),
                LetterDefOf.NeutralEvent, t);
            // The colony gave it to this god itself, and knows it: in the colony's book by those who saw the words said.
            Worship.Witness(g, map, GodActKind.Favour, text, t.Position);
            Worship.Booked(g, map, t.Position, text, p);
            if (p != null) CouncilGameComponent.Instance?.journals.WriteText(p, Worship.Line("Rimfolk_Pool_Worship_DedicateJournal", p, g) ?? why);
        }

        /// <summary>
        /// What the colony's people will do at a god's place, in a sentence (WorshipMath.ShrineDoings): leave it small gifts
        /// where they carry any to this god, pray in hard times, keep its feast there where they keep it at this home
        /// (Festivals.GodsFor).
        /// </summary>
        public static string Doings(God g, Map map)
        {
            var parts = new List<string>();
            bool feast = GodWorshipDef.KeepsFestival(g) && map != null && Festivals.GodsFor(map).Contains(g);
            foreach (var d in WorshipMath.ShrineDoings(GodWorshipDef.GiftsLeft(g), feast))
            {
                if (d == "Feast")
                {
                    int season = Mathf.Clamp(Festivals.When(g).season, 0, 3);
                    parts.Add("CC_Wsh_Card_Do_Feast".Translate(Festivals.Name(g).Named("FEAST"), ("CC_Wsh_Card_Season_" + season).Translate().Named("SEASON")).Resolve());
                }
                else parts.Add(("CC_Wsh_Card_Do_" + d).Translate(Worship.Called(g).Named("GOD")).Resolve());
            }
            return "CC_Wsh_Card_Doing".Translate(parts.ToCommaList(true).Named("DOINGS")).Resolve();
        }

        // ================= The button and the inspect line =================

        internal static Gizmo GizmoFor(Thing t)
        {
            if (!Worship.On || !Is(t)) return null;
            var map = t.Map;
            var r = Worship.Book?.ShrineOf(t);
            var g = r != null ? Gods.ById(r.god) : null;
            string label = r == null ? "CC_Wsh_Gizmo_Dedicate".Translate().Resolve()
                : r.pending ? "CC_Wsh_Gizmo_Pending".Translate(Worship.Called(g).Named("GOD")).Resolve()
                : "CC_Wsh_Gizmo_Dedicated".Translate(Worship.Called(g).Named("GOD")).Resolve();
            string desc = "CC_Wsh_Gizmo_Desc".Translate(Worship.ShrineWord.Named("SHRINE")).Resolve();
            if (g != null && r.by != null && !r.pending)
                desc += "\n\n" + "CC_Wsh_Gizmo_Of".Translate(Worship.Called(g).Named("GOD"), (r.byName ?? "?").Named("PAWN")).Resolve() + " " + Doings(g, map);
            // The place's own picture: no god is drawn as a symbol.
            var cmd = new Command_Action
            {
                defaultLabel = label,
                defaultDesc = desc,
                icon = t.def.uiIcon ?? BaseContent.BadTex,
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
            var mine = Worship.PrayedTo(map);
            foreach (var g in Choices(map))
            {
                var god = g;
                string label = "CC_Wsh_Menu_God".Translate(Worship.Called(god).Named("GOD"),
                    (mine.Contains(god) ? "CC_Wsh_Menu_Ours" : "CC_Wsh_Menu_Known").Translate().Named("WHOSE")).Resolve();
                if (r != null && r.god == god.id) label += " " + "CC_Wsh_Menu_Now".Translate();
                opts.Add(new FloatMenuOption(label, () => Ask(t, god)));
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
            if (r.pending) return "CC_Wsh_Inspect_Pending".Translate(Worship.Called(g).Named("GOD")).Resolve();
            string line = (IsTemple(t) ? "CC_Wsh_Inspect_Temple" : "CC_Wsh_Inspect").Translate(Worship.Called(g).Named("GOD"), Worship.ShrineWord.Named("SHRINE"),
                (r.byName ?? "?").Named("PAWN")).Resolve();
            var waiting = Worship.Book.offerings.FirstOrDefault(o => o != null && o.set && o.fate == OfferingFate.Waiting && o.shrine == t);
            if (waiting != null) line += "\n" + "CC_Wsh_Inspect_Offering".Translate((waiting.label ?? "?").Named("WHAT")).Resolve();
            else if (r.lastAt >= 0 && !r.lastWhat.NullOrEmpty() && r.lastFate != OfferingFate.Waiting)
            {
                string ago = "CC_Wsh_Inspect_Ago".Translate(Math.Max(0, Worship.Now - r.lastAt).ToStringTicksToPeriodVague().Named("TIME")).Resolve();
                // What became of it, in the god's own way's words where it has them (a sapling stood there, it lay in the sun all day).
                line += "\n" + "CC_Wsh_Inspect_Last".Translate(r.lastWhat.Named("WHAT"), (r.lastBy ?? "?").Named("PAWN"), ago.Named("AGO"),
                    (OfferingWays.FateKey(g, r.lastFate) ?? "CC_Wsh_Inspect_Fate_" + r.lastFate).Translate().Named("FATE")).Resolve();
            }
            string held = Belief(g, r);
            if (!held.NullOrEmpty()) line += "\n" + held;
            return line;
        }

        /// <summary>
        /// What someone of the colony holds about the god of a place, as its inspect line puts it: the newest "what it loves"
        /// in its chapter of the colony's book, as the one who holds it holds it (possibly wrong); else the words its last
        /// offering was set down with, as they were said. Never the god's own mind.
        /// </summary>
        private static string Belief(God g, ShrineRecord r)
        {
            var lore = GodLore.Instance;
            string chapter = lore?.PageOf(g)?.name;
            var held = chapter == null ? null : lore.verses.LastOrDefault(v => v != null && v.part == BookPart.Loves && !v.against && v.page == chapter && !v.by.NullOrEmpty());
            if (held != null)
                return "CC_Wsh_Inspect_Belief".Translate(held.by.Named("BY"), chapter.Named("GOD"), held.text.Named("ITEM")).Resolve();
            if (!r.lastWords.NullOrEmpty() && !r.lastBy.NullOrEmpty())
                return "CC_Wsh_Inspect_Words".Translate(r.lastBy.Named("PAWN"), r.lastWords.Named("WORDS")).Resolve();
            return null;
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
