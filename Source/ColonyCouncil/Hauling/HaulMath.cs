using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of carrying and hauling (Hauling/; DESIGN.md "Carrying and hauling"), with no game types so
    /// Tools/Tests can check them: how much fits in a pawn's inventory, when a load is worth a trip and when it has been
    /// carried long enough, the long material run (how far counts as far, the build area round the target, how much more
    /// to take), what counts as on the way, how much of a stack goes to storage and how much the pawn keeps, and the
    /// hand-over from the player's standalone Harvest and Haul mod (its settings file and the loads in a save).
    /// </summary>
    public static class HaulMath
    {
        public const int HourTicks = 2500;

        // ================= Harvest and haul =================

        /// <summary>What a pawn's work drops lands on its cell or next to it; the search looks two cells wider.</summary>
        public const int SearchMargin = 2;
        /// <summary>After a delivery fails (no storage, no way there), no new one is tried for ten seconds.</summary>
        public const int DeliveryFailCooldownTicks = 600;
        /// <summary>A deconstruction's leftovers are hauled "right away" only within an hour and this distance of the pawn.</summary>
        public const int HaulNowWindowTicks = HourTicks;
        public const float HaulNowMaxDistance = 25f;
        /// <summary>The stack that didn't fit is carried in the hands on the delivery only while it lies this close.</summary>
        public const float MaxLeftoverDistance = 15f;

        /// <summary>Free mass under the fill limit: capacity times the share that may be filled, less what is carried.</summary>
        public static float FreeMass(float capacity, float fill, float carried) => capacity * Clamp(fill, 0f, 1f) - carried;

        /// <summary>How many of a stack fit into the free mass (all of it when the thing weighs nothing).</summary>
        public static int CountThatFits(int stackCount, float unitMass, float freeMass)
        {
            if (stackCount <= 0) return 0;
            if (unitMass <= 0.0001f) return stackCount;
            if (freeMass <= 0f) return 0;
            return Math.Min(stackCount, (int)Math.Floor(freeMass / unitMass + 0.0001f));
        }

        /// <summary>A full pawn stops work for a trip to storage only once the load has some weight.</summary>
        public static bool WorthATrip(float mass, float minTripMass) => mass >= minTripMass;

        /// <summary>A load carried along while the same kind of work goes on is delivered after this many hours anyway.</summary>
        public static bool HoldExpired(int startedTick, int now, float maxHoldHours) =>
            startedTick >= 0 && now - startedTick > maxHoldHours * HourTicks;

        /// <summary>
        /// A load held three times the longest hold, and a day at least, has waited too long for the job choice to send it
        /// (the player's game, 7 November 2026: a grower with tuberculosis carried 68 logs for three days, his pack full, so
        /// every harvest after fell to the ground): it is queued for storage between jobs, whatever comes next.
        /// </summary>
        public static bool Stuck(int startedTick, int now, float maxHoldHours) =>
            startedTick >= 0 && now - startedTick > Math.Max(24f, maxHoldHours * 3f) * HourTicks;

        // ================= The long material run =================

        public const float RunDistanceDefault = 30f, RunDistanceMin = 15f, RunDistanceMax = 80f;
        /// <summary>The game's own reach for "nearby" frames and blueprints when it fills the hands (WorkGiver_ConstructDeliverResources).</summary>
        public const float NearbyNeeders = 8f;
        /// <summary>A run with no pickup or delivery for two hours ends, and what is left goes to storage.</summary>
        public const int RunIdleTicks = 2 * HourTicks;
        /// <summary>A runner's stock keeps other builders from the stockpile only while it was used within the last hour.</summary>
        public const int CoverTicks = HourTicks;

        /// <summary>
        /// Far: the straight distance from the material to the frame or blueprint it is for. It is the shortest the walk
        /// can be, it costs nothing to work out while the game looks for work, and a wall in the way only makes the walk
        /// longer, so a run never starts for a trip that is short in truth.
        /// </summary>
        public static bool Far(float distance, float threshold) => threshold > 0f && distance >= threshold;

        /// <summary>
        /// The build area: frames and blueprints within this many cells of the one being built, half the run's distance
        /// and never less than the game's own 8 or more than 15, so the area never reaches back to the stockpile.
        /// </summary>
        public static float RunRadius(float threshold) => Clamp(threshold / 2f, NearbyNeeders, 15f);

        /// <summary>How many more to take into the inventory: what the build area still needs, less what the hands carry and what is already in the inventory.</summary>
        public static int Extra(int neededAround, int hands, int inInventory) =>
            Math.Max(0, neededAround - Math.Max(0, hands) - Math.Max(0, inInventory));

        /// <summary>A runner's stock covers a frame for the other builders while it holds all the build area still needs of that material.</summary>
        public static bool Covered(int stock, int areaNeed) => stock > 0 && stock >= areaNeed;

        /// <summary>The run is over when the build area needs none of it any more or it went unused for two hours.</summary>
        public static bool RunOver(int areaNeed, int lastUsedTick, int now) => areaNeed <= 0 || lastUsedTick < 0 || now - lastUsedTick > RunIdleTicks;

        // ================= On the way =================

        /// <summary>At most six cells out of the way, there and back again.</summary>
        public const float MaxDetour = 6f;
        /// <summary>At most six things picked up on one walk.</summary>
        public const int MaxOnTheWay = 6;
        /// <summary>Only on a walk of at least twelve cells: on a short one everything is "on the way".</summary>
        public const float MinWalk = 12f;
        /// <summary>A walk that found nothing it could pick up looks again only after ten seconds.</summary>
        public const int OnTheWayCooldownTicks = 600;

        /// <summary>The extra walk to pick something up: there and on to the destination, less the straight walk.</summary>
        public static float Detour(float toThing, float thingToEnd, float straight) => toThing + thingToEnd - straight;

        public static bool OnTheWay(float toThing, float thingToEnd, float straight) =>
            straight >= MinWalk && Detour(toThing, thingToEnd, straight) <= MaxDetour;

        // ================= What goes to storage and what the pawn keeps =================

        /// <summary>
        /// How much of what Rimfolk put in the inventory goes to storage: never more than the pawn still has, and never
        /// what it should keep (the drugs its drug policy carries, its inventory stock, the packed food it wants).
        /// </summary>
        public static int Deliverable(int owed, int have, int keep) => Math.Max(0, Math.Min(owed, have - Math.Max(0, keep)));

        /// <summary>A stack is handed whole to Pick Up And Haul's unloading only when all of it is Rimfolk's and none of it is kept.</summary>
        public static bool WhollyOurs(int owed, int have, int keep) => have > 0 && keep <= 0 && owed >= have;

        /// <summary>
        /// Packed food the pawn keeps, as the game's own unloading does: as many as fit within its food need's top
        /// (less the other packed food it carries), never more than it has.
        /// </summary>
        public static int FoodKept(int have, float nutritionEach, float otherPackedNutrition, float maxLevel)
        {
            if (have <= 0 || nutritionEach <= 0f) return 0;
            float room = maxLevel - Math.Max(0f, otherPackedNutrition);
            if (room <= 0f) return 0;
            return Math.Min(have, (int)Math.Floor(room / nutritionEach + 0.0001f));
        }

        // ================= The standalone mod's hand-over =================

        /// <summary>The standalone Harvest and Haul's settings, by its keys, and the Rimfolk settings they become.</summary>
        public static readonly Dictionary<string, string> StandaloneKeys = new Dictionary<string, string>
        {
            ["enabled"] = "haulOwnWork",
            ["collectPlants"] = "haulPlants",
            ["collectMining"] = "haulMining",
            ["collectDeepDrill"] = "haulDeepDrill",
            ["collectAnimalProducts"] = "haulAnimals",
            ["collectDeconstruction"] = "haulDeconstruct",
            ["haulDeconstructionLeftovers"] = "haulLeftoversNow",
            ["collectFishing"] = "haulFishing",
            ["collectChunks"] = "haulChunks",
            ["fillPercent"] = "haulFill",
            ["minTripMass"] = "haulMinTrip",
            ["maxHoldHours"] = "haulMaxHours",
            ["deliverBeforeNeeds"] = "haulBeforeNeeds",
            ["includeMechanoids"] = "haulMechs",
            ["verboseLogging"] = "haulLog"
        };

        /// <summary>
        /// The values in the standalone mod's settings file (Mod_..._HarvestAndHaulMod.xml in the game's Config folder),
        /// by Rimfolk's setting: only those it names (the game leaves a value at its default out of the file), only
        /// those that read as what they are. Empty for anything that isn't that file.
        /// </summary>
        public static Dictionary<string, string> ReadStandaloneSettings(string xml)
        {
            var found = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(xml)) return found;
            XmlDocument doc;
            try
            {
                doc = new XmlDocument();
                doc.LoadXml(xml);
            }
            catch (XmlException)
            {
                return found;
            }
            XmlNode settings = doc.DocumentElement?.SelectSingleNode("ModSettings");
            string cls = settings?.Attributes?["Class"]?.Value;
            if (settings == null || cls == null || !cls.EndsWith("HarvestAndHaulSettings", StringComparison.Ordinal)) return found;
            foreach (XmlNode child in settings.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element || !StandaloneKeys.TryGetValue(child.Name, out var field)) continue;
                string value = child.InnerText.Trim();
                bool isBool = bool.TryParse(value, out _);
                bool isNumber = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && !float.IsNaN(f) && !float.IsInfinity(f);
                bool wantsNumber = field == "haulFill" || field == "haulMinTrip" || field == "haulMaxHours";
                if (wantsNumber ? isNumber : isBool) found[field] = value;
            }
            return found;
        }

        /// <summary>A standalone setting's value brought onto Rimfolk's slider: the fill share 10% to 100%, the trip 0 to 30 kg, the hold 1 to 24 hours.</summary>
        public static float ImportNumber(string field, float value)
        {
            switch (field)
            {
                case "haulFill": return Clamp(value, 0.1f, 1f);
                case "haulMinTrip": return Clamp(value, 0f, 30f);
                case "haulMaxHours": return Clamp(value, 1f, 24f);
                default: return value;
            }
        }

        /// <summary>One pawn's load as the standalone mod saved it: who carries it (the pawn's load id), what is owed, the work it came from.</summary>
        public sealed class StandaloneLoad
        {
            public string pawnId;
            public readonly Dictionary<string, int> owed = new Dictionary<string, int>();
            public readonly List<string> skills = new List<string>();
            public int startedTick = -1;
            public bool full;
        }

        /// <summary>The standalone tracker's class in a save; its loads are read from the save itself once the mod is gone.</summary>
        public const string StandaloneTrackerClass = "HarvestAndHaul.HarvestTracker";

        /// <summary>
        /// The standalone mod's tracker in a save's game components (a dictionary of pawn references and loads, each with
        /// its owed counts by def), read without the mod: the loads with a pawn and something owed.
        /// </summary>
        public static List<StandaloneLoad> ReadStandaloneLoads(XmlNode tracker)
        {
            var result = new List<StandaloneLoad>();
            XmlNode loads = tracker?.SelectSingleNode("loads");
            if (loads == null) return result;
            var keys = Lis(loads.SelectSingleNode("keys"));
            var values = Lis(loads.SelectSingleNode("values"));
            for (int i = 0; i < keys.Count && i < values.Count; i++)
            {
                string id = keys[i].InnerText.Trim();
                if (id.Length == 0 || id == "null") continue;
                var load = new StandaloneLoad { pawnId = id };
                XmlNode owed = values[i].SelectSingleNode("owed");
                var defs = Lis(owed?.SelectSingleNode("keys"));
                var counts = Lis(owed?.SelectSingleNode("values"));
                for (int j = 0; j < defs.Count && j < counts.Count; j++)
                {
                    string def = defs[j].InnerText.Trim();
                    if (def.Length == 0 || def == "null" || !int.TryParse(counts[j].InnerText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n <= 0) continue;
                    load.owed[def] = (load.owed.TryGetValue(def, out int had) ? had : 0) + n;
                }
                foreach (var s in Lis(values[i].SelectSingleNode("skills")))
                {
                    string skill = s.InnerText.Trim();
                    if (skill.Length > 0 && skill != "null" && !load.skills.Contains(skill)) load.skills.Add(skill);
                }
                if (int.TryParse(values[i].SelectSingleNode("startedTick")?.InnerText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int started))
                    load.startedTick = started;
                load.full = string.Equals(values[i].SelectSingleNode("full")?.InnerText.Trim(), "True", StringComparison.OrdinalIgnoreCase);
                if (load.owed.Count > 0) result.Add(load);
            }
            return result;
        }

        /// <summary>The standalone tracker's node among a save's game components (the list itself, or the game node that holds it), or null.</summary>
        public static XmlNode FindStandaloneTracker(XmlNode near)
        {
            for (XmlNode n = near; n != null; n = n.ParentNode)
            {
                XmlNode list = n.Name == "components" ? n : n.SelectSingleNode("components");
                if (list == null) continue;
                foreach (XmlNode li in list.ChildNodes)
                    if (li.NodeType == XmlNodeType.Element && li.Attributes?["Class"]?.Value == StandaloneTrackerClass)
                        return li;
                return null;
            }
            return null;
        }

        private static List<XmlNode> Lis(XmlNode list)
        {
            var r = new List<XmlNode>();
            if (list == null) return r;
            foreach (XmlNode li in list.ChildNodes)
                if (li.NodeType == XmlNodeType.Element && li.Name == "li")
                    r.Add(li);
            return r;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
