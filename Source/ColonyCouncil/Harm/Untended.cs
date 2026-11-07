using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 5: small things left alone that grow, and dirty water (Harm/HarmFive.cs is the hub; each colonist's hour).
    /// <list type="bullet">
    /// <item>Small things that grow (3.2.3): worms (from raw meat or fish) left untended three days become the game's gut
    /// worms one time in 20, in their place; insect bites left a day untended in a malaria country (a land where the
    /// game's own malaria comes) carry the game's malaria one time in 20, half with sleeves (clothes over the arms), half
    /// for one who sleeps indoors (a net, a roof). Cooked meat (no worms at all) and a tending prevent it. Each is looked at
    /// in the one hour it turns that old, so nothing needs remembering. With the everyday ailments.</item>
    /// <item>Dirty water (4.9.3, Dubs Bad Hygiene, read by name): one of the colony who drinks (its thirst need rising)
    /// at a home with no water treatment of the mod's: the stomach bug one such day in 400. Only while the mod's own
    /// illnesses from water are off (read from its settings by name; when they can't be read they count as on), so nothing
    /// is counted twice; nothing at all without the mod or its thirst. With the everyday ailments.</item>
    /// </list>
    /// Each is one of the game's illnesses or Rimfolk's ailments through Harm.Ail, under the cap, with a message as the
    /// game gives for an illness, naming the cause, and a line over the head.
    /// </summary>
    public static class Untended
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Dev mode: the next worms or bites looked at grow, the next drink of untreated water sickens (past their rates, never past the cap).</summary>
        public static bool DebugGrow, DebugWater;

        public static void Reset()
        {
            thirstLast.Clear();
            waterDay.Clear();
            untreated.Clear();
        }

        public static void Hour(Pawn p, int now)
        {
            if (!Harm.On || !HarmFour.Ours(p)) return;
            Grow(p);
            Water(p, now);
        }

        // ================= Small things that grow (3.2.3) =================

        private static IncidentDef malariaIncident;
        private static bool lookedMalaria;

        /// <summary>A land where the game's own malaria comes (its biome lists the disease).</summary>
        private static bool MalariaLand(Map map)
        {
            if (!lookedMalaria)
            {
                lookedMalaria = true;
                malariaIncident = DefDatabase<IncidentDef>.GetNamedSilentFail("Disease_Malaria");
            }
            if (map?.Biome == null || malariaIncident == null) return false;
            try
            {
                return map.Biome.CommonalityOfDisease(malariaIncident) > 0f;
            }
            catch
            {
                return false;
            }
        }

        private static BodyPartGroupDef arms;

        /// <summary>Sleeves: worn clothes over the arms.</summary>
        private static bool Sleeves(Pawn p)
        {
            arms ??= DefDatabase<BodyPartGroupDef>.GetNamedSilentFail("Arms");
            var worn = p.apparel?.WornApparel;
            if (worn == null || arms == null) return false;
            for (int i = 0; i < worn.Count; i++)
                if (worn[i]?.def?.apparel != null && worn[i].def.apparel.CoversBodyPartGroup(arms))
                    return true;
            return false;
        }

        /// <summary>Their bed is indoors (under a roof, a net over it in effect).</summary>
        private static bool SleepsIndoors(Pawn p)
        {
            var bed = p.ownership?.OwnedBed;
            var room = bed?.Spawned == true ? bed.GetRoom() : null;
            return room != null && !room.PsychologicallyOutdoors;
        }

        private static void Grow(Pawn p)
        {
            if (!Harm.SystemOn(HarmKinds.SmallThings.system)) return;
            var worms = HarmFour.Def("Council_Ail_Worms");
            var bites = HarmFour.Def("Council_Ail_InsectBites");
            Hediff wormy = null, bitten = null;
            var hs = p.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (h == null || h.IsTended()) continue;
                if (h.def == worms && (DebugGrow || HarmFiveMath.AgeWindow(h.ageTicks, HarmFiveMath.WormDays))) wormy = h;
                else if (h.def == bites && (DebugGrow || HarmFiveMath.AgeWindow(h.ageTicks, HarmFiveMath.BiteDays))) bitten = h;
            }
            bool debug = DebugGrow;
            if (wormy != null || bitten != null) DebugGrow = false;
            // Each has its roll in its own hour; a hurt from the worms holds the bites back by the cap.
            if (wormy != null && GrowWorms(p, wormy, debug)) return;
            if (bitten != null) GrowBites(p, debug);
        }

        /// <summary>Worms left alone become the game's gut worms, in their place. True when it came.</summary>
        private static bool GrowWorms(Pawn p, Hediff wormy, bool debug)
        {
            var gut = HarmFour.Def("GutWorms");
            if (gut == null || (debug ? !Harm.May(p, HurtClass.Injury) : !Harm.Roll(p, HarmKinds.SmallThings, HarmFiveMath.SmallThingsChance, HurtClass.Injury))) return false;
            var h = Harm.Ail(p, gut, Math.Max(0.05f, gut.initialSeverity), HarmKinds.SmallThings, HurtClass.Injury, wormy);
            if (h == null) return false;
            HarmFour.Say(p, "Rimfolk_Pool_SmallThings", null, "gutWorms");
            HarmFour.TellHurt(p, "CC_HarmFive_GutWorms", p.Named("PAWN"), h.def.label.Named("ILLNESS"));
            return true;
        }

        /// <summary>Bites left alone in a malaria country carry the game's malaria.</summary>
        private static void GrowBites(Pawn p, bool debug)
        {
            var malaria = HarmFour.Def("Malaria");
            float chance = HarmFiveMath.BitesGrow(MalariaLand(p.Map), Sleeves(p), SleepsIndoors(p));
            if (malaria == null || (debug ? !Harm.May(p, HurtClass.Injury) : chance <= 0f || !Harm.Roll(p, HarmKinds.SmallThings, chance, HurtClass.Injury))) return;
            var h = Harm.Ail(p, malaria, Math.Max(0.05f, malaria.initialSeverity), HarmKinds.SmallThings, HurtClass.Injury);
            if (h == null) return;
            HarmFour.Say(p, "Rimfolk_Pool_SmallThings", null, "malaria");
            HarmFour.TellHurt(p, "CC_HarmFive_Malaria", p.Named("PAWN"), h.def.label.Named("ILLNESS"));
        }

        // ================= Dirty water (4.9.3) =================

        private static NeedDef thirst;
        private static bool lookedThirst;

        /// <summary>Dubs Bad Hygiene's thirst, by its defName (only there with the mod, and its thirst on).</summary>
        public static NeedDef Thirst
        {
            get
            {
                if (!lookedThirst)
                {
                    lookedThirst = true;
                    thirst = DefDatabase<NeedDef>.GetNamedSilentFail("DBHThirst");
                }
                return thirst;
            }
        }

        private static bool? ownIllnesses;

        /// <summary>
        /// The mod's own illnesses from water are on: any of its settings named for diseases, contamination or illness that
        /// is set (a "disable" one cleared). When none can be read, they count as on, so nothing is counted twice.
        /// </summary>
        public static bool ModOwnIllnesses
        {
            get
            {
                if (ownIllnesses == null) ownIllnesses = ReadOwnIllnesses();
                return ownIllnesses.Value;
            }
        }

        /// <summary>Dirty water can come: the mod and its thirst are here, and its own illnesses from water are off.</summary>
        public static bool DirtyWaterReady => Thirst != null && !ModOwnIllnesses;

        private static readonly string[] IllnessWords = { "disease", "contamin", "illness", "diarr", "dysent", "cholera" };

        private static bool ReadOwnIllnesses()
        {
            try
            {
                bool found = false, on = false;
                void Look(object instance, Type type)
                {
                    foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | (instance == null ? BindingFlags.Static : BindingFlags.Instance)))
                    {
                        if (f.FieldType != typeof(bool)) continue;
                        string name = f.Name.ToLowerInvariant();
                        if (!IllnessWords.Any(w => name.Contains(w))) continue;
                        found = true;
                        bool value = (bool)f.GetValue(instance);
                        if (name.Contains("disable") || name.StartsWith("no", StringComparison.Ordinal)) value = !value;
                        if (value) on = true;
                    }
                }
                var modSettings = AccessTools.Field(typeof(Mod), "modSettings");
                foreach (var mod in LoadedModManager.ModHandles)
                {
                    var ns = mod?.GetType().Namespace;
                    if (ns == null || !ns.StartsWith("DubsBadHygiene", StringComparison.Ordinal)) continue;
                    var settings = modSettings?.GetValue(mod);
                    if (settings != null) Look(settings, settings.GetType());
                }
                foreach (var type in GenTypes.AllTypes)
                {
                    if (type?.Namespace == null || !type.Namespace.StartsWith("DubsBadHygiene", StringComparison.Ordinal) || type.Name.IndexOf("Settings", StringComparison.Ordinal) < 0) continue;
                    Look(null, type);
                }
                return !found || on;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>A colonist's thingIDNumber: their thirst at the last look, and the day a drink last had its roll.</summary>
        private static readonly Dictionary<int, float> thirstLast = new Dictionary<int, float>();
        private static readonly Dictionary<int, int> waterDay = new Dictionary<int, int>();
        /// <summary>A home map's uniqueID: whether it has no water treatment, and when that was looked at.</summary>
        private static readonly Dictionary<int, (int at, bool untreated)> untreated = new Dictionary<int, (int, bool)>();

        /// <summary>No water treatment of the mod's built at this home (its buildings named for treatment or filtering).</summary>
        private static bool Untreated(Map map)
        {
            int now = Now;
            if (untreated.TryGetValue(map.uniqueID, out var u) && now >= u.at && now - u.at < HarmMath.DayTicks) return u.untreated;
            bool treated = false;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                var d = b?.def;
                string pack = d?.modContentPack?.PackageId;
                if (pack == null || pack.IndexOf("dubwise", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (d.defName.IndexOf("Treatment", StringComparison.OrdinalIgnoreCase) >= 0 || d.defName.IndexOf("Filter", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    treated = true;
                    break;
                }
            }
            untreated[map.uniqueID] = (now, !treated);
            return !treated;
        }

        private static void Water(Pawn p, int now)
        {
            var need = Thirst;
            if (need == null) return;
            var n = p.needs?.TryGetNeed(need);
            if (n == null) return;
            bool had = thirstLast.TryGetValue(p.thingIDNumber, out float last);
            if (thirstLast.Count > 200) thirstLast.Clear();
            thirstLast[p.thingIDNumber] = n.CurLevel;
            bool debug = DebugWater;
            if (!debug && (!had || n.CurLevel - last < HarmFiveMath.DrinkRise)) return;
            if (!Harm.SystemOn(HarmKinds.DirtyWater.system)) return;
            int day = now / HarmMath.DayTicks;
            if (!debug && waterDay.TryGetValue(p.thingIDNumber, out int d) && d == day) return;
            if (waterDay.Count > 200) waterDay.Clear();
            waterDay[p.thingIDNumber] = day;
            DebugWater = false;
            float chance = HarmFiveMath.DirtyWater(true, Untreated(p.Map), ModOwnIllnesses);
            var bug = HarmFour.Def("Council_Ail_StomachBug");
            if (bug == null || p.health.hediffSet.HasHediff(bug) || (debug ? !Harm.May(p, HurtClass.Injury) : chance <= 0f || !Harm.Roll(p, HarmKinds.DirtyWater, chance, HurtClass.Injury))) return;
            var info = AilmentTracker.InfoOf(bug);
            var h = Harm.Ail(p, bug, info?.startSeverity.RandomInRange ?? 0.3f, HarmKinds.DirtyWater, HurtClass.Injury);
            if (h == null) return;
            if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
            HarmFour.Say(p, "Rimfolk_Pool_DirtyWater");
            HarmFour.TellHurt(p, "CC_HarmFive_DirtyWater", p.Named("PAWN"), AilmentTracker.NameOf(bug).Named("ILLNESS"), (AilmentTracker.DoctorsNote(bug) ?? "").Named("NOTE"));
        }

        /// <summary>Dev mode: what Dubs Bad Hygiene is read as.</summary>
        public static string DebugReadWater() =>
            Thirst == null ? "no thirst need (Dubs Bad Hygiene isn't loaded, or its thirst is off): no dirty water."
                : "Dubs Bad Hygiene's thirst is here; its own illnesses from water read as " + (ModOwnIllnesses ? "on (or unreadable): no dirty water of Rimfolk's." : "off: dirty water can come.")
                  + " The next colonist's drink has its roll, past its rate.";
    }
}
