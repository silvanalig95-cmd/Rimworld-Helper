using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    [DefOf]
    public static class BodyWhenItDiesDefOf
    {
        /// <summary>The mark over a bleeding animal whose body is to be taken when it dies (Defs/Misc/Rimfolk_BodyWhenItDies.xml).</summary>
        public static DesignationDef Rimfolk_TakeBodyWhenDead;

        static BodyWhenItDiesDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(BodyWhenItDiesDefOf));
    }

    /// <summary>
    /// Take a bleeding wild animal's body when it dies (the player's request, 8 November 2026; the rules in
    /// <see cref="BodyWhenItDiesMath"/>). A strong animal bleeding out can be left to die instead of hunted:
    /// <list type="bullet">
    /// <item>The button "Take the body when it dies" on a living animal that isn't the colony's, while it bleeds. It puts
    /// Rimfolk's own mark on it (<see cref="BodyWhenItDiesDefOf.Rimfolk_TakeBodyWhenDead"/>, a designation: it shows over
    /// the animal with the game's haul icon, the game saves it and its Cancel tool takes it off), in place of a hunting or
    /// taming order, as the game's own orders on an animal replace each other. The tooltip says whether it will bleed to
    /// death and about when, by the health tab's figure and the game's rules for wounds that stop bleeding with age.</item>
    /// <item>While it lives nothing is done: no hunt and no haul (the colony's pets leave it alone too: PetHunts).</item>
    /// <item>When it dies, however it dies, the mark is carried across <see cref="Pawn.Kill"/>
    /// (<see cref="Patch_BodyWhenItDiesKill"/>: read before the kill, since the game removes every order on the animal as
    /// it is destroyed, after the body is down): the body is unforbidden and given the game's own haul order, so a hauler
    /// takes it to storage by the usual rules. With no storage on the map that takes it, the player is told once.</item>
    /// <item>Cancelled on its own, with one short message naming the animal and why, when it stops bleeding (its wounds aged
    /// out, were tended or healed), is tamed or joins the colony, or leaves the map: looked at on the wild's short pass
    /// (Beasts/BeastBook.cs, every 250 ticks), no slot of its own.</item>
    /// </list>
    /// Nothing saved of its own: the mark is the game's designation; which marked animals were wild is session memory,
    /// filled again from the marks after a load. The setting "Take a bleeding animal's body when it dies" (What's on,
    /// Strangers and creatures) and the deeper hooks: off, the button is gone and marks left are dropped without a word.
    /// </summary>
    public static class BodyWhenItDies
    {
        /// <summary>The setting and the deeper hooks (the kill needs the patch).</summary>
        public static bool On => (ColonyCouncilMod.Settings?.bodyWhenItDies ?? true) && RimfolkPatches.On && Current.Game != null;

        private static DesignationDef Mark => BodyWhenItDiesDefOf.Rimfolk_TakeBodyWhenDead;

        private struct Watch
        {
            public Map map;
            public bool wasWild;
        }

        /// <summary>The marked animals, with the map they were marked on and whether they were wild (session memory).</summary>
        private static readonly Dictionary<Pawn, Watch> watched = new Dictionary<Pawn, Watch>();
        private static Game watchedGame;
        private static Texture2D icon;

        /// <summary>Its order is on it, on the map that holds it.</summary>
        public static bool Marked(Pawn a)
        {
            var def = Mark;
            var dm = a?.MapHeld?.designationManager;
            return def != null && dm != null && dm.DesignationOn(a, def) != null;
        }

        // ---------------- The button ----------------

        public static Gizmo GizmoFor(Pawn a)
        {
            if (a?.RaceProps == null || !a.RaceProps.Animal || a.Faction == Faction.OfPlayer || !a.Spawned) return null;
            if (!On || Mark == null) return null;
            float bleed = a.health?.hediffSet?.BleedRateTotal ?? 0f;
            if (!BodyWhenItDiesMath.ButtonShows(true, true, false, a.Dead, a.Spawned, bleed, Marked(a))) return null;
            icon ??= ContentFinder<Texture2D>.Get("UI/Designators/Haul", false) ?? BaseContent.BadTex;
            return new Command_Action
            {
                defaultLabel = "CC_BodyMark_Gizmo".Translate(),
                defaultDesc = Describe(a),
                icon = icon,
                activateSound = SoundDefOf.Designate_Haul,
                action = () => Put(a),
                groupKey = 0x52466802
            };
        }

        /// <summary>What the order does, how fast it bleeds and whether it will bleed to death, and about when.</summary>
        public static string Describe(Pawn a)
        {
            var o = OutlookOf(a);
            string what = "CC_BodyMark_GizmoDesc".Translate(a.Named("ANIMAL")).Resolve();
            string rate = "CC_BodyMark_Bleeding".Translate(o.rate.ToStringPercent()).Resolve();
            string key = BodyWhenItDiesMath.OutlookKey(o);
            int ticks = o.Dies ? o.gameTicks : o.closesIn;
            string outlook = ticks == int.MaxValue ? key.Translate().Resolve() : key.Translate(ticks.ToStringTicksToPeriod()).Resolve();
            return what + "\n\n" + rate + " " + outlook;
        }

        /// <summary>The animal's bleeding read the game's way: each wound's share and when it stops on its own.</summary>
        public static BleedOutlook OutlookOf(Pawn a)
        {
            var set = a?.health?.hediffSet;
            if (set == null) return new BleedOutlook { gameTicks = int.MaxValue, diesIn = -1 };
            var rates = new List<float>();
            var left = new List<int>();
            foreach (var h in set.hediffs)
            {
                float r = h.BleedRate;
                if (r <= 0f) continue;
                rates.Add(r);
                if (h is Hediff_Injury inj) left.Add(Math.Max(0, BodyWhenItDiesMath.InjuryStopsAt(inj.Severity) - inj.ageTicks));
                else if (h is Hediff_MissingPart) left.Add(Math.Max(0, BodyWhenItDiesMath.MissingPartFreshTicks - h.ageTicks));
                else left.Add(int.MaxValue);
            }
            float loss = set.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;
            return BodyWhenItDiesMath.Outlook(loss, set.BleedRateTotal, rates, left);
        }

        /// <summary>The order put on: a hunting or taming order gives way, as the game's own orders on an animal do.</summary>
        public static void Put(Pawn a)
        {
            var def = Mark;
            var map = a?.Map;
            if (def == null || map == null || a.Dead) return;
            var dm = map.designationManager;
            if (dm.DesignationOn(a, def) != null) return;
            dm.RemoveAllDesignationsOn(a);
            dm.AddDesignation(new Designation(a, def));
            Fresh();
            watched[a] = new Watch { map = map, wasWild = a.Faction == null };
        }

        // ---------------- The death ----------------

        /// <summary>For the kill's prefix: the map whose order is on this animal, or null (nothing to carry over).</summary>
        public static Map MarkedMap(Pawn a)
        {
            if (!On) return null;
            var def = Mark;
            var map = a?.MapHeld;
            if (def == null || map == null) return null;
            return map.designationManager.DesignationOn(a, def) != null ? map : null;
        }

        /// <summary>
        /// The marked animal is dead: its body unforbidden and marked for hauling (unless it lies in storage already), and the
        /// player told once if no storage on the map takes it.
        /// </summary>
        public static void Died(Pawn a, Map map)
        {
            watched.Remove(a);
            var def = Mark;
            if (def != null) map?.designationManager?.TryRemoveDesignationOn(a, def);
            var corpse = a.Corpse;
            if (corpse == null || corpse.Destroyed || !corpse.Spawned || corpse.Map == null) return;
            var at = corpse.Map;
            var dm = at.designationManager;
            corpse.SetForbidden(false, false);
            bool stored = corpse.IsInValidStorage();
            if (BodyWhenItDiesMath.HaulOrder(stored, dm.DesignationOn(corpse, DesignationDefOf.Haul) != null))
                dm.AddDesignation(new Designation(corpse, DesignationDefOf.Haul));
            bool found = stored || StoreUtility.TryFindBestBetterStorageFor(corpse, null, at, StoreUtility.CurrentStoragePriorityOf(corpse), Faction.OfPlayer,
                out _, out _, false);
            if (BodyWhenItDiesMath.TellNoStorage(stored, found))
                Notify.Message("CC_BodyMark_NoStorage".Translate(a.Named("ANIMAL")).Resolve().CapitalizeFirst(), new LookTargets(corpse), MessageTypeDefOf.NeutralEvent,
                    Importance.Important);
        }

        // ---------------- The short pass ----------------

        private static void Fresh()
        {
            if (watchedGame == Current.Game) return;
            watched.Clear();
            watchedGame = Current.Game;
        }

        /// <summary>
        /// Every 250 ticks (the wild's short pass): marks found after a load join the watched; each watched animal is looked
        /// at, and its order dropped (with a message where it says so) once it stopped bleeding, was tamed, joined the colony
        /// or left the map. With the setting or the hooks off, every mark is dropped without a word.
        /// </summary>
        public static void Rare(int now)
        {
            var def = Mark;
            if (def == null || Find.Maps == null) return;
            Fresh();
            foreach (var map in Find.Maps)
            {
                var marks = map?.designationManager?.designationsByDef[def];
                if (marks == null || marks.Count == 0) continue;
                foreach (var d in marks.ToList())
                {
                    if (!(d.target.Thing is Pawn a))
                    {
                        map.designationManager.RemoveDesignation(d);
                        continue;
                    }
                    if (!watched.ContainsKey(a)) watched[a] = new Watch { map = map, wasWild = a.Faction == null };
                }
            }
            if (watched.Count == 0) return;
            bool on = On;
            foreach (var kv in watched.ToList())
            {
                var a = kv.Key;
                var w = kv.Value;
                // The map itself is gone (a camp left behind): the game dropped its orders with it; nothing to tell.
                if (w.map == null || !Find.Maps.Contains(w.map))
                {
                    watched.Remove(a);
                    continue;
                }
                var dm = w.map.designationManager;
                var des = dm.DesignationOn(a, def);
                bool gone = a.Dead || a.Destroyed || a.Discarded;
                float bleed = gone ? 0f : a.health?.hediffSet?.BleedRateTotal ?? 0f;
                var end = BodyWhenItDiesMath.Judge(on, des != null, gone, a.MapHeld == w.map, a.Faction == Faction.OfPlayer, w.wasWild, bleed);
                if (!BodyWhenItDiesMath.Drop(end)) continue;
                if (des != null) dm.RemoveDesignation(des);
                watched.Remove(a);
                string key = BodyWhenItDiesMath.MessageKey(end);
                if (key == null) continue;
                Notify.Message(key.Translate(a.Named("ANIMAL")).Resolve().CapitalizeFirst(), a.Spawned ? new LookTargets(a) : LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent, Importance.Important);
            }
        }

        /// <summary>Every mark and how each animal is doing, for the dev action's log.</summary>
        public static string Explain()
        {
            var def = Mark;
            if (def == null) return "the designation def is missing";
            var lines = new List<string>();
            foreach (var map in Find.Maps)
                foreach (var d in map.designationManager.designationsByDef[def])
                    if (d.target.Thing is Pawn a)
                    {
                        var o = OutlookOf(a);
                        lines.Add("  " + a.LabelShort + " (" + a.def.defName + "): bleeding " + o.rate.ToStringPercent() + "/day, health tab "
                                  + (o.gameTicks == int.MaxValue ? "not bleeding" : o.gameTicks.ToStringTicksToPeriod()) + ", "
                                  + (o.Dies ? "dies in " + o.diesIn.ToStringTicksToPeriod() : "lives")
                                  + ", wounds close in " + (o.closesIn == int.MaxValue ? "never by age" : o.closesIn.ToStringTicksToPeriod())
                                  + (watched.TryGetValue(a, out var w) ? (w.wasWild ? ", wild" : ", another people's") : ", not yet watched"));
                    }
            return lines.Count == 0 ? "  (no animal is marked)" : string.Join("\n", lines);
        }
    }
}
