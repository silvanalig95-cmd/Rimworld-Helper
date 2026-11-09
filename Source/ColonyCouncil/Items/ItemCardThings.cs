using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The item card's reading of shells, furniture in a crate and anything else (Rimfolk's look, stage 3, point 5, step 3). A
    /// shell by the projectile it is loaded as (its damage, burst and armour piercing, what it leaves) and the guns whose
    /// storage takes it (every buildable building looked through once a kind), the colony's that fire it with a click to one.
    /// Furniture in a crate read as the piece inside: its comfort, beauty, rest, work speed, power, recreation, a facility's
    /// links, heat and light; at home how many stand and wait in crates, who sleeps without a bed, the recreation the colony
    /// lacks, the room it would lift most (the art card's own sums). Anything else: a modest card of the game's own words and
    /// stats, an unfinished thing's work left and bench, the colony's stock, where it comes from and trade. Rules and words in
    /// Items/ItemCardUseMath.cs.
    /// </summary>
    public static partial class ItemCardLive
    {
        // ---------------------------------------------------------------- shells

        /// <summary>A shell's burst and the guns that fire it (once a kind).</summary>
        private static void ReadShell(ThingDef d, UsePart up)
        {
            var proj = d.projectileWhenLoaded?.projectile;
            if (proj != null)
            {
                up.damage = proj.damageDef?.label ?? "";
                try
                {
                    if (proj.damageDef != null) up.amount = proj.GetDamageAmount((Thing)null);
                    up.penetration = proj.GetArmorPenetration((Thing)null);
                }
                catch
                {
                    // The burst's own numbers can't be read: its kind alone.
                }
                up.radius = proj.explosionRadius;
                up.fire = proj.explosionChanceToStartFire > 0f || proj.ai_IsIncendiary;
                string after = proj.postExplosionSpawnThingDef?.defName ?? "";
                up.foam = after.IndexOf("Foam", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            up.explodesItself = d.GetCompProperties<CompProperties_Explosive>() != null;
            foreach (var t in Avail.AllBuildings)
            {
                try
                {
                    var gun = t.building?.turretGunDef;
                    if (gun == null) continue;
                    var own = t.building.fixedStorageSettings?.filter;
                    var gunOwn = gun.building?.fixedStorageSettings?.filter;
                    if (own != null && own.Allows(d) || gunOwn != null && gunOwn.Allows(d)) up.launchers.Add(t);
                }
                catch
                {
                    // A mod's turret that can't be read: left out.
                }
            }
            up.launcherKind = up.launchers.FirstOrDefault(t => t.IsResearchFinished)?.label ?? up.launchers.FirstOrDefault()?.label ?? "";
        }

        private static void LiveShell(UsePart up, UseFacts u)
        {
            u.Damage = up.damage;
            u.DamageAmount = up.amount;
            u.Radius = up.radius;
            u.Penetration = up.penetration;
            u.Fire = up.fire;
            u.Foam = up.foam;
            u.ExplodesItself = up.explodesItself;
            u.AnyLauncher = up.launchers.Count > 0;
            u.LauncherKind = up.launcherKind;
        }

        /// <summary>The colony's guns that fire it, a click going to one.</summary>
        private static void ColonyShell(ItemCardModel m, UsePart up, UseFacts u)
        {
            u.Held = HeldOf(m.map, m.def);
            u.Launchers = 0;
            u.LauncherWords = "";
            var counts = new Dictionary<ThingDef, int>();
            Thing first = null;
            foreach (var def in up.launchers)
            {
                foreach (var b in m.map.listerBuildings.AllBuildingsColonistOfDef(def))
                {
                    counts[def] = counts.TryGetValue(def, out int n) ? n + 1 : 1;
                    first ??= b;
                }
            }
            u.Launchers = counts.Values.Sum();
            u.LauncherWords = ItemCardMath.AndList(counts.OrderByDescending(kv => kv.Value).Take(3)
                .Select(kv => kv.Value + " " + Find.ActiveLanguageWorker.Pluralize(kv.Key.label ?? kv.Key.defName, kv.Value)).ToList());
            if (first != null)
            {
                var go = first;
                m.useGo.Add(new ItemGoLine { text = W("CC_ItemCard_GoGun", go.LabelCap.Named("GUN")), tip = W("CC_ItemCard_GoThing_Tip"), go = () => GoTo(go), tone = 1 });
            }
        }

        private static void RowsShell(ItemCardModel m, UseFacts u)
        {
            m.use.Clear();
            m.use.Add((W("CC_ItemCard_Row_Damage"), u.Damage.Length == 0 ? W("CC_ItemCard_DamageUnknown")
                : u.DamageAmount > 0 ? W("CC_ItemCard_DamageAmount", ItemCardMath.Cap(u.Damage).Named("DAMAGE"), u.DamageAmount.Named("N")) : ItemCardMath.Cap(u.Damage)));
            if (u.Radius > 0f) m.use.Add((W("CC_ItemCard_Row_Radius"), ItemCardMath.N((float)Math.Round(u.Radius, 1))));
            if (u.Penetration > 0f) m.use.Add((W("CC_ItemCard_Row_Penetration"), u.Penetration.ToStringPercent()));
            if (u.Fire) m.use.Add((W("CC_ItemCard_Row_Leaves"), W("CC_ItemCard_ShellFire")));
            else if (u.Foam) m.use.Add((W("CC_ItemCard_Row_Leaves"), W("CC_ItemCard_ShellFoam")));
            if (u.ExplodesItself) m.use.Add((W("CC_ItemCard_Row_Store"), W("CC_ItemCard_ShellExplodes")));
            if (m.facts.HasColony)
                m.use.Add((W("CC_ItemCard_Row_FiredBy"), u.Launchers > 0 ? W("CC_ItemCard_ShellFiredBy", u.LauncherWords.Named("GUNS"))
                    : u.AnyLauncher ? W("CC_ItemCard_ShellNoGun", (u.LauncherKind.Length > 0 ? Find.ActiveLanguageWorker.WithIndefiniteArticle(u.LauncherKind) : "?").Named("GUN"))
                    : W("CC_ItemCard_ShellNoGunAtAll")));
        }

        // ---------------------------------------------------------------- furniture in a crate

        /// <summary>What the piece is for, by its def (once a kind): a bed, a table, a seat, a bench, recreation, power, a facility, heat and light.</summary>
        private static void ReadFurniture(ThingDef d, UsePart up)
        {
            up.bed = d.IsBed;
            up.table = d.IsTable;
            up.workTable = d.IsWorkTable;
            up.seat = d.building?.isSittable == true;
            up.joyKind = d.building?.joyKind;
            try
            {
                up.recipes = d.AllRecipes?.Count ?? 0;
            }
            catch
            {
                up.recipes = 0;
            }
            var power = d.GetCompProperties<CompProperties_Power>();
            if (power != null)
            {
                try
                {
                    up.power = power.PowerConsumption;
                }
                catch
                {
                    up.power = 0f;
                }
            }
            var facility = d.GetCompProperties<CompProperties_Facility>();
            if (facility != null)
            {
                var offsets = (facility.statOffsets ?? new List<StatModifier>()).Where(s => s?.stat != null).Take(3)
                    .Select(s => s.stat.LabelCap + " " + s.stat.ValueToString(s.value, ToStringNumberSense.Offset)).ToList();
                var links = (facility.linkableBuildings ?? new List<ThingDef>()).Where(b => b != null).Select(b => b.label).Distinct().Take(2).ToList();
                if (offsets.Count > 0)
                    up.facility = string.Join(", ", offsets) + (links.Count > 0 ? " " + W("CC_ItemCard_FacilityTo", ItemCardMath.AndList(links).Named("LINKS")) : "");
            }
        }

        /// <summary>The piece in the crate: its own stats (its quality and stuff), once a card.</summary>
        private static void LiveFurniture(ItemCardModel m, UsePart up, UseFacts u)
        {
            var inner = m.inner ?? m.thing;
            u.Bed = up.bed;
            u.Table = up.table;
            u.WorkTable = up.workTable;
            u.Seat = up.seat;
            u.JoyKind = up.joyKind?.label ?? "";
            u.Power = up.power;
            u.Facility = up.facility;
            u.Quality = inner != null && inner.TryGetQuality(out var q) ? q.GetLabel() : "";
            try
            {
                if (inner != null)
                {
                    u.Beauty = inner.GetStatValue(StatDefOf.Beauty);
                    u.Comfort = up.seat || up.bed ? inner.GetStatValue(StatDefOf.Comfort) : 0f;
                    u.Rest = up.bed ? inner.GetStatValue(StatDefOf.BedRestEffectiveness) : 0f;
                    u.WorkSpeed = up.workTable ? inner.GetStatValue(StatDefOf.WorkTableWorkSpeedFactor) : 1f;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card reading a crate's piece", e);
            }
        }

        /// <summary>At home: how many stand and wait in crates, who sleeps without a bed, the recreation the colony lacks, the room it would lift most.</summary>
        private static void ColonyFurniture(ItemCardModel m, UsePart up, UseFacts u, List<Pawn> people)
        {
            var map = m.map;
            var d = m.def;
            u.Bedless = 0;
            u.BedlessWho = u.BestRoom = u.BestStageNow = u.BestStageWith = "";
            u.BestIndexNow = u.BestIndexWith = 0;
            u.JoyNew = false;
            m.rooms.Clear();
            int placed = map.listerBuildings.AllBuildingsColonistOfDef(d).Count();
            int crated = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing))
                if (t is MinifiedThing mt && mt.InnerThing?.def == d && !t.IsForbidden(Faction.OfPlayer)) crated++;
            u.Held = crated;
            u.Placed = placed;
            if (up.bed && d.building != null && d.building.bed_humanlike)
            {
                var bedless = people.Where(x => !x.DevelopmentalStage.Baby() && x.ownership != null && x.ownership.OwnedBed == null).Select(x => x.LabelShort).ToList();
                u.Bedless = bedless.Count;
                u.BedlessWho = ItemCardMath.AndList(bedless.Take(3).ToList()) + (bedless.Count > 3 ? " " + W("CC_ItemCard_AndMoreThings", (bedless.Count - 3).Named("N")) : "");
            }
            if (up.joyKind != null)
                u.JoyNew = !map.listerBuildings.allBuildingsColonist.Any(b => b.def.building?.joyKind == up.joyKind);
            // The room it would lift most, by the art card's own sums (the piece's beauty over the room's divisor and its worth).
            if (u.Beauty > 0.5f)
            {
                var a = new ArtFacts { Beauty = u.Beauty, Worth = m.inner?.MarketValue ?? m.facts.Value };
                BestRooms(m, a, map);
                u.BestRoom = a.BestRoom;
                u.BestStageNow = a.BestStageNow;
                u.BestStageWith = a.BestStageWith;
                u.BestIndexNow = a.BestIndexNow;
                u.BestIndexWith = a.BestIndexWith;
            }
            m.forWhomNote = up.bed ? u.Bedless > 0 ? W("CC_ItemCard_WhoBedless", u.BedlessWho.Named("WHO")) : W("CC_ItemCard_WhoAllBeds")
                : up.joyKind != null ? W(u.JoyNew ? "CC_ItemCard_WhoJoyNew" : "CC_ItemCard_WhoJoyHave", up.joyKind.label.Named("JOY"))
                : null;
        }

        private static void RowsFurniture(ItemCardModel m, UsePart up, UseFacts u)
        {
            m.use.Clear();
            if (u.Bed) m.use.Add((W("CC_ItemCard_Row_Bed"), W("CC_ItemCard_FurnitureBed", u.Rest.ToStringPercent().Named("REST"), u.Comfort.ToStringPercent().Named("COMFORT"))));
            else if (u.Seat) m.use.Add((W("CC_ItemCard_Row_Comfort"), u.Comfort.ToStringPercent()));
            if (u.Table) m.use.Add((W("CC_ItemCard_Row_Table"), W("CC_ItemCard_FurnitureTable")));
            if (u.WorkTable) m.use.Add((W("CC_ItemCard_Row_WorkSpeed"), W("CC_ItemCard_FurnitureBench", u.WorkSpeed.ToStringPercent().Named("SPEED"))));
            if (u.JoyKind.Length > 0) m.use.Add((W("CC_ItemCard_Row_Joy"), W("CC_ItemCard_FurnitureJoy", u.JoyKind.Named("JOY"))));
            if (u.Facility.Length > 0) m.use.Add((W("CC_ItemCard_Row_Facility"), u.Facility));
            if (Math.Abs(u.Power) > 0.5f) m.use.Add((W("CC_ItemCard_Row_Power"), W(u.Power > 0f ? "CC_ItemCard_PowerUses" : "CC_ItemCard_PowerMakes", ItemCardMath.Whole(Math.Abs(u.Power)).Named("W"))));
            m.use.Add((W("CC_ItemCard_Row_Beauty"), ItemCardMath.N((float)Math.Round(u.Beauty, 1)) + (u.Quality.Length > 0 ? " · " + W("CC_ItemCard_AtQuality", u.Quality.Named("QUALITY")) : "")));
            string room = u.Bed ? "Bed" : u.Table ? "Table" : u.JoyKind.Length > 0 ? "Joy" : u.WorkTable ? "Bench" : u.Seat ? "Seat" : "";
            if (room.Length > 0) m.use.Add((W("CC_ItemCard_Row_RoomNeeds"), W("CC_ItemCard_RoomNeeds_" + room)));
            if (up != null && up.recipes > 0 && u.WorkTable) m.use.Add((W("CC_ItemCard_Row_Recipes"), W("CC_ItemCard_FurnitureRecipes", up.recipes.Named("N"))));
            if (m.facts.HasColony) m.use.Add((W("CC_ItemCard_Row_AtHome"), W("CC_ItemCard_FurnitureAtHome", u.Placed.Named("PLACED"), u.Held.Named("CRATED"))));
        }

        // ---------------------------------------------------------------- anything else

        /// <summary>The stats its def sets that say something on their own (the card has worth and weight elsewhere).</summary>
        private static readonly HashSet<string> PlainStats = new HashSet<string>
        {
            "MarketValue", "Mass", "MaxHitPoints", "Flammability", "DeteriorationRate", "WorkToMake", "WorkToBuild", "SellPriceFactor", "Beauty", "Cleanliness", "Nutrition"
        };

        /// <summary>Its description's first sentences and its own stats (once a kind).</summary>
        private static void ReadOther(ThingDef d, UsePart up)
        {
            string desc = d.description ?? "";
            int cut = desc.IndexOf("\n", StringComparison.Ordinal);
            if (cut > 0) desc = desc.Substring(0, cut);
            if (desc.Length > 320)
            {
                int dot = desc.LastIndexOf(". ", 320, StringComparison.Ordinal);
                desc = dot > 40 ? desc.Substring(0, dot + 1) : desc.Substring(0, 320).TrimEnd() + "...";
            }
            up.description = desc.Trim();
            foreach (var sm in d.statBases ?? new List<StatModifier>())
            {
                if (up.stats.Count >= 6) break;
                if (sm?.stat == null || PlainStats.Contains(sm.stat.defName) || Math.Abs(sm.value) < 0.0001f) continue;
                try
                {
                    up.stats.Add((sm.stat.LabelCap, sm.stat.ValueToString(sm.value)));
                }
                catch
                {
                    // A mod's stat that can't be put in words: left out.
                }
            }
        }

        /// <summary>An unfinished thing in hand: what it will be, the work left, its bench and who began it (once a card).</summary>
        private static void LiveOther(ItemCardModel m, UsePart up, UseFacts u)
        {
            if (!(m.thing is UnfinishedThing uft)) return;
            u.Unfinished = true;
            try
            {
                u.Product = uft.Recipe?.ProducedThingDef?.label ?? uft.Recipe?.label ?? "";
                u.WorkLeft = uft.workLeft;
                u.Bench = uft.BoundWorkTable?.def?.label ?? "";
                if (uft.Creator != null) m.useNote = W("CC_ItemCard_UnfinishedBy", uft.Creator.LabelShort.Named("WHO"));
            }
            catch
            {
                // What it will be can't be read: its kind alone.
            }
        }

        private static void RowsOther(ItemCardModel m, UsePart up, UseFacts u)
        {
            m.use.Clear();
            if (u.Unfinished)
            {
                m.use.Add((W("CC_ItemCard_Row_WillBe"), u.Product.Length > 0 ? ItemCardMath.Cap(u.Product) : "?"));
                m.use.Add((W("CC_ItemCard_Row_WorkLeft"), W("CC_ItemCard_WorkLeft", ItemCardMath.Whole(u.WorkLeft).Named("WORK"))
                                                          + (u.Bench.Length > 0 ? " " + W("CC_ItemCard_WorkAt", u.Bench.Named("BENCH")) : "")));
            }
            if (up != null)
            {
                foreach (var (label, value) in up.stats) m.use.Add((label, value));
                if (!u.Unfinished && !up.description.NullOrEmpty() && m.useNote == null) m.useNote = up.description;
            }
        }
    }
}
